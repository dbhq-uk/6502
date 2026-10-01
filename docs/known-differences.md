# Known differences

Where the core knowingly differs from a reference it is tested against, or
where no reference we trust exists. Each entry says what, why, and how the
tests treat it. It was written with the plan, after the plan's code had been
run against every reference, and is kept true as the code lands.

## The 65C02's extra decimal cycle, in immediate mode

**What.** On the three 65C02 variants, `ADC #imm` (`$69`) and `SBC #imm`
(`$E9`) take one extra cycle when the decimal flag is set. Tom Harte's data
records that cycle as a read of a fixed address: `$007F`, `$0059` or `$0056`
for `ADC` on WDC, Rockwell and Synertek, and `$0000` for `SBC` on all three.

**Why we differ.** A fixed address that changes between chips and between
two sibling instructions looks like a property of the program that generated
the data, not of the chip. In every other addressing mode the same extra
cycle re-reads the operand's address, so the core does that here too: it
re-reads the immediate byte.

**How the tests treat it.** For those two opcodes, on those three variants,
with the decimal flag set, the comparison checks that the third cycle exists
and is a read, and does not compare its address or value. Everything else in
those cases is compared as normal.

## Synertek's bit-instruction opcodes, where two references disagree

**What.** On the Synertek 65C02, which has no `RMB`, `SMB`, `BBR` or `BBS`,
the opcodes in columns 7 and F are no-ops. Harte's data says the column 7
opcodes are two bytes long and read zero page, and the column F opcodes three
bytes, with an extra cycle in odd rows. Klaus Dormann's extended test, set up
to check them as no-ops, expects `$07` to be one byte long.

**Why we differ from Dormann.** Harte's data is the per-instruction
authority in this project, and the core matches all of it. Which of the two
is right about real Synertek silicon is not known here.

**How the tests treat it.** The Synertek build of Dormann's extended test is
assembled with `rkwl_wdc_op = 2`, which is the test's own setting for leaving
those opcodes out. Everything else in that test still runs.

## `WAI` and `STP`

**What.** WDC's `WAI` (`$CB`) and `STP` (`$DB`) have no data in Harte's WDC
set, because neither can be tested one instruction at a time.

**How the tests treat it.** Tests of our own check what they do: `WAI` waits
for an interrupt and then either takes it or carries on, depending on the
interrupt-disable flag, and `STP` stops until reset. The cycle counts follow
WDC's datasheet and are not checked against the chip. How many cycles `WAI`
takes to wake is not asserted, because no reference we trust gives it.

## 65C02 interrupt timing

**What.** The NMOS interrupt tests are checked against the Visual6502
transistor-level model, and the 65C02 uses the same timing rules in the core.
There is no public transistor-level model of the 65C02 to check that against.

**How the tests treat it.** The 65C02's own interrupt behaviour follows WDC's
datasheet for the decimal-flag clear, the `WAI` and `STP` cycle counts and the
wake rules. It is not checked against the chip. That the 65C02 times
interrupts like the NMOS chip is assumed. The tests check the decimal flag
and the pushed P, and the `WAI` and `STP` cycle counts, wake outcome and
stop-until-reset; they hold no bus logs of a 65C02 taking an interrupt.

## JAM

**What.** The NMOS `JAM` opcodes (for example `$02`) lock the chip up. Harte
records one step of one: the opcode fetch plus ten reads.

**How the tests treat it.** The core makes those eleven bus accesses and
matches Harte's data. What happens after that is not checked against any
reference: the core reads `$FFFF` on every `Step`, ignores IRQ and NMI, and
is cleared by `Reset`.

## Unstable NMOS opcodes

**What.** `ANE`, `LXA`, `SHA`, `SHX`, `SHY` and `TAS` give results on real
chips that vary between individual parts, and for `ANE` and `LXA` with
temperature.

**How the tests treat it.** The core matches Harte's data, which fixes the
`ANE` and `LXA` constant at `$EE`. That is one answer, not every
chip's.

## The KIM-1: reading the 6530 timer after it has passed zero

**What.** Appendix H of the KIM-1 User Manual says that once the timer has
counted past zero, reading either `$1706` or `$170E` will "disable the
interrupt option". The MCS6530 data sheet says address line A3 sets the
interrupt enable on every read or write of the timer, and `$170E` has A3 high.

**Why we differ from the manual.** The data sheet describes the chip; the
manual describes how the board's users should use it, and its own table one
paragraph earlier says reading `$170E` enables the interrupt. Which one real
silicon does after zero was not checked here.

**How the tests treat it.** `Rriot6530Tests` checks that a read with A3 high
enables the interrupt. Nothing on a stock KIM-1 connects the timer's
interrupt to the CPU, so the monitor never depends on it.

## The KIM-1: the data sheet's timer example, off by one

**What.** The data sheet's worked example writes 52 at divide by 8. Its text
says the interrupt comes at (52 x 8) + 1 = 417 clocks, and its two later
readings, `$E4` at 444 and `$AC` at 500, agree with 417. Its Figure 5 says the
interrupt occurs "at pulse 416".

**How the tests treat it.** The timer follows the text and the two readings:
the flag rises on clock N x k + 1 after the write. `TheDataSheetsWorkedExampleHolds`
checks 25 at clock 213, 0 at 415, `$FF` at 417, `$E4` at 444 and `$AC` at 500.

## The KIM-1: what is modelled rather than measured

**What.** Four behaviours of the board come from a reasoned model, not from a
reference that could be tested against:

- **Open bus.** A read where nothing answers ($0400-$13FF and $1400-$16FF)
  returns the last byte on the data bus. No measurement of a real board was
  found.
- **RAM at power on** is all zeros. Real static RAM starts with arbitrary
  contents.
- **The display.** A digit shows each segment that was on for more than half
  of the cycles it was selected, and goes dark 20 ms after it was last
  scanned. That stands in for the eye, not for the LEDs.
- **Single step.** The SST logic raises NMI on the cycle the CPU fetches an
  opcode outside `$1C00-$1FFF`. The machine treats the first access of every
  instruction, and of every interrupt sequence, as that fetch.

**How the tests treat it.** The acceptance tests depend on the display
model and on single step, and pass with the original monitor ROM doing what
the User Manual says it does. The open-bus value and power-on RAM are pinned
by tests of the memory map and the boot state, so a change to either is
seen.
