---
title: "The NES's speed in a browser, with the bus and the PPU"
date: 2026-10-05
summary: "The NES as built so far, with its bus and a PPU that draws the background and sprites, runs at about two times real time in a browser compiled ahead of time, on a machine that was busy, and about a fifth of the BBC Micro's rate on the same machine at the same time. The plan's rule says under ten times is a stop before the sound is built. The loaded figures alone do not make it a stop, but the estimate for a quiet machine is about three times, so the stop is almost certain, and the decision is Dan's."
order: 30
---

# 5 October 2026: the NES's speed in a browser

Task 6 of the NES plan measures how fast the machine runs in a browser before the
sound unit and the mappers exist, because they will only make it slower. The
plan's rule, from the BBC Micro's: at least 25 times real time, carry on; 10 to
25 times, carry on and record it; under 10 times, stop and tell Dan before
building the sound. The cheaper path to look at first would be the PPU batching
idle dots with rendering off, or a faster pixel path.

**The measurement is not clean, and I say so first.** The virtual machine was
shared with other work for the whole session. The one-minute load average on
its 8 cores was between 3 and 90 while I worked, and it jumped by a factor of ten in
seconds when another agent started a build. I tried three times to catch a
quiet window (load under 3.5 at the start of a set) and each set was caught by a
spike within a minute. So no figure here is a quiet-machine figure. What I can
give is the figures with their load, and the BBC Micro's bench run in the same
session as a relative baseline.

## What was built

- **`src/Dbhq.Machines.Nes.Wasm/`**, the NES as a plain .NET WebAssembly app in
  the shape of the BBC Micro's. A first host class, `NesHost`, with `Load(rom,
  region, sampleRate)` (region 0 NTSC, 1 PAL), `Run(cycles)`, `Cycles()`,
  `Frames()` and `CpuHz()`. `CpuHz()` returns `Region.CpuHz`, so the page and the
  bench never type a rate. It holds no ROM. Task 14 replaces `Load`'s signature.
  It is in `6502.slnx`.
- **`bench/nes-speed/`**, the page, the script that serves it and runs headless
  Chrome, a `native/` console program in the solution, and a README.
- **The ROM is not committed.** `run-in-browser.mjs` reads the pinned fork's
  commit and the ROM's hash from `Pins.cs`, downloads the ROM from
  `raw.githubusercontent.com/dbhq-uk/nes-test-roms/<commit>/` into `.testdata/`,
  checks its SHA-256 and prints it on every run. `site/tests/mirrors.test.mjs`
  scans `bench/` and passes.
- **The Ppu split.** Task 5's review said `Ppu.cs` was 1,079 lines and should be
  split. The background half (the shifters and the tile fetch) is now
  `PpuBackground.cs` and the sprite half (evaluation, fetches, the line buffer)
  is `PpuSprites.cs`, as `partial class Ppu`. It is a pure move: the only line
  that changed in `Ppu.cs` that is not a deletion is `class` to `partial class`,
  checked by comparing the removed lines with the added files line by line. All
  440 NES tests pass in Release (task 5's report said 439; the count is the run's
  own output, not a figure I typed), `dotnet build -c Release` has no warnings,
  and the nestest tests are among them.

## The ROM

The workload has to turn rendering on, keep it on, draw something different
every frame, and run on NROM, which is the only board built so far. I took
**SNOW** by Repulse, `other/snow.nes` in the fork, a demo whose header says
mapper 0 (bytes 6 and 7 are both zero) with 32 KB of program and 8 KB of
character ROM. Its SHA-256 is `7db551e8...7b7210` (full value in `Pins.cs`,
under `NesTestRomHashes`, keyed `other/snow.nes`), taken from a fresh download
from raw.githubusercontent.com at the pinned commit on 5 October.

The native program checks it is the workload: after the boot it prints
`rendering yes` and `picture changes yes`, from the PPU's mask and from a hash of
the pixel buffer across one frame. Both printed `yes` in both regions in every
run below. Without that check a ROM that had stopped drawing would time the
PPU's idle path, which is the cheap one.

I did not compare the other ROMs in `other/` for how much they draw. The brief
said to choose by what draws most; I chose one that draws every frame and is on
NROM and stopped there, because the result is nowhere near a boundary, so a
different ROM would not change the decision. A more expensive ROM (many sprites
a line) would only be slower.

## The workload

Power on, run 5 million cycles (the boot, timed on its own, which includes the
runtime warming up), then time `Run(1_790_000)` five times. The multiple of real
time is the cycles a second divided by `Region.CpuHz` for the region: 1,789,773
for NTSC and 1,662,607 for PAL, as the machine reported them. Each timed run is
about 60 NTSC frames or 54 PAL frames.

## The commands

From the repository root. The AOT and interpreter publishes were run one at a
time, deleting `obj/Release` between them.

```
dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release -o bench/nes-speed/publish/interpreter
dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release -p:RunAOTCompilation=true -o bench/nes-speed/publish/aot
dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release -p:RunAOTCompilation=true -o bench/bbc-micro-speed/publish/aot
cd bench/nes-speed
node run-in-browser.mjs publish/aot 3 1790000 5000000 5 0          # NTSC, three launches
node run-in-browser.mjs publish/aot 3 1790000 5000000 5 1          # PAL
node run-in-browser.mjs publish/interpreter 1 1790000 5000000 5 0
node run-in-browser.mjs publish/interpreter 1 1790000 5000000 5 1
(cd ../bbc-micro-speed && node run-in-browser.mjs publish/aot 3)  # the baseline
dotnet run -c Release --no-build --project native -- 5 1790000 ntsc
dotnet run -c Release --no-build --project native -- 5 1790000 pal
```

Chrome 153.0.8010.47 headless, .NET SDK 10.0.400 with `wasm-tools`, on the same
8 core virtual machine as the earlier checks.

## Set A: the best set, started at a load of about 5.6

Started 15:37:59 UTC. `uptime` before: load average 5.58, 9.46, 19.90. After the
NES NTSC runs (15:38:18) 5.98; after the BBC runs (15:38:31) 5.84; after the NES
PAL runs (15:38:55) 7.44; after the native runs (15:39:06) 8.44. The three
browser runs and the native runs took 70 seconds, so the load did not move much
within the set, though the first minute's average of 5.6 was already well above a
quiet machine.

Each row is the timed runs, 15 for a browser build (three launches of five) and 5
for native, in millions of cycles a second, with the multiple of the region's real
time in brackets.

| Build | Best | Median | Slowest |
| --- | --- | --- | --- |
| NES AOT, NTSC | 4.564 (2.55 times) | 3.474 (1.94 times) | 2.850 (1.59 times) |
| NES AOT, PAL | 3.639 (2.19 times) | 2.835 (1.71 times) | 2.045 (1.23 times) |
| NES native, NTSC | 7.627 (4.26 times) | 6.044 (3.38 times) | 5.868 (3.28 times) |
| NES native, PAL | 5.424 (3.26 times) | 4.673 (2.81 times) | 3.735 (2.25 times) |
| BBC Micro AOT (its standard bench) | 20.790 | 18.349 (9.17 times 2 MHz) | 12.136 |

The NES lines as printed, AOT NTSC:

```
launch 1 boot cycles=5000002 frames=167 ms=1851.800 cycles_per_second=2700077 mhz=2.700 times_real=1.51
launch 1 timed 1 cycles=1790002 frames=61 ms=606.600 cycles_per_second=2950877 mhz=2.951 times_real=1.65
launch 1 timed 2 cycles=1790001 frames=60 ms=572.600 cycles_per_second=3126093 mhz=3.126 times_real=1.75
launch 1 timed 3 cycles=1790000 frames=60 ms=455.800 cycles_per_second=3927161 mhz=3.927 times_real=2.19
launch 1 timed 4 cycles=1790002 frames=60 ms=508.800 cycles_per_second=3518086 mhz=3.518 times_real=1.97
launch 1 timed 5 cycles=1790001 frames=60 ms=567.100 cycles_per_second=3156412 mhz=3.156 times_real=1.76
launch 2 boot cycles=5000002 frames=167 ms=1299.000 cycles_per_second=3849116 mhz=3.849 times_real=2.15
launch 2 timed 1 cycles=1790002 frames=61 ms=516.100 cycles_per_second=3468324 mhz=3.468 times_real=1.94
launch 2 timed 2 cycles=1790001 frames=60 ms=535.600 cycles_per_second=3342048 mhz=3.342 times_real=1.87
launch 2 timed 3 cycles=1790000 frames=60 ms=516.600 cycles_per_second=3464963 mhz=3.465 times_real=1.94
launch 2 timed 4 cycles=1790002 frames=60 ms=628.000 cycles_per_second=2850322 mhz=2.850 times_real=1.59
launch 2 timed 5 cycles=1790001 frames=60 ms=515.300 cycles_per_second=3473707 mhz=3.474 times_real=1.94
launch 3 boot cycles=5000002 frames=167 ms=1184.900 cycles_per_second=4219767 mhz=4.220 times_real=2.36
launch 3 timed 1 cycles=1790002 frames=61 ms=469.500 cycles_per_second=3812571 mhz=3.813 times_real=2.13
launch 3 timed 2 cycles=1790001 frames=60 ms=392.200 cycles_per_second=4564001 mhz=4.564 times_real=2.55
launch 3 timed 3 cycles=1790000 frames=60 ms=425.500 cycles_per_second=4206816 mhz=4.207 times_real=2.35
launch 3 timed 4 cycles=1790002 frames=60 ms=468.700 cycles_per_second=3819078 mhz=3.819 times_real=2.13
launch 3 timed 5 cycles=1790001 frames=60 ms=406.800 cycles_per_second=4400199 mhz=4.400 times_real=2.46
```

AOT PAL:

```
launch 1 boot cycles=5000002 frames=150 ms=1900.300 cycles_per_second=2631165 mhz=2.631 times_real=1.58
launch 1 timed 1 cycles=1790001 frames=54 ms=715.300 cycles_per_second=2502448 mhz=2.502 times_real=1.51
launch 1 timed 2 cycles=1790002 frames=54 ms=583.700 cycles_per_second=3066647 mhz=3.067 times_real=1.84
launch 1 timed 3 cycles=1790000 frames=53 ms=592.700 cycles_per_second=3020078 mhz=3.020 times_real=1.82
launch 1 timed 4 cycles=1790001 frames=54 ms=609.300 cycles_per_second=2937799 mhz=2.938 times_real=1.77
launch 1 timed 5 cycles=1790002 frames=54 ms=631.300 cycles_per_second=2835422 mhz=2.835 times_real=1.71
launch 2 boot cycles=5000002 frames=150 ms=1906.900 cycles_per_second=2622058 mhz=2.622 times_real=1.58
launch 2 timed 1 cycles=1790001 frames=54 ms=704.200 cycles_per_second=2541893 mhz=2.542 times_real=1.53
launch 2 timed 2 cycles=1790002 frames=54 ms=875.400 cycles_per_second=2044782 mhz=2.045 times_real=1.23
launch 2 timed 3 cycles=1790000 frames=53 ms=639.700 cycles_per_second=2798187 mhz=2.798 times_real=1.68
launch 2 timed 4 cycles=1790001 frames=54 ms=685.700 cycles_per_second=2610473 mhz=2.610 times_real=1.57
launch 2 timed 5 cycles=1790002 frames=54 ms=694.000 cycles_per_second=2579254 mhz=2.579 times_real=1.55
launch 3 boot cycles=5000002 frames=150 ms=1531.300 cycles_per_second=3265201 mhz=3.265 times_real=1.96
launch 3 timed 1 cycles=1790001 frames=54 ms=580.400 cycles_per_second=3084082 mhz=3.084 times_real=1.85
launch 3 timed 2 cycles=1790002 frames=54 ms=534.000 cycles_per_second=3352064 mhz=3.352 times_real=2.02
launch 3 timed 3 cycles=1790000 frames=53 ms=491.900 cycles_per_second=3638951 mhz=3.639 times_real=2.19
launch 3 timed 4 cycles=1790001 frames=54 ms=669.700 cycles_per_second=2672840 mhz=2.673 times_real=1.61
launch 3 timed 5 cycles=1790002 frames=54 ms=587.100 cycles_per_second=3048888 mhz=3.049 times_real=1.83
```

## Set B: a worse set, load rising from 3.4 to 36

Started 16:01:15 UTC at a load of 3.44 (3.44, 17.08, 19.33), when the wait
script saw a quiet minute. Another agent's work started within a minute: 30.95 at
16:02:13, 35.31 at 16:03:34 and 35.81 at 16:03:55. The NES AOT NTSC runs were
mostly before the spike and are the slowest of the day regardless, which shows
how much a spike that is not yet in the one-minute average costs. Medians in
millions of cycles a second:

| Build | Best | Median | Slowest |
| --- | --- | --- | --- |
| NES AOT, NTSC | 2.123 (1.19 times) | 1.644 (0.92 times) | 0.967 (0.54 times) |
| NES AOT, PAL | 2.248 (1.35 times) | 1.735 (1.04 times) | 1.202 (0.72 times) |
| NES native, NTSC | 4.102 (2.29 times) | 3.599 (2.01 times) | 2.071 (1.16 times) |
| NES native, PAL | 5.369 (3.23 times) | 4.644 (2.79 times) | 3.682 (2.21 times) |
| BBC Micro AOT | 16.556 | 10.638 (5.32 times 2 MHz) | 9.465 |

Two more waits for a quiet window (a poll every five seconds for ten minutes,
for a one-minute load under 3.5) found none, the load being 12 to 88 at the end of
each.

## The interpreter

Single launch each, five timed runs, 15:40:40 UTC at a load of 22.10 (22.10,
13.02, 19.45) for NTSC and 15:41:39 at 19.44 for PAL, 9.53 after:

```
launch 1 region 0
launch 1 boot cycles=5000002 frames=167 ms=20490.300 cycles_per_second=244018 mhz=0.244 times_real=0.14
launch 1 timed 1 cycles=1790002 frames=61 ms=7172.000 cycles_per_second=249582 mhz=0.250 times_real=0.14
launch 1 timed 2 cycles=1790001 frames=60 ms=6443.700 cycles_per_second=277791 mhz=0.278 times_real=0.16
launch 1 timed 3 cycles=1790000 frames=60 ms=5047.200 cycles_per_second=354652 mhz=0.355 times_real=0.20
launch 1 timed 4 cycles=1790002 frames=60 ms=5051.700 cycles_per_second=354337 mhz=0.354 times_real=0.20
launch 1 timed 5 cycles=1790001 frames=60 ms=5439.800 cycles_per_second=329056 mhz=0.329 times_real=0.18
launch 1 region 1
launch 1 boot cycles=5000002 frames=150 ms=29721.300 cycles_per_second=168230 mhz=0.168 times_real=0.10
launch 1 timed 1 cycles=1790001 frames=54 ms=6597.100 cycles_per_second=271331 mhz=0.271 times_real=0.16
launch 1 timed 2 cycles=1790002 frames=54 ms=4976.700 cycles_per_second=359676 mhz=0.360 times_real=0.22
launch 1 timed 3 cycles=1790000 frames=53 ms=4382.800 cycles_per_second=408415 mhz=0.408 times_real=0.25
launch 1 timed 4 cycles=1790001 frames=54 ms=4511.500 cycles_per_second=396764 mhz=0.397 times_real=0.24
launch 1 timed 5 cycles=1790002 frames=54 ms=4077.000 cycles_per_second=439049 mhz=0.439 times_real=0.26
```

The interpreter median is 0.329 MHz (0.18 times real time) for NTSC and 0.397
MHz (0.24 times) for PAL; the best is 0.355 (0.20 times) and 0.439 (0.26 times).
The interpreter would run the NES at about a fifth of real time, which is not
usable, and it was never going to be: the BBC Micro's was 0.64 times a 2 MHz
machine at the same stage. This figure was taken under a load of about 20, so it
is the least reliable of the lot, but its distance from 1 is large.

## Against the BBC Micro, in the same session

The BBC's bench run straight after the NES's, in the same set, measures the
machine's speed at that moment. The ratio of the NES's cycles a second to the
BBC's cancels most of the load, because both ran in the same minute:

| Set | NES AOT NTSC over BBC | NES AOT PAL over BBC |
| --- | --- | --- |
| A | 0.189 | 0.155 |
| B | 0.155 | 0.163 |

So the NES runs at about a sixth to a fifth of the rate, in simulated cycles a
second, of the BBC Micro at its prompt. That is the expected direction: the BBC
at the prompt does almost nothing with its video chips, and the NES draws three
PPU dots every CPU cycle, each with its fetches, the pixel mixing and the
sprite work. Per CPU cycle the NES does the work of about three PPU dots on top
of the CPU and the bus.

**The estimate for a quiet machine.** The BBC's last quiet measurement is the
median of 13.62 times 2 MHz on 4 October (the final pass entry), which is 27.24
million cycles a second. Multiplied by the ratios above, and by nothing else,
the NES on a quiet machine would be about:

- NTSC: 0.189 or 0.155 of 27.24 MHz is 5.2 or 4.2 MHz, **about 2.4 to 2.9 times
  real time**.
- PAL: 0.155 or 0.163 of 27.24 MHz is 4.2 or 4.4 MHz, **about 2.5 to 2.7 times
  real time**.

The BBC in set A made 18.35 MHz against 27.24 on its quiet day, so set A ran at
about two thirds of a quiet machine's speed, at a load of 6 to 8. The estimate
above is the NES's ratio applied to the quiet figure. It is an estimate, not a
measurement.

## What this means for the rule

- **Measured, under load:** the AOT median is 1.94 times real time (NTSC) and 1.71
  times (PAL) in the better set, and 0.92 and 1.04 in the worse.
- **Estimated, quiet:** about 2.4 to 2.9 times (NTSC) and 2.5 to 2.7 times (PAL).
- **The rule:** under 10 times on a quiet machine is a stop. I could not get a
  quiet machine, and the brief says not to declare a stop on loaded figures
  alone, so I do not declare one. But the estimate is about a quarter of the
  line. The machine would have to be three to four times faster than the
  BBC-ratio estimate says to reach 10 times. I would call the stop almost
  certain. The decision is Dan's: does the machine get faster before the sound
  and the mappers are built?

The machine runs this ROM at about twice real time here, so a page could play it
on this hardware today. But the figure is a ceiling: the sound and the mappers
will lower it, and a slower laptop or phone has less room.

## What I would try, not done

Nothing was changed for speed; these are for the decision.

1. **The PPU's idle dots.** Task 4's ROM-off path already skips work; the fix
   named in the plan is to batch dots with rendering off. This ROM has rendering
   on, so it would not help the figure here, only a boot or a blank screen.
2. **A dot is about 100 nanoseconds in the browser.** At 3.5 MHz CPU, 10.4
   million dots a second, so each dot, including its share of the CPU and the
   bus, costs about 95 ns. The structure is a call to `Ppu.Tick` per dot, with a
   branch tree inside it, `RenderDot`, `DrawPixel`, `Evaluate`, `FetchBackground`
   and `FetchSprite`, plus interface calls to the mapper on every fetch. Three
   candidates for a measurement: render a whole line in one call (the PPU runs
   ahead of the CPU between register accesses, and catches up when one happens),
   cheapen the per-cycle bus work (the `_apu.Tick` and `_mapper.CpuCycle`
   interface calls, and the division in the dot accumulator), and make the
   background fetch table-driven so each dot is a lookup. The first is the
   largest and the riskiest, since the timing tests pin the dots.
3. **Measure before choosing.** A profile of the native run by the share of time
   in each of those would show which matters. I did not run one: it would be
   guessing at a figure taken under load.

## What was assumed, and what to check

- That the BBC Micro's bench is a fair load gauge for the NES's: both single
  threaded wasm on the same machine at the same minute, but it does very
  different work, so an effect like cache pressure would not scale the same.
- That 13.62 times 2 MHz, from 4 October, is what the BBC bench gives on a quiet
  machine today. It was taken at a quiet hour. The BBC's code has not changed
  since in this branch except for one comment.
- That one ROM is representative. SNOW draws every frame and uses the
  background; whether it uses many sprites a line I did not check.
- A set on a quiet machine remains to do, and is worth doing before the
  decision: `cd bench/nes-speed && node run-in-browser.mjs publish/aot 3 1790000
  5000000 5 0`, the same for region 1, and the BBC bench beside it, at a
  one-minute load under 1.

## Again, after the sound (task 9)

Task 9 added the DMC, its DMA, the mixer's tables and the sample buffer, and ran
this check again on 5 October 2026 against the commit before it (exported with
`git archive` and built the same way), alternating the two. At a load of 2.6 to
4.2, AOT medians went from 1.81 to 1.67 times real time on NTSC and from 2.19 to
1.96 on PAL; natively, at 3.6 to 4.8, from 3.27 to 3.05 on NTSC. That is 7 to
10 % for the sound. The table, the commands and how the cost divides are in
[Planning the NES](2026-10-05-the-nes-plan.md), under task 9.
