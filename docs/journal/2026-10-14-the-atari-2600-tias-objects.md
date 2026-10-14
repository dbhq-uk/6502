---
title: "The Atari VCS's TIA: the players, the missiles, the ball and the collisions"
date: 2026-10-14
summary: "The sixth task of the Atari VCS: the two players, the two missiles and the ball, each on a position counter with its own start decodes and reset, their copies, sizes, reflection and vertical delay, priority and score mode, and the fifteen collision latches, all taken from the same signals the picture is drawn from. Every self-test of the suite the plan gives this task passes, and two more pass a task early."
order: 59
---

# 14 October 2026: the Atari 2600's TIA, the players, the missiles, the ball and the collisions

Task 6 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): the TIA's movable objects, built from [`tia.md`](../atari-2600/facts/tia.md) sections 9 to 14. New: `TiaPositionCounter`, `TiaPlayer`, `TiaMissile` and `TiaBall`. Changed: `Tia` (priority, score mode, the collision latches and the objects' registers) and `TiaPlayfield` (which half of the line a cell is in).

Before it, task 5's review was folded in as its own commit: guesses tagged in the journal, `known-differences.md` and `FrameBuffer.cs`, the RSYNC choice tied to the WSYNC release point in `tia.md` section 6, a note on `HorizontalCounter` during an RSYNC, the L3 row of task 5's fault table corrected from a rerun, and task 4's `bus-faults.py` ported to the code task 5 rewrote, with the rerun in the task 4 entry.

## What was built

- **One position counter for all five objects.** `TiaPositionCounter` keeps the count, 0 to 39, and the quarter of its two-phase clock, 0 to 3, stepped by the motion clock (MOTCK) while the blank is off. The chip's counter is polynomial, like the horizontal sync counter, but only its place in the sequence is decoded, so the place is kept. Task 7's HMOVE will add its extra clocks to these same counters.
- **The start decodes.** Count 39 decodes the main copy, which starts as the counter wraps; counts 3, 7 and 15 decode the close, medium and far copies, which NUSIZ allows. A decode is held until the two-phase clock next completes a cycle and only then becomes the start signal, active for that count's four clocks, as Towers describes the "block of logic that requires a full cycle of the two-phase clock". NUSIZ is read there.
- **Players.** The start sets a three-bit scan counter, which counts to 7 and stops and is never reset otherwise, so a player past the right edge finishes after the next line's blank. Its clock is gated by NUSIZ: every clock at single width, quarters 1 and 3 of the two-phase clock at double width, quarter 1 at quad width. REFP and VDEL are read at every pixel. GRP0 copies new GRP1 to old, GRP1 copies new GRP0 and the ball's new enable to old.
- **Missiles and the ball** draw from the start signal through the width circuit Towers describes: width 1 is the start's first quarter, 2 its first two, 4 the whole start, 8 the start and the start of the count before. The missiles take the players' copy decodes; the stretched settings give them one copy. RESBL holds a main-copy decode, so the ball draws on its own line.
- **RESMP** hides the missile, so it neither draws nor collides, and resets its counter where the player's main copy says (below).
- **Priority and score mode** choose the colour register from the six outputs (section 13).
- **The collision latches** are set every clock from the same six outputs, through a table of the fifteen pairs, unless VBLANK's gate is on. Nothing else stops them: they set in the horizontal blank, where the counters wait and an output already started stays on.
- `Tick` still allocates nothing.

## Each object's start delay, and where it came from

| Path | In the model | Source |
|---|---|---|
| A reset strobe to the counter's restart | 4 clocks: 2 colour clocks, then 2 motion clocks | Fitted to both arms of `tia.md` section 9's landing rule (the suite's headers, 2k6specs), with Towers's "4 CLK" to reset |
| A decode to its start signal | to the next boundary of the two-phase clock, 4 clocks | Towers: the decodes need "a full cycle of the two-phase clock" |
| The start to a missile's or the ball's first pixel | 0 clocks | Section 9: 4 clocks after the strobe |
| The start to a single-width player's first pixel | 1 clock, the first clock the scan counter's gate lets through | Towers: "1 additional CLK to latch the player START"; section 9: 5 clocks after the strobe |
| The start to a double or quad player's first pixel | 2 clocks, the gate's first quarter 1 | Section 9: "one clock later still" (the suite's `nusiz` and `object-priority`) |
| A GRP write to the picture | 1 clock after it lands | Section 4, from the suite's recorded `midline-grp` picture |

So a missile reset written at W on the drawn line draws from W + 4, a player from W + 5, and from the blank they draw at pixels 2 and 3 of the next line, the table in section 9 at every row but one.

## Decisions

- **The reset's four clocks are two colour clocks and then two motion clocks**, chosen over four of either. Four colour clocks would land a missile reset in the blank at pixel 0, and four motion clocks at pixel 4, where the sheet gives 2; on the drawn line all three give W + 4. The split is the one that gives both arms of the landing rule from one mechanism. `[guessing - verify]`
- **A player reset landing at colour clock 69 lands at pixel 6**, where the suite measured 7. No mechanism found in the documents gives 7 there and the landing table elsewhere; it is the sheet's open item 7 and task 7's `merge-delivery-replant` measured it. `TiaPlayerTests` pins the 6, so a change is seen.
- **A decode held when a reset lands is delivered as the reset ends**, because the reset restarts the two-phase clock and its end is a boundary. That is the ride-through `resp-restrobe` asserts (the copy "emerges 5 clocks after the reset") and Towers's start "lost or delayed up to 3 CLK": a reset landing before the decode stops the counter short of it, and the copy is lost.
- **A reset cuts off a start being drawn.** The suite's `resbl-kill` says so for the ball; the model does it for every object. `[guessing - verify]` Task 6's review found the reset left a start a player's scan counter had not yet taken, so a close copy whose start came at a line's end still drew after a RESP0 in the next blank; the reset now cuts that off too, and `TiaPlayerTests.Section_9_a_reset_cuts_off_a_start_the_scan_has_not_taken` holds it.
- **RESMP resets the missile as the main copy's scan counter reaches `%010`**, chosen over Towers's `%100`. The suite measures the released missile 4, 6 and 10 pixels right of a single, double and quad player's first pixel, and 2k6specs gives 6 and 10. A reset at `%100` with any one delay gives 4, 8 and 16; `%010` with a delay of two clocks gives all three. That is a fit to three measurements against a document's bit pattern. `[guessing - verify]`; task 7's `missile-reset-lock` tests it.
- **The scan counter's gates are quarters 1 and 3 (double) and 1 (quad)**, the quarters that give the sheet's first pixels and widths; Towers names the phases without their order. `[guessing - verify]`
- **A start that arrives while a player is drawing restarts its scan counter.** No source says. `[guessing - verify]`
- **Score mode's halves are the playfield's own**, from the CNT decode, so they move with RSYNC as the playfield does, over a fixed pixel 80. In score mode the playfield takes its player's priority and the ball keeps COLUPF, and the score bit counts only with D2 clear, as 2k6specs alone states. `[guessing - verify]`
- **VBLANK's gate for the latches is the picture's**, taken a clock after the write (task 5's choice for the picture). `[guessing - verify]`
- **The objects power on at count 0, quarter 0, with nothing held or drawing.** No source gives it.

`docs/known-differences.md` has each of these, and `tia.md` sections 9.1 and 11.1 the model's counter and RESMP.

## The suite

Every trusted self-test `suite.md` gives task 6 passes in both regions, all on the first run of the code: `cpu/rmw-strobe` and `stack-aliases-tia`; `tia-timing/reset-same-line`, `resp-restrobe` and `rsync`; `tia-render/missiles-ball`, `nusiz`, `player-reflect`, `players`, `playfield-reflect`, `playfield`, `positioning` and `vertical-delay`; and `collision/all-pairs`, `latches`, `onset-sweep`, `per-pixel` and `reset-phase`. The trusted rows of tasks 4 and 5 still pass.

**Two tests pass a task early, and are not claimed as this task's.** `cpu/partial-drive`, which the plan gives task 9, now passes: its first failure was its collision check, CODE `$05`, and its input checks read only the inputs at rest that task 5 fixed (a dumped INPT0 reads 0, an open fire button 1). `collision/blank-gating`, which `suite.md` gives task 7, passes too, but on a visible overlap: with HMOVE doing nothing, its group 2 missiles meet at pixel 2 and the latch is read near colour clock 130, so the pass does not show latching in the blank (task 6's review found it: planting C2, the latches stopped by the horizontal blank, leaves it passing). Task 7 must prove the blank mechanism; until then `TiaCollisionTests.Section_14_the_latches_set_in_the_blank` is its only guard. Both are now trusted rows, with a comment saying so, and task 7 and task 9 must keep them passing.

**The rest of the three folders wait for task 7.** Sixteen trusted self-tests in `tia-timing/`, `tia-render/` and `collision/` use HMOVE. They are now in the "not yet" list with their first failures, which were read from the run, and must fail exactly so until task 7 moves them: `hmove-apply` `$01`, `hmove-line-aligned` `$02`, `hmove-reset-merge` `$01`, `hmove-walk` `$01`, `missile-copies` `$16`, `missile-reset-lock` `$01`, `blanked-reset` `$02`, `copy-adjacency` `$01`, `hmove-edge` `$01`, `latency-swallow` `$12`, `merge-delivery-replant` `$01`, `merge-delivery-stretch` `$02`, `merge-delivery-train` `$02`, `merge-delivery` `$01`, `resbl-kill` `$03` and `stuck-drift` `$02` (the same in both regions; the observed and expected bytes are in `SuiteSelfTests`).

**Task 5's RSYNC guesses.** `tia-timing/rsync` passes with RSYNC restarting the line three colour clocks after its write and with the line it starts releasing a pending WSYNC. `rsync` alone does not tell the guesses apart: the fault pass's X1 (RSYNC four clocks after the write) and X2 (four, with the WSYNC release a clock later) both leave it passing, and so did the release two and three clocks later, tried by hand in the same scratch copy. But other trusted rows do: with four clocks `tia-render/playfield` fails in every one of those cases and `tia-timing/resp-restrobe` with the release up to a clock later, and `latency-swallow`'s first failure moves. So the suite's object tests side with task 5's three clocks and a release on colour clock 0. Task 5's review had suggested four with a later release might align the same; in this model it does not, and `tia.md` section 6, `known-differences.md` and the task 5 entry now say so. Whether RSYNC's line releases a pending WSYNC (X3) no suite test shows; only `TiaLineTests` holds it.

The outcomes were read on 14 October 2026 from `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~SuiteSelfTests" --logger "console;verbosity=detailed"`.

## The tests, test first

The test files were written first: `TiaPlayerTests` (section 9's landing table, the clock 69 case, a reset that starts no main copy, the eight NUSIZ settings, player 1's own registers, the cycle 55 and `$B4` worked examples, the bit order and reflection, the reflect bit read mid-player, vertical delay both ways, the GRP write's clock, the scan counter running past the edge, the copy held through a reset, the copy a reset loses, resets that hide the main copy, and RESMP at three widths and with copies), `TiaMissileTests` (the landing table, the cycle 55 example, the four widths, the copies and the stretched settings, ENAM, missile 1, a reset strobed twice, the counter running under VBLANK), `TiaBallTests` (the cycle 55 example, landings in and after the blank, the widths, several strobes a line, vertical delay, its colour), `TiaPriorityTests` (sixteen overlaps with CTRLPF D2 clear and set, and score mode) and `TiaCollisionTests` (each of the fifteen latches by its own pair alone and by no single object, the section's worked examples, the blank, VBLANK, priority, CXCLR and a missile hidden by RESMP), with the helper `TiaScene`. `StateReportTests` now names the new classes, and `SuiteSelfTests` gained the eighteen rows. Three tests were added after the fault pass, below.

The first build failed only on two uses of `Assert.Empty` the xUnit analyser refuses, which were the tests' own. Then, with the four new classes written but not wired into `Tia`, `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests` failed 164 cases on 14 October 2026: `StateReportTests` (9), `SuiteSelfTests` (36), `TiaBallTests` (14), `TiaCollisionTests` (23), `TiaMissileTests` (24), `TiaPlayerTests` (40) and `TiaPriorityTests` (18). With `Tia` wired, every new test passed on its first run, and no test was changed to pass; the only failure was the "not yet" row of `cpu/partial-drive`, which now passed and was moved, as above.

On 14 October 2026 `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests` passed 854 test cases, and `dotnet build -c Release` of the solution gave no warnings.

## Every test file shown able to fail

The fault pass ran on 14 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/tia-object-faults.py <scratch>`), one fault at a time, building and running the whole Atari 2600 test project. The figure in brackets is the number of failing test cases in each file.

| Fault | Planted | Test files that failed |
|---|---|---|
| R1 | a reset holds the counter three clocks | `SuiteSelfTests` (8), `TiaBallTests` (11), `TiaCollisionTests` (1), `TiaMissileTests` (21), `TiaPlayerTests` (21) |
| R2 | a reset is colour clocks only | `TiaBallTests` (2), `TiaMissileTests` (3), `TiaPlayerTests` (19) |
| R3 | a reset is MOTCK clocks only | `SuiteSelfTests` (2), `TiaBallTests` (3), `TiaMissileTests` (3), `TiaPlayerTests` (19) |
| R4 | a held decode is dropped by a reset | `SuiteSelfTests` (2), `TiaPlayerTests` (1) |
| R5 | RESBL sends no start | `SuiteSelfTests` (2), `TiaBallTests` (8) |
| R6 | RESP0 and RESP1 send a start | `SuiteSelfTests` (4), `TiaPlayerTests` (4) |
| D1 | the close copy decoded a count late | `SuiteSelfTests` (6), `TiaMissileTests` (1), `TiaPlayerTests` (8) |
| D2 | the far copy decoded a count late | `SuiteSelfTests` (4), `TiaMissileTests` (2), `TiaPlayerTests` (2) |
| D3 | NUSIZ `%110` without its far copy | `SuiteSelfTests` (4), `TiaMissileTests` (1), `TiaPlayerTests` (1) |
| D4 | NUSIZ `%101` with a close copy | `SuiteSelfTests` (6), `TiaMissileTests` (1), `TiaPlayerTests` (2) |
| D5 | the main copy decoded a count early | `SuiteSelfTests` (18), `TiaBallTests` (14), `TiaCollisionTests` (1), `TiaMissileTests` (24), `TiaPlayerTests` (37) |
| D6 | NUSIZ read at the decode, not at the boundary | none at first; after a test was added, `TiaPlayerTests` (3) |
| P1 | double width gated on quarters 0 and 2 | `SuiteSelfTests` (2), `TiaPlayerTests` (2) |
| P2 | quad width gated on quarter 0 | `SuiteSelfTests` (10), `TiaPlayerTests` (4) |
| P3 | REFP ignored | `SuiteSelfTests` (2), `TiaPlayerTests` (4) |
| P4 | VDELP ignored | `SuiteSelfTests` (2), `TiaPlayerTests` (3) |
| P5 | GRP seen on the clock it lands | `TiaPlayerTests` (1) |
| P6 | GRP0 copies into player 0's own old register | `SuiteSelfTests` (2), `TiaPlayerTests` (2) |
| P7 | GRP1 does not copy the ball's enable | `TiaBallTests` (1) |
| P8 | the scan counter stops at the line's end | `TiaCollisionTests` (1), `TiaPlayerTests` (1) |
| P9 | a new start does not restart the scan | none at first; after a test was added, `TiaPlayerTests` (1) |
| L1 | RESMP locks at scan `%100` | `TiaPlayerTests` (4) |
| L2 | RESMP locks on every copy | `TiaPlayerTests` (1) |
| L3 | RESMP does not hide the missile | `TiaCollisionTests` (1), `TiaPlayerTests` (3) |
| W1 | width 2 draws one pixel | `TiaBallTests` (1), `TiaMissileTests` (2) |
| W2 | width 8 draws four | `SuiteSelfTests` (4), `TiaBallTests` (1), `TiaMissileTests` (7) |
| W3 | the ball's width from NUSIZ0 | `SuiteSelfTests` (2), `TiaBallTests` (4) |
| C1 | two latches of CXM0P swapped | `SuiteSelfTests` (8), `TiaCollisionTests` (3) |
| C2 | the latches stopped by the horizontal blank | `TiaCollisionTests` (1) |
| C3 | VBLANK does not stop the latches | `SuiteSelfTests` (2), `TiaCollisionTests` (1) |
| C4 | CXCLR clears nothing | `Atari2600BusTests` (1), `SuiteSelfTests` (46), `TiaCollisionTests` (3) |
| C5 | no BL-PF latch | `SuiteSelfTests` (12), `TiaCollisionTests` (4) |
| C6 | the latches miss what player 0 and missile 0 hide | `SuiteSelfTests` (28), `TiaCollisionTests` (9) |
| Q1 | CTRLPF D2 ignored | `TiaPriorityTests` (4) |
| Q2 | score mode ignored | `TiaPriorityTests` (3) |
| Q3 | score mode's halves swapped | `TiaPriorityTests` (3) |
| Q4 | player 1 in front of player 0 | `TiaPriorityTests` (6) |
| Q5 | score mode's half by pixel 79 | none at first; after a test was added, `TiaPriorityTests` (1) |
| M1 | the objects clocked in the blank | `SuiteSelfTests` (58), `TiaBallTests` (14), `TiaCollisionTests` (10), `TiaMissileTests` (24), `TiaPlayerTests` (37), `TiaPriorityTests` (15) |
| X1 | RSYNC restarts the line four clocks after the write | `CycleOrderTests` (4), `SuiteSelfTests` (6), `TiaLineTests` (4) |
| X2 | RSYNC at four clocks and WSYNC released a clock later | `CycleOrderTests` (4), `SuiteSelfTests` (6), `TiaLineTests` (6) |
| X3 | RSYNC's line does not release WSYNC | `TiaLineTests` (1) |
| S1 | the player does not report its scan | `StateReportTests` (9) |
| S2 | the counter does not report its held decode | `StateReportTests` (9) |
| S3 | the playfield does not report its half | `StateReportTests` (9) |
| S4 | the TIA does not report the ball | `StateReportTests` (9) |

In all, 46 faults were planted and all 46 were caught, three only after a test was added. **D6, P9 and Q5 survived the first run.** D6: no test changed NUSIZ between a decode and its start, so `Section_9_nusiz_is_read_as_the_decode_becomes_the_start` now does it both ways. P9: no test delivered a start while a player was drawing, so `Section_10_a_start_while_the_player_is_drawing_restarts_its_scan` now does, by making a quad player single width with a close copy part way through. Q5: no score mode test lit the left half's last cell, so `Section_13_score_modes_left_half_ends_with_the_last_left_cell` now does. Run again with each fault (`... tia-object-faults.py <scratch> D6 P9 Q5`), all three were caught. D6 and P9 test choices the model makes and no source states, so those tests hold the choice, marked `[guessing - verify]`, not a fact; Q5's test holds the half the sheet states, from pixel 80.

X1 to X3 are task 5's RSYNC guesses replanted, for the suite section above; X1 and X2 are caught by trusted suite rows, X3 only by `TiaLineTests`.
