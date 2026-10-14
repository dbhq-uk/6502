# The Atari 2600's speed milestone: the rule, fixed before the first launch

This is the rule for task 8 of [the Atari 2600 plan](../../docs/superpowers/plans/2026-10-14-atari-2600.md): how fast the machine runs in headless Chrome, with the bus, the RIOT and the TIA's picture and before the sound and the input, and what happens next. It states items 9 and 10 of the plan's "Fixed before running" and fixes what they leave open. It is committed before the first launch, as the BBC Micro's [`chip-counters/rule.md`](../bbc-micro-speed/chip-counters/rule.md) was, and nothing here is changed after a result is seen.

Written on 14 October 2026 and committed at about 20:21 UTC, before any launch. No launch has run.

Added on 14 October 2026, after the first run: that run and its verdict are in the journal entry [the speed milestone](../../docs/journal/2026-10-14-the-atari-2600-speed-milestone.md). The text above and below is as it was committed before the launch.

Amended on 14 October 2026, after the first result was seen (task 10's review): [`analyse.py`](analyse.py) rounds each AOT median to the two decimal places it prints before it judges it, so the figure judged against the lines below is the printed, two-decimal figure, not the unrounded median. This rule did not say so; this note was made after the first result, and it changes no verdict: every lower AOT median so far is under 10, and none lies within a rounding step of 10 or 25.

## Items 9 and 10, as the plan fixes them

**9. The speed milestone's rule (the NES plan's):** the AOT median, as a multiple of real time from `Region.CpuHz`, on the bench program in item 10. At least 25 times: continue. From 10 to 25: continue, and the page's headroom line says so. Under 10: stop and tell Dan before the sound is built.

**10. The speed bench:** `harness/calibration_ntsc.a26` on NTSC and `_pal.a26` on PAL (the capture-rig target, which draws every line), run for 60 frames to settle, then `Run` of one second of machine time timed five times, best and median, AOT and interpreter, on a GitHub runner by a hand-started workflow, with the host recorded first and the load checked before each launch as `bbc-speed-run.yml` does it, and the verdict printed.

## The host

A GitHub-hosted runner, `ubuntu-latest`, by the hand-started workflow [`atari-2600-speed-run.yml`](../../.github/workflows/atari-2600-speed-run.yml), never the development box (the plan's task 8; Dan, 14 October 2026). The workflow records the host in `host.txt` before anything is timed.

## The builds

From one commit, the `ref` the workflow is given, exported with `git archive`:

- **AOT:** `dotnet publish src/Dbhq.Machines.Atari2600.Wasm -c Release -p:RunAOTCompilation=true -o <folder>`.
- **Interpreter:** `dotnet publish src/Dbhq.Machines.Atari2600.Wasm -c Release -o <folder>`.

Each after deleting the project's `obj/Release` and `bin/Release`, because an AOT publish after an interpreter publish can leave a runtime that will not start (`bench/bbc-micro-speed/README.md`).

## The launches

Four launches, one at a time, in this order: AOT NTSC, AOT PAL, interpreter NTSC, interpreter PAL. Each is

```sh
node run-in-browser.mjs <folder> 1 <NTSC|PAL> 5 60
```

which fetches the region's `harness/calibration` from the fork, checks it, powers the machine on with it, runs 60 frames (`RunFrames(60)`) to settle, then times `Run` of one second of machine time five times. One second of machine time is the region's `Region.CpuHz`, read from the machine and rounded to whole cycles; `Run` runs whole instructions, so a run is that many cycles or a few more, and the page prints the cycles it actually ran. The timer is the page's `performance.now()` around each `Run` call.

**What counts.** A launch counts only if it ended with exit 0, printed its `load` line naming the board and the region, a `settle` line with 60 frames, and five `timed` lines, and in every `timed` line the frames are more than 0 and the last frame's lines equal the region's `Region.NominalLines` (the page prints both, read from the machine). So a program that stopped drawing, or never ends its frames, is not timed as the workload.

**A launch that does not count** stays in the log, and is run again, up to three tries in all. It has no figure, so nothing is chosen among figures: the first launch under its label that counts is the one judged. If none of three counts, the run has no verdict and the job is red.

**The load gate.** Before every launch the one-minute load average in `/proc/loadavg` must be under 1.00; if not, the script waits, polling every 15 seconds. If one wait passes 1800 seconds, the series stops with an error and there is no verdict. The busiest processes are logged before each launch, and a watch log samples the load every 15 seconds through the series. This is `bench/bbc-micro-speed/chip-counters/run-series.sh`'s gate.

**The log.** Every launch's whole output goes to `launches.txt`, between `=== launch <label> folder=<folder> start=<UTC> loadavg=<the five fields>` and `--- exit=<code> end=<UTC> loadavg=<the five fields>`, with the label `<aot|interpreter>-<ntsc|pal>`. [`analyse.py`](analyse.py) reads that file.

## The measure

- Per timed run: `times_real` = (cycles run / seconds) / `Region.CpuHz`, the clock the page printed from the machine.
- Per launch: the best (the highest) and the median of its five `times_real`.

## The verdict

The rule is judged on the **AOT median** of each region. Item 9 names one figure and item 10 two regions, so both must clear a line: the verdict is set by the lower of the two AOT medians.

- **CONTINUE** if that lower median is at least 25.
- **CONTINUE, HEADROOM** if it is at least 10 and under 25: continue, and task 13's headroom line says so.
- **STOP** if it is under 10: stop and tell Dan, with the figure and the run's link, before task 10 (the sound) is built. The spec already plans the speed pass after the count (issue #130), so the likely answer is to go on; it is still his call.

`analyse.py` prints each launch's five figures, its best and its median, then one line `VERDICT: CONTINUE`, `VERDICT: CONTINUE, HEADROOM` or `VERDICT: STOP`. With a launch missing it prints `VERDICT: NONE`. The interpreter's figures are measured and recorded, and judged by nothing.

The workflow is green only on CONTINUE or CONTINUE, HEADROOM, as the plan's step 4 asks ("the job green only when the rule says continue"), so a red run is never read as a pass. A STOP is recorded as it is, and Dan is told; the rule is not loosened, and the machine is not changed to clear it without his say.

## What the figure does not show

- **The page's other work.** The timed runs only run the machine. The page also turns each frame into RGBA, plays the sound (task 10) and reads the input (task 9); none of that is here, as the plan's "before the sound and the input" says.
- **One host.** A GitHub runner is a virtual machine on shared hardware, and its neighbours do not show in its load average. The figure is that runner's, on that day.
- **One program.** The calibration target draws every line with the playfield, a colour and the ball, and no players, missiles or HMOVE.

The figure is quoted only with its run's link, the commit measured and the date (`AGENTS.md` rule 5), and nothing on the site states it.
