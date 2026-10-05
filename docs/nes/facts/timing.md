# NES timing: clocks, the frame, the dot ratio, reset alignment

Written 5 October 2026 for the NES plan, task 1. Tags and page revisions are in
[`README.md`](README.md). Used by tasks 2 (`Region`), 3 (`NesBus`), 4 and 5 (the
PPU) and 8 (the APU frame counter).

## 1. Clocks

| | NTSC (2A03, 2C02) | PAL (2A07, 2C07) | Source |
|---|---|---|---|
| Master clock | 21.477272 MHz (236.25 MHz / 11 by definition) | 26.601712 MHz (26.6017125 MHz by definition) | [from Cycle reference chart] |
| CPU clock | master / 12 = 1.789773 MHz | master / 16 = 1.662607 MHz | [from Cycle reference chart] |
| Master clocks per PPU dot | 4 | 5 | [from Cycle reference chart] |
| PPU dots per CPU cycle | 3 | 3.2 | [from Cycle reference chart] |
| CPU cycles per scanline | 341 x 4 / 12 = 113 2/3 | 341 x 5 / 16 = 106 9/16 | [from Cycle reference chart] |
| APU frame counter rate | 60 Hz | 50 Hz | [from Cycle reference chart] |
| Frame rate | 60.0988 Hz | 50.0070 Hz | [from Cycle reference chart] |

- PAL divides by 16 and not 15 because the CPU's divider is a Johnson counter,
  which always has an even period [from Cycle reference chart]. So the PAL CPU
  and PPU are not in a whole-number ratio.
- Dendy (UA6527P and UA6538) divides the PAL master clock by 15, so it runs 3
  dots a cycle with PAL's line count and 51 post-render lines [from Cycle
  reference chart]. It is out of scope (spec, "Decisions").

## 2. The frame

| | NTSC | PAL | Source |
|---|---|---|---|
| Dots per line | 341 | 341 | [from PPU rendering; Cycle reference chart] |
| Lines per frame | 262 | 312 | [from PPU rendering; Cycle reference chart: 341 x 312 = 106392 dots] |
| Visible lines | 0 to 239 | 0 to 239 (the 2C07 blanks line 0, see `ppu.md` 10) | [from PPU rendering; Overscan] |
| Post-render line | 240 | 240 | [from Cycle reference chart: one post-render line on both] |
| VBlank flag set | line 241, dot 1 | line 241, dot 1 | [from PPU rendering; NMI]; PAL [inferring from Cycle reference chart: one post-render line, so the flag is set after line 240 as on NTSC; PPU power up state: first PAL VBL "close to 241 * 341/3.2 cycles"] |
| Lines of VBlank after NMI | 20 (241 to 260) | 70 (241 to 310) | [from Cycle reference chart] |
| Pre-render line | 261 | 311 | [from PPU rendering, NTSC]; PAL [inferring: 241 + 70 = 311, and 311 = 312 - 1] |
| VBlank, sprite 0 and overflow flags cleared | pre-render line, dot 1 | pre-render line, dot 1 [guessing - verify] | [from PPU registers; NMI: "scanline 261, dot 1"]. PPU frame timing says the clear time on PAL awaits confirmation |
| Odd frame | one dot shorter when rendering is on | never shorter | [from PPU frame timing; Cycle reference chart: PAL = 341 x 312 exactly] |
| Dots per frame | 89342, or 89341 on an odd frame with rendering on | 106392 | [from Cycle reference chart; PPU frame timing] |
| CPU cycles per frame | 29780.5 (rendering on), 29780 2/3 (off) | 33247.5 | [from Cycle reference chart] |

**The odd frame.** The PPU keeps an even/odd flag, toggled every frame whether
rendering is on or off. With rendering on (background or sprites enabled in
`$2001`), an odd frame jumps from dot 339 of line 261 straight to dot 0 of line
0, so the idle dot at the start of line 0 is skipped and the frame is one dot
short [from PPU frame timing; PPU rendering]. With rendering off there is no skip
[from PPU frame timing]. Turning rendering on late in the pre-render line means
the skip does not happen that frame [from PPU registers, PPUMASK notes].

**When the skip is decided** is tested by `ppu_vbl_nmi` test 10 to one dot
("Clock is skipped too soon/too late, relative to enabling BG") [from the fork:
ppu_vbl_nmi/readme.txt]. The pages say where the skip happens, a jump "from
(339,261)" to (0,0) [from PPU frame timing; PPU rendering], and not when the
rendering bit is read for it. Measured in task 4 (5 October 2026), with the model's position
meaning the dot the PPU runs next: the bit is sampled when dot 338 of the
pre-render line runs, so a `$2001` write made while the PPU is at dot 338 or
earlier counts for this frame and one made at dot 339 does not. With the sample
at dot 339 test 10 fails with code 3, "Clock is skipped too late, relative to
enabling BG" (text `08 07`); at dot 337 with code 2, "too soon" (text `09`);
at 338 it passes, and so do the other nine singles [measured: `dotnet test
tests/Dbhq.Machines.Nes.Tests -c Release --filter
"FullyQualifiedName~BlarggTests"`, with the sample dot changed between runs].
Dot 338 is the model's cutoff in its own alignment of CPU and PPU (section 4,
`bus.md` section 2): the last position at which a `$2001` write still counts.
Set against the wiki's dot 339 it is an effective delay of one dot for the
write, where `ppu.md` section 1 gives 3 to 4 dots for a rendering toggle. The
gap between one dot and 3 to 4 is open: the alignment, the sample point and the
delay all move the cutoff, and test 10 sees only the cutoff [inferring].

### Worked example 1: one frame in dots

- NTSC, rendering off: 262 x 341 = 89342 dots; at 3 dots a cycle that is
  29780 2/3 CPU cycles. Three frames are exactly 89342 CPU cycles.
- NTSC, rendering on: an even frame of 89342 dots and an odd frame of 89341
  dots, 178683 dots a pair, 59561 CPU cycles a pair.
- PAL: 312 x 341 = 106392 dots; at 16 dots in 5 cycles that is 33247.5 cycles,
  so two frames are exactly 66495 CPU cycles.

### Worked example 2: where the VBlank flag is set, counted from power-on

With the PPU at line 0 dot 0 on power-on (section 4), the flag is set on the
dot that takes the PPU to line 241 dot 1: dot number 241 x 341 + 1 = 82182
from the start, counting the first dot as 0. After 27394 CPU cycles exactly
82182 dots have run, numbers 0 to 82181, so on NTSC that dot is the first of CPU
cycle 27395 [inferring; corrected in task 4, which first wrote 82182 / 3 =
27394, the cycles before it]. PPU power up state says the flag is first set "around 27384"
cycles after reset. The pages read do not account for the 10-cycle gap; it is
within the "around", and the same page puts the start of the APU 10 cycles
before the first instruction (section 4) [guessing - verify: task 5's
`ppu_vbl_nmi` run settles it to the dot].

## 3. The dot ratio per CPU cycle

**NTSC.** Three dots in every CPU cycle, always [from Cycle reference chart].

**PAL.** 3.2 dots a cycle, which is 16 dots in every 5 CPU cycles [from Cycle
reference chart]. The pages read give the ratio and not which cycles carry the
fourth dot. It follows from the dividers: in 80 master clocks there are 5 CPU
cycles (one every 16 master clocks) and 16 dots (one every 5). If a CPU cycle
and a dot start on the same master clock, the dots end at master clocks 5, 10,
15, ... 80 and the CPU cycles end at 16, 32, 48, 64, 80, so the five cycles
complete 3, 3, 3, 3 and 4 dots [inferring from the dividers in the Cycle
reference chart]. With another starting phase the 4 moves to another of the
five places, but there is always exactly one 4 in every 5 [inferring]. The
phase a real console powers up in is not on the pages read [guessing - verify].

**For the model** (a choice, not a fact): a whole-number accumulator that adds
16 each CPU cycle and runs one dot for every 5 it holds gives 3, 3, 3, 3, 4 from
a zero start, which is the same pattern [inferring]. The plan records the phase
it picks, and `docs/known-differences.md` says that a real console may be in any
of the five.

### Worked example 3: the PAL accumulator

Start at 0. Each cycle add 16, then run dots while the accumulator is at least 5,
taking 5 each time.

| Cycle | Before | After adding | Dots run | Left |
|---|---|---|---|---|
| 1 | 0 | 16 | 3 | 1 |
| 2 | 1 | 17 | 3 | 2 |
| 3 | 2 | 18 | 3 | 3 |
| 4 | 3 | 19 | 3 | 4 |
| 5 | 4 | 20 | 4 | 0 |

Five cycles, 16 dots, and the accumulator is back at 0. 33247.5 cycles a frame
is 6649.5 of these groups, so the fourth dot falls at a different place in the
line from frame to frame [inferring].

### The order inside one CPU cycle, measured

`bus.md` section 2 left the order of the dots and the access inside a cycle to
the plan. Task 4 (5 October 2026) measured it against the ten `ppu_vbl_nmi`
singles on NTSC. Two things were varied: how many of the cycle's three dots run
before the CPU's access (the rest run after), and when in the cycle the PPU's
NMI output is taken as the line the CPU sees for that cycle: as the cycle
begins (what the previous cycle left), after the dots before the access, after
the access, or at the end of the cycle. The odd-frame sample was at dot 338
(section 2) for every row [measured: `dotnet test tests/Dbhq.Machines.Nes.Tests
-c Release --filter "FullyQualifiedName~BlarggTests"`, sixteen runs]:

| Dots before the access | NMI line taken | Singles passed | Failing singles (status) |
|---|---|---|---|
| 0 | start, after the dots before, after the access, end | 6, 6, 5, 5 | 05, 06, 07, 08 (1); and 04 (11) when taken after the access or at the end |
| 1 | the same four | 6, 6, 5, 5 | the same as with 0 |
| **2** | **start** | **10** | **none** |
| 2 | after the dots before | 6 | 05, 06, 07, 08 (1) |
| 2 | after the access | 5 | 04 (11), 05, 06, 07, 08 (1) |
| 2 | end | 8 | 04 (11), 05 (1) |
| 3 | the same four | 6, 6, 5, 5 | the same as with 0 |

Singles 01, 02, 03 and 09 passed in every row: they time themselves from the
flag, so moving every read by the same amount does not change what they see.
Test 4's code 11 is "Immediate occurence should be after NEXT instruction":
with the line taken after the access, a `$2000` write that enables NMI during
VBlank in an instruction's last cycle reaches the CPU in that same cycle, and
the NMI comes one instruction early. With the line at the end, test 5 printed
`3 3 3 3 3 3 2 2 2 2` for offsets 00 to 09 where the readme gives `4 4 4 3 3 3
3 3 3 2`: the NMI was one CPU cycle early.

**The model:** two dots before the access and the rest after, and the CPU sees
the NMI line as the chips held it when the cycle began, so a change made during
cycle N reaches the CPU in cycle N + 1. This is the core's own convention: its
interrupts were checked against the transistor-level model with "the line
changed at the start of a cycle" (journal, 30 September 2026). The
start-of-cycle NMI line is also what reproduces the suppression window of
`bus.md` section 2: a `$2002` read on the dot the flag is set, or one dot after,
clears it in the cycle that set it, which began with the NMI not raised, so the
CPU never sees it; a read two dots after falls in the next cycle, which began
with the NMI already raised, so it comes [inferring; task 5 pins it in both
regions with `NesBusTests`, reads landing at line 241 dots 2, 3 and 4]. The bus
takes the IRQ line at the same point. Task 8 checked it with the first IRQ
source, the APU's frame counter: `pal_apu_tests` 08.irq_timing, which times the
IRQ handler to the cycle, passes with the line as the cycle began, fails "too
soon" (code 2) with the line taken at the end of the cycle, and fails "too late"
(code 3) with it one cycle later [measured in task 8, `dotnet test
tests/Dbhq.Machines.Nes.Tests -c Release --filter "FullyQualifiedName~BlarggTests.EachPal"`].
The other IRQ ROMs (`apu_test` 3-irq_flag and 6-irq_flag_timing, `pal_apu_tests`
03.irq_flag and 07.irq_flag_timing) poll `$4015` and do not depend on it.

**On PAL** the same rule gives two dots before the access and one after, and in
the cycle that carries the fourth dot, two after. With the accumulator from zero
(worked example 3) that is the fifth cycle of every five, the pattern 3, 3, 3,
3, 4. No PAL test ROM checks this to the dot [from the fork: ppu_vbl_nmi tests
"the NTSC PPU"], so on PAL it is the NTSC rule applied, not a measurement.

## 4. CPU and PPU alignment at reset

- The PPU comes out of power and reset at the top of the picture [from PPU
  power up state].
- On the NTSC NES (front loader, NES-001) the reset button resets both the CPU
  and the PPU, at the same moment [from PPU power up state]. On the Famicom and
  the top loader only the CPU is reset [from PPU power up state].
- `nestest.log`'s first line reads `PPU:  0, 21 CYC:7`, and its line at
  `CYC:114` reads `PPU:  1,  1` [from the fork: other/nestest.log]. So the log
  counts the 7 reset cycles, the PPU column is line then dot, and the PPU was at
  line 0 dot 0 at cycle 0: 7 x 3 = 21, and 114 x 3 = 342 = 341 + 1 [inferring].
  The log is Nintendulator's, not a real console's [from Emulator tests: "a
  known good log (created using Nintendulator)"].
- Within one power-up the PPU's events can fall on any of several alignments
  against the CPU cycle: the CPU cycle can start 0 to 3 master clocks from the
  nearest following PPU dot [from PPU frame timing]. The `ppu_vbl_nmi` tests and
  the timings on PPU frame timing use one alignment, "the one which gives the
  fewest number of special cases, where a read will see a change to a flag if
  and only if it starts at or after the PPU tick where the flag changes" [from
  PPU frame timing]. The readme of `ppu_vbl_nmi` says the NES often starts with
  a different clock divider value, and then some tests fail [from the fork:
  ppu_vbl_nmi/readme.txt].
- After power or reset, writes to `$2000`, `$2001`, `$2005` and `$2006` are
  ignored until the end of the first VBlank: about 29658 CPU cycles on NTSC and
  about 33132 on PAL [from PPU registers; PPU power up state]. The other PPU
  registers work at once [from PPU power up state].
- After power and reset, the APU behaves as if `$4017` was written 10 cycles
  before the first instruction [from PPU power up state].

**For the model** (a choice): power-on puts the PPU at line 0 dot 0, and the bus
runs the PPU from the first reset cycle, so the 7 reset cycles end at line 0 dot
21 on NTSC, matching the log. On PAL the same 7 cycles run 22 dots from a zero
accumulator (3, 3, 3, 3, 4, 3, 3) [inferring from section 3]. Which alignment a
read sees is the sub-cycle order of `bus.md` section 2.

### Worked example 4: the first cycles on NTSC

| CPU cycle (after it) | PPU line | PPU dot |
|---|---|---|
| 0 (power-on) | 0 | 0 |
| 7 (reset done) | 0 | 21 |
| 10 | 0 | 30 |
| 114 | 1 | 1 |

These are the first, second and forty-fourth lines of `nestest.log` [from the
fork: other/nestest.log].

## 5. Open items

1. The PAL pre-render clear at dot 1 of line 311: implied by 70 lines of VBlank,
   not confirmed on a page [guessing - verify].
2. The PAL fourth-dot phase at power-on [guessing - verify].
3. The dot at which the NTSC odd-frame skip samples the rendering bit: settled
   in task 4, dot 338 (section 2).
4. The first VBlank after power on: the model sets it in cycle 27395 counted
   from power on, and PPU power up state says "around 27384" (worked example
   2). The `ppu_vbl_nmi` singles time themselves from the flag and do not
   settle it [guessing - verify].
