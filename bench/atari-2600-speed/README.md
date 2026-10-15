# Atari 2600 browser speed check

How many CPU cycles a second the Atari 2600 runs in headless Chrome, NTSC and PAL, compiled ahead of time (AOT) and in .NET WebAssembly's interpreter, with the bus, the RIOT and the TIA's picture and before the sound and the input. It is task 8 of [the Atari 2600 plan](../../docs/superpowers/plans/2026-10-14-atari-2600.md), the speed milestone, measured on a GitHub runner by a workflow started by hand and never on the development box. The rule, fixed before the first launch, is [`rule.md`](rule.md). The figures, with the run, the commit and the host, are in the journal entry [`docs/journal/2026-10-14-the-atari-2600-speed-milestone.md`](../../docs/journal/2026-10-14-the-atari-2600-speed-milestone.md). This file is how to run it again.

## What is here

| Path | What it is |
| --- | --- |
| `../../src/Dbhq.Machines.Atari2600.Wasm/` | The machine as a WebAssembly app. The bench calls `Load` (the cartridge as bytes, the region by name, "NTSC" or "PAL", and the sample rate; it returns a sentence naming the board, which the bench prints), `RunFrames`, `Run`, `Cycles`, `Frames`, `Lines`, `CpuHz` and `NominalLines`. It holds no cartridge. |
| `index.html`, `main.js` | The page. It fetches the program, runs 60 frames to settle, then times the runs. |
| `run-in-browser.mjs`, `package.json`, `package-lock.json` | Fetches the region's `harness/calibration` from the fork `dbhq-uk/missingno` at its pinned commit, checks the suite's manifest against its SHA-256 in `Pins.cs` and the file against its git blob hash in the manifest, serves it with the page and a published copy of the app on `127.0.0.1`, and runs it in a headless Chrome, a fresh launch each time. |
| `rule.md` | Items 9 and 10 of the plan's "Fixed before running", and what they leave open, fixed before the first launch. |
| `run-series.sh` | Runs the four launches the rule fixes, each behind the load gate, and writes the log `analyse.py` reads. |
| `analyse.py` | Reads the log and prints each launch's figures, its best and its median, then the `VERDICT` line. `--counts` says whether one launch counts; `run-series.sh` asks it, so what counts is written once. |
| [`../../.github/workflows/atari-2600-speed-run.yml`](../../.github/workflows/atari-2600-speed-run.yml) | The workflow that does the whole run on a GitHub runner. |

## The workload

The program is the suite's `harness/calibration`, the capture-rig target, which draws every line: `calibration_ntsc.a26` on NTSC and `calibration_pal.a26` on PAL. Both are 4K plain cartridges. It is never committed (`AGENTS.md` rule 3): the harness downloads it from the fork once into `.testdata/`, where `Atari2600Suite.Read` keeps it, and checks it every time it is read.

The machine is powered on and run for 60 frames to settle; that run is timed on its own and includes the runtime warming up. Then `Run` of one second of machine time, the region's `Region.CpuHz` read from the machine, is timed five times. Every line has the same form:

```
timed 1 cycles=<n> frames=<n> lines=<n> ms=<ms> cycles_per_second=<n>
```

`analyse.py` turns each into a multiple of real time by the clock the page printed from the machine, never a rate typed here. A launch counts only if each timed run ended at least one frame and its last frame had the region's `Region.NominalLines`, so a program that stopped drawing is not timed as the workload.

## Why it runs in CI at all

[AGENTS.md](../../AGENTS.md) keeps `bench/` out of CI, except speed workflows that are started by hand and never part of a pull request check, one per machine that needs one, each approved by Dan when it is built. The plan's task 8 measures this machine on a GitHub runner because the development box is an aarch64 virtual machine shared with other work, with no Chrome. So `atari-2600-speed-run.yml` has only a `workflow_dispatch` trigger. It never runs on a push or a pull request.

## Starting it

GitHub offers a `workflow_dispatch` workflow only once the file is on the default branch, so the workflow is on `main`, and it measures the branch it is told to:

```sh
gh workflow run atari-2600-speed-run.yml --repo dbhq-uk/6502 --ref main -f ref=feat/atari-2600
gh run watch --repo dbhq-uk/6502 "$(gh run list --repo dbhq-uk/6502 --workflow atari-2600-speed-run.yml --limit 1 --json databaseId -q '.[0].databaseId')"
```

The one input, `ref`, is the branch or commit to measure. The workflow builds that commit's `src/Dbhq.Machines.Atari2600.Wasm` and runs that commit's `bench/atari-2600-speed/`, so the harness and the rule are the measured commit's own.

## What the workflow does

1. Checks out the whole history, resolves `ref` to a commit and exports it with `git archive`.
2. Installs the .NET SDK and the `wasm-tools` workload, pinned as in `validate.yml`, and Node.
3. Records the host in `host.txt` before anything is timed: the run, the commit, the runner and its image, `nproc`, the load average, `uname`, the operating system, `lscpu`, memory, the Chrome, Node and .NET versions, and the workloads.
4. Publishes the app compiled ahead of time and in the interpreter, each after deleting `obj/Release` and `bin/Release`, then stops the build servers.
5. Runs `npm ci` here. The runner's own Chrome, `/usr/bin/google-chrome`, is used; nothing downloads a browser.
6. Runs `run-series.sh`: AOT NTSC, AOT PAL, interpreter NTSC, interpreter PAL, each behind the load gate.
7. Applies `analyse.py`, uploads everything in `out/` as the artefact `atari-2600-speed-run`, even when a step failed, and puts the analysis and the head of `host.txt` in the run's summary.

The job is green only on `CONTINUE` or `CONTINUE, HEADROOM`. `STOP` and `NONE` end red, so a red run is never read as a pass. One run at a time: a second run waits for the first.

## The artefact

| File | What it holds |
| --- | --- |
| `host.txt` | The host as recorded before the run, and the load average after it. |
| `launches.txt` | Every launch's whole output, between `=== launch <label> folder=<folder> start=<UTC> loadavg=<the five fields>` and `--- exit=<code> end=<UTC> loadavg=<the five fields>`, with the four busiest processes before it on a `--- top before:` line. |
| `watch.txt` | The load average and the four busiest processes every 15 seconds through the series. |
| `reruns.txt` | Only if a launch was run again: which, and on which try. |
| `analysis.txt` | `analyse.py` on `launches.txt`. |

## Running it on a host of your own

From the repository root, with the .NET 10 SDK and the `wasm-tools` workload, Node, and Chrome (`CHROME_PATH` if it is not at `/usr/bin/google-chrome`):

```sh
dotnet publish src/Dbhq.Machines.Atari2600.Wasm -c Release -o bench/atari-2600-speed/publish/interpreter
rm -rf src/Dbhq.Machines.Atari2600.Wasm/obj/Release src/Dbhq.Machines.Atari2600.Wasm/bin/Release
dotnet publish src/Dbhq.Machines.Atari2600.Wasm -c Release -p:RunAOTCompilation=true -o bench/atari-2600-speed/publish/aot

cd bench/atari-2600-speed
npm ci
node run-in-browser.mjs publish/aot 1 NTSC 5 60                 # folder, launches, region, timed runs, settle frames
./run-series.sh publish/aot publish/interpreter out              # the rule's four launches
python3 analyse.py out/launches.txt
```

`publish/` is git-ignored. A figure from a host of your own is that host's; the milestone's figure is the workflow's, and only a figure quoted with its run, its commit and its date describes the project (`AGENTS.md` rule 5).
