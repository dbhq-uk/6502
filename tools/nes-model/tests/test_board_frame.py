"""The board's frame (board_frame.py), on a made-up scan of a made-up board.

    /tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q

The made-up board is drawn four times over size and averaged down in linear
light, as a scanner's blur mixes it, with a known x and y scale (near this
scan's 11.81 pixels per millimetre, and not equal), a known turn and a known
offset. As on the NES board, every DIP lies across: its rows of pins run along
x, so x comes from rows of pins and y only from the spacing between a DIP's
two rows. Each pad is a tinned ring round an open drill hole, and the solder
is drawn off the drill: on the DIPs away from the footprint's middle by 1 per
cent of the row spacing, so the pads' centroids read the spacing 2 per cent
wide, and at each row's two ends outwards along the row by 0.1 mm, so the
centroids read every row 0.2 mm long. Only the drill centres give either true.

The outline has the NES board's kinds of feature: a step down in the top
edge; the remains of break-off tabs standing 0.9 mm out of the top edge, over
most of the short stretch at the top left with half holes between them that
reach the routed edge, and over a third of the top right; two slots either
side of a tab in the bottom edge; a step in the right edge; a round notch in
the left edge, deeper than a half disc, and a small one in the right edge. The
scanner's lid is shaded next to the board, as on the scan. The mounting holes
show the lid, with the hole's far wall seen as a dark crescent on the rear
side and the lid shaded next to it, and one has a speck of dirt on its front
rim. The outline's corners and notches are marked as a person would mark
them, a few pixels off.
"""
import math
import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import board_frame  # noqa: E402

SX, SY, THETA_DEG, ORIGIN = 11.83, 11.79, -0.30, (48.4, 41.7)
SIZE = (1300, 940)                      # the made-up scan, pixels
PITCH = 2.54

# sRGB colours: the lid, the lacquer, a pad's tinned ring, the drill's wall in
# shadow and the lid seen down the drill (task 0's synthetic pads), and a
# mounting hole's far wall.
LID, LACQUER, RING, WALL, DRILL_LID, CRESCENT = (245, 245, 243), (12, 32, 16), (98, 100, 94), (40, 40, 40), (75, 75, 73), (30, 28, 25)
RING_R, WALL_R, DRILL_LID_R = 0.825, 0.474, 0.347     # mm: task 0's synthetic pad, 9.75, 5.6 and 4.1 px at 11.81

# The board in its own frame, millimetres from its top left corner as the
# scan lies, x to the right and y down; its routed edge, corner by corner.
OUTLINE = [(0, 0), (16, 0), (16, 3), (80, 3), (80, 0), (100, 0), (100, 40), (97, 40), (97, 70), (70, 70), (70, 60),
           (60, 60), (60, 70), (30, 70), (30, 60), (20, 60), (20, 70), (0, 70)]
TAB_HEIGHT = 0.9
TABS_TOP_LEFT = ((0.0, 16.0), [3.5, 10.5, 14.0])        # the tabs' stretch, and the half holes' middles
TABS_TOP_RIGHT = [(86.0, 88.0), (91.0, 93.5)]           # tabs on the top right edge, by x
NOTCHES = [((0.2, 30.0), 1.9, 17), ((100.0, 20.0), 0.7, 5)]   # centre, radius, the edge it is in
HOLES = [(3.5, 63.0, 3.2), (94.5, 6.0, 3.0), (50.0, 32.6, 3.4), (93.0, 62.0, 3.0)]
DIRTY_HOLE = 2                          # the hole with a speck of dirt on its front right rim
CRESCENT_MM = 0.35                      # how far the far rim is seen shifted towards the front
SOLDER_END_MM = 0.10                    # each row's end pads' solder, outwards along the row

# Rows of pins across the board: (id, part, first pin's centre, pitches).
# DIPs: (ref, pins, x of pin 1, y of the upper row, spacing). Pin 1 is at the
# lower left; pin N directly above it.
DIPS = [('U6', 40, 8.0, 13.0, 15.24), ('U5', 40, 40.0, 37.0, 15.24), ('U1', 24, 66.0, 13.0, 15.24),
        ('U3', 16, 4.0, 42.0, 7.62), ('U9', 14, 33.0, 58.0, 7.62), ('U7', 16, 72.0, 58.0, 7.62)]
CONNECTOR = ('P2 pins 1 to 24', 'P2', (6.0, 7.0), 23)


def rows_mm():
    """Every row: (id, part, first pad, pitches, solder offset in y)."""
    rows = [CONNECTOR + (0.0,)]
    for ref, pins, x, y, spacing in DIPS:
        n = pins // 2 - 1
        off = 0.01 * spacing
        rows.append((f'{ref} pins 1 to {pins // 2}', ref, (x, y + spacing), n, +off))
        rows.append((f'{ref} pins {pins} to {pins // 2 + 1}', ref, (x, y), n, -off))
    return rows


def to_px(pts):
    t = math.radians(THETA_DEG)
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    return np.asarray(pts, float).reshape(-1, 2) @ (np.diag([SX, SY]) @ R).T + np.array(ORIGIN)


def draw():
    """The made-up scan: labels drawn at four times the size, each label's
    colour mixed in linear light as the scanner's blur mixes it, then shrunk
    by averaging and stored as sRGB."""
    import cv2
    up, frac = 4, 1 << 4
    W, H = SIZE[0] * up, SIZE[1] * up

    def sub(p):          # pixel i of the shrunk picture covers 4i to 4i + 3 here; cv2's fixed point
        return np.round(((np.asarray(p, float) + 0.5) * up - 0.5) * frac).astype(np.int32)

    def disc(img, centre_mm, r_mm, value):
        c = sub(to_px([centre_mm])[0])
        # an ellipse with axes along the scan's x and y is a circle in
        # millimetres, the scale being the scanner's; a turn changes nothing
        axes = (int(round(r_mm * SX * up * frac)), int(round(r_mm * SY * up * frac)))
        cv2.ellipse(img, (int(c[0]), int(c[1])), axes, 0, 0, 360, value, -1, cv2.LINE_8, 4)

    def poly(img, pts, value):
        cv2.fillPoly(img, [sub(to_px(pts)).reshape(-1, 1, 2)], value, cv2.LINE_8, 4)

    BOARD_, RING_, WALL_, DLID_, HOLE_, CRES_ = 1, 2, 3, 4, 5, 6
    label = np.zeros((H, W), np.uint8)
    poly(label, OUTLINE, BOARD_)
    # the tabs' remains: a strip standing out of the top left edge with half
    # holes cut down to the routed edge, and two blocks on the top right
    (x0, x1), holes = TABS_TOP_LEFT
    poly(label, [(x0, -TAB_HEIGHT), (x1, -TAB_HEIGHT), (x1, 0.01), (x0, 0.01)], BOARD_)
    for xc in holes:
        disc(label, (xc, -TAB_HEIGHT), TAB_HEIGHT, 0)
    for a, b in TABS_TOP_RIGHT:
        poly(label, [(a, -TAB_HEIGHT + 0.1), (b, -TAB_HEIGHT + 0.1), (b, 0.01), (a, 0.01)], BOARD_)
    for centre, r, _ in NOTCHES:
        disc(label, centre, r, 0)
    for rid, part, (x, y), n, off in rows_mm():
        for k in range(n + 1):
            c = (x + k * PITCH, y)
            dx = -SOLDER_END_MM if k == 0 else SOLDER_END_MM if k == n else 0.0
            disc(label, (c[0] + dx, c[1] + off), RING_R, RING_)
            disc(label, c, WALL_R, WALL_)
            disc(label, c, DRILL_LID_R, DLID_)
    # The lid next to the board is shaded, darkest at the board's edge (to
    # 55 per cent of its light) and lighter over about a millimetre.
    outside = (label == 0).astype(np.uint8)
    d = cv2.distanceTransform(outside, cv2.DIST_L2, 5) / (math.sqrt(SX * SY) * up)
    shade = np.where(outside == 1, 1 - 0.45 * np.exp(-d / 0.4), 1.0).astype(np.float32)
    del d
    # A mounting hole: inside its near rim (the board's face on the glass) the
    # lid shows where the far rim, seen shifted to the front, lets it through;
    # the rest inside the near rim is the far wall, the crescent.
    for i, (hx, hy, dmm) in enumerate(HOLES):
        near, far = np.zeros_like(label), np.zeros_like(label)
        disc(near, (hx, hy), dmm / 2, 1)
        disc(far, (hx, hy + CRESCENT_MM), dmm / 2, 1)
        label[near == 1] = CRES_
        label[(near == 1) & (far == 1)] = HOLE_
        del near, far
        if i == DIRTY_HOLE:                     # dirt on the front right rim, inside the hole
            a = math.radians(45)
            disc(label, (hx + 0.85 * dmm / 2 * math.cos(a), hy + 0.85 * dmm / 2 * math.sin(a)), 0.4, CRES_)
        # The lid is shaded next to the crescent, over 0.8 mm, so that side's
        # edge is soft, as on the scan; the other side is sharp.
        cx, cy = to_px([(hx, hy + CRESCENT_MM)])[0]
        cx, cy = (cx + 0.5) * up - 0.5, (cy + 0.5) * up - 0.5
        r = dmm / 2 * math.sqrt(SX * SY) * up
        y0, x0 = max(int(cy - r - 2), 0), max(int(cx - r - 2), 0)
        yy, xx = np.ogrid[y0:int(cy + r + 3), x0:int(cx + r + 3)]
        box = (slice(y0, int(cy + r + 3)), slice(x0, int(cx + r + 3)))
        below_top = (yy - (cy - r)) + 0 * xx                         # how far below the far rim's top
        f = np.clip(0.15 + below_top / (0.8 * SY * up), 0.15, 1.0).astype(np.float32)
        shade[box] = np.where(label[box] == HOLE_, f, shade[box])

    def linear(c):
        c = np.array(c, np.float32) / 255
        return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    colours = [LID, LACQUER, RING, WALL, DRILL_LID, LID, CRESCENT]
    lin = np.zeros((SIZE[1], SIZE[0], 3), np.float32)
    for ch in range(3):
        lut = np.array([linear(c)[ch] for c in colours], np.float32)
        big = lut[label] * shade
        lin[..., ch] = cv2.resize(big, SIZE, interpolation=cv2.INTER_AREA)
        del big
    rgb = 255 * np.where(lin <= 0.0031308, 12.92 * lin, 1.055 * np.power(np.maximum(lin, 0), 1 / 2.4) - 0.055)
    rgb += np.random.default_rng(7).normal(0, 2.0, rgb.shape)
    return np.clip(np.round(rgb), 0, 255).astype(np.uint8)


@pytest.fixture(scope='module')
def scan():
    return draw()


def marked():
    """The rows, footprints and outline as a person would mark them: each
    row's first and last pad, each corner and each notch's deepest point a few
    pixels off."""
    rng = np.random.default_rng(11)
    rows = []
    for rid, part, first, n, _ in rows_mm():
        last = (first[0] + n * PITCH, first[1])
        a, b = to_px([first, last]) + rng.uniform(-3, 3, (2, 2))
        rows.append({'id': rid, 'part': part, 'first': a.tolist(), 'last': b.tolist(), 'pitches': n, 'pitchMm': PITCH})
    footprints = [{'ref': ref, 'pins': pins, 'spacingMm': spacing, 'pin1Row': f'{ref} pins 1 to {pins // 2}',
                   'pinNRow': f'{ref} pins {pins} to {pins // 2 + 1}'} for ref, pins, _, _, spacing in DIPS]
    corners = to_px(OUTLINE) + rng.uniform(-3, 3, (len(OUTLINE), 2))
    notches = []
    for (cx, cy), r, edge in NOTCHES:
        inward = (1.0, 0.0) if edge == 17 else (-1.0, 0.0)
        deepest = to_px([(cx + inward[0] * r, cy + inward[1] * r)])[0] + rng.uniform(-2, 2, 2)
        notches.append({'id': f'notch in edge {edge}', 'edge': edge, 'deepest': deepest.tolist()})
    outline = {'corners': corners.tolist(), 'tabs': [0], 'notches': notches}
    return rows, footprints, outline


@pytest.fixture(scope='module')
def measured(scan):
    rows, footprints, outline = marked()
    return board_frame.measure(scan, rows, footprints, outline)


# --- the frame and the fits, on exact numbers ------------------------------------

def test_the_frame_goes_both_ways():
    f = board_frame.Frame(11.8, 11.7, math.radians(-0.4), (12.5, -3.25))
    p = np.array([[0, 0], [100.5, 2000.25], [4000, 3000]], float)
    assert np.abs(f.to_px(f.to_board(p)) - p).max() < 1e-9
    assert np.abs(f.to_board(f.to_px([[0, 0]]))).max() < 1e-12


def exact(axis, mm):
    v = (mm, 0.0) if axis == 'x' else (0.0, mm)
    return to_px([v])[0] - to_px([(0, 0)])[0]


def test_the_scale_comes_from_rows_across_and_spacings_down():
    rows = [{'id': str(i), 'd': exact('x', n * PITCH), 'trueMm': n * PITCH} for i, n in enumerate((19, 23, 11, 7))]
    fps = [{'ref': f'U{i}', 'd': exact('y', s), 'spacingMm': s} for i, s in enumerate((15.24, 15.24, 7.62))]
    sx, sy = board_frame.fit_scale(rows, fps)
    assert sx == pytest.approx(SX, rel=1e-9) and sy == pytest.approx(SY, rel=1e-9)


def test_a_held_out_row_or_footprint_is_never_in_its_own_fit():
    rows = [{'id': str(i), 'd': exact('x', n * PITCH), 'trueMm': n * PITCH, 'pitches': n} for i, n in enumerate((19, 19, 23, 11, 7))]
    fps = [{'ref': f'U{i}', 'd': exact('y', s), 'spacingMm': s} for i, s in enumerate((15.24, 15.24, 7.62, 7.62))]
    # Row 3 made 1 mm long and U2 made 1 per cent wide: each, held out, reads
    # exactly that, as neither is in the fit that judges it.
    rows[3]['d'] = rows[3]['d'] * (11 * PITCH + 1.0) / (11 * PITCH)
    fps[2]['d'] = fps[2]['d'] * 1.01
    hx = {r['row']: r for r in board_frame.held_out_x(rows, fps)}
    assert hx['3']['errMm'] == pytest.approx(1.0, abs=1e-6)
    assert hx['3']['scaledErrMm'] == pytest.approx(48.26 / (11 * PITCH), abs=1e-6)
    assert hx['0']['scored'] and hx['2']['scored'] and not hx['3']['scored']     # 30 mm and over are scored
    hy = {r['ref']: r for r in board_frame.held_out_y(rows, fps)}
    assert hy['U2']['errPct'] == pytest.approx(1.0, abs=1e-6)


def test_the_verdicts_are_the_plans():
    def fig(x=0.10, rows=6, y=0.3, fps=10, ratio=1.004):
        return {'heldOutX': {'n': rows, 'scaledErrMm': {'median': x}}, 'heldOutY': {'errPct': {'median': y}, 'footprints': [{}] * fps},
                'pxPerMm': {'x': ratio, 'y': 1.0}}

    def names(f):
        return [v['verdict'] for v in board_frame.judge(f)]
    assert [v['check'] for v in board_frame.judge(fig())] == ['scale x', 'scale y', 'x against y']
    assert names(fig()) == ['pass'] * 3
    assert names(fig(x=0.2)) == ['between pass and stop', 'pass', 'pass']
    assert names(fig(x=0.26)) == ['STOP', 'pass', 'pass']
    assert names(fig(rows=3)) == ['between pass and stop', 'pass', 'pass']
    assert names(fig(y=0.6)) == ['pass', 'between pass and stop', 'pass']
    assert names(fig(y=1.1)) == ['pass', 'STOP', 'pass']
    assert names(fig(fps=7)) == ['pass', 'between pass and stop', 'pass']
    assert names(fig(ratio=1.016)) == ['pass', 'pass', 'STOP']
    assert names(fig(ratio=0.986)) == ['pass', 'pass', 'pass']


def test_a_drill_off_its_rows_line_is_refused():
    # A row of 20 drills on 2.54 mm, with 0.02 mm of noise; pin 7 is found
    # 0.3 mm off (a glint in its hole) and pin 19, an end, 0.25 mm along the
    # row. Each is refused; the rest are kept.
    rng = np.random.default_rng(3)
    k = np.arange(20)
    pts = np.c_[100 + k * PITCH * SX, 300 + 0.4 * k] + rng.normal(0, 0.02 * SX, (20, 2))
    pts[7, 1] += 0.3 * SY
    pts[19, 0] += 0.25 * SX
    drills = [tuple(p) for p in pts]
    drills[12] = None                                   # one not found at all
    keep, why = board_frame.drills_on_their_row(drills, px_per_mm=SX)
    assert [i for i, ok in enumerate(keep) if not ok] == [7, 12, 19]
    assert why[7] == why[19] == 'off its row' and why[12] is None


# --- the made-up scan ----------------------------------------------------------------

def test_the_scale_and_turn_come_back(measured):
    result = measured[0]
    assert result['pxPerMm']['x'] == pytest.approx(SX, rel=5e-4)
    assert result['pxPerMm']['y'] == pytest.approx(SY, rel=5e-4)
    assert result['rotationDeg'] == pytest.approx(THETA_DEG, abs=0.01)


def test_y_comes_from_the_drill_centres_not_the_solder(measured):
    result = measured[0]
    # the solder's centroids read every spacing 2 per cent wide; the drills do not
    assert result['yFromPadCentroids']['pxPerMmY'] > SY * 1.01
    assert result['heldOutY']['errPct']['max'] < 0.3
    assert {f['ref'] for f in result['heldOutY']['footprints']} == {d[0] for d in DIPS}
    assert all(f['pairs'] == f['pins'] // 2 for f in result['heldOutY']['footprints'])


def test_x_comes_from_the_drill_centres_not_the_solder(measured):
    result = measured[0]
    # the end pads' solder reads every row 0.2 mm long; the drills do not
    assert result['heldOutX']['scaledErrMm']['max'] < 0.05
    assert result['xFromPadCentroids']['pxPerMmX'] > SX * 1.002
    assert result['xFromPadCentroids']['rows'] == len(result['heldOutX']['rows'])


def test_the_held_out_rows_are_scored(measured):
    hx = measured[0]['heldOutX']
    assert {r['row'] for r in hx['rows'] if r['scored']} == {'P2 pins 1 to 24', 'U6 pins 1 to 20', 'U6 pins 40 to 21',
                                                             'U5 pins 1 to 20', 'U5 pins 40 to 21'}
    assert hx['n'] == 5 and len(hx['rows']) == 13
    assert hx['largestRow'] in {r['row'] for r in hx['rows'] if r['scored']}


def test_the_rows_are_straight(measured):
    s = measured[0]['straightness']
    assert s['rmsMm']['max'] < 0.02
    assert {r['row'] for r in s['rows']} == {'P2 pins 1 to 24', 'U6 pins 1 to 20', 'U6 pins 40 to 21', 'U5 pins 1 to 20',
                                             'U5 pins 40 to 21', 'U1 pins 1 to 12', 'U1 pins 24 to 13'}
    assert s['over'] == []


def test_the_outline_comes_back_with_its_notches(measured):
    result = measured[0]
    outline = np.array(result['outline'], float)
    assert np.allclose(outline[0], outline[-1]), 'the outline is closed'
    assert np.allclose(outline[0], [0, 0]), 'it starts at the origin'
    got = outline[:-1]
    # every corner is found within 0.1 mm, the tabs' remains left out
    for corner in OUTLINE:
        assert np.hypot(*(got - corner).T).min() < 0.1, (corner, got.round(3).tolist())
    # every point of the outline is a corner or on a notch
    near_corner = np.array([np.hypot(*(np.array(OUTLINE, float) - p).T).min() < 0.1 for p in got])
    on_notch = np.zeros(len(got), bool)
    for (cx, cy), r, _ in NOTCHES:
        on = np.abs(np.hypot(got[:, 0] - cx, got[:, 1] - cy) - r) < 0.1
        assert on.sum() >= 6, (cx, cy)
        on_notch |= on
    assert (on_notch | near_corner).all(), got[~(on_notch | near_corner)].round(3).tolist()
    notches = result['notches']
    assert len(notches) == len(NOTCHES)
    for (cx, cy), r, _ in NOTCHES:
        n = min(notches, key=lambda n: math.hypot(n['x'] - cx, n['y'] - cy))
        assert math.hypot(n['x'] - cx, n['y'] - cy) < 0.1 and n['r'] == pytest.approx(r, abs=0.1), n
    assert result['board']['widthMm'] == pytest.approx(100, abs=0.1)
    assert result['board']['depthMm'] == pytest.approx(70, abs=0.1)


def test_the_holes_come_back_on_their_top_rims(measured):
    holes = measured[0]['holes']
    assert len(holes) == len(HOLES), holes
    for x, y, d in HOLES:
        best = min(holes, key=lambda h: math.hypot(h['x'] - x, h['y'] - y))
        assert math.hypot(best['x'] - x, best['y'] - y) < 0.05, (x, y, best)
        assert best['d'] == pytest.approx(d, abs=0.1)
        assert abs(((best['crescentDeg'] + 180) % 360) - 90) < 30      # the crescent is to the rear (-y)


def test_the_origin_is_the_top_left_corner(measured):
    origin = measured[0]['origin']
    assert math.hypot(origin[0] - ORIGIN[0], origin[1] - ORIGIN[1]) < 0.1 * SX


def test_an_outline_marked_with_two_edges_on_one_line_running_on_is_refused(scan):
    rows, footprints, outline = marked()
    corners = outline['corners']
    bad = dict(outline, corners=corners[:1] + [[(corners[0][0] + corners[1][0]) / 2, corners[0][1]]] + corners[1:],
               tabs=[], notches=[])
    with pytest.raises(ValueError, match='turn'):
        board_frame.outline_edges(bad, SX, SY, math.radians(THETA_DEG))
