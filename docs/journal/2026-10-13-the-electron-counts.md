---
title: "The Electron counts"
date: 2026-10-13
summary: "The Electron's page goes live in the build: a credited photograph of an original machine, the registry switched to running, and the home page's count of machines going up by one, from the registry and never typed. The preview that built the page before it counted is deleted again, and the page's tests and the browser check read the site that deploys."
order: 39
---

# 13 October 2026: the Electron counts

The second half of task 14 of the Electron plan. The first half (5 October, in [the Electron's fact sheets and plan](2026-10-04-the-electron-fact-sheets.md)) built the page and kept it out of the deployed site, because the registry said planned until a photograph was chosen. This half adds the photograph, switches the registry record to running, and lets everything that reads the registry follow: the machine's page, the machines table, the home page's count and its sentence about which machines run, and the sitemap. It is the BBC Micro's second half (`db46311`, [the BBC Micro counts](2026-10-04-the-bbc-micro-counts.md)) done again, step for step. The branch is not merged here; merging deploys, and that is Dan's call.

## The rule that lets it count

The registry's validator lets a machine say `running` only when it names an acceptance test that is in the test results and passed, with none failed or skipped, and has at least one credited photograph (`validateRegistry` in `site/src/lib/registry.mjs`; the home page's card says "A machine counts only once it runs in the browser and passes an automated test"). Both hold. `ElectronAcceptanceTests` is in the solution, so CI's `dotnet test` runs it and `make-results.mjs` puts it in the results the site is built from; it passed in CI on the branch's last two runs (the `test` job of runs 38419977068 and 38432176181) and here. The page runs the machine in a browser; the browser check that shows it is CI's (below).

## The photograph

Every file in Commons' `Category:Acorn Electron` was listed through the Commons API on 13 October 2026, sixteen files, and the photographs of a whole machine were looked at:

| File | Author, licence | Why not, or why |
|---|---|---|
| [`Acorn Electron (1).jpg`](https://commons.wikimedia.org/wiki/File:Acorn_Electron_(1).jpg) | simon.inns, CC BY 2.0 | **Chosen.** The whole machine, nothing plugged in, from the front left and above on a plain blue cloth, sharp, 5182 pixels wide, every key's legend readable. The same photographer and day as the BBC Micro's photograph, so the two Acorn pages match |
| [`Acorn Electron 4x3.jpg`](https://commons.wikimedia.org/wiki/File:Acorn_Electron_4x3.jpg) | Bilby, CC BY 3.0 | The closest rival: the whole machine on white, 3500 pixels wide, with the ports on its left end showing. Lower and flatter, so the keyboard's legends are small and foreshortened |
| [`Acorn Electron Tietokonemuseo.JPG`](https://commons.wikimedia.org/wiki/File:Acorn_Electron_Tietokonemuseo.JPG) | MKFI, public domain | In a glass museum case at Helsinki, with reflections across the case and the frame |
| [`Acorn Electron (2225182042).jpg`](https://commons.wikimedia.org/wiki/File:Acorn_Electron_(2225182042).jpg), and `Acorn Electron.jpg` and `Acorn Electron 2.jpg` made from it | Marcin Wichary, CC BY 2.0 | A Plus 1 is fitted behind it, which the page lists as not modelled |
| [`Acorn electron - Flickr - map.jpg`](https://commons.wikimedia.org/wiki/File:Acorn_electron_-_Flickr_-_map.jpg) | map, CC BY 2.0 | Dark, behind glass, among books and tapes |
| [`MadriSX Retro 2007 IMG 2767.JPG`](https://commons.wikimedia.org/wiki/File:MadriSX_Retro_2007_IMG_2767.JPG) | Oscar Orallo, CC BY-SA 2.5 | Straight on from the front at keyboard height on a wooden table, 3264 pixels wide, so the keys are seen edge on; its two companions show the rear and the underside |

The other five are not photographs of a whole stock machine on its own: a Plus 1 fitted (two files), the Plus 3, the ULA's die, a group of Acorn machines, and a logo.

**What the source says, and what the picture can show.** The file page says "An Acorn Electron computer" and no more. The photograph does not say which issue of the Electron it shows: the issues differ on the board inside, and the case and keyboard look the same. The photographs' README says that and no more, and the page's caption says "an original Acorn Electron".

**Checked before use.** The original, fetched from `upload.wikimedia.org` with a User-Agent naming the site, was 2,021,110 bytes, 5182 by 3456 pixels, and its SHA-1 was `c43fe9121348351261836287525f6aff190b4c5c`, the SHA-1 Commons records for the file. The Commons API gave `LicenseShortName` CC BY 2.0, `AttributionRequired` true, the artist simon.inns, `DateTimeOriginal` 2018-02-18 11:47:27, and FlickreviewR 2's review of the Flickr licence, passed on 14 September 2021.

**The copy, made without cwebp.** The other photographs were made with `cwebp -q 82 -resize 1600 0 -metadata none`. `cwebp` is not on this machine and Pillow is not installed, so the copy was made with sharp 0.35.5, which the site already depends on, with its own libwebp 1.6.0, at the same settings: run with Node from `site/`, `require('sharp')(original).resize({ width: 1600 }).webp({ quality: 82 }).toFile('electron.webp')`. It gave 1600 by 1067 pixels, 105,866 bytes, SHA-256 `6da3b7f8...511e706c`, and the same hash when run a second time. One difference from cwebp is real: the original carries an Adobe RGB profile. sharp converts the pixels to sRGB through it and writes no profile; `cwebp -metadata none` would have dropped the profile and kept the numbers, which shows the colours a little flatter. A pixel sampled from both ways of reading it differed (`bd ad 9b` converted against `b8 ab 9a` as stored), so the conversion happened. The README says so. Not cropped: the whole case sits inside the frame with cloth on every side, so "resized and converted to WebP", which `MachinePhoto.astro` writes under every photograph, is the whole of the change.

**The on-screen keys, checked against it.** The first half read the page's on-screen rows from the keyboard matrix and said this photograph would be the check. On the photograph the rows are ESCAPE, 1 to 0, `=` and `-`, `^` with left, `|` with right, BREAK; CAPS LK FUNC, Q to P, `£` with up, `_` with down, COPY; CTRL, A to L, `+` and `;`, `*` and `:`, RETURN; SHIFT, Z to M, `<` and `,`, `>` and `.`, `?` and `/`, SHIFT, DELETE; and the space bar. The page's rows are the same, key for key, but for BREAK, which is a button of its own because it is not in the matrix, and the second SHIFT, which is the same matrix position as the first (`ula.md` s7b). Looked at, not measured.

**The licence and the credit.** CC BY 2.0 asks for attribution and an indication of changes. The credit the page builds from the registry gives the author, the source linked, the licence linked to its deed, and "Changes: resized and converted to WebP for this page." A new test checks it as built. The repository's README lists CC BY 2.0 for the Electron's photograph beside the BBC Micro's.

## The registry, and the preview deleted

The record had its `acceptance`, `rights` and `notes` already; now `status` is `running`, `cpu` gains its clock, `6502, 2 MHz`, as the first half's preview fixture had it, and `photos` is a list of one in the BBC Micro's field order, with `used` saying the photograph is shown and checked against and nothing is traced or measured from it. The family document's row matches.

Then the preview went, as it did for the BBC Micro: `REGISTRY_PREVIEW`, `applyPreview`, the fixture, `SITE_DIST`, the temporary build in `electron-page.test.mjs` and the run of the whole suite on it, and the second build in the browser check, whose Electron section now runs on the same server as the KIM-1's, the BBC Micro's and the NES's. With them went the tests the preview gave meaning: that the deployed site has no Electron page, that the home page's count does not move, that no workflow sets the preview, that the preview builds, the two tests of the preview itself, and that the KIM-1's and the BBC Micro's pages are byte for byte the same with the Electron switched on. In their place: the registry says running, names the acceptance test, gives the clock and has the photograph; the page is linked from the machines table; the home page's count is the registry's running count and its sentence names the Electron; the photograph is credited as its source gives it; and the not-modelled list's issues are 57 to 60.

The preview's nested run of the whole suite was also what failed CI on the branch's last run before this one (run 38432176181, on `6a945fc`): inside it, an NES page test that the outer run passed failed, and the known to-do test in `bbc-models.test.mjs`, which the test runner reports as `not ok` with a TODO directive, was counted by the nested run's check as a failure. Both went with the nested run.

## The count

Nothing on the home page was edited. Built with the new registry (`npm test` in `site/` builds first), `dist/index.html` has `>4 of 51<` in the machines-implemented card, where the deployed site has 3, and "Running in the browser now: KIM-1, BBC Micro, Electron and NES.", in the registry's order. The sitemap lists `/machines/electron/`. The home page's three machine cards are the registry's `priority` 1 to 3, the KIM-1, the BBC Micro and the NES; the Electron has no priority and is not given one, so the cards and their "The first three" stay true.

## CI

- **The site test floor.** `npm test` in `site/`: 526 tests, 525 pass, 0 fail, 1 to-do, against 529, 528 and 1 before. Eight tests went with the preview, six in `electron-page.test.mjs` and two in `electron.test.mjs`, and five came in, four and one. Both workflows' floors are 525, with the history in the comment.
- **The deploy's floor of machine files.** Counted in `dist/machines` after the build, not index pages: 69 files, the KIM-1's 13, the BBC Micro's 30 (17 of them discs), the NES's 12 and the Electron's 14 (`os.rom`, `BASIC.ROM` and twelve framework files). The floor is what every machine must publish at the least, its ROMs and eight framework files each (the four runtime files, CoreLib, the core, the machine and its host), so the Electron adds ten: 40 and the discs, up from 30. Not the measured 52, because a .NET update that drops an assembly the machines do not name would fail a good deploy. Both steps that use it, before the deploy and at the edge, now say 40.
- **The Electron's AOT build and cache** were added to both workflows in the first half and are unchanged.
- **What the Electron costs CI, from real runs.** Its AOT publish, on a cache miss, took 68.7 seconds in the `machines` job of run 38419977068 (13 October: 15:16:13.5 at its `dotnet publish` line to 15:17:22.2 at its `site/public/machines/electron` line, against 76.6, 71.7 and 68.0 for the KIM-1, the BBC Micro and the NES in the same run); a cache hit costs only the restore. That job runs beside the .NET tests, so it adds to the wall time only when it is the longer. Its test project took 15.7 seconds ("Total time", run 38432176181), in parallel with the other projects. Its browser check section took 26.9 seconds on 5 October (run 37311085494, from "electron: page opened" to the line after its images), on the preview build, which took another 3.5 seconds to make. The section on `dist/`, without that build, took 27.1 seconds in the `site` job of run 38433835321, the run on `34827aa`, measured the same way later the same day (16:58:37.265 at "electron: page opened" to 16:59:04.393 at the line after its images).

## What was run

All on 13 October 2026, on this aarch64 machine, with other work on it.

- `npm test` in `site/`, with the four machines built by `node scripts/build-machines.mjs kim-1 bbc-micro nes electron` earlier the same day and `results.json` from that day's `dotnet test` runs: 526 tests, 525 pass, 0 fail, 1 to-do.
- `dotnet test tests/Dbhq.Machines.Electron.Tests -c Release`: "Passed!  - Failed: 0, Passed: 511, Skipped: 0, Total: 511, Duration: 24 s". No .NET code changed in this half; the five projects' runs earlier the same day, after main was merged in, are in the fact sheets' entry.

**Not run.** The browser check (`npm run browser-check`) needs Chrome, and this machine has none. CI runs it. Its Electron section changed only in where it gets the site from: it was given a server of its own on a preview build, and is now given the one the other three sections use. What it checks is unchanged: Start, the prompt, `PRINT 6*7` typed through the page's keys reading back 42, the bad tapes refused in the reader's words, a program saved, downloaded, dropped back in and loaded with `LOAD ""`, and no console errors, failed requests or CSP violations. The KIM-1's, the BBC Micro's and the NES's sections are unchanged.

## Mistakes

- The first query to the Commons API carried a User-Agent with a contact address in it. The others, and the download of the photograph, named the site only, as the BBC Micro's fetch did.

## Final review fix wave

A final review of the branch made eleven findings. Each was checked before it was acted on. None was rejected; two were met in a different way from the one suggested, and both are said below.

- **Contention "exactly" (design entry).** The summary and the paragraph claimed more than the check of plus or minus 0.05 s shows. Both now say "access by access", which is what the model does.
- **"Pixel for pixel, as its ULA draws it".** On the Electron's panel and in `site/DESIGN.md` it now says "as the model of its ULA draws it". The BBC Micro's and the NES's panels were left alone. No page test pins either string.
- **"FUNC and K types CHAIN" had no test through the OS.** Added `BootTests.FuncAndKTypeTheKeywordChainThroughTheOs`: it presses FUNC (the Caps Lock position), then K, and reads `>CHAIN` off the picture. Finding: the two pressed in the same instant did not give CHAIN, so the test presses FUNC first, as a person does. It can fail: with K moved to another matrix position the screen reads `>LIST` (run once, then reverted). `dotnet test -c Release tests/Dbhq.Machines.Electron.Tests`: 512 passed. No site test was added, so both workflow floors stay as they were.
- **"Under a hundredth of a second" in `known-differences.md`.** The fact sheet gives the figure with a pointer to an `io1` case in its section 4g, and that case is not in the sheet or in any test here. The figure is dropped from `known-differences.md` and the entry says no run measures it. `ula.md` still carries the sheet's own estimate (section 3 and section 12 item 2), marked there as a guess; it was not touched.
- **Journal order.** `the-electron-mutation-pass.md` was order 40, as was PR 90's entry, with PR 92's at 41. It is now 42. No other entry on this branch or on PR 90 uses 42.
- **"Four came in" in both workflows.** It is five (529 - 8 + 5 = 526). The comment was corrected and the floor was not touched.
- **Plan jargon in `known-differences.md`.** "The brief fixed a constant level" now points at `ula.md`'s description of S of 0 and 1. "The task 10 probe" now names `TapeProbe` and `TapeProbeTests`.
- **The `LOAD` figure in `tape.md`.** The sheet's 11,116,283 cycles is the probe's run. A clause now says so and gives the production cassette's 11,116,233, as the page and the journal do.
- **Hard-wrapped prose.** The new sections of `known-differences.md` and the Electron paragraph added to `README.md` are one line a paragraph or list item. Only lines this branch added were changed.
- **Build order in `AGENTS.md`.** It said KIM-1, BBC Micro, Electron, Atari, NES, Commodore 64, which is the plan's order. The history is KIM-1, BBC Micro, NES, Electron (the README says the NES is the third and the Electron the fourth). `AGENTS.md` now says that, with the Atari 2600 and the Commodore 64 to come. The same wrong order was in two sentences of `README.md` and was fixed there too.

`npm test` in `site/` after the fixes: 526 tests, 525 pass, 0 fail, 1 to-do, the same as before.
