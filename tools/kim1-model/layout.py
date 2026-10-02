"""Measures where the KIM-1's parts are, and writes the model's parts list.

    python layout.py [--parts-only]

--parts-only rewrites the parts list from data/layout.json without measuring
again. Reads data/kicad-parts.json, data/heights.json and the rectified photographs
(out/rect-*.png, rectify.py), and writes:

- site/src/models/kim-1-parts.mjs, the generated parts list the model draws;
- data/layout.json, the measurements behind it, with their residuals.

Where each kind of part comes from:

- Every part soldered through the board (the integrated circuits, resistors,
  capacitors, diodes, transistors, the trimmer, the crystal's leads, the
  keypad's connector and the edge contacts) is placed by its pads, from the
  KiCad replica, which the photographs are registered to. Checked: each
  integrated circuit's body is found in two registered photographs by
  sliding a rectangle of its size over where the photograph does not show
  the board, and the offset that covers most of it is the residual.
- The parts that sit on the board without being placed by their own pads
  (the keypad, its bezel and keys, the display's window and the crystal's
  can) are measured on the photographs: one corner of each is triangulated
  in three dimensions (heights.py), which no parallax can move, and its size
  and the keys' centres are measured on the rectified photographs relative to
  that corner.

Heights are heights.py's measurements where there is one, and otherwise the
part's datasheet or footprint value, and the generated file says which.
"""
import json
import os

import cv2
import numpy as np
from PIL import Image

import common

R = 12
TOP = ['bolo-top', 'rev-b-front']


def load(sid):
    rgb = np.asarray(Image.open(os.path.join(common.OUT, f'rect-{sid}.png')).convert('RGB'))
    t = np.asarray(Image.open(os.path.join(common.OUT, f'trace-{sid}.png')).convert('RGB'))
    return rgb, t[..., 1] > 127


def px(x, y):
    x0, y0, _, _ = common.FRAME
    return int(round((x - x0) * R)), int(round((y - y0) * R))


def mm(c, r):
    x0, y0, _, _ = common.FRAME
    return c / R + x0, r / R + y0


DIP = {40: (52.0, 13.7), 16: (19.3, 6.4), 14: (19.0, 6.4), 8: (9.6, 6.4)}


def dip_offset(hidden, centre, along_x, pins):
    """Where a DIP body of this size sits on the region the photograph does
    not show, within 3 mm of the replica's centre: the offset in mm, and how
    much of the body box is hidden there. The template is +1 over the body,
    -1 over a 1.5 mm band beyond each end and over a band 2.2 to 3.2 mm beyond
    each long side (outside the legs and their pads), where the board should
    show; so the ends fix it along the package and the bands across it."""
    length, width = DIP[pins]
    L_, W_ = int(length * R), int(width * R)
    e, s0, s1 = int(1.5 * R), int(2.2 * R), int(3.2 * R)
    t = np.zeros((W_ + 2 * s1, L_ + 2 * e), np.float32)
    t[s1:s1 + W_, e:e + L_] = 1
    t[s1:s1 + W_, :e] = -1
    t[s1:s1 + W_, e + L_:] = -1
    t[s1 - s0 - (s1 - s0):s1 - s0, e:e + L_] = -1
    t[s1 + W_ + s0:s1 + W_ + s1, e:e + L_] = -1
    t /= np.abs(t).sum()
    if not along_x:
        t = t.T.copy()
    th, tw = t.shape
    cx, cy = px(*centre)
    s = int(3 * R)
    win = hidden[cy - th // 2 - s:cy - th // 2 + th + s, cx - tw // 2 - s:cx - tw // 2 + tw + s].astype(np.float32)
    score = cv2.matchTemplate(win, t, cv2.TM_CCORR)
    best = np.unravel_index(np.argmax(score), score.shape)
    body = win[best[0]:best[0] + th, best[1]:best[1] + tw][t > 0]
    return (best[1] - s) / R, (best[0] - s) / R, float(body.mean())


def box_of(mask, near, window):
    """The bounding box, in mm, of the biggest region of `mask` inside
    `window` ([x0, y0, x1, y1] in mm) that contains or is nearest `near`."""
    a, b = px(window[0], window[1])
    c, d = px(window[2], window[3])
    sub = mask[b:d, a:c].astype(np.uint8)
    n, labels, stats, cents = cv2.connectedComponentsWithStats(sub, connectivity=8)
    if n < 2:
        return None
    k = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    x, y, w, h = stats[k, :4]
    x0, y0 = mm(a + x, b + y)
    x1, y1 = mm(a + x + w, b + y + h)
    return [round(float(x0), 2), round(float(y0), 2), round(float(x1), 2), round(float(y1), 2)]


def main():
    import sys
    if '--parts-only' in sys.argv:
        # Rewrite kim-1-parts.mjs from the measurements already in data/layout.json.
        parts = json.load(open(os.path.join(common.DATA, 'kicad-parts.json'), encoding='utf8'))['parts']
        heights = {p['part']: p for p in json.load(open(os.path.join(common.DATA, 'heights.json'), encoding='utf8'))['points'] if p['part'] != 'check'}
        write_parts(parts, heights, json.load(open(os.path.join(common.DATA, 'layout.json'), encoding='utf8')))
        return
    parts = json.load(open(os.path.join(common.DATA, 'kicad-parts.json'), encoding='utf8'))['parts']
    heights = {p['part']: p for p in json.load(open(os.path.join(common.DATA, 'heights.json'), encoding='utf8'))['points'] if p['part'] != 'check'}
    by_ref = {p['ref']: p for p in parts}
    report = {'dips': {}, 'measured': {}}

    # 1. The integrated circuits, checked against two photographs.
    photos = {sid: load(sid) for sid in TOP + ['bolo-end']}
    offsets = []
    for p in parts:
        if not p['footprint'].startswith('DIP-'):
            continue
        pins = p['padCount'] if p['padCount'] in DIP else int(p['footprint'].split('-')[1].split('_')[0])
        a, b, c, d = p['pads']
        along_x = (c - a) > (d - b)
        entry = {'centre': p['centre'], 'pins': pins, 'alongX': along_x}
        for sid in TOP:
            _, shows = photos[sid]
            dx, dy, cover = dip_offset(~shows, p['centre'], along_x, pins)
            entry[sid] = {'dx': round(dx, 2), 'dy': round(dy, 2), 'cover': round(cover, 2)}
            if cover > 0.85:
                offsets.append(np.hypot(dx, dy))
        report['dips'][p['ref']] = entry
    offsets = np.array(offsets)
    report['dipResidualMm'] = {'n': int(len(offsets)), 'median': round(float(np.median(offsets)), 3), 'p90': round(float(np.percentile(offsets, 90)), 3), 'max': round(float(offsets.max()), 3)}
    print(f"integrated circuits: {len(report['dips'])}; body found in a photograph {len(offsets)} times (at least 85% of the box not showing the board); offset from the replica's centre: median {report['dipResidualMm']['median']} mm, 90th percentile {report['dipResidualMm']['p90']}, max {report['dipResidualMm']['max']}")

    # 2. The keypad, the display's window and the crystal's can, measured on
    #    the rectified photographs and anchored at a triangulated corner.
    # On bolo-top, the photograph taken square on, where a part's top is
    # displaced least by parallax.
    rgb, shows = photos['bolo-top']
    L, A, B = common.oklab(rgb)
    C = np.hypot(A, B)
    # The bezel's edges are hand-marked (data/layout-marks.json): it is black
    # on a dark board, with black keys in it, and no threshold separated it
    # from the board round it reliably.
    bm = json.load(open(os.path.join(common.DATA, 'layout-marks.json'), encoding='utf8'))['keypadBezel']
    bezel = [bm['left'], bm['top'], bm['right'], bm['bottom']]
    black = cv2.morphologyEx((L < 0.08).astype(np.uint8), cv2.MORPH_CLOSE, np.ones((15, 15), np.uint8)) > 0
    window = box_of(black, (155, 147), (115, 133, 198, 163))
    silver = cv2.morphologyEx(((C < 0.06) & (L > 0.5)).astype(np.uint8), cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8)) > 0
    can = box_of(silver, (146, 17), (134, 4, 162, 30))
    # Shift each box so that its corner is the triangulated one.
    def anchor(box, corner, which):
        x, y = corner
        dx = x - box[0] if which in ('tl', 'bl') else x - box[2]
        dy = y - box[1] if which in ('tl', 'tr') else y - box[3]
        return [round(float(box[0] + dx), 2), round(float(box[1] + dy), 2), round(float(box[2] + dx), 2), round(float(box[3] + dy), 2)], (round(float(dx), 2), round(float(dy), 2))
    bezel_a, bezel_shift = anchor(bezel, heights['keypad-bezel']['xy'], 'tl')
    window_a, window_shift = anchor(window, heights['display']['xy'], 'tl')
    cx, cy = heights['crystal']['xy']
    w, h = can[2] - can[0], can[3] - can[1]
    can_a = [round(cx - w / 2, 2), round(cy - h / 2, 2), round(cx + w / 2, 2), round(cy + h / 2, 2)]
    report['measured'] = {
        'bezel': {'rectified': bezel, 'anchored': bezel_a, 'shift': bezel_shift},
        'window': {'rectified': window, 'anchored': window_a, 'shift': window_shift},
        'can': {'rectified': can, 'anchored': can_a},
    }
    dbg = rgb.copy()
    for bx in (bezel, window, can):
        p0, p1 = px(bx[0], bx[1]), px(bx[2], bx[3])
        cv2.rectangle(dbg, p0, p1, (255, 0, 255), 2)
    Image.fromarray(dbg).save(os.path.join(common.OUT, 'layout-boxes.png'))
    print(f'keypad bezel: {bezel_a} (moved by {bezel_shift} mm to its triangulated corner); display window: {window_a} (moved by {window_shift}); crystal can: {can_a}')

    # The keys: the white print of their legends. A regular grid of four
    # columns and six rows is fitted by searching pitch and origin for the
    # most legend inside a 6 mm square round each point; then each key is
    # moved to the median of the legend pixels within 4 mm of its point, twice.
    # SST (the top right cell) is a switch, not a key, and is left out.
    legend = (L > 0.75) & (C < 0.06)
    ii = cv2.integral(legend.astype(np.uint8))
    def inside(x, y, half):
        c0, r0 = px(x - half, y - half)
        c1, r1 = px(x + half, y + half)
        r1, c1 = min(r1, ii.shape[0] - 1), min(c1, ii.shape[1] - 1)
        return ii[r1, c1] - ii[r0, c1] - ii[r1, c0] + ii[r0, c0]
    best = None
    for pitch_x in np.arange(12.0, 13.6, 0.1):
        for pitch_y in np.arange(12.0, 13.6, 0.1):
            for x0 in np.arange(bezel[0] + 6, bezel[0] + 16, 0.25):
                for y0 in np.arange(bezel[1] + 6, bezel[1] + 16, 0.25):
                    score = sum(inside(x0 + i * pitch_x, y0 + j * pitch_y, 3) for i in range(4) for j in range(6) if not (i == 3 and j == 0))
                    if best is None or score > best[0]:
                        best = (score, x0, y0, pitch_x, pitch_y)
    _, gx, gy, gpx, gpy = best
    keys = {}
    for j in range(6):
        for i in range(4):
            if i == 3 and j == 0:
                continue
            x, y = gx + i * gpx, gy + j * gpy
            for _ in range(2):
                c0, r0 = px(x - 4, y - 4)
                c1, r1 = px(x + 4, y + 4)
                rr, cc = np.nonzero(legend[r0:r1, c0:c1])
                if len(rr) > 20:
                    x, y = mm(c0 + np.median(cc), r0 + np.median(rr))
            keys[(i, j)] = (x, y)
            cv2.circle(dbg, px(x, y), 6, (0, 255, 255), 2)
    Image.fromarray(dbg).save(os.path.join(common.OUT, 'layout-boxes.png'))
    col_c = [float(np.median([keys[(i, j)][0] for j in range(6) if (i, j) in keys])) + bezel_shift[0] for i in range(4)]
    row_c = [float(np.median([keys[(i, j)][1] for i in range(4) if (i, j) in keys])) + bezel_shift[1] for j in range(6)]
    resid = [np.hypot(keys[k][0] + bezel_shift[0] - col_c[k[0]], keys[k][1] + bezel_shift[1] - row_c[k[1]]) for k in keys]
    report['keys'] = {'found': len(keys), 'pitch': [round(gpx, 2), round(gpy, 2)], 'columns': [round(v, 2) for v in col_c], 'rows': [round(v, 2) for v in row_c], 'gridResidualMm': {'median': round(float(np.median(resid)), 3), 'max': round(float(np.max(resid)), 3)}}
    print(f"keys: {len(keys)}, on a grid of {gpx:.1f} by {gpy:.1f} mm; columns {report['keys']['columns']}, rows {report['keys']['rows']}; a key's legend is {report['keys']['gridResidualMm']['median']} mm from its row and column at the median, {report['keys']['gridResidualMm']['max']} at most")

    # The name: the white letters of "KIM-1" on its dark plate, top right.
    a, b = px(150, 30)
    c, d = px(195, 47)
    letters = ((L > 0.7) & (C < 0.06))[b:d, a:c]
    n, labels, stats, _ = cv2.connectedComponentsWithStats(letters.astype(np.uint8), connectivity=8)
    letters = np.isin(labels, [k for k in range(1, n) if stats[k, cv2.CC_STAT_AREA] > 2 * R * R])
    rr, cc = np.nonzero(letters)
    x0, y0 = mm(a + cc.min(), b + rr.min())
    x1, y1 = mm(a + cc.max() + 1, b + rr.max() + 1)
    report['name'] = [round(x0, 2), round(y0, 2), round(x1, 2), round(y1, 2)]
    print(f"the KIM-1 name's letters: x {x0:.1f} to {x1:.1f}, y {y0:.1f} to {y1:.1f} mm")

    # The loose red wire on this board: its red pixels, ordered along it and
    # thinned to a point every 8 mm. It lies on the board, so its height is
    # not measured: it is drawn 1 mm above the face.
    red = ((C > 0.08) & (np.degrees(np.arctan2(B, A)) % 360 < 45) & (L > 0.3) & (L < 0.7)).astype(np.uint8)
    red = cv2.morphologyEx(red, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, labels, stats, _ = cv2.connectedComponentsWithStats(red, connectivity=8)
    long_ = [k for k in range(1, n) if max(stats[k, cv2.CC_STAT_WIDTH], stats[k, cv2.CC_STAT_HEIGHT]) > 40 * R]
    rr, cc = np.nonzero(np.isin(labels, long_))
    pts = np.stack([cc / R + common.FRAME[0], rr / R + common.FRAME[1]], 1)
    centre = pts.mean(0)
    axis = np.linalg.svd(pts - centre, full_matrices=False)[2][0]
    t = (pts - centre) @ axis
    wire = []
    for lo in np.arange(t.min(), t.max(), 8):
        sel = (t >= lo) & (t < lo + 8)
        if sel.sum() > 20:
            wire.append([round(float(v), 2) for v in np.median(pts[sel], 0)])
    report['wire'] = wire
    print(f"the red wire: {len(pts)} pixels in {len(long_)} pieces, {len(wire)} points from {wire[0]} to {wire[-1]}")

    common.save_json(os.path.join(common.DATA, 'layout.json'), report)
    write_parts(parts, heights, report)


def tabs_from(outline):
    """The two edge-connector tabs, from the outline. The outline is a set of
    line and arc segments in no particular order, so the tabs are found from
    their outer edges: the points furthest left come in pairs, one at each
    end of a tab's straight edge, and each tab runs from the corner round
    above the first to the corner round below the second (every point left of
    the body within 5 mm of them)."""
    out = min(x for x, y in outline)
    ends = sorted(y for x, y in outline if x < out + 1e-6)
    tabs = []
    for a, b in zip(ends[0::2], ends[1::2]):
        near = [y for x, y in outline if x < -0.5 and a - 5 <= y <= b + 5]
        tabs.append({'y0': round(min(near), 2), 'y1': round(max(near), 2), 'out': round(-out, 2)})
    if len(tabs) != 2:
        raise SystemExit(f'expected two edge-connector tabs in the outline, found {len(tabs)}')
    return tabs


def write_parts(parts, heights, report):
    """site/src/models/kim-1-parts.mjs: what the model draws, generated."""
    H = lambda k: heights[k]['height']
    def footprint_size(name):
        import re
        m = re.search(r'L([\d.]+)mm_D([\d.]+)mm', name)
        if m:
            return float(m.group(1)), float(m.group(2))
        m = re.search(r'DIN0207', name)
        if m:
            return 6.3, 2.3
        m = re.search(r'DO-35', name)
        if m:
            return 4.0, 1.9
        m = re.search(r'DO-41', name)
        if m:
            return 5.2, 2.7
        m = re.search(r'Disc_D([\d.]+)mm_W([\d.]+)mm', name)
        if m:
            return float(m.group(1)), float(m.group(2))
        return None

    chips, axial, transistors, other = [], [], [], []
    for p in parts:
        ref, fp = p['ref'], p['footprint']
        a, b, c, d = p['pads']
        if fp.startswith('DIP-'):
            dip = report['dips'][ref]
            pins = dip['pins']
            kind = {'U1': 'ceramic', 'U3': 'ceramic', 'U2': 'socketed'}.get(ref, 'plastic' if pins in (14, 8) or ref in ('U4', 'U24') else 'memory')
            chips.append({'ref': ref, 'value': p['value'], 'x': p['centre'][0], 'y': p['centre'][1], 'pins': pins, 'alongX': dip['alongX'], 'kind': kind})
        elif fp.startswith(('R_', 'C_Axial', 'CP_Axial', 'D_DO')):
            size = footprint_size(fp)
            part = {'ref': ref, 'value': p['value'], 'x0': a, 'y0': b, 'x1': c, 'y1': d, 'length': size[0], 'diameter': size[1], 'kind': 'resistor' if ref.startswith('R') else 'diode' if ref.startswith('CR') else 'capacitor'}
            # A capacitor whose top was triangulated (a mark within 4 mm of its
            # pads' line) takes that as its diameter: it lies on the board.
            for k, h in heights.items():
                if k.startswith('capacitor') and a - 4 <= h['xy'][0] <= c + 4 and b - 4 <= h['xy'][1] <= d + 4:
                    part['diameter'], part['measured'] = h['height'], True
            axial.append(part)
        elif fp.startswith('C_Disc'):
            size = footprint_size(fp)
            other.append({'ref': ref, 'kind': 'disc', 'x': p['centre'][0], 'y': p['centre'][1], 'width': size[0], 'thickness': size[1], 'alongX': (c - a) > (d - b)})
        elif fp == 'TO-92':
            transistors.append({'ref': ref, 'x': p['centre'][0], 'y': p['centre'][1], 'rotation': p['rotation']})
        elif fp.startswith('Piher'):
            other.append({'ref': ref, 'kind': 'trimmer', 'x': p['centre'][0], 'y': p['centre'][1]})
    holes = [p['centre'] for p in parts if p['footprint'].startswith('Mounting_Hole_4.3') and p['ref'] in ('H1', 'H2', 'H3', 'H4')]
    keypad_holes = [p['centre'] for p in parts if p['footprint'].startswith('Mounting_Hole') and p['ref'] not in ('H1', 'H2', 'H3', 'H4')]
    connector = next(p for p in parts if p['ref'] == 'J1')
    tabs = []
    for ref in ('P1', 'P2'):
        p = next(q for q in parts if q['ref'] == ref)
        tabs.append({'ref': ref, 'first': p['pads'][1], 'last': p['pads'][3]})
    outline = json.load(open(os.path.join(common.DATA, 'kicad-parts.json'), encoding='utf8'))['outline']
    m = report['measured']
    keys = report['keys']
    data = {
        'BODY': {'width': common.BODY[0], 'depth': common.BODY[1]},
        'OUTLINE': outline,
        'TABS': tabs_from(outline),
        'NAME': report['name'],
        'WIRE': report['wire'],
        'TAB_CONTACTS': tabs,
        'HOLES': holes,
        'KEYPAD_HOLES': keypad_holes,
        'CHIPS': chips,
        'AXIAL': axial,
        'TRANSISTORS': transistors,
        'OTHER': other,
        'CRYSTAL': {'box': m['can']['anchored'], 'height': H('crystal'), 'leads': [connector and next(p for p in parts if p['ref'] == 'X1')['pads']][0]},
        'DISPLAY_WINDOW': {'box': m['window']['anchored'], 'height': H('display')},
        # The six LED digits: the MAN72A footprints' centres, U18 to U23.
        'DIGITS': [p['centre'] for p in sorted((q for q in parts if q['footprint'].startswith('MAN72A')), key=lambda q: q['centre'][0])],
        'KEYPAD': {
            'bezel': m['bezel']['anchored'], 'bezelHeight': H('keypad-bezel'),
            'board': [heights['keypad-board']['xy'][0], heights['keypad-board']['xy'][1], m['bezel']['anchored'][2], m['bezel']['anchored'][3]], 'boardHeight': H('keypad-board'),
            'keyHeight': H('key'), 'columns': keys['columns'], 'rows': keys['rows'],
            'connector': connector['pads'],
        },
        'HEIGHTS': {
            'ceramic': H('dip40-ceramic'), 'socketed': H('dip40-plastic'),
            'memory': round(float(np.mean([p['height'] for p in json.load(open(os.path.join(common.DATA, 'heights.json'), encoding='utf8'))['points'] if p['part'] == 'dip16'])), 2),
            'plastic': H('dip14'), 'transistor': H('to92'), 'trimmer': H('trimmer'),
        },
    }
    lines = [
        '// GENERATED by tools/kim1-model/layout.py: do not edit by hand. Run it again',
        '// instead (tools/kim1-model/README.md). Millimetres in the board frame: from',
        "// the top left corner of the board's body, x to the right and y down, as the",
        '// component side shows it. Where each figure comes from is in that script',
        '// and in the journal for 2 October 2026.',
        '//',
        "// Positions of parts soldered through the board are their pads' positions in",
        "// Eduardo Casino's KiCad replica of the Rev D board (CC BY-NC 4.0), which",
        '// the photographs are registered to. The keypad, the display window and the',
        '// crystal can are measured on the photographs. HEIGHTS, and the heights in',
        '// CRYSTAL, DISPLAY_WINDOW and KEYPAD, are measured by triangulation between',
        '// two photographs, above the top face, in mm.',
        '',
    ]
    for k, v in data.items():
        lines.append(f'export const {k} = {json.dumps(v, separators=(",", ": "))};')
    out = os.path.join(common.SITE, 'src', 'models', 'kim-1-parts.mjs')
    with open(out, 'w', encoding='utf8') as f:
        f.write('\n'.join(lines) + '\n')
    print(f'wrote {os.path.relpath(out, common.REPO)}: {len(chips)} integrated circuits, {len(axial)} axial parts, {len(transistors)} transistors, {len(other)} others')


if __name__ == '__main__':
    main()
