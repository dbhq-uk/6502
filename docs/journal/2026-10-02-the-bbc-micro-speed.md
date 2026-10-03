---
title: "The BBC Micro's speed in a browser, with the chips so far"
date: 2026-10-02
summary: "The machine as built so far runs well short of the speed the plan asked for in a browser, compiled ahead of time, and under real time interpreted. The plan said to stop here and tell Dan before building the video chips, so that is what happened."
order: 15
---

# 2 October 2026: the BBC Micro's speed in a browser

Task 6 of the plan measures how fast the machine runs in a browser before the
video chips are built, because they will only make it slower and the fix for a
slow machine is cheaper now. The target in the design is at least 25 times a
2 MHz machine for the core alone. The plan's rule for the machine with its chips:
at least 25 times, carry on; 10 to 25 times, carry on and record it; under 10
times, stop and tell Dan before building the video chips.

**The result is under 10 times, so this is a stop.** The compiled build ran at a
median of 7.13 times a 2 MHz machine over fifteen timed runs. The interpreter
ran at 0.64 times, which is slower than a real BBC Micro. Nothing after this
task has been started.

## What was built

- **`src/Dbhq.Machines.BbcMicro.Wasm/`**, the machine as a plain .NET
  WebAssembly app in the shape of `Dbhq.Machines.Kim1.Wasm`: `Load(os, basic,
  dfs)` builds the machine and powers it on, `Run(cycles)`, `Cycles()` and
  `ScreenRow(row)`, which reads a mode 7 text row from screen memory. It is in
  `6502.slnx`.
- **The ROMs are not in it.** The page fetches them as bytes. The bench script
  reads each from `roms/bbc-micro/`, checks it against the SHA-256 in `Pins.cs`
  (it reads the pin from that file, so no hash is typed twice) and serves it
  from memory; it prints the three hashes on every run.
- **`bench/bbc-micro-speed/`**, the page and the script that serves it and runs
  headless Chrome, and a README with the commands. Not built or run in CI.

## What was measured, and how

- **The workload:** power on, run the real OS for 6 million cycles, check the
  screen reads `BBC Computer 32K`, `BASIC` and `>` (every launch printed
  `prompt yes`), then time `Run(2_000_000)` five times. Each run is one second
  of machine time. The machine is idle at the prompt, in the OS's keyboard loop
  with its 100 Hz interrupts, so this is a lower bound on the work a running
  program would do. It also has no video, sound or disc yet.
- **Three fresh browser launches of each build**, five timed runs in each, so
  fifteen runs a build. The task asked for five; the other ten show the
  launch-to-launch spread.
- **The commands**, from the repository root:

  ```
  dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release -o bench/bbc-micro-speed/publish/interpreter
  dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release -p:RunAOTCompilation=true -o bench/bbc-micro-speed/publish/aot
  cd bench/bbc-micro-speed
  node run-in-browser.mjs publish/aot 3
  node run-in-browser.mjs publish/interpreter 3
  ```

- **The machine:** the same KVM virtual machine as the earlier check (8 cores),
  Google Chrome 153.0.8010.47 headless, .NET SDK 10.0.400 with the `wasm-tools`
  workload. **It was not otherwise idle.** `uptime` printed a load average of
  1.92 for the minute before the AOT runs and 2.33 after; `ps` showed one
  unrelated Python training job using a core at about 95 per cent, and nothing
  else above 10 per cent. The wasm code is single threaded and there were seven
  other cores, so I do not think it changed the figures much, but I did not test
  that. Runs were made 21:39 to 21:41 UTC.

## The figures, as printed

AOT, `node run-in-browser.mjs publish/aot 3`, started 21:39:42 UTC:

```
launch 1 boot cycles=6000002 ms=439.400 cycles_per_second=13654989 mhz=13.655
launch 1 timed 1 cycles=2000003 ms=129.000 cycles_per_second=15503899 mhz=15.504
launch 1 timed 2 cycles=2000000 ms=140.100 cycles_per_second=14275517 mhz=14.276
launch 1 timed 3 cycles=2000001 ms=131.600 cycles_per_second=15197576 mhz=15.198
launch 1 timed 4 cycles=2000001 ms=133.800 cycles_per_second=14947691 mhz=14.948
launch 1 timed 5 cycles=2000001 ms=132.800 cycles_per_second=15060248 mhz=15.060
launch 2 boot cycles=6000002 ms=398.200 cycles_per_second=15067810 mhz=15.068
launch 2 timed 1 cycles=2000003 ms=141.700 cycles_per_second=14114347 mhz=14.114
launch 2 timed 2 cycles=2000000 ms=149.700 cycles_per_second=13360053 mhz=13.360
launch 2 timed 3 cycles=2000001 ms=140.200 cycles_per_second=14265342 mhz=14.265
launch 2 timed 4 cycles=2000001 ms=148.300 cycles_per_second=13486183 mhz=13.486
launch 2 timed 5 cycles=2000001 ms=146.100 cycles_per_second=13689261 mhz=13.689
launch 3 boot cycles=6000002 ms=431.300 cycles_per_second=13911435 mhz=13.911
launch 3 timed 1 cycles=2000003 ms=144.200 cycles_per_second=13869646 mhz=13.870
launch 3 timed 2 cycles=2000000 ms=152.100 cycles_per_second=13149244 mhz=13.149
launch 3 timed 3 cycles=2000001 ms=141.100 cycles_per_second=14174352 mhz=14.174
launch 3 timed 4 cycles=2000001 ms=138.600 cycles_per_second=14430022 mhz=14.430
launch 3 timed 5 cycles=2000001 ms=138.200 cycles_per_second=14471787 mhz=14.472
```

Interpreter, `node run-in-browser.mjs publish/interpreter 3`, started 21:39:56 UTC:

```
launch 1 boot cycles=6000002 ms=4783.800 cycles_per_second=1254233 mhz=1.254
launch 1 timed 1 cycles=2000003 ms=1714.300 cycles_per_second=1166659 mhz=1.167
launch 1 timed 2 cycles=2000000 ms=1668.100 cycles_per_second=1198969 mhz=1.199
launch 1 timed 3 cycles=2000001 ms=1690.000 cycles_per_second=1183433 mhz=1.183
launch 1 timed 4 cycles=2000001 ms=1532.100 cycles_per_second=1305398 mhz=1.305
launch 1 timed 5 cycles=2000001 ms=1614.400 cycles_per_second=1238851 mhz=1.239
launch 2 boot cycles=6000002 ms=11079.300 cycles_per_second=541551 mhz=0.542
launch 2 timed 1 cycles=2000003 ms=1539.700 cycles_per_second=1298956 mhz=1.299
launch 2 timed 2 cycles=2000000 ms=1565.500 cycles_per_second=1277547 mhz=1.278
launch 2 timed 3 cycles=2000001 ms=1461.700 cycles_per_second=1368271 mhz=1.368
launch 2 timed 4 cycles=2000001 ms=1389.500 cycles_per_second=1439367 mhz=1.439
launch 2 timed 5 cycles=2000001 ms=1427.900 cycles_per_second=1400659 mhz=1.401
launch 3 boot cycles=6000002 ms=4901.000 cycles_per_second=1224240 mhz=1.224
launch 3 timed 1 cycles=2000003 ms=1502.600 cycles_per_second=1331028 mhz=1.331
launch 3 timed 2 cycles=2000000 ms=1482.300 cycles_per_second=1349255 mhz=1.349
launch 3 timed 3 cycles=2000001 ms=1642.100 cycles_per_second=1217953 mhz=1.218
launch 3 timed 4 cycles=2000001 ms=1760.800 cycles_per_second=1135848 mhz=1.136
launch 3 timed 5 cycles=2000001 ms=1666.500 cycles_per_second=1200121 mhz=1.200
```

The timed runs in millions of cycles per second, and as multiples of 2 MHz
(the figure divided by two, worked in Python from the printed values):

| Build | Best of 15 | Median of 15 | Slowest of 15 | Median per launch |
| --- | --- | --- | --- | --- |
| AOT | 15.504 (7.75 times) | 14.265 (7.13 times) | 13.149 (6.57 times) | 15.060, 13.689, 14.174 (7.53, 6.84, 7.09 times) |
| Interpreter | 1.439 (0.72 times) | 1.278 (0.64 times) | 1.136 (0.57 times) | 1.199, 1.368, 1.218 (0.60, 0.68, 0.61 times) |

For the reading against the plan's rule, the first launch alone (the five runs
the task asked for) gives an AOT median of 15.060, which is 7.53 times. Every
reading is under 10.

## Where it stands against the core alone

For comparison, not as a measurement of the project: the earlier check
([the browser speed check](2026-09-30-the-browser-speed-check.md)) ran the bare
core at a best of 50.968 MHz AOT in the browser on a synthetic loop, 25.48
times. The machine here is about a third of that speed. To see whether the
browser or the machine is the cost, I also ran the same workload as a native
console program on the same virtual machine (a throwaway project in `/tmp`,
not committed): 33.781 to 36.874 MHz over five timed runs, so about 17 times a
2 MHz machine, against about 100 MHz for the core's synthetic loop native. The
machine costs roughly three times the bare core on the same hardware, in a
browser or out of one. AOT in the browser is about 0.4 of native on this
workload, a slightly lower fraction than the core alone showed (0.47). That is
one run of one throwaway program on a shared machine, so read it as the shape
of the cost and not as a figure to quote.

## The decision

**Stop, as the plan says.** The AOT median is 7.13 times, under the plan's 10.
The plan's fix is a cheaper per-cycle path, by batching the 1 MHz ticks and
skipping idle chips, and it is Dan's call whether to spend time on that before
the video chips are built. I have not started on it and have not touched the
bus.

One thing to weigh in that call, from the code and not from a profile: I did not
profile, so I do not know where the cost is. The bus ticks both VIAs, the
keyboard and the 1 MHz stretch logic on every CPU cycle (`BbcBus.cs`), and a
profile of the native build would say how much of the roughly threefold cost is
those ticks before anyone changes anything. The video chips will add to it:
the CRTC's character clock is 1 or 2 MHz depending on the mode and the video ULA's pixel clock is up to 16 MHz (`bus.md` section 3).

## What this does not say

One machine, shared with a training job, one browser, one afternoon, three
launches each. The workload is the OS idling at the prompt, with no video,
sound or disc. The figures are for the machine as it stood after task 5 and
describe nothing later.
