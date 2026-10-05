# NES mappers: NROM, MMC1, UxROM, CNROM, AxROM, MMC3

Written 5 October 2026 for the NES plan, task 1. Tags and page revisions are in
[`README.md`](README.md). Used by tasks 2 (the interface), 10 (the five simple
boards and MMC1) and 11 (MMC3). The header that names the mapper is in
[`cartridge.md`](cartridge.md).

## 1. Nametable arrangements

The console has 2 KB of nametable RAM (CIRAM); the cartridge drives its address
line A10 [from PPU nametables; Mirroring].

| Arrangement | `$2000` | `$2400` | `$2800` | `$2C00` | CIRAM A10 = | Source |
|---|---|---|---|---|---|---|
| Vertical mirroring (horizontal arrangement) | A | B | A | B | PPU A10 | [from PPU nametables; Mirroring] |
| Horizontal mirroring (vertical arrangement) | A | A | B | B | PPU A11 | [from PPU nametables; Mirroring] |
| One screen, lower | A | A | A | A | 0 | [from Mirroring; MMC1; AxROM] |
| One screen, upper | B | B | B | B | 1 | [from Mirroring; MMC1; AxROM] |
| Four screen | A | B | C | D | (cartridge RAM) | [from Mirroring; iNES] |

A and B are the first and second KB of CIRAM [inferring from "connecting CIRAM
A10 to PPU A10/A11"].

### Worked example 1: one address under each arrangement

PPU `$2C05`: vertical mirroring maps it to CIRAM `$0405` (A10 = 1), horizontal
to `$0405` (A11 = 1), one screen lower to `$0005`, one screen upper to `$0405`
[inferring from the table]. PPU `$2805`: vertical gives `$0005` (A10 = 0),
horizontal gives `$0405` [inferring].

## 2. NROM (mapper 0)

- PRG ROM 16 or 32 KB at `$8000-$FFFF`; 16 KB is mirrored at `$C000` [from
  NROM].
- CHR 8 KB at PPU `$0000` [from NROM]. Homebrew sometimes uses 8 KB CHR RAM
  instead, and most emulators allow it [from NROM].
- No registers. Mirroring fixed by the board, from the header (`cartridge.md`)
  [from NROM].
- PRG RAM only on Family BASIC (2 or 4 KB at `$6000`); most emulators map 8 KB
  at `$6000` when the battery flag is set [from NROM].

## 3. MMC1 (mapper 1)

**The serial port.** Every write to `$8000-$FFFF` goes to a 5-bit shift
register [from MMC1]:

- A write with bit 7 set clears the shift register and ORs the control register
  with `$0C` (PRG mode 3) [from MMC1].
- Otherwise bit 0 is shifted in, low bit first. On the fifth write the value goes
  to the register chosen by address bits 14 and 13, and the shift register
  clears. Only the fifth write's address matters [from MMC1].
- **Writes on consecutive cycles:** only the first is taken (a read-modify-write
  writes twice). The bit 7 reset is never ignored [from MMC1].

| Register | Addresses | Bits | Source |
|---|---|---|---|
| Control | `$8000-$9FFF` | `CPPMM`: mirroring (0 one-screen lower, 1 one-screen upper, 2 vertical, 3 horizontal); PRG mode (0 and 1: 32 KB at `$8000`, low bit ignored; 2: first bank fixed at `$8000`, 16 KB switched at `$C000`; 3: 16 KB switched at `$8000`, last bank fixed at `$C000`); CHR mode (0: 8 KB; 1: two 4 KB) | [from MMC1] |
| CHR bank 0 | `$A000-$BFFF` | 5 bits: 4 KB bank at PPU `$0000`, or 8 KB (low bit ignored) | [from MMC1] |
| CHR bank 1 | `$C000-$DFFF` | 5 bits: 4 KB bank at PPU `$1000`; ignored in 8 KB mode | [from MMC1] |
| PRG bank | `$E000-$FFFF` | `RPPPP`: 16 KB bank (low bit ignored in 32 KB mode); R on MMC1B = PRG RAM disabled | [from MMC1] |

- **Power-on:** the control register is in PRG mode 3 on all tests reported,
  though one test found this fragile [from MMC1]. For the model: control = `$0C`
  at power-on [inferring from the reset write's effect].
- With 8 KB of CHR the bank number is ANDed with 1 [from MMC1].
- PRG RAM 8 KB at `$6000` (up to 32 KB on some boards, banked through the CHR
  registers: SOROM, SUROM, SXROM, SZROM) [from MMC1]. Without NES 2.0, 32 KB is
  enough for every known game [from MMC1]. The plan models the plain SxROM case
  with 8 KB [guessing - verify: the bundled homebrew and the test ROMs need no
  more].
- SEROM, SHROM and SH1ROM have 32 KB PRG unbanked; "an emulator may switch to
  PRG bank 0 at power-on" (submapper 5) [from MMC1].

### Worked example 2: an MMC1 PRG bank write

Write `$80` to `$8000` (reset: PRG mode 3). Then write bank 5 (`%00101`) to
`$E000` one bit at a time: `$01`, `$00`, `$01`, `$00`, `$00`, each to `$E000`.
Now `$8000-$BFFF` reads PRG bank 5 and `$C000-$FFFF` the last bank [from MMC1,
its example; the values are an example]. If the writes came from `INC $E000`
(two writes on consecutive cycles), only the first would count [from MMC1].

## 4. UxROM (mapper 2)

- `$8000-$BFFF`: a switchable 16 KB bank; `$C000-$FFFF`: fixed to the last bank
  [from UxROM].
- Bank select at `$8000-$FFFF`. UNROM uses bits 2 to 0, UOROM 3 to 0; emulators
  treat it as a full 8-bit register without bus conflicts [from UxROM].
- CHR is 8 KB, normally RAM [from UxROM: CHR capacity 8K]. Mirroring fixed by
  the board [from UxROM].
- Nintendo's boards have bus conflicts, which the games avoid; NES 2.0
  submappers say which [from UxROM]. For the model: no bus conflicts
  [inferring from "Emulator implementations of iNES mapper 2 treat this as a full
  8-bit bank select register, without bus conflicts"].

## 5. CNROM (mapper 3) and AxROM (mapper 7)

**CNROM.**

- PRG ROM 16 or 32 KB unbanked at `$8000`; CHR ROM up to 32 KB in 8 KB banks at
  PPU `$0000` [from CNROM].
- Bank select at `$8000-$FFFF`, bits 1 to 0 (up to bits 3 to 0, 128 KB, on the
  oversize variant) [from CNROM].
- The original board has AND-type bus conflicts: the value written is ANDed with
  the ROM byte at that address. Submapper 1 is no conflicts, 2 is AND
  conflicts, 0 unknown [from CNROM]. For the model: apply the AND unless the
  header says submapper 1 [guessing - verify: no test ROM in the plan's list
  writes a value that differs from the ROM byte].
- Mirroring fixed by the board [from CNROM].

**AxROM.**

- PRG ROM up to 256 KB in 32 KB banks at `$8000-$FFFF`; CHR 8 KB RAM [from
  AxROM].
- Bank select at `$8000-$FFFF`: `xxxM xPPP`, PRG bank (bits 2 to 0) and the one
  nametable page for all four (bit 4) [from AxROM].
- AMROM and AOROM have bus conflicts; ANROM and AN1ROM do not; every retail
  AOROM game probably runs without them [from AxROM]. For the model: no bus
  conflicts [inferring].

## 6. MMC3 (mapper 4)

**Banks** [from MMC3]:

| CPU | PRG mode 0 | PRG mode 1 |
|---|---|---|
| `$6000-$7FFF` | 8 KB PRG RAM | 8 KB PRG RAM |
| `$8000-$9FFF` | R6 | second-last bank |
| `$A000-$BFFF` | R7 | R7 |
| `$C000-$DFFF` | second-last bank | R6 |
| `$E000-$FFFF` | last bank | last bank |

| PPU | CHR A12 inversion 0 | CHR A12 inversion 1 |
|---|---|---|
| `$0000-$07FF` | R0 (2 KB) | R2, R3 (1 KB each) |
| `$0800-$0FFF` | R1 (2 KB) | R4, R5 |
| `$1000-$17FF` | R2, R3 (1 KB each) | R0 (2 KB) |
| `$1800-$1FFF` | R4, R5 | R1 (2 KB) |

**Registers**, even and odd addresses in each 8 KB range [from MMC3]:

| Address | Register | Bits |
|---|---|---|
| `$8000` even | Bank select | `CPMx xRRR`: CHR A12 inversion, PRG mode, register R0 to R7 to write next |
| `$8001` odd | Bank data | R0 and R1 ignore bit 0 (2 KB banks); R6 and R7 ignore bits 7 and 6 |
| `$A000` even | Mirroring | bit 0: 0 vertical, 1 horizontal; no effect with four-screen VRAM |
| `$A001` odd | PRG RAM protect | bit 7 enable, bit 6 write protect; many emulators leave it out for MMC6 compatibility |
| `$C000` even | IRQ latch | the reload value |
| `$C001` odd | IRQ reload | clears the counter and sets the reload flag |
| `$E000` even | IRQ disable | disables IRQs and acknowledges a pending one |
| `$E001` odd | IRQ enable | enables IRQs |

- R6, R7 and `$8000` are unspecified at power-on, so the reset vector is in the
  fixed `$E000-$FFFF` [from MMC3]. For the model: R0 to R7 start at 0, 2, 4, 5,
  6, 7, 0, 1 and the bank select at 0, so the first 32 KB of PRG and the first
  8 KB of CHR read in order, and PRG RAM starts on and writable. The test ROMs
  need both: `mmc3_test_2`'s are 32 KB programs whose shell writes no bank
  register and never writes `$A001`, and they report through `$6000` [from the
  fork: mmc3_test_2/source/common/build_rom.s, shell.inc; the choice is
  guessing - verify against a console].

**The IRQ counter** [from MMC3]:

- It is clocked by a rising edge of PPU A12 that comes after A12 has been low for
  three falling edges of M2 (the CPU clock). This filter is why the counter sees
  one edge a line [from MMC3]. For the model: M2 falls at the end of each CPU
  cycle, so a low that began in cycle `c` has seen three falls when A12 rises in
  cycle `c + 3` or later [inferring]. With the filter at 1 or 2 cycles,
  `mmc3_test_2`'s `2-details` and `4-scanline_timing` fail; at 3, 4 and 7 every
  ROM that should pass does [measured in task 11, 5 October 2026: the constant
  changed and the MMC3 tests run].
- On a clock: if the counter is 0 or the reload flag is set, counter = latch and
  the reload flag clears; otherwise it counts down. Then, if the counter is 0
  and IRQs are enabled, IRQ is raised (the "new" or Sharp behaviour; the NEC
  "old" behaviour raises it only on a change from 1 to 0) [from MMC3].
- With the background at `$0000` and 8x8 sprites at `$1000`, A12 rises once a
  line, at the first sprite pattern fetch of the visible and pre-render lines
  [from MMC3]. The page says "PPU cycle 260"; in the model, whose fetches put
  their address out on the first of their two dots (section 6 of `ppu.md`), it
  is dot 261. `mmc3_test_2`'s `4-scanline_timing` passes with the rise on dot
  261, fails with status 2 ("Scanline 0 IRQ should occur later when
  $2000=$08") with it moved to 260, and with status 3 ("should occur sooner")
  with it moved to 262 [measured in task 11, 5 October 2026]. So the page's 260
  and the model's 261 are the same moment counted two ways, and the model's dot
  is the one the ROM accepts.
- With the background at `$1000` and sprites at `$0000` it clocks at dot 324 of
  the line before, and the pre-render line clocks twice every other frame [from
  MMC3].
- With 8x16 sprites A12 must be tracked fetch by fetch; empty slots fetch
  `$1FE0-$1FFF` [from MMC3].
- Writes to `$2006` and `$2007` accesses that move A12 also clock it [from
  MMC3].
- IRQs come every N + 1 lines for latch N [from MMC3].
- The IRQ holds the CPU's IRQ line low until `$E000` is written [inferring from
  "acknowledge any pending interrupts"].

### Worked example 3: the first IRQ after a reload

Background `$0000`, 8x8 sprites `$1000`, rendering on. In VBlank write `$C000` =
31, `$C001` = any, `$E001` = any. The pre-render line's rise finds the reload
flag set, so the counter = 31. Each line's rise then counts down: 30 after line
0, 29 after line 1, and 0 after line 30, so the IRQ is raised during line 30's
sprite fetches, the 32nd rise counting the reload [inferring from the counter
rules above]. That is the page's "N + 1" with N = 31. Later IRQs come every 32
lines while the latch stays 31 [inferring].

**MMC3 variants.** TxROM boards with four-screen VRAM (Rad Racer II) set the
header's four-screen bit [from MMC3; iNES]. Mapper 4 also covers MMC6
(StarTropics), told apart by NES 2.0 submapper 1 [from MMC3]. MMC3 A, B and C
differ in the IRQ at latch 0: Sharp chips fire every line, NEC chips once [from
MMC3]. `mmc3_test_2` has tests for both [from the fork:
mmc3_test_2/rom_singles]. For the model: the Sharp ("new") behaviour [from
MMC3: "games ... rely on the Sharp behavior", and the emulators that implement
only it run the others]. The fork's readmes call the chip in Super Mario Bros.
3 and Mega Man 3 the one whose counter at 0 reloads on every clock and raises
the IRQ "after decrementing/reloading, if the counter is zero" (the new
behaviour), and Crystalis's the other ("revision A") [from the fork:
mmc3_test_2/readme.txt, mmc3_irq_tests/readme.txt]. `5-MMC3` and
`6.MMC3_rev_B` test the first and pass; `6-MMC3_alt` and `5.MMC3_rev_A` test
the second and fail on the sub-test where the two differ (2 and 3) [measured in
task 11, 5 October 2026]. The readme of `mmc3_irq_tests` says "at most only one
will pass on a particular emulator".

## 7. The interface the mappers need

From the above, a mapper sees [inferring from sections 2 to 6]:

- CPU reads at `$4020-$FFFF` and CPU writes anywhere it decodes;
- PPU reads and writes at `$0000-$1FFF`, and the nametable arrangement for
  `$2000-$3EFF` (or its own four-screen RAM);
- every change of PPU A12, with the CPU cycle count so it can apply the M2
  filter (MMC3);
- consecutive-cycle writes (MMC1), which needs the CPU cycle of each write;
- an IRQ line out.

## 8. Open items

1. MMC3's exact clock dot, 260 or 261 (6). Settled in task 11: dot 261 in the
   model's numbering, the only one `4-scanline_timing` accepts.
2. CNROM bus conflicts (5) [guessing - verify].
3. PRG RAM size for MMC1 boards beyond 8 KB (3) [guessing - verify].
4. What the console's reset button does to MMC1 and the other boards' registers.
   The sheet gives only the power-on state. The model leaves them alone, as the
   chips have no reset line (task 10) [guessing - verify].
5. UxROM and AxROM submapper 2 as AND-type bus conflicts. The sheet says the
   submappers tell and does not give their numbers; task 10 took them from the
   nesdev UxROM and AxROM pages, which list 0 unknown, 1 none and 2 AND-type for
   both (read 5 October 2026).
6. MMC3's power-on bank registers and PRG RAM protect (6). The sheet says they
   are unspecified; the model's choice is the one the test ROMs need [guessing -
   verify].
7. What the PPU's address bus carries on the post-render line and when
   rendering is switched off mid-frame. The sheet says `v` "in VBlank or with
   rendering off"; the model tells the board of `v` only when a register write or
   a `$2007` access changes it, so a `v` with bit 12 set left by rendering is not
   seen as a rise until the program next moves it (task 11) [guessing - verify].
8. MMC6 (submapper 1) is not modelled: it runs as an MMC3, whose `$A001` means
   something else (6).
9. "The pre-render line clocks twice every other frame" with the background at
   `$1000` and sprites at `$0000` (6) is not modelled: the model clocks once on
   every rendering line, at dot 325, because it does not tell the board of the
   background's nametable fetches (`ppu.md` 6). Which low the second clock comes
   from, and so how long the real filter needs, is not on the sheet [guessing -
   verify].
