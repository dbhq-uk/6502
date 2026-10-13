# The BBC Micro's chip counters: running the speed rule

How to run the browser speed A/B that [`rule.md`](rule.md) fixes, by hand on a GitHub runner or on a host of your own. The rule and [`analyse.py`](analyse.py) are not changed by anything here.

## What is here

| File | What it is |
| --- | --- |
| [`rule.md`](rule.md) | The design and the pass rule, fixed before any launch. |
| [`analyse.py`](analyse.py) | Reads a launches file and prints each case's ratios, interval and verdict, then the `VERDICT` line. |
| [`run-series.sh`](run-series.sh) | Runs the launches as the rule says: the load gate, the order of each pair, the log format, what counts, and a pair run again when a launch in it does not count. |
| [`../../../.github/workflows/bbc-speed-run.yml`](../../../.github/workflows/bbc-speed-run.yml) | The workflow that does the whole run on a GitHub runner. |

## Why it runs in CI at all

[AGENTS.md](../../../AGENTS.md) keeps `bench/` out of CI. The rule needs a quiet host with Chrome, and the box task 6 was built on has neither. Dan approved one exception on 13 October 2026: "in CI, as a manually triggered (workflow_dispatch) one-off workflow, not part of any PR check". So the workflow has only a `workflow_dispatch` trigger. It never runs on a push or a pull request, and it is not a check on any pull request.

## Starting it

GitHub only offers a `workflow_dispatch` workflow once the file is on the default branch. Until then, `gh workflow run` refuses it. Once it is on `main`:

```sh
gh workflow run bbc-speed-run.yml --repo dbhq-uk/6502 --ref main -f after=feat/bbc-micro-models-2
gh run watch --repo dbhq-uk/6502 "$(gh run list --repo dbhq-uk/6502 --workflow bbc-speed-run.yml --limit 1 --json databaseId -q '.[0].databaseId')"
```

The inputs:

- `after`: the commit or branch with the counters. Default `feat/bbc-micro-models-2`. Its `bench/bbc-micro-speed` is the harness for both builds.
- `base`: the baseline. Empty by default, which means the rule's baseline, `git merge-base origin/main <after>`. Give a commit only to repeat an earlier run.

The script and `analyse.py` are taken from the ref the workflow is started from, so start it from a ref that has this folder.

## What the workflow does

1. Checks out the whole history, resolves both refs to commits and exports each with `git archive` to a tree of its own.
2. Installs the .NET SDK and the `wasm-tools` workload, pinned as in `validate.yml`, and Node.
3. Records the host in `host.txt` before anything is timed: the run, both commits, the runner and its image, `nproc`, the load average, `uname`, the operating system, `lscpu`, memory, the Chrome, Node and .NET versions, and the workloads.
4. Publishes each tree's `src/Dbhq.Machines.BbcMicro.Wasm` compiled ahead of time, after deleting its `obj/Release` and `bin/Release`, then stops the build servers.
5. Runs `npm ci` in the after tree's `bench/bbc-micro-speed`. The runner's own Chrome, `/usr/bin/google-chrome`, is used; nothing downloads a browser.
6. Runs the smoke launch, then pairs 1 to 8 of mode 7 and then of dense, and applies `analyse.py`.
7. If the verdict is INCONCLUSIVE, runs pairs 9 to 16 in the same order and applies `analyse.py` to all sixteen. Then it stops, whatever that says.
8. As a supplement, not part of the verdict, runs both trees' native fingerprints and compares them.
9. Uploads everything in `out/` as the artifact `bbc-speed-run`, even when a step failed, and puts the final analysis and the head of `host.txt` in the run's summary.

The job is green only on PASS. FAIL, INCONCLUSIVE after sixteen pairs, or no verdict all end red, so a red run is never read as a pass. One run at a time: a second run waits for the first.

## The load gate on a shared runner

A GitHub-hosted runner is a virtual machine on shared hardware. The rule's gate applies as written: before every launch the one-minute load average in `/proc/loadavg` must be under 1.00, or the script waits, polling every 15 seconds. No launch starts above the limit, so no launch is dropped for its load. If one wait passes `GATE_MAX_SECONDS` (default 1800), the series stops with an error instead of waiting out the job's timeout; the launches so far stay in the log.

The load average sees only this virtual machine. Neighbours on the same hardware do not show in it, and this is the main thing the run cannot rule out. The pairs alternate their order so that drift falls on both builds, and the ratio of the two builds on the same host in the same minutes is all the rule judges.

## The artifact

| File | What it holds |
| --- | --- |
| `host.txt` | The host as recorded before the run, and the load average after it. |
| `launches.txt` | Every launch's whole output, between `=== launch <label> folder=<folder> screen=<screen> start=<UTC> loadavg=<the five fields>` and `--- exit=<code> end=<UTC> loadavg=<the five fields>`, with the four busiest processes before it on a `--- top before:` line. |
| `watch.txt` | The load average and the four busiest processes every 15 seconds through the series. |
| `reruns.txt` | Only if a pair was run again: which, and on which try. |
| `analysis-pairs-1-8.txt` | `analyse.py` on the first eight pairs. |
| `analysis-pairs-1-16.txt` | Only if the first eight were inconclusive: `analyse.py` on all sixteen. |
| `fingerprint-base.txt`, `fingerprint-after.txt`, `fingerprints.txt` | The supplementary native fingerprints and whether they are the same. |

## Running it on a host of your own

The same script runs anywhere with the .NET 10 SDK and `wasm-tools`, Node and Chrome (`CHROME_PATH` if it is not at `/usr/bin/google-chrome`). Publish both trees as the workflow does, then from the after tree's `bench/bbc-micro-speed`, after `npm ci`:

```sh
chip-counters/run-series.sh smoke <after-folder> <out>
chip-counters/run-series.sh pairs <base-folder> <after-folder> <out> 1 8
python3 chip-counters/analyse.py <out>/launches.txt
```

If the verdict is INCONCLUSIVE, run `pairs ... 9 16` and `analyse.py` again. `LOADAVG_FILE` points the gate at a file other than `/proc/loadavg`; it exists only to try the script without a quiet host, and is never set for a run that counts.

## What a result means

A result is recorded as `analyse.py` prints it, with `host.txt`. A FAIL is recorded and Dan is told; the machine is not changed to pass without his say, and the rule is not loosened after a result is seen. [`rule.md`](rule.md), "What a pass does not show", says what even a pass does not show.
