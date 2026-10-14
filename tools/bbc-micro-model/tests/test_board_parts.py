"""Every part's place and identity on the Issue 7 board (board_parts.py), on
made-up footprints, made-up manual positions and made-up markings.

    ~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q

The footprints are drawn as task 3's registration.json holds them: pads in
board millimetres (x to the right, y towards the front), in pin order, pin 1
first. Every DIP on the board lies with pin 1 at the rear left and pin 1's
row running towards the front (rotation 90 degrees: from x towards y), the
pins counted down pin 1's row and back up the other; two lie across
(rotation 0). The Service Manual's places are millimetres from the board's
SW corner, the front left as the component side is seen with the rear
connectors at the far edge, so its Y runs towards the rear.
"""
import math
import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import board_parts  # noqa: E402


def dip_pads(pin1, rotation_deg, pins, spacing):
    """A DIP's pads in pin order: pin 1 at `pin1`, pin 1's row running along
    `rotation_deg` (from x towards y), the other row `spacing` mm to its left
    as the row runs (for rotation 90, at larger x), counted back."""
    half = pins // 2
    t = math.radians(rotation_deg)
    u = np.array([math.cos(t), math.sin(t)])
    v = np.array([-math.sin(t), math.cos(t)])     # for rotation 90: (-1, 0), so the other row is at -v
    p1 = np.array(pin1, float)
    first = [p1 + k * 2.54 * u for k in range(half)]
    second = [p1 + (half - 1 - k) * 2.54 * u - spacing * v for k in range(half)]
    return [q.tolist() for q in first + second]


# --- the place of a footprint ---------------------------------------------------------

@pytest.mark.parametrize('rotation', [90.0, 0.0, -90.0, 180.0, 30.0])
def test_place_is_the_pads_centre_and_the_turn_from_pin_1_along_its_row(rotation):
    pads = dip_pads((50.0, 40.0), rotation, 16, 7.62)
    got = board_parts.place(pads, 16)
    c = np.mean(pads, 0)
    assert got['x'] == pytest.approx(c[0], abs=1e-9) and got['y'] == pytest.approx(c[1], abs=1e-9)
    assert math.cos(math.radians(got['rotation'] - rotation)) == pytest.approx(1.0, abs=1e-12)


def test_a_dip_lying_down_the_board_with_pin_1_at_the_rear_left_turns_90_degrees():
    pads = dip_pads((119.4, 157.0), 90.0, 14, 7.62)
    got = board_parts.place(pads, 14)
    assert got['rotation'] == pytest.approx(90.0, abs=1e-9)
    assert got['x'] == pytest.approx(119.4 + 3.81, abs=1e-9)          # the other row is to the right
    assert got['y'] == pytest.approx(157.0 + 3 * 2.54, abs=1e-9)


def table(**pins):
    return {'ics': [{'ref': r, 'part': '74LS00', 'pins': n} for r, n in pins.items()]}


def fp(ref, pads):
    return {'ref': ref, 'pads': pads}


def test_a_footprint_whose_pad_count_is_not_its_parts_is_reported_not_placed():
    good = dip_pads((20.0, 20.0), 90.0, 16, 7.62)
    short = dip_pads((60.0, 20.0), 90.0, 16, 7.62)[:15]
    placed, problems = board_parts.place_ics({'IC3': fp('IC3', good), 'IC7': fp('IC7', short)}, table(IC3=16, IC7=16))
    assert [p['ref'] for p in placed] == ['IC3']
    assert placed[0]['pins'] == 16 and placed[0]['padCount'] == 16
    assert problems == [{'ref': 'IC7', 'problem': 'its footprint has 15 pads, its part 16 pins: not placed'}]


def test_a_part_with_no_footprint_is_reported():
    placed, problems = board_parts.place_ics({'IC3': fp('IC3', dip_pads((20.0, 20.0), 90.0, 16, 7.62))}, table(IC3=16, IC9=14))
    assert [p['ref'] for p in placed] == ['IC3']
    assert problems == [{'ref': 'IC9', 'problem': 'no footprint: not placed'}]


def test_an_eight_pin_dip_marked_by_hand_takes_its_pads_and_infers_a_missing_one_from_the_pitch():
    pads = dip_pads((50.5, 196.5), 90.0, 8, 7.62)
    board_pads = [{'x': x, 'y': y} for x, y in pads]
    mark = {'ref': 'IC19', 'pads': [0, 1, None, 3, 4, 5, 6, 7]}
    got = board_parts.marked_footprint(mark, board_pads)
    assert got['ref'] == 'IC19' and got['padsInferred'] == [3]
    assert np.allclose(got['pads'], pads, atol=1e-9)
    # an inferred pad sits on the pitch between its neighbours in the row
    assert got['pads'][2] == pytest.approx(pads[2], abs=1e-9)


# --- the manual's places ----------------------------------------------------------------

def board_point(rotation=90.0, pins=16, spacing=7.62, pin1=(119.4, 186.2)):
    pads = dip_pads(pin1, rotation, pins, spacing)
    return pads, {'ref': 'X', 'pins': pins, 'spacing': spacing, **board_parts.place(pads, pins)}


def test_the_conventions_points_on_a_dip():
    pads, p = board_point()
    c = board_parts.convention_point(p, pads, 'centre')
    assert c == pytest.approx(np.mean(pads, 0).tolist())
    assert board_parts.convention_point(p, pads, 'pin1') == pytest.approx(pads[0])
    assert board_parts.convention_point(p, pads, 'pinHalf') == pytest.approx(pads[7])
    # the body's corner beyond pin N/2, on pin 1's side: out along the row by the end overhang, out across by the side overhang
    l, w = board_parts.body_size(16, 7.62)
    corner = board_parts.convention_point(p, pads, 'bodyCorner')
    assert corner[1] == pytest.approx(pads[7][1] + (l - 7 * 2.54) / 2)
    assert corner[0] == pytest.approx(pads[7][0] - (w - 7.62) / 2)


def manual_from(points, scale=1.0, turn_deg=0.5, shift=(-3.0, 231.0)):
    """Manual (X, Y), Y towards the rear, from board points: the board's y
    flipped, then a small similarity, as the manual's drawing might be."""
    t = math.radians(turn_deg)
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]]) * scale
    out = {}
    for ref, (x, y) in points.items():
        q = R @ np.array([x, -y]) + np.array(shift)
        out[ref] = q.tolist()
    return out


def grid_points():
    return {f'IC{k}': (20.0 + 30.0 * (k % 9), 15.0 + 40.0 * (k // 9)) for k in range(1, 40)}


def test_the_manual_check_passes_on_places_within_its_limit_and_flips_y():
    pts = grid_points()
    manual = manual_from(pts)
    rng = np.random.default_rng(1)
    manual = {r: (np.array(v) + rng.normal(0, 1.5, 2)).tolist() for r, v in manual.items()}
    got = board_parts.manual_check(pts, manual, limit=10.0)
    assert got['verdict'] == 'pass'
    assert got['maxMm'] < 7.0
    assert got['scale'] == pytest.approx(1.0, abs=0.02)


def test_the_manual_check_finds_two_chips_swapped():
    pts = grid_points()
    manual = manual_from(pts)
    manual['IC3'], manual['IC4'] = manual['IC4'], manual['IC3']          # 30 mm apart
    got = board_parts.manual_check(pts, manual, limit=10.0)
    assert got['verdict'] == 'fail'
    worst = sorted(got['perPart'], key=lambda r: -got['perPart'][r]['offMm'])[:2]
    assert sorted(worst) == ['IC3', 'IC4']
    assert all(got['perPart'][r]['offMm'] > 20 for r in worst)


def test_the_manual_check_fails_a_swap_of_two_chips_only_12_mm_apart():
    pts = grid_points()
    pts['IC40'] = (pts['IC3'][0], pts['IC3'][1] + 12.0)
    manual = manual_from(pts)
    manual['IC3'], manual['IC40'] = manual['IC40'], manual['IC3']
    assert board_parts.manual_check(pts, manual, limit=10.0)['verdict'] == 'fail'


def test_the_manual_check_s_own_limit_is_the_plan_s_ten_millimetres():
    # Added in task 10 (14 October 2026): with the limit loosened to 20 mm in the module, every test passed, each
    # passing its own limit; the run uses the module's. A swap 12 mm apart fails at it, and its limit is 10 mm.
    pts = grid_points()
    pts['IC40'] = (pts['IC3'][0], pts['IC3'][1] + 12.0)
    manual = manual_from(pts)
    manual['IC3'], manual['IC40'] = manual['IC40'], manual['IC3']
    got = board_parts.manual_check(pts, manual)
    assert got['limitMm'] == 10.0
    assert got['verdict'] == 'fail'


@pytest.mark.parametrize('truth', board_parts.CONVENTIONS)
def test_settling_the_convention_finds_the_one_the_manual_used(truth):
    # IC51 and IC52 (28-pin, 600 mil), IC20 (16-pin) and IC21 (14-pin), placed as on the board
    parts = {'IC51': ((213.4, 170.8), 28, 15.24), 'IC52': ((232.5, 170.8), 28, 15.24),
             'IC20': ((119.3, 186.2), 16, 7.62), 'IC21': ((119.4, 157.0), 14, 7.62)}
    footprints, places = {}, {}
    for ref, (p1, n, s) in parts.items():
        pads = dip_pads(p1, 90.0, n, s)
        footprints[ref] = pads
        places[ref] = {'ref': ref, 'pins': n, 'spacing': s, **board_parts.place(pads, n)}
    pts = {r: board_parts.convention_point(places[r], footprints[r], truth) for r in parts}
    manual = manual_from(pts)
    got = board_parts.settle_convention(places, footprints, manual)
    assert got['chosen'] == truth
    assert got['byConvention'][truth]['maxMm'] < 1e-6


# --- identities ---------------------------------------------------------------------

@pytest.mark.parametrize('marking,part,verdict', [
    ('SN74LS00N', '74LS00', 'agrees'),
    ('DM74LS00N 8306', '74LS00', 'agrees'),
    ('SN74S00N', '74LS00', 'disagrees'),
    ('SN74LS00N', '74S00', 'disagrees'),
    ('SN74LS30N', '74LS20', 'disagrees'),
    ('74LS1630', '74LS163', 'disagrees'),
    ('SN74LS163AN', '74LS163', 'agrees'),
    ('R6502AP R6502-13', '6502A', 'agrees'),
    ('R6502P', '6502A', 'disagrees'),
    ('NEC D7002C', 'uPD7002', 'agrees'),
    ('MC68B50P', '6850', 'agrees'),
    ('F6850P', '6850', 'agrees'),
    ('MC6854P', '68B54', 'disagrees'),
    ('CD4013BE', '4013', 'agrees'),
    ('MC14013BCP', '4013', 'agrees'),
    ('SAA 5050', 'SAA5050', 'agrees'),
    ('FERRANTI ULA 2C199E-7', '2C199', 'agrees'),
    ('SN7438N', '7438', 'agrees'),
    ('SN74LS38N', '7438', 'disagrees'),
])
def test_a_marking_agrees_with_the_tables_part_only_by_the_fixed_rule(marking, part, verdict):
    assert board_parts.identity(marking, {'part': part})['verdict'] == verdict


def test_the_tables_earlier_part_is_recorded_as_such_not_as_a_disagreement():
    ic = {'part': '74S00', 'tableSaysPreviously': '74LS00'}
    assert board_parts.identity('SN74LS00N', ic)['verdict'] == 'the table\'s earlier part'
    assert board_parts.identity('SN74S00N', ic)['verdict'] == 'agrees'


def test_a_rom_socket_is_not_compared_and_an_unread_marking_is_not_judged():
    assert board_parts.identity('M27C64A', {'ref': 'IC88', 'part': '2764'})['verdict'] == 'not compared'
    assert board_parts.identity(None, {'ref': 'IC3', 'part': '6522'})['verdict'] == 'unread'


def test_the_identity_row_fails_on_one_disagreement_and_on_too_few_read():
    agree = {'verdict': 'agrees'}
    rows = {f'IC{k}': agree for k in range(1, 9)}
    assert board_parts.identity_row(rows, populated=8)['verdict'] == 'pass'
    rows['IC4'] = {'verdict': 'disagrees'}
    assert board_parts.identity_row(rows, populated=8)['verdict'] == 'fail'
    few = {f'IC{k}': agree for k in range(1, 6)}
    few.update({f'IC{k}': {'verdict': 'unread'} for k in range(6, 9)})
    assert board_parts.identity_row(few, populated=8)['verdict'] == 'not measured'      # 5 of 8 read, under three quarters


# --- bodies and the outline -----------------------------------------------------------

def test_bodies_that_touch_are_apart_and_bodies_that_cross_overlap():
    a = {'x': 10.0, 'y': 10.0, 'l': 19.3, 'w': 6.35, 'rotation': 90.0}
    b = dict(a, x=10.0 + 6.35)
    assert not board_parts.overlap(a, b)
    assert board_parts.overlap(a, dict(a, x=10.0 + 6.0))
    assert board_parts.overlap(a, dict(a, rotation=0.0))


def test_inside_the_outline():
    sq = [[0, 0], [100, 0], [100, 50], [0, 50]]
    assert board_parts.inside(sq, (50, 25))
    assert not board_parts.inside(sq, (101, 25))
    assert not board_parts.inside(sq, (100, 25))      # on an edge is out


# --- the passives -------------------------------------------------------------------

def test_two_lead_parts_are_pairs_of_free_pads_with_print_between_them():
    pads = {0: (10.0, 10.0), 1: (20.16, 10.0),       # a resistor's two pads, 10.16 mm apart, print between them
            2: (10.0, 20.0), 3: (15.08, 20.0),       # a capacitor's, 5.08 mm, print between
            4: (40.0, 10.0), 5: (45.08, 10.0),       # two pads with no print between them: not a part
            6: (40.0, 30.0), 7: (47.62, 30.0), 8: (43.81, 30.0)}   # print from 6 to 7, but pad 8 (a via) sits on the way
    printed = {(0, 1), (2, 3), (6, 7)}

    def share(a, b):
        return 0.95 if (a, b) in printed or (b, a) in printed else 0.0
    got = board_parts.two_lead_parts(pads, share)
    pairs = sorted(tuple(sorted(p['pads'])) for p in got)
    assert (0, 1) in pairs and (2, 3) in pairs
    assert (4, 5) not in pairs and (6, 7) not in pairs
    kinds = {tuple(sorted(p['pads'])): p['kind'] for p in got}
    assert kinds[(0, 1)] == 'axial' and kinds[(2, 3)] == 'radial'
    # each pad is in one part at most
    used = [i for p in got for i in p['pads']]
    assert len(used) == len(set(used))


def test_two_pads_of_one_row_of_pads_are_never_a_part():
    pads = {0: (10.0, 10.0), 1: (12.54, 10.0), 2: (15.08, 10.0)}      # a header's or a link's pins in a row
    share = lambda a, b: 0.95
    assert board_parts.two_lead_parts(pads, share, {0: 7, 1: 7, 2: 7}) == []
    assert len(board_parts.two_lead_parts(pads, share)) == 1


def test_print_share_along_a_segment():
    # a 16 px/mm board image: a printed line from (10, 10) to (20, 10) mm, broken for 1 mm in the middle
    img = np.zeros((400, 600), bool)
    img[158:163, 160:312] = True
    img[158:163, 232:248] = False
    share = board_parts.print_share(img, 16, (10.0, 10.0), (20.0, 10.0))
    assert share == pytest.approx((10 - 2 * board_parts.PAD_CLEAR_MM - 1.0) / (10 - 2 * board_parts.PAD_CLEAR_MM), abs=0.03)
    assert board_parts.print_share(img, 16, (10.0, 20.0), (20.0, 20.0)) == 0.0


# --- the module ---------------------------------------------------------------------

def test_the_module_is_generated_names_its_sources_and_holds_no_colour():
    model = {'board': {'width': 1}, 'ics': [], 'connectors': [], 'passives': [], 'others': [],
             'heights': {'board': {'value': 1.6, 'measured': False, 'what': 'typical'}}, 'sources': ['I1', 'I2', 'I5', 'D1']}
    text = board_parts.module_text(model)
    assert text.startswith('// GENERATED by tools/bbc-micro-model/board_parts.py')
    for name in ('BOARD', 'ICS', 'CONNECTORS', 'PASSIVES', 'OTHERS', 'HEIGHTS_TYPICAL'):
        assert f'export const {name} = ' in text
    assert 'I1, I2, I5, D1' in text
    assert '#' not in text.split('\n', 12)[-1]
