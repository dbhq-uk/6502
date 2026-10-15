---
title: "The machine pages made short: the machine first, the reference folded"
date: 2026-10-15
summary: "Dan asked for less text around the machines. Every machine page now has one shape: what the machine is, the machine itself, how to try it, and one short line saying whose ROM or game it runs. The rest, what is modelled, how it is checked, the photographs, the credits and the full rights, is in closed sections at the foot of the page, still in the page and still tested."
order: 63
---

# 15 October 2026: the machine pages made short

Dan, 15 October 2026: "too much text where the app is; keep everything concise; make sure it is all verved". This is the machine pages' part of that pass. The other pages and the margins were done separately.

## What was measured

The visible words of each page, measured before and after in Chrome at 1440 pixels wide: the `innerText` of `<main>` with the page loaded and nothing opened, so closed sections and hidden elements do not count. The machines' WebAssembly was not loaded. The script was a one-off, not committed; the figures are a record of the day.

| Page | Words before | Words after | Page height before (px) | Page height after (px) |
|---|---|---|---|---|
| KIM-1 | 2608 | 833 | 10094 | 5410 |
| BBC Micro | 3764 | 936 | 12594 | 5637 |
| NES | 2847 | 1047 | 9575 | 5521 |
| Acorn Electron | 1203 | 688 | 6498 | 4260 |
| Atari 2600 | 2034 | 1071 | 7795 | 4751 |

The text in the built HTML, folded parts included, fell much less, because almost everything was kept: the cuts were wording, and the facts moved into folds rather than out of the page.

## What was decided

- **One shape for all five pages.** The head (name, year, maker and CPU, the acceptance count, the photograph with its credit), the machine, Try it, how to type at it (the KIM-1's teletype, the Acorns' keyboards), the 3D models, then one section, "About the" machine, of reference. Before, each page had its own run of sections, and the KIM-1's rights were six screens below its keypad.
- **One short rights line under the machine, never folded.** It names whose ROM it is, or the game, its author and its licence, and keeps the registry's caveat in the registry's words ("no licence or permission from any holder was found", and for the Acorns "documented, not cleared"). The full `rights` text, the hashes and the copies stay in the reference. The line is written in the page, not the registry, beside the page's description; the build stops if a computer's `rights` stops saying the caveat the line repeats.
- **Reference goes in closed `<details>`, not off the page.** What is modelled and what is not, with every issue; how it is checked; the discs' credits; the photographs with their credits; the full rights. Each summary is a plain label, because a screen reader reads it as the name of a button. The headings stayed outside the folds: a heading inside a `<summary>` loses its heading role in some screen readers. The chosen alternative, one heading over all the reference, also saves the space of five section paddings.
- **The main photograph's credit stays beside it in the head.** Only the gallery, each photograph still with its own credit under it, is folded.
- **Small print in the panels folds too, quietly.** The disc drive's details, how long a tape takes, the symbols tables, the teletype session and the model's sources and limits sit in the same component, `site/src/components/Fold.astro`, with no rules round it. Its style is scoped to the details and its summary and sets no text colour, so the panels' markup and the contrast table are unchanged.
- **The registry's notes, rights and photograph text are unchanged.** They were already plain lists of facts and legal positions; tightening them would have risked a caveat for a handful of words, and they are now folded. The try-it files are unchanged for the same reason: their steps are the instructions.
- **Not done: the 3D models' own notes and the chip legends.** The BBC Micro's and the NES's model sections hold most of those pages' remaining words, in the boards' chip tables. The table is live while the machine runs (a chip's row lights as the model's chip does), so folding it would hide the feature. The notes on how each model was made are now folded under their heading.

## What the tests hold

`site/tests/machine-pages-shape.test.mjs` is new: the same order on every running machine's page, every reference part closed with a label, the head never folded and the photograph's credit in it, the rights line visible, short and true to the registry, and the full rights, every photograph's source and every issue link still in the page. The page tests that matched a section heading, such as "What is not modelled", now find the part by its id and check its label and that it is closed (`pagePart` and `foldOf` in `site/tests/helpers.mjs`). Every link, credit, licence and issue they asserted is still asserted.
