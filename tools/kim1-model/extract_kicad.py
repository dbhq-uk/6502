"""Extracts what the analysis uses from Eduardo Casino's KiCad replica of the
KIM-1 (Rev D), into data/:

- kicad-copper-top.png and kicad-copper-bottom.png: each copper layer drawn in
  the board frame (common.FRAME) at 8 pixels to the millimetre, white for
  copper, as the component side sees it (the bottom layer is not mirrored).
- kicad-parts.json: every footprint, with its reference, value, library
  footprint, centre and the box round its pads in the board frame, and its
  rotation.

    python extract_kicad.py <path to kim-1.kicad_pcb>

The replica is CC BY-NC 4.0, and these files are derived from it: see
data/README.md. The file read is pinned by its SHA-256 in data/sources.json.
"""
import os
import sys

import numpy as np
from PIL import Image

import common
import kicad

R = 8


def main(path):
    src = common.sources()['kicad-replica']
    got = common.sha256(path)
    if got != src['original']['sha256']:
        raise SystemExit(f'{path}: SHA-256 {got}, not the pinned {src["original"]["sha256"]}')
    board = kicad.load(path)
    M = common.to_frame_px(R)

    def T(x, y):
        bx, by = common.kicad_to_board(x, y)
        return tuple((M @ np.array([bx, by, 1.0]))[:2])

    size = common.frame_size(R)
    for layer, name in (('F.Cu', 'top'), ('B.Cu', 'bottom')):
        img = kicad.draw_copper(board, layer, T, size, R)
        img.convert('1').save(os.path.join(common.DATA, f'kicad-copper-{name}.png'), optimize=True)
        print(f'kicad-copper-{name}.png: {size[0]} x {size[1]}, copper {np.asarray(img).mean() / 2.55:.1f}% of the frame')
    outline = [common.kicad_to_board(*p) for p in kicad.outline(board)]
    parts = []
    for fp in board['footprints']:
        if not fp['ref'] or fp['ref'].startswith('Ref') or not fp['pads']:
            continue
        pads = np.array([common.kicad_to_board(*p['at']) for p in fp['pads']])
        lo, hi = pads.min(0), pads.max(0)
        parts.append({
            'ref': fp['ref'], 'value': fp['value'], 'footprint': fp['lib'].split(':', 1)[1],
            'centre': [round(float(v), 3) for v in (lo + hi) / 2],
            'pads': [round(float(v), 3) for v in (*lo, *hi)],
            'origin': [round(v, 3) for v in common.kicad_to_board(*fp['at'])],
            'rotation': fp['rot'], 'padCount': len(fp['pads']),
        })
    parts.sort(key=lambda p: (p['ref'].rstrip('0123456789'), int(''.join(c for c in p['ref'] if c.isdigit()) or 0)))
    common.save_json(os.path.join(common.DATA, 'kicad-parts.json'), {
        'about': 'Footprints of Eduardo Casino\'s KiCad replica of the KIM-1 Rev D board, in the board frame (mm from the top left corner of the body, component side). CC BY-NC 4.0, see README.md here. Made by extract_kicad.py.',
        'outline': [[round(x, 3), round(y, 3)] for x, y in outline],
        'parts': parts,
    })
    print(f'kicad-parts.json: {len(parts)} footprints')


if __name__ == '__main__':
    main(sys.argv[1])
