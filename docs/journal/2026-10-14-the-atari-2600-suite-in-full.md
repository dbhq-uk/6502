---
title: "The Atari VCS's suite in full: what each test showed"
date: 2026-10-14
summary: "The eleventh task of the Atari VCS: every test of the console's test suite that the trust split runs, run in both regions, with the outcomes of the recorded tests written by a recorder and checked in CI. Four trusted tests about a player's pattern scan under a merged motion step fail, and Dan agreed, one test at a time, to record them with the cause written down. Two PAL pictures differ only because the reference draws several colours in one grey."
order: 65
---

# 14 October 2026: the Atari 2600's suite in full

Task 11 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): every test of the pinned `missingno-vcs-tests` suite that the trust split runs, in both regions; the triage of every failure and every mismatch; and the recorded outcomes. New: `SuiteOutcomes` and `SuiteRecordedTests` in the tests, `machines/atari-2600/suite-outcomes.json`, and the fault probe `tools/probes/atari-2600/suite-record-faults.py`. Changed: `SuiteSelfTests` (the recorded rows moved out, the not-run list and the trust-split check added), `ScreenshotTests` (its comment), `docs/known-differences.md` and `docs/atari-2600/facts/suite.md`.

Before it, as its own commit (`37e0a52`), task 10's review was folded in: a sound test that could not see the HMOVE line now can, guesses are tagged, and the speed milestone's claims are cut to what its runs show. [The task 10 entry](2026-10-14-the-atari-2600-sound.md) and [the speed milestone entry](2026-10-14-the-atari-2600-speed-milestone.md) say what changed.

**One change to the plan's steps, by the controller:** a trusted test the model cannot pass stays where it is, on the not-yet list with its exact failure, and is not moved to recorded by this task. For each one, the evidence is written here so that Dan can be asked one test at a time; a test moves only on his answer. **Dan's answer, 15 October 2026**, given one test at a time with the evidence below: move all four `collision/merge-delivery` tests to recorded. Each now has its `docs/known-differences.md` entry, its outcome is the recorder's, and the not-yet list is empty and gone.

## What was built

- **`SuiteOutcomes`** runs each recorded test once per region and keeps its outcome, and builds the whole file. A self-test's outcome is `"pass"`; a pass with the OBSERVED byte for a test whose header reports a measurement on a pass (`power-on-bank`, `suite.md` section 2); a fail with CODE, OBSERVED and EXPECTED; or no verdict with the byte at `$80`. A screenshot test's outcome is its count of mismatched pixels by the partition rule. The file also names the two pinned commits and the manifest's hash, so outcomes recorded under one pin are never compared under another.
- **`SuiteRecordedTests`** has a row per recorded test and region that compares the machine's outcome with the file, and `Record_the_suite_outcomes`, the recorder: with `ATARI_RECORD=1` it writes the file, and otherwise it checks that the committed file is byte for byte what it would write, as the NES's `RecordTheBootFrames` does. So the file is the recorder's (`AGENTS.md` rule 5), and a test left out of it, or one it holds that is not recorded, fails.
- **`SuiteSelfTests`** keeps every trusted self-test, both regions. The four recorded cartridge rows moved to `SuiteRecordedTests`. `NotRun` lists the tests of boards not modelled with each board's issue, taken from `suite.md` section 5.8. `The_trust_split_places_every_test_in_the_suite_exactly_once` reads every source in the pinned manifest and checks that each is trusted (in `Trusted`), recorded, or not run, as the plan's trust table puts it, and nothing else; a test the split trusts leaves `Trusted` only through `SuiteOutcomes.MovedFromTrusted`, the four Dan agreed to move. Nothing is skipped silently.

## Decisions

- **The rows do not compare while the recorder writes.** With `ATARI_RECORD=1`, `--filter Record` also runs the rows, in no fixed order with the recorder, and the file on disk may be the one being replaced; so a row then shows its outcome and asserts nothing. The variable is never set in CI. Chosen over a filter that runs the recorder alone, because the plan's command is `--filter Record`.
- **The not-yet rows kept their task number, 11, until Dan answered**, chosen over moving them, which the controller's change forbade. Once he had, **the list was removed, not left empty**: an xUnit theory with no rows fails as having no data, and an empty list with no theory would check nothing. The moved tests are named in `SuiteOutcomes.MovedFromTrusted`, with the date and the rule, so the trust-split check can tell a test Dan moved from one left out.
- **The partition rule is not changed for PAL.** The suite's README says a screenshot test is compared "pixel-exact" against missingno's renders, and says nothing of a frame drawn with another palette, so it gives no rule to put in place of the plan's item 5. The PAL counts are recorded as they are (below).
- **OBSERVED is recorded on a pass only where the header says it carries a measurement.** `bank-fa` also leaves a byte at `$82` on its pass, which its header does not describe; recording it would make the file depend on scratch bytes.

## The sweep

The plan's step 2, on 14 October 2026 and again on 15 October after the move: `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~SuiteSelfTests|FullyQualifiedName~SuiteRecordedTests|FullyQualifiedName~ScreenshotTests" --logger "console;verbosity=detailed"`, before and after the code above, with the same outcomes.

**Trusted.** Every trusted self-test in `SuiteSelfTests.Trusted` passes in both regions: `cpu/`, `riot/`, `harness/sanity`, the four trusted cartridge tests, and every `tia-timing/`, `tia-render/` and `collision/` self-test but four. The four failed, in both regions, with the same bytes as after task 7, and are now recorded with Dan's agreement:

| Test | Region | CODE | OBSERVED | EXPECTED |
|---|---|---|---|---|
| `collision/merge-delivery` | both | `$04` | `$53` | `$7B` |
| `collision/merge-delivery-replant` | both | `$03` | `$49` | `$4D` |
| `collision/merge-delivery-stretch` | both | `$03` | `$2A` | `$3E` |
| `collision/merge-delivery-train` | both | `$04` | `$47` | `$7F` |

**Recorded**, as `suite-outcomes.json` holds them after the recorder's run of 15 October 2026 (`ATARI_RECORD=1 dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter Record`), made from that file by a throwaway script and not typed:

| Test | Kind | NTSC | PAL |
|---|---|---|---|
| `cartridge/bank-fa` | self-test | pass | pass |
| `cartridge/bank-hotspot-window` | self-test | pass | pass |
| `cartridge/power-on-bank` | self-test | pass, OBSERVED `$B1` | pass, OBSERVED `$B1` |
| `cartridge/ram-superchip` | self-test | pass | pass |
| `collision/merge-delivery` | self-test | fail, CODE `$04`, OBSERVED `$53`, EXPECTED `$7B` | fail, CODE `$04`, OBSERVED `$53`, EXPECTED `$7B` |
| `collision/merge-delivery-replant` | self-test | fail, CODE `$03`, OBSERVED `$49`, EXPECTED `$4D` | fail, CODE `$03`, OBSERVED `$49`, EXPECTED `$4D` |
| `collision/merge-delivery-stretch` | self-test | fail, CODE `$03`, OBSERVED `$2A`, EXPECTED `$3E` | fail, CODE `$03`, OBSERVED `$2A`, EXPECTED `$3E` |
| `collision/merge-delivery-train` | self-test | fail, CODE `$04`, OBSERVED `$47`, EXPECTED `$7F` | fail, CODE `$04`, OBSERVED `$47`, EXPECTED `$7F` |
| `harness/calibration` | screenshot, mismatched pixels | 0 | 407 |
| `tia-render/colors` | screenshot, mismatched pixels | 0 | 3680 |
| `tia-render/draw-delay` | screenshot, mismatched pixels | 0 | 0 |
| `tia-render/object-priority` | screenshot, mismatched pixels | 0 | 0 |
| `tia-render/pf-priority` | screenshot, mismatched pixels | 0 | 0 |
| `tia-render/score-mode` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-comb` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-late` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-line-end` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-live-reach` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-live-seam` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-rewrite-race` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-strobe-line` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-stuck-grid` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-stuck-latch` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-stuck-player` | screenshot, mismatched pixels | 143 | 143 |
| `tia-timing/hmove-stuck-release` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/hmove-stuck-straddle` | screenshot, mismatched pixels | 143 | 170 |
| `tia-timing/hmove-stuck-stretch` | screenshot, mismatched pixels | 184 | 184 |
| `tia-timing/hmove-stuck-widths` | screenshot, mismatched pixels | 5 | 5 |
| `tia-timing/hmove-values` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/midline-color` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/midline-grp` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/midline-pf` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/midline-resp` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/midline-vblank` | screenshot, mismatched pixels | 0 | 0 |
| `tia-timing/nusiz-draw` | screenshot, mismatched pixels | 0 | 0 |

**Not run:** the tests of boards not modelled (`SuiteSelfTests.NotRun`, `suite.md` section 5.8, each with its issue) and every SECAM build (#106).

## The four trusted tests that fail: the evidence Dan was given, and his answer

Each is trusted by the spec's table: it is a self-test in `collision/`, checked on the suite's console, and its header does not say it is untested on hardware. Each stayed on the not-yet list, failing exactly as above, until Dan said whether it moves to recorded with a `known-differences.md` entry; on 15 October 2026 he agreed for each, one at a time. They share one cause, so the cause and what was tried come first, then each test.

### The cause they share

On the drawn line, an extra motion clock from HMOVE that falls on an object's own motion clock merges with it into one stretched pulse. The model takes that pulse as one early step for every object alike: the object's position does not change, but its pixel for that clock is its next state (`tia.md` section 17.1). For a one-pixel missile or ball the suite agrees. For a player, the four tests say the console decides by the state of the player's pattern scan (the three-bit scan counter that walks GRP's eight bits): the pulse carries two advances, and whether the second reaches the scan depends on where the scan is (on its start decode, on its first bit, counting), on the phase of the four-clock step grid, and, at double width, on which bit is starting. The model gives the scan the same single early step in every state.

**What the sources say.** Towers's notes describe the scan counter as a three-bit binary ripple counter clocked by MOTCK through a gate NUSIZ sets. jigo2600's `TIA_Visual_Objects.md` (facts only, as Dan allowed) gives its logic as flip-flops stepped on the rising edge of the player clock, with the NUSIZ gate passed through an enable flip-flop one player clock late. Both describe clean clock edges. Neither says what a merged, stretched pulse does to that counter, and a flip-flop stepped on a rising edge steps once for one pulse, so the documented logic gives one advance whatever the scan's state. The second advance the suite measures is a matter of how the pulse's two parts propagate through the ripple counter and the enable flip-flop, which no document read gives at that level. `tia.md` open item 12 has said so since task 7.

**What was tried.**

- Task 7 built the merged step from the one mechanism the sources give and checked it against every HMOVE header; the four tests failed on their player legs, and a player-only rule fitted to the tests' tables is what the plan's review focus forbids, so none was written.
- Task 11 read the four headers again, Towers's scan counter section and jigo2600's player section, looking for any state-dependent path. The one candidate is jigo2600's enable flip-flop: the model's NUSIZ gate reads the two-phase clock's quarter at once, where jigo2600 latches it one player clock late. At single width the gate is always open, so the flip-flop cannot explain `merge-delivery` or `-replant`; and at double width a one-clock lag on a clean edge still gives one advance per pulse. So it was not built.
- `merge-delivery-replant`'s first wrong leg is leg 3, a RESP0 landing at colour clock 69, the reset the suite measures at pixel 7 and the model lands at 6 (`tia.md` open item 7). In a scratch copy outside the repository, that reset was held one clock longer so that it lands at 7 (`DIAG_CLOCK=69` in a throwaway change to `Tia.cs` and `TiaPositionCounter.cs`, then `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~merge-delivery|FullyQualifiedName~SuiteSelfTests.Trusted|FullyQualifiedName~colour_clock_69"`). `merge-delivery-replant` still failed at CODE `$03`, now with OBSERVED `$09` against `$4D`: leg 7 lost its merge pixel too, so open item 7 does not explain it. Held at 68 or 70 instead, nothing changed. Every trusted test still passed, and the other three failures did not move. The change was thrown away.

**What would settle it:** a gate-level reading of the scan counter and its enable flip-flop on the TIA schematic's sheet for the players (traced, not read for labels), or a capture of each test's legs on a second console. Either is beyond this task.

### `collision/merge-delivery`

- **Header.** "A motion step merged into a player mid-line reaches that player's pattern scan only when the scan's own state allows it. A scan still on its first pattern bit takes the step at any phase of the step grid. A scan already counting takes it only at its own phase. The first pattern bit is never skipped. A reset written over the start decode suppresses the start." Seven legs, each read through a player-missile collision latch. OBSERVED is the seven legs as bits 0 to 6; EXPECTED `$7B` is every leg colliding but leg 3.
- **Observed.** CODE `$04`, OBSERVED `$53`, EXPECTED `$7B`, both regions. `$53` is legs 1, 2, 5 and 7 colliding; legs 4 and 6 do not. Leg 4 is a scan one step from skipping its first bit, at grid phase 1, which must take the step as a plain tick (the never-skipped rule); leg 6 is a counting scan on its third bit at phase 0, off its own phase, which must refuse the step. The model takes the merged step in both, so the probe column goes dark.
- **Sheet.** `tia.md` sections 10 and 17.1, open item 12.
- **Cause.** The shared one, above: the model has no state-dependent delivery to a player's scan.

### `collision/merge-delivery-replant`

- **Header.** "A RESP0 write that lands while the start decode for that player is already running does not corrupt the delivery. The pattern still appears, kept in place or shifted whole per one fixed per-landing-clock profile, and the merge pixel is never deleted." Twelve legs at quad width (three park columns by four reset clocks, 63, 66, 69 and 72), each with a stuck motion train putting a step on the start decode; a missile probes the merge pixel. PASS is the profile `$4D` for legs 1 to 8 and `$E0` for legs 9 to 12 and the flags; on a fail CODE is the first wrong leg and OBSERVED and EXPECTED the byte that differs. This header has no "region-independent" line (`suite.md` section 7.3); it fails the same in both regions.
- **Observed.** CODE `$03`, OBSERVED `$49`, EXPECTED `$4D`, both regions. `$49` is merge pixels on legs 1, 4 and 7; the console has them on legs 1, 3, 4 and 7. Leg 3 is the first park column with the RESP0 landing at colour clock 69; the model deletes its merge pixel, which the header says never happens.
- **Sheet.** `tia.md` sections 9, 10 and 17.1, open items 7 and 12.
- **Cause.** The shared one; and not open item 7, by the diagnostic above.

### `collision/merge-delivery-stretch`

- **Header.** "Whether a lone merged motion step advances a double-width player's pattern scan depends on which pattern bit is starting. The start decode and every second bit take the advance, at either phase of the step grid. The other bits refuse it, even at their own phase." Six legs at NUSIZ `$05`, two missile probes each; CODE `$01` to `$06` is probe A of a leg, with OBSERVED all six A bits against EXPECTED `$3E`.
- **Observed.** CODE `$03`, OBSERVED `$2A`, EXPECTED `$3E`, both regions. `$2A` is probe A lit on legs 2, 4 and 6; the console also has legs 3 and 5. Leg 3 is the first bit's first clock and leg 5 the third bit's: the header says both refuse the advance, so the bit's second clock is kept and probe A lit. The model takes the advance there, as on every bit.
- **Sheet.** `tia.md` sections 10 and 17.1, open item 12.
- **Cause.** The shared one, at double width: the console's scan treats odd and even bits differently under a merged step, and no source read says how.

### `collision/merge-delivery-train`

- **Header.** "A double-width player's pattern scan takes a merged motion step by the same state rule whether the pulse is alone or deep inside a pulse train. The train carries no memory. From the fourth pattern bit on, delivery follows the step grid's own phase instead." Seven probe legs (CODE `$01` to `$07`, OBSERVED all seven, EXPECTED `$7F`) and a control pair (`$08`, `$09`).
- **Observed.** CODE `$04`, OBSERVED `$47`, EXPECTED `$7F`, both regions. `$47` is legs 1, 2, 3 and 7 right; legs 4, 5 and 6 wrong. Leg 4 is a train row's fourth bit, off the grid's phase, which must stay silent; leg 5 a train row's first bit, which must refuse; leg 6 a walked park's fourth bit off phase, silent. The model takes the step in all three.
- **Sheet.** `tia.md` sections 10 and 17.1, open item 12.
- **Cause.** The shared one, plus a second rule the header states for the fourth bit on (the grid's phase decides), which no source read describes either.

## The pictures that do not match

Each was looked at as task 7 did, our frame beside the reference in `TestResults/atari-2600/`, and triaged as a likely bug first.

- **`harness/calibration` and `tia-render/colors`, PAL.** A throwaway test in the scratch copy listed, for every reference colour that more than one of our colour numbers sits under, those numbers and our PAL palette's colour for each. In calibration every mismatched pixel is `$08` against `$E8` under one reference grey; in `colors` the reference draws hues 0, 1, 14 and 15 of each luminance in one grey, over our `$0x`, `$1x`, `$Ex` and `$Fx`. In both, the numbers that share a reference colour are ones our own PAL palette also draws identically (`tia.md` section 19.5). Compared over our palette's colours instead of colour numbers, calibration gives no mismatch at all, and `colors` keeps only the three lines of `$10`, `$E0` and `$F0`, which the reference draws in a dark grey apart from its black and our palette draws black. Neither is a TIA fault. So the TIA's numbers are right and the count is the plan's item 5 meeting a palette that merges numbers. The rule is not changed (above), and the counts are recorded.
- **`hmove-stuck-player`, `-straddle` and `-stretch`, both regions.** Single pixels of a player on grid clocks of the drawn line under the stuck motion train, as task 7 found: the same difference as the four `merge-delivery` tests. Recorded; they are screenshot tests, recorded by the trust split, so they need no move.
- **`hmove-stuck-widths`, both regions.** The width-2 missile task 9 triaged (`tia.md` open item 19). Recorded.

Every other picture gives no mismatch in either region.

## The tests, test first

`SuiteOutcomes`, `SuiteRecordedTests` and the new `SuiteSelfTests` facts were written before the file existed. On 14 October 2026, `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~SuiteRecordedTests|FullyQualifiedName~SuiteSelfTests"` failed every recorded row and the recorder, each for want of `suite-outcomes.json`, and passed the trust-split check. Then `ATARI_RECORD=1 dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter Record` wrote the file, and without the variable the whole project passed. On 15 October 2026, after Dan's answer, the four tests were added to `SuiteOutcomes.MovedFromTrusted` and the not-yet list removed first: the same filter then failed the eight new rows and the recorder, because the file did not hold the four yet. The recorder was run again, and the whole project passed.

## Every test file shown able to fail

The fault pass ran on 14 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/suite-record-faults.py <scratch>`), one fault at a time, building and running the whole Atari 2600 test project. The O faults are the plan's step 4, a recorded value changed by hand; the R faults are in the recorder; the T faults are in the trust split's lists. The figure in brackets is the number of failing test cases in each file.

| Fault | Planted | Test files that failed |
|---|---|---|
| O1 | a screenshot count changed by hand: PAL calibration one fewer | `SuiteRecordedTests` (2) |
| O2 | a self-test outcome changed by hand: the power-on bank read as bank 0 | `SuiteRecordedTests` (2) |
| O3 | a recorded pass changed by hand to a fail | `SuiteRecordedTests` (2) |
| O4 | a recorded test left out of the file | `SuiteRecordedTests` (3) |
| O5 | the pins in the file changed | `SuiteRecordedTests` (1) |
| R1 | a pass recorded without the OBSERVED its header reports | `SuiteRecordedTests` (3) |
| R2 | a no verdict recorded as a pass | none at first; after a test was added, `SuiteRecordedTests` (1) |
| T1 | a not-run test missing from `NotRun` | `SuiteSelfTests` (1) |
| T2 | a trusted test missing from `Trusted` | `SuiteSelfTests` (1) |
| T3 | a recorded cartridge test listed as not run too | `SuiteSelfTests` (2) |
| T4 | a not-yet test listed as not run instead (first pass only; the list is gone) | `SuiteSelfTests` (1) |

In all, 11 faults were planted and 10 were caught on the first run. **R2 survived it**: none of the recorded self-tests ends without a verdict, so the shape of a no verdict was never written. `SuiteRecordedTests.Each_kind_of_self_test_outcome_is_recorded_in_its_own_shape` now builds each shape from made-up runs. Run again (`... suite-record-faults.py <scratch> R2`), it caught R2. None survives.

**After the move, 15 October 2026**, the pass was run again on the tree with the four moved (`python3 -I tools/probes/atari-2600/suite-record-faults.py <scratch>`), with T4 replaced and three faults added for the moved tests. O4's text had to follow the file, where `ram-superchip` is no longer the last self-test, and was run again alone (`... <scratch> O4`). Every fault was caught; the figures that changed:

| Fault | Planted | Test files that failed |
|---|---|---|
| O4 | a recorded test left out of the file (run alone) | `SuiteRecordedTests` (3) |
| R1 | a pass recorded without the OBSERVED its header reports | `SuiteRecordedTests` (4) |
| T4 | a moved test kept in `Trusted` too | `SuiteSelfTests` (4), among them the trusted row of `collision/merge-delivery` in both regions |
| T5 | a moved test dropped from the moved list | `SuiteRecordedTests` (1), `SuiteSelfTests` (1) |
| O6 | a moved test's recorded code changed by hand | `SuiteRecordedTests` (2) |

The others gave the same figures as the first pass. None survives. O1 to O6 are the plan's step 4: a recorded value changed by hand fails CI's test.

## Where this leaves the task

Every trusted self-test in `SuiteSelfTests` passes in both regions, and the not-yet list is gone. Every recorded outcome, the four moved tests included, is the recorder's, and CI fails on a change (the O faults above). The commands, all on this box in Release: the sweep, `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "SuiteSelfTests|SuiteRecordedTests" --logger "console;verbosity=detailed"`; the recorder, `ATARI_RECORD=1 dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter Record`; the fault pass, `python3 -I tools/probes/atari-2600/suite-record-faults.py <scratch>`.
