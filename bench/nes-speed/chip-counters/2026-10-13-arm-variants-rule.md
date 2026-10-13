# Issue 77, second run: two variants of the counter, the design and the rule, fixed before the first launch

Written 2026-10-13 08:39 UTC, after Dan chose "try a variant", before any variant was built or launched.

Host and method as in 2026-10-13-arm-rule.md (dbhq-arm, Chromium 153.0.8010.12, AOT, THREAD_TIME=8, `node run-in-browser.mjs <folder> 1 1790000 5000000 5 <region>`, load under 1.00 before every launch, every launch kept).

- Builds, four: **base** `origin/main` 5640a57 (the same published folder as the first run); **cur** `feat/nes-models` 6319ce1, the counters as they are (a control: does the slowdown show again today); **A** cur with the counting done by plain field increments in the bus's decode, no call and no array; **B** cur with the PPU counting the CPU's accesses to its registers in its own register handlers, so the bus's PPU decode is as on main (the sound and pad counts stay in the bus's $4000-$401F decode).
- Before any launch, each variant must pass `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release`, and its differential output must be byte-identical to the baseline's (`cmp` against /tmp/nes77/diff-base.txt). A variant that fails either is dropped, and the run goes on without it.
- Rounds of four launches, one of each build, in a rotating order: round i uses order i mod 4 of (base, cur, A, B), (cur, A, B, base), (A, B, base, cur), (B, base, cur, A). Eight rounds in NTSC, then eight in PAL. Ratios are each build over the base launch of the same round.
- The rule, applied to each of cur, A and B on its own, the same as before: PASS if in each region the median ratio >= 0.98 and the 90 per cent bootstrap interval's lower end >= 0.96 (10,000 resamples, seed 77); FAIL if in any region the upper end < 0.98; otherwise INCONCLUSIVE. If A or B is inconclusive, one more batch of eight rounds per region for all four builds, judged on all sixteen, then stop.
- The choice: a variant goes into pull request 76 only if it passes. If both pass, the one with the higher NTSC median ratio; if those are within 0.005 of each other, A, the smaller change. If neither passes, nothing is changed and Dan decides.
- What a pass does not prove: any change moves the whole program's layout, so a pass shows this build is not slower on this host, not why. That is said in the journal whatever the result.
- Added 2026-10-13 09:31 UTC, before any valid launch: a launch also waits while any Dbhq.Machines.Nes.Differential process is running (another job's check made the first try invalid).
