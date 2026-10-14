---
title: "The reset button in the middle of sprite evaluation"
date: 2026-10-06
summary: "A fault pass in the lazy chips work found a crash that has been in the NES's PPU since it was built: press reset in the middle of sprite evaluation, switch rendering on after dot 1 of a line with sprites in range, and evaluation writes past the end of secondary OAM and stops the machine. The test came first and failed with the exception. The fix wraps the place in secondary OAM, as the chip's counter does, rather than clearing evaluation in the reset, which the sheet does not say. Nothing else changed: the whole NES project, the community ROMs and the differential's output are as they were."
order: 37
---

# 6 October 2026: the reset button in the middle of sprite evaluation

## What happened

While the lazy chips work was checking its gate (see
[the lazy chips entry](2026-10-06-the-nes-lazy-chips.md) on the branch that
holds it), a planted fault made one synthetic run, `fuzz-mmc3-3` on NTSC, throw
`IndexOutOfRangeException` in `Ppu.EvaluationStep`. The fault did not cause it.
The code at `5e48505` does it too, and it is a bug in the live NES: the exception
stops the machine on the page.

`Ppu.Reset` clears the count of sprites found (`_found`) and the sprites on the
line, but not the rest of sprite evaluation. If the reset button is pressed in
the middle of evaluation, and rendering is next switched on after dot 1 of a
visible line, the line's own start of evaluation (dot 1) does not happen. With
sprites in range, evaluation carries on from the old place in secondary OAM
(`_secondaryIndex`) with the count back at 0. It then copies eight sprites more,
which is 32 bytes more, and the index goes past 32.

## What the chip resets, and what it does not

`ppu.md` section 12 lists what a reset does to the registers. It says nothing of
sprite evaluation. In the model, `Ppu.Reset` before this change:

| Resets | Keeps |
| --- | --- |
| `_found`, `_sprite0Found`, `_spriteCount`, `_sprite0OnLine`, the sprite line buffer | `_secondaryOam`, `_evaluationN`, `_evaluationM`, `_oamLatch`, `_secondaryIndex`, `_secondaryFull`, `_evaluationDone` |

## The test, first

`TheResetButtonInTheMiddleOfEvaluationLeavesItSafeAndTheNextLineClean`, in both
regions. All 64 sprites are at Y = 0 and 8 by 16, rendering is on, the PPU runs
to line 10 dot 105 (five sprites copied), the reset is pressed, ten dots run, and
`$2001` is written with `$18`. The PPU then runs to line 20. On the code as it
was, both regions failed with `IndexOutOfRangeException` at
`PpuSprites.cs` line 59. The test also checks the picture as the sheet gives it:
line 1 starts evaluation afresh, so line 2 shows sprites 0 to 7 and not the ninth.

## The fix, and what it was chosen over

The place in secondary OAM is now `(index + 1) & 31`, where it is stepped. The
chip's own counter is 5 bits and wraps.

- **Chosen over clearing all of evaluation in `Ppu.Reset`.** The sheet does not
  say a reset clears it, and the real chip is not known to. It would also fix only
  this path. The wrap makes the write safe for any state the index could be in.
- **Chosen over a bounds check.** A check would say "stop writing", which the
  chip does not do either. The wrap is the chip's counter, and costs one AND.

The difference is in
[`known-differences.md`](../known-differences.md), under the PPU's picture.

## Checked

On the machine this was written on, 6 October 2026:

- `dotnet test tests/Dbhq.Machines.Nes.Tests -c Release`: all pass, with the two new
  cases, including `BlarggTests` (`sprite_hit_tests`, `sprite_overflow_tests`,
  `ppu_vbl_nmi`).
- `dotnet test tests/Dbhq.Cpu6502.Tests -c Release --filter Nestest`: both pass.
- The differential (`bench/nes-speed/differential`, the version on `main`) run
  on a `git archive` of `5e48505` and on this change: the two output files are
  identical with `cmp`. The old harness has no run that reaches the crash, so
  this shows only that nothing else moved.
