---
title: "The Atari VCS's bundled game, its boot check and its acceptance test"
date: 2026-10-14
summary: "The twelfth task of the Atari VCS: the bundled game, a port of the tile-joining puzzle by Carlos Duarte do Nascimento under the MIT licence, is committed in its own folder with its licence, its provenance and its notice. It draws its title screen in both regions, the frames are recorded and were looked at, and the acceptance test calls the suite's checks and the boot check in both regions."
order: 66
---

# 14 October 2026: the Atari 2600's bundled game, its boot check and its acceptance test

Task 12 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md), done on 15 October 2026, after task 11's review was folded in as its own commit (recorded in [the task 11 entry](2026-10-14-the-atari-2600-suite-in-full.md)). New: the folder `machines/atari-2600/cartridges/2048/` (`2048.bin`, `LICENSE`, `README.md`, `manifest.json`) and the list `machines/atari-2600/cartridges/manifest.json`; `machines/atari-2600/expected-frames.json`, written by the recorder; `machines/atari-2600/try-it.json`; `BootCheck`, `BootTests` and `Atari2600AcceptanceTests` in the tests; the fault probe `tools/probes/atari-2600/boot-faults.py`. Changed: `Pins.cs` (`Atari2600HomebrewPath` and `Atari2600HomebrewSha256`), `NOTICE.md`, `roms/README.md`, `AGENTS.md` (the `NOTICE.md` line), `.gitattributes`, and `SuiteSelfTests` and `SuiteRecordedTests`, whose checks the acceptance test now calls.

## The file, fetched once

The design's rights pass chose 2048 2600 and fixed the file: the author's own build, `2048.bin`, at commit `35de3c1b3b7bc4c3aadc8cf7fe630050a6984761` of `chesterbr/2048-2600`, 2048 bytes, SHA-256 `c85dc2204299b0a29209d3f80ae112678c171dab216719593625647d2436f1e1`. It was fetched once by hand on 15 October 2026, into a new empty directory outside the repository, then copied in:

```
curl -sS -f -o "$D/2048.bin" https://raw.githubusercontent.com/chesterbr/2048-2600/35de3c1b3b7bc4c3aadc8cf7fe630050a6984761/2048.bin
stat -c %s "$D/2048.bin"     # 2048
sha256sum "$D/2048.bin"      # c85dc2204299b0a29209d3f80ae112678c171dab216719593625647d2436f1e1
```

Both matched, so the plan's stop did not apply. No test or build fetches it: the tests read it from the folder through `RepoPaths.ReadChecked`, which checks the hash every time (rule 3).

**One judgement call on reading.** The controller allowed one fetch, of the binary. The folder must also hold the licence "as its author published it" (rule 4), and `NOTICE.md` the notice "as the source carries it", so the repository's `LICENSE`, `README.md` and `2048.asm` were read at the same commit through the GitHub API (`gh api -H "Accept: application/vnd.github.raw" repos/chesterbr/2048-2600/contents/<file>?ref=35de3c1b...`), into a second new directory outside the repository. Only `LICENSE` is committed, byte for byte; its git blob hash, `b2abb55904a1a7dcb35ce431327a7af7a25413bd`, is the one the tree at that commit gives. The commit history of `2048.bin` and `2048.asm` was listed the same way, to check the dates the spec gives; they hold (below). Nothing else was read from that repository but its tree listing at the pinned commit (`gh api repos/chesterbr/2048-2600/git/trees/35de3c1b...`), which gave `LICENSE`'s blob hash and the names of the label pictures the README lists; none of it is a test input. (The last two sentences were corrected by task 12's review: the first version left out the tree listing.)

## What it showed, looked at

The recorder (`ATARI_RECORD=1 dotnet test tests/Dbhq.Machines.Atari2600.Tests --filter Record`, run on 15 October 2026) wrote `expected-frames.json` and a picture of each region's window to `TestResults/atari-2600/`. Its output gave 262 lines for the NTSC frame and 312 for the PAL frame. Both pictures were opened and looked at:

- **NTSC, frame 120, colour switch at colour.** A black screen with eight tiles near the middle, in two rows of four, reading 2, 0, 4, 8 and 2, 6, 0, 0, in several colours (purples, a pale pink, a pale blue, a blue and a pale cyan), and below them on the right, in gold, the author's handle in two lines, "CHEST" over "ERBR". The 8 tile is a very dark teal at this frame, hard to see against the black.
- **PAL, frame 100, colour switch at black and white.** The same title, lower in the taller window, in other colours (dark reds, greens, a pink, a cyan, a dark teal and a magenta), and the handle in a yellow green. The game draws its title in colour with the switch at black and white: on this cartridge the switch picks NTSC or PAL timing, as its README says, not a grey picture.

So 2048 2600 draws a clear picture in its first seconds, and **the spec's fallbacks were not needed**: Core Escape was not fetched, and nothing was asked of Dan.

A scratch test, not committed, looked further on the same day: every frame of the title from the second on differs from the one before it, because the tiles' colours change every frame; the first frame after power on has 48 lines, the part of a frame left when the program first ends VSYNC; pressing the left fire button for ten frames starts a one-player game, a grey board of four by four cells with one 2 tile and a row of six zeros above it for the score; and pushing the joystick left slides that tile to the left edge and puts a new 2 tile on the board. The same was seen in both regions. **What this means for the browser check (tasks 13 and 14):** the title changes every frame, so the canvas must be compared at exactly the recorded frame count, not near it.

The picture was not compared with any other emulator's: none was run.

## Decisions

- **Two hashes a region, as the plan's interface asks.** `colours` is the SHA-256 of the whole last frame as TIA colour numbers, so it does not depend on the palette; `rgba` is the SHA-256 of the region's window (`FrameBuffer.ToRgba`, 160 by `Region.ShownLines`) written out as a canvas holds it, each pixel's red, green, blue and alpha bytes row by row. Chosen over one hash of the frame through the palette, because the canvas shows only the window and the colour numbers should be checkable without the palette.
- **The frame counts and the switches are in the file per region**, and the check refuses a file recorded for another count or switch, as the NES's refuses another count. The counts and switches themselves are the plan's item 7, fixed before running, and were not moved.
- **The sound check counts what was read.** The sample ring is a quarter of a second deep, so `BootCheck` reads it after every frame. The check is the plan's: the samples read are at least what the run's time gives at the sample rate, less one frame's worth, where the run's time is its CPU cycles over the region's CPU clock; and none were dropped. Chosen over the NES's extra check that the title is not silent, which the plan does not ask for: whether the title plays a sound was not checked, and sound was not listened to.
- **The frame file is read once, when `BootCheck` is first used**, as task 11's review made `SuiteOutcomes` do: the recorder reads `BootCheck`'s fields before it writes, so a check run beside it compares with the hashes as committed, never with a file half written.
- **One recorder variable.** The boot recorder uses `ATARI_RECORD`, as the suite's recorder does, so the plan's one command records both files. It rewrote `suite-outcomes.json` byte for byte the same.
- **The acceptance test calls the checks, not copies of them.** `SuiteSelfTests` and `SuiteRecordedTests` now expose their checks as internal helpers, which their own rows and `Atari2600AcceptanceTests` both call; the trusted runs are kept once made, as the recorded ones already were, so a whole run of the project runs each program once. The acceptance test also calls the trust-split check (`The_trust_split_places_every_test_in_the_suite_exactly_once`), because "every trusted test passes" means nothing if a trusted test can drop off the list.
- **The folder's tests check its words.** `BootTests` checks that the folder holds its four files, that `LICENSE` and `NOTICE.md` carry the copyright lines, that the README carries the hash, the commit and the sentence that the cartridge art is not in the ROM, that the two manifests agree with the pin, and that `roms/README.md` points to the folder. Rule 4's paperwork can then not drift from the file without a test failing.
- **The folder is binary to git**, as the BBC Micro's discs are (`.gitattributes`), so `LICENSE`, which has no newline at its end as published, and the image are never converted; the README and the manifests are text.
- **Two manifests.** The folder's `manifest.json` is the game's full record with its SPDX id, as the plan asks; the list beside the folders names each cartridge with its path, licence and hash, as `machines/bbc-micro/discs/manifest.json` does for the discs.
- **The try-it is in the NES's shape** (`about`, `rom`, `title`, `author`, `licence`, `steps` of `do` and `says`, `controls`), with a `credit` and the licence's address added. The site's `loadTryIt` does not know `atari-2600` yet and would read its steps as the KIM-1's keys; only running machines get a page, so nothing reads the file until task 14, which must add its check. The steps say only what was seen above or what the game's README says (the two-player game and GAME SELECT were not tried).

## The rights, as written

The folder's README, `NOTICE.md` and `roms/README.md` say what the spec's rights pass found and nothing more:

- **The licence.** The repository's `LICENSE` is MIT, "Copyright (c) 2014 Carlos Duarte do Nascimento (Chester)". The source, `2048.asm`, carries the same licence at its end with a second line, "Original 2048 game Copyright (c) 2014 Gabriele Cirulli". `NOTICE.md` keeps the source's notice whole, both lines, with the repository's address and the commit. One finding: the README's "MIT license" link goes to Gabriele Cirulli's licence file for the original 2048, not to the repository's own; the repository's `LICENSE` and the source's notice are the author's own statements, so they are what is kept.
- **The history.** `2048.bin` was last changed on 8 April 2014 (`0694b5464e`), after Ben Combee's fix (`2590aaf12b`, 25 March 2014) and SvOlli's PAL support (`020c75b105`, 28 March 2014), which the README credits and whose contributions it licenses under the same terms. The source's one later change (`c8050d0d3c`, 2 September 2014) adds only a comment about a known score bug, which the bundled game has.
- **The cartridge art is not in the ROM.** Jenn Harrison's art is the label pictures in the repository, which are not bundled, shown or committed.
- **What is not claimed.** No statement from the author beyond his licence and README; nothing about the art beyond the README's credit; and nothing about Gabriele Cirulli's licence beyond what the spec's pass read on 14 October.

The registry's `rights` field and the page's credit are task 14's.

## The fault pass

The fault pass ran on 15 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/boot-faults.py <scratch>`), one fault at a time, building and running the whole Atari 2600 test project. G faults are in the game's files and pins, F faults are the recorded frame file changed by hand, S faults in the check's fixed switches and counts, M faults in the machine, and A faults in the suite's checks that the acceptance test calls. The figure in brackets is the number of failing test cases in each file.

| Fault | Caught by |
|---|---|
| G1 one byte of the game changed | `Atari2600AcceptanceTests` (2), `BootTests` (9) |
| G2 the game's pinned hash changed | `Atari2600AcceptanceTests` (2), `BootTests` (10) |
| G3 the licence's copyright line changed | `BootTests` (1) |
| G4 the art sentence dropped from the folder's README | `BootTests` (1) |
| G5 the folder's manifest names another licence | `BootTests` (1) |
| G6 the list names another hash | `BootTests` (1) |
| G7 the notice loses the second copyright line | `BootTests` (1) |
| G8 `roms/README.md` loses its pointer | `BootTests` (1), on the second run |
| G9 the try-it names another file | `BootTests` (1) |
| G10 the pin names the game outside its folder (task 12's review) | `Atari2600AcceptanceTests` (2), `BootTests` (10) |
| G11 the game's pinned commit changed (task 12's review) | `BootTests` (3) |
| F1 the NTSC window's hash changed by hand | `Atari2600AcceptanceTests` (1), `BootTests` (2) |
| F2 the PAL colours' hash changed by hand | `Atari2600AcceptanceTests` (1), `BootTests` (2) |
| F3 the NTSC frame count changed by hand | `Atari2600AcceptanceTests` (1), `BootTests` (2) |
| F4 the PAL switch changed by hand | `Atari2600AcceptanceTests` (1), `BootTests` (2) |
| S1 the check runs PAL with the switch at colour | `Atari2600AcceptanceTests` (1), `BootTests` (2) |
| S2 the check runs NTSC for 119 frames | `Atari2600AcceptanceTests` (1), `BootTests` (2) |
| M1 the machine draws every colour with bit 1 set | `BootTests` (3), `Atari2600AcceptanceTests` (6), and seven other files |
| M2 the machine draws nothing | `BootTests` (5), `Atari2600AcceptanceTests` (56), and eleven other files |
| M3 the sound makes half its samples | `Atari2600AcceptanceTests` (2), `BootTests` (2), `ResamplerTests` (6), `SampleBufferTests` (16) |
| A1 a recorded outcome changed by hand | `Atari2600AcceptanceTests` (1), `SuiteRecordedTests` (2) |
| A2 a trusted test missing from `Trusted` | `Atari2600AcceptanceTests` (1), `SuiteSelfTests` (1) |

**G8 survived the first run**, and the fault was the weak one: it changed only the first of the two places the pointer names the folder, the link's text, and left its address, which the test found. The fault was made to replace the whole link and run again with F1 and A1 (`... <scratch> G8 F1 A1`), after `BootCheck` began reading the frame file once, as below; all three were caught. The probe now also copies `docs/known-differences.md`, which task 11's review made a test read.

M2 is the one fault that makes the picture blank: `The_picture_is_not_blank` is among the five `BootTests` cases it fails.

## Task 12's review, folded in

A review of this task (commits up to `a409de8`) found five things, folded in on 15 October 2026 as their own commit, with no change to what the machine or any check does:

- **The art (rule 4).** The folder's README said the ROM's picture comes from `graphics.asm`, generated by `graphics_gen.rb`, both under the author's copyright and the MIT licence, and so the art's copyright "does not reach anything this repository ships". That is more than the folder needs to claim, and nothing here checked it. The README now says what was seen: the README's credit for the art, the four label pictures it names, that none of them is bundled, shown or committed, and, on the design's reading of 14 October, that none of the art is in the ROM. The journal's rights list lost the same sentence. `NOTICE.md` claimed nothing more and was left. The design's rights pass (`docs/superpowers/specs/2026-10-14-atari-2600-design.md`, "The art is not in the ROM") and its journal entry still carry the longer sentence; they record the rights pass of 14 October and were not edited.
- **What was read.** The note on reading said nothing else was read from the game's repository, but the tree listing was, for `LICENSE`'s blob hash and the names of the label pictures; the note now says so.
- **The try-it** said the title shows "the author's name, CHESTERBR"; the screen shows his handle in two rows, so it now says "his handle, CHEST over ERBR".
- **The plan** gained one line in task 14's step 1, and one in task 13's panel list, on what the browser check needs: power on at frame 0 with no input, the recorded switches, the recorder's definition of a frame (the first, of 48 lines, counts) and exactly the recorded count.
- **`BootTests`** typed the game's commit in the test, and compared `Pins.Atari2600HomebrewPath` with a copy of itself, which could not fail. The commit is now a pin, `Pins.Atari2600HomebrewCommit`, which the README, the manifest and `NOTICE.md` are each checked to name; and the path check is that the pinned file exists and is `2048.bin` in the game's own folder, so the folder's licence and README are the game's. The fault probe gained G10 (the pin names the game outside its folder) and G11 (the pinned commit changed), and G4 was run again, because the README's art section changed (`python3 -I tools/probes/atari-2600/boot-faults.py <scratch> G4 G10 G11`, 15 October 2026): the baseline had no failure and all three were caught, as the table below gives.

**The quotations and the history, checked again.** The review had not checked the folder README's quotations or the history's commits against the upstream repository, so they were read again on 15 October 2026 through the GitHub API, at the pinned commit unless named:

- `gh api -H "Accept: application/vnd.github.raw" repos/chesterbr/2048-2600/contents/<file>?ref=35de3c1b...` for `LICENSE`, `README.md` and `2048.asm`. `LICENSE` has the SHA-256 and the git blob hash the folder's README gives, and is byte for byte the committed one. The README's "Author and License" section is as quoted, with its link markup taken out and its line breaks joined, and so is its sentence on the TV TYPE switch. The notice at the end of `2048.asm` is, line for line, the one in `NOTICE.md` (compared with `diff` once the comment marks were taken off).
- `gh api repos/chesterbr/2048-2600/commits?path=<file>&sha=35de3c1b...` for `2048.bin` and `2048.asm`, and `gh api repos/chesterbr/2048-2600/commits/<sha>` for each commit named. `0694b5464e` (8 April 2014, "fixed 'always party' bug") is the last to change `2048.bin`. `020c75b105` (28 March 2014) has the message the README quotes. `2590aaf12b` (25 March 2014) is Ben Combee's and changes one line of `2048.asm`; its message is "Update 2048.asm" with a line on the fix, and the words the folder's README quoted, "Fixed the bug that prevented the last tile from being picked", are the repository README's credit, not the commit's; the folder's README now says so, and names `5d3d8b4ed4`, the same day, which rebuilt `2048.bin` with the fix. `c8050d0d3c` (2 September 2014) is the last change to `2048.asm` and changes comments only: the file before and after it, with comments and blank lines taken out, is the same, and the quoted sentence about the deviant binary is in it.
- `gh api repos/chesterbr/2048-2600/git/trees/35de3c1b...` for the tree: `LICENSE`'s blob hash, and the four label pictures.

## Commands

All on 15 October 2026, on the development box, in the worktree:

- `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~BootTests"` before the files existed: seven failures, for the folder's missing files, the missing notice, the missing frame file and the missing try-it; the five cases that only read the game or run it passed.
- `ATARI_RECORD=1 dotnet test tests/Dbhq.Machines.Atari2600.Tests --filter Record`, as above.
- `dotnet build -c Release`: no warnings. `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests`: every test passed.
- `python3 -I tools/probes/atari-2600/boot-faults.py <scratch>`, as above.
