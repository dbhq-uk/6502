using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The screen decode checked on pictures drawn here, by hand from the font, before it is trusted
/// with the machine's: every glyph in every mode from 0 to 6, in three colour pairs, with the cursor
/// over it and without.
/// </summary>
public class ScreenTextTests
{
    private const uint Black = 0xFF000000, White = 0xFFFFFFFF, Blue = 0xFFFF0000, Yellow = 0xFF00FFFF, Red = 0xFF0000FF;
    private const uint Invert = 0x00FFFFFF;

    private static byte[] Os => BbcSession.Roms.Os;

    [Fact]
    public void TheGridsAreTheRomsAndTheFontHasNoTwinsOrInverses()
    {
        // The text grid of video.md s3.1 against the ROM's columns - 1 at $C3EF and rows - 1 at
        // $C3E7: the decode cuts the picture by the sheet, and the sheet agrees with the ROM.
        for (int mode = 0; mode < 8; mode++)
        {
            Assert.Equal(ScreenText.Grids[mode].Columns - 1, Os[0x03EF + mode]);
            Assert.Equal(ScreenText.Grids[mode].Rows - 1, Os[0x03E7 + mode]);
        }

        // No glyph is another's, or another's inverse, so a cell of two colours names one
        // character whichever colour is the foreground.
        var seen = new HashSet<ulong>();
        for (int code = 0x20; code <= 0x7E; code++)
        {
            ulong bits = Bits(code);
            Assert.True(seen.Add(bits), $"character {code:X2} repeats a glyph");
        }
        for (int code = 0x20; code <= 0x7E; code++)
        {
            Assert.DoesNotContain(~Bits(code), seen);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EveryGlyphReadsBackInEveryMode(int mode)
    {
        (int columns, int rows) = ScreenText.Grids[mode];
        foreach ((uint fg, uint bg) in new[] { (White, Black), (Yellow, Blue), (Red, Black) })
        {
            var picture = new uint[640 * 512];
            Array.Fill(picture, Black);
            var expected = new char[rows][];
            for (int row = 0; row < rows; row++)
            {
                expected[row] = new char[columns];
                for (int column = 0; column < columns; column++)
                {
                    int code = 0x20 + (((row * columns) + column) % 95);
                    Draw(picture, mode, row, column, code, fg, bg, cursorFields: 0);
                    expected[row][column] = (char)code;
                }
            }

            ScreenText.Cell[][] cells = ScreenText.ReadCells(picture, mode, cursor: null, Os);
            Assert.Equal(rows, cells.Length);
            for (int row = 0; row < rows; row++)
            {
                Assert.Equal(new string(expected[row]), new string(cells[row].Select(cell => cell.Text).ToArray()));
                foreach (ScreenText.Cell cell in cells[row])
                {
                    Assert.Equal(bg, cell.Background);
                    Assert.Equal(cell.Text == ' ' ? null : fg, cell.Foreground);
                }
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void TheCursorCellReadsTheSameShownOrNot(int mode)
    {
        // Every glyph at (3, 2), the cursor's cell, drawn with the cursor's lines inverted and
        // without. In white on black, with eight-line rows, a space under the cursor and an
        // underscore without it draw the same pixels, and so do an underscore under the cursor and
        // a space without it, so an underscore in the cursor's cell reads as a space, shown or not
        // (ScreenText's remarks). With ten-line rows the cursor also lights the blank lines 8 and
        // 9, which no glyph does, so there is no such pair.
        int lines = (ScreenText.CrtcTable(Os, mode)[9] & 0x1F) + 1;
        foreach ((uint fg, uint bg) in new[] { (White, Black), (Red, Black) })
        {
            for (int code = 0x20; code <= 0x7E; code++)
            {
                // Bit 0: shown on the even field, bit 1 the odd: at a blink it is on one only.
                for (int shown = 0; shown < 4; shown++)
                {
                    var picture = new uint[640 * 512];
                    Array.Fill(picture, Black);
                    Draw(picture, mode, 2, 3, code, fg, bg, shown);

                    char read = ScreenText.ReadCell(picture, mode, 2, 3, hasCursor: true, Os);
                    char expected = code == '_' && fg == White && lines == 8 ? ' ' : (char)code;
                    Assert.True(expected == read, $"mode {mode}, character {code:X2}, cursor on fields {shown}, colour {fg:X8}: read '{read}'");
                }
            }
        }
    }

    [Fact]
    public void AStrayPixelOrAThirdColourReadsAsUnknown()
    {
        var picture = new uint[640 * 512];
        Array.Fill(picture, Black);
        Draw(picture, 0, 0, 0, 'A', White, Black, cursorFields: 0);
        Draw(picture, 0, 0, 1, 'B', White, Black, cursorFields: 0);
        Draw(picture, 0, 0, 2, 'C', White, Black, cursorFields: 0);
        picture[(2 * 640) + 8 + 7] = Red;    // a third colour in B: line 1's last bit, both rows
        picture[(3 * 640) + 8 + 7] = Red;
        picture[(5 * 640) + 16] ^= Invert;   // one framebuffer row of a patch in C, not the other

        Assert.Equal("A??", ScreenText.Read(picture, 0, cursor: null, Os)[0][..3]);
    }

    private static ulong Bits(int code)
    {
        ulong bits = 0;
        foreach (byte b in ScreenText.Glyph(Os, code))
        {
            bits = (bits << 8) | b;
        }
        return bits;
    }

    /// <summary>
    /// A character as the OS and the video chips would draw it, worked from ScreenText's remarks:
    /// each font bit a screen pixel of 640 / columns / 8 framebuffer pixels, each line two rows,
    /// lines 8 and 9 of a 10-line row black, and the cursor's lines (R10 to R11 from the ROM)
    /// inverted on the fields <paramref name="cursorFields"/> names, bit 0 the even and bit 1 the odd.
    /// </summary>
    private static void Draw(uint[] picture, int mode, int row, int column, int code, uint fg, uint bg, int cursorFields)
    {
        (int columns, _) = ScreenText.Grids[mode];
        int lines = (ScreenText.CrtcTable(Os, mode)[9] & 0x1F) + 1;
        (int first, int last) = ScreenText.CursorLines(Os, mode);
        int bitWidth = 640 / columns / 8;
        ReadOnlySpan<byte> glyph = ScreenText.Glyph(Os, code);
        for (int line = 0; line < lines; line++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                for (int y = 0; y < 2; y++)
                {
                    uint colour = line >= 8 ? Black : ((glyph[line] >> (7 - bit)) & 1) != 0 ? fg : bg;
                    if (((cursorFields >> y) & 1) != 0 && line >= first && line <= last)
                    {
                        colour ^= Invert;
                    }
                    for (int x = 0; x < bitWidth; x++)
                    {
                        int px = (640 / columns * column) + (bit * bitWidth) + x;
                        int py = (2 * lines * row) + (2 * line) + y;
                        picture[(py * 640) + px] = colour;
                    }
                }
            }
        }
    }
}
