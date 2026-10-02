"""Why the map the model had before scores low even against the photograph it
was traced from (bolo-top): how far it sits from where the registration puts
that photograph's copper, and how much wider it traced the tracks.

    python probe_before.py

Prints, on the body where bolo-top shows the board, at 8 pixels to the
millimetre: the intersection over union of the old map and bolo-top's new
trace as placed, at the best shift within 0.75 mm, and with a pixel's
tolerance either way; and each map's share of copper. Reads out/trace-bolo-top.png
(trace.py) and the old map from git (fuse.py). Writes nothing.
"""
import cv2
import numpy as np

import common
import fuse

R = 8


def main():
    old = fuse.old_map_in_frame(R)
    copper, shows = fuse.load_trace('bolo-top')
    down = lambda m: cv2.resize(m.astype(np.uint8) * 255, common.frame_size(R), interpolation=cv2.INTER_AREA) > 127
    new, where = down(copper & shows), down(shows)
    rs, cs = fuse.body_slice(R)
    body = np.zeros_like(where)
    body[rs, cs] = True
    where &= body
    shifts = []
    for dy in range(-6, 7):
        for dx in range(-6, 7):
            shifts.append((fuse.iou(np.roll(np.roll(old, dy, 0), dx, 1), new, where), dx / R, dy / R))
    best = max(shifts)
    k = np.ones((3, 3), np.uint8)
    tolerant = fuse.iou(cv2.dilate(old.astype(np.uint8), k) > 0, cv2.dilate(new.astype(np.uint8), k) > 0, where)
    print(f'old map against bolo-top\'s new trace: as placed {fuse.iou(old, new, where)}; at its best shift, {best[1]:+.3f} mm in x and {best[2]:+.3f} mm in y, {best[0]}; with a pixel\'s tolerance, {tolerant}')
    print(f'copper share where bolo-top shows the board: old map {(old & where).sum() / where.sum():.3f}, new trace {(new & where).sum() / where.sum():.3f}')


if __name__ == '__main__':
    main()
