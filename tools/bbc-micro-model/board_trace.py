"""Task 4: the copper on both faces of the BBC Micro's Issue 7 board, and its
printed legend, traced from the bare scans (I1, the component side, and I2,
the solder side); the plan's checks on them (coverage, drills in copper, the
known nets, and the component side against I5, a photograph of a populated
board); and the track map the inside model draws. Adapted from
tools/nes-model/board_trace.py, which was adapted from
tools/kim1-model/trace.py.

    BBC_MODEL_INPUTS=<folder> python board_trace.py --look       # the traces' overlays and the map's sizes; no check is run
    BBC_MODEL_INPUTS=<folder> python board_trace.py --map-ppm N  # the checks, the map at N px/mm, data/copper.json

Everything is in the board frame of data/frame.json (task 2): millimetres
from the board's left rear corner, x to the right, y towards the front. I1 is
read through that frame; I2, flipped left to right, through the solder side's
saved map in data/registration.json (task 3), inverted here. Both are
resampled onto one grid at 16 pixels per millimetre (R), about the scans' own
15.7, over the outline's box, so the two faces and the print share their
pixels.

1. Colour. OKLab: L is lightness, a runs from green (negative) to red, b
   from blue to yellow, C is chroma. On these scans (the probes below) a pixel
   is one of these:
   copper under the lacquer: a strong green, a about -0.10, where the bare
     laminate under the lacquer is nearly black and grey-green (a about
     -0.017). On both faces a track is a bright line, often with a lighter
     stripe down its middle; no track was seen shown only by its edges.
   tin: solder, grey (C under 0.035) and light (L 0.60 or more): every pad,
     and solder run over the copper on the solder side.
   print: the yellow legend, on the component side only (the solder side has
     none: looked at on both scans): b 0.095 or more and L 0.55 or more, not
     on a pad, grown by 0.15 mm for the strokes' blurred edges.
   the rest: lacquer over laminate, the dark rim round a pad's solder, the
     component side's grey ring round some pads and the solder side's olive
     halo round its pads (where the lacquer stops short of the pad: grey,
     a about -0.020 and L about 0.40, as dark in a as the laminate, so not
     copper; see 4).
2. The cue is greenness, -a, not lightness. On the solder side the olive
   halos round the pads are as light as copper under the lacquer (L 0.40
   against 0.43 to 0.66), and they touch from pin to pin along a DIP's rows,
   so a lightness cue would join every pin of a row (as the NES's did through
   its lacquer's bright rims). In -a the halo (0.020) sits with the laminate
   (0.017) and copper (0.06 to 0.12) stands apart. -a is smoothed by a
   Gaussian of 0.75 pixels taken over the lacquer alone (a normalised
   convolution), so the tin and the print do not bleed into it.
3. The level. The laminate's greenness is taken as a smooth surface,
   quadratic in x and y, fitted with outliers left out to the median -a of
   the laminate in 10 mm blocks every 5 mm, the laminate being, in each
   block, what lies under Otsu's split of -a less a first surface fitted to
   the blocks' 10th percentiles: the NES's level, which followed the light
   where an opening or a block's percentile followed the copper's density.
   Copper is -a over the level by more than Otsu's threshold on that
   difference over the lacquer, with hysteresis: a region over the threshold
   is kept if it holds a pixel as far over it as the laminate's median is
   under it.
4. Pads, from task 3, not from a rule about the light round them. Every pad
   task 3 found on a face is a disc of its solder's radius (half its blob's
   larger side, or 0.8 mm where the blob ran into more solder and is over
   2.6 mm across) and 0.15 mm more, which takes in the dark rim round the
   solder (the pads' radial profile on both faces, every 7th drilled pad:
   solder to 0.78 mm, the rim darkest at 0.85 to 0.94 mm). A track joins a
   pad where its green reaches that disc. The NES cut its pads off their
   tracks with a rim rule, and its rims were wider than the gap between two
   pins; here the halo is not copper by colour (2), so no rim rule is needed.
5. Copper: the green, the tin and the pads' discs, and the one pixel between
   tin and green where solder meets a track (a mix of the two colours, so
   neither). A drill's hole inside it
   is filled (a piece of not-copper under 2 square mm that is mostly grey).
   Pieces under 0.15 square mm are dropped, and so are pieces under 1 square
   mm that are not pad-shaped (no more than 1.8 to 1, at least 0.45 of their
   box filled): specks of noise, not copper.
6. Copper under the print is recovered, before the specks are dropped (5),
   only where copper either side of the print continues in line: along the line straight across the print's
   stroke (the nearest of 8 lines to the stroke's normal, from the print
   mask's structure tensor), the print is crossed within 1.2 mm, and on both
   sides the copper begins at the print's edge and runs on for at least
   0.3 mm the same way. A wider block of print, and print over laminate, is
   left as print. (The NES's rule, kept: on its scan a slanting line from a
   gap between two tracks reached a track on each side and joined them.)

The checks, against the plan's table (fixed before the measurements, and
the details below fixed on 13 October 2026 before any check was run):
coverage, each face's copper over the board's area, 10 to 50 per cent;
drills in copper (task 3's drills: copper on at least half of a ring 0.1 mm
outside the drill's edge, a drill filled with solder taken as 0.6 mm), at
least 95 per cent on each face; the known nets: for each IC whose footprint
and pin 1 task 3 found and whose GND and +5V pins data/ic-table.json gives
with a source, those pins, each joined to both faces (a through-hole lead),
and the faces joined at every drill; at least 20 ICs, at least 90 per cent of
their GND pins in one net and of their +5V pins in another, and no net
holding both. Then I5 against the component side: I5 registered to the
board frame by a homography on chip pins marked by hand (data/marks.json
"i5Pins"), each chip held out of the fit in turn (the plan's "I5 to I1" row:
median at most 1.0 mm passes, over 2.0 stops); then the component side's
copper, drawn into I5's pixels through that fit, against the copper seen on
I5 where I5 shows the board (see i5_trace), intersection over union at least
0.50. The nets check and the I5 check are held out: the method was fixed, and
the SHA-256 of this script and its data written in the journal with the time,
before either was first run.

Writes data/copper.json, the track map site/src/assets/tracks/
bbc-micro-board.webp (lossless; red the component side's copper, green the
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

R = 16                       # pixels per millimetre of the traced grid
MAP_PPMS = (8, 7, 6)         # the map's pixels per millimetre: the highest inside the budget (the plan)
MAP_BUDGET = 600_000         # bytes (the plan)
MAP_FILE = common.REPO / 'site' / 'src' / 'assets' / 'tracks' / 'bbc-micro-board.webp'

# --- the probes (13 October 2026; boxes in the 16 px/mm grid, x then y; 5th / 50th / 95th percentiles) ---
# Laminate under the lacquer, component side, x 3885-3940 y 1835-1862: L 0.263 / 0.278 / 0.299, a -0.021 /
#   -0.016 / -0.010, sRGB median (34, 43, 34); solder side, x 3867-3880 y 1872-1898: L 0.240 / 0.282 / 0.391,
#   a -0.038 / -0.021 / -0.017, sRGB (33, 45, 35).
# Copper under the lacquer, component side, the pour at x 3683-3740 y 1672-1684: L 0.429 / 0.472 / 0.669,
#   a -0.104 / -0.099 / -0.058, b 0.030 / 0.054 / 0.058, sRGB (26, 109, 57). Thin tracks, across their
#   width (component side down x 3790 from y 1820; solder side along y 1700 from x 3680): peaks of L 0.59 and
#   0.66 to 0.92 and of a -0.08 and -0.08 to -0.12, against the laminate's L 0.22 to 0.30 and a -0.01 to -0.03.
# The solder side's halo round a pad, x 3790-3820 y 1812-1822: L 0.277 / 0.397 / 0.586, a -0.058 / -0.022 /
#   -0.009, sRGB (64, 75, 59); x 3795-3810 y 1815-1819: L 0.355 / 0.402 / 0.442, a -0.034 / -0.020 / -0.012.
# The print, x 3863-3868 y 1795-1885: L 0.723 / 0.783 / 0.884, b 0.132 / 0.149 / 0.159, sRGB (212, 183, 39).
#   PRINT_MIN_B is half way between the copper's 95th percentile of b (0.058) and the print's 5th (0.132).
# Solder on a pad, x 3797-3807 y 1796-1804: component side L 0.361 / 0.670 / 0.988, C 0.004 / 0.011 / 0.018;
#   solder side L 0.657 / 0.747 / 0.906, C 0.004 / 0.012 / 0.019. TIN_C is twice the solder's 95th
#   percentile of C; TIN_MIN_L is over the halo's median L (0.40) and its 95th in the second box (0.44), and
#   under the solder side's solder's 5th (0.657): the component side's darker specks sit inside the pads' discs.
# The pads' radial profile, every 7th pad task 3 placed at a drill (400 a face), median L by distance from the
#   centre: solder (L 0.69 to 0.75) out to 0.75 mm, the rim darkest at 0.85 to 0.94 mm (L 0.36 to 0.41), then
#   L 0.41 to 0.47 with a -0.02 to -0.04 out to 1.5 mm: the halo, or the grey ring, and neighbours' tracks.
SMOOTH_PX = 0.75
TIN_C, TIN_MIN_L = 0.035, 0.60
PRINT_MIN_B, PRINT_MIN_L = 0.095, 0.55
PRINT_GROW_MM = 0.15
PRINT_MIN_MM2 = 0.02
PAD_EDGE_MM = 0.15
PAD_MAX_R_MM, PAD_TYPICAL_R_MM = 1.3, 0.8
LEVEL_BLOCK_MM = 10.0
LEVEL_FIRST_Q = 10
MIN_PIECE_MM2 = 0.15
SMALL_PIECE_MM2 = 1.0
PAD_ASPECT, PAD_FILL = 1.8, 0.45
FILL_HOLE_MM2 = 2.0
RECOVER_GAP_MM = 1.2
RECOVER_RUN_MM = 0.3
DRILL_RING_MM = 0.1
DRILL_D_UNKNOWN_MM = 0.6     # a drill filled with solder has no diameter: a via's
POINT_R_MM = 0.5             # a pin's net: the commonest copper within this of its centre

# The plan's table, and its rows' details fixed on 13 October 2026 before any check was run
COVERAGE = (0.10, 0.50)
DRILLS_MIN = 0.95
NETS_MIN_CHIPS = 20
NETS_MIN_SHARE = 0.90
I5_REG_PASS_MM, I5_REG_STOP_MM = 1.0, 2.0
I5_IOU_MIN = 0.50
THRESHOLDS = {
    'coverage': 'each face\'s copper 10 to 50 per cent of the board',
    'drillsInCopper': 'at least 95 per cent on each face',
    'nets': ('at least 20 ICs; at least 90 per cent of their GND pins in one net and of their +5V pins in another; '
             'the two never connected'),
    'i5Registration': 'I5 to the board frame on chip pins, each chip held out: median at most 1.0 mm passes, over 2.0 mm stops',
    'againstI5': 'the component side against I5 where I5 shows the board: intersection over union at least 0.50',
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


def smooth_over(v, ok, sigma):
    """v smoothed by a Gaussian over the pixels in ok alone (a normalised
    convolution): what is not lacquer does not bleed into it."""
    w = cv2.GaussianBlur(ok.astype(np.float32), (0, 0), sigma)
    s = cv2.GaussianBlur(np.where(ok, v, 0).astype(np.float32), (0, 0), sigma)
    with np.errstate(invalid='ignore', divide='ignore'):
        out = s / w
    return np.where(w > 1e-3, out, v).astype(np.float32)


def _blocks(v, ok, q, min_px, b):
    H, W = v.shape
    pts = []
    for y in range(0, max(1, H - b // 2), b // 2):
        for x in range(0, max(1, W - b // 2), b // 2):
            s = ok[y:y + b, x:x + b]
            if s.sum() >= min_px:
                pts.append((x + s.shape[1] / 2, y + s.shape[0] / 2, float(np.percentile(v[y:y + b, x:x + b][s], q))))
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


def level_surface(v, ok, block_px=None):
    """The laminate's level (the module's docstring, 3), in blocks of
    LEVEL_BLOCK_MM unless block_px is given."""
    b = block_px or mm(LEVEL_BLOCK_MM)
    min_px = int(0.06 * b ** 2)
    first, _ = _surface(_blocks(v, ok, LEVEL_FIRST_Q, min_px, b), v.shape)
    split = otsu(np.clip((v - first)[ok], -0.1, 0.2))
    laminate = ok & ((v - first) < split)
    level, info = _surface(_blocks(v, laminate, 50, min_px, b), v.shape)
    return level, dict(info, firstSplit=split)


def hysteresis(cand, d, lo, hi):
    weak = cand & (d > lo)
    n, labels = cv2.connectedComponents(weak.astype(np.uint8), connectivity=8)
    keep = np.zeros(n, bool)
    keep[np.unique(labels[cand & (d > hi)])] = True
    keep[0] = False
    return keep[labels]


def pad_discs(shape, pads, edge_mm=PAD_EDGE_MM):
    """Discs over the pads ((x, y, r) in mm, r the solder's radius), each grown by edge_mm."""
    out = np.zeros(shape, np.uint8)
    for x, y, r in pads:
        cv2.circle(out, (int(round(x * R)), int(round(y * R))), int(round((r + edge_mm) * R)), 1, -1)
    return out > 0


def pad_radius(p):
    """A pad's solder radius in mm from task 3's blob: half its larger side, or,
    where the blob ran into more solder (over 2.6 mm across), 0.8 mm."""
    r = max(p['w'], p['h']) / 2
    return r if r <= PAD_MAX_R_MM else PAD_TYPICAL_R_MM


def clean(copper, grey):
    """The module's docstring, 5: drills' holes filled, specks and small
    pieces that are not pad-shaped dropped."""
    n, labels, stats = pieces(~copper)
    area = stats[:, cv2.CC_STAT_AREA]
    greyn = np.bincount(labels.ravel(), weights=grey.ravel().astype(float), minlength=n)
    fill = (area < FILL_HOLE_MM2 * R * R) & (greyn >= 0.5 * area)
    fill[0] = False
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


def recover_under_print(copper, printed):
    """Copper under the print where copper either side continues in line (the
    module's docstring, 6). copper is the copper found off the print."""
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
    """One face, in the grid (the module's docstring, 1 to 6). rgb: the face
    resampled onto the grid; inside: the board; pads: (x, y, r) in mm, the
    pads task 3 found on this face, r the solder's radius; hidden: what hides
    the board and is neither copper nor print, marked by hand. Returns copper
    (with what was recovered under the print), print, hidden, recovered, and
    the figures behind them."""
    L, A, B = common.oklab(rgb)
    A1, B1 = cv2.GaussianBlur(A, (0, 0), 1.0), cv2.GaussianBlur(B, (0, 0), 1.0)
    C1 = np.hypot(A1, B1)
    L1 = cv2.GaussianBlur(L, (0, 0), 1.0)
    grow = disc(PRINT_GROW_MM * R)
    on_pad = pad_discs(L.shape, pads) & inside
    hid = np.zeros(L.shape, bool) if hidden is None else cv2.dilate(hidden.astype(np.uint8), grow) > 0
    hid &= inside
    printed = np.zeros(L.shape, bool)
    if side == 'top':
        yellow = inside & (B1 >= PRINT_MIN_B) & (L1 >= PRINT_MIN_L) & ~on_pad & ~hid
        n, labels, stats = pieces(yellow)
        keep = stats[:, cv2.CC_STAT_AREA] >= PRINT_MIN_MM2 * R * R
        keep[0] = False
        printed = (cv2.dilate(keep[labels].astype(np.uint8), grow) > 0) & inside & ~on_pad & ~hid
    tin = inside & (C1 < TIN_C) & (L1 >= TIN_MIN_L) & ~printed & ~hid
    grey = inside & (C1 < TIN_C) & ~printed & ~hid
    lacquer = inside & ~tin & ~printed & ~hid & ~on_pad
    G = smooth_over(-A, lacquer, SMOOTH_PX)
    others = cv2.dilate((~lacquer).astype(np.uint8), np.ones((5, 5), np.uint8)) > 0
    core = lacquer & ~others
    level, info = level_surface(G, core)
    dG = G - level
    t = otsu(np.clip(dG[core], -0.1, 0.2))
    m0 = float(np.median(dG[core][dG[core] <= t]))
    high = 2 * t - m0
    green = hysteresis(lacquer, dG, t, high)
    # the one pixel where solder meets green copper is a mix of the two, neither grey enough for tin nor green
    # enough for copper (seen on the made-up board where solder runs over a track): it joins them
    k3 = np.ones((3, 3), np.uint8)
    seam = (cv2.dilate(tin.astype(np.uint8), k3) > 0) & (cv2.dilate(green.astype(np.uint8), k3) > 0) & lacquer & ~green
    found = green | tin | seam | on_pad
    # under the print first, then the specks: a track crossed by several strokes of print is in short pieces
    # until it is recovered under them, and the specks rule dropped those pieces (seen on I1 at x 3880, y 1690
    # to 1780 in the grid, a track across an IC's outline and its "A")
    recovered = recover_under_print(found, printed) if printed.any() else np.zeros_like(printed)
    copper = clean(found | recovered, grey) & inside
    recovered &= copper
    return {'copper': copper, 'print': printed, 'hidden': hid, 'recovered': recovered,
            'threshold': t, 'high': high, 'laminateMedian': m0, 'level': info, 'shares': {
                'tin': float(tin[inside].mean()), 'green': float((copper & green)[inside].mean()),
                'pads': float(on_pad[inside].mean())}}


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
    out = {
        'coverage': ok(all(COVERAGE[0] <= cov[f] <= COVERAGE[1] for f in ('top', 'bottom'))),
        'drillsInCopper': ok(all(dr[f] >= DRILLS_MIN for f in ('top', 'bottom'))),
        'nets': ok(n['chips'] >= NETS_MIN_CHIPS and not n['touching']
                   and all(n[k]['pins'] > 0 and n[k]['inLargest'] >= NETS_MIN_SHARE * n[k]['pins'] - 1e-9 for k in ('gnd', 'vcc'))),
    }
    if 'againstI5' in c:
        med = c['againstI5']['registration']['heldOutMm']['median']
        out['i5Registration'] = 'pass' if med <= I5_REG_PASS_MM else ('STOP' if med > I5_REG_STOP_MM else 'between pass and stop')
        out['againstI5'] = ok(c['againstI5']['iou'] >= I5_IOU_MIN)
    return out


# --- I5 -------------------------------------------------------------------------------------

# I5 (8BS's photograph of a populated Issue 7, about 5.4 px/mm, in its pixels as stored) is not a scan:
# its light and colours are its own. Probed on 13 October 2026 in I5's pixels (x then y; 5th / 50th / 95th
# percentiles): laminate under the lacquer, x 905-945 y 641-644: L 0.341 / 0.358 / 0.375, a -0.052 / -0.049 /
# -0.046, C 0.053 / 0.055 / 0.059, sRGB (14, 69, 76); copper under the lacquer, x 995-1045 y 624-628: L 0.624 /
# 0.746 / 0.788, a -0.133 / -0.124 / -0.104, sRGB (14, 197, 190); a chip's body, x 905-950 y 605-630: a -0.001 /
# 0.003 / 0.009, C 0.000 / 0.009 / 0.022; the table, x 20-80 y 600-700: a 0.026 / 0.037 / 0.050; a pin's leg
# (x 1055-1060 y 674-679): C 0.007 / 0.016 / 0.030. I5's print is white (I1's is yellow), so low in chroma too.
# Where I5 shows the board is where it is the lacquer's teal: a at most -0.03 (under the laminate's -0.046) and
# chroma at least 0.04 (under the laminate's 0.053, over a body's, a leg's and the print's), less 0.5 mm round
# every edge for parts' shadows and the photograph's blur. Copper there is -a over the laminate's level, by
# Otsu's threshold and the same hysteresis as the scans (3), the level fitted in blocks of 54 pixels (10 mm).
I5_GREEN_A, I5_MIN_C = -0.03, 0.04
I5_SHOWS_ERODE_MM = 0.5
I5_PX_PER_MM = 5.4                   # only to size the level's blocks and the erosion; the fit measures the scale


def i5_trace(rgb):
    """Where I5 shows the board, and the copper seen there (the comment
    above): (shows, copper, figures)."""
    L, A, B = common.oklab(rgb)
    A1, B1 = cv2.GaussianBlur(A, (0, 0), 1.0), cv2.GaussianBlur(B, (0, 0), 1.0)
    teal = (A1 <= I5_GREEN_A) & (np.hypot(A1, B1) >= I5_MIN_C)
    n, labels, stats = pieces(teal)
    big = stats[:, cv2.CC_STAT_AREA] >= 0.02 * teal.size    # the board's lacquer, not a teal speck on a part
    big[0] = False
    shows = cv2.erode(big[labels].astype(np.uint8), disc(I5_SHOWS_ERODE_MM * I5_PX_PER_MM)) > 0
    G = -A
    level, info = level_surface(G, shows, block_px=int(round(10 * I5_PX_PER_MM)))
    d = G - level
    t = otsu(np.clip(d[shows], -0.1, 0.2))
    m0 = float(np.median(d[shows][d[shows] <= t]))
    copper = hysteresis(shows, d, t, 2 * t - m0)
    return shows, copper, {'threshold': t, 'level': info, 'showsShare': float(shows.mean())}


def top_in_i5(top, x0, y0, fit, shape):
    """The component side's mask (in the grid) drawn into I5's pixels
    through the fitted homography from I5 to the board frame: (mask, on the
    board)."""
    H, W = shape
    u, v = np.meshgrid(np.arange(W, dtype=float), np.arange(H, dtype=float))
    q = common.transform('homography', fit['params'], np.c_[u.ravel(), v.ravel()])
    gx = np.round((q[:, 0] - x0) * R - 0.5).astype(int)
    gy = np.round((q[:, 1] - y0) * R - 0.5).astype(int)
    ok = (gx >= 0) & (gy >= 0) & (gx < top.shape[1]) & (gy < top.shape[0])
    out = np.zeros(H * W, bool)
    out[ok] = top[gy[ok], gx[ok]]
    return out.reshape(H, W), ok.reshape(H, W)


def register_i5(i5_px, board_mm, chips):
    """I5's pixels to the board frame by a homography on chip pins marked by
    hand, each chip's pins held out of the fit together (the plan's "I5 to
    I1" row). i5_px and board_mm are (N, 2); chips one label a point."""
    fit = common.fit_held_out(np.asarray(i5_px, float), np.asarray(board_mm, float), 'homography', list(chips))
    return {'model': 'homography', 'points': fit['n'], 'chips': len(set(chips)), 'folds': 'each chip held out in turn',
            'params': fit['params'], 'fitMm': fit['fitMm'], 'heldOutMm': fit['heldOutMm'], 'worst': fit['worst'],
            'heldOutEach': fit['heldOutEach']}


def iou_where(a, b, shows):
    """Intersection over union of two masks, over the pixels in shows alone."""
    a, b = a & shows, b & shows
    return {'iou': float((a & b).sum() / max((a | b).sum(), 1)), 'comparedPx': int(shows.sum())}


# --- the frame and the faces ------------------------------------------------------------------

def solder_to_px(params):
    """Board millimetres to I2's mirrored pixels: the inverse of the solder
    side's saved cubic map (registration.json), by fixed-point iteration on
    the homography's output (the cubic correction is small and smooth)."""
    Hi = np.linalg.inv(np.array(params['H']))
    centre, half = np.array(params['centre']), np.array(params['half'])
    px_, py_ = np.array(params['poly']['x']), np.array(params['poly']['y'])

    def to_px(q):
        q = np.asarray(q, float).reshape(-1, 2)
        b = q.copy()
        for _ in range(30):
            T = common._cubic_terms(b, centre, half)
            nb = q - np.c_[T @ px_, T @ py_]
            done = np.abs(nb - b).max() < 1e-10
            b = nb
            if done:
                break
        return common._homogeneous(Hi, b)
    return to_px


def grid(outline):
    """The grid over the outline's box: (x0, y0, W, H)."""
    o = np.asarray(outline, float)
    (x0, y0), (x1, y1) = o.min(0), o.max(0)
    return float(x0), float(y0), int(round((x1 - x0) * R)), int(round((y1 - y0) * R))


def resample(rgb, to_px, x0, y0, W, H, rows=512):
    out = np.empty((H, W, 3), np.uint8)
    for r0 in range(0, H, rows):
        r1 = min(H, r0 + rows)
        u, v = np.meshgrid(x0 + (np.arange(W) + 0.5) / R, y0 + (np.arange(r0, r1) + 0.5) / R)
        p = to_px(np.c_[u.ravel(), v.ravel()])
        out[r0:r1] = cv2.remap(rgb, p[:, 0].reshape(r1 - r0, W).astype(np.float32), p[:, 1].reshape(r1 - r0, W).astype(np.float32),
                               cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=(255, 255, 255))
    return out


def board_mask(outline, holes, x0, y0, W, H):
    """The board in the grid: inside the outline, out of the mounting holes."""
    m = np.zeros((H, W), np.uint8)
    pts = np.round((np.asarray(outline, float) - [x0, y0]) * R - 0.5).astype(np.int32)
    cv2.fillPoly(m, [pts.reshape(-1, 1, 2)], 1)
    for h in holes:
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
    fps = {f['ref']: f for f in reg['footprints'] if f.get('ref')}
    chips, excluded = [], []
    for ic in table['ics']:
        ref = ic['ref']
        f = fps.get(ref)
        why = None
        if ic.get('excludedWhy'):
            why = ic['excludedWhy']
        elif ic.get('gnd') is None or ic.get('vcc') is None or not ic.get('pinoutSource'):
            why = 'no GND or +5V pin with a source'
        elif f is None:
            why = 'no footprint in registration.json'
        elif not f.get('pin1'):
            why = 'task 3 found no pin 1'
        elif f['pins'] != ic['pins']:
            why = f'its footprint has {f["pins"]} pins, its package {ic["pins"]}'
        if why:
            excluded.append([ref, why])
            continue
        p = {}
        for k in ('gnd', 'vcc'):
            pad = reg['pads'][f['pads'][ic[k] - 1]]
            p[k] = ((pad['x'] - x0) * R - 0.5, (pad['y'] - y0) * R - 0.5)
        chips.append({'ref': ref, **p})
    return chips, excluded


def script_hashes():
    """The SHA-256 of this script and of the data it reads, for the journal's
    record of when the method was fixed."""
    files = [common.TOOL / 'board_trace.py', common.TOOL / 'common.py', common.TOOL / 'board_frame.py'] + \
        [common.DATA / n for n in ('frame.json', 'registration.json', 'ic-table.json', 'marks.json')]
    return {str(f.relative_to(common.REPO)): common.sha256(f) for f in files}


CLOSE = (('IC1', 'cpu'), ('IC56', 'ram'), ('IC6', 'video'), ('IC14', 'bus'))


def overlays(faces, inside, x0, y0, reg, chips, nets):
    """Pictures to look at, in out/: each face with its copper's edge (red),
    the print (blue) and what was recovered under it (yellow); close-ups of
    the processor, the RAM block, the video ULA and the data bus buffer, the
    component side above the solder side; and the GND and +5V pins coloured
    by net (green: in the largest GND net, magenta: in the largest +5V net,
    white: elsewhere)."""
    from PIL import Image, ImageDraw
    common.OUT.mkdir(exist_ok=True)
    sheets = {}
    for name, (rgb, t) in faces.items():
        img = np.clip(rgb.astype(np.float32) * 1.6, 0, 255).astype(np.uint8)
        c = t['copper'].astype(np.uint8)
        edge = (c - cv2.erode(c, np.ones((3, 3), np.uint8))) > 0
        img[edge] = (255, 40, 40)
        sel = t['print'] & ~t['recovered']
        img[sel] = (img[sel] * 0.5 + np.array([0, 60, 255]) * 0.5).astype(np.uint8)
        img[t['recovered']] = (255, 230, 0)
        img[t['hidden']] = img[t['hidden']] // 3
        sheets[name] = img
        Image.fromarray(img[::2, ::2]).save(common.OUT / f'trace-{name}-half.jpg', quality=90)
        flat = np.zeros(img.shape, np.uint8)
        flat[inside] = (0, 50, 0)
        flat[t['copper']] = (230, 120, 30)
        flat[t['print']] = (40, 90, 255)
        flat[t['recovered']] = (255, 230, 0)
        flat[t['hidden']] = (90, 90, 90)
        Image.fromarray(flat[::2, ::2]).save(common.OUT / f'trace-{name}-masks.png')
        Image.fromarray(t['copper']).save(common.OUT / f'trace-{name}-copper.png')
        Image.fromarray(t['print']).save(common.OUT / f'trace-{name}-print.png')
    fps = {f['ref']: f for f in reg['footprints'] if f.get('ref')}
    for ref, name in CLOSE:
        f = fps.get(ref)
        if f is None:
            continue
        a, b, c_, d = f['box']
        X0, Y0 = max(0, int((a - 6 - x0) * R)), max(0, int((b - 6 - y0) * R))
        X1, Y1 = int((c_ + 6 - x0) * R), int((d + 6 - y0) * R)
        row = np.concatenate([sheets['top'][Y0:Y1, X0:X1], sheets['bottom'][Y0:Y1, X0:X1]], 0)
        Image.fromarray(row).save(common.OUT / f'trace-close-{name}.png')
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
            d.ellipse([x - 12, y - 12, x + 12, y + 12], outline=fill, width=3)
            d.text((x + 14, y - 6), f"{c['ref']} {k}", fill=fill)
    im.resize((im.width // 2, im.height // 2)).save(common.OUT / 'trace-nets.png')


def faces_of(fj, reg, marks):
    """Both faces resampled onto the grid and traced: (faces, inside, x0, y0)."""
    frame = board_frame.Frame(fj['pxPerMm']['x'], fj['pxPerMm']['y'], math.radians(fj['rotationDeg']), fj['origin'])
    x0, y0, W, H = grid(fj['outline'])
    inside = board_mask(fj['outline'], reg['holes'], x0, y0, W, H)
    to_px_back = solder_to_px(reg['solder']['params'])
    hidden = np.zeros((H, W), bool)
    for h in marks.get('hidden', {}).get('marks', []):
        q = np.array(h['boxMm'], float).reshape(-1, 2)
        a = np.round((q - [x0, y0]) * R).astype(int)
        hidden[max(0, a[:, 1].min()):a[:, 1].max(), max(0, a[:, 0].min()):a[:, 0].max()] = True
    faces = {}
    for name, sid, to_px in (('top', 'I1', frame.to_px), ('bottom', 'I2', to_px_back)):
        rgb = resample(common.read_rgb(sid, mirror=(name == 'bottom')), to_px, x0, y0, W, H)
        pads = [(p['x'] - x0, p['y'] - y0, pad_radius(p)) for p in reg['pads'] if p['face'] in (name, 'both')]
        faces[name] = (rgb, trace_face(rgb, inside, pads=pads, hidden=hidden if name == 'top' else None, side=name))
        t = faces[name][1]
        print(f"{name}: Otsu d(-a) {t['threshold']:.4f}; level from {t['level']['kept']} of {t['level']['blocks']} blocks, robust sd "
              f"{t['level']['robustSd']:.4f}; copper {t['copper'][inside].mean() * 100:.1f}% of the board "
              f"(green {t['shares']['green'] * 100:.1f}%, tin {t['shares']['tin'] * 100:.1f}%, pads {t['shares']['pads'] * 100:.1f}%)")
    return faces, inside, x0, y0, frame


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--map-ppm', type=int, default=None, help='the map\'s pixels per millimetre, chosen before the final run')
    ap.add_argument('--look', action='store_true',
                    help='trace both faces, draw the overlays and print the map\'s size at each of MAP_PPMS; run no check')
    ap.add_argument('--hashes', action='store_true', help='print the SHA-256 of this script and its data, and stop')
    args = ap.parse_args()
    if args.hashes:
        for k, v in script_hashes().items():
            print(v, k)
        return 0
    warnings.simplefilter('ignore', RuntimeWarning)
    fj, reg, table, marks = load('frame.json'), load('registration.json'), load('ic-table.json'), load('marks.json')
    faces, inside, x0, y0, frame = faces_of(fj, reg, marks)
    top, bottom = faces['top'][1], faces['bottom'][1]
    printed = top['print'] & inside
    if args.look:
        for ppm in MAP_PPMS:
            print(f'map at {ppm} px/mm: {len(webp(make_map(top["copper"], bottom["copper"], printed, inside, ppm)))} bytes')
        overlays(faces, inside, x0, y0, reg, None, None)
        return 0
    if args.map_ppm not in MAP_PPMS:
        sys.exit('give --map-ppm, chosen beforehand from --look: the highest of 8, 7 or 6 inside 600,000 bytes')
    area = inside.sum()
    coverage = {'top': float(top['copper'][inside].sum() / area), 'bottom': float(bottom['copper'][inside].sum() / area),
                'print': float(printed.sum() / area)}
    drills = [((d['x'] - x0), (d['y'] - y0), d['d']) for d in reg['drills']]
    din = {f: drills_in_copper(faces[f][1]['copper'], drills) for f in ('top', 'bottom')}
    drills_share = {f: float(np.mean(din[f])) for f in din}
    chips, excluded = chip_pins(reg, table, x0, y0)
    nets = nets_check(top['copper'], bottom['copper'], [((x * R) - 0.5, (y * R) - 0.5) for x, y, _ in drills], chips)
    # I5: registered on the chip pins marked by hand, each chip held out; then compared where it shows the board
    fps = {f['ref']: f for f in reg['footprints'] if f.get('ref')}
    pm = marks['i5Pins']['marks']
    i5_px = np.array([m['i5Px'] for m in pm], float)
    board_mm = np.array([[reg['pads'][fps[m['ref']]['pads'][m['pin'] - 1]][k] for k in ('x', 'y')] for m in pm], float)
    i5reg = register_i5(i5_px, board_mm, [m['ref'] for m in pm])
    i5 = common.read_rgb('I5')
    shows, i5copper, i5info = i5_trace(i5)
    top_i5, on_grid = top_in_i5(top['copper'], x0, y0, i5reg, i5.shape[:2])
    inside_i5, _ = top_in_i5(inside, x0, y0, i5reg, i5.shape[:2])
    print_i5, _ = top_in_i5(printed, x0, y0, i5reg, i5.shape[:2])
    where = shows & on_grid & inside_i5 & ~print_i5
    cmp_ = iou_where(top_i5, i5copper, where)
    against = {'registration': {k: i5reg[k] for k in ('model', 'points', 'chips', 'folds', 'params', 'fitMm', 'heldOutMm', 'worst')},
               'iou': cmp_['iou'], 'comparedPx': cmp_['comparedPx'],
               'comparedMm2': None,
               'i5Trace': i5info,
               'what': ('I5 registered to the board frame by a homography on chip pins marked by hand (marks.json i5Pins), each chip held out of '
                        'the fit in turn; then the component side\'s copper drawn into I5\'s pixels through the fit to every pin, against the '
                        'copper seen on I5, over the pixels where I5 shows the board (its lacquer\'s teal, less 0.5 mm round every edge) and '
                        'I1 shows no print; intersection over union')}
    # the area compared, in square millimetres of the board: each I5 pixel's area through the fit, summed
    ys, xs = np.nonzero(where)
    if len(xs):
        sample = np.c_[xs, ys][:: max(1, len(xs) // 20000)].astype(float)
        e = 0.5
        a_ = common.transform('homography', i5reg['params'], sample)
        b_ = common.transform('homography', i5reg['params'], sample + [e, 0])
        c_ = common.transform('homography', i5reg['params'], sample + [0, e])
        jac = np.abs((b_[:, 0] - a_[:, 0]) * (c_[:, 1] - a_[:, 1]) - (b_[:, 1] - a_[:, 1]) * (c_[:, 0] - a_[:, 0])) / e ** 2
        against['comparedMm2'] = float(jac.mean() * len(xs))
    hidden_print = printed.sum() / R ** 2
    rec = top['recovered'].sum() / R ** 2
    result = {
        'about': ('Task 4 of the BBC Micro models plan: the copper on both faces of the Issue 7 board and its printed legend, traced from the '
                  'bare scans I1 and I2 (amb5l) in the board frame of frame.json; the plan\'s checks on them, the component side also against '
                  'I5 (8BS); and the track map. Written by board_trace.py; every figure here is a measurement made on the date of the journal '
                  'entry that quotes it.'),
        'grid': {'pxPerMm': R, 'originMm': [x0, y0], 'width': int(inside.shape[1]), 'height': int(inside.shape[0]),
                 'what': 'The traced grid: pixel (u, v)\'s centre is board millimetre (originMm + ((u + 0.5) / pxPerMm, (v + 0.5) / pxPerMm))'},
        'coverage': coverage,
        'drillsInCopper': drills_share,
        'drills': {'n': len(drills), 'notInCopper': {f: [i for i, ok in enumerate(din[f]) if not ok] for f in din}},
        'nets': dict({k: nets[k] for k in ('chips', 'gnd', 'vcc', 'touching')}, excluded=excluded),
        'netsChips': [c['ref'] for c in chips],
        'pinNets': nets['pinNets'],
        'againstI5': against,
        'print': {'areaMm2': float(hidden_print), 'recoveredMm2': float(rec), 'recoveredShare': float(rec / max(hidden_print, 1e-9))},
        'trace': {f: {'otsuDA': faces[f][1]['threshold'], 'hysteresisHigh': faces[f][1]['high'], 'laminateMedianDA': faces[f][1]['laminateMedian'],
                      'level': faces[f][1]['level'], 'shares': faces[f][1]['shares']} for f in faces},
        'thresholds': THRESHOLDS,
        'methodFixed': script_hashes(),
    }
    result['verdicts'] = verdicts(result)
    m = make_map(top['copper'], bottom['copper'], printed, inside, args.map_ppm)
    data = webp(m)
    if len(data) > MAP_BUDGET:
        sys.exit(f'the map at {args.map_ppm} px/mm is {len(data)} bytes, over the budget')
    MAP_FILE.parent.mkdir(parents=True, exist_ok=True)
    MAP_FILE.write_bytes(data)
    result['mapPxPerMm'] = args.map_ppm
    result['map'] = {'width': int(m.shape[1]), 'height': int(m.shape[0]), 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(),
                     'file': 'site/src/assets/tracks/bbc-micro-board.webp', 'originMm': [x0, y0],
                     'what': ('Red the component side\'s copper, green the solder side\'s, blue the print, each 255 or 0; pixel (u, v)\'s centre '
                              'is board millimetre (originMm + ((u + 0.5) / mapPxPerMm, (v + 0.5) / mapPxPerMm)); lossless WebP'),
                     'budget': MAP_BUDGET, 'choice': 'the highest of 8, 7 or 6 px/mm inside the budget, chosen from --look before this run'}
    common.write_data('copper.json', result)
    v = result['verdicts']
    print(f"coverage: top {coverage['top'] * 100:.1f}%, bottom {coverage['bottom'] * 100:.1f}%, print {coverage['print'] * 100:.1f}%: {v['coverage']}")
    print(f"drills in copper: top {drills_share['top'] * 100:.1f}%, bottom {drills_share['bottom'] * 100:.1f}% of {len(drills)}: {v['drillsInCopper']}")
    print(f"nets: {nets['chips']} ICs; excluded {len(excluded)}; GND {nets['gnd']['inLargest']} of {nets['gnd']['pins']} in one net, "
          f"+5V {nets['vcc']['inLargest']} of {nets['vcc']['pins']}; touching {nets['touching']}: {v['nets']}")
    h = i5reg['heldOutMm']
    print(f"I5 to the board frame: {i5reg['points']} pins on {i5reg['chips']} chips, each chip held out: median {h['median']:.3f}, "
          f"p90 {h['p90']:.3f}, max {h['max']:.3f} mm: {v['i5Registration']}")
    print(f"top against I5: intersection over union {cmp_['iou']:.3f} over {against['comparedMm2'] or 0:.0f} mm2: {v['againstI5']}")
    print(f"print {hidden_print:.0f} mm2, copper recovered under it {rec:.0f} mm2; map {args.map_ppm} px/mm, {m.shape[1]} x {m.shape[0]}, {len(data)} bytes")
    overlays(faces, inside, x0, y0, reg, chips, nets)
    from PIL import Image
    ov = np.zeros(i5.shape, np.uint8)
    ov[where] = (40, 40, 40)
    ov[where & top_i5] = (200, 0, 0)
    ov[where & i5copper] = (0, 0, 200)
    ov[where & top_i5 & i5copper] = (230, 230, 230)
    Image.fromarray(ov).save(common.OUT / 'trace-against-i5.png')
    return 0


if __name__ == '__main__':
    sys.exit(main())
