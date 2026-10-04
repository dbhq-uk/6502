---
title: "The BBC Micro reads and writes a disc"
date: 2026-10-04
summary: "The floppy disc controller is in, with disc images, and the real DFS lists, saves, loads and runs files through it. It is never ticked: it works out each byte when it is looked at, and a copy ticked every cycle agrees with it on every access. The disc found a bug in the processor core: an interrupt held through the moment another one fetches its address was lost, where the real chip takes it. And DFS throws its catalogue away after every listing."
order: 22
---

# 4 October 2026: the BBC Micro reads and writes a disc

Task 12 of the plan: the Intel 8271 floppy disc controller, the disc images it
reads, and Acorn DFS 1.20 running on them. Started late on 3 October and
finished in the small hours of the 4th, UTC. It was built from the fact sheet,
`docs/bbc-micro/facts/disc.md`, whose sections 1 to 3 had been worked out with a
throwaway Python machine running the real DFS ROM (`tools/probes/bbc-dfs-trace/`),
and joins the bus as the bus speed entry's "How a new chip plugs in" says
([the bus speed entry](2026-10-03-the-bbc-micro-bus-speed.md)), on the NMI line
where the VIAs are on the IRQ line.

## The model

- **Registers (s1a, s1b).** Status at `&FE80` (busy, command full, parameter
  full, result full, INT, data request), command at the same address, result and
  parameter at `&FE81`, reset at `&FE82`, data at `&FE84`. A parameter is taken
  as it is written, so parameter full is never seen set; the sheet's probe
  passed with 0 cycles. Reading the result clears result full and the result's
  interrupt; touching the data register while a byte is requested takes it and
  clears the request and its interrupt.
- **Commands (s1c).** Read Drive Status, Read and Write Special Register and
  Specify end at once, with no interrupt. Every other command uses a drive and
  ends with an interrupt and a result. Read Data, Write Data and Verify move
  sectors of 256 bytes; Seek moves the head; Scan, Read ID and Format are not
  modelled (they end with `$00` and move nothing; no DFS 1.20 command uses them).
- **Drives (s1e, s1f).** Bits 7 and 6 of a command select drive 1 or 0. Side
  select is bit 5 of special register `&23`, the drive control port, not a
  parameter. Bit 3 of that port is LOAD HEAD, which on the BBC is the motor. A
  drive is ready when it has a disc, its select bit and the motor bit. A drive
  command sets the motor itself when there is a disc; with none it ends with not
  ready, `$10`, and latches it until a Read Drive Status.
- **The head unloads** (s3) the index count times 400,000 cycles after a drive
  command ends, if no other has started: the port loses its motor and select
  bits, and the drive is no longer ready. DFS's specify gives an index count of
  12, so 2.4 seconds.
- **Time.** A drive command reaches its first byte, or its result if it moves
  none, 2,000 cycles after it starts; then a byte every 128 cycles, no gap
  between sectors, and the result one byte time after the last byte.
- **The images (s4, s5).** `DiscImage` holds a `.ssd` or `.dsd` as its bytes, 40
  or 80 tracks of ten 256-byte sectors a side. `Blank` makes an empty DFS
  catalogue: everything zero but the sector count in bytes 6 and 7 of sector 1.

### Decision: the chip is never ticked, and names its next step

As the brief set out. Between two looks nothing reaches the chip from outside,
so everything it does is a step at a known cycle: a byte offered, a late byte
withdrawn, a result, the head unloading. Catching up is a loop over those steps,
at most one every 128 cycles, so an idle controller costs nothing, and a busy one
a few comparisons a byte. It tells the bus the cycle of its next step while a
command runs, because that is when INT may rise, and the bus sets the CPU's NMI
line from INT in `Service()`, with the IRQ line, after the whole access. Chosen
over ticking it from the bus (a cost on every cycle, which task 6b took out) and
over working a whole transfer out at once (not possible: the CPU has to take
each byte, and the chip's next step depends on whether it did).

### Decision: one fixed start delay of 2,000 cycles

The sheet's suggestion for a plausible figure, marked a guess, standing for the
seek, the head settling and the sector coming round. Chosen over 0, which DFS
also passes with but which makes every command finish impossibly fast, and over
modelling the steps, the settle time from the specify and the disc's rotation,
which DFS does not depend on (s1h: step rate, settle and spin-up made no
difference to any run) and which would put numbers in the model that no test
could check.

### Decision: a `.dsd` is track interleaved; a short image is extended with zeros

The sources disagree on the `.dsd` layout (s4): track 0 of side 0 then track 0
of side 1 and so on, or all of side 0 then all of side 1. The sheet calls the
interleaved form the most common and recommends it, and DFS's own view (drive 2
is side 1 of drive 0) does not decide it. A sequential image will read wrongly.
For a short image, the sheet's recommendation is to treat what is missing as
zeros, rather than as sectors that cannot be found (what the probe did, giving
`Disk fault 18`); it has never been tried on hardware, because no real disc is
short. Both are in `docs/known-differences.md`.

*Added in task 12b, from the review:* 40 or 80 tracks by length alone turned a
trimmed 80-track image, which archives often keep only as long as the part in
use, into a 40-track disc if it was 102,400 bytes or less, and a write past
track 39 then failed with `Disk fault 18`. An image short enough to be 40 tracks
whose catalogue says 800 sectors is now taken as 80 tracks, the sheet's second
test (s4); a test trims a blank 80-track disc, one and two sided, to three
tracks.

### Decision: the registers repeat every eight bytes

`$FE80-$FE9F` is the 8271's. Within each eight bytes, A2 set is the data
register (`$FE84-$FE87`, the DMA acknowledge block, s1a) and A2 clear the chip's
own registers by A1 and A0, and the eight repeat four times. `disc.md` (IC28
splits the block on A2 only) and `bus.md` s1c (S2's B+ section) both infer it;
nothing uses the mirrors. `&FE82` and `&FE83` read `$FE`, as the bus floats for
a fast device that does not drive it, a guess. Chosen over a block of four
repeating eight times (no source) and over no mirror (contradicts both sheets).

### Decision: Read Drive Status reports a latched not-ready once

The datasheet's footnote (D1 p8-129) says the ready bits are zero latching, and
that clearing them on a ready drive takes two Read Drive Status commands. So the
first one after a not-ready shows not ready and clears the latch, and the next
shows the drive as it is. The probe's model cleared the latch without reporting
it. DFS polls Read Drive Status until it says ready, so it cannot tell.

### Smaller choices

- BREAK leaves the 8271 alone: no source read says its reset pin is on the reset
  line, and the DFS resets it through `&FE82` before it uses it.
- A command written while another runs replaces it. The datasheet forbids it and
  says nothing of what happens.
- A sector being read or written keeps coming from the disc it started on, if the
  disc is changed in the middle of it.

## What the tests found

### A bug in the core, found by the disc

**How it showed.** The disc tests showed `*TITLE` leaving the catalogue's cycle
number one short: its catalogue write never finished. A scratch test logging
every 8271 access and every NMI found the moment. A byte's INT rose in the very
cycle the CPU was reading the low byte of the IRQ vector, `&FFFE`, at the start
of an IRQ, and the core lost the NMI: nothing took the byte, the 8271 ended the
command with late data, INT stayed high from the byte to the result so there was
no new edge, and DFS's NMI handler never ran again. DFS waited, and the next
command typed went on as if the write had happened.

At the prompt the CPU read the IRQ vector 1,500 times in 20 million cycles, ten
seconds, so 150 IRQ entries a second (measured at 00:29 UTC on 4 October with a
scratch test that counted reads of `&FFFE`, not committed; the OS's 100 Hz timer
and the 50 Hz vertical sync). A transfer of two sectors, 65 milliseconds, meets
about ten of them, each with a one in 128 chance of landing on a byte.

**The first diagnosis was wrong.** The core's rule, written in stage 1 from the
transistor-level model, was that an NMI arriving while the vector's low byte is
read is lost. I took that as the chip's behaviour and worked round it in the
8271 (`3008e63`): a late byte's request was withdrawn, so INT fell, and the
result came a cycle later, so INT rose again and DFS could retry. That left a
residual hang, the same loss on the INT that announces a result, about one
result in 13,000, which the first version of this entry put down to the CPU.

**The review found the real cause.** Every stage 1 run that showed the loss
released the NMI line two cycles after it went active: the `nmi`, `brk` and
`irq-nmi` families in `tools/perfect6502/harness.c` all set it inactive at k + 2.
The reviewer rebuilt the harness against the pinned perfect6502 with the line
held, and with other pulse lengths: an NMI from the IRQ's vector-low cycle and
still active two cycles later is not lost but taken after the handler's first
instruction, like one that arrives a cycle later. The 8271's INT is a held level,
active until the data or result register is touched, at least 18 cycles later in
DFS's handler, so a real 6502 takes it: a real BBC neither goes late there nor
hangs. The core's `EnterHandler` cleared the latched edge after the low-byte
read, and the detector fires only on an edge, so a held line was lost for good.

**Task 12b: the model data, then the fix.** The harness gained 116 runs, after
the 150 it had, which are unchanged byte for byte: an NMI held from each cycle
around an IRQ's vector reads (`irq-nmi-held`) and a BRK's (`brk-held`), pulses
of one to four cycles at the same places (`irq-nmi-pulse1` to `4`,
`brk-pulse1` to `4`), and a second NMI, held or pulsed, around the vector reads
of a first one (`nmi-nmi-held`, `nmi-nmi-pulse1` to `4`). `generate.sh` printed
`266 runs written`. Against the core as it was, 13 failed
(`dotnet test tests/Dbhq.Cpu6502.Tests -c Release --filter
"FullyQualifiedName~TransistorModel"`, on 4 October): the held and
three- and four-cycle runs from the IRQ's and BRK's vector-low cycle (lost by
the core, taken by the chip), the one-cycle pulse from their vector-high cycle
(kept by the core, lost by the chip), and every NMI-on-NMI run from the first
NMI's vector-high cycle (kept by the core, lost by the chip, held or not).

What the model shows, as rules: on a BRK or IRQ, an NMI edge that comes while
the vector is read is taken, after the handler's first instruction, if the line
is still active in the cycle after the vector's high byte, and lost if not. On
an NMI, a second edge that comes while its own vector is read is lost whatever
the line does. The reviewer's sketch (drop the edge and forget the line's last
level, so the next cycle latches it again if it is still active) gives the
first; the second needed the high-byte edge dropped too on the NMI path, with
the last level kept, so a held line makes no new edge. The fix is in
`EnterHandler` only, which runs once an interrupt; nothing changed in the
per-cycle path. All 266 runs pass, and the whole core suite, Harte's, Dormann's
and nestest's included, 1,597 tests, at 00:58 to 01:00 UTC; the KIM-1's 39 too.

**The 8271 went back to the sheet's rule:** a late byte ends the command with
`$0A` and INT stays high from the byte to the result, and the class's remark that
INT falls only at an access is true again. `*TITLE` finishes, and the BBC
project's tests pass.

**How often it would have bitten.** A scratch soak test (not committed) typed a
BASIC loop into the machine that, for each I, saved a file, ran `*INFO *`,
loaded the file and ran `*CAT`, four disc commands with a random pause of BASIC
work between them, and watched the loop counter, on four seeds of `RND`. With
the old core and the sheet's late-data rule every seed stalled for good, after
56, 117, 171 and 173 iterations, 224 to 692 commands (01:09 to 01:10 UTC). With
the fixed core all four ran their 1,250 iterations to the end, 20,000 disc
commands, about 2.57 billion cycles each, with no stall (01:04 to 01:09 UTC).
With the one-cycle gap of `3008e63` the review estimated a hang about once in
2,000 to 4,500 disc commands, from the lost results alone.

**The KIM-1 page runs this core too.** Its NMI is the ST key, held while it is
down, and the single-step line (`Kim1Bus.cs`). The KIM-1 model wires no IRQ to
the CPU, but a program's `BRK` reads its vector the same way, so an ST press
whose edge landed on that cycle would have been lost where the real chip takes
it. The rule was wrong for every machine on the core. Stage 1's
journal entries and the design spec carry dated correction notes.

### DFS throws its catalogue away after a listing

The sheet (s2b) said DFS reads the catalogue again only when its ready test
fails or the drive changes, so a disc swapped behind its back is not seen until
the head unloads. The brief's swap test followed it: `*CAT`, swap, `*CAT`, and
expect the old catalogue. The machine showed the new one at once. Logging the
8271 commands showed that the second `*CAT` read the catalogue and the first did
not, and the ROM says why. `*CAT` sorts its listing inside the RAM copy of the
catalogue, clearing the directory byte of each entry in the current directory
(`$A3FB-$A417`), and then marks the copy invalid by setting `&1082`, the drive
the copy came from, to `$FF` (`$A420`). The check at `$AA5F-$AA69` keeps the copy
only when the drive is ready and `&1082` is the drive. One entry of the DFS's
error routine (`$9FB8`, used for `Locked`) does the same; another (`$9FC8`, used
for `Not found` and for the Escape from the ready poll) does not.

So the swap test now uses `*INFO *` for the stale reads, as the probe's own
measurement did, and checks the `*CAT` rule as well: after a swap a cached
`*CAT` still shows the old title, the command after it reads the new disc, a
swap back is stale again, and after 4.8 million cycles of nothing the head has
unloaded and the next command reads the disc that is in. The sheet's s2b is
corrected.

### The head-unload swap rule

How DFS notices a new disc, which is how a page will have to work: it reads the
catalogue again only after its ready test fails, and the drive stops being ready
only when the 8271 unloads the head, 2.4 seconds after the last drive command
with DFS's specify. A disc swapped sooner shows the old catalogue, on a real
machine as here, unless a `*CAT` or one of those errors came between. A page
that lets someone drop a new image in should either reset the machine or wait
for the head to unload.

### Errors come after a blank row

Every DFS error typed at the BASIC prompt shows on the row after a blank one.
That is BASIC, not DFS: its default error handler (BASIC `$B433`) is `REPORT`,
which starts with `JSR $BC25`, and `$BC25` is `JSR OSNEWL` (BASIC
`$BFE4-$BFE7`). The tests read the message from the row after the blank, and the
sheet's s6d now says so.

### The messages

As the sheet found when it ran the ROM, and not what one would first guess:
`Disk fault 18 at 00/00`, spelled "Disk"; `Not found`, not "File not found";
`Bad command` for an unknown `*` command and for `*RUN` of a missing file;
`Disk read only`; and no "Not ready" message at all, because the ROM has none:
an empty drive is polled until Escape. The tests read each off the picture.

### The 128 cycles, and how they were found

The byte time is the sheet's: an 8 microsecond bit cell (S2 5.5.1) makes a byte
64 microseconds, 128 CPU cycles; the datasheet's 32 microseconds is the 8-inch
figure. The probe then ran DFS saving and loading with the byte every 300, 128,
100, 90 and 80 cycles (all passed) and every 76, 70 and 64 (nested NMIs, a crash
or `Disk fault 18`): DFS's NMI handler takes 71 cycles for a write and 77 for a
read, so a byte must not come before the last has been handled. Planting 64 here
(below) broke seven of the nine disc tests, through the real DFS, so the C#
machine agrees.

### The boot

With the 8271 fitted and the drive empty, the boot screen gains `Acorn DFS` and
a blank row after the banner, so `BASIC` is on row 5 and the prompt on row 7, in
every mode. The boot tests take their rows from `BootScreen.Rows`, which now has
the DFS's line, read from the ROM at `$B3B4`, and all eight modes' picture tests
pass with it: nine characters do not wrap in the twenty-column modes. `bus.md`
s4b had the row [inferring] until now. A scratch test logging every access to
`&FE80-&FE9F` through the boot (00:29 UTC, not committed) found what the DFS does
to the controller with the drive empty: 27 status reads, a specify of each kind
(`35 0D 0C 0A C8`, `35 10 FF FF 00`, `35 18 FF FF 00`) and the mode register
(`3A 17 C1`), and nothing that uses the drive, so an empty drive does not hold
up the boot. It was the same with a disc in. The specify is the fourth of the
table's four (s2), the one OSBYTE `&FF`'s bits 5 and 4 pick when both are set;
its index count is 12, like the others'.

## The tests, and whether they can fail

**The order of work, stated plainly.** The chip and the image were written
before their tests, not after, against the project's method: the design had
been worked out in full from the brief and the sheet before any code, and the
first thing run was a smoke test that booted and typed `*CAT`. The tests were
then written from the sheet, not from what the code printed, and the
failing-first check was made the other way round: with the 8271's decode taken
off the bus, which is the code before this task (the last planted mistake
below), every disc test, both bus decode tests, five boot tests and the bus and
machine equivalence tests fail.

- `DiscImageTests`: the blank catalogue for 40 and 80 tracks and two sides, the
  offset of a sector in both layouts, a sector off the disc, 40 or 80 tracks by
  length, short images extended with zeros, and copies in and out.
- `Fdc8271Tests`, the chip alone: the sheet's tidy test (s3), byte strings
  copied from the sheet, with 512 NMIs exactly 128 cycles apart, the first 2,000
  cycles after the command, the bytes the disc holds, then INT, status `$18`,
  result `$00`; the status values DFS saw; Read Drive Status with ready following
  the motor and write protect; not ready latched until Read Drive Status; no
  drive selected; write protect; sector not found four ways, with the scan sector
  register DFS prints; a 128-byte command; side select; Write Data; Verify; late
  data, INT held from the byte to the result (in `3008e63` it fell and rose, see
  above); the head unloading in the very cycle, read
  through `7D 23`, on two machines run the same way to the cycle before and the
  cycle of it; index count 15; reset.
- `DfsCatalogueTests`: the helper that reads a catalogue, written from the
  published layout and checked against the sheet's two worked examples before
  any disc test uses it, including the high address bits, a start sector above
  255 and the `*INFO` line the sheet gives.
- `DiscTests`, the real DFS through the machine, read off the picture: `*CAT` on
  a blank disc (s6a); `*SAVE TEST 2000 2100 2000 2000`, then the image's bytes,
  `*INFO *`, and a new machine on the same image loading the file at `&4000` and
  running it to print `A`; `*ACCESS L`, `*ACCESS`, `*TITLE`, `*OPT 4,3` and
  `*DELETE` each changing the catalogue, with the cycle number counting each
  rewrite, and `*CAT` showing the padded title and `Option 3 (EXEC)`; `Disk read
  only`; `Disk fault 18 at 00/00` for side 1 of a single-sided disc; side 1 of a
  `.dsd` as drive 2; `Not found` and `Bad command`; `*CAT` with no disc still
  polling after ten seconds of machine time, then `Escape`; and the disc swap.
- `BbcBusTests`: the 8271's addresses, every mirror, and the floating reads.

**Equivalence.** `Oracle/ReferenceFdc8271.cs` is the same model as a plain
stepper, ticked every cycle, counting down to each thing it does, written from
the model above rather than from the lazy code. Three comparisons:

- the chip on its own, six seeds of 6,000 random steps: commands leaning on
  DFS's, parameters cut short, spans of 0 to 1.3 million cycles, a handler taking
  bytes 18 to 40 cycles after each NMI and now and then too late, reads and
  writes of every register at random, resets, discs swapped and write protected.
  After every access the value, the status and INT must match, and the discs'
  bytes at the end. Each run must have seen every result DFS can meet (`$00`,
  `$0A`, `$10`, `$12`, `$18`), moved over 2,000 bytes and seen the head unload
  more than twenty times;
- the bus against the oracle bus, which now ticks the reference 8271 every cycle
  and sets the NMI line after every access: 1.5 million accesses at every 8271
  address, the handler waiting for each NMI through RAM reads and stretched VIA
  reads. After every access the value, the cycle count and both interrupt lines
  must match, so an NMI the bus sets a cycle late fails at once. It must see
  over 5,000 NMIs and every result;
- the machine against the oracle on the real DFS, every bus access compared, NMI
  included: boot with a blank disc, `*SAVE`, `*CAT`, `*LOAD`, `*RUN`, and the two
  images equal at the end. Task 6b's whole-machine equivalence test, which boots
  with no disc, now runs the DFS against the reference 8271 too.

**Planting mistakes.** On `3008e63`, before the core fix, eighteen deliberate one-line mistakes, each built and run
against the disc, bus and boot tests in place, the file restored after each
(`python3 /tmp/t12/mutate.py`, a scratch script, not committed; run from 00:14 to
00:29 UTC on 4 October). All eighteen were caught:

| Mistake | Caught by |
| --- | --- |
| A byte every 64 cycles | seven disc tests, three chip tests, two equivalence tests |
| Side from bit 4 of the port | three disc and chip side tests, two equivalence tests |
| The blank catalogue's sector count one more | the three image tests, the helper's blank test, two disc tests |
| The bus looking at the 8271 a cycle late | the bus and machine equivalence tests |
| Ready not following the motor | the head unload, ready, mirror and swap tests, three equivalence tests |
| The head never unloading | the head unload and swap tests, the chip equivalence test |
| A late byte keeping INT high to the result (the sheet's rule, which task 12b restored once the core was fixed) | the `*TITLE` disc test, the late data test, two equivalence tests |
| A `.dsd` read side after side | the two image layout tests |
| The not-ready latch never set | the not-ready test, the chip equivalence test |
| Write protect ignored | the read-only disc and chip tests, two equivalence tests |
| A data read leaving the request | fourteen tests |
| A data read not counted as an access by the bus | seven disc tests, two equivalence tests |
| The data register at one address of four | seven disc tests, two equivalence tests |
| Catching up a step early | two chip tests, three equivalence tests |
| The first byte a cycle late | two chip tests, three equivalence tests |
| INT not on the NMI line | six disc tests, two equivalence tests |
| The head unload clearing the side bit | the head unload test, two equivalence tests |
| No 8271 on the bus (the old code) | eighteen tests, the boot tests among them |

One of them only the equivalence tests catch: the bus looking at the 8271 a
cycle late. DFS does not mind an NMI a cycle late, which is why the oracle is
there.

**Mistakes on the way.** The chip test's helper had `Run(long)` for cycles and
`Run(byte, params byte[])` for a command, and `Run(1)` chose the second, which
called `Run(1)`: the test host overflowed its stack and aborted the run. Renamed
`Execute`. The machine equivalence test first expected `$41` at `&4000`, the
letter the program prints, where the file holds the program, whose first byte is
`$A9`. The bus equivalence test first ran out of accesses waiting for NMIs that
were never coming, and saw only fourteen; it now waits only while a command runs
or a result waits.

`dotnet test -c Release` from 00:40 to 00:44 UTC on 4 October: the BBC Micro's
tests 930 passed, the core's 1,481 and the KIM-1's 39, none failed, with no
warnings. The site's suite (`npm run build` then `npm test` in `site/`) at 00:41
UTC: 193 passed. *After task 12b's fix,* `dotnet test -c Release` from 01:14 to
01:18 UTC: the BBC Micro's tests 932 passed, the core's 1,597 and the KIM-1's
39, none failed, with no warnings.

### The fingerprint

Task 6b's scripted run (`dotnet run -c Release --project
bench/bbc-micro-speed/native -- --fingerprint`, which hashes every instruction
and, every 100,000 cycles, all of RAM and every VIA register) was run on 4
October on three exported copies: the base `b9a1335`, the task's commit
`3008e63`, and the fix of task 12b. The base:

```
boot cycles=6000009 instructions=1680472 irq_steps=22963 steps=6D17BC233D03E54A checkpoints=AB4528B518EC3522
program cycles=90320897 instructions=25132452 irq_steps=304378 steps=9B587FFDB4E18DB9 checkpoints=D6810809ABBAAC8C
break cycles=96320907 instructions=26794569 irq_steps=327681 steps=2313FC39D4337E97 checkpoints=B0E5F0C371BA137D
rerun cycles=116960920 instructions=32511256 irq_steps=393648 steps=8FC2DFE11EDE11DC checkpoints=93FBE6414BE5E9EE
```

`3008e63` and the fix, identical to each other:

```
boot cycles=6000010 instructions=1681181 irq_steps=29248 steps=18B3B71E06EFD0C9 checkpoints=A340322134C60133
program cycles=90320900 instructions=25133349 irq_steps=310793 steps=75411C5A03B49E88 checkpoints=62013A8E040B4B1B
break cycles=96320907 instructions=26796508 irq_steps=343669 steps=DA6C387E3E94C1FD checkpoints=DDCBBC531A5EDDA2
rerun cycles=116960932 instructions=32513241 irq_steps=409496 steps=D7CE4E494E825F88 checkpoints=838DC94503540734
```

The boot changes, and it should: with the 8271 fitted the DFS serves its calls
and prints `Acorn DFS`. A throwaway trace of every instruction of the first
400,000 from power on, on the base and the fix (`/tmp/t12/trace-*`, not
committed), first differs at the DFS's first read of the 8271's status, `LDA
&FE80` at DFS `$B495`, in the instruction ending at cycle 530,114: the base reads
`$FE`, an absent fast device, and the fix `$00`, an idle 8271. From there the
DFS goes on to specify the controller and print its line, everything after runs
at shifted cycles, and the values the script's program reads from the user VIA's
timers differ with it; the screen after BREAK gains the `Acorn DFS` row. The core
fix changes nothing in this run, which has no NMI. The CPU's own guard is its
suites: Harte, Dormann, nestest and the transistor-model runs, all passing.

## The speed

**What changed in the hot path: almost nothing.** `Read` and `Write` are as they
were for memory; a SHEILA access has one more case in its switch; `Service()`
asks the 8271 for its next step and its INT when it brings the chips up to date,
which is a comparison each when the controller is idle. At the prompt the drive
is empty and the DFS does not call the controller, so the timed workload should
not change, and the boot gains the DFS's 45 accesses.

The base commit (`b9a1335`, published from an exported copy in `/tmp/t12/old`)
against this task's code, both compiled ahead of time and natively, run
alternately with `bench/bbc-micro-speed/alternate.sh` from 00:33 to 00:39 UTC on
4 October, with the bench's prompt check now accepting the DFS's line. The
machine was busy, at one-minute load averages from 1.8 to 15.3. Medians in
millions of cycles a second:

| Set | Load (1 min), before to after | Before this task | With the 8271 |
| --- | --- | --- | --- |
| Native, old first, 00:33 | 2.09 to 1.77 | 50.664 | 43.992 |
| Native, new first, 00:33 | 2.03 to 2.84 | 51.940 | 53.799 |
| Native, old first, 00:34 | 2.84 to 3.98 | 36.124 | 44.942 |
| Native, new first, 00:34 | 3.98 to 4.89 | 56.418 | 44.029 |
| Native, old first, 00:34 | 5.14 to 5.03 | 60.276 | 57.346 |
| Native, old first, 00:36 | 8.50 to 8.21 | 56.617 | 49.741 |
| Native, new first, 00:36 | 8.21 to 7.58 | 49.419 | 57.571 |
| Native, old first, 00:36 | 7.58 to 6.80 | 50.633 | 53.692 |
| Native, new first, 00:37 | 6.80 to 6.82 | 50.881 | 45.416 |
| Browser AOT, old first, 00:34 | 5.03 to 5.46 | 18.067 (9.03 times) | 24.125 (12.06 times) |
| Browser AOT, new first, 00:35 | 5.46 to 7.84 | 21.345 (10.67 times) | 21.668 (10.83 times) |
| Browser AOT, old first, 00:35 | 7.84 to 9.50 | 22.883 (11.44 times) | 19.940 (9.97 times) |
| Browser AOT, new first, 00:36 | 9.50 to 8.80 | 24.361 (12.18 times) | 26.810 (13.40 times) |
| Browser AOT, old first, 00:37 | 6.11 to 14.12 | 26.351 (13.18 times) | 19.589 (9.79 times) |
| Browser AOT, new first, 00:38 | 14.12 to 15.28 | 14.793 (7.40 times) | 12.555 (6.28 times) |

The commands were `./alternate.sh native 5 <first> <second>` and
`./alternate.sh browser 3 <first> <second>` with the builds in
`publish/native-task12-before`, `publish/native-task12-after`,
`publish/aot-task12-before` and `publish/aot-task12-after`. Worked in Python from
the medians above: the new code's median over the old's, set by set, is 0.87,
1.04, 1.24, 0.78, 0.95, 0.88, 1.16, 1.06 and 0.89 natively (median 0.95) and
1.34, 1.02, 0.87, 1.10, 0.74 and 0.85 in the browser (median 0.94). The two
builds are level within the noise, as the code says they should be; the best
runs agree more closely than the medians (natively 63.0 to 68.2 against 60.9 to
68.7). The boot, timed by the same runs, is level natively (medians 556.7 and
539.3 ms, 552.3 and 523.2, 487.3 and 491.2, 584.6 and 636.8, old then new) and
slower in four of six browser sets (762.0 and 574.1, 422.0 and 597.3, 440.2 and
536.6, 392.7 and 453.5, 458.5 and 587.5, 780.8 and 715.6), with ratios from 0.75
to 1.41 on a host whose load rose to 15. That is not a measurement either way;
it wants a quiet hour, with task 15's.

## What this does not say

The model is the minimal controller of s3, proven against DFS 1.20's own
commands, the probe's runs and the datasheet's words; it is not checked against
a real 8271 or a real drive, and nothing here says how a copy-protected disc or
a program that drives the 8271 itself would fare. The figures above describe the
machine at this task's commit on one shared virtual machine.
