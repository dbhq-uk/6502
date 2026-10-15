---
title: "The registry's words made short: the photographs' descriptions, the notes and the try-it steps"
date: 2026-10-15
summary: "The machine pages' pass left the registry's text and the try-it files alone. Dan asked for all of the site's text to be short and plain, so this pass did them too: the photographs' alt text lost about a third of its words, the try-it steps and the notes were tightened, and the rights kept every fact and caveat, changing only where a sentence ran on."
order: 66
---

# 15 October 2026: the registry's words made short

Dan, 15 October 2026: "too much text where the app is; keep everything concise; make sure it is all verved". The machine pages' part of that pass kept `machines/registry.json` and the try-it files as they were, judging that tightening them would risk a caveat for a handful of words. Dan asked for all of the site's text to be done, so this pass did those files, under the same rules: every fact, name, date, hash and link stays, and the `rights` text, the project's own statement under AGENTS.md rule 4, keeps every caveat with the same force.

## What was measured

Words per running machine in the fields the site prints, counted before and after with a one-off Python script that splits each field on white space (not committed; the figures are a record of the day). "Captions and alt" is every photograph's `caption` and `alt`. "Try it" is each step's `do` and `says`, each control's `does` and the game's `credit`; the BBC Micro's and the Electron's try-it files hold only BASIC lines, so they have none.

| Machine | Rights | Notes | Captions and alt | Try it | Total before | Total after |
|---|---|---|---|---|---|---|
| KIM-1 | 65 to 65 | 42 to 42 | 293 to 265 | 112 to 109 | 512 | 481 |
| BBC Micro | 101 to 101 | 49 to 49 | 562 to 436 | none | 712 | 586 |
| Electron | 140 to 137 | 55 to 55 | 90 to 60 | none | 285 | 252 |
| Atari 2600 | 167 to 165 | 84 to 81 | 255 to 156 | 320 to 313 | 826 | 715 |
| NES | 148 to 153 | 62 to 60 | 390 to 330 | 219 to 217 | 819 | 760 |

## What was decided

The alt text took most of the cut. It is not on the screen, but a screen reader reads all of it, and the longest ran past a hundred words for one photograph. Each now names what the photograph is of, from where, and the parts a reader would look for: the switches' labels, the ports' names, the words on the case, and the viewing angle and positions that tell a visitor what the picture shows. A review put back several details that the first cut had dropped, among them the Econet socket, which a shorter list had made look like part of the D socket.

The rights changed least, on purpose. Each still names the holder, the copyright lines, where our copy came from, the commit, the address, the SHA-256 and what is not known, including "no licence or permission from any holder was found". They changed only in commas, clause order and where two sentences are split; the NES's reason for using the Internet Archive now comes before the address it explains. The KIM-1's "who holds it now" does not say whether "it" is the copyright or the ROM, and a shorter sentence would have had to choose, so it keeps its original wording and lost one comma. The Atari 2600's game credit in its try-it file is unchanged because it is a credit line.

The notes became lists with semicolons where the Atari 2600's and the NES's ran several items with commas inside items together. The KIM-1's, the BBC Micro's and the Electron's were already plain lists and are unchanged.

The try-it steps lost words, not steps. Each still says what to press and what the screen then shows, because a visitor checks the machine against it.

Left as they were: the photographs' and references' `used` text and the references' titles, which are credits saying what was taken from each source and which the model tests read phrase by phrase; the planned machines' notes and the chip table, which are already a line each; the try-it files' `about`, which the site does not print; the KIM-1's teletype session; and the BBC Micro's disc library text, which is already one line per disc.

## What the tests hold

No test changed. The tests that read these fields compare the page with the registry or the try-it file, so they follow the new words, and the ones that look for a phrase (the rights' caveats, the Atari 2600's and the NES's copyright lines and hashes, the notes' lists of boards and parts left out, the KIM-1's "Not modelled" sentence) still find it. The Atari 2600's page fault probe, `tools/probes/atari-2600/page-faults.py`, deletes the rights' second copyright line by its exact words; that sentence is unchanged, so the probe still plants its fault.
