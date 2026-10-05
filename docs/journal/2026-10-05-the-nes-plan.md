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

**RAM sizes are capped at 64 KB each (a review finding).** The first version took
the PRG RAM and CHR RAM sizes from the header alone. A 16 KB NES 2.0 file with
bytes 10 and 11 at `$FF` named 2 MB of each kind, and the board made its own copy
of the CHR RAM as well, so about 12 MB was allocated for a 16 KB file. Now
`Cartridge.MaxRamSize` is 64 KB. An iNES byte 8 over the cap is clamped to it, so
a junk byte 8 does not stop a playable file loading. A NES 2.0 size over the cap
is refused with a plain sentence, because that header states its sizes on
purpose. The cartridge keeps the size of its CHR RAM, and makes its block of
zeros only if `Chr` is read, so the board's copy is the only one in use.

## Task 3: the bus, the machine and nestest through the real bus

The third task added `NesBus`, `Nes` and `NesOptions`, with the PPU and the
sound unit as private stubs that tasks 4 and 8 replace. The tests were written
first and failed to compile, because the types did not exist. `nestest.nes` then
ran through the whole machine, in both regions, and every line of its log
matched on the first run.

**What the log's PPU column proved.** The log's first line is
`PPU:  0, 21 CYC:7`. The bus counts dots on its own, and the CPU's reset takes
seven bus cycles, so after `PowerOn` the counter reads 21, which is line 0 dot
21. All 8,991 lines then agree on line and dot, with the dots counted by the
bus and not computed from the CPU's cycle count. So the dot counter, the
three-to-one ratio and the reset alignment (the PPU at line 0 dot 0 at cycle 0,
running from the first reset cycle) are right on NTSC. The test also checks that
the bus's cycle count equals the CPU's on every line. The log is Nintendulator's
and not a real console's, so this proves agreement with a good emulator and
nothing more.

**The PAL ratio is kept in whole numbers.** Each cycle adds 16 to an
accumulator, and one dot runs for every 5 it holds, with the remainder kept.
From zero that gives 3, 3, 3, 3, 4, which the bus test checks, and the 7 reset
cycles run 22 dots. NTSC uses the same code with 3 over 1. The bus never asks
which region it is: it reads the numerator and denominator from `Region`.
nestest's log has no PAL column, so on PAL the test compares the dot counter to
`CYC * 16 / 5` in integers. That checks the accumulator against the ratio, not
against a PAL console, and the sheet says the phase a real console powers up in
is not known.

**One method runs a cycle.** `Cycle` is the only place that counts a cycle. It
runs the dots, ticks the sound unit, makes the access, then sets the CPU's NMI
and IRQ lines from the chips. The lines are set after the access, so a write
that changes a line is seen in the same cycle's sample, which is what the CPU
core does with the bus. The first version runs all the dots and then the
access. Where the dots fall around the access is task 4's decision, made
against `ppu_vbl_nmi`, and it is all inside this one method. The first draft
passed a closure for the access. It allocated on every cycle, so it became
arguments instead.

**Open bus is an assumption.** The bus keeps the last value that crossed it, and
a read of nothing returns it, so a write of `$AB` to `$5000` followed by a read
of `$5001` gives `$AB`. The sheet says the same, and its worked example
(`LDA $5000` gives `$50`) follows because the CPU's last read was the operand's
high byte. A read of `$4015` is the exception: it is internal to the CPU and
leaves the latch alone. The controller ports drive bits 4 to 0 and leave 7 to 5
to the latch, as the sheet says, and the pads themselves arrive in task 7.
Nothing here is checked against hardware. The PPU's own I/O latch is a different
thing, and the stub PPU simply returns the bus latch until task 4.

**Decisions.**
- Writes to `$4016` strobe both pads, and writes to `$4017` go to the frame
  counter, as `bus.md` section 7 and the register table say. Both are stubbed.
- `PpuDots` is a permanent counter on the bus, only ever reset by power on, so
  task 4's PPU can keep its own position and this stays the total.
- Power on clears RAM to zero. The real pattern is undefined. Zeros were chosen
  over a pseudo-random fill because a test then reads the same thing every time.
  It is in `known-differences.md`.
- `PowerOn` clears the board's PRG RAM and `Reset` keeps it. A board's own
  registers are not cleared by `PowerOn`, because the interface has no reset and
  NROM has none. Task 10 adds one when the first board with registers needs it.
- The constructor builds the board, so it can throw `NesFormatException` for a
  mapper the machine does not model. A caller shows a visitor's file, so it
  catches the exception from `Cartridge.Load` and from the constructor.

## Task 4: the PPU's registers, memory and timing

The fourth task added `Ppu`: the eight registers with `v`, `t`, `x` and `w`, the
`$2007` read buffer, the palette and nametable RAM, OAM, the VBlank flag and
NMI, and the frame with its odd-frame dot. It draws nothing yet. The bus now
owns it in place of the stub, and the order of the dots inside a CPU cycle was
measured, not chosen. Blargg's ten `ppu_vbl_nmi` singles are pinned and all ten
pass on NTSC.

**The tests went first.** The runner and the ten singles were written before the
PPU and run against the stub: all ten timed out with `$6000` at `$80`, because
the stub never sets the VBlank flag the tests wait for. The register and timing
tests failed to compile, because `Ppu` did not exist.

**The order inside a cycle.** Two things were varied: how many of a cycle's
dots run before the access (0 to 3), and at which point in the cycle the PPU's
NMI output becomes the line the CPU sees (the start of the cycle, after the dots
before the access, after the access, or the end). Each of the sixteen was run
against the ten singles (`dotnet test tests/Dbhq.Machines.Nes.Tests -c Release
--filter "FullyQualifiedName~BlarggTests"`, 5 October 2026):

| Dots before | NMI line taken | Passed | Failing (status) |
|---|---|---|---|
| 0, 1 or 3 | start, after the dots before | 6 | 05, 06, 07, 08 (1) |
| 0, 1 or 3 | after the access, end | 5 | 04 (11), 05, 06, 07, 08 (1) |
| 2 | start | 10 | none |
| 2 | after the dots before | 6 | 05, 06, 07, 08 (1) |
| 2 | after the access | 5 | 04 (11), 05, 06, 07, 08 (1) |
| 2 | end | 8 | 04 (11), 05 (1) |

The plan's first version, two before and one after with the line taken at the
end of the cycle, passed eight. Test 4 said "Immediate occurence should be after
NEXT instruction", and test 5 printed `3 3 3 3 3 3 2 2 2 2` where the readme
gives `4 4 4 3 3 3 3 3 3 2`. Both say the CPU saw the NMI one cycle early. The
core already had the answer: its interrupts were checked against the
transistor-level model with "the line changed at the start of a cycle", so a
line that changes during cycle N belongs to cycle N + 1. Taking the lines as the
cycle begins passes all ten, and it is the only row that does. The IRQ line is
taken at the same point. Nothing holds it yet, so task 8 checks that.

**The PAL pattern.** The same rule gives two dots before the access and the rest
after. From a zero accumulator the fourth dot falls in the fifth cycle of every
five (3, 3, 3, 3, 4), so that cycle runs two after. No test ROM checks the PPU on
PAL to the dot, so this is the NTSC rule carried over, not a measurement, and
`known-differences.md` says so.

**The odd frame's dot.** The first version sampled rendering when dot 339 of
the pre-render line ran. Test 10 failed with code 3, "Clock is skipped too late,
relative to enabling BG". Sampling at dot 337 failed with code 2, "too soon".
Dot 338 passes. The model applies a `$2001` write at once, and the sheet says the
chip takes 3 to 4 dots, so 338 may be a later sample plus that delay. The test
cannot tell them apart, and the sheet records it as measured.

**How long the singles run.** Each single was run once more through
`BlarggRunner.Run` by a throwaway test that printed `BlarggResult.Cycles` (5
October 2026, Release build). From power on to the result: 01 4,230,162 cycles,
02 5,332,051, 03 5,064,021, 04 984,069, 05 6,553,059, 06 6,642,399, 07
5,897,881, 08 6,612,617, 09 2,324,202, 10 4,259,942. The test's budget is 18
million, so the slowest uses about a third of it.

**Edge cases found.**
- The signature at `$6001` to `$6003` is `$DE $B0 $61`. It was read from the ROM
  (`A9 DE 8D 01 60 A9 B0 8D 02 60 A9 61 8D 03 60`), and the runner waits for
  these bytes before it trusts `$6000`. `cartridge.md` is corrected.
- Singles 01, 02, 03 and 09 passed in all sixteen rows. They time themselves from
  the flag, so moving every read by the same amount changes nothing they see.
  Only the NMI tests and test 10 told the rows apart.
- `timing.md` worked example 2 said the first VBlank dot falls in cycle 82182 / 3
  = 27394. That is the count of cycles before it; the dot is the first of cycle
  27395. Corrected. The wiki's "around 27384" is still not explained, and the
  singles cannot settle it.
- The first version of the test that an NMI reaches the CPU at the right
  instruction boundary passed with the line taken at the wrong point, because
  the NMI rarely lands on an instruction's last cycle. It now runs from eleven
  start points, each two cycles apart in an 11-cycle loop, so the NMI lands on
  every cycle of the loop once. With the wrong point, 8 of its 22 rows fail.

**Decisions.**
- `Line` and `Dot` name the dot the PPU runs next. A register access between
  two ticks therefore comes before that dot, so "a read at line 241 dot 1 sees
  the flag clear and suppresses it" means what the sheet means. It also matches
  `nestest.log`, whose `PPU: 0, 21` comes after 21 dots.
- The PPU has its own I/O latch. The stub returned the CPU's bus latch. A write
  to any register fills it, a `$2002` read fills bits 7 to 5, and a read of a
  write-only register returns it. Its decay is not modelled, and a test pins
  that.
- `NesBus.Peek` is pure for the PPU's registers. It asks `Ppu.PeekRegister`,
  which clears no flag, moves no buffer and leaves the latch alone. A test
  peeks every kind of address and then checks that both latches are unchanged.
- The reset button resets the PPU: PPUCTRL, PPUMASK, `w`, `t`, `x`, the buffer
  and the odd flag go to zero, and the PPU starts again at line 0 dot 0, the top
  of the picture, as the sheet says the NES-001 does. VBlank, OAMADDR, `v` and
  the memories are kept. `PpuDots` on the bus is never reset except by power on.
- `v`, `t`, `x` and `w` are public and read-only, for the tests. This was chosen
  over `InternalsVisibleTo`, which this repository does not use anywhere.
- The nametable RAM is one 4 KB array. The first 2 KB is the console's, and the
  second is used only while the board says four-screen, so a board that changes
  its mirroring can never index past the end.
- The PPU reports `v` to the board each time it becomes a pattern-table address,
  and each `$2007` access to one. The board is given the CPU cycle through a
  function the bus sets, so the PPU keeps the constructor the plan gives it.
- Writes to `$2000`, `$2001`, `$2005` and `$2006` are not ignored before the
  first VBlank. No pinned test needs it, and every test of the PPU alone would
  have had to run a frame first. It is in `known-differences.md`.
- The Y increment's worked example has three rows. Only the first can be reached
  through the registers alone, because `$2006` clears bit 14. The other two wait
  for task 5, whose pipeline copies `t` into `v`.
