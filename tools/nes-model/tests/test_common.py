"""The shared parts of the NES model tools, on made-up inputs.

    /tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q
"""
import hashlib
import json
import math
import os
import sys

import numpy as np
import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import common  # noqa: E402


# --- inputs and their hashes -------------------------------------------------

def made_up(tmp_path, monkeypatch, content=b'a made-up scan'):
    (tmp_path / 'made-up.jpg').write_bytes(content)
    source = {'id': 'X1', 'file': 'made-up.jpg', 'sha256': hashlib.sha256(b'a made-up scan').hexdigest()}
    monkeypatch.setenv('NES_MODEL_INPUTS', str(tmp_path))
    monkeypatch.setattr(common, 'sources', lambda: [source])
    return source


def test_original_accepts_the_recorded_hash(tmp_path, monkeypatch):
    made_up(tmp_path, monkeypatch)
    assert common.original('X1') == tmp_path / 'made-up.jpg'


def test_original_refuses_a_different_file(tmp_path, monkeypatch, capsys):
    made_up(tmp_path, monkeypatch, content=b'a different scan')
    with pytest.raises(SystemExit) as e:
        common.original('X1')
    assert e.value.code == 2


def test_original_refuses_a_missing_file(tmp_path, monkeypatch):
    made_up(tmp_path, monkeypatch)
    (tmp_path / 'made-up.jpg').unlink()
    with pytest.raises(SystemExit) as e:
        common.original('X1')
    assert e.value.code == 2


def test_original_refuses_an_unknown_id(tmp_path, monkeypatch):
    made_up(tmp_path, monkeypatch)
    with pytest.raises(SystemExit) as e:
        common.original('NOPE')
    assert e.value.code == 2


def test_inputs_dir_names_the_variable(monkeypatch, tmp_path):
    monkeypatch.delenv('NES_MODEL_INPUTS', raising=False)
    with pytest.raises(SystemExit) as e:
        common.inputs_dir()
    assert 'NES_MODEL_INPUTS' in str(e.value.code)
    monkeypatch.setenv('NES_MODEL_INPUTS', str(tmp_path / 'not-there'))
    with pytest.raises(SystemExit) as e:
        common.inputs_dir()
    assert 'NES_MODEL_INPUTS' in str(e.value.code)


# --- fits with held-out errors ------------------------------------------------

def grid(n=7, size=200.0):
    xs = np.linspace(10, size, n)
    return np.array([(x, y) for x in xs for y in np.linspace(5, size * 0.7, n)])


def apply(model, pts):
    pts = np.asarray(pts, float)
    if model == 'similarity':
        s, a, tx, ty = 15.7, math.radians(1.3), 40.0, -25.0
        R = s * np.array([[math.cos(a), -math.sin(a)], [math.sin(a), math.cos(a)]])
        return pts @ R.T + [tx, ty]
    if model == 'affine':
        M = np.array([[15.8, 0.21, 31.0], [-0.13, 15.6, 12.0]])
        return pts @ M[:, :2].T + M[:, 2]
    if model == 'homography':
        H = np.array([[4.6, 0.3, 300.0], [-0.1, 3.9, 900.0], [0.0001, 0.0009, 1.0]])
        p = np.c_[pts, np.ones(len(pts))] @ H.T
        return p[:, :2] / p[:, 2:]
    raise ValueError(model)


@pytest.mark.parametrize('model', ['similarity', 'affine', 'homography'])
def test_fit_recovers_a_known_transform(model):
    dst = grid()                    # mm
    src = apply(model, dst)         # pixels
    r = common.fit_held_out(src, dst, model, 'leave-one-out')
    assert r['n'] == len(dst)
    assert r['fitMm']['max'] < 1e-6
    assert r['heldOutMm']['max'] < 1e-6


def test_cubic_recovers_a_homography_and_a_smooth_bend():
    dst = grid(9)
    bent = dst + 1e-6 * np.c_[(dst[:, 0] - 100) ** 3, (dst[:, 1] - 70) ** 2 * dst[:, 0] / 100]
    src = apply('homography', bent)
    r = common.fit_held_out(src, dst, 'cubic', 'leave-one-out')
    assert r['fitMm']['max'] < 0.01
    assert common.fit_held_out(src, dst, 'homography', 'leave-one-out')['fitMm']['max'] > 0.05


@pytest.mark.parametrize('model', ['similarity', 'affine', 'homography'])
def test_held_out_median_reports_the_noise(model):
    rng = np.random.default_rng(1)
    dst = grid(8)
    src = apply(model, dst + rng.normal(0, 0.1, dst.shape))
    r = common.fit_held_out(src, dst, model, 'leave-one-out')
    assert 0.05 <= r['heldOutMm']['median'] <= 0.2
    assert r['heldOutMm']['median'] >= r['fitMm']['median']


def test_leave_one_out_never_fits_the_point_it_scores():
    # Every point but one is exact: if the odd one were in its own fit, the fit
    # would bend towards it and its error would come out under 10 mm.
    dst = grid()
    src = apply('affine', dst)
    src[5] = apply('affine', dst[5:6] + [10.0, 0.0])[0]
    r = common.fit_held_out(src, dst, 'affine', 'leave-one-out')
    assert r['worst'][0][0] == 5
    assert abs(r['worst'][0][1] - 10.0) < 1e-6
    assert r['fitMm']['max'] < 10.0


def test_folds_hold_out_a_whole_block():
    dst = grid(8)
    src = apply('affine', dst)
    labels = [int(x // 50) % 2 for x, _ in dst]
    src[labels.index(1)] += [200.0, 0.0]          # one point in fold 1 is off by about 12.7 mm
    r = common.fit_held_out(src, dst, 'affine', labels)
    odd = labels.index(1)
    each = dict(r['heldOutEach'])
    assert each[odd] > 10.0                        # scored by a fit on fold 0 alone, which is exact
    assert all(each[i] < 1e-6 for i in each if labels[i] == 1 and i != odd)
    assert all(each[i] > 1e-6 for i in each if labels[i] == 0)   # scored by a fit that holds the odd point


def test_ids_come_from_dicts():
    dst = grid(5)
    src = apply('similarity', dst)
    ids = [f'p{i}' for i in range(len(dst))]
    r = common.fit_held_out(dict(zip(ids, src)), dict(zip(ids, dst)), 'similarity', 'leave-one-out')
    assert {i for i, _ in r['heldOutEach']} == set(ids)


# --- writing data -------------------------------------------------------------

def test_write_data_sorts_keys_and_rounds(tmp_path, monkeypatch):
    monkeypatch.setattr(common, 'DATA', tmp_path)
    common.write_data('x.json', {'b': np.float64(1.234567), 'a': [np.int64(3), 2.00001], 'c': {'z': True, 'y': None}})
    text = (tmp_path / 'x.json').read_text()
    assert json.loads(text) == {'a': [3, 2.0], 'b': 1.2346, 'c': {'y': None, 'z': True}}
    assert text.index('"a"') < text.index('"b"') < text.index('"c"')


def test_write_data_refuses_what_is_not_a_plain_number(tmp_path, monkeypatch):
    monkeypatch.setattr(common, 'DATA', tmp_path)
    with pytest.raises(ValueError):
        common.write_data('x.json', {'a': float('nan')})
    with pytest.raises(TypeError):
        common.write_data('x.json', {'a': object()})


# --- pads ---------------------------------------------------------------------

# A synthetic board at the scan's 11.81 px/mm (300 dpi), drawn four times over
# size and shrunk, as the scanner's blur mixes light: a tinned ring of this
# scan's size (about 1.65 mm across, its open hole about 0.95 mm) on dark green
# lacquer, with the hole showing the scanner's lid, mid grey. The ring's light
# (OKLab L 0.50) is the scan's, below the BBC Micro's 0.55 floor for solder.
PX_PER_MM = 11.81


def board_with_pads(centres, r=9.75, hole=5.6):
    import cv2
    img = np.zeros((1200, 1200, 3), np.uint8)
    img[:] = (12, 32, 16)                                    # dark green lacquer, L about 0.2
    for x, y in centres:
        c = (int(round((4 * x + 1.5) * 16)), int(round((4 * y + 1.5) * 16)))   # pixel i of the shrunk image is 4i to 4i + 3 here
        cv2.circle(img, c, int(round(r * 4 * 16)), (98, 100, 94), -1, cv2.LINE_AA, 4)
        cv2.circle(img, c, int(round(hole * 4 * 16)), (40, 40, 40), -1, cv2.LINE_AA, 4)          # the hole's wall, in shadow
        cv2.circle(img, c, int(round((hole - 1.5) * 4 * 16)), (75, 75, 73), -1, cv2.LINE_AA, 4)  # the lid through the hole
    return cv2.resize(img, (300, 300), interpolation=cv2.INTER_AREA)


def test_the_synthetic_pad_is_this_scans_size():
    import cv2
    m = common.pad_mask(board_with_pads([(150.0, 150.0)]))
    n, _, st, _ = cv2.connectedComponentsWithStats(m, 8)
    assert n == 2
    assert common.PAD_AREA_PX[0] < st[1, 4] < common.PAD_AREA_PX[1]
    assert 223 <= st[1, 4] <= 378                            # the probe's 1st to 99th percentiles on U6's pads


def test_find_pads_finds_the_centres():
    centres = [(50.25, 60.5), (80.25, 60.5), (200.75, 210.25)]   # the first two 30 px apart: 2.54 mm
    pads = common.find_pads(board_with_pads(centres))
    assert len(pads) == 3
    for c in centres:
        assert np.hypot(*(pads - c).T).min() < 0.3


def test_refine_moves_a_mark_to_the_pad_only_within_the_limit():
    img = board_with_pads([(100.0, 100.0)])
    x, y, moved = common.refine_to_pad(img, 104.0, 97.0, PX_PER_MM)
    assert abs(x - 100) < 0.3 and abs(y - 100) < 0.3
    assert abs(moved - 5 / PX_PER_MM) < 0.03
    assert common.refine_to_pad(img, 109.0, 100.0, PX_PER_MM) is None      # 9 px is 0.76 mm, over 0.6


def test_rim_centre_finds_the_drill_not_the_solder():
    # rim_centre moved here from spike.py for task 2. The ring of solder is
    # drawn 2 px off the hole's centre, as solder lies on a real pad: the rim
    # is the hole's, so the centre found is the hole's, within 0.3 px.
    import cv2
    img = np.zeros((1200, 1200, 3), np.uint8)
    img[:] = (12, 32, 16)
    ring, hole = (100.0 + 2.0, 100.0), (100.0, 100.0)

    def at(p):
        return (int(round((4 * p[0] + 1.5) * 16)), int(round((4 * p[1] + 1.5) * 16)))
    cv2.circle(img, at(ring), int(round(9.75 * 4 * 16)), (98, 100, 94), -1, cv2.LINE_AA, 4)
    cv2.circle(img, at(hole), int(round(5.6 * 4 * 16)), (40, 40, 40), -1, cv2.LINE_AA, 4)
    cv2.circle(img, at(hole), int(round(4.1 * 4 * 16)), (75, 75, 73), -1, cv2.LINE_AA, 4)
    L = common.oklab(cv2.resize(img, (300, 300), interpolation=cv2.INTER_AREA))[0]
    found, rim = common.rim_centre(L, 101.5, 99.0, px_per_mm=PX_PER_MM)
    assert found is not None and len(rim) >= common.RIM_RAYS / 2
    assert np.hypot(found['x'] - hole[0], found['y'] - hole[1]) < 0.3
    assert abs(found['diameterMm'] - 2 * 5.6 / PX_PER_MM) < 0.06
    flat = common.oklab(np.full((60, 60, 3), (12, 32, 16), np.uint8))[0]
    assert common.rim_centre(flat, 30.0, 30.0) == (None, None)          # no hole: refused


# --- the KiCad redrawing (I2) ----------------------------------------------------

KICAD = '''(kicad_pcb (version 20240108) (generator "pcbnew")
  (gr_line (start 10 20) (end 206.25 20) (layer "Edge.Cuts") (width 0.1))
  (gr_line (start 206.25 20) (end 206.25 138.7) (layer "Edge.Cuts") (width 0.1))
  (gr_arc (start 10 138.7) (mid 9 130) (end 10 120) (layer "Edge.Cuts") (width 0.1))
  (gr_line (start 0 0) (end 300 300) (layer "F.SilkS") (width 0.1))
  (footprint "Package_DIP:DIP-40_W15.24mm" (layer "F.Cu")
    (at 58.006 100.89 90)
    (property "Reference" "U6")
    (property "Value" "RP2A03 CPU")
    (pad "1" thru_hole rect (at 0 0) (size 1.6 1.6) (drill 0.8) (layers "*.Cu" "*.Mask"))
    (pad "2" thru_hole oval (at 0 2.54) (size 1.6 1.6) (drill 0.8) (layers "*.Cu" "*.Mask"))
    (pad "" np_thru_hole circle (at 5 5) (size 3 3) (drill 3) (layers "*.Cu" "*.Mask"))
  )
  (footprint "Crystal:Crystal_HC49-U_Horizontal_1EP_style2" (layer "F.Cu")
    (at 98.15 48.8)
    (property "Reference" "X1")
    (property "Value" "21.477272 MHz")
    (pad "1" thru_hole circle (at 0 0) (size 1.5 1.5) (drill 0.8) (layers "*.Cu" "*.Mask"))
    (pad "1" thru_hole circle (at 1 0) (size 1.5 1.5) (drill 0.8) (layers "*.Cu" "*.Mask"))
  )
)
'''


def test_kicad_footprints_reads_place_turn_value_and_plated_pads(tmp_path):
    path = tmp_path / 'b.kicad_pcb'
    path.write_text(KICAD)
    f = common.kicad_footprints(path)
    assert set(f) == {'U6', 'X1'}
    assert f['U6'] == {'value': 'RP2A03 CPU', 'footprint': 'DIP-40_W15.24mm', 'at': (58.006, 100.89), 'rotation': 90.0, 'pads': 2}
    assert f['X1']['rotation'] == 0.0
    assert f['X1']['pads'] == 1          # two copper pads named "1" are one pad, as KiCad counts them


def test_kicad_outline_box_reads_edge_cuts_only(tmp_path):
    path = tmp_path / 'b.kicad_pcb'
    path.write_text(KICAD)
    x0, y0, x1, y1 = common.kicad_outline_box(path)
    assert (x0, y0, x1, y1) == (9.0, 20.0, 206.25, 138.7)   # the silkscreen line to (300, 300) is not the edge


def test_write_data_can_put_one_record_to_a_line(tmp_path, monkeypatch):
    import json
    monkeypatch.setattr(common, 'DATA', tmp_path)
    obj = {'b': [{'y': 1.23456, 'x': 2}, {'y': 3, 'x': 4}], 'a': {'n': 5, 'rows': [[1.0001, 2], [3, 4]]}, 'c': [1, 2]}
    common.write_data('r.json', obj, places=3, rows_per_line=True)
    text = (tmp_path / 'r.json').read_text()
    assert json.loads(text) == {'a': {'n': 5, 'rows': [[1.0, 2], [3, 4]]}, 'b': [{'x': 2, 'y': 1.235}, {'x': 4, 'y': 3}], 'c': [1, 2]}
    lines = text.splitlines()
    assert '  {"x":2,"y":1.235},' in lines and '  {"x":4,"y":3}' in lines
    assert '   [1.0,2],' in lines
