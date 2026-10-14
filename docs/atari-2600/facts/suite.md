# Atari 2600 suite: every test, its status and the task it waits for

This sheet lists every test in the pinned `missingno-vcs-tests` suite, read from its header, with its kind, its status under the spec's trust split, its board, the machine parts it needs, and the plan task in which it should first pass.

Written 14 October 2026 for the Atari 2600 plan, task 1. Tags and sources are in [`README.md`](README.md). Used by tasks 2 (the boards and the suite's cartridge files), 4 (`SuiteSelfTests` and its "not yet" list), 5, 6, 7 and 9 (the rows each adds), 7 (the screenshot runner and the reference pictures) and 11 (the full sweep and the recorded outcomes).

## 1. How this sheet reads the suite

### 1.1 The two forks

- The sources, headers and reference pictures are in `dbhq-uk/missingno-vcs-tests` at `891c079976a2f68033ca4af2fe2fd85aaf1906c9`. Every `.asm` under `roms/` opens with a comment block that states the behaviour, the expected result and how a wrong model shows [from the suite: README.md].
- The prebuilt binaries are in `dbhq-uk/missingno` at `a4159f47ce7ef6f9c3d5402a8c58b18940045adc`, under `crates/systems/vcs/tests/accuracy/roms/<folder>/`. There are 381 `.a26` files, three per test: `_ntsc`, `_pal` and `_secam` [from the binaries] (counted with `ls crates/systems/vcs/tests/accuracy/roms/*/ | grep -c a26`, 14 October 2026).
- A test's header is its leading comment block, up to the first line that is neither a comment nor blank. It was printed for all 127 sources with `tools/probes/atari-2600/headers.sh`, an `awk` filter that keeps exactly that block.

### 1.2 Kind

- **Self-test**: the source includes `result.h` and writes its verdict to RAM at `$80` [from the suite: include/result.h].
- **Screenshot**: a `_ntsc.png` sits beside the source. Each one also has a `_pal.png`, a `_secam.png` and a `_pal_capture.png` [from the suite: README.md].
- The two kinds do not overlap. 100 sources include `result.h` and have no picture; 27 have all four pictures and do not include `result.h`. No test is both a self-test and a screenshot test (14 October 2026, a loop over `roms/*/*.asm` that tested `grep -c 'include "result.h"'` and the existence of each picture). A test whose header calls itself a diagnostic is marked in the Kind column.

### 1.3 Status

By the spec's table "Which suite results are trusted" [from the spec]:

| Results | Status |
|---|---|
| Self-tests in `cpu/`, `riot/`, `collision/`, `tia-timing/`, `tia-render/`, and `harness/sanity` | Trusted |
| `cartridge/mirror-2k`, `bank-f8`, `bank-f6`, `bank-f4` | Trusted |
| `cartridge/bank-fa`, `bank-hotspot-window`, `ram-superchip`, `power-on-bank` | Recorded |
| Screenshot tests (all in `tia-timing/`, `tia-render/` and `harness/calibration`) | Recorded |
| Every other `cartridge/` test (an unmodelled board), and every SECAM build | Not run |

The trust split gives 58 trusted tests (54 self-tests outside `cartridge/` and 4 in it), 31 recorded (4 cartridge self-tests and 27 screenshot tests) and 38 not run [inferring: the table above applied to the rows of section 5].

### 1.4 Board

The board is the header's `; mapper:` line. All 46 sources in `cartridge/` carry one, and no source outside `cartridge/` does (`grep -l -i '; *mapper' roms/*/*.asm | wc -l` gives 46, 14 October 2026). An unmarked source is a plain 4K board [from the harness: crates/systems/vcs/tests/accuracy/common/mod.rs, the comment on the stated board: "an unmarked source is a plain 4K board"]. That agrees with the binaries: all 243 non-cartridge `.a26` files are 4096 bytes [from the binaries] (`stat -c %s` over the six folders, 14 October 2026).

### 1.5 Parts, and the task that builds each

| Part | Short name in the tables | Built in |
|---|---|---|
| The boards: plain 2K and 4K, F8, F6, F4, FA, the Superchip | board | 2 |
| The RIOT: RAM, the timer, ports A and B with their direction registers, the PA7 edge | RIOT RAM, timer, ports, PA7 | 3 (alone), 4 (on the bus) |
| The 13-bit decode and every mirror, the floating bus, the WSYNC halt | decode, mirrors, floating bus, WSYNC | 4 |
| The TIA's horizontal counter and VSYNC, the registers stored | line | 4 |
| RSYNC, VBLANK, the playfield, `CTRLPF` reflect, `COLUPF` and `COLUBK`, the palette, the frame | RSYNC, VBLANK, playfield, colour | 5 |
| Players, missiles and ball with their resets, `NUSIZ`, `REFP`, `VDELxx`, `RESMPx`, priority, score mode, the fifteen collision latches | objects, priority, collisions | 6 |
| HMOVE, `HMCLR`, the motion clocks, the comb, late HMOVE, writes during HMOVE (the stuck latch) | HMOVE | 7 |
| The screenshot runner and the partition rule | runner | 7 |
| INPT0 to INPT5, the VBLANK dump and latch bits, the joysticks and switches | inputs | 9 |

[from the plan: tasks 2 to 9, "Files" and "What it must do"]

### 1.6 The task rule

The Task column is the earliest task by whose end every part the test needs is built, with these fixed points [inferring: section 1.5 joined with each header and with the registers each source writes, counted by `tools/probes/atari-2600/regs.sh`]:

- No suite ROM can run before task 4, because the machine and the bus arrive there. So a test that needs only the RIOT and the bus says 4.
- A test that reads a collision register only while every latch is clear (`cpu/open-bus`, `riot/io-mirrors`) needs no object, because a TIA with no objects drawn leaves D7 and D6 at 0. It says 4 [inferring: the header says each probe is taken after `CXCLR` or with nothing drawn].
- A self-test needs every part any of its sub-checks uses, because it stops at its first failure and a later check still has to pass. For example `cpu/partial-drive` needs the collisions (task 6) for codes `$05` and `$06` and the inputs (task 9) for codes `$02` to `$04`, so it says 9.
- A screenshot test is first compared in task 7, where the runner is built, and its outcome is recorded in task 11. Its Task cell reads `7 (parts N)`, where N is the task that completes its parts.
- A trusted cartridge test needs its board (task 2) and the bus (task 4). Its cell reads `2 (board), 4 (run)`. A recorded one adds `11 (recorded)`.
- A not-run test has no task. Its cell names the board's issue.

## 2. The result protocol

| Address | Name | Meaning |
|---|---|---|
| `$80` | RESULT | `$00` while running, `$A5` pass, `$5A` fail |
| `$81` | CODE | which check failed, 1 to 255; 0 on a pass |
| `$82` | OBSERVED | the value seen |
| `$83` | EXPECTED | the value the test wanted |
| `$85` to `$89` | | scratch for the result screen (colour and two font pointers) |

[from the suite: include/result.h] The magic bytes are `PASS_MAGIC = $A5` and `FAIL_MAGIC = $5A` [from the suite: include/result.h]. The harness states the same four addresses and two bytes [from the harness: crates/systems/vcs/tests/accuracy/common/mod.rs, constants `RESULT` to `FAIL_MAGIC`].

- `TEST_BEGIN` runs after the start-up and clears carry and overflow, then zeroes `$80` to `$83`, so the state is deterministic from there [from the suite: include/result.h].
- A self-test stops at its first failing check. CODE, OBSERVED and EXPECTED are to be read as hexadecimal, and two tests can fail with the same bytes, so the runner must keep which ROM ran [from the suite: include/result.h].
- After the verdict a ROM draws a green pass screen or a red fail screen with CODE, OBSERVED and EXPECTED in hex digits, using the playfield [from the suite: include/result_screen.asm].
- Some tests put a measurement in OBSERVED on a pass and show it on the pass screen (`pass_result_observed`): `riot/pa7-polarity`, `collision/resbl-kill`, `cartridge/power-on-bank`, `bank-sb` [from the suite: each header].

Worked example: `riot/timer` fails with CODE `$02` if one line of `TIM1T` does not fall by 76, which is what a model whose timer stops during the WSYNC halt gives [from the suite: roms/riot/timer.asm]. Its check is `ASSERT_EQ $92, 76, $02` [from the suite: roms/riot/timer.asm, the code], so after the fail `$80` holds `$5A`, `$81` holds `$02`, `$82` the difference the model measured and `$83` holds `$4C`, which is 76 [inferring: the macro in result.h stores the three bytes; the hex is arithmetic].

## 3. The frame each region build makes

| Build | VSYNC | VBLANK | Visible | Overscan | Total | Field |
|---|---|---|---|---|---|---|
| NTSC (no flag) | 3 | 37 | 192 | 30 | 262 | 60 Hz |
| PAL (`-DPAL`) | 3 | 45 | 228 | 36 | 312 | 50 Hz |
| SECAM (`-DSECAM`) | 3 | 45 | 228 | 36 | 312 | 50 Hz |

[from the suite: include/region.h; Makefile] Every line is 228 colour clocks, 76 CPU cycles, in every build; only the line counts differ [from the suite: include/region.h].

- `frame.asm` gives the shape: `vertical_sync` sets VSYNC, waits three `WSYNC`s and clears it; `vblank_lines` sets VBLANK bit 1 for `VBLANK_LINES` lines; the test draws `VISIBLE_LINES`; `overscan_lines` sets VBLANK for `OVERSCAN_LINES` [from the suite: include/frame.asm]. 112 sources include it (`grep -h 'include "frame.asm"' roms/*/*.asm | wc -l`, 14 October 2026).
- The pass and fail screens use the same counts from `region.h` [from the suite: include/result_screen.asm].
- The region changes only the line counts and the result-screen colours: the pass green is `$C8` on NTSC and `$58` on PAL, because hue `$C` shows violet on a PAL console [from the suite: include/region.h, which says this was confirmed on a PAL console on 16 July 2026].
- Self-test verdicts do not depend on the region [from the suite: include/region.h; README.md].

Worked example: the NTSC frame is 3 + 37 + 192 + 30 = 262 lines; a picture of every line after VSYNC is 259 rows, and the PAL one 309 [inferring: arithmetic on the table]. The README gives 160 by 259 and 160 by 309 for the references [from the suite: README.md], and section 4 measures them.

## 4. The reference pictures

Measured on 14 October 2026 with `tools/probes/atari-2600/pnginfo.py`, which decodes each PNG with the standard library:

| File | Count | Size | Format |
|---|---|---|---|
| `*_ntsc.png` | 26 | 160 by 259 | 8-bit RGBA (colour type 6), not interlaced, filter 1 on every row, alpha 255 everywhere |
| `*_ntsc.png` | 1 (`tia-timing/midline-resp`) | 160 by 260 | as above |
| `*_pal.png`, `*_secam.png` | 26 each | 160 by 309 | as above |
| `*_pal.png`, `*_secam.png` | 1 each (`midline-resp`) | 160 by 310 | as above |
| `*_pal_capture.png` | 27 | 1920 by 1080 | 8-bit RGB (colour type 2) |

[from the suite: the PNG files themselves]

- The reference rows are the frame's lines less the three VSYNC lines (262 - 3 = 259; 312 - 3 = 309). That supports the README's statement and not the harness comment that references "carry the VSYNC-period lines" [inferring: section 3's counts against these sizes; from the harness: crates/systems/vcs/tests/accuracy/common/mod.rs, the comment in the screenshot comparison]. Item 4 of the plan's "Fixed before running" still settles the offset on `harness/calibration`; this is evidence for it, not a substitute.
- `midline-resp` is one row taller in every region. Why is not established `[guessing - verify]`: the test's frame may have one more line than the standard field.
- The references are missingno's renders through its palette; the captures are the PAL console's output [from the suite: README.md].

## 5. The tests

Source of every row: that test's header, `[from the suite: roms/<folder>/<test>.asm]`. The Board column follows section 1.4, and the Parts and Task columns are `[inferring]` by the rule in section 1.6. The Behaviour column is a summary in this sheet's words.

### 5.1 `cpu/` (7)

| Test | Kind | Status | Board | Parts | Task | Behaviour |
|---|---|---|---|---|---|---|
| `cpu/floating-bus` | self-test | Trusted | 4K | floating bus, TIA read decode | 4 | A read of a TIA address ending in `$E` or `$F` drives nothing, so `lda $2E` returns `$2E` and `lda $3E` returns `$3E`. |
| `cpu/open-bus` | self-test | Trusted | 4K | floating bus, TIA read decode, collision registers (all clear) | 4 | A TIA read merges its driven bits with the bus residue; an absolute read leaves the address's high byte as the residue (`lda $013E` gives `$01`). |
| `cpu/partial-drive` | self-test | Trusted | 4K | floating bus, objects, collisions, inputs (INPT0 dumped by VBLANK bit 7, INPT4 at rest) | 9 | Collision registers drive D7 and D6 and input ports D7 only, over the residue, through the A4 to A6 mirrors (`lda $3C` gives `$BC`). |
| `cpu/rmw-strobe` | self-test | Trusted | 4K | missiles, collisions, WSYNC | 6 | `dec RESM0` strobes twice a cycle apart, and the missile lands where the second write puts it. |
| `cpu/rmw-wsync` | self-test | Trusted | 4K | WSYNC, RIOT timer | 4 | `inc WSYNC` costs one line, as `sta WSYNC` does, because the halt stops reads and lets the second write through. |
| `cpu/stack-aliases-ram` | self-test | Trusted | 4K | decode, RAM mirror at `$0180` | 4 | A push to `$01FF` lands in RAM `$FF` and nowhere else, and a pull from `$01FE` reads RAM `$FE`. |
| `cpu/stack-aliases-tia` | self-test | Trusted | 4K | TIA mirror at `$0100-$017F`, missiles, playfield, collisions | 6 | With SP at `$1D` a push writes ENAM0; a JSR's two pushes write ENAM1 then ENAM0. |

### 5.2 `riot/` (11)

| Test | Kind | Status | Board | Parts | Task | Behaviour |
|---|---|---|---|---|---|---|
| `riot/io-mirrors` | self-test | Trusted | 4K | decode, RIOT I/O mirrors, ports, TIA read mirror (latches clear) | 4 | SWCHA answers at `$380`, `$680` and `$A80`, SWACNT at `$381`, and CXM0P at `$40`. |
| `riot/io-output` | self-test | Trusted | 4K | ports (idle input pins read 1) | 4 | Port A's output register keeps every written bit whatever the direction; an input pin reads 1 when nothing drives it. |
| `riot/io-ports` | self-test | Trusted | 4K | ports | 4 | With SWACNT `$FF`, SWCHA reads back `$55`, `$AA`, `$FF` and `$00` as written. |
| `riot/pa7-edge` | self-test | Trusted | 4K | ports, PA7, timer flag | 4 | An edge of the armed polarity sets TIMINT bit 6; a TIMINT read clears bit 6 and leaves bit 7. |
| `riot/pa7-polarity` | self-test | Trusted | 4K | ports, PA7 | 4 | Arming a polarity at `$284` or `$285` never sets the edge flag by itself, at either pin level; OBSERVED on a pass is `$10`. |
| `riot/ram-mirrors` | self-test | Trusted | 4K | decode, RAM mirrors | 4 | `$0080`, `$0180`, `$0480` and `$0880` are one cell. |
| `riot/ram` | self-test | Trusted | 4K | RIOT RAM | 4 | Cells `$90` to `$F9` hold `$55`, `$AA`, `$00` and their own address, and setting one disturbs no neighbour. |
| `riot/timer-divisors` | self-test | Trusted | 4K | timer, WSYNC | 4 | TIM64T falls 19 over 16 lines; a TIMINT read leaves the timer flag and an INTIM read clears it. |
| `riot/timer-underflow-read` | self-test | Trusted | 4K | timer | 4 | An INTIM read on the cycle of the underflow returns `$FF` and leaves the timer flag set. |
| `riot/timer-vblank-spin` | self-test | Trusted | 4K | timer (prescaler phase) | 4 | Eight sampling phases of an `lda INTIM / bne` loop count 46, then 47 five times, then 48 twice. |
| `riot/timer` | self-test | Trusted | 4K | timer, WSYNC | 4 | TIM1T falls 76 a line and TIM8T 76 over eight lines; underflow sets the flag; the timer runs through a WSYNC halt. |

### 5.3 `harness/` (2)

| Test | Kind | Status | Board | Parts | Task | Behaviour |
|---|---|---|---|---|---|---|
| `harness/calibration` | screenshot; "not a behaviour test" | Recorded | 4K | playfield, colour, ball | 7 (parts 6) | Nineteen bands of 4-on, 12-off playfield stripes with 1-pixel ball probes, for fitting the capture rig; the plan's alignment check (item 4) and speed bench (item 10). |
| `harness/sanity` | self-test | Trusted | 4K | RIOT RAM | 4 | Writes a pass and nothing else, which proves the runner reads `$80`. |

### 5.4 `tia-timing/` (30)

| Test | Kind | Status | Board | Parts | Task | Behaviour |
|---|---|---|---|---|---|---|
| `tia-timing/hmove-apply` | self-test | Trusted | 4K | missiles, collisions, HMOVE | 7 | Every non-zero `HMM0` value moves the missile (profile `$0001`), and `HMCLR` cancels a pending move. |
| `tia-timing/hmove-comb` | screenshot | Recorded | 4K | playfield, HMOVE | 7 (parts 7) | HMOVE blanks pixels 0 to 7 of its line, so the stripe at x 0 to 3 dashes 4 lines on, 4 off. |
| `tia-timing/hmove-late` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | A late HMOVE with `HMM0 = $70` moves less; 16 bands two cycles apart land at x 20, 21, 23, 24, 26, 27, 29, then 23. |
| `tia-timing/hmove-line-aligned` | self-test | Trusted | 4K | missiles, collisions, HMOVE | 7 | An HMOVE right after WSYNC moves the same on any line. |
| `tia-timing/hmove-line-end` | screenshot | Recorded | 4K | missiles, colour, HMOVE | 7 (parts 7) | HMOVE written at cycles 65 to 80 of the line: from 75 the comb and the move fall on the next line; just before, `$70` moves up to 15 pixels with no comb. |
| `tia-timing/hmove-live-reach` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | A mid-line HMOVE's pulses distort a parked dot no further right than x 98. |
| `tia-timing/hmove-live-seam` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | A mid-line HMOVE that nets no move still swallows a dot at column 2 mod 4 and widens one at 3 mod 4. |
| `tia-timing/hmove-reset-merge` | self-test | Trusted | 4K | ball, playfield, collisions, HMOVE | 7 | A reset and a motion pulse on the same colour clock move the counter once; HMOVE then RESBL three cycles later puts the ball at column 3. |
| `tia-timing/hmove-rewrite-race` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | Rewriting `HMM0` to `$60` at cycle 24 after a cycle-3 HMOVE jams the motion latch (the Cosmic Ark starfield); one cycle either side does not. |
| `tia-timing/hmove-strobe-line` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | The line that carries HMOVE, across 16 strobe times, including the comb and the pulse grid. |
| `tia-timing/hmove-stuck-grid` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | The stuck motion pulses sit on a grid fixed to the line, not to the HMOVE write. |
| `tia-timing/hmove-stuck-latch` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | A jammed latch drifts the missile 17 pixels left a line with no further HMOVE until `HMM0 = $80` frees it at x 27. |
| `tia-timing/hmove-stuck-player` | screenshot | Recorded | 4K | players, HMOVE | 7 (parts 7) | Under the stuck latch a `$B4` player draws intact on three lines in four and deforms on the fourth. |
| `tia-timing/hmove-stuck-release` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | Of the sixteen values `$00` to `$F0`, only `$80` frees a stuck latch. |
| `tia-timing/hmove-stuck-straddle` | screenshot | Recorded | 4K | players, HMOVE | 7 (parts 7) | A stuck pulse at the line's last slot, colour clock 225, follows the same merge rule as mid-line. |
| `tia-timing/hmove-stuck-stretch` | screenshot | Recorded | 4K | players, NUSIZ, HMOVE | 7 (parts 7) | Under the stuck latch a double-width player deforms every fourth line and a quad-width one slides whole. |
| `tia-timing/hmove-stuck-widths` | screenshot | Recorded | 4K | missiles, ball, HMOVE | 7 (parts 7) | The stuck-pulse distortion at widths 1, 2, 4 and 8 for a missile and the ball. |
| `tia-timing/hmove-values` | screenshot | Recorded | 4K | missiles, HMOVE | 7 (parts 7) | All sixteen `HMM0` values as a ruler: `$10` to `$70` move 1 to 7 left, `$80` to `$F0` move 8 to 1 right. |
| `tia-timing/hmove-walk` | self-test | Trusted | 4K | missiles, playfield, collisions, HMOVE | 7 | Each HMOVE with `HMM0 = $80` moves the missile 8 right again; only 7 and 8 strobes reach the block at x 64 to 79. |
| `tia-timing/midline-color` | screenshot | Recorded | 4K | colour | 7 (parts 5) | A `COLUBK` write changes the line from the colour clock it lands on; a 9-pixel pulse steps 6 left a line from x 91 to x 25. |
| `tia-timing/midline-grp` | screenshot | Recorded | 4K | players, NUSIZ | 7 (parts 6) | A `GRP0` write inside a quad-width player changes the bits not yet drawn. |
| `tia-timing/midline-pf` | screenshot | Recorded | 4K | playfield | 7 (parts 5) | A PF2 write mid-line changes only the cells not yet drawn. |
| `tia-timing/midline-resp` | screenshot | Recorded | 4K | players | 7 (parts 6) | RESP0 written later each line walks a 1-pixel player from x 39 to x 99 in steps of 6. |
| `tia-timing/midline-vblank` | screenshot | Recorded | 4K | playfield, VBLANK | 7 (parts 5) | VBLANK bit 1 blanks the picture from the colour clock it is set. |
| `tia-timing/nusiz-draw` | screenshot | Recorded | 4K | players, NUSIZ | 7 (parts 6) | A `NUSIZ0` write in the blank takes effect on that line; eight bands show the eight layouts. |
| `tia-timing/reset-same-line` | self-test | Trusted | 4K | ball, missiles, playfield, collisions | 6 | RESBL mid-line draws the ball on that line; RESM0 draws nothing until the next. |
| `tia-timing/resp-restrobe` | self-test | Trusted | 4K | players, NUSIZ, playfield, collisions | 6 | RESP0 and RESP1 strobed every 3 cycles draw a copy 5 clocks after each strobe but the first. |
| `tia-timing/rsync` | self-test | Trusted | 4K | RSYNC, playfield, missiles, collisions | 6 | RSYNC restarts the playfield's counter and not the objects', so a parked missile meets the playfield. |
| `tia-timing/wsync-line-end` | self-test | Trusted | 4K | WSYNC, timer | 4 | A WSYNC written at cycle 76 costs nothing extra; one written at cycle 77 to 80 waits a whole line more. |
| `tia-timing/wsync` | self-test | Trusted | 4K | WSYNC, timer | 4 | Two WSYNCs take exactly 76 cycles more than one. |

### 5.5 `tia-render/` (15)

| Test | Kind | Status | Board | Parts | Task | Behaviour |
|---|---|---|---|---|---|---|
| `tia-render/colors` | screenshot | Recorded | 4K | colour | 7 (parts 5) | `COLUBK` steps through the 128 even codes, one a line; bit 0 is ignored. |
| `tia-render/draw-delay` | screenshot | Recorded | 4K | objects, priority | 7 (parts 6) | From one reset, a player's first pixel is one clock right of a missile's or the ball's. |
| `tia-render/missile-copies` | self-test | Trusted | 4K | missiles, NUSIZ, collisions, HMOVE (to place the probe) | 7 | `NUSIZ` copy modes copy the missile as they copy the player; modes 5 and 7 do not widen it. |
| `tia-render/missile-reset-lock` | self-test | Trusted | 4K | missiles, players, `RESMP0`, collisions, HMOVE (to walk the probe) | 7 | Released from `RESMP0`, the missile lands 4, 6 or 10 clocks right of a 1x, 2x or 4x player's first pixel. |
| `tia-render/missiles-ball` | self-test | Trusted | 4K | missiles, ball, playfield, collisions | 6 | ENAM0, ENAM1 and ENABL switch each object; width 1 and width 8 are told apart. |
| `tia-render/nusiz` | self-test | Trusted | 4K | players, NUSIZ, missiles, collisions | 6 | The eight `NUSIZ` copy modes; stretched modes start one clock later. |
| `tia-render/object-priority` | screenshot | Recorded | 4K | objects, playfield, priority | 7 (parts 6) | P0 and M0 over P1 and M1 over playfield and ball over background. |
| `tia-render/pf-priority` | screenshot | Recorded | 4K | players, playfield, priority | 7 (parts 6) | `CTRLPF` bit 2 puts the playfield in front of the players. |
| `tia-render/player-reflect` | self-test | Trusted | 4K | players, `REFP0`, missiles, collisions | 6 | `REFP0` bit 3 draws GRP0 bit 0 first; profiles `$00 $0A` plain and `$40 $01` reflected. |
| `tia-render/players` | self-test | Trusted | 4K | players, missiles, collisions | 6 | A player draws bit 7 at its left edge; profile `$00 $0A`. |
| `tia-render/playfield-reflect` | self-test | Trusted | 4K | playfield, missiles, collisions | 6 | `CTRLPF` bit 0 clear repeats the left half at x 80; set mirrors it to x 144. |
| `tia-render/playfield` | self-test | Trusted | 4K | playfield, missiles, collisions | 6 | PF0 draws D4 to D7, PF1 D7 to D0 and PF2 D0 to D7, four pixels a bit. |
| `tia-render/positioning` | self-test | Trusted | 4K | players, collisions | 6 | Each cycle later a reset strobe lands the object three pixels further right. |
| `tia-render/score-mode` | screenshot | Recorded | 4K | playfield, score mode | 7 (parts 6) | `CTRLPF` bit 1 draws the left half's playfield in `COLUP0` and the right half's in `COLUP1`. |
| `tia-render/vertical-delay` | self-test | Trusted | 4K | players, `VDELP0`, missiles, collisions | 6 | A `GRP1` write copies player 0's new graphics to its old; `VDELP0` picks which is drawn. |

### 5.6 `collision/` (16)

| Test | Kind | Status | Board | Parts | Task | Behaviour |
|---|---|---|---|---|---|---|
| `collision/all-pairs` | self-test | Trusted | 4K | objects, playfield, collisions | 6 | Each of the fifteen pairs has its own latch on its own bit, and no latch answers for another pair. |
| `collision/blank-gating` | self-test | Trusted | 4K | missiles, collisions, VBLANK, HMOVE | 7 | Objects that meet inside the horizontal blank still collide; only VBLANK bit 1 stops the latches. |
| `collision/blanked-reset` | self-test | Trusted | 4K | missiles, ball, players, collisions, HMOVE | 7 | A missile or ball reset under HMOVE's longer blank lands 2 clocks after the blank ends; a reset at clock 75 lands one pixel later. |
| `collision/copy-adjacency` | self-test | Trusted | 4K | players, NUSIZ, collisions, HMOVE | 7 | Player 0 hits player 1's second copy only where lit pixels share a column; placed as Pitfall! places them. |
| `collision/hmove-edge` | self-test | Trusted | 4K | missiles, players, collisions, HMOVE | 7 | Stepping a 1-pixel missile right one pixel an HMOVE, the hits are exactly eight steps wide. |
| `collision/latches` | self-test | Trusted | 4K | players, missiles, playfield, collisions | 6 | A latch sets on overlap, stays clear without one, and clears only on `CXCLR`. |
| `collision/latency-swallow` | self-test | Trusted | 4K | objects, playfield, collisions, HMOVE (stuck) | 7 | A player's first pixel comes 5 clocks after its reset and a missile's or ball's 4; a swallowed stuck dot collides with nothing. |
| `collision/merge-delivery-replant` | self-test (no region line in its header) | Trusted | 4K | players, collisions, HMOVE (stuck) | 7 | A RESP0 during the start decode, with a motion pulse on it, never loses the copy or its merge pixel; profile `$4D` and `$E0`. |
| `collision/merge-delivery-stretch` | self-test | Trusted | 4K | players, NUSIZ, collisions, HMOVE | 7 | For a double-width player, a lone merged pulse is taken by the start decode and even bits and refused by odd ones. |
| `collision/merge-delivery-train` | self-test | Trusted | 4K | players, NUSIZ, collisions, HMOVE (stuck) | 7 | The same rule inside a stuck pulse train; from the fourth bit the grid phase decides. |
| `collision/merge-delivery` | self-test | Trusted | 4K | players, collisions, HMOVE | 7 | A merged pulse reaches a player's scan only when the scan's state allows; a reset over the start decode suppresses the copy. |
| `collision/onset-sweep` | self-test | Trusted | 4K | players, missiles, collisions | 6 | Sweeping a missile over a quad player, the hits form one run as wide as the overlap. |
| `collision/per-pixel` | self-test | Trusted | 4K | players, collisions | 6 | Latches follow lit pixels, not spans (`$F0` against `$0F` never hits), and stay set until `CXCLR`. |
| `collision/resbl-kill` | self-test | Trusted | 4K | ball, missiles, collisions, HMOVE | 7 | A RESBL write erases a ball pixel committed but not yet shown, two clocks wide; the new ball lands at column 26. |
| `collision/reset-phase` | self-test | Trusted | 4K | objects, playfield, collisions | 6 | A reset lands its object right at every colour-clock phase of the strobe (the Surround false hit). |
| `collision/stuck-drift` | self-test | Trusted | 4K | missiles, playfield, collisions, HMOVE (stuck) | 7 | Under the stuck latch a swallowed dot collides with nothing and a widened one still collides. |

### 5.7 `cartridge/`, the modelled boards (8)

| Test | Kind | Status | Board | Parts | Task | Behaviour |
|---|---|---|---|---|---|---|
| `cartridge/mirror-2k` | self-test | Trusted | 2K | board, decode | 2 (board), 4 (run) | A 2K image answers at `$F000` and `$F800`, for data and code; the reset vector at `$FFFC` points to `$F800`. |
| `cartridge/bank-f8` | self-test | Trusted | F8 | board | 2 (board), 4 (run) | `$1FF8` and `$1FF9` select banks 0 and 1, by read or write; the next cycle comes from the new bank. |
| `cartridge/bank-f6` | self-test | Trusted | F6 | board | 2 (board), 4 (run) | `$1FF6` to `$1FF9` select banks 0 to 3, by read or write. |
| `cartridge/bank-f4` | self-test | Trusted | F4 | board | 2 (board), 4 (run) | `$1FF4` to `$1FFB` select banks 0 to 7, by read or write. |
| `cartridge/bank-fa` | self-test | Recorded | FA | board (FA, its RAM and the D0 gate) | 2 (board), 4 (run), 11 (recorded) | `$1FF8` to `$1FFA` select banks 0 to 2 only when D0 is 1, a read's D0 coming from the bank showing before the switch; 256 bytes of RAM written at `$F000` and read at `$F100`. |
| `cartridge/bank-hotspot-window` | self-test | Recorded | F8 | board | 2 (board), 4 (run), 11 (recorded) | Reads near the hotspots come from the bank showing; a read of a hotspot returns the byte of the bank it selects (codes `$0C`, `$0E`). |
| `cartridge/power-on-bank` | self-test; "a diagnostic, not a pass/fail test" | Recorded | F8 | board (power-on bank) | 2 (board), 4 (run), 11 (recorded) | Always passes; OBSERVED is `$A0` if the board woke in bank 0 and `$B1` if bank 1. |
| `cartridge/ram-superchip` | self-test | Recorded | F8SC | board (F8 with the Superchip) | 2 (board), 4 (run), 11 (recorded) | 128 bytes written at `$F000` and read at `$F080`, shared by both banks; a page-crossing read that touches the write port stores the bus residue `$F0` in the cell. |

The plan adds rows to `SuiteSelfTests` for `cpu/`, `riot/` and `harness/sanity` in task 4 and fills the table in task 11; no task before 11 names the four trusted cartridge tests, though the bus can run them from task 4 [from the plan: tasks 4 and 11].

### 5.8 `cartridge/`, the boards not modelled (38)

All are self-tests, all are Not run, and none has a task: each waits for its board's issue, under [#107](https://github.com/dbhq-uk/6502/issues/107). The issue numbers are the issue titles "Atari 2600 board: <board>", and each issue's body names the same tests as this table [from the GitHub issues: #131 to #154, read with `gh issue view`].

| Test | Board | Issue | Behaviour |
|---|---|---|---|
| `cartridge/ar-config` | AR, an 8448-byte fastload image | [#154](https://github.com/dbhq-uk/6502/issues/154) | A read of `$F0nn` then a read of `$FFF8` sets the control register to `nn`; bits 4 to 2 pick one of eight bank layouts. |
| `cartridge/ar-hazards` | AR, 8448 bytes | [#154](https://github.com/dbhq-uk/6502/issues/154) | With writes enabled any `$F0xx` read arms a RAM write; the pending value waits any time for `$FFF8`. |
| `cartridge/ar-write` | AR, 8448 bytes | [#154](https://github.com/dbhq-uk/6502/issues/154) | A RAM write armed by a read lands five address-bus changes later. |
| `cartridge/bank-03e0` | 03E0 | [#152](https://github.com/dbhq-uk/6502/issues/152) | Accesses at `$0380-$03FF` page 1K slices into three segments, each enabled by A4, A5 or A6 low. |
| `cartridge/bank-0840` | 0840 | [#145](https://github.com/dbhq-uk/6502/issues/145) | Any access in `$0800-$0FFF` picks bank 0 or 1 by A6; `$F840` does not switch. |
| `cartridge/bank-0fa0` | 0FA0 | [#153](https://github.com/dbhq-uk/6502/issues/153) | Six address lines decode `$06A0` for bank 0 and `$06C0` for bank 1, with many aliases. |
| `cartridge/bank-3e-wide` | 3E | [#136](https://github.com/dbhq-uk/6502/issues/136) | A 256K image: 128 ROM banks and 32 RAM banks all reachable and distinct. |
| `cartridge/bank-3e` | 3E | [#136](https://github.com/dbhq-uk/6502/issues/136) | Writes to `$3F` page a 2K ROM bank and to `$3E` a 1K RAM bank into `$F000-$F7FF`; RAM read low, written high. |
| `cartridge/bank-3ep-wide` | 3E+ | [#137](https://github.com/dbhq-uk/6502/issues/137) | A 64K image: the value's top two bits pick the segment and the low six the bank, for all 64 banks. |
| `cartridge/bank-3ep` | 3E+ | [#137](https://github.com/dbhq-uk/6502/issues/137) | Four 1K segments each banked by one write packing segment and bank; segment 3 starts at ROM bank 0. |
| `cartridge/bank-3f` | 3F | [#132](https://github.com/dbhq-uk/6502/issues/132) | An access below `$40` arms a latch that takes the bus byte when A12 next rises, paging the lower 2K. |
| `cartridge/bank-bf` | BF | [#141](https://github.com/dbhq-uk/6502/issues/141) | `$1F80` to `$1FBF` select 64 banks; OBSERVED is a power-on diagnostic. |
| `cartridge/bank-df` | DF | [#142](https://github.com/dbhq-uk/6502/issues/142) | `$1FC0` to `$1FDF` select 32 banks; the board powers on in bank 15. |
| `cartridge/bank-e0` | E0 | [#131](https://github.com/dbhq-uk/6502/issues/131) | `$1FE0` to `$1FF7` page eight 1K slices into three segments; the fourth is slice 7. |
| `cartridge/bank-e7` | E7 | [#133](https://github.com/dbhq-uk/6502/issues/133) | `$1FE0` to `$1FE6` page 2K ROM banks, `$1FE7` a 1K RAM, `$1FE8` to `$1FEB` 256-byte RAM pages. |
| `cartridge/bank-ef` | EF | [#140](https://github.com/dbhq-uk/6502/issues/140) | `$1FE0` to `$1FEF` select 16 banks; OBSERVED is a power-on diagnostic. |
| `cartridge/bank-f0` | F0 | [#138](https://github.com/dbhq-uk/6502/issues/138) | Each access to `$1FF0` steps to the next of 16 banks, wrapping. |
| `cartridge/bank-fc` | FC | [#150](https://github.com/dbhq-uk/6502/issues/150) | Writes to `$1FF8` and `$1FF9` latch a bank number that a read of `$1FFC` commits. |
| `cartridge/bank-fe` | FE | [#135](https://github.com/dbhq-uk/6502/issues/135) | An access to `$01FE` arms a latch that takes D5 of the next cycle's byte as the bank. |
| `cartridge/bank-jane` | JANE | [#151](https://github.com/dbhq-uk/6502/issues/151) | `$1FF0`, `$1FF1`, `$1FF8` and `$1FF9` select banks 0 to 3. |
| `cartridge/bank-mdm` | MDM | [#147](https://github.com/dbhq-uk/6502/issues/147) | An access at `$0800-$0BFF` selects bank (low byte mod 8); a value with bit 7 set locks the board. |
| `cartridge/bank-sb` | SB | [#143](https://github.com/dbhq-uk/6502/issues/143) | Any access at `$0800-$0FFF` selects bank (low five bits); OBSERVED records the start bank. |
| `cartridge/bank-ua` | UA | [#139](https://github.com/dbhq-uk/6502/issues/139) | `$0220` and `$0240` and their loose-decode mirrors select banks 0 and 1. |
| `cartridge/bank-wd` | WD | [#148](https://github.com/dbhq-uk/6502/issues/148) | A read of `$30` to `$3F` loads one of eight fixed arrangements of 1K banks, a little later; 64 bytes of RAM read low. |
| `cartridge/bank-wf8` | WF8 | [#149](https://github.com/dbhq-uk/6502/issues/149) | A write to `$1FF8` picks the bank from data bit D2. |
| `cartridge/bank-x07` | X07 | [#146](https://github.com/dbhq-uk/6502/issues/146) | `$080D + 16b` selects bank b; from bank 14 or 15 any TIA access picks 14 or 15 by A6. |
| `cartridge/dpc-bank` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | F8 banking leaves the DPC's fetchers and flags as they were. |
| `cartridge/dpc-drawline` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | DF4's draw-line adder and its carry gate the high nibble of the `$1004-$1007` reads. |
| `cartridge/dpc-fetch` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | A fetcher read returns a graphics byte, then counts its 11-bit counter down. |
| `cartridge/dpc-flag-edges` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | Three contested edges of the fetcher flag, and the Pitfall II case. |
| `cartridge/dpc-flag` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | The flag sets when counter-low equals Top and resets at Bottom; masked and flag windows. |
| `cartridge/dpc-music` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | Music mode on DF5 to DF7, clocked by reads, and the mixed amplitude at `$F004`. |
| `cartridge/dpc-probes` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | The register mirror at `$1800`, and register direction decoded from A6 alone. |
| `cartridge/dpc-rng` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | The 8-bit LFSR's law, with the stride between reads measured, not asserted. |
| `cartridge/dpc-shift` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | The shift windows shift with zero fill, not rotate. |
| `cartridge/dpc-swizzle` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | The nibble-swap and bit-reverse windows. |
| `cartridge/dpc-window-reads` | DPC | [#134](https://github.com/dbhq-uk/6502/issues/134) | Reads of `$F000-$F007` move no fetcher. |
| `cartridge/ram-cv` | CV | [#144](https://github.com/dbhq-uk/6502/issues/144) | 1K RAM read at `$F000` and written at `$F400`, under 2K of ROM. |

The ARM boards (#155) have no test in the suite [from the GitHub issues: #155].

The AR images are not raw ROMs: `dasm` builds one 2K RAM bank, and `scripts/build_ar.py` copies it into the three RAM banks and wraps it in the checksummed fastload image that loaders verify, 8448 bytes [from the suite: Makefile; README.md]. All three `ar-*_ntsc.a26` files are 8448 bytes [from the binaries].

## 6. The cartridge binaries and the Superchip filler

Sizes of the `_ntsc.a26` files, `stat -c %s`, 14 October 2026 [from the binaries: crates/systems/vcs/tests/accuracy/roms/cartridge/]:

| Test | Status | Bytes | First 256 bytes of every 4K bank one repeated byte? |
|---|---|---|---|
| `mirror-2k` | Trusted | 2048 | no (one 2K image) |
| `bank-f8` | Trusted | 8192 | no, in either bank |
| `bank-f6` | Trusted | 16384 | no, in any of four |
| `bank-f4` | Trusted | 32768 | no, in any of eight |
| `bank-fa` | Recorded | 12288 | no, in any of three |
| `bank-hotspot-window` | Recorded | 8192 | no, in either bank |
| `power-on-bank` | Recorded | 8192 | no, in either bank |
| `ram-superchip` | Recorded | 8192 | yes: `$E7` in both banks |

The check is `python3 -I tools/probes/atari-2600/filler.py <files>`, which splits each file into 4K banks and tests whether the first 256 bytes of each bank hold one value. The `_pal` builds give the same answer for all eight [from the binaries].

This agrees with the plan's "Fixed before running" item 6: only `ram-superchip` has one repeated byte there in every bank, so the filler rule finds the Superchip on it and on no other [inferring: the table against item 6]. Nothing contradicts item 6.

Worked example: `ram-superchip_ntsc.a26` is 8192 bytes, two banks; bytes `$0000-$00FF` and `$1000-$10FF` of the file are all `$E7`, so the size rule gives F8 and the filler rule adds the Superchip [inferring: the measurement against the plan's task 2 detection rule].

For the plan's review focus 2, the sizes of the not-run images, against the boards the size rule would choose [from the binaries; inferring: the plan's task 2 size table]: 2048 (`ram-cv`, read as plain 2K); 8192 (`bank-03e0`, `0840`, `0fa0`, `3e`, `3ep`, `3f`, `e0`, `fe`, `ua`, `wd`, `wf8`, read as F8); 16384 (`bank-e7`, `jane`, read as F6); 32768 (`bank-fc`, `mdm`, read as F4); 8448 (the three AR images), 10496 (the eleven DPC images), 65536 (`bank-3ep-wide`, `ef`, `f0`, `x07`), 131072 (`bank-df`, `sb`) and 262144 (`bank-3e-wide`, `bf`), all refused.

## 7. What the headers say about their own standing

### 7.1 Untested on hardware

- `cartridge/bank-fa`: the D0 gate (codes `$0C`, `$0D`) is "untested on hardware", asserted on the authority of US patent 4,485,457, and a model "may legitimately fail" those codes.
- `cartridge/bank-hotspot-window`: the byte a hotspot read returns (codes `$0C`, `$0E`) follows from US patents 4,368,515 and 4,432,067 and is "untested on hardware".
- `cartridge/ram-superchip`: the read that destroys a cell (code `$08`) "is untested here, for want of a Superchip board".
- Not run, each untested on hardware in whole or part: `ar-hazards` (code `$06`), `ar-write`, `bank-3e` and `bank-3e-wide`, `bank-3ep` (segments 0 to 2) and `bank-3ep-wide`, `bank-3f`, `bank-fc`, `bank-fe`, `bank-ua` (codes `$06` to `$09`), `bank-wd` (the latch edge), `bank-wf8`, `ram-cv` (the destroying read), and the DPC tests `dpc-drawline`, `dpc-flag` (code `$07`), `dpc-flag-edges` (codes `$02` to `$04`), `dpc-music`, `dpc-probes`, `dpc-rng` (the clocking), `dpc-shift`, `dpc-swizzle`, `dpc-window-reads`.

No header in `cpu/`, `riot/`, `collision/`, `tia-timing/`, `tia-render/` or `harness/` says it is untested on hardware. One trusted header names a document, not a console, as the source of its expectation: `collision/blank-gating` says its expected result "comes from the TIA-1A schematic" [from the suite: roms/collision/blank-gating.asm].

### 7.2 Diagnostics

- `cartridge/power-on-bank`: "a diagnostic, not a pass/fail test"; it always passes and reports the bank in OBSERVED.
- `harness/calibration`: "not a behaviour test"; a target for fitting the capture rig.
- Not run: `bank-bf` and `bank-ef` report the power-on bank in OBSERVED as "Diagnostic only"; `bank-sb` records the start bank; `dpc-rng` reports its measured stride.

### 7.3 Region-dependent

Every self-test header but one ends "region-independent"; `wsync-line-end` adds that a line is 76 cycles in both regions. `collision/merge-delivery-replant` has no such line [from the suite: each header]. The screenshot tests are region-dependent by construction, because each region's reference is its own frame height (section 4). Three headers say how:

- `tia-render/colors`: below the 128 ramp lines the frame is black, 64 lines on NTSC and 100 on PAL.
- `tia-timing/nusiz-draw`: on a 50 Hz field band 7's quad player runs on to the bottom.
- `tia-timing/hmove-stuck-straddle`: its row map is given in PAL rows; NTSC rows are 8 fewer, and row 241 exists only on 50 Hz fields.
- `harness/calibration`: each band is 12 lines on PAL and SECAM and 10 on NTSC.

## 8. The start-up, the stack wipe and the mirror decode

- The README says every ROM begins with `CLEAN_START`, whose stack wipe passes through the TIA mirror at `$0100-$017F` and strobes several TIA registers, RSYNC twice among them; a model without the full mirror decode, or with an RSYNC fault, shifts whole frames, so scrambled first frames point to the mirror decode and RSYNC first [from the suite: README.md].
- One header says the same: `cpu/stack-aliases-tia` notes that every `CLEAN_START` ROM relies on the mirror, because the wipe strobes RSYNC as it passes `$0143` and `$0103` [from the suite: roms/cpu/stack-aliases-tia.asm]. `harness/sanity` says `CLEAN_START` zeroes RAM, the registers and the decimal flag [from the suite: roms/harness/sanity.asm]. No other header mentions the wipe or the mirror decode.
- Not every ROM uses it. Nine sources never call `CLEAN_START`: `cartridge/bank-fe`, and in `collision/` `blanked-reset`, `copy-adjacency`, `merge-delivery`, `merge-delivery-replant`, `merge-delivery-stretch`, `merge-delivery-train`, `resbl-kill` and `reset-phase` (`tools/probes/atari-2600/regs.sh`, the `clean_start` count, 14 October 2026). Their code comments, not their headers, say why: on FE the wipe's write to `$01FE` would switch banks [from the suite: roms/cartridge/bank-fe.asm, the comment at `Main`]; in seven of the collision tests a long clearing loop moves every later strobe onto one phase of the TIA's clock and could hide a phase-dependent fault, "a spurious pass" in `reset-phase`'s words [from the suite: the comment at `Reset` in roms/collision/reset-phase.asm, blanked-reset.asm, copy-adjacency.asm, merge-delivery.asm, merge-delivery-stretch.asm, merge-delivery-train.asm and resbl-kill.asm]. `merge-delivery-replant` starts the same short way with no comment [from the suite: roms/collision/merge-delivery-replant.asm].
- So the outcome of those eight trusted collision tests depends on where the TIA's clocks stand against the CPU when the program starts: the power-on state of the TIA and the order inside a cycle [inferring: the comments above with the plan's task 5 and its power-on choices]. A trusted failure there is read against the power-on alignment as well as the mechanism.

## 9. Counts, dated 14 October 2026

Measured in the clone at `891c079` with `tools/probes/atari-2600/counts.sh`, which counts `roms/<folder>/*.asm` and, as screenshot tests, `roms/<folder>/*_ntsc.png`:

| Folder | Self-test | Screenshot | Total |
|---|---|---|---|
| `cartridge` | 46 | 0 | 46 |
| `collision` | 16 | 0 | 16 |
| `cpu` | 7 | 0 | 7 |
| `harness` | 1 | 1 | 2 |
| `riot` | 11 | 0 | 11 |
| `tia-render` | 10 | 5 | 15 |
| `tia-timing` | 9 | 21 | 30 |
| all | 100 | 27 | 127 |

By the rule in section 1.6, on that day, the trusted self-tests fall to tasks as: task 4, 18 (4 in `cpu/`, 11 in `riot/`, `harness/sanity`, `wsync`, `wsync-line-end`); task 6, 18 (2 in `cpu/`, 3 in `tia-timing/`, 8 in `tia-render/`, 5 in `collision/`); task 7, 17 (4 in `tia-timing/`, 2 in `tia-render/`, 11 in `collision/`); task 9, 1 (`cpu/partial-drive`); and the 4 trusted cartridge tests from task 4 [inferring: the rows of section 5]. These are a reading of the headers on that day, not a claim about the machine.

## 10. Where the headers differ from the plan and the spec

- The plan's task 5 lists `rsync` among the `tia-timing/` self-tests that need only the line. Its header needs a missile, the playfield and the M0-PF collision latch, so it cannot pass before task 6.
- The plan's task 5 adds `wsync` and `wsync-line-end` there. Both need only the WSYNC halt and the RIOT timer, which task 4 builds, so they can pass in task 4.
- So at task 5 the only trusted `tia-timing/` self-tests the line allows are `wsync` and `wsync-line-end`, and `wsync` measures a difference in which any fixed offset cancels. Item 8's choice of the order inside a cycle then rests on `wsync-line-end` alone, and a tie between splits is likely, which the tie rule settles [inferring: the two headers against item 8].
- The plan's task 6 expects "most of `collision/`". Eleven of the sixteen use HMOVE and wait for task 7; task 6 can pass five (`all-pairs`, `latches`, `onset-sweep`, `per-pixel`, `reset-phase`).
- The plan's task 6 expects "the self-tests in `tia-render/`". Two of the ten, `missile-copies` and `missile-reset-lock`, use HMOVE to place their probe and wait for task 7.
- The plan's task 9 adds the `riot/io-*` self-tests. They drive port A as outputs, with idle inputs reading 1, and use no joystick; they need only the RIOT and pass in task 4. The one suite test that needs task 9 is `cpu/partial-drive`.
- `cpu/partial-drive`, `rmw-strobe` and `stack-aliases-tia` are in the folder task 4 runs but need TIA parts from tasks 6 and 9; the plan allows this through its "not yet" list.
- The spec and the plan's task 1 say a TIA read drives D7 and D6 only. The headers are finer: a collision register drives D7 and D6, an input port D7 only, and an address ending in `$E` or `$F` drives nothing [from the suite: roms/cpu/open-bus.asm; roms/cpu/partial-drive.asm].
- The plan's task 2 tests that a hotspot access returns the byte of the bank showing when it was made. `bank-hotspot-window` (codes `$0C`, `$0E`) asserts the byte of the bank it selects. `bank-f8` says only that the switch takes effect on the next bus cycle. With the plan's rule, `bank-hotspot-window` records a fail.
- The plan records a recorded test's outcome as a pass or as CODE, OBSERVED and EXPECTED. `power-on-bank` always passes and reports the bank only in OBSERVED, so its recorded outcome would not say which bank the model woke in.
- The plan's task 7 says 8-bit RGB and palette PNGs are enough for the references. Every `_ntsc.png` and `_pal.png` is 8-bit RGBA (colour type 6), so `PngReader` must read RGBA.
- The README's "every ROM starts with `CLEAN_START`", which the plan's task 4 relies on to read first-frame faults, does not hold for nine sources (section 8).
- The plan's trust table and the spec's give four trusted cartridge tests (`mirror-2k`, `bank-f8`, `bank-f6`, `bank-f4`); this sheet agrees, and all eight modelled-board tests the plan names exist.
- Every test the plan and the spec name exists in the suite (checked by a loop over each backticked test name in both files, 14 October 2026).

## 11. What could not be verified

- No ROM was run: the tasks are a reading of headers and register use, not of outcomes. A test may need a part its header does not name.
- Why `midline-resp`'s references are one row taller (section 4) `[guessing - verify]`.
- Whether the four trusted cartridge tests and the tests of section 8 pass with the power-on bank and TIA phase the model chooses `[guessing - verify]`.
- The binaries were not assembled from the sources here; that is task 1's optional DASM check.

## Sources opened

| Short name | What | Where | Read |
|---|---|---|---|
| the suite | `dbhq-uk/missingno-vcs-tests` at `891c079976a2f68033ca4af2fe2fd85aaf1906c9`: README.md, Makefile, include/result.h, include/region.h, include/frame.asm, include/result_screen.asm (its opening comment and line structure), the header of every `.asm` under `roms/`, the start-up comments of the nine sources in section 8, and the reference PNGs | https://github.com/dbhq-uk/missingno-vcs-tests/tree/891c079976a2f68033ca4af2fe2fd85aaf1906c9 | 14 October 2026 |
| the harness | `crates/systems/vcs/tests/accuracy/common/mod.rs` in `dbhq-uk/missingno` at `a4159f47ce7ef6f9c3d5402a8c58b18940045adc`, its constants and comments only | https://github.com/dbhq-uk/missingno/blob/a4159f47ce7ef6f9c3d5402a8c58b18940045adc/crates/systems/vcs/tests/accuracy/common/mod.rs | 14 October 2026 |
| the binaries | the `.a26` files under `crates/systems/vcs/tests/accuracy/roms/` in the same fork and commit, read for size and bytes | https://github.com/dbhq-uk/missingno/tree/a4159f47ce7ef6f9c3d5402a8c58b18940045adc/crates/systems/vcs/tests/accuracy/roms | 14 October 2026 |
| the plan | `docs/superpowers/plans/2026-10-14-atari-2600.md`, "The suite's results, and what each one gates", "Fixed before running" and tasks 1 to 11 | this repository, the Atari 2600 worktree | 14 October 2026 |
| the spec | `docs/superpowers/specs/2026-10-14-atari-2600-design.md`, "Which suite results are trusted", "Proof" and the bus section | this repository, the Atari 2600 worktree | 14 October 2026 |
| the GitHub issues | `dbhq-uk/6502` issues #107 (title), #131 to #154 (titles and the tests each body names), #155 | https://github.com/dbhq-uk/6502/issues | 14 October 2026 |

The scripts named above are committed in `tools/probes/atari-2600/`: `headers.sh` (prints each header), `regs.sh` (register use per source), `counts.sh` (section 9), `filler.py` (section 6) and `pnginfo.py` (section 4). Each is run in a clone of the fork it reads.
