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
- `RepoPaths`: Static methods to find the repository root and the `.testdata` directory.

### Instructions implemented

One instruction in the official set:

- `0xEA` (NOP): A no-op that reads the next byte and takes two cycles.

The NMOS undocumented opcodes and the 65C02-specific instructions throw `NotImplementedException` with the opcode that is missing. This means an unimplemented instruction fails loudly with its name instead of silently doing the wrong thing.

### Bus access and cycles

- `Reset()` takes exactly seven cycles: three stack reads that decrement S as the chip does, then the reset vector at `$FFFC`. The interrupt-disable flag is set; on the 65C02 variants, decimal mode is cleared.
- `NOP` takes two cycles: one to fetch, one to read the next byte.
- `Step()` runs one instruction and returns the number of cycles it took.

Every cycle is one read or one write. A throwaway read is explicit in the code, because on real hardware it reaches the bus.

### Tests

Three tests, seven assertions passed:

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
