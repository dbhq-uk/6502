---
title: "The NES's mutation pass, and the known differences checked"
date: 2026-10-06
summary: "Task 16 of the NES plan, its first two steps. Faults were planted one at a time in a scratch copy of the NES, the plan's list, the further ones the task named and some of the pass's own, and the whole NES test project was run after each. Almost all were caught by the project's own tests, and many by those alone, since the community ROMs miss the tables, the boards' details and the controllers. One was seen only by nestest's log on PAL and three by nothing; each of the four now has a test that fails on it. Then docs/known-differences.md was checked against the plan's list and the fact sheets' open guesses, and what it did not say was added."
order: 34
---

# 6 October 2026: the NES's mutation pass, and the known differences checked

Task 16 of the NES plan has five steps. This entry is the first two: the
mutation pass, and the check of `docs/known-differences.md`. The third, the
issues for what is left out, was done in task 15. The final review and the
report to Dan are the controller's.

## The mutation pass

**How.** One fault at a time, in a scratch copy outside the repository: `git
archive HEAD | tar -x -C /tmp/nes-mutation`, the repository's `.testdata`
linked in, and a throwaway git there to put the code back between faults. Each
fault was a single exact replacement, checked to match once, applied by a small
script; then the NES test project was built and run in two halves, the
project's own tests and the tests that run third-party or bundled ROMs:

```
cd /tmp/nes-mutation && git checkout -- . && <apply one fault>
dotnet build tests/Dbhq.Machines.Nes.Tests -c Release -v q
dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --no-build \
  --filter "FullyQualifiedName!~BlarggTests&FullyQualifiedName!~NesAcceptanceTests&FullyQualifiedName!~NestestOnTheBus&FullyQualifiedName!~BootTests"
dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --no-build \
  --filter "FullyQualifiedName~BlarggTests|FullyQualifiedName~NesAcceptanceTests|FullyQualifiedName~NestestOnTheBus|FullyQualifiedName~BootTests"
```

The first half is called "unit" below: 1,176 tests in about 6 seconds, the
chip tests written from the fact sheets. It includes `NesLoaderTests`, two of
whose tests compare the homebrew's boot frame with its recorded hash; only
faults 24a and 24b reached them, and both were caught by a `PpuBackgroundTests`
test too. The second half is called "ROM": 320 tests in about a minute, Blargg's
and the other community ROMs (`BlarggTests`, and `NesAcceptanceTests`, which
runs them again with the rest of the table), `nestest` on the bus, and the
homebrew's boot. Both halves ran for every fault, not only until one test
failed, so the table says which ROMs see each fault as well as which unit tests.

**When.** 6 October 2026, from 03:28 to 04:55 UTC, on the shared machine, one
fault after another. The one-minute load at the start and end of each fault's
run was between 0.47 and 27.40; it was over 6 only while faults 26 and 27a ran
(27.40 at 04:16 and 9.31 at 04:19), when another job took the machine for a
few minutes. The load changes how long a run takes, not what it finds, since
the machine is deterministic.

**The table.** Each fault is one change; the third column gives the line and
the change, or its location where the change is a value in a table, in
`src/Dbhq.Machines.Nes/` at commit `d70aedf`. "Unit" and "ROM" are the failing tests of each half. Where a theory
has a row per region or parameter, each row counts.

| # | Fault planted | Where, before, after | Unit | ROM | Unit tests that caught it | ROMs that caught it |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | The PAL accumulator's denominator 5 made 6 (NTSC, with remainder 0, unchanged) | `NesBus.cs:140` `_dotDenominator = region.DotsDenominator;`, `+ 1` added | 15 | 4 | `NesBusTests`: `ThePalDotsRunThreeThreeThreeThreeFour`, `EachReadOrWriteIsOneCycleAndTheDotsFollowTheRegionsRatio`, the NMI boundary rows; `OamDmaTests.ThePpuRunsThroughTheStallAtTheRegionsRatio` | `nestest` (PAL), the homebrew's boot (PAL) |
| 2a | The odd-frame skip on PAL too | `Ppu.cs:419` `_dropDot = _oddFrame && _oddFrameSkipsADot && RenderingEnabled;`, `_oddFrameSkipsADot` removed | 2 | 0 | `PpuTimingTests`: `TheDroppedDotIsDecidedWhenDot338OfThePreRenderLineRuns`, `AFrameIsLinesTimes341DotsLessOneOnAnOddNtscFrameWithRenderingOn` (both PAL) | none: no PPU timing ROM runs on PAL |
| 2b | The odd-frame skip with rendering off too | the same line, `&& RenderingEnabled` removed | 4 | 26 | `PpuTimingTests.WithRenderingOffEveryFrameIsWhole`, `TheDroppedDotIsDecided...`; `NesBusTests.WithRenderingOffThePpusFrameLineAndDotAreTheBussDotCount` | nine of the ten `ppu_vbl_nmi` singles (not 04) and the combined ROM, `cpu_interrupts_v2` 2, `mmc3_test_2` 4 |
| 3 | The VBlank flag set one dot early | `Ppu.cs:398` `if (_dot == 1)` made `if (_dot == (_line == VblankLine ? 0 : 1))` | 24 | 18 | `PpuTimingTests` (the flag's dot, suppression, the NMI output), `NesBusTests` (the NMI boundary, the read on the flag's dot) | `ppu_vbl_nmi` 02, 03, 06, 07, 10, `cpu_interrupts_v2` 2, `mmc3_test_2` 4 |
| 4 | The sprite overflow bug removed: each later sprite's Y checked straight | `PpuSprites.cs:72` `_evaluationM = (_evaluationM + 1) & 3;` removed | 4 | 4 | `PpuSpriteTests.WorkedExample6_TheOverflowBugsFalsePositive`, `...FalseNegative` | `sprite_overflow_tests` 3 and 4 |
| 5 | A pulse duty entry: 25 per cent one step late | `ApuChannels.cs:177` `[0x02, 0x06, 0x1E, 0xF9]` made `[0x02, 0x0C, 0x1E, 0xF9]` | 2 | 0 | `PulseTests.EachDutyStepsThroughItsEightOutputsOneStepEveryTimerPeriod` | none |
| 6 | The noise table's PAL entry 9, 236 made 254 | `Region.cs:84` | 1 | 0 | `RegionTests.TheNoisePeriodsAreApuSection7` | none |
| 7 | The frame counter's first PAL step, 8313 made 8314 | `Region.cs:86` | 1 | 0 | `RegionTests.TheFrameCounterListsAreTheRowsOfApuSection10` | none |
| 8a | The DMC rate for index 5 on NTSC, 254 made 252 | `Region.cs:67` | 1 | 4 | `RegionTests.TheDmcRatesAreApuSection8` | `apu_test` 8-dmc_rates and the combined ROM |
| 8b | The DMC rate for index 5 on PAL, 236 made 238 | `Region.cs:85` | 1 | 0 | `RegionTests.TheDmcRatesAreApuSection8` | none: no DMC rate ROM runs on PAL |
| 9 | MMC1's back-to-back write no longer ignored | `Mappers/Mmc1.cs:72` `_sinceWrite == 1` made `_sinceWrite == -1` | 4 | 0 | `Mmc1Tests`: `AWriteOnTheCycleAfterAWriteIsIgnored`, `ARunOfConsecutiveWritesTakesOnlyTheFirst`, `ARealIncOnTheCpuLoadsOneBitNotTwo`, the bus RMW test | none |
| 10a | MMC3's A12 filter 3 cycles made 2 | `Mappers/Mmc3.cs:54` `FilterCycles = 3` made `2` | 11 | 8 | `Mmc3Tests.TheFilterCountsARiseOnlyAfterA12HasBeenLowForThreeCycles`, four `Mmc3ScanlineCounterTests` | `mmc3_test_2` 2 and 4, `mmc3_irq_tests` 2 and 4 |
| 10b | MMC3's A12 filter made 4 | the same line, `4` | 3 | 0 | `Mmc3Tests.TheFilterCounts...`, `Mmc3ScanlineCounterTests.TheBoardIsToldTheBusCycleOfEachPpuAccess` | none |
| 11 | OAM DMA's extra cycle on the other parity (reads on puts, writes on gets) | `NesBus.cs:411` and `416`, `get` and `!get` swapped | 16 | 8 | `OamDmaTests` (513 and 514, the stall, no fetch in it), `DmcDmaTests.AFetchInsideOamDmaCostsTwoAndAtItsEndOneOrThree` | both `sprdma_and_dmc_dma` ROMs, `cpu_interrupts_v2` 4 and the combined ROM |
| 12 | The controller's read order: A with B, Select with Start, Up with Down, Left with Right | `Controller.cs:66` and `77`, `_reads` made `_reads ^ 1` | 31 | 0 | 13 `ControllerTests`, `OamDmaTests` and `DmcDmaTests` (the halt on a pad read) | none: no pinned ROM reads a pad |
| 13 | The PAL palette's emphasis swap removed | `Region.cs:88` `emphasisSwapsRedAndGreen: true` made `false` | 2 | 0 | `RegionTests.OnlyThePalPictureChipSwapsRedAndGreenInEmphasis`, `PpuBackgroundTests.ThePalPpuEmphasisesWithItsRedAndGreenBitsSwapped` | none |
| 14 | DMC DMA without its dummy cycle (2 or 3 stolen, not 3 or 4) | `NesBus.cs:434` `dmc++;` made `dmc = 3;` | 18 | 10 | seven `DmcDmaTests` | `dmc_dma_during_read4` `dma_2007_read`, `dma_2007_write`, `dma_4016_read`; both `sprdma_and_dmc_dma` |
| 15 | The `$4017` write delay's parity swapped | `Apu.cs:329` `? 3 : 4` made `? 4 : 3` | 26 | 4 | seven `FrameCounterTests` | `cpu_interrupts_v2` 4 and the combined ROM |
| 16a | The IRQ line taken at the end of the cycle (the sound unit and the board) | `NesBus.cs:342` `_cpu.Irq = irq;` made `_cpu.Irq = _apu.Irq \|\| (_mapperCanInterrupt && _mapper.Irq);` | 6 | 14 | `NesBusTests.TheFrameIrqReachesTheCpuInTheCycleAfterTheOneThatSetIt`, two `Mmc3ScanlineCounterTests` | `pal_apu_tests` and `blargg_apu_2005` 08.irq_timing, `cpu_interrupts_v2` 3 to 5, `mmc3_test_2` 4 |
| 16b | The NMI line taken at the end of the cycle | `NesBus.cs:341` `_cpu.Nmi = nmi;` made `_cpu.Nmi = _ppu.Nmi;` | 12 | 12 | `NesBusTests`: the NMI boundary, `EnablingNmiDuringVblankIsTakenAfterTheNextInstruction`, the read on the flag's dot | `ppu_vbl_nmi` 04 and 05, `cpu_interrupts_v2` 2 and 3 |
| 17 | Pulse 1's sweep negating like pulse 2's | `Apu.cs:95` `onesComplement: true` made `false` | 2 | 0 | `PulseTests.TheNegatesDifferPulseOneTakesOneMore` | none |
| 18 | The triangle's linear reload flag cleared with the control bit set | `ApuChannels.cs:451` to `454`, `if (!_control)` dropped | 4 | 2 | `TriangleTests.WithTheControlFlagSetTheLinearCounterReloadsEveryQuarterFrame`, `ALinearCounterOfZeroStopsTheSequencerAndTheOutputHoldsItsValue` | `apu_mixer` triangle |
| 19 | Sprites' left-edge clipping ignored | `Ppu.cs:802` `? 0 : 8` made `0` | 2 | 2 | `PpuSpriteTests.WithPpumaskBit2ClearSpritesAreHiddenInTheLeftmostEightPixels` | `sprite_hit_tests` 05.left_clip |
| 20 | Sprite 0 hit at x = 255 | `Ppu.cs:1005` `&& x != 255` removed | 2 | 4 | `PpuSpriteTests.WorkedExample5_NoHitAtColumn255` | `sprite_hit_tests` 06.right_edge, 11.edge_timing |
| 21a | The latch decay 600 ms made 60 ms | `Ppu.cs:258` `0.6` made `0.06` | 6 | 0 | six `PpuRegisterTests` (`TheLatchKeepsItsBitsForHalfASecond` and the refreshes) | none |
| 21b | The latch decay made 6 s | the same line, `6.0` | 5 | 4 | `PpuRegisterTests.ALatchBitNotRefreshedForMoreThanTheDecayTimeReadsZero` and three more | `ppu_open_bus` (both regions) |
| 22 | MMC3's PRG bank mode inverted | `Mappers/Mmc3.cs:204` `!= 0` made `== 0` | 10 | 0 | seven `Mmc3Tests` | none |
| 23 | UxROM's fixed bank the second-last | `Mappers/Uxrom.cs:51` `Prg16Count - 1` made `- 2` | 6 | 0 | three `UxromTests` | none |
| 24a | One attribute shift: coarse Y bit 0 in place of bit 1 | `PpuBackground.cs:89` `(_v >> 4) & 4` made `(_v >> 3) & 4` | 4 | 5 | `PpuBackgroundTests.CoarseYScrollRunsOnIntoTheNametableBelowAfterRow29`, the boot hash in `NesLoaderTests` | the homebrew's boot (both regions) |
| 24b | One attribute shift: coarse X bit 0 in place of bit 1 | the same line, `_v & 2` made `(_v << 1) & 2` | 56 | 5 | 13 `PpuBackgroundTests`, the boot hash | the homebrew's boot |
| 25 | `$2007` read with no buffer delay | `Ppu.cs:484` and `485` swapped | 5 | 16 | `PpuRegisterTests.WorkedExample3_TheReadBufferDelays...` and three more in `PpuRegisterTests` and `NesBusTests` | `ppu_read_buffer`, `cpu_dummy_writes_ppumem`, three `dmc_dma_during_read4` ROMs, and `double_2007_read` no longer fails as written down |
| 26 | PRG RAM cleared by the reset button | after `NesBus.cs:281`, `_mapper.ClearPrgRam();` added | 8 | 6 | `NesBusTests.ResetKeepsRamAndPrgRamAndTheCpuResetsAgain`, the six battery rows, `Mmc1Tests` (power on and reset) | `NesAcceptanceTests`' PRG RAM rule, all six mappers |
| 27a | 8x16 sprites' table from PPUCTRL bit 3, not tile bit 0 | `PpuSprites.cs:186` `(_fetchTile & 1) << 12` made `(_ctrl & 0x08) << 9` | 4 | 0 | `PpuSpriteTests.In8By16ModeBit0OfTheTileChoosesTheTableAndAVerticalFlipSwapsTheHalves` | none |
| 27b | 8x16 sprites' tile bit 0 not cleared | `PpuSprites.cs:185` `(_fetchTile & 0xFE)` made `_fetchTile` | 4 | 0 | the same test | none |
| 28 | The power-on dot phase moved (a faulty fault: see below) | `NesBus.cs:253` `_dotAccumulator = 0;` made `= 2;` | 9 | 4 | `NesBusTests` (NTSC rows) | `nestest` (both) |
| 28b | The PAL power-on dot phase moved, NTSC untouched | the same line, `= _dotDenominator > 2 ? 2 : 0;` | 0 | 2 | **none** | `nestest` (PAL) only |
| 28c | Power on leaving the dot accumulator where it was | the same line removed | 0 | 0 | **none** (run with the new test in: it alone failed) | none |
| 29 | One dot before the access, not two | `NesBus.cs:102` `DotsBeforeAccess = 2` made `1` | 2 | 16 | `NesBusTests.AStatusReadOnTheDotTheFlagIsSetOrTheNextReadsItAndStopsTheNmi` | `ppu_vbl_nmi` 05 to 08, `cpu_interrupts_v2` 2, `mmc3_test_2` 4 |
| 30 | The 2A07's DMC DMA repeating the halted read | `Region.cs:89` `dmcDmaRepeatsHaltedRead: false` made `true` | 2 | 0 | `DmcDmaTests`' two "on NTSC only" tests, PAL rows | none |
| 31 | Power-on RAM `$FF`, not `$00` | `NesBus.cs:249` `Array.Clear(_ram);` made `Array.Fill(_ram, (byte)0xFF);` | 32 | 4 | `NesBusTests.PowerOnClearsRam...`, and four more `NesBusTests` and `OamDmaTests` tests whose programs ran differently with RAM at `$FF` | `nestest` (both) |
| 32 | The odd-frame drop decided at dot 337 | `Ppu.cs:99` `338` made `337` | 1 | 4 | `PpuTimingTests.TheDroppedDotIsDecidedWhenDot338OfThePreRenderLineRuns` | `ppu_vbl_nmi` 10 and the combined ROM |
| 33 | MMC3 as the other (NEC) revision | `Mappers/Mmc3.cs:165` to `175`, the IRQ only on a count down to 0 or a requested reload | 14 | 8 | `Mmc3Tests` (both latch 0 tests), five `Mmc3ScanlineCounterTests` | `mmc3_test_2` 5, `mmc3_irq_tests` 6; and the two known failures stop failing as written |
| 34 | `$4017` with the inhibit bit not clearing the frame IRQ flag | `Apu.cs:323` to `326` removed | 2 | 54 | `FrameCounterTests.SettingTheInhibitBitClearsTheFlagAndKeepsItClear` | the APU's IRQ and timing ROMs in all three suites, `cpu_interrupts_v2`, every MMC3 IRQ ROM, `instr_test-v5` 16-special |
| 35 | Noise mode 1 tapping bit 5 | `ApuChannels.cs:528` `? 6 : 1` made `? 5 : 1` | 2 | 0 | `NoiseTests.ModeOneFromOneGivesTheSheetsSequence` | none |
| 36 | OAM attribute bits 4 to 2 kept | `Ppu.cs:531` the `& 0xE3` dropped | 12 | 8 | `PpuRegisterTests.Oam2003And2004...`, `OamDmaTests`, `DmcDmaTests` | `oam_stress`, `ppu_open_bus` (both regions) |
| 37 | The DMC load's delay parity swapped | `ApuChannels.cs:719` `? 3 : 4` made `? 4 : 3` | 10 | 8 | three `DmcDmaTests` | `dma_2007_read`, `dma_4016_read`, both `sprdma_and_dmc_dma` |
| 38 | A DMC reload halting from the put after next | `ApuChannels.cs:795` `cycle + 2` made `cycle + 4` | 0 | 0 | **none** | none |
| 39 | The length table's entry 14, 26 made 24 | `ApuChannels.cs:22` | 4 | 8 | `PulseTests.EveryLengthTableEntryLoads` | `len_table` in `apu_test`, `pal_apu_tests` and `blargg_apu_2005` |
| 40 | A `$2002` read one dot before VBlank no longer suppressing it | `Ppu.cs:457` `true` made `false` | 2 | 6 | `PpuTimingTests.AStatusReadOneDotBeforeTheFlagIsSet...` | `ppu_vbl_nmi` 02, 06 and the combined ROM |
| 41 | The sprite priority bit ignored | `Ppu.cs:1011` `\|\| true` added | 4 | 0 | two `PpuSpriteTests` on priority | none |
| 42 | A `$4015` read clearing the DMC's IRQ flag too | after `Apu.cs:255`, a call that clears it | 2 | 4 | `DmcTests.ASampleThatEndsWithIrqEnabledSetsTheFlagAndHoldsTheLine` | `apu_test` 7-dmc_basics and the combined ROM |
| 43 | Writing 0 to `$4016` with the strobe low reloading the pad | `Controller.cs:40` `high \|\| _strobe` made `high \|\| !high` | 2 | 0 | `ControllerTests.AWriteOfZeroToTheStrobeWhileItIsAlreadyLowDoesNotReload` | none |
| 44 | OAMADDR not cleared on dots 257 to 320 | `Ppu.cs:964` removed | 2 | 0 | `PpuSpriteTests.DuringRenderingOamaddrIsClearedOnDots257To320...` | none |
| 45 | MMC1's PRG register bit 4 not switching PRG RAM off | `Mappers/Mmc1.cs:159` made `PrgRamEnabled = true;` | 1 | 0 | `Mmc1Tests.ThePrgRamIsAtSixThousandAndTheBankRegisterCanDisableIt` | none |
| 46 | CNROM's bus conflict removed | `Mappers/Cnrom.cs:39` made `SetChr8(value);` | 2 | 0 | two `CnromTests` | none |
| 47 | A `$4015` read putting its value on the CPU's open bus | `NesBus.cs:457` the value kept in `_openBus` | 0 | 0 | **none** | none |

Rows 1 to 13 are the plan's list; 14 to 27 the further ones the task named; 28
to 47 this pass's own, chosen at the seams the others do not reach: the power-on
and reset state, the order inside a cycle, the PPU's odd-frame and suppression
dots, the DMA's halts, and the boards' smaller rules.

**Fault 28 was a faulty fault.** Starting the accumulator at 2 was meant to move
PAL's fourth dot, but on NTSC the accumulator's denominator is 1 and the code
counts on it staying under that, so it broke NTSC too, which is what the unit
tests caught. 28b is the fault as it was meant, PAL only.

**The totals.** Of the 56 faults, 52 were caught by the unit tests: 51, and
fault 28, the faulty one above. One more, 28b, was caught only by a ROM, and
three by nothing.

**Four were not caught by the unit tests.**

- **28b, the PAL power-on phase,** was seen only by `nestest`'s log on PAL. The
  unit test for the 3, 3, 3, 3, 4 pattern runs on a bus that was never powered
  on, whose accumulator starts at zero because a new field does, so it never
  saw `PowerOn` set it. `known-differences.md` said the tests check the phase,
  which was true only through `nestest`.
- **28c, a second power on keeping the old phase,** was seen by nothing: each
  ROM runs on a new machine, and every power-on test powers on once.
- **38, a DMC reload halting two cycles later,** was seen by nothing. The ROMs
  synchronise themselves to the DMC and test the cost and the parity, as the
  known differences already said, and the unit tests measured the cost and the
  parity too, never the delay.
- **47, a `$4015` read changing the CPU's open bus,** was seen by nothing. The
  bus's comment and `bus.md` 3 both say that read leaves the bus alone, and no
  test read the bus straight after it.

**What was done about them.** Each now has a test, in the project, that fails
on its fault and passes on the code as it stands (checked both ways, the new
tests alone, against each fault):

| Fault | New test | On the fault |
| --- | --- | --- |
| 28b, 28c | `NesBusTests.PowerOnPutsThePalFourthDotInEveryFifthCycleCountedFromPowerOn` | fails |
| 38 | `DmcDmaTests.AReloadHaltsOnTheNextPutAfterTheOutputClockThatEmptiedTheBuffer` (both regions) | fails on both |
| 47 | `NesBusTests.AReadOf4015LeavesTheOpenBusAsItWas` (both regions) | fails on both |

The test for 38 pins the model's choice, not the chip: when in its APU cycle
the hardware schedules a reload is not on the sheet, and the entry in
`known-differences.md` now says the test holds the choice. It watches the DMC's
level, which moves on every output clock with bytes of `$AA`, to find the clock
that ended a byte, and checks the halt is on the next put, two cycles later.

One more test came from the known-differences check below, not from a fault:
`NesBusTests.TheFirstVblankFlagIsSetInTheCycleThatRunsLine241Dot1`, which holds
the "cycle 27395" that `known-differences.md` gave with nothing behind it, and
PAL's 25683. It also fails on fault 3, the flag a dot early.

**One fault was caught by a community ROM alone,** 28b, by `nestest`'s log on
PAL, and none by a Blargg ROM alone. The other way round is the more telling:
22 of the 56 faults (2a, 5, 6, 7, 8b, 9, 10b, 12, 13, 17, 21a, 22, 23, 27a, 27b,
30, 35, 41, 43, 44, 45, 46) were caught by the project's own tests and by no
ROM. The ROMs do not read the pads, do not run PPU timing on PAL, do not reach the PAL tables, and do not test the boards'
smaller rules; for those the chip tests from the fact sheets are the only
guard. Several of those are held by a single test: the PAL tables only by
`RegionTests`' comparison with the sheet, the odd-frame decision dot only by
`TheDroppedDotIsDecidedWhenDot338OfThePreRenderLineRuns` and `ppu_vbl_nmi` 10.

**A finding about the code: none.** No fault survived because the code was
wrong in the same way, and the pass found no defect in the machine. The pass
changed no line in `src/`; the final fix wave later corrected comments there,
below.

## The known differences, checked

`docs/known-differences.md` was read against the list the plan's task gives
and the list the controller's brief added, and every `[guessing - verify]` left
in `docs/nes/facts/` was followed to an entry or a stated resolution
(`grep -rn -i "guessing" docs/nes/facts/*.md`: 24 lines when the check ran,
23 once `cartridge.md`'s settled open item lost its tag).

**Already there, with a cause and a test or ROM:** RAM at power on and the
PPU's power-on alignment; the PAL fourth dot's power-on phase; the latch's one
decay time and `ppu_open_bus`; PAL using the NTSC colours; every community
known failure with its entry (the two `$AB` ROMs on both regions and the LXA
decision, `double_2007_read`, the two MMC3 ROMs of the other revision); the
MMC3 revision chosen; the 2A07's DMC DMA guess; the `$4017` parity; MMC1's and
the other boards' reset; UxROM's and AxROM's submapper 2 and CNROM's AND.

**Added**, each with its cause and the test or ROM that shows it:

- **The reset button leaves the PAL phase where it was,** in the bus section:
  the PPU goes back to line 0 dot 0 and the accumulator does not, which a test
  uses on purpose to move the cycles against the dots.
- **On PAL the flags clear at dot 1 of line 311,** implied by 70 lines of VBlank
  and not confirmed (`timing.md` open item 1), the one sheet guess with no
  entry.
- **The colours were never compared with a real PAL television** or a capture
  of one, and which tests do hold them.
- **Not run, and why:** `nmi_sync` (the readme says to look at the picture,
  and no frame check was written) and `dmc_tests` (a sound to listen to, no
  readme, no source).
- **Every known failure in one place,** pointing to its entry.
- **A new section, the cartridge file and the page:** the iNES TV system bit
  not read, the Dendy refused, the 4 MB file cap and the 64 KB RAM caps, and the
  picture shown whole, 256 by 240, in the region's pixel shape (8:7 on NTSC).
- **The open bus's `$4015` exception now names its test,** and the bus section
  no longer says there are no bus conflicts at all, since the boards have their
  AND.

**Hand-typed figures tied to a test or a dated command** (AGENTS.md rule 5): the
first VBlank's cycle 27395 to the new test; the sound unit's "72 dB or more"
and the DMC section's "89.6 dB" to what `ResamplerTests` holds, more than 65 dB,
with the figures it printed this morning quoted with the date and the command;
the mixer's "within 1e-7" corrected to the 1e-6 that `MixerTests` holds; and
the 8-cycle mean's "under 0.25 dB at 20 kHz" replaced by the formula it follows
from. Four comments in the source carried the same figures: `ApuMixer.cs` (the
1e-7), `TriangleChannel` in `ApuChannels.cs` (the 72 dB), and `SampleBuffer.cs`
(the 0.25 dB, and the -55 and -72 dB of the kernel's phases). In the final fix
wave of 6 October 2026 each was put right the same way: the line a test holds,
or the dated task 9 run and its command, or the formula. In the same wave the
sample buffer's exception notes were corrected to the rule its code applies, a
sample rate no higher than an eighth of the CPU clock.

**Every guess in the fact sheets has an entry or a resolution.** Of the 23
lines, one is the README's definition of the tag. The rest: `cartridge.md`'s
"which ROMs run on PAL" was settled in task 12 by each ROM's readme or source
(the journal entry of 5 October), and its open item now says so; `ppu.md`'s
rendering toggle delay, the evaluation's start at sprite 0 and greyscale have
entries; `apu.md`'s PAL pulse
under 8 has one; `timing.md`'s first VBlank, the PAL fourth dot and now the PAL
clear have them; `bus.md`'s 2A07 DMA has one; and every `mappers.md` guess
(MMC1's PRG RAM, CNROM, the boards' reset, MMC3's power on, the bus after
rendering, the pre-render line's second clock) has one.

## The issues

The parts left out (task 16 step 3) were filed in task 15 as issues 61 to 69,
and the NES page links each from its list: the Dendy (#61), the Famicom Disk
System (#62), expansion audio (#63), the Zapper and other peripherals (#64),
unlicensed mappers (#65), the battery-RAM save (#66), the picture's analogue
quirks (#69); the speed (#67) and the 3D models (#68, which names the branch
`feat/nes-models`) are linked from the page's text. None is missing.
