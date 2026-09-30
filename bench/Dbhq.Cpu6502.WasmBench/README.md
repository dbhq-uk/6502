# Browser speed check

How many 6502 cycles per second does the core run in a browser? Measured two
ways, .NET WebAssembly in its default interpreter mode and compiled ahead of
time (AOT), and against the same workload run as an ordinary native program.

It is a measurement, not a product. The results are one machine's, on one
day, and the journal entry
[`docs/journal/2026-09-30-the-browser-speed-check.md`](../../docs/journal/2026-09-30-the-browser-speed-check.md)
has the figures and what they were run on. This file is how to run it again.

## What is here

| Path | What it is |
| --- | --- |
| `Workload.cs` | The 6502 program, hand-assembled as a byte array with a comment per instruction, and the loop that runs it. Compiled into both apps below. |
| `Program.cs`, `wwwroot/` | The browser app. The page runs a warm-up, then the measured run, and prints one line each. |
| `run-in-browser.mjs`, `package.json` | Serves a published copy on `127.0.0.1` and runs it in a real headless Chrome, once per run, each in a fresh browser. |
| `../Dbhq.Cpu6502.SpeedNative/` | The same workload as a native console app, for the comparable native figure. Its `--listing` mode checks the bytes read back as the program described. |

Both projects are in `6502.slnx` and build with warnings as errors like the
rest. The browser project sets `AllowUnsafeBlocks` because the JavaScript
interop generator writes unsafe code for `[JSExport]`, and declares
`SupportedOSPlatform("browser")` because the platform analyser is an error
here. Nothing in `src/` was touched, and the core still names no machine.

## The workload

Our own synthetic program, written for this measurement. It is not a real
machine's software and not a third-party program, and nothing third-party is
committed here. It runs forever on a plain 64 KB array with no logging, on the
NMOS 6502 variant. Each lap of its loop does:

- zero-page and absolute loads and stores, and a logic operation
- binary arithmetic (`ADC`)
- an indexed load that crosses a page on 15 laps in 16, and an indexed store
- a `JSR` and `RTS` pair, with a read-modify-write inside
- a decimal-mode `ADC`
- a branch never taken, a branch taken on its own page, and a loop-closing
  branch taken 255 laps in 256 that crosses a page boundary

The program straddles the page boundary at `$2000` on purpose. The comments in
`Workload.cs` say what each part is for. `--listing` prints the disassembly and
how many cycles each watched instruction took over 512 laps.

The run stops at the first instruction that ends at or after the requested
number of cycles, so it overshoots by less than one instruction; the cycles
actually run are what is reported. Every line has the same form:

```
cycles=100000001 ms=1050.422 cycles_per_second=95199817 mhz=95.200
```

Each run does a warm-up first (5 million cycles by default), timed and
printed but not counted, then the measured run (100 million by default). Both
are parameters.

## How to run it

From the repository root. Needs the .NET 10 SDK with the `wasm-tools`
workload (`dotnet workload list` shows it; `dotnet workload install
wasm-tools` adds it), Node, and Chrome.

Native:

```sh
dotnet build -c Release bench/Dbhq.Cpu6502.SpeedNative
dotnet bench/Dbhq.Cpu6502.SpeedNative/bin/Release/net10.0/Dbhq.Cpu6502.SpeedNative.dll
# arguments: [measured cycles] [warm-up cycles]; run it five times for five runs
dotnet run -c Release --project bench/Dbhq.Cpu6502.SpeedNative -- --listing
```

Browser, both builds. The published output goes under `publish/`, which is
git-ignored. AOT compilation takes a couple of minutes and a lot of CPU.

```sh
dotnet publish bench/Dbhq.Cpu6502.WasmBench -c Release -o bench/Dbhq.Cpu6502.WasmBench/publish/interpreter
dotnet publish bench/Dbhq.Cpu6502.WasmBench -c Release -p:RunAOTCompilation=true -o bench/Dbhq.Cpu6502.WasmBench/publish/aot

cd bench/Dbhq.Cpu6502.WasmBench
npm install
node run-in-browser.mjs publish/interpreter 5    # folder, runs, cycles, warm-up cycles
node run-in-browser.mjs publish/aot 5
```

`run-in-browser.mjs` uses `/usr/bin/google-chrome`; set `CHROME_PATH` to use
another. `node_modules/` is git-ignored. The server binds to loopback and
serves one folder; do not expose it.

Run one build at a time and leave the machine otherwise idle. Runs vary, so
run each at least five times and report all of them.

## Limits

One machine, one browser, one day. The workload is synthetic and small: it
exercises a handful of instructions, not the whole opcode table. It runs on a
plain array, so the cost of a real machine's bus, with video, sound and timers
advancing on each cycle, is not in these figures.
