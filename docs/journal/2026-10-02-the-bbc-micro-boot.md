---
title: "The BBC Micro boots to the BASIC prompt"
date: 2026-10-02
summary: "The real operating system runs on the core for the first time and reaches BASIC's prompt with no video chip at all: in teletext mode the screen is character codes in memory, so the banner can be read straight out of RAM. It booted on the first run. The one difference from the expected screen was the ROM's own doing: with no disc controller fitted, the DFS stays silent."
order: 14
---

# 2 October 2026: the BBC Micro boots to the BASIC prompt

Task 5 of the plan puts the pieces from tasks 2 to 4 together as a machine and
runs MOS 1.20, BASIC and the DFS on it. Three files are new: `BbcMachine.cs` in
`src/Dbhq.Machines.BbcMicro/`, and `BbcSession.cs` and `BootTests.cs` in its
test project. `BbcBus.cs` gained stand-ins for the CRTC and the video ULA. The
sources are `bus.md` sections 1d, 4 and 5, `via.md` section 2.3 and `video.md`
sections 3.2 and 3.3. No emulator's code was read.

## The machine

`BbcMachine` builds the bus and an NMOS 6502 on it and hands the bus the CPU,
so the VIAs drive its IRQ line. `PowerOn()` resets both VIAs and then the CPU;
`PressBreak()` resets the user VIA and the CPU and leaves the system VIA alone,
which is how the OS tells the two apart. `Step()` returns the 2 MHz cycles the
bus counted, not the CPU's count of its own accesses, because a slow device
stretches a cycle and only the bus sees that. `Run(n)` runs whole instructions
until at least n cycles have passed.

**Why the boot can be checked before the video exists.** In mode 7 the OS
writes the screen as character codes at `$7C00`, one byte a character, forty
to a row. `BbcSession.ScreenRowAsMemory(row)` reads those forty bytes. The text
comes from the ROMs running on the CPU, so the test is not reading back
anything the test put there.

## The CRTC and the video ULA, for now

The OS writes the 6845 and the video ULA during the boot, so the bus needed
somewhere to put those writes. Each is a private class inside `BbcBus.cs` that
holds its registers and does nothing else; tasks 7 and 8 replace them with the
real chips. **Decision:** the CRTC stand-in reads back what the HD6845S reads
back, R12 to R17, with the high byte of each 14-bit address six bits wide, and
everything else reads as an absent slow device, `$00`. That was chosen over
reading nothing back, so a test can see that the OS programmed the screen start
for each mode, which it does: R12 is `$06`, `$08`, `$0B`, `$0C` and `$28` for
the five screen layouts, as `bus.md` section 3b works out from the ROM. The
video ULA's registers are write only, and a read of `$FE20` is Econet's INTON,
so it reads `$FE` like any absent fast device.

## It booted on the first run

The plan warned that this was where the real faults would show: a VIA register
the OS reads at reset, interrupt timing, reads of `$FE18`, the 100 Hz tick. None
did. The first run of the test reached BASIC's prompt and printed the banner,
and every VIA check the OS makes at reset passed. To be sure the boot had not
taken a wrong turn quietly, a scratch tracer outside the repository logged
every I/O access of the first three seconds and the OS's own records:

- `$028D`, the last reset type, was 1, a power on: the system VIA's IER read
  `$80` at `$D9D7`.
- `$028E`, the top of RAM, was `$80`, so the banner says 32K.
- `$0277` kept its default of `$FF`: the user VIA passed the OS's check that
  writes `$0E` to its PCR and reads it back (`$DA94-$DA9F`). A failed check
  would have incremented it.
- Every interrupt in those three seconds was timer 1 of the system VIA. Once
  they were running they arrived 20,000 CPU cycles (10 ms) apart, give or take
  the few cycles it takes the current instruction to finish, and the OS's clock
  at `$0292` counted them: the 100 Hz tick runs.
- `$FEC0`, the ADC, read `$00` on every tick, so the OS never started a
  conversion; `$FEE0` read `$FE`, so no Tube; `$FE40` read with bit 7 set, so
  no speech chip.
- `$FE18` was never read. The CRTC was written and never read.

A surprise of a different kind: the boot never waits for vertical sync. The
CRTC stand-in makes none, CA1 never moves, and the OS reaches the prompt
anyway.

## The finding: no disc controller, no DFS banner

The sheet's screen (`bus.md` section 4b, marked "inferring, not run") had
`Acorn DFS` on row 3, then `BASIC` on row 5 and `>` on row 7. The machine put
`BASIC` on row 3 and `>` on row 5, with nothing from the DFS.

The rows were not pasted in as the new expectation. The trace found the reason
in the DFS ROM: the paged ROM is `DFS,NET`, and every service call goes first to
the DFS half at `$B494`, which reads the 8271's status at `$FE80`, does
`AND #$03`, and returns at once if the answer is not zero. The 8271 is not
modelled until task 12, so `$FE80` reads as an absent fast device does, `$FE`,
whose low two bits are `10`. The DFS takes the controller as missing and serves
no call, so it never prints `Acorn DFS`. A fitted 8271 reads those two bits as
0. So the machine does what a real Model B with the DFS ROM and no controller
would do, and the sheet's screen is right for a machine that has one. The rest
of the screen was checked against the strings in the ROMs: `BBC Computer ` and
`32K` at OS `$C304`, BASIC's title at `$8009`, printed by the OS at `$DBF2`,
then two newlines (`$DBF7`, `$DBFA`), and the prompt from BASIC `$8B06`.

**Decision:** the test expects the screen the ROMs print with no 8271, and its
comment says why `Acorn DFS` is missing and when it comes back. This was chosen
over a status register that reads 0 at `$FE80`, which would have printed the
line by pretending a controller is there, and left the DFS free to send it
commands nothing would answer. The sheet's section 4b has a correction note
saying which rows have now been run, and `docs/known-differences.md` lists the
missing line with the other parts not yet built. A test in `BbcBusTests` pins
`$FE80` reading `$FE`.

## The other boots

**Soft BREAK.** After a cold boot, `PressBreak()` and three more seconds put
`BBC Computer` on row 1 with no memory size, then `BASIC` and `>`, and `$028D`
reads 0. That is what the ROM says: `$DB71` skips the size when `$028D` is 0,
and the language is re-entered at `$DBE6`, which prints its title.

**Modes 0 to 6.** With the start-up links set for each mode the machine boots
into it: the OS's current-mode byte holds the mode, something has been drawn in
that mode's screen memory, and the CRTC holds that mode's screen start. The
current-mode byte is `$0355`. `video.md` section 3.3 did not name it, so it was
found in the ROM, where the mode-set code does `AND #$07` and then `STX $0355`
at `$CB3D`, and a line saying so is now in the sheet.

## What the tests showed

With `dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release` on 2 October
2026, the new tests passed and so did the earlier ones. A boot test runs
millions of cycles and checks a handful of bytes, so to see that the tests can
fail at all, the code was then mutated five
times, one change at a time: resetting the system VIA at BREAK, dropping every
CRTC write, reading R12 back at eight bits, making RAM alias every 16 KB (a
Model A), and ignoring the start-up links. Each made at least one test fail.

## A mistake in the method

`BbcMachine` was written before the failing test was run, which is the wrong
order. It was moved out of the project and the test run without it, to see it
fail to build, and then put back.
