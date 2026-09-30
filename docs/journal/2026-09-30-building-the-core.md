# 30 September 2026: building the core

## Task 1: The solution, the first instruction, and CI

The skeleton every later task builds on: the solution, the core with `Reset` and one instruction (`NOP`), the test projects, and the `Validate` workflow.

### What was built

The C# solution file `6502.slnx` with three projects:

- `src/Dbhq.Cpu6502/Dbhq.Cpu6502.csproj`: The core library. No external dependencies beyond the .NET runtime.
- `tests/Dbhq.Cpu6502.TestSupport/Dbhq.Cpu6502.TestSupport.csproj`: A test support library providing `FlatBus`, a 64 KB RAM that can record every bus access, and `RepoPaths` to find the repository root.
- `tests/Dbhq.Cpu6502.Tests/Dbhq.Cpu6502.Tests.csproj`: The test suite using xunit.

### Types and interfaces

Defined in the core:

- `IBus`: The bus interface. Every read or write is one cycle.
- `CpuVariant`: An enum naming the five family members: `Nmos6502`, `Ricoh2A03`, `Synertek65C02`, `Rockwell65C02`, `Wdc65C02`.
- `StatusFlags`: The bits of the status register.
- `Cpu`: The main class, with properties `A`, `X`, `Y`, `S`, `P`, `PC`, `Cycles`, `Irq`, `Nmi`, `IsJammed`, `IsWaiting`, `IsStopped`, and methods `Step()` and `Reset()`.

In the test support library:

- `FlatBus`: A bus implementation backed by a byte array, with a `Log` recording every access.
- `BusAccess`: A record of one bus access (address, value, is-write).
- `RepoPaths`: Static properties to find the repository root and the `.testdata` directory.

### Instructions implemented

One instruction in the official set:

- `0xEA` (NOP): A no-op that reads the next byte and takes two cycles.

The NMOS undocumented opcodes and the 65C02-specific instructions throw `NotImplementedException` with the opcode that is missing. This means an unimplemented instruction fails loudly with its name instead of silently doing the wrong thing.

### Bus access and cycles

- `Reset()` takes exactly seven cycles: three stack reads that decrement S as the chip does, then the reset vector at `$FFFC`. The interrupt-disable flag is set; on the 65C02 variants, decimal mode is cleared.
- `NOP` takes two cycles: one to fetch, one to read the next byte.
- `Step()` runs one instruction and returns the number of cycles it took.

Every cycle is one read or one write. A throwaway read is explicit in the code, because on real hardware it reaches the bus.

### The red step: build failure

Before implementing `Cpu`, the tests were created referencing a class that did not exist. The build failed with "The type or namespace name 'Cpu' could not be found". This is the expected failure before any code that makes it pass.

### Decisions made

**All addressing mode helpers were written in task 1**, though later tasks will implement the instructions that use them. Alternative: add each helper in the task that first needs it. Reason for the choice: every helper is small enough to review in one task; reviewing thirty helpers scattered across later tasks would be harder; and the plan's code was built and tested before it was written, so the helpers are proven already.

### Tests

Three test methods, seven test cases, all passing:

- **ResetTakesSevenCyclesAndReadsTheVector**: Verifies that reset takes seven cycles, sets the PC from the reset vector at `$FFFC`, sets the stack pointer to `$FD`, and sets the interrupt-disable flag. The bus log confirms no writes happen during reset.
- **ResetClearsDecimalModeOnlyOnThe65C02**: Verifies that the NMOS 6502 and the 2A03 keep their decimal mode flag across reset, but all three 65C02 variants clear it.
- **NopIsTwoCyclesAndReadsTheByteAfterIt**: Verifies that NOP fetches from PC, increments PC, and takes two cycles.

Run: `dotnet test` on 30 September 2026.

```
Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7, Duration: 108 ms - Dbhq.Cpu6502.Tests.dll (net10.0)
```

### CI

The `Validate` workflow:

- Runs on push to main (to record when main was last green), on every pull request, and on manual dispatch.
- Runs the full test suite with `dotnet test --configuration Release`.
- Checks that no file carries an em dash or en dash (all hyphens must be plain).
- Caches third-party test data (downloaded on first run, reused if the hash has not changed).
- Installs 32-bit runtime support for Dormann's as65 assembler before it is used.

The workflow is pinned to named commits: `checkout@3d3c42...`, `setup-dotnet@a98b56...`, `cache@55cc83...`.

### Corrections

On review, the comment for `CpuVariant.Ricoh2A03` said "in the NES", which violates constraint 2 (the core knows no machine). Corrected to "Ricoh's 2A03 and 2A07: the NMOS 6502 with decimal mode removed." The same line in `docs/superpowers/plans/2026-09-30-stage-1-core.md` was corrected to prevent the same mistake in later tasks that use the plan as a source.

## Task 2: The Harte harness

The test harness that proves every instruction correct by checking every cycle, register and memory location against reference test data. Tom Harte's SingleStepTests provides 1,280 files covering all opcodes across all five variants, with 10,000 cases per opcode - 12.8 million test cases.

### What was built

Two new test support classes:

- `Pins.cs`: A static class holding every third-party commit and hash the tests use. Organizes the constants so they are easy to find and verify.
- `PinnedFiles.cs`: Downloads a file once, checks it against a recorded git blob hash, caches it under `.testdata`, and checks it again before each use.

A manifest generation script:

- `tools/harte-manifest.sh`: Calls GitHub's tree API once for the pinned Harte commit and records every test file's git blob hash. Generates 1,280-line `harte.manifest` in one run.

Six test classes in the `Harte` namespace:

- `HarteFile.cs`: Reads a JSON file from Tom Harte's test set and parses it into records holding the initial state, final state, and the sequence of bus cycles.
- `HarteSets.cs`: Maps each variant to its Harte folder, looks up a test file in the manifest, and fetches it (checking the hash automatically).
- `HarteRunner.cs`: Runs test cases one at a time, cycle by cycle. Catches `NotImplementedException` and reports it as a test failure. Has one documented exception: the 65C02's extra decimal-mode cycle on `ADC #imm` and `SBC #imm`, which Harte records but whose address differs by variant. The cycle must exist as a read; its address and value are not compared.
- `HarteRunnerTests.cs`: Unit tests proving the harness itself fails when it should. Tests a correct case passes, a wrong cycle is named, and the decimal exception is exactly one cycle wide.
- `Coverage.cs`: Tracks which opcodes have implementations so far. Task 2 implements `0xA9` (LDA #) and `0xEA` (NOP), so it returns those two. Task 11 replaces this list with all 256 opcodes.
- `HarteTests.cs`: Five test classes, one per variant. Each runs every implemented opcode's Harte cases, checking that bus cycles, registers, and memory match the reference data.

One instruction implemented:

- `0xA9` (LDA #): Load the accumulator with an immediate value. Reads one byte from the next instruction, sets the N and Z flags based on the result, and takes two cycles.

Modified `src/Dbhq.Cpu6502/Cpu.Official.cs` to add LDA # and modified `tests/Dbhq.Cpu6502.Tests/Dbhq.Cpu6502.Tests.csproj` to copy the manifest to the output directory.

### The red step: opcode not implemented

Before `LDA #` was implemented, running the tests showed:

```
Nmos6502Harte.Opcode(opcode: 169) FAILED
Ricoh2A03Harte.Opcode(opcode: 169) FAILED
Synertek65C02Harte.Opcode(opcode: 169) FAILED
Rockwell65C02Harte.Opcode(opcode: 169) FAILED
Wdc65C02Harte.Opcode(opcode: 169) FAILED
```

Each failure reported "Nmos6502 opcode $A9 is not implemented" (or the appropriate variant). The harness test for NOP and all harness infrastructure tests passed.

### Tests and results

Twenty test methods passing:

- **HarteRunnerTests** (3 methods): Unit tests for the harness itself. `ACorrectCasePasses` verifies a known-good case passes. `AWrongCycleIsNamed` verifies the harness catches a cycle mismatch and reports which cycle failed. `TheDecimalExceptionIsOneCycleWide` verifies the decimal-mode exception is narrowly scoped.
- **Nmos6502Harte** (2 methods): Opcode tests for LDA # and NOP on the NMOS 6502.
- **Ricoh2A03Harte** (2 methods): The same opcodes on the Ricoh 2A03.
- **Synertek65C02Harte** (2 methods): The same opcodes on the Synertek 65C02.
- **Rockwell65C02Harte** (2 methods): The same opcodes on the Rockwell 65C02.
- **Wdc65C02Harte** (2 methods): The same opcodes on the WDC 65C02.

Run: `dotnet test` on 30 September 2026.

```
Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:    20, Duration: 7 s - Dbhq.Cpu6502.Tests.dll (net10.0)
```

The first run downloaded ten files from GitHub (five for LDA #, five for NOP, one per variant), checked each against its git blob hash from the manifest, and cached them under `.testdata`. Subsequent runs use the cached copies and re-check them before running the tests.

### Decisions made

**The manifest was generated from GitHub's tree API, not by scanning a local download.** Harte's repository is 5 GB. Alternative: download the whole repository and hash its files locally. Reason for the choice: a single API call records all hashes in one call, at a fixed commit; the tests can then verify downloads without anyone downloading the whole 5 GB. If Harte's repository were deleted or moved, the recorded hashes mean the files can still be verified.

### Known differences

The 65C02's extra decimal-mode cycle on `ADC #imm` (0x69) and `SBC #imm` (0xE9) is documented in `docs/known-differences.md`. Harte's data has the cycle read a fixed address that differs by variant, which looks like an artefact of how the data was made. The cycle must exist as a read; address and value are not compared. This exception is scoped to exactly cycle 2 of those two instructions when decimal mode is set.

### Surprises

None. The harness worked as designed on first build.

## Task 3: Loads, stores, logic and shifts

The 117 shared opcodes that are neither arithmetic nor control flow. Each instruction reads like the chip's cycles: an instruction's cycles are counted from its `Read` and `Write` calls. Read-modify-write goes through `ReadForModify`, which reads a value, then on the NMOS chip writes it back, or on the 65C02 reads it again.

### What was built

Three modified or created files:

- **`src/Dbhq.Cpu6502/Cpu.Logic.cs`** (new file): A partial class containing the shift and logic helpers: `Asl`, `Lsr`, `Rol`, `Ror`, `And`, `Ora`, `Eor`, `Compare`, `Bit`, and the read-modify-write helpers `AslAt`, `LsrAt`, `RolAt`, `RorAt`, `IncAt`, `DecAt`. Every helper is written to align with the bus cycles of its instruction.
- **`src/Dbhq.Cpu6502/Cpu.Official.cs`** (modified): Expanded `ExecuteOfficial` from 2 opcodes to 117 opcodes. Opcodes are grouped by function: loads (18), stores (13), transfers (6), flags (7), increments and decrements (12), compares (14), logic (26), and shifts and rotates (20), plus NOP (0xEA), the 117th. Group counts verified by counting `case` labels: `grep -c "case 0x" src/Dbhq.Cpu6502/Cpu.Official.cs` returns 117.
- **`tests/Dbhq.Cpu6502.Tests/Harte/Coverage.cs`** (modified): Updated the coverage list from `FirstInstructions` (2 opcodes) to `LoadsStoresLogicAndShifts` (117 opcodes). The array lists every opcode once in ascending order, spanning 0x01 to 0xFE.

### Instructions implemented

One hundred seventeen instructions:

- **Loads (18)**: LDA #, LDA zp, LDA zp,X, LDA abs, LDA abs,X, LDA abs,Y, LDA (ind,X), LDA (ind),Y; LDX #, LDX zp, LDX zp,Y, LDX abs, LDX abs,Y; LDY #, LDY zp, LDY zp,X, LDY abs, LDY abs,X.
- **Stores (13)**: STA zp, STA zp,X, STA abs, STA abs,X, STA abs,Y, STA (ind,X), STA (ind),Y; STX zp, STX zp,Y, STX abs; STY zp, STY zp,X, STY abs.
- **Transfers (6)**: TAX, TAY, TXA, TYA, TSX, TXS.
- **Flags (7)**: CLC, SEC, CLI, SEI, CLV, CLD, SED.
- **Increments and decrements (12)**: INC zp, INC zp,X, INC abs, INC abs,X; DEC zp, DEC zp,X, DEC abs, DEC abs,X; INX, INY, DEX, DEY.
- **Compares (14)**: CMP #, CMP zp, CMP zp,X, CMP abs, CMP abs,X, CMP abs,Y, CMP (ind,X), CMP (ind),Y; CPX #, CPX zp, CPX abs; CPY #, CPY zp, CPY abs.
- **Logic (26)**: AND #, AND zp, AND zp,X, AND abs, AND abs,X, AND abs,Y, AND (ind,X), AND (ind),Y; ORA #, ORA zp, ORA zp,X, ORA abs, ORA abs,X, ORA abs,Y, ORA (ind,X), ORA (ind),Y; EOR #, EOR zp, EOR zp,X, EOR abs, EOR abs,X, EOR abs,Y, EOR (ind,X), EOR (ind),Y; BIT zp, BIT abs.
- **Shifts and rotates (20)**: ASL A, ASL zp, ASL zp,X, ASL abs, ASL abs,X; LSR A, LSR zp, LSR zp,X, LSR abs, LSR abs,X; ROL A, ROL zp, ROL zp,X, ROL abs, ROL abs,X; ROR A, ROR zp, ROR zp,X, ROR abs, ROR abs,X.
- **NOP (1)**: 0xEA, a no-op that reads the next byte and takes two cycles.

### The red step: opcodes not implemented

Before `Cpu.Logic.cs` and the updated `Cpu.Official.cs`, running `dotnet test --filter "FullyQualifiedName~Harte"` showed 575 test failures and 13 test passes. Every failure reported an unimplemented opcode: "Nmos6502 opcode $xx is not implemented", with variant-specific messages for each of the five CPU types. Typical failure output:

```
Nmos6502 $36 fails 5 or more cases:
36 13 1b: Nmos6502 opcode $36 is not implemented
36 49 c9: Nmos6502 opcode $36 is not implemented
36 c2 62: Nmos6502 opcode $36 is not implemented
36 a9 16: Nmos6502 opcode $36 is not implemented
36 22 3b: Nmos6502 opcode $36 is not implemented
```

The harness tests themselves (HarteRunnerTests) and the two previously implemented opcodes (0xA9 and 0xEA) continued to pass.

### Tests and results

All 595 tests pass:

Run: `dotnet test` on 30 September 2026.

```
Passed!  - Failed:     0, Passed:   595, Skipped:     0, Total:   595, Duration: 2 m 52 s - Dbhq.Cpu6502.Tests.dll (net10.0)
```

The 595 passing tests are:

- **CpuTests**: 3 methods, 7 cases (`ResetTakesSevenCyclesAndReadsTheVector`, `ResetClearsDecimalModeOnlyOnThe65C02` with 5 variant cases, `NopIsTwoCyclesAndReadsTheByteAfterIt`).
- **HarteRunnerTests** (3 methods): `ACorrectCasePasses`, `AWrongCycleIsNamed`, `TheDecimalExceptionIsOneCycleWide`.
- **Harte opcode tests**: 5 theory methods (one per variant), 585 cases (117 opcodes x 5 variants).

The test run took 2 minutes 52 seconds on a single machine.

### Decisions made

**All helpers were written in a single file (`Cpu.Logic.cs`) rather than split per-instruction.** Alternative: create a separate helper file per instruction or per category. Reason for the choice: all helpers are small enough to understand in one read; reviewing them together makes the patterns visible (carry-in handling in Rol/Ror, flag updates in Compare).

**Shifts and rotates use `ReadForModify` for memory operations.** The NMOS 6502's read-modify-write instructions read a value at an address, write it back unchanged, then write the modified value. The 65C02 improved this: after reading the value, it reads it again instead of writing it back. One `ReadForModify` call handles both: if CMOS, it calls `Read` twice; if NMOS, it calls `Read` then `Write`. This keeps the code simple and the bus cycles accurate.

**Immediate-mode instructions read from PC and post-increment.** This is true of all immediate-mode opcodes: `LDA #`, `CMP #`, `AND #` and the rest. The increment is part of the fetch, not a separate cycle.

**Transfer instructions read the next byte and discard it.** 0xAA (TAX) and its peers have an unused fetch as part of the two-cycle instruction. The code calls `Read(PC)` and discards the result; the read is explicit because it touches the bus.

### Known differences

None discovered. All test cases pass against Harte's reference data.

### Surprises

None. The helpers worked as designed on first build. All 117 opcodes passed their first test run.

## Task 4: ADC and SBC

Binary and decimal arithmetic on every variant. Decimal mode follows Bruce Clark's description, which `tools/probes/check_harte_findings.py` verified against every decimal case before the plan was written. The NMOS chip takes N and V from before the high digit is adjusted and Z from the binary sum; the 65C02 takes N and Z from the result and spends one more cycle. The 2A03 has no decimal mode.

### What was built

Four modified or created files:

- **`src/Dbhq.Cpu6502/Cpu.cs`** (modified): Added the `_decimal` field, set in the constructor from `variant != CpuVariant.Ricoh2A03` so every variant except the 2A03 can execute decimal-mode arithmetic.
- **`src/Dbhq.Cpu6502/Cpu.Arithmetic.cs`** (new file): All arithmetic helpers: `AdcAt(address)`, `SbcAt(address)`, `Adc(value)`, `Sbc(value)`, `DecimalExtraCycle(address)`, `AdcDecimal(value, carryIn)`, `SbcDecimal(value, carryIn)`. ADC and SBC branch into binary or decimal paths depending on the decimal flag. In decimal mode, `AdcDecimal` computes V from the signed sum before the high digit is adjusted (same on both variants); only N and Z differ by variant (NMOS: from the sum before adjustment and binary sum respectively; 65C02: from the result). In `SbcDecimal`, V and C come from the binary subtraction on both variants; only the result adjustment and where N and Z are taken differ by variant.
- **`src/Dbhq.Cpu6502/Cpu.Official.cs`** (modified): Expanded from 117 opcodes to 133 opcodes. Arithmetic section added: ADC (0x69, 0x65, 0x75, 0x6D, 0x7D, 0x79, 0x61, 0x71) and SBC (0xE9, 0xE5, 0xF5, 0xED, 0xFD, 0xF9, 0xE1, 0xF1), 16 case labels verified by `grep -c "case 0x" src/Dbhq.Cpu6502/Cpu.Official.cs`.
- **`tests/Dbhq.Cpu6502.Tests/Harte/Coverage.cs`** (modified): Added the `Arithmetic` array holding the 16 ADC and SBC opcodes, and combined it with `LoadsStoresLogicAndShifts` into the `Official` list.

### Instructions implemented

Sixteen opcodes (ADC and SBC in eight addressing forms each).

- **ADC (8)**: ADC #, ADC zp, ADC zp,X, ADC abs, ADC abs,X, ADC abs,Y, ADC (ind,X), ADC (ind),Y.
- **SBC (8)**: SBC #, SBC zp, SBC zp,X, SBC abs, SBC abs,X, SBC abs,Y, SBC (ind,X), SBC (ind),Y.

### The red step: opcodes not implemented

Before `Cpu.Arithmetic.cs` and the updated `Cpu.Official.cs`, running `dotnet test --filter "FullyQualifiedName~Harte"` showed 80 test failures. Each newly covered opcode failed with "is not implemented": examples included Nmos6502 $F5, Synertek65C02 $E1, Wdc65C02 $ED, Rockwell65C02 $E9, Rockwell65C02 $79. The previously implemented 117 opcodes and all harness tests continued to pass.

### Tests and results

All 675 tests pass:

Run: `dotnet test` on 30 September 2026.

```
Passed!  - Failed:     0, Passed:   675, Skipped:     0, Total:   675, Duration: 1 m 16 s - Dbhq.Cpu6502.Tests.dll (net10.0)
```

The 675 passing tests comprise:

- **CpuTests**: 3 methods, 7 cases.
- **HarteRunnerTests**: 3 methods.
- **Harte opcode tests**: 5 theory methods (one per variant), 665 cases (133 opcodes x 5 variants).

### Decisions made

**Immediate-mode ADC and SBC on the 65C02 spend an extra cycle in decimal mode reading the operand byte again.** This is the `DecimalExtraCycle` method, which reads the same address again when the 65C02 is in decimal mode. Alternative: handle the decimal-mode cycle in the immediate-mode cases themselves. Reason for the choice: the extra cycle is only decimal-mode specific, and centralising the logic in one method keeps it in one place. It also covers memory-addressed ADC and SBC, so one place holds the CMOS logic.

**Decimal arithmetic branches at the start of `Adc` and `Sbc`.** The binary and decimal paths are fundamentally different (binary uses one addition, decimal adjusts digits), so an early branch reads clearer than flag checks scattered through a single algorithm. Alternative: one algorithm with conditional digit adjustment. Reason: clarity; the paths are already distinct enough that trying to unify them would make each harder to follow.

**One decimal helper per operation (`AdcDecimal`, `SbcDecimal`), branching on `_cmos` internally where the variants differ.** Alternative: four separate helpers, one per operation per variant. Reason for the choice: the shared parts (V computation in `AdcDecimal`, V and C in `SbcDecimal`) are substantial enough that repeating them would obscure the differences. Branching at the point of difference keeps the shared logic visible and maintainable.

### Known differences

The 65C02's extra decimal-mode cycle on ADC #imm (0x69) and SBC #imm (0xE9) is documented in `docs/known-differences.md`. In every addressing mode, the extra cycle re-reads the operand's address. Immediate-mode is the only mode where Harte's data disagrees, recording a fixed address instead. The core re-reads the operand address in all modes including immediate, so the tests check that the third cycle (index 2), on the three 65C02 variants, with the decimal flag set, is a read, and do not compare its address or value.

### Surprises

None. The arithmetic operations worked as designed on first build. All 16 new opcodes passed their first test run against Harte's data for all five variants.

## Task 5: Control flow and the stack

Branches, jumps, subroutines, break, return from interrupt, and the four stack instructions. JMP indirect handles the NMOS page-wrap bug and the 65C02 fix. The 151 shared opcodes across all variants are now fully covered.

### What was built

Three files modified or created:

- **`src/Dbhq.Cpu6502/Cpu.ControlFlow.cs`** (new file): Control flow helpers: `Branch(taken)`, `JmpIndirect`, `Jsr`, `Rts`, `Rti`, `Brk`. Branch handles conditional jumps with the timing of the false-condition path and the cross-page-boundary penalty read. JmpIndirect models the NMOS bug (pointer never carries into the high byte) against the 65C02 fix (extra throwaway read of the wrong address).
- **`src/Dbhq.Cpu6502/Cpu.Official.cs`** (modified): Expanded from 133 opcodes to 151. Control flow section added with branches (0x10, 0x30, 0x50, 0x70, 0x90, 0xB0, 0xD0, 0xF0), jumps (0x4C, 0x6C), subroutines (0x20, 0x60), interrupt return (0x40), break (0x00). Stack section added: PHA, PHP, PLA, PLP (0x48, 0x08, 0x68, 0x28). Case labels verified: `grep -c "case 0x" src/Dbhq.Cpu6502/Cpu.Official.cs` returns 151.
- **`tests/Dbhq.Cpu6502.Tests/Harte/Coverage.cs`** (modified): Added `ControlFlowAndStack` array with 18 opcodes, combined with prior arrays into `Official`.

### Instructions implemented

Eighteen opcodes:

- **Branches (8)**: BPL, BMI, BVC, BVS, BCC, BCS, BNE, BEQ (0x10, 0x30, 0x50, 0x70, 0x90, 0xB0, 0xD0, 0xF0).
- **Jumps and subroutines (3)**: JMP abs, JMP ind, JSR (0x4C, 0x6C, 0x20).
- **Returns (2)**: RTS, RTI (0x60, 0x40).
- **Break (1)**: BRK (0x00).
- **Stack (4)**: PHA, PHP, PLA, PLP (0x48, 0x08, 0x68, 0x28).

### The red step: opcodes not implemented

Before `Cpu.ControlFlow.cs` and the updated `Cpu.Official.cs`, running `dotnet test --filter "FullyQualifiedName~Harte"` showed test failures for each newly covered opcode. Examples included Nmos6502 $00, Ricoh2A03 $F0, Wdc65C02 $40. Each reported "opcode $xx is not implemented". The previously implemented 133 opcodes and all harness tests continued to pass.

### Tests and results

All 765 tests pass.

Run: `dotnet test` on 30 September 2026.

```
Passed!  - Failed:     0, Passed:   765, Skipped:     0, Total:   765, Duration: 3 m 26 s - Dbhq.Cpu6502.Tests.dll (net10.0)
```

The 765 passing tests comprise:

- **CpuTests**: 3 methods, 7 cases.
- **HarteRunnerTests**: 3 methods.
- **Harte opcode tests**: 5 theory methods (one per variant), 755 cases (151 opcodes x 5 variants).

### Decisions made

No decisions: transcribed from the brief.

### Surprises

None. All 18 new opcodes passed their first test run against Harte's data for all five variants.

## Task 6: The 105 undocumented NMOS opcodes

All NMOS-only opcodes: the no-ops of every length, the twelve JAM opcodes, the read-modify-write combinations, LAX and SAX, the immediate oddities, and the stores that AND with the high byte. The 2A03 is NMOS, so both NMOS variants cover all 256 opcodes.

### What was built

Four files modified:

- **`src/Dbhq.Cpu6502/Cpu.cs`** (modified): Added jam handling to `Step()`. When `IsJammed` is true, `Step()` reads `$FFFF` and returns 1 cycle, keeping the clock running without executing further instructions.
- **`src/Dbhq.Cpu6502/Cpu.Nmos.cs`** (modified): All 105 undocumented opcodes and their helpers: `ExecuteNmos(byte opcode)` with a switch covering no-ops (27 cases in six addressing-mode groups: 1-byte, 2-byte immediate, zero page, zero page,X, absolute, absolute,X), JAM (12 cases), shift-or-ALU combinations (Slo, Rla, Sre, Rra, Dcp, Isc - 42 cases each with seven forms), SAX (4 cases) and LAX (6 cases, 10 total), immediate oddities (8 cases: ANC, ALR, ARR, ANE, LXA, SBX, SBC), stores that AND with one more than the high byte (5 cases: SHY, SHX, SHA x2, TAS), and LAS (1 case). The Jam method makes the opcode fetch plus ten reads (PC, `$FFFF`, `$FFFE` twice, `$FFFF` six times), matching Harte's record. ANE and LXA use the constant `$EE`, as Harte's data does; `docs/known-differences.md` notes that real chips vary.
- **`tests/Dbhq.Cpu6502.Tests/Harte/Coverage.cs`** (modified): Updated `NmosOnly` to use `OtherThan(Official)`, which computes the 105 undocumented opcodes by excluding the 151 official ones from all 256.

### Instructions implemented

All 105 undocumented NMOS opcodes:

- **No-ops (27)**: 0x1A, 0x3A, 0x5A, 0x7A, 0xDA, 0xFA (1-byte); 0x80, 0x82, 0x89, 0xC2, 0xE2 (2-byte immediate); 0x04, 0x44, 0x64 (zero page); 0x14, 0x34, 0x54, 0x74, 0xD4, 0xF4 (zero page,X); 0x0C (absolute); 0x1C, 0x3C, 0x5C, 0x7C, 0xDC, 0xFC (absolute,X).
- **JAM (12)**: 0x02, 0x12, 0x22, 0x32, 0x42, 0x52, 0x62, 0x72, 0x92, 0xB2, 0xD2, 0xF2.
- **Shift-or-ALU (42)**: SLO (0x07, 0x17, 0x0F, 0x1F, 0x1B, 0x03, 0x13), RLA (0x27, 0x37, 0x2F, 0x3F, 0x3B, 0x23, 0x33), SRE (0x47, 0x57, 0x4F, 0x5F, 0x5B, 0x43, 0x53), RRA (0x67, 0x77, 0x6F, 0x7F, 0x7B, 0x63, 0x73), DCP (0xC7, 0xD7, 0xCF, 0xDF, 0xDB, 0xC3, 0xD3), ISC (0xE7, 0xF7, 0xEF, 0xFF, 0xFB, 0xE3, 0xF3).
- **SAX (4)**: 0x87, 0x97, 0x8F, 0x83.
- **LAX (6)**: 0xA7, 0xB7, 0xAF, 0xBF, 0xA3, 0xB3.
- **Immediate oddities (8)**: ANC (0x0B, 0x2B), ALR (0x4B), ARR (0x6B), ANE (0x8B), LXA (0xAB), SBX (0xCB), SBC (0xEB).
- **Stores that AND with high byte plus one (5)**: SHY (0x9C), SHX (0x9E), SHA (0x9F, 0x93), TAS (0x9B).
- **LAS (1)**: 0xBB.

### The red step: opcodes not implemented

Before implementing the 105 opcodes in `Cpu.Nmos.cs`, running `dotnet test --filter "FullyQualifiedName~Harte"` showed 210 test failures on the NMOS variants. Captured failure examples included:
```
Ricoh2A03 $03 fails 5 or more cases:
03 02 08: Ricoh2A03 opcode $03 is not implemented
Nmos6502 $5F fails 5 or more cases:
5f 44 66: Nmos6502 opcode $5F is not implemented
```
The previously implemented 151 official opcodes and all harness tests continued to pass.

### Tests and results

All 975 tests pass.

Run: `dotnet test` on 30 September 2026.

```
Passed!  - Failed:     0, Passed:   975, Skipped:     0, Total:   975, Duration: 2 m 57 s - Dbhq.Cpu6502.Tests.dll (net10.0)
```

The 975 passing tests comprise:

- **CpuTests**: 3 methods, 7 cases.
- **HarteRunnerTests**: 3 methods.
- **Harte opcode tests**: 5 theory methods (one per variant), 965 cases (256 opcodes x 2 NMOS variants + 151 opcodes x 3 CMOS variants = 965).

### Decisions made

No decisions: transcribed from the brief.

### Known differences

JAM is stepped as Harte records it (the opcode fetch plus ten reads, then the chip stays jammed). ANE and LXA use `$EE` because Harte's data does; `docs/known-differences.md` notes real chips vary.

### Surprises

None. All 105 new opcodes passed their first test run against Harte's data for the NMOS 6502 and Ricoh 2A03 variants.

## Task 7: The 65C02

All three variants of the 65C02 - Synertek, Rockwell and WDC - now cover every opcode. RMB, SMB, BBR and BBS are real instructions on Rockwell and WDC, but on Synertek they are no-ops whose length and timing depend on which row (verified by `grep -c "case 0x" src/Dbhq.Cpu6502/Cpu.Cmos.cs`: 34 case labels in the switch statement covering all patterns including multi-case statements). WAI and STP exist on WDC only; they set the IsWaiting and IsStopped flags, stopping the CPU until an interrupt (task 8) or reset.

### What was built

Three files modified and one new:

- **`src/Dbhq.Cpu6502/Cpu.cs`** (modified): Added `_bitInstructions` and `_waitAndStop` flags set in the constructor (Rockwell and WDC get bit instructions; WDC only gets wait and stop). Modified `Step()` to return 0 cycles when IsStopped or IsWaiting (task 8 teaches WAI to wake).
- **`src/Dbhq.Cpu6502/Cpu.Cmos.cs`** (new file): `ExecuteCmos` method with all 65C02-specific opcodes: the (zp) indirect mode (8 opcodes), TSB and TRB (4 opcodes), INC A and DEC A (2 opcodes), BIT zp,X and BIT abs,X (2 opcodes), BIT #imm (1 opcode), PHX, PHY, PLX, PLY (4 opcodes), STZ (4 opcodes), JMP (addr,X) (1 opcode), BRA (1 opcode), RMB and SMB (16 cases on Rockwell and WDC, 8 opcodes with address modes), BBR and BBS (16 cases on Rockwell and WDC, 8 opcodes with address modes), WAI and STP (2 opcodes on WDC only), and defined no-ops for all other 65C02-only opcodes.
- **`tests/Dbhq.Cpu6502.Tests/Harte/Coverage.cs`** (modified): Set `CmosOnly = OtherThan(Official)`, allowing all three 65C02 variants to test every opcode (except WAI and STP on WDC, which have no Harte data).

### Instructions implemented

All 65C02-specific opcodes on all three variants, covering every opcode except WAI and STP (which have no reference data). The addressing mode additions are (zp) for logic and load-store (8 opcodes); new standalone instructions are INC A, DEC A, BIT #imm, BRA, JMP (addr,X) (5 opcodes); new addressing modes for existing instructions are BIT zp,X, BIT abs,X, and STZ zp, STZ zp,X, STZ abs, STZ abs,X (6 opcodes). The bit manipulation instructions RMB, SMB, BBR, BBS span rows 0-7 (16 case labels total) and exist on Rockwell and WDC only; on Synertek they are no-ops with lengths depending on the row - three cycles in even rows, four in odd ones (BBR and BBS only; RMB and SMB patterns differ).

### The red step: opcodes not implemented

Before `Cpu.Cmos.cs` and the updated Coverage, running `dotnet test --filter "FullyQualifiedName~Harte"` showed 1281 tests failing. Every 65C02-specific opcode reported "opcode $xx is not implemented". Examples included Synertek65C02 $12, Wdc65C02 $1C, Rockwell65C02 $3F. The previously implemented 151 official opcodes and all harness tests continued to pass.

### Tests and results

All 1288 tests pass.

Run: `dotnet test` on 30 September 2026.

```
Passed!  - Failed:     0, Passed:  1288, Skipped:     0, Total:  1288, Duration: 1 m 18 s
```

The 1288 passing tests comprise:

- **CpuTests**: 3 methods, 7 cases.
- **HarteRunnerTests**: 3 methods.
- **Harte opcode tests**: 5 theory methods (one per variant), 1278 cases (256 opcodes each for NMOS6502, Ricoh2A03, Synertek65C02, Rockwell65C02; 254 for Wdc65C02 excluding 0xCB and 0xDB which have no Harte data).

### Decisions made

No decisions: transcribed from the brief.

### Known differences

RMB/SMB/BBR/BBS are real instructions on Rockwell and WDC; on Synertek their opcodes are no-ops whose length and timing depend on the row, documented in `docs/known-differences.md`. WAI and STP exist on WDC only and have no Harte data; `docs/known-differences.md` records this. `Cpu.cs` was modified (added two flags and changed `Step()`); `Cpu.Cmos.cs` was created; `Coverage.cs` was modified.

### Surprises

None. All 105 new 65C02-specific opcodes passed their first test run against Harte's data for all three CMOS variants.
