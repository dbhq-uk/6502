---
title: "The machine pages made short: the machine first, the reference folded"
date: 2026-10-15
summary: "Dan asked for less text around the machines. Every machine page now has one shape: what the machine is, the machine itself, how to try it, and one short line saying whose ROM or game it runs. The rest, what is modelled, how it is checked, the photographs, the credits and the full rights, is in closed sections at the foot of the page, still in the page and still tested."
order: 67
---

# 15 October 2026: the machine pages made short

Dan, 15 October 2026: "too much text where the app is; keep everything concise; make sure it is all verved". This is the machine pages' part of that pass. The other pages and the margins were done separately.

## What was measured

The visible words of each page, measured before and after in Chrome at 1440 pixels wide: the `innerText` of `<main>` with the page loaded and nothing opened, so closed sections and hidden elements do not count. The machines' WebAssembly was not loaded. The command is `npm run build && node scripts/measure-page-words.mjs`, run in `site/` (set `CHROME_PATH` if Chrome is not at `/usr/bin/google-chrome`); the script is `site/scripts/measure-page-words.mjs`. "Before" is the same script run on a build of the base commit, `9568268`; "after" is the head of this pull request after the review fixes below. The figures are a record of the day, not a claim about the page now.

| Page | Words before | Words after | Page height before (px) | Page height after (px) |
|---|---|---|---|---|
| KIM-1 | 2625 | 939 | 10131 | 5567 |
| BBC Micro | 3844 | 1047 | 12594 | 5795 |
| NES | 2847 | 1118 | 9575 | 5641 |
| Acorn Electron | 1260 | 730 | 6498 | 4290 |
| Atari 2600 | 2034 | 1131 | 7795 | 4805 |

The first version of this entry gave other figures (833 words after for the KIM-1, for example), taken with a script that was not kept. Both columns were taken again with the committed script, and these replace them. The text in the built HTML, folded parts included, fell much less, because almost everything was kept: the cuts were wording, and the facts moved into folds rather than out of the page.

## What was decided

- **One shape for all five pages.** The head (name, year, maker and CPU, the acceptance count, the photograph with its credit), the machine, Try it, how to type at it (the KIM-1's teletype, the Acorns' keyboards), the 3D models, then one section, "About the" machine, of reference. Before, each page had its own run of sections, and the KIM-1's rights were six screens below its keypad.
- **One short rights line under the machine, never folded.** It names whose ROM it is, or the game, its author and its licence, and keeps the registry's caveat in the registry's words ("no licence or permission from any holder was found", and for the Acorns "documented, not cleared"). The full `rights` text, the hashes and the copies stay in the reference. The line is written in the page, not the registry, beside the page's description; the build stops if a computer's `rights` stops saying the caveat the line repeats.
- **Reference goes in closed `<details>`, not off the page.** What is modelled and what is not, with every issue; how it is checked; the discs' credits; the photographs with their credits; the full rights. Each summary is a plain label, because a screen reader reads it as the name of a button. The headings stayed outside the folds: a heading inside a `<summary>` loses its heading role in some screen readers. The chosen alternative, one heading over all the reference, also saves the space of five section paddings.
- **The main photograph's credit stays beside it in the head.** Only the gallery, each photograph still with its own credit under it, is folded.
- **Small print in the panels folds too, quietly.** The disc drive's details, how long a tape takes, the symbols tables, the teletype session and the model's sources and limits sit in the same component, `site/src/components/Fold.astro`, with no rules round it. Its style is scoped to the details and its summary and sets no text colour, so the panels' markup and the contrast table are unchanged.
- **The registry's notes, rights and photograph text are unchanged.** They were already plain lists of facts and legal positions; tightening them would have risked a caveat for a handful of words, and they are now folded. The try-it files are unchanged for the same reason: their steps are the instructions.
- **The chip legends are deliberately not folded.** The BBC Micro's and the NES's model sections hold most of those pages' remaining words, in the boards' chip tables. The table is live while the machine runs (a chip's row lights as the model's chip does), so folding it would hide the feature. The notes on how each model was made are folded under their heading.

## What the tests hold

`site/tests/machine-pages-shape.test.mjs` is new: the same order on every running machine's page, every reference part closed with a label, the head never folded and the photograph's credit in it, the rights line visible, short and true to the registry, and the full rights, every photograph's source and every issue link still in the page. The page tests that matched a section heading, such as "What is not modelled", now find the part by its id and check its label and that it is closed (`pagePart` and `foldOf` in `site/tests/helpers.mjs`). Every link, credit, licence and issue they asserted is still asserted.

The first CI run found one thing the local tests could not: the real-browser checks load every photograph, and a photograph inside a closed `<details>` is not loaded until it is opened. The five browser checks now open the photographs by clicking their summary, as a visitor would, before they check each one loads from this site. The test floor moved to the pass count that run printed.

## Review fixes

A review of the pull request found these, and each is fixed with a test that fails without the fix.

- **A link to a folded part landed on a shut fold.** Every `#rom` link, the rights line's "Full rights" link, scrolled to a closed `<details>`: the browser does not open one for a fragment. `site/public/fold-open.js` opens the fold a fragment names, or every fold round the element it names, when the page loads and when the fragment changes, and scrolls there, because the browser scrolled while the fold was shut. The brief asked for a small inline script. The site's content security policy has no `'unsafe-inline'` in `script-src` (`site/public/_headers`, and a test that no page has an inline script), so it is an external module, loaded by every machine page and listed in the deploy check. The BBC Micro's and the Electron's typing tip linked to the keyboard heading, which is not in a fold, so the symbols table below it stayed shut; it now links to the table's own fold, `#symbols`. `site/tests/fold-open.test.mjs` runs the script on a stand-in document and checks the links on the built pages; `site/scripts/browser-check-folds.mjs` opens each machine's page in Chrome at `#rom`, a changed fragment and a click on the rights line's link, and fails if the fold is shut. I ran it on the Atari 2600's page with the script emptied and it failed on the fragment, the changed fragment and the click; with the script it passed.
- **The one disc caveat that costs a visitor their work is back in the open.** The BBC Micro page's disc drive folded "Save disc is the only way to keep what was written" with the two limits (drive 0 only, type `*CAT` first). The open text now says "Anything written to a disc is lost unless you press Save disc", and the fold, now labelled "Disc drive limits", holds the rest.
- **The licence is a link in the console pages' rights line.** The test asserted the words "MIT licence"; it now asserts the link to the licence the try-it file gives. I took the link out of the page to check that the test failed. The KIM-1's line says that who holds the monitor ROM now was not established, as the registry does, and the NES's and the Atari 2600's say commercial games are not bundled "and never will be". The limit on the line's length stays at 40 words; the longest line is under it.
- **Wording.** The KIM-1's line is three short sentences, the link reads "Full rights: Whose ROM this is.", and "and says so" and "sending" are back where the first pass cut them.
- **What a machine models is said once, in the head.** The head has one sentence, "It models ...", from the first sentence of the registry's `notes`. The fold no longer repeats it, and no longer puts the notes' "Not modelled" sentence above a list of the same parts with their issues; the KIM-1, which has no issues to link, keeps the registry's sentence there. The build stops if a machine's notes are no longer those two sentences.
- **The folds of the 3D models are told apart.** Their labels now name the view: "What the outside was measured from, its sources and its limits", and the same for the inside and the board.
- **The credit for what the 3D models were traced from stays in the open.** The CC BY-SA attributions were inside the closed folds. Under each model's heading there is now one line with the author and licence of every source, linked to the fold that says what was taken from each. The line gives a licence by its name; the fold keeps the licence's whole text and note.
