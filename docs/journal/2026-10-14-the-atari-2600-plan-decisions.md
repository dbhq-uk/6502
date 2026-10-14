---
title: "The Atari VCS plan: Dan's answers"
date: 2026-10-14
summary: "Dan answered the six open points of the Atari VCS plan. The bundled game goes in a cartridge folder under the machine's own directory, by the rule that already covers discs, which now names cartridges. The speed run reuses the BBC Micro's hand-started workflow pattern, one per machine that needs one, and the rule in the repository's guide says so. The picture alignment is fixed once on a calibration test, the suite's real-console captures are left out of the first version and tracked in an issue as recorded-only evidence, and the other two points stand as the plan had them. The spec, the plan and the guide are amended; no code was started."
order: 52
---

# 14 October 2026: the Atari 2600 plan, with Dan's answers

The [plan](../superpowers/plans/2026-10-14-atari-2600.md) ended with six open points, and its [journal entry](2026-10-14-the-atari-2600-plan.md) said they went back to Dan. He answered them the same day. This entry records the answers and what changed because of them. It is a docs-only change: no code was written.

## The answers

| # | Point | Dan's answer, 14 October 2026 | What changed |
|---|---|---|---|
| 1 | Where the bundled game lives | In `machines/atari-2600/cartridges/2048/`, by the rule for bundled programs | `AGENTS.md` says cartridges are covered the same way discs are. The spec and plan name the folder: the image, the MIT licence text as its author published it, a README with the provenance, the pinned commit and the SHA-256, an SPDX manifest, and a `manifest.json` that lists it |
| 2 | A second speed workflow in CI | Reuse the BBC Micro's pattern: started by hand, on a GitHub runner, built only when task 8 needs it, the rule fixed before any run, the load checked, the verdict printed. Dan approves each when it is built | `AGENTS.md`'s `bench/` sentence now makes speed workflows that are started by hand and never part of a pull request check the one exception, one per machine that needs one. Task 8 asks for his approval of the finished workflow instead of for an exception |
| 3 | "Every code block compiled and run" | Accepted: the plan's code blocks are interfaces only | Nothing; the plan already said so |
| 4 | Each file's hash "in `Pins.cs`" | Accepted: pins go through a manifest whose hash is pinned in `Pins.cs` | Nothing; the plan already said so |
| 5 | The suite's two statements about which lines a reference starts on | Fix the alignment once, on a calibration test whose picture is unambiguous | Item 4 of the plan's "Fixed before running" says so; it was already the plan's method |
| 6 | The suite's real-console captures | Not compared in the first version | The spec and plan say so. [Issue 162](https://github.com/dbhq-uk/6502/issues/162), a sub-issue of the Atari 2600 epic, tracks them as recorded-only evidence: compared in the acceptance run, where a change fails CI, and not gating |

## What was chosen over what

**The cartridge folder.** The plan had followed the spec and the NES, which keeps Lan Master in `roms/nes/`. That folder holds system ROMs, and the rule for a bundled program is the one that asks for the licence text, a verbatim statement and a manifest. Dan chose the rule's folder. The NES's Lan Master predates the rule. It stays where it is, and moving it is a separate chore.

**One workflow per machine, not a single exception.** The first workflow was approved on 13 October as the single exception. Dan's answer makes the BBC Micro's workflow the first of a kind. The guide now says what the kind is, so the next machine that needs a speed run does not reopen the question, and each workflow still needs his approval when it is built.

**The captures.** They are stacked stills off one console's RF output, so they cannot be compared pixel for pixel. Recording the outcome and failing CI on a change gives their evidence a place without making a failure against them mean our TIA is wrong.

## What was checked

The paths in the spec and plan were searched for the old folder (`grep -rn 'roms/atari-2600'` over `docs/superpowers`), and none is left. The design and plan journal entries describe the points as they stood on the day and are not changed.
