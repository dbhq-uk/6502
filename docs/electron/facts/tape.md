# Acorn Electron cassette interface, tape block format and UEF: fact sheet

Written 4 Oct 2026 for `dbhq-uk/6502` issue #51, before any code. Nothing here is code from Elkulator, elkjs, jsbeeb, B-em, MAME or any other emulator, and none was read for this sheet. It covers what the ULA sheet ([`ula.md`](ula.md) section 9) left to a second sheet: the UEF container, the Acorn tape block format, the CRC, and where the tape can sit in the model. It is shorter than the ULA sheet on purpose: what could not be sourced is listed in section 7 and settled by an experiment in the plan's tape task, not guessed here.

Tags follow the other sheets. `[from Sn ref]` was read directly. `[from ROM]` was read out of `os.rom`. `[from run]` was computed by a script I wrote and ran, and the script is quoted, or, in what section 7 settled on 5 Oct 2026, was seen by running the OS ROM against the tape probe, with the command that ran it. `[inferring ...]` joins dots. `[guessing - verify]` flags a correction.

## Source key

| Tag | Source | URL |
|---|---|---|
| S1 | Acorn Electron Advanced User Guide (Dickens and Holmes, 1984). The copy read is the archive.org text, section 11.5 "*ROM data format", whose block layout is "very similar to the tape data format", and section 14.1 on the ULA | https://archive.org/details/acorn-electron-advanced-user-guide (text: `.../download/acorn-electron-advanced-user-guide/Electron%20Advanced%20User%20Guide_djvu.txt`) |
| S2 | UEF (Unified Emulator Format) file format draft specification 0.10, document draft 28 (revised 10/1/2006), Thomas Harte, with additions by Fraser Ross and Greg Cook. Read from the Wayback copy, because the original host did not answer | https://web.archive.org/web/2016id_/http://electrem.emuunlim.com/UEFSpecs.htm |
| ROM | `os.rom`, Electron OS 1.00, file offset = CPU address - `$C000`. Disassembled with a throwaway script, not kept | local |
| ULA | [`ula.md`](ula.md), this repository | local |

**What was not reachable.** The BBC Micro Advanced User Guide's cassette chapter, the Electron Service Manual's cassette appendix and the Stardot threads were not read for this sheet. The block-header layout in S1 comes from the *ROM filing system chapter, which says it copies the tape layout; that is a second-hand source for the tape and it is flagged where it matters. Section 7 lists what that leaves open.

## Five facts that change how the tape must be built

1. **A UEF is a gzip file of chunks, and a plain Acorn program tape needs only four chunk kinds.** The container is `UEF File!` then two version bytes, then chunks of a 2-byte id and a 4-byte length (S2). The data chunk `&0100` carries bytes, with the start and stop bits implied, so a reader can work in bytes and never see a tone (S2).
2. **The block CRC is CRC-16 CCITT, polynomial `$1021`, initial value 0, stored high byte first, and it was checked two ways.** The CRC of the AUG's example data is `$5D65` by my computation and the AUG prints `&655D` for the value its `EQUW` stores, which is the same two bytes in the other order (section 3). The standard check value for the string `123456789` is `$31C3`, which this implementation gives (section 3).
3. **The OS's own CRC update is at `$F713` to `$F730`**, a shift-and-XOR loop with `EOR #$08` on the high byte and `EOR #$10` on the low byte, holding the CRC in `$BE` (low) and `$BF` (high), and a simulation of it agrees with the CCITT loop on 304 inputs (section 3). The proof on the real machine is the ROM itself: write a file with `SAVE`, and the block's CRC must match the one this sheet's code computes.
4. **The ULA gives the CPU a byte at a time at 1200 baud, and nothing in the first version needs the sound of the tape.** A bit is 832 microseconds, 1,664 cycles of 2 MHz, so a byte of ten bits is 16,640 cycles, one every 8.32 ms (ULA sheet section 9). The model can sit at the ULA's serial register (section 5), and a tape is then a list of byte and carrier events.
5. **Carrier is the idle line, and running the ROM settled it** (5 Oct 2026, section 7). The OS writes nothing to `$FE04` for the leader, between blocks or after the last block: it leaves the line idle in output mode with the motor on and counts fields, 255 of them (5.1 s) before the first block, 45 between blocks and 265 after the last. Then it sends a block's bytes back to back, each written inside the stop bit of the one before. No `$FF` bytes and no dummy byte.

---

## 1. The UEF container

[from S2 unless tagged]

| Item | Fact |
|---|---|
| Compression | A UEF is usually gzip compressed and keeps the suffix `.uef`. The gzip magic `1F 8B` is the first two bytes. A reader checks for it and inflates, or reads the file as it is |
| Header | 12 bytes: the string `UEF File!` with its terminating zero (10 bytes), then the **minor** version byte, then the **major** version byte |
| Chunk | 2-byte id, 4-byte length not counting those six bytes, then the data. **All numbers are little endian** |
| Order | Tape chunks are ordered and are processed from the start of the file in sequence |
| Defaults on opening | Base frequency 1200 Hz, the "1200 baud" data format, phase 180 degrees |
| Unknown chunks | The specification names many (inlay scans, ROM hints, discs, snapshots). A tape reader skips any chunk it does not use, by its length |

### 1a. The chunks an Electron program tape carries

| Id | Name | Layout | What a reader does | What a writer emits |
|---|---|---|---|---|
| `&0000` | Origin | Text, a few lines | Skip, or show | One chunk naming this tool |
| `&0005` | Target machine | 1 byte: high nibble `1` is an Electron, low nibble the keyboard hint | Skip, or use as a hint | `$10` (Electron, no keyboard preference) |
| `&0100` | Implicit start/stop bit data | Bytes; each is sent as a 0 start bit, eight data bits least significant first, a 1 stop bit | **The data.** Return the bytes | **The data**, in one or more chunks |
| `&0102` | Explicit tape data | First byte is the count of unused bits at the end, then raw bits, least significant bit of each byte first | Not used by Acorn program tapes; see section 6 | Never |
| `&0104` | Defined tape format | Three header bytes (bits per packet, parity `N` `E` `O`, stop bits) then data | Refuse the tape with a message, section 6 | Never |
| `&0110` | Carrier tone | 2 bytes: number of cycles at twice the base frequency | A gap in the data and a signal that high tone was heard | Before the first block, between blocks and after the last: the idle line (section 7 items 1 and 2) |
| `&0111` | Carrier tone with a dummy byte | 4 bytes: cycles before, then cycles after, with ten bits `0 0 1 0 1 0 1 0 1 1` between them | As above, with a dummy byte of `$AA`: the ten bits are the start bit, `0 1 0 1 0 1 0 1` least significant bit first, and the stop bit. (This sheet first said `$55`, which is the same pattern read the wrong way round; S2 says "always `&AA`" and task 11 checked the arithmetic, 5 Oct 2026) | Never: the OS writes no dummy byte (section 7 item 1) |
| `&0112` | Integer gap | 2 bytes: a count of half cycles at the base frequency. S2 prints the gap as `1 / (2n x base frequency)` seconds, which shrinks as n grows, so it cannot be a length; the reader takes `n / (2 x 1200)` seconds, 2,400 being one second (task 11, 5 Oct 2026) | Silence. Skip | Between files |
| `&0113` | Change of base frequency | One 4-byte IEEE float | Refuse if it is not 1200 | Never |
| `&0114` | Security cycles | Count, two flags, bit-packed cycles | Skip | Never |
| `&0115` | Phase change | 16-bit angle | Skip | Never |
| `&0116` | Floating point gap | One 4-byte float of seconds | Silence. Skip | May be used for a gap |
| `&0117` | Data encoding format change | 16-bit value, 300 or 1200 | **1200: carry on. 300: see section 6** | Never |
| `&0120` | Position marker | Text | Skip | Never |

[from S2]. `&0101` and `&0103` are a "multiplexed" copy of the data chunk before them, which a tape reader ignores [from S2].

The floating point format in `&0113` and `&0116` is IEEE 754 single, little endian [from S2].

### 1b. What S2 itself says about a simple reader

"Those looking to implement 80% compatibility with UEF files without a detailed emulation of the underlying tape hardware are recommended to implement chunks `&0100`, `&0110` and `&0111` and may otherwise assume 1200 baud." [from S2 "Simplified Usage"]. That is this design's reader. The tape the machine writes uses `&0100` and `&0110`.

---

## 2. The tape block format

Every Acorn cassette file is a series of blocks, each of at most 256 bytes of data. [from run, section 7 item 3: 513 bytes went to tape as blocks of 256, 256 and 1. S1's 255 is the *ROM format's number, not the tape's.]

### 2a. Layout [from S1 section 11.5, the *ROM format, which the text calls very similar to tape; tape-specific differences are in section 7]

| Field | Length | Notes |
|---|---|---|
| Synchronisation byte | 1 | `$2A` |
| File name | 1 to 10, then `$00` | The zero terminates it and is sent. Ten is the limit: eleven is refused with `Bad string` before the motor starts [from run, section 7 item 6] |
| Load address | 4 | Low byte first |
| Execution address | 4 | Low byte first |
| Block number | 2 | Low byte first. 0 for the first block |
| Block length | 2 | Bytes of data in this block, low byte first |
| Block flag | 1 | Bit 7: last block of the file. Bit 6: the block has no data. Bit 0: locked (the file may only be run) [from S1]. `SAVE` writes `$00`, and `$80` on the last block; a block with bit 0 set makes `LOAD` fail with `Locked` [from run, section 7 item 5] |
| Address of next file | 4 | Low byte first. In the *ROM format a pointer; on tape the OS writes 0 [from run, section 7 item 4], and a reader does not check it |
| **Header CRC** | 2 | **High byte first.** Over every byte from the first byte of the file name to the last byte of the next-file address, not the sync byte [from S1 "header CRC (1 to n + 16 incl.)", where byte 0 is the sync byte, so the span starts at the file name] |
| Data | block length | |
| **Data CRC** | 2 | **High byte first.** Over the data only. Not present when bit 6 of the flag says there is no data [inferring; S1 shows a title file with a zero-length block having no data CRC, `$C0` flag] |

### 2b. A one-block file named TEST holding `ABC` [from run]

Choices that are this sheet's own: load and execution address `$0900`, block number 0, length 3, flag `$80` (last block, has data, not locked), next address `$0903`. The OS itself writes 0 for the next address (section 7 item 4); the vector keeps `$0903` because its CRCs are computed with it, and either value is a valid block.

```
2A                      synchronisation
54 45 53 54 00          "TEST" and its terminator
00 09 00 00             load address $0900
00 09 00 00             execution address $0900
00 00                   block number 0
03 00                   block length 3
80                      block flag: last block
03 09 00 00             next address $0903
09 D8                   header CRC $09D8, high byte first
41 42 43                data "ABC"
39 94                   data CRC $3994, high byte first
```

All 30 bytes in one line: `2a5445535400000900000009000000000300800309000009d84142433994`. The two CRCs are from the script in section 3, applied to the 22 header bytes after the sync byte and to the three data bytes. A test may use this vector, but it should state that its expected bytes come from this sheet and the CRC rule, and not from the writer under test.

---

## 3. The CRC

CRC-16 CCITT: polynomial `$1021`, initial value 0, most significant bit first, no final XOR, stored high byte first. This is the algorithm usually called XMODEM.

```python
def crc(data, poly=0x1021, init=0):
    c = init
    for b in data:
        c ^= b << 8
        for _ in range(8):
            c = ((c << 1) ^ poly) & 0xFFFF if c & 0x8000 else (c << 1) & 0xFFFF
    return c
```

Three checks, none using the model [from run]:

| Input | Result | What it checks |
|---|---|---|
| The ASCII string `123456789` | `$31C3` | The published check value for CRC-16/XMODEM, so the algorithm and the parameters are the standard ones |
| `REM This is a very short text file.` then `$0D` (36 bytes) | `$5D65` | **S1 prints `&655D`** as the value its example's `EQUW` stores for this data. `EQUW` writes the low byte first, so `$655D` lays down `5D 65` in memory, which is `$5D65` high byte first. The AUG's number is the CRC byte-swapped so that the 6502 `EQUW` puts it down in the order the format wants |
| The block in section 2b | header `$09D8`, data `$3994` | A vector for the tests |

**The OS's own routine** [from ROM `$F713-$F730`]:

```
F713  ROR $CB       ; $CB holds the byte being added; the loop shifts it through the carry
F715  EOR $BF
F717  STA $BF       ; the high byte of the CRC takes the next bit
F719  LDA $BF
F71B  ROL A
F71C  BCC $F72A
F71E  ROR A
F71F  EOR #$08
F721  STA $BF
F723  LDA $BE
F725  EOR #$10
F727  STA $BE
F729  SEC
F72A  ROL $BE
F72C  ROL $BF
F72E  LSR $CB
F730  BNE $F719
```

The two constants `$08` and `$10` are the polynomial `$1021` seen through the one-bit shifts. The CRC is held in `$BE` (low) and `$BF` (high). `$CB` is a loop counter, not the byte: `SEC` then `ROR $CB` puts a marker bit in bit 7, and the `LSR $CB` / `BNE` at the end runs the loop exactly eight times. The byte being added arrives in `A` and is XORed into the high byte first (`EOR $BF` at `$F715`).

**Checked by simulation** [from run]. I wrote a small simulation of exactly these instructions, with `$CB` taken as zero on entry (not traced: the plan's tape task confirms it), and compared it with the Python `crc` above on 304 inputs: the three in the table, the 256 byte values in order, and 300 random strings of 1 to 299 bytes. **There were no mismatches**, and both give `$31C3` for `123456789`. So the OS's routine is CRC-16 CCITT as described, on the evidence of a model of it.

**Checked on real files** [from run, 5 Oct 2026]. The OS ROM, run headless against the tape probe of section 7, wrote four blocks: one for `SAVE "TEST"` of a one-line program, and three for a 513-byte `*SAVE`. For every one, the header CRC and the data CRC it wrote are what the `crc` above gives over the name to the next-file address and over the data. That is eight CRCs written by the OS's own routine above, and none disagrees. A fifth block, with a ten-character name, was checked the same way (scratch, not kept). The tests `SaveWritesOneBlockInTheSheetsLayoutAndItsCrcsAreTheSheets` and `BlocksHold256BytesAndOnlyTheLastIsFlagged` in `tests/Dbhq.Machines.Electron.Tests/TapeProbeTests.cs` hold it. `$CB` on entry was not traced: the agreement on real files is the evidence that the routine does what the simulation said.

---

## 4. The ULA's tape hardware, as the OS uses it

Cross-reference to [`ula.md`](ula.md) section 9, which holds the facts. The ones the model needs:

| Item | Fact | Source |
|---|---|---|
| Rate | 1200 baud, bit time 832 us, 1,664 cycles of 2 MHz. Byte of ten bits: 16,640 cycles | ULA 9 |
| Frame | Start bit 0, eight data bits least significant first, stop bit 1 | ULA 9 |
| Input | `$FE07` bits 2 and 1 = `00`, motor bit 6 set. Interrupts: high tone detect (bit 6) and receive full (bit 4) | ULA 6a, 9a |
| Output | `$FE07` bits 2 and 1 = `10`, motor on. Interrupt: transmit empty (bit 5) | ULA 6a, 9a |
| Receive register | `$FE04`. A new bit enters at bit 7 and moves towards bit 0. Reading it clears receive full | ULA 6a, 9 |
| High tone detect | Ten successive bits of high tone, 20 cycles of 2400 Hz, motor set, input mode, `$FE06` = 0. Cleared by `$FE05` bit 6 | ULA 9 |
| Receive full | Set as soon as bit 7 of a received byte is in the shift register: after nine bit times of the byte. The byte must be read within about 2 ms | ULA 6a, 9 |
| Transmit empty | Normally set. Cleared by writing `$FE04` and set again after bit 7 of that byte has gone out, before the stop bit. So the next byte can be written about 9 bit times after the last. **The OS does write it then**, 128 to 408 cycles after transmit-empty in the runs of section 7, always inside the stop bit; that byte starts on the line when the stop bit ends, and its own transmit-empty is nine bit times after that start, not after the write (section 7 item 2) | ULA 6a; run |
| What the OS does | Tape input from `$FACD`, tape output from `$FABF`. The receive handler is at `$F4DF`, reading `$FE04` and writing `$40` to `$FE05`, then the block decoder at `$F50A`. The transmit handler writes the next byte from `$BD` to `$FE04` | ULA 9a |
| Messages | `Searching`, `Loading`, `RECORD then RETURN`, `Data?`, `File?`, `Block?`, `Rewind tape`, and `Locked` from the load path. `File not found` is the ROM filing system's, not the tape's. Which one a visitor sees for which fault is section 7 item 7 | ULA 9a; ROM `$F59E`, `$F92A`, `$F8A9`, `$FA00`, `$FA0B`, `$FA16`, `$FA35`, `$F131`, `$F5BE-$F5CA` |

---

## 5. Where "tape as bytes" can sit

Three places, with what each costs.

| Level | What the tape is | What the model does | Fidelity cost | Verdict |
|---|---|---|---|---|
| **Tone** | A sound wave: 1200 Hz and 2400 Hz cycles with phase | A tone detector and a bit-clock recovery circuit, as the ULA has | None, but the high tone RC circuit's time constants are not sourced (ULA section 12, item 10) | Rejected. The detector cannot be written from what is known |
| **ULA bit** (recommended) | A list of events: `Carrier(cycles)` and `Byte(value)`, each with its place in time | The ULA's input side answers, at bit-time pacing, with high-tone-detect after ten bits of carrier and then receive-full for each byte, with the byte in `$FE04`. Its output side takes bytes written to `$FE04` at the same pace, and a run of carrier when the OS is sending leader, and hands them to a writer | Tone shape, phase, 300 baud as a different encoding, and a tape error that is a damaged waveform are not modelled. The timing of the interrupts, which the OS depends on, is | **Recommended** |
| **Byte** | A list of bytes | The OS's tape routines replaced or skipped, or the ULA answering instantly | Real timing is lost, so the OS's own timeouts and leader counters are not exercised, which is the part that proves the machine works | Rejected for the machine. It is what the **UEF reader and writer** do on their side of the boundary |

**Recommendation and reason.** Put the boundary at the ULA's serial register. The OS then runs its own tape code unchanged, against interrupts that arrive at the real rate, so a `SAVE` and `LOAD` through the machine prove the OS, the ULA's tape side and the reader and writer together. The reader and writer themselves work in bytes and carrier counts, as S2 recommends for a simple implementation.

**The output rule, found by running the OS** (section 7 items 1 and 2). The "ULA bit" boundary holds, and the likeliest mechanism was the right one: in output mode with the motor on, the OS makes carrier by writing nothing. So the output side records:

- a `Byte` for every write to `$FE04`. The byte starts on the line at the later of the write and the end of the byte before (ten bit times after that byte started). Transmit-empty is cleared by the write and set again nine bit times, 14,976 cycles, after the byte **starts**. The OS writes each next byte 128 to 408 cycles after transmit-empty, inside the stop bit of the one before (section 7 item 2, with the command), so under a rule that counted from the write it would outrun the line by more than 1,200 cycles a byte. **This rule rests on that latency staying below one bit time, 1,664 cycles**: a write after the stop bit had ended would start at once, and the two rules would agree;
- a `Carrier` for every stretch the line is idle: from the motor going on in output mode (or output mode being entered with the motor on) to the first byte's start, from one byte's end to the next byte's start when there is a gap, and from the last byte's end to the motor going off or output mode being left. Within a block there is no gap, so a writer emits carrier only before, between and after blocks;
- nothing else. The OS sends no `$FF` bytes and no dummy byte, so `&0111` is never written.

**Carrier units.** The probe, and these sections, measure carrier in 2 MHz CPU cycles. A UEF `&0110` chunk, and the cassette's `Carrier(n)`, count cycles of 2400 Hz, each 832 CPU cycles (half a bit time). So the writer divides by 832, and I recommend **rounding to the nearest whole cycle**: the error is then at most half a 2400 Hz cycle, 416 CPU cycles (0.2 ms), either way and never biased in one direction, where truncating would shorten every gap by up to a whole cycle. Nothing the OS does depends on that precision: it needs ten bit times of high tone before a sync byte, and its gaps are measured in fields of 40,000 cycles. For example, the first gap between blocks in section 7, 1,751,050 cycles, is 2,104.6 cycles of 2400 Hz and is written as 2,105.

**The round trip the design asks for.** Type a program, `SAVE` it: the ULA's output side collects `Byte` and `Carrier` events and the writer encodes them as `&0110` and `&0100` chunks. Reset. `LOAD`: the reader decodes the same chunks to events and the ULA's input side delivers them. Nothing in that loop is a recording made elsewhere.

---

## 6. What the first version does not load

The page and the reader say so rather than failing quietly.

| Tape | Why not | What a reader does |
|---|---|---|
| A UEF with `&0117` set to 300 | At 300 baud a bit is four cycles of 1200 Hz or eight of 2400 Hz, and the Electron's ULA is a fixed 1200 baud receiver. How the OS gets 300 baud out of it (a software repeat of each bit four times) is not read here | Refuse, naming the chunk and the value |
| A UEF with `&0104` or `&0102` carrying tape data | Non-Acorn framing, or a raw bit stream | Refuse, naming the chunk |
| A UEF with `&0113` not 1200 Hz | A different tone frequency | Refuse |
| A UEF with a major version above 0 | Its chunk meanings may differ [from S2: a major version bump means separate code paths] | Refuse |
| A gzip file that does not inflate, or a header without `UEF File!` | Not a UEF | Refuse with the reason |
| Disc, ROM and snapshot chunks | Not tape | Skip |

**Games.** Commercial tapes are the user's to supply: the page takes a `.uef` the visitor drops on it. Nothing is bundled. (`AGENTS.md` rule 3 and 4: games and commercial software are never committed.)

---

## 7. Open, and what running the OS settled

Items 1 to 7 and 9 were settled on 5 Oct 2026 by running OS 1.00 and BASIC headless against a test-only tape probe, [`TapeProbe.cs`](../../../tests/Dbhq.Machines.Electron.Tests/TapeProbe.cs), on a seam in the ULA (`ITapeTap`). The cassette itself, `UlaTape` (task 12), is built to the same rules, and the probe stays on the seam as an independent check: the same `SAVE` on both records the same tape (`TheCassetteRecordsWhatTheProbeRecorded`). Item 12 was found with the cassette. The probe logs every access to `$FE04` to `$FE07` with its cycle. In output mode it puts each byte written to `$FE04` on the line at the later of the write and the end of the byte before, sets transmit-empty nine bit times after that start, and records the idle stretches as carrier. In input mode it plays a tape of carrier and bytes at the pace of section 4, raising high-tone-detect after ten bit times of carrier and again every ten bit times while it lasts (the probe's choice: item 11). The SAVE was: boot, type `10 PRINT "HELLO"`, then `SAVE "TEST"`, and RETURN at `RECORD then RETURN`. The multi-block case was `*SAVE "ABCDEFGHIJ" 3000 3201` over a pattern in RAM. Each LOAD played back the tape the OS had written.

The tests that hold these findings are in [`TapeProbeTests.cs`](../../../tests/Dbhq.Machines.Electron.Tests/TapeProbeTests.cs), and the figures below are their printed output from

```
dotnet test tests/Dbhq.Machines.Electron.Tests --filter "FullyQualifiedName~Dbhq.Machines.Electron.Tests.Tape" --logger "console;verbosity=detailed"
```

run on 5 Oct 2026. They are a record of that day's run, not a claim about the code as it stands. A few details came from scratch logs of the same runs and are marked "(scratch, not kept)". A field is 39,936 or 40,064 cycles, 40,000 on average ([`ula.md`](ula.md) s5d), and the display-end handler decrements the OS's tape timer `$0240` once a field (ROM `$DBC0`), which is how the OS times everything below.

1. **The leader** [from run; from ROM]. **It is idle line, not bytes, and there is no dummy byte.** The first byte the OS writes for a block is its sync byte `$2A`. Before the first block it counts 255 fields: five waits for `$0240` to run from 50 past zero (`$F7F3`). Measured: **10,232,965 cycles, 5.12 s, from RETURN going down to the sync byte starting**; the few hundredths over 5.1 s are the OS's keyboard scan seeing RETURN. The OS turns the motor on in output mode *before* it prints `RECORD then RETURN` (`$F89B`, `$F8C9`, then OSBYTE `$89` at `$FB1F`), so a recording that starts at the motor also carries the wait for RETURN: 10,316,804 cycles from the motor going on in this run. **Between blocks: 1,751,050 and 1,751,020 cycles, 0.88 s**: 5 x 2 fields after a block (`$F7BF`) and 5 x (`$C7` + 1) before the next (`$F764`, `$F7F7`), with `$C7` = 6 (`$FA88`) when no gap has been set in `$03D1`: 45 fields. The measured gap is 43.8 fields of 40,000 cycles because the count starts when the OS writes a block's last byte, before that byte leaves the line, and each of the two waits starts partway through a field. The test's slack of two fields cannot tell 44 from 45, so **the exact count of 45 rests on the ROM reading**, not on the run. **After the last block: 10,565,510 cycles, 5.28 s**, from the last byte's end to the motor going off: the 10 fields of `$F7BF`, then 255 more (`$F7CE`). Chunk `&0111` is never needed.

2. **How carrier is made** [from run; from ROM]. **The line is left idle.** For the leader, between blocks and after the last block, the OS writes nothing to `$FE04`: it disables the tape interrupts (`$FAB0`, mask AND `$8F`) and counts fields. To send a block it puts `$2A` in `$BD` and enables transmit-empty (`$FABF`); transmit-empty is already set, so the handler at `$F4F0` writes the sync byte at once, and from then on writes each byte on transmit-empty. **No `$FF` bytes.** So carrier on a tape is what the ULA puts out with nothing to send, and it must be high tone, because the loader will not take a sync byte until it has heard high tone (`$F50E-$F515`) [inferring from the ROM; a run cannot show the waveform]. **A second finding, about pace:** the OS writes each next byte of a block 164 to 176 cycles after transmit-empty over the 42 writes of the one-block `SAVE`, and 128 to 408 cycles over the 609 writes of the three-block `*SAVE`. Those are the least and most of `next.Cycle - (before.Start + ReadyCycles)`, printed by `TheOsMakesCarrierByLeavingTheLineIdleAndSendsTheBlockBackToBack` and `EveryBlockIsSentBackToBack` when run with `dotnet test tests/Dbhq.Machines.Electron.Tests --filter "FullyQualifiedName~Dbhq.Machines.Electron.Tests.Tape" --logger "console;verbosity=detailed"` on 5 Oct 2026; both tests assert that every latency is at least 0 and below one bit time. The latency varies with what else the OS is doing when transmit-empty comes, such as an earlier interrupt still being served. Every write is therefore inside the stop bit of the byte before. A first version of the probe set transmit-empty nine bit times after the **write**, and under it the OS wrote a byte every 15,140 to 15,522 cycles (scratch, not kept; the largest means a latency of 546 cycles in that run, still below a bit time), faster than the 16,640 cycles a byte of ten bits takes to leave the line. So the rule is: **a byte written while another is on the line starts when that one's stop bit ends, and transmit-empty is set nine bit times after the byte starts.** With it the block's bytes go out back to back, 16,640 cycles apart. **The conclusion rests on the latency being below one bit time**: it is what puts each write inside a stop bit, and the tests fail if a write ever lands later. Section 5 states the output rule in full.

3. **The block limit** [from run]. **256 bytes.** 513 bytes went out as blocks of 256, 256 and 1 (at 255 a block they would have been 255, 255 and 3).

4. **The next-file address** [from run]. **0**, in all four blocks written, not the load address plus the length. The header has no other spare bytes. A reader ignores it.

5. **The locked flag** [from run; from ROM]. `SAVE` writes flag `$00` on every block but the last and `$80` on the last; it never sets bit 0. A tape whose block has bit 0 set, with its header CRC made good, makes `LOAD` print **`Locked`** (error `$D5`, `$F12D`) as soon as the header is read; the prompt returns and nothing is loaded. The same code (`$F124-$F13D`) has a branch that lets a locked file through and sets `$0258` to 3; which call takes it was read in the ROM, not run.

6. **The name limit** [from run]. **10 characters.** A ten-character name is written whole, then its zero. `SAVE "ABCDEFGHIJK"`, eleven, is refused with **`Bad string`** (the OS's, `$E870`) before the motor goes on and with nothing written; twelve the same (scratch, not kept).

7. **What `LOAD` prints at each failure** [from run]. The tape after each fault was the OS's own, broken in one place. The rows from the first `Searching` on are asserted whole by the tests in `TapeLoadTests`, the catalogue lines included, except where marked:

   | Fault | The screen shows | Then |
   |---|---|---|
   | A missing file (`LOAD "OTHER"`; the tape holds `TEST`) | `Searching`, then the catalogue line of each file it passes, here `TEST       00 0010` (name padded to eleven columns, block number, and on a last block the file's length, ROM `$F82E-$F86D`) | It searches on with the motor on, after the tape has ended too, with no message. **Escape** prints `Escape` and gives the prompt, motor off. `File not found` (`$F5CA`) is the ROM filing system's message, not the tape's (`$F5BE`) |
   | A bad header CRC | `Loading`, the file's line, then **`Data?`** | **`Rewind tape`**, then `Searching` again with the motor on. There is no `Header?` message in the ROM |
   | A bad data CRC | `Loading`, the file's line, then **`Data?`** | `Rewind tape`, then `Searching` again |
   | A block out of order (blocks 0, 2, 1) | `Loading`, the line of block 0, then the line of block 2, `ABCDEFGHIJ 02 0201`, then **`Block?`** | `Rewind tape`, then `Searching`; it then took block 1, the one it wanted, when it came (scratch, not kept) |
   | A locked file | **`Locked`** | The prompt; nothing loaded (item 5) |

   `File?` (`$FA0B`) is in the ROM and was not seen in these runs.

8. **How the OS sets 300 baud (`*TAPE 3`) on this machine.** Not needed for the first version. Still open.

9. **Timing a `SAVE` and a `LOAD`** [from run]. **`SAVE` of the one-line program: 21,513,995 cycles, 10.76 s**, from RETURN at `RECORD then RETURN` to the motor going off: 5.12 s of leader, 43 bytes of 16,640 cycles (0.36 s: a 25-byte header and 16 bytes of program with their CRC), and 5.28 s after. In general a `SAVE` takes about 5.1 s, plus 0.88 s between blocks, plus 8.32 ms a byte on tape, plus 5.3 s. **`LOAD` of the tape that SAVE wrote: 11,116,283 cycles, 5.56 s**, from RETURN to the motor going off. That is the tape's own leader as recorded and its 43 bytes: the motor goes off 848 cycles after the last byte ends, so a `LOAD` takes as long as the tape up to the end of the file's last block, and a tape with a shorter leader loads sooner.

10. **Tone-level facts** (the high tone detector's time constants, `CAS RC`, and how the ULA recovers the bit clock from 1200 and 2400 Hz) are outside this design and stay open in the ULA sheet.

11. **High-tone-detect during a long carrier** [guessing - verify]. Whether the ULA raises high-tone-detect again after the OS has cleared it, while the same carrier goes on, is not known. The probe raised it every ten bit times, and the OS loaded every tape. The cassette (`UlaTape`, task 12) does the same, so the question stays open and the model has chosen.

12. **A load that cannot finish** [from run, 5 Oct 2026, with the production cassette]. **The OS has no timeout of its own.** With the tape ejected after `Loading` appeared, it waited with the motor on and the screen unchanged for 30 s of machine time (scratch, not kept); a silent tape leaves `LOAD` at `Searching`, and `*CAT` on an empty tape prints nothing, both with the motor on, searching. In every case **Escape** prints `Escape` and gives the prompt with the motor off. **BREAK** while loading stops the motor 4,736 cycles after the BREAK in the run (scratch, not kept), with the CPU at `$D973`, the instruction after the write below: the ULA keeps its registers on BREAK in this model (ULA s12 item 5), and what stops the motor is the OS's reset code writing `$B4`, motor bit clear, to `$FE07` at `$D96B-$D970` (ULA s10a), which a soft BREAK reaches without the RAM clear. The tests that hold these are `TapeFaultTests` in [`TapeRoundTripTests.cs`](../../../tests/Dbhq.Machines.Electron.Tests/TapeRoundTripTests.cs).

**Also from the run.** The OS reads `$FE04` on a high-tone interrupt as well as on receive-full (`$F4FF`), 665 reads for a 43-byte file (scratch, not kept), so the cassette must answer a read of `$FE04` at any time and clear receive-full on it.
