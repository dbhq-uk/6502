---
title: "The BBC Micro's bus, made cheap to run"
date: 2026-10-03
summary: "The two VIAs now work out their own cycles only when something looks at them, and the bus only looks at them when an interrupt could change. The machine runs well over twice as fast in a browser, past the speed this task aimed for, and a copy of the old chips kept as an oracle shows it does exactly what it did before, cycle for cycle."
order: 16
---

# 3 October 2026: the BBC Micro's bus, made cheap to run

Task 6 stopped the plan: the machine as built ran at a median of 7.13 times a
2 MHz BBC Micro in a browser, compiled ahead of time, under the plan's 10
([the speed entry](2026-10-02-the-bbc-micro-speed.md)). Dan's call on 3 October
was to make the bus and its chips cheaper now, before the video chips add their
own per-cycle work, because a restructure later costs more. This is task 6b,
which is not in the plan text. The rules for it: nothing the machine does may
change, every bus access is still one cycle, a VIA's flag and IRQ still land on
the exact cycle the fact sheet's worked examples say, and the browser build is
what gets measured.

**The result: a median of 16.98 times a 2 MHz machine in the browser,
compiled ahead of time, against 6.81 for the old code in the same session.**
The brief for this task set a target of 15. The fingerprint of a scripted run of
117 million cycles is identical before and after, and so is every bus access of
the real OS compared side by side with the old code.

## The bus, optimised

### How it was measured

- **Native:** a console program, `bench/bbc-micro-speed/native/`, runs the
  browser page's workload (boot for 6 million cycles, check the prompt, then time
  `Run(2_000_000)`), and two more modes: `--fingerprint`, which hashes every
  instruction's cycle count, registers and IRQ line, and at every 100,000 cycles
  all of RAM and every VIA register, pin and line, through a script that types a
  BASIC program driving the user VIA's timers, shift register, pulse counting and
  handshakes, runs it, presses BREAK and runs it again; and `--profile`.
- **Browser:** the task 6 page and script, unchanged. Each build was published
  once, and builds were compared by alternating single browser launches, five
  timed runs each, so drift in the machine's load falls on both.
- **Alternating** is `bench/bbc-micro-speed/alternate.sh`, written during the
  task as two scripts in `/tmp` and committed at the end as one: `browser` mode
  runs one launch of each build in turn, `native` mode one launch of twelve timed
  runs of each in turn and keeps the last ten. Both print the load average before
  and after.
- **The machine was shared.** A Python job and a database backup ran on it
  during the morning; every set below gives `uptime`'s load average before and
  after.

### The profile, before any change

`--profile` at commit `54a6630`, pinned to one core (`taskset -c 5`), eleven
rounds after a warm-up round. It runs the booted machine and takes parts away.
It was first run at 07:18 UTC, before any change, and gave minimums of 29.11,
16.13, 22.01, 25.48 and 10.40 for the five rows below; the table is a second run
of the same commit, from an exported copy, at 08:19 UTC with a load average of
0.83, kept because it ran beside the round 1 profile. Removing a chip changes what the
OS does (no ticks, no interrupts), so each difference is an estimate, not a
measurement. A run's minimum is the figure least disturbed by the load:

| Variant | ns per CPU cycle (min) | median |
| --- | --- | --- |
| The whole machine | 29.01 | 31.99 |
| No chip ticks at all | 16.27 | 17.18 |
| Only the user VIA ticking | 21.55 | 22.41 |
| Only the system VIA, with the keyboard, ticking | 24.98 | 26.00 |
| The bare CPU on a flat 64 KB copy of the memory | 10.49 | 11.04 |

So about 45 per cent of a cycle went on ticking the two VIAs, about 20 per cent
on the bus itself (the stretch check, the decode, the virtual `Tick()` call and
setting the IRQ line after every access), and about 35 per cent on the CPU. The
system VIA cost more than the user VIA, probably because of the keyboard's part,
which looped over seven rows of the column on every 1 MHz cycle; with only that
VIA ticking the OS also takes its interrupts, so the two rows are not quite the
same work. A sampling profile
(`dotnet-trace`, about a hundred samples a second) was tried first and was no
use: it put two thirds of the time in the CPU's `ExecuteOfficial` and only an
eighth in the VIAs' ticks, which the subtraction above contradicts. The likely
reason, not checked, is that the JIT inlines the bus call into the CPU, so the
samples land in the caller.

### Decision: the chips run lazily, and catch up when looked at

Chosen over three smaller fixes: a fast path in each VIA's tick for "nothing
pending", ticking the VIAs in batches from the bus, and caching the keyboard's
column state. Each of those still does some work on every 1 MHz cycle, and the
profile said the work itself was the cost.

A VIA now reads the time from the machine's clock and does nothing when a cycle
happens. Every member that shows the chip or changes it (a register access, a
pin, a line, an input, a reset) first catches it up to the clock, so nobody can
see stale state; there is no new place for the machine's state to hide, only
work done later. The clock is a small internal class, `BbcClock`, holding the
CPU cycle count the bus moves and the next event; the bus owns it and hands it to
the two VIAs. Chosen over making the bus itself the chips' clock (it would make
a public class derive from an internal one) and over a delegate the chip calls
for the time, which the AOT build pays for on every call.

`Tick()` on a VIA still exists and still means "one 1 MHz cycle passes", for a
chip on its own, which is how the sixteen worked examples drive it. A chip in a
machine refuses it.

### Decision: each part of the chip worked out in closed form

Between two looks nothing from outside reaches the chip, so each part runs on
its own and can be done for any number of cycles in a few lines: timer 1
counting down, wrapping and going round its latch (latch + 2 cycles a turn,
toggling PB7 and flagging each turn when free-running, flagging once when
armed); timer 2 counting down and on round from &FFFF; timer 2 counting PB6
pulses, which can count only in the first cycle because the pins do not move
between accesses; the shift register's count; the CA2 and CB2 pulses; and the
keyboard's part. A flag set in the last of the cycles counts as "just set", so
the coincident acknowledge still works. The old per-cycle rules are kept as
`TickOnce` and used for the one case that is not worked out at once: autoscan
with a key held down, when CA2 rises and falls as the counter passes the key's
column.

**The first version of this was slower, and the reason is worth keeping.** It
skipped only "quiet" cycles (counters counting down, nothing else) and ran every
other cycle through `TickOnce`, and the bus looked at the chips at every
non-quiet cycle. Natively it went from 35.91 to 26.15 MHz. At the BASIC prompt
the user VIA's timer 1 is in one-shot mode with a latch of 0, so it wraps and
reloads on alternate 1 MHz cycles for ever: no cycle was quiet, and the bus
brought both chips up to date on every instruction. A throwaway probe that read
the chips' fields after boot showed it (user VIA: ACR 0, timer 1 counter and
latch 0; the bus's next event moved on every one of 200,000 instructions). That
led to the closed forms above and to the next decision.

### Decision: the bus looks only when an IRQ line could change

The event horizon. After each access the bus compares the cycle count with the
earliest cycle at which some chip's IRQ line could change by itself, and only
then brings the chips up to date, sets the CPU's IRQ line and asks each chip for
its next event. A chip's line can change by itself only when a flag enabled in
IER rises, or when a coincident acknowledge's one-cycle wait ends; a raised line
stays raised until an access, because only an access clears a flag. A flag that
is not enabled is just state, which a later look picks up. The bus also looks at
the end of every VIA access, and a chip changed from outside (a key, a line, a
reset) asks to be looked at at the end of the next cycle, which is when the old
bus would have set the IRQ line anyway. Chosen over looking at every chip
event, which is what the slow first version did.

At the prompt the same probe showed what is enabled: the system VIA's IER is
&73 (CA2, CA1, CB1, timer 2 and timer 1) with timer 2 counting PB6 pulses, which
never come, and the user VIA's IER is 0. With no key down and no vsync yet, the
only thing that can raise the line by itself is the system VIA's timer 1, the
OS's hundred-a-second tick, so between those the bus does no work for the chips
apart from the comparison [inferring from the probe and the code; the number of
looks was not counted].

### The tests that changed, and why

`BbcBusTests` had two buses that overrode the bus's per-cycle `Tick()`, which no
longer exists. Their meaning is kept with a different observation point, the
user VIA's timer 1:

- **Every cycle reaches the chips exactly once.** The test used to count calls of
  `Tick()`. Now the timer is started at &FFFF before the measured accesses, and
  after each one it must have counted down by exactly the number of even cycle
  counts the access passed. That is a check on the chip, where the old one was a
  check on a call.
- **The chips tick before the access of their cycle.** The test used to record,
  at the start of every cycle, the ROM latch and the system VIA's IER. Now a T1C-H
  write from an odd count (three cycles) is followed by reads of T1C-L: the counter
  must still hold N in the next cycle and be N-1 in the one after (`via.md`
  s1.4). Were the write before its own cycle's tick, that tick would spend the
  hold and the first read would see N-1. The ROM latch part was dropped: no chip
  reads the latch, so there is nothing to observe.
- `PeekHasNoCycleAndNoSideEffect` checked that the tick count stayed 0; it now
  checks that the timer stands still across peeks, as well as the cycle count.

No assertion was loosened. The BBC project's tests went from 427 to 434.

### The oracle, and how it was checked

Before changing anything, the per-cycle `Via6522`, `SystemVia`, `UserVia` and
`BbcBus` were copied into the test project as `ReferenceVia6522` and friends,
unchanged but for their names (`tests/Dbhq.Machines.BbcMicro.Tests/Oracle/`),
with the equivalence tests, committed and passing against the identical code
first. They are:

- the VIA, and the system VIA with its keyboard, against the oracle through runs
  of random accesses, input changes, key presses and resets (over 100,000
  accesses each), alternating busy stretches with quiet ones of up to 40,000
  cycles. One copy is compared with the oracle in every cycle; a second is looked
  at only when accessed and at random moments, so it catches up over long spans,
  which is the path the machine takes;
- the whole machine on the real OS against the oracle bus, an instruction at a
  time, comparing every bus access's address, value, direction, cycle count and
  the CPU's IRQ line after it, through boot, a typed BASIC program that drives the
  user VIA, its run, and BREAK;
- the bus alone against the oracle bus through 250,000 random operations (VIA
  accesses, keys, lines and resets) with gaps of RAM reads, comparing the value,
  the cycle count and the IRQ line after every access;
- one fixed case, timer 2 counting pulses at a count of 1, described below.

**Whether the tests would see a mistake was tested by making mistakes.** Seven
deliberate one-line errors in the first version and ten in the final one: a
timer's turn one cycle short, PB7's parity flipped, an IRQ event a cycle late,
timer 2 off by one after a wrap, the keyboard settling a cycle early, the
coincident acknowledge's wait not treated as an event, a flag counted as just set
when it was not, and others. The final ten were all caught. Two got through on
the way, and each found a gap that is now closed:

- **A late IRQ after a coincident acknowledge** got through the first version.
  The OS rarely acknowledges a flag in the very cycle it rises with that interrupt
  enabled, and the random tests nearly always touched a VIA again in the next
  cycle, which hid the late line. The bus-level random test was added for it, with
  RAM reads after every VIA access.
- **Timer 2's pulse-count flag counted as just set over a long catch-up** got
  through the final version, because the random runs rarely line up an armed count
  of 1, a PB6 fall, two cycles nobody looks at, and an acknowledge. It now has a
  fixed test of its own, and the random values lean towards small counts. In the
  machine PB6 never moves, but the chip should be right anyway.

**The oracle also caught a real mistake.** The first closed form for the
keyboard did its part once for the whole span, on the reasoning that, enabled,
a second cycle repeats the first. That holds only if PA0 to PA6 of the input
were already 1, which in the machine they always are. A test that sets the input
from outside showed that the first cycle can read its column from the outside
value, so it takes two cycles to settle. The closed form now does the part twice
when two or more cycles are owed.

### Decision: memory first in the bus, and the paged ROM by reference

Round 2. `Read` and `Write` now test for RAM, then paged ROM, then the OS ROM,
before anything to do with I/O; the paged ROM is an array reference that the
latch sets, with an empty slot as an array of its addresses' high bytes, in place
of a switch on the slot in every read; and the bus class is sealed, now that no
test subclasses it. Chosen over copying the paged ROM into one 64 KB array on
every latch write, which makes a latch write cost a 16 KB copy, and over
`Unsafe` reads to drop the bounds checks, which the code has never used.

It made no difference natively, where the JIT had already done the work, and
about 12 per cent in the browser, which is why the brief says to measure there.

### Each change, measured

Native: five alternating launches a build, keeping the last ten of twelve timed
runs each, so fifty runs a build. Browser AOT: three alternating launches of five
timed runs, so fifteen a build. Medians in millions of CPU cycles a second; the
load is the one-minute average before and after each set.

| Change | Native, before to after | Browser AOT, before to after | Load (1 min) |
| --- | --- | --- | --- |
| Lazy VIAs, first version (not committed) | 35.91 to 26.15 | not measured | 2.70 to 2.70 |
| Round 1: lazy VIAs, closed forms, IRQ-only event horizon (`c16ba87`) | 33.96 to 88.45 | 13.615 to 33.841 | native 2.78 to 2.74; browser 1.34 to 1.26 |
| Round 2: memory first, paged ROM by reference (`98fe9d5`) | 93.72 to 93.35 | 34.014 to 38.314, then reversed 33.613 to 35.971 | native 1.47 to 1.43; browser 1.21 to 1.57, then 1.60 to 1.75 |

The browser figure for round 2 was taken twice, the second time with the order
of the launches reversed, because the first gain was small enough to be noise.
It was not.

### Decision: stop after two rounds

The brief allowed three. On the same morning the bare core's own browser
benchmark (`bench/Dbhq.Cpu6502.WasmBench`, AOT, five runs from 08:26 UTC, load
1.39) ran at a median of 51.956 MHz. The whole machine now runs at about two
thirds of the bare core's speed in a browser, against about a quarter before, so
even a bus and VIAs that cost nothing would make it at most about half as fast
again. The core's benchmark calls its bus through the same interface, so that
call is not in the difference; but it runs a synthetic loop, not the OS, so the
ratio is a rough one. The video chips are where the next cost will be, and they
should be built on this before anything else is tuned.

### How a new chip plugs in

The CRTC, the video ULA, the sound chip and the 8271 (tasks 7, 8, 11 and 12)
each join the same way. *This section was corrected in task 7, when the CRTC
became the first chip to join: step 4 said what had to happen but not how,
because the VIA had no way to catch up to a cycle in the past, and step 3 did
not say that the order of the terms in `Service()` matters. Both are below as
they are now built.*

1. **It takes the clock.** An internal constructor takes the `BbcClock`; the chip
   keeps how far it has got and has a `Sync()` that does the cycles owed. Every
   public member that shows or changes the chip calls it first, and the chip's own
   cycle work never calls a member that does, so catching up cannot start inside
   itself (the VIA throws if it does). A chip on its own can keep a `Tick()` for its
   tests, as the VIA does.
2. **It says when it next needs to be seen.** An internal `NextEventCycle`: the
   earliest CPU cycle at which something it does by itself must be visible outside
   it. For the VIAs that is an IRQ line changing. For the CRTC it is each edge of
   VSYNC, because VSYNC is the system VIA's CA1, and each frame start; it works
   these out ahead and keeps the answer until a register write makes it stale. A
   chip changed from outside calls the clock's `WakeAt` so the bus looks at the end
   of the next cycle.
3. **The bus has three places for it:** a case in the SHEILA decode
   (`ReadSheila`, `WriteSheila`, `PeekSheila`) followed by `ChipAccessed()`; a term
   in the minimum in `Service()`; and a line in the resets. **The order in
   `Service()` matters:** a chip that drives another chip's input is brought up to
   date first (`Crtc.SyncIfDue()`), before the driven chip's `NextEventCycle` and
   `Irq` are read, or the driven chip would report its line and its next event
   without an edge that has already happened.
4. **A chip that drives another chip's input delivers each edge with its cycle.**
   The driven chip's `Sync()` first calls `SyncInputs()`, which the system VIA
   uses to bring the CRTC up to date if one of its events may be due by now
   (`SyncIfDue`: one comparison when none is). The CRTC, catching up, hands each
   VSYNC edge to the system VIA with the CPU cycle it happened in
   (`SetVsyncAt(cycle, level)`), and the VIA catches up to that cycle, not to
   now (`SyncTo(cycle)`), then takes the edge, so the edge lands after that
   cycle's tick and before the next, exactly as a chip ticked every cycle would
   see it. `SyncTo` throws if the VIA has already done that cycle, so an edge can
   never land late without a test noticing. `SyncTo` does not call `SyncInputs`,
   so a delivery cannot start another.
5. **Work that cannot be worked out at once is done in a loop at catch-up.** The
   video ULA has to draw pixels; it can draw the cycles owed in one tight loop when
   it is looked at, or when a frame ends, rather than being called on every
   access.
6. **A chip that consumes another chip's output is driven by its own events.**
   *Added in task 7's review.* The video ULA reads the CRTC's MA, RA, display
   and cursor; that is the other way round from step 4, and it has three rules.
   (a) It must not rely on the producer's inner stepping: the CRTC's progress
   is nearly always a jump to the state it worked out ahead, which skips
   `Advance` entirely, and its working-out happens early, before later writes.
   So the consumer names its own events in the bus's minimum (a line start, or
   a frame), and at each one brings the CRTC to that cycle (`SyncTo`) and reads
   the point state it needs. (b) It must bring itself, and the producer, up to
   date before any write that changes what it consumes: a CRTC register write
   (R1, R12 and R13, the cursor) and the ULA's own registers. Screen memory the
   CPU writes in the middle of a line is a question task 8 has to settle,
   because catching up on every RAM write would put a cost on every write.
   (c) It reads the point getters at most once a line;
   one call a character is millions a second, more than the browser can spend.

### The final figures

The same method as task 6: the final code (`98fe9d5`) published fresh, both
builds, and run alternately with the old code (`2876852`, published from an
exported copy of that commit) in single launches of five timed runs, three
launches each. Headless Chrome 153.0.8010.47, .NET SDK 10.0.400, the same KVM
virtual machine.

```
./alternate.sh browser 3 publish/aot-baseline publish/aot                    # 08:28:52 to 08:29:07 UTC, load 1.47 to 1.37
./alternate.sh browser 3 publish/interpreter-baseline2 publish/interpreter   # 08:30:02 to 08:31:48 UTC, load 1.51 to 4.65
```

These were run with the `/tmp` script that became `alternate.sh`, which does
the same thing. The timed lines, as printed (the launch number is per call of
`run-in-browser.mjs`, so every line says launch 1):

```
publish/aot-baseline launch 1 timed 1 cycles=2000003 ms=146.800 cycles_per_second=13623999 mhz=13.624
publish/aot-baseline launch 1 timed 2 cycles=2000000 ms=139.500 cycles_per_second=14336918 mhz=14.337
publish/aot-baseline launch 1 timed 3 cycles=2000001 ms=143.900 cycles_per_second=13898548 mhz=13.899
publish/aot-baseline launch 1 timed 4 cycles=2000001 ms=166.900 cycles_per_second=11983229 mhz=11.983
publish/aot-baseline launch 1 timed 5 cycles=2000001 ms=158.100 cycles_per_second=12650228 mhz=12.650
publish/aot launch 1 timed 1 cycles=2000003 ms=53.100 cycles_per_second=37664840 mhz=37.665
publish/aot launch 1 timed 2 cycles=2000000 ms=52.800 cycles_per_second=37878788 mhz=37.879
publish/aot launch 1 timed 3 cycles=2000001 ms=55.800 cycles_per_second=35842312 mhz=35.842
publish/aot launch 1 timed 4 cycles=2000001 ms=52.000 cycles_per_second=38461558 mhz=38.462
publish/aot launch 1 timed 5 cycles=2000001 ms=53.300 cycles_per_second=37523471 mhz=37.523
publish/aot-baseline launch 1 timed 1 cycles=2000003 ms=136.300 cycles_per_second=14673536 mhz=14.674
publish/aot-baseline launch 1 timed 2 cycles=2000000 ms=137.300 cycles_per_second=14566642 mhz=14.567
publish/aot-baseline launch 1 timed 3 cycles=2000001 ms=139.700 cycles_per_second=14316399 mhz=14.316
publish/aot-baseline launch 1 timed 4 cycles=2000001 ms=143.700 cycles_per_second=13917891 mhz=13.918
publish/aot-baseline launch 1 timed 5 cycles=2000001 ms=135.400 cycles_per_second=14771056 mhz=14.771
publish/aot launch 1 timed 1 cycles=2000003 ms=61.800 cycles_per_second=32362508 mhz=32.363
publish/aot launch 1 timed 2 cycles=2000000 ms=61.600 cycles_per_second=32467533 mhz=32.468
publish/aot launch 1 timed 3 cycles=2000001 ms=63.300 cycles_per_second=31595592 mhz=31.596
publish/aot launch 1 timed 4 cycles=2000001 ms=60.400 cycles_per_second=33112599 mhz=33.113
publish/aot launch 1 timed 5 cycles=2000001 ms=64.900 cycles_per_second=30816656 mhz=30.817
publish/aot-baseline launch 1 timed 1 cycles=2000003 ms=156.500 cycles_per_second=12779572 mhz=12.780
publish/aot-baseline launch 1 timed 2 cycles=2000000 ms=155.500 cycles_per_second=12861736 mhz=12.862
publish/aot-baseline launch 1 timed 3 cycles=2000001 ms=174.400 cycles_per_second=11467896 mhz=11.468
publish/aot-baseline launch 1 timed 4 cycles=2000001 ms=166.600 cycles_per_second=12004808 mhz=12.005
publish/aot-baseline launch 1 timed 5 cycles=2000001 ms=176.500 cycles_per_second=11331450 mhz=11.331
publish/aot launch 1 timed 1 cycles=2000003 ms=59.600 cycles_per_second=33557097 mhz=33.557
publish/aot launch 1 timed 2 cycles=2000000 ms=58.500 cycles_per_second=34188034 mhz=34.188
publish/aot launch 1 timed 3 cycles=2000001 ms=55.800 cycles_per_second=35842312 mhz=35.842
publish/aot launch 1 timed 4 cycles=2000001 ms=58.900 cycles_per_second=33955874 mhz=33.956
publish/aot launch 1 timed 5 cycles=2000001 ms=65.300 cycles_per_second=30627887 mhz=30.628
publish/interpreter-baseline2 launch 1 timed 1 cycles=2000003 ms=1759.300 cycles_per_second=1136817 mhz=1.137
publish/interpreter-baseline2 launch 1 timed 2 cycles=2000000 ms=1640.900 cycles_per_second=1218843 mhz=1.219
publish/interpreter-baseline2 launch 1 timed 3 cycles=2000001 ms=1720.300 cycles_per_second=1162589 mhz=1.163
publish/interpreter-baseline2 launch 1 timed 4 cycles=2000001 ms=1634.200 cycles_per_second=1223841 mhz=1.224
publish/interpreter-baseline2 launch 1 timed 5 cycles=2000001 ms=1602.400 cycles_per_second=1248128 mhz=1.248
publish/interpreter launch 1 timed 1 cycles=2000003 ms=462.400 cycles_per_second=4325266 mhz=4.325
publish/interpreter launch 1 timed 2 cycles=2000000 ms=457.300 cycles_per_second=4373497 mhz=4.373
publish/interpreter launch 1 timed 3 cycles=2000001 ms=475.000 cycles_per_second=4210528 mhz=4.211
publish/interpreter launch 1 timed 4 cycles=2000001 ms=512.300 cycles_per_second=3903964 mhz=3.904
publish/interpreter launch 1 timed 5 cycles=2000001 ms=460.900 cycles_per_second=4339338 mhz=4.339
publish/interpreter-baseline2 launch 1 timed 1 cycles=2000003 ms=4076.800 cycles_per_second=490582 mhz=0.491
publish/interpreter-baseline2 launch 1 timed 2 cycles=2000000 ms=3850.100 cycles_per_second=519467 mhz=0.519
publish/interpreter-baseline2 launch 1 timed 3 cycles=2000001 ms=5844.500 cycles_per_second=342202 mhz=0.342
publish/interpreter-baseline2 launch 1 timed 4 cycles=2000001 ms=2920.900 cycles_per_second=684721 mhz=0.685
publish/interpreter-baseline2 launch 1 timed 5 cycles=2000001 ms=1874.500 cycles_per_second=1066952 mhz=1.067
publish/interpreter launch 1 timed 1 cycles=2000003 ms=433.700 cycles_per_second=4611490 mhz=4.611
publish/interpreter launch 1 timed 2 cycles=2000000 ms=474.500 cycles_per_second=4214963 mhz=4.215
publish/interpreter launch 1 timed 3 cycles=2000001 ms=497.800 cycles_per_second=4017680 mhz=4.018
publish/interpreter launch 1 timed 4 cycles=2000001 ms=476.500 cycles_per_second=4197274 mhz=4.197
publish/interpreter launch 1 timed 5 cycles=2000001 ms=490.800 cycles_per_second=4074982 mhz=4.075
publish/interpreter-baseline2 launch 1 timed 1 cycles=2000003 ms=1640.500 cycles_per_second=1219142 mhz=1.219
publish/interpreter-baseline2 launch 1 timed 2 cycles=2000000 ms=1630.300 cycles_per_second=1226768 mhz=1.227
publish/interpreter-baseline2 launch 1 timed 3 cycles=2000001 ms=1678.600 cycles_per_second=1191470 mhz=1.191
publish/interpreter-baseline2 launch 1 timed 4 cycles=2000001 ms=1566.800 cycles_per_second=1276488 mhz=1.276
publish/interpreter-baseline2 launch 1 timed 5 cycles=2000001 ms=1606.200 cycles_per_second=1245176 mhz=1.245
publish/interpreter launch 1 timed 1 cycles=2000003 ms=437.900 cycles_per_second=4567260 mhz=4.567
publish/interpreter launch 1 timed 2 cycles=2000000 ms=431.400 cycles_per_second=4636069 mhz=4.636
publish/interpreter launch 1 timed 3 cycles=2000001 ms=444.800 cycles_per_second=4496405 mhz=4.496
publish/interpreter launch 1 timed 4 cycles=2000001 ms=486.600 cycles_per_second=4110154 mhz=4.110
publish/interpreter launch 1 timed 5 cycles=2000001 ms=583.200 cycles_per_second=3429357 mhz=3.429
```

In millions of cycles a second, and as multiples of 2 MHz (worked in Python from
the printed values):

| Build | Best of 15 | Median of 15 | Slowest of 15 | Median per launch |
| --- | --- | --- | --- | --- |
| AOT, old code | 14.771 (7.39 times) | 13.624 (6.81 times) | 11.331 (5.67 times) | 6.81, 7.28, 6.00 times |
| **AOT, final** | 38.462 (19.23 times) | **33.956 (16.98 times)** | 30.628 (15.31 times) | 18.83, 16.18, 16.98 times |
| Interpreter, old code | 1.276 (0.64 times) | 1.191 (0.60 times) | 0.342 (0.17 times) | 0.61, 0.26, 0.61 times |
| Interpreter, final | 4.636 (2.32 times) | 4.215 (2.11 times) | 3.429 (1.71 times) | 2.16, 2.10, 2.25 times |

Every AOT run of the final code is over the 15 times this task aimed for. The
two round 2 measurements of the same code earlier the same morning gave medians
of 38.314 and 35.971, so the spread from one set to the next is about a tenth.
The interpreter is now faster than a real BBC Micro, where it was slower.

**The load caveat.** The AOT set ran at a load average of about 1.4. During the
interpreter set the load rose to 4.65, and the second launch of the old
interpreter build was hit hardest: its runs fell to 0.342 MHz, against 1.2 in
the other two launches. Its median per launch is shown, and the median of
fifteen includes it; without that launch the old interpreter's median is 1.22.
The first publish of the old interpreter build failed to start in the browser
(`ExitStatus`), because it was made straight after an AOT publish in the same
copy of the source, the stale-output trap the bench's README warns about; it was
published again from a clean copy, as `interpreter-baseline2`.

Natively, `./alternate.sh native 5 publish/native-baseline publish/native-round2`
from 08:34:56 UTC (load 0.81 to 1.00) gave the old code a median of 33.609 MHz
and the final code 82.213, fifty runs each.

## What this does not say

One shared virtual machine, one browser, one morning. The workload is still the
OS idling at the prompt, with no video, sound or disc, and the video chips will
add their own cost. The figures describe the machine at commit `98fe9d5` and
nothing later.
