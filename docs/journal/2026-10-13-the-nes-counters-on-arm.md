---
title: "The NES chip counters' speed, measured again on a quiet ARM machine"
date: 2026-10-13
summary: "The chip counters the NES models added to the bus were measured again on an idle ARM machine, after the lazy PPU made the machine faster. By the rule fixed beforehand the first result is a fail: in NTSC the build with the counters was a little under three per cent slower. A profile showed the counter's own code takes almost none of the time, so two variants were tried that change only how the bus counts. Both passed in both regions, in a run with the original as a control that showed the slowdown again. The smaller variant, plain field increments in the bus with no call, is now in the branch. The differential check shows the counters, in every version, change no behaviour."
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

- **The machine's code was not changed until Dan chose.** The brief said: on a slowdown over 2 per cent, profile and tell Dan before changing anything. He chose to try a variant, and A is the result (below). The two variants tried are not the two this entry first named (count only while a model is mounted, or count in the lazy catch-up); they change the same thing, the code V8 compiles for the access, with a smaller change to the machine. Since the counting itself is nearly free, any such change works only by changing that code, and has to be measured, not assumed.
- **The rule was kept as written.** NTSC failed by its upper end, 0.9786 against 0.98. It was not loosened after the result was seen, and no launch was dropped.
- **The supporting files are committed** in `bench/nes-speed/chip-counters/`, unlike 7 October's report, which was kept outside the repository.

## What was got wrong, or left open

- The x64 question is open. V8 compiles WebAssembly separately for each processor, so the two speed groups on ARM may not appear on x64, where most of the site's visitors are. A run on an idle x64 host with the same rule would settle it.
- Why one launch settles into the slow code and another does not was not traced.
- The order inside each pair was always baseline then after. With launches this steady, drift would show as a trend across pairs, and none shows, but an alternated order would rule it out.

## The variants, and the second run

Dan chose, on 13 October 2026, to try a variant of the counter and measure it the same way. Two were made, in scratch worktrees (their diffs are [`2026-10-13-variant-A.diff`](../../bench/nes-speed/chip-counters/2026-10-13-variant-A.diff) and [`2026-10-13-variant-B.diff`](../../bench/nes-speed/chip-counters/2026-10-13-variant-B.diff)):

- **A**: four plain `uint` fields on `ChipAccesses`, and the bus adds to them in its own decode (`_accesses.Ppu++` and so on). No call, no array, no address decode. `Note` stays, for the tests, and gives the same count by address.
- **B**: the PPU counts the CPU's reads and writes of its registers in `ReadRegister` and `WriteRegister`, so the bus's PPU branches are as on `main`; the sound and pad counts stay in the bus.

The design and the rule were written before either was built, in [`2026-10-13-arm-variants-rule.md`](../../bench/nes-speed/chip-counters/2026-10-13-arm-variants-rule.md): four builds (`main`, the branch as it was, A, B), rounds of four launches in a rotating order, each build over the same round's `main` launch, the same pass rule applied to each of the three on its own, and the choice fixed too (a variant goes in only if it passes; if both pass, the higher NTSC median, and within 0.005 of each other, A as the smaller change). Before any launch, each variant passed all 1661 NES tests and wrote a differential file byte-identical to `main`'s (SHA-256 `5c68068807f2895393fb9ac795edadd1c4f4c6bcaad582d23e2e253f8ebd3722`).

**The first try was invalid and is kept.** Three launches ran from 09:25:37 to 09:26:05 UTC while another job's differential check (`faults.py`, not mine) was using about two cores. The one-minute load average had not caught up, so my gate let them start. I stopped them, kept them in [`2026-10-13-arm-variants-aborted.txt`](../../bench/nes-speed/chip-counters/2026-10-13-arm-variants-aborted.txt) marked invalid, and added a second condition to the gate before any valid launch: wait while any `Dbhq.Machines.Nes.Differential` process runs. My own `pkill -f` matched my shell twice while I stopped them, which left the first restart half-started; no launch from that counts. A wait for the other job ran out its two-hour limit without a launch.

**The valid run** ran from 11:31:19 to 11:40:16 UTC, with 64 launches (eight rounds of four, both regions), all exit 0, the load average before each launch 0.00 to 0.97, and nothing but the bench's own Chrome (and one sample each of a Ruby and a Claude process) above 10 per cent of a core in the 37 watch samples ([`2026-10-13-arm-variants-analysis.txt`](../../bench/nes-speed/chip-counters/2026-10-13-arm-variants-analysis.txt) has every round):

| Build | Region | Median ratio over `main` (eight rounds) | 90 per cent interval | Verdict |
| --- | --- | --- | --- | --- |
| the branch as it was (control) | NTSC | 0.9750 | 0.9660 to 0.9915 | inconclusive |
| the branch as it was (control) | PAL | 0.9806 | 0.9657 to 0.9950 | pass |
| A | NTSC | 0.9976 | 0.9957 to 0.9994 | pass |
| A | PAL | 0.9989 | 0.9967 to 0.9994 | pass |
| B | NTSC | 0.9951 | 0.9933 to 0.9970 | pass |
| B | PAL | 0.9939 | 0.9878 to 0.9967 | pass |

**A passes, and so does B.** The control matters: the branch as it was ran about 2 to 4 per cent slower than `main` in six of eight NTSC rounds and in five of eight PAL rounds, so the slowdown of the first run came back in the same session, and A and B did not show it. By the choice fixed beforehand, A goes in: its NTSC median is higher than B's, and the two are within 0.005.

**What this does and does not show.** A passes the rule on this host, in Chromium on ARM. It does not show why the call cost three per cent when the profile put it under 0.1 per cent of the samples. The likeliest cause, still a fit and not a proof, is that the extra call in `ReadAccess` and `WriteAccess` changes what V8 compiles for the whole loop, and plain increments leave that code as it was. Any change moves the program's layout, so a pass says this build is not slower here, not that the layout cannot matter on x64. The x64 run is still open. The native instruction count was not repeated for A; the differential file of A is the same as `main`'s.

A is in the branch as `ChipAccesses.cs` and `NesBus.cs`, with the class's comments corrected to say the bus counts directly. The code is the measured code; only the doc comments differ from the measured build.

## The checks before this was pushed

On 13 October 2026, on `dbhq-arm`, one at a time, on this change (documents and bench files only; no code changed): `dotnet build -c Release 6502.slnx`, 0 warnings and 0 errors; `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --no-build`, 1661 passed and 0 failed; in `site/`, `node scripts/build-machines.mjs nes bbc-micro kim-1` and `npm test`, 476 tests, 475 passed, 0 failed and 1 to-do (the BBC Micro's, already there); `node scripts/browser-check.mjs` with `CHROME_PATH` set, exit 0, no console errors, no failed requests and no CSP violations, from 07:36 to 07:43 UTC at a load average of 3.13 at its start.

## The checks after variant A went in

On 13 October 2026, on `dbhq-arm`, one at a time, with A in the tree: `dotnet build -c Release 6502.slnx`, 0 warnings and 0 errors; `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --no-build`, 1661 passed and 0 failed; in `site/`, `node scripts/build-machines.mjs nes bbc-micro kim-1` and `npm test`, 476 tests, 475 passed, 0 failed and 1 to-do (the BBC Micro's, already there). The differential file of A (332 runs) is byte-identical to `main`'s, from before the comments were corrected; the code is the same apart from doc comments (checked with `diff` after removing the comment lines).

**The whole browser check failed once, then passed.** The first run, from 11:42 UTC, exit 1: "phone: a finger on the focused model scrolled the page", in the KIM-1's part, the step issue #80 is about, in code this change does not touch (the output is kept outside the repository). The load average at its start was about 0.8 and its 5-minute average 3.3, from my own build and test just before. The second run, from 11:57 UTC at a load average of 0.77, exit 0: no console errors, no failed requests, no CSP violations. So #80's step has now also failed once on a host that was not busy; that is noted on the issue. It is not a result of this change, but two runs do not prove that, and it is said so.
