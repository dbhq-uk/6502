"""The BBC Micro's outside (keys.py, case_profile.py, case_parts.py), on made-up
keyboards, cameras and rear faces.

    ~/venv-models/bin/python -m pytest tools/bbc-micro-model/tests -q

The frame is K1's: millimetres, x to the right, y towards the front, from
above. The cameras are pinholes, x_cam = R X + t, with the world's third axis
pointing down into the key plane, as case_profile.py fits them.
"""
import math
import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import case_parts  # noqa: E402
import case_profile  # noqa: E402
import common  # noqa: E402
import keys  # noqa: E402

PITCH = 19.05


def camera(f, c, centre, look_at):
    """A pinhole at `centre` (z down, so above the plane is negative z) looking at `look_at`, x roughly along +x."""
    C, T = np.array(centre, float), np.array(look_at, float)
    z = (T - C) / np.linalg.norm(T - C)
    x = np.cross([0, 0, 1.0], z)
    x = -x / np.linalg.norm(x) if x[0] < 0 else x / np.linalg.norm(x)
    y = np.cross(z, x)
    R = np.stack([x, y, z])
    return (float(f), np.array(c, float), R, -R @ C)


def grid():
    """A made-up keyboard: four rows of ten keys at the pitch, each row named."""
    names = [f'K{r}{i}' for r in range(4) for i in range(10)]
    pts = np.array([[100 + i * PITCH + r * 4.7, 120 + r * PITCH] for r in range(4) for i in range(10)], float)
    return names, pts


# --- 1. the keys -------------------------------------------------------------------------------

def test_mapping_passes_with_every_key_once_and_stops_on_a_key_k1_lacks():
    names = ['Shift', 'A', 'B']
    k = [{'name': n} for n in ['Shift', 'A', 'B', 'Break', 'Shift2']]
    assert keys.mapping_check(k, names)['verdict'] == 'pass'
    lack = keys.mapping_check([e for e in k if e['name'] != 'B'], names)
    assert lack['verdict'] == 'STOP' and lack['lacks'] == ['B']
    twice = keys.mapping_check(k + [{'name': 'A'}], names)
    assert twice['verdict'] == 'fail' and twice['twice'] == ['A']
    no_second_shift = keys.mapping_check([e for e in k if e['name'] != 'Shift2'], names)
    assert no_second_shift['verdict'] == 'STOP' and no_second_shift['lacks'] == ['Shift2']


def test_the_table_maps_every_bbc_key_once_and_no_k1_reference_twice():
    mapped = [v[0] for v in keys.K1_KEYS.values()]
    machine = keys.bbc_keys()
    assert len(machine) == 72
    assert sorted(mapped) == sorted(machine + list(keys.EXTRA))
    assert len(set(keys.K1_KEYS)) == len(keys.K1_KEYS)


def test_a_legend_is_found_at_the_middle_of_its_bounding_box():
    rgb = np.full((80, 80, 3), 40, np.uint8)
    rgb[30:50, 33:37] = 240           # an L: its stroke
    rgb[46:50, 33:47] = 240           # and its foot; the box is x 33 to 46, y 30 to 49
    rgb[12:15, 12:15] = 250           # glare near the window's edge, which must be left out
    x, y = keys.legend_centre(rgb, 40, 40, 30)
    assert x == pytest.approx((33 + 46) / 2) and y == pytest.approx((30 + 49) / 2)


def test_a_key_grid_seen_through_a_camera_with_rows_at_their_own_heights_registers_to_the_parallax_they_imply():
    names, pts = grid()
    cam = camera(2400, (1000, 750), (240, 400, -520), (240, 170, 0))
    flat = case_profile.project(*cam, np.c_[pts, np.zeros(len(pts))])
    reg = keys.register({n: tuple(p) for n, p in zip(names, flat)}, [{'name': n, 'x': p[0], 'y': p[1]} for n, p in zip(names, pts)])
    assert reg['heldOutMm']['max'] < 1e-6        # one plane: a homography is exact
    heights = np.array([[0, 0, -1.5 * (r % 2)] for r in range(4) for _ in range(10)], float)   # every other row 1.5 mm higher
    seen = case_profile.project(*cam, np.c_[pts, np.zeros(len(pts))] + heights)
    reg = keys.register({n: tuple(p) for n, p in zip(names, seen)}, [{'name': n, 'x': p[0], 'y': p[1]} for n, p in zip(names, pts)])
    # a point h above the plane moves by about its distance from under the camera times h over the camera's height
    worst = max(np.hypot(*(p - (240, 400))) * 1.5 / 520 for p in pts)
    assert 0.05 < reg['heldOutMm']['median'] < worst and reg['heldOutMm']['max'] < 2 * worst


# --- 2. the two cameras ------------------------------------------------------------------------

def two_views():
    names, pts = grid()
    W = np.c_[pts, np.zeros(len(pts))]
    cams = {'O4': camera(2450, (900, 790), (200, 370, -500), (220, 230, 0)),
            'O5': camera(2450, (930, 780), (-220, 500, -450), (200, 180, 0))}
    unknown = {'openingFrontLeft': np.array([23.0, 234.0, 6.0]), 'openingFrontRight': np.array([385.0, 236.0, 6.0]),
               'ledCassetteMotor': np.array([75.0, 226.0, 9.0]), 'ledCapsLock': np.array([95.0, 226.0, 9.0]),
               'ledShiftLock': np.array([115.0, 226.0, 9.0])}
    known = {v: (W, case_profile.project(*cams[v], W)) for v in cams}
    marked = {n: {v: tuple(case_profile.project(*cams[v], X[None])[0]) for v in cams} for n, X in unknown.items()}
    return cams, known, marked, unknown


def test_two_views_of_known_points_triangulate_the_others_to_under_half_a_millimetre(monkeypatch):
    cams, known, marked, unknown = two_views()
    rng = np.random.default_rng(1)
    known = {v: (W, p + rng.normal(0, 0.3, p.shape)) for v, (W, p) in known.items()}
    monkeypatch.setattr(case_profile, 'F0_PX', 2240.0)
    b = case_profile.Bundle(known, marked, {'O4': (1929, 1338), 'O5': (1756, 1239)})
    q = b.fit()
    got, X = b.unpack(q)
    assert q[0] == pytest.approx(2450, rel=0.03)
    for j, n in enumerate(b.names):
        assert np.linalg.norm(X[j] - unknown[n]) < 0.5, n
    # a point left out of the fit, on the plane, comes back where it is
    P = np.array([150.0, 160.0, 0.0])
    seen = [case_profile.project(*got[v], P[None])[0] for v in ('O4', 'O5')]
    assert np.linalg.norm(case_profile.triangulate([got['O4'], got['O5']], seen) - P) < 0.5


def test_triangulation_is_the_point_least_off_in_both_views_not_the_linear_start():
    cams, known, marked, unknown = two_views()
    P = np.array([150.0, 160.0, -4.0])
    seen = [case_profile.project(*cams[v], P[None])[0] + e for v, e in zip(('O4', 'O5'), ((2.0, -1.5), (-2.5, 1.0)))]
    A = []
    for (f, c, R, t), (x, y) in zip([cams['O4'], cams['O5']], seen):
        Pm = np.array([[f, 0, c[0]], [0, f, c[1]], [0, 0, 1.0]]) @ np.c_[R, t]
        A += [x * Pm[2] - Pm[0], y * Pm[2] - Pm[1]]
    lin = np.linalg.svd(np.array(A))[2][-1]
    lin = lin[:3] / lin[3]
    rss = lambda X: sum(float(((case_profile.project(*cams[v], X[None])[0] - s) ** 2).sum()) for v, s in zip(('O4', 'O5'), seen))
    got = case_profile.triangulate([cams['O4'], cams['O5']], seen)
    assert rss(got) < rss(lin) - 1e-3


def test_the_check_point_fails_when_its_legend_is_found_off_its_place():
    cams, known, marked, unknown = two_views()
    P = np.array([150.0, 160.0, 0.0])
    seen = [case_profile.project(*cams[v], P[None])[0] for v in ('O4', 'O5')]
    seen[1] = seen[1] + (12.0, 0.0)          # found 12 px off in one view
    err = np.linalg.norm(case_profile.triangulate([cams['O4'], cams['O5']], seen) - P)
    assert err > case_profile.CHECK_PASS_MM


# --- 3. the rear and the board in the case ----------------------------------------------------

def rear_marks(ports_case_x, z=40.0, scale=4.2, xl=1900.0, move=None, tilt=0.8):
    """O2's marks for a rear face seen square on across at `scale` px per mm, the machine's left end at xl, and
    foreshortened up and down by `tilt` (the camera looks down on it)."""
    top, bot = 520.0, 520.0 + (73 - case_parts.t('feet')) * scale * tilt
    py = bot - (z - case_parts.t('feet')) * scale * tilt
    px = lambda x: xl - x * scale
    ports = []
    for ref, kind, x in ports_case_x:
        if move and ref == move[0]:
            x += move[1]
        if kind == 'din':
            r = 8 * scale
            arc = [[px(x) + r * math.cos(a), py + r * math.sin(a)] for a in np.radians([100, 120, 140, 160, 180, 200])]
            ports.append({'label': ref, 'ref': ref, 'kind': 'din', 'arc': arc})
        elif kind == 'dType':
            ports.append({'label': ref, 'ref': ref, 'kind': 'dType', 'centre': 'dsubPosts'})
            dsub = x
        else:
            ports.append({'label': ref, 'ref': ref, 'kind': kind, 'centre': [px(x), py], 'what': 'its middle'})
    half = 33.325 / 2 * scale            # TE's drawing 1757819: the holes 33.20 to 33.45 mm apart
    return {'o2': {
        'dsubPosts': {'left': [px(dsub) - half, py], 'right': [px(dsub) + half, py]},
        'caseEnds': {'machineLeft': [xl, 580], 'machineRight': [xl - 415 * scale, 580]},
        'rearFace': {'topY': top, 'bottomY': bot},
        'ports': ports, 'vents': {'slots': [[300, 700]], 'y': [560, 580]}, 'mains': {'box': [1500, 600, 1700, 700], 'words': ['On']}}}


BOARD_X = {'SK1': 275.6, 'SK2': None, 'SK3': 215.15, 'SK4': 194.0, 'SK5': 173.95, 'SK6': 104.55, 'SK7': None}


def rear_setup(monkeypatch, offset=40.0, move=None):
    kinds = {'SK1': 'phono', 'SK2': 'bnc', 'SK3': 'din', 'SK4': 'din', 'SK5': 'din', 'SK6': 'dType', 'SK7': 'din'}
    xs = {r: (BOARD_X[r] + offset if BOARD_X[r] is not None else {'SK2': 290.0, 'SK7': 60.0}[r]) for r in kinds}
    marks = rear_marks([(r, kinds[r], xs[r]) for r in kinds], move=move)
    monkeypatch.setattr(keys, 'case_marks', lambda: marks)
    parts = {'model': {'connectors': [{'ref': r, 'x': x} for r, x in BOARD_X.items() if x is not None]}}
    return case_parts.rear(parts), xs


def test_ports_placed_by_a_known_board_offset_come_back_to_a_tenth_of_a_millimetre(monkeypatch):
    r, xs = rear_setup(monkeypatch, offset=40.0)
    assert r['offsetX'] == pytest.approx(40.0, abs=0.1)
    assert r['check']['verdict'] == 'pass' and r['check']['n'] == 5
    for p in r['ports']:
        assert p['x'] == pytest.approx(xs[p['ref']], abs=0.1), p['ref']
        assert p['z'] == pytest.approx(40.0, abs=0.1), p['ref']
    assert set(r['check']['errMm']) == set(case_parts.SCORED)
    assert r['order'] == ['SK1', 'SK2', 'SK3', 'SK4', 'SK5', 'SK6', 'SK7']     # O2's order, left to right: the machine's right first


def test_a_port_three_millimetres_off_fails_the_rear_check_and_is_named(monkeypatch):
    r, _ = rear_setup(monkeypatch, offset=40.0, move=('SK4', 3.0))
    assert r['check']['verdict'] == 'fail'
    assert abs(r['check']['errMm']['SK4']) > 2.0
    assert all(abs(v) <= 2.0 for k, v in r['check']['errMm'].items() if k != 'SK4')


def test_the_d_shell_scale_is_the_middle_of_te_s_hole_spacing():
    assert case_parts.DSUB_HOLES_MM == pytest.approx(33.325)


def test_a_rim_arc_gives_its_circle():
    a = np.radians(np.linspace(100, 210, 6))
    cx, cy, r = case_parts.circle(np.c_[300 + 30 * np.cos(a), 700 + 30 * np.sin(a)])
    assert (cx, cy, r) == pytest.approx((300, 700, 30), abs=1e-6)


def test_the_rigid_motion_takes_two_points_to_two_points_and_reports_the_scale_they_imply():
    src = [[20.0, 230.0], [380.0, 236.0]]
    a = math.radians(1.5)
    R = np.array([[math.cos(a), -math.sin(a)], [math.sin(a), math.cos(a)]])
    dst = (np.array(src) @ R.T + (12.0, 95.0)).tolist()
    tr = case_parts.rigid(src, dst)
    assert tr['rotationDeg'] == pytest.approx(1.5) and tr['impliedScale'] == pytest.approx(1.0)
    assert np.allclose(case_parts.apply(tr['matrix'], src), dst)
    tr2 = case_parts.rigid(src, (np.array(dst) * 1.02).tolist())
    assert tr2['impliedScale'] == pytest.approx(1.02)


def test_the_module_exports_every_part_with_a_note():
    case = {'footprint': {'widthMm': 415, 'depthMm': 345}, 'heightMm': 73, 'outline': {'cornerRadiusMm': 3},
            'recess': {'opening': [[0, 0]], 'panelZ': 70, 'from': 'O1'}, 'profile': [{'y': 0, 'z': 5}], 'keyPlaneZ': 79,
            'profileCheck': {'fallback': False}, 'profileFrom': 'D1', 'keys': [{'name': 'A'}], 'k1ToCase': {'from': 'x', 'impliedScale': 1.03},
            'caseTop': {'scale': 1.04}, 'widthOnRear': {'mm': 425.0},
            'keyPlaneFrom': 'y', 'leds': [{'name': 'caps lock', 'x': 1, 'y': 2, 'z': 3}], 'ledsFrom': 'z',
            'badge': {'strip': [], 'words': ['W'], 'wordsBox': [0, 0, 1, 1], 'from': 'O1'}, 'grille': {'slots': [], 'from': 'O2'},
            'rear': [{'label': 'RGB', 'ref': 'SK3', 'kind': 'din', 'x': 1, 'z': 2, 'w': 3, 'h': 4}], 'mains': {}, 'rearCheck': {'verdict': 'fail'},
            'underside': [{'ref': 'PL8', 'label': 'disc drive', 'x': 1, 'y': 2, 'w': 3, 'd': 4}], 'undersidePanel': {},
            'feet': {'heightMm': 5}, 'sources': ['D1']}
    text = case_parts.module_text(case)
    assert text.startswith('// GENERATED by tools/bbc-micro-model/case_parts.py')
    for name in ('CASE', 'RECESS', 'PROFILE', 'KEYS', 'LEDS', 'BADGE', 'GRILLE', 'REAR', 'UNDERSIDE', 'FEET'):
        line = next(l for l in text.splitlines() if l.startswith(f'export const {name} = '))
        assert '"note":' in line, name
    assert 'the check failed as measured' in text
    # corrected after task 9a's review (13 October 2026): the measures point different ways, not all wider
    width = ("the Service Manual's size; the outside's measures point different ways, not resolved: in K1's units the keys read 1.0400 "
             "and the opening's front corners 1.0300 times a frame that takes the case as 415 mm, so if K1 is true to size the case "
             "is narrower, about 399 mm, the first perhaps partly parallax, the key tops standing above the plane registered; "
             "on O2's 15-way D scale it reads 425.0 mm, wider")
    for name in ('CASE', 'RECESS', 'REAR'):
        line = next(l for l in text.splitlines() if l.startswith(f'export const {name} = '))
        assert width in line, name
    assert 'times it' not in text
    assert "the rear's x values are on the 15-way D's scale, on which the case reads 425.0 mm wide, not rescaled to 415 mm" in text


def test_colour_classes_go_by_the_cap_not_its_legend(monkeypatch):
    """Black caps with large white legends, red caps and light brown caps, drawn 40 px apart: each comes out its class."""
    img = np.full((60, 40 * 12, 3), 20, np.uint8)
    kinds = ['dark'] * 6 + ['red'] * 3 + ['light'] * 3
    colour = {'dark': (35, 33, 32), 'red': (200, 30, 30), 'light': (110, 95, 70)}
    for i, k in enumerate(kinds):
        x = 20 + 40 * i
        img[10:50, x - 18:x + 18] = colour[k]
        if k == 'dark':
            img[27:34, x - 3:x + 4] = 245           # a legend over more than half the sampled disc (radius 5 px) and under three quarters
    monkeypatch.setattr(common, 'read_rgb', lambda sid: img)
    caps = [{'x': 20 + 40 * i, 'y': 30.0} for i in range(len(kinds))]
    H = np.array([[40 / PITCH, 0, 0], [0, 40 / PITCH, 0], [0, 0, 1.0]])     # K1 millimetres are pixels here: one pitch is 40 px
    caps = [{'x': c['x'] * PITCH / 40, 'y': c['y'] * PITCH / 40} for c in caps]
    classes, clusters = keys.colour_classes(caps, H)
    assert classes == kinds


def test_judge_keys_gives_the_plan_s_three_bands():
    """The plan's O1 row: median at most 1.0, 90th percentile at most 2.0, largest at most 3.0 and 30 keys to pass;
    a median over 1.5 or more than five keys over 3.0 is a STOP; anything else is between."""
    k = lambda median, p90, mx, n=50, over3=0: {'heldOutMm': {'median': median, 'p90': p90, 'max': mx}, 'n': n, 'over3mm': over3}
    assert keys.judge_keys(k(1.0, 2.0, 3.0)) == 'pass'
    assert keys.judge_keys(k(1.2, 1.5, 2.0)) == 'between pass and stop'
    assert keys.judge_keys(k(0.9, 2.1, 2.5)) == 'between pass and stop'
    assert keys.judge_keys(k(0.9, 1.5, 3.1)) == 'between pass and stop'
    assert keys.judge_keys(k(0.9, 1.5, 2.0, n=29)) == 'between pass and stop'
    assert keys.judge_keys(k(1.6, 1.8, 2.0)) == 'STOP'
    assert keys.judge_keys(k(0.9, 3.5, 9.0, over3=6)) == 'STOP'
    assert keys.judge_keys(k(1.5, 1.8, 2.0)) == 'between pass and stop'
