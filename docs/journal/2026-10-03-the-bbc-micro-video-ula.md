---
title: "The BBC Micro draws its first pixels"
date: 2026-10-03
summary: "The video ULA turns screen memory into pixels in every mode but the teletext one, a line at a time from the CRTC's state, and a palette change in the middle of a line now changes the colour from the next character. A plain model that draws every character in its own cycle agrees with it frame for frame, and the entry says exactly what is and is not cycle-exact."
order: 18
---

# 3 October 2026: the BBC Micro draws its first pixels

Task 8 of the plan replaces the video ULA's stand-in in `BbcBus.cs`, which kept
its two registers and drew nothing, with `src/Dbhq.Machines.BbcMicro/VideoUla.cs`
and a picture to draw into, `Framebuffer.cs`. It covers modes 0 to 6; mode 7's
picture comes from the teletext chip, which is task 9. The sources are
`video.md` sections 2, 3 and 5 and the documents they cite (BeebWiki's Video ULA
page, the sheet's S4, and a clone author's notes on the chip, S8). No
emulator's code was read.

## What the chip does

Two write-only registers. The control register at `&FE20` says how fast the
pixels go (2, 4, 8 or 16 MHz, which is 10, 20, 40 or 80 characters a line),
whether the CRTC runs at 1 or 2 MHz, which of the cursor's three segments to
draw, whether the picture is the teletext chip's, and the flash select. The
palette at `&FE21` is sixteen entries of four bits, each a colour stored
inverted (so the OS writes the colour XOR 7) and a flash bit. Each CRTC
character the ULA takes the byte the CRTC addresses into a shift register, and
each pixel clock looks the palette up with the register's bits 7, 5, 3 and 1,
then shifts it left, filling with 1s. That one rule makes every mode: eight
1-bit pixels a byte at 16 MHz, four 2-bit pixels with the bits interleaved at 8
MHz, two 4-bit pixels at 4 MHz. DISEN, which is the CRTC's display enable and
not RA3, blanks to black, and the cursor inverts.

The byte's address comes from the CRTC's MA and RA: `(MA << 3) | RA` on MA0 to
MA11, and when MA12 is high, past `&7FFF`, the board adds a fixed amount chosen
by two bits of the system VIA's latch, which wraps the address back to the
mode's screen base. The sheet found that the ROM writes those two bits the
other way round from the Advanced User Guide's table for modes 0 to 2 and 4 to
5; the ROM's pairs are the ones that land each mode's wrap on its own base, and
a test boots each mode and checks it.

## The design: a line at a time, exact at every register write

The controller set the design after task 7, from the rule for a chip that reads
another chip's outputs ([the bus speed entry](2026-10-03-the-bbc-micro-bus-speed.md),
"How a new chip plugs in", step 6) and the CRTC's `StateAt(cycle)`
([the CRTC entry](2026-10-03-the-bbc-micro-crtc.md)):

- **The ULA does not run in every cycle, and is not fed every character.** It
  draws one line per event. The event is the cycle of the line's last
  character, worked out from where the drawing stands, R0 and the clock. At
  that event it asks the CRTC for its state there (the line's start address,
  RA, the vertical display, R1, the skews, the cursor), works out each
  character's display enable, cursor and address the way the CRTC does, reads
  the bytes and draws.
- **Before any write that changes what it draws, it draws up to that write's
  cycle with what it had.** Its own two registers, any CRTC register, and the
  system VIA's ORB and DDRB, which strobe the latch. So a palette change in the
  middle of a line changes the line from the next character, which is what a
  game's raster bars need. A test writes the palette at character 30 of line
  100 and checks the line is white to the left of the next character and cyan
  from it, in a 2 MHz mode and, in a 1 MHz mode, with the write in each half of
  its character.

### What is cycle-exact, and what is not

Dan's choice in the design was a cycle-accurate video path rather than a
scanline renderer. This narrows it, and the narrowing is the controller's
decision for this task, made for speed:

- **Exact to the character:** every register that changes the picture: the
  palette, the control register (the flash, the pixel rate, the CRTC's clock,
  the cursor's segments), every CRTC register, and the latch's screen start
  bits. A write is seen from the first character clocked after its cycle.
- **Exact to the line, not the cycle: screen memory.** A line's bytes are read
  when the line is drawn, at its last character or at a register write that
  comes first. A store to screen memory while the beam is part way along a line
  is seen by the whole of the stretch drawn after it, where the real ULA,
  fetching byte by byte, would show the old bytes to the left of the store and
  the new ones to the right. Doing it exactly would mean logging every store
  the CPU makes to RAM, which is most of its writes, so every program would pay
  on its busiest path for the few that race the beam.
- **Not known at all: the ULA's pipeline.** How many characters lie between the
  CRTC's fetch and the pixel is not documented (`video.md` s6 item 3). The model
  has none, named `VideoUla.PipelineDelayCharacters`. Both this and the screen
  memory reading are in `docs/known-differences.md`.

### Decision: the CRTC brings the ULA up first, whoever moves it

The rule for a consumer says its events must be handled before anything else
brings the CRTC up to now. The bus's `Service()` can keep that order, but it is
not the only thing that moves the CRTC: the system VIA brings it up to date
before catching up itself (task 7's `SyncInputs`), on any read of a system VIA
register, and a key press does the same. Any of those could take the CRTC past
a line end the ULA had not drawn yet, and the next `StateAt` would throw. So
the CRTC now has a `Consumer`, and before it moves on to any cycle, for any
reason, it has the consumer draw up to that cycle first; it also tells the
consumer of a reset. Chosen over making every path that reaches the CRTC call
the ULA first, which would have to be remembered at each new path, and over
giving the CRTC a history of past lines, which would cost every line whether or
not anyone drew it. The ULA asks with `StateForConsumer`, which does not call
it back.

### Decision: line ends are kept apart from the chips' events

The first version put the ULA's line end into the bus's one event horizon. A
line end then cost a full look at every chip: both VIAs brought up to date and
asked for their next event, and the IRQ line set, sixteen thousand times a
second of machine time. A line end can never move the IRQ line, so the clock
now has a second horizon, `ChipEvent`, for the chips that can, and a line end
costs only the ULA's drawing. The first try at this had a mistake the bus's
oracle test caught at once: `PowerOnReset` and `BreakReset` call `Service()`
directly, and with the split it no longer looked at the chips unless their
event was due, so after a reset the IRQ line kept its old level until the
chips' next event, and the test saw it high in a cycle where the oracle's was
low. They now mark the chips as accessed first.

### Decision: the framebuffer

640 by 512, as the brief proposed. Across, one pixel is the ULA's fastest pixel
clock, 16 MHz, so every mode fills the width: a mode 0 pixel is one wide, a mode
4 pixel two, a mode 5 pixel four. Down, each CRTC frame is one field of the
television picture, and with interlace on (R8 bit 0, which the OS sets in every
mode) the two fields are woven, the even field's lines in the even rows and the
odd field's in the odd rows; with interlace off both rows get the line. Chosen
over doubling every line, which would have hidden which field a change landed
in, and over weaving always, which with interlace off would show two moments of
a moving picture combed together where a television shows each in the same
place. Which field goes on top follows the timing: after an even field's
half-line VSYNC, the next field's first line comes half a line later after the
sync than the other field's does, so it is drawn half a line lower, and that
next field is odd. Mode 7's rows of odd and even rasters agree with
that, which task 9 will see.

Positions are the model's own, not a television's: across from the CRTC's
character 0, down from its frame start, lines 0 to 255 kept. Chosen over
measuring from HSYNC and VSYNC, as a television does, because the sources do
not settle where a real picture sits (mode 7's HSYNC is two characters later
than mode 6's and its picture is said to sit one character right; by my
arithmetic the television's rule gives two characters left for the later sync
and one right for the display skew, one left in all), and because the screen-reading helper of
task 10 is simpler against a fixed grid. With the OS's registers every mode
lands in the same place either way; a program that moves R2 or R7 to shift the
picture will not see it move. `Frames` counts CRTC frame starts, one a field,
fifty a second, so the page can draw on each.

### Decisions the brief left open, or that differ from it

- **No `Mode` property.** The brief asked for the mode decoded from the control
  byte, but the byte cannot tell mode 0 from mode 3, or 4 from 6: they differ in
  the CRTC and the latch, not the ULA. The ULA gives what the byte does say:
  `CharactersPerLine`, `TwoMhz`, `Teletext` and `FlashSelect`.
- **No `Clock(byte, ...)` method.** The brief's interface fed the ULA one
  character at a time; the controller's design replaced that. The per-character
  form lives on in the test project's reference model. In its place is
  `Draw(byte, pixels)`, which gives one character's pixels for a byte with the
  registers as they are, and is what the hand-worked pixel tests use.
- **What resets the ULA.** Nothing. S8's pin list has no reset input, so BREAK
  leaves both registers, and the OS writes both again in the mode change every
  BREAK makes. At power on what they hold is not known, and the model takes
  zero, which also starts the CRTC on its 1 MHz clock. A test pins both.
- **The RA3 gate in mode 7** is taken to be off while the teletext select is on.
- **Mode 7 is black** until task 9, with the ULA's cursor drawn over it.

## The tests, and whether they can fail

`VideoUlaTests` has the brief's list: the eight control bytes from the ROM; the
palette protocol and the eight flashing pairs; one byte per mode turned into
pixels, each expectation worked by hand from the sheet and written in the test
as a string of colour letters; the two odd cases the sheet notes (80 columns on
the 1 MHz clock, whose second half is logical colour 15, and 10 columns on 2
MHz, where only the odd bits show); the logical colour as eight, four or one
palette entries; the address for MA and RA with the wrap per mode; the ROM's
latch bits, by booting each mode; RA3 blanking lines 8 and 9 of each row in
modes 3 and 6; a scroll by R12 and R13 that brings the first character back at
the last cell through the wrap; the cursor one, two or four characters wide as
the control byte says; the flash select changing the screen; the palette change
in the middle of a line; and mode 1 booting with its banner on the screen.

Two of my own hand-worked expectations were wrong when first written: mode 5's
`&96` gives yellow, red, red, yellow, not four colours, and `&5B` does not have
the same odd bits as `&12`. Both were caught by working them again before the
first run, and the tests now use `&CA` and `&47`.

**The equivalence test.** `Oracle/ReferenceVideoUla.cs` is a plain model fed in
every character clock by the oracle bus, with the byte fetched from RAM in that
cycle, the CRTC's outputs as they are then, and the shift register stepped one
pixel clock at a time. `VideoUlaEquivalenceTests` drives the machine's bus and
the oracle bus with the same random programs (the OS's modes with values
changed, and small random ones whose frames are a few lines long), random
palette, control, CRTC and latch writes at random cycles, BREAKs, and random
screen memory, and compares the whole framebuffer at the end of each frame,
or of the first frame to end 4,000 cycles after the last comparison, so the
small programs' frames of a few lines are not all compared.
Screen memory changes only straight after one of the writes that make the ULA
draw up to its cycle, the one place the two readings of RAM agree.

The task 7 test that asks the CRTC about past cycles in the machine had to
change: the ULA now takes the CRTC to every line end, so only a line's worth of
past cycles is left to ask about, and the test fell short of its count of
checks. It now looks four times as often; no assertion changed.

**Planting mistakes.** Thirty deliberate one-line mistakes, each run against the
ULA, oracle, CRTC and boot tests (`python3 /tmp/t8mut/run.py`, a scratch script,
not committed). Twenty-eight were caught the first time: the wrap adder with the
Advanced User Guide's pairs, the latch read as one bit, RA3 not blanking,
teletext drawing bytes, the palette without its XOR 7, the flash select the
wrong way round, the shift register filling with 0s, index bits 7 and 5
swapped, cursor segments 0 and 1 swapped, segment 2 one character wide, a
palette or latch write not drawn up to first, the fields woven the other way,
interlace off not doubled, the characters a pixel pair late, the rows a short
field does not reach left as they were, the rest of a line left as it was, the
straight path running past the cursor, a reset counted as a frame, the cached
pixels kept after a palette or control write, the display ending a character
late, the last line of a field not counted, `Draw` ignoring teletext, BREAK not
looking at the chips, and in the CRTC the line count not reset at a frame start,
the other field reported, and the consumer not drawn up first. Two got through,
and each is explained:

- *An early stop that ignored a cursor still being drawn.* It shows only when
  CUDISP is delayed longer than the display and the cursor sits at the display's
  last character, so the cursor runs on into the border, which BeebWiki says
  can happen. Neither test had that case. A fixed test now does, and the random
  programs now keep a steady cursor on the screen, often at a row's end.
- *The straight path leaving the display history as it was.* It cannot be seen:
  the straight path runs only with no skew, where only the newest bit of the
  history is used, and that is worked out again for the next character. The
  history is still kept right, so that a later change to the straight path does
  not inherit a wrong one.

The oracle comparison also missed "the straight path running past the cursor"
at first, caught only by the fixed cursor test, for the same reason as the
first survivor: the random programs rarely kept a cursor on the screen long
enough to be compared. With the cursor kept there, it catches it too.

`dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release`: the BBC project's tests went from 469 to 561, all passing, and the whole solution (`dotnet test -c Release`) passes.

## The speed

The brief's rule: measure before and after, natively and in the browser
compiled ahead of time, and stop and say so if the browser median falls under
ten times a 2 MHz machine. The bench's workload sits at the BASIC prompt in
mode 7, which until task 9 draws nothing but black, so the cost of drawing
would never show in it. The bench now takes a screen mode (`?mode=N` on the
page, `MODE=N` for `alternate.sh`; the bench's README says how), and the old
code was published again from an exported copy of `fb7a0fb` with only the
bench's two `Program.cs` files changed, so it could be run in the same modes.

**The machine was busy all afternoon.** Other sessions were compiling and
running test suites on it, and the one-minute load average swung between 1 and
14 within minutes. Every set below alternates single launches of the old and
new builds, so a burst falls on both, and gives the load before and after; one
set was hit hard enough to halve both builds, and it is shown anyway.

### What it cost, and what was done about it

The first version (`publish/native-task8-v1`, not committed) was 30 per cent
slower natively in mode 7 and 62 per cent slower in mode 1
(`MODE=7` and `MODE=1 ./alternate.sh native 2`, 11:21 UTC, load 2.33 to 1.93:
82.386 to 56.694 MHz in mode 7, 91.160 to 34.520 in mode 1). Each change after
it was measured against the old code in the same set:

- **Line ends kept apart from the chips' events** (above). Natively mode 1 went
  to 40.367 MHz (11:25 UTC, load 3.10 to 2.93).
- **Two ways to draw nothing cheaply.** A run of characters where nothing can
  show (mode 7, the lines RA3 blanks, the lines outside the display) is one fill
  of black, and a whole line of black over a row already black writes nothing.
- **A straight path** for every displayed character of modes 0 to 6 but the
  cursor's: no skew, nothing blanked, no cursor, so each character is only its
  byte through the palette. Natively mode 1 went to 49.713 MHz (11:33 UTC, load
  3.16 to 2.90).
- **Each byte's finished pixels cached,** made again only when the byte is next
  drawn after a palette or control write, so a raster bar's palette writes cost
  nothing until the bytes they change are drawn; **stored two pixels to a
  `ulong`**, so a character is four or eight stores; and **the copy written out
  for four and eight pairs** through slices of a fixed length. On the browser
  build, mode 1 went from 8.32 times a 2 MHz machine (the old code 13.76 in the
  same set, 11:45 UTC, load 6.35 to 4.69) to 12.93 (the old code 14.49, 11:53
  UTC, load 5.07 to 4.11), then 11.43 (the old code 14.04, 12:01 UTC, load 4.75
  to 3.08).

**What did not help, and was not kept:** taking out the three `try`/`finally`
blocks on a line's path (13:32 UTC: the same best run in mode 7, 32.468 against
32.415 MHz), and drawing through plain arrays instead of slices (13:43 UTC: a
worse best run in mode 1, 23.229 against 28.050). **A sampling profile was no
use again,** as in task 6b: it put two thirds of the time in `EndField`, which
a counter showed was doing no work at all; the figures above come from builds
with parts switched off, which is slower but cannot be misread. Natively, on
the bus alone with no CPU (a scratch program, not committed), the old code took
about 3.2 ns a cycle and the final code about 4.5 in mode 7 and 6.4 to 6.9 in
mode 1: about 170 ns a line for asking the CRTC and keeping count, and about 3
ns a character for drawing. In the browser the same split is about a third of
the cost for the lines and two thirds for the drawing in mode 1: a build that
asked the CRTC about every line but drew nothing ran at 14.48 times in mode 1
where the final code ran at 12.32 and the old code at 15.74 (13:38 UTC, load
2.94 to 3.06).

### The final figures

The final code against the old (`fb7a0fb`, published from an exported copy
with the mode-aware bench), all in one afternoon on the same shared virtual
machine, the same headless Chrome as task 7, .NET SDK 10.0.400. Medians in millions of
CPU cycles a second, and as multiples of a 2 MHz machine.

Browser, compiled ahead of time, five launches of five timed runs a build:

```
MODE=1 ./alternate.sh browser 5 publish/aot-task8-before-modes publish/aot-task8-final   # and the same with the builds the other way round
MODE=7 ./alternate.sh browser 5 ...
MODE=4 ./alternate.sh browser 5 ...
```

| Mode | Order | UTC | Load (1 min), before to after | Old code | Final code |
| --- | --- | --- | --- | --- | --- |
| 1 | old first | 13:22 | 10.71 to 9.30 | 31.056 (15.53 times) | 25.157 (12.58 times) |
| 1 | new first | 13:23 | 9.30 to 4.68 | 33.784 (16.89 times) | 26.738 (13.37 times) |
| 7 | old first | 13:24 | 4.68 to 2.90 | 37.037 (18.52 times) | 29.112 (14.56 times) |
| 7 | new first | 13:25 | 2.90 to 3.35 | 35.026 (17.51 times) | 29.028 (14.51 times) |
| 4 | old first | 13:26 | 3.35 to 13.50 | 18.002 (9.00 times) | 15.314 (7.66 times) |
| 4 | new first | 13:28 | 13.50 to 9.33 | 33.727 (16.86 times) | 25.063 (12.53 times) |

The mode 4 set that starts at 13:26 was run while the load went from 3.35 to
13.50; both builds fell to half their usual speed, the old code's slowest run to
10.953, so its medians say nothing about either. Three more sets of the final
code were taken beside the experiments above, with the old code alongside each
time: mode 1 at 13.10 times (the old code 14.61, 13:34 UTC, load 3.55 to 4.63),
12.32 times (15.74, 13:39 UTC, load 2.94 to 3.06) and 9.58 times (15.59, 13:43
UTC, load 1.78 to 6.31); mode 7 at 11.68 times (16.85, 13:32 UTC, load 6.21 to
3.55) and 14.81 (15.22, 13:38 UTC, load 4.55 to 2.94); mode 4 at 11.82 times
(16.24, 13:45 UTC, load 6.31 to 4.49). **The one set of the final code under
ten times** is the 13:43 one in mode 1, during a jump in the load from 1.78 to
6.31, whose slowest run of the old code was 7.651 MHz; across the eight sets of
the final code in modes 1 and 4 the median is about 12.5 times. The final code
runs at about four fifths of the old code's speed in modes 1 and 7 and about
three quarters in mode 4, worked from the paired medians above.

The mode 1 set with the final code first, as printed (13:23:53 to 13:24:47 UTC):

```
publish/aot-task8-final launch 1 timed 1 cycles=2000003 ms=89.600 cycles_per_second=22321462 mhz=22.321
publish/aot-task8-final launch 1 timed 2 cycles=2000000 ms=89.500 cycles_per_second=22346369 mhz=22.346
publish/aot-task8-final launch 1 timed 3 cycles=2000000 ms=83.400 cycles_per_second=23980815 mhz=23.981
publish/aot-task8-final launch 1 timed 4 cycles=2000000 ms=80.900 cycles_per_second=24721879 mhz=24.722
publish/aot-task8-final launch 1 timed 5 cycles=2000000 ms=84.900 cycles_per_second=23557126 mhz=23.557
publish/aot-task8-before-modes launch 1 timed 1 cycles=2000003 ms=68.700 cycles_per_second=29112125 mhz=29.112
publish/aot-task8-before-modes launch 1 timed 2 cycles=2000000 ms=64.600 cycles_per_second=30959752 mhz=30.960
publish/aot-task8-before-modes launch 1 timed 3 cycles=2000000 ms=65.100 cycles_per_second=30721966 mhz=30.722
publish/aot-task8-before-modes launch 1 timed 4 cycles=2000000 ms=70.700 cycles_per_second=28288543 mhz=28.289
publish/aot-task8-before-modes launch 1 timed 5 cycles=2000000 ms=65.600 cycles_per_second=30487805 mhz=30.488
publish/aot-task8-final launch 1 timed 1 cycles=2000003 ms=91.300 cycles_per_second=21905838 mhz=21.906
publish/aot-task8-final launch 1 timed 2 cycles=2000000 ms=77.200 cycles_per_second=25906736 mhz=25.907
publish/aot-task8-final launch 1 timed 3 cycles=2000000 ms=75.100 cycles_per_second=26631158 mhz=26.631
publish/aot-task8-final launch 1 timed 4 cycles=2000000 ms=74.800 cycles_per_second=26737968 mhz=26.738
publish/aot-task8-final launch 1 timed 5 cycles=2000000 ms=74.400 cycles_per_second=26881720 mhz=26.882
publish/aot-task8-before-modes launch 1 timed 1 cycles=2000003 ms=65.700 cycles_per_second=30441446 mhz=30.441
publish/aot-task8-before-modes launch 1 timed 2 cycles=2000000 ms=55.600 cycles_per_second=35971223 mhz=35.971
publish/aot-task8-before-modes launch 1 timed 3 cycles=2000000 ms=57.300 cycles_per_second=34904014 mhz=34.904
publish/aot-task8-before-modes launch 1 timed 4 cycles=2000000 ms=59.200 cycles_per_second=33783784 mhz=33.784
publish/aot-task8-before-modes launch 1 timed 5 cycles=2000000 ms=59.500 cycles_per_second=33613445 mhz=33.613
publish/aot-task8-final launch 1 timed 1 cycles=2000003 ms=68.400 cycles_per_second=29239810 mhz=29.240
publish/aot-task8-final launch 1 timed 2 cycles=2000000 ms=75.400 cycles_per_second=26525199 mhz=26.525
publish/aot-task8-final launch 1 timed 3 cycles=2000000 ms=69.800 cycles_per_second=28653295 mhz=28.653
publish/aot-task8-final launch 1 timed 4 cycles=2000000 ms=66.400 cycles_per_second=30120482 mhz=30.120
publish/aot-task8-final launch 1 timed 5 cycles=2000000 ms=67.800 cycles_per_second=29498525 mhz=29.499
publish/aot-task8-before-modes launch 1 timed 1 cycles=2000003 ms=60.000 cycles_per_second=33333383 mhz=33.333
publish/aot-task8-before-modes launch 1 timed 2 cycles=2000000 ms=60.800 cycles_per_second=32894737 mhz=32.895
publish/aot-task8-before-modes launch 1 timed 3 cycles=2000000 ms=51.200 cycles_per_second=39062500 mhz=39.062
publish/aot-task8-before-modes launch 1 timed 4 cycles=2000000 ms=52.700 cycles_per_second=37950664 mhz=37.951
publish/aot-task8-before-modes launch 1 timed 5 cycles=2000000 ms=54.600 cycles_per_second=36630037 mhz=36.630
publish/aot-task8-final launch 1 timed 1 cycles=2000003 ms=70.200 cycles_per_second=28490071 mhz=28.490
publish/aot-task8-final launch 1 timed 2 cycles=2000000 ms=68.900 cycles_per_second=29027576 mhz=29.028
publish/aot-task8-final launch 1 timed 3 cycles=2000000 ms=69.800 cycles_per_second=28653295 mhz=28.653
publish/aot-task8-final launch 1 timed 4 cycles=2000000 ms=92.600 cycles_per_second=21598272 mhz=21.598
publish/aot-task8-final launch 1 timed 5 cycles=2000000 ms=105.500 cycles_per_second=18957346 mhz=18.957
publish/aot-task8-before-modes launch 1 timed 1 cycles=2000003 ms=63.800 cycles_per_second=31348009 mhz=31.348
publish/aot-task8-before-modes launch 1 timed 2 cycles=2000000 ms=80.100 cycles_per_second=24968789 mhz=24.969
publish/aot-task8-before-modes launch 1 timed 3 cycles=2000000 ms=57.900 cycles_per_second=34542314 mhz=34.542
publish/aot-task8-before-modes launch 1 timed 4 cycles=2000000 ms=55.800 cycles_per_second=35842294 mhz=35.842
publish/aot-task8-before-modes launch 1 timed 5 cycles=2000000 ms=55.600 cycles_per_second=35971223 mhz=35.971
publish/aot-task8-final launch 1 timed 1 cycles=2000003 ms=73.700 cycles_per_second=27137083 mhz=27.137
publish/aot-task8-final launch 1 timed 2 cycles=2000000 ms=71.500 cycles_per_second=27972028 mhz=27.972
publish/aot-task8-final launch 1 timed 3 cycles=2000000 ms=72.800 cycles_per_second=27472527 mhz=27.473
publish/aot-task8-final launch 1 timed 4 cycles=2000000 ms=73.600 cycles_per_second=27173913 mhz=27.174
publish/aot-task8-final launch 1 timed 5 cycles=2000000 ms=77.500 cycles_per_second=25806452 mhz=25.806
publish/aot-task8-before-modes launch 1 timed 1 cycles=2000003 ms=72.900 cycles_per_second=27434883 mhz=27.435
publish/aot-task8-before-modes launch 1 timed 2 cycles=2000000 ms=55.200 cycles_per_second=36231884 mhz=36.232
publish/aot-task8-before-modes launch 1 timed 3 cycles=2000000 ms=58.700 cycles_per_second=34071550 mhz=34.072
publish/aot-task8-before-modes launch 1 timed 4 cycles=2000000 ms=54.800 cycles_per_second=36496350 mhz=36.496
publish/aot-task8-before-modes launch 1 timed 5 cycles=2000000 ms=54.300 cycles_per_second=36832412 mhz=36.832
```

Natively, five launches of twelve timed runs a build, the last ten kept
(`MODE=7`, `1` and `4 ./alternate.sh native 5 publish/native-task8-before-modes
publish/native-task8-final`, 13:48 to 13:50 UTC, load 3.00 to 1.75): mode 7
83.179 to 65.954 MHz, mode 1 81.866 to 47.478, mode 4 68.029 to 54.023. An
earlier native set at 13:07 UTC ran while another session's compiler was using
more than two cores, and its runs fell into two groups, near 48 and near 21 MHz
for the same build, so it is not used.

**The machine still does the same thing.** `--fingerprint` (the bench's hash of
every instruction, and every 100,000 cycles all of RAM and every VIA register,
through boot, a typed BASIC program and BREAK) prints the same lines from the
old build and the final one: the ULA changes nothing the CPU can see.

## What this does not say

One shared virtual machine, one browser, one afternoon of other people's load.
The workload is the OS idling at the prompt, which draws its screen and moves
nothing on it, so a game that scrolls or writes the palette every line will cost
more than these figures. Mode 7 is black until the teletext chip, which will add
its own cost to the default mode. The figures describe this commit and nothing
later.
