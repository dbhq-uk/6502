"""The solder side registered, and the pads, drills and footprints
(board_register.py), on a made-up pair of scans of a made-up board.

    /tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q

The made-up board is drawn as the NES board's scans show it, at about their
11.81 pixels per millimetre: pads are tinned rings round open holes (task 0's
made-up pad: the hole's wall dark, the scanner's lid grey down the hole),
vias are small open rings or domes of solder, the print is white, and the
edge fingers are 36 tinned strips on each face on a 2.50 mm pitch. Every DIP
lies across, its rows along x, as every DIP on the NES board does: pin 1 is
marked by the print's notch at its left end on some, by a square pad on
others, and one lies the other way round. One DIP has a third row between
its two, as U1 and U4 have, for a narrower chip.

The pair: the component side drawn through a known frame; the solder side
drawn as the same board turned over (so mirrored), turned on the glass,
scaled a little differently and warped by a mild cubic, with 2 per cent of
its holes missing, then flipped left to right as board_register flips I1-back.
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
SX, SY, THETA_DEG, ORIGIN = 11.80, 11.82, 0.20, (40.0, 35.0)
SIZE = (1560, 1060)
BOARD = (120.0, 80.0)

# sRGB colours: the lacquer, a pad's tinned ring, the drill's wall in shadow,
# the lid seen down the drill (task 0's made-up pad), a dome of solder, the
# white print and the tinned edge fingers.
LACQUER, RING, WALL, DRILL_LID, PRINT, FINGER = (
    (12, 32, 16), (98, 100, 94), (40, 40, 40), (75, 75, 73), (205, 210, 205), (110, 112, 108))
DOMES = ((95, 96, 92), (140, 141, 137), (190, 191, 187))     # a dome's rings, its edge to its middle
RING_R, WALL_R, DRILL_LID_R = 0.825, 0.474, 0.347           # mm: task 0's made-up pad
VIA_R, VIA_WALL_R, VIA_LID_R = 0.45, 0.26, 0.18             # a small open via
DOME_RS = (0.55, 0.40, 0.25)

# DIPs: (id, pins, x of the lower row's left pin, y of the upper row, spacing,
# how pin 1 is marked, turned round). Pin 1 is at the lower left, pins run
# right along the lower row and back along the upper; turned round, pin 1 is
# at the upper right.
DIPS = [
    ('A', 40, 8.0, 8.0, 15.24, 'print', False),
    ('B', 16, 65.0, 8.0, 7.62, 'square', False),
    ('D', 14, 90.0, 8.0, 7.62, 'square', True),
    ('E', 20, 60.0, 30.0, 7.62, 'print', False),
    ('C', 24, 8.0, 36.0, 15.24, 'hand', False),
]
# C's third row, between its two, 7.62 mm above its lower row: the narrow
# chip's other row, as on U1 and U4.
C_MIDDLE = True
CONNECTOR = [(60.0 + k * PITCH, 52.0) for k in range(10)]       # a row of ten, not a DIP
NOTCH_DOME = (59.7, 33.81)          # a via in E's notch, as at U7
FINGERS_Y = (70.0, 77.0)
FINGER_X0 = 15.0
FINGER_PITCH = 2.50


def dip_pins(pins, x0, y_upper, spacing, turned):
    """Pin centres 1..N: along the lower row from the left, back along the upper."""
    n = pins // 2
    lower = [(x0 + k * PITCH, y_upper + spacing) for k in range(n)]
    upper = [(x0 + (n - 1 - k) * PITCH, y_upper) for k in range(n)]
    p = np.array(lower + upper)
    if turned:
        mid = (p.min(0) + p.max(0)) / 2
        p = 2 * mid - p
    return p


def vias():
    """Small open rings and domes of solder, off the pitch grid."""
    rng = np.random.default_rng(4)
    pts = []
    while len(pts) < 70:
        q = (rng.uniform(4, BOARD[0] - 4), rng.uniform(4, 66))
        pts.append(q)
    return np.array(pts)


def holes():
    """Every hole: (x, y, kind, square), kind 'ring', 'via' or 'dome'."""
    out = []
    for fid, pins, x0, yu, sp, mark, turned in DIPS:
        for k, p in enumerate(dip_pins(pins, x0, yu, sp, turned)):
            out.append((p[0], p[1], 'ring', mark == 'square' and k == 0))
    fid, pins, x0, yu, sp, _, _ = next(d for d in DIPS if d[0] == 'C')
    for k in range(1, pins // 2):           # its first hole not found, as on U1
        out.append((x0 + k * PITCH, yu + sp - 7.62, 'ring', False))
    out.append(NOTCH_DOME + ('dome', False))
    for q in CONNECTOR:
        out.append((q[0], q[1], 'ring', False))
    pts = vias()
    for i, q in enumerate(pts):
        taken = np.array([(h[0], h[1]) for h in out])
        if np.hypot(*(taken - q).T).min() < 2.0:
            continue
        out.append((q[0], q[1], 'via' if i % 2 else 'dome', False))
    return out


END_SHIFT = 0.55


def fingers():
    """36 strips, the others 1.9 mm wide and the end ones 3.0, widened
    outwards as on the board (task 0: their centres 88.52 mm apart, not
    87.5): (x, width)."""
    return [(FINGER_X0 + k * FINGER_PITCH + (-END_SHIFT if k == 0 else END_SHIFT if k == 35 else 0.0),
             3.0 if k in (0, 35) else 1.9) for k in range(36)]


def frame1():
    return board_frame.Frame(SX, SY, math.radians(THETA_DEG), ORIGIN)


def to_back_raw(mm):
    """The solder side as scanned: the board turned over, so x runs the other
    way, turned 0.7 degrees on the glass, scaled 0.3 per cent larger, offset,
    and warped by a mild cubic (up to about 0.3 mm)."""
    mm = np.asarray(mm, float).reshape(-1, 2)
    u = (mm[:, 0] - 60) / 60
    v = (mm[:, 1] - 40) / 40
    w = np.c_[mm[:, 0] + 0.30 * u ** 3 + 0.08 * u * v * v, mm[:, 1] + 0.20 * v ** 3 - 0.10 * u * u * v]
    t = math.radians(-0.7)
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    p = (w @ R.T) * np.array([SX * 1.003, SY * 1.003]) + np.array([45.0, 30.0])
    p[:, 0] = SIZE[0] - 1 - p[:, 0]
    return p


def to_back_mirrored(mm):
    p = to_back_raw(mm)
    p[:, 0] = SIZE[0] - 1 - p[:, 0]
    return p


def linear(c):
    c = np.array(c, np.float32) / 255
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def draw_face(to_px, hs, keep, front, seed):
    """One face, drawn four times over size in labels and averaged down in
    linear light, as a scanner's blur mixes it, stored as sRGB with noise."""
    import cv2
    up, frac = 4, 1 << 4
    W, H = SIZE[0] * up, SIZE[1] * up
    scale = math.sqrt(SX * SY)

    def sub(p):
        return np.round(((np.asarray(p, float) + 0.5) * up - 0.5) * frac).astype(np.int32)

    def disc(img, c_mm, r_mm, value):
        c = sub(to_px([c_mm])[0])
        cv2.circle(img, (int(c[0]), int(c[1])), int(round(r_mm * scale * up * frac)), value, -1, cv2.LINE_8, 4)

    def poly(img, pts, value):
        cv2.fillPoly(img, [sub(to_px(pts)).reshape(-1, 1, 2)], value, cv2.LINE_8, 4)

    def line(img, a, b, w_mm, value):
        a, b = sub(to_px([a, b]))
        cv2.line(img, (int(a[0]), int(a[1])), (int(b[0]), int(b[1])), value, max(1, int(round(w_mm * scale * up))), cv2.LINE_8, 4)

    RING_, WALL_, DLID_, DOME1_, PRINT_, FINGER_, DOME2_, DOME3_ = 1, 2, 3, 4, 5, 6, 7, 8
    label = np.zeros((H, W), np.uint8)
    if front:
        # the print: each DIP's outline 1.3 mm past its end pins and 1.8 mm
        # inside its rows, with the notch at pin 1's end where the print marks it
        for fid, pins, x0, yu, sp, mark, turned in DIPS:
            p = dip_pins(pins, x0, yu, sp, turned)
            lo, hi = p.min(0), p.max(0)
            box = [(lo[0] - 1.3, lo[1] + 1.8), (hi[0] + 1.3, lo[1] + 1.8), (hi[0] + 1.3, hi[1] - 1.8), (lo[0] - 1.3, hi[1] - 1.8)]
            for a, b in zip(box, box[1:] + box[:1]):
                line(label, a, b, 0.25, PRINT_)
            if mark == 'print':
                cx = lo[0] - 1.3 if not turned else hi[0] + 1.3
                inward = 1.0 if not turned else -1.0
                cy = (lo[1] + hi[1]) / 2
                th = np.radians(np.arange(-90, 91, 5))
                arc = np.c_[cx + inward * 1.1 * np.cos(th), cy + 1.1 * np.sin(th)]
                for a, b in zip(arc[:-1], arc[1:]):
                    line(label, a, b, 0.25, PRINT_)
            if fid == 'C' and C_MIDDLE:
                # the narrow chip's outline between the middle and the lower row
                y0, y1 = hi[1] - 7.62 + 1.8, hi[1] - 1.8
                box = [(lo[0] - 1.3, y0), (hi[0] + 1.3, y0), (hi[0] + 1.3, y1), (lo[0] - 1.3, y1)]
                for a, b in zip(box, box[1:] + box[:1]):
                    line(label, a, b, 0.25, PRINT_)
                th = np.radians(np.arange(-90, 91, 5))       # its notch, at its left end
                arc = np.c_[lo[0] - 1.3 + 1.1 * np.cos(th), (y0 + y1) / 2 + 1.1 * np.sin(th)]
                for a, b in zip(arc[:-1], arc[1:]):
                    line(label, a, b, 0.25, PRINT_)
    for (x, y, kind, square), k in zip(hs, keep):
        if not k:
            continue
        if kind == 'ring':
            if square:
                h = RING_R
                poly(label, [(x - h, y - h), (x + h, y - h), (x + h, y + h), (x - h, y + h)], RING_)
            else:
                disc(label, (x, y), RING_R, RING_)
            disc(label, (x, y), WALL_R, WALL_)
            disc(label, (x, y), DRILL_LID_R, DLID_)
        elif kind == 'via':
            disc(label, (x, y), VIA_R, RING_)
            disc(label, (x, y), VIA_WALL_R, WALL_)
            disc(label, (x, y), VIA_LID_R, DLID_)
        else:
            # a dome of solder is lit brightest in its middle, as on the scans
            for r, v in zip(DOME_RS, (DOME1_, DOME2_, DOME3_)):
                disc(label, (x, y), r, v)
    for x, w in fingers():
        poly(label, [(x - w / 2, FINGERS_Y[0]), (x + w / 2, FINGERS_Y[0]), (x + w / 2, FINGERS_Y[1]), (x - w / 2, FINGERS_Y[1])], FINGER_)
    colours = [LACQUER, RING, WALL, DRILL_LID, DOMES[0], PRINT, FINGER, DOMES[1], DOMES[2]]
    lin = np.zeros((SIZE[1], SIZE[0], 3), np.float32)
    for ch in range(3):
        lut = np.array([linear(c)[ch] for c in colours], np.float32)
        lin[..., ch] = cv2.resize(lut[label], SIZE, interpolation=cv2.INTER_AREA)
    rgb = 255 * np.where(lin <= 0.0031308, 12.92 * lin, 1.055 * np.power(np.maximum(lin, 0), 1 / 2.4) - 0.055)
    rgb += np.random.default_rng(seed).normal(0, 2.0, rgb.shape)
    return np.clip(np.round(rgb), 0, 255).astype(np.uint8)


def marks_for(hs):
    """Hand marks as a person would make them, in the component side's pixels,
    a pixel or so off: four seeds far apart on both faces, and the footprints'
    references; C's pairing (its 600 mil rows) and pin 1, the narrow chip's
    rows as its alternate, and P1's pin 1."""
    f = frame1()
    ring = np.array([(h[0], h[1]) for h in hs if h[2] == 'ring'])
    corners = [int(np.argmin(ring[:, 0] + ring[:, 1])), int(np.argmax(ring[:, 0] - ring[:, 1])),
               int(np.argmin(ring[:, 0] - ring[:, 1])), int(np.argmax(ring[:, 0] + ring[:, 1]))]
    seeds = [{'I1-front': (f.to_px([ring[i]])[0] + 0.8).tolist(), 'I1-back mirrored': (to_back_mirrored([ring[i]])[0] - 0.7).tolist()}
             for i in corners]
    fps = []
    for fid, pins, x0, yu, sp, mark, turned in DIPS:
        p = dip_pins(pins, x0, yu, sp, turned)
        m = {'ref': fid, 'at': f.to_px([(p.min(0) + p.max(0)) / 2])[0].tolist(), 'reason': 'made up'}
        if fid == 'C':
            m['rowSpacingMm'] = 15.24
            m['pin1'] = (f.to_px([p[0]])[0] + 1.0).tolist()
            m['at'] = f.to_px([(x0 + 3 * PITCH, yu + 2.0)])[0].tolist()       # between the upper and the middle row
        fps.append(m)
    _, pins, x0, yu, sp, _, _ = next(d for d in DIPS if d[0] == 'C')
    fps.append({'ref': 'C (300 mil)', 'at': f.to_px([(x0 + 3 * PITCH, yu + sp - 3.81)])[0].tolist(), 'rowSpacingMm': 7.62,
                'alternate': True, 'reason': 'made up'})
    fps.append({'ref': 'P9', 'kind': 'connector', 'rows': [{'first': f.to_px([CONNECTOR[0]])[0].tolist(), 'last': f.to_px([CONNECTOR[-1]])[0].tolist(),
                                                              'pins': len(CONNECTOR)}], 'reason': 'made up'})
    fps.append({'ref': 'P1', 'pin1': f.to_px([(FINGER_X0, sum(FINGERS_Y) / 2)])[0].tolist(), 'at': f.to_px([(FINGER_X0 + 40, 73.0)])[0].tolist(),
                'reason': 'made up'})
    return {'solderSeeds': {'holes': seeds}, 'footprints': {'marks': fps}}


@pytest.fixture(scope='module')
def built():
    hs = holes()
    rng = np.random.default_rng(5)
    keep2 = rng.random(len(hs)) >= 0.02
    front = draw_face(frame1().to_px, hs, np.ones(len(hs), bool), True, 11)
    raw = draw_face(to_back_raw, hs, keep2, False, 13)
    back_m = np.ascontiguousarray(raw[:, ::-1])
    m = marks_for(hs)
    out = board_register.build(front, back_m, m, frame1())
    return out, hs, keep2, front


# --- the registration ------------------------------------------------------------------

def test_the_solder_side_comes_back_to_under_five_hundredths_held_out(built):
    out, hs, keep2, _ = built
    reg = out['reg']
    assert reg['holes'] >= 0.97 * keep2.sum(), (reg['holes'], keep2.sum())
    assert reg['model'] == 'cubic', reg['fits']
    assert reg['heldOutMm']['median'] < 0.05, reg['heldOutMm']
    assert reg['heldOutMm']['p90'] < 0.05, reg['heldOutMm']
    assert reg['heldOutMm']['max'] < 0.08, reg['heldOutMm']
    # the affine cannot follow the warp, held out
    assert reg['fits']['affine']['heldOutMm']['median'] > reg['fits']['cubic']['heldOutMm']['median']


def test_every_kind_of_hole_is_found_and_lands_on_its_place(built):
    out, hs, keep2, _ = built
    reg = out['reg']
    kinds = {k for p in reg['pairs'] for k in p['kinds']}
    assert kinds == {'ring', 'open', 'dome'}, kinds
    truth = np.array([(h[0], h[1]) for h in hs])
    for key in ('top', 'bottom'):
        got = np.array([p[key] for p in reg['pairs']])
        d = np.hypot(*(got[:, None, :] - truth[None, :, :]).transpose(2, 0, 1)).min(1)
        assert d.max() < 0.08, (key, d.max())


def test_the_seeds_name_the_mirror():
    src = np.array([[0, 0], [10, 0], [0, 10], [10, 10]], float)
    assert board_register.seed_handedness(src, src * np.array([-1, 1]) + 50) < 0
    assert board_register.seed_handedness(src, src + 3) > 0


def test_the_model_is_the_lower_held_out_median_and_a_tie_goes_to_the_affine():
    def fits(a, c):
        return {'affine': {'heldOutMm': {'median': a}}, 'cubic': {'heldOutMm': {'median': c}}}
    assert board_register.choose_model(fits(0.100, 0.090)) == 'cubic'
    assert board_register.choose_model(fits(0.100, 0.096)) == 'affine'
    assert board_register.choose_model(fits(0.090, 0.100)) == 'affine'


def test_the_verdict_is_the_plans_solder_row():
    v = board_register.judge
    assert v({'median': 0.20, 'p90': 0.40, 'max': 2.0}, 150) == 'pass'          # the max is not judged
    assert v({'median': 0.20, 'p90': 0.40, 'max': 0.5}, 149) == 'between pass and stop'
    assert v({'median': 0.21, 'p90': 0.30, 'max': 0.5}, 400) == 'between pass and stop'
    assert v({'median': 0.10, 'p90': 0.41, 'max': 0.5}, 400) == 'between pass and stop'
    assert v({'median': 0.31, 'p90': 0.30, 'max': 0.5}, 400) == 'STOP'
    assert v({'median': 0.10, 'p90': 0.61, 'max': 0.5}, 400) == 'STOP'
    assert v({'median': 0.30, 'p90': 0.60, 'max': 9.0}, 400) == 'between pass and stop'


def test_the_ring_finder_is_task_zeros():
    """The ring holes are task 0's drill centres (spike.drill_centres), unchanged."""
    import spike
    hs = [h for h in holes() if h[2] == 'ring'][:40]
    img = draw_face(frame1().to_px, hs, np.ones(len(hs), bool), False, 3)
    L = common.oklab(img)[0]
    mine = board_register.ring_holes(img, L)
    theirs, _ = spike.drill_centres(img, L)
    assert len(mine) == len(theirs) > 30
    a = np.array(sorted(tuple(h['px']) for h in mine))
    b = np.array(sorted(tuple(h['drill']) for h in theirs))
    assert np.abs(a - b).max() < 1e-9


# --- the outlier rule --------------------------------------------------------------------

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
    assert 'residual' not in board_register.roundness_exclusions.__code__.co_varnames
    assert 'held' not in ''.join(board_register.roundness_exclusions.__code__.co_varnames)


def test_the_excluded_holes_are_the_rules_and_both_figures_are_given(built):
    reg = built[0]['reg']
    want = board_register.roundness_exclusions([p['blobs'][0] for p in reg['pairs']], [p['blobs'][1] for p in reg['pairs']])
    assert [e['index'] for e in reg['excluded']] == [e['index'] for e in want]
    keep = [p['heldOutMm'] for i, p in enumerate(reg['pairs']) if i not in {e['index'] for e in want}]
    assert reg['heldOutMmWithExclusion']['median'] == pytest.approx(float(np.median(keep)))


def test_second_moments_axis_ratio():
    import cv2
    m = np.zeros((200, 200), np.uint8)
    cv2.ellipse(m, (100, 100), (40, 20), 30, 0, 360, 1, -1)
    ys, xs = np.nonzero(m)
    assert board_register.axis_ratio(xs, ys) == pytest.approx(2.0, rel=0.02)
    m[:] = 0
    cv2.circle(m, (100, 100), 30, 1, -1)
    ys, xs = np.nonzero(m)
    assert board_register.axis_ratio(xs, ys) == pytest.approx(1.0, abs=0.02)


def test_the_square_pad_measure():
    import cv2
    m = np.zeros((100, 100), np.uint8)
    cv2.rectangle(m, (30, 30), (49, 49), 1, -1)
    assert board_register.squareness(m) > 0.95
    m[:] = 0
    cv2.circle(m, (50, 50), 10, 1, -1)
    assert board_register.squareness(m) < 0.85


# --- pads and drills -----------------------------------------------------------------------

def test_every_drill_has_a_pad_on_both_faces_within_two_tenths(built):
    out = built[0]
    drills, pads = out['drills'], out['pads']
    assert len(drills) >= 0.95 * built[0]['reg']['holes']
    for i, d in enumerate(drills):
        mine = [p for p in pads if p['drill'] == i]
        for face in ('top', 'bottom'):
            assert any(p['face'] == face and math.hypot(p['x'] - d['x'], p['y'] - d['y']) <= 0.2 for p in mine), (i, face)
        assert d['d'] is None or 0.2 < d['d'] < 2.0


def test_an_open_drill_has_its_diameter_and_a_dome_has_none(built):
    out, hs, _, _ = built
    truth = {(round(h[0], 1), round(h[1], 1)): h[2] for h in hs}
    T = np.array([(h[0], h[1]) for h in hs])
    for d in out['drills']:
        k = int(np.argmin(np.hypot(T[:, 0] - d['x'], T[:, 1] - d['y'])))
        kind = hs[k][2]
        if kind == 'ring':
            assert d['d'] == pytest.approx(2 * WALL_R, abs=0.1)
        elif kind == 'dome':
            assert d['d'] is None


# --- footprints ------------------------------------------------------------------------------

def _find(fps, ref):
    got = [f for f in fps if f['ref'] == ref]
    assert len(got) == 1, (ref, [(f['ref'], f['kind'], f['pins']) for f in fps])
    return got[0]


def test_each_dip_is_found_with_its_pin_count_and_pin_one(built):
    out = built[0]
    fps, pads = out['footprints'], out['pads']
    for fid, pins, x0, yu, sp, mark, turned in DIPS:
        f = _find(fps, fid)
        assert f['kind'] == 'dip'
        assert f['pins'] == pins
        assert f['rowSpacingMm'] == sp
        want = dip_pins(pins, x0, yu, sp, turned)
        assert math.hypot(f['pin1'][0] - want[0][0], f['pin1'][1] - want[0][1]) < 0.2, (fid, f['pin1'], want[0])
        # pin by pin, in order
        got = np.array([[pads[i]['x'], pads[i]['y']] for i in f['pads']])
        assert np.hypot(*(got - want).T).max() < 0.2, fid


def test_pin_one_comes_from_the_square_pad_the_print_or_a_hand_mark(built):
    fps = built[0]['footprints']
    for fid, pins, x0, yu, sp, mark, turned in DIPS:
        want = {'square': 'square pad', 'print': 'print', 'hand': 'marked by hand'}[mark]
        assert _find(fps, fid)['pin1From'] == want, fid


def test_a_third_row_is_the_narrow_chips_and_shares_the_lower_row(built):
    out = built[0]
    fps = out['footprints']
    c, alt = _find(fps, 'C'), _find(fps, 'C (300 mil)')
    assert alt['kind'] == 'dip' and alt['pins'] == 24 and alt['rowSpacingMm'] == 7.62 and alt['alternate'] is True
    lower = set(c['pads'][:12])
    assert lower == set(alt['pads'][:12])
    # its middle row's first hole was not found: the row runs the shared row's length, the pin inferred
    assert alt['pin1From'] == 'print'
    assert alt['padsInferred'] + alt['padsNotFound'] >= 1
    _, pins, x0, yu, sp, _, _ = next(d for d in DIPS if d[0] == 'C')
    last = out['pads'][alt['pads'][-1]]
    assert math.hypot(last['x'] - x0, last['y'] - (yu + sp - 7.62)) < 0.2


def test_a_via_in_the_notch_does_not_hide_it(built):
    assert _find(built[0]['footprints'], 'E')['pin1From'] == 'print'


def test_the_connector_row_is_grouped_by_hand_and_not_twice(built):
    fps = built[0]['footprints']
    f = _find(fps, 'P9')
    assert f['kind'] == 'connector' and f['pins'] == 10 and not f['pinsNotFound']
    pads = built[0]['pads']
    got = np.array([[pads[i]['x'], pads[i]['y']] for i in f['pads']])
    assert np.hypot(*(got - np.array(CONNECTOR)).T).max() < 0.2
    assert not [g for g in fps if g is not f and set(g['pads']) & set(f['pads'])], 'the grouping\'s row of the same holes is dropped'


def test_the_edge_fingers_are_one_footprint_of_36_per_face(built):
    out = built[0]
    f = _find(out['footprints'], 'P1')
    assert f['kind'] == 'edge' and f['pins'] == 72
    faces = [out['pads'][i]['face'] for i in f['pads']]
    assert faces.count('top') == 36 and faces.count('bottom') == 36
    for face in ('top', 'bottom'):
        xs = sorted(out['pads'][i]['x'] for i in f['pads'] if out['pads'][i]['face'] == face)
        assert np.diff(xs[1:-1]).mean() == pytest.approx(FINGER_PITCH, abs=0.01), face
        assert xs[0] == pytest.approx(FINGER_X0 - END_SHIFT, abs=0.1)
        assert xs[-1] == pytest.approx(FINGER_X0 + 35 * FINGER_PITCH + END_SHIFT, abs=0.1)
    assert math.hypot(f['pin1'][0] - (FINGER_X0 - END_SHIFT), f['pin1'][1] - sum(FINGERS_Y) / 2) < 0.3
    for i in f['pads']:
        assert out['pads'][i]['shape'] == 'rect' and out['pads'][i]['drill'] is None


def test_every_footprints_kind_is_the_plans(built):
    for f in built[0]['footprints']:
        assert f['kind'] in ('dip', 'connector', 'edge', 'crystal', 'axial', 'radial', 'other'), f


def test_nothing_in_a_mounting_hole_is_a_hole_here():
    hs = [(10.0, 10.0, 'ring', False), (20.0, 10.0, 'dome', False)]
    img = draw_face(frame1().to_px, hs, [True, True], False, 3)
    got, _ = board_register.holes_of_one_face(img)
    assert len(got) == 2
    c = frame1().to_px([(20.0, 10.0)])[0]
    got, info = board_register.holes_of_one_face(img, [(c[0], c[1], 20.0)])
    assert [h['kind'] for h in got] == ['ring'] and info['inMountingHoles'] == 1


def test_a_hand_pin_one_says_whether_the_grouping_agreed_only_when_it_had_one(built):
    c = _find(built[0]['footprints'], 'C')
    assert c['pin1From'] == 'marked by hand' and c['pin1FromGrouping'] is None
    assert c['pin1AgreesWithGrouping'] is None
