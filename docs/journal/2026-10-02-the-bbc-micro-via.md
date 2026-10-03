---
title: "The BBC Micro's VIA, cycle by cycle"
date: 2026-10-02
summary: "The VIA, the BBC Micro's interface chip, is written on its own, one microsecond at a time, and tested against the fact sheet's worked examples without a processor or a bus. The hard part is the half cycle: a timer's flag rises in the middle of a cycle, and an acknowledge in that same cycle does not clear it, which real BBC Micros showed. Where the sources disagree the code follows the real machine, and where nobody measured anything the guess has a name."
order: 12
---

# 2 October 2026: the BBC Micro's VIA, cycle by cycle

Task 3 of the plan adds `Via6522`, the 6522 Versatile Interface Adapter, as a
chip on its own: `src/Dbhq.Machines.BbcMicro/Via6522.cs`, tested in
`tests/Dbhq.Machines.BbcMicro.Tests/Via6522Tests.cs` with no CPU and no bus.
Task 4 wraps it as the system and the user VIA. Everything in it comes from
the fact sheet, `docs/bbc-micro/facts/via.md` section 1, and the datasheets it
cites. No emulator's code was read.

## How it is driven

The VIA runs on the 1 MHz clock, so one `Tick()` is one microsecond, two CPU
cycles. `Tick()` is called at the start of a cycle and the CPU's access, if
there is one, happens after it in the same cycle. A read therefore sees what
the tick changed, which stands for the real chip sampling a read at the end of
the cycle. The tests count cycles the way the sheet does: W is the cycle of the
write that starts a timer, and a read "at W+k" is made after the k-th tick that
follows.

## The visibility rule

A timer loaded with N in cycle W holds N through W+1, counts down to 0 in
W+N+1, and shows `$FFFF` in W+N+2. That is when its flag rises: N+1.5 cycles
after the end of the write, in the middle of a cycle. A read in that cycle sees
the flag; a read one cycle earlier does not. Timer 1 then reloads from its
latch, so in free-run the flag comes every N+2 cycles. The operating system's
100 Hz tick, a latch of `$270E`, comes out at exactly 10000 cycles, and a test
runs three periods to show it.

The half cycle matters in one place. Stardot members ran test programs on real
Model Bs and Masters, and hoglet put a scope on one in October 2025. When the
CPU acknowledges timer 1 (by reading T1C-L, writing T1C-H or writing IFR) in
the very cycle the flag rises, the flag is not cleared, and the interrupt line
goes low half a cycle late, at the start of the next cycle. The flag's set
input is held for the whole cycle and wins.

The code does this with a mark. The tick that sets a flag marks it "just set";
a clearing access that hits a marked flag leaves it set and records the
collision; and `Irq` ignores a collided flag until the next tick drops both
marks. The sheet's example 9 is the real-machine result, and it passes.

**Decision: the rule covers every flag the clock sets.** The sheet gives it for
timer 1, and Rich Talbot-Watkins thought it probably applies to other
interrupts too. `Via6522` applies it to both timers and the shift register,
which set their flags in a tick, and not to CA1, CA2, CB1 and CB2. Those edges
arrive from outside between ticks, so there is no "same cycle" for them to
collide in. This was chosen over timer 1 only, which would have made timer 2
behave differently from timer 1 for no reason anyone has measured.

## What the stardot threads gave the chip

- **A T1L-H write clears IFR6 in one-shot mode.** The Rockwell sheet and the
  Advanced User Guide say it has no effect; the MOS and WDC sheets say it
  clears, and BigEd's real Model B, with a Rockwell R6522P, cleared it. The game
  Skirmish hangs if it does not. The code clears it (example 16).
- **PB7 reads as timer 1's output when ACR bit 7 is set, whatever DDRB bit 7
  says,** and the flip-flop behind it keeps toggling with ACR bit 7 clear. The
  example 4 test runs with PB7 as an output and again as an input.
- **Timer 2 does not reload.** It goes `$FFFF`, `$FFFE` and on, and flags once
  until T2C-H is written again (example 7, run past a whole wrap of the
  counter).
- **With ACR `$60`, as the OS sets the system VIA, timer 2 counts PB6 pulses,**
  and with PB6 held high it never moves (example 8).

Four measured quirks are not built: an ACR write in the same cycle as an
expiry, a latch write racing the reload, an ACR bit 5 change taking effect a
cycle late, and the timing of a T2C-H write in pulse-counting mode. None is on
the boot path. They are listed in `docs/known-differences.md`.

## The disagreement table

The sheet's table of source disagreements has two rows for this chip.

- **T1L-H write clears IFR6:** the real Model B, as above.
- **Timer 1's counter after a one-shot expiry:** the datasheets' text says it
  keeps counting down past `$FFFF`; their figure and Rich Talbot-Watkins say
  it reloads from the latch. The code reloads in both modes, choosing the
  figure and the real-machine report over the text. The OS never reads timer 1
  after a one-shot expiry, so nothing at boot depends on it. A test pins the
  choice and `docs/known-differences.md` says it is unresolved.

The CA1 and CA2 row (which carries vsync and which the keyboard) is about the
BBC's wiring, which is task 4. The chip names neither. Its tests use the OS's
PCR of `$04`, under which a negative edge on CA1 sets IFR1 and a positive edge
on CA2 sets IFR0.

## The shift register: one mode, and a guess

Only mode 010, shift in under the system clock, is built, because only the DFS
uses the shift register, and only in that mode. IFR2 rises `ShiftMode2Cycles`
cycles after the SR access that starts it, and the constant is 19. That number
was read with a ruler off the WDC datasheet's drawing of the CMOS part, and the
sheet marks it `[guessing - verify]`.

It does not need to be right to the cycle. Disassembling the committed DFS ROM
at `$963C` today, with the sheet's own throwaway disassembler, shows
`LDA #$04 / BIT $FE4D / BNE`, returning 5 when IFR2 is clear: the DFS tests
whether the flag has appeared and counts nothing itself. The plan cited
`disc.md` section 2 for this, but that section does not mention the shift
register; `via.md` section 1.8 and the ROM are the sources. The other seven
modes shift nothing and set no flag, and `docs/known-differences.md` says so.

**A correction to the sheet.** Its register table says an SR read starts a
shift in the input modes. For mode 010 the MOS datasheet says reading or
writing starts it, so the code starts it on either and the sheet now has a
note in section 1.8. The DFS starts it with a read, so the boot is the same
either way.

## Guesses with names

Where nothing was measured, the code says what it assumed, in a comment, and
`docs/known-differences.md` lists it: the counters, latches and shift register
start at zero; the PB7 output starts high; the four control lines start low;
the port inputs start at `$FF`, as nothing-attached inputs read on a real Model
B; reset leaves no one-shot armed; the CA2 and CB2 handshake and pulse outputs
follow the datasheets' words; and timer 2 counts a PB6 pulse when PB6 is low at
the start of a cycle after being high at the one before.

## Mistakes

- **The first free-run test saw a flag at W+1.** The chip was right and the
  test was wrong. A timer runs from power on, and with its counter at zero and
  ACR set to free-run during the setup, it expired every two cycles. One of
  those expiries landed in the very cycle of the T1C-H write, where, by the
  coincident acknowledge, the write could not clear it. The test setup now
  sends the counter far from zero and clears IFR before W. The OS tick test
  passed by luck before the same change, and now has it too.
- **The PB6 test read T2C-L before checking IFR5,** and reading T2C-L clears
  IFR5. It now reads IFR first.

## What the tests showed

On 2 October 2026 the chip's tests were mutated six times, one change at a
time, with `dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release` after
each: removing the hold cycle after a timer load, removing the coincident
acknowledge, stopping a T1L-H write from clearing IFR6, making timer 2 reload,
reading IER without bit 7 forced to 1, and starting the shift flag a cycle
early. Every one made at least one test fail.
