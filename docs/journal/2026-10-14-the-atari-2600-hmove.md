---
title: "The Atari VCS's HMOVE: the motion clocks, early and late, and the screenshot runner"
date: 2026-10-14
summary: "The seventh task of the Atari VCS: HMOVE, its ripple counter, the latches that stop each object's extra clocks, the comb, and late and early HMOVE, all from one mechanism with no table of cases; and the runner that compares a frame with the suite's reference pictures. Most of the suite's HMOVE self-tests pass; four about a player's scan counter do not, and wait for the triage."
order: 61
---

# 14 October 2026: the Atari 2600's HMOVE, the motion clocks and the screenshot runner

Task 7 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): HMOVE and the motion clocks, built from [`tia.md`](../atari-2600/facts/tia.md) sections 15 to 17, and the screenshot runner. New: `TiaMotion`, and in the tests `TiaMotionTests`, `Partition`, `PartitionTests`, `PngReader`, `ScreenshotRunner` and `ScreenshotTests`. Changed: `Tia` (HMOVE, HMCLR, the comb and the extra clocks), `TiaPlayer` (its graphics registers latched apart from its clock), `TiaPositionCounter` (when a reset cuts a start off) and `SuiteSelfTests`.

Before it, task 6's review was folded in as its own commit (`66219da`): a reset now cuts off a start a player's scan counter has not taken yet (test first), `collision/blank-gating`'s early pass is described as the visible overlap it was, four guesses are tagged, the bus entry's probe counts are dated, and the plan names `TiaPositionCounter.cs` and `TiaScene.cs`.

## What was built

- **One mechanism, no table.** `TiaMotion` holds the ripple counter, the five more-motion latches and the HMOVE latch. An HMOVE write is taken (SEC) on the first grid clock at least four colour clocks after it lands; the grid clocks are colour clocks 2, 6, ..., 226 of the line. SEC loads the counter with 15 if it is at rest and sets every latch and the HMOVE latch. On each grid clock an object with its latch set gets an extra clock, the counter steps down, and each latch clears where the counter equals its register's nibble with the low three bits inverted. After 0 the counter rests at 15; a latch whose stop was missed stays set and sends an extra clock every grid clock until a write of `$80` to `$8F`. `tia.md` section 17.1 gives it in full.
- **The comb.** The HMOVE latch is cleared as each line starts. While set, the blank ends at count 18 (LRHB, colour clock 76) instead of 16, so the first eight pixels are black and the objects miss eight motion clocks.
- **The extra clocks go to the counters task 6 built.** `TiaPositionCounter` already had one clock input; an extra clock drives it as a motion clock does, so a reset's hold counts it, and the comb and the pulses act on the same counters as the picture.
- **Where an extra clock acts**: on the grid clock, before that clock's pixel, because it rises with the colour clock and a motion clock half a clock later, as jigo2600 reads the schematic. In the blank it moves the object left. On the drawn line it merges with the motion clock into one early step: no move, but the object's pixel for that clock is its next state, so a one-pixel dot is swallowed or widened by where it falls, as the suite measures.
- `Tick` still allocates nothing.

## Decisions

- **jigo2600's `TIA_Visual_Objects.md` was read for facts**, chosen over working from Towers and the suite alone, because it is the one written analysis of the schematic's motion logic found, and the spec already admits jigo2600's `doc/TIA_*.md` files for facts (task 1). It was fetched on 14 October 2026 from the commit `92662bb` the sheet already cites; no code of the project was read. From it come the part names (SEC, SECL, the extra clocks), that an HMOVE while the count runs does not start it again, and that an extra clock steps the object before its pixel is latched. The sheet's sources table names it.
- **An extra clock acts on the clock after its slot**, chosen over the slot itself. Both give the same positions; only this one gives the suite's residues on the drawn line and also puts a pulse on colour clock 18, where `hmove-reset-merge`'s RESBL lands. That settles `tia.md` open item 4 in the model `[inferring]`.
- **HMOVE is taken at the first grid clock four or more clocks after the write**, chosen as the one rule that gives every cell of section 16's movement table and the comb's boundaries at cycles 20, 21, 74 and 75. It is a fit, and `tia.md` open item 3 stays open.
- **An HMOVE while the count runs sets the latches again and does not start the count again** `[inferring from jigo2600; guessing - verify]`.
- **A reset cuts off its object's start at the end of the clock the write lands on**, chosen over at once, which task 6 had. The suite's `resbl-kill` failed on it: its RESBL landing at colour clock 90 must spare the ball pixel at 90 and erase the one at 91. Its header says a missile's RESMx suppresses "from write to plant", and `hmove-values`'s header and picture show a missile's dot on the reset's own clock "already drawn", so the cut is the same for missiles; the model does it for players too `[guessing - verify]`. Test first: `TiaBallTests` and `TiaMissileTests` gained the cases and failed before the change.
- **The player merge rules are not modelled.** The four `merge-delivery` tests state when a merged step reaches a player's scan counter as rules on the scan's state (on its start decode, about to show its first bit, counting, and which bit at double width). No mechanism in the documents read gives them; fitting a table to them is what the plan's review focus forbids. So every object takes a merged step the same way, and the four fail. They stay in the "not yet" list with their failures, now marked for task 11, which triages them; they are not moved to recorded without Dan. `tia.md` open item 12.
- **Open item 7, the player reset at colour clock 69**, is not decided. `merge-delivery-replant`'s comments say again that it lands at 7, "measured", for the test's own probe, but the test fails first on its merge legs, so nothing in it separates a mechanism for 7. The model still lands it at 6, and `TiaPlayerTests` still pins that.
- **The partition rule's open case**: where reference colours share a match, the one with the most pixels in the reference keeps it, and a tie goes to the lower RGBA value. The plan says "the less frequent keeps no match" and does not say frequent in what, or what breaks a tie.

## The suite's self-tests

Of the sixteen trusted self-tests that waited for this task, twelve pass in both regions: `tia-timing/hmove-apply`, `hmove-line-aligned`, `hmove-reset-merge` and `hmove-walk`; `tia-render/missile-copies` and `missile-reset-lock`; and `collision/blanked-reset`, `copy-adjacency`, `hmove-edge`, `latency-swallow`, `resbl-kill` and `stuck-drift`. Eleven passed on the first run of the code; `resbl-kill` failed with CODE `$04`, OBSERVED `$FC`, EXPECTED `$FE` (the erasure reaching one clock too far back) until the reset's cut moved, above.

`missile-reset-lock` passing is the check task 6 left on its RESMP choice: the missile released at 4, 6 and 10 pixels right of a single, double and quad player, with the lock at scan count `%010`.

The four that fail, in both regions, with the same bytes: `merge-delivery` CODE `$04`, OBSERVED `$53`, EXPECTED `$7B`; `merge-delivery-replant` CODE `$03`, OBSERVED `$49`, EXPECTED `$4D`; `merge-delivery-stretch` CODE `$03`, OBSERVED `$2A`, EXPECTED `$3E`; `merge-delivery-train` CODE `$04`, OBSERVED `$47`, EXPECTED `$7F`. In `merge-delivery`, the legs read wrong are 4 (a scan one step from showing its first bit, which must refuse the step) and 6 (a counting scan off its own phase, which must refuse it); the model takes the step in both.

**`collision/blank-gating`, rechecked.** Task 6's review found it passed on a visible overlap. With HMOVE built, its group 2 missiles are stepped into the blank by the extra clocks, as its header describes, and it still passes; with the latches stopped in the horizontal blank (fault G1 below) it now fails, so its pass rests on latching in the blank.

The outcomes were read on 14 October 2026 from `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~SuiteSelfTests" --logger "console;verbosity=detailed"`.

## The screenshot runner

`ScreenshotRunner` powers the machine on with a test's binary, runs it until eight frames have ended (the plan's item 3) and compares the eighth with the test's reference picture by the partition rule (item 5), reference row r against our row r + k. `PngReader` reads the references with `ZLibStream`, checking each chunk's CRC and undoing all five row filters; anything but 8-bit RGBA or RGB, not interlaced, is refused with a plain sentence.

**The alignment, fixed once on `harness/calibration`** (item 4). Each offset from -8 to +8 rows was tried in both regions. On NTSC the best is 0 with no mismatched pixel; on PAL the best is also 0, with 407. Every other offset gives over a thousand in either region. So k = 0: our row 0, the line VSYNC ends on, is the reference's row 0, which agrees with the suite's README and not its harness's comment. `ScreenshotRunner.RowOffset` holds it, and `ScreenshotTests.Calibration_matches_best_at_the_recorded_offset` fails if calibration ever matches best elsewhere.

**Every screenshot test's count**, on 14 October 2026, from `dotnet test tests/Dbhq.Machines.Atari2600.Tests --filter ScreenshotTests --logger "console;verbosity=detailed"` (Release). Every test not in this table gave 0 in both regions.

| Test | NTSC | PAL | What it is |
|---|---|---|---|
| `harness/calibration` | 0 | 407 | the PAL palette, not the TIA (below) |
| `tia-render/colors` | 0 | 3680 | the PAL palette, not the TIA (below) |
| `tia-timing/hmove-stuck-player` | 143 | 143 | a player under merged steps |
| `tia-timing/hmove-stuck-straddle` | 143 | 170 | a player under merged steps |
| `tia-timing/hmove-stuck-stretch` | 184 | 184 | a double and quad player under merged steps |
| `tia-timing/hmove-stuck-widths` | 5 | 5 | wide missiles and the ball under merged steps |

Each non-zero count was looked at, as our frame beside the reference written to `TestResults/atari-2600/`, and with the mismatched pixels listed by a throwaway test, and triaged as a likely bug first:

- **The PAL pictures of `calibration` and `colors`.** Every mismatched pixel is a colour number the TIA keeps apart from another that the suite's PAL reference draws in the same grey: `$E8` and `$08` in calibration, and hues 1, 14 and 15 against hue 0 in the colour ramp, since a PAL console shows all four as grey (`tia.md` section 19.5). The partition rule then gives the shared grey to one number and counts the other's pixels. The frame is right; the count is the fixed rule meeting a palette that merges colour numbers. The rule is not changed to suit it (item 5); task 11 records the counts.
- **The four `hmove-stuck` pictures.** Every mismatched pixel is one pixel on a grid clock of the drawn line, where the reference keeps a player, or a missile or ball wider than one pixel, lit and the model has it dark, because its merged step skipped that pixel's state. It is the same difference as the four failing `merge-delivery` self-tests: the model treats a merged step the same for every object and width, and the console does not. Not fixed here, for the reason above.
- **`hmove-values`** gave 1 in each region at first: a missile's dot on the very clock a RESM0 landed, drawn in the reference and cut in the model. It is now 0, by the change to the reset's cut above.

No count is asserted: task 11 records them.

## The tests, test first

`TiaMotionTests` was written first, from sections 15 to 17's worked examples: each value from `$00` to `$F0` on cycle 3, the low nibble ignored, HMOVE applied again each line, each object by its own register, HMCLR, the comb and its boundaries (cycles 1, 19, 20, 21, 55, 74, 75 and 76), section 16's movement table (every cell the sheet does not mark as the model's own), the residues of a dot under drawn-line pulses and its collisions, the rewrite race before, on and after the last comparison, a new value whose stop is still ahead, the jam and its drift of 17 a line, `$00` on cycle 19, HMCLR on cycle 19, `hmove-reset-merge`'s ball at 3 and its walk by every HMBL value, resets under the comb, and an HMOVE while counting. `PartitionTests`, `ScreenshotTests` and `StateReportTests` (now naming `TiaMotion`) came with the runner.

On 14 October 2026, before `TiaMotion` existed, `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~TiaMotionTests|FullyQualifiedName~PartitionTests|FullyQualifiedName~ScreenshotTests"` failed 114 cases, all in `TiaMotionTests`. `PartitionTests` and `ScreenshotTests` passed from the start, because the code they test is test code written with them; the fault pass below shows them able to fail. The first build of `TiaMotion` took HMOVE a grid clock early, because the start was checked after the wait had been counted down on the same clock, and 19 cases failed; with that fixed, three cases of one test failed on a test fault (the ball was behind the missile on the line it checked) and were corrected. Then the whole project passed, with the "not yet" rows aside, and those were moved as above.

On 14 October 2026 `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests` passed 1100 test cases, and `dotnet build -c Release` of the solution gave no warnings.

## Every test file shown able to fail

The fault pass ran on 14 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/tia-motion-faults.py <scratch>`), one fault at a time, building and running the whole Atari 2600 test project. The figure in brackets is the number of failing test cases in each file.

| Fault | Planted | Test files that failed |
|---|---|---|
| H1 | HMOVE taken a grid clock early | `SuiteSelfTests` (12), `TiaMotionTests` (68) |
| H2 | HMOVE taken a grid clock late | `SuiteSelfTests` (16), `TiaMotionTests` (69) |
| H3 | the comb left out | `SuiteSelfTests` (28), `TiaMotionTests` (105) |
| H4 | the HMOVE latch never cleared | `SuiteSelfTests` (66), `TiaMotionTests` (156) |
| H5 | the comparators read every bit inverted | `SuiteSelfTests` (26), `TiaMotionTests` (116) |
| H6 | an HMOVE while counting starts the count again | `TiaMotionTests` (1) |
| H7 | the ripple counter never comes to rest | `SuiteSelfTests` (29), `TiaMotionTests` (20) |
| H8 | HMCLR clears nothing | `SuiteSelfTests` (10), `TiaMotionTests` (2) |
| H9 | HMCLR clears the latches too | `TiaMotionTests` (1) |
| H10 | the grid on quarter 1 | `SuiteSelfTests` (12), `TiaMotionTests` (24) |
| H11 | the extra clock acts after the pixel | `SuiteSelfTests` (12), `TiaMotionTests` (5) |
| H12 | the extra clock and the motion clock both count | `SuiteSelfTests` (8), `TiaMotionTests` (52) |
| H13 | the extra clocks skip the players | `SuiteSelfTests` (16), `TiaMotionTests` (1) |
| R1 | a reset cuts the start on the clock it lands | `SuiteSelfTests` (2), `TiaBallTests` (1), `TiaMissileTests` (1) |
| G1 | the latches stopped by the horizontal blank (task 6's C2) | `SuiteSelfTests` (2), `TiaCollisionTests` (1) |
| Z1 | a partition tie to the higher number | none at first; after a test was strengthened, `PartitionTests` (1) |
| Z2 | a shared match kept by the less frequent | `PartitionTests` (1) |
| Z3 | a colour without a match counts nothing | `PartitionTests` (1) |
| Z4 | the Paeth filter takes the up byte on a tie | none (an equivalent fault, below) |
| Z5 | the average filter rounds up | none at first; after a test was strengthened, `ScreenshotTests` (1) |
| Z6 | the rows aligned one apart | `ScreenshotTests` (2) |
| Z7 | the seventh frame compared | none (below) |
| Z8 | the CRC not checked | none at first; after a test was strengthened, `ScreenshotTests` (1) |
| S1 | the TIA does not report its motion | `StateReportTests` (9) |
| S2 | the motion does not report its latches | `StateReportTests` (9) |
| S3 | the counter does not report its late cut | `StateReportTests` (9) |

In all, 26 faults were planted. 21 were caught on the first run, and three more after their tests were strengthened; two survive. **Z1, Z5 and Z8 survived the first run.** Z1: the partition test of a tie gave the same count whichever number won, so `A_tie_under_a_colour_goes_to_the_lower_number` now adds a second colour that makes the choice count. Z5: the PNG round trip ran the same predictor in both directions, so a wrong predictor cancelled out; `The_png_reader_undoes_each_filter_as_the_specification_works_it` now checks each filter against rows worked by hand from the specification. Z8: the corrupted file broke the image data, which zlib refuses anyway, so `The_png_reader_refuses_a_chunk_whose_crc_is_wrong` now changes a CRC alone. Run again (`... tia-motion-faults.py <scratch> Z1 Z4 Z5 Z7 Z8`), all three were caught.

**Two survive.** Z4 is an equivalent fault: Paeth chooses between `a` and `b` on a tie only when `pa`, `pb` and `pc` are all equal, which forces `a`, `b` and `c` to be equal, so no byte can change. Z7, comparing the seventh frame, changes only the counts, which nothing asserts until task 11 records them (the plan: "No count is asserted here"); calibration still matches best at offset 0 on the seventh frame. Task 11's recorded outcomes will catch it.

**G1 is task 6's C2 replanted**: the latches stopped by the horizontal blank. Task 6's review found `collision/blank-gating` passing with it. Now it fails `SuiteSelfTests` in both regions on `collision/blank-gating`, as well as `TiaCollisionTests.Section_14_the_latches_set_in_the_blank`, so the suite test now proves latching in the blank, and its group 2 missiles meet there through HMOVE's extra clocks, as its header describes.
