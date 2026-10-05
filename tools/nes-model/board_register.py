"""Task 3: the solder side registered to the component side, and the board's
pads, drills and footprints, from the bare NES-CPU-10 scans of both faces
(I1-front, the component side, and I1-back, the solder side). Adapted from
tools/bbc-micro-model/board_register.py.

    NES_MODEL_INPUTS=<folder> python board_register.py

Everything is in the board frame of data/frame.json (task 2): millimetres
from the board's top left corner as I1-front lies, x to the right, y down the
scan towards the edge fingers. Its x and y scales are used as they are; none
is fitted again here.

1. The holes on each face. I1-back is flipped left to right first, so that it
   lies as the component side does. On these scans a hole shows three ways,
   and each is found and kept apart by its kind:
   ring: a pad's tinned ring round an open hole. Task 0's finder, unchanged
     (spike.py's drill_centres): the round blobs of common.pad_mask the size
     of a pad, each fitted on its hole's top rim by common.rim_centre, and
     refused when the rim's centre is over 0.5 mm from the blob's. The top
     rim is where the scanned face, on the glass, meets the hole. One
     refusal is added: a rim the light does not rise across, from inside it
     into the ring, by 0.05 (OKLab L) has no open hole under it; the blob
     goes to the next finder.
   open: any other open hole: the small rings of the vias, and the large
     pads (the RF modulator's), which task 0's finder's size and its 11 pixel
     opening leave out. Blobs of the same light, low-colour mask opened only
     5 pixels, round and 40 to 2500 pixels, not within 1.0 mm of a ring
     hole; each fitted on its top rim as common.rim_centre fits it, with the
     hole's radius between 0.3 and 0.75 of the blob's, a ray counting only
     where the light rises by 0.05 into the ring (rim_fit).
   dome: a hole filled with solder, which shows no rim: a blob as above
     that rim_fit refuses, its centre the circle fitted by consensus to its
     outer edge, where the light falls fastest going out (dome_fit). The
     consensus leaves out the tail solder drags off a dome.
   What none of them finds is not a hole here: a hole in a tinned plane on
   the solder side shows only as a dark speck, and a pad with a ragged blob
   of solder on both faces fits neither circle.

2. The registration, as task 0's spike, over every hole found. An affine
   from the four holes marked by hand on both faces (data/marks.json
   "solderSeeds") seeds the match; holes are paired when each is the other's
   nearest within 1.0 mm, the affine is refitted on the pairs and the match
   redone, three times. Then an affine and a cubic (a homography, then a
   cubic correction) from I1-back flipped to the board frame are each scored
   on a chequerboard of 20 mm blocks, each colour held out of the fit and
   measured under the fit to the other. The model is the one with the lower
   held-out median; within 0.005 mm of each other is a tie, and the simpler
   (the affine) wins it. All task 0's.

   The outlier rule, the plan's, fixed before any measurement and using no
   residual: a matched hole is left out of the scoring only if its pad's blob
   fails the roundness test on either face: the major to minor axis ratio of
   its second moments over 1.25, or its area outside 0.6 to 1.6 times the
   median of the matched pads' blobs on that face. A ring hole's blob is
   task 0's (common.pad_mask's); an open hole's or a dome's is the blob it
   was found from. The figures are reported with and without it; the verdict
   is judged without it, against the plan's solder row.

3. Pads and drills. A matched pair is a drill, at the mean of where the two
   faces put the hole, when the two are within 0.4 mm of each other, so that
   each face's pad is within 0.2 mm of the drill (the plan's check); a pair
   further apart is recorded, not drilled. Each drill has a pad on each face
   at that face's own centre for the hole, sized and shaped from that face's
   blob. Its diameter is the mean of its open rims, and a hole filled with
   solder on both faces has none. A ring hole (task 0's finder) found on one
   face only is a pad on that face with no drill; an open hole or a dome
   found on one face only is not a pad (on I1-front the second finder also
   takes some printed figures, an 0 or a 6, and scratches in the tinned
   planes, for open holes: seen on its overlays on 5 October 2026, before
   any registration was run), but it may complete a footprint's row. On
   I1-front nothing within 0.5 mm of a mounting hole's rim (frame.json) is
   taken for a hole: the lid seen through one is a round light blob.

4. The edge fingers: tinned strips along the bottom edge, darker than the
   pads, found on each face as tall strips 1.2 to 4.5 mm wide in one row
   with even gaps between them (on the 2.50 mm pitch; the end ones are
   wider); the 36 of each face are one footprint, P1, of 72.

5. Footprints. Holes 2.54 mm apart in a line are chained into rows. Two rows
   facing each other hole for hole at 7.62 or 15.24 mm are a DIP; where a
   row could pair more than one way the print decides (a DIP's outline runs
   1.2 to 2.2 mm inside both its rows), and a hand mark may say which
   (U1 and U4 have a 300 mil row between their 600 mil rows, for the PAL
   board's RAMs; the narrow pairing is kept too, as the alternate). Pin 1:
   the print's notch at one end of the outline, else a square pad, else a
   hand mark. Rows left over on the 2.54 or 2.0 mm pitch are connectors (two
   facing rows) or single rows ("other"); what grouping cannot tell (the RF
   modulator, the power and reset header, the crystals) is grouped by hand,
   pin by pin. Every reference is read from the print by hand
   (data/marks.json "footprints"), because nothing here reads text.

Writes data/registration.json and overlays to look at in out/ (git-ignored:
the scans state no licence). Exits 3 when a judged figure crosses one of the
plan's STOP thresholds.
"""
import itertools
import json
import math
import sys

import numpy as np

import board_frame
import common

PITCH = 2.54
FINGER_PITCH = 2.50
STATED_PX_PER_MM = 300 / 25.4        # task 0's gates and windows are in these; the scale is frame.json's

# The plan's solder row (Global Constraints), set before the measurements: the
# median and 90th percentile judged, the largest recorded, at least 150 holes.
PASS = {'median': 0.20, 'p90': 0.40}
STOP = {'median': 0.30, 'p90': 0.60}
MIN_HOLES = 150
# Task 0's match and folds, kept.
MATCH_GATE_MM = 1.0
BLOCK_MM = 20.0
MATCH_ROUNDS = 3
TIE_MM = 0.005
HOLE_FROM_BLOB_MM = 0.5              # task 0: a rim further than this from its blob is another feature
# The plan's outlier rule, fixed before any measurement.
ROUND_MAX_AXIS = 1.25
ROUND_AREA = (0.6, 1.6)

# The second finder: open holes and domes that task 0's leaves out. Set on 5
# October 2026 from the scans before any registration was run: a via's ring
# on I1-back is about 0.9 mm across (about 90 pixels when filled), which an
# 11 pixel opening removes; a dome of solder is 1.0 to 1.3 mm across; the RF
# modulator's rings are about 3 mm (about 1000 pixels).
SMALL_OPEN_PX = 5
BLOB_AREA_PX = (40, 2500)
BLOB_MAX_ASPECT = 1.6                # its box's long side to its short
BLOB_MIN_FILL = 0.6                  # its area over the disc on its box's long side
TAKEN_MM = 1.0                       # a blob this near a ring hole is that pad
MOUNTING_CLEAR_MM = 0.5              # past a mounting hole's rim (frame.json), nothing is a hole here
HOLE_R_FRAC = (0.3, 0.75)            # a hole's radius over its blob's: task 0's pads 0.57, the vias about 0.5
HOLE_MIN_R_PX = 2.0
DOME_R_FRAC = (0.6, 1.4)
RIM_TOL_PX = common.RIM_TOL_PX
# A ray of rim_fit counts only where the light rises by at least this into
# the ring (OKLab L): an open hole's wall is about 0.2 to 0.3 under its ring
# (task 0's probe: ring median 0.52, the lacquer and the wall's shadow about
# 0.2); a dome, lit brightest in its middle, only falls going out. Set on 5
# October 2026 from the made-up domes, which the rise alone let through.
RIM_MIN_RISE = 0.05

# Pads and drills.
DRILL_AGREE_MM = 0.4                 # the two faces' centres within this make a drill (each pad within 0.2 of it)
SQUARE_MIN = 0.90                    # a square pad fills its smallest rectangle (a disc fills pi / 4)
SQUARE_OTHERS_MAX = 0.86

# The edge fingers: tinned strips, darker than the pads' rings. Read off both
# scans on 5 October 2026 after the first run found 3 and 9 of them with the
# pads' floor (OKLab L 0.34): across the fingers, the columns' median light is
# 0.36 to 0.44 on a finger and 0.17 to 0.22 in a gap, with darker streaks
# down a finger (to about 0.26). The light is averaged FINGER_BLUR_PX down
# the scan first (the gaps run down it) and the floor is half way. The
# scanner's lid below the board's edge is lighter still (task 2's floor for
# it, board_frame.LID_MIN_L) and is left out, or every finger joins it.
FINGER_W_MM = (1.2, 4.5)
FINGER_MIN_L = 0.29
FINGER_BLUR_PX = 15
FINGER_OPEN_MM = 4.0                 # opened with a box this tall: the strips are about 8 mm
FINGER_MIN_H_MM = 5.0
FINGER_MAX_H_MM = 16.0               # the strips with the bare edge below them read about 11 mm
# A strip is 1.2 to 4.5 mm wide: the others about 2.0 mm, the end ones wider,
# widened outwards (task 0: 3.0 mm between their edges; the mask, which takes
# in the bare edge's bevel beside them, reads them 3.8 to 4.0). Neighbouring
# strips' edges are 0.25 to 1.0 mm apart (the gaps read 0.5 to 0.6 mm); a
# piece nearer than 0.25 mm to the next is one strip broken by a streak.
FINGER_GAP_MM = (0.25, 1.0)
FINGER_JOIN_MM = 0.25

# Rows and footprints.
PITCH_TOL_MM = 0.35                  # a neighbour in a row: one pitch along, within this
ACROSS_TOL_MM = 0.50                 # and within this across
FACING_TOL_MM = 0.50
SPACINGS_MM = (7.62, 15.24)
SPACING_TOL_MM = 0.50
DIP_ROW_PADS = (7, 20)               # 14 to 40 pins
ROW_MAX_STEP = 3                     # a DIP's row may lack two pads in a row and stay one row
COMPLETE_MM = 0.6                    # a spare hole this near where a missing pin goes is its pad
ROW_PITCHES_MM = (2.54, 2.0)         # rows left over: the 2.0 mm ones are the controller headers
ROW_MIN_PADS = 5
CONNECTOR_MAX_SPACING_MM = 10.0
# The print: white lines. Read off I1-front on 5 October 2026 across U6's
# outline: the line's light (OKLab L) 0.77 to 0.79 at its middle, colour
# about 0.03; the pads' rings L 0.52 at their median (task 0's probe), the
# lacquer under 0.25. The floor sits between the rings' median and the print.
PRINT_MIN_L, PRINT_MAX_C = 0.65, 0.10
PRINT_CLEAR_MM = 1.0                 # the print is not looked for this near a hole (a pad's radius is about 0.85)
OUTLINE_OFFSETS_MM = (1.2, 2.2)      # a DIP's outline runs this far inside each row (U6 1.8, U3 1.7 and 1.95)
OUTLINE_MIN = 0.5
# The notch: a half circle about 1.1 mm across its radius, centred on the
# outline's end about 1.3 mm past the end pins, read off U6, U3 and U7 on
# I1-front. Looked for over these places and sizes; its share of print on
# the arc, and on its middle third, must reach NOTCH_MIN and beat the
# other end's by NOTCH_MARGIN.
NOTCH_OFFSETS_MM = (0.9, 1.8)
NOTCH_RADII_MM = (0.8, 1.4)
NOTCH_MIN = 0.6
NOTCH_MARGIN = 0.3


# --- blobs and their shapes -------------------------------------------------------------

def axis_ratio(xs, ys):
    """Major to minor axis of the ellipse from a blob's second moments."""
    p = np.c_[np.asarray(xs, float), np.asarray(ys, float)]
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


def pad_blobs(rgb):
    """Task 0's pad blobs (spike.pad_blobs, unchanged): common.find_pads'
    blobs with their centroid, area in pixels and second moments' axis ratio."""
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


def small_mask(rgb):
    """common.pad_mask's light, low-colour mask, its specks filled, opened
    only SMALL_OPEN_PX: a via's ring and a dome stay."""
    import cv2
    L, A, B = common.oklab(rgb)
    m = ((np.hypot(A, B) < common.PAD_MAX_CHROMA) & (L > common.PAD_MIN_L)).astype(np.uint8)
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (5, 5)))
    n, lab, st, _ = cv2.connectedComponentsWithStats((1 - m).astype(np.uint8), 8)
    edge = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])).tolist())
    holes = [i for i in range(1, n) if st[i, 4] < 400 and i not in edge]
    m = m | np.isin(lab, holes).astype(np.uint8)
    return cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (SMALL_OPEN_PX, SMALL_OPEN_PX)))


# --- the holes ----------------------------------------------------------------------------

def rise(L, x, y, r):
    """How much the light rises from inside a fitted rim into the ring: the
    median over the rays of the brightest point within 3.5 pixels outside
    the rim less the darkest between 0.3 r and the rim."""
    rs = np.arange(0, r + 3.5 + 1e-9, 0.25)
    _, prof, _ = _rays(L, x, y, rs)
    inner, outer = (rs >= 0.3 * r) & (rs <= r), rs >= r
    return float(np.median(prof[:, outer].max(1) - prof[:, inner].min(1)))


def ring_holes(rgb, L):
    """Task 0's drill centres (spike.drill_centres): every pad blob's hole on
    its top rim, refused when the rim is refused or is over
    HOLE_FROM_BLOB_MM from the blob's centroid. One refusal is added: where
    the light does not rise from inside the rim into the ring by
    RIM_MIN_RISE (rise), there is no open hole, and the blob is left to the
    second finder (a dome of solder, on the made-up board, was fitted a rim)."""
    out = []
    for b in pad_blobs(rgb):
        r, _ = common.rim_centre(L, b['x'], b['y'])
        if r is None or math.hypot(r['x'] - b['x'], r['y'] - b['y']) > HOLE_FROM_BLOB_MM * STATED_PX_PER_MM:
            continue
        rad = r['diameterMm'] * STATED_PX_PER_MM / 2
        if rise(L, r['x'], r['y'], rad) < RIM_MIN_RISE:
            continue                         # no hole under the rim: a dome, left to the second finder
        out.append({'kind': 'ring', 'px': (r['x'], r['y']), 'blob': {'area': b['areaPx'], 'axis': b['axisRatio']},
                    'centroid': (b['x'], b['y']), 'rPx': r['diameterMm'] * STATED_PX_PER_MM / 2, 'rays': r['rays']})
    return out


def _consensus(P, r_lo, r_hi, tol=RIM_TOL_PX):
    """common._consensus with the radius's range given: of the circles through
    three of the points (every second one), the one most others lie within
    tol of, refined by least squares on those."""
    n = len(P)
    T = np.array(list(itertools.combinations(range(0, n, 2), 3)))
    if not len(T):
        return None, np.zeros(n, bool)
    a, b, c = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
    d = 2 * (a[:, 0] * (b[:, 1] - c[:, 1]) + b[:, 0] * (c[:, 1] - a[:, 1]) + c[:, 0] * (a[:, 1] - b[:, 1]))
    ok = np.abs(d) > 1e-9
    d = np.where(ok, d, 1.0)
    sa, sb, sc = (a ** 2).sum(1), (b ** 2).sum(1), (c ** 2).sum(1)
    ux = (sa * (b[:, 1] - c[:, 1]) + sb * (c[:, 1] - a[:, 1]) + sc * (a[:, 1] - b[:, 1])) / d
    uy = (sa * (c[:, 0] - b[:, 0]) + sb * (a[:, 0] - c[:, 0]) + sc * (b[:, 0] - a[:, 0])) / d
    r = np.hypot(a[:, 0] - ux, a[:, 1] - uy)
    ok &= (r >= r_lo) & (r <= r_hi)
    if not ok.any():
        return None, np.zeros(n, bool)
    ux, uy, r = ux[ok], uy[ok], r[ok]
    dist = np.abs(np.hypot(P[None, :, 0] - ux[:, None], P[None, :, 1] - uy[:, None]) - r[:, None])
    i = int(np.argmax((dist <= tol).sum(1)))
    inl = dist[i] <= tol
    circ = np.array([ux[i], uy[i], r[i]])
    for _ in range(3):
        if inl.sum() < 3:
            return None, inl
        circ = common._circle(P[inl])
        inl = np.abs(np.hypot(*(P - circ[:2]).T) - circ[2]) <= tol
    return circ, inl


def _rays(L, cx, cy, rs, n=common.RIM_RAYS):
    from scipy.ndimage import gaussian_filter1d, map_coordinates
    th = np.linspace(0, 2 * np.pi, n, endpoint=False)
    X = cx + np.outer(np.cos(th), rs)
    Y = cy + np.outer(np.sin(th), rs)
    prof = gaussian_filter1d(map_coordinates(L, [Y, X], order=1, mode='nearest'), 2.0, axis=1)
    return th, prof, np.gradient(prof, rs, axis=1)


def _sub(g, i, rs, step):
    a, b, c = g[i - 1], g[i], g[i + 1]
    den = a - 2 * b + c
    return rs[i] + (0.5 * (a - c) / den if den < 0 else 0.0) * step


def rim_fit(L, x, y, r_lo, r_hi, rounds=3):
    """A hole's top rim, as common.rim_centre finds it, for a hole whose
    radius is r_lo to r_hi pixels: along each ray, the steepest rise in light
    on the way out to the ring's brightest point (within the larger of 3.5
    pixels and 0.4 r_hi inside it), where the light rises by at least
    RIM_MIN_RISE; a circle by consensus; three times from the new centre. With r_lo 5.0 and r_hi 7.5 this is common.rim_centre.
    Returns (x, y, r) in pixels and the rays that agree, or None when fewer
    than half agree."""
    step = 0.25
    rs = np.arange(0, r_hi + 3.5 + 1e-9, step)
    near = np.nonzero((rs >= r_lo - 0.5) & (rs <= r_hi + 2.0))[0]
    inside = max(3.5, 0.4 * r_hi)
    cx, cy = float(x), float(y)
    keep = None
    for _ in range(rounds):
        th, prof, g = _rays(L, cx, cy, rs)
        pts = []
        for k in range(len(th)):
            ip = near[int(np.argmax(prof[k, near]))]
            w = np.nonzero((rs >= rs[ip] - inside) & (rs <= rs[ip]))[0]
            i = w[int(np.argmax(g[k, w]))]
            if g[k, i] <= 0 or i == 0 or i >= len(rs) - 1 or prof[k, ip] - prof[k, w].min() < RIM_MIN_RISE:
                continue
            r = _sub(g[k], i, rs, step)
            pts.append((cx + r * math.cos(th[k]), cy + r * math.sin(th[k])))
        if len(pts) < 3:
            return None
        P = np.array(pts)
        circ, keep = _consensus(P, r_lo, r_hi)
        if circ is None:
            return None
        cx, cy, rad = (float(v) for v in circ)
    if keep.sum() < common.RIM_RAYS / 2:
        return None
    return cx, cy, rad, int(keep.sum())


def dome_fit(L, x, y, R, rounds=2):
    """A dome of solder's outline: along each ray, the steepest fall in light
    going out, between 0.3 and 1.7 R; a circle of radius DOME_R_FRAC R by
    consensus. None when fewer than half the rays agree."""
    step = 0.25
    rs = np.arange(0, 1.7 * R + 1e-9, step)
    win = np.nonzero(rs >= 0.3 * R)[0]
    cx, cy = float(x), float(y)
    keep = None
    for _ in range(rounds):
        th, prof, g = _rays(L, cx, cy, rs)
        pts = []
        for k in range(len(th)):
            i = win[int(np.argmin(g[k, win]))]
            if g[k, i] >= 0 or i >= len(rs) - 1:
                continue
            r = _sub(-g[k], i, rs, step)
            pts.append((cx + r * math.cos(th[k]), cy + r * math.sin(th[k])))
        if len(pts) < 3:
            return None
        P = np.array(pts)
        circ, keep = _consensus(P, DOME_R_FRAC[0] * R, DOME_R_FRAC[1] * R)
        if circ is None:
            return None
        cx, cy, rad = (float(v) for v in circ)
    if keep.sum() < common.RIM_RAYS / 2:
        return None
    return cx, cy, rad, int(keep.sum())


def other_holes(rgb, L, taken_px, mask=None):
    """Open holes and domes that task 0's finder leaves out (the module's
    docstring, 1). taken_px: the ring holes' centres. Returns the holes and
    how many blobs were refused."""
    import cv2
    m = small_mask(rgb) if mask is None else mask
    n, lab, st, cen = cv2.connectedComponentsWithStats(m, 8)
    from scipy.spatial import cKDTree
    tree = cKDTree(np.asarray(taken_px, float).reshape(-1, 2)) if len(taken_px) else None
    out, refused = [], 0
    for i in range(1, n):
        x, y, w, h, a = st[i]
        if not BLOB_AREA_PX[0] <= a <= BLOB_AREA_PX[1] or max(w, h) > BLOB_MAX_ASPECT * min(w, h):
            continue
        if a / (math.pi * (max(w, h) / 2) ** 2) < BLOB_MIN_FILL:
            continue
        c = (float(cen[i][0]), float(cen[i][1]))
        if tree is not None and tree.query(c)[0] <= TAKEN_MM * STATED_PX_PER_MM:
            continue
        R = math.sqrt(a / math.pi)
        ys, xs = np.nonzero(lab[y:y + h, x:x + w] == i)
        blob = {'area': int(a), 'axis': axis_ratio(xs, ys)}
        r = rim_fit(L, c[0], c[1], max(HOLE_MIN_R_PX, HOLE_R_FRAC[0] * R), HOLE_R_FRAC[1] * R)
        if r is not None and math.hypot(r[0] - c[0], r[1] - c[1]) <= 0.5 * R:
            out.append({'kind': 'open', 'px': (r[0], r[1]), 'blob': blob, 'centroid': c, 'rPx': r[2], 'rays': r[3]})
            continue
        d = dome_fit(L, c[0], c[1], R)
        if d is not None and math.hypot(d[0] - c[0], d[1] - c[1]) <= 0.5 * R:
            out.append({'kind': 'dome', 'px': (d[0], d[1]), 'blob': blob, 'centroid': c, 'rPx': None, 'rays': d[3]})
            continue
        refused += 1
    return out, refused


def holes_of_one_face(rgb, skip_px=()):
    """Every hole found on one face (the module's docstring, 1), each with
    its pad's shape from the small mask: w and h in pixels along its
    smallest rectangle, and its squareness. skip_px: circles (x, y, r) in
    pixels where nothing is a hole here (the mounting holes, which task 2
    measures: the lid seen through one is a round light blob)."""
    import cv2
    L = common.oklab(rgb)[0]
    rings = ring_holes(rgb, L)
    m = small_mask(rgb)
    others, refused = other_holes(rgb, L, [h['px'] for h in rings], m)
    hs = [h for h in rings + others if not any(math.hypot(h['px'][0] - x, h['px'][1] - y) <= r for x, y, r in skip_px)]
    skipped = len(rings) + len(others) - len(hs)
    rings = [h for h in hs if h['kind'] == 'ring']
    others = [h for h in hs if h['kind'] != 'ring']
    n, lab = cv2.connectedComponents(m, connectivity=8)
    for h in hs:
        x, y = (int(round(v)) for v in h['px'])
        j = lab[min(max(y, 0), lab.shape[0] - 1), min(max(x, 0), lab.shape[1] - 1)]
        h['shape'] = _shape(lab == j) if j else {'wPx': 0.0, 'hPx': 0.0, 'square': 0.0, 'axis': float('inf')}
    return hs, {'ring': len(rings), 'open': sum(h['kind'] == 'open' for h in others),
                'dome': sum(h['kind'] == 'dome' for h in others), 'refused': refused, 'inMountingHoles': skipped}


def _shape(mask):
    import cv2
    ys, xs = np.nonzero(mask)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    sub = mask[y0:y1 + 1, x0:x1 + 1]
    (_, _), (w, h), _ = cv2.minAreaRect(np.c_[xs, ys].astype(np.float32))
    return {'wPx': float(max(w, h) + 1), 'hPx': float(min(w, h) + 1), 'square': squareness(sub), 'axis': axis_ratio(xs, ys)}


# --- the registration ---------------------------------------------------------------------

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
    when the solder side was flipped before it was marked, as it must be."""
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


def judge(stats, holes):
    """The plan's solder row on the figures without exclusion: 'pass',
    'between pass and stop' or 'STOP'. The largest is recorded, not judged."""
    ok = stats['median'] <= PASS['median'] and stats['p90'] <= PASS['p90'] and holes >= MIN_HOLES
    stop = stats['median'] > STOP['median'] or stats['p90'] > STOP['p90']
    return 'STOP' if stop else ('pass' if ok else 'between pass and stop')


def register_scans(front, back_m, seeds, frame, mounting=()):
    """I1-back flipped registered to the board frame on every hole found on
    both faces (the module's docstring, 2). seeds: [{"I1-front": [x, y],
    "I1-back mirrored": [x, y]}] in pixels; mounting: frame.json's holes
    ({"x", "y", "d"} in board mm), where nothing on I1-front is taken for a
    hole here."""
    skip = [(*frame.to_px([(h['x'], h['y'])])[0], (h['d'] / 2 + MOUNTING_CLEAR_MM) * frame.sx) for h in mounting]
    faces = {}
    found = {}
    for name, img, sk in (('front', front, skip), ('back', back_m, ())):
        faces[name], found[name] = holes_of_one_face(img, sk)
    h1 = np.array([h['px'] for h in faces['front']], float).reshape(-1, 2)
    h2 = np.array([h['px'] for h in faces['back']], float).reshape(-1, 2)
    s1 = np.array([s['I1-front'] for s in seeds], float)
    s2 = np.array([s['I1-back mirrored'] for s in seeds], float)
    if seed_handedness(s2, s1) <= 0:
        raise ValueError('the seeds need a mirror: is I1-back flipped left to right before marking?')
    seed = common.fit_held_out(s2, s1, 'affine', 'leave-one-out')
    params = seed['params']
    gate_px = MATCH_GATE_MM * STATED_PX_PER_MM
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
    for h in faces['front']:
        h['mm'] = frame.to_board([h['px']])[0].tolist()
    for h in faces['back']:
        h['mm'] = common.transform(model, chosen['params'], [h['px']])[0].tolist()
    out_pairs = []
    for k, (i, j) in enumerate(pairs):
        t, b = faces['front'][i], faces['back'][j]
        out_pairs.append({'front': i, 'back': j, 'top': t['mm'], 'bottom': b['mm'], 'heldOutMm': chosen['heldOutEach'][k][1],
                          'kinds': [t['kind'], b['kind']], 'blobs': [t['blob'], b['blob']]})
    excluded = roundness_exclusions([p['blobs'][0] for p in out_pairs], [p['blobs'][1] for p in out_pairs])
    keep = np.ones(len(out_pairs), bool)
    keep[[e['index'] for e in excluded]] = False
    held = np.array([p['heldOutMm'] for p in out_pairs])
    by_kind = {}
    for kinds in sorted({tuple(p['kinds']) for p in out_pairs}):
        sel = np.array([tuple(p['kinds']) == kinds for p in out_pairs])
        by_kind[' / '.join(kinds)] = dict(common.stats(held[sel]), n=int(sel.sum()))
    return {
        'found': found,
        'seedFitPx': seed['fitMm'],
        'gateMm': MATCH_GATE_MM, 'blockMm': BLOCK_MM, 'tieMm': TIE_MM,
        'holes': len(out_pairs),
        'model': model,
        'params': chosen['params'],
        'heldOutMm': chosen['heldOutMm'],
        'fits': {k: {'fitMm': v['fitMm'], 'heldOutMm': v['heldOutMm']} for k, v in fits.items()},
        'heldOutByKind': by_kind,
        'worst': chosen['worst'],
        'excluded': excluded,
        'heldOutMmWithExclusion': common.stats(held[keep]) if keep.any() else None,
        'pairs': out_pairs,
        'faces': faces,
    }


# --- pads and drills ----------------------------------------------------------------------

def _pad(h, x, y, face, drill, source, px_per_mm):
    s = h['shape']
    if s['axis'] > 1.4:
        shape = 'oval'
    elif s['square'] >= SQUARE_MIN and s['axis'] <= 1.15:
        shape = 'square'
    else:
        shape = 'round'
    return {'x': float(x), 'y': float(y), 'w': s['wPx'] / px_per_mm, 'h': s['hPx'] / px_per_mm, 'shape': shape, 'face': face,
            'drill': drill, 'square': s['square'], 'kind': h['kind'], 'source': source}


def pads_and_drills(reg, px_per_mm):
    """Drills and pads from the matched holes (the module's docstring, 3).
    Returns drills, pads, the pairs not drilled, and the spare holes (on one
    face, unmatched) for completing a footprint's rows."""
    front, back = reg['faces']['front'], reg['faces']['back']
    drills, pads, apart = [], [], []
    used = {'front': set(), 'back': set()}
    for k, p in enumerate(reg['pairs']):
        t, b = front[p['front']], back[p['back']]
        spread = math.hypot(t['mm'][0] - b['mm'][0], t['mm'][1] - b['mm'][1])
        if spread > DRILL_AGREE_MM:
            apart.append({'pair': k, 'top': t['mm'], 'bottom': b['mm'], 'spreadMm': spread, 'kinds': p['kinds']})
            continue
        used['front'].add(p['front'])
        used['back'].add(p['back'])
        ds = [h['rPx'] * 2 / px_per_mm for h in (t, b) if h['rPx'] is not None]
        drills.append({'x': (t['mm'][0] + b['mm'][0]) / 2, 'y': (t['mm'][1] + b['mm'][1]) / 2, 'd': float(np.mean(ds)) if ds else None,
                       'filled': not ds, 'spreadMm': spread, 'kinds': p['kinds'], 'pair': k})
        n = len(drills) - 1
        pads.append(_pad(t, t['mm'][0], t['mm'][1], 'top', n, 'drill', px_per_mm))
        pads.append(_pad(b, b['mm'][0], b['mm'][1], 'bottom', n, 'drill', px_per_mm))
    spare = []
    single = {'top': 0, 'bottom': 0}
    for name, face, hs in (('front', 'top', front), ('back', 'bottom', back)):
        for i, h in enumerate(hs):
            if i in used[name]:
                continue
            if h['kind'] == 'ring':
                pads.append(_pad(h, h['mm'][0], h['mm'][1], face, None, 'one face', px_per_mm))
                single[face] += 1
            else:
                spare.append(dict(h, face=face))
    return drills, pads, apart, spare, single


# --- the edge fingers -----------------------------------------------------------------------

def fingers_of_one_face(rgb, to_board, px_per_mm):
    """The edge fingers on one face: tall tinned strips in one row on the
    2.50 mm pitch. Each {"x", "y", "w", "h"} in board millimetres (to_board
    takes the face's pixels there), left to right."""
    import cv2
    L, A, B = common.oklab(rgb)
    Lv = cv2.blur(L.astype(np.float32), (1, FINGER_BLUR_PX))
    m = ((np.hypot(A, B) < common.PAD_MAX_CHROMA) & (Lv > FINGER_MIN_L) & (L < board_frame.LID_MIN_L)).astype(np.uint8)
    # what is not tall goes: the bare edge below the fingers and the via
    # rings above them join every finger to the next across the gaps
    tall = int(round(FINGER_OPEN_MM * px_per_mm))
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_RECT, (3, tall)))
    n, lab, st, cen = cv2.connectedComponentsWithStats(m, 8)
    # pieces FINGER_MIN_H_MM to FINGER_MAX_H_MM tall and no wider than a
    # strip (the tinned planes are as light); pieces closer than
    # FINGER_JOIN_MM side by side are one strip broken by a streak
    pieces = sorted((int(st[i][0]), int(st[i][0] + st[i][2] - 1), int(st[i][1]), int(st[i][1] + st[i][3] - 1))
                    for i in range(1, n) if FINGER_MIN_H_MM * px_per_mm <= st[i][3] <= FINGER_MAX_H_MM * px_per_mm
                    and st[i][2] <= FINGER_W_MM[1] * px_per_mm)
    strips = []
    for x0, x1, y0, y1 in pieces:
        for k, (a0, a1, b0, b1) in enumerate(strips):
            overlap = min(y1, b1) - max(y0, b0)
            if overlap > 0.5 * min(y1 - y0, b1 - b0) and x0 - a1 - 1 < FINGER_JOIN_MM * px_per_mm and x1 >= a0:
                strips[k] = (a0, max(a1, x1), min(b0, y0), max(b1, y1))
                break
        else:
            strips.append((x0, x1, y0, y1))
    cands = []
    for x0, x1, y0, y1 in strips:
        w = (x1 - x0 + 1) / px_per_mm
        if not FINGER_W_MM[0] <= w <= FINGER_W_MM[1]:
            continue
        xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
        c = to_board([(xm, ym)])[0]
        left, right = to_board([(x0 - 0.5, ym)])[0], to_board([(x1 + 0.5, ym)])[0]
        top, bot = to_board([(xm, y0 - 0.5)])[0], to_board([(xm, y1 + 0.5)])[0]
        cands.append({'x': float(c[0]), 'y': float(c[1]), 'w': float(abs(right[0] - left[0])), 'h': float(abs(bot[1] - top[1])),
                      'left': float(min(left[0], right[0])), 'right': float(max(left[0], right[0]))})
    cands.sort(key=lambda f: f['x'])
    # the longest run of strips side by side, each gap between neighbours
    # FINGER_GAP_MM wide (the end fingers are wider than the others, so the
    # gaps, not the centres, are even), each overlapping the last down the
    # scan by half (their tops are uneven where the lacquer meets them)
    runs = []
    for f in cands:
        for run in runs:
            g = run[-1]
            if FINGER_GAP_MM[0] <= f['left'] - g['right'] <= FINGER_GAP_MM[1] and \
                    min(f['y'] + f['h'] / 2, g['y'] + g['h'] / 2) - max(f['y'] - f['h'] / 2, g['y'] - g['h'] / 2) > 0.5 * min(f['h'], g['h']):
                run.append(f)
                break
        else:
            runs.append([f])
    best = max(runs, key=len) if runs else []
    for f in best:
        del f['left'], f['right']
    return best


# --- footprints -------------------------------------------------------------------------------

def _chains(P, axis, pitch=PITCH, max_step=1):
    """Rows of points on `pitch` along `axis` (0: x, 1: y): lists of indexes
    in order. A step of up to max_step pitches is a link."""
    from scipy.spatial import cKDTree
    if not len(P):
        return []
    tree = cKDTree(P)
    nxt, prv = {}, {}
    for i, p in enumerate(P):
        best = None
        for j in tree.query_ball_point(p, max_step * pitch + PITCH_TOL_MM):
            d = P[j] - p
            along, across = d[axis], d[1 - axis]
            k = round(along / pitch)
            if 1 <= k <= max_step and abs(along - k * pitch) <= PITCH_TOL_MM and abs(across) <= ACROSS_TOL_MM:
                err = (k, abs(along - k * pitch) + abs(across))
                if best is None or err < best[0]:
                    best = (err, j)
        if best is not None:
            nxt[i] = best[1]
    for i, j in sorted(nxt.items(), key=lambda kv: abs(P[kv[1]][axis] - P[kv[0]][axis])):
        if j in prv:
            del nxt[i]
        else:
            prv[j] = i
    out = []
    for i in range(len(P)):
        if i in prv or i not in nxt:
            continue
        c = [i]
        while c[-1] in nxt:
            c.append(nxt[c[-1]])
        out.append(c)
    return out


def print_mask(rgb, holes_px, r_px):
    """The white print, and where it is not looked for: a disc of r_px round
    each hole found (a pad's solder is as light and as grey). Returns the
    two as float images (print less the discs, and the discs). Not less
    common.pad_mask's blobs: its closing and filling turn the outline's
    notch, a half circle on a line, into a solid blob."""
    import cv2
    L, A, B = common.oklab(rgb)
    clear = np.zeros(L.shape, np.uint8)
    for x, y in holes_px:
        cv2.circle(clear, (int(round(x)), int(round(y))), int(round(r_px)), 1, -1)
    pm = (L > PRINT_MIN_L) & (np.hypot(A, B) < PRINT_MAX_C) & (clear == 0)
    return pm.astype(np.float32), clear.astype(np.float32)


def _share(pm, to_px, pts):
    """The print's share of the points that are not cleared round a hole;
    0 when under half of them can be seen."""
    q = to_px(pts)
    on = _sample(pm[0], q) > 0.5
    seen = _sample(pm[1], q) < 0.5
    return float(on[seen].mean()) if seen.sum() >= 0.5 * len(seen) else 0.0


def _sample(mask, pts_px):
    from scipy.ndimage import map_coordinates
    pts_px = np.asarray(pts_px, float).reshape(-1, 2)
    return map_coordinates(mask.astype(np.float32, copy=False), [pts_px[:, 1], pts_px[:, 0]], order=1, mode='constant')


def outline_score(pm, to_px, row, other, axis):
    """The print's share along the inside of `row` (towards `other`),
    OUTLINE_OFFSETS_MM in, on the best line (pm: print_mask's pair)."""
    side = math.copysign(1, other[0][1 - axis] - row[0][1 - axis])
    a, b = row[0][axis], row[-1][axis]
    us = np.arange(a, b + 1e-9, 0.25)
    base = np.interp(us, [a, b], [row[0][1 - axis], row[-1][1 - axis]])
    best = 0.0
    for off in np.arange(OUTLINE_OFFSETS_MM[0], OUTLINE_OFFSETS_MM[1] + 1e-9, 0.1):
        v = base + side * off
        pts = np.c_[us, v] if axis == 0 else np.c_[v, us]
        best = max(best, _share(pm, to_px, pts))
    return best


def notch_score(pm, to_px, end_a, end_b, inward, axis):
    """The print's share on a half circle bulging into the footprint from
    the outline's end past end_a and end_b (the two rows' end pins); inward
    is +1 or -1 along the axis. The best over NOTCH_OFFSETS_MM and
    NOTCH_RADII_MM."""
    mid = (np.asarray(end_a, float) + np.asarray(end_b, float)) / 2
    th = np.radians(np.arange(-75, 76, 5))
    apex = np.abs(th) <= np.radians(30)
    best = 0.0
    for off in np.arange(NOTCH_OFFSETS_MM[0], NOTCH_OFFSETS_MM[1] + 1e-9, 0.1):
        for r in np.arange(NOTCH_RADII_MM[0], NOTCH_RADII_MM[1] + 1e-9, 0.1):
            along = mid[axis] - inward * off + inward * r * np.cos(th)
            across = mid[1 - axis] + r * np.sin(th)
            pts = np.c_[along, across] if axis == 0 else np.c_[across, along]
            # the arc's middle third must be on print too: a straight end can
            # lie along the arc's flanks, never its apex
            best = max(best, min(_share(pm, to_px, pts), _share(pm, to_px, pts[apex])))
    return best


def _slots(P, chain, u0, axis):
    out = {}
    for i in chain:
        s = round((P[i][axis] - u0) / PITCH)
        e = abs(P[i][axis] - u0 - s * PITCH)
        if e <= FACING_TOL_MM and (s not in out or e < abs(P[out[s]][axis] - u0 - s * PITCH)):
            out[s] = i
    return out


def _facing_runs(P, a, b, axis):
    """Each run of pitch slots along two rows where at least one has a pad in
    each slot, trimmed so that both rows have one at both ends: pairs of
    lists, None where that row's pad was not found."""
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
    out = []
    for r in runs:
        while r and not (r[0] in sa and r[0] in sb):
            r = r[1:]
        while r and not (r[-1] in sa and r[-1] in sb):
            r = r[:-1]
        if r:
            out.append(([sa.get(s) for s in r], [sb.get(s) for s in r]))
    return out


def _row_line(P, row, axis):
    q = np.array([P[i] for i in row if i is not None])
    if len(q) < 2:
        return lambda u: float(q[0][1 - axis])
    c1, c0 = np.polyfit(q[:, axis], q[:, 1 - axis], 1)
    return lambda u: float(c0 + c1 * u)


def _box(Q, pad=1.0):
    Q = np.asarray(Q, float)
    return [float(Q[:, 0].min() - pad), float(Q[:, 1].min() - pad), float(Q[:, 0].max() + pad), float(Q[:, 1].max() + pad)]


def _pin_order(a, b, pin1):
    """Pins 1 to N: along pin 1's row from pin 1, then back along the other."""
    row, end = pin1
    first, second = (a, b) if row == 'a' else (b, a)
    first = list(first) if end == 0 else list(reversed(first))
    second = list(reversed(second)) if end == 0 else list(second)
    return first + second


class Sites:
    """The places a footprint's pins are looked for: one per drill (its top
    pad, at the drill) and one per pad found on one face only, in board
    millimetres; spare holes complete a row, and a pin still missing is
    inferred. Pads are appended to `pads` for both."""

    def __init__(self, pads, spare, px_per_mm):
        self.pads, self.px_per_mm = pads, px_per_mm
        self.index, pts = [], []
        seen = set()
        for i, p in enumerate(pads):
            if p['drill'] is not None:
                if p['drill'] in seen or p['face'] != 'top':
                    continue
                seen.add(p['drill'])
            elif p['source'] not in ('one face',):
                continue
            self.index.append(i)
            pts.append((p['x'], p['y']))
        self.P = np.array(pts, float).reshape(-1, 2)
        self.spare = [dict(s) for s in spare]

    def at(self, k):
        return self.pads[self.index[k]]

    def complete(self, rows, axis):
        """Each pin None in rows: the nearest spare hole within COMPLETE_MM
        of where its row puts it, else a pad inferred there. Returns the rows
        as pad indexes."""
        from scipy.spatial import cKDTree
        S = np.array([s['mm'] for s in self.spare], float).reshape(-1, 2)
        tree = cKDTree(S) if len(S) else None
        out = []
        for row in rows:
            line = _row_line(self.P, row, axis)
            known = [(k, self.P[i][axis]) for k, i in enumerate(row) if i is not None]
            ks, us = zip(*known)
            pitch, u0 = np.polyfit(ks, us, 1) if len(known) > 1 else (PITCH, us[0])
            got = []
            for k, i in enumerate(row):
                if i is not None:
                    got.append(self.index[i])
                    continue
                u = u0 + pitch * k
                q = (u, line(u)) if axis == 0 else (line(u), u)
                done = False
                if tree is not None:
                    d, j = tree.query(q)
                    if d <= COMPLETE_MM and not self.spare[j].get('taken'):
                        s = self.spare[j]
                        s['taken'] = True
                        self.pads.append(_pad(s, s['mm'][0], s['mm'][1], s['face'], None, 'completion', self.px_per_mm))
                        done = True
                if not done:
                    w = float(np.median([self.pads[self.index[r]]['w'] for r in row if r is not None]))
                    self.pads.append({'x': float(q[0]), 'y': float(q[1]), 'w': w, 'h': w, 'shape': 'round', 'face': 'both',
                                      'drill': None, 'square': 0.0, 'kind': 'inferred', 'source': 'inferred'})
                got.append(len(self.pads) - 1)
            out.append(got)
        return out


def dip_candidates(sites, pm, to_px):
    P = sites.P
    out = []
    for axis in (0, 1):
        cs = [c for c in _chains(P, axis, max_step=ROW_MAX_STEP) if len(c) >= DIP_ROW_PADS[0] - 2]
        for ia, ib in itertools.combinations(range(len(cs)), 2):
            a, b = cs[ia], cs[ib]
            across = abs(np.median(P[b][:, 1 - axis]) - np.median(P[a][:, 1 - axis]))
            spacing = min(SPACINGS_MM, key=lambda s: abs(s - across))
            if abs(spacing - across) > SPACING_TOL_MM:
                continue
            if np.median(P[b][:, 1 - axis]) < np.median(P[a][:, 1 - axis]):
                a, b = b, a                      # a: the row with the smaller across coordinate
            for ra, rb in _facing_runs(P, a, b, axis):
                if not DIP_ROW_PADS[0] <= len(ra) <= DIP_ROW_PADS[1]:
                    continue
                fa, fb = [i for i in ra if i is not None], [i for i in rb if i is not None]
                if min(len(fa), len(fb)) < len(ra) / 2:
                    continue
                score = min(outline_score(pm, to_px, P[fa], P[fb], axis), outline_score(pm, to_px, P[fb], P[fa], axis))
                Q = P[fa + fb]
                out.append({'axis': axis, 'a': ra, 'b': rb, 'spacing': spacing, 'outline': score, 'box': _box(Q, 0.0)})
    return out


def _inside(box, q):
    return box[0] - 0.5 <= q[0] <= box[2] + 0.5 and box[1] - 0.5 <= q[1] <= box[3] + 0.5


def group_footprints(sites, pm, to_px, prefer=()):
    """DIPs and rows from the sites (the module's docstring, 5). prefer: hand
    marks [{"at": board mm, "rowSpacingMm", "alternate"}]: the DIP there with
    that spacing is taken first; an alternate is taken as well, sharing pads
    with the DIP already there."""
    P = sites.P
    cands = dip_candidates(sites, pm, to_px)
    cands.sort(key=lambda c: (-round(c['outline'], 2), -len(c['a']), c['spacing']))
    chosen, used = [], set()
    for m in prefer:
        if m.get('alternate'):
            continue
        hits = [c for c in cands if c['spacing'] == m['rowSpacingMm'] and _inside(c['box'], m['at'])]
        if hits:
            c = hits[0]
            found = {i for i in c['a'] + c['b'] if i is not None}
            if not used & found:
                used |= found
                chosen.append(dict(c, preferred=True))
    for c in cands:
        found = {i for i in c['a'] + c['b'] if i is not None}
        if used & found or c['outline'] < OUTLINE_MIN:
            continue
        used |= found
        chosen.append(c)
    for m in prefer:
        if not m.get('alternate'):
            continue
        hits = [c for c in cands if c['spacing'] == m['rowSpacingMm'] and _inside(c['box'], m['at'])]
        if hits:
            c = _span_shared_row(dict(hits[0], alternate=True), [x for x in chosen if not x.get('alternate')], P)
            chosen.append(c)
            used |= {i for i in c['a'] + c['b'] if i is not None}
    fps = []
    for c in chosen:
        missing = sum(i is None for i in c['a'] + c['b'])
        n_before = len(sites.pads)
        a, b = sites.complete([c['a'], c['b']], c['axis'])
        fp = _dip(c, a, b, sites, pm, to_px)
        fp['padsNotFound'] = missing
        fp['padsInferred'] = sum(sites.pads[i]['source'] == 'inferred' for i in range(n_before, len(sites.pads)))
        fp['alternate'] = bool(c.get('alternate'))
        fps.append(fp)
    # rows left over: connectors (two facing rows) and single rows
    rest = []
    for pitch in ROW_PITCHES_MM:
        for axis in (0, 1):
            for ch in _chains(P, axis, pitch=pitch):
                if len(ch) >= ROW_MIN_PADS and not used & set(ch):
                    rest.append((axis, pitch, ch))
    rest.sort(key=lambda r: -len(r[2]))
    for k, (axis, pitch, ch) in enumerate(rest):
        if used & set(ch):
            continue
        group = [ch]
        for axis2, pitch2, ch2 in rest[k + 1:]:
            if axis2 != axis or pitch2 != pitch or used & set(ch2) or len(ch2) != len(ch):
                continue
            across = abs(np.median(P[ch2][:, 1 - axis]) - np.median(P[ch][:, 1 - axis]))
            if across <= CONNECTOR_MAX_SPACING_MM and abs(P[ch2][0][axis] - P[ch][0][axis]) <= FACING_TOL_MM:
                group.append(ch2)
                break
        idx = [i for g in group for i in g]
        used |= set(idx)
        pads = [sites.index[i] for i in idx]
        fps.append({'ref': None, 'kind': 'connector' if len(group) == 2 else 'other', 'pins': len(idx),
                    'pin1': P[ch[0]].tolist(), 'pin1From': None, 'pads': pads, 'box': _box(P[idx]),
                    'axis': 'x' if axis == 0 else 'y', 'pitchMm': pitch, 'rows': len(group)})
    return fps


def _span_shared_row(c, mains, P):
    """An alternate DIP whose row is shared with a DIP already taken (U1's
    and U4's narrow footprints share their lower row with the wide one) runs
    that row's whole length: its own other row is looked for slot by slot
    along it (a site within FACING_TOL_MM of where the row's line puts it),
    None where none is, which completion then fills. Its rows as found were
    cut back to where both ends were found."""
    axis = c['axis']
    for m in mains:
        for side in ('a', 'b'):
            mine = {i for i in c[side] if i is not None}
            for mside in ('a', 'b'):
                if m['axis'] != axis or not mine & {i for i in m[mside] if i is not None}:
                    continue
                full = m[mside]
                other = 'b' if side == 'a' else 'a'
                line = _row_line(P, c[other], axis)
                known = [(k, P[i][axis]) for k, i in enumerate(full) if i is not None]
                ks, us = zip(*known)
                pitch, u0 = np.polyfit(ks, us, 1) if len(known) > 1 else (PITCH, us[0])
                row = []
                for k in range(len(full)):
                    u = u0 + pitch * k
                    q = np.array((u, line(u)) if axis == 0 else (line(u), u))
                    d = np.hypot(*(P - q).T)
                    j = int(np.argmin(d))
                    row.append(j if d[j] <= FACING_TOL_MM else None)
                out = dict(c)
                out[side], out[other] = list(full), row
                return out
    return c


def _dip(c, a, b, sites, pm, to_px):
    """A DIP footprint: its pin 1 from the print's notch, else a square pad,
    else none (a hand mark says), and its pads pin by pin."""
    pads = sites.pads
    axis, n = c['axis'], len(a)
    Q = {k: np.array([pads[i]['x'], pads[i]['y']]) for k, i in (('a0', a[0]), ('a1', a[-1]), ('b0', b[0]), ('b1', b[-1]))}
    ends = {0: notch_score(pm, to_px, Q['a0'], Q['b0'], +1, axis), 1: notch_score(pm, to_px, Q['a1'], Q['b1'], -1, axis)}
    centre = (Q['a0'] + Q['a1'] + Q['b0'] + Q['b1']) / 4
    pin1, how = None, None
    e = max(ends, key=ends.get)
    if ends[e] >= NOTCH_MIN and ends[e] - ends[1 - e] >= NOTCH_MARGIN:
        # Seen from the component side, pins run anticlockwise: pin 1 is the
        # corner at the notch's end on the side where cross(notch, corner) < 0
        # (x right, y down: a notch to the left puts pin 1 at the lower left).
        nvec = np.zeros(2)
        nvec[axis] = -1.0 if e == 0 else 1.0
        for row in ('a', 'b'):
            v = Q[f'{row}{e}'] - centre
            if nvec[0] * v[1] - nvec[1] * v[0] < 0:
                pin1, how = (row, e), 'print'
    if pin1 is None:
        squares = {(row, end): pads[Q_i]['square'] for (row, end), Q_i in ((('a', 0), a[0]), (('a', 1), a[-1]), (('b', 0), b[0]), (('b', 1), b[-1]))}
        sq = [k for k, v in squares.items() if v >= SQUARE_MIN]
        if len(sq) == 1 and all(v <= SQUARE_OTHERS_MAX for k, v in squares.items() if k != sq[0]):
            pin1, how = sq[0], 'square pad'
    order = _pin_order(a, b, pin1 or ('a', 0))
    p1 = pads[order[0]]
    return {'ref': None, 'kind': 'dip', 'pins': 2 * n, 'pin1': [p1['x'], p1['y']], 'pin1From': how, 'pads': order,
            'box': _box([[pads[i]['x'], pads[i]['y']] for i in order]), 'axis': 'x' if axis == 0 else 'y',
            'rowSpacingMm': c['spacing'], 'outline': c['outline'], 'notch': [ends[0], ends[1]], 'rows': (a, b)}


def edge_footprint(pads, fingers):
    """P1: the edge fingers of both faces, one footprint; appends their pads."""
    idx = []
    for face, fs in (('top', fingers['top']), ('bottom', fingers['bottom'])):
        for f in fs:
            pads.append({'x': f['x'], 'y': f['y'], 'w': f['w'], 'h': f['h'], 'shape': 'rect', 'face': face, 'drill': None,
                         'square': 0.0, 'kind': 'finger', 'source': 'finger'})
            idx.append(len(pads) - 1)
    Q = [[pads[i]['x'], pads[i]['y']] for i in idx]
    first = pads[idx[0]] if idx else None
    return {'ref': None, 'kind': 'edge', 'pins': len(idx), 'pin1': [first['x'], first['y']] if first else None, 'pin1From': None,
            'pads': idx, 'box': _box(Q, 0.5) if Q else None, 'axis': 'x', 'pitchMm': FINGER_PITCH,
            'perFace': {'top': len(fingers['top']), 'bottom': len(fingers['bottom'])}}


def hand_footprint(m, sites, to_board):
    """A footprint grouped by hand: "pins", a point on each pin's pad in
    order, or "rows", each a row's first and last pin and how many pins it
    has, the pins between spaced evenly. Each pin takes the nearest site
    within COMPLETE_MM; a pin with none is inferred at its place, and said."""
    if 'rows' in m:
        pts = []
        for r in m['rows']:
            a, b = to_board([r['first'], r['last']])
            pts += [a + (b - a) * k / (r['pins'] - 1) for k in range(r['pins'])]
        pts = np.array(pts)
    else:
        pts = to_board(m['pins'])
    pads, missing = [], []
    for k, q in enumerate(pts):
        d = np.hypot(*(sites.P - q).T) if len(sites.P) else np.array([np.inf])
        j = int(np.argmin(d))
        if d[j] <= COMPLETE_MM:
            pads.append(sites.index[j])
            continue
        missing.append(k + 1)
        w = float(np.median([sites.pads[i]['w'] for i in pads])) if pads else 1.6
        sites.pads.append({'x': float(q[0]), 'y': float(q[1]), 'w': w, 'h': w, 'shape': 'round', 'face': 'both', 'drill': None,
                           'square': 0.0, 'kind': 'inferred', 'source': 'inferred'})
        pads.append(len(sites.pads) - 1)
    Q = [[sites.pads[i]['x'], sites.pads[i]['y']] for i in pads]
    return {'ref': m['ref'], 'kind': m['kind'], 'pins': len(pads), 'pin1': Q[0], 'pin1From': 'marked by hand', 'pads': pads,
            'box': _box(Q), 'pinsNotFound': missing, 'byHand': True}


def apply_marks(fps, marks, sites, to_board):
    """The references read from the print by hand, and pin 1 where marked:
    each mark a point in I1-front's pixels inside its footprint; a mark with
    "pins" is a footprint grouped by hand. Returns what each mark did."""
    pads = sites.pads
    out = []
    for m in marks:
        if 'pins' in m or 'rows' in m:
            f = hand_footprint(m, sites, to_board)
            # the grouping's own guess at the same holes, a row or two, gives way
            mine = set(f['pads'])
            dropped = [g for g in fps if g['ref'] is None and g['kind'] in ('other', 'connector') and set(g['pads']) & mine]
            for g in dropped:
                fps.remove(g)
            fps.append(f)
            out.append({'ref': m['ref'], 'found': True, 'kind': m['kind'], 'pins': f['pins'], 'byHand': True,
                        'pinsNotFound': f['pinsNotFound'], 'replaces': [[g['kind'], g['pins']] for g in dropped]})
            continue
        q = to_board([m['at']])[0]
        hits = [f for f in fps if f['box'] and _inside(f['box'], q) and f['ref'] is None]
        if 'rowSpacingMm' in m:
            hits = [f for f in hits if f.get('rowSpacingMm') == m['rowSpacingMm'] and f.get('alternate', False) == bool(m.get('alternate'))]
        hits.sort(key=lambda f: (f['kind'] != 'dip', (f['box'][2] - f['box'][0]) * (f['box'][3] - f['box'][1])))
        if not hits:
            out.append({'ref': m['ref'], 'found': False})
            continue
        f = hits[0]
        f['ref'] = m['ref']
        if 'kind' in m:
            f['kind'] = m['kind']
        if 'pin1' in m:
            p1 = to_board([m['pin1']])[0]
            k = int(np.argmin([math.hypot(pads[i]['x'] - p1[0], pads[i]['y'] - p1[1]) for i in f['pads']]))
            if f['kind'] == 'dip':
                a, b = f['rows']
                key = next((kk for kk, i in ((('a', 0), a[0]), (('a', 1), a[-1]), (('b', 0), b[0]), (('b', 1), b[-1])) if i == f['pads'][k]), None)
                if key is None:
                    out.append({'ref': m['ref'], 'found': True, 'pin1': 'not a corner pad'})
                    continue
                # None where the grouping found no pin 1 of its own to agree with
                agrees = None if f['pin1From'] is None else f['pads'][0] == f['pads'][k]
                f['pads'] = _pin_order(a, b, key)
                f['pin1FromGrouping'] = f['pin1From']
                f['pin1AgreesWithGrouping'] = agrees
            elif f['kind'] == 'edge':
                # the fingers of the marked one's face from it, then the other face's
                face = pads[f['pads'][k]]['face']
                mine = sorted((i for i in f['pads'] if pads[i]['face'] == face), key=lambda i: abs(pads[i]['x'] - pads[f['pads'][k]]['x']))
                rest = [i for i in f['pads'] if pads[i]['face'] != face]
                f['pads'] = mine + sorted(rest, key=lambda i: abs(pads[i]['x'] - pads[mine[0]]['x']))
            f['pin1'] = [pads[f['pads'][0]]['x'], pads[f['pads'][0]]['y']]
            f['pin1From'] = 'marked by hand'
        out.append({'ref': m['ref'], 'found': True, 'kind': f['kind'], 'pins': f['pins'], 'pin1From': f['pin1From']})
    return out


# --- everything, from the two scans -----------------------------------------------------------

def build(front, back_m, marks, frame, mounting=()):
    """The registration, pads, drills and footprints from I1-front and
    I1-back flipped, given the hand marks and task 2's frame and mounting
    holes."""
    px = math.sqrt(frame.sx * frame.sy)
    reg = register_scans(front, back_m, marks['solderSeeds']['holes'], frame, mounting)
    drills, pads, apart, spare, single = pads_and_drills(reg, px)
    fingers = {'top': fingers_of_one_face(front, frame.to_board, px),
               'bottom': fingers_of_one_face(back_m, lambda q: common.transform(reg['model'], reg['params'], q), px)}
    sites = Sites(pads, spare, px)
    pm = print_mask(front, [h['px'] for h in reg['faces']['front']], PRINT_CLEAR_MM * px)
    fmarks = marks.get('footprints', {}).get('marks', [])
    prefer = [{'at': frame.to_board([m['at']])[0], 'rowSpacingMm': m['rowSpacingMm'], 'alternate': bool(m.get('alternate'))}
              for m in fmarks if 'rowSpacingMm' in m]
    fps = group_footprints(sites, pm, frame.to_px, prefer)
    fps.append(edge_footprint(pads, fingers))
    marked = apply_marks(fps, fmarks, sites, frame.to_board)
    for f in fps:
        f.pop('rows', None)
    return {'reg': reg, 'drills': drills, 'pads': pads, 'apart': apart, 'single': single, 'fingers': fingers,
            'footprints': fps, 'marks': marked}


# --- I1-front and I1-back ---------------------------------------------------------------------

def marks():
    with open(common.DATA / 'marks.json', encoding='utf8') as f:
        return json.load(f)


def load_frame():
    with open(common.DATA / 'frame.json', encoding='utf8') as f:
        fj = json.load(f)
    return fj, board_frame.Frame.read(fj)


def overlays(front, back_m, out, frame):
    """Pictures to look at, in out/ (git-ignored): every hole found on each
    face by kind, the matched holes coloured by their held-out error, and the
    pads and footprints on I1-front."""
    from PIL import Image, ImageDraw
    common.OUT.mkdir(exist_ok=True)
    reg = out['reg']
    colour = {'ring': (0, 255, 255), 'open': (255, 160, 0), 'dome': (255, 0, 255)}
    matched = {'front': {p['front'] for p in reg['pairs']}, 'back': {p['back'] for p in reg['pairs']}}
    for name, img in (('front', front), ('back', back_m)):
        im = Image.fromarray(img)
        d = ImageDraw.Draw(im)
        for i, h in enumerate(reg['faces'][name]):
            x, y = h['px']
            c = colour[h['kind']]
            r = 9 if i in matched[name] else 5
            d.ellipse([x - r, y - r, x + r, y + r], outline=c, width=1 if i in matched[name] else 2)
            d.line([x - 2, y, x + 2, y], fill=c)
            d.line([x, y - 2, x, y + 2], fill=c)
        im.save(common.OUT / f'register-holes-{name}.png')
    im = Image.fromarray(np.ascontiguousarray(front // 2))
    d = ImageDraw.Draw(im)
    excluded = {e['index'] for e in reg['excluded']}
    for n, p in enumerate(reg['pairs']):
        x, y = frame.to_px([p['top']])[0]
        e = p['heldOutMm']
        c = (0, 255, 0) if e <= 0.15 else (255, 255, 0) if e <= 0.30 else (255, 0, 0)
        d.ellipse([x - 5, y - 5, x + 5, y + 5], fill=c)
        if n in excluded:
            d.ellipse([x - 9, y - 9, x + 9, y + 9], outline=(255, 0, 255))
    im.save(common.OUT / 'register-held-out.png')
    im = Image.fromarray(front)
    d = ImageDraw.Draw(im)
    for p in out['pads']:
        x, y = frame.to_px([(p['x'], p['y'])])[0]
        col = {'top': (0, 255, 255), 'bottom': (255, 0, 255), 'both': (255, 255, 0)}[p['face']]
        if p['shape'] == 'rect':
            w, h = p['w'] * frame.sx / 2, p['h'] * frame.sy / 2
            d.rectangle([x - w, y - h, x + w, y + h], outline=col)
        else:
            d.ellipse([x - 4, y - 4, x + 4, y + 4], outline=col)
    for f in out['footprints']:
        if not f['box']:
            continue
        q = frame.to_px([(f['box'][0], f['box'][1]), (f['box'][2], f['box'][3])])
        col = {'dip': (255, 0, 0), 'connector': (0, 128, 255), 'edge': (0, 255, 0)}.get(f['kind'], (255, 255, 255))
        d.rectangle([q[0][0], q[0][1], q[1][0], q[1][1]], outline=col, width=2)
        if f['pin1']:
            x, y = frame.to_px([f['pin1']])[0]
            d.ellipse([x - 8, y - 8, x + 8, y + 8], outline=(255, 255, 0) if f['pin1From'] else (255, 0, 255), width=3)
        d.text((q[0][0] + 3, q[0][1] + 3), f"{f['ref'] or '?'} {f['kind']} {f['pins']}", fill=(255, 255, 255))
    im.save(common.OUT / 'footprints.png')


def main():
    fj, frame = load_frame()
    m = marks()
    front = common.read_rgb('I1-front')
    back_m = common.read_rgb('I1-back', mirror=True)
    out = build(front, back_m, m, frame, fj['holes'])
    reg = out['reg']
    verdict = judge(reg['heldOutMm'], reg['holes'])
    excluded = [{'hole': e['index'], 'at': reg['pairs'][e['index']]['top'], 'why': e['why']} for e in reg['excluded']]
    fps = out['footprints']
    dips = [f for f in fps if f['kind'] == 'dip']
    result = {
        'about': ('Task 3 of the NES models plan: the solder side (I1-back, flipped) registered to the component side (I1-front) on every hole '
                  'found on both, and the pads, drills and footprints, in the board frame of frame.json (millimetres from the board\'s top left '
                  'corner as I1-front lies, x to the right, y down towards the edge fingers). Written by board_register.py; every figure here is a '
                  'measurement made on the date of the journal entry that quotes it.'),
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
            'worst': [{'hole': i, 'heldOutMm': e, 'at': reg['pairs'][i]['top'], 'kinds': reg['pairs'][i]['kinds']} for i, e in reg['worst']],
            'outliers': {
                'rule': ('A matched hole is left out of the scoring only if its pad\'s blob fails the roundness test on either face: the major to '
                         'minor axis ratio of its second moments over 1.25, or its area outside 0.6 to 1.6 times the median area of the matched '
                         'holes\' blobs on that face (the plan, fixed before any measurement; no residual is used). The verdict is judged without it.'),
                'excluded': excluded,
                'count': len(excluded),
                'heldOutMmWithExclusion': reg['heldOutMmWithExclusion'],
            },
            'matched': {
                'what': ('Every matched hole, in the order the excluded list counts them: its kind and blob on each face (axis ratio from second '
                         'moments, area in pixels), its place on each face in board millimetres, and its held-out error under the chosen model'),
                'columns': ['topKind', 'topAxis', 'topArea', 'bottomKind', 'bottomAxis', 'bottomArea', 'x', 'y', 'heldOutMm'],
                'rows': [[p['kinds'][0], p['blobs'][0]['axis'], p['blobs'][0]['area'], p['kinds'][1], p['blobs'][1]['axis'], p['blobs'][1]['area'],
                          p['top'][0], p['top'][1], p['heldOutMm']] for p in reg['pairs']],
            },
        },
        'drills': [{k: d[k] for k in ('x', 'y', 'd', 'filled', 'spreadMm', 'kinds')} for d in out['drills']],
        'drillSpreadMm': common.stats([d['spreadMm'] for d in out['drills']]),
        'notDrilled': {'what': f'Matched pairs whose two faces\' centres are over {DRILL_AGREE_MM} mm apart: recorded, not drilled',
                       'pairs': out['apart']},
        'pads': [{k: p[k] for k in ('x', 'y', 'w', 'h', 'shape', 'face', 'drill', 'kind', 'source')} for p in out['pads']],
        'padCounts': {'total': len(out['pads']), 'byFace': {f: sum(p['face'] == f for p in out['pads']) for f in ('top', 'bottom', 'both')},
                      'bySource': {s: sum(p['source'] == s for p in out['pads']) for s in ('drill', 'one face', 'completion', 'inferred', 'finger')},
                      'singleFace': out['single'], 'inFootprints': len({i for f in fps for i in f['pads']})},
        'footprints': [{k: f.get(k) for k in ('ref', 'kind', 'pins', 'pin1', 'pin1From', 'pads', 'box', 'axis', 'rowSpacingMm', 'pitchMm',
                                              'outline', 'notch', 'padsNotFound', 'padsInferred', 'alternate', 'pin1FromGrouping',
                                              'pin1AgreesWithGrouping', 'perFace', 'pinsNotFound', 'byHand') if k in f} for f in fps],
        'footprintCounts': {'dip': len(dips), 'connector': sum(f['kind'] == 'connector' for f in fps), 'other': sum(f['kind'] == 'other' for f in fps),
                            'dipPin1': {str(k): sum(f['pin1From'] == k for f in dips) for k in ('print', 'square pad', 'marked by hand', None)},
                            'withRef': sum(f['ref'] is not None for f in fps)},
        'marks': out['marks'],
    }
    hs = reg['heldOutMm']
    wx = reg['heldOutMmWithExclusion']
    print(f"solder side: {reg['model']}, {reg['holes']} holes; held out median {hs['median']:.3f}, p90 {hs['p90']:.3f}, max {hs['max']:.3f} mm: {verdict}")
    print(f"  with the roundness rule ({len(excluded)} excluded): median {wx['median']:.3f}, p90 {wx['p90']:.3f}, max {wx['max']:.3f} mm")
    print("  fits: " + '; '.join(f"{k} {v['heldOutMm']['median']:.3f}/{v['heldOutMm']['p90']:.3f}/{v['heldOutMm']['max']:.3f}" for k, v in reg['fits'].items()))
    print(f"  found: {reg['found']}")
    print(f"drills {len(out['drills'])} (not drilled {len(out['apart'])}), pads {len(out['pads'])}, footprints {len(fps)} ({len(dips)} DIPs); "
          f"fingers {len(out['fingers']['top'])} + {len(out['fingers']['bottom'])}")
    # Three places is a micron, far under anything judged; one record a line.
    common.write_data('registration.json', result, places=3, rows_per_line=True)
    overlays(front, back_m, out, frame)
    return 3 if verdict == 'STOP' else 0


if __name__ == '__main__':
    sys.exit(main())
