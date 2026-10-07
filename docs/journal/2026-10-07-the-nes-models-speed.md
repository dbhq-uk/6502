---
title: "The NES chip counters' speed, measured at last with the machine's own method"
date: 2026-10-07
summary: "The NES models added a counter to the bus: one call whenever the CPU touches the PPU, the sound unit or the I/O registers. The claim was that it costs no more than two per cent of the machine's speed, and earlier runs on a busy host could not show it. This time both builds were compiled ahead of time and run alternately, eight launches each in both regions, measured in the main thread's CPU time. By the rule fixed beforehand it passes: the median ratio was above one in both regions. But the launches differ by a fifth, so the data rule out a slowdown beyond about three and a half per cent, not two. The host was never empty, though its load average was low. The differential check shows the counters change no behaviour."
order: 36
---

# 7 October 2026: the NES chip counters' speed

The NES models branch gave the bus a counter (`ChipAccesses`, four calls to
`_accesses.Note(...)` in `NesBus.cs`, one for each read or write of the PPU,
sound and I/O registers; `Peek` is untouched). The claim in the plan was that
it costs no more than 2 per cent of the machine's speed. Two earlier tries did
not settle that ([the models entry](2026-10-05-the-nes-models.md), "The speed",
and its final pass): the native half could not resolve 2 per cent, and the
browser half compiled ahead of time read NTSC 0.942 and PAL 1.010 on a busy
host. Today the host looked quiet, so this is the run
[`bench/nes-speed/README.md`](../../bench/nes-speed/README.md) asks for.

## The design, fixed before the first run

- **Baseline** is `origin/main` (`5e48505`) exactly, with no counters. **After**
  is the branch at `95d40f7`.
- Both built with `dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release
  -p:RunAOTCompilation=true -o <folder>`, each from its own tree (the baseline
  from `git archive origin/main | tar -x -C /tmp/nes-speed-base`).
- `THREAD_TIME=8 node run-in-browser.mjs <folder> 1 1790000 5000000 5 <region>`,
  one fresh launch at a time, the builds alternated, baseline then after, eight
  launches each in each region. The one-minute load average read before every
  launch, and no launch started above 3.
- The measure: the median of a launch's eight `thread` lines' `times_real`; the
  ratio after over baseline for each pair of launches; per region the median of
  the eight ratios and a 90 per cent bootstrap interval (10,000 resamples).
- **The rule:** pass if in each region the median ratio is at least 0.98 and the
  interval's lower end at least 0.96; fail if in any region the interval's upper
  end is below 0.98; otherwise inconclusive, with one more set of launches
  allowed.

## What was measured

All of this was on 7 October 2026, on the 8-core virtual machine, Chrome
153.0.8010.47, .NET SDK 10.0.400. The timing launches ran from 09:31 to 10:13
UTC. The load average before each launch was 1.01 to 2.67. The full output of
every launch, the script and the commands are in the controller's report,
`.superpowers/sdd/2026-10-05-nes-models/speed-report.md` (not committed: that
folder is ignored); the figures are below.

| Region | Median ratio, after over baseline | Lowest to highest of the eight | 90 per cent interval of the median |
| --- | --- | --- | --- |
| NTSC | 1.0118 | 0.8148 to 1.1611 | 0.9660 to 1.1379 |
| PAL | 1.0773 | 0.8571 to 1.2359 | 0.9665 to 1.1395 |

The median of the per-launch medians, in times real time: NTSC baseline 2.145,
after 2.218; PAL baseline 2.307, after 2.343. The page's own wall-clock figures
(NTSC 1.550 and 1.345, PAL 1.425 and 1.405) were lower and are not the measure:
other work took the processor from the page.

**By the rule fixed beforehand, this passes in both regions.** What that means:

- It passes narrowly: both lower ends are 0.966 against a limit of 0.96. The
  intervals run from about 0.97 to 1.14. So a slowdown beyond about 3.5 per cent
  is not seen; a slowdown of 2 to 3 per cent is not ruled out. Both medians are
  above one, so the counters were not slower in these runs.
- The noise is in the launches. At a load of 1 to 2, the main thread's CPU time
  per run still differed by up to a fifth from one launch to the next (NTSC pair
  seven read 0.8148, PAL pair five 1.2359). A set of launches this size cannot
  do better than this. Narrowing the interval to half would take about four
  times as many launches.
- The browser's compiled-ahead-of-time build is the one the site ships, so this
  is the figure that matters; the native half was not done (see below).

## What was not quiet

The load average was low, and the host was not empty. A sample every fifteen
seconds of the busiest processes (`/tmp/nes-runs/watch.log`) shows other work in
31 of the 35 samples taken during launches: another worktree's differential
check, the C# compiler server at 80 to 220 per cent of a core, a build and a
test run, other Chrome processes. The first launch had to wait five minutes for
the two builds' own load to fall, and the load passed 39 while waiting for
others. Seven launches ended with the one-minute average above 3 (up to 9.30),
because other work started during them. They are in the figures: no launch was
dropped. A look at the pairs whose launches both ended at 3 or below (six
NTSC pairs, median 0.998; three PAL pairs, median 1.092) was made after seeing
the loads; it is not the verdict and has too few pairs to add to it.

## The differential check

```sh
cd /tmp/nes-speed-base/bench/nes-speed && dotnet run -c Release --project differential -- /tmp/nes-runs/diff-base.txt
cd <worktree>/bench/nes-speed          && dotnet run -c Release --project differential -- /tmp/nes-runs/diff-after.txt
cmp /tmp/nes-runs/diff-base.txt /tmp/nes-runs/diff-after.txt
```

Each wrote 254 runs and 38764 bytes; `cmp` printed nothing. The two files are
identical (SHA-256 `137a0396e57ec79176c1edbd087c61969edc4f18d112edab4023a95206b023a6`),
so the counters change nothing a pinned NES test ROM can see, in either region.
This was run after the timing runs.

## The native half was not done

`perf` is installed (version 6.8.12) but `kernel.perf_event_paranoid` is 4 on
this host, so `perf stat -e instructions:u` is not allowed to an ordinary user.
The setting was not changed. A count of instructions for each cycle simulated
would have been the better native figure, because it does not move with load.
It would need that setting lowered on a host that is meant for it.

## What was decided, and what it was chosen over

- Both builds compiled ahead of time and measured in the main thread's CPU time,
  not the interpreter or the wall clock: the build the site ships, and the
  measure that other work disturbs least (the core speed entry of 6 October
  used the same).
- Launches in pairs, baseline then after, in the same minutes, over running
  eight of one and then eight of the other: a drift in the host's load then
  falls on both. Waiting for the load to fall put up to seven minutes between
  the two launches of some pairs.
- The rule written before the first run, and kept: it was not changed after the
  intervals were seen. It says pass.

## What was got wrong, or left open

- The plan owed "a quiet host". The load average said quiet and the process list
  said otherwise. Next time the gate should include the busiest other process,
  or the run should be done on a host that is the project's own.
- Eight launches a side is too few to show 2 per cent in the browser. If the
  claim must be shown rather than not contradicted, either more launches (about
  thirty-two a side for an interval half as wide) or the instruction count
  would do it. If the slowdown is ever suspected, the next step is to profile
  `ChipAccesses.Note` in the compiled-ahead-of-time build.
