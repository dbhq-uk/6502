# Atari 2600 TIA: the line, the objects, motion, collisions, sound, colour and inputs

This sheet describes the TIA from its counters and latches: its horizontal timing, its registers, the playfield, the five movable objects, priority, the collision latches, HMOVE, the two sound channels, the colour signal the palette is computed from, and the six input ports, so the chip can be built from documents and not from another emulator.

Written 14 October 2026 for the Atari 2600 plan, task 1. Tags and sources are in [`README.md`](README.md). Used by tasks 4 (the horizontal counter and WSYNC, for the bus's halt), 5 (the line, the playfield, VSYNC, VBLANK and the order inside a cycle), 6 (the objects, priority and the collisions), 7 (HMOVE and the motion clocks), 9 (the inputs and VBLANK's latch bit, section 20), 10 (the sound, section 18), 5 again (the palette, section 19) and 11 (triage of the suite). Sections 18 to 20 also tag the SPG's hardware manual by its own numbers (`SPG TIA 1A 11.A`), its register entries by name (`SPG AUDC0`) and its address tables as `SPG write summary` and `SPG read summary`.

The suite's headers describe one PAL console; its reference pictures are missingno's renders aligned to that console's captures, and the spec only records the screenshot tests. Statements taken from a reference picture say so, and are weaker than a header's statement of a self-test.

## 1. Conventions: colour clocks, pixels and write cycles

- **Colour clock 0** is the first clock of a line's horizontal blank, where the horizontal sync counter starts its count at 0. A line is colour clocks 0 to 227 [from Towers: the HSync counter table, CLK column; from SPG 3.1: 160 + 68 = 228].
- **Pixel x** (0 to 159) is shown at colour clock 68 + x. The first 68 clocks are horizontal blank [from SPG 3.1; from Towers: "the first visible pixel won't appear until CLK=68"].
- **Write cycle k** (1 to 76) is the CPU cycle within the line on which a store's write happens. Cycle 1 is the first cycle that completes after a WSYNC halt; a store straight after `sta WSYNC` (`sta HMOVE`, three cycles) writes on cycle 3 [from the suite: roms/tia-timing/hmove-rewrite-race.asm, "strobes HMOVE three CPU cycles into a fresh scanline (write cycle 3)"; roms/tia-timing/wsync-line-end.asm: cycle 76 is the line's final cycle and the next is cycle 1].
- **A write on cycle k lands at colour clock W = 3k.** The suite's own comments say so: "write ends cycle 25 -> clock 75" [from the suite: roms/tia-timing/resp-restrobe.asm, lines 62 to 76], and "write end 69" for a strobe on cycle 23 [from the suite: roms/collision/blanked-reset.asm]. Towers's table numbers its CPU column the same way (CPU 55 is CLK 165) [from Towers: "CPU Clock to Player Pixel Table"]. Brad Mott's late HMOVE table counts "cycles after WSYNC" to the end of `STA HMOVE`, so his "Cyc" is the same k [from Mott 1998; inferring: his `sta Wsync / jmp ($82)` is commented "+5 5" and the last delay "+3 73"].

**Worked example.** `sta WSYNC`, then 12 two-cycle `nop`s after a 26-cycle preamble, then `sta COLUBK`: the store writes on cycle 53, W = 159, which is pixel 91 [inferring from the suite: roms/tia-timing/midline-color.asm, whose kernel puts the store on cycle 53 and whose header gives the pulse's left edge at x 91].

## 2. The registers by address

The TIA has six address lines, A0 to A5, and is selected where A12 and A7 are both low [from SPG TIA 1A detailed description, section 1: "the six (6) real address lines"; from 2k6specs: Memory Mirrors, "addressed by A12=0, A7=0"]. There is no register that is both read and written; one address can hold a write register and a different read register [from SPG TIA 1A section 1].

**Decoding.** Writes use all six lines, so each write register appears twice in each 128-byte window, at `$xx00` and `$xx40` [from 2k6specs: TIA Mirrors]. Reads use A0 to A3 only, so each read register answers at every `$10` step from `$00` to `$7F` of its window (`bus.md` section 2) [from the suite: roms/cpu/partial-drive.asm, "decodes reads from address lines A0-A3 only, ignoring A4, A5 and A6"; from 2k6specs: "input port are mirrored eight times in each window"]. The SPG lists the read registers as six-bit addresses `0` to `D` and does not say A4 and A5 are ignored for reads [from SPG TIA read address summary]. This sheet follows the suite and 2k6specs, which agree, and the suite's `cpu/partial-drive` self-test reads INPT4 at `$3C` and `$1C` and CXM0P at `$10`, `$20` and `$30`.

### 2.1 Write registers, `$00` to `$2C`

Bits are D7 to D0; `1` is a bit the register uses, `.` one it ignores. A strobe acts on any write; its data is ignored [from SPG 2.0].

| Address | Name | Bits used | What it does | Section |
|---|---|---|---|---|
| `$00` | VSYNC | `......1.` | D1 starts or stops vertical sync | 7 |
| `$01` | VBLANK | `11....1.` | D1 blanks the output; D6 enables the INPT4 and INPT5 latches; D7 dumps INPT0 to INPT3 to ground | 7 |
| `$02` | WSYNC | strobe | Pulls RDY low until the next line starts | 5 |
| `$03` | RSYNC | strobe | Resets the horizontal sync counter | 6 |
| `$04` | NUSIZ0 | `..111111` | Player 0 and missile 0 copies and player size (D2 to D0); missile 0 width (D5, D4) | 10, 11 |
| `$05` | NUSIZ1 | `..111111` | The same for player 1 and missile 1 | 10, 11 |
| `$06` | COLUP0 | `1111111.` | Colour and luminance of player 0 and missile 0 | 13 |
| `$07` | COLUP1 | `1111111.` | Player 1 and missile 1 | 13 |
| `$08` | COLUPF | `1111111.` | Playfield and ball | 13 |
| `$09` | COLUBK | `1111111.` | Background | 13 |
| `$0A` | CTRLPF | `..11.111` | D0 reflect, D1 score, D2 playfield priority, D5 and D4 ball width | 8, 12, 13 |
| `$0B` | REFP0 | `....1...` | D3 reflects player 0 | 10 |
| `$0C` | REFP1 | `....1...` | D3 reflects player 1 | 10 |
| `$0D` | PF0 | `1111....` | Playfield cells 0 to 3 | 8 |
| `$0E` | PF1 | `11111111` | Playfield cells 4 to 11 | 8 |
| `$0F` | PF2 | `11111111` | Playfield cells 12 to 19 | 8 |
| `$10` | RESP0 | strobe | Resets player 0's position counter | 9 |
| `$11` | RESP1 | strobe | Player 1 | 9 |
| `$12` | RESM0 | strobe | Missile 0 | 9 |
| `$13` | RESM1 | strobe | Missile 1 | 9 |
| `$14` | RESBL | strobe | Ball | 9, 12 |
| `$15` | AUDC0 | `....1111` | Audio control 0 (section 18) | |
| `$16` | AUDC1 | `....1111` | Audio control 1 | |
| `$17` | AUDF0 | `...11111` | Audio frequency 0 | |
| `$18` | AUDF1 | `...11111` | Audio frequency 1 | |
| `$19` | AUDV0 | `....1111` | Audio volume 0 | |
| `$1A` | AUDV1 | `....1111` | Audio volume 1 | |
| `$1B` | GRP0 | `11111111` | Player 0 graphics ("new"); copies new GRP1 to old GRP1 | 10 |
| `$1C` | GRP1 | `11111111` | Player 1 graphics ("new"); copies new GRP0 to old GRP0 and new ENABL to old ENABL | 10, 12 |
| `$1D` | ENAM0 | `......1.` | D1 enables missile 0 | 11 |
| `$1E` | ENAM1 | `......1.` | D1 enables missile 1 | 11 |
| `$1F` | ENABL | `......1.` | D1 enables the ball ("new") | 12 |
| `$20` | HMP0 | `1111....` | Motion of player 0, -8 to +7, positive is left | 15 |
| `$21` | HMP1 | `1111....` | Player 1 | 15 |
| `$22` | HMM0 | `1111....` | Missile 0 | 15 |
| `$23` | HMM1 | `1111....` | Missile 1 | 15 |
| `$24` | HMBL | `1111....` | Ball | 15 |
| `$25` | VDELP0 | `.......1` | D0 draws player 0 from its old register | 10 |
| `$26` | VDELP1 | `.......1` | Player 1 | 10 |
| `$27` | VDELBL | `.......1` | D0 draws the ball from its old enable bit | 12 |
| `$28` | RESMP0 | `......1.` | D1 locks missile 0 to player 0 and hides it | 11 |
| `$29` | RESMP1 | `......1.` | Missile 1 to player 1 | 11 |
| `$2A` | HMOVE | strobe | Starts the motion clocks | 15 |
| `$2B` | HMCLR | strobe | Sets all five motion registers to 0 | 15 |
| `$2C` | CXCLR | strobe | Clears all fifteen collision latches | 14 |

[from SPG TIA write address summary and Write Address Detailed Functions; from 2k6specs: TIA write address summary.] The two agree except that the SPG's summary table gives AUDC1 as `...11111` and AUDF1 as `....1111`, the reverse of AUDC0 and AUDF0 and of the SPG's own AUDF and AUDC sections (five bits of frequency, four of control). 2k6specs gives AUDC1 `....1111` and AUDF1 `...11111`. This sheet follows 2k6specs and the SPG's detailed sections, and takes the summary row as a transcription slip. The SPG's VBLANK section says D7 dumps "INPT1,2,3,6", where its section 8B says I0 to I3; this sheet follows 8B.

`$2D` to `$3F` have no write register in any source opened. [guessing - verify] a write there does nothing.

### 2.2 Read registers, `$00` to `$0D` (and every `$10` above)

| Address | Name | D7 | D6 | Lines driven |
|---|---|---|---|---|
| `$00` | CXM0P | M0-P1 | M0-P0 | D7, D6 |
| `$01` | CXM1P | M1-P0 | M1-P1 | D7, D6 |
| `$02` | CXP0FB | P0-PF | P0-BL | D7, D6 |
| `$03` | CXP1FB | P1-PF | P1-BL | D7, D6 |
| `$04` | CXM0FB | M0-PF | M0-BL | D7, D6 |
| `$05` | CXM1FB | M1-PF | M1-BL | D7, D6 |
| `$06` | CXBLPF | BL-PF | unused | D7, and D6 not stated [inferring from "most"; guessing - verify] |
| `$07` | CXPPMM | P0-P1 | M0-M1 | D7, D6 |
| `$08` | INPT0 | paddle 0 | | D7 |
| `$09` | INPT1 | paddle 1 | | D7 |
| `$0A` | INPT2 | paddle 2 | | D7 |
| `$0B` | INPT3 | paddle 3 | | D7 |
| `$0C` | INPT4 | fire 0 | | D7 |
| `$0D` | INPT5 | fire 1 | | D7 |
| `$0E`, `$0F` | none | | | nothing |

[from SPG TIA read address summary; from 2k6specs: TIA read address summary and Video Collision; from the suite: roms/collision/all-pairs.asm for the pairs, roms/cpu/open-bus.asm and roms/cpu/partial-drive.asm for the lines driven.] A line the TIA does not drive keeps the last byte on the data bus (`bus.md` section 4). The suite says "most collision registers drive D7 and D6" and an input port drives D7 [from the suite: roms/cpu/open-bus.asm]. The SPG and 2k6specs mark CXBLPF D6 "unused". Whether CXBLPF drives D6 as 0 or leaves it floating is not stated; [guessing - verify] it floats, as an unused input line does. Reads at `$0E` and `$0F` drive nothing and return the bus residue [from the suite: roms/cpu/floating-bus.asm].

**Worked example.** `lda $70` with no M0 collisions returns `$30`: D7 and D6 driven 0, the low six bits from the operand `$70` [from the suite: roms/cpu/open-bus.asm, code `$05`]. `lda $3C` (INPT4 through a mirror) with the button up returns `$BC` [from the suite: roms/cpu/partial-drive.asm, code `$03`]. `lda $2E` returns `$2E` [from the suite: roms/cpu/floating-bus.asm].

## 3. The horizontal sync counter

The horizontal timing comes from one 6-bit polynomial counter (a linear feedback shift register), not a binary counter [from Towers: "Polynomial Counters" and "Horizontal Sync Counter"; from SPG TIA 1A 2A: "a total count of 228"].

- **The clock.** Beside the counter a two-phase clock divides the colour clock by 4 and gives two non-overlapping pulses, H@1 and H@2, once each per 4 colour clocks. The counter steps on each H@1, H@2 pair, so it changes state once every 4 colour clocks [from Towers: "two-phase clock generator"].
- **The sequence.** Written as Towers prints the states (six bits, left to right), each step shifts the bits one place right and puts a new bit on the left, which is 1 when the two rightmost bits are equal and 0 when they differ. From `000000` this gives `100000`, `110000`, `111000`, `111100`, `111110`, `011111`, and so on through 63 states [inferring: the rule matches all 63 rows of Towers's table (`python3 -I tools/probes/atari-2600/towers_lfsr.py TIA_HW_Notes.txt`, 14 October 2026); Towers says only that the feedback is "a single two-input XNOR gate"].
- **The decodes.** A decode matrix beside the shift register watches for states. `111111` (the illegal state, possible at power on) forces a reset to `000000` [from Towers]. The line's events, at the colour clock where the counter enters the state [from Towers: the HSync counter table and its key]:

| Count | State | Colour clock | Decode | Effect |
|---|---|---|---|---|
| 4 | `111100` | 16 | SHS | Horizontal sync on |
| 8 | `110111` | 32 | RHS | Horizontal sync off, 4 clocks later |
| 12 | `001111` | 48 | RCB | Colour burst off, 4 clocks later |
| 16 | `011100` | 64 | RHB | Horizontal blank off, 4 clocks later: pixel 0 at colour clock 68 |
| 18 | `010111` | 72 | LRHB | Late RHB, used instead of RHB when the HMOVE latch is set: blank off at 76 (section 15) |
| 36 | `101100` | 144 | CNT | Centre: the playfield starts its second half, 4 clocks later, at colour clock 148 (pixel 80) |
| 56 | `010100` | 224 | SHB | Horizontal blank on, and the counter resets to `000000` 4 clocks later, so count 57 is count 0 at colour clock 228 |

The period is 57 counts of 4 colour clocks, 228 [from Towers]. The SPG gives blank as 68 counts and sync and burst as 16 counts each [from SPG TIA 1A 2A]. The line is the same length in NTSC and PAL, 228 colour clocks or 76 CPU cycles; only the number of lines a program makes differs [from the suite: include/region.h; from SPG PAL/SECAM conversions].

**Worked example.** From colour clock 0 the counter reads count 16 (`011100`) at colour clock 64; the first pixel shows at colour clock 68; count 36 at 144 starts the right half of the playfield at 148, pixel 80; count 56 at 224 starts the next blank, and colour clock 228 is the next line's colour clock 0.

## 4. When a register write takes effect inside a CPU cycle

The plan's fixed item 8 chooses how many of a cycle's three colour clocks run before the access. What each source states:

| Source | What it says |
|---|---|
| SPG TIA 1A section 1 | A write is taken "when the 02 clock goes from high to low", the end of the CPU cycle. A read drives D7 and D6 "while the 02 clock is high", the second half of the cycle |
| SPG TIA 1A section 2D | The TIA divides the colour clock by 3 and gives the CPU its clock, so the two are locked, three colour clocks a cycle |
| Towers, the score trick | Each store "makes the change on the -end- of the 3rd cycle" |
| Towers, CPU clock to player pixel table | A RESP0 on CPU cycle N acts at colour clock 3N (cycle 55 is CLK 165) |
| The suite, roms/tia-timing/hmove-line-end.asm | "A store's write reaches the TIA on the store instruction's final CPU cycle" |
| The suite, roms/tia-timing/resp-restrobe.asm | "write ends cycle 25 -> clock 75" |
| The suite, roms/tia-timing/midline-color.asm | A colour register write "takes effect at the colour clock the CPU performs it" |

**What follows** [inferring from the rows above and section 5]: cycle k runs colour clocks 3k - 3, 3k - 2 and 3k - 1, and its write is seen from colour clock 3k. That is the split with all three colour clocks before the access, with the WSYNC halt ending so that cycle 1 runs colour clocks 0, 1 and 2. A split of n clocks before and 3 - n after gives the same pictures only if the release point moves by 3 - n clocks, so tests that start every line from WSYNC see the split and the release point together, never the split alone. The sources state the end of the cycle, so the plan's tie-break gives three before, none after.

**Each path has its own delay after the write lands.** The landing clock W = 3k is the same for every register, but what the picture shows depends on the path inside the TIA. From the suite's headers and reference pictures (the pictures are recorded, not trusted):

| Register | First colour clock that shows the new value | Source |
|---|---|---|
| COLUBK (and the other colour registers) | W | [from the suite: roms/tia-timing/midline-color.asm header, x 91 for cycle 53] |
| VBLANK D1 | W + 1 | [from the suite: roms/tia-timing/midline-vblank_ntsc.png, rows 37 to 46 (`tools/probes/atari-2600/png_rows.py`) against its kernel: set on cycle 45 (W 135, pixel 67), the stripe pixel 67 still shows; cleared on cycle 44 (W 132, pixel 64), pixel 64 is still blanked; inferring the cycles from the kernel] |
| GRP0 | W + 1 | [from the suite: roms/tia-timing/midline-grp_ntsc.png, rows 39 to 44 (`png_rows.py`): GRP0 = 0 on cycle 41 (W 123, pixel 55) leaves pixels 28 to 55 lit and 56 dark; inferring the cycles from the kernel] |
| PF0, PF1, PF2 | A cell whose first pixel is at colour clock P shows the new bit if W is at most P - 3, the old bit if W is at least P - 1 | [from the suite: roms/tia-timing/midline-pf_ntsc.png, rows 37 to 44 (`png_rows.py`); see section 8] |
| RESP0, RESM0, RESBL | The object's first pixel at W + 5 (player) or W + 4 (missile, ball) | Section 9 |
| HMOVE | First motion pulse at the first grid slot at or after W + 7 | Section 16 |

**Reads.** [inferring from SPG TIA 1A section 1 and the split above] a read on cycle k returns the latches as they stand after colour clock 3k - 1. No source opened tests a collision read racing a collision on the same cycle.

**Worked examples for task 5** (cycle k after `sta WSYNC`, the line's colour clocks as in section 1):

| Write | k | W | Effect |
|---|---|---|---|
| `sta COLUBK` | 53 | 159 | Background changes from pixel 91 |
| `sty COLUBK` | 56 | 168 | Changes back from pixel 100 (so pixels 91 to 99 carry the first colour) |
| `sta VBLANK` (D1 = 1) | 45 | 135 | Pixel 67 still shows; blanked from pixel 68 |
| `sta RESP0` | 55 | 165 | Player 0 at pixels 102 onward from the next line |
| `sta RESM0` | 55 | 165 | Missile 0 at pixel 101 from the next line |

### 4.1 The split, as task 5 settled it

Task 5 ran the plan's rule ("Fixed before running", item 8) on 14 October 2026 with `python3 -I tools/probes/atari-2600/cycle-split.py <scratch>`, which builds the machine with each split as the one constant `Atari2600Bus.ClocksBeforeAccess`, then runs every trusted suite row and `CycleOrderTests`. In the model RDY is sampled at the access, after the cycle's clocks before it, and the TIA releases it on colour clock 0 of the next line (section 5), for every split.

| Clocks before the access | `tia-timing/wsync` (NTSC, PAL) | `tia-timing/wsync-line-end` (NTSC, PAL) | Other trusted rows | COLUBK on cycle 53 from power on | The same after `CLEAN_START` |
|---|---|---|---|---|---|
| 0 | pass, pass | pass, pass | all pass | pixel 91 | pixel 91 |
| 1 | pass, pass | pass, pass | all pass | not pixel 91 | pixel 91 |
| 2 | pass, pass | pass, pass | all pass | not pixel 91 | pixel 91 |
| 3 | pass, pass | pass, pass | all pass | pixel 91 | pixel 91 |

All four splits pass both trusted `tia-timing/` self-tests the line allows, so the tie rule decides: the sources state the end of the cycle (the table above), and **all three colour clocks run before the access**. The table shows why the tie was certain [inferring]: the split only moves the accesses against the colour clocks by a constant, which is the same as a different power-on phase. Split 0 is split 3 with the cycles counted one later. After `CLEAN_START`'s two RSYNC strobes every split behaves alike, because RSYNC restarts the line at a fixed distance from the write. Only a program that never strobes RSYNC, such as the eight collision tests that skip `CLEAN_START` (`suite.md` section 8), can see the split, and then only together with the TIA's power-on phase.


## 5. WSYNC and the halt

Writing WSYNC sets a one-bit latch that drives the CPU's RDY line low [from SPG TIA 1A 2D; from SPG WSYNC: "halts microprocessor by clearing RDY latch to zero"]. The latch is reset "by the leading edge of the next horizontal blank", which releases RDY [from SPG TIA 1A 2D and WSYNC]. On the 6507 RDY stops only read cycles; a write cycle always completes, and the CPU then holds at its next read, the following opcode fetch [from the suite: roms/cpu/rmw-wsync.asm]. So `inc WSYNC` strobes WSYNC twice on one line and still costs one line [from the suite: roms/cpu/rmw-wsync.asm]. Consecutive line starts are 76 cycles apart [from the suite: roms/tia-timing/wsync.asm].

**Where RDY goes high: colour clock 0** of the next line, the start of its horizontal blank [from SPG WSYNC; inferring the clock from section 3, where the blank starts as the counter becomes 0 at colour clock 228]. The first cycle that completes after the halt runs colour clocks 0 to 2 and is cycle 1 [inferring from section 4].

**The line end** [from the suite: roms/tia-timing/wsync-line-end.asm]: a WSYNC written on cycle 73, 74, 75 or 76 resumes at the next line's cycle 1. A write on cycle 76 is "absorbed at the wrap" and costs nothing extra. A write on cycle 77, which is cycle 1 of the next line, halts to the end of that line, 76 cycles more. [inferring] So the latch is reset at the line boundary itself and a write landing at colour clock 228 (cycle 76) is seen before it.

**Worked examples** (the halted cycles are repeated reads of the same address, one bus access each):

| `sta WSYNC` writes on cycle | Halted read cycles | Next opcode fetch |
|---|---|---|
| 20 | 56 (cycles 21 to 76) | Cycle 1 of the next line, colour clocks 0 to 2 |
| 73 | 3 (cycles 74 to 76) | Cycle 1 of the next line |
| 75 | 1 (cycle 76) | Cycle 1 of the next line |
| 76 | 0 | Cycle 1 of the next line |
| 1 (one cycle later than 76) | 75 (cycles 2 to 76) | Cycle 1 of the line after |
| `inc WSYNC`, writes on 19 and 20 | 56 | Cycle 1 of the next line |

## 6. RSYNC

RSYNC "resets the horizontal sync counter to define the beginning of horizontal blank time, and is used in chip testing" [from SPG RSYNC; from 2k6specs, the same]. Towers reads the schematic as: the strobe resets the counter's two-phase clock to the H@1 edge, and "a full H@1-H@2 cycle after RSYNC is strobed" the counter is reset to `000000` and horizontal blank turns on; he adds that it "requires more investigation" [from Towers: Horizontal Sync Counter]. The object position counters are not reset, so a mid-line RSYNC moves the playfield and not the objects [from the suite: roms/tia-timing/rsync.asm]. `CLEAN_START` strobes RSYNC twice through the stack's TIA mirror, and the suite's README warns that a wrong RSYNC shifts whole frames [from the suite: README.md, "A note on initialisation"].

**The model's choice (task 5)** [guessing - verify]: the line restarts **three** colour clocks after W, with the blank on and RDY released, as at any line start. Towers counts a full four-clock cycle of the two-phase clock from the strobe; the model counts it from one clock before the write lands, where the strobe is seen while phase two is high (SPG TIA 1A section 1). Three keeps the alignment the suite's headers give: every `CLEAN_START` program strobes RSYNC, and `tia-timing/midline-color`, one of them, states its pulse at x 91 for a write on cycle 53, which is W = 3k (section 1). Four would move every write of such a program one clock earlier. `CycleOrderTests` checks both, and task 6's `rsync` test bears on it. The choice is tied to where the WSYNC halt releases (section 5), so task 5's review asked whether RSYNC at four clocks with a later release would give the same alignment. Task 6 tried it on 14 October 2026 (`tools/probes/atari-2600/tia-object-faults.py`, faults X1 and X2, and the release two and three clocks later by hand in a scratch copy): `rsync` itself passes in every case, but the trusted `tia-render/playfield` fails in every case and `resp-restrobe` with the release up to a clock later, so the suite's object tests side with three clocks and a release on colour clock 0. It is still a fit to the suite, not a reading of the schematic. Whether RSYNC's line start releases a pending WSYNC is not stated; the model releases it, and no suite test tells (fault X3) [guessing - verify].

**Worked example.** RSYNC written on cycle 36 (W 108, pixel 40): 3 colour clocks later in the model (colour clock 111; Towers's full cycle would give about 4, colour clock 112) the counter is at 0 and the line restarts with 68 clocks of blank; the playfield block of PF0 is redrawn from the new line's pixel 0, while missile 0, parked near pixel 120 by a RESM0 on cycle 61 (the header says about 121; section 9 gives 119), keeps its own counter, so the two now meet and CXM0FB D7 sets [from the suite: roms/tia-timing/rsync.asm; the 4-clock delay from Towers; the exact clock is guessing - verify].

## 7. VSYNC and VBLANK

**VSYNC.** D1 = 1 starts vertical sync and D1 = 0 ends it; the program holds it for at least 3 lines [from SPG 3.3 and VSYNC]. The TIA has no vertical counter; vertical timing is the program's [from SPG TIA 1A 2B]. Composite sync is horizontal sync combined with the VSYNC bit [from SPG TIA 1A 2C]. The machine ends its frame at the end of VSYNC (plan, fixed item 2); nothing in the TIA marks it.

**VBLANK.** D1 = 1 forces the output to the blanking level, which shows black, over the playfield, the objects and the background alike; what is underneath is unchanged [from SPG VBLANK; from the suite: roms/tia-timing/midline-vblank.asm; from 2k6specs: "During blanking, playfield and moveable objects are hidden and the screen area becomes black"]. The gate is sampled per pixel, so a write mid-line blanks from that point on (section 4: one clock after W). VBLANK D1 is also the only signal that stops the collision latches from setting (section 14) [from the suite: roms/collision/blank-gating.asm]. [inferring] The position counters keep running under VBLANK: Towers gates their clock only by horizontal blank, and the SPG describes VBLANK only as an output and input control. D6 enables the INPT4 and INPT5 latches and D7 grounds INPT0 to INPT3 (section 20) [from SPG VBLANK and 12.0].

**Worked example.** VBLANK D1 set on cycle 45 and cleared on cycle 48 of a line with playfield stripes at pixels 64 to 67: pixel 67 still shows, pixels 68 to 76 are black [from the suite: roms/tia-timing/midline-vblank_ntsc.png, row 46; recorded, not trusted].

## 8. The playfield and CTRLPF

The playfield is 20 bits, each drawn 4 pixels wide, over the left half of the line; the right half repeats them or, with CTRLPF D0 = 1, reflects them [from SPG 5.0 and TIA 1A 3]. The bit order [from SPG PF0 serial output table; from 2k6specs: Video Playfield; from the suite: roms/tia-render/playfield.asm]:

| Cells | Pixels | Bits, left to right |
|---|---|---|
| 0 to 3 | 0 to 15 | PF0 D4, D5, D6, D7 |
| 4 to 11 | 16 to 47 | PF1 D7 down to D0 |
| 12 to 19 | 48 to 79 | PF2 D0 up to D7 |
| 20 to 39, repeat | 80 to 159 | the same 20 bits in the same order |
| 20 to 39, reflect | 80 to 159 | PF2 D7 to D0, PF1 D0 to D7, PF0 D7 to D4 |

The scan starts at the end of horizontal blank and the right half starts at the CNT decode (section 3) [from Towers; from SPG TIA 1A 3B and 3C]. The registers can be written at any time; a write while the register is being scanned changes only the cells not yet drawn [from SPG TIA 1A 3D; from the suite: roms/tia-timing/midline-pf.asm].

**When a mid-line write shows.** A cell whose first pixel is at colour clock P takes its bit from the register as it stands 2 colour clocks earlier: a write landing at W ≤ P - 3 shows in that cell, one at W ≥ P - 1 does not [from the suite: roms/tia-timing/midline-pf_ntsc.png, rows 37 to 44, against the kernel's cycles 45 and 48, 43 and 46, 41 and 44, 39 and 42, 37 and 40, 35 and 38; inferring the rule, which fits all six rows; recorded, not trusted]. W = P - 2 never occurs in that test (W is odd there), so [guessing - verify] which value a cell takes then. Task 5's model reads each cell's bit on clock P - 2, so a write at W = P - 2 shows; it reads CTRLPF's reflect bit with the cell's bit, which no source states [guessing - verify]. Towers says the playfield scan starts on RHB at colour clock 64, 4 clocks before its first pixel, without saying when the bits are read [from Towers].

**Worked examples.**

| Write | Result |
|---|---|
| PF0 = `$50`, PF1 = `$C3`, PF2 = `$A5`, CTRLPF = 0 | Lit pixels 0 to 3, 8 to 11 (PF0 D4, D6), 16 to 23, 40 to 47 (PF1), 48 to 51, 56 to 59, 68 to 71, 76 to 79 (PF2 D0, D2, D5, D7), and the same again from pixel 80 [inferring from the bit order; the pattern is the suite's roms/tia-render/playfield.asm] |
| PF0 = `$F0` only, CTRLPF D0 = 0 | Pixels 0 to 15 and 80 to 95 |
| PF0 = `$F0` only, CTRLPF D0 = 1 | Pixels 0 to 15 and 144 to 159 [from the suite: roms/tia-render/playfield-reflect.asm] |
| PF1 changed from `$00` to `$FF` by a store writing on cycle 29 (W 87), CTRLPF = 0 | Cells at pixels 16 to 23 (D7, D6; P = 84, 88) keep the old `$00`; pixels 24 to 47 show the new bits (P = 92 onward); the right-half copy at 96 to 127 is all new [inferring from the rule above] |
| PF2 `$FF` written on cycle 45 (W 135), `$00` on cycle 48 (W 144) | Only pixels 72 to 79 light [from the suite: roms/tia-timing/midline-pf_ntsc.png] |

## 9. The object position counters

Players, missiles and the ball each have a position counter of the same polynomial kind as the horizontal counter, with its own two-phase clock [from Towers: the player, missile and ball counter sections].

- **Length.** It counts 0 to 39 and wraps, 40 counts of 4 colour clocks, 160 [from Towers]. At count 39 (`101101`) it resets to `000000`, and that wrap is the START of the main copy [from Towers; from SPG TIA 1A 4A: "every time these counters pass through zero"].
- **Its clock.** The counters are clocked by MOTCK, an inverted colour clock that runs only while horizontal blank is off, 160 clocks a line. During the blank they stop "on the exact 1/4 phase" they were at [from Towers; from the suite: roms/tia-timing/resp-restrobe.asm]. So an object is drawn at the same pixel every line unless its counter is reset or given extra clocks (section 15) [from SPG TIA 1A 4A].
- **Copy decodes.** Player and missile counters also decode counts 3, 7 and 15, which start the close, medium and far copies when NUSIZ allows them (section 10) [from Towers; from SPG TIA 1A 4C]. The ball has only the wrap [from SPG TIA 1A 4B].
- **Start delay.** A START takes 4 colour clocks to decode; a player then takes 1 more to latch it, so a player's first pixel is one clock right of a missile's or the ball's from the same reset [from Towers; from the suite: roms/tia-render/draw-delay.asm and roms/collision/latency-swallow.asm, "5 clocks for a player, 4 for a missile or the ball"].
- **Reset.** RESP0, RESP1, RESM0, RESM1 and RESBL reset the counter, and its two-phase clock, to the colour clock of the write [from Towers: "resetting a Position Counter ... also resets the two-phase clock"; from the suite: roms/collision/reset-phase.asm]. The reset of a player or a missile does **not** start a copy: the main copy first shows when the counter next wraps, 160 visible clocks later, on the next line. The ball's reset does start it, on the same line (section 12) [from Towers; from the suite: roms/tia-timing/reset-same-line.asm and roms/tia-timing/resp-restrobe.asm]. Copies whose decodes come before the wrap do show on the strobe line [from Towers: Re-trigger section].

**Where an object lands.** The first lit pixel is the later of two arms: the strobe arm, W + 5 for a player and W + 4 for a missile or the ball, and the blanking arm, 3 (player) or 2 (missile, ball) clocks after horizontal blank ends [from the suite: roms/collision/blanked-reset.asm and roms/collision/copy-adjacency.asm]. Double- and quad-width players start one clock later still [from the suite: roms/tia-render/nusiz.asm]. With the normal blank ending at colour clock 68, that gives:

| Strobe on cycle k | W | Player 0 (1x) first pixel | Missile or ball first pixel |
|---|---|---|---|
| 1 to 22 (in the blank) | 3 to 66 | 3 | 2 |
| 23 | 69 | 7 (the strobe arm says 6; measured 7) | 5 |
| 24 | 72 | 9 | 8 |
| 34 | 102 | 39 | 38 |
| 55 | 165 | 102 | 101 |
| 69 | 207 | 144 | 143 |
| 75 | 225 | 162, so 2 on the next count (the copy wraps) | 161, so 1 |

[from the suite: roms/tia-timing/midline-resp.asm (cycle 34, pixel 39), roms/tia-timing/hmove-stuck-player.asm ("write cycle 55 -> P0 left edge at x=102"), roms/tia-timing/hmove-stuck-latch.asm ("write cycle 55 -> M0 lands at x=101"), roms/tia-timing/hmove-live-reach.asm ("write cycle 69 - BAND -> x = 143 - 3*BAND"), roms/collision/blanked-reset.asm (cycle 23: "write end 69 -> column 7"), roms/tia-timing/nusiz-draw.asm (a reset in the blank, base copy at x=3); from 2k6specs: a reset in the blank lands "3 pixels for P0/P1, and only 2 pixels for M0/M1/BL"; the cycle 75 row is inferring, wrapping modulo 160.]

**Towers disagrees for a reset in the blank:** he says it gives "player graphics at playfield pixel 1", and his table puts cycle 23 at pixel 6 [from Towers: CPU Clock to Player Pixel Table]. The suite (checked on a console, with a trusted self-test) and 2k6specs both give 3, and the suite measures 7 at cycle 23. This sheet follows the suite.

On a line with an early HMOVE the blank ends at colour clock 76, so the blanking arm is 79 (player) or 78 (missile, ball). A missile or ball reset written at colour clock 72 lands at pixel 10, at 75 at pixel 11, at 78 at pixel 14, when no motion pulse reaches it after the reset [from the suite: roms/collision/blanked-reset.asm]. A motion pulse after the reset moves it as usual (section 15).

**Strobing twice.** Two resets less than 160 clocks apart mean the main copy never shows; resetting every line hides it for good [from Towers: "If you hit RESPn at least twice on every scanline, you will never see the 'main' copy"]. A copy already decoded when a later reset lands still draws, moved: with resets 18 clocks apart its start is delayed 2 more clocks, which is 5 clocks after the second reset [from Towers: "Re-triggering after exactly 18, 33, 66 or 162 cycles"; from the suite: roms/tia-timing/resp-restrobe.asm, "The close-copy start armed by player 0's reset at 93 would naturally deliver at 46-49; the reset at 111 carries it to 48-51"]. The two sources agree on this example. A read-modify-write to a reset strobe resets twice, and the second write decides the position [from the suite: roms/cpu/rmw-strobe.asm].

### 9.1 The model's counter, as task 6 built it

`TiaPositionCounter` holds the count (0 to 39, the polynomial counter's place in its sequence) and the two-phase clock's quarter (0 to 3), stepped by MOTCK while the blank is off. These are the model's choices, each [inferring from this section's table; guessing - verify]:

- **The reset** holds the counter for four clocks and then starts it at count 0, quarter 0: two colour clocks, which run in the blank too, then two MOTCK clocks, which wait for the blank to end. This one split gives both arms of the landing rule: a missile written at W on the drawn line restarts at W + 4, and one written in the blank at colour clock 70.
- **A decode is held** until the two-phase clock next completes a cycle, and only then becomes the start signal, for the four clocks of that count; NUSIZ is read there. The main copy is decoded at count 39 and starts as the counter wraps; the close, medium and far copies at counts 3, 7 and 15. A missile or the ball draws from the clock its start begins, a single-width player one clock later.
- **A reset's end is a boundary of the two-phase clock**, so a decode held when a reset lands is delivered as the reset ends, 4 clocks after the write (5 for a player): the ride-through of `resp-restrobe` and Towers's "delayed up to 3 CLK". A reset that lands before the decode is made stops the counter short of it, and that copy is lost.
- **The ball's reset holds a main-copy decode**, so the ball draws where its count restarts, on the strobe line.
- **What fits and what does not.** Every row of the table above fits, for players and for missiles and the ball, except a player reset at colour clock 69, which the model lands at 6 where the suite measured 7 (open item 7). Task 7's `merge-delivery-replant` measured it.
- **Outputs through the blank.** The counters stop in the blank and a start already delivered stays on, so an object whose start falls at a line's end keeps its output through the blank, and objects collide there (section 14); `TiaCollisionTests.Section_14_the_latches_set_in_the_blank` holds it. `collision/blank-gating` passes from task 6 on a visible overlap, not on this: with HMOVE doing nothing its group 2 missiles meet at pixel 2. Task 7 proved the blank mechanism through HMOVE's clocks, as the header does: with HMOVE built, its group 2 missiles meet inside the blank, and stopping the latches in the blank fails it (the task 7 journal entry).
- **A reset cuts off a start**, one being drawn and one a player's scan counter has not taken yet, at the end of the clock the write lands on (section 17.1). The suite's `collision/resbl-kill` says so for the ball and `hmove-values` for a missile; the model does it for every object [for players guessing - verify].

## 10. Players

- **Scan counter.** Each player has a 3-bit binary counter that picks which of the 8 bits is drawn. It is never reset: once a START arrives it counts 0 to 7, clocked by MOTCK, so a player that runs off the right edge finishes at the start of the next line [from Towers: Player Graphics Scan Counters]. A START that arrives while the scan is running restarts it at 0 in the model; no source says [guessing - verify].
- **Size.** NUSIZ lets the scan counter step every 1, 2 or 4 clocks; it is read every clock and can change mid-line [from Towers; from SPG TIA 1A 6C]. A NUSIZ write that completes in the horizontal blank applies on that line [from the suite: roms/tia-timing/nusiz-draw.asm].
- **Bits.** D7 is drawn first unless REFP D3 is set, which draws D0 first. The reflect bit is read at every pixel [from SPG GRP0 and REFP0; from Towers; from the suite: roms/tia-render/players.asm and roms/tia-render/player-reflect.asm].
- **Live graphics.** GRP is read as the player is drawn, so a write mid-player changes the bits not yet drawn [from Towers: "safe to modify GRPn at any time, with immediate effect"; from the suite: roms/tia-timing/midline-grp.asm]. Section 4 gives the one-clock delay seen in the reference picture.
- **Vertical delay.** Each player has a "new" register, written by GRPn, and an "old" one. A write to GRP1 copies new GRP0 into old GRP0; a write to GRP0 copies new GRP1 into old GRP1. VDELPn = 1 draws the old register, 0 the new; it is read at every pixel [from Towers; from SPG TIA 1A 6D; from the suite: roms/tia-render/vertical-delay.asm]. 2k6specs describes the same thing the other way round (writes held in GRPxA until GRPy is written), with the same result [from 2k6specs: Notes on High Resolution Text and Vertical Delay].

**NUSIZ D2 to D0** [from SPG NUSIZ0; from Towers: the player counter decodes; from the suite: roms/tia-render/nusiz.asm and roms/tia-timing/nusiz-draw.asm]. With the main copy's first pixel at x:

| Value | Copies | Pixels with GRP0 = `$FF`, main copy reset in the blank (x = 3) |
|---|---|---|
| `%000` | One | 3 to 10 |
| `%001` | Two, close (main, +16) | 3 to 10, 19 to 26 |
| `%010` | Two, medium (main, +32) | 3 to 10, 35 to 42 |
| `%011` | Three, close (main, +16, +32) | 3 to 10, 19 to 26, 35 to 42 |
| `%100` | Two, wide (main, +64) | 3 to 10, 67 to 74 |
| `%101` | One, double width | 4 to 19 |
| `%110` | Three, medium (main, +32, +64) | 3 to 10, 35 to 42, 67 to 74 |
| `%111` | One, quad width | 4 to 35 |

The +16, +32 and +64 come from the decodes at counts 3, 7 and 15, which are 4, 8 and 16 counts after the wrap [from Towers]. The double and quad copies start one clock later than a single-width copy from the same reset [from the suite: roms/tia-render/nusiz.asm and roms/tia-render/object-priority.asm].

**Worked examples.**

- RESP0 on cycle 55, NUSIZ0 = `%001`, GRP0 = `$80`: on the strobe line only the close copy shows, at pixel 118; from the next line pixels 102 and 118 [from Towers: CPU Clock to Player Pixel Table, row 55: main 102, close 118, medium 134].
- GRP0 = `$B4` at quad width from pixel 37: lit pixels 37 to 40, 45 to 52, 57 to 60; with REFP0 D3 set: 45 to 48, 53 to 60, 65 to 68 [inferring from the bit order; the pattern and span are the suite's roms/tia-render/players.asm and player-reflect.asm, which probe pixel 38 and 50 lit without reflection, 56 and 68 with it].
- Vertical delay: VDELP0 = 1, then GRP0 = `$F0`, GRP1 = any (old GRP0 becomes `$F0`), GRP0 = `$0F` (new GRP0 is `$0F`). Player 0 draws `$F0`; with VDELP0 = 0 it draws `$0F` [from the suite: roms/tia-render/vertical-delay.asm].

## 11. Missiles

A missile's counter is a player's counter without the extra clock: the same decodes, the same NUSIZ copies (D2 to D0), but the double and quad settings give one copy, not stretched [from Towers: Missile Position Counters; from the suite: roms/tia-render/missile-copies.asm]. Its width is NUSIZ D5 and D4: 1, 2, 4 or 8 pixels, by the same circuit as the ball's (section 12) [from SPG NUSIZ0; from Towers]. ENAMn D1 turns it on [from SPG ENAM0].

**RESMP.** While RESMPn D1 = 1 the missile is hidden and its counter is reset whenever player n's scan counter reaches `%100` during the player's main copy, not its other copies [from SPG RESMP0; from Towers: "resetting the M0 counter when the P0 graphics scan counter is at %100 ... AND the main copy of P0 is being drawn"]. When D1 is cleared the missile shows at a fixed offset right of the player's first pixel: +4 at 1x, +6 at double width, +10 at quad width [from the suite: roms/tia-render/missile-reset-lock.asm, a trusted self-test]. The SPG calls this "the center" of the player [from SPG RESMP0]. 2k6specs gives +3, +6 and +10 [from 2k6specs: RESMP0]; for 1x it disagrees with the suite by one pixel, and this sheet follows the suite.

**Worked examples.** NUSIZ0 = `$30` (8 pixels wide, one copy), missile at 17: pixels 17 to 24. NUSIZ0 = `$33`: pixels 17 to 24, 33 to 40 and 49 to 56. NUSIZ0 = `$35` or `$37`: pixels 17 to 24 only [from the suite: roms/tia-render/missile-copies.asm]. Player 0 at 1x from pixel 50, RESMP0 = `$02`, then `$00`: missile 0 shows at pixel 54 on the following lines; at quad width (player from 51) it shows at 61 [from the suite: roms/tia-render/missile-reset-lock.asm; which line it first shows on is guessing - verify].

### 11.1 RESMP in the model, as task 6 built it

The model resets the missile's counter as the player's main copy's scan counter reaches `%010`, and the missile then lands two clocks after the player's third bit starts, 2, 4 or 8 pixels in: 4, 6 and 10 pixels right of a single, double and quad player's first pixel, as the suite measures. Towers's `%100` with any one delay gives 4, 8 and 16, the SPG's "centre"; `%010` is the one scan count whose time, with one delay, gives all three of the suite's offsets [inferring from the suite's roms/tia-render/missile-reset-lock.asm; it disagrees with Towers's bit pattern; guessing - verify]. The player's scan counter is clocked every clock at single width, on the two-phase clock's quarters 1 and 3 at double width and on quarter 1 at quad width, the quarters that give the first pixels and widths of section 10 [inferring; guessing - verify].

## 12. The ball

The ball's counter has only the wrap decode [from SPG TIA 1A 4B]. **RESBL sends a START**, so the ball draws at its new place on the strobe line itself, 4 clocks after the write, and can be retriggered many times a line [from Towers: Ball Horizontal Position Counter; from the suite: roms/tia-timing/reset-same-line.asm]. A RESBL also erases a ball pixel already started but not yet shown, including the pixel due on the clock the erasure acts; a pixel already shown stays [from the suite: roms/collision/resbl-kill.asm].

**Width.** CTRLPF D5 and D4: `00` 1, `01` 2, `10` 4, `11` 8 pixels [from SPG CTRLPF]. The circuit [from Towers: Ball/Missile width]: the START is 4 clocks long; with D5 and D4 clear a one-in-four phase clock gives one pixel, D4 a two-in-four clock gives two, D5 lets the whole START through for four, and with both set a copy of START delayed 4 clocks is added for eight.

**Vertical delay.** ENABL writes the "new" enable bit; a GRP1 write copies new into old; VDELBL D0 = 1 draws from the old bit [from Towers; from SPG TIA 1A 6E].

**Worked example.** RESBL on cycle 55, CTRLPF = `$20`, ENABL = 2: the ball draws pixels 101 to 104 on that line and every line after [from the suite: roms/tia-timing/reset-same-line.asm for the same-line draw; inferring the column from section 9]. A RESM0 on the same cycle draws nothing on that line and pixel 101 from the next [from the suite: roms/tia-timing/reset-same-line.asm].

## 13. Priority and score mode

Each missile shares its player's colour register and priority, and the ball shares the playfield's [from SPG 4.0 and TIA 1A 8.5B]. The priority encoder selects one of four colour registers per pixel [from SPG TIA 1A 8.5C and 9B].

| CTRLPF D2 | Front | Second | Third | Back |
|---|---|---|---|---|
| 0 | P0, M0 (COLUP0) | P1, M1 (COLUP1) | PF, BL (COLUPF) | BK (COLUBK) |
| 1 | PF, BL (COLUPF) | P0, M0 | P1, M1 | BK |

[from SPG 9.0 and TIA 1A 8.5; from the suite: roms/tia-render/object-priority.asm and roms/tia-render/pf-priority.asm.]

**Score mode, CTRLPF D1.** The playfield takes COLUP0 in the left half (pixels 0 to 79) and COLUP1 in the right half (80 to 159) (in the model, the halves are the playfield's own, from the CNT decode, so they move with RSYNC as the playfield does [guessing - verify]) [from SPG CTRLPF and TIA 1A 8.5C; from the suite: roms/tia-render/score-mode.asm]. 2k6specs adds that the ball keeps COLUPF, that the playfield takes player 0's priority on the left and player 1's on the right, and that the score bit is ignored when D2 is set [from 2k6specs: CTRLPF and Object Colors and Priorities]. No other source opened states those three; [guessing - verify].

**Worked examples.**

- CTRLPF = 0: player 0 over a playfield stripe shows COLUP0; player 1 over the stripe shows COLUP1; where players 0 and 1 overlap, COLUP0 [from the suite: roms/tia-render/object-priority.asm, pixels 48 to 49, 80 and 62 to 63].
- CTRLPF D2 = 1: the same stripe over the player shows COLUPF [from the suite: roms/tia-render/pf-priority.asm, pixels 34 to 35].
- CTRLPF = `$02`, COLUP0 green, COLUP1 blue, COLUPF `$46`, stripes every 16 pixels: stripes at 0 to 67 are green, at 80 to 147 blue, and `$46` shows nowhere [from the suite: roms/tia-render/score-mode.asm].

## 14. The collision latches

Fifteen latches, one per pair of the six objects (P0, P1, M0, M1, BL, PF). A latch sets when both of its objects put out a pixel on the same colour clock and holds until CXCLR clears all fifteen at once [from SPG 10.0 and TIA 1A 7; from the suite: roms/collision/latches.asm, roms/collision/per-pixel.asm and roms/collision/all-pairs.asm]. They are read two to a register on D7 and D6 (the table in section 2.2).

- **What sets them is the objects' serial outputs, not the picture.** Horizontal blank, the HMOVE comb and the extended blank do not stop them: an object whose output fires inside the blank collides there. Only VBLANK D1 gates them [from the suite: roms/collision/blank-gating.asm, from the TIA-1A schematic as its header says; from 2k6specs: "Collision checks still take place below of the extra blanking area"].
- Priority does not affect them [from the suite: roms/tia-render/object-priority.asm].
- A missile hidden by RESMP, or a dot swallowed by a motion pulse (section 16), puts out nothing and collides with nothing [from the suite: roms/collision/stuck-drift.asm; from SPG RESMP0].

**Worked examples** (each latch by its own pair and no other) [from the suite: roms/collision/all-pairs.asm]: all five objects at one pixel over a lit playfield set all fifteen bits, so D7 and D6 read 1 in CXM0P to CXPPMM, and D7 reads 1 in CXBLPF. With the playfield clear, P0, M0 and BL together on the left and P1 and M1 together on the right: CXM0P = D6 only (M0-P0), CXM1P = D6 only (M1-P1), CXP0FB = D6 only (P0-BL), CXP1FB = 0, CXM0FB = D6 only (M0-BL), CXM1FB = 0, CXBLPF = 0, CXPPMM = 0. Two quad players at the same place with GRP0 = `$AA` and GRP1 = `$55` never collide; with both `$AA` they do [from the suite: roms/collision/per-pixel.asm].

## 15. HMOVE and the motion clocks

**The mechanism** [from Towers: "Playing with the HMOVE registers"; from the suite: roms/tia-timing/hmove-rewrite-race.asm and hmove-stuck-latch.asm].

1. **The motion registers.** HMP0, HMP1, HMM0, HMM1 and HMBL each hold D7 to D4 in four latches with built-in comparators. The value is a signed 4-bit number, +7 to -8, positive moving left [from SPG HMP0 and TIA 1A 5A]. Towers: the D7 comparator is wired inverted, so the register acts as a count v = (D7 to D4) XOR `%1000` of pulses to send: `$70` gives 15, `$00` 8, `$80` 0, `$F0` 7.
2. **The ripple counter.** HMOVE loads a 4-bit counter to 15; it counts down one step per 4 colour clocks, and is wired to all five comparators [from Towers]. It rests at `%1111` [from Towers; from the suite: roms/tia-timing/hmove-stuck-latch.asm].
3. **The more-motion latches.** HMOVE sets one latch per object. While its latch is set, the object gets one extra clock, a motion pulse, every 4 colour clocks, on a fixed grid of the line [from Towers: "on every H@1 signal from the HSync two-phase clock"]. The grid slots are colour clocks 1, 5, 9, ..., 225: 17 of them in the 68-clock blank, and 225 the last of the line [from the suite: roms/tia-timing/hmove-stuck-grid.asm, "fixed to the line", and hmove-stuck-straddle.asm, "The line's last step slot is colour clock 225"; inferring the residue from 225].
4. **The stop.** An object's latch clears on the step where the ripple counter equals the register's nibble with its low three bits inverted, w = (D7 to D4) XOR `%0111` [from the suite: roms/tia-timing/hmove-stuck-latch.asm]. Towers states the same thing as "none of the 4 bits match" between the counter and v, because w is v with every bit inverted. The object therefore gets 15 - w = v pulses.
5. **Where the pulses go.** A pulse reaches the object's position counter on the same clock line as MOTCK. In the blank, where MOTCK is off, each pulse advances the counter one step, one pixel left. On the drawn line it merges with a MOTCK clock and does not move the object, but it can disturb the object's output (section 16) [from Towers: "HMOVE during the visible scanline"; from the suite: roms/collision/stuck-drift.asm and roms/tia-timing/hmove-live-seam.asm].
6. **The extended blank (the comb).** If the HMOVE latch is set when the counter passes count 16, horizontal blank ends on LRHB at colour clock 76 instead of 68. The objects then miss 8 MOTCK clocks, which moves everything except the playfield 8 pixels right, and the first 8 pixels of the line are blank: the comb [from Towers; from SPG TIA 1A 5B; from the suite: roms/tia-timing/hmove-comb.asm]. The HMOVE latch is cleared near the end of the line (section 16).
7. **Net motion of an early HMOVE.** v pulses left in the blank, 8 right from the comb: v - 8 = the signed value, as documented [from Towers; from the suite: roms/tia-timing/hmove-rewrite-race.asm, "fifteen pulses minus those eight is $70's usual seven-pixel move"].
8. **HMCLR** sets all five registers to 0 (w = `%0111`) and does not touch the more-motion latches [from SPG HMCLR; from Towers: "The HMCLR command does not reset these latches"].
9. **The player's scan counter** also gets the pulses, so pulses in the blank eat a wrapped player's remaining pixels: v pixels at 1x [from Towers: Graphics Scan Counters during HMOVE].

**Timing of an HMOVE written on cycle k (W = 3k)** [inferring: a fit to Mott's table and the suite's headers below, not read from the schematic]: the first pulse falls on the first grid slot at or after W + 7, the others every 4 clocks, so v pulses at most. For k = 3: W = 9, pulses at 17, 21, ..., 73 (15 for `$70`), all before the blank ends at 76. Towers gives the first comparison "15 CLK after STA HMOVE begins" and the first decrement at 17 [from Towers], which agrees to within the cycle.

**Worked example: each value, HMOVE on cycle 3** [from the suite: roms/tia-timing/hmove-values.asm, missile 0 from pixel 17]:

| HMM0 | Signed | v (pulses) | Pixel after |
|---|---|---|---|
| `$00` | 0 | 8 | 17 |
| `$10` to `$70` | +1 to +7 | 9 to 15 | 16 down to 10 |
| `$80` | -8 | 0 | 25 |
| `$90` to `$F0` | -7 to -1 | 1 to 7 | 24 down to 18 |

The low nibble is ignored [from the suite: roms/tia-timing/hmove-values.asm]. Each HMOVE applies the value again; the register is not consumed: HMM0 = `$80` and eight HMOVEs on eight lines move a missile 64 pixels right [from SPG 8.0; from 2k6specs; from the suite: roms/tia-timing/hmove-walk.asm, missile from about 14, inside 64 to 79 after 7 and 8 strobes]. Fifteen nonzero values each move the object and `$00` does not; HMCLR before HMOVE cancels a pending move [from the suite: roms/tia-timing/hmove-apply.asm]. HMOVE on cycle 3 of any line gives the same move [from the suite: roms/tia-timing/hmove-line-aligned.asm].

**Worked example: the comb.** HMOVE on cycle 3 with a playfield stripe at pixels 0 to 3: the stripe is blank on that line; pixels 0 to 7 show black whatever the playfield or background; the stripe at 16 to 19 is untouched [from the suite: roms/tia-timing/hmove-comb.asm].

**Worked example: a reset during the pulses.** `sta WSYNC`, `sta HMOVE` (cycle 3), `sta RESBL` (cycle 6, W 18), all HMxx = 0: the ball lands at pixel 3. The reset and a motion pulse fall on the same clock and count once; counting both puts the ball at 2. Then one more HMOVE on a later line walks it by HMBL: `$00` to `$70` give 3, 2, 1, 0, 159, 158, 157, 156, and `$80` to `$F0` give 11 down to 4 [from the suite: roms/tia-timing/hmove-reset-merge.asm, a trusted self-test]. [inferring] Section 9's rule gives the same: the blank ends at 76, the ball's blanking arm is 78 (pixel 10), and the 7 pulses at 21 to 45 that follow the reset move it to 3. The suite's wording that the pulse and the reset share a clock does not fit pulses on the 1-modulo-4 grid with the reset at W = 18 [guessing - verify] by one clock.

## 16. Late and early HMOVE

Where HMOVE is written decides three things: how many pulses fall in the blank (and move the object), whether the comb happens, and on which line.

**The comb.** Written on cycles 1 to 20 (W up to 60), HMOVE gives the comb on that line. On cycles 21 to 74 it gives none. On cycles 75 and 76 (W 225 and 228) the comb, and the whole move, land on the next line, as if HMOVE had been written at its start [from Mott 1998, the HBLANK column; from the suite: roms/tia-timing/hmove-line-end.asm, "The comb is absent for k<=74 and present from k=75"]. Towers explains the cycle 74 trick: the latch is cleared "when the HSync Counter wraps around", so an HMOVE just before that clear gives pulses but no comb, and every object moves 8 + value pixels left [from Towers]. [inferring] The latch must be set by about colour clock 61 to change RHB at count 16 into LRHB, and is cleared between colour clocks 222 and 225; the exact clocks of the set and the clear are [guessing - verify].

**Pulses that count.** With the first pulse at the first slot at or after W + 7 (section 15), the object moves left one pixel for each pulse that falls in a blank, and right 8 pixels if the comb happened on that line. The blank ends at colour clock 68, or 76 with the comb. Pulses on the drawn line do not move the object. Pulses that run past colour clock 227 continue into the next line's blank and move the object there [inferring; checked against every cell of the table below on 14 October 2026].

**The movement table** (Δx in pixels on the line the move shows, negative is left; HMOVE written on cycle k after `sta WSYNC`) [from Mott 1998, players, by "hitting HMOVE at various cycles after WSYNC"; cycles 3 to 5 from his rows 79 to 81, which repeat the line; the `$70` and `$80` columns agree with the suite's roms/tia-timing/hmove-line-end.asm (missiles, k = 65 to 80), hmove-late.asm and hmove-strobe-line.asm (k = 8 to 22, settled columns 20, 21, 23, 24, 26, 27, 29, then 23 from a base of 23), and hmove-stuck-grid.asm (k = 4 and 5)]:

| k | W | Comb | `$00` | `$70` | `$80` | `$F0` |
|---|---|---|---|---|---|---|
| 3 | 9 | this line | 0 | -7 | +8 | +1 |
| 4 | 12 | this line | 0 | -6 | +8 | +1 |
| 5 | 15 | this line | 0 | -5 | +8 | +1 |
| 8 | 24 | this line | 0 (model) | -3 | +8 (model) | +1 (model) |
| 10 | 30 | this line | 0 | -2 | +8 | +1 |
| 12 | 36 | this line | 0 | 0 | +8 | +1 |
| 14 | 42 | this line | +1 | +1 | +8 | +1 |
| 16 | 48 | this line | +3 | +3 | +8 | +3 |
| 18 | 54 | this line | +4 | +4 | +8 | +4 |
| 20 | 60 | this line | +6 | +6 | +8 | +6 |
| 21 to 54 | 63 to 162 | none | 0 | 0 | 0 | 0 |
| 55 | 165 | none | 0 | -1 | 0 | 0 |
| 60 | 180 | none | 0 | -5 | 0 | 0 |
| 64 | 192 | none | -1 | -8 | 0 | 0 |
| 65 | 195 | none | -2 | -9 | 0 | -1 |
| 70 | 210 | none | -5 | -12 | 0 | -4 |
| 73 | 219 | none | -8 | -15 | 0 | -7 |
| 74 | 222 | none | -8 | -15 | 0 | -7 |
| 75, 76 | 225, 228 | next line | 0 | -7 | +8 | +1 |
| 80 (4 of the next line) | 12 | that line | 0 | -6 | +8 | +1 |

(The k = 8 row is from the suite, hmove-late band 0 and hmove-strobe-line band 0, `$70` only; its other cells come from the model below and are [guessing - verify].) Two cells of the model, for task 7's tests: HMOVE on cycle 65 with `$70` gives pulses at 205, 209, ..., 225 (6, on the drawn line, no move) and 1, 5, ..., 33 of the next line (9, in its blank), so -9 with no comb. On cycle 16 with `$F0` (7 pulses): pulses at 57, 61, ..., 81, of which 57 to 73 (5) fall before the blank ends at 76, so 8 - 5 = +3.

2k6specs gives a different and self-doubting table of "extra motion offsets" and says the results "are kinda unpredictable" [from 2k6specs: HMOVE Extra Motion Offsets]. This sheet follows Mott and the suite, which agree with each other.

**Pulses on the drawn line distort objects** [from the suite: roms/tia-timing/hmove-live-seam.asm, hmove-live-reach.asm, hmove-strobe-line.asm and roms/collision/stuck-drift.asm; recorded pictures except stuck-drift, which is a trusted self-test]. HMOVE on cycles 21 to 54 moves nothing but its pulses still run across the picture. A 1-pixel missile at pixel x on a pulsed line: x modulo 4 = 2 is swallowed (nothing drawn, no collision), x modulo 4 = 3 is widened to 2 pixels (x - 1 and x), 0 and 1 draw normally. HMOVE on cycle 33 with HMM0 = `$70` distorts dots at pixel 98 and left of it, nothing right of 98. Wider missiles and the ball lose or gain one clock, never vanish; players' patterns are taken or refused by rules the suite's `collision/merge-delivery*.asm` headers tabulate. Towers predicted pulses on the drawn line "plug" MOTCK clocks rather than stuff extra ones [from Towers]. [guessing - verify] that the merge of a pulse and a MOTCK clock reproduces the suite's residue rules without a special case; task 7 checks it against those tests.

## 17. Writes to the motion registers during HMOVE

The SPG says not to change the motion registers for 24 cycles after HMOVE, as "Unpredictable motion values may result" [from SPG HMP0 and 8.0]. Towers explains why: the comparators compare the live register with the ripple counter, so a new value changes when the latch clears [from Towers].

- A new value whose stop step is still ahead works normally [from Towers].
- A new value whose stop step has passed never matches the steps left, so the latch is still set when the counter comes to rest at `%1111`. It then keeps sending a pulse every 4 colour clocks, on every line, with no further HMOVE, until a write makes w = `%1111`, which only `$80` to `$8F` do. HMCLR does not free it. In the blank 17 pulses a line move the object 17 pixels left each line (the Cosmic Ark starfield) [from Towers; from the suite: roms/tia-timing/hmove-stuck-latch.asm, hmove-stuck-release.asm and hmove-stuck-grid.asm].
- Which value a comparison sees: while the counter descends, each step's comparison reads the register as it stood a few clocks before that step's pulse; the final comparison, after the 15th pulse, reads it a clock or so after that pulse [from the suite: roms/tia-timing/hmove-rewrite-race.asm and hmove-stuck-latch.asm]. [inferring, fitted to the two worked examples below] the comparison for the pulse at slot P reads the register as at colour clock P - 3, and the final one as at P + 1 where P is the 15th pulse's slot.

**Worked examples, HMOVE on cycle 3 (pulses at 17, 21, ..., 73), HMM0 = `$70`, missile 0 parked at pixel 101:**

| Then written | On cycle (W) | Result |
|---|---|---|
| HMM0 = `$60` | 20 to 23 (60 to 69) | Latch clears before the 15th pulse: 14 pulses, missile at 95 |
| HMM0 = `$60` | 24 (72) | Jammed: misses the step for counter 1 and fails the final comparison. Drifts 17 pixels left a line, wrapping |
| HMM0 = `$60` | 25 to 35 (75 to 105) | Too late, no effect: 15 pulses, missile at 94 |
| HMM0 = `$00` | 19 (57) | Jammed (its stop step, counter 7, was the pulse at 49) |
| then HMM0 = `$70` | any later line | Still jammed |
| then HMM0 = `$80` | any later line | Freed at once; the missile freezes "a few colour clocks left" of where it was |

[from the suite: roms/tia-timing/hmove-rewrite-race.asm (bands 0 to 15, cycles 20 to 35) and hmove-stuck-latch.asm; Towers: Cosmic Ark wrote `$60` to HMM0 "21 cycles after HMOVE starts", which is cycle 24.] HMCLR on cycle 19 after an HMOVE on cycle 3, with all five registers at `$70`, should jam all five objects the same way [inferring from Towers: "a HMCLR 20 cycles in has the same effect", and the `$00` row above].

**The stuck drift** [from the suite: roms/tia-timing/hmove-stuck-latch.asm, hmove-stuck-grid.asm, hmove-stuck-widths.asm, hmove-stuck-player.asm and hmove-stuck-stretch.asm]: the pulses keep to the line's grid, not to when HMOVE was written; jamming from an HMOVE on cycle 4 or 5 instead of 3 gives a first-line move of -6 or -5 instead of -7, and the same 4-line pattern after. The drift of 17 a line is 1 more than a multiple of 4, so a 1-pixel dot cycles through the four residues of section 16 every 4 lines: widened, missing, normal, normal. A player at 1x draws its pattern intact on three lines in four and a solid 4-pixel block over offsets 2 to 5 on the fourth.

### 17.1 HMOVE in the model, as task 7 built it

`TiaMotion` holds the ripple counter, the five more-motion latches and the HMOVE latch; the objects take its extra clocks on the position counters of section 9.1. There is no table of cases: every row of section 16's movement table, the comb's boundaries and section 17's worked examples come from these pieces and when they act. The part names (SEC, SECL, the extra clocks) are jigo2600's, from its reading of the schematic [from jigo2600 Visual Objects]; the clocks are the fits of sections 15 to 17. Dan confirmed on 14 October 2026 that jigo2600's written analyses (its `doc/TIA_*.md` files) may be read as a source of facts only, with no code or text copied (spec, "Where the code comes from").

- **SEC.** An HMOVE write is taken on the first grid clock at least four colour clocks after it lands, a fit to section 16's table and the comb's boundaries [guessing - verify]. The grid clocks are colour clocks 2, 6, ..., 226 (quarter 2 of the horizontal counter's two-phase clock, so they move with RSYNC). SEC loads the ripple counter with 15 if it is at rest, sets all five more-motion latches and sets the HMOVE latch. A count already running is not started again, and its latches are set again [inferring from jigo2600 Visual Objects: "triggering SEC multiple times while the counter is running has no effect"; guessing - verify].
- **Each grid clock**, in this order: every object whose latch is set gets an extra clock; the ripple counter steps down one, and after 0 comes to rest at 15 and stops; SEC, if due; then each latch is cleared where the counter equals its register's nibble XOR `%0111`, the register read as it stands on that clock. So the comparison for a pulse is made on the grid clock before it, the "P - 3" of section 17, and the final comparison on the clock of the fifteenth pulse, its "P + 1". A latch still set at rest sends an extra clock every grid clock until a write makes the nibble `%1000`.
- **The comb.** The HMOVE latch is cleared at each line's start (SHB). While it is set, the blank ends at LRHB (count 18, colour clock 76) instead of RHB, so pixels 0 to 7 are black and the objects miss eight motion clocks. An HMOVE written on cycle 20 is taken at colour clock 66, before RHB acts, and combs; one on cycle 21 is taken at 70 and does not; one on cycle 74 is taken at 226 and is cleared at the line's end, with its pulses in the next line's blank; one on cycle 75 is taken at the next line's colour clock 2 and combs that line.
- **Where an extra clock acts.** On the grid clock itself, one after the slots 1, 5, ..., 225 the suite counts, and before the clock's pixel: the extra clock rises with the colour clock, half a clock before a motion clock, so the object steps before its pixel is latched [from jigo2600 Visual Objects: the extra clock's edge is "in sync with CLK, not CLKP" and "the levels are updated before" the pixel latch]. In the blank it moves the object one pixel left. On the drawn line it merges with that clock's motion clock into one early step: the object does not move, its pixel for that clock shows its next state, and its next pixel shows that state again. That gives section 16's residues for a one-pixel dot (swallowed two clocks after a slot, widened three after), and it puts a pulse on colour clock 18, the clock `hmove-reset-merge`'s RESBL lands on (open item 4) [inferring; guessing - verify].
- **A reset and an extra clock** on one clock count once, because the reset's hold takes one clock a colour clock whatever drives it (section 9.1); extra clocks in its last two count as its motion clocks. From `sta HMOVE` on cycle 3 and RESBL on cycle 6 that lands the ball at pixel 3, as the suite measures.
- **A reset cuts off its object's start at the end of the clock the write lands on**, so that clock's pixel still shows and the next is erased: `resbl-kill`'s "kill clock" for the ball, and for a missile the dot "already drawn" at the reset point in `hmove-values`'s header and picture (section 9.1).
- **Not modelled: the player merge rules.** The suite's `collision/merge-delivery*` tests state when a merged step reaches a player's scan counter as rules on the scan's state: a scan on its start decode takes it, one about to show its first bit refuses it, one counting takes it only at one phase, and at double width only every second bit takes it. The model gives every object the early step, so its players take every merged step. No mechanism in the documents read gives those rules (open item 12), and the four trusted tests fail; task 11 triages them. The same difference shows in the recorded pictures of `hmove-stuck-player`, `-stretch` and `-straddle` as single pixels on grid clocks where the reference keeps a player lit. (Corrected by task 9, 14 October 2026: task 7 put `-widths` in this list. Its pixels are a missile's, not a player's: open item 19.)
- **Not modelled: a missile and the ball differing at width 2.** `hmove-stuck-widths`'s header says that at width 2 a dot whose second clock is a grid clock (residue 1) keeps both pixels on a missile and loses the second on the ball [from the suite: roms/tia-timing/hmove-stuck-widths.asm]. The model gives both one width circuit, as Towers and jigo2600 do, so both lose it: the ball matches the reference and the missile does not, by one pixel every fourth line of that band (open item 19).

## 18. The sound

### 18.1 What the guide says

- Two audio circuits, identical and independent [from SPG 11.0; SPG TIA 1A 11]. Each has three write registers: a 4-bit control `AUDC` (the "noise-tone generator"), a 5-bit frequency divider `AUDF` and a 4-bit volume `AUDV` [from SPG 11.1 to 11.3].
- Addresses: `AUDC0` `$15`, `AUDC1` `$16`, `AUDF0` `$17`, `AUDF1` `$18`, `AUDV0` `$19`, `AUDV1` `$1A` [from SPG write summary]. The summary's bit columns for `AUDC1` (five bits) and `AUDF1` (four bits) disagree with `AUDC0` and `AUDF0` and with the detailed entries, which give `AUDC` bits 3 to 0 and `AUDF` bits 4 to 0 [from SPG AUDC0; SPG AUDF0]. The sheet follows the detailed entries, which 2k6specs agrees with [from 2k6specs]; the two summary rows are a transcription slip.
- Any of the registers may be written at any time [from SPG TIA 1A 11.A to 11.C].
- The circuit is clocked at "approximately 30 KHz" by pulses from the horizontal sync counter [from SPG TIA 1A 11.A].
- `AUDF` divides that clock by its value plus one: `%00000` "no division", `%00001` divide by 2, up to `%11111` divide by 32 [from SPG AUDF0; SPG TIA 1A 11.A: "divided by 1 (code 00000) through 32 (code 11111)"].
- `AUDC` selects "shift counter feedback taps and count lengths" of "a nine bit shift counter" [from SPG TIA 1A 11.B]. The guide's table of the 16 values [from SPG AUDC0]:

| `AUDC` | The guide's words |
|---|---|
| `$0` | set to 1 |
| `$1` | 4 bit poly |
| `$2` | div 15 -> 4 bit poly |
| `$3` | 5 bit poly -> 4 bit poly |
| `$4`, `$5` | div 2 : pure tone |
| `$6` | div 31 : pure tone |
| `$7` | 5 bit poly -> div 2 |
| `$8` | 9 bit poly (white noise) |
| `$9` | 5 bit poly |
| `$A` | div 31 : pure tone |
| `$B` | set last 4 bits to 1 |
| `$C`, `$D` | div 6 : pure tone |
| `$E` | div 93 : pure tone |
| `$F` | 5 bit poly div 6 |

- Volume: writing 0 turns the channel off and higher values are louder, up to 15 [from SPG 11.3; SPG AUDV0]. The shift counter's output pulls the audio pad down through four driver transistors, each twice the size of the one before, one per `AUDV` bit, giving 16 levels [from SPG TIA 1A 11.C].
- On PAL the clock is slower, so the same `AUDF` gives a slightly lower pitch [from SPG, PAL/SECAM conversions, item 2].

### 18.2 When the channels are clocked

- The audio clock is a two-phase clock, `AΦ1` and `AΦ2`, made by the horizontal sync counter [from TIA schematic sheet 1: "AUDIO CLOCKS (Φ1 - Φ2)"]. On the sheet, `AΦ2` comes out of a two-input delay fed by the RHS and CNT decodes [from TIA schematic sheet 1]. jigo2600's reading of the same sheet gives `AΦ1 = D2(SHS, LRHB)` and `AΦ2 = D2(RHS, CNT)`, so each phase fires twice a line [from jigo2600 Visual Objects, its signal table].
- The decodes sit at horizontal counts 4 (SHS), 8 (RHS), 18 (LRHB) and 36 (CNT), that is colour clocks 16, 32, 72 and 144 from the start of the line [from Towers, the HSync counter table]. A `D2` delays by one count, four colour clocks [inferring from Towers: "delayed 4 CLK" for the decoded lines; jigo2600 Visual Objects]. So `AΦ1` is high at colour clocks 20 to 23 and 76 to 79, and `AΦ2` at 36 to 39 and 148 to 151 [from jigo2600 Visual Objects, its table; agrees with the inference].
- The counters and the output change on `AΦ2` [from jigo2600 Sound: dual-phase logic takes its new value when phase 2 is high]. **The model steps each channel at colour clocks 36 and 148 of every line.** The two steps are 112 and 116 colour clocks apart, not evenly spaced [inferring from the clocks above; jigo2600 Sound also says the TIA's samples "are not exactly evenly spaced"].
- LRHB is a decode of the counter, and the HMOVE latch only chooses whether it ends the blank [from jigo2600 Visual Objects]. So the audio clock does not move with HMOVE [inferring]. RSYNC resets the horizontal counter [from Towers], so it shifts the audio clocks with the line [inferring].
- Dan confirmed on 14 October 2026 that jigo2600's written analyses (its `doc/TIA_*.md` files) may be read as a source of facts only, with no code or text copied (spec, "Where the code comes from"); that covers Visual Objects here.
- Rate: two steps per 228 colour clocks, the colour clock divided by 114: 31399.5 Hz on NTSC (3.579545 MHz / 114) and 31113.1 Hz on PAL (3.546894 MHz / 114) [from 2k6specs, "AUDF"]. Stolberg gives 31440 Hz and 31200 Hz, computed from 262 lines at 60 Hz and 312 at 50 Hz [from Stolberg freqform]; those are the nominal frame rates, not the clock, and the sheet follows 2k6specs.

### 18.3 The divider

- A 5-bit counter advances by one, modulo 32, on each audio step. When it equals `AUDF` it is reset to 0 and one divided pulse is made. So the divided clock fires every `AUDF + 1` audio steps [from jigo2600 Sound].
- When a write makes `AUDF` smaller than the counter's present value, the counter runs on through 31 and wraps before it matches [inferring from jigo2600 Sound: "advances by one unit (modulo 32)" and is "compared to the value of AUDFx"].
- The divider's value at power on and the exact audio step on which the first divided pulse comes after a write are not documented [guessing - verify]. The model starts the counter at 0.

### 18.4 The noise-tone generator

The structure, read from the schematic: a block labelled "AUDIO NOISE GEN." holds five dual-phase delay stages (`D1`) in a chain, and below it four latch stages labelled Q6 to Q9; the four `AUDC` bits feed the logic that controls both [from TIA schematic sheet 4]. This is the guide's "nine bit shift counter" as two parts, five bits and four bits [from SPG TIA 1A 11.B; jigo2600 Sound; Galloway 2003 saw the same five-bit section "with taps on bits 2 and 4"].

The logic below is jigo2600's reading of that sheet, restated [from jigo2600 Sound]. Name the nine bits `C0` to `C8`. `C0` to `C4` are the 5-bit counter ("poly5"), `C0` the newest; `C5` to `C8` are the 4-bit counter ("poly4"), `C5` the newest. The channel's output bit is `C8`. Write `A` for the 4-bit `AUDC`, `A[1:0]` for its low two bits and `A[3:2]` for its high two.

On each divided pulse:

1. **Poly5 shifts.** `C1` to `C4` take the old `C0` to `C3`. The new `C0` is:
   - 1 if `A` = 0 ("flood with 1");
   - otherwise, if `A[1:0]` = 0: `C8 XOR C4`, or 1 if all nine bits are 0 (the two counters chained into one 9-bit counter);
   - otherwise: `C2 XOR C4`, or 1 if `C0` to `C4` are all 0 (a 5-bit counter on its own).
2. **Poly4 is clocked or not.** It shifts only when its clock condition holds:
   - `A[1:0]` = 0 or 1: always;
   - `A[1:0]` = 2: when `C0` to `C4`, read as a number with `C0` the least significant bit, equal 8 or 24 (twice in each 31 steps of poly5);
   - `A[1:0]` = 3: when `C4` is 1.
3. **When poly4 shifts**, `C6` to `C8` take the old `C5` to `C7`, and the new `C5` is:
   - 1 if `A` = 0;
   - `A[3:2]` = 0: `C7 XOR C8`, or 1 if `C5` to `C8` are all 0 (a 4-bit counter);
   - `A[3:2]` = 1: NOT `C5` (a divide by 2);
   - `A[3:2]` = 2: `C4` (poly4 becomes a delay of poly5's output; with `A[1:0]` = 0 this is the 9-bit chain);
   - `A[3:2]` = 3: NOT `C7`, or 1 if `C5 C6 C7` = `%101` read with `C5` least significant (a 3-stage counter of period 6, three 1s then three 0s, which leaves its two unused states within two steps).

- Every new bit is computed from the state before the pulse, and all nine bits change together [inferring from jigo2600 Sound: the dual-phase stages and the flip-flops are both clocked by the divided clock; the exact order inside the pulse is a model choice, guessing - verify].
- The same rules agree with 2k6specs's description of the counters [from 2k6specs, "Internal Poly Formulas": 4-bit `z = c xor d`, 5-bit `z = c xor e`, 9-bit `z = e xor i`, shifting right, output the rightmost bit].
- **The state at power on is not documented** [guessing - verify]. `AUDC` = 0 floods all nine bits with 1 within nine divided pulses [inferring from the rules above]. The model powers on with all nine bits 1, the state a program reaches by holding `AUDC` at 0, and every worked example starts there.

### 18.5 Each `AUDC` value, worked

Derived by stepping the rules of 18.4 from all nine bits 1, with `AUDF` = 0 so that each divided pulse is one audio step. "First 64" is `C8` after each of the first 64 pulses. Period and the count of 1s are over one period once the start-up has passed [inferring from jigo2600 Sound, by stepping the rules; `python3 -I tools/probes/atari-2600/tia_sound.py` prints this table, 14 October 2026].

| `AUDC` | Counters used | Period (pulses) | 1s per period | First 64 output bits |
|---|---|---|---|---|
| `$0` | none, all 1 | 1 | 1 | `1111111111111111111111111111111111111111111111111111111111111111` |
| `$1` | poly4 | 15 | 8 | `1110001001101011110001001101011110001001101011110001001101011110` |
| `$2` | poly4 clocked twice per 31 | 465 | 248 | `1111111111111111111111111111111111111111111111100000000000000000` |
| `$3` | poly4 clocked by poly5's output | 465 | 248 | `1110000001100111100000111001111110000001000111001111100011111110` |
| `$4` | divide by 2 | 2 | 1 | `1110101010101010101010101010101010101010101010101010101010101010` |
| `$5` | divide by 2 | 2 | 1 | `1110101010101010101010101010101010101010101010101010101010101010` |
| `$6` | divide by 2 clocked twice per 31 | 31 | 13 | `1111111111111111111111111111111111111111111111100000000000000000` |
| `$7` | divide by 2 clocked by poly5's output | 31 | 15 | `1110111101101001100000111001000101011110110100110000011100100010` |
| `$8` | poly9 (poly5 and poly4 chained) | 511 | 256 | `1111111100000111101111100010111001100100000100101001110110100011` |
| `$9` | poly5, through poly4 as a delay | 31 | 16 | `1111111100011011101010000100101100111110001101110101000010010110` |
| `$A` | poly5's output sampled twice per 31 | 31 | 18 | `1111111111111111111111111111111111111111111111111111111111111111` |
| `$B` | poly4 fills with 1 | 1 | 1 | `1111111111111111111111111111111111111111111111111111111111111111` |
| `$C` | divide by 6 | 6 | 3 | `1110001110001110001110001110001110001110001110001110001110001110` |
| `$D` | divide by 6 | 6 | 3 | `1110001110001110001110001110001110001110001110001110001110001110` |
| `$E` | divide by 6 clocked twice per 31 | 93 | 44 | `1111111111111111111111111111111111111111111111100000000000000000` |
| `$F` | divide by 6 clocked by poly5's output | 93 | 46 | `1110000001111000000000111111000001111110000111110000000000111110` |

- Every period matches the guide's names: 15 and 31 for the 4-bit and 5-bit counters, 2, 6, 31 and 93 for the "div" tones, 511 for the 9-bit counter, and 465 (15 times 31) for `$2` and `$3` [inferring: the table against SPG AUDC0]. 2k6specs lists the same repeat lengths for all 16 values [from 2k6specs, "AUDC Pattern Shapes"].
- **Cross-check.** For every value 2k6specs draws (`$1`, `$2`, `$3`, `$6` to `$A`, `$C`, `$E`, `$F`), its 70-step drawing is a stretch of the model's repeating sequence, or of its inverse for `$6`, `$7`, `$E` and `$F` [inferring: `python3 -I tools/probes/atari-2600/tia_sound.py 2k6specs.txt` compares each, 14 October 2026; 2k6specs, "AUDC Pattern Shapes"]. The inverse appears only where the output is a toggle, whose polarity depends on the starting state. Stolberg's sequences for `$1`, `$6` and `$9` are also a stretch of the model's or its inverse [from Stolberg freqform; compared by eye, unreproduced: no script makes this comparison]. Stolberg's `$E` (each bit of the 13-and-18 `$6` pattern held for three steps, 39 and 54) differs from the model's 44 and 49; Stolberg says his tables were calculated from Ron Fries's TIASOUND emulation, and 2k6specs's drawing of `$E` (49 one way) agrees with the model, so the sheet follows the model [from Stolberg freqform; 2k6specs].
- Values that sound the same are the same here: `$4` and `$5`, `$C` and `$D`, `$0` and `$B` give identical output; `$7` sounds as `$9` [from 2k6specs, "AUDC Pattern Type Notes"; agrees with the table up to inversion].
- `$6` and `$A` are both a 31-step tone made of a run of 13 and a run of 18 [from 2k6specs: "18 HIGH and 13 LOW cycles"; Stolberg freqform: 13 then 18]. On SECAM, `AUDC` = 0 is reported to give a background noise; PAL and NTSC are silent [from SPG, PAL/SECAM conversions, SECAM item 2; 2k6specs]. SECAM is not modelled.

### 18.6 The divider, worked

- `AUDC` = `$4`, `AUDF` = 3: each output bit lasts 4 audio steps, so a full cycle of the square wave is 8 steps: 31399.5 / 8 = 3924.9 Hz on NTSC and 31113.1 / 8 = 3889.1 Hz on PAL [inferring from 18.2 and 18.3].
- `AUDC` = `$4`, `AUDF` = 0: 15699.8 Hz on NTSC, the highest tone [inferring; Stolberg freqform gives "15720 Hz" from its rounded clock].
- `AUDC` = `$1`, `AUDF` = 0: 31399.5 / 15 = 2093.3 Hz on NTSC; Stolberg's table gives 2096 Hz and 2080 Hz on PAL from his rounded clocks; the model gives 2074.2 Hz on PAL [inferring; from Stolberg freqform].
- Counter at 20, then `AUDF` written as 5: the next divided pulse comes 17 audio steps later (21 to 31, then 0 to 5) [inferring from 18.3; guessing - verify].

### 18.7 Volume and the mix

- Each channel's level is its output bit times its volume: 0 when `AUDV` = 0 or the bit is 0, `AUDV` when the bit is 1 [inferring from SPG TIA 1A 11.C: the bit gates four binary-weighted drivers enabled by the `AUDV` bits]. Which polarity of the bit pulls the pad down is not settled from the sheet as read; it does not change what is heard. The model takes bit 1 as "on", which makes `AUDC` = 0 a steady level of `AUDV`, the property the "4-bit samples by changing the volume" trick relies on [from Stolberg freqform, distortion 0 and 11].
- The schematic labels the four drivers `10/10 30K`, `20/10 15K`, `40/10 7.5K` and `80/10 3.75K` (width over length and an on-resistance), one per `AUDV` bit from D0 to D3 [from TIA schematic sheet 4]. So a channel's pull-down conductance is `AUDV` / 30 kΩ while its bit is on [inferring].
- Each channel has its own pad, `AUD0` on pin 13 and `AUD1` on pin 12 ("2 PADS") [from TIA schematic sheet 4; SPG TIA 1A 11: outputs "could be combined externally"]. On NTSC boards the two pins are joined [from 2k6specs, "Composite Video Mod": "TIA.Pin13+12"; FSM, the audio flowchart treats "pin 12/13 of A201" as one line]. The PAL TIA joins them inside the chip on one pin [from 2k6specs, "Chipset Pin-Outs": "AUD is audio AUD0+AUD1 merged on one pin"]. Either way the program cannot see it [inferring].
- **The mix.** Two current sinks on one node add their conductances, so the mixed output depends only on `s = AUDV0 x bit0 + AUDV1 x bit1`, 0 to 30 [inferring from the two bullets above]. The pull-up the pads work against, and so the curve from `s` to voltage, was not found [guessing - verify]. The model makes the output linear in `s`, `s / 30`.
- Worked: `AUDV0` = 15 with bit 1 and `AUDV1` = 0: `s` = 15, output 0.5. Both 15 with both bits 1: 30, output 1.0. `AUDV0` = 6 with bit 1 and `AUDV1` = 9 with bit 0: `s` = 6, output 0.2. Volume 0 gives 0 whatever `AUDC` is [from SPG 11.3: "turns sound off completely"].

## 19. The colour signal and the palette

### 19.1 The colour byte

- Four colour-luminance registers: `COLUP0` `$06` (player 0 and missile 0), `COLUP1` `$07` (player 1 and missile 1), `COLUPF` `$08` (playfield and ball), `COLUBK` `$09` (background) [from SPG 4.0; SPG write summary].
- Bits 7 to 4 choose one of 16 colours (the hue) and bits 3 to 1 one of 8 luminances; bit 0 is not used [from SPG COLUP0, its table of D7 to D1; SPG write summary marks bits 7 to 1; 2k6specs, "Video Colors"; the suite: roms/tia-render/colors.asm: bit 0 "is not wired to the colour decode"].
- Worked: `$45` and `$44` are the same colour; so are `$0F` and `$0E` [from the suite: roms/tia-render/colors.asm].
- Write `H` for the hue (bits 7 to 4) and `L` for the luminance (bits 3 to 1, 0 to 7).
- During the blanks the TIA puts out no colour register: the border is black [from 2k6specs, "Screen Border Color"; jigo2600 Composite: with no register selected the colour code is 0].
- The colour or black and white switch changes nothing in the TIA; a program reads it and chooses its own colours [from 2k6specs, "Software Switches/Buttons"].

### 19.2 What the TIA puts out (NTSC, the TIA 1A)

- Three luminance pins carry `L` as binary, straight from the colour register; the board turns them into a voltage with resistors [from SPG TIA 1A 9.B: "Three of these lines are binary coded luminosity and go directly to chip output pads"; Ball 2003: "a simple resistor D to A converter"].
- One colour pin, `COL`. During horizontal blank it gives a reference, the colour burst; while the line is drawn it gives the colour carrier shifted in phase from the burst. Hue 0 gives no colour. Hue 1 is "gold (same phase as color burst)". Hues 2 to 15 "shift the phase from zero through almost 360 degrees", 15 colours in all [from SPG TIA 1A 10].
- The burst runs for colour clocks 36 to 51, 16 clocks after HSYNC ends, and not during VSYNC [from jigo2600 Composite; Towers: RCB at colour clock 48 plus its 4-clock delay].
- The phase comes from the colour clock passed through a chain of fourteen delay stages; hue `H` takes the output after `H - 1` stages, and the burst uses the undelayed clock, as hue 1 does [from jigo2600 Composite]. Ball describes the same, "delay gates selected via a multiplexer (with an output disable for COL=0)" [from Ball 2003, 11 December].
- The delay of the stages is set by a potentiometer on the TIA's delay pin [from jigo2600 Composite; 2k6specs, "Chipset Pin-Outs": pin 10, "CADJ (+3.4V)"]. The service manual sets it with the diagnostic cartridge's colour bar test: "R211 (R213 on the 2600A board) should be adjusted so the bars immediately above and below the reference line are within one shade of each other" [from FSM, Color Bar Test, page 3-9]. The manual's figure is black and white, so it does not say which hues those bars are.
- The colour signal and the burst come from the same pin, so their amplitudes are equal [from Ball 2003, 5 December]. Saturation cannot be set; a colour is either on at that amplitude or off [from jigo2600 Composite].

### 19.3 How big the phase step is: documented, measured and chosen

| Value | Source | Kind |
|---|---|---|
| "zero through almost 360 degrees" over hues 2 to 15 | [from SPG TIA 1A 10] | Atari's documentation |
| 24 degrees a step, 336 degrees at hue 15 | [from jigo2600 Composite: "should result in a further delay of 24 degrees"; Ball 2003] | a stated design value; neither names a measurement |
| 360/14.5 = 24.8, 360/14 = 25.7 or 360/13.5 = 26.7 degrees | [from Jentzsch 2008] | three candidate palettes, judged by eye |
| about 23 degrees at power on, 25 to 27 once warm, up to about 28 | [from AtariAge 216590, Trebor] | one owner's observation of consoles warming up |
| hue 15 matched to hue 1, so 25.7 degrees | [from AtariAge 216590, Trebor: test carts and documentation aim "to match Hue 1x and Hue 15x"] | a service practice as reported; the FSM's figure cannot confirm which bars |

**Model choice: 360/14 degrees a step (about 25.71), so hue 15 lands back on hue 1** [guessing - verify]. Chosen because the delay is adjustable and the service manual adjusts it until two bars match, which a forum report names as hues 1 and 15; 24 degrees is the design figure but not a fixed property of a console. The consequence is that on NTSC `$1x` and `$Fx` are the same colour, so the palette has 120 different colours, not 128. Choosing 24 degrees instead gives 128; the step is one constant.

### 19.4 The NTSC palette, the formula

The model follows Ball's published conversion [from Ball 2003, 5, 6 and 9 December], with the step of 19.3 and one scale factor:

- Luminance: `Y' = L / 7`. Ball derives `Y' = 0.09 L` from the CX2600's resistors (10K on SYNC, 12K on LM2) and 92.5 IRE from black to white, so `L` = 7 gives 0.63 [from Ball 2003, 6 and 9 December]. A television's contrast control scales the whole signal [guessing - verify]; the model scales luminance and colour by the same factor, 1/0.63, so `$0E` is white. This scaling is a model choice [guessing - verify]. Ball notes the 2600A has no BLK connection and different resistors, so its darks are darker [from Ball 2003, 6 December]; the model is the CX2600.
- Hue angle on the U (B - Y) and V (R - Y) plane, for `H` from 1 to 15: `θ = 180° - (H - 1) x 25.714°`. Hue 1 at 180 degrees is the NTSC burst's own phase [from Ball 2003, 5 December: `hue = 180 degrees - (COLU / 16 - 1) * 24 degrees`; SPG TIA 1A 10: hue 1 has the burst's phase]. A delay subtracts from the angle [from Ball 2003, the same formula].
- Saturation: `S = (20 / 92.5) / 0.63 = 0.3432` for `H` from 1 to 15, and 0 for `H` = 0. Ball's `20 / 92.5` is a 20 IRE carrier, the burst's amplitude, over 92.5 IRE from black to white [from Ball 2003, 6 and 9 December]; the division by 0.63 is the model's scale above.
- `U = S cos θ`, `V = S sin θ`.
- To RGB: `R' = Y' + 1.140 V`, `G' = Y' - 0.3947 U - 0.5808 V`, `B' = Y' + 2.033 U`, each clipped to 0 to 1, then times 255 and rounded [from Ball 2003, 5 and 9 December: the inverse of `Y' = 0.299 R' + 0.587 G' + 0.114 B'`, `U' = 0.492 (B' - Y')`, `V' = 0.877 (R' - Y')`; clipping "the same way the TV would"]. The values are used as they are, with no gamma step [guessing - verify; Ball 2003 left gamma open].
- The colours a television shows depend on its tint, colour and contrast controls and on the console's delay setting [from Ball 2003, 11 December; Stolberg in the same thread, 9 December, on hue 15: "depends mostly on the setting of the tint control"]. This goes in `docs/known-differences.md`.

### 19.5 PAL

What is documented:

- The PAL palette has 104 colours: hues 0, 1, 14 and 15 are all grey ("Same as color 0"), and hues 2 to 13 are colours [from 2k6specs, "Color & Luminance Values"]. A real PAL console shows hue 1 as grey, hue 5 as green, hue 6 as red, hue 2 as yellow, hue 7 as cyan, and `$C8` as violet [from the suite: include/region.h, "Hardware-confirmed on a real PAL console, 2026-07-16"].
- 2k6specs names the PAL hues: 2 yellow, 3 green-yellow, 4 orange, 5 green, 6 pink-orange, 7 green-blue, 8 pink, 9 turquoise, A pink-blue, B light blue, C blue-red, D dark blue [from 2k6specs]. The even hues walk one way round the colour wheel from yellow through red to blue-red, and the odd hues walk the other way from green-yellow through green and cyan to dark blue [inferring from that list].
- The PAL TIA has extra pins, `PALS` and `PALI`, and a delay pin; 2k6specs's 5 V modification re-adjusts the PAL colour potentiometer to about 3.38 V [from 2k6specs, "Chipset Pin-Outs" and "Using +5V DC Power Supply"]. How the PAL TIA makes its carrier and its line-by-line V switch was not found in any document [guessing - verify]. Jentzsch reports from experiments on his PAL console that the potentiometer has a usable range of about 30 degrees, that the colours jump to grey outside it, and that a "shift" of 30 degrees "seems a good default" in his model [from AtariAge 216590, Jentzsch, May 2020]. His posted pseudo-code is not self-consistent (it takes a sine for both U and V), so it is not used.
- Luminance uses the same three bits [from 2k6specs, "Video Colors"].

**The PAL model, a choice** [guessing - verify]: the twelve colour hues sit evenly, 30 degrees apart, the even hues on the V-positive half of the plane and each odd hue the mirror of the even hue before it:

- even `H` (2, 4, ... 12): `θ = 165° - ((H - 2) / 2) x 30°`, so 2 is at 165, 4 at 135, 6 at 105, 8 at 75, 10 at 45, 12 at 15;
- odd `H` (3, 5, ... 13): `θ = -(165° - ((H - 3) / 2) x 30°)`, so 3 at 195, 5 at 225, 7 at 255, 9 at 285, 11 at 315, 13 at 345;
- `H` = 0, 1, 14, 15: `S` = 0.

Chosen because it reproduces every documented name and every hardware-confirmed colour above (19.6 shows the confirmed ones), gives 12 times 8 colours plus 8 greys, 104, and its 30-degree spacing is the step Jentzsch found on a real console. `Y'`, `S`, the YUV to RGB step and the clipping are the NTSC ones; PAL is natively YUV [from 2k6specs and AtariAge 216590 as above; the YUV formulas from Ball 2003].

### 19.6 Worked examples

Computed from 19.4 and 19.5 by `python3 -I tools/probes/atari-2600/tia_palette.py` on 14 October 2026 [inferring; the model is a choice as marked].

| Region | Colour | `Y'` | `U` | `V` | RGB | What the sources call it |
|---|---|---|---|---|---|---|
| NTSC | `$00` | 0 | 0 | 0 | `#000000` | black [from SPG COLUP0] |
| NTSC | `$0E` | 1 | 0 | 0 | `#FFFFFF` | white [from SPG COLUP0] |
| NTSC | `$1E` | 1 | -0.3432 | 0 | `#FFFF4D` | gold, NTSC yellow [from SPG TIA 1A 10; the suite: include/region.h, `COL_OBSERVED`] |
| NTSC | `$44` | 0.2857 | -0.0764 | +0.3346 | `#AA1F21` | red [from the suite: include/region.h, `COL_FAIL`] |
| NTSC | `$C8` | 0.5714 | -0.0764 | -0.3346 | `#30CB6A` | green [from the suite: include/region.h, `COL_PASS`] |
| NTSC | `$F8` | 0.5714 | -0.3432 | 0 | `#92B400` | the same as `$18` under the model |
| PAL | `$1E` | 1 | 0 | 0 | `#FFFFFF` | grey [from 2k6specs; the suite: include/region.h] |
| PAL | `$2E` | 1 | -0.3315 | +0.0888 | `#FFFF53` | yellow [from the suite: include/region.h] |
| PAL | `$58` | 0.5714 | -0.2427 | -0.2427 | `#4BCE14` | green [from the suite: include/region.h, `COL_PASS`] |
| PAL | `$64` | 0.2857 | -0.0888 | +0.3315 | `#A9211B` | red [from the suite: include/region.h, `COL_FAIL`] |
| PAL | `$7E` | 1 | -0.0888 | -0.3315 | `#9FFFD1` | cyan [from the suite: include/region.h] |
| PAL | `$C8` | 0.5714 | +0.3315 | +0.0888 | `#AC63FF` | violet [from the suite: include/region.h] |
| PAL | `$E8` | 0.5714 | 0 | 0 | `#929292` | grey [from 2k6specs] |

What tests can take from this, in both regions: hue 0 is grey (`R = G = B`) at every `L`; every RGB component is non-decreasing as `L` rises within a hue, and their sum strictly rises (`tia_palette.py` checks these for all 16 hues in both regions, and counts the colours, 14 October 2026); `$xy` and `$xy + 1` are the same colour; on PAL, hues 0, 1, 14 and 15 are grey at every `L`; NTSC and PAL differ. The model gives 120 colours on NTSC and 104 on PAL.

## 20. The inputs

### 20.1 The six ports

- Six input ports, read on D7 at `INPT0` to `INPT5`, read registers `$08` to `$0D` [from SPG 12.0; SPG read summary]. A read drives D7 only; D6 to D0 are not driven and keep what was last on the bus [from SPG read summary, only bit 7 marked; the suite: roms/cpu/partial-drive.asm: "an input read presents its one real bit over the low seven"]. The floating bus itself is in `bus.md` section 4.
- `INPT0` to `INPT3` are "dumped" and `INPT4` and `INPT5` are "latched" [from SPG 12.0; SPG TIA 1A 8.A].
- Both kinds are controlled by `VBLANK` (`$01`): D7 dumps `INPT0` to `INPT3` to ground, D6 enables the `INPT4` and `INPT5` latches, D1 is the vertical blank [from SPG VBLANK]. The guide's table says the dump grounds "INPT1,2,3,6"; its read summary note says "I0, I2, I2, I3 can be grounded", and its hardware section says the four ports I0 to I3 [from SPG VBLANK; SPG read summary; SPG TIA 1A 8.B]. The sheet follows the hardware section: `INPT0` to `INPT3`; the other two are typing slips.
- One `VBLANK` write sets all three bits. So a program that writes `$02` to start the blank also turns the dump off and disables the latches [inferring from SPG VBLANK].
- Which pin is which, on the two controller ports: pin 5 is `INPT0` (left port) and `INPT2` (right), pin 9 is `INPT1` and `INPT3`, pin 6 is `INPT4` and `INPT5` [from 2k6specs, "Joystick Ports"]. The joystick's fire button is on pin 6 and pulls it to ground when pressed: `INPT4` D7 is the left joystick's button and `INPT5` the right's, 0 pressed and 1 not [from 2k6specs, "Controllers: Joysticks"; the suite: roms/cpu/partial-drive.asm: "the unpressed fire button is held high by its pull-up"].

### 20.2 `INPT4` and `INPT5`, the latch

- **Latch disabled** (`VBLANK` D6 = 0): the latches are "removed from the circuit completely" and the port reads the pin as it is now [from SPG TIA 1A 8.C; SPG 12.2].
- **Latch enabled** (D6 = 1): the port reads the latch. The latch stays 1 while the pin stays 1; the pin going 0 clears it to 0, and it stays 0 "even after the port returns positive" until the latch is disabled [from SPG TIA 1A 8.C; SPG 12.2].
- **Disabling resets the latch to 1**: "Disable latches (D6 = 0) also resets latches to logic true" [from SPG VBLANK; 2k6specs, "VBLANK"].
- jigo2600 reads the circuit as an SR latch whose set input is the inverted enable bit and whose reset input is the inverted pin [from jigo2600 Ports]. So if the button is already held when the latch is enabled, the latch is cleared at once and reads 0 [inferring from that circuit and SPG TIA 1A 8.C, "remain positive as long as the input ports remain positive"].
- Both latches are enabled and disabled together [from SPG TIA 1A 8.C].
- When a press or a `VBLANK` write shows in a read, to the colour clock, is not documented [guessing - verify]. The model makes both take effect on the next read.
- At power on the latch's state is not documented [guessing - verify]; the model starts with D6 = 0, so the ports read the pins.

Worked, left joystick, reading `INPT4` with `LDA $0C` (so the low seven bits are the operand, `$0C`, per 20.1):

| Step | `VBLANK` D6 | Button | `INPT4` reads |
|---|---|---|---|
| 1 | 0 | released | `$8C` |
| 2 | 0 | held | `$0C` |
| 3 | 0 | released | `$8C` |
| 4 | write `$40`: 1 | released | `$8C` |
| 5 | 1 | held | `$0C` |
| 6 | 1 | released | `$0C`, latched |
| 7 | write `$00`: 0 | released | `$8C`, reset |
| 8 | 0 | held | `$0C` |
| 9 | write `$40`: 1 | still held | `$0C` [inferring, as above] |

[from SPG TIA 1A 8.C; SPG VBLANK; the suite: roms/cpu/partial-drive.asm for the bus bits: `lda $3C` with the button open reads `$BC`, `lda $1C` reads `$9C`].

### 20.3 `INPT0` to `INPT3`, the dumped ports

- Each port has a large transistor that grounds it while `VBLANK` D7 = 1. While D7 = 0 the ports are "general purpose high impedance input ports" [from SPG TIA 1A 8.B]. A port reads 1 once its voltage passes the input threshold [from SPG 12.1: "when the capacitor is charged the input port goes HI"; jigo2600 Ports: a Schmitt trigger].
- On the board each port has a 68 nF capacitor to ground and a 1.8 kΩ resistor in series [from 2k6specs, "Controllers: Paddles"; FSM, CX2600 parts list: "Cap. Epoxy Dipped .068uF", locators C215 to C218, and the paddle lines' capacitors C215 to C218 in the "Paddle Lines Test" chart]. A paddle is a 1 MΩ potentiometer from +5 V (pin 7) to the port; it charges the capacitor, and the time until the port reads 1 measures the knob [from FSM, "Paddle Controller"; 2k6specs: 380 lines at one end, 190 at the centre, 0 at the other].
- **Nothing in the console pulls these pins up.** The keyboard controller needs "external 4.7K Ohm pullups" on pins 5 and 9, and the three-button joystick adapter's extra buttons, which connect those pins to +5 V, read 0 when not pressed after a dump [from 2k6specs, "Controllers: Keypad" and "3-Button Joystick Adapter"]. 2k6specs advises a timeout "to detect the presence of paddles" [from 2k6specs]. So with no paddle, a dumped capacitor never charges and the port reads 0 [inferring from those three].
- The suite checks the dumped case: with `VBLANK` = `$82`, `lda $18` (an `INPT0` mirror) reads `$18`, D7 = 0 [from the suite: roms/cpu/partial-drive.asm, code `$02`]. No suite test reads `INPT0` to `INPT3` with the dump off (searched for `INPT` and `VBLANK` under `roms/` on 14 October 2026).

**What "a paddle at rest" must mean for the model.** The machine has joysticks in both ports and no paddles (spec, "Controllers"). A joystick leaves pins 5 and 9 unconnected [from 2k6specs, "Joystick Ports": no joystick function on pins 5 and 9]. So the model's `INPT0` to `INPT3` are the ports with nothing connected:

| `VBLANK` D7 | Before any dump since power on | After a dump |
|---|---|---|
| 1 (dumped) | D7 = 0 [from SPG TIA 1A 8.B] | D7 = 0 [from SPG TIA 1A 8.B; the suite: roms/cpu/partial-drive.asm] |
| 0 | D7 = 0 [guessing - verify: the capacitor is assumed discharged at power on] | D7 = 0, for ever [inferring from 2k6specs as above] |

A real port left floating may drift, through leakage, to 1 after a long time; no document gives a figure [guessing - verify]. The model reads 0 always. A connected paddle at rest would instead read 1 a fixed time after each dump ends; that is the paddles' issue, #108, and is not modelled.

### 20.4 The inputs in the model, as task 9 built it

The TIA keeps the two fire pins and the two latches. A pin change (from `Controllers`) and a VBLANK write are the only events that move a latch, and both happen between the CPU's bus accesses, so the latches are worked out there, not every colour clock: D6 cleared sets both latches to 1; D6 set, or a pin change while it is set, clears a latch whose pin is low. A read of INPT4 or INPT5 gives the latch while D6 is set and the pin otherwise. INPT0 to INPT3 read 0 always. The pins are the outside world, so power on leaves them; the latches power on disabled and at 1 (open item 13). `TiaInputTests` holds the worked example of section 20.2 step by step.

### 20.5 Not modelled

The keyboard controller reads its columns on `INPT0`, `INPT1` and `INPT4` (left port) [from SPG, PIA 5.5; 2k6specs], the paddles' pots on `INPT0` to `INPT3` [from SPG 12.1], and the three-button adapter's extra buttons on `INPT0` to `INPT3` [from 2k6specs]. None is modelled (spec, "Controllers"; issues #108 and #124).

## Open items

1. **RSYNC's exact timing**: how many clocks after the write the counter restarts, the length of the shortened line, and whether its blank releases a pending WSYNC. Towers says it needs more investigation; no other source gives clocks. Task 5 chose three clocks after W and a release (section 6).
2. **The HMOVE latch's set and clear clocks.** Only the boundaries are known: cycle 20 combs and 21 does not; 74 does not and 75 combs the next line.
3. **The first pulse at the first slot at or after W + 7**, and the comparator read at P - 3 and P + 1, are fits to Mott's table and two suite tests, not read from the schematic. The TIA schematics were not opened. Task 7's model takes HMOVE on the first grid clock four or more clocks after the write (section 17.1), which gives every cell of section 16's table.
4. **The pulse grid's residue.** The suite gives 225 as the last slot (1 modulo 4) and also says the RESBL at W = 18 in `hmove-reset-merge` shares a clock with a pulse; one of the two is off by a clock in this sheet's numbering. Task 7: both hold if an extra clock acts on the clock after its slot, before that clock's pixel, as jigo2600 reads the schematic (section 17.1) [inferring].
5. **Path delays from reference pictures only**: VBLANK and GRP one clock after W, and the playfield read 2 clocks before a cell. The case W = P - 2 for a playfield cell is not covered.
6. **RESMP at 1x**: +4 (suite) against +3 (2k6specs), and on which line the released missile first shows.
7. **A player reset on cycle 23** lands at 7 where the two-arm rule gives 6; the suite measured it but its mechanism is not stated. Task 7: `merge-delivery-replant`'s comments say again "write ends clock 69 -> column 7 ... measured", for its body probe, but the test fails for its merge rules first (open item 12), so it does not decide the mechanism. The model still lands at 6.
8. **Score mode's priority and the ball's colour** in score mode: 2k6specs only.
9. **CXBLPF D6**, and writes to `$2D` to `$3F`.
10. **The position counters under VBLANK** are taken to run; no source says so directly.
11. **Power-on state** of the counters, latches and registers: no source opened gives it, except that a counter can power up in `111111` and is reset by its decode (Towers).
12. **The merge rules for players** under drawn-line pulses (`collision/merge-delivery*.asm`, `tia-timing/hmove-stuck-straddle.asm`) are stated only by the suite, from one console. Task 7 found no mechanism for them (section 17.1); the four trusted `merge-delivery` tests fail and wait for task 11.
13. The power-on state of the nine audio bits, the divider count and the input latch [guessing - verify]. The model's choices are all ones, 0, and disabled.
14. The order of the audio updates inside a divided pulse, and the bit numbering behind "states 8 and 24" in 18.4 [guessing - verify; from jigo2600 Sound]. Neither changes a period; both change the phase of the first 64 bits for `AUDC` `$2`, `$6`, `$A` and `$E`.
15. The voltage curve of the mix [guessing - verify]; the model is linear in `s`.
16. The NTSC phase step (25.7 degrees chosen, 24 the design figure) and the contrast scale [guessing - verify].
17. The PAL TIA's colour mechanism; the PAL hue angles are fitted to names and to the suite's confirmed colours [guessing - verify].
18. The region decision holds for these sections: NTSC and PAL differ here only in the clock (the audio rate) and the colour signal; the PAL chip's joined audio pads are invisible to the program [inferring from 18.2, 18.7, 19.5 and section 20].
19. **A missile at width 2 under a merged step.** The suite says a missile keeps the second pixel of a width-2 dot whose second clock is a grid clock and the ball loses it; every source opened gives the two one width circuit (Towers: "Ball/Missile width enclockifier"; jigo2600 Visual Objects: the same expression for both). The TIA schematic's sheet 3 was looked at for the missile's width-2 gate and its source node was not traced with confidence; the ball's was not traced. No mechanism found; task 9's triage is in the task 7 journal entry [guessing - verify].

## Sources opened

All read on 14 October 2026.

| Short name | Document |
|---|---|
| SPG | Steve Wright, *Stella Programmer's Guide*, Atari, 3 December 1979; HTML reconstruction by Charles Sinnett (1993) and B. Watson (2001) |
| Towers | Andrew Towers, *Atari 2600 TIA Hardware Notes*, v1.0, 6 March 2003 |
| the suite | `dbhq-uk/missingno-vcs-tests` at `891c079976a2f68033ca4af2fe2fd85aaf1906c9`: every header in `roms/tia-timing/`, `roms/tia-render/` and `roms/collision/`, the headers of `roms/cpu/floating-bus.asm`, `open-bus.asm`, `partial-drive.asm`, `stack-aliases-tia.asm`, `rmw-strobe.asm` and `rmw-wsync.asm`, `README.md`, `include/region.h`, the kernels of `tia-timing/midline-color.asm`, `midline-pf.asm`, `midline-vblank.asm`, `midline-grp.asm`, `midline-resp.asm`, `rsync.asm`, `hmove-reset-merge.asm` and the comments of `resp-restrobe.asm`, and the reference pictures `tia-timing/midline-pf_ntsc.png`, `midline-pf_pal.png`, `midline-vblank_ntsc.png` and `midline-grp_ntsc.png` (decoded and looked at row by row; `tools/probes/atari-2600/pnginfo.py` reads the same files) |
| 2k6specs | Martin Korth, *Atari 2600 Specifications*, https://problemkaputt.de/2k6specs.htm |
| Mott 1998 | Bradford W. Mott, "Re: [stella] Games that do bad things to HMOVE...", Stella mailing list, 24 April 1998, http://www.biglist.com/lists/stella/archives/199804/msg00198.html; Towers cites it as "these results by exhaustive testing" |

For sections 18 to 20, besides the SPG, Towers and the suite, also read on 14 October 2026:

- **2k6specs**: Martin Korth, "Atari 2600 Specifications" (nocash), https://problemkaputt.de/2k6specs.htm. A reverse-engineering document; it does not say how each figure was obtained.
- **TIA schematic sheet 1** and **sheet 4**: Atari Inc., "TIA-1A Logic Diagram", sheet 1 "Sync Counter, Playfield and Motion Registers" and sheet 4 "Audio Circuits (2 Reqd.)", rev. E, scanned by Mark De Smet, https://www.atariage.com/2600/archives/schematics_tia/index.html (files `TIA_1A_400dpi_1.zip` and `TIA_1A_400dpi_4.zip`). Read for structure and labels only; the transistor-level logic was not traced. Sheet 3, "Object, Missile Motion Counters" (`TIA_1A_400dpi_3.zip`), was opened by task 9 on 14 October 2026 for open item 19 only.
- **jigo2600 Sound**, **jigo2600 Composite**, **jigo2600 Visual Objects**, **jigo2600 Ports**: the jigo2600 project ("Jigo2600 Team", BSD-2-Clause), documents `doc/TIA_Sound.md`, `doc/TIA_Composite.md`, `doc/TIA_Visual_Objects.md` and `doc/TIA_Ports.md` at commit `92662bb3006c487f5b3b85bd0996a30950325d59`, https://github.com/jigo2600/jigo2600/tree/master/doc. A written analysis of the TIA schematics; only these documents were read, not the project's code.
- **FSM**: Atari Inc., "Atari Video Computer System Field Service Manual, Model 2600/2600A Domestic (M/N)", FD100133 Rev. 02, 1983, http://www.atariguide.com/pdfs/Atari_2600_VCS_Domestic_Field_Service_Manual.pdf, read through the Internet Archive: https://web.archive.org/web/20170215054248/http://www.atariguide.com/pdfs/Atari_2600_VCS_Domestic_Field_Service_Manual.pdf. NTSC consoles only.
- **Stolberg freqform**: Eckhard Stolberg, "Atari 2600 VCS Sound Frequency and Waveform Guide", version 1.01, 25 May 2000, http://buerger.metropolis.de/estolberg/texts/freqform.txt, read through the Internet Archive: http://web.archive.org/web/2002/http://buerger.metropolis.de/estolberg/texts/freqform.txt. By its own account calculated from Ron Fries's TIASOUND emulation and checked by sampling; used only as a cross-check.
- **Ball 2003**: Eric Ball, posts to the Stella mailing list, thread "tia hue luminance", 5, 6, 9 and 11 December 2003, https://www.biglist.com/lists/stella/archives/200312/msg00037.html, msg00049.html, msg00090.html and msg00117.html; Eckhard Stolberg's replies in the same thread, msg00071.html and msg00096.html. A derivation from the console schematics and the video standard, not a measurement.
- **Galloway 2003**: David Galloway, "Re: [stella] TIA Audio Polynomials", Stella mailing list, 19 November 2003, https://www.biglist.com/lists/stella/archives/200311/msg00142.html. A reading of the schematic.
- **Jentzsch 2008**: Thomas Jentzsch, AtariAge forum thread "2600 and 5200 palettes compared", page 3, October 2008, https://atariage.com/forums/topic/132561-2600-and-5200-palettes-compared/page/3/, read through the Internet Archive: http://web.archive.org/web/2021/https://atariage.com/forums/topic/132561-2600-and-5200-palettes-compared/page/3/. Candidate palettes judged by eye.
- **AtariAge 216590**: AtariAge forum thread "Stella gets some new clothes", page 4, posts by Trebor (2013 to 2015) and Thomas Jentzsch (May 2020), https://atariage.com/forums/topic/216590-stella-gets-some-new-clothes/page/4/, read through the Internet Archive: http://web.archive.org/web/2021/https://atariage.com/forums/topic/216590-stella-gets-some-new-clothes/page/4/. Owners' observations; no code read.
- Opened and not used for any statement: Paul Slocum, "Atari 2600 Music And Sound Programming Guide", v1.02, 2003, https://www.qotile.net/files/2600_music_guide.txt (no pattern lengths); AtariAge thread "Atari 2600 TIA NTSC and PAL" (topic 39196) and "Revisiting NTSC Colour Generation" (topic 242235), through the Internet Archive (no documented figures); Norbert Landsteiner, "RC2018/04: Refraction for the Atari 2600 (E-02)", https://www.masswerk.at/rc2018/04/02.html (palette counts from emulator charts).
