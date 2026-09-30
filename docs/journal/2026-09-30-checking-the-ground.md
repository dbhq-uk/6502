# 30 September 2026: checking the ground before the plan

Before writing the plan for the core, every outside fact it depends on was
checked, and every behaviour the core's code would assume was tested against
the data it will be tested against. The aim was a plan that carries
behaviour the data confirms, not behaviour remembered.

## Harte's test data

- **Five sets, one per variant:** `6502`, `nes6502`, `synertek65c02`,
  `rockwell65c02` and `wdc65c02`. Each has 256 files, one per opcode, and
  each file holds 10,000 cases. Each set is about a gigabyte of JSON, a
  little over 5 GB in all (GitHub contents API, 30 September).
- **Pinned to commit `2f6980a`.** Every file is checked against its git blob
  hash, which GitHub's tree API returns for all 1,280 files in one call, so
  the manifest can be built without downloading 5 GB first. A downloaded
  file's hash was checked against the tree's and they matched.
- **The WDC set has no data for `WAI` (`$CB`) and `STP` (`$DB`):** both files
  are empty, because neither instruction can be tested one step at a time.
  They get tests of our own.

## What the data confirmed

`python3 tools/probes/check_harte_findings.py` checks each behaviour below
against every case in the relevant files. On 30 September it passed all 39
checks, with no mismatches.

- **The unstable NMOS opcodes `ANE` and `LXA` use the magic constant `$EE`.**
  On real chips this constant varies from part to part; Harte's data fixes it
  at `$EE`, and the core will match the data and say so.
- **`SHA`, `SHX`, `SHY` and `TAS` store the register ANDed with one more
  than the high byte of the base address,** and on a page cross that value
  also replaces the high byte of the address written to.
- **Decimal mode matches Bruce Clark's description exactly,** for the NMOS
  chip and for all three 65C02s: result, carry, negative, overflow and zero.
  The NES's 2A03 ignores the decimal flag entirely.
- **`ARR`, `SBX`, `ANC` and `ALR`** behave as documented, including `ARR`'s
  own decimal-mode adjustment on the NMOS chip and its absence on the 2A03.
- **Status bits 4 and 5 have nothing behind them.** `PLP` and `RTI` load P
  with bit 4 cleared and bit 5 set; `PHP` pushes both set.
- **A `JAM` opcode, stepped once,** reads the opcode, the next byte, `$FFFF`,
  `$FFFE` twice, then `$FFFF` six times, and leaves PC one past the opcode.

## What the 65C02 does differently on the bus

The NMOS 6502 sometimes reads a half-computed address as a throwaway: the
right low byte with the wrong high byte. The 65C02 fixed that. The data
shows what it does instead:

- **Where the NMOS chip reads the half-computed address, the 65C02 re-reads
  the last byte of the instruction.** `LDA abs,X` crossing a page re-reads
  the operand's high byte; `LDA (zp),Y` crossing a page re-reads the
  zero-page operand.
- **A read-modify-write reads twice and writes once.** The NMOS chip writes
  the old value back before writing the new one, which matters on hardware
  registers.
- **`INC` and `DEC` with `abs,X` always take the extra cycle; `ASL`, `LSR`,
  `ROL` and `ROR` with `abs,X` take it only on a page cross.**
- **`JMP (abs)` reads the correct second byte across a page boundary,** after
  a throwaway read of the address the NMOS chip would have used.
- **Undefined opcodes are defined no-ops, and they differ by variant:**

| Opcode | WDC | Rockwell | Synertek |
|---|---|---|---|
| `$02` and friends | 2 bytes, 2 cycles | 2 bytes, 2 cycles | 2 bytes, 2 cycles |
| `$03` and the other `x3`, `xB` | 1 byte, 1 cycle | 1 byte, 1 cycle | 1 byte, 1 cycle |
| `$44` | 2 bytes, 3 cycles | 2 bytes, 3 cycles | 2 bytes, 3 cycles |
| `$54` | 2 bytes, 4 cycles | 2 bytes, 4 cycles | 2 bytes, 4 cycles |
| `$5C`, `$DC`, `$FC` | 3 bytes, 4 cycles | 3 bytes, 4 cycles | 3 bytes, 4 cycles |
| `$07` (`RMB0` where it exists) | 2 bytes, 5 cycles | 2 bytes, 5 cycles | 2 bytes, 3 cycles |
| `$0F` (`BBR0` where it exists) | a branch | a branch | 3 bytes, 3 cycles |
| `$CB` | `WAI`, no data | 1 byte, 2 cycles | 1 byte, 2 cycles |
| `$DB` | `STP`, no data | 2 bytes, 4 cycles | 2 bytes, 4 cycles |

These were sampled one opcode per group. The full sets decide the rest when
the core runs against them.

## One thing in the data that is not the chip

On all three 65C02s, `ADC` and `SBC` in decimal mode take one extra cycle,
and the data shows that cycle as a read. For the memory addressing modes it
re-reads the operand's address, which is plausible hardware behaviour. For
the immediate mode it reads a fixed address that depends on the variant and
the instruction: `$007F`, `$0059` or `$0056` for `ADC` on WDC, Rockwell and
Synertek, and `$0000` for `SBC` on all three, in every one of roughly 5,000
decimal cases each.

A fixed address that differs between chips and between two sibling
instructions looks like something in the program that generated the data,
not like the chip. Matching it would build an artefact into the emulator.
So the core re-reads the operand's address, as it does in every other mode,
and the test harness makes one narrow exception: for those two opcodes, on
those three variants, with the decimal flag set, it checks that the third
cycle exists and is a read, but not its address or value. The exception is
written up in [`docs/known-differences.md`](../known-differences.md).

## Klaus Dormann's tests

- **Only two of the programs the plan needs come assembled:** the functional
  test in its NMOS setup and the 65C02 extended test in its WDC setup. The
  exhaustive decimal test, the functional test with decimal mode off for the
  2A03, and the extended test in its Rockwell and Synertek setups exist only
  as assembly source.
- **They need Dormann's assembler, `as65`,** a 2007 build for 32-bit Linux.
  On a 64-bit machine it needs two packages, `libc6-i386` and
  `lib32stdc++6`, both standard Ubuntu packages. Both were installed on the
  build machine, and all three programs assembled.
- **The assembler's `-x` switch rewrites `JMP *` as the 65C02's `BRA`.** On
  an NMOS chip `$80` is a two-byte no-op, so the NMOS decimal test built with
  `-x` would never have stopped. Only 65C02 builds get `-x`.
- **The success address is read from the assembler's listing,** not typed in:
  the line that says "test passed". For the functional test it is `$3469`,
  the same as the prebuilt binary.
- **Decision: keep the assembler, on CI and Linux only,** chosen over
  dropping it and running the two prebuilt programs. Dropping it would have
  let the whole suite run on any machine with .NET, but it would have lost
  the only exhaustive decimal test and the whole-program runs on three of the
  five variants.

## `nestest`

- **Pinned to `christopherpow/nes-test-roms` at commit `95d8f62`,** rather
  than the original download site, because a git commit cannot change under
  us. That copy of `nestest.log` has LF line endings; the original's has
  CRLF, which accounts for the whole size difference between them.
- **The log is 8,991 lines.** Its last few instructions write to the NES
  sound registers, which is harmless when the test runs on a plain 64 KB
  memory with no sound chip.
- **It names undocumented opcodes the Nintendulator way,** with a leading
  `*` and `ISB` rather than `ISC`, so the disassembler uses those names.

## The transistor-level model

- **perfect6502, MIT licence, pinned to commit `09fc542`.** It simulates the
  Visual6502 netlist of the real chip, one half-cycle at a time.
- **Its IRQ and NMI inputs are netlist nodes 103 and 1297,** set with its
  public `setNode` function. A small harness of our own will drive them on
  chosen cycles and write out the bus log, which becomes the expected result
  for the interrupt tests.

## A name for the code

C# names cannot start with a digit, so the code cannot be called `6502`.
The namespace is `Dbhq.Cpu6502`, with the machines to follow as `Dbhq.Kim1`,
`Dbhq.BbcMicro` and `Dbhq.Nes`. Dan asked why not `Mos6502`, and the name
was confirmed over it and over `Dbhq.Mos6502` for two reasons:

- **It is the most crowded name in exactly this category.** On 30 September
  a GitHub search found 176 repositories with "mos6502" in the name, at
  least eight of them named exactly `mos6502`, and every one of those a 6502
  emulator, in C++, Rust, Lua, Python and Scala. One, in Rust, supports the
  same variants this core does.
- **It names one maker, and the core covers four.** MOS made the NMOS 6502;
  the 2A03 is Ricoh's, and the three 65C02s are WDC's, Rockwell's and
  Synertek's.

It is a code namespace rather than the product's name, which stays "6502",
so the clash would not have broken anything. The organisation prefix keeps
the code clear of it anyway.

The same conversation asked who created the 6502. The answer, checked
against its sources, is now in
[`docs/the-6502-family.md`](../the-6502-family.md#who-made-it).

## Proving the plan before writing it

The plan for the core was not written from the design and then hoped for.
Every file it contains was first built in a scratch copy and run against
every reference the design names, and only then written into the plan. The
plan's tasks were then replayed, in order, into an empty folder, building
with warnings as errors and running the whole suite after each one, so every
step of it is known to work.

On 30 September, with the plan's own tests run by `dotnet test`:

- **Harte:** all 1,278 files that have data, 12.78 million cases across the
  five variants, passed, in a little over two minutes.
- **Dormann:** all twelve builds passed, on every variant each applies to.
- **`nestest`:** all 8,991 lines matched on program counter, instruction bytes, mnemonic and undocumented mark, A, X, Y, P, SP and cycle count, and its error bytes ended at zero.
- **The transistor-level model:** all 134 interrupt runs matched, cycle for
  cycle.
- **Speed:** the benchmark's best run on the build machine was 98.7 MHz, 49
  times a 2 MHz BBC Micro, against a target of 25.

Three things turned up that the plan would otherwise have got wrong.

**The Synertek no-ops were sampled from the wrong rows.** The table in the
section above looked at `$07` and `$0F` and generalised. The full data showed
the odd rows differ: `$17` reads like `LDA zp,X` and `$1F` takes an extra
cycle. Sixteen files failed on the first run and passed after the fix, and
the table above is now known to hold for even rows only.

**The textbook interrupt rule was one cycle early.** The first version of the
interrupt code followed the usual description: an instruction takes an
interrupt that was active by the end of its second-to-last cycle. Against the
transistor-level model it disagreed on 29 of the first 105 runs. The model
shows an instruction taking an interrupt that became active by the end of its
last cycle, with the line changed at the start of a cycle; the likely reason
is that the usual description counts from a different point within the
cycle. The same runs showed exactly where `BRK` stops being taken over by an
NMI, and something the code had not expected at all: an NMI that arrives
while `BRK` reads the low byte of its vector is lost, never taken. Two new
families of runs were added to cover what the first set could not, a branch
that crosses a page and an NMI around a branch, and the corrected code
matches all 134.

**Two references disagree about Synertek.** Dormann's extended test, set up
to check the bit-instruction opcodes as no-ops, expects `$07` to be one byte
long. Harte's data says two, and the core matches Harte on all 10,000 cases.
Harte is the per-instruction authority here, so the Synertek build of
Dormann's test leaves those opcodes out, using the test's own setting for
that. It is recorded in [`docs/known-differences.md`](../known-differences.md).

One smaller thing: the assembler needed a second 32-bit package,
`lib32stdc++6`, beyond the `libc6-i386` it first asked for.

The plan is
[`docs/superpowers/plans/2026-09-30-stage-1-core.md`](../superpowers/plans/2026-09-30-stage-1-core.md).
