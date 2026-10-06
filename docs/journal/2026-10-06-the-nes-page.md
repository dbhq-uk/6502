---
title: "The NES's page, and the NES counts"
date: 2026-10-06
summary: "The NES gets its page: the panel from the day before on a page that says both regions are modelled, that the machine needs a fast computer, what is not modelled with an issue for each part, and whose the bundled game is, with a public-domain photograph of an original console. The registry says it runs, so the home page's count goes up by one, from the registry and never typed. On the way, the pull request turned out never to have been checked by CI, because it had conflicted with main from the day it opened."
order: 33
---

# 6 October 2026: the NES's page, and the NES counts

Task 15 of the NES plan: the registry record, the photograph, the page, the
browser check, CI and the count. The branch is not merged here; merging deploys
and changes the live count, and that is Dan's call after the final review.

## The pull request had never been checked

Before anything else: Validate had never run on this pull request. It opened on
5 October at 07:37 UTC, and main had already taken three merges the evening
before (the next four machines, #49; the BBC Micro's preset discs, #50; the
deploy's waits, #53) that touched the same files as the branch. A pull request
that conflicts with its base gets no `pull_request` run, so every "CI is the
real check" in the NES's task reports was a check that never happened. Found
with `gh pr view 55 --json mergeable` (`CONFLICTING`) when `gh pr checks 55`
said "no checks reported".

I merged `origin/main` into the branch rather than rebasing it, because the
branch is pushed and a rebase needs a force-push. The conflicts were all of
the "both added a line" kind and kept both sides: `.gitattributes`, `AGENTS.md`'s
layout, `NOTICE.md`, the README's machine paragraph and the site README's
test table; the test floor was measured again on the merged tree. The first
Validate run on the branch followed within a minute.

The merged tree had one failing site test, and it was the branch's own: the
status page test that the design's target, 25 times, is written exactly once
found it twice, because the NES's `SampleBufferTests` has 25 passing tests and
the status page lists every suite. The test now leaves the suites' table out
of that one search; the test before it still checks every number on the page
against the generated figures. Chosen over renaming or splitting a test to
change the count, which would hide the cause.

## The registry: one record, nes

Ruling Q: the record's id is `nes`, as the plan, `machines/nes/` and the model
rules (a model module starts with the machine's id and a hyphen) all say. The
planned row `nes-famicom` was **replaced**, not joined by a second NES row; a
grep for `nes-famicom` found it in the registry and in task 13's journal, which
is a record of its day and stays as written. The name is "NES", so the family
document's table row is now "NES" too (its test checks the two agree), and its
paragraph says the Famicom is the same console in Japan.

The record mirrors the BBC Micro's field for field, as the brief asked, with
two differences of substance:

- **The cpu field carries both clocks:** "Ricoh 2A03 at 1.79 MHz (NTSC), 2A07
  at 1.66 MHz (PAL)". The page reads the first figure in MHz and refuses to
  build without one. The NES's panel does not use it: the page script takes the
  clock from the machine (`NesHost.CpuHz`) after every load, because the region
  can change. The figures describe the console, not the project, and a test
  works them out from `Region.cs`'s dividers, so the field cannot drift from the
  machine.
- **A case and no model:** `case: true` and no `models` field. The 3D models
  are their own project on `feat/nes-models` (issue 68), and the registry rules
  already let a cased machine claim none.

The rights are written from `roms/README.md`: no system ROM; commercial games
are not bundled and are the visitor's own; Lan Master by Shiru, in his own
public-domain words from the manual; the Internet Archive capture we copied and
the archive's SHA-256. The brief asked that the rights and `try-it.json`'s
title, author and licence agree; a registry is JSON and cannot read another
file, so a test asserts they agree instead, and that the manual's sentence, the
archive's address and its hash are word for word in both the rights and
`roms/README.md`.

## The try-it file

`loadTryIt('nes')` threw before today, because it read every file but the BBC
Micro's as the KIM-1's keypad steps. It now has a NES branch: the title, author
and licence present; steps of `do` and `says`; controls of a pad button and
what it does; and last, the game named is the pinned file, read and checked
against its hash. A test feeds it nine made-up bad files and the pinned name
with the wrong bytes.

The buttons a control may name are a list typed in `machines.mjs` (`D-pad`, A,
B, Select, Start) and not read from `public/nes-keys.js`, with a test that the
two agree. The first version imported `nes-keys.js`, and the build script's own
test, which copies the files the build reads into a scratch folder, failed:
`machines.mjs` is read by the build, and nothing under `public/` is. Importing
it would also have put a page file into the machines' cache keys.

## The photograph

Searched as the KIM-1's and the BBC Micro's were: Wikimedia Commons, through
its API, for "Nintendo Entertainment System console" (50 file results), then
the original console's photographs looked at in full.

**Chosen:**
[`File:Nintendo-Entertainment-System-NES-Console-FL.jpg`](https://commons.wikimedia.org/wiki/File:Nintendo-Entertainment-System-NES-Console-FL.jpg),
by Evan-Amos, released by him into the public domain (`{{PD-self}}`). The
whole console from the front left and a little above, on white, sharp, 4020 by
2880 pixels, every part a visitor knows in view: the cartridge lid, the red
name, POWER and RESET, and the two controller ports. Its file page calls it
"the first game console released in America by Nintendo", so the North
American NTSC console; the picture alone cannot tell it from a PAL console,
whose case is the same, and the photographs' README says so rather than the
page claiming it.

**Not cropped.** The console sits in an even margin of white, about 240 pixels
on each side (ImageMagick's trim box at 2 % fuzz: 3520 by 2395 at 243, 249), so
it was only resized: `cwebp -q 82 -resize 1600 0 -metadata none`, 1600 by 1147
pixels, 32,014 bytes. I looked at the converted file before committing it.

**The runners-up, and why not:**

- `NES-Console-Set.jpg`, Evan-Amos, public domain, 5560 by 3020: the same
  console with a controller plugged in. Its lead crosses the front and the
  controller takes a third of the frame; the page is about the console.
- `Nintendo-Entertainment-System-NES-Console-FR.jpg`, Evan-Amos, public domain:
  the same console from the front right, which turns the buttons and the name
  away.
- `Nintendo Entertainment System video game console (53665518370).jpg`, Chris
  Williams, CC0, 2024: a straight-on front view, but dusty, on a wooden bench,
  with a hand at the left edge.
- `Nintendo Entertainment System NES (1985) 1.jpg`, Jzh2074, CC BY-SA 4.0:
  share-alike, and smaller.
- `Wikipedia NES PAL.jpg`, JCD1981NL, CC BY 3.0: a PAL console, but 1200 pixels
  wide.

**Why the photograph came with the count.** The site test that every
photograph in the folder is shown by some page fails for a photograph of a
machine that has no page yet, so the file, its registry entry and its README
section went in with the status change, as the BBC Micro's did.

## The page's decisions

- **No speed is typed.** The page says the NES "needs a fast computer to run at
  full speed", why (its picture chip is run a dot at a time, three for every CPU
  cycle on NTSC), and that the line under the screen, measured as it runs, is
  the speed. The only multiple a visitor sees is that live line from the shared
  host. A test fails the page on any "N times", "times as fast" or an MHz figure
  other than the two clocks in the head. The figures, and the decision about
  them, are issue 67, linked from the page: accept about two to three times, make
  the shared core's CPU path faster, or build the catch-up PPU Dan turned down on
  5 October.
- **The region sentence** says both consoles are modelled, what each is, that a
  cartridge starts in the region its file names or NTSC, and that the line under
  the control says which and why. The line itself is the machine's sentence, as
  task 14 built it.
- **The try-it section** shows the steps from `try-it.json` and a table of the
  controls with the keys that press each button, taken from the page script's
  own key map, so the table cannot name a key the page does not use.
- **What is not modelled** lists seven parts, each with its issue, in the order
  of the registry's notes, and says the 3D models are a project of their own. It
  says again, as the panel does, that saving is not built, so a reload loses
  battery RAM.
- **"Whose game this is"**, not "Whose ROM this is": the NES has no system ROM,
  and the one file the page serves is a game.
- **The download size** is the machine's folder as built, read by the page, as
  the BBC Micro's is; the rule is now any machine's (`machineDownloadBytes`),
  with the BBC Micro's preset discs still left out of its figure.

## The issues

Filed on 6 October 2026 with `gh issue create`, after `gh issue list` found no
NES issue, each with the `nes` label (new) and `enhancement`:

| Issue | What |
| --- | --- |
| 61 | NES: the Dendy |
| 62 | NES: the Famicom Disk System |
| 63 | NES: expansion audio |
| 64 | NES: the Zapper and other peripherals |
| 65 | NES: unlicensed and other mappers |
| 66 | NES: saving battery-backed RAM |
| 67 | Speed: the NES runs at about two to three times real time in the browser |
| 68 | NES: the 3D models, the outside and the board |
| 69 | NES: the picture's analogue quirks |

The analogue quirks were not on the brief's list, but the design names them as
left out and the page lists them, so they have an issue like the rest.

## CI

The NES has its own cache in both workflows, keyed as the BBC Micro's is (its
two projects, the core, `Directory.Build.props`, `Pins.cs` and the three build
files), and `nes` in the step that names the machines whose cache missed. The
deploy's floor of machine files rose from 21 and the discs to 30 and the discs:
the NES must publish at least its game and the eight runtime and assembly files
the other two count. Its three scripts joined the list the deploy checks are
serving.

What the NES adds to a run, from the first run that built it, Validate run
37405641205 on 6 October 2026 (`gh run view 37405641205 --json jobs` and the
jobs' logs), which passed every job:

- **The machines job** built all three machines, because the shared build
  files changed every key; the NES's own publish took 69 seconds of it (02:48:10
  to 02:49:19 UTC). The job runs beside the test job and finished first, so it
  added nothing to the run's length. With its cache restored it costs about a
  second. Locally the same build took 2 minutes 7 seconds (`time node
  scripts/build-machines.mjs nes`).
- **The test job:** the NES project's 1,496 tests took 32 seconds, beside the
  core's, which took 4.6 minutes in that run because the test data's cache key,
  which hashes `Pins.cs`, had changed with the NES's pins and the data was
  fetched again. The next run restores it.
- **The site job** took 4 minutes 2 seconds, before the NES's browser section,
  which the commit that makes the NES count adds to it.

## What was checked

All on 6 October 2026, on the dev machine.

- `dotnet test --configuration Release --logger trx` on the merged tree, then
  `node site/scripts/make-results.mjs`: 4,112 passed, 0 failed, in 84 suites;
  the NES project 1,496, `NesAcceptanceTests` 159. This results file, not a copy,
  is what the site was built against.
- `cd site && npm test`: 369 tests, 368 pass, 0 fail, 1 todo (the BBC Micro
  models' todo). Both workflows' floors are now 368.
- `node scripts/browser-check.mjs` with all three sections: passed in 3 minutes
  56 seconds, no console errors, no failed requests, no CSP violations. The NES's
  lines: nothing fetched before Start; running 763 ms after Start; 12 files,
  7,356,720 bytes; at 120 frames from power on the canvas hashed to the recorded
  `6d9ab957...062cc7` in NTSC, in PAL after switching (the line under the
  control changed to "The region changed, so the machine restarted. Running as
  PAL, as chosen here: the file does not say which."), and in NTSC again; Start
  on the keyboard changed 43,708 of 61,440 pixels against 88 in the same time
  without it; the test cartridge turned the screen one colour; the text file and
  the file one byte over 4 MiB were refused with the machine running on.
- The headroom line in that run read "Running at the NES's own 1.79 MHz, with
  little to spare in this browser." (capacity 2.21 MHz, load average 2.7 over
  five minutes). A separate run a minute later, at a one-minute load of 1.3,
  read "about 2 times as fast" for eight seconds, capacity 3.67 to 4.91 MHz. Both
  are this machine on this day; the page states neither.

## Mistakes

- **I trusted the task reports' "CI is the real check" until I went to read the
  run.** There was no run. A check that has never been seen to pass is not a
  check.
- My first version of the page test summed the whole built folder for the
  download size and counted the page's own `index.html`, which the build puts in
  the same folder. It now counts the machine's files.
- Sharing the BBC Micro's caption rule with the new controls table changed a
  selector in the stylesheet, and the contrast test, which lists every rule that
  sets a text colour, failed until the table named it.
