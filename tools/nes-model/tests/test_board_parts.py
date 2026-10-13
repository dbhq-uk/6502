"""Every part's place and identity, both consoles (board_parts.py), on
made-up footprints and made-up photographs.

    /tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q

The footprints are drawn as task 3's registration.json holds them: pads in
board millimetres, in pin order, pin 1 first. The photographs are drawn as
Evan-Amos's top and bottom views of the populated board show them: a green
board, a DIP's body near black with no colour, its leads light grey, and on
the solder side each pin a grey joint, light and dark, round its lead.
"""
import math
import os
import re
import sys

import cv2
import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import board_parts  # noqa: E402


def dip_pads(centre, rotation_deg, pins, spacing):
    """A DIP's pads in pin order, as task 3 numbers them: pin 1 at the lower
    left when the DIP lies along x (rotation 0), the pins counted along the
    lower row, then back along the upper row. y is down, as in the board
    frame, so the upper row is at smaller y."""
    half = pins // 2
    local = [((k - (half - 1) / 2) * 2.54, spacing / 2) for k in range(half)]
    local += [(((half - 1) - k - (half - 1) / 2) * 2.54, -spacing / 2) for k in range(half)]
    t = math.radians(rotation_deg)
    c, s = math.cos(t), math.sin(t)
    return [[centre[0] + c * x - s * y, centre[1] + s * x + c * y] for x, y in local]


# --- the place of a footprint ---------------------------------------------------------

@pytest.mark.parametrize('rotation', [0.0, 90.0, 180.0, -90.0, 30.0])
def test_place_is_the_pad_groups_centre_and_the_turn_from_pin_1(rotation):
    pads = dip_pads((50.0, 40.0), rotation, 16, 7.62)
    got = board_parts.place(pads, 16)
    assert got['x'] == pytest.approx(50.0, abs=1e-9)
    assert got['y'] == pytest.approx(40.0, abs=1e-9)
    assert got['rotation'] == pytest.approx(rotation if rotation != 180.0 else 180.0, abs=1e-9) or \
        got['rotation'] == pytest.approx(rotation - 360.0, abs=1e-9)


def test_place_does_not_move_with_pin_1_when_a_pad_is_far_off_the_middle():
    # the centre is the mean of all the pads, so one pad read 0.3 mm off moves it by 0.3 / 16
    pads = dip_pads((10.0, 10.0), 0.0, 16, 7.62)
    pads[3][1] += 0.3
    got = board_parts.place(pads, 16)
    assert got['y'] == pytest.approx(10.0 + 0.3 / 16, abs=1e-9)


def table(**pins):
    return {'ics': [{'ref': r, 'pins': n, 'package': f'DIP-{n} 300 mil'} for r, n in pins.items()]}


def footprint(ref, pads, pins=None):
    return {'ref': ref, 'kind': 'dip', 'pins': pins if pins is not None else len(pads), 'pads': pads}


def test_a_footprint_whose_pad_count_is_not_its_parts_is_reported_not_placed():
    good = dip_pads((20.0, 20.0), 0.0, 16, 7.62)
    short = dip_pads((60.0, 20.0), 0.0, 16, 7.62)[:15]
    fps = [footprint('U3', good), footprint('U7', short)]
    placed, problems = board_parts.place_ics(fps, table(U3=16, U7=16), use={})
    assert [p['ref'] for p in placed] == ['U3']
    assert placed[0]['pins'] == 16 and placed[0]['padCount'] == 16
    assert problems == [{'ref': 'U7', 'problem': 'its footprint has 15 pads, its part 16 pins: not placed'}]


def test_a_part_with_no_footprint_is_reported():
    fps = [footprint('U3', dip_pads((20.0, 20.0), 0.0, 16, 7.62))]
    placed, problems = board_parts.place_ics(fps, table(U3=16, U9=14), use={})
    assert [p['ref'] for p in placed] == ['U3']
    assert problems == [{'ref': 'U9', 'problem': 'no footprint named U9: not placed'}]


def test_the_footprint_a_part_sits_on_is_the_one_named_for_it():
    # U1's 300 mil alternate shares the 600 mil footprint's lower row; the part sits on the one `use` names
    six = dip_pads((40.0, 30.0), 0.0, 24, 15.24)
    three = dip_pads((40.0, 30.0 + (15.24 - 7.62) / 2), 0.0, 24, 7.62)
    fps = [footprint('U1', six), footprint('U1 (300 mil)', three)]
    placed, _ = board_parts.place_ics(fps, table(U1=24), use={'U1': 'U1 (300 mil)'})
    assert placed[0]['footprint'] == 'U1 (300 mil)'
    assert placed[0]['y'] == pytest.approx(30.0 + 3.81, abs=1e-9)


# --- the places against the KiCad redrawing's ----------------------------------------------

def similar(points, scale, turn_deg, shift):
    t = math.radians(turn_deg)
    R = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    return (np.asarray(points, float) @ R.T) * scale + np.asarray(shift, float)


PLACES = {'U1': (52.6, 92.9), 'U2': (105.9, 75.0), 'U3': (27.2, 47.2), 'U4': (108.4, 95.4), 'U5': (119.0, 56.0),
          'U6': (42.4, 68.7), 'U7': (136.4, 21.7), 'U8': (136.4, 34.4), 'U9': (159.3, 62.4), 'U10': (140.2, 80.0)}


def test_a_best_fit_similarity_to_the_kicad_places_passes_a_board_with_no_swap():
    refs = list(PLACES)
    scan = {r: PLACES[r] for r in refs}
    kicad_pts = similar([PLACES[r] for r in refs], 1.002, 0.4, (39.7, 24.4))
    rng = np.random.default_rng(1)
    kicad = {r: (kicad_pts[i] + rng.normal(0, 0.15, 2)).tolist() for i, r in enumerate(refs)}
    got = board_parts.kicad_check(scan, kicad, limit=3.0)
    assert got['verdict'] == 'pass'
    assert got['maxMm'] < 1.0
    assert got['scale'] == pytest.approx(1 / 1.002, abs=0.005)


def test_a_best_fit_similarity_to_the_kicad_places_finds_a_deliberate_swap():
    refs = list(PLACES)
    kicad_pts = similar([PLACES[r] for r in refs], 1.0, 0.0, (39.7, 24.4))
    kicad = {r: kicad_pts[i].tolist() for i, r in enumerate(refs)}
    scan = dict(PLACES)
    scan['U7'], scan['U8'] = PLACES['U8'], PLACES['U7']      # the two controller buffers, 12.7 mm apart
    got = board_parts.kicad_check(scan, kicad, limit=3.0)
    assert got['verdict'] == 'fail'
    over = {r for r, e in got['perPart'].items() if e['offMm'] > 3.0}
    assert over == {'U7', 'U8'}


# --- each photographed part on the scan's footprint of the same name -----------------------

def test_held_out_parts_find_a_part_moved_on_the_photograph():
    rng = np.random.default_rng(2)
    H = np.array([[19.8, 0.4, 300.0], [-0.3, 20.1, 150.0], [1e-5, -2e-5, 1.0]])
    pts = {}
    for r, (x, y) in PLACES.items():
        mm = [[x - 10, y], [x + 10, y], [x, y - 3], [x, y + 3]]
        px = board_parts.apply_h(H, mm) + rng.normal(0, 0.5, (4, 2))
        if r == 'U9':
            px = px + np.array([3.0 * 20, 0.0])      # U9 sits 3 mm to the right on this photograph
        pts[r] = {'px': px.tolist(), 'mm': mm}
    got = board_parts.held_out_parts(pts)
    # the moved part is the one over the limit; the others carry some of it, through their fits, which hold it
    assert got['U9']['heldOutMm'] > board_parts.SITS_MM
    assert all(got[r]['heldOutMm'] < board_parts.SITS_MM for r in PLACES if r != 'U9')
    assert max(got, key=lambda r: got[r]['heldOutMm']) == 'U9'


# --- inside the outline, and the bodies apart ----------------------------------------------

SQUARE = [[0, 0], [100, 0], [100, 50], [0, 50]]


def test_inside_the_outline():
    assert board_parts.inside(SQUARE, [10, 10])
    assert not board_parts.inside(SQUARE, [101, 10])
    assert not board_parts.inside(SQUARE, [50, -0.1])


def test_corners_of_a_turned_body():
    c = board_parts.body_corners({'x': 10, 'y': 20, 'l': 4, 'w': 2, 'rotation': 90})
    got = sorted((round(x, 9), round(y, 9)) for x, y in c)
    assert got == [(9, 18), (9, 22), (11, 18), (11, 22)]


def test_two_bodies_overlap_or_not():
    a = {'x': 10, 'y': 10, 'l': 20, 'w': 6, 'rotation': 0}
    assert board_parts.overlap(a, {'x': 10, 'y': 15.9, 'l': 20, 'w': 6, 'rotation': 0})
    assert not board_parts.overlap(a, {'x': 10, 'y': 16.1, 'l': 20, 'w': 6, 'rotation': 0})
    assert board_parts.overlap(a, {'x': 20.5, 'y': 10, 'l': 6, 'w': 2, 'rotation': 90})
    assert not board_parts.overlap(a, {'x': 21, 'y': 10, 'l': 6, 'w': 2, 'rotation': 90})   # touching


# --- the photographs: a DIP's midline on the top side, a joint on the solder side ---------------

GREEN, BODY, LEAD = (60, 120, 70), (45, 45, 45), (190, 190, 185)


def drawn_dip(img, centre, pins, spacing_px, pitch_px):
    """A DIP lying along x in a photograph from above: a near-black body and
    light leads standing out of both long sides."""
    cx, cy = centre
    half = pins // 2
    length = half * pitch_px
    body_w = spacing_px * 0.85
    cv2.rectangle(img, (int(cx - length / 2), int(cy - body_w / 2)), (int(cx + length / 2), int(cy + body_w / 2)), BODY, -1)
    for k in range(half):
        x = cx + (k - (half - 1) / 2) * pitch_px
        for side in (-1, 1):
            y0 = cy + side * body_w / 2
            y1 = cy + side * spacing_px / 2
            cv2.rectangle(img, (int(round(x - pitch_px * 0.25)), int(round(min(y0, y1)))),
                          (int(round(x + pitch_px * 0.25)), int(round(max(y0, y1)))), LEAD, -1)


def test_a_dips_midline_is_found_between_its_body_edges_at_its_lead_columns():
    img = np.zeros((400, 900, 3), np.uint8)
    img[:] = GREEN
    drawn_dip(img, (450.0, 200.0), 16, 152.0, 50.8)
    img = cv2.GaussianBlur(img, (3, 3), 0.8)
    pins = list(range(1, 17))
    # the predicted pin places, 6 px off, as a fit from other parts would give them
    pred_bottom = [[450 + (k - 3.5) * 50.8 + 6, 200 + 76 + 4] for k in range(8)]
    pred_top = [[450 + (k - 3.5) * 50.8 + 6, 200 - 76 + 4] for k in range(8)]   # pins 16 down to 9, left to right
    got = board_parts.dip_midline(img, pred_bottom, pred_top)
    assert len(got) == 8
    for k, (x, y) in enumerate(got):
        assert x == pytest.approx(450 + (k - 3.5) * 50.8, abs=1.0)
        assert y == pytest.approx(200.0, abs=1.0)


def test_a_solder_joint_is_found_by_its_colourless_disc():
    img = np.zeros((120, 120, 3), np.uint8)
    img[:] = GREEN
    cv2.circle(img, (61, 57), 14, (110, 110, 110), -1)
    cv2.circle(img, (66, 52), 5, (240, 240, 240), -1)         # a highlight off the middle
    got = board_parts.joint(img, 55, 62)
    assert got == pytest.approx([61, 57], abs=0.6)
    assert board_parts.joint(np.full((120, 120, 3), GREEN, np.uint8), 60, 60) is None


# --- the module the site reads ------------------------------------------------------------

def test_the_module_says_it_is_generated_names_its_sources_and_has_no_colour():
    parts = {
        'board': {'width': 195.9, 'depth': 119.4, 'thickness': 1.6, 'outline': [[0, 0], [1, 0], [1, 1]], 'holes': [{'x': 1, 'y': 2, 'd': 3.6}]},
        'ics': [{'ref': 'U6', 'role': 'r', 'pins': 40, 'x': 1, 'y': 2, 'w': 13.8, 'l': 52.1, 'rotation': 0, 'height': 4,
                 'parts': {'ntsc': 'RP2A03G', 'pal': 'RP2A07A'}, 'chip': 'apu', 'always': None}],
        'connectors': [], 'passives': [], 'others': {'ntsc': [], 'pal': []},
        'heights': {'board': 1.6, 'dip': {'value': 4.0, 'measured': False}},
        'sources': ['I1-front', 'I3', 'I4'],
    }
    text = board_parts.module_text(parts)
    assert text.startswith('// GENERATED by tools/nes-model/board_parts.py')
    for s in ('I1-front', 'I3', 'I4'):
        assert s in text.split('export')[0]
    for name in ('BOARD', 'ICS', 'CONNECTORS', 'PASSIVES', 'OTHERS', 'HEIGHTS'):
        assert re.search(rf'^export const {name} = ', text, re.M), name
    assert not re.search(r'#[0-9a-fA-F]{3,8}\b|rgba?\(', text)
    assert '"ntsc"' in text and '"pal"' in text
