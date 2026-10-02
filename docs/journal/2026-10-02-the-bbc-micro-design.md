---
title: "Designing the BBC Micro"
date: 2026-10-02
summary: "The second machine is designed before any code: the disc controller is in the first version, the chips tick on every cycle, and the machine is proven by our own tests. The ROMs come from jsbeeb with their rights position written down, and the disc test turned out to need a third ROM."
order: 10
---

# 2 October 2026: designing the BBC Micro

The KIM-1 is live, so the BBC Micro Model B is next. This entry records the
design decisions, in the order they were made. The spec is
`docs/superpowers/specs/2026-10-02-bbc-micro-design.md`.

## Decisions

- **The disc controller is in the first version.** Dan chose this over booting to
  BASIC with no disc, and over a display-and-keyboard-only first cut. The
  recommendation was boot to BASIC with the disc left for later. Dan's reason is
  the disc is what makes the machine usable. The cost is that first light waits
  on the 8271.
- **Timing is cycle-accurate.** Every bus access ticks the video chips, the
  timers and the slow-device stretch. It was chosen over scanline rendering,
  which is simpler but loses mid-line changes and would fail timing tests. It is
  the core's own rule, every bus access is one cycle, applied to the whole
  machine.
- **The acceptance test is our own tests first.** Boot in each mode, a BASIC
  program, a disc save and load, and timer checks. It was chosen over
  researching community test discs first, which would add a research step before
  any code.
- **The count does not wait for the 3D models.** The machine counts when it
  passes its tests and runs in the browser. The models follow in a second pull
  request. This is a reading of issue 28 that Dan approved in the session.

## What was checked

- **ROM source.** jsbeeb's `public/roms/` holds the Model B's operating system
  and BBC BASIC. Its `public/roms/README` says the ROMs are copyrighted, are not
  GPL, that it thinks publishing them is fair use provided you own them, and
  that it will remove them on request. That is a documented rights position, so
  under the ROM rule they are used.
- **A third ROM.** jsbeeb pairs `os.rom`, `BASIC.ROM` and `b/DFS-1.2.rom` for a
  Model B with an 8271 disc controller. The disc test needs the DFS ROM, which
  the first outline had not counted. The DFS ROM names itself "DFS,NET", so it
  appears to carry an Econet filing system as well. That is read from its
  strings and not checked further.
- **The files.** All three are 16384 bytes. The SHA-256 of each is in the spec.
  They were fetched at jsbeeb commit `e27b20d4a33c2a7b17d2cf830f4695e961b6846e`
  on 2 October 2026 with `curl` from `raw.githubusercontent.com`.
- **The operating system's version is not known.** The file is called `os.rom`
  with no version. The plan reads it off the ROM.

## A rule that came out of it

jsbeeb is GPL-3.0 and this repository is MIT. So the chips are written from the
datasheets and checked by our own tests, and no code is taken from jsbeeb or
B-em. Only the ROMs are taken, and they are not its code. The same rule decides
the teletext character set: jsbeeb's copy is GPL, so the plan confirms a
different source before any glyph is written.

## Mistakes

- **A box did not show.** The first version of the design's section 1 was
  followed straight away by a question box, and the section above it did not
  reach Dan. It was posted again as plain text. For the rest of the session the
  design went out as text with a plain question at the end.
