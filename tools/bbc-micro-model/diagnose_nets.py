"""After task 4's run, looking only: why the known nets check failed.

    python diagnose_nets.py

Reads the copper masks board_trace.py wrote to out/ on its run
(trace-top-copper.png and trace-bottom-copper.png, git-ignored, so run
board_trace.py --map-ppm 8 first), and data/copper.json, registration.json and
ic-table.json. Changes nothing and writes nothing: it rebuilds the nets
check's pieces exactly as board_trace.nets_check does and prints

1. the nets that hold the most GND and +5V pins, from copper.json's pinNets;
2. the shortest chain of pieces from a GND pin to a +5V pin (a piece of one
   face joined to one of the other at a drill or a pin), with each piece's
   area and box in board millimetres and the power pins on it;
3. for each GND or +5V pin outside the largest net, the area of the piece it
   sits on, on each face.

It was written after the check had failed, to describe the failure, and is
no part of the method: nothing here may be used to change board_trace.py
without a new check fixed before it runs.
"""
import collections
import json
import sys

import cv2
import numpy as np

import board_trace as bt
import common


def main():
    from PIL import Image
    R = bt.R
    masks = {}
    for face in ('top', 'bottom'):
        p = common.OUT / f'trace-{face}-copper.png'
        if not p.is_file():
            sys.exit(f'{p} is missing: run board_trace.py --map-ppm 8 first')
        masks[face] = np.asarray(Image.open(p)) > 0
    lab = {f: cv2.connectedComponentsWithStats(m.astype(np.uint8), connectivity=8) for f, m in masks.items()}
    reg, table, copper = bt.load('registration.json'), bt.load('ic-table.json'), bt.load('copper.json')
    fj = bt.load('frame.json')
    x0, y0, _, _ = bt.grid(fj['outline'])
    chips, _ = bt.chip_pins(reg, table, x0, y0)
    r = bt.mm(bt.POINT_R_MM)
    graph = collections.defaultdict(set)

    def node(p):
        a, b = bt._label_at(lab['top'][1], p, r), bt._label_at(lab['bottom'][1], p, r)
        a, b = (('top', a) if a else None), (('bottom', b) if b else None)
        if a and b:
            graph[a].add(b)
            graph[b].add(a)
        return a or b
    for d in reg['drills']:
        node(((d['x'] - x0) * R - 0.5, (d['y'] - y0) * R - 0.5))
    pins = {k: {c['ref']: node(c[k]) for c in chips} for k in ('gnd', 'vcc')}

    def piece(n):
        st = lab[n[0]][2][n[1]]
        return (f'{n[0]} piece {n[1]}: {st[4] / R ** 2:.1f} mm2, x {st[0] / R + x0:.1f} to {(st[0] + st[2]) / R + x0:.1f}, '
                f'y {st[1] / R + y0:.1f} to {(st[1] + st[3]) / R + y0:.1f} mm')

    print('1. the nets holding the most power pins (copper.json pinNets):')
    for k in ('gnd', 'vcc'):
        counts = collections.Counter(n for _, n in copper['pinNets'][k])
        print(f'   {k}: ' + ', '.join(f'{n} holds {c}' for n, c in counts.most_common(3)))
    vcc_at = {n: ref for ref, n in pins['vcc'].items() if n}
    best = None
    for ref, s in pins['gnd'].items():
        if not s:
            continue
        prev, q = {s: None}, collections.deque([s])
        while q:
            u = q.popleft()
            if u in vcc_at:
                path = [u]
                while prev[path[-1]] is not None:
                    path.append(prev[path[-1]])
                if best is None or len(path) < len(best[2]):
                    best = (ref, vcc_at[u], path[::-1])
                break
            for v in graph[u]:
                if v not in prev:
                    prev[v] = u
                    q.append(v)
    print('2. the shortest chain of pieces from a GND pin to a +5V pin:')
    if best is None:
        print('   none: no piece joins the two')
    else:
        print(f'   from {best[0]}\'s GND pin to {best[1]}\'s +5V pin, {len(best[2])} piece(s)')
        for n in best[2]:
            on = [f'{ref} {k}' for k in ('gnd', 'vcc') for ref, m in pins[k].items() if m == n]
            print(f'   {piece(n)}; power pins on it: {", ".join(on) or "none"}')
    print('3. power pins outside the largest net, and the pieces they sit on:')
    biggest = collections.Counter(n for k in ('gnd', 'vcc') for _, n in copper['pinNets'][k]).most_common(1)[0][0]
    at = {c['ref']: c for c in chips}
    for k in ('gnd', 'vcc'):
        for ref, n in copper['pinNets'][k]:
            if n == biggest:
                continue
            p = at[ref][k]
            sizes = []
            for face in ('top', 'bottom'):
                i = bt._label_at(lab[face][1], p, r)
                sizes.append(f'{face} {lab[face][2][i][4] / R ** 2:.1f} mm2' if i else f'{face} none')
            print(f'   {ref} {k}: {", ".join(sizes)}')
    return 0


if __name__ == '__main__':
    sys.exit(main())
