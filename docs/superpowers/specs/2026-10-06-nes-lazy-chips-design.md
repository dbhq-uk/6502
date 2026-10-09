# The NES's lazy chips: design

Written 6 October 2026, after the NES went live and Dan found it slow. It amends
one decision in [the NES design](2026-10-05-nes-design.md) and one sentence of
rule 1 in `AGENTS.md`. Everything else in that design stands.

## What was built

7 October 2026. The plan's tasks 1 and 2 were built: the gate (the extended
differential and its baseline) and the lazy PPU. The PPU is caught up before a
CPU access to `$2000` to `$3FFF`, before each OAM DMA write, at its next event,
at each frame end, and every cycle for a board that watches its address bus, as
below, and also before every CPU write to `$4020` to `$FFFF`, which this design's
list missed: a board's register can switch the PPU's pattern banks or nametable
layout. The gate's output was identical to the baseline on every run. Rule 1's
new wording, which Dan approved with this design on 6 October 2026, is now in
`AGENTS.md`.

The fast path and the APU's batch below (the plan's tasks 3 to 5) were **not
built**. In the browser, the lazy PPU alone made the benchmark ROM about 1.3 to
1.4 times as fast (natively about 2 times), and a profile of that build put
about 40 percent of the time still in the PPU, about 26 in the bus and loop and
about 14 each in the sound unit and the CPU core. From those shares the fast
path is estimated at 1.1 to 1.5 times (about 1.3 in the middle case) and the
APU's batch at 1.04 to 1.1; all the options together give about 1.6 times, and
none reaches 10 times. The fast path is a second implementation of the PPU's
line, and the batch a second loop over the APU's timers, each to be held equal
to the first by the gate, for those gains. So they remain as designed below,
specified and unstarted, as issues
[#72](https://github.com/dbhq-uk/6502/issues/72) (the fast path) and
[#73](https://github.com/dbhq-uk/6502/issues/73) (the APU's batch), to be built
only on an order from Dan. The figures were measured on 7 October 2026; the commands and the full
tables are in the journal,
[`../../journal/2026-10-06-the-nes-lazy-chips.md`](../../journal/2026-10-06-the-nes-lazy-chips.md),
sections "Task 2b" and "Task 2c", and the plan's "What was built" says the same.

The speed target below, 1.5 times, was met natively on the benchmark ROM and
about met on the homebrew (the journal's task 2, "Speed"), and not met in the
browser (task 2b).

The baseline changed when the reset fix landed. Pull request #71 made the PPU's
place in secondary OAM wrap at 32, and that place is in the PPU's state report,
so with #71 in the tree the check against `baseline/4e9b92b.txt` failed on the
PPU's hash with no difference in behaviour behind it. On 8 October 2026 the
baseline was re-recorded from the merged tree with the per-dot reference
(`--oracle`) as `bench/nes-speed/differential/baseline/38c5544.txt`, the lazy
build was checked against it, and the old file was removed. The journal says why
the file changed ([#74](https://github.com/dbhq-uk/6502/issues/74)). On the same
day the gate was extended for the fast scanline renderer (format 3: the picture as
far as it is drawn at every point, the sprite 0 sweep over the whole picture, and
scene tests on every dot of a line, then a second reset on a chosen PAL dot
phase) and its baseline recorded again as
`bench/nes-speed/differential/baseline/034b43f.txt`
([the scanline renderer entry](../../journal/2026-10-08-the-nes-scanline-renderer.md), task 3a),
and on 9 October, with twelve background-only scrolling cartridges added after the
review of task 3, as `bench/nes-speed/differential/baseline/132fd50.txt`.

## Why

The NES runs at about 2 to 2.5 times real time in the browser on the dev
machine, and slower on a laptop or a phone, where the page plays in slow motion.
The profile
([the speed journal](../../journal/2026-10-05-the-nes-speed.md)) puts about 40
percent of the time in the PPU, 16 in the per-cycle bus code, 11 in the APU and 10
in the CPU core. The shared core is a tenth, so the work of
[task 17](../../journal/2026-10-06-the-core-speed.md) could not move the NES.
Only doing less work per second in the PPU, the bus and the APU can.

## The decision this amends

The 5 October design chose "tick inside every bus call": each `Read` or `Write`
advances the PPU by its dots and the APU by one cycle, then does its access. It
rejected catch-up scheduling because it is "easy to get wrong near NMI, sprite-0
and the MMC3 A12 edge, which is where the proof looks".

That reason was about accuracy, and accuracy can now be held to a measure. On 6
October 2026 there were the NES's tests (1,503 that day), its pinned test ROMs
(about 140), a mutation pass (56 mutations) and a differential tool that compares
two builds instruction by instruction over the test ROMs (127 that day). So Dan
chose, on 6 October 2026, to make the PPU and APU lazy **on one condition:
the lazy build must give bit for bit what the per-dot build gives, at every
point where anything can see the chip.** That is a rule with a test, and the
test decides.

## The rule, restated

`AGENTS.md` rule 1 says the machine "advances its other chips inside each call".
The new wording, which Dan is asked to approve with this design:

> **1. Every bus access is one cycle.** The CPU does one `Read` or one `Write`
> per cycle, and the machine advances its other chips as of each call. A chip may
> be advanced lazily, in a batch, only if nothing can see it between catch-up
> points and its state at each catch-up is exactly what advancing it inside every
> call would have given; a differential test over real programs proves that. A
> cycle that happens without a bus access, or a bus access that is not a cycle,
> is still a bug.

The cycle count, the dot count and every interrupt line stay exact. Only the
moment at which a chip's own state is brought up to date moves.

## What it builds

As designed on 6 October 2026. The PPU's catch-up was built; the APU's
catch-up, the fast path and the APU's batch were not ("What was built", above).

**Catch-up.** The PPU keeps a logical position (the dot the bus says it is on)
and a physical position (the dot its state is computed to). The bus adds dots to
the logical position each cycle, which is an integer add. The PPU is brought up
to date, with `CatchUp`, only:

1. before any CPU access to `$2000` to `$3FFF`, and to the OAM DMA's writes;
2. when the logical position reaches the PPU's next event (the dot where the
   VBlank flag sets, so the NMI line can rise on the right cycle);
3. at the end of a frame, so the page can draw it;
4. every cycle, for a cartridge whose board watches the PPU's address bus (MMC3
   today), which therefore stays on the exact per-dot path. A later change may
   schedule MMC3's clock; this one does not.

The APU is the same (not built): caught up before a CPU access to `$4000` to `$4017`, at its
next event (frame-counter IRQ, DMC fetch, DMC IRQ), and when the page reads the
samples.

**The exact path stays.** The existing per-dot `Tick` is the reference. It is
what every catch-up does when it cannot do better, and it is what the
differential compares against. It is not removed.

**The fast path** (not built). A catch-up that covers whole scanlines may render each one in
a tight loop instead of dot by dot, when the PPU can show that nothing in the
line is observable: no register access inside it (the catch-up's own length
tells us), rendering state fixed for the line, and no board that watches the
address bus. The fast path must leave every piece of PPU state, at the line's end,
equal to what the per-dot path would have left: `v`, `t`, fine X, the write
toggle, the shifters, the sprite buffers, OAM and secondary OAM, the flags
(VBlank, sprite 0, overflow), the line and dot, and the pixels of the line. It is
built in steps, each gated by the differential: lines with rendering off, then
the background, then sprites, sprite 0 and overflow.

**The APU's batch** (not built). A catch-up over `n` cycles runs a tight loop over the
channels' timers with no bus call, skipping silent channels, and gives the
sample buffer the same samples as per-cycle stepping.

## What does not change

The CPU core, the bus's `Read` and `Write` contracts, the cycle count, OAM DMA and
DMC DMA stalls (each stall cycle still goes through the one cycle method), the
interrupt sampling rule, the mappers' behaviour, the page and the WebAssembly
host. Nothing about the KIM-1 or the BBC Micro changes.

## Proof

The differential is the gate. It is extended **first**, before any change to the
chips, to record at every point where anything can see a chip (each PPU or APU
register access, each OAM DMA write, each frame end) the chip's full state, and,
at each frame end, a hash of the picture and the samples. The baseline output is
recorded from the current `main`. Every change that follows must give identical
output over the differential's ROMs (127 on 6 October 2026) in both regions, or
it is not kept. As before the
harness is shown to fail: a deliberate one-dot fault in each new path must change
its output.

On top of that: all the NES tests (1,503 on 6 October 2026), the core's tests,
the KIM-1 and BBC tests, the mutation pass's mutations (56 on 6 October 2026)
re-run on the new paths (a mutation in the fast path must be
caught by a unit test, not only by the differential), and new unit tests that
drive the fast path and the exact path on the same scenes (a table of scenes:
scroll splits, sprite 0 near the line's ends, rendering toggled at the line
edges, left-edge clipping, 8 by 16 sprites, more than eight sprites on a line,
emphasis changes) and require equal state at the line's end.

## Speed

The target is a figure, not a promise: at least 1.5 times today's speed on the
bundled game and on the benchmark ROM, in both regions, measured the way task 6b
and 17 did it (alternated with the baseline, with the load average, and the BBC
bench as a gauge). Games that read `$2002` in a tight loop mid-frame (sprite-0
waits) will gain less, and games on MMC3 gain nothing until its clock is
scheduled. Both are said on the page's issue and in the journal. If the first
steps show the target is out of reach, the work stops there and says so.

## Out of scope

Scheduling MMC3's scanline counter, a threaded or worker-based renderer,
frame skipping (a separate small change), and any change to what the picture
shows.
