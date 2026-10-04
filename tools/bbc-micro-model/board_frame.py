"""Task 2: the board's frame, from the bare Issue 7 scan of the component
side (I1): its x and y scales, its turn on the scanner glass, the board's
outline and its holes. Every later board script reads I1 through this frame.

    BBC_MODEL_INPUTS=<folder> python board_frame.py

The frame. A point X on the board, in millimetres from the board's left rear
corner (x to the right, y towards the front), is at pixel

    p = origin + S R(theta) X,    S = diag(sx, sy),

where sx and sy are the scanner's pixels per millimetre along its own x and
y (a flatbed scanner's two directions are made differently, so they are
measured apart) and theta is the board's turn on the glass. Because S acts
after the turn, a row's length in millimetres, |S^-1 d| for a pixel vector d,
does not depend on theta at all.

1. Scale. Rows of pads on the 2.54 mm pitch, each marked by hand at its first
   and last pad (data/marks.json) and each pad moved to its solder blob's
   centroid (common.refine_to_pad, refused beyond 0.6 mm). Rows down the
   board (y): the DIP footprints' rows, pin 1 to pin N/2, from task 0's
   marks. Rows across the board (x): the connector rows on the 0.1 inch pitch
   and the rows of the DIPs that lie across. Never a DIP's row spacing (task 0
   found its solder centroids about 1 per cent wide). sx and sy are fitted
   together to make every row's length, first pad to last, its pitches x
   2.54 mm, by least squares in millimetres; the rows across set sx and the
   rows down set sy.

   Held out. y: the footprints are in two halves set in marks.json before any
   fit; each half's rows down are measured through the scale fitted to every
   other row, and the 40-pin rows (48.26 mm) are judged. x: each row across is
   left out of the fit in turn and its length measured through the rest; its
   error is scaled to a 48.26 mm row (error x 48.26 / its length) and judged.

2. The turn. Every pad of every long row (10 pads or more) is found in turn
   along it; a straight line through each row's pads, in millimetres. One
   turn for the whole board, fitted to every long row's pads at once with an
   offset per row (rows down turned a quarter turn first). Each row's
   distance from its own straight line is recorded: under 0.05 mm, or the
   scan is not flat.

3. The outline. The scanner lid is pale and grey; the board is green, or
   yellow print, or dark. The board is the largest region that is not lid,
   with everything inside it filled. Its edge, in millimetres turned to the
   board's axes, is cut into runs across and runs down (by the direction of
   the edge over 1 mm either side); a line is fitted to each run, robustly,
   leaving out its ends where the corners are rounded, and the corners are
   where neighbouring runs' lines meet. Notches and slots are runs like any
   other, so they are kept. The origin is where the left edge's line meets
   the rear edge's.

4. The holes. A hole shows the lid through the board: a pale, grey, smooth
   region inside the board, roughly round and at least 1.5 mm across (a
   tinned pad is pale and grey too, but its solder is rough). A circle is
   fitted to where colour starts round it, found along rays from its middle.
   On a made-up scan that is the hole's edge. On I1 it is the edge of the lid
   seen through the hole: the hole's wall shows as a grey crescent on its
   rear side (the scanner looks at a slight slant), and some holes have a
   grey ring round them, and the circle stops at both. So on I1 a hole's
   diameter reads small and its centre may sit towards the front by part of
   the crescent; journal, 4 October 2026.

Writes data/frame.json, a copy of I1 rectified to the board frame at 16
pixels per millimetre in out/ (git-ignored, never committed: I1 states no
licence), and overlays to look at in out/. Exits 3 when a figure crosses one
of the plan's STOP thresholds.
"""
import json
import math
import sys

import numpy as np

import common

PITCH = 2.54
FORTY_PIN_MM = 48.26
NOMINAL_PX_PER_MM = 15.75     # only to size search windows; the scale is measured
# The plan's thresholds, as set before the measurements (Global Constraints).
PASS = {'yMedian': 0.10, 'yMax': 0.25, 'xMedian': 0.10, 'xMax': 0.25}
STOP = {'yMedian': 0.20, 'yMax': 0.50, 'ratioPct': 1.5, 'xMedian': 0.20, 'xMax': 0.50}
STRAIGHT_MM = 0.05            # the plan: a row's residual from a straight line
LONG_ROW_PADS = 10            # a long row, for the turn and the straightness
MIN_LINE_PADS = 8             # pads found, for a row's line to be fitted
RECTIFIED_PX_PER_MM = 16
RECTIFIED_MARGIN_MM = 2.0

# The scanner lid: pale and grey (task 0's crops: L 0.88 to 0.95, chroma
# under 0.022); the board's lacquer is green (chroma 0.05 and more) or dark.
LID_MIN_L, LID_MAX_C = 0.78, 0.025
# A hole: the lid seen through it, round, and smooth where solder is rough.
# The lid seen through a hole is cut on one side by the hole's far wall, seen
# at a slant, so it is round only roughly (on I1 up to 1.64 times as long as
# wide). Its texture is the spread (standard deviation) of OKLab L over the
# region less 3 pixels all round, unsmoothed: on I1 every hole is 0.017 to
# 0.027 and every tinned pad over 1.8 mm is 0.057 or more (read off I1 before
# any hole was fitted; the limit sits between them).
HOLE_MIN_D_MM, HOLE_MAX_D_MM = 1.5, 6.0
HOLE_MAX_ASPECT, HOLE_MIN_FILL = 2.0, 0.60
HOLE_MAX_TEXTURE = 0.04
RIM_C = 0.03                  # chroma where the board's colour starts at a hole's rim
EDGE_WINDOW_MM = 1.0          # the edge's direction is judged over this either side
MIN_RUN_MM = 0.8              # an edge run shorter than this is noise
TRIM_MM = 0.8                 # left off each end of a run (the corners are rounded)


# --- the frame ------------------------------------------------------------------

def rotation(theta):
    c, s = math.cos(theta), math.sin(theta)
    return np.array([[c, -s], [s, c]])


class Frame:
    """Board millimetres to I1's pixels and back (see the module's docstring)."""

    def __init__(self, sx, sy, theta, origin):
        self.sx, self.sy, self.theta = float(sx), float(sy), float(theta)
        self.origin = np.asarray(origin, float)
        self.M = np.diag([self.sx, self.sy]) @ rotation(self.theta)
        self.Minv = np.linalg.inv(self.M)

    def to_px(self, mm):
        return np.asarray(mm, float).reshape(-1, 2) @ self.M.T + self.origin

    def to_board(self, px):
        return (np.asarray(px, float).reshape(-1, 2) - self.origin) @ self.Minv.T


def to_turned(px, sx, sy, theta):
    """Pixels to millimetres on the board's axes, before the origin is known."""
    q = np.asarray(px, float).reshape(-1, 2) / np.array([sx, sy])
    return q @ rotation(-theta).T


def from_turned(q, sx, sy, theta):
    return (np.asarray(q, float).reshape(-1, 2) @ rotation(theta).T) * np.array([sx, sy])


# --- 1. scale -------------------------------------------------------------------

def length_mm(d, sx, sy):
    """A pixel vector's length in millimetres (whatever the board's turn)."""
    return float(math.hypot(d[0] / sx, d[1] / sy))


def row_pads(rgb, first, last, pitches, px_per_mm=NOMINAL_PX_PER_MM):
    """Every pad of a row, k = 0 (the first) to `pitches` (the last): each
    looked for where the row's marks put it and moved to its blob's centroid,
    or None where refine_to_pad refuses it."""
    a, b = np.asarray(first, float), np.asarray(last, float)
    out = []
    for k in range(pitches + 1):
        p = a + (b - a) * k / pitches
        r = common.refine_to_pad(rgb, p[0], p[1], px_per_mm)
        out.append(None if r is None else (r[0], r[1]))
    return out


def measure_rows(rgb, rows, px_per_mm=NOMINAL_PX_PER_MM):
    """Each row with its pads found: `pads` (k, x, y) for every pad found,
    `d` the vector from the first pad's centroid to the last's (None when
    either is refused) and `trueMm` its length on the pitch."""
    out = []
    for r in rows:
        pads = row_pads(rgb, r['first'], r['last'], r['pitches'], px_per_mm)
        found = [(k, p[0], p[1]) for k, p in enumerate(pads) if p is not None]
        d = None if pads[0] is None or pads[-1] is None else np.subtract(pads[-1], pads[0])
        out.append(dict(r, pads=found, d=d, trueMm=r['pitches'] * PITCH,
                        refused=[k for k, p in enumerate(pads) if p is None]))
    return out


def fit_scale(rows):
    """sx and sy that make every row's length, |S^-1 d|, its true length, by
    least squares in millimetres. Needs rows both across and down."""
    from scipy.optimize import least_squares
    d = np.array([r['d'] for r in rows], float)
    L = np.array([r['trueMm'] for r in rows], float)
    if not any(r['axis'] == 'x' for r in rows) or not any(r['axis'] == 'y' for r in rows):
        raise ValueError('the scale needs rows across (x) and down (y)')

    def resid(s):
        return np.hypot(d[:, 0] / s[0], d[:, 1] / s[1]) - L
    r = least_squares(resid, [NOMINAL_PX_PER_MM, NOMINAL_PX_PER_MM], method='lm', xtol=1e-15, ftol=1e-15, gtol=1e-15)
    return float(r.x[0]), float(r.x[1])


def held_out_x(rows):
    """Each row across left out of the fit in turn, measured through the scale
    fitted to every other row; its error scaled to a 48.26 mm row."""
    out = []
    for i, r in enumerate(rows):
        if r['axis'] != 'x':
            continue
        sx, sy = fit_scale([o for j, o in enumerate(rows) if j != i])
        m = length_mm(r['d'], sx, sy)
        err = m - r['trueMm']
        out.append({'row': r['id'], 'kind': r.get('kind'), 'pitches': r['pitches'], 'lengthMm': r['trueMm'],
                    'measuredMm': m, 'errMm': err, 'scaledErrMm': err * FORTY_PIN_MM / r['trueMm']})
    return out


def held_out_y(rows):
    """The rows down, a half at a time (the folds set in marks.json), each
    half measured through the scale fitted to every other row."""
    out = []
    folds = sorted({r['fold'] for r in rows if r['axis'] == 'y' and r.get('fold')})
    for f in folds:
        held = [r for r in rows if r['axis'] == 'y' and r.get('fold') == f]
        sx, sy = fit_scale([r for r in rows if not (r['axis'] == 'y' and r.get('fold') == f)])
        for r in held:
            m = length_mm(r['d'], sx, sy)
            out.append({'row': r['id'], 'footprint': r.get('footprint'), 'pins': r.get('pins'), 'fold': f,
                        'lengthMm': r['trueMm'], 'measuredMm': m, 'errMm': m - r['trueMm']})
    return out


def abs_stats(values):
    e = np.abs(np.asarray(values, float))
    if not len(e):
        return {'n': 0}
    return dict(common.stats(e), n=len(e))


# --- 2. the turn and the straightness ------------------------------------------------

def fit_turn(rows, sx, sy):
    """One turn for the board from every long row's pads (an offset per row),
    and each row's own straight line: its angle and its pads' distances from
    it, in millimetres."""
    lines, pooled = [], np.zeros((2, 2))
    per_axis = {'x': np.zeros((2, 2)), 'y': np.zeros((2, 2))}
    for r in rows:
        if r['pitches'] + 1 < LONG_ROW_PADS or len(r['pads']) < MIN_LINE_PADS:
            continue
        q = np.array([p[1:] for p in r['pads']], float) / np.array([sx, sy])
        c = q - q.mean(0)
        if r['axis'] == 'y':           # a quarter turn, so the row runs along x
            c = np.c_[c[:, 1], -c[:, 0]]
        scatter = c.T @ c
        pooled += scatter
        per_axis[r['axis']] += scatter
        w, v = np.linalg.eigh(scatter)
        u = v[:, 1] * np.sign(v[0, 1] or 1)
        res = c @ np.array([-u[1], u[0]])
        along = c @ u
        # A bent scan bows a row; a ragged pad only scatters it. The bow is the
        # sagitta of a parabola through the residuals, over the row's length.
        half = (along.max() - along.min()) / 2
        bow = float(np.polyfit(along, res, 2)[0] * half * half)
        lines.append({'row': r['id'], 'axis': r['axis'], 'pads': len(r['pads']), 'of': r['pitches'] + 1,
                      'angleDeg': math.degrees(math.atan2(u[1], u[0])),
                      'rmsMm': float(np.sqrt((res ** 2).mean())), 'maxMm': float(np.abs(res).max()), 'bowMm': bow})

    def angle(s):
        w, v = np.linalg.eigh(s)
        u = v[:, 1] * np.sign(v[0, 1] or 1)
        return math.atan2(u[1], u[0])
    theta = angle(pooled)
    return theta, {
        'rows': lines,
        'skewDeg': math.degrees(angle(per_axis['x']) - angle(per_axis['y'])) if per_axis['x'].any() and per_axis['y'].any() else None,
    }


# --- 3. the board and its outline --------------------------------------------------

def lid_and_chroma(rgb):
    import cv2
    L, A, B = common.oklab(rgb)
    C = np.hypot(A, B).astype(np.float32)
    L = cv2.medianBlur(L.astype(np.float32), 5)
    C = cv2.medianBlur(C, 5)
    return L, C


def board_mask(rgb, L=None, C=None):
    """The board: the largest region that is not the lid, everything inside it
    filled. Returns the filled mask, the labels of the regions that are not
    board, and the ones of those inside the board (each a dict: label, stats,
    centroid): holes, and tinned pads, which the holes are picked from."""
    import cv2
    if L is None:
        L, C = lid_and_chroma(rgb)
    lid = (L > LID_MIN_L) & (C < LID_MAX_C)
    n, lab, st, _ = cv2.connectedComponentsWithStats((~lid).astype(np.uint8), 4)
    big = 1 + int(np.argmax(st[1:, 4]))
    board = (lab == big).astype(np.uint8)
    n, lab, st, cen = cv2.connectedComponentsWithStats(1 - board, 4)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]])).tolist())
    inside = [i for i in range(1, n) if i not in border]
    filled = (board | np.isin(lab, inside)).astype(np.uint8)
    return filled, lab, [{'label': i, 'stats': st[i].tolist(), 'centroid': cen[i].tolist()} for i in inside]


def _runs(cls):
    """Circular runs of equal values: [(value, start, length)], start at a change."""
    n = len(cls)
    change = np.nonzero(cls != np.roll(cls, 1))[0]
    if not len(change):
        return [(cls[0], 0, n)]
    out = []
    for i, s in enumerate(change):
        e = change[(i + 1) % len(change)]
        out.append((cls[s], int(s), int((e - s) % n or n)))
    return out


def _robust_line(u, v, keep_mm=0.05):
    """v = a + b (u - um), refitted three times without the points more than
    three robust standard deviations (and keep_mm) off."""
    um = float(u.mean())
    ok = np.ones(len(u), bool)
    for _ in range(4):
        b, a = np.polyfit(u[ok] - um, v[ok], 1)
        r = v - (a + b * (u - um))
        mad = 1.4826 * np.median(np.abs(r[ok]))
        ok = np.abs(r) <= max(3 * mad, keep_mm)
    rms = float(np.sqrt((r[ok] ** 2).mean()))
    return float(a), float(b), um, int(ok.sum()), rms


def linear_light(rgb):
    """Luminance in linear light (the scanner's blur mixes light, not the
    stored sRGB numbers), 0 to 1."""
    c = rgb.astype(np.float32) / 255
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return (0.2126 * c[..., 0] + 0.7152 * c[..., 1] + 0.0722 * c[..., 2]).astype(np.float32)


EDGE_PROFILE_MM = 1.0         # the light across an edge is looked at this far either side


def edge_offset(Y, a, b, um, u0, u1, v, out_sign, sx, sy, theta):
    """How far outward of the line v = a + b (u - um) the light crosses half
    way between the board's level and the lid's: the median of profiles
    across the edge every 0.25 mm from u0 to u1. None when the two levels are
    not told apart, or nothing crosses."""
    from scipy.ndimage import map_coordinates
    us = np.arange(u0, u1 + 1e-9, 0.25)
    t = np.arange(-EDGE_PROFILE_MM, EDGE_PROFILE_MM + 1e-9, 0.02)
    U, T = np.meshgrid(us, t, indexing='ij')
    line = a + b * (U - um) + out_sign * T
    q = np.c_[U.ravel(), line.ravel()] if v == 0 else np.c_[line.ravel(), U.ravel()]
    p = from_turned(q, sx, sy, theta)
    prof = np.median(map_coordinates(Y, [p[:, 1], p[:, 0]], order=1, mode='nearest').reshape(U.shape), 0)
    board, lid = np.median(prof[t <= -0.5]), np.median(prof[t >= 0.5])
    if abs(lid - board) < 0.1:
        return None
    half = (board + lid) / 2
    sign = np.sign(prof - half)
    cross = np.nonzero(sign[:-1] * sign[1:] < 0)[0]
    if not len(cross):
        return None
    j = cross[np.argmin(np.abs(t[cross]))]
    f = (half - prof[j]) / (prof[j + 1] - prof[j])
    return float(t[j] + f * (t[j + 1] - t[j]))


JOG_MM = (2.0, 0.5)          # a run under 2 mm long between two runs within 0.5 mm of one line is a jog


def _merge_jogs(cls, Q):
    """A short run between two runs of the other kind that lie on one line
    (within JOG_MM[1] of each other) is a jog in a straight edge, from dirt or
    a ragged bit of the edge, not a corner: it joins them. A slot or a notch
    is not merged: its floor's neighbours are its two walls, whose lines are
    a slot's width apart."""
    while True:
        runs = _runs(cls)
        if len(runs) < 3:
            return cls
        merged = False
        for i, (v, start, length) in enumerate(runs):
            q = Q[(start + np.arange(length)) % len(Q)]
            extent = np.ptp(q[:, 0] if v == 0 else q[:, 1])
            if extent >= JOG_MM[0]:
                continue
            p, n = runs[i - 1], runs[(i + 1) % len(runs)]
            if p[0] != n[0] or p[0] == v:
                continue
            across = 1 if p[0] == 0 else 0           # a run across sits at a y; a run down at an x
            qp = Q[(p[1] + np.arange(p[2])) % len(Q)]
            qn = Q[(n[1] + np.arange(n[2])) % len(Q)]
            if abs(np.median(qp[:, across]) - np.median(qn[:, across])) < JOG_MM[1]:
                cls[(start + np.arange(length)) % len(cls)] = p[0]
                merged = True
                break
        if not merged:
            return cls


def fit_outline(filled, Y, sx, sy, theta):
    """The outline in millimetres on the board's axes (before the origin):
    its edge runs, each with its fitted line, and its corners, each where
    neighbouring runs' lines meet."""
    import cv2
    cs, _ = cv2.findContours(filled, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    P = max(cs, key=len)[:, 0, :].astype(float)
    Q = to_turned(P, sx, sy, theta)
    s = math.sqrt(sx * sy)
    w = max(3, int(round(EDGE_WINDOW_MM * s)))
    d = np.roll(Q, -w, 0) - np.roll(Q, w, 0)
    cls = (np.abs(d[:, 1]) > np.abs(d[:, 0])).astype(int)       # 0 across (H), 1 down (V)
    min_run = int(round(MIN_RUN_MM * s))
    while True:
        runs = _runs(cls)
        if len(runs) <= 1:
            break
        short = [r for r in runs if r[2] < min_run]
        if not short:
            break
        v, start, length = min(short, key=lambda r: r[2])
        idx = (start + np.arange(length)) % len(cls)
        cls[idx] = 1 - v
    cls = _merge_jogs(cls, Q)
    runs = _runs(cls)
    edges = []
    for v, start, length in runs:
        idx = (start + np.arange(length)) % len(Q)
        q = Q[idx]
        u, val = (q[:, 0], q[:, 1]) if v == 0 else (q[:, 1], q[:, 0])
        keep = (u > u.min() + TRIM_MM) & (u < u.max() - TRIM_MM)
        if keep.sum() < 5:
            mid = np.argsort(u)[len(u) // 3: 2 * len(u) // 3 + 1]
            keep = np.zeros(len(u), bool)
            keep[mid] = True
        a, b, um, used, rms = _robust_line(u[keep], val[keep])
        # The contour runs through the board's last pixels, a fraction of a
        # pixel inside the edge. The edge itself is where the light, averaged
        # along the run, is half way between the board's and the lid's.
        mid = q[len(q) // 2]
        normal = np.array([0.0, 1.0]) if v == 0 else np.array([1.0, 0.0])
        probe = from_turned([mid + normal * 3 / s, mid - normal * 3 / s], sx, sy, theta)
        inside = [filled[int(round(min(max(p[1], 0), filled.shape[0] - 1))), int(round(min(max(p[0], 0), filled.shape[1] - 1)))] for p in probe]
        out_sign = -1.0 if inside[0] and not inside[1] else 1.0
        shift = edge_offset(Y, a, b, um, u[keep].min(), u[keep].max(), v, out_sign, sx, sy, theta)
        a += out_sign * (shift if shift is not None else 0.5 / (sy if v == 0 else sx))
        edges.append({'axis': 'x' if v == 0 else 'y', 'a': a, 'b': b, 'um': um, 'from': float(u.min()), 'to': float(u.max()),
                      'points': used, 'rmsMm': rms, 'meanAt': float(val.mean()), 'halfLightMm': shift})
    corners = []
    for i, e in enumerate(edges):
        f = edges[(i + 1) % len(edges)]
        if e['axis'] == f['axis']:
            continue
        h, vline = (e, f) if e['axis'] == 'x' else (f, e)
        # h: y = a + b (x - um);  v: x = a + b (y - um)
        A = np.array([[-h['b'], 1.0], [1.0, -vline['b']]])
        rhs = np.array([h['a'] - h['b'] * h['um'], vline['a'] - vline['b'] * vline['um']])
        corners.append(np.linalg.solve(A, rhs))
    return np.array(corners), edges


def frame_edges(edges, corners):
    """The left, rear, right and front edges: the outermost long runs. The
    front is every run across within 1 mm of the frontmost (the front edge
    between its notch and slots)."""
    span = corners.max(0) - corners.min(0)
    down = [e for e in edges if e['axis'] == 'y' and e['to'] - e['from'] > 0.3 * span[1]]
    across = [e for e in edges if e['axis'] == 'x' and e['to'] - e['from'] > 0.3 * span[0]]
    left = min(down, key=lambda e: e['meanAt'])
    right = max(down, key=lambda e: e['meanAt'])
    rear = min(across, key=lambda e: e['meanAt'])
    front_at = max(e['meanAt'] for e in edges if e['axis'] == 'x')
    front = [e for e in edges if e['axis'] == 'x' and e['meanAt'] > front_at - 1.0]
    return left, rear, right, front


def at(line, u):
    return line['a'] + line['b'] * (u - line['um'])


# --- 4. the holes -------------------------------------------------------------------

def _circle(pts):
    """A circle through points, by the algebraic fit then least squares."""
    from scipy.optimize import least_squares
    x, y = pts[:, 0], pts[:, 1]
    A = np.c_[2 * x, 2 * y, np.ones(len(x))]
    cx, cy, c = np.linalg.lstsq(A, x * x + y * y, rcond=None)[0]
    r0 = math.sqrt(max(c + cx * cx + cy * cy, 1e-12))
    res = least_squares(lambda p: np.hypot(x - p[0], y - p[1]) - p[2], [cx, cy, r0], method='lm')
    return res.x


def rim(C, centre, r_min, r_max, sx, sy, theta, n_rays=120):
    """Where the board's colour starts, along rays from `centre` (pixels):
    the points in millimetres on the board's axes."""
    from scipy.ndimage import map_coordinates
    t = np.linspace(0, 2 * math.pi, n_rays, endpoint=False)
    r = np.arange(0, r_max, 0.25)
    xs = centre[0] + np.outer(np.cos(t), r)
    ys = centre[1] + np.outer(np.sin(t), r)
    v = map_coordinates(C, [ys.ravel(), xs.ravel()], order=1, mode='nearest').reshape(xs.shape)
    pts = []
    for i in range(n_rays):
        above = np.nonzero(v[i] >= RIM_C)[0]
        above = above[(above > 0) & (r[above] >= r_min)]
        if not len(above):
            continue
        j = above[0]
        f = (RIM_C - v[i, j - 1]) / max(v[i, j] - v[i, j - 1], 1e-9)
        rr = r[j - 1] + f * (r[j] - r[j - 1])
        pts.append((centre[0] + rr * math.cos(t[i]), centre[1] + rr * math.sin(t[i])))
    return to_turned(np.array(pts, float).reshape(-1, 2), sx, sy, theta)


def find_holes(L_raw, C, lab, inside, sx, sy, theta):
    """Holes through the board, on the board's axes in millimetres (before
    the origin): centre, diameter, and how well the rim fits a circle.
    Returns (holes, every candidate with why it was kept or refused)."""
    import cv2
    s = math.sqrt(sx * sy)
    holes, seen = [], []
    for c in inside:
        x0, y0, w, h, area = c['stats']
        d_eq = 2 * math.sqrt(area / math.pi) / s
        why, texture = None, None
        if not HOLE_MIN_D_MM <= d_eq <= HOLE_MAX_D_MM:
            continue              # most are tinned pads, too small to be a hole: not listed
        if max(w, h) > HOLE_MAX_ASPECT * min(w, h) or area / (math.pi / 4 * w * h) < HOLE_MIN_FILL:
            why = 'not round'
        else:
            m = (lab[y0:y0 + h, x0:x0 + w] == c['label']).astype(np.uint8)
            m = cv2.erode(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
            texture = float(L_raw[y0:y0 + h, x0:x0 + w][m > 0].std()) if m.any() else 1.0
            if texture > HOLE_MAX_TEXTURE:
                why = 'rough (solder)'
        entry = {'centroidPx': c['centroid'], 'dEqMm': d_eq, 'refused': why}
        if texture is not None:
            entry['texture'] = texture
        if why is None:
            centre = np.array(c['centroid'], float)
            r_max = 1.6 * (d_eq / 2) * s + 6
            for _ in range(2):
                pts = rim(C, centre, 0.4 * (d_eq / 2) * s, r_max, sx, sy, theta)
                if len(pts) < 40:
                    break
                ok = np.ones(len(pts), bool)
                for _ in range(3):
                    cx, cy, r = _circle(pts[ok])
                    res = np.hypot(pts[:, 0] - cx, pts[:, 1] - cy) - r
                    mad = 1.4826 * np.median(np.abs(res[ok]))
                    ok = np.abs(res) <= max(3 * mad, 0.05)
                centre = from_turned([(cx, cy)], sx, sy, theta)[0]
            if len(pts) < 40 or ok.sum() < 0.6 * len(pts):
                entry['refused'] = 'no clear rim'
            else:
                rms = float(np.sqrt((res[ok] ** 2).mean()))
                entry.update({'x': float(cx), 'y': float(cy), 'd': float(2 * r), 'rimPoints': int(ok.sum()), 'rimRmsMm': rms})
                holes.append({'x': float(cx), 'y': float(cy), 'd': float(2 * r), 'rimPoints': int(ok.sum()), 'rimRmsMm': rms})
        seen.append(entry)
    return holes, seen


# --- all of it ------------------------------------------------------------------------

def measure(rgb, rows, px_per_mm=NOMINAL_PX_PER_MM):
    """The frame from a scan and its marked rows. Returns (the result as
    frame.json holds it, the Frame, and what the overlays need)."""
    rows = measure_rows(rgb, rows, px_per_mm)
    scored = [r for r in rows if r['d'] is not None]
    sx, sy = fit_scale(scored)
    hx = held_out_x(scored)
    hy = held_out_y(scored)
    theta, turn = fit_turn(rows, sx, sy)
    L, C = lid_and_chroma(rgb)
    filled, lab, inside = board_mask(rgb, L, C)
    L_raw = common.oklab(rgb)[0]
    corners, edges = fit_outline(filled, linear_light(rgb), sx, sy, theta)
    left, rear, right, front = frame_edges(edges, corners)
    # the origin: the left edge's line meets the rear edge's
    A = np.array([[-rear['b'], 1.0], [1.0, -left['b']]])
    o = np.linalg.solve(A, [rear['a'] - rear['b'] * rear['um'], left['a'] - left['b'] * left['um']])
    origin_px = from_turned([o], sx, sy, theta)[0]
    frame = Frame(sx, sy, theta, origin_px)
    outline = corners - o
    start = int(np.argmin(np.hypot(*outline.T)))
    outline = np.roll(outline, -start, 0)
    outline = np.vstack([outline, outline[:1]])
    # the board's width and depth, between the edges' lines at the middle
    fu = np.concatenate([[e['from'], e['to']] for e in front])
    mid_y = (at(rear, (corners[:, 0].min() + corners[:, 0].max()) / 2) + max(e['meanAt'] for e in front)) / 2
    mid_x = (corners[:, 0].min() + corners[:, 0].max()) / 2
    front_y = float(np.average([at(e, mid_x) for e in front], weights=[e['points'] for e in front]))
    width = at(right, mid_y) - at(left, mid_y)
    depth = front_y - at(rear, mid_x)
    holes, seen = find_holes(L_raw, C, lab, inside, sx, sy, theta)
    for h in holes:
        h['x'] -= o[0]
        h['y'] -= o[1]
    for e in edges:
        e['meanAt'] -= o[1] if e['axis'] == 'x' else o[0]
        e['a'] -= o[1] if e['axis'] == 'x' else o[0]
        e['um'] -= o[0] if e['axis'] == 'x' else o[1]
        e['from'] -= o[0] if e['axis'] == 'x' else o[1]
        e['to'] -= o[0] if e['axis'] == 'x' else o[1]
    y40 = [r['errMm'] for r in hy if r['pins'] == 40]
    straight = turn['rows']
    result = {
        'pxPerMm': {'x': sx, 'y': sy},
        'ratio': sx / sy,
        'ratioPct': 100 * abs(sx / sy - 1),
        'origin': origin_px.tolist(),
        'rotationDeg': math.degrees(theta),
        'skewDeg': turn['skewDeg'],
        'heldOut': {
            'what': 'Rows down the board (y), pin 1 to pin N/2 of DIP footprints, a half of the footprints at a time held out of the scale fit; rowLengthErrMm is the 40-pin rows (48.26 mm), the ones the plan judges; allRowsErrMm is every row held out',
            'rowLengthErrMm': abs_stats(y40),
            'allRowsErrMm': abs_stats([r['errMm'] for r in hy]),
            'footprints': len({r['footprint'] for r in hy}),
            'rows': hy,
        },
        'heldOutX': {
            'what': 'Rows across the board (x), first pad to last, connector rows on the 0.1 inch pitch and the rows of DIPs that lie across, each held out of the scale fit in turn; scaledErrMm is the error x 48.26 / the row\'s length',
            'rows': hx,
            'scaledErrMm': abs_stats([r['scaledErrMm'] for r in hx]),
            'errMm': abs_stats([r['errMm'] for r in hx]),
        },
        'rows': [{'row': r['id'], 'axis': r['axis'], 'kind': r.get('kind'), 'pitches': r['pitches'], 'padsFound': len(r['pads']),
                  'refusedPads': r['refused'], 'scored': r['d'] is not None} for r in rows],
        'straightness': {
            'what': 'Each long row\'s pads (10 or more) against a straight line through them, in millimetres; the plan wants every row under 0.05 mm',
            'limitMm': STRAIGHT_MM,
            'rows': straight,
            'rmsMm': abs_stats([r['rmsMm'] for r in straight]),
            'bowMm': abs_stats([r['bowMm'] for r in straight]),
            'over': [r['row'] for r in straight if r['rmsMm'] >= STRAIGHT_MM],
            'flat': all(r['rmsMm'] < STRAIGHT_MM for r in straight),
        },
        'board': {'widthMm': width, 'depthMm': depth},
        'edges': edges,
        'outline': outline.tolist(),
        'holes': holes,
        'holeCandidates': seen,
        'frontEdgeSpanMm': [float(fu.min() - o[0]), float(fu.max() - o[0])],
    }
    return result, frame, {'rows': rows, 'filled': filled}


# --- I1 ---------------------------------------------------------------------------

def marks():
    with open(common.DATA / 'marks.json', encoding='utf8') as f:
        return json.load(f)


def rows_from_marks(m):
    """The rows: task 0's DIP footprints (their rows, never their row spacing)
    and the connector rows marked for this task."""
    rows = []
    for fp in m['scale']['footprints']:
        c, n = fp['corners'], fp['pins'] // 2 - 1
        pairs = (('tl', 'bl'), ('tr', 'br')) if fp['rows'] == 'vertical' else (('tl', 'tr'), ('bl', 'br'))
        for a, b in pairs:
            rows.append({'id': f"{fp['id']}.{a}-{b}", 'axis': 'y' if fp['rows'] == 'vertical' else 'x', 'kind': 'dip',
                         'first': c[a], 'last': c[b], 'pitches': n, 'footprint': fp['id'], 'fold': fp['fold'], 'pins': fp['pins']})
    for r in m['frame']['rowsAcross']:
        rows.append({'id': r['id'], 'axis': 'x', 'kind': 'connector', 'first': r['first'], 'last': r['last'], 'pitches': r['pitches']})
    return rows


def judge(f):
    y, x = f['heldOut']['rowLengthErrMm'], f['heldOutX']['scaledErrMm']
    lines = [
        ('scale y, 40-pin rows held out', y['median'] <= PASS['yMedian'] and y['max'] <= PASS['yMax'],
         y['median'] > STOP['yMedian'] or y['max'] > STOP['yMax'],
         f"median {y['median']:.3f} mm, max {y['max']:.3f} mm over {y['n']} rows"),
        ('scale, x against y', True, f['ratioPct'] > STOP['ratioPct'],
         f"x {f['pxPerMm']['x']:.4f}, y {f['pxPerMm']['y']:.4f} px/mm, ratio {f['ratio']:.5f} ({f['ratioPct']:.2f} per cent)"),
        ('scale x, rows across held out, scaled to 48.26 mm', x['median'] <= PASS['xMedian'] and x['max'] <= PASS['xMax'],
         x['median'] > STOP['xMedian'] or x['max'] > STOP['xMax'],
         f"median {x['median']:.3f} mm, max {x['max']:.3f} mm over {x['n']} rows"),
    ]
    out = []
    for name, ok, stop, text in lines:
        verdict = 'STOP' if stop else ('pass' if ok else 'between pass and stop')
        out.append({'measure': name, 'verdict': verdict, 'figures': text})
        print(f'{name}: {text}: {verdict}')
    return out


def rectify(rgb, frame, width_mm, depth_mm, ppm=RECTIFIED_PX_PER_MM, margin=RECTIFIED_MARGIN_MM):
    """I1 resampled onto the board frame: pixel (u, v)'s centre is board
    millimetre ((u + 0.5) / ppm - margin, (v + 0.5) / ppm - margin)."""
    import cv2
    W = int(math.ceil((width_mm + 2 * margin) * ppm))
    H = int(math.ceil((depth_mm + 2 * margin) * ppm))
    u, v = np.meshgrid((np.arange(W) + 0.5) / ppm - margin, (np.arange(H) + 0.5) / ppm - margin)
    p = frame.to_px(np.c_[u.ravel(), v.ravel()])
    mx = p[:, 0].reshape(H, W).astype(np.float32)
    my = p[:, 1].reshape(H, W).astype(np.float32)
    return cv2.remap(rgb, mx, my, cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(255, 255, 255))


def overlays(rgb, result, frame, extra):
    """Pictures to look at, in out/: the rectified board with the outline,
    holes and rows drawn on it, and the full-size rectified copy."""
    import cv2
    from PIL import Image
    common.OUT.mkdir(exist_ok=True)
    ppm, m = RECTIFIED_PX_PER_MM, RECTIFIED_MARGIN_MM
    rect = rectify(rgb, frame, result['board']['widthMm'], result['board']['depthMm'])
    Image.fromarray(rect).save(common.OUT / 'rectified-16.jpg', quality=92)

    def P(x, y):
        return (int(round((x + m) * ppm * 4)), int(round((y + m) * ppm * 4)))
    img = rect.copy()
    pts = np.array([P(x, y) for x, y in result['outline']], np.int32)
    cv2.polylines(img, [pts.reshape(-1, 1, 2)], True, (255, 0, 255), 2, cv2.LINE_AA, 2)
    for h in result['holes']:
        cv2.circle(img, P(h['x'], h['y']), int(round(h['d'] / 2 * ppm * 4)), (255, 0, 0), 2, cv2.LINE_AA, 2)
        cv2.drawMarker(img, (int(round((h['x'] + m) * ppm)), int(round((h['y'] + m) * ppm))), (255, 0, 0), cv2.MARKER_CROSS, 12, 1)
    for r in extra['rows']:
        for _, x, y in r['pads']:
            X, Y = frame.to_board([(x, y)])[0]
            cv2.circle(img, P(X, Y), 4 * 4, (0, 255, 255) if r['axis'] == 'x' else (255, 255, 0), 1, cv2.LINE_AA, 2)
    Image.fromarray(img).save(common.OUT / 'frame-overlay.jpg', quality=90)
    Image.fromarray(img).resize((img.shape[1] // 4, img.shape[0] // 4), Image.LANCZOS).save(common.OUT / 'frame-overlay-small.jpg', quality=90)


def main():
    rgb = common.read_rgb('I1')
    result, frame, extra = measure(rgb, rows_from_marks(marks()))
    result['verdicts'] = judge(result)
    result['rectified'] = {'pxPerMm': RECTIFIED_PX_PER_MM, 'marginMm': RECTIFIED_MARGIN_MM, 'file': 'out/rectified-16.jpg',
                           'what': 'I1 resampled onto the board frame: pixel (u, v)\'s centre is board millimetre ((u + 0.5) / 16 - 2, (v + 0.5) / 16 - 2). Made by board_frame.py in out/, which is git-ignored: I1 states no licence, so no copy of it is committed'}
    result['about'] = ('Task 2 of the BBC Micro models plan: the board frame on scan I1. A board millimetre X (from the left rear corner, x to the right, '
                       'y towards the front) is at I1 pixel origin + diag(pxPerMm.x, pxPerMm.y) R(rotationDeg) X. The outline, holes and edges are '
                       'in board millimetres. Written by board_frame.py; every figure here is a measurement made on the date of the journal entry that quotes it.')
    print(f"board {result['board']['widthMm']:.2f} x {result['board']['depthMm']:.2f} mm, turned {result['rotationDeg']:.4f} degrees, "
          f"{len(result['holes'])} holes, outline {len(result['outline']) - 1} corners; straightness median "
          f"{result['straightness']['rmsMm']['median']:.4f}, max {result['straightness']['rmsMm']['max']:.4f} mm")
    common.write_data('frame.json', result)
    overlays(rgb, result, frame, extra)
    return 3 if any(v['verdict'] == 'STOP' for v in result['verdicts']) else 0


if __name__ == '__main__':
    sys.exit(main())
