# Probes

Scripts that check an assumption against test data before code depends on
it. They are not part of the test suite: they answer "is this what the data
says?" while a plan is being written, and they stay here so the answer can be
checked again by anyone.

| Script | What it checks |
|---|---|
| `check_harte_findings.py` | The behaviours the core's plan assumes, against every case in the relevant Harte files: the unstable NMOS opcodes, decimal mode on every variant, status bits 4 and 5, `JAM`, the 65C02's bus patterns, and the undefined 65C02 opcodes |
| `electron_mutations.py` | That the Electron's tests notice each of the faults task 15 of the Electron plan lists: plants one at a time, runs the Electron test project (and the site's key test where the page's copy of the matrix changes), reports what failed and puts the files back. Run it in a scratch worktree; the results are in `docs/journal/2026-10-13-the-electron-mutation-pass.md` |

Run from the repository root with Python 3 and no other dependencies. Data is
downloaded from a pinned commit into `.testdata/`, which git ignores.
