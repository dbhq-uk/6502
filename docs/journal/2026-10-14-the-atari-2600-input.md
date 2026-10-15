---
title: "The Atari VCS's joysticks, fire buttons and console switches"
date: 2026-10-14
summary: "The ninth task of the Atari VCS: two joysticks on the RIOT's first port, their fire buttons on the TIA with the latch the vertical blank register turns on, the paddle ports with nothing plugged in, and the console switches on the second port, every one active low and any combination taken as written. Before it, a mismatch the HMOVE task left unexplained was traced to one missile width, where the suite says the console's missile and ball differ and every document read gives them one circuit."
order: 63
---

# 14 October 2026: the Atari 2600's joysticks, fire buttons and console switches

Task 9 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): the input, built from [`riot.md`](../atari-2600/facts/riot.md) section 10 and [`tia.md`](../atari-2600/facts/tia.md) section 20. New: `Controllers` (with the `Joystick` and `ConsoleSwitches` flags), and in the tests `ControllersTests` and `TiaInputTests`. Changed: `Tia` (INPT4 and INPT5, their latch, INPT0 to INPT3), `Atari2600` (`SetJoystick`, `SetSwitches`, and `JoystickState` and `Switches` to read them back), and comments in `Riot6532` and `SuiteSelfTests`.

Before it, as its own commit, task 7's review finding 1 was triaged: `hmove-stuck-widths`'s mismatched pixels are one width-2 missile, where the suite's header says the ball alone loses a pixel and the model gives the missile the ball's width circuit, as every source read does. No mechanism was found, so it is recorded and not fitted. The triage, with what was tried, is in [the task 7 entry](2026-10-14-the-atari-2600-hmove.md); `tia.md` open item 19 and `docs/known-differences.md` carry it.

## What was built

- **The bit maps.** Port A: the left stick on D7 to D4 (right, left, down, up), the right stick on D3 to D0 in the same order, each 0 while held. Port B: D0 Reset and D1 Select, 0 while pressed; D3 colour, 0 at black and white; D6 the left difficulty and D7 the right, 1 at A; D2, D4 and D5 not connected, 1 through the RIOT's pull-ups. The fire buttons: the left on INPT4's D7 and the right on INPT5's, 0 while pressed. The `Joystick` flags put up, down, left and right in bits 0 to 3, which is the order a stick's nibble has on the port, so the map is the nibble inverted and shifted.
- **The latch.** VBLANK D6 clear removes both latches and resets them to 1, so the ports read the pins. D6 set brings them in; a pin that goes low clears its latch, which stays 0 after the button is let go until D6 is cleared, and a button already held when D6 is set clears it at once. The latches move only on a pin change or a VBLANK write, both between the CPU's bus accesses, so they are worked out there and not every colour clock, and `Tick` is unchanged.
- **The paddle ports**, INPT0 to INPT3, read 0 with the dump on or off: with joysticks in both ports nothing charges their capacitors (`tia.md` section 20.3).
- **`Controllers`** keeps what was written and puts it on the chips at once; it refuses a stick other than 0 or 1 and a bit that names no input, and changes nothing when it does. Power on leaves it as it is.
- **The WebAssembly host** builds unchanged; task 13 gives it the input.

## Decisions

- **A port A output at 1 reads 0 while its stick holds the pin low**, `riot.md` section 12 item 4, which the plan left to this task. Kept as task 3 built it, over reading the output register as port B does: the data sheet gives port A an output-high current of about 100 µA, which cannot hold up a pin a switch grounds, and it says port B, not port A, reads its register for an output. No suite test drives an output against a held stick, so it stays `[guessing - verify]`.
- **Any combination is taken as written**, up and down together and every input at once included, and read back exactly by `JoystickState` and `Switches`. Chosen over filtering what a real stick cannot do, as the spec's "Input" section asks; the page will not filter either (task 13).
- **A bit outside the five a stick or the switches have is refused**, chosen over masking it, so what is read back is always what was written.
- **The inputs hold through power on.** The console's Reset is a switch the program reads, not a reset line (`riot.md` section 2), so a switch held through a power cycle is still held.
- **The latches power on disabled and at 1**, the choice `tia.md` open item 13 already recorded; the latch's power-on state is not documented.
- **`JoystickState` and `Switches`** were added beside the plan's two setters, so the machine itself can report what it was given (the plan's Review Focus 4). The plan is corrected.

## The suite

`cpu/partial-drive`, the one suite test `suite.md` gives this task, was already a trusted row, passing since task 6 on the inputs at rest; it still passes in both regions. No suite program reads SWCHB or presses a button (searched for `SWCHA`, `SWCHB`, `INPT4` and `INPT5` under `roms/`), and every screenshot count is the same as task 7's after the machine's port B moved from all ones to the console at rest (`dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter ScreenshotTests --logger "console;verbosity=detailed"`, 14 October 2026).

## The tests, test first

`TiaInputTests` was written from `tia.md` section 20: the worked example of section 20.2 step by step, the right button on INPT5 and not INPT4, both latches enabled and disabled together, disabling resetting a latch for the next enable (added after the fault pass, below), each latch holding its own press, one VBLANK write setting D6 with the blank bits, D6 written again keeping a press, the four paddle ports at 0 under every dump and button state, the power-on choice, and D7 the only line driven. `ControllersTests` was written from `riot.md` section 10 and the plan's Review Focus 4: each direction of each stick on its bit and back, each fire button on its INPT and not on port A, section 20.2's worked example through a real program (`LDA $0C` reads `$8C` and `$0C`), the latch through the bus, each switch on its bit and back, every row of worked example 10, opposite directions together, every input at once, the inputs through power on, the refusals, the port A decision, a port B output, and the left stick's right as a PA7 edge.

On 14 October 2026, before `Controllers` existed, `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~ControllersTests|FullyQualifiedName~TiaInputTests"` failed to compile, on the missing `Joystick` and `ConsoleSwitches`. With the code, the same command passed all their cases on the first run; the fault pass below shows each file able to fail. Then `dotnet build -c Release` of the solution gave no warnings, and `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests` passed every case.

## Every test file shown able to fail

The fault pass ran on 14 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/input-faults.py <scratch>`), one fault at a time, building and running the whole Atari 2600 test project. The figure in brackets is the number of failing test cases in each file.

| Fault | Planted | Test files that failed |
|---|---|---|
| I1 | the fire latch ignoring VBLANK D6 | `ControllersTests` (1), `TiaInputTests` (5) |
| I2 | disabling the latch does not reset it | none at first; after a test was added, `TiaInputTests` (1) |
| I3 | enabling with a button held does not clear the latch | `TiaInputTests` (1) |
| I4 | the latch catches presses while disabled | `TiaInputTests` (1) |
| I5 | INPT4 and INPT5 swapped | `ControllersTests` (5), `TiaInputTests` (6) |
| I6 | the fire buttons active high | `Atari2600BusTests` (2), `ControllersTests` (14), `SuiteSelfTests` (2, `cpu/partial-drive`) |
| I7 | the paddle ports read 1 with the dump off | `Atari2600BusTests` (5), `TiaInputTests` (16) |
| I8 | the two sticks on each other's nibble | `ControllersTests` (15) |
| I9 | up and down swapped on the port | `ControllersTests` (8) |
| I10 | the difficulty switches swapped | `ControllersTests` (4) |
| I11 | the colour switch active high | `ControllersTests` (15) |
| I12 | the unconnected port B pins read 0 | `ControllersTests` (14) |
| I13 | opposite directions filtered out | `ControllersTests` (2) |
| I14 | a bit that names no input taken | `ControllersTests` (1) |
| I15 | the stick check lets -1 through | `ControllersTests` (1) |
| I16 | power on releases the fire buttons | `ControllersTests` (1), `TiaInputTests` (1) |
| I17 | a port A output at 1 holds its pin high against a closed switch | `ControllersTests` (1), `Riot6532Tests` (1) |
| I18 | the switches not applied until the first change | `ControllersTests` (6) |
| S1 | the TIA does not report its fire latches | `StateReportTests` (9) |
| S2 | the TIA does not report its fire pins | `StateReportTests` (9) |

In all, 20 faults were planted and 19 were caught on the first run. **I2 survived it**: with the latch disabled the port reads the pin, so a latch left at 0 by a missing reset never showed; every test that disabled the latch either read it disabled or enabled it again with the button held, which clears it anyway. `TiaInputTests.Section_20_2_disabling_resets_the_latch_so_the_next_enable_starts_at_1` now latches a press, disables and enables again with the button up, and expects 1. Run again (`... input-faults.py <scratch> I2`), it caught I2. None survives.
