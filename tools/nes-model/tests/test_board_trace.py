"""The copper on both faces, the print, and the checks on them
(board_trace.py), on a made-up board.

    /tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q

The made-up board is drawn as the NES board's bare scans show it, already in
the board frame at 12 pixels per millimetre: copper under the green lacquer a
lighter green than the bare laminate under it (the colours are the median
sRGB of a laminate box and a copper box on I1-front, read on 5 October 2026),
tinned pads and a tinned plane in grey, drills dark, the print white, a
sticker cream; the light falls off by 15 per cent across the board, each
channel carries noise and the whole is blurred a little, as a scanner does.

Two nets are drawn: GND, three pads joined by tracks on the top face and,
through a via, a track on the bottom face; VCC, two pads joined by a track
0.4 mm from GND's, so the gap between them must be found. A white print line
crosses both nets' tracks, and the copper under it must be recovered; a
printed block 3 mm wide crosses another track, too wide to recover. A drawn
short joins the two nets.
"""
import math
import os
import sys

import cv2
import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import board_trace  # noqa: E402

R = board_trace.R
W_MM, H_MM = 60.0, 40.0
W, H = int(W_MM * R), int(H_MM * R)

LAMINATE, COPPER = (1, 15, 3), (1, 32, 7)       # I1-front's laminate and copper boxes, median sRGB
TIN, DRILL, PRINT, STICKER = (88, 90, 86), (8, 8, 8), (200, 205, 200), (205, 195, 160)
NOISE = 4.0

PAD_R, DRILL_R = 0.8, 0.4
GND_PADS = [(8.0, 10.0), (30.0, 10.0), (50.0, 30.0)]
VCC_PADS = [(8.0, 14.0), (30.0, 14.0)]
VIA = (40.0, 10.0)
PARALLEL = (33.0, 33.75, 34.5, 35.25)    # 9 pixels apart
# A row of pads on the solder side, 2.54 mm apart, each with the lacquer's bright rim round it, which on the
# scans joins neighbouring pads (sRGB from I1-back between U6's pins); a track leaves the first pad upwards
# and another runs between the second and third
ROW = [(10.0 + 2.54 * k, 5.0) for k in range(4)]
RIM, RIM_MM = (6, 60, 40), 0.45
BETWEEN_X = 10.0 + 2.54 * 1.5


def px(p):
    return int(round(p[0] * R)), int(round(p[1] * R))


def line(mask, a, b, width_mm):
    cv2.line(mask, px(a), px(b), 1, max(1, int(round(width_mm * R))), cv2.LINE_8)


def draw(short=False):
    """(top RGB, bottom RGB, truths): the faces in the board frame, with the
    true copper of each face, the true print and the pads."""
    top, bot = np.zeros((H, W), np.uint8), np.zeros((H, W), np.uint8)
    tin = np.zeros((H, W), np.uint8)
    # GND on the top face: pad to pad along y = 10 to the via; VCC beside it
    line(top, GND_PADS[0], GND_PADS[1], 0.6)
    line(top, GND_PADS[1], VIA, 0.6)
    line(top, VCC_PADS[0], VCC_PADS[1], 0.6)
    # Bottom: the via down to the third GND pad
    line(bot, VIA, (40.0, 30.0), 0.8)
    line(bot, (40.0, 30.0), GND_PADS[2], 0.8)
    line(bot, ROW[0], (ROW[0][0], 1.0), 0.4)
    line(bot, (BETWEEN_X, 1.5), (BETWEEN_X, 9.0), 0.3)
    # A copper pour on the top, joined to nothing, and a lone track under a wide printed block
    cv2.rectangle(top, px((40.0, 18.0)), px((55.0, 25.0)), 1, -1)
    line(top, (10.0, 30.0), (30.0, 30.0), 0.5)
    # Four tracks side by side, 6 pixels (0.5 mm) wide with 3 pixels (0.25 mm) between them, under a print
    # line across them: I1-front's narrowest gaps, between the wide tracks under U6, are 2 to 4 pixels
    for x in PARALLEL:
        top[px((0, 27.0))[1]:px((0, 34.0))[1], px((x, 0))[0] - 3:px((x, 0))[0] + 3] = 1
    # A tinned plane along the bottom edge of the top face
    cv2.rectangle(tin, px((0.0, 36.0)), px((W_MM, H_MM)), 1, -1)
    if short:
        line(top, (20.0, 10.0), (20.0, 14.0), 0.5)
    pads = []
    for p in GND_PADS + VCC_PADS + [VIA]:
        r = PAD_R if p != VIA else 0.5
        for m in (top, bot):
            cv2.circle(m, px(p), int(round(r * R)), 1, -1)
        pads.append((p[0], p[1], r))
    # The print: a thin line across both nets' tracks, a 3 mm block across the lone track, a label, and a sticker
    printed = np.zeros((H, W), np.uint8)
    line(printed, (15.0, 7.0), (15.0, 17.0), 0.3)
    line(printed, (25.0, 7.0), (25.0, 17.0), 0.3)
    line(printed, (32.0, 30.5), (37.0, 30.5), 0.3)
    cv2.rectangle(printed, px((18.5, 28.0)), px((21.5, 32.0)), 1, -1)
    for k in range(4):
        line(printed, (44.0 + k * 1.5, 4.0), (44.5 + k * 1.5, 6.0), 0.25)
    sticker = np.zeros((H, W), bool)
    sticker[px((2.0, 20.0))[1]:px((2.0, 26.0))[1], px((2.0, 20.0))[0]:px((14.0, 20.0))[0]] = True

    def face(copper, with_print):
        img = np.empty((H, W, 3), np.float32)
        img[:] = LAMINATE
        img[copper > 0] = COPPER
        if with_print:                       # the tinned plane is on the component side
            img[tin > 0] = TIN
        if not with_print:
            for x, y in ROW:
                cv2.circle(img, px((x, y)), int(round((PAD_R + RIM_MM) * R)), RIM, -1)
            img[bot > 0] = COPPER
        for x, y, r in pads + ([(x, y, PAD_R) for x, y in ROW] if not with_print else []):
            cv2.circle(img, px((x, y)), int(round(r * R)), TIN, -1)
            cv2.circle(img, px((x, y)), int(round((DRILL_R if r > 0.6 else 0.25) * R)), DRILL, -1)
        if with_print:
            img[printed > 0] = PRINT
            img[sticker] = STICKER
        light = np.linspace(1.0, 0.85, W, dtype=np.float32)[None, :, None]
        rng = np.random.default_rng(7 if with_print else 8)
        img = img * light + rng.normal(0, NOISE, img.shape)
        img = cv2.GaussianBlur(img, (0, 0), 0.7)
        return np.clip(img, 0, 255).astype(np.uint8)

    copper_top = (top > 0) | (tin > 0)
    copper_bot = bot > 0
    for x, y, r in pads:
        for m in (copper_top, copper_bot):
            cv2.circle(m.view(np.uint8), px((x, y)), int(round(r * R)), 1, -1)
    for x, y in ROW:
        cv2.circle(copper_bot.view(np.uint8), px((x, y)), int(round(PAD_R * R)), 1, -1)
    return face(top, True), face(bot, False), {
        'top': copper_top, 'bottom': copper_bot, 'print': printed > 0, 'sticker': sticker, 'pads': pads}


def iou(a, b, where):
    a, b = a & where, b & where
    return (a & b).sum() / max((a | b).sum(), 1)


@pytest.fixture(scope='module')
def traced():
    top, bot, truth = draw()
    inside = np.ones((H, W), bool)
    t = board_trace.trace_face(top, inside, pads=truth['pads'], hidden=truth['sticker'], side='top')
    b = board_trace.trace_face(bot, inside, pads=truth['pads'], side='bottom')
    return t, b, truth


def test_the_copper_comes_back_on_both_faces(traced):
    t, b, truth = traced
    # away from the print and the sticker, which hide the board
    seen = ~cv2.dilate(truth['print'].astype(np.uint8), np.ones((5, 5), np.uint8)).astype(bool) & ~truth['sticker']
    assert iou(t['copper'], truth['top'], seen) >= 0.95
    assert iou(b['copper'], truth['bottom'], np.ones_like(seen)) >= 0.95


def test_the_print_is_traced_apart_from_the_copper(traced):
    t, b, truth = traced
    # every printed pixel is in the print's mask, and the mask, grown on purpose to take the strokes' blurred
    # edges, reaches no further than 0.25 mm from the print
    assert t['print'][truth['print']].mean() >= 0.98
    near = cv2.dilate(truth['print'].astype(np.uint8), board_trace.disc(0.25 * R)) > 0
    assert near[t['print']].mean() >= 0.98
    # the print's mask takes the strokes' blurred edges, but not the pads, the tin or the sticker
    assert not (t['print'] & truth['sticker']).any()
    for x, y, r in truth['pads']:
        assert not t['print'][int(round(y * R)), int(round(x * R))]
    assert not b['print'].any(), 'the solder side carries no print'
    assert t['hidden'][truth['sticker']].all()
    assert not t['copper'][truth['sticker']].any()


def test_copper_under_thin_print_is_recovered_only_where_it_continues_in_line(traced):
    t, _, truth = traced
    # the 0.3 mm print lines cross the GND and VCC tracks at x = 15 and 25 mm: recovered
    for x in (15.0, 25.0):
        for y in (10.0, 14.0):
            assert t['copper'][int(y * R), int(x * R)], (x, y)
            assert t['recovered'][int(y * R), int(x * R)], (x, y)
    # off the tracks, the print line hides laminate: nothing recovered there
    assert not t['recovered'][int(12.0 * R), int(15.0 * R)]
    assert not t['recovered'][int(16.5 * R), int(25.0 * R)]
    # side by side tracks under a print line across them: each track is recovered straight across the line, and
    # the gaps between them are not, though a slanting line from a gap reaches a track on both sides
    for x in PARALLEL:
        assert t['recovered'][int(round(30.5 * R)), int(round(x * R))], x
    for x in PARALLEL[:-1]:
        assert not t['recovered'][int(round(30.5 * R)), int(round(x * R)) + 4], x
        assert not t['copper'][int(round(29.0 * R)), int(round(x * R)) + 4], x
    # and off the print the four stay four pieces: no gap is closed
    win = t['copper'][int(27.5 * R):int(34 * R), int(32 * R):int(36.5 * R)]
    assert cv2.connectedComponents(win.astype(np.uint8), connectivity=8)[0] - 1 == 4
    # the 3 mm block is too wide: the lone track under it is not recovered
    assert not t['recovered'][int(30.0 * R), int(20.0 * R)]
    assert t['recovered'].sum() <= t['print'].sum()


def test_the_nets_are_found_apart_and_joined_through_the_via(traced):
    t, b, truth = traced
    chips = [{'ref': 'A', 'gnd': px(GND_PADS[0]), 'vcc': px(VCC_PADS[0])},
             {'ref': 'B', 'gnd': px(GND_PADS[1]), 'vcc': px(VCC_PADS[1])},
             {'ref': 'C', 'gnd': px(GND_PADS[2]), 'vcc': None}]
    drills = [px(VIA)]
    n = board_trace.nets_check(t['copper'], b['copper'], drills, chips)
    assert n['gnd'] == {'pins': 3, 'inLargest': 3}
    assert n['vcc'] == {'pins': 2, 'inLargest': 2}
    assert n['touching'] is False
    # each pin's net is written as a plain label, the face and the piece's number there
    import re
    for k in ('gnd', 'vcc'):
        for ref, net in n['pinNets'][k]:
            assert net is None or re.fullmatch(r'(top|bottom):[0-9]+', net), (ref, net)
    # without the via, the third GND pad is on its own
    n = board_trace.nets_check(t['copper'], b['copper'], [], chips)
    assert n['gnd'] == {'pins': 3, 'inLargest': 2}


def test_a_drawn_short_is_caught():
    top, bot, truth = draw(short=True)
    inside = np.ones((H, W), bool)
    t = board_trace.trace_face(top, inside, pads=truth['pads'], hidden=truth['sticker'], side='top')
    b = board_trace.trace_face(bot, inside, pads=truth['pads'], side='bottom')
    chips = [{'ref': 'A', 'gnd': px(GND_PADS[0]), 'vcc': px(VCC_PADS[0])},
             {'ref': 'B', 'gnd': px(GND_PADS[1]), 'vcc': px(VCC_PADS[1])}]
    assert board_trace.nets_check(t['copper'], b['copper'], [px(VIA)], chips)['touching'] is True


def test_the_verdicts_are_the_plans():
    v = board_trace.verdicts
    good = {'coverage': {'top': 0.3, 'bottom': 0.2}, 'drillsInCopper': {'top': 0.97, 'bottom': 0.95},
            'nets': {'chips': 8, 'gnd': {'pins': 8, 'inLargest': 8}, 'vcc': {'pins': 8, 'inLargest': 8}, 'touching': False}}
    assert v(good) == {'coverage': 'pass', 'drillsInCopper': 'pass', 'nets': 'pass'}
    bad = dict(good, coverage={'top': 0.51, 'bottom': 0.2})
    assert v(bad)['coverage'] == 'fail'
    assert v(dict(good, coverage={'top': 0.3, 'bottom': 0.09}))['coverage'] == 'fail'
    assert v(dict(good, drillsInCopper={'top': 0.94, 'bottom': 0.99}))['drillsInCopper'] == 'fail'
    assert v(dict(good, nets=dict(good['nets'], chips=7)))['nets'] == 'fail'
    assert v(dict(good, nets=dict(good['nets'], touching=True)))['nets'] == 'fail'
    assert v(dict(good, nets=dict(good['nets'], gnd={'pins': 10, 'inLargest': 8})))['nets'] == 'fail'
    assert v(dict(good, nets=dict(good['nets'], vcc={'pins': 10, 'inLargest': 9})))['nets'] == 'pass'


def test_a_drill_is_in_copper_when_its_ring_is():
    top, _, truth = draw()
    copper = truth['top']
    drills = [(x, y, 2 * DRILL_R) for x, y, r in truth['pads'] if r > 0.6] + [(5.0, 5.0, 0.8)]
    got = board_trace.drills_in_copper(copper, drills)
    assert got == [True] * (len(drills) - 1) + [False]


def test_the_solder_sides_affine_comes_back_from_its_corners():
    M = np.array([[-0.0846, 0.0003, 197.8], [0.0002, 0.0847, -4.8]])
    px_ = [[0, 0], [2376, 0], [0, 1492], [2376, 1492]]
    mm = (np.c_[np.array(px_, float), np.ones(4)] @ M.T).tolist()
    to_mm, to_px = board_trace.affine_from_corners({'model': 'affine', 'cornersPx': px_, 'cornersMm': mm})
    q = np.array([[100.0, 200.0], [2000.0, 1400.0]])
    assert np.allclose(to_mm(q), np.c_[q, np.ones(2)] @ M.T, atol=1e-9)
    assert np.allclose(to_px(to_mm(q)), q, atol=1e-6)
    bent = {'model': 'affine', 'cornersPx': px_, 'cornersMm': [mm[0], mm[1], mm[2], [mm[3][0] + 0.01, mm[3][1]]]}
    with pytest.raises(ValueError):
        board_trace.affine_from_corners(bent)


def test_the_map_puts_each_layer_in_its_channel_at_its_resolution():
    top = np.zeros((H, W), bool)
    bot = np.zeros((H, W), bool)
    pr = np.zeros((H, W), bool)
    top[:, : W // 2] = True
    bot[: H // 2, :] = True
    pr[H // 2:, W // 2:] = True
    inside = np.ones((H, W), bool)
    inside[:12, :12] = False
    m = board_trace.make_map(top, bot, pr, inside, ppm=6)
    assert m.shape == (H_MM * 6, W_MM * 6, 3)
    assert m.dtype == np.uint8 and set(np.unique(m)) <= {0, 255}
    assert m[7:, : m.shape[1] // 2 - 1, 0].min() == 255 and m[:, m.shape[1] // 2 + 1:, 0].max() == 0
    assert m[: m.shape[0] // 2 - 1, 6:, 1].min() == 255
    assert m[m.shape[0] // 2 + 1:, m.shape[1] // 2 + 1:, 2].min() == 255
    assert m[:5, :5].max() == 0, 'nothing outside the board'


def test_the_lacquers_bright_rim_round_a_pad_does_not_join_its_neighbours(traced):
    _, b, truth = traced
    lab = cv2.connectedComponents(b['copper'].astype(np.uint8), connectivity=8)[1]
    at = lambda x, y: lab[int(round(y * R)), int(round(x * R))]  # noqa: E731
    pads = [at(x, y) for x, y in ROW]
    assert all(pads) and len(set(pads)) == 4, 'each pad of the row is its own piece'
    assert at(ROW[0][0], 1.6) == pads[0], 'the track leaving the first pad stays joined to it'
    assert at(BETWEEN_X, 1.8) == at(BETWEEN_X, 8.6) != 0, 'the track between two pads runs on past them'
    assert at(BETWEEN_X, 5.0) == at(BETWEEN_X, 1.8)
    assert at(BETWEEN_X, 1.8) not in pads
