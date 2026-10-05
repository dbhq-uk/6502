---
title: "Planning the NES"
date: 2026-10-05
summary: "The third machine is the NES, in both its NTSC and PAL forms. Before any of its code, six fact sheets were written from the nesdev wiki, and every number the plan had written from memory was checked against them. Most held. The PAL frame counter's last step was one cycle out, the DMA cycle counts were stated too simply, the test ROM signature could not be confirmed, and the PPU test ROM the plan meant to run first turned out to need a mapper that arrives six tasks later. The project, its test project and the test ROM pins went in with the sheets."
order: 29
---

# 5 October 2026: planning the NES

The design was settled today, in
`docs/superpowers/specs/2026-10-05-nes-design.md`, and the plan is
`docs/superpowers/plans/2026-10-05-nes.md`. This entry records what task 1 of
the plan found and did: six fact sheets in `docs/nes/facts/`, the corrections
they made to the plan, and the first code, which is the project, its test
project and the pins for the test ROMs.

## Two decisions from the design

The design's table has six decisions. Two of them changed the shape of the work
after the first draft, and both are Dan's calls of 5 October.

- **PAL is in this machine, with NTSC.** It was chosen over NTSC only. The PAL
  console is a different timing, not a different machine: its PPU runs 3.2 dots
  for every CPU cycle rather than 3, its frame is 312 lines rather than 262, and
  its sound unit has its own tables. So the bus's tick is a ratio per region and
  not a fixed three, and a `Region` value holds every number that differs, so no
  chip tests for a region by name. Every test that touches timing runs for both.
- **The 3D models are in scope, but are their own project.** They were chosen
  over putting the models in this design, and over leaving them out. The BBC
  Micro's models were their own design and plan, and Dan ruled on 2 October that
  models never hold up a count. The NES is cased, so it gets two models or none,
  built by another agent in a second pull request.

## The plan's form, and why

The plan has sixteen tasks, one commit each, on the branch `implement-nes`. Like
the BBC Micro's, and unlike the core's, it does not carry the code. The PPU and
the sound unit are thousands of lines, and writing them into a document that
nothing has run would be a guess at the code rather than a plan for it. So each
task gives its interfaces, the facts it is built from and the exact test
assertions, a fresh agent writes the tests first and the code to pass them, and
a reviewer gates each task before the next starts.

What is proven early is the risky part. Task 3 runs `nestest` through the real
bus, and its log also checks the PPU's dot counter. Task 6 measures the speed in
a browser before the sound and the mappers exist, because a PPU stepped one dot
at a time inside every CPU cycle is about 5.4 million dots a second and the
browser has to keep up. A draft pull request opens after this task so that CI
runs on every push.

## The fact sheets

The chips are written from the nesdev wiki through the sheets, never from
another emulator's code: Mesen, FCEUX and Nestopia are GPL and this repository is
MIT. Six sheets came first: timing, the bus, the PPU, the sound unit, the
mappers and the cartridge file. Every statement names the wiki page it comes
from, anything not settled is marked `[guessing - verify]`, and each behaviour
has a worked example a test can be taken from.

**How the wiki was read.** The wiki refuses automated fetches with a Cloudflare
challenge, and a browser was blocked too. So each page was read from its newest
copy in the Internet Archive that held the page rather than the challenge. Each
copy names the wiki revision it is, and the sheets' README lists them, so the
exact text can be opened again. The Archive went offline for part of the
afternoon, which delayed two pages (the noise channel and the sweep) but lost
none.

## What the sheets changed in the plan

The plan's list of "facts every task relies on" was written from memory and
marked as a guess. Each item was checked against its page. These held: the
clocks, the frame sizes, the VBlank lines, the NTSC frame counter, the noise
periods and the DMC rates in both regions, and the PPU's position at reset as
`nestest.log` shows it. These did not, and the list now says so in place:

- **The PAL frame counter's last step was one cycle late.** Memory had 33253 to
  33255 CPU cycles; the wiki gives the step in APU cycles, 16626 and 16627, which
  is 33252 to 33254. The NTSC figures converted the same way match what memory
  had, which is the check that the conversion is right.
- **OAM DMA's extra cycle is not about odd cycles as such.** It is 514 cycles
  when the halt falls on a "get" half of the APU's clock and 513 otherwise, and
  which CPU cycles are gets is random at power-on. "Odd cycles cost one more" is
  one of the two consoles, so it stays as the model's choice and is written down
  as one.
- **DMC DMA does not simply steal "up to 4" cycles.** The first fetch after
  enabling normally takes 3, later ones 4, a fetch inside OAM DMA 2, and one at
  its very end 1 or 3; a CPU write delays it.
- **The test ROM signature could not be confirmed.** The readme in the fork
  writes the bytes at `$6001` to `$6003` as `$DE $B0 $G1`, and `$G1` is not
  hexadecimal. Memory had `61`. Task 4 reads the real bytes from a running ROM.
- **The PPU test ROM needs a mapper that comes six tasks later.** The combined
  `ppu_vbl_nmi.nes` is an MMC1 cartridge (256 KB of program, mapper 1), and MMC1
  is task 10, but task 4 runs it. Its ten single tests are plain NROM
  cartridges, so task 4 now pins and runs those, and the combined ROM moves to
  task 12, after MMC1. It is still pinned here, as this task's ruling asked.
- **Two test ROM folders the design names are not in the fork.** There is no
  `ppu_sprite_hit` or `ppu_sprite_overflow`; the older
  `sprite_hit_tests_2005.10.05` and `sprite_overflow_tests` are there, and the
  plan already uses those. `mmc3_test_2` has only single tests.

Two further findings are for later tasks rather than corrections:

- **The PAL picture is not 8:7.** The design gives the screen an 8:7 pixel shape
  for 4:3. That is NTSC's; the wiki gives the PAL pixel as about 1.386 to 1
  (Overscan page). Task 14 decides whether the page shows PAL at its own shape.
- **The PAL PPU blanks part of the picture.** The 2C07 draws a black border over
  the top line and two pixels at each side, which the NTSC PPU does not.

The plan's task 14 now shows the picture in each region's own pixel shape, 8:7
on NTSC and about 1.386:1 on PAL, and tests it.

## The PAL dot ratio

The PAL PPU runs 3.2 dots for each CPU cycle, which is 16 dots in every 5
cycles. The wiki gives the ratio but not which of the five cycles gets the
fourth dot. It follows from the two clock dividers: the CPU takes 16 master
clocks a cycle and the PPU 5 a dot, so in 80 master clocks there are 5 cycles and
16 dots, and if a cycle and a dot start together the cycles complete 3, 3, 3, 3
and then 4 dots. Another starting phase moves the 4, and there is always exactly
one. A whole-number accumulator that adds 16 each cycle and runs a dot for every
5 gives the same pattern from zero. Which phase a real console powers up in is
not known, so the model's choice will go in `docs/known-differences.md` when task
3 makes it.

## Task 1

The task added `src/Dbhq.Machines.Nes/`, an empty library on the core, and
`tests/Dbhq.Machines.Nes.Tests/`, both in the solution. The test ROMs are not
committed (AGENTS.md rule 3): `NesTestRoms.Read` fetches a file from the fork
`dbhq-uk/nes-test-roms` at a pinned commit, the same commit `nestest` already
came from, and checks it against its SHA-256 in `Pins.NesTestRomHashes`, which is
keyed by the file's path in the fork. Two are pinned: `other/nestest.nes`, whose
hash is the existing `NestestRomSha256` so the two can never disagree, and
`ppu_vbl_nmi/ppu_vbl_nmi.nes`, hashed with `sha256sum` on a fresh download.

The test was written first and failed to compile, because neither `NesTestRoms`
nor the pins existed. It checks that every pinned file matches its hash and
starts with the iNES magic `4E 45 53 1A`, that both ROMs tasks 3 and 4 need are
pinned, that nestest has one hash and not two, and that a name that is not
pinned is refused. Changing one character of the PPU ROM's pin made the test
fail with the download's hash mismatch, which is the check that the test reads
the pin.

## Mistakes

The review of this task found two worked examples in `ppu.md` that a test would
have been wrong to copy. One gave the pattern fetch addresses for `v` = `$2000`
as if fine Y were 0, but in the rendering layout `$2000` is fine Y = 2, so the
addresses were 2 too low; it now uses `v` = `$0000` and shows both. The other
said a sprite at X = 4 with the left columns hidden makes no hit, but an 8-pixel
sprite there reaches column 11, so the hit comes at column 8. Both were worked
by eye from the rules rather than computed from the bit layout. Every worked
example in the sheet was then recomputed from the layout, and the rest held.

## Task 2: the region, the cartridge and NROM

The second task added `Region`, the cartridge reader, the board interface and
the first board, NROM, with 71 tests. They were written first and failed to
compile, because none of the types existed.

**The region holds every number that differs.** Lines (262 and 312), the
pre-render line, the dropped odd-frame dot, the dot ratio as a numerator and a
denominator (3 over 1 and 16 over 5), the CPU clock, the noise and DMC tables,
the frame counter's steps and the PAL red and green swap. The CPU clock is
computed from the master clock the sheet gives (236.25 MHz over 11 and over 12;
26.6017125 MHz over 16), and the frame rate is computed from the clock, the
ratio and the dots in a frame, so no rate is typed. The test recomputes both
from the master clock and checks them to 0.001 of a frame a second. They come
out near the sheet's 60.0988 and 50.0070, which checks the formula.

**The frame counter lists follow the sheet's table row by row, not the plan's
shorter list.** The plan's facts list gave four entries for each mode. The
table in `apu.md` section 10 has six rows for each, because the 4-step mode
sets the IRQ flag on three consecutive cycles and the 5-step mode has a step
with no clock. So the NTSC 4-step list is 7457, 14913, 22371, 29828, 29829,
29830 and the 5-step list is 7457, 14913, 22371, 29829, 37281, 37282. PAL is
the same shape. The last entry of each is where the sequence wraps. The plan's
four numbers are all in the lists.

**The header fields.** Byte 6 gives the mirroring (bit 0 set is vertical,
bit 3 is four-screen and wins), the battery (bit 1), the trainer (bit 2) and
the low mapper nibble. Byte 7 gives the high nibble and, with bits 3 and 2 equal
to `10`, marks NES 2.0. A NES 2.0 header adds a third mapper nibble and the
submapper in byte 8, the high size nibbles in byte 9, the RAM sizes in bytes
10 and 11 and the timing in byte 12. A trainer is skipped, not refused, as it
costs nothing to skip. The high mapper nibble of an iNES file is ignored when
bytes 12 to 15 are not zero, because old tools wrote text there ("DiskDude!")
that adds 64 to the mapper. That is the sheet's first choice of the two it
offers (the other was to refuse the file).

**The iNES 1 region flag is not read.** `cartridge.md` quotes the iNES page:
very few emulators honour it, because virtually no image sets it. A flag that
is nearly always zero says NTSC for a PAL game, which would be wrong in the way
that matters. So an iNES file names no region and the page starts at NTSC and
lets the visitor choose. NES 2.0 byte 12 is read, where the format means it:
0 is NTSC, 1 is PAL, 2 is "either" and so names no region.

**Dendy is refused, in the header, with its name.** A NES 2.0 file with timing 3
throws a `NesFormatException` that says the cartridge is for the Dendy. It was
chosen over running it as PAL, which would be a quiet wrong speed (the Dendy
runs three dots a cycle on a PAL frame), and over a fifth region, which the
spec puts out of scope. No licensed game uses the value, so a refusal costs
almost nobody anything.

**The size limit is 4 MB, one constant in `Cartridge`.** It is sixteen times
the largest ROM the tests use and far over what any board this machine models
holds, and it bounds what a hostile or mistaken file can make the page copy. The
check is the first thing `Load` does after the empty test, before the header is
read. The NES 2.0 sizes can ask for tens of megabytes, so they are checked
against the file's length one at a time before they are added, and an exponent
form size that does not fit in 64 bits is treated as too big. The first version
of that check added the sizes first, and the test with an exponent of 63
overflowed the sum and reached `Span` with a bad length. The test found it. Each
refusal is one plain sentence, and the tests check that each has a single full
stop and no dash, because the page shows them as written.

**Two sizes could not be tested the way the brief said.** A NES 2.0 PRG with
the high size nibble set starts at 256 banks of 16 KB, which is 4 MB, so it is
over the limit and cannot be loaded. The test uses the CHR nibble instead (256
banks of 8 KB, 2 MB). And the unsupported mapper is refused by `CreateMapper`
and not by `Load`, so a header can be read, and its fields tested, for any
mapper number. The message names the mapper and lists the ones that are
modelled, from one array that each later board's task extends.

**PRG RAM.** An iNES header's byte 8 counts 8 KB units and the format says 0
means 8 KB. That matters: the test ROMs report their result at `$6000` and their
headers have byte 8 equal to 0. So an iNES cartridge has 8 KB of PRG RAM unless
it says more, and a NES 2.0 one has what bytes 10 and 11 say, which can be none,
and then `$6000` is open bus. A RAM smaller than the 8 KB window repeats through
it. A NES 2.0 file with no CHR ROM and no CHR RAM has no character memory, as
`cartridge.md` section 2 says, and reads of it give 0 and writes are dropped
rather than failing.

**What was not done.** The sheet's rule that a NES 2.0 header whose sizes do not
fit the file may be an iNES one is not applied: such a file is refused as
shorter than its header says. The Vs. System and PlayChoice console types are
read as ordinary cartridges.
