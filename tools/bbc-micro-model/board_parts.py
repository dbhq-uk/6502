"""Task 5: every part on the BBC Micro's Issue 7 board, placed from the bare
scan's footprints and named from the Service Manual's table 8.1; writes
data/parts.json and the module the site reads,
site/src/models/bbc-micro-board-parts.mjs.

    BBC_MODEL_INPUTS=<folder> python board_parts.py            # the run
    BBC_MODEL_INPUTS=<folder> python board_parts.py --settle   # the manual's convention, on IC51, IC52, IC20 and IC21 alone
    BBC_MODEL_INPUTS=<folder> python board_parts.py --crops    # crops of I5 and I6 to read the markings on, in out/
    python board_parts.py --hashes                             # the SHA-256 of the method's files

Everything is in task 2's board frame (frame.json: millimetres from the
board's left rear corner as the component side is seen with the rear
connectors at the far edge, x to the right, y towards the front), on task 3's
footprints and pads (registration.json).

1. The ICs. Each IC is placed on the footprint of its reference: its place is
   the centre of the footprint's pads, its rotation the direction from pin 1
   to the last pin of pin 1's row (pin N/2), in degrees from x towards y (90
   for every DIP on this board but IC74 and IC75, which lie across: pin 1 at
   the rear left, its row running towards the front). A footprint whose pad
   count is not its part's pin count (ic-table.json "pins") is reported, not
   placed. The two 8-pin DIPs, IC16 and IC19, are not task 3's footprints
   (it grouped DIPs of 14 to 40 pins); their pads are named by hand
   (marks.json "parts.eightPin"), and a pad task 3 did not find is put on the
   pitch between its row's others (padsInferred). The body drawn is the
   package's typical size (BODY_MM), not measured.

2. The manual's places (the plan's check: after a best-fit similarity, no IC
   more than MANUAL_LIMIT_MM, 10 mm, off; it catches a swap). Table 8.1 gives
   each IC's place in millimetres from the board's SW corner, the front left
   as the component side is seen, so its Y runs towards the rear: it is
   flipped (X, -Y) before the similarity is fitted. Which point of a package
   the manual measures (CONVENTIONS: the pads' centre, pin 1, pin N/2, or the
   body's corner beyond pin N/2 on pin 1's side) is settled on IC51, IC52
   (28 pins, 600 mil), IC20 (16 pins) and IC21 (14 pins) alone, by the
   smallest largest residual of a similarity fitted to those four, a tie
   within TIE_MM going to the earlier convention in CONVENTIONS; written in
   the journal before the check over every placed IC runs, once. CONVENTION
   holds the result; the run refuses to judge while it is None.

3. I5 to the board frame: the homography task 4 fitted on the chip pins
   marked by hand (marks.json "i5Pins"), refitted here unrounded (copper.json
   keeps its parameters to 4 places, which loses the perspective terms), each
   chip held out (the plan's "I5 to I1" row: median at most 1.0 mm passes,
   over 2.0 stops). The same marks and the same fit as task 4: not a new,
   independent check.

4. Identities. Each IC's identity is table 8.1's part (ic-table.json). The
   check: each IC's marking, read by hand on a crop of I5 drawn onto the
   board frame through the fit (marks.json "parts.identities.I5"), against
   the table's part by the rule in `identity`, fixed before any crop was
   read: letters and digits only, upper case; the table's part with its
   leading letters dropped (uPD7002 to 7002, SAA5050 to 5050) must appear in
   the marking, not followed by a digit; a 68-family part given bare (6850)
   also matches its speed grades (68A50, 68B50); the 74 families must match
   exactly (74S00 is not 74LS00). A marking that is the table's earlier part
   (IC40's "previously 74LS00") is recorded as that, not as a disagreement.
   The ROM sockets (ROM_SOCKETS) are not compared: on the machine the page
   runs, a socket's identity is the ROM in it (the OS, BASIC, the DFS), which
   a photograph of another board cannot show; their markings are recorded.
   The row passes if no marking read disagrees and at least IDENTITY_MIN_READ
   (three quarters) of the ICs I5 shows populated, ROM sockets left out, are
   read; under that it is not measured. I6 (another board, oblique, in its
   case) is read the same way where a marking shows, and recorded, not
   judged.

5. The fitted set (ic-table.json "fitted", with fittedWhy): the Model B the
   page runs, with the 8271 disc interface and the DFS, and without Econet
   or speech, each IC by the upgrade in the Service Manual's section 4 that
   fits it. Recorded in docs/bbc-micro/facts/models.md, "The fitted set",
   which the site test holds every generated file to.

6. Connectors and the other large parts (the modulator, the crystal, the
   cassette motor's relay): boxes read by hand on I5 drawn onto the board
   frame through the fit (marks.json "parts.outlines"). The fit is the
   board's plane, so the top of a tall part, which is what a view from above
   shows, leans away from the middle of the photograph by its height times
   its distance from the middle over the camera's distance; not corrected,
   said in the marks. Labels from the print and D1, each with its source.

7. Two-lead parts (resistors, capacitors, diodes): every pair of pads not in
   an IC or a named connector, 2.3 to 16 mm apart, with the yellow print
   (board_trace.py's colour: b at least 0.095, L at least 0.55) running along
   at least PRINT_SHARE_MIN of the line between them (PAD_CLEAR_MM off each
   pad's centre), and no other such pad within ON_WAY_MM of that line; each
   pad in one part, the pairs with the most print taken first; two pads in
   one of task 3's rows of pads (a header's pins, a link's, a transistor's
   legs in a line) are never a part, and nor is a pair whose middle lies
   inside an IC's body or a box of step 6 (that part's own pins). Axial if the
   pads are AXIAL_MIN_MM or more apart, else radial; a typical body for its
   kind. An inference from the print, not a reading of each part: the
   journal says how many, and what it misses.

8. Heights: typical, not measured (no photograph of a populated Issue 7
   board from the side was found); each carries measured: false.

9. The module: generated, with a header naming its sources; no colours.

`sheila` names the IC's counter in the machine (task 6's BbcChip, as the
plan fixed them), from the Service Manual's strobe list for its select lines
(D1 PDF page 58, "DEFPROC strobe select lines": &FE00 IC2, &FE08 IC4, &FE10
IC7, &FE20 IC6, &FE30 IC76, &FE40 IC3, &FE60 IC69, &FE80 IC78, &FEC0 IC73)
and the sound chip, IC18, written through the system VIA (via.md).
"""
import argparse
import json
import math
import re
import sys
import warnings

import numpy as np

import common

PARTS_MJS = common.REPO / 'site' / 'src' / 'models' / 'bbc-micro-board-parts.mjs'

# --- the checks, fixed on 13 October 2026 before any was run -------------------------------
MANUAL_LIMIT_MM = 10.0
CONVENTIONS = ('centre', 'pin1', 'pinHalf', 'bodyCorner')
SETTLE_ON = ('IC51', 'IC52', 'IC20', 'IC21')
TIE_MM = 0.2
CONVENTION = 'pinHalf'       # settled by --settle on SETTLE_ON alone (13 October 2026), and written in the journal, before the check ran
I5_REG_PASS_MM, I5_REG_STOP_MM = 1.0, 2.0
IDENTITY_MIN_READ = 0.75
ROM_SOCKETS = ('IC51', 'IC52', 'IC88', 'IC100', 'IC101')
THRESHOLDS = {
    'padCounts': 'every placed IC\'s pad count is its part\'s pin count; a fitted IC not placed fails',
    'identities': ('every IC\'s marking read on I5 is table 8.1\'s part by the fixed rule (ROM sockets not compared), and at '
                   'least three quarters of the ICs I5 shows populated are read; under that, not measured'),
    'manual': 'after a best-fit similarity, no placed IC more than 10 mm from table 8.1\'s place, on the convention settled first',
    'i5Registration': 'I5 to the board frame on chip pins, each chip held out: median at most 1.0 mm passes, over 2.0 mm stops',
    'inside': 'every IC body and every two-lead part inside the outline; a connector or other part over it is recorded',
    'apart': 'no two IC bodies overlap',
}

# --- what the model draws ------------------------------------------------------------------
# Typical plastic DIP bodies, millimetres (length along the rows, width across), not measured on these boards.
BODY_MM = {(8, 7.62): (9.3, 6.35), (14, 7.62): (19.05, 6.35), (16, 7.62): (19.3, 6.35), (20, 7.62): (24.9, 6.35),
           (24, 15.24): (31.75, 13.97), (28, 15.24): (36.8, 13.97), (40, 15.24): (52.1, 13.97)}
TYPICAL_H = {
    'board': (1.6, 'the board\'s thickness, the usual for a two-layer board'),
    'dip': (4.0, 'a plastic DIP seated on the board, to the top of its body'),
    'socket': (3.0, 'what a socket adds under a DIP'),
    'axial': (2.5, 'an axial part lying on the board: its body\'s diameter'),
    'radial': (6.0, 'a disc or dipped capacitor standing on its leads'),
    'din': (13.0, 'a DIN socket\'s housing'),
    'dType': (12.5, 'the 15-way D socket\'s housing'),
    'idc': (9.0, 'a boxed header with latches'),
    'header': (8.5, 'a pin header'),
    'phono': (12.0, 'the modulator\'s output socket'),
    'modulator': (17.0, 'the UHF modulator\'s can'),
    'crystal': (4.5, 'a crystal can lying flat'),
    'relay': (11.0, 'the cassette motor\'s relay'),
}
PASSIVE_MM = {'axial': (6.3, 2.4), 'radial': (5.0, 3.0)}   # (along the leads, across): a resistor; a dipped capacitor, flat, as I5 shows them

# Two-lead parts from the print (step 7)
PAIR_MM = (2.3, 16.0)
PRINT_SHARE_MIN = 0.6
PAD_CLEAR_MM = 0.9
ON_WAY_MM = 1.0
AXIAL_MIN_MM = 7.0
SAMPLE_MM = 0.1
ACROSS_MM = (-0.25, 0.0, 0.25)

# The machine's counters (task 6's BbcChip, in its order, as the plan fixed the names) and the ICs they mark.
SHEILA = ('crtc', 'acia', 'serialUla', 'videoUla', 'romLatch', 'systemVia', 'userVia', 'fdc8271', 'adc', 'sound')
SHEILA_OF = {'IC2': 'crtc', 'IC4': 'acia', 'IC7': 'serialUla', 'IC6': 'videoUla', 'IC76': 'romLatch', 'IC3': 'systemVia',
             'IC69': 'userVia', 'IC78': 'fdc8271', 'IC73': 'adc', 'IC18': 'sound'}
SHEILA_FROM = ('D1 PDF page 58, "DEFPROC strobe select lines" (each SHEILA address and the IC pin its select line reaches); '
               'the sound chip, IC18, is written through the system VIA (docs/bbc-micro/facts/via.md)')

# Sockets the Service Manual names: modification A replaces the OS ROM in IC51; B, C and D say "insert the ICs listed
# above into the sockets provided" (D1 PDF pages 35, 37 and 38); IC52, IC88, IC100 and IC101 are the sideways ROM sockets.
MANUAL_SOCKETS = {'IC51', 'IC52', 'IC100', 'IC101', 'IC98', 'IC99'} | {f'IC{n}' for n in range(61, 89)}


# --- places ----------------------------------------------------------------------------------

def place(pads, pins):
    """A footprint's place: the centre of its pads, and its rotation, the
    direction from pin 1 to pin N/2 in degrees from x towards y."""
    P = np.asarray(pads, float).reshape(-1, 2)
    c = P.mean(0)
    end = P[pins // 2 - 1] - P[0]
    return {'x': float(c[0]), 'y': float(c[1]), 'rotation': math.degrees(math.atan2(end[1], end[0]))}


def place_ics(footprints, table):
    """Each IC of the table with pins on the footprint of its reference.
    footprints: {ref: {"pads": [[x, y], ...]}}. Returns (placed, problems)."""
    placed, problems = [], []
    for ic in table['ics']:
        if not ic.get('pins'):
            continue
        f = footprints.get(ic['ref'])
        if f is None:
            problems.append({'ref': ic['ref'], 'problem': 'no footprint: not placed'})
            continue
        if len(f['pads']) != ic['pins']:
            problems.append({'ref': ic['ref'], 'problem': f"its footprint has {len(f['pads'])} pads, its part {ic['pins']} pins: not placed"})
            continue
        placed.append({'ref': ic['ref'], 'pins': ic['pins'], 'padCount': len(f['pads']), **place(f['pads'], ic['pins'])})
    return placed, problems


def marked_footprint(mark, board_pads):
    """An 8-pin DIP's footprint from pads named by hand in pin order; a pad
    task 3 did not find (None) is put on its row's line at its place in the
    pitch, from the row's other pads."""
    n = len(mark['pads'])
    half = n // 2
    pts = [None if i is None else [board_pads[i]['x'], board_pads[i]['y']] for i in mark['pads']]
    inferred = []
    for row in (range(0, half), range(half, n)):
        known = [(k, pts[i]) for k, i in enumerate(row) if pts[i] is not None]
        ks = np.array([k for k, _ in known], float)
        xy = np.array([p for _, p in known], float)
        fx, fy = np.polyfit(ks, xy[:, 0], 1), np.polyfit(ks, xy[:, 1], 1)
        for k, i in enumerate(row):
            if pts[i] is None:
                pts[i] = [float(np.polyval(fx, k)), float(np.polyval(fy, k))]
                inferred.append(i + 1)
    return {'ref': mark['ref'], 'pads': pts, 'padIndexes': mark['pads'], 'padsInferred': inferred}


def body_size(pins, spacing):
    """The package's typical body: (length along the rows, width across)."""
    return BODY_MM[(pins, round(spacing, 2))]


def spacing_of(pads, pins):
    P = np.asarray(pads, float)
    return float(np.hypot(*(P[pins - 1] - P[0])))


def convention_point(p, pads, which):
    """The point of a placed DIP a convention names: the pads' centre, pin 1,
    pin N/2, or the body's corner beyond pin N/2 on pin 1's side."""
    P = np.asarray(pads, float)
    n = p['pins']
    if which == 'centre':
        return P.mean(0).tolist()
    if which == 'pin1':
        return P[0].tolist()
    if which == 'pinHalf':
        return P[n // 2 - 1].tolist()
    if which == 'bodyCorner':
        s = p.get('spacing') or spacing_of(P, n)
        l, w = body_size(n, s)
        u = (P[n // 2 - 1] - P[0]) / np.hypot(*(P[n // 2 - 1] - P[0]))
        out = -(P[n - 1] - P[0]) / np.hypot(*(P[n - 1] - P[0]))
        return (P[n // 2 - 1] + u * (l - (n // 2 - 1) * 2.54) / 2 + out * (w - s) / 2).tolist()
    raise ValueError(which)


def manual_check(points, manual, limit=MANUAL_LIMIT_MM):
    """Each IC's point on the board against table 8.1's place, after the
    similarity taking the manual's places, Y flipped, onto the board's that
    fits them best. points and manual: {ref: (x, y)}, the same refs."""
    refs = sorted(points, key=lambda r: (len(r), r))
    src = np.array([[manual[r][0], -manual[r][1]] for r in refs], float)
    dst = np.array([points[r] for r in refs], float)
    params = common._fit('similarity', src, dst)
    got = common.transform('similarity', params, src)
    per = {}
    for r, g, d in zip(refs, got, dst):
        per[r] = {'offMm': float(np.hypot(*(g - d))), 'dxMm': float(g[0] - d[0]), 'dyMm': float(g[1] - d[1])}
    offs = [e['offMm'] for e in per.values()]
    worst = max(offs)
    return {'perPart': per, 'maxMm': worst, 'medianMm': float(np.median(offs)), 'scale': params['scale'],
            'rotationDeg': params['rotationDeg'], 'matrix': params['matrix'], 'limitMm': limit,
            'verdict': 'pass' if worst <= limit else 'fail'}


def settle_convention(places, footprints, manual, on=None):
    """Which point of a package the manual measures, from the parts `on`
    alone (default: all given): a similarity fitted to their points under each
    convention; the smallest largest residual wins, a tie within TIE_MM going
    to the earlier in CONVENTIONS."""
    refs = list(on or places)
    by = {}
    for c in CONVENTIONS:
        pts = {r: convention_point(places[r], footprints[r], c) for r in refs}
        m = manual_check(pts, {r: manual[r] for r in refs}, limit=float('inf'))
        by[c] = {'maxMm': m['maxMm'], 'medianMm': m['medianMm'], 'scale': m['scale'], 'rotationDeg': m['rotationDeg'],
                 'perPart': {r: m['perPart'][r]['offMm'] for r in refs}}
    best = min(by[c]['maxMm'] for c in CONVENTIONS)
    chosen = next(c for c in CONVENTIONS if by[c]['maxMm'] <= best + TIE_MM)
    return {'on': refs, 'byConvention': by, 'chosen': chosen, 'tieMm': TIE_MM}


# --- identities ------------------------------------------------------------------------------

def _norm(s):
    return re.sub(r'[^A-Z0-9]', '', s.upper())


def _matches(marking, part):
    core = re.sub(r'^[A-Z]+', '', _norm(part))
    m68 = re.fullmatch(r'68(\d\d)', core)
    pat = f'68[AB]?{m68.group(1)}' if m68 else re.escape(core)
    return re.search(pat + r'(?!\d)', _norm(marking)) is not None


def identity(marking, ic):
    """A marking read on a photograph against table 8.1's part, by the rule
    fixed before any crop was read (the docstring's step 4)."""
    if ic.get('ref') in ROM_SOCKETS or ic['part'].startswith('('):
        return {'verdict': 'not compared', 'why': 'a ROM socket: its identity on the machine is the ROM in it'}
    if not marking:
        return {'verdict': 'unread'}
    if _matches(marking, ic['part']):
        return {'verdict': 'agrees'}
    if ic.get('tableSaysPreviously') and _matches(marking, ic['tableSaysPreviously']):
        return {'verdict': 'the table\'s earlier part'}
    return {'verdict': 'disagrees'}


def identity_row(rows, populated):
    """The identities row: rows {ref: {"verdict"}}; populated, how many ICs
    the photograph shows populated (ROM sockets left out)."""
    read = [r for r, v in rows.items() if v['verdict'] in ('agrees', 'disagrees', 'the table\'s earlier part')]
    bad = sorted((r for r, v in rows.items() if v['verdict'] == 'disagrees'), key=lambda r: (len(r), r))
    if len(read) < IDENTITY_MIN_READ * populated:
        verdict = 'not measured'
    else:
        verdict = 'fail' if bad else 'pass'
    return {'read': len(read), 'populated': populated, 'disagree': bad, 'verdict': verdict}


# --- geometry ------------------------------------------------------------------------------

def inside(poly, p):
    """p strictly inside the polygon (ray casting; a point on an edge is out)."""
    x, y = p
    n = len(poly)
    c = False
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        cross = (x2 - x1) * (y - y1) - (y2 - y1) * (x - x1)
        if abs(cross) <= 1e-9 * max(1.0, math.hypot(x2 - x1, y2 - y1)) and \
                min(x1, x2) - 1e-9 <= x <= max(x1, x2) + 1e-9 and min(y1, y2) - 1e-9 <= y <= max(y1, y2) + 1e-9:
            return False
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            c = not c
    return c


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
            if pa.max() <= pb.min() + 1e-6 or pb.max() <= pa.min() + 1e-6:
                return False
    return True


# --- two-lead parts ------------------------------------------------------------------------

def print_share(mask, ppm, a, b, origin=(0.0, 0.0)):
    """The share of the line from a to b, PAD_CLEAR_MM off each end, along
    which the print mask (ppm pixels a millimetre; pixel (u, v)'s centre at
    origin + ((u + 0.5) / ppm, (v + 0.5) / ppm)) shows print at any of
    ACROSS_MM across it."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    d = np.hypot(*(b - a))
    if d <= 2 * PAD_CLEAR_MM:
        return 0.0
    u = (b - a) / d
    v = np.array([-u[1], u[0]])
    s = np.arange(PAD_CLEAR_MM, d - PAD_CLEAR_MM + 1e-9, SAMPLE_MM)
    hit = np.zeros(len(s), bool)
    for o in ACROSS_MM:
        q = a + np.outer(s, u) + o * v
        px = np.floor((q - origin) * ppm).astype(int)
        ok = (px[:, 0] >= 0) & (px[:, 1] >= 0) & (px[:, 0] < mask.shape[1]) & (px[:, 1] < mask.shape[0])
        h = np.zeros(len(s), bool)
        h[ok] = mask[px[ok, 1], px[ok, 0]]
        hit |= h
    return float(hit.mean())


def two_lead_parts(pads, share, group=None):
    """Pairs of pads that are a two-lead part (step 7). pads: {index: (x, y)};
    share(a, b): the print's share of the line between pads a and b; group:
    {index: the task 3 group (a row of pads) it is in}, and two pads of one
    row are never a part (a header's pins, a link's, a transistor's legs)."""
    group = group or {}
    ids = list(pads)
    P = np.array([pads[i] for i in ids], float)
    cands = []
    for i in range(len(ids)):
        d = np.hypot(*(P - P[i]).T)
        for j in np.nonzero((d >= PAIR_MM[0]) & (d <= PAIR_MM[1]))[0]:
            if j <= i or (ids[i] in group and group.get(ids[i]) == group.get(ids[j])):
                continue
            a, b = P[i], P[j]
            u = (b - a) / d[j]
            rel = P - a
            along = rel @ u
            across = np.abs(rel @ np.array([-u[1], u[0]]))
            way = (along > PAD_CLEAR_MM) & (along < d[j] - PAD_CLEAR_MM) & (across < ON_WAY_MM)
            way[[i, j]] = False
            if way.any():
                continue
            s = share(ids[i], ids[j])
            if s >= PRINT_SHARE_MIN:
                cands.append((-s, float(d[j]), ids[i], ids[j]))
    cands.sort()
    used, out = set(), []
    for negs, d, a, b in cands:
        if a in used or b in used:
            continue
        used |= {a, b}
        pa, pb = np.array(pads[a], float), np.array(pads[b], float)
        kind = 'axial' if d >= AXIAL_MIN_MM else 'radial'
        length, dia = PASSIVE_MM[kind]
        c = (pa + pb) / 2
        out.append({'pads': [a, b], 'kind': kind, 'x': float(c[0]), 'y': float(c[1]),
                    'rotation': math.degrees(math.atan2(pb[1] - pa[1], pb[0] - pa[0])), 'length': length, 'diameter': dia,
                    'leadSpanMm': d, 'printShare': -negs})
    return out


# --- the module ----------------------------------------------------------------------------

def _js(obj):
    return json.dumps(common.plain(obj, 3), ensure_ascii=False, separators=(',', ':'))


def module_text(model):
    """The module the site reads, from parts.json's model."""
    head = [
        '// GENERATED by tools/bbc-micro-model/board_parts.py: do not edit by hand. Run it',
        '// again instead (tools/bbc-micro-model/README.md). Millimetres in the board frame:',
        '// from the board\'s left rear corner as the component side is seen with the rear',
        '// connectors at the far edge, x to the right, y towards the front; rotation in',
        '// degrees from x towards y; a body\'s l runs along its rotation, w across it.',
        '// Places come from the bare Issue 7 scans\' footprints;',
        '// identities and the fitted set from the Service Manual (D1). Bodies and heights',
        '// are typical, not measured, where HEIGHTS_TYPICAL says measured: false.',
        '//',
        '// Sources (tools/bbc-micro-model/data/sources.json): ' + ', '.join(model['sources']) + '.',
        '',
    ]
    body = [
        f"export const BOARD = {_js(model['board'])};",
        f"export const ICS = {_js(model['ics'])};",
        f"export const CONNECTORS = {_js(model['connectors'])};",
        f"export const PASSIVES = {_js(model['passives'])};",
        f"export const OTHERS = {_js(model['others'])};",
        f"export const HEIGHTS_TYPICAL = {_js(model['heights'])};",
    ]
    return '\n'.join(head + body) + '\n'


# --- the run -------------------------------------------------------------------------------

def load(name):
    with open(common.DATA / name, encoding='utf8') as f:
        return json.load(f)


def method_hashes():
    """The SHA-256 of the method's files, for the journal's record of when it was fixed."""
    files = [common.TOOL / n for n in ('board_parts.py', 'common.py', 'board_trace.py', 'board_frame.py')] + \
        [common.DATA / n for n in ('frame.json', 'registration.json', 'ic-table.json')]
    return {str(f.relative_to(common.REPO)): common.sha256(f) for f in files}


def footprints_of(reg, marks):
    """Every DIP's pads in pin order, by reference: task 3's footprints and
    the 8-pin DIPs marked by hand."""
    fps = {}
    for f in reg['footprints']:
        if f['kind'] == 'dip' and f.get('ref'):
            fps[f['ref']] = {'pads': [[reg['pads'][i]['x'], reg['pads'][i]['y']] for i in f['pads']], 'padIndexes': f['pads'],
                             'from': 'task 3', 'spacing': f.get('rowSpacingMm')}
    for m in marks['parts']['eightPin']['marks']:
        g = marked_footprint(m, reg['pads'])
        fps[m['ref']] = {'pads': g['pads'], 'padIndexes': g['padIndexes'], 'padsInferred': g['padsInferred'],
                         'from': 'pads named by hand (marks.json parts.eightPin)', 'spacing': 7.62}
    return fps


def i5_fit(reg, marks):
    """Task 4's I5 registration, refitted unrounded on the same marks."""
    import board_trace
    fps = {f['ref']: f for f in reg['footprints'] if f.get('ref')}
    pm = marks['i5Pins']['marks']
    src = np.array([m['i5Px'] for m in pm], float)
    dst = np.array([[reg['pads'][fps[m['ref']]['pads'][m['pin'] - 1]][k] for k in ('x', 'y')] for m in pm], float)
    return board_trace.register_i5(src, dst, [m['ref'] for m in pm])


def places_and_settle(reg, table, marks):
    fps = footprints_of(reg, marks)
    placed, problems = place_ics(fps, table)
    for p in placed:
        p['spacing'] = round(spacing_of(fps[p['ref']]['pads'], p['pins']) / 7.62) * 7.62
    places = {p['ref']: p for p in placed}
    manual = {i['ref']: i['manualXY'] for i in table['ics'] if i.get('manualXY')}
    settle = settle_convention(places, {r: fps[r]['pads'] for r in places}, manual, on=SETTLE_ON)
    return fps, places, problems, manual, settle


def print_mask_on_grid(fj):
    """I1's yellow print in the board frame at 16 px/mm over the outline's
    box, by board_trace.py's colour rule: (mask, origin)."""
    import board_frame
    import board_trace
    frame = board_frame.Frame(fj['pxPerMm']['x'], fj['pxPerMm']['y'], math.radians(fj['rotationDeg']), fj['origin'])
    x0, y0, W, H = board_trace.grid(fj['outline'])
    rgb = board_trace.resample(common.read_rgb('I1'), frame.to_px, x0, y0, W, H)
    L, _, B = common.oklab(rgb)
    return (B >= board_trace.PRINT_MIN_B) & (L >= board_trace.PRINT_MIN_L), (x0, y0)


def h_of(key):
    return TYPICAL_H[key][0]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--settle', action='store_true', help='settle the manual\'s convention on SETTLE_ON alone, print it, and stop')
    ap.add_argument('--crops', action='store_true', help='write crops of I5 and I6 to read the markings on, in out/, and stop')
    ap.add_argument('--hashes', action='store_true', help='print the SHA-256 of the method\'s files, and stop')
    ap.add_argument('--look', action='store_true',
                    help='place the parts, find the two-lead parts and draw them in out/; run neither held-out check, write no data')
    args = ap.parse_args()
    if args.hashes:
        for k, v in method_hashes().items():
            print(v, k)
        return 0
    warnings.simplefilter('ignore', RuntimeWarning)
    reg, fj, table, marks = load('registration.json'), load('frame.json'), load('ic-table.json'), load('marks.json')
    tab = {i['ref']: i for i in table['ics']}
    fps, places, problems, manual, settle = places_and_settle(reg, table, marks)

    if args.settle:
        for c in CONVENTIONS:
            b = settle['byConvention'][c]
            print(f"{c}: largest {b['maxMm']:.2f} mm, median {b['medianMm']:.2f}, scale {b['scale']:.4f}, turn {b['rotationDeg']:.2f} deg; "
                  + ', '.join(f'{r} {v:.2f}' for r, v in b['perPart'].items()))
        print(f"chosen: {settle['chosen']} (on {', '.join(settle['on'])}; a tie within {TIE_MM} mm goes to the earlier)")
        return 0
    if args.crops:
        crops(reg, marks, places, tab)
        return 0
    if CONVENTION is None and not args.look:
        sys.exit('CONVENTION is not set: run --settle, write it in the journal, set it, and fix the method first')
    readings = {k: {m['ref']: m for m in marks['parts'].get('identities', {}).get(k, {}).get('marks', [])} for k in ('I5', 'I6')}

    # 5. the fitted set; the model's ICs
    ICS = []
    for ic in table['ics']:
        r = ic['ref']
        if r not in places:
            continue
        p = places[r]
        l, w = body_size(p['pins'], p['spacing'])
        sock = r in MANUAL_SOCKETS or bool(readings['I5'].get(r, {}).get('socket'))
        e = {'ref': r, 'part': ic['part'], 'role': ic['role'], 'pins': p['pins'], 'x': p['x'], 'y': p['y'], 'w': w, 'l': l,
             'rotation': p['rotation'], 'socketed': sock, 'height': h_of('dip') + (h_of('socket') if sock else 0.0),
             'fitted': ic['fitted'], 'sheila': SHEILA_OF.get(r)}
        if ic.get('paged'):
            e['paged'] = ic['paged']
        ICS.append(e)

    # 6. connectors and the other large parts
    CONN, OTH = [], []
    for b in marks['parts'].get('outlines', {}).get('boxes', []):
        x0, y0, x1, y1 = b['box']
        e = {'ref': b['ref'], 'kind': b['kind'], 'x': (x0 + x1) / 2, 'y': (y0 + y1) / 2, 'l': x1 - x0, 'w': y1 - y0,
             'h': h_of(b['kind']), 'rotation': 0.0, 'label': b['label']}
        if b['group'] == 'connector':
            e['edge'] = b['edge']
            CONN.append(e)
        else:
            OTH.append(e)

    # 7. two-lead parts
    taken = set()
    for f in reg['footprints']:
        if (f['kind'] == 'dip' and f.get('ref')) or (f.get('ref') or '').startswith(('PL', 'SK', 'RP')):
            taken |= set(f['pads'])
    for m in marks['parts']['eightPin']['marks']:
        taken |= {i for i in m['pads'] if i is not None}
    free = {i: (p['x'], p['y']) for i, p in enumerate(reg['pads']) if i not in taken}
    mask, origin = print_mask_on_grid(fj)
    rows = {i: k for k, f in enumerate(reg['footprints']) for i in f['pads']}
    found = two_lead_parts(free, lambda a, b: print_share(mask, 16, free[a], free[b], origin), rows)
    PASS = [q for q in found if not any(inside(body_corners(b), (q['x'], q['y'])) for b in ICS + CONN + OTH)]

    # bodies inside the outline, and apart
    outline = fj['outline']
    out_ics = [q['ref'] for q in ICS if any(not inside(outline, c) for c in body_corners(q))]
    out_pass = [q['pads'] for q in PASS if any(not inside(outline, c) for c in body_corners(
        {'x': q['x'], 'y': q['y'], 'l': q['length'], 'w': q['diameter'], 'rotation': q['rotation']}))]
    over = [q['ref'] for q in CONN + OTH if any(not inside(outline, c) for c in body_corners(q))]
    clashes = [[a['ref'], b['ref']] for i, a in enumerate(ICS) for b in ICS[i + 1:] if overlap(a, b)]

    unplaced_fitted = [p['ref'] for p in problems if tab[p['ref']]['fitted']]
    if args.look:
        print(f"{len(places)} placed; problems {problems}; fitted not placed {unplaced_fitted}")
        print(f"inside: ICs out {out_ics}, two-lead parts out {out_pass}; overhang {over}; clashes {clashes}")
        print(f"ICs {len(ICS)} ({sum(q['fitted'] for q in ICS)} fitted), connectors {len(CONN)}, others {len(OTH)}, "
              f"two-lead parts {len(PASS)} (axial {sum(q['kind'] == 'axial' for q in PASS)}, radial {sum(q['kind'] == 'radial' for q in PASS)}) "
              f"from {len(free)} free pads; {len(found) - len(PASS)} pairs under another part's body dropped")
        overlays({'ics': ICS, 'connectors': CONN, 'others': OTH, 'passives': PASS}, fj)
        return 0

    # 2. the manual's places (held out: run once, on the method fixed)
    pts = {r: convention_point(places[r], fps[r]['pads'], CONVENTION) for r in places if r in manual}
    check = manual_check(pts, manual)

    # 3. I5 on the board
    i5 = i5_fit(reg, marks)
    h = i5['heldOutMm']
    i5_verdict = 'pass' if h['median'] <= I5_REG_PASS_MM else ('stop' if h['median'] > I5_REG_STOP_MM else 'between pass and stop')

    # 4. identities (held out: run once, on the method fixed, after the crops were read)
    ids = {}
    for k, rd in readings.items():
        ids[k] = {}
        for ref, m in rd.items():
            ids[k][ref] = {'reading': m.get('reading'), 'why': m.get('why'), **identity(m.get('reading'), tab[ref])}
    populated = sum(1 for r, m in readings['I5'].items() if r not in ROM_SOCKETS and m.get('populated'))
    id_row = identity_row(ids['I5'], populated)

    HEIGHTS = {k: {'value': v, 'measured': False, 'what': f'typical, not measured: {what}'} for k, (v, what) in TYPICAL_H.items()}
    BOARD = {'width': fj['board']['widthMm'], 'depth': fj['board']['depthMm'], 'thickness': h_of('board'), 'outline': outline,
             'holes': [{'x': q['x'], 'y': q['y'], 'd': q['d']} for q in reg['holes']] if reg.get('holes') else
             [{'x': q['x'], 'y': q['y'], 'd': q['d']} for q in fj['holes']]}
    model = {'board': BOARD, 'ics': ICS, 'connectors': CONN,
             'passives': [{k: q[k] for k in ('kind', 'x', 'y', 'rotation', 'length', 'diameter')} for q in PASS],
             'others': OTH, 'heights': HEIGHTS, 'sources': ['I1', 'I2', 'I5', 'D1']}

    verdicts = {
        'padCounts': 'pass' if not unplaced_fitted and all(places[r]['padCount'] == places[r]['pins'] for r in places) else 'fail',
        'identities': id_row['verdict'],
        'manual': check['verdict'],
        'i5Registration': i5_verdict,
        'inside': 'pass' if not out_ics and not out_pass else 'fail',
        'apart': 'pass' if not clashes else 'fail',
    }
    result = {
        'about': ('Task 5 of the BBC Micro models plan: every part on the Issue 7 board, placed on the bare scans\' footprints '
                  '(registration.json) in the board frame (frame.json), named from table 8.1 of the Service Manual (D1, through '
                  'ic-table.json), and checked against the manual\'s places and the markings on I5; I6 read and recorded. Written by '
                  'board_parts.py; every figure here is a measurement made on the date of the journal entry that quotes it. `model` '
                  'is what site/src/models/bbc-micro-board-parts.mjs exports.'),
        'model': model,
        'places': {r: {'pins': places[r]['pins'], 'padCount': places[r]['padCount'], 'x': places[r]['x'], 'y': places[r]['y'],
                       'rotation': places[r]['rotation'], 'footprint': fps[r]['from'], 'padsInferred': fps[r].get('padsInferred', [])}
                   for r in sorted(places, key=lambda r: (len(r), r))},
        'problems': problems,
        'manual': {'what': ('Table 8.1\'s places (mm from the SW corner, Y towards the rear) against each IC\'s point on the board under '
                            'the convention settled on IC51, IC52, IC20 and IC21 alone, after the similarity fitted to every placed IC '
                            'with a place in the table (the manual\'s Y flipped); none may be more than 10 mm off'),
                   'convention': CONVENTION, 'settle': settle, 'check': check},
        'i5': {'what': ('Task 4\'s registration of I5 to the board frame, refitted unrounded on the same marks (marks.json i5Pins), each '
                        'chip held out: the same fit, not a new check'),
               'registration': {k: i5[k] for k in ('model', 'points', 'chips', 'folds', 'params', 'fitMm', 'heldOutMm', 'worst')},
               'verdict': i5_verdict},
        'identities': {'what': ('Each IC\'s marking read by hand on a crop (marks.json parts.identities), against table 8.1\'s part by '
                                'the rule fixed before any crop was read (board_parts.py, step 4); I6 recorded, not judged'),
                       'I5': ids['I5'], 'I6': ids['I6'], 'row': id_row},
        'fitted': {'what': 'ic-table.json\'s fitted set, as docs/bbc-micro/facts/models.md "The fitted set" records it',
                   'fitted': [i['ref'] for i in table['ics'] if i['fitted']],
                   'notFitted': [[i['ref'], i['fittedWhy']] for i in table['ics'] if not i['fitted']]},
        'sheila': {'names': list(SHEILA), 'of': SHEILA_OF, 'from': SHEILA_FROM},
        'passives': {'what': ('Two-lead parts inferred from the print: pairs of free pads with print along the line between them '
                              '(board_parts.py, step 7); typical bodies'), 'n': len(PASS),
                     'byKind': {k: sum(1 for q in PASS if q['kind'] == k) for k in PASSIVE_MM}, 'parts': PASS,
                     'freePads': len(free), 'padsUsed': 2 * len(PASS), 'underOtherParts': len(found) - len(PASS)},
        'overhang': {'what': 'Connector and other bodies that run past the board\'s outline, as I5 shows them', 'refs': over},
        'outside': {'ics': out_ics, 'passives': out_pass},
        'clashes': clashes,
        'thresholds': THRESHOLDS,
        'verdicts': verdicts,
        'methodFixed': method_hashes(),
    }
    write(result, model)
    print(f"pad counts: {verdicts['padCounts']} ({len(places)} placed; problems: {problems})")
    print(f"I5 to the board frame: {i5['points']} pins on {i5['chips']} chips, each chip held out: median {h['median']:.3f}, "
          f"p90 {h['p90']:.3f}, max {h['max']:.3f} mm: {i5_verdict}")
    print(f"identities on I5: {id_row['read']} read of {id_row['populated']} populated; disagree {id_row['disagree']}: {id_row['verdict']}")
    print(f"manual places ({CONVENTION}): {len(pts)} ICs, largest {check['maxMm']:.2f} mm, median {check['medianMm']:.2f}, "
          f"scale {check['scale']:.4f}, turn {check['rotationDeg']:.2f} deg: {check['verdict']}")
    worst = sorted(check['perPart'].items(), key=lambda kv: -kv[1]['offMm'])[:5]
    print('largest: ' + ', '.join(f"{r} {v['offMm']:.2f}" for r, v in worst))
    print(f"inside: {verdicts['inside']} (ICs out {out_ics}, two-lead parts out {len(out_pass)}); overhang {over}; apart: {verdicts['apart']} {clashes}")
    print(f"ICs {len(ICS)} ({sum(q['fitted'] for q in ICS)} fitted), connectors {len(CONN)}, two-lead parts {len(PASS)}, others {len(OTH)}")
    overlays(model, fj)
    return 3 if i5_verdict == 'stop' else 0     # a STOP ends the run, as run-board.sh says; a failed check is recorded


def _round(obj, places=3):
    """Floats to `places`, except a homography's H, kept whole."""
    if isinstance(obj, dict):
        return {k: (v if k == 'H' else _round(v, places)) for k, v in obj.items()}
    if isinstance(obj, (list, tuple)):
        return [_round(v, places) for v in obj]
    if isinstance(obj, (float, np.floating)):
        return round(float(obj), places)
    return obj


def write(result, model):
    common.write_data('parts.json', _round(common.plain(result, None)), places=None, rows_per_line=True)
    PARTS_MJS.write_text(module_text(model), encoding='utf8')


# --- pictures to look at, and crops to read on --------------------------------------------------

def overlays(model, fj):
    """The parts drawn on the rectified I1 (out/, git-ignored)."""
    from PIL import Image, ImageDraw
    path = common.OUT / 'rectified-16.jpg'
    if not path.exists():
        return
    Image.MAX_IMAGE_PIXELS = None
    im = Image.open(path).convert('RGB')
    im = im.resize((im.width // 2, im.height // 2))
    S, M = 8, 2.0
    d = ImageDraw.Draw(im)
    P = lambda q: ((q[0] + M) * S, (q[1] + M) * S)

    def box(q, col):
        c = [P(p) for p in body_corners(q)]
        d.line(c + [c[0]], fill=col, width=2)
    for q in model['ics']:
        box(q, (255, 0, 0) if q['fitted'] else (120, 120, 255))
        d.text(P((q['x'] - 3, q['y'])), q['ref'], fill=(255, 255, 255))
    for q in model['connectors'] + model['others']:
        box(q, (0, 200, 255))
    for q in model['passives']:
        box({'x': q['x'], 'y': q['y'], 'l': q['length'], 'w': q['diameter'], 'rotation': q['rotation']},
            (255, 160, 0) if q['kind'] == 'axial' else (255, 0, 255))
    common.OUT.mkdir(exist_ok=True)
    im.save(common.OUT / 'parts-board.jpg', quality=88)


CROP_PPM = 12


def crops(reg, marks, places, tab):
    """Each placed IC's body on I5 drawn onto the board frame through the
    fit (and on I6 through a homography on the points marked by hand, where
    there are any), turned so its rows run across, labelled with its
    reference; sheets of eight in out/ (git-ignored)."""
    import cv2
    from PIL import Image, ImageDraw
    common.OUT.mkdir(exist_ok=True)
    fits = {'I5': np.array(i5_fit(reg, marks)['params']['H'])}
    i6 = marks['parts'].get('i6Points', {}).get('marks', [])
    if len(i6) >= 4:
        fps = {f['ref']: f for f in reg['footprints'] if f.get('ref')}
        src = np.array([m['i6Px'] for m in i6], float)
        dst = np.array([[reg['pads'][fps[m['ref']]['pads'][m['pin'] - 1]][k] for k in ('x', 'y')] for m in i6], float)
        fits['I6'] = np.array(common._fit('homography', src, dst)['H'])
    order = sorted(places, key=lambda r: (len(r), r))
    for name, H in fits.items():
        rgb = common.read_rgb(name)
        Hi = np.linalg.inv(H)
        tiles = []
        for r in order:
            p = places[r]
            l, w = body_size(p['pins'], p['spacing'])
            L, Wd = l + 6.0, w + 8.0
            t = math.radians(p['rotation'])
            u = np.array([math.cos(t), math.sin(t)])
            v = np.array([-math.sin(t), math.cos(t)])
            n_u, n_v = int(L * CROP_PPM), int(Wd * CROP_PPM)
            a, b = np.meshgrid((np.arange(n_u) + 0.5) / CROP_PPM - L / 2, (np.arange(n_v) + 0.5) / CROP_PPM - Wd / 2)
            mm = np.array([p['x'], p['y']]) + a[..., None] * u + b[..., None] * v
            px = common._homogeneous(Hi, mm.reshape(-1, 2)).reshape(n_v, n_u, 2).astype(np.float32)
            tile = cv2.remap(rgb, px[..., 0], px[..., 1], cv2.INTER_CUBIC, borderValue=(255, 255, 255))
            im = Image.fromarray(tile)
            im = im.resize((im.width * 2, im.height * 2), Image.LANCZOS)
            canvas = Image.new('RGB', (im.width, im.height + 18), (0, 0, 0))
            canvas.paste(im, (0, 18))
            ImageDraw.Draw(canvas).text((4, 3), f"{r} {tab[r]['part']}", fill=(255, 255, 0))
            tiles.append(canvas)
        for k in range(0, len(tiles), 8):
            group = tiles[k:k + 8]
            Wt = max(t.width for t in group)
            sheet = Image.new('RGB', (Wt, sum(t.height for t in group)), (0, 0, 0))
            y = 0
            for t in group:
                sheet.paste(t, (0, y))
                y += t.height
            sheet.save(common.OUT / f'parts-{name.lower()}-{k // 8:02d}.png')


if __name__ == '__main__':
    sys.exit(main())
