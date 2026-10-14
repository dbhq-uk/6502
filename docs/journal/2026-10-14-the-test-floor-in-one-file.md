---
title: "The test floor in one file"
date: 2026-10-14
summary: "The floor under the site's passing tests was a number typed into two workflows, each above a long comment of its history. Every pull request that added tests raised both, and in the merge train of this day three of six conflicted on the number and its comment. The number now lives in one file that both workflows read, the history is kept here, and a test fails if a workflow holds a number of its own or stops reading the file."
order: 49
---

# 14 October 2026: the test floor in one file

The site's tests are counted in CI, and the build fails if fewer pass than a floor. The count is read back because a test run that silently skips looks the same as one that passes. Until today the floor was typed into two workflows, `validate.yml` and `deploy-site.yml`, each above the same long comment that recorded how the floor had risen. This is issue 116.

## What went wrong

Every pull request that adds site tests raises the floor, so every such pull request edits both workflows in the same few lines. In the merge train of 14 October 2026 (pull requests 93, 94, 87, 52, 54 and 88, merged in that order), three of the six (87, 54 and 88) conflicted in both workflows, on the floor and on its comment. Each later merge had to measure the floor again on the merged tree, because neither side's number was right once the other was in.

## What was decided

The floor is one number in one file, `site/tests/floor.json`, as `{"floor": N}`. Both workflows read it with `jq` in the same step that checks the count, and refuse a value that is not a whole number. Neither workflow holds the number.

This was chosen over a line in `site/package.json`, which also changes for unrelated reasons and would make unrelated conflicts. It was chosen over a script that computes the floor from the tests, because the floor is meant to be a deliberate number: a computed one would fall with the suite and catch nothing.

Two branches that each raise the floor still conflict on that one line. That is the point. The conflict is a prompt to measure the count again on the merged tree, and it is now the only place it can happen.

The history of the floor moved out of the workflow comments. It is below, so the dated record is kept. From now on a pull request that raises the floor may add a line here or leave the history to its own journal entry; the file holds only the number, so that it stays one line to conflict on.

## What is checked

`site/tests/build.test.mjs` already asserted that the two workflows held the same number. That check could not survive the number leaving the workflows, so it now asserts that both read the same file and that the file holds a positive whole number. A second test asserts that neither workflow holds a number of its own: the lines that compare the passing count are the only ones that name it, and they compare it with `$FLOOR` and nothing else.

Both tests were shown able to fail on scratch copies of the workflows, not the real ones. In one copy `validate.yml` had its comparison put back to a typed number; in another `deploy-site.yml` stopped reading `floor.json`; in a third the file held a word instead of a number. Each copy failed the test it should and no other.

The second test adds one to the suite, so the floor goes from 640 to 641 in this change.

## The floor's history, moved from the workflows

Newest first. These are the comment lines that sat above the number, with the lead-in words kept as written.

- 640 as at 14 Oct 2026, measured on the BBC Micro models' branch with main merged in (the NES models' notes, the BBC Micro's robustness, the KIM-1's teletype and the Electron, pull requests 93, 94, 87, 52 and 54)
- 568 as at 14 Oct 2026, with task 10's fix wave (one test: known-differences.md's section on the BBC models gives each figure and verdict as its data file has it)
- 567 as at 14 Oct 2026, with the BBC Micro models' task 10 (five tests for mutations that survived: the keys in the machine's layout, the lights' bits from BbcLeds, no figure typed in either note, every copper credit traced to look at, and the browser check's judgements on the inside and the lights)
- 562 as at 13 Oct 2026, with the BBC Micro models' task 9b (eight tests: the two entries and their modules import, the claim of both views and the MODELS entries, each view's credits, the words on the page, each entry thin and following the machine, the first load and the budgets, the browser check's models steps)
- 554 as at 13 Oct 2026, with task 9a's review (eight tests: each BBC model module loads, with its main exports)
- 546 as at 13 Oct 2026, with the BBC Micro models' task 9a (thirteen tests: the outside's keys and legends, its shell and opening, its lights, its key state on bbc:key, a click on a key, no owl, the rear's and underside's labels, the words atlas, its tokens, caption, results file and note, unclaimed)
- 533 as at 13 Oct 2026, with the BBC Micro models' task 8 (ten tests: the outside's method and its changes, the keys' mapping from the fork, O1, the front edge not measurable, the check point, the keys and lights in the opening, the keys' placing and colours, the rear ports, the case module, the photographs)
- 523 as at 13 Oct 2026, with the BBC Micro models' task 7 (sixteen tests: the shared access sampler, and the inside's scene, legend, caption, results file and note, unclaimed)
- 507 as at 13 Oct 2026, with the BBC Micro models' task 6 (eleven tests: the page's bbc:key, bbc:start and panel.bbc, and the counters' names as BbcChip's)
- 496 as at 13 Oct 2026, with task 5's review (two tests: the method's files and marks.json on disk as the run read them; every description of the parts says both checks failed)
- 494 as at 13 Oct 2026, with the BBC Micro models' task 5 (nine tests: the parts' method pinned to its hashes, places on footprints, the fitted set against models.md, the counters, the outline and overlaps, the manual's places and the identities pinned as failed, I5, the module)
- 485 as at 13 Oct 2026, with task 4's review (a test that board_trace.py and common.py on disk are the method whose hashes the journal recorded before the run)
- 484 as at 13 Oct 2026, with the BBC Micro models' task 4 (six tests: the board's track map, copper.json's verdicts and figures, its nets and I5 FAILs pinned, ic-table.json, the method's hashes, and NOTICE.md's section on the map)
- 478 as at 13 Oct 2026, with the BBC Micro models' task 3 review minors (a test that registration.json keeps the solder side's model, unrounded parameters and transform)
- 477 as at 13 Oct 2026, with the BBC Micro models' task 3 fix wave (the drills' faces as the plan revised them, the held-out figures by kind of hole, and every DIP's reference read or said unread)
- 546 as at 14 Oct 2026, measured on the Electron's branch with main merged in again (the NES models' notes, the BBC Micro's robustness and the KIM-1's teletype, pull requests 93, 94 and 87)
- 525 as at 13 Oct 2026, when the Electron was switched on in the registry (`npm test` in site/: 526 tests, 525 pass, 1 todo): its preview and the tests that only the preview gave meaning went, and five came in
- 528 as at 13 Oct 2026, measured on the Electron's branch with main merged in (`npm test` in site/: 529 tests, 528 pass, 1 todo): main's 475 and the Electron page's tests, its preview build's whole suite among them
- 496 as at 14 Oct 2026, measured once main (the NES models' notes and the BBC Micro's robustness, pull requests 93 and 94) was merged into the KIM-1 teletype
- 483 as at 13 Oct 2026, read from the passing count in CI's log of the KIM-1 teletype branch after main was merged into it (the site job's pass=483)
- 486 as at 14 Oct 2026, measured once main (pull requests 84, 89 and 90) was merged into the NES models' notes
- 482 as at 13 Oct 2026, with the NES models' notes (issue 81: seven tests, the frame's corners against their marks, the footprint furthest from its spacing, the outside model's simplifications, the stage's green tint, the PAL photographs' lens, U9's jobs from the redrawing's nets, and the status line once a missing console is drawn again)
- 475 as at 6 Oct 2026, after the NES models' final fix wave (two tests of the NES browser check's wait for a camera to come to rest)
- 473 as at 6 Oct 2026, with task 10 of the NES models (five tests for mutations that had survived: the parts against the fact sheet, a change of console in the scene, the board's mount, the case's sizes in its code and caption)
- 468 tests as at 6 Oct 2026, with task 8's follow-up (the photographs committed for the NES's outside model: credited, listed and hashed)
- 467 as at 6 Oct 2026, after task 9's fix round (a model's region path on a console it does not draw; tab keys with Alt, Ctrl or Meta left to the browser)
- 465 with task 9 of the NES models done (the rule that a machine claiming a model states case, the case's credits, a console the model does not draw)
- 462 with task 9's second stage of the NES models (the outside model: its entry, sizes, features, words, buttons and notes)
- 453 with task 9's first stage (the views of a cased machine as tabs: their markup, styles and loader)
- 447 after task 7's fix round of the NES models (two access tests: a count that falls is a new machine
- 445 with task 7, the board's inside model on the page, its legend, rates, results file and credits
- 423 with task 6, the page's panel.nes and its events
- 416 with tasks 0 to 5 and 8 merged onto the NES's branch, 369 before them, after the review of the NES's page
- 368 with the page and the NES counting, 351 with its registry record, try-it file and build, 342 before them that day with main's preset discs merged into the NES's branch
- 282 on main after the discs' second review, 292 on the branch.

## Where this is described

`site/README.md` says where the floor lives and how to raise it. Nothing in `AGENTS.md` or `CONTRIBUTING.md` named the floor.
