# 6502

A cycle-accurate 6502 core in C#, covering the original NMOS 6502 and the CMOS
65C02, and the aim of implementing as many 6502-family machines as possible on
it, each one proven: the KIM-1 first, then the BBC Micro, then the NES, and
more.

This is built in public. The design, the plan and every step of the work are
in this repository, and the journey is written up at
[6502.dbhq.uk](https://6502.dbhq.uk/).

## Where this is going

As many 6502-family machines as possible, each one running in the browser and
proven by an automated test. The core comes first, then the KIM-1, the BBC Micro
and the NES. The design is in
[`docs/superpowers/specs/2026-09-29-6502-design.md`](docs/superpowers/specs/2026-09-29-6502-design.md).

## Where it stands

The core is done, in all five variants, and proven against every check the
design lists. `dotnet test` is the proof; the numbers live in its output.
The KIM-1, the first machine, runs its original monitor ROM, passes its
acceptance test, and runs in the browser at
[6502.dbhq.uk/machines/kim-1/](https://6502.dbhq.uk/machines/kim-1/), so it
counts as implemented. The BBC Micro Model B, the second, runs Acorn's own
operating system, BBC BASIC and disc filing system, passes its acceptance test,
and runs in the browser at
[6502.dbhq.uk/machines/bbc-micro/](https://6502.dbhq.uk/machines/bbc-micro/),
with its keyboard, sound and a disc drive, so it counts too. The NES, NTSC and
PAL, is next: its design, plan and fact sheets are in the repository, and it is
built task by task. The programme's
design is in
[`docs/superpowers/specs/2026-09-30-machines-and-site-design.md`](docs/superpowers/specs/2026-09-30-machines-and-site-design.md).

## What "cycle-accurate" means here

The 6502 does exactly one read or one write on its bus every clock cycle. The
core reproduces every one of them, in order, including the reads whose result
the chip throws away. The other chips in a machine run in step with it, one
cycle at a time.
Where a machine's model is deliberately coarser than a cycle (the BBC Micro's
video path reads screen memory a line at a time and applies a register write
from the next character clocked), [`docs/known-differences.md`](docs/known-differences.md)
says exactly what is and is not cycle-exact.

That is checked, not claimed: the core is tested against Tom Harte's
SingleStepTests, which record the bus activity of every opcode cycle by cycle.

## Licence

MIT. See [`LICENSE`](LICENSE). Some files here are somebody else's, or made
from somebody else's, and keep their own terms: the system ROMs in
[`roms/`](roms/README.md); the photographs of the original machines in
[`site/src/assets/photos/`](site/src/assets/photos/README.md), each under the
licence its source gives it (CC BY-SA 2.0 fr, share-alike, for the Musée Bolo's
KIM-1, none stated for the two from Hans Otten's site, and CC BY 2.0 for the
BBC Micro); the KIM-1 model's track
map, `site/src/assets/tracks/kim-1.webp`, traced from those photographs and
from a replica of the board's layout, whose channels carry those terms and
CC BY-NC 4.0 (the photographs' README says which); and the files in
[`tools/kim1-model/data/`](tools/kim1-model/data/README.md) drawn from that
replica, which are CC BY-NC 4.0. No game and no third-party test program is
committed; the tests download what they need, pinned by hash.
