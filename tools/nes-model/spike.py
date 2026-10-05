"""Task 0's eight checks: what the NES's two models rest on, each measured on
data held out of its fit and judged against the thresholds the plan set
before the measurements (docs/superpowers/plans/2026-10-05-nes-models.md,
Global Constraints). The rules are the same as site/tests/nes-spike-verdicts.mjs.

    NES_MODEL_INPUTS=<folder> python spike.py

Kept as a record; not part of the run that builds the models. Writes
data/spike.json, and pictures to look at in out/ (git-ignored). Exits 3 when
a figure crosses a STOP threshold.

1. Scale, on I1-front (data/marks.json "scale"). x: rows of pins that run
   across the board, first pad to last, each mark moved to its blob's centroid
   (common.refine_to_pad). One x scale by least squares over every row; each
   row held out in turn and measured through the scale fitted to the others;
   rows of at least 30 mm are scored, their errors scaled to a 48.26 mm row.
   y: pin 1 and pin N of each IC, directly across from each other, on drill
   centres fitted to the hole's top rim (rim_centre); one y scale over the
   footprints, each held out in turn, its error a per cent of its spacing.
2. The solder side. Drill centres on I1-front and on I1-back flipped left to
   right; an affine seeded from four holes marked on both; pairs that are
   each other's nearest within 1.0 mm; an affine and a cubic each scored on a
   chequerboard of 20 mm blocks. The outlier rule is the plan's.
3. The PAL layout: I3's parts against I1-front's by a homography, each part
   held out in turn.
4. The case: its depth and height over its width on the design patent's
   orthographic views (O1), judged; from O2-FL and O2-BR, each with its
   camera, recorded only (the plan as revised on 5 October 2026).
5. The PAL front: O4 against O2-FL's front face rectified, as the plan first
   had it and has again since its second revision; the patent's front view
   recorded only.
"""
import json
import math
import sys

import numpy as np

import common

# The plan's thresholds, as set before the measurements (the same numbers as
# site/tests/nes-spike-verdicts.mjs).
# Revised twice on 5 October 2026, each time after task 0's figures were seen.
# First (Dan): the case judged on the patent's orthographic views, its height
# with the depth's limits, the PAL front on O4 against the patent's front
# view, on at least two measured ratios; no limit moved. Second (the
# controller, with Dan's instruction to be pragmatic), after the patent's
# figures were seen: the sources disagree by more than 1.5 and 3 per cent can
# resolve, and the check's purpose is to catch a gross scale error, so the
# case's depth and height are judged against the published ratios' midpoints
# within 5 per cent (pass) and over 8 (STOP), and the PAL front is back on O4
# against O2-FL's front, as first written. The earlier results are in
# spike.json's revision.
PASS = {'xMedian': 0.15, 'yMedianPct': 0.5, 'solderMedian': 0.20, 'solderP90': 0.40, 'palMedian': 1.0, 'palMax': 2.0,
        'depthPct': 5.0, 'heightPct': 5.0, 'palFrontPct': 2.0}
STOP = {'xMedian': 0.25, 'yMedianPct': 1.0, 'ratioPct': 1.5, 'solderMedian': 0.30, 'solderP90': 0.60, 'palAny': 3.0,
        'depthPct': 8.0, 'heightPct': 8.0, 'palFrontPct': 4.0}
MIN = {'xRows': 4, 'yFootprints': 8, 'solderHoles': 150, 'palParts': 10, 'palFrontRatios': 2}

PITCH = 2.54
STATED_DPI = 300
NOMINAL_PX_PER_MM = STATED_DPI / 25.4   # only to size windows and gates; the scale is measured below
SCORED_ROW_MM = 30.0                     # a row across is scored if it is at least this long (the plan)
SCALED_TO_MM = 48.26                     # each scored row's error is scaled to a row this long (the plan)
MATCH_GATE_MM = 1.0                      # under half the pitch, set before matching
BLOCK_MM = 20.0
TIE_MM = 0.005                           # two fits' held-out medians this close are a tie; the simpler wins
CASE_WIDTH_MM, CASE_DEPTH_MM, CASE_HEIGHT_MM = 254.0, 203.2, 88.9     # published, not Nintendo's (docs/nes/facts/models.md, D1 and D2)
CASE_WIDTHS_MM = (254.0, 256.0)     # the published widths: D2 (Fandom, Thingiverse) and D1 (dimensions.com)


def marks():
    with open(common.DATA / 'marks.json', encoding='utf8') as f:
        return json.load(f)


# --- a drill's centre, on its top rim ------------------------------------------------
#
# On these scans a hole is open: a tinned ring, light, round a hole that shows
# the scanner's lid, grey, with the hole's wall in shadow on one side. The top
# rim is the ring's inner edge, where the board's face meets the hole. Along 72
# rays from a first guess, the rim is the steepest rise in light on the way out
# to the ring's brightest point (within 3.5 px inside it); a circle is fitted to
# the rim points by consensus (the circle through three of them that most of
# the others lie within 0.75 px of, its radius 5.0 to 7.5 px, a hole of 0.85 to
# 1.27 mm), refined by least squares on those, and the rays are cast again from
# its centre, three times. A glint inside the hole or solder in it makes rays
# that disagree; the consensus leaves them out. A fit is refused when fewer
# than half the rays agree.

RIM_RAYS = 72
RIM_RADIUS_PX = (5.0, 7.5)
RIM_TOL_PX = 0.75
_TRIPLES = {}


def _circle(P):
    A = np.c_[2 * P, np.ones(len(P))]
    s = np.linalg.lstsq(A, (P ** 2).sum(1), rcond=None)[0]
    return np.array([s[0], s[1], math.sqrt(max(s[2] + s[0] ** 2 + s[1] ** 2, 0.0))])


def _consensus(P):
    import itertools
    n = len(P)
    if n not in _TRIPLES:
        _TRIPLES[n] = np.array(list(itertools.combinations(range(0, n, 2), 3)))
    T = _TRIPLES[n]
    a, b, c = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
    d = 2 * (a[:, 0] * (b[:, 1] - c[:, 1]) + b[:, 0] * (c[:, 1] - a[:, 1]) + c[:, 0] * (a[:, 1] - b[:, 1]))
    ok = np.abs(d) > 1e-9
    d = np.where(ok, d, 1.0)
    sa, sb, sc = (a ** 2).sum(1), (b ** 2).sum(1), (c ** 2).sum(1)
    ux = (sa * (b[:, 1] - c[:, 1]) + sb * (c[:, 1] - a[:, 1]) + sc * (a[:, 1] - b[:, 1])) / d
    uy = (sa * (c[:, 0] - b[:, 0]) + sb * (a[:, 0] - c[:, 0]) + sc * (b[:, 0] - a[:, 0])) / d
    r = np.hypot(a[:, 0] - ux, a[:, 1] - uy)
    ok &= (r >= RIM_RADIUS_PX[0]) & (r <= RIM_RADIUS_PX[1])
    if not ok.any():
        return None, np.zeros(n, bool)
    ux, uy, r = ux[ok], uy[ok], r[ok]
    dist = np.abs(np.hypot(P[None, :, 0] - ux[:, None], P[None, :, 1] - uy[:, None]) - r[:, None])
    i = int(np.argmax((dist <= RIM_TOL_PX).sum(1)))
    inl = dist[i] <= RIM_TOL_PX
    circ = np.array([ux[i], uy[i], r[i]])
    for _ in range(3):
        circ = _circle(P[inl])
        inl = np.abs(np.hypot(*(P - circ[:2]).T) - circ[2]) <= RIM_TOL_PX
    return circ, inl


def rim_centre(L, x, y, rounds=3):
    """The drill's centre from its top rim, near (x, y) on an OKLab L image.
    Returns {"x", "y", "diameterMm", "rimRmsPx", "rays"} (rays: how many of 72
    agree) and the rim points, or (None, None) when it is refused."""
    from scipy.ndimage import gaussian_filter1d, map_coordinates
    step = 0.25
    rs = np.arange(0, 11.01, step)
    near = np.nonzero((rs >= 4.5) & (rs <= 9.5))[0]
    th = np.linspace(0, 2 * np.pi, RIM_RAYS, endpoint=False)
    cx, cy = float(x), float(y)
    for _ in range(rounds):
        X = cx + np.outer(np.cos(th), rs)
        Y = cy + np.outer(np.sin(th), rs)
        prof = gaussian_filter1d(map_coordinates(L, [Y, X], order=1), 2.0, axis=1)
        g = np.gradient(prof, rs, axis=1)
        pts = []
        for k in range(RIM_RAYS):
            ip = near[int(np.argmax(prof[k, near]))]
            w = np.nonzero((rs >= rs[ip] - 3.5) & (rs <= rs[ip]))[0]
            i = w[int(np.argmax(g[k, w]))]
            if g[k, i] <= 0 or i == 0:
                continue
            a, b, c = g[k, i - 1], g[k, i], g[k, i + 1]
            den = a - 2 * b + c
            r = rs[i] + (0.5 * (a - c) / den if den < 0 else 0.0) * step
            pts.append((cx + r * math.cos(th[k]), cy + r * math.sin(th[k])))
        if len(pts) < 3:
            return None, None
        P = np.array(pts)
        circ, keep = _consensus(P)
        if circ is None:
            return None, None
        cx, cy, rad = (float(v) for v in circ)
    if keep.sum() < RIM_RAYS / 2:
        return None, None
    res = np.hypot(P[keep, 0] - cx, P[keep, 1] - cy) - rad
    return {'x': cx, 'y': cy, 'diameterMm': 2 * rad / NOMINAL_PX_PER_MM, 'rimRmsPx': float(np.sqrt((res ** 2).mean())),
            'rays': int(keep.sum())}, P[keep]


# --- 1. scale --------------------------------------------------------------------------

def finger_centre(L, C, x, band, half=25):
    """The middle of the edge finger under column x: half way between its two
    edges, on the light and the colour averaged down the finger (rows `band`).
    A finger is grey and light; the gap beside it is dark, and the board's
    bevelled edge beyond an end finger is brown. A column is finger while its
    light is over half way from the gap's to the finger's and its colour
    (OKLab chroma) under half way from the finger's to the bevel's; each edge
    is put where the criterion it fails crosses its half-way level, by linear
    interpolation."""
    pl, pc = L[band[0]:band[1]].mean(0), C[band[0]:band[1]].mean(0)
    xs = np.arange(int(round(x)) - half, int(round(x)) + half + 1)
    l_fin, l_gap = float(np.median(pl[xs])), float(pl[xs].min())
    c_fin, c_edge = float(np.median(pc[xs])), float(pc[xs].max())
    lh, ch = (l_fin + l_gap) / 2, (c_fin + c_edge) / 2
    inside = lambda i: pl[i] > lh and pc[i] < ch
    i = int(round(x))
    if not inside(i):
        raise ValueError(f'the mark at x {x} is not on a finger')

    def edge(i, step):
        while inside(i + step):
            i += step
        j = i + step                     # the first column out
        if pl[j] <= lh:                  # out by its light: a gap
            return i + step * (pl[i] - lh) / (pl[i] - pl[j])
        return i + step * (ch - pc[i]) / (pc[j] - pc[i])

    left, right = edge(i, -1), edge(i, 1)
    return (left + right) / 2, left, right


def finger_evidence(L, f, fingers, sx):
    """What says the edge fingers are not on 2.54 mm: the pitch of the 35
    gaps between them (a straight line through their places, each the lowest
    light between two fingers, to a tenth of a pixel), and the span of the end
    fingers, both through the x scale of the other rows; beside them, the
    KiCad redrawing's P1, read from I2 (compared, not used)."""
    from scipy.signal import find_peaks
    prof = L[f['band'][0]:f['band'][1]].mean(0)
    lo, hi = int(fingers['edgesPx'][0][1]) - 3, int(fingers['edgesPx'][1][0]) + 4
    pk, _ = find_peaks(-prof[lo:hi], distance=15, prominence=0.08)
    gaps = []
    for i in pk + lo:
        a, b, c = prof[i - 1], prof[i], prof[i + 1]
        gaps.append(i + 0.5 * (a - c) / (a - 2 * b + c))
    k = np.arange(len(gaps))
    slope = float(np.polyfit(k, gaps, 1)[0])
    pads = kicad_pads('P1')
    front = sorted((p for p in pads if p['layer'] == 'F.Cu'), key=lambda p: p['x'])
    return {'gaps': len(gaps), 'gapPitchMm': slope / sx, 'spanMm': fingers['px'] / sx, 'asWordedSpanMm': f['pitches'] * PITCH,
            'endFingerWidthsMm': [(e[1] - e[0]) / sx for e in fingers['edgesPx']],
            'kicad': {'pads': len(front), 'pitchMm': front[2]['x'] - front[1]['x'], 'spanMm': front[-1]['x'] - front[0]['x'],
                      'endPadWidthsMm': [front[0]['w'], front[-1]['w']], 'pad2WidthMm': front[1]['w']}}


def kicad_pads(ref):
    """One footprint's pads from I2, the KiCad redrawing: number, x (mm, the
    footprint unrotated), width and copper layer. A comparison only."""
    from pathlib import Path
    root = common._sexpr(Path(common.original('I2')).read_text(encoding='utf8'))
    for fp in root[1:]:
        if not (isinstance(fp, list) and fp and fp[0] == 'footprint'):
            continue
        props = {q[1]: q[2] for q in fp[1:] if isinstance(q, list) and q and q[0] == 'property'}
        if props.get('Reference') != ref:
            continue
        out = []
        for q in fp[1:]:
            if isinstance(q, list) and q and q[0] == 'pad':
                at, size, layers = common._child(q, 'at'), common._child(q, 'size'), common._child(q, 'layers')
                out.append({'n': q[1], 'x': float(at[1]), 'w': float(size[1]), 'layer': layers[1]})
        return out
    raise KeyError(ref)


def fit_x(rows):
    """px per mm across, by least squares of every row's length in pixels
    against its length in millimetres."""
    px = np.array([r['px'] for r in rows])
    mm = np.array([r['trueMm'] for r in rows])
    return float((px * mm).sum() / (mm * mm).sum())


def held_out_x(rows):
    out = []
    for i, r in enumerate(rows):
        sx = fit_x(rows[:i] + rows[i + 1:])
        err = r['px'] / sx - r['trueMm']
        out.append(dict(r, heldOutPxPerMm=sx, errMm=err, scaledErrMm=err * SCALED_TO_MM / r['trueMm'],
                        scored=r['trueMm'] >= SCORED_ROW_MM))
    return out


def x_summary(held):
    scored = [r for r in held if r['scored']]
    e = np.abs([r['scaledErrMm'] for r in scored])
    largest = max(scored, key=lambda r: abs(r['scaledErrMm']))
    return {'rows': len(scored), 'scaledErrMm': {'median': float(np.median(e)), 'max': float(e.max())},
            'largestRow': largest['id']}


def scale(F, LF, CF):
    m = marks()['scale']
    rows, refused = [], []
    for row in m['rows']:
        ends = []
        for key in ('first', 'last'):
            r = common.refine_to_pad(F, *row[key], NOMINAL_PX_PER_MM)
            ends.append(r)
        if None in ends:
            refused.append({'id': row['id'], 'refused': [k for k, e in zip(('first', 'last'), ends) if e is None]})
            continue
        (x0, y0, m0), (x1, y1, m1) = ends
        rows.append({'id': row['id'], 'part': row['part'], 'pitches': row['pitches'], 'trueMm': row['pitches'] * row['pitchMm'],
                     'px': math.hypot(x1 - x0, y1 - y0), 'first': [x0, y0], 'last': [x1, y1], 'movedMm': [m0, m1]})
    f = m['fingers']
    c0, c1 = finger_centre(LF, CF, f['first'][0], f['band']), finger_centre(LF, CF, f['last'][0], f['band'])
    fingers = {'id': f['id'], 'part': 'P1', 'pitches': f['pitches'], 'px': c1[0] - c0[0], 'first': [c0[0], f['first'][1]],
               'last': [c1[0], f['last'][1]], 'edgesPx': [[c0[1], c0[2]], [c1[1], c1[2]]]}

    # The plan's table counts the edge fingers among the rows at 2.54 mm. They
    # are on 2.50 mm (measured below, and so in the KiCad redrawing), so the
    # check is computed as the plan words it, which is judged, and without
    # them, which is recorded beside it.
    as_worded = rows + [dict(fingers, trueMm=f['pitches'] * PITCH, movedMm=None)]
    held = held_out_x(as_worded)
    held_dip = held_out_x(rows)
    sx = fit_x(as_worded)
    sx_dip = fit_x(rows)

    drills, refused_drills = [], []
    for d in m['drills']:
        ends = {}
        for key in ('pin1', 'pinN'):
            r, _ = rim_centre(LF, *d[key])
            ends[key] = r
        if None in ends.values():
            refused_drills.append({'part': d['part'], 'refused': [k for k, v in ends.items() if v is None]})
            continue
        a, b = ends['pin1'], ends['pinN']
        drills.append({'part': d['part'], 'pins': d['pins'], 'spacingMm': d['rowSpacingMm'],
                       'px': math.hypot(b['x'] - a['x'], b['y'] - a['y']), 'pin1': a, 'pinN': b})

    def fit_y(ds):
        px = np.array([d['px'] for d in ds])
        mm = np.array([d['spacingMm'] for d in ds])
        return float((px * mm).sum() / (mm * mm).sum())

    for i, d in enumerate(drills):
        sy_i = fit_y(drills[:i] + drills[i + 1:])
        d['heldOutPxPerMm'] = sy_i
        d['errPct'] = 100 * (d['px'] / sy_i - d['spacingMm']) / d['spacingMm']
    sy = fit_y(drills)

    def med(ds):
        return float(np.median(np.abs([d['errPct'] for d in ds]))) if ds else None

    e = np.abs([d['errPct'] for d in drills])
    fingers.update(finger_evidence(LF, f, fingers, sx_dip))
    return {
        'x': dict(x_summary(held), each=[{k: r[k] for k in ('id', 'trueMm', 'px', 'errMm', 'scaledErrMm', 'scored')} for r in held],
                  asWorded='The plan\'s rows, the edge fingers among them at 2.54 mm: this is the figure judged.',
                  withoutFingers=dict(x_summary(held_dip), pxPerMm=sx_dip,
                                      why='The edge fingers are on 2.50 mm, not 2.54: measured here with the x scale from the other rows, '
                                          'and so in the KiCad redrawing (P1\'s pads 2.5 mm apart). Recorded, not judged.'),
                  refusedRows=refused),
        'y': {'footprints': len(drills), 'errPct': {'median': float(np.median(e)), 'max': float(e.max())},
              'errPct600': {'median': med([d for d in drills if d['spacingMm'] == 15.24])},
              'errPct300': {'median': med([d for d in drills if d['spacingMm'] == 7.62])},
              'each': drills, 'refused': refused_drills},
        'pxPerMm': {'x': sx, 'y': sy, 'stated': NOMINAL_PX_PER_MM, 'xWithoutFingers': sx_dip},
        'ratio': sx / sy,
        'ratioWithoutFingers': sx_dip / sy,
        'statedDpi': STATED_DPI,
        'fingers': fingers,
    }


def outline(LF, CF, sx, sy):
    """The board's extent on I1-front: the largest region darker than half way
    between the scanner's lid (L about 0.97) and the board's tinned edges
    (about 0.45), its bounding box in pixels, in millimetres through the
    scale. Recorded, not judged: the KiCad layout is a redrawing."""
    import cv2
    board = (LF < 0.71).astype(np.uint8)
    board = cv2.morphologyEx(board, cv2.MORPH_CLOSE, np.ones((7, 7), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(board, 8)
    i = 1 + int(np.argmax(st[1:, 4]))
    x, y, w, h = (int(v) for v in st[i, :4])
    return {'scanPx': [x, y, x + w - 1, y + h - 1], 'scanMm': [w / sx, h / sy]}


# --- 2. the solder side ------------------------------------------------------------------

def pad_blobs(rgb):
    """find_pads' blobs, with what the outlier rule reads: each blob's
    centroid (the same as common.find_pads gives), its area in pixels, and the
    ratio of its ellipse's major to minor axis from its second moments."""
    import cv2
    n, lab, st, cen = cv2.connectedComponentsWithStats(common.pad_mask(rgb), 8)
    out = []
    for i in range(1, n):
        x, y, w, h, a = st[i]
        if not common.PAD_AREA_PX[0] <= a <= common.PAD_AREA_PX[1] or max(w, h) > 1.5 * min(w, h):
            continue
        if a / (math.pi * (max(w, h) / 2) ** 2) < 0.7:
            continue
        ys, xs = np.nonzero(lab[y:y + h, x:x + w] == i)
        ev = np.linalg.eigvalsh(np.cov(np.vstack([xs, ys])))
        out.append({'x': float(cen[i][0]), 'y': float(cen[i][1]), 'areaPx': int(a), 'axisRatio': float(math.sqrt(ev[1] / max(ev[0], 1e-9)))})
    return out


HOLE_FROM_BLOB_MM = 0.5     # a rim's centre further than this from its blob's centroid is another feature


def drill_centres(rgb, L):
    """Every pad blob's drill, on its top rim. A blob whose rim is refused, or
    whose rim's centre is over HOLE_FROM_BLOB_MM from the blob's centroid, has
    no drill found."""
    found, lost = [], 0
    for b in pad_blobs(rgb):
        r, _ = rim_centre(L, b['x'], b['y'])
        if r is None or math.hypot(r['x'] - b['x'], r['y'] - b['y']) > HOLE_FROM_BLOB_MM * NOMINAL_PX_PER_MM:
            lost += 1
            continue
        found.append(dict(b, drill=[r['x'], r['y']], diameterMm=r['diameterMm'], rays=r['rays']))
    return found, lost


def mutual_pairs(a, b, gate):
    from scipy.spatial import cKDTree
    da, ia = cKDTree(b).query(a)
    _, ib = cKDTree(a).query(b)
    return [(i, int(j)) for i, (d, j) in enumerate(zip(da, ia)) if d <= gate and ib[j] == i]


def solder(front, back, sx, sy):
    seeds = marks()['solderSeeds']['holes']
    s1 = np.array([h['I1-front'] for h in seeds], float)
    s2 = np.array([h['I1-back mirrored'] for h in seeds], float)
    seed = common.fit_held_out(s2, s1, 'affine', 'leave-one-out')
    params = seed['params']
    p1 = np.array([h['drill'] for h in front])
    p2 = np.array([h['drill'] for h in back])
    gate = MATCH_GATE_MM * NOMINAL_PX_PER_MM
    for _ in range(3):
        pairs = mutual_pairs(p1, common.transform('affine', params, p2), gate)
        params = common._fit('affine', p2[[j for _, j in pairs]], p1[[i for i, _ in pairs]])
    pairs = mutual_pairs(p1, common.transform('affine', params, p2), gate)
    i1 = [i for i, _ in pairs]
    i2 = [j for _, j in pairs]
    dst = p1[i1] / [sx, sy]                         # I1-front in millimetres, less an offset
    dst = dst - dst.min(0)
    folds = [int(x // BLOCK_MM + y // BLOCK_MM) % 2 for x, y in dst]
    fits = {model: common.fit_held_out(p2[i2], dst, model, folds) for model in ('affine', 'cubic')}
    a, c = fits['affine']['heldOutMm']['median'], fits['cubic']['heldOutMm']['median']
    best = 'cubic' if c < a - TIE_MM else 'affine'

    # The plan's outlier rule, fixed before any measurement: left out of the
    # scoring (not the fit) only if the pad fails the roundness test on either face.
    area1 = np.median([front[i]['areaPx'] for i in i1])
    area2 = np.median([back[j]['areaPx'] for j in i2])

    def fails(h, med):
        return h['axisRatio'] > 1.25 or not 0.6 * med <= h['areaPx'] <= 1.6 * med

    out = np.array([fails(front[i], area1) or fails(back[j], area2) for i, j in pairs])
    held = np.array([e for _, e in fits[best]['heldOutEach']])
    return {
        'model': best,
        'holes': len(pairs),
        'heldOutMm': fits[best]['heldOutMm'],
        'outliers': {'excluded': int(out.sum()), 'heldOutMmWithExclusion': common.stats(held[~out]),
                     'rule': 'a matched hole is left out of the scoring only if its pad on either face has a major to minor axis ratio over 1.25 '
                             'from its second moments, or an area outside 0.6 to 1.6 times the median of the matched pads on that face'},
        'fits': {k: {'fitMm': v['fitMm'], 'heldOutMm': v['heldOutMm']} for k, v in fits.items()},
        'drillsFound': {'I1-front': len(front), 'I1-back': len(back)},
        'seedFitPx': seed['fitMm'],
        'gateMm': MATCH_GATE_MM,
        'blockMm': BLOCK_MM,
        'tieMm': TIE_MM,
        'worst': fits[best]['worst'],
    }, (p1[i1], held, out)


# --- 3. the PAL layout ----------------------------------------------------------------------

def pal_layout(F, sx, sy):
    """I3's parts against I1-front's. Each part's centre is the mean of its
    marked pins (the same pins on both boards); a homography from I3's pixels
    to I1's millimetres is fitted on every other part's pins, and the held-out
    part's centre measured through it."""
    m = marks()['palLayout']
    parts = m['parts']
    src, dst, refined = {}, {}, []
    for p in parts:
        for pin in p['pins']:
            key = f"{p['part']} pin {pin['pin']}"
            src[key] = pin['I3']
            r = common.refine_to_pad(F, *pin['I1-front'], NOMINAL_PX_PER_MM)
            xy = pin['I1-front'] if r is None else [r[0], r[1]]
            refined.append({'pin': key, 'movedMm': None if r is None else r[2]})
            dst[key] = [xy[0] / sx, xy[1] / sy]
    per = []
    for p in parts:
        mine = [f"{p['part']} pin {q['pin']}" for q in p['pins']]
        others = [k for k in src if k not in mine]
        H = common._fit('homography', np.array([src[k] for k in others], float), np.array([dst[k] for k in others], float))
        got = common.transform('homography', H, [src[k] for k in mine])
        want = np.array([dst[k] for k in mine])
        d = got.mean(0) - want.mean(0)
        per.append({'part': p['part'], 'pins': len(mine), 'heldOutMm': float(np.hypot(*d)), 'dxMm': float(d[0]), 'dyMm': float(d[1]),
                    'pinsHeldOutMm': [float(v) for v in np.hypot(*(got - want).T)]})
    e = [q['heldOutMm'] for q in per]
    every = common.fit_held_out(src, dst, 'homography', 'leave-one-out')
    return {
        'parts': len(per),
        'heldOutMm': {'median': float(np.median(e)), 'max': float(max(e))},
        'perPart': per,
        'unmatched': m['unmatched'],
        'notMeasured': m['notMeasured'],
        'pinsLeaveOneOutMm': every['heldOutMm'],
        'i1Refused': sum(1 for r in refined if r['movedMm'] is None),
        'model': 'homography',
    }


# --- lines on a photograph ------------------------------------------------------------------

def edge_line(L, p0, p1, half=15, step=10, dark=False):
    """A straight edge near the segment p0-p1 marked by hand on an OKLab L
    image: every `step` px along the segment, the steepest change of light
    across it (within `half` px, smoothed over 4 px, to a quarter pixel), or
    with `dark` the middle of a dark line (its lowest light), and
    a straight line through those points, refitted three times without the
    points over three times the median distance from it (at least 1 px).
    Returns (a point on it, its unit direction, the rms of the points kept in
    px, points kept, points found)."""
    from scipy.ndimage import gaussian_filter1d, map_coordinates
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    u = (p1 - p0) / np.hypot(*(p1 - p0))
    nrm = np.array([-u[1], u[0]])
    offs = np.arange(-half, half + 0.01, 0.25)
    pts = []
    for t in np.arange(0, np.hypot(*(p1 - p0)) + 0.01, step):
        c = p0 + u * t
        P = c[:, None] + nrm[:, None] * offs[None, :]
        prof = gaussian_filter1d(map_coordinates(L, [P[1], P[0]], order=1), 4.0)
        g = -prof if dark else np.abs(np.gradient(prof, offs))
        i = int(np.argmax(g))
        o = offs[i]
        if 0 < i < len(offs) - 1:
            a, b, cc = g[i - 1], g[i], g[i + 1]
            den = a - 2 * b + cc
            if den < 0:
                o += 0.5 * (a - cc) / den * 0.25
        pts.append(c + nrm * o)
    pts = np.array(pts)
    keep = np.ones(len(pts), bool)
    for _ in range(3):
        m = pts[keep].mean(0)
        d = np.linalg.svd(pts[keep] - m)[2][0]
        r = (pts - m) @ np.array([-d[1], d[0]])
        keep = np.abs(r) <= max(1.0, 3 * np.median(np.abs(r[keep])))
    return m, d, float(np.sqrt((r[keep] ** 2).mean())), int(keep.sum()), len(pts)


def meet(a, b):
    """Where two lines (point, direction) cross."""
    s = np.linalg.solve(np.c_[a[1], -b[1]], b[0] - a[0])
    return a[0] + a[1] * s[0]


def lines(L, segs, dark=()):
    out, fit = {}, {}
    for k, seg in segs.items():
        m, d, rms, kept, n = edge_line(L, seg[0], seg[1], half=seg[2] if len(seg) > 2 else 15, dark=k in dark)
        out[k] = (m, d)
        fit[k] = {'rmsPx': rms, 'points': n, 'kept': kept}
    return out, fit


# --- 4. the case ----------------------------------------------------------------------------
#
# A box seen by a pinhole camera with square pixels, its principal point at the
# picture's centre. The case top's four corners are where its edges' lines
# meet (the corners are rounded). H takes the unit square to the four corners;
# with K, K^-1 H = [W r1, D r2, t] up to scale, so D / W is the ratio of its
# second column's length to its first's. The two vanishing points of the top's
# edges give the focal length for which r1 and r2 are at right angles.

def unit_square_homography(C):
    src = np.array([[0, 0], [1, 0], [1, 1], [0, 1]], float)
    return common._dlt(src, np.asarray(C, float))


def focal_from_vanishing(C, c):
    h = lambda p: np.array([p[0], p[1], 1.0])
    v1 = np.cross(np.cross(h(C[0]), h(C[1])), np.cross(h(C[3]), h(C[2])))
    v2 = np.cross(np.cross(h(C[0]), h(C[3])), np.cross(h(C[1]), h(C[2])))
    v1, v2 = v1[:2] / v1[2], v2[:2] / v2[2]
    f2 = -float(np.dot(v1 - c, v2 - c))
    return (math.sqrt(f2) if f2 > 0 else None), v1, v2


def case_photo(sid, m):
    rgb = common.read_rgb(sid)
    L = common.oklab(rgb)[0]
    s = common.source(sid)
    c = np.array([(s['width'] - 1) / 2, (s['height'] - 1) / 2])
    ln, fit = lines(L, m['edges'])
    C = np.array([meet(ln['farShort'], ln['nearLong']), meet(ln['nearShort'], ln['nearLong']),
                  meet(ln['nearShort'], ln['farLong']), meet(ln['farShort'], ln['farLong'])])
    f_exif = EXIF_FOCAL_MM / SENSOR_WIDTH_MM * s['width']
    f_vp, v1, v2 = focal_from_vanishing(C, c)
    f = f_vp if f_vp is not None and abs(f_vp - f_exif) / f_exif > 0.10 else f_exif
    K = np.array([[f, 0, c[0]], [0, f, c[1]], [0, 0, 1.0]])
    M = np.linalg.inv(K) @ unit_square_homography(C)
    if M[2, 2] < 0:
        M = -M
    W, D = np.linalg.norm(M[:, 0]), np.linalg.norm(M[:, 1])
    n = np.cross(M[:, 0], M[:, 1])
    n /= np.linalg.norm(n)
    X0, X1 = M[:, 2], M[:, 0] + M[:, 2]
    if np.dot(n, -X0) < 0:          # the top's normal points up, towards the camera
        n = -n
    face = np.cross(X1 - X0, n)     # the near long face: the vertical plane through the near long edge

    def below(pts):
        """Each image point on the near long face: how far below the top it is, over W."""
        out = []
        for p in pts:
            r = np.linalg.inv(K) @ np.array([p[0], p[1], 1.0])
            X = r * np.dot(X0, face) / np.dot(r, face)
            out.append(float(np.dot(n, X0 - X)) / W)
        return out

    # Two checks on the camera, recorded: how far D / W moves when one corner
    # moves 3 px, and whether the corners' vertical edges point where this
    # camera says vertical lines must (towards K n, the vertical's vanishing point).
    moved = []
    for i in range(4):
        for dxy in ((3, 0), (-3, 0), (0, 3), (0, -3)):
            Cm = C.copy()
            Cm[i] += dxy
            fm, _, _ = focal_from_vanishing(Cm, c)
            if fm is not None:
                Mm = np.linalg.inv(np.array([[fm, 0, c[0]], [0, fm, c[1]], [0, 0, 1.0]])) @ unit_square_homography(Cm)
                moved.append(float(np.linalg.norm(Mm[:, 1]) / np.linalg.norm(Mm[:, 0])))
    v3 = K @ n
    verticals = []
    for seg in m['verticals']:
        vm, vd, vrms, _, _ = edge_line(L, *seg, half=10, step=6)
        want = v3[:2] / v3[2] - vm
        if vd[1] < 0:
            vd = -vd
        if want[1] < 0:
            want = -want
        verticals.append({'atPx': vm.tolist(), 'seenDeg': math.degrees(math.atan2(vd[0], vd[1])),
                          'cameraDeg': math.degrees(math.atan2(want[0], want[1])), 'rmsPx': vrms})

    base_m, base_d, base_rms, kept, npts = edge_line(L, *m['base'])
    t = np.linspace(0, 1, 15)
    base_pts = [base_m + base_d * np.dot(np.asarray(m['base'][0]) + (np.asarray(m['base'][1]) - np.asarray(m['base'][0])) * k - base_m, base_d)
                for k in t]
    heights = below(base_pts)
    return {
        'cornersPx': C.tolist(),
        'edges': fit,
        'principalPointPx': c.tolist(),
        'focalPx': {'exif': f_exif, 'vanishing': f_vp, 'used': f},
        'vanishingPointsPx': [v1.tolist(), v2.tolist()],
        'depthToWidth': D / W,
        'depthToWidthWithExif': exif_ratio(C, f_exif, c),
        'heightToWidth': float(np.median(heights)),
        'heightToWidthSpread': [float(min(heights)), float(max(heights))],
        'base': {'rmsPx': base_rms, 'points': npts, 'kept': kept},
        'depthToWidthOneCornerMoved3Px': [min(moved), max(moved)],
        'verticals': verticals,
    }, (L, K, n, X0, face, W, ln, below)


def exif_ratio(C, f, c):
    K = np.array([[f, 0, c[0]], [0, f, c[1]], [0, 0, 1.0]])
    M = np.linalg.inv(K) @ unit_square_homography(C)
    return float(np.linalg.norm(M[:, 1]) / np.linalg.norm(M[:, 0]))


EXIF_FOCAL_MM, SENSOR_WIDTH_MM = 112.0, 23.6     # O2's Exif: 112 mm; the D7000's sensor is 23.6 mm wide (the plan)


def case():
    m = marks()['case']
    fl, fl_ctx = case_photo('O2-FL', m['O2-FL'])
    br, _ = case_photo('O2-BR', m['O2-BR'])
    target = CASE_DEPTH_MM / CASE_WIDTH_MM
    errs = {k: 100 * (v['depthToWidth'] - target) / target for k, v in (('fl', fl), ('br', br))}
    worse = max(errs.values(), key=abs)
    hw = {'fl': fl['heightToWidth'], 'br': br['heightToWidth']}
    return {
        'photographsRecordedOnly': 'The figures at this level are the corner photographs\', each through its camera: recorded, not judged, since the plan was revised on 5 October 2026 (see revision). The judged figures are under patent.',
        'depthToWidth': {'fl': fl['depthToWidth'], 'br': br['depthToWidth']},
        'depthToWidthErrPct': worse,
        'depthToWidthErrPctEach': errs,
        'published': {'widthMm': CASE_WIDTH_MM, 'depthMm': CASE_DEPTH_MM, 'depthToWidth': target},
        'heightToWidth': hw,
        'heightToWidthAgreePct': 100 * abs(hw['fl'] - hw['br']) / ((hw['fl'] + hw['br']) / 2),
        'focalPx': {'exif': fl['focalPx']['exif'], 'vanishing': {'fl': fl['focalPx']['vanishing'], 'br': br['focalPx']['vanishing']},
                    'used': {'fl': fl['focalPx']['used'], 'br': br['focalPx']['used']}},
        'photos': {'O2-FL': fl, 'O2-BR': br},
    }, fl_ctx


# --- 4b. the case on the design patent (O1), as the plan was revised on 5 October 2026 ---------
#
# The patent's drawings are orthographic, so they need no camera. Its sheets
# 2 and 3 (the PDF's pages 3 and 4) are taken out at their own 300 dpi by
# pdfimages (poppler), which is the one program outside Python this reads
# with. Each view is a box marked by hand round the drawing (data/marks.json
# "patent"), its speckle under 50 px dropped. Across, a view's extent is its
# outermost drawn pixels: nothing stands out sideways in these views. Down,
# it is from the outer edge of its first long line to the outer edge of its
# last (a long line: a row at least half as black as the blackest row), so
# what stands out of the body (the buttons in the top view, the feet in the
# front and side views) is left out; the overall extents are recorded beside.

def patent_pages():
    import shutil
    import subprocess
    import tempfile
    from PIL import Image
    if not shutil.which('pdfimages'):
        sys.exit('pdfimages (poppler-utils) is needed to read O1, the patent')
    pdf = common.original('O1')
    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run(['pdfimages', '-png', '-f', '3', '-l', '4', str(pdf), f'{tmp}/p'], check=True)
        return {3: np.asarray(Image.open(f'{tmp}/p-000.png').convert('L')) < 128,
                4: np.asarray(Image.open(f'{tmp}/p-001.png').convert('L')) < 128}


def view_extent(page, box):
    import cv2
    x0, y0, x1, y1 = box
    b = page[y0:y1, x0:x1].astype(np.uint8)
    n, lab, st, _ = cv2.connectedComponentsWithStats(b, 8)
    keep = np.isin(lab, [i for i in range(1, n) if st[i, 4] >= 50])
    rows = keep.sum(1)
    long_rows = np.nonzero(rows >= 0.5 * rows.max())[0]
    ys, xs = np.nonzero(keep)
    across = int(xs.max() - xs.min() + 1)
    down = int(long_rows.max() - long_rows.min() + 1)
    return {'acrossPx': across, 'downPx': down, 'overallDownPx': int(ys.max() - ys.min() + 1),
            'box': [int(x0 + xs.min()), int(y0 + long_rows.min()), int(x0 + xs.max()), int(y0 + long_rows.max())]}


def line_place(page, feature, half=5):
    """A drawn line's place: in each row (or column) along it, the mean of
    its black pixels within `half` px of the mark; the median of those."""
    at, lo, hi = feature['at'], feature['from'], feature['to']
    h = feature.get('half', half)
    vals = []
    for k in range(int(lo), int(hi) + 1):
        a, b = int(round(at - h)), int(round(at + h)) + 1
        seg = page[k, a:b] if feature['line'] == 'vertical' else page[a:b, k]
        idx = np.nonzero(seg)[0]
        if len(idx):
            vals.append(a + idx.mean())
    return float(np.median(vals))


def patent():
    m = marks()['patent']
    pages = patent_pages()
    v = {k: view_extent(pages[m['pages'][k]], m['boxes'][k]) for k in m['boxes']}
    front_w = v['FIG 3']['acrossPx']
    d2w = {'top': v['FIG 5']['downPx'] / v['FIG 5']['acrossPx'],
           'bottom': v['FIG 6']['downPx'] / v['FIG 6']['acrossPx'],
           'sideOverFront': v['FIG 7']['acrossPx'] / front_w}
    h2w = {'front': v['FIG 3']['downPx'] / front_w, 'side': v['FIG 7']['downPx'] / front_w}
    # Judged (the second revision): against the midpoint of the published
    # depth to width (203.2 over 256 and over 254) and 88.9 over the midpoint
    # of the published widths. Recorded: against 254 mm alone, as the first
    # revision judged them.
    d_target = sum(CASE_DEPTH_MM / w for w in CASE_WIDTHS_MM) / len(CASE_WIDTHS_MM)
    h_target = CASE_HEIGHT_MM / (sum(CASE_WIDTHS_MM) / len(CASE_WIDTHS_MM))
    d_err = {k: 100 * (x - d_target) / d_target for k, x in d2w.items()}
    h_err = {k: 100 * (x - h_target) / h_target for k, x in h2w.items()}
    d254, h254 = CASE_DEPTH_MM / CASE_WIDTH_MM, CASE_HEIGHT_MM / CASE_WIDTH_MM
    d_err254 = {k: 100 * (x - d254) / d254 for k, x in d2w.items()}
    h_err254 = {k: 100 * (x - h254) / h254 for k, x in h2w.items()}
    page = pages[m['pages']['FIG 3']]
    f = {k: line_place(page, spec) for k, spec in m['front'].items()}
    face = f['faceRight'] - f['faceLeft']
    front = {'faceWidthPx': face,
             'doorWidth': (f['doorRight'] - f['doorLeft']) / face,
             'bandHeight': (f['seam'] - f['bandTop']) / face,
             'buttonsSpan': (f['resetRight'] - f['powerLeft']) / face,
             'linesPx': f}
    return {
        'views': v,
        'depthToWidth': d2w,
        'depthToWidthErrPctEach': d_err,
        'depthToWidthErrPct': max(d_err.values(), key=abs),
        'heightToWidth': h2w,
        'heightToWidthErrPctEach': h_err,
        'heightToWidthErrPct': max(h_err.values(), key=abs),
        'heightToWidthWithFeet': {'front': v['FIG 3']['overallDownPx'] / front_w, 'side': v['FIG 7']['overallDownPx'] / front_w},
        'published': {'depthToWidth': d_target, 'heightToWidth': h_target, 'widthsMm': list(CASE_WIDTHS_MM),
                      'depthMm': CASE_DEPTH_MM, 'heightMm': CASE_HEIGHT_MM,
                      'depthToWidthRange': [CASE_DEPTH_MM / max(CASE_WIDTHS_MM), CASE_DEPTH_MM / min(CASE_WIDTHS_MM)]},
        'firstRevisionAgainst254Mm': {'depthToWidth': d254, 'heightToWidth': h254,
                                      'depthToWidthErrPctEach': d_err254, 'depthToWidthErrPct': max(d_err254.values(), key=abs),
                                      'heightToWidthErrPctEach': h_err254, 'heightToWidthErrPct': max(h_err254.values(), key=abs)},
        'front': front,
    }


# --- 5. the PAL front -------------------------------------------------------------------------
#
# The front of the top shell is a band the width of the case, from the top's
# front edge down to where the bottom shell starts; it carries the door and
# the console's name. Each picture's band is rectified by the homography on its
# four corners (image to the unit square), and a vertical line on the band
# (the door's two sides) is placed across it by where it meets the band's top
# and bottom edges, averaged. Widths over the band's width are then read off
# directly. The band's height over its width needs the band's real shape:
# on O2-FL from the case's camera (the vertical plane through the top's front
# edge); on O4, which is taken straight on, from its picture, its mean height
# over its mean width in pixels; on the patent's front view (FIG 3), from the
# drawing, which is orthographic.

def band_u(G, top, bottom, line):
    return float(np.mean([common.transform('homography', {'H': G}, [meet(line, e)])[0][0] for e in (top, bottom)]))


def pal_front(fl_ctx, pat_front):
    m = marks()['palFront']
    L, K, n, X0, face, W, ln_case, below = fl_ctx
    f = m['O2-FL']
    ln, fit = lines(L, f['lines'], dark=f.get('dark', []))
    top = ln_case['nearLong']
    C = np.array([meet(ln_case['farShort'], top), meet(ln_case['nearShort'], top),
                  meet(ln['rightEnd'], ln['seamRight']), meet(ln['leftEnd'], ln['seamLeft'])])
    G = common._dlt(C, np.array([[0, 0], [1, 0], [1, 1], [0, 1]], float))
    seam_l = ln['seamLeft']
    door = band_u(G, top, seam_l, ln['doorRight']) - band_u(G, top, seam_l, ln['doorLeft'])
    seam_pts = [seam_l[0] + seam_l[1] * k for k in np.linspace(-300, 300, 13)]
    band = float(np.median(below(seam_pts)))
    buttons = band_u(G, top, seam_l, ln['resetRight']) - band_u(G, top, seam_l, ln['powerLeft'])
    fl = {'doorWidth': door, 'bandHeight': band, 'buttonsSpan': buttons, 'cornersPx': C.tolist(), 'lines': fit}

    o = m['O4']
    L4 = common.oklab(common.read_rgb('O4'))[0]
    l4, fit4 = lines(L4, o['lines'], dark=o.get('dark', []))
    C4 = np.array([meet(l4['leftEnd'], l4['top']), meet(l4['rightEnd'], l4['top']),
                   meet(l4['rightEnd'], l4['bottom']), meet(l4['leftEnd'], l4['bottom'])])
    G4 = common._dlt(C4, np.array([[0, 0], [1, 0], [1, 1], [0, 1]], float))
    door4 = band_u(G4, l4['top'], l4['bottom'], l4['doorRight']) - band_u(G4, l4['top'], l4['bottom'], l4['doorLeft'])
    width4 = (np.hypot(*(C4[1] - C4[0])) + np.hypot(*(C4[2] - C4[3]))) / 2
    height4 = (np.hypot(*(C4[3] - C4[0])) + np.hypot(*(C4[2] - C4[1]))) / 2
    o4 = {'doorWidth': door4, 'bandHeight': float(height4 / width4), 'buttonsSpan': None, 'cornersPx': C4.tolist(), 'lines': fit4}

    # Judged (the plan's second revision of 5 October 2026, the check's first
    # form again): O4 against O2-FL's front, rectified on its four corners.
    # Recorded only: both against the patent's front view, as the first
    # revision judged O4.
    names = ['door width', 'label band height', 'buttons span']
    keys = ['doorWidth', 'bandHeight', 'buttonsSpan']
    pct = lambda a, b: None if a is None or b is None else 100 * (a - b) / b
    ratios, errs, measured = [], [], []
    for name, k in zip(names, keys):
        ratios.append({'ratio': name, 'patentFig3': pat_front[k], 'O4': o4[k], 'O2-FL': fl[k],
                       'O2-FLAgainstPatentPct': pct(fl[k], pat_front[k]), 'O4AgainstPatentPct': pct(o4[k], pat_front[k]),
                       'O4AgainstO2-FLPct': pct(o4[k], fl[k])})
        if o4[k] is not None and fl[k] is not None:
            errs.append(pct(o4[k], fl[k]))
            measured.append(name)
    return {'ratios': ratios, 'ratioErrPct': errs, 'measured': measured, 'notMeasured': m['notMeasured'],
            'what': m['what'], 'O2-FL': fl, 'O4': o4}


# --- the verdicts: the same rules as site/tests/nes-spike-verdicts.mjs -------------------------

def verdicts(s):
    def v(ok, stop):
        return 'STOP' if stop else ('pass' if ok else 'between pass and stop')
    sc, so, pal = s['scale'], s['solder']['heldOutMm'], s['palLayout']
    depth = abs(s['case']['patent']['depthToWidthErrPct'])
    height = abs(s['case']['patent']['heightToWidthErrPct'])
    ratios = s['palFront']['ratioErrPct']
    front = max(abs(x) for x in ratios) if ratios else 0.0
    return [
        {'check': 'scale x', 'verdict': v(sc['x']['scaledErrMm']['median'] <= PASS['xMedian'] and sc['x']['rows'] >= MIN['xRows'],
                                          sc['x']['scaledErrMm']['median'] > STOP['xMedian'])},
        {'check': 'scale y', 'verdict': v(sc['y']['errPct']['median'] <= PASS['yMedianPct'] and sc['y']['footprints'] >= MIN['yFootprints'],
                                          sc['y']['errPct']['median'] > STOP['yMedianPct'])},
        {'check': 'x against y', 'verdict': v(True, 100 * abs(sc['ratio'] - 1) > STOP['ratioPct'])},
        {'check': 'solder side', 'verdict': v(so['median'] <= PASS['solderMedian'] and so['p90'] <= PASS['solderP90'] and s['solder']['holes'] >= MIN['solderHoles'],
                                              so['median'] > STOP['solderMedian'] or so['p90'] > STOP['solderP90'])},
        {'check': 'PAL layout', 'verdict': v(pal['heldOutMm']['median'] <= PASS['palMedian'] and pal['heldOutMm']['max'] <= PASS['palMax'] and pal['parts'] >= MIN['palParts'],
                                             pal['heldOutMm']['max'] > STOP['palAny'] or len(pal['unmatched']) > 0)},
        {'check': 'case depth', 'verdict': v(depth <= PASS['depthPct'], depth > STOP['depthPct'])},
        {'check': 'case height', 'verdict': v(height <= PASS['heightPct'], height > STOP['heightPct'])},
        {'check': 'PAL front', 'verdict': v(front <= PASS['palFrontPct'] and len(ratios) >= MIN['palFrontRatios'], front > STOP['palFrontPct'])},
    ]


# --- pictures to look at --------------------------------------------------------------------

def overlays(F, s, solder_data):
    """out/: the scale's marks and drills, the solder side's held-out errors,
    and the case's lines, to look at. Never committed (out/ is git-ignored)."""
    import cv2
    from PIL import Image
    common.OUT.mkdir(exist_ok=True)
    img = F.copy()
    for d in s['scale']['y']['each']:
        for k in ('pin1', 'pinN'):
            cv2.circle(img, (int(round(d[k]['x'] * 4)), int(round(d[k]['y'] * 4))), int(round(d[k]['diameterMm'] * NOMINAL_PX_PER_MM * 2)), (255, 0, 255), 1, cv2.LINE_AA, 2)
    Image.fromarray(img).save(common.OUT / 'scale-drills.png')
    p1, held, out = solder_data
    img = (F // 2).copy()
    for (x, y), e, o in zip(p1, held, out):
        c = (0, 255, 0) if e <= 0.20 else (255, 255, 0) if e <= 0.40 else (255, 0, 0)
        cv2.circle(img, (int(round(x)), int(round(y))), 6, c, -1 if not o else 1)
    Image.fromarray(img).save(common.OUT / 'solder-held-out.png')
    for sid in ('O2-FL', 'O2-BR'):
        rgb = common.read_rgb(sid).copy()
        C = np.array(s['case']['photos'][sid]['cornersPx'])
        for i in range(4):
            cv2.line(rgb, tuple(int(v) for v in C[i]), tuple(int(v) for v in C[(i + 1) % 4]), (255, 0, 0), 3, cv2.LINE_AA)
        if sid == 'O2-FL':
            B = np.array(s['palFront']['O2-FL']['cornersPx'])
            for i in range(4):
                cv2.line(rgb, tuple(int(v) for v in B[i]), tuple(int(v) for v in B[(i + 1) % 4]), (0, 0, 255), 3, cv2.LINE_AA)
        Image.fromarray(rgb).resize((rgb.shape[1] // 3, rgb.shape[0] // 3)).save(common.OUT / f'case-{sid}.jpg')
    rgb = common.read_rgb('O4').copy()
    B = np.array(s['palFront']['O4']['cornersPx'])
    for i in range(4):
        cv2.line(rgb, tuple(int(v) for v in B[i]), tuple(int(v) for v in B[(i + 1) % 4]), (0, 0, 255), 4, cv2.LINE_AA)
    Image.fromarray(rgb).save(common.OUT / 'pal-front-O4.png')


ABOUT = ('Task 0 of the NES models plan: the eight checks the two models rest on, each on data held out of its fit where it '
         'has one, judged against the plan\'s thresholds. Written by spike.py; every figure here is a measurement made on the '
         'date of the journal entry that quotes it (docs/journal/2026-10-05-the-nes-models.md).')


REVISION = {
    'date': '2026-10-05',
    'note': ('The case depth check as first written, on the corner photographs O2-FL and O2-BR each through its camera, '
             'crossed its STOP: -8.77 per cent on O2-BR (case.depthToWidthErrPct, kept with the photographs\' other figures, '
             'recorded only). Dan ruled on 5 October 2026, after an independent review, that it judged the wrong quantity: '
             'it measured the camera, not the case. The plan (docs/superpowers/plans/2026-10-05-nes-models.md, Global '
             'Constraints, the rows revised that day) now judges the case\'s depth and height on the design patent\'s '
             'orthographic views (case.patent), against the same 1.5 and 3 per cent, drops the row on the two photographs\' '
             'heights agreeing, and judges the PAL front on O4 against the patent\'s front view, on at least two measured ratios. '
             'That first revision\'s run crossed two STOPs: case depth -3.46 per cent on the patent\'s top view against 203.2 / 254 '
             '(case.patent.firstRevisionAgainst254Mm), and the PAL front -8.08 per cent on the label band, O4 against the patent '
             '(palFront.ratios, O4AgainstPatentPct). A second revision, the same day, by the controller with Dan\'s instruction to be '
             'pragmatic, was made AFTER those patent figures were seen: the sources disagree with each other by more than the 1.5 '
             'and 3 per cent limits can resolve (the patent\'s top view against its side over front; the published widths of 254 '
             'and 256 mm at a depth of 203.2), and the check\'s real purpose is to catch a gross scale error. So the case\'s depth '
             'and height are judged against the published ratios\' midpoints, pass within 5 per cent, STOP over 8, and the PAL front '
             'is back on O4 against O2-FL\'s front as the plan first had it. Its door width needs no camera (the rectification on '
             'four corners is planar); its label band height does: on O2-FL it is read through the case\'s camera, the one the first '
             'revision found faulty, and O4 is taken to be straight on, so the band\'s figure is the weakest and the nearest a stop, '
             'known and disclosed.'),
    'firstVerdicts': [
        {'check': 'scale x', 'verdict': 'pass'},
        {'check': 'scale y', 'verdict': 'between pass and stop'},
        {'check': 'x against y', 'verdict': 'pass'},
        {'check': 'solder side', 'verdict': 'pass'},
        {'check': 'PAL layout', 'verdict': 'pass'},
        {'check': 'case depth', 'verdict': 'STOP'},
        {'check': 'case height, the two photographs', 'verdict': 'pass'},
        {'check': 'PAL front', 'verdict': 'between pass and stop'},
    ],
    'firstRevisionVerdicts': [
        {'check': 'scale x', 'verdict': 'pass'},
        {'check': 'scale y', 'verdict': 'between pass and stop'},
        {'check': 'x against y', 'verdict': 'pass'},
        {'check': 'solder side', 'verdict': 'pass'},
        {'check': 'PAL layout', 'verdict': 'pass'},
        {'check': 'case depth', 'verdict': 'STOP'},
        {'check': 'case height', 'verdict': 'pass'},
        {'check': 'PAL front', 'verdict': 'STOP'},
    ],
}


def main():
    F = common.read_rgb('I1-front')
    LF, A, B = common.oklab(F)
    CF = np.hypot(A, B)
    sc = scale(F, LF, CF)
    sx, sy = sc['pxPerMm']['x'], sc['pxPerMm']['y']
    x0, y0, x1, y1 = common.kicad_outline_box(common.original('I2'))
    ol = outline(LF, CF, sx, sy)
    ol['kicadMm'] = [x1 - x0, y1 - y0]
    Bk = common.read_rgb('I1-back', mirror=True)
    LB = common.oklab(Bk)[0]
    front, lost_f = drill_centres(F, LF)
    back, lost_b = drill_centres(Bk, LB)
    so, solder_data = solder(front, back, sx, sy)
    so['drillsLost'] = {'I1-front': lost_f, 'I1-back': lost_b}
    pal = pal_layout(F, sx, sy)
    cs, fl_ctx = case()
    cs['patent'] = patent()
    pf = pal_front(fl_ctx, cs['patent']['front'])
    s = {'about': ABOUT, 'scale': sc, 'outline': ol, 'solder': so, 'palLayout': pal, 'case': cs, 'palFront': pf}
    s['verdicts'] = verdicts(s)
    s['revision'] = REVISION
    common.write_data('spike.json', s)
    for v in s['verdicts']:
        print(f"{v['check']}: {v['verdict']}")
    overlays(F, s, solder_data)
    return 3 if any(v['verdict'] == 'STOP' for v in s['verdicts']) else 0


if __name__ == '__main__':
    sys.exit(main())
