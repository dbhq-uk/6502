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
| `native/` | The same workload as a console program, in the solution so CI builds it. `--fingerprint` runs a scripted session (boot, a typed BASIC program that drives the user VIA, BREAK) and hashes every instruction and, every 100,000 cycles, all of RAM and every VIA register, so two builds can be shown to do the same thing. `--profile` compares the machine with the bare CPU on a flat copy of its memory. |
| `alternate.sh` | Runs two or more builds in turn, a launch of each at a time, in the browser or natively, so the shared machine's load falls on all of them, and prints each build's median. |

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

To compare two builds, publish each to its own folder under `publish/` and
alternate them; for the native bench, copy a build of `native/` into `publish/`
(it finds the ROMs by looking for `6502.slnx` above itself):

```sh
dotnet run -c Release --project native -- --fingerprint   # the same lines from two builds: the same behaviour
./alternate.sh browser 3 publish/aot-before publish/aot
./alternate.sh native 5 publish/native-before publish/native-after
```

Benchmarks are run locally. They are not run in CI.

## Measurements so far

Dated, with the command that made them; each journal entry has the full output.

| Date | Code | AOT median | Interpreter median | Where |
| --- | --- | --- | --- | --- |
| 2 October 2026 | `2876852`, the bus and VIAs ticking every cycle | 7.13 times 2 MHz | 0.64 times | [the speed entry](../../docs/journal/2026-10-02-the-bbc-micro-speed.md) |
| 3 October 2026 | `98fe9d5`, the VIAs lazy, the bus looking only at events | 16.98 times 2 MHz (the old code 6.81 in the same session) | 2.11 times (the old code 0.60) | [the bus speed entry](../../docs/journal/2026-10-03-the-bbc-micro-bus-speed.md) |
| 3 October 2026 | task 7, the CRTC added, lazily | 17.95 and 18.83 times 2 MHz in two sets (the old code 17.64 and 18.55 in the same sets) | not measured | [the CRTC entry](../../docs/journal/2026-10-03-the-bbc-micro-crtc.md) |

Both were taken on a shared virtual machine with other work running; the
entries give the load average for each set. The 3 October interpreter set ran
while the load rose to 4.65, and its old-code median includes one launch that
load slowed to a third.
