"""The shared parts of the BBC Micro model tools, on made-up inputs.

    /tmp/bbcvenv/bin/python -m pytest tools/bbc-micro-model/tests -q
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
    monkeypatch.setenv('BBC_MODEL_INPUTS', str(tmp_path))
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
    monkeypatch.delenv('BBC_MODEL_INPUTS', raising=False)
    with pytest.raises(SystemExit) as e:
        common.inputs_dir()
    assert 'BBC_MODEL_INPUTS' in str(e.value.code)
    monkeypatch.setenv('BBC_MODEL_INPUTS', str(tmp_path / 'not-there'))
    with pytest.raises(SystemExit) as e:
        common.inputs_dir()
    assert 'BBC_MODEL_INPUTS' in str(e.value.code)


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

def board_with_pads(centres, r=13):
    import cv2
    img = np.zeros((300, 300, 3), np.uint8)
    img[:] = (20, 90, 40)                                    # green lacquer
    for x, y in centres:
        cv2.circle(img, (int(round(x * 4)), int(round(y * 4))), r * 4, (200, 200, 195), -1, cv2.LINE_AA, 2)
        cv2.circle(img, (int(round(x * 4)), int(round(y * 4))), 3 * 4, (60, 60, 60), -1, cv2.LINE_AA, 2)   # a dark speck
    return img


def test_find_pads_finds_the_centres():
    centres = [(50.25, 60.5), (90.0, 60.5), (200.75, 210.25)]
    pads = common.find_pads(board_with_pads(centres))
    assert len(pads) == 3
    for c in centres:
        assert np.hypot(*(pads - c).T).min() < 0.3


def test_refine_moves_a_mark_to_the_pad_only_within_the_limit():
    img = board_with_pads([(100.0, 100.0)])
    x, y, moved = common.refine_to_pad(img, 104.0, 97.0, 15.7)
    assert abs(x - 100) < 0.3 and abs(y - 100) < 0.3
    assert abs(moved - 5 / 15.7) < 0.03
    assert common.refine_to_pad(img, 112.0, 100.0, 15.7) is None      # 12 px is 0.76 mm, over 0.6


# --- the KiCad keyboard ---------------------------------------------------------

KICAD = '''(kicad_pcb (version 20211014) (generator pcbnew)
  (footprint "Switch_Keyboard_Cherry_MX:SW_Cherry_MX_PCB_1.00u" (layer "F.Cu")
    (at 100 50 90)
    (fp_text reference "SW1" (at 0 0) (layer "F.SilkS"))
    (fp_text value "Q" (at 0 0) (layer "F.Fab"))
    (pad "" np_thru_hole circle (at 2 0) (size 4 4) (drill 4) (layers *.Cu *.Mask))
    (pad "" np_thru_hole circle (at 7.08 0) (size 1.75 1.75) (drill 1.75) (layers *.Cu *.Mask))
    (pad "1" thru_hole circle (at -3.81 -2.54) (size 2.5 2.5) (drill 1.5) (layers *.Cu *.Mask))
  )
  (footprint "Switch_Keyboard_Cherry_MX:SW_Cherry_MX_PCB_1.50u" (layer "F.Cu")
    (at 20 30 180)
    (property "Reference" "SW2")
    (property "Value" "tab")
    (pad "" np_thru_hole circle (at 1 3) (size 4 4) (drill 4) (layers *.Cu *.Mask))
  )
  (footprint "Diode_THT:D_DO-34" (layer "F.Cu") (at 0 0)
    (fp_text reference "D1" (at 0 0) (layer "F.SilkS"))
  )
)
'''


def test_switch_centres_reads_the_hole_through_the_footprints_rotation(tmp_path):
    # KiCad turns a footprint anticlockwise as seen on screen, with y down: at
    # 90 degrees a hole 2 mm to the right of the origin ends up 2 mm above it,
    # and at 180 degrees (1, 3) ends up at (-1, -3). Task 8 reads every key this way.
    path = tmp_path / 'k.kicad_pcb'
    path.write_text(KICAD)
    k = common.switch_centres(path)
    assert set(k) == {'SW1', 'SW2'}                      # the diode is not a switch
    assert k['SW1']['value'] == 'Q' and k['SW2']['value'] == 'tab'
    assert np.allclose(k['SW1']['centre'], (100.0, 48.0))
    assert np.allclose(k['SW2']['centre'], (19.0, 27.0))
    assert k['SW2']['footprint'] == 'SW_Cherry_MX_PCB_1.50u'
