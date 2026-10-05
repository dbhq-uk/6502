# NES cartridge files: iNES, NES 2.0, the region, the test ROM protocol

Written 5 October 2026 for the NES plan, task 1. Tags and page revisions are in
[`README.md`](README.md). Used by tasks 2 (`Cartridge`), 4 (the test ROM runner),
10 and 11 (the mappers) and 14 (the page's file picker and region choice).

## 1. The iNES file

A `.nes` file is, in order [from iNES]:

1. a 16-byte header;
2. a 512-byte trainer, if flags 6 bit 2 is set;
3. PRG ROM, 16384 x (byte 4) bytes;
4. CHR ROM, 8192 x (byte 5) bytes; byte 5 = 0 means the board has CHR RAM;
5. PlayChoice data, if flags 7 bit 1 is set (8 KB, and often 32 bytes more).

Some files carry a 127 or 128-byte title at the end [from iNES].

| Byte | iNES meaning | Source |
|---|---|---|
| 0-3 | `4E 45 53 1A` ("NES" and MS-DOS end of file) | [from iNES] |
| 4 | PRG ROM in 16 KB units | [from iNES] |
| 5 | CHR ROM in 8 KB units (0 = CHR RAM) | [from iNES] |
| 6 | Flags 6: bit 0 mirroring, bit 1 battery, bit 2 trainer, bit 3 four-screen (or another layout), bits 7 to 4 mapper low nibble | [from iNES] |
| 7 | Flags 7: bit 0 Vs. System, bit 1 PlayChoice-10, bits 3 to 2 = 2 means NES 2.0, bits 7 to 4 mapper high nibble | [from iNES] |
| 8 | PRG RAM in 8 KB units (0 means 8 KB) | [from iNES] |
| 9 | bit 0: TV system, 0 NTSC, 1 PAL; "very few emulators honor this bit" | [from iNES] |
| 10 | unofficial: TV system and PRG RAM presence | [from iNES] |
| 11-15 | padding, should be 0 | [from iNES] |

**Flags 6 bit 0:** 0 = vertical arrangement, which is horizontal mirroring
(CIRAM A10 = PPU A11); 1 = horizontal arrangement, which is vertical mirroring
(CIRAM A10 = PPU A10). Mappers that set mirroring themselves (MMC1, MMC3, AxROM)
ignore it [from iNES].

**Flags 6 bit 3** means four-screen VRAM on MMC3 (Rad Racer II) and mapper 206;
for other mappers it means other things [from iNES; NES 2.0].

**The trainer** is 512 bytes loaded at CPU `$7000-$71FF`. It is not on
unmodified dumps of real cartridges [from iNES; NES 2.0].

**Junk in bytes 7 to 15.** Old tools wrote text there ("DiskDude!"), which adds
64 to the mapper number. If bytes 12 to 15 are not all 0 and the header is not
NES 2.0, mask off the mapper's high nibble or refuse the file [from iNES].

**Detecting the format** [from iNES]:

1. Byte 7 AND `$0C` = `$08`, and the size including byte 9 fits the file: NES
   2.0.
2. Byte 7 AND `$0C` = `$04`: archaic iNES.
3. Byte 7 AND `$0C` = `$00` and bytes 12 to 15 all 0: iNES.
4. Otherwise iNES 0.7 or archaic iNES.

## 2. NES 2.0

Bytes 0 to 7 mean the same as iNES [from NES 2.0]. Then:

| Byte | Meaning | Source |
|---|---|---|
| 6 | as iNES; bit 0 is the hard-wired mirroring, 0 for mapper-controlled | [from NES 2.0] |
| 7 | bits 1 to 0 console type (0 NES, 1 Vs., 2 PlayChoice, 3 extended); bits 3 to 2 = `10` | [from NES 2.0] |
| 8 | bits 3 to 0 mapper bits 11 to 8; bits 7 to 4 submapper | [from NES 2.0] |
| 9 | bits 3 to 0 PRG ROM size high; bits 7 to 4 CHR ROM size high | [from NES 2.0] |
| 10 | PRG RAM: bits 3 to 0 volatile, bits 7 to 4 non-volatile; size `64 << n`, 0 = none | [from NES 2.0] |
| 11 | CHR RAM: bits 3 to 0 volatile, bits 7 to 4 non-volatile; size `64 << n`, 0 = none | [from NES 2.0] |
| 12 | bits 1 to 0 CPU/PPU timing: 0 RP2C02 (NTSC), 1 RP2C07 (PAL), 2 multiple, 3 UA6538 (Dendy) | [from NES 2.0] |
| 13 | Vs. PPU and hardware, or the extended console type | [from NES 2.0] |
| 14 | bits 1 to 0: number of miscellaneous ROMs | [from NES 2.0] |
| 15 | bits 6 to 0: default expansion device | [from NES 2.0] |

- **ROM sizes.** If the high nibble in byte 9 is `$0` to `$E`, the size is
  `(nibble << 8 | byte 4)` x 16 KB for PRG, and the same with byte 5 x 8 KB for
  CHR. If it is `$F`, byte 4 (or 5) is `EEEEEEMM` and the size is `2^E x (MM x 2
  + 1)` bytes [from NES 2.0].
- **CHR RAM.** With a NES 2.0 header, no CHR ROM does not imply 8 KB CHR RAM;
  byte 11 must say so [from NES 2.0].
- **Battery.** If the PRG NVRAM nibble is not 0 the battery bit is set, and the
  other way round except for mapper-internal memory [from NES 2.0].
- **Byte 12, value 2 ("multiple")** means the game detects the console and runs
  correctly on both NTSC and PAL [from NES 2.0]. Value 3 (Dendy) is used for no
  licensed game [from NES 2.0].

## 3. The region a file names

| Header | Says | Source |
|---|---|---|
| NES 2.0, byte 12 = 0 | NTSC | [from NES 2.0] |
| NES 2.0, byte 12 = 1 | PAL | [from NES 2.0] |
| NES 2.0, byte 12 = 2 | either | [from NES 2.0] |
| NES 2.0, byte 12 = 3 | Dendy: refused (spec, "Decisions") | [from NES 2.0] |
| iNES, byte 9 bit 0 | NTSC or PAL, but rarely set, so not trusted alone | [from iNES: "very few emulators honor this bit as virtually no ROM images in circulation make use of it"] |
| iNES, byte 10 bits 1 to 0 | unofficial, "relatively few emulators honor it" | [from iNES] |

For the model (the spec's rule): NES 2.0 byte 12 decides when present; an iNES
file names no region, and the page starts at NTSC with the visitor free to
change it [inferring from the spec, "Cartridge" and "Region"].

## 4. Files that must be refused

The Review Focus (plan, item 1) asks for a plain message for each. What makes
each one detectable [inferring from sections 1 and 2]:

| Case | How it is known |
|---|---|
| Empty, or shorter than 16 bytes | no header |
| Wrong magic | bytes 0 to 3 are not `4E 45 53 1A` |
| Shorter than the header says | 16 + trainer + PRG + CHR is more than the file's length |
| A trainer | flags 6 bit 2; it can be skipped (512 bytes) or refused, a choice for task 2 |
| An unsupported mapper | not 0, 1, 2, 3, 4 or 7 |
| Dendy | NES 2.0 byte 12 = 3 |
| Many megabytes | the largest NES 2.0 sizes are tens of megabytes (PRG up to 62,898,176 bytes in the simple form) [from NES 2.0]; a limit is a choice for task 14 |
| PRG size 0 | nothing to map at `$8000` [inferring] |

### Worked example 1: the two pinned test ROMs

`other/nestest.nes`: 24592 bytes = 16 + 16384 + 8192, so 1 PRG bank and 1 CHR
bank, NROM [from the fork: the file's length, measured on 5 October 2026 by
downloading it].

`ppu_vbl_nmi/ppu_vbl_nmi.nes` starts `4E 45 53 1A 10 00 11 00 00 00 00 00 00 00
00 00`: 16 x 16 KB = 256 KB of PRG, CHR RAM, flags 6 = `$11` so mapper 1 (MMC1)
with bit 0 set, flags 7 = 0 so iNES; 16 + 262144 = 262160 bytes, which is the
file's length [from the fork: the file, read with `xxd` on 5 October 2026;
inferring the fields from section 1]. So the combined PPU test needs MMC1, and
task 4 cannot run it on NROM alone. Its ten singles can: every file in
`ppu_vbl_nmi/rom_singles/` starts `4E 45 53 1A 02 01 01 00`, 32 KB of PRG and 8
KB of CHR on mapper 0, and `01-vbl_basics.nes` is 40976 bytes [from the fork:
the files' first 8 bytes, read the same way].

## 5. The test ROM protocol

Blargg's test ROMs report through cartridge RAM as well as on screen [from the
fork: ppu_vbl_nmi/readme.txt]:

- `$6000` is the status: `$80` while running; `$81` means press reset, at least
  100 ms from now; `$00` to `$7F` is the result, 0 a pass, 1 a failure, 2 and up
  a specific reason [from the fork: ppu_vbl_nmi/readme.txt].
- The text is written from `$6004`, zero-terminated, and the terminator moves on
  as text is added [from the fork: ppu_vbl_nmi/readme.txt].
- `$6001-$6003` hold a signature so an emulator can tell the data is valid. The
  readme writes it as "`$DE $B0 $G1`", where `$G1` is not hexadecimal
  [from the fork: ppu_vbl_nmi/readme.txt]. The bytes are `$DE $B0 $61`: the
  single `01-vbl_basics.nes` stores them with `A9 DE 8D 01 60 A9 B0 8D 02 60 A9
  61 8D 03 60` at file offset `$6B06`, and all ten singles, run in task 4, put
  them there before they report [from the fork: the file, read 5 October 2026;
  the task 4 runner, which waits for these three bytes].
- A multi-test ROM's result is the first sub-test that failed; the singles are in
  `rom_singles/` [from the fork: ppu_vbl_nmi/readme.txt].

**Which test ROMs the fork has.** At the pinned commit the fork holds
`instr_test-v5`, `instr_timing`, `cpu_interrupts_v2`, `ppu_vbl_nmi`,
`sprite_hit_tests_2005.10.05`, `sprite_overflow_tests`, `apu_test`, `apu_mixer`,
`pal_apu_tests`, `dmc_dma_during_read4`, `mmc3_test_2` and `other/` [from the
fork: its file list, read with `gh api` on 5 October 2026]. It does **not** hold
`ppu_sprite_hit` or `ppu_sprite_overflow`, which the spec names; the older
`sprite_hit_tests_2005.10.05` and `sprite_overflow_tests` stand in for them
[from the fork; Emulator tests: "older revision of the tests"].
`mmc3_test_2` has only `rom_singles/`, no combined ROM, and `pal_apu_tests` has
tests 01 to 08, 10 and 11 [from the fork: the directory lists].

Of these, `ppu_vbl_nmi` tests "the NTSC PPU" [from the fork:
ppu_vbl_nmi/readme.txt], and `pal_apu_tests` is "PAL version of the
blargg_apu_2005.07.30 tests" [from Emulator tests]. Which others run on PAL is
not on the pages read [guessing - verify: the plan's task for each ROM says].

## 6. Open items

1. The `$6001-$6003` signature's third byte (5): settled in task 4, `$61`.
2. Which test ROMs are meaningful on PAL (5) [guessing - verify].
