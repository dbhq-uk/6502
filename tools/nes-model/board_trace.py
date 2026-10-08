"""Task 4: the copper on both faces of the NES-CPU-10 board, and its printed
legend, traced from the bare scans (I1-front, the component side, and
I1-back, the solder side); the plan's checks on them (coverage, drills in
copper, the known nets); and the track map the inside model draws. Adapted
from tools/kim1-model/trace.py.

    NES_MODEL_INPUTS=<folder> python board_trace.py --look       # the traces' overlays and the map's sizes; no check is run
    NES_MODEL_INPUTS=<folder> python board_trace.py --map-ppm N  # the checks, the map at N px/mm, data/copper.json

Everything is in the board frame of data/frame.json (task 2): millimetres
from the board's top left corner as I1-front lies, x to the right, y down
towards the edge fingers. I1-front is read through that frame; I1-back,
flipped left to right, through the solder side's affine in
data/registration.json (task 3). Both are resampled onto one grid at 12
pixels per millimetre (R), over the outline's box, so the two faces and the
print share their pixels.

1. Colour. OKLab: L is lightness, C chroma. On these scans (probes below) a
   pixel is one of four things:
   tin: copper with no lacquer on it, tinned (the pads, the wide plane round
     the component side's edge, the solder side's hatched strips, the edge
     fingers): grey, its chroma under 0.08 of its lightness, and L over 0.15.
   print: the white legend, on the component side only (the solder side has
     none: looked at on both scans): L of 0.63 or more, except on a pad
     task 3 found (a pad's solder is as white), grown by 0.15 mm to take the
     strokes' blurred edges. Pieces under 0.02 square mm are specks.
   hidden: what hides the board and is not print: the NTSC sticker, marked by
     hand (data/marks.json "hidden"), grown by 0.15 mm.
   lacquer: the rest, green; under it, copper or bare laminate.
2. The lacquer's level. Under the lacquer, copper is lighter than the bare
   laminate by about 0.07 in L on both faces, and the laminate's own
   lightness drifts across the component side by more than half that. The
   level is a smooth surface, quadratic in x and y, fitted with outliers
   left out to the median L of the laminate in 10 mm blocks (every 5 mm),
   the laminate being, in each block, what lies under Otsu's split of L less
   a first surface fitted to the blocks' 10th percentiles. This was chosen
   over the KIM-1's level (a percentile in 6 mm blocks) and over a grey
   opening: on these scans both followed the copper's density, not the
   light, since a block or an opening's disc inside a wide pour sees no
   laminate (their maps, looked at on 5 October 2026, show the board's
   layout). L is smoothed first by a Gaussian of 1.25 pixels taken over the
   lacquer alone (a normalised convolution), so the bright tin and print do
   not bleed into it.
3. Copper under the lacquer: dL, L less the level, over a threshold chosen
   by Otsu's method on dL over the lacquer (at least 2 pixels from anything
   else), with hysteresis: a region over the threshold is kept only if it
   holds a pixel as far over it as the laminate's median is under it. The
   KIM-1's hysteresis (a region over 0.6 of the threshold kept if it holds a
   pixel over it) was tried first, on the made-up board of the tests: it
   widened every track by a pixel each side (the solder side's intersection
   over union 0.82), and on I1-front the gaps between the wide tracks under
   U6 are 2 to 4 pixels, which that would close. So the low threshold is
   Otsu's, where the edge is, and the high one only refuses specks.
4. The lacquer's bright rim round a pad. Next to a tinned pad the lacquer
   is lighter than copper under it, for a few pixels (round the solder
   side's DIP pins brighter still, sRGB about (6, 72, 47) between two of
   U6's pins), and the rims of two neighbouring pads meet, so copper under
   the lacquer would join every pin of a row. The rim's width is measured on
   each face as it is traced: the median L of the lacquer at each whole
   pixel's distance from the pads (the tinned pieces of 0.8 to 4 square mm),
   and the rim ends at the first distance where that median is within 0.005
   of its median 10 to 12 pixels out. Copper under the lacquer within the rim
   is kept only where it is a track running on past it: along the line
   straight out from the pad, it reaches the pad one way and, the other way,
   copper beyond the rim that runs on along the same line for as many steps
   as the rim is pixels wide, or 0.3 mm, whichever is more (a slanting line
   crosses a track's width in fewer steps); or, for a pixel at least 3 pixels
   from every pad (a track passing between two pads), along any of the 8
   lines it reaches copper beyond the rim both ways within 3 mm, walking
   only on such pixels. (The line across the one out from the pad was tried
   first; beside a pad's curve it slants, and the made-up track between two
   pads was cut where the pads' edges turn.) The run along the same line,
   and the 3 pixels, were set on the made-up board of the tests, where
   without them a slanting walk from a pad's rim rode into a track passing
   between two pads and joined both pads to it.
5. Copper: tin and copper under the lacquer. A drill's hole inside it is
   filled: a piece of not-copper under 2 square mm that is mostly grey (its
   chroma under 0.08 of its lightness, however dark: a drill's wall in shadow
   or the lid seen down it). A ring of bare laminate round a pad is green,
   so it is never filled, and a pad is not joined to a plane round it.
   Pieces under 0.15 square mm are dropped, and so are pieces under 1 square
   mm that are not pad-shaped (no more than 1.8 to 1, at least 0.45 of their
   box filled): pads are kept by their shape, specks of noise are not.
6. Copper under the print is recovered only where copper either side of the
   print continues in line: along the line straight across the print's
   stroke at that pixel (the nearest of 8 to the stroke's normal, from the
   print mask's structure tensor), the print is crossed within 1.2 mm, and
   on both sides the copper begins at the print's edge and runs on for at
   least 0.3 mm the same way. A wider block of print, and print over bare
   laminate, is left as print. At first any of the 8 lines would do; on
   I1-front, under the lower line of U6's outline, a slanting line from a
   gap between two tracks reached a track on each side, and the whole line
   was recovered, joining the tracks (seen on its overlay before any check
   was run); the made-up board's side by side tracks test it.

The checks, against the plan's table (fixed before the measurements):
coverage, each face's copper over the board's area, 10 to 50 per cent;
drills in copper (task 3's drills: copper on at least half of a ring 0.1 mm
outside the drill's edge), at least 95 per cent on each face; the known nets:
for each IC whose footprint and pin 1 task 3 found and whose GND and +5V pins
data/ic-table.json gives with a source, those pins, each joined to both faces
(a through-hole lead), and the faces joined at every drill; at least 8 ICs,
at least 90 per cent of their GND pins in one net and of their +5V pins in
another, and no net holding both. The nets check is the held-out test: the
method above was fixed before it was first run, and nothing in it was changed
after.

Writes data/copper.json, the track map site/src/assets/tracks/
nes-famicom-board.webp (lossless; red the component side's copper, green the
solder side's, blue the print; 255 or 0), and overlays in out/ (git-ignored:
the scans state no licence).
"""
import argparse
import hashlib
import io
import json
import math
import sys
import warnings

import cv2
import numpy as np

import board_frame
import common

R = 12                       # pixels per millimetre of the traced grid
MAP_PPMS = (10, 9, 8, 7, 6)  # the map's pixels per millimetre: the highest inside the budget
MAP_BUDGET = 600_000         # bytes (the plan)
MAP_FILE = common.REPO / 'site' / 'src' / 'assets' / 'tracks' / 'nes-famicom-board.webp'

# --- the probes (5 October 2026; sRGB in OKLab; boxes in the 12 px/mm grid unless said) ---
# Bare laminate under the lacquer, component side, box x 585-640 y 815-840: L 0.146 (sd 0.017),
#   sRGB median (1, 15, 3); solder side, box x 1030-1090 y 970-1030: L 0.144 (sd 0.017).
# Copper under the lacquer, component side, box x 425-455 y 760-800 (between U6's rows): L 0.212
#   (sd 0.024), sRGB (1, 32, 7); solder side, box x 1122-1150 y 982-995: L 0.213 (sd 0.028).
#   Copper is the laminate's colour made lighter: their hue is the same, 145 to 147 degrees.
# Smoothed by a Gaussian of 1.25 px, the two boxes' L sit 6.2 (component side) and 4.4 (solder side)
#   of their pooled standard deviations apart, against 3.3 and 3.0 unsmoothed (6.6 and 4.9 at 1.5 px,
#   5.9 and 4.0 at 1.0). SMOOTH_PX is the largest of 1.5, 1.25 and 1.0 that keeps the made-up board's four
#   tracks 3 pixels apart as four pieces (1.5 joined them): I1-front's narrowest gaps, between the wide
#   tracks under U6, are 2 to 4 pixels (box x 380-520, y 740-820, read by eye on 5 October 2026).
# Lacquer (common.py's boxes on I1-front): L 0.084 / 0.171 / 0.248 (1st, 50th, 99th percentiles),
#   chroma over lightness 0.121 / 0.270 / 0.340.
# Tin, component side's left plane (scan x 40-130, y 470-640): L 0.179 / 0.339 / 0.520 (5th, 50th,
#   99th), C / L median 0.044; solder side's hatched strip (scan, flipped, x 50-110 y 300-800):
#   L 0.237 / 0.380 / 0.568, C / L median 0.033.
#   SILVER_CL is half way between the tin's median C / L (0.044) and the lacquer's 1st percentile
#   (0.121); SILVER_MIN_L is under the tin's 5th percentile and leaves out the dark drills.
# Print, the "NES-CPU-10" legend (scan x 930-1230, y 828-862), its pixels over L 0.6: L 0.627 /
#   0.737 / 0.804 (5th, 50th, 99th), C median 0.033. PRINT_MIN_L is half way between the tin's 99th
#   percentile (0.520) and the print's median (0.737). A pad's solder reaches L 0.80 (front) and
#   0.89 (back) (common.py's U6 rings), so white on a pad is a pad.
# The NTSC sticker (scan x 530-720, y 1255-1300): L 0.742, C 0.045, hue 83: as light as the print,
#   so it is marked by hand (marks.json "hidden"), not told apart by colour.
# The scanner's lid: L 0.982. A drill is grey, never green: its wall in shadow and the lid seen down it.
SMOOTH_PX = 1.25
SILVER_CL, SILVER_MIN_L = 0.08, 0.15
PRINT_MIN_L = 0.63
PRINT_GROW_MM = 0.15
PRINT_MIN_MM2 = 0.02
PAD_CLEAR_MM = 0.2
PAD_MAX_R_MM, PAD_TYPICAL_R_MM = 1.6, 0.8
LEVEL_BLOCK_MM = 10.0
LEVEL_FIRST_Q = 10
MIN_PIECE_MM2 = 0.15
SMALL_PIECE_MM2 = 1.0
PAD_ASPECT, PAD_FILL = 1.8, 0.45
FILL_HOLE_MM2 = 2.0
RECOVER_GAP_MM = 1.2
PAD_PIECE_MM2 = (0.8, 4.0)
RIM_TOL, RIM_PLATEAU_PX, RIM_MAX_PX = 0.005, (10, 12), 10
RIM_ACROSS_MM = 3.0
RIM_ACROSS_MIN_PX = 3
RECOVER_RUN_MM = 0.3
DRILL_RING_MM = 0.1
DRILL_D_UNKNOWN_MM = 0.6     # a drill filled with solder has no diameter: a via's
POINT_R_MM = 0.5             # a pin's net: the commonest copper within this of its centre

# The plan's table
COVERAGE = (0.10, 0.50)
DRILLS_MIN = 0.95
NETS_MIN_CHIPS = 8
NETS_MIN_SHARE = 0.90
THRESHOLDS = {
    'coverage': 'each face\'s copper 10 to 50 per cent of the board',
    'drillsInCopper': 'at least 95 per cent on each face',
    'nets': ('at least 8 ICs; at least 90 per cent of their GND pins in one net and of their +5V pins in another; '
             'the two never connected'),
}

LINES = ((1, 0), (0, 1), (1, 1), (1, -1), (2, 1), (1, 2), (2, -1), (1, -2))


def mm(v):
    return max(1, int(round(v * R)))


def otsu(values):
    hist, edges = np.histogram(values, bins=256)
    centres = (edges[:-1] + edges[1:]) / 2
    w0 = np.cumsum(hist)
    w1 = w0[-1] - w0
    m0 = np.cumsum(hist * centres) / np.maximum(w0, 1)
    m1 = (np.sum(hist * centres) - np.cumsum(hist * centres)) / np.maximum(w1, 1)
    return float(centres[np.argmax(w0 * w1 * (m0 - m1) ** 2)])


def disc(r_px):
    r = max(1, int(round(r_px)))
    return cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1))


def pieces(mask):
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), connectivity=8)
    return n, labels, stats


def smooth_over(L, ok, sigma):
    """L smoothed by a Gaussian over the pixels in ok alone (a normalised
    convolution): what is not lacquer does not bleed into it."""
    w = cv2.GaussianBlur(ok.astype(np.float32), (0, 0), sigma)
    v = cv2.GaussianBlur(np.where(ok, L, 0).astype(np.float32), (0, 0), sigma)
    with np.errstate(invalid='ignore', divide='ignore'):
        out = v / w
    return np.where(w > 1e-3, out, L).astype(np.float32)


def _blocks(L, ok, q, min_px):
    b = mm(LEVEL_BLOCK_MM)
    H, W = L.shape
    pts = []
    for y in range(0, max(1, H - b // 2), b // 2):
        for x in range(0, max(1, W - b // 2), b // 2):
            s = ok[y:y + b, x:x + b]
            if s.sum() >= min_px:
                pts.append((x + s.shape[1] / 2, y + s.shape[0] / 2, float(np.percentile(L[y:y + b, x:x + b][s], q))))
    return np.array(pts, float).reshape(-1, 3)


def _surface(pts, shape, rounds=5):
    """A quadratic in x and y fitted to (x, y, value) points, leaving out those
    over 2.5 robust standard deviations from it, refitted 5 times."""
    H, W = shape
    x, y, v = pts.T

    def T(x, y):
        u, w = x / W - 0.5, y / H - 0.5
        return np.stack([np.ones_like(u), u, w, u * u, u * w, w * w], -1)
    keep = np.ones(len(v), bool)
    for _ in range(rounds):
        c = np.linalg.lstsq(T(x[keep], y[keep]), v[keep], rcond=None)[0]
        r = v - T(x, y) @ c
        s = max(1.4826 * float(np.median(np.abs(r[keep]))), 1e-4)
        keep = np.abs(r) < 2.5 * s
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    return (T(xx, yy) @ c.astype(np.float32)).astype(np.float32), {'blocks': int(len(v)), 'kept': int(keep.sum()), 'robustSd': s}


def level_surface(L, ok):
    """The lacquer's level (the module's docstring, 2)."""
    min_px = int(0.06 * mm(LEVEL_BLOCK_MM) ** 2)
    first, _ = _surface(_blocks(L, ok, LEVEL_FIRST_Q, min_px), L.shape)
    split = otsu(np.clip((L - first)[ok], -0.2, 0.4))
    laminate = ok & ((L - first) < split)
    level, info = _surface(_blocks(L, laminate, 50, min_px), L.shape)
    return level, dict(info, firstSplit=split)


def hysteresis(cand, dL, lo, hi):
    weak = cand & (dL > lo)
    n, labels = cv2.connectedComponents(weak.astype(np.uint8), connectivity=8)
    keep = np.zeros(n, bool)
    keep[np.unique(labels[cand & (dL > hi)])] = True
    keep[0] = False
    return keep[labels]


def pad_discs(shape, pads, clear_mm=PAD_CLEAR_MM):
    """Discs over the pads ((x, y, r) in mm, r the pad's radius), each grown by clear_mm."""
    out = np.zeros(shape, np.uint8)
    for x, y, r in pads:
        cv2.circle(out, (int(round(x * R)), int(round(y * R))), int(round((r + clear_mm) * R)), 1, -1)
    return out > 0


def pad_radius(p):
    """A pad's radius in mm from task 3's blob: half its larger side, or, where
    the blob ran into a tinned plane or track (over 3.2 mm; 95 of task 3's
    1296 pads at drills or on one face), 0.8 mm, task 0's made-up pad."""
    r = max(p['w'], p['h']) / 2
    return r if r <= PAD_MAX_R_MM else PAD_TYPICAL_R_MM


def clean(copper, grey):
    """The module's docstring, 4: drills' holes filled, specks and small
    pieces that are not pad-shaped dropped."""
    n, labels, stats = pieces(~copper)
    area = stats[:, cv2.CC_STAT_AREA]
    greyn = np.bincount(labels.ravel(), weights=grey.ravel().astype(float), minlength=n)
    fill = (area < FILL_HOLE_MM2 * R * R) & (greyn >= 0.5 * area)
    fill[0] = False
    # a hole touching the grid's edge is outside, never filled
    edge = np.unique(np.r_[labels[0], labels[-1], labels[:, 0], labels[:, -1]])
    fill[edge] = False
    copper = copper | fill[labels]
    n, labels, stats = pieces(copper)
    w, h = stats[:, cv2.CC_STAT_WIDTH], stats[:, cv2.CC_STAT_HEIGHT]
    area = stats[:, cv2.CC_STAT_AREA] / R ** 2
    padlike = (np.maximum(w, h) <= PAD_ASPECT * np.minimum(w, h)) & (stats[:, cv2.CC_STAT_AREA] >= PAD_FILL * w * h)
    keep = (area >= MIN_PIECE_MM2) & ((area >= SMALL_PIECE_MM2) | padlike)
    keep[0] = False
    return keep[labels]


def _shift(a, d, k):
    """b[y, x] = a[y + k dy, x + k dx], False off the grid."""
    dx, dy = d[0] * k, d[1] * k
    H, W = a.shape
    b = np.zeros_like(a)
    if abs(dx) >= W or abs(dy) >= H:
        return b
    ys, yd = (slice(dy, H), slice(0, H - dy)) if dy >= 0 else (slice(0, H + dy), slice(-dy, H))
    xs, xd = (slice(dx, W), slice(0, W - dx)) if dx >= 0 else (slice(0, W + dx), slice(-dx, W))
    b[yd, xd] = a[ys, xs]
    return b


def stroke_normals(printed):
    """For each pixel, the index into LINES of the line nearest the print
    stroke's normal there: the structure tensor of the print mask blurred by
    1 px, summed over a Gaussian of 3 px."""
    m = cv2.GaussianBlur(printed.astype(np.float32), (0, 0), 1.0)
    gx = cv2.Sobel(m, cv2.CV_32F, 1, 0, ksize=3)
    gy = cv2.Sobel(m, cv2.CV_32F, 0, 1, ksize=3)
    jxx = cv2.GaussianBlur(gx * gx, (0, 0), 3.0)
    jyy = cv2.GaussianBlur(gy * gy, (0, 0), 3.0)
    jxy = cv2.GaussianBlur(gx * gy, (0, 0), 3.0)
    normal = 0.5 * np.arctan2(2 * jxy, jxx - jyy)
    angles = np.array([math.atan2(d[1], d[0]) for d in LINES])
    diff = np.abs((normal[..., None] - angles + np.pi / 2) % np.pi - np.pi / 2)
    return diff.argmin(-1)


def _exit(zone, target, dd, G):
    """For each pixel of zone, the step k (1 to G) at which a walk along dd
    first leaves zone, when the pixel it lands on is in target; else 0."""
    got = np.zeros(zone.shape, np.int32)
    alive = zone.copy()
    for k in range(1, G + 1):
        at = _shift(zone, dd, k)
        leaving = alive & ~at
        got[leaving & _shift(target, dd, k)] = k
        alive &= at
        if not alive.any():
            break
    return got


def _runs(copper, dd, C):
    runs = copper.copy()
    for j in range(1, C):
        runs &= _shift(copper, dd, j)
    return runs


def _line_index(angle):
    """The nearest of LINES to each angle (radians), and the sign that
    points the line's way."""
    angles = np.array([math.atan2(d[1], d[0]) for d in LINES])
    diff = (angle[..., None] - angles + np.pi / 2) % np.pi - np.pi / 2
    i = np.abs(diff).argmin(-1)
    sign = np.where(np.cos(angle - angles[i]) >= 0, 1, -1)
    return i, sign


def rim_width(L, pads, lacquer):
    """The lacquer's bright rim round the pads, in whole pixels (the
    module's docstring, 4), with the medians it was read from."""
    d = cv2.distanceTransform((~pads).astype(np.uint8), cv2.DIST_L2, 5)
    med = {}
    for k in range(1, RIM_PLATEAU_PX[1] + 1):
        v = L[lacquer & (d > k - 0.5) & (d <= k + 0.5)]
        med[k] = float(np.median(v)) if v.size else float('nan')
    plateau = float(np.nanmedian([med[k] for k in range(RIM_PLATEAU_PX[0], RIM_PLATEAU_PX[1] + 1)]))
    for k in range(1, RIM_MAX_PX + 1):
        if med[k] - plateau <= RIM_TOL:
            return k, med, plateau
    return RIM_MAX_PX, med, plateau


def keep_off_rims(under, pads, rim_px):
    """Copper under the lacquer with the rims round the pads taken off, except
    tracks running on past them (the module's docstring, 4)."""
    d = cv2.distanceTransform((~pads).astype(np.uint8), cv2.DIST_L2, 5)
    rim = ~pads & (d <= rim_px + 0.5)
    far = under & ~rim
    zone = under & rim
    if not zone.any():
        return far
    gy = cv2.Sobel(d, cv2.CV_32F, 0, 1, ksize=3)
    gx = cv2.Sobel(d, cv2.CV_32F, 1, 0, ksize=3)
    out_angle = np.arctan2(gy, gx)
    radial, rsign = _line_index(out_angle)
    middle = zone & (d >= RIM_ACROSS_MIN_PX)
    kept = np.zeros_like(zone)
    for i, d0 in enumerate(LINES):
        step = math.hypot(*d0)
        C = max(1, int(math.ceil(RECOVER_RUN_MM * R / step)))
        Cr = max(C, rim_px)
        Gr = int(math.ceil((rim_px + 2) / step))
        Ga = int(math.ceil(RIM_ACROSS_MM * R / step))
        for sign in (1, -1):
            dd = (d0[0] * sign, d0[1] * sign)
            back = (-dd[0], -dd[1])
            sel = zone & (radial == i) & (rsign == sign)
            if sel.any():
                outward = _exit(zone, _runs(far, dd, Cr), dd, Gr) > 0
                inward = _exit(zone, pads, back, Gr) > 0
                kept |= sel & outward & inward
        sel = middle
        if sel.any():
            a = _exit(middle, _runs(far, d0, C), d0, Ga) > 0
            b = _exit(middle, _runs(far, (-d0[0], -d0[1]), C), (-d0[0], -d0[1]), Ga) > 0
            kept |= sel & a & b
    return far | kept


def recover_under_print(copper, printed):
    """Copper under the print where copper either side continues in line (the
    module's docstring, 5). copper is the copper found off the print."""
    out = np.zeros_like(printed)
    across = stroke_normals(printed)
    for i, d in enumerate(LINES):
        step = math.hypot(*d)
        G = int(math.ceil(RECOVER_GAP_MM * R / step))
        C = max(1, int(math.ceil(RECOVER_RUN_MM * R / step)))
        reach = {}
        for sign in (1, -1):
            dd = (d[0] * sign, d[1] * sign)
            reach[sign] = _exit(printed, _runs(copper, dd, C), dd, G)
        out |= printed & (across == i) & (reach[1] > 0) & (reach[-1] > 0) & (reach[1] + reach[-1] - 1 <= G)
    return out


def trace_face(rgb, inside, pads=(), hidden=None, side='top'):
    """One face, in the grid (the module's docstring, 1 to 5). rgb: the face
    resampled onto the grid; inside: the board; pads: (x, y, r) in mm, the
    pads task 3 found on this face; hidden: what hides the board (the
    sticker). Returns copper (with what was recovered under the print), print,
    hidden, recovered, and the figures behind them."""
    L, A, B = common.oklab(rgb)
    C = np.hypot(cv2.GaussianBlur(A, (0, 0), 1.0), cv2.GaussianBlur(B, (0, 0), 1.0))
    L1 = cv2.GaussianBlur(L, (0, 0), 1.0)
    grow = disc(PRINT_GROW_MM * R)
    on_pad = pad_discs(L.shape, pads)
    hid = np.zeros(L.shape, bool) if hidden is None else cv2.dilate(hidden.astype(np.uint8), grow) > 0
    hid &= inside
    printed = np.zeros(L.shape, bool)
    if side == 'top':
        white = inside & (L1 >= PRINT_MIN_L) & ~on_pad & ~hid
        n, labels, stats = pieces(white)
        keep = stats[:, cv2.CC_STAT_AREA] >= PRINT_MIN_MM2 * R * R
        keep[0] = False
        printed = (cv2.dilate(keep[labels].astype(np.uint8), grow) > 0) & inside & ~on_pad & ~hid
    silver = inside & (C < SILVER_CL * L1) & (L1 > SILVER_MIN_L) & ~printed & ~hid
    grey = inside & (C < SILVER_CL * L1) & ~printed & ~hid
    lacquer = inside & ~grey & ~printed & ~hid
    Ls = smooth_over(L, lacquer, SMOOTH_PX)
    others = cv2.dilate((~lacquer).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    core = lacquer & ~others
    level, info = level_surface(Ls, core)
    dL = Ls - level
    t = otsu(np.clip(dL[core], -0.2, 0.4))
    m0 = float(np.median(dL[core][dL[core] <= t]))
    high = 2 * t - m0
    under = hysteresis(lacquer, dL, t, high)
    n, labels, stats = pieces(silver)
    area = stats[:, cv2.CC_STAT_AREA] / R ** 2
    padlike = (area >= PAD_PIECE_MM2[0]) & (area <= PAD_PIECE_MM2[1])
    padlike[0] = False
    pads_m = padlike[labels]
    away = lacquer & ~(cv2.dilate((silver & ~pads_m).astype(np.uint8), np.ones((9, 9), np.uint8)) > 0)
    rim_px, rim_med, plateau = rim_width(L, pads_m, away)
    under_all = under
    under = keep_off_rims(under, pads_m, rim_px)
    copper = clean(silver | under, grey)
    recovered = recover_under_print(copper, printed) if printed.any() else np.zeros_like(printed)
    return {'copper': copper | recovered, 'print': printed, 'hidden': hid, 'recovered': recovered,
            'threshold': t, 'high': high, 'laminateMedian': m0, 'level': info,
            'rim': {'px': rim_px, 'medianL': {str(k): v for k, v in rim_med.items()}, 'plateau': plateau,
                    'takenOffMm2': float((under_all & ~under).sum() / R ** 2)}, 'shares': {
                'tin': float(silver[inside].mean()), 'underLacquer': float((copper & lacquer)[inside].mean()),
                'lacquerAsCopper': float((dL[core] > t).mean())}}


# --- the checks -------------------------------------------------------------------------

def drills_in_copper(copper, drills):
    """For each drill (x, y, d) in mm (d None for one filled with solder), is
    copper on at least half of a ring DRILL_RING_MM outside its edge?"""
    H, W = copper.shape
    a = np.linspace(0, 2 * np.pi, 32, endpoint=False)
    out = []
    for x, y, d in drills:
        r = ((d if d else DRILL_D_UNKNOWN_MM) / 2 + DRILL_RING_MM) * R
        xs = np.clip(np.round(x * R + r * np.cos(a)).astype(int), 0, W - 1)
        ys = np.clip(np.round(y * R + r * np.sin(a)).astype(int), 0, H - 1)
        out.append(bool(copper[ys, xs].mean() >= 0.5))
    return out


class _Union:
    def __init__(self):
        self.p = {}

    def find(self, a):
        self.p.setdefault(a, a)
        while self.p[a] != a:
            self.p[a] = self.p[self.p[a]]
            a = self.p[a]
        return a

    def join(self, a, b):
        self.p[self.find(a)] = self.find(b)


def _label_at(labels, p, r):
    x, y = int(round(p[0])), int(round(p[1]))
    H, W = labels.shape
    y0, y1, x0, x1 = max(0, y - r), min(H, y + r + 1), max(0, x - r), min(W, x + r + 1)
    yy, xx = np.mgrid[y0:y1, x0:x1]
    v = labels[y0:y1, x0:x1][(yy - y) ** 2 + (xx - x) ** 2 <= r * r]
    v = v[v > 0]
    return int(np.bincount(v).argmax()) if v.size else None


def nets_check(top, bottom, drills, chips):
    """The known nets (the module's docstring). top, bottom: each face's copper
    in the grid; drills: (x, y) in grid pixels, each joining the faces;
    chips: [{"ref", "gnd": (x, y) or None, "vcc": (x, y) or None}] in grid
    pixels, each pin a through-hole lead joining the faces."""
    lt = cv2.connectedComponents(top.astype(np.uint8), connectivity=8)[1]
    lb = cv2.connectedComponents(bottom.astype(np.uint8), connectivity=8)[1]
    r = mm(POINT_R_MM)
    u = _Union()

    def at(p):
        a, b = _label_at(lt, p, r), _label_at(lb, p, r)
        a, b = (('t', a) if a else None), (('b', b) if b else None)
        if a and b:
            u.join(a, b)
        return a or b
    for p in drills:
        at(p)
    pins = {'gnd': [], 'vcc': []}
    for c in chips:
        for k in ('gnd', 'vcc'):
            if c.get(k) is not None:
                pins[k].append((c['ref'], at(c[k])))
    out = {'chips': len(chips)}
    nets = {}
    for k in ('gnd', 'vcc'):
        found = [u.find(n) for _, n in pins[k] if n is not None]
        nets[k] = set(found)
        counts = {}
        for n in found:
            counts[n] = counts.get(n, 0) + 1
        out[k] = {'pins': len(pins[k]), 'inLargest': max(counts.values()) if counts else 0}
    out['touching'] = bool(nets['gnd'] & nets['vcc'])
    def label(n):
        face, piece = u.find(n)
        return f"{'top' if face == 't' else 'bottom'}:{piece}"
    out['pinNets'] = {k: [[ref, None if n is None else label(n)] for ref, n in pins[k]] for k in pins}
    return out


def verdicts(c):
    cov, dr, n = c['coverage'], c['drillsInCopper'], c['nets']
    ok = lambda b: 'pass' if b else 'fail'  # noqa: E731
    return {
        'coverage': ok(all(COVERAGE[0] <= cov[f] <= COVERAGE[1] for f in ('top', 'bottom'))),
        'drillsInCopper': ok(all(dr[f] >= DRILLS_MIN for f in ('top', 'bottom'))),
        'nets': ok(n['chips'] >= NETS_MIN_CHIPS and not n['touching']
                   and all(n[k]['pins'] > 0 and n[k]['inLargest'] >= NETS_MIN_SHARE * n[k]['pins'] - 1e-9 for k in ('gnd', 'vcc'))),
    }


# --- the frame and the faces ------------------------------------------------------------------

def affine_from_corners(t):
    """The solder side's affine from registration.json's corners (board_register.
    solder_transform): (to_mm, to_px), each taking (N, 2) points. The fourth
    corner must agree with the affine the other three fix, within 0.002 mm."""
    if t['model'] != 'affine':
        raise ValueError('the solder side is read through an affine')
    src = np.array(t['cornersPx'], float)
    dst = np.array(t['cornersMm'], float)
    M = np.linalg.solve(np.c_[src[:3], np.ones(3)], dst[:3]).T
    check = src[3] @ M[:, :2].T + M[:, 2]
    if np.hypot(*(check - dst[3])) > 0.002:
        raise ValueError(f'the fourth corner is {np.hypot(*(check - dst[3])):.4f} mm off the affine of the other three')
    Mi = np.linalg.inv(np.vstack([M, [0, 0, 1]]))[:2]
    return (lambda q: np.asarray(q, float).reshape(-1, 2) @ M[:, :2].T + M[:, 2],
            lambda q: np.asarray(q, float).reshape(-1, 2) @ Mi[:, :2].T + Mi[:, 2])


def grid(outline):
    """The grid over the outline's box: (x0, y0, W, H)."""
    o = np.asarray(outline, float)
    (x0, y0), (x1, y1) = o.min(0), o.max(0)
    return float(x0), float(y0), int(round((x1 - x0) * R)), int(round((y1 - y0) * R))


def resample(rgb, to_px, x0, y0, W, H):
    u, v = np.meshgrid(x0 + (np.arange(W) + 0.5) / R, y0 + (np.arange(H) + 0.5) / R)
    p = to_px(np.c_[u.ravel(), v.ravel()])
    return cv2.remap(rgb, p[:, 0].reshape(H, W).astype(np.float32), p[:, 1].reshape(H, W).astype(np.float32),
                     cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(255, 255, 255))


def board_mask(fj, x0, y0, W, H):
    """The board in the grid: inside the outline, out of the mounting holes."""
    m = np.zeros((H, W), np.uint8)
    pts = np.round((np.asarray(fj['outline'], float) - [x0, y0]) * R - 0.5).astype(np.int32)
    cv2.fillPoly(m, [pts.reshape(-1, 1, 2)], 1)
    for h in fj['holes']:
        cv2.circle(m, (int(round((h['x'] - x0) * R - 0.5)), int(round((h['y'] - y0) * R - 0.5))), int(round(h['d'] / 2 * R)), 0, -1)
    return m > 0


# --- the map --------------------------------------------------------------------------------

def make_map(top, bottom, printed, inside, ppm):
    """The track map at ppm pixels per millimetre: red the component side's
    copper, green the solder side's, blue the print, each 255 or 0, nothing
    outside the board."""
    H, W = top.shape
    size = (int(round(W / R * ppm)), int(round(H / R * ppm)))

    def down(m):
        return cv2.resize(m.astype(np.float32), size, interpolation=cv2.INTER_AREA) > 0.5
    keep = down(inside)
    out = np.zeros((size[1], size[0], 3), np.uint8)
    for i, m in enumerate((top, bottom, printed)):
        out[..., i] = (down(m) & keep) * 255
    return out


def webp(rgb):
    from PIL import Image
    buf = io.BytesIO()
    Image.fromarray(rgb).save(buf, 'WEBP', lossless=True, quality=100, method=6, exact=True)
    return buf.getvalue()


# --- the run ------------------------------------------------------------------------------

def load(name):
    with open(common.DATA / name, encoding='utf8') as f:
        return json.load(f)


def chip_pins(reg, table, x0, y0):
    """For each IC whose footprint and pin 1 task 3 found and whose pins the
    table gives with a source: its GND and +5V pins in grid pixels; and the
    ICs left out, with why."""
    fps = {f['ref']: f for f in reg['footprints'] if f['ref']}
    chips, excluded = [], []
    for ic in table['ics']:
        ref = ic['ref']
        f = fps.get(ref)
        why = None
        if f is None:
            why = 'no footprint in registration.json'
        elif not f.get('pin1'):
            why = 'task 3 found no pin 1'
        elif f['pins'] != ic['pins']:
            why = f'its footprint has {f["pins"]} pins, its package {ic["pins"]}'
        elif ic.get('gnd') is None or ic.get('vcc') is None or not ic.get('pinoutSource'):
            why = ic.get('excludedWhy') or 'no GND or +5V pin with a source'
        if why:
            excluded.append([ref, why])
            continue
        p = {}
        for k in ('gnd', 'vcc'):
            pad = reg['pads'][f['pads'][ic[k] - 1]]
            p[k] = ((pad['x'] - x0) * R - 0.5, (pad['y'] - y0) * R - 0.5)
        chips.append({'ref': ref, **p})
    return chips, excluded


def overlays(faces, inside, x0, y0, reg, chips, nets):
    """Pictures to look at, in out/: each face with its copper's edge (red),
    the print (blue) and what was recovered under it (yellow); close-ups of
    the CPU, the PPU and the edge connector; and the GND and +5V pins coloured
    by net (green: in the largest GND net, magenta: in the largest +5V net,
    white: elsewhere)."""
    from PIL import Image, ImageDraw
    common.OUT.mkdir(exist_ok=True)
    sheets = {}
    for name, (rgb, t) in faces.items():
        img = np.clip(rgb.astype(np.float32) * 2.2, 0, 255).astype(np.uint8)
        c = t['copper'].astype(np.uint8)
        edge = (c - cv2.erode(c, np.ones((3, 3), np.uint8))) > 0
        img[edge] = (255, 40, 40)
        img[t['print'] & ~t['recovered']] = (img[t['print'] & ~t['recovered']] * 0.5 + np.array([0, 60, 255]) * 0.5).astype(np.uint8)
        img[t['recovered']] = (255, 230, 0)
        img[t['hidden']] = (img[t['hidden']] // 3)
        sheets[name] = img
        Image.fromarray(img).save(common.OUT / f'trace-{name}.png')
        flat = np.zeros(img.shape, np.uint8)
        flat[inside] = (0, 50, 0)
        flat[t['copper']] = (230, 120, 30)
        flat[t['print']] = (40, 90, 255)
        flat[t['recovered']] = (255, 230, 0)
        flat[t['hidden']] = (90, 90, 90)
        Image.fromarray(flat).save(common.OUT / f'trace-{name}-masks.png')
    fps = {f['ref']: f for f in reg['footprints'] if f['ref']}
    for ref, name in (('U6', 'cpu'), ('U5', 'ppu'), ('P1', 'edge')):
        f = fps[ref]
        a, b, c_, d = f['box']
        X0, Y0 = int((a - 3 - x0) * R), int((b - 3 - y0) * R)
        X1, Y1 = int((c_ + 3 - x0) * R), int((d + 3 - y0) * R)
        row = np.concatenate([sheets['top'][Y0:Y1, X0:X1], sheets['bottom'][Y0:Y1, X0:X1]], 0)
        Image.fromarray(row).resize((row.shape[1] * 2, row.shape[0] * 2), Image.NEAREST).save(common.OUT / f'trace-close-{name}.png')
    if nets is None:
        return
    im = Image.fromarray(np.stack([(faces['top'][1]['copper'] * 120).astype(np.uint8),
                                   (faces['bottom'][1]['copper'] * 120).astype(np.uint8),
                                   np.zeros(inside.shape, np.uint8)], -1))
    d = ImageDraw.Draw(im)
    big = {}
    for k in ('gnd', 'vcc'):
        ids = [n for _, n in nets['pinNets'][k] if n]
        big[k] = max(set(ids), key=ids.count) if ids else None
    for c in chips:
        for k, col in (('gnd', (0, 255, 0)), ('vcc', (255, 0, 255))):
            n = dict(nets['pinNets'][k]).get(c['ref'])
            x, y = c[k]
            fill = col if n is not None and n == big[k] else (255, 255, 255)
            d.ellipse([x - 9, y - 9, x + 9, y + 9], outline=fill, width=3)
            d.text((x + 11, y - 6), f"{c['ref']} {k}", fill=fill)
    im.save(common.OUT / 'trace-nets.png')
    return


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--map-ppm', type=int, default=None, help='the map\'s pixels per millimetre, chosen before the final run')
    ap.add_argument('--look', action='store_true',
                    help='trace both faces, draw the overlays and print the map\'s size at each of MAP_PPMS; run no check')
    args = ap.parse_args()
    warnings.simplefilter('ignore', RuntimeWarning)
    fj = load('frame.json')
    reg = load('registration.json')
    table = load('ic-table.json')
    marks = load('marks.json')
    frame = board_frame.Frame.read(fj)
    x0, y0, W, H = grid(fj['outline'])
    inside = board_mask(fj, x0, y0, W, H)
    to_mm_back, to_px_back = affine_from_corners(reg['solder']['transform'])
    hidden = np.zeros((H, W), bool)
    for h in marks.get('hidden', {}).get('marks', []):
        q = frame.to_board(np.array(h['boxPx'], float).reshape(-1, 2))
        a = np.round((q - [x0, y0]) * R).astype(int)
        hidden[max(0, a[:, 1].min()):a[:, 1].max(), max(0, a[:, 0].min()):a[:, 0].max()] = True
    faces = {}
    for name, sid, to_px in (('top', 'I1-front', frame.to_px), ('bottom', 'I1-back', to_px_back)):
        rgb = resample(common.read_rgb(sid, mirror=(name == 'bottom')), to_px, x0, y0, W, H)
        pads = [(p['x'] - x0, p['y'] - y0, pad_radius(p)) for p in reg['pads'] if p['face'] in (name, 'both') and p['source'] != 'finger']
        faces[name] = (rgb, trace_face(rgb, inside, pads=pads, hidden=hidden if name == 'top' else None, side=name))
        t = faces[name][1]
        print(f"{name}: Otsu dL {t['threshold']:.4f}; level from {t['level']['kept']} of {t['level']['blocks']} blocks, robust sd "
              f"{t['level']['robustSd']:.4f}; copper {t['copper'][inside].mean() * 100:.1f}% of the board "
              f"(tin {t['shares']['tin'] * 100:.1f}%, under the lacquer {t['shares']['underLacquer'] * 100:.1f}%)")
    top, bottom = faces['top'][1], faces['bottom'][1]
    printed = top['print'] & inside
    if args.look:
        for ppm in MAP_PPMS:
            print(f'map at {ppm} px/mm: {len(webp(make_map(top["copper"], bottom["copper"], printed, inside, ppm)))} bytes')
        overlays(faces, inside, x0, y0, reg, None, None)
        return 0
    if args.map_ppm not in MAP_PPMS:
        sys.exit('give --map-ppm, chosen beforehand from --look: the highest of 10, 9, 8, 7 or 6 inside 600,000 bytes')
    area = inside.sum()
    coverage = {'top': float(top['copper'][inside].sum() / area), 'bottom': float(bottom['copper'][inside].sum() / area),
                'print': float(top['print'][inside].sum() / area)}
    drills = [((d['x'] - x0), (d['y'] - y0), d['d']) for d in reg['drills']]
    din = {f: drills_in_copper(faces[f][1]['copper'], drills) for f in ('top', 'bottom')}
    drills_share = {f: float(np.mean(din[f])) for f in din}
    chips, excluded = chip_pins(reg, table, x0, y0)
    nets = nets_check(top['copper'], bottom['copper'], [((x * R) - 0.5, (y * R) - 0.5) for x, y, _ in drills], chips)
    hidden_print = printed.sum() / R ** 2
    rec = top['recovered'].sum() / R ** 2
    result = {
        'about': ('Task 4 of the NES models plan: the copper on both faces of the NES-CPU-10 board and its printed legend, traced from '
                  'the bare scans I1-front and I1-back (OpenTendo, read from the dbhq-uk fork at commit '
                  '3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009) in the board frame of frame.json; the plan\'s checks on them; and the track '
                  'map. Written by board_trace.py; every figure here is a measurement made on the date of the journal entry that quotes it.'),
        'grid': {'pxPerMm': R, 'originMm': [x0, y0], 'width': W, 'height': H,
                 'what': 'The traced grid: pixel (u, v)\'s centre is board millimetre (originMm + ((u + 0.5) / pxPerMm, (v + 0.5) / pxPerMm))'},
        'coverage': coverage,
        'drillsInCopper': drills_share,
        'drills': {'n': len(drills), 'notInCopper': {f: [i for i, ok in enumerate(din[f]) if not ok] for f in din}},
        'nets': {k: nets[k] for k in ('chips', 'gnd', 'vcc', 'touching')},
        'netsChips': [c['ref'] for c in chips],
        'netsExcluded': excluded,
        'pinNets': nets['pinNets'],
        'print': {'areaMm2': float(hidden_print), 'recoveredMm2': float(rec), 'recoveredShare': float(rec / max(hidden_print, 1e-9)),
                  'hiddenByStickerMm2': float((top['hidden'] & inside).sum() / R ** 2)},
        'trace': {f: {'otsuDL': faces[f][1]['threshold'], 'hysteresisHigh': faces[f][1]['high'], 'laminateMedianDL': faces[f][1]['laminateMedian'],
                      'level': faces[f][1]['level'], 'rim': faces[f][1]['rim'], 'shares': faces[f][1]['shares']} for f in faces},
        'thresholds': THRESHOLDS,
    }
    result['nets']['excluded'] = excluded
    result['verdicts'] = verdicts(result)
    m = make_map(top['copper'], bottom['copper'], printed, inside, args.map_ppm)
    data = webp(m)
    if len(data) > MAP_BUDGET:
        sys.exit(f'the map at {args.map_ppm} px/mm is {len(data)} bytes, over the budget')
    MAP_FILE.parent.mkdir(parents=True, exist_ok=True)
    MAP_FILE.write_bytes(data)
    result['mapPxPerMm'] = args.map_ppm
    result['map'] = {'width': int(m.shape[1]), 'height': int(m.shape[0]), 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
                     'file': 'site/src/assets/tracks/nes-famicom-board.webp', 'originMm': [x0, y0],
                     'what': ('Red the component side\'s copper, green the solder side\'s, blue the print, each 255 or 0; pixel (u, v)\'s centre '
                              'is board millimetre (originMm + ((u + 0.5) / mapPxPerMm, (v + 0.5) / mapPxPerMm)); lossless WebP'),
                     'budget': MAP_BUDGET, 'choice': 'the highest of 10, 9, 8, 7 or 6 px/mm inside the budget, chosen from --look before this run'}
    common.write_data('copper.json', result)
    print(f"coverage: top {coverage['top'] * 100:.1f}%, bottom {coverage['bottom'] * 100:.1f}%, print {coverage['print'] * 100:.1f}%: "
          f"{result['verdicts']['coverage']}")
    print(f"drills in copper: top {drills_share['top'] * 100:.1f}%, bottom {drills_share['bottom'] * 100:.1f}% of {len(drills)}: "
          f"{result['verdicts']['drillsInCopper']}")
    print(f"nets: {nets['chips']} ICs ({', '.join(c['ref'] for c in chips)}); excluded {excluded}; GND {nets['gnd']['inLargest']} of "
          f"{nets['gnd']['pins']} in one net, +5V {nets['vcc']['inLargest']} of {nets['vcc']['pins']}; touching {nets['touching']}: "
          f"{result['verdicts']['nets']}")
    print(f"print {hidden_print:.0f} mm2, copper recovered under it {rec:.0f} mm2; map {args.map_ppm} px/mm, {m.shape[1]} x {m.shape[0]}, "
          f"{len(data)} bytes")
    overlays(faces, inside, x0, y0, reg, chips, nets)
    return 0


if __name__ == '__main__':
    sys.exit(main())
