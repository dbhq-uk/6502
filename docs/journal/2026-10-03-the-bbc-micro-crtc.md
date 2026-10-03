---
title: "The BBC Micro gets its CRTC"
date: 2026-10-03
summary: "The chip that times the picture now counts characters, lines and rows the way the Hitachi part does, and its vertical sync reaches the system VIA in the cycle it happens, so the operating system takes a vertical sync interrupt every field. It runs lazily like the VIAs and works its next event out ahead, a plain model that steps every character agrees with it through millions of random characters, and the browser build runs as fast as before."
order: 17
---

# 3 October 2026: the BBC Micro gets its CRTC

Task 7 of the plan replaces the CRTC stand-in in `BbcBus.cs` with a model of
the HD6845S, `src/Dbhq.Machines.BbcMicro/Crtc6845.cs`, and wires its VSYNC to
the system VIA's CA1. It is the first new chip on the bus that task 6b made
lazy ([the bus speed entry](2026-10-03-the-bbc-micro-bus-speed.md)), so most of
the work was in how it catches up, not in what it counts. The sources are
`video.md` sections 1, 3 and 5, and the documents the sheet cites: the Amstrad
CPC CRTC Compendium (ACCC, the sheet's S2), the Hitachi datasheet (S1) and the
stardot thread on interlace (S14). No emulator's code was read; a copy of
jsbeeb's video file sits among the fact sheet's working downloads in `/tmp`
and was not opened.

## The model chosen

**The HD6845S as the ACCC describes its "CRTC 0"**, chosen over a textbook
MC6845 (count to R0, R9 and R4 and wrap), which has neither quirk the sheet
asks for, and over the whole of the ACCC's CRTC 0, which runs to dozens of
special cases (R0 = 0 freezing the counters, VSYNC on an R7 write, the odd-R9
interlace balancing) that no BBC mode uses. What it does, briefly:

- C0, C9 and C4 count characters, lines and rows. At a line end C9 = R9 moves
  C4 on; a C9 past R9 runs on to 31 and wraps.
- **The end of a frame is decided early.** While C0 is 0 or 1 the chip sets a
  "last line" state from C4 = R4 and C9 = R9 and never looks again on that
  line (ACCC 10.3.1.2), so changing R4 or R9 later on the last line does not
  stop the frame ending. That is the first quirk of `video.md` s1.6, and
  `TheEndOfTheFrameIsDecidedAtTheStartOfTheLastLine` writes R4 at C0 = 0 (the
  frame goes on) and at C0 = 2 (it ends anyway).
- **A last line arms the vertical adjust, and only C0 = 2 disarms it.** With
  R0 = 1, C0 never gets there, so every frame gets a one-line adjust even with
  R5 = 0, and lines of two characters make no HSYNC (ACCC 13.2.5). With R4 = R9
  = 0 a frame starts every four characters, not every two. That is the second
  quirk. VSYNC also needs the line before to reach C0 = 2 (ACCC 13.2.2).
- Interlace (R8 bit 0): an even field's VSYNC is half a line late and the field
  gets an extra adjust line; the field's parity flips through a state that
  turns over when C4 reaches R6 (ACCC 19.5.2, 19.6.1). In interlace sync and
  video (R8 = `&93` in mode 7) C9 counts one a line, a row ends at C9 = R9 / 2,
  and RA is C9 shifted left with the parity in bit 0.
- DISPTMG and CUDISP with R8's skews; the cursor on lines R10 to R11 at
  MA = R14/R15, blinking on the field count; nothing displayed in the first
  field after a reset, with MA from 0 (S1).

The rest of the ACCC's list is in `docs/known-differences.md`, under the CRTC.

### The interlace arithmetic, checked

Mode 0 to 2's registers (R4 = 38, R9 = 7, R5 = 0, R7 = 34 + 1, R0 = 127 at
2 MHz): 39 rows of 8 lines is 312 lines, and the even field adds one, 313.
VSYNC starts at row 35, line 280, in both. The odd field's VSYNC is at C0 = 0,
the even field's half a line, 64 characters, later. From the odd field's VSYNC
to the even one's is 312 lines and 64 cycles, 39,936 + 64 = 40,000 CPU cycles;
from the even to the odd, 313 lines less 64 cycles, 40,064 - 64 = 40,000.
Mode 7 (R4 = 30, ten lines a field row, R5 = 2, R0 = 63 at 1 MHz): 310 + 2 = 312
lines, 313 in the even field, VSYNC at row 28, line 280, and half a line is 32
characters, 64 cycles, so the same 40,000. With R8 = 0 there is no extra line
and no half line: 312 x 128 = 39,936. The tests assert all three, and the
machine-level test sees the same gaps as IFR1 rising on the system VIA.

### Decision: half a line is (R0 + 1) / 2 characters

Chosen over the ACCC's "when C0 reaches R0/2", which it spells out as C0 = 31
for R0 = 63. For the BBC's odd R0 that is a character short, and the vsync
interrupts would alternate 39,999 and 40,001 cycles (39,998 and 40,002 at
1 MHz). On a real Model B, hoglet measured the delay with a scope as "exactly
half a line (32us)", and hexwab timed the interrupts at "around 40000
(312.5*128) with interlace on" in both fields (S14). The BBC measurement wins,
but neither source resolves one character, so it is in the known differences.
Planting the ACCC's reading in the code fails eleven tests, so the choice is
pinned.

### Decision: VSYNC's width is counted in lines from its start

Chosen over counting it with HSYNC, which the sheet's first draft said and the
ACCC's "stops at the end of the HSYNC of this line" suggests. Counted with
HSYNC, the even field's late pulse would end at the same point of a line as
the odd field's, and since the OS takes the interrupt on the fall (PCR `$04`),
the interrupts would alternate 39,936 and 40,064 cycles. S14's measurement says
40,000 both times, so the fall moves with the rise. `video.md` s1.4 is
corrected with a dated note.

### The registers

The brief said R0 to R13 are write only and R14 to R17 read back. The HD6845S
datasheet says the start address "can be read" on the S (not the R), with
R12's top two bits 0, the fact sheet says the same, and the task 5 boot test
reads R12 back through `&FE01`. So R12 to R15 read back, R16 and R17 (the light
pen, never strobed) read 0, and R0 to R11 read `$00`, as the stand-in had it.
Writes are masked to each register's width.

## How it runs: lazily, and a line at a time

The brief's rule: no work on every cycle, and the browser speed must not fall
much. The chip reads the time from the machine's clock; its character clock is
every CPU cycle at 2 MHz and every even one at 1 MHz (the same even cycles as
the VIAs), chosen by the video ULA's control bit 4, which the stand-in still
holds until task 8. A write to `&FE20` that changes the bit catches the CRTC up
at the old rate first.

**Decision: step only the characters that can make an event.** Only two
characters of a line can change VSYNC or start a frame: the one that ends the
line, and the middle of a line during a half-line VSYNC. Those go through
`Step`, the rules one character at a time; every run between them is done at
once by `Advance`, which applies the rules at C0 = 1, 2 and R1 once each and
counts HSYNC in a few spans. Chosen over stopping at every point of a line
where something happens (C0 = 1, 2, R1, R2, the end of HSYNC, the cursor),
which was the first version: correct, but six stops a line. Chosen also over
working out whole rows or frames in closed form, which would be cheaper still
but would have to restate every vertical rule, and the quirks are exactly the
cases where a closed form goes wrong.

**Decision: work the next event out ahead, and keep the state it ends in.**
`NextEventCycle` runs a copy of the counters forward to the next VSYNC edge or
frame start and keeps the copy. When the bus reaches that cycle, catching up is
a copy of a struct. A write to a register that moves the timing (R0, R3 to R9)
throws the prediction away; the others (R1, R2, R10 to R15) keep its time and
drop only the copy, so the cursor moving does not make the chip work its next
event out again. Just after a timing write the chip looks only four lines ahead,
because a program rewriting R0 twice a line (the stardot demos) would otherwise
pay for a field's worth of lines on every write.

At the BASIC prompt, a scratch build with counters (not committed) showed what
the chip does in a second of machine time after the boot: 150 predictions,
150 jumps to a kept state and no ordinary catch-up at all. Its first version
made 94,325 steps in that second; the second makes 15,700, which is 314 a
field: one for each line's last character and one for the middle of each line
of a half-line VSYNC.

## The seam, and the reviewer's note

Task 6b's reviewer said step 4 of "How a new chip plugs in" needed a way for a
VIA to catch up to a past cycle, and that the order of `Service()` matters. Both
are now built, and that section of the bus speed entry is corrected:

- `Via6522.SyncTo(cycle)` catches the VIA up to a given CPU cycle, not to now,
  and throws if it has already done that cycle.
- `SystemVia.SetVsyncAt(cycle, level)` takes a VSYNC edge in its own cycle,
  after that cycle's tick, as a VIA ticked every cycle would.
- `Via6522.SyncInputs()` runs before a VIA catches up; the system VIA uses it
  to bring the CRTC up to date if one of its events may be due
  (`Crtc6845.SyncIfDue()`, one comparison when none is).
- `BbcBus.Service()` brings the CRTC up to date before it reads the system
  VIA's next event and IRQ line.

## What resets it

Power on and BREAK both reset the CRTC's counters and leave its registers, as
the datasheet's /RES does. The hardware guide says RST, which both make, goes to
all the circuitry but the system VIA (S9 s3.14); IC2's pin was not read on a
schematic, so `video.md` s6 gains it as item 8a. A reset that drops a high
VSYNC is an edge on CA1. The video ULA stand-in is still not reset: the real
chip has no reset pin, which task 8 can confirm.

## The tests, and whether they can fail

`Crtc6845Tests` asserts the sheet's section 5 numbers for each OS register set,
the two quirks, the skews, the cursor and its blink, the first field, the
registers, and, through the bus, each VSYNC fall setting IFR1 in its own cycle
40,000 or 39,936 cycles apart. `BootTests` gains one: the OS's vsync counter at
`&0240` goes down by fifty in a second of machine time.

Equivalence, as in task 6b. `Oracle/ReferenceCrtc6845.cs` is a plain model of
the same rules, every rule in every character. The oracle bus now has it in
place of the stand-in, ticked in every cycle its clock is due, with VSYNC on
CA1, so the whole-machine comparison (real OS, typed BASIC, BREAK) now runs with
real vsync interrupts, and passed on the first run. Three comparisons:

- the lazy chip against the plain model through ten million characters of
  random register programs, written at random characters, with resets: one copy
  looked at every character, one looked at from one to 300,000 characters apart;
- the bus's random test now also programs the CRTC through `&FE00` and `&FE01`
  and flips the ULA's clock bit, and compares the CRTC's outputs now and then;
- a fixed case: a write that brings the next VSYNC fall forward (R3 cut to one
  line, or the clock doubled) while the bus has already chosen its next look.

The chip was written before these tests were first run, so the tests never
failed against a missing chip the way the method asks (the brief's step 2).
The planted mistakes below are the evidence that they can fail.

**Planting mistakes.** Twenty-six deliberate one-line mistakes in the first
version, each run against the whole BBC test project (`python3 /tmp/mut/run.py`,
a scratch script, not committed). Twenty-two were caught, two of them only after
they were rewritten, because the first form did not compile with warnings as
errors. Four got through, and each is now explained:

- *Skipping the stop at the cursor's next character.* The stop was redundant: a
  run already shifts in "no cursor". It is gone from the code.
- *An edge delivered a cycle late*, and *an edge taken at "now" instead of its
  cycle.* Neither can be seen at the bus. The VIA's state can only be seen when
  something looks at it, and anything that looks brings the CRTC up to date
  first, so the edge always lands before the look. The only thing an edge's exact
  cycle reaches is the VIA's port A input latch on CA1, within two cycles of a
  port A change, and the OS never turns the latch on. Exactness is kept anyway.
- *The bus not looking again after a CRTC write.* This was real: a write that
  brings the fall forward left the bus's next look at the old one, and the IRQ
  would rise late. The random test rarely leaves a gap after a CRTC write, so
  it missed it. The fixed case above was written for it, and it also catches
  the same mistake after a ULA clock change.

The second version's run arithmetic got its own nine mistakes (the history
reaching before the run, HSYNC restarting at R2, the C0 = 1 check skipped or
repeated at 2, the middle of the line passed, the display, HSYNC count, cursor
and row start each a character out): all nine caught.

**Two mistakes of my own.** The first equivalence run failed at once, and the
cause was the test: it read the lazy chip's event counts before reading its
outputs, and reading the outputs is what makes a lazy chip catch up and raise
its events. And the first mutation run hung for fifteen minutes: two tests
waited for a VSYNC or a frame with no limit, and a mutant that stopped VSYNC
waited for ever. Both loops now give up and fail.

`dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release`: the BBC project's
tests went from 434 to 469, all passing.

## The speed

Native: `bench/bbc-micro-speed/native`, built from the commit before this task
(`85ce6c5`, the same code as task 6b's `98fe9d5`) and from this task's code, run
with `./alternate.sh native 5 <first> <second>` in both orders, fifty timed runs
of two million cycles each. Browser, compiled ahead of time: task 6b's final
build (`publish/aot`, from `98fe9d5`) and a fresh publish of this task's code,
with `./alternate.sh browser 3 <first> <second>` in both orders, fifteen runs
each. Medians in millions of CPU cycles a second, from 10:10 UTC:

| Set | Load (1 min), before to after | Before this task | With the CRTC |
| --- | --- | --- | --- |
| Native, old build first | 2.01 to 1.93 | 88.394 | 80.684 |
| Native, new build first | 1.93 to 1.78 | 87.900 | 84.626 |
| Browser AOT, old build first | 1.78 to 1.82 | 35.273 (17.64 times 2 MHz) | 35.907 (17.95 times) |
| Browser AOT, new build first | 1.82 to 1.94 | 37.106 (18.55 times) | 37.665 (18.83 times) |

Natively the machine is 4 to 9 per cent slower; in the browser the difference
is inside the noise, with the new build slightly ahead both times. Some of the
native cost is not the CRTC at all: the OS now runs its vsync interrupt fifty
times a second. The first version, with six stops a line, measured 92.357
against 78.297 natively at 10:00 UTC (load 2.25 to 2.15), about 15 per cent
slower, which is what led to the second.

## For task 8

*Corrected twice in review, the same day.* The first version of this section
said the seam for the video ULA was ready, because `Advance` walks a line in
spans and a sink there could hand the ULA a span at a time. That is wrong for
the path the machine takes. While nothing else looks at the CRTC, its progress
is a jump to the state it worked out ahead, which never calls `Advance`; and the
working-out happens early, so a sink fed then would emit spans before later
writes to screen memory or the palette. Worse, a write to R0 or R3 to R9 throws
the prediction away, so such a sink would have emitted spans that never happen.
The second version said a consumer could "bring the CRTC to each event's cycle
and read the state there". That could not work either: every getter catches the
chip up to now before it answers, so after bringing it to a past cycle the next
getter moved it on, and bringing it to a cycle already passed did nothing.

**What was added: `StateAt(cycle)`.** It returns a `CrtcState`, a value with no
allocation: the cycle, C0, MA, the line's start address, RA, whether the line is
inside the vertical display, DISPTMG and CUDISP for that character with their
skews applied, HSYNC, VSYNC, R1 in force, both skews as numbers, the cursor
address, whether the cursor shows on this line, and the CPU cycles a character
takes. `StateCycle` says the earliest cycle it can answer for; the latest is now.

**Decision: bring the chip to the cycle, rather than keep a history.** `StateAt`
does `SyncTo(cycle)`, raising any events on the way, then reads the chip's own
state. The answer is exactly right because registers can only change at a
write, and a write brings the chip up to the write's cycle first, so between
the chip's state and now the registers are the ones in force. Chosen over a
small ring of line-start snapshots, which would cost something every line
whether or not anyone reads them, and which could only answer at line starts;
and over running a copy of the state forward from where the chip stands, which
costs a whole frame's worth of lines again for each line asked about. With
`StateAt` the chip moves forward once, a line at a time, and a consumer that
asks every line makes it step through `Run` and `Advance` at one or two steps a
line, about the 15,700 steps a second measured above, instead of jumping.

**It is tested against the plain model at past cycles.** In
`CrtcEquivalenceTests` and, through the bus at 1 and 2 MHz with CA1's interrupt
on, in `BusEquivalenceTests`. The plain model's state is recorded every
character, the lazy chip is asked about random cycles in order, from a few
characters to thousands apart, and register writes come at random characters,
each after a look up to its cycle. Three planted mistakes in `StateAt`
(bringing the chip to now, a row's start for the line's, the cursor line
without the blink) each fail both tests.

**The contract for a consumer**, which step 6 of "How a new chip plugs in" in
[the bus speed entry](2026-10-03-the-bbc-micro-bus-speed.md) states for any
consumer:

- It is driven by its own event in the bus's minimum (a line start, or a frame),
  and at each event it asks `StateAt(cycle)`, not the point getters.
- Before any write that changes what it reads (a CRTC register, the ULA's
  registers and palette), it must be brought up to that write's cycle first; the
  write then moves the CRTC on, and no earlier cycle can be asked about.
- Its events must be handled before anything else brings the CRTC up to now,
  such as the bus's `SyncIfDue` in `Service()`, or a CRTC write.
- Asking about a cycle the chip has passed throws. `SyncTo` to a cycle already
  passed does nothing.

Still not provided, for task 8 to add as it needs them: an event at each line
start (the bus looks only at VSYNC edges and frame starts today), and anything
about screen memory the CPU writes in the middle of a line.
