---
title: "What was left of the BBC Micro's robustness items"
date: 2026-10-15
summary: "The second pass at the issue of small items from the BBC Micro's reviews. Every bullet was checked against main again. The CRTC's remarks now name the four registers that read back, where they named six, and the equivalence test on the machine's clock compares the chip's outputs as well as its events, so the CRTC's outputs are checked at one megahertz for the first time. The five wording nits from one review still cannot be identified, so the issue stays open. No behaviour changed."
order: 62
---

# 15 October 2026: what was left of the BBC Micro's robustness items

[Issue #44](https://github.com/dbhq-uk/6502/issues/44) collects the small items left from the BBC Micro's reviews. [The first pass](2026-10-13-the-bbc-micro-robustness.md) (pull request #94) did most of it. This entry is the second pass: each bullet was checked against `main` (`d88b7eb`) again rather than taken from the issue's list.

## Where each bullet stands

| Bullet | State on `main` |
| --- | --- |
| `Via6522._catchingUp` reset in a `finally` | Done in #94. `CatchUp` clears the guard in a `finally`. |
| `SystemVia.VsyncInput` public | Done in #94. It is `internal`. |
| `BbcBus.Irq` public | Done in #94. It is `internal`. |
| CRTC equivalence: the cycle each event carries, and the handler-writes-the-chip throw | Done in #94. |
| CRTC equivalence: a 1 MHz mode | Half done in #94, finished here (below). |
| The sticky SHIFT latch and the "Press Start first" message | Done in `6e55f93`, before #94. |
| The five wording nits from the task 14b review | Still open. They cannot be identified (below). |
| Dan's comment on the issue: the CRTC's remarks say "R12 to R17 read back" | Done here (below). |

## Done

**The CRTC's remarks.** The class remarks in `Crtc6845.cs` said "R12 to R17 read back" and then that R16 and R17 read 0. `ReadData` returns R12 to R15 and 0 for everything else, and its own summary already said so. The remarks now say "R12 to R15 read back". The test `OnlyR12ToR17ReadBack` was renamed `OnlyR12ToR15ReadBack` to match; its assertions were already right. The journal entry of 2 October that says R12 to R17 was left alone, because it is a record of that day.

**The CRTC's outputs at 1 MHz.** The task 7 review asked for a 1 MHz mode in the CRTC equivalence test. The main equivalence test runs the chip on its own, where it counts one character per `Tick` and has no 1 MHz mode. Pull request #94 added `EventsCarryTheCycleTheOraclesCharacterFallsIn`, which runs the chip on the machine's clock at 1 MHz (on an even and an odd cycle) and at 2 MHz, but it compares only the events: each VSYNC edge and frame start, and the cycle each carries. Nothing compared the chip's outputs (MA, RA, DISPTMG, HSYNC, VSYNC, CUDISP, the line start and the vertical display) with the oracle while the chip counted its characters from the clock.

- **Chosen:** the same test now compares every output with the oracle at each look, before it compares the events. The test's summary says so. This was chosen over a new test with its own copy of the loop, which would be a hundred lines repeated to add one check.
- **Shown to matter.** The chip was changed so that at 1 MHz a character falls on the odd cycle rather than the even one (`TicksBetween` as `((to + 1) >> 1) - ((from + 1) >> 1)`). With the new check, the odd-cycle case fails at character 864 on the first wrong output ("expected outputs (1568, ...), was (1569, ...)"). With the test as it was on `main`, the same change is caught only at character 1,227, when a frame start arrives early. Every timing fault found so far shows in both outputs and events sooner or later, because one drives the other, so this is not a fault that only the new check sees. It finds the fault at the first wrong character, and it checks the outputs on the path the machine takes.
- No code in the chip changed, so when the chip is advanced did not change, and the bus equivalence tests were not touched.

## Not done

- **The five wording nits from the task 14b review.** As the first pass found, they were in the plan's ledger, which was never committed, and the final pass's wording fixes cleared what could be found. Nothing identifiable remains, so nothing was changed. The issue stays open for this one bullet, as Dan asked in his comment on it; a wording fault found later gets its own issue.
- **Items in the final pass's "Not done" list that are not in the issue.** A bus test that reaches `StateAt`'s jump branch, the mode 7 parity assertion, the on-screen keys on a phone (being worked in another pull request) and the order of the deploy workflow were left as they were. They are outside this issue.

## Checks

On 15 October 2026, in this worktree: `dotnet build 6502.slnx -c Release` gave 0 warnings, and `dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release` passed 1,004 tests with none failed (10:23 to 10:25 UTC). Nothing in the core or any other machine was touched, so their tests were not run. No site code changed, so `site/tests/floor.json` is unchanged.
