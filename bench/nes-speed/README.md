# NES browser speed check

How many NTSC or PAL CPU cycles per second does the NES run in a browser, with the
bus and the PPU built so far? Measured in .NET WebAssembly's default interpreter
mode and compiled ahead of time (AOT). It is the same method as
[`../bbc-micro-speed/`](../bbc-micro-speed/README.md), with the NES in place of the
BBC Micro. The figures, and what they were run on, are in the journal entry
[`docs/journal/2026-10-05-the-nes-speed.md`](../../docs/journal/2026-10-05-the-nes-speed.md).
This file is how to run it again.

The first figures were taken before the sound and the mappers were built; each row of the table
below says which code it measured.

## What is here

| Path | What it is |
| --- | --- |
| `../../src/Dbhq.Machines.Nes.Wasm/` | The machine as a WebAssembly app. The bench calls `Load` (the cartridge as bytes, the region by name, "NTSC" or "PAL", and the sample rate; it returns the sentence the page shows, which the bench prints), `Run`, `Cycles`, `Frames` and `CpuHz`. The page uses the same class (task 14 of the NES plan gave `Load` its region by name). It holds no ROM. |
| `index.html`, `main.js` | The page. It fetches the ROM, boots it, then times the runs. |
| `run-in-browser.mjs`, `package.json` | Fetches the ROM from the pinned fork, checks it against its SHA-256 in `Pins.cs`, serves it with the page and a published copy of the app on `127.0.0.1`, and runs it in a headless Chrome, a fresh launch each time. |
| `native/` | The same workload as a console program, in the solution so CI builds it. |
| `differential/` | The check that a change for speed changed nothing else: it runs every pinned NES test ROM, the bundled homebrew with a fixed round of button presses, and synthetic cartridges it assembles itself, which keep rendering on and touch the chips at moving dots, in both regions and writes one line of hashes for each (every instruction's registers, cycle and PPU position, every cycle's interrupt lines, every frame's pixels, the sound, the end state, each chip's whole state at every point where it can be seen, and, since format 3, the picture as far as it is drawn at each of those points). In the solution too. |
| `differential/baseline/` | The differential's output with the per-dot reference (`--oracle`), which the lazy build is checked against; the file is named for the commit it was recorded at (`acf9748.txt`, format 3, from 8 October 2026). It replaced `38c5544.txt` (format 2, after the reset fix), which had replaced `4e9b92b.txt`, recorded from the code before the lazy chips, behaving as `5e48505` (below). |
| `differential/faults.py` | Shows the differential can fail: in a scratch copy of the repository, whose path it takes, it plants one fault at a time, runs `--check` against a baseline, and counts the runs each changes; with `--tests <filter>` it also runs the NES tests that match with each fault in, and with `--no-differential` only those. |

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

To check that a change for speed changed no behaviour, run the differential check on the code
before and after it and compare the files; they must be identical:

```sh
dotnet run -c Release --project differential -- before.txt      # on the code before
dotnet run -c Release --project differential -- after.txt       # on the code after
cmp before.txt after.txt
```

Or check against the committed baseline in one command. It prints the first run that differs and
which of its hashes do, and exits 1 on any difference:

```sh
dotnet run -c Release --project differential -- --check differential/baseline/acf9748.txt
dotnet run -c Release --project differential -- --check differential/baseline/acf9748.txt --oracle
```

`--oracle` builds the machine with `NesOptions.PerDotReference`, the per-dot reference that a
lazy build keeps, so the baseline can be made again from a later build and compared. `--only
<text>` runs only the ROMs whose name contains the text (and `--check` then compares those
lines), and `--out <file>` keeps a check's output. The program's opening comment says what each
hash in a line covers, and the file's first line names the format and the frame count, so two
files are comparable only when their first lines are the same. Before it runs anything it checks
by reflection that every field of every chip is in the chips' state reports, and stops, naming
the field, if one is not. A run that throws is written as `crashed: <exception> in <method>`
and the others go on. It runs four ROMs at a time (`--threads <n>` for another number), and
`--coverage` prints, for each synthetic run, the dots its PPU register writes landed on with
rendering on, and for the sprite0 runs of each region together how many of the places sprite 0's
sweep sets (y 0 to 239, eight x's, a left clip on and off) were drawn; the journal entry
[`docs/journal/2026-10-06-the-nes-lazy-chips.md`](../../docs/journal/2026-10-06-the-nes-lazy-chips.md)
has how long a run took, dated.

**The baseline changed when the reset fix landed.** Pull request #71 (the reset button in the
middle of sprite evaluation) made the PPU's place in secondary OAM wrap at 32. That place is in
the PPU's state report, so with #71 in the tree `--check differential/baseline/4e9b92b.txt`
failed on the PPU's hash, with no difference in behaviour behind it: the value the report holds
when secondary OAM is full is 0 where it was 32. On 8 October 2026 the baseline was re-recorded
from the merged tree (`38c5544`) with `--oracle` (the per-dot reference), the lazy build was
checked against it, and the old file was removed (issue #74). The new file differs from the old
in the `ppu` hash alone, on 116 of the 332 runs, and in no other hash; the journal entry
([the lazy chips](../../docs/journal/2026-10-06-the-nes-lazy-chips.md), "The baseline after the
reset fix") has the commands, the date and the counts.

**Format 3, 8 October 2026.** Task 3a of the scanline renderer work (issue #72) closed three gaps
in the gate before the renderer is written. A `pixels` hash was added: at each point where the PPU
can be seen, other than a frame end, power on and the reset button (where the whole picture is
already in the `ppu` hash), the point and the picture as far as it is drawn in the frame, each
finished row hashed once and the row the PPU is on whole. The sprite0 cartridges now sweep sprite 0
over the whole picture and run 900 frames. And scene tests in `tests/Dbhq.Machines.Nes.Tests`
(`PpuDotSceneTests`) put a register access on every dot of a line, which no program timed from the
frame can do on PAL. A new field is a new format, so the baseline was recorded again from `acf9748`
with `--oracle`, the lazy build checked against it, and `38c5544.txt` removed. The runs that are not
sprite0 runs keep every hash of format 2 as `38c5544.txt` had it; the journal entry
([the scanline renderer](../../docs/journal/2026-10-08-the-nes-scanline-renderer.md), "Task 3a")
has the commands, the dates and the counts.

To run it on a baseline that is older than the tool, export that commit with `git archive`
into a folder of its own, copy `bench/nes-speed/differential/` into the same place in the
export, link or copy `.testdata/` there so the ROMs are not fetched again, and run it from the
export's `bench/nes-speed` as above.

For a profile of the WebAssembly build, publish it with `-p:WasmNativeStrip=false`, which keeps
the function names, and give `PROFILE=<n>` to `run-in-browser.mjs`: it records the run with
Chrome's sampling profiler and prints the n functions with the most self time (added in task 17,
the core's speed work; before that the profile was taken with an uncommitted copy of the script).

On a shared machine, `THREAD_TIME=<n>` makes n more timed runs after the page's own, driven
from the script and each measured in the CPU time of the page's main thread (the DevTools
protocol's `ThreadTime`), printed as `thread <i> ... times_real=<x>`. The wall-clock figure falls
when other work takes the processor from the page; the thread's CPU time much less. With both
set, the profile covers only those runs. With `PROFILE_OUT=<path>` as well, each launch's whole profile is written to `<path>.<launch>.json`, and
`node lazy-chips/profile-groups.mjs <profile.json>...` sums the samples by function and by group
(the PPU's per-dot work, the bus, the sound unit, the CPU core, the browser) and prints each group's
share, per profile and the median (task 2c). The same two options work in
[`../bbc-micro-speed/`](../bbc-micro-speed/README.md).

`publish/` is git-ignored. `run-in-browser.mjs` uses `/usr/bin/google-chrome`; set
`CHROME_PATH` to use another. Run one build at a time, leave the machine otherwise idle, and
note `uptime` before and after. If an AOT publish follows an interpreter publish and the
runtime refuses to start, delete `src/Dbhq.Machines.Nes.Wasm/obj/Release` and publish again.

Benchmarks are run locally. They are not run in CI.

## Measurements so far

Dated, with the command that made them; the journal entry has the full output.

| Date | Code | AOT median | Interpreter median | Where |
| --- | --- | --- | --- | --- |
| 7 October 2026, 02:37 to 02:44 UTC | task 2c, a profile of the lazy build (`6a98780`) and of `5e48505`, three profiles of each kind in each region, load 1.1 to 3.5, the BBC Micro bench at 20.0 to 27.6 MHz | the PPU's per-dot work is 38 to 40 percent of the samples in steady state, the bus 26, the sound unit 14, the CPU core 14, the browser 6 to 7 | not measured | [the lazy chips entry](../../docs/journal/2026-10-06-the-nes-lazy-chips.md), task 2c; tables in `lazy-chips/task-2c-profile-groups.txt` |
| 7 October 2026 | the lazy PPU (`3fa4146` against `5e48505`, alternated, two sets of four launches), main-thread CPU time (`THREAD_TIME=8`), load 1.3 to 1.8, the BBC Micro bench at 21.2 to 22.6 MHz in the same minutes | NTSC 2.25 to 3.13 and PAL 2.54 to 3.33 times real time (ratios 1.39 and 1.31) | not measured | [the lazy chips entry](../../docs/journal/2026-10-06-the-nes-lazy-chips.md), task 2b; every line in `lazy-chips/task-2b-browser.txt` |
| 6 October 2026 | task 17, the shared core (`3e52229` against `18cc4ec`, alternated), timed in the main thread's CPU time (`THREAD_TIME=8`) | NTSC 2.21 to 2.50 in one set and 2.48 to 2.46 in another, PAL 2.50 to 2.50 and 2.50 to 2.47 times real time: within noise; the page's wall clock gave 1.1 to 1.5 at loads of 2 to 50 | not measured | [the core speed entry](../../docs/journal/2026-10-06-the-core-speed.md) |
| 5 October 2026, late | task 6b, the speed work (`f97483d` against `5021298`, alternated) | NTSC 2.09 to 2.82 and PAL 2.44 to 3.24 times real time, at a load of about 2, the BBC Micro bench at 29.0 to 29.5 MHz in the same minutes (a quiet machine) | NTSC 0.26 to 0.39 and PAL 0.30 to 0.49 times real time, at a load of 2 to 3 | [the speed entry](../../docs/journal/2026-10-05-the-nes-speed.md), task 6b |
| 5 October 2026 | task 6, the bus and the PPU drawing the background and sprites (`47e72ee` and the `Ppu` split) | NTSC 1.94 and PAL 1.71 times real time at a load of 6 to 8 (0.92 and 1.04 in a later set at a load of 3 to 36); the BBC Micro bench in the same sets gave 9.17 and 5.32 times 2 MHz; a quiet machine is estimated at 2.4 to 2.9 times | NTSC 0.18 and PAL 0.24 times real time, at a load of about 20 | [the speed entry](../../docs/journal/2026-10-05-the-nes-speed.md) |
