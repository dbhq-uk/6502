# The NES's lazy chips: implementation plan

## What was built, 7 October 2026

**Tasks 1 and 2 were built and reviewed.** Task 1 is the gate: the extended differential, its baseline (`bench/nes-speed/differential/baseline/4e9b92b.txt`, which adds 38 synthetic cartridges to the test ROMs), the deliberate faults, the chips' state reports and the bus's observer. Task 2 made the PPU lazy: `Deliver`, `CatchUp` and `NextEventDot`, with the PPU caught up before a CPU access to `$2000` to `$3FFF`, before every CPU write to `$4020` to `$FFFF` (a point the spec's list missed: a board's register can switch the PPU's banks or nametable layout), before each OAM DMA write, at `NextEventDot`, at each frame end, and every cycle for a board that watches the PPU's address bus (MMC3) and in the per-dot reference mode. The gate's output was identical to the baseline on every run, lazy and per-dot. The journal, [`../../journal/2026-10-06-the-nes-lazy-chips.md`](../../journal/2026-10-06-the-nes-lazy-chips.md), has the commands and the results.

**Tasks 3, 4 and 5 were not built.** The evidence, measured on 7 October 2026 and recorded with its commands in the journal's sections "Task 2b" and "Task 2c":

- In the browser, the build the live page runs, the lazy PPU alone took the benchmark ROM from 2.25 to 3.13 times real time on NTSC and from 2.54 to 3.33 on PAL: about 1.3 to 1.4 times as fast, where natively it was about 2 times.
- A profile of that build in the browser puts about 40 percent of the time in the PPU, about 26 in the bus and the machine's loop, about 14 in the sound unit and about 14 in the CPU core.
- The Amdahl estimates from those shares: a fast scanline renderer (tasks 3 and 4) 1.1 to 1.5 times, about 1.3 as the middle case; a lazy APU (task 5) 1.04 to 1.1; a cheaper bus cycle 1.05 to 1.12; a cheaper CPU core in AOT 1.02 to 1.08. All five together give about 1.6 times, about 5 times real time on the development machine. None of them reaches 10 times, alone or together.

So tasks 3 and 4 would be a second implementation of the PPU's line, which the gate must hold equal to the first on every line it takes, for an estimated 1.3 times, and task 5 a batch for the APU for 1.04 to 1.1 times. Dan was asked whether to go on; no answer came, and the recommended path was taken: stop with what is built. Tasks 3 to 5 stay below as written, specified and unstarted. They are to be raised as issues, and built only on an order from Dan; the gate, the profile and this plan are ready for them. Task 6 was done in a reduced form: its browser figure is task 2b's, and its full run was repeated on the final tree.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the NES's PPU and APU lazy, so the page runs faster on slow devices, with the lazy build bit for bit equal to the per-dot build at every point where a chip can be seen, as the design in [`../specs/2026-10-06-nes-lazy-chips-design.md`](../specs/2026-10-06-nes-lazy-chips-design.md) says.

**Architecture:** The bus adds dots and cycles to each chip's logical position. A chip is caught up on demand: before a CPU access to it, at its next event, at the end of a frame, and every cycle for a cartridge whose board watches the PPU's address bus. The existing per-dot `Tick` stays as the reference and the fallback. A catch-up over whole scanlines may use a fast scanline renderer. The differential tool is extended first and gates every change.

**Tech Stack:** C# on .NET 10, xUnit, the existing differential and thread-time benches, headless Chrome for the browser figure.

**This plan is not pre-run**, like the NES's own plan: each task states interfaces, the facts it rests on and the exact checks. The checks are the point: **no task is kept unless the extended differential gives output identical to the baseline.**

**Spec:** [`../specs/2026-10-06-nes-lazy-chips-design.md`](../specs/2026-10-06-nes-lazy-chips-design.md); it amends [the NES design](../specs/2026-10-05-nes-design.md) and `AGENTS.md` rule 1.

## Global Constraints

- **The lazy build equals the per-dot build, bit for bit, at every point where a chip can be seen:** each PPU or APU register access, each OAM DMA write, each frame end (picture and samples), and every interrupt line at every cycle. A task that cannot show this is not kept. (Spec "The rule, restated", "Proof".)
- **Every bus access is one cycle; the cycle count, dot count and interrupt lines stay exact.** `NesBus.Cycle` stays the one place a cycle is counted, and every DMA stall cycle still goes through it. (`AGENTS.md` rule 1, as amended by the spec.)
- **The core knows no machine.** Nothing under `src/Dbhq.Cpu6502/` changes in this plan. (Rule 2.)
- **The per-dot `Tick` path stays, unchanged in behaviour,** as the reference and as the fallback for a board that watches the PPU's address bus.
- **No existing test is weakened or removed.** Tests that assert structure rather than behaviour may change only with the reason written in the task's report.
- **No figure describing the project as it stands is typed by hand.** Every speed figure has its command, date and load average, alternated with the baseline. (Rule 5.)
- **Document as you go.** One journal entry, `docs/journal/2026-10-06-the-nes-lazy-chips.md`, with each step's result, what was dropped and why. Differences from a reference go in `docs/known-differences.md`. (Rule 6.)
- **Do not merge, and do not mark the pull request ready.** Merging deploys. After the last task the controller reports to Dan, who decides.
- **No em or en dashes. British English. Warnings are errors.**
- **Branch `perf/nes-lazy-chips`, cut from `main` at `5e48505`.** One commit per kept step, pushed, with a draft pull request opened after task 1.

## Review Focus

The conditions the spec implies and the main tests would not exercise. Each gets its test in the task that owns the code.

1. **A register access in the middle of a line that the fast path was about to take.** A `$2005`, `$2006`, `$2000` or `$2001` write, or a `$2002` read, on any dot of a visible line, including the first, the 256th, the 257th and the 340th. The fast path must not be used for a line that contains one, and the exact path must give the same result as before. (Task 3.)
2. **Rendering switched on or off at a line edge, and the odd-frame dropped dot.** NTSC only, only when rendering is on, and the switch can land on the dot that decides it. (Tasks 2 and 3.)
3. **Sprite 0 hit and overflow near the ends of a line, and on the left-edge clip.** The flag must be set at the same dot, and a `$2002` read just before or just after it must see the same bit. (Task 4.)
4. **The NMI line when `$2000` bit 7 changes during VBlank, and when `$2002` is read on the VBlank dot.** The lazy build must raise or suppress the NMI on the same cycle. (Task 2.)
5. **A frame with no register access at all (a game stuck in a loop), and a catch-up that spans many frames.** A catch-up must not skip a frame end, so the picture and the samples are made for every frame. (Tasks 2 and 5.)

---

## File structure

```
src/Dbhq.Machines.Nes/
  Ppu.cs, PpuBackground.cs, PpuSprites.cs   # logical position, CatchUp, the exact Tick kept
  PpuScanline.cs                            # the fast scanline renderer (tasks 3 and 4)
  Apu.cs, ApuChannels.cs                    # logical position, CatchUp, the batch loop (task 5)
  NesBus.cs                                 # Cycle adds to logical positions; catch-up at the observation points
bench/nes-speed/differential/               # extended first (task 1)
tests/Dbhq.Machines.Nes.Tests/              # scene tests that drive both paths (tasks 3 and 4)
docs/journal/2026-10-06-the-nes-lazy-chips.md
```

---

### Task 1: The gate: the differential, extended, and the baseline recorded

**Files:**
- Modify: `bench/nes-speed/differential/Program.cs`, its README
- Modify: `src/Dbhq.Machines.Nes/NesBus.cs` and `Ppu.cs` only to add an observation hook (a plain event or callback, invoked at each point where the lazy build will catch a chip up; in this task they are called on every access, as today)
- Create: `docs/journal/2026-10-06-the-nes-lazy-chips.md`, `docs/superpowers/` already has the spec and plan

**Interfaces:**
- Produces: `NesBus.Observe` (or similar, internal): called after each CPU access to `$2000`-`$401F`, each OAM DMA write and each frame end, with the chip's full state available through existing public read-only members (`Ppu.V`, `T`, `FineX`, `WriteToggle`, `Oam`, secondary OAM and the sprite buffers, shifters, flags, `Line`, `Dot`, `Frame`, `Screen.Pixels`; the APU's channel states, frame counter, DMC state, `Irq`, the sample buffer's position). Anything the harness needs and cannot reach is exposed read-only and internal, with `InternalsVisibleTo` to the harness if it is not already.

- [ ] **Step 1: List the full state of each chip** from `Ppu.cs`, `PpuBackground.cs`, `PpuSprites.cs`, `Apu.cs`, `ApuChannels.cs` (every field a catch-up could leave different), and write the list into the journal. A field missed here is a hole in the gate.
- [ ] **Step 2: Extend the harness.** At each observation point it hashes the chips' full state with the logical position; at each frame end it hashes the picture and the samples as before. It still hashes every instruction (registers, bus cycle, line and dot) and every interrupt line. The PPU position it hashes must be the **logical** one, so the lazy build can be compared without forcing a catch-up.
- [ ] **Step 3: Record the baseline** from the current build (`5e48505`), over the 127 ROMs, both regions, and keep the output hash per ROM in the journal with the command, date and load.
- [ ] **Step 4: Show the gate can fail:** a deliberate one-dot fault in each of the PPU's background pipeline, sprite evaluation, sprite 0 hit, the APU's frame counter and the DMC must change the output. Record how many ROMs each changes. If a fault changes nothing, the state list is incomplete: fix it before going on.
- [ ] **Step 5: Run everything and commit.** NES tests, `dotnet build -c Release`, the dash check. Open the draft pull request.
  ```bash
  git add -A && git commit -m "bench(nes): the differential records every chip's state where it can be seen, and the baseline"
  git push -u origin perf/nes-lazy-chips && gh pr create --draft --base main --title "perf: the NES's lazy chips" --body "Draft. Built task by task from docs/superpowers/plans/2026-10-06-nes-lazy-chips.md. Not for merge until Dan says."
  ```

---

### Task 2: Catch-up for the PPU, with the same dot work

**Files:**
- Modify: `Ppu.cs`, `NesBus.cs`
- Test: `PpuCatchUpTests.cs`, `NesBusTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed partial class Ppu
  {
      public long LogicalDots { get; }            // dots the bus has delivered
      public long CaughtUpDots { get; }           // dots the state is computed to
      public void Deliver(int dots);              // add dots to the logical position only
      public void CatchUp();                      // run Tick until CaughtUpDots == LogicalDots
      public long NextEventDot { get; }           // the logical dot at which the NMI line would next change
  }
  ```
  `Ppu.Line` and `Ppu.Dot` stay and report the **logical** position, computed without forcing a catch-up.

**What it must do.** `NesBus.Cycle` calls `Deliver` instead of `Tick` for the dots of the cycle, with the same 2-before-3-after split for the observable ordering, and **catches up** (a) before a CPU access to `$2000`-`$3FFF` and before an OAM DMA write, at the dot the access sees; (b) when the logical dot reaches `NextEventDot` (so `Cpu.Nmi` is raised on the same cycle as before); (c) at each frame end; (d) every cycle when `Mapper.WatchesPpuAddresses` is true, so MMC3 and any later board stay exact. The catch-up in this task is just the existing `Tick` in a loop: the same work, batched. The speed gain is small; the point is the machinery and the gate.

- [ ] **Step 1: Write the failing tests.**
  - A table of scenes (register writes at chosen dots of chosen lines, rendering on and off, the VBlank dot read, `$2000` bit 7 toggled during VBlank) run on the **baseline per-dot build** (kept as a test oracle: a flag on the bus that forces `CatchUp` every cycle) and on the lazy build; at every observation point the full state is equal. Review Focus 4 (NMI) and 5 (a frame with no register access, and a catch-up spanning several frames, which must still end each frame) are rows.
  - `NextEventDot` is the dot at which the VBlank flag sets when `$2000` bit 7 is set, or none; it moves when `$2000` is written (a catch-up happens first, so the write sees the right state).
  - A board with `WatchesPpuAddresses` is caught up every cycle (a test cartridge with a stub watching board).
- [ ] **Step 2: Run them to see them fail. Step 3: Implement. Step 4: tests pass.**
- [ ] **Step 5: The gate.** Run the extended differential over the 127 ROMs, both regions: output **identical** to the task 1 baseline. Run the 56 mutations that touch the PPU (the mutation pass's scratch scripts are the model) and confirm each is still caught.
- [ ] **Step 6: Measure** (alternated with the baseline, load recorded). Journal. **Commit** `perf(nes): the PPU is caught up on demand, with the same dot work`; push.

---

### Task 3: The fast scanline renderer, background and idle lines

**Files:**
- Create: `src/Dbhq.Machines.Nes/PpuScanline.cs`
- Modify: `Ppu.cs` (`CatchUp` takes the fast path when it may)
- Test: `PpuScanlineTests.cs`

**Interfaces:**
- Produces: `internal bool Ppu.TryRenderLine(...)`: renders the whole of the current line in one call and leaves the PPU as the per-dot path would at the end of the line; returns false, having changed nothing, when it may not.

**When it may.** The catch-up covers the whole line from dot 0 (or from the dot the line began in the exact path, handed on at the line's start only), no register access falls inside it (the catch-up's end is at or after the line's last dot), the board does not watch the address bus, and the line is one of: a VBlank or post-render line (batch: only the line and dot counters, the flags and the frame end change); a visible line with rendering off (the backdrop fill, as the per-dot path draws it, including the `v`-in-palette case); a visible line with rendering on and **no sprites on the line and sprites off** (then only the background pipeline); the pre-render line is left to the exact path in this task.

- [ ] **Step 1: Write the failing tests.** For each class of line above, a table of scenes (scroll values, fine X, nametable select, attribute quadrants, the left-edge clip bit, greyscale and emphasis, rendering toggled on the line before, odd-frame dot skip on the pre-render line): run each scene through the exact path and the fast path, and require equal state at the line's end: `v`, `t`, fine X, the write toggle, the shifters, the pixels of the line, OAM and secondary OAM, the flags, the line and dot. Review Focus 1 (a register write inside the line: the fast path must refuse) and 2 (rendering toggled at a line edge) are rows.
- [ ] **Step 2: Fail. Step 3: Implement,** in a tight loop with no calls through interfaces in it: per tile, one nametable byte, one attribute byte and two pattern bytes read from the board's memory views, 8 pixels written. It must not call the board's `PpuAddressChanged` (the gate: it is taken only when the board does not watch).
- [ ] **Step 4: Tests pass. Step 5: The gate** (differential identical, both regions; mutations in the background pipeline re-run and caught by a unit test as well). **Step 6: Measure.** Journal. **Commit** `perf(nes): whole scanlines rendered at once when nothing can see them, background and idle lines`; push.

---

### Task 4: Sprites on the fast path

**Files:** `PpuScanline.cs`, `PpuSprites.cs` as needed; `PpuScanlineTests.cs`

**What it must do.** Extend the fast path to lines with sprites: evaluation of the next line's sprites with the 8-per-line limit and the **overflow bug**, sprite pixels with priority and flips and 8 by 16, **sprite 0 hit at the dot the exact path sets it**, the left-edge clips, and the OAM address behaviour during rendering. The sprite 0 hit flag and the overflow flag are set at their dots in the logical timeline, so a `$2002` read at any dot sees the same bit as before: **the fast path is taken only when no `$2002` read can land in the line** (a catch-up that ends inside the line goes down the exact path for the line).

- [ ] **Step 1: Write the failing tests:** the scenes from the NES's own sprite tests (more than eight sprites on a line, the diagonal overflow false positive and negative, sprite 0 at x = 0, 7, 8, 254, 255, behind the background, with the left clip on and off, 8 by 16 with flips, rendering toggled near dots 257 to 320). Equal state at the line's end, including the flags. Review Focus 3 rows included.
- [ ] **Step 2 to 4: as task 3.** **Step 5: the gate,** plus `sprite_hit_tests_2005.10.05` and `sprite_overflow_tests` through the lazy build (they are in the acceptance test already). **Step 6: Measure.** Journal. **Commit** `perf(nes): sprites on the fast scanline path`; push.

---

### Task 5: The APU's batch

**Files:** `Apu.cs`, `ApuChannels.cs`, `NesBus.cs`; `ApuCatchUpTests.cs`

**What it must do.** The APU keeps a logical cycle and a caught-up cycle. A catch-up over `n` cycles runs one tight loop over the channels' timers, skipping silent channels, with no bus call, and gives the sample buffer the same samples as stepping each cycle. It is caught up before a CPU access to `$4000`-`$4017`, at its next event (the frame counter's IRQ cycle, the next DMC fetch, the DMC's IRQ), before a DMC DMA stall, and when the page reads the samples. `Apu.Irq` is exact on every cycle: the next IRQ cycle is an event.

- [ ] **Step 1: Write the failing tests:** scenes (frame counter modes and the `$4017` write delay, a DMC sample with and without loop and IRQ, length counters and sweeps across a long silent stretch, a catch-up of several frames) equal in state and in samples with per-cycle stepping; Review Focus 5 (a long catch-up must not skip a sample boundary).
- [ ] **Step 2 to 4 as before. Step 5: the gate,** including every APU ROM in the acceptance table. **Step 6: Measure.** Journal. **Commit** `perf(nes): the APU is caught up on demand`; push.

---

### Task 6: Measure, document, and report

**Files:** `docs/journal/2026-10-06-the-nes-lazy-chips.md`, `docs/known-differences.md`, `AGENTS.md` (rule 1's new wording, only if Dan has approved it), `docs/superpowers/specs/2026-10-05-nes-design.md` (a line pointing at the amendment), `README.md` if it says how the machine ticks

- [ ] **Step 1: The browser figure.** Native and browser AOT, NTSC and PAL, on the bundled game and the benchmark ROM, alternated with the baseline `5e48505`, with the load average and the BBC gauge, as tasks 6b and 17 did it. Report the gain and which kinds of game it does and does not reach (sprite-0 polling loops, MMC3).
- [ ] **Step 2: The full run.** NES tests, the core's, the KIM-1's and the BBC's, `dotnet build -c Release`, the dash check, the site tests, the browser check, the extended differential one last time, and the mutation pass over the changed code (a table in the journal).
- [ ] **Step 3: Final review** of the whole branch on the most capable model, against the spec and this plan, with one question first: **is there any point at which a chip can be seen that the differential does not hash?** Apply what it finds in one fix wave.
- [ ] **Step 4: Report to Dan:** the gain, the limits, the pull request, and the file to merge. Wait for his go. Merging deploys.

---

## Self-review

**Spec coverage.** Catch-up points: before register access and OAM DMA (task 2), the next event for the NMI (2), frame end (2, review focus 5), the exact path for boards that watch the bus (2). The fast scanline renderer in steps: idle and background lines (3), sprites and flags (4). The APU batch (5). The gate: extended first (1), run in every task. The speed target and its honesty: measured in 6, per step in 2 to 5. Rule 1's wording: task 6, with Dan's approval. Out of scope (MMC3's clock, threads, frame skipping) stays out.

**Placeholders.** None intended. The state list in task 1 is written from the code at the time and is a gate in itself: step 4 shows it can fail.

**Known soft spots.** The fast path is a second implementation of the PPU's line, and the equivalence is only as good as the state list and the scenes. The differential and the mutation pass are what make that acceptable, and the plan stops early if they show a gap that cannot be closed. The gain is for games that leave a line alone: a game that polls `$2002` every few cycles through the frame stays on the exact path for those lines.
