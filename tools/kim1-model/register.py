"""Registers each photograph of the KIM-1 to the board frame, and measures how
well it fits.

    python register.py [--original <id>=<path> ...]

For every source in data/sources.json with a `corners` entry (the board body's
four corners, marked by hand on the original to a few pixels, in the order top
left, top right, bottom right, bottom left as the component side has them):

1. A homography from those corners to the body's true size (200 by 273 mm).
2. Refined by intensity: the photograph is turned into a map of "lighter than
   the mask round it" (the copper, tinned and under green lacquer, is lighter
   than the board on both faces), warped into the frame, and aligned to the
   KiCad replica's copper on the same face with OpenCV's ECC (enhanced
   correlation coefficient) as a full homography, coarse to fine at 2, 4 and
   8 pixels to the millimetre.
3. Measured: the body is cut into 24 mm blocks every 16 mm, and in each block
   with enough copper the leftover shift between the photograph and the
   replica is found with ECC again, as a translation. The spread of those
   shifts is the registration error. Blocks where the two do not correlate
   (cc under 0.6) are skipped: there the Rev D replica and the Rev B board
   differ, or a part hides the board.
4. Corrected: a smooth cubic in x and y is fitted to the shifts (a lens bends
   straight lines a little, and a homography cannot), robustly, and it is
   tested on blocks it was not fitted to: fitted on half the blocks in a
   chequerboard, measured on the other half, then refitted on all of them.

Writes data/registration.json. Without --original the committed copies are
used, at their lower resolution.
"""
import argparse
import os
import sys

import cv2
import numpy as np

import common
import kicad

BLOCK_MM, STEP_MM, R_BLOCK = 24, 16, 8
MIN_CC = 0.6


def feature(rgb):
    """How much lighter than the mask round it each pixel is, 0 to 1."""
    L, _, _ = common.oklab(rgb)
    k = max(3, int(rgb.shape[1] / 300)) | 1
    bg = cv2.morphologyEx(L, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_RECT, (k, k)))
    return np.clip((L - bg) / 0.15, 0, 1).astype(np.float32)


def reference(layer, R):
    """The replica's copper on one face, in the frame at R pixels per mm, 0 to 1
    (data/kicad-copper-<layer>.png is at 8 pixels per mm)."""
    from PIL import Image
    full = np.asarray(Image.open(os.path.join(common.DATA, f'kicad-copper-{layer}.png')).convert('L')).astype(np.float32) / 255
    size = common.frame_size(R)
    return full if full.shape[::-1] == size else cv2.resize(full, size, interpolation=cv2.INTER_AREA)


def poly_terms(x, y):
    x, y = x / 100.0, y / 100.0
    return np.stack([np.ones_like(x), x, y, x * x, x * y, y * y, x ** 3, x * x * y, x * y * y, y ** 3], axis=-1)


def fit_poly(pts, shifts):
    """A robust cubic fit of the shifts: refit twice without points over 3 MADs."""
    keep = np.ones(len(pts), bool)
    for _ in range(3):
        A = poly_terms(pts[keep, 0], pts[keep, 1])
        cx, *_ = np.linalg.lstsq(A, shifts[keep, 0], rcond=None)
        cy, *_ = np.linalg.lstsq(A, shifts[keep, 1], rcond=None)
        r = np.hypot(*(shifts - np.stack([poly_terms(pts[:, 0], pts[:, 1]) @ cx, poly_terms(pts[:, 0], pts[:, 1]) @ cy], -1)).T)
        mad = np.median(np.abs(r - np.median(r[keep]))) + 1e-6
        keep = r < np.median(r[keep]) + 3 * 1.4826 * mad
    return cx, cy, keep


def correction(reg, x, y):
    if not reg.get('poly'):
        return np.zeros_like(x), np.zeros_like(y)
    T = poly_terms(x, y)
    return T @ np.array(reg['poly']['x']), T @ np.array(reg['poly']['y'])


def rectify(reg, img, scale, R, interpolation=cv2.INTER_LINEAR):
    """The photograph in the board frame at R pixels per mm, with the
    registration's homography and its smooth correction applied."""
    w, h = common.frame_size(R)
    x0, y0, _, _ = common.FRAME
    xs = x0 + (np.arange(w) + 0.5) / R
    ys = y0 + (np.arange(h) + 0.5) / R
    X, Y = np.meshgrid(xs, ys)
    dx, dy = correction(reg, X.ravel(), Y.ravel())
    qx, qy = X.ravel() + dx, Y.ravel() + dy
    Hi = np.linalg.inv(np.array(reg['H']))
    P = Hi @ np.stack([qx, qy, np.ones_like(qx)])
    mx = (P[0] / P[2] * scale).reshape(h, w).astype(np.float32)
    my = (P[1] / P[2] * scale).reshape(h, w).astype(np.float32)
    if scale < 1 and img.ndim == 3:
        pass
    return cv2.remap(img, mx, my, interpolation, borderMode=cv2.BORDER_CONSTANT, borderValue=0)


def ecc(ref, moving, motion, warp):
    criteria = (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 200, 1e-6)
    return cv2.findTransformECC(ref, moving, warp, motion, criteria, None, 5)


def register(source, img, scale):
    layer = 'bottom' if source['side'] == 'bottom' else 'top'
    f = feature(img)
    src = np.array(source['corners'], dtype=np.float32) * scale
    dst = np.array([[0, 0], [common.BODY[0], 0], [common.BODY[0], common.BODY[1]], [0, common.BODY[1]]], dtype=np.float32)
    H = cv2.getPerspectiveTransform(src, dst).astype(np.float64)  # image px -> board mm
    steps = []
    for R in (2, 4, 8):
        ref = cv2.GaussianBlur(reference(layer, R), (0, 0), 1.5)
        W = common.to_frame_px(R) @ H
        moving = cv2.GaussianBlur(cv2.warpPerspective(f, W, common.frame_size(R), flags=cv2.INTER_AREA), (0, 0), 1.5)
        cc, E = ecc(ref, moving, cv2.MOTION_HOMOGRAPHY, np.eye(3, dtype=np.float32))
        H = np.linalg.inv(common.to_frame_px(R)) @ np.linalg.inv(E.astype(np.float64)) @ W
        H /= H[2, 2]
        steps.append({'pxPerMm': R, 'cc': round(float(cc), 4)})

    # The leftover shifts, block by block.
    R = R_BLOCK
    ref = cv2.GaussianBlur(reference(layer, R), (0, 0), 1.0)
    moving = cv2.GaussianBlur(cv2.warpPerspective(f, common.to_frame_px(R) @ H, common.frame_size(R), flags=cv2.INTER_AREA), (0, 0), 1.0)
    x0, y0, _, _ = common.FRAME
    pts, shifts = [], []
    b = BLOCK_MM * R
    for by in np.arange(0, common.BODY[1] - BLOCK_MM + 0.01, STEP_MM):
        for bx in np.arange(0, common.BODY[0] - BLOCK_MM + 0.01, STEP_MM):
            px, py = int((bx - x0) * R), int((by - y0) * R)
            rb, mb = ref[py:py + b, px:px + b], moving[py:py + b, px:px + b]
            if rb.std() < 0.15 or mb.std() < 0.05:
                continue
            try:
                cc, E = ecc(rb, mb, cv2.MOTION_TRANSLATION, np.eye(2, 3, dtype=np.float32))
            except cv2.error:
                continue
            t = E[:, 2] / R
            if cc < MIN_CC or np.hypot(*t) > 3:
                continue
            pts.append((bx + BLOCK_MM / 2, by + BLOCK_MM / 2))
            shifts.append(t)
    pts, shifts = np.array(pts), np.array(shifts)
    stats = lambda r: {'median': round(float(np.median(r)), 3), 'p90': round(float(np.percentile(r, 90)), 3), 'max': round(float(r.max()), 3)}
    before = np.hypot(shifts[:, 0], shifts[:, 1])
    # Held out: fit on a chequerboard half, measure on the other half.
    cell = ((pts[:, 0] // STEP_MM) + (pts[:, 1] // STEP_MM)) % 2 == 0
    cx, cy, _ = fit_poly(pts[cell], shifts[cell])
    T = poly_terms(pts[~cell, 0], pts[~cell, 1])
    held = np.hypot(shifts[~cell, 0] - T @ cx, shifts[~cell, 1] - T @ cy)
    cx, cy, keep = fit_poly(pts, shifts)
    T = poly_terms(pts[:, 0], pts[:, 1])
    after = np.hypot(shifts[:, 0] - T @ cx, shifts[:, 1] - T @ cy)
    return {
        'H': H.tolist(),
        'imageScale': scale,
        'ecc': steps,
        'blocks': int(len(pts)),
        'residualMm': {
            'homography': stats(before),
            'corrected': stats(after[keep]),
            'correctedHeldOut': stats(held),
            'outliers': int((~keep).sum()),
        },
        'poly': {'x': cx.tolist(), 'y': cy.tolist()},
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--original', action='append', default=[], help='<id>=<path to the full-size original>')
    ap.add_argument('--only', action='append', default=[])
    args = ap.parse_args()
    originals = dict(a.split('=', 1) for a in args.original)
    out_path = os.path.join(common.DATA, 'registration.json')
    result = {}
    if os.path.exists(out_path):
        import json
        result = json.load(open(out_path, encoding='utf8'))
    for sid, s in common.sources().items():
        if 'corners' not in s or (args.only and sid not in args.only):
            continue
        img, scale = common.open_source(s, originals.get(sid))
        r = register(s, img, scale)
        r['from'] = 'original' if sid in originals else 'committed copy'
        r['imageScale'] = 1.0  # H is stored for the original's pixels
        H = np.array(r['H']) @ np.diag([scale, scale, 1])
        r['H'] = (H / H[2, 2]).tolist()
        result[sid] = r
        rm = r['residualMm']
        print(f"{sid}: ECC cc {', '.join(str(e['cc']) for e in r['ecc'])}; {r['blocks']} blocks; residual after the homography median {rm['homography']['median']} mm, 90th percentile {rm['homography']['p90']}, max {rm['homography']['max']}; after the cubic correction {rm['corrected']['median']}, {rm['corrected']['p90']}, {rm['corrected']['max']} ({rm['outliers']} outliers); held out {rm['correctedHeldOut']['median']}, {rm['correctedHeldOut']['p90']}, {rm['correctedHeldOut']['max']}")
    common.save_json(out_path, result)


if __name__ == '__main__':
    sys.exit(main())
