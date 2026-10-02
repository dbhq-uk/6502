# BBC Micro Model B video path: fact sheet

Written 2 Oct 2026 for the cycle-accurate video path in `dbhq-uk/6502`. Read first: `docs/superpowers/specs/2026-10-02-bbc-micro-design.md` (origin/main, commit a825ceb). ROMs: `/tmp/bbcrom/` (sha-256 of all three matches the design).

**GPL rule.** I did not open jsbeeb or B-em source. No code was copied or paraphrased. Where a stardot post is by an emulator author, I used the hardware fact only.

**Tags.** `[from Sn]` = read in source Sn (list at the end). `[from ROM]` = read from `/tmp/bbcrom/os.rom` by me. `[inferring ...]` = joining dots. `[guessing - verify]` = a flag for a test or a second source.

**Live-host note.** `beebwiki.mdfs.net`, `mdfs.net` and `6502.org` refused connections. I read the BeebWiki pages from web.archive.org snapshots of the same URLs. cpcwiki.eu is Cloudflare-blocked, so the ACCC PDF came from a web.archive.org copy of its cpcwiki URL.

---

## 1. The 6845 CRTC

### 1.1 Which chip

| Fact | Tag |
|---|---|
| Model B IC2 is a Hitachi 6845 family part, sold as HD6845SP or HD46505SP. No primary Acorn parts list was reachable, so this is from owner reports and one parts note | [inferring from S19 (stardot "6845 Quirks" and the web search it came from)] |
| The OS needs the **HD6845S / "Type 1"** feature set. It writes R3 high nibble = 2 (programmable VSYNC width) and R8 = &93 (display and cursor skew bits). A plain MC6845 or HD6845R has neither (R: VSYNC fixed at 16 lines, no skew) | [from ROM os.rom 0x46E..0x4A9] + [from S1 "Comparison between HD6845S and HD6845R"] |
| **Emulate the HD6845S.** In the CPC community numbering it is "CRTC 0" (type numbers differ from Acorn's own Type 0/1/2 in S22; do not mix them) | [from S23] |
| HD6845S vs R matters in four ways: VSYNC width (S programmable, R fixed 16 lines); skew bits R8[7:4] (S only); R12/R13 readable (S only); interlace-sync-and-video unit (S programs R4/R6/R7 in 1-line units, R in 2-line units) | [from S1] |
| The Master 128 CRTC varies by board (Hitachi or VLSI VL6845) and behaves differently. Out of scope for the Model B | [from S19] |

### 1.2 Port behaviour

| Fact | Tag |
|---|---|
| `&FE00` = address register (write the register number 0..17). `&FE01` = data register. Writes to the data port go to the register named by the last address write | [from S21] |
| The block is `&FE00-&FE07`; I take even = address, odd = data, mirrored four times | [inferring from S1 RS pin: RS = A0; BBC decode of the 8-byte window not read in a primary source] |
| Address register is 5 bits; values 18..31 do nothing | [from S1 "Address Register (AR)"] |
| The OS writes every register with `STY &FE00 / STA &FE01`, and writes R12/R13 and R14/R15 as pairs | [from S7 ch5 s54 s60, `setCRTCRegisterDirect` $C985, `setTwoCRTCRegisters` $CA2B] |
| Register writes take effect immediately, except the start address R12/R13, which is latched at the start of the next frame | [from S10 slides 7-8] + [from S3 "Start address timing"] |

### 1.3 Registers

Write column: all of R0..R15 are writable. R16/R17 are not. Read column per S1.

| Reg | Name | Unit | Bits and meaning | Read? | BBC value (modes 0-2 / 3 / 4-5 / 6 / 7, from ROM) |
|---|---|---|---|---|---|
| R0 | Horizontal total | chars, value = total - 1 | 8 bits | no | 127 / 127 / 63 / 63 / 63 |
| R1 | Horizontal displayed | chars | 8 bits | no | 80 / 80 / 40 / 40 / 40 |
| R2 | H sync position | chars | HSYNC starts when C0 == R2 | no | 98 / 98 / 49 / 49 / 51 |
| R3 | Sync widths | b3..0 = HSYNC width in chars (0 not usable); b7..4 = VSYNC width in lines (0 = 16) | no | &28 / &28 / &24 / &24 / &24 (HSYNC 8 or 4 chars, VSYNC 2 lines) |
| R4 | Vertical total | char rows, value = total - 1 | 7 bits | no | 38 / 30 / 38 / 30 / 30 |
| R5 | Vertical total adjust | scan lines | 5 bits | no | 0 / 2 / 0 / 2 / 2 |
| R6 | Vertical displayed | char rows | 7 bits | no | 32 / 25 / 32 / 25 / 25 |
| R7 | V sync position | char rows | VSYNC starts when C4 == R7 | no | **table 34 / 27 / 34 / 27 / 27, but written as table + 1 (see 3.3): 35 / 28 / 35 / 28 / 28** |
| R8 | Interlace and skew | b1..0: 00 or 10 non-interlace, 01 interlace sync, 11 interlace sync and video. b5..4 = DISPTMG skew (0, 1, 2 chars, 3 = none). b7..6 = CUDISP skew (same coding) | no | 1 / 1 / 1 / 1 / &93 |
| R9 | Max raster address | lines. Non-interlace and interlace sync: value = lines per row - 1. Interlace sync and video: value = lines per row - 2 | 5 bits | no | 7 / 9 / 7 / 9 / 18 |
| R10 | Cursor start | b4..0 = start line. b6..5 = mode: 00 steady, 01 no cursor, 10 blink 16 fields, 11 blink 32 fields | no | &67 (start 7, blink 32) / &67 / &67 / &67 / &72 (start 18, blink 32) |
| R11 | Cursor end | line | 5 bits | no | 8 / 9 / 8 / 9 / 19 |
| R12, R13 | Start address H, L | 14 bits (R12 b7..6 read 0) | MA reload at frame start | yes on S | see 3.1 |
| R14, R15 | Cursor address H, L | 14 bits | compared with MA | yes | set by OS |
| R16, R17 | Light pen H, L | 14 bits | latched on LPSTB | read only | unused |

Sources: columns 1-6 [from S1 Table 1 and notes]; BBC values [from ROM] (see section 3 for offsets).
Cursor end (R11) of 8 in modes 0-2/4/5 is beyond R9 = 7, so the cursor is a one-line underline on line 7 [inferring; datasheet requires R10 <= R11 <= R9 and says nothing on the case R11 > R9].
Reading a write-only register or the address register on a real BBC: [guessing - verify] returns 0 or open bus. The OS never does it.

### 1.4 Counter model (HD6845S)

Idealised model, from the CAST/ACCC description. Quirks follow in 1.6.

| Item | Rule | Tag |
|---|---|---|
| C0 (horizontal) | Counts character clocks 0..R0, then wraps. R0+1 = characters per line | [from S2 6.1.1] |
| Horizontal display | On while C0 < R1. Goes off when C0 == R1, on again at C0 = 0 | [from S2 6.1.3, S3] |
| HSYNC | Starts when C0 == R2. Lasts R3[3:0] chars | [from S2 6.1.2] |
| C9 (scan line) | Counts 0..R9 per char row, then wraps and bumps C4 | [from S2 6.1.1] |
| C4 (char row) | Counts rows. When C4 == R4 and C9 == R9 the active frame ends. Then R5 extra lines follow (adjust), then C4 = 0 and MA reloads from R12/R13 | [from S2 6.1.1, S3] |
| HD6845S adjust detail | There is no separate C5. C9 is reused and compared with R5 instead of R9 during the adjust period | [from S2 11.2.2] |
| Vertical display | On while C4 < R6 | [from S2 6.1.3] |
| VSYNC start | When C4 reaches R7, at C0 = 0 and C9 = 0 | [from S2 7.2] |
| VSYNC length | R3[7:4] lines (0 = 16). The count advances with HSYNC; ACCC says a shortened VSYNC "stops at the end of the HSYNC of this line" | [from S2 14.2]. Exact falling-edge position inside the last line: [inferring from S2 14.2; verify on hardware] |
| DISPTMG | Horizontal AND vertical display, delayed by R8[5:4] chars | [from S1 R8 text] |
| CUDISP | Cursor output, delayed by R8[7:6] chars. Inhibited while DISPTMG is low. Active on lines R10[4:0]..R11 of the char row where MA == R14/R15, when the blink phase allows | [from S1 "Cursor Control"] + [inferring line comparison] |
| Cursor blink | 16 or 32 field periods, from a frame counter | [from S1 Table 7]; "frame counter" [from S19 post by dreamseal] |
| MA | Linear counter, 14 bits. Reloaded from R12/R13 at frame start. Same row start is reloaded on each scan line of a char row; after the last line of the row the row start advances by R1 | [from S2 6.1.4] + [from S24 "Internals"] |
| RA | C9 (5 bits). In interlace sync and video mode RA steps 0,2,4... on even fields and 1,3,5... on odd fields, for an even raster total | [from S1 Table 9] |
| First field after /RES | DISPTMG and CUDISP stay low, MA and RA start at 0, R12/R13 ignored | [from S1 "Display sequence after /RES release"] |

**Interlace (both modes).** Total lines per frame are odd. The VSYNC pulse is delayed half a line (to C0 = R0/2) on even fields, and the even field gets one extra line after its R5 lines. VSYNC to VSYNC is then a constant 312.5 lines. [from S2 19.3.1, 19.3.2.1 and 11.9] + [inferring the constant 312.5 from the half-line delay plus one extra line]. Which BBC field is "even": [guessing - verify].

**Interlace sync and video (mode 7).** Per-field char row is R9+2 total lines / 2 = 10 lines for R9 = 18. R4, R6, R7 count rows of 10 lines. R5 is still a plain line count. [from S1 R9 note and Table 9] + [from S2 19.3.3, 19.4.1: "R9 = N-2 ... R4, R6 and R7 ... characters contain twice less lines"].

### 1.5 What the BBC does with the CRTC pins

| CRTC pin | Goes to | Tag |
|---|---|---|
| CLK | 1 MHz or 2 MHz from the video ULA (ULA control bit 4) | [from S4 "Control register"] |
| MA0-MA13, RA0-RA2 | Address translation hardware to the DRAM (see 2.5). MA13 selects the teletext path | [from S5] |
| RA3 | Gates DISEN to the ULA: DISEN = DISPTMG AND NOT RA3. So lines 8-9 of a 10-line row are black in modes 3 and 6 | [from S5 "HI RES"] + [from S8 "INVERT, DISEN & CURSOR"] (two sources agree) |
| RA0 (inverted, "!SC0") | SAA5050 CRS pin (odd/even field select) | [from S17 post by SarahWalker, 10 Mar 2017, reading the schematic] |
| DISPTMG | Video ULA DISEN (via RA3 gate); SAA5050 LOSE, latched by IC15 | [from S8]; LOSE [from S16 posts by Rich Talbot-Watkins, reading the circuit diagram] |
| CUDISP | Video ULA CURSOR | [from S4] |
| HSYNC | NOR with VSYNC makes composite sync (IC41); SAA5050 GLR | [from S9 s3.6] + [from S16] |
| VSYNC | System VIA CA1; SAA5050 DEW. OS sets PCR = %00000100: CA1 on the **negative edge**; IER bit 1 enabled; IFR bit 1 = "vertical sync" | [from S7 ch10 s13, `systemVIAPeripheralControlRegister`] + [from S16 SarahWalker: DEW is VSYNC] |
| LPSTB | 15-way analogue connector and VIA CB2. Left out of scope | [from S9] + [from S7 ch10 s13 PCR comment] |

Is the RA3 gate disabled in mode 7? It has to be. Mode 7 runs RA 0..19, and RA3 is set for 8..15, yet the picture is not blanked. The gating circuit is not described anywhere I could read [inferring; mechanism not established; see section 6].

### 1.6 Known HD6845S quirks that a model must decide on

All from the real-hardware work in S19 and the ACCC (S2). Not needed for standard modes. Needed for demos and cycle-exact timing.

| Quirk | Source |
|---|---|
| The end-of-frame decision is latched early: "taken 2 cycles after the start of the new scanline". Changing R4 or R9 after that on the last line does not stop the frame ending | S19 posts by scarybeasts and Rich Talbot-Watkins; S2 10.3.1.2 ("last frame line" armed when C0 < 2) |
| R0 = 1 skips lines with no HSYNC. R0 = 3 is the minimum "for sanity" | S19; S2 13.2.5 |
| R9 changes: if R9 is set equal to current C9, C9 goes to 0 next line; if set below C9, C9 keeps counting up to 31 and wraps | S2 10.3.1.1 |
| C9 bits 3-4 are not used for addressing; C9 = 8 addresses line 0 | S2 10.1 |
| R5 adds lines on every frame; in interlace an extra line is added on top of R5 | S2 11.2, 11.9 |
| A rewrite of R0, R2, R8, R9 in the display period can disturb the picture | S1 "Anomalous operations" |

---

## 2. The video ULA (Ferranti ULA 5C094 / VIDPROC)

### 2.1 Interface

| Fact | Tag |
|---|---|
| Two write-only registers: `&FE20` control, `&FE21` palette. Address bit A0 picks the register | [from S8 "Pin layout"; S7 ch3 s8, s9] |
| MOS keeps RAM copies: control at `&248`, palette at `&249`. Use OSBYTE 154 / 155 | [from S4] |
| It also divides the 16 MHz master clock into 8, 4, 2, 1 MHz | [from S4 "Clock division"; S9 s3.3] |
| It is on the 4 MHz DRAM data bus. CPU and video each get a 2 MHz slot, interleaved, so no RAM contention | [from S4 "Video serialisation"; S9 s3.5 "alternately switched every 250ns"] |

### 2.2 Control register `&FE20` (write only)

| Bit | Meaning | Tag |
|---|---|---|
| 0 | Flash colour select. When set, palette entries with the flash bit show the inverted colour. The OS toggles this from the VSYNC IRQ using mark/space counters, default **25 and 25 VSYNCs** (OSBYTE 9/10; initial counter &19). Cycle: 50 fields | [from S4] + [from S7 ch11 flash code, ch10 default table at $D991] + [from ROM: bytes `19 19 19 32` at os.rom 0x1991] |
| 1 | Teletext select. 1 = pass the SAA5050 RGB through, palette bypassed | [from S4, S9 s3.6] |
| 3..2 | Characters per line: 00 = 10, 01 = 20, 10 = 40, 11 = 80. This sets the pixel rate: 2 / 4 / 8 / 16 MHz | [from S4, S8] |
| 4 | CRTC clock: 0 = 1 MHz (40 bytes per line), 1 = 2 MHz (80 bytes per line) | [from S4, S8] |
| 7, 6, 5 | Cursor segments 0, 1, 2. Segment 0 and 1 are each 1 CRTC char wide; segment 2 is 2 chars wide. Chars are 1 or 2 per us by bit 4. Result: widths of 1, 2 or 4 bytes | [from S4 "Control register"; S8] + [from S7 ch4 s10 "cursor width in bytes 1,2,4"] |

Notes:
- 80 columns with the 1 MHz CRTC clock: the shift register runs out and every other char is blank, filled with logical colour 15. [from S4]
- 10 columns with the 2 MHz clock: only the odd bits of each byte affect the picture. [from S4]
- Cursor in mode 7 is segment 1 only, because teletext output is delayed one char. [from S4]

Per-mode control bytes, **read from ROM at `$C3F7` (file offset 0x03F7)** and matching S4 and S7:

| Mode | Byte | Bits 7-5 | b4 | b3-2 | b1 | b0 |
|---|---|---|---|---|---|---|
| 0 | &9C | 100 | 1 | 11 (80) | 0 | 0 |
| 1 | &D8 | 110 | 1 | 10 (40) | 0 | 0 |
| 2 | &F4 | 111 | 1 | 01 (20) | 0 | 0 |
| 3 | &9C | 100 | 1 | 11 | 0 | 0 |
| 4 | &88 | 100 | 0 | 10 | 0 | 0 |
| 5 | &C4 | 110 | 0 | 01 | 0 | 0 |
| 6 | &88 | 100 | 0 | 10 | 0 | 0 |
| 7 | &4B | 010 | 0 | 10 | 1 | 1 |

[from ROM] + [from S4 table]. The ROM and BeebWiki agree on all eight.

### 2.3 Palette register `&FE21`

| Fact | Tag |
|---|---|
| 16 entries x 4 bits (64 bits of fast RAM). A write stores bits 3..0 at the entry named by bits 7..4 | [from S4, S8] |
| Entry bits: b3 = flash, b2 = ~blue, b1 = ~green, b0 = ~red. So **value written = physical colour XOR 7** (bits 2..0) | [from S7 ch3 s9, S4, S8] |
| Physical colours 0-7: black, red, green, yellow, blue, magenta, cyan, white. 8-15 = flashing pairs: 8 black-white, 9 red-cyan, 10 green-magenta, 11 yellow-blue, 12 blue-yellow, 13 magenta-green, 14 cyan-red, 15 white-black | [from S7 ch3 s9] |
| Output: if (entry flash bit AND control bit 0) the stored (inverted) RGB goes out, else it is re-inverted. So a flashing entry alternates between colour c and c XOR 7 | [from S4 "Video serialisation"] + [inferring the c XOR 7 reading; it matches the "flashing red-cyan" names] |
| The palette is looked up with the shift register bits **7, 5, 3, 1** as the 4-bit index | [from S4] |
| Because only some index bits matter in a mode, a logical-colour change needs several writes: **8 writes in modes 0, 3, 4, 6** (value bit 7 = logical colour; bits 6..4 free); **4 writes in modes 1, 5** (bits 7 and 5 = logical colour; bits 6 and 4 free); **1 write in mode 2** | [from S7 ch3 s9] + [from S4] |
| Power-up palette writes, in order (hex): modes 0,3,4,6: `80 90 A0 B0 C0 D0 E0 F0 07 17 27 37 47 57 67 77`. Modes 1,5: `A0 B0 E0 F0 84 94 C4 D4 26 36 66 76 07 17 47 57`. Mode 2: `F8 E9 DA CB BC AD 9E 8F 70 61 52 43 34 25 16 07` | [from S4] |

I decoded all three by hand and they give: modes 0/3/4/6 logical 0 = black, 1 = white; modes 1/5 0 = black, 1 = red, 2 = yellow, 3 = white; mode 2 logical n = physical n for 0-7 and flashing (n-8) for 8-15. [inferring from S4 write lists; consistent with the OS defaults in S7]

Palette writes mid-line change the colour from the next pixel. [guessing - verify exact pixel latency]

### 2.4 Turning bytes into pixels

Model that fits S4, S8 and the ROM font and fill tables. It is a model, not a described circuit.

1. Once per CRTC char clock the ULA latches the byte on the data bus (the CRTC half of the memory cycle). [from S9 s3.6 "latched in to the video ULA at the end of each CRTC access cycle"]
2. A pixel clock ticks at 16, 8, 4 or 2 MHz (control bits 3..2). Each tick the 8-bit shift register shifts left one bit, filling with 1s. [from S4]
3. Each tick the palette index = {sr7, sr5, sr3, sr1}. [from S4]
4. Ticks per CRTC char = pixel clock / CRTC clock.
5. DISEN low blanks the output to black. CURSOR inverts the output. [from S4 "final stage"]. The FPGA doc's polarity wording ("RGB high when blanked") differs; the real picture is black [from S4; trust S4, S8 is a clone author's note].

| Mode | CRTC clock | Pixel clock | Ticks per byte | Bits per pixel | Pixel p (0 = left) takes bits | Tag |
|---|---|---|---|---|---|---|
| 0 | 2 MHz | 16 MHz | 8 | 1 | b(7-p) | model + [from ROM mask table $C40D: 80 40 20 10 08 04 02 01] |
| 1 | 2 MHz | 8 MHz | 4 | 2 | high = b(7-p), low = b(3-p) | model + [from ROM $C409: 88 44 22 11] |
| 2 | 2 MHz | 4 MHz | 2 | 4 | p0 = b7,b5,b3,b1 (b7 = colour bit 3); p1 = b6,b4,b2,b0 | model + [from ROM $C407: AA 55] |
| 3 | 2 MHz | 16 MHz | 8 | 1 (text only, 10-line rows) | as mode 0 | [from ROM tables] |
| 4 | 1 MHz | 8 MHz | 8 | 1 | as mode 0 | model |
| 5 | 1 MHz | 4 MHz | 4 | 2 | as mode 1 | model |
| 6 | 1 MHz | 8 MHz | 8 | 1 (text only) | as mode 0 | model |
| 7 | 1 MHz | SAA5050 12 half-dots per char | - | - | SAA5050 RGB | see section 4 |

Check that the OS solid-colour fill bytes agree: mode 1 colour 1 = %00001111, colour 2 = %11110000 (the low colour bit of each pixel lives in bits 3..0, the high bit in 7..4). Mode 2 colour 1 = &03, 2 = &0C, 4 = &30, 8 = &C0. [from ROM $C426 and $C42A] These fit the table above exactly.

The ROM also sets "pixels per byte - 1" = 7, 3, 1, 0, 7, 3 for modes 0-5 [from ROM $C43A]; modes 3 and 6 have no graphics (0 = "no graphics available"). [from S7 ch4 s11]

Cursor drawing: segments are 1, 1 and 2 CRTC chars wide, drawn in turn from the char where CUDISP starts. [from S4]. How many chars wide each segment really is at 1 MHz vs 2 MHz: S4 says "1/40 or 1/80 of the display width" = 1 CRTC char either way. [from S4]

### 2.5 Screen memory address from MA and RA

Three address modes alternate on the 2 MHz clock. The CRTC never needs a RAM stall: CPU and video interleave. [from S5]

| Path | When | Address formed | Tag |
|---|---|---|---|
| CPU | 2 MHz clock low | A0..A14 straight through | [from S5] |
| **HI RES** (modes 0-6) | 2 MHz high and MA13 = 0 | `DA14..DA11 = AA3..AA0` (MA11..MA8 after wrap fix), `DA10..DA3 = MA7..MA0`, `DA2..DA0 = RA2..RA0`. So **byte address = (MA << 3) \| (RA & 7)** before wrap | [from S5 "HI RES"] |
| **TTX VDU** (mode 7) | 2 MHz high and MA13 = 1 | RA ignored. `DA14 = AA3`, `DA13..DA10 = 1`, `DA9..DA7 = MA9..MA7`, `DA6 = MA6 XOR NOT(1MHz clock)`, `DA5..DA0 = MA5..MA0`. So **address = ((MA & &800) << 3) \| &3C00 \| (MA & &3FF)**. Result is in &7C00-&7FFF (or &3C00-&3FFF when MA11 is clear) | [from S5 "TTX VDU", "MODE 7"] |

**Hardware scroll and wrap (HI RES).** When MA12 goes high the address has passed &7FFF. A quad adder (IC39) subtracts a fixed amount from the high address bits. The amount is chosen by two bits C0, C1 of the addressable latch IC32, written through system VIA port B bits 3..0 (bit 3 = data, bits 2..0 = latch address; C0 = latch output 4, C1 = latch output 5). [from S5; S9 s3.6]

| C1 | C0 | Subtract | Restart | Modes | Screen bytes | ROM write pair (latch addr 4 first, then 5) |
|---|---|---|---|---|---|---|
| 1 | 0 | &5000 | &3000 | 0, 1, 2 | &5000 (20 KB) | 4, then 13 |
| 0 | 0 | &4000 | &4000 | 3 | &4000 (16 KB) | 4, then 5 |
| 1 | 1 | &2800 | &5800 | 4, 5 | &2800 (10 KB) | 12, then 13 |
| 0 | 1 | &2000 | &6000 | 6 | &2000 (8 KB) | 12, then 5 |

[from S5 table] + [from ROM `$C44B` = 0D 05 0D 05 and `$C44F` = 04 04 0C 0C 04]. The ROM write pair for mode 7 writes 4 twice and leaves C1 unchanged; the HI RES path is unused in mode 7. [from ROM]

Worked check: mode 0, start MA = &0600 gives &3000. At MA = &1000 the unwrapped address is &8000; minus &5000 = &3000. [inferring from S5; arithmetic is mine]

Mode 7 start address: the OS writes R12/R13 = &28/&00 for screen &7C00. Computed in the OS as: high byte &7C minus &74 gives &08, XOR &20 gives &28. For modes 0-6 the OS writes start address >> 3. [from S7 ch5 s59, `$CA0E-$CA2B`]. A start of &2400 gives a 2 KB linear buffer &3C00-&3FFF then &7C00-&7FFF. [from S5]

The refresh side effect (MA6 XOR 1 MHz in mode 7 fetches two bytes per us, the SAA5050 gets one) does not change what is shown. [from S5]

---

## 3. OS screen modes 0-7

### 3.1 Table, read out of `/tmp/bbcrom/os.rom`

OS 1.20. File offset = CPU address - &C000. The CRTC rows are R0..R11 as stored in the ROM. R7 is shown stored (the OS adds 1 on write, see 3.3). Mode to table-group index at `$C440` is `0 0 0 1 2 2 3 4`.

| Mode | Text | Gfx | Colours | ULA ctl | CRTC R0..R11 as stored | Base | Bytes | R12/R13 |
|---|---|---|---|---|---|---|---|---|
| 0 | 80 x 32 | 640 x 256 | 2 | &9C | 7F 50 62 28 26 00 20 22 01 07 67 08 | &3000 | &5000 | &0600 |
| 1 | 40 x 32 | 320 x 256 | 4 | &D8 | same as mode 0 | &3000 | &5000 | &0600 |
| 2 | 20 x 32 | 160 x 256 | 16 (8 + 8 flashing) | &F4 | same as mode 0 | &3000 | &5000 | &0600 |
| 3 | 80 x 25 | none (640 x 200 text) | 2 | &9C | 7F 50 62 28 1E 02 19 1B 01 09 67 09 | &4000 | &4000 | &0800 |
| 4 | 40 x 32 | 320 x 256 | 2 | &88 | 3F 28 31 24 26 00 20 22 01 07 67 08 | &5800 | &2800 | &0B00 |
| 5 | 20 x 32 | 160 x 256 | 4 | &C4 | same as mode 4 | &5800 | &2800 | &0B00 |
| 6 | 40 x 25 | none (320 x 200 text) | 2 | &88 | 3F 28 31 24 1E 02 19 1B 01 09 67 09 | &6000 | &2000 | &0C00 |
| 7 | 40 x 25 | teletext | n/a | &4B | 3F 28 33 24 1E 02 19 1B 93 12 72 13 | &7C00 | &0400 | &2800 |

Where each table sits [from ROM; each row cross-checked against S7 ch4 and ch6]:

| Table | CPU addr | File offset |
|---|---|---|
| Rows - 1 per mode (31 31 31 24 31 31 24 24) | $C3E7 | 0x03E7 |
| Columns - 1 per mode (79 39 19 79 39 19 39 39) | $C3EF | 0x03EF |
| ULA control per mode | $C3F7 | 0x03F7 |
| Bytes per char per mode (8 16 32 8 8 16 8 1) | $C3FF | 0x03FF |
| Pixel masks (2 / 4 / 8 colour) | $C407 / $C409 / $C40D | 0x0407 / 0x0409 / 0x040D |
| Solid fill bytes (2 / 4 / 16 colour) | $C424 / $C426 / $C42A | 0x0424 / 0x0426 / 0x042A |
| Pixels per byte - 1 | $C43A | 0x043A |
| Mode to group index | $C440 | 0x0440 |
| VIA wrap latch values, table 1 and 2 | $C44B / $C44F | 0x044B / 0x044F |
| Screen size high byte per group (50 40 28 20 04) | $C459 | 0x0459 |
| Screen start high byte per group (30 40 58 60 7C) | $C45E | 0x045E |
| Bytes per row low (40, 320, 640) | $C463 | 0x0463 |
| Cursor-end offsets (11 23 35 47 59) | $C469 | 0x0469 |
| CRTC R0..R11: modes 0-2 / 3 / 4-5 / 6 / 7 | $C46E / $C47A / $C486 / $C492 / $C49E | 0x046E / 0x047A / 0x0486 / 0x0492 / 0x049E |
| 8 x 8 font, chars &20-&7F (96 x 8 bytes; char &20 at offset 0) | $C000-$C2FF | 0x0000-0x02FF |
| Boot text "BBC Computer " | $C304 | 0x0304 |
| Teletext OSWRCH conversion table (23 5F 60 23) | $C4B6 | 0x04B6 |
| Copyright and credits text (at CPU $FC00, hidden by the I/O pages) | $FC00 | 0x3C00 |

(The copyright string "(C) 1981 Acorn Computers Ltd." is at file offset 0x3C00 [from ROM]; that is CPU $FC00, hidden behind the I/O pages, so the CPU never sees it.) The font offsets let the boot-test helper decode the framebuffer from the ROM. [inferring from the design's boot test; offset 0x08 holds `18 18 18 18 18 00 18 00` = "!" [from ROM]]

**Cross-checks.**
- Text and graphics sizes, colours, memory and address ranges match Wikipedia's mode table: modes 0-2 3000-7FFF 20 KB, mode 3 4000-7FFF 16 KB, modes 4-5 5800-7FFF 10 KB, mode 6 6000-7FFF 8 KB, mode 7 7C00-7FFF 1 KB. [from S20]
- Mode 7 CRTC values `3F 28 33 24 1E 02 19 1B 93 12 72 13` and ULA &4B match the BeebWiki MODE 7 page, which cites "MOS 1.20 &C49E..&C4A9". [from S6]
- Modes 0-2 CRTC values (R0 127, R1 80, R2 98, R4 38, R6 32, R9 7) match the Bitshifters slides. The slide gives R7 = 35: that is table 34 + 1. [from S10 slide 6]
- ULA bytes match S4 and S7 table for all eight modes.
- The New Advanced User Guide table (p190) was not reachable, so this is not a check against the AUG itself.

**OS version.** `os.rom` is **OS 1.20** [inferring: every table address above matches the annotated OS 1.20 disassembly in S7, and the boot text and credits match]. This answers the design's open item. The design also says the OS names itself "BBC Computer"; the screen banner is "BBC Computer" then memory size, from `$C304`. [from ROM]

### 3.2 Default mode at reset

Chosen by the eight DIP switches next to the keyboard, read by the OS at power-on through keyboard internal key numbers 9, 8, 7 for mode bits 0, 1, 2 (DIP positions 8, 7, 6). A switch that is "set" (closed) reads as 1; the OS inverts the byte, so **a closed switch gives a 0 bit in the mode number**. [from S7 ch10 s6-s7 and ch17 table] The inversion is an inference from the code comments plus the EOR #&FF line: [inferring]. With all switches open the mode is 7. [inferring; the OS comment says "EOR with 11111111, start up options"]. Conventional factory setting is mode 7 [guessing - verify; no source read]. To boot into mode m in the tests: close the switch for each 0 bit of m. DIP bit 3 (switch 5) reverses SHIFT-BREAK; bits 4-5 (switches 4-3) are disc timing. [from S7 ch17]

### 3.3 What the OS writes to the CRTC on a mode change

Order, from the disassembly [from S7 ch6 s2, ch5 s53 s59]:
1. VIA wrap latch: table 2 value then table 1 value (3 above), to system VIA port B.
2. Video ULA control byte for the mode (via OSBYTE 154 path, so the RAM copy updates).
3. R11 down to R0 (loop counts Y from 11 to 0, reading the table backwards). Each write goes through `setCRTCRegisterAY`, which:
   - **R7: writes table value + `vduVerticalAdjust` + 1** (carry is set by `CPY #7`). With *TV 0 that is table + 1. So R7 is 35 (modes 0-2, 4, 5) or 28 (modes 3, 6, 7).
   - **R8: if bit 7 of the table value is clear, writes table EOR `vduInterlaceValue`** (0 or 1). So modes 0-6 give R8 = 1 by default and R8 = 0 after `*TV x,1` (interlace off). Mode 7's &93 is written unchanged, so interlace cannot be turned off in mode 7.
   - R10 and others: direct.
4. Palette defaults (VDU 20), windows, then R12/R13 = start address (3.1), cursor, clear screen.
`*TV` (OSBYTE 144) takes effect only at the next mode change. [from S14 quoting the AUG, "2.20 *TVx,y"]

---

## 4. Mode 7 teletext (SAA5050)

### 4.1 Grid, clocking, delays

| Fact | Tag |
|---|---|
| 40 x 25 chars. Each cell is 6 dots x 10 TV lines per field = 12 x 20 sub-pixels per frame. Screen 480 x 500 | [from S11 "Description"; S20; S6] |
| Alphanumerics use a 5 x 9 matrix. The ROM does not hold the top row or left column of the 6 x 10 cell | [from S11; S13 header comments and `glyphs[]` comment "Top row (and left column) don't appear in ROM"] |
| Clocks: TR6 = 6 MHz dot clock, F1 = 1 MHz character clock. 6 MHz is made from the 8, 4 and 2 MHz clocks, phase-locked to the 16 MHz master | [from S9 s3.3; S11] |
| The chip outputs on both edges of 6 MHz: 12 sub-pixels per char | [from S13 header comment "uses both edges to output subpixels"] |
| Input is 7 bits. Bit 7 of the byte is ignored. So &80-&9F are control codes &00-&1F | [from S11; S6] |
| On-chip delay, char data in to output: about 2.6 us (graphics) and 2.77 us (alphanumerics) | [from S11 timing table] |
| The BBC compensates: R8 = &93 gives 1 char DISPTMG skew and 2 char CUDISP skew; the ULA draws cursor segment 1 only. Visible result: mode 7 sits one char right of mode 6 | [from ROM R8] + [from S1 R8] + [from S4, S6] |
| Mode 7 reads one byte per char from MA (RA ignored). Same 40 bytes are presented on every scan line of a row | [from S5] |
| **Line counting is inside the chip.** DEW (from VSYNC) resets the internal row counter at frame start; GLR (from HSYNC) and LOSE (from DISPTMG) advance it. The chip assumes 10 lines per char row per field | [from S11 pin text; S16 posts; S17 Tom Seddon post]. Exact increment edge: [guessing - verify] |
| Field parity: the chip reads CRS, wired to inverted RA0. CRS = 0 on the odd field, 1 on the even field | [from S11 "Character rounding"] + [from S17 SarahWalker] |
| The CRTC R9 register can be changed without the SAA5050 noticing: the chip keeps counting 10-line rows (a "mode 7/75" demo shows this on real hardware) | [from S16 posts by kieranhj and Rich Talbot-Watkins] |
| Row height: R9 = 18 in interlace sync and video gives 10 lines per row per field, 20 per frame | [from S1 R9; S6 "text rows must be 20 scanlines deep"] |

### 4.2 Control codes &80-&9F

"Set-at" = effect on the control cell itself. "Set-after" = effect from the next cell. All control cells show as a space in the current background, except under Hold Graphics. Tags: ETSI = [from S12 Table 26]; SAA5050 table = [from S11 Table 1].

| Code | Name | When | Notes |
|---|---|---|---|
| &80 | Alpha black (ETSI) / NUL (SAA5050) | - | SAA5050 Table 1 marks 0/0 reserved. No black foreground on the chip. [from S11]. BBC User Guide wording not read: [guessing - verify] it is a no-op |
| &81-&87 | Alpha red, green, yellow, blue, magenta, cyan, white | set-after | Select alphanumeric set, set foreground. Also cancels conceal. &87 = row default. [from S12; S18 posts confirm BBC colour changes from the next char] |
| &88 | Flash | set-after | Foreground pixels alternate with background. Cleared by &89 or row start |
| &89 | Steady | set-at | Row default |
| &8A | End box | set-after | No visible effect on the BBC [inferring from S11 Table 3 with PO/DE ties unknown] |
| &8B | Start box | set-after | Same |
| &8C | Normal height | set-at | Row default |
| &8D | Double height | set-after | See 4.3 |
| &8E, &8F | SO, S1 | - | Reserved on the SAA5050. [from S11 Table 1] |
| &90 | Graphics black (ETSI) / DLE (SAA5050) | - | Reserved, as &80 |
| &91-&97 | Graphics red ... white | set-after | Select the graphics set, set foreground |
| &98 | Conceal | set-at | Spaces until a colour code or row end. Conceal takes priority over hold substitution. [from S12; S18 post by Richard Russell] |
| &99 | Contiguous graphics | set-at | Row default |
| &9A | Separated graphics | set-at | Stardot: with hold active, a separated attribute takes effect "from the first non-control code". [from S18]. Treat with care: [guessing - verify] |
| &9B | ESC | - | Reserved on the SAA5050 |
| &9C | Black background | set-at | Row default. Sets the background to black |
| &9D | New background | set-at | Background takes the current foreground colour |
| &9E | Hold graphics | set-at | See 4.3 |
| &9F | Release graphics | set-after | Row default |

Row defaults at the start of every row: alpha white, steady, end box, normal height, contiguous, black background, release. [from S12 Table 26 "Start-of-row default"] + [from S11 Table 1 note "presumed before each row begins"]

Graphics cells: bits b0,b1,b2,b3,b4,b6 of the byte set TL, TR, ML, MR, BL, BR (ETSI numbers bits from 1: bits 1,2,3,4,5,7). Applies to codes &20-&3F and &60-&7F in graphics mode. Bit 5 (&20) must be set; codes &40-&5F stay alphanumeric in graphics mode ("blast through"): `@ A-Z ← ½ → ↑ #`. [from S12 Table 26; S11 Fig 9 note "b6 always 1"; S15 post by dreamseal on the decode] Block geometry: top blocks lines 0-2, middle 3-6, bottom 7-9 of the 10-line cell (3, 4, 3). [from S20 "6 x 6, 6 x 8, 6 x 6 in 12 x 20"; S16 "3, 4, 3"]. Separated graphics shrink each block by 2 sub-pixels each way. [from S20]. Exact pixel positions: not established (S11 Fig 10 is a drawing, not dimensions).

### 4.3 Behaviour details

| Topic | Fact | Tag |
|---|---|---|
| Hold graphics | While active, every control cell shows the most recent graphics char with its own contiguous or separated form, not the current mode. Held char is reset to space at row start, on alpha/graphics change, and on a height change (not on a repeated same-height code) | [from S12 Table 26 0/E; S15 post by dreamseal 31 Jan 2021] |
| Hold graphics, SAA5050 specific | A graphics char is only cached after the Hold code has been seen on the row (other chips cache earlier). Chars &40-&5F do not feed or take part in hold. Hold takes effect from the next cell; Release takes 2 char cycles to clear in the simulation (doubtful) | [from S18 Richard Russell; S15 posts 28-30 Jan 2021] |
| Double height | A row with an &8D code shows the top half of double-height chars. The row below must repeat the text with &8D; it then shows the bottom half. Non-double-height cells on the lower row are shown as background only | [from S12 Table 26 0/D; S18 summary of BBC BASIC manual]. The chip tracks top/bottom itself via an internal height flip-flop: [inferring from S11 TLC pin and S15 "height change pulse"] |
| Double-height rounding | Reference row alternates up/down each line (internal signal). Normal height uses CRS | [from S11 "Character rounding"] |
| Which glyph rows appear on which lines in double height | Not established. [guessing - verify: each of the 9 glyph rows is shown on about 2 lines of the 10, top half rows 0-4, bottom half rows 4-8] | - |
| Character rounding | Diagonals in the 5 x 9 matrix get half-dots added. Compare each glyph row with the row before (even field, CRS = 1) or the row after (odd field, CRS = 0). Rule per 2 x 2 clump containing a diagonal: add two sub-pixels. 5 x 9 expands to 10 x 18 over the 2 fields | [from S11 "Character rounding"; S13 header diagram] |
| Flash | Counter of 6 bits, 0..63, increments once per field (on DEW = VSYNC). Text is **off for counts 0-15 and on for 16-63** (16 fields off, 48 on; 0.78125 Hz). The datasheet says 0.75 Hz at 3:1; the die-shot work says the datasheet is wrong. **Trust the die shot** (it gives whole fields) | [from S15 post by dreamseal; S11 "Flash oscillator"] |
| Flash vs palette flash | Independent. The palette flash is the ULA's, 25 / 25 VSYNCs from the OS | [from S15 summary; S7] |
| Reset | Power-on resets to tv, conceal, not superimpose modes | [from S11] |
| Box, reveal, superimpose | No effect on the BBC. PO, DE tie-offs not verified | [inferring from S11 Table 3] |
| Top row of the cell | Blank in alphanumerics. Rows are 10 lines x 2 fields | [from S11, S13] |

### 4.4 ASCII to glyph

Codes &20-&7F alphanumeric set = the SAA5050 English set (Fig 11 of S11, 96 glyphs, 16 x 6 grid). Same as US ASCII (SAA5055, Fig 12) except:

| Code | English set shows | US ASCII shows |
|---|---|---|
| &23 | £ | # |
| &27 | ' (plain vertical tick) | ' (curly right quote) |
| &5B | left arrow | [ |
| &5C | one half | \ |
| &5D | right arrow | ] |
| &5E | up arrow | ^ |
| &5F | # | _ |
| &60 | long dash | reversed quote |
| &7B | one quarter | { |
| &7C | double vertical bar | broken bar |
| &7D | three quarters | } |
| &7E | divide | ~ |
| &7F | solid block | solid block |

[from S13 `glyphs[]`, section "Extra characters found in the English (SAA5050) character set", checked against S11 Fig 11 grid positions 2/3, 2/7, 5/11-5/15, 6/0, 7/11-7/15.]

OS side: writing a char to screen memory directly gives £ for &23, # for &5F and a long dash for &60. Printing through OSWRCH converts: # prints as stored &5F, underscore prints as a long dash, and the pound sign (&60 in BBC text) is stored as &23. [from S7 ch4 s16, `teletextCharacterConversionTable` at $C4B6 = bytes 23 5F 60 23, "replace each byte with the next" when writing in mode 7; verified in ROM].

### 4.5 The glyph licensing question

The repo is MIT. jsbeeb is GPL-3.0 (GitHub API licence field: GPL-3.0). B-em is GPL. Neither can supply glyph data.

**Are the 5 x 9 grids published in the datasheet?** Yes. Fig. 11 "SAA5050 character set (English)" draws all 96 glyphs as pixel grids. Fig. 12 draws the US ASCII (SAA5055) set. In the PDF below it is page 16 (printed page 5-270). Table 1 (PDF page 13, printed 5-267) gives the code layout. Fig. 9 (page 11) gives the graphics bit mapping; Fig. 10 (page 12) shows contiguous vs separated blocks.
URL: http://www.elektronikjk.com/elementy_czynne/IC/SAA5050.pdf (Signetics SAA5050/55 sheet, 17 pages). The Mullard original is cited by Wikipedia at https://vd-view.azurewebsites.net/Documents/SAA5050.pdf (not fetched). [from S11]

**Candidates, with licence text.**

| # | Source | Licence, verbatim or as read | Usable in an MIT repo? |
|---|---|---|---|
| A | **Bedstead** by Ben Harris, Simon Tatham, Marnanel Thurman, Neil Williamson. `bedstead.c` v3.261, glyph table `glyphs[]` at about lines 255-372, 9 octal row bytes per glyph. https://bjh21.me.uk/bedstead/ | Header: "To the extent possible under law, Ben Harris, Simon Tatham, Marnanel Thurman and Neil Williamson have dedicated all copyright and related and neighboring rights to this software and the embodied typeface to the public domain worldwide. This software and typeface are distributed without any warranty. You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>." Same header also says: "Many of the character bitmaps below formed the typeface embodied in the SAA5050 series ... Copyright in the typeface will still be owned by Mullard's corporate successors, but under section 55 of the Copyright Designs and Patents Act 1988 that copyright is no longer infringed by the production or use of articles specifically designed or adapted for producing material in that typeface." Web page: "I believe that the original SAA5050 bitmap font is essentially in the public domain in the United Kingdom ... I'm not a lawyer, though, so this may well be wrong." Mirrors: github.com/textmodes/bedstead (GitHub API: CC0-1.0, last push 2017, stale) and github.com/glxxyz/bedstead (Teletext50, GitHub API: CC0-1.0) | **Yes.** Data only (rows of 5 bits), with provenance recorded |
| B | The Signetics datasheet itself (Fig 11) | No licence text in the PDF. Rights reserved by default | Reading it is fine. Copying the artwork is not. Transcribing facts (which dots are on) is the same typeface-copyright question as A |
| C | ETSI EN 300 706 V1.2.1 (Enhanced Teletext). Latin G0 set table in clause 15.6.1. https://www.etsi.org/deliver/etsi_en/300700_300799/300706/01.02.01_60/en_300706v010201p.pdf | "No part may be reproduced except as authorized by written permission. The copyright and the foregoing restriction extend to reproduction in all media. (c) European Telecommunications Standards Institute 2003. (c) European Broadcasting Union 2003. All rights reserved." | No. Also a different glyph design from the BBC chip |
| D | fabianswebworld/teletext-decoder-font ("inspired by the pixel glyphs depicted in the ETSI Teletext standard") | GPL-3.0 (GitHub API) | No |
| E | NoxiousPluK/SAA5050-Font-Rom-Extractor | MIT (GitHub API). It is a script that extracts glyphs from a chip ROM dump. It carries no glyph data | Not as a source. A dump of the ROM is the data, and that is Mullard's |
| F | opless/RetroText ("8x9 Viewdata / Teletext Font based in part on the ZX Spectrum font") | MIT (GitHub API) | Licence fine, shapes are a different font and based partly on a Sinclair font. Not faithful |
| G | lanceewing/saa5050 (die-shot logic reverse engineering) and lanceewing/hd6845sp | No licence stated (GitHub API: none) | No code or schematic copy. Facts only |
| H | ali1234/vhs-teletext | GPL-3.0 | No |
| I | jsbeeb, B-em, MAME | GPL-3.0 (jsbeeb confirmed by API); B-em GPL; MAME needs the ROM dump | No |

**Recommendation: A, Bedstead's `glyphs[]` table.** Reasons:
1. It is the only candidate that is machine-readable, complete (all 96 English codes plus the US set) and under an explicit, standard, permissive dedication (CC0). CC0 is compatible with MIT.
2. Its authors state the bitmaps are "as shown in the datasheet, and the English ones have been checked against a real SAA5050" [from S13 header comment]. That is the same source the design says to draw from, so there is no second guess to make.
3. It removes the transcription risk of hand-copying 96 x 9 rows from a bitmap figure.
4. The residual risk is the original Mullard typeface copyright. Bedstead relies on UK CDPA 1988 s.55 and says it is not legal advice. The same question applies if we draw the glyphs ourselves from Fig 11, so drawing them does not remove it.
How to use it: take the glyph row bytes and the code mapping only, never the C code. Keep a `NOTICE` with the CC0 header above and the s.55 sentence. Then add a test that spot-checks a few glyphs against the datasheet Fig 11 picture (for example `£`, `←`, `½`, `#`, `A`, `g`).
Not for me to decide (it is the repo owner's call): whether the s.55 position is acceptable for a public repo. I recommend yes, and recording the reasoning in the repo the way the ROM position is documented.

---

## 5. Timing numbers a test can assert

All from the ROM table values (3.1), with R7 = table + 1 and default `*TV 0,0`. Arithmetic mine; inputs [from ROM]; formulas [from S1, S2].

| | Modes 0, 1, 2 | Mode 3 | Modes 4, 5 | Mode 6 | Mode 7 |
|---|---|---|---|---|---|
| CRTC clock | 2 MHz | 2 MHz | 1 MHz | 1 MHz | 1 MHz |
| Chars per line (R0+1) | 128 | 128 | 64 | 64 | 64 |
| Line time | 64 us | 64 us | 64 us | 64 us | 64 us |
| CPU cycles per line (2 MHz) | 128 | 128 | 128 | 128 | 128 |
| Displayed chars (R1) | 80 | 80 | 40 | 40 | 40 |
| HSYNC starts at char (R2) | 98 = 49 us | 98 = 49 us | 49 = 49 us | 49 = 49 us | 51 = 51 us |
| HSYNC width | 8 chars = 4 us | 4 us | 4 chars = 4 us | 4 us | 4 us |
| Lines per char row | 8 | 10 (8 shown) | 8 | 10 (8 shown) | 20 per frame, 10 per field |
| Rows total (R4+1) | 39 | 31 | 39 | 31 | 31 |
| R5 adjust lines | 0 | 2 | 0 | 2 | 2 |
| Lines per field, non-interlaced (R8 = 0, `*TV x,1`) | 312 | 312 | 312 | 312 | not allowed |
| Lines per frame, default (R8 = 1 or &93) | 625 (312 + 313) | 625 | 625 | 625 | 625 |
| VSYNC start (R7 eff x lines per row) | line 280 | 280 | 280 | 280 | 280 |
| VSYNC width | 2 lines | 2 | 2 | 2 | 2 |
| Displayed lines per field | 256 | 200 (25 rows x 8 lit lines; RA3 blanks lines 8-9) | 256 | 200 | 250 |
| Field rate, non-interlaced | 50.08 Hz (19,968 us, 39,936 cycles) | same | same | same | n/a |
| Field rate, default interlace sync (VSYNC to VSYNC 312.5 lines) | 50.00 Hz (20,000 us, 40,000 cycles) | same | same | same | same |

- 312 x 64 us = 19,968 us = 39,936 CPU cycles = 50.08 Hz. [from S14 hoglet: "repeating the same 312-line field twice, giving a field rate of 50.08Hz"; S2 19.3]
- 625 lines per frame at 64 us = 40 ms, 25 Hz frames, 50 Hz fields. [from S2 19.3.1; S14 AUG quote: "All BBC microcomputer screen modes are interlaced sync only except for mode 7 which is interlaced sync and video"]
- Default is interlace ON for every mode: R8 = 1 in modes 0-6, &93 in mode 7. `*TV x,1` gives R8 = 0 in modes 0-6 only. [from ROM] + [from S14] + [from S7 ch5 s53]
- The 39,936-cycle frame applies only after `*TV 0,1`, which many games issue. The OS default is the 625-line interlaced-sync timing. [inferring from the lines above]
- VSYNC IRQ: CA1 on the VSYNC falling edge, interval 40,000 cycles (default) or 39,936 (no interlace). [from S7 ch10 + inference from S2 14.2]
- System VIA IFR bit 1 = vertical sync. IER default enables it. [from S7 ch3 s19, s20; ch10 s13]
- Frame counts the OS keeps: flash counters decrement on the VSYNC IRQ, 25 + 25. [from S7 ch11]
- Memory per mode, bytes: 20,480 / 20,480 / 20,480 / 16,384 / 10,240 / 10,240 / 8,192 / 1,024. [from ROM $C459]
- Bytes per scan line: 80 (modes 0-3), 40 (modes 4-7). [from S4]
- Character cells: 640 bytes per row in modes 0-3 (80 x 8), 320 in modes 4-6, 40 in mode 7. [from ROM $C463]
- Default mode at reset: from DIP switches, mode 7 with all switches open (3.2). The design's "boots in every video mode" needs a link-setting helper.
- Banner: "BBC Computer " at `$C304`, then memory size, then `>` prompt. [from ROM; the design marks the exact text for the plan to confirm]

---

## 6. What I could NOT establish

1. **Primary-source chip part for Model B IC2.** Only owner reports and the OS's use of VSYNC width and skew. No Acorn service manual or parts list was reachable.
2. **Exact HD6845S cycle behaviour** beyond the CAST/ACCC model: the char-clock at which each counter updates relative to the CPU write, MA during horizontal blanking, behaviour when R-values change mid-line, the exact VSYNC falling edge inside its last line, and which field gets the extra line. ACCC (S2) has CPC-specific detail per CRTC type (chapters 10-13, 19) and S19 lists real-hardware quirks. A logic-analyser trace or the HD6845S die-shot work (lanceewing/hd6845sp, unlicensed, facts only) would settle it.
3. **Video ULA pipeline latency.** How many chars or half-chars between the CRTC fetch and the pixel on screen, and how DISEN, CURSOR and the latch line up. Only the observable offsets (mode 7 one char right) are documented.
4. **How RA3 gating is switched off in mode 7.** It must be (see 1.5), but no source describes the gate.
5. **SAA5050 internals to cycle level:** exactly when the line counter increments, the double-height glyph row mapping (4.3), exact separated-graphics pixel positions, what a ULA-latched LOSE does at the start and end of the display window, and whether the black foreground codes (&80, &90) are truly no-ops on the BBC chip. The die-shot work (lanceewing/saa5050, no licence) and the beebjit teletext test disc referred to in S15 (not fetched, not checked for licence) are the places to look.
6. **Box codes and PO/DE tie-offs** on the BBC board.
7. **AUG pages not read.** The New Advanced User Guide tables (p187-190) and the BBC User Guide memory layout pages were not reachable, so section 3 is checked against the ROM, an annotated disassembly (S7, derived from the same ROM), Wikipedia, BeebWiki and the Bitshifters slides, not against the AUG.
8. **Reading write-only CRTC registers** on the real machine.
9. **HSYNC width 0** (R3[3:0] = 0). The datasheet says "can't be programmed". Variants differ (S2 14.1). No BBC mode uses it.
10. **Conventional factory DIP switch setting.** Mode 7 is likely but I read no source.
11. **Which stardot claims hold on real hardware:** the Hold Graphics release timing (2 cycles) is described by its author as possibly a simulation artefact.

---

## Sources

| Key | What | URL |
|---|---|---|
| S1 | Hitachi HD6845R/S datasheet, HTML transcription (text only, figures missing) | https://cpctech.cpcwiki.de/docs/hd6845s/hd6845sp.htm |
| S2 | Amstrad CPC Common CRTC Compendium v1.8 ("CRTC 0" = HD6845S) | https://www.cpcwiki.eu/imgs/4/4a/ACCC1.8-EN.pdf (read via web.archive.org copy) |
| S3 | CAST C6845 datasheet quotes (horizontal and vertical timing) | https://neuro-sys.github.io/2019/10/01/amstrad-cpc-crtc.html |
| S4 | BeebWiki, Video ULA | https://beebwiki.mdfs.net/Video_ULA (web.archive.org snapshot) |
| S5 | BeebWiki, Address translation | https://beebwiki.mdfs.net/Address_translation (web.archive.org snapshot) |
| S6 | BeebWiki, MODE 7 | https://beebwiki.mdfs.net/MODE_7 (web.archive.org snapshot) |
| S7 | tobylobster, annotated BBC OS 1.20 disassembly. Chapters 3, 4, 5, 6, 10, 11, 15, 17 | https://tobylobster.github.io/mos/mos/index.html |
| S8 | YazanMehyar FPGA-BBC-micro, VULA.txt (clone author notes) | https://raw.githubusercontent.com/YazanMehyar/FPGA-BBC-micro/master/docs/VULA.txt |
| S9 | Wise Owl, A Hardware Guide for the BBC Microcomputer | https://acorn.huininga.nl/pub/docs/manuals/Wise-Owl/A%20Hardware%20Guide%20For%20The%20BBC%20Microcomputer.pdf |
| S10 | ABUG Masterclass slides, intro to CRTC registers | http://abug.org.uk/wp-content/uploads/2021/01/Masterclass_210114_IntroductionToCRTCRegistersPart1.pdf |
| S11 | Signetics SAA5050/55 datasheet | http://www.elektronikjk.com/elementy_czynne/IC/SAA5050.pdf |
| S12 | ETSI EN 300 706 V1.2.1 | https://www.etsi.org/deliver/etsi_en/300700_300799/300706/01.02.01_60/en_300706v010201p.pdf |
| S13 | Bedstead, `bedstead.c` and page | https://bjh21.me.uk/bedstead/bedstead.c , https://bjh21.me.uk/bedstead/ |
| S14 | stardot, "Games turn off interlace mode" (quotes AUG 2.20 and 18.6) | https://stardot.org.uk/forums/viewtopic.php?t=13174 |
| S15 | stardot, "SAA5050 Reverse Engineering" | https://stardot.org.uk/forums/viewtopic.php?t=21608 |
| S16 | stardot, "MODE 7/75" | https://stardot.org.uk/forums/viewtopic.php?t=12436 |
| S17 | stardot, "MODE 7 Vertical Rupture" | https://stardot.org.uk/forums/viewtopic.php?t=12679 |
| S18 | stardot, "Beebem Mode 7 glitch" | https://stardot.org.uk/forums/viewtopic.php?t=15280 |
| S19 | stardot, "6845 Quirks (and FPGA implementation)" | https://stardot.org.uk/forums/viewtopic.php?t=22008 |
| S20 | Wikipedia, BBC Micro (mode table), raw | https://en.wikipedia.org/wiki/BBC_Micro |
| S21 | 8bs.com BBC Model B memory map | https://8bs.com/mag/32/bbcmemmap2.txt |
| S22 | theoddys.com, The 6845 CRTC (Acorn type 0/1/2 table) | https://theoddys.com/acorn/the_6845_crtc/the_6845_crtc.html |
| S23 | cpctech, 6845 CRTC types (HD6845S = type 0 in CPC numbering) | https://cpctech.cpcwiki.de/docs/crtcnew.html |
| S24 | Wikipedia, Motorola 6845 (MA row-start behaviour) | https://en.wikipedia.org/wiki/Motorola_6845 |
| ROM | `/tmp/bbcrom/os.rom`, sha-256 2d9fea69017864f6962704481829f95fee08446c8c3a13826d5d4e44000ac9de | local |

Working copies of every page and PDF above are in `/tmp/bbc-facts/src/` (not committed anywhere).
