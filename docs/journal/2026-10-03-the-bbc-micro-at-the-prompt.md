---
title: "The BBC Micro at the prompt, read off the picture"
date: 2026-10-03
summary: "Every screen mode now boots to the BASIC prompt and the tests read it back from the pixels the video chips drew, not from screen memory: typed BASIC runs, the screen scrolls, the palette turns the background blue and a flashing colour flashes. The decode found no fault in the chips, and planted faults show what it can see that the memory-level boot test cannot."
order: 20
---

# 3 October 2026: the BBC Micro at the prompt, read off the picture

Task 10 of the plan. Until today the boot was checked by reading mode 7's
screen memory, which shows that the OS and BASIC wrote the right characters
but says nothing about whether the video chips drew them. Now the tests type
on the keyboard matrix, run BASIC, and read the result back from the
framebuffer, in every mode from 0 to 7. Nothing in `src/` changed: this task
is tests, a screen decoder and a typist, all in the test project.

## Reading the screen off the picture

`tests/Dbhq.Machines.BbcMicro.Tests/ScreenText.cs` cuts the framebuffer into
the mode's character cells and matches each one against a font. For mode 7
that is task 9's `TeletextScreen`, matching cells against the glyph grids typed
from the SAA5050 datasheet. For modes 0 to 6 it is the OS's own font, eight
bytes a character at `$C000 + (code - $20) * 8` in `os.rom`.

**How a cell is read in modes 0 to 6.** The OS draws each font bit as one
pixel of the mode, in the text colour where the bit is set and the text
background where it is not; in modes 1, 2 and 5 it expands each bit into the
two or four bits a pixel of that colour takes. So in every mode a font bit is
one screen pixel, 640 / columns / 8 framebuffer pixels wide and two rows deep,
and a cell holds at most two colours. The decode takes the colour of each font
bit's patch, insists it is one colour, then looks for the glyph whose set bits
are exactly the patches of one colour. A cell of one colour is a space. Lines 8
and 9 of a row in modes 3 and 6 must be black. Nothing about which colour is
the text is assumed, so the same decode reads white on black, white on blue
after a palette change, and red on black.

**Decision: the grid by the mode the OS set, not by the chips' registers.** The
decode needs to know the mode to cut the picture. It reads it from the OS's
variable at `$0355`, and the grid from the fact sheet's table (`video.md`
s3.1). Chosen over working the grid out from the ULA's control register and the
CRTC's R1, R6 and R9, because then a chip that drew the wrong geometry would
have the decode cut its picture the same wrong way and read it happily. With
the OS's mode, a picture the chips drew wrong does not read. A test holds the
grid to the ROM's own tables (`$C3EF` and `$C3E7`) and to the text window the
OS sets after booting each mode (`$0308` to `$030B`, set at `$C9C7-$C9D6`); the
sheet, the ROM and the running OS agree for all eight modes.

**Decision: the decode is told where the cursor is.** The cursor inverts the
bottom lines of one cell and blinks. In white on black, with eight-line rows,
a space under the cursor and an underscore with it off draw exactly the same
pixels, and so do an underscore under the cursor and a space without it: one
picture cannot tell them apart. So the decode takes the cursor's cell from the
OS's text cursor (`$0318` and `$0319`, absolute coordinates: the code at
`$C66A` compares the column with the window's right edge) and its lines from
R10 and R11 in the ROM's register table for the mode, and in that cell allows a
reading with those lines inverted. Where both readings fit, which only a space
and an underscore can, it reads a space. So an underscore in the cursor's cell
reads as a space, in a mode of eight-line rows, in white on black. Chosen over
reading the cursor's place from the CRTC's R14 and R15, which would need the
start address and the wrap to turn into a cell, and over waiting for the blink
and comparing two pictures, which would make reading the screen run the
machine. In modes 3 and 6 the cursor also lights the blank lines 8 and 9, which
no glyph does, so there the pair never arises.

**Decision: in the cursor's cell, each field is read on its own.** The first
machine runs read the cell after the prompt as `?` in two tests. The cursor
blinks field by field, and the framebuffer weaves the two fields, so just
after a blink the even rows had the cursor and the odd rows did not. Everywhere
else the two fields must agree; in the cursor's cell each is read separately,
and the two readings must agree. Mode 7 does the same with its cursor's two
frame rows.

**What is left out.** Character `$7F` is a solid block in the font, which would
match any cell of one colour; the OS never draws it, because VDU 127 is delete,
so it is not a candidate. Characters a program redefines with VDU 23 are not
read. A test shows no two glyphs in the font are the same, or each other's
inverse, so a two-colour cell names one character whichever colour is the
text.

**The decode tested first on pictures drawn by hand.** `ScreenTextTests` draws
every glyph in every mode from 0 to 6, in white on black, yellow on blue and
red on black, and reads them all back; draws each glyph in the cursor's cell
with the cursor on neither field, either or both; and checks a stray pixel or a
third colour reads `?`. My first expectation for the cursor case was wrong: I
had an underscore read as a space only with the cursor shown, and the test
showed it reads as a space with the cursor off too, which is the same pair the
other way round. The expectation was corrected, not the decode.

## Typing

`BbcSession.Type` presses each key on the keyboard matrix with SHIFT where the
character needs it, holds it, lets go and rests, running the machine all the
while. **The table of keys is made from the ROM, not typed:** the unshifted
code of each key is the OS's key table at `$F03B + 16 (row - 1) + column`, and
SHIFT on a code from `$21` to `$3F` flips bit 4 except on `0` (`via.md` s3(b)).
A test checks it comes out as the keys are marked: `"` is SHIFT and 2, `*` is
SHIFT and colon, `=` SHIFT and minus. Capitals come from the letter keys alone,
because CAPS LOCK is on from power on (the keyboard status at `$025A` starts as
`$20`, from the default table at `$D99A`), which the typed BASIC confirms.

**Decision: hold 40 ms, rest 40 ms** (80,000 CPU cycles each). From the ROM: a
new key sets the countdown at `$E7` to 1 (`$F01F-$F026`), and each 100 Hz tick
counts it down and buffers the character at zero (`$EF54-$EF66`, and the code after it), so a key is
typed at the first tick after it is seen. Four ticks of hold leave room for a
tick that lands just before the press, and stay far below the auto-repeat
delay, which is 50 centiseconds from power on (`$D994` = `$32`, copied to
`$0254`). The rest lets the OS's scan see the key go up before the next comes
down, so each key is a new press. These facts are now in `via.md` s3(e), and
the text window and cursor variables in `video.md` s3.3.

## The tests

`BootTests` boots each of modes 0 to 7 once, from a fixture shared by the
read-only tests, and reads the picture: row 0 blank, row 1 `BBC Computer 32K`,
row 2 blank, row 3 `BASIC`, row 4 blank, row 5 the prompt, every row below
blank. **The expected rows come out of the ROMs**, not from what the machine
printed: the banner from OS `$C304` and `$C317`, BASIC's title from `$8009`,
the prompt from BASIC's `LDA #$3E` at `$8B06`, laid out as `bus.md` s4b
derives. Then the machine was run, and every mode agreed. The banner is sixteen
characters, so even the twenty-column modes do not wrap it, and the rows are
the same in every mode. When task 12 fits the 8271, `Acorn DFS` and a blank row
come after row 2; the expected rows are one list (`BootScreen.Rows`) and its
comment names the one-line change.

`BasicTests` types into a fresh machine each time: `PRINT 6*7` shows `42` on
the next row in every mode; the `FOR` loop prints `1`, `2`, `3`; a program line
is stored and `RUN` prints `HELLO`; `MODE 4` clears the screen and the prompt
reads in forty columns of thirty-two rows; `P.` is `PRINT`; `PRINT ?&FE40`
reads the system VIA and BASIC carries on; `VDU 32` to `126` in a loop reads
back as the whole font, in each of modes 0 to 6; forty numbers scroll the
screen in every mode and the last fifteen read back above the prompt;
`VDU 19,0,4,0,0,0` turns every background pixel blue in modes 4 and 1 with the
text still readable; and `VDU 19,1,9,0,0,0` in mode 4 makes the text flash red
and cyan, field by field.

**Mistakes of my own.** I wrote the decoder and the typist before the machine
tests, so those tests could not fail first for the reason the plan meant; they
passed at their first run. The planted faults below are what shows they can
fail. Two tests first read the screen too soon: `Settle`, five fields, is
enough for `PRINT 6*7`, but BASIC takes most of a second over the `VDU` loop in
mode 2 and over forty lines of scrolling in the twenty kilobyte modes, as a
real machine would, and the picture was read with the loop still running. Those
tests now wait two seconds of machine time.

## What the decode found

**No fault in the chips.** The picture decode found none that the memory-level
boot test had missed: the number this task was asked to record is nought. The
chips drew every mode correctly at the first run, and every failure on the way
was my own (above), in the tests or the decoder.

That is a statement about today's chips, so to see what the decode can see, I
planted one-line faults in the chips, one at a time, and ran the memory-level
boot tests (the four that read RAM and registers), the new picture tests, and
the chip-level tests of the ULA, the teletext chip, the keyboard and the system
VIA (`python3 /tmp/t10/mut.py`, a scratch script, not committed). The table is
the run after the review round below, with the colour check in place:

| Planted fault | Memory-level boot | Picture tests | Chip tests |
| --- | --- | --- | --- |
| Palette index from bits 7, 3, 5, 1 | missed | caught | caught |
| Screen wrap of modes 4 and 5 swapped with mode 6 | missed | caught (only by the scroll test) | caught |
| RA3 blanking off in modes 3 and 6 | missed | caught | caught |
| Red and blue swapped | missed | caught (only by the palette and flash tests) | caught |
| The flash bit ignored | missed | caught (only by the flash test) | caught |
| Palette entry from the low nibble | missed | caught | caught |
| One row of the teletext `B` damaged | missed | caught | caught |
| SHIFT never read from the matrix | missed | caught | caught |
| The cursor never inverts | missed | missed | caught |
| The shift register fills with 0s | missed | missed | caught |
| Palette entry number with bit 0 flipped (the review's) | missed | caught (only by the colour check) | caught |

The memory-level boot test missed all eleven, because none of them changes what
the OS writes to memory. The picture tests caught nine. The wrap fault was
missed at first, because nothing scrolled; the scroll test was added for it.
The last fault was missed by every picture test until the review round, below.
The two left are missed by design: the decode tolerates the cursor on or off,
and a shift register filling with 0s shows only in eighty columns on the 1 MHz
clock, which no OS mode sets. The chip tests caught all eleven, which is what
they are for; what the picture tests add is that the chips, the OS and BASIC
agree end to end.

## The review round: the boot picture now checks colour

The review found the gap the last row of the table shows. The decode takes
either colour of a cell as the text, so it reads shapes, not colours, and the
only tests that looked at colour were the palette and flash tests, in modes 4
and 1. The reviewer flipped bit 0 of the palette entry number in the ULA's
palette write. Modes 0, 4, 1 and 5 repeat their palette entries, so nothing
changes there, but mode 2 booted as cyan text on a red background. Its rows
still read correctly, and every boot and BASIC test passed. Mode 7 had the same
blind spot, because `TeletextScreen` asks only whether a pixel is lit.

**Decision: the boot picture checks its colours in every mode.**
`EachModeBootsInWhiteOnBlack` asks two things. First, that the whole picture
holds black and white and nothing else, which is the OS's default in every mode:
logical 0 is black and the text colour white in the power-up palettes `video.md`
s2.3 decodes, and mode 7's rows start white on black. Second, that in every
cell the decode reads, the text is white and the background black. For that the
decode now returns each cell's colours as well as its character
(`ScreenText.ReadCells`). In modes 0 to 6 these are the two colours the match
found, before any cursor. In mode 7 it is the one colour of the cell's lit
pixels, the cursor's lines left out, on black. Asking only for the set of
colours was not enough, because a picture with black text on white would pass
it. I re-planted the review's fault and saw the new test fail in mode 2, then
took the fault out. With the fault out, the rest of the table above is unchanged.

So the boot tests now check colour in every mode, but only white on black:
they would not see a fault that sends another colour to the wrong place. The
palette and flash tests cover two modes beyond that, and the chip tests cover
the palette entry by entry.

Smaller fixes from the review:

- Adding the DFS's row was not the one-line change I had claimed. The literal
  list in `TheBootScreenIsTheRomsText` and the two memory-level boot tests used
  absolute rows. Every boot test now takes its rows from `BootScreen.Rows`
  (`LanguageRow`, `PromptRow`), so the change for task 12 is one line. The
  DFS's text is read from its ROM (`$B3B4`, to its carriage return) rather than
  typed.
- The flash test's wait for the next field had no limit, so a fault that
  stopped the frames would have hung it. It now fails after 100,000 cycles.
- The keyboard tick code was cited one byte late. It is `$EF54-$EF66`, with the
  buffering in the code after it.

## The time it takes

The new tests boot a machine for each start-up mode once for the read-only
boot tests, and one each for the BASIC tests, which type. Measured on 3 October
(`dotnet test tests/Dbhq.Machines.BbcMicro.Tests -c Release --filter
"FullyQualifiedName~ScreenTextTests|FullyQualifiedName~BasicTests|FullyQualifiedName~BootTests"`,
on the shared machine): the longest single test took just under a second, and
the three classes together 25 seconds of wall time with the runner's
parallelism.
