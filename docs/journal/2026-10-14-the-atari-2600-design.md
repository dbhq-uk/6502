---
title: "The design of the Atari VCS"
date: 2026-10-14
summary: "The Atari VCS is written up as the next machine, from Dan's answers to the design interview: a picture exact to the colour clock, with the HMOVE effects and the object quirks, and a speed pass planned for after it counts; NTSC and PAL in one row; the common Atari boards and CBS's RAM board; two joysticks and the console switches; a bundled homebrew; and an MIT test suite forked and pinned, with one expected frame of the game. The rights pass opened each candidate game's licence file and recommends Core Escape, which is dedicated to the public domain. The suite turned out to keep its built test files in a second repository, and only part of it is trusted for the count. No code was started."
order: 47
---

# 14 October 2026: the design of the Atari 2600

The [research memo of 13 October](../research/NextMachines_20261013/NextMachines_20261013.md) recommended keeping the Atari 2600 next, as published on [4 October](2026-10-04-the-next-four-machines.md). Dan agreed, answered the design interview, and this entry is the session that wrote the spec, [`2026-10-14-atari-2600-design.md`](../superpowers/specs/2026-10-14-atari-2600-design.md). It is a docs-only change: no code, no fork and no issue was made.

## The decisions

Dan's six calls are in the spec's first table, each with what it was chosen over: a TIA exact to the colour clock, including late and early HMOVE and the object quirks, with a speed pass planned and built after the machine counts; NTSC and PAL in one row with a region switch, and SECAM left out; plain 2K and 4K cartridges and the F8, F6, F4 and FA boards, with every other board an issue; two joysticks and the console switches, with paddles, keypads and the driving controller as issues; a bundled homebrew with a clear licence; and the `missingno-vcs-tests` suite, forked and pinned, plus one expected frame of the game. The spec adds eight smaller decisions of its own, one line each, the most important being that no code is taken from any emulator, the MIT missingno included, because the suite was developed alongside it.

## What was checked, and how

**The suite.** Its README, its licence and its tree at `891c079` were read through the GitHub API and a clone. It has the tests' sources and reference pictures but no built ROMs. The README points to prebuilt files at a path in `ajoneil/missingno` that no longer exists; they are now under `crates/systems/vcs/tests/accuracy/roms/`, and the test names there match the suite's one for one (`diff` of the two name lists, 14 October 2026). So two forks are needed, not one, or a DASM build of our own, and the spec asks Dan which. Of the cartridge tests, the header of `power-on-bank` says it "is a diagnostic, not a pass/fail test", so it is recorded and not trusted, and `bank-fa` and `bank-hotspot-window` say "untested on hardware".

**The trust split.** The suite was checked on one PAL console, and its reference pictures are missingno's renders. So the self-tests that were checked on that console count, and the screenshot tests and the disputed cartridge tests are recorded: they run in CI, and a change in their outcome fails the build until it is written down. The screenshot comparison is made by colour partition, so it does not depend on missingno's palette.

**The bundled game.** Each candidate's `LICENSE` was opened through the GitHub API. Core Escape's is the full CC0 text, and its source says the code "was provided by me - Murilo M. Grosso - and it's free to use". 2048 2600's is MIT, with the cartridge art separately copyrighted and not needed. Berta And Butterflies is MIT, but its README calls it a remake of a Nintendo Game & Watch game, and its binary carries batari Basic's code under a licence not read. Core Escape is recommended, with 2048 2600 as the fallback. Both files were downloaded at a pinned commit and hashed; the hashes are in the spec.

**The photograph.** Two public domain photographs by Evan-Amos on Wikimedia Commons were found and their licences read through the Commons API.

**The RIOT.** The KIM-1's `Rriot6530.cs` was read against what the 6532 adds: twice the RAM, no ROM, a PA7 edge detect, a flag register and an interrupt pin of its own. The two are kept as separate classes, because what they share is small. The differences are checked against the data sheet in the plan's first task.

## What the memo got wrong, or did not know

The memo sized the machine at 10 to 14 tasks. The spec has 15, because Dan's call on exactness makes the TIA three large tasks, then about 6 for the speed pass. The memo did not know the suite's binaries had moved. Its note that the 2048 2600 and Berta licences were read from READMEs is now closed: both licence files were read.

## What is open

Four questions are Dan's, and are listed at the end of the spec: whether the Superchip boards come in, which way to get the suite's binaries, which game to bundle, and whether the trust split stands.
