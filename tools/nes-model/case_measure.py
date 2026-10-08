"""Task 8: the case, measured, for both consoles: the NTSC NES-001 and the PAL
NESE-001.

    NES_MODEL_INPUTS=<folder> python case_measure.py

Writes data/case.json and site/src/models/nes-famicom-case-parts.mjs, and
pictures to look at in out/ (git-ignored). The plan is
docs/superpowers/plans/2026-10-05-nes-models.md, task 8, as revised twice on
5 October 2026.

The case frame is millimetres from the case's left rear corner at table
level, x to the right as seen from the front, y towards the front, z up.

1. The size is the published 254 by 203.2 by 88.9 mm (docs/nes/facts/models.md,
   D1 and D2), which are not Nintendo's figures; how well they are known is
   case.json's footprint.goodToPct, with its reason beside it; task 0 checked
   the patent's proportions against them within 5. The corner photographs
   give features only: their camera failed its own check
   in task 0, so nothing here relates two of a photograph's planes through a
   camera. Each face is rectified on its own, by the homography that takes
   the four lines bounding it to the published rectangle, which any camera,
   and any perspective correction made in Photoshop, leaves exact for points
   on that face (lens distortion apart).
2. The profile. The case's front and rear faces are flat and upright (the
   patent's side views, FIG 7 and FIG 8, show both edges straight), so its
   side outline is a rectangle; what varies is the ends of the bottom shell,
   which lean in below the seam (FIG 3 and FIG 4). The profile is that end
   seen from the front: points (inset from the end, z). Built from O2-FL's
   front face; its check is the inset at the base read on O2-BR's rear face,
   held out of it, within PROFILE_LIMIT_MM; past that, the patent's FIG 3
   scaled to the case is used instead, and case.json says so.
3. Features on the faces: the front (O2-FL), the top (O2-FL's top, on its
   four corners), the rear (O2-BR); each is also read on the patent's view
   of that face scaled to the case, and the two are recorded side by side.
   The underside from the patent's FIG 6, with O5 for the PAL console.
4. The board in the case: from a view with the board and the case together,
   the PAL set's HOF06601 (O3, Evan-Amos's set, never shows the whole board
   and the case at once), by the two planes and the nadir (board_in_case).
   The rear connectors are placed on the modulator's face (I7-FL) on the
   board, plus that offset, and each is checked against where O2-BR shows it.
"""
import json
import math
import re
import sys

import numpy as np

import common

WIDTH_MM, DEPTH_MM, HEIGHT_MM = 254.0, 203.2, 88.9     # published, not Nintendo's (models.md D1, D2)
PROFILE_LIMIT_MM = 2.0                                  # the plan's: the profile's held-out point
REAR_LIMIT_MM = 2.0                                     # the plan's: a rear connector against its board place plus the offset

EXPORTS = ('CASE', 'PROFILE', 'DOOR', 'VENTS', 'BUTTONS', 'LED', 'PORTS', 'REAR', 'LABELS', 'UNDERSIDE', 'FEET')
CASE_MJS = common.REPO / 'site' / 'src' / 'models' / 'nes-famicom-case-parts.mjs'


# --- lines and faces ---------------------------------------------------------------------------

def fit_line(pts):
    """The least-squares line through points: (a point on it, its unit direction)."""
    pts = np.asarray(pts, float)
    m = pts.mean(0)
    d = np.linalg.svd(pts - m)[2][0]
    return m, d


def meet(a, b):
    """Where two lines (point, direction) cross."""
    s = np.linalg.solve(np.c_[a[1], -b[1]], b[0] - a[0])
    return a[0] + a[1] * s[0]


def corners_from_lines(ln):
    """A face's four corners from its bounding lines `top`, `right`, `base`
    and `left`: top left, top right, base right, base left (as seen)."""
    return np.array([meet(ln['left'], ln['top']), meet(ln['right'], ln['top']),
                     meet(ln['right'], ln['base']), meet(ln['left'], ln['base'])])


def face_homography(C, a, b=None):
    """The homography from a picture to a face. With a width and a height,
    the four corners (top left, top right, base right, base left) go to
    (0, b), (a, b), (a, 0), (0, 0): u along the face from its left end as
    seen, z up. Otherwise `a` is the four points they go to."""
    dst = np.array([[0, b], [a, b], [a, 0], [0, 0]], float) if b is not None else np.asarray(a, float)
    return common._dlt(np.asarray(C, float), dst)


def to_face(G, pts):
    """Points (or one homogeneous 3-vector, a point at any distance) through G."""
    pts = np.asarray(pts, float)
    if pts.ndim == 1 and len(pts) == 3:
        q = G @ pts
        return (q[:2] / q[2])[None, :]
    return common.transform('homography', {'H': G}, np.atleast_2d(pts))


# --- a box from one corner photograph (recorded only on the real photographs) ----------------
#
# The method of task 0's spike.py: a pinhole with square pixels and its
# principal point at the picture's centre; the focal length for which the
# top's two vanishing points are at right angles; the top's homography from
# the unit square gives depth over width; points on the near long face's
# bottom edge, taken through that face's plane, give height over width.

def _unit_square(C):
    return common._dlt(np.array([[0, 0], [1, 0], [1, 1], [0, 1]], float), np.asarray(C, float))


def box_from_photo(C, c, base_pts):
    """C: the top's corners, the near long edge first (its two ends), then
    the far long edge's (in the same turn). c: the principal point.
    base_pts: points along the near long face's bottom edge."""
    h = lambda p: np.array([p[0], p[1], 1.0])
    v1 = np.cross(np.cross(h(C[0]), h(C[1])), np.cross(h(C[3]), h(C[2])))
    v2 = np.cross(np.cross(h(C[0]), h(C[3])), np.cross(h(C[1]), h(C[2])))
    v1, v2 = v1[:2] / v1[2], v2[:2] / v2[2]
    f = math.sqrt(-float(np.dot(v1 - c, v2 - c)))
    K = np.array([[f, 0, c[0]], [0, f, c[1]], [0, 0, 1.0]])
    M = np.linalg.inv(K) @ _unit_square(C)
    if M[2, 2] < 0:
        M = -M
    Wn, Dn = np.linalg.norm(M[:, 0]), np.linalg.norm(M[:, 1])
    n = np.cross(M[:, 0], M[:, 1])
    n /= np.linalg.norm(n)
    X0, X1 = M[:, 2], M[:, 0] + M[:, 2]
    if np.dot(n, -X0) < 0:
        n = -n
    face = np.cross(X1 - X0, n)
    below = []
    for p in base_pts:
        r = np.linalg.inv(K) @ h(p)
        X = r * np.dot(X0, face) / np.dot(r, face)
        below.append(float(np.dot(n, X0 - X)) / Wn)
    return {'depthToWidth': float(Dn / Wn), 'heightToWidth': float(np.median(below)), 'focalPx': f}


# --- the board in the case ---------------------------------------------------------------------
#
# The board lies in the bottom shell solder side up: turned over about its x
# axis, so board (bx, by) is at case (x0 + bx, y0 - by), give or take a small
# turn. In one picture of both, the rim's homography (picture to the rim's
# plane in case millimetres) takes a point on the board, lower down, to where
# the camera's ray through it meets the rim's plane: p' = F + k (P - F), F the
# point of the rim's plane straight below the camera (the picture's nadir, the
# vertical vanishing point, through the same homography) and k the ratio of
# the camera's heights over the rim and over the board. So p' is a
# similarity of the board's points, scale k and offset F (1 - k) + k T, and
# with F known, T = (x0, y0) follows.

def _similarity(src, dst):
    """dst ~ s R src + t, R a rotation (Umeyama): s, R, t, residuals."""
    ms, md = src.mean(0), dst.mean(0)
    A, B = src - ms, dst - md
    U, S, Vt = np.linalg.svd(B.T @ A)
    E = np.diag([1, np.sign(np.linalg.det(U @ Vt))])
    R = U @ E @ Vt
    s = float(np.trace(np.diag(S) @ E) / (A ** 2).sum())
    t = md - s * R @ ms
    res = np.hypot(*(dst - (s * (R @ src.T).T + t)).T)
    return s, R, t, res


def board_in_case(img, board_pts, Hr, nadir):
    """The board's place in the case from one picture: `img` the board
    points' places in it, `board_pts` theirs in the board frame, `Hr` the
    picture to the rim's plane (case millimetres), `nadir` the vertical
    vanishing point (homogeneous, as it may be far off the picture)."""
    p = to_face(Hr, img)
    F = to_face(Hr, np.asarray(nadir, float))[0]
    m = np.c_[board_pts[:, 0], -board_pts[:, 1]]
    k, R, t, res = _similarity(m, p)
    T = (t - (1 - k) * F) / k
    return {'x': float(T[0]), 'y': float(T[1]), 'k': k, 'turnDeg': math.degrees(math.atan2(R[1, 0], R[0, 0])),
            'nadirMm': F.tolist(), 'fitMm': common.stats(res / k)}


def board_to_case(pts, placed):
    t = math.radians(placed.get('turnDeg', 0.0))
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    pts = np.atleast_2d(np.asarray(pts, float))
    return np.c_[pts[:, 0], -pts[:, 1]] @ R.T + [placed['x'], placed['y']]


def case_to_board(pts, placed):
    t = math.radians(placed.get('turnDeg', 0.0))
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    m = (np.atleast_2d(np.asarray(pts, float)) - [placed['x'], placed['y']]) @ R
    return np.c_[m[:, 0], -m[:, 1]]


def rear_u_to_x(u, width=WIDTH_MM):
    """A place along the rear face from its left end as seen from behind,
    which is the case's right end, to the case's x."""
    return width - u


# --- the profile -------------------------------------------------------------------------------

def _inset_at(profile, z):
    pts = sorted(profile, key=lambda p: p['z'])
    zs = [p['z'] for p in pts]
    return float(np.interp(z, zs, [p['inset'] for p in pts]))


def profile_choice(built, check, patent):
    """The end profile built from the photographs, judged on one point held
    out of it; past PROFILE_LIMIT_MM the patent's, scaled to the case."""
    err = abs(check['inset'] - _inset_at(built, check['z']))
    ok = err <= PROFILE_LIMIT_MM
    return {'profile': built if ok else patent, 'from': 'photographs' if ok else 'patent',
            'check': {'errMm': err, 'limitMm': PROFILE_LIMIT_MM, 'passes': ok}}


# --- words -------------------------------------------------------------------------------------

LABEL_KEYS = {'words', 'box', 'face', 'from', 'note', 'size'}


def label_problems(labels):
    """Why a list of labels is not words alone: the case's words are drawn in
    the site's own face, never as a logo's shape, a path or an image."""
    out = []
    for i, lab in enumerate(labels):
        extra = set(lab) - LABEL_KEYS
        if extra:
            out.append(f'label {i} has {sorted(extra)}')
        w = lab.get('words')
        if not isinstance(w, str) or not w.strip() or re.search(r'[<>{}\\]', w):
            out.append(f'label {i}: {w!r} is not words')
        box = lab.get('box')
        if not (isinstance(box, list) and len(box) == 4 and all(isinstance(v, (int, float)) for v in box)):
            out.append(f'label {i}: its box is not four numbers')
    return out


# --- the module the site reads -----------------------------------------------------------------

COLOUR = re.compile(r'#[0-9a-fA-F]{3,8}\b|\b(rgb|hsl)a?\(')


def _js(obj):
    return json.dumps(common.plain(obj, 3), ensure_ascii=False, separators=(',', ':'))


def parts_module(exports, sources):
    """The generated module: every name in EXPORTS, each with its note, and
    no colour (the model's colours are tokens, read by the model)."""
    missing = [k for k in EXPORTS if k not in exports]
    if missing:
        raise ValueError(f'the module lacks {missing}')
    for k, v in exports.items():
        text = json.dumps(v)
        if COLOUR.search(text) or re.search(r'"colou?r"', text):
            raise ValueError(f'{k} holds a colour')
        if 'note' not in v:
            raise ValueError(f'{k} has no note')
    head = [
        '// GENERATED by tools/nes-model/case_measure.py: do not edit by hand. Run it',
        '// again instead (tools/nes-model/README.md). Millimetres in the case frame:',
        '// from the case\'s left rear corner at table level, x to the right as seen',
        '// from the front, y towards the front, z up. The size is the published',
        '// 254 by 203.2 by 88.9 mm, which are not Nintendo\'s figures. Each export',
        '// carries a note saying where it came from. No colour here: the model\'s',
        '// colours are tokens.',
        '//',
        '// Sources (tools/nes-model/data/sources.json): ' + ', '.join(sources) + '.',
        '',
    ]
    body = [f'export const {k} = {_js(exports[k])};' for k in EXPORTS]
    return '\n'.join(head + body) + '\n'


# ================================================================================================
# The measurements. Everything below reads the inputs; everything above is
# geometry, tested in tests/test_case.py on made-up scenes.
# ================================================================================================

def marks():
    return json.loads((common.DATA / 'marks.json').read_text(encoding='utf8'))['caseFeatures']


def _spike():
    import spike
    return spike


def _lines(L, segs, dark=()):
    sp = _spike()
    out, fit = {}, {}
    for k, seg in segs.items():
        m, d, rms, kept, n = sp.edge_line(L, seg[0], seg[1], half=seg[2] if len(seg) > 2 else 15, dark=k in dark)
        out[k] = (m, d)
        fit[k] = {'rmsPx': rms, 'points': n, 'kept': kept}
    return out, fit


def _line_in(G, line, span=400):
    """A picture's line through G: (a point, its unit direction) on the face."""
    m, d = line
    p = to_face(G, [m - d * span, m + d * span])
    return fit_line(p)


def _u_at_z(line, z):
    m, d = line
    return float(m[0] + d[0] * (z - m[1]) / d[1])


def _z_at_u(line, u):
    m, d = line
    return float(m[1] + d[1] * (u - m[0]) / d[0])


def _box(pts):
    p = np.asarray(pts, float)
    return [float(p[:, 0].min()), float(p[:, 1].min()), float(p[:, 0].max() - p[:, 0].min()), float(p[:, 1].max() - p[:, 1].min())]


# --- 1. the patent's views, scaled to the published case -------------------------------------
#
# Each view's body is its extent as task 0 measured it (spike.view_extent:
# across, its outermost drawn pixels; down, its first long line to its last,
# so the feet below the front and side views are left out). Each axis is
# scaled to the published size on its own, so the drawing's own proportions
# (1 to 3 per cent off the published ones, task 0) do not move a feature off
# the case; the scales are recorded.

class View:
    def __init__(self, ext, across_mm, down_mm):
        x0, y0, x1, y1 = ext['box']
        self.x0, self.y0, self.x1, self.y1 = x0, y0, x0 + ext['acrossPx'] - 1, y0 + ext['downPx'] - 1
        self.sx = across_mm / ext['acrossPx']
        self.sy = down_mm / ext['downPx']
        self.ext = ext

    def a(self, px):          # across, from the view's left
        return (px - self.x0) * self.sx

    def b(self, py):          # down, from the view's top
        return (py - self.y0) * self.sy


def patent_views():
    sp = _spike()
    m = marks()['patent']
    pages = sp.patent_pages()
    ext = {k: sp.view_extent(pages[m['pages'][k]], m['boxes'][k]) for k in m['boxes']}
    v = {'FIG 3': View(ext['FIG 3'], WIDTH_MM, HEIGHT_MM), 'FIG 4': View(ext['FIG 4'], WIDTH_MM, HEIGHT_MM),
         'FIG 5': View(ext['FIG 5'], WIDTH_MM, DEPTH_MM), 'FIG 6': View(ext['FIG 6'], WIDTH_MM, DEPTH_MM),
         'FIG 7': View(ext['FIG 7'], DEPTH_MM, HEIGHT_MM)}
    return pages, v, m


def patent_profile(page, view, every_mm=2.0):
    """The end profile on FIG 3: each row's outermost drawn pixel at each
    end, from the top to the base; insets averaged over the two ends."""
    import cv2
    x0, y0, x1, y1 = view.ext['box']
    b = page[view.y0:view.y1 + 1, x0 - 5:x1 + 6].astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(b, 8)
    keep = np.isin(lab, [i for i in range(1, n) if st[i, 4] >= 50])
    rows = []
    for r in range(keep.shape[0]):
        xs = np.nonzero(keep[r])[0]
        if len(xs):
            left = (x0 - 5 + xs.min()) - view.x0
            right = view.x1 - (x0 - 5 + xs.max())
            rows.append((r, left * view.sx, right * view.sx))
    rows = np.array(rows)
    z = HEIGHT_MM - rows[:, 0] * view.sy
    inset = (rows[:, 1] + rows[:, 2]) / 2
    out = []
    for zz in np.arange(0, HEIGHT_MM + 0.01, every_mm):
        i = int(np.argmin(np.abs(z - zz)))
        out.append({'inset': float(max(inset[i], 0.0)), 'z': float(zz)})
    each = {'left': [{'inset': float(rows[i, 1]), 'z': float(z[i])} for i in range(0, len(rows), 20)],
            'right': [{'inset': float(rows[i, 2]), 'z': float(z[i])} for i in range(0, len(rows), 20)]}
    return out, each


def patent_features():
    pages, v, m = patent_views()
    f3, f4, f5, f6, f7 = (v[k] for k in ('FIG 3', 'FIG 4', 'FIG 5', 'FIG 6', 'FIG 7'))
    feet_h = (f3.ext['overallDownPx'] - f3.ext['downPx']) * f3.sy
    z3 = lambda py: feet_h + HEIGHT_MM - f3.b(py)               # FIG 3: z in the case frame
    box3 = lambda b: {'x': f3.a(b[0]), 'z': z3(b[3]), 'w': (b[2] - b[0]) * f3.sx, 'h': (b[3] - b[1]) * f3.sy}
    z4 = lambda py: feet_h + HEIGHT_MM - f4.b(py)
    box4 = lambda b: {'x': WIDTH_MM - f4.a(b[2]), 'z': z4(b[3]), 'w': (b[2] - b[0]) * f4.sx, 'h': (b[3] - b[1]) * f4.sy}
    box5 = lambda b: {'x': f5.a(b[0]), 'y': f5.b(b[1]), 'w': (b[2] - b[0]) * f5.sx, 'd': (b[3] - b[1]) * f5.sy}
    box6 = lambda b: {'x': f6.a(b[0]), 'y': DEPTH_MM - f6.b(b[3]), 'w': (b[2] - b[0]) * f6.sx, 'd': (b[3] - b[1]) * f6.sy}
    circ6 = lambda c: {'x': f6.a(c[0]), 'y': DEPTH_MM - f6.b(c[1]), 'd': 2 * c[2] * f6.sx}
    p3, p4, p5, p6, p7 = (m[k] for k in ('FIG 3', 'FIG 4', 'FIG 5', 'FIG 6', 'FIG 7'))
    profile, each = patent_profile(pages[m['pages']['FIG 3']], f3)
    # the ribs: rows inside the band's middle that are drawn across most of its width
    page5 = pages[m['pages']['FIG 5']]
    r0, r1 = int(p5['ribs'][1]), int(p5['ribs'][3])
    c0, c1 = p5['ribsCount']
    frac = page5[r0:r1 + 1, int(c0):int(c1)].mean(1)
    lines, prev = 0, False
    for on in frac > 0.8:
        lines += int(on and not prev)
        prev = on
    seam = _spike().line_place(pages[3], _spike().marks()['patent']['front']['seam'])
    return {
        'scales': {k: {'acrossMmPerPx': x.sx, 'downMmPerPx': x.sy, 'box': x.ext['box']} for k, x in v.items()},
        'feetHeightMm': feet_h,
        'seamZ': z3(seam),
        'profile': profile, 'profileEachEnd': each,
        'front': {k: box3(p3[k]) for k in ('door', 'lip', 'band', 'block', 'port1', 'port2', 'panel', 'led', 'power', 'reset')},
        'feetFront': [box3(b) for b in p3['feet']],
        'rear': {**{k: box4(p4[k]) for k in ('window', 'labelStrip', 'power', 'channel', 'rf', 'notch', 'band')},
                 'powerCentre': {'x': WIDTH_MM - f4.a(p4['powerCentre'][0]), 'z': z4(p4['powerCentre'][1])},
                 'channelCentre': {'x': WIDTH_MM - f4.a((p4['channel'][0] + p4['channel'][2]) / 2), 'z': z4((p4['channel'][1] + p4['channel'][3]) / 2)},
                 'rfCentre': {'x': WIDTH_MM - f4.a(p4['rfCentre'][0]), 'z': z4(p4['rfCentre'][1])}},
        'top': {'door': box5(p5['door']), 'band': box5(p5['band']), 'ribs': box5(p5['ribs']), 'ribLines': lines, 'ribCount': lines // 2,
                'buttonsProudMm': (p5['buttons'][0][3] - p5['front']) * f5.sy,
                'buttonsSeenDeepMm': (p5['buttons'][0][3] - p5['buttons'][0][1]) * f5.sy},
        'bottom': {'feet': [circ6(c) for c in p6['feet']], 'screws': [circ6(c) for c in p6['screws']],
                   'cover': box6(p6['cover']), 'coverInner': box6(p6['coverInner']),
                   'straps': [box6(p6['strapAcross']), box6(p6['strapDown'])],
                   'vents': [{**box6(x['box']), 'slots': x['slots']} for x in p6['vents']],
                   'panels': [box6(b) for b in p6['panels']],
                   'bumps': {k: box6(p6[k]) for k in ('frontBracket', 'rearBump', 'avBump')}},
        'side': {'avWindow': {'y': DEPTH_MM - f7.a(p7['avWindow'][2]), 'z': feet_h + HEIGHT_MM - f7.b(p7['avWindow'][3]),
                              'd': (p7['avWindow'][2] - p7['avWindow'][0]) * f7.sx, 'h': (p7['avWindow'][3] - p7['avWindow'][1]) * f7.sy},
                 **{k: {'y': DEPTH_MM - f7.a(p7[k][0]), 'z': feet_h + HEIGHT_MM - f7.b(p7[k][1]), 'd': 2 * p7[k][2] * f7.sx} for k in ('video', 'audio')}},
    }


# --- 2. the photographs, each face rectified on its own ---------------------------------------

def front_fl():
    """O2-FL's front face. u from the left end, z up from the base (the
    body's bottom edge, the feet below it)."""
    mk = marks()['O2-FL']
    L = common.oklab(common.read_rgb('O2-FL'))[0]
    ln, fit = _lines(L, mk['lines'], dark=mk.get('dark', []))
    C = corners_from_lines(ln)
    G = face_homography(C, WIDTH_MM, HEIGHT_MM)
    f = {k: _line_in(G, ln[k]) for k in ln}
    # the profile: the seam either side; where each end's leaning edge leaves
    # the end (the break) and where it meets the base (the inset)
    seam = float(np.mean([_z_at_u(f['seamLeft'], 40.0), _z_at_u(f['seamRight'], WIDTH_MM - 15.0)]))
    left = {'break': _z_at_u(f['slantLeft'], 0.0), 'inset': _u_at_z(f['slantLeft'], 0.0)}
    right = {'break': _z_at_u(f['slantRight'], WIDTH_MM), 'inset': WIDTH_MM - _u_at_z(f['slantRight'], 0.0)}
    door_bottom = _z_at_u(f['doorBottom'], 110.0)
    door = {'left': float(np.mean([_u_at_z(f['doorLeft'], z) for z in (door_bottom, HEIGHT_MM)])),
            'right': float(np.mean([_u_at_z(f['doorRight'], z) for z in (door_bottom, HEIGHT_MM)])),
            'bottom': door_bottom}
    panel = {'left': float(np.mean([_u_at_z(f['panelLeft'], z) for z in (20, 40)])),
             'right': float(np.mean([_u_at_z(f['panelRight'], z) for z in (20, 40)])),
             'bottom': float(np.mean([_z_at_u(f['panelBottom'], u) for u in (40, 90)]))}
    block_right = float(np.mean([_u_at_z(f['blockRight'], z) for z in (10, 40)]))
    return {'cornersPx': C, 'G': G, 'lines': fit, 'seamZ': seam, 'left': left, 'right': right, 'door': door,
            'panel': panel, 'blockRight': block_right}


def top_fl():
    """O2-FL's top, on task 0's four corners (spike.py's case marks): case x
    and y directly, the published width and depth."""
    sp = _spike()
    L = common.oklab(common.read_rgb('O2-FL'))[0]
    case_marks = sp.marks()['case']['O2-FL']
    ln0, _ = _lines(L, case_marks['edges'])
    C = np.array([sp.meet(ln0['farShort'], ln0['nearLong']), sp.meet(ln0['nearShort'], ln0['nearLong']),
                  sp.meet(ln0['nearShort'], ln0['farLong']), sp.meet(ln0['farShort'], ln0['farLong'])])
    G = face_homography(C, [[0, DEPTH_MM], [WIDTH_MM, DEPTH_MM], [WIDTH_MM, 0], [0, 0]])
    mk = marks()['O2-FL']['top']
    ln, fit = _lines(L, mk['lines'])
    f = {k: _line_in(G, ln[k]) for k in ln}
    y_at = lambda line, x: _z_at_u(line, x)          # (x, y) here: the same arithmetic
    x_at = lambda line, y: _u_at_z(line, y)
    return {'cornersPx': C, 'lines': fit,
            'doorRear': float(np.mean([y_at(f['doorRear'], x) for x in (60, 150)])),
            'bandLeft': float(np.mean([x_at(f['bandLeft'], y) for y in (20, 180)])),
            'bandRight': float(np.mean([x_at(f['bandRight'], y) for y in (20, 180)])),
            'ribsRear': float(np.mean([y_at(f['ribsRear'], x) for x in (185, 215)])),
            'ribsFront': float(np.mean([y_at(f['ribsFront'], x) for x in (185, 215)]))}


def rear_br():
    """O2-BR's rear face, seen from behind: u from its left end as seen (the
    case's right), z up from the base. The window's panel is set back: a
    point on it reads off its place through the face's homography along the
    direction a line square to the face reads (the top's far short edge),
    by as much as the panel's base reads above the base."""
    mk = marks()['O2-BR']
    L = common.oklab(common.read_rgb('O2-BR'))[0]
    ln, fit = _lines(L, mk['lines'])
    C = corners_from_lines(ln)
    G = face_homography(C, WIDTH_MM, HEIGHT_MM)
    f = {k: _line_in(G, ln[k]) for k in ln}
    sq = f['farShort'][1]                   # the direction a depth reads in, on the face
    if sq[1] < 0:
        sq = -sq
    base_pts = to_face(G, [ln['panelBase'][0] + ln['panelBase'][1] * t for t in np.linspace(-200, 200, 9)])
    lift = float(np.mean(base_pts[:, 1]))   # how high the panel's base reads
    shift = sq * lift / sq[1]               # what a point on the panel reads off by
    left = {'break': _z_at_u(f['slantLeft'], 0.0), 'inset': _u_at_z(f['slantLeft'], 0.0)}
    right = {'break': _z_at_u(f['slantRight'], WIDTH_MM), 'inset': WIDTH_MM - _u_at_z(f['slantRight'], 0.0)}
    notch = {'left': rear_u_to_x(_u_at_z(f['notchRight'], 0.0)), 'right': rear_u_to_x(_u_at_z(f['notchLeft'], 0.0))}
    return {'cornersPx': C, 'lines': fit, 'panelLiftMm': lift, 'squareDirection': sq, 'shiftMm': shift,
            'left': left, 'right': right, 'notch': notch}


def modulator_face(parts):
    """I7-FL: the modulator's rear face by its four corners, its width the
    modulator's x extent on the board (parts.json, the NTSC one)."""
    mk = marks()['I7-FL']
    mod = next(o for o in parts['model']['others']['ntsc'] if o['kind'] == 'modulator')
    G = face_homography(mk['corners'], mod['l'], 22.0)       # only u is used: the height is not known, and not needed
    left = mod['x'] - mod['l'] / 2
    face_y = mod['y'] + mod['w'] / 2
    u = {k: float(to_face(G, [p])[0][0]) for k, p in mk['points'].items()}
    return {'modulator': {'x': mod['x'], 'y': mod['y'], 'l': mod['l'], 'w': mod['w']}, 'faceY': face_y,
            'u': u, 'board': {k: [left + v, face_y] for k, v in u.items()}}


# --- pictures to look at (out/, git-ignored) ----------------------------------------------------

def rectified(rgb, G, w_mm, h_mm, ppm=4.0, z_up=True):
    """The picture warped into a face's frame at ppm px/mm (z up, so the
    picture's row 0 is the face's top), for looking at."""
    import cv2
    S = np.array([[ppm, 0, 0], [0, -ppm if z_up else ppm, h_mm * ppm if z_up else 0], [0, 0, 1.0]])
    return cv2.warpPerspective(rgb, S @ G, (int(w_mm * ppm), int(h_mm * ppm)), flags=cv2.INTER_AREA)


def draw_boxes(img, boxes, ppm=4.0, h_mm=None, colour=(255, 0, 255), z_up=True):
    """Boxes [u, z, w, h] (z the lower edge when z_up) onto a rectified picture."""
    from PIL import Image, ImageDraw
    im = Image.fromarray(img)
    d = ImageDraw.Draw(im)
    for name, (u, z, w, h) in boxes.items():
        if z_up:
            x0, y0, x1, y1 = u * ppm, (h_mm - z - h) * ppm, (u + w) * ppm, (h_mm - z) * ppm
        else:
            x0, y0, x1, y1 = u * ppm, z * ppm, (u + w) * ppm, (z + h) * ppm
        d.rectangle([x0, y0, x1, y1], outline=colour, width=2)
        d.text((x0 + 2, y0 + 2), name, fill=colour)
    return im


def mm_grid(img, ppm, w_mm, h_mm, step_mm=5.0, z_up=True, scale=1.0):
    """A rectified picture with a grid every step_mm, labelled in mm in a
    strip outside it (u across from 0; z up from 0, or down when not z_up),
    to read features off by eye."""
    from PIL import Image, ImageDraw
    im = Image.fromarray(np.asarray(img)) if not hasattr(img, 'size') or isinstance(img, np.ndarray) else img
    if scale != 1.0:
        im = im.resize((int(im.width * scale), int(im.height * scale)), Image.LANCZOS)
    k = ppm * scale
    M = 36
    can = Image.new('RGB', (im.width + M, im.height + M), 'white')
    can.paste(im, (M, M))
    d = ImageDraw.Draw(can)
    for i, u in enumerate(np.arange(0, w_mm + 0.01, step_mm)):
        X = M + u * k
        d.line([(X, M), (X, M + im.height)], fill=(0, 200, 255) if i % 2 else (255, 140, 0), width=1)
        if i % 2 == 0:
            d.text((X - 8, 4 + (i // 2 % 2) * 14), f'{u:g}', fill='black')
    for i, z in enumerate(np.arange(0, h_mm + 0.01, step_mm)):
        Y = M + ((h_mm - z) if z_up else z) * k
        d.line([(M, Y), (M + im.width, Y)], fill=(0, 200, 255) if i % 2 else (255, 140, 0), width=1)
        if i % 2 == 0:
            d.text((2, Y - 6), f'{z:g}', fill='black')
    return can


# --- 3. the board in the case, on O9 (the PAL set's HOF06601) ---------------------------------
#
# The camera is known from the file's own XMP: a Sony ILCE-7RM4 (its sensor
# 35.7 by 23.8 mm, 9504 by 6336 px) behind a 20 mm lens, the picture a crop
# of the whole frame (CropLeft, CropTop as fractions of it) with no resize,
# no perspective correction, and the lens profile's distortion taken off
# (Camera Raw's DistortionCorrectionAlreadyApplied). So K is the 20 mm's,
# with its principal point at the frame's centre less the crop's origin. With
# K, the rim's homography gives the camera's turn, so the nadir (K r3) and the
# camera's height over the rim.

SENSOR_MM, SENSOR_PX = (35.7, 23.8), (9504, 6336)    # Sony ILCE-7RM4 (A7R IV)


def camera_from_xmp(sid):
    from PIL import Image
    Image.MAX_IMAGE_PIXELS = None
    x = Image.open(common.original(sid)).info['XML:com.adobe.xmp']
    get = lambda k: re.search(rf'{k}="([^"]*)"', x).group(1)
    num, den = get('exif:FocalLength').split('/')
    f_mm = float(num) / float(den)
    f = f_mm / SENSOR_MM[0] * SENSOR_PX[0]
    cx = SENSOR_PX[0] / 2 - float(get('crs:CropLeft')) * SENSOR_PX[0] - 0.5
    cy = SENSOR_PX[1] / 2 - float(get('crs:CropTop')) * SENSOR_PX[1] - 0.5
    facts = {k: get(k) for k in ('tiff:Model', 'exif:FocalLength', 'crs:CropLeft', 'crs:CropTop', 'crs:CropRight', 'crs:CropBottom',
                                  'crs:PerspectiveVertical', 'crs:PerspectiveHorizontal', 'crs:LensProfileEnable')}
    return np.array([[f, 0, cx], [0, f, cy], [0, 0, 1.0]]), facts


def pose_from_plane(K, Hr):
    """From the picture-to-plane homography: the vertical's vanishing point
    (homogeneous) and the camera's height over the plane (its units)."""
    M = np.linalg.inv(K) @ np.linalg.inv(Hr)
    s = (np.linalg.norm(M[:, 0]) + np.linalg.norm(M[:, 1])) / 2
    M = M / s
    r1, r2 = M[:, 0] / np.linalg.norm(M[:, 0]), M[:, 1] / np.linalg.norm(M[:, 1])
    r3 = np.cross(r1, r2)
    R = np.c_[r1, r2, r3]
    C = -R.T @ M[:, 2]
    return K @ r3, float(abs(C[2])), {'orthogonality': float(abs(r1 @ r2)), 'scaleRatio': float(np.linalg.norm(M[:, 0]) / np.linalg.norm(M[:, 1]))}


def _blob(L, p, r, dark):
    """The centroid of the light (or dark) blob nearest p within r px."""
    import cv2
    x, y = int(round(p[0])), int(round(p[1]))
    w = L[y - r:y + r + 1, x - r:x + r + 1]
    t = (w.max() + w.min()) / 2
    m = (w < t) if dark else (w > t)
    n, lab, st, cen = cv2.connectedComponentsWithStats(m.astype(np.uint8), 8)
    if n < 2:
        return None
    d = [np.hypot(*(cen[i] - r)) if st[i, 4] >= 20 else 1e9 for i in range(1, n)]
    i = 1 + int(np.argmin(d))
    if d[i - 1] > r * 0.6:
        return None
    return np.array([x - r + cen[i][0], y - r + cen[i][1]])


BOARD_HOLES_USED = range(8)     # holes 8 to 11 lie under the cartridge connector's body (by 113.6 mm), not on the board's face


def board_points(parts):
    """The ten ICs' end pins (pins 1, N/2, N/2 + 1 and N on their pads) and
    the board's holes not under the connector, in the board frame."""
    reg = json.loads((common.DATA / 'registration.json').read_text(encoding='utf8'))
    P = reg['pads']
    pts = {}
    for f in reg['footprints']:
        if (f['ref'] or '').startswith('U') and not f.get('alternate'):
            p = f['pads']
            n = len(p)
            for i, name in ((0, '1'), (n // 2 - 1, str(n // 2)), (n // 2, str(n // 2 + 1)), (n - 1, str(n))):
                pts[f"{f['ref']}.{name}"] = (P[p[i]]['x'], P[p[i]]['y'])
    holes = parts['model']['board']['holes']
    for k in BOARD_HOLES_USED:
        pts[f'H{k}'] = (holes[k]['x'], holes[k]['y'])
    return pts


def board_in_view(parts):
    mk = marks()['HOF06601']
    rgb = common.read_rgb('O9')
    L = common.oklab(rgb)[0]
    ln, fit = _lines(L, mk['rim'])
    C = corners_from_lines(ln)     # picture top left, top right, base right, base left
    # the picture: the case's front at the top, the case's right on the left
    Hr = face_homography(C, [[WIDTH_MM, DEPTH_MM], [0, DEPTH_MM], [0, 0], [WIDTH_MM, 0]])
    K, facts = camera_from_xmp('O9')
    nadir, height, check = pose_from_plane(K, Hr)
    pts = board_points(parts)
    # the seed: two holes marked by hand give a similarity (the board seen
    # from its solder side, so mirrored); every point is then found near
    # where a homography of the points found so far puts it, twice over
    seed = mk['seed']
    src = np.array([pts['H5'], pts['H4']])
    dst = np.array([seed['H5'], seed['H4']], float)
    a = np.c_[-src[:, 0], src[:, 1]]
    z = complex(*(dst[1] - dst[0])) / complex(*(a[1] - a[0]))
    guess = {k: np.array([(z * complex(*(np.array([-v[0], v[1]]) - a[0]))).real, (z * complex(*(np.array([-v[0], v[1]]) - a[0]))).imag]) + dst[0]
             for k, v in pts.items()}
    found = {}
    for _ in range(2):
        for k, g in guess.items():
            hole = k.startswith('H')
            b = _blob(L, g, 45 if hole else 22, dark=hole)
            if b is not None:
                found[k] = b
        H = common._fit_homography(np.array([pts[k] for k in found]), np.array([found[k] for k in found]))
        guess = {k: common.transform('homography', {'H': H}, [pts[k]])[0] for k in pts}
    keys = sorted(found)
    held = common.fit_held_out({k: found[k] for k in keys}, {k: pts[k] for k in keys}, 'homography', 'leave-one-out')
    img = np.array([found[k] for k in keys])
    bp = np.array([pts[k] for k in keys], float)
    placed = board_in_case(img, bp, Hr, nadir)
    k_ratio = placed['k']
    drop = height * (1 / k_ratio - 1)          # how far the board's face lies below the rim
    # how much the place moves if the principal point is 100 px out either way
    moved = []
    for dx, dy in ((100, 0), (-100, 0), (0, 100), (0, -100)):
        K2 = K.copy()
        K2[0, 2] += dx
        K2[1, 2] += dy
        n2, _, _ = pose_from_plane(K2, Hr)
        q = board_in_case(img, bp, Hr, n2)
        moved.append(float(np.hypot(q['x'] - placed['x'], q['y'] - placed['y'])))
    return {'cornersPx': C, 'rim': fit, 'K': K, 'xmp': facts, 'poseCheck': check, 'cameraOverRimMm': height,
            'nadirPx': (nadir[:2] / nadir[2]).tolist(), 'points': {k: found[k].tolist() for k in keys}, 'missed': sorted(set(pts) - set(found)),
            'imageToBoard': {'n': held['n'], 'fitMm': held['fitMm'], 'heldOutMm': held['heldOutMm'], 'worst': held['worst'][:3]},
            'placed': placed, 'boardBelowRimMm': drop, 'movedByPrincipalPoint100PxMm': max(moved)}


# --- 4. the PAL console's own: its front label (O4) and its underside (O5) ---------------------

def pal_front_words(seam_z, top_z):
    """O4's words on the PAL top shell's front band, which runs from the
    seam to the top: task 0's four corners of the band (spike.json)."""
    spk = json.loads((common.DATA / 'spike.json').read_text(encoding='utf8'))
    C = spk['palFront']['O4']['cornersPx']
    G = face_homography(C, [[0, top_z], [WIDTH_MM, top_z], [WIDTH_MM, seam_z], [0, seam_z]])
    return {k: _box(to_face(G, v)) for k, v in marks()['O4']['polys'].items()}


O5_SCREWS = {'rearRight': 3, 'rearMiddle': 5, 'rearLeft': 2, 'frontRight': 1, 'frontMiddle': 4, 'frontLeft': 0}   # their index in FIG 6's screws


def pal_underside(fig6):
    mk = marks()['O5']
    L = common.oklab(common.read_rgb('O5'))[0]
    found = {k: _blob(L, p, 45, dark=True) for k, p in mk['screws'].items()}
    found = {k: v for k, v in found.items() if v is not None}
    ref = {k: [fig6['screws'][i]['x'], fig6['screws'][i]['y']] for k, i in O5_SCREWS.items()}
    keys = sorted(found)
    held = common.fit_held_out({k: found[k] for k in keys}, {k: ref[k] for k in keys}, 'homography', 'leave-one-out')
    H = np.array(held['params']['H'])
    feet = [{'x': float(q[0]), 'y': float(q[1]), 'd': 2 * f[2] * float(np.linalg.norm(H[:2, :2] @ [1, 0]) / (H[2] @ [f[0], f[1], 1]))}
            for f, q in zip(mk['feet'], to_face(H, [f[:2] for f in mk['feet']]))]
    polys = {k: to_face(H, v) for k, v in mk['polys'].items()}
    return {'screwsPx': {k: v.tolist() for k, v in found.items()}, 'screwsToFig6Mm': {'n': held['n'], 'fitMm': held['fitMm'], 'heldOutMm': held['heldOutMm'], 'worst': held['worst'][:3]},
            'feet': feet, 'boxes': {k: [float(p[:, 0].min()), float(p[:, 1].min()), float(p[:, 0].max() - p[:, 0].min()), float(p[:, 1].max() - p[:, 1].min())] for k, p in polys.items()}}


# --- 5. what the model is made of: case.json and the parts module ----------------------------

def _r(v, places=2):
    return round(float(v), places)


def _fbox(b, feet):
    """A front-face box [u0, z0, u1, z1] (z from the base) to the case frame."""
    return {'x': _r(b[0]), 'z': _r(b[1] + feet), 'w': _r(b[2] - b[0]), 'h': _r(b[3] - b[1])}


def _rbox(b, feet, shift=(0.0, 0.0)):
    """A rear-face box [u0, z0, u1, z1] as seen from behind, less the panel's
    shift, to the case frame (x from the left as seen from the front)."""
    u0, z0, u1, z1 = b[0] - shift[0], b[1] - shift[1], b[2] - shift[0], b[3] - shift[1]
    return {'x': _r(rear_u_to_x(u1)), 'z': _r(z0 + feet), 'w': _r(u1 - u0), 'h': _r(z1 - z0)}


def build(parts):
    pat = patent_features()
    feet = pat['feetHeightMm']
    top_z = HEIGHT_MM + feet
    fl = front_fl()
    tp = top_fl()
    br = rear_br()
    mod = modulator_face(parts)
    view = board_in_view(parts)
    spk = json.loads((common.DATA / 'spike.json').read_text(encoding='utf8'))
    on = marks()['O2-FL']['onFace']
    onr = marks()['O2-BR']['onFace']
    shift = br['shiftMm']

    # the profile (z in the face's frame first, from the base)
    seam = fl['seamZ']
    brk = min(float(np.mean([fl['left']['break'], fl['right']['break']])), seam)
    inset = float(np.mean([fl['left']['inset'], fl['right']['inset']]))
    built = [{'inset': 0.0, 'z': HEIGHT_MM}, {'inset': 0.0, 'z': seam}, {'inset': 0.0, 'z': brk}, {'inset': inset, 'z': 0.0}]
    check_pt = {'inset': float(np.mean([br['left']['inset'], br['right']['inset']])), 'z': 0.0}
    # FIG 3's first row, at the base itself, is the bottom corner's rounded
    # edge, not the end's lean (it reads 17.62, where the next row kept, 2.0 mm
    # up, reads 14.63): left out of the patent's profile, and said so
    patent_prof = [{'inset': p['inset'], 'z': p['z']} for p in pat['profile'] if p['z'] > 0]
    patent_dropped = [{'inset': p['inset'], 'z': p['z']} for p in pat['profile'] if p['z'] <= 0]
    choice = profile_choice(built, check_pt, patent_prof)
    profile = [{'inset': _r(p['inset']), 'z': _r(p['z'] + feet)} for p in choice['profile']]
    base_inset = _inset_at(choice['profile'], 0.0)

    # the board in the case, and the rear connectors
    placed = view['placed']
    rim_z = seam + feet
    board_z = rim_z - view['boardBelowRimMm']
    pc = lambda b: {'x': (b[0] + b[2]) / 2, 'z': (b[1] + b[3]) / 2}
    panel = onr['panel']
    photo = {'power': pc(panel['powerOpening']), 'channel': pc(panel['channelSlot']), 'rf': pc(panel['rfRing'])}
    rear = []
    for key, label, kind, box in (('power', 'AC ADAPTER', 'power jack', panel['powerOpening']),
                                  ('channel', 'CH3-CH4', 'channel switch', panel['channelSlot']),
                                  ('rf', 'RF SWITCH', 'RF jack', panel['rfRing'])):
        on_board = mod['board'][key]
        via_board = board_to_case([on_board], placed)[0]
        px = rear_u_to_x(photo[key]['x'] - shift[0])
        b = _rbox(box, feet, shift)
        err = abs(via_board[0] - px)
        rear.append({'label': label, 'kind': kind, 'face': 'rear', 'x': b['x'], 'z': b['z'], 'w': b['w'], 'h': b['h'],
                     'centre': {'x': _r(px), 'z': _r(photo[key]['z'] - shift[1] + feet)},
                     'from': 'O2-BR, the rear face rectified, the window panel\'s set-back taken off',
                     'board': {'x': _r(on_board[0]), 'y': _r(on_board[1]), 'from': 'I7-FL, the modulator\'s face, at the width parts.json gives the modulator'},
                     'boardPlusOffsetX': _r(via_board[0]), 'checkMm': _r(err), 'within': err <= REAR_LIMIT_MM, 'checked': True,
                     'patentX': _r(pat['rear'][key + 'Centre']['x'])})
    side = []
    for name, label in (('video', 'VIDEO'), ('audio', 'AUDIO')):
        s = pat['side'][name]
        side.append({'label': label, 'kind': 'RCA jack', 'face': 'right', 'y': _r(s['y']), 'z': _r(s['z']),
                     'x': _r(WIDTH_MM - _inset_at(choice['profile'], s['z'] - feet)), 'd': _r(s['d']),
                     'from': 'O1 FIG 7, scaled to the case; its words not read: O2-FL sees that face edge on',
                     'checked': False, 'checkedWhy': 'no photograph shows the modulator\'s side face square enough to place the jacks on the board (I7-FR is too oblique), so nothing checks FIG 7\'s places'})
    rear_ntsc = rear + side
    rear_pal = [{**r, 'label': {'AC ADAPTER': 'ANSCHLUSS NETZGERAT/ ADAPTER', 'CH3-CH4': 'KANAL 3 / KANAL 4', 'RF SWITCH': 'ANSCHLUSS ANTENNE'}.get(r['label'], r['label']),
                 'from': r['from'] + '; the PAL shell\'s window is the same, as O10 shows its three openings [inferring]'} for r in rear_ntsc]
    for r in rear_pal:
        r.pop('board', None)
        r.pop('boardPlusOffsetX', None)
        r.pop('checkMm', None)
        r.pop('within', None)
        if r['face'] == 'rear':
            r['checked'] = False
            r['checkedWhy'] = 'the PAL modulator\'s jacks are never photographed face on, so the PAL rear is not checked against the PAL board'
    checked = [r for r in rear if 'checkMm' in r]
    used_vs_patent = max(abs(r['centre']['x'] - r['patentX']) for r in checked)
    rear_words = (f"O2-BR and the patent's rear view agree within {used_vs_patent:.1f} mm; the board's places miss by up to "
                  f"{max(r['checkMm'] for r in checked):.1f} mm; the check failed as measured")
    ends = [fl['left']['inset'], fl['right']['inset'], br['left']['inset'], br['right']['inset']]

    # the front
    door_front = {'x': _r(fl['door']['left']), 'w': _r(on['band'][0] - fl['door']['left']), 'z': _r(fl['door']['bottom'] + feet),
                  'h': _r(HEIGHT_MM - fl['door']['bottom'])}
    door_top = {'x': _r(fl['door']['left']), 'w': _r(tp['bandLeft'] - fl['door']['left']), 'y': _r(tp['doorRear']), 'd': _r(DEPTH_MM - tp['doorRear'])}
    buttons = []
    for name, key in (('POWER', 'power'), ('RESET', 'reset')):
        b = _fbox(on[key], feet)
        buttons.append({'name': name, **b, 'y': DEPTH_MM, 'proudMm': _r(pat['top']['buttonsProudMm']),
                        'd': _r(pat['top']['buttonsSeenDeepMm']), 'travelMm': 3.0, 'travelFrom': 'typical',
                        'from': 'O2-FL, the front face rectified: the button\'s hole in the panel',
                        'face': _fbox(on[key + 'Face'], feet), 'patent': {k: _r(v) for k, v in pat['front'][key].items()}})
    led = {**_fbox(on['led'], feet), 'y': DEPTH_MM, 'd': _r(on['led'][2] - on['led'][0]), 'from': 'O2-FL, the front face rectified',
           'patent': {k: _r(v) for k, v in pat['front']['led'].items()}}
    ports = [{'name': n, **_fbox(on[k], feet), 'y': DEPTH_MM, 'from': 'O2-FL, the front face rectified: the port\'s frame',
              'patent': {kk: _r(v) for kk, v in pat['front'][k].items()}} for n, k in (('1', 'port1'), ('2', 'port2'))]
    panel_front = {'x': _r(fl['panel']['left']), 'w': _r(fl['panel']['right'] - fl['panel']['left']), 'z': _r(fl['panel']['bottom'] + feet),
                   'h': _r(seam - fl['panel']['bottom']), 'from': 'O2-FL', 'patent': {k: _r(v) for k, v in pat['front']['panel'].items()}}
    band = {'front': {'x': _r(on['band'][0]), 'w': _r(on['band'][2] - on['band'][0])},
            'top': {'x': _r(tp['bandLeft']), 'w': _r(tp['bandRight'] - tp['bandLeft'])},
            'rear': {'x': _r(pat['rear']['band']['x']), 'w': _r(pat['rear']['band']['w'])},
            'from': 'front: O2-FL rectified; top: O2-FL\'s top rectified; rear: O1 FIG 4', 'blackEnds': {'rearD': _r(tp['ribsRear']), 'frontY': _r(tp['ribsFront'])}}
    vents = [{'face': 'top', 'x': band['top']['x'], 'w': band['top']['w'], 'y': _r(tp['ribsRear']), 'd': _r(tp['ribsFront'] - tp['ribsRear']),
              'slots': 20, 'from': 'O2-FL\'s top rectified; 20 gaps between the slats there, against 21 bars on O1 FIG 5'}]
    vents += [{'face': 'bottom', 'x': _r(v['x']), 'y': _r(v['y']), 'w': _r(v['w']), 'd': _r(v['d']), 'slots': v['slots'], 'from': 'O1 FIG 6'} for v in pat['bottom']['vents']]

    # words: front, buttons, ports, rear; the PAL console's own
    fw = lambda k, face='front': {'words': k, 'box': [_fbox(on['words'][k], feet)[c] for c in ('x', 'z', 'w', 'h')], 'face': face, 'from': 'O2-FL'}
    rw = lambda k, words=None: {'words': words or k, 'box': [_rbox(onr['panel']['words'][k], feet, shift)[c] for c in ('x', 'z', 'w', 'h')],
                                'face': 'rear', 'from': 'O2-BR' if words is None else 'O10, placed as on O2-BR'}
    common_words = [fw('POWER', 'button:POWER'), fw('RESET', 'button:RESET'), fw('1'), fw('2')]
    pal_words = pal_front_words(seam + feet, top_z)
    labels_ntsc = [fw('Nintendo'), fw('ENTERTAINMENT SYSTEM')] + common_words + [rw('AC ADAPTER'), rw('CH3-CH4'), rw('RF SWITCH')]
    labels_pal = ([{'words': k, 'box': [_r(v) for v in b], 'face': 'front', 'from': 'O4'} for k, b in pal_words.items()]
                  + common_words
                  + [rw('AC ADAPTER', 'ANSCHLUSS NETZGERAT/ ADAPTER'), rw('CH3-CH4', 'KANAL 3 / KANAL 4'), rw('RF SWITCH', 'ANSCHLUSS ANTENNE')])
    under_pal = pal_underside(pat['bottom'])
    b1, b2 = under_pal['boxes']['label1'], under_pal['boxes']['label2']
    labels_pal += [
        {'words': 'Nintendo\nENTERTAINMENT SYSTEM\nBezeichnung : NESE-001    Nennstrom : 1.3A (AC)\nNennspannung : AC9V, 50Hz    Schutzklasse : III\n'
                  '© 1985 Nintendo\nDarf nur in Verbindung mit Netzgerät/Adapter NES-002ED (TÜV GS S 60534) in Betrieb genommen werden.\nFRG',
         'box': [_r(v) for v in b1], 'face': 'bottom', 'from': 'O5, the label read on it; its three marks (a house, TÜV GS, TÜV) left out'},
        {'words': 'Model: NESE 001\nBrand: Nintendo\nSerial-No.\nG 625\n592 W\nFRG', 'box': [_r(v) for v in b2], 'face': 'bottom',
         'from': 'O5, the label read on it; the Bundespost\'s mark and the pkm mark left out'}]
    for labs in (labels_ntsc, labels_pal):
        bad = label_problems(labs)
        if bad:
            raise ValueError(bad)

    fig6 = pat['bottom']
    under_common = {'feet': [{k: _r(v) for k, v in f.items()} for f in fig6['feet']], 'screws': [{k: _r(v) for k, v in s.items()} for s in fig6['screws']],
                    'cover': {k: _r(v) for k, v in fig6['cover'].items()}, 'coverInner': {k: _r(v) for k, v in fig6['coverInner'].items()},
                    'straps': [{k: _r(v) for k, v in s.items()} for s in fig6['straps']],
                    'panels': [{k: _r(v) for k, v in s.items()} for s in fig6['panels']],
                    'bumps': {n: {k: _r(v) for k, v in s.items()} for n, s in fig6['bumps'].items()},
                    'baseInsetMm': _r(base_inset)}
    underside = {'ntsc': {**under_common, 'from': 'O1 FIG 6, the case turned over, scaled to the case; no photograph of the NTSC underside was used, so its label is not drawn'},
                 'pal': {**under_common, 'from': 'O1 FIG 6 for the moulding [inferring: the same shell]; O5 for the labels, its feet recorded against FIG 6\'s',
                         'labels': [{'box': [_r(v) for v in b1]}, {'box': [_r(v) for v in b2]}],
                         'o5': {'feet': [{k: _r(v) for k, v in f.items()} for f in under_pal['feet']], 'cover': [_r(v) for v in under_pal['boxes']['cover']],
                                'screwsToFig6Mm': under_pal['screwsToFig6Mm']}}}
    feet_list = [{'x': f['x'], 'y': f['y'], 'd': f['d'], 'h': _r(feet)} for f in under_common['feet']]

    verdicts = {v['check']: v['verdict'] for v in spk['verdicts']}
    # How well the size and the profile are known, as numbers beside their reasons, not words for results.py to parse
    # (the final fix wave, 6 Oct 2026). The size: the largest disagreement between the published proportions and the
    # patent's drawings, task 0's judged figures, to a whole per cent. The profile: half the range of the four ends.
    pat_err = spk['case']['patent']
    size_err = max(abs(v) for v in [*pat_err['depthToWidthErrPctEach'].values(), *pat_err['heightToWidthErrPctEach'].values()])
    good_to_pct = int(round(size_err))
    half_range = (_r(max(ends)) - _r(min(ends))) / 2
    good_to_mm = round(half_range, 1)
    pal_diff = [
        'The front label adds a third line, "EUROPEAN VERSION", under "ENTERTAINMENT SYSTEM" (O4); the words above it sit higher on the band.',
        'The rear\'s words are German: "ANSCHLUSS NETZGERAT/ ADAPTER", "KANAL 3 / KANAL 4", "ANSCHLUSS ANTENNE" (O10), where the NTSC console\'s say "AC ADAPTER", "CH3-CH4", "RF SWITCH" (O2-BR).',
        'The underside carries two labels, the rating label and the type approval label, with their words (O5); no photograph of the NTSC underside was used, so the NTSC model has none.',
        'Inside, the modulator is an ALPS can, not the NTSC frame (task 5, parts.json); its jacks are not photographed face on, so the PAL rear is not checked against the PAL board.',
        'The PAL console photographed is yellowed; the model draws both as made, in the same greys (the plan).']
    return {
        'about': ('The NES-001 and NESE-001 cases, measured by tools/nes-model/case_measure.py (task 8 of the NES models plan). '
                  'Millimetres in the case frame: from the case\'s left rear corner at table level, x to the right as seen from the front, y towards the front, z up.'),
        'footprint': {'widthMm': WIDTH_MM, 'depthMm': DEPTH_MM, 'depthFrom': 'published',
                      'widthSource': 'published, not Nintendo\'s: 10 by 8 by 3.5 inches (the NES Fandom wiki, and Thingiverse 243385\'s "254 X 203 X 89 mm", D2) and 10.1 by 8 by 3.5 inches (dimensions.com, D1); good to about ' + str(good_to_pct) + ' per cent',
                      'notNintendos': True, 'goodToPct': good_to_pct,
                      'goodToWhy': (f'the largest disagreement between the published size\'s proportions and the design patent\'s drawings, task 0\'s judged '
                                    f'figures in spike.json case.patent ({size_err:.2f} per cent), to a whole per cent')},
        'heightMm': HEIGHT_MM, 'heightFrom': 'published',
        'heightWhat': 'the body without its feet: the patent\'s height to width without its feet (FIG 3 and FIG 7) is within 1.4 per cent of 88.9 over 254 and 255, and with them 4.6 to 6.4 per cent over',
        'feetMm': _r(feet), 'feetFrom': 'O1 FIG 3, the feet below the body, scaled with its height',
        'seamZ': _r(seam + feet), 'seamFrom': 'O2-FL, the front face rectified; O1 FIG 3 gives ' + str(_r(pat['seamZ'])),
        'spike': {'caseDepth': verdicts['case depth'], 'caseHeight': verdicts['case height'], 'palFront': verdicts['PAL front'],
                  'publishedDepthMm': spk['case']['patent']['published']['depthMm'], 'publishedHeightMm': spk['case']['patent']['published']['heightMm']},
        'outline': [[0, 0], [WIDTH_MM, 0], [WIDTH_MM, DEPTH_MM], [0, DEPTH_MM]],
        'outlineWhat': 'the top seen from above, the published width and depth; its corners are rounded on the case and not measured',
        'profile': profile, 'profileFrom': choice['from'],
        'profileWhat': ('the end of the case seen from the front, the same at both ends and at the front and the rear: points (inset from the end, z). '
                        'The case\'s front and rear faces are upright (O1 FIG 7 and FIG 8), so its side outline is the footprint\'s depth by the height; '
                        'the bottom shell\'s two ends lean in below a break (O1 FIG 3 and FIG 4, O2-FL, O2-BR). Built from O2-FL\'s front, its two ends averaged.'),
        'profileCheck': {'what': 'the bottom shell\'s inset at the base, read on O2-BR\'s rear face (both ends averaged), held out of the profile built on O2-FL\'s front',
                         'endsMm': {'min': _r(min(ends)), 'max': _r(max(ends))},
                         'endsWhat': (f'the four ends read at the base {min(ends):.2f} to {max(ends):.2f} mm (the silhouettes {fl["left"]["inset"]:.2f} and {br["left"]["inset"]:.2f}, the ends turning away '
                                      f'{fl["right"]["inset"]:.2f} and {br["right"]["inset"]:.2f}; the patent about 15.5); the profile\'s {inset:.1f} is O2-FL\'s two ends averaged (all four average {np.mean(ends):.1f}), good to about {good_to_mm} mm, half the four ends\' range. '
                                      'The held-out figure shows that the averaging repeats on a second photograph, not that the average is the true inset'),
                         'goodToMm': good_to_mm, 'goodToWhy': f'half the range of the four ends, ({_r(max(ends))} - {_r(min(ends))}) / 2 = {half_range:.3f} mm, to one place',
                         'errMm': _r(choice['check']['errMm']), 'limitMm': PROFILE_LIMIT_MM, 'passes': choice['check']['passes'],
                         'fallback': 'O1 FIG 3\'s outline, scaled to the case', 'fallbackUsed': choice['from'] == 'patent'},
        'profileEachEnd': {'O2-FL': {'left': {k: _r(v) for k, v in fl['left'].items()}, 'right': {k: _r(v) for k, v in fl['right'].items()}},
                           'O2-BR': {'caseRight': {k: _r(v) for k, v in br['left'].items()}, 'caseLeft': {k: _r(v) for k, v in br['right'].items()}},
                           'what': 'break: the z where the end starts to lean, from the base; inset: at the base. Where the photograph shows the end\'s silhouette (O2-FL\'s left end, O2-BR\'s left as seen, the case\'s right) the insets are 17.6 and 17.7 mm; where it shows the end itself turning away (the other two) they are 14.4 and 12.9: the rounded edge between the front and the end reads differently each way'},
        'profilePatent': [{'inset': _r(p['inset']), 'z': _r(p['z'] + feet)} for p in patent_prof],
        'profilePatentLeftOut': {'points': [{'inset': _r(p['inset']), 'z': _r(p['z'] + feet)} for p in patent_dropped],
                                 'why': 'the row at the base itself is the bottom corner\'s rounded edge, not the end\'s lean'},
        'door': {'front': door_front, 'top': door_top, 'lip': {**_fbox(on['lip'], feet)}, 'from': 'O2-FL: the front and the top rectified',
                 'patent': {'front': {k: _r(v) for k, v in pat['front']['door'].items()}, 'top': {k: _r(v) for k, v in pat['top']['door'].items()}}},
        'band': band, 'panel': panel_front,
        'vents': vents,
        'buttons': buttons,
        'powerLatch': {'seen': False,
                       'what': ('No photograph shows POWER pushed in: O2-FL, O3-01 to O3-04 and the PAL set show both buttons out, the consoles off. O3-01 shows the switch board behind them, '
                                'two push switches of different sizes, which does not say whether POWER\'s latches. The model shows POWER in while the machine runs, as the design says [guessing - verify].')},
        'led': led, 'ports': ports,
        'rear': {'ntsc': rear_ntsc, 'pal': rear_pal},
        'rearWindow': {**_rbox(onr['window'], feet), 'labelStrip': _rbox(onr['panel']['labelStrip'], feet, shift), 'setBackReadMm': _r(br['panelLiftMm']),
                       'shiftMm': [_r(v) for v in shift], 'from': 'O2-BR, the rear face rectified',
                       'patent': {k: _r(v) for k, v in pat['rear']['window'].items()}},
        'notch': {'x': _r(br['notch']['left']), 'w': _r(br['notch']['right'] - br['notch']['left']), 'z': _r(feet), 'h': _r(pat['rear']['notch']['h']),
                  'from': 'its sides O2-BR, its height O1 FIG 4'},
        'rearCheck': {'what': 'each rear connector\'s place along the rear on the board (I7-FL\'s modulator face, at the modulator\'s width from parts.json) plus the board\'s place in the case (O9), against where O2-BR shows it',
                      'limitMm': REAR_LIMIT_MM, 'checked': len(checked), 'within': sum(1 for r in checked if r['within']),
                      'worstMm': _r(max(r['checkMm'] for r in checked)),
                      'uncertaintyMm': {'placesUsed': _r(used_vs_patent, 1), 'boardMiss': _r(max(r['checkMm'] for r in checked)),
                                        'what': 'placesUsed: the largest difference between O2-BR\'s places, which the model uses, and the patent\'s rear view (FIG 4); boardMiss: the largest by which the board\'s places miss O2-BR\'s'},
                      'words': rear_words,
                      'ruling': 'Accepted as failed, as measured (the controller, 5 Oct 2026): the model keeps O2-BR\'s places, and the page states the uncertainty; a recheck needs the modulator face\'s width measured on its own first, with its limit fixed in the plan before it runs',
                      'placedFrom': 'O2-BR: the board\'s places are recorded beside, and the model does not use them, because they miss O2-BR (and O1 FIG 4, which agrees with O2-BR) by more than the limit; chosen after the figures were seen'},
        'labels': {'ntsc': labels_ntsc, 'pal': labels_pal},
        'underside': underside,
        'feet': feet_list,
        'boardInCase': {'x': _r(placed['x']), 'y': _r(placed['y']), 'z': _r(board_z), 'turnDeg': _r(placed['turnDeg']),
                        'uncertaintyMm': float(math.ceil(view['placed']['fitMm']['max'] + view['movedByPrincipalPoint100PxMm'])),
                        'uncertaintyWhat': (f"about {math.ceil(view['placed']['fitMm']['max'] + view['movedByPrincipalPoint100PxMm']):.0f} mm in x and y: the similarity's worst residual ({view['placed']['fitMm']['max']:.2f} mm) "
                                            f"plus the move when the principal point is put 100 px out ({view['movedByPrincipalPoint100PxMm']:.2f} mm), rounded up to a whole millimetre; "
                                            'the shared-moulding assumption (the PAL shell for the NTSC one) is not in it, and stays flagged'),
                        'solderSideUp': True, 'mapping': 'board (bx, by) is at case (x + bx, y - by): the board lies solder side up, its edge fingers to the rear',
                        'zWhat': 'the solder side\'s face: the rim (the seam, O2-FL) less how far below it the board lies on O9',
                        'from': 'O9 (the PAL set\'s HOF06601), the board and the bottom shell in one view, through the camera its XMP records; the PAL and NTSC boards share the layout (task 0) and the shells the moulding [inferring]',
                        'o9': {'boardBelowRimMm': _r(view['boardBelowRimMm']), 'cameraOverRimMm': _r(view['cameraOverRimMm'], 1),
                               'k': _r(view['placed']['k'], 4), 'points': len(view['points']), 'missed': view['missed'],
                               'imageToBoardHeldOutMm': {k: _r(v) for k, v in view['imageToBoard']['heldOutMm'].items()},
                               'similarityFitMm': {k: _r(v) for k, v in view['placed']['fitMm'].items()},
                               'cameraCheck': 'rimScaleRatio is O9\'s own camera check: the rim\'s two scales through the XMP\'s camera, 1 if that camera and the published 254 by 203.2 agreed',
                               'movedByPrincipalPoint100PxMm': _r(view['movedByPrincipalPoint100PxMm']),
                               'rimScaleRatio': _r(view['poseCheck']['scaleRatio'], 4), 'rimOrthogonality': _r(view['poseCheck']['orthogonality'], 4),
                               'xmp': view['xmp']},
                        'o3': 'O3-01 to O3-04 never show the whole board and the case together (the cartridge tray or the shield covers the board, or it is out), so the width cross-check on O3 could not be made'},
        'widthCrossCheck': {'what': 'on O9, the rim\'s depth to width through the camera its XMP records, where the rim is taken as the published 254 by 203.2',
                            'depthToWidth': _r(DEPTH_MM / WIDTH_MM / view['poseCheck']['scaleRatio'], 4),
                            'againstPublishedMidpointPct': _r(100 * (DEPTH_MM / WIDTH_MM / view['poseCheck']['scaleRatio'] / spk['case']['patent']['published']['depthToWidth'] - 1)),
                            'widthAgainstBoard': 'not separable on O9: the board lies about 11 mm below the rim, and that depth and the rim\'s true width trade against each other in one view'},
        'palFront': {'verdict': verdicts['PAL front'], 'ratios': spk['palFront']['ratios'], 'measured': spk['palFront']['measured'],
                     'words': {k: [_r(v) for v in b] for k, b in pal_words.items()}},
        'palDifferences': pal_diff,
        'sources': ['O1', 'O2-FL', 'O2-BR', 'O3-01', 'O4', 'O5', 'O9', 'O10', 'I7-FL'],
    }


def module_exports(c):
    note = lambda s: s
    return {
        'CASE': {'note': 'published size, not Nintendo\'s (' + c['footprint']['widthSource'] + '); the feet from O1 FIG 3; the seam from O2-FL',
                 'width': c['footprint']['widthMm'], 'depth': c['footprint']['depthMm'], 'height': c['heightMm'], 'feet': c['feetMm'],
                 'seamZ': c['seamZ'], 'outline': c['outline'], 'band': c['band'], 'panel': c['panel'], 'notch': c['notch'], 'rearWindow': c['rearWindow']},
        'PROFILE': {'note': (f"the end seen from the front, from {c['profileFrom']}; its inset at the base is O2-FL's two ends averaged, of four ends that read "
                             f"{c['profileCheck']['endsMm']['min']} to {c['profileCheck']['endsMm']['max']} mm, good to about {c['profileCheck']['goodToMm']} mm; its held-out check, "
                             f"{c['profileCheck']['errMm']} mm against {c['profileCheck']['limitMm']}, shows the averaging repeats, not that it is the true inset"),
                    'points': c['profile'], 'from': c['profileFrom']},
        'DOOR': {'note': c['door']['from'], 'front': c['door']['front'], 'top': c['door']['top'], 'lip': c['door']['lip']},
        'VENTS': {'note': 'top: O2-FL\'s top rectified; bottom: O1 FIG 6', 'list': c['vents']},
        'BUTTONS': {'note': 'O2-FL, the front face rectified; travel typical; POWER shown in while running [guessing - verify]',
                    'list': [{k: b[k] for k in ('name', 'x', 'y', 'z', 'w', 'h', 'd', 'proudMm', 'travelMm', 'travelFrom')} for b in c['buttons']]},
        'LED': {'note': c['led']['from'], **{k: c['led'][k] for k in ('x', 'y', 'z', 'w', 'h', 'd')}},
        'PORTS': {'note': 'O2-FL, the front face rectified', 'list': [{k: p[k] for k in ('name', 'x', 'y', 'z', 'w', 'h')} for p in c['ports']]},
        'REAR': {'note': 'NTSC: O2-BR and O1 FIG 7; PAL: the same places, its words from O10. ' + c['rearCheck']['words'],
                 **{r: [{k: v for k, v in x.items() if k in ('label', 'kind', 'face', 'x', 'y', 'z', 'w', 'h', 'd', 'centre', 'checked', 'checkedWhy')} for x in c['rear'][r]] for r in ('ntsc', 'pal')}},
        'LABELS': {'note': 'words only, to be drawn in the site\'s own face',
                   **{r: [{k: v for k, v in x.items() if k in ('words', 'box', 'face')} for x in c['labels'][r]] for r in ('ntsc', 'pal')}},
        'UNDERSIDE': {'note': 'O1 FIG 6 for both; the PAL labels from O5',
                      **{r: {k: v for k, v in c['underside'][r].items() if k not in ('from', 'o5')} for r in ('ntsc', 'pal')}},
        'FEET': {'note': 'O1 FIG 6 for their places, FIG 3 for their height', 'list': c['feet']},
    }


def overlays(c):
    """Pictures to look at, in out/: each face rectified with what was read
    off it (magenta) and the patent's boxes (cyan); O9 with the rim and the
    board's points found; O5 with its screw holes."""
    from PIL import Image, ImageDraw
    common.OUT.mkdir(exist_ok=True)
    feet = c['feetMm']
    fl = front_fl()
    rgb = common.read_rgb('O2-FL')
    im = Image.fromarray(rectified(rgb, fl['G'], WIDTH_MM, HEIGHT_MM, 8))
    for half, (a, b) in (('left', (0, 130)), ('right', (125, WIDTH_MM))):
        mm_grid(im.crop((int(a * 8), 0, int(b * 8), int(HEIGHT_MM * 8))), 8, b - a, HEIGHT_MM, 5).save(common.OUT / f'case-front-{half}.png')
    fb = lambda d: [d['x'], d['z'] - feet, d['w'], d['h']]
    mine = {'door': [c['door']['front']['x'], c['door']['front']['z'] - feet, c['door']['front']['w'], c['door']['front']['h']],
            'led': fb(c['led']), 'panel': fb(c['panel']), **{b['name']: fb(b) for b in c['buttons']}, **{'port' + p['name']: fb(p) for p in c['ports']},
            **{l['words']: [l['box'][0], l['box'][1] - feet, l['box'][2], l['box'][3]] for l in c['labels']['ntsc'] if l['face'] != 'rear'}}
    theirs = {'door': fb(c['door']['patent']['front']), 'led': fb(c['led']['patent']), 'panel': fb(c['panel']['patent']),
              **{b['name']: fb(b['patent']) for b in c['buttons']}, **{'port' + p['name']: fb(p['patent']) for p in c['ports']}}
    pic = draw_boxes(rectified(rgb, fl['G'], WIDTH_MM, HEIGHT_MM, 5), mine, 5, HEIGHT_MM)
    draw_boxes(np.asarray(pic), theirs, 5, HEIGHT_MM, colour=(0, 200, 255)).save(common.OUT / 'case-front-read.png')
    br = rear_br()
    rgbb = common.read_rgb('O2-BR')
    G = face_homography(br['cornersPx'], WIDTH_MM, HEIGHT_MM)
    im = Image.fromarray(rectified(rgbb, G, WIDTH_MM, HEIGHT_MM, 8))
    mm_grid(im.crop((0, 0, int(130 * 8), int(HEIGHT_MM * 8))), 8, 130, HEIGHT_MM, 5).save(common.OUT / 'case-rear.png')
    rb = lambda d: [WIDTH_MM - d['x'] - d['w'], d['z'] - feet, d['w'], d['h']]
    mine = {r['label']: rb(r) for r in c['rear']['ntsc'] if r['face'] == 'rear'}
    mine.update({'window': rb(c['rearWindow']), 'notch': rb(c['notch'])})
    theirs = {'window': rb(c['rearWindow']['patent'])}
    pic = draw_boxes(rectified(rgbb, G, WIDTH_MM, HEIGHT_MM, 5), mine, 5, HEIGHT_MM)
    draw_boxes(np.asarray(pic), theirs, 5, HEIGHT_MM, colour=(0, 200, 255)).save(common.OUT / 'case-rear-read.png')
    tp = top_fl()
    Gt = face_homography(tp['cornersPx'], [[0, DEPTH_MM], [WIDTH_MM, DEPTH_MM], [WIDTH_MM, 0], [0, 0]])
    mm_grid(Image.fromarray(rectified(rgb, Gt, WIDTH_MM, DEPTH_MM, 4, z_up=False)), 4, WIDTH_MM, DEPTH_MM, 10, z_up=False).save(common.OUT / 'case-top.png')
    # O9: the rim's corners and the board's points
    parts = json.loads((common.DATA / 'parts.json').read_text(encoding='utf8'))
    v = board_in_view(parts)
    im9 = Image.fromarray(common.read_rgb('O9'))
    d = ImageDraw.Draw(im9)
    d.polygon([tuple(p) for p in v['cornersPx']], outline=(255, 0, 255), width=6)
    for k, p in v['points'].items():
        d.ellipse([p[0] - 12, p[1] - 12, p[0] + 12, p[1] + 12], outline=(255, 0, 255), width=4)
        d.text((p[0] + 14, p[1] - 14), k, fill=(255, 255, 0))
    n = v['nadirPx']
    d.ellipse([n[0] - 30, n[1] - 30, n[0] + 30, n[1] + 30], outline=(0, 255, 255), width=6)
    im9.resize((im9.width // 3, im9.height // 3)).save(common.OUT / 'case-o9.png')
    im9.crop((1500, 800, 5100, 3100)).save(common.OUT / 'case-o9-board.png')


def main():
    parts = json.loads((common.DATA / 'parts.json').read_text(encoding='utf8'))
    c = build(parts)
    c['model'] = common.plain(module_exports(c), 3)
    common.write_data('case.json', c, places=3)
    text = parts_module(c['model'], c['sources'])
    CASE_MJS.write_text(text, encoding='utf8')
    print(f"profile from {c['profileFrom']}: held-out {c['profileCheck']['errMm']} mm (limit {PROFILE_LIMIT_MM})")
    print(f"board in case: x {c['boardInCase']['x']} y {c['boardInCase']['y']} z {c['boardInCase']['z']} mm, turn {c['boardInCase']['turnDeg']} deg")
    print(f"rear check: {c['rearCheck']['within']} of {c['rearCheck']['checked']} within {REAR_LIMIT_MM} mm, worst {c['rearCheck']['worstMm']}")
    for r in c['rear']['ntsc']:
        if 'checkMm' in r:
            print(f"  {r['label']}: board plus offset {r['boardPlusOffsetX']}, O2-BR {r['centre']['x']}, O1 {r['patentX']}, {r['checkMm']} mm")
    print(f"width cross-check (O9): depth to width {c['widthCrossCheck']['depthToWidth']}, {c['widthCrossCheck']['againstPublishedMidpointPct']} per cent")
    if '--look' in sys.argv:
        overlays(c)
    return 0


if __name__ == '__main__':
    sys.exit(main())
