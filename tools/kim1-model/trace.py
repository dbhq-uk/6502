"""Traces the copper in each registered photograph, in the board frame.

    python trace.py [--debug]

Reads out/rect-<id>.png (rectify.py) and writes out/trace-<id>.png, an image
whose red channel is copper (255) and whose green channel is where the
photograph shows the board at all (255), so that a part, a label or a wire
that hides the board is told apart from mask with no copper on it.

The method is the same for every photograph, and no threshold is tuned against
the replica: the replica is what the result is measured against.

1. OKLab, a perceptual colour space: L is lightness, C chroma, h hue. Copper,
   tinned and under the board's green lacquer, is lighter than the lacquer
   round it, on both faces, in every photograph here.
2. The lacquer's own lightness round each pixel: in 6 mm blocks every 3 mm,
   the 35th percentile of L where the board shows, smoothed. dL is how much
   lighter a pixel is than that. It cancels light that falls unevenly, and a
   pour is measured against the lacquer, not against itself.
3. Where the board shows: green, or silver and light. Green is chroma over
   0.02 and over 0.06 of L (which leaves out white ceramic and labels with a
   green cast) and a hue from 15 degrees below to 30 above the photograph's
   own median hue (the lacquer's, since it covers most of the board; it is
   about 104 in one photograph and 144 in another, which is why it is not a
   fixed band). Copper sits on the upper side of it, and yellow capacitors and
   cream ones below it. Silver is a tinned pad or joint: light and colourless
   (chroma under 0.07, lighter than 60% of the green), touching green, and
   pad-shaped; light, colourless strokes touching green are the board's white
   print, which hides the board, with 0.3 mm round it.
   Closed over 0.3 mm, so a track's highlight counts, and opened over 0.4 mm,
   so a speck of green on a part does not. Everything else (chips,
   capacitors, resistors, labels, a wire, the keypad, glare that has lost its
   colour) hides the board, and so do the integrated circuits and the LED
   digits wherever the replica's footprints put them, with 0.6 mm for their
   legs (known_occluders), on the top face; nothing stands on the underside.
4. Copper: dL over a threshold chosen by Otsu's method on dL where the board
   shows (the threshold that best splits it in two), with hysteresis: a
   region over 0.6 of the threshold is kept if it holds a pixel over it.
5. Clean up: regions under 0.3 square mm dropped, holes under 0.6 filled,
   and any piece that is neither pad-shaped nor longer than 5 mm dropped:
   aged print such as "Rev. B" passes every colour test, but it comes in
   letters.
"""
import argparse
import json
import os

import cv2
import numpy as np
from PIL import Image

import common

R = 12  # pixels per mm, as rectify.py writes them


def mm(v):
    return max(1, int(round(v * R)))


def otsu(values):
    hist, edges = np.histogram(values, bins=256)
    centres = (edges[:-1] + edges[1:]) / 2
    w0 = np.cumsum(hist)
    w1 = w0[-1] - w0
    m0 = np.cumsum(hist * centres) / np.maximum(w0, 1)
    m1 = (np.sum(hist * centres) - np.cumsum(hist * centres)) / np.maximum(w1, 1)
    between = w0 * w1 * (m0 - m1) ** 2
    return float(centres[np.argmax(between)])


def area_filter(mask, min_px, keep_holes_under=0):
    n, labels, stats, _ = cv2.connectedComponentsWithStats(mask.astype(np.uint8), connectivity=8)
    keep = stats[:, cv2.CC_STAT_AREA] >= min_px
    keep[0] = False
    out = keep[labels]
    if keep_holes_under:
        n, labels, stats, _ = cv2.connectedComponentsWithStats((~out).astype(np.uint8), connectivity=4)
        small = stats[:, cv2.CC_STAT_AREA] < keep_holes_under
        small[0] = False
        out |= small[labels]
    return out


def lacquer_level(L, shows):
    """The lacquer's lightness round each pixel: in 6 mm blocks every 3 mm, the
    35th percentile of L where the board shows (copper covers well under 65%
    of any block, so that is lacquer), smoothed and scaled up. Light that falls
    unevenly across the board cancels out in L minus this."""
    h, w = L.shape
    step, half = mm(3), mm(3)
    gy, gx = np.arange(0, h, step), np.arange(0, w, step)
    grid = np.full((len(gy), len(gx)), np.nan, np.float32)
    for i, y in enumerate(gy):
        for j, x in enumerate(gx):
            s = shows[max(0, y - half):y + half, max(0, x - half):x + half]
            if s.sum() > 0.2 * s.size:
                grid[i, j] = np.percentile(L[max(0, y - half):y + half, max(0, x - half):x + half][s], 35)
    # Fill the blocks with no board in them from their neighbours.
    filled = grid.copy()
    while np.isnan(filled).any():
        nan = np.isnan(filled)
        pad = np.pad(filled, 1, constant_values=np.nan)
        neigh = np.stack([pad[1 + dy:1 + dy + filled.shape[0], 1 + dx:1 + dx + filled.shape[1]] for dy in (-1, 0, 1) for dx in (-1, 0, 1)])
        with np.errstate(all='ignore'):
            import warnings
            warnings.simplefilter('ignore', RuntimeWarning)
            mean = np.nanmean(neigh, axis=0)
        filled[nan] = mean[nan]
    filled = cv2.GaussianBlur(filled, (0, 0), 1.0)
    return cv2.resize(filled, (w, h), interpolation=cv2.INTER_CUBIC)


def known_occluders(shape):
    """The packages that sit in the same place on every board, from the
    replica's footprints: each integrated circuit and LED digit, over its pads
    and 0.6 mm round them (the legs). No photograph can show the board there,
    so whatever a photograph shows there is a part."""
    parts = json.load(open(os.path.join(common.DATA, 'kicad-parts.json'), encoding='utf8'))['parts']
    out = np.zeros(shape, bool)
    x0, y0, _, _ = common.FRAME
    for p in parts:
        if not (p['footprint'].startswith('DIP-') or p['footprint'].startswith('MAN72A')):
            continue
        a, b, c, d = p['pads']
        m = 0.6 if p['footprint'].startswith('DIP-') else 1.5
        out[int((b - m - y0) * R):int((d + m - y0) * R) + 1, int((a - m - x0) * R):int((c + m - x0) * R) + 1] = True
    return out


def trace(rgb, side='top'):
    L, A, B = common.oklab(rgb)
    C = np.hypot(A, B)
    hue = (np.degrees(np.arctan2(B, A)) + 360) % 360
    coloured = (C > 0.03) & (C > 0.06 * L)
    centre = float(np.median(hue[coloured]))
    green = (C > 0.02) & (C > 0.06 * L) & (hue > centre - 15) & (hue < centre + 30)
    # Light and colourless, touching green: a tinned pad or joint, or the
    # board's white print (labels, the name, the logo). A pad is a compact blob
    # (0.3 to 8 square mm, 0.6 to 3.2 mm a side, no more than 1.8 to 1, at
    # least 0.45 of its box filled); print is strokes. Pads show the board
    # (they are copper); print hides it, with 0.3 mm round it, so a letter is
    # never taken for a track. Big colourless areas (white ceramic packages,
    # stickers) are neither, and do not show the board.
    white = (C < 0.07) & (L > np.percentile(L[green], 60)) & ~green
    near = cv2.dilate(green.astype(np.uint8), np.ones((mm(0.3), mm(0.3)), np.uint8)) > 0
    n, labels, stats, _ = cv2.connectedComponentsWithStats(white.astype(np.uint8), connectivity=8)
    touching = np.zeros(n, bool)
    touching[np.unique(labels[white & near])] = True
    touching[0] = False
    w, h, area = stats[:, cv2.CC_STAT_WIDTH] / R, stats[:, cv2.CC_STAT_HEIGHT] / R, stats[:, cv2.CC_STAT_AREA] / R ** 2
    fill = area / np.maximum(w * h, 1e-6)
    pad = touching & (area >= 0.3) & (area <= 8) & (w >= 0.6) & (h >= 0.6) & (w <= 3.2) & (h <= 3.2) & (np.maximum(w, h) <= 1.8 * np.minimum(w, h)) & (fill >= 0.45)
    printed = touching & ~pad & (area >= 0.25) & (area <= 40)
    silver = pad[labels]
    print_mask = cv2.dilate(printed[labels].astype(np.uint8), np.ones((mm(0.6), mm(0.6)), np.uint8)) > 0
    green &= ~print_mask
    shows = (green | silver) & (rgb.sum(-1) > 0)
    shows = cv2.morphologyEx(shows.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((mm(0.3), mm(0.3)), np.uint8))
    shows = (cv2.morphologyEx(shows, cv2.MORPH_OPEN, np.ones((mm(0.4), mm(0.4)), np.uint8)) > 0) 
    shows &= ~print_mask
    if side == 'top':
        shows &= ~known_occluders(shows.shape)
    dL = L - lacquer_level(L, shows)
    t = otsu(np.clip(dL[shows], -0.2, 0.5))
    strong = shows & (dL > t)
    weak = shows & (dL > t * 0.6)
    n, labels = cv2.connectedComponents(weak.astype(np.uint8), connectivity=8)
    anchored = np.zeros(n, bool)
    anchored[np.unique(labels[strong])] = True
    anchored[0] = False
    copper = anchored[labels]
    copper = area_filter(copper, 0.3 * R * R, keep_holes_under=0.6 * R * R) & shows
    # Small pieces that are not pad-shaped are print in the board's own
    # colours (aged labels such as "Rev. B" and "EC-715" pass every colour
    # test): copper is either a pad or part of something longer than 5 mm.
    n, labels, stats, _ = cv2.connectedComponentsWithStats(copper.astype(np.uint8), connectivity=8)
    w, h, area = stats[:, cv2.CC_STAT_WIDTH] / R, stats[:, cv2.CC_STAT_HEIGHT] / R, stats[:, cv2.CC_STAT_AREA] / R ** 2
    fill = area / np.maximum(w * h, 1e-6)
    padlike = (np.maximum(w, h) <= 1.8 * np.minimum(w, h)) & (fill >= 0.45) & (np.maximum(w, h) <= 3.5)
    keep = (np.maximum(w, h) >= 5) | padlike
    keep[0] = False
    copper = keep[labels]
    return copper, shows, {'threshold': round(t, 4), 'hue': [round(centre - 15), round(centre + 30)]}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--only', action='append', default=[])
    args = ap.parse_args()
    for sid, s in common.sources().items():
        path = os.path.join(common.OUT, f'rect-{sid}.png')
        if not os.path.exists(path) or (args.only and sid not in args.only):
            continue
        rgb = np.asarray(Image.open(path).convert('RGB'))
        copper, shows, info = trace(rgb, s['side'])
        out = np.zeros(rgb.shape, np.uint8)
        out[..., 0] = copper * 255
        out[..., 1] = shows * 255
        Image.fromarray(out).save(os.path.join(common.OUT, f'trace-{sid}.png'))
        print(f'{sid}: green is hue {info["hue"][0]} to {info["hue"][1]}; Otsu threshold dL {info["threshold"]}; the board shows over {shows.mean() * 100:.1f}% of the frame, copper {copper.mean() * 100:.1f}%')


if __name__ == '__main__':
    main()
