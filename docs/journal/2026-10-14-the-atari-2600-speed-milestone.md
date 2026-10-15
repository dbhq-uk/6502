---
title: "The Atari VCS's speed milestone: the rule, the bench and the workflow"
date: 2026-10-14
summary: "The eighth task of the Atari VCS: the machine as WebAssembly, a browser bench that times the suite's calibration program in both regions, compiled ahead of time and in the interpreter, the rule that judges it, committed before any launch, and a workflow started by hand that runs it on a GitHub runner and never on this box. Its first run, judged by that rule, says stop: Dan decides before the sound is built."
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

`AGENTS.md` allows a speed workflow started by hand and never part of a pull request check, one per machine that needs one, each approved by Dan when it is built. GitHub offers a `workflow_dispatch` workflow only once the file is on the default branch, so the workflow goes to `main` in a small pull request of its own, and it is merged only on Dan's approval. Dan approved the workflow and its rule as they stood on 14 October 2026, and pull request #170 was merged (`d88b7eb`).

## The run, and the verdict: STOP

Run [38634166609](https://github.com/dbhq-uk/6502/actions/runs/38634166609), started on 14 October 2026 at 20:41 UTC with

```sh
gh workflow run atari-2600-speed-run.yml --repo dbhq-uk/6502 --ref main -f ref=7812989b87dffa901c7f9216c61d3d1d96aa8edf
```

The ref was given as the commit, the head of `feat/atari-2600` when the run started, so a push to the branch during the run could not change what was measured.

**The host**, from the artefact's `host.txt`, recorded at 20:42:43 UTC before anything was timed: a GitHub-hosted runner, image `ubuntu24` 20261004.327.1, Ubuntu 24.04.5 LTS, Linux 6.17.0-1022-azure, x86_64, an AMD EPYC 9V45 96-Core Processor under Microsoft's hypervisor, 4 CPUs; Chrome 154.0.8037.97. The load gate let each launch through with the one-minute load under 1.00 (0.97, 0.94, 0.94 and 0.87 at the four starts). Every launch counted on its first try.

**The figures**, as `analyse.py` printed them in the artefact's `analysis.txt`, times real time from `Region.CpuHz`:

| Launch | The five timed runs | Best | Median |
|---|---|---|---|
| AOT, NTSC | 8.47 8.31 8.36 8.40 8.48 | 8.48 | 8.40 |
| AOT, PAL | 8.10 8.30 8.15 8.22 8.22 | 8.30 | 8.22 |
| Interpreter, NTSC | 0.59 0.57 0.58 0.58 0.58 | 0.59 | 0.58 |
| Interpreter, PAL | 0.59 0.59 0.58 0.58 0.58 | 0.59 | 0.58 |

The lower AOT median is 8.22 times real time, under the rule's line of 10, so **the verdict is STOP**, and the run ended red as the rule says it must. Each timed run ended 59 or 60 frames of 262 lines on NTSC and 49 or 50 of 312 on PAL, so the program was drawing throughout. The interpreter runs under real time, about 0.58 times, so in the interpreter the machine cannot keep up at all.

**What follows.** The plan's step 6: under 10 times, stop and tell Dan, with the figure and the run's link, before task 10 (the sound) is built. The spec already plans the speed pass after the count (issue #130), as the NES's lazy chips came after its machine counted, so the expected answer is to go on and do the speed pass later; it is Dan's call, and the sound (task 10) is not started on a STOP verdict without him. Dan decided on 14 October 2026 to carry on, with the speed pass after the count (#130). The rule is not loosened, and no launch is dropped. For comparison only, and from another day, another host and another program: the NES's own milestone, measured on 5 October 2026 on the development box while it was busy, gave an AOT median of 1.94 times real time on NTSC and 1.71 on PAL in the better of its two sets, as its entry, [the NES speed](2026-10-05-the-nes-speed.md), records under "What this means for the rule".

## The review, and one fix to `analyse.py`

Task 8's review found that `analyse.py` printed each median to two decimal places but judged the unrounded value, so a median of 24.9999 printed as 25.00 and was judged CONTINUE, HEADROOM, and one of 9.9999 printed as 10.00 and was judged STOP. Test first: two synthetic launches files in a scratch folder outside the repository, with AOT medians of 24.9999 and 9.9999, gave `25.00` with `CONTINUE, HEADROOM` and `10.00` with `STOP` before the fix (`python3 -I bench/atari-2600-speed/analyse.py <file>`, 14 October 2026). Now each AOT median is rounded to the two places it is printed with before it is judged, and the same files give `CONTINUE` and `CONTINUE, HEADROOM`. The first run's verdict does not change: its lower median, 8.22, is the same rounded. `rule.md` keeps its text as committed before the launch, with a dated pointer to this entry added.

## The run again after the sound, and a control

The plan's task 10 asks for this workflow to be run again once the sound is built, and its figure recorded beside the first. Dan had already decided to carry on past the STOP; the run is a record, and the verdict line is printed by the same rule. Two runs were started by hand on 14 October 2026, each given a commit:

```sh
gh workflow run atari-2600-speed-run.yml --repo dbhq-uk/6502 --ref main -f ref=0ea88f29e7200a36e1c36d450a1579f9c27cc6dd
gh workflow run atari-2600-speed-run.yml --repo dbhq-uk/6502 --ref main -f ref=d34110845828d48aafc1e40e26ea07d991b94242
```

- **With the sound**: run [38646805950](https://github.com/dbhq-uk/6502/actions/runs/38646805950), commit `0ea88f2` (task 10's head), host recorded at 22:19:51 UTC.
- **A control, without it**: run [38647501100](https://github.com/dbhq-uk/6502/actions/runs/38647501100), commit `d341108` (task 9's head, the last before the sound), host recorded at 22:25:47 UTC. It was not asked for. It was started because the first run landed on a different processor from task 8's, so its figure alone could not say what the sound cost.

Both hosts were GitHub-hosted runners, image `ubuntu24` 20261012.334.1, Ubuntu 24.04.5 LTS, 4 CPUs, an **AMD EPYC 7763** under Microsoft's hypervisor; task 8's run had an AMD EPYC 9V45. The load gate let every launch through with the one-minute load under 1.00, and every launch counted on its first try. The figures, times real time, as `analyse.py` printed them in each artefact's `analysis.txt`:

| Launch | Task 8, `7812989`, EPYC 9V45 (median) | Control, `d341108`, EPYC 7763: five runs; median | With the sound, `0ea88f2`, EPYC 7763: five runs; median |
|---|---|---|---|
| AOT, NTSC | 8.40 | 4.80 4.82 4.52 4.76 4.80; 4.80 | 4.53 4.55 4.51 4.52 4.52; 4.52 |
| AOT, PAL | 8.22 | 4.89 4.86 4.89 4.88 4.90; 4.89 | 4.69 4.65 4.74 4.79 4.73; 4.73 |
| Interpreter, NTSC | 0.58 | 0.24 0.24 0.25 0.24 0.24; 0.24 | 0.24 0.24 0.23 0.23 0.23; 0.23 |
| Interpreter, PAL | 0.58 | 0.25 0.25 0.25 0.25 0.25; 0.25 | 0.25 0.25 0.25 0.25 0.25; 0.25 |

**With the sound, the lower AOT median is 4.52 times real time, and the verdict is STOP**, as it was. The fall from 8.22 to 4.80 is not shown to be the host: the control (`d341108`) also holds task 9's code, and task 8's commit `7812989` was never run on an EPYC 7763, so that fall mixes the processor and task 9. Only the step from 4.80 to 4.52, on the same processor model, is the sound. Against the control, NTSC is not separated from the spread: the control's own NTSC runs span 4.52 to 4.82, and the sound's median, 4.52, equals the control's lowest run. PAL is about 3 per cent slower with the sound (4.89 to 4.73). That is one run of each, on two runners of the same type. The speed pass (#130) starts from these figures. (Corrected on 14 October 2026 by task 10's review: this paragraph first said most of the fall from 8.22 was the host and that the sound cost about 6 per cent on NTSC; neither was shown.)
