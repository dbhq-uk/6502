---
title: "The BBC Micro's two VIAs, wired, and its keyboard"
date: 2026-10-02
summary: "The VIA chip becomes the BBC Micro's system and user VIAs: the keyboard and the sound chip on one port, an eight-bit latch on the other, vsync and the keyboard on the control lines, and both interrupt outputs on the processor's one IRQ line. The keyboard's tests read every key the way the operating system does, and its start-up links give the screen mode. Where sources disagree about which line is which, the code follows the ROM."
order: 13
---

# 2 October 2026: the BBC Micro's two VIAs, wired, and its keyboard

Task 4 of the plan takes `Via6522`, the chip on its own from task 3, and wires
it into the Model B. Four files are new in `src/Dbhq.Machines.BbcMicro/`:
`SystemVia.cs`, `UserVia.cs`, `BbcKey.cs` and `BbcKeyboard.cs`. `BbcBus` now
owns the two VIAs and the keyboard, ticks the VIAs, decodes them in SHEILA and
drives the processor's IRQ line. The sources are `docs/bbc-micro/facts/via.md`
sections 2 and 3 and `bus.md` sections 1c and 5. No emulator's code was read.

## What the bus does now

- **The VIAs tick on the 1 MHz clock,** which is every other CPU cycle: on an
  even cycle count. A stretched access to a slow device always ends on an even
  count, so every VIA access has its VIA's tick before it in the same 1 MHz
  cycle, which is what the chip expects.
- **Each VIA is sixteen registers at `$FE40` and `$FE60`, repeated in the
  upper half of its block,** because address line A4 is not decoded.
- **Both VIAs' IRQ outputs share the 6502's IRQ line.** The bus sets the line
  at the end of every `Read` and `Write`, after the access. Task 3 left a note
  saying why that order matters: a VIA's IRQ can rise in the tick that starts a
  cycle and fall at an acknowledge in the same cycle, so reading it between the
  tick and the access would show the CPU an interrupt that never was.
- **Two resets.** `PowerOnReset()` resets both VIAs; `BreakReset()` resets the
  user VIA only. The system VIA has its own reset circuit that fires at power
  on and nowhere else, and the OS reads the system VIA's IER at reset to tell a
  cold start from BREAK. The latch IC32 is reset by neither: its clear pin is
  tied high.

Two tests left over from task 2's review are in too. One checks that the chips
tick before the access of their cycle: a bus that records the ROM slot and the
system VIA's IER at every tick shows that the ticks of a write to `$FE30`, and
the three ticks of a write to `$FE4E` from an odd count, all saw the old value.
The other times `ROL $FE48` from an odd count: 3, 2 and 2 cycles for the read
and the two writes.

## The system VIA

**Port B drives the latch.** PB0 to PB2 address one of its eight bits and PB3
is the value. Bit 0 is the sound chip's write enable, so a fall of bit 0 raises
`SoundWrite` with the byte on port A for task 11. Bit 3 enables the keyboard.
Bits 4 and 5 are the screen start adder, exposed as `ScreenStartLatch` for the
video tasks. A test writes each bit low and high and checks that no other bit
moves.

**Port A is the keyboard.** A counter on the keyboard board picks the column.
With latch bit 3 low the counter loads PA0 to PA3 on every 1 MHz clock and PA7
reads the key at that column and the row on PA4 to PA6. With bit 3 high the
counter runs free over sixteen states, a microsecond each, and CA2 goes high
whenever the column it has reached holds a key in rows 1 to 7. So a held key
raises the CA2 interrupt once a sweep until the OS disables it, which is what
the OS's keyboard code expects.

**Decision: model the counter rather than the sheet's shortcut.** The sheet
suggests raising CA2 at once, and again after each clear, while any key is
down. A counter is as short to write, follows the schematic, and needs no rule
about when to re-raise. It was chosen for that.

**Decision: the machine-specific parts subclass the chip.** `SystemVia` and
`UserVia` derive from `Via6522`. Two small changes to task 3's chip were needed:
`Tick()` became virtual, so the system VIA can move the keyboard counter in the
same tick as the timers, and `Peek()` was added, a read with no side effects,
because a debugger's look at `$FE44` must not acknowledge timer 1. `Read()` is
now `Peek()` plus the side effects. This was chosen over wrapping the chip in a
class that forwards all sixteen registers, which would have doubled the surface
for nothing.

## The OS's sequences, run through the bus

The tests do not poke the keyboard directly. They drive the system VIA with the
OS's own instruction sequences, read from the ROM:

- **The link read at reset.** Decoding `os.rom` at `$DA03` by hand today
  confirmed the sheet: `LDX #$0F / STX $FE42` sets DDRB, a loop writes `$0E`
  down to `$08` to ORB, then for X from 9 down to 1 it calls the key test,
  does `CPX #$80` and `ROR $FC`. Nine rotates through an eight-bit byte leave
  column 9's link in the carry and CTRL in bit 7, and the `ROL $FC` after the
  loop swaps them back: the links end in bits 7 to 0 and CTRL in the carry. The
  start-up byte is `$FC EOR $FF`. The test runs that arithmetic on what the
  bus returns and gets `$FF` for mode 7 and `$F8` for mode 0.
- **The key test at `$F02A`** for all 72 keys: ORB = 3, DDRA = `$7F`, write the
  key number to `$FE4F`, read it back. Bit 7 is set exactly when the key is
  down.
- **The column test of the full scan at `$F0E6`:** column 15, clear IFR bit 0,
  select the column, test IFR bit 0. For every key, only its own column answers,
  and only if it is in rows 1 to 7.
- **The interrupt:** IER `$81`, as the OS writes once no key is down, and a key
  press reaches the CPU's IRQ within one sweep.

The key table in the test is typed from the sheet's list, column and row as two
numbers, and is not derived from `BbcKey`'s values. `BbcKey`'s values are the
OS's internal key numbers, column plus sixteen times the row.

## The start-up links

The links are row 0, columns 2 to 9, and bit n of the start-up byte is column
9 - n; a made link reads as a pressed key and the OS stores it as 0.
`BbcKeyboard(mode)` makes the links for the mode in bits 0 to 2 and leaves the
other five open, so the byte is `$F8` plus the mode. **Decision:** the other
five bits stay at the sheet's default (SHIFT-BREAK boots the disc, the slowest
disc timings, DFS rather than NFS) instead of becoming options. Nothing in the
plan needs them, and `BbcOptions` can grow them when something does.

## The disagreement table

The sheet's table of source disagreements has four rows for this task:

- **CA1 and CA2.** 8bs.com says CA1 is the keyboard and CA2 vsync. The
  Advanced User Guide, the Hardware Guide and the ROM say the opposite: the IRQ
  handler tests IFR bit 1 and runs the vsync code, and only the keyboard code
  arms IER bit 0. The code follows the ROM: `VsyncInput` drives CA1 and the
  keyboard drives CA2.
- **Keyboard rows and columns.** The Service Manual calls the decoder's outputs
  rows and the selector's inputs columns. The code uses the OS's names, which
  the Advanced User Guide shares: the column is PA0 to PA3, 0 to 9, and the row
  is PA4 to PA6, 0 to 7.
- **The screen start adder bits.** The Advanced User Guide's table swaps the
  pairs for modes 0 to 2 and 4 and 5; the ROM writes the other way round. The
  code exposes the two bits as the ROM writes them, and the mapping to an
  address is the video tasks' to make, from the ROM's pairs.
- **The speech lines on PB6 and PB7.** Sources disagree on which is ready and
  which is the interrupt. With no speech chip it does not matter: both read 1,
  and PB7 reading 1 is what tells the OS no speech chip is fitted.

## Guesses with names

`docs/known-differences.md` lists them: the latch is strobed on ORB and DDRB
writes rather than on every write to the chip, which the sheet shows is the
same while PB0 to PB3 are outputs; the latch starts at `$00`; PA7 reads 0 with
the keyboard disabled; PB6 reads 1; and the user VIA's CB1 and CB2 idle high.
The user VIA's CA1, the printer's acknowledge, is pulled up on the board, so it
idles high, and a test shows that a fall on it is then an edge.

## What the tests showed

Every new test passed on its first run, which proves nothing about whether they
can fail. So on 2 October 2026 the code was mutated ten times, one change at
a time, with `dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release` after
each: wiring row 0 into CA2, dropping the IRQ update after a write, ticking the
VIAs every CPU cycle, resetting the system VIA at BREAK, reading the key with
the keyboard disabled, ticking after the access instead of before, freezing the
autoscan counter, raising `SoundWrite` while the strobe is held low, mapping the
links to the wrong columns, and leaving the user VIA's CA1 low. The last one
survived at first, because nothing drove the line; the test now pulls it low as
a printer would. After that every mutation made at least one test fail.
