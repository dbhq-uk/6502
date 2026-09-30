# 30 September 2026: the browser speed check

The design says the emulators run in the browser as .NET WebAssembly apps, and
that a speed check comes first, while a change of course is still cheap. This is
that check. The question was how many 6502 cycles per second the core runs in a
browser, in .NET's default interpreter mode and compiled ahead of time (AOT),
against a BBC Micro's 2 million a second.

Everything here is one machine's result, on one day, for a program of our own
that is not a real machine's software. The project, with the commands to run it
again, is in [`bench/Dbhq.Cpu6502.WasmBench/`](../../bench/Dbhq.Cpu6502.WasmBench/README.md).

## What was measured, and how

- **The workload is our own synthetic 6502 program,** a small loop written as a
  byte array in `bench/Dbhq.Cpu6502.WasmBench/Workload.cs`, with a comment on
  every instruction. It mixes zero-page and absolute loads and stores, binary
  arithmetic, an indexed load that crosses a page on 15 laps in 16, an indexed
  store, a `JSR` and `RTS` pair, a decimal-mode `ADC`, a branch that is never
  taken, a branch that is taken on its own page, and a loop-closing branch that
  is taken 255 laps in 256 and crosses a page. It is not Dormann's program or
  anything else third-party, and it is not a real machine's software.
- **The bus is a plain 64 KB array with no logging,** the core is the NMOS 6502
  variant, and the run calls `Cpu.Step()` until at least 100 million cycles
  have gone by. The cycles actually run are printed, because the last
  instruction can overshoot: every run ran 100,000,001 cycles.
- **Each run does a warm-up first,** 5 million cycles, timed and printed but
  not counted, then the measured run. The same source file, `Workload.cs`, is
  compiled into both the browser app and a native console app
  (`bench/Dbhq.Cpu6502.SpeedNative`), so the three figures come from the same
  bytes on the same core.
- **The bytes were checked to be the program the comments describe.**
  `dotnet run -c Release --project bench/Dbhq.Cpu6502.SpeedNative -- --listing`
  disassembles them with the core's own disassembler, and traces 512 laps. Over
  those laps the indexed load took 5 cycles 480 times and 4 cycles 32 times (15
  laps in 16 cross a page, as intended), the never-taken branch took 2 cycles 512
  times, the same-page taken branch took 3 cycles 512 times, and the loop-closing
  branch took 4 cycles 510 times and 2 cycles twice (once per wrap of X).
- **Two browser builds, one native run.** Interpreter: `dotnet publish
  bench/Dbhq.Cpu6502.WasmBench -c Release -o
  bench/Dbhq.Cpu6502.WasmBench/publish/interpreter`. AOT: the same with
  `-p:RunAOTCompilation=true` and `-o .../publish/aot`. Each was served from
  `127.0.0.1` and run by `node run-in-browser.mjs publish/<build> 5`, which
  starts a fresh headless Chrome for every run. Native: five separate
  invocations of `dotnet bench/Dbhq.Cpu6502.SpeedNative/bin/Release/net10.0/Dbhq.Cpu6502.SpeedNative.dll`.
- **The machine:** `nproc` printed 8; `lscpu` names the model `DO-Premium-AMD`
  (one socket, eight cores, one thread each), `/proc/cpuinfo` showed 1996.25
  MHz, and `free -h` showed 31 GiB. The browser was Google Chrome
  153.0.8010.47 (`google-chrome --version`, and the same string from
  Playwright's `browser.version()`), headless. .NET SDK 10.0.400 with the
  `wasm-tools` workload; the browser runtime pack was 10.0.12 (from the build
  output). The highest installed native runtime is 10.0.11. The wasm code is
  single threaded, so the eight cores matter only in that the machine was
  otherwise idle.

## The figures, as printed

Native, run at 12:42 UTC. The five runs are five separate processes.

```
run 1 warmup cycles=5000002 ms=200.622 cycles_per_second=24922526 mhz=24.923
run 1 measured cycles=100000001 ms=1050.422 cycles_per_second=95199817 mhz=95.200
run 2 warmup cycles=5000002 ms=213.973 cycles_per_second=23367454 mhz=23.367
run 2 measured cycles=100000001 ms=952.310 cycles_per_second=105007868 mhz=105.008
run 3 warmup cycles=5000002 ms=219.269 cycles_per_second=22803009 mhz=22.803
run 3 measured cycles=100000001 ms=984.311 cycles_per_second=101593867 mhz=101.594
run 4 warmup cycles=5000002 ms=209.469 cycles_per_second=23869936 mhz=23.870
run 4 measured cycles=100000001 ms=929.419 cycles_per_second=107594112 mhz=107.594
run 5 warmup cycles=5000002 ms=200.987 cycles_per_second=24877204 mhz=24.877
run 5 measured cycles=100000001 ms=997.384 cycles_per_second=100262307 mhz=100.262
```

WebAssembly, interpreter build, run 12:42:12 to 12:44:25 UTC with
`node run-in-browser.mjs publish/interpreter 5`. The first line is the browser
version the script printed.

```
browser 153.0.8010.47
run 1 warmup cycles=5000002 ms=2448.600 cycles_per_second=2041984 mhz=2.042
run 1 measured cycles=100000001 ms=22319.900 cycles_per_second=4480307 mhz=4.480
run 2 warmup cycles=5000002 ms=2402.000 cycles_per_second=2081600 mhz=2.082
run 2 measured cycles=100000001 ms=24871.400 cycles_per_second=4020682 mhz=4.021
run 3 warmup cycles=5000002 ms=2524.400 cycles_per_second=1980669 mhz=1.981
run 3 measured cycles=100000001 ms=23079.000 cycles_per_second=4332943 mhz=4.333
run 4 warmup cycles=5000002 ms=2368.700 cycles_per_second=2110863 mhz=2.111
run 4 measured cycles=100000001 ms=21524.800 cycles_per_second=4645804 mhz=4.646
run 5 warmup cycles=5000002 ms=2872.500 cycles_per_second=1740645 mhz=1.741
run 5 measured cycles=100000001 ms=21329.700 cycles_per_second=4688299 mhz=4.688
```

WebAssembly, AOT build, run 12:44:28 to 12:44:45 UTC with
`node run-in-browser.mjs publish/aot 5`.

```
browser 153.0.8010.47
run 1 warmup cycles=5000002 ms=127.900 cycles_per_second=39093057 mhz=39.093
run 1 measured cycles=100000001 ms=2034.000 cycles_per_second=49164209 mhz=49.164
run 2 warmup cycles=5000002 ms=168.500 cycles_per_second=29673602 mhz=29.674
run 2 measured cycles=100000001 ms=2181.200 cycles_per_second=45846324 mhz=45.846
run 3 warmup cycles=5000002 ms=132.100 cycles_per_second=37850129 mhz=37.850
run 3 measured cycles=100000001 ms=1990.400 cycles_per_second=50241158 mhz=50.241
run 4 warmup cycles=5000002 ms=131.300 cycles_per_second=38080746 mhz=38.081
run 4 measured cycles=100000001 ms=2066.500 cycles_per_second=48391000 mhz=48.391
run 5 warmup cycles=5000002 ms=142.300 cycles_per_second=35137048 mhz=35.137
run 5 measured cycles=100000001 ms=1962.000 cycles_per_second=50968400 mhz=50.968
```

The measured runs, in millions of cycles per second, for reading:

| Run | Native | Interpreter | AOT |
| --- | --- | --- | --- |
| 1 | 95.200 | 4.480 | 49.164 |
| 2 | 105.008 | 4.021 | 45.846 |
| 3 | 101.594 | 4.333 | 50.241 |
| 4 | 107.594 | 4.646 | 48.391 |
| 5 | 100.262 | 4.688 | 50.968 |
| **Best** | **107.594** | **4.688** | **50.968** |

Runs vary, so the best of five is reported, the same as the existing
benchmark; the spread is shown so nobody has to take the best on trust. The
slowest and fastest runs were 95.200 and 107.594 (native), 4.021 and 4.688
(interpreter) and 45.846 and 50.968 (AOT).

## The ratios, from the best runs

Arithmetic, on the printed `cycles_per_second` values (107,594,112 native;
4,688,299 interpreter; 50,968,400 AOT), worked in Python and checked against
the table above:

- **Against a 2 MHz BBC Micro:** native 107,594,112 / 2,000,000 = 53.8 times;
  interpreter 4,688,299 / 2,000,000 = 2.34 times; AOT 50,968,400 / 2,000,000 =
  25.48 times.
- **Against each other:** AOT is 50,968,400 / 4,688,299 = 10.87 times the
  interpreter. AOT is 50,968,400 / 107,594,112 = 0.474 of native, and the
  interpreter is 4,688,299 / 107,594,112 = 0.0436 of native, so native is 2.11
  times AOT and 22.9 times the interpreter.
- **Against the design's target of at least 25 times real speed** (a target
  for the core alone, as a native figure): native was above it on every run.
  AOT in the browser reached 25 times a 2 MHz BBC Micro on two of five runs
  (25.12 and 25.48 times) and was below it on three (24.58, 22.92 and 24.20).
  The interpreter's runs were 2.01 to 2.34 times a 2 MHz BBC Micro.

## What that means, stated plainly

The best browser figures on this machine were 2.34 times a 2 MHz BBC Micro
with the interpreter and 25.48 times with AOT. Those are the figures; whether
that is headroom enough once a real machine's chips share each cycle is not
something this measurement can say, because the workload runs on a plain array
and none of the machine's other chips are in it.

## Problems hit and how they were solved

- **The browser project would not compile at first.** `[JSExport]` needs
  `AllowUnsafeBlocks` (error SYSLIB1075 and CS0227: the interop generator writes
  unsafe code). Set in that project's csproj only, with a comment.
- **Then the platform analyser failed the build.** CA1416 said `JSExport` is
  supported only on the browser, and warnings are errors here. The template
  solves it with `SupportedOSPlatform("browser")` on the assembly; the same
  attribute is in `Program.cs`. Warnings stay as errors.
- **The project is in `6502.slnx`.** `dotnet build -c Release` on the whole
  solution succeeded with 0 warnings and 0 errors, and `dotnet test
  --configuration Release` passed all 1,480 tests. CI runs `dotnet test` on
  the solution too, and the `Validate` run on the pull request
  (https://github.com/dbhq-uk/6502/pull/6) finished with a conclusion of
  success, so the wasm project builds there as well (`gh run list --repo
  dbhq-uk/6502 --branch stage-2/browser-speed`). I did not check whether that
  runner had the `wasm-tools` workload installed.
- **The interpreter publish also relinked the native runtime:** its build
  output shows `wasm-ld` running. So "default" here means the default settings
  of `dotnet publish -c Release` for this SDK, with no AOT and no other options
  set. I did not test other settings.
- **Publish times, one each:** the interpreter publish took 42.8 s and the AOT
  publish 2 min 5 s of wall clock (`time`). The AOT output was 18 MB in the
  publish folder against 12 MB for the interpreter (`du -sh`); `dotnet.native.wasm`
  was 7,128,453 bytes for AOT and 2,875,697 for the interpreter (`ls -la`).
- **A smoke run of the interpreter build** with 2 million cycles was made while
  the AOT publish was still compiling, to check the page and the script worked.
  Its figures are not used anywhere.

## What surprised me

- **The interpreter is not as slow as feared.** Its best run was 4.688 MHz, 2.34
  times a BBC Micro, and its worst was 4.021 MHz. I had guessed 1 to 3
  MHz before running it. I did not investigate why it is as fast as it is.
- **The warm-up runs are much slower than the measured runs on all three,**
  which is why they are there: native printed 22.8 to 24.9 MHz warm-up against
  95.2 to 107.6 measured, the interpreter 1.74 to 2.11 against 4.02 to 4.69, and
  AOT 29.7 to 39.1 against 45.8 to 51.0. I did not investigate the reasons, and
  a run of only a few million cycles would report much lower figures than a long
  one.
- **The native figure here is higher than the existing Dormann benchmark's.**
  That benchmark's entry in
  [`2026-09-30-building-the-core.md`](2026-09-30-building-the-core.md) printed 62.0
  to 82.8 MHz on its own program. This workload is a different program, so the
  two are not comparable, and I did not look into the difference.
- **Browser timings come in steps of 0.1 ms** in every printed `ms` value (for
  example `22319.900`), where the native ones have three decimals. That is what
  was printed; I did not check the browser's timer resolution.

## What this does not say

It is one machine, one browser build, one day, and five runs each. The workload
is a synthetic program of our own, on a plain array, so it says nothing about a
whole BBC Micro or NES with their other chips running, or about other browsers,
other machines, or later .NET releases.
