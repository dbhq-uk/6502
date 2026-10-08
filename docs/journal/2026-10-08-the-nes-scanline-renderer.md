---
title: "The NES's fast scanline renderer: the gate first"
date: 2026-10-08
summary: "Dan said to merge the lazy PPU and continue, which was read as building the fast scanline renderer the lazy chips plan specified and left unbuilt: a second way to run a line of the PPU, used when nothing can see the line. Before any of it is written, the gate that must hold it equal to the first was made able to see it fail, closing the three gaps the last review named. The differential now hashes the picture as far as it is drawn at every point where the PPU can be seen, not only at the end of a frame. Its sprite 0 cartridges sweep sprite 0 down the whole picture, at every named x, with the left clips on and off, where before they covered the top half. And a new layer of scene tests puts the PPU at any dot directly and makes a register access on every dot of a visible line, the VBlank lines and the pre-render line, in both regions, which no program can do on PAL. Four faults of the kind a fast renderer could make were planted one at a time: the old gate saw one of them, a little; the new one sees all four, the two on PAL's pre-render line through the scene tests alone. The differential's output changed format, so its baseline was recorded again from the per-dot reference, and the lazy build matches it on every run."
order: 38
---

# 8 October 2026: the NES's fast scanline renderer, the gate first

On 8 October Dan merged the lazy PPU (#70), the reset fix (#71) and the
re-recorded baseline (#82), and said "merge and continue". That was read as
continue the speed work: build the fast scanline renderer, tasks 3 and 4 of
[the lazy chips plan](../superpowers/plans/2026-10-06-nes-lazy-chips.md), which
were specified and not built ([#72](https://github.com/dbhq-uk/6502/issues/72)),
on branch `perf/nes-scanline`, cut from `main` at `5640a57`. The renderer is a
second implementation of a line of the PPU, used inside `Ppu.CatchUp` when a
catch-up covers whole lines that nothing can see. It must leave every piece of
the PPU's state at the line's end what the per-dot path leaves, and the line's
pixels the same. The gate that holds it to that is the differential of
[the lazy chips entry](2026-10-06-the-nes-lazy-chips.md), and the final review
of that work named three things the gate could not see. Those are closed first,
in a task of their own, before a line of the renderer is written.

This entry is kept as the work goes, one section a task.

## Task 3a: the gate before the renderer

### What the gate could not see

Issue #72 lists them, from the final review of the lazy chips:

- **The pixels at register points.** The differential hashed the picture at a
  frame's end, and at power on and the reset button, and nowhere else. A fast
  path that writes a line's pixels at another moment than the per-dot path does,
  but ends the frame with the same picture, passed. So did one whose picture is
  wrong only between two points: the page's `Picture()` reads the frame buffer
  after a catch-up, and so do the tests and the tools.
- **The sprite 0 sweep covered only the top half of the picture.** The sprite0
  cartridges set sprite 0's y from the sweep's count, 0 to 119, and its x from
  the frame's low three bits. The count runs to 120, a multiple of 8, so each y
  only ever met one x, and no y below 119 was tried.
- **PAL's pre-render dots 1, 257, 337 and 340 cannot be reached by a program.**
  PAL's cycle is 3.2 dots, 16 dots in 5 cycles, so a program timed from the
  frame puts an access on only 10 of each 16 dots of one line (the lazy chips
  entry, "After the review: synthetic cartridges").

The fourth item in the issue, that MMC3 stays on the per-dot path, is a rule for
the renderer, not a gap: a board that watches the PPU's address bus is caught
up every cycle, so the fast path is never taken for it.

### The pixels at every point

At each point where the PPU can be seen and the picture is not already hashed
(a PPU register access, OAM DMA's writes among them, and a write to the board;
not a frame end, power on or reset), the differential now hashes the picture as
far as it is drawn in the frame, into a new field, `pixels`. A point is not the
place to hash 61,440 pixels: the journal of the lazy chips worked out that this
at every access would make a run hours long. So the hash is kept as it goes, in
`DrawnRows` (`tests/Dbhq.Machines.Nes.Tests/DrawnRows.cs`, linked into the
differential as `StateCompleteness.cs` is):

- each row the PPU has finished in the frame is hashed once, at the first point
  after it is finished, and folded into a running hash with the count of rows;
- the row the PPU is on is hashed whole at each point: the pixels it has drawn
  on it, and those the frame before left, which the per-dot path has not yet
  reached;
- the running hash starts again when the frame's start time changes
  (`Ppu.FrameStartTime`, which moves at each frame end and at the reset button)
  and at power on.

So a frame costs each of its rows once, and a point one row. The whole current
row, not only its drawn part, was the choice because a fast path that renders a
line to its end when the catch-up stops inside it writes the row's later pixels
too early, and those are exactly the ones the drawn part leaves out. The rows
below the PPU's line are not hashed until it is on them; a pixel written there
too early is seen when it reaches them. The pixels and the position are read as
the state stands, with no catch-up (`Ppu.PixelsAsRun`, `Ppu.CaughtUpLine`, new
internal accessors in `src/Dbhq.Machines.Nes/PpuTestHooks.cs`), so a point
cannot change what the lazy build runs; at a point where the PPU can be seen the
bus has caught it up already. The scene tests' recorder (`CatchUpRecorder`) adds
the same hash to each of its PPU points. `DrawnRowsTests` pins what the hash
covers: a pixel of the row the PPU is on, drawn or not, changes it; a row
finished since the last point changes it and a row below does not; a row is
hashed once; in VBlank every row is finished; and it reads without a catch-up.

### The sprite 0 sweep over the whole picture

The sprite0 cartridges now walk sprite 0 through every place on the picture: y
from 0 to 239 at each of the eight x's (0, 1, 7, 8, 128, 248, 254, 255), with
the left clips off and then on. There are 3,840 places; the eight sprite0
cartridges take 480 each, the first four with the clips off and the last four on
(on is both clipped, or the background's or the sprites' alone, by the frame).
The program writes sprite 0 into OAM through `$2003` and `$2004` in VBlank, so
the frame after it shows it; the NMI's OAM DMA comes first, and would show it a
frame late. Rendering is switched off in one frame in four at random (the
`frame_setup` of the lazy chips entry, which moves the odd frame's dot phase), so
the sweep moves on only after a frame that drew its place: `frame_setup` leaves a
flag that says so. 480 drawn frames do not fit in 600, so the sprite0 runs are
900 frames at the default 150 (`Synthetic.Sprite0Times`); the other runs are as
they were.

`--coverage` now counts, for the sprite0 runs of a region together, the places
sprite 0 was drawn at (seen from the `$2002` reads on visible lines with
rendering on), and the places where a read saw it hit. **A mistake, found by
it.** The first version, run the same way from 16:06:07 to 16:08:09 UTC (loads
4.3 to 14.9, `--threads 6`), drew 3,838 of the 3,840 places on NTSC and 3,834 on
PAL. The reset button half way comes after a frame end and before the place the
last NMI set is drawn, but the flag said the frame was drawn, so the place was
passed over. The flag is now cleared at every reset. Then, with

```
dotnet bench/nes-speed/differential/bin/Release/net10.0/Dbhq.Machines.Nes.Differential.dll /tmp/t3a/s0-after2.txt --only synthetic/sprite0 --coverage --threads 4
```

8 October 2026, 16:24:06 to 16:27:17 UTC, loads 2.5 to 18.8, 3 minutes 11
seconds of wall clock and 3 minutes 42 seconds of CPU for the 16 runs:

```
sprite0 runs, NTSC, all together: sprite 0 drawn at 3840 of 3840 places (y 0 to 239, x 0,1,7,8,128,248,254,255, a left clip on and off), a hit read at 3105; never drawn: none
sprite0 runs, PAL, all together: sprite 0 drawn at 3840 of 3840 places (y 0 to 239, x 0,1,7,8,128,248,254,255, a left clip on and off), a hit read at 3105; never drawn: none
```

735 places had no hit read. By the rules, 733 places cannot hit: at x 255
sprite 0 never does (480), at x 0 with a clip on it is wholly clipped (240), and
at y 239 it is below the picture (13 more); that is reasoned, not counted by the
tool, and the 2 left over were not looked into. The same 16 runs took 4
minutes 26 seconds of wall clock and 2 minutes 27 seconds of CPU with the old
sweep, 15:49:12 to 15:53:38 UTC at loads of 4.1 to 43.5 (`--only synthetic/sprite0
--threads 4`); the new ones do half as many frames again and hash the pixels, so
they cost more.

### Every dot of a line, by scene

A program cannot put an access on every dot of PAL's pre-render line, so a test
puts the PPU there. `Ppu.MoveTo(line, dot, oddFrame, status)` (internal, in
`PpuTestHooks.cs`; nothing in the machine calls it) catches up, then sets the
position, the odd frame flag and the three status flags, and leaves the rest
of the state alone; whether the odd frame drops its last dot is worked out as
dot 338 would have. `PpuMoveToTests` pins it.

The driver is `DotScenePair` (`tests/Dbhq.Machines.Nes.Tests/DotScenes.cs`),
written for the renderer tasks to use again. A scene is a start (line, dot,
odd frame, flags), a list of accesses each at a dot offset from the start, and a
length. The pair is two PPUs built the same way: the per-dot reference, which
runs each dot as it is delivered (`Tick`), and the lazy one, which is given the
dots up to each access at once (`Deliver`) and runs them in the access's own
catch-up, as the bus's lazy build does. After each access and at the scene's end
the two must agree on what a read gave, on the logical position, the dots
delivered and run, the next event, the frame count, the state at every frame end
on the way, and the whole state report with the picture. The reports are
compared as bytes (`StateBytes`), field by field, so a failure names the first
field that differs, and for the picture its row and column. Scenes run one after
another on one pair, so a table of thousands costs one setup.

`PpuDotSceneTests` uses it on fourteen kinds of access: a `$2000` write, a
`$2001` write that switches rendering and one that changes the clips, greyscale
and emphasis, a `$2002` read, `$2003` and `$2004` writes, a `$2004` read, the
first and the second `$2005` and `$2006` writes, `$2007` reads and writes, and a
read of the picture, a catch-up with no access at all. Each is put on every dot,
0 to 340, of:

- a visible line, 100, on which sprite 0 hits and the evaluation for the next
  line sets the overflow flag (`TheVisibleLineHasSprite0HitAndOverflowInsideIt`
  checks that both happen inside the line), both regions;
- the pre-render line, on odd and even frames, with rendering on and off, both
  regions (`PpuDotScenePreRenderTests`);
- the VBlank lines 240, 241 and the last before the pre-render line, both
  regions (`PpuDotSceneVblankTests`).

Each scene starts at dot 0 of the line before and ends at dot 0 of the line after
the next, so the catch-up to the access covers a whole line and then the dots of
the access's line before it, and the one after covers the rest of that line and
a whole line more: what a fast path for whole lines would take. **Shorter than
first written.** The first version started two lines before and ended two after,
and cost about a millisecond a scene. A scratch xUnit test, deleted after,
timed a million dots three times each way at 16:01 UTC, at a load of about 27:
`Tick` took 210 to 406 nanoseconds a dot and the same dots in one catch-up 73 to
88; leaving the state comparison out did not change the time, so the dots were
the cost and the scenes were cut to three lines. **A second mistake, found by the
faults below.** The scenes run on one pair one after another, and an access
changes the state for the next: a `$2001` write that switches rendering off left
it off for every later scene, and a `$2000` write of a fixed value changed
nothing after the first. Each scene now starts by writing its own PPUCTRL and
PPUMASK.

Today these scenes can only agree: the lazy PPU's catch-up runs the same per-dot
code. They are there for the renderer, whose fast path will live in the catch-up
and so only on the lazy side of the pair.

### Faults that only the new coverage sees

A fault in the per-dot code changes the lazy build and the per-dot reference
alike, so it cannot be seen by a test that holds one to the other; only the
differential, against a baseline recorded from the right code, sees it. A fast
renderer's mistakes will be on the lazy side alone. So the four new faults in
`faults.py` act only inside a catch-up of more than four dots, which the lazy
build makes and the per-dot reference never does, and each leaves the state as
the right code leaves it once the catch-up goes past the dot, so only a point
inside the window sees it:

- `batch-pixel-one-off`: on dot 130 of a visible line, column 128's pixel is
  written one pixel off, into column 129, and put right on dot 131;
- `batch-sprite0-hit-late-low`: with sprite 0's y above 120, the hit is set a dot
  late;
- `batch-pal-prerender-337-early`: on PAL's pre-render line, dot 337's shift and
  reload are done at the end of dot 336;
- `batch-pal-prerender-340-early`: on PAL's pre-render line, the odd frame flag
  turns as the PPU reaches dot 340, not with the frame's end after it.

**A third mistake.** The first version kept the faults' flags in `[ThreadStatic]`
statics, so as not to add fields that the state reports must name. The two PPUs
of a scene pair run on one thread and shared them, and a flag left set by one
test leaked into the next: the PAL-only fault failed NTSC rows. They are now
fields of the PPU, which each fault also skips in the PPU's report, so the
reflection check passes and no hash changes. `faults.py` also takes `--tests
<filter>`, which runs the NES tests that match with each fault in, and
`--no-differential`.

**The new faults, the old gate against the new.** The old gate is the tree at
`5640a57` (format 2, the old sweep, no scene tests) with each fault planted,
against `38c5544.txt`, and its unit tests that compare the lazy PPU with the
per-dot one (`PpuCatchUpTests`, and `StateReport` as a check that a fault's
field breaks nothing: 114 tests). The new gate is the tree at `acf9748` against
`acf9748.txt`, and the same classes with the scene tests (284 tests). 8 October
2026, 16:31 to 19:16 UTC, in scratch copies (`git archive` or `rsync`,
`.testdata` linked, removed afterwards), at loads of 1 to 49; the whole log is
[`bench/nes-speed/lazy-chips/task-3a-faults.txt`](../../bench/nes-speed/lazy-chips/task-3a-faults.txt).
Of 332 runs:

| Fault | Old gate: runs | Old unit tests failing | New gate: runs | Of them, seen only by `pixels` | New unit tests failing |
| --- | --- | --- | --- | --- | --- |
| `batch-pixel-one-off` | 6 | 0 | 113 | 107 | 26 |
| `batch-sprite0-hit-late-low` | 9 | 0 | 19 | 0 | 0 |
| `batch-pal-prerender-337-early` | 8 | 0 | 4 | 0 | 14 |
| `batch-pal-prerender-340-early` | 68 | 0 | 66 | 0 | 28 |

What each shows:

- **The pixel fault** is what the `pixels` hash is for: 107 of the 113 runs that
  see it see it nowhere else. The old gate saw it in 6, through the frame's
  picture: when a `$2001` write switches rendering off on exactly dot 131, the
  fault's put-right never runs and the pixel stays wrong. Of the scene tests, 12
  of the 14 kinds on the visible line catch it in both regions, and two bus-level
  scenes (`OAM DMA and $2003 and $2004 while rendering`) through the recorder's
  new hash; the two kinds that do not, the `$2001` write that switches rendering
  and the second `$2005` write, have the two columns the same colour in their
  scenes (a transparent background pixel shows the backdrop), so a one-pixel
  fault there changes no colour.
- **The sprite 0 fault** is the full sweep's: 19 runs, all synthetic, against
  9. The fuzz runs, whose OAM is random, see it in both; the sprite0 runs, whose
  sprite 0 now goes below line 120, account for 12 of the 19 and 2 of the 9. The
  two MMC3 sprite0 cartridges cannot see it: a board that watches the PPU's
  address bus is caught up every cycle, so there is never a batch to be wrong
  in. No unit test sees it: the scenes' sprite 0 is at y 99. That is left for task 4,
  which puts sprites on the fast path and needs scenes with sprite 0 down the
  picture.
- **The PAL pre-render faults** are the scene tests': the dot 337 fault fails all
  14 kinds on PAL's pre-render line with rendering on, and the dot 340 fault all
  28 PAL pre-render rows, rendering on and off. The old unit tests saw neither.
- **A correction.** The lazy chips entry found that a program cannot reach dots
  1, 257, 337 and 340 of PAL's pre-render line. That was a count of the writes
  in the synthetic runs. The differential sees the dot 337 fault in 4 runs (8 in
  the old gate) and the dot 340 fault in 66 (68), so programs do get there: by
  reads, mostly of `$2002` in the test ROMs' VBlank loops for dot 340, and
  through the reset button half way, which puts the PPU back at line 0 dot 0
  while the bus's dot accumulator goes on, so the run after it has another of
  PAL's dot phases. Which runs those are depends on the moment of the reset, not
  on the program, so no workload can be written to reach them: on one dot phase
  a program reaches the same 10 of each 16 dots of a line whatever it does. The
  dot 337 fault is seen by fewer than ten runs of the differential, which the
  plan's rule would answer with a new workload; here the answer is the scene
  tests, which are the gate for those dots.

**The old faults against the new gate.** The fourteen faults of task 1 and task
2, in the same scratch copies, against `acf9748.txt`, with the unit tests above:

| Fault | Runs changed | Of them synthetic | `pixels` changed in | Task 2's count | Unit tests failing (of 284) |
| --- | --- | --- | --- | --- | --- |
| The background's nametable byte fetched a dot late | 298 | 76 | 72 | 298 | 0 |
| The background's shifters reloaded a dot late | 287 | 76 | 280 | 287 | 0 |
| Sprite evaluation's first OAM read a dot late | 191 | 76 | 133 | 191 | 2 |
| Sprite 0 hit tested against the next column | 42 | 38 | 35 | 42 | 0 |
| The VBlank flag set on dot 2 of line 241 | 171 | 50 | 163 | 174 | 68 |
| A CPU access to the PPU after three of its cycle's dots | 332 | 76 | 332 | 332 | 0 |
| Every frame counter step a cycle late | 332 | 76 | 38 | 332 | 0 |
| A DMC reload's fetch allowed to halt the CPU a cycle later | 81 | 24 | 42 | 81 | 0 |
| The sprite fetch's address a dot late (MMC3's A12) | 26 | 22 | 24 | 26 | 0 |
| MMC1 taking the write on the cycle straight after a write | 20 | 20 | 20 | 20 | 0 |
| No catch-up before a write to the cartridge | 54 | 38 | 51 | 54 | 2 |
| No catch-up before a `$2002` read | 284 | 54 | 284 | 284 | 54 |
| `NextEventDot` a dot late | 52 | 36 | 30 | 54 | 20 |
| The frame end not an event | 286 | 54 | 0 | 286 | 41 |

Each changes as many runs as before, but for the VBlank and `NextEventDot`
faults, which change three and two fewer. Those are sprite0 runs, whose programs
are new: in every other run each format 2 hash is as it was, so a run a fault
changed before it changes now, and the `pixels` hash can only add runs. The unit test column counts only the classes that hold
the lazy PPU to the per-dot one and the state reports, so a fault in the per-dot
code shared by both, which the NES's other unit tests catch (the mutation pass
of task 2), shows 0 there; `sprite-eval-late`'s 2 are the scene setup's own check
that the overflow flag is set inside the visible line.

### The baseline, format 3

A new field is a new format (`# nes-differential format 3`), so the baseline was
recorded again, as PR #82 did it: from the code at the commit that has the new
harness, `acf9748`, with the per-dot reference, the file named for that commit,
the lazy build checked against it, and the old file removed.

```
dotnet run -c Release --project bench/nes-speed/differential -- bench/nes-speed/differential/baseline/acf9748.txt --oracle
```

8 October 2026, 16:31:30 to 16:44:01 UTC, 12 minutes 31 seconds of wall clock
and 14 minutes 34 seconds of CPU, at loads of 2.7 to 21.0: 332 runs, 98,355
bytes (`38c5544.txt` was 90,387; recorded the same way it took 13 minutes 38
seconds of CPU). A script compared the two files field by field: in the 316 runs
that are not sprite0 runs, every one of format 2's twelve fields is the same as
in `38c5544.txt`, so the new field and the harness changed nothing else; all 16
sprite0 runs differ, because their programs changed.

```
dotnet run -c Release --project bench/nes-speed/differential -- --check bench/nes-speed/differential/baseline/acf9748.txt --out /tmp/t3a/lazy-check.txt
dotnet run -c Release --project bench/nes-speed/differential -- --check bench/nes-speed/differential/baseline/acf9748.txt --oracle --out /tmp/t3a/oracle-check.txt
```

The lazy build, 16:47:16 to 17:03:42 UTC, 16 minutes 25 seconds of wall clock
and 13 minutes 26 seconds of CPU, loads 4.7 to 8.9: `IDENTICAL: all 332 runs
match`, and its output was the file byte for byte (`cmp`). The per-dot reference, 17:07:05 to 17:32:54 UTC, 25 minutes 50 seconds of wall
clock and 15 minutes 53 seconds of CPU, loads 18.9 to 34.8: `IDENTICAL: all 332
runs match`, and its output was the file byte for byte too. `38c5544.txt` was
removed.

**How long the gate takes now.** The agents on this machine share a limit of
four processors, and the load was high all afternoon, so the wall clock says
little; the CPU time is the better measure. Recording `acf9748.txt` took 14
minutes 34 seconds of CPU, against 13 minutes 38 seconds for `38c5544.txt`
recorded the same way: the `pixels` hash and the longer sprite0 runs cost about
a fifteenth more. At four threads on four free processors that is about four
minutes of wall clock. The NES tests take about two minutes more than before for
the scene tests (the run of the filtered classes above took 1 minute 30 seconds
alone).

### The tests

```
dotnet test tests/Dbhq.Machines.Nes.Tests -c Release
```

Before the work, 8 October 2026, 15:49:11 to 15:55:21 UTC, loads 4.5 to 29.1:
1,627 passed, none failed. With it, on the final code, 18:48:20 to 18:55:18 UTC,
loads 25.7 to 19.4: 1,809 passed, none failed. The new ones are
`PpuDotSceneTests`, `PpuDotScenePreRenderTests`, `PpuDotSceneVblankTests`,
`PpuMoveToTests` and `DrawnRowsTests`. `dotnet build 6502.slnx -c Release`: no
warnings, no errors.

### For the renderer tasks

- Use `DotScenePair` for the fast path against the exact path: a scene is a
  start and a list of accesses at dot offsets. The fast path will run on its
  lazy side only.
- The sprite 0 fault was seen by the differential and by no unit test: the
  scenes' sprite 0 is at y 99. Task 4, which puts sprites on the fast path, needs
  scenes with sprite 0 across the picture's height.
- The PAL pre-render faults are seen by every scene test that reaches the dot,
  and by the differential only where the reset button happens to move PAL's dot
  phase onto it; for those dots the scene tests are the gate.
- MMC3, and any board that watches the PPU's address bus, stays on the per-dot
  path; the fast path must refuse it.
