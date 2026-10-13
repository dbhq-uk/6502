---
title: "The Electron's mutation pass"
date: 2026-10-13
summary: "Task 15 of the Electron plan, its first two steps. Twenty-three faults were planted one at a time in a scratch copy, the plan's seventeen and six close variants, and the Electron's tests were run against each. Every one was caught; the thinnest catches are a ROM select fault that one test sees and the RTS dummy read that two see. The known differences were checked against the plan's list of twelve, and three things the model does without saying so were added: what absent devices read, the ROM latch at power on, and that BREAK resets only the 6502."
order: 40
---

# 13 October 2026: the Electron's mutation pass

Steps 1 and 2 of task 15 of the [Electron plan](../superpowers/plans/2026-10-04-electron.md): break one thing at a time and see which test notices, then check that `docs/known-differences.md` says everything the plan names. The branch is the one [the Electron counts](2026-10-13-the-electron-counts.md) left, at `34827aa`. Steps 3 and 4, the final review and the report to Dan, are not in this entry.

## How it was run

The faults are a script, [`tools/probes/electron_mutations.py`](../../tools/probes/electron_mutations.py), written in the style of the KIM-1 teletype's (`tools/probes/kim1_teletype_mutations.py` on that branch): each fault is one or more exact text replacements, each of which must match once, then `dotnet test tests/Dbhq.Machines.Electron.Tests -c Release` runs, the failed tests are collected by class and method, and the files are put back. The two keyboard faults also run `site/tests/electron.test.mjs`, because that test holds the page's copy of the key matrix against `ElectronKey.cs`. It ran in a scratch worktree outside the repository (`git worktree add ~/6502-ctl-elec-mut HEAD --detach`), so a fault left behind by a crash could not reach the branch; afterwards `git status` there showed nothing changed but the script itself.

## The plan's list against the code

The plan was written before the code, so each fault was mapped to the code as it is. Most mapped to one line. Where one did not, the closest real fault was taken, and where the plan's words fit more than one place, each place got its own fault:

- **The dummy read left out of `RTS`** is in the core (`Cpu.ControlFlow.cs`), not the Electron. The fault removes the last of `RTS`'s reads, the one at the pulled address before the increment. The core has its own tests; this run asks only whether the Electron's tests see it.
- **The window one boundary too narrow or too wide.** The window is the boundaries at positions 2 to 80 of the line, 40 of them (`ElectronTiming.WindowFirst` and `WindowLast`). The faults move the last one to 78 (39 boundaries) and 82 (41).
- **The RTC at 12,672.** Moved in both fields (M6, a pure shift of two cycles) and in the odd field alone (M6b).
- **A `$FE05` write of `$00` honoured while slot 10 is paged.** The acceptance rule covers slots 8 to 11, so the fault honours `$00` in all four.
- **Caps Lock and Func split, and both Shifts separate.** The code has one key for each pair, so there is nothing to split; the real fault is a second key added. It goes at one of the two positions the schematic leaves unconnected (column 0 bit 2 for Func, column 2 bit 3 for a right Shift), in `ElectronKey.cs` and in the page's copy in `site/public/electron-keys.js` together, with the PC's right Shift pressing the new key.
- **The keyboard's OR changed to AND** is two replacements: the value starts at `$0F` and each selected column is ANDed in.
- **The CRC stored low byte first** was planted twice: written and read so, consistently (M16), and written so but read high byte first (M16b).
- **The UEF's chunk length counted with its own header** likewise: by the writer and the reader together (M17), and by the reader alone (M17b).
- **A carrier count off by one** has three places a count is made: the writer's `&0110` chunks (M18), the reader's (M18b), and the cassette's recorder, which turns idle line into a count (M18c).

## What caught each fault

Run on 13 October 2026 on the shared aarch64 machine with `python3 tools/probes/electron_mutations.py` in the scratch worktree; the 23 faults took 19 minutes. The unbroken project passed 511 of 511 there first. "Failed" counts tests by class and method, so a theory with several failing cases counts once.

| Fault | What it does | Failed | Caught by, the most direct first |
|---|---|---|---|
| M1 | The cost parity flipped: 3 cycles from an even start, 2 from an odd | 19 | `ElectronTimingTests.TheCostRepeatsWithTheParityOfTheStart`, `ElectronTimingTests.AnAccessCostsWhatTheSheetSays`, `ForLoopAnchorTests.TheForLoopTakesWhatTheRealMachinesTook`, 12 `ContentionTests` |
| M2 | The dummy read left out of `RTS` | 2 | `ElectronTimingTests.RtsMakesSixAccessesAndTheThirdIsADummyReadOfTheStack`, `ForLoopAnchorTests.TheForLoopTakesWhatTheRealMachinesTook` |
| M3 | The window too narrow: 39 boundaries | 14 | `ContentionTests.APureRamStreamCompletesTheSheetsCountInAFrame`, `ContentionTests.TheHistogramOfAccessLengthsIsTheSheets`, both `ForLoopAnchorTests`, 10 more |
| M4 | The window too wide: 41 boundaries | 14 | The same 14 as M3 |
| M5 | Mode 3's two blank lines in each row contended | 6 | `ContentionTests.Mode3LeavesTheTwoBlankLinesOfEachRowFree`, both `ForLoopAnchorTests`, 3 more `ContentionTests` |
| M6 | The RTC at 12,672 and 52,672 | 5 | `UlaInterruptTests.TheRealTimeClockFiresAt12670And52670OfEachFrame` and four more `UlaInterruptTests` |
| M6b | The odd field's RTC alone at 12,672 | 5 | The same 5 as M6 |
| M7 | The two display ends 40,000 apart | 5 | `UlaInterruptTests.TheTwoDisplayEndsAreThirtyNineThousandNineHundredAndThirtySixApart_NotForty` and four more `UlaInterruptTests` |
| M8 | A `$FE05` write of `$00` honoured with slot 8 to 11 paged | 1 | `ElectronBusTests.TheSelectRegisterFollowsTheSheetsAcceptanceRule` |
| M9 | Caps Lock and Func split, a Func key of its own | 9 | `ElectronKeyboardTests.BothShiftsAreOneKeyAndCapsLockAndFuncAreOne`, `ElectronKeyboardTests.ThereIsOneKeyForEveryConnectedPositionInTheTable`, 3 more .NET tests, 4 site tests |
| M10 | Both Shifts separate, a right Shift of its own | 8 | The same 5 .NET tests as M9, 3 site tests |
| M11 | The keyboard's OR changed to AND | 39 | `ElectronKeyboardTests.TwoColumnsSelectedAtOnceReadAsTheOrOfBoth`, 7 more `ElectronKeyboardTests`, and everything that types: the acceptance tests, the boot, the tape loads |
| M12 | The palette's negative logic dropped | 18 | `UlaDisplayTests.TheOsPaletteForMode0IsBlackAndWhiteAndRedNeedsBothRegisters`, `UlaDisplayTests.TheOsPalettesForModes1And2DecodeToTheirColours`, 5 `DisplayPictureHashTests` |
| M13 | Mode 2's bit order swapped | 5 | `UlaDisplayTests.A4BitByteIsTwoPixelsFromBits7531And6420`, 3 `DisplayPictureHashTests`, `BootTests.EachModeTypedInBasicShowsThePromptAtTheTopLeft` |
| M14 | The sound's half period 16 x (S + 1) | 12 | `UlaSoundTests.WithSOf15TheOutputTogglesEvery512CyclesSoItsPitchIs1953Hz`, `UlaSoundTests.WithSOf255TheOutputIs122Hz`, 9 more `UlaSoundTests`, 1 `SoundTests` |
| M15 | The CRC's initial value `$FFFF` | 22 | `TapeCrcTests.TheStandardCheckValue`, `TapeCrcTests.TheWorkedBlockCrcs`, `TapeBlocksTests.EncodeGivesTheSheetsThirtyBytesExactly`, and the tests that hold the blocks the OS itself saved against `TapeCrc` |
| M16 | The CRC stored low byte first, written and read so | 18 | `TapeBlocksTests.EncodeGivesTheSheetsThirtyBytesExactly`, `TapeProbeTests.SaveWritesOneBlockInTheSheetsLayoutAndItsCrcsAreTheSheets`, and the tests that parse what the OS saved |
| M16b | The CRC written low byte first, read high byte first | 7 | `TapeBlocksTests.EncodeGivesTheSheetsThirtyBytesExactly`, 6 more `TapeBlocksTests` |
| M17 | The UEF chunk length counted with its header, written and read so | 35 | Most of `UefTests`, which build their files by hand from the specification, and the saved program loading back |
| M17b | The reader alone counting the header | 32 | The same, less three that read the writer's bytes directly |
| M18 | The writer one carrier cycle over in each `&0110` | 4 | `UefTests.TheWritersChunksAreOriginThenTargetMachineThenDataAndCarrierAsTheSheetLaysThemOut`, `UefTests.ARoundTripGivesTheEventsBack`, 2 more |
| M18b | The reader one carrier cycle over | 6 | `UefTests.DataChunksBecomeABytePerByteAndCarrierBecomesCarrier`, `UefTests.CarrierChunksRunTogetherAreOneCarrier`, 4 more |
| M18c | The recorder one carrier cycle over | 4 | `UlaTapeTests.IdleLineLongerThanABitTimeIsCarrierAndShorterIsNot`, `TapeRoundTripTests.TheCassetteRecordsWhatTheProbeRecorded`, 2 more |

The full list of failed tests for each fault is what the script prints; it is not copied here, because it can be made again.

## What it shows

**None survived**, so step 1 adds no test and no known difference. Three things are worth saying about the catches:

- **The thinnest is M8, with one test.** The acceptance rule is the only thing that keeps an interrupt clear (`$10`, `$20`, `$40`) from dropping BASIC to slot 0, and with the fault in, the boot, BASIC and the tape all still pass: nothing they do writes exactly `$00` there with BASIC paged in. One direct test is enough to catch it; it is noted because it is the one place a single test stands between a fault and the branch.
- **The consistent faults are the ones that matter, and they are caught by numbers from outside the code.** A CRC stored low byte first by both the writer and the reader (M16), or a chunk length counted with its header by both (M17), would pass any round trip. They are caught because `TapeBlocksTests` holds the 30 bytes of the sheet's worked block, written from `tape.md` and not from the encoder, and `UefTests` builds its files by hand from the UEF specification; M16 is caught again where the parser reads blocks the real OS saved, which the OS wrote high byte first.
- **The FOR-loop anchor catches the contention faults and `RTS` but not the interrupts.** M1 to M5 all fail `TheForLoopTakesWhatTheRealMachinesTook`, the seven real timings. M6, M6b and M7 do not: moving the RTC by two cycles or a display end by 64 does not move a BASIC loop of several seconds outside its tolerance. Those are held by the `UlaInterruptTests` that give each time to the cycle from `ula.md` section 5d.

## Step 2: the known differences

Each item the plan's step 2 names was looked for in `docs/known-differences.md`, and the code was read to check what the entry says:

| The plan's item | Where it is |
|---|---|
| The window's phase and the first pixel's offset | The contention, "The window's phase is a convention" |
| ULA registers and FRED and JIM at 1 MHz, uncontended | The contention, its second bullet |
| What an absent device returns | Was only the ULA's unreadable registers. **Added**: FRED, JIM and an empty slot too, all the high byte of the address |
| The power-on state of the ULA, and what BREAK clears | Transmit-empty, the mode and the power-on flag were there, and the display's, the sound's and the cassette's power-on states in their own sections. **Added**: the ROM latch at slot 0, and that BREAK resets the 6502 and leaves every ULA register, the latch and RAM as they were (`ElectronMachine.PressBreak` calls `Cpu.Reset` and nothing else) |
| The picture read a line at a time, and no fetch-to-pixel offset | The display, its first two bullets |
| The counters when the mode moves between 80 and 40 bytes a line mid-row | The display |
| The address counter in the blank lines of modes 3 and 6 | The display |
| The sound counter's reload, and the level on entering sound mode | The sound, its first two bullets |
| The tape at the ULA's serial register, with no tones, no phase and no 300 baud | The cassette, its "What" and first two bullets |
| The high-tone detector as a count | The cassette |
| Interrupts sampled at instruction boundaries | The ULA's interrupts |
| The Plus 1 slow-down | The contention, its last bullet |

The three additions are in the ULA's interrupts section, because they are the bus's and the machine's rather than the display's or the tape's.

## The browser check, measured

The counts entry said the browser check's Electron section on the deployed build was not yet measured, because CI had not run it. CI has now run it on `34827aa` (run 38433835321, green), and the figure is in [that entry](2026-10-13-the-electron-counts.md#ci): 27.1 seconds, from "electron: page opened" to the line after its images, as the 5 October figure was taken. From the same log: `SAVE` took 11.3 seconds of real time and `LOAD` 6.3, and the speed line said the browser could run the machine about 16 times as fast. Those are that run's numbers on GitHub's machine, not a property of the page.

## What was run

All on 13 October 2026, on this aarch64 machine, with other work on it.

- `dotnet test tests/Dbhq.Machines.Electron.Tests -c Release` in the scratch worktree before any fault: 511 passed, 0 failed, 47.9 seconds with the build.
- `python3 tools/probes/electron_mutations.py` in the scratch worktree: 23 faults, 23 caught, 0 survived.
- `gh run view 38433835321 --log` for the browser check's times.
