---
title: "The NES chip counters' speed, measured again on a quiet ARM machine"
date: 2026-10-13
summary: "The chip counters the NES models added to the bus were measured again, on the machine as it now is, after the lazy PPU made it faster. This time the host was an idle ARM machine and the launches agreed closely. By the rule fixed beforehand the result is a fail: in NTSC the build with the counters was a little under three per cent slower, and the whole interval lies below the limit. PAL passed. A profile shows the counter's own code takes almost none of the time; the slow launches are slower everywhere at once, as if the browser compiled the same program differently. Nothing in the machine was changed. The differential check shows the counters change no behaviour."
order: 38
---

# 13 October 2026: the NES chip counters' speed, on ARM

The NES models branch gives the bus a counter (`ChipAccesses`, four calls to `_accesses.Note(...)` in `src/Dbhq.Machines.Nes/NesBus.cs`). The claim is that it costs no more than 2 per cent of the machine's speed. [The run of 7 October](2026-10-07-the-nes-models-speed.md) passed that rule narrowly, on a busy host, against a `main` without the lazy PPU. The lazy PPU then made the machine about 1.3 times faster, so the same counter became a larger share. Issue #77 asked for the run again on an idle host. This is it.

**The host is ARM.** It is `dbhq-arm`: 4 cores, aarch64, Ubuntu 24.04, Chromium 153.0.8010.12 from Playwright (`CHROME_PATH`), .NET SDK 10.0.401 with the `wasm-tools` workload. The times-real-time figures below are not comparable with the x64 figures in the [bench README](../../bench/nes-speed/README.md)'s table. Only the ratio of the two builds, on the same host in the same minutes, is judged.

## The design, fixed before the first launch

Written at 06:14 UTC, before any launch, and committed unchanged as [`2026-10-13-arm-rule.md`](../../bench/nes-speed/chip-counters/2026-10-13-arm-rule.md). In short:

- **Baseline**: `origin/main` at `5640a57`, which is `git merge-base origin/main HEAD`, exported with `git archive`. **After**: `feat/nes-models` at `c0fdd2d`.
- Both published with `dotnet publish src/Dbhq.Machines.Nes.Wasm -c Release -p:RunAOTCompilation=true -o <folder>`, each from its own tree.
- Each launch: `THREAD_TIME=8 node run-in-browser.mjs <folder> 1 1790000 5000000 5 <region>`, one at a time, from the branch's `bench/nes-speed`.
- NTSC first, then PAL; in each, eight pairs, baseline then after.
- Before every launch the one-minute load average had to be under 1.00, or the script waited. A watch log sampled the four busiest processes every 15 seconds.
- The measure: the median of a launch's eight `thread` lines' `times_real`; the ratio after over baseline for each pair; per region, the median of the ratios and a 90 per cent percentile bootstrap interval of that median (10,000 resamples, seed 77).
- **The rule** (the same as on 7 October): pass if in each region the median ratio is at least 0.98 and the interval's lower end at least 0.96; fail if in any region the interval's upper end is below 0.98; otherwise inconclusive, with one more batch of eight pairs per region, judged on all sixteen, and then stop.
- Every launch is kept. One smoke launch of the after build checked the harness before the series; it was added to the rule before it ran, and is kept and not counted.

## What was measured

All on 13 October 2026. The 65 launches (the smoke launch and 64 timed) ran from 06:18:34 to 06:29:57 UTC. The load average before each launch was 0.80 to 0.99; at the end of a launch it was 0.80 to 2.45. In the 45 watch samples taken in that window, nothing but the bench's own Chrome used more than 10 per cent of a core, apart from one sample of Paseo at 12 per cent and one of Claude at 10. Every launch printed its eight `thread` lines and exited 0, so no pair was run again. Every line is in [`2026-10-13-arm-launches.txt`](../../bench/nes-speed/chip-counters/2026-10-13-arm-launches.txt), and [`analyse.py`](../../bench/nes-speed/chip-counters/analyse.py) turns it into [`2026-10-13-arm-analysis.txt`](../../bench/nes-speed/chip-counters/2026-10-13-arm-analysis.txt).

After the first eight pairs NTSC was inconclusive (median ratio 0.9732, interval 0.9666 to 0.9866) and PAL passed (0.9961, 0.9900 to 0.9995). So the rule's one extra batch was run, from 06:24:32 UTC. On all sixteen pairs:

| Region | Median ratio, after over baseline | Lowest to highest of the sixteen | 90 per cent interval of the median | Verdict |
| --- | --- | --- | --- | --- |
| NTSC | 0.9720 | 0.9600 to 1.0000 | 0.9666 to 0.9786 | fail: the upper end is below 0.98 |
| PAL | 0.9956 | 0.9646 to 1.0045 | 0.9873 to 0.9961 | pass |

The median of the launch medians, in times real time on this host: NTSC baseline 4.117, after 3.982; PAL baseline 4.510, after 4.490.

**By the rule fixed beforehand, this is a fail.** In NTSC, in Chromium on this ARM host, the build with the counters ran about 2.8 per cent slower, and the data rule out a slowdown smaller than about 2.1 per cent. In PAL the slowdown is about 0.4 per cent.

## The launches fall into two groups

The noise that made the 7 October run wide is gone: a baseline NTSC launch read 4.09 to 4.12 in fifteen of sixteen launches. The after build is different. Its NTSC launches read either 4.09 to 4.11, the same as the baseline (four of sixteen: pairs 2, 5, 10 and 13, and the smoke launch read 4.09 too), or 3.955 to 4.02, about 3 per cent slower (the other twelve). In PAL it is the other way round: nine after launches read 4.49 to 4.50, as the baseline does, three read 4.45 to 4.47, and four (pairs 3, 8, 11 and 13) read 4.355 to 4.435, up to 3.5 per cent slower. The baseline's PAL launches all read 4.48 to 4.52. Apart from the first run or two, which are slower in every launch of both builds, a launch's runs agree to about 1 per cent, so each launch settles into one speed and keeps it.

## The profile

The handover says to profile `ChipAccesses.Note` before any change to the machine. Both trees were published again with `-p:WasmNativeStrip=false`, and six pairs of NTSC launches were run with `PROFILE=40 PROFILE_OUT=<path> THREAD_TIME=8`, alternated, under the same load gate, from 07:06:20 to 07:14:40 UTC ([`2026-10-13-arm-profiles.txt`](../../bench/nes-speed/chip-counters/2026-10-13-arm-profiles.txt)). [`selftime.py`](../../bench/nes-speed/chip-counters/selftime.py) sums each profile's samples by function ([`2026-10-13-arm-selftime.txt`](../../bench/nes-speed/chip-counters/2026-10-13-arm-selftime.txt)).

- `ChipAccesses_Note_uint16_bool` is 0.00 to 0.03 per cent of the samples in every after launch. The counter's own code costs almost nothing, because SNOW touches the PPU and sound registers rarely compared with the cycles the machine runs.
- One after launch (pair 5) was slow, 3.725 against its baseline's 3.910. Its profile has 8,606 samples against its baseline's 8,032 for the same work, about 7 per cent more. The extra samples fall in several functions at once: the PPU's per-dot work (11 per cent more than its baseline), the CPU's read (15 per cent), the CPU's step (13 per cent), the sound unit (4 per cent). The bus's `Cycle` and the PPU's catch-up took the same. `Note` is not among them.
- The profiled launches themselves gave a median ratio of 0.9949 over six pairs; they are not part of the verdict, because the profiler changes the timing.

So the slowdown is not the counting. The likeliest cause is how the browser compiles the program: the after build's code is a little different (the `Note` calls in `ReadAccess` and `WriteAccess`), and in most NTSC launches V8 ends up with slower code for the whole loop. That fits both the two groups and the profile. It is a fit, not a proof: V8's tiering was not traced.

The first try at this profile is kept too ([`2026-10-13-arm-profiles-stripped.txt`](../../bench/nes-speed/chip-counters/2026-10-13-arm-profiles-stripped.txt), twelve launches from 06:48:17 UTC). The publish with `-p:WasmNativeStrip=false` reused the stripped `dotnet.native.wasm` from the earlier publish (the same file hash), so the profile had no function names. Deleting `src/Dbhq.Machines.Nes.Wasm/obj/Release` and `bin/Release` in both trees and publishing again gave a file with names (16 MB against 5.7 MB).

## The native instruction count

Declared in the rule as supplementary, not part of the verdict. `perf stat -e instructions:u` counted the native program's instructions (the .NET JIT on ARM, not WebAssembly) for 0 and for 20 timed runs of 1,790,000 cycles, in five alternated pairs per region. The difference over 35.8 million cycles gives instructions per emulated cycle ([`2026-10-13-arm-perf.txt`](../../bench/nes-speed/chip-counters/2026-10-13-arm-perf.txt); every run printed `picture changes yes`):

| Region | After over baseline, median of five | Lowest to highest |
| --- | --- | --- |
| NTSC | 1.0057 | 1.0027 to 1.0088 |
| PAL | 1.0073 | 1.0003 to 1.0719 |

The count includes every thread, the JIT's among them, which explains the one PAL outlier. Natively, the counters cost well under 1 per cent of the instructions.

**A host setting was changed and put back.** `kernel.perf_event_paranoid` was 4. It was set to 1 with `sudo sysctl` at 07:15:33 and back to 4 at 07:15:34 UTC, and again from 07:15:44 to 07:17:45 UTC. The first session's numbers were wrong and are not used: the program's app host could not find .NET without `DOTNET_ROOT` and exited at once, so every count was about 1.76 million instructions. Its output was overwritten by the second session's and is not kept.

## The differential check

Run after the timing, on each tree, with the same tool (`bench/nes-speed/differential/` is the same in both):

```sh
cd /tmp/nes77/base/bench/nes-speed && dotnet run -c Release --project differential -- /tmp/nes77/diff-base.txt
cd <worktree>/bench/nes-speed     && dotnet run -c Release --project differential -- /tmp/nes77/diff-after.txt
cmp /tmp/nes77/diff-base.txt /tmp/nes77/diff-after.txt
```

Each wrote 332 runs and 90,387 bytes; `cmp` printed nothing. The files are identical (SHA-256 `5c68068807f2895393fb9ac795edadd1c4f4c6bcaad582d23e2e253f8ebd3722`). The counters change nothing a pinned test ROM, the homebrew or the synthetic cartridges can see, in either region.

## What was decided, and what it was chosen over

- **The machine's code was not changed.** The brief said: on a slowdown over 2 per cent, profile and tell Dan before changing anything. Two changes could remove the slowdown: count only while a model is mounted, or count in the lazy catch-up instead of in `ReadAccess` and `WriteAccess`. Since the counting itself is nearly free, either would work only by changing the code V8 compiles, and that has to be measured, not assumed.
- **The rule was kept as written.** NTSC failed by its upper end, 0.9786 against 0.98. It was not loosened after the result was seen, and no launch was dropped.
- **The supporting files are committed** in `bench/nes-speed/chip-counters/`, unlike 7 October's report, which was kept outside the repository.

## What was got wrong, or left open

- The x64 question is open. V8 compiles WebAssembly separately for each processor, so the two speed groups on ARM may not appear on x64, where most of the site's visitors are. A run on an idle x64 host with the same rule would settle it.
- Why one launch settles into the slow code and another does not was not traced.
- The order inside each pair was always baseline then after. With launches this steady, drift would show as a trend across pairs, and none shows, but an alternated order would rule it out.

## The checks before this was pushed

On 13 October 2026, on `dbhq-arm`, one at a time, on this change (documents and bench files only; no code changed): `dotnet build -c Release 6502.slnx`, 0 warnings and 0 errors; `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --no-build`, 1661 passed and 0 failed; in `site/`, `node scripts/build-machines.mjs nes bbc-micro kim-1` and `npm test`, 476 tests, 475 passed, 0 failed and 1 to-do (the BBC Micro's, already there); `node scripts/browser-check.mjs` with `CHROME_PATH` set, exit 0, no console errors, no failed requests and no CSP violations, from 07:36 to 07:43 UTC at a load average of 3.13 at its start.
