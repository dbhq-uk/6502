---
title: "The order of the machines after the Atari VCS"
date: 2026-10-14
summary: "Dan fixed the order of the machines after the Atari VCS. The Atari eight-bit family is next, and the rest follow the research memo of the day before. The Commodore machine starts only when its ROM question is settled."
order: 60
---

# 14 October 2026: the order of the machines after the Atari VCS

This records a decision. The reasons are in [the memo](../research/NextMachines_20261013/NextMachines_20261013.md) and in [which machines next](2026-10-13-which-machines-next.md); they are not repeated here.

## What was decided

Dan chose the order on 14 October 2026, while the Atari 2600 was being built, with the memo's ranking as the basis:

1. The Atari 2600, in build (epic #98).
2. The Atari 8-bit family.
3. The BBC Master.
4. The Commodore 64.
5. The Apple II.
6. The VIC-20.
7. The PET.
8. The Apple I.
9. The Oric.

The Atari 8-bit family is next because its system ROM position is the cleanest of the computers on the list, it has a hardware-verified test suite, and one chip set gives the most rows. It is a large build.

The Commodore 64 stays fourth. Its KERNAL question (a licence from the holder, the open replacement, or a documented position under rule 4) must be settled before its spec is written.

The order after the Atari 8-bit family was kept as the memo ranked it. It was not weighed again, and it is open to change when the Atari 8-bit family is running.

## What would change it

The memo lists three things: the 2600 test suite disagreeing with real consoles, a ruling that variants count once per family (the BBC Master would then be second), and the Commodore 64's ROMs being granted to open emulators.

## What follows

The Atari 8-bit family gets its own epic. The memo's open questions for it (the licence files of AltirraOS and Altirra BASIC read from the files themselves, and a pinned fork of atari800 and Acid800 under rule 3) become sub-issues of that epic before its spec starts. No spec or code starts until the Atari 2600 counts.
