# Acorn Electron browser speed check

How many 2 MHz cycles per second does the Electron run in a browser, with the ULA's
interrupts and the display contention judged on every RAM access? Measured in .NET
WebAssembly's default interpreter mode and compiled ahead of time (AOT). It is the same
method as [`../bbc-micro-speed/`](../bbc-micro-speed/README.md). The figures, and what they
were run on, are in the journal entry
[`docs/journal/2026-10-04-the-electron-fact-sheets.md`](../../docs/journal/2026-10-04-the-electron-fact-sheets.md),
the seventh task's section. This file is how to run it again.

## What is here

| Path | What it is |
| --- | --- |
| `../../src/Dbhq.Machines.Electron.Wasm/` | The machine as a WebAssembly app. The bench calls `Load`, `Run`, `Cycles`, `Mode`, `SetMode`, `ScreenRow` and `Peek`. It holds no ROMs. |
| `index.html`, `main.js` | The page. It fetches the two ROMs, boots the OS, checks the prompt is on the screen, then times the runs in each mode asked for. |
| `alternate.sh` | Runs two or more published builds in turn, a launch each at a time, and prints each one's medians. |
| `run-in-browser.mjs`, `package.json` | Reads the OS from `roms/electron/` and BASIC from `roms/bbc-micro/`, checks each against its SHA-256 in `Pins.cs`, serves them with the page and a published copy of the app on `127.0.0.1`, and runs it in a headless Chrome, a fresh launch each time. |

## The workload

Power on, run the real OS 1.00 and BBC BASIC for 4 million cycles to the prompt, check the
screen reads `Acorn Electron`, `BASIC` and `>` (mode 6 text, read from screen memory), then
for each mode in the list (default `6,0`) put the ULA in that mode with a write of `$FE07` and
time `Run(2_000_000)` five times. Each run is one second of machine time, so the multiple of a
real Electron is the figure in MHz divided by two. Mode 6 is the boot default and never held up
by the display; mode 0 is held up on every one of its 256 contended lines. After each mode the
page checks the ULA is still in it. The machine sits at the prompt: it is the OS's idle loop,
which in mode 0 spends about half its time held off RAM, so it runs half the instructions it does
in mode 6. For the idle loop, mode 6 is the heavier case for the browser; ROM-resident code with few
RAM accesses would run more instructions a second than it does, and nothing here measures that. The
mode 0 figure is for the idle loop at the prompt only and is likely optimistic for busier programs.

## How to run it

From the repository root. Needs the .NET 10 SDK with the `wasm-tools` workload, Node, and Chrome.

```sh
dotnet publish src/Dbhq.Machines.Electron.Wasm -c Release -o bench/electron-speed/publish/interpreter
dotnet publish src/Dbhq.Machines.Electron.Wasm -c Release -p:RunAOTCompilation=true -o bench/electron-speed/publish/aot

cd bench/electron-speed
npm ci
node run-in-browser.mjs publish/interpreter 3   # folder, launches, timed cycles, boot cycles, timed runs, modes
node run-in-browser.mjs publish/aot 3
node run-in-browser.mjs publish/aot 3 2000000 4000000 5 0,6   # the modes in the other order
```

`publish/` is git-ignored. `run-in-browser.mjs` uses `/usr/bin/google-chrome`; set `CHROME_PATH`
to use another. Run one build at a time, leave the machine otherwise idle, and note `uptime`
before and after. If an AOT publish follows an interpreter publish and the runtime refuses to
start, delete `src/Dbhq.Machines.Electron.Wasm/obj/Release` and publish again.

**On a shared machine, compare builds side by side.** `./alternate.sh <launches> <folder>...`
runs one launch of each build in turn, so a change in the machine's load falls on all of them,
and prints each build's median in each mode (`MODES=0,6` to change the modes). To compare with
an older commit, unpack its source with `git archive <commit> src Directory.Build.props` into a
scratch folder, publish it from there, and give that folder to the script. The ratio between two
builds run this way means more than either figure alone.

Benchmarks are run locally. They are not run in CI.
