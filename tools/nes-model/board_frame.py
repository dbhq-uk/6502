"""Task 2: the board's frame, from the bare NES-CPU-10 scan of the component
side (I1-front): its x and y scales, its turn on the scanner glass, the
board's outline and its mounting holes. Every later board script reads
I1-front through this frame. Adapted from tools/bbc-micro-model/board_frame.py.

    NES_MODEL_INPUTS=<folder> python board_frame.py

The frame. A point X on the board, in millimetres from the board's top left
corner as the scan lies (x to the right, y down the scan towards the edge
fingers), is at pixel

    p = origin + S R(theta) X,    S = diag(sx, sy),

where sx and sy are the scanner's pixels per millimetre along its own x and
y (a flatbed scanner's two directions are made differently, so they are
measured apart) and theta is the board's turn on the glass. Because S acts
after the turn, a length in millimetres, |S^-1 d| for a pixel vector d, does
not depend on theta.

1. Scale. Every DIP on this board lies the same way, its rows of pins across
   the board, so x and y come from different things. Every pin of every row
   marked in data/marks.json ("scale", task 0's marks: each row's first and
   last pad) is found on its drill: the hole's top rim (common.rim_centre),
   fitted from the point the marks put it at and again from its solder
   blob's centroid (common.refine_to_pad), refused if the two fits disagree
   by more than DRILL_AGREE_MM or if the drill lies more than DRILL_ROW_MM
   from where a straight, evenly spaced row through the row's other drills
   puts it. The solder is not the drill: the BBC Micro's task 0 found solder
   centroids reading spacings about 1 per cent wide, and on this scan they
   wander by up to 4 pixels from the drills.
   x: rows of pins on the 2.54 mm pitch that run across the board, the DIPs'
   rows and the expansion header's, first drill to last. Not the edge
   fingers: task 0 found them on a 2.50 mm pitch, not 2.54. Their pitch
   through this frame is recorded. The same rows from the solder centroids
   (task 0's way) are recorded beside, not judged.
   y: each DIP's row spacing (15.24 or 7.62 mm): every pin of the pin 1 row
   paired with the pin directly across from it, the footprint's spacing the
   mean of its pairs' vectors, drill to drill. Task 0 used pin 1 and pin N
   alone, and the solder centroids give another figure; both recorded.
   sx and sy are fitted together, by least squares in millimetres, to make
   every row's length its pitches x 2.54 mm and every footprint's spacing its
   own; the rows set sx and the spacings sy.
   Held out (the plan's thresholds table): each row across left out of the
   fit in turn, its error first drill to last scaled to a 48.26 mm row, only
   rows of at least 30 mm scored; each footprint left out in turn, its error
   a per cent of its spacing.

2. The turn. The drills of every long row (10 pins or more), one turn for
   the whole board from all of them at once, with an offset per row, and a
   straight line per row. Each row's distance from its own line is recorded,
   and the rows at or over 0.05 mm named: recorded, not judged.

3. The outline. Its shape is marked by hand in data/marks.json ("outline"):
   the corners in order round the board, from the top left, each read by
   eye to about half a millimetre; the round notches, each by its deepest
   point; and the edges that carry the remains of break-off tabs. Where each
   edge lies is measured, not marked: at stations every 0.1 mm along it,
   TRIM_MM in from its ends (the router leaves inside corners rounded) and
   clear of its notches, the edge is where the light (linear, as the scanner
   mixes it) rises fastest from the board to the scanner's lid, within
   EDGE_SEARCH_MM of the marked line; for a blurred step that is its half
   way point. A straight line is fitted to the stations, robustly and one
   sided: points standing out of the board are left out, so the tabs'
   remains on the top right edge drop out. An edge shorter than
   SHORT_EDGE_MM is held square to the frame. On the short top left stretch
   the tabs' remains cover most of the edge, with half holes between them
   reaching down to the routed edge, so that edge is held square at the
   innermost TAB_PCT per cent of its stations, the half holes' bottoms. The
   corners are where neighbouring edges' lines meet; the origin is the first,
   the top left. A round notch is a circle fitted, robustly, to where the
   light rises fastest from the board into the notch, along rays from its
   centre over the board's side; it is drawn in the outline as an arc from
   where it leaves its edge's line to where it comes back.
   Known: where the lid is in deep shadow, in the inside corner under the
   right edge's step, the light rises most steeply out in the shadow, not at
   the board's edge, and that short edge is placed out in it (the journal,
   5 October 2026, gives how far).

4. The mounting holes. A hole shows the lid through the board: a pale region
   inside the board, round, filling its box. On this scan the hole's far wall
   is seen as a dark crescent on one side, so on that side the light falls
   fastest at the far rim, not at the hole's top rim, where the board's face
   on the glass meets the hole. The crescent comes from the scanner's
   optics, so it lies the same way in every hole: it is the circular median
   of each hole's darkest stretch just inside its rim (one hole's dirt cannot
   turn it round). Along rays from the middle on the other side, within
   HOLE_ARC_DEG of straight away from the crescent, the top rim is where the
   light falls fastest going out, and a circle is fitted to those by
   consensus (consensus_circle), twice from the new centre. Dirt in a hole
   makes rays that disagree; the consensus leaves them out.

Writes data/frame.json, a copy of I1-front rectified to the board frame at 12
pixels per millimetre in out/ (git-ignored, never committed: the scan states
no licence), and overlays to look at in out/. Exits 3 when a figure crosses
one of the plan's STOP thresholds.
"""
import json
import math
import sys

import numpy as np

import common

PITCH = 2.54
FORTY_PIN_MM = 48.26
STATED_DPI = 300
NOMINAL_PX_PER_MM = STATED_DPI / 25.4     # only to size search windows; the scale is measured

# The plan's thresholds for the scale (Global Constraints), the same numbers as
# site/tests/nes-spike-verdicts.mjs, which the site test reads.
PASS = {'xMedian': 0.15, 'yMedianPct': 0.5}
STOP = {'xMedian': 0.25, 'yMedianPct': 1.0, 'ratioPct': 1.5}
MIN = {'xRows': 4, 'yFootprints': 8}
X_SCORED_MIN_MM = 30.0          # a row across is scored if it is at least this long (the plan)

STRAIGHT_MM = 0.05              # the plan: a row's residual from a straight line, named at or over this
LONG_ROW_PADS = 10              # a long row, for the turn and the straightness
MIN_LINE_PADS = 8               # drills kept, for a row's line to be fitted
RECTIFIED_PX_PER_MM = 12
RECTIFIED_MARGIN_MM = 2.0

# A drill is refused when its two rim fits (from the marks' point and from
# the solder's centroid) disagree by more than this, or when it lies more
# than DRILL_ROW_MM from where a straight, evenly spaced row through the
# row's other drills puts it. Set on 5 October 2026 before any figure was
# judged, from the rim fits on I1-front's rows: fits that agree do so within
# 0.05 mm, and the ones that do not are 0.2 to 0.6 mm apart.
DRILL_AGREE_MM = 0.10
DRILL_ROW_MM = 0.15

# The outline.
EDGE_SEARCH_MM = 1.2            # the edge is looked for this far either side of the marked line
EDGE_STEP_MM = 0.1              # a station every this along an edge
TRIM_MM = 1.0                   # stations start this far from an edge's ends (inside corners are rounded, about 0.6 mm)
SHORT_EDGE_MM = 5.0             # an edge shorter than this is held square to the frame
TAB_PCT = 5                     # an edge with tabs' remains is at this percentile of its stations, the innermost
NOTCH_CLEAR_MM = 0.5            # stations within a notch's depth and this of its middle are left out of its edge
NOTCH_RAY_DEG = 70              # a notch's rays: within this of straight into the board
NOTCH_SEARCH_MM = 0.8           # a notch's rim is looked for this far either side of the circle so far

# The lid: pale and grey. Read off I1-front on 5 October 2026 (1st, 50th and
# 99th percentiles) from strips of lid beyond each edge: L 0.82 to 1.00, chroma
# under 0.01; on the left the lid is shaded towards the board, L down to about
# 0.75 within 2 mm of it. The board's edge (bare laminate, tinned copper or
# lacquer) is L 0.17 to 0.60. The limit sits between. Used only to find the
# mounting holes' candidates: the lid seen through the board.
LID_MIN_L, LID_MAX_C = 0.70, 0.03
# A mounting hole: the lid seen through the board. On I1-front the holes'
# regions fill 0.93 to 1.05 of their box's ellipse and the large tinned pads
# (the RF modulator's) 0.34 to 0.69, read off the candidates before any hole
# was fitted; the limit sits between.
HOLE_MIN_D_MM, HOLE_MAX_D_MM = 1.4, 6.0
HOLE_MAX_ASPECT, HOLE_MIN_FILL = 2.0, 0.85
HOLE_RAYS = 180
HOLE_ARC_DEG = 75               # the rays used: within this of straight away from the crescent
HOLE_TOL_MM = 0.08              # a ray agrees with the rim's circle within this (about a pixel)
CRESCENT_SMOOTH_DEG = 30        # the crescent is the darkest stretch, the light averaged over this either side


# --- the frame ------------------------------------------------------------------

def rotation(theta):
    c, s = math.cos(theta), math.sin(theta)
    return np.array([[c, -s], [s, c]])


class Frame:
    """Board millimetres to I1-front's pixels and back (the module's docstring)."""

    def __init__(self, sx, sy, theta, origin):
        self.sx, self.sy, self.theta = float(sx), float(sy), float(theta)
        self.origin = np.asarray(origin, float)
        self.M = np.diag([self.sx, self.sy]) @ rotation(self.theta)
        self.Minv = np.linalg.inv(self.M)

    def to_px(self, mm):
        return np.asarray(mm, float).reshape(-1, 2) @ self.M.T + self.origin

    def to_board(self, px):
        return (np.asarray(px, float).reshape(-1, 2) - self.origin) @ self.Minv.T

    @classmethod
    def read(cls, f):
        """The frame frame.json holds."""
        return cls(f['pxPerMm']['x'], f['pxPerMm']['y'], math.radians(f['rotationDeg']), f['origin'])


def to_turned(px, sx, sy, theta):
    """Pixels to millimetres on the board's axes, before the origin is known."""
    q = np.asarray(px, float).reshape(-1, 2) / np.array([sx, sy])
    return q @ rotation(-theta).T


def from_turned(q, sx, sy, theta):
    return (np.asarray(q, float).reshape(-1, 2) @ rotation(theta).T) * np.array([sx, sy])


def length_mm(d, sx, sy):
    """A pixel vector's length in millimetres, whatever the board's turn."""
    return float(math.hypot(d[0] / sx, d[1] / sy))


def linear_light(rgb):
    """Luminance in linear light (the scanner's blur mixes light, not the
    stored sRGB numbers), 0 to 1."""
    c = rgb.astype(np.float32) / 255
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return (0.2126 * c[..., 0] + 0.7152 * c[..., 1] + 0.0722 * c[..., 2]).astype(np.float32)


# --- 1. the drills and the scale ----------------------------------------------------

def drill_at(L, seed, centroid, px_per_mm=NOMINAL_PX_PER_MM):
    """A pin's drill, on its top rim: fitted from `seed` (where the marks put
    the pin) and, when the pad's solder was found, from its centroid too.
    Returns ((x, y), None), or (None, why) when refused: no rim from either,
    or the two fits more than DRILL_AGREE_MM apart."""
    fits = []
    for s in (seed, centroid):
        if s is not None:
            r, _ = common.rim_centre(L, s[0], s[1], px_per_mm=px_per_mm)
            if r is not None:
                fits.append((r['x'], r['y']))
    if not fits:
        return None, 'no rim'
    if len(fits) == 2 and math.hypot(fits[0][0] - fits[1][0], fits[0][1] - fits[1][1]) > DRILL_AGREE_MM * px_per_mm:
        return None, 'two fits disagree'
    return fits[0], None


def drills_on_their_row(drills, px_per_mm=NOMINAL_PX_PER_MM):
    """Which of a row's drills (one (x, y) or None per pin, in order) to keep:
    a drill is refused when it lies more than DRILL_ROW_MM from where a
    straight, evenly spaced row fitted to the row's other kept drills puts it
    (each judged held out of its own fit, so an end drill cannot pull the
    row to itself), the worst first, one at a time. Returns (keep, why)."""
    n = len(drills)
    keep = [d is not None for d in drills]
    why = [None] * n
    P = np.array([d if d is not None else (np.nan, np.nan) for d in drills], float)
    k = np.arange(n, dtype=float)
    while sum(keep) > 3:
        worst, err = None, 0.0
        idx = [i for i in range(n) if keep[i]]
        for i in idx:
            others = [j for j in idx if j != i]
            fx = np.polyfit(k[others], P[others, 0], 1)
            fy = np.polyfit(k[others], P[others, 1], 1)
            e = math.hypot(np.polyval(fx, k[i]) - P[i, 0], np.polyval(fy, k[i]) - P[i, 1])
            if e > err:
                worst, err = i, e
        if err <= DRILL_ROW_MM * px_per_mm:
            break
        keep[worst] = False
        why[worst] = 'off its row'
    return keep, why


def measure_rows(rgb, L, rows, px_per_mm=NOMINAL_PX_PER_MM):
    """Every pin of every row (k = 0, the first, to `pitches`, the last): the
    solder's centroid (`pads`, None where refine_to_pad refuses) and the drill
    (`drills`, None where refused, with why in `refused`). `found` is the
    (k, x, y) of the drills kept; `d` is from the first drill to the last
    (None when either is refused) and `dCentroids` the same from the solder;
    `trueMm` is the row's length on its pitch."""
    out = []
    for r in rows:
        a, b = np.asarray(r['first'], float), np.asarray(r['last'], float)
        pads, drills, refused = [], [], []
        for k in range(r['pitches'] + 1):
            seed = a + (b - a) * k / r['pitches']
            c = common.refine_to_pad(rgb, seed[0], seed[1], px_per_mm)
            pads.append(None if c is None else (c[0], c[1]))
            d, why = drill_at(L, seed, pads[-1], px_per_mm)
            drills.append(d)
            if why:
                refused.append({'pin': k, 'why': why})
        keep, why = drills_on_their_row(drills, px_per_mm)
        for k, w in enumerate(why):
            if w:
                refused.append({'pin': k, 'why': w})
        drills = [d if ok else None for d, ok in zip(drills, keep)]
        out.append(dict(r, pads=pads, drills=drills, refused=sorted(refused, key=lambda x: x['pin']),
                        found=[(k, p[0], p[1]) for k, p in enumerate(drills) if p is not None],
                        d=None if drills[0] is None or drills[-1] is None else np.subtract(drills[-1], drills[0]),
                        dCentroids=None if pads[0] is None or pads[-1] is None else np.subtract(pads[-1], pads[0]),
                        trueMm=r['pitches'] * r.get('pitchMm', PITCH)))
    return out


def measure_footprints(rows, footprints):
    """Each DIP's row spacing: every pin of its pin 1 row paired with the pin
    directly across. `d` is the mean of the pairs' drill vectors (pin 1 row to
    pin N row); `dCentroids` the same from the solder's centroids, and `dPin1`
    pin 1 to pin N alone (task 0's way), both recorded."""
    by_id = {r['id']: r for r in rows}
    out = []
    for fp in footprints:
        A, B = by_id[fp['pin1Row']], by_id[fp['pinNRow']]
        pairs, cents, refused = [], [], []
        for k, (p, q) in enumerate(zip(A['drills'], B['drills'])):
            if p is None or q is None:
                refused.append(k)
            else:
                pairs.append((k, np.subtract(q, p)))
            if A['pads'][k] is not None and B['pads'][k] is not None:
                cents.append(np.subtract(B['pads'][k], A['pads'][k]))
        out.append(dict(fp, d=np.mean([v for _, v in pairs], 0) if pairs else None, pairs=len(pairs), refusedPairs=refused,
                        pairVectors=pairs, dCentroids=np.mean(cents, 0) if cents else None, dPin1=dict(pairs).get(0)))
    return out


def fit_scale(rows, footprints):
    """sx and sy that make every row's length, |S^-1 d|, its true length and
    every footprint's spacing its own, by least squares in millimetres. Rows
    are across (they set sx); spacings are down (they set sy)."""
    from scipy.optimize import least_squares
    if not rows or not footprints:
        raise ValueError('the scale needs rows across and footprints\' spacings down')
    d = np.array([r['d'] for r in rows] + [f['d'] for f in footprints], float)
    L = np.array([r['trueMm'] for r in rows] + [f['spacingMm'] for f in footprints], float)

    def resid(s):
        return np.hypot(d[:, 0] / s[0], d[:, 1] / s[1]) - L
    r = least_squares(resid, [NOMINAL_PX_PER_MM, NOMINAL_PX_PER_MM], method='lm', xtol=1e-15, ftol=1e-15, gtol=1e-15)
    return float(r.x[0]), float(r.x[1])


def held_out_x(rows, footprints):
    """Each row across left out of the fit in turn, measured through the scale
    fitted to every other row and every footprint; its error scaled to a
    48.26 mm row."""
    out = []
    for i, r in enumerate(rows):
        sx, sy = fit_scale(rows[:i] + rows[i + 1:], footprints)
        m = length_mm(r['d'], sx, sy)
        err = m - r['trueMm']
        out.append({'row': r['id'], 'pitches': r.get('pitches'), 'lengthMm': r['trueMm'], 'measuredMm': m, 'errMm': err,
                    'scaledErrMm': err * FORTY_PIN_MM / r['trueMm'], 'scored': r['trueMm'] >= X_SCORED_MIN_MM})
    return out


def held_out_y(rows, footprints, key='d'):
    """Each footprint left out of the fit in turn, its spacing measured
    through the scale fitted to the rest; its error a per cent of its
    spacing. `key` picks the vector (the drills' by default)."""
    use = [dict(f, d=f[key]) for f in footprints]
    out = []
    for i, f in enumerate(use):
        sx, sy = fit_scale(rows, use[:i] + use[i + 1:])
        m = length_mm(f['d'], sx, sy)
        out.append({'ref': f['ref'], 'pins': f.get('pins'), 'spacingMm': f['spacingMm'], 'pairs': f.get('pairs'),
                    'measuredMm': m, 'errPct': 100 * (m - f['spacingMm']) / f['spacingMm']})
    return out


def abs_stats(values):
    e = np.abs(np.asarray(values, float))
    if not len(e):
        return {'n': 0}
    return dict(common.stats(e), n=len(e))


def x_summary(rows, footprints):
    """The x scale and its held-out errors over the scored rows."""
    held = held_out_x(rows, footprints)
    scored = [h for h in held if h['scored']]
    s = abs_stats([h['scaledErrMm'] for h in scored])
    largest = max(scored, key=lambda h: abs(h['scaledErrMm']), default=None)
    return held, {'pxPerMmX': fit_scale(rows, footprints)[0], 'rows': len(held), 'n': len(scored),
                  'scaledErrMm': {k: s[k] for k in ('median', 'max') if k in s}, 'largestRow': largest['row'] if largest else None}


def y_summary(rows, footprints, key='d'):
    """The y scale and its held-out errors from one choice of vectors."""
    use = [dict(f, d=f[key]) for f in footprints if f[key] is not None]
    held = held_out_y(rows, use)
    e = [h['errPct'] for h in held]

    def part(spacing):
        return abs_stats([h['errPct'] for h in held if h['spacingMm'] == spacing])
    return held, {
        'pxPerMmY': fit_scale(rows, use)[1],
        'footprints': len(held),
        'errPct': {'median': float(np.median(np.abs(e))), 'max': float(np.max(np.abs(e)))},
        'errPct600': part(15.24),
        'errPct300': part(7.62),
    }


# --- 2. the turn and the straightness ------------------------------------------------

def fit_turn(rows, footprints, sx, sy):
    """One turn for the board from every long row's drills (an offset per
    row), and each row's own straight line: its angle and its drills'
    distances from it, in millimetres. The skew: how far each footprint's
    spacing vector is from square to the rows, recorded."""
    lines, pooled = [], np.zeros((2, 2))
    for r in rows:
        if r['pitches'] + 1 < LONG_ROW_PADS or len(r['found']) < MIN_LINE_PADS:
            continue
        q = np.array([p[1:] for p in r['found']], float) / np.array([sx, sy])
        c = q - q.mean(0)
        scatter = c.T @ c
        pooled += scatter
        w, v = np.linalg.eigh(scatter)
        u = v[:, 1] * np.sign(v[0, 1] or 1)
        res = c @ np.array([-u[1], u[0]])
        along = c @ u
        half = (along.max() - along.min()) / 2
        bow = float(np.polyfit(along, res, 2)[0] * half * half)
        lines.append({'row': r['id'], 'pads': len(r['found']), 'of': r['pitches'] + 1,
                      'angleDeg': math.degrees(math.atan2(u[1], u[0])),
                      'rmsMm': float(np.sqrt((res ** 2).mean())), 'maxMm': float(np.abs(res).max()), 'bowMm': bow})
    w, v = np.linalg.eigh(pooled)
    u = v[:, 1] * np.sign(v[0, 1] or 1)
    theta = math.atan2(u[1], u[0])
    skews = []
    for f in footprints:
        if f['d'] is None:
            continue
        q = np.asarray(f['d'], float) / np.array([sx, sy])
        # square to the rows is the direction theta + 90 degrees
        a = math.atan2(q[1], q[0]) - (theta + math.pi / 2)
        skews.append({'ref': f['ref'], 'skewDeg': math.degrees((a + math.pi / 2) % math.pi - math.pi / 2)})
    return theta, lines, skews


# --- 3. the outline -------------------------------------------------------------------

def _uv(axis, q):
    """(along, across) of points: (x, y) for an edge across, (y, x) for one down."""
    q = np.asarray(q, float)
    return (q[..., 0], q[..., 1]) if axis == 'x' else (q[..., 1], q[..., 0])


def _xy(axis, u, v):
    return np.stack([u, v], -1) if axis == 'x' else np.stack([v, u], -1)


def outline_edges(marks, sx, sy, theta):
    """The marked outline on the board's axes (millimetres, before the
    origin): each edge, from corner i to corner i + 1, with its axis ('x'
    across, 'y' down), its extent along that axis (u0 to u1), where the marks
    put it across (v), which way is out of the board (out, +1 when out is
    towards larger v), whether it carries tabs' remains, and its notches
    (their middle along it, u, how deep the marked deepest point is, and that
    point). Raises ValueError when two edges in a row run the same way: a
    corner marked where the outline does not turn."""
    Q = to_turned(marks['corners'], sx, sy, theta)
    n = len(Q)
    area = 0.5 * float(np.sum(Q[:, 0] * np.roll(Q[:, 1], -1) - np.roll(Q[:, 0], -1) * Q[:, 1]))
    edges = []
    for i in range(n):
        a, b = Q[i], Q[(i + 1) % n]
        d = b - a
        axis = 'x' if abs(d[0]) >= abs(d[1]) else 'y'
        left = np.array([-d[1], d[0]])                  # the board is on the left of travel when the area is positive
        out = -left if area > 0 else left
        out_sign = float(np.sign(out[1] if axis == 'x' else out[0]))
        ua, va = _uv(axis, a)
        ub, vb = _uv(axis, b)
        edges.append({'index': i, 'axis': axis, 'u0': float(min(ua, ub)), 'u1': float(max(ua, ub)), 'v': float((va + vb) / 2),
                      'out': out_sign, 'forward': bool(ub > ua), 'tabs': i in marks.get('tabs', []), 'notches': []})
    for i in range(n):
        if edges[i]['axis'] == edges[(i + 1) % n]['axis']:
            raise ValueError(f'the outline does not turn at corner {(i + 1) % n}: edges {i} and {(i + 1) % n} both run '
                             f'{"across" if edges[i]["axis"] == "x" else "down"}')
    for m in marks.get('notches', []):
        e = edges[m['edge']]
        q = to_turned([m['deepest']], sx, sy, theta)[0]
        u, v = _uv(e['axis'], q)
        e['notches'].append({'id': m.get('id'), 'u': float(u), 'depth': float((e['v'] - v) * e['out']), 'deepest': q})
    return edges


def _profile_edge(Y, pts_mm, normals, sx, sy, theta, search):
    """Where the light rises fastest along `normals` (unit, pointing out of
    the board, towards the lid) from each of `pts_mm`, within `search` either
    side, to a fraction of a step: one distance per point, its profile
    averaged with its two neighbours either side; NaN where nothing rises."""
    from scipy.ndimage import gaussian_filter1d, map_coordinates, uniform_filter1d
    t = np.arange(-search, search + 1e-9, 0.02)
    q = pts_mm[:, None, :] + normals[:, None, :] * t[None, :, None]
    p = from_turned(q.reshape(-1, 2), sx, sy, theta)
    prof = map_coordinates(Y, [p[:, 1], p[:, 0]], order=1, mode='nearest').reshape(len(pts_mm), len(t))
    prof = uniform_filter1d(prof, 5, axis=0, mode='nearest')
    g = np.gradient(gaussian_filter1d(prof, 1.5, axis=1), t, axis=1)
    out = np.full(len(g), np.nan)
    for i, gi in enumerate(g):
        j = int(np.argmax(gi))
        if gi[j] <= 0:
            continue
        out[i] = t[j]
        if 0 < j < len(t) - 1:
            a, b, c = gi[j - 1], gi[j], gi[j + 1]
            den = a - 2 * b + c
            if den < 0:
                out[i] += 0.5 * (a - c) / den * (t[1] - t[0])
    return out


def _robust_line(u, v, out, slope=True, keep_mm=0.15, inward_mm=0.5):
    """v = a + b (u - um), refitted until it settles, one sided: a point more
    than keep_mm out of the board (out is +1 or -1, the sign of a residual
    pointing out) is left out whatever the spread, one inward only beyond
    three robust standard deviations and inward_mm. So bumps standing out of
    the routed edge are left out even where they are much of it. keep_mm is
    over a pixel (0.085 mm), so both steps of a turned edge's pixel staircase
    are kept. slope=False holds the line square to the frame (b = 0)."""
    um = float(u.mean())
    ok = np.ones(len(u), bool)
    for _ in range(12):
        if slope:
            b, a = np.polyfit(u[ok] - um, v[ok], 1)
        else:
            b, a = 0.0, float(np.median(v[ok]))
        r = (v - (a + b * (u - um))) * out
        mad = 1.4826 * np.median(np.abs(r[ok]))
        new = (r <= keep_mm) & (r >= -max(3 * mad, inward_mm))
        if new.sum() < 3 or (new == ok).all():
            break
        ok = new
    r = v - (a + b * (u - um))
    return float(a), float(b), um, int(ok.sum()), float(np.sqrt((r[ok] ** 2).mean()))


def fit_edge(Y, e, sx, sy, theta):
    """Edge e's line (adds a, b, um and how it was fitted to e): the light's
    edge at stations along it (the module's docstring, 3)."""
    length = e['u1'] - e['u0']
    if length > 2 * TRIM_MM + 0.3:
        us = np.arange(e['u0'] + TRIM_MM, e['u1'] - TRIM_MM + 1e-9, EDGE_STEP_MM)
    else:
        us = np.linspace(e['u0'] + length / 3, e['u1'] - length / 3, 5)
    for n in e['notches']:
        us = us[np.abs(us - n['u']) >= n['depth'] + NOTCH_CLEAR_MM]
    pts = _xy(e['axis'], us, np.full(len(us), e['v']))
    normal = (np.array([0.0, 1.0]) if e['axis'] == 'x' else np.array([1.0, 0.0])) * e['out']
    offs = _profile_edge(Y, pts, np.tile(normal, (len(us), 1)), sx, sy, theta, EDGE_SEARCH_MM)
    ok = ~np.isnan(offs)
    u, v = us[ok], e['v'] + e['out'] * offs[ok]
    if len(u) < 3:
        raise ValueError(f'edge {e["index"]}: the light rises at only {len(u)} stations')
    square = length < SHORT_EDGE_MM or e['tabs']
    if e['tabs']:
        level = float(np.percentile(v * e['out'], TAB_PCT)) * e['out']
        a, b, um, used = level, 0.0, float(u.mean()), int(np.sum(v * e['out'] <= level * e['out'] + 1e-12))
        rms = None
    else:
        a, b, um, used, rms = _robust_line(u, v, e['out'], slope=not square)
    e.update(a=a, b=b, um=um, stations=int(len(us)), lightFound=int(len(u)), points=used, rmsMm=rms, square=bool(square),
             shiftMm=float(np.median(v - e['v']) * e['out']))
    return e


def _line_at(e, u):
    return e['a'] + e['b'] * (u - e['um'])


def _meet(e, f):
    """Where two lines meet, one across ('x': y = a + b (x - um)) and one
    down ('y': x = a + b (y - um))."""
    h, v = (e, f) if e['axis'] == 'x' else (f, e)
    A = np.array([[-h['b'], 1.0], [1.0, -v['b']]])
    rhs = np.array([h['a'] - h['b'] * h['um'], v['a'] - v['b'] * v['um']])
    return np.linalg.solve(A, rhs)


def _circle(pts):
    """A circle through points, by the algebraic fit then least squares."""
    from scipy.optimize import least_squares
    x, y = pts[:, 0], pts[:, 1]
    A = np.c_[2 * x, 2 * y, np.ones(len(x))]
    cx, cy, c = np.linalg.lstsq(A, x * x + y * y, rcond=None)[0]
    r0 = math.sqrt(max(c + cx * cx + cy * cy, 1e-12))
    res = least_squares(lambda p: np.hypot(x - p[0], y - p[1]) - p[2], [cx, cy, r0], method='lm')
    return res.x


def _robust_circle(q, floor_mm):
    """A circle fitted to points, leaving out those more than three robust
    standard deviations (and floor_mm) off it, four times. Returns the
    circle, the points kept and the rms of their distances from it."""
    keep = np.ones(len(q), bool)
    for _ in range(4):
        cx, cy, r = _circle(q[keep])
        res = np.hypot(q[:, 0] - cx, q[:, 1] - cy) - r
        mad = 1.4826 * np.median(np.abs(res[keep]))
        keep = np.abs(res) <= max(3 * mad, floor_mm)
    return (float(cx), float(cy), float(r)), keep, float(np.sqrt((res[keep] ** 2).mean()))


def consensus_circle(q, r_range, tol):
    """A circle that most of the points agree with: of the circles through
    three of them (every second point, so up to about 9,000 triples) with a
    radius in r_range, the one that the most points lie within `tol` of,
    then fitted by least squares to those, twice. Returns the circle, the
    points that agree, and the rms of their distances; None when no triple
    has a radius in range. Robust where a quarter of the points are wrong."""
    import itertools
    P = np.asarray(q, float)
    idx = np.arange(0, len(P), 2 if len(P) > 30 else 1)
    T = np.array(list(itertools.combinations(idx, 3)))
    a, b, c = P[T[:, 0]], P[T[:, 1]], P[T[:, 2]]
    d = 2 * (a[:, 0] * (b[:, 1] - c[:, 1]) + b[:, 0] * (c[:, 1] - a[:, 1]) + c[:, 0] * (a[:, 1] - b[:, 1]))
    ok = np.abs(d) > 1e-12
    d = np.where(ok, d, 1.0)
    sa, sb, sc = (a ** 2).sum(1), (b ** 2).sum(1), (c ** 2).sum(1)
    ux = (sa * (b[:, 1] - c[:, 1]) + sb * (c[:, 1] - a[:, 1]) + sc * (a[:, 1] - b[:, 1])) / d
    uy = (sa * (c[:, 0] - b[:, 0]) + sb * (a[:, 0] - c[:, 0]) + sc * (b[:, 0] - a[:, 0])) / d
    r = np.hypot(a[:, 0] - ux, a[:, 1] - uy)
    ok &= (r >= r_range[0]) & (r <= r_range[1])
    if not ok.any():
        return None
    ux, uy, r = ux[ok], uy[ok], r[ok]
    votes = np.zeros(len(r), int)
    for s in range(0, len(r), 2000):                    # in blocks, to bound the memory
        dist = np.abs(np.hypot(P[None, :, 0] - ux[s:s + 2000, None], P[None, :, 1] - uy[s:s + 2000, None]) - r[s:s + 2000, None])
        votes[s:s + 2000] = (dist <= tol).sum(1)
    i = int(np.argmax(votes))
    circ = (ux[i], uy[i], r[i])
    keep = np.abs(np.hypot(P[:, 0] - circ[0], P[:, 1] - circ[1]) - circ[2]) <= tol
    for _ in range(2):
        circ = _circle(P[keep])
        keep = np.abs(np.hypot(P[:, 0] - circ[0], P[:, 1] - circ[1]) - circ[2]) <= tol
    res = np.hypot(P[keep, 0] - circ[0], P[keep, 1] - circ[1]) - circ[2]
    return (float(circ[0]), float(circ[1]), float(circ[2])), keep, float(np.sqrt((res ** 2).mean()))


def fit_notch(Y, e, notch, sx, sy, theta):
    """A round notch in edge e: a circle fitted to where the light rises
    fastest going from the board into the notch, along rays from its centre
    within NOTCH_RAY_DEG of straight into the board, robustly; three times,
    each from the circle before. It starts from the point on the edge's line
    beside the marked deepest point, with the marked depth as its radius."""
    c = _xy(e['axis'], np.array(notch['u']), np.array(_line_at(e, notch['u']))).astype(float)
    r = max(notch['depth'], 0.3)
    inward = -(np.array([0.0, 1.0]) if e['axis'] == 'x' else np.array([1.0, 0.0])) * e['out']
    a0 = math.atan2(inward[1], inward[0])
    th = a0 + np.radians(np.linspace(-NOTCH_RAY_DEG, NOTCH_RAY_DEG, 57))
    dirs = np.c_[np.cos(th), np.sin(th)]
    for _ in range(3):
        search = min(NOTCH_SEARCH_MM, 0.85 * r)
        t = _profile_edge(Y, c + dirs * r, -dirs, sx, sy, theta, search)        # out of the board is towards the centre
        ok = ~np.isnan(t)
        if ok.sum() < 8:
            raise ValueError(f'notch {notch["id"]}: the light rises along only {int(ok.sum())} rays')
        q = c + dirs[ok] * (r - t[ok])[:, None]
        (cx, cy, r), keep, rms = _robust_circle(q, 0.03)
        c = np.array([cx, cy])
    return {'id': notch['id'], 'edge': e['index'], 'x': float(c[0]), 'y': float(c[1]), 'r': float(r), 'fitRmsMm': rms,
            'rays': int(ok.sum()), 'kept': int(keep.sum())}


def notch_arc(e, n, step_deg=10):
    """Points along round notch n from where it leaves edge e's line to where
    it comes back, round the board's side, in the order the outline runs
    along the edge."""
    uc, vc = _uv(e['axis'], np.array([n['x'], n['y']]))
    r, a, b, um = n['r'], e['a'], e['b'], e['um']
    # (u - uc)^2 + (a + b (u - um) - vc)^2 = r^2
    k0 = a - b * um - vc
    qa, qb, qc = 1 + b * b, 2 * (b * k0 - uc), uc * uc + k0 * k0 - r * r
    disc = qb * qb - 4 * qa * qc
    if disc <= 0:
        raise ValueError(f'notch {n["id"]}: its circle does not cross its edge\'s line')
    us = sorted(((-qb - math.sqrt(disc)) / (2 * qa), (-qb + math.sqrt(disc)) / (2 * qa)), reverse=not e['forward'])
    ends = [np.array(_xy(e['axis'], np.array(u), np.array(_line_at(e, u))), float) for u in us]
    c = np.array([n['x'], n['y']])
    inward = -(np.array([0.0, 1.0]) if e['axis'] == 'x' else np.array([1.0, 0.0])) * e['out']
    a0, a1 = (math.atan2(p[1] - c[1], p[0] - c[0]) for p in ends)
    am = math.atan2(inward[1], inward[0])
    ccw = (a1 - a0) % (2 * math.pi)
    sweep = ccw if (am - a0) % (2 * math.pi) < ccw else ccw - 2 * math.pi
    steps = max(2, int(math.ceil(abs(math.degrees(sweep)) / step_deg)))
    t = a0 + sweep * np.arange(steps + 1) / steps
    return list(c + r * np.c_[np.cos(t), np.sin(t)])


def fit_outline(Y, marks, sx, sy, theta):
    """The outline on the board's axes in millimetres (before the origin):
    the corners (corner i where edge i - 1 meets edge i), the outline's
    points in order (each corner, and each notch's arc along its edge), the
    edges and the notches."""
    edges = outline_edges(marks, sx, sy, theta)
    for e in edges:
        fit_edge(Y, e, sx, sy, theta)
    n = len(edges)
    corners = np.array([_meet(edges[i - 1], edges[i]) for i in range(n)])
    if int(np.argmin(corners.sum(1))) != 0:
        raise ValueError('the outline\'s marks must start at the board\'s top left corner')
    notches, pts = [], []
    for i, e in enumerate(edges):
        pts.append(corners[i])
        for m in sorted(e['notches'], key=lambda m: m['u'] if e['forward'] else -m['u']):
            f = fit_notch(Y, e, m, sx, sy, theta)
            notches.append(f)
            pts.extend(notch_arc(e, f))
    return corners, np.array(pts), edges, notches


# --- 4. the mounting holes -------------------------------------------------------------

def lid_and_chroma(rgb):
    import cv2
    L, A, B = common.oklab(rgb)
    C = np.hypot(A, B).astype(np.float32)
    return cv2.medianBlur(L.astype(np.float32), 5), cv2.medianBlur(C, 5)


def board_mask(L, C):
    """The board: the largest region that is not lid, everything inside it
    filled. Returns the filled mask, the labels of the regions that are not
    board, and those of them inside the board (dicts: label, stats,
    centroid), which the holes are picked from."""
    import cv2
    lid = (L > LID_MIN_L) & (C < LID_MAX_C)
    n, lab, st, _ = cv2.connectedComponentsWithStats((~lid).astype(np.uint8), 4)
    big = 1 + int(np.argmax(st[1:, 4]))
    board = (lab == big).astype(np.uint8)
    n, lab, st, cen = cv2.connectedComponentsWithStats(1 - board, 4)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])).tolist())
    inside = [i for i in range(1, n) if i not in border]
    filled = (board | np.isin(lab, inside)).astype(np.uint8)
    return filled, lab, [{'label': i, 'stats': st[i].tolist(), 'centroid': cen[i].tolist()} for i in inside]


def _ray_light(Y, c, r_px):
    """The light along HOLE_RAYS rays from c, smoothed along each: (angles,
    radii, light)."""
    from scipy.ndimage import gaussian_filter1d, map_coordinates
    t = np.linspace(0, 2 * math.pi, HOLE_RAYS, endpoint=False)
    r = np.arange(0.5 * r_px, 1.3 * r_px + 3, 0.25)
    xs = c[0] + np.outer(np.cos(t), r)
    ys = c[1] + np.outer(np.sin(t), r)
    return t, r, gaussian_filter1d(map_coordinates(Y, [ys.ravel(), xs.ravel()], order=1, mode='nearest').reshape(xs.shape), 2.0, axis=1)


def crescent_of(Y, centre_px, r_px):
    """The direction (radians, on the scan) of a hole's darkest stretch just
    inside its rim, the light averaged over CRESCENT_SMOOTH_DEG either side."""
    t, r, v = _ray_light(Y, np.asarray(centre_px, float), r_px)
    inner = v[:, (r >= 0.55 * r_px) & (r <= 0.9 * r_px)].mean(1)
    w = int(round(CRESCENT_SMOOTH_DEG / 360 * HOLE_RAYS))
    smooth = np.array([inner[(i + np.arange(-w, w + 1)) % HOLE_RAYS].mean() for i in range(HOLE_RAYS)])
    return float(t[int(np.argmin(smooth))])


def circular_median(angles):
    """The angle with the least summed angular distance to the others."""
    a = np.asarray(angles, float)
    cost = [np.abs((a - x + math.pi) % (2 * math.pi) - math.pi).sum() for x in a]
    return float(a[int(np.argmin(cost))])


def hole_rim(Y, centre_px, r_px, sx, sy, theta, crescent):
    """A mounting hole's top rim (the module's docstring, 4), the crescent
    lying towards `crescent` (radians, on the scan). Returns the centre and
    diameter in millimetres on the board's axes, with how well it fits, or
    None when fewer than half the rays used agree."""
    c = np.asarray(centre_px, float)
    for _ in range(2):
        t, r, v = _ray_light(Y, c, r_px)
        g = -np.gradient(v, r, axis=1)                  # the fall in light going out
        j = np.argmax(g, 1)
        rr = r[j].astype(float)
        for i in range(HOLE_RAYS):
            if 0 < j[i] < len(r) - 1:
                a, b, cc = g[i, j[i] - 1], g[i, j[i]], g[i, j[i] + 1]
                den = a - 2 * b + cc
                if den < 0:
                    rr[i] += 0.5 * (a - cc) / den * 0.25
        dang = (t - crescent - math.pi + math.pi) % (2 * math.pi) - math.pi
        use = np.abs(dang) <= math.radians(HOLE_ARC_DEG)
        q = to_turned((c + rr[:, None] * np.c_[np.cos(t), np.sin(t)])[use], sx, sy, theta)
        r_mm = r_px / math.sqrt(sx * sy)
        fit = consensus_circle(q, (0.7 * r_mm, 1.3 * r_mm), HOLE_TOL_MM)
        if fit is None:
            return None
        (cx, cy, rad), ok, rms = fit
        if ok.sum() < 0.5 * use.sum():
            return None
        c = from_turned([(cx, cy)], sx, sy, theta)[0]
        r_px = rad * math.sqrt(sx * sy)
    return {'x': float(cx), 'y': float(cy), 'd': float(2 * rad), 'rimRmsMm': rms, 'rays': int(use.sum()), 'kept': int(ok.sum())}


def _board_deg(angle, sx, sy, theta):
    """A direction on the scan (radians) as degrees on the board's axes."""
    d = to_turned([(math.cos(angle) * 10, math.sin(angle) * 10)], sx, sy, theta)[0] - to_turned([(0.0, 0.0)], sx, sy, theta)[0]
    return float(math.degrees(math.atan2(d[1], d[0])) % 360)


def find_holes(Y, inside, sx, sy, theta):
    """Mounting holes, on the board's axes in millimetres (before the
    origin): centre, diameter, fit. The crescent comes from the scanner's
    optics, so it lies the same way in every hole on one scan: each hole's
    own darkest direction is found, and every hole is fitted with their
    circular median, so that dirt in one hole cannot turn its fit round.
    Returns (holes, every candidate big enough to be one, with why it was
    kept or refused, and the scan's crescent direction in degrees on the
    board's axes)."""
    s = math.sqrt(sx * sy)
    seen, round_ = [], []
    for c in inside:
        x0, y0, w, h, area = c['stats']
        d_eq = 2 * math.sqrt(area / math.pi) / s
        if not HOLE_MIN_D_MM <= d_eq <= HOLE_MAX_D_MM:
            continue
        fill = area / (math.pi / 4 * w * h)
        q = to_turned([c['centroid']], sx, sy, theta)[0]
        entry = {'centroidMm': q.tolist(), 'dEqMm': d_eq, 'fill': fill, 'aspect': max(w, h) / min(w, h), 'refused': None}
        if max(w, h) > HOLE_MAX_ASPECT * min(w, h):
            entry['refused'] = 'not round'
        elif fill < HOLE_MIN_FILL:
            entry['refused'] = 'does not fill its box (a pad)'
        else:
            own = crescent_of(Y, c['centroid'], max(w, h) / 2)
            entry['ownCrescentDeg'] = _board_deg(own, sx, sy, theta)
            round_.append((entry, c, own))
        seen.append(entry)
    holes = []
    crescent = circular_median([own for _, _, own in round_]) if round_ else None
    for entry, c, _ in round_:
        rim = hole_rim(Y, c['centroid'], max(c['stats'][2:4]) / 2, sx, sy, theta, crescent)
        if rim is None:
            entry['refused'] = 'no clear rim'
        else:
            rim['crescentDeg'] = _board_deg(crescent, sx, sy, theta)
            entry.update(rim)
            holes.append(rim)
    return holes, seen, (None if crescent is None else _board_deg(crescent, sx, sy, theta))


# --- all of it ------------------------------------------------------------------------

def measure(rgb, rows, footprints, outline_marks, px_per_mm=NOMINAL_PX_PER_MM):
    """The frame from a scan, its marked rows across, its DIPs (each naming
    its pin 1 row and its pin N row) and its marked outline. Returns (the
    result as frame.json holds it, the Frame, what the overlays need)."""
    L_raw = common.oklab(rgb)[0]
    rows = measure_rows(rgb, L_raw, rows, px_per_mm)
    fps = measure_footprints(rows, footprints)
    xrows = [r for r in rows if r['d'] is not None]
    yfps = [f for f in fps if f['d'] is not None]
    sx, sy = fit_scale(xrows, yfps)
    hx, xsum = x_summary(xrows, yfps)
    hy, ysum = y_summary(xrows, yfps)
    _, xcent = x_summary([dict(r, d=r['dCentroids']) for r in rows if r['dCentroids'] is not None], yfps)
    _, ycent = y_summary(xrows, [f for f in fps if f['dCentroids'] is not None], 'dCentroids')
    _, ypin1 = y_summary(xrows, [f for f in fps if f['dPin1'] is not None], 'dPin1')
    theta, straight, skews = fit_turn(rows, yfps, sx, sy)

    Y = linear_light(rgb)
    corners, pts, edges, notches = fit_outline(Y, outline_marks, sx, sy, theta)
    o = corners[0].copy()
    origin_px = from_turned([o], sx, sy, theta)[0]
    frame = Frame(sx, sy, theta, origin_px)
    corners = corners - o
    outline = np.vstack([pts - o, (pts - o)[:1]])
    for e in edges:
        ou, ov = _uv(e['axis'], o)
        e.update(a=e['a'] - ov, um=e['um'] - ou, u0=e['u0'] - ou, u1=e['u1'] - ou, v=e['v'] - ov)
        e['slopeDeg'] = math.degrees(math.atan(e['b']))
        e['notches'] = [m['id'] for m in e['notches']]
        for k in ('forward', 'out'):
            e.pop(k)
    for n in notches:
        n['x'] -= o[0]
        n['y'] -= o[1]
    width, depth = (corners.max(0) - corners.min(0)).tolist()

    L, C = lid_and_chroma(rgb)
    filled, _, inside = board_mask(L, C)
    holes, seen, crescent = find_holes(Y, inside, sx, sy, theta)
    for h in holes:
        h['x'] -= o[0]
        h['y'] -= o[1]
    for h in seen:
        h['centroidMm'] = [h['centroidMm'][0] - o[0], h['centroidMm'][1] - o[1]]
        if 'x' in h:
            h['x'] -= o[0]
            h['y'] -= o[1]

    result = {
        'pxPerMm': {'x': sx, 'y': sy, 'stated': NOMINAL_PX_PER_MM},
        'ratio': sx / sy,
        'ratioPct': 100 * abs(sx / sy - 1),
        'statedDpi': STATED_DPI,
        'origin': origin_px.tolist(),
        'rotationDeg': math.degrees(theta),
        'heldOutX': {
            'what': 'Rows of pins on 2.54 mm across the board (the DIPs\' and the expansion header\'s), first drill to last, each held out of the '
                    'scale fit in turn; scaledErrMm is the error x 48.26 / the row\'s length; rows of at least 30 mm are scored (the plan). The '
                    'edge fingers are not among them: they are on 2.50 mm (task 0)',
            'rows': hx,
            'n': xsum['n'],
            'scaledErrMm': xsum['scaledErrMm'],
            'largestRow': xsum['largestRow'],
            'everyRowScaledErrMm': abs_stats([r['scaledErrMm'] for r in hx]),
        },
        'heldOutY': {
            'what': 'Each DIP\'s row spacing (15.24 or 7.62 mm) on drill centres, fitted on their top rims: the mean of every pin\'s pair across the '
                    'footprint; each footprint held out of the scale fit in turn, its error a per cent of its spacing',
            'footprints': hy,
            'errPct': ysum['errPct'],
            'errPct600': ysum['errPct600'],
            'errPct300': ysum['errPct300'],
            'refusedPairs': {f['ref']: f['refusedPairs'] for f in fps if f['refusedPairs']},
        },
        'xFromPadCentroids': dict(xcent, what='The same rows from the solder\'s centroids, first pad to last (task 0\'s way): recorded, not judged'),
        'yFromPadCentroids': dict(ycent, what='The same spacings from the pads\' solder centroids, not the drills: recorded, not judged'),
        'yFromPin1AndPinN': dict(ypin1, what='Task 0\'s way: pin 1 and pin N alone, on drill centres: recorded, not judged'),
        'straightness': {
            'what': 'Each long row\'s drills (10 or more) against a straight line through them, in millimetres; recorded, not judged: the rows at '
                    'or over 0.05 mm are named',
            'limitMm': STRAIGHT_MM,
            'rows': straight,
            'rmsMm': abs_stats([r['rmsMm'] for r in straight]),
            'bowMm': abs_stats([r['bowMm'] for r in straight]),
            'over': [r['row'] for r in straight if r['rmsMm'] >= STRAIGHT_MM],
        },
        'skew': {'what': 'How far each DIP\'s spacing, on drills, is from square to the rows; recorded', 'footprints': skews,
                 'medianDeg': float(np.median([k['skewDeg'] for k in skews])) if skews else None},
        'rows': [{'row': r['id'], 'pitches': r['pitches'], 'drillsKept': len(r['found']), 'padsFound': sum(p is not None for p in r['pads']),
                  'refused': r['refused'], 'usable': r['d'] is not None} for r in rows],
        'board': {'widthMm': float(width), 'depthMm': float(depth)},
        'corners': corners.tolist(),
        'edges': edges,
        'notches': notches,
        'outline': outline.tolist(),
        'holes': holes,
        'holeCrescentDeg': crescent,
        'holeCandidates': seen,
    }
    return result, frame, {'rows': rows, 'filled': filled, 'fps': fps}


# --- I1-front ---------------------------------------------------------------------

def marks():
    with open(common.DATA / 'marks.json', encoding='utf8') as f:
        return json.load(f)


def rows_and_footprints(m):
    """The rows across (task 0's marks) and the DIPs: each DIP's row from pin
    1 and the row from pin N, which marks.json names "<ref> pins 1 to <N/2>"
    and "<ref> pins <N> to <N/2 + 1>"."""
    rows = [{k: r[k] for k in ('id', 'part', 'first', 'last', 'pitches', 'pitchMm')} for r in m['scale']['rows']]
    ids = {r['id'] for r in rows}
    fps = []
    for d in m['scale']['drills']:
        n = d['pins']
        a, b = f"{d['part']} pins 1 to {n // 2}", f"{d['part']} pins {n} to {n // 2 + 1}"
        assert a in ids and b in ids, (a, b)
        fps.append({'ref': d['part'], 'pins': n, 'spacingMm': d['rowSpacingMm'], 'pin1Row': a, 'pinNRow': b})
    return rows, fps


def outline_marks(m):
    """The outline's marks as measure() takes them: the corners in order,
    the edges with tabs' remains, and each notch's edge and deepest point."""
    o = m['outline']
    corners = sorted(o['corners'], key=lambda c: c['corner'])
    assert [c['corner'] for c in corners] == list(range(len(corners)))
    return {'corners': [c['at'] for c in corners], 'tabs': o['tabs'],
            'notches': [{'id': n['id'], 'edge': n['edge'], 'deepest': n['deepest']} for n in o['notches']]}


def judge(f):
    """The plan's scale rows, the same rules as nes-spike-verdicts.mjs:
    'pass', 'between pass and stop' or 'STOP'."""
    def v(ok, stop):
        return 'STOP' if stop else ('pass' if ok else 'between pass and stop')
    x, y = f['heldOutX'], f['heldOutY']
    ratio_pct = 100 * abs(f['pxPerMm']['x'] / f['pxPerMm']['y'] - 1)
    return [
        {'check': 'scale x', 'verdict': v(x['scaledErrMm']['median'] <= PASS['xMedian'] and x['n'] >= MIN['xRows'], x['scaledErrMm']['median'] > STOP['xMedian'])},
        {'check': 'scale y', 'verdict': v(y['errPct']['median'] <= PASS['yMedianPct'] and len(y['footprints']) >= MIN['yFootprints'],
                                          y['errPct']['median'] > STOP['yMedianPct'])},
        {'check': 'x against y', 'verdict': v(True, ratio_pct > STOP['ratioPct'])},
    ]


def finger_pitch(L, f, frame):
    """The edge fingers' pitch through this frame: the gaps between them (the
    lowest light between two fingers, to a tenth of a pixel, on the light
    averaged down the fingers) and a straight line through their places along
    x. Recorded: the fingers are not in the scale."""
    from scipy.signal import find_peaks
    band = f['band']
    prof = L[band[0]:band[1]].mean(0)
    lo, hi = int(f['first'][0]), int(f['last'][0]) + 1
    pk, _ = find_peaks(-prof[lo:hi], distance=15, prominence=0.08)
    xs = []
    for i in pk + lo:
        a, b, c = prof[i - 1], prof[i], prof[i + 1]
        xs.append(i + 0.5 * (a - c) / (a - 2 * b + c))
    q = frame.to_board(np.c_[xs, np.full(len(xs), (band[0] + band[1]) / 2)])
    k = np.arange(len(q))
    slope = float(np.polyfit(k, q[:, 0], 1)[0])
    return {'gaps': len(xs), 'gapPitchMm': slope,
            'what': 'The gaps between P1\'s 36 fingers, their pitch along x through this frame: recorded, not used (task 0 found 2.50 mm, not 2.54)'}


def kicad_holes(path):
    """The KiCad redrawing's round holes in its board outline (Edge.Cuts
    circles), from its outline's top left corner: compared, never drawn."""
    from pathlib import Path
    root = common._sexpr(Path(path).read_text(encoding='utf8'))
    x0, y0, _, _ = common.kicad_outline_box(path)
    out = []
    for g in root[1:]:
        if isinstance(g, list) and g and g[0] == 'gr_circle':
            layer = common._child(g, 'layer')
            if layer and layer[1] == 'Edge.Cuts':
                c, e = common._child(g, 'center'), common._child(g, 'end')
                cx, cy = float(c[1]), float(c[2])
                out.append({'x': cx - x0, 'y': cy - y0, 'd': 2 * math.hypot(float(e[1]) - cx, float(e[2]) - cy)})
    return out


def rectify(rgb, frame, width_mm, depth_mm, ppm=RECTIFIED_PX_PER_MM, margin=RECTIFIED_MARGIN_MM):
    """I1-front resampled onto the board frame: pixel (u, v)'s centre is board
    millimetre ((u + 0.5) / ppm - margin, (v + 0.5) / ppm - margin)."""
    import cv2
    W = int(math.ceil((width_mm + 2 * margin) * ppm))
    H = int(math.ceil((depth_mm + 2 * margin) * ppm))
    u, v = np.meshgrid((np.arange(W) + 0.5) / ppm - margin, (np.arange(H) + 0.5) / ppm - margin)
    p = frame.to_px(np.c_[u.ravel(), v.ravel()])
    return cv2.remap(rgb, p[:, 0].reshape(H, W).astype(np.float32), p[:, 1].reshape(H, W).astype(np.float32),
                     cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(255, 255, 255))


def overlays(rgb, result, frame, extra, m):
    """Pictures to look at, in out/: the rectified copy; the same at twice the
    size with the outline (magenta), the notches' circles (orange), the holes
    (blue, the crescent's side marked), the rows' drills (cyan) and each
    DIP's pin pairs (yellow) drawn on it; and a sheet of close-ups of every
    corner, notch and hole, and of every row's two end drills on the scan."""
    import cv2
    from PIL import Image, ImageDraw
    common.OUT.mkdir(exist_ok=True)
    ppm, mg = RECTIFIED_PX_PER_MM, RECTIFIED_MARGIN_MM
    w, h = result['board']['widthMm'], result['board']['depthMm']
    rect = rectify(rgb, frame, w, h)
    Image.fromarray(rect).save(common.OUT / 'rectified-12.jpg', quality=92)
    big = cv2.resize(rect, (rect.shape[1] * 4, rect.shape[0] * 4), interpolation=cv2.INTER_NEAREST)

    def P(x, y):
        return (int(round((x + mg) * ppm * 4 * 4)), int(round((y + mg) * ppm * 4 * 4)))
    pts = np.array([P(x, y) for x, y in result['outline']], np.int32)
    cv2.polylines(big, [pts.reshape(-1, 1, 2)], True, (255, 0, 255), 2, cv2.LINE_AA, 2)
    for n in result['notches']:
        cv2.circle(big, P(n['x'], n['y']), int(round(n['r'] * ppm * 16)), (255, 140, 0), 1, cv2.LINE_AA, 2)
    for hole in result['holes']:
        cv2.circle(big, P(hole['x'], hole['y']), int(round(hole['d'] / 2 * ppm * 16)), (0, 80, 255), 2, cv2.LINE_AA, 2)
        a = math.radians(hole['crescentDeg'])
        cv2.drawMarker(big, P(hole['x'] + hole['d'] / 2 * math.cos(a), hole['y'] + hole['d'] / 2 * math.sin(a)), (255, 120, 0),
                       cv2.MARKER_TILTED_CROSS, 16, 2)
    for r in extra['rows']:
        for _, x, y in r['found']:
            X, Yb = frame.to_board([(x, y)])[0]
            cv2.circle(big, P(X, Yb), 4 * 4 * 3, (0, 255, 255), 1, cv2.LINE_AA, 2)
    by = {r['id']: r for r in extra['rows']}
    for f in extra['fps']:
        for k, v in f['pairVectors']:
            a = by[f['pin1Row']]['drills'][k]
            X, Yb = frame.to_board([a])[0]
            X2, Y2 = frame.to_board([np.asarray(a) + v])[0]
            cv2.line(big, P(X, Yb), P(X2, Y2), (255, 255, 0), 1, cv2.LINE_AA, 2)
    small = cv2.resize(big, (rect.shape[1] * 2, rect.shape[0] * 2), interpolation=cv2.INTER_AREA)
    Image.fromarray(small).save(common.OUT / 'frame-overlay.jpg', quality=90)

    # Close-ups: 8 mm across, from the overlay at 24 px/mm, shown at 2x.
    ov = Image.fromarray(small)
    s2 = ppm * 2
    spots = [(f'corner {i}', x, y) for i, (x, y) in enumerate(result['corners'])]
    spots += [(f'notch {n["id"]}', n['x'], n['y']) for n in result['notches']]
    spots += [(f'hole {i}', hh['x'], hh['y']) for i, hh in enumerate(result['holes'])]
    tiles = []
    for name, x, y in spots:
        c = ov.crop((int((x - 4 + mg) * s2), int((y - 4 + mg) * s2), int((x + 4 + mg) * s2), int((y + 4 + mg) * s2))).resize((384, 384), Image.NEAREST)
        ImageDraw.Draw(c).text((4, 4), name, fill=(255, 0, 0))
        tiles.append(c)
    # each row's end drills on the scan itself, 3 mm across at 8x
    for r in extra['rows']:
        for k in (0, r['pitches']):
            p = r['drills'][k] or r['pads'][k] or r['first']
            x0, y0 = int(round(p[0])) - 18, int(round(p[1])) - 18
            c = Image.fromarray(rgb[max(y0, 0):y0 + 36, max(x0, 0):x0 + 36]).resize((288, 288), Image.NEAREST)
            d = ImageDraw.Draw(c)
            if r['drills'][k] is not None:
                cx, cy = (r['drills'][k][0] - x0 + 0.5) * 8, (r['drills'][k][1] - y0 + 0.5) * 8
                d.line([(cx - 10, cy), (cx + 10, cy)], fill=(0, 255, 255))
                d.line([(cx, cy - 10), (cx, cy + 10)], fill=(0, 255, 255))
            d.text((4, 4), f'{r["id"]} pin {k}', fill=(255, 0, 0))
            tiles.append(c.resize((384, 384), Image.NEAREST))
    cols = 8
    sheet = Image.new('RGB', (cols * 384, ((len(tiles) + cols - 1) // cols) * 384), 'white')
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % cols) * 384, (i // cols) * 384))
    sheet.save(common.OUT / 'frame-close-ups.jpg', quality=88)


def main():
    rgb = common.read_rgb('I1-front')
    m = marks()
    rows, fps = rows_and_footprints(m)
    result, frame, extra = measure(rgb, rows, fps, outline_marks(m))
    result['verdicts'] = judge(result)
    L = common.oklab(rgb)[0]
    result['fingers'] = finger_pitch(L, m['scale']['fingers'], frame)
    i2 = common.original('I2')
    x0, y0, x1, y1 = common.kicad_outline_box(i2)
    result['kicad'] = {'widthMm': x1 - x0, 'depthMm': y1 - y0,
                       'what': 'The KiCad redrawing\'s outline box (I2), compared and never drawn'}
    kh = kicad_holes(i2)
    offs = []
    for h in result['holes']:
        near = min(kh, key=lambda k: math.hypot(k['x'] - h['x'], k['y'] - h['y']))
        off = math.hypot(near['x'] - h['x'], near['y'] - h['y'])
        h['kicad'] = {'offMm': off, 'dxMm': near['x'] - h['x'], 'dyMm': near['y'] - h['y'], 'd': near['d']} if off < 3 else None
        if off < 3:
            offs.append((near['x'] - h['x'], near['y'] - h['y']))
    result['kicadHoles'] = {'n': len(kh), 'matched': len(offs),
                            'medianOffsetMm': np.median(offs, 0).tolist() if offs else None,
                            'what': 'The redrawing\'s round holes in its outline; each hole above names the nearest one within 3 mm, in the two '
                                    'outlines\' own top left frames, and the median offset is the two frames\' difference: recorded, not judged'}
    result['rectified'] = {'pxPerMm': RECTIFIED_PX_PER_MM, 'marginMm': RECTIFIED_MARGIN_MM, 'file': 'out/rectified-12.jpg',
                           'what': 'I1-front resampled onto the board frame: pixel (u, v)\'s centre is board millimetre ((u + 0.5) / 12 - 2, (v + 0.5) / 12 - 2). '
                                   'Made by board_frame.py in out/, which is git-ignored: the scan states no licence, so no copy of it is committed'}
    result['about'] = ('Task 2 of the NES models plan: the board frame on scan I1-front. A board millimetre X (from the board\'s top left corner as the '
                       'scan lies, x to the right, y down towards the edge fingers) is at I1-front pixel origin + diag(pxPerMm.x, pxPerMm.y) '
                       'R(rotationDeg) X. The outline, holes, notches, corners and edges are in board millimetres. Written by board_frame.py; every '
                       'figure here is a measurement made on the date of the journal entry that quotes it.')
    for v in result['verdicts']:
        print(f"{v['check']}: {v['verdict']}")
    x, y = result['heldOutX'], result['heldOutY']
    print(f"x {result['pxPerMm']['x']:.4f}, y {result['pxPerMm']['y']:.4f} px/mm, ratio {result['ratio']:.5f}; "
          f"x median {x['scaledErrMm']['median']:.3f} mm over {x['n']} rows, largest {x['scaledErrMm']['max']:.3f} ({x['largestRow']}); "
          f"y median {y['errPct']['median']:.3f} %, max {y['errPct']['max']:.3f} % over {len(y['footprints'])} footprints")
    xc = result['xFromPadCentroids']
    print(f"recorded: x from the solder's centroids median {xc['scaledErrMm']['median']:.3f} mm, largest {xc['scaledErrMm']['max']:.3f}; "
          f"y from centroids median {result['yFromPadCentroids']['errPct']['median']:.3f} %")
    print(f"board {result['board']['widthMm']:.2f} x {result['board']['depthMm']:.2f} mm (KiCad {result['kicad']['widthMm']:.3f} x "
          f"{result['kicad']['depthMm']:.3f}), turned {result['rotationDeg']:.4f} degrees, {len(result['holes'])} holes, "
          f"{len(result['notches'])} notches, outline {len(result['outline']) - 1} points; fingers' pitch {result['fingers']['gapPitchMm']:.4f} mm")
    common.write_data('frame.json', result)
    overlays(rgb, result, frame, extra, m)
    return 3 if any(v['verdict'] == 'STOP' for v in result['verdicts']) else 0


if __name__ == '__main__':
    sys.exit(main())
