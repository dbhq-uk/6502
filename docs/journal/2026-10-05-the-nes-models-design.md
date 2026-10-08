---
title: "The NES's models: what they can be built from"
date: 2026-10-05
summary: "The NES gets two models, the outside and the board, as its own piece of work after the machine. Before any design, a search for freely licensed sources found a bare board scanned on both sides, public-domain photographs of the American console and its board, and a Creative Commons set of a European one. Dan chose both consoles, inside and out, and live rates on the board. The design reuses the BBC Micro's views, and a first task checks the inputs, with rules for when the work stops."
order: 29
---

# 5 October 2026: the NES's models, the design

The NES's design hands its 3D models to their own project, in a second pull
request after the machine's (Dan, 5 October 2026). This entry records how that
project's design was made: what sources exist, what Dan chose, and what the
design took by precedent. Nothing was built. The design is
[`../superpowers/specs/2026-10-05-nes-models-design.md`](../superpowers/specs/2026-10-05-nes-models-design.md),
the sources are in [`../nes/facts/models.md`](../nes/facts/models.md), and the
plan is
[`../superpowers/plans/2026-10-05-nes-models.md`](../superpowers/plans/2026-10-05-nes-models.md).

## What sources exist

A research agent searched Wikimedia Commons, GitHub, Printables, Thingiverse,
GrabCAD, the nesdev forum and the ConsoleMods wiki, downloaded the most useful
candidates to `~/dbhq-previews/nes-model-research/` and hashed them. I then
checked the claims the design rests on myself:

- **The bare board scans are as good as claimed.** OpenTendo's two scans of a
  bare NES-CPU-10 are 2376 by 1492 pixels at 300 dots an inch, both faces, with
  the print sharp enough to read every part's name. Looked at on 5 October
  2026; the size and resolution read with Pillow 12.1.1.
- **The licence is the repository's, not the scans'.** OpenTendo's README says
  "Licensed under the TAPR Open Hardware License (www.tapr.org/OHL)"; GitHub's
  API reports the licence as `NOASSERTION`; the scans carry nothing of their
  own. Read with `gh api repos/Redherring32/OpenTendo/readme` and
  `gh api repos/Redherring32/OpenTendo --jq .license` on 5 October 2026.
- **The scans are where the research said, at the commit to pin.** At
  `3bd0b0be5c9ed6fc6a36d9e458bc58d9976b2009`, the head of `master`, the two
  files have the byte sizes of the downloads (6354680 and 6710399), from
  `gh api "repos/Redherring32/OpenTendo/contents/Scans?ref=3bd0b0be..."`.
  No `dbhq-uk` fork exists yet (`gh api repos/dbhq-uk/OpenTendo` returned
  404).
- **The corner photographs have little perspective.** Evan-Amos's four
  photographs of the NES-001 were taken on a Nikon D7000 at 112 mm, and his
  two of the NES-CPU-07 on a D7100 with a 60 mm macro, read from the Exif with
  Pillow on 5 October 2026.
- **The PAL board looks like the same layout.** The CC BY 4.0 photograph of an
  NES-CPU-11 "PAL-EEC" board, looked at beside the NTSC scan, has its chips and
  connectors in the same places. That is an impression, not a measurement: the
  design makes it task 0's check.

What was not found: any dimensions from Nintendo, a bare PAL board, a PAL
solder side, or any open model of the case that is not someone else's 3D model
or non-commercial.

## What Dan chose

Asked one question at a time:

1. **Which console.** I recommended the NTSC NES-001, because its inputs are
   clearly better (bare scans of both faces, public-domain photographs, the
   patent's views) and its chips are the default region's. Dan answered "Both
   inside and out". That reads two ways, so I asked again: Dan chose **NTSC and
   PAL, both views**. Four models in effect, over the NTSC console alone.
2. **What state the models show.** Dan took the recommendation: **the power
   LED, POWER and RESET outside, and chip marks with live rates inside**, over
   the BBC Micro's marks alone or the outside's state only. The reason is the
   NES's own: a game reads the PPU and the pads every frame, so a mark alone
   would stay lit and tell the visitor little.
3. **The design, in three parts** (what is built and from what; the state, the
   host calls and the page; the proof, the stop rules and the order). Dan
   approved each as written.

## What the design took by precedent

Recorded in the design's decisions table, each with what it was chosen over,
and told to Dan before the design was presented:

- **No Nintendo logo shapes**, as the BBC Micro's owl was left out. The words
  on the case are drawn in the site's own face. The board's printed legend is
  traced as it is, as the BBC Micro's is.
- **OpenTendo is forked into `dbhq-uk` and pinned** before anything reads it
  (rule 3), and the track map traced from its scans carries the TAPR OHL's
  terms.
- **The console as made**, in the site's grey tokens, not yellowed as the 2022
  photographs show it.

And three decisions of the design's own:

- **One module per view, drawing both consoles**, chosen over four modules
  with a region on each registry claim. The registry's rules from the BBC
  Micro's work stay as they are; `MODELS` gains `regions`. The consoles share
  most of their geometry, so one bundle per view carries both, and a region
  switch needs no download.
- **The PAL board reuses the NTSC copper only if a measurement says so.** Task
  0 registers the PAL photograph to the NTSC scan and compares every IC's
  footprint centre against limits fixed in advance. If the layouts differ, the
  PAL models stop and Dan decides whether NTSC goes on alone.
- **The NES reuses the BBC Micro's two views and waits for them**, rather than
  building its own tabs. The BBC Micro's models branch has its registry rules
  (its task 1) but not yet its tabs (its task 9).

## Mistakes

- **I asked a question whose answer could be read two ways.** "Which console
  should the NES models show?", with two consoles as the options, drew the
  answer "Both inside and out", which could mean one console for both views or
  both consoles. A second question settled it. A clearer first question would
  have named the third option, both consoles, outright.

## The plan, and what was run before it was written

Dan approved the written spec, and the plan followed:
[`../superpowers/plans/2026-10-05-nes-models.md`](../superpowers/plans/2026-10-05-nes-models.md).
Every code block in it was run before it went in, twice: once in a scratch
copy while it was written, and once more by taking the blocks back out of the
plan's own text and applying them to a fresh copy of the repository at
`ead017d`, as an implementer would. Nothing from either copy was committed. On
the fresh copy, on 5 October 2026:

- `/tmp/bbcvenv/bin/python -m pytest tools/nes-model/tests -q`: 22 passed
  (the BBC Micro's shared tool code with the NES's names, and two new tests
  for the KiCad reader).
- `NES_MODEL_INPUTS=/tmp/nes-inputs python3 verify.py`: all 13 inputs
  matched their recorded SHA-256, with the folder holding links to the
  research's downloads.
- `node --test` on the five new or extended site test files and the mirrors
  test: 18 passed. Two of the input tests were also shown to fail, on a
  changed hash in the fact sheet and on an original copied into `tools/`; the
  test of task 0's recorded verdicts passed on a made-up `spike.json` whose
  verdicts match its figures and failed on one whose do not.
- `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release`, warnings as errors,
  in a scratch project with the repository's build settings: 19 passed.

Read along the way, each with the command:

- **The KiCad redrawing's outline is 196.252 by 118.700 mm**, and it names
  every IC, from the plan's `kicad_outline_box` and `kicad_footprints` run on
  the downloaded file. Its two 74HC368s carry the values "40H368 (CI)" and
  "40H368 (CII)", as the scan's print does, which is the first evidence for
  which buffer serves which controller port. The design was corrected to say
  so; task 6 checks it against the nesdev wiki.
- **Every licence was confirmed on Commons itself**, through its API's
  `extmetadata` for each of the ten Commons files: public domain for Evan-Amos's
  and the patent's, CC BY 4.0 for PantheraLeo1359531's. One date differs: the
  NES-CPU-07 solder side's Exif says 15 July 2015 and Commons says 13 July.
  The fact sheet now gives both.
- **The nine Commons file URLs answer** (HTTP 206 on a one-byte range request
  to each).

Decisions taken while writing the plan, each with what it was chosen over:

- **The scan's y scale comes from DIP row spacings on drill centres**, over
  pad centroids. Every DIP on this board lies the same way, so pin pitch gives
  only x; the BBC Micro's task 0 found solder centroids read row spacings
  about 1 per cent wide, so the holes' rims are used, and the 600-mil and
  300-mil chips are reported apart as a check on that bias.
- **Task 0 has eight checks, not four.** The design's four become x, y and x
  against y for the scale, the solder side, the PAL layout, the case's depth,
  the case's height (no stop: the published 88.9 mm is the fallback, said on
  the page), and the PAL front against the NTSC front (a stop for the PAL
  models only).
- **The counters' arithmetic is tested where it is used.** A C# test of a
  wrapping counter would have needed a test-only setter; the wrap matters in
  the page, which takes differences, so it is tested there.
- **The sampler resets on a new machine.** A region change builds a new
  machine whose counters start from zero, so a difference across it would show
  a rate of billions. The sampler drops its last snapshot on `nes:start` and
  `nes:region`, and divides by the time it measured, not the time it asked
  for. Both are tested.
- **`$4016` writes count on the CPU's row**, over the pads'. A write is the
  strobe, on the CPU's own output pin; only reads pass through the 74HC368s.
  The design was corrected.

## Mistakes, while writing the plan

- **The first C# test of the counter's wrap did nothing.** It held a loop that
  broke at once and an assertion on two constants. Caught on reading it back
  before the plan was written; the test was dropped and the wrap is tested in
  the page's code, above.
- **The first sampler design lived in the three.js module**, which node would
  have had to load to test it. Moved to the plain module beside `accessRates`.

## Approval

Dan approved the written spec, and then the plan, on 5 October 2026, and chose
subagent-driven execution (a fresh agent per task, each reviewed before the
next) over doing it in one session. This task's brief ended at the written
design and plan, so no model, tool or site code was started. Execution begins
with task 0, whose first step asks Dan whether to fork OpenTendo into
`dbhq-uk`.
