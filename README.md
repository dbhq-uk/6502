# 6502

A cycle-accurate 6502 core in C#, covering the original NMOS 6502 and the CMOS
65C02, and the aim of implementing as many 6502-family machines as possible on
it, each one proven: the KIM-1 first, then the BBC Micro, then the Acorn
Electron, the Atari 2600, the NES and the Commodore 64, and more.

This is built in public. The design, the plan and every step of the work are
in this repository, and the journey is written up at
[6502.dbhq.uk](https://6502.dbhq.uk/).

## Where this is going

As many 6502-family machines as possible, each one running in the browser and
proven by an automated test. The core comes first, then the KIM-1, the BBC Micro,
the Acorn Electron, the Atari 2600, the NES and the Commodore 64. The design is in
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
with its keyboard, sound and a disc drive, so it counts too. Its page offers a
library of homebrew and freely licensed discs to start with one click, each
kept in [`machines/bbc-micro/discs/`](machines/bbc-micro/discs/) with its
author's licence and, where the licence asks for it, its source; a disc image
of your own goes in as before. The NES, the third, NTSC and PAL, passes its
acceptance test and runs in the browser at
[6502.dbhq.uk/machines/nes/](https://6502.dbhq.uk/machines/nes/), with its
picture, sound, two controllers and six cartridge boards, starting with a
public-domain game, Lan Master, so it counts as well; a game of your own goes
in as a file and is never uploaded. Its page also has two 3D models of the
console, the outside (the case) and the inside (the main board, showing which
chips the processor is talking to), each drawn for the NTSC and the PAL
console. They are measured from photographs, a design patent and scans of a bare
board, and the note under each says how well each part is known. The Acorn
Electron, the Atari 2600 and the Commodore 64 come next, one at a time and
smallest first. The programme's design is in
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
KIM-1, none stated for the two from Hans Otten's site, CC BY 2.0 for the
BBC Micro, public domain for the NTSC NES's two and CC BY 4.0 for the PAL
NES's two); the KIM-1 model's track map, `site/src/assets/tracks/kim-1.webp`,
traced from those photographs and from a replica of the board's layout, whose
channels carry those terms and CC BY-NC 4.0 (the photographs' README says
which); the files in
[`tools/kim1-model/data/`](tools/kim1-model/data/README.md) drawn from that
replica, which are CC BY-NC 4.0; and the NES board model's track map,
`site/src/assets/tracks/nes-famicom-board.webp`, traced from the scans of a bare
board in OpenTendo (read from its fork,
[`dbhq-uk/OpenTendo`](https://github.com/dbhq-uk/OpenTendo)), and so offered on
the TAPR Open Hardware License's terms, which OpenTendo states
([`NOTICE.md`](NOTICE.md) says what is derived). No commercial game is committed. What is
committed, the BBC Micro's preset discs (games, demos and tools, one of them a
disc of Dormann's and Clark's tests) and the NES's Lan Master, is free software
and homebrew whose authors let it be shared, each with its licence recorded
beside it ([`NOTICE.md`](NOTICE.md), [`roms/README.md`](roms/README.md)). The
tests download the test programs they run, pinned by hash.
