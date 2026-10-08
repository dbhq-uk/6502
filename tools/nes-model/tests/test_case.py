"""The case's shape and features, both consoles (case_measure.py), on
made-up scenes photographed by made-up pinhole cameras.

    /tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q

The case frame is the plan's: millimetres from the case's left rear corner at
table level, x to the right as seen from the front, y towards the front, z up.
A camera here is a pinhole with square pixels: K = [[f, 0, cx], [0, f, cy],
[0, 0, 1]], and a world point X goes to K (R X + t).
"""
import math
import os
import re
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import case_measure as cm  # noqa: E402

W, D, H = 254.0, 203.2, 88.9


def camera(f, size, eye, target, up=(0, 0, 1)):
    """K, R, t for a camera at `eye` looking at `target`, image y down."""
    eye, target, up = (np.asarray(v, float) for v in (eye, target, up))
    z = target - eye
    z /= np.linalg.norm(z)
    x = np.cross(z, up)
    if np.linalg.norm(x) < 1e-9:          # looking straight down: pick any x
        x = np.cross(z, (0, 1, 0))
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    R = np.array([x, y, z])
    K = np.array([[f, 0, (size[0] - 1) / 2], [0, f, (size[1] - 1) / 2], [0, 0, 1.0]])
    return K, R, -R @ eye


def project(cam, X):
    K, R, t = cam
    X = np.atleast_2d(np.asarray(X, float))
    p = (K @ (R @ X.T + t[:, None])).T
    return p[:, :2] / p[:, 2:]


def noisy(p, rng, px=0.2):
    return p + rng.normal(0, px, p.shape)


# --- the box: its proportions and height from two corner photographs ---------------------

def stepped_box_views():
    """A box W by D by H with a step on its top (a raised strip, as the
    case's black band is a separate part), seen from front left and rear
    right by two 112 mm cameras on a 4020 px wide picture, as O2's are."""
    size = (4020, 2880)
    f = 24000.0
    cams = [camera(f, size, (-700, 2300, 1400), (W / 2, D / 2, H / 2)),
            camera(f, size, (W + 700, -2100, 1400), (W / 2, D / 2, H / 2))]
    top = np.array([[0, D, H], [W, D, H], [W, 0, H], [0, 0, H]], float)          # near long edge first for camera 0
    top_br = np.array([[W, 0, H], [0, 0, H], [0, D, H], [W, D, H]], float)       # and for camera 1
    base = [np.array([[x, D, 0] for x in np.linspace(20, W - 20, 9)]),
            np.array([[x, 0, 0] for x in np.linspace(20, W - 20, 9)])]
    step = np.array([[180, 0, H + 3], [215, 0, H + 3], [215, D, H + 3], [180, D, H + 3]], float)
    return cams, [top, top_br], base, step, size


def test_a_stepped_box_from_two_corners_gives_its_proportions_and_height():
    rng = np.random.default_rng(5)
    cams, tops, bases, step, size = stepped_box_views()
    got = []
    for cam, top, base in zip(cams, tops, bases):
        C = noisy(project(cam, top), rng)
        # the step's own corners are in the picture too, and must not be taken for the top's
        assert np.all(np.isfinite(project(cam, step)))
        c = np.array([(size[0] - 1) / 2, (size[1] - 1) / 2])
        got.append(cm.box_from_photo(C, c, noisy(project(cam, base), rng)))
    d2w = np.mean([g['depthToWidth'] for g in got])
    h2w = np.mean([g['heightToWidth'] for g in got])
    assert abs(d2w / (D / W) - 1) < 0.003
    assert abs(h2w / (H / W) - 1) < 0.005


def test_a_box_from_one_corner_says_which_focal_length_it_used():
    cams, tops, bases, _, size = stepped_box_views()
    c = np.array([(size[0] - 1) / 2, (size[1] - 1) / 2])
    g = cm.box_from_photo(project(cams[0], tops[0]), c, project(cams[0], bases[0]))
    assert abs(g['focalPx'] / 24000.0 - 1) < 0.001


# --- a feature on a face, rectified ------------------------------------------------------

def front_face_view():
    size = (4020, 2880)
    cam = camera(20000.0, size, (-500, 2400, 1200), (W / 2, D, H / 2))
    face = lambda u, z: np.array([u, D, z], float)      # the front face: u along it from its left end
    return cam, face


def test_a_face_rectified_on_its_four_bounding_lines_gives_a_feature_to_a_fifth_of_a_millimetre():
    rng = np.random.default_rng(7)
    cam, face = front_face_view()
    corners = np.array([face(0, H), face(W, H), face(W, 0), face(0, 0)])
    # each bounding line is fitted to points along it, as the marks are
    lines = {}
    for name, (a, b) in {'top': (face(0, H), face(W, H)), 'right': (face(W, H), face(W, 0)),
                         'base': (face(W, 0), face(0, 0)), 'left': (face(0, 0), face(0, H))}.items():
        pts = noisy(project(cam, [a + (b - a) * s for s in np.linspace(0.1, 0.9, 30)]), rng)
        lines[name] = cm.fit_line(pts)
    G = cm.face_homography(cm.corners_from_lines(lines), W, H)
    for u, z in [(30.0, 20.0), (127.0, 44.0), (230.0, 70.0), (60.5, 5.0)]:
        back = cm.to_face(G, noisy(project(cam, face(u, z)), rng, 0.1))[0]
        assert np.hypot(back[0] - u, back[1] - z) < 0.2, (u, z, back)
    assert np.allclose(cm.to_face(G, project(cam, corners)), [[0, H], [W, H], [W, 0], [0, 0]], atol=0.2)


def test_a_face_homography_with_the_wrong_height_still_gives_the_right_places_along_it():
    cam, face = front_face_view()
    C = project(cam, np.array([face(0, H), face(W, H), face(W, 0), face(0, 0)]))
    G = cm.face_homography(C, W, 2.5 * H)
    for u in (12.0, 100.0, 201.0):
        assert abs(cm.to_face(G, project(cam, face(u, 30.0)))[0][0] - u) < 1e-6


# --- the board in the case, and the ports placed by it -----------------------------------

BOARD_POINTS = np.array([[10, 6], [150, 6], [188, 85], [5, 114], [180, 114], [42, 69], [118, 56], [60, 30]], float)


def board_scene(x0=24.0, y0=171.0, rim_z=60.0, board_z=28.0, turn_deg=0.0, eye=(140, 80, 330)):
    """The bottom shell's rim, its top edge W by D at rim_z, and the board
    lying in it solder side up at board_z, turned over about x: board (bx, by)
    is at case (x0 + bx, y0 - by). A 20 mm lens close above, as the PAL set's."""
    size = (6184, 4861)
    cam = camera(5300.0, size, eye, (W / 2, D / 2, board_z))
    rim = np.array([[0, 0, rim_z], [W, 0, rim_z], [W, D, rim_z], [0, D, rim_z]], float)
    t = math.radians(turn_deg)
    rot = np.array([[math.cos(t), -math.sin(t)], [math.sin(t), math.cos(t)]])
    def on_case(b):
        b = np.atleast_2d(b)
        m = np.c_[b[:, 0], -b[:, 1]] @ rot.T
        return np.c_[x0 + m[:, 0], y0 + m[:, 1], np.full(len(b), board_z)]
    K, R, tt = cam
    nadir = K @ (R @ np.array([0, 0, -1.0]))
    return cam, rim, on_case, nadir


@pytest.mark.parametrize('eye', [(140, 80, 330), (60, 150, 420), (200, 40, 300)])
def test_the_board_in_the_case_from_one_view_with_its_nadir(eye):
    rng = np.random.default_rng(11)
    cam, rim, on_case, nadir = board_scene(eye=eye)
    Hr = cm.face_homography(project(cam, rim), [[0, 0], [W, 0], [W, D], [0, D]])
    img = noisy(project(cam, on_case(BOARD_POINTS)), rng, 0.05)
    got = cm.board_in_case(img, BOARD_POINTS, Hr, nadir)
    assert abs(got['x'] - 24.0) < 0.1 and abs(got['y'] - 171.0) < 0.1, got
    assert abs(got['turnDeg']) < 0.05
    assert got['k'] < 1      # the board is further from the camera than the rim


def test_the_board_in_the_case_without_its_nadir_would_be_wrong():
    """The reason the nadir is needed: taking the board's picture as if it
    lay in the rim's plane puts it several millimetres out."""
    cam, rim, on_case, nadir = board_scene()
    Hr = cm.face_homography(project(cam, rim), [[0, 0], [W, 0], [W, D], [0, D]])
    p = cm.to_face(Hr, project(cam, on_case(BOARD_POINTS)))
    naive = np.mean(p + np.c_[-BOARD_POINTS[:, 0], BOARD_POINTS[:, 1]], 0)
    assert np.hypot(naive[0] - 24.0, naive[1] - 171.0) > 2.0


def test_ports_placed_by_a_known_board_offset_come_back_to_a_tenth_of_a_millimetre():
    placed = {'x': 24.0, 'y': 171.0, 'turnDeg': 0.0}
    jacks = np.array([[170.0, 155.4], [190.5, 155.4], [208.25, 155.4]])
    case = cm.board_to_case(jacks, placed)
    assert np.allclose(case, [[194.0, 15.6], [214.5, 15.6], [232.25, 15.6]], atol=0.1)
    back = cm.case_to_board(case, placed)
    assert np.allclose(back, jacks, atol=1e-9)


def test_a_place_on_the_rear_face_seen_from_behind_is_mirrored_into_the_case_frame():
    # u runs from the rear face's left end as seen from behind, which is the case's right
    assert cm.rear_u_to_x(0.0, W) == W
    assert cm.rear_u_to_x(54.0, W) == W - 54.0


# --- the profile, and its check ----------------------------------------------------------

def test_the_profile_check_passes_within_its_limit_and_falls_back_beyond_it():
    built = [{'inset': 0.0, 'z': H}, {'inset': 0.0, 'z': 41.0}, {'inset': 11.0, 'z': 0.0}]
    patent = [{'inset': 0.0, 'z': H}, {'inset': 0.0, 'z': 40.0}, {'inset': 12.0, 'z': 0.0}]
    ok = cm.profile_choice(built, {'inset': 12.5, 'z': 0.0}, patent)
    assert ok['check']['errMm'] == pytest.approx(1.5) and ok['from'] == 'photographs' and ok['profile'] == built
    bad = cm.profile_choice(built, {'inset': 13.5, 'z': 0.0}, patent)
    assert bad['check']['errMm'] == pytest.approx(2.5) and bad['from'] == 'patent' and bad['profile'] == patent
    assert cm.PROFILE_LIMIT_MM == 2.0


# --- the words, and the module the site reads --------------------------------------------

def test_labels_are_words_and_nothing_else():
    good = [{'words': 'POWER', 'box': [10, 20, 18, 4], 'face': 'front'}]
    assert cm.label_problems(good) == []
    for bad in ([{'words': '', 'box': [0, 0, 1, 1], 'face': 'front'}],
                [{'words': 'Nintendo', 'box': [0, 0, 1, 1], 'face': 'front', 'path': 'M0 0L1 1'}],
                [{'words': 'Nintendo', 'box': [0, 0, 1, 1], 'face': 'front', 'image': 'logo.png'}],
                [{'words': 'Nintendo', 'box': [0, 0, 1], 'face': 'front'}],
                [{'words': '<svg/>', 'box': [0, 0, 1, 1], 'face': 'front'}]):
        assert cm.label_problems(bad), bad


def test_the_parts_module_is_generated_with_its_sources_and_no_colours(tmp_path):
    exports = {name: {'note': f'{name}: from somewhere', 'value': [1, 2]} for name in cm.EXPORTS}
    text = cm.parts_module(exports, ['O1', 'O2-FL'])
    assert text.startswith('// GENERATED by tools/nes-model/case_measure.py')
    for name in cm.EXPORTS:
        assert f'export const {name} = ' in text
    assert 'O1' in text.split('\n\n')[0]
    assert not re.search(r'#[0-9a-fA-F]{3,8}\b|\b(rgb|hsl)a?\(', text)
    with pytest.raises(ValueError):
        cm.parts_module({**exports, 'CASE': {'note': 'x', 'colour': '#ffffff'}}, ['O1'])
    with pytest.raises(ValueError):
        cm.parts_module({k: v for k, v in exports.items() if k != 'FEET'}, ['O1'])
