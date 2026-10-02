"""Writes each registered photograph into the board frame, at R pixels to the
millimetre, as out/rect-<id>.png: the photograph as it would look square on,
with the body's top left corner at common.FRAME's offset. The tracing and the
overlays start from these.

    python rectify.py [--original <id>=<path> ...] [--r 12]
"""
import argparse
import json
import os

import cv2
import numpy as np
from PIL import Image

import common
from register import rectify


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--original', action='append', default=[])
    ap.add_argument('--r', type=float, default=12)
    args = ap.parse_args()
    originals = dict(a.split('=', 1) for a in args.original)
    reg = json.load(open(os.path.join(common.DATA, 'registration.json'), encoding='utf8'))
    os.makedirs(common.OUT, exist_ok=True)
    for sid, r in reg.items():
        s = common.sources()[sid]
        img, scale = common.open_source(s, originals.get(sid))
        # Shrink first when the photograph is much finer than the frame, so the
        # remap does not alias.
        out = rectify(r, img, scale, args.r, cv2.INTER_AREA if scale < 1 else cv2.INTER_LINEAR)
        Image.fromarray(out).save(os.path.join(common.OUT, f'rect-{sid}.png'))
        print(f'out/rect-{sid}.png from the {"original" if sid in originals else "committed copy"}: {out.shape[1]} x {out.shape[0]}')


if __name__ == '__main__':
    main()
