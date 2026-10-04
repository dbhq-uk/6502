using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The SAA5050 against <c>video.md</c> section 4 and the datasheet: the control codes, the
/// graphics, character rounding, double height, hold graphics, conceal and flash, then mode 7 on
/// the machine. Every expected half-dot is worked by hand from the sources and written here.
/// </summary>
/// <remarks>
/// A cell is written as twelve letters, one per half-dot, left to right: K black, R red, G green,
/// Y yellow, B blue, M magenta, C cyan, W white (colours 0 to 7). An alphanumeric's dot d is
/// half-dots 2 + 2d and 3 + 2d, after the cell's two blank half-dots on the left (s4.1). Glyph
/// rows come from <see cref="Figure11"/>; the cell's line n shows the glyph's row n - 1, line 0
/// being the cell's blank top line.
/// </remarks>
public class TeletextTests
{
    private const string Blank = "KKKKKKKKKKKK";
    private const uint OpaqueBlack = 0xFF000000;

    // Line 5 of an 'A' is its fifth row, "#####": every dot on, so rounding cannot add to it.
    private const int FullLineOfA = 5;

    [Theory]
    [InlineData(0x81, 'R')]
    [InlineData(0x82, 'G')]
    [InlineData(0x83, 'Y')]
    [InlineData(0x84, 'B')]
    [InlineData(0x85, 'M')]
    [InlineData(0x86, 'C')]
    [InlineData(0x87, 'W')]
    public void AnAlphanumericColourCodeColoursTheTextAfterIt(byte code, char colour)
    {
        // s4.2: $81 to $87 select alphanumerics in red, green, yellow, blue, magenta, cyan, white,
        // set-after; the code's own cell is a space in the background.
        string[] cells = Render([code, (byte)'A'], FullLineOfA);
        Assert.Equal(Blank, cells[0]);
        Assert.Equal("KK" + new string(colour, 10), cells[1]);
    }

    [Fact]
    public void TheRowStartsInWhiteAlphanumericsOnBlack()
    {
        // s4.2: the row defaults include alpha white and a black background.
        Assert.Equal("KKWWWWWWWWWW", Render([(byte)'A'], FullLineOfA)[0]);
    }

    [Fact]
    public void ACodeWithBitSevenClearIsTheSameCode()
    {
        // s4.1: bit 7 of the byte is ignored.
        Assert.Equal(Render([0x81, (byte)'A'], FullLineOfA), Render([0x01, (byte)'A'], FullLineOfA));
    }

    [Fact]
    public void AColourCodeActsFromTheNextCellNotItsOwn()
    {
        // Set-after: in "$81 $82 A" the A is green, and the $82 cell itself is a space.
        string[] cells = Render([0x81, 0x82, (byte)'A'], FullLineOfA);
        Assert.Equal(Blank, cells[1]);
        Assert.Equal("KKGGGGGGGGGG", cells[2]);
    }

    [Fact]
    public void FlashHidesTheForegroundWhenTheFlashIsOffAndSteadyStopsIt()
    {
        // s4.2: $88 flash is set-after, $89 steady set-at.
        Assert.Equal(Blank, Render([0x88, (byte)'A'], FullLineOfA, flashOn: false)[1]);
        Assert.Equal("KKWWWWWWWWWW", Render([0x88, (byte)'A'], FullLineOfA, flashOn: true)[1]);
        Assert.Equal("KKWWWWWWWWWW", Render([0x88, 0x89, (byte)'A'], FullLineOfA, flashOn: false)[2]);
        // With a background, a flashing character's foreground shows as the background.
        Assert.Equal("RRRRRRRRRRRR", Render([0x81, 0x9D, 0x87, 0x88, (byte)'A'], FullLineOfA, flashOn: false)[4]);
    }

    [Fact]
    public void NewBackgroundTakesTheForegroundOnItsOwnCellAndBlackBackgroundEndsIt()
    {
        // s4.2: $9D new background (set-at) makes the background the current foreground; $9C black
        // background (set-at) makes it black again.
        string[] cells = Render([0x81, 0x9D, 0x82, (byte)'A', 0x9C, (byte)'A'], FullLineOfA);
        Assert.Equal("RRRRRRRRRRRR", cells[1]);
        Assert.Equal("RRRRRRRRRRRR", cells[2]);
        Assert.Equal("RRGGGGGGGGGG", cells[3]);
        Assert.Equal(Blank, cells[4]);
        Assert.Equal("KKGGGGGGGGGG", cells[5]);
    }

    [Fact]
    public void ConcealHidesTextUntilAColourCode()
    {
        // s4.2: $98 conceal is set-at, and a colour code cancels it (set-after, with the colour).
        string[] cells = Render([0x98, (byte)'A', 0x82, (byte)'A'], FullLineOfA);
        Assert.Equal(Blank, cells[1]);
        Assert.Equal("KKGGGGGGGGGG", cells[3]);
        // A graphics colour code cancels it too.
        Assert.Equal("GGGGGGGGGGGG", Render([0x98, 0x92, 0xFF], 0)[2]);
    }

    [Theory]
    [InlineData(0, "RRRRRRRRRRRR")]
    [InlineData(2, "RRRRRRRRRRRR")]
    [InlineData(3, "RRRRRRRRRRRR")]
    [InlineData(9, "RRRRRRRRRRRR")]
    public void TheSolidBlockFillsTheCellInContiguousGraphics(int line, string expected)
    {
        // Figure 9: $7F in graphics lights all six blocks; contiguous blocks fill the cell.
        Assert.Equal(expected, Render([0x91, 0x7F], line)[1]);
    }

    [Theory]
    // Figure 10's "contiguous graphics character 0110111" (b1 to b7, so code $76): top left off,
    // top right on, middle left on, middle right off, bottom left and bottom right on. The rows of
    // blocks are lines 0 to 2, 3 to 6 and 7 to 9 (s4.2, "3, 4, 3"); a block is six half-dots.
    [InlineData(0, "KKKKKKRRRRRR")]
    [InlineData(2, "KKKKKKRRRRRR")]
    [InlineData(3, "RRRRRRKKKKKK")]
    [InlineData(6, "RRRRRRKKKKKK")]
    [InlineData(7, "RRRRRRRRRRRR")]
    [InlineData(9, "RRRRRRRRRRRR")]
    public void BlockGraphicsFollowFigureNine(int line, string expected)
    {
        Assert.Equal(expected, Render([0x91, 0x76], line)[1]);
    }

    [Theory]
    // Figure 10, separated: each block loses its left two half-dots and its last line.
    [InlineData(0, "KKRRRRKKRRRR")]
    [InlineData(1, "KKRRRRKKRRRR")]
    [InlineData(2, Blank)]
    [InlineData(3, "KKRRRRKKRRRR")]
    [InlineData(5, "KKRRRRKKRRRR")]
    [InlineData(6, Blank)]
    [InlineData(7, "KKRRRRKKRRRR")]
    [InlineData(8, "KKRRRRKKRRRR")]
    [InlineData(9, Blank)]
    public void SeparatedGraphicsLeaveAGapLeftAndBelowEachBlock(int line, string expected)
    {
        Assert.Equal(expected, Render([0x91, 0x9A, 0x7F], line)[2]);
    }

    [Fact]
    public void ContiguousGraphicsComeBackWithNinetyNine()
    {
        Assert.Equal("RRRRRRRRRRRR", Render([0x91, 0x9A, 0x99, 0x7F], 0)[3]);
    }

    [Fact]
    public void CapitalsBlastThroughInGraphicsAndLowerCaseCodesAreBlocks()
    {
        // s4.2: codes $40 to $5F stay alphanumeric in graphics mode; $61 is a block: bit 0 top
        // left, bit 6 bottom right.
        Assert.Equal("KKRRRRRRRRRR", Render([0x91, (byte)'A'], FullLineOfA)[1]);
        Assert.Equal("RRRRRRKKKKKK", Render([0x91, 0x61], 0)[1]);
        Assert.Equal("KKKKKKRRRRRR", Render([0x91, 0x61], 9)[1]);
    }

    [Fact]
    public void RoundingFillsTheDiagonalsOfAnAFromTheRowBeforeOnTheEvenFieldAndTheRowAfterOnTheOdd()
    {
        // The datasheet, "Character rounding": the row before on the even field, the row after on
        // the odd. Line 2 of an 'A' is ".#.#." (dots 1 and 3, half-dots 4, 5 and 8, 9).
        // Even field, against line 1, "..#..": dots 1 and 2 cross diagonally, so the off dot 2 gets
        // its left half (6), and dots 2 and 3 likewise its right half (7).
        Assert.Equal("KKKKWWWWWWKK", Render([(byte)'A'], 2, odd: false)[0]);
        // Odd field, against line 3, "#...#": dots 0 and 1 cross, so dot 0 gets its right half (3);
        // dots 3 and 4 cross, so dot 4 gets its left half (10).
        Assert.Equal("KKKWWWKKWWWK", Render([(byte)'A'], 2, odd: true)[0]);
    }

    [Fact]
    public void AStraightStrokeIsNotRounded()
    {
        // 'I' (Figure 11) is ".###." over "..#..": no diagonal, so line 1 is the same in both fields.
        Assert.Equal("KKKKWWWWWWKK", Render([(byte)'I'], 1, odd: false)[0]);
        Assert.Equal("KKKKWWWWWWKK", Render([(byte)'I'], 1, odd: true)[0]);
    }

    [Fact]
    public void DoubleHeightShowsTheUpperHalfOnItsRowAndTheLowerHalfOnTheNext()
    {
        // s4.3: double height draws each row of the cell on two lines of the field, the upper half
        // (cell lines 0 to 4) on the row with the code and the lower half (5 to 9) on the row below,
        // which repeats it. $8D is set-after.
        // Upper row, line 8: cell line 4 of 'A', "#...#"; line 9 likewise.
        Assert.Equal("KKWWKKKKKKWW", Render([0x8D, (byte)'A'], 8)[1]);
        Assert.Equal("KKWWKKKKKKWW", Render([0x8D, (byte)'A'], 9)[1]);
        // Upper row, lines 0 and 1: the cell's blank top line.
        Assert.Equal(Blank, Render([0x8D, (byte)'A'], 1)[1]);
        // Lower row, lines 0 and 1: cell line 5, "#####".
        Assert.Equal("KKWWWWWWWWWW", Render([0x8D, (byte)'A'], 0, lower: true)[1]);
        Assert.Equal("KKWWWWWWWWWW", Render([0x8D, (byte)'A'], 1, lower: true)[1]);
        // The same line in normal height is cell line 0, blank: the double height is what shows it.
        Assert.Equal(Blank, Render([(byte)'A'], 0)[0]);
    }

    [Fact]
    public void OnTheLowerRowNormalHeightCellsAreBackgroundOnly()
    {
        // s4.3: non-double-height cells on the lower row are shown as background.
        Assert.Equal(Blank, Render([(byte)'A'], FullLineOfA, lower: true)[0]);
        Assert.Equal("RRRRRRRRRRRR", Render([0x81, 0x9D, (byte)'A'], FullLineOfA, lower: true)[2]);
        // $8C normal height is set-at: after it the cells are normal again.
        Assert.Equal(Blank, Render([0x8D, 0x8C, (byte)'A'], 0, lower: true)[2]);
    }

    [Fact]
    public void DoubleHeightRoundsUpThenDownOnAlternateLines()
    {
        // The datasheet: in double height the reference row alternates up and down every line.
        // Upper row, line 4 is the first of the two lines for cell line 2 of 'A' (".#.#."), so it
        // rounds against the line before, "..#..", in either field; line 5 against the line after.
        Assert.Equal("KKKKWWWWWWKK", Render([0x8D, (byte)'A'], 4, odd: false)[1]);
        Assert.Equal("KKKKWWWWWWKK", Render([0x8D, (byte)'A'], 4, odd: true)[1]);
        Assert.Equal("KKKWWWKKWWWK", Render([0x8D, (byte)'A'], 5, odd: false)[1]);
    }

    [Fact]
    public void HoldGraphicsShowsTheBlockHeldOnControlCellsFromTheCellAfterTheHoldCode()
    {
        // s4.3 and the die-shot description: hold acts from the cell after $9E; on a control cell in
        // graphics mode it shows the last block, in the colour then in force (the colour code is
        // set-after); release ($9F) is set-after too.
        string[] cells = Render([0x91, 0x9E, 0x7F, 0x92, 0x9F, 0x94], 0);
        Assert.Equal(Blank, cells[1]);
        Assert.Equal("RRRRRRRRRRRR", cells[2]);
        Assert.Equal("RRRRRRRRRRRR", cells[3]);
        Assert.Equal("GGGGGGGGGGGG", cells[4]);
        Assert.Equal(Blank, cells[5]);
    }

    [Fact]
    public void TheSaa5050HoldsNoBlockDrawnBeforeTheHoldCode()
    {
        // s4.3, SAA5050 specific: a block is only held after the hold code has been seen.
        Assert.Equal(Blank, Render([0x91, 0x7F, 0x9E, 0x92], 0)[3]);
    }

    [Fact]
    public void AHeldBlockKeepsItsOwnContiguousOrSeparatedForm()
    {
        // s4.3: the held character is shown in its own form, not the one in force.
        Assert.Equal("RRRRRRRRRRRR", Render([0x91, 0x9E, 0x7F, 0x9A], 0)[3]);
    }

    [Fact]
    public void ACapitalInGraphicsModeLoadsASpaceIntoTheHoldRegister()
    {
        // Inferred from the die-shot description (s4.3, S15): a capital blasting through is not a
        // block, so the register loads a space and a control cell after it holds nothing.
        Assert.Equal(Blank, Render([0x91, 0x9E, 0x7F, (byte)'A', 0x92], 0)[4]);
    }

    [Fact]
    public void AHeightChangeAndConcealBothStopTheHeldBlock()
    {
        // s4.3: the held character is reset on a height change; conceal takes priority over hold.
        string[] height = Render([0x91, 0x9E, 0x7F, 0x8D, 0x92], 0);
        Assert.Equal(Blank, height[3]);
        Assert.Equal(Blank, height[4]);
        Assert.Equal(Blank, Render([0x91, 0x9E, 0x7F, 0x98], 0)[3]);
    }

    [Fact]
    public void FlashIsOffForSixteenFieldsAndOnForFortyEight()
    {
        // s4.3: the die-shot figure, a 6-bit counter stepped by DEW, off for counts 0 to 15.
        var chip = new Teletext();
        Assert.False(chip.FlashOn);
        int on = 0, off = 0, changes = 0, firstOn = -1;
        bool last = chip.FlashOn;
        for (int field = 0; field < 64; field++)
        {
            chip.FieldStart();
            if (chip.FlashOn)
            {
                on++;
                firstOn = firstOn < 0 ? field + 1 : firstOn;
            }
            else
            {
                off++;
            }
            changes += chip.FlashOn != last ? 1 : 0;
            last = chip.FlashOn;
        }

        Assert.Equal(16, firstOn);
        Assert.Equal(48, on);
        Assert.Equal(16, off);
        Assert.Equal(2, changes);
        Assert.Equal(0, chip.FlashCount);
    }

    [Fact]
    public void TheChipCountsTenDisplayedLinesARowAndADoubleHeightRowMakesTheNextALowerHalf()
    {
        // s4.1 and s4.3: DEW resets the count; lines with LOSE move it; a row with a double height
        // code makes the next row a lower half, and that one does not make the row after it one.
        var chip = new Teletext();
        chip.FieldStart();
        Assert.Equal(0, chip.LineInRow);

        RunRow(chip, [0x8D, (byte)'A']);
        Assert.True(chip.LowerRow);
        RunRow(chip, [0x8D, (byte)'A']);
        Assert.False(chip.LowerRow);
        RunRow(chip, [(byte)'A']);
        Assert.False(chip.LowerRow);

        chip.LineEnd(hadDisplay: false);
        Assert.Equal(0, chip.LineInRow);
        chip.LineEnd(hadDisplay: true);
        Assert.Equal(1, chip.LineInRow);
        chip.FieldStart();
        Assert.Equal(0, chip.LineInRow);
    }

    [Fact]
    public void Mode7DrawsTheTextOfScreenMemoryAsTeletextCells()
    {
        // The OS's mode 7 registers on a bus with no CPU, screen memory at $7C00 filled with spaces
        // and "BBC Computer 32K" on text row 1. Each cell is 16 framebuffer pixels across (one
        // microsecond) and 20 rows down (10 lines a field, woven), and decodes to its character.
        var rig = new Mode7Rig();
        rig.Text(1, 0, "BBC Computer 32K");
        rig.RunFrames(3);

        Assert.Equal("BBC Computer 32K" + new string(' ', 24), TeletextScreen.ReadRow(rig.Bus.Screen, 1));
        Assert.Equal(new string(' ', 40), TeletextScreen.ReadRow(rig.Bus.Screen, 0));
    }

    [Fact]
    public void Mode7ShowsTheDescendersOnTheLastTwoLinesOfTheRow()
    {
        // The RA3 gate is off in teletext (s1.5): a 'p' has its descender on cell lines 8 and 9,
        // RA 16 to 19, which would be blanked in modes 3 and 6.
        var rig = new Mode7Rig();
        rig.Text(2, 4, "p");
        rig.RunFrames(3);

        int y = (20 * 2) + (2 * 9);
        Assert.Equal(OpaqueBlack, rig.Bus.Screen.Pixel(16 * 4, y));
        Assert.NotEqual(OpaqueBlack, rig.Bus.Screen.Pixel((16 * 4) + 3, y));
        Assert.NotEqual(OpaqueBlack, rig.Bus.Screen.Pixel((16 * 4) + 3, y + 1));
    }

    [Fact]
    public void Mode7RoundsFromTheRowBeforeOnTheEvenFieldAndTheRowAfterOnTheOdd()
    {
        // CRS is RA0 inverted (s1.5), and the even field's lines are the even frame rows. Line 2 of
        // an 'A' is "KKKKWWWWWWKK" on the even field and "KKKWWWKKWWWK" on the odd (worked in
        // RoundingFillsTheDiagonalsOfAnA...). Half-dot 3 is pixel 4 of the cell and half-dot 6
        // pixel 8: a pixel shows the half-dot under its centre.
        var rig = new Mode7Rig();
        rig.Text(1, 0, "A");
        rig.RunFrames(3);

        int even = 20 + (2 * 2);
        Assert.Equal(OpaqueBlack, rig.Bus.Screen.Pixel(4, even));
        Assert.Equal(0xFFFFFFFFu, rig.Bus.Screen.Pixel(8, even));
        Assert.Equal(0xFFFFFFFFu, rig.Bus.Screen.Pixel(4, even + 1));
        Assert.Equal(OpaqueBlack, rig.Bus.Screen.Pixel(8, even + 1));
    }

    [Fact]
    public void TurningTheTeletextSelectOffAndOnInALineShowsTheChipsThreeCellsAsBlack()
    {
        // The model's choice (known-differences): the three cells in the chip when the select
        // changes are not shown, and the chip starts the row's defaults again. Palette all black,
        // so the ULA's own picture in between is black. Row 2 is "$91" then red blocks; on line 3
        // of the row (both blocks of the middle row lit) the select goes off, and back on six
        // characters (twelve cycles) later. So the line is: the code's black cell, red cells up to
        // three before the switch, nine black cells (six, and the chip's three), then the codes as
        // the row's defaults show them, white alphanumeric solid blocks.
        var rig = new Mode7Rig();
        for (int entry = 0; entry < 16; entry++)
        {
            rig.Bus.Write(0xFE21, (byte)((entry << 4) | 7));
        }
        rig.Poke(2, 0, 0x91);
        for (int column = 1; column < 40; column++)
        {
            rig.Poke(2, column, 0xFF);
        }
        rig.RunFrames(3);

        CrtcState state;
        while (!((state = rig.Bus.Crtc.StateAt(rig.Bus.Cycles)).LineInFrame == 23 && state.Character == 14))
        {
            rig.Bus.Read(0x0000);
        }
        bool odd = state.OddField;
        rig.Bus.Write(0xFE20, 0x48);
        for (int i = 0; i < 11; i++)
        {
            rig.Bus.Read(0x0000);
        }
        rig.Bus.Write(0xFE20, 0x4B);
        while (rig.Bus.Crtc.StateAt(rig.Bus.Cycles).LineInFrame == 23)
        {
            rig.Bus.Read(0x0000);
        }

        int y = (2 * 23) + (odd ? 1 : 0);
        var cells = new char[40];
        for (int column = 0; column < 40; column++)
        {
            uint left = rig.Bus.Screen.Pixel(16 * column, y), right = rig.Bus.Screen.Pixel((16 * column) + 15, y);
            cells[column] = (left, right) switch
            {
                (OpaqueBlack, OpaqueBlack) => 'K',
                (0xFF0000FFu, 0xFF0000FFu) => 'R',
                (OpaqueBlack, 0xFFFFFFFFu) => 'W',
                _ => '?',
            };
        }
        string line = new(cells);
        int red = line.LastIndexOf('R');
        Assert.Matches("^KR+K{9}W+$", line);
        Assert.InRange(red, 10, 14);
    }

    [Fact]
    public void Mode7ColoursComeFromTheControlCodesInScreenMemory()
    {
        // A red solid block after $91 on row 0: the 16 pixels of cell 1 on every line of the row
        // are red, and cell 0 (the code) is black.
        var rig = new Mode7Rig();
        rig.Poke(0, 0, 0x91);
        rig.Poke(0, 1, 0xFF);
        rig.RunFrames(3);

        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.Equal(OpaqueBlack, rig.Bus.Screen.Pixel(x, y));
                Assert.Equal(0xFF0000FFu, rig.Bus.Screen.Pixel(16 + x, y));
            }
        }
        Assert.Equal(OpaqueBlack, rig.Bus.Screen.Pixel(32, 0));
    }

    [Fact]
    public void TheMode7CursorLandsOnTheCellAtItsAddress()
    {
        // R8 = &93 skews CUDISP two characters and the ULA draws only segment 1, one more, which
        // lines it up with the chip's output (s2.2, s4.1). A steady cursor on lines 18 and 19 of
        // the row at text row 3, column 5 inverts that cell's last two frame rows and nothing else.
        var rig = new Mode7Rig();
        int address = 0x2800 + (3 * 40) + 5;
        rig.Crtc(10, 0x12);
        rig.Crtc(11, 0x13);
        rig.Crtc(14, (byte)(address >> 8));
        rig.Crtc(15, (byte)address);
        rig.RunFrames(3);

        for (int y = (20 * 3) + 18; y < (20 * 3) + 20; y++)
        {
            for (int x = 0; x < 640; x++)
            {
                uint expected = x >= 16 * 5 && x < 16 * 6 ? 0xFFFFFFFFu : OpaqueBlack;
                Assert.True(expected == rig.Bus.Screen.Pixel(x, y), $"pixel ({x}, {y})");
            }
        }
        Assert.Equal(OpaqueBlack, rig.Bus.Screen.Pixel(16 * 5, (20 * 3) + 17));
    }

    [Fact]
    public void Mode7BootsWithTheBannerInTeletext()
    {
        // The boot screen in mode 7 (the plan's constraints, task 5): row 1 reads the banner, and
        // BASIC's title is on its row of BootScreen (row 5 since the 8271 let the DFS print).
        var s = new BbcSession(mode: 7).Boot(3_000_000);
        Framebuffer screen = s.Machine.Screen;
        long frames = screen.Frames;
        for (int n = 0; n < 200 && screen.Frames < frames + 2; n++)
        {
            s.Machine.Run(1_000);
        }

        Assert.True(screen.Frames >= frames + 2, "two frames did not complete");
        Assert.StartsWith("BBC Computer 32K", TeletextScreen.ReadRow(screen, 1));
        Assert.Equal(BootScreen.Dfs, TeletextScreen.ReadRow(screen, 3).TrimEnd());
        Assert.Equal("BASIC", TeletextScreen.ReadRow(screen, BootScreen.LanguageRow).TrimEnd());
    }

    private static void RunRow(Teletext chip, byte[] codes)
    {
        for (int line = 0; line < Teletext.LinesPerRow; line++)
        {
            chip.BeginLine(roundFromRowBefore: true);
            chip.StartDisplay();
            foreach (byte code in codes)
            {
                chip.Cell(code);
            }
            chip.LineEnd(hadDisplay: true);
        }
    }

    private static string[] Render(byte[] codes, int line, bool odd = false, bool flashOn = true, bool lower = false)
    {
        var chip = new Teletext();
        var pixels = new byte[codes.Length * Teletext.CellWidth];
        chip.RenderLine(codes, line, odd, flashOn, lower, pixels);
        var cells = new string[codes.Length];
        for (int i = 0; i < codes.Length; i++)
        {
            var text = new char[Teletext.CellWidth];
            for (int h = 0; h < Teletext.CellWidth; h++)
            {
                text[h] = "KRGYBMCW"[pixels[(i * Teletext.CellWidth) + h]];
            }
            cells[i] = new string(text);
        }
        return cells;
    }

    /// <summary>
    /// A bus with no CPU, set to mode 7 with the registers the OS writes (video.md s3.1, s3.3:
    /// R7 plus one), screen memory at $7C00 filled with spaces. The first field after the CRTC's
    /// reset shows nothing (s1.4), so a picture of both fields needs three frames.
    /// </summary>
    private sealed class Mode7Rig
    {
        private static readonly byte[] CrtcMode7 = [0x3F, 0x28, 0x33, 0x24, 0x1E, 0x02, 0x19, 0x1B, 0x93, 0x12, 0x72, 0x13];

        public Mode7Rig()
        {
            Bus = new BbcBus(BbcSession.Roms);
            Bus.PowerOnReset();
            Bus.Write(0xFE20, 0x4B);
            for (int r = 11; r >= 0; r--)
            {
                Crtc(r, r == 7 ? (byte)(CrtcMode7[r] + 1) : CrtcMode7[r]);
            }
            Crtc(12, 0x28);
            Crtc(13, 0x00);
            for (int a = 0x7C00; a < 0x8000; a++)
            {
                Bus.PokeRam((ushort)a, 0x20);
            }
        }

        public BbcBus Bus { get; }

        public void Crtc(int register, byte value)
        {
            Bus.Write(0xFE00, (byte)register);
            Bus.Write(0xFE01, value);
        }

        public void Poke(int row, int column, byte value) => Bus.PokeRam((ushort)(0x7C00 + (row * 40) + column), value);

        public void Text(int row, int column, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                Poke(row, column + i, (byte)text[i]);
            }
        }

        public void RunFrames(int frames)
        {
            long target = Bus.Screen.Frames + frames;
            for (int n = 0; n < 1_000 && Bus.Screen.Frames < target; n++)
            {
                for (int i = 0; i < 1_000; i++)
                {
                    Bus.Read(0x0000);
                }
            }
            Assert.True(Bus.Screen.Frames >= target, $"{frames} frames did not complete");
        }
    }
}
