# Acorn Electron cassette interface, tape block format and UEF: fact sheet

Written 4 Oct 2026 for `dbhq-uk/6502` issue #51, before any code. Nothing here is code from Elkulator, elkjs, jsbeeb, B-em, MAME or any other emulator, and none was read for this sheet. It covers what the ULA sheet ([`ula.md`](ula.md) section 9) left to a second sheet: the UEF container, the Acorn tape block format, the CRC, and where the tape can sit in the model. It is shorter than the ULA sheet on purpose: what could not be sourced is listed in section 7 and settled by an experiment in the plan's tape task, not guessed here.

Tags follow the other sheets. `[from Sn ref]` was read directly. `[from ROM]` was read out of `os.rom`. `[from run]` was computed by a script I wrote and ran, and the script is quoted. `[inferring ...]` joins dots. `[guessing - verify]` flags a correction.

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
5. **Two things the plan must find out by running the ROM, because no source read here settles them:** the exact sequence of bytes and gaps the OS writes for the leader, and how it asks the ULA for carrier tone. Section 7 says how.

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
| `&0110` | Carrier tone | 2 bytes: number of cycles at twice the base frequency | A gap in the data and a signal that high tone was heard | Before the first block and between blocks |
| `&0111` | Carrier tone with a dummy byte | 4 bytes: cycles before, then cycles after, with ten bits `0 0 1 0 1 0 1 0 1 1` between them | As above, with a `$55`-pattern dummy byte | Only if the OS is found to write one (section 7) |
| `&0112` | Integer gap | 2 bytes: a count of half cycles at the base frequency (the gap is `1 / (2 x n x base frequency)` seconds) | Silence. Skip | Between files |
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

Every Acorn cassette file is a series of blocks, each of at most 256 bytes of data. [inferring; S1 states the *ROM format "is very similar to the tape data format" and has "blocks of up to 255 bytes". The BBC and Electron tape blocks are 256 bytes in the sources I know, and 255 is S1's number for the *ROM format; the plan settles the tape's limit by running the ROM, section 7.]

### 2a. Layout [from S1 section 11.5, the *ROM format, which the text calls very similar to tape; tape-specific differences are in section 7]

| Field | Length | Notes |
|---|---|---|
| Synchronisation byte | 1 | `$2A` |
| File name | 1 to 10, then `$00` | The zero terminates it and is sent |
| Load address | 4 | Low byte first |
| Execution address | 4 | Low byte first |
| Block number | 2 | Low byte first. 0 for the first block |
| Block length | 2 | Bytes of data in this block, low byte first |
| Block flag | 1 | Bit 7: last block of the file. Bit 6: the block has no data. Bit 0: locked (the file may only be run) [from S1] |
| Address of next file | 4 | Low byte first. In the *ROM format a pointer; on tape, what the OS writes here is not checked by a reader (section 7) |
| **Header CRC** | 2 | **High byte first.** Over every byte from the first byte of the file name to the last byte of the next-file address, not the sync byte [from S1 "header CRC (1 to n + 16 incl.)", where byte 0 is the sync byte, so the span starts at the file name] |
| Data | block length | |
| **Data CRC** | 2 | **High byte first.** Over the data only. Not present when bit 6 of the flag says there is no data [inferring; S1 shows a title file with a zero-length block having no data CRC, `$C0` flag] |

### 2b. A one-block file named TEST holding `ABC` [from run]

Choices that are this sheet's own: load and execution address `$0900`, block number 0, length 3, flag `$80` (last block, has data, not locked), next address `$0903`.

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

**Checked by simulation** [from run]. I wrote a small simulation of exactly these instructions, with `$CB` taken as zero on entry (not traced: the plan's tape task confirms it), and compared it with the Python `crc` above on 304 inputs: the three in the table, the 256 byte values in order, and 300 random strings of 1 to 299 bytes. **There were no mismatches**, and both give `$31C3` for `123456789`. So the OS's routine is CRC-16 CCITT as described, on the evidence of a model of it. The plan's tape task repeats the proof on the real thing: it `SAVE`s a file on the machine, reads the block back off the tape, and compares the CRC the OS wrote with this sheet's.

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
| Transmit empty | Normally set. Cleared by writing `$FE04` and set again after bit 7 of that byte has gone out, before the stop bit. So the next byte can be written about 9 bit times after the last | ULA 6a |
| What the OS does | Tape input from `$FACD`, tape output from `$FABF`. The receive handler is at `$F4DF`, reading `$FE04` and writing `$40` to `$FE05`, then the block decoder at `$F50A`. The transmit handler writes the next byte from `$BD` to `$FE04` | ULA 9a |
| Messages | `Searching`, `Loading`, `RECORD then RETURN`, `File not found`, `Block?`, `Rewind tape` | ULA 9a |

---

## 5. Where "tape as bytes" can sit

Three places, with what each costs.

| Level | What the tape is | What the model does | Fidelity cost | Verdict |
|---|---|---|---|---|
| **Tone** | A sound wave: 1200 Hz and 2400 Hz cycles with phase | A tone detector and a bit-clock recovery circuit, as the ULA has | None, but the high tone RC circuit's time constants are not sourced (ULA section 12, item 10) | Rejected. The detector cannot be written from what is known |
| **ULA bit** (recommended) | A list of events: `Carrier(cycles)` and `Byte(value)`, each with its place in time | The ULA's input side answers, at bit-time pacing, with high-tone-detect after ten bits of carrier and then receive-full for each byte, with the byte in `$FE04`. Its output side takes bytes written to `$FE04` at the same pace, and a run of carrier when the OS is sending leader, and hands them to a writer | Tone shape, phase, 300 baud as a different encoding, and a tape error that is a damaged waveform are not modelled. The timing of the interrupts, which the OS depends on, is | **Recommended** |
| **Byte** | A list of bytes | The OS's tape routines replaced or skipped, or the ULA answering instantly | Real timing is lost, so the OS's own timeouts and leader counters are not exercised, which is the part that proves the machine works | Rejected for the machine. It is what the **UEF reader and writer** do on their side of the boundary |

**Recommendation and reason.** Put the boundary at the ULA's serial register. The OS then runs its own tape code unchanged, against interrupts that arrive at the real rate, so a `SAVE` and `LOAD` through the machine prove the OS, the ULA's tape side and the reader and writer together. The reader and writer themselves work in bytes and carrier counts, as S2 recommends for a simple implementation.

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

## 7. Open

All of these are `[guessing - verify]`, and the plan's first tape task settles them by running the OS ROM headless with an instrumented ULA that logs every write and read of `$FE04`, `$FE05`, `$FE06` and `$FE07` with the cycle it happened at, during a `SAVE` and a `LOAD`. The log is the source, and its findings are written into this sheet before the reader or writer is built.

1. **The leader the OS writes.** How long it sends carrier before the first block and between blocks, and whether there is a dummy byte (chunk `&0111`). Acorn tapes are said to carry about five seconds of carrier before the first block and a shorter one between blocks, and I did not find that in a source read here.
2. **How the OS makes carrier.** The ULA has no carrier-tone command that this sheet found. Carrier on tape is a stream of 1 bits at 2400 Hz, and the likeliest mechanism is the OS writing `$FF` bytes through the same register, with the stop bit and start bit between them, but a run of `$FF` bytes would carry a start bit every ten bits. Whether the OS does that, or switches the ULA's output to a continuous high, is for the log to show.
3. **The block limit.** 256 bytes of data a block on tape, or 255 as S1 says for the *ROM format.
4. **What the OS writes in the next-file address field on tape**, and in the spare bytes. A reader must not depend on it. The sheet's example uses load address plus length, which is a guess.
5. **The flag bit for a locked file** and what the OS does with the flag on `LOAD`, taken from S1's *ROM description.
6. **The block header's name field limit.** S1 says 1 to 10 characters. The OS may accept more and truncate.
7. **What `LOAD` prints and does at each failure**: a bad header CRC, a bad data CRC (`Data?`), a missing file, a block out of order. The page should say what a visitor will see, and the OS's own messages in section 4 are only the ones found by string search.
8. **How the OS sets 300 baud (`*TAPE 3`) on this machine.** Not needed for the first version.
9. **Timing a `SAVE` and a `LOAD` of a small program in seconds**, so the page can say how long they take. It is measured in the plan, not estimated: the machine's own cycle counter over the run, at 2 MHz.
10. **Tone-level facts** (the high tone detector's time constants, `CAS RC`, and how the ULA recovers the bit clock from 1200 and 2400 Hz) are outside this design and stay open in the ULA sheet.
