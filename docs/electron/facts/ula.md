# Acorn Electron ULA, memory map, contention, video, interrupts, keyboard, sound, cassette and reset: fact sheet

Written 4 Oct 2026 for `dbhq-uk/6502` issue #51, before any code. Nothing here is code from Elkulator, elkjs, jsbeeb, B-em, MAME, ElectrEm or any other emulator. No repo file other than this one was edited.
Machine: Acorn Electron, OS 1.00, BBC BASIC II, no Plus 1, no Plus 3. ROMs: `/tmp/elkrom/{os.rom,basic.rom}`. Their SHA-256 values match the design doc (checked: `b63f851d...cdcda4` and `45bd55dc...132079`).

Tags follow the BBC sheets. `[from Sn ref]` was read directly. `[from ROM]` was read out of a ROM. `[from M]` was produced by running a throwaway model I wrote (see the key). `[inferring ...]` joins dots. `[guessing - verify]` is a flag for a correction. Where sources disagree, the sheet says which it trusts.

## Source key

Tags are written `[from Sn ref]`. Each Sn is a URL.

| Tag | Source | URL |
|---|---|---|
| S1 | Acorn Electron Advanced User Guide (Dickens and Holmes, Adder Publishing, ISBN 0 947929 03 7, first published Sep 1984). The copy read is the **fifth edition, Nov 2023**, revised by stardot.org.uk members, so its ULA chapters carry post-1984 measurements (for example the RTC timing to the microsecond). The authors are Dickens and Holmes, not Bray. OCR text, with the keyboard and palette figures read as page images | https://archive.org/details/acorn-electron-advanced-user-guide |
| S2 | Acorn Electron Service Manual (Acorn, 1983). Primary source for the hardware. Identical scans at `acorn-electron-sm_202010` and `manualzilla-id-6038054` | https://archive.org/details/acorn-electron-sm |
| S3 | Thomas Harte (ElectrEm author), "Electron Technical Information", second draft. **Not gospel, by its own words**: it records what he assumed to make an emulator run 99% of software. `mdfs.net` refused connections from this machine, so it was read from a Wayback snapshot | https://mdfs.net/Docs/Comp/Electron/Techinfo.htm (read as https://web.archive.org/web/2016id_/http://mdfs.net/Docs/Comp/Electron/Techinfo.htm) |
| S4 | Stardot thread "Electron Memory Contention", pages 1 and 2. David Banks (hoglet) with a scope and a logic analyser on a real Issue 2 Electron, 5 to 7 Sep 2015, and Harte's confirmation of the model, 11 Mar 2016. Two short routines from Elkulator and elkjs are quoted in a post there: **read only to cross-check, nothing copied** | https://stardot.org.uk/forums/viewtopic.php?t=10069 (and `&start=30`) |
| S5 | Stardot thread "Exact timing of Acorn Electron" (0xC0DE, hoglet, May 2025). Frame, field, RTC and display-end timing, checked against hoglet's 16 MHz logic analyser captures in modes 4 and 6 | https://stardot.org.uk/forums/viewtopic.php?t=31017 |
| S6 | Stardot thread "Electron ULA to RAM timing" (hoglet, Mar 2021). 16 MHz clock, RAM cycled at 4 MHz | https://stardot.org.uk/forums/viewtopic.php?t=21962 |
| S7 | Stardot thread "New hardware trick: vertical rupture on the Acorn Electron" (0xC0DE, hoglet, Jul 2020). Line counter detail and a 63-cycle raster loop | https://stardot.org.uk/forums/viewtopic.php?t=20004 |
| S8 | Stardot thread "Electron ULA Basic Board", page 2 (hoglet, 23 Jun 2020). The real ULA slows to 1 MHz to read the keyboard | https://stardot.org.uk/forums/viewtopic.php?t=19463&start=30 |
| S9 | Stardot thread "Speed of zero page vs Electron sideways RAM" (Apr 2025). Zero page is dynamic RAM, so 1 MHz | https://stardot.org.uk/forums/viewtopic.php?t=30959 |
| S10 | **hoglet67/ElectronFpga `src/common/ElectronULA.vhd`, read only to cross-check a number** (GPL-3.0, nothing copied, nothing translated). It is a re-implementation of the real ULA, tuned on real hardware (see S4 post 19) | https://github.com/hoglet67/ElectronFpga |
| M | A throwaway model I wrote from scratch in C, `/tmp/elk-facts/tools/elk.c`, scratch, not in any repo: a bus-cycle NMOS 6502 and a minimal ULA (ROM paging, keyboard matrix, RTC and display-end interrupts, the contention rule of section 4). Used to read the boot screen out of the ROMs and to test the contention rule against seven real-hardware timings (section 4g). It was written from the rules in this sheet, not from S10 or any emulator | local |
| ROM | Read directly from the files. File offset = CPU address - `$C000` for `os.rom`, - `$8000` for `basic.rom`. I wrote a throwaway disassembler, `/tmp/elk-facts/tools/d6502.py`, to read them | local |

**Paths under `/tmp` are the working files of the session that wrote this sheet and were not kept.** The sources they name are public.

**Cross-checks that were not independent.** The "a RAM access finishes only on a 1 MHz boundary" rule is in S1 15.3.2 and S3 as prose. The way a stalled access is released (only on a boundary, with the wait decided at each boundary) and the 1 MHz treatment of I/O accesses are the points where I leaned on S10. Both are called out where they appear.

## Five facts that change how the machine must be built

1. **Every access to RAM, in every mode, is a 1 MHz access.** It costs 2 cycles of 2 MHz if the previous bus cycle ended on a 1 MHz boundary and 3 if it did not. Only ROM (the OS ROM and the paged ROMs) runs at 2 MHz, one cycle an access. So the core must expose every bus cycle with its address, **dummy reads included**: my first model left out the dummy stack read in `RTS` and `RTI` and ran 1.7% fast in all seven modes (section 4g).
2. **In modes 0 to 3 the CPU cannot complete a RAM access during the 40 microsecond display window of an active line.** 40 of the 64 one-microsecond boundaries in each contended line are blocked. Modes 0, 1, 2 block 256 lines per field, mode 3 blocks 200 (not the two blank lines under each text row), modes 4, 5, 6 block none. Over a frame the CPU can complete 19,520 RAM accesses in modes 0 to 2, 24,000 in mode 3 and 40,000 in modes 4 to 6.
3. **There is no keyboard ROM image.** ROM slots 8 and 9 are the keyboard matrix itself, decoded by the ULA: address lines A0 to A13 select columns and D0 to D3 return the keys. BASIC fills slots 10 and 11 (one chip, both slots). The OS ROM and `basic.rom` are all a stock machine needs.
4. **The ROM select register at `$FE05` shares its bits with the interrupt clear bits**, and rejects writes that would select slots 0 to 7 while slots 8 to 11 are paged in. The OS selects a ROM by writing `$0C` and then the number, every time.
5. **A frame is exactly 80,000 cycles of 2 MHz: two fields of 312 and 313 lines of 128 cycles.** The RTC and display-end interrupts fall at fixed positions in it, and the OS builds its 100 Hz clock from both. The machine boots to mode 6, with `Acorn Electron` and a bell glyph on row 1, `BASIC` on row 3 and the `>` prompt on row 5.

---

## 1. CPU memory map

### 1a. Top level

| CPU range | What | Notes | Source |
|---|---|---|---|
| `$0000-$7FFF` | RAM, 32 KB, four 64 Kbit DRAMs (ICs 4 to 7) | **No mirroring.** Each chip holds two bits of every byte and the bus to the ULA is **4 bits wide**, so one byte takes two accesses (two CAS pulses in one RAS cycle). The OS does no size test: its reset RAM clear writes `$0400-$7FFF` unconditionally | [from S2 5.1; from ROM `$D8FB-$D90C`] |
| `$8000-$BFFF` | Paged ROM window, 16 KB, one of 16 slots chosen by `$FE05` | On a stock machine: slots 8 and 9 are the keyboard, slots 10 and 11 are BASIC, all other slots are empty (expansion). Writes are ignored | [from S1 14.1, 14.2, 15.4] |
| `$C000-$FBFF` | OS ROM, 16 KB image | Read only | [from S1 ch.12; from ROM] |
| `$FC00-$FCFF` | FRED, 1 MHz bus page | Nothing on a stock machine. The Plus 1 puts the ADC at `$FC70` (status `$FC72`) and the printer port at `$FC71`; `$FC73` is a strobe on the expansion connector for a paging register; the Plus 3's 1770 is at `$FCC4-$FCC7`; the Tube at `$FCE0` | [from S1 15.5, 15.4 and Appendix F] |
| `$FD00-$FDFF` | JIM, 1 MHz bus page, 256-byte window chosen by the paging register at `$FCFF` | Nothing on a stock machine | [from S1 15.5] |
| `$FE00-$FEFF` | SHEILA. **Only the ULA, and it appears in every 16-byte block** (`$FE02` is the same as `$FEA2` or `$FE32`) | Section 1c | [from S1 ch.13 "SHEILA and the ULA"; S3] |
| `$FF00-$FFFF` | OS ROM (jump table, vectors) | | [from ROM] |

- The OS ROM image is 16 KB but the CPU sees 15.25 KB. Image offsets `$3C00-$3EFF` (CPU `$FC00-$FEFF`) are hidden by I/O. They hold the credits text starting `(C) 1983 Acorn Computers Ltd.Thanks are due to the following contributors to the development of the Electron ...`. [from ROM; S1 ch.12 "made inaccessible by the switch to memory mapped I/O"]
- Vectors at `$FFFA` (file offset `$3FFA`): NMI `$0D00`, RESET `$D8D2`, IRQ `$DAE7`. [from ROM, bytes `00 0D D2 D8 E7 DA`]
- The OS stores an RTI (`$40`) at `$0D00` as its first act at reset, so a stray NMI does nothing. [from ROM `$D8D2-$D8D4`]
- The design doc says the reset vector is `$D8D2`. That is correct. [from ROM]

### 1b. What an unmapped or write-only read returns

| Case | Value | Source |
|---|---|---|
| Read of a write-only ULA register (`$FE01`-`$FE03`, `$FE05`-`$FE0F`) | S3 says the underlying ROM byte. S10 disables the ROM select over `$FC00-$FEFF` (ROM enable only for `$C000-$FBFF` and `$FF00-$FFFF`), so the bus would float. **Not settled** | see section 12 |
| Read of FRED or JIM with nothing fitted | Not measured on an Electron | see section 12 |
| Read of `$8000-$BFFF` with an empty slot paged | Not measured | see section 12 |
| Writes to ROM, to absent hardware | Ignored | [inferring] |

**The OS does not depend on any of these.** [from M] I booted the OS and BASIC with empty slots and absent hardware returning `$00`, `$FF` and the high byte of the address in turn. All three boot to the same screen. The two probes the OS makes are safe for any constant:
- The Tube probe writes `$81` to `$FCE0`, reads it back and rotates bit 0 into carry; if bit 0 is clear it skips. If bit 0 is set it writes `$01` and reads again, and skips if bit 0 is still set. A constant read always skips. [from ROM `$D9F6-$DA16`]
- The ROM scan reads `$8006` and `$8007` of each slot and compares the bytes at the copyright offset with `00 28 43 29` (`\0(C)`). Open bus never matches. [from ROM `$D98B-$D99D`, string at `$DC97`]

### 1c. The ULA registers

All are at `$FE0n`, mirrored through the whole of `$FE00-$FEFF` (the top nibble of the low byte is ignored). [from S1 ch.13; S3]

| Reg | Write | Read | Source |
|---|---|---|---|
| `$FE00` | Interrupt enable, bits 6 to 2 only (bits 1 and 0 and 7 have no effect) | Interrupt status. Bit 7 always 1, bit 0 master IRQ, bit 1 power-on flag, bits 6 to 2 the five sources | [from S1 14.1 fig 14.1; S3] |
| `$FE01` | Unused | | [from S1 ch.13] |
| `$FE02` | Screen start address, low part: bits 7 to 5 = A8, A7, A6 | | [from S1 14.1 fig 14.2] |
| `$FE03` | Screen start address, high part: bits 5 to 0 = A14 to A9 | | [from S1 14.1 fig 14.2] |
| `$FE04` | Cassette data shift register | Same register | [from S1 14.1 fig 14.3] |
| `$FE05` | Interrupt clear (bits 7 to 4) and ROM select (bits 3 to 0) | | [from S1 14.1 fig 14.4] |
| `$FE06` | The counter: sound pitch, or tape baud setting | | [from S1 14.1 fig 14.5] |
| `$FE07` | Control: comms mode, display mode, motor, caps lock LED | | [from S1 14.1 fig 14.6] |
| `$FE08-$FE0F` | Palette | | [from S1 14.1 fig 14.7] |

The OS never reads any register except `$FE00` and `$FE04`. [from ROM, scan of every absolute reference to `$FE0n`]

---

## 2. Paged ROM select

### 2a. The register

| Item | Fact | Source |
|---|---|---|
| Address | `$FE05`, shared with the interrupt clear bits | [from S1 14.1] |
| Bits 3 to 0 | Slot number 0 to 15 | [from S1 14.1] |
| Bits 7 to 4 | Write a 1 to clear: bit 7 NMI, bit 6 high tone, bit 5 RTC, bit 4 display end | [from S1 fig 14.4] |
| The acceptance rule | **If the slot now paged in is 8 to 11, a write is honoured only if its bit 3 is set** (that is, only 8 to 15 can be selected). If the slot now paged in is 0 to 7 or 12 to 15, any write is honoured | [from S1 14.1 "Paging ROMs", S3, ROM; agrees with S10] |
| Why | An interrupt clear writes `$10`, `$20` or `$40`, whose low nibble is 0. With BASIC (10 or 11) paged in, the write must not drop to slot 0 | [inferring from S3] |
| Side effect | From slots 0 to 7 or 12 to 15 the same clear write **does** change the slot to 0. S1 warns of it | [from S1 14.1] |
| How the OS selects a slot | `$E39F` stores the number in `$F4`, writes `$0C` to `$FE05`, then writes the number. The `$0C` first puts the register in the accept-anything state even when 8 to 11 is paged | [from ROM `$E39F-$E3B0`] |
| Slots 12 to 15 | Writing `$0C-$0F` selects slot 12 to 15 **and changes the register so that it accepts a second write that can select any slot 0 to 15.** The "full" register that decodes all 16 slots is in the Plus 1, not the ULA | [from S1 14.1, 15.4] |

### 2b. What is in each slot on a stock machine

| Slot | Contents | Source |
|---|---|---|
| 0 to 7 | Empty. Acorn allocated: 0 and 1 SK2, 2 and 3 SK1 (Plus 1), 4 disc, 5 and 6 user, 7 modem | [from S1 15.4] |
| **8 and 9** | **Keyboard, in hardware. One device, appearing in both slots** | [from S1 14.1, 14.2] |
| **10 and 11** | **BASIC, one chip appearing in both slots** | [from S1 14.1] |
| 12 | Plus 1 operating system | [from S1 15.4] |
| 13 | High-priority slot in the expansion module | [from S1 15.4] |
| 14 | Econet | [from S1 15.4] |
| 15 | Reserved | [from S1 15.4] |

### 2c. The open question in the spec: is a separate keyboard ROM image needed

**No. The keyboard code is in the OS ROM and the keyboard is hardware.** Slots 8 and 9 do not hold an image at all.
- The OS ROM scans the keyboard by writing `$08` to `$FE05` and then reading addresses such as `$A000`, `$9FFF`, `$AFFF`, `$BBFF`, `$BDFF`, `$BEFF`: bits 0 to 3 of the byte are the key states. [from ROM `$ED76-$EDD2`]
- The OS ROM carries the key-to-code table at `$EDD3-$EE0A` (section 7b). [from ROM]
- The Service Manual's keyboard schematic has the 14 diodes and four pulled-up data lines that make the same thing in hardware. [from S2 "Keyboard matrix" appendix]
- Read it as: the keyboard is a read-only device whose value is a function of the low address bits, not a ROM.

**To boot to BASIC with only `os.rom` and `basic.rom`, fill slot 10 and slot 11 with `basic.rom`, and make slots 8 and 9 the keyboard matrix.** Slots 0 to 7 and 12 to 15 stay empty. [from M] I tested these variants. All print the same screen unless noted:
- BASIC in 10 and 11: slot 11 is chosen as the language.
- BASIC in 10 only: boots, slot 10 is the language.
- BASIC in 11 only: boots, slot 11.
- No BASIC anywhere: row 3 prints `Language?` and there is no prompt. The text is at `$DAA6`. [from ROM]

How the OS copes with the alias. It scans slots 0 to 15 in order. For each valid ROM it compares the first 1 KB with every higher slot and ignores the copy if they match, so the lower of two identical slots is dropped. A ROM whose type byte passes `AND #$8F` = 0 (BASIC's `$60` does) is recorded at `$024B`, and the last one recorded, the highest slot, is the language the OS enters. So aliasing slot 10 into slot 11 needs no modelling in either direction. [from ROM `$D98B-$D9E3`; from M]

BASIC's header: type `$60`, copyright offset `$0E`, title `BASIC`, `(C)1982 Acorn`. [from ROM `basic.rom` bytes `$8000-$801D`]

---

## 3. Clocks

| Clock | Value | Source |
|---|---|---|
| Master | **16 MHz** crystal (IC8). It is also an input to the ULA (`CLOCK IN`, pin 49) and is on the expansion connector | [from S2 5.1, 6; S6] |
| CPU clock, `PHI OUT` | Derived by the ULA from the 16 MHz. Three speeds: 2 MHz, 1 MHz, stopped | [from S2 5.1] |
| CPU | 6502A, a Synertek SY6502A. Chosen, S3 says, because it survives a stopped clock for 40 µs without losing its state. NMOS, so the core's `Nmos6502` variant | [from S3; S2 5.1] |
| 2 MHz period | 500 ns = 8 ticks of the master | [inferring] |
| 1 MHz boundaries | One every 2 cycles (1 µs). The ULA divides the 16 MHz by 16 | [inferring] |
| RAM cycle | 500 ns RAS cycles alternating **CPU, VDU, CPU, VDU** (S2 fig 1). Each carries two CAS pulses (two nibbles = one byte) | [from S2 5.1 fig 1] |
| Divide-by-13 clock | 16 MHz / 13 = 1.2308 MHz (812 ns), made outside the ULA and fed in at the `÷13 IN` pin. It is also on the expansion connector. Divided by 1024 it gives **1201.9 baud**, the tape rate | [from S2 6, 8.8; S1 15.2 "16/13 MHz"] |
| ULA clocks the RAM at | 4 MHz (one nibble access per 250 ns) | [from S6] |

### 3a. CPU clock rule, access by access

| Access | Clock | Source |
|---|---|---|
| ROM, including the OS ROM, A15 = 1 and not I/O | **2 MHz**: `PHI OUT` low 250 ns, high 250 ns. One cycle | [from S1 15.3.1; S2 5.1] |
| RAM, A15 = 0, **in every mode** | **1 MHz.** Always at least 2 cycles | [from S4 hoglet 6 Sep 2015 10:53, scope; S9] |
| RAM, modes 0 to 3, during the display window of an active line | **Stopped** until the window ends, then 1 MHz | [from S1 15.3.2; S2 5.1; S4] |
| ULA registers, `$FC00-$FDFF`, and the keyboard (slots 8, 9) | 1 MHz, with no contention. The 6522 and other FRED devices are named by S1 as "peripherals" that need the slow down | [from S1 15.3.2; S8; S10] |
| Everything above is judged **by the address on the bus** in that cycle, reads and writes alike, including dummy reads and the second write of a read-modify-write | | [inferring from S2 5.1, S10] |

**Sources disagree on the ULA registers and the keyboard.** S3 says the CPU runs at 2 MHz for ROM, the keyboard and the ULA registers. S8 (hoglet, real Electron) says "the real ULA slows down to 1MHz when accessing the keyboard; this was not widely understood". S10 puts `$FC`, `$FD`, `$FE` and the keyboard together as "IO accesses always happen at 1MHz (no contention)". **I trust S8 for the keyboard and S10 for the rest.** The effect on any timing is below a hundredth of a second in the BASIC loop (section 4g, `io1` case), so this is a fidelity choice and not a boot one. The ULA register part is [guessing - verify] (section 12).

### 3b. Pixel clock per mode

The ULA fetches one byte at a time and shifts it out at the pixel clock. [from S1 14.1, App C; inferring the clock from pixels per line and 40 µs]

| Mode | Pixels per line | Bits per pixel | Pixel clock | One byte per |
|---|---|---|---|---|
| 0 | 640 | 1 | 16 MHz | 0.5 µs |
| 1 | 320 | 2 | 8 MHz | 0.5 µs |
| 2 | 160 | 4 | 4 MHz | 0.5 µs |
| 3 | 640 | 1 (text only) | 16 MHz | 0.5 µs |
| 4 | 320 | 1 | 8 MHz | 1 µs |
| 5 | 160 | 2 | 4 MHz | 1 µs |
| 6 | 320 | 1 (text only) | 8 MHz | 1 µs |

---

## 4. Contention

### 4a. What the sources say, exactly

- The RAM is four bits wide, so reading or writing a byte needs two accesses. [from S2 5.1]
- RAM timing alternates one 500 ns RAS cycle for the CPU and one for the VDU, so the CPU has at most one RAM slot per microsecond whatever the mode. [from S2 5.1 fig 1]
- Modes 4 to 6 need 40 bytes per line, one byte per microsecond, so the VDU takes only its own slot and the CPU keeps the other. [from S2 5.1, S1 15.3.2]
- Modes 0 to 3 need 80 bytes per line. That is both slots, so the ULA takes the CPU's slot as well and the CPU is stopped. [from S1 15.3.2; S2 5.1 "uses all the available memory time slots"]
- **"The processor is denied RAM access during 40 microseconds of each 64 microseconds of the 256 lines in 312 which is the display period, and it is made to wait for RAM access until the end of the period."** [from S2 5.1]
- S1 gives the same, "the ULA will hold its clock high for up to 40 µs", and adds that an NMI gives the 6502 priority over the ULA for RAM, which makes snow on the screen. [from S1 15.2 pin 14, 15.3.2]
- Measured on a real Issue 2 Electron with a scope: RAM accesses always drop to 1 MHz. In modes 4 to 6 they are never extended and take exactly 1 µs (clock low 0.25 µs, high 0.75 µs). In modes 0 to 3 they are extended when they fall in the visible part of a line, and **the longest the clock is ever held high is 41.25 µs, making a 41.5 µs cycle (83 cycles of 2 MHz)**. [from S4 hoglet 6 Sep 2015 10:53]
- The transition from 2 MHz to 1 MHz is either 250 ns low then 750 ns high (a 2-cycle access) or 250 ns low then 1250 ns high (a 3-cycle access), depending on the relative phase of the 1 MHz and 2 MHz clocks. [from S1 15.3.2 fig 15.1]
- Harte, who first had the cost as 1 or 2 cycles, corrected it to **2 or 3**. [from S4 Harte 11 Mar 2016]
- A model with 80,000 cycles per frame in two fields, 2 MHz for the high 32 KB, 1 MHz plus a possible lost 2 MHz cycle for RAM, and a stopped clock for RAM accesses during the pixel part of lines in modes 0 to 3, "exactly reproduces the timing results" of the BASIC loop below. [from S4 Harte 11 Mar 2016]
- A 6502 loop of nominally 63 cycles that runs partly in mode 3, so that it collides with the active line, self-synchronises to the scanline and ends up with exactly N = 1 extra cycle of contention, 64 in all, one 64 µs line. [from S7 hoglet 21 Jul 2020]

**The sources give the rule and its totals. They do not publish the exact cycle at which the window starts relative to the 1 MHz clock.** I show in 4f that no total depends on that.

### 4b. The rule as a model

Time T counts 2 MHz cycles (500 ns). A bus cycle starts at T0, the time the previous one ended. A "1 MHz boundary" is a time with a fixed parity of T (call the parity p; it is not known, see 4f). `line(B)` and `pos(B)` give the display line and the cycle within the line (0 to 127) at time B.

```
completes(T0, address):
  if address is ROM (A15 = 1, not FC/FD/FE, not keyboard):   return T0 + 1
  B = first time >= T0 + 2 with parity p                     # first boundary, but never T0 + 1
  if address is RAM:
      while blocked(B):  B += 2
  return B                                                  # I/O: no blocking

blocked(B) = mode <= 3
             and line(B) is a contended line
             and pos(B) falls in the 80-cycle display window
```

Reading this:
- A RAM or I/O access that starts on a boundary takes **2 cycles**. One that starts one cycle off a boundary (after an odd number of ROM cycles) takes **3**. In code, the same shape as the BBC's slow device rule: `wait = 1 + (parity of T0 vs p)`. [from S1 15.3.2; S4 Harte; from M]
- The boundary at T0 + 1 is never used, even if it has the right parity. That is why the cost is 2 or 3 and not 1 or 2. [from S4 Harte 11 Mar 2016; M reproduces hoglet's 9 µs loop with it, section 4g]
- A boundary is blocked if the display is inside its window: **40 boundaries per contended line**, whatever the window's exact start (4f). [from M]
- The wait is decided at the boundary, so an access that starts before a window and still needs a boundary after it is stopped, and an access that starts inside a window waits for the window's end. [inferring; the release-on-boundary shape is in S10]
- Writes follow the same rule as reads. [inferring from S10]

### 4c. Which lines are contended

| Mode | Active display lines per field | Contended lines per field | Notes | Source |
|---|---|---|---|---|
| 0, 1, 2 | 256 | **256** | Every active line | [from S2 5.1; S1 15.3.2] |
| 3 | 250 (25 rows of 10 lines) | **200** | The 8 data lines of each row. **Not the 2 blank lines at the bottom of each row**, which fetch nothing | [from M: contending them gives 14.34 s in the BASIC loop against 11.92 s real; hoglet suspected the same on 5 Sep 2015, S4 post 1, "the non-active lines between rows in Mode 3"; S10] |
| 4, 5, 6 | 256 (4, 5), 250 (6) | **0** | RAM still at 1 MHz | [from S4 scope] |

Both fields use the same lines. The first active line is line 0.

### 4d. The fetch pattern within a line

| Modes | Bytes per line | Fetch rate | Slots the VDU takes | CPU slots left |
|---|---|---|---|---|
| 0, 1, 2, 3 | 80 | 1 byte per 500 ns for 40 µs | **Both slots** of every microsecond of the window | none until the window ends |
| 4, 5, 6 | 40 | 1 byte per 1 µs for 40 µs | One slot per microsecond | The other one, all the time |

Per line, 128 cycles (64 µs) of 2 MHz [from S5; S1 15.3.2]:
- **Display window, 40 µs = 80 cycles.** The ULA fetches the line's bytes. Modes 0 to 3 stop the CPU's RAM access here.
- **The rest, 24 µs = 48 cycles:** front porch about 8 µs, horizontal sync about 4 µs, back porch about 12 µs. [from S1 14.1 scope text; S10 gives 8, 4, 12 µs] The CPU gets every microsecond boundary: **24 RAM accesses** per contended line.
- Lines that are not contended (vertical blanking, the two blank lines under each row in mode 3, every line in modes 4 to 6): all 64 boundaries are free, **64 RAM accesses** per line.

How the VDU address is formed (section 5e). The very first byte of the window is fetched at the start of the line. How many cycles before the pixel it is drawn, and so the exact offset of the window against the first pixel, is not known (section 12). It does not change any count here.

### 4e. Totals per field and per frame

A frame is 80,000 cycles, which is 40,000 boundaries. [from S5; S4 Harte]

| Mode | Contended lines per frame | Blocked boundaries per frame (x 40) | **RAM accesses a pure RAM stream completes per frame** | In the odd field (312 lines) | In the even field (313 lines) | Mean cycles per RAM access |
|---|---|---|---|---|---|---|
| 0, 1, 2 | 512 | 20,480 | **19,520** | 9,728 | 9,792 | 4.098 |
| 3 | 400 | 16,000 | **24,000** | 11,968 | 12,032 | 3.333 |
| 4, 5, 6 | 0 | 0 | **40,000** | 19,968 | 20,032 | 2.000 |

[from M, and the same by arithmetic: 256 x 24 + 56 x 64 = 9,728]. S4's own working of the same bandwidth for modes 0 to 2 gives 9,728 per 312-line field [from S4 paulb 6 Sep 2015 12:02]. A forum estimate that the CPU runs 152 of every 312 lines gives a mean of 4.1 cycles per access, and 4.098 agrees [from S9 SteveF 28 Apr 2025].

Stall length of a single RAM access in a pure RAM stream, modes 0 to 2: **every contended line has exactly one access of 82 cycles and 23 of 2 cycles**. 82 = 128 - 23 x 2. Over two frames the model gives 1,024 accesses of 82 cycles in mode 0 (512 a frame) and 800 in mode 3 (400 a frame). [from M] If the stalled access started one cycle off a boundary it takes 83, which is the 41.5 µs hoglet saw on the scope as the longest cycle. [inferring from S4 and the rule]

### 4f. What is, and is not, established about phase

- The **count** of free boundaries per contended line is 24 whatever the offset of the window, because 80 cycles always cover exactly 40 boundaries of a given parity. So the totals above hold for any phase. [from M: window start 0 to 3 cycles after the line start, and parity 0 or 1, change the BASIC loop by no more than 0.01 s]
- **Which** 24 boundaries are free in a line is the last 24 µs, give or take 2 cycles. For a stated convention only: line starts at T = 0, boundaries at even T, window blocking boundaries at positions 2 to 80; the free ones are positions 0, 82, 84, ..., 126. The first RAM access started at T = 0 then completes at T = 82. [from M]
- The window width matters. A width of 76 cycles instead of 80 gives 13.88 s in the BASIC loop and 84 gives 15.67 s, against 14.71 s real. So 40 blocked boundaries per line is pinned by the data. [from M]
- The offset of the window against the first visible pixel, and the pipeline offset between the 16 MHz tick and the memory cycle, are not in any source I could read. S10 puts the blocked region about 2 ticks (0.125 cycle) after the start of the active region. [from S10, read only to cross-check]

### 4g. Test of the rule against real hardware

I ran the OS and BASIC in the throwaway model with this rule and typed hoglet's loop (`10 MODE n`, `20 TIME=0`, `30 FOR A=0 TO 10000:NEXT`, `40 A%=TIME`, `RUN`, then read A% from RAM at `$0404`, little-endian, in centiseconds):

| Mode | Real Issue 2 Electron (S4 hoglet) | Real Issue 4, no Plus 1 (S4 davidb) | Model M |
|---|---|---|---|
| 0 | 14.71 | 14.71 | **14.71** |
| 1 | 14.74 | 14.74 | **14.74** |
| 2 | 14.89 | 14.90 | **14.89** |
| 3 | 11.92 | 11.94 | **11.93** |
| 4 | 7.07 | 7.08 | **7.08** |
| 5 | 7.08 | 7.08 | **7.08** |
| 6 | 7.07 | 7.08 | **7.07** |

Seconds. Seven timings across two real machines match to a hundredth. The same rule gives hoglet's other measurement exactly: the loop `SEI : LDA $C000 : JMP $2000` in RAM runs in **9.000 µs** (18 cycles) and he measured 9 µs on the real machine. [from S4 hoglet 6 Sep 2015 16:13; M]

What the first attempt taught. [from M] A model that missed the dummy stack read in `RTS` and `RTI` (6 cycles each, with a read of the stack page that nothing uses) ran 1.4% to 1.7% fast in every mode: 14.50, 14.54, 14.69, 11.75, 6.95, 6.96, 6.95. Each dummy read is a RAM access and costs 2 or 3 cycles. Nothing else changed. Every bus cycle, dummy reads and the writes of a read-modify-write included, must reach the ULA model.

With a Plus 1 fitted, real machines run slower (17.04 s in mode 0 for davidb, 17.19 for Wookie) and the cause is not known. The Plus 1 is out of scope. [from S4]

---

## 5. Video

### 5a. Modes

| Mode | Graphics | Colours | Text | Bytes per line | Pixel format | Display lines | Screen start | Screen RAM used | `$FE03` | `$FE02` | `$FE07` bits 5 to 3 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 640 x 256 | 2 | 80 x 32 | 80 | 1 bit | 256 | `$3000` | 20,480 | `$18` | `$00` | 000 |
| 1 | 320 x 256 | 4 | 40 x 32 | 80 | 2 bits | 256 | `$3000` | 20,480 | `$18` | `$00` | 001 |
| 2 | 160 x 256 | 16 | 20 x 32 | 80 | 4 bits | 256 | `$3000` | 20,480 | `$18` | `$00` | 010 |
| 3 | none | 2 | 80 x 25 | 80 | 1 bit | 250 | `$4000` | 16,000 | `$20` | `$00` | 011 |
| 4 | 320 x 256 | 2 | 40 x 32 | 40 | 1 bit | 256 | `$5800` | 10,240 | `$2C` | `$00` | 100 |
| 5 | 160 x 256 | 4 | 20 x 32 | 40 | 2 bits | 256 | `$5800` | 10,240 | `$2C` | `$00` | 101 |
| 6 | none | 2 | 40 x 25 | 40 | 1 bit | 250 | `$6000` | 8,000 | `$30` | `$00` | 110 |

- Resolutions, colours, text sizes and screen start: [from S1 App C screen layouts; from ROM tables at `$C3FB-$C401` (map type per mode `00 00 00 01 02 02 03`) and `$C407-$C40E` (sizes `$5000 $4000 $2800 $2000`, starts `$3000 $4000 $5800 $6000`)].
- Screen RAM used is 32 or 25 character rows x bytes per line x 8. Modes 3 and 6 use 25 rows, which is why they end at `$7E80` and `$7F40`, short of `$7FFF`. [from S1 App C mode 6 layout ends `$7F3F`; arithmetic mine]
- `$FE02` and `$FE03` hold the start address shifted right by one, so `$FE03` = start >> 9 and `$FE02` = (start >> 1) and `$E0`. The OS writes them by `LSR A : STA $FE03 : TXA : ROR A : STA $FE02`. [from ROM `$C9F6-$C9FD`; from M, values per mode as shown]
- **The start address has 64-byte resolution** (bits A5 to A0 are not stored). A move by one 64-byte step is a move of 8 characters in mode 0. [from S1 14.1 fig 14.2, "increments of 64 bytes"]
- `$FE07` bits 5 to 3 hold the mode. S3 and S10 say a mode value of 7 gives mode 4. The OS never writes 7: it maps it to 6. [from S3; S10; ROM `$DADA-$DAE2`]
- **Modes 3 and 6 have 10 scanlines per character row**: 8 data lines and 2 blank, black, with no fetch. [from S1 14.1 "two blank lines underneath each row"; from M for the effect on contention]

### 5b. Where bytes go on the screen

Screen memory is in 8-byte character cells, like the BBC's. The byte for column c, row r, scanline s in the row is at `start + r x (bytes per line x 8) + c x 8 + s`. Mode 0 row 0: `$3000, $3008, ... $3278` along the first scanline, `$3001, $3009, ...` along the second, and row 1 starts at `$3280`. [from S1 App C mode 0, 4, 6 layouts: mode 6 `$6000 $6008 ... $6138`, next row `$6140`]

Pixel layout inside a byte, left pixel first [from ROM, the pixel mask tables at `$C3C2-$C3CF`: `AA 55` (mode 2), `88 44 22 11` (modes 1, 5), `80 40 20 10 08 04 02 01` (modes 0, 3, 4, 6); the same arrangement as the BBC video ULA]:
- 1 bit per pixel: bit 7 is the leftmost pixel.
- 2 bits per pixel: pixel k (0 to 3, from the left) uses bit 7-k as the high bit and bit 3-k as the low bit.
- 4 bits per pixel: pixel k (0 or 1) uses bits 7-k, 5-k, 3-k, 1-k as the high bit to the low bit.

### 5c. Palette

The palette registers are write only and use **negative logic: a 1 turns that colour off**. [from S1 14.1]. A colour is red, green and blue on or off: eight colours, 0 black, 1 red, 2 green, 3 yellow, 4 blue, 5 magenta, 6 cyan, 7 white. The cursor and **flashing colours are produced by the OS rewriting the palette**, not by the ULA. [from S1 14.1]

A pixel value becomes a **logical colour number** and the register bits for that number give red, green and blue:
- 1 bit per pixel: pixel 0 is logical colour 0 and pixel 1 is logical colour 8. [from S1 14.1 "logical colour 1 will produce a colour defined by FE08 bit 6 (blue), FE08 bit 2 (green), FE09 bit 2 (red)"; fig 14.7a]
- 2 bits per pixel: pixel values 0, 1, 2, 3 are logical colours 0, 2, 8, 10. [from S1 fig 14.7b; S10]
- 4 bits per pixel: the pixel value is the logical colour number. [from S1 fig 14.7c]

Register bits (a 1 = that component is **off**; bits 1 and 0 of `$FE08`, `$FE0A`, `$FE0C`, `$FE0E`, and bits 7 and 6 of `$FE09`, `$FE0B`, `$FE0D`, `$FE0F` are unused):

| Register | Bit 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
|---|---|---|---|---|---|---|---|---|
| `$FE08` | B10 | B8 | B2 | B0 | G10 | G8 | x | x |
| `$FE09` | x | x | G2 | G0 | R10 | R8 | R2 | R0 |
| `$FE0A` | B14 | B12 | B6 | B4 | G14 | G12 | x | x |
| `$FE0B` | x | x | G6 | G4 | R14 | R12 | R6 | R4 |
| `$FE0C` | B15 | B13 | B7 | B5 | G15 | G13 | x | x |
| `$FE0D` | x | x | G7 | G5 | R15 | R13 | R7 | R5 |
| `$FE0E` | B11 | B9 | B3 | B1 | G11 | G9 | x | x |
| `$FE0F` | x | x | G3 | G1 | R11 | R9 | R3 | R1 |

[from S1 fig 14.7c, read as a page image; S10 agrees bit for bit]. The OS's own writes decode to the standard colours with this table. [from M and ROM] Defaults the OS writes: modes 0, 3, 4, 6: `$FE08` = `$11`, `$FE09` = `$11` (black and white); modes 1 and 5: `$73`, `$31` (black, red, yellow, white); mode 2: `$F5 $5F $05 $5F $05 $50 $F5 $50` in `$FE08-$FE0F` (black, red, green, yellow, blue, magenta, cyan, white, then the same eight for 8 to 15 in the first phase of the flash). The OS writes a palette register with `STA $FE08,Y` at `$C8E8`, and skips the write when either of bits 5 and 4 of `$034B` is set (what that flag means was not traced). [from ROM `$C8DF-$C8EB`; from M]

### 5d. Frame timing

Zero is the start of the active part of the first display line of the odd field. All numbers are 2 MHz cycles. [from S5, 0xC0DE, adjusted by hoglet's logic analyser captures]

| What | Cycle | Notes |
|---|---|---|
| Line | 128 | 64 µs, 15.625 kHz. Active 80 (640 master ticks), front porch 16, hsync 8 (4 µs), back porch 24 [from S1 14.1 scope text; S10 for the split] |
| Odd field | 0 to 39,935 | **312 lines** |
| Even field | 39,936 to 79,999 | **313 lines**, 40,064 cycles |
| Frame | 80,000 | 625 lines, 2 fields, interlaced. 50 Hz mean (vsync to vsync is exactly 40,000) |
| Vsync start, odd | 35,968 | Start of line 281, counting the first active line as line 0 |
| Vsync, even | 75,968 | Half way through line 281 of that field |
| Vsync length | 320 | 160 µs, 2.5 lines |
| RTC interrupt, odd | **12,670** | |
| RTC interrupt, even | **52,670** | So RTC is exactly 40,000 apart. S5 first had 12,672 and 52,672 then corrected both by 1 µs (2 cycles) after hoglet's captures |
| Display end, odd, modes 0, 1, 2, 4, 5 | **32,736** | Line 255 at cycle 96, which is the falling edge of hsync after the last active line |
| Display end, even, modes 0, 1, 2, 4, 5 | **72,672** | 39,936 + 32,736 |
| Display end, odd, modes 3 and 6 | **31,968** | Line 249 at cycle 96 |
| Display end, even, modes 3 and 6 | **71,904** | |

- **The two fields are not equal, so display end is 40,000 plus or minus 64 cycles apart and RTC is exactly 40,000 apart.** Timing an RTC to a display end tells you which field it is. [from S5 0xC0DE]
- S1 puts the RTC "exactly 8192 µs after the end of the 160 µs vsync pulse" and, in S1's own labelling, 31 µs after the start of line 99 in the "odd" field and 1 µs before it in the "even" field. S5 has them the other way round (odd 1 µs before line 99, even 62 cycles = 31 µs after). **Both are the same pair of intervals; the labels differ.** I use S5's, because its zero is defined and checked against captures: S1 does not define which field is first. [from S1 14.1; S5]
- S3 has 312 lines per frame, 50.08 Hz, an RTC on hsync at the end of line 100 and a display end at the end of line 255 with "VSYNC not signalled in any way". S5 supersedes it. [from S3; S5]
- In modes 3 and 6 the display-end interrupt falls two lines later than the last data line, because of the two blank lines under each row. [from S1 14.1]
- The vsync pulse is a CSYNC pulse, not an interrupt. No ULA interrupt is raised at vsync. [from S2 6 pin list; S1 14.1]
- Outside the active region (640 pixels, 256 or 250 lines) the output is black. [from S10, read only to cross-check]
- hoglet thinks S5's vsync figures are also 1 µs (2 cycles) late; S5 corrected the RTC only. Treat the vsync rows as plus or minus 2 cycles. [from S5 hoglet 10 May 2025]

### 5e. How the ULA derives the address of each fetch

[start address once per field: S3 and S7; wrap: S3, with S10 read only to cross-check; fetch addresses and the last-line behaviour: S7 hoglet 21 Jul 2020 and S10 read only to cross-check]
- The start address (`$FE02`, `$FE03`) is read **once, at the start of each field**. Writes during the field take effect at the next one. There is no way to reload the address counter mid-screen. [from S3; S7 0xC0DE 20 Jul 2020]
- Per scanline, the ULA fetches N bytes (80 or 40) at `rowbase + 8 x k + scanline-in-row`, k = 0 to N-1.
- At the end of a scanline, if it is not the last line of the character row, the column counter returns to `rowbase` for the next scanline. If it is the last line of the row, `rowbase` becomes `rowbase + 8 x N`. The last line is line 7 in the 8-line modes and line 9 in modes 3 and 6. [from S7 hoglet 21 Jul 2020 for the "last line" signal, read at hsync start and hsync end; from S10]
- **Wrap.** If the address counter would pass `$7FFF` it continues at the mode's start address (modes 0 to 2 `$3000`, mode 3 `$4000`, modes 4 and 5 `$5800`, mode 6 `$6000`) plus the overflow. Loading an address below `$0800` is replaced by the mode's start address too. Loading an address between `$0800` and the mode start is **honoured**, so the region [mode start, `$8000`) is not enforced. [from S3; S10 agrees]
- The hardware-scroll examples in S1 14.1 move the start by 64 and by 640 bytes and wrap by subtracting `$5000` in mode 0. [from S1 14.1]
- Mode changes take effect at the end of the current scanline. What happens to the internal counters when switching between 80 and 40 byte modes in the middle of an 8-line block is not established. [from S3]

---

## 6. Interrupts

### 6a. Status register `$FE00`, read

| Bit | Meaning | Set when | Cleared by |
|---|---|---|---|
| 7 | Always 1 | | |
| 6 | **High tone detect** | 10 successive bits of high tone, 20 cycles of 2400 Hz, on tape input | Write 1 to bit 6 of `$FE05` |
| 5 | **Transmit data empty** | Immediately after bit 7 of the byte being sent has gone out, before the stop bit | Writing `$FE04`. **Normally set**: it is clear only while a byte is being sent |
| 4 | **Receive data full** | As soon as bit 7 of a received byte is in the shift register | Reading `$FE04`, or when the next start bit is clocked in (a window of two bit times, about 1.66 ms) |
| 3 | **Real time clock** | 50 Hz, section 5d | Write 1 to bit 5 of `$FE05` |
| 2 | **Display end** | Falling edge of hsync after the last active line, section 5d | Write 1 to bit 4 of `$FE05` |
| 1 | **Power-on reset** | At power on | **Cleared by the first read of `$FE00`** (S3). Not set by BREAK, which is how the OS tells them apart |
| 0 | **Master IRQ** | Any enabled interrupt in bits 6 to 2 is set | Follows the others |

[from S1 14.1 fig 14.1 and the text; S3 for bit 1; from ROM]

- **The status bits are set whether or not the interrupt is enabled.** The enable only gates bit 0 and the IRQ line. [from S1 14.1]
- Writing `$FE00` bits 6 to 2 enables or disables sources. [from S1 14.1]
- IRQ is the line to the CPU and is also on the expansion connector with a 3K3 pull-up. NMI is an input to the ULA (open collector). **The ULA has no NMI source of its own.** [from S2 6; S1 15.2]
- **Tape naming.** S3 says S1 may have the tape data full and data empty bits named the wrong way round. The OS settles it: tape input enables bits 6 and 4 (`$025B AND $DF OR $50`, at `$FAD4-$FADB`) and tape output enables bit 5 (`AND $AF OR $20`, at `$FABF-$FAC4`). S1's naming is right. [from ROM]
- **Quirk some games depend on.** Entering tape output mode makes the receive-full interrupt fire, and output also raises receive-full when the pattern looks like a start and stop bit. Northern Star and Southern Belle use it as a timer. [from S4 Harte 11 and 14 Mar 2016; S10 comment]

### 6b. `$FE05` write, interrupt clear

| Bit | Clears |
|---|---|
| 7 | NMI (gives the 6502 priority over the ULA and so the snow stops) |
| 6 | High tone detect |
| 5 | Real time clock |
| 4 | Display end |

The OS clears display end with `$10` and RTC with `$20`, in its IRQ handler at `$DBC5` and `$DB8C`, and high tone and receive full through `$40` at `$F504`. [from ROM]. Remember the paging side effect of section 2a.

### 6c. What the OS enables

At reset the OS writes `$00` to `$FE00` (`$D8DD`), later `$0C` (`$D97B`): RTC and display end. Tape routines change the mask in `$025B`. [from ROM, and the first ULA writes in M]. Both RTC and display end increment the 100 Hz clock, which is why TIME runs at 100 Hz from two 50 Hz interrupts. [from S1 14.1; ROM `$DB8C-$DBF2`]

### 6d. CPU side

IRQ vector `$FFFE` points to `$DAE7`, which tests the BRK flag and jumps through `$0204`. Interrupts must not be held off for more than about 2 ms. In modes 0 to 3 an IRQ can wait about 83 cycles (41.5 µs) for the RAM stack push. [from ROM; S1 7.4; inferring the 83 from section 4e]

---

## 7. Keyboard

### 7a. How it is read

- The keyboard is the paged "ROM" in slots 8 and 9. With either selected, **a read at an address in `$8000-$BFFF` returns the keys in bits 0 to 3**. [from S1 14.2]
- **Address lines A0 to A13 are the 14 columns.** If an address line is **low**, that column is selected. The data lines D0 to D3 are the four rows; **a 1 in a bit means a key in that column is down**. [from S1 14.2; S2 keyboard schematic: 14 diodes `D1-D14` (1N4148), one per address line, and D0 to D3 with 15K pull-ups to +5 V]
- **Several lines low at once return the OR** of those columns' bits. [from S3]
- Bits 7 to 4 are 0. [from S3; S10] The OS masks with `$0F`. [from ROM]
- **The access is a 1 MHz one with no contention** (section 3a). [from S8]
- With nothing pressed, every read in the window returns 0. [from S3]
- The OS scans a column group at a time and does it by interrupt, not by polling. It can be stopped with OSBYTE `$B2`. [from S1 14.2, 7.4; ROM `$ED76-$EDD2`]

### 7b. The matrix

Address with only that column's line low. Bit 0 is D0. Every key position is from the Electron's own schematic and agrees with the OS ROM's key table at `$EDD3`. [from S1 14.2 as a page image; S2 keyboard matrix; ROM `$EDD3-$EE0A`]

| Column | Address | Bit 0 | Bit 1 | Bit 2 | Bit 3 |
|---|---|---|---|---|---|
| 0 | `$BFFE` | Right | Copy | not connected | Space |
| 1 | `$BFFD` | Left | Down | Return | Delete |
| 2 | `$BFFB` | `-` | Up | `:` | not connected |
| 3 | `$BFF7` | `0` | `P` | `;` | `/` |
| 4 | `$BFEF` | `9` | `O` | `L` | `.` |
| 5 | `$BFDF` | `8` | `I` | `K` | `,` |
| 6 | `$BFBF` | `7` | `U` | `J` | `M` |
| 7 | `$BF7F` | `6` | `Y` | `H` | `N` |
| 8 | `$BEFF` | `5` | `T` | `G` | `B` |
| 9 | `$BDFF` | `4` | `R` | `F` | `V` |
| 10 (A) | `$BBFF` | `3` | `E` | `D` | `C` |
| 11 (B) | `$B7FF` | `2` | `W` | `S` | `X` |
| 12 (C) | `$AFFF` | `1` | `Q` | `A` | `Z` |
| 13 (D) | `$9FFF` | **Escape** | **Caps Lock / Func** | **Ctrl** | **Shift** |

- The addresses in the table are from S1. The OS ROM uses `$A000` (A13 high: columns 0 to 12 together), `$9FFF` (column 13 only), `$AFFF`, `$BBFF`, `$BDFF`, `$BEFF` among others. [from ROM]
- `Caps Lock` and `Func` are **one key**. S1's table calls it Caps Lk and S3's calls it func. S1 9.2 describes "the CAPS LK/FUNC key", pressed alone to toggle the lock and with another key as a function key. [from S1 9.2; S2 keyboard schematic key legend `CAPS LK/FCT`; S8 "FUNC plus K (CHAIN)"]
- **Both Shift keys are one position** in the matrix. [from S3; S2 schematic]
- The cursor keys and Copy are ordinary matrix keys at the places above. BREAK is not (section 7c). [from S1 14.2; S2 keyboard schematic]

### 7c. Break, Escape, LEDs

- **Escape** is column 13, bit 0, at `$9FFF`. [from S1 14.2; ROM]
- **BREAK is not in the matrix.** It is a switch to 0 V on its own keyboard connector pin (pin 1, `BREAK`). Pressing it makes the ULA assert RST to the 6502 and to the expansion connector. [from S2 keyboard schematic; S2 6 pin list "RST ... enabled on power up and when the BREAK key is pressed"]
- The **caps lock LED** is driven by the ULA's `CAPS LOCK` pin through a 470 ohm resistor, and is `$FE07` bit 7: 1 turns it on. [from S1 14.1 fig 14.6; S2 6, keyboard schematic]
- The **cassette motor relay** is `$FE07` bit 6: 1 turns it on. [from S1 14.1]
- The OS powers the machine up with caps lock on: its first `$FE07` write is `$B4`, bit 7 set. [from M; ROM `$D96B-$D970`]

---

## 8. Sound

- One channel and one bit. The ULA's `SOUND O/P` pin is a square wave and goes to the speaker through a simple amplifier. [from S2 6; S1 14.1]
- **Sound is selected by `$FE07` bits 2 and 1 = `01`.** Cassette input is `00`, cassette output is `10`, `11` is not used. Sound and cassette are exclusive. [from S1 14.1 fig 14.6]
- **The pitch is set by the counter at `$FE06`, S, 0 to 255.** The output frequency is
  `f = 1 MHz / (32 x (S + 1))`.
  That runs from 31.25 kHz at S = 0 to 122 Hz at S = 255. [from S1 14.1 fig 14.5b]
- So the output toggles every `16 x (S + 1)` microseconds, which is `32 x (S + 1)` cycles of 2 MHz. The half period is `(S + 1)` ticks of a 62.5 kHz clock (the 16 MHz divided by 256). [inferring from the formula; the 16 µs unit is the same as S10's comment, `1MHz / [16 x (S + 1)]` for the toggle rate]
- The two lowest S values (0 and 1) are inaudible, a constant level, and S3 notes you can use them as an on-off speaker bit, as on the ZX Spectrum. [from S3]
- **To turn it into samples:** keep a sound-bit state and the time of the next toggle in 2 MHz cycles. At each write to `$FE06` or to the mode bits in `$FE07`, record the new level and period. For each output sample interval, set the sample to the level at that instant, or better average the level over the interval, because the high frequencies are above audio. Output nothing (hold the last level) when bits 2 and 1 are not `01`. [inferring; the first two points of the counter reload and the output level on entering sound mode are open, section 12]
- How the OS uses it: it writes the comms bits from `$0820` and the pitch from `$0821` at `$E934-$E94D`, and the envelope code at `$E955-$E970` updates `$FE06` every 100 Hz tick. [from ROM]. The start-up beep is OSWRCH 7. [from ROM `$DA3F-$DA41`]

---

## 9. Cassette

Cross-reference only; the tape format has its own sheet. [from S1 14.1; S2 6; S3]

| Item | Fact | Source |
|---|---|---|
| Registers | `$FE04` shift register, `$FE06` counter (0 in tape modes), `$FE07` bits 2 and 1 and bit 6, interrupt bits 6, 5, 4 | [from S1 14.1] |
| Baud rate | **1200 baud**, from the 16/13 MHz clock divided by 1024: 1201.9 baud, bit time 832 µs = 1,664 cycles of 2 MHz. S3 writes it as 1,000,000 / (16 x 52) | [from S1 15.2; S3; S2 8.8] |
| Frame | Start bit 0, eight data bits, **least significant first**, stop bit 1: ten bits per byte | [from S3] |
| Encoding | A 0 bit is one cycle of 1200 Hz; a 1 bit is two cycles of 2400 Hz. A sine wave, not a square wave. High tone is a stream of 1 bits at 2400 Hz | [from S3; S1 14.1] |
| Receive register direction | **A new bit enters at bit 7 and moves towards bit 0.** Written: bit 0 goes out first, then the bits move down. S3 had warned that older printings of S1 had it the other way round. The fifth edition has it right | [from S1 14.1 fig 14.3; S3] |
| High tone detect | 10 successive bits of high tone (20 cycles of 2400 Hz). Works only when the motor bit is set, the counter mode is tape input and `$FE06` is 0 (or near 0). Cleared by `$FE05` bit 6 | [from S1 14.1] |
| High tone RC | The ULA `CAS RC` pin is an input and output on an RC circuit, used to detect a continuous period of high tone | [from S2 6] |
| Motor | `$FE07` bit 6 drives a relay through the `CAS MO` pin | [from S1 14.1; S2 6] |
| Output level | `CAS OUT` is a pseudo-sinusoid, 1.8 V peak to peak. Input must be 0.5 V to 2 V peak to peak | [from S2 6] |
| Screen mode for tape | The 6502 cannot be interrupted for long stretches in high resolution modes, so bits are sometimes lost. **Use modes 4 to 6.** Worst-case interrupt delay is about 4 ms in modes 4 to 6 and up to 10 ms in modes 0 to 3 | [from S1 14.1 fig 14.3, 15.3.4] |
| Receive full window | The byte must be read within about 2 ms or it is lost | [from S1 14.1] |

### 9a. What the OS writes

[from ROM]
- **Tape input:** `$FACD`: `$FE06` = 0, `$CA` = 0, mask `$025B` = (`$025B` AND `$DF`) OR `$50`; `$FE07` = (`$0282` AND `$F9`) OR `$40`. That is comms mode `00` with the motor on, and interrupts 6 and 4 enabled.
- **Tape output:** `$FABF`: `$CA` = 4, mask = (`$025B` AND `$AF`) OR `$20`; `$FE07` = (`$0282` AND `$F9`) OR `$44`. That is comms mode `10` with the motor on, interrupt 5 enabled.
- The receive handler reads `$FE04`, writes `$40` to `$FE05` and goes on to the block decoder at `$F50A`. The transmit handler writes the next byte from `$BD` to `$FE04`. [from ROM `$F4DF-$F50A`]
- OS messages: `Searching`, `Loading`, `RECORD then RETURN`, `File not found`, `Block?`, `Rewind tape`. [from ROM `$F59E`, `$F92B`, `$F8A9`, `$F5CA`, `$FA17`, `$FA35`]

---

## 10. Reset and power-on

### 10a. The OS ROM at reset

| Item | Fact | Source |
|---|---|---|
| Release | **OS 1.00.** `\rOS 1.00\r` at `$EEDE` (printed by `*HELP`), `OS 1.00` after `BRK` and error `$F7` at `$E5F6` | [from ROM; design doc] |
| Copyright | `(C) 1983 Acorn Computers Ltd.` at `$FC00`, hidden by I/O | [from ROM] |
| Reset vector | `$FFFC/$FFFD` = `D2 D8` = **`$D8D2`** | [from ROM] |
| First instructions | `LDA #$40 : STA $0D00` (RTI at the NMI entry), `SEI`, `CLD`, `LDX #$FF : TXS` | [from ROM `$D8D2-$D8DB`] |
| Next | `INX : STX $FE00` (disable every ULA interrupt), `STX $028D`, **`LDA #$F8 : STA $FE05`** (clear every interrupt and select slot 8, the keyboard) | [from ROM `$D8DC-$D8E7`] |
| Power-on test | **`LDA $FE00 : AND #$02 : EOR #$02`.** Power-on flag set means power on. The first read of `$FE00` also clears it. No power-on flag means BREAK | [from ROM `$D8E8-$D8ED`] |
| RAM clear | On power on, or on BREAK when the break action (`$0258` >> 1) is 1: write 0 to `$0400-$04FF` and then to `$0501-$05FF`, `$0601-$06FF`, ... up to `$7FFF`, **skipping byte 0 of every page after the first** (protects `$0D00`). Takes about 1,040,000 cycles in the model, with the ULA in mode 0 and so contended at power on. The real power-on mode is not known | [from ROM `$D8FB-$D90F`; from M] |
| Defaults | `$028F` = `$FF` on power on. The default start-up mode is `$028F AND 7`, and 7 becomes **6** | [from ROM `$D92D`, `$D965-$D96B`, `$DADA-$DAE2`] |
| First `$FE07` | `$B4`: caps lock LED on, mode 6, cassette output mode | [from M; ROM `$D96B-$D970`] |
| Order afterwards | Vectors and workspace from `$0200`; `$FE07`, `$FE04`; `$FE00` = `$0C`; cassette set up; mode set (`$E976`); the **ROM scan of all 16 slots with `$0C`, n pairs written to `$FE05`**; Tube probe at `$FCE0`; service calls; the banner; auto-boot service call; the language ROM entered at `$8000` | [from ROM `$D8D2-$DAD7`; from M's ULA write log] |
| Interrupts on from | About cycle 1,052,054 in the model, when `$FE00` is written | [from M] |

### 10b. What it prints

The banner is two strings in the OS ROM. At `$C303`: `0D` `Acorn Electron ` (15 characters, trailing space) `00`. At `$C314`: `08 0D 0D 00`. At `$C42B`: the bell glyph, 8 bytes `18 3C 3C 7E 7E 00 7E 3C`. [from ROM]

Printed through OSASCI, so each `0D` becomes a line feed and carriage return. [from ROM `$DA35-$DA4F`, `$DC34-$DC45`]. After the text, if `$028D` is non-zero, it sends `BEL` and **draws the bell glyph on the screen at the cursor** (`$CED5` copies the 8 bytes at `$C42B` to the cursor cell, wrapped in the cursor save and restore routine `$D6DE`), then backspaces and prints two newlines. `$028D` is 1 on power on and 2 on CTRL-BREAK (the reset code increments it once for power on and once more if CTRL is down) and 0 on a soft BREAK, so a soft BREAK prints the text and the newlines but no bell and no glyph. [from ROM `$D8E0`, `$D91F-$D92A`, `$DA3A-$DA4D`, `$CED5-$CEF9`; the meaning of `$028D` is S1's "last BREAK type", OSBYTE 253]

The screen after reset with only these two ROMs, mode 6, 40 x 25 at `$6000`, 320 bytes per character row [from M, which ran the ROMs and decoded the screen with the font at `$C000`; the rows follow from the ROM text above]:

| Row | Text | Printed by |
|---|---|---|
| 0 | (blank) | the mode change leaves the cursor at row 0 and the first `0D` moves down |
| 1 | `Acorn Electron ` and a **bell glyph in column 15** (power on only) | OS banner |
| 2 | (blank) | the `0D` after the backspace |
| 3 | `BASIC` | the OS, entering the language: it prints the ROM title string at `$8009` |
| 4 | (blank) | two newlines after the title |
| 5 | `>` and the cursor in column 1 | BASIC |

- The machine in the model reaches this screen about **0.66 s** after the reset is released. [from M]
- **The text is `Acorn Electron`, not `BBC Computer`.** There is no memory size. The line has a trailing space and a bell glyph, so a test that compares the whole row has to include the glyph. [from ROM]
- Row 1's cell at column 15: `18 3C 3C 7E 7E 00 7E 3C`. [from M]
- Row 3: `BASIC`. Row 5: `>`. Each character cell is the 8 bytes from the font. The font is at `$C000` for characters `$20-$7F`, 8 bytes each: `$C000 + (ch - $20) x 8`. [from ROM; M decoded the screen with it]

### 10c. How the first ULA writes go

[from M, ULA write log, cycle counts in the model]

```
98        FE00 <- 00      all interrupts off
112       FE05 <- F8      clear all, select slot 8
(RAM clear, about 1,040,000 cycles)
...       FE05 <- 0C, 08  select slot 8
          FE05 <- 0C, 00  select slot 0
...       FE07 <- B4      mode 6, caps LED on, cassette output
          FE04 <- B4
          FE00 <- 0C      enable RTC and display end
          FE07 <- B0
          FE06 <- 00      x4
          then 0C, 00 / 0C, 01 / ... / 0C, 0F: the ROM scan, slots 0 to 15
```

---

## 11. Test material

All four are to be written **before** the model and need no third-party material. The first three are derived from sections 4b to 4e, by hand arithmetic that anyone can redo (a worked check is under 11c). The fourth is a real-hardware measurement. State in the test that the expected numbers come from this sheet and not from the model.

### 11a. The alignment loop, mode 4, 5 or 6

```
$2000  78           SEI
$2001  AD 00 C0     LDA $C000     ; reads the OS ROM
$2004  4C 00 20     JMP $2000
```
Bus accesses per iteration: 8 in RAM (`SEI` 2, `LDA` 3 fetches, `JMP` 3) and 1 in ROM. **Expected: 18 cycles of 2 MHz per iteration, steady state, exactly 9 µs.** The naive "RAM costs 2, ROM costs 1" gives 17, and the old ElectrEm rule (1 or 2 cycles) gives 16 (8 µs). [from S4 hoglet 6 Sep 2015: measured 9 µs on a real Issue 2 Electron; M gives 18.000]

### 11b. A pure RAM stream: 46 cycles a loop in modes 4, 5, 6

```
$2000  78                       SEI
$2001  EA x 10                  NOP, ten of them
$200B  4C 01 20                 JMP $2001
```
Each `NOP` is 2 accesses, both in RAM (opcode, then the dummy read of the next byte). `JMP` is 3. That is 23 accesses a loop. **Expected in modes 4, 5, 6: every access takes 2 cycles, so 46 cycles a loop, every loop** (the first access of the first loop may take 3). [from M]

In a **frame** of 80,000 cycles, with `SEI` set so no interrupt steals time, counting the completed RAM accesses:

| Mode | Expected accesses per 80,000 cycles | Tolerance |
|---|---|---|
| 0, 1, 2 | **19,520** | plus or minus 1 (where the frame boundary cuts an access) |
| 3 | **24,000** | plus or minus 1 |
| 4, 5, 6 | **40,000** | plus or minus 1 |

and the **histogram of access lengths** over a frame in modes 0, 1, 2: **512 accesses of 82 cycles** and every other access of 2 cycles. In mode 3: **400 accesses of 82 cycles**. In modes 4, 5, 6: none longer than 2 cycles. [from M and the arithmetic of 4e]

### 11c. When does the Nth access complete

For the stream of 11b, with a stated convention that matters only to plus or minus 2 cycles: the first access starts at T = 0, the first display line of the odd field, boundaries at even T, window blocking positions 2 to 80. [from M]

| Mode | #24 | #48 | #6,144 | #9,728 | #9,792 | #19,520 |
|---|---|---|---|---|---|---|
| 0, 1, 2 | 128 | 256 | 32,768 | **39,936** | 40,304 | **80,000** |
| 3 | 128 | 256 | 24,688 | 35,456 | 35,584 | 70,400 |
| 4, 5, 6 | 48 | 96 | 12,288 | 19,456 | 19,584 | 39,040 |

Hand check for mode 0: line k (k = 0 to 255) completes 24 accesses by its end, so access #24(k+1) completes at T = 128(k+1) and #6,144 at 32,768; then each of the 56 blank lines of the odd field adds 64 accesses per 128 cycles, so #6,144 + 56 x 64 = #9,728 completes at 32,768 + 56 x 128 = 39,936, the end of the odd field. For mode 3 the same arithmetic runs row by row: 8 lines of 24 and 2 lines of 64 are 320 accesses in 1,280 cycles. In modes 4, 5, 6 access #n completes at T = 2n, and #40,000 at 80,000. In mode 0, access #9,728 ends exactly with the odd field and #19,520 exactly with the frame. Accept plus or minus 2 on every entry: a different convention moves each by 1 (one less in modes 0 to 3, one more in modes 4 to 6). [from M, boundaries at odd T and window start 0]

### 11d. The real-hardware anchor: BASIC's FOR loop

Type in BASIC (hoglet's version used `INPUT M` and `PRINT TIME`):
```
10 MODE n
20 TIME=0
30 FOR A=0 TO 10000:NEXT
40 A%=TIME
RUN
```
and read `TIME` in centiseconds. **Expected, in seconds, plus or minus 0.05 (TIME has 0.01 resolution and the two real machines differ by up to 0.02):**

| Mode | 0 | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|---|
| Real, Issue 2 (hoglet) | 14.71 | 14.74 | 14.89 | 11.92 | 7.07 | 7.08 | 7.07 |
| Real, Issue 4, no Plus 1 (davidb) | 14.71 | 14.74 | 14.90 | 11.94 | 7.08 | 7.08 | 7.08 |

This is a measurement of the whole chain: the 6502 bus cycles, the OS and BASIC ROMs, the interrupts and the contention rule. It fails if modes 0 to 2 and 4 to 6 are not about 2.08 times apart, if mode 3 is not between them (about 1.68 times mode 4), or if mode 3 contends on the two blank lines of each row (14.34 s). [from S4; M]

---

## 12. Open

Everything here is flagged `[guessing - verify]`. None blocks the boot test; the first two bear on exact raster tricks only.

1. **The absolute phase of the 1 MHz boundaries against the line start, and of the display window against the first pixel.** `[guessing - verify]` Any phase gives the same counts (4f). It moves exact instants by up to 2 cycles. It matters only for code that counts cycles to a pixel. The convention in 4f is mine, not a measurement.
2. **Whether the ULA register accesses (`$FE00-$FEFF`) and `$FC/$FD` are 1 MHz or 2 MHz.** `[guessing - verify]` S3 says 2 MHz, S10 says 1 MHz, S8 confirms 1 MHz for the keyboard only. I recommend 1 MHz with no contention (the keyboard is certain, the rest follows S10). The effect on the BASIC loop is under 0.01 s.
3. **What an absent device returns.** `[guessing - verify]` A read of a write-only ULA register, of FRED or JIM with nothing fitted, and of `$8000-$BFFF` in an empty slot. S3 says the ULA reads return the ROM byte; S10 turns the ROM off for that range. I recommend the high byte of the address (the BBC sheet's measured rule for absent fast devices), and I showed the boot does not care.
4. **The power-on state of the ULA.** `[guessing - verify]` ROM latch, mode, `$FE07`, palette and the initial interrupt status (in particular whether bit 5, transmit empty, is set after reset, since S1 says it is normally set). Only the length of the RAM clear depends on it (the mode, as it sets contention). S10 resets to slot 0, mode from a jumper, sound mode.
5. **Whether BREAK resets any ULA register.** `[guessing - verify]` S2 says only that the ULA asserts RST. The OS rewrites what it needs.
6. **The exact pipeline offset between a fetch and its pixel, and when a palette or mode write takes effect inside a line.** `[guessing - verify]` S3 says a mode change takes effect at the end of the current scanline. S7 shows a mode change at a precise cycle has an effect on the "last line" test. Palette writes are said to take effect at once. The raster tricks that depend on it are not in the first version.
7. **The address counter during a mode change between 80 and 40 byte modes mid-row.** `[guessing - verify]` S3 admits it is not known.
8. **The address counter in the two blank lines of modes 3 and 6.** `[guessing - verify]` I take it that it advances once per row (the end of the last line). From S10 only.
9. **Sound.** `[guessing - verify]` Whether writing `$FE06` restarts the divider or only reloads it at the next toggle, the level of the output on entering sound mode, and the filtering in the amplifier. The formula `1 MHz / (32 x (S + 1))` is S1's; I did not trace how the OS turns a `SOUND` pitch into S.
10. **Cassette internals.** `[guessing - verify]` The time constants of the high tone detector (the `CAS RC` pin), exactly when receive full fires while sending, and what the ULA does when `$FE06` is not 0 in input mode. The other sheet will take them.
11. **Undocumented opcodes in the Synertek 6502A.** `[guessing - verify]` S3 quotes a Stardot contributor saying they behave like the ones other NMOS 6502s have. I did not look for any use in the ROMs; the OS and BASIC boot and run the BASIC loop under a documented-opcodes-only core, so they are not needed for those.
12. **The Plus 1 slow-down.** `[guessing - verify]` With a Plus 1, real machines are 16% slower in modes 0 to 2 and 7% in modes 4 to 6 (17.04 s and 7.58 s for davidb). Cause not known. Out of scope.
13. **Interrupt latency detail.** `[guessing - verify]` The 6502 samples IRQ and NMI at the end of the second-to-last cycle and a taken branch delays an interrupt by one instruction (S5 hoglet 10 May 2025 points to Stardot and 6502.org threads on it). The model in M polled at instruction boundaries and the BASIC loop still matched. A CPU with the proper sampling would be nearer the machine.
14. **The 6502 sees `PHI OUT` high for up to 41.25 µs.** `[guessing - verify]` Whether the 6502's dynamic registers survive that on a hot day is a hardware matter. S3 says the SY6502A can survive 40 µs. Irrelevant to emulation.
15. **S1's edition.** `[guessing - verify]` The fifth edition (2023) is a community revision. A statement in S1 that disagrees with a 1984 printing is not marked here. The three places S1 and the OS or another source disagree were: S1's RTC field labels against S5 (4d), the tape register direction against S3 (9), and the tape interrupt names against S3 (6a).
