"""U9's jobs, read as numbers from the KiCad redrawing's nets.

    NES_MODEL_INPUTS=<folder of inputs> python3 board_nets.py

U9 is the hex inverter. The page says what it does (inverts the PPU's address
line A13 and the reset line, and clocks the lockout chip), and says that this is
read from OpenTendo's KiCad redrawing of the board (I2), not traced on the scan.
It was first read by hand, in the journal of 5 October 2026. This reads it again
from the file: for each pin of U9, its net, its pin type and every other part's
pin on that net; and for each job the page names, whether the pins it uses are
the right way round (an inverter's input is an input) and the parts it must
reach are on its nets. Writes data/u9-nets.json, which the site's test reads.

A cross-check, as I2 always is here: a redrawing of a board, not the board, and
its U9 is a 74LS04 where the boards photographed carry a 74HCU04. Nothing is
drawn from it and nothing on the page depends on the footprint's part, only on
which nets the six gates are in.
"""
import sys
from pathlib import Path

import common

# A pin's net names are the redrawing's. The jobs: what U9 does, the inverter it
# does it with (input pin, output pin), and the parts each of the two nets must
# reach. The pins and parts are read off the redrawing on 5 October 2026 (the
# journal says how); what this checks is that the file still says so.
JOBS = [
    {'job': 'runs the lockout chips\' 4 MHz oscillator, with X2', 'in': 1, 'out': 2, 'inWith': ['X2'], 'outWith': ['R1']},
    {'job': 'clocks the lockout chip', 'in': 13, 'out': 12, 'inWith': ['R1'], 'outWith': ['U10']},
    {'job': 'buffers that clock to the cartridge and the expansion port', 'in': 3, 'out': 4, 'inWith': ['R1'], 'outWith': ['P1', 'P2']},
    {'job': 'inverts the PPU\'s address line A13', 'in': 5, 'out': 6, 'inWith': ['U5'], 'outWith': ['P1']},
    {'job': 'inverts the reset line', 'in': 9, 'out': 8, 'inWith': ['U5', 'U6', 'U10'], 'outWith': ['R5']},
    {'job': 'amplifies the expansion port\'s audio', 'in': 11, 'out': 10, 'inWith': ['R6'], 'outWith': ['P2', 'R6']},
]
POWER = {'/GND', '/VCC'}


def _pin(name):
    return int(name) if str(name).isdigit() else name


def read_nets(path):
    """(pins, members): pins[ref][pin] = (net, pin type) for every pad on a
    net, and members[net] = [(ref, pin), ...]."""
    root = common._sexpr(Path(path).read_text(encoding='utf8'))
    pins, members = {}, {}
    for fp in root[1:]:
        if not (isinstance(fp, list) and fp and fp[0] == 'footprint'):
            continue
        ref = next((p[2] for p in fp[1:] if isinstance(p, list) and p[:2] == ['property', 'Reference']), None)
        for pad in fp[1:]:
            if not (isinstance(pad, list) and pad and pad[0] == 'pad'):
                continue
            net = common._child(pad, 'net')
            if not net or len(net) < 3 or not net[2]:
                continue
            kind = common._child(pad, 'pintype')
            pin = _pin(pad[1])
            pins.setdefault(ref, {})[pin] = (net[2], kind[1] if kind else None)
            members.setdefault(net[2], []).append((ref, pin))
    return pins, members



def _others(members, net, ref, pin):
    return sorted(f'{r}.{p}' for r, p in members.get(net, []) if (r, p) != (ref, pin))


def u9_jobs(pins, members, value=None):
    """U9's pins and the jobs, as data/u9-nets.json holds them."""
    if 'U9' not in pins:
        raise ValueError('the redrawing has no U9')
    u9 = pins['U9']
    out_pins = {}
    for pin, (net, kind) in sorted(u9.items(), key=lambda kv: str(kv[0])):
        entry = {'net': net, 'type': kind}
        if net not in POWER:
            entry['with'] = _others(members, net, 'U9', pin)
        out_pins[str(pin)] = entry

    def side(pin, want, must):
        net, kind = u9[pin]
        got = _others(members, net, 'U9', pin)
        on = {g.split('.')[0] for g in got}
        return {'pin': pin, 'net': net, 'type': kind, 'with': [g for g in got if g.split('.')[0] in must]}, kind == want and set(must) <= on

    jobs = []
    for j in JOBS:
        i, i_ok = side(j['in'], 'input', j['inWith'])
        o, o_ok = side(j['out'], 'output', j['outWith'])
        jobs.append({'job': j['job'], 'in': i, 'out': o, 'holds': bool(i_ok and o_ok)})
    return {'value': value, 'pins': out_pins, 'jobs': jobs}


def main():
    path = common.original('I2')
    pins, members = read_nets(path)
    fps = common.kicad_footprints(path)
    result = u9_jobs(pins, members, fps['U9']['value'])
    result['about'] = ('Issue 81, 13 October 2026, replacing the by-hand reading of 5 October: U9, the hex inverter, in OpenTendo\'s KiCad redrawing of '
                       'the board (I2, checked against its SHA-256 in sources.json). For each pin of U9 its net, its pin type and every other '
                       'part\'s pin on that net (not for the power pins); then each job the page names, with the inverter it uses (an input '
                       'pin and an output pin), and `holds` true when those pins are the right way round and the parts it must reach are on '
                       'their nets. A cross-check, not the scan: the redrawing\'s U9 is a 74LS04, the boards photographed carry a 74HCU04. '
                       'Written by board_nets.py')
    result['source'] = {'id': 'I2', 'sha256': common.source('I2')['sha256']}
    for j in result['jobs']:
        print(f"{'holds' if j['holds'] else 'FAILS'}: U9 {j['job']} (pin {j['in']['pin']} on {j['in']['net']}, pin {j['out']['pin']} on {j['out']['net']})")
    common.write_data('u9-nets.json', result)
    return 0 if all(j['holds'] for j in result['jobs']) else 3


if __name__ == '__main__':
    sys.exit(main())
