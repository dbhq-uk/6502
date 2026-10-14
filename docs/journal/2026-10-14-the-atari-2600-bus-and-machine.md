---
title: "The Atari VCS's bus, its machine and the WSYNC halt"
date: 2026-10-14
summary: "The fourth task of the Atari VCS: the bus that keeps the processor's thirteen address lines, decodes the TIA, the RIOT and the cartridge with every mirror, keeps the floating data bus and halts the processor on WSYNC, a whole cycle at a time; the machine on the core's NMOS processor unchanged; the hooks the speed pass will need; and the first run of the suite's own programs, through the real bus."
order: 57
---

# 14 October 2026: the Atari 2600's bus, its machine and the WSYNC halt

Task 4 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): `Atari2600Bus`, `Atari2600`, `Atari2600Options`, `StateReport.cs` (the state reports and the observer) and a first `Tia`, built from [`bus.md`](../atari-2600/facts/bus.md) and the parts of [`tia.md`](../atari-2600/facts/tia.md) the bus needs. The suite's programs run for the first time here, headless, through the real bus.

## What was built

- **`Atari2600Bus`** keeps A0 to A12 and drops A13 to A15, so the core puts out sixteen bits and knows nothing of the 6507 (rule 2). It decodes by three lines, as `bus.md` section 2 gives them: A12 the cartridge; else A7 low the TIA; else A9 the RIOT's RAM or its I/O. No other line is looked at, so every mirror the sheet lists comes from the decode itself, the TIA at `$0100-$017F` and the RAM at `$0180-$01FF` among them. `Decode` is one function, used by the read, the write and `Peek`.
- **One place runs a cycle, `Cycle`.** It counts the cycle, runs the TIA's three colour clocks, ticks the RIOT, makes the access and keeps the byte on the data bus. `Cycles++` happens nowhere else (rule 1). All three colour clocks run before the access: the split the sources state (`tia.md` section 4), which task 5 tests against the others by the plan's rule. The RIOT is ticked before the access, as task 3's `Riot6532` asks.
- **The WSYNC halt.** A write to WSYNC sets the TIA's latch. The next `Read` repeats its read through `Cycle` until the TIA's next colour clock is a line's first, then makes the read. The TIA clears the latch on that clock. A write is never held, so `DEC WSYNC` writes twice and halts on the following opcode fetch, and a WSYNC written on a line's cycle 76 costs nothing, as `tia.md` section 5 says.
- **The floating bus.** The bus keeps the last byte read or written. A TIA read drives only the lines `Tia.DrivenLines` names (D7 and D6 for a collision register, D7 for an input port, none for `$xE` and `$xF`); the rest are that byte.
- **`Atari2600`** runs the core's `Nmos6502` unchanged on the bus, powers on (chips, then the CPU's seven-cycle reset through the bus), and offers `Step`, `Run` and `RunFrames`. The bus never touches the CPU's IRQ or NMI line: the 6507 has neither pin, and the RIOT's IRQ output goes nowhere.
- **A first `Tia`**: its write registers stored, a horizontal counter of colour clocks 0 to 227, the WSYNC latch, VSYNC ending a frame and a frame ending at 400 lines without one (the plan's item 2), the collision latches as the read registers show them, and CXCLR. Tasks 5 to 10 build the rest.
- **The speed pass's hooks** (spec, "Decisions made while writing this"): `Atari2600Options.PerColourClockReference`; a state report (`IReportsState.Report`) from the bus, the TIA, the RIOT and every board; and an observer (`IAtari2600Observer`) told of each access to the TIA or the RIOT's I/O, each bank switch, each halt's end and each frame's end, behind the runtime switch `Dbhq.Machines.Atari2600.Observable`, as the NES's is. The RIOT and the boards gained their reports here.

## Decisions

- **The halt's test is "the next colour clock is a line's first"** (`Tia.HaltReleaseDue`), checked before each halted cycle, over a latch cleared inside a cycle's ticks and checked after. With all three clocks before the access, a write on cycle 76 lands after the counter has wrapped; the test then lets the next read through, which is the "absorbed at the wrap" of `wsync-line-end`. The latch is cleared by the TIA on colour clock 0, so a second WSYNC written on the next line's cycle 1 still halts a whole line.
- **The halted cycles are real reads of the held address**, so the cartridge sees each of them. `bus.md` section 8 item 2 marks that `[guessing - verify]`; it is the data sheet's "address lines hold the current address", and no game is known to depend on it. `WsyncHaltTests` checks it through a board that writes down every access.
- **CXBLPF drives D7 only**, its unused D6 taken to float, as `tia.md` section 2.2 guesses, over driving it as 0. A test holds the choice and names it a guess.
- **The residue is `$00` at power on**, and **the TIA's counter is at colour clock 0**, both choices (`bus.md` section 8 item 3; no source gives the TIA's power-on state) `[guessing - verify]`. In `docs/known-differences.md`.
- **The inputs are fixed until task 9**: INPT0 to INPT3 read D7 = 0, as ports with no paddle connected do (`tia.md` section 20.3), and INPT4 and INPT5 read D7 = 1, both fire buttons up with the latch off.
- **A test bus constructor takes a board**, internal, so a test can fit a board that records the slot's accesses. The plan's interface takes a `Cartridge`, which stays the public one.
- **The interface's method is `Report`**, as the plan names it, where the NES's is `ReportState`; `StateCompleteness` is copied from the NES's tests with that one change.
- **The observer gets the thirteen-bit address**, the one the bus keeps, so a mirror reads as the address the chips see.
- **The TIA's frame count lives in `Tia` for now** (`Frames`, `LastFrameLines`); the plan puts it in task 5's `FrameBuffer`, which can take it over.
- **The suite's "not yet" list asserts that each test does not pass**, so a test that starts passing early fails until it is moved to the trusted list. It holds the three `cpu/` tests that `suite.md` gives to later tasks.

## The suite through the real bus

`SuiteRunner` loads a test's `_ntsc` build on NTSC or its `_pal` build on PAL from the pinned fork, powers on, and calls `Step` until `$80` holds `$A5` or `$5A` or the plan's limit of 5,000,000 instructions passes. Every trusted test the plan gives to task 4 passed on its first run, in both regions: `cpu/floating-bus`, `cpu/open-bus`, `cpu/rmw-wsync` and `cpu/stack-aliases-ram`; all eleven `riot/` tests; `harness/sanity`; `tia-timing/wsync` and `wsync-line-end`; and the four trusted cartridge tests, `mirror-2k`, `bank-f8`, `bank-f6` and `bank-f4`. Nothing needed changing in the RIOT or the boards to get there.

The three `cpu/` tests that wait for later tasks fail as expected, at the first check that needs a part not built yet. They are the plan's "not yet" list, not skips.

| Test | Waits for | Outcome, both regions |
|---|---|---|
| `cpu/partial-drive` | task 6 next, then 9 | fail, CODE `$05`, OBSERVED `$10`, EXPECTED `$50`: the first collision check. Its input checks, `$02` to `$04`, already pass on the fixed inputs |
| `cpu/rmw-strobe` | task 6 | fail, CODE `$01`, OBSERVED `$00`, EXPECTED `$04` |
| `cpu/stack-aliases-tia` | task 6 | fail, CODE `$03`, OBSERVED `$00`, EXPECTED `$80` |

The four recorded cartridge tests ran and are shown, never gated: their test asserts nothing about `bank-fa`, `bank-hotspot-window`, `ram-superchip` or `power-on-bank`, and task 11 records their outcomes. On this day each passed in both regions: `bank-fa` (OBSERVED `$B1`), `bank-hotspot-window` (`$79`), `ram-superchip` (`$F0`, the bus residue its header expects in the cell) and `power-on-bank` (`$B1`, bank 1, which task 2's journal predicted from the last-bank choice). That FA passes here says only that the model agrees with the suite's reading of CBS's patent, which its header marks untested on hardware.

The outcomes above were read on 14 October 2026 from `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~SuiteSelfTests" --logger "console;verbosity=detailed"`, which prints each test's outcome.

## The tests, test first

The test files were written first, and the first build failed to compile, naming `Atari2600Bus`, `IReportsState`, `IStateSink`, `IAtari2600Observer` and the machine class as missing. They are `Atari2600BusTests` (the decode of every address from its bits, `Peek` against each part's byte at every address, worked example 2's mirrors, one cycle and three colour clocks a call, the floating bus by register and through the CPU as worked example 4 gives it, no interrupt line, power on, `Run` and `RunFrames`, the line limit, the region switch, `Peek` with no side effect anywhere, and power on again putting every part back), `WsyncHaltTests` (a WSYNC written on each of a line's 76 cycles in both regions, `tia.md` section 5's table, the held reads as the slot sees them, a write straight after, the RIOT counting through the halt, and `DEC WSYNC`), `StateReportTests` (every field of every part reported or skipped, on every board; and the observer and the reference option changing no value and no cycle over two hundred thousand cycles of the suite's `riot/timer`), `ObserverTests` and `SuiteSelfTests`, with `SuiteRunner`, `TestPrograms` and `StateCompleteness`.

On 14 October 2026 `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests` passed 518 test cases, the 59 of `SuiteSelfTests` among them (44 trusted rows, 6 "not yet" rows, 8 recorded rows and the check that no test is in two lists), and `dotnet build -c Release` of the solution gave no warnings. Nothing under `src/Dbhq.Cpu6502/` changed.

## Every test file shown able to fail

The fault pass ran on 14 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/bus-faults.py <scratch>`, which copies the projects and the suite's cached files, plants one fault at a time in the bus, the machine, the TIA, the state reports or the RIOT's and boards' reports, builds, runs the whole Atari 2600 test project and puts the file back). The figure in brackets is the number of failing test cases in each test file.

| Fault | Planted | Test files that failed |
|---|---|---|
| B1 | A13 decoded as the cartridge's select | `Atari2600BusTests` (13), `ObserverTests` (3), `SuiteSelfTests` (44), `WsyncHaltTests` (154) |
| B2 | the TIA mirror at $0100 dropped | `Atari2600BusTests` (12), `SuiteSelfTests` (2) |
| B3 | RAM or I/O chosen by A8, not A9 | `Atari2600BusTests` (9), `ObserverTests` (1), `SuiteSelfTests` (42), `WsyncHaltTests` (2) |
| B4 | a TIA read drives all eight lines | `Atari2600BusTests` (8), `SuiteSelfTests` (4) |
| B5 | a write leaves the residue | `Atari2600BusTests` (7) |
| B6 | the halt releases one cycle late | `ObserverTests` (1), `SuiteSelfTests` (2), `WsyncHaltTests` (160) |
| B7 | RDY holds a write too | `SuiteSelfTests` (2), `WsyncHaltTests` (4) |
| B8 | the halted cycles are not cycles | `Atari2600BusTests` (1), `ObserverTests` (2), `SuiteSelfTests` (10), `WsyncHaltTests` (158) |
| B9 | the RIOT ticked after the access | `Atari2600BusTests` (4), `SuiteSelfTests` (16), `WsyncHaltTests` (2) |
| B10 | two colour clocks a cycle | `Atari2600BusTests` (4), `ObserverTests` (2), `SuiteSelfTests` (10), `WsyncHaltTests` (160) |
| B11 | a TIA write decodes A0 to A4 | `Atari2600BusTests` (1) |
| B12 | a TIA read decodes A0 to A4 | `Atari2600BusTests` (6), `SuiteSelfTests` (2) |
| B13 | Peek reads the RIOT's I/O with its side effects | `Atari2600BusTests` (1) |
| B14 | Peek of the cartridge is an access | none: it survived the first run; after the test was strengthened, `Atari2600BusTests` (1) |
| B15 | power on keeps the data bus | none: it survived the first run; after the test was strengthened, `Atari2600BusTests` (1) |
| B16 | power on keeps the board's bank | `Atari2600BusTests` (2) |
| H1 | the observer is told of every access | `ObserverTests` (1), `WsyncHaltTests` (2) |
| H2 | the observer is not told of a bank switch | `ObserverTests` (1) |
| H3 | the halt's end is not told | `ObserverTests` (1), `WsyncHaltTests` (2) |
| H4 | a frame's end is not told | `ObserverTests` (1) |
| H5 | observing clears the timer flag | `StateReportTests` (2) |
| H6 | the reference option adds a colour clock | `StateReportTests` (2) |
| S1 | the bus does not report the data bus | `StateReportTests` (9) |
| S2 | the TIA does not report its line | `StateReportTests` (9) |
| S3 | the RIOT does not report PA7's level | `StateReportTests` (9) |
| S4 | the Superchip does not report its board | `StateReportTests` (3) |
| T1 | WSYNC released at colour clock 3 | `ObserverTests` (1), `SuiteSelfTests` (2), `WsyncHaltTests` (160) |
| T2 | WSYNC's latch never reset | `Atari2600BusTests` (1), `ObserverTests` (1), `SuiteSelfTests` (18), `WsyncHaltTests` (154) |
| T3 | a frame ends at the start of VSYNC | `ObserverTests` (1) |
| T4 | the line limit one line late | `Atari2600BusTests` (1), `ObserverTests` (1) |
| T5 | CXCLR clears nothing | `Atari2600BusTests` (1) |
| T6 | CXBLPF drives D6 | `Atari2600BusTests` (1) |
| T7 | a fire button reads pressed | `Atari2600BusTests` (5) |
| T8 | a collision register's D7 and D6 swapped | `Atari2600BusTests` (3) |
| T9 | power on leaves the counter | `Atari2600BusTests` (2) |
| M1 | power on without the CPU's reset | `Atari2600BusTests` (7), `ObserverTests` (3), `WsyncHaltTests` (2) |
| M2 | the CPU a 65C02 | `SuiteSelfTests` (44), `WsyncHaltTests` (2) |
| M3 | RunFrames one frame long | `Atari2600BusTests` (2), `ObserverTests` (1) |

**Two faults survived the first run.** B14: `Peek_changes_nothing_anywhere` peeked every address in order, so on an F8 board a `Peek` that switched banks passed `$1FF8` and then `$1FF9` and ended back in bank 1; the test now peeks bank 0's hotspot last. B15: the machine's power on runs the CPU's reset, whose reads overwrite the data bus, so a bus that kept its old byte through power on could not be seen through the machine; `Section_4_the_residue_is_the_last_byte_read_or_written` now powers the bus on alone and reads it. Run again with each fault (`... bus-faults.py <scratch> B14 B15`), both were caught.

**The probe after task 5.** Task 5 rewrote the TIA and the cycle, and 13 of the probe's 38 patterns stopped matching the code (task 5's review found it). They were ported to the new code on 14 October 2026, each naming the same fault where the code still has the thing it broke. T2, a WSYNC latch never reset, now halts the machine for ever and only a timeout would catch it, so it became a release a count early. Rerun with `python3 -I tools/probes/atari-2600/bus-faults.py <scratch> B6 B8 B9 B10 H3 H5 H6 S2 T1 T2 T4 T9 M3`, all 13 were caught. The counts are as at `a6e5ff0`, before task 6's rows; with them `SuiteSelfTests` also fails more cases (B6 8, B8 16, B10 46, T1 6, T2 14, T4 10, as task 6's review counted):

| Fault | Now planted | Test files that failed |
|---|---|---|
| B6 | the halt releases one cycle late | `CycleOrderTests` (6), `ObserverTests` (1), `SuiteSelfTests` (2), `WsyncHaltTests` (160) |
| B8 | the halted cycles are not cycles | `Atari2600BusTests` (1), `CycleOrderTests` (6), `ObserverTests` (2), `SuiteSelfTests` (10), `WsyncHaltTests` (160) |
| B9 | the RIOT ticked after the access | `Atari2600BusTests` (1), `WsyncHaltTests` (2) |
| B10 | two colour clocks a cycle | `Atari2600BusTests` (4), `CycleOrderTests` (6), `ObserverTests` (2), `SuiteSelfTests` (10), `WsyncHaltTests` (160) |
| H3 | the halt's end is not told | `ObserverTests` (1), `WsyncHaltTests` (2) |
| H5 | observing clears the timer flag | `StateReportTests` (2) |
| H6 | the reference option adds a colour clock | `StateReportTests` (2) |
| S2 | the TIA does not report its counter | `StateReportTests` (9) |
| T1 | WSYNC released a count late | `CycleOrderTests` (6), `ObserverTests` (1), `TiaLineTests` (3), `WsyncHaltTests` (160) |
| T2 | WSYNC released a count early | `CycleOrderTests` (6), `ObserverTests` (1), `TiaLineTests` (3), `WsyncHaltTests` (160) |
| T4 | the line limit one line late | `Atari2600BusTests` (3), `ObserverTests` (1), `SuiteSelfTests` (2), `TiaLineTests` (1) |
| T9 | power on leaves the counter | `Atari2600BusTests` (2), `TiaLineTests` (1) |
| M3 | `RunFrames` one frame long | `Atari2600BusTests` (2), `FrameBufferTests` (4), `ObserverTests` (1) |

The faults each test file caught: `Atari2600BusTests` (B1 to B5, B11 to B16, T4 to T9, M1, M3), `WsyncHaltTests` (B6 to B10, T1, T2), `ObserverTests` (H1 to H4, T3), `StateReportTests` (H5, H6, S1 to S4) and `SuiteSelfTests` (B1 to B4, B6 to B10, M2). In all, 38 faults were planted and all 38 were caught, two of them only after their test was strengthened. Two faults were left out as no fault at all: dropping the thirteen-line mask changes nothing a part can see, because no decode reads A13 to A15 and each part decodes its own low lines, but the observer is the exception, since it is told the address the bus keeps (the task 4 review found the fault survived; `ObserverTests.The_observer_is_told_the_thirteen_bit_address` now catches it); and a WSYNC release one colour clock late never comes, because a cycle starts on a multiple of three clocks until task 5's RSYNC, so the halt would never end.

## The task 4 review

A reviewer read task 4 and found eight things, fixed in one commit of tests, comments and this entry, with no change of behaviour. The "not yet" test now asserts each test fails, at the CODE, OBSERVED and EXPECTED in the table above, so a hang or a different failure no longer satisfies it, and the table names task 6 as the next to run `cpu/partial-drive`, since its first failure is a collision check. The power-on residue and the halted reads reaching the cartridge carry `[guessing - verify]` wherever the code states them. A new observer test reads `$E280` and expects to be told `$0280`, which the dropped mask fails. The WSYNC tests say their recording board stands in for the observer, which is not told of plain cartridge reads, and the state report test says its reference half is vacuous until the speed pass. The new assertions were each shown to fail in a scratch copy on 14 October 2026, with one fault planted at a time and the whole test project run: the mask dropped fails the new observer test; a fire button read as pressed makes `cpu/partial-drive` fail at a different check, which the "not yet" test now catches in both regions; and RAM writes lost, so no test ever writes a verdict, fails all six "not yet" rows, where the old assertion passed them.

