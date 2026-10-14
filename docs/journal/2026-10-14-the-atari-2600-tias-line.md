---
title: "The Atari VCS's TIA: the line, the playfield, the palette and the frame"
date: 2026-10-14
summary: "The fifth task of the Atari VCS: the TIA's horizontal counter built as the chip's own polynomial counter, RSYNC, VSYNC and VBLANK, the playfield, a palette computed from each region's colour signal, and the frame. The plan's rule for the order inside a processor cycle ended in a tie, which went to the order the sources state, and the reason for the tie is written down."
order: 58
---

# 14 October 2026: the Atari 2600's TIA, the line, the playfield, the palette and the frame

Task 5 of [the Atari 2600 plan](../superpowers/plans/2026-10-14-atari-2600.md): the TIA's line, built from [`tia.md`](../atari-2600/facts/tia.md) sections 3 to 8 and 19 and [`region.md`](../atari-2600/facts/region.md). New: `TiaPlayfield`, `TiaPalette` and `FrameBuffer`. Changed: `Tia` (the line), `Region` (its palette) and `Atari2600Bus` (the order inside a cycle).

## What was built

- **The horizontal sync counter is the chip's.** A six-bit polynomial counter, stepped once every four colour clocks, whose next left bit is 1 when its two rightmost bits are equal. Its states are decoded as Towers's table gives them, and each decode acts on the next step, four clocks later: RHB (count 16) ends the blank at colour clock 68, and SHB (count 56) resets the counter to 000000 at 228, which starts the next line with the blank on. The decode of LRHB (count 18) is there for task 7's HMOVE. A test walks the counter through Towers's states at each decode, and checks that the rule visits 63 states.
- **The beam.** `HorizontalCounter` is the colour clock of the line, 0 to 227, and pixel x is colour clock 68 + x. It equals the count times four plus the two-phase clock's place, except in the three clocks between an RSYNC and the line it starts.
- **RSYNC** restarts the two-phase clock and resets the counter, so the line starts again with the blank on, and the playfield, which is decoded from the same counter, starts again from its pixel 0.
- **VSYNC and VBLANK.** The end of VSYNC ends the frame. VBLANK's D1 makes the output black and the counters keep counting.
- **The playfield**: twenty bits a half line in the sheet's bit order, the right half repeated or reflected by CTRLPF D0. A cell's bit is fetched on the third clock of the count before it and shown on its first clock, so a mid-line write changes only the cells not yet read.
- **A pixel** is black in the blank or under VBLANK, otherwise COLUPF where the playfield is lit and COLUBK where it is not. Score mode and priority are task 6's.
- **The palette** is computed from each region's colour signal by the sheet's formula (Eric Ball's conversion, `tia.md` section 19.4), with the NTSC hue angles at 360/14 degrees a step and the PAL angles as section 19.5 fits them. The formula is in `TiaPalette.cs` and in the sheet. It was checked against the sheet's worked examples and properties, which `tools/probes/atari-2600/tia_palette.py` prints from the same formulas, and against no emulator's table. `Region.Palette` holds each region's palette, built from a function of the hue, so no code asks a region its name.
- **The frame**: two buffers of 160 colour numbers by 400 lines, one drawn while the other shows the last frame finished. `ToRgba` gives a region's window through its palette, black below the frame. `Tick` allocates nothing.
- **The frame count moved from `Tia` to `FrameBuffer`** (`Frames` and `Lines`), as task 4 left room for; the machine, the bus's observer and the task 4 tests read it there.

## The order inside a cycle

The plan fixed the rule before running ("Fixed before running", item 8): of the four splits of a cycle's three colour clocks around its access, keep the one that passes the most trusted `tia-timing/` self-tests; a tie goes to the split the sources state. `suite.md` had already found that only `wsync` and `wsync-line-end` can pass at this task, and that a tie was likely.

To make the split one constant, the bus now samples RDY at the access, after the cycle's clocks before it, instead of asking before the cycle whether the next clock starts a line. The two agree for task 4's split, and only the new one survives RSYNC, which can put a line's start in the middle of a cycle. A held read is still a whole cycle through `Cycle`, the only place a cycle is counted.

`python3 -I tools/probes/atari-2600/cycle-split.py <scratch>` built each split in a scratch copy on 14 October 2026 and ran every trusted suite row and `CycleOrderTests`:

| Clocks before the access | `wsync` (NTSC, PAL) | `wsync-line-end` (NTSC, PAL) | The other trusted rows | COLUBK on cycle 53 from power on at pixel 91 | The same after `CLEAN_START` |
|---|---|---|---|---|---|
| 0 | pass, pass | pass, pass | all pass | yes | yes |
| 1 | pass, pass | pass, pass | all pass | no | yes |
| 2 | pass, pass | pass, pass | all pass | no | yes |
| 3 | pass, pass | pass, pass | all pass | yes | yes |

A four-way tie, so the tie rule kept **all three colour clocks before the access**, the end of the cycle the SPG, Towers and the suite's headers state. The halt releases on colour clock 0 of the next line, after a write that lands there. The table shows why the tie was certain. The split only moves the accesses against the colour clocks by a constant, which is the same as another power-on phase: split 0 is split 3 with the cycles counted one later. After `CLEAN_START`'s two RSYNC strobes every split behaves alike, because RSYNC restarts the line at a fixed distance from its write. So the split can be seen only in a program that never strobes RSYNC, and then only together with the TIA's power-on phase. `tia.md` section 4.1 has the table.

## Decisions

- **RSYNC restarts the line three colour clocks after its write**, chosen over four. Towers gives "a full H@1-H@2 cycle" after the strobe, four clocks, and says it "requires more investigation". With four, every write in a program that runs `CLEAN_START` would land one colour clock before where the suite's headers put it (W = 3k). `tia-timing/midline-color`, which runs `CLEAN_START`, states its pulse at x 91 for a write on cycle 53. So the model counts the four clocks from one clock before the write lands, where the strobe is seen while phase two is high. `[guessing - verify]`; task 6's `rsync` test bears on it.
- **RSYNC's line start releases a pending WSYNC**, as every line start does; no source says either way.
- **A frame's row 0 is the line VSYNC ends on**, over the line after it. That line is the first after the three VSYNC lines, which is where the suite's README starts its pictures. A 262-line program then makes 262-row frames, which is what the page's line count needs. A frame that ends mid-line carries the part of the line already drawn into the next frame's row 0.
- **A new row is cleared to black**, so a line RSYNC cuts short shows nothing of a frame two frames old.
- **The playfield cell reads its bit two clocks before its first pixel**, so a write landing exactly then shows. The suite's picture fixes the clocks either side and not that one.
- **CTRLPF's reflect bit is read with each cell's bit**, over once a line. No source says.
- **VBLANK's gate acts a colour clock after the write**, from the suite's recorded `midline-vblank` picture as `tia.md` section 4 reads it.
- **The line limit is the plan's 400 lines** (item 2), `FrameBuffer.MaxLines`, and `Tia.MaxLines` is the same constant.
- **The NTSC hue step is 360/14 degrees**, over the design figure of 24, as `tia.md` section 19.3 chose, so NTSC shows 120 colours and PAL 104.

`docs/known-differences.md` has each choice that a television or a console could show differently, and that the colours a television shows vary.

## The suite

No suite test is claimed to pass here that the plan does not give this task. The trusted rows of task 4 still pass in both regions, `wsync` and `wsync-line-end` among them. `tia-timing/rsync` joins the "not yet" list for task 6: it fails at CODE `$02`, OBSERVED `$00`, EXPECTED `$80` in both regions, its second check, because its missile and the M0-PF latch are task 6's.

`CycleOrderTests` also runs the suite's `tia-timing/midline-color` for four frames and reads the pulse's columns from the frame: in both regions every line's pulse is nine pixels, the first at x 91 and each 6 further left to 25, then 91 again, as the test's header states. That is a reading of the header's stated picture to check the order and the RSYNC choice, not the screenshot comparison, which task 7 builds and task 11 records.

The outcomes were read on 14 October 2026 from `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests --filter "FullyQualifiedName~SuiteSelfTests" --logger "console;verbosity=detailed"`.

## The tests, test first

The test files were written first, and the first build failed to compile, naming `Tia.Frame`, `FrameBuffer`, `Region.Palette`, `TiaPalette`, `Tia.PolynomialCounter`, `Tia.HorizontalBlank`, `Tia.NextState` and `Atari2600Bus.ClocksBeforeAccess` as missing. Two helper mistakes showed on the first run and were the tests' own: a line helper that returned at once from power on, and a VSYNC test that wrote `$FD`, whose D1 is clear. The files: `TiaLineTests` (the line, the counter's states, the blank, WSYNC's release, RSYNC, VSYNC, VBLANK and its delay, a colour write, the line limit and short frames, power on), `TiaPlayfieldTests` (every cell in both halves, repeated and reflected, the sheet's worked examples, the read clock either side of a cell, the reflect bit mid-line), `TiaPaletteTests` (the sheet's thirteen worked colours and its properties, both regions), `FrameBufferTests` (frames and lines through the machine, the window through the palette, black below a short frame) and `CycleOrderTests`.

On 14 October 2026 `dotnet test -c Release tests/Dbhq.Machines.Atari2600.Tests` passed 643 test cases, and `dotnet build -c Release` of the solution gave no warnings.

## Every test file shown able to fail

The fault pass ran on 14 October 2026 in a scratch copy outside the repository (`python3 -I tools/probes/atari-2600/tia-line-faults.py <scratch>`), one fault at a time, building and running the whole Atari 2600 test project. The figure in brackets is the number of failing test cases in each file.

| Fault | Planted | Test files that failed |
|---|---|---|
| L1 | the blank ends on LRHB, at 76 | `FrameBufferTests` (12), `TiaLineTests` (7), `TiaPlayfieldTests` (11) |
| L2 | the line ends a count early | `Atari2600BusTests` (1), `CycleOrderTests` (4), `FrameBufferTests` (12), `ObserverTests` (2), `SuiteSelfTests` (10), `TiaLineTests` (13), `TiaPlayfieldTests` (8), `WsyncHaltTests` (160) |
| L3 | RSYNC restarts the line four clocks after the write | `CycleOrderTests` (4), `TiaLineTests` (3) |
| L4 | RSYNC does nothing | `CycleOrderTests` (4), `TiaLineTests` (3) |
| L5 | WSYNC released as the counter wraps, before a write landing at 228 | `CycleOrderTests` (6), `ObserverTests` (1), `TiaLineTests` (3), `WsyncHaltTests` (160) |
| L6 | VBLANK's gate with no clock of delay | `TiaLineTests` (1) |
| L7 | VBLANK blanks nothing | `TiaLineTests` (2) |
| L8 | bit 0 kept in the frame | `TiaLineTests` (1) |
| L9 | a frame ends at the start of VSYNC | `ObserverTests` (1), `TiaLineTests` (2) |
| P1 | PF0's bit order reversed | `TiaPlayfieldTests` (12) |
| P2 | PF1 not reversed | `TiaPlayfieldTests` (18) |
| P3 | PF2 reversed | `TiaPlayfieldTests` (18) |
| P4 | reflect ignored | `TiaPlayfieldTests` (22) |
| P5 | a cell's bit read a clock earlier | `TiaPlayfieldTests` (1) |
| P6 | the right half starts on CNT itself | `TiaPlayfieldTests` (48) |
| F1 | a new line's row not cleared | none at first; after the test was strengthened, `TiaLineTests` (1) |
| F2 | the line limit at 399 | `Atari2600BusTests` (1), `ObserverTests` (1), `TiaLineTests` (1) |
| F3 | the window one line low | `FrameBufferTests` (4) |
| F4 | rows below the frame not black | none at first; after the test was strengthened, `FrameBufferTests` (2) |
| F5 | the line VSYNC ends on left in the old frame | `Atari2600BusTests` (1), `CycleOrderTests` (4), `FrameBufferTests` (12), `TiaLineTests` (9), `TiaPlayfieldTests` (55) |
| C1 | the PAL signal on NTSC | `FrameBufferTests` (1), `TiaPaletteTests` (7) |
| C2 | the NTSC step 24 degrees | `TiaPaletteTests` (5) |
| C3 | the saturation without the contrast scale | `TiaPaletteTests` (9) |
| C4 | red and blue swapped | `TiaPaletteTests` (9) |
| C5 | bit 0 read as luminance | `FrameBufferTests` (4), `TiaPaletteTests` (24) |
| B1 | two colour clocks before the access | `Atari2600BusTests` (2), `CycleOrderTests` (3) |
| B2 | RDY sampled as the cycle starts | `CycleOrderTests` (6), `ObserverTests` (1), `SuiteSelfTests` (2), `WsyncHaltTests` (160) |
| B3 | a held read not repeated | `Atari2600BusTests` (1), `CycleOrderTests` (6), `FrameBufferTests` (4), `ObserverTests` (1), `SuiteSelfTests` (10), `WsyncHaltTests` (158) |
| S1 | the frame does not report its row | `StateReportTests` (9) |
| S2 | the TIA does not report its clock | `StateReportTests` (9) |
| S3 | the playfield does not report its fetched bit | `StateReportTests` (9) |

In all, 31 faults were planted and all 31 were caught, two only after their test was strengthened. **F1 and F4 survived the first run**, for one reason: the tests drew into a frame buffer that had never held an older frame, so stale pixels were zeros, which is black anyway. `To_rgba_is_black_below_a_short_frame` now draws a long frame and then two short ones first. The RSYNC test now cuts a line in row 1 of a third frame, because row 0 is always overwritten by the carried line. Run again with each fault (`... tia-line-faults.py <scratch> F1 F4`), both were caught.

Task 4's probe, `bus-faults.py`, plants its TIA and cycle faults in code this task rewrote, so those of its patterns no longer match; it is kept as the record of task 4, and this task's probe covers the new code.
