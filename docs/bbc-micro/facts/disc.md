# BBC Micro Model B disc: Intel 8271, DFS 1.20, .ssd/.dsd, catalogue and *CAT text

Written 2 Oct 2026 for the BBC Micro work in `dbhq-uk/6502`. No repo was edited. Nothing here is code from jsbeeb or B-em: **I did not open either of them at all** (no cross-check was needed).

**How the facts were made.** Most of sections 2, 3, 5 and 6 were not only read: I wrote a throwaway Python machine (6502 + MOS 1.20 + BASIC + the real `DFS-1.2.rom` + an 8271 written from the datasheet) in `/tmp/bbc-facts/tools/` and ran `*CAT`, `*SAVE`, `*LOAD`, `*RUN`, `*INFO`, `*TITLE`, `*OPT`, `*ACCESS`, `*DELETE` and the error cases against it. The machine boots to the BASIC prompt and prints the banner. What this proves is the DFS-visible behaviour of the 8271, nothing more (see section 8). Tag `[TRACE]` means "observed in that run".

## Source key

| Tag | Source | URL / path |
|---|---|---|
| D1 | Intel 8271/8271-6 datasheet, 1981 Intel Component Data Catalog pp 8-115 to 8-142 (PDF pages 743-767). Text layer is poor OCR, so every table below was read from the page images | https://bitsavers.org/components/intel/_dataBooks/1981_Intel_Component_Data_Catalog.pdf (plain `curl` gets 403, send a browser User-Agent) |
| S1 | Advanced User Guide, ch 25.1 (register list), OSBYTE &FF text | `/tmp/bbc-facts/src/aug.txt` |
| S2 | Acorn Service Manual 5.5.1 (8271 interface) | `/tmp/bbc-facts/src/svc.txt` |
| B1 | BeebWiki, OSWORD &7F (web.archive.org snapshot; live beebwiki refused) | https://web.archive.org/web/2023/https://beebwiki.mdfs.net/OSWORD_%267F |
| B2 | BeebWiki, Acorn DFS disc format (web.archive.org snapshot) | https://web.archive.org/web/2024/https://beebwiki.mdfs.net/Acorn_DFS_disc_format |
| G1 | DFS User Guide Issue 2 with Addendum 1 (error list, `*CAT` example) | https://chrisacorns.computinghistory.org.uk/docs/Acorn/Manuals/Acorn_DiscSystemUGI2.pdf |
| T1 | Stardot "SSD File Format" (lurkio, jgharston, Coeus) | https://stardot.org.uk/forums/viewtopic.php?t=24533 |
| T2 | Stardot "DFS floppy capacity and dsd format" (sweh, 1024MAK) | https://stardot.org.uk/forums/viewtopic.php?t=19104 |
| T3 | scarybeasts, reverse engineering the 8271 (specify = three special-register writes) | https://scarybeastsecurity.blogspot.com/2020/11/reverse-engineering-forgotten-1970s.html |
| T4 | Stardot 8271 de-cap thread, seen **only as a search-result summary** (registers 0D/0E/0F) | https://stardot.org.uk/forums/viewtopic.php?t=19762&start=120 |
| ROM | `/tmp/bbcrom/DFS-1.2.rom`, CPU base `$8000` (file offset = address - `$8000`). Header: type `$82`, title `DFS,NET`, `DFS 1.20` at `$BA2C`, `Acorn DFS` at `$B3B4`. Disassembly in `/tmp/bbc-facts/tools/dfs.dis` (linear, so inline strings and tables appear as junk) | local |
| OS | `/tmp/bbcrom/os.rom` (MOS 1.20) | local |
| TRACE | My Python machine: `cpu.py`, `fdc8271.py`, `bbc.py`, scenarios `s1.py` to `s13.py` | `/tmp/bbc-facts/tools/` |

---

## 1. The 8271 as the BBC sees it

### 1a. Registers and decode

| CPU addr | Read | Write | Source |
|---|---|---|---|
| `$FE80` | Status register | Command register | [from D1 p8-127, S1 s25.1] |
| `$FE81` | Result register | Parameter register | [from D1, S1] |
| `$FE82` | nothing | Reset register: write 1 then 0 (held 11+ chip clocks per D1 p8-118; the reset pin needs 10+) | [from D1 p8-118, p8-127; ROM `$AB84-$AB8B` does `LDA #1: STA $FE82: TAX: DEX: STX $FE82`] |
| `$FE83` | not used | not used | [from S1] |
| `$FE84` | Data (non-DMA read) | Data (non-DMA write) | [from S1, S2 5.5.1, D1 p8-125] |
| `$FE85-$FE87` | same as `$FE84` (nDACK low for `$FE84-$FE87`) | same | [from S2 5.5.1] |
| `$FE88-$FE9F` | Repeats the block above: IC21 enables `$FE80-$FE9F`, IC28 splits it by address bit 2 only | | [inferring from S2 5.5.1; nothing uses it] |

Data accesses are a separate path: DACK low and CS high means data; D1 forbids both low (D1 p8-125 table). A1:A0 = 00 status/command, 01 result/parameter, 10 reset (D1). Nothing in DFS 1.20 touches `$FE83` or `$FE85-$FE9F` [from ROM: the only `$FE8x` operands are `$FE80`, `$FE81`, `$FE82`, `$FE84`].

### 1b. Status register (`$FE80` read) [from D1 p8-127]

| Bit | Name | Meaning | Does DFS 1.20 read it? |
|---|---|---|---|
| 7 | Command busy | Set when the command is written, clear when the command is complete | yes, `BIT`/`BMI` at `$AC9B`, `$ACAE` |
| 6 | Command full | Set on command write, clear when the chip starts on it | **no** |
| 5 | Parameter full | Set on parameter write, clear when the chip has taken it. Writing while set loses the earlier parameter | yes, `AND #$20` at `$AC89` |
| 4 | Result full | Set when a result is ready, clear when `$FE81` is read | **no** |
| 3 | Interrupt request | Mirrors the INT pin. Cleared by reading the result. In non-DMA mode INT is also raised for every data byte | yes, `AND #$08` at `$AD55` (completion test inside the NMI handler) |
| 2 | Non-DMA data request (NDRQ) | A byte is waiting (read) or wanted (write) at `$FE84` | yes, `AND #$04` in every data handler |
| 1:0 | not used | read as 0 | yes: `$B495` tests `AND #$03` against 0 |

Values the DFS run saw [TRACE]: idle `$00`; after a command byte `$C0` until the first parameter is taken (DFS only waits for bit 5); data phase `$8C` (busy, INT, NDRQ); completion `$18` (INT, result full, busy clear); immediate-result commands `$10`.
**Rule for the model:** busy must be clear before the result is read, because DFS polls busy twice in a row and then reads `$FE81` (`$ACAE`-`$ACAA`). A model that keeps busy set until the result is read deadlocks [from ROM; not tried].

### 1c. Command byte and the commands

Command byte: bits 7:6 = drive select (SEL1:SEL0), bits 5:0 = opcode [from D1 p8-115 and the command diagrams]. **On the BBC drive 0 is bit 6 (`$40`) and drive 1 is bit 7 (`$80`)**; DFS builds all its commands with `$40` and does `EOR #$C0` for drive 1 and 3 (`$AC92-$AC99`). [from ROM `$AC92`, B1]

| Opcode (low 6 bits) | With drive 0 (as DFS sends) | Command | Params | Result | Used by DFS 1.20 `*CAT/*SAVE/*LOAD/*INFO/*RUN` |
|---|---|---|---|---|---|
| `$00` / `$04` | `$40` / `$44` | Scan data / scan data and deleted | 5 | INT + result | no. Needs DMA, impossible on a BBC [from B1] |
| `$0A` / `$0B` | `$4A` / `$4B` | Write data: 128-byte single record / variable length | 2 / 3 | INT + result | **`$4B`** (catalogue and file data) |
| `$0E` / `$0F` | `$4E` / `$4F` | Write deleted data | 2 / 3 | INT + result | no |
| `$12` / `$13` | `$52` / `$53` | Read data | 2 / 3 | INT + result | **`$53`** |
| `$16` / `$17` | `$56` / `$57` | Read data and deleted data | 2 / 3 | INT + result | no |
| `$1E` / `$1F` | `$5E` / `$5F` | Verify data and deleted data (no transfer) | 2 / 3 | INT + result | **`$5F`**, once per catalogue rewrite |
| `$1B` | `$5B` | Read ID | 3 (track, 0, count) | INT + result, 4 bytes per ID by NDRQ | no (utilities disc only) |
| `$23` | `$63` | Format track | 5 (track, gap3, size<<5 or count, gap5, gap1) | INT + result, 4 ID bytes per sector by NDRQ | no (`*FORM` is on the utilities disc) |
| `$29` | `$69` | Seek | 1 (track) | INT + result | **`$69 00`**, before every operation |
| `$2C` | `$6C` | Read drive status | 0 | **immediate result, no INT** | **`$6C`**, ready test |
| `$35` | `$35` (select bits 00) | Specify | 4 (type + 3) | none, no INT | yes, at reset |
| `$3A` | `$3A` | Write special register | 2 (register, value) | none | yes |
| `$3D` | `$3D` / `$7D` | Read special register | 1 (register) | **immediate result, no INT** | yes |

Param counts [from D1 p8-129, p8-136/137, B1]. Variable-length parameter 3 is `size<<5 | sector count`; DFS always sends size = 1 (256 bytes), so `$20 | n`, e.g. `$22` (2 sectors), `$2A` (10) [TRACE]. Params 1 and 2 of read/write/verify are track and first sector. Datasheet 128-byte opcodes `$12`, `$0A`, `$16`, `$0E`, `$1E` take 2 params and no size.
All commands except specify, write special and the two immediate ones set INT and a result at the end. The datasheet's own check: opcode (select bits masked) below `$2C` gives a standard result with INT; `$2C` and above gives an immediate result [from D1 p8-125 fig 17 note].

**Specify (`$35`)** [from D1 p8-129]: first parameter selects the type.

| Param 0 | Type | Params 1-3 |
|---|---|---|
| `$0D` | Initialisation | step rate (1 ms units, doubled for mini-floppy), head settle time (1 ms units, doubled), (index count before head unload << 4) or head load time (4 ms units, doubled). Index count 15 = head stays loaded |
| `$10` | Load bad tracks, surface 0 | bad track 1, bad track 2, **current track** |
| `$18` | Load bad tracks, surface 1 | same |

T3 says specify has the same effect as three consecutive write-special-register commands. [from T3; T4 snippet gives registers `$0D` step, `$0E` settle, `$0F` index count/head load]

### 1d. Result register [from D1 p8-127/128; B1 agrees]

`D7:D6 = 0`, `D5` = deleted data found (add `$20`), `D4:D3` = completion type, `D2:D1` = completion code, `D0` = 0.

| Result | Type | Code | Meaning | DFS 1.20 reaction [TRACE, all 11 attempts then error] |
|---|---|---|---|---|
| `$00` | 00 | 00 | Good | continue |
| `$02` / `$04` | 00 | 01 / 10 | Scan met equal / not equal | n/a |
| `$08` | 01 | 00 | Clock error | `Disk fault 08 at tt/ss` |
| `$0A` | 01 | 01 | Late DMA (data not taken in time) | `Drive fault 0A at tt/ss` |
| `$0C` | 01 | 10 | ID CRC error | `Disk fault 0C at tt/ss` |
| `$0E` | 01 | 11 | Data CRC error | `Disk fault 0E at tt/ss` |
| `$10` | 10 | 00 | Drive not ready (latched until a Read Drive Status) | `Drive fault 10 at tt/ss` |
| `$12` | 10 | 01 | Write protected | `Disk read only` (`$C9`), checked before the others |
| `$14` | 10 | 10 | Track 0 not found (255 steps) | `Drive fault 14 at tt/ss` |
| `$16` | 10 | 11 | Write fault | `Drive fault 16 at tt/ss` |
| `$18` | 11 | 00 | Sector not found (not seen in two index pulses, or track field mismatch after auto-stepping twice) | `Disk fault 18 at tt/ss` |

Rule inferred from the ROM and confirmed by the table: `$12` is special; `$0A` or a low nibble below 8 gives `Drive fault`; everything else gives `Disk fault`. DFS retries a failing command 10 more times (11 attempts in all) [TRACE, `$AADC-$AAED`].

### 1e. Read Drive Status result (`$6C`) and drive input pins [from D1 p8-129 zoomed; B1 agrees]

| Bit | Pin | Notes |
|---|---|---|
| 7 | not used | |
| 6 | RDY1 | **On the BBC one ready circuit (index-pulse timer IC4) drives both ready pins, so bits 6 and 2 are set together** [from S2 5.5.1] |
| 5 | FAULT | |
| 4 | INDEX | |
| 3 | WR PROT | set when the image is write protected |
| 2 | RDY0 | **the only bit DFS looks at**: `AND #$04` at `$AA92`. Set = ready |
| 1 | TRACK 0 | **DFS never reads it**: it homes with `$69 00` and does not test the pin |
| 0 | COUNT/OPI | |

Ready bits are "zero latching": a not-ready is held until Read Drive Status is issued, and to clear it on a ready drive you issue it twice [from D1 p8-129 footnote]. Real ready needs the motor on and then two index pulses, about 400 ms, because the BBC times index-to-index and wants it under 213 ms [from S2 5.5.1].

### 1f. Special registers [from D1 p8-130 table 4; B1]

| Reg | Meaning |
|---|---|
| `$06` | Scan sector number (DFS reads it after an error to print `at tt/ss`) |
| `$0D`, `$0E`, `$0F` | step rate, head settle, index count (hi nibble) and head load (lo nibble). Not in D1 [T3, T4] |
| `$10` `$11` | surface 0 bad tracks 1, 2 |
| `$12` | surface 0 current track |
| `$13` `$14` | scan count LSB, MSB |
| `$17` | **Mode register**: `1 1 0 0 0 0 S N`. `N` = 1 non-DMA, `S` = 1 single actuator. **DFS writes `$C1`** (`3A 17 C1`, table `$ACFC`) [from D1 p8-130, ROM] |
| `$18` `$19` `$1A` | surface 1 bad tracks 1, 2 and current track |
| `$22` | Drive control **input** port (pin states). Bit layout not given in D1 [guessing: same as 1e; DFS never reads it] |
| `$23` | Drive control **output** port, below |

**`$23`, drive control output port** [from D1 p8-131, B1; ROM `$AB94`]:

| Bit | Pin | On the BBC |
|---|---|---|
| 0 | WRITE ENABLE | |
| 1 | SEEK/STEP | |
| 2 | DIRECTION | |
| 3 | LOAD HEAD | **drive motor**; DFS tests it after writing (`7D 23`, `AND #$08`) |
| 4 | LOW HEAD CURRENT | |
| 5 | FAULT RESET / optional output | **side select**: 0 = side 0, 1 = side 1 [from B1; confirmed by the DFS values below] |
| 6 | SELECT 0 | drive 0 |
| 7 | SELECT 1 | drive 1 |

Values DFS writes (table `$AB94`, indexed by DFS drive number 0-3) [from ROM, TRACE: `3A 23 48`, `3A 23 68`]:

| DFS drive | Meaning | `$23` value |
|---|---|---|
| 0 | physical 0, side 0 | `$48` |
| 1 | physical 1, side 0 | `$88` |
| 2 | physical 0, side 1 | `$68` |
| 3 | physical 1, side 1 | `$A8` |

Per D1 p8-124, when a new command selects different drive bits the chip clears bits 0-4 (WE, step, dir, load head, low current), and it clears them and the select bits after the index-count unload. So DFS sets `$23`, then reads it back with `7D 23` and loops until bit 3 is set (`$AB5B-$AB60`).

### 1g. Non-DMA operation on the BBC [from S2 5.5.1, D1 p8-118/128]

| Item | Fact |
|---|---|
| INT | 8271 INT (IC15 pin 11) is inverted by an open-collector NAND (IC7) to nNMI, wire-ORed with Econet, into the CPU via AND gate IC34. **INT high = NMI asserted** [from S2 5.5.1] |
| NMI vector | `$FFFA` = `$0D00` (MOS 1.20 vector bytes `00 0D`). The OS plants an RTI at `$0D00` at reset [from `bus.md` s1a, ROM] |
| DMA pins | Not wired. Status bit 2 (NDRQ) replaces DRQ [from S2, T3 blog: "DMA capability wasn't wired up at all"] |
| Per byte | Chip raises NDRQ and INT. Handler reads or writes `$FE84`, which clears both. Next byte is one byte-time later. NMI is edge triggered, so INT must fall and rise again for every byte [inferring: the only way DFS can work; D1 only says "an interrupt is generated with each data byte" p8-128] |
| Completion | After the last byte: INT with result full, busy clear. INT stays until `$FE81` is read |
| Byte time | Bit cell 8 us, so **64 us per byte = 128 CPU cycles at 2 MHz** [from S2 5.5.1: "each bit interval is 8us"]. D1's "one byte every 32 us" is the 8-inch figure |
| Late data | D1 p8-118: "not serviced within 31 us" gives result `$0A`. D1 says mini-floppy timings are doubled (p8-122/123, p8-141 note 3) so the BBC limit is about one byte time. [inferring]. Not reached by DFS in any run |

### 1h. Timing DFS actually depends on [TRACE; each row is a separate run of save, fresh machine, load, compare 512 bytes]

| Variation | DFS result |
|---|---|
| Byte every 128 cycles, instant seek/settle/ready, late-DMA error on | pass (baseline) |
| Byte every 100, 90, 80 cycles | pass |
| Byte every **76 cycles** | **crash** (nested NMI, bad opcode) |
| Byte every 70 and 64 cycles | crash / `Disk fault 18` |
| Byte every 300 or 2000 cycles | pass |
| No late-DMA error, bytes paced at 128 | pass |
| No late-DMA error, next byte the instant the last is taken (0 spacing) | **fails** (nested NMI) |
| Parameter-full flag held 0, 8, or 200 cycles | pass |
| Delay between last byte and completion INT: 0, 64, 300, 5000, 60000 cycles | pass |
| Real-ish timings: step 8000 cycles/track, settle 32000, 200000 cycles to find a sector, spin-up 800000 cycles | pass (SAVE+LOAD 2.7 M cycles) |
| Seek, settle, head load, spin-up all zero | pass |

Reason for the crash: the DFS data handler takes **71 cycles (write) and 77 cycles (read) from NMI taken to RTI complete** [TRACE]. A byte arriving before that nests the NMI and the handler is not re-entrant. The completion handler takes about 183 cycles before it returns, and then runs more DFS code for the next command.
**Measured response:** the DFS handler touches `$FE84` between 23 and 41 cycles after the byte is presented (20 us at worst) [TRACE, resolution about 4 cycles because my bus time is the start of the instruction].

**Conclusions:** DFS 1.20 does not depend on step rate, settle time, head load time or spin-up. It depends only on (1) bytes at least about 80 cycles apart, (2) NMI taken within one byte time, (3) the status bit rules in 1b, and (4) a ready bit that eventually goes true. It polls ready with no timeout: with an empty drive it issues `6C` about 25 000 times in 3 M cycles and stops only on Escape, which gives the error `Escape` (`$11`) [TRACE].

---

## 2. How DFS 1.20 uses it (read from the ROM, confirmed in TRACE)

| What | Where / value |
|---|---|
| Every 8271 command byte | One routine: `$AC92` (applies the `EOR #$C0` for odd drives) falls into `$AC9B`, which waits for busy clear then `STA $FE80` at `$ACA0`. `$AC9B` is the entry that skips the drive swap |
| Every parameter | `$AC85`: waits until `$FE80` bit 5 is clear, then `STA $FE81` at `$AC8E` |
| Result | `$ACA7`: `$ACAE` waits busy clear twice, then `LDA $FE81` |
| Command table (inline, `$ACD8`, terminated by `$EA`) | `35 0D 02 08 C0` / `35 0D 03 08 C0` / `35 0D 03 08 C7` / `35 0D 0C 0A C8` (step/settle/load choices picked by OSBYTE `$FF` bits 5:4: offset 0, 6, 12, 18), `35 10 FF FF 00`, `35 18 FF FF 00`, `3A 17 C1`, `69 00` (seek 0), `5F 00 08 22` (verify track 0 sectors 8-9), `3D 06` (read sector reg), `7D 23` (read output port). Step 2 = 4 ms, settle 8 = 16 ms |
| 8271 reset | `$AB84`: `$FE82` = 1 then 0, then the specify table, then re-init |
| Drive-ready test | `$AA84`: sends `6C`, reads the result, returns `AND #$04` |
| Drive/side select | `$AB44-$AB5E`: `3A 23 <48/88/68/A8>` then `7D 23`, loop until result bit 3 is set |
| NMI claim | `$B920`: OSBYTE `$8F` with X = `$0C` (service call "NMI claim"), Y = `$FF`, then copies a handler from the ROM into `$0D00`. Flag `$10C8`. The ROM number is patched into the copy at `$0D3C` (`LDA $F4: STA $0D3C`, `$B955`) |
| NMI release | `$B96A`: OSBYTE `$8F` with X = `$0B` |
| Handler table (`$B9F2` low, `$B9F9` high, `$BA00` length-1) | index 0 = `$AD21`, 78 bytes (**write**, with a 19-byte overlay from `$AD0E` placed at `$0D0A`); 1 = `$AD21`, 78 bytes (**read**); 2 = `$AD51`, 1 byte (**just RTI**, for seek and verify); 3 = `$ADB5`, 16 (write from Tube); 4 = `$AD8A`, 27 (OSWORD `$7F` write); 5 = `$ADA5`, 16 (read to Tube); 6 = `$AD6F`, 27 (OSWORD `$7F` read). `*CAT/*SAVE/*LOAD` use only 0, 1 and 2 |
| What the read handler does (`$0D00`) | `PHA/TYA/PHA`; `LDA $FE80: AND #4`; if set: `LDA $FE84`, if the byte count (`$A3-$A5`) is not exhausted store at `($A6)` and decrement; `PLA/TAY/PLA/RTI`. If NDRQ is clear: `LDA $FE80: AND #8`; if INT: switch ROM to DFS (`STA $FE30`), `JSR $AC0E` (read result, set up the next track's command), switch ROM back |
| Mode | Non-DMA: `3A 17 C1` |

### 2a. Command sequences DFS sent [TRACE, blank 40-track image, drive 0]

| Operation | Commands (drive 0; `<n>` = a count) |
|---|---|
| First command after boot (spin up, once) | `6C`, `3A 23 48`, `7D 23`, `6C` |
| Every operation, start | `6C` (ready), `69 00`, `69 00` (seek track 0, twice) |
| `*CAT` / `*INFO` / `*RUN` / `*LOAD` catalogue read | `53 00 00 22` (read data, track 0, sector 0, 2 x 256 bytes) |
| `*SAVE` | catalogue read as above, then `6C`, `69 00`, **`5F 00 08 22`** (verify), **`4B 00 00 22`** (write catalogue), then data `4B <track> <sector> <size 1, n sectors>` track by track. Example 256 bytes: `4B 00 02 21`. Example 12 KB: `4B 00 03 27`, `4B 01 00 2A`, `4B 02 00 2A`, `4B 03 00 2A`, `4B 04 00 2A`, `4B 05 00 21` |
| `*LOAD` / `*RUN` | catalogue read, then `53 <track> <sector> <0x20 or n>` per track, e.g. `53 00 03 27`, `53 01 00 2A` ... |
| `*TITLE`, `*OPT 4`, `*ACCESS`, `*DELETE` | catalogue read, then `5F 00 08 22`, `4B 00 00 22` |
| `*CAT` with side 1 (drive 2) | same, but `3A 23 68` first |
| After a failure | up to 11 attempts of the same command, then the error |

A command never crosses a track. The data NMI count equals 256 x sectors; the last sector is always read in full, and DFS discards bytes past the file length (the count in `$A3-$A5` runs out) [TRACE].

### 2b. DFS caches the catalogue: what this means for disc swaps [TRACE]

DFS keeps the catalogue in RAM (`$0E00-$0FFF`) and re-reads it only when its ready test (`6C`, bit 2) says **not ready**, or when the drive number changed. It does not look at a changed image. Measured:

| Event | Catalogue read? |
|---|---|
| `*INFO *` twice, same disc | second one: no |
| Swap the image behind DFS's back, immediately `*INFO *` | **no** (stale: still shows the old files) |
| Swap the image, idle for 5 M cycles (the 8271 unloads the head after index count 12 x 200 ms = 2.4 s = 4.8 M cycles, which clears LOAD HEAD, so ready drops), then `*INFO *` | yes |
| Disc removed for 2 M cycles, then another inserted | yes |

So the model's **ready must follow the motor** (special register `$23` bit 3 on the selected drive), and the head must unload after the specified index count. For a "drop a new .ssd on the page" feature, either reset the machine or force one not-ready on the next `6C`.

### 2c. Boot banner [TRACE, with no key held]
`BBC Computer 32K` / `Acorn DFS` / `BASIC` / `>` (the DFS prints `Acorn DFS` on the boot service call, `*HELP` prints `DFS 1.20`).

---

## 3. Minimal-but-correct 8271 design (DFS cannot tell it from a real drive)

Evidence for every line is in 1h and 2b.

| Part | Design | Why it is safe |
|---|---|---|
| Registers | as 1a, 1b. Status bits 7, 5, 3, 2 must be right; 6 and 4 are not read by DFS but give them the real values | ROM reads only those bits |
| Command accept | write sets busy, takes params, runs when the count is reached. Param-full may clear after 0 cycles | 0, 8, 200 cycles all pass |
| Specify, write special, read special, read status | instant, update the register file. Read ones set result-full, clear busy, **no INT** | DFS polls busy then reads `$FE81` |
| Seek | instant: set the surface's current-track register and the drive's physical track; seek to track 0 sets both to 0 | zero step/settle passes |
| Ready | `disc present AND motor on`, motor = LOAD HEAD (port `$23` bit 3) of the selected drive; the 8271 sets LOAD HEAD itself when it runs a data command. **Spin-up may be instant** | DFS only polls; real 800 000 cycles also passes |
| Head unload | `index count x 400 000` cycles after the last command (200 ms per revolution at 2 MHz), clear port bits 0-4 and 6-7, motor off. Count 15 = never | This is how DFS notices a disc swap (2b) |
| Data transfer | after any start delay K (0 was fine; use about 2 000 cycles to look plausible [guessing]) present one byte every **128 cycles**: set NDRQ and INT (INT rises, NMI edge), clear both when `$FE84` is accessed. Sector gap: 0 passes, real-ish `16 x 64` passes | min safe spacing is about 80, real is 128 |
| Late data | if a byte is not taken within one byte time (128 cycles) end the command with `$0A`. Optional: waiting forever also passes. **Never space bytes closer than about 80 cycles, and never nest** | rows 76 and 0 in 1h |
| Completion | after the last byte: busy clear, result-full, INT high (NMI edge). Delay 0 to 60 000 passes | |
| Not-ready latch | set on a data command with no ready drive: result `$10`, until Read Drive Status | |
| Commands without a disc | with no image in the drive the ready bit is never set and DFS polls forever until Escape. That is real behaviour [TRACE], not a bug to fix | |
| Image mapping | `(side, track, sector)` to offset as in section 4, ignore sector skew. Anything outside the image gives `$18` | DFS shows `Disk fault 18 at tt/ss` (section 6) |
| Write protect | image read-only gives `$12` on every write command and bit 3 set in `6C` | DFS says `Disk read only`, after 11 attempts |
| Not needed for DFS | scan, deleted data, read ID content, format. If format is wanted, `*FORM` is a utilities-disc program, not in this ROM | |

A tidy unit test for the chip alone: after `3A 17 C1`, `35 0D 02 08 C0`, `69 00`, `53 00 00 22`, expect 512 NMIs 128 cycles apart, then INT, status `$18`, result `$00`.

---

## 4. Disc image formats

| Format | Layout | Source |
|---|---|---|
| `.ssd` | Single sided. Just the sector data in order, **256-byte sectors, 10 per track, track by track**. Offset = `(track x 10 + sector) x 256`. No IDs, no gaps, no header | [from B2, T1, T2] |
| 40-track `.ssd` | 40 x 10 x 256 = **102 400** bytes (400 sectors, LBA 0 to `$18F`) | [from B2, T2 (sizes)] |
| 80-track `.ssd` | **204 800** bytes (800 sectors, LBA 0 to `$31F`) | [same] |
| `.dsd` (common) | Double sided, **track interleaved**: 2 560 bytes of track 0 side 0, then 2 560 of track 0 side 1, then track 1 side 0, and so on. Offset = `((track x 2 + side) x 10 + sector) x 256`. Side 0 is DFS drive 0, side 1 is drive 2 | [from T1 (jgharston), T2] |
| `.dsd` (other) | Some images are **sequential** (all of side 0, then all of side 1). Sizes: 40-track double sided 204 800, 80-track double sided 409 600. T1 says jgharston's CP/M `.dsd` files are sequential and the interleaved form is "most common"; Coeus says do not assume | [from T1] |
| Sector numbers | Logical 0-9 on each track, so DFS asks for sector 0-9; the physical on-disc skew of 3 is not stored | [from B2] |

Side select is **not** a command parameter. It is bit 5 of special register `$23`, which DFS sets before the command (`3A 23 68`), then sends the same `53 00 00 22` [TRACE: side 1 track 0 sector 0 gave the file offset 2 560 catalogue].

**40 or 80 track?** Nothing in the 8271 or the DFS tells you. The 8271 has no track count, and the DFS never probes for one. Use, in this order [from B2, T1, T2]:
1. Size and extension: 102 400 = 40-track ssd; 204 800 = 80-track ssd **or** 40-track dsd (the extension decides); 409 600 = 80-track dsd.
2. The catalogue disc-size field: 400 sectors = 40 tracks, 800 = 80. B2 lists "400 or 800" as the typical heuristic.
3. Image bytes carry no header, so T1/Coeus say size plus a catalogue that looks right is the only evidence.
What the DFS does with it: it takes the sector count only from the catalogue (sector 1 bytes 6-7). It will happily write file data up to that size [TRACE: an 80-track blank image took `*SAVE` normally]. The user guide gives the real-hardware symptom: `Disk fault` for "an 80 track disc in a 40 track drive" [from G1 p84]. A 40-track drive simply cannot step past about track 40; the emulator does not need to model that.

**Short images** (file smaller than the catalogue claims, as with trimmed images): there is no source for what a real disc does, because a real disc is always whole. In the model I gave a missing sector `$18`: DFS then reports `Disk fault 18 at 03/00` after 11 attempts [TRACE: 3-track image, `*SAVE` of 8 KB spilling to track 3]. Reading files that fit inside the short image works normally. **Recommendation** [guessing, not tested]: for a short image, extend it with zeros when a write goes past the end, and treat reads past the end of an otherwise valid catalogue as zero too; pick one and put it in `known-differences.md`.

---

## 5. Acorn DFS catalogue layout

All from B2, confirmed byte for byte in TRACE (the DFS ROM read and wrote catalogues built from this description).

### 5a. Sectors 0 and 1 (track 0)

| Where | Bytes | Content |
|---|---|---|
| Sector 0, 0-7 | 8 | First 8 characters of the disc title (padded NUL or space) |
| Sector 0, 8-15 + 8n | 8 each | File n (0 to 30): name, 7 chars padded with spaces, then directory character. **Bit 7 of byte 15 = locked** |
| Sector 1, 0-3 | 4 | Last 4 characters of the title |
| Sector 1, 4 | 1 | **Cycle number**, BCD, incremented every time the catalogue is rewritten (DFS uses `SED`) |
| Sector 1, 5 | 1 | **File count x 8** (offset of the last entry). Max 31 files = 248 |
| Sector 1, 6 | 1 | Bits 5:4 = boot option (0 none, 1 `*LOAD`, 2 `*RUN`, 3 `*EXEC` of `$.!BOOT`). Bits 1:0 = **bits 9:8 of the sector count**. Other bits must be 0 |
| Sector 1, 7 | 1 | **Low 8 bits of the total sector count** |
| Sector 1, 8-15 + 8n | 8 each | File n: load low, load high, exec low, exec high, length low, length high, **byte 14** (packed, below), start sector low |

Byte 14 of file entry n (sector 1, offset 14 + 8n):

| Bits | Meaning |
|---|---|
| 7:6 | bits 17:16 of the **exec** address |
| 5:4 | bits 17:16 of the **length** |
| 3:2 | bits 17:16 of the **load** address |
| 1:0 | bits 9:8 of the **start sector** |

Rules [from B2]: files sorted in **descending** start sector, no gaps in the table; start sector below 2 is invalid; a file is `start .. start + ceil(length/256) - 1`; data lives in sector `start` on, no fragmentation. If bits 17:16 of load or exec are both set the address is "I/O processor" and `*INFO` shows `FFxxxx`. The title is up to 12 characters; DFS 1.20 pads a `*TITLE` with spaces (NULs also valid) [from B2].

### 5b. Empty catalogue, built from the description [from B2: "clear the two sectors and then initialise the disc size field"]. This exact image booted and ran `*CAT`, `*SAVE`, `*LOAD`, `*RUN` under DFS 1.20 [TRACE]

Everything not listed is `$00`. Sector 0 is all `$00`. Sector 1 (the 256 bytes at file offset 256):

| Image | Byte 4 (cycle) | Byte 5 (files x 8) | Byte 6 | Byte 7 | Total sectors |
|---|---|---|---|---|---|
| 40-track, single sided | `00` | `00` | **`01`** | **`90`** | 400 = `$190` |
| 80-track, single sided | `00` | `00` | **`03`** | **`20`** | 800 = `$320` |

```
40-track, offset 0x100:  00 00 00 00 00 00 01 90 00 00 00 00 00 00 00 00 ...
80-track, offset 0x100:  00 00 00 00 00 00 03 20 00 00 00 00 00 00 00 00 ...
```
(For a boot option put `n << 4` in byte 6: `$21` is `*RUN` on a 40-track disc.) For a `.dsd`, build the same pair for each side at offset 0 and offset 2 560, with side 1 using the same disc-size bytes. The rest of the image can be `$00`; a freshly `*FORM`ed disc would hold `$E5` fill [from D1 p8-133 "E5 data pattern"], which DFS does not care about.

### 5c. Worked example 1: `*SAVE TEST 2000 2100 2000 2000` on the blank 40-track disc with title `MYTITLE` [TRACE]

| | Bytes (hex) | Read as |
|---|---|---|
| Sector 0, 0-7 | `4D 59 54 49 54 4C 45 00` | `MYTITLE` + NUL |
| Sector 0, 8-15 | `54 45 53 54 20 20 20 24` | name `TEST` padded, directory `$` (bit 7 clear: not locked) |
| Sector 1, 0-3 | `00 00 00 00` | title tail |
| Sector 1, 4-7 | `01 08 01 90` | cycle 01, 1 file (8), boot 0 + sector-count high bits 01, low `90` = 400 |
| Sector 1, 8-15 | `00 20 00 20 00 01 00 02` | load `$2000`, exec `$2000`, length `$0100`, byte 14 = `00`, start sector `$02` |

Data is in sector 2 (`4B 00 02 21`); the file data goes after the catalogue, sectors 2 up.

### 5d. Worked example 2: high bits and a start sector above 255 [TRACE, with a hand-built catalogue]

Disc held one file `FILL` at sectors 2-338 (length `$15100`), then OSFILE (as BASIC `SAVE "HELLO"` would) saved `HELLO` with load `$FFFF1900`, exec `$FFFF8023`, length `$0123`. DFS placed it after FILL at sector `$153` = 339 and put its entry **first** (descending order):

| | Bytes | Working |
|---|---|---|
| Sector 0, 8-15 (new first entry) | `48 45 4C 4C 4F 20 20 24` | `HELLO` + pad, `$` |
| Sector 0, 16-23 | `46 49 4C 4C 20 20 20 24` | `FILL`, moved down |
| Sector 1, 8-15 (HELLO) | `00 19 23 80 23 01 CD 53` | load lo/hi `00 19`; exec lo/hi `23 80`; length lo/hi `23 01`; byte 14 `CD`; start low `53` |
| Sector 1, 16-23 (FILL) | `00 19 00 19 00 51 10 02` | load `$1900`, exec `$1900`, length low/mid `00 51`, byte 14 = `10` (length bits 17:16 = 01), start `02` |
| Sector 1, 5 | `10` | 2 files |

Byte 14 of HELLO by hand: exec high bits 11 -> `$C0`; length high bits 00 -> `$00`; load high bits 11 -> `$0C`; start sector high bits (`$153 >> 8` = 1) -> `$01`; sum **`$CD`**. Matches. `*INFO *` printed `$.HELLO       FF1900 FF8023 000123 153`.
A locked file sets bit 7 of sector 0 byte 15 (`A.HELLO` locked is `C1` at that byte) [from B2; TRACE `*ACCESS A.HELLO L` printed `L`].

---

## 6. What the DFS prints [TRACE: captured from OSWRCH, one run each]

OSASCI sends a newline as `LF CR` (`0A 0D`). DFS prints the title through OSWRCH including NUL bytes, and some option names are padded with a NUL. **NUL is VDU 0, which does nothing on screen**, so a test that reads the screen sees the text with NULs removed. Streams below are exact; "screen rows" have NULs removed.

### 6a. `*CAT`, empty 40-track disc, empty title
Stream: `<12 x NUL> " (00)" LF CR "Drive 0" <13 spaces> "Option 0 (off" NUL ")" LF CR "Dir. :0.$" <11 spaces> "Lib. :0.$" LF CR LF CR`

| Screen row | Text (exact) |
|---|---|
| 0 | ` (00)` (starts with one space) |
| 1 | `Drive 0             Option 0 (off)` |
| 2 | `Dir. :0.$           Lib. :0.$` |
| 3 | (blank) |
| 4 | cursor |

The title is always printed as 12 characters, then a space, then `(` + 2 BCD digits + `)`. A NUL-padded title (as in 5b) shows on screen as the letters then ` (00)`, e.g. `MYTITLE (00)`. A title set by `*TITLE` is space padded to 12, so row 0 is `MYDISC       (08)` (6 letters, 7 spaces). Cycle number rises by 1 per catalogue rewrite (a save, delete, `*TITLE`, `*OPT 4`, `*ACCESS` each count).

Option text: `Option 0 (off)`, `Option 1 (LOAD)`, `Option 2 (RUN)`, `Option 3 (EXEC)` (the 3-letter names carry a NUL after them). Drive number is `Drive 0` to `Drive 3` (`*CAT 2` printed `Drive 2`).

### 6b. `*CAT` with one file `TEST` in `$`
Rows 0-2 as above (with ` (01)`), then row 3 blank, then

| Screen row | Text |
|---|---|
| 4 | `    TEST       ` (4 spaces, `TEST`, then 3 pad + 4 spaces = 15 characters) |

Entry rule [TRACE, 6 listings]: each entry is 15 characters: `2 spaces + (dir "." or 2 spaces) + name padded to 7 + 2 spaces + ("L" or space) + 1 space`. Two entries per row, **second entry starts at column 20**. Files in the current directory come first, alphabetically, reading across; then a blank row; then files in other directories (sorted by directory character then name). Example, three files (`ABCDEFG`, `TEST` in `$`, `A.HELLO` locked): row 4 `    ABCDEFG             TEST       `, row 5 blank, row 6 `  A.HELLO    L `.

### 6c. `*INFO`
Format, as a regex on the screen row [TRACE, 6 rows checked]: `^(.)\.(.{7})  (L| ) {2}[0-9A-F]{6} [0-9A-F]{6} [0-9A-F]{6} [0-9A-F]{3}$` (directory, dot, name padded to 7, 2 spaces, lock flag, 2 spaces, load, exec, length, start sector). Examples:

| Command | Output |
|---|---|
| `*INFO TEST` | `$.TEST        002000 002000 000006 002` |
| `*INFO *.*` (one locked) | `A.HELLO    L  002000 002000 000006 004` |
| BASIC-saved (high bits) | `$.HELLO       FF1900 FF8023 000123 153` |

Load, exec, length are 6 hex digits, start sector 3 hex digits. Output order follows the catalogue (descending start sector).

### 6d. Error text. Every message below was seen as the BRK error block; error number is the byte after BRK [TRACE]

| Situation | Number | Text (exact, as printed) |
|---|---|---|
| `*NOSUCH`, or `*RUN` of a missing file | `$FE` | `Bad command` |
| `*LOAD`, `*INFO` or `*DELETE` of a missing file | `$D6` | `Not found` (**not** "File not found") |
| Sector not found, unformatted image (11 attempts) | `$C7` | `Disk fault 18 at 00/00` (spelled **Disk**, not Disc) |
| Data CRC / ID CRC / clock | `$C7` | `Disk fault 0E at 00/00`, `0C`, `08` |
| Late DMA, not ready result, track 0, write fault | `$C7` | `Drive fault 0A at 00/00`, `10`, `14`, `16` |
| Write-protected image, any write | `$C9` | `Disk read only` |
| Write to a locked file | `$C3` | `Locked` |
| File name over 7 characters | `$CC` | `Bad name` |
| Not enough free sectors (catalogue says 4 sectors, 2 KB saved) | `$C6` | `Disk full` |
| Empty drive, `*CAT` | none | **No message**: DFS polls ready forever; Escape gives `$11` `Escape` |
| 32nd file (31 already in the catalogue) | `$BE` | `Cat full` |

`tt/ss` is track and sector in hex: `03/00` for the spill case. G1 lists `&C5 Drive fault` but this ROM raises `$C7` for both texts [TRACE]. There is **no "Not ready" text in this ROM** [from ROM, searched every string]; and no "Disc fault" either, the spelling is "Disk fault".

---

## 7. Test-helper recipe (what to build from the above)

1. Image: 102 400 zero bytes (or 204 800 for 80 track); at offset `0x106` write `01 90` (or `03 20`). Optional title at offsets 0-7 and `0x100-0x103`.
2. Boot the machine with that image in drive 0. Wait for the `>` prompt. Run `*SAVE TEST 2000 2100 2000 2000` after poking RAM.
3. Check the image bytes against 5c, then reset (new machine, same image), `*LOAD TEST 4000` and compare 256 bytes; `*RUN` of a file holding `A9 41 20 EE FF 60` prints `A`.
4. Check the screen rows from 6a/6b with NULs ignored, and the 8271 chip test in section 3.
5. For a disc swap test, drop the head (idle 5 M cycles) or force one not-ready, or DFS will show the old catalogue (2b).

---

## 8. What I could NOT establish

| Item | State |
|---|---|
| Real 8271 internal timing: command-accept delay, when exactly busy drops, how long from last byte to INT | Not in D1. DFS passes with 0 to 60 000 cycles so it does not matter for DFS. Anything else is [guessing - verify] |
| Real BBC late-data limit | D1 says 31 us (8 inch). I inferred about one byte time (64 us) for mini-floppy. Unverified, never hit by DFS |
| 8271 clock frequency the BBC supplies | S2 5.5.1 does not say. D1 says mini-floppy timing needs a 2 MHz clock. [guessing] |
| Whether INT drops at the data read between bytes | Inferred: DFS cannot work otherwise. D1 only says an interrupt per byte |
| 1 MHz bus stretch on `$FE80` accesses | My CPU model has none. `bus.md` s2 puts the 8271 at 2 MHz [inferring], so handler timings stand, within a couple of cycles |
| Read ID, format, scan, 128-byte commands, deleted data | Datasheet gives opcodes and params only. DFS 1.20's built-in commands do not use them; `*FORM`/`*VERIFY` live on the utilities disc, which I do not have. No trace |
| Format-time contents of a fresh `*FORM`ed catalogue | B2 says "clear and set the size"; not checked against the utilities disc |
| Drive control **input** port (`$22`) bit layout | D1 prints no diagram; DFS 1.20 does not read it |
| Step doubling for 40-track discs in 80-track drives | No source found |
| `SHIFT+BREAK` / `!BOOT` | Not traced |
| Exact `*CAT` second-column layout at other screen widths | Mode 7 (40 columns) only |
| Behaviour on a real machine for a short or trimmed image | No real disc is short. My `Disk fault 18` is a model result, not hardware |
| How well my Python 6502 matches real silicon | It ran MOS 1.20 + BASIC + DFS (including decimal-mode BCD for the cycle number) without a wrong result I could see, but it has no flag-level test suite. My 8271 is written from D1 and proves only the DFS-visible subset |
| The one stale-catalogue rule | Found in my model: DFS re-reads on a not-ready reading. Real DFS may also re-read on a timer I have not seen; I only tested the two cases in 2b |
| Real-hardware proof of any `Disk fault` text | These come from the ROM text run in my machine, not a real BBC |
