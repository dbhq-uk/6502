# BBC Micro browser speed check

How many 2 MHz cycles per second does the BBC Micro run in a browser, with the
chips built so far? Measured in .NET WebAssembly's default interpreter mode and
compiled ahead of time (AOT). It is the same method as
[`../Dbhq.Cpu6502.WasmBench/`](../Dbhq.Cpu6502.WasmBench/README.md), with the
whole machine in place of a synthetic loop. The figures, and what they were run
on, are in the journal entry
[`docs/journal/2026-10-02-the-bbc-micro-speed.md`](../../docs/journal/2026-10-02-the-bbc-micro-speed.md).
This file is how to run it again.

## What is here

| Path | What it is |
| --- | --- |
| `../../src/Dbhq.Machines.BbcMicro.Wasm/` | The machine as a WebAssembly app: `Load`, `Run`, `Cycles`, `ScreenRow`. It holds no ROMs. |
| `index.html`, `main.js` | The page. It fetches the three ROMs, boots the OS, checks the prompt is on the screen, then times the runs. |
| `run-in-browser.mjs`, `package.json` | Reads the three ROMs from `roms/bbc-micro/`, checks each against its SHA-256 in `Pins.cs`, serves them with the page and a published copy of the app on `127.0.0.1`, and runs it in a headless Chrome, a fresh launch each time. |

## The workload

Power on, run the real MOS 1.20 and BBC BASIC for 6 million cycles (three seconds
of machine time) to the prompt, check the screen reads `BBC Computer 32K`,
`BASIC` and `>`, then time `Run(2_000_000)` five times. Each timed run is one
second of machine time, so the multiple of a real machine is the figure in MHz
divided by two. The machine sits at the prompt, waiting for a key, while it is
timed: that is the OS's idle loop and its 100 Hz interrupts, not a program.

## How to run it

From the repository root. Needs the .NET 10 SDK with the `wasm-tools` workload,
Node, and Chrome.

```sh
dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release -o bench/bbc-micro-speed/publish/interpreter
dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release -p:RunAOTCompilation=true -o bench/bbc-micro-speed/publish/aot

cd bench/bbc-micro-speed
npm ci
node run-in-browser.mjs publish/interpreter 3   # folder, launches, timed cycles, boot cycles, timed runs
node run-in-browser.mjs publish/aot 3
```

`publish/` is git-ignored. `run-in-browser.mjs` uses `/usr/bin/google-chrome`;
set `CHROME_PATH` to use another. Run one build at a time, leave the machine
otherwise idle, and note `uptime` before and after. If an AOT publish follows an
interpreter publish and the runtime refuses to start, delete
`src/Dbhq.Machines.BbcMicro.Wasm/obj/Release` and publish again.

Benchmarks are run locally. They are not run in CI.
