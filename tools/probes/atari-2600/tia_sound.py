"""Makes tia.md section 18.5's table of the TIA's sound, and checks it against Korth's drawings.

Steps the noise-tone generator of tia.md section 18.4 (restated there from the jigo2600
project's written analysis of the TIA schematics; no emulator code) for each AUDC value, from
the sheet's chosen start of all nine bits 1, with AUDF = 0 so each divided pulse is one step.
Prints each value's period, the 1s in one period after start-up, and the first 64 output bits.

Given Korth's 2k6specs as text (the argument), it also checks each of his "AUDC Pattern Shapes"
drawings (70 steps, '-' high and '_' low) against the model: a drawing must be a stretch of
the model's repeating sequence, or of its inverse, and his repeat length must be the period.

    python3 -I tools/probes/atari-2600/tia_sound.py [2k6specs.txt]
"""
import re
import sys

SETTLE = 600  # steps run before the period is measured; longer than any start-up


def value(c, lo, hi):  # c[lo] is the least significant bit
    return sum(c[i] << (i - lo) for i in range(lo, hi + 1))


def step(c, audc):
    """One divided pulse. c[0..4] is poly5 (c[0] newest), c[5..8] poly4 (c[5] newest); every new bit
    is computed from the state before the pulse."""
    low, high = audc & 3, audc >> 2
    if audc == 0:
        n0 = 1
    elif low == 0:
        n0 = (c[8] ^ c[4]) | (value(c, 0, 8) == 0)
    else:
        n0 = (c[2] ^ c[4]) | (value(c, 0, 4) == 0)
    if low in (0, 1):
        clock4 = True
    elif low == 2:
        clock4 = value(c, 0, 4) in (8, 24)
    else:
        clock4 = c[4] == 1
    if audc == 0:
        n5 = 1
    elif high == 0:
        n5 = (c[7] ^ c[8]) | (value(c, 5, 8) == 0)
    elif high == 1:
        n5 = 1 - c[5]
    elif high == 2:
        n5 = c[4]
    else:
        n5 = (1 - c[7]) | (value(c, 5, 7) == 5)
    new = [int(n0)] + c[0:4] + c[5:9]
    if clock4:
        new[5:9] = [int(n5)] + c[5:8]
    return new


def run(audc, n):
    c, out = [1] * 9, []
    for _ in range(n):
        c = step(c, audc)
        out.append(c[8])
    return out


def period(seq):
    tail = seq[SETTLE:]
    return next(p for p in range(1, SETTLE) if all(tail[i] == tail[i + p] for i in range(len(tail) - p)))


def bits(seq):
    return ''.join(map(str, seq))


drawings = {}
if len(sys.argv) > 1:
    text = open(sys.argv[1], encoding='latin-1').read()
    block = text[text.index('AUDC Pattern Shapes'):text.index('AUDC Pattern Type Notes')]
    for m in re.finditer(r'^\s+([0-9A-F])\s+(\d+)\s+([-_]+)\s*$', block, re.M):
        drawings[int(m.group(1), 16)] = (int(m.group(2)), m.group(3).replace('-', '1').replace('_', '0'))

for audc in range(16):
    seq = run(audc, 3000)
    p = period(seq)
    line = f'AUDC ${audc:X}: period {p}, 1s per period {sum(seq[SETTLE:SETTLE + p])}, first 64 {bits(seq[:64])}'
    if audc in drawings:
        rep, drawn = drawings[audc]
        cycle = bits(seq[SETTLE:SETTLE + p]) * (len(drawn) // p + 2)
        inverse = cycle.translate(str.maketrans('01', '10'))
        found = 'a stretch of the model' if drawn in cycle else 'a stretch of its inverse' if drawn in inverse else 'NOT FOUND'
        line += f'; 2k6specs repeat {rep} ({"same" if rep == p else "DIFFERENT"}), drawing {found}'
    print(line)
