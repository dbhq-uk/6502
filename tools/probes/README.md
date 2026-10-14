# Probes

Scripts that check an assumption against test data before code depends on
it. They are not part of the test suite: they answer "is this what the data
says?" while a plan is being written, and they stay here so the answer can be
checked again by anyone.

| Script | What it checks |
|---|---|
| `check_harte_findings.py` | The behaviours the core's plan assumes, against every case in the relevant Harte files: the unstable NMOS opcodes, decimal mode on every variant, status bits 4 and 5, `JAM`, the 65C02's bus patterns, and the undefined 65C02 opcodes |
| `atari-2600/` | The Atari 2600's test suite, read from clones of its two forks at the pinned commits (task 1 of the Atari 2600 plan): `headers.sh` prints each test's header, `regs.sh` counts the registers each source names, `counts.sh` counts the tests by folder and kind, `filler.py` applies the Superchip filler rule to binaries, and `pnginfo.py` decodes the reference pictures. `manifest-mutations.sh` plants one fault at a time in a scratch copy and shows each `SuiteManifestTests` test fail. The results are in `docs/atari-2600/facts/suite.md` and `docs/journal/2026-10-14-the-atari-2600-fact-sheets.md` |
| `bbc_models_mutations.py` | That the BBC Micro models' tests notice each of the faults task 10 of the models plan lists, and more: plants one at a time in the models' data, modules, tools, credits, the page's wiring, the browser check's node-testable parts and the machine's counters, runs the site suite (after a build), the tools' pytest and, for C#, the BBC Micro's test project, reports what failed and puts the files back. Run it on a scratch worktree with `--root`; the results are in `docs/journal/2026-10-04-the-bbc-micro-models.md`, "Task 10" |
| `electron_mutations.py` | That the Electron's tests notice each of the faults task 15 of the Electron plan lists: plants one at a time, runs the Electron test project (and the site's key test where the page's copy of the matrix changes), reports what failed and puts the files back. Run it in a scratch worktree; the results are in `docs/journal/2026-10-13-the-electron-mutation-pass.md` |
| `kim1_serial_routines.py` | The KIM-1 monitor's teletype routines, disassembled from the hash-checked ROM in `roms/kim-1/`: reset's speed measurement, GETCH, OUTCH, the delay loops and the commands, each labelled |
| `kim1_teletype_mutations.py` | Plants 22 faults, one at a time, in the KIM-1 teletype (the machine) and its page, runs the tests that should notice, puts each file back, and reports which tests failed or that the fault survived. Unlike the others it runs tests and takes minutes; it is how the journal's mutation tables were made |

Run from the repository root with Python 3 and no other dependencies. Data is
downloaded from a pinned commit into `.testdata/`, which git ignores.
