---
title: "The Electron's design"
date: 2026-10-04
summary: "The Acorn Electron is written up as the third machine: tape for storage, the ULA holding the CPU off modelled exactly, and a search for ROMs that found them in a place other than where the BBC's came from."
order: 30
---

# 4 October 2026: the Electron's design

The Electron is the third machine, after the order was settled earlier the same
day. This entry is the design session for its spec, which is
[`2026-10-04-electron-design.md`](../superpowers/specs/2026-10-04-electron-design.md).

## The decisions, in the order they were made

**Storage: cassette.** The Electron's own storage is a tape port, and it sits in
the same chip, the ULA, that does the display and the sound, so it comes nearly
free. Chosen over the Plus 3 with disc images, which needs the Plus 1
expansion, a disc controller and a second ROM before the machine can count, and
over no storage at all, which would drop the save-and-load proof the BBC
set. Disc is a later issue.

**Contention, exactly.** The ULA and the CPU share RAM that is four bits wide,
so while the ULA fetches the picture the CPU waits, and how often depends on the
screen mode. The model decides this on every bus access from the display's
position. Chosen over one average slowdown per mode, which would be wrong inside
a frame and would break the core's claim that every bus access is one cycle, and
over ignoring it, which makes a different machine. The one cost is speed: the
BBC's chips are caught up lazily, and this check runs on every access, so the
first milestone measures it in the browser before the rest is built.

## What the ROM search found

The BBC's ROMs came from jsbeeb. jsbeeb has none for the Electron: its ROM
folder holds the BBC, the Master, the Atom and the Compact. The search for
another source found `dmcoles/elkjs`, an Electron emulator in JavaScript, which
keeps two 16 KB images in its root.

- **`os.rom`** names itself "OS 1.00" and carries "(C) 1983 Acorn Computers
  Ltd." in its own text. Its reset vector is `$D8D2`, inside the ROM, which is
  what a real operating system image looks like.
- **`basic.rom`** has the same SHA-256 as the BBC Micro's `BASIC.ROM`, checked
  by hashing both. It is one file, kept once.

The elkjs repository is licensed GPL-2.0 and says nothing about the ROMs, so the
licence is not a licence for them. The position is the BBC's: Acorn's copyright,
no holder found, no permission found, documented and not cleared.

## A correction

Earlier the same day the Electron was described as using the BBC's operating
system. That was wrong, and it was said from memory. The operating system is a
different image from a different file. BASIC is the BBC's, as above. The
operating system's rights record is therefore a new one and not a reuse.

## What is still open

Whether the Electron needs a separate keyboard ROM image or has that code in the
operating system is left to the plan to read off the paging, and the spec says
so. The fetch pattern, the I/O addresses and the tape timing are also taken from
Acorn's documentation in the plan and not assumed here.
