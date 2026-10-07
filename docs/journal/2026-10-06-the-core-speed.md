---
title: "The core's speed: what the shared 6502 costs, and what made it cheaper"
date: 2026-10-06
summary: "Task 17: after the NES was finished, Dan chose to speed up the shared core first. Two changes were kept: the interrupt poll is worked out once an instruction instead of every cycle, and a machine's bus is called through an abstract class instead of the interface. In the browser's AOT build the BBC Micro got about 17 % faster; natively the core alone about 13 %. The NES did not move beyond noise, because in its AOT build the core is about a tenth of the time. Ten times real time for the NES was not reached and cannot be reached from the core: the PPU, the sound unit and the bus's own cycle are most of its cost. A generic CPU made for each machine's bus, the obvious large change, was built, measured and dropped: Mono's AOT compiler gives it no direct calls."
order: 35
---

# 6 October 2026: the core's speed

After the NES was finished, Dan decided to "speed up the shared core first". The
NES runs SNOW at about 2 to 3 times real time in the browser's AOT build, and ten
times would give comfortable headroom. Task 6b of the NES work
([the NES speed entry](2026-10-05-the-nes-speed.md)) had already made the PPU, the
sound unit and the boards cheaper, and estimated that the CPU and the bus alone,
with the PPU and the sound unit free, would run at 5.5 to 7.5 times. This task
looked at the other part: the core in `src/Dbhq.Cpu6502`, which all three machines
share, and the path each machine's bus takes once a cycle.

The rule was that nothing may change, cycle for cycle. Every bus access is still
one cycle, every dummy read and write still happens (AGENTS.md rule 1), and the
core still names no machine (rule 2).

**What was reached.** Two changes were kept. In the browser's AOT build the BBC
Micro at its prompt got about 17 % faster. Natively, in thread CPU time, the core
alone on Dormann's test got about 13 % faster and the BBC Micro about 6 %; the NES
stayed within noise natively and in the browser, and the KIM-1, measured natively
only, within noise. Ten times real time for the NES was
not reached: SNOW still runs at about 2.5 times in the main thread's CPU time. The
reason is below: in the NES's AOT build the core is about a tenth of the time, so
even a free core would give about 12 %.

## How the work was measured

The virtual machine was shared all morning. The one-minute load average on its 8
cores went from 1 to 78, and Chrome once failed to start in three minutes. Wall
clock figures were not usable for a 5 % decision, so I measured CPU time instead,
in two ways:

- **Natively**, `bench/thread-time`, which times each run in the thread's own CPU
  time (`clock_gettime` with `CLOCK_THREAD_CPUTIME_ID`) and prints nanoseconds a
  cycle. It has five workloads: `core` (Dormann's functional test on a flat bus),
  `bbc` (the BBC Micro at its prompt), `kim` (the KIM-1 monitor after RS), and
  `ntsc` and `pal` (the NES running SNOW). Each comparison built it into the
  baseline export and into the work, and ran `dotnet <build>/Dbhq.Machines.ThreadTime.dll
  <workload> 5` for each build in turn, four processes each (eight where said),
  taking the median of each build's medians. The baseline was `18cc4ec`, exported
  with `git archive`. It was a scratch program while the work was done and was
  committed afterwards, in the review; the committed code is the code that gave
  every figure here, with comments added and nothing else changed.
- **In the browser**, a new option in both browser benches: `THREAD_TIME=<n>`
  makes n more timed runs after the page's own, each read with the DevTools
  protocol's `ThreadTime` metric, the CPU time of the page's main thread. Under
  load the wall clock gave 1.0 times real time where the thread time gave 2.4, on
  the same build in the same minute. `PROFILE=<n>` prints the functions with the
  most self time. Both are in `bench/nes-speed/run-in-browser.mjs` and
  `bench/bbc-micro-speed/run-in-browser.mjs`, and their READMEs say how to use
  them. Task 6b's profile was taken with an uncommitted script; this one is
  committed, so its figures can be taken again.

Thread time is steadier than the wall clock, but not steady: a launch's median
still moved by 10 to 20 % from one launch to the next, and between sessions. A
difference under about 10 % needed six or more alternated launches to see.

## How behaviour was kept

At every kept commit:

- `dotnet test tests/Dbhq.Cpu6502.Tests -c Release` (Harte's SingleStepTests with
  their cycle-by-cycle bus traces, Dormann's tests, nestest, and the transistor
  model's interrupt cases): 1,597 passed;
- `dotnet test` for the KIM-1 (39), the BBC Micro (980) and the NES (1,503): all
  passed;
- the NES differential (`bench/nes-speed/differential`, 127 ROMs in both regions,
  254 lines) against the baseline's file: identical;
- the BBC Micro's fingerprint (`dotnet run -c Release --project
  bench/bbc-micro-speed/native -- --fingerprint`, a scripted session hashed every
  instruction): identical;
- a new KIM-1 differential, `bench/kim-1-differential`: RS, the "try it" program
  typed and run, ST, the SST switch stepping it (an NMI on every opcode fetch
  outside the monitor), and ST held, hashed after every instruction. Built and
  run on the baseline export too, it prints the same six lines. With a scratch
  fault, the NMI edge never latched, three of its six lines change, from the
  first ST on.

After the review, one more check, on the work as finished. The poll change rests
on a rule that no test names for an instruction not yet written: one that changes
the I flag after its last bus access must call `FreezePoll` first. The review wrote
a randomised differential for it, now `bench/interrupt-differential`: random memory
run as code on every CPU variant, the IRQ and NMI lines toggled at random inside
bus accesses and between steps, and resets, with every bus access and every step's
state hashed. Its README says how to run it against a `git archive` export. Run on
6 October at 13:03 UTC (load 16.7 to 13.0) as `dotnet run -c Release --project
bench/interrupt-differential -- 3000000` (60 configurations of 3 million steps, 180
million in all), on `18cc4ec` and on `4533f1b`: the two outputs, 61 lines each, are
identical, and the same as the review's own run. With SEI's `FreezePoll` taken out
of a scratch copy of the core, 18 of the 60 configuration lines and the total
change, on all five variants, so the check can fail. (The committed file differs
from the review's in one place: a cast in the step hash goes through `uint` first,
because warnings are errors here. The step count it casts is never negative, so no
hash changes.)

### The buses left on the wrapper

The abstract class is for speed, and every bus whose speed is measured now derives
from it: the three machines', `FlatBus` in the test support, the core speed
benches' two, and, after the review, the bare-CPU bus of the BBC Micro bench's
`--profile` mode, which until then compared a faster machine with a bare CPU still
on the wrapper. The `IBus` implementers that stay on the wrapper are all in tests
or tools, where speed is not measured: `ScheduledBus` (the transistor model's
interrupt runs), `ReferenceBbcBus` and the two `Recorder`s (the BBC Micro's
equivalence tests), `RecordingBus` in `tools/Dbhq.Cpu6502.ChipTrace`, and the
interrupt differential's `RandBus`, which has to build on commits from before
`Bus` existed. Keeping them on `IBus` also keeps the wrapper itself under test.

## Where the time goes

**In the browser, on the NES**, the profile of the baseline (`PROFILE=45 node
run-in-browser.mjs <baseline> 1 1790000 5000000 5 0`, 07:06 UTC, load 14, the
whole page with the boot): `Ppu.Tick` 24.4 %, `NesBus.Cycle` 11.8 %, `Apu.Tick`
11.3 %, the runtime's own code about 9 %, then the core: `Cpu.Read` 5.7 %,
`ExecuteOfficial` 2.8 %, `Write` 1.0 %, `Step` 0.8 %. The core is about a tenth of
the time. So the ceiling for this task on the NES is small: a core that cost
nothing would make the NES about 12 % faster.

**In the browser, on the BBC Micro**, the core is most of the time. The baseline,
profiled over 20 driver-timed runs (`THREAD_TIME=20 PROFILE=12`, about 09:00 UTC):
`Cpu.Read` 22.3 %, `Step` 13.7 %, `BbcMachine.Run` 10.1 %, `ExecuteOfficial` 5.5 %,
`Branch` 3.9 %, `Write` 2.1 %. The bus's own `Read` hardly shows.

The browser's profile has to be read with care. V8 inlines WebAssembly functions
into each other, so a function's self time includes what was inlined into it:
`BbcMachine.Run` is a three-line loop, and the 10 % is the code V8 placed in it.

**Natively**, CoreCLR's tiered compilation with dynamic PGO already turns the call
through `IBus` into a guarded direct call and inlines the small helpers. A
`dotnet-trace` profile of SNOW showed the NES bus, the PPU and the sound unit
ahead of the core, as in the browser.

## What was tried, and what was kept

### Kept: the interrupt poll once an instruction (`c42343d`)

`EndCycle` ran at the end of every cycle and did three things: count the cycle,
latch an NMI edge, and refresh the poll snapshot (the IRQ line with the I flag,
and the latched NMI) unless a branch had frozen it. Only the last cycle's snapshot
is ever used, by `Poll` at the end of the instruction. So the snapshot is now
taken in `Poll`, once. It gives the same values because nothing changes them
between an instruction's last access and its `Poll`: a bus sets the lines only
inside an access, and the NMI latch changes after the last access only in
`EnterHandler`, whose poll is suppressed. The I flag is the exception: CLI, SEI
and PLP change it after their last access. Those three now freeze the snapshot
before they change it, as a taken branch on its page already did. `EndCycle` now
stores nothing in a cycle where the NMI line has not moved.

Each of the three freezes was taken out in turn and the interrupt tests run
(`--filter FullyQualifiedName~Interrupt`): without CLI's, many transistor model
cases fail; without PLP's, 10 fail; without SEI's, 2 fail. So the tests pin all
three.

Natively (`dotnet <build>/Dbhq.Machines.ThreadTime.dll <workload> 5`, four
alternated processes each, about 08:30 UTC, load about 7): the core 11.44 to 10.76
ns a cycle, the BBC Micro 17.53 to 16.16, the KIM-1 16.34 to 15.84, the NES 159.6 to
156.9 (noise).
In the browser the BBC Micro did not move: 20.68 against 20.44 MHz, thread-time
medians of 60 runs each, alternated (09:00 UTC, load 8.5 to 11.8). It is kept for
the native gain and because it does less.

### Kept: the bus as an abstract class (`3e52229`)

The CPU called the bus through `IBus`, an interface. In Mono's AOT WebAssembly an
interface call is two indirect calls: one to a dispatch thunk that finds the
method, then the method. A virtual call is one. So the core now has an abstract
class, `Bus`, that implements `IBus`; the CPU holds a `Bus`, and a bus that is
only an `IBus` is wrapped in one, so every existing bus works as before. The three
machines' buses, `FlatBus` in the test support and the two speed benches' buses
now derive from `Bus`. The constructor, `new Cpu(IBus, CpuVariant)`, did not
change, and nothing else in the public API did.

In the browser this step was measured on its own only on the BBC Micro: its
thread-time median rose by about 10 % against the baseline in two of three
alternated sessions (09:01 and 09:03 UTC, load 3 to 9) and was level in the third
(08:18 UTC, load 21 to 24). The figures below are for the two kept changes
together.

## The figures, before and after

The baseline is `18cc4ec`; "after" is `3e52229`, the core with the two kept changes
and nothing else. Every pair was run alternately, baseline then after, in
the same minutes. The browser builds were published with
`dotnet publish <wasm project> -c Release -p:RunAOTCompilation=true
-p:WasmNativeStrip=false -o <folder>`, the baseline's from a `git archive` export.
Chrome 153.0.8010.47 headless, .NET SDK 10.0.400, the 8-core virtual machine.

**In the browser, AOT**, main-thread CPU time (`THREAD_TIME=8 node
run-in-browser.mjs <folder> 1 1790000 5000000 5 <region>` for the NES,
`THREAD_TIME=10 node run-in-browser.mjs <folder> 1 2000000 6000000 1` for the BBC
Micro), medians:

| Machine | Before | After | When (UTC), load | Runs each |
| --- | --- | --- | --- | --- |
| BBC Micro | 21.99 MHz | 25.72 MHz | 12:21 to 12:23, 50.6 to 31.5 | 50, 5 launches |
| NES NTSC | 2.21 times | 2.50 times | 12:15 to 12:17, 8.9 to 1.9 | 32, 4 launches |
| NES PAL | 2.50 times | 2.50 times | 12:18 to 12:21, 1.9 to 48.6 | 32, 4 launches |
| NES NTSC | 2.48 times | 2.46 times | 12:23 to 12:25, 27.6 to 6.6 | 24, 3 launches |
| NES PAL | 2.50 times | 2.47 times | 12:25 to 12:27, 6.6 to 3.5 | 24, 3 launches |

Every launch of the BBC Micro after the change had a higher median than every
launch before it (23.47 to 26.56 MHz against 20.56 to 23.80). The NES moved by
less than its noise: the one higher NTSC figure did not repeat. The page's own
wall-clock runs in the same launches gave the NES 1.1 to 1.5 times real time,
because the load took the processor from the page; a quiet machine's wall clock
is about its thread time.

**Natively**, thread CPU time: `dotnet <build>/Dbhq.Machines.ThreadTime.dll
<workload> 5` from `bench/thread-time`, built into the baseline export and into the
work, the builds run in turn, the median of four processes' medians each (eight for
the KIM-1), 12:10 to 12:15 UTC, load 10 to 17 with an AOT publish running:

| Workload | Before | After |
| --- | --- | --- |
| `core`: the core alone, Dormann's functional test | 11.91 ns a cycle | 10.40 |
| `bbc`: BBC Micro at the prompt | 17.98 | 16.93 |
| `kim`: KIM-1 monitor after RS | 18.57 | 18.42 |
| `ntsc`: NES SNOW, NTSC | 169.2 | 165.9 |
| `pal`: NES SNOW, PAL | 151.8 | 140.7 |

That is about 13 % for the core alone and about 6 % for the BBC Micro. The KIM-1
and the NES moved by less than their noise; the PAL figure is inside the spread
the same workload showed between sessions.

For the record, the committed native benches were also run, wall clock, at a load
of 8.6 to 13.7 (12:27 to 12:29 UTC). `dotnet <build>/Dbhq.Machines.Nes.SpeedNative.dll
5 1790000 <region>`, three runs of five each, alternated: NTSC median 1.92 before
and 2.07 after, PAL 2.15 and 2.11 times real time. `./alternate.sh native 3
<before> <after>` in `bench/bbc-micro-speed`: the BBC Micro's median 22.42 MHz
before and 27.84 after, best 56.53 and 69.25, with the slowest and fastest runs of
each build three to five times apart. These are loaded readings, not a measure of the
gain: at that load the wall clock is the reading this entry says cannot settle a 5 %
question, and the thread-time figures above are the ones to use.

### Tried and dropped: a CPU made for each bus, `Cpu<TBus>`

The obvious large change: make the CPU generic over a struct bus, so the compiler
makes a copy of the core for each machine and can call, or inline, that machine's
bus directly. I built it in full (a `CpuBase` with the registers and the lines, a
`Cpu<TBus>`, and `Cpu` kept as `Cpu<AnyBus>` so tests and tools did not change)
and measured it on all three machines.

- Natively it gained nothing (SNOW 162.8 against 173.9 ns a cycle, `ntsc 5` of
  `bench/thread-time`, three alternated processes each, 07:10 UTC, load 13 to 17):
  dynamic PGO had already done the same.
- In the first AOT build it made the NES less than half as fast (0.70 against 1.61
  times real time). Mono's AOT compiler had made only the constructor of
  `Cpu<NesBus.CpuPort>`: `Step` was a virtual method of the base class, C# names
  the base method in the call, and the compiler did not follow it. The rest ran as
  shared generic code, some of it in the interpreter (`do_jit_call` and
  `mono_interp_exec_method` in the profile).
- With `Step` and `Reset` made ordinary methods, every method was compiled for the
  machine's bus, into a separate `aot-instances` module, and the NES was as fast as
  before: 2.21 against 2.24 times real time, thread-time medians (07:27 UTC, load
  11 to 19). With `AggressiveInlining` on the helpers, 2.76 against 2.71. The BBC
  Micro, 23.9 against 25.2 MHz. With `-p:WasmDedup=false` the methods moved into
  the machine's own module, and the BBC Micro gave 25.0 against 25.3.
- The disassembled WebAssembly (`wasm-dis` from the Emscripten pack) shows why:
  in that module every call from one method of the instance to another is an
  indirect call through a table, and the call to the bus is still a call through
  the vtable.

So it changed the public API of every machine and bought nothing in either
runtime. It was not kept.

### Tried and dropped: `AggressiveInlining` on the core's helpers

`Read`, `Write`, `EndCycle`, the flag helpers, the addressing modes, the stack
and the logic operations. In the browser it moved nothing (the NES 2.52 against
2.60 times real time, 07:35 UTC, load 5 to 10); natively it made the core slower,
12.77 against 10.78 ns a cycle (`core 5` of `bench/thread-time`, three alternated
processes each, 07:45 UTC, load 7 to 10), because the opcode switch grew.

### Tried and dropped: flags set in one write

`NZ`, `Compare`, the shifts, `BIT` and binary `ADC` set N, Z, C and V in one
write of P with no branch, and `Step` tested its three rare states with one test.
Natively within noise (`bench/thread-time`, four alternated processes each, about
08:50 UTC: the core 10.36 against 10.54 ns, the BBC Micro 16.88 against 15.97, the
KIM-1 16.81 against 16.86); in the browser the BBC Micro 20.79
against 20.44 MHz. Not measurable, so not kept.

## What was not tried, and why

- **The NES's per-cycle path beyond the core** (`NesBus.Cycle`, `Ppu.Tick`,
  `Apu.Tick`): together about half the browser's time on the NES, and task 6b's
  work. Making the core free gives at most about 12 %; ten times needs the NES's
  own cycle and its PPU to shrink to a third, which is a design question (a PPU
  that runs ahead and catches up), and Dan turned that down on 5 October.
- **Other runtimes**: NativeAOT for WebAssembly, or Mono's AOT options beyond
  `WasmDedup`. Not tried; they change the build of every page.
- **A table of handlers in place of the opcode switch**: the switch already
  compiles to a jump table in both runtimes, and the profile does not point at
  dispatch.

## What was assumed, and what to check

- That thread CPU time is a fair gauge. It removes the time the page was not
  running, but not the slowdown from sharing a core's caches with other work, so
  figures from different sessions are not comparable, only figures alternated in
  the same session.
- That `bench/thread-time` measures what the other native benches do. The NES and
  BBC Micro workloads are theirs (SNOW after a five million cycle boot, the BBC
  Micro at the prompt after six million), but the KIM-1 and the core-alone
  workloads are its own.
