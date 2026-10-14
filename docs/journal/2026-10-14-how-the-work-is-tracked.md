---
title: "How the work is tracked: an epic per machine, and a roadmap in git"
date: 2026-10-14
summary: "The open issues had no shape: machine parts, flaky checks, speed and the models' loose ends sat side by side. Each machine now has an epic, and so do the site and CI, speed, the models and the machines to come. Every open issue sits under exactly one, new issues record what was only in notes, blockers carry their reasons, and the order of what comes next is a file in the repository."
order: 48
---

# 14 October 2026: how the work is tracked

Until today the open issues were a flat list. A machine's missing parts, a flaky browser check, a speed figure and a 3D model's failed check all sat side by side, and several loose ends lived only in the notes of whoever had worked on them. Dan asked for the tracking to be structured properly. This entry records how, and why.

## What was decided, and by whom

Dan decided the shape, on 14 October 2026:

- **One epic per machine:** the KIM-1 (#99), the BBC Micro (#26), the NES (#100), the Electron (#51) and the Atari 2600 (#98, new). An existing issue was reused where it already was the machine's tracking issue, so #26 and #51 became epics rather than gaining a twin.
- **Three cross-cutting epics:** Site and CI (#102), Speed (#103) and Models (3D) (#28, reused for the same reason). A fourth, Machines to come (#101), was added to hold the ranked list of machines after the Atari 2600 (#122), which had nowhere else to go.
- **The 3D models' re-work is parked at low priority** under Models (3D): the NES's #78 and #79, and four new issues for the BBC Micro's gaps (#110 to #113). Each says what a re-try needs: a new check, fixed before it runs, because the old checks' failures have been diagnosed and a method could now be tuned to them.
- **The Atari 2600's design** was settled in an interview the same day and is written up in its own spec pull request (#104, entry `2026-10-14-the-atari-2600-design.md`). Its tracking issue is #105, and the four parts the spec leaves out or depends on are filed under #98 and blocked by it.

Chosen over: one epic per kind of work (machines, site, speed) with the machines as labels. Rejected because each machine is a unit of work that ends (its registry row counts), and the epic is where "done" for that machine is written down.

## How it is kept

The repository opts into Dan's `deskwork` skill, configured in `.github/deskwork.toml`. It files issues from a template (Context and Acceptance for a Task; Expected and Actual added for a Bug; Proposal and Out of scope for a Feature), checks for duplicates before filing, records each blocker with its reason in a comment on the issue, and writes `roadmap.md` from an order given with a reason for each item.

- **Sub-issues are the source of truth** for what an epic holds. Each epic's body says its status and what "done" means, and names its children for a reader, but the sub-issue list is what counts.
- **Labels:** `epic` marks the nine epics. `priority:low` marks what is parked. The machines keep their own labels, and `atari-2600` was added. The skill's area labels (`area:core`, `area:ci`, `area:models`, `area:speed`, `area:rights`) are kinds of work that cut across machines; the machines were not repeated as areas, because two labels for one machine would drift.
- **Triage:** the skill puts `triage` on every issue it files. It was taken off the issues Dan named, because he had already decided them, and left on the two found in the notes and filed on judgement (#120, a stray character on the KIM-1's teletype paper, and #121, whether the committed GPL disc fits rule 3), for Dan to read.
- **Issue types:** Bug, Feature and Task, the organisation's three. The epics are Features.

## What was checked

The live list of open issues was read before anything was moved (`gh api repos/dbhq-uk/6502/issues?state=open`), and it had changed from the list in the brief: #91, the flaky NES panel wait, was already closed. After the moves, `deskwork review --json` showed every open issue under exactly one epic, and the epics under none.

The notes from the merge train of 13 and 14 October 2026 were read for anything not yet tracked. Each item found became an issue with its sources cited: the journal's duplicate orders (#115), the site test floor held in two workflows (#116), the throwaway CI branches (#117), the BBC models' camera that once did not settle (#118), the scans behind the BBC board's track map (#119), and the two above.

## Blockers, and what was not made a blocker

Each blocker was recorded with `deskwork link` and a reason. The four Atari 2600 issues are blocked by the spec (#105), because nothing for the machine is built before it merges. The ranked list (#122) is blocked by the Atari 2600 epic, because its acceptance waits for that machine to count. Three proposed edges were rejected and recorded as rejected, so they are not proposed again: the NES's scanline renderer (#72) and lazy APU (#73) are options that #67 lists, not work #67 must finish first, and the BBC Micro's copper re-trace (#111) does not wait for the NES's (#78).

## Not done, and why

- **The board.** Dan asked for a Projects v2 board as well. The GitHub token lacked the `project` scope when this was done, so the board was not created and the config leaves `project` commented out, with a note saying how to finish: grant the scope, create the board, put its id in the config, and run `deskwork init` and `deskwork intake`.
- **A mistake, caught.** Two issues filed one after the other (#117 and #118) had their labels and their places in the roadmap's order swapped by number. Reading each title back found it before the roadmap was written, and both were corrected.
- **Nothing was closed, assigned or prioritised** beyond the labels above. #44 stays open: its last item, five wording nits from a review, was never written down and cannot be recovered, and a comment on it says so.

## Later the same day: the Atari 2600's issues, as its spec lists them

Dan answered the Atari 2600 spec's questions and its pull request (#104) merged. Three answers changed what had been filed in the morning. The Superchip variants (F8SC, F6SC and F4SC) are in the first version, so they are not a not-modelled issue. Both test forks now exist in `dbhq-uk`, so the fork issue (#109) is done and a comment on it says so; it stays open for Dan to close. The bundled game is 2048 2600, with Core Escape as the fallback.

The spec's section "Forks and issues" asks for an issue per part it leaves out, finer than the morning's split, and those were filed under the Atari 2600's epic (#98): the paddles (#108, retitled from the morning's issue that held all three controllers), the keypads, the driving controller, other peripherals, the analogue picture with PAL colour loss, detecting the board from the image, the speed pass and the 3D models (#124 to #130). The speed pass and the models sit under the machine's epic, with the labels `area:speed` and `area:models`, rather than under Speed or Models (3D), so that the machine's epic holds all of its work. The bank switching issue (#107) became the parent of one issue per board not modelled (#131 to #155), each naming its tests in the suite where it has them, all at low priority.

Every one of them is blocked by the spec issue (#105), because nothing for the machine is built before its spec merges. The spec has now merged, and a comment on #105 says its acceptance looks met; closing it is Dan's, and it unblocks them.
