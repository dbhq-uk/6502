# Atari 2600 bus: the 6507, the decode and its mirrors, the floating bus and the WSYNC halt

Written 14 October 2026 for the Atari 2600 plan, task 1. Tags and sources are in [`README.md`](README.md). Used by tasks 1 (the reset vector check), 4 (`Atari2600Bus`), 5 (the order inside a cycle) and 6 (the collision reads).

## 1. The 6507

- The 6507 is a 6500-family processor in a 28-pin package with thirteen address lines, A0 to A12, so it reaches 8 KB [from SY6500, the SY6504 and SY6507 pin-out]. It has a RDY input and no IRQ or NMI pin [from SY6500, the family table and the SY6507 pin-out]. It has no SYNC pin either [same].
- It runs the 6502's instruction set [from SY6500, the family description: "software compatible"]. The core runs it as `Nmos6502` (`docs/the-6502-family.md`; spec, "The 6507 on the core").
- The core puts out sixteen address bits. The 6507 has no A13 to A15, so the bus keeps the low thirteen and drops the rest, and every address reaches the same place as the one `$2000` below or above it [inferring from the pin-out; 2k6specs, "CPU 8K address space mirrors": memory repeats every `$2000`].
- The 6507 is clocked by the TIA, which divides its colour clock by three and gives the result to the CPU's phase zero input [from SPG TIA 1A 2.D]. So one CPU cycle is three colour clocks [from SPG 3.2].
- With no interrupt pins, nothing can interrupt the 6507: the bus never raises IRQ or NMI [inferring from the pin-out]. BRK still works, as an instruction, through the vector at `$1FFE` [inferring: BRK is an instruction, not a pin].

### Worked example 1: the vectors

The core reads the reset vector at `$FFFC` and `$FFFD`. With A13 to A15 dropped these are `$1FFC` and `$1FFD`, the cartridge's last bytes but three [inferring from the above]. The suite's `harness/sanity_ntsc.a26` is 4096 bytes long; its bytes at offsets 4092 and 4093 are the reset vector, and the address they hold, with its top three bits dropped, lies in `$1000-$1FFF` (task 1's `SuiteManifestTests` checks this on the downloaded file) [inferring].

## 2. The decode

The console selects a chip with three address lines, A12, A7 and A9 [from 2k6specs, "Memory mirrors"; the suite: roms/cpu/stack-aliases-ram.asm, roms/cpu/stack-aliases-tia.asm, roms/cpu/open-bus.asm]:

| A12 | A7 | A9 | Chip | Lines the chip itself decodes | Source |
|---|---|---|---|---|---|
| 1 | any | any | The cartridge | A0 to A11 (4 KB) | [from 2k6specs, "Cartridge mirrors" and the slot's pin-out] |
| 0 | 0 | any | The TIA | A0 to A5 for a write, A0 to A3 for a read | [from 2k6specs, "TIA mirrors"; SPG TIA 1A 1; the suite: roms/cpu/partial-drive.asm] |
| 0 | 1 | 0 | The RIOT's RAM | A0 to A6 (128 bytes) | [from 2k6specs, "PIA RAM mirrors"; the suite: roms/cpu/stack-aliases-ram.asm] |
| 0 | 1 | 1 | The RIOT's ports and timer | the low address lines, as `riot.md` gives | [from 2k6specs, "PIA I/O mirrors"; `riot.md`] |

- **A8, A10 and A11 are decoded by nothing** when A12 is 0 [from 2k6specs, "Memory mirrors": the windows repeat at `$0100` and `$0400` steps; the suite: roms/cpu/stack-aliases-ram.asm, "A8 is a don't-care"]. So every chip below `$1000` repeats in every page whose A9 and A7 match [inferring].
- **There is no unmapped address**: every access selects the TIA, the RIOT or the cartridge [from the suite: roms/cpu/open-bus.asm]. A selected chip need not drive every data line (section 4).
- The RIOT's RAM and its ports are one chip, a 6532, whose RS pin chooses between them; the 2600 wires RS to A9 [from the spec's table, confirmed in `riot.md`].
- **The TIA's write registers** are `$00` to `$2C`; `$2D` to `$3F` select no register [from 2k6specs, "Overview" and the TIA write address summary; SPG "Write address summary"]. With A6 not decoded, each write register appears at `$xx00` and `$xx40` of its window [from 2k6specs, "TIA mirrors"].
- **The TIA's read registers** are `$0` to `$D` of the low four address lines; `$E` and `$F` select none [from the suite: roms/cpu/open-bus.asm, roms/cpu/floating-bus.asm; 2k6specs, "Overview"]. A4 to A6 are not decoded for a read, so each read register answers at every `$10` step from `$00` to `$7F` of its window [from the suite: roms/cpu/partial-drive.asm; 2k6specs, "TIA mirrors": "mirrored eight times in each window"].
- So one address can be a write register and a different read register: a read of `$02` (the WSYNC address) reads the collision register `CXP0FB` [inferring from the two tables in `tia.md`].

### Worked example 2: where each address goes

| Address the core puts out | 13 bits | Reaches | Why |
|---|---|---|---|
| `$F000`, `$1000`, `$3000` | `$1000` | Cartridge offset `$000` | A12 = 1 [from 2k6specs] |
| `$FFFC` | `$1FFC` | Cartridge offset `$FFC` (reset vector) | A12 = 1 |
| `$0080` and `$0180` | same | RAM byte 0 | A12 = 0, A7 = 1, A9 = 0; A8 ignored [from the suite: roms/cpu/stack-aliases-ram.asm] |
| `$01FF` (stack top) | `$01FF` | RAM byte 127, the same cell as `$00FF` | as above |
| `$0480`, `$0580`, `$0C80` | same | RAM byte 0 | A10 and A11 ignored [from 2k6specs, "PIA RAM mirrors"] |
| `$0102` (write) | `$0102` | TIA WSYNC | A12 = 0, A7 = 0; A8 ignored [from the suite: roms/cpu/stack-aliases-tia.asm] |
| `$0143` (write) | `$0143` | TIA RSYNC (`$03`) | A6 ignored for writes [from the suite: roms/cpu/stack-aliases-tia.asm, the stack wipe "strobes RSYNC as it passes `$0143`/`$0103`"] |
| `$011D` (write) | `$011D` | TIA ENAM0 | [from the suite: roms/cpu/stack-aliases-tia.asm] |
| `$0030` (read) | `$0030` | TIA CXM0P (`$0`) | reads decode A0 to A3 [from the suite: roms/cpu/partial-drive.asm] |
| `$003C` (read) | `$003C` | TIA INPT4 (`$C`) | as above |
| `$002E` (read) | `$002E` | the TIA, no register | low four bits `$E` [from the suite: roms/cpu/floating-bus.asm] |
| `$0280` | `$0280` | RIOT port A (SWCHA) | A12 = 0, A7 = 1, A9 = 1 [from SPG "Address summary table"] |
| `$0380` | `$0380` | RIOT port A again | A8 ignored [from 2k6specs, "PIA I/O mirrors"] |

### Worked example 3: the stack

The stack page is `$0100-$01FF`. With the stack pointer at `$FF`, `PHA` writes `$01FF`, which is RAM byte 127, the cell the program also reads at `$00FF`. With the stack pointer at `$7F` or below, a push writes a TIA register: `$011D` is ENAM0 [from the suite: roms/cpu/stack-aliases-ram.asm, roms/cpu/stack-aliases-tia.asm]. dasm's `CLEAN_START`, which every suite ROM runs first, clears the stack page from the top down and so writes every TIA register on the way, RSYNC twice among them; a model without the full TIA mirror, or with RSYNC wrong, shifts every later frame [from the suite: README.md, "A note on initialisation"].

## 3. The order of a cycle

- A write is taken by the TIA "when the phase two clock goes from high to low", at the end of the CPU cycle [from SPG TIA 1A 1]. A read is driven while phase two is high, the second half of the cycle [same].
- Which of a cycle's three colour clocks the access falls between is not stated in the SPG. `tia.md` section 4 gives what each source says (they put the write at the end of the cycle, so all three clocks before it), and task 5 fixes it by the rule in the plan ("Fixed before running" 8).

## 4. The data bus and what each read drives

- **The bus has no pull-up resistors.** After every access its eight lines keep, as charge, the last byte driven onto them [from the suite: roms/cpu/floating-bus.asm, roms/cpu/open-bus.asm]. The suite calls this the bus residue.
- **The residue is the byte of the cycle before the read**: the operand for a zero-page read (`LDA $nn`), and the address's high byte for an absolute read (`LDA $hhll`), because that is what the 6502 fetched on the cycle before [from the suite: roms/cpu/open-bus.asm, its cycle-by-cycle walk]. A model that returns the address's low byte instead agrees only for zero-page reads [same].
- **What each chip drives on a read:**

| Read | Lines driven | Source |
|---|---|---|
| The cartridge's ROM | all eight | [inferring: a ROM drives its whole byte] |
| The RIOT (RAM, ports, timer) | all eight | [inferring from `riot.md`: an eight-bit data bus] |
| A TIA collision register | D7 and D6, except CXBLPF, which drives D7 (`tia.md` section 2.2) | [from the suite: roms/cpu/open-bus.asm, roms/cpu/partial-drive.asm; SPG 2.0: "data bits 6 or 7"] |
| A TIA input port, INPT0 to INPT5 | D7 only | [from the suite: roms/cpu/open-bus.asm, roms/cpu/partial-drive.asm] |
| A TIA read address with low bits `$E` or `$F` | none | [from the suite: roms/cpu/open-bus.asm, roms/cpu/floating-bus.asm] |

- Every line a chip does not drive returns the residue [from the suite: roms/cpu/open-bus.asm]. CXBLPF, whose D6 is unused, drives only D7 (`tia.md` section 2.2).
- **This corrects the spec**, which says "a TIA read drives only data bits 7 and 6": an input port drives D7 alone, and `$xE` and `$xF` drive nothing [from the suite: roms/cpu/partial-drive.asm, roms/cpu/open-bus.asm].
- A write puts the CPU's byte on the bus, so the residue after a write is the byte written [inferring: the CPU drives the bus on a write; SPG TIA 1A 1].

### Worked example 4: the floating bus

| Instruction | Bytes | Cycles on the bus | Returns | Source |
|---|---|---|---|---|
| `LDA $2E` | `A5 2E` | fetch `$A5`, fetch `$2E`, read `$002E` (TIA, no register) | `$2E` | [from the suite: roms/cpu/floating-bus.asm] |
| `LDA $013E` | `AD 3E 01` | fetch `$AD`, fetch `$3E`, fetch `$01`, read `$013E` (TIA, no register) | `$01` | [from the suite: roms/cpu/open-bus.asm] |
| `LDA $70` with no collision latched | `A5 70` | ..., read CXM0P | `$30`: D7 and D6 driven 0, the low six bits from `$70` | [from the suite: roms/cpu/open-bus.asm, check `$05`] |
| `LDA $0970`, the same register | `AD 70 09` | ..., read CXM0P | `$09` | [from the suite: roms/cpu/open-bus.asm, check `$06`] |
| `LDA $3C`, fire button up, no latch | `A5 3C` | ..., read INPT4 | `$BC`: D7 is 1, the low seven bits from `$3C` | [from the suite: roms/cpu/partial-drive.asm, check `$03`] |
| `LDA $18` with VBLANK D7 set (the paddles dumped) | `A5 18` | ..., read INPT0 | `$18`: D7 is 0 | [from the suite: roms/cpu/partial-drive.asm, check `$02`] |

## 5. WSYNC and RDY

- **The rule of the 6502's RDY input:** RDY low halts the processor on any cycle except a write, with the address lines holding the address it was reading; the halt lasts as long as RDY stays low. "If Ready is low during a write cycle, it is ignored until the following read operation" [from MOS 6500, "Ready (RDY)"].
- **WSYNC:** a write to WSYNC sets a latch in the TIA that pulls RDY low; the latch is cleared at the leading edge of the next horizontal blank, which releases RDY [from SPG TIA 1A 2.D; SPG 3.2]. The data written does not matter [from SPG 2.0: a strobe]. The release is at colour clock 0 of the next line, the start of its horizontal blank (`tia.md` section 5).
- So a `STA WSYNC` (three cycles, the last a write) completes, and the CPU halts on the next cycle, the opcode fetch of the next instruction, which waits for the line's start [inferring from the two rules; the suite: roms/cpu/rmw-wsync.asm].
- **A read-modify-write of WSYNC** (`INC WSYNC`, `DEC WSYNC`) reads, writes the old value back, then writes the new value on the next cycle. The first write sets the latch; the second is a write, so RDY does not stop it, and it strobes WSYNC again on the same line. The halt falls on the next read, the following opcode fetch. The cost is one line, as for a plain `STA WSYNC` [from the suite: roms/cpu/rmw-wsync.asm; MOS 6500, "Ready (RDY)"].
- **The RIOT's timer keeps counting during the halt**: it has no RDY pin and counts one a CPU cycle [from the suite: roms/cpu/rmw-wsync.asm]. So each halted cycle is a full cycle for the RIOT and three colour clocks for the TIA [inferring].
- **The model** (spec, "The bus, the halt and the order inside a cycle"): the halted cycles are reads of the address the CPU holds, each one a bus access through the one cycle method, as the NES's DMA halt is (AGENTS.md rule 1). The data sheet says the address lines hold "the current address being fetched" during the halt [from MOS 6500, "Ready (RDY)"], so a halted read reaches the chip at that address on every cycle [inferring]. For a halt after `STA WSYNC` that is the next opcode's address, in the cartridge; a board that switches banks on a read of that address would see each cycle [inferring; the next instruction is not normally on a hotspot].

### Worked example 5: one line of WSYNC

`STA WSYNC` (`85 02`) with its write on CPU cycle c: cycles c-2 and c-1 fetch `$85` and `$02`, cycle c writes `$0002`. Cycle c+1 is the read of the next opcode; RDY is low, so it is repeated with the same address until the TIA releases RDY at the start of the next line, then completes. The suite's `cpu/rmw-wsync` times one `STA WSYNC` and two, and requires the difference to be 76 CPU cycles, one line [from the suite: roms/cpu/rmw-wsync.asm, check `$01`], and `INC WSYNC` (`EE 02 00`, six cycles: three fetches, the read of `$0002`, two writes) to cost one line, not two [same, check `$02`].

## 6. Read-modify-write on strobes and hotspots

- A read-modify-write instruction puts its target address on the bus three times: a read, a write of the unchanged byte, then a write of the new byte on the next cycle [from the suite: roms/cpu/rmw-strobe.asm]. Both writes reach a strobe: `DEC RESM0` resets missile 0 twice, three colour clocks apart, and the missile ends where the second write puts it [same].
- The core already makes these dummy accesses, cycle for cycle, as Harte's tests check bus cycle by bus cycle; the bus has only to count each as one cycle (AGENTS.md rule 1). A board that switches banks sees each of them (`cartridge.md`).

## 7. The cartridge slot

- The slot has 24 pins: A0 to A12, D0 to D7, power, ground and one pin a cartridge ties to ground. It has no read/write line and no clock [from 2k6specs, "Cartridge slot (24 pins)"]. So a board cannot tell a read from a write and sees both alike (`cartridge.md`) [inferring].
- A12 is the cartridge's chip select [from 2k6specs, "Cartridge slot (24 pins)"]. A 2K image appears twice in the 4K window [from 2k6specs, "Cartridge mirrors"].
- A board's hotspots need not be gated by A12, so on some boards a hotspot at `$1FF8` also answers at `$0FF8`, in the RIOT's I/O mirror [from 2k6specs, "Cartridge mirrors"]; `cartridge.md` says what the suite's `bank-hotspot-window` test expects.

## 8. Open items

1. The colour clock within a cycle at which a TIA write takes effect: `tia.md` section 4 and task 5.
2. Whether a WSYNC halt's repeated reads are seen by the cartridge as accesses, as the model makes them: [inferring from MOS 6500, "Ready (RDY)"]; no 2600 source read confirms it, and no game is known to depend on it [guessing - verify].
3. The power-on value of the bus residue: not documented; the model starts it at `$00` and `docs/known-differences.md` says so [guessing - verify].
