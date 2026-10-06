"""Task 3: the solder side registered to the component side, and the board's
pads, drills and footprints, from the bare Issue 7 scans of both faces (I1,
the component side, and I2, the solder side).

    BBC_MODEL_INPUTS=<folder> python board_register.py

Everything is in the board frame of data/frame.json (task 2): millimetres
from the board's left rear corner, the component side seen from above, x to
the right and y towards the front.

1. The holes on each face. A hole is found two ways. A tinned pad: on these
   scans most holes are filled with solder, so the hole is the solder blob's
   centre, found by common.find_pads exactly as in task 0 (grey, light, round
   and pad-sized). An open hole: a via, or a pad the solder did not fill,
   shows a bright copper ring round a dark middle; it is found where the ring
   is brighter than the middle in every one of eight sectors, and its centre
   is the centroid of the dark middle, which tracks joining the ring cannot
   pull. A ring must be copper seen through the green lacquer (green, not
   yellow) and clear of every solder blob: a dark speck in a tinned pad, or a
   letter of the print, is ring-shaped too, and the first run counted both.
   I2 is flipped left to right first, so that it lies as the component side
   does.

2. The registration, as task 0's spike, over every hole found. An affine from
   four holes marked by hand on both faces (data/marks.json) seeds the match;
   holes are paired when each is the other's nearest within 1.0 mm, the affine
   is refitted on the pairs and the match redone, three times. Then an affine
   and a cubic (a homography, then a cubic correction) from I2 mirrored to the
   board frame are each scored on a chequerboard of 20 mm blocks, each colour
   held out of the fit and measured under the fit to the other. The model is
   the one with the lower held-out median; within 0.005 mm of each other is a
   tie, and the simpler (the affine) wins it. Both rules were written here
   before the run.

   The outlier rule (the plan's, fixed before this task and using no
   residual): a matched hole is left out of the scoring only if its blob
   fails the roundness test on either face: the major to minor axis ratio of
   its second moments over 1.25, or its area outside 0.6 to 1.6 times the
   median area of the matched holes' blobs on that face. An open hole's blob
   is its ring and the middle it encloses. The figures are reported with and
   without the exclusion; the verdict is judged without it.

3. Pads and drills. Every blob of solder and every ring on either face is a
   pad; one on each face that pair up (each the other's nearest within
   1.0 mm, through the registration) is a pad on both faces with a drill
   between them, at the mean of the two faces' centres. A pair whose blobs
   fail the hole finder's shape gates on both faces places no drill (its
   centroids are not a hole's): it is a pad on both faces if neither blob is
   bigger than one pad, and is dropped if one is (pads bridged by solder). A drill's diameter is
   measured only where the hole is open (the ring's dark middle); a hole
   filled with solder has no diameter on these scans and says so.

4. Footprints. Pads 2.54 mm apart in a line are chained into rows, across the
   board and down it. Two rows facing each other pad for pad at 7.62, 10.16
   or 15.24 mm are a DIP; where a row could pair either way, the print
   decides: a DIP's outline runs just inside both its rows. Pin 1 is read off
   the print first (the outline's chamfered corner), then from a square pad,
   else it is marked by hand (data/marks.json). Rows left over are a
   connector (eight pads or more, or two rows 2.54 mm apart) or a SIP. Each
   footprint's reference is read from the print by hand (data/marks.json),
   because nothing here reads text.

5. The mounting holes. Task 2's circle stops at the edge of the lid seen
   through a hole, inside the grey crescent of the hole's far wall. Here each
   is fitted on its top rim: the scanned face lies on the glass, so the near
   rim bounds the pale region on the side away from the crescent and the
   crescent's outer edge continues it on the other. Along rays from the
   middle, the rim is the first place the light falls below half way between
   the lid and the lacquer round the hole, or the colour rises to the
   print's; a circle is fitted robustly. The difference from task 2's centres
   is recorded.

Writes data/registration.json and overlays to look at in out/ (git-ignored:
I1 and I2 state no licence). Exits 3 when a judged figure crosses one of the
plan's STOP thresholds.
"""
import json
import math
import sys

import numpy as np

import board_frame
import common

PITCH = 2.54
# The plan's thresholds for the solder side (Global Constraints), set before
# the measurements: the median and 90th percentile judged, the largest recorded.
PASS = {'median': 0.15, 'p90': 0.30}
STOP = {'median': 0.25, 'p90': 0.50}
MIN_HOLES = 200
# Task 0's match and folds, kept.
MATCH_GATE_MM = 1.0
BLOCK_MM = 20.0
MATCH_ROUNDS = 3
# A tie between the two models' held-out medians (written before the run).
TIE_MM = 0.005
# The plan's outlier rule, fixed on 4 October 2026 before this task.
ROUND_MAX_AXIS = 1.25
ROUND_AREA = (0.6, 1.6)

# Open holes: a bright ring round a dark middle. Read off I1 and I2 before
# any fit (a via at I1 (2715, 1585), I2 mirrored (2679, 1561)): the ring is L
# 0.7 to 0.95 out to about 0.5 mm, the middle L 0.2 to 0.35 inside about
# 0.28 mm, the lacquer round it about 0.28. The print is yellow, chroma 0.12
# and more, and its letters can be rings too, so a ring must not be that
# colourful (the copper ring's chroma is 0.04 to 0.08).
RING_CENTRE_MM = 0.20
RING_ANNULUS_MM = (0.34, 0.50)
RING_MIN_CONTRAST = 0.20      # every sector of the ring this much lighter than the middle
RING_MAX_MIDDLE_L = 0.50
RING_MAX_CHROMA = 0.10
RING_SECTORS = 8
RING_BLOB_MM = 0.8            # the window a ring's blob (for the roundness rule) is taken in
# A ring is copper seen through the green lacquer, not solder and not print.
# Read off the first run's rings on 4 October 2026, before these rules
# existed: the lighter half of a via's ring (rings over 3 mm from any solder
# blob, looked at) has OKLab a -0.068 to -0.007 (median -0.054) and b 0.008
# to 0.047; a speck in a tinned pad (rings within 0.6 mm of a solder blob's
# centre, looked at) is solder, a -0.016 to 0 and chroma about 0.01; a letter
# of the print is yellow, b 0.05 to 0.12. The first run counted those specks
# as second holes beside their pads, and letters as holes.
RING_MAX_A = -0.02
RING_MAX_B = 0.05
# A ring whose centre is this near a solder blob's would overlap it (a pad's
# radius is about 0.85 mm, a via ring's 0.5): it is part of that pad.
RING_SOLDER_CLEAR_MM = 1.3
# Every blob of solder that could be a pad, kept as a pad (the hole finder is
# stricter: common.PAD_AREA_PX and its shape gates).
PAD_KEEP_PX = (150, 2500)

# Rows and footprints.
PITCH_TOL_MM = 0.35           # a neighbour in a row: 2.54 mm along, within this
ACROSS_TOL_MM = 0.50          # and within this across (IC1's last pad sits 0.46 off its row's line)
FACING_TOL_MM = 0.50          # two rows' pads face each other within this along
SPACINGS_MM = (7.62, 10.16, 15.24)
SPACING_TOL_MM = 0.50
DIP_ROW_PADS = (7, 20)        # 14 to 40 pins
ROW_MAX_STEP = 3              # a DIP's row may lack up to two pads in a row and stay one row
COMPLETE_MM = 0.6             # a spare blob this near where a missing pin goes is its pad
# A DIP's print outline runs inside both its rows: every pairing accepted on
# I1 scored 0.74 or more, and the wrong pairings it was the evidence against
# (a row and the next DIP's row, 7.62 or 15.24 mm apart) 0.1 to 0.2, read on
# the first run before this floor was set; it sits between them.
OUTLINE_MIN = 0.5
CONNECTOR_MIN_PADS = 8
SIP_MIN_PADS = 3
# The print: yellow. Read off I1's outlines (OKLab b 0.10 to 0.16, L over
# 0.7) and the lacquer and copper (b under 0.05).
PRINT_MIN_B, PRINT_MIN_L = 0.08, 0.55
OUTLINE_OFFSETS_MM = (0.7, 1.5)    # a DIP's outline runs this far inside each row
CHAMFER_BOX_MM = ((-0.6, 0.6), (1.6, 2.6))   # (along into the footprint, across towards the other row)
# The chamfer's share of the box, and its margin over the next corner's. The
# outline's long sides cross the box at every corner of the narrow DIPs (they
# run about 1.9 mm inside the rows there, 1.1 mm on IC1), adding 0.2 to 0.35
# to every corner, so the chamfer is told by its margin. Set on 4 October
# 2026 after the first grouping on I1 was seen (a ratio of two to the next
# corner missed 11 chamfers that were plainly there; a floor of 0.30 then
# lost the wide DIPs' clean chamfers, about 0.2 against 0 at every other
# corner); every pin 1 is drawn on the footprint overlay and was looked at.
CHAMFER_MIN = 0.15
CHAMFER_MARGIN = 0.10
SQUARE_MIN = 0.90             # a square pad fills its smallest rectangle (a disc fills pi / 4)
SQUARE_OTHERS_MAX = 0.86

# The mounting holes' top rims.
RIM_RAYS = 180
RIM_RANGE = (0.3, 1.9)        # the rays run from and to these multiples of task 2's radius
RIM_STOP_CHROMA = 0.10        # the print round a hole stops a ray as the lacquer's dark does


# --- blobs and their shapes ----------------------------------------------------------

def axis_ratio(xs, ys, sx, sy):
    """Major to minor axis of the ellipse from a blob's second moments, in
    millimetres (sx, sy pixels per millimetre)."""
    p = np.c_[np.asarray(xs, float) / sx, np.asarray(ys, float) / sy]
    if len(p) < 3:
        return float('inf')
    w = np.linalg.eigvalsh(np.cov(p.T))
    return float(math.sqrt(w[1] / max(w[0], 1e-12)))


def squareness(mask):
    """A blob's area over its smallest bounding rectangle's (pixel counts, so
    the rectangle through the outermost pixels' centres is one pixel larger
    each way): a square about 1, a disc about pi / 4."""
    import cv2
    cs, _ = cv2.findContours(mask.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if not cs:
        return 0.0
    (_, _), (w, h), _ = cv2.minAreaRect(max(cs, key=len))
    return float(mask.sum() / ((w + 1) * (h + 1)))


def _min_rect_mm(mask, sx, sy):
    import cv2
    ys, xs = np.nonzero(mask)
    pts = np.c_[xs / sx, ys / sy].astype(np.float32)
    (_, _), (w, h), _ = cv2.minAreaRect(pts)
    return float(max(w, h) + 1 / sx), float(min(w, h) + 1 / sy)


def solder_blobs(rgb, sx, sy):
    """Every blob of solder big enough to be a pad, with its shape, and
    whether it passes the hole finder's gates (common.find_pads, unchanged)."""
    import cv2
    n, lab, st, cen = cv2.connectedComponentsWithStats(common.pad_mask(rgb), 8)
    out = []
    for i in range(1, n):
        x, y, w, h, a = st[i]
        if not PAD_KEEP_PX[0] <= a <= PAD_KEEP_PX[1]:
            continue
        hole = (common.PAD_AREA_PX[0] <= a <= common.PAD_AREA_PX[1] and max(w, h) <= 1.5 * min(w, h)
                and a / (math.pi * (max(w, h) / 2) ** 2) >= 0.7)
        sub = lab[y:y + h, x:x + w] == i
        ys, xs = np.nonzero(sub)
        W, H = _min_rect_mm(sub, sx, sy)
        out.append({'kind': 'solder', 'px': (float(cen[i][0]), float(cen[i][1])), 'area': int(a),
                    'axis': axis_ratio(xs, ys, sx, sy), 'square': squareness(sub), 'w': W, 'h': H,
                    'hole': bool(hole), 'd': None})
    return out


def _ring_kernels(s):
    rc = RING_CENTRE_MM * s
    r0, r1 = RING_ANNULUS_MM[0] * s, RING_ANNULUS_MM[1] * s
    k = int(math.ceil(r1)) + 1
    yy, xx = np.mgrid[-k:k + 1, -k:k + 1].astype(np.float32)
    rr = np.hypot(xx, yy)
    ang = np.mod(np.arctan2(yy, xx), 2 * math.pi)
    centre = (rr <= rc).astype(np.float32)
    sectors = []
    for j in range(RING_SECTORS):
        m = ((rr >= r0) & (rr <= r1) & (ang >= 2 * math.pi * j / RING_SECTORS) & (ang < 2 * math.pi * (j + 1) / RING_SECTORS)).astype(np.float32)
        sectors.append(m / m.sum())
    annulus = ((rr >= r0) & (rr <= r1)).astype(np.float32)
    return centre / centre.sum(), sectors, annulus / annulus.sum()


def rings(rgb, sx, sy, solder=()):
    """Open holes: a bright ring lighter than its dark middle in every sector.
    Each with its centre (the centroid of the dark middle), its diameter, and
    its blob (the ring and the middle it encloses) for the roundness rule."""
    import cv2
    L, A, B = common.oklab(rgb)
    L = L.astype(np.float32)
    C = np.hypot(A, B).astype(np.float32)
    s = math.sqrt(sx * sy)
    kc, ks, ka = _ring_kernels(s)
    mid = cv2.filter2D(L, -1, kc, borderType=cv2.BORDER_REPLICATE)
    low = None
    for k in ks:
        v = cv2.filter2D(L, -1, k, borderType=cv2.BORDER_REPLICATE)
        low = v if low is None else np.minimum(low, v)
    chroma = cv2.filter2D(C, -1, ka, borderType=cv2.BORDER_REPLICATE)
    score = low - mid
    score[(mid > RING_MAX_MIDDLE_L) | (chroma > RING_MAX_CHROMA)] = 0
    nms = int(round(RING_ANNULUS_MM[1] * s)) * 2 + 1
    peak = (score >= cv2.dilate(score, np.ones((nms, nms), np.uint8))) & (score > RING_MIN_CONTRAST)
    ys, xs = np.nonzero(peak)
    if len(solder):
        from scipy.spatial import cKDTree
        d, _ = cKDTree(np.array([b['px'] for b in solder], float)).query(np.c_[xs, ys])
        clear = d / s >= RING_SOLDER_CLEAR_MM
        ys, xs = ys[clear], xs[clear]
    order = np.argsort(-score[ys, xs])
    yy0, xx0 = np.mgrid[-int(math.ceil(RING_ANNULUS_MM[1] * s)):int(math.ceil(RING_ANNULUS_MM[1] * s)) + 1,
                        -int(math.ceil(RING_ANNULUS_MM[1] * s)):int(math.ceil(RING_ANNULUS_MM[1] * s)) + 1]
    ann = (np.hypot(xx0, yy0) >= RING_ANNULUS_MM[0] * s) & (np.hypot(xx0, yy0) <= RING_ANNULUS_MM[1] * s)
    ka = yy0.shape[0] // 2
    taken = []
    out = []
    half = int(math.ceil(RING_BLOB_MM * s))
    for i in order:
        x, y = int(xs[i]), int(ys[i])
        if any((x - a) ** 2 + (y - b) ** 2 < (RING_ANNULUS_MM[1] * s) ** 2 for a, b in taken):
            continue
        if x < half or y < half or x >= L.shape[1] - half or y >= L.shape[0] - half:
            continue
        taken.append((x, y))
        # the ring's colour, on its lighter half: copper under green lacquer
        la, aa, ba = L[y - ka:y + ka + 1, x - ka:x + ka + 1][ann], A[y - ka:y + ka + 1, x - ka:x + ka + 1][ann], B[y - ka:y + ka + 1, x - ka:x + ka + 1][ann]
        light_half = la > np.median(la)
        if aa[light_half].mean() > RING_MAX_A or ba[light_half].mean() > RING_MAX_B:
            continue
        win = L[y - half:y + half + 1, x - half:x + half + 1]
        t = (mid[y, x] + low[y, x]) / 2
        yy, xx = np.mgrid[-half:half + 1, -half:half + 1]
        dark = (win < t) & (np.hypot(xx, yy) < RING_ANNULUS_MM[0] * s)
        n, lab = cv2.connectedComponents(dark.astype(np.uint8), connectivity=4)
        if n < 2:
            continue
        # the dark region nearest the peak
        best = min(range(1, n), key=lambda j: np.hypot(xx[lab == j], yy[lab == j]).min())
        m = lab == best
        wgt = np.clip(t - win, 0, None) * m
        cx = x + float((wgt * xx).sum() / wgt.sum())
        cy = y + float((wgt * yy).sum() / wgt.sum())
        d = 2 * math.sqrt(m.sum() / math.pi) / s
        # the blob: the ring's light pixels and the middle they enclose
        light = (win >= t).astype(np.uint8)
        filled = light | m.astype(np.uint8)
        n2, lab2 = cv2.connectedComponents(filled, connectivity=8)
        blob = lab2 == lab2[half, half] if lab2[half, half] else filled.astype(bool)
        by, bx = np.nonzero(blob)
        W, H = _min_rect_mm(blob, sx, sy)
        out.append({'kind': 'ring', 'px': (cx, cy), 'area': int(blob.sum()), 'axis': axis_ratio(bx, by, sx, sy),
                    'square': squareness(blob), 'w': W, 'h': H, 'hole': True, 'd': float(d),
                    'contrast': float(score[y, x])})
    return out


def pads_of_one_face(rgb, sx, sy, to_board=None):
    """Every pad on one face: blobs of solder and open rings, each with its
    pixel centre and, given to_board, its place in board millimetres."""
    solder = solder_blobs(rgb, sx, sy)
    pads = solder + rings(rgb, sx, sy, solder)
    if to_board is not None:
        xy = to_board(np.array([p['px'] for p in pads], float).reshape(-1, 2))
        for p, q in zip(pads, xy):
            p['x'], p['y'] = float(q[0]), float(q[1])
    return pads


# --- the registration ------------------------------------------------------------------

def mutual_pairs(a, b, gate):
    """(i, j) where a[i] and b[j] are each the other's nearest, within gate."""
    from scipy.spatial import cKDTree
    if not len(a) or not len(b):
        return []
    da, ia = cKDTree(b).query(a)
    _, ib = cKDTree(a).query(b)
    return [(i, int(j)) for i, (d, j) in enumerate(zip(da, ia)) if d <= gate and ib[j] == i]


def seed_handedness(src, dst):
    """The sign of the determinant of the affine through the seeds: positive
    when the solder side was mirrored before it was marked, as it must be."""
    M = np.array(common._fit('affine', np.asarray(src, float), np.asarray(dst, float))['matrix'])
    return float(np.linalg.det(M[:, :2]))


def roundness_exclusions(top, bottom):
    """The plan's outlier rule, on the blobs alone: each matched hole whose
    blob on either face has an axis ratio over 1.25, or an area outside 0.6
    to 1.6 times the median of that face's matched blobs. top and bottom are
    lists of {"axis", "area"}, one per matched hole, in the same order.
    Returns [{"index", "why": [reasons]}] in order."""
    medians = {'top': float(np.median([b['area'] for b in top])), 'bottom': float(np.median([b['area'] for b in bottom]))}
    out = []
    for i, (t, b) in enumerate(zip(top, bottom)):
        why = []
        for face, blob in (('top', t), ('bottom', b)):
            if blob['axis'] > ROUND_MAX_AXIS:
                why.append(f"{face}: axis ratio {blob['axis']:.2f} over {ROUND_MAX_AXIS}")
            r = blob['area'] / medians[face]
            if not ROUND_AREA[0] <= r <= ROUND_AREA[1]:
                why.append(f"{face}: area {r:.2f} times the face's median, outside {ROUND_AREA[0]} to {ROUND_AREA[1]}")
        if why:
            out.append({'index': i, 'why': why})
    return out


def choose_model(fits):
    """The lower held-out median; within TIE_MM of each other the affine."""
    a, c = fits['affine']['heldOutMm']['median'], fits['cubic']['heldOutMm']['median']
    return 'cubic' if c < a - TIE_MM else 'affine'


def register_scans(i1, i2m, seeds, frame):
    """I2 mirrored registered to the board frame on every hole found on both
    faces. seeds: [{"I1": [x, y], "I2mirrored": [x, y]}] (pixels). Returns
    the result, with the faces' pads and the matched pairs."""
    sx, sy = frame.sx, frame.sy
    faces = {'I1': pads_of_one_face(i1, sx, sy, frame.to_board), 'I2': pads_of_one_face(i2m, sx, sy)}
    holes = {f: [i for i, p in enumerate(v) if p['hole']] for f, v in faces.items()}
    h1 = np.array([faces['I1'][i]['px'] for i in holes['I1']], float).reshape(-1, 2)
    h2 = np.array([faces['I2'][i]['px'] for i in holes['I2']], float).reshape(-1, 2)
    s1 = np.array([s['I1'] for s in seeds], float)
    s2 = np.array([s['I2mirrored'] for s in seeds], float)
    if seed_handedness(s2, s1) <= 0:
        raise ValueError('the seeds need a mirror: is I2 flipped left to right before marking?')
    seed = common.fit_held_out(s2, s1, 'affine', 'leave-one-out')
    params = seed['params']
    gate_px = MATCH_GATE_MM * math.sqrt(sx * sy)
    for _ in range(MATCH_ROUNDS):
        pairs = mutual_pairs(h1, common.transform('affine', params, h2), gate_px)
        params = common._fit('affine', h2[[j for _, j in pairs]], h1[[i for i, _ in pairs]])
    pairs = mutual_pairs(h1, common.transform('affine', params, h2), gate_px)
    src = h2[[j for _, j in pairs]]
    dst = frame.to_board(h1[[i for i, _ in pairs]])
    folds = [int(x // BLOCK_MM + y // BLOCK_MM) % 2 for x, y in dst]
    fits = {m: common.fit_held_out(src, dst, m, folds) for m in ('affine', 'cubic')}
    model = choose_model(fits)
    chosen = fits[model]
    bottom = common.transform(model, chosen['params'], src)
    out_pairs = []
    for k, (i, j) in enumerate(pairs):
        t, b = faces['I1'][holes['I1'][i]], faces['I2'][holes['I2'][j]]
        out_pairs.append({'i1': holes['I1'][i], 'i2': holes['I2'][j], 'top': dst[k].tolist(), 'bottom': bottom[k].tolist(),
                          'heldOutMm': chosen['heldOutEach'][k][1], 'kinds': [t['kind'], b['kind']],
                          'blobs': [{'axis': t['axis'], 'area': t['area']}, {'axis': b['axis'], 'area': b['area']}]})
    for p in faces['I2']:
        q = common.transform(model, chosen['params'], [p['px']])[0]
        p['x'], p['y'] = float(q[0]), float(q[1])
    excluded = roundness_exclusions([p['blobs'][0] for p in out_pairs], [p['blobs'][1] for p in out_pairs])
    keep = np.ones(len(out_pairs), bool)
    keep[[e['index'] for e in excluded]] = False
    held = np.array([p['heldOutMm'] for p in out_pairs])
    by_kind = {}
    for kinds in ({'solder'}, {'ring'}):
        sel = np.array([set(p['kinds']) <= kinds for p in out_pairs])
        if sel.any():
            by_kind['+'.join(sorted(kinds))] = dict(common.stats(held[sel]), n=int(sel.sum()))
    return {
        'found': {f: {'solder': sum(p['kind'] == 'solder' and p['hole'] for p in v), 'rings': sum(p['kind'] == 'ring' for p in v),
                      'pads': len(v)} for f, v in faces.items()},
        'seedFitPx': seed['fitMm'],
        'gateMm': MATCH_GATE_MM, 'blockMm': BLOCK_MM, 'tieMm': TIE_MM,
        'holes': len(out_pairs),
        'model': model,
        'params': chosen['params'],
        'heldOutMm': chosen['heldOutMm'],
        'fits': {k: {'fitMm': v['fitMm'], 'heldOutMm': v['heldOutMm']} for k, v in fits.items()},
        'heldOutByKind': by_kind,
        'excluded': excluded,
        'heldOutMmWithExclusion': common.stats(held[keep]),
        'pairs': out_pairs,
        'faces': faces,
    }


# --- pads and drills ------------------------------------------------------------------

def pads_and_drills(faces, gate_mm=MATCH_GATE_MM):
    """Pads on both faces with a drill between them, and pads seen on one face
    only. faces: the register's pads, each with x, y in board millimetres."""
    top, bot = faces['I1'], faces['I2']
    a = np.array([[p['x'], p['y']] for p in top], float).reshape(-1, 2)
    b = np.array([[p['x'], p['y']] for p in bot], float).reshape(-1, 2)
    pairs = mutual_pairs(a, b, gate_mm)
    drills, pads, dropped = [], [], 0
    used_t, used_b = set(), set()
    for i, j in pairs:
        t, u = top[i], bot[j]
        used_t.add(i)
        used_b.add(j)
        if not (t['hole'] or u['hole']):
            # Ragged on both faces, so neither centroid places a drill. Two
            # blobs no bigger than one pad are still a pad on both faces (a
            # DIP's pin, say); bigger ones may be pads bridged by solder.
            if max(t['area'], u['area']) <= common.PAD_AREA_PX[1]:
                pads.append(_pad(t, (t['x'] + u['x']) / 2, (t['y'] + u['y']) / 2, 'both', None, 'pair, not hole-shaped'))
            else:
                dropped += 1
            continue
        x, y = (t['x'] + u['x']) / 2, (t['y'] + u['y']) / 2
        ds = [p['d'] for p in (t, u) if p['kind'] == 'ring']
        drills.append({'x': x, 'y': y, 'd': float(np.mean(ds)) if ds else None, 'filled': not ds,
                       'top': [t['x'], t['y']], 'bottom': [u['x'], u['y']],
                       'spreadMm': float(math.hypot(t['x'] - u['x'], t['y'] - u['y'])), 'kinds': [t['kind'], u['kind']]})
        pads.append(_pad(t, x, y, 'both', len(drills) - 1, 'drill'))
    single = {'top': 0, 'bottom': 0}
    for face, plist, used in (('top', top, used_t), ('bottom', bot, used_b)):
        for i, p in enumerate(plist):
            # A pad on one face only: a blob of solder that passes the hole
            # finder's gates, with no partner. A ring with no partner is not
            # kept: an open hole shows on both faces.
            if i in used or p['kind'] != 'solder' or not p['hole']:
                continue
            pads.append(_pad(p, p['x'], p['y'], face, None, 'one face'))
            single[face] += 1
    # what is left on either face, to complete a footprint's rows: every blob
    # of solder and ring that is not a pad above
    spare = [dict(p, face=face) for face, plist, used in (('top', top, used_t), ('bottom', bot, used_b))
             for i, p in enumerate(plist) if i not in used and not (p['kind'] == 'solder' and p['hole'])]
    return drills, pads, {'pairs': len(pairs), 'notHoles': dropped, 'singleFace': single}, spare


def _pad(p, x, y, face, drill, source):
    if p['axis'] > 1.4:
        shape = 'oval'
    elif p['square'] >= SQUARE_MIN and p['axis'] <= 1.15:
        shape = 'square'
    else:
        shape = 'round'
    return {'x': x, 'y': y, 'w': p['w'], 'h': p['h'], 'shape': shape, 'face': face, 'drill': drill,
            'square': p['square'], 'kind': p['kind'], 'source': source}


# --- footprints --------------------------------------------------------------------------

def _chains(P, axis, max_step=1):
    """Rows of pads on the 2.54 mm pitch along `axis` (0: across, x; 1: down,
    y): lists of pad indexes in order. A step of up to `max_step` pitches is
    a link, so a row with a pad not found stays one row."""
    from scipy.spatial import cKDTree
    tree = cKDTree(P)
    nxt, prv = {}, {}
    for i, p in enumerate(P):
        best = None
        for j in tree.query_ball_point(p, max_step * PITCH + PITCH_TOL_MM):
            d = P[j] - p
            along, across = d[axis], d[1 - axis]
            k = round(along / PITCH)
            if 1 <= k <= max_step and abs(along - k * PITCH) <= PITCH_TOL_MM and abs(across) <= ACROSS_TOL_MM:
                err = (k, abs(along - k * PITCH) + abs(across))
                if best is None or err < best[0]:
                    best = (err, j)
        if best is not None:
            nxt[i] = best[1]
    for i, j in sorted(nxt.items(), key=lambda kv: abs(P[kv[1]][axis] - P[kv[0]][axis])):
        if j in prv:               # two pads claim the same next: the nearer has it
            del nxt[i]
        else:
            prv[j] = i
    chains = []
    for i in range(len(P)):
        if i in prv or i not in nxt:
            continue
        c = [i]
        while c[-1] in nxt:
            c.append(nxt[c[-1]])
        chains.append(c)
    return chains


def print_mask(rgb):
    L, A, B = common.oklab(rgb)
    return (B > PRINT_MIN_B) & (L > PRINT_MIN_L)


def _sample(mask, pts_px):
    from scipy.ndimage import map_coordinates
    pts_px = np.asarray(pts_px, float).reshape(-1, 2)
    return map_coordinates(mask.astype(np.float32), [pts_px[:, 1], pts_px[:, 0]], order=1, mode='constant')


def outline_score(pm, to_px, row, other, axis):
    """How much of the print runs along the inside of `row` (towards
    `other`), 0.7 to 1.5 mm in: the share of points on the best line."""
    side = math.copysign(1, other[0][1 - axis] - row[0][1 - axis])
    best = 0.0
    a, b = row[0][axis], row[-1][axis]
    us = np.arange(a, b + 1e-9, 0.25)
    base = np.interp(us, [a, b], [row[0][1 - axis], row[-1][1 - axis]])
    for off in np.arange(OUTLINE_OFFSETS_MM[0], OUTLINE_OFFSETS_MM[1] + 1e-9, 0.1):
        v = base + side * off
        pts = np.c_[us, v] if axis == 0 else np.c_[v, us]
        best = max(best, float((_sample(pm, to_px(pts)) > 0.5).mean()))
    return best


def chamfer_share(pm, to_px, corner, along_in, across_in, axis):
    """The print's share of a small box inside a footprint's corner, where
    the outline's chamfer at pin 1 crosses."""
    (u0, u1), (v0, v1) = CHAMFER_BOX_MM
    us, vs = np.meshgrid(np.arange(u0, u1 + 1e-9, 0.1), np.arange(v0, v1 + 1e-9, 0.1))
    du, dv = us.ravel() * along_in, vs.ravel() * across_in
    pts = np.c_[corner[0] + du, corner[1] + dv] if axis == 0 else np.c_[corner[0] + dv, corner[1] + du]
    return float((_sample(pm, to_px(pts)) > 0.5).mean())


def _slots(P, chain, u0, axis):
    """Each pad of a chain on the pitch grid from u0: {slot: pad}."""
    out = {}
    for i in chain:
        s = round((P[i][axis] - u0) / PITCH)
        if abs(P[i][axis] - u0 - s * PITCH) <= FACING_TOL_MM:
            if s not in out or abs(P[i][axis] - u0 - s * PITCH) < abs(P[out[s]][axis] - u0 - s * PITCH):
                out[s] = i
    return out


def _facing_runs(P, a, b, axis, covered=None):
    """Each run of pitch slots along two rows where at least one row has a
    pad in each slot (a slot empty on both rows ends a run), trimmed so that
    both rows have a pad at both ends (a DIP's four corner pads are found):
    pairs of lists of the same length, in order along the rows, None where
    that row's pad was not found. covered(q), given, says whether solder
    covers board millimetre q: an end slot with one row's pad found and
    solder at the other's place counts as both found."""
    u0 = P[a[0]][axis]
    sa, sb = _slots(P, a, u0, axis), _slots(P, b, u0, axis)
    slots = sorted(set(sa) | set(sb))
    runs, cur = [], []
    for s in slots + [None]:
        if s is not None and (not cur or s == cur[-1] + 1):
            cur.append(s)
        else:
            if cur:
                runs.append(cur)
            cur = [s] if s is not None else []
    la, lb = _row_line(P, a, axis), _row_line(P, b, axis)

    def both(s):
        if s in sa and s in sb:
            return True
        if covered is None or (s not in sa and s not in sb):
            return False
        # one row's pad found; the other row's place covered by solder (a pin
        # under a blob too ragged or too big to be a pad on its own)
        u = u0 + s * PITCH
        line = lb if s in sa else la
        return covered((u, line(u)) if axis == 0 else (line(u), u))

    out = []
    for r in runs:
        while r and not both(r[0]):
            r = r[1:]
        while r and not both(r[-1]):
            r = r[:-1]
        if r:
            out.append(([sa.get(s) for s in r], [sb.get(s) for s in r]))
    return out


def _row_line(P, row, axis):
    """across = c0 + c1 along, through a row's found pads."""
    q = np.array([P[i] for i in row if i is not None])
    if len(q) < 2:
        return lambda u: float(q[0][1 - axis])
    c1, c0 = np.polyfit(q[:, axis], q[:, 1 - axis], 1)
    return lambda u: float(c0 + c1 * u)


def _complete(c, P, pads, spare, axis):
    """Each pin whose pad was not among the pads: the nearest spare blob of
    solder or ring (either face) within COMPLETE_MM of where the row puts it,
    else a pad inferred at that place. Appends to pads; returns the rows."""
    from scipy.spatial import cKDTree
    S = np.array([[s['x'], s['y']] for s in spare], float).reshape(-1, 2)
    tree = cKDTree(S) if len(S) else None
    rows = []
    for row in (c['a'], c['b']):
        line = _row_line(P, row, axis)
        known = [(k, P[i][axis]) for k, i in enumerate(row) if i is not None]
        ks, us = zip(*known)
        pitch, u0 = np.polyfit(ks, us, 1) if len(known) > 1 else (PITCH, us[0])
        out = []
        for k, i in enumerate(row):
            if i is not None:
                out.append(i)
                continue
            u = u0 + pitch * k
            q = (u, line(u)) if axis == 0 else (line(u), u)
            src = None
            if tree is not None:
                d, j = tree.query(q)
                if d <= COMPLETE_MM and not spare[j].get('taken'):
                    s = spare[j]
                    s['taken'] = True
                    pads.append(_pad(s, s['x'], s['y'], s['face'], None, 'completion'))
                    src = 'completion'
            if src is None:
                w = float(np.median([pads[r]['w'] for r in row if r is not None]))
                pads.append({'x': float(q[0]), 'y': float(q[1]), 'w': w, 'h': w, 'shape': 'round', 'face': 'both', 'drill': None,
                             'square': 0.0, 'kind': 'inferred', 'source': 'inferred'})
            out.append(len(pads) - 1)
        rows.append(out)
    return rows


def group_footprints(pads, to_px, rgb, spare=()):
    """DIPs, connectors and SIPs from pads in board millimetres. to_px maps
    board millimetres to the component side's pixels (rgb), where the print
    is read, and where solder covering a pin's place is looked for. spare:
    blobs on either face that are not pads (each with x, y
    and face), to complete a DIP's rows; pins still missing are inferred.
    Pads are appended to `pads` for both."""
    spare = [dict(s) for s in spare]
    P = np.array([[p['x'], p['y']] for p in pads], float)
    pm = print_mask(rgb)
    solder = common.pad_mask(rgb)

    def covered(q):
        x, y = to_px([q])[0]
        return bool(solder[int(round(min(max(y, 0), solder.shape[0] - 1))), int(round(min(max(x, 0), solder.shape[1] - 1)))])
    cands = []
    chains = {0: _chains(P, 0), 1: _chains(P, 1)}
    for axis in (0, 1):
        cs = [c for c in _chains(P, axis, max_step=ROW_MAX_STEP) if len(c) >= DIP_ROW_PADS[0] - 2]
        for ia in range(len(cs)):
            for ib in range(ia + 1, len(cs)):
                a, b = cs[ia], cs[ib]
                across = abs(np.median(P[b][:, 1 - axis]) - np.median(P[a][:, 1 - axis]))
                spacing = min(SPACINGS_MM, key=lambda s: abs(s - across))
                if abs(spacing - across) > SPACING_TOL_MM:
                    continue
                for ra, rb in _facing_runs(P, a, b, axis, covered):
                    if not DIP_ROW_PADS[0] <= len(ra) <= DIP_ROW_PADS[1]:
                        continue
                    fa, fb = [i for i in ra if i is not None], [i for i in rb if i is not None]
                    if min(len(fa), len(fb)) < len(ra) / 2:
                        continue                # most of both rows' pads are found, or it is not a DIP
                    score = min(outline_score(pm, to_px, P[fa], P[fb], axis), outline_score(pm, to_px, P[fb], P[fa], axis))
                    cands.append({'axis': axis, 'a': ra, 'b': rb, 'spacing': spacing, 'outline': score})
    cands.sort(key=lambda c: (-round(c['outline'], 2), -len(c['a']), c['spacing']))
    used, fps = set(), []
    for c in cands:
        found = {i for i in c['a'] + c['b'] if i is not None}
        if used & found or c['outline'] < OUTLINE_MIN:
            continue
        used |= found
        missing = sum(i is None for i in c['a'] + c['b'])
        c['a'], c['b'] = _complete(c, P, pads, spare, c['axis'])
        P = np.array([[p['x'], p['y']] for p in pads], float)
        fp = _dip(c, P, pads, pm, to_px)
        fp['padsNotFound'] = missing
        fp['padsInferred'] = sum(pads[i].get('source') == 'inferred' for i in fp['pads'])
        fps.append(fp)
    # rows left over: connectors (a double row 2.54 mm apart, or eight pads
    # or more) and SIPs
    rest = []
    for axis in (0, 1):
        for c in chains[axis]:
            left = [i for i in c if i not in used]
            if len(left) == len(c) and len(c) >= SIP_MIN_PADS:
                rest.append((axis, c))
    rest.sort(key=lambda r: -len(r[1]))
    for k, (axis, c) in enumerate(rest):
        if used & set(c):
            continue
        group = [c]
        for axis2, c2 in rest[k + 1:]:
            if axis2 != axis or used & set(c2) or len(c2) != len(c):
                continue
            across = abs(np.median(P[c2][:, 1 - axis]) - np.median(P[c][:, 1 - axis]))
            if abs(across - PITCH) <= PITCH_TOL_MM and abs(P[c2][0][axis] - P[c][0][axis]) <= FACING_TOL_MM:
                group.append(c2)
                break
        idx = [i for g in group for i in g]
        used |= set(idx)
        kind = 'connector' if len(group) == 2 or len(c) >= CONNECTOR_MIN_PADS else 'sip'
        fps.append({'ref': None, 'kind': kind, 'pins': len(idx), 'pin1': P[c[0]].tolist(), 'pin1From': 'not marked: the first pad along the row',
                    'pads': idx, 'box': _box(P[idx]), 'axis': 'x' if axis == 0 else 'y'})
    return fps


def _box(Q, pad=1.0):
    return [float(Q[:, 0].min() - pad), float(Q[:, 1].min() - pad), float(Q[:, 0].max() + pad), float(Q[:, 1].max() + pad)]


def _dip(c, P, pads, pm, to_px):
    axis, a, b = c['axis'], c['a'], c['b']
    n = len(a)
    corners = {('a', 0): a[0], ('a', 1): a[-1], ('b', 0): b[0], ('b', 1): b[-1]}
    shares, squares = {}, {}
    for (row, end), i in corners.items():
        other = b if row == 'a' else a
        along_in = 1.0 if end == 0 else -1.0
        across_in = math.copysign(1, P[other[0]][1 - axis] - P[i][1 - axis])
        shares[(row, end)] = chamfer_share(pm, to_px, P[i], along_in, across_in, axis)
        squares[(row, end)] = pads[i].get('square', 0.0)
    ranked = sorted(shares.items(), key=lambda kv: -kv[1])
    pin1, how = None, None
    if ranked[0][1] >= CHAMFER_MIN and ranked[0][1] - ranked[1][1] >= CHAMFER_MARGIN:
        pin1, how = ranked[0][0], 'print'
    else:
        sq = [k for k, v in squares.items() if v >= SQUARE_MIN]
        if len(sq) == 1 and all(v <= SQUARE_OTHERS_MAX for k, v in squares.items() if k != sq[0]):
            pin1, how = sq[0], 'square pad'
    if pin1 is None:
        pin1 = ('a', 0)              # a placeholder until a hand mark says which
        how = None
    order = _pin_order(a, b, pin1)
    return {'ref': None, 'kind': 'dip', 'pins': 2 * n, 'pin1': P[order[0]].tolist(), 'pin1From': how,
            'pads': order, 'box': _box(P[order]), 'axis': 'x' if axis == 0 else 'y', 'rowSpacingMm': c['spacing'],
            'outline': c['outline'], 'chamfer': [round(v, 3) for v in shares.values()]}


def _pin_order(a, b, pin1):
    """Pins 1 to N: along pin 1's row from pin 1, then back along the other."""
    row, end = pin1
    first, second = (a, b) if row == 'a' else (b, a)
    first = list(first) if end == 0 else list(reversed(first))
    second = list(reversed(second)) if end == 0 else list(second)
    return first + second


def apply_marks(fps, pads, marks, to_board):
    """The references, and any pin 1, read from the print by hand: each mark
    is a point in I1's pixels inside its footprint."""
    P = np.array([[p['x'], p['y']] for p in pads], float)
    out = []
    for m in marks:
        q = to_board([m['at']])[0]
        hits = [f for f in fps if f['box'][0] <= q[0] <= f['box'][2] and f['box'][1] <= q[1] <= f['box'][3]]
        hits.sort(key=lambda f: (f['kind'] != 'dip', (f['box'][2] - f['box'][0]) * (f['box'][3] - f['box'][1])))
        if not hits:
            out.append({'ref': m.get('ref'), 'found': False})
            continue
        f = hits[0]
        if m.get('ref') is not None:
            f['ref'] = m['ref']
        if 'kind' in m:                  # grouping got it wrong: the print says what it is
            f['kind'], f['pin1From'] = m['kind'], 'not marked: ' + m['reason']
            out.append({'ref': m.get('ref'), 'found': True, 'kind': f['kind'], 'pins': f['pins']})
            continue
        if 'pin1' in m:
            p1 = to_board([m['pin1']])[0]
            k = int(np.argmin([math.hypot(*(P[i] - p1)) for i in f['pads']]))
            if f['kind'] == 'dip':
                n = f['pins'] // 2
                a, b = f['pads'][:n], f['pads'][n:][::-1]
                corner = {f['pads'][k]}
                key = ('a', 0) if a[0] in corner else ('a', 1) if a[-1] in corner else ('b', 0) if b[0] in corner else ('b', 1) if b[-1] in corner else None
                if key is None:
                    out.append({'ref': m.get('ref'), 'found': True, 'pin1': 'not a corner pad'})
                    continue
                f['pads'] = _pin_order(a, b, key)
            f['pin1'] = P[f['pads'][0]].tolist()
            f['pin1From'] = 'marked by hand'
        out.append({'ref': m.get('ref'), 'found': True, 'kind': f['kind'], 'pins': f['pins'], 'pin1From': f['pin1From']})
    return out


# --- the mounting holes -----------------------------------------------------------------

def fit_top_rim(rgb, centre_px, radius_px, sx, sy):
    """A hole's top rim, in pixels: centre (x, y), diameter d (in pixels along
    the mean scale), and how well the points fit. None when too few rays
    find it."""
    from scipy.ndimage import map_coordinates
    L, A, B = common.oklab(rgb)
    C = np.hypot(A, B)
    c = np.asarray(centre_px, float)
    t = np.linspace(0, 2 * math.pi, RIM_RAYS, endpoint=False)
    r = np.arange(RIM_RANGE[0] * radius_px, RIM_RANGE[1] * radius_px, 0.25)
    xs = c[0] + np.outer(np.cos(t), r)
    ys = c[1] + np.outer(np.sin(t), r)
    v = map_coordinates(L, [ys.ravel(), xs.ravel()], order=1, mode='nearest').reshape(xs.shape)
    ch = map_coordinates(C, [ys.ravel(), xs.ravel()], order=1, mode='nearest').reshape(xs.shape)
    lid = float(np.median(v[:, r < 0.6 * radius_px]))
    dark = float(np.percentile(v[:, r > 1.3 * radius_px], 10))
    thr = (lid + dark) / 2
    pts = []
    for i in range(RIM_RAYS):
        out = np.nonzero((v[i] < thr) | (ch[i] > RIM_STOP_CHROMA))[0]
        if not len(out) or out[0] == 0:
            continue
        j = out[0]
        if v[i, j] < thr:
            f = (thr - v[i, j - 1]) / (v[i, j] - v[i, j - 1])
        else:
            f = (RIM_STOP_CHROMA - ch[i, j - 1]) / max(ch[i, j] - ch[i, j - 1], 1e-9)
        rr = r[j - 1] + float(np.clip(f, 0, 1)) * (r[j] - r[j - 1])
        pts.append((rr * math.cos(t[i]) / sx, rr * math.sin(t[i]) / sy))
    if len(pts) < 0.5 * RIM_RAYS:
        return None
    pts = np.array(pts)
    ok = np.ones(len(pts), bool)
    for _ in range(4):
        cx, cy, rad = board_frame._circle(pts[ok])
        res = np.hypot(pts[:, 0] - cx, pts[:, 1] - cy) - rad
        mad = 1.4826 * np.median(np.abs(res[ok]))
        ok = np.abs(res) <= max(3 * mad, 0.05)
    s = math.sqrt(sx * sy)
    return {'x': float(c[0] + cx * sx), 'y': float(c[1] + cy * sy), 'd': float(2 * rad * s), 'dMm': float(2 * rad),
            'rays': int(len(pts)), 'kept': int(ok.sum()), 'rmsMm': float(np.sqrt((res[ok] ** 2).mean())),
            'lidL': lid, 'darkL': dark}


def mounting_holes(rgb, frame, holes):
    out = []
    for h in holes:
        c = frame.to_px([(h['x'], h['y'])])[0]
        rim = fit_top_rim(rgb, c, h['d'] / 2 * math.sqrt(frame.sx * frame.sy), frame.sx, frame.sy)
        if rim is None:
            out.append({'task2': h, 'refused': 'too few rays found the rim'})
            continue
        q = frame.to_board([(rim['x'], rim['y'])])[0]
        out.append({'x': float(q[0]), 'y': float(q[1]), 'd': rim['dMm'], 'rimRmsMm': rim['rmsMm'], 'rays': rim['rays'], 'kept': rim['kept'],
                    'task2': {'x': h['x'], 'y': h['y'], 'd': h['d']},
                    'shiftMm': [float(q[0] - h['x']), float(q[1] - h['y'])], 'shiftLenMm': float(math.hypot(q[0] - h['x'], q[1] - h['y']))})
    return out


# --- I1 and I2 ---------------------------------------------------------------------------

def marks():
    with open(common.DATA / 'marks.json', encoding='utf8') as f:
        return json.load(f)


def load_frame():
    with open(common.DATA / 'frame.json', encoding='utf8') as f:
        fj = json.load(f)
    return fj, board_frame.Frame(fj['pxPerMm']['x'], fj['pxPerMm']['y'], math.radians(fj['rotationDeg']), fj['origin'])


def judge(stats, holes):
    """The plan's solder row, on the figures without exclusion: median and
    90th percentile judged, the largest recorded, at least 200 holes."""
    ok = stats['median'] <= PASS['median'] and stats['p90'] <= PASS['p90'] and holes >= MIN_HOLES
    stop = stats['median'] > STOP['median'] or stats['p90'] > STOP['p90']
    return 'STOP' if stop else ('pass' if ok else 'between pass and stop')


def overlays(i1, reg, drills, pads, fps, holes, frame):
    """Pictures to look at, in out/ (git-ignored)."""
    import cv2
    from PIL import Image, ImageDraw
    common.OUT.mkdir(exist_ok=True)
    k = 3
    small = np.ascontiguousarray(i1[::k, ::k] // 2)
    excluded = {e['index'] for e in reg['excluded']}
    for n, p in enumerate(reg['pairs']):
        x, y = frame.to_px([p['top']])[0] / k
        e = p['heldOutMm']
        c = (0, 255, 0) if e <= 0.15 else (255, 255, 0) if e <= 0.30 else (255, 0, 0)
        cv2.circle(small, (int(round(x)), int(round(y))), 4, c, -1)
        if n in excluded:
            cv2.circle(small, (int(round(x)), int(round(y))), 7, (255, 0, 255), 1)
    Image.fromarray(small).save(common.OUT / 'register-held-out.jpg', quality=90)
    # footprints on I1 at half size
    k = 2
    img = Image.fromarray(np.ascontiguousarray(i1[::k, ::k]))
    d = ImageDraw.Draw(img)
    for p in pads:
        x, y = frame.to_px([(p['x'], p['y'])])[0] / k
        col = {'both': (0, 255, 255), 'top': (255, 128, 0), 'bottom': (255, 0, 255)}[p['face']]
        d.ellipse([x - 3, y - 3, x + 3, y + 3], outline=col)
    for f in fps:
        q = frame.to_px([(f['box'][0], f['box'][1]), (f['box'][2], f['box'][3])]) / k
        col = {'dip': (255, 0, 0), 'connector': (0, 128, 255), 'sip': (0, 200, 0)}.get(f['kind'], (255, 255, 255))
        d.rectangle([q[0][0], q[0][1], q[1][0], q[1][1]], outline=col, width=2)
        x, y = frame.to_px([f['pin1']])[0] / k
        p1col = (255, 0, 255) if f['pin1From'] in (None,) else (255, 255, 0)
        d.ellipse([x - 7, y - 7, x + 7, y + 7], outline=p1col, width=3)
        label = f"{f['ref'] or ''} {f['pins']}{'' if f['pin1From'] else ' ?'}"
        d.text((q[0][0] + 3, q[0][1] + 3), label, fill=(255, 255, 255))
    img.save(common.OUT / 'footprints.jpg', quality=88)
    # the mounting holes: task 2's circle in blue, the top rim in magenta
    tiles = []
    for h in holes:
        if 'x' not in h:
            continue
        c = frame.to_px([(h['task2']['x'], h['task2']['y'])])[0]
        half = int(1.2 * h['d'] * frame.sx)
        x0, y0 = int(c[0]) - half, int(c[1]) - half
        z = 5
        t = Image.fromarray(np.ascontiguousarray(i1[y0:y0 + 2 * half, x0:x0 + 2 * half])).resize((2 * half * z, 2 * half * z), Image.NEAREST)
        dd = ImageDraw.Draw(t)
        for hx, hy, hd, col in ((h['task2']['x'], h['task2']['y'], h['task2']['d'], (0, 128, 255)), (h['x'], h['y'], h['d'], (255, 0, 255))):
            X, Y = (frame.to_px([(hx, hy)])[0] - (x0, y0)) * z
            R = hd / 2 * frame.sx * z
            dd.ellipse([X - R, Y - R, X + R, Y + R], outline=col, width=2)
            dd.line([X - 10, Y, X + 10, Y], fill=col, width=2)
            dd.line([X, Y - 10, X, Y + 10], fill=col, width=2)
        tiles.append(t)
    if tiles:
        W = max(t.size[0] for t in tiles)
        sheet = Image.new('RGB', (W * 3, W * ((len(tiles) + 2) // 3)), 'white')
        for i, t in enumerate(tiles):
            sheet.paste(t, ((i % 3) * W, (i // 3) * W))
        sheet.save(common.OUT / 'holes-top-rim.jpg', quality=90)


def main():
    fj, frame = load_frame()
    m = marks()
    i1 = common.read_rgb('I1')
    i2m = common.read_rgb('I2', mirror=True)
    reg = register_scans(i1, i2m, m['solderSeeds']['holes'], frame)
    verdict = judge(reg['heldOutMm'], reg['holes'])
    drills, pads, pad_counts, spare = pads_and_drills(reg['faces'])
    fps = group_footprints(pads, frame.to_px, i1, spare)
    marked = apply_marks(fps, pads, m.get('footprints', {}).get('marks', []), frame.to_board)
    holes = mounting_holes(i1, frame, fj['holes'])
    shifts = [h['shiftLenMm'] for h in holes if 'shiftLenMm' in h]
    excluded = [{'hole': e['index'], 'at': reg['pairs'][e['index']]['top'], 'why': e['why']} for e in reg['excluded']]
    dips = [f for f in fps if f['kind'] == 'dip']
    result = {
        'about': ('Task 3 of the BBC Micro models plan: the solder side (I2) registered to the component side (I1) on every hole found, '
                  'and the pads, drills and footprints, in the board frame of frame.json (millimetres from the left rear corner, x to the right, '
                  'y towards the front). Written by board_register.py; every figure here is a measurement made on the date of the journal entry that quotes it.'),
        'solder': {
            'model': reg['model'],
            'holes': reg['holes'],
            'heldOutMm': reg['heldOutMm'],
            'verdict': verdict,
            'thresholds': {'pass': PASS, 'stop': STOP, 'minHoles': MIN_HOLES, 'max': 'recorded, not judged'},
            'fits': reg['fits'],
            'choice': f'the lower held-out median; within {TIE_MM} mm a tie, which the affine (the simpler) wins',
            'heldOutByKind': reg['heldOutByKind'],
            'found': reg['found'],
            'seedFitPx': reg['seedFitPx'],
            'gateMm': reg['gateMm'], 'blockMm': reg['blockMm'],
            'outliers': {
                'rule': ('A matched hole is left out of the scoring only if its blob fails the roundness test on either face: the major to minor '
                         'axis ratio of its second moments over 1.25, or its area outside 0.6 to 1.6 times the median area of the matched holes\' '
                         'blobs on that face (the plan, fixed on 4 October 2026 before this task; no residual is used). The verdict is judged without it.'),
                'excluded': excluded,
                'count': len(excluded),
                'heldOutMmWithExclusion': reg['heldOutMmWithExclusion'],
            },
            'matched': {
                'what': 'Every matched hole, in the order the excluded list counts them: its blob on each face (axis ratio from second moments, area in I1 or I2 pixels) and its held-out error under the chosen model',
                'columns': ['topAxis', 'topArea', 'bottomAxis', 'bottomArea', 'heldOutMm'],
                'rows': [[p['blobs'][0]['axis'], p['blobs'][0]['area'], p['blobs'][1]['axis'], p['blobs'][1]['area'], p['heldOutMm']]
                         for p in reg['pairs']],
            },
        },
        'drills': [{k: d[k] for k in ('x', 'y', 'd', 'filled', 'spreadMm')} for d in drills],
        'drillSpreadMm': common.stats([d['spreadMm'] for d in drills]),
        'pads': [{k: p[k] for k in ('x', 'y', 'w', 'h', 'shape', 'face', 'drill', 'source')} for p in pads],
        'padCounts': dict(pad_counts, total=len(pads), both=sum(p['face'] == 'both' for p in pads),
                          bySource={s: sum(p['source'] == s for p in pads) for s in ('drill', 'pair, not hole-shaped', 'one face', 'completion', 'inferred')},
                          inFootprints=len({i for f in fps for i in f['pads']})),
        'footprints': [{k: f.get(k) for k in ('ref', 'kind', 'pins', 'pin1', 'pin1From', 'pads', 'box', 'axis', 'rowSpacingMm', 'outline',
                                              'padsNotFound', 'padsInferred')} for f in fps],
        'footprintCounts': {'dip': len(dips), 'connector': sum(f['kind'] == 'connector' for f in fps), 'sip': sum(f['kind'] == 'sip' for f in fps),
                            'dipPin1': {str(k): sum(f['pin1From'] == k for f in dips) for k in ('print', 'square pad', 'marked by hand', None)},
                            'withRef': sum(f['ref'] is not None for f in fps)},
        'marks': marked,
        'holes': holes,
        'holeShiftMm': {'mean': float(np.mean(shifts)), 'max': float(np.max(shifts)), 'n': len(shifts),
                        'what': 'Each mounting hole fitted on its top rim (the outer edge of the grey crescent), against task 2\'s circle, which stopped at the lid seen through the hole'},
        'firstRun': {
            'what': ('The first run on 4 October 2026, kept as measured. Its ring finder also took dark specks inside tinned pads, and letters of the print, '
                     'for open holes: 1167 rings on I1 and 945 on I2, where the run below finds 399 and 703. A speck beside its own pad made one hole two, '
                     '0.25 to 0.5 mm apart, which broke DIP rows; that is how it was found, on the footprint overlay, not from the residuals. The rules that '
                     'reject them (a ring is green and clear of every solder blob) were set from the first run\'s rings, looked at, and the registration was run again.'),
            'holes': 2521, 'model': 'cubic', 'heldOutMm': {'median': 0.116, 'p90': 0.292, 'max': 1.009},
            'affineHeldOutMm': {'median': 0.126, 'p90': 0.320, 'max': 0.980},
            'excluded': 1146, 'heldOutMmWithExclusion': {'median': 0.100, 'p90': 0.244, 'max': 1.009},
            'verdict': 'pass',
        },
        'slots': 'The front edge\'s slots are placed by frame.json\'s outline; their walls are weakly fitted (task 2) and are not used here as geometry',
    }
    hs = reg['heldOutMm']
    wx = reg['heldOutMmWithExclusion']
    print(f"solder side: {reg['model']}, {reg['holes']} holes; held out median {hs['median']:.3f}, p90 {hs['p90']:.3f}, max {hs['max']:.3f} mm: {verdict}")
    print(f"  with the roundness rule ({len(excluded)} excluded): median {wx['median']:.3f}, p90 {wx['p90']:.3f}, max {wx['max']:.3f} mm")
    print("  fits: " + '; '.join(f"{k} {v['heldOutMm']['median']:.3f}/{v['heldOutMm']['p90']:.3f}/{v['heldOutMm']['max']:.3f}" for k, v in reg['fits'].items()))
    print(f"drills {len(drills)}, pads {len(pads)}, footprints {len(fps)} ({len(dips)} DIPs); holes shifted mean {np.mean(shifts):.3f}, max {np.max(shifts):.3f} mm")
    # Three places is a micron, far under anything judged; one record a line.
    common.write_data('registration.json', result, places=3, rows_per_line=True)
    overlays(i1, reg, drills, pads, fps, holes, frame)
    return 3 if verdict == 'STOP' else 0


if __name__ == '__main__':
    sys.exit(main())
