# BBC Micro Model B bus, timing, CRTC, OS and reset: fact sheet

Written 2 Oct 2026 for `dbhq-uk/6502` issue #26. Nothing here is code from jsbeeb or B-em. No repo was edited.
Machine: Model B, OS 1.20, 8271 disc. ROMs: `/tmp/bbcrom/{os.rom,BASIC.ROM,DFS-1.2.rom}`. Their SHA-256 values match the design doc (checked).

## Source key

Tags are written `[from Sn ref]`. Each Sn is a URL.

| Tag | Source | URL |
|---|---|---|
| S1 | Advanced User Guide (Bray, Dickens, Holmes, 1983), OCR text | https://archive.org/details/bbc-micro-advanced-user-guide |
| S2 | Acorn BBC Micro Service Manual (Model B and B+ sections mixed; B+ parts flagged) | https://archive.org/details/bbc-micro-service-manual |
| S3 | A Hardware Guide for the BBC Microcomputer (Derrick), ch.3 and ch.4 | http://bbc.nvg.org/doc/A%20Hardware%20Guide%20for%20the%20BBC%20Microcomputer/bbc_hw_03.htm (and `bbc_hw_04.htm`) |
| S4 | MOS 1.20 annotated reassembly (Toby Nelson), assembles byte for byte to the original ROM | https://tobylobster.github.io/mos/ and https://tobylobster.github.io/mos/os120_acme.a |
| S5 | Stardot thread with real-hardware read tests (gfoot, 11 Aug 2021) | https://stardot.org.uk/forums/viewtopic.php?p=330803 |
| S6 | Godbolt, jsbeeb part 3: CPU timings (a blog post, read as a cross-check of the 2 or 3 rule) | https://xania.org/201405/jsbeeb-getting-the-timings-right-cpu |
| S7 | Steil, 6502 BRK/IRQ/NMI/RESET internals (Visual6502 trace) | https://www.pagetable.com/?p=410 |
| S8 | Wikipedia, Acorn MOS | https://en.wikipedia.org/wiki/Acorn_MOS |
| S9 | RetroBat wiki listing MAME `bbcb` romset hashes (seen in a search result only, page not opened) | https://wiki.retrobat.org/systems-and-emulators/supported-game-systems/home-computer/acorn-computers/bbc-micro |
| S10 | **jsbeeb, read only to cross-check a number** (GPL, nothing copied). Commit `e27b20d4a33c2a7b17d2cf830f4695e961b6846e`: `src/6502.js`, `src/video.js`, `src/models.js` | https://github.com/mattgodbolt/jsbeeb |
| ROM | Read directly from the files. File offset = CPU address - `$C000` for `os.rom`, - `$8000` for the others | local |

I wrote a throwaway disassembler at `/tmp/bbc-facts/tools/d6502.py` to read the ROMs. It is scratch, not in any repo. S4 was used to confirm what the bytes mean. B-em was not read.

---

## 1. CPU memory map

### 1a. Top level

| CPU range | What | Notes | Source |
|---|---|---|---|
| `$0000-$7FFF` | RAM, 32 KB (16 KB on a Model A) | No mirroring on a B. The OS detects a 16 KB machine because `$4000+` aliases `$0000+`, so a B must NOT alias | [from ROM `$D9E7-$D9FD`, S4 "clearRAM"] |
| `$8000-$BFFF` | Paged ROM, 16 KB window, one of 16 slots | Writes are ignored. Slot chosen by the ROM latch | [from S1 s.21, S3 3.4] |
| `$C000-$FBFF` | OS ROM | Read only | [from S2 3.2] |
| `$FC00-$FCFF` | FRED, 1 MHz bus page | | [from S1 s.28.2] |
| `$FD00-$FDFF` | JIM, 1 MHz bus page | | [from S1 s.28.3] |
| `$FE00-$FEFF` | SHEILA, on-board I/O | table 1c | [from S1 s.17 table p.358] |
| `$FF00-$FFFF` | OS ROM (jump table, vectors) | | [from ROM] |

- The OS ROM file is 16 KB but the CPU only sees 15.25 KB. Image offsets `$3C00-$3EFF` (CPU `$FC00-$FEFF`) are hidden by I/O. They hold the credits text starting "(C) 1981 Acorn Computers Ltd." at image offset `$3C00`. [from ROM; "3/4K left unused" S2 3.2]
- S3 3.4 says the hole is "1 kilobyte". That is wrong: `$FF00-$FFFF` is ROM. Trust S2 (768 bytes) and the ROM.
- ROM is disabled for all of `$FC00-$FEFF` and during write cycles. [from S2 3.2]
- Vectors (file offset `$3FFA`): NMI `$0D00`, RESET `$D9CD`, IRQ `$DC1C`. [from ROM, bytes `00 0d cd d9 1c dc`]
- The OS puts an RTI (`$40`) at `$0D00` as its first act, so a stray NMI does nothing. [from ROM `$D9CD`]

### 1b. Paged ROM select latch

| Item | Fact | Source |
|---|---|---|
| Address | Write `$FE30`. The whole block `$FE30-$FE3F` decodes to the same latch (any of 16 locations) | [from S1 s.17 note p.358, s.21] |
| Bits | D0-D3 = ROM number 0-15. D4-D7 ignored. Write only | [from S1 s.21 "4 bit write only register"; S3 3.4 "four least significant data bus lines"] |
| Chip | 74LS163 (IC76), loaded on write | [from S3 3.4, ch.4 link 20] |
| Sockets on the board | 4 sideways sockets IC52, IC88, IC100, IC101. S1 s.21 numbers them 12, 13, 14, 15 and says 15 is highest priority | [from S1 s.21] |
| Decode of the 4 sockets | Only the two low latch bits select a socket (74LS139 IC20 fed from the latch). So a socket answers in 4 slots (n, n+4, n+8, n+12) | [from S3 ch.4 links S20/S22; inferring the 4-slot aliasing] |
| The OS expects aliasing | At reset it compares the first 1 KB of each valid ROM with every higher slot and ignores the lower duplicate | [from ROM `$DAD1-$DAF9`, S4 "selectROMLoop"] |
| Which slot is BASIC and DFS | **Sources disagree.** S3 3.4 says BASIC is in IC52. S1 s.21 maps IC52 to slot 12. A stock B is known to run BASIC from the top slot. The OS picks the highest-numbered language ROM, so BASIC must be higher than any other language | see below |
| Recommended | **BASIC in slot 15, DFS in slot 14, slots 0-13 empty.** Cross-checked: jsbeeb loads BASIC at 15 then DFS at 14 for this exact model [S10 models.js, 6502.js `loadOs`]. No aliasing needs modelling, because the OS ignores lower copies | [inferring] |
| Header facts | BASIC: type `$60` (language, no service entry), copyright offset `$0E`, title "BASIC". DFS: type `$82` (service, 6502), copyright offset `$10`, title "DFS,NET", version byte `$83`. Both carry `00 28 43 29` ("\0(C)") at the copyright offset, which is what the OS tests | [from ROM] |
| Value after power on | Not established | see section 6 |
| Slot service order | Service calls go from slot 15 down to 0. Language entry = highest slot with bit 6 of the type byte set | [from S4 "osbyte143", "findBestLanguageROM"] |

### 1c. SHEILA, every device

The first four columns are [from S1 s.17 table p.358]. Read/write behaviour and mirrors are as marked.

| Range | Device | Registers and mirrors | Read/write notes | Source |
|---|---|---|---|---|
| `$FE00-$FE07` | 6845 CRTC | `$FE00` address register, `$FE01` data. A0 = RS. `$FE02-$FE07` mirror the pair | R0-R13 write only. R14-R17 read | [from S1 s.18.2; S2 IC39 note (B+ section, same decode): uses A3, A4 only] [inferring the pair mirror] |
| `$FE08-$FE0F` | 6850 ACIA | `$FE08` control (W) / status (R), `$FE09` TDR (W) / RDR (R). A0 = RS. Pairs mirror to `$FE0F` | | [from S1 s.20.4-20.7] [inferring mirror] |
| `$FE10-$FE17` | Serial ULA | `$FE10` control, write only. Mirrors to `$FE17` | | [from S1 s.17; S2 IC39 note (B+ section) "SERPROC (`$FE10`)"] |
| `$FE18-$FE1F` | Station ID (read) / INTOFF | Read `$FE18` = Econet station ID and disables Econet NMI | Not the ADC (that is the Master) | [from S2 3.9 (Model B section)] |
| `$FE20-$FE2F` | Video ULA | `$FE20` control register (W), `$FE21` palette (W). A0 selects. Pairs presumably mirror to `$FE2F` | Write only. A **read** of `$FE20` is INTON (Econet NMI enable) | [from S1 s.19; S2 3.9] [inferring mirror] |
| `$FE30-$FE3F` | ROM select latch | see 1b | Write only | [from S1 s.21] |
| `$FE40-$FE5F` | System VIA 6522 | 16 registers at `$FE40-$FE4F`. A4 not decoded, so mirrored at `$FE50-$FE5F` | | [from S1 s.22 "&40-&4F" heading, table "&40-&5F"] [inferring mirror] |
| `$FE60-$FE7F` | User VIA 6522 | 16 registers at `$FE60-$FE6F`, mirrored at `$FE70-$FE7F` | | [from S1 s.24; same inference] |
| `$FE80-$FE9F` | 8271 FDC | `$FE80` status (R) / command (W). `$FE81` result (R) / parameter (W). `$FE82` reset (W). `$FE83` unused. `$FE84` data (R/W), DMA acknowledge | `$FE84-$FE87` is the data/DACK block. `$FE88-$FE9F`: S2 (B+ section 5.5.1, same chip and wiring) splits the space in blocks of four on A2, so it probably repeats every 8 bytes | [from S1 s.25.1; S2 5.5.1] [inferring mirror] |
| `$FEA0-$FEBF` | 68B54 ADLC (Econet) | `$FEA0` CR1 (W) / SR1 (R). `$FEA1` CR2,3 (W) / SR2 (R). `$FEA2` TX/RX FIFO. `$FEA3` TX/RX FIFO (frame terminate). Mirrors every 4 bytes | | [from S1 s.25.2] [inferring mirror] |
| `$FEC0-$FEDF` | uPD7002 ADC | `$FEC0` data latch/start (W), status (R). `$FEC1` high byte (R). `$FEC2` low byte (R). `$FEC3` unused. Mirrors every 4 bytes | | [from S1 s.26] [inferring mirror] |
| `$FEE0-$FEFF` | Tube ULA | 8 registers at `$FEE0-$FEE7` (4 read latches, 4 write latches, status). Mirrors every 8 bytes | | [from S1 s.27.1] [inferring mirror] |
| `$FC00-$FCFE` | FRED | Devices: test `$FC00-0F`, teletext `$FC10-13`, Prestel `$FC14-1F`, IEEE488 `$FC20-27`, Cambridge Ring `$FC30-3F`, Winchester `$FC40-47`, test `$FC80-8F`, user `$FCC0-FE`. All optional | | [from S1 s.28.2] |
| `$FCFF` | JIM paging register | 0 on power up if fitted | | [from S1 s.28.2; S3 3.22] |
| `$FD00-$FDFF` | JIM, paged memory | one 256-byte page chosen by `$FCFF` | | [from S1 s.28.3] |

Notes on this table:
- jsbeeb, cross-check only: it routes the register blocks as 4-byte groups and sends all of `$FE40-$5F`, `$FE60-$7F`, `$FEC0-$DF` and `$FEE0-$FF` to their devices [S10 6502.js `readDevice`]. That agrees with the mirrors above. It does NOT send `$FE38-$FE3B` to the ROM latch on a Model B. S1 says all 16 addresses work. Trust S1.
- The Master uses `$FE18` for the ADC and `$FE30` onwards differently. Ignore for a Model B.

### 1d. What an unmapped access returns

Every address maps to something, so "unmapped" means: absent hardware, an empty ROM slot, or a write-only register read back.

| Case | Value | Source |
|---|---|---|
| Read FRED or JIM with nothing on the 1 MHz bus | `$FF`. The bus transceiver IC72 is on and passes pulled-up inputs | [from S5, measured: `$FC00` and `$FD00` read 255] |
| Read an absent **fast** (2 MHz) device, e.g. `$FE20`, `$FEA0` | `$FE`, the last value on the bus (high byte of the address just fetched) | [from S5, measured: `$FEA0`, `$FE20` read 254] |
| Read an absent **slow** (1 MHz) device, e.g. missing user VIA | `$00`. The weak pull-downs have time to discharge | [from S5, measured; the poster's explanation, one machine] |
| Same, rule for the emulator | FRED/JIM `$FF`; fast SHEILA = high byte of the address; slow SHEILA = `$00` | [inferring from S5] |
| Writes to absent hardware or to ROM | Ignored | [inferring] |

**The OS depends on this. Checked in the ROM, so the values above are not optional.**

| What the code does | Needs | Source |
|---|---|---|
| Tube probe: write `$81` to `$FEE0`, read `$FEE0`, ROR, branch if bit 0 clear means no Tube | Read has **bit 0 = 0**. `$FF` would make the OS think a Tube is fitted and start a Tube service call | [from ROM `$DB38-$DB41`; S4 "Initialise Tube"] |
| DFS service call 1: reads `$FEA0` and `$FEA1`, tests `AND #$ED` and `AND #$DB`; both zero means "Econet present" | At least one non-zero. `$FE` gives `$EC` and `$DA` so Econet is correctly absent. `$00` would switch the Econet code on | [from ROM `DFS-1.2.rom $8105-$8111`] |
| Start-up checks the user VIA is real: write `$0E` to `$FE6C`, read back, compare | User VIA PCR must read back what was written | [from ROM `$DA8F-$DA9F`] |

So `$FE` for absent fast SHEILA devices satisfies both probes. jsbeeb, cross-check only, returns `$FF` for FRED/JIM and the address high byte for other absent devices [S10 6502.js `readDevice`], which agrees.

---

## 2. The 1 MHz bus stretch

### 2a. Which devices are 1 MHz

| Speed | Devices | Source |
|---|---|---|
| **1 MHz (stretched)** | 6845 CRTC `$FE00-07`; 6850 ACIA `$FE08-0F`; serial ULA `$FE10-17`; both VIAs `$FE40-7F`; ADC `$FEC0-DF`; FRED `$FC00-FF`; JIM `$FD00-FF` | [from S2 3.1: "the 1 MHz extension bus, the ADC, the two VIA's, the 6845 CRT controller, the ACIA, and the serial processor"] |
| **2 MHz (not stretched)** | RAM, OS ROM, paged ROMs (standard config), video ULA `$FE20`, ROM latch `$FE30`, 8271 `$FE80-9F`, ADLC `$FEA0-BF`, Tube `$FEE0-FF` | [inferring: not in the S2 list; same split in S10 `is1MHzAccess`] |

Disagreements and gaps:
- S3 3.8 also lists "sideways ROMs (optional)". Those are links and diodes (S18, S19, D10-D12) that make a ROM slow for old EPROMs. Standard is fast. [from S3 ch.4 links 18, 19; S2 link survey]
- FRED/JIM are slow by default. Links S15/S16 can make them fast. [from S2 link survey 15, 16]
- `$FE18-$FE1F` (station ID / INTOFF) is in none of the lists. S10 treats all of `$FE00-$FE1F` as slow. Not established (section 6). Irrelevant while Econet is not modelled.

### 2b. The rule

Source for the mechanism: S2 3.1 and its Figure 1 "2-1 MHz stretching" (page 9, PDF page 19; I read the diagram), S3 3.8, S1 s.28.5. The 6502 clock is held high until the falling edges of the 1 MHz clock (1MHzE) and the 2 MHz clock coincide. S3 3.8 states the result: the 2 MHz clock to the CPU "will be held at logic '1' for either 3 or 5 half cycles" instead of 1.

Let T = the count of 2 MHz CPU cycles (500 ns each) since reset. Define the 1MHzE phase so that a 1 MHz cycle starts on **even T**. Then, for each bus cycle whose address is in a slow range:

| Cycle starts on | Total length of that cycle | Extra cycles (waits) | Source |
|---|---|---|---|
| even T (1MHzE has just fallen: "Case A") | 2 cycles | **+1** | [from S2 Figure 1 case A: 1 half low, 3 halves high] |
| odd T (1MHzE has just risen: "Case B") | 3 cycles | **+2** | [from S2 Figure 1 case B: 1 half low, 5 halves high; S6: "stretched to either 2 or 3 cycles"] |

In code: `wait = 1 + (T & 1)`. After any stretched cycle the next cycle starts on an even T (both cases end on a falling 1MHzE edge), so back-to-back slow accesses cost 2 each. [from S2 Figure 1; S6 worked example: first access 3 ticks, next two 2 ticks each]

More facts that matter:
- **Every bus cycle is judged separately by its own address**, including dummy reads and read-modify-write double writes. `STA abs,X`, `STA abs,Y`, `STA (zp),Y` do a dummy read at the unfixed address; `INC`/`ASL`/`ROL abs` etc. write twice. Each hit on a slow address is stretched. S6's example: `ROL $FE48` runs 6 CPU cycles in 10 2 MHz ticks. [from S6; stardot t=11528 (hoglet) via web summary, page not opened in full]
- **The access happens at the end of the stretched cycle**, when both clocks fall. So tick the wait cycles first, then do the read or write. This matches the design doc's "tick extra cycles before returning". [inferring from S2 Figure 1]
- The VIAs, ACIA and 6845 are clocked by 1MHzE, so a VIA timer counts once per 2 CPU cycles on the same phase. [inferring from S1 s.22 "decrements at the system clock rate (1 MHz)"]
- "Double access" (device hit twice, S1 s.28.5.2, S2 3.1 text) is described for devices on the **external** 1 MHz bus and is cured by the clean-up circuits in S1 s.28.5.3. Do not model it for on-board chips. A Stardot poster says CRTC double writes happen only on issue 3 and earlier boards (web summary only, not verified). [inferring]
- The 1MHzE divider is a flip-flop dividing 2 MHz by two (S2 3.1). So it can start in either phase at power-on. See section 6.
- RAM is never stalled by video. The CPU and CRTC use alternate clock phases. [from S1 s.18.1; S3 3.5]
- Cross-check, S10 only: jsbeeb adds `1 + ((cycles ^ currentCycles) & 1)` extra cycles for a slow access, which is the same 1-or-2 parity rule. I did not copy anything.

---

## 3. CRTC clock, pixel clock, bytes per row, register values

### 3a. Where the tables are in `os.rom`

| Table | CPU address | File offset | Contents |
|---|---|---|---|
| Video ULA control value per mode | `$C3F7` | `$03F7` | `9C D8 F4 9C 88 C4 88 4B` (modes 0-7) |
| Bytes per character cell, per mode | `$C3FF` | `$03FF` | `08 10 20 08 08 10 08 01` |
| Mode to CRTC set (0-4) | `$C440` | `$0440` | `00 00 00 01 02 02 03 04` |
| Last-register offset per set | `$C469` | `$0469` | `0B 17 23 2F 3B` |
| **CRTC R0-R11, five sets of 12 bytes** | **`$C46E-$C4A9`** | **`$046E-$04A9`** | below |
| Screen memory size (high byte) per set | `$C459` | `$0459` | `50 40 28 20 04` |
| Screen start (high byte) per set | `$C45E` | `$045E` | `30 40 58 60 7C` |
| System VIA port B writes for hardware scroll, bit 5 | `$C44B` | `$044B` | `0D 05 0D 05 04` |
| Same, bit 4 | `$C44F` | `$044F` | `04 04 0C 0C 04` |
| Boot message | `$C303` | `$0303` | section 4 |
| Font, 8 bytes per char, ASCII `$20-$7F` | `$C000-$C2FF` | `$0000-$02FF` | `$C000 + (ch-$20)*8` |

All of these match S4's labelled tables (`crtcRegisters0to11ForMODEs012` etc.). The R-values also match the S1 s.18 per-mode tables (R0, R1, R2, R3 low nibble, R4, R5, R6, R7, R9, R11). S1 has no printed R8 or R10 table; R8 matches its prose ("interlace sync only except mode 7: sync and video", one-character display delay and two-character cursor delay in mode 7).

### 3b. CRTC registers the OS programs (decimal; `$` = hex)

| Set | Modes | R0 | R1 | R2 | R3 | R4 | R5 | R6 | R7 | R8 | R9 | R10 | R11 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 0, 1, 2 | 127 | 80 | 98 | `$28` | 38 | 0 | 32 | 34 | `$01` | 7 | `$67` | 8 |
| 1 | 3 | 127 | 80 | 98 | `$28` | 30 | 2 | 25 | 27 | `$01` | 9 | `$67` | 9 |
| 2 | 4, 5 | 63 | 40 | 49 | `$24` | 38 | 0 | 32 | 34 | `$01` | 7 | `$67` | 8 |
| 3 | 6 | 63 | 40 | 49 | `$24` | 30 | 2 | 25 | 27 | `$01` | 9 | `$67` | 9 |
| 4 | 7 | 63 | 40 | 51 | `$24` | 30 | 2 | 25 | 27 | `$93` | 18 | `$72` | 19 |

R12/R13 (screen start) are written separately, not from this table. Modes 0-6: start address / 8, so mode 0-2 `$3000` gives R12=`$06` R13=`$00`; mode 3 `$4000` gives `$08/$00`; modes 4-5 `$5800` gives `$0B/$00`; mode 6 `$6000` gives `$0C/$00`. Mode 7: `R12 = (hi - $74) EOR $20`, R13 = lo, so `$7C00` gives R12=`$28`, R13=`$00`. [from ROM `$5700-` routine, S4 "setHardwareScreenOrCursorAddress", and the start table above; the arithmetic is mine]

### 3c. Clocks, pixels, bytes per row

| Mode | CRTC char clock | ULA control | ULA chars/line | Bits per pixel | Pixels per line | Pixel clock | Bytes per char row | Screen RAM |
|---|---|---|---|---|---|---|---|---|
| 0 | **2 MHz** | `$9C` | 80 | 1 | 640 | 16 MHz | 640 | 20 KB at `$3000` |
| 1 | **2 MHz** | `$D8` | 40 | 2 | 320 | 8 MHz | 640 | 20 KB at `$3000` |
| 2 | **2 MHz** | `$F4` | 20 | 4 | 160 | 4 MHz | 640 | 20 KB at `$3000` |
| 3 | **2 MHz** | `$9C` | 80 | 1 | 640 | 16 MHz | 640 | 16 KB at `$4000` |
| 4 | **1 MHz** | `$88` | 40 | 1 | 320 | 8 MHz | 320 | 10 KB at `$5800` |
| 5 | **1 MHz** | `$C4` | 20 | 2 | 160 | 4 MHz | 320 | 10 KB at `$5800` |
| 6 | **1 MHz** | `$88` | 40 | 1 | 320 | 8 MHz | 320 | 8 KB at `$6000` |
| 7 | **1 MHz** | `$4B` | 40 (teletext) | n/a | 40 chars | SAA5050 6 MHz | 40 (1 byte per char) | 1 KB at `$7C00` |

Sources and how each column was got:
- ULA control values: [from ROM `$C3F7`; same as S1 s.19.1.7 table]. Bit 4 = CRTC clock: 1 = 2 MHz (modes 0-3), 0 = 1 MHz (modes 4-7). Bits 3-2 = chars per line: 11=80, 10=40, 01=20, 00=10. Bit 1 = teletext. Bit 0 = flash. [from S1 s.19.1.1-19.1.4]
- Bits per pixel: [from ROM `$C414`: colours-1 = 1,3,15,1,1,3,1,0].
- Bytes per char row = R1 x 8 scanlines in modes 0-6: [from ROM `bytesPerRow` table 640/320/40 and S4]. Check: 25 or 32 rows x that = 16000, 20480, 10240, 8000 bytes, matching the memory sizes in S1 and S3 table 3.1.
- Pixels per line = ULA chars x 8: [inferring from S1 s.19.1.3]. Pixel clock = pixels per byte x byte rate (one byte per CRTC character clock): [inferring]. Consistent with "2 bytes per microsecond" in S3 3.6 for 2 MHz modes.
- SAA5050 6 MHz: [from S3 3.3].
- Modes 3 and 6 have 10 scanlines per row but 8 bytes per column. Scanlines 8 and 9 are blank spacing: S1 s.18.7 says R9 counts "scan lines per character row including spacing". How the hardware blanks them is not in the sources read. [from S1; mechanism not established]
- Timing: a line is (R0+1) chars: 128 x 0.5 us (modes 0-3) or 64 x 1 us (modes 4-7), so **64 us** in every mode. A frame is (R4+1) x (R9+1) + R5 = 312 lines in modes 0-6 (19.968 ms); mode 7 gives 312 per field if the raster counter steps by 2 in interlace-sync-and-video mode. [computed from the table; mode 7 reading is inferring]

### 3d. Video address and wrap (for the framebuffer reader)

- Hardware wrap: the OS writes system VIA port B (latch bits B4, B5) per mode so the video fetch wraps back into RAM. The subtraction equals the screen size: **mode 0-2 = 20 KB, mode 3 = 16 KB, modes 4-5 = 10 KB, mode 6 = 8 KB**. [from S1 s.23.2 table and S3 table 3.1 (as "add 12K, 16K, 22K, 24K")]
- **Sources disagree on the B5/B4 bits.** S1 s.23.2 prints (B5,B4) = 11 for modes 0-2 and 10 for modes 4-5. The OS ROM tables, which are what actually runs, write (B5,B4) = **10 for modes 0-2, 00 for mode 3, 11 for modes 4-5, 01 for mode 6**. Mode 7 writes B4 = 0 only. [from ROM `$C44B`, `$C44F`; S4 hardware scroll tables] Cross-check: jsbeeb's table of subtract amounts indexed by B5B4 is `[8,4,10,5]` units of 2 KB, i.e. 00 = 16K, 01 = 8K, 10 = 20K, 11 = 10K, which fits the ROM and not the S1 print [S10 video.js]. Trust the ROM. Treat S1's two rows as an error.
- Modes 0-6 video address = `((MA & $FF) << 3) | (RA & 7)` for the low part, with MA11-8 reduced by the wrap amount when MA12 is set. [inferring from S10 video.js `readVideoMem`; consistent with S1 s.18.10 "divide by 8"]
- Mode 7 (MA13 set): address = `(MA & $3FF)` plus `$7C00`; **on a Model B, when MA11 is clear it is `$3C00` instead** (a Model B quirk). The jsbeeb comment cites a Retro Software forum thread. I did not open that thread. [from S10 video.js comment only; guessing - verify]

---

## 4. OS ROM version and the reset text

### 4a. Which release

| Fact | Value | Source |
|---|---|---|
| Release | **MOS 1.20**, the final BBC Micro (Model B) OS. Series run 0.10 to 1.20 | [from S8; ROM string below] |
| Version string | `"OS 1.20"` at CPU `$E825` (file `$2825`), after `BRK` and error number `$F7`. That is OSBYTE 0 with X=0 raising error `$F7 "OS 1.20"` | [from ROM; S4 "osbyte0EntryPoint"] |
| Version byte | OSBYTE 0 with X non-zero returns **X = 1** ("1 meaning OS 1.20") | [from ROM `$E81E` LDX #$01; S4] |
| Second copy | `"\rOS 1.20\r"` at `$F0C1`, printed by `*HELP` | [from ROM] |
| Copyright text | `"(C) 1981 Acorn Computers Ltd.Thanks are due to the following contributors to the development of the BBC Computer ..."` at `$FC00`, file offset `$3C00`. It sits under the I/O hole, so no CPU access sees it | [from ROM] |
| Build date | **None in the ROM.** No date string, no version header word | [from ROM, searched all printable strings] |
| Identity | MD5 `0a59a5ba15fe8557b5f7fee32bbd393a` equals the MD5 S4 gives for its byte-for-byte rebuild of "OS1.20". SHA-1 `0d9bcaf6a393c9ce2359ed700ddb53c232c2c45d` equals MAME's `os12.rom` per S9. CRC32 `3c14fc70` is mine, not independently confirmed | [from S4; S9 (search snippet); computed locally] |
| New in 1.20 | OSRDRM (`$FFB9`) is new in MOS 1.20 | [from S4] |

So `roms/README.md` can say: the OS is MOS 1.20, the final Model B release, identical to the ROM S4 reassembles.

### 4b. What the OS prints at reset

The banner is three strings in the ROM. File offset `$0303`, CPU `$C303` (bytes: `0D "BBC Computer " 00 "16K" 07 00 "32K" 07 00 08 0D 0D 00`).

| Part | Bytes | When |
|---|---|---|
| `bootMessage` | `0D` `BBC Computer ` (13 chars, trailing space) `00` | always, unless a ROM suppresses it |
| memory | `32K` `07` or `16K` `07` | power on and CTRL-BREAK only. **Skipped on a soft BREAK** | 
| ending | `08 0D 0D` | always |

[from ROM; the conditions from S4 "Show bootup message" and ROM `$DB6C-$DB84`]

- Printed through OSASCI (`JSR $FFE3` in `$DEB4`), so each `0D` becomes a newline (LF CR). So: a newline, then `BBC Computer 32K`, a BEL (the start-up beep, via the sound chip), a backspace, then two newlines. [from ROM `$DEA9-$DEBA`]
- **How the memory size is formed.** There is no arithmetic. The text is a fixed `16K` or `32K`. The RAM clear loop at `$D9E7` writes zero through `$0400-$7FFF`; on a 16 KB machine `$4000+` aliases `$0000+`, which zeroes `$01` and ends the loop early. It stores the top RAM page (`$40` or `$80`) in `$028E`. The banner prints `32K` if bit 7 of `$028E` is set. [from ROM `$D9E7-$D9FD`, `$DB78-$DB7F`; S4 "clearRAM"] **The emulator therefore prints `32K` simply by having 32 KB of distinct RAM.**
- Default mode is 7. The OS reads eight keyboard-matrix "keys" 9-1 (row 0, columns 9 down to 1). Columns 2-9 are the DIP links and column 1 is CTRL. A set link reads bit 7 high. The start-up mode is the inverted links, bits 0-2 = columns 9, 8, 7. With all links open the mode is 7. [from ROM `$DA15-$DA3D`; S4 comments; "mode 7 by default" is also the known factory setting]
- Rows: the first newline moves the cursor to row 1, so `BBC Computer 32K` is on **row 1** (second row), row 0 is blank. [inferring from the 0D lead byte and OSASCI; not run]

Full screen after reset with these three ROMs, an 8271 fitted and no disc, in text rows (0 = top) [inferring, not run]:

| Row | Text | Printed by |
|---|---|---|
| 0 | (blank) | |
| 1 | `BBC Computer 32K` | OS `$DB6C-$DB82` |
| 2 | (blank) | OS banner ending |
| 3 | `Acorn DFS` | DFS service call 3, string at DFS `$B3B4` |
| 4 | (blank) | the string ends `0D 0D` |
| 5 | `BASIC` | OS, OSBYTE 142 prints the ROM title string at `$8009` |
| 6 | (blank) | two OSNEWL after the title |
| 7 | `>` and cursor | BASIC, `LDA #$3E : JSR $BC02` at BASIC `$8B06` |

**Corrected 2 Oct 2026, task 5, from running the ROMs: with no 8271 fitted the DFS prints nothing, so `BASIC` is on row 3 and `>` on row 5.** [from ROM `DFS-1.2.rom $B494-$B49A`, traced on the machine] Before it serves any service call, the DFS half of `DFS,NET` reads the 8271 status at `$FE80`, does `AND #$03` and returns untouched if the result is non-zero. An absent fast device reads `$FE` (section 1d), whose low bits are `10`, so the DFS takes the controller as missing; a fitted 8271 has status bits 0 and 1 unused and reading 0, which gives the table above. Rows 0, 1, 2 and 4 and the `BASIC` and `>` text were confirmed by running, with the 8271 absent; the `Acorn DFS` row stays [inferring] until the 8271 is modelled. The soft BREAK screen was also run: `BBC Computer` on row 1, then `BASIC` and `>` as above.

**Confirmed 4 Oct 2026, task 12, from running the ROMs with the 8271 fitted and the drive empty: the table above is the screen, in all eight modes.** The DFS's boot touches the controller with 27 status reads, three specifies (`35 0D 0C 0A C8`, `35 10 FF FF 00`, `35 18 FF FF 00`) and the mode register (`3A 17 C1`), and nothing that uses a drive, so an empty drive does not hold up the boot.

- `Acorn DFS`: DFS `$B4EB` compares the call number with 3, scans the keyboard (OSBYTE `$7A`), and if no key is down jumps to `$B3AF`, which prints the string inline (`JSR $9FF7`, text, then `0D 0D`). [from ROM `DFS-1.2.rom $B4EB-$B505, $B3AF-$B3BD`] I did not trace the dispatch that reaches `$B4EB`; that it is the auto-boot service call (3) is [inferring]. The DFS ROM title is `DFS,NET`, but the printed text is `Acorn DFS`. `DFS 1.20` is printed by `*HELP`, not at boot.
- The prompt is `>`, printed by BASIC before each line. Cursor after it. [from ROM]
- `BASIC` row: the title text is in BASIC's header at `$8009`, printed by the OS in `osbyte142EntryPoint`. [from ROM; S4]
- A soft BREAK prints `BBC Computer ` with no memory size. The language entry then uses the previous language. [from S4]

---

## 5. Reset

| Item | Fact | Source |
|---|---|---|
| Reset vector | `$FFFC/$FFFD` = bytes `CD D9` at file `$3FFC` = **`$D9CD`** | [from ROM] |
| CPU sequence | After RESET is released the Visual6502 trace shows: cycles 0-2 idle at `$00FF`; cycles 3-5 are three stack **reads** (`$0100`, `$01FF`, `$01FE`, R/W high, nothing written); cycle 6 reads vector low at `$FFFC`; cycle 7 reads vector high at `$FFFD`; cycle 8 is the first opcode fetch at the vector address. SP ends at `$FD`; the I flag becomes set. (Often quoted as "7 cycles": that counts cycles 1-7 of the sequence.) | [from S7, Visual6502 trace] |
| What the CPU sees at `$D9CD` | `A9 40 8D 00 0D 78 D8 A2 FF 9A AD 4E FE 0A 48 F0 09 ...` | [from ROM] |
| First act | `LDA #$40 : STA $0D00` (puts an RTI at the NMI entry), `SEI`, `CLD`, `LDX #$FF : TXS` | [from ROM `$D9CD-$D9D6`; S4 "Reset entry point"] |
| Power on versus BREAK | `LDA $FE4E` (system VIA IER) then `ASL`. A 6522 IER read always has bit 7 = 1, so after `ASL` zero means "all IER bits clear" = power on. Non-zero = BREAK | [from ROM `$D9D7-$D9DC`; S1 s.22 "bit 7 is then always read as a logic 1"] |
| How hardware makes the difference | A separate RC reset ("Reset A") clears only the **system VIA** at power on. BREAK (555 timer) resets the rest but leaves the system VIA | [from S2 3.1, 5.3] |
| Power on | RAM test and clear (`$0400-$7FFF`, skipping byte 0 of each page to protect `$0D00`), sets `$028E`. BREAK clears RAM only if the BREAK action (`$0258` >> 1) is 1 | [from ROM `$D9DE-$D9FD`; S4] |
| Order after that | System VIA setup (port B bits 0-3 out; writes 14 to 8 to port B); read keyboard links and CTRL; init OS vectors and variables from `$0200`; ACIA and serial ULA setup; disable then enable VIA interrupts (IER `$F2` = vsync, ADC, timer 2, timer 1); 100 Hz timer 1 = `$270E` (9998 us); user VIA presence test; ADC start; clear sounds; **scan all 16 ROM slots**; speech check; screen mode set; Tube probe; ROM service calls; **banner**; auto-boot service call; select language and enter it | [from ROM; S4 list at "Reset entry point"] |
| Interrupts the OS needs | System VIA CA1 = CRTC vertical sync (50 Hz); timer 1 free run at 100 Hz | [from ROM `$DA80-$DAA7`; S4] |
| Peripherals the OS touches at boot even though the design leaves them out | ACIA `$FE08`, serial ULA `$FE10`, ADC `$FEC0`, Tube `$FEE0`, Econet `$FEA0`/`$FEA1` (read by DFS). They must return the section 1d values | [from ROM] |

---

## 6. What I could NOT establish

1. **Absolute phase of 1MHzE after reset.** The rule above is relative: even T means aligned, by my definition. Which parity real hardware has at power on is set by a free-running divide-by-two flip-flop, so it may differ between boots. The OS does not care. Cycle-exact tests of VIA timers or CRTC writes will.
2. **Whether `$FE18-$FE1F` is stretched.** S2 does not list it; jsbeeb treats it as slow. No effect without Econet.
3. **Reads of write-only registers** (CRTC `$FE00` and R0-R13, video ULA, ROM latch, serial ULA read side) and **reads of an empty ROM slot**. S5 measures only `$FC00`, `$FD00`, `$FEA0`, `$FE20` and a missing user VIA. I gave rules (high byte for fast, `$00` for slow) but they are one poster on one machine.
4. **Power-on content of the ROM latch.** Not documented. The OS writes it before the first read of `$8000`.
5. **Exact 8271 mirror pattern** `$FE88-$FE9F`. Taken from the B+ section of S2 plus S1's register table. *(Task 12 built it as `disc.md` s1a puts it: the eight bytes `$FE80-$FE87` repeat to `$FE9F`, A2 choosing the data register. Nothing the DFS does reaches a mirror, so it is still not checked.)*
6. **Which chips BREAK resets,** beyond "everything except the system VIA". S2 5.3 does not list them.
7. **The rest of the boot screen was not run.** `Acorn DFS`, the blank rows, and the `BASIC` and `>` rows come from reading the ROMs. Not traced: whether DFS touches the 8271 at boot with no disc, and whether the Econet part of `DFS,NET` prints anything. *(2 Oct 2026, task 5: run with the 8271 absent; see the correction under section 4b. The DFS reads the 8271 status on every service call. The Econet half printed nothing.)* *(4 Oct 2026, task 12: run with the 8271 fitted; the DFS specifies it at boot and does not touch a drive, see section 4b.)*
8. **How scanlines 8 and 9 are blanked in modes 3 and 6.** Not in the sources I read.
9. **The mode 7 Model B `$3C00` addressing quirk** is from a jsbeeb comment only. The forum thread it cites was not opened.
10. **BASIC and DFS slots.** S1 and S3 disagree on which socket is which number. The 15/14 choice is a recommendation backed by the OS's selection rule and jsbeeb's config.
11. **Datasheets.** I could not get the MOS 6502 or Rockwell 6522 datasheets: `archive.6502.org` timed out, and the Princeton PDF has no text layer. I used S7 (Visual6502) for the reset sequence and S1 s.22 for the 6522 IER bit 7 rule. The datasheet statement that RES clears VIA registers was not read.
12. **OS 1.20 release date.** Not in the ROM, and S8 gives no date.
13. **Sources I could not reach:** `mdfs.net`, `beebwiki.mdfs.net` (connection refused or timed out from this machine) and `bbcdocs.com` (parked domain). The stardot hardware wiki pages were not found.
14. **Figure reading.** The "3 or 5 half cycles" rule is stated in S3 3.8 in words. The mapping of Case A to even T and Case B to odd T is from my reading of the S2 diagram, cross-checked with S6's example.
