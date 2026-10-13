---
title: "Which machines next, after the Electron"
date: 2026-10-13
summary: "A research pass weighed nine machines for the place after the Electron, against the repository's rules on ROMs and inputs, what the core and the browser host already cover, the test material that exists, and the BBC Micro's and NES's records as the yardstick. It recommends keeping the Atari VCS next, as published in October, and moving the Atari eight-bit family ahead of the Commodore Sixty-Four. It is a recommendation for Dan, not a decision, and no machine code was started."
order: 41
---

# 13 October 2026: which machines next, after the Electron

The order set on [4 October](2026-10-04-the-next-four-machines.md) was the Electron, the Atari 2600, the NES and the Commodore 64. The NES is now running and the Electron is in review, so the rest of the order was looked at again before the 2600's spec is written. The memo is [NextMachines_20261013](../research/NextMachines_20261013/NextMachines_20261013.md), with its fetch log beside it.

## What was weighed

Nine machines: the Atari 2600, the Commodore 64, the VIC-20, the Apple II, the Atari 8-bit family, the Oric, the PET, the BBC Master and the Apple I. For each: what the core and the shared browser host already cover, read from the code; the chips that would be new; the system ROM and what is known of its rights, with a pinned copy we could fork under rules 3 and 4; the acceptance test that exists and whether it can be redistributed; a size in plan tasks; and how many registry rows it could make count.

The yardstick is the two machines already built from a plan. As counted on 13 October 2026 by `grep -c -E '^#{2,3} Task'` on each plan, the BBC Micro's plan has 15 tasks and the NES's 16, and `gh api repos/dbhq-uk/6502/pulls/<n>` gives the machine pull requests as #41 and #55. The NES then needed a speed pass of its own.

## What was found

- **The core needs nothing new for any of the nine.** The 6507, the 6510 and the Atari's "Sally" run as the NMOS 6502, and their stalls (the 2600's WSYNC, ANTIC's DMA, the VIC-II's bad lines) follow the pattern the NES's DMA and the BBC's 1 MHz stretch already use in the bus.
- **The browser host is shared; the panels are not.** `machine-host.js` runs every machine, but each one still writes its own screen, keys and sound script, and the Electron branch reuses no chip code from the BBC library.
- **The 2600 has no ROM to clear.** An MIT test suite for its TIA and RIOT exists on GitHub and can be forked. It is new this year, verified on one PAL console, and its README says it was written largely with Claude, so it is a recognised test to use with care.
- **The Atari 8-bit family has the only fully licensed system ROM of the nine.** AltirraOS and Altirra BASIC carry an all-permissive notice, and the Acid800 suite is MIT and passes on real hardware. One chip set serves up to four registry rows.
- **The Commodore 64's ROM position is the hardest.** Cloanto says the CBM 8-bit ROMs belong to Amiga Corporation and need a licence to redistribute, and no grant to open emulators was found. MEGA65's Open ROMs are LGPL and not complete.

## The recommendation

**Keep the Atari 2600 next, and move the Atari 8-bit family ahead of the Commodore 64.** The 2600 first because it has no ROM, the fewest new chips and the hardest test of cycle timing. The Atari 8-bit second because its ROM and its tests are both clean. Chosen over keeping the Commodore 64 fourth, because its KERNAL question is still open, and over the BBC Master, which is the smallest step but has no recognised test suite.

This is a recommendation. The order is Dan's call, and the machines design and the README change only if he makes it.

## What could not be checked

Each research angle had one round and was still finding new names when it stopped, so none is saturated. No rights holder was asked directly. Several pages refused to open, among them VICE's own note on its ROMs and the article on the 2026 Commodore deals. The task sizes are judgements against two machines, not measurements, and nothing was assembled or run.
