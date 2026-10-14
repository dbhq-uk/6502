"""The copper on both faces, the print, and the checks on them
(board_trace.py), on a made-up board.

    ~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q

The made-up board is drawn as the Issue 7 board's bare scans show it, already
in the board frame at 16 pixels per millimetre. Every colour is the median
sRGB of a box on the scans, read on 13 October 2026 (board_trace.py's probe
comments give the boxes): the laminate under the lacquer nearly black, copper
under the lacquer a strong green, tinned pads grey with a dark rim round the
solder, the solder side's pads in an olive halo where the lacquer stops, the
component side's in a grey ring, and the print yellow. The light falls off by
15 per cent across the board, each channel carries noise, and the whole is
blurred a little, as a scanner does.

The NES's trace failed on its scans in four ways, and each is drawn here so
that a trace that fails the same way fails a test (the plan's review focus 2):
a track that shows only as two bright edges with a dim middle; pads cut off
from their tracks by a rule about the light round a pad; a hatched pour; and
a short. Two nets are drawn (GND, three pads joined through a via to the
solder side, and +5V, two pads) with a GND stub 0.3 mm from the +5V track, so
the gap between them must be found.
"""
import math
import os
import sys

import cv2
import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import board_trace  # noqa: E402
import common  # noqa: E402

R = board_trace.R
W_MM, H_MM = 60.0, 40.0
W, H = int(W_MM * R), int(H_MM * R)

# Median sRGB of boxes on the scans in the 16 px/mm grid (board_trace.py's probes, 13 October 2026)
LAMINATE, COPPER = (34, 43, 34), (26, 109, 57)
DIM = (30, 76, 45)                     # half way from the laminate to the copper: an edges-only track's middle
SOLDER_TOP, SOLDER_BOTTOM = (147, 150, 142), (166, 175, 170)
RIM, HALO, PRINT, DRILL = (50, 56, 48), (64, 74, 58), (212, 183, 39), (8, 8, 8)
NOISE = 4.0

SOLDER_R, RIM_R, RING_R, HALO_HALF = 0.78, 0.88, 1.0, 1.1
GND_PADS = [(8.0, 10.0), (30.0, 10.0), (50.0, 30.0)]
VCC_PADS = [(8.0, 14.0), (30.0, 14.0)]
VIA = (40.0, 10.0)
STUB_Y = 13.4                         # a GND stub whose edge is 0.3 mm from the +5V track's
PARALLEL = (33.0, 33.65, 34.3, 34.95)  # 0.4 mm tracks 0.25 mm apart, under a print line at y 30.5
ROW = [(10.0 + 2.54 * k, 5.0) for k in range(4)]
BETWEEN_X = 10.0 + 2.54 * 1.5
EDGES_Y = (20.0, 36.0)                # an edges-only track along y = 24 from x 20 to x 36
POUR = (40.0, 16.0, 55.0, 26.0)       # a hatched pour: a frame and diagonal lines inside
SMEAR = ((39.4, 20.0), (40.6, 24.0))  # solder over the solder side's track


def px(p):
    return int(round(p[0] * R)), int(round(p[1] * R))


def line(mask, a, b, width_mm, value=1):
    cv2.line(mask, px(a), px(b), value, max(1, int(round(width_mm * R))), cv2.LINE_8)


def hatched(mask, box, pitch=1.0, width=0.3, frame=0.5):
    x0, y0, x1, y1 = box
    m = np.zeros_like(mask)
    k = x0 - (y1 - y0)
    while k < x1:
        line(m, (k, y1), (k + (y1 - y0), y0), width)
        k += pitch
    inside = np.zeros_like(mask)
    cv2.rectangle(inside, px((x0, y0)), px((x1, y1)), 1, -1)
    m &= inside
    cv2.rectangle(m, px((x0, y0)), px((x1, y1)), 1, int(round(frame * R)))
    mask |= m


def draw(short=False):
    """(top RGB, bottom RGB, truths): the faces in the board frame, with each
    face's true copper, the true print, the pads ((x, y, r) in mm, r the
    solder's radius, as task 3 gives them) and where the truth is not known."""
    top, bot = np.zeros((H, W), np.uint8), np.zeros((H, W), np.uint8)
    line(top, GND_PADS[0], GND_PADS[1], 0.5)
    line(top, GND_PADS[1], VIA, 0.5)
    line(top, VCC_PADS[0], VCC_PADS[1], 0.4)
    line(top, (12.0, STUB_Y - 0.15), (26.0, STUB_Y - 0.15), 0.4)   # the GND stub, edge 0.3 mm from +5V's
    line(top, (12.0, STUB_Y - 0.15), (12.0, 10.0), 0.4)
    line(bot, VIA, (40.0, 30.0), 0.5)
    line(bot, (40.0, 30.0), GND_PADS[2], 0.5)
    line(bot, ROW[0], (ROW[0][0], 1.0), 0.3)
    line(bot, (BETWEEN_X, 1.5), (BETWEEN_X, 9.0), 0.3)
    line(top, (10.0, 30.0), (30.0, 30.0), 0.5)                    # a lone track under a wide printed block
    for x in PARALLEL:
        top[px((0, 27.0))[1]:px((0, 34.0))[1], px((x, 0))[0] - 3:px((x, 0))[0] + 3] = 1
    hatched(top, POUR)
    edges_full = np.zeros((H, W), np.uint8)
    line(edges_full, (EDGES_Y[0], 24.0), (EDGES_Y[1], 24.0), 0.5)
    middle = np.zeros((H, W), np.uint8)
    line(middle, (EDGES_Y[0] + 0.3, 24.0), (EDGES_Y[1] - 0.3, 24.0), 0.25)
    if short:
        line(top, (20.0, 10.0), (20.0, 14.0), 0.4)
    pads = [(x, y, SOLDER_R) for x, y in GND_PADS + VCC_PADS + [VIA]]
    row_pads = [(x, y, SOLDER_R) for x, y in ROW]
    printed = np.zeros((H, W), np.uint8)
    line(printed, (15.0, 7.0), (15.0, 17.0), 0.3)
    line(printed, (25.0, 7.0), (25.0, 12.5), 0.3)
    line(printed, (32.0, 30.5), (36.0, 30.5), 0.3)
    cv2.rectangle(printed, px((18.5, 28.0)), px((21.5, 32.0)), 1, -1)
    for k in range(4):
        line(printed, (5.0 + k * 1.5, 34.0), (5.5 + k * 1.5, 36.0), 0.25)

    def face(copper, side):
        img = np.empty((H, W, 3), np.float32)
        img[:] = LAMINATE
        own = pads + (row_pads if side == 'bottom' else [])
        if side == 'bottom':
            for x, y, _ in own:
                a, b = px((x - HALO_HALF, y - HALO_HALF)), px((x + HALO_HALF, y + HALO_HALF))
                cv2.rectangle(img, a, b, HALO, -1)
        else:
            for x, y, _ in own:
                cv2.circle(img, px((x, y)), int(round(RING_R * R)), HALO, -1)
        img[copper > 0] = COPPER
        if side == 'top':
            img[edges_full > 0] = COPPER
            img[middle > 0] = DIM
        if side == 'bottom':
            cv2.rectangle(img, px(SMEAR[0]), px(SMEAR[1]), SOLDER_BOTTOM, -1)
        for x, y, r in own:
            cv2.circle(img, px((x, y)), int(round(RIM_R * R)), RIM, -1)
            cv2.circle(img, px((x, y)), int(round(r * R)), SOLDER_TOP if side == 'top' else SOLDER_BOTTOM, -1)
        if side == 'top':
            img[printed > 0] = PRINT
        light = np.linspace(1.0, 0.85, W, dtype=np.float32)[None, :, None]
        rng = np.random.default_rng(7 if side == 'top' else 8)
        img = img * light + rng.normal(0, NOISE, img.shape)
        img = cv2.GaussianBlur(img, (0, 0), 0.7)
        return np.clip(img, 0, 255).astype(np.uint8)

    truth_top = (top > 0) | (edges_full > 0)
    truth_bot = bot > 0
    smear = np.zeros((H, W), np.uint8)
    cv2.rectangle(smear, px(SMEAR[0]), px(SMEAR[1]), 1, -1)
    truth_bot |= smear > 0
    unknown_top = np.zeros((H, W), np.uint8)
    unknown_bot = np.zeros((H, W), np.uint8)
    for x, y, _ in pads + row_pads:
        for m in (truth_top, truth_bot):
            if (x, y, _) in row_pads and m is truth_top:
                continue
            cv2.circle(m.view(np.uint8), px((x, y)), int(round(RIM_R * R)), 1, -1)
        # the rim's outer edge, and the component side's grey ring, are not known to be copper or not
        cv2.circle(unknown_top, px((x, y)), int(round(RING_R * R)) + 2, 1, -1)
        cv2.circle(unknown_bot, px((x, y)), int(round(RIM_R * R)) + 2, 1, -1)
        cv2.circle(unknown_top, px((x, y)), int(round(RIM_R * R)) - 2, 0, -1)
        cv2.circle(unknown_bot, px((x, y)), int(round(RIM_R * R)) - 2, 0, -1)
    return face(top, 'top'), face(bot, 'bottom'), {
        'top': truth_top, 'bottom': truth_bot, 'print': printed > 0, 'pads': pads, 'rowPads': row_pads,
        'unknownTop': unknown_top > 0, 'unknownBottom': unknown_bot > 0}


def iou(a, b, where):
    a, b = a & where, b & where
    return (a & b).sum() / max((a | b).sum(), 1)


@pytest.fixture(scope='module')
def traced():
    top, bot, truth = draw()
    inside = np.ones((H, W), bool)
    t = board_trace.trace_face(top, inside, pads=truth['pads'], side='top')
    b = board_trace.trace_face(bot, inside, pads=truth['pads'] + truth['rowPads'], side='bottom')
    return t, b, truth


def at(mask_or_labels, x, y):
    return mask_or_labels[int(round(y * R)), int(round(x * R))]


def test_the_copper_comes_back_on_both_faces(traced):
    t, b, truth = traced
    seen = ~cv2.dilate(truth['print'].astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool)
    assert iou(t['copper'], truth['top'], seen & ~truth['unknownTop']) >= 0.95
    assert iou(b['copper'], truth['bottom'], ~truth['unknownBottom']) >= 0.95


def test_the_solder_sides_halo_and_the_component_sides_ring_are_not_copper(traced):
    t, b, truth = traced
    # the halo's corners, away from the pads' discs and from every track
    for x, y in ROW[1:]:
        assert not at(b['copper'], x + HALO_HALF - 0.1, y + HALO_HALF - 0.1), (x, y)
    # the component side's grey ring, on the side of a pad no track reaches
    x, y = GND_PADS[0]
    assert not at(t['copper'], x - RING_R + 0.05, y + 0.3)


def test_the_print_is_traced_apart_from_the_copper(traced):
    t, b, truth = traced
    assert t['print'][truth['print']].mean() >= 0.98
    near = cv2.dilate(truth['print'].astype(np.uint8), board_trace.disc(0.25 * R)) > 0
    assert near[t['print']].mean() >= 0.98
    for x, y, r in truth['pads']:
        assert not at(t['print'], x, y)
    assert not b['print'].any(), 'the solder side carries no print'


def test_copper_under_thin_print_is_recovered_only_where_it_continues_in_line(traced):
    t, _, truth = traced
    for x, y in ((15.0, 10.0), (15.0, 14.0), (25.0, 10.0)):
        assert at(t['copper'], x, y), (x, y)
        assert at(t['recovered'], x, y), (x, y)
    assert not at(t['recovered'], 15.0, 12.0)
    assert not at(t['recovered'], 15.0, 16.5)
    for x in PARALLEL:
        assert at(t['recovered'], x, 30.5), x
    for x in PARALLEL[:-1]:
        assert not t['recovered'][int(round(30.5 * R)), int(round(x * R)) + 5], x
    win = t['copper'][int(27.5 * R):int(34 * R), int(32.5 * R):int(35.5 * R)]
    assert cv2.connectedComponents(win.astype(np.uint8), connectivity=8)[0] - 1 == 4
    assert not at(t['recovered'], 20.0, 30.0), 'the 3 mm block is too wide to recover under'


def test_a_track_seen_only_as_two_bright_edges_comes_back_whole(traced):
    t, _, truth = traced
    band = np.zeros((H, W), np.uint8)
    line(band, (EDGES_Y[0] + 0.5, 24.0), (EDGES_Y[1] - 0.5, 24.0), 0.5)
    band = band > 0
    assert t['copper'][band].mean() >= 0.95, 'the track\'s dim middle is copper too'
    lab = cv2.connectedComponents(t['copper'].astype(np.uint8), connectivity=8)[1]
    assert at(lab, EDGES_Y[0] + 0.2, 24.0) == at(lab, EDGES_Y[1] - 0.2, 24.0) != 0


def test_a_hatched_pour_comes_back_as_one_piece(traced):
    t, _, truth = traced
    x0, y0, x1, y1 = POUR
    win = (slice(int(y0 * R) - 4, int(y1 * R) + 5), slice(int(x0 * R) - 4, int(x1 * R) + 5))
    assert iou(t['copper'][win], truth['top'][win], np.ones_like(truth['top'][win])) >= 0.85
    assert cv2.connectedComponents(t['copper'][win].astype(np.uint8), connectivity=8)[0] - 1 == 1


def test_pads_stay_joined_to_their_tracks(traced):
    t, b, _ = traced
    lt = cv2.connectedComponents(t['copper'].astype(np.uint8), connectivity=8)[1]
    lb = cv2.connectedComponents(b['copper'].astype(np.uint8), connectivity=8)[1]
    assert at(lt, *GND_PADS[0]) == at(lt, 19.0, 10.0) == at(lt, *GND_PADS[1]) == at(lt, *VIA) != 0
    assert at(lt, *VCC_PADS[0]) == at(lt, *VCC_PADS[1]) != 0
    assert at(lb, *VIA) == at(lb, 40.0, 22.0) == at(lb, *GND_PADS[2]) != 0, 'and through the solder over the track'
    assert at(lb, *ROW[0]) == at(lb, ROW[0][0], 1.5) != 0


def test_neighbouring_pads_in_one_halo_are_not_joined(traced):
    _, b, _ = traced
    lab = cv2.connectedComponents(b['copper'].astype(np.uint8), connectivity=8)[1]
    pads = [at(lab, x, y) for x, y in ROW]
    assert all(pads) and len(set(pads)) == 4, 'each pad of the row is its own piece'
    assert at(lab, BETWEEN_X, 2.0) == at(lab, BETWEEN_X, 8.5) != 0
    assert at(lab, BETWEEN_X, 5.0) not in pads


def test_the_nets_are_found_apart_and_joined_through_the_via(traced):
    t, b, truth = traced
    chips = [{'ref': 'A', 'gnd': px(GND_PADS[0]), 'vcc': px(VCC_PADS[0])},
             {'ref': 'B', 'gnd': px(GND_PADS[1]), 'vcc': px(VCC_PADS[1])},
             {'ref': 'C', 'gnd': px(GND_PADS[2]), 'vcc': None}]
    n = board_trace.nets_check(t['copper'], b['copper'], [px(VIA)], chips)
    assert n['gnd'] == {'pins': 3, 'inLargest': 3}
    assert n['vcc'] == {'pins': 2, 'inLargest': 2}
    assert n['touching'] is False
    import re
    for k in ('gnd', 'vcc'):
        for ref, net in n['pinNets'][k]:
            assert net is None or re.fullmatch(r'(top|bottom):[0-9]+', net), (ref, net)
    n = board_trace.nets_check(t['copper'], b['copper'], [], chips)
    assert n['gnd'] == {'pins': 3, 'inLargest': 2}


def test_a_drawn_short_is_caught():
    top, bot, truth = draw(short=True)
    inside = np.ones((H, W), bool)
    t = board_trace.trace_face(top, inside, pads=truth['pads'], side='top')
    b = board_trace.trace_face(bot, inside, pads=truth['pads'] + truth['rowPads'], side='bottom')
    chips = [{'ref': 'A', 'gnd': px(GND_PADS[0]), 'vcc': px(VCC_PADS[0])},
             {'ref': 'B', 'gnd': px(GND_PADS[1]), 'vcc': px(VCC_PADS[1])}]
    assert board_trace.nets_check(t['copper'], b['copper'], [px(VIA)], chips)['touching'] is True


def test_the_verdicts_are_the_plans():
    v = board_trace.verdicts
    good = {'coverage': {'top': 0.3, 'bottom': 0.2}, 'drillsInCopper': {'top': 0.97, 'bottom': 0.95},
            'nets': {'chips': 20, 'gnd': {'pins': 20, 'inLargest': 18}, 'vcc': {'pins': 20, 'inLargest': 18}, 'touching': False},
            'againstI5': {'registration': {'heldOutMm': {'median': 1.0}}, 'iou': 0.50}}
    assert v(good) == {'coverage': 'pass', 'drillsInCopper': 'pass', 'nets': 'pass', 'i5Registration': 'pass', 'againstI5': 'pass'}
    assert v(dict(good, coverage={'top': 0.51, 'bottom': 0.2}))['coverage'] == 'fail'
    assert v(dict(good, coverage={'top': 0.3, 'bottom': 0.09}))['coverage'] == 'fail'
    assert v(dict(good, drillsInCopper={'top': 0.94, 'bottom': 0.99}))['drillsInCopper'] == 'fail'
    assert v(dict(good, nets=dict(good['nets'], chips=19)))['nets'] == 'fail'
    assert v(dict(good, nets=dict(good['nets'], touching=True)))['nets'] == 'fail'
    assert v(dict(good, nets=dict(good['nets'], gnd={'pins': 20, 'inLargest': 17})))['nets'] == 'fail'
    assert v(dict(good, nets=dict(good['nets'], vcc={'pins': 20, 'inLargest': 17})))['nets'] == 'fail'
    i5 = lambda med, u: dict(good, againstI5={'registration': {'heldOutMm': {'median': med}}, 'iou': u})  # noqa: E731
    assert v(i5(1.01, 0.5))['i5Registration'] == 'between pass and stop'
    assert v(i5(2.0, 0.5))['i5Registration'] == 'between pass and stop'
    assert v(i5(2.01, 0.5))['i5Registration'] == 'STOP'
    assert v(i5(1.0, 0.499))['againstI5'] == 'fail'


def test_a_drill_is_in_copper_when_its_ring_is():
    _, _, truth = draw()
    copper = truth['top']
    drills = [(x, y, None) for x, y, r in truth['pads']] + [(5.0, 5.0, 0.8)]
    got = board_trace.drills_in_copper(copper, drills)
    assert got == [True] * (len(drills) - 1) + [False]


def test_the_solder_side_is_read_back_through_its_saved_map():
    # a made-up cubic map from I2's mirrored pixels to board millimetres, as registration.json keeps it
    rng = np.random.default_rng(3)
    src = rng.uniform(0, 4800, (300, 2))
    a = math.radians(0.7)
    dst = src @ (np.array([[math.cos(a), -math.sin(a)], [math.sin(a), math.cos(a)]]).T / 15.8) + [3.0, -2.0]
    dst += 0.2 * np.sin(src / 1500.0)
    params = common._fit('cubic', src, dst)
    to_px = board_trace.solder_to_px(params)
    q = common.transform('cubic', params, src[:50])
    assert np.abs(to_px(q) - src[:50]).max() < 1e-6


def test_the_map_puts_each_layer_in_its_channel_at_its_resolution():
    top = np.zeros((H, W), bool)
    bot = np.zeros((H, W), bool)
    pr = np.zeros((H, W), bool)
    top[:, : W // 2] = True
    bot[: H // 2, :] = True
    pr[H // 2:, W // 2:] = True
    inside = np.ones((H, W), bool)
    inside[:16, :16] = False
    m = board_trace.make_map(top, bot, pr, inside, ppm=6)
    assert m.shape == (H_MM * 6, W_MM * 6, 3)
    assert m.dtype == np.uint8 and set(np.unique(m)) <= {0, 255}
    assert m[7:, : m.shape[1] // 2 - 1, 0].min() == 255 and m[:, m.shape[1] // 2 + 1:, 0].max() == 0
    assert m[: m.shape[0] // 2 - 1, 6:, 1].min() == 255
    assert m[m.shape[0] // 2 + 1:, m.shape[1] // 2 + 1:, 2].min() == 255
    assert m[:5, :5].max() == 0, 'nothing outside the board'


def test_each_chip_gives_the_pins_its_table_names_and_the_others_say_why_not():
    pads = [{'x': 10.0 + 2.54 * k, 'y': 20.0} for k in range(7)] + [{'x': 10.0 + 2.54 * k, 'y': 27.62} for k in range(6, -1, -1)]
    reg = {'pads': pads, 'footprints': [
        {'ref': 'IC1', 'kind': 'dip', 'pins': 14, 'pin1': [10.0, 20.0], 'pads': list(range(14))},
        {'ref': 'IC2', 'kind': 'dip', 'pins': 14, 'pin1': [10.0, 20.0], 'pads': list(range(14))}]}
    table = {'ics': [{'ref': 'IC1', 'part': '74LS00', 'pins': 14, 'gnd': 7, 'vcc': 14, 'pinoutSource': 'a data sheet'},
                     {'ref': 'IC2', 'part': '74LS00', 'pins': 16, 'gnd': 8, 'vcc': 16, 'pinoutSource': 'a data sheet'},
                     {'ref': 'IC3', 'part': '74LS00', 'pins': 14, 'gnd': 7, 'vcc': 14, 'pinoutSource': 'a data sheet'},
                     {'ref': 'IC4', 'part': 'LM324', 'excludedWhy': 'not a logic IC'}]}
    chips, excluded = board_trace.chip_pins(reg, table, 0.0, 0.0)
    assert [c['ref'] for c in chips] == ['IC1']
    gnd, vcc = chips[0]['gnd'], chips[0]['vcc']
    assert np.allclose(gnd, ((10.0 + 2.54 * 6) * R - 0.5, 20.0 * R - 0.5))
    assert np.allclose(vcc, (10.0 * R - 0.5, 27.62 * R - 0.5))
    why = dict(excluded)
    assert set(why) == {'IC2', 'IC3', 'IC4'}
    assert 'pins' in why['IC2'] and 'footprint' in why['IC3'] and why['IC4'] == 'not a logic IC'


def test_i5_is_registered_chip_by_chip_held_out():
    # four chips' corner pins, seen through a known homography with 0.1 mm of noise in I5's pixels
    Hm = np.array([[6.1, 0.2, 140.0], [-0.1, 6.0, 120.0], [1e-5, -2e-5, 1.0]])
    board, i5, chip = [], [], []
    rng = np.random.default_rng(5)
    for k, (cx, cy) in enumerate(((40, 40), (250, 40), (40, 190), (250, 190), (150, 110))):
        for dx, dy in ((0, 0), (17.78, 0), (0, 7.62), (17.78, 7.62)):
            p = np.array([cx + dx, cy + dy], float)
            q = np.c_[p[None], 1.0] @ Hm.T
            board.append(p)
            i5.append(q[0, :2] / q[0, 2] + rng.normal(0, 0.6, 2))
            chip.append(f'IC{k}')
    r = board_trace.register_i5(np.array(i5), np.array(board), chip)
    assert r['chips'] == 5 and r['points'] == 20
    assert r['heldOutMm']['median'] < 0.5
    assert r['folds'] == 'each chip held out in turn'
    # a chip whose marks are 3 mm off is caught held out, not fitted away
    bad = np.array(i5)
    bad[16:] += 18.0
    r = board_trace.register_i5(bad, np.array(board), chip)
    assert r['heldOutMm']['max'] > 2.0
    # held out together, the chip's four pins all read the whole shift and more (about 4 mm); held out one at a
    # time, the other three would pull the fit their way and each would read about 3 mm
    assert min(e for _, e in r['heldOutEach'][16:]) > 3.5


def test_the_top_face_is_compared_with_i5_only_where_i5_shows_the_board():
    a = np.zeros((50, 50), bool)
    b = np.zeros((50, 50), bool)
    shows = np.zeros((50, 50), bool)
    a[10:20, :] = True
    b[12:22, :] = True
    shows[:, :25] = True
    b[30:40, 25:] = True            # copper where I5 does not show the board does not count
    r = board_trace.iou_where(a, b, shows)
    assert r['iou'] == pytest.approx(8 / 12)
    assert r['comparedPx'] == 50 * 25


def test_i5_shows_the_board_only_where_its_lacquer_is_seen_and_finds_the_copper_there():
    # I5's own colours (board_trace.py's I5 probes): teal laminate, cyan copper, a grey chip, the table's wood
    LAM5, COP5, BODY, WOOD, LEG = (14, 69, 76), (14, 197, 190), (86, 86, 93), (255, 206, 164), (224, 220, 208)
    h, w = 300, 400
    img = np.empty((h, w, 3), np.float32)
    img[:] = LAM5
    img[:, :30] = WOOD
    truth = np.zeros((h, w), bool)
    for y in range(40, 280, 12):
        truth[y:y + 3, 30:] = True
    img[truth] = COP5
    img[100:200, 150:250] = BODY
    leg = np.zeros((h, w), bool)
    for y in range(105, 200, 14):
        leg[y:y + 5, 140:150] = True
    img[leg] = LEG
    rng = np.random.default_rng(9)
    img = np.clip(cv2.GaussianBlur(img + rng.normal(0, 3, img.shape), (0, 0), 0.6), 0, 255).astype(np.uint8)
    shows, copper, info = board_trace.i5_trace(img)
    assert not shows[:, :30].any(), 'the table is not the board'
    assert not shows[100:200, 150:250].any(), 'a chip is not the board'
    assert not shows[leg].any(), 'nor are its legs'
    assert shows[20:90, 60:380].mean() > 0.9
    where = shows.copy()
    assert board_trace.iou_where(copper, truth, where)['iou'] >= 0.8


def test_the_component_side_is_drawn_into_i5_through_the_fit():
    # a fit that is a pure scale and shift: I5 pixel (u, v) is board mm (u / 5 + 1, v / 5 + 2)
    fit = {'params': {'H': [[0.2, 0.0, 1.0], [0.0, 0.2, 2.0], [0.0, 0.0, 1.0]]}}
    top = np.zeros((40 * R, 50 * R), bool)
    top[int(10 * R):int(12 * R), :] = True            # board y 10 to 12 mm
    m, ok = board_trace.top_in_i5(top, 0.0, 0.0, fit, (100, 120))
    # I5 rows v with board y = v / 5 + 2 in [10, 12): v in [40, 50)
    assert m[41:49, 5:].all() and not m[:39].any() and not m[51:].any()
    assert ok.all()
