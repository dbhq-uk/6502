"""U9's jobs, read as numbers from the KiCad redrawing's nets (board_nets.py), on a made-up redrawing.

    /tmp/nesvenv/bin/python -m pytest tools/nes-model/tests -q

The made-up file has the shape of KiCad 8's: a footprint holds its reference in
a property and its pads, each pad naming its net by number and name and its pin
type. Only the parts of it that board_nets.py reads are made up.
"""
import os
import sys

import pytest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import board_nets  # noqa: E402

# U9's pins as the redrawing has them: pin, type, net.
U9 = {1: ('input', 'Net-(C7-Pad2)'), 2: ('output', 'Net-(R1-Pad1)'), 3: ('input', 'Net-(R1-Pad1)'), 4: ('output', '/CIC-CLK'),
      5: ('input', '/PPU-A13'), 6: ('output', '/PPU-~{A13}'), 7: ('power_in', '/GND'), 8: ('output', 'Net-(R5-Pad1)'),
      9: ('input', '/~{RST}'), 10: ('output', '/EXP-AUDIO-OUT'), 11: ('input', 'Net-(C20-Pad1)'), 12: ('output', 'Net-(U10-CLK_IN)'),
      13: ('input', 'Net-(R1-Pad1)'), 14: ('power_in', '/VCC')}
OTHERS = {'U5': {25: '/PPU-A13', 22: '/~{RST}'}, 'U6': {3: '/~{RST}'}, 'U10': {9: '/~{RST}', 6: 'Net-(U10-CLK_IN)'},
          'P1': {71: '/CIC-CLK', 58: '/PPU-~{A13}'}, 'P2': {24: '/CIC-CLK', 22: '/EXP-AUDIO-OUT'}, 'R5': {1: 'Net-(R5-Pad1)'},
          'R6': {1: 'Net-(C20-Pad1)', 2: '/EXP-AUDIO-OUT'}, 'R1': {1: 'Net-(R1-Pad1)', 2: 'Net-(C7-Pad2)'}, 'X2': {3: 'Net-(C7-Pad2)'}}


def pad(pin, net, kind='input', numbers=None):
    n = (numbers or {}).setdefault(net, len(numbers) + 1)
    return f'(pad "{pin}" thru_hole oval (at 0 0 90) (size 1.6 1.6) (net {n} "{net}") (pintype "{kind}"))'


def board(u9=U9, others=OTHERS, value='74LS04'):
    numbers = {}
    fps = [f'(footprint "Package_DIP:DIP-14_W7.62mm" (at 1 2) (property "Reference" "U9") (property "Value" "{value}") '
           + ' '.join(pad(p, net, kind, numbers) for p, (kind, net) in u9.items()) + ')']
    for ref, pins in others.items():
        fps.append(f'(footprint "X:{ref}" (at 3 4) (property "Reference" "{ref}") (property "Value" "x") '
                   + ' '.join(pad(p, net, 'passive', numbers) for p, net in pins.items()) + ')')
    # a mounting hole with no net, as the redrawing has
    fps.append('(footprint "H" (at 0 0) (property "Reference" "H1") (property "Value" "h") (pad "" np_thru_hole circle (at 0 0) (size 3 3)))')
    return '(kicad_pcb (version 20240108) ' + ' '.join(fps) + ')'


@pytest.fixture
def nets(tmp_path):
    p = tmp_path / 'made-up.kicad_pcb'
    p.write_text(board(), encoding='utf8')
    return board_nets.read_nets(p)


def test_every_pad_is_read_with_its_net_and_type(nets):
    pins, members = nets
    assert pins['U9'][5] == ('/PPU-A13', 'input')
    assert pins['U9'][6] == ('/PPU-~{A13}', 'output')
    assert pins['U5'][25] == ('/PPU-A13', 'passive')
    assert 'H1' not in pins, 'a pad with no net is left out'
    assert sorted(members['/PPU-A13']) == [('U5', 25), ('U9', 5)]


def test_a_job_holds_when_its_pins_are_the_right_way_round_and_the_right_parts_are_on_its_nets(nets):
    pins, members = nets
    result = board_nets.u9_jobs(pins, members)
    jobs = {j['job']: j for j in result['jobs']}
    assert all(j['holds'] for j in jobs.values()), [j for j in jobs.values() if not j['holds']]
    a13 = jobs["inverts the PPU's address line A13"]
    assert a13['in'] == {'pin': 5, 'net': '/PPU-A13', 'type': 'input', 'with': ['U5.25']}
    assert a13['out'] == {'pin': 6, 'net': '/PPU-~{A13}', 'type': 'output', 'with': ['P1.58']}
    assert jobs['inverts the reset line']['in']['with'] == ['U10.9', 'U5.22', 'U6.3']
    assert jobs['clocks the lockout chip']['out']['with'] == ['U10.6']
    assert result['pins']['4'] == {'net': '/CIC-CLK', 'type': 'output', 'with': ['P1.71', 'P2.24']}


def test_a_job_fails_when_a_part_it_names_is_not_on_its_net(tmp_path):
    others = {**OTHERS, 'U5': {25: '/SOMETHING-ELSE', 22: '/~{RST}'}}
    p = tmp_path / 'b.kicad_pcb'
    p.write_text(board(others=others), encoding='utf8')
    jobs = {j['job']: j for j in board_nets.u9_jobs(*board_nets.read_nets(p))['jobs']}
    assert not jobs["inverts the PPU's address line A13"]['holds']
    assert jobs['inverts the reset line']['holds']


def test_a_job_fails_when_its_pin_is_the_wrong_way_round(tmp_path):
    u9 = {**U9, 5: ('output', '/PPU-A13')}
    p = tmp_path / 'b.kicad_pcb'
    p.write_text(board(u9=u9), encoding='utf8')
    jobs = {j['job']: j for j in board_nets.u9_jobs(*board_nets.read_nets(p))['jobs']}
    assert not jobs["inverts the PPU's address line A13"]['holds']


def test_a_missing_u9_is_refused(tmp_path):
    p = tmp_path / 'b.kicad_pcb'
    p.write_text(board().replace('"U9"', '"U99"'), encoding='utf8')
    with pytest.raises(ValueError, match='U9'):
        board_nets.u9_jobs(*board_nets.read_nets(p))
