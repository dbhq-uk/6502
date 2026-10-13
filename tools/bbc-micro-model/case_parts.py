"""Task 8: the BBC Micro's outside, put together; writes data/case.json and the
module the site reads, site/src/models/bbc-micro-case-parts.mjs.

    BBC_MODEL_INPUTS=<folder> python case_parts.py           # the run (after keys.py)
    python case_parts.py --hashes                            # the SHA-256 of the method's files

The case frame: millimetres from the case's left rear corner at table level,
x to the right, y towards the front, z up. The case is the Service Manual's
415 by 345 mm, 73 mm high with its feet (D1, PDF page 109).

1. The front edge, corrected for parallax (the plan's task 8 step 3a). The
   check needs h, O1's camera height above the key plane, from a
   measurement independent of the 415 mm. None was found (H_NOT_FOUND says
   why), so the check is dropped as not measurable, as the plan says, before
   any corrected width is computed, and none is. d is measured anyway
   (case_profile.py) and recorded. The raw width is task 0's.

2. The keys placed in the case. If the side profile's check passes, K1's
   frame is put in the case by the rigid motion (a turn and a shift, no
   scale) taking the opening's two front corners, triangulated in K1's frame
   from O4 and O5, to the same corners on O1's case top registration; the
   scale those two pairs would imply is recorded. The key tops are at the
   case's 73 mm plus d. If the check fails, K1 is put in the case by the
   similarity case_register.py fits from K1 to O1's keys on the plane of the
   top, and the key tops are KEY_ABOVE_TOP_TYPICAL above the top, typical.

3. The lights: triangulated (case_profile.py) and placed as the keys are; or,
   if the check fails, O1's points on the top's plane at the panel's typical
   depth.

4. The rear (the plan's check, fixed before it ran: each port within 2.0 mm
   of its socket's x plus the board's offset in the case). The scale on O2 is
   named now, before the ports are scored: the 15-way D's two posts, which
   stand in its mounting holes, 33.325 mm apart, the middle of the 33.20 to
   33.45 mm TE Connectivity's drawing 1757819 gives for the holes of a shell
   size 2 (P-TE_1757819). A port's x in the case is the machine's left end of
   the rear face (O2's right, at y 580) less the port's x in O2, over that
   scale. The board's offset is the median over the scored ports of the
   port's x in the case less its socket's x on the board (parts.json): SK1,
   SK3, SK4, SK5 and SK6. SK2 (the video BNC, on the case and wired to the
   board) and SK7 (Econet, not fitted on the emulated machine and with no
   place on the board) are placed from O2 alone and not scored. A port's
   height is read on the rear face's own height, its top edge at 73 mm and
   its bottom at the feet's height, so the photograph's tilt does not enter.

5. The rest. The opening and the badge strip from O1 on the top's plane; the
   vents and the mains panel from O2; the underside connectors at PL8 to
   PL12's places on the board plus the board's offset, named from O8's
   words in O8's order; the board's front-to-back place and height, the
   feet, the corners' radius and the underside panel are typical
   (TYPICAL), each said so.
"""
import argparse
import json
import math
import statistics
import sys

import numpy as np

import case_profile
import case_register
import common
import keys as keyslib

CASE_MJS = common.REPO / 'site' / 'src' / 'models' / 'bbc-micro-case-parts.mjs'
CASE_W, CASE_D, CASE_H = case_register.CASE_W, case_register.CASE_D, case_register.CASE_H

# --- fixed before the run ---------------------------------------------------------------------
DSUB_HOLES_MM = (33.20 + 33.45) / 2      # P-TE_1757819, shell size 2 (15 positions), dimension C
REAR_LIMIT_MM = 2.0
SCORED = ('SK1', 'SK3', 'SK4', 'SK5', 'SK6')
H_NOT_FOUND = (
    'No measurement of h independent of the 415 mm was found. K2, the keycap model the plan suggested for the step in height between rows, '
    'could not be fetched: Thingiverse answered both its page and its download with a 403 challenge page, and its API asks for a token, so '
    'the cross-check was dropped as the plan says (13 October 2026). O1 has no Exif. O1 looks almost straight down: the case\'s front face '
    'shows as a strip about 5 px tall and its base is not seen, so no upright edge gives a vanishing point and no known height can be '
    'measured on it. O4\'s and O5\'s camera is another camera. The key fit\'s own camera gives h only from its right-angle constraint, '
    'which task 0 found loose (536 mm, jackknife standard error 355 mm) and the plan rules out.')
TYPICAL = {
    'feet': {'value': 5.0, 'measured': False, 'what': 'typical, not measured: the feet\'s height; the case\'s 73 mm includes them (D1)'},
    'cornerRadius': {'value': 3.0, 'measured': False, 'what': 'typical, not measured: the radius of the case\'s corners seen from above'},
    'keyAboveTop': {'value': 5.0, 'measured': False, 'what': 'typical, used only if the side profile\'s check fails: how far the key tops stand above the case top'},
    'panelBelowKeys': {'value': 8.0, 'measured': False, 'what': 'typical, used only if the side profile\'s check fails: how far the panel round the keys lies below the key tops'},
    'boardY': {'value': 4.5, 'measured': False, 'what': 'typical, not measured: the board\'s rear edge from the case\'s rear face, a 3 mm wall and the DIN sockets\' 1.5 mm overhang'},
    'boardZ': {'value': 14.0, 'measured': False, 'what': 'typical, not measured: the board\'s top face above the table'},
    'wall': {'value': 3.0, 'measured': False, 'what': 'typical, not measured: the case\'s wall, which the underside panel stops at'},
    'undersideMargin': {'value': 10.0, 'measured': False, 'what': 'typical, not measured: the underside panel\'s margin round its connectors'},
    'portSize': {'value': {'phono': 9.0, 'bnc': 14.0, 'din': 16.0, 'dType': [39.1, 12.5]}, 'measured': False,
                 'what': 'typical, not measured: a port\'s width and height on the rear face (the D\'s width is TE\'s flange, 38.76 to 39.52 mm)'},
}
THRESHOLDS = {
    # The clause below on the factor's interval was never evaluated: no h was had, so the verdict is the constant
    # 'not measurable', decided before the run (the journal, task 8). Kept as written, being part of the fixed method.
    'frontEdge': ('the front edge\'s width corrected for parallax, its interval plus or minus two standard errors (h, d and the raw width in '
                  'quadrature): pass if all of it is within 1.5 per cent of 415 mm, stop if all of it is outside 2.5, else inconclusive; not '
                  'measurable if no independent h, or if the factor (h + d) / h has an interval wider than 3 per cent of its value, decided '
                  'before the width is computed'),
    'rear': 'each scored rear port within 2.0 mm of its socket\'s x on the board plus the board\'s offset in the case, on the D shell\'s scale; else FAIL, recorded and pinned',
}


def t(v):
    return TYPICAL[v]['value']


def load(name):
    with open(common.DATA / name, encoding='utf8') as f:
        return json.load(f)


def circle(points):
    """The circle through points on an arc, by least squares (Kasa): (cx, cy, r)."""
    p = np.array(points, float)
    A = np.c_[2 * p, np.ones(len(p))]
    b = (p ** 2).sum(1)
    cx, cy, c = np.linalg.lstsq(A, b, rcond=None)[0]
    return float(cx), float(cy), float(math.sqrt(c + cx * cx + cy * cy))


def rear(parts):
    """The rear ports, vents and mains panel from O2, and the board's offset; the check."""
    o2 = keyslib.case_marks()['o2']
    posts = np.array([o2['dsubPosts']['left'], o2['dsubPosts']['right']], float)
    scale = float(np.hypot(*(posts[1] - posts[0]))) / DSUB_HOLES_MM          # px per mm along the rear face
    xl = o2['caseEnds']['machineLeft'][0]
    xr = o2['caseEnds']['machineRight'][0]
    top, bot = o2['rearFace']['topY'], o2['rearFace']['bottomY']
    zof = lambda py: t('feet') + (bot - py) / (bot - top) * (CASE_H - t('feet'))
    xof = lambda px: (xl - px) / scale
    board = {c['ref']: c for c in parts['model']['connectors']}
    ports = []
    for p in o2['ports']:
        if p['kind'] == 'din':
            cx, cy, r = circle(p['arc'])
            centre, how = (cx, cy), f'the circle through {len(p["arc"])} points on its rim\'s lit arc (radius {r:.1f} px)'
        elif p['centre'] == 'dsubPosts':
            centre, how = tuple(posts.mean(0)), 'the middle of its two posts'
        else:
            centre, how = tuple(p['centre']), p['what']
        size = TYPICAL['portSize']['value'][p['kind']]
        w, h = (size, size) if not isinstance(size, list) else size
        ports.append({'label': p['label'], 'ref': p['ref'], 'kind': p['kind'], 'px': list(centre), 'how': how,
                      'x': xof(centre[0]), 'z': zof(centre[1]), 'w': w, 'h': h,
                      'boardX': board[p['ref']]['x'] if p['ref'] in board else None})
    scored = [p for p in ports if p['ref'] in SCORED]
    offset = statistics.median(p['x'] - p['boardX'] for p in scored)
    for p in ports:
        p['scored'] = p['ref'] in SCORED
        if p['scored']:
            p['errMm'] = p['x'] - (p['boardX'] + offset)
    worst = max(abs(p['errMm']) for p in scored)
    within = sum(1 for p in scored if abs(p['errMm']) <= REAR_LIMIT_MM)
    verdict = 'pass' if within == len(scored) else 'fail'
    order = [p['label'] for p in sorted(ports, key=lambda p: -p['x'])]      # as O2 shows them, left to right
    vents = [{'x0': xof(b), 'x1': xof(a), 'z0': zof(o2['vents']['y'][1]), 'z1': zof(o2['vents']['y'][0])} for a, b in o2['vents']['slots']]
    mb = o2['mains']['box']
    mains = {'x0': xof(mb[2]), 'x1': xof(mb[0]), 'z0': zof(mb[3]), 'z1': zof(mb[1]), 'words': o2['mains']['words']}
    width = (xl - xr) / scale
    return {
        'scale': {'pxPerMm': scale, 'from': f'the 15-way D\'s posts, {np.hypot(*(posts[1] - posts[0])):.2f} px apart, taken as {DSUB_HOLES_MM} mm (P-TE_1757819)'},
        'ports': ports, 'order': order, 'vents': vents, 'mains': mains,
        'offsetX': offset,
        'check': {'scored': list(SCORED), 'within': within, 'n': len(scored), 'worstMm': worst, 'limitMm': REAR_LIMIT_MM, 'verdict': verdict,
                  'errMm': {p['ref']: p['errMm'] for p in scored}},
        'widthOnScaleMm': width,
        'widthWhat': 'the case\'s width at y 580 on O2 on the D shell\'s scale, against D1\'s 415 mm: recorded, not judged',
    }


def rigid(src, dst):
    """The turn and shift taking two points src to two points dst (no scale), and the scale the pairs imply."""
    s, d = np.array(src, float), np.array(dst, float)
    a = math.atan2(*(d[1] - d[0])[::-1]) - math.atan2(*(s[1] - s[0])[::-1])
    R = np.array([[math.cos(a), -math.sin(a)], [math.sin(a), math.cos(a)]])
    tr = d.mean(0) - R @ s.mean(0)
    return {'matrix': np.c_[R, tr].tolist(), 'rotationDeg': math.degrees(a),
            'impliedScale': float(np.hypot(*(d[1] - d[0])) / np.hypot(*(s[1] - s[0])))}


def apply(M, pts):
    M = np.array(M, float)
    return np.array(pts, float) @ M[:, :2].T + M[:, 2]


def measure():
    kj = load('keys.json')
    parts = load('parts.json')
    top = case_register.measure()
    prof = case_profile.measure()
    ok = prof['check']['verdict'] == 'pass'
    opening = top['opening']                      # rear left, rear right, front right, front left (case frame)
    if ok:
        P = prof['points']
        tr = rigid([[P['openingFrontLeft']['x'], P['openingFrontLeft']['y']], [P['openingFrontRight']['x'], P['openingFrontRight']['y']]],
                   [opening[3], opening[2]])
        tr['from'] = ('the rigid motion taking the opening\'s two front corners, triangulated in K1\'s frame from O4 and O5, to the same corners '
                      'on O1\'s case top registration')
        d = prof['d']['mm']
        keyZ = CASE_H + d
        leds = [{'name': n, 'z': keyZ + P[k]['z']} for n, k in (('cassette motor', 'ledCassetteMotor'), ('caps lock', 'ledCapsLock'), ('shift lock', 'ledShiftLock'))]
        for e, k in zip(leds, ('ledCassetteMotor', 'ledCapsLock', 'ledShiftLock')):
            e['x'], e['y'] = apply(tr['matrix'], [[P[k]['x'], P[k]['y']]])[0]
        ledsFrom = 'triangulated from O4 and O5, placed in the case as the keys are'
        panelZ = float(np.mean([e['z'] for e in leds]))
        keyFrom = f'the case\'s {CASE_H:g} mm plus d, the front edge\'s depth below the key plane triangulated from O4 and O5'
    else:
        kt = top['keysOnTop']
        tr = {'matrix': kt['matrix'], 'rotationDeg': kt['rotationDeg'], 'impliedScale': kt['scale'],
              'from': 'the side profile\'s check failed: the similarity from K1 to O1\'s keys on the plane of the top (case_register.py), so the keys are placed as if their tops were in that plane'}
        keyZ = CASE_H + t('keyAboveTop')
        panelZ = keyZ - t('panelBelowKeys')
        leds = [{'name': n, 'x': p[0], 'y': p[1], 'z': panelZ} for n, p in zip(top['ledsOnTop']['order'], top['ledsOnTop']['points'])]
        ledsFrom = 'O1 on the plane of the top, at the panel\'s typical depth (the side profile\'s check failed)'
        keyFrom = 'typical: the side profile\'s check failed'
    M = np.array(tr['matrix'], float)
    turn = math.degrees(math.atan2(M[1, 0], M[0, 0]))
    KEYS = []
    for k in kj['keys']:
        x, y = apply(M, [[k['x'], k['y']]])[0]
        KEYS.append({'name': k['name'], 'x': x, 'y': y, 'z': keyZ, 'w': k['w'], 'd': k['d'], 'rotation': turn, 'row': k['row'], 'colour': k['colour']})
    r = rear(parts)
    boardIn = {'x': r['offsetX'], 'y': t('boardY'), 'z': t('boardZ'),
               'from': {'x': 'O2: the median of each scored rear port\'s x in the case less its socket\'s x on the board', 'y': TYPICAL['boardY']['what'], 'z': TYPICAL['boardZ']['what']}}
    o8 = keyslib.case_marks()['o8']
    under = sorted([c for c in parts['model']['connectors'] if c['edge'] == 'underside'], key=lambda c: -c['x'])
    UNDER = []
    for c, words in zip(under, o8['connectorsLeftToRight']):
        UNDER.append({'ref': c['ref'], 'label': words, 'boardLabel': c['label'], 'x': c['x'] + boardIn['x'], 'y': c['y'] + boardIn['y'],
                      'w': c['l'], 'd': c['w']})
    m = t('undersideMargin')
    wall = TYPICAL['wall']['value']       # the panel stops at the case's walls (added after the first run, whose panel ran past the case's right end)
    panel = {'x0': max(min(u['x'] - u['w'] / 2 for u in UNDER) - m, wall), 'x1': min(max(u['x'] + u['w'] / 2 for u in UNDER) + m, CASE_W - wall),
             'y0': min(u['y'] - u['d'] / 2 for u in UNDER) - m, 'y1': max(u['y'] + u['d'] / 2 for u in UNDER) + m}
    front = {
        'rawMm': kj['frontEdgeMm'], 'rawSeMm': kj['parallax']['rawWidthSeMm'],
        'h': {'mm': None, 'seMm': None, 'from': H_NOT_FOUND},
        'd': prof['d'],
        'correctedMm': None,
        'verdict': 'not measurable',
        'why': 'no h independent of the 415 mm could be had, so the check was dropped before any corrected width was computed (the plan, task 8 step 3a)',
    }
    profile = [{'y': 0.0, 'z': t('feet')}, {'y': 0.0, 'z': CASE_H}, {'y': CASE_D, 'z': CASE_H}, {'y': CASE_D, 'z': t('feet')}]
    case = {
        'about': ('Task 8 of the BBC Micro models plan: the outside measured. Written by case_parts.py; every figure is a measurement made on '
                  'the date of the journal entry that quotes it, except where `typical` says it is not. The case frame: millimetres from the '
                  'case\'s left rear corner at table level, x to the right, y towards the front, z up.'),
        'footprint': {'widthMm': CASE_W, 'depthMm': CASE_D, 'source': 'D1'},
        'heightMm': CASE_H,
        'frontEdge': front,
        'caseTop': {**top['keysOnTop'], 'registeredOn': 'O1\'s four case top corners, taken as 415 by 345 mm'},
        'outline': {'widthMm': CASE_W, 'depthMm': CASE_D, 'cornerRadiusMm': t('cornerRadius'), 'from': 'D1\'s footprint; the corners\' radius typical'},
        'recess': {'opening': opening, 'panelZ': panelZ, 'from': 'O1 on the case top\'s registration; the panel\'s height from the lights\''},
        'profile': profile,
        'profileFrom': ('D1: the top level at 73 mm with the feet, front and rear upright; the feet typical' +
                        ('; the key plane and the panel from the triangulation' if ok else '; the key plane and the panel typical, the triangulation having failed its check')),
        'profileCheck': {'what': f"the key {prof['check']['key']}'s legend, left out of the camera fit, triangulated from O4 and O5 against K1's place on the key plane",
                         'errMm': prof['check']['errMm'], 'limitMm': prof['check']['limitMm'], 'verdict': prof['check']['verdict'],
                         'triangulated': prof['check']['triangulated'], 'k1': prof['check']['k1'], 'fallback': not ok},
        'triangulation': {'fit': prof['fit'], 'points': prof['points'], 'topAtOpening': prof['topAtOpening'], 'jackknife': prof['jackknife']},
        'keyPlaneZ': keyZ, 'keyPlaneFrom': keyFrom,
        'k1ToCase': tr,
        'leds': leds, 'ledsFrom': ledsFrom, 'ledsOnTop': top['ledsOnTop'],
        'badge': {'strip': top['badgeStrip'], 'words': top['badgeWords']['words'], 'wordsBox': top['badgeWords']['box'],
                  'from': 'O1 on the case top\'s registration; the strip lies a little below the top, so it is placed to a few millimetres. No owl is drawn or measured'},
        'grille': {'slots': r['vents'], 'face': 'rear', 'from': 'O2, on the D shell\'s scale and the rear face\'s height'},
        'rear': [{k: p[k] for k in ('label', 'ref', 'kind', 'x', 'z', 'w', 'h', 'scored', 'how')} | ({'errMm': p['errMm']} if p['scored'] else {}) for p in r['ports']],
        'rearOrder': r['order'],
        'rearScale': r['scale'],
        'rearCheck': r['check'],
        'mains': r['mains'],
        'widthOnRear': {'mm': r['widthOnScaleMm'], 'what': r['widthWhat']},
        'underside': UNDER,
        'undersidePanel': {**panel, 'from': 'typical: a margin round the connectors; O8 shows them in one recessed panel under the front'},
        'boardInCase': boardIn,
        'feet': {'heightMm': t('feet'), 'from': 'typical'},
        'typical': TYPICAL,
        'thresholds': {**THRESHOLDS, **case_profile.THRESHOLDS, **case_register.THRESHOLDS, **keyslib.THRESHOLDS},
        'verdicts': {'frontEdge': front['verdict'], 'profileCheck': prof['check']['verdict'], 'rear': r['check']['verdict'],
                     'mapping': kj['verdicts']['mapping'], 'o1': kj['verdicts']['o1'], 'o3': kj['verdicts']['o3']},
        'keys': KEYS,
        'sources': ['D1', 'K1', 'O1', 'O2', 'O3', 'O4', 'O5', 'O8-I', 'P-TE_1757819'],
    }
    print(f"front edge: raw {front['rawMm']:.1f} mm; h not obtained; d {prof['d']['mm']:.2f} mm (se {prof['d']['seMm']:.2f}): not measurable")
    print(f"rear: scale {r['scale']['pxPerMm']:.4f} px/mm; board offset x {r['offsetX']:.2f} mm; {r['check']['within']} of {r['check']['n']} within "
          f"{REAR_LIMIT_MM} mm, worst {r['check']['worstMm']:.2f}: {r['check']['verdict']}")
    for p in r['ports']:
        print(f"  {p['ref']} {p['label']}: case x {p['x']:.2f}" + (f", board x {p['boardX']:.2f} plus offset, error {p['errMm']:+.2f} mm" if p['scored'] else ', not scored'))
    print(f"the case's width on O2's scale: {r['widthOnScaleMm']:.1f} mm (recorded)")
    return case


def width_words(case):
    """What the module says of the case's size: the manual's, and the outside's measures, which point different ways.
    Added after the last run, to the module's notes only (case.json is that run's): task 8's review asked for it.
    Corrected after task 9a's review (13 October 2026): it had said the three measures read the case wider. The two
    K1 scales are the keys' size on a frame that takes the case as 415 mm, so if K1 is true they make the case
    narrower; only the rear's D scale makes it wider. The keys' scale on O1's top may be partly parallax, the key
    tops standing above the plane registered (case.json caseTop.what)."""
    top, corners, rear = case['caseTop']['scale'], case['k1ToCase']['impliedScale'], case['widthOnRear']['mm']
    return (f"the Service Manual's size; the outside's measures point different ways, not resolved: in K1's units the keys read {top:.4f} "
            f"and the opening's front corners {corners:.4f} times a frame that takes the case as {CASE_W:g} mm, so if K1 is true to size "
            f"the case is narrower, about {CASE_W / max(top, corners):.0f} mm, the first perhaps partly parallax, the key tops standing "
            f"above the plane registered; on O2's 15-way D scale it reads {rear:.1f} mm, wider")


def module_text(case):
    width = width_words(case)
    rear_scale = (f"the rear's x values are on the 15-way D's scale, on which the case reads {case['widthOnRear']['mm']:.1f} mm wide, "
                  f"not rescaled to {CASE_W:g} mm, while the opening and the keys are on a frame that takes the case as {CASE_W:g} mm")
    head = [
        '// GENERATED by tools/bbc-micro-model/case_parts.py: do not edit by hand. Run it',
        '// again instead (tools/bbc-micro-model/README.md). Millimetres in the case frame:',
        '// from the case\'s left rear corner at table level, x to the right, y towards the',
        '// front, z up; rotation in degrees from x towards y. A key\'s w and d are the cell',
        '// it stands in (K1\'s footprint), not its cap; its legend is LEGENDS[name] in',
        '// site/public/bbc-keys.js. Each export\'s note says where it comes from; what is',
        '// typical, not measured, says so.',
        '//',
        '// Sources (tools/bbc-micro-model/data/sources.json): ' + ', '.join(case['sources']) + '.',
        '',
    ]
    js = lambda o: json.dumps(common.plain(o, 3), ensure_ascii=False, separators=(',', ':'))
    body = {
        'CASE': {'width': case['footprint']['widthMm'], 'depth': case['footprint']['depthMm'], 'height': case['heightMm'],
                 'cornerRadius': case['outline']['cornerRadiusMm'], 'note': f'D1\'s 415 by 345 by 73 mm with the feet, {width}; the corners\' radius typical'},
        'RECESS': {'opening': case['recess']['opening'], 'top': case['heightMm'], 'panel': case['recess']['panelZ'], 'note': f"{case['recess']['from']}; the case top taken as {width}"},
        'PROFILE': {'points': case['profile'], 'keyPlane': case['keyPlaneZ'], 'fallback': case['profileCheck']['fallback'], 'note': case['profileFrom']},
        'KEYS': {'keys': case['keys'], 'note': f"K1's centres and cells, placed by {case['k1ToCase']['from']}; tops at {case['keyPlaneFrom']}; colours by class from O1"},
        'LEDS': {'leds': [{k: e[k] for k in ('name', 'x', 'y', 'z')} for e in case['leds']], 'note': case['ledsFrom']},
        'BADGE': {'strip': case['badge']['strip'], 'words': case['badge']['words'], 'wordsBox': case['badge']['wordsBox'], 'note': case['badge']['from']},
        'GRILLE': {'slots': case['grille']['slots'], 'face': 'rear', 'note': case['grille']['from']},
        'REAR': {'ports': [{k: p[k] for k in ('label', 'ref', 'kind', 'x', 'z', 'w', 'h')} for p in case['rear']], 'mains': case['mains'],
                 'note': 'O2 on the 15-way D shell\'s scale; the sizes typical; ' + ('the check passed' if case['rearCheck']['verdict'] == 'pass' else 'the check failed as measured')
                 + f'; {rear_scale}; the case itself is {width}'},
        'UNDERSIDE': {'connectors': [{k: u[k] for k in ('ref', 'label', 'x', 'y', 'w', 'd')} for u in case['underside']], 'panel': case['undersidePanel'],
                      'note': 'PL8 to PL12\'s places on the board plus the board\'s offset in the case, named as O8 shows them; the panel typical'},
        'FEET': {'height': case['feet']['heightMm'], 'note': 'typical, not measured'},
    }
    return '\n'.join(head + [f'export const {k} = {js(v)};' for k, v in body.items()]) + '\n'


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--hashes', action='store_true', help='print the SHA-256 of the method\'s files, and stop')
    ap.add_argument('--module', action='store_true', help='write the module from data/case.json as it is, measuring nothing, and stop')
    args = ap.parse_args()
    if args.hashes:
        for k, v in keyslib.method_hashes().items():
            print(v, k)
        return 0
    if args.module:
        CASE_MJS.write_text(module_text(load('case.json')), encoding='utf8')
        return 0
    case = measure()
    case['methodFixed'] = keyslib.method_hashes()
    common.write_data('case.json', case)
    CASE_MJS.write_text(module_text(case), encoding='utf8')
    return 0


if __name__ == '__main__':
    sys.exit(main())
