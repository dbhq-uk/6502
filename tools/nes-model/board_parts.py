"""Task 5: every part on the NES board, placed from the bare scan's footprints
and named from photographs of populated boards, for both consoles; writes
data/parts.json and the module the site reads,
site/src/models/nes-famicom-board-parts.mjs.

    NES_MODEL_INPUTS=<folder> python board_parts.py

Everything is in task 2's board frame (frame.json: millimetres from the
board's top left corner as I1-front lies, x to the right, y down towards the
edge fingers), on task 3's footprints and pads (registration.json).

1. The ICs. Each IC is placed on its footprint: its place is the centre of the
   footprint's pads (all of them, in pin order as task 3 numbers them), its
   rotation the direction from pin 1 to the last pin of pin 1's row, in
   degrees in the board frame (0 when pin 1 is at the lower left and the row
   runs to the right, as every DIP on this board lies; positive turns from x
   towards y, so clockwise as the component side is seen). A footprint whose
   pad count is not its part's pin count (ic-table.json) is reported, not
   placed. U1 and U4 each have a 600 mil and a 300 mil footprint (task 3);
   the photographs show which the RAMs sit on (USE below, and step 2). The
   body is the package's typical size (PACKAGE_MM), not measured; the
   photographs' outlines of each body are recorded beside, not used.

2. Each photographed board on the scan's footprints. I4 (Evan-Amos's NTSC
   NES-CPU-07, top side) and I5 (its solder side) are registered to the board
   frame by a homography on the ICs' pins, and I3 (the PAL NES-CPU-11) by the
   one task 0 fitted, on its marks (marks.json "palLayout"). On I4 each DIP's
   midline is found at every pair of lead columns across it: the leads' middles
   across the row (where the light rises and falls along a band through the
   leads' flat shoulders) and the body's two long edges (where the light falls
   from a lead into the body), the midline half way between the edges; it is
   the midline of the pads of those two pins on the scan. Where the leads do
   not show as light flats (the MN74HC368s, U7 and U8: their leads are grey
   between two highlights) the midline's ends are marked by hand
   (marks.json "parts.I4.midlines"). On I5 each pin is its solder joint, the
   centre of the colourless disc round it (the lacquer is green), and is the
   pad on the scan. A homography starts from four pins marked by hand on U6
   and grows part by part. Then each part is held out in turn: the homography
   fitted on every other part's points, its own points through it, and the
   distance between the mean of where they land and the mean of where its
   pads are on the scan is its error. A part sits on the footprint of its
   name if that error is at most SITS_MM (2.0 mm, the plan's PAL layout row's
   "none over 2.0", chosen before any figure was seen; a pin pitch is 2.54).

3. The places against the KiCad redrawing (I2), a cross-check only: each
   IC's place against the centre of the same pads in the redrawing (for U1
   and U4, whose redrawn footprint holds both widths, the width the part sits
   on), after the similarity that fits them best; none may be more than
   KICAD_MM (3 mm, the plan's thresholds table) off. The connectors and
   crystals are recorded beside, not judged. Nothing is drawn from I2.

4. Connectors, passives and the crystals and modulators. Connectors are
   placed on their footprints; their bodies' outlines are marked by hand on
   I4 (marks.json "parts.outlines") and taken into the board frame through
   I4's homography, which is the board's plane: a body's top stands above it
   and leans away from the middle of the picture, by its height times its
   distance from the middle over the camera's distance (about 0.8 m for I4,
   from its 60 mm lens and 20 pixels a millimetre), so an outline read at a
   top 15 mm up and 100 mm out is about 2 mm out; each outline says where it
   was read. The passives are the two-lead (and three-lead) parts I4 shows,
   each on the scan's pads its leads go through (marks.json "parts.passives",
   each pad read off I4 with the scan's pads drawn on it), with a typical body
   for its kind (PASSIVE_MM). Each console's crystal and modulator are read off
   its own photograph: NTSC off I4, with the modulator's lid off; PAL off I3,
   the can.

5. Heights. The oblique photographs of a populated board (I7) are not
   fetched, so every height is typical, not measured, and HEIGHTS says so.

6. The module: generated, with a header naming its sources; no colours.
"""
import json
import math
import sys
from pathlib import Path

import numpy as np

import common

SITS_MM = 2.0
KICAD_MM = 3.0
PARTS_MJS = common.REPO / 'site' / 'src' / 'models' / 'nes-famicom-board-parts.mjs'

# The counters the machine keeps (task 6's NesChip, in the order of COUNTED in
# site/src/models/nes-famicom-access.mjs) and why an IC is never marked.
COUNTED = ('ppu', 'apu', 'pad1', 'pad2')
ALWAYS = ('cpu', 'ram', 'decoder', 'latch', 'cartridge', 'lockout')


# --- places ---------------------------------------------------------------------------

def place(pads, pins, rows=2):
    """A footprint's place: the centre of its pads, and its rotation, the
    direction from pin 1 to the last pin of pin 1's row (pin pins / rows),
    in degrees in the board frame."""
    P = np.asarray(pads, float).reshape(-1, 2)
    c = P.mean(0)
    end = P[pins // rows - 1] - P[0]
    return {'x': float(c[0]), 'y': float(c[1]), 'rotation': math.degrees(math.atan2(end[1], end[0]))}


def place_ics(footprints, table, use):
    """Each IC in the table on its footprint (the one `use` names for it, or
    the one of its own reference). Returns (placed, problems): a part with no
    footprint, or whose footprint's pad count is not its pin count, is a
    problem and is not placed."""
    by_ref = {f['ref']: f for f in footprints if f.get('ref')}
    placed, problems = [], []
    for ic in table['ics']:
        name = use.get(ic['ref'], ic['ref'])
        f = by_ref.get(name)
        if f is None:
            problems.append({'ref': ic['ref'], 'problem': f'no footprint named {name}: not placed'})
            continue
        if len(f['pads']) != ic['pins']:
            problems.append({'ref': ic['ref'], 'problem': f"its footprint has {len(f['pads'])} pads, its part {ic['pins']} pins: not placed"})
            continue
        p = place(f['pads'], ic['pins'])
        placed.append({'ref': ic['ref'], 'footprint': name, 'pins': ic['pins'], 'padCount': len(f['pads']), **p})
    return placed, problems


# --- the KiCad redrawing --------------------------------------------------------------

def kicad_check(scan, kicad, limit=KICAD_MM):
    """Each part's place against the redrawing's, after the similarity (turn,
    one scale, shift) taking the redrawing's places onto the scan's that fits
    them best. scan, kicad: {ref: (x, y)}, the same refs."""
    refs = sorted(scan)
    src = np.array([kicad[r] for r in refs], float)
    dst = np.array([scan[r] for r in refs], float)
    params = common._fit('similarity', src, dst)
    got = common.transform('similarity', params, src)
    per = {}
    for r, g, d in zip(refs, got, dst):
        per[r] = {'offMm': float(np.hypot(*(g - d))), 'dxMm': float(g[0] - d[0]), 'dyMm': float(g[1] - d[1])}
    worst = max(per.values(), key=lambda e: e['offMm'])['offMm']
    return {'perPart': per, 'maxMm': worst, 'medianMm': float(np.median([e['offMm'] for e in per.values()])),
            'scale': params['scale'], 'rotationDeg': params['rotationDeg'],
            'verdict': 'pass' if worst <= limit else 'fail', 'limitMm': limit}


def kicad_pads(path):
    """Every footprint's plated pads in the redrawing, in KiCad's millimetres:
    {ref: {"value", "pads": {name: [[x, y], ...]}}}, each pad where the
    footprint's place and turn put it (KiCad turns a footprint's pads by its
    angle anticlockwise as seen, with y down). Surface pads are taken only
    for a footprint that has no plated holes (the cartridge connector's
    fingers)."""
    root = common._sexpr(Path(path).read_text(encoding='utf8'))
    out = {}
    for fp in root[1:]:
        if not (isinstance(fp, list) and fp and fp[0] == 'footprint'):
            continue
        at = common._child(fp, 'at')
        ox, oy = float(at[1]), float(at[2])
        t = math.radians(float(at[3])) if len(at) > 3 else 0.0
        props = {p[1]: p[2] for p in fp[1:] if isinstance(p, list) and p and p[0] == 'property'}
        pads, smd = {}, {}
        for p in fp[1:]:
            if not (isinstance(p, list) and p and p[0] == 'pad') or p[2] == 'np_thru_hole':
                continue
            pa = common._child(p, 'at')
            x, y = float(pa[1]), float(pa[2])
            (smd if p[2] == 'smd' else pads).setdefault(p[1], []).append(
                [ox + x * math.cos(t) + y * math.sin(t), oy - x * math.sin(t) + y * math.cos(t)])
        out[props['Reference']] = {'value': props.get('Value'), 'pads': pads or smd}
    return out


def kicad_centre(fp, pins, spacing=None):
    """The centre of a redrawn footprint's pads 1 to `pins`. Where a pad name
    has more than one copper pad (U1's and U4's footprints hold both widths),
    the copy at `spacing` mm across from pin 1's row is taken."""
    pts = []
    first = np.array(fp['pads']['1'][0])
    for k in range(1, pins + 1):
        cands = [np.array(c) for c in fp['pads'][str(k)]]
        if len(cands) > 1:
            if spacing is None:
                raise ValueError(f'pad {k} has {len(cands)} copies: say which spacing')
            cands.sort(key=lambda c: abs(abs(_across(fp, c, first)) - spacing))
        pts.append(cands[0])
    return np.mean(pts, 0).tolist()


def _across(fp, c, first):
    """How far c lies from pin 1's row, across it (pin 1's row runs from pad 1
    to the pad whose single copy is furthest from it)."""
    ones = [np.array(v[0]) for v in fp['pads'].values() if len(v) == 1]
    far = max(ones, key=lambda q: np.hypot(*(q - first)))
    u = (far - first) / np.hypot(*(far - first))
    d = c - first
    return d[0] * -u[1] + d[1] * u[0]


# --- photographs ------------------------------------------------------------------------

def apply_h(H, pts):
    return common._homogeneous(np.asarray(H, float), np.asarray(pts, float).reshape(-1, 2))


def fit_h(src, dst):
    return np.array(common._fit('homography', np.asarray(src, float), np.asarray(dst, float))['H'])


def held_out_parts(points):
    """points: {ref: {"px": [[x, y], ...], "mm": [[x, y], ...]}}, a part's
    points on the photograph and where they are on the scan. Each part held
    out in turn: a homography from the photograph to the board fitted on every
    other part's points; the part's error is the distance between the mean of
    its points through it and the mean of their places on the scan."""
    out = {}
    for r in points:
        others = [q for q in points if q != r]
        src = [p for q in others for p in points[q]['px']]
        dst = [p for q in others for p in points[q]['mm']]
        H = fit_h(src, dst)
        got = apply_h(H, points[r]['px'])
        want = np.asarray(points[r]['mm'], float)
        d = got.mean(0) - want.mean(0)
        e = np.hypot(*(got - want).T)
        out[r] = {'points': len(want), 'heldOutMm': float(np.hypot(*d)), 'dxMm': float(d[0]), 'dyMm': float(d[1]),
                  'pointsMm': {'median': float(np.median(e)), 'max': float(e.max())}}
    return out


def _luma(rgb):
    rgb = np.asarray(rgb, float)
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def _runs(profile, thr, x0):
    on = profile > thr
    out, s = [], None
    for i, v in enumerate(on):
        if v and s is None:
            s = i
        if s is not None and (not v or i == len(on) - 1):
            out.append((s + x0, (i - 1 if not v else i) + x0))
            s = None
    return out


LEAD_RUN_PX = (8, 40)       # a lead's flat across the row, in pixels (about 20 px/mm: 0.4 to 2 mm)
LEAD_MATCH_PX = 15          # a lead this near its predicted place is that pin's
BAND_PX = 10                # the band through the leads' shoulders
BAND_OFFSETS = range(-10, 22, 2)
EDGE_SEARCH_PX = 40


def lead_row(L, pred, outward):
    """The leads of one row near their predicted places (pixels), and at each
    the body's edge: [(x, edge_y) or (None, None)] in the row's pin order.
    outward is +1 when the row's outside is down the picture, -1 when up. The
    band through the shoulders is the one, of offsets from the predicted row,
    whose light runs match the most pins."""
    pred = np.asarray(pred, float)
    y = int(round(pred[:, 1].mean()))
    x0, x1 = int(pred[:, 0].min()) - 30, int(pred[:, 0].max()) + 30
    best = None
    for off in BAND_OFFSETS:
        ys = (y - off - BAND_PX, y - off) if outward > 0 else (y + off, y + off + BAND_PX)
        prof = L[ys[0]:ys[1], x0:x1].mean(0)
        thr = (np.percentile(prof, 85) + np.percentile(prof, 20)) / 2
        rr = [r for r in _runs(prof, thr, x0) if LEAD_RUN_PX[0] <= r[1] - r[0] <= LEAD_RUN_PX[1]]
        cs = np.array([(a + b) / 2 for a, b in rr])
        xs = []
        for px, _ in pred:
            k = int(np.argmin(abs(cs - px))) if len(cs) else None
            xs.append(float(cs[k]) if k is not None and abs(cs[k] - px) < LEAD_MATCH_PX else None)
        n = sum(x is not None for x in xs)
        if best is None or n > best[0]:
            best = (n, xs)
    out = []
    for x in best[1]:
        if x is None:
            out.append((None, None))
            continue
        xi = int(round(x))
        col = L[y - EDGE_SEARCH_PX:y + EDGE_SEARCH_PX + 1, xi - 3:xi + 4].mean(1)
        g = np.diff(col)
        if outward > 0:     # the body above: light rises from the body into the lead going down
            e = y - EDGE_SEARCH_PX + int(np.argmax(g[:EDGE_SEARCH_PX])) + 0.5
        else:               # the body below: light falls from the lead into the body going down
            e = y + 1 + int(np.argmin(g[EDGE_SEARCH_PX + 1:])) + 0.5
        out.append((x, float(e)))
    return out


def dip_midline(rgb, pred_bottom, pred_top):
    """A DIP's midline on a photograph from above, at each pair of lead
    columns: pred_bottom the predicted places of pins 1 to N/2 (the lower row,
    its outside down the picture), pred_top those of pins N down to N/2 + 1,
    column by column. Each long edge of the body is a line fitted to its
    edge points; the midline is half way between them, at the mean of the two
    leads' middles. Returns, in pin order (pin k and pin N + 1 - k), (x, y)
    for each column where both leads were found and None for the others."""
    L = _luma(rgb)
    bot = lead_row(L, pred_bottom, +1)
    top = lead_row(L, pred_top, -1)
    be = [(x, e) for x, e in bot if x is not None]
    te = [(x, e) for x, e in top if x is not None]
    if len(be) < 2 or len(te) < 2:
        return []
    lb = np.polyfit([a for a, _ in be], [b for _, b in be], 1)
    lt = np.polyfit([a for a, _ in te], [b for _, b in te], 1)
    out = []
    for (xb, _), (xt, _) in zip(bot, top):
        if xb is None or xt is None:
            out.append(None)
            continue
        xm = (xb + xt) / 2
        out.append((float(xm), float((np.polyval(lb, xm) + np.polyval(lt, xm)) / 2)))
    return out


JOINT_MAX_CHROMA = 0.04     # a joint is grey; the lacquer round it is green (I5: joint 0.00 to 0.03, lacquer 0.06 to 0.08)
JOINT_AREA_PX = (150, 1400)
JOINT_HALF_PX = 22


def joint(rgb, x, y, half=JOINT_HALF_PX):
    """The solder joint nearest (x, y) on a photograph of the solder side: the
    centroid of the colourless disc (light and shade alike), holes filled; or
    None."""
    from scipy import ndimage
    x, y = int(round(x)), int(round(y))
    win = np.asarray(rgb)[y - half:y + half + 1, x - half:x + half + 1]
    _, A, B = common.oklab(win)
    m = np.hypot(A, B) < JOINT_MAX_CHROMA
    m = ndimage.binary_fill_holes(ndimage.binary_opening(m, np.ones((3, 3))))
    lab, n = ndimage.label(m)
    best = None
    for k in range(1, n + 1):
        ys, xs = np.nonzero(lab == k)
        if not (JOINT_AREA_PX[0] <= len(xs) <= JOINT_AREA_PX[1]):
            continue
        if xs.min() == 0 or ys.min() == 0 or xs.max() == 2 * half or ys.max() == 2 * half:
            continue
        cx, cy = xs.mean(), ys.mean()
        d = math.hypot(cx - half, cy - half)
        if best is None or d < best[0]:
            best = (d, cx, cy)
    if best is None or best[0] > half * 0.7:
        return None
    return [float(x - half + best[1]), float(y - half + best[2])]


# --- geometry ------------------------------------------------------------------------------

def inside(poly, p):
    """p strictly inside the polygon (ray casting; a point on an edge is out)."""
    x, y = p
    n = len(poly)
    c = False
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if _on_segment((x1, y1), (x2, y2), (x, y)):
            return False
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            c = not c
    return c


def _on_segment(a, b, p, tol=1e-9):
    (x1, y1), (x2, y2), (x, y) = a, b, p
    cross = (x2 - x1) * (y - y1) - (y2 - y1) * (x - x1)
    if abs(cross) > tol * max(1.0, math.hypot(x2 - x1, y2 - y1)):
        return False
    return min(x1, x2) - tol <= x <= max(x1, x2) + tol and min(y1, y2) - tol <= y <= max(y1, y2) + tol


def body_corners(b):
    """The four corners of a body: centre (x, y), length l along its
    rotation, width w across."""
    t = math.radians(b['rotation'])
    u = np.array([math.cos(t), math.sin(t)]) * b['l'] / 2
    v = np.array([-math.sin(t), math.cos(t)]) * b['w'] / 2
    c = np.array([b['x'], b['y']], float)
    return [(c - u - v).tolist(), (c + u - v).tolist(), (c + u + v).tolist(), (c - u + v).tolist()]


def overlap(a, b):
    """Two bodies' rectangles overlap (separating axes; touching is apart)."""
    A, B = np.array(body_corners(a)), np.array(body_corners(b))
    for P in (A, B):
        for i in range(4):
            e = P[(i + 1) % 4] - P[i]
            n = np.array([-e[1], e[0]])
            pa, pb = A @ n, B @ n
            if pa.max() <= pb.min() + 1e-9 or pb.max() <= pa.min() + 1e-9:
                return False
    return True


# --- the module ----------------------------------------------------------------------------

def _js(obj):
    return json.dumps(common.plain(obj, 3), ensure_ascii=False, separators=(',', ':'))


def module_text(parts):
    """The module the site reads, from parts.json's content."""
    head = [
        '// GENERATED by tools/nes-model/board_parts.py: do not edit by hand. Run it',
        '// again instead (tools/nes-model/README.md). Millimetres in the board frame:',
        '// from the board\'s top left corner as the component side lies with the edge',
        '// fingers at the bottom, x to the right, y down; rotation in degrees from x',
        '// towards y. Places come from the bare NES-CPU-10 scan\'s footprints; each',
        '// console\'s parts are read off photographs of populated boards. Heights are',
        '// typical, not measured, where HEIGHTS says measured: false.',
        '//',
        '// Sources (tools/nes-model/data/sources.json): ' + ', '.join(parts['sources']) + '.',
        '// The KiCad redrawing (I2) is a cross-check of the places only.',
        '',
    ]
    body = [
        f"export const BOARD = {_js(parts['board'])};",
        f"export const ICS = {_js(parts['ics'])};",
        f"export const CONNECTORS = {_js(parts['connectors'])};",
        f"export const PASSIVES = {_js(parts['passives'])};",
        f"export const OTHERS = {_js(parts['others'])};",
        f"export const HEIGHTS = {_js(parts['heights'])};",
    ]
    return '\n'.join(head + body) + '\n'


# --- the run -------------------------------------------------------------------------------

def load(name):
    with open(common.DATA / name, encoding='utf8') as f:
        return json.load(f)


class Board:
    """The scan's footprints and pads (registration.json), by reference."""

    def __init__(self, reg):
        self.pads = reg['pads']
        self.fp = {f['ref']: f for f in reg['footprints'] if f.get('ref')}

    def pin(self, ref, k):
        p = self.pads[self.fp[ref]['pads'][k - 1]]
        return [p['x'], p['y']]

    def pins(self, ref):
        return [self.pin(ref, k) for k in range(1, len(self.fp[ref]['pads']) + 1)]


# The footprint each IC sits on, where its reference names two (task 3): the
# RAMs on both photographed boards are 300 mil "skinny" DIPs (I4: MB8416A-15-SK,
# its body about 7 mm wide; I3: XRM6216-10, task 0), on the 300 mil footprint.
USE = {'U1': 'U1 (300 mil)', 'U4': 'U4 (300 mil)'}
I4_ORDER = ['U6', 'U3', 'U1', 'U2', 'U5', 'U4', 'U10', 'U9', 'U8', 'U7']


def _bootstrap(seeds):
    src = [s['mm'] for s in seeds]
    dst = [s['px'] for s in seeds]
    return src, dst


def _fit_mm_to_px(src, dst, parts):
    model = 'homography' if parts >= 6 else 'affine'
    return model, common._fit(model, np.asarray(src, float), np.asarray(dst, float))


def register_top(rgb, board, marks, order=I4_ORDER, rounds=2):
    """I4's points for each IC (step 2): {ref: {"px", "mm", "how"}}."""
    seed_src, seed_dst = _bootstrap([{'mm': board.pin(USE.get(s['ref'], s['ref']), s['pin']), 'px': s['px']} for s in marks['seeds']])
    hand = {m['ref']: m for m in marks['midlines']}
    got = {}
    model, params = _fit_mm_to_px(seed_src, seed_dst, 0)
    for _ in range(rounds):
        for ref in order:
            fpn = USE.get(ref, ref)
            n = len(board.fp[fpn]['pads'])
            h = n // 2
            if ref in hand:
                px = [m['px'] for m in hand[ref]['ends']]
                mm = [((np.array(board.pin(fpn, e['pins'][0])) + np.array(board.pin(fpn, e['pins'][1]))) / 2).tolist() for e in hand[ref]['ends']]
                got[ref] = {'px': px, 'mm': mm, 'how': 'midline ends marked by hand'}
            else:
                bottom = list(range(1, h + 1))
                top = list(range(n, h, -1))
                pb = common.transform(model, params, [board.pin(fpn, k) for k in bottom])
                pt = common.transform(model, params, [board.pin(fpn, k) for k in top])
                mid = dip_midline(rgb, pb, pt)
                px, mm = [], []
                for k, p in enumerate(mid):
                    if p is None:
                        continue
                    px.append(list(p))
                    mm.append(((np.array(board.pin(fpn, bottom[k])) + np.array(board.pin(fpn, top[k]))) / 2).tolist())
                got[ref] = {'px': px, 'mm': mm, 'how': f'midline at {len(px)} of {h} lead columns, found'}
            src = seed_src + [p for r in got for p in got[r]['mm']]
            dst = seed_dst + [p for r in got for p in got[r]['px']]
            model, params = _fit_mm_to_px(src, dst, sum(1 for r in got if len(got[r]['px']) >= 2))
    return got


JOINT_MATCH_PX = 12


def register_bottom(rgb, board, marks, order=I4_ORDER, rounds=2):
    """I5's points for each IC (step 2): every pin's solder joint."""
    seed_src, seed_dst = [], []
    for s in marks['seeds']:
        j = joint(rgb, *s['px'])
        seed_src.append(board.pin(USE.get(s['ref'], s['ref']), s['pin']))
        seed_dst.append(j if j is not None else s['px'])
    got = {}
    model, params = _fit_mm_to_px(seed_src, seed_dst, 0)
    for _ in range(rounds):
        for ref in order:
            fpn = USE.get(ref, ref)
            mm_all = board.pins(fpn)
            pred = common.transform(model, params, mm_all)
            px, mm = [], []
            for (x, y), m in zip(pred, mm_all):
                j = joint(rgb, x, y)
                if j is not None and math.hypot(j[0] - x, j[1] - y) <= JOINT_MATCH_PX:
                    px.append(j)
                    mm.append(m)
            got[ref] = {'px': px, 'mm': mm, 'how': f'{len(px)} of {len(mm_all)} joints found'}
            src = seed_src + [p for r in got for p in got[r]['mm']]
            dst = seed_dst + [p for r in got for p in got[r]['px']]
            model, params = _fit_mm_to_px(src, dst, sum(1 for r in got if len(got[r]['px']) >= 4))
    return got


def pal_points(board, frame, marks):
    """I3's points for each IC, from task 0's marks (marks.json "palLayout"):
    each marked pin's foot on I3, and the scan's pad nearest the pin's mark on
    I1-front among the part's footprints (so U1 and U4 take their 300 mil
    rows, where task 0 marked them)."""
    out = {}
    for p in marks['palLayout']['parts']:
        names = [n for n in board.fp if n == p['part'] or n.startswith(p['part'] + ' (')]
        px, mm, used = [], [], set()
        for pin in p['pins']:
            at = frame.to_board([pin['I1-front']])[0]
            best = min(((math.hypot(*(np.array(board.pin(n, pin['pin'])) - at)), n) for n in names))
            used.add(best[1])
            px.append(pin['I3'])
            mm.append(board.pin(best[1], pin['pin']))
        out[p['part']] = {'px': px, 'mm': mm, 'how': f"task 0's marks, {len(px)} pins, on {', '.join(sorted(used))}"}
    return out


def photo_fit(points):
    """The homography from a photograph to the board on every part's points."""
    src = [p for r in points for p in points[r]['px']]
    dst = [p for r in points for p in points[r]['mm']]
    H = fit_h(src, dst)
    e = np.hypot(*(apply_h(H, src) - np.asarray(dst)).T)
    return H, {'points': len(src), 'fitMm': {'median': float(np.median(e)), 'p90': float(np.percentile(e, 90)), 'max': float(e.max())}}


def sits_on_joints(rgb, H_mm_to_px, points_mm, match_px=JOINT_MATCH_PX):
    """On the solder side: how many of a part's holes on the scan have a joint
    on the photograph within match_px of where the fit puts them, and how far
    the joints found lie from there, in millimetres (through the fit's local
    scale)."""
    pred = apply_h(H_mm_to_px, points_mm)
    found, off = 0, []
    for (x, y), m in zip(pred, points_mm):
        j = joint(rgb, x, y)
        if j is not None and math.hypot(j[0] - x, j[1] - y) <= match_px:
            found += 1
            back = apply_h(np.linalg.inv(H_mm_to_px), [j])[0]
            off.append(float(math.hypot(back[0] - m[0], back[1] - m[1])))
    return {'holes': len(points_mm), 'joints': found, 'offMm': {'median': float(np.median(off)), 'max': float(max(off))} if off else None}


BODY_MAX_C, BODY_MAX_L = 0.025, 0.45     # an IC's body on I4: colourless and dark (U6's body L 0.27 to 0.34, C 0.003; the board C 0.06)
BODY_GAP_MM = 1.6                         # the marking's light letters break a line through the body: gaps this short are closed
BODY_STEP_MM = 0.05


def _run_through_middle(on, step, gap):
    """The extent (from, to) in steps, from the middle, of the run of True
    through the middle of `on`, short gaps closed."""
    n = len(on)
    mid = n // 2
    g = int(round(gap / step))
    filled = on.copy()
    idx = np.nonzero(on)[0]
    for a, b in zip(idx[:-1], idx[1:]):
        if 1 < b - a <= g:
            filled[a:b] = True
    if not filled[mid]:
        return None
    lo = mid
    while lo > 0 and filled[lo - 1]:
        lo -= 1
    hi = mid
    while hi < n - 1 and filled[hi + 1]:
        hi += 1
    return lo, hi


def body_size(rgb, H_mm_to_px, place, pins, spacing):
    """An IC's body on a photograph from above: its length along its rows and
    its width across, in board millimetres through the fit, each the median of
    three lines through the body (along: at the middle and a quarter of the
    width either side; across: between pins, near the middle and the two
    quarters of the length, where no lead stands beside the body)."""
    t = math.radians(place['rotation'])
    u = np.array([math.cos(t), math.sin(t)])
    v = np.array([-math.sin(t), math.cos(t)])
    c = np.array([place['x'], place['y']])
    half = pins // 2
    l0 = half * 2.54 + 4.0
    w0 = spacing + 2.0
    def line(p0, d, reach):
        s = np.arange(-reach, reach + 1e-9, BODY_STEP_MM)
        mm = p0 + np.outer(s, d)
        px = np.rint(apply_h(H_mm_to_px, mm)).astype(int)
        ok = (px[:, 0] >= 0) & (px[:, 1] >= 0) & (px[:, 0] < rgb.shape[1]) & (px[:, 1] < rgb.shape[0])
        px = np.clip(px, 0, [rgb.shape[1] - 1, rgb.shape[0] - 1])
        L, A, B = common.oklab(rgb[px[:, 1], px[:, 0]][None])
        on = (np.hypot(A, B)[0] < BODY_MAX_C) & (L[0] < BODY_MAX_L) & ok
        r = _run_through_middle(on, BODY_STEP_MM, BODY_GAP_MM)
        return None if r is None else (s[r[0]], s[r[1]])
    along = [line(c + k * spacing * 0.25 * v, u, l0 / 2 + 3) for k in (-1, 0, 1)]
    cols = [(round(f * (half - 1)) - (half - 1) / 2) * 2.54 + 1.27 for f in (0.25, 0.5, 0.75)]
    across = [line(c + x * u, v, w0 / 2 + 2) for x in cols]
    al = [b - a for a, b in (r for r in along if r)]
    ac = [b - a for a, b in (r for r in across if r)]
    shift_u = [(a + b) / 2 for a, b in (r for r in along if r)]
    shift_v = [(a + b) / 2 for a, b in (r for r in across if r)]
    if not al or not ac:
        return None
    return {'l': float(np.median(al)), 'w': float(np.median(ac)), 'lines': [len(al), len(ac)],
            'offsetMm': [float(np.median(shift_u)), float(np.median(shift_v))]}


# --- what the model draws ---------------------------------------------------------------

# Heights above the board's top face, millimetres. None is measured: the oblique
# photographs of a populated board (I7) were not fetched for task 5, so each is
# a typical figure for its kind of part, and HEIGHTS says so.
TYPICAL_H = {
    'dip': (4.0, 'a 300 mil plastic DIP seated on its standoffs, to the top of its body'),
    'dip600': (4.6, 'a 600 mil plastic DIP seated on its standoffs, to the top of its body'),
    'axial': (2.5, 'an axial part lying on the board: its body\'s diameter'),
    'radial': (7.0, 'a disc or electrolytic capacitor standing on its leads'),
    'other': (5.0, 'a single-in-line array, a transistor or a trimmer'),
    'crystal': (4.5, 'a crystal can lying flat'),
    'modulator': (22.0, 'the RF modulator\'s box'),
    'P1': (14.0, 'the 72-pin cartridge connector'),
    'P2': (8.0, 'the expansion port\'s socket'),
    'P4': (6.0, 'a controller header'),
    'P5': (6.0, 'a controller header'),
    'P6': (12.0, 'the POWER and RESET switch block'),
}

# Typical bodies for the passives (millimetres), by kind and part as seen on I4.
PASSIVE_MM = {
    ('axial', 'resistor'): (6.3, 2.4),
    ('axial', 'ferrite'): (6.0, 3.0),
    ('axial', 'inductor'): (6.3, 2.6),
    ('radial', 'disc'): (None, 5.0),
    ('radial', 'electrolytic'): (None, 5.0),
    ('other', 'transistor'): (7.0, 3.0),
    ('other', 'trimmer'): (6.5, 6.5),
    ('other', 'sip'): (None, 2.5),
}

# What marks each IC on the page (the spec, "What state each model shows"):
# `chip` names the counter (task 6's NesChip, in COUNTED's order) that marks it;
# `always` says why an IC is never marked. U7 and U8 are the controller ports'
# buffers by the board's print, "40H368(CI)" and "40H368(CII)"; that CI and CII
# are ports one and two, so $4016 and $4017, is inferred from the names, and
# task 6 checks it against the nesdev wiki. U9, the hex inverter, is the master
# clock's oscillator: the plan's list of reasons had none for it, so 'clock' was
# added (5 October 2026; the plan's task 5 interface says so).
CHIP = {'U6': 'apu', 'U5': 'ppu', 'U7': 'pad1', 'U8': 'pad2'}
ALWAYS_OF = {'U1': 'ram', 'U4': 'ram', 'U2': 'latch', 'U3': 'decoder', 'U10': 'lockout', 'U9': 'clock'}
ALWAYS = ALWAYS + ('clock',)
CHIP_INFERRED = ('U7 is pad1 and U8 pad2 by the board\'s print, "40H368(CI)" and "40H368(CII)"; that CI and CII are controller ports one '
                 'and two, so the reads of $4016 and $4017, is inferred from the names, and task 6 checks it against the nesdev wiki')

# Each IC's part on each console, read off the photographs on 5 October 2026,
# each a crop of the part looked at (the journal lists the crops): NTSC on I4,
# the NES-CPU-07; PAL on I3, the NES-CPU-11 (its CPU also on I6). The marking
# is the part's top line as printed, the maker's mark and date codes left out.
PARTS = {
    'U6': ('RP2A03G', 'RP2A07A'),
    'U5': ('RP2C02G-0', 'RP2C07-0'),
    'U1': ('MB8416A-15-SK', 'XRM6216-10'),
    'U4': ('MB8416A-15-SK', 'XRM6216-10'),
    'U2': ('SN74LS373N', 'MB74LS373'),
    'U3': ('SN74LS139N', 'SN74LS139N'),
    'U7': ('MN74HC368', 'MC74HC368N'),
    'U8': ('MN74HC368', 'MC74HC368N'),
    'U9': ('74HCU04AP', 'SN74HCU04N'),
    'U10': ('3193A', '3195A'),
}
CRYSTAL = {'ntsc': ('21.47727 MHz', '21.47727 KDS 8A'), 'pal': ('26.601712 MHz', '26.601712 KDS 1G')}

CONNECTOR_KINDS = {
    'P1': ('cartridge', 'the 72-pin cartridge connector'),
    'P2': ('expansion', 'the expansion port\'s socket'),
    'P4': ('controller', 'controller port 1\'s header (printed P4 I)'),
    'P5': ('controller', 'controller port 2\'s header (printed P5 II)'),
    'P6': ('power', 'the POWER and RESET switches\' header'),
}


def snap(board_pads, p, limit=0.6):
    """A lead read by eye moved to the scan's pad nearest it within limit mm."""
    best = min(board_pads, key=lambda q: math.hypot(q[0] - p[0], q[1] - p[1]))
    d = math.hypot(best[0] - p[0], best[1] - p[1])
    return (list(best), d) if d <= limit else (list(p), None)


def passive(m, pads):
    leads, moved = [], []
    for p in m['leads']:
        q, d = snap(pads, p)
        leads.append(q)
        moved.append(None if d is None else round(d, 3))
    a, b = np.array(leads[0], float), np.array(leads[-1], float)
    c = (a + b) / 2 if len(leads) == 2 or m['kind'] != 'other' else np.mean(leads, 0)
    if m['kind'] == 'other' and m.get('pins'):
        c = (a + b) / 2
    rot = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
    length, dia = PASSIVE_MM[(m['kind'], m['part'])]
    if length is None:
        length = float(np.hypot(*(b - a))) + 2.54 if m['kind'] == 'other' else float(dia)
    return {'ref': m['ref'], 'kind': m['kind'], 'part': m['part'], 'x': float(c[0]), 'y': float(c[1]), 'rotation': rot,
            'length': float(length), 'diameter': float(dia), 'leads': leads, 'leadMovedMm': moved}


def outline_box(H_px_to_mm, box):
    """A body's outline box read on a photograph, as a box in the board frame
    through the photograph's fit (its four corners; the box the frame holds them in)."""
    x0, y0, x1, y1 = box
    q = apply_h(H_px_to_mm, [[x0, y0], [x1, y0], [x1, y1], [x0, y1]])
    lo, hi = q.min(0), q.max(0)
    return {'x': float((lo[0] + hi[0]) / 2), 'y': float((lo[1] + hi[1]) / 2), 'w': float(hi[0] - lo[0]), 'l': float(hi[1] - lo[1]),
            'corners': q.tolist()}


def main():
    reg, fj, table, marks = load('registration.json'), load('frame.json'), load('ic-table.json'), load('marks.json')
    import board_frame
    frame = board_frame.Frame.read(fj)
    board = Board(reg)
    pm = marks['parts']

    # 1. the ICs on their footprints
    footprints = [{**f, 'pads': board.pins(f['ref'])} for f in reg['footprints'] if f.get('ref')]   # pad indices to their places
    placed, problems = place_ics(footprints, table, USE)
    if problems:
        print('problems:', problems)
    ics = {p['ref']: p for p in placed}

    # 2. the photographs on the scan's footprints
    print('I4: the top side of the NTSC NES-CPU-07 ...')
    I4 = common.read_rgb('I4')
    seeds4 = {'seeds': pm['I4']['seeds']['pins'], 'midlines': pm['I4']['midlines']['parts']}
    top = register_top(I4, board, seeds4)
    held4 = held_out_parts(top)
    H4, fit4 = photo_fit(top)
    H4mm2px = np.linalg.inv(H4)
    # the RAMs' other width: where the part would be on the 600 mil footprint
    other_width = {}
    for ref in ('U1', 'U4'):
        n = 24
        mids = [((np.array(board.pin(ref, k)) + np.array(board.pin(ref, n + 1 - k))) / 2).tolist() for k in range(1, 13)]
        others = [q for q in top if q != ref]
        Hb = fit_h([p for q in others for p in top[q]['px']], [p for q in others for p in top[q]['mm']])
        got = apply_h(Hb, top[ref]['px']).mean(0)
        other_width[ref] = float(np.hypot(*(got - np.mean(mids, 0))))
    print('I5: the solder side ...')
    I5 = common.read_rgb('I5')
    bottom = register_bottom(I5, board, {'seeds': pm['I5']['seeds']['pins']})
    held5 = held_out_parts(bottom)
    H5, fit5 = photo_fit(bottom)
    H5mm2px = np.linalg.inv(H5)
    print('I3: the PAL NES-CPU-11, on task 0\'s marks ...')
    pal = pal_points(board, frame, marks)
    held3 = held_out_parts(pal)
    H3, fit3 = photo_fit(pal)

    sits = {}
    for ref in ics:
        worst = max(held4[ref]['heldOutMm'], held5[ref]['heldOutMm'])
        sits[ref] = {'I4': held4[ref]['heldOutMm'], 'I5': held5[ref]['heldOutMm'], 'I3': held3[ref]['heldOutMm'],
                     'verdict': 'sits on it' if worst <= SITS_MM else 'does not sit on it'}
    others_sit = {}
    for ref in ('P1', 'P2', 'P3', 'P4', 'P5', 'P6', 'X1', 'X2'):
        others_sit[ref] = sits_on_joints(I5, H5mm2px, board.pins(ref))

    # 3. the bodies, measured on I4 (recorded beside the package's) and the places against the redrawing's
    for ref, p in ics.items():
        ic = next(i for i in table['ics'] if i['ref'] == ref)
        spacing = 15.24 if ic['package'].endswith('600 mil') and ref not in USE else 7.62
        p['spacing'] = spacing
        p['bodyOnI4'] = body_size(I4, H4mm2px, p, ic['pins'], spacing)
    kic = kicad_pads(common.original('I2'))
    kicad_places = {}
    for ref, p in ics.items():
        kicad_places[ref] = kicad_centre(kic[ref], p['pins'], spacing=p['spacing'])
    check = kicad_check({r: (ics[r]['x'], ics[r]['y']) for r in ics}, kicad_places)

    # the connectors' and crystals' holes on the solder side: the finder's count, and what was looked at
    looked = pm['I5']['looked']
    for ref, v in others_sit.items():
        seen = looked.get(ref)
        v['looked'] = seen['what'] if seen else None
        v['verdict'] = 'sits on it' if (v['joints'] == v['holes'] or (seen and seen['sits'])) else 'not shown'

    # bodies: each package's size, the median of its parts measured on I4
    by_pkg = {}
    for ref, p in ics.items():
        ic = next(i for i in table['ics'] if i['ref'] == ref)
        key = (ic['pins'], p['spacing'])
        by_pkg.setdefault(key, []).append(p['bodyOnI4'])
    pkg_size = {k: {'l': float(np.median([b['l'] for b in v])), 'w': float(np.median([b['w'] for b in v])), 'parts': len(v)} for k, v in by_pkg.items()}

    # the model's content
    role = {i['ref']: i['role'] for i in table['ics']}
    order = [i['ref'] for i in table['ics']]
    ICS = []
    for ref in order:
        p = ics[ref]
        size = pkg_size[(p['pins'], p['spacing'])]
        hk = 'dip600' if p['spacing'] > 10 else 'dip'
        ICS.append({'ref': ref, 'role': role[ref], 'pins': p['pins'], 'x': p['x'], 'y': p['y'], 'w': size['w'], 'l': size['l'],
                    'rotation': p['rotation'], 'height': TYPICAL_H[hk][0], 'parts': {'ntsc': PARTS[ref][0], 'pal': PARTS[ref][1]},
                    'chip': CHIP.get(ref), 'always': ALWAYS_OF.get(ref)})
    H4px2mm = H4
    CONN = []
    boxes4 = {b['ref']: b for b in pm['I4']['outlines']['boxes']}
    for ref, (kind, label) in CONNECTOR_KINDS.items():
        o = outline_box(H4px2mm, boxes4[ref]['box'])
        CONN.append({'ref': ref, 'kind': kind, 'x': o['x'], 'y': o['y'], 'w': o['l'], 'l': o['w'], 'h': TYPICAL_H[ref][0], 'rotation': 0.0,
                     'label': label})
    pads_all = [[q['x'], q['y']] for q in reg['pads']]
    PASS = [passive(m, pads_all) for m in pm['passives']['parts']]
    x2 = board.pins('X2')
    a, b = np.array(x2[0]), np.array(x2[-1])
    PASS.append({'ref': 'X2', 'kind': 'other', 'part': 'resonator', 'x': float((a[0] + b[0]) / 2), 'y': float((a[1] + b[1]) / 2),
                 'rotation': math.degrees(math.atan2(b[1] - a[1], b[0] - a[0])), 'length': 8.0, 'diameter': 3.5,
                 'leads': x2, 'leadMovedMm': [0.0] * len(x2)})
    boxes3 = {b['ref']: b for b in pm['I3']['outlines']['boxes']}
    def other(kind, ref, H, box, region, part, label, hk):
        o = outline_box(H, box)
        return {'kind': kind, 'ref': ref, 'x': o['x'], 'y': o['y'], 'w': o['l'], 'l': o['w'], 'h': TYPICAL_H[hk][0], 'rotation': 0.0,
                'part': part, 'label': label}
    OTH = {
        'ntsc': [other('modulator', 'P3', H4px2mm, boxes4['P3']['box'], 'ntsc', None, 'RF modulator (its lid off in the photograph; no marking seen)', 'modulator'),
                 other('crystal', 'X1', H4px2mm, boxes4['X1']['box'], 'ntsc', CRYSTAL['ntsc'][0], f"crystal, {CRYSTAL['ntsc'][0]}", 'crystal')],
        'pal': [other('modulator', 'P3', H3, boxes3['P3']['box'], 'pal', 'ALPS', 'RF modulator (ALPS, its can closed)', 'modulator'),
                other('crystal', 'X1', H3, boxes3['X1']['box'], 'pal', CRYSTAL['pal'][0], f"crystal, {CRYSTAL['pal'][0]}", 'crystal')],
    }
    HEIGHTS = {'board': 1.6}
    for k, (v, what) in TYPICAL_H.items():
        HEIGHTS[k] = {'value': v, 'measured': False, 'what': f'typical, not measured: {what}'}
    BOARD = {'width': fj['board']['widthMm'], 'depth': fj['board']['depthMm'], 'thickness': 1.6, 'outline': fj['outline'],
             'holes': [{'x': h['x'], 'y': h['y'], 'd': h['d']} for h in fj['holes']]}
    model = {'board': BOARD, 'ics': ICS, 'connectors': CONN, 'passives': [{k: q[k] for k in ('kind', 'x', 'y', 'rotation', 'length', 'diameter')} for q in PASS],
             'others': OTH, 'heights': HEIGHTS, 'sources': ['I1-front', 'I1-back', 'I3', 'I4', 'I5']}

    # inside the outline, and the bodies apart
    outline = fj['outline']
    outside = [{'ref': q['ref'], 'corner': c} for q in ICS for c in body_corners(q) if not inside(outline, c)]
    over = []
    for q in CONN + OTH['ntsc'] + OTH['pal']:
        if any(not inside(outline, c) for c in body_corners(q)):
            over.append(q['ref'] + ('' if q in CONN else f" ({'ntsc' if q in OTH['ntsc'] else 'pal'})"))
    clashes = [[a['ref'], b['ref']] for i, a in enumerate(ICS) for b in ICS[i + 1:] if overlap(a, b)]

    # the connectors and crystals against the redrawing, through the ICs' similarity: recorded, not judged
    S = common._fit('similarity', np.array([kicad_places[r] for r in sorted(ics)], float), np.array([[ics[r]['x'], ics[r]['y']] for r in sorted(ics)], float))
    beside = {}
    for ref in ('P1', 'P2', 'P3', 'P4', 'P5', 'P6', 'X1', 'X2'):
        n = len(board.fp[ref]['pads'])
        names = [str(k) for k in range(1, n + 1) if str(k) in kic[ref]['pads']]
        kc = np.mean([c for nm in names for c in kic[ref]['pads'][nm]], 0)
        sc = np.mean(board.pins(ref), 0)
        got = common.transform('similarity', S, [kc])[0]
        beside[ref] = {'offMm': float(np.hypot(*(got - sc))), 'pads': [len(names), n]}

    result = {
        'about': ('Task 5 of the NES models plan: every part on the board, placed on the bare NES-CPU-10 scan\'s footprints (registration.json) '
                  'in its board frame (frame.json), and each console\'s parts read off photographs of populated boards: NTSC on I4 and I5, '
                  'Evan-Amos\'s NES-CPU-07, PAL on I3, an NES-CPU-11. Written by board_parts.py; every figure here is a measurement made on the '
                  'date of the journal entry that quotes it. `model` is what site/src/models/nes-famicom-board-parts.mjs exports.'),
        'model': model,
        'chipInferred': CHIP_INFERRED,
        'problems': problems,
        'places': {r: {k: ics[r][k] for k in ('footprint', 'pins', 'padCount', 'x', 'y', 'rotation')} for r in order},
        'photographs': {
            'what': ('Each photograph fitted to the board frame by a homography on the ICs\' points (I4: the midline at each lead column, '
                     'top of the bodies; I5: every pin\'s solder joint; I3: task 0\'s marks, the pins\' feet), each IC held out in turn: '
                     'its points through the fit on the others, the mean against the mean of their places on the scan'),
            'I4': {'fit': fit4, 'heldOut': held4, 'how': {r: top[r]['how'] for r in top},
                   'otherWidthMm': {'what': 'U1 and U4 held out against the 600 mil footprint\'s midline instead: where the RAMs would be on it', **other_width}},
            'I5': {'fit': fit5, 'heldOut': held5, 'how': {r: bottom[r]['how'] for r in bottom}},
            'I3': {'fit': fit3, 'heldOut': held3, 'how': {r: pal[r]['how'] for r in pal}},
        },
        'sitsOn': {'what': (f'Each part of the CPU-07 (I4, I5) on the CPU-10 footprint of the same reference: an IC sits on it if its held-out '
                            f'error on I4 and on I5 is at most {SITS_MM} mm (the plan\'s PAL layout row\'s "none over 2.0", set before any figure '
                            f'was seen); a connector or crystal if its holes have their joints on I5 (the finder\'s count, and where it found '
                            f'fewer, what the crop showed). I3\'s held-out errors are the PAL board\'s, recorded beside'),
                   'limitMm': SITS_MM, 'ics': sits, 'others': others_sit},
        'kicad': {'what': ('Each IC\'s place (its pads\' centre) against the centre of the same pads in the KiCad redrawing (I2), for U1 and U4 '
                           'the 300 mil copies, after the similarity that fits them best; none may be more than 3 mm off (the plan). The '
                           'connectors and crystals through the same similarity, recorded, not judged. Numbers compared; nothing drawn from I2'),
                  'places': kicad_places, **check, 'beside': beside},
        'bodies': {'what': ('Each IC\'s body measured on I4 (lines through it, through the fit: their median), and the size the model draws: each '
                            'package\'s median over its parts. Recorded beside: a body\'s centre against its pads\' (offsetMm, along and across)'),
                   'onI4': {r: ics[r]['bodyOnI4'] for r in order},
                   'byPackage': {f'DIP-{k[0]} {"600" if k[1] > 10 else "300"} mil': v for k, v in pkg_size.items()}},
        'passives': {'what': 'Each passive: its ref, the part as seen on I4, its leads on the scan\'s pads (leadMovedMm: how far each lead read by eye moved to its pad; null: no pad within 0.6 mm, the lead read by eye kept)',
                     'parts': PASS},
        'overhang': {'what': 'Bodies that run past the board\'s outline, as the photographs show them (the connectors at the edges and the modulators)', 'refs': over},
        'verdicts': [
            {'check': 'pad counts', 'verdict': 'pass' if not problems else 'fail'},
            {'check': 'KiCad places', 'verdict': check['verdict']},
            {'check': 'CPU-07 parts on the CPU-10 footprints', 'verdict': 'pass' if all(v['verdict'] == 'sits on it' for v in list(sits.values()) + list(others_sit.values())) else 'fail'},
            {'check': 'IC bodies inside the outline', 'verdict': 'pass' if not outside else 'fail'},
            {'check': 'IC bodies apart', 'verdict': 'pass' if not clashes else 'fail'},
        ],
    }
    common.write_data('parts.json', result, places=3, rows_per_line=True)
    PARTS_MJS.write_text(module_text(model), encoding='utf8')

    # ic-table.json: each console's part
    for ic in table['ics']:
        ic['parts'] = {'ntsc': {'part': PARTS[ic['ref']][0], 'seen': 'I4'}, 'pal': {'part': PARTS[ic['ref']][1], 'seen': 'I3'}}
        if ic['ref'] in ('U7', 'U8'):
            ic['port'] = {'value': 1 if ic['ref'] == 'U7' else 2, 'inferred': True, 'why': CHIP_INFERRED}
    table['crystals'] = [{'ref': 'X1', 'role': 'the master clock\'s crystal', 'kicadValue': '21.477272 MHz',
                          'parts': {r: {'part': CRYSTAL[r][0], 'seen': 'I4' if r == 'ntsc' else 'I3', 'marking': CRYSTAL[r][1]} for r in ('ntsc', 'pal')}}]
    # in the file's own order (task 4 wrote it by hand), each IC's new keys after its old ones
    (common.DATA / 'ic-table.json').write_text(json.dumps(table, indent=1, ensure_ascii=False) + '\n', encoding='utf8')

    overlays(I4, H4, model, fj, frame)
    for v in result['verdicts']:
        print(f"{v['check']}: {v['verdict']}")
    print(f"KiCad places: max {check['maxMm']:.3f} mm, median {check['medianMm']:.3f}, scale {check['scale']:.5f}, turn {check['rotationDeg']:.3f} deg")
    print('held out (mm), I4 / I5 / I3: ' + '; '.join(f"{r} {held4[r]['heldOutMm']:.3f}/{held5[r]['heldOutMm']:.3f}/{held3[r]['heldOutMm']:.3f}" for r in order))
    print(f"fits: I4 {fit4['points']} points, median {fit4['fitMm']['median']:.3f}; I5 {fit5['points']}, {fit5['fitMm']['median']:.3f}; I3 {fit3['points']}, {fit3['fitMm']['median']:.3f}")
    print(f"U1 and U4 on the 600 mil footprint instead: {other_width['U1']:.2f} and {other_width['U4']:.2f} mm off")
    print('connectors and crystals on I5: ' + '; '.join(f"{r} {v['joints']}/{v['holes']} ({v['verdict']})" for r, v in others_sit.items()))
    print(f"overhang: {over}; IC bodies outside: {len(outside)}; clashes: {clashes}")
    print(f"ICs {len(ICS)}, connectors {len(CONN)}, passives {len(PASS)}, others {len(OTH['ntsc'])} + {len(OTH['pal'])}")
    bad = [v for v in result['verdicts'] if v['verdict'] != 'pass']
    return 1 if bad else 0


def overlays(I4, H4, model, fj, frame):
    """Pictures to look at, in out/ (git-ignored): the board frame with every
    part drawn on the scan, and I4 with the ICs' bodies drawn through its fit."""
    from PIL import Image, ImageDraw
    import cv2
    common.OUT.mkdir(exist_ok=True)
    S, M = 8, 3
    F = common.read_rgb('I1-front')
    W, Hh = int((fj['board']['widthMm'] + 2 * M) * S), int((fj['board']['depthMm'] + 2 * M) * S)
    u, v = np.meshgrid(np.arange(W), np.arange(Hh))
    mm = np.stack([(u + 0.5) / S - M, (v + 0.5) / S - M], -1).reshape(-1, 2)
    px = frame.to_px(mm).reshape(Hh, W, 2).astype(np.float32)
    im = Image.fromarray(cv2.remap(F, px[..., 0], px[..., 1], cv2.INTER_LINEAR))
    d = ImageDraw.Draw(im)
    P = lambda q: ((q[0] + M) * S, (q[1] + M) * S)
    def box(q, col):
        c = [P(p) for p in body_corners(q)]
        d.line(c + [c[0]], fill=col, width=2)
    for q in model['ics']:
        box(q, (255, 0, 0))
        d.text(P((q['x'] - 3, q['y'] - 1)), q['ref'], fill=(255, 255, 0))
    for q in model['connectors']:
        box(q, (0, 140, 255))
    for q in model['others']['ntsc']:
        box(q, (0, 255, 255))
    for q in model['others']['pal']:
        box(q, (255, 0, 255))
    for q in model['passives']:
        w = q['diameter'] if q['kind'] != 'radial' else q['diameter']
        l = q['length'] if q['kind'] != 'radial' else q['diameter']
        box({'x': q['x'], 'y': q['y'], 'l': l, 'w': w, 'rotation': q['rotation']}, (255, 255, 0))
    im.save(common.OUT / 'parts-board.png')
    big = Image.fromarray(I4)
    d = ImageDraw.Draw(big)
    Hi = np.linalg.inv(H4)
    for q in model['ics']:
        c = apply_h(Hi, body_corners(q)).tolist()
        d.line([tuple(p) for p in c] + [tuple(c[0])], fill=(255, 0, 255), width=3)
    big.save(common.OUT / 'parts-I4.jpg', quality=85)


if __name__ == '__main__':
    sys.exit(main())
