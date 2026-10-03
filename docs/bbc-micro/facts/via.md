# BBC Micro Model B: system and user VIA, wiring, keyboard, SN76489 - fact sheet

Written 2 October 2026 for the BBC Micro design (`docs/superpowers/specs/2026-10-02-bbc-micro-design.md` on `origin/main` of dbhq-uk/6502). No code written, no repo edited. Working files: `/tmp/bbc-facts/via-src/` (datasheets, forum threads, my throwaway 6502 disassembler `dis.py`), `/tmp/bbc-facts/via-parts/` (the four parts that this file concatenates; sections 3 and 4 were researched by two sub-agents and are included unedited except for the title and the AUG cross-check at the start of section 4).

**Tags.** `[from <key>]` = read directly (keys resolve to URLs in each section's source table). `[from ROM addr]` = disassembled from `/tmp/bbcrom/os.rom`, BASIC.ROM or DFS-1.2.rom. `[inferring ...]` and `[guessing - verify]` as in the repo rules.

**GPL.** Nobody on this task copied or paraphrased jsbeeb or B-em code. Cross-check reads, as disclosed per section: sections 1 and 2 - none (I only saw short quoted fragments inside stardot forum posts and used none); section 3 - none; section 4 - the sound sub-agent opened jsbeeb `soundchip.js` and b-em `sn76489.c` to cross-check four numbers (LFSR width, taps and reset value, n=0 handling, volume formula), tagged JSB/BEM there.

**OS identity.** The ROM is OS 1.20: vectors NMI &0D00, RESET &D9CD, IRQ &DC1C, and the string "OS 1.20" at &E825 and &F0C2 [from ROM, keyboard sub-agent].

## The twelve facts most likely to bite

1. **VIA time base is 1 MHz.** All timer maths is in 1 MHz cycles; a CPU access lands in one VIA cycle. The 2 MHz-to-1 MHz phase alignment is not established here (1.0).
2. **T1 first expiry is N+1.5 cycles after the end of the write cycle; free-run period N+2.** A read in cycle W+N+2 sees IFR6 set and T1C-L = &FF; W+N+1 sees 0 (1.4, 1.10). The OS's 100 Hz tick is latch &270E = 9998, giving 10000 cycles exactly.
3. **IFR/IRQ rise mid-cycle.** A read or write in the same cycle that would clear the flag does not clear it (real Model B, Master). IRQ_N then asserts 500 ns later (1.9).
4. **Timer 1 PB7 reads as the timer output** when ACR7 = 1, whatever DDRB7 says, and the flip-flop keeps toggling even if ACR7 is cleared (1.3). Snapper and Planetoid need it.
5. **T1L-H write clears IFR6 even in one-shot** on real hardware, against the Rockwell sheet and the AUG (1.1).
6. **The OS uses IFR writes, not T1C-L reads, to acknowledge T1** (write &40), and clears IFR/IER with &7F (1.4, 1.6).
7. **Cold-start test: system IER must read &80 after power-on and keep its value across BREAK.** The system VIA is reset by RSTA only; BREAK resets the user VIA (1.2).
8. **System VIA PCR=&04, ACR=&60, IER=&F2.** ACR=&60 puts T2 in pulse-count mode on PB6 (speech), so T2 must not run free. CA1 = vsync (negative edge = end of pulse), CA2 = keyboard (positive edge), CB1 = ADC (1.7, 1.5).
9. **PB7 must read 1** (no speech chip), PB4/PB5 read 1 (no fire buttons) (1.3, 2.1).
10. **Port A reads the pins**; keyboard and sound go through &FE4F (no handshake) (1.3, 2.1, 3a).
11. **The DFS 1.2 ROM uses the system VIA shift register in mode 010** as a timer, enable via IER &84. First version needs SR mode 2 to set IFR2 about 19 cycles after an SR read [guessing - verify] (1.8).
12. **Unmapped ADC (&FEC0) must read with bit 6 clear**, or the OS starts and finishes conversions on garbage; default channel count is 4 (2.5).

## Source disagreements, in one place

| Topic | Disagreement | Trusted |
|---|---|---|
| CA1 / CA2 | 8bs.com says CA1 = keyboard, CA2 = vsync | AUG, Hardware Guide, the ROM (IFR bit 1 = vsync, IER bit 0 armed only by the keyboard code) |
| T1L-H write clears IFR6 | Rockwell sheet and AUG: no effect (one-shot); MOS and WDC: clears | Real Model B: clears |
| T1 counter after one-shot expiry | Datasheet text: keeps decrementing; datasheet figure and Rich Talbot-Watkins: reloads from the latch | Unresolved; no OS dependency |
| PB6 / PB7 speech lines | AUG: PB6 ready, PB7 interrupt; Hardware Guide and a later PCB issue: PB7 ready, PB6 interrupt | Irrelevant without speech; PB7 must read 1 |
| Screen-start adder bits (latch 4, 5) | AUG table swaps the pairs for modes 0-2 and 4-5 | The ROM writes (it drives the hardware) |
| Keyboard "rows" vs "columns" | Service Manual swaps the names | OS/AUG naming: PA0-3 column 0-9, PA4-6 row 0-7 |
| SN76489 noise register reset value | one stardot post &8000; SMS Power, TI datasheet logic: top bit of a 15-bit register (&4000) | &4000 |
| SN76489 tone n = 0 | SMS Power: constant; jsbeeb: 1024 | Unknown on a real BBC chip |

---

# 1. The 6522 VIA, for a cycle-accurate BBC Micro Model B

Tags: `[from KEY]` = read directly in that source. `[from ROM addr]` = disassembled from `/tmp/bbcrom/os.rom` (my own throwaway disassembler, not any emulator's). `[inferring ...]` = joining dots. `[guessing - verify]` = flag for correction.

Source keys (all fetched this session; local copies in `/tmp/bbc-facts/via-src/`):

| Key | Source |
|---|---|
| WDC | WDC W65C22 datasheet, 16 Feb 2024, https://www.westerndesigncenter.com/documentation/w65c22.pdf (page numbers are the PDF's own) |
| RWL | Rockwell R6522 datasheet, Sep 1993, https://archive.org/download/Rockwell_R6522/Rockwell_R6522.pdf (scan; OCR of the same file via http://archive.6502.org/datasheets/rockwell_r6522_via.pdf) |
| MOS | MOS MCS6522 preliminary datasheet, Nov 1977, https://6502.org/documents/datasheets/mos/mos_6522_preliminary_nov_1977.pdf |
| AUG | BBC Microcomputer Advanced User Guide (Bray, Dickens, Holmes), https://stardot.org.uk/mirrors/www.bbcdocs.com/filebase/essentials/BBC%20Microcomputer%20Advanced%20User%20Guide.pdf - ch.22 VIA, 23 System VIA, 24 User VIA, 26 ADC |
| SM | Acorn BBC Microcomputer Service Manual (Oct 85 ed.), http://chrisacorns.computinghistory.org.uk/docs/Acorn/Manuals/Acorn_BBCSMOct85_Sec1.pdf |
| HG | A. D. Derrick, A Hardware Guide for the BBC Microcomputer, https://acorn.huininga.nl/pub/docs/manuals/Wise-Owl/A%20Hardware%20Guide%20For%20The%20BBC%20Microcomputer.pdf |
| 8BS | https://8bs.com/inbbcmapped.htm (Model B motherboard notes, text derived from the Service Manual) |
| SD-nnnn | stardot forum thread `https://stardot.org.uk/forums/viewtopic.php?t=nnnn`. The 2018-19 threads by scarybeasts, Rich Talbot-Watkins (RTW), BigEd, hoglet, Pernod, 1024MAK ran test programs on REAL Model Bs. 16081 = "simple 6522 VIA queries", 16138 = "More interesting 6522 VIA emulation discrepancies" (plus page 2, p=466442, Oct 2025), 16251 = T1LH writes, 16252 = ACR writes, 16262 = IFR write vs timer, 16263 = PB7 vs PB7 mode, 16271 = ACR writes vs expiry, 16402 = two more, 29288 and 26939 = vsync |

**GPL note.** I did not open jsbeeb or B-em source. The stardot threads above quote small fragments of both (a jsbeeb generated-opcode snippet, a B-em `case T2CH` block). I read them in passing, used none of it, and none of it is paraphrased here. All numbers below come from datasheets, the ROM or real-hardware test results posted by people with Beebs.

**Chip variant.** The real Model B carried NMOS parts: Rockwell R6522P in BigEd's test machine (datecodes 8243, 8304) [from SD-16271]; MOS, Synertek, UMC and Rockwell parts all exist and hoglet warns corner cases may differ between makers [from SD-16251]. The WDC W65C22 sheet used here is the CMOS part; I treat it as the NMOS behaviour except where real-hardware results say otherwise (flagged).

## 1.0 Time base (read this first)

- The VIA's phi2 is the 1 MHz bus clock. A VIA "cycle" below is one 1 MHz clock = 1 us = 2 CPU cycles at 2 MHz [from AUG ch.22 "decrements at the system clock rate (1 MHz)"; from 8BS "1MHz system clock ... one of the 2MHz clock cycles is masked off" for slow devices].
- A CPU access to a VIA takes place in exactly one VIA cycle. The CPU's 2 MHz access is stretched to line up with it. Which 2 MHz cycles are added is NOT established here (it belongs to the bus-stretch fact sheet).
- Notation: **W** = the VIA cycle in which the write strobe happens. A write completes at the end of its cycle (phi2 falling edge). **W+k** = the k-th VIA cycle after it. A read in a VIA cycle samples the register at the end of that cycle.
- Within a cycle: phi2 low first, then phi2 high. The datasheet figures draw the write strobe asserted for the whole cycle and counter changes on phi2 falling edges [from WDC Fig 2-3, p.16, rendered and read by eye].

## 1.1 The 16 registers

| RS | Name | Write | Read | Reset | BBC addr: system / user |
|---|---|---|---|---|---|
| 0 | ORB/IRB | output reg B. Clears IFR4 (CB1) and IFR3 (CB2) unless CB2 is in "independent" mode | pins for input bits, ORB for output bits (see 1.3). Same flag clearing | 0 | &FE40 / &FE60 |
| 1 | ORA/IRA | output reg A. Clears IFR1 (CA1) and IFR0 (CA2 unless independent). CA2 handshake/pulse fires | **pin levels** (see 1.3). Same flag clearing and handshake | 0 | &FE41 / &FE61 |
| 2 | DDRB | 1 = output | DDRB | 0 (all input) | &FE42 / &FE62 |
| 3 | DDRA | 1 = output | DDRA | 0 | &FE43 / &FE63 |
| 4 | T1C-L / T1L-L | writes the low LATCH only | T1 counter low; **clears IFR6** | not reset | &FE44 / &FE64 |
| 5 | T1C-H | writes high latch AND loads counter (low from low latch); clears IFR6; starts T1 | T1 counter high | not reset | &FE45 / &FE65 |
| 6 | T1L-L | low latch | low latch; does NOT clear IFR6 | not reset | &FE46 / &FE66 |
| 7 | T1L-H | high latch only, no counter load; **clears IFR6** | high latch | not reset | &FE47 / &FE67 |
| 8 | T2C-L / T2L-L | low latch only (write-only latch) | T2 counter low; **clears IFR5** | not reset | &FE48 / &FE68 |
| 9 | T2C-H | high counter; low latch -> low counter; clears IFR5; starts T2 and re-arms IFR5 | T2 counter high | not reset | &FE49 / &FE69 |
| A | SR | load; starts a shift in out-modes | read; starts a shift in in-modes. Both clear IFR2 | not reset (power-on value unknown) | &FE4A / &FE6A |
| B | ACR | see 1.8 | | 0 | &FE4B / &FE6B |
| C | PCR | see 1.7 | | 0 | &FE4C / &FE6C |
| D | IFR | write 1 to bits 0-6 clears those flags; bit 7 ignored | flags; bit 7 = IRQ | 0 | &FE4D / &FE6D |
| E | IER | bit 7 = 1 sets the named bits, bit 7 = 0 clears them | bits 0-6 = enables, **bit 7 reads as 1** | 0 (reads &80) | &FE4E / &FE6E |
| F | ORA/IRA | as reg 1 but NO handshake and NO CA1/CA2 flag clearing | as reg 1, same exceptions | | &FE4F / &FE6F |

Sources: register map and read/write behaviour [from WDC Tables 2-1, 2-6 to 2-9, 2-11, 2-12, pp.7-25]; flag clearing [from WDC Table 2-11 p.25, MOS PCR section]. BBC addresses [from SM section 9 test listing "LDA &FE40 \ IC3 (VIA A)", "LDA &FE60 \ IC69 (VIA B)"; from AUG ch.23 "Sheila &40-&4F", ch.24 "&60-&6F"].

**Datasheet disagreement on T1L-H.** WDC and MOS say a write to T1L-H clears IFR6. RWL's text and the AUG do not mention it ("writing into T1L-H has no effect" in one-shot [from RWL p.6]). Real Model B (Rockwell R6522P): a T1L-H write in ONE-SHOT mode clears IFR6 (test returned 64 then 0) [from SD-16251, BigEd run]. Trust the real machine plus MOS/WDC. A game (Skirmish) hangs if this is wrong [from SD-16251, tom_seddon].

**IER write quirk.** Reads always return bit 7 = 1 [from WDC p.25, AUG p.414]. Worked: IER=0, write &F2 -> reads &F2; then write &02 -> reads &F0; write &7F -> reads &80.

## 1.2 Reset and power-on

| Item | State | Source |
|---|---|---|
| Registers cleared by RES | ORA, ORB, DDRA, DDRB, ACR, PCR, IFR, IER all 0. T1/T2 counters and latches and SR are NOT cleared | [from MOS "RESET (RES)": clears all internal registers to 0 except T1, T2 and SR; WDC s.3.9; RWL p.2] |
| After reset | all port pins are inputs; T1, T2, SR and interrupt logic disabled | same |
| Real Model B after power-on, user VIA, nothing plugged in | `?&FE60`=255, `?&FE61`=0, `?&FE62`=0, `?&FE63`=255 (the last is the OS's own setting). So input pins with nothing attached read **1**; ORA=0, ORB=0, DDRs=0 | [from SD-16081, 1024MAK and Pernod, real Model Bs; MAME agreed] |
| T1/T2 counters, latches, SR at power-on | Undefined. Not measured. SR "might be $FF or $00", asked for and not answered | [from SD-16271, scarybeasts "bonus test" - no reply found] |
| **Which reset pin each VIA gets (BBC-specific)** | The **system VIA** is reset only by the power-on circuit (RSTA). BREAK (RST) resets everything else but NOT the system VIA. The **user VIA** gets the general RST, so BREAK resets it | [from HG s.3.14; from 8BS IC16 note; consistent with ROM below] |
| How the OS uses that | Reset code reads system IER (`LDA &FE4E`, `ASL A`, `PHA`, `BEQ`): IER=0 means cold start (does the RAM test), non-zero means BREAK. So **a BREAK must leave the system VIA's IER alone** | [from ROM D9D7-D9E4] |

## 1.3 Port reads (output vs input pins)

- **IRA (ports A, regs 1 and F): the value is the logic level on the pin**, for input AND output bits. An overloaded output can read back wrong [from WDC p.8]. For an emulator: pin = ORA bit where DDRA bit is 1, else the external level.
- **IRB (port B): output bits read ORB, input bits read the pin** [from WDC p.8].
- **Exception, PB7 timer output.** With ACR7=1, PB7 reads as the timer's output level, even when DDRB7=0 and even when DDRB7=1 (then it does NOT read ORB7) [from SD-16081 tom_seddon and Coeus on real-machine results: "PB7 low before T1 expiry, high after", works with DDRB7 input or output]. Real Beeb test VIA.PB2 returned 128 (PB7 high): with ACR7 cleared at the moment T1 expired and set again before the read, PB7 still read high, so the PB7 flip-flop keeps toggling regardless of ACR7 [from SD-16263, BigEd]. The game Snapper and Planetoid rely on the PB7 timer output being readable [from SD-16081].
- **Input latching.** ACR0=1 latches IRA on the CA1 active edge; ACR1=1 latches IRB on the CB1 active edge. With latching off, IRx follows the pins [from WDC p.8, Table 2-8]. The OS and DFS never set ACR0/ACR1 [from ROM: only ACR writes are &60 at DA8C, and the DFS's SR-mode writes below].
- **Handshake side effect.** Reading or writing reg 1 (`&FE41`) clears the CA1 flag and, unless PCR CA2 mode is "independent", the CA2 flag. Reg F (`&FE4F`) does neither. The OS scans the keyboard and writes the sound chip through `&FE4F` for exactly this reason [from ROM F034/F037, EB28; the choice of &FE4F over &FE41 is [inferring]].
- Observed system VIA port B levels the OS needs: **PB7 must read 1 with no speech chip fitted**. At reset `BIT &FE40 / BMI` skips the speech-chip initialisation if bit 7 is 1; if it read 0 the OS would decide a speech processor is present and write to it [from ROM DB11-DB14, DB16 `DEC &027B`]. PB4, PB5 (joystick fire buttons) read 1 when not pressed [from AUG ch.23.1]. PB6: no OS dependency found; treat as 1 [guessing - verify].

## 1.4 Timer 1, cycle by cycle

**Timeline (the datasheet's Fig 2-3 and Fig 2-4, read by eye; unit = VIA cycles after write cycle W).**

| When | T1 counter value a read of T1C-L would give | Notes |
|---|---|---|
| W (write of T1C-H, latch = N) | old value | latch high = data; counter low = latch low; counter high = data; IFR6 cleared; PB7 (if ACR7=1) falls at the end of W |
| W+1 | **N** | counter holds N for one full cycle |
| W+2 | N-1 | |
| W+k (1 <= k <= N+1) | N-(k-1) | |
| W+N+1 | 0 | |
| W+N+2 | $FFFF (low byte 255) | **IFR6 becomes 1 in the MIDDLE of this cycle (phi2 rising edge); IRQ_N goes low at the same instant; PB7 goes high** |
| W+N+3, free-run | N (reload from latch) | one cycle of FFFF, then the latch |

[from WDC Fig 2-3 (p.16) and Fig 2-4 (p.17); RWL Figs 15, 16 give the same "N+1.5 CYCLES" and "N+2 CYCLES" labels; RWL text: "PB7 will go low on the falling edge of phi2 following the write operation"]. The counter reading 1, 0, 255, then the latch value is confirmed on real machines [from SD-16138, RTW and hoglet's VIA.T11 run: `1, 0, 255, 4, 3, 2`].

- **First timeout: N+1.5 cycles after the end of the write cycle.** Real scope measurement for N=4: IRQ_N falls 5.7 us after the T1C-H write, 5.5 us plus about 0.2 us of gate delay [from SD-16138 p=466442, hoglet, Oct 2025]. So the cycle in which the flag first reads set is **W+N+2**; a read in W+N+1 sees it clear [inferring from the two sources above; the figure's phi2 edges carry this, nothing states "cycle W+N+2" in words].
- **Free-run period is N+2 cycles** between flag sets [from WDC Fig 2-4; RWL Fig 16; AUG `T1 period`; Robin's 6522 experiment blog and 6502.org interrupts tutorial also say n+2 per the web search, not opened]. So the n-th expiry is in cycle **W + n(N+2)**. PB7 inverts at each.
- **One-shot (ACR7:6 = 00 or 10).** IFR6 sets once and does not set again until re-armed by a T1C-H write. After the flag, the counter keeps going. WDC/RWL text says "the counter continues to decrement" past zero; the T1 row of the datasheet figure and RTW's real-machine claim say it goes 1, 0, 255 then back to the latch value even in one-shot [from SD-16138, RTW; this is partly retracted for T2 only]. **Disagreement; unresolved which the silicon does for T1 one-shot.** The OS does not read T1 after expiry, so this does not matter for boot.
- **T1C-L read** clears IFR6. T1C-H read does not. T1L-L read does not [from WDC Tables 2-6, 2-7].
- **Writing T1L-L or T1L-H during a countdown changes only the latch**; the new value is used at the next reload, never the running count [from WDC s.2.7, RWL p.7]. The exact cycle at which a latch write races the reload was tested on a real Model B (VIA.T12: the program rewrites the latch around the reload and reads T1C-L twice; real result 253 then 0, matched by B-Em, b2, BeebEm and jsbeeb, not by MAME) [from SD-16402, billcarr2005]. I could not decode the rule from those two numbers without the exact program timing, so no rule is stated here.
- **PB7 rules.** ACR7=1: PB7 falls at the end of the T1C-H write cycle, rises at expiry (one-shot), toggles at every expiry (free-run). Both ACR7 and DDRB7 must be 1 for PB7 to be a *driven* output per the datasheet; real machines also show it on reads when DDRB7=0 (see 1.3) [from RWL p.7].
- **The BBC OS's T1 use** (system VIA): free-run, ACR=&60 so PB7 off, latch &270E = 9998, giving a flag every 9998+2 = **10000 cycles = 100 Hz** [from ROM DA8C, DA91 `STA &FE46` with A=&0E, DAA4/DAA7 `STA &FE47`/`STA &FE45` with A=&27]. It clears IFR6 by **writing &40 to IFR** (not by reading T1C-L) [from ROM DDCC-DDCE]. The user VIA's timers are not touched by the OS [from ROM: no reference to &FE64-&FE69].

## 1.5 Timer 2

| Item | Value | Source |
|---|---|---|
| Mode select | ACR5: 0 = one-shot interval timer, 1 = count falling edges on PB6 | WDC Table 2-8 |
| Load | T2C-L write = latch only. T2C-H write: counter high = data, counter low = low latch, IFR5 cleared, IFR5 armed | WDC Table 2-9 |
| Timing | Same N+1.5 as T1: IFR5 sets in the middle of cycle W+N+2 (counter values N at W+1, ..., 0 at W+N+1, FFFF at W+N+2) | WDC Fig 2-3 T2 row |
| After timeout | No reload from the latch: counter goes $FFFF, $FFFE ... real machine confirms `1, 0, 255, 254, 253` (VIA.T22) | WDC Fig 2-3; SD-16138 hoglet |
| Re-arm | Only a T2C-H write re-arms IFR5. IFR5 clears on T2C-L read or T2C-H write | WDC p.18 |
| Pulse-count mode | Counter is frozen unless PB6 pulses. Writing T2C-H "freezes then starts"; real machine: `1, 0, 255` timing; B-em, jsbeeb and b2 wrongly added 1 in this mode, MAME wrongly started the timer | SD-16138 |
| Freeze/start via ACR5 | A write that changes ACR5 takes effect **one VIA cycle later** than you'd expect (VIA.T23 real result 251, 249) | SD-16402, billcarr2005 |
| **OS use** | ACR=&60 so **ACR5=1 on the system VIA: T2 is in pulse-count mode on PB6**. IER enables bit 5. With nothing driving PB6 low it never counts, so IFR5 never sets. The OS's T2 handler is the speech-chip interrupt (writes `&20` to IFR, reloads T2C-H with 0 and runs the speech read loop) | ROM DA8C, DA82, DD6F-DD7D; [inferring] role |
| Datasheet note | Pulse-count decrement needs PB6 low across the leading edge of phi2 | WDC p.19 |

[guessing - verify] How many cycles an externally pulsed PB6 takes to reach IFR5 (WDC Fig 2-5 shows N, N-1 ... 0 against PB6 pulses with IRQB after the count of 0): not needed for the BBC without speech.

## 1.6 IFR and IER

| Bit | Flag | Set by | Cleared by |
|---|---|---|---|
| 0 | CA2 | active edge on CA2 | read/write ORA (&FE41), unless PCR CA2 mode is "independent"; or write 1 to IFR0 |
| 1 | CA1 | active edge on CA1 | read/write ORA (&FE41); or write 1 |
| 2 | SR | 8 shifts complete | read/write SR; or write 1 |
| 3 | CB2 | active edge on CB2 | read/write ORB, unless independent; or write 1 |
| 4 | CB1 | active edge on CB1 | read/write ORB; or write 1 |
| 5 | T2 | timeout | T2C-L read, T2C-H write; or write 1 |
| 6 | T1 | timeout | T1C-L read, T1C-H write, T1L-H write; or write 1 |
| 7 | IRQ | = OR over bits 0-6 of (IFR & IER) | cleared only as a result of the above; a write to bit 7 does nothing |

[from WDC Table 2-11 p.25, formula p.25, AUG ch.22.2.11 p.413-414.] `IRQ_N = low when ((IFR & IER) & &7F) != 0` [from WDC p.24-25; open-drain, wire-ORable per MOS "IRQ" paragraph]. Reading IFR clears nothing.

**IFR writes.** Writing 1s to bits 0-6 clears those flags, 0s change nothing, bit 7 is ignored [from WDC p.25]. The OS does this constantly: `&7F` clears all, `&40` clears T1, `&20` T2, `&10` CB1, `&02` CA1, `&01` CA2 [from ROM DA6E, DDCC, DD6F, DE6C, DD42, DE7B].

## 1.7 CA1, CA2, CB1, CB2 and the PCR

| PCR bits | Mode | Notes |
|---|---|---|
| bit 0 (CA1) | 0 = IFR1 sets on **negative** edge, 1 = positive edge | WDC Table 2-5 |
| bits 3-1 (CA2) | 000 input, neg edge, flag cleared by ORA access. 001 independent, neg edge. 010 input, pos edge. 011 independent, pos edge. 100 handshake: CA2 low on ORA read/write, back high on CA1 active edge. 101 pulse: CA2 low for one cycle after ORA read/write. 110 CA2 held low. 111 CA2 held high | WDC p.12; MOS pp.13-14 |
| bit 4 (CB1) | 0 = negative edge, 1 = positive | |
| bits 7-5 (CB2) | as CA2 but handshake and pulse fire on **ORB write only** | |

- **What the OS programs.** System VIA PCR = **&04**: CA1 negative edge (vsync), CA2 positive-edge input (keyboard), CB1 negative edge (ADC end of conversion), CB2 negative-edge input (light pen, unused) [from ROM DA85-DA87]. User VIA PCR = **&0E** at reset (CA2 held high = printer strobe idle, CA1 negative edge = printer ACK, CB1/CB2 negative-edge inputs) [from ROM DA94]. To strobe the printer the OS toggles PCR bits 3-1 by hand (`AND #&F1, ORA #&0C` then `ORA #&0E`), it does not use pulse mode [from ROM E153-E15F].
- **Reset-time quirk.** DA9A does `CMP &FE6C` after writing `&0E`: if the PCR read-back differs, `INC &0277` (a flag byte). A VIA with a normal PCR read-back never trips it [from ROM DA94-DA9F].
- **CA1 = vertical sync.** The 6845 VSYNC output is an active-high pulse feeding CA1 directly; with PCR bit0 = 0 the flag sets at the **end** of the pulse (high to low) [from SD-29288; consistent with the OS's PCR=&04]. So the interrupt comes one vsync-pulse width after the CRTC raises VSYNC (the CRTC fact sheet owns the width: one stardot post says about 4 raster lines, another "2 scanlines later" [from SD-29288, SD-26939; unresolved]).
- **CA2 = keyboard**, positive edge, i.e. a key appearing in the scanned column [from AUG ch.23.1; HG s.3.10; ROM EEE2 enables IER bit 0, F0F5-F0FD uses IFR0 to detect "any key in this column"].
- **Source conflict on CA1/CA2.** 8BS says "CA1 is used for detecting keyboard activity, CA2 is connected to the video vertical sync". That is the opposite of AUG, HG, the ROM and every emulator author. **Ignore 8BS here.** The ROM is decisive: IRQ handler tests IFR bit 1 first and runs the vsync/event code (`DD06-DD15`), and IER bit 0 is enabled only when the keyboard is armed (`EEE2-EEE4`).

## 1.8 ACR latch enables and the shift register

| ACR | Use on the BBC |
|---|---|
| bit 0, bit 1 (PA/PB latch enables) | Not used by the OS or DFS |
| bits 4-2 (SR mode) | **System VIA: not used by the OS** (ACR written once, &60). **The DFS 1.2 ROM uses mode 010 (shift in under phi2)** as a timer: it does `LDA #&84 / STA &FE4E` (enable IFR2), saves ACR&&1C, sets `ACR = (ACR & &E3) | &08`, then `BIT &FE4A` to start the shift; later polls `BIT &FE4D` for bit 2 (&04), then restores ACR, reads SR, writes &04 to IFR and &04 to IER (disables it) [from ROM DFS-1.2.rom &9B03-&9B1D, &963C-&965D]. What triggers it is [not determined - it is a DFS entry point, not traced]. User VIA SR: not used by OS or DFS. |
| bits 7-6 | System: 01 (T1 free-run, PB7 off). User: 00 (never written) [from ROM DA8C] |
| bit 5 | System: 1 (T2 counts PB6 pulses) [from ROM DA8C] |

- **Shift mode 010 timing.** WDC Fig 2-7 (p.21, read at 260 dpi): the read of SR is followed, after about two phi2 cycles, by 8 CB1 clock pulses each lasting two phi2 cycles; IFR2 and IRQ_N fall about two phi2 cycles after the last pulse ends. Pixel-measured total: **about 19 phi2 cycles from the end of the SR read cycle to IFR2 = 1** [inferring from a drawing, marked [guessing - verify]; WDC part, not NMOS]. CB2 data is sampled on the trailing edge of phi2. hoglet notes known bugs in the NMOS MOS 6522 shift register [from SD-16251].
- **What starts a mode 010 shift.** Added 2 October 2026 with the VIA code (plan task 3): the table in 1.1 says an SR read starts a shift in the in-modes, but for mode 010 the MOS sheet says "the shifting operation is triggered by reading or writing the Shift Register" [from MOS, Mode 010], so a write starts it too. The DFS starts it with a read (`BIT &FE4A`), so the boot path is the same either way. The DFS's test of the flag at &963C is `LDA #&04 / BIT &FE4D / BNE`, returning 5 when IFR2 is clear [from ROM DFS-1.2.rom &963C-&9645]: it needs the flag to have appeared, and counts no cycles of its own.
- **Other SR modes** (000, 001, 011, 100-111) are not used by anything on the Model B boot path. Their behaviour is in WDC pp.19-24 and AUG ch.22.2.10 pp.408-413 [not needed for first light].

## 1.9 Exactly when an expiry is visible, and when IRQ_N goes low

Define E = the cycle in which a timer expires (T1/T2: E = W+N+2 for the first expiry).

| Event | Where | Source |
|---|---|---|
| IFR flag (and bit 7 of IFR) become 1 | In the **middle of VIA cycle E** (phi2 rising edge). A CPU read in cycle E, sampling at the end, **sees it set**. A read in cycle E-1 does not | [inferring from WDC Fig 2-3, SD-16138 hoglet: "latches the T1 Underflow condition on phi2 (in the middle of the cycle)"] |
| IRQ_N output goes low | Same instant, **mid-cycle E**, if the flag is enabled in IER | WDC Fig 2-3; hoglet: "interrupt firing about 0.7 us into the cycle" |
| PB7 changes | Same instant | WDC Fig 2-3 |
| The 6502 sees it | A 6502 acts on IRQ if the line is low half a cycle before the end of the second-to-last cycle of the instruction (it latches half a cycle early). "The interrupt status should be known before the end of the penultimate cycle" is good enough | SD-16138 RTW, scarybeasts, citing nesdev CPU interrupts page and visual6502 |
| This core | Samples `Irq` at the end of **every** CPU cycle and uses the sample from the instruction's last cycle; "a line active during an instruction's last cycle is seen when that instruction ends"; the line changes "at the start of a cycle". So **raise `Irq` no later than the CPU cycle that contains the VIA's mid-cycle assertion** and let the core's own rule do the rest | [from `src/Dbhq.Cpu6502/Cpu.Interrupts.cs` and `docs/superpowers/specs/2026-09-29-6502-design.md` on origin/main; the mapping from a 1 MHz mid-cycle to a 2 MHz CPU cycle is [inferring]; it must be checked against 1.10 results] |

**Coincident acknowledge (real machine, Model B and Master, Rockwell NMOS).** The VIA sets the flag at mid-cycle E. A read of T1C-L, a write of T1C-H, or a write to IFR6 **in the same cycle E** does NOT clear it: the flag ends up set. IRQ_N is delayed 500 ns, to the start of cycle E+1, so the interrupt is taken one instruction later. Explanation: IFR is an RS latch; "set" is held for the whole cycle and is the last signal released [from SD-16138 p=466442 hoglet; SD-16262 for the IFR-write case; real result 1, 192 (interrupt fires, flag set)]. An acknowledge in cycle E+1 or later clears normally. RTW says it probably applies to other interrupt types too [SD-16138].

**Other real-machine quirks (not used by the OS, for later)**

- ACR written between 00 and 40 in the same cycle as T1 expiry: ACR=00 wins either way (both tests returned 0, 0) [from SD-16271, BigEd, Rockwell R6522P]. A write one cycle before expiry behaves normally.
- If the CPU has committed to an interrupt (end of penultimate cycle) and the last cycle of the instruction acknowledges it, the handler runs with IFR bit 7 clear [from SD-16138 RTW].
- One cycle passes between an acknowledge and IRQ_N rising again [from SD-16138 RTW's visual6502 note and a 6502.org thread title in search results, "6522 - exact timing"; I did not open the 6502.org page, blocked in this sandbox].
- A write to ACR does not re-arm timers [from SD-16252 conclusion as reported by a search summary; thread t16252 not read in full].

## 1.10 Worked examples a unit test can assert (VIA cycles; W = write cycle)

Arithmetic from the datasheet timeline in 1.4. "reads" = value returned by a read in that cycle.

| # | Setup (all at W unless stated) | Expected |
|---|---|---|
| 1 | ACR=0 (one-shot), IER=&C0, T1C-L=&04 then T1C-H=&00, N=4. Writes at W | T1C-L reads 4,3,2,1,0 at W+1..W+5 then &FF at W+6. IFR6=0 up to W+5, **IFR=&C0 from W+6**; IRQ_N low from mid W+6. Arithmetic: N+1.5=5.5 cycles after end of W, which lies in cycle W+6 |
| 2 | N=0 | T1C-L reads 0 at W+1, &FF at W+2; IFR6 set in W+2 (0+2=2) |
| 3 | ACR=&40 (free-run), N=4 | Flag cycles W+6, W+12, W+18 (n(N+2)=6n). T1C-L sequence per period: 4,3,2,1,0,&FF then 4 again. Clear IFR6 by T1C-L read in W+7: next flag still W+12 (period unaffected) |
| 4 | ACR=&C0 (free-run + PB7), DDRB7=1, N=4 | PB7 reads 0 from W+1; reads 1 from W+6; 0 again from W+12; alternating each 6 cycles |
| 5 | ACR=&80 (one-shot + PB7), N=4 | PB7 0 from W+1 to W+5, 1 from W+6 on and stays 1 |
| 6 | OS 100 Hz: ACR=&40, T1L=&270E, T1C-H written | Flag every 9998+2 = **10000 cycles** exactly (n(N+2) with N=&270E=9998) |
| 7 | T2 one-shot (ACR5=0), T2L-L=3, T2C-H=0 | T2C-L reads 3,2,1,0 at W+1..W+4, &FF at W+5, &FE at W+6 (no reload). IFR5 set in W+5 (3+2) and not again; T2C-H rewrite re-arms |
| 8 | OS init: ACR=&60 (T2 pulse mode), PB6 held 1, T2C-H=0 | T2 never counts; IFR5 never sets (RWL/WDC pulse-count text; behaviour with PB6 constant high is [inferring]) |
| 9 | Coincident ack: as 1, then T1C-L read in **W+6** | After the read IFR6 is still 1 (read in the setting cycle does not clear it). IRQ_N low from start of W+7. A read in W+7 clears it [first part from SD-16138 hoglet, real hardware; the W+7 clause is [inferring]] |
| 10 | Only IFR6 set, IER=&C0 (IFR reads &C0); write &40 to IFR | IFR6 clears; no flag is left, so IFR reads &00 (bit 7 follows the flags) |
| 11 | IER=0; set T1 flag; read IFR | &40 (bit 7 = 0 because nothing is enabled). Write &C0 to IER -> IFR now reads &C0 |
| 12 | Reset; read IER | &80. Write &7F -> &80. Write &F2 -> &F2 |
| 13 | Reset: read ORB/ORA/DDR | all 0; port B read with inputs high: &FF (pins pull up to 1) [from SD-16081] |
| 14 | DDRB=&0F, ORB=&0A, external pins PB4-7 = 1 | ORB read = &FA (outputs from ORB, inputs from pins) |
| 15 | DDRA=&7F, ORA=&35, PA7 pin driven 1 | IRA read = &B5 (pins; PA0-6 are the driven values) |
| 16 | T1L-H write during one-shot after expiry | IFR6 clears (real Model B) |


Real-hardware regression data, for later and as third-party evidence only (the design says no third-party test material in the acceptance test): RTW's "TestTimings" BASIC (`viatest.ssd`, https://stardot.org.uk/forums/download/file.php?id=42336, saved to `/tmp/bbc-facts/via-src/sd/vt/`) drives timer 1 (N=4) around CPU-cycle-exact accesses; BigEd's output from a real Model B is the table "Updated results" in SD-16138 (first column the interrupted instruction's address and opcode bytes, then IFR and T1C-L at IRQ time). It needs the CPU, the 1 MHz stretch and the VIA together, so it is a later check.

## 1.11 What the BBC firmware actually exercises (so you know what must be exact)

From the OS ROM (`/tmp/bbcrom/os.rom`, OS 1.20 [inferring: reset vector &D9CD, IRQ vector &DC1C, NMI &0D00 match the documented 1.20 layout; the plan should confirm]):

| Register | OS accesses | Meaning |
|---|---|---|
| System IER | read at D9D7; write &7F, then &F2 (DA82), &81/&01 to arm/disarm CA2 (EEE4, EF06); the DFS adds &84 and writes &04 (1.8) | cold-start test; enable T1, T2, CB1, CA1 |
| System IFR | read at DD06 and dispatch; writes &7F, &40, &20, &10, &02, &01 | |
| PCR / ACR | &04 / &60, written once | 1.7, 1.8 |
| T1 | latch &0E, then &27 into T1L-H and T1C-H | 100 Hz |
| T2 | T2C-L/H written 0 at DB21-DB24 and DD76 | speech |
| DDRB | &0F (DA05) | PB0-3 outputs, PB4-7 inputs |
| DDRA | &FF for sound (EB25) and speech writes (table F076 = &FF), &7F for keyboard (F031, F0E8), &00 for speech reads (table F075 = &00) | |
| ORB | latch writes (see 2.2), and `BIT &FE40` at DB11 / `LDA &FE40` at E75F | |
| ORA &FE4F | sound data, keyboard column/row, link read | |
| User VIA | DDRA=&FF (DA50), IER &7F, IFR &7F, PCR &0E, IER &82 for printer ACK, ORA write for printer data, T1/T2 never | |

BASIC ROM: no VIA access (no `&FE4x/&FE6x` operands found by a linear and a byte-pattern scan; one byte-pattern hit at &834A was inspected as data) [from my scan of `/tmp/bbcrom/BASIC.ROM`]. DFS 1.2: system VIA IER/ACR/SR/IFR as in 1.8 only.

IRQ dispatch in the OS: after a CPU IRQ the handler first reads the ACIA (`&FE08`), then system IFR (`&FE4D`), then user IFR (`&FE6D`), so **all three share the one 6502 IRQ line** [from ROM DCA2, DD06, DD47]. Whether the 1 MHz bus NIRQ also does is not needed; it is open on a stock machine.

---

# 2. How the BBC wires the two VIAs

Source keys as in section 1. Extra: `AUG-23` = AUG ch.23 "The System VIA" pp.417-423; `AUG-24` = AUG ch.24 pp.425-426.

## 2.1 System VIA (IC3, &FE40-&FE4F)

| Pin | Direction on the BBC | Function | Source |
|---|---|---|---|
| PA0-PA7 | bidirectional, the "slow data bus" | Shared by the keyboard, the SN76489 sound chip and the speech chips. To write: DDRA=&FF, data to ORA (&FE4F). To read: DDRA=&00 (speech) or &7F (keyboard, PA7 input) then read &FE4F | [from AUG-23 s.23.1; ROM EB25-EB28, F02A-F037, EE80-EE9F] |
| PA0-PA3 | output | Keyboard **column** number 0-9 (BCD). Loaded into the keyboard's 74LS163 counter | [from HG s.3.10 "PA0 to PA3 ... column"; ROM F0FA `STX &FE4F` with X=9..0] |
| PA4-PA6 | output | Keyboard **row** number 0-7 into the 74LS251 data selector | [from HG s.3.10 "PA4, 5 and 6"; ROM F0FD-F11E where A increases by &10 per row, bit 7 ends the loop] |
| PA7 | input | The 74LS251 output: **1 = key pressed (or link made)** for the selected column and row, while the keyboard is write-enabled | [from ROM F02A returns X with bit 7 = state; F10B `BIT &FE4F`, `BPL` = not pressed; polarity [inferring] from the OS code branching on bit 7 set = key down] |
| PA0-PA7 | to sound chip | Same eight lines carry the SN76489 data byte, written directly (bits not reversed by the OS) | [from ROM EB28; see the SN76489 section] |
| PB0-PB2 | output | Address of one bit of the 8-bit addressable latch IC32 (74LS259) | [from AUG-23, HG, 8BS] |
| PB3 | output | Data value written to the addressed latch bit | [from AUG-23] |
| PB4 | input | Joystick fire button (0 = pressed, 1 = open) | [from AUG-23] |
| PB5 | input | Second joystick fire button | [from AUG-23] |
| PB6, PB7 | input | Speech processor RDY/INT lines. **Sources disagree on which is which**: AUG says PB6 = ready, PB7 = interrupt; HG says INT -> PB6, RDY -> PB7; the Service Manual's PCB-issue notes say VSPRDY and VSPINT "were changed over to connect to PB7 and PB6 respectively" on a later issue [from SM issue-change note 6]. Trust the ROM-visible need: **with no speech chip PB7 must read 1** (1.3). | [from AUG-23, HG s.3.16, SM p.36, ROM DB11] |
| CA1 | input | **Vertical sync from the 6845** (negative edge = end of the sync pulse) | [from AUG-23 "CA1 input: vertical sync input from the 6845"; ROM PCR=&04] |
| CA2 | input | **Keyboard interrupt** (positive edge) - the 74LS30 (8-input NAND) output goes high when a key is down in the currently selected column | [from AUG-23 "CA2 input: from the keyboard circuit"; HG s.3.10; ROM DA85-DA87] |
| CB1 | input | **ADC end of conversion** (EOC from the uPD7002) | [from AUG-23, HG s.3.15, SM s.3.9] |
| CB2 | input | **Light pen strobe**; also wired to the 6845's LPEN input | [from AUG-23, SM s.4 connector note] |
| IRQ | output | Open-drain, wired together with the user VIA's IRQ and the ACIA's (and the 1 MHz bus's NIRQ) to the 6502 IRQ pin. The OS polls ACIA, system IFR, user IFR in that order [from ROM DCA2, DD06, DD47] | [inferring wire-OR; MOS says the chip's IRQ is open-drain "to be wire-or'ed"] |

**Source conflict, restated.** 8BS says CA1 = keyboard and CA2 = vsync. It is wrong: AUG, HG, the ROM, and the stardot threads all say the opposite. Recorded in section 1.7.

## 2.2 The 8-bit addressable latch IC32 (74LS259), written via ORB

Write one latch bit by writing `ORB = (D << 3) | A` where A = bit number (PB0-PB2) and D = new value (PB3). On a real 74LS259 only the addressed output changes [from AUG-23 s.23.2: "PB0-PB2 are set to the required address ... PB3 is set to the value"]. DDRB must have bits 0-3 as outputs; the OS sets DDRB=&0F [from ROM DA05].

| Latch bit | Function | OS evidence |
|---|---|---|
| 0 | **Sound generator write enable**, active low. Low >= 8 us then high | [from AUG s.23.3; ROM EB21-EB3F: `ORA <- byte`, `ORB <- &00` (bit0 = 0), delay, `ORB <- &08` (bit 0 = 1), delay] |
| 1 | Speech processor READ select (active low) | [from AUG-23; ROM EE80 table] |
| 2 | Speech processor WRITE select (active low) | [from AUG-23; ROM EE94 writes &02] |
| 3 | **Keyboard write enable**. 0 = keyboard enabled for software column/row selection through PA. 1 = autoscan (the 74LS163 free-runs on the 1 MHz clock, walking the columns, raising CA2 when a key is in the current column) | [from AUG-23 "Keyboard write enable (see Appendix J)"; HG s.3.10; ROM F02A `ORB <- &03` before scanning; F12E `ORB <- &0B` afterwards; EEE2 enables CA2 IRQ] |
| 4 | Screen wrap-around C0 | [from AUG-23, SM s.4 text] |
| 5 | Screen wrap-around C1 | |
| 6 | CAPS LOCK LED | [from AUG-23; ROM EEEF-EEF4] |
| 7 | SHIFT LOCK LED | [from AUG-23; ROM EEF7-EEFA] |

- **Reset-time initialisation.** The OS writes `ORB = &0E, &0D, &0C, &0B, &0A, &09, &08` (latch bits 6 down to 0, each set to 1) [from ROM DA05-DA0E]. Bit 7 is not written there.
- **When the latch is strobed.** On the Model B a flip-flop (half of IC31, clocked from the 1 MHz clock) strobes the latch each time **the system VIA is written to** (any register), so a write to any system-VIA register re-applies the current PB0-PB3 outputs [from AUG-23? no - from SM s.3.6 and 8BS "IC3": "Each time the system VIA is written to, any changes on Port B which should affect the addressable latch are strobed into it by a flip-flop (IC31)"]. With DDRB bits 0-3 fixed as outputs, re-strobing the same (A,D) is idempotent, so **modelling the latch only on ORB writes is equivalent** [inferring]; only a DDRB change that floats PB0-3 would differ (the OS never does this).
- **Screen address latch bits (C0 = bit 4, C1 = bit 5)** as the OS programs them per mode: the OS writes bit 4 then bit 5 from tables at &C44F and &C44B indexed by a screen-size class from &C440 [from ROM CB4C-CB73]:

| Modes | Size class | Bit 4 (C0) | Bit 5 (C1) | Hardware "number to add" (AUG/SM text) |
|---|---|---|---|---|
| 0, 1, 2 | 20 KB | 0 | 1 | &3000 (12K) |
| 3 | 16 KB | 0 | 0 | &4000 (16K) |
| 4, 5 | 10 KB | 1 | 1 | &5800 (22K) |
| 6 | 8 KB | 1 | 0 | &6000 (24K) |
| 7 | 1 KB (teletext) | 0 | **unchanged**: the ROM's second table read overlaps the first for this class, so only bit 4 is written (both table reads give &04) | not given in AUG; mode 7 starts at &7C00 and the CRTC R12/R13 correction handles the wrap [from AUG s.18.11.3] |

  **Conflict:** AUG's own table (s.23.2, columns "B5 B4") lists modes 0-2 as 1 1 and modes 4,5 as 1 0. The ROM writes the opposite pairs for those two groups (mode 0: bit4=0, bit5=1; mode 4: bit4=1, bit5=1). Modes 3 and 6 agree. **Trust the ROM**: it is what drives the real hardware, and it is the code under test. The adder in the emulator therefore maps (bit5,bit4) = 00 -> +&4000, 01 -> +&6000, 10 -> +&3000, 11 -> +&5800 [inferring from the ROM pairs plus the per-mode add values in SM s.3, p.17]. The adder applies only when CRTC address line MA12 = 1 [from SM p.16-17]. Mode 7 leaving bit 5 unchanged is [from ROM table overlap; flagged as odd - verify it matters in the video fact sheet].

## 2.3 What the OS does through the VIA, in order (cold boot) [from ROM D9CD-DAAA]

1. `LDA #&40; STA &0D00` (NMI vector area), `SEI`, `CLD`, stack reset.
2. `LDA &FE4E` (system IER), power-on test (1.2). On a cold start, RAM test.
3. DDRB=&0F; latch bits 6..0 := 1 (above).
4. Read the **startup links** as 9 reads via `F02A` (keyboard column 9 down to 1, row 0): ORB=3, DDRA=&7F, ORA=column number, read ORA, PA7 is the link state. Details in section 3.
5. User VIA DDRA=&FF (printer data lines). Both VIAs: IER=&7F, IFR=&7F.
6. `BIT &FC / BVC / JSR &F055` (not traced; a keyboard-links branch).
7. System IER=&F2, PCR=&04, ACR=&60, T1L-L=&0E; user PCR=&0E; ADC `STA &FEC0` (A=&0E); read back user PCR; T1L-H=&27; T1C-H=&27 (starts the 100 Hz clock). (A `CLI` / `SEI` pair at DA77-DA78 lets a pending IRQ in before the VIAs are set up.)
8. Speech presence test: `BIT &FE40` (PB7). Then the banner and language ROM search.

## 2.4 User VIA (IC69, &FE60-&FE6F)

| Pin | Function | Source |
|---|---|---|
| PA0-PA7 | **Printer data**, Centronics, buffered by an octal 3-state driver (IC70). **Output only**: the buffer is unidirectional | [from AUG-24 s.24.1; HG s.3.13] |
| CA1 | Printer ACK input. Pulled to +5 V through 4k7 | [from AUG-24; SM s.3.11 and connector text] |
| CA2 | Printer STROBE output, open-collector buffer (transistor Q11); asserted low for about 5 us. A link (S1/link option 1) connects CA2 straight to the connector on later issues | [from AUG-24; SM s.3.11] |
| PB0-PB7 | The 20-way **user port**, direct to the VIA pins, input or output. PB7 can be a timer 1 pulse output, PB6 the pulse-count input of timer 2 | [from AUG-24 s.24.2; SM s.3.11] |
| CB1, CB2 | on the user port connector | [from AUG-24] |
| IRQ | Wired to the 6502 IRQ with the system VIA's (and ACIA's) | [from ROM DD47; inferring wire-OR] |

The Model B's own firmware uses the user VIA only for the printer: DDRA=&FF, data via `&FE61`, PCR toggled by hand for the strobe, IER &82 (CA1) for the ACK [from ROM E14B-E15F, DA50]. Nothing reads or writes its timers, port B or ACR. **The spec marks the printer port and user-port peripherals out of scope**, so a user VIA with the 1 MHz-clocked timers, flags and ports and nothing attached is enough; port B inputs read &FF, port A reads back ORA (output buffers) [from SD-16081 real Beeb with nothing attached: &FE60=255, &FE61=0].

## 2.5 Addresses and the other chips on the same page

- SHEILA map, for reference: &FE00 6845, &FE08 6850 ACIA, &FE10 serial ULA, &FE18 station ID/INTOFF, &FE20 video ULA/INTON, &FE30 ROMSEL, &FE40 system VIA, &FE60 user VIA, &FE80 8271, &FEA0 ADLC, &FEC0 ADC, &FEE0 Tube [from SM s.9 test listing, AUG s.25-27].
- The **ADC (&FEC0-&FEC2) is out of the first version**, but the OS still touches it. At reset it writes `&FEC0` (A=&0E). **Every 100 Hz tick it does `BIT &FEC0` and, if bit 6 (V) is set, runs the ADC "conversion done" code**: reads `&FEC2`/`&FEC1`, stores them, raises event 3 and writes `&FEC0` to start the next conversion [from ROM DE41-DE6C]. The default "number of ADC channels" byte `&024C` is **4**, copied from the OS defaults table at `&D98C` [from ROM D93F table, DA5B-DA64], so this code is live by default. AUG status bit 6 = 0 means busy, 1 = not busy; bit 7 = 1 means "not completed" [from AUG s.26.1.3]. **Recommendation, [inferring]: an unmapped ADC should read `&FEC0` with bit 6 = 0 (busy) so the OS never starts or finishes a conversion**; that keeps BASIC's `ADVAL` returning stale values rather than the OS running ADC code on garbage. Not verified by running it.
- CB1 flag (IFR4) is enabled in the OS's IER. With no ADC it never sets.

---

# 3. The keyboard matrix and the startup links

Scope: matrix, system VIA scan protocol, key table, startup links, BREAK, OS scan sequence. OS 1.20 only.
Tag shorthand: `[S1]` etc. means "from the URL in the Sources table". `[ROM x]` is read from `/tmp/bbcrom/os.rom` (mapped at `$C000`) with my own disassembler `/tmp/kbd/dis.py`. `[inferring ...]` and `[guessing - verify]` as normal.

## Sources and licence note

| Tag | Source | URL |
|---|---|---|
| S1 | tobylobster annotated MOS 1.20 reassembly, ch.2/10/11/14/15/17 (own downloads in `src/mos_s*.txt`) | https://tobylobster.github.io/mos/mos/S-s17.html (also S-s10, S-s15, S-s14, S-s2) |
| S2 | Advanced User Guide (AUG), PDF from bitshifters. Book pp.142-145 (key numbers, OSBYTE 120-122), p.246 (OSBYTE 255), pp.455-458 (App C), pp.489-490 (App J, Fig J.2 keyboard circuit) | https://raw.githubusercontent.com/bitshifters/bbc-documents/master/AUG/AUG.pdf |
| S3 | New Advanced User Guide (NAUG), same repo. s24.1.5 p.421 (OSBYTE 255), keyboard section | https://raw.githubusercontent.com/bitshifters/bbc-documents/master/AUG/NAUG.pdf |
| S4 | BeebWiki "Keyboard" (scan code matrix), via archive.org | https://web.archive.org/web/2024/https://beebwiki.mdfs.net/Keyboard |
| S5 | BeebMaster "Configuring a BBC Model B" (links, OSBYTE 255) | http://beebmaster.co.uk/BeebHelp/ConfigBBCB.html |
| S6 | Acorn BBC Microcomputer Service Manual, s3.1, s3.6, s3.7 | https://raw.githubusercontent.com/bitshifters/bbc-documents/master/B/BBCServiceManual.pdf |
| S7 | Derrick, A Hardware Guide for the BBC Microcomputer, s3.10, s3.14 | http://bbc.nvg.org/doc/A%20Hardware%20Guide%20for%20the%20BBC%20Microcomputer/bbc_hw_03.htm |
| S8 | Acorn BBC Model B circuit diagram 103,000/C (PNG, viewed as image) | https://raw.githubusercontent.com/bitshifters/bbc-documents/master/B/BBC-circuit-diagram.png |

- GPL code: I read NO jsbeeb or B-em source. Nothing here is copied or paraphrased from either. [from this task]
- S1 is itself a reassembly of Acorn's ROM; I used its comments and cross-checked every address and byte against the ROM image myself. No S1 code is reproduced.
- OS version check: ROM contains the string `OS 1.20` at `$E825` (OSBYTE 0 error) and `$F0C2` (*HELP); vectors NMI `$0D00`, RESET `$D9CD`, IRQ `$DC1C`. [ROM $FFFA-$FFFF, $E825, $F0C2]
- Other agents' files in `/tmp/bbc-facts/src/` were not used for any fact. My downloads are in `/tmp/bbc-facts/via-parts/src/`.

## (a) Matrix and system VIA protocol

### Wiring (keyboard PCB, Fig J.2)

| Item | Fact | Tag |
|---|---|---|
| Matrix size | 10 columns x 8 rows. 73 keys + 8 links wired in it. The two SHIFT keys share one cell. BREAK is not in it. | [S4] [S3 keyboard section] |
| Column select | PA0-PA3 go to IC1 (74LS163 counter, parallel load, clocked 1 MHz) which feeds IC3 (7445 BCD-to-decimal). Outputs 0-9 are the 10 columns, one pulled low at a time. Inputs 10-15 select no column. | [S2 Fig J.2 p.490] [S7 s3.10] |
| Row select | PA4-PA6 go to C,B,A of IC2 (74LS251 8:1 data selector). Inputs D0-D7 are the 8 rows. Row n = D(n). | [S2 Fig J.2] [S7 s3.10] |
| Key data out | IC2 output "W" goes to PA7 (PL1 pin 12). IC2 enable pin S (strobe) and IC1 LOAD are both driven by KB EN (PL1 pin 4). | [S2 Fig J.2] |
| Polarity | Rows have 10K pull-ups to +5V (R4-R11). A closed key in the selected column pulls its row line low. W is the inverted output, so a pressed key reads PA7=1. | [S2 Fig J.2]; PA7=1 means pressed is also what the OS code expects [ROM $F0F2-$F10E, $F02A-$F03A] |
| Terminology clash | The Service Manual calls the 7445 outputs "rows" and the 251 address "columns". The OS, AUG, BeebWiki and this sheet use column = PA0-3 (0-9), row = PA4-6 (0-7). | [S6 s3.7] vs [S2] [S4]. Trust the OS/AUG naming: it matches the ROM. |
| CA2 source | IC4 (74LS30, 8-input NAND) inputs are rows D1-D7 plus one input tied to +5V. Row 0 (D0) is NOT connected. Output goes high when any of rows 1-7 is low on the strobed column, so CA2 gets a rising edge. | [S2 Fig J.2 p.490, read from the image] [S1 ch17 s34: "first row ... do not generate interrupts"] [S4] |
| KB EN source | System VIA PB0-PB2 = address, PB3 = data into IC32 (74LS259 addressable latch). Latch bit 3 is "keyboard write enable" = KB EN. Latch /CLR is tied high, so the latch is not cleared by reset. | [S2 s23.2 p.419] [S8 crop of IC3/IC32, CLR 15 to +5V] |
| Diodes | D1-D10 (1N4116) sit on the columns at row-1 level; their purpose is not stated in any source I read. | [S2 Fig J.2] [guessing - verify: row-0 isolation] |

### Select and read

| Step | Hardware effect | Tag |
|---|---|---|
| Latch bit 3 = 0 (write ORB `$03`: PB0-2 = 3, PB3 = 0) | KB EN low. 74LS251 enabled; counter loads PA0-PA3 from the bus each 1 MHz clock. Manual mode. OS comment: "stop auto scanning of the keyboard". | [ROM $F0EB-$F0ED, $F02A-$F02C] [S1 ch17 s34] |
| Latch bit 3 = 1 (write ORB `$0B`: PB0-2 = 3, PB3 = 1) | KB EN high. Counter free-runs 0-15 at 1 MHz (walking zero over columns 0-9, nothing on 10-15). 251 output disabled. Auto-scan mode. OS comment: "enable auto scan of keyboard". | [ROM $F12E-$F130] [S1 ch17 s34] [S7 s3.10 "walking zero"] |
| DDRA = `$7F` (write `$FE43`) | PA0-PA6 outputs, PA7 input. | [ROM $F02F-$F031, $F0E6-$F0E8] |
| Write ORA = `col + row*16` (bit 7 ignored, it is an input) | Selects the cell. | [ROM $F034] [S1 ch17 s13 "write X to Port A"] |
| Read ORA/IRA | PA7 = 1 if the selected key (or closed link) is down. Low 7 bits read back the value driven. Result `$80+key` if pressed, else the key number. | [ROM $F037] [S1 ch17 s13] |
| Column 15 | Selects no column. All rows idle, so no key, no CA2. Used by the OS as "deselect". | [ROM $F0F0-$F0F2] [S1 ch17 s34] |
| Register used | All keyboard accesses use `$FE4F` (ORA/IRA without handshake), `$FE43`, `$FE40`, `$FE4D` (IFR), `$FE4E` (IER). Never `$FE41`. | [ROM $F034-$F037, $F0E8-$F0FD] |
| `$FE4F` not clearing CA2 | A read/write of `$FE41` would clear the CA2 flag; `$FE4F` does not. | [inferring from standard 6522 behaviour, other agent owns 6522] |
| PCR | System VIA PCR = `$04`: CA2 = positive active edge, input. ACR = `$60` (PA latch off). | [ROM $DA85-$DA8C: LDX #$04; STX $FE4C; ... LDA #$60; STA $FE4B] [S1 ch10 s13] |

### CA2 interrupt: when it fires

| Case | Behaviour | Tag |
|---|---|---|
| Rows | Only a key in rows 1-7 can raise CA2. Row 0 (SHIFT, CTRL, the 8 links) never does. | [S2 Fig J.2] [S4] [S1 ch17 s34] |
| Auto mode | The counter sweeps columns. When it reaches a column that has a pressed key in rows 1-7, IC4 pulses high, CA2 sets IFR bit 0 (if IER bit 0 is set, IRQ). Repeats every sweep (16 us) while the key is held. | [S7 s3.10] [S6 s3.7] |
| Held key | If the handler does not clear/disable it, the IRQ recurs continuously ("machine seizes up until the key is released"). | [S2 s13.11 "Intercepting interrupts" example text] |
| OS reaction | IRQ handler tests IFR bit 0 (`ROL` x4), calls KEYV with C=0,V=1. KEYV writes IER=`$01` (disables the CA2 interrupt), then runs a full manual scan. IFR bit 0 is cleared by writing `$01` to IFR. | [ROM $DE72-$DE7D (IRQ side; then LDA #1 / STA IFR at $DE6E), $EF02-$EF0B (KEYV)] [S1 ch11 s18, ch17 s3] |
| Re-enable | `tidyUpAfterKeyboardProcessing` writes IER=`$81` once no key is pressed (`$EC` and `$ED` both 0). | [ROM $EEDA-$EEE4] |
| Manual mode edge | Manual scan: write col 15, write IFR=1, write col X, test IFR bit 0. CA2 is the level "any of rows 1-7 down in selected column"; IFR bit 0 sets on its 0 to 1 edge. | [ROM $F0F0-$F100] [inferring from schematic + code] |
| Emulation rule | CA2 level = (manual mode) any_key_down(col = PA&15, rows 1-7); (auto mode) any_key_down(any col 0-9, rows 1-7), edge within 16 us. | [inferring from the rows above] |
| NAUG disagreement | NAUG says the OS "reads the column counter" after the interrupt. On the Model B the counter outputs go only to the 7445 (not readable) and OS 1.20 never reads it. Trust: schematic + ROM. | [S3 keyboard section] vs [S2 Fig J.2] [ROM $F00F-$F022] |

### Key numbers

| Item | Fact | Tag |
|---|---|---|
| Internal key number | `internal = column + (row << 4)`. Same value as what is written to port A (bit 7 clear). Examples: Q = col 0 row 1 = `$10`; SPACE = col 2 row 6 = `$62`. | [S1 ch17 s34 table] [S2 p.142] [ROM $F0D1-$F11E: loop adds `$10` per row] |
| Scan order | OS scans columns 9 down to 0; in each, rows from low to high (`ADC #$10` until bit 7). | [ROM $F0E1-$F124] |
| INKEY value | `INKEY byte = internal EOR $FF`, i.e. INKEY(-n) with n = internal + 1 (SHIFT = -1, CTRL = -2, Q = -17). AUG states the EOR $FF rule. | [S2 p.143] [S2 App C p.456-458] |
| Negative INKEY path | OSBYTE 129 with Y=`$FF`, X<0: `X EOR $7F` (= internal EOR $80), then KEYV with N set so the single key is tested; result bit 7 to carry. | [ROM $E721-$E731] [S1 ch15 s16] |
| OSBYTE 121 | X = internal EOR `$80` tests one key (exit X bit 7 set if down). X positive scans from that key upward; returns key number or `$FF`. | [S2 p.144] [ROM $F0D1-$F0D7] |
| OSBYTE 122 | Same as 121 with X = `$10` (skips row 0). | [ROM $F0CD] [S2 p.145] |
| Zero page | `$EC` = last key + `$80`, `$ED` = first key + `$80`, `$EE` = key to ignore in scans. | [S2 p.142, p.144] [ROM $EEDC-$EEDE] |

## (b) Full key-to-matrix table

Layout as a grid (each cell is internal key number = col + row*16):

| row \ col | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | SHIFT | CTRL | link b7 | link b6 | link b5 | link b4 | link b3 | link b2 | link b1 | link b0 |
| 1 | Q | 3 | 4 | 5 | f4 | 8 | f7 | - | ^ | LEFT |
| 2 | f0 | W | E | T | 7 | I | 9 | 0 | _ | DOWN |
| 3 | 1 | 2 | D | R | 6 | U | O | P | [ | UP |
| 4 | CAPS LOCK | A | X | F | Y | J | K | @ | : | RETURN |
| 5 | SHIFT LOCK | S | C | G | H | N | L | ; | ] | DELETE |
| 6 | TAB | Z | SPACE | V | B | M | , | . | / | COPY |
| 7 | ESCAPE | f1 | f2 | f3 | f5 | f6 | f8 | f9 | \ | RIGHT |

Checks performed (all passed, 0 mismatches):
- ROM key-data tables (7 tables x 10 bytes, `$F03B`, `$F04B`, `$F05B`, `$F06B`, `$F07B`, `$F08B`, `$F09B` = rows 1-7, index = column) decoded with my script `/tmp/kbd/cross.py`, byte values mapped to names. [ROM $F03B-$F0A4]
- 71 of 72 matrix keys compared with AUG Appendix C (internal number, decimal and hex, and INKEY byte = internal EOR $FF) by script: all agree. [S2 pp.456-458] (`-` was matched by hand: the printed minus sign broke my parser. `$17`, INKEY -24 = `$E8`. Agrees.)
- All 70 positions in rows 1-7 compared with the BeebWiki scan code matrix (`&10-&79`) by script (key name match, my parser `/tmp/kbd/cross.py` plus a second ad hoc script): all agree. [S4]
- Whole grid also compared by eye with the keyboard schematic in AUG Fig J.2 (cols 0-9 left to right, rows D0-D7), including row 0 = 2x SHIFT, CTRL, then SW2 positions. [S2 p.490]

Three independent sources (ROM bytes, AUG, BeebWiki) plus the schematic agree on every key.

Full list, sorted by internal number. "ROM code byte" is the unshifted code at the table address (`$F03B + 16*(row-1) + col`):

| Key | col | row | internal | INKEY(-n), n | INKEY byte | ROM table addr | ROM code byte |
|---|---|---|---|---|---|---|---|
| SHIFT | 0 | 0 | $00 | 1 | $FF | none (read via KEYV test) | - |
| CTRL | 1 | 0 | $01 | 2 | $FE | none (read via KEYV test) | - |
| Q | 0 | 1 | $10 | 17 | $EF | $F03B | $71 |
| 3 | 1 | 1 | $11 | 18 | $EE | $F03C | $33 |
| 4 | 2 | 1 | $12 | 19 | $ED | $F03D | $34 |
| 5 | 3 | 1 | $13 | 20 | $EC | $F03E | $35 |
| f4 | 4 | 1 | $14 | 21 | $EB | $F03F | $84 |
| 8 | 5 | 1 | $15 | 22 | $EA | $F040 | $38 |
| f7 | 6 | 1 | $16 | 23 | $E9 | $F041 | $87 |
| - | 7 | 1 | $17 | 24 | $E8 | $F042 | $2D |
| ^ | 8 | 1 | $18 | 25 | $E7 | $F043 | $5E |
| LEFT | 9 | 1 | $19 | 26 | $E6 | $F044 | $8C |
| f0 | 0 | 2 | $20 | 33 | $DF | $F04B | $80 |
| W | 1 | 2 | $21 | 34 | $DE | $F04C | $77 |
| E | 2 | 2 | $22 | 35 | $DD | $F04D | $65 |
| T | 3 | 2 | $23 | 36 | $DC | $F04E | $74 |
| 7 | 4 | 2 | $24 | 37 | $DB | $F04F | $37 |
| I | 5 | 2 | $25 | 38 | $DA | $F050 | $69 |
| 9 | 6 | 2 | $26 | 39 | $D9 | $F051 | $39 |
| 0 | 7 | 2 | $27 | 40 | $D8 | $F052 | $30 |
| _ | 8 | 2 | $28 | 41 | $D7 | $F053 | $5F |
| DOWN | 9 | 2 | $29 | 42 | $D6 | $F054 | $8E |
| 1 | 0 | 3 | $30 | 49 | $CF | $F05B | $31 |
| 2 | 1 | 3 | $31 | 50 | $CE | $F05C | $32 |
| D | 2 | 3 | $32 | 51 | $CD | $F05D | $64 |
| R | 3 | 3 | $33 | 52 | $CC | $F05E | $72 |
| 6 | 4 | 3 | $34 | 53 | $CB | $F05F | $36 |
| U | 5 | 3 | $35 | 54 | $CA | $F060 | $75 |
| O | 6 | 3 | $36 | 55 | $C9 | $F061 | $6F |
| P | 7 | 3 | $37 | 56 | $C8 | $F062 | $70 |
| [ | 8 | 3 | $38 | 57 | $C7 | $F063 | $5B |
| UP | 9 | 3 | $39 | 58 | $C6 | $F064 | $8F |
| CAPS LOCK | 0 | 4 | $40 | 65 | $BF | $F06B | $01 |
| A | 1 | 4 | $41 | 66 | $BE | $F06C | $61 |
| X | 2 | 4 | $42 | 67 | $BD | $F06D | $78 |
| F | 3 | 4 | $43 | 68 | $BC | $F06E | $66 |
| Y | 4 | 4 | $44 | 69 | $BB | $F06F | $79 |
| J | 5 | 4 | $45 | 70 | $BA | $F070 | $6A |
| K | 6 | 4 | $46 | 71 | $B9 | $F071 | $6B |
| @ | 7 | 4 | $47 | 72 | $B8 | $F072 | $40 |
| : | 8 | 4 | $48 | 73 | $B7 | $F073 | $3A |
| RETURN | 9 | 4 | $49 | 74 | $B6 | $F074 | $0D |
| SHIFT LOCK | 0 | 5 | $50 | 81 | $AF | $F07B | $02 |
| S | 1 | 5 | $51 | 82 | $AE | $F07C | $73 |
| C | 2 | 5 | $52 | 83 | $AD | $F07D | $63 |
| G | 3 | 5 | $53 | 84 | $AC | $F07E | $67 |
| H | 4 | 5 | $54 | 85 | $AB | $F07F | $68 |
| N | 5 | 5 | $55 | 86 | $AA | $F080 | $6E |
| L | 6 | 5 | $56 | 87 | $A9 | $F081 | $6C |
| ; | 7 | 5 | $57 | 88 | $A8 | $F082 | $3B |
| ] | 8 | 5 | $58 | 89 | $A7 | $F083 | $5D |
| DELETE | 9 | 5 | $59 | 90 | $A6 | $F084 | $7F |
| TAB | 0 | 6 | $60 | 97 | $9F | $F08B | $00 |
| Z | 1 | 6 | $61 | 98 | $9E | $F08C | $7A |
| SPACE | 2 | 6 | $62 | 99 | $9D | $F08D | $20 |
| V | 3 | 6 | $63 | 100 | $9C | $F08E | $76 |
| B | 4 | 6 | $64 | 101 | $9B | $F08F | $62 |
| M | 5 | 6 | $65 | 102 | $9A | $F090 | $6D |
| , | 6 | 6 | $66 | 103 | $99 | $F091 | $2C |
| . | 7 | 6 | $67 | 104 | $98 | $F092 | $2E |
| / | 8 | 6 | $68 | 105 | $97 | $F093 | $2F |
| COPY | 9 | 6 | $69 | 106 | $96 | $F094 | $8B |
| ESCAPE | 0 | 7 | $70 | 113 | $8F | $F09B | $1B |
| f1 | 1 | 7 | $71 | 114 | $8E | $F09C | $81 |
| f2 | 2 | 7 | $72 | 115 | $8D | $F09D | $82 |
| f3 | 3 | 7 | $73 | 116 | $8C | $F09E | $83 |
| f5 | 4 | 7 | $74 | 117 | $8B | $F09F | $85 |
| f6 | 5 | 7 | $75 | 118 | $8A | $F0A0 | $86 |
| f8 | 6 | 7 | $76 | 119 | $89 | $F0A1 | $88 |
| f9 | 7 | 7 | $77 | 120 | $88 | $F0A2 | $89 |
| \ | 8 | 7 | $78 | 121 | $87 | $F0A3 | $5C |
| RIGHT | 9 | 7 | $79 | 122 | $86 | $F0A4 | $8D |

Notes on the code bytes [ROM $F03B-$F0A4]:
- Letters are stored lowercase (`$61`-`$7A`). Digits and punctuation are the unshifted ASCII. `RETURN` = `$0D`, `SPACE` = `$20`, `DELETE` = `$7F`, `ESCAPE` = `$1B`, `TAB` = `$00` (special-cased as "TAB code", default 9), `CAPS LOCK` = `$01`, `SHIFT LOCK` = `$02` (both tested by internal number `$C0`/`$D0` in the OS, not by these bytes).
- f0-f9 are `$80`-`$89`. COPY = `$8B`, LEFT = `$8C`, RIGHT = `$8D`, DOWN = `$8E`, UP = `$8F`. These are soft key 11-15 codes. [S1 ch14 s32]
- AUG Appendix C lists the delivered codes as COPY `$87`, LEFT `$88`, RIGHT `$89`, DOWN `$8A`, UP `$8B` (cursor editing mode). The OS converts via the function/cursor key base table (OSBYTE 225-232). Table bytes and delivered codes differ; both are right. [S2 p.458] [S1 ch14 s32]
- The `_` key is stored `$5F`; SHIFT gives `$60` (the pound sign in the BBC set). Matches the schematic legend "pound / _". [ROM $EA9C-$EABE] [S2 Fig J.2] [S4]
- Both SHIFT keys are one matrix cell (col 0, row 0), so they cannot be told apart. [S1 ch17 s34] [S4]
- OS 1.20 has NO separate shift or CTRL translation tables. SHIFT and CTRL are done by code on the base character (`implementSHIFT` at `$EA9C`, `implementCTRLCodes` at `$EABF`), with CAPS/SHIFT LOCK handled at `$EFB5-$EFC6`. The "normal/shift/ctrl tables near $EF71" in the brief do not exist on this OS. What is at `$EF71` is code. [ROM $EA9C-$EAD1, $EF91-$EFE8] [S1 ch15 s65-66, ch17 s9]
- Shift rules from the code: `0`, `@`, `DELETE` unchanged by SHIFT; chars `$21-$3F` swap with their SHIFT partner by flipping bit 4; letters flip bit 5; `_` and pound swap. CTRL: `@`-`_` and lowercase letters give 0-31, `32-63` unchanged, `$7F` unchanged, pound gives 31. [ROM $EA9C-$EAD1] [S1 ch15 s65-66]

## (c) Startup options links (8-way switch, SW2)

### Where they are and how they read

| Item | Fact | Tag |
|---|---|---|
| Location | 8 links (or a DIP switch) at the front right of the keyboard PCB. Each is a normally-open switch between row line D0 and one of columns 2-9. They are matrix cells in ROW 0, COLUMNS 2-9. | [S2 Fig J.2 p.490] [S1 ch17 s34] |
| Internal key numbers | `$02` to `$09` (col 2-9, row 0). AUG calls them "bit 7" at `$02` down to "bit 0" at `$09`. | [S2 p.142, p.458] [S1 ch17 s34] |
| Mapping | startup bit n = internal key `$09 - n`. bit 0 = `$09` (col 9), bit 7 = `$02` (col 2). | [S2 App C p.458] [ROM $DA11-$DA20] |
| Read with keyboard enabled | The reset code calls the same key-test routine, so latch bit 3 = 0 (keyboard enabled), DDRA = `$7F`, column in PA0-3, row 0 in PA4-6 = 0. | [ROM $DA11-$DA15, $F02A-$F03A] |
| Closed link | Link made, column strobed: row 0 low, PA7 = 1, "pressed". Open link: PA7 = 0. | [inferring from S2 Fig J.2 + ROM `CPX #$80` carry logic at $DA15] |
| Stored polarity | The OS inverts the byte: `startUpOptions = NOT(links)`. So link made = bit 0; link open = bit 1. | [ROM $DA39-$DA3D: `LDA $FC; EOR #$FF; STA $028F`] [S1 ch10 s7] [S5: "unmade link, bit SET"] |
| Where kept | `startUpOptions` = `$028F`. Read/write with OSBYTE 255 (`*FX255`): new = (old AND Y) EOR X. | [S1 ch2 `$028F`] [S2 p.246] [S3 s24.1.5 p.421] |
| When read | Every reset (power-on, BREAK, CTRL-BREAK) runs the 9-key read loop at `$DA11-$DA1B` (keys 9 down to 1). But `startUpOptions` is only written on power-on and hard BREAK (CTRL held). Soft BREAK leaves the stored byte alone (so `*FX255,n` survives a soft BREAK). | [ROM $DA2A-$DA3D] [S5 "new value takes effect after the next soft reset"] |
| Reading routine | `$F02A` (key test). Called from the reset loop at `$DA12` with A = X = 9..1. Same routine as every other key test. | [ROM $F02A, $DA12] |

### Bit meanings (startup byte, after inversion)

| Bit | Internal key / col | Meaning | Tag |
|---|---|---|---|
| 0-2 | `$09`,`$08`,`$07` (cols 9,8,7) | Start-up screen MODE = the 3-bit value (bit 0 is LSB). | [S2 p.246] [S3 p.421] [S1 ch10 s17: `LDA startUpOptions; JSR initialiseScreenOnReset`] |
| 3 | `$06` (col 6) | If CLEAR: reverse SHIFT-BREAK. Set (default): SHIFT-BREAK boots the disc, BREAK alone does not. Clear: BREAK alone boots, SHIFT-BREAK does not. | [S2 p.246] [ROM $DB8E-$DB99] |
| 4-5 | `$05`,`$04` (cols 5,4) | Disc drive timings (below). | [S2 p.246] [S5] |
| 6 | `$03` (col 3) | Unused by the OS. | [S2 p.246] [S1 ch2] |
| 7 | `$02` (col 2) | DNFS filing system: set = DFS, clear = NFS. (AUG p.246 and NAUG p.421 agree.) | [S2 p.246] [S3 p.421] [S5] |

8271 timings (bits 5,4): `00` = step 4 ms, settle 16 ms, head load 0 ms. `01` = 6 / 16 / 0. `10` = 6 / 50 / 32. `11` = 24 / 20 / 64. In AUG's table the column "link 3 link 4" holds 1 for a made link, which is the inverse of the bit, consistent with the polarity above: bit pair `00` is printed as links `1,1`. [S2 p.246] [S3 p.421] [S5]. 1770/1772 timings differ and are only relevant if you emulate those controllers. [S2 p.246] [S3 p.421]

### Defaults

| Item | Fact | Tag |
|---|---|---|
| Supplied state | All links open (unmade). | [S5] [guessing - verify: no Acorn source read that says what the factory shipped. S5 says "default state is each link unmade".] |
| Resulting byte | `$FF` = MODE 7, SHIFT-BREAK boots, DFS, disc timings `11` (8271: 24 ms step, 20 ms settle, 64 ms head load). | [S5 "default setting ... all bits set, 255"] [inferring from the polarity rows above] |
| Row-0 reads with all open | cols 2-9 read PA7 = 0. SHIFT and CTRL read 0 (not pressed). | [inferring] |
| Emulation | Offer a configurable startup byte B. Row-0 col (9-n) must read PA7 = NOT bit n of B. E.g. B=`$FF`: PA7 = 0 for cols 2-9. For a MODE 0 start (bits 0-2 = 0): PA7 = 1 on cols 9,8,7. | [inferring] |

### Link numbering conflict

| Source | Numbering |
|---|---|
| Schematic SW2 labels (Fig J.2) | positions 8,7,6,5,4,3,2,1 under cols 2-9, so label 1 = col 9 = bit 0. [S2 p.490] |
| S5, S1, AUG's disc-timing table | link 1 = bit 7 ... link 8 = bit 0 (link 3 = bit 5, link 4 = bit 4). [S5] [S1 ch17 s34 "dip 8-6 = bits 0-2"] [S2 p.246] |
Trust: bit-to-column mapping from the ROM and schematic. The printed link/DIP numbers differ by keyboard type (S4 notes DIP orientation differs by type) and do not matter for an emulator. [S4]

## (d) BREAK, SHIFT and CTRL at reset

| Item | Fact | Tag |
|---|---|---|
| BREAK on matrix? | No. It is a switch on the keyboard PCB (SW1) that pulls the reset circuit's trigger low. Not in the 10x8 matrix. | [S2 Fig J.2 SW1 to PL1 pin 2 "RST"] [S1 ch17 s34] [S4] [S3: "direct link to the 6502 reset line"] |
| Reset circuit | LM555 (IC16) monostable gives RST / RST-bar for BREAK and power-up. A separate C10/R20 network gives RSTA, which goes low only at power-up. | [S6 s3.1] [S7 s3.14] [S8 crop: IC16 555, outputs "KBD RST SWITCH", RST-bar, RST] |
| What RST resets | The 6502 (pin 40). "Throughout the remaining circuitry" except the system VIA. | [S6 fault-finding checklist: "Check that the reset line on the 6502A (pin 40) is high, and only goes low when BREAK is pressed"] [S7 s3.14] |
| What RSTA resets | System VIA (IC3) only, and only at power-up. BREAK does NOT reset the system VIA, so its registers, and the addressable latch (CLR tied high), survive BREAK. | [S6 s3.1] [S7 s3.14] [S8 IC32 CLR] |
| How the OS tells them apart | At `$D9CD` it reads the system VIA IER: after power-on IER bits 0-6 are all 0, after BREAK at least one is set. | [S1 ch10 s3] [ROM $D9D7-$D9DC: `LDA $FE4E; ASL A; PHA; BEQ`] [S6 s3.1] |
| BREAK vector | Reset vector `$FFFC` = `$D9CD`. | [ROM $FFFC-$FFFD] |
| SHIFT held at reset | After startup, `OSBYTE 118` tests SHIFT (KEYV C=0,V=0), giving N set for SHIFT. Then `(N<<3) EOR startUpOptions` AND `$08`: result 0 = run `*/!BOOT`; 8 = do not. With default bit 3 = 1: SHIFT-BREAK boots, BREAK does not. | [ROM $DB88-$DB99, $E9D9-$E9E9] [S1 ch10 s22, ch15 s56] |
| CTRL held at reset | CTRL (key `$01`) is the last read of the 9-key reset loop, ends up in carry. Carry set at `$DA2F` = hard reset (CTRL-BREAK): power-on type 1, hard reset type 2, soft (carry clear) type 0. Hard reset also reloads `startUpOptions` and clears more OS variables. | [ROM $DA11-$DA40] [S1 ch10 s7-8] |
| Power on | `lastResetType` = 1. CTRL-BREAK = 2. BREAK = 0. | [ROM $DA2B-$DA36] [S1 ch10 s8] |
| SHIFT at power-on | Same test, same effect. | [inferring] |
| BREAK is not scanned by KEYV | The OS never reads a "BREAK key" bit; there is no matrix cell for it. | [S1 ch17 s34] |

## (e) OS keyboard scan sequence (what the emulator must support)

### Key test (`$F02A`, used by OSBYTE 129 negative, SHIFT/CTRL test, reset reads, repeat tests)

| Addr | Instruction | Effect | Tag |
|---|---|---|---|
| `$F02A` | `LDY #$03; STY $FE40` | ORB = 3: latch bit 3 := 0 (keyboard enabled / manual). | [ROM $F02A-$F02C] |
| `$F02F` | `LDY #$7F; STY $FE43` | DDRA = `$7F`. | [ROM $F02F-$F031] |
| `$F034` | `STX $FE4F` | Port A (no handshake) = X. Selects col = X&15, row = (X>>4)&7. | [ROM $F034] |
| `$F037` | `LDX $FE4F` | Read PA7 (key) and echo of the low bits. | [ROM $F037] |
| `$F03A` | `RTS` | X = `$80 + key` if pressed, else X = key (bit 7 clear). Callers test N. Auto scan is NOT re-enabled here. | [ROM $F03A] [S1 ch17 s13] |

### Full keyboard scan (`$F0D1`, loop `$F0E3`)

| Step | Code | Effect | Tag |
|---|---|---|---|
| 1 | `LDX #9` | column counter 9 down to 0 | [ROM $F0E1] |
| 2 | `JSR $F129` (`JSR $F12E; CLI; SEI; ` then `LDA #$0B; STA $FE40; TXA; RTS`) | re-enable auto scan, allow IRQs for a moment | [ROM $F129-$F134] |
| 3 | `LDA #$7F; STA $FE43` | DDRA = `$7F` | [ROM $F0E6-$F0E8] |
| 4 | `LDA #$03; STA $FE40` | stop auto scan (latch bit 3 := 0) | [ROM $F0EB-$F0ED] |
| 5 | `LDA #$0F; STA $FE4F` | port A = 15: no column selected | [ROM $F0F0-$F0F2] |
| 6 | `LDA #$01; STA $FE4D` | clear IFR bit 0 (CA2) | [ROM $F0F5-$F0F7] |
| 7 | `STX $FE4F` | select column X | [ROM $F0FA] |
| 8 | `BIT $FE4D; BEQ next` | IFR bit 0 set only if a key in rows 1-7 of column X is down | [ROM $F0FD-$F100] |
| 9 | row loop: `A = X` (col), `CMP start; BCC skip; STA $FE4F; BIT $FE4F; BPL skip` | write col + row*16, test PA7. `ADC #$10` per row, ends when bit 7 set (past row 7). | [ROM $F102-$F121] |
| 10 | on hit: return A as key number (rollover logic compares with the previous key) | | [ROM $F110-$F127] |
| 11 | end: `JSR $F129` | re-enable auto scan (`LDA #$0B; STA $FE40`) | [ROM $F126-$F134] |

Rows 1-7 are scanned this way; row 0 is never found by the full scan, so SHIFT/CTRL/links must be read with the single-key test. [S1 ch17 s34] [ROM $F0D1-$F0D7: negative X goes to `$F02A`]

### Emulator must support (from the above)

- System VIA: `$FE40` (ORB, PB0-3 outputs) to drive latch bit 3; `$FE43` DDRA; `$FE4F` ORA/IRA no-handshake; `$FE4D` IFR bit 0; `$FE4E` IER bit 0; PCR `$04`. [ROM, see rows above]
- Addressable latch: write ORB; bit = PB0-2, value = PB3. KB EN = latch bit 3 (active low = keyboard enabled for read). [S2 s23.2]
- PA7 read in manual mode as in (a). CA2 edge as in (a). IRQ when IFR0 and IER0.
- Row-0 cols 2-9 return the startup link bits (section c). Cols 10-15 return nothing.
- The IRQ path runs a full scan on each key interrupt, so a held key keeps re-raising CA2 in auto mode until IER bit 0 is cleared by the handler. [S2 s13.11]

### When a typed key reaches the buffer, and when it repeats (added 3 Oct 2026, task 10)

| Item | Fact | Tag |
|---|---|---|
| New key | On a new key the OS sets the countdown at `$E7` to 1 and copies the auto-repeat delay from `$0254` to `$02CA`. | [from ROM `$F01F-$F026`] |
| Into the buffer | On each 100 Hz tick, while a key is down, the countdown at `$E7` is decremented; at zero the character goes in and the countdown is reloaded from `$02CA`, which then takes the repeat rate from `$0255`. So a new key's character is buffered at the first tick after it is seen, within 10 ms. | [from ROM `$EF55-$EF67`] |
| Defaults | Auto-repeat delay `$0254` = `$32` (50 cs), repeat rate `$0255` = `$08` (8 cs) from power on: the reset code copies the OS's default table (`$D940` on) to `$0200` on (`$DA5B-$DA62`), and the bytes at `$D994`/`$D995` are `32 08`. `*FX12,0` sets the same two values (`$E98E-$E993`). | [from ROM] |
| Keyboard status | `$025A` defaults to `$20` (`$D99A`), and capitals come from the letter keys without SHIFT: CAPS LOCK is on from power on. | [from ROM; the CAPS LOCK reading confirmed by running, task 10's typed BASIC] |
| For an emulator's typing | A key held longer than one tick and shorter than the 50 cs delay types once. The test project holds 40 ms and rests 40 ms (`BbcSession.HoldCycles`, `RestCycles`). | [inferring from the rows above] |

## Could NOT establish

| Item | Why / what I recommend | Tag |
|---|---|---|
| PA7 level when the keyboard is NOT selected (latch bit 3 = 1) | No source states it. Hardware: 74LS251 S pin is high, so its output is disabled (tri-state, so PA7 is undriven by it). Whatever else sits on the slow bus decides, and I found no pull-up on PA7 in the schematic crops I read. Keyboard code only reads PA7 after writing `$03` to ORB (keyboard enabled). The only other reader of port A is the speech routine at `$EE82`, which sets DDRA = 0 and reads the byte with the keyboard latch bit untouched (normally 1 = auto scan), so PA7 then comes from the TMS5220 if one is fitted. With no speech board nothing drives it. Recommend: return 0 for PA7 when the keyboard is not selected and no speech device is emulated. OS 1.20 does not depend on it (it checks PB7 for speech presence at `$DB11-$DB14`, not PA7). | [guessing - verify] [ROM $EE82-$EEAA; S1 ch16 s25; ROM $DB11-$DB14] |
| Exact CA2 timing in auto mode | Real hardware gives one edge per 16 us sweep while a key is held. No timing in any source beyond "1 MHz counter". Recommend: raise IFR bit 0 immediately and again after each IFR clear while a key (rows 1-7) is down and latch bit 3 = 1. | [inferring from S7 s3.10, S6 s3.7] |
| Purpose of diodes D1-D10 | Not stated in AUG, Service Manual or Hardware Guide. Not needed for emulation (no ghosting). | [S2 Fig J.2] |
| Complete list of chips on RST | I verified the 6502 reset line (S6) and that the system VIA is excluded (RSTA). The circuit diagram image is too dense for me to trace every other consumer. Likely consumers (6845, user VIA, 8271, ACIA/serial, 1 MHz bus, Tube): [guessing - verify]. For keyboard emulation the only fact that matters is the system VIA keeps its state across BREAK. | [S7 s3.14] [S8] |
| Factory-shipped link state | S5 says default is all unmade. No Acorn manual I read says what the shipped machine had. Disc timing bits especially may have varied with drive type. | [S5] [guessing - verify] |
| Link/DIP physical numbering | Sources disagree (see the table in (c)). Irrelevant for an emulator, which only needs bit n = col 9-n. | [S2 p.490] vs [S5] |
| What happens to PA0-PA6 reads when DDRA bits are inputs | Other agent owns the 6522. The keyboard code only uses DDRA = `$7F`. | [ROM] |
| Whether both columns and rows keep their meanings on B+ / Master | Out of scope. BeebWiki says the Master matrix is 13x8 and differs. Do not reuse this table for a Master. | [S4] [S3] |
| Exact reset pulse width of the LM555 | Not read. Not needed for a software emulator. | - |

---

# 4. The SN76489 as the BBC drives it

Date of research: 2 Oct 2026. Nothing in any repo was edited. Downloaded sources and my scripts are in `/tmp/bbc-facts/via-parts/src-sn/`.

## Source key

Tags below use these keys. Each key stands for the full URL or path shown.

| Key | Source |
|---|---|
| DS | TI SN76489AN datasheet, scanned PDF, https://map.grauw.nl/resources/sound/texas_instruments_sn76489an.pdf (local `sn76489an.pdf`; text read by OCR with tesseract, so the Table 3 bit patterns were garbled and are corroborated by SMS) |
| SMS | https://www.smspower.org/Development/SN76489 (Maxim) |
| SD-BUS | https://stardot.org.uk/forums/viewtopic.php?t=30838 (scarybeasts, Apr 2025: bus-side analysis, BBC circuit diagram) |
| SD-SAMP | https://stardot.org.uk/forums/viewtopic.php?t=17537 (sampled sound tests, 2019; Tom Seddon, simonm, scarybeasts) |
| SD-BEST | https://stardot.org.uk/forums/viewtopic.php?t=15071 (best sample playback, 2018; SarahWalker, 1024MAK) |
| BLOG | https://scarybeastsecurity.blogspot.com/2020/06/sampled-sound-1980s-style-from-sn76489.html (scope traces of a real BBC chip) |
| HWG | http://bbc.nvg.org/doc/A%20Hardware%20Guide%20for%20the%20BBC%20Microcomputer/bbc_hw_03.htm s.3.17 |
| 8BS | https://8bs.com/inbbcmapped.htm (BBC B motherboard text, IC3 / IC18 / IC32) |
| ROM | `/tmp/bbcrom/os.rom`, 16 KB at &C000-&FFFF. Reset vector at &FFFC is &D9CD, so this is OS 1.20. Disassembled with my own script `src-sn/dis.py`. |
| JSB, BEM | jsbeeb `src/soundchip.js` and b-em `src/sn76489.c` (both GPL). See "GPL cross-check" below. |

## GPL cross-check (disclosure)

I read jsbeeb `soundchip.js` and b-em `sn76489.c` from raw.githubusercontent.com. I used them only to cross-check numbers: LFSR width, taps and reset value, how a tone period of 0 is treated, and the volume formula. I copied and paraphrased no code. Lines tagged JSB or BEM are cross-checks only. They are not primary evidence.

## 1. Write protocol through the system VIA

| Fact | Value | Source |
|---|---|---|
| Chip fitted | SN76489AN (IC18). Tom Seddon's Model B has one; his Master has an SN76496AN. | [from HWG s.3.17], [from SD-SAMP] |
| Data path | System VIA (IC3) port A is the "slow data bus" shared by keyboard, sound chip and speech chips. | [from 8BS], [from HWG] |
| Strobe path | System VIA port B drives an addressable latch (74LS259, IC32). It supplies the read and write strobes. Latch bits 6 and 7 are the caps and shift lock LEDs. | [from 8BS] |
| Sound WE | Latch bit 0, active low, wired to the chip's /WE (pin 5). | [from SD-BUS], [from ROM EB2C/EB36: writes &00 then &08] |
| Latch addressing | ORB (&FE40): PB0-PB2 = latch address, PB3 = data bit. So &00 = latch bit 0 cleared (WE low), &08 = latch bit 0 set (WE high). | [inferring from ROM EB2C, EB36 and the &03/&0B keyboard writes at F0ED/F12E] |
| CE (pin 6) | Tied to 0 V, always enabled. | [from SD-BUS, reading the BBC circuit diagram] |
| READY (pin 4) | Wired to 0 V on the BBC. Software cannot see it. The CPU is not stalled. A BBC must wait the full ~8 us in software. | [from SD-BUS], [from SD-BEST] |
| Chip load time | About 32 clock cycles. At 4 MHz that is 8 us. | [from DS], [from SD-BEST] |
| Response latency measured | WE low to audible response was 3.7-6.8 us on one boot and 5.0-8.0 us on another, after a 1 us latch delay. Software that held WE low reliably needed a bus change exactly 9 us after WE fell, then every 16 us. | [from SD-BUS] |
| Bit order on the wire | The TI datasheet calls the first bit D0 (MSB, pin 3) and D7 the LSB (pin 10). The OS never reverses bits: it writes conventional bytes (&9F, &E0|n ...) straight to ORA. So PA7 meets the chip's "first bit" and bit 7 of the 6502 byte is the 1 = latch flag. | [from DS pin table], [from ROM EB28 and ED7E-ED85], [inferring the wiring] |
| Emulator consequence | The emulator treats the PA byte as a normal 6502 byte. Bit 7 = 1 means latch/data byte. No reversal. | [inferring from the two rows above] |

### The OS write routine (OS 1.20)

Entry `&EB21` is "write A to the sound chip". Entry `&EB22` skips the PHP (the caller has already done one). Disassembled from `os.rom`.

| Addr | Bytes | Instruction | Effect |
|---|---|---|---|
| EB21 | 08 | PHP | save I flag |
| EB22 | 78 | SEI | |
| EB23 | A0 FF | LDY #&FF | |
| EB25 | 8C 43 FE | STY &FE43 | DDRA = &FF, all of port A is output |
| EB28 | 8D 4F FE | STA &FE4F | ORA no-handshake = data byte on PA0-7 |
| EB2B | C8 | INY | Y = 0 |
| EB2C | 8C 40 FE | STY &FE40 | latch bit 0 = 0, WE low |
| EB2F | A0 02 | LDY #2 | |
| EB31 | 88 / D0 FD | DEY / BNE | 2 loops, 9 cycles |
| EB34 | A0 08 | LDY #8 | |
| EB36 | 8C 40 FE | STY &FE40 | latch bit 0 = 1, WE high |
| EB39 | A0 04 | LDY #4 | |
| EB3B | 88 / D0 FD | DEY / BNE | 4 loops, 21 cycles of padding after WE high |
| EB3E | 28 | PLP | |
| EB3F | 60 | RTS | |

All entries above are [from ROM EB21-EB3F].

| Timing fact | Value | Source |
|---|---|---|
| WE low to WE high | 17 CPU cycles at 2 MHz, about 8.5 us, between the two STY writes (2 + 9 + 2 + 4). The VIA sits on the 1 MHz bus, so real time is somewhat longer. The stretch is not modelled in this count. | [computed from ROM EB2F-EB36] |
| Padding after WE high | 21 cycles, about 10.5 us, before the next write can start | [computed from ROM EB39-EB3E] |
| Shortest WE-low pulse seen in software | 14 CPU cycles (Repton 2, at &2CAA, per the jsbeeb comment). Cross-check only. | [from JSB, `minCyclesWELow`] |
| Reset sets WE high | Reset code writes ORB = &0E, &0D ... &08 after DDRB = &0F. That sets latch bits 6 down to 0 to 1, so sound WE starts inactive (high). | [from ROM D9FF-DA0E] |

### What an emulator must do

| Item | Recommendation | Source |
|---|---|---|
| Minimum | Take the byte on PA when latch bit 0 goes 0 to 1 (WE rising), or at WE falling. OS code holds the bus stable for the whole window, so both give the same result. | [inferring from ROM EB28-EB36] |
| Re-latch | If WE stays low, the real chip re-takes the byte about every 16 us (8 us busy, 8 us idle). That is harmless for tone and volume bytes. It resets the noise LFSR every time for noise bytes. | [from SD-BUS], [from SD-SAMP, Tom Seddon's WE0-NOISE test: silence with WE held low] |
| Do not write on every VIA tick | Applying the byte on every VIA tick while WE is low re-applies data bytes (bit 7 = 0) to the last latched register. The beebium issue describes this as a known inaccuracy. | [from https://github.com/rob-smallshire/beebium/issues/89] |
| Whole-register timing | Software that holds WE open and uses 16 us cadences (sample playback) needs an "open gate" model. Not needed for OS sound. | [from SD-BUS] |

## 2. Register layout and byte encoding

Chip register addresses (3 bits, the `cc t` field of a latch byte):

| cc t | Register | Width | Source |
|---|---|---|---|
| 000 | Tone 1 (chip channel 0) frequency | 10 bits | [from DS Table 4 order], [from SMS] |
| 001 | Tone 1 attenuation | 4 bits | same |
| 010 | Tone 2 (chip channel 1) frequency | 10 bits | same |
| 011 | Tone 2 attenuation | 4 bits | same |
| 100 | Tone 3 (chip channel 2) frequency | 10 bits | same |
| 101 | Tone 3 attenuation | 4 bits | same |
| 110 | Noise control | 3 bits | same |
| 111 | Noise attenuation | 4 bits | same |

(The datasheet's address codes were OCR-garbled. The order above is the datasheet's list order and matches SMS Power exactly.)

| Byte | Layout (6502 bit order, bit 7 = MSB) | Effect | Source |
|---|---|---|---|
| Latch/data, bit 7 = 1 | `1 c c t d d d d` | Latches register `cc t`. The low 4 bits go into the low 4 bits of that register. For the 3-bit noise register the top bit is dropped. | [from SMS], [from DS data formats] |
| Data, bit 7 = 0 | `0 - D D D D D D` | If a tone register is latched, the low 6 bits go into the HIGH 6 bits of that 10-bit register. For other registers the low bits go into the register and extra high bits are dropped. The latch is never cleared by a data byte. | [from SMS] |
| 10-bit tone value N | first byte `1 cc 0 N3..N0`, second byte `0 0 N9..N4` | N = (second byte << 4) OR (first byte & 15) | [from DS: first byte carries F6-F9, second byte F0-F5, F0 = MSB], [from ROM ED7E-ED95] |
| Attenuation | `1 cc 1 a3 a2 a1 a0` | 4-bit value, 0 = full volume, 15 = off | [from DS Table 1], [from SMS] |
| Noise control | `1 1 1 0 - F NF1 NF0` | bit 2 = FB (1 = white, 0 = periodic), bits 1-0 = shift rate select | [from DS data formats, bit order mapped], [from SMS] |
| Tone updates are instant | A tone register changes as soon as the first byte lands. The chip does not wait for the second byte. | [from SMS] |
| Data byte not ignored | A data byte after a volume latch updates the volume low 4 bits. A data byte after a noise latch updates its 3 bits (and still resets the shift register, see s.4). | [from SMS] |
| Quick tone sweep | Send both bytes once. Later updates can be the second byte only, because the register address stays latched. | [from DS section 4] |

Noise shift-rate select:

| NF1 NF0 | Shift rate | At 4 MHz | Source |
|---|---|---|---|
| 00 | N/512 | 7812.5 Hz | [from DS Table 3], [from SMS: counter reset 0x10] |
| 01 | N/1024 | 3906.25 Hz | same |
| 10 | N/2048 | 1953.125 Hz | same |
| 11 | tone generator 3 output | 125000 / N3 Hz | [from DS Table 3], [inferring the rate from SMS: noise counter reset from tone 2] |

N in the first three rows is the input clock (4 MHz). NF0 is the MSB of the pair in the datasheet's numbering, so the select value is simply `byte & 3`. This is [inferring from DS bit order plus SMS].

## 3. Clock

| Fact | Value | Source |
|---|---|---|
| Input clock | 4 MHz from the video ULA | [from HWG s.3.17], [from SD-BUS] |
| Internal divider | 16 (SN76489AN). The tone counter decrements at N/16 = 250 kHz. | [from DS "decremented at a N/16 rate"], [from SD-BUS] |
| Tone formula | f = N_clk / (32 * n) = 4,000,000 / (32 n) = 125,000 / n Hz | [from DS], [from SMS: Hz = clock / (2 * reg * 16)] |
| Counter behaviour | A 10-stage counter is decremented. At zero a borrow reloads it and toggles the output flip-flop. So the half-period is n ticks of 250 kHz. | [from DS tone generator text], [from SMS] |
| Range, n = 1023 / 1 | 122.19 Hz / 125 kHz | [computed from the formula] |
| Blog error | The BLOG post said "2 MHz with divide-by-8". A reader (Robert Smallshire) corrected it to 4 MHz and divide-by-16, which gives the same 250 kHz. I trust the datasheet and SD-BUS. | [from BLOG comments], [from SD-BUS] |
| Part variants | SN76494N has no divide-by-eight stage and needs 4 clocks to load, vs 32 for SN76489AN. The BBC uses the AN. | [from DS front page] |

### n = 0 and n = 1

| Question | Answer | Source |
|---|---|---|
| n = 1 | Real chip emits a 125 kHz square wave. A scope on a real BBC chip shows 0.72 Vpp. After the analogue filters that carrier disappears. The effect is a constant level set by the volume, which is the basis of sampled sound. | [from BLOG], [from SD-SAMP] |
| n = 0 or 1 in SMS model | "Output is a constant value of +1" (the positive peak). | [from SMS] |
| n = 0 in jsbeeb | Treated as 1024. Cross-check only. | [from JSB] |
| Disagreement | SMS says n = 0 behaves like 1 (constant). jsbeeb says 0 behaves like 1024. The real BBC chip's n = 0 is not documented. Simon M says it is unknown. | [from SD-SAMP, simonm 27 Jul 2019] |
| Which I trust | For n = 1: the BBC scope result, so a mean level of half the amplitude with the carrier filtered. For n = 0: neither is proven. Recommend 1024. A 10-bit counter loaded with 0 would wrap to 1023 before reaching zero, which fits the datasheet's "decrement to zero" wording. | [inferring from DS], [guessing - verify] |
| Emulator options | Either run the chip at 250 kHz and box-average, or treat n <= 1 as constant at half amplitude. A constant at full amplitude (SMS) would be a DC step, but it is inaudible after the AC coupling in the real analogue chain. | [inferring] |

## 4. Noise generator

| Fact | Value | Source |
|---|---|---|
| LFSR width on the BBC | 15 bits | [from SMS: "SG-1000, OMV, SC-3000H, BBC Micro and Colecovision ... fed back into bit 14"], [from SD-SAMP, simonm: "bit 14 ... on the BBC's version"], [from JSB and BEM, both 15-bit in effect] |
| White noise taps | Bits 0 and 1 (mask &0003), XOR, fed into bit 14. Shift right. Output is bit 0. | [from SMS], [cross-check JSB, BEM] |
| Periodic mode | Only bit 0 is tapped: bit 0 goes back into bit 14. Output is one 1 in every 15 shifts (1/15 duty). The SMS/Genesis 16-bit chip gives 1/16. | [from SMS], [from SD-SAMP, simonm] |
| SMS / Genesis variant (not the BBC) | 16-bit, taps bits 0 and 3 (&0009), fed into bit 15 | [from SMS] |
| Reset on write | Every write to the noise control register clears the shift register. After the clear, one bit is set: the top bit, which is bit 14 for the 15-bit register (&4000). | [from DS: "Whenever the noise control register is changed, the shift register is cleared"], [from SMS: "all bits are zero except the highest bit"], [cross-check JSB and BEM both load &4000] |
| Does a data byte also reset it? | Yes. The datasheet says "changed". A register write is what counts, including a re-write of the same value (Tom Seddon's WE0-NOISE test went silent when the write repeated every 16 us). | [from SD-SAMP], [from SD-BEST, SarahWalker: "AN resets the bit sequence when writing to the noise control register"] |
| Disagreement on reset value | simonm quotes &8000 (16-bit assumption). SMS, JSB and BEM use the highest bit of a 15-bit register, &4000. I trust &4000 for the BBC. | [from SD-SAMP], [from SMS] |
| Direction / polarity | Shift right. Output is never inverted by the register logic. | [from SD-SAMP, scarybeasts citing MAME], [from SMS] |
| Disagreement on XOR vs XNOR | Some non-BBC SN variants use XNOR. For the BBC, a maximal 15-bit LFSR with XOR taps 0,1 has period 32767. | [from SD-SAMP], [computed: simulated, period 32767 from seed &4000] |
| Initial noise register | Not defined by the datasheet. The OS writes the chip at reset (s.7). | [inferring from DS] |

### Noise test vectors (white, 15-bit, seed &4000, taps 0 and 1)

Output bit (bit 0) before each shift. Computed by me from the model above. I did not run any emulator for this.

White: `0000000000000010000000000000110000000000` (first 40).
Periodic (taps bit 0 only): `00000000000000100000000000000100` (first 32).

All of this is [inferring from SMS + the model above, computed in Python].

## 5. Volume table

| Fact | Value | Source |
|---|---|---|
| Step size | 2 dB per step. Four bits weighted 2, 4, 8, 16 dB. Maximum non-off attenuation = 28 dB. 1111 = off. | [from DS Table 1] |
| Tolerance on real parts | 2 dB step: 1-3 dB. 4 dB: 3-5. 8 dB: 7-9. 16 dB: 15-17. | [from DS electrical characteristics] |
| Amplitude formula | A(n) = 10^(-2 n / 20) for n = 0..14, A(15) = 0 | [from SMS], [from DS] |

| n | A(n) | x 32767 | /4 (per-channel share of 1.0 mix) |
|---|---|---|---|
| 0 | 1.000000 | 32767 | 0.2500 |
| 1 | 0.794328 | 26028 | 0.1986 |
| 2 | 0.630957 | 20675 | 0.1577 |
| 3 | 0.501187 | 16422 | 0.1253 |
| 4 | 0.398107 | 13045 | 0.0995 |
| 5 | 0.316228 | 10362 | 0.0791 |
| 6 | 0.251189 | 8231 | 0.0628 |
| 7 | 0.199526 | 6538 | 0.0499 |
| 8 | 0.158489 | 5193 | 0.0396 |
| 9 | 0.125893 | 4125 | 0.0315 |
| 10 | 0.100000 | 3277 | 0.0250 |
| 11 | 0.079433 | 2603 | 0.0199 |
| 12 | 0.063096 | 2067 | 0.0158 |
| 13 | 0.050119 | 1642 | 0.0125 |
| 14 | 0.039811 | 1304 | 0.0100 |
| 15 | 0 | 0 | 0 |

All values [computed from the formula in Python].

| Discrepancy | Detail | Source |
|---|---|---|
| SMS table, n = 7 | SMS Power's example table lists 6568 for n = 7. The formula gives 6538 (32767 * 10^-1.4). All other entries match. I trust the formula. 6568 looks like a typo. | [from SMS], [computed] |
| Max output | If each channel is normalised to 1.0 at full volume, four channels at n = 0 sum to 4.0. jsbeeb scales each channel by 1/4 so the mix tops out at 1.0. | [cross-check JSB] |
| Real BBC, non-scaled | BEM uses its own float table (roughly 0 to 15 with a different curve). Not 2 dB steps by any simple formula. Do not use it as a reference. | [cross-check BEM, not primary] |

## 6. Output mixing

| Fact | Value | Source |
|---|---|---|
| Mixing | The chip sums the 3 tone outputs and the noise output in an op-amp output stage. | [from DS "Output buffer / amplifier"] |
| Unipolar, not bipolar | On a real BBC chip each channel alternates between two non-negative voltages. A max-volume channel swings between 0 V and about 0.8 V. A silent channel sits constant at about 0.8 V. The chip's output is "inverted" (louder means lower voltage). | [from BLOG] |
| Emulator consequence | Model each channel as 0..A(n), not -A..+A. This matters for sampled sound: the mean level varies with volume only if the swing is unipolar. A bipolar swing averages to 0 and loses the effect. DC can be removed after the sum. | [from SD-SAMP, scarybeasts and Chris's NOISE1 test], [from BLOG] |
| Noise on real hardware | Noise oscillates 0 to +A, not -A to +A. | [from SD-SAMP: jsbeeb patch after the NOISE1 test] |
| Whole-output inversion | MAME inverts the whole output (min volume = high). Tom Seddon notes that polarity makes no audible difference. | [from SD-SAMP, Tom Seddon 29 Jul 2019] |
| Analogue chain | Chip -> LM324 mixing/filter (IC17) -> LM386 (IC19) -> speaker. Also feeds the DIN line out. A low-pass near 8 kHz is suspected on the LM324 stage. Secondary. | [from HWG], [from BLOG], [guessing - verify the 8 kHz figure] |

## 7. What the OS does

| Fact | Detail | Source |
|---|---|---|
| Silence at reset | Reset code (vector &D9CD) calls `JSR &EC60` at `&DAAA`. `&EC60` loops X = 7 down to 4 and calls `&ECA2`, which calls `&EB03`. That builds each attenuation latch byte and calls `&EB21`. Bytes written, in order: **&9F, &BF, &DF, &FF**. This confirms attenuation = 15 on all four channels. | [from ROM DAAA, EC60-EC68, ECA2, EB03-EB1F] |
| How &EB03 builds the byte | A = &C0 (silent). `SEC; SBC #&40; LSR x3; EOR #&0F; ORA &EB3C,X; ORA #&10`. A = &C0 gives &1F, so the attenuation nibble is F. | [from ROM EB0D-EB1F] |
| Channel base-byte table | `&EB40..&EB43` = &E0, &C0, &A0, &80, indexed by `&EB3C,X` with X = 4..7. So X=4 is the noise channel (&E0), X=5 is chip tone 3 (&C0), X=6 is chip tone 2 (&A0), X=7 is chip tone 1 (&80). | [from ROM EB40-EB43] |
| BBC channel to chip channel | BBC channel 0 (noise) = X 4. BBC channels 1, 2, 3 = X 5, 6, 7 = chip tone 3, 2, 1. | [inferring from ROM ED09 `CPX #4` taking the noise path, plus EB40 table] |
| Sound disable flag | `&0262` non-zero forces silence in the volume routine at &EB0D-&EB12. | [from ROM EB0D-EB12] |
| Tone write | Low nibble: `AND #&0F; ORA &EB3C,X` then `JSR &EB21`. High 6 bits: value shifted right 4, then `JMP &EB22`. | [from ROM ED7E-ED95] |
| Noise write | `AND #&0F; ORA &EB3C,X` (X = 4 gives &E0 or its nibble), then one write. The SOUND pitch for channel 0 is used as the noise nibble directly: 0-3 periodic, 4-7 white. | [from ROM ED09-ED13], [inferring the SOUND mapping] |
| Per-channel detune | `ADC &C43D,X` adds a small offset to the period low byte: +0 for chip tone 3 (X=5), +1 for tone 2 (X=6), +2 for tone 1 (X=7). | [from ROM ED73 and ROM bytes at &C441-&C444 = 00 00 01 02] |

### OS pitch to chip period (pitch p, 0..255)

Routine `&ED01`. Two 12-byte tables are read.

| Step | Detail | Source |
|---|---|---|
| fine = p AND 3 | quarter-semitone steps | [from ROM ED16-ED19] |
| s = p >> 2 | then s is reduced mod 12, counting the octaves removed in `oct` | [from ROM ED22-ED2D] |
| Base period | `base = lo[s] | ((hi[s] AND 3) << 8)`, `delta = hi[s] >> 4` | [from ROM ED34-ED48] |
| Fine adjust | `base -= delta * fine` | [from ROM ED4B-ED5D] |
| Octave shift | `period = base >> oct` | [from ROM ED62-ED6D] |
| Detune | add &C43D,X offset, then send low nibble first, high 6 bits second | [from ROM ED6F-ED95] |

Tables (lo at `&EDFB+s`, hi at `&EE07+s`). All [from ROM].

| s | lo | hi | base N | delta | f at 4 MHz (Hz) |
|---|---|---|---|---|---|
| 0 | &F0 | &E7 | 1008 | 14 | 124.01 |
| 1 | &B7 | &D7 | 951 | 13 | 131.44 |
| 2 | &82 | &CB | 898 | 12 | 139.20 |
| 3 | &4F | &C3 | 847 | 12 | 147.58 |
| 4 | &20 | &B7 | 800 | 11 | 156.25 |
| 5 | &F3 | &AA | 755 | 10 | 165.56 |
| 6 | &C8 | &A2 | 712 | 10 | 175.56 |
| 7 | &A0 | &9A | 672 | 9 | 186.01 |
| 8 | &7B | &92 | 635 | 9 | 196.85 |
| 9 | &57 | &8A | 599 | 8 | 208.68 |
| 10 | &35 | &82 | 565 | 8 | 221.24 |
| 11 | &16 | &7A | 534 | 7 | 234.08 |

## 8. Worked examples for unit tests

Tone formula tests (f = 125000 / n). All [computed from the DS formula].

| n | Hex | Frequency (Hz) | Latch byte (chip tone 1) | Data byte |
|---|---|---|---|---|
| 1 | 0x001 | 125000.000 | &81 | &00 |
| 2 | 0x002 | 62500.000 | &82 | &00 |
| 100 | 0x064 | 1250.000 | &84 | &06 |
| 284 | 0x11C | 440.141 | &8C | &11 |
| 478 | 0x1DE | 261.506 | &8E | &1D |
| 1023 | 0x3FF | 122.190 | &8F | &3F |

Rule: latch byte = &80 | (cc << 5) | (n & 15). Data byte = n >> 4. For chip tone 2 use &A0 in place of &80. For tone 3 use &C0.

Register byte tests. All [from SMS and DS].

| Bytes written (in order) | Result |
|---|---|
| &8C, &11 | tone 1 N = 0x11C = 284 |
| &8F, &3F | tone 1 N = 1023 |
| &8C then &8F (no data byte) | tone 1 low nibble goes &C then &F. The high 6 bits stay as they were. |
| &90 | attenuation 1 = 0, full volume |
| &9F | attenuation 1 = 15, off |
| &9F, &BF, &DF, &FF | all four channels off (the OS reset sequence) |
| &DF then &00 | attenuation of chip tone 3 set to 15, then updated to 0 by the data byte (data byte is not ignored) |
| &E5 | noise: FB = 1 (white), NF = 01 (N/1024 = 3906.25 Hz). Resets the LFSR to &4000. |
| &E4 | noise: white, NF = 00 (N/512 = 7812.5 Hz) |
| &E3 | noise: periodic, NF = 11 (follows tone 3) |

Noise rate tests. All [computed].

| NF | White shift rate (Hz) | Periodic note rate = shift rate / 15 (Hz) |
|---|---|---|
| 00 | 7812.5 | 520.83 |
| 01 | 3906.25 | 260.42 |
| 10 | 1953.125 | 130.21 |

OS pitch tests (chip tone 3, no detune). All [computed from the ROM tables above].

| SOUND pitch | fine | s | oct | base | adjust | period N | f (Hz) |
|---|---|---|---|---|---|---|---|
| 0 | 0 | 0 | 0 | 1008 | 0 | 1008 | 124.01 |
| 1 | 1 | 0 | 0 | 1008 | -14 | 994 | 125.75 |
| 4 | 0 | 1 | 0 | 951 | 0 | 951 | 131.44 |
| 48 | 0 | 0 | 1 | 1008 | 0 | 504 | 248.02 |
| 52 | 0 | 1 | 1 | 951 | 0 | 475 | 263.16 |
| **53** | 1 | 1 | 1 | 951 | -13 | 938 >> 1 = **469** | **266.52** |
| 100 | 0 | 1 | 2 | 951 | 0 | 237 | 527.43 |
| 255 | 3 | 3 | 5 | 847 | -36 | 811 >> 5 = 25 | 5000.00 |

For SOUND channels 2 and 3, add +1 and +2 to the final period respectively (pitch 53: 470 and 471). That is [from ROM C441-C444].

Middle C note. BBC documentation says pitch 53 is middle C (261.63 Hz). That is [guessing - verify; from memory, not read]. The ROM maps 53 to N = 469, which is 266.52 Hz (about 31 cents sharp). Pitch 52 gives 263.16 Hz. The tables are tuned for pitch 0 = B2 area. I could not find a source stating exact pitch 53 frequency. Test against the ROM tables, not against a musical-pitch claim.

## Could NOT establish

| Item | Why |
|---|---|
| Direct schematic proof of PA0..PA7 to D7..D0 | I found no schematic text. The statement is inferred from the datasheet pin names (D0 = MSB) plus the OS writing conventional bytes with no bit reversal. |
| Real-chip behaviour for tone n = 0 | Not in the datasheet, and no BBC measurement found. Simon M (SD-SAMP) says it is unknown. |
| Power-on state of a real BBC chip | The OS silences the chip at reset. What the chip does before that is unknown. SMS Power says discrete chips seem to start random. I did not find a BBC-specific measurement. |
| Initial output flip-flop state | SMS: "may be set arbitrarily". Nothing BBC-specific. |
| Exact write-latch instant inside the 32-clock window | Measured as 3.7-6.8 us after WE low on one boot and 5.0-8.0 us on another (SD-BUS). Not fixed by a datasheet value. |
| Real elapsed time of the OS WE-low pulse | I counted 17 CPU cycles (8.5 us). The 1 MHz stretch on each VIA write is not modelled in that count. |
| Whether a noise-register WE re-latch always resets the LFSR | Tom Seddon's test says yes (silence with WE held low). SD-BEST gives SarahWalker as the source for "AN resets". Both are forum observations, not a datasheet text. The datasheet says "changed". |
| Pitch 53 vs middle C | The BBC manual's figure was not found in a source I could read. |
| LFSR direction as a hardware fact | Only emulator and MAME-derived consensus plus the SMS captures. No gate-level source. |
| TI Table 3 bit patterns | The scanned datasheet OCR was garbled for that table. The mapping used is corroborated by SMS Power but not read cleanly from the scan. |
| BBC Advanced User Guide and beebwiki | beebwiki.mdfs.net via the Wayback Machine returned an empty shell. The Advanced User Guide PDF could not be fetched. So the AUG's own wording of the 8 us wait and the latch table is not quoted here. |

## AUG cross-check of section 4 (added by the VIA researcher; the sound sub-agent could not fetch the AUG)

The Advanced User Guide is available at https://stardot.org.uk/mirrors/www.bbcdocs.com/filebase/essentials/BBC%20Microcomputer%20Advanced%20User%20Guide.pdf (local text `/tmp/bbc-facts/via-src/aug.txt`, ch.23.3 pp.419-422). It agrees with this section on every point it covers, with one naming trap:

| Item | AUG | Agrees? |
|---|---|---|
| Sound write enable | latch B0; "pulled low for at least 8 uS then pulled high again" | yes |
| Tone frequency | `4000000/32 x 10 bit binary number` | yes |
| Volume | 16 levels, 0 = off (15 = max in AUG's volume numbering), 2 dB steps not stated; 4-bit attenuation field A3-A0 where 0000 = max, 1111 = off | yes |
| Noise | FB = 1 white, 0 periodic; NF1 NF0: 00 low, 01 medium, 10 high, 11 "tone generator 1 frequency" | yes (AUG's "tone generator 1" is BBC channel 1 = chip tone 3, below) |
| Register address field | R2 R1 R0: 000 "Tone 3" freq, 001 "Tone 3" volume, 010 "Tone 2", 011, 100 "Tone 1" freq, 101 "Tone 1" volume, 110 noise control, 111 noise volume | **Naming trap:** AUG uses BBC channel numbers, which run opposite to TI's numbering. AUG "Tone 3" (000) = TI tone 1; AUG "Tone 1" (100) = TI tone 3 (BBC SOUND channel 1). The ROM table in section 4.7 (BBC channels 1, 2, 3 = chip tone 3, 2, 1) agrees |
| Byte formats | first freq byte `1 R2 R1 R0 F3 F2 F1 F0`, second `0 X F9..F4`, noise `1 R2 R1 R0 X FB NF1 NF0`, volume `1 R2 R1 R0 A3..A0` | yes |
| Direct-poke example | sets DDRA=&FF, byte in ORA (&FE41), ORB &00 then &08 (latch bit 0) | yes; matches ROM &EB21 |



---

# Consolidated: what could NOT be established

Parts 3 and 4 each end with their own list (above). The VIA and wiring gaps, with a recommendation for each:

| # | Gap | Recommendation |
|---|---|---|
| 1 | Exact phase of the 1 MHz VIA cycle against the 2 MHz CPU cycle, and the stretch rule (which accesses wait 1 and which 2 cycles). Without it I cannot turn "VIA cycle W+N+2" into an absolute CPU cycle count | Build the VIA in 1 MHz ticks, drive it from the bus stretch the bus sheet defines, then check against RTW's real-hardware timing table (1.10, last paragraph) |
| 2 | Shift register mode 010 timing (my 19-cycle figure is read off a WDC drawing) and what makes the DFS ROM start it | Implement 8 shifts at phi2/2, set IFR2 about 19 cycles after the SR read, make IFR2 clear on SR read/write; trace the DFS caller when the disc test runs |
| 3 | Power-on counter, latch and SR values | Use 0 for all; the OS writes what it needs. Real values are undefined |
| 4 | T1 counter after a one-shot expiry (reload or decrement) | Reload from the latch after &FFFF, as in the datasheet figure and the one real-hardware remark |
| 5 | PB6 level and which of PB6/PB7 is speech ready | Read 1 for both with no speech chip |
| 6 | Whether every write to any system-VIA register strobes the latch | Strobe only on ORB and DDRB writes; equivalent for the OS (2.2) |
| 7 | Timer 2 pulse counting: how many PB6 edges reach IFR5 | Not exercised on a Model B without speech; hold the counter |
| 8 | MOS 6522 shift-register bugs (hoglet mentions them, undescribed) | Ignore until a real test needs them |
| 9 | Vsync pulse width as seen at CA1 (one post says about 4 raster lines, another 2 scanlines) | Take it from the CRTC sheet; it is the 6845 R3 high nibble |
| 10 | The 6502.org thread "6522 - exact timing" (RTW on one cycle between acknowledge and IRQ rising) | Only seen in a search snippet; sandbox DNS blocks 6502.org, mdfs.net and beebwiki.mdfs.net directly (beebwiki only via web.archive.org, which returned an empty shell for the VIA page) |
| 11 | What a real unmapped ADC read returns | Return a value with bit 6 clear (2.5) |

Datasheet pages, forum threads and the AUG text I relied on are all in `/tmp/bbc-facts/via-src/`.
