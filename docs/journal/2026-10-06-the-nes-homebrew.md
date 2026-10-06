---
title: "The NES's bundled homebrew, its boot check and its acceptance test"
date: 2026-10-06
summary: "The NES page needs one title it can run before a visitor loads their own. A rights pass through the fork's other/ folder and its authors' own pages found that only a few say in words that their work may be copied, and the clearest is Shiru, whose Lan Master manual says it is released into the public domain. Lan Master is now committed with its provenance, its title screen is the boot check's recorded picture in both regions, and NesAcceptanceTests runs nestest, every test ROM in the table, the boot check and the PRG RAM rule through the same checks the other test files make."
order: 31
---

# 6 October 2026: the NES's bundled homebrew, its boot check and its acceptance test

Task 13 of the NES plan. The NES has no system ROM, so the page has nothing to
run until a visitor loads a file of their own. The spec asks for one bundled
homebrew title, chosen by a rights pass before any page work, with a licence
that lets this repository carry it. Then three things are built on it: the boot
check (the title's picture after a set number of frames, against a recorded
hash), the acceptance test the registry will name, and the `try it` file the
page will render.

## The rule for a title

Ruling E of the run: the choice is not to wait for Dan. I run the pass, choose,
record the evidence and the runners-up here, and Dan can overrule before merge.
The safety rule with it: **a title qualifies only if its author says in words
that it may be redistributed, under a licence that can be named** (CC0, CC BY,
CC BY-SA, MIT, public domain, or an explicit "free to distribute"). A title whose
author says nothing does not qualify, even if it sits in the fork's `other/`
folder beside test ROMs. This repository is public and a committed file stays in
its history, so an unclear licence is a no.

It must also draw a clear picture with no input in its first seconds, and use a
mapper the machine has (0, 1, 2, 3, 4 or 7).

## How the pass was run

The `legwork` skill fans its research out to one agent per question. This task
was run by one agent that may not start others, so I did the same work by hand:
the readme of every title in the fork's `other/` folder at the pinned commit,
then each author's own page or archive, through the Internet Archive where the
page has gone. Every quotation below is from a file I opened, not from a search
summary. The iNES headers are read from the files at the pinned commit
(`gh api repos/dbhq-uk/nes-test-roms/contents/other?ref=95d8f621...`, then each
file from raw.githubusercontent.com, and bytes 6 and 7 for the mapper).

Search summaries said things that the files did not bear out, and two of them
mattered: one said Shiru's games were "CC0" (his own files say "Public Domain",
which is the stronger claim, and say it in the game's manual); another said
pulsar.nes was a 2011 demo, when it is most likely Neil Baldwin's music tracker.
Neither summary was used.

## The candidates

| Title | Author | Mapper | What the author says | Verdict |
|---|---|---|---|---|
| **Lan Master** | Shiru | 0 | The manual in his archive: "It is free of charge, released into Public Domain"; the cover "PD 2011 Shiru"; the title screen "PD2011 Shiru" | **Qualifies. Chosen** |
| Lawn Mower | Shiru | 0 | The same words in its manual; its title screen "PD2011 Shiru" | Qualifies. Runner-up |
| Chase | Shiru | 0 | `game.c` and `neslib.s` in the archive with the ROM: "Feel free to do anything you want with this code, consider it Public Domain" | Qualifies, on the code's header. Runner-up |
| Zooming Secretary | Shiru and PinWizz | 0 | The manual: "released under Creative Commons Attribution license", "CC BY 2011 Alexei Bespalko and Shiru" | Qualifies, with a credit to both authors. Runner-up |
| SNOW (`other/snow.nes`) | Repulse (Tennessee Carmel-Veilleux) | 0 | `other/snow.txt`, "Distribution License": "You can distribute this intro far and wide in both ROM and Cartridge form. HOWEVER, you cannot sell the data itself, you can only sell the medium." | Qualifies as "free to distribute", with a no-selling term. Not chosen (below) |
| Duelito (The Duel) | Hassán Hernández Benítez | 0 | Both readmes give the copyright, how to play and thanks, and nothing about copying | Does not qualify |
| Blade Buster | High Level Challenge | 4 | Its readme (OpenEmu's copy of `BladeBuster.txt`) under "紹介、転載について": "ファイルへの直接リンクや、ファイルの転載はご遠慮ください" (please do not link to the file directly or repost it), and "無許可での商用利用・転用を禁じます" (commercial use or reuse without permission is forbidden) | Does not qualify: reposting is refused |
| CMC'80s, Sayoonara!, and Chris Covell's other demos | Chris Covell | 0 and 3 | Both readmes describe the demo and ask for e-mail; neither says anything about copying. Sayoonara's music is "ripped from Ferrari Grand Prix" | Do not qualify |
| Years Behind | Retrocoders | 1 | The scene.org archive holds the ROM and scene.org's own text, no readme | Does not qualify |
| Quantum Disco Brothers | wAMMA | 3 | `wamma.nfo` in the archive: credits and thanks, nothing about copying | Does not qualify |
| minipack (2003 MiniGame Compo) | five authors, packed by Memblers | 0 | Only Escape from Pong carries a licence ("may be distributed or resued in any way without express permission so long as credit is given"); the other four games and the compilation say nothing | Does not qualify as a whole |
| Deadline Console Invitro | 8bitpeoples | 1 | The fork has the ROM alone; no licence found | Does not qualify |
| High Hopes | aspekt | 4 | No readme or licence found | Does not qualify |
| pulsar.nes | most likely Neil Baldwin (the Pulsar tracker) | 1 | No licence found; the GitHub address in the search results gave 404 | Does not qualify |
| Streemerz bundle, firefly | | 28, 5 | Not looked into: the machine has neither mapper | Out |

The rest of `other/` are test ROMs and emulator demos (nestest, the raster and
window tests, the litewall demos and so on), not titles for a visitor.

## The choice: Lan Master

**Chosen over SNOW** because SNOW's licence, though it allows copying, forbids
selling the data, and this repository is MIT: anyone may sell what they build
from it. A public-domain title carries no term to pass on. SNOW's music is also
by others (Memblers, from an original by Random), whom its licence does not
speak for. It stays what it was, the speed check's workload, fetched from the
fork and never committed.

**Chosen over Lawn Mower,** whose release is word for word the same, for what it
shows: Lan Master's title fades in and is still within a second, where Lawn
Mower's slides its letters in. **Over Chase,** whose public-domain line is in
its code's header and not in a statement about the game. **Over Zooming
Secretary,** whose CC BY licence needs a credit to two people kept with every
copy, which is more to get right for no gain.

**Where it came from.** Shiru's software page lists
`https://shiru.untergrund.net/files/nes/lan_master.zip`. On 6 October 2026 that
address answered `403 Forbidden`, and plain `http` refused the connection, so
the copy is the Internet Archive's. Its index gives the file one content digest
in every capture from 27 March 2016 to 16 September 2025, and the software page
as archived on 17 September 2026 still lists it at the same size. I took the
2016 capture and fetched the one of 15 August 2025 as a check: byte for byte the
same.
`roms/README.md` gives the address, the SHA-256 of the archive and of each file
taken, and the date. The ROM is pinned in `Pins.cs` as `NesHomebrewPath` and
`NesHomebrewSha256`, and read through `RepoPaths.ReadChecked`, which checks it
every time. The manual is committed beside it, because it carries the licence.
`NOTICE.md` points at `roms/README.md`; a public-domain release asks for no
notice, but the credit is given anyway.

## What the machine shows, and N

Before recording anything I ran the ROM in a scratch program outside the
repository: power on, run frame by frame, and print each frame's colour count
and a hash of its pixels. Frames 1 to 6 are one colour, or two in a single frame
while the game clears the screen; the title fades in over
frames 7 to 21 on NTSC (7 to 18 on PAL), with four, five, six and then seven
colours; from frame 22 on NTSC and 19 on PAL the picture is whole. After that
it changes only in two small places, found by comparing the frames: the little
computer beside the name (pixels 213 to 225 across, 61 to 72 down), which
animates, and the square cursor by START (168 to 175, 144 to 152), which blinks.
The two regions animate on different frames.

**N is 120:** two seconds on NTSC and 2.4 on PAL, a hundred frames past the
fade, at a frame where both regions show the cursor, so the two recorded hashes
are of the same picture. A frame inside the fade would have been the worst
choice: a change to the fade's timing would change the hash without anything
being wrong with the picture.

I also played it in the scratch program, as the `try it` file tells a visitor
to: Start on the title began level 1 (a board of wires and computers, "LEVEL01
DONE000% TIME020" along the top), Right then A moved the square and turned a
piece, and Start paused with RESUME, RESTART, MAIN MENU and the pass code. The
controls in `machines/nes/try-it.json` are from the game's source
(`lan_master_src.zip`, `game.asm` and `mainmenu.asm`): A turns a piece a quarter
turn one way, B the other way, Select half way round.

## The recording, and looking at it

`BootTests.RecordTheBootFrames` is the recorder. It does nothing unless
`NES_RECORD=1` is set, and then writes `machines/nes/expected-frames.json`
(the ROM's path, N, and each region's SHA-256 of the frame's bytes in the order
a canvas holds them: red, green, blue, alpha, row by row) and a PNG of each
region's frame to `TestResults/nes/`. The file is never edited by hand, and
the recorder checks that: run without the variable, it compares the committed
file byte for byte with what it would write, and fails on any difference. It
does that rather than skip itself, because the site's results refuse any
skipped test (`validateResults` in `site/src/lib/results.mjs`).

Recorded on 6 October 2026 at about 00:39 UTC with

```
NES_RECORD=1 dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --no-build --filter Record
```

I opened both PNGs. Each is the Lan Master title: the name in white on a blue
band across the top, the little computer to its right, a black strip with "PD2011
Shiru", and on the dark blue below, START with the square cursor beside it, CODE,
SFX and BGM. Nothing is torn, shifted or the wrong colour, and the two regions'
pictures are the same, as the run said they would be. Shiru's own screenshot on
his software page (`pic/lan_master.png`, through the Internet Archive) shows the
same screen, with the cursor at START; his emulator's colours are a little
different, which is the palette, not the picture. The two hashes in the file
are equal for that reason.

## The boot check

`BootCheck` holds the checks; `BootTests` and `NesAcceptanceTests` call them. A
run is made once per region and kept, since the machine is deterministic.

- **The frame** hashes to the recorded one. The test also checks the file is for
  this ROM and for the same N, so a change to N without a new recording fails.
- **The picture is not blank:** more than one colour, and at least one pixel in a
  hundred not the backdrop's colour (palette entry 0). The run on 6 October
  printed 7 colours and 24,653 of 61,440 pixels drawn in each region.
- **The sound.** The plan asked for at least `N / FramesPerSecond * SampleRate`
  samples, less a tolerance, and a non-zero sample. Two things changed on the
  way. The sound's ring holds a quarter of a second and drops the oldest, so the
  check reads it as the machine runs, counts what it read, and also checks that
  nothing was dropped; the tolerance is one frame's samples, both ways, because
  power on starts a few dots into the first frame and the resampler holds back
  its latency. And **a non-zero sample proves nothing**: the console's filters
  carry the step at power on as a tail that decays towards 0 and never gets
  there, so a silent ROM has non-zero samples too. On 6 October the loudest
  sample in the second half of the run was 1.7e-30 for nestest, which plays
  nothing, and 0.05 for Lan Master, whose title has music. The check is that a
  late sample is louder than 0.001, between the two; `BootTests` checks the
  silent side with nestest and prints both
  (`dotnet test ... --filter "FullyQualifiedName~BootTests" --logger
  "console;verbosity=detailed"`).
- **The sample count is the regions' timing check.** The two recorded hashes
  are equal, because both regions show the same picture at frame 120, so the
  picture does not tell them apart. The count does: 120 frames are about 95.8
  thousand samples at 48 kHz on NTSC and 115.2 thousand on PAL, each within one
  frame's samples, so a region that ran at the other's frame rate fails it.

The first run of the frame tests, before the recording, failed as it should,
saying the expected file does not exist and how to make it.

## The acceptance test

`NesAcceptanceTests` has one test for each thing the spec's "Proof" says counts,
and restates no number:

- `NestestRunsThroughTheRealBus`, both regions: the body of
  `NestestOnTheBusTests` moved into `AssertEveryLineMatches(Region)`, which both
  call.
- `EveryPinnedTestRomThatPassesPasses`, one Theory over every passing row, and
  `EveryKnownFailureFailsAsWrittenDown` over the known failures. **The tables
  that were in `BlarggTests` are now `TestRomTable`,** with each group's check
  as a static method. `BlarggTests` runs the same groups under the same test
  names and data as before (149 tests before and after); the acceptance test
  runs the whole table through the same methods. Two groups that were
  `[InlineData]` (the `sprdma_and_dmc_dma` pair and `branch_timing_tests`)
  became tables so the acceptance test could see them.
- `TheHomebrewBootsToItsRecordedPictureWithSound`, both regions.
- `BatteryBackedPrgRamSurvivesResetAndNotAPowerCycle`, for each of the six
  mappers: the plan's Review Focus 5 on the whole machine. The boards' own tests
  (task 10) hold it for a board alone, and `NesBusTests` had it for NROM with no
  battery; `PrgRamRule` builds a battery-backed cartridge with 8 KB of PRG RAM
  for each mapper, fills `$6000` to `$7FFF` through the bus with bytes none of
  which is 0, presses reset and reads them all back, then powers on and finds
  them all 0. `NesBusTests` runs the same Theory. To see that it can fail, I
  made the bus's reset clear the PRG RAM for one run: every one of those tests
  failed, with the old `ResetKeepsRamAndPrgRam...`, and passed again when it was
  put back.

**Each test ROM runs once in a run of the project.** Driving every ROM a second
time would have doubled the slowest part of the suite, so `TestRomTable` keeps
each result the first time it is asked for and both classes read it. The
machine is deterministic, so the second reader sees what a second run would have
produced. On 6 October (load average about 2 on 8 cores), with
`dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --no-build --filter
"FullyQualifiedName~NesAcceptanceTests"`, the acceptance test alone took 1 min
36 s and `BlarggTests` alone 1 min 39 s; the whole project took about 1 min,
less than `BlarggTests` took alone before the change (1 min 45 s, the same
command with `~BlarggTests` at the start of the task), because the two classes
now share the ROM runs across two threads.

## A test that failed once

In the first full run after the change, `SampleBufferTests.AddingAllocatesNothing`
(task 9) failed: 3,896 bytes were counted on its thread during a loop that
allocates nothing. It passed in the next three full runs and in fifteen runs on
its own. `Add` and `Read` allocate nothing, so the bytes were the runtime's own
work: the measured loop was in the test method itself, after only 10,000 calls
to `Add` and none to `Read`, so it ran while that code was still being compiled
and recompiled, and the busier suite made that more likely to land inside the
measurement.

My first change made the test pass if one of three passes allocated nothing. The
review rightly called that a retry. The test now warms `Add` and `Read` in the
same loop as the measurement, three passes of a million cycles, and measures one
more pass in a method of its own, marked `NoInlining` and
`AggressiveOptimization` so that it is compiled optimised once, before it runs;
the check is strict again (`Assert.Equal` of the counts before and after). I did
not reproduce the original failure on purpose: in a scratch program, the old
loop counted 0 bytes in 20 runs out of 20 with three other threads compiling
code beside it, and 0 in 20 with tiered compilation off. So the cause is a
reasoned one, not a proven one. What was checked is the new shape, on 6 October
2026: 10 full runs of the NES test project (`dotnet test
tests/Dbhq.Machines.Nes.Tests -c Release --no-build`, each 1,461 passed), 10
runs of `SampleBufferTests` alone, 10 beside a run of `BlarggTests` in another
process, and 5 with `DOTNET_TieredCompilation=0`, all passed.

## Left for task 15

- The registry's NES row is still `nes-famicom`, `planned`, with `rights` null.
  Task 15 replaces it with the `nes` record, writes `rights` from
  `roms/README.md`'s section, and names `NesAcceptanceTests`.
- `site/src/lib/machines.mjs`'s `loadTryIt` reads the BBC Micro's and the
  KIM-1's shapes. `machines/nes/try-it.json` is a third: `rom`, `title`,
  `author`, `licence`, `steps` (each a `do` and a `says`) and `controls` (each a
  pad `button` and what it `does`). The keys that press each button are the
  page's own map (task 14), so the file names pad buttons, not keys.
- The browser check reads `machines/nes/expected-frames.json` for the picture
  to match.
