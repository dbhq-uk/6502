---
title: "The Atari VCS's mutation pass, its known differences and its issues"
date: 2026-10-14
summary: "The fifteenth task of the Atari VCS, its first three steps: the plan's list of mutations, each planted alone and run against every test; the one that got through and the test that now catches it; the known differences checked against the plan's list and against every guess the fact sheets left open; and a comment on each issue the page links."
order: 69
---

# 14 October 2026: the Atari 2600's mutation pass, known differences and issues

Steps 1 to 3 of task 15 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md), done on 15 October 2026 and dated 14 October with the rest of the build. Steps 4 and 5, the final review of the whole branch and the report to Dan, are the controller's. New: `tools/probes/atari-2600/mutation-pass.py` and one test in `WsyncHaltTests`. Changed: `docs/known-differences.md`.

## The mutation pass

The plan names twenty mutations. `mutation-pass.py` holds them, takes a copy of the committed head with `git archive` into a scratch directory outside the repository, adds the suite's files from the `.testdata` cache, then for each mutation plants it alone, builds, runs the whole Atari 2600 test project and puts the file back. Fourteen of the patterns are the earlier fault passes', named in the script; M4, M5, M13, M16 and M17 are new, because no earlier fault was the plan's.

Two of the plan's mutations needed a reading:

- **"A hotspot answering on its own access."** The plan's own rule, in task 2, was that a hotspot read returns the byte of the bank showing before it. `cartridge.md` reversed that for F8, F6 and F4 (the read returns its byte in the bank it selects, as the suite's `bank-hotspot-window` asserts) and kept the plan's rule for FA. So the mutation was run both ways, as M9a and M9b: each board answered from the bank its rule does not give.
- **"DEC WSYNC writing once."** The core is not touched (rule 2), so M5 drops a second WSYNC write on the cycle after the first, in the bus. The write that is lost is the second, since losing the first changes nothing: the second write alone still halts.

The run, on `01f3bc0`: `python3 -I tools/probes/atari-2600/mutation-pass.py /tmp/atari-task15-mutations`, 15 October 2026. The line numbers are in the unchanged file. "Caught by" names the first failing test of the test file that holds the mechanism, then every file that failed, with its count of failing cases.

| Mutation | Changed | Caught by |
|---|---|---|
| M1 the TIA mirror at `$0100` dropped | `Atari2600Bus.cs:177` | `Atari2600BusTests.Worked_example_2_the_tia_at_0100_to_017F_takes_writes_through_a6`; `Atari2600BusTests` (12), `SuiteSelfTests` (4), `Atari2600AcceptanceTests` (4) |
| M2 A12 decoded as A13 | `Atari2600Bus.cs:172` | `Atari2600BusTests.Peek_changes_nothing_anywhere`; thirteen files, `WsyncHaltTests` (154), `SuiteSelfTests` (108) and `Atari2600AcceptanceTests` (180) among them |
| M3 the floating bus driving all eight bits | `Atari2600Bus.cs:324` | `Atari2600BusTests.Section_4_a_collision_register_drives_d7_and_d6_over_the_residue`; `Atari2600BusTests` (8), `ControllersTests` (1), `SuiteSelfTests` (6), `Atari2600AcceptanceTests` (6) |
| M4 the WSYNC halt releasing one colour clock late | `Tia.cs:325` | `TiaLineTests.Section_5_halt_release_is_due_from_colour_clock_0_of_the_next_line`; `TiaLineTests` (3) |
| M5 DEC WSYNC writing once | `Atari2600Bus.cs:304` | **Got through** on the first run (below); now `WsyncHaltTests.Section_5_dec_wsync_across_a_line_end_its_second_write_halts_a_whole_line` (2) |
| M6 the split inside a cycle moved by one clock | `Atari2600Bus.cs:189` | `CycleOrderTests.The_bus_runs_all_three_colour_clocks_before_the_access`; `CycleOrderTests` (3), `Atari2600BusTests` (2), `SuiteSelfTests` (6), `SuiteRecordedTests` (9), `Atari2600AcceptanceTests` (14) |
| M7 the timer's count after zero held | `Riot6532.cs:129` | `Riot6532Tests.Worked_example_4_write_three_at_each_divider`; `Riot6532Tests` (8) |
| M8 the PA7 flag cleared by the wrong read (INTIM, not TIMINT) | `Riot6532.cs:170` and `175` | `Riot6532Tests.Section_8_only_a_timint_read_clears_the_edge_flag`; `Riot6532Tests` (9), `SuiteSelfTests` (4), `Atari2600AcceptanceTests` (4) |
| M9a an F8, F6 or F4 hotspot read answered from the bank before its own access | `Boards/BankedBoard.cs:51` | `BankedBoardTests.An_indexed_store_to_a_hotspot_reads_it_first`; `BankedBoardTests` (7), `BoardContractTests` (6), `SuperchipTests` (3), `SuiteRecordedTests` (3), `Atari2600AcceptanceTests` (2) |
| M9b an FA hotspot read answered from the bank its own access selects | `Boards/Fa.cs:63` | `FaTests.A_hotspot_read_returns_the_byte_of_the_bank_showing_before_it`; `FaTests` (2) |
| M10 the Superchip's dummy read not writing | `Boards/Superchip.cs:53` | `SuperchipTests.A_read_of_the_write_port_writes_the_bus_into_the_cell`; `SuperchipTests` (6), `SuiteRecordedTests` (3), `Atari2600AcceptanceTests` (2) |
| M11 FA's D0 gate inverted | `Boards/Fa.cs:61` | `FaTests.A_read_gates_on_the_rom_byte_and_not_on_the_bus`; `FaTests` (17), `SuiteRecordedTests` (3), `Atari2600AcceptanceTests` (2) |
| M12 the playfield's `PF0` bit order reversed | `TiaPlayfield.cs:67` | `TiaPlayfieldTests.Section_8_each_bit_draws_its_four_pixels_in_each_half`; `TiaPlayfieldTests` (12), `TiaPriorityTests` (1), `TiaMotionTests` (1), `PalReferenceGroupingTests` (1), `SuiteSelfTests` (6), `SuiteRecordedTests` (15), `Atari2600AcceptanceTests` (20) |
| M13 a `NUSIZ` copy spacing off by eight (the close copy at 24, not 16) | `TiaPositionCounter.cs:241` | `TiaPlayerTests.Section_9_a_copy_decoded_before_a_reset_rides_through_it`; `TiaPlayerTests` (11), `TiaMissileTests` (1), `SuiteSelfTests` (6), `SuiteRecordedTests` (3), `Atari2600AcceptanceTests` (8) |
| M14 `RESMP` not hiding the missile | `TiaMissile.cs:51` | `TiaPlayerTests.Section_11_resmp_hides_the_missile_and_releases_it_by_the_players_width`; `TiaPlayerTests` (3), `TiaCollisionTests` (1) |
| M15 the HMOVE comb left out | `Tia.cs:627` | `TiaMotionTests.Section_16_late_and_early_hmove_move_by_the_pulses_that_fall_in_a_blank`; `TiaMotionTests` (105), `BootTests` (3), `PagePathTests` (2), `SuiteSelfTests` (20), `SuiteRecordedTests` (35), `Atari2600AcceptanceTests` (56) |
| M16 one motion value's sign flipped (+1 read as -1) | `TiaMotion.cs:140` | `TiaMotionTests.Worked_example_the_ball_walked_from_pixel_3_by_each_value`; `TiaMotionTests` (3), `BootTests` (3), `PagePathTests` (2), `SuiteSelfTests` (10), `SuiteRecordedTests` (5), `Atari2600AcceptanceTests` (16) |
| M17 a collision latch wired to the wrong pair (P0-PF's latch set by P0-M1) | `Tia.cs:751` | `TiaCollisionTests.Section_14_each_latch_is_set_by_its_own_pair_and_no_other`; `TiaCollisionTests` (3), `SuiteSelfTests` (8), `Atari2600AcceptanceTests` (8) |
| M18 a polynomial counter's tap moved (the five-bit counter's from bit 2 to bit 3) | `TiaSound.cs:144` | `TiaSoundTests.Section_18_5_each_AUDC_value_gives_the_sheets_first_64_bits_and_period`; `TiaSoundTests` (16) |
| M19 the PAL palette used on NTSC | `Region.cs:54` | `TiaPaletteTests.Section_19_6_worked_examples`; `TiaPaletteTests` (7), `FrameBufferTests` (1), `BootTests` (2), `PagePathTests` (1), `Atari2600AcceptanceTests` (1) |
| M20 the fire latch ignoring VBLANK bit 6 | `Tia.cs:427` | `TiaInputTests.Section_20_2_each_latch_holds_its_own_press`; `TiaInputTests` (5), `ControllersTests` (1) |

The script prints the first few failing test names of every file; the table keeps one name for each mutation.

### The one that got through: DEC WSYNC writing once

On the first run M5 showed as caught, but only by `StateReportTests.Every_field_of_every_part_is_reported_or_skipped`: the mutation's new field in the bus was not in the state report. That is the state report's own check working, not a test of the behaviour. With the field reported and powered on as the bus's others are, M5 was run again alone (`... /tmp/atari-task15-m5 M5`) and **survived**: no test noticed that DEC WSYNC wrote WSYNC once.

The reason is in the rules. Both writes of `DEC WSYNC` strobe WSYNC (`bus.md` section 5); the first sets the latch, and the halt falls on the next read either way, so a lost second write changes nothing on the same line. It shows only when the two writes fall on two lines: the first on cycle 76, the line's last, and the second on cycle 1 of the next, after the line's start has released the first. Then the second write alone halts the next fetch for the rest of that line, and a bus that lost it would not halt at all. The existing test (`Section_5_dec_wsync_writes_twice_then_halts_and_costs_one_line`) and the suite's `cpu/rmw-wsync` time `DEC WSYNC` and `INC WSYNC` inside one line. No source states the two-line case; it follows from the sheet's two rules, and the test says so.

**A test, not a known difference.** `WsyncHaltTests.Section_5_dec_wsync_across_a_line_end_its_second_write_halts_a_whole_line` runs `STA WSYNC`, thirty-six `NOP`s and `DEC WSYNC`, so the `DEC`'s writes land on cycle 76 and on the next line's cycle 1, and checks that the next `NOP` takes the rest of that line. Shown in the scratch copy, both regions: without the mutation it passes; with it, it fails, "Expected: 77, Actual: 2". Then M5 was run again through the script on the commit that adds the test, and was caught by it, in both regions.

So, in the end, every mutation is caught by a test and none needed a known-differences entry.

## The known differences

Step 2 asks `docs/known-differences.md` to list, each with its cause and the test that shows it, the items below. Each was checked against the file.

| Item | Where it is | Test |
|---|---|---|
| The power-on RAM | The RIOT's section (the RAM clear) and the cartridge's (the Superchip and FA RAM clear) | `Riot6532Tests.Section_9_power_on`; `BoardContractTests` |
| The power-on TIA state | The bus's section (colour clock 0, every register 0) and the objects' section | `Atari2600BusTests`, and now named in the objects' section: `Atari2600BusTests.Power_on_again_puts_every_part_back` and `TiaLineTests.Power_on_puts_the_counter_the_frame_and_the_registers_back` |
| The power-on bank | The cartridge's section (the last bank) | `BoardContractTests` |
| The palette computed and televisions varying | The line's section, first bullet | `TiaPaletteTests` |
| The one console the suite's trusted results come from | The suite's section, first bullet | `SuiteSelfTests`; the page test checks the file still says it |
| Every recorded outcome that is not a pass | The suite's section | `SuiteRecordedTests` |
| SECAM | **Missing as its own item**: only "every SECAM build" not run, and SECAM's noise under the sound | **Added** to the cartridge and region section, with `RegionTests.The_two_regions_are_named` and `SuiteRunner` |
| The paddles at rest | The inputs' section (INPT0 to INPT3 read 0 for ever) | `TiaInputTests` |

The recorded outcomes were checked against `machines/atari-2600/suite-outcomes.json` by reading every entry that is not a pass: the four `merge-delivery` self-tests in both regions, and the pictures `harness/calibration` and `tia-render/colors` on PAL and the four `hmove-stuck` pictures in both regions. Each is in the suite's section with its cause. One line was added beside the one console: the suite's photographs of that console's screen are not compared (issue 162).

**Every `[guessing - verify]` the sheets left open.** A read-only search over the fact sheets, the machine's code and its tests found the tag on more than a hundred lines. Each was matched by subject against `known-differences.md` and the open items of the sheet it belongs to. One subject was in neither, and five were there only in part:

- **Not covered:** only an access with A12 high reaches the board, sourced from Atari's patent and a guess for FA, a CBS board (`cartridge.md` section 3). Added to the cartridge's section.
- **In part:** a reset that lands before a copy's decode loses that copy, and the ball's reset holds a main-copy decode (`tia.md` section 9.1); which line a missile released by RESMP first shows on (section 10, open item 6); the clocks of the comb latch's set and clear (open item 2); the cells of section 16's table at cycle 8 other than `$70`; and the audio step of the first divided pulse after an `AUDF` write (section 18.6). Each now has a bullet in its task's section, with the test that holds it. For the table at cycle 8, the test holds only the measured cell, and the bullet says so.
- **One more added though a sheet's open items had it:** the region switch at once with no restart (`region.md` section 5, open item 3), because it is a choice the model makes, with its test, `Atari2600BusTests.The_region_switch_takes_effect_at_once_and_changes_nothing_else`. The other subject found only in a sheet, why the suite's `midline-resp` picture is one row taller (`suite.md` section 11), is an observation about the suite, not a choice of the model's, and stays there.

Three things the search noticed and that were left alone: `BankedBoardTests` says Harte's tests settle the address an indexed store reads first, while `cartridge.md` section 11 still lists it as not verified; `tia.md` section 16 still tags the drawn-line merge as a guess for task 7, which task 7 settled (it does not hold for players, open item 12); and `suite.md` section 11 tags an item it also marks settled. The sheets are the record of what was known when they were written, and the later findings are in the journal and in the known differences.

## The issues

Each issue the page links has a comment saying where the page links it and that it stays open: in "What is not modelled", issues 106, 108, 124, 125, 127, 128, 126 (which the panel's note on loading a file links too) and 107, then each board not modelled, 131 to 155, and the 3D models, 129; in "The machine", the speed pass, 130; and in "How it is checked", the suite's captures, 162. The list was read from `site/src/lib/atari-2600.mjs` (`NOT_MODELLED`, `BOARDS`, `ELSEWHERE`), which the page and its test both read, and from `Atari2600Panel.astro`. Issue 98, the machine's epic, has a comment with the pull request and what is left under it: the 3D models (129), the speed pass (130) and the parts not modelled. None was closed.

## Commands

All on 15 October 2026, on the development box:

- `python3 -I tools/probes/atari-2600/mutation-pass.py /tmp/atari-task15-mutations`, then `... /tmp/atari-task15-m5 M5` on `01f3bc0`, and again on the commit with the new test.
- In the scratch copy, with the new test copied in: `dotnet test -c Release --no-build tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~WsyncHaltTests.Section_5_dec"`, without and with M5 planted.
- In the worktree: `dotnet build -c Release`, no warnings; `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests`, every test passed.
- `gh issue comment` on each issue above.
