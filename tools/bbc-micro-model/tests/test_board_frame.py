"""The board's frame (board_frame.py), on a made-up scan of a made-up board.

    /tmp/bbcvenv/bin/python -m pytest tools/bbc-micro-model/tests -q

The made-up board is drawn four times over size and averaged down, so its
edges are smooth to a fraction of a pixel, with a known x and y scale, a known
turn and a known offset. It has a notch and a slot in its front edge, four
holes, rows of pads on the 2.54 mm pitch across it (two connector rows and a
DIP's row that lies across) and down it (two 40-pin footprints). Its pads are
grainy, as solder is, and its holes show the lid, smooth.
"""
import math
import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import board_frame  # noqa: E402

SX, SY, THETA_DEG, ORIGIN = 15.80, 15.72, 0.35, (70.4, 52.6)
SIZE = (1750, 1260)                     # the made-up scan, pixels
BACKGROUND, BOARD, PAD = (228, 230, 229), (25, 90, 55), (205, 205, 203)
PITCH = 2.54

# The board in its own frame, millimetres from its left rear corner, x to the
# right and y towards the front: a notch 12 mm wide and 6 deep, and a slot 2 mm
# wide and 4 deep, in the front edge.
OUTLINE = [(0, 0), (100, 0), (100, 70), (62, 70), (62, 64), (50, 64), (50, 70),
           (32, 70), (32, 66), (30, 66), (30, 70), (0, 70)]
HOLES = [(4.0, 4.0, 3.2), (95.0, 5.0, 3.2), (45.0, 35.0, 3.4), (95.5, 64.5, 2.5)]
PAD_D = 1.8


def rows_mm():
    """Every row of pads: (id, axis, first pad in mm, pitches, kind, footprint, fold)."""
    rows = [('CONA', 'x', (5.0, 60.0), 16, 'connector', None, None),
            ('CONB', 'x', (52.0, 10.0), 15, 'connector', None, None),
            ('ICX.a', 'x', (64.0, 66.5), 7, 'dip', 'ICX', None)]
    for fp, x0, y0, fold in (('IC1', 10.0, 8.0, 'A'), ('IC2', 70.0, 15.0, 'B')):
        rows.append((f'{fp}.l', 'y', (x0, y0), 19, 'dip', fp, fold))
        rows.append((f'{fp}.r', 'y', (x0 + 15.24, y0), 19, 'dip', fp, fold))
    return rows


def to_px(pts):
    t = math.radians(THETA_DEG)
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    S = np.diag([SX, SY])
    return np.asarray(pts, float).reshape(-1, 2) @ (S @ R).T + np.array(ORIGIN)


def pad_centres():
    out = []
    for _, axis, (x, y), n, *_ in rows_mm():
        for k in range(n + 1):
            out.append((x + k * PITCH, y) if axis == 'x' else (x, y + k * PITCH))
    return out


def draw():
    """The made-up scan: drawn at four times the size and averaged down."""
    import cv2
    up = 4
    W, H = SIZE[0] * up, SIZE[1] * up
    frac = 1 << 4

    def sub(p):          # a pixel position at the large size, in cv2's fixed point
        return np.round(((np.asarray(p) + 0.5) * up - 0.5) * frac).astype(np.int32)

    t = math.radians(THETA_DEG)

    def disc(img, centre_mm, d_mm, value):
        c = to_px([centre_mm])[0]
        axes = (int(round(d_mm / 2 * SX * up * frac)), int(round(d_mm / 2 * SY * up * frac)))
        # an ellipse with axes along the scan's x and y (the scale is the
        # scanner's), so a circle in millimetres; turning a circle changes nothing
        cv2.ellipse(img, tuple(int(v) for v in sub(c)), axes, 0, 0, 360, value, -1, cv2.LINE_8, 4)

    label = np.zeros((H, W), np.uint8)
    cv2.fillPoly(label, [sub(to_px(OUTLINE)).reshape(-1, 1, 2)], 1, cv2.LINE_8, 4)
    for x, y, d in HOLES:
        disc(label, (x, y), d, 0)
    for c in pad_centres():
        disc(label, c, PAD_D, 2)
    del t
    # Mixed in linear light, as a scanner's blur mixes it, then stored as sRGB.
    def linear(c):
        c = np.array(c, np.float32) / 255
        return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    lin = np.zeros((SIZE[1], SIZE[0], 3), np.float32)
    for value, colour in ((0, BACKGROUND), (1, BOARD), (2, PAD)):
        cover = cv2.resize((label == value).astype(np.float32), SIZE, interpolation=cv2.INTER_AREA)
        lin += cover[..., None] * linear(colour)
    rgb = 255 * np.where(lin <= 0.0031308, 12.92 * lin, 1.055 * np.power(lin, 1 / 2.4) - 0.055)
    rng = np.random.default_rng(7)
    rgb += rng.normal(0, 2.0, rgb.shape)
    # Solder is rough where the lid seen through a hole is smooth: the pads
    # get a grain, grey (the same on each channel).
    grain = rng.normal(0, 25.0, rgb.shape[:2])
    pad = cv2.resize((label == 2).astype(np.float32), SIZE, interpolation=cv2.INTER_AREA)
    rgb += (pad * grain)[..., None]
    return np.clip(np.round(rgb), 0, 255).astype(np.uint8)


@pytest.fixture(scope='module')
def scan():
    return draw()


def marked_rows():
    """The rows as a person would mark them: the first and last pad, a few
    pixels off their centres."""
    rng = np.random.default_rng(11)
    out = []
    for rid, axis, first, n, kind, fp, fold in rows_mm():
        last = (first[0] + n * PITCH, first[1]) if axis == 'x' else (first[0], first[1] + n * PITCH)
        a, b = to_px([first, last]) + rng.uniform(-3, 3, (2, 2))
        row = {'id': rid, 'axis': axis, 'first': a.tolist(), 'last': b.tolist(), 'pitches': n, 'kind': kind}
        if fp:
            row['footprint'] = fp
        if fold:
            row['fold'] = fold
            row['pins'] = 40
        out.append(row)
    return out


@pytest.fixture(scope='module')
def measured(scan):
    return board_frame.measure(scan, marked_rows())


def test_the_frame_goes_both_ways():
    f = board_frame.Frame(15.8, 15.7, math.radians(0.4), (12.5, -3.25))
    p = np.array([[0, 0], [100.5, 2000.25], [4000, 3000]], float)
    assert np.abs(f.to_px(f.to_board(p)) - p).max() < 1e-9
    assert np.abs(f.to_board(f.to_px([[0, 0]]))).max() < 1e-12


def test_row_lengths_do_not_depend_on_the_turn():
    d = to_px([(48.26, 0)])[0] - to_px([(0, 0)])[0]
    assert board_frame.length_mm(d, SX, SY) == pytest.approx(48.26, abs=1e-9)
    d = to_px([(0, 48.26)])[0] - to_px([(0, 0)])[0]
    assert board_frame.length_mm(d, SX, SY) == pytest.approx(48.26, abs=1e-9)


def test_scale_from_exact_rows():
    rows = []
    for i, (axis, n) in enumerate((('x', 19), ('x', 9), ('y', 19), ('y', 13), ('x', 7))):
        v = (n * PITCH, 0.0) if axis == 'x' else (0.0, n * PITCH)
        d = to_px([v])[0] - to_px([(0, 0)])[0]
        rows.append({'id': str(i), 'axis': axis, 'd': d, 'trueMm': n * PITCH})
    sx, sy = board_frame.fit_scale(rows)
    assert sx == pytest.approx(SX, rel=1e-9) and sy == pytest.approx(SY, rel=1e-9)


def test_a_held_out_x_row_is_never_in_its_own_fit():
    rows = []
    for i, (axis, n) in enumerate((('x', 19), ('x', 16), ('x', 12), ('x', 9), ('y', 19), ('y', 19), ('y', 13))):
        v = (n * PITCH, 0.0) if axis == 'x' else (0.0, n * PITCH)
        d = to_px([v])[0] - to_px([(0, 0)])[0]
        rows.append({'id': str(i), 'axis': axis, 'd': d, 'trueMm': n * PITCH, 'pitches': n})
    # The row of 12 pitches made 1 mm too long: held out, it reads 1 mm long,
    # scaled to a 48.26 mm row; it is not in the fit that judges it.
    rows[2]['d'] = rows[2]['d'] * (12 * PITCH + 1.0) / (12 * PITCH)
    held = {r['row']: r for r in board_frame.held_out_x(rows)}
    assert held['2']['errMm'] == pytest.approx(1.0, abs=1e-6)
    assert held['2']['scaledErrMm'] == pytest.approx(48.26 / (12 * PITCH), abs=1e-6)
    assert set(held) == {'0', '1', '2', '3'}


def test_the_scale_comes_back(measured):
    result = measured[0]
    assert result['pxPerMm']['x'] == pytest.approx(SX, rel=5e-4)
    assert result['pxPerMm']['y'] == pytest.approx(SY, rel=5e-4)
    assert result['rotationDeg'] == pytest.approx(THETA_DEG, abs=0.01)


def test_the_held_out_rows_are_scored(measured):
    result = measured[0]
    assert result['heldOutX']['scaledErrMm']['max'] < 0.05
    assert {r['row'] for r in result['heldOutX']['rows']} == {'CONA', 'CONB', 'ICX.a'}
    assert result['heldOut']['rowLengthErrMm']['max'] < 0.05
    assert result['heldOut']['rowLengthErrMm']['n'] == 4
    assert result['heldOut']['footprints'] == 2


def test_the_rows_are_straight(measured):
    s = measured[0]['straightness']
    assert s['rmsMm']['max'] < 0.02
    assert {r['row'] for r in s['rows']} == {'CONA', 'CONB', 'IC1.l', 'IC1.r', 'IC2.l', 'IC2.r'}


def test_the_outline_comes_back(measured):
    outline = np.array(measured[0]['outline'], float)
    assert np.allclose(outline[0], outline[-1]), 'the outline is closed'
    got = outline[:-1]
    assert len(got) == len(OUTLINE), got.round(2).tolist()
    for corner in OUTLINE:
        assert np.hypot(*(got - corner).T).min() < 0.1, (corner, got.round(3).tolist())
    board = measured[0]['board']
    assert board['widthMm'] == pytest.approx(100, abs=0.1)
    assert board['depthMm'] == pytest.approx(70, abs=0.1)


def test_the_holes_come_back(measured):
    holes = measured[0]['holes']
    assert len(holes) == len(HOLES), holes
    for x, y, d in HOLES:
        best = min(holes, key=lambda h: math.hypot(h['x'] - x, h['y'] - y))
        assert math.hypot(best['x'] - x, best['y'] - y) < 0.05, (x, y, best)
        assert best['d'] == pytest.approx(d, abs=0.1)


def test_the_origin_is_the_left_rear_corner(measured):
    origin = measured[0]['origin']
    assert np.hypot(origin[0] - ORIGIN[0], origin[1] - ORIGIN[1]) < 0.1 * SX
