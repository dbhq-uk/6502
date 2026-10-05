# NES browser speed check

How many NTSC or PAL CPU cycles per second does the NES run in a browser, with the
bus and the PPU built so far? Measured in .NET WebAssembly's default interpreter
mode and compiled ahead of time (AOT). It is the same method as
[`../bbc-micro-speed/`](../bbc-micro-speed/README.md), with the NES in place of the
BBC Micro. The figures, and what they were run on, are in the journal entry
[`docs/journal/2026-10-05-the-nes-speed.md`](../../docs/journal/2026-10-05-the-nes-speed.md).
This file is how to run it again.

The sound and the mappers will add to the cost, so these figures are a ceiling.

## What is here

| Path | What it is |
| --- | --- |
| `../../src/Dbhq.Machines.Nes.Wasm/` | The machine as a WebAssembly app. The bench calls `Load` (the cartridge as bytes, the region, 0 NTSC or 1 PAL, and the sample rate), `Run`, `Cycles`, `Frames` and `CpuHz`. It holds no ROM. |
| `index.html`, `main.js` | The page. It fetches the ROM, boots it, then times the runs. |
| `run-in-browser.mjs`, `package.json` | Fetches the ROM from the pinned fork, checks it against its SHA-256 in `Pins.cs`, serves it with the page and a published copy of the app on `127.0.0.1`, and runs it in a headless Chrome, a fresh launch each time. |
| `native/` | The same workload as a console program, in the solution so CI builds it. |

## The workload

The ROM is SNOW, a demo by Repulse, `other/snow.nes` in the fork `dbhq-uk/nes-test-roms`.
It is NROM (mapper 0), turns rendering on and keeps it on, and changes the picture every
frame. It is fetched at the commit in `Pins.NesTestRomsCommit`, checked against its hash
in `Pins.NesTestRomHashes`, kept under `.testdata/`, and never committed
(AGENTS.md rule 3). Games and commercial software are never used.

The machine is powered on and run for 5 million cycles (the boot, timed on its own, which
includes the runtime warming up). Then `Run(1_790_000)` is timed five times. Every line has
the same form:

```
timed 1 cycles=<n> frames=<n> ms=<ms> cycles_per_second=<n> mhz=<x> times_real=<x>
```

`times_real` is the cycles a second divided by the region's own clock, `Region.CpuHz`, read
from the machine and never typed. The native program also checks that rendering is on and
that the picture changes from one frame to the next, and prints `rendering yes` and
`picture changes yes`; if either says `NO` the timed runs are not the workload.

## How to run it

From the repository root. Needs the .NET 10 SDK with the `wasm-tools` workload, Node, and Chrome.

```sh
dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release -o bench/nes-speed/publish/interpreter
dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release -p:RunAOTCompilation=true -o bench/nes-speed/publish/aot

cd bench/nes-speed
npm ci
node run-in-browser.mjs publish/interpreter 1 1790000 5000000 5 0   # folder, launches, timed cycles, boot cycles, timed runs, region
node run-in-browser.mjs publish/aot 1 1790000 5000000 5 1           # region 1 is PAL

# natively
dotnet run -c Release --project native -- 5 1790000 ntsc            # timed runs, cycles a run, region, boot cycles
dotnet run -c Release --project native -- 5 1790000 pal
```

`publish/` is git-ignored. `run-in-browser.mjs` uses `/usr/bin/google-chrome`; set
`CHROME_PATH` to use another. Run one build at a time, leave the machine otherwise idle, and
note `uptime` before and after. If an AOT publish follows an interpreter publish and the
runtime refuses to start, delete `src/Dbhq.Machines.Nes.Wasm/obj/Release` and publish again.

Benchmarks are run locally. They are not run in CI.

## Measurements so far

Dated, with the command that made them; the journal entry has the full output.

| Date | Code | AOT median | Interpreter median | Where |
| --- | --- | --- | --- | --- |
| 5 October 2026 | task 6, the bus and the PPU drawing the background and sprites (`47e72ee` and the `Ppu` split) | NTSC 1.94 and PAL 1.71 times real time at a load of 6 to 8 (0.92 and 1.04 in a later set at a load of 3 to 36); the BBC Micro bench in the same sets gave 9.17 and 5.32 times 2 MHz; a quiet machine is estimated at 2.4 to 2.9 times | NTSC 0.18 and PAL 0.24 times real time, at a load of about 20 | [the speed entry](../../docs/journal/2026-10-05-the-nes-speed.md) |
