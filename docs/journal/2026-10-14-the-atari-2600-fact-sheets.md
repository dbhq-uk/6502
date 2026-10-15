---
title: "The Atari VCS's fact sheets and the suite's pins"
date: 2026-10-14
summary: "The first task of the Atari VCS: six fact sheets written before any code, from Atari's programmer's guide, the chips' data sheets, a study of the TIA's schematics, Korth's specifications, three patents and the test suite's own headers. They corrected the plan in several places, among them which data lines a TIA read drives, which bank a hotspot read returns, which task each suite test waits for, when HMOVE combs a line and what the reference pictures are made of. The suite's files are pinned through one manifest of git blob hashes whose own hash is pinned, and the new test project checks it."
order: 53
---

# 14 October 2026: the Atari 2600's fact sheets and the suite's pins

Task 1 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): the fact sheets, the two projects, and the pins of the test suite. Nothing of the machine is built yet; the library is an empty project, and its test project holds only the suite's pin checks.

## The sheets, and how they were written

The sheets are in [`../atari-2600/facts/`](../atari-2600/facts/README.md): `bus.md`, `riot.md`, `tia.md`, `cartridge.md`, `region.md` and `suite.md`, with a README that lists every source and the tags. Each statement names the source it was read in, on 14 October 2026, and what could not be established is marked `[guessing - verify]` for the task that relies on it.

Five research agents drafted `riot.md`, `cartridge.md`, `suite.md` and the two halves of `tia.md` in parallel, from one brief and the same local copies of the sources; I wrote `bus.md`, `region.md` and the README, and read every draft before it went in. The brief forbade emulator code, and each agent reported what it opened. Stella's, Gopher2600's and missingno's code was not read; missingno's test harness was read for the protocol numbers only.

The sources: the Stella Programmer's Guide (Steve Wright, Atari, 1979), read through the Internet Archive because its usual address now answers "not found"; Andrew Towers's TIA hardware notes (2003), written from the TIA-1A schematics; Martin Korth's *Atari 2600 Specifications*; the MOS 6532 data sheets of 1977 and 1981, the 6530's, a Rockwell note on the 6532's timer, and the MOS 6500 and Synertek SY6500 data sheets for the 6507 and its RDY pin; Atari's Field Service Manual's motherboard schematics, in a 2024 redraw; Kevin Horton's bank switching notes; Atari's and CBS's bank switching patents; Brad Mott's 1998 late HMOVE measurements on the Stella list; Eric Ball's and David Galloway's posts on the TIA's colour and sound, the jigo2600 project's written analyses of the TIA's schematics, Stolberg's waveform guide as a cross-check, and the suite's headers. The TIA's own schematics were opened only for the labels of the audio and clock sheets; for the line, the objects and HMOVE, Towers's reading of them stands in, and `tia.md` says where that is thin.

## What the sheets changed in the plan

Each is corrected in the plan with a note naming the sheet. One line each:

- **A TIA read drives fewer lines than the plan said.** A collision register drives D7 and D6 (CXBLPF perhaps D7 alone, [inferring from "most"; guessing - verify]), an input port D7 only, and a read of `$xE` or `$xF` nothing (`bus.md` section 4, from the suite's `cpu/open-bus` and `cpu/partial-drive`). The spec's "a TIA read drives only data bits 7 and 6" is the same mistake; the plan's task 4 now says it right.
- **A hotspot read returns the new bank's byte**, not the old bank's, on F8, F6 and F4, as the suite's `bank-hotspot-window` asserts and Atari's patent allows; FA is the exception, because its D0 gate decides on the old bank's byte (`cartridge.md` sections 4 and 7.2). Task 2's test is changed. No Atari game can tell, because Atari put the same `NOP` at the hotspots of every bank. The caveat: the suite's cells for this are marked "untested on hardware", like all its cartridge tests, so the rule rests on the patent's reading and the suite's, not on a console.
- **The suite's tests wait for different tasks than the plan guessed** (`suite.md` section 10): `wsync` and `wsync-line-end` need only the halt and the timer and join in task 4, as do the four trusted cartridge tests; `rsync` needs a missile and a collision latch and joins in task 6; only five of the sixteen `collision/` tests and eight of the ten `tia-render/` self-tests can pass before HMOVE exists; and the `riot/io-*` tests use no joystick, so task 9 gains only `cpu/partial-drive`.
- **The reference pictures are RGBA**, colour type 6, every one; the plan's PNG reader read RGB and palette images only (`suite.md` section 4).
- **A recorded pass keeps its OBSERVED byte** where the test reports a measurement on a pass, or `power-on-bank`'s outcome would not say which bank the machine woke in (`suite.md` section 10).
- **The RIOT needs a read with no side effect** for the bus's `Peek`: reading INTIM clears the timer flag and sets the timer's interrupt enable, and reading TIMINT clears the edge flag (`riot.md` sections 6 and 7). Task 3's interface gains `PeekRegister`.
- **A joystick port bit set as an output reads 0 while its switch is closed**, if it was set to 1, by inference from the data sheet; task 9 decides and records it (`riot.md` section 5).
- **Collisions are set in the blank and under the HMOVE comb too**, and only VBLANK stops them (`tia.md` section 14, from the suite's `collision/blank-gating`); the plan said "as pixels overlap".
- **HMOVE combs the line it is written on only for cycles 1 to 20**; cycles 21 to 74 give no comb, and 75 and 76 comb the next line (`tia.md` section 16, from Mott's measurements and the suite's `hmove-line-end`).
- **INPT0 to INPT3 read as ports with nothing connected**, D7 = 0 whether the dump is on or off (before the first dump a guess, tia.md 20.3), because nothing in the console pulls them up; "a paddle at rest" would read 1 a while after each dump, which belongs to the paddles' issue, #108 (`tia.md` section 20.3).
- **The sound's worked bit patterns start from a chosen state**, all nine bits 1, because no document gives the power-on state; the plan's "from power on" now says so. The channels step at colour clocks 36 and 148, unevenly, and the mix is linear in the two volumes by choice (`tia.md` section 18).
- **The order inside a cycle cannot be measured alone.** Every source puts a write at the end of the CPU cycle, all three colour clocks before it; tests that start from WSYNC see the split and the release point together; and the delays a register adds after its write lands are the TIA's own paths, not a split (`tia.md` section 4). Item 8 of "Fixed before running" is unchanged and carries the note. With only `wsync-line-end` able to tell splits apart in task 5, a tie and its tie rule are likely.

## What the sheets found that the plan already allows for

- **The Superchip's filler rule misses a known game.** Korth says Superchip images "should be" filled with `$00` or `$FF` where the RAM hides them, but that Dig Dug and Omega Race carry garbage there. Omega Race is FA and chosen by its size; Dig Dug, a 16K Superchip game by Horton's list, would run as plain F6 and fail. The spec's risk "the Superchip by its filler" covers it, and issue #126 is the way out. On the suite's own files the rule gives exactly the plan's fixed list (item 6), measured below.
- **FA's D0 gate rests on CBS's patent and the suite**; Horton and Korth say any access switches. The sheet follows the gate, which agrees with the spec, and `bank-fa` stays recorded.
- **The power-on bank is undefined**; the sheet recommends the last bank, on the reasoning that boards with a fixed segment fix the last one. Task 2 chooses and records it.
- **The PAL colour clock's last digit**: Korth gives 3.546894 MHz, four fifths of the PAL subcarrier is 3.546895 MHz. The model will compute it from the subcarrier (`region.md` section 1).
- **The region switch at once rests on an inference**: no source compares the two TIAs' logic, but nothing a program sees differs and the suite calls its verdicts region-independent (`region.md` section 5). Task 11 can overturn it.
- **The picture window is a choice**: Atari's picture plus Korth's 18-line PAL border margin, above and below (`region.md` section 3), checked in tasks 12 and 13.
- **The palette is computed from the colour signal on NTSC only.** The NTSC TIA delays the colour clock by a chain of stages set by a potentiometer; the sheet takes 360/14 degrees a step, so hue 15 matches hue 1 as the service adjustment aims for, which gives fewer distinct colours than the 24-degree design figure would. No document says how the PAL TIA makes its colour, so the PAL hue angles are fitted to Korth's names and to the colours the suite's author confirmed on a PAL console. Both are choices, marked so, and one constant each (`tia.md` section 19). The bit-level sound logic and the colour delay chain come from the jigo2600 project's written analyses of the schematics; jigo2600 is an emulator, and only its documents were read.
- **Not every suite ROM starts with `CLEAN_START`**, as its README says: nine do not, eight of them trusted collision tests, which skip the long wipe on purpose so that the TIA's clock phase at start-up is not hidden. Their outcomes depend on the machine's power-on alignment (`suite.md` section 8).
- **The 6532's flag register and its timer around zero** are settled from the two data sheets and three suite tests, including the read on the cycle the timer passes zero; the 1981 sheet's own figure is one cycle off its text, and the 1977 sheet and the suite agree with the text (`riot.md` sections 6 and 11).

## The suite's pins

The suite comes from two forks, both MIT: [`dbhq-uk/missingno-vcs-tests`](https://github.com/dbhq-uk/missingno-vcs-tests) at `891c079976a2f68033ca4af2fe2fd85aaf1906c9`, for the tests' sources, headers and reference pictures, and [`dbhq-uk/missingno`](https://github.com/dbhq-uk/missingno) at `a4159f47ce7ef6f9c3d5402a8c58b18940045adc`, for the built binaries, which the suite's own repository does not hold. Nothing else of missingno is read.

**One manifest, not one pin a file.** The spec asked for each file's hash in `Pins.cs`. There are a few hundred files, so, as Harte's tests are pinned, `tools/atari-2600-suite-manifest.sh` reads each fork's tree at its commit with one call to GitHub's tree API, refuses a truncated listing, and writes `<git blob sha> <fork> <path>` for each file the tests may read; `Pins.cs` pins the two commits and the manifest's SHA-256. `Atari2600Suite.Read` checks the manifest against that hash before anything is used, then checks each download against its blob hash, and fetches only from `raw.githubusercontent.com/dbhq-uk/`. Dan accepted this form on 14 October. The `_pal_capture.png` and `_secam.png` pictures are not in the manifest: the first version compares neither (#162, and SECAM is #106).

On 14 October 2026 `tools/atari-2600-suite-manifest.sh` wrote 181 files from the suite's fork and 381 from the binaries' fork, and every blob hash matched `git hash-object` of the same file in local clones of the two forks at the pinned commits (a loop over the manifest, no mismatches). That every source has its three binaries and each binary its source, the spec's risk "the suite's binaries live in a second repository", is now a test, not a one-off check.

**The tests, test first.** `SuiteManifestTests` was written before `Atari2600Suite` and failed to compile; with the manifest written and a placeholder pin it failed all eight; with the real pin it passed. Then `tools/probes/atari-2600/manifest-mutations.sh`, run in a scratch copy of the repository on 14 October 2026, planted ten faults one at a time (a corrupted blob hash with and without the manifest pinned again, a binary, a source and a PAL picture dropped, a binary claimed by the wrong fork, a short hash, the manifest's first two lines swapped, an unknown name read anyway, and the downloaded bytes reversed). Each made at least one test fail, and the corrupted blob hash, once pinned, failed the download with "does not match its pinned hash".

**DASM** is not installed on the build box, so the optional check of assembling the suite's sources against the forked binaries was not done.

## The rest of the scaffold

`src/Dbhq.Machines.Atari2600/` and `tests/Dbhq.Machines.Atari2600.Tests/` are in the solution, in the shape of the NES's and the Electron's projects. The WebAssembly project arrives with task 8, and `machines/atari-2600/` with tasks 11 and 12; `AGENTS.md`'s layout says so. The README says the machine is in progress and does not count yet. The probes that read the suite for `suite.md` are committed in `tools/probes/atari-2600/`, so every count in that sheet can be made again.

## The review of task 1, and what it changed

The task's review found nothing critical and eight things to fix, all done the same day:

- **A guess was stated as fact.** That CXBLPF drives D7 alone rests on the suite's word "most" for the collision registers; `bus.md`, the plan and this entry now tag it `[inferring from "most"; guessing - verify]`, as `tia.md` already did.
- **Figures in `tia.md` came from scripts that were not committed**: the horizontal counter rule against Towers's table, the sound table and its comparison with Korth's drawings, the palette examples and their checks, and the delays read from the reference pictures. The scripts are now in `tools/probes/atari-2600/` (`towers_lfsr.py`, `tia_sound.py`, `tia_palette.py`, `png_rows.py`), each standard library only, and each printed the figures the sheet quotes when run on 14 October 2026: the rule matches all of Towers's rows, every `AUDC` period and pattern is the sheet's and Korth's repeat lengths agree, and the palette checks pass. One comparison, with Stolberg's waveform guide, was by eye and is marked unreproduced.
- **`tia.md` said a read register answers up to `$3F`** of its window; it is `$7F`, as `bus.md` says.
- **The spec still said four things the sheets overturn** (the floating bus, the collisions, the paddle ports and the HMOVE comb); each now carries a one-line note pointing to its sheet, and the spec is otherwise unchanged.
- **jigo2600's written analyses were not in the spec's sources.** The spec now names them as read for facts only, with no code read or used, for Dan to confirm. (Dan confirmed it the same day, during task 8: a source of facts only, with no code or text copied.)
- **The order test could not fail**, because `Atari2600Suite.Names` sorts in code. It now reads the manifest file's own line order, and the manifest tool sorts with `LC_ALL=C` so the order does not depend on the machine's locale (the manifest it wrote again was byte for byte the same). Swapping the manifest's first two lines, with the manifest pinned again, now fails it.
- **`suite.md`'s counts** now say they hold as of the pinned commits and the manifest's hash.
- **The hotspot line above** gained its caveat: the suite's cell is untested on hardware.

