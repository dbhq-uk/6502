# Atari 2600 cartridge: the window, the boards, bank switching and cartridge RAM

This sheet states how a 2600 cartridge answers the CPU: the 4K window and the 2K mirror, the hotspots of F8, F6, F4 and FA, when a new bank answers, the Superchip's and FA's RAM ports, FA's D0 gate, the power-on bank, and how the board is chosen from a file.

Written 14 October 2026 for the Atari 2600 plan, task 1. Tags and sources are in [`README.md`](README.md). Used by tasks 2 (the cartridge and the boards) and 11 (the suite in full: the cartridge tests and their known differences).

Two sources are new to this sheet and are listed at the end: Kevin Horton's "Cart Information" (1997), tagged `[from Horton]`, and Martin Korth's "Atari 2600 Specifications", tagged `[from 2k6specs]`. Three patents are tagged by number: `[from US 4,368,515]` (Atari, the bank switch), `[from US 4,432,067]` (its division, same text) and `[from US 4,485,457]` (CBS, RAM Plus). The project's own CPU design is tagged `[from the 6502 spec]`.

The suite's README says its cartridge tests were not run on a console, "they need mapper hardware a flashcart lacks", and contested behaviour is marked "untested on hardware" in each header [from the suite: README.md]. So for cartridges the suite is one reading of the documents, not a measurement.

## 1. The window and the 2K mirror

The cartridge answers every access with A12 high [from the suite: roms/cartridge/bank-f8.asm; from 2k6specs "Cartridge Mirrors"]. On the 6507's 13 address lines that is `$1000-$1FFF`; a program usually spells it `$F000-$FFFF`, which is the same 13-bit address [from the suite: roms/cartridge/bank-f8.asm; from 2k6specs "Memory Mirrors"]. On original cartridges A12 is the ROM's chip select, active high [from 2k6specs "Cart Pin-Outs"].

- **4K:** the image fills the window once; offset n is at `$1000+n` [from Horton "4K"; from 2k6specs "4K and 2K ROMs"].
- **2K:** the image appears twice, because the board does not wire A11; offset n is at `$1000+n` and `$1800+n` alike, for data reads and opcode fetches [from the suite: roms/cartridge/mirror-2k.asm; from Horton "2K": "the data repeats twice in the 4K address space"; from 2k6specs "4K and 2K ROMs"].
- **Writes** to a plain board change nothing [inferring: a ROM has no write input, and the slot has no read/write line, from 2k6specs "Cart Pin-Outs" (the 24 pins carry A0 to A12, D0 to D7, +5V, GND and /EXROM)].

### Worked example 1: a 2K image

| CPU address | 13-bit address | Image offset |
|---|---|---|
| `$F123` | `$1123` | `$123` |
| `$F923` | `$1923` | `$123` |
| `$FFFC`, `$FFFD` | `$1FFC`, `$1FFD` | `$7FC`, `$7FD`, the reset vector |

In `mirror-2k`, offsets `$7FC` and `$7FD` hold `$00` and `$F8`, so the reset vector is `$F800`, which is offset 0 seen with A11 high; the lower half plays no part in reset [from the suite: roms/cartridge/mirror-2k.asm]. A 2K image's `$FFFx` vectors are mirrors of `$F7Fx` [from 2k6specs "ROM Origin & Entrypoint"].

## 2. The boards modelled

Every bank is 4K. Bank n is the image's bytes `n*$1000` to `n*$1000+$FFF` [from Horton "8K" to "32K"; from 2k6specs "Cart 4K-Banking"].

| Board | File size | Banks | Hotspot: bank it selects | RAM | Sources |
|---|---|---|---|---|---|
| Plain | 2048, 4096 | 1 | none | none | [from Horton "2K", "4K"; from 2k6specs "4K and 2K ROMs"; from the suite: roms/cartridge/mirror-2k.asm] |
| F8 | 8192 | 2 | `$1FF8`: 0, `$1FF9`: 1 | none | [from the suite: roms/cartridge/bank-f8.asm; from Horton "8K"; from 2k6specs "Cart 4K-Banking"; from US 4,368,515 (FF8 and FF9 set and reset the bank flip-flop)] |
| F6 | 16384 | 4 | `$1FF6`: 0, `$1FF7`: 1, `$1FF8`: 2, `$1FF9`: 3 | none | [from the suite: roms/cartridge/bank-f6.asm; from Horton "16K"; from 2k6specs "Cart 4K-Banking"] |
| F4 | 32768 | 8 | `$1FF4` to `$1FFB`: 0 to 7 in order | none | [from the suite: roms/cartridge/bank-f4.asm; from Horton "32K"; from 2k6specs "Cart 4K-Banking"] |
| FA (CBS RAM Plus) | 12288 | 3 | `$1FF8`: 0, `$1FF9`: 1, `$1FFA`: 2, gated by D0 (section 7) | 256 bytes: write `$1000-$10FF`, read `$1100-$11FF` | [from the suite: roms/cartridge/bank-fa.asm; from Horton "12K" and "Extra RAM in Carts"; from 2k6specs "Cart 4K-Banking", "Cart Expansion RAM"; from US 4,485,457] |
| Superchip on F8, F6 or F4 (F8SC, F6SC, F4SC) | 8192, 16384, 32768 | as the board | as the board | 128 bytes: write `$1000-$107F`, read `$1080-$10FF` | [from the suite: roms/cartridge/ram-superchip.asm; from Horton "Extra RAM in Carts" and the key ("F8+SC, F4+SC"); from 2k6specs "Cart Expansion RAM"] |

Horton's list marks F6 games with the Superchip too (Dig Dug, Crystal Castles, Millipede and others), and Fatal Run, his only F4 game, has it [from Horton, Atari table]. Korth counts, in the AtariAge collection of 533 games, 3 games at 8K with 128 bytes of RAM, 13 at 16K, 1 at 32K (Fatal Run), and 3 at 12K with 256 bytes (Mountain King, Omega Race, Tunnel Runner) [from 2k6specs "Cart Summary and General Info"]. Horton names the same three CBS games as FA [from Horton, CBS table].

No hotspot of a modelled board overlaps a vector: `$1FFC-$1FFF` are never hotspots [inferring from the table].

## 3. How a hotspot works

- **Read or write alike, any value.** An access to a hotspot switches the bank whether it is a read or a write, and the data does not matter (FA excepted, section 7) [from the suite: roms/cartridge/bank-f8.asm, bank-f6.asm, bank-f4.asm; from Horton "F8": "Any access to either one of these locations switches banks"; from 2k6specs "Cart 4K-Banking": "by reading from (or by writing any value to)"]. The slot has no read/write line, so the board cannot tell the two apart [from Horton "Extra RAM in Carts"; from 2k6specs "Cart Pin-Outs"].
- **The latch holds** until another hotspot is accessed; every other address leaves the bank alone [from US 4,368,515: the flip-flop's state is left unchanged when the decode is inactive; from the suite: roms/cartridge/bank-hotspot-window.asm, reads at `$FFE0`, `$FFE7`, `$FFED`, `$FFF0` and `$FFF7` must not switch an F8 board].
- **A12 is part of the decode on Atari's boards.** In the patent the hotspot gates are enabled by the ROM's chip select [from US 4,368,515, FIG. 6 description]. Korth warns that bank ports "aren't necessarily connected to A12" and "may overlap" the RIOT mirror at `$0FF8` [from 2k6specs "Cartridge Mirrors"]. This sheet follows the patent for the boards modelled: only accesses with A12 high reach the board, as the plan's `IBoard` assumes. [guessing - verify] for boards not built by Atari.
- **Short addresses do not count.** The patent's one-shot passes a decoded hotspot to the latch only if it is still present after a delay, so an address that flickers between two cycles does not switch [from US 4,368,515, FIG. 6 description].

### Worked example 2: switching F6 and F4

An F6 board in bank 0: `STA $1FF8` selects bank 2; `BIT $1FF7` then selects bank 1; `LDA $1FFA` changes nothing, because `$1FFA` is not an F6 hotspot. The same `LDA $1FFA` on an F4 board selects bank 6 [from the table in section 2]. `LDA $1FF5` on F8 or F6 changes nothing; on F4 it selects bank 1.

## 4. When the new bank answers

Three statements, and they do not all say the same thing.

1. **Every access after the hotspot access sees the new bank.** "The switch takes effect on the next bus cycle. The instruction fetched right after a hotspot access already comes from the new bank" [from the suite: roms/cartridge/bank-f8.asm, bank-f6.asm, bank-f4.asm]. Horton's example says the same at instruction level: after the switching instruction finishes, the processor is in the new bank [from Horton "How Bankswitching Works"]. Korth: after `BIT $FFFx` the program carries on at PC+3, and the new bank must have valid code there [from 2k6specs "Cart 4K-Banking"].
2. **The hotspot read itself returns the byte of the bank it selects.** The board flips the latch while the hotspot address is still on the bus, the latch is the ROM's 13th address bit, so the ROM answers from the bank just selected [from the suite: roms/cartridge/bank-hotspot-window.asm, cells `$0C` and `$0E`, citing US 4,368,515 and US 4,432,067]. The suite marks this "untested on hardware".
3. **The patent itself says the hotspot access may reach either bank.** An access to FF8 "will access the two memory locations", one in each bank, so in Atari's design the hotspot locations in both banks hold a NOP [from US 4,368,515; the same text in US 4,432,067]. The suite agrees that Atari's images hold `$EA` at `$FFF8` and `$FFF9` in both banks, so no Atari game can see the difference [from the suite: roms/cartridge/bank-hotspot-window.asm].

**This sheet follows statement 2 for F8, F6 and F4:** on a hotspot access the board switches first, and a read returns the byte at that address in the newly selected bank. Every later access sees the new bank. Reasons: statement 1 holds under this rule too; statement 3 shows no game depends on the byte; and the plan's rule (the old bank's byte) fails the suite's cells `$0C` and `$0E`. **This contradicts the plan, task 2 `BankedBoardTests`** ("an access to a hotspot returns the byte of the bank showing when it was made"), and the spec's "the new bank is visible from the next cycle" in the narrow case of the hotspot read's own byte. FA is different (section 7).

### Worked example 3: the hotspot read's own byte (from `bank-hotspot-window`)

Bank 0 holds `$58` at `$1FF8` and `$59` at `$1FF9`; bank 1 holds `$78` and `$79` [from the suite: roms/cartridge/bank-hotspot-window.asm].

| Bank before | Access | Byte returned | Bank after |
|---|---|---|---|
| 1 | read `$1FF8` (`LDX #$F8`, `LDA $FF00,X`: no page cross, one access) | `$58` (bank 0's) | 0 |
| 0 | read `$1FF9` | `$79` (bank 1's) | 1 |
| 0 | read `$1FF0` | bank 0's byte at `$1FF0` | 0 |
| 0 | write `$1FF9` (`STA $1FF9`) | none | 1 |

Opcode level: `BIT $1FF9` at `$1800` in bank 0 fetches `$1800`, `$1801`, `$1802` from bank 0, reads `$1FF9` (bank 1 from here), and the next opcode fetch at `$1803` comes from bank 1 [inferring from statements 1 and 2 and a 4-cycle `BIT abs`].

## 5. The CPU's dummy reads count

A board sees an address and nothing else, and any address held for a whole bus cycle passes the patent's one-shot [from US 4,368,515], so a dummy read switches banks and hits a RAM port exactly as a real read does [inferring from section 3]. The suite relies on this for the Superchip's ghost write [from the suite: roms/cartridge/ram-superchip.asm]. The 6507 is a cut-down NMOS 6502 [from 2k6specs "CPU 6507"]; on the NMOS chip a read-modify-write instruction writes the old value back before the new one, and an indexed read that crosses a page re-reads a different address as its dummy read [from the 6502 spec]. The un-carried address is the base's high byte with the low byte already indexed [from the suite: roms/cartridge/ram-superchip.asm: `lda $F080,y` with Y=`$80` puts `$F000` on the bus].

The core already makes these accesses and is checked cycle by cycle against Harte's tests, so task 2's tests can take each sequence from the core rather than from this table.

### Worked example 4: accesses in the cartridge window, operand fetches left out

| Instruction | Accesses to the operand's addresses | Effect |
|---|---|---|
| `LDA $1FF8` (`AD F8 1F`) | read `$1FF8` | one hotspot access |
| `INC $1FF8` (`EE F8 1F`) | read `$1FF8`, write `$1FF8` (old), write `$1FF8` (new) | three accesses to one hotspot; the bank ends as `$1FF8` says, and the writes go nowhere |
| `LDA $1FF0,X`, X=`$08` (`BD F0 1F`) | read `$1FF8` | `$F0+$08=$F8`, no carry, so no dummy read: one access |
| `STA $1FF0,X`, X=`$08` | read `$1FF8`, write `$1FF8` | an indexed store always reads the un-carried address first [guessing - verify] against Harte's data; both accesses select the same bank |
| `LDA $1FF9,X`, X=`$FF` | dummy read `$1FF8`, then read `$20F8` | the dummy read selects bank 0; `$20F8` is `$00F8` on 13 lines, the RIOT's RAM, so a load from RAM switched the bank [inferring: the 13-bit decode in bus.md] |
| Superchip: `LDA $10F0,X`, X=`$20` | dummy read `$1010`, then read `$1110` | `$1010` is the write port, cell `$10`: the cell takes the bus residue, `$10`, the operand's high byte just fetched; A gets the ROM byte at `$1110` in the current bank (section 6) |
| FA: `LDA $10F0,X`, X=`$20` | dummy read `$1010`, then read `$1110` | cell `$10` takes `$10` through FA's write port, then `$1110` is FA's read port for the same cell, so A=`$10` [inferring from section 7] |

## 6. The Superchip's RAM

- **Ports.** 128 cells. A write to `$1000+n` (n from `$00` to `$7F`) stores into cell n; a read of `$1080+n` returns cell n [from the suite: roms/cartridge/ram-superchip.asm; from Horton "Extra RAM in Carts": "The first 128 bytes is the write port, while the second 128 bytes is the read port"; from 2k6specs "Cart Expansion RAM"].
- **The ROM under the ports is never seen.** A read of `$1080-$10FF` returns RAM, not the image [from the suite: roms/cartridge/ram-superchip.asm, cell `$04`]. The ROM area is `$1100-$1FFF`, 3.75K per bank [from 2k6specs "Cart Expansion RAM"].
- **One RAM for every bank.** The RAM is outside the banked ROM; a switch neither moves nor clears it [from the suite: roms/cartridge/ram-superchip.asm, cells `$05` to `$07`; from 2k6specs "Cart Expansion RAM": "RAM is NOT banked"].
- **A read of the write port writes.** The port is chosen by address alone, so a read of `$1000-$107F`, real or dummy, latches the data bus into the cell. Nothing drives the bus in that cycle, so the bus holds the last byte driven [from the suite: roms/cartridge/ram-superchip.asm, cell `$08`, "untested here, for want of a Superchip board"; from 2k6specs "Cart Expansion RAM": reading a write address "would overwrite the addressed byte by garbage"]. The CPU then reads that same residue [inferring: nothing drives the bus].
- **A write to the read port** stores nothing [inferring: the port is a read by its address]; what lands on the bus is a clash of the CPU and the RAM [guessing - verify]. So `INC`, `DEC`, `ASL`, `LSR`, `ROL` and `ROR` cannot work on this RAM [from 2k6specs "Cart Expansion RAM"].
- **Power-on contents** are not stated by any source read [guessing - verify].

### Worked example 5: the Superchip

| Step | Access | Result |
|---|---|---|
| `LDA #$A5`, `STA $1000` | write `$1000` | cell 0 = `$A5` |
| `LDA $1080` | read `$1080` | A = `$A5` |
| `STA $1FF9`, then `LDA $1080` | | A = `$A5` (same RAM in bank 1) |
| `LDA $1000` (`AD 00 10`) | read `$1000` with `$10` on the bus | cell 0 = `$10`, A = `$10` |
| the suite's `LDY #$80`, `LDA $F080,Y` (`B9 80 F0`) | dummy read `$F000` (`$1000`) with `$F0` on the bus, then read `$F100` | cell 0 = `$F0`; A = the ROM byte at `$1100` [from the suite: roms/cartridge/ram-superchip.asm] |
| `STA $1080` with A = `$33` | write `$1080` | cell 0 unchanged |

The residue depends on how the program spelt the address: `$F080` leaves `$F0`, `$1080` leaves `$10` [inferring: the last byte fetched is the operand's high byte].

## 7. FA (CBS RAM Plus)

### 7.1 RAM ports

256 cells. A write to `$1000+n` (n from `$00` to `$FF`) stores into cell n; a read of `$1100+n` returns cell n [from the suite: roms/cartridge/bank-fa.asm; from Horton "12K" and "Extra RAM in Carts"; from 2k6specs "Cart Expansion RAM"; from US 4,485,457: address lines A8 to A12 equal to `$10` enable a write and `$11` a read]. The RAM is not banked; the ROM under `$1000-$11FF` is never seen [from the suite: roms/cartridge/bank-fa.asm, cells `$0A`, `$0B`; from 2k6specs "Cart Expansion RAM"]. The read port is decoded by address alone, as the Superchip's is [from US 4,485,457], so a read of `$1000-$10FF` writes the bus residue into the cell [inferring: the same argument as section 6; no FA cell in the suite tests it].

The patent's text once gives the read segment as "1100 through 12FF", but its own decode (`$11` on A8 to A12) and its 256 cells make it `$1100-$11FF`; the patent's pages are 3.5K at `$1200-$1FFF` [from US 4,485,457]. A 12K image keeps each page in a 4K bank whose first 512 bytes are hidden [inferring from Horton "12K" and 2k6specs "Cart Expansion RAM" (ROM area `$1200-$1FFF`, 3.5K)].

### 7.2 The D0 gate: what each source says

| Source | What it says |
|---|---|
| US 4,485,457 (CBS, filed 31 May 1983) | The page select logic responds to a hotspot address "with data line D0 in a high state". The reason given: address and data reach the logic at different times, so the address settles first and the later data triggers the switch. Only D0 is used in the embodiment |
| The suite, `bank-fa` | Asserts the gate on the patent's authority: a write switches only if the value has D0=1; a read switches only if the hotspot's ROM byte has D0=1, and that byte comes from the bank showing before the switch. Cells `$0C` and `$0D` test the gate and are "contested"; "The gate is untested on hardware" |
| Horton | No gate: "You select the desired bank via 1FF8, 1FF9, and 1FFA" |
| 2k6specs | No gate: the 12K row is in the table introduced by "reading from (or by writing any value to)" |

**This sheet follows the gate, D0 only, D1 to D7 ignored**, because the patent is CBS's own description of the board and the suite asserts it; an ungated board fails `$0C` and `$0D`. It agrees with the spec ("only when data bit D0 is 1"). The risk: a patent describes an embodiment, not necessarily the board CBS shipped, and the two documentation sources say any access switches [guessing - verify]. If a visitor's CBS game fails to change bank, this gate is the first suspect. The choice goes in `docs/known-differences.md`.

**The byte a hotspot read returns on FA** is the byte of the bank showing before the access [guessing - verify]: the gate decides on that byte, late in the cycle [from US 4,485,457; from the suite: roms/cartridge/bank-fa.asm], so this sheet does not extend section 4's rule to FA. No suite cell checks it, and CBS's three games are not known to depend on it.

### Worked example 6: FA, with the suite's bytes

Every bank holds `$01` at `$1FF8`; bank 0 holds `$00` at `$1FFA` and bank 1 holds `$00` at `$1FF9`; every other hotspot byte is `$01` [from the suite: roms/cartridge/bank-fa.asm].

| Bank before | Access | D0 seen | Bank after |
|---|---|---|---|
| 0 | `LDA #$01`, `STA $1FF9` | 1 | 1 |
| 0 | `LDA #$00`, `STA $1FF9` | 0 | 0 |
| 0 | `BIT $1FF9` (bank 0's byte `$01`) | 1 | 1 (bank 1's own `$00` plays no part) |
| 0 | `BIT $1FFA` (bank 0's byte `$00`) | 0 | 0 |
| 2 | `LDA #$03`, `STA $1FF8` | 1 | 0 |
| any | `LDA #$6D`, `STA $1040`, then `LDA $1140` | | A = `$6D` |

## 8. The power-on bank and the reset vector

- **Undefined on the hardware.** Nothing on the board sets a known bank at reset [from the suite: roms/cartridge/power-on-bank.asm; from 2k6specs "Cart 4K-Banking": "The initial bank number is undefined upon power-on"; from US 4,368,515: the latch is two cross-coupled NOR gates set and reset only by the hotspot decode, and the slot carries no reset line, from 2k6specs "Cart Pin-Outs"].
- **What the suite reports.** `power-on-bank` is a diagnostic that always passes; it records `$A0` in OBSERVED if the F8 board woke in bank 0 and `$B1` if bank 1 [from the suite: roms/cartridge/power-on-bank.asm]. No console result is recorded for it, since the cartridge tests were not run on hardware [from the suite: README.md].
- **Where games put the reset vector.** Korth: "each bank should contain a valid entry point at [FFFC], and a small power-on procedure" [from 2k6specs "Cart 4K-Banking"]. Every banked image in the suite does so, measured in section 9.2: F8, F6 and F4 tests point every bank's vector at `$F000`, `bank-fa` at `$F200` and `ram-superchip` at `$F100`, each above its RAM ports, and each entry stub first forces bank 0 [from the suite: roms/cartridge/bank-f8.asm, bank-fa.asm, ram-superchip.asm]. Boards with a fixed segment fix the last one "so that the cart always starts up in the exact same place" [from Horton "E0"]; Korth says a 3F board must start in its last bank [from 2k6specs "Cart 2K-Banking (by Port 3Fh)"].
- **The model's choice.** The model must pick one. This sheet recommends **the last bank** (bank 1 on F8, 3 on F6, 7 on F4, 2 on FA), so `power-on-bank` reports `$B1`. Reason: a well-made image starts in any bank, and an image that breaks Korth's rule most likely keeps its start-up in the last bank, as the fixed-segment boards do [inferring from Horton "E0" and 2k6specs] [guessing - verify] for real F8, F6 and F4 games. Bank 0, the suite's "home bank", passes every suite test equally. The choice goes in `docs/known-differences.md`.

## 9. Choosing the board from a file

### 9.1 By size

Each modelled size names one board: 2048 and 4096 plain, 8192 F8, 12288 FA, 16384 F6, 32768 F4 [from section 2]. Size alone does not name the board in general: Horton lists FE, E0 and 3F at 8K and E7 at 16K [from Horton "8K", "16K"], and Korth lists 8K images of four kinds [from 2k6specs "Cart Summary and General Info"]. The spec already says such a game runs as the board of its size and fails.

### 9.2 The Superchip filler rule

The spec's rule: an 8K, 16K or 32K image gets the Superchip when the first 256 bytes of every 4K bank hold one repeated byte.

- **Why filler is expected.** The RAM hides `$1000-$10FF` in every bank, so those image bytes are never read [from section 6]. Korth: "these areas should be 00h or FFh filled in ROM-images", which "allows to determine if a game uses external RAM" [from 2k6specs "Cart Expansion RAM"].
- **Where it fails.** Korth goes on: "Omegarace and Digdug use garbage filled area though" [from 2k6specs "Cart Expansion RAM"]. Omega Race is FA, chosen by size, so it is unaffected. **Dig Dug is a 16K Superchip game** [from Horton, Atari table: Dig Dug, CX2677, 16K, SC, F6], so under the spec's rule it gets a plain F6 board and fails. This contradicts the spec's decision row ("a Superchip image has filler there"). No other image's filler could be checked, because no commercial image was read (rule 4).
- **The suite's image.** `ram-superchip` fills both banks' first 256 bytes with `$E7`, and its source calls that "the SC fingerprint" with the note "0-127 == 128-255" [from the suite: roms/cartridge/ram-superchip.asm]: the suite expects a looser test, the first 128 bytes equal to the next 128. Its image passes both rules.

**Measurement, 14 October 2026.** Every 8K, 16K and 32K binary in the fork `dbhq-uk/missingno` at `a4159f47ce7ef6f9c3d5402a8c58b18940045adc`, all three regions:

```
python3 -I -c 'import sys,glob,os
root=sys.argv[1]; hits=[]; total=0
for p in sorted(glob.glob(root+"/**/*.a26",recursive=True)):
    b=open(p,"rb").read(); n=len(b)
    if n not in (8192,16384,32768): continue
    total+=1
    if all(len(set(b[i:i+256]))==1 for i in range(0,n,4096)): hits.append(os.path.relpath(p,root))
print(total, hits)' crates/systems/vcs/tests/accuracy/roms
```

Output: `63 ['cartridge/ram-superchip_ntsc.a26', 'cartridge/ram-superchip_pal.a26', 'cartridge/ram-superchip_secam.a26']`. So on the suite's files the rule picks the Superchip for `ram-superchip` alone, as the plan's fixed list expects, and leaves `bank-f8`, `bank-f6`, `bank-f4`, `bank-hotspot-window` and `power-on-bank` plain. A second run over the `_ntsc.a26` files printed each bank's reset vector, the figures quoted in section 8 (F8, F6, F4 and their siblings `$F000`; `bank-fa` `$F200`; `ram-superchip` `$F100`).

## 10. Boards in the suite that are not modelled

Read from each header's `; mapper:` line [from the suite: roms/cartridge/*.asm]. Each runs as the board of its size if its size is modelled, and fails; each needs an issue.

| Mapper | Tests |
|---|---|
| 03E0, 0840, 0FA0, BF, DF, E0, E7, EF, F0, FC, FE, JANE, MDM, SB, UA, WD, WF8, X07, 3E, 3E+, 3F | `bank-03e0`, `bank-0840`, `bank-0fa0`, `bank-bf`, `bank-df`, `bank-e0`, `bank-e7`, `bank-ef`, `bank-f0`, `bank-fc`, `bank-fe`, `bank-jane`, `bank-mdm`, `bank-sb`, `bank-ua`, `bank-wd`, `bank-wf8`, `bank-x07`, `bank-3e`, `bank-3e-wide`, `bank-3ep`, `bank-3ep-wide`, `bank-3f` |
| DPC (Pitfall II) | `dpc-bank`, `dpc-drawline`, `dpc-fetch`, `dpc-flag`, `dpc-flag-edges`, `dpc-music`, `dpc-probes`, `dpc-rng`, `dpc-shift`, `dpc-swizzle`, `dpc-window-reads` |
| AR (Supercharger) | `ar-config`, `ar-hazards`, `ar-write` |
| CV (CommaVid RAM) | `ram-cv` |

## 11. Not verified

- Which bank's byte a hotspot read returns on any real board (section 4); the suite and the patent disagree in emphasis and no source measured it.
- FA's D0 gate on a shipped CBS board, and the byte an FA hotspot read returns (section 7.2).
- The ghost write on real Superchip and FA boards, and what a write to a read port does to the bus (sections 6, 7.1).
- What fills `$1000-$10FF` in commercial Superchip images other than Korth's two named exceptions (section 9.2).
- Which bank a real board wakes in, and whether any commercial F8, F6 or F4 game keeps its reset vector in one bank only (section 8).
- That an indexed store always makes its dummy read at the un-carried address (worked example 4): check against Harte's data.

## Sources opened

All read on 14 October 2026.

| Tag | Title, author | URL |
|---|---|---|
| `[from Horton]` | "Cart Information: Info about cart sizes and bankswitching methods", V6.00, Kevin Horton, 18 April 1997 (the Bankzilla machine-readable copy) | https://atariage.com/2600/programming/bankswitch_sizes.txt |
| `[from 2k6specs]` | "Atari 2600 Specifications", Martin Korth | https://problemkaputt.de/2k6specs.htm |
| `[from US 4,368,515]` | "Bank switchable memory system", Carl J. Nielsen, Atari Inc, filed 7 May 1981, granted 11 January 1983 | https://patents.google.com/patent/US4368515A/en |
| `[from US 4,432,067]` | "Memory cartridge for video game system", Carl J. Nielsen, Atari Inc, a division of the above, granted 14 February 1984 | https://patents.google.com/patent/US4432067A/en |
| `[from US 4,485,457]` | "Memory system including RAM and page switchable ROM", Richard K. Balaska, Robert L. Hunter, Scott S. Robinson Jr., CBS Inc, filed 31 May 1983 | https://patents.google.com/patent/US4485457A/en |
| `[from the 6502 spec]` | This repository's CPU design, `docs/superpowers/specs/2026-09-29-6502-design.md` | in the repository |
| `[from the suite: ...]` | `dbhq-uk/missingno-vcs-tests` at `891c079976a2f68033ca4af2fe2fd85aaf1906c9`: `README.md` and `roms/cartridge/*.asm` | in the clone |

The binaries measured in section 9.2 are from `dbhq-uk/missingno` at `a4159f47ce7ef6f9c3d5402a8c58b18940045adc`; only their bytes were read. An AtariAge forum thread on Superchip RAM was refused (a Cloudflare challenge) and had no Internet Archive copy; it is not used. No emulator's code was read.
