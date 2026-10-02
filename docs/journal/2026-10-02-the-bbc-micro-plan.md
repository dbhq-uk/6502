---
title: "Planning the BBC Micro"
date: 2026-10-02
summary: "Before any code, four fact sheets were written for the BBC Micro's chips, and they changed the plan. The picture is interlaced, two fields to a frame. The disc software cannot cope with the controller presenting bytes too close together, so the controller needs the real byte rate. The operating system needs particular answers from parts of the machine that are absent. The plan gives each task its tests but not its code, and the project, the three ROMs and their rights go in first."
order: 11
---

# 2 October 2026: planning the BBC Micro

The design was settled earlier today (see "Designing the BBC Micro"). This entry
records what came next: four fact sheets, the plan, and the first task, which
puts the project and the ROMs in the repository. The plan is
`docs/superpowers/plans/2026-10-02-bbc-micro.md` and the sheets are in
`docs/bbc-micro/facts/`.

## The fact sheets, and what they changed

The BBC Micro's chips are thousands of lines of code, and the code is written
from sources and not from memory. So four sheets came first: the bus and memory
map, the 6522 VIAs with the keyboard and sound chip, the video chips, and the
disc controller. Each fact carries a tag saying whether it was read from a
source, read out of a ROM, inferred, or is a guess that needs checking. Where
the sources disagreed, the sheet says which one it trusts and why.

Five things in them changed what gets built.

- **The picture is 625 interlaced lines, not 312.** The operating system turns
  on interlace in every mode, so a frame is a field of 312 lines and a field of
  313. Only with interlace off (`*TV x,1`, and never in mode 7) is a field 312
  lines. Writing the video chip to a 312-line frame would have been wrong.
- **The video chip is a Hitachi HD6845S.** The operating system writes a
  programmable VSYNC width and skew bits that a plain 6845 does not have. The
  sheet says to build the HD6845S behaviour, not a generic 6845.
- **The disc controller must not present bytes too close together.** The DFS
  data handler takes about 77 cycles per byte (71 to write, 77 to read, from
  the NMI being taken to the return) and is not re-entrant. A byte that arrives
  inside that time nests the interrupt and the ROM crashes. In the trace, bytes
  80 to 128 cycles apart passed, and so did bytes 300 and 2000 cycles apart;
  76 cycles and closer crashed. The real drive delivers one byte every 128 CPU
  cycles, so that is the rate the model uses, and it is a choice of the model,
  not something the ROM asks for. This was found by running the ROM against a
  throwaway disc machine written from the Intel datasheet, kept in
  `tools/probes/bbc-dfs-trace/`. It is evidence for the sheet and nothing in
  `src/` is derived from it.
- **The ROM needs particular values from parts of the machine that are not
  there.** The DFS probes for an Econet adapter by reading two absent device
  addresses, and it switches its Econet code on if both read zero. The value
  measured on a real machine for an absent fast device is `$FE`, which keeps
  Econet off. The operating system also tells power-on from BREAK by what the
  system VIA's interrupt enable register holds. Where the sheet has a measured
  value for something absent, the model returns it and not zero.
- **The boot screen was derived, not run.** From the ROMs, the mode 7 screen
  should read: a blank row, `BBC Computer 32K`, a blank row, `Acorn DFS`, a
  blank row, `BASIC`, a blank row, then `>`. Nothing has run to check it. Task 5
  of the plan boots the real operating system and corrects the sheet if it is
  wrong.

## The third ROM

The design counted two ROMs, the operating system and BASIC. The disc test needs
the DFS ROM too, so there are three, each 16384 bytes. The DFS ROM names itself
`DFS,NET` in its header but prints `Acorn DFS` at boot; `DFS 1.20` appears only
under `*HELP`. The operating system's version was not known when the design was
written. The ROM says `OS 1.20` at `$E825`, and a test now checks it, along with
the reset vector `$D9CD`.

All three were fetched from jsbeeb at commit
`e27b20d4a33c2a7b17d2cf830f4695e961b6846e`, checked against the hashes in
`tests/Dbhq.Cpu6502.TestSupport/Pins.cs`, and committed under
`roms/bbc-micro/`. Their rights position is in `roms/README.md`: jsbeeb says the
ROMs are still copyrighted, thinks publishing them is fair use if you own them,
and will remove them on request; the copyright is Acorn's, who holds it now was
not established, and no permission was found. They are used under the rule of
1 October 2026 and will be removed if a holder asks.

## The teletext glyphs

Mode 7 needs the SAA5050's character shapes. jsbeeb's copy is GPL, so it
cannot be used. The fact sheet looked at the candidates and recommends the glyph
table in Bedstead, a typeface whose authors dedicate it to the public domain
under CC0. Only the table of row bytes will be taken, with a notice carrying its
header and its note on the Mullard typeface's copyright, never its C code. That
copyright question is the same if we draw the glyphs ourselves, so drawing them
does not remove it. The plan adds a test that spot-checks a few glyphs against
the datasheet's picture.

## The plan's form

The plan has fifteen tasks, one commit each, on the branch `feat/bbc-micro`.
Unlike the plan for the core, it was not run in advance. The core's plan put
the finished code in the plan, and that works for a small chip. The BBC's chips
are too big for it. So each task gives its interfaces, the facts it is built
from and the exact test assertions, and the code is written to pass them, with
a review of each task before the next begins. This was chosen over writing
thousands of lines of code into a document that nothing had checked.

What is proven early instead is the risky part. Task 5 boots the real operating
system headless, and task 6 measures speed in a browser, so a wrong assumption
shows up in the first third of the work and not the last. A draft pull request
opens after task 1, so that CI has something to run on. The 3D models are a separate
branch and a separate plan.

## What is not known

Two limits are on the record in the sheets, and the plan does not pretend
otherwise.

- **The video pipeline's delay is undocumented.** When the palette changes in
  the middle of a line, the colour changes from the next pixel, but the exact
  number of pixels of delay between the CRTC and the screen is a guess, flagged
  `[guessing - verify]` in `video.md`.
- **The phase of the 1 MHz clock against the 2 MHz CPU clock at power-on is
  unknown.** Without it, a VIA access cannot be placed at an exact CPU cycle.
  The plan builds the VIA in 1 MHz ticks driven by the bus's stretch rule and
  checks it against timings measured on real machines.

## Task 1

The first task adds the project and its test project, `BbcRoms` and
`BbcOptions`, the three ROMs, their six pins, and a test that each ROM is 16384
bytes and matches its pin, that the operating system is `OS 1.20`, and that
`BbcRoms` refuses an image of any other size. The tests were written first and
failed to compile because the pins did not exist.

## Mistakes

- **A new test project did not compile.** It was copied from the KIM-1's test
  project by a `sed` that changed only the first match on each line, so it still
  pointed at the KIM-1 library. The build warned that the referenced project
  did not exist, and the tests ran anyway, because the pins live in the shared
  test support library. A passing run with a warning in it is not a clean run.
