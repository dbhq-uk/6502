"""Reads Eduardo Casino's KiCad replica of the KIM-1's Rev D board (a .kicad_pcb
file) into plain Python: the board outline, every copper segment and zone on
both layers, every pad and via, and every footprint with its reference, value,
position and rotation. Then draws a copper layer as a raster.

The replica is not committed here (it is CC BY-NC 4.0 and 6.7 MB). Its source
and the commit read are in site/src/assets/photos/README.md; extract_kicad.py writes
the parts of it the model uses to tools/kim1-model/data/.

Coordinates are KiCad's: millimetres, x to the right, y down, as seen from the
component side.
"""
import math
import re

TOKEN = re.compile(r'\(|\)|"(?:[^"\\]|\\.)*"|[^\s()"]+')


def parse(text):
    """The file as nested lists: an atom is a str, a quoted string keeps no quotes."""
    stack = [[]]
    for m in TOKEN.finditer(text):
        t = m.group(0)
        if t == '(':
            stack.append([])
        elif t == ')':
            done = stack.pop()
            stack[-1].append(done)
        elif t.startswith('"'):
            stack[-1].append(t[1:-1])
        else:
            stack[-1].append(t)
    return stack[0][0]


def child(node, name):
    for c in node[1:]:
        if isinstance(c, list) and c and c[0] == name:
            return c
    return None


def children(node, name):
    return [c for c in node[1:] if isinstance(c, list) and c and c[0] == name]


def xy(node):
    return float(node[1]), float(node[2])


def rotate(x, y, deg):
    # KiCad's rotation is clockwise on screen with y down, which is the
    # mathematical anticlockwise rotation applied with y negated.
    a = math.radians(-deg)
    return x * math.cos(a) - y * math.sin(a), x * math.sin(a) + y * math.cos(a)


def load(path):
    root = parse(open(path, encoding='utf8').read())
    board = {'segments': [], 'arcs': [], 'zones': [], 'pads': [], 'vias': [], 'edges': [], 'footprints': []}
    for n in root[1:]:
        if not isinstance(n, list):
            continue
        kind = n[0]
        if kind == 'segment':
            board['segments'].append({'a': xy(child(n, 'start')), 'b': xy(child(n, 'end')), 'w': float(child(n, 'width')[1]), 'layer': child(n, 'layer')[1]})
        elif kind == 'arc':
            board['arcs'].append({'a': xy(child(n, 'start')), 'm': xy(child(n, 'mid')), 'b': xy(child(n, 'end')), 'w': float(child(n, 'width')[1]), 'layer': child(n, 'layer')[1]})
        elif kind == 'via':
            at = xy(child(n, 'at'))
            board['vias'].append({'at': at, 'size': float(child(n, 'size')[1]), 'drill': float(child(n, 'drill')[1])})
        elif kind == 'zone':
            layers = child(n, 'layer') or child(n, 'layers')
            for fp in children(n, 'filled_polygon'):
                layer = child(fp, 'layer')[1]
                pts = [xy(p) for p in child(fp, 'pts')[1:] if p[0] == 'xy']
                board['zones'].append({'layer': layer, 'pts': pts})
        elif kind in ('gr_line', 'gr_arc') and child(n, 'layer')[1] == 'Edge.Cuts':
            e = {'kind': kind, 'a': xy(child(n, 'start')), 'b': xy(child(n, 'end'))}
            if kind == 'gr_arc':
                e['m'] = xy(child(n, 'mid'))
            board['edges'].append(e)
        elif kind == 'footprint':
            at = child(n, 'at')
            fx, fy = float(at[1]), float(at[2])
            frot = float(at[3]) if len(at) > 3 else 0.0
            ref = value = None
            for p in children(n, 'property'):
                if p[1] == 'Reference':
                    ref = p[2]
                if p[1] == 'Value':
                    value = p[2]
            for t in children(n, 'fp_text'):
                if t[1] == 'reference' and ref is None:
                    ref = t[2]
                if t[1] == 'value' and value is None:
                    value = t[2]
            fp = {'lib': n[1], 'ref': ref, 'value': value, 'at': (fx, fy), 'rot': frot, 'layer': child(n, 'layer')[1], 'pads': []}
            for p in children(n, 'pad'):
                pat = child(p, 'at')
                px, py = float(pat[1]), float(pat[2])
                prot = float(pat[3]) if len(pat) > 3 else 0.0
                dx, dy = rotate(px, py, frot)
                size = child(p, 'size')
                drill = child(p, 'drill')
                layers = child(p, 'layers')[1:]
                pad = {
                    'at': (fx + dx, fy + dy), 'rot': prot, 'shape': p[3], 'kind': p[2],
                    'size': (float(size[1]), float(size[2])),
                    'drill': float(drill[1]) if drill and len(drill) > 1 and drill[1] != 'oval' else (float(drill[2]) if drill and len(drill) > 2 else 0.0),
                    'layers': layers, 'number': p[1],
                }
                fp['pads'].append(pad)
                board['pads'].append({**pad, 'ref': ref})
            board['footprints'].append(fp)
    return board


def arc_points(a, m, b, steps=24):
    """Points along the circular arc through a, m and b."""
    (x1, y1), (x2, y2), (x3, y3) = a, m, b
    d = 2 * (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2))
    if abs(d) < 1e-12:
        return [a, b]
    ux = ((x1 * x1 + y1 * y1) * (y2 - y3) + (x2 * x2 + y2 * y2) * (y3 - y1) + (x3 * x3 + y3 * y3) * (y1 - y2)) / d
    uy = ((x1 * x1 + y1 * y1) * (x3 - x2) + (x2 * x2 + y2 * y2) * (x1 - x3) + (x3 * x3 + y3 * y3) * (x2 - x1)) / d
    r = math.hypot(x1 - ux, y1 - uy)
    t1, t2, t3 = (math.atan2(y - uy, x - ux) for x, y in (a, m, b))

    def between(s, e, t):
        s, e, t = s % (2 * math.pi), e % (2 * math.pi), t % (2 * math.pi)
        return (s <= t <= e) if s <= e else (t >= s or t <= e)

    if between(t1, t3, t2):
        span = (t3 - t1) % (2 * math.pi)
    else:
        span = -((t1 - t3) % (2 * math.pi))
    return [(ux + r * math.cos(t1 + span * i / steps), uy + r * math.sin(t1 + span * i / steps)) for i in range(steps + 1)]


def outline(board):
    """The board outline as one closed polygon, from the Edge.Cuts lines and arcs."""
    pieces = []
    for e in board['edges']:
        pts = arc_points(e['a'], e['m'], e['b']) if e['kind'] == 'gr_arc' else [e['a'], e['b']]
        pieces.append(pts)
    poly = list(pieces.pop(0))
    close = lambda p, q: math.hypot(p[0] - q[0], p[1] - q[1]) < 0.01
    while pieces:
        for i, p in enumerate(pieces):
            if close(p[0], poly[-1]):
                poly += p[1:]
            elif close(p[-1], poly[-1]):
                poly += list(reversed(p))[1:]
            else:
                continue
            pieces.pop(i)
            break
        else:
            raise ValueError('the outline does not close')
    return poly


def draw_copper(board, layer, transform, size, scale):
    """Draws one copper layer ('F.Cu' or 'B.Cu') into a PIL L image of `size`,
    white for copper. `transform(x, y)` maps KiCad mm to pixels; `scale` is
    pixels per mm, for widths. Drill holes are left as copper: in a photograph
    a through-hole is filled with solder."""
    from PIL import Image, ImageDraw
    img = Image.new('L', size, 0)
    g = ImageDraw.Draw(img)

    def stroke(pts, w):
        tp = [transform(*p) for p in pts]
        r = max(0.5, w * scale / 2)
        g.line(tp, fill=255, width=max(1, round(w * scale)))
        for x, y in tp:
            g.ellipse([x - r, y - r, x + r, y + r], fill=255)

    for z in board['zones']:
        if z['layer'] == layer:
            g.polygon([transform(*p) for p in z['pts']], fill=255)
    for s in board['segments']:
        if s['layer'] == layer:
            stroke([s['a'], s['b']], s['w'])
    for a in board['arcs']:
        if a['layer'] == layer:
            stroke(arc_points(a['a'], a['m'], a['b']), a['w'])
    for v in board['vias']:
        x, y = transform(*v['at'])
        r = v['size'] * scale / 2
        g.ellipse([x - r, y - r, x + r, y + r], fill=255)
    for p in board['pads']:
        if not (layer in p['layers'] or '*.Cu' in p['layers']):
            continue
        w, h = p['size']
        cx, cy = p['at']
        total = p['rot']
        if p['shape'] == 'circle' or (p['shape'] == 'oval' and abs(w - h) < 1e-6):
            x, y = transform(cx, cy)
            r = w * scale / 2
            g.ellipse([x - r, y - r, x + r, y + r], fill=255)
            continue
        if p['shape'] == 'oval':
            # A stadium: a line of width min(w, h) with round ends.
            long_, short = max(w, h), min(w, h)
            dx, dy = ((long_ - short) / 2, 0) if w >= h else (0, (long_ - short) / 2)
            ax, ay = rotate(dx, dy, total)
            stroke([(cx - ax, cy - ay), (cx + ax, cy + ay)], short)
            continue
        corners = [rotate(sx * w / 2, sy * h / 2, total) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        g.polygon([transform(cx + dx, cy + dy) for dx, dy in corners], fill=255)
    return img
