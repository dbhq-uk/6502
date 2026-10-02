"""What the KIM-1 model's offline analysis scripts share: where things are, the
board frame, colour conversion, and the photographs' provenance.

The board frame is millimetres from the top left corner of the board's body
(not its tabs), as seen from the component side: x to the right, y down. It is
Eduardo Casino's KiCad replica's own frame, moved so that the body's corner is
the origin. The replica's scale comes from its chips' footprints, whose pins
are 0.1 inch apart (its README), and every photograph is registered to it.
"""
import hashlib
import json
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..'))
SITE = os.path.join(REPO, 'site')
PHOTOS = os.path.join(SITE, 'src', 'assets', 'photos')
DATA = os.path.join(HERE, 'data')
OUT = os.path.join(HERE, 'out')

# The KiCad replica's body corner, in its own coordinates: the left edge of
# the body (the line the tabs stand out from) and the top edge.
KICAD_ORIGIN = (53.178825, 57.603954)

# The board's body and tabs in the frame, from the replica's Edge.Cuts.
BODY = (200.0, 273.0)
FRAME = (-12.0, -3.0, 203.0, 276.0)  # what a registered image covers: [x0, y0, x1, y1] in mm


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def sources():
    """The photographs and drawings, from data/sources.json."""
    with open(os.path.join(DATA, 'sources.json'), encoding='utf8') as f:
        return {s['id']: s for s in json.load(f)['sources']}


def open_source(source, path=None):
    """The image for a source: the full-size original when given (and its hash
    must match), else the committed copy, which works at lower resolution.
    Returns (RGB array, scale), where scale is the image's width over the
    original's, so that marked points, which are in the original's pixels,
    can be scaled to it."""
    from PIL import Image
    Image.MAX_IMAGE_PIXELS = None
    if path:
        got = sha256(path)
        if got != source['original']['sha256']:
            raise SystemExit(f"{path}: SHA-256 {got} is not the original's ({source['original']['sha256']})")
    else:
        path = os.path.join(PHOTOS, source['file'])
    img = np.asarray(Image.open(path).convert('RGB'))
    return img, img.shape[1] / source['original']['width']


def oklab(rgb):
    """RGB uint8 (H, W, 3) to OKLab float32 L, a, b."""
    c = rgb.astype(np.float32) / 255
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    r, g, b = c[..., 0], c[..., 1], c[..., 2]
    l = np.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
    m = np.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
    s = np.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)
    L = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s
    A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s
    B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s
    return L, A, B


def frame_size(R):
    x0, y0, x1, y1 = FRAME
    return int(round((x1 - x0) * R)), int(round((y1 - y0) * R))


def to_frame_px(R):
    """A 3x3 matrix from board mm to frame pixels at R pixels per mm."""
    x0, y0, _, _ = FRAME
    return np.array([[R, 0, -x0 * R], [0, R, -y0 * R], [0, 0, 1]], dtype=np.float64)


def kicad_to_board(x, y):
    return x - KICAD_ORIGIN[0], y - KICAD_ORIGIN[1]


def save_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'w', encoding='utf8') as f:
        json.dump(data, f, indent=1, ensure_ascii=False)
        f.write('\n')
