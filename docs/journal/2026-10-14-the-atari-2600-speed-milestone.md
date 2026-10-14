---
title: "The Atari VCS's speed milestone: the rule, the bench and the workflow"
date: 2026-10-14
summary: "The eighth task of the Atari VCS: the machine as WebAssembly, a browser bench that times the suite's calibration program in both regions, compiled ahead of time and in the interpreter, the rule that judges it, committed before any launch, and a workflow started by hand that runs it on a GitHub runner and never on this box."
order: 62
---

# 14 October 2026: the Atari 2600's speed milestone

Task 8 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): how fast the machine runs in headless Chrome with the bus, the RIOT and the TIA's picture, before the sound and the input are built. The spec planned it because the TIA does three colour clocks of work for every CPU cycle, the NES's PPU pattern, and the NES needed its lazy chips after it went live. New: `src/Dbhq.Machines.Atari2600.Wasm/` and `bench/atari-2600-speed/`, and, in a pull request of its own to `main`, `.github/workflows/atari-2600-speed-run.yml`.

## What was built

- **`Dbhq.Machines.Atari2600.Wasm`**, the machine as .NET WebAssembly, its project in the shape of the NES's. Its first host, `Atari2600Host`, has the plan's `Load(rom, region, sampleRate)`, `Run`, `Cycles` and `Frames`, and four more the bench needs: `RunFrames` for the 60 frames that settle the machine, `CpuHz` so the multiple of real time comes from `Region.CpuHz` and is never typed, and `Lines` and `NominalLines` for the check that the program is drawing whole frames. `Load` takes the region by name, "NTSC" or "PAL", returns a sentence naming the board, and keeps the machine running when a file is refused, as `NesHost` does. Task 13 adds the picture, the sound and the input.
- **`bench/atari-2600-speed/`**, in the shape of `bench/bbc-micro-speed/` and its `chip-counters/`: the page and its driver, `run-in-browser.mjs`, which fetches the region's `harness/calibration` from `dbhq-uk/missingno` at the pinned commit, checks the suite's manifest against its SHA-256 in `Pins.cs` and the file against its git blob hash, and runs the page in a fresh headless Chrome; `run-series.sh`, which runs the rule's four launches behind the load gate; and `analyse.py`, which prints the figures and the verdict.
- **[`rule.md`](../../bench/atari-2600-speed/rule.md)**, items 9 and 10 of the plan's "Fixed before running" and what they leave open, committed before the first launch.
- **The workflow**, `workflow_dispatch` only, with one input, `ref`. It builds that ref's WebAssembly app, AOT and interpreter, and runs that ref's bench and rule, so the harness and the rule are the measured commit's own. It records the host first, uploads everything as an artefact, and puts the verdict in the run's summary.

## Decisions

- **The verdict is set by the lower of the two regions' AOT medians.** Item 9 names one figure, "the AOT median", and item 10 measures two regions. Judging the lower one means both must clear a line. It was chosen over judging NTSC alone, which would let a slow PAL pass unseen, and over the mean of the two, which item 9 does not name.
- **One launch per build and region, five timed runs in each**, as item 10 says, in a fixed order: AOT NTSC, AOT PAL, interpreter NTSC, interpreter PAL. The BBC Micro's rule alternated pairs because it compared two builds; this one compares a figure with fixed lines, so there is nothing for drift to favour.
- **A launch counts only if the program was drawing.** Each timed run must end at least one frame, and its last frame must have the region's `Region.NominalLines`. Without that, a program that had stopped drawing or never ended a frame would be timed as the workload; the NES's bench met the same risk with its "rendering yes" check.
- **A launch that does not count is run again, up to three tries, and the first that counts is judged.** It has no figure, so no figure is chosen among others. What counts is written once, in `analyse.py`, and `run-series.sh` asks it after each launch.
- **The workflow is green only on CONTINUE or CONTINUE, HEADROOM**, as the plan's step 4 says. STOP and a run with no verdict end red.
- **The harness and the rule come from the measured ref, not the workflow's own checkout.** The plan asks for this so the workflow can measure the feature branch; the BBC Micro's workflow took its scripts from the branch it was started from instead.
- **The actions and the SDK are pinned as `validate.yml` pins them today**: `actions/setup-node` at v7.1.0 and `actions/upload-artifact` at v7.0.2, newer than the pins in `bbc-speed-run.yml`.

## What was checked, and how

Nothing was timed on this box (the brief: no speed figure from here; it is an aarch64 virtual machine shared with other work, with no Chrome).

- **`analyse.py`**, against eight synthetic launches files whose figures are made up, written before it in a scratch folder outside the repository: well above the top line, between the lines, under the bottom line, exactly 25 and exactly 10, a first launch that does not count followed by one that does, a launch that failed, and a timed run that ended no frame. Each case has the verdict it should give. Without `analyse.py` all eight gave no verdict; with it all eight gave the one expected.
- **`run-series.sh`**, with a stand-in for `node` that prints the same synthetic output and fails its first call: the failed launch was kept in the log, run again, and the next one judged; with the load file set above the limit and no wait allowed, the series stopped before any launch.
- **The host**, published in the interpreter (`dotnet publish src/Dbhq.Machines.Atari2600.Wasm -c Release`) and loaded under Node by a scratch script that times nothing: in each region `Load` named the board ("The cartridge runs as 4K, in NTSC." and the same in PAL), 60 frames settled with the region's nominal lines, and a run of one second of machine time ended frames of those lines, which is the check a launch must pass; a three-byte file and the region "SECAM" were refused with their sentences. The page itself was not run here, for want of Chrome; its first run is the workflow's.
- **The workflow file** parses as YAML, with `workflow_dispatch` its only trigger.

## The workflow, and Dan's approval

`AGENTS.md` allows a speed workflow started by hand and never part of a pull request check, one per machine that needs one, each approved by Dan when it is built. GitHub offers a `workflow_dispatch` workflow only once the file is on the default branch, so the workflow goes to `main` in a small pull request of its own, and it is merged only on Dan's approval. Until then nothing is measured.
