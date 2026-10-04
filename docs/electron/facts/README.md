# Acorn Electron fact sheets

Two sheets, written on 4 October 2026 before any code, so the Electron's chips are built from sources and not from memory. Each fact carries a tag: `[from <source>]` was read directly, `[from ROM]` was read out of a ROM, `[from run]` was computed by a script quoted in the sheet, `[inferring ...]` joins dots, and `[guessing - verify]` is a flag for a correction. Where sources disagree, the sheet says which it trusts.

| Sheet | Covers |
|---|---|
| [`ula.md`](ula.md) | The memory map, the paged ROM select and its acceptance rule, the clocks, the contention rule with its totals and four tests worked out by hand, the seven display modes, the palette, the frame timing, the interrupts, the keyboard matrix, sound, the cassette registers, reset and the boot screen |
| [`tape.md`](tape.md) | The UEF container and the chunks an Acorn program tape carries, the tape block format with a worked 30-byte example, the CRC and its check values, where "tape as bytes" can sit, and what has to be found by running the ROM |

**Paths under `/tmp` are the working files of the sessions that wrote the sheets and were not kept.** The sources they name are public; the ROMs are in `roms/electron/` once the plan's first task has run.

**Nothing here is code from Elkulator, elkjs, jsbeeb, B-em, MAME or ElectrEm**, which are GPL or have other terms. `ula.md` read one repository, `hoglet67/ElectronFpga`, only to cross-check a few numbers, and says where.
