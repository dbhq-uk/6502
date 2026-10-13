---
title: "The small notes from the NES models' reviews, worked through"
date: 2026-10-13
summary: "One issue collected six small notes left from the reviews of the NES models. Each was checked against current main first. The frame's corners are now checked against the marks they were fitted from. The journal wording was corrected in four places, and one of the four notes could not be reproduced. A model's status line no longer keeps its sentence about a missing console. U9's jobs are read from the redrawing's nets by a script and a test. Three claims on the page that no test touched now have one. One bound in a test turned out to do nothing and is now said to. Nothing on the page changed."
order: 39
---

# 13 October 2026: the NES models' notes, worked through

Issue 81 holds six notes from the reviews of the NES models (pull request 76) that were left until after the merge. None of them changes a figure or a sentence on the page. For each I checked first that it was still true on main (commit `54c1016`), then fixed it test first, and broke the code on purpose to see the test fail. This entry says what was done for each and what was not.

## 1. Frame checks

**Still true.** The site check "outline within 0.5 mm of board" compares the outline's extent with `frame.board`, and `board` is the extent of the corners, so it cannot fail. The Python corner test matched each true corner to the nearest point of the outline of any kind.

**Done.**

- `board_frame.py` now records `cornersAgainstMarks`: each corner the fit found, mapped back through the frame to the scan's pixels, against the mark in `marks.json` it was fitted from, in millimetres. The marks are placed by eye and say only where to look, so this is recorded and not judged.
- A new site test does the same sum on its own, in JavaScript, from `frame.json` and `marks.json`, and asks that every corner is within 1.0 mm of its mark. It also checks the recorded figures against its own and that exactly the edges with both end corners over 0.5 mm carry a note.
- The nearest-point corner test in `test_board_frame.py` now compares `result['corners'][i]` with `OUTLINE[i]`, by index. The old test would pass a corner found at another corner's place.
- `edges[6]` in `frame.json` now has a note. Corners 6 and 7 are 0.69 and 0.69 mm from their marks (the step in the right edge, in deep shadow). The text is written by `board_frame.py` from the figures, not typed.
- `heldOutY` now has `largest`: U6, 0.9813 per cent narrow, with a note that it is recorded and not judged (the verdict is on the median).

`frame.json` was not regenerated from the scan, because the scan is not here. It was rewritten by calling the same new functions on its own contents and writing it with the tool's own `write_data`; a rewrite of the unchanged file first gave no difference, so the only changes are the new fields. The next run of `board_frame.py` on the scan will write the same ones.

**Shown to fail.** Corners 3 and 4 swapped in `board_frame.py`: the by-index test fails (the old test would not). Pixel distance in place of millimetres: the offsets test fails. `and` changed to `or` in the far-corner rule: its test fails. The largest taken by signed value: its test fails. In the site test, moving one corner of `frame.json` by 2 mm fails it ("corner 3 is 1.99 mm from its mark"), and taking the note off edge 6 fails it.

## 2. Journal wording

All four checked against the entry of 5 October and edited in place:

- Task 4: the line "the nets row is not asserted to pass" now says that the test it describes was replaced after review by one that pins the verdict to FAIL.
- Task 4: it now quotes `board_trace.py`'s hash after the label change, `53225a47467bb46e3689cc319608fe577d1c6d50fca5eb9513ecf2447dd16000`. Checked with `sha256sum` on the file on main and on the file at the commit that made the change (`fee1eae`): the same.
- Task 7: the bold lead on the 32,311 PPU accesses a second now says "most likely the game", and so does the sentence "The cause is" in the body, because the sample's length was not recorded and the figure is agreed with and not proved.
- Task 6: the mutation sentence. **I could not reproduce what the issue says.** It asks for "three tests failed (one existing)". I made two mutations of the halted-read repeat in `RunDma` and ran `dotnet test tests/Dbhq.Machines.Nes.Tests` on each. Counting only the first repeat of a halt failed two tests, the two odd-parity cases (two repeats). Counting no repeat at all failed four, all four cases of the same theory. Neither failed an existing test. The review's exact mutation was not kept, so I cannot say what failed three. The journal now says what was recorded then, that the exact mutation is lost, and what the two new mutations did. The part of the note I could check is true: with an even parity there is one halted read, so counting repeats once and counting each repeat give the same answer, and those cases pin only the cycle count and the single repeat. Both mutations were undone; `git status` shows `NesBus.cs` unchanged.

## 3. Parts: the 15 mm bound

**Still true, and worse than the note says.** I slid P5's body 14 mm off its pads in `parts.json`. The test failed on "does not cover its pad", as intended. With the covers-all-pads line taken out as well, the test passed. The 15 mm bound reads the footprint's pads and not the body, so it can never see a body slid off its pads. The old comment said it did.

**Done.** I did not tighten it. Tightening to 12 mm would fit the five known values (11.9 mm is the largest) and would still not look at the body. The comment now says what the experiment showed and why the bound stays at 15. A comment change has no test to break; the experiment above is the evidence.

## 4. Dormant paths (task 9)

**Still true.** Both paths are unreachable today, because both modules draw both consoles. Read from the code: `followRegion` deleted the flag when the page's region came back to a drawn console but left the sentence on the status line; and each module's last line, `if (!root.dataset.modelMissing) status.textContent = ...`, dropped a tracks error whenever the flag was set.

**Done.**

- `followRegion` takes a fifth argument, `restore`, a function that gives the line the model would show now. When the flag goes, the status line is set from it. With no `restore` the line is emptied, not left stale. The board module's `restore` gives the tracks error if there was one, otherwise the machine's state; the case module's gives the machine's state. I moved the board's `tracksError` up beside `words` so that `restore` cannot meet it before it exists while the tracks are still loading.
- A new `settleStatus(root, status, line, problem)` replaces the two modules' last line. When nothing is missing it writes `line`. When the sentence is up it leaves it and adds `problem` after it, so a tracks error is now said even then.
- Tests: one new test in `model-regions.test.mjs` (the restore is asked for only when the sentence goes, and answers with what is true at that time; no restore gives an empty line; `settleStatus` in each case), and the existing page test now checks that the sentence goes and that both modules pass a restore and call `settleStatus`. Three tests in `nes-models.test.mjs` that matched the old call by its text were updated to the new call.
- **Shown to fail.** Removing the restore line from `followRegion`: two tests fail. Removing the `problem` branch of `settleStatus`: one fails. The board module not passing a restore: one fails. The board module's old last line put back: one fails.

I did not run the page in a browser. Chrome is not installed here and the paths cannot be reached on the real page today; the Node tests drive `followRegion` on a stand-in for a model's panel, as the earlier test of this path does.

## 5. Not measured

Each of these is on the page and was marked PARTLY supported in the final claims check: true by looking, not by a test. Three tests now read the code or data behind them. They test that the page's words and the model's code agree, not that the model is right.

- **Square corners, flat rear window, ribs as lines:** one test reads the outside model's module. The shells are polygons extruded with `bevelEnabled: false` and nothing curves; the window is drawn with `onRear` at the default depth and the module never reads the set-back (which is measured, and above zero); the underside's straps, panels and bumps all go through `rect()`, which builds line segments. The test also checks that the note says each of the three. Shown to fail by turning the bevel on, by using the set-back in the window's depth, and by drawing the ribs as meshes.
- **The green tint:** one test reads `stage.mjs` and `tokens.css`. The two lights that are not white are the `mint` fill and the `moss-70` ground of the hemisphere light, both lead in green, and together they are under a third of the lights' strength. "A little" is not defined anywhere, so the line at one third is mine, drawn on 13 October and written in the test. Shown to fail by multiplying the fill's strength by ten, and by making it white.
- **"Strong perspective" for the 20 mm lens:** one test. The file's own XMP names the camera, an ILCE-7RM4 (the Sony alpha 7R IV), a full-frame camera with a sensor 35.7 mm wide. That width is Sony's published figure from memory of its specification, not read from a file here, and is marked as such in the test. A 20 mm lens on it sees more than 80 degrees across the frame, and 24 mm or less is the usual line for an ultra-wide lens. Shown to fail by setting the source's focal length to 50 mm.

**Not done: nothing here measures how much perspective a photograph has.** That would need the photographs themselves, a vanishing-point fit on a quiet host. The tests say the lens is a wide one; they do not say the pictures look distorted by the amount the note implies.

## 6. U9's jobs

**Still true.** The page says U9 inverts A13 and the reset line and clocks the lockout chip, "read from OpenTendo's KiCad redrawing". They were read by hand on 5 October.

**Done.** `tools/nes-model/board_nets.py` reads the redrawing's nets. For each pin of U9 it records the net, the pin type and every other pad on that net, and for each of six jobs whether the pins are the right way round (input is an input, output an output) and the parts it must reach are on its nets. It writes `data/u9-nets.json`; `run-board.sh` runs it last. I fetched I2 from the `dbhq-uk` fork at the pinned commit into a scratch folder; its SHA-256 matched `sources.json`.

On 13 October all six jobs hold. For the three the page names: pin 5 (input) is on `/PPU-A13` with U5 pin 25, and pin 6 (output) on `/PPU-~{A13}` with P1 pin 58; pin 9 (input) is on `/~{RST}` with U5 pin 22, U6 pin 3 and U10 pin 9, and pin 8 (output) on a net with R5; pin 12 (output) is on a net with U10 pin 6, its `CLK_IN`. The other three are the oscillator with X2 (pins 1 and 2), the clock buffered to P1 and P2 (pins 3 and 4), and the audio amplifier (pins 11 and 10, with R6 across them). These agree with the hand reading of 5 October.

The site test reads `u9-nets.json`, checks that it was read from the file the sources record, that every job holds and that each job's parts are on its nets, and that the parts it reaches are the ones the page calls the PPU, the CPU and the lockout chip. It also checks the page's legend sentence. Python: five tests on a made-up redrawing (a pad's net and type, a job that holds, a part off its net, a pin the wrong way round, a missing U9). **Shown to fail:** taking the pin-type check out fails the wrong-way-round test; taking the parts check out fails the off-net test. In the site test, a job set to not hold fails it, and a wrong part on a job's net fails it.

The job table, which pin is which job and which parts each net must reach, is still typed by hand in `board_nets.py`. What is read from the netlist is the nets, pin types and members. A reader who disagrees with a job changes that table and the test runs again.

## Tests

Run on 13 October 2026 on the ARM host.

- `python -m pytest tools/nes-model/tests -q`: 117 passed, from 107 before. Ten new: five on the frame's corners and notes, five on U9's nets.
- `cd site && npm test` (after `npm run build` and a `results.json`, which was copied from another working copy for this run because the full .NET suite was not run for it): 483 tests, 482 pass, 0 fail, 1 todo. That is seven new: two on the frame, four on the outside model, the stage, the lens and U9, one on the status line. The floors in `validate.yml` and `deploy-site.yml` rise from 475 to 482 to match.
- `dotnet test tests/Dbhq.Machines.Nes.Tests`: run with the two mutations above and once unmutated; the unmutated result is in the pull request.

## Not done

Nothing in the issue was skipped. The one thing a reader may want more of is in section 5: how much perspective the PAL photographs have is a measurement on the photographs, and no test here makes it.
