---
title: "The NES's fast scanline renderer: the gate first, then the lines without sprites"
date: 2026-10-08
summary: "Dan said to merge the lazy PPU and continue, which was read as building the fast scanline renderer the lazy chips plan specified and left unbuilt: a second way to run a line of the PPU, used when nothing can see the line. Before any of it is written, the gate that must hold it equal to the first was made able to see it fail, closing the three gaps the last review named. The differential now hashes the picture as far as it is drawn at every point where the PPU can be seen, not only at the end of a frame. Its sprite 0 cartridges sweep sprite 0 down the whole picture, at every named x, with the left clips on and off, where before they covered the top half. A new layer of scene tests puts the PPU at any dot directly and makes register accesses on every dot of visible lines, the VBlank lines and the pre-render line, in both regions. Four faults of the kind a fast renderer could make were planted one at a time. The old differential saw each of them, two in only a handful of runs, and the old unit tests saw none. The new differential sees the pixel fault in many times as many runs and the sprite 0 fault in twice as many, and the scene tests catch all four. The review of this work found holes in the scene layer: sprite 0 hit its line a line early because the scenes kept the last scene's sprites, and the scenes left the right pixels behind. Both were fixed, the scenes were widened, and the differential now presses reset a second time on a dot phase chosen for each run, so every dot of PAL's pre-render line is reached by programs as well as by the scenes. The differential's output changed format twice, so its baseline was recorded again from the per-dot reference each time, and the lazy build matches it on every run. Then the renderer itself was built for the lines that draw no sprite: the lines after the picture and in VBlank, the visible lines with rendering off, and those with the background alone, on which sprite evaluation still runs and is run with the per-dot code. The gate stayed identical to its baseline, and every fault planted in the new code was caught both by the differential and by new scene tests that also say how many lines the fast path must take. Against main, in the browser, the homebrew's title screen runs about a third faster and SNOW, whose every visible line has sprites, no faster at all, so the stopping rule set before the work was reached. After its review the new tests ran under every nametable layout, and the differential gained cartridges that scroll the background alone, which the renderer's faults now show in many more runs."
order: 38
---

# 8 October 2026: the NES's fast scanline renderer, the gate first, then the lines without sprites

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
  in. No unit test saw it in this first version: the scenes' sprite 0 was at y
  99. After the review the scenes cover lines 200 and 239, and catch it.
- **The PAL pre-render faults** are the scene tests': the dot 337 fault fails all
  14 kinds on PAL's pre-render line with rendering on, and the dot 340 fault all
  28 PAL pre-render rows, rendering on and off. The old unit tests saw neither.
- **A correction.** The lazy chips entry found that a program cannot reach dots
  1, 257, 337 and 340 of PAL's pre-render line, from the writes of the
  synthetic runs. The differential sees the dot 337 fault in 4 runs (8 in the
  old gate) and the dot 340 fault in 66 (68), so runs do get there, and the way
  in is the reset button half way. A read lands at the same point in its cycle
  as a write, so reads reach no other dots; but the reset puts the PPU back at
  line 0 dot 0 while the bus's dot count goes on, so the run after it is on
  another of PAL's dot phases, with another 10 of each 16 dots in reach. Which
  phase a run gets depends on the moment of the reset, not on the program. The
  dot 337 fault was seen by fewer than ten runs, and the review asked for the
  phase to be chosen rather than left to chance: see "After the review".

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

(Replaced after the review by a format 4 file: see "After the review".)

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
minutes of wall clock. The scene tests in this first version, with the hook's and
the drawn rows' (182 tests, `dotnet test ... --filter
"FullyQualifiedName~PpuDotScene|FullyQualifiedName~PpuMoveTo|FullyQualifiedName~DrawnRows"`),
took 1 minute 30 seconds of test time at 16:53 UTC at a load of 14.7; the larger
set after the review is timed in "After the review".

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

### After the review

The review of task 3a found the differential's side and the baseline sound and
the scene layer short of what the renderer will need. Each finding, and what was
done:

- **Sprite 0 hit the scenes' line a line early.** `MoveTo` left the sprite line
  as it was. The scene before ends at dot 0 of the line after the next, with
  sprite 0 fetched for that line, so the next scene's start drew it on the line
  before its line, and the hit was already set as its line began: the reviewer
  counted 340 of the 341 scenes so, in both regions. The sanity test built a
  fresh PPU for itself and so could not see it. `MoveTo` now leaves no sprite
  fetched, as `Reset` does (`PpuMoveToTests.ItLeavesNoSpriteFetchedForTheLineItPutsThePpuOn`),
  and the visible-line tests check every scene, run in sequence as the tables
  run them: sprite 0 hit and overflow are clear as the line begins, and, for the
  accesses that cannot change them (a `$2002`, `$2004` read, a `$2003` write, the
  picture read), both are set inside it. **The first run of that check failed**
  after six scenes: `v` too carried from scene to scene, so each scene drew
  another row of the background, and on most of them sprite 0 met no opaque
  pixel. Each visible-line scene now sets `v` first, by a mid-frame scroll's
  writes (`$2002`, `$2006`, `$2005`, `$2005`, `$2006`), to what the frame has on
  the line before, so every scene draws the same lines.
- **Pixels left right by the scene before.** Each scene now starts with every
  pixel of both PPUs set to a colour the PPU never draws (`DotScenePair.Unpainted`,
  alpha 0), so a pixel the fast path should draw and does not is seen.
- **Wider scenes.** The fourteen kinds now run on every dot of lines 1, 100, 200
  and 239, and of line 150 with `v` set so that coarse Y wraps from 29 at its
  dot 256 (`OnTheWrapLineCoarseYWrapsFrom29InsideTheLine` checks that it does),
  on NTSC on odd frames as well as even. A `$2002` read on every dot of line 200
  runs with sprite 0 at each of the sprite0 workload's x's (0, 1, 7, 8, 128,
  248, 254, 255) under each of PPUMASK `$1E`, `$18`, `$1A` and `$1C`
  (`PpuDotSceneSprite0Tests`). And each kind is made twice on line 100, on dot d
  and on d + k for k of 1, 3, 8 and 64, so a catch-up that starts and ends inside
  the line is tested (`PpuDotSceneTwiceTests`). `Busy` and `OamByte` take the
  line and sprite 0's x.
- **Offsets that check themselves.** An access can carry the position it must be
  made at (`DotAccess.At`), and the driver fails if the offset puts the PPU
  elsewhere. Every scene's accesses carry it. On NTSC's odd frames with rendering
  on the pre-render line has no dot 340, and the scene for it lands on line 0 dot
  0 of the next frame; it is now named so.
- **`MoveTo`'s comment** says it leaves `_timeBase` alone (the time jumps with the
  position; both PPUs jump the same), and the scene tests' comment no longer says
  PAL's four dots are out of a program's reach.

```
dotnet test tests/Dbhq.Machines.Nes.Tests -c Release --no-build --filter "FullyQualifiedName~PpuDotScene|FullyQualifiedName~PpuMoveTo"
```

8 October 2026, 19:53:28 to 19:54:01 UTC, load 1.5: 463 passed, 29 seconds of
test time, 1 minute 20 seconds of CPU. The whole NES project, 20:00:20 to
20:01:46 UTC, loads 0.8 to 3.7: 2,098 passed, none failed, 1 minute 19 seconds
of test time.

**PAL's dot phase chosen, not left to chance.** The dot 337 fault was seen by 4
runs, 8 before the sprite0 runs went to 900 frames: that moved their reset from
frame 300 to frame 450, and with it the dot phase they had after it. The
homebrew's and the synthetic runs now press the reset button a second time, at
three quarters of the run. On PAL the harness first steps instructions until
the bus's dot count (`NesBus.PpuDots`) is, mod 16, the run's class: PAL's 16 dots
in 5 cycles leave the count on 0, 3, 6, 9 or 12 mod 16 at the end of an
instruction, and the runs take those in turn by their place in the list. The
reset puts the PPU at line 0 dot 0 on that phase. On NTSC a cycle is 3 whole dots,
a reset cannot move the phase, and the button is pressed at once. The 127
pinned ROMs' runs press it once, as before, and their lines did not change. The
window workloads (scroll, mask, sprites, apu) also begin the window before PAL's
pre-render line again at their third start, so that they sweep that line on the
chosen phase; they count their starts in RAM, which the reset keeps. That is
format 4 (`# nes-differential format 4`): the fields are format 3's.

```
dotnet bench/nes-speed/differential/bin/Release/net10.0/Dbhq.Machines.Nes.Differential.dll /tmp/t3a/cov2.txt --only synthetic/ --coverage --threads 4
```

19:55:10 to 19:56:56 UTC, loads 1.2 to 4.0, a new summary line (any access, a
read or a write, with rendering on):

```
synthetic runs, PAL, all together: pre-render line, any access with rendering on: runs reaching dot 0: 29, 1: 15, 2: 26, 255: 31, 256: 31, 257: 19, 258: 33, 320: 31, 337: 20, 338: 33, 339: 31, 340: 23; dots no run reaches: none
```

and the same for NTSC, with no dot unreached; the sprite0 runs still draw 3,840
of 3,840 places in each region.

**The baseline, format 4**, from the commit with the code, `034b43f`, as before:

```
dotnet run -c Release --project bench/nes-speed/differential -- bench/nes-speed/differential/baseline/034b43f.txt --oracle
```

20:03:53 to 20:10:43 UTC, 6 minutes 50 seconds of wall clock and 13 minutes 59
seconds of CPU, loads 1.1 to 1.5: 332 runs, 98,354 bytes. Against `acf9748.txt`,
the 254 pinned ROM runs are the same line for line and the 78 homebrew and
synthetic runs all differ. `acf9748.txt` was removed.

```
dotnet run -c Release --project bench/nes-speed/differential -- --check bench/nes-speed/differential/baseline/034b43f.txt --out /tmp/t3a/lazy4.txt
dotnet run -c Release --project bench/nes-speed/differential -- --check bench/nes-speed/differential/baseline/034b43f.txt --oracle --out /tmp/t3a/oracle4.txt
```

The lazy build, 20:13:20 to 20:18:57 UTC, 5 minutes 37 seconds of wall clock and
12 minutes 34 seconds of CPU, loads 0.6 to 2.9, and the per-dot reference,
20:23:06 to 20:29:03 UTC, 5 minutes 57 seconds and 13 minutes 29 seconds, loads
2.3 to 3.9: each `IDENTICAL: all 332 runs match`, each output the file byte for
byte.

**The batch faults again.** With the scene tests widened, each of the four is
caught by them, the sprite 0 fault for the first time. The NES tests matching
`PpuDotScene`, `PpuCatchUpTests`, `StateReport` and `PpuMoveTo`, 577 of them,
with each fault in, 20:04 to 20:10 UTC:

| Fault | Tests failing (of 577) | Before the review (of 284) |
| --- | --- | --- |
| `batch-pixel-one-off` | 320 | 26 |
| `batch-sprite0-hit-late-low` | 114 | 0 |
| `batch-pal-prerender-337-early` | 14 | 14 |
| `batch-pal-prerender-340-early` | 28 | 28 |

And the differential, the tree at `034b43f` with each fault planted, against
`034b43f.txt` (`faults.py <scratch copy> bench/nes-speed/differential/baseline/034b43f.txt
--threads 2 <the four>`), 20:13 to 20:41 UTC, loads 0.6 to 3.9. Of 332 runs:

| Fault | Runs changed | Of them seen only by `pixels` | In format 3 |
| --- | --- | --- | --- |
| `batch-pixel-one-off` | 113 | 107 | 113 |
| `batch-sprite0-hit-late-low` | 17 | 0 | 19 |
| `batch-pal-prerender-337-early` | 17 | 0 | 4 |
| `batch-pal-prerender-340-early` | 77 | 0 | 66 |

The dot 337 fault, which only the phase reaches, is now seen by 17 runs, 16 of
them synthetic, each a PAL run. The sprite 0 fault lost two runs to the new
reset (17 against 19); its sprite0 runs still see it. Both logs are appended to
[`bench/nes-speed/lazy-chips/task-3a-faults.txt`](../../bench/nes-speed/lazy-chips/task-3a-faults.txt).

### For the renderer tasks

- Use `DotScenePair` for the fast path against the exact path: a scene is a
  start and a list of accesses at dot offsets. The fast path will run on its
  lazy side only.
- Every batch fault is caught by the scene tests and by the differential; keep
  it so for each fault planted in the fast path.
- The scenes check their own positions and, on the visible lines, that sprite 0
  hit and overflow happen inside the line; a new family should do the same.
- The scene tests take about half a minute of test time; a wider table should
  say what it costs.
- MMC3, and any board that watches the PPU's address bus, stays on the per-dot
  path; the fast path must refuse it.

## Task 3: the fast scanline renderer, idle and background lines

The renderer itself, for the lines that need no sprite drawing: commit
`779523e`, `src/Dbhq.Machines.Nes/PpuScanline.cs`. `Ppu.CatchUp` asks
`TryRenderLine` at dot 0 of a line whenever the dots owed reach past the line's
last dot. It runs the whole line in one call and leaves the PPU as the per-dot
path (`RunDot`) leaves it at the line's end, or it declines and changes
nothing. The per-dot path is unchanged, apart from a count of the lines it
finishes, and stays the reference and the fallback.

### When a line is taken, and when it is refused

A catch-up never runs past a register access, an OAM DMA write, a write to the
cartridge, `NextEventDot` or the frame's end, because the bus catches up before
each of them (task 2). So a catch-up that owes a whole line from its dot 0 owes
a line that nothing can see inside. That is the first condition, and the only
one about time. The line is then taken when all of these hold:

- the switch `Ppu.WholeLines` is on (it is, but for the tests that turn it off);
- the board does not watch the PPU's address bus (MMC3 is caught up every cycle
  and never owes a whole line; the renderer refuses it as well, because it tells
  the board nothing);
- the line is one of:
  - a line after the picture or in VBlank, but not the pre-render line. On such
    a line the per-dot path changes only the position, and on line 241 the
    VBlank flag at dot 1, unless a `$2002` read stopped it;
  - a visible line with rendering off. Columns 0 to 255 get the backdrop, or the
    palette entry `v` points at (no dot of the line moves `v`), and dot 257
    leaves no sprite for the next line;
  - a visible line with the background on and the sprites off, on a board that
    keeps its pattern tables as windows and its nametables as a page table.

Everything else goes down the per-dot path:

- the pre-render line, with its vertical copy, its flags at dot 1 and the odd
  frame's dropped dot, which this task leaves alone, as the plan says;
- every visible line with the sprites on, which is task 4's;
- a background line on a board without windows, whose every fetch would be a
  call to the board;
- a line a catch-up starts or ends inside.

Refusing changes nothing, so a refusal is never wrong, only slower. Each
condition has its own test (below).

### What the plan said and what the code said

The plan's brief took the third kind of line as "no sprites on the line and
sprites off". The code and the sheet say more. `RunDot` runs sprite evaluation
and the sprite fetches whenever either layer is on (`(_mask & 0x18) != 0`), and
`ppu.md` 7 says the same ("Evaluation runs if either layer is enabled"). So a
background-only line still fills secondary OAM, sets the overflow flag, lays the
next line's sprites into the line buffer, holds OAMADDR at 0 and moves the
fetch latches and `$2004`'s latch. All of that is state, and all of it is in the
state report.

There were three ways to handle this:

- refuse every line whose evaluation finds a sprite;
- write a second implementation of evaluation;
- run evaluation and the fetches with the per-dot code itself, after the
  background.

The third was chosen. Inside a line with no access, evaluation and the
background share nothing:

- evaluation reads OAM, the line and PPUCTRL, and writes its own fields and the
  overflow flag;
- the background reads `v`, `t`, fine X, the nametables and the pattern tables;
- with the sprites off, the pixel never looks at a sprite, and sprite 0 cannot
  hit (`x >= _spritesFrom` is never true when `_spritesFrom` is 256).

So the order they run in inside the line cannot matter, and the per-dot code
gives exactly what the per-dot path gives. `EvaluateLine` stops reading once the
search is over, because after that every odd dot reads the same OAM byte again:
`n` and `m` no longer move. `FetchSpritesForNextLine` calls `FetchSprite` for
the 64 dots of the fetches. With the sprites off, then, "no sprites on the line"
is not needed. A line's sprites can still be taken by this path, and nothing of
them reaches the picture.

### Decisions, and what they were chosen over

- **The background is the second implementation, and the sprites are not.** The
  background loop draws the 8 columns from 8k from one value of the shifters:
  column 8k + i is the shifters' pixel 15 - fine X - i, then 8 shifts and the
  reload of the tile just fetched. That is what dots 8k + 2 to 8k + 9 do one dot
  at a time. Then come dot 256's Y increment, dot 257's horizontal copy, and the
  next line's two tiles, which leave the first tile in the shifters' high half
  and the second in their low half, with the latches as the second's fetch
  leaves them. Writing evaluation twice would double the code the gate must
  hold equal, for no speed on these lines. Task 4 can still write a second
  evaluation if sprite lines need one.
- **One fetch helper (`FetchTile`)** is used for the 32 tiles of the line and the
  two of the next. Each read is an array read from the page table and the
  windows, with no call to the board. One helper means one place where a fault
  can go, and one place to get the attribute quadrant right. `Tile` writes
  `Reload`'s expression again rather than changing the per-dot `Reload`, so the
  per-dot path is untouched.
- **Refused in the renderer as well as by the bus.** A board that watches is
  caught up every cycle, so it never owes a whole line. The renderer still
  checks `_watchesAddresses`, so that a future caller of `CatchUp` with a long
  span cannot skip the board's address reports.
- **The question is asked only at dot 0 with a line owed**: one compare per dot
  in the per-dot loop, and nothing else changed in it.
- **Bookkeeping, not state.** `_wholeLines`, `_fastLines` and `_exactLines` are
  skipped in `PpuState.cs` with a reason, so the completeness check passes and
  no hash changes. The differential's `--coverage` prints the share of the
  lines each run took (below). Nothing in the machine reads them.
- **A test board like the real ones.** `TestMapper` gained `Quiet` (it does not
  watch) and `Windowed` (windows and a page table). Its default stays the old
  board, which watches, so every test written before runs as it did, and the
  scenes of task 3a still never take the fast path. `PpuScene` and `Busy` take a
  `quiet` flag. `DotAccess` gained a `Cartridge` kind, a CPU write to the board
  after a catch-up as the bus makes it, through a new hook, `Ppu.Board`.
- **One commit for the tests and the renderer.** The tests say how many lines
  the fast path must take, so against a stub that always declines they fail:
  353 of them did, each on that count (`bench/nes-speed/lazy-chips/task-3-red.txt`).
  Each commit must be green, so the tests went in with the renderer. The red
  run is the record that they can fail.
- **The browser bench takes the homebrew.** `run-in-browser.mjs` has
  `ROM=homebrew`, which serves Lan Master from `roms/nes/`, checked against its
  pin. The stopping rule (below) asks for the browser figure, and SNOW alone
  turned out to be the one workload this task cannot help.

### The tests

`PpuScanlineTests`. Every family runs on the per-dot reference (`Tick`) and the
lazy PPU (`Deliver` and the catch-ups) with `DotScenePair`. The two must have
the same state and picture after every access and at every line's end, and the
family says how many lines the fast path must take. The model of the refusal
conditions in the tests is `Fast(region, line, mask)`: every line but the
pre-render line that is after the picture, or visible with the sprites off. The
families:

- **The background across the scroll table**: every fine X; coarse X 0 to 3 and
  29 to 31; coarse Y 0 to 3, 27 to 31, each with fine Y 0 and 7 (so the wrap
  from 29 and the attribute rows 30 and 31 are both crossed); and each
  nametable. They run under nine masks: the left clip on and off, the sprite
  clip bit, greyscale, each emphasis bit and all three. PPUCTRL rotates the
  background's pattern table, 8 by 16 sprites and NMI, on lines 1, 100, 200 and
  239, in both regions. The same table runs on a real NROM board.
- **The lines after the pre-render line**, on odd and even frames, under 23
  masks: an odd NTSC frame with rendering on drops its last dot, and line 0
  still starts whole.
- **Rendering off**: `v` at the backdrop and at every kind of palette address
  (`$3F00`, `$3F10`, `$3F13`, `$3F1F`, `$3FE7`, `$7F0A` with bit 14 set) and at
  addresses outside it, under seven masks. Also the backdrop written by `$2007`
  at a line's dot 0, which moves `v` to the next entry by 1 or by 32.
- **Idle lines** from line 238 through the pre-render line into the next frame,
  with NMI on and off, rendering off, background only and everything on. They
  run again with `$2002` read at every line's end. Separately, a `$2002` read on
  line 240 dot 340 and on line 241 dots 0 to 3: on dot 1, the read stops the
  flag and line 241 is not whole.
- **Sprite lines refused** under seven masks with the sprites on.
- **Review Focus 1**: each of task 3a's 14 kinds of access on every dot of lines
  1, 100 and 239 with the background alone, in both regions. The line before
  and the line after are taken. The access's own line is taken only when the
  access is on its dot 0.
- **Review Focus 2**: `$2001` switching among off, the background with and
  without the clip, everything, and the sprites alone, on the line before's
  dots 339 and 340 and the line's dots 0 and 1. Also on the pre-render line's
  dots 336 to 340 of odd and even frames, around dot 338, where the drop is
  decided.
- **CNROM**: four banks of CHR ROM whose bytes differ, switched at a line's dot
  0 (the line is taken, from the new bank) and inside the next line (the
  per-dot path runs it). A check confirms that the four banks draw the line
  four different ways.
- **Review Focus 5**: three frames and a part in one catch-up, with every frame
  end compared on the way.
- **The edges of the conditions**: a line in two catch-ups of 340 dots and 1 dot
  is not taken; the switch; a board that watches (never taken); a quiet board
  without windows (idle and rendering-off lines taken, background lines
  refused).
- **The bus**: the lazy machine on NROM takes more than three quarters of the
  lines in reach, and the per-dot reference and MMC3 take none. Five new bus
  scenes run in the old scene table too: no access with the background alone,
  NMI on and off; the key-dot scroll writes with the background alone; `$2001`
  switched at line edges among the background, off and everything; and CNROM
  bank writes at line starts.
- **No allocation**: four frames of whole lines, measured as
  `SampleBufferTests` measures.

Against the stub, 353 of the 562 tests in `PpuScanlineTests`,
`PpuCatchUpTests` and `StateReport` failed, every one on the count (8 October
2026, 21:19 UTC). With the renderer, all 562 passed at the first run. On the
final code the whole NES project gave 2,548 passed and 0 failed (task 3a left
2,098), at 21:32 to 21:34 UTC:
`dotnet test tests/Dbhq.Machines.Nes.Tests -c Release`. One earlier full run
failed `SampleBufferTests.AddingAllocatesNothing` by 3,896 bytes. That is the
runtime's own allocation, which the test's comment records from 6 October with
the same count. It passed on three reruns of its class and in the final full
run, and it has nothing to do with the PPU.

### The gate

On 8 October 2026, from `bench/nes-speed/differential`:

- `dotnet run -c Release -- --check baseline/034b43f.txt --coverage --out <file>`
  gave IDENTICAL on all 332 runs, 21:27:17 to 21:31:24 UTC, 4m07s wall, loads
  2.1 to 3.3.
- `dotnet run -c Release -- --check baseline/034b43f.txt --oracle --out <file>`
  gave IDENTICAL on all 332 runs, 21:34:34 to 21:39:06 UTC, 4m32s wall, loads
  2.5 to 3.8.

Both outputs are the baseline byte for byte (`cmp`). Both were run again on
the final tree, `e82e846`, on 9 October 2026: lazy, 00:07:20 to 00:11:43 UTC;
`--oracle`, 00:11:43 to 00:16:31 UTC; loads 0.6 to 3.1. Both gave IDENTICAL on
all 332 runs, byte for byte. The baseline did not change: nothing new is
hashed, and the behaviour is the same.

### Faults in the fast path

Ten faults were planted in `PpuScanline.cs`, one at a time, by `faults.py` in a
scratch copy. Each was run against `034b43f.txt`, with the tests that hold the
lazy PPU to the per-dot one (`PpuScanlineTests`, `PpuDotScene*`,
`PpuCatchUpTests`, `StateReport` and `PpuMoveTo`: 1,027 tests). That was 21:39
to 22:32 UTC, loads 1.1 to 3.8; the log is
[`bench/nes-speed/lazy-chips/task-3-faults.txt`](../../bench/nes-speed/lazy-chips/task-3-faults.txt).
Of 332 runs:

| Fault | Runs changed | ROMs | Of the runs, synthetic | Tests failing (of 1,027) |
| --- | --- | --- | --- | --- |
| `fast-pixel-one-off`: column 128 written into 129 | 51 | 33 | 12 | 246 |
| `fast-attribute-quadrant`: the bottom half by coarse Y bit 0 | 16 | 8 | 12 | 220 |
| `fast-fine-x`: fine X's bit 0 dropped | 12 | 6 | 12 | 246 |
| `fast-coarse-y-wrap`: the wrap from 30, not 29 | 133 | 81 | 12 | 86 |
| `fast-vertical-copy`: dot 257 copies the vertical bits too | 195 | 100 | 12 | 246 |
| `fast-left-clip-off-by-one`: the first column shown left blank | 14 | 7 | 12 | 220 |
| `fast-off-palette-v-ignored`: the backdrop even with `v` in the palette | 13 | 7 | 12 | 56 |
| `fast-off-emphasis-ignored`: rendering off drawn without greyscale and emphasis | 16 | 8 | 12 | 60 |
| `fast-vblank-line-off-by-one`: the VBlank flag by line 240 | 20 | 17 | 7 | 130 |
| `fast-sprites-not-evaluated`: evaluation left out of background lines | 97 | 50 | 12 | 240 |

Every fault is seen by at least 12 runs and by at least 56 tests. The scene
tests of task 3a fail none of them, because their board watches. That is the
point of the new families. A caveat on the pixel faults: in the fine X,
attribute, clip and rendering-off rows, the synthetic runs are the same 12,
the six fuzz cartridges in both regions, and the pinned ROMs add one to four
runs. They are over the ten-run line, but only one family of workload stands
behind them. A synthetic cartridge that scrolls the background alone would
widen that. It would add runs, and so a new baseline, which this task did not
otherwise need, so it is left as a note for task 4. The scene tests catch each
of these faults in 56 to 246 tests.

**The earlier faults.** The 14 faults of tasks 1 and 2 and the four batch
faults of task 3a ran against the same gate with the fast path in, the
differential alone, 8 October 22:48 to 9 October 00:04 UTC (log as above). The
last column is task 3a's count: against `acf9748.txt` (format 3) for the first
14, and against `034b43f.txt` for the batch faults.

| Fault | Runs changed | Of them synthetic | Task 3a's count |
| --- | --- | --- | --- |
| `bg-nametable-late` | 298 | 76 | 298 |
| `bg-reload-late` | 285 | 76 | 287 |
| `sprite-eval-late` | 191 | 76 | 191 |
| `sprite0-hit-next-dot` | 40 | 36 | 42 |
| `vblank-late` | 184 | 63 | 171 |
| `ppu-access-dot-late` | 332 | 76 | 332 |
| `frame-counter-late` | 332 | 76 | 332 |
| `dmc-reload-late` | 81 | 24 | 81 |
| `mmc3-a12-late` | 26 | 22 | 26 |
| `mmc1-second-write-taken` | 20 | 20 | 20 |
| `no-catch-up-before-cartridge-write` | 54 | 38 | 54 |
| `no-catch-up-before-2002-read` | 284 | 54 | 284 |
| `next-event-one-dot-late` | 64 | 48 | 52 |
| `no-frame-end-event` | 286 | 54 | 286 |
| `batch-pixel-one-off` | 113 | 54 | 113 |
| `batch-sprite0-hit-late-low` | 17 | 17 | 17 |
| `batch-pal-prerender-337-early` | 17 | 16 | 17 |
| `batch-pal-prerender-340-early` | 77 | 22 | 77 |

Every one is still seen, by 17 runs or more. A fault planted in the per-dot
code now acts on fewer lines of the lazy build, because the fast lines no longer
run that code; `bg-reload-late` and `sprite0-hit-next-dot` lost two runs each.
The first 14 rows changed against format 3, not format 4, so `vblank-late` and
`next-event-one-dot-late`, which gained runs, also reflect the second reset that
format 4 added.

### How much the fast path takes

`--coverage` now prints, for every run, the lines the fast path took against
the lines the per-dot path finished, then each region's runs together
([`task-3-coverage.txt`](../../bench/nes-speed/lazy-chips/task-3-coverage.txt)).
From the lazy gate run above:

- NTSC, all 166 runs: 29.8 percent of all lines; the median run's share 71.8
  percent; 97 runs with more than half their lines fast, and 23 with none.
- PAL: 31.8 percent; the median run 74.0 percent; 95 and 23.

The 23 with none are the MMC3 runs, 12 test ROMs and 11 synthetic, whose board
watches. The long sprite0 and synthetic runs keep the sprites on, which pulls
the totals down. By
ROM folder, the share of lines, NTSC and PAL:

| Folder | Runs a region | NTSC | PAL |
| --- | --- | --- | --- |
| `apu_mixer` | 4 | 77.2 | 77.5 |
| `apu_reset` | 6 | 84.1 | 78.5 |
| `apu_test` | 9 | 79.7 | 72.3 |
| `blargg_apu_2005.07.30` | 11 | 90.7 | 90.1 |
| `branch_timing_tests` | 3 | 91.4 | 92.9 |
| `cpu_dummy_reads` | 1 | 44.8 | 55.8 |
| `cpu_dummy_writes` | 2 | 13.1 | 12.0 |
| `cpu_interrupts_v2` | 6 | 48.4 | 53.0 |
| `cpu_reset` | 2 | 79.7 | 81.8 |
| `dmc_dma_during_read4` | 5 | 25.1 | 24.5 |
| `instr_test-v5` | 18 | 86.4 | 84.5 |
| `instr_timing` | 3 | 87.3 | 75.4 |
| `mmc3_irq_tests`, `mmc3_test_2` | 12 | 0.0 | 0.0 |
| `oam_read` | 1 | 62.4 | 68.9 |
| `oam_stress` | 1 | 6.0 | 5.8 |
| `other`: `nestest` and SNOW | 2 | 58.2 | 64.7 |
| `pal_apu_tests` | 10 | 94.5 | 94.2 |
| `ppu_open_bus` | 1 | 94.6 | 94.8 |
| `ppu_read_buffer` | 1 | 9.5 | 9.0 |
| `ppu_vbl_nmi` | 11 | 53.6 | 53.9 |
| `sprdma_and_dmc_dma` | 2 | 83.7 | 88.9 |
| `sprite_hit_tests_2005.10.05` | 11 | 73.2 | 81.8 |
| `sprite_overflow_tests` | 5 | 79.5 | 86.0 |
| the homebrew, Lan Master | 1 | 60.9 | 62.9 |
| synthetic, not MMC3: `apu`, `mask`, `scroll`, `sprites` | 13 | 26.1 to 30.1 | 35.3 to 39.9 |
| synthetic `nmi`, not MMC3 | 2 | 16.7 to 16.8 | 26.2 to 26.6 |
| synthetic `fuzz`, not MMC3 | 6 | 9.0 to 10.1 | 11.5 to 12.6 |
| synthetic `sprite0`, not MMC3 | 6 | 0.1 | 0.1 |
| synthetic on MMC3 | 11 | 0.0 | 0.0 |

Most test ROMs draw text with the sprites off or rendering off, so most of their
lines are taken. The synthetic runs keep the sprites on by design, so only their
idle lines and their background stretches are taken. SNOW's own runs are 52.5
and 60.1 percent (`nestest` 92.3 and 92.2), over the whole run from power on
with two resets; the window the speed benches time is another matter (below).

### Speed

**SNOW's lines, in the benches' window.** A scratch probe counted the lines by
kind in the window both speed benches time: power on, 5 million cycles, then
5 times 1.79 million. It was a copy of the tree with counters added, never
committed; the program, the counters and the output are in
[`task-3-line-probe.txt`](../../bench/nes-speed/lazy-chips/task-3-line-probe.txt).

- SNOW has PPUMASK `$1E` on every visible line: the sprites are on all the way
  down. The fast path takes only its idle lines, 15 a frame on NTSC (line 240
  and lines 247 to 260) and 65 on PAL.
- Lines 241 to 246 are not whole. The NMI handler's OAM DMA makes a catch-up for
  each of its writes there.
- Lan Master at its title has PPUMASK `$0E`, the background alone. The fast path
  takes the idle lines and 239 of the 240 visible lines on NTSC, where line 0 was
  never whole in this window. The catch-up at the frame's end is made at the end
  of the cycle that reaches it, which there was always a dot or two into line 0.
  On PAL, whose cycle is 3.2 dots, that cycle ended exactly at line 0 dot 0 in
  about half the frames, and line 0 was taken whole too: it was exact in 134 of
  269 frames. (Corrected after the review: the first version said line 0 was
  never whole.)

So for SNOW, this task can only touch the idle lines, which were already the
cheapest. That is what the figures show.

**Natively**, in thread CPU time a cycle (`bench/thread-time`). Main at
`5640a57` was exported with `git archive` and `.testdata` linked, and the bench
was unchanged between the two trees (checked with `diff`). Both builds were
alternated in four rounds by
[`task-3-thread-time.sh`](../../bench/nes-speed/lazy-chips/task-3-thread-time.sh).
Set 1 was 22:32:31 to 22:33:39 UTC, loads 1.5 to 2.0. Set 2, 22:34:13 to
22:35:05 UTC, loads 1.2 to 1.4, added a third build: this tree with
`WholeLines` off by default, in a scratch copy, to separate the fast path from
the rest of the build. The figures are nanoseconds a cycle, the median of the
four rounds' medians; the lines are in
[`task-3-thread-time.txt`](../../bench/nes-speed/lazy-chips/task-3-thread-time.txt).

| Workload | Set 1: `5640a57` | Set 1: this build | Gain | Set 2: `5640a57` | Set 2: fast path off | Set 2: this build | Gain |
| --- | --- | --- | --- | --- | --- | --- | --- |
| SNOW, NTSC | 71.6 | 76.1 | 0.94 | 70.8 | 72.6 | 71.4 | 0.99 |
| SNOW, PAL | 71.3 | 67.9 | 1.05 | 70.6 | 71.2 | 66.4 | 1.06 |
| Lan Master, NTSC | 65.1 | 52.7 | 1.23 | 59.7 | 62.3 | 47.8 | 1.25 |
| Lan Master, PAL | 65.1 | 52.5 | 1.24 | | | | |

The per-dot reference of this build gave 154.8 (NTSC) and 138.5 (PAL). The
bench's noise is 10 to 20 percent from launch to launch (task 17), so SNOW NTSC's
0.94 and 0.99 mean no gain, not a loss. In set 2 the build with the fast path
off is within noise of main, so the gain on Lan Master is the fast path's.

**In the browser** (AOT), with the same method as task 2b:

- Both builds were published with `dotnet publish src/Dbhq.Machines.Nes.Wasm -c
  Release -p:RunAOTCompilation=true -o <folder>`, from the `5640a57` export and
  from this tree, after deleting the project's `obj/Release` in each. They were
  served by this tree's harness, which is the same as main's but for the
  `ROM=homebrew` option.
- [`task-3-browser.sh`](../../bench/nes-speed/lazy-chips/task-3-browser.sh) ran
  each set: SNOW and Lan Master, NTSC then PAL, four rounds each of one fresh
  launch of main, then one of this build. Each launch was `THREAD_TIME=8 node
  run-in-browser.mjs <folder> 1 1790000 5000000 5 <region>`: the page's five
  timed runs, then eight more timed in the main thread's CPU time. The BBC
  Micro gauge ran after each set.
- Three sets: 22:36:47 to 22:40:08, 22:41:32 to 22:44:33 and 22:44:55 to
  22:48:06 UTC. One-minute loads were 1.3 to 4.4, and 1.3 to 2.2 in the last
  two. Nothing else of this task ran then.
- The gauge was the median of each set's ten runs in the main thread's CPU time,
  25.5 to 33.4 MHz. Task 2b saw 21 to 23, so the machine was quick.
- Chrome 153.0.8010.47.
- The medians, by
  [`task-3-browser-medians.py`](../../bench/nes-speed/lazy-chips/task-3-browser-medians.py)
  from the whole output,
  [`task-3-browser.txt`](../../bench/nes-speed/lazy-chips/task-3-browser.txt).

The table is `times_real`, the median of each build's 96 runs in the main
thread's CPU time over the three sets, with the ratio of each set beside it:

| Workload | `5640a57` | This build | Ratio | The three sets | Wall clock, the page's 60 runs, main to this build |
| --- | --- | --- | --- | --- | --- |
| SNOW, NTSC | 3.94 | 3.84 | 0.98 | 1.02, 0.99, 0.95 | 3.82 to 3.71 |
| SNOW, PAL | 4.34 | 4.36 | 1.00 | 1.07, 0.94, 1.04 | 4.22 to 4.25 |
| Lan Master, NTSC | 4.11 | 5.59 | 1.36 | 1.37, 1.38, 1.38 | 4.05 to 5.75 |
| Lan Master, PAL | 4.31 | 5.99 | 1.39 | 1.38, 1.42, 1.34 | 4.52 to 6.21 |

One cost was not isolated. With the fast path in, the per-dot loop in
`CatchUp` compares the dot with 0 on every dot it runs, and on a workload whose
every visible line has sprites that compare is all the fast path adds. What it
costs in Mono's AOT build was not measured on its own in the browser. It is
inferred only from SNOW's 0.98 and 1.00, which are within the noise of no
change, and natively the build with the switch off ran within noise of main.

### The stopping rule

Ruling Y said: if the browser AOT gain over main after this task is under about
8 percent, stop before task 4 and report to Dan. **On SNOW, the benchmark ROM,
it is: the gain is none (0.98 on NTSC, 1.00 on PAL), so the rule triggers.**

Two facts sit beside it:

- On the homebrew's title, the fast path gives about 1.4 times in the browser
  and 1.25 natively.
- The rule's premise was that idle and background lines would be most of the
  renderer's gain. That holds for a program that shows the background alone,
  such as most test ROMs and Lan Master's title. It does not hold for SNOW, whose
  every visible line has sprites. On SNOW, only task 4 can take a visible line
  at all. This task did not measure what task 4 would give, and no figure here
  should be read as one.

### For task 4, if it is built

- Every visible line of SNOW, and of most game play, has the sprites on.
- Lines 241 to 246 of SNOW are split by the NMI handler's OAM DMA, and line 0,
  in every NTSC frame and about half of the PAL ones, by the frame end's catch-up
  a dot or two into it. Both are left to the per-dot path whatever task 4 does.
- The pixel faults' runs come mostly from the fuzz cartridges. A background-only
  scrolling workload would widen them, at the cost of a new baseline.
- The families here are written to take a sprites-on line's expected count from
  `Fast`. Task 4 changes `Fast` and adds sprite families: Review Focus 3, and
  sprite 0 down the picture, which task 3a's review asked for.

### After the review

The review traced the fast path beside the per-dot path and found them the
same, and found no critical fault. It asked for seven changes, all made in
commit `132fd50`, with the baseline recorded again after it.

**The scene families saw only vertical mirroring.** Every board in
`PpuScanlineTests` was vertical. Under vertical mirroring a nametable fetch that
uses only the select's low bit (`(v >> 10) & 1` for `& 3`) reads the right page,
so the reviewer's planted fault failed none of the family's tests. There were
two holes, not one:

- `BackgroundLinesUnderEachMirroring` runs the scroll table (each nametable
  select, the coarse Y wrap from 29) under horizontal, single-screen low and
  high, four-screen and vertical mirroring, in both regions.
- Even then the fault passed. `Busy` fills the nametables with `(i * 7) + 3`,
  whose low byte repeats every 256 bytes, so all four 1 KB pages held the same
  bytes and no page mix-up could show. Under the other layouts each page now
  gets its own bytes. The vertical fill is unchanged, so the tests written
  before see what they saw.

With both fixes the fault, now in `faults.py` as `fast-nametable-page`, fails
the horizontal and four-screen rows in both regions (single-screen and
vertical cannot show it, by their wiring) and two CNROM bus scenes.

**Five faults rested on the fuzz cartridges alone.** The fine X, attribute,
clip and rendering-off faults were seen by 12 to 16 runs, 12 of them always the
six fuzz cartridges. The differential gained twelve synthetic "background"
cartridges (`Synthetic.cs`): five on NROM, four on CNROM (its banks switched in
the frame), two on MMC1 (which also turns its mirroring each frame) and one on
AxROM. The sprites are never on, and the header's mirroring is horizontal.

- Each frame, X moves by an odd step, so fine X takes every value in 8 frames,
  and Y moves by a step. For odd seeds Y wraps at 240, so the picture crosses
  coarse Y 29 into the other vertical nametable. For even seeds it runs on
  through the attribute rows 30 and 31.
- The nametable select comes from the frame, and the background's pattern table
  turns every 16 frames.
- PPUMASK comes from a table: the left clip on and off, greyscale, each
  emphasis bit and all three.
- `v` is left in the palette in VBlank, which the one frame in four with
  rendering off shows.
- In the frame comes the scroll workload's split and the board's write.

They are the last jobs in the list, so every earlier job kept its place and
its PAL reset phase. On their own they run 92.2 and 92.9 percent of their lines
fast, NTSC and PAL
([`task-3-coverage.txt`](../../bench/nes-speed/lazy-chips/task-3-coverage.txt)).

**The baseline, recorded again.** On 9 October 2026, from
`bench/nes-speed/differential` at `132fd50`:

- `dotnet run -c Release -- baseline/132fd50.txt --oracle` wrote 356 runs,
  00:47:40 to 00:52:05 UTC, loads 2.9 to 5.4. The format did not change.
- Its first line and all 332 lines of `034b43f.txt` are identical, the 254
  pinned-ROM lines among them. The 24 new lines are the background runs.
  `034b43f.txt` was removed.
- `--check baseline/132fd50.txt`, the lazy build, gave IDENTICAL on all 356
  runs, byte for byte, 00:52:05 to 00:56:02 UTC.

**The faults again**, the eleven fast-path faults against `132fd50.txt`, 00:57
to 01:55 UTC, then their tests alone again after the page fix
([`task-3-review-faults.txt`](../../bench/nes-speed/lazy-chips/task-3-review-faults.txt)).
The count before is from the first fault run, of 332:

| Fault | Runs changed (of 356) | Before | Of them synthetic | Tests failing (of 1,037) |
| --- | --- | --- | --- | --- |
| `fast-pixel-one-off` | 75 | 51 | 36 | 256 |
| `fast-attribute-quadrant` | 40 | 16 | 36 | 230 |
| `fast-fine-x` | 36 | 12 | 36 | 256 |
| `fast-coarse-y-wrap` | 157 | 133 | 36 | 96 |
| `fast-vertical-copy` | 219 | 195 | 36 | 256 |
| `fast-left-clip-off-by-one` | 38 | 14 | 36 | 230 |
| `fast-off-palette-v-ignored` | 37 | 13 | 36 | 56 |
| `fast-off-emphasis-ignored` | 40 | 16 | 36 | 60 |
| `fast-vblank-line-off-by-one` | 20 | 20 | 7 | 140 |
| `fast-nametable-page` (new) | 37 | | 26 | 6 |
| `fast-sprites-not-evaluated` | 121 | 97 | 36 | 250 |

Each pixel fault is now seen by at least 36 runs, 24 of them the background
cartridges. The VBlank line fault is untouched by them (it lives on the idle
lines) and stays at 20. `fast-coarse-y-wrap` changed with the next item: the
fast path now calls the shared `IncrementY`. A fault there would hit both paths,
as the old per-dot faults do. So the fault planted for this row stops the fast
path's Y increment going from fine Y 7 and coarse Y 29 to 0, and sends it to
coarse Y 30, in the fast path alone.

**Shared increments.** `RenderBackgroundLine` repeated `IncrementY` inline, and
its `CoarseXOn` repeated `IncrementCoarseX`. It now calls the two statics.
Natively, in four alternated rounds (9 October 00:40:56 to 00:42:33 UTC, loads
0.9 to 1.0), the build with its own copies against the build with the shared
ones gave ns a cycle (the median of the four medians):

- Lan Master: 49.6 against 52.6 on NTSC, and 53.9 against 53.9 on PAL;
- SNOW: 74.2 against 73.9.

The rounds' medians overlap (Lan Master NTSC 46.7 to 62.4 against 45.6 to 73.4).
That is within noise, so the shared statics stay
([`task-3-thread-time.txt`](../../bench/nes-speed/lazy-chips/task-3-thread-time.txt), set 3).

**Back-references.** The per-dot methods the renderer repeats or calls now say
so:

- `RenderVisibleDot`, `RenderDot` (its "must be made in both" now names
  `PpuScanline.cs` and the gate), `DrawPixel` and `DrawRenderingOff`;
- `IncrementY` and `IncrementCoarseX`;
- `Reload`, `FetchAttributeBits` and `FetchPatternHigh`;
- `Evaluate` and `FetchSprite`.

**A board with latching pattern reads.** `Board` now says, at its windows and at
`WatchesPpuAddresses`, that a board whose pattern reads have side effects (MMC2
and MMC4) must override `WatchesPpuAddresses`. By inheritance it would qualify
for the fast path, and its latch would never move.

**Two corrections** are made above. Line 0 is whole in about half the PAL
frames, not never. The Mono AOT cost of the per-dot compare in `CatchUp` was
not isolated in the browser.

The NES tests gave 2,558 passed and 0 failed, both before and after the page fix
(after it, 02:15:54 to 02:17:20 UTC). The source did not change after
`132fd50`. On the final tree, from `bench/nes-speed/differential`:

- `--check baseline/132fd50.txt`, 02:17:28 to 02:21:41 UTC;
- `--check baseline/132fd50.txt --oracle`, 02:21:41 to 02:26:20 UTC, loads 2.7
  to 5.2.

Each gave IDENTICAL on all 356 runs, byte for byte.
