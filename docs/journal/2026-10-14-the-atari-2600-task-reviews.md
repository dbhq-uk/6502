---
title: "What the Atari VCS's task reviews found"
date: 2026-10-14
summary: "Every task of the Atari VCS had a review. None found a critical fault. What they found was claims that went further than their evidence and guesses left untagged, and later tests that could not fail. This entry records the reviews of the second, sixth, seventh, eighth and ninth tasks, which no other entry does, and the final review's fix wave."
order: 70
---

# 14 October 2026: what the Atari 2600's task reviews found

A record of the reviews of tasks 2, 6, 7, and 8 and 9 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md), written on 15 October 2026 and dated 14 October with the rest of the build. Each task's other reviews are in that task's own entry; these four were in commits only. Each list below is read from the review commit's diff, not from its message alone.

## The pattern

Every task from 1 to 14 had a review whose findings landed as a commit of their own (`git log --format='%h %s' origin/main..HEAD | grep review`, 15 October 2026), and task 15's is the final review of the whole branch. By the controller's notes, which are not committed, none of the task reviews reported a Critical finding; the task 6 review did change behaviour (a reset now cuts off an untaken start), as a minor fault. What they found was the same few kinds of thing, in this order as the build went on: claims that went further than their evidence, guesses that did not carry `[guessing - verify]`, and then tests that could not fail. The final review of the whole branch found more of the first two (below).

## Task 2, the cartridge and the boards (`8ed0a0f`)

- The task's entry said each test file was written before its code. The tests and the code landed in one commit, so the history cannot show it; the sentence now says it was the author's order of work.
- `FaTests` now names, on the D0-clear test, the disagreement it takes a side in: CBS's patent and the suite's `bank-fa` gate FA's switch on D0, Horton and Korth say any access switches, and the test would fail under Horton and Korth.
- `docs/known-differences.md` gained the bullet that `Peek` of a RAM write port shows the cell. It had been only in `IBoard.cs` and the journal.

## Task 6, the objects (`66219da`)

- **One behaviour change, test first.** A reset left a start that a player's scan counter had not yet taken, so a close copy whose start came at a line's end still drew after a RESP0 in the next blank. `TiaPositionCounter.Reset` now cuts it off, and a new `TiaPlayerTests` case holds it. It is a guess for players, tagged: the suite's `resbl-kill` says it only for the ball.
- `collision/blank-gating`'s early pass was explained wrongly. It passed on a visible overlap (with HMOVE doing nothing, its missiles meet at pixel 2), not on latching in the blank; the sheet, the journal, `known-differences.md` and a comment in `SuiteSelfTests` say so, and task 7 was left to prove the blank mechanism.
- Three guesses tagged: a new start restarts a running scan, VBLANK gates the latches a colour clock after the write, and a reset cuts off a start for every object.
- `SuiteSelfTests` no longer types a count of rows per folder in a comment (rule 5).
- The plan's task 6 file list gained `TiaPositionCounter.cs` and the helper `TiaScene.cs`, both added when built; the bus entry's probe rerun was dated before task 6's rows.

## Task 7, HMOVE (`9954683`)

- The use of jigo2600's `TIA_Visual_Objects.md` was marked as not confirmed by Dan, because it went past the two purposes the spec named. Dan later confirmed it on 14 October 2026, and `tia.md` section 17.1 now says so.
- Two fits tagged `[guessing - verify]` in `TiaMotion.cs`, the sheet and the entry: an extra clock acts on the clock after its slot, and HMOVE is taken four or more clocks after the write.
- A typed count of sources went from a `ScreenshotTests` comment. A new test, `The_runner_compares_the_eighth_frame_after_power_on`, checks the runner's machine has ended eight frames; with it the probe's fault Z7, comparing the seventh frame, which had survived, is caught.
- The `hmove-stuck-widths` pixels were left unattributed, to be triaged; task 9 triaged them to a width-2 missile (`6faa5eb`).
- The plan's task 7 Modify list named `TiaMissile.cs` and `TiaBall.cs`, which were not touched; it now names `TiaPositionCounter.cs`.

## Tasks 8 and 9, the speed milestone and the inputs (`1fbe714`)

- `bench/atari-2600-speed/analyse.py` judged each median unrounded but printed it to two places, so a median that printed as 25.00 could be judged below 25. It now judges the figure it prints, shown wrong first on synthetic launches files.
- `rule.md` gained a dated pointer to the first run's entry, its text otherwise unchanged.
- The speed entry says what was held for Dan's decision and quotes the NES's figure from the NES's own entry.
- `AGENTS.md` names the hand-started workflow `atari-2600-speed-run.yml` and Dan's approval of it in its layout.
- A guess in the HMOVE entry was tagged.

## The final review's fix wave

The final review of the branch, at `340eb34`, found no Critical issue either. Its fix wave, on 15 October 2026, changed docs, comments, test names, the fault probes and three unused accessors, and no behaviour:

- **Claims.** Overclaims in the sheets, `known-differences.md` and several entries were cut to their evidence; untagged guesses were tagged; jigo2600's scan counter gate and its missile and ball width expression, which the entries cited and no sheet held, were added to `tia.md` sections 10 and 17.1 as paraphrased facts.
- **Test names.** The tests that gave a section number for what that section gives only as the model's choice were renamed to say so.
- **The fault probes.** Four patterns no longer matched the code they were written for and planted nothing: `board-faults.py` R4, `bus-faults.py` T7, `tia-line-faults.py` L1 and `tia-object-faults.py` R4. Each was ported to the current code and run alone (results below). `tools/probes/README.md` now lists every Atari 2600 probe.
- **Unused code.** `Tia`'s accessors for its objects and motion, `TiaPlayer.New` and `Old`, and `TiaPositionCounter.Resetting` had no caller in the tests, the WebAssembly host or the benches, and were deleted. The speed pass (#130) compares state reports, not these.

### The four ported faults, run alone

Each was run on 15 October 2026 with `python3 -I tools/probes/atari-2600/<script> <id>` on the code of `2885a5d`; the accessors' deletion, made while they ran, touches none of the four. The scripts copy only what their own task's tests needed, so on today's code the scratch copy's baseline already fails in `Atari2600AcceptanceTests`, `BootTests`, `PagePathTests`, `SuiteRecordedTests` and `SuiteSelfTests`, which read `machines/` and `docs/` (162 cases, in the three that print it). A fault is read by the files that fail beyond those. Each script's header now says so.

| Fault | What it plants now | Caught beyond the baseline by |
|---|---|---|
| `board-faults.py` R4 | the PAL pixel 2:1 | `RegionTests` |
| `bus-faults.py` T7 | a fire button reads pressed (`Tia.Read` gives 0 for INPT4 and INPT5) | `Atari2600BusTests`, `ControllersTests`, `TiaInputTests`, and two more cases each in `Atari2600AcceptanceTests` and `SuiteSelfTests` |
| `tia-line-faults.py` L1 | the blank always ends on LRHB, at 76 | `TiaLineTests`, `FrameBufferTests`, the object tests and many more |
| `tia-object-faults.py` R4 | a reset drops a held decode | `TiaMissileTests`, `TiaPlayerTests`, and two more cases each in `Atari2600AcceptanceTests` and `SuiteSelfTests` |
