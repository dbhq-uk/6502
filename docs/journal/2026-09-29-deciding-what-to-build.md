# 29 September 2026: deciding what to build

## Where it started

The idea was a verified port of *Mega Man 2*, the 1988 NES game: a
reimplementation checked against the original by differential testing. The
original's own instructions run on an emulator, the port runs beside it from
the same starting bytes, and every byte of memory and video is compared on
every pass. A difference is a number, not an argument.

That makes the emulator the reference, so it has to be trusted before
anything built on it can be. An NES is a 6502-family CPU with a picture chip,
a sound unit and a cartridge. So the work starts at the bottom: a 6502 core,
then machines on it, then the NES, then the port.

## The decisions, in the order they were made

**The NES emulator is a product in its own right**, not only scaffolding for
the port. That raised the bar from "good enough for one game" to the
community accuracy test ROMs.

**Built in public from the first commit.** The repository was created public,
the design and plan live in it rather than in a private repository, and the
journey is written up at 6502.dbhq.uk.

**The BBC Micro before the NES**, chosen over going straight to the NES. A
simpler machine proves that the core is really shared before the hardest
machine depends on it.

**Then the KIM-1 before the BBC Micro.** The BBC Micro needs six chips
working together before it shows a character, so the first sign of life
would have been weeks away. The KIM-1, MOS Technology's own 1976 board, has
one new chip and a keypad and LED display, so the core can be seen running
real software within days. It is also the natural first chapter: the machine
the 6502's makers built to show it off.

**The 65C02 goes into the core now.** A Commodore 64 between the BBC Micro
and the NES was considered and dropped in favour of the CMOS 65C02: three
variants (Synertek, Rockwell, WDC) beside the NMOS 6502 and the NES's 2A03.
It opens the BBC Master, the later Apple IIs and every modern 6502 machine,
and it can be proven the same way as the original chip.

**Every bus access is one cycle.** The 6502 does one read or one write every
clock cycle. The core is written as plain instruction code in which each read
or write advances the rest of the machine by one cycle. Two alternatives were
rejected: an explicit per-cycle state machine (no more accurate, three times
the code) and a transistor-level model (exact but thousands of times too
slow). The transistor-level model survives as a referee for interrupt timing.

**How the core is proven:** Tom Harte's SingleStepTests, Klaus Dormann's
functional tests, interrupt timing tests checked against the transistor-level
model, and the `nestest` trace.

***Mega Man 2* is named in public as the goal,** with a line saying the
project is not affiliated with Capcom. Silence until the port started was
chosen first and reversed the same hour. Nothing of Capcom's goes in the
repository or on the site.

## Also written that day

- [`docs/superpowers/specs/2026-09-29-6502-design.md`](../superpowers/specs/2026-09-29-6502-design.md),
  the design.
- [`docs/the-6502-family.md`](../the-6502-family.md): every chip and machine
  built on a 6502 or a descendant, and which of them the core can run.
  Checking its sources corrected two things said from memory while planning:
  Motorola's 6800 was not launched after the 6502 (its $175 was simply the
  price at the time), and the SYM-1 and AIM-65 are dated differently by
  different sources, so the document says 1978 is the best supported year
  rather than stating it as fact.
