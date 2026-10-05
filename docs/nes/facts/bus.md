# NES bus: memory map, open bus, registers, DMA, controllers

Written 5 October 2026 for the NES plan, task 1. Tags and page revisions are in
[`README.md`](README.md). Used by tasks 3 (`NesBus`), 7 (controllers), 8 and 9
(the APU's registers and DMC DMA) and 10 (the cartridge space).

## 1. CPU memory map

| Range | What | Source |
|---|---|---|
| `$0000-$07FF` | 2 KB internal RAM | [from CPU memory map] |
| `$0800-$1FFF` | Three mirrors of `$0000-$07FF` | [from CPU memory map; Mirroring] |
| `$2000-$2007` | PPU registers | [from CPU memory map] |
| `$2008-$3FFF` | Mirrors of `$2000-$2007`, every 8 bytes | [from CPU memory map; PPU registers: "a write to `$3456` is the same as a write to `$2006`"] |
| `$4000-$4017` | APU and I/O registers (section 4) | [from CPU memory map; 2A03] |
| `$4018-$401F` | APU test registers and an unfinished timer, disabled on a retail NES | [from CPU memory map; 2A03] |
| `$4020-$FFFF` | The cartridge: usually PRG RAM at `$6000-$7FFF` and PRG ROM and mapper registers at `$8000-$FFFF` | [from CPU memory map] |
| `$FFFA`, `$FFFC`, `$FFFE` | NMI, reset and IRQ/BRK vectors, supplied by the cartridge | [from CPU memory map] |

- The 2A03's own registers are fully decoded, so `$4000-$401F` has no mirrors
  [from 2A03].
- The cartridge sees every CPU read and write, wherever it is, except reads of
  `$4015`, which are internal to the CPU [from CPU memory map]. So a mapper may
  have writable registers anywhere [from CPU memory map].
- DMC sample fetches run from `$C000` up and wrap from `$FFFF` to `$8000` [from
  CPU memory map; APU DMC].

### Worked example 1: mirrors

| CPU address | Reaches |
|---|---|
| `$0173`, `$0973`, `$1173`, `$1973` | RAM byte `$0173` [from Mirroring] |
| `$2002`, `$200A`, `$3FFA` | PPUSTATUS [from PPU registers; the fork: ppu_vbl_nmi/readme.txt, test 1 checks `$200A` and every 8 bytes up to `$2FFA`] |
| `$3456` | PPUADDR (`$2006`) [from PPU registers] |
| `$4016` | Controller 1 read, or the strobe write; not mirrored [from 2A03] |

## 2. The order inside one CPU cycle

The bus advances the PPU and the APU inside each `Read` and `Write` (AGENTS.md
rule 1). What the wiki gives to settle where in the cycle the access falls:

- One alignment of CPU and PPU is documented and tested: "a read will see a
  change to a flag if and only if it starts at or after the PPU tick where the
  flag changes" [from PPU frame timing]. Other alignments may see a change one
  dot earlier or later [from PPU frame timing].
- Reading `$2002` one dot before the VBlank flag is set reads it clear and the
  flag is never set that frame, so no NMI. Reading on the dot it is set, or one
  dot after, reads it set, clears it, and the NMI does not happen. Two or more
  dots away, the read behaves normally [from PPU frame timing; NMI: "If 1 and 3
  happen simultaneously, PPUSTATUS bit 7 is read as false"].
- The CPU samples NMI and IRQ in the second half of each cycle (φ2) [from CPU
  interrupts].
- `ppu_vbl_nmi` tests 2, 3, 5, 6, 7 and 8 print what each dot offset should
  show, so the order can be checked to the dot [from the fork:
  ppu_vbl_nmi/readme.txt].

The order itself is the plan's to choose (spec, "Settled in the plan"). This
sheet only names the target: the alignment above, where a read sees the flag
from the dot it changes. Task 4 measured it against the `ppu_vbl_nmi` singles:
two dots before the access, the rest after, and the interrupt lines the CPU sees
in a cycle are those the chips held as it began. The table of what was tried is
in `timing.md` section 3.

## 3. Open bus

- **CPU.** A read from an address nothing drives returns the last value on the
  data bus [from Open bus behavior]. Usually that is the last byte of the
  instruction's operand: an absolute read returns the high byte of the address,
  an indexed read the high byte of the base address [from Open bus behavior].
- `$4015` reads do not drive the external bus, so a `$4015` read neither sets
  nor shows open bus except in bit 5, which is open bus from the last cycle that
  was not a `$4015` read [from APU].
- **Controllers.** `$4016` and `$4017` drive bits 4 to 0 only. Bits 7 to 5 are
  open bus, usually `010` from the high byte `$40` [from Open bus behavior;
  Standard controller]. Some games (Paperboy and other Mindscape titles) need a
  pressed button to read exactly `$41` [from Open bus behavior; Standard
  controller].
- **Nothing at `$4018-$7FFF`** and at write-only APU registers: open bus [from
  Open bus behavior: no circuit decodes reads of `$4000-$4014` or
  `$4018-$7FFF`; APU Pulse: the pulse registers are write-only].
- A DMC DMA read changes the last value read [from Open bus behavior].
- The PPU has its own I/O latch, a different thing (`ppu.md` section 1).

### Worked example 2: open bus

`LDA $5000` with nothing mapped there: the read cycles are `$AD`, `$00`, `$50`,
then the read of `$5000`, which returns `$50`, the high byte of the operand
[from Open bus behavior].

`LDA ($04),Y` with `$04/$05` = `$73FA`, Y = `$31`, nothing at `$7300-$74FF`:
cycle 5 reads `$732B` and gets `$73`, cycle 6 reads `$742B` and gets `$73`
[from Open bus behavior, its worked table].

`LDA $4016` with A held on controller 1 (the first read after a strobe):
`$41` [from Standard controller: "return exactly `$40` or `$41`"].

## 4. The register file at `$4000-$401F`

| Address | Write | Read | Source |
|---|---|---|---|
| `$4000-$4003` | Pulse 1 | open bus | [from 2A03; APU Pulse] |
| `$4004-$4007` | Pulse 2 | open bus | [from 2A03] |
| `$4008`, `$400A`, `$400B` | Triangle (`$4009` unused) | open bus | [from 2A03] |
| `$400C`, `$400E`, `$400F` | Noise (`$400D` unused) | open bus | [from 2A03] |
| `$4010-$4013` | DMC | open bus | [from 2A03] |
| `$4014` | OAM DMA page (section 5) | open bus | [from 2A03; PPU registers] |
| `$4015` | Channel enables | Channel and IRQ status (`apu.md` 7) | [from 2A03; APU] |
| `$4016` | Controller strobe (bits 2 to 0 latched, bit 0 to both pads) | Controller 1 data | [from 2A03; Controller reading] |
| `$4017` | APU frame counter (`apu.md` 8) | Controller 2 data | [from 2A03] |
| `$4018-$401A` | APU test registers, disabled | open bus | [from 2A03] |
| `$401C-$401F` | Unfinished IRQ timer, always disabled | open bus | [from 2A03] |

The "open bus" column is [inferring from Open bus behavior: no circuit drives a
read of `$4000-$4014` or `$4018-$401F`].

## 5. OAM DMA

- A write of page `N` to `$4014` copies 256 bytes, `$N00` to `$NFF`, to OAM
  through `$2004`, starting at the current OAMADDR [from PPU registers; PPU OAM;
  DMA].
- The CPU is halted using its RDY input, which works only on a read cycle; a
  halt that meets a write waits a cycle and tries again [from DMA]. OAM DMA tries
  to halt on the first cycle after the `$4014` write [from DMA].
- The CPU's cycles alternate between **get** (DMA may read) and **put** (DMA may
  write). They are the two halves of an APU cycle, and which CPU cycle parity is
  a get is random at power-on [from DMA].
- OAM DMA is one halt cycle, one alignment cycle if the next cycle is not a
  get, then 256 get/put pairs: **513 or 514 cycles**, not counting the `$4014`
  write [from DMA; PPU OAM; Cycle reference chart: "513 (+1 if starting on CPU
  get cycles)"].
- On the halted cycles the 2A03 repeats the read it was halted on; after DMA the
  CPU performs that read again [from DMA].
- `INC $4014` and other read-modify-write writes copy from the second page
  written [from DMA].
- Writes to `$2004` during rendering do not write OAM (`ppu.md` 4), and that
  includes OAM DMA [from PPU registers].

### Worked example 3: OAM DMA cycle counts

| `$4014` write lands on | Halt cycle | Alignment | Pairs | Total |
|---|---|---|---|---|
| a get cycle | put | none, the next is a get | 256 x 2 | 1 + 512 = 513 |
| a put cycle | get | one | 256 x 2 | 1 + 1 + 512 = 514 |

[from DMA, its two examples]. For the model, where the get/put parity of a CPU
cycle is a choice made at power-on, "the write on an odd cycle costs 514" is one
of the two consoles [inferring].

## 6. DMC DMA

- DMC DMA fetches one sample byte when playback is on, bytes remain, and the
  sample buffer is empty [from DMA; APU DMC].
- It halts the CPU, spends a dummy cycle, spends an alignment cycle if the next
  cycle is not a get, then reads: **3 or 4 cycles** [from DMA].
- The first fetch after a `$4015` write that enables the DMC (a "load") tries to
  halt on the get cycle in the second APU cycle after the write, the 3rd or 4th
  CPU cycle; it normally takes 3 cycles. Later fetches ("reloads") try to halt
  on a put cycle and normally take 4 [from DMA].
- A halt that meets a CPU write waits and retries; up to 3 writes in a row
  (an interrupt's pushes) [from DMA].
- During OAM DMA, a DMC fetch normally costs 2 cycles; at the very end of OAM
  DMA it can cost 1 or 3 [from DMA].
- Stopping a sample just before a reload can abort a DMA after one cycle, and on
  late 2A03G and 2A03H chips an extra fetch can happen [from DMA]. Whether the
  2A07 has these bugs is not known [from DMA].
- **Register conflicts (2A03).** On halted cycles the 2A03 repeats the halted
  read, so a DMC fetch during a read of `$2002`, `$2007`, `$4015` or a
  controller can read that register again [from DMA]. Controllers see one clock
  for each run of consecutive reads, so a conflict deletes a bit [from DMA;
  Controller reading]. The 2A07 does not have these extra reads [from DMA;
  Controller reading: "This glitch is fixed in the 2A07"; APU DMC: by halting on
  the first cycle of an instruction].
- The test ROM `dmc_dma_during_read4` checks these conflicts [from Emulator
  tests].

### Worked example 4: a reload fetch in the common case

| Cycle | Get or put | Bus |
|---|---|---|
| 1 | put | halted: CPU's read of A repeated (halt) |
| 2 | get | halted: A repeated (dummy) |
| 3 | put | halted: A repeated (alignment) |
| 4 | get | DMC reads the sample byte |
| 5 | put | CPU performs its read of A |

Four stolen cycles [from DMA, "Reload DMA"].

## 7. Controllers

- **Strobe.** Writing `$4016` latches bits 2 to 0; bit 0 drives the OUT line to
  both controller ports [from Controller reading]. While it is 1 the pads reload
  their shift registers from the buttons continuously, and a read returns button
  A; writing 0 stops the reload [from Standard controller].
- **Read.** `$4016` clocks controller 1 and `$4017` controller 2; each read
  returns one bit in D0 [from Controller reading]. The order is A, B, Select,
  Start, Up, Down, Left, Right [from Standard controller]. After 8 reads an
  official pad returns 1 [from Standard controller].
- **Pressed reads 1.** The 4021 holds a pressed button as 0 and the NES inverts
  it [from Standard controller].
- **The clock edge.** The pad's shift register advances when the read ends (the
  CLK line returns high), so the bit returned is the one before the shift [from
  Standard controller; Controller reading].
- **No pad connected:** D0 reads 0 [from Standard controller; Controller
  reading].
- **NES-001 lines:** D1 to D4 read 0 when nothing drives them, D5 to D7 are open
  bus [from Controller reading].
- The spec takes any 8-bit mask, including opposite directions together (spec,
  "Review Focus" 4). The pad itself has no interlock in its wiring [inferring
  from the Standard controller schematic: eight independent pull-ups and
  switches].

### Worked example 5: reading a pad

Buttons held: A and Right. Write `$01` then `$00` to `$4016`. Eight reads of
`$4016` with open bus `$40` return `$41 $40 $40 $40 $40 $40 $40 $41`, and a
ninth returns `$41` [from Standard controller: report order, 1 after 8 reads,
`$40`/`$41`].

With the strobe left at 1, every read returns the A bit, `$41` [from Standard
controller].

## 8. Interrupt lines

- **NMI** is edge-sensitive: the CPU sees a high-to-low edge between one cycle's
  φ2 and the next [from CPU interrupts]. On the NES only the PPU drives it, low
  while the VBlank flag and PPUCTRL bit 7 are both set (`ppu.md` 5) [from NMI].
- **IRQ** is level-sensitive and active low [from CPU interrupts]. On the NES it
  is driven by the APU frame counter, the DMC and the cartridge [from APU Frame
  Counter; APU DMC; MMC3]. Each holds the line until its own flag is cleared
  [from APU DMC: "continuously asserted"; APU Frame Counter], so the bus ORs
  them [inferring].
- Interrupts are polled at the end of the second-to-last cycle of an
  instruction; CLI, SEI and PLP change I after the poll; a taken branch has its
  own polling points; an NMI arriving in the first four cycles of BRK, IRQ or NMI
  takes the NMI vector [from CPU interrupts]. These are the core's, already
  tested in the core; the bus only has to present the lines at the right cycle.
- Enabling NMI (PPUCTRL bit 7, 0 to 1) while the VBlank flag is set causes an
  NMI at once, taken after the next instruction [from PPU registers; the fork:
  ppu_vbl_nmi/readme.txt, test 4 item 11].

## 9. Open items

1. The CPU cycle parity that is a get cycle at power-on, for the model
   (random on a real console) [from DMA]: a choice, recorded where made.
2. The 2A07's DMC DMA bugs [from DMA: not known].
