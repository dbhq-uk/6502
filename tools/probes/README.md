# Probes

Scripts that check an assumption against test data before code depends on
it. They are not part of the test suite: they answer "is this what the data
says?" while a plan is being written, and they stay here so the answer can be
checked again by anyone.

| Script | What it checks |
|---|---|
| `check_harte_findings.py` | The behaviours the core's plan assumes, against every case in the relevant Harte files: the unstable NMOS opcodes, decimal mode on every variant, status bits 4 and 5, `JAM`, the 65C02's bus patterns, and the undefined 65C02 opcodes |
| `kim1_serial_routines.py` | The KIM-1 monitor's teletype routines, disassembled from the hash-checked ROM in `roms/kim-1/`: reset's speed measurement, GETCH, OUTCH, the delay loops and the commands, each labelled |

Run from the repository root with Python 3 and no other dependencies. Data is
downloaded from a pinned commit into `.testdata/`, which git ignores.
