---
title: "The next four machines"
date: 2026-10-04
summary: "After the BBC Micro, four more machines, one at a time and smallest first, starting with the Acorn Electron. The NES gives up third place, because half of the reason it held it went with the port."
order: 29
---

# 4 October 2026: the next four machines

With the BBC Micro counting, the design said the NES was next, and called that
settled. It was talked through again before any work started on it.

## Why the old order was open again

The machines design gave the NES third place for two reasons: it was a machine
people want, and "it is also the reference for the later port". The port was
dropped on 1 October. The first reason still holds; the second does not. The
README and the site still said the NES was next.

## The candidates

Four were weighed, each against the design's own tests: the size of the
machine, whether its ROM can be served, whether test material exists, and how
many people would want it.

- **The NES.** No system ROM. The core already runs the 2A03, `nestest` already
  passes, and the `nes-test-roms` fork is in place. The job is large: the PPU,
  the APU and the common mappers.
- **The Atari 2600.** No system ROM and two chips. The program draws the
  picture in step with the beam, so it shows exact cycle timing better than any
  other machine. Its page needs a homebrew or test cartridge to show anything.
- **The Acorn Electron.** The smallest step from the BBC Micro. BBC BASIC is
  the same ROM, the operating system is another Acorn ROM from the same holder,
  and one ULA does the work of the BBC's video and sound chips.
- **The Commodore 64.** The largest audience. The VIC-II and the SID are the
  hardest chips on the list, and the KERNAL's rights need checking first.

## The decision

**All four, one at a time, smallest first: the Electron, the Atari 2600, the
NES, then the Commodore 64.** Dan's call.

Chosen over two other orders:

- **The NES first, then the other three.** This kept the published order, but
  the count would have stayed at two for weeks.
- **All four in parallel, one worktree each.** Each machine has its own
  folders, but every machine also changes `machines/registry.json`, the shared
  browser host, the site's machine pages and the day's journal. Four branches
  would conflict on all of them, and four large reviews would arrive at once.

Smallest first also means each machine adds to the shared browser host before
the larger ones need it, and the Commodore 64 going last leaves room to settle
its KERNAL rights before its spec is written.

The order is in the machines design, and the README and `AGENTS.md` follow it.
Each machine still gets its own spec before any code.
