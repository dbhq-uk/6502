---
title: "The BBC Micro's small robustness items, from the reviews"
date: 2026-10-13
summary: "Issue #44 collected the small items the BBC Micro's reviews left. Four are done, each with a test that fails without the change: the VIA's catch-up guard is let go in a finally, SystemVia.VsyncInput and BbcBus.Irq are internal, and the CRTC gets tests for the cycle each event carries and for a handler that writes to the chip. Two were found already done on main, and one list could not be recovered. No cycle behaviour changed."
order: 43
---

# 13 October 2026: the BBC Micro's robustness items

[Issue #44](https://github.com/dbhq-uk/6502/issues/44) lists the small items left from the per-task reviews of the BBC Micro. Each was checked against `main` first (`54c1016`). The original list is in [the final pass](2026-10-04-the-bbc-micro-final-pass.md) under "Not done, and why". This entry says what was done, how each change was shown to matter, and what was not done.

## Done

**`Via6522._catchingUp` is let go in a `finally`.** `CatchUp` set the guard, did the cycles, and cleared it on the last line. An exception in between left the guard set, so the next look threw "A cycle's work looked at the chip through a member that catches up", which hides the real fault. The body is now in a `try` and the guard is cleared in the `finally`.

- The final pass declined this because a `try` on the catch-up path changes the hot path after the speed was measured. It was chosen anyway, because a `try` that does not throw costs nothing in .NET beyond what the JIT may no longer inline, and `CatchUp` has loops and calls and is not inlined. The cost was measured (next item) rather than argued.
- **The test needed a fault to inject.** Nothing in the machine can throw inside a catch-up: no handler runs there. `AnExceptionInACatchUpLeavesTheChipSayingWhatWentWrongEachTime` therefore takes the keyboard away from a standalone `SystemVia` with reflection (the readonly `_keyboard` field), so the chip's next catch-up throws a `NullReferenceException`. The first look must throw it, and so must the second. Chosen against a test seam in the chip itself, which would put test-only code in the machine.
- **Shown to matter.** With `Via6522.cs` put back to `HEAD`, the test fails (the second look throws the guard's message). With the change, it passes.

- **The speed, measured on 13 October 2026, 18:18 UTC, on the ARM host.** `main` (`54c1016`) and this branch were built with `dotnet build -c Release bench/bbc-micro-speed/native`, and run in turn four times each with `bench/bbc-micro-speed/alternate.sh native 4 <base> <branch>` (host load about 2.5 before and after, so not quiet). Both builds gave identical `--fingerprint` output (the boot and program step hashes and checkpoints match, and so do the screen lines), so no cycle behaviour changed. The median was 87.0 MHz for `main` and 85.8 for the branch, 1.4 per cent lower; the best runs were 88.1 and 87.1. Boot times were 440.9 and 439.4 ms. The difference is within what the host's load moves between launches, but it is one sample from a busy host, so it is a measurement of that morning and not a claim that the `try` is free. The browser speed was not measured again.

**`SystemVia.VsyncInput` and `BbcBus.Irq` are `internal`.** The final pass kept `VsyncInput` public because `SystemViaTests` and `BusEquivalenceTests` drive CA1 through it on a machine's bus, and said a guard meant redesigning those tests. Making the property internal needs no redesign: the BBC Micro assembly now has `InternalsVisibleTo` for `Dbhq.Machines.BbcMicro.Tests` (the NES assembly already does this), and nothing outside the assembly can put CA1 out of step with the CRTC. `BbcBus.Irq` is used by two tests and by the native speed bench; the bench, a separate assembly, now reads `bus.SystemVia.Irq || bus.UserVia.Irq`, which is the same expression, so its fingerprint is unchanged.

- **Test.** `Ca1FromOutsideAndTheBusIrqLineAreNotPublic` checks by reflection that neither is public. Shown to fail with both put back to `public`.
- **Side effect.** The tests can now construct a chip on the machine's clock (`new Crtc6845(clock)`), which the next item uses.

**The CRTC equivalence tests.** Two were listed in the task 7 review.

- `EventsCarryTheCycleTheOraclesCharacterFallsIn` runs the per-character oracle beside two lazy chips on a `BbcClock`, one looked at after every character and one at random moments, and compares the cycle each event carries: every VSYNC edge with its level (`VsyncDriven`), and every frame start (the chip's `StateCycle` read inside the handler). Character n is cycle 2n at 1 MHz and cycle n at 2 MHz; a reset carries the cycle it was made in. It runs at 1 MHz with the clock on an even cycle and on an odd cycle, and at 2 MHz. The existing equivalence tests count events on a chip on its own and never see a cycle, which is why this gap was left. Shown to matter by two changes to the chip, each failing it: `VsyncDriven` raised with `Now` instead of `_doneTime` (all three cases fail), and `TimeAfter` at 1 MHz without `from & ~1` (the odd-cycle case fails and the other two pass).
- `AHandlerOfTheChipsEventThatWritesToItIsRefusedAndTheChipCarriesOn` was untested: a handler of `FrameStarted` that writes a register gets "A handler of this chip's event wrote to it", and the chip works afterwards, because its own guard is cleared in a `finally`. A second test pins that a write to R16 or above from a handler is not refused, because the chip returns before the guard. Shown to matter by removing the guard from `WriteData`: the first fails.

## Found already done

- **The sticky on-screen SHIFT latch and the "Press Start first" drop message.** Both were fixed in `6e55f93` (the final pass): a latched SHIFT or CTRL lets go when the screen takes the keyboard and at BREAK (`keys.letGo` in `site/public/bbc-micro.js`, with a test in `site/tests/bbc-micro.test.mjs`), and the drop message says to press Start only before Start and to wait while loading. The code on `main` was read again and no further edge was found. A latch holds no key until the next key is tapped, so a hidden page leaves nothing stuck. The drop handler is inside the page's wiring and is not exported, so its "loading" branch has no unit test, and it was not run in a browser here (Chrome is not installed on this host; the browser check runs in CI). It was left alone because a change written without a browser to try it in is a risk with no known fault to pay for it. No site code was changed, so the site tests and the workflow floors are unchanged.

## Not done

- **The five wording nits from the task 14b review.** The list was in the plan's ledger, which was never committed, and the final pass's "Wording made true" group already cleared what it found there. The files were searched for the nits and the journal entries for task 14b read again; nothing identifiable remained, so nothing was changed. If the five are written down somewhere, the issue should list them.
- **The other task 7 test ideas** (a bus test that reaches `StateAt`'s jump branch, and the mode 7 parity assertion) are not in issue #44 and stay as the final pass left them.

## Checks

The commands and what they showed are in the pull request. Nothing in the core was touched, so the core's tests were not run.
