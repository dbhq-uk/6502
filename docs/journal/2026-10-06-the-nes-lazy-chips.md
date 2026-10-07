---
title: "The NES's lazy chips: the gate first"
date: 2026-10-06
summary: "Dan chose to let the NES's PPU and sound unit be brought up to date only when something can see them, on one condition: the lazy build must give bit for bit what the per-dot build gives at every point where a chip can be seen. Before any chip changes, the differential was extended to be that test. The bus now tells an observer of each such point, every chip reports its whole state, a reflection test fails if a field is left out, and the per-ROM output of the build before the work is committed as the baseline. Ten deliberate one-dot or one-cycle faults in the parts the work will touch, planted one at a time in a scratch copy, each changed the output. The review found the gate's real gap: the test ROMs hardly touch the chips while a line is drawn, which is what the lazy work will change. So the differential now also builds 38 small cartridges of its own, whose register writes sweep across the dots of the lines frame by frame, and with them every fault changes at least 20 runs. A fault also turned up a crash in the PPU that has been there since the NES was built: the reset button in the middle of sprite evaluation can leave it writing past secondary OAM. The second task made the PPU lazy: the bus gives it its dots, and it runs them, with the same per-dot code, only where something can see it. The gate gave output identical to the baseline on every run, the old faults are still seen, and so is each of four new faults planted in the catch-up. Natively, a cycle of the benchmark ROM takes about half the time it did."
order: 36
---

# 6 October 2026: the NES's lazy chips, the gate first

The NES runs at about 2 to 2.5 times real time in the browser on the
development machine, and slower on a laptop or a phone. On 6 October Dan
approved the design in
[`docs/superpowers/specs/2026-10-06-nes-lazy-chips-design.md`](../superpowers/specs/2026-10-06-nes-lazy-chips-design.md):
the PPU and the sound unit are advanced lazily, in batches, and brought up to
date ("caught up") only where something outside them can see them. The
condition is a rule with a test: **the lazy build must give bit for bit what the
per-dot build gives, at every point where a chip can be seen.** The plan is
[`docs/superpowers/plans/2026-10-06-nes-lazy-chips.md`](../superpowers/plans/2026-10-06-nes-lazy-chips.md).

This entry is kept as the work goes, one section a task. Task 1 builds the test
before anything changes: the differential in `bench/nes-speed/differential`,
extended, and its output from the code as it stood recorded as the baseline.

## Task 1: the gate

### What was there

The differential written in task 6b of the NES work (see
[the NES speed entry](2026-10-05-the-nes-speed.md)) runs the 127 pinned test
ROMs in both regions and writes one line of hashes a run: after every
instruction the CPU's registers, the cycle count and the PPU's line and dot; at
every frame end the pixels, `v`, `t`, fine X, the dot count, OAM, the interrupt
lines and the samples; at the end RAM, the PPU's registers, VRAM and OAM. That
covers the CPU completely, but the chips only once a frame and through a few of
their fields. A lazy PPU that left `_attributeBits` or the sprite buffers
different at a `$2002` read, and got the picture right by luck, would pass it.

### The observation points

The bus now tells an observer (`INesObserver`, internal) of each point where a
chip can be seen, which is each point where a lazy build must have caught it
up:

- **after each CPU access to `$2000` to `$401F`**, read or write, with the value
  a read gave, and before the dots of the cycle that come after the access.
  OAM DMA's writes to `$2004` are such accesses, and so are the repeated reads a
  DMA's halt cycles make;
- **after each write to the board's registers, `$8000` to `$FFFF`.** The design
  does not list this one, and it must: a board write can move the pattern banks
  or the nametable layout (MMC1, CNROM, AxROM's mirroring), and the PPU reads
  them on every fetch. A PPU caught up after the write instead of before would
  draw the dots between with the new banks. So the lazy build must catch the PPU
  up before a board write, and the differential hashes the PPU there. PRG RAM
  writes at `$6000` to `$7FFF` change nothing the PPU reads and are not points;
- **after each DMC fetch**, once the byte is in the channel;
- **at each frame end**, from inside the PPU's dot that ends the frame, so the
  state is the frame's last whatever the bus is doing in that cycle;
- **at power on and the reset button**, after the chips are reset and before the
  CPU's reset sequence runs. A lazy build must catch up before it resets a chip;
- **at the end of every cycle**, with the NMI and IRQ lines the CPU was given.
  These are not catch-up points; they are there because the lines must be exact
  on every cycle in a lazy build too.

The harness adds one of its own: after it reads the samples at each frame's end,
which is where the page reads them, so the sound unit must be caught up there.

**What it costs when nobody listens.** Each call is behind
`NesBus.Observable`, a `static readonly` flag read once from the runtime option
`Dbhq.Machines.Nes.Observable`, which only the differential's and the NES
tests' project files set. With it off, .NET's tiered JIT treats the flag as a
constant and drops the calls: the disassembly of `NesBus.Cycle` at tier 1 with
the flag off has no call to the observer, and with it on it has the null checks
and the two calls (`DOTNET_JitDisasm="Cycle" dotnet <bench>/Dbhq.Machines.ThreadTime.dll ntsc 1`,
6 October 2026, 19:05 UTC). In the browser's AOT build the flag should be a load
and a branch that always goes the same way, since Mono's AOT compiler builds the
code before the class is set up and cannot treat the field as a constant; that is
reasoned, not measured, as no browser round was run for it. The thread-time bench was run alternately
on the baseline (`5e48505`, exported with `git archive`), this build, and this
build with the flag switched on in its `runtimeconfig.json` and no observer set,
four rounds of five runs of SNOW each region, 6 October 2026 from 19:03 UTC, at
loads (one-minute average) falling from 19.1 to 3.8:

| Build | NTSC medians, ns a cycle | Median of the four | PAL medians | Median of the four |
| --- | --- | --- | --- | --- |
| `5e48505` | 179.69, 170.09, 151.70, 162.48 | 166.3 | 160.72, 158.41, 166.55, 189.02 | 163.6 |
| this build | 164.33, 163.36, 162.28, 191.91 | 163.8 | 159.56, 152.56, 167.31, 171.94 | 163.4 |
| this build, flag on | 164.57, 182.77, 166.16, 166.24 | 166.2 | 170.79, 172.08, 161.69, 166.81 | 168.8 |

```
for round in 1 2 3 4; do for b in <base>/out <after>/out <after>/out-on; do
  for w in ntsc pal; do dotnet $b/Dbhq.Machines.ThreadTime.dll $w 5; done; done; done
```

The three are within the bench's noise, which task 17 measured at 10 to 20
percent from one launch to the next on this shared machine: the cycle path did
not get measurably slower.

### The per-dot reference

`NesOptions.PerDotReference` is the switch Dan's design keeps for good: a
build that is otherwise lazy advances the chips inside every call, as now, and
that is the oracle the lazy build is compared with, in the tests and in the
benches. In this build it changes nothing. The differential takes `--oracle`,
so the baseline can be made again from a lazy build in that mode and compared
with the committed file: run on this build on 6 October at 18:53 UTC, it gave
the same 256 lines.

### The state list

Every class with state now reports it (`IReportsState.ReportState`, internal):
each field by name, in a fixed order, or skipped with the reason. The list was
written from the code, field by field. `StateReportTests` reads the reports of
a machine built on each of the six boards, with CHR ROM and with CHR RAM, and
compares the names with the fields each class declares by reflection: a field
that is neither reported nor skipped fails it, naming the field, and so does a
name that is no field. The differential runs the same check
(`StateCompleteness`, one file shared by both) before it runs anything, and
stops if a field is missing. So the list cannot fall behind the code without a
test failing: a field a later task adds must be reported or skipped with its
reason.

Reported, and so hashed wherever the chip is hashed:

| Part | Fields |
| --- | --- |
| PPU registers | `_ctrl`, `_mask`, `_status` (VBlank, sprite 0 hit, overflow), `_oamAddress`, the I/O latch `_latch` and when each bit was driven `_latchDriven`, the time base `_timeBase` the decay is measured in, the read buffer `_readBuffer` |
| PPU scroll | `_v`, `_t`, `_x`, `_w` |
| PPU position | `_line`, `_dot`, `_oddFrame`, `_frame`, the VBlank suppression `_suppressVblank`, the odd frame's dropped dot `_dropDot`, and `CpuCycle`, the cycle given to the board with each address |
| PPU mask caches | `_emphasis`, `_greyscaleMask`, the 32 entries' colours `_entryColours`, `_backgroundFrom`, `_spritesFrom` |
| PPU memory | the palette `_palette`, the nametable RAM `_nametables` (4 KB, the four-screen half too), `Oam` |
| PPU background | the latches `_nametableByte`, `_attributeBits`, `_patternLowByte`, `_patternHighByte`, the pattern address `_patternAddress`, the shifters `_backgroundPixels` |
| PPU sprite evaluation | secondary OAM `_secondaryOam`, `_evaluationN`, `_evaluationM`, `_oamLatch`, `_secondaryIndex`, `_found`, `_secondaryFull`, `_evaluationDone`, `_sprite0Found` |
| PPU sprites on the line | the line buffer `_spriteLine` and its extent `_spriteLeft`, `_spriteWidth`, `_spriteCount`, `_sprite0OnLine`, and the slot being fetched `_fetchY`, `_fetchTile`, `_fetchAttributes`, `_fetchX`, `_fetchLow`, `_spriteAddress` |
| The picture | `Screen.Pixels` and `Screen.Frame` |
| Sound unit | the cycle count `_cycles`; the frame counter `_fiveStepMode`, `_irqInhibit`, `_frameIrq`, which table `_steps` and `_actions` are, `_stepIndex`, `_frameCycle`, `_nextStep`, the `$4017` write delay `_resetIn` and `_pendingFiveStepMode`; `_lengthWritten`; the mixer's `_output`, `_pulseMix`, `_tndMix` |
| Pulses | `_timer`, `_period`, `_step`, `_duty`, `_muted`, the sweep's `_sweepEnabled`, `_sweepPeriod`, `_sweepNegate`, `_sweepShift`, `_sweepDivider`, `_sweepReload`, the envelope, the length counter |
| Triangle | `_timer`, `_period`, `_step`, the linear counter `_linear`, `_linearReloadValue`, `_linearReload`, `_control`, the length counter |
| Noise | `_timer`, `_reload`, `_periodIndex`, the LFSR `_shiftRegister`, `_feedbackBit`, the envelope, the length counter |
| DMC | `_rateIndex`, `_timer`, the output level `_level`, `_irqEnabled`, `_loop`, `_irqFlag`, `_sampleAddress`, `_sampleLength`, the reader's `_currentAddress`, `_bytesRemaining`, `_buffer`, `_bufferFull`, when the next fetch may halt `_fetchFrom`, the output unit's `_shift`, `_bitsRemaining`, `_silent` |
| Envelope | `_start`, `_divider`, `_decay`, `Loop`, `Constant`, `V` |
| Length counter | `Value`, `Halt`, `Enabled`, and the write-in-the-clock's-cycle flags `_haltWritten`, `_haltBefore`, `_loaded`, `_valueBefore` |
| Sample buffer | the block `_blockSum`, `_blockLeft`, the level `_level`, the running sum `_output`, the instant `_instant` and phase `_fraction`, the pending steps `_pending`, the filters `_hp90Out`, `_hp90In`, `_hp440Out`, `_hp440In`, `_lpOut`, the ring's `_read`, `_count`, `_dropped`, and the samples waiting in it |
| Bus | RAM `_ram`, the open bus `_openBus`, the dot accumulator `_dotAccumulator`, `_cycles`, `_ppuDots`, the DMA page waiting `_dmaPage`, the last read address `_lastReadAddress` (the pads' clock) |
| Pads | `_strobe`, `_latched`, `_reads`, `Buttons` |
| Every board | the PRG and CHR windows `PrgBase`, `ChrBase`, `PrgRamEnabled`, `PrgRamWritable`, the mirroring and its page table, PRG RAM, and CHR RAM where the board has it |
| MMC1 | the shift register `_shift` and its count `_count`, `_control`, `_chr0`, `_chr1`, `_prg`, the cycles since a write `_sinceWrite` |
| MMC3 | the banks `_banks`, `_select`, the IRQ latch `_latch`, counter `_counter`, reload `_reload`, enable `_irqEnabled`, line `_irq`, and A12's filter `_a12Low`, `_lowSince` |

Skipped, with the reason each report gives: every field fixed when its object is
made (the region's tables and counts, the board's flags, the decay time, the
palette's colours, the dot ratio's parts, the sweep's negate, the bus-conflict
flags, the NROM masks); the PRG ROM and CHR ROM, which never change; the copies
of another part's arrays the PPU and the bus hold to read without a call (the
board's pattern memory, windows and page table, and its PRG), which that part
reports; `_pixels`, the same array as `Screen.Pixels`; the CPU, whose registers
are hashed after each instruction and whose lines every cycle; the observer
itself; and one cache flag, the sound unit's `_mixStale`. The mixer's three values
are reported as `Output` would make them, worked out in the report when they are
stale, so a report changes nothing and a batch that leaves the flag the other way
round with the same values is not a difference, while one with different values
is.

### What is hashed where

At a point where one chip can be seen the other is not hashed, because a lazy
build need not have caught it up there, and a difference would be a false alarm
that pushed the design into catching up for nothing:

| Point | Hashed |
| --- | --- |
| a PPU register access, OAM DMA's writes among them, or a board write | the point and the access, the logical position (the bus's cycle and dot counts, `Ppu.Line` and `Ppu.Dot`), and the PPU's whole state but the picture; the bus's, the pads' and the board's state |
| a sound register or pad access, `$4000` to `$401F` | the point and the access, the bus's counts, the sound unit's and the sample buffer's whole state; the bus's, the pads' and the board's |
| a DMC fetch | the same as a sound register access |
| a frame end | the bus's counts and the PPU's whole state with the picture |
| after the samples are read at a frame's end | the sound unit, the sample buffer, the bus, the pads and the board |
| power on and reset | all of it |
| every cycle | the NMI and IRQ lines the CPU was given |
| every instruction, as before | the registers, the cycle count, and the PPU's line and dot |

Three choices in that table. **The position is the logical one.**
`Ppu.Line` and `Ppu.Dot` will report the dot the bus says the PPU is on,
without a catch-up, so hashing them beside the PPU's own `_line` and `_dot`
checks that the PPU was caught up at the point; at a frame end only the state's
own is hashed, because the dot that ends a frame can be run before the bus is
done with its cycle. **The picture is hashed at frame ends, not at every
access.** Each pixel is written once a frame, on its own dot, so a wrong one
stays wrong until the frame end hashes it, and hashing 61,440 pixels at each of
a few thousand accesses a frame would make a run hours long. **A frame end
hashes only the PPU and the cycle count:** the frame can end in the dots before
or after the cycle's access, so RAM, the board and the sound unit there could
differ by an access between two correct builds.

The homebrew is run too, which the old tool did not: Lan Master, the game the
page starts, from `roms/nes/`, for four times the frames, with a fixed round of
buttons (Start on the title, then moves and turns, one button 3 frames in 6), so
the gate covers a game's play and not only the test ROMs. Its picture was looked
at in a scratch copy at frame 280: the first level, with pieces turned.

The six hashes of the old tool are made as it made them, so its files and the
new ones agree on them. They use FNV-1a over 64-bit words, and that has a weak
spot found while writing this: a word's high bits only reach the hash's high bits,
so two differences in, say, bit 63 cancel exactly. The new hashes mix each word
first (splitmix64's finaliser), and the per-instruction words are hashed again
that way as `cpu`. The program's opening comment says what each hash covers, and
the file's first line names the format and the frame count, so two files are
comparable only when their first lines match. `--check <file>` runs, compares,
prints the first run that differs and which of its hashes, and exits 1, so each
later task's gate is one command:

```
dotnet run -c Release --project bench/nes-speed/differential -- --check bench/nes-speed/differential/baseline/4e9b92b.txt
```

### The baseline

First, that the hook changes nothing: the old tool, built from the tree before
any of this, was run on 6 October between 18:19 and 18:24 UTC, and this build's output was
compared with it on the six hashes they share: all 254 lines the same. Then the
baseline, from this build, whose `src/` differs from `5e48505` only in the
observer, the option and the state reports:

```
dotnet run -c Release --project bench/nes-speed/differential -- bench/nes-speed/differential/baseline/5e48505.txt
```

6 October 2026, 18:44:59 to 18:53:33 UTC, load (one-minute average) 25.7 at the
start and 31.8 at the end, 8 minutes 34 seconds. It is 256 runs, the 127 ROMs
and the homebrew in both regions, and the file is 70,451 bytes. It was the same,
byte for byte, as a first run made between 18:34 and 18:41 UTC (6 minutes 53
seconds, at loads of 17.2 to 1.9), and `--check` with `--oracle` from 18:53 to
19:01 UTC (loads 31.8 to 32.9) printed `IDENTICAL: all 256 runs match`.

**That file was replaced after the review** by one with the synthetic runs,
`bench/nes-speed/differential/baseline/4e9b92b.txt`, named for the commit it was
recorded at. That commit's `src/` still differs from `5e48505` only in the
observer, the option, the state reports and the reports' skip reasons (`git diff
5e48505 4e9b92b -- src`); the harness changed, not the machine.

```
dotnet run -c Release --project bench/nes-speed/differential -- bench/nes-speed/differential/baseline/4e9b92b.txt
```

6 October 2026, 21:41:12 to 21:45:30 UTC, 4 minutes 18 seconds of wall clock and
10 minutes 47 seconds of CPU, at loads of 4.7 to 1.4: 332 runs, the 127 ROMs, the
homebrew and the 38 synthetic cartridges in both regions, 90,387 bytes. Its 256
ROM and homebrew lines agree with the first file on every hash but `ppu`, `apu` and
`board`, whose memories are now hashed in four lanes (above), so the first file
was dropped rather than kept beside it. `--check` with `--oracle`, 21:45:30 to
21:49:00 UTC, printed `IDENTICAL: all 332 runs match`.

### Showing that it fails

The plan's step 4: a deliberate fault in each part the lazy work will touch
must change the output, or the state list or the ROMs miss something. In a
scratch copy outside the repository (the working tree copied with `rsync` to
`/tmp/lazy-faults`, `.testdata` linked in, removed afterwards), a script applied
one exact replacement at a time, built the differential, ran `--check` against
the baseline with `--out`, counted the lines that differ and which hashes, and
put the file back. 6 October 2026, 19:04 to 20:02 UTC, loads 0.7 to 23.6.

| Fault (one edit each) | Runs changed, of 256 | ROMs | Of those, seen only by the new state hashes |
| --- | --- | --- | --- |
| The background's nametable byte fetched a dot late (the third dot of each 8 on the visible lines instead of the second) | 222 | 114 | 222 |
| The background's shifters reloaded a dot late | 211 | 109 | 5 |
| Sprite evaluation's first OAM read a dot late (secondary OAM cleared up to dot 65) | 115 | 58 | 68 |
| Sprite 0 hit tested against the next column's background pixel | 4 | 2 | 0 |
| The VBlank flag set on dot 2 of line 241 instead of dot 1 | 121 | 83 | 10 |
| A CPU access to the PPU made after three of its cycle's dots instead of two, which is what a catch-up one dot short would do | 256 | 128 | 58 |
| Every step of the sound unit's frame counter a cycle late | 256 | 128 | 197 |
| A DMC reload's fetch allowed to halt the CPU a cycle later | 57 | 29 | 39 |
| The sprite fetch's pattern address put on the PPU's bus a dot late, so MMC3 sees A12 rise a dot later | 4 | 3 | 2 |
| MMC1 ignoring a write two cycles after a write, not only one | 12 | 6 | 0 |

"Seen only by the new state hashes" counts the runs where none of the old tool's
six hashes changed, nor `cpu`. The first fault is the one the old tool could not
see: the byte is the same, because `v` does not move between the two dots, so
the picture is the same, but a `$2002` read on that dot sees the latch hold the
old byte, and a lazy PPU could get it wrong in the same way.

So every fault was seen. Two were seen by few ROMs: sprite 0 hit by the two
`sprite_hit_tests` that test its alignment and corners, and the MMC3 fault by the
three scanline-timing ROMs. The review took that further (below). The completeness
check was shown to fail as well: with one field's line taken out of the PPU's
report in the scratch copy, `StateReportTests` failed on every board naming
`Ppu._fetchLow`, and the differential stopped before running, naming it too.

### After the review: synthetic cartridges

The review of task 1 found the gate's real gap, which was the plan's, not the
code's: almost every one of the 127 ROMs draws a still screen with rendering off,
or works in VBlank. The fast scanline renderer of tasks 3 to 5 exists to skip
exactly the lines those ROMs never exercise, so a fast path wrong only there would
pass. So the differential now builds its own cartridges, from bytes, each a small
6502 program assembled by a tiny assembler in the harness (`Asm.cs`, `Synthetic.cs`;
never a game), and runs 38 of them in both regions beside the ROMs. Each is a
workload on a board, and each keeps rendering on and moves its timing frame by
frame:

| Workload | What it does |
| --- | --- |
| fuzz | an LFSR picks a register (`$2000` to `$2007`, `$4014`, `$4015`, `$4016`, `$4017`), a value and a read or a write, then a delay; now and then the board's write and a wait for sprite 0; the DMC plays and IRQs are taken; eight seeds on five boards |
| sprite0 | `BIT $2002` / `BVC` waits for sprite 0 hit, with sprite 0 at x = 0, 1, 7, 8, 128, 248, 254 and 255, its y swept, its flips and priority varied, the left clips on and off; after the hit, a delay a cycle longer each frame, a `$2005` and `$2006` split, and the board's write; on all six boards |
| scroll | from the NMI, a delay a cycle longer each frame, then `$2006`, `$2006`, `$2005`, `$2005` and `$2000` |
| mask | the same delay, then `$2001` with bit 1, 2, 3 or 4 cleared, or greyscale, or emphasis, put back 7 cycles later |
| sprites | 64 sprites in clusters, more than eight on many lines, moving at four speeds (the overflow flag's false positives and negatives come and go), 8 by 16 with both flips on some boards; after the delay, `$2003` and `$2004` writes and an OAM DMA during rendering |
| nmi | VBlank found by polling, then a delay to just before the next, then a burst of `$2000` bit 7 on and off and `$2002` reads across the dot the flag is set, for NTSC and PAL frame lengths |
| apu | length counters loaded in 8 frames of 64 and left to run out through the silent rest, the DMC looping one frame and ending with an IRQ the next, and after the delay a `$4017` write (the delay's single cycles put it on odd and even cycles), a `$4015` read and a `$4015` write |

The boards each add a write of their own, mid-frame: MMC1 a read-modify-write to
the board, whose second write the chip ignores, then a CHR bank by its serial
port; CNROM a CHR bank, through a table so the bus conflict changes nothing; AxROM
its single screen, the two screens different; UxROM a PRG bank, with CHR RAM
written in VBlank; MMC3 a CHR bank from the main loop, and its IRQ, a number of
lines down that changes every frame, switching another bank under the sprites and
toggling greyscale, with 8 by 16 sprites or 8 by 8 from `$1000` so A12 rises each
line.

**The sweep.** The timed workloads run in the NMI handler, after its OAM DMA, so
the delay starts a fixed time after VBlank begins. A count goes up a cycle each
frame to 120, then starts again in the next of three windows: just before NTSC's
pre-render line, just before PAL's, and the middle of the picture. A clockslide (a
run of `CMP #$C9` entered n bytes from its end) gives single cycles. On NTSC a cycle
is three dots, so a cycle a frame alone reaches only the dots in step with the
frame; rendering is switched off in one frame in four, at random, so that odd
frames sometimes keep their last dot and the phase moves. `--coverage` prints, for
each synthetic run, the dots its PPU register writes landed on with rendering on,
and changes no hash (its output's lines were the baseline's). Run on 6 October,
21:52 to 21:54 UTC, at loads of 1.0 to 5.1, as `dotnet
bench/nes-speed/differential/bin/Release/net10.0/Dbhq.Machines.Nes.Differential.dll
/tmp/cov.txt --only synthetic/ --coverage --threads 6`:

| Runs | Visible lines' dots reached, each run | Pre-render line's dots reached, each run | Of the review's dots (0, 1, 2, 255, 256, 257, 258, 320, 337, 338, 339, 340), on the pre-render line, reached by none |
| --- | --- | --- | --- |
| NTSC scroll | 341 | 306 to 320 | none |
| NTSC mask | 340 to 341 | 202 to 274 | none |
| NTSC sprites | 341 | 334 to 338 | none |
| PAL scroll | 304 to 339 | 208 to 211 | 1, 257, 337, 340 |
| PAL mask | 304 to 331 | 193 to 204 | 1, 257, 337, 340 |
| PAL sprites | 341 | 212 to 213 | 1, 257, 337, 340 |

The sprite0, fuzz and nmi runs reach 333 to 341 of the visible lines' dots each,
and the nmi runs put `$2000` writes and `$2002` reads on each of dots 0 to 3 of
line 241, in both regions. **PAL's pre-render line cannot be covered this way.**
PAL's CPU cycle is 3.2 dots, 16 dots every 5 cycles, and its frame of 106,392 dots
is 8 more than a multiple of 16, so a program whose timing follows the frame can
put an access on only 10 of every 16 dots of any one line, about 62 percent, which
is what the runs reach. On another line the 10 are other dots, which is why the
visible lines are covered; the pre-render line is one line, and dots 1, 257, 337
and 340 of it are among those no program here can reach on PAL. It is a property
of the model's fixed power-on alignment; a later task that wants those dots needs a
scene test that sets the PPU's position directly.

The synthetic runs are four times the frames, 600 at the default 150, like the
homebrew. Before the memories' hash was given four lanes, which halved a sprite-0
run's time, the whole differential took 18 minutes of wall clock and about 20 of
CPU at loads of 10 to 23; the baseline's figure is below.

### After the review: the fault pass again

The same faults, against the new baseline, with the script now committed as
[`bench/nes-speed/differential/faults.py`](../../bench/nes-speed/differential/faults.py),
which takes the path of a scratch copy (here `git archive 4e9b92b` into
`/tmp/nes-faults`, `.testdata` linked, removed afterwards; for the last five
faults, the harness's handling of a crash, below, was copied in first):

```
python3 bench/nes-speed/differential/faults.py /tmp/nes-faults bench/nes-speed/differential/baseline/4e9b92b.txt --threads 6
```

6 October 2026, 21:54 to 23:01 UTC, loads 1.4 to 15.8. Of 332 runs (the first
pass's figure, of 256, in brackets):

| Fault | Runs changed | Of them synthetic | Seen only by the new state hashes |
| --- | --- | --- | --- |
| The background's nametable byte fetched a dot late | 298 (222) | 76 | 226 |
| The background's shifters reloaded a dot late | 287 (211) | 76 | 5 |
| Sprite evaluation's first OAM read a dot late | 191 (115) | 76 | 68 |
| Sprite 0 hit tested against the next column's background pixel | 42 (4) | 38 | 7 |
| The VBlank flag set on dot 2 of line 241 | 174 (121) | 53 | 10 |
| A CPU access to the PPU made after three of its cycle's dots | 332 (256) | 76 | 58 |
| Every frame counter step a cycle late | 332 (256) | 76 | 249 |
| A DMC reload's fetch allowed to halt the CPU a cycle later | 81 (57) | 24 | 39 |
| The sprite fetch's address on the PPU's bus a dot late (MMC3's A12) | 26 (4) | 22 | 2 |
| MMC1 taking the write on the cycle straight after a write | 20 | 20 | 0 |

Sprite 0 and MMC3, seen by 4 runs each before, are now seen by 42 and 26. The last
row is a different fault from the first pass's, which was wrong: it made MMC1
ignore a write two cycles after the last, but the count it tested stops at 2, so
it ignored almost every write, and was not the one-cycle fault it was meant to
be. No 6502 instruction writes twice two cycles apart; a read-modify-write writes
on two cycles in a row, and the chip ignores the second. Taking that second write
is now the fault. No test ROM writes to MMC1 that way, so only the synthetic
MMC1 runs, whose board write is an `INC` of a ROM byte, see it: 20 runs, on all
ten of them in both regions.

**A crash found on the way.** With the fault that makes PPU accesses a dot late,
one synthetic run (`fuzz-mmc3-3`, NTSC) threw `IndexOutOfRangeException` in
`Ppu.EvaluationStep`, which stopped the whole differential. It is not the fault's:
the code at `5e48505` does it too. `Ppu.Reset` clears `_found` but not the rest of
sprite evaluation (`_secondaryIndex`, `_evaluationN`, `_evaluationM`,
`_secondaryFull`, `_evaluationDone`). If the reset button lands in the middle of
evaluation, and rendering is next switched on after dot 1 of a visible line (so
the line's own reset at dot 1 does not happen) with sprites in range, evaluation
goes on from the old index with the count at 0, and writes past the 32 bytes of
secondary OAM. A scratch test on `5e48505` showed it in a few lines: sprites at
y = 0, 8 by 16, rendering on, the PPU reset at line 10 dot 105, ten dots, `$2001`
written with `$18`, then on to line 20: it throws. The fault only moved a write
onto a dot where the fuzz cartridge could reach it. This task changes no
behaviour, so it is not fixed here; the fix, for its own change with that test, is
to put evaluation back with the rest in `Ppu.Reset`, or to wrap the index at 32 as
the chip's 5-bit counter does. The differential now writes a run that throws as
that run's line (`crashed: <exception> in <method>`) and goes on with the others,
so a crash is reported like any other difference.

### After the review: the reports checked by value

`StateCompleteness` checks the reports by name. Three more tests in
`StateReportTests` check them by value, on each board with CHR ROM and CHR RAM,
after three frames:

- every reported field, changed alone by reflection (its low bit, an array's first
  element, an enum's next value; the frame counter's table, which is reported as
  which one, swapped for the other; the sample ring's first waiting sample), changes
  the whole report, and put back, puts it back;
- every field skipped as fixed is read-only (`IsInitOnly`);
- every field skipped as another part's own is that part's: the PPU's board, its
  pattern memory, windows and nametable layout and the bus's PRG and windows are the
  board's own objects, and `_pixels` is `Screen.Pixels`. NROM keeps no windows, so
  for it the PPU's and the bus's are checked to be the fixed layout, and the skip
  reasons now say so.

Two observer tests were added: an OAM DMA is told as 256 writes to `$2004`, and
reads at `$8000` and up and accesses to `$6000` to `$7FFF` are not told. The test
`WithNoObserverNothingIsCalled` could not see a call with no observer set, so it
is renamed for what it does check. A malformed baseline line now stops `--check` with a
message.

### For the tasks that follow

What the gate asks of a lazy build, from the points above:

- catch the PPU up before a CPU access to `$2000` to `$3FFF`, before each OAM
  DMA write, and also **before a write to the board's registers**, which the
  design does not list;
- catch the sound unit up before an access to `$4000` to `$401F`, before the
  DMC's byte is handed to the channel, and when the samples are read;
- end each frame in the cycle it ends in, from inside the catch-up, so the frame
  end sees the same cycle count;
- catch both up before power on or the reset button changes them;
- keep `Ppu.Line` and `Ppu.Dot` logical, and every other public read of a chip's
  state (Ruling R of the plan's ledger) caught up when read;
- the board's state that the PPU drives, MMC3's `_counter`, `_irq`, `_a12Low`
  and `_lowSince`, is hashed at the sound unit's points too, with the rest of the
  board. That is right only because a board that watches the PPU's address bus
  stays on the per-dot path, so the PPU has run its dots before the access as now.
  If MMC3's clock is ever scheduled, those four fields must move to the PPU's
  points. (The choice was between this note and having MMC3 report them only at the
  PPU's points; the note keeps the stricter hash while it is true.)
- a new field must be reported or skipped with its reason, or
  `StateReportTests` fails. A lazy build's bookkeeping, such as the dots
  delivered and the dots caught up to, is skipped with that reason: the
  logical position is hashed through `Line`, `Dot` and the bus's counts.

## Task 2: the PPU caught up on demand

The bus no longer runs the PPU's dots inside each cycle. It gives them to the
PPU, which runs them when something can see it. The dots are run by the same
per-dot code as before, one at a time, so the work is the same; only the moment
it is done moves. 7 October 2026.

### What was built

**Two positions** (`PpuCatchUp.cs`). `Ppu.LogicalDots` counts the dots the bus
has delivered, `Ppu.CaughtUpDots` the dots the state has been run to. `Deliver`
adds to the first and runs nothing. `CatchUp` runs the owed dots through the old
`Tick` body, now the private `RunDot`, until the two are equal. `Tick` is one dot
delivered and caught up, so the tests that drive a PPU alone work as before. A
catch-up that spans frame ends runs each one in turn, with its observer call.

**Where the bus catches it up** (`NesBus.Cycle`). The cycle's first two dots are
delivered before the access and the rest after, as the per-dot build ran them.
Then:

| Where in the cycle | When |
| --- | --- |
| after the first two dots, before the sound unit, the board and the access | a CPU access to `$2000` to `$3FFF` (OAM DMA's writes to `$2004` among them), and a CPU write to the cartridge, `$4020` to `$FFFF` (Ruling T: a write there can switch the pattern banks or the nametable layout) |
| at the end, before the bus adds the cycle's dots to its count | when the delivered dots reach `NextEventDot` or the frame's end |
| both places, every cycle | with `NesOptions.PerDotReference`, and for a board that watches the PPU's address bus (MMC3), so it is told each address in the cycle it is put out |
| before power on and the reset button | always, before the board is reset |

The frame end is an event of its own, so a frame ends in the cycle it ends in,
from inside the catch-up, and the frame end's hash of the cycle count and the
picture is the per-dot build's. `Ppu.ReadRegister`, `WriteRegister` and the
peeks also catch up themselves, so a caller that is not the bus gets the right
state too; the bus's own check before a register access is then a second guard.

**`NextEventDot`** is the value `LogicalDots` has once the dot that next changes
the NMI output has been delivered:

- PPUCTRL bit 7 clear: none (`long.MaxValue`). No dot can change the output.
- bit 7 set and the VBlank flag clear: the dot at line 241 dot 1.
- bit 7 set and the flag set: the pre-render line's dot 1, which clears it.

It is worked out again (`ScheduleEvents`) when the dots pass it or the frame
end, after a `$2000` write (bit 7), a `$2001` write (rendering decides whether
the odd frame drops its last dot, which moves the frame end), a `$2002` read
(it clears the flag, and on the dot before the flag it stops it), the reset
button and power on. Each of those accesses comes after a catch-up, so the new
event is worked out from the state the access left. After a read that stops
the flag, the event stays on line 241 dot 1: the catch-up there finds the flag
not set and works the next one out, which costs a catch-up and nothing else.

So the NMI output, which the bus reads at the start of every cycle, is right
without a catch-up: every dot that could change it has been run by the end of
the cycle before. A `$2002` read on the VBlank dot comes after a catch-up to the
dot it sees, so it sees the flag and stops it as before; a `$2000` write that
turns NMI on in VBlank comes after a catch-up, so the line rises at that
cycle's end, as before.

**The reads** (Ruling R). `Line` and `Dot` give the logical position without a
catch-up: the caught-up one moved on by the dots owed, and past a frame end by
whole frames less the odd frame's dropped dot, which only a `$2001` write could
change. `LogicalDots`, `NextEventDot` and `RenderingEnabled` do not catch up
either. `Frame` and `OddFrame` change only at a frame end, so they catch up
only when one is owed: `Nes.RunFrames`, the page's loop, reads `Frame` after
every instruction, and a full catch-up there would make the build per-dot
again. `V`, `T`, `FineX`, `WriteToggle`, `Oam`, `Screen`, `Nmi`,
`PeekRegister` and `PeekVram` catch up. `Oam` and `Screen` were properties
with hidden fields; they are now `_oam` and `_screen` with a property in front,
because the dots read them and must not catch up from inside a dot. The bus
reads the NMI output through an internal `NmiOutput` that does not catch up.

**The state report.** The five new fields are skipped with their reason: the
two counts are the catch-up's bookkeeping (the logical position is hashed
through `Line`, `Dot` and the bus's counts), and the three event dots are
worked out from the state.

**A seam for tests.** `NesBus` has an internal constructor that takes a board,
so a test can fit a stub board that does or does not watch the address bus.

### Decisions, and what they were chosen over

- **The frame end as an event**, over working `Frame` out from the logical
  position. The frame end's hash has the cycle count in it, and the page reads
  the picture when `Frame` moves on, so the frame must really end in its cycle.
- **Every write to the cartridge**, `$6000` to `$7FFF` included, over the
  board's registers alone. Ruling T names the whole space. PRG RAM writes move
  nothing the PPU reads today, so for them the catch-up is only a cost; a game
  that writes PRG RAM often pays it. Narrowing it is a later choice, with a
  board flag for "this write can move the PPU's banks".
- **The register methods catch up themselves**, as well as the bus. It costs
  one compare, and a test or a tool that writes a register directly gets the
  per-dot answer. It means that a fault that takes out only the bus's catch-up
  before a `$2002` read changes nothing; the fault below takes out both.
- **The catch-up before the reset button's board reset** is a guard. No board
  here changes anything the PPU reads at the reset button, so no test can see
  it go. The one before power on is needed: power on clears a board's CHR RAM,
  and a test (`PowerOnAndResetCatchThePpuUpBeforeTheBoardIsReset`) fails
  without it.

### The tests

Written first, then run against an eager stub of the same members (`Deliver`
ran the dots at once, `NextEventDot` was always none): 56 of the 62 new tests
failed. The six that passed are the ones an eager PPU meets as well: `Tick`,
reset and power on catching up, and the MMC3 scene, which is caught up every
cycle anyway. That shows only that the stub was not lazy: the scene rows failed
on their check that some dots were owed at a cycle's end, not on a difference in
state. It does not show that the tests catch a timing fault in the catch-up;
the planted faults below are the evidence for that. Then built.

- `PpuCatchUpTests`, the PPU alone: `Deliver` moves only the logical position;
  chunks of every size from 1 up, across frame ends and the dropped dot, give
  the position the ticks give and the same state when caught up; a catch-up over
  four frames ends each (Review Focus 5); `NextEventDot` is the VBlank dot with
  bit 7 on and none with it off, moves to the clear with the flag set, and to
  the next frame after a `$2002` read; a `$2000` write catches up first; which
  reads catch up and which do not; `Tick` is one dot delivered and caught up;
  reset and power on catch up first.
- The scene table (`CatchUpScenes.cs`): 20 scenes, each in both regions, run on
  the per-dot reference and the lazy build. Every access to the PPU or write to
  the cartridge, with the logical position, the PPU's whole state and the bus's;
  every frame end with the picture; the reset; and the NMI and IRQ lines every
  cycle: all equal. The lazy build must also leave dots owed at some cycle's end,
  so the test is not passing on a build that is not lazy. The rows: no access at
  all with NMI on, off and rendering off (Review Focus 5); `$2005` and `$2006` on
  the line's key dots; `$2000` mid-line; `$2001` at the line edges and on the
  pre-render line's dots 333 to 340 (Review Focus 2); `$2002` read at line 240
  dot 340 and line 241 dots 0, 1 and 2, and `$2000` bit 7 on and off through
  VBlank, with rendering on and off (Review Focus 4); OAM DMA and `$2003` and
  `$2004` while rendering; `$2007` while rendering; `$2002` polled through sprite
  0, 8 by 8 and 8 by 16; CNROM bank writes and PRG RAM writes mid-line; MMC3's
  scanline IRQ; the reset button mid-frame.
- `NesBusTests`: a stub board that watches has the PPU caught up at every
  cycle's end and is told the same addresses in the same cycles as on the
  reference, and one that does not watch leaves dots owed; a CNROM bank switch
  at line 100 dot 130 draws the old bank before the write and the new one after,
  on both builds (Ruling T); the bus catches up in the cycle that reaches
  `NextEventDot`, not before, and a `$2000` write moves it; power on and the
  reset catch up before the board is reset.

Each new fault was planted in turn and the new tests run (a scratch script,
`/tmp/t2/unitfaults.py`, not kept): the catch-up before cartridge writes taken
out, 4 tests failed; before `$2002` reads (bus and register read), 44;
`NextEventDot` a dot late, 23; the frame end not an event, 39; the catch-up
before power on, 2.

NES tests: 1,621 passed, none failed (1,557 before, 64 new), 7 October 00:04
UTC. `dotnet build 6502.slnx -c Release`: no warnings, no errors.

After the review, three more checks, each shown to fail with its fault planted:

- `ACatchUpOverSeveralFramesEndsEachFrame` now delivers its four frames in
  pieces with nothing caught up and compares `Line` and `Dot` with the ticked
  PPU's after each, with rendering on, so the walk over whole frames in the
  logical position and the odd frames' dropped dot are pinned. Starting the walk
  on the wrong frame parity, or leaving the dropped dot out, fails it on NTSC.
- A scene row with a `$2002` read and two `$2000` writes on the pre-render line's
  dots 335 to 340, rendering on, over twelve frames, so the events are worked
  out on odd frames on both sides of dot 338.
- That row cannot see a wrong boundary at dot 338 (`_dot >= DropDecidedDot` for
  `_dot > DropDecidedDot` in `ScheduleEvents`), and the fault passed it. The
  access sees dot 338 after its cycle's first two dots, and the frame's true last
  dot and the wrong one then both fall in the next cycle, which catches up at its
  end either way, so nothing the bus does shows it. It does show in the logical
  position and `Frame` read between the dots, so a PPU-level test
  (`AnAccessOnThePreRenderLinesLastDotsKnowsWhetherTheFrameDropsItsLastDot`)
  writes `$2000` at each of the pre-render line's dots 335 to 340 on even and odd
  frames, then delivers four dots one at a time and compares the position and
  the frame count with the ticked PPU's. The fault fails it on NTSC.

The "PRG RAM writes mid-line" row has no point at its writes: the bus tells the
observer of cartridge writes at `$8000` and up only, as the differential hashes
them. It checks that the catch-up those writes cause changes nothing seen after.

**The observer's order.** When a frame's last dot is one of the two before a
cycle's access and the access does not catch the PPU up, the lazy build ends the
frame at the end of the cycle, after the observer has been told of the access;
the per-dot reference tells it of the frame end first. The state and the bus's
counts the frame end sees are the same, which is what the differential hashes;
`INesObserver.FrameEnded` says so.

**A board's per-cycle call.** For a board that does not watch the address bus,
the PPU's dots of a cycle can now run after `IMapper.CpuCycle` and after the
access. No board here changes what the PPU reads in that call (MMC1 counts
cycles since a write), but one that did would have to say it watches, or the bus
would have to catch up before the call; `IMapper.CpuCycle` says so.

### The gate

```
dotnet run -c Release --project bench/nes-speed/differential -- --check bench/nes-speed/differential/baseline/4e9b92b.txt
dotnet run -c Release --project bench/nes-speed/differential -- --check bench/nes-speed/differential/baseline/4e9b92b.txt --oracle
```

The lazy build, 7 October 2026, 00:06:01 to 00:11:22 UTC, loads 3.05 to 10.33:
`IDENTICAL: all 332 runs match`. The per-dot reference, 00:11:25 to 00:17:56
UTC, loads 10.38 to 7.75: `IDENTICAL: all 332 runs match`. Again after the
review's fixes, once at the end: the lazy build 01:53:16 to 01:56:48 UTC, loads
2.13 to 2.84, and the per-dot reference 01:56:48 to 02:00:44 UTC, loads 2.84 to
3.69, both `IDENTICAL: all 332 runs match`. NES tests then: 1,625 passed, none
failed (four more: the pre-render row and the drop test, each in both regions).

### The faults pass again

The PPU's faults from task 1 and four new ones in the catch-up, in a scratch
copy (`rsync` of the working tree to `/tmp/nes-faults`, `.testdata` linked).
`faults.py` now takes a fault made of several replacements, and the sprite
evaluation fault reads `_oam`.

```
python3 bench/nes-speed/differential/faults.py /tmp/nes-faults bench/nes-speed/differential/baseline/4e9b92b.txt --threads 7 bg-nametable-late bg-reload-late sprite-eval-late sprite0-hit-next-dot vblank-late ppu-access-dot-late mmc3-a12-late no-catch-up-before-cartridge-write no-catch-up-before-2002-read next-event-one-dot-late no-frame-end-event
```

7 October 2026, 00:18 to 01:04 UTC, loads 4.8 to 2.7. Of 332 runs:

| Fault | Runs changed | Of them synthetic | Only the new state hashes | Task 1's figure |
| --- | --- | --- | --- | --- |
| The background's nametable byte fetched a dot late | 298 | 76 | 226 | 298 |
| The background's shifters reloaded a dot late | 287 | 76 | 5 | 287 |
| Sprite evaluation's first OAM read a dot late | 191 | 76 | 68 | 191 |
| Sprite 0 hit tested against the next column | 42 | 38 | 7 | 42 |
| The VBlank flag set on dot 2 of line 241 | 174 | 53 | 8 | 174 |
| A CPU access to the PPU after three of its cycle's dots | 332 | 76 | 58 | 332 |
| The sprite fetch's address a dot late (MMC3's A12) | 26 | 22 | 2 | 26 |
| New: no catch-up before a write to the cartridge | 54 | 38 | 22 | |
| New: no catch-up before a `$2002` read, by the bus or the read | 284 | 54 | 0 | |
| New: `NextEventDot` a dot late | 54 | 38 | 16 | |
| New: the frame end not an event | 286 | 54 | 199 | |

Every fault is seen by more than 10 runs, so no workload was added. The old
faults change exactly the runs they changed before. The VBlank fault's "only
the new hashes" went from 10 to 8 because on the lazy build it also moves when
the NMI line rises: the catch-up at the predicted dot finds no flag. The
`PPU access` fault still makes `fuzz-mmc3-3` throw in `Ppu.EvaluationStep`,
the reset bug of task 1 (Ruling U, fixed in its own pull request). The 46 runs
the frame end fault leaves alone are the 23 MMC3 cartridges and ROMs in both
regions, which are caught up every cycle, so they never wait for an event; the
same is why the cartridge write and `NextEventDot` faults do not reach them.
The log is committed as
[`bench/nes-speed/lazy-chips/task-2-faults.txt`](../../bench/nes-speed/lazy-chips/task-2-faults.txt).

### The mutation pass, the PPU's rows

The plan's step 5 asks for the 56 mutations of the NES's mutation pass that
touch the PPU to be run again. Their script was a scratch one, never committed,
and it was still on this machine (`/tmp/nes-mut/mutations.py`). Of its 56 rows,
29 touch the PPU or how it is clocked: every row in `Ppu.cs`, `PpuSprites.cs`
and `PpuBackground.cs`, the bus's rows on the dot ratio, the power-on dot phase,
the dots before the access, the NMI line and OAM DMA, PAL's emphasis swap, and
MMC3's A12 rows. Each was applied alone to a scratch copy of this build (row 36
reading `_oam` for `Oam`, after the rename), and the NES tests' unit half was
run (the test ROMs left out, as the pass ran them), 7 October 2026, 01:33 to
01:48 UTC, loads 0.4 to 5.1. **Every one was caught by a unit test; none
survived.** The tests that caught each are committed as
[`bench/nes-speed/lazy-chips/task-2-mutations.txt`](../../bench/nes-speed/lazy-chips/task-2-mutations.txt)
(the row, what it does, the file, failed of 1,301, when, and the tests). Rows 28b
and 28c, the PAL power-on dot phase, are caught by one test each; 32 (the drop
decided at dot 337), 19, 20 and 13 by one or two. The scratch copy was taken
before the review's three new checks, so they are not among the catchers.

### Speed

The thread-time bench (`bench/thread-time`, its README), with three NES
workloads added for this: `ntsc-oracle` and `pal-oracle`, SNOW with
`NesOptions.PerDotReference`, and `lan-ntsc` and `lan-pal`, the homebrew at its
title. Each runs 5 million cycles, then 5 timed runs of 1.79 million. The
baseline is `5e48505`, exported with `git archive` with this bench folder copied
in, as the README says (the `-oracle` workloads stop there with a message, since
the option did not exist). Alternated in four rounds, 7 October 2026, 01:49:24
to 01:50:54 UTC, loads 3.1 to 2.0:

```
git archive 5e48505 | tar -x -C <base>; cp -r bench/thread-time <base>/bench/; ln -s "$PWD/.testdata" <base>/.testdata
(cd <base> && dotnet build bench/thread-time -c Release -o <base>/out)
dotnet build bench/thread-time -c Release -o <after>/out
for round in 1 2 3 4; do
  for bw in "base ntsc" "after ntsc" "after ntsc-oracle" "base pal" "after pal" "after pal-oracle" \
            "base lan-ntsc" "after lan-ntsc" "base lan-pal" "after lan-pal"; do
    set -- $bw; dotnet <$1>/out/Dbhq.Machines.ThreadTime.dll $2 5
  done
done
```

Nanoseconds of thread time a cycle, the median of the four runs' medians,
worked out by a script from the bench's lines, which are committed as
[`bench/nes-speed/lazy-chips/task-2-thread-time.txt`](../../bench/nes-speed/lazy-chips/task-2-thread-time.txt):

| Workload | `5e48505` | this build | this build, per-dot reference |
| --- | --- | --- | --- |
| SNOW, NTSC | 150.5 | 78.6 | 165.8 |
| SNOW, PAL | 150.8 | 79.7 | 165.0 |
| Lan Master, NTSC | 104.3 | 69.9 | |
| Lan Master, PAL | 113.5 | 70.9 | |

Earlier sets the same evening, run with the same workloads in scratch copies of
the bench before they were committed (their lines are in the same file), gave
SNOW 146.2 and 75.2 on NTSC, 142.6 and 71.9 on PAL, the per-dot reference 151.0
and 141.4, and Lan Master 97.6 and 70.6 on NTSC, 107.5 and 67.5 on PAL. The two
sets agree within the bench's noise, which task 17 put at 10 to 20 percent from
one launch to the next; the per-dot reference came out about a tenth slower in
the second.

So natively a cycle of SNOW takes about half the time it did, and of the
homebrew about two thirds. That is far more than the plan expected ("the speed
gain is small"). The per-dot reference of the same build is no faster than
`5e48505`, so the gain is the batching, not some other change. It is the same
dots: at the end of a SNOW run the PPU had run all but the last 159 dots
delivered, with rendering on and the same frame count as the reference (checked
with a scratch print). Why batching is worth so much is reasoned, not measured
(`perf` is not allowed on this machine): a dot's code dispatches on the dot's
place in its 8-dot fetch and on the line, and run between the CPU core's own
dispatch, three times a cycle, those branches are hard to predict; run in a loop
they follow a fixed pattern. The comment on `RenderVisibleDot` already blamed
the 6502's code between two dots for mispredictions. The browser figure (Mono's
AOT) is task 6's, and may differ.

### For the tasks that follow

- The fast scanline renderer of tasks 3 and 4 goes inside `CatchUp`: the loop
  there knows how many dots it owes and that no access falls inside them.
- `LogicalPosition` assumes rendering cannot change while dots are owed. A
  later change that lets anything but a `$2001` write change PPUMASK must catch
  up first.
- MMC3 and any board that watches is still per-dot, so it does not gain. The
  board fields the PPU drives are still hashed at the sound unit's points
  (task 1's note stands).
