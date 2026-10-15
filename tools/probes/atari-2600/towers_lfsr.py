"""Checks tia.md section 3's rule for the TIA's horizontal sync counter against Towers's table.

The rule: written as Towers prints the six bits, each step shifts them one place right and puts
a new bit on the left, 1 when the two rightmost bits are equal and 0 when they differ. Stepped
from 000000, every state must be the next row of the table in Andrew Towers's TIA hardware
notes (TIA_HW_Notes.txt, given as the argument). Prints each mismatch and a count.

    python3 -I tools/probes/atari-2600/towers_lfsr.py TIA_HW_Notes.txt
"""
import re
import sys

rows = []
in_table = False
for line in open(sys.argv[1], encoding='latin-1'):
    if line.startswith('Value\tHCount'):
        in_table = True
        continue
    if in_table:
        if line.startswith('111111'):  # the error state ends the table
            break
        m = re.match(r'^([01]{6})\t', line)
        if m:
            rows.append(m.group(1))
# The table runs 000000 to 000001 and then repeats 000000 (the "(cycle)" row); drop the repeat.
states = rows[:-1] if rows and rows[-1] == rows[0] else rows

state = '000000'
mismatches = 0
for i, expected in enumerate(states):
    if state != expected:
        mismatches += 1
        print(f'row {i}: the rule gives {state}, the table {expected}')
    new = '1' if state[4] == state[5] else '0'
    state = new + state[:5]
print(f'{len(states) - mismatches} of {len(states)} rows match; after the last row the rule gives {state}')
