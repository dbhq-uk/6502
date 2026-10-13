# The BBC Micro's chip counters: the browser speed rule, fixed before the first launch

This is the design and the pass rule for timing the BBC Micro with and without the chip counters that task 6 of the [models plan](../../../docs/superpowers/plans/2026-10-04-bbc-micro-models.md) adds to its bus. It is the NES's rule ([`../../nes-speed/chip-counters/2026-10-13-arm-rule.md`](../../nes-speed/chip-counters/2026-10-13-arm-rule.md)) with the BBC Micro's two cases in place of the NES's two regions.

Written 2026-10-13 at 20:43 UTC, before any launch. No launch has run.

## The host

The host is not chosen. It is Dan's to name (the plan's "Decisions that are Dan's", item 2). The box task 6 was built on is ruled out: it is aarch64 and shared, and it has no Chrome. Until a host is named and the run is made, the speed run is owed, and the pull request does not merge without it. Whoever runs it records the host first: its processor, cores, operating system, the browser and its version, and the .NET SDK with its `wasm-tools` workload.

## The design

- **Baseline:** `git merge-base origin/main <after>`, exported with `git archive` to a folder of its own. **After:** the branch's commit with the counters, named by its hash when the run starts.
- **Builds:** both with `dotnet publish src/Dbhq.Machines.BbcMicro.Wasm -c Release -p:RunAOTCompilation=true -o <folder>`, each from its own tree, after deleting that tree's `src/Dbhq.Machines.BbcMicro.Wasm/obj/Release` and `bin/Release`.
- **Harness:** this branch's `bench/bbc-micro-speed`, `npm ci` once, for both builds.
- **Launch:** `THREAD_TIME=8 node run-in-browser.mjs <folder> 1 2000000 6000000 5 7 <screen> none`, one at a time, where `<screen>` is `boot` for the case **mode 7** and `dense` for the case **dense**. A launch counts only if it printed `launch 1 prompt yes`, and for dense `launch 1 screen dense`, and eight `thread` lines.
- **Order:** mode 7 first, then dense. In each case, eight pairs. Pair 1, 3, 5 and 7 launch the baseline first; pairs 2, 4, 6 and 8 launch the after build first, so drift in the host cannot favour one build.
- **Gate:** before every launch the one-minute load average must be under 1.00; if not, wait, polling every 15 seconds. The busiest processes are logged before every launch, and a watch log samples them every 15 seconds throughout.
- **Log:** every launch's whole output goes to one launches file, each between a line `=== launch <label> folder=<folder> start=<UTC> loadavg=<the five fields of /proc/loadavg>` and a line `--- exit=<code> end=<UTC> loadavg=<the same>`, the label being `<case>-p<pair>-<base|after>` with the case `mode7` or `dense`. [`analyse.py`](analyse.py) reads that file.

## The measure

- Per launch, the median of its eight `thread` lines' `mhz` (the main thread's CPU time, which moves less with the host's load than the wall clock).
- Per pair, the ratio after over baseline.
- Per case, the median of the ratios and a 90 per cent percentile bootstrap interval of that median: 10,000 resamples, seed 88.

## The rule

- **PASS** if in each case the median ratio is at least 0.98 and the interval's lower end is at least 0.96.
- **FAIL** if in either case the interval's upper end is below 0.98.
- **INCONCLUSIVE** otherwise. Then one more batch of eight pairs in each case, in the same order, judged on all sixteen, and then stop whatever it says.

A FAIL is recorded and Dan is told. The machine is not changed to pass without his say. The rule is not loosened after a result is seen, and no launch is dropped.

## Every launch is kept

A launch that does not count (no prompt, no dense page, or fewer than eight `thread` lines) stays in the log, is reported, and its pair is run again. One smoke launch of the after build in mode 7 checks the harness on the host before the series; it is kept in the log and not counted.

## Supplementary, not part of the verdict

- The native fingerprint (`dotnet run -c Release --project bench/bbc-micro-speed/native -- --fingerprint`) of both trees, compared with `cmp`.
- The host's load before and after the series, and the watch log's busiest processes.

## What a pass does not show

Any change to the machine moves the whole program's layout, so a pass shows that this build is not slower on that host, not why, and not that it is not slower on another processor. The NES found its first counter about three per cent slower on ARM in one region with almost none of the profile in the counter itself ([the journal for 13 October 2026](../../../docs/journal/2026-10-13-the-nes-counters-on-arm.md)). This rule is applied to the counters as built, which count the NES's faster way, with plain fields and no call.
