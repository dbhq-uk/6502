"""The solder side registered, and the pads, drills and footprints
(board_register.py), on made-up scans of a made-up board.

    /tmp/bbcvenv/bin/python -m pytest tools/bbc-micro-model/tests -q

The registration test draws one set of holes twice: once as the component
side is scanned, and once as the solder side is, which is the board turned
over (so mirrored), turned on the glass, scaled a little differently and
warped by a mild cubic, with 2 per cent of the holes missing. The holes are
tinned pads (grey, grainy discs) and open vias (a bright ring round a dark
middle). The grouping test draws DIP footprints on the 2.54 mm pitch, with
pin 1 marked either by a square pad or by the print's chamfered corner, a
connector row and some loose pads, and the print's outlines between each
DIP's rows.
"""
import math
import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import board_frame  # noqa: E402
import board_register  # noqa: E402
import common  # noqa: E402

PITCH = 2.54
GREEN, PAD, RING, PRINT, LID = (25, 90, 55), (205, 205, 203), (160, 200, 175), (225, 200, 40), (228, 230, 229)
SX, SY = 15.75, 15.72


def linear(c):
    c = np.array(c, np.float32) / 255
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def render(size, layers, seed=7):
    """Layers are (label image at 4x, colour, grain); later layers cover
    earlier ones. Mixed in linear light and averaged down, as a scanner's blur
    mixes it, then stored as sRGB with a little noise."""
    import cv2
    W, H = size
    lin = np.zeros((H, W, 3), np.float32) + linear(GREEN)
    rng = np.random.default_rng(seed)
    grain_all = np.zeros((H, W), np.float32)
    for big, colour, grain in layers:
        cover = cv2.resize(big.astype(np.float32), (W, H), interpolation=cv2.INTER_AREA)
        lin = lin * (1 - cover[..., None]) + cover[..., None] * linear(colour)
        if grain:
            grain_all += cover * rng.normal(0, grain, (H, W)).astype(np.float32)
    rgb = 255 * np.where(lin <= 0.0031308, 12.92 * lin, 1.055 * np.power(lin, 1 / 2.4) - 0.055)
    rgb += rng.normal(0, 2.0, rgb.shape) + grain_all[..., None]
    return np.clip(np.round(rgb), 0, 255).astype(np.uint8)


class Canvas:
    """Draws at 4x in cv2's fixed point, given a board-millimetre to pixel map."""

    UP, FRAC = 4, 16

    def __init__(self, size, to_px, scale):
        self.size, self.to_px, self.scale = size, to_px, scale
        self.layers = {}

    def layer(self, name):
        if name not in self.layers:
            self.layers[name] = np.zeros((self.size[1] * self.UP, self.size[0] * self.UP), np.uint8)
        return self.layers[name]

    def sub(self, p):
        return np.round(((np.asarray(p, float) + 0.5) * self.UP - 0.5) * self.FRAC).astype(np.int32)

    def disc(self, name, centre_mm, d_mm, value=1):
        import cv2
        c = self.to_px([centre_mm])[0]
        r = int(round(d_mm / 2 * self.scale * self.UP * self.FRAC))
        cv2.circle(self.layer(name), tuple(int(v) for v in self.sub(c)), r, value, -1, cv2.LINE_8, 4)

    def poly(self, name, pts_mm, value=1):
        import cv2
        p = self.sub(self.to_px(pts_mm)).reshape(-1, 1, 2)
        cv2.fillPoly(self.layer(name), [p], value, cv2.LINE_8, 4)

    def line(self, name, a_mm, b_mm, w_mm):
        import cv2
        a, b = self.sub(self.to_px([a_mm, b_mm]))
        cv2.line(self.layer(name), tuple(int(v) for v in a), tuple(int(v) for v in b), 1,
                 max(1, int(round(w_mm * self.scale * self.UP))), cv2.LINE_8, 4)

    def render(self, order, seed=7):
        grain = {'pad': 25.0}
        colour = {'pad': PAD, 'ring': RING, 'hole': GREEN, 'print': PRINT, 'lid': LID}
        return render(self.size, [(self.layers[n], colour[n], grain.get(n, 0)) for n in order if n in self.layers], seed)


# --- the registration ---------------------------------------------------------------

THETA1 = math.radians(0.25)
ORIGIN1 = (60.0, 45.0)
BOARD = (110.0, 75.0)
SIZE = (1860, 1270)


def frame1():
    return board_frame.Frame(SX, SY, THETA1, ORIGIN1)


def to_i2_raw(mm):
    """The solder side as scanned: the board turned over, so x runs the other
    way, turned 0.7 degrees on the glass, scaled 0.3 per cent larger, offset,
    and warped by a mild cubic (up to about 0.3 mm)."""
    mm = np.asarray(mm, float).reshape(-1, 2)
    u = (mm[:, 0] - 55) / 55
    v = (mm[:, 1] - 37) / 37
    w = np.c_[mm[:, 0] + 0.30 * u ** 3 + 0.08 * u * v * v, mm[:, 1] + 0.20 * v ** 3 - 0.10 * u * u * v]
    t = math.radians(-0.7)
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    p = (w @ R.T) * np.array([SX * 1.003, SY * 1.003]) + np.array([70.0, 40.0])
    p[:, 0] = SIZE[0] - 1 - p[:, 0]          # turned over: mirrored left to right
    return p


def to_i2_mirrored(mm):
    p = to_i2_raw(mm)
    p[:, 0] = SIZE[0] - 1 - p[:, 0]
    return p


def hole_set():
    """Holes on the 2.54 mm grid, about a quarter of the points taken, and a
    few vias off the grid."""
    rng = np.random.default_rng(3)
    xs = np.arange(5.0, BOARD[0] - 4, PITCH)
    ys = np.arange(5.0, BOARD[1] - 4, PITCH)
    pts = [(x, y) for x in xs for y in ys if rng.random() < 0.25]
    vias = [(x + PITCH / 2, y + PITCH / 2) for x, y in pts[::9]]
    return np.array(pts), np.array(vias)


def draw_face(to_px, pads, vias, keep, seed):
    c = Canvas(SIZE, to_px, (SX + SY) / 2)
    for p, k in zip(pads, keep[:len(pads)]):
        if k:
            c.disc('pad', p, 1.7)
    for p, k in zip(vias, keep[len(pads):]):
        if k:
            c.disc('ring', p, 1.05)
            c.disc('hole', p, 0.5)
    return c.render(['ring', 'hole', 'pad'], seed)


@pytest.fixture(scope='module')
def registered():
    pads, vias = hole_set()
    n = len(pads) + len(vias)
    rng = np.random.default_rng(5)
    keep2 = rng.random(n) >= 0.02
    keep2[:4] = True
    i1 = draw_face(frame1().to_px, pads, vias, np.ones(n, bool), 11)
    raw = draw_face(to_i2_raw, pads, vias, keep2, 13)
    i2m = np.ascontiguousarray(raw[:, ::-1])
    # four seeds far apart, marked by hand a pixel or so off
    corners = [int(np.argmin(pads[:, 0] + pads[:, 1])), int(np.argmax(pads[:, 0] - pads[:, 1])),
               int(np.argmin(pads[:, 0] - pads[:, 1])), int(np.argmax(pads[:, 0] + pads[:, 1]))]
    seeds = [{'I1': (frame1().to_px([pads[i]])[0] + 0.8).tolist(), 'I2mirrored': (to_i2_mirrored([pads[i]])[0] - 0.7).tolist()}
             for i in corners]
    result = board_register.register_scans(i1, i2m, seeds, frame1())
    return result, pads, vias, keep2


def test_the_hole_finders_find_the_holes(registered):
    result, pads, vias, _ = registered
    assert result['found']['I1']['solder'] == len(pads)
    assert result['found']['I1']['rings'] == len(vias)


def test_the_solder_side_comes_back_to_under_five_hundredths_held_out(registered):
    result, pads, vias, keep2 = registered
    n = int(keep2.sum())
    assert result['holes'] >= 0.98 * n, (result['holes'], n)
    assert result['model'] == 'cubic', result['fits']
    assert result['heldOutMm']['max'] < 0.05, result['heldOutMm']
    # the affine cannot follow the warp, held out
    assert result['fits']['affine']['heldOutMm']['median'] > result['fits']['cubic']['heldOutMm']['median']


def test_every_hole_lands_on_its_place(registered):
    result, pads, vias, _ = registered
    truth = np.vstack([pads, vias])
    got = np.array([[h['bottom'][0], h['bottom'][1]] for h in result['pairs']])
    d = np.hypot(*(got[:, None, :] - truth[None, :, :]).transpose(2, 0, 1)).min(1)
    assert d.max() < 0.05


def test_the_seeds_name_the_mirror():
    """Seeds given without mirroring the solder side would need a reflection,
    which no affine fitted to them can be without a negative determinant."""
    src = np.array([[0, 0], [10, 0], [0, 10], [10, 10]], float)
    dst = src * np.array([-1, 1]) + 50
    assert board_register.seed_handedness(src, dst) < 0
    assert board_register.seed_handedness(src, src + 3) > 0


# --- the roundness rule ---------------------------------------------------------------

def test_the_roundness_rule_never_looks_at_a_residual():
    top = [{'axis': 1.0, 'area': 500}, {'axis': 1.3, 'area': 500}, {'axis': 1.1, 'area': 250},
           {'axis': 1.1, 'area': 900}, {'axis': 1.0, 'area': 500}, {'axis': 1.0, 'area': 520}]
    bottom = [{'axis': 1.0, 'area': 400}, {'axis': 1.0, 'area': 400}, {'axis': 1.0, 'area': 400},
              {'axis': 1.0, 'area': 400}, {'axis': 1.26, 'area': 400}, {'axis': 1.0, 'area': 100}]
    out = board_register.roundness_exclusions(top, bottom)
    assert [e['index'] for e in out] == [1, 2, 3, 4, 5]
    assert out[0]['why'] == ['top: axis ratio 1.30 over 1.25']
    assert out[1]['why'][0].startswith('top: area 0.50')
    assert out[3]['why'] == ['bottom: axis ratio 1.26 over 1.25']
    assert out[4]['why'][0].startswith('bottom: area 0.25')
    # it takes no residuals: the same blobs give the same list whatever the fit did
    assert 'residual' not in board_register.roundness_exclusions.__code__.co_varnames


def test_second_moments_axis_ratio():
    import cv2
    m = np.zeros((200, 200), np.uint8)
    cv2.ellipse(m, (100, 100), (40, 20), 30, 0, 360, 1, -1)
    ys, xs = np.nonzero(m)
    assert board_register.axis_ratio(xs, ys, 1.0, 1.0) == pytest.approx(2.0, rel=0.02)
    m[:] = 0
    cv2.circle(m, (100, 100), 30, 1, -1)
    ys, xs = np.nonzero(m)
    assert board_register.axis_ratio(xs, ys, 1.0, 1.0) == pytest.approx(1.0, abs=0.02)


# --- the outer rim of a hole ----------------------------------------------------------

def test_a_hole_is_fitted_on_its_top_rim_not_the_lid_seen_through_it():
    """A 3.2 mm hole whose far wall shows as a grey crescent on its rear
    side: the lid is seen only where the near rim and the far rim, shifted
    0.35 mm towards the front, overlap. The fit finds the near rim."""
    S = 15.75
    size = (200, 200)
    centre = (6.0, 6.0)

    def to_px(mm):
        return np.asarray(mm, float).reshape(-1, 2) * S
    c = Canvas(size, to_px, S)
    c.disc('lid', centre, 3.2)                      # the near rim: the wall's colour first
    lid_only = Canvas(size, to_px, S)
    lid_only.disc('lid', centre, 3.2)
    shifted = Canvas(size, to_px, S)
    shifted.disc('lid', (centre[0], centre[1] + 0.35), 3.2)
    wall = c.layers['lid'].copy()
    lid = lid_only.layers['lid'] & shifted.layers['lid']
    # the wall grey and lighter than half way from the lacquer to the lid, as
    # on I1 (L about 0.75, the lid 0.82, the lacquer 0.38)
    rgb = render(size, [(wall, (175, 182, 175), 0), (lid, LID, 0)])
    got = board_register.fit_top_rim(rgb, (centre[0] * S + 2, centre[1] * S + 6), 1.7 * S, S, S)
    assert got is not None
    assert math.hypot(got['x'] / S - centre[0], got['y'] / S - centre[1]) < 0.05
    assert got['d'] / S == pytest.approx(3.2, abs=0.08)


# --- pads into footprints ----------------------------------------------------------

DIPS = [
    # id, pins, first pin-1 pad (mm), axis ('y' runs down, 'x' runs across), row spacing, rotated 180, pin 1 mark
    ('A', 14, (10.0, 10.0), 'y', 7.62, False, 'square'),
    ('B', 16, (30.0, 10.0), 'y', 7.62, True, 'square'),
    ('C', 28, (50.0, 10.0), 'y', 15.24, False, 'print'),
    ('D', 40, (80.0, 8.0), 'y', 15.24, False, 'square'),
    ('E', 16, (10.0, 70.0), 'x', 7.62, False, 'square'),
    # two narrow DIPs side by side, 7.62 mm between their facing rows: only
    # the print says which rows pair
    ('F', 16, (110.0, 10.0), 'y', 7.62, False, 'square'),
    ('G', 16, (125.24, 10.0), 'y', 7.62, False, 'print'),
    # its last pin bridged by solder to a blob 3.5 mm long: no pad of its own
    ('H', 14, (30.0, 40.0), 'y', 7.62, False, 'square'),
]


# One pad of D is not drawn (pin 7, in its first row): the row must stay one
# row and the pin be inferred at its place.
MISSING = ('D', 6)
# H's pin 8 (the first pin of its second row, at the far end from pin 1) is
# under a long blob of solder: found nowhere as a pad, but covered.
BRIDGED = ('H', 7)


def dip_pins(pins, first, axis, spacing, rotated):
    """Pin centres 1..N, anticlockwise from above: down the first row, back up
    the second. A DIP down the board with pin 1 at its top left; across the
    board with pin 1 at its bottom left. Rotated 180 degrees about its middle."""
    n = pins // 2
    x0, y0 = first
    out = []
    for k in range(pins):
        if axis == 'y':
            p = (x0, y0 + k * PITCH) if k < n else (x0 + spacing, y0 + (pins - 1 - k) * PITCH)
        else:
            p = (x0 + k * PITCH, y0) if k < n else (x0 + (pins - 1 - k) * PITCH, y0 - spacing)
        out.append(p)
    out = np.array(out)
    if rotated:
        mid = (out.min(0) + out.max(0)) / 2
        out = 2 * mid - out
    return out


@pytest.fixture(scope='module')
def grouped():
    S = 15.75
    size = (2400, 1500)

    def to_px(mm):
        return np.asarray(mm, float).reshape(-1, 2) * S + 30
    c = Canvas(size, to_px, S)
    truth = {}
    for fid, pins, first, axis, spacing, rotated, mark in DIPS:
        p = dip_pins(pins, first, axis, spacing, rotated)
        truth[fid] = (pins, p[0])
        for k, q in enumerate(p):
            if (fid, k) == MISSING:
                continue
            if (fid, k) == BRIDGED:
                c.poly('pad', [(q[0] - 0.85, q[1] - 0.85), (q[0] + 0.85, q[1] - 0.85), (q[0] + 0.85, q[1] + 2.65), (q[0] - 0.85, q[1] + 2.65)])
                continue
            if k == 0 and mark == 'square':
                h = 0.85
                c.poly('pad', [(q[0] - h, q[1] - h), (q[0] + h, q[1] - h), (q[0] + h, q[1] + h), (q[0] - h, q[1] + h)])
            else:
                c.disc('pad', q, 1.7)
        # the print's outline, 1.1 mm inside each row and 1.5 mm past the end pads,
        # with a chamfer at pin 1's corner when the print marks it
        lo, hi = p.min(0), p.max(0)
        if axis == 'y':
            box = [(lo[0] + 1.1, lo[1] - 1.5), (hi[0] - 1.1, lo[1] - 1.5), (hi[0] - 1.1, hi[1] + 1.5), (lo[0] + 1.1, hi[1] + 1.5)]
        else:
            box = [(lo[0] - 1.5, lo[1] + 1.1), (hi[0] + 1.5, lo[1] + 1.1), (hi[0] + 1.5, hi[1] - 1.1), (lo[0] - 1.5, hi[1] - 1.1)]
        for a, b in zip(box, box[1:] + box[:1]):
            c.line('print', a, b, 0.3)
        if mark == 'print':
            q = p[0]
            inward = np.sign(((lo + hi) / 2) - q)
            if axis == 'y':
                a = (q[0] + inward[0] * 1.1, q[1] + inward[1] * 1.5)
                b = (q[0] + inward[0] * 3.0, q[1] - inward[1] * 1.5)
            else:
                a = (q[0] + inward[0] * 1.5, q[1] + inward[1] * 1.1)
                b = (q[0] - inward[0] * 1.5, q[1] + inward[1] * 3.0)
            c.line('print', a, b, 0.3)
    # a connector row of ten, and a few loose pads
    for k in range(10):
        c.disc('pad', (10.0 + k * PITCH, 85.0), 1.7)
    for q in ((60.0, 75.0), (70.16, 75.0), (100.0, 80.0)):
        c.disc('pad', q, 1.7)
    rgb = c.render(['print', 'pad'])
    pads = board_register.pads_of_one_face(rgb, S, S, lambda px: (np.asarray(px, float).reshape(-1, 2) - 30) / S)
    fps = board_register.group_footprints(pads, lambda mm: to_px(mm), rgb)
    return fps, pads, truth


def test_each_dip_is_found_with_its_pin_count(grouped):
    fps, pads, truth = grouped
    dips = [f for f in fps if f['kind'] == 'dip']
    assert len(dips) == len(truth), [(f['pins'], f['pin1']) for f in dips]
    for fid, (pins, pin1) in truth.items():
        best = min(dips, key=lambda f: math.hypot(f['pin1'][0] - pin1[0], f['pin1'][1] - pin1[1]))
        assert best['pins'] == pins, (fid, best)
        assert math.hypot(best['pin1'][0] - pin1[0], best['pin1'][1] - pin1[1]) < 0.2, (fid, best['pin1'], pin1)
        assert best['pin1From'] in ('square pad', 'print'), best


def test_pin_one_comes_from_the_square_pad_or_the_print(grouped):
    fps, pads, truth = grouped
    by = {fid: min((f for f in fps if f['kind'] == 'dip'), key=lambda f: math.hypot(f['pin1'][0] - p[0], f['pin1'][1] - p[1]))
          for fid, (_, p) in truth.items()}
    for fid, pins, first, axis, spacing, rotated, mark in DIPS:
        assert by[fid]['pin1From'] == ('square pad' if mark == 'square' else 'print'), (fid, by[fid])


def test_the_connector_row_is_not_a_dip(grouped):
    fps, _, _ = grouped
    rows = [f for f in fps if f['kind'] in ('connector', 'sip')]
    assert any(f['pins'] == 10 for f in rows), [(f['kind'], f['pins']) for f in fps]


def test_a_footprint_lists_its_pads_pin_by_pin(grouped):
    fps, pads, _ = grouped
    for f in fps:
        if f['kind'] != 'dip':
            continue
        assert len(f['pads']) == f['pins']
        first = pads[f['pads'][0]]
        assert math.hypot(first['x'] - f['pin1'][0], first['y'] - f['pin1'][1]) < 1e-6
        # pin N/2 is (N/2 - 1) x 2.54 mm from pin 1 along the row
        half = pads[f['pads'][f['pins'] // 2 - 1]]
        assert math.hypot(half['x'] - first['x'], half['y'] - first['y']) == pytest.approx((f['pins'] // 2 - 1) * PITCH, abs=0.15)


def test_the_square_pad_measure():
    """A square pad fills its smallest bounding rectangle; a disc fills pi / 4."""
    import cv2
    m = np.zeros((100, 100), np.uint8)
    cv2.rectangle(m, (30, 30), (56, 56), 1, -1)
    assert board_register.squareness(m) > 0.95
    m[:] = 0
    cv2.circle(m, (50, 50), 14, 1, -1)
    assert board_register.squareness(m) < 0.85


def test_the_hole_finder_is_task_zeros(registered):
    """The solder holes are common.find_pads' blobs, unchanged from task 0."""
    pads, vias = hole_set()
    i1 = draw_face(frame1().to_px, pads, vias, np.ones(len(pads) + len(vias), bool), 11)
    mine = board_register.pads_of_one_face(i1, SX, SY, frame1().to_board)
    holes = np.array([[p['px'][0], p['px'][1]] for p in mine if p['kind'] == 'solder' and p['hole']])
    theirs = common.find_pads(i1)
    assert len(holes) == len(theirs)
    assert np.abs(np.sort(holes, 0) - np.sort(theirs, 0)).max() < 1e-9


def test_a_dip_with_a_pad_not_found_keeps_its_pin_count(grouped):
    fps, pads, truth = grouped
    fid, k = MISSING
    pins, first, axis, spacing, rotated = next((d[1], d[2], d[3], d[4], d[5]) for d in DIPS if d[0] == fid)
    want = dip_pins(pins, first, axis, spacing, rotated)
    f = min((f for f in fps if f['kind'] == 'dip'), key=lambda f: math.hypot(f['pin1'][0] - want[0][0], f['pin1'][1] - want[0][1]))
    assert f['pins'] == pins
    assert f['padsInferred'] == 1
    q = pads[f['pads'][k]]
    assert q['source'] == 'inferred'
    assert math.hypot(q['x'] - want[k][0], q['y'] - want[k][1]) < 0.1


# --- what is not an open hole --------------------------------------------------------

def _one_face(draw):
    S = 15.75
    size = (300, 200)

    def to_px(mm):
        return np.asarray(mm, float).reshape(-1, 2) * S
    c = Canvas(size, to_px, S)
    order = draw(c)
    return board_register.pads_of_one_face(c.render(order), S, S, lambda px: np.asarray(px, float).reshape(-1, 2) / S)


def test_a_speck_in_a_solder_pad_is_one_hole_not_two():
    """A tinned pad with a dark speck in its middle: light grey solder round a
    dark centre is ring-shaped, but it is the pad's hole, found once."""
    def draw(c):
        c.disc('pad', (6.0, 6.0), 1.8)
        c.disc('hole', (6.0, 6.0), 0.45)
        c.disc('ring', (12.0, 6.0), 1.05)          # a real via, well clear of it
        c.disc('hole', (12.0, 6.0), 0.5)
        return ['pad', 'ring', 'hole']
    pads = _one_face(draw)
    assert [p['kind'] for p in pads if p['hole']].count('ring') == 1
    ring = next(p for p in pads if p['kind'] == 'ring')
    assert math.hypot(ring['x'] - 12.0, ring['y'] - 6.0) < 0.05


def test_a_letter_in_the_print_is_not_a_hole():
    """An O in the yellow print is a light ring round a dark middle too."""
    def draw(c):
        c.disc('print', (6.0, 6.0), 1.05)
        c.disc('hole', (6.0, 6.0), 0.5)
        return ['print', 'hole']
    assert not [p for p in _one_face(draw) if p['kind'] == 'ring']


def test_a_dip_whose_end_pin_is_under_bridged_solder_keeps_its_pin_count(grouped):
    fps, pads, truth = grouped
    pins, pin1 = truth['H']
    f = min((f for f in fps if f['kind'] == 'dip'), key=lambda f: math.hypot(f['pin1'][0] - pin1[0], f['pin1'][1] - pin1[1]))
    assert f['pins'] == pins, f
