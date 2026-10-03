---
title: "The BBC Micro speaks teletext"
date: 2026-10-03
summary: "The default screen mode now draws real teletext: a public-domain glyph table checked dot for dot against the chip's own datasheet, the control codes, double height, hold graphics and flash, and the chip's own count of lines and rows, so the machine boots to its banner in the chip's characters. The entry records the licensing position, every behaviour the sources leave open and what the code does instead, and a bug the oracle caught in a shortcut taken for speed."
order: 19
---

# 3 October 2026: the BBC Micro speaks teletext

Task 9 of the plan gives mode 7 its picture. Until today the video ULA drew it
black. Now `src/Dbhq.Machines.BbcMicro/Teletext.cs` is the Mullard SAA5050,
the teletext character generator on the Model B, `TeletextGlyphs.cs` holds its
character set, and the ULA feeds the chip and draws its cells. The sources are
`video.md` section 4 and the documents it cites, chiefly the Signetics SAA5050/55
datasheet (the sheet's S11), and for the chip's insides the die-shot thread on
stardot (S15). No emulator's code was read.

## The glyphs, and where they come from

The SAA5050 draws 96 characters from a 5 by 9 dot matrix in a ROM on the chip.
The ROM's contents are Mullard's typeface. The fact sheet (s4.5) weighed nine
candidates and recommended one, and the decision follows Dan's rule of 1 October,
that a source is used when its position is documented.

**Decision: Bedstead's table, the data only.** Bedstead is a font made from the
SAA5050's bitmaps by Ben Harris, Simon Tatham, Marnanel Thurman and Neil
Williamson, and its program, `bedstead.c`, holds the bitmaps as a table. Its
header dedicates "this software and the embodied typeface" to the public domain
under CC0, which sits happily in an MIT repository. Only the row data for the
English set was taken, with which code each glyph belongs to and Bedstead's name
for each (kept as a comment), never the C code: the US ASCII entries for codes
`$20` to `$7F`, with the twelve codes where the English set differs. Eleven of
those (pound, a plain tick, three arrows and one half, long dash, one quarter,
double bar, three quarters and divide) come from the table's own list of extra
English characters, and the twelfth, the hash at `$5F`, is the US set's own
number sign entry moved from `$23`. *(Corrected in the review round: this said
all twelve came from the extras list, which has eleven.)* The header goes with the data, at the top of `TeletextGlyphs.cs`
and in a new `NOTICE.md` at the repository's root. Chosen over drawing the
glyphs ourselves from the datasheet, which would carry the same typeface
question with a transcription risk on top, and over every other candidate on
the sheet's list, which are GPL, all rights reserved, a different design, or
only a script that needs a dump of the ROM.

**What stays uncertain.** The copyright in Mullard's typeface still stands, held
by Mullard's corporate successors; Bedstead's header says so. Bedstead's
position is that under section 55 of the UK Copyright, Designs and Patents Act
1988 that copyright is no longer infringed by articles made to produce material
in the typeface, and its web page calls this a belief and says its writer is not
a lawyer. This repository relies on the same position and claims no more.
`NOTICE.md` says that if a rights holder asks, the table comes out.

**The fetch.** `https://bjh21.me.uk/bedstead/bedstead.c` did not answer on 3
October (`curl` gave up after 40 seconds). The same path over plain `http`
answered, and the Internet Archive's copy of the `https` address, fetched the
same day, is byte for byte the same: `bedstead.c` version 3.261, 233,092 bytes,
SHA-256 `432e8fe8b77cada259833170b4007494a2bc8584caeefcf8bebd385f5d4262b1`.
The table is committed, so no test fetches anything.

## The independent check: Figure 11, read off the page

The datasheet's Figure 11 draws all 96 English characters as grids. It is a
picture in the PDF, not text, so I rendered page 16 at 600 dots an inch
(`pdftoppm -f 16 -l 16 -r 600`), found each glyph's frame from the dark rows and
columns of its region, and took the middle of each of the 5 by 9 squares as on
when more than half dark. Then I compared the 96 readings with Bedstead's
English table, and seven disagreed, all in two rows of the figure. Looking at
those seven crops showed why: their frames had been found short, because my crop
started a few pixels below the top of the grid. Widening the crop, a fix to the
reading of the figure and not a copy from Bedstead, made all seven read
correctly, and now all 96 agree, every dot. So Bedstead was used to find my
reading's error, never to supply a grid. The grids are typed out in `tests/Dbhq.Machines.BbcMicro.Tests/Figure11.cs`,
all 96 rather than the brief's sample of fourteen, so damage to any glyph in the
production table fails a test, and the comparison is `TeletextGlyphsTests`. The
same grids are what the tests read the screen back with.

## How the chip works, and the design

The SAA5050 takes one 7-bit code per character cell. Codes `$00` to `$1F` (the
BBC writes them as `$80` to `$9F`) are control codes shown as spaces, and the
rest are characters, or in graphics mode blocks of a 2 by 3 grid. A cell is 6
dots by 10 lines a field, and the chip puts out a dot on each edge of its 6 MHz
clock, so a cell is 12 half-dots across, and with character rounding the 5 by 9
matrix becomes 10 by 18 over the two fields of an interlaced frame.

**Decision: a renderer that takes a line's codes, not one fed a character at a
time.** The brief's interface had `Clock(character, displayEnable,
rasterAddress, oddField)` per character cell; the controller superseded it to
fit task 8's line-at-a-time ULA. The chip gives, for one displayed cell, its
twelve half-dots as a mask and its two colours, packed in an `int` from lookup
tables made once (the rounded rows of every glyph, and every block pattern), so
a line allocates nothing. `RenderLine` draws a whole line of codes for the
tests. The brief's `TeletextGlyphs.Rows(code, graphics, separated)` became
`Rows(code)`: the blocks are not glyphs, the chip decodes them from the code's
bits (Figure 9), and so does `Teletext`.

**What the chip counts itself.** The SAA5050 does not see RA. DEW, wired to
VSYNC, resets its line count; each line with LOSE moves it on; every ten lines a
row. A row holding a double height code makes the next row the lower half of
the pair. The model keeps that count in the chip and steps it at every line end
in every mode, because a program can change mode at any line. DEW is VSYNC's
rise as seen at a line's end, which every VSYNC pulse spans. CRS, the field
signal for rounding, is RA0 inverted on the board (s1.5), so it comes from RA.

**Decision: three characters from fetch to picture, lined up by the R8 skews.**
No source gives the chip's delay in characters. The datasheet gives 2.6 to 2.77
microseconds from its input to its output; the OS's mode 7 R8 skews the cursor
two characters and the ULA draws only cursor segment 1, a third; and the sheet
notes that mode 7 sits one character right of mode 6 with its HSYNC two
characters later, which is three again. So the model takes three
(`VideoUla.TeletextDelayCharacters`), and the byte fetched in character `c`
gets LOSE from DISPTMG after the skew in character `c + 1`, which is what R8's
one-character display skew lines up. A test puts a steady cursor at row 3,
column 5 and finds it on exactly that cell, which only works if all three
figures agree. Chosen over no delay at all, which would have put the cursor
three cells right.

**Decision: draw each cell where its byte was fetched.** Task 8's framebuffer
measures across from the CRTC's character 0, not from HSYNC. Drawing mode 7
where the chip outputs it would push the last three columns off the right of a
640-pixel picture. So a cell is drawn as its output arrives, three characters
back: mode 7 fills the same 640 pixels as the other modes, sixteen pixels to a
cell, a pixel showing the half-dot under its centre.

**Hold graphics is the die-shot description, not the broadcast standard.** The
ETSI standard says a held character is the most recent block on the row. The
SAA5050 is known to differ (S18), and the die-shot description (S15) explains
why: the chip loads a register with each cell's block, a space for anything
else, and under hold shows the register on a control cell instead of loading
it; hold acts from the cell after its code, so the hold code's own cell loads a
space, and a block drawn before the hold code is never held. The model is that
register. It also settles cases ETSI leaves open, such as a capital in graphics
mode, which loads a space.

## What the sources could not establish, and what the code does

Each is a constant or a remark in the code and an item in
`docs/known-differences.md`:

- **`$80` and `$90`** (section 6 item 5): the datasheet's Table 1 marks them
  reserved and "normally displayed as spaces", so they do nothing. So do the box
  codes, SO, SI and ESC. No test asserts them, as the brief asked.
- **When hold is released** (item 11): from the next cell. The die-shot
  simulation's two characters is doubted by its own author.
- **Double height** (item 5): the upper row shows the cell's lines 0 to 4 (its
  blank top line and glyph rows 0 to 3) and the lower row lines 5 to 9, each on
  two lines of the field, rounded against
  the line before on the first and the line after on the second (the datasheet
  says only that it alternates every line). That is the model's own choice, not
  the sheet's guess, which was glyph rows 0 to 4 and 4 to 8. A new field starts
  with an upper row.
- **Separated graphics** (item 5): each block loses its left two half-dots and
  its last line. No source gives positions; Figure 10, drawn to scale, shows the
  blocks flush top and right with gaps of about that size left and below.
- **When the line count moves** (s4.1): at the end of a line that had LOSE.
- **The RA3 gate in mode 7** (s1.5, item 4): off, as task 8 decided; no source
  describes how the board does it. A test shows a descender on the row's last
  two lines.
- **The ULA's display enable does not blank the chip's picture**: inferred,
  because it runs a character ahead of the chip's output and would cut the
  row's last cells.
- **Changing the teletext select in the middle of a line** shows the chip's
  three cells in flight as black, and the chip's attributes start again from the
  row's defaults when it comes back on. The model does not run the chip's
  decoding while the ULA draws its own picture.

## The tests, and whether they can fail

`TeletextGlyphsTests` holds the table to all 96 grids. `TeletextTests` checks,
half-dot by half-dot and every expectation worked by hand from the sources: the
alphanumeric colours, set-after against set-at, flash and steady, new and black
background, conceal and what ends it, the blocks of Figures 9 and 10 contiguous
and separated, blast-through capitals, character rounding of an 'A' in both
fields (worked from the datasheet's description: on the even field, line 2's two
off dots between the diagonals each get a half-dot next to the stroke), a
straight stroke left alone, both halves of double height and the alternating
rounding there, the lower row's background, hold and release with the SAA5050's
late start, the held block's own form, height change and conceal against hold,
flash at 16 fields off and 48 on, and the chip's own line and row count. On the
machine: a row of text decoded cell by cell against the Figure 11 grids,
colours from control codes in screen memory, descenders, the cursor on its cell,
rounding on the right frame rows, the select switched off and on in a line, and
the boot, whose row 1 reads `BBC Computer 32K` and row 3 `BASIC`.

**The oracle has a teletext chip of its own.** `Oracle/ReferenceTeletext.cs` is
written separately and plainly: no lookup tables, each half-dot of each cell
worked out from the glyph rows, the rounding rule and the block geometry. The
reference ULA keeps the chip's pipeline a character at a time. The equivalence
test now compares mode 7 frames, with random screen memory, so every control
code and attribute comes up, and it fails if too few of its frames were in
mode 7.

**Planting mistakes.** Thirty-five one-line mistakes (`python3
/tmp/task9-ttx/mut/run.py`, a scratch script, not committed). The first run of 28
caught 24; two of my mistakes did not compile (a self-assignment and an
`if (false)`, both errors under warnings-as-errors) and were rewritten. One
survived: keeping the chip's pipeline across a stretch the ULA draws itself. The
random programs almost never switch the select off and on inside one line, so a
fixed test now does. Three more were caught only by the oracle (the flash
counter's phase, a capital not clearing the held block, CRS the wrong way), and
each now has a fixed test of its own. Seven more were planted in the fast paths
added for speed (below), and all seven were caught, two only by the oracle (the
line going dark with a cell still in the chip, and a dark line painting
nothing), which is what the oracle is for; one of those first needed rewriting
because it did not compile. With the new tests, all 35 are caught.

**Mistakes of my own.** I wrote `Teletext.cs` before its tests, then wrote the
tests from the sources and ran them; only the five machine-level tests could
fail first, and they did. The first mode 7 rig tests ran two frames, which gave
only one field: the first field after the CRTC's reset displays nothing (s1.4).
A test key of an em dash character would have failed the repository's dash
check, so the long dash is keyed by `_`, which the OS prints as one.

## The speed

The brief's rule: measure before and after, natively and in the browser compiled
ahead of time, in mode 7, alternating the builds so the shared machine's load
falls on both, and stop and say so if the browser median falls under ten times
a 2 MHz machine. Until today mode 7 drew nothing, so it was the cheapest mode;
now it draws. The old code is `76c2b2c`, published to
`publish/native-task9-before` and `publish/aot-task9-before` before any source
changed. The machine was shared with other sessions all afternoon, and the load
average did not always show it: at one point a compiler server, a WebKit process
and two Python jobs were using several of the eight cores while the load read
under 4.

**The first version was too slow.** In the browser, under a load of 13.4 to 14.7
(`MODE=7 ./alternate.sh browser 4 publish/aot-task9-before publish/aot-task9-after`,
16:39 UTC), the old code ran at 7.95 times and the new at 4.48, a ratio of
0.56; natively (16:39, load 10.7 to 15.5) old 25.367, new 19.785 MHz. If that
ratio held on a quiet machine, the 14.5 times of task 8 would become about 8,
under the line. So the drawing was made cheaper before anything else, each
change kept exact by the oracle:

- **A line goes dark.** Once neither the display nor the cursor can come on
  again in a line and no cell with LOSE is left in the chip, every cell left is
  black, so the rest of the line is done at once rather than a character at a
  time: the 24 characters a line that mode 7 does not display, and every line
  outside the display. Natively (16:57, load 9.1 to 8.5) old 56.773, new 42.437
  MHz; in the browser (16:58, load 8.5 to 9.8) old 14.77, new 10.56 times.
- **A straight path** for the line every row of the OS's mode 7 has but the
  cursor's: the run starts the line, DISPTMG skewed one character, no cursor in
  play. Its cells are simply 0 to R1 - 1, all shown, so the per-character
  history work goes. **And each cell written four pixels at a time** from a table
  of the sixteen patterns of four for the colours in use. Natively (17:03, load
  3.5 to 3.2) old 55.835, new 45.063 MHz; in the browser (17:03, load 3.2 to 5.7)
  old 12.20, new 10.37 times.
- **What was left.** A build with the teletext drawing switched off ran natively
  at the old code's speed (best runs: that build 64.456 MHz, the old code 64.263, 17:06, load
  14.8 to 15.5), so what remained was the drawing itself, not the per-line machinery.
  (A first try at that build read an environment variable in every run, which
  cost more than the drawing; it was thrown away.) The last change looks up the
  row's black flag once a run instead of dividing for it every cell, skips black
  cells over a black row in the straight path, and gives `Teletext.Cell` a small
  inlined path for a character in alphanumerics mode. Natively (17:09, load 2.8
  to 3.2) old 54.288, new 52.770 MHz; in the browser (17:09, load 3.2 to 4.1)
  old 12.79, new 10.78 times.

**The oracle caught a bug in the shortcut.** The dark line's first version left
the rest of the line for the line's end to paint black. The equivalence test
failed at cycle 2,290,120: a cyan cell where the oracle had black. A trace of the
line showed teletext at 2 MHz for 17 characters, then a control write keeping
teletext on but switching to 1 MHz (I first misread that byte, `$CA`, as
switching teletext off). At 1 MHz a cell is drawn 48 pixels back from its
character, so the cells after the switch land on cells the 2 MHz stretch had
drawn, and the full path paints them black where the dark line painted nothing.
A dark run now paints, at once, the black its cells would cover. A guard I tried
first, against drawing past the next cell, did not fix it, which is how I found I
had the cause wrong.

**A false alarm in mode 1.** Two browser sets (17:10 and 17:11 UTC, load 3.6 to 1)
put the new code at 5.64 and 5.56 times in mode 1 against 7.37 and 7.58 for the
old, both far below task 8's figures, though modes 0 to 6 gained only a few
operations a line. A build with the chip's line bookkeeping taken out of modes 0
to 6 ran no faster (17:16, load 15.2 to 12.0: old 7.05, new 6.78, experiment 6.80
times), and the final quiet set below shows no difference at all. The low
figures were the other sessions.

### The final figures

The final code (`publish/aot-task9-after4`, the code in this commit) against the
old, browser compiled ahead of time, five launches of five timed runs a build,
the same headless Chrome as task 8, in the quietest window of the afternoon (the
CPUs 81 per cent idle when it began):

```
MODE=7 ./alternate.sh browser 5 publish/aot-task9-before publish/aot-task9-after4
MODE=7 ./alternate.sh browser 5 publish/aot-task9-after4 publish/aot-task9-before
MODE=1 ./alternate.sh browser 5 publish/aot-task9-before publish/aot-task9-after4
```

| Mode | Order | UTC | Load (1 min), before to after | Old code | Final code |
| --- | --- | --- | --- | --- | --- |
| 7 | old first | 17:58 | 5.77 to 5.81 | 28.653 (14.33 times) | 24.783 (12.39 times) |
| 7 | new first | 17:59 | 5.81 to 5.23 | 29.155 (14.58 times) | 27.739 (13.87 times) |
| 1 | old first | 17:59 | 5.23 to 5.74 | 23.838 (11.92 times) | 24.390 (12.20 times) |

So mode 7 in the browser runs at 12.4 and 13.9 times a 2 MHz machine, above the
line of ten, at 0.86 and 0.95 of the old code's speed in the two orders; mode 1
is unchanged. Boots in mode 7 in the same sets: a median of old 352.4, new 471.1 ms,
then old 383.1, new 385.0 ms. Earlier sets under heavier load (17:37 to 17:45,
load up to 23.7) put both builds near half speed, the new code at 0.88 to 0.99 of
the old by median.

Natively, five launches of twelve timed runs a build, the last ten kept: the
quietest set is the 17:09 one above (mode 7: old 54.288, new 52.770 MHz, best
runs old 67.712, new 60.825; mode 1 at 17:10, load 3.65 to 3.40: old 42.061,
new 45.887). A final set at 17:46 to 17:47 ran at a load of 7.3 to 13.7, and its
medians are within 3 per cent either way in both modes (`MODE=7` and `MODE=1
./alternate.sh native 5`, both orders); its best runs in mode 7 were 62.837 and
67.934 MHz for the old code and 58.327 and 58.240 for the new.

**The machine still does the same thing.** `--fingerprint` (every instruction,
and every 100,000 cycles all of RAM and every VIA register, through boot, a
typed BASIC program and BREAK) prints the same 54 lines from the old build and
the final one: the teletext chip changes nothing the CPU can see.

## The review round

The review found the provenance, the glyphs (the reviewer read all 96 Figure 11
grids again, independently), the control codes, the ULA wiring and the
fingerprint sound, and asked for six things.

**Every choice named in the code.** `Teletext.cs` said that every place the
sources stop is a constant or a remark in the class, and four were not: release
acting from the next cell (s6 item 11), `$80` and `$90` doing nothing, the box
codes, SO, SI and ESC doing nothing (items 5 and 6), and DEW returning the chip
to an upper row. Each now has a comment naming the assumption and its item, the
no-op codes as explicit cases, and while making the sentence true I marked three
more the same way: the double height mapping, a capital clearing the hold
register, and the edge on which the line count moves.

**Wording.** Three corrections. The provenance texts said all twelve
English-set codes came from Bedstead's list of extra English characters; that
list has eleven, and the hash at `$5F` is the US set's own number sign entry,
moved from `$23`. They also said only the row data was taken, when Bedstead's
glyph names came too, as comments. And `known-differences.md` said double height
maps rows "as the sheet guessed": the sheet guessed glyph rows 0 to 4 and 4 to 8,
and the model splits the ten-line cell into two halves of five, its own choice.
The speed pairs in this entry now say which figure is the old code's.

**One address formula.** The mode 7 address of `video.md` s2.5 had been copied
into the two drawing loops for speed. It is now one small inlined method that
`ScreenAddress` and both loops call. On the boot screen the build with it ran no
slower than the committed one (`MODE=7 ./alternate.sh native 5`, 19:29 UTC, load
5.0 to 5.7: committed 59.249 MHz, this round 63.814).

**The worst case, measured.** The boot screen is mostly blank rows, the easy
case. The reviewer measured a page where every cell is drawn, natively, and
found it ran at about two thirds of the boot screen's speed, which in the browser
would be about 8 to 9 times a 2 MHz machine: under the brief's line of ten. The
bench now has that page: `screen=dense` fills mode 7's 1,000 bytes of screen
memory with random bytes from a fixed seed, so every control code comes up, and
`screen=text` with random printable characters (the bench README says how).
The old code was published again from an exported copy of `76c2b2c` with only
the bench's two `Program.cs` files changed, so both builds time the same page;
`alternate.sh` stops if a build does not confirm it filled it.

Two cheap savings in the per-cell path were taken. A cell's pixels come from a
table of the sixteen patterns of four pixels for its two colours, which was made
again at every change of colour, and a random page changes colour all the time;
since the cursor's inversion only ever maps one physical colour to another, all
64 colour pairs are now tabled once when the program starts. And a cell no
longer marks its row as drawn once the line has drawn anything. Both are held by
the oracle and the planted mistakes (below). Natively on the dense page
(`SCREEN=dense MODE=7 ./alternate.sh native 5`, 19:28 UTC, load 7.4 to 6.2,
CPUs 62 per cent idle): the old code 66.447 MHz, this round's starting code
41.785, with the savings 46.739. In the browser they made little difference
(`SCREEN=dense ... browser 5 publish/aot-t9r-after publish/aot-t9r-after2`,
19:27, load 5.1 to 4.8: without 11.06 times, with 11.11; best runs 23.753 and
25.478 MHz).

The worst case in the browser, compiled ahead of time, five launches of five
timed runs a build, the old code first and then the new:

```
SCREEN=dense MODE=7 ./alternate.sh browser 5 publish/aot-t9r-before publish/aot-t9r-after2
SCREEN=dense MODE=7 ./alternate.sh browser 5 publish/aot-t9r-after2 publish/aot-t9r-before
SCREEN=text MODE=7 ./alternate.sh browser 5 publish/aot-t9r-before publish/aot-t9r-after2
```

| Page | Order | UTC | Load (1 min), before to after | Old code | New code |
| --- | --- | --- | --- | --- | --- |
| dense | old first | 19:26 | 4.80 to 5.60 | 29.542 (14.77 times) | 23.725 (11.86 times) |
| dense | new first | 19:26 | 5.60 to 5.37 | 30.349 (15.17 times) | 21.763 (10.88 times) |
| text | old first | 19:25 | 2.95 to 5.13 | 30.257 (15.13 times) | 24.038 (12.02 times) |

The old code draws nothing in mode 7, so its figure is the same for any page.
**The worst case, a dense random page, runs at 10.9 and 11.9 times a 2 MHz
machine in the browser: above the line of ten, but not by much,** at 0.72 and
0.80 of the old code. Two earlier dense sets (19:23 and 19:24, the CPUs half
busy with other work) put the new code at 9.83 and 9.13 times, under the line,
with the old code at 10.79 and 9.82 in the same sets; a busy machine takes a
dense page under ten. It is far above real time in every set. Natively on the
same pages: dense 46.739 MHz against the old code's 66.447, text 48.989 against
59.608 (19:28 and 19:29, load 7.4 to 5.7).

**What I would try next** if the worst case has to rise: keep, for each
framebuffer row, the codes and line state it was last drawn from, and skip a
line whose inputs have not changed, since most of a teletext page is still
from one field to the next; and give graphics characters an inlined fast path
like the one alphanumerics have. Neither was cheap enough for this round: the
first needs every writer of a row to invalidate it.

**Planted mistakes again.** All 35 were run against this round's code, with the
patterns that the speed work had moved rewritten (the pipeline's LOSE test is
now a bit in a register, so "LOSE from the same character" became "LOSE a
character late"), and three new ones in the new code: rows never marked drawn,
the colour pair taken the wrong way round, and the teletext address off by a
bit. All 38 are caught. The fingerprint is still the same 54 lines.

## What this does not say

One shared virtual machine, loaded by other sessions all afternoon, and one
browser. The workload is the OS idling at the prompt over its boot screen,
which is mostly blank rows; the worst case, a dense random page, is measured in
the review round above. The figures describe the code as committed that day and
nothing later.
