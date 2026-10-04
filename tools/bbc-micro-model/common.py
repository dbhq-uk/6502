"""What the BBC Micro model tools share: where things are, the inputs and their
hashes, reading an image, finding the tinned pads on a scan of the board,
fitting one set of points to another with an error measured on points held
out of the fit, and writing the data files.

Nothing here fetches anything. The inputs (scans, photographs and the KiCad
keyboard) are fetched by hand into the folder BBC_MODEL_INPUTS names, as the
README says, and every one is checked against the SHA-256 in
data/sources.json before it is read.

The board frame is millimetres from the board's left rear corner, the
component side seen from above with the rear connectors at the far edge: x to
the right, y towards the front. Scan I1 is already that way up, so its pixels
divided by its x and y scales are the board frame, less an offset.
"""
import hashlib
import json
import math
import os
import sys
from pathlib import Path

import numpy as np

TOOL = Path(__file__).resolve().parent
REPO = TOOL.parent.parent
DATA = TOOL / 'data'
OUT = TOOL / 'out'


# --- inputs ---------------------------------------------------------------------

def inputs_dir() -> Path:
    """The folder of inputs, from $BBC_MODEL_INPUTS; exits naming the variable
    if it is unset or is not a folder."""
    value = os.environ.get('BBC_MODEL_INPUTS')
    if not value:
        sys.exit('BBC_MODEL_INPUTS is not set: point it at the folder the inputs were fetched into (see tools/bbc-micro-model/README.md)')
    path = Path(value).expanduser()
    if not path.is_dir():
        sys.exit(f'BBC_MODEL_INPUTS is {value}, which is not a folder')
    return path


def sources() -> list:
    """Every input, from data/sources.json."""
    with open(DATA / 'sources.json', encoding='utf8') as f:
        return json.load(f)['sources']


def source(source_id: str) -> dict:
    for s in sources():
        if s['id'] == source_id:
            return s
    print(f'{source_id}: not in data/sources.json', file=sys.stderr)
    sys.exit(2)


def sha256(path) -> str:
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def original(source_id: str) -> Path:
    """The input's path in BBC_MODEL_INPUTS. Refuses (exit 2) unless the file
    is there and its SHA-256 is the one recorded in data/sources.json."""
    s = source(source_id)
    path = inputs_dir() / s['file']
    if not path.is_file():
        print(f'{source_id}: {path} is missing; fetch it by hand as tools/bbc-micro-model/README.md says', file=sys.stderr)
        sys.exit(2)
    got = sha256(path)
    if got != s['sha256']:
        print(f'{source_id}: {path} has SHA-256 {got}, not the recorded {s["sha256"]}', file=sys.stderr)
        sys.exit(2)
    return path


def read_rgb(source_id: str, mirror=False) -> np.ndarray:
    """An input image as an RGB uint8 array, checked first. `mirror` flips it
    left to right (the solder side, so that it lies as the component side)."""
    from PIL import Image
    Image.MAX_IMAGE_PIXELS = None
    img = Image.open(original(source_id)).convert('RGB')
    if mirror:
        img = img.transpose(Image.FLIP_LEFT_RIGHT)
    return np.asarray(img)


# --- the tinned pads on a scan ------------------------------------------------

def oklab(rgb):
    """RGB uint8 (H, W, 3) to OKLab float32 L, a, b."""
    c = rgb.astype(np.float32) / 255
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    l = np.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
    m = np.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
    s = np.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)
    L = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s
    A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s
    B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s
    return L, A, B


# A pad on these scans is solder: grey (almost no colour) and light, on green
# lacquer (strongly coloured) or the yellow print. The thresholds were read off
# the scans (the 1st to 99th percentiles of a crop of pads and lacquer).
PAD_MIN_L, PAD_MAX_CHROMA = 0.55, 0.045
PAD_AREA_PX = (200, 1100)      # a 1.5 to 1.9 mm pad at about 15.7 px/mm is 350 to 700 px


def pad_mask(rgb):
    """1 where a pixel is solder, with the dark specks inside a pad filled and
    anything thinner than a pad (track edges, print) opened away."""
    import cv2
    L, A, B = oklab(rgb)
    m = ((np.hypot(A, B) < PAD_MAX_CHROMA) & (L > PAD_MIN_L)).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    n, lab, st, _ = cv2.connectedComponentsWithStats((1 - m).astype(np.uint8), 8)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])).tolist())
    holes = [i for i in range(1, n) if st[i, 4] < 400 and i not in edge]
    m = m | np.isin(lab, holes).astype(np.uint8)
    return cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (11, 11)))


def find_pads(rgb, origin=(0, 0)):
    """The centroid of every round blob of solder the size of a pad, as an
    (N, 2) array of pixel coordinates (plus `origin`). A centroid is the mean
    of the blob's pixels after its specks are filled: the pad's centre, and so
    the hole's, as far as the solder on it is centred."""
    import cv2
    n, _, st, cen = cv2.connectedComponentsWithStats(pad_mask(rgb), 8)
    out = []
    for i in range(1, n):
        _, _, w, h, a = st[i]
        if not PAD_AREA_PX[0] <= a <= PAD_AREA_PX[1] or max(w, h) > 1.5 * min(w, h):
            continue
        if a / (math.pi * (max(w, h) / 2) ** 2) < 0.7:
            continue
        out.append((cen[i][0] + origin[0], cen[i][1] + origin[1]))
    return np.array(out, float).reshape(-1, 2)


def refine_to_pad(rgb, x, y, px_per_mm, limit_mm=0.6):
    """A point marked by hand on a pad, moved to the centroid of the pad blob
    nearest it (find_pads in a 3 mm window). Returns (x, y, moved_mm), or None
    when no pad's centroid is within `limit_mm` of the mark."""
    half = int(round(1.5 * px_per_mm))
    x0, y0 = int(round(x)) - half, int(round(y)) - half
    pads = find_pads(rgb[max(y0, 0):y0 + 2 * half + 1, max(x0, 0):x0 + 2 * half + 1], (max(x0, 0), max(y0, 0)))
    if not len(pads):
        return None
    d = np.hypot(pads[:, 0] - x, pads[:, 1] - y)
    i = int(np.argmin(d))
    moved = d[i] / px_per_mm
    if moved > limit_mm:
        return None
    return float(pads[i, 0]), float(pads[i, 1]), float(moved)


# --- the KiCad keyboard (K1) -----------------------------------------------------

def _sexpr(text):
    """A KiCad file as nested lists (the KIM-1 tools' kicad.parse, adapted)."""
    import re
    token = re.compile(r'\(|\)|"(?:[^"\\]|\\.)*"|[^\s()"]+')
    stack = [[]]
    for m in token.finditer(text):
        t = m.group(0)
        if t == '(':
            stack.append([])
        elif t == ')':
            done = stack.pop()
            stack[-1].append(done)
        else:
            stack[-1].append(t[1:-1] if t.startswith('"') else t)
    return stack[0][0]


def _child(node, name):
    return next((c for c in node[1:] if isinstance(c, list) and c and c[0] == name), None)


def switch_centres(path) -> dict:
    """Every key switch in a KiCad board: {reference: {"value", "centre": (x, y),
    "footprint"}}, in KiCad's millimetres (x to the right, y down, from above).
    The centre is the switch's 4 mm locating hole, which is the middle of the
    switch whatever the footprint's origin is."""
    import re
    root = _sexpr(Path(path).read_text(encoding='utf8'))
    out = {}
    for fp in root[1:]:
        if not (isinstance(fp, list) and fp and fp[0] == 'footprint' and 'Cherry_MX_PCB' in fp[1]):
            continue
        at = _child(fp, 'at')
        fx, fy = float(at[1]), float(at[2])
        rot = math.radians(-float(at[3])) if len(at) > 3 else 0.0
        props = {p[1]: p[2] for p in fp[1:] if isinstance(p, list) and p and p[0] == 'property'}
        for t in fp[1:]:   # KiCad 6 keeps them as fp_text, KiCad 8 as property
            if isinstance(t, list) and t and t[0] == 'fp_text' and t[1] in ('reference', 'value'):
                props.setdefault(t[1].capitalize(), t[2])
        holes = []
        for pad in fp[1:]:
            if isinstance(pad, list) and pad and pad[0] == 'pad' and pad[2] == 'np_thru_hole':
                size = float(_child(pad, 'size')[1])
                px, py = float(_child(pad, 'at')[1]), float(_child(pad, 'at')[2])
                holes.append((size, fx + px * math.cos(rot) - py * math.sin(rot), fy + px * math.sin(rot) + py * math.cos(rot)))
        size, x, y = max(holes)
        if abs(size - 4.0) > 1e-6:
            raise ValueError(f'{props.get("Reference")}: no 4 mm locating hole')
        out[props['Reference']] = {'value': props.get('Value'), 'centre': (x, y), 'footprint': re.sub(r'^.*:', '', fp[1])}
    return out


# --- fits ---------------------------------------------------------------------

MODELS = ('similarity', 'affine', 'homography', 'cubic')
MIN_POINTS = {'similarity': 2, 'affine': 3, 'homography': 4, 'cubic': 14}


def _homogeneous(H, pts):
    p = np.c_[pts, np.ones(len(pts))] @ H.T
    return p[:, :2] / p[:, 2:3]


def _dlt(src, dst):
    """The homography src -> dst by the normalised direct linear transform."""
    def norm(p):
        c = p.mean(0)
        s = math.sqrt(2) / max(np.sqrt(((p - c) ** 2).sum(1)).mean(), 1e-12)
        return np.array([[s, 0, -s * c[0]], [0, s, -s * c[1]], [0, 0, 1]])
    Ts, Td = norm(src), norm(dst)
    a, b = _homogeneous(Ts, src), _homogeneous(Td, dst)
    rows = []
    for (x, y), (u, v) in zip(a, b):
        rows.append([-x, -y, -1, 0, 0, 0, u * x, u * y, u])
        rows.append([0, 0, 0, -x, -y, -1, v * x, v * y, v])
    _, _, vt = np.linalg.svd(np.array(rows))
    H = np.linalg.inv(Td) @ vt[-1].reshape(3, 3) @ Ts
    return H / H[2, 2]


def _fit_homography(src, dst):
    """The direct linear transform, then refined to the least squares of the
    distances in dst (the algebraic error of the transform is not a distance)."""
    from scipy.optimize import least_squares
    H = _dlt(src, dst)
    if len(src) == 4:
        return H
    scale = np.abs(H).max()
    r = least_squares(lambda h: (_homogeneous(np.append(h, 1.0).reshape(3, 3), src) - dst).ravel(),
                      (H / H[2, 2]).ravel()[:8], x_scale=np.maximum(np.abs((H / H[2, 2]).ravel()[:8]), 1e-6 * scale),
                      method='lm', xtol=1e-15, ftol=1e-15, gtol=1e-15, max_nfev=2000)
    return np.append(r.x, 1.0).reshape(3, 3)


def _cubic_terms(p, centre, half):
    x, y = ((p - centre) / half).T
    return np.stack([np.ones_like(x), x, y, x * x, x * y, y * y, x ** 3, x * x * y, x * y * y, y ** 3], -1)


def _fit(model, src, dst):
    """Parameters taking src to dst, fitted by least squares."""
    n = len(src)
    if model == 'similarity':
        A = np.zeros((2 * n, 4))
        A[0::2] = np.c_[src[:, 0], -src[:, 1], np.ones(n), np.zeros(n)]
        A[1::2] = np.c_[src[:, 1], src[:, 0], np.zeros(n), np.ones(n)]
        a, b, tx, ty = np.linalg.lstsq(A, dst.ravel(), rcond=None)[0]
        M = np.array([[a, -b, tx], [b, a, ty]])
        return {'matrix': M.tolist(), 'scale': math.hypot(a, b), 'rotationDeg': math.degrees(math.atan2(b, a))}
    if model == 'affine':
        X = np.c_[src, np.ones(n)]
        M = np.linalg.lstsq(X, dst, rcond=None)[0].T
        return {'matrix': M.tolist()}
    H = _fit_homography(src, dst)
    if model == 'homography':
        return {'H': H.tolist()}
    if model == 'cubic':
        base = _homogeneous(H, src)
        centre = (base.max(0) + base.min(0)) / 2
        half = np.maximum((base.max(0) - base.min(0)) / 2, 1e-9)
        T = _cubic_terms(base, centre, half)
        cx = np.linalg.lstsq(T, dst[:, 0] - base[:, 0], rcond=None)[0]
        cy = np.linalg.lstsq(T, dst[:, 1] - base[:, 1], rcond=None)[0]
        return {'H': H.tolist(), 'centre': centre.tolist(), 'half': half.tolist(), 'poly': {'x': cx.tolist(), 'y': cy.tolist()}}
    raise ValueError(f'model is one of {MODELS}, not {model!r}')


def transform(model, params, pts):
    """Applies fitted parameters to points (N, 2)."""
    pts = np.asarray(pts, float).reshape(-1, 2)
    if model in ('similarity', 'affine'):
        M = np.array(params['matrix'])
        return pts @ M[:, :2].T + M[:, 2]
    base = _homogeneous(np.array(params['H']), pts)
    if model == 'homography':
        return base
    T = _cubic_terms(base, np.array(params['centre']), np.array(params['half']))
    return base + np.c_[T @ np.array(params['poly']['x']), T @ np.array(params['poly']['y'])]


def stats(errors):
    e = np.asarray(errors, float)
    return {'median': float(np.median(e)), 'p90': float(np.percentile(e, 90)), 'max': float(e.max())}


def fit_held_out(src, dst, model: str, folds) -> dict:
    """Fits `model` taking src to dst, and measures it on points held out of
    the fit.

    src and dst are (N, 2) sequences, or dicts with the same keys (the keys
    are then the points' ids; otherwise an id is the index). model is
    "similarity", "affine", "homography" or "cubic" (a homography, then a
    cubic correction in x and y fitted to what it leaves). folds is
    "leave-one-out" (each point held out in turn) or a sequence of one label
    per point: each label's points are held out together, scored by a fit to
    all the others (a chequerboard of blocks, say).

    Errors are distances in dst's units (millimetres here). Returns n, the
    parameters fitted to every point, fitMm (the errors of that fit),
    heldOutMm (each point's error under the fit it was held out of),
    heldOutEach ([id, mm] for every point, in order) and worst (the ten
    largest held-out errors, largest first)."""
    if isinstance(src, dict):
        ids = list(src)
        src = np.array([src[i] for i in ids], float)
        dst = np.array([dst[i] for i in ids], float)
    else:
        src, dst = np.asarray(src, float).reshape(-1, 2), np.asarray(dst, float).reshape(-1, 2)
        ids = list(range(len(src)))
    if model not in MODELS:
        raise ValueError(f'model is one of {MODELS}, not {model!r}')
    n = len(src)
    labels = list(range(n)) if folds == 'leave-one-out' else list(folds)
    if len(labels) != n:
        raise ValueError('one fold label per point')
    labels = np.array(labels, dtype=object)
    held = np.full(n, np.nan)
    for label in dict.fromkeys(labels.tolist()):
        out = labels == label
        if (~out).sum() < MIN_POINTS[model]:
            raise ValueError(f'fold {label!r} leaves {(~out).sum()} points to fit a {model}')
        p = _fit(model, src[~out], dst[~out])
        held[out] = np.hypot(*(transform(model, p, src[out]) - dst[out]).T)
    params = _fit(model, src, dst)
    fit = np.hypot(*(transform(model, params, src) - dst).T)
    order = np.argsort(-held, kind='stable')
    return {
        'n': n,
        'params': params,
        'fitMm': stats(fit),
        'heldOutMm': stats(held),
        'heldOutEach': [[ids[i], float(held[i])] for i in range(n)],
        'worst': [[ids[i], float(held[i])] for i in order[:10]],
    }


# --- data files ---------------------------------------------------------------

def plain(obj, places=4):
    """obj as plain Python: dicts, lists, str, bool, None, int and floats
    rounded to `places` places. numpy numbers become Python numbers; NaN and
    infinity are refused (ValueError), and so is anything else (TypeError)."""
    if isinstance(obj, dict):
        return {str(k): plain(v, places) for k, v in obj.items()}
    if isinstance(obj, (list, tuple, np.ndarray)):
        return [plain(v, places) for v in obj]
    if obj is None or isinstance(obj, (str, bool, np.bool_)):
        return bool(obj) if isinstance(obj, np.bool_) else obj
    if isinstance(obj, (int, np.integer)):
        return int(obj)
    if isinstance(obj, (float, np.floating)):
        if not math.isfinite(obj):
            raise ValueError(f'{obj} is not a finite number')
        r = round(float(obj), places)
        return 0.0 if r == 0 else r
    raise TypeError(f'{type(obj).__name__} is not plain data')


def _rows_per_line(obj, depth=0):
    """JSON with dicts indented, and a list of dicts or lists one element to a
    line (a long table stays readable, and small)."""
    pad = ' ' * (depth + 1)
    if isinstance(obj, dict) and obj:
        items = [f'{pad}{json.dumps(k)}: {_rows_per_line(v, depth + 1)}' for k, v in sorted(obj.items())]
        return '{\n' + ',\n'.join(items) + '\n' + ' ' * depth + '}'
    if isinstance(obj, list) and obj and all(isinstance(v, (dict, list)) for v in obj):
        rows = [pad + json.dumps(v, sort_keys=True, ensure_ascii=False, separators=(',', ':')) for v in obj]
        return '[\n' + ',\n'.join(rows) + '\n' + ' ' * depth + ']'
    return json.dumps(obj, sort_keys=True, ensure_ascii=False, separators=(', ', ': '))


def write_data(name: str, obj, places=4, rows_per_line=False) -> None:
    """data/<name>: keys sorted, floats to `places` places (4 unless said),
    plain Python numbers only. rows_per_line writes a list of records one to
    a line, for a file with long tables."""
    if rows_per_line:
        text = _rows_per_line(plain(obj, places))
        json.loads(text)
        Path(DATA).mkdir(parents=True, exist_ok=True)
        (Path(DATA) / name).write_text(text + '\n', encoding='utf8')
        return
    text = json.dumps(plain(obj), indent=1, sort_keys=True, ensure_ascii=False)
    Path(DATA).mkdir(parents=True, exist_ok=True)
    (Path(DATA) / name).write_text(text + '\n', encoding='utf8')
