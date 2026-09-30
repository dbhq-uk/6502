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
