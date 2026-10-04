"""Task 0's three numbers: what everything else in the BBC Micro's models
rests on, each measured on data held out of its fit and judged against the
thresholds the plan set before they were measured
(docs/superpowers/plans/2026-10-04-bbc-micro-models.md, Global Constraints).

    BBC_MODEL_INPUTS=<folder> python spike.py

Kept as a record; not part of the run that builds the models.

1. The scan's scale. The four corner pins of 16 DIP footprints on I1, marked
   by hand (data/marks.json) and each moved to the centroid of the solder
   blob nearest it (common.refine_to_pad, refused beyond 0.6 mm). Each
   footprint's corners are a rectangle of known size in millimetres: its row
   is (pins / 2 - 1) x 2.54 mm long and its rows 7.62 or 15.24 mm apart. One
   2 x 2 linear map A (pixels per millimetre along x and y, and any skew)
   with a free offset per footprint is fitted by least squares on half the
   footprints, and every row of the other half is measured through it against
   its true length; then the halves swap. A from every footprint gives the
   scale. The threshold is on the 40-pin rows (48.26 mm); every row is
   reported too.

2. The solder side. Pads found on I1 and on I2 mirrored (common.find_pads).
   An affine from four pads marked on both seeds the match; pads are paired
   when each is the other's nearest within 1.0 mm (a gate set before
   matching: under half the 2.54 mm pitch), the affine is refitted on the
   pairs and the match redone, three times. Then an affine and a cubic from
   I2 mirrored to I1 in millimetres are each scored on a chequerboard of
   20 mm blocks: each colour held out of the fit and measured under the fit
   to the other.

3. The keys. Every key centre of K1 (the KiCad keyboard) from its switch's
   4 mm hole, and 50 key tops marked by hand on O1. A homography O1 -> K1,
   each key held out in turn. The case's front edge, two corners marked on
   O1, mapped through the homography fitted to every key and measured. The
   case is 415 mm wide (Service Manual).

Writes data/spike.json, and overlays to look at in out/.
"""
import json
import math
import sys

import numpy as np

import common

# The plan's thresholds, as set before the measurements (Global Constraints).
PASS = {'scaleMedian': 0.10, 'scaleMax': 0.25, 'solderMedian': 0.15, 'solderP90': 0.30, 'solderMax': 0.60,
        'keysMedian': 1.0, 'keysP90': 2.0, 'keysMax': 3.0, 'frontEdgePct': 1.5}
STOP = {'scaleMedian': 0.20, 'scaleMax': 0.50, 'ratioPct': 1.5, 'solderMedian': 0.25, 'solderP90': 0.50,
        'keysMedian': 1.5, 'keysOver3': 5, 'frontEdgePct': 2.5}
PITCH = 2.54
CASE_WIDTH_MM = 415.0
NOMINAL_PX_PER_MM = 15.75     # only to size search windows; the scale is measured below
MATCH_GATE_MM = 1.0
BLOCK_MM = 20.0


def marks():
    with open(common.DATA / 'marks.json', encoding='utf8') as f:
        return json.load(f)


# --- 1. scale ---------------------------------------------------------------------

def footprint_mm(fp):
    """The corner pins' places in millimetres, in the footprint's own frame
    lined up with the board's axes."""
    L = (fp['pins'] // 2 - 1) * PITCH
    w = fp['rowSpacingMm']
    if fp['rows'] == 'vertical':
        return {'tl': (0, 0), 'tr': (w, 0), 'bl': (0, L), 'br': (w, L)}, [('tl', 'bl'), ('tr', 'br')], L
    return {'tl': (0, 0), 'tr': (L, 0), 'bl': (0, w), 'br': (L, w)}, [('tl', 'tr'), ('bl', 'br')], L


def fit_linear(fps):
    """A in px = A mm + offset, one offset per footprint, by least squares."""
    P, Q = [], []
    for fp in fps:
        mm, _, _ = footprint_mm(fp)
        c = [k for k in mm if k in fp['px']]
        p = np.array([fp['px'][k] for k in c])
        q = np.array([mm[k] for k in c], float)
        P.append(p - p.mean(0))
        Q.append(q - q.mean(0))
    P, Q = np.vstack(P), np.vstack(Q)
    return np.linalg.lstsq(Q, P, rcond=None)[0].T


def row_errors(A, fps):
    Ai = np.linalg.inv(A)
    out = []
    for fp in fps:
        _, rows, L = footprint_mm(fp)
        for a, b in rows:
            if a in fp['px'] and b in fp['px']:
                d = Ai @ (np.array(fp['px'][b]) - np.array(fp['px'][a]))
                out.append({'footprint': fp['id'], 'pins': fp['pins'], 'row': f'{a}-{b}', 'trueMm': L,
                            'measuredMm': float(np.hypot(*d)), 'errMm': float(np.hypot(*d) - L)})
    return out


# Boxes round four rows of connector pads that run across the board (x), in
# I1 pixels, read off gridded views of the scan: PL8 and PL9 (two rows each)
# and PL13 and PL14. A check on the x scale that does not use any DIP's row
# spacing: the pads are on a 0.1 inch pitch.
CONNECTOR_ROWS = {'PL8': (150, 3480, 950, 3650), 'PL9': (1150, 3480, 1800, 3650), 'PL13': (1100, 2520, 1850, 2600), 'PL14': (200, 2520, 700, 2600)}


def connector_pitch(pads):
    """Pixels per millimetre along each row of connector pads, from a straight
    line fitted to the pads' places against their pitch numbers."""
    rows = []
    for name, (x0, y0, x1, y1) in CONNECTOR_ROWS.items():
        q = pads[(pads[:, 0] > x0) & (pads[:, 0] < x1) & (pads[:, 1] > y0) & (pads[:, 1] < y1)]
        groups = []
        for p in q[np.argsort(q[:, 1])]:
            if groups and abs(np.mean([g[1] for g in groups[-1]]) - p[1]) < 8:
                groups[-1].append(p)
            else:
                groups.append([p])
        for g in groups:
            if len(g) < 6:
                continue
            g = np.array(sorted(g, key=lambda a: a[0]))
            k = np.round((g[:, 0] - g[0, 0]) / (PITCH * NOMINAL_PX_PER_MM))
            bx = np.polyfit(k, g[:, 0], 1)[0]
            by = np.polyfit(k, g[:, 1], 1)[0]
            rows.append({'row': name, 'pads': len(g), 'pitches': int(k.max()), 'pxPerMm': float(np.hypot(bx, by) / PITCH)})
    v = [r['pxPerMm'] for r in rows]
    return {'rows': rows, 'median': float(np.median(v)), 'min': float(min(v)), 'max': float(max(v))}


def scale(i1, pads1):
    m = marks()['scale']
    fps, refined = [], []
    for fp in m['footprints']:
        fp = dict(fp, px={})
        for corner, (x, y) in fp['corners'].items():
            r = common.refine_to_pad(i1, x, y, NOMINAL_PX_PER_MM)
            refined.append({'footprint': fp['id'], 'corner': corner, 'mark': [x, y],
                            'refined': None if r is None else [r[0], r[1]], 'movedMm': None if r is None else r[2]})
            if r is not None:
                fp['px'][corner] = (r[0], r[1])
        fps.append(fp)
    halves = {f: [fp for fp in fps if fp['fold'] == f] for f in ('A', 'B')}
    held = row_errors(fit_linear(halves['A']), halves['B']) + row_errors(fit_linear(halves['B']), halves['A'])
    A = fit_linear(fps)
    x, y = float(np.hypot(*A[:, 0])), float(np.hypot(*A[:, 1]))
    skew = math.degrees(math.atan2(A[1, 0], A[0, 0]) - math.atan2(-A[0, 1], A[1, 1]))
    err_all = np.abs([r['errMm'] for r in held])
    err_40 = np.abs([r['errMm'] for r in held if r['pins'] == 40])
    moved = [r['movedMm'] for r in refined if r['movedMm'] is not None]
    return A, {
        'pxPerMm': {'x': x, 'y': y},
        'ratio': x / y,
        'skewDeg': skew,
        'rotationDeg': math.degrees(math.atan2(A[1, 0], A[0, 0])),
        'matrix': A.tolist(),
        'footprints': len(fps),
        'rowsScored': len(held),
        'heldOut': {'rowLengthErrMm': {'median': float(np.median(err_all)), 'max': float(err_all.max())},
                    'fortyPinRowErrMm': {'median': float(np.median(err_40)), 'max': float(err_40.max()), 'n': len(err_40)}},
        'rows': held,
        'marks': {'n': len(refined), 'refused': sum(r['movedMm'] is None for r in refined),
                  'movedMm': {'median': float(np.median(moved)), 'max': float(np.max(moved))}},
        'xFromConnectorPitch': connector_pitch(pads1),
    }


# --- 2. solder side --------------------------------------------------------------

def mutual_pairs(a, b, gate):
    from scipy.spatial import cKDTree
    da, ia = cKDTree(b).query(a)
    _, ib = cKDTree(a).query(b)
    return [(i, int(j)) for i, (d, j) in enumerate(zip(da, ia)) if d <= gate and ib[j] == i]


def solder(A, pads1, pads2):
    seeds = marks()['solderSeeds']['holes']
    s1 = np.array([h['I1'] for h in seeds], float)
    s2 = np.array([h['I2mirrored'] for h in seeds], float)
    seed = common.fit_held_out(s2, s1, 'affine', 'leave-one-out')
    params = seed['params']
    gate_px = MATCH_GATE_MM * float(np.sqrt(abs(np.linalg.det(A))))
    for _ in range(3):
        pairs = mutual_pairs(pads1, common.transform('affine', params, pads2), gate_px)
        params = common._fit('affine', pads2[[j for _, j in pairs]], pads1[[i for i, _ in pairs]])
    pairs = mutual_pairs(pads1, common.transform('affine', params, pads2), gate_px)
    p1 = pads1[[i for i, _ in pairs]]
    p2 = pads2[[j for _, j in pairs]]
    dst = p1 @ np.linalg.inv(A).T                       # I1 in millimetres (the board frame, less an offset)
    dst = dst - dst.min(0)
    folds = [int(x // BLOCK_MM + y // BLOCK_MM) % 2 for x, y in dst]
    fits = {model: common.fit_held_out(p2, dst, model, folds) for model in ('affine', 'cubic')}
    best = min(fits, key=lambda k: fits[k]['heldOutMm']['median'])
    return {
        'padsFound': {'I1': len(pads1), 'I2': len(pads2)},
        'seedFitPx': seed['fitMm'],
        'gateMm': MATCH_GATE_MM,
        'blockMm': BLOCK_MM,
        'holes': len(pairs),
        'fit': best,
        'heldOutMm': fits[best]['heldOutMm'],
        'fits': {k: {'fitMm': v['fitMm'], 'heldOutMm': v['heldOutMm']} for k, v in fits.items()},
    }, (p1, p2, dst, fits)


# --- 3. keys ----------------------------------------------------------------------

def keys():
    k1 = common.switch_centres(common.original('K1'))
    m = marks()['keys']
    src = {k['key']: k['at'] for k in m['marks']}
    dst = {k['key']: k1[k['k1']]['centre'] for k in m['marks']}
    r = common.fit_held_out(src, dst, 'homography', 'leave-one-out')
    held = dict(r['heldOutEach'])
    edge = common.transform('homography', r['params'], [m['frontEdge']['left'], m['frontEdge']['right']])
    width = float(np.hypot(*(edge[1] - edge[0])))
    again = common.transform('homography', r['params'], [m['frontEdge']['repeat']['left'], m['frontEdge']['repeat']['right']])
    # The rows: how much of what the homography leaves is one offset per row.
    names = list(src)
    res = np.array([dst[n] for n in names]) - common.transform('homography', r['params'], [src[n] for n in names])
    row_of = {n: round(dst[n][1], 2) for n in names}
    rows = sorted(set(row_of.values()))
    means = {y: res[[row_of[n] == y for n in names]].mean(0) for y in rows}
    within = sum(float(((res[i] - means[row_of[n]]) ** 2).sum()) for i, n in enumerate(names))
    total = float((res ** 2).sum())
    repeats = m['repeats']['marks']
    first = {k['key']: k['at'] for k in m['marks']}
    rep = []
    for k in repeats:
        a, b = common.transform('homography', r['params'], [k['at'], first[k['key']]])
        rep.append(float(np.hypot(*(a - b))))
    return {
        'n': r['n'],
        'model': 'homography',
        'fitMm': r['fitMm'],
        'heldOutMm': r['heldOutMm'],
        'over3mm': sum(1 for v in held.values() if v > 3.0),
        'worst': r['worst'],
        'frontEdgeMm': width,
        'frontEdgeErrPct': 100 * (width - CASE_WIDTH_MM) / CASE_WIDTH_MM,
        'frontEdgeRepeatMm': float(np.hypot(*(again[1] - again[0]))),
        'rowOffsetsMm': {f'{y:g}': means[y].tolist() for y in rows},
        'rowOffsetsExplain': 1 - within / total,
        'repeatMm': {'n': len(rep), 'median': float(np.median(rep)), 'max': float(max(rep))},
        'parallax': parallax(src, dst, [m['frontEdge']['left'], m['frontEdge']['right']], width, (common.source('O1')['width'], common.source('O1')['height'])),
    }, r


def camera(H, size):
    """A camera from a plane's homography alone: square pixels, the principal
    point at the picture's centre, and the focal length f that makes the
    plane's two axes come out at right angles (closed form). H takes picture
    pixels to plane millimetres. Returns (f in pixels, the camera's height above
    the plane in millimetres), or None when no real f exists."""
    G = np.linalg.inv(np.array(H, float))
    cx, cy = size[0] / 2, size[1] / 2
    a = G[0, :2] - cx * G[2, :2]
    b = G[1, :2] - cy * G[2, :2]
    z = G[2, :2]
    f2 = -(a[0] * a[1] + b[0] * b[1]) / (z[0] * z[1])
    if not f2 > 0:
        return None
    f = math.sqrt(f2)
    K = np.array([[f, 0, cx], [0, f, cy], [0, 0, 1.0]])
    M = np.linalg.inv(K) @ G
    lam = 1 / math.sqrt(np.linalg.norm(M[:, 0]) * np.linalg.norm(M[:, 1]))
    r1, r2, t = M[:, 0] * lam, M[:, 1] * lam, M[:, 2] * lam
    R = np.c_[r1, r2, np.cross(r1, r2)]
    return f, float(abs((-R.T @ t)[2]))


def parallax(src, dst, corners, width, size):
    """How far the key fit alone pins the camera, and what a front edge d mm
    below the key plane would measure if the camera were right: the width
    scales about the point under the camera by (h + d) / h. The camera's
    uncertainty, and the raw width's, is a jackknife over the keys (each left
    out in turn). The depths d are ASSUMED, not measured: this is an estimate
    of a likely cause, not a correction, and with h this loose it could not be
    one."""
    names = list(src)
    S = np.array([src[n] for n in names], float)
    D = np.array([dst[n] for n in names], float)
    f, h = camera(common._fit('homography', S, D)['H'], size)
    jack, widths = [], []
    for i in range(len(names)):
        keep = np.arange(len(names)) != i
        H = common._fit('homography', S[keep], D[keep])['H']
        e = common.transform('homography', {'H': H}, corners)
        widths.append(float(np.hypot(*(e[1] - e[0]))))
        c = camera(H, size)
        if c is not None:
            jack.append(c)
    def se(v):
        n = len(v)
        return float(math.sqrt((n - 1) / n * ((np.array(v) - np.mean(v)) ** 2).sum()))
    return {
        'what': 'Camera from the key fit alone (square pixels, principal point at the centre, f from the right-angle constraint), with a jackknife over the keys; the edge depths are assumed',
        'fPx': f, 'hMm': h,
        'jackknife': {'n': len(jack), 'fPxSe': se([c[0] for c in jack]), 'hMmSe': se([c[1] for c in jack])},
        'widthIfDepthAssumedMm': {f'{d}': width * (h + d) / h for d in (10, 15, 20, 25)},
        'rawWidthSeMm': se(widths),
    }


# --- judging -----------------------------------------------------------------------

def judge(s):
    sc, so, k = s['scale'], s['solder'], s['keys']
    e40 = sc['heldOut']['fortyPinRowErrMm']
    ratio_pct = 100 * abs(sc['ratio'] - 1)
    edge = abs(k['frontEdgeErrPct'])
    lines = [
        ('scale, 40-pin rows held out', e40['median'] <= PASS['scaleMedian'] and e40['max'] <= PASS['scaleMax'],
         e40['median'] > STOP['scaleMedian'] or e40['max'] > STOP['scaleMax'],
         f"median {e40['median']:.3f} mm, max {e40['max']:.3f} mm over {e40['n']} rows"),
        ('scale, x against y', True, ratio_pct > STOP['ratioPct'],
         f"x {sc['pxPerMm']['x']:.4f}, y {sc['pxPerMm']['y']:.4f} px/mm, ratio {sc['ratio']:.5f} ({ratio_pct:.2f} per cent)"),
        ('solder side, held out', so['heldOutMm']['median'] <= PASS['solderMedian'] and so['heldOutMm']['p90'] <= PASS['solderP90']
         and so['heldOutMm']['max'] <= PASS['solderMax'] and so['holes'] >= 200,
         so['heldOutMm']['median'] > STOP['solderMedian'] or so['heldOutMm']['p90'] > STOP['solderP90'],
         f"{so['fit']}: median {so['heldOutMm']['median']:.3f}, p90 {so['heldOutMm']['p90']:.3f}, max {so['heldOutMm']['max']:.3f} mm over {so['holes']} holes"),
        ('keys, each held out', k['heldOutMm']['median'] <= PASS['keysMedian'] and k['heldOutMm']['p90'] <= PASS['keysP90']
         and k['heldOutMm']['max'] <= PASS['keysMax'] and k['n'] >= 30,
         k['heldOutMm']['median'] > STOP['keysMedian'] or k['over3mm'] > STOP['keysOver3'],
         f"median {k['heldOutMm']['median']:.3f}, p90 {k['heldOutMm']['p90']:.3f}, max {k['heldOutMm']['max']:.3f} mm over {k['n']} keys, {k['over3mm']} over 3 mm"),
        ("O1's case front edge", edge <= PASS['frontEdgePct'], edge > STOP['frontEdgePct'],
         f"{k['frontEdgeMm']:.1f} mm against 415, {k['frontEdgeErrPct']:+.2f} per cent"),
    ]
    out = []
    for name, ok, stop, text in lines:
        verdict = 'STOP' if stop else ('pass' if ok else 'between pass and stop')
        out.append({'measure': name, 'verdict': verdict, 'figures': text})
        print(f'{name}: {text}: {verdict}')
    return out


def overlays(i1, i2m, s, solder_data):
    """Pictures to look at: the scale marks, and the matched holes' held-out errors."""
    import cv2
    from PIL import Image
    common.OUT.mkdir(exist_ok=True)
    img = i1.copy()
    for fp in marks()['scale']['footprints']:
        for (x, y) in fp['corners'].values():
            cv2.circle(img, (int(round(x)), int(round(y))), 14, (255, 0, 255), 3)
    Image.fromarray(img).resize((img.shape[1] // 3, img.shape[0] // 3)).save(common.OUT / 'scale-marks.jpg')
    p1, p2, dst, fits = solder_data
    best = fits[s['solder']['fit']]
    img = (i1 // 2).copy()
    for (x, y), (_, e) in zip(p1, best['heldOutEach']):
        c = (0, 255, 0) if e <= 0.15 else (255, 255, 0) if e <= 0.30 else (255, 0, 0)
        cv2.circle(img, (int(round(x)), int(round(y))), 10, c, -1)
    Image.fromarray(img).resize((img.shape[1] // 3, img.shape[0] // 3)).save(common.OUT / 'solder-held-out.jpg')


def main():
    i1 = common.read_rgb('I1')
    i2m = common.read_rgb('I2', mirror=True)
    pads1, pads2 = common.find_pads(i1), common.find_pads(i2m)
    A, sc = scale(i1, pads1)
    so, solder_data = solder(A, pads1, pads2)
    k, _ = keys()
    s = {'scale': sc, 'solder': so, 'keys': k}
    s['verdicts'] = judge(s)
    s['revision'] = ('The front edge verdict is kept as measured: it crossed the plan\'s 2.5 per cent stop. On 4 October 2026 the plan '
                     'was revised (docs/superpowers/plans/2026-10-04-bbc-micro-models.md, Global Constraints and the Task 0 outcome): the edge '
                     'is not in the key plane, so its raw width on the key-plane registration is recorded only, and task 8 judges the width '
                     'after a parallax correction against the same 1.5 and 2.5 per cent, judged on its interval, with h measured independently of the 415 mm. '
                     'The solder side\'s verdict is also kept as measured; the same revision made its largest error recorded, not a pass criterion, '
                     'with an outlier rule fixed in advance.')
    s['about'] = 'Task 0 of the BBC Micro models plan: the scan scale, the solder side registration and the keys, each on data held out of its fit. Written by spike.py; every figure here is a measurement made on the date of the journal entry that quotes it.'
    common.write_data('spike.json', s)
    overlays(i1, i2m, s, solder_data)
    return 3 if any(v['verdict'] == 'STOP' for v in s['verdicts']) else 0


if __name__ == '__main__':
    sys.exit(main())
