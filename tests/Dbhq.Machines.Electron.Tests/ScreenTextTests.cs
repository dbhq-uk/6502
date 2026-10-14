using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The screen decode checked on pictures made here before it is trusted with the machine's: first
/// pictures drawn straight from the font, every glyph in every mode, then one character put in
/// screen memory by hand in each mode and drawn by the display.
/// </summary>
public class ScreenTextTests
{
    private const uint Black = 0xFF000000, White = 0xFFFFFFFF, Blue = 0xFFFF0000, Yellow = 0xFF00FFFF, Red = 0xFF0000FF;

    private static byte[] Os => ElectronSession.Roms.Os;

    [Fact]
    public void TheGridsAreTheSheetsAndTheOsTables()
    {
        // ula.md s5a: text 80 x 32, 40 x 32, 20 x 32, 80 x 25, 40 x 32, 20 x 32, 40 x 25. The decode
        // reads them from the ROM ($C3B4 columns - 1, $C3AD rows - 1), so the ROM and the sheet agree.
        (int, int)[] sheet = [(80, 32), (40, 32), (20, 32), (80, 25), (40, 32), (20, 32), (40, 25)];
        for (int mode = 0; mode < 7; mode++)
        {
            Assert.Equal(sheet[mode], ScreenText.Grid(Os, mode));

            // The rows fill the lines the display shows: 32 x 8 = 256, 25 x 10 = 250 (s5a, s5d).
            Assert.Equal(UlaDisplay.DisplayedLines(mode), sheet[mode].Item2 * ScreenText.LinesPerRow(mode));
        }
    }

    [Fact]
    public void TheFontHasNoTwinsAndFourPairsDifferOnlyInLine7()
    {
        // Every glyph $20 to $7E is different in its eight lines; four pairs are the same above line 7,
        // which is where the OS's cursor goes (ScreenText's remarks).
        var seen = new HashSet<ulong>();
        for (int code = 0x20; code <= 0x7E; code++)
        {
            Assert.True(seen.Add(BitConverter.ToUInt64(ScreenText.Glyph(Os, code))), $"character {code:X2} repeats a glyph");
        }

        // In the order of the second of each pair: '.' $2E, ';' $3B, '_' $5F, 'q' $71.
        Assert.Equal([(',', '.'), (':', ';'), (' ', '_'), ('g', 'q')], ScreenText.SameAboveLine7(Os).ToArray());
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
        (int columns, int rows) = ScreenText.Grid(Os, mode);
        foreach ((uint fg, uint bg) in new[] { (White, Black), (Yellow, Blue) })
        {
            var picture = Blank();
            var expected = new string[rows];
            for (int row = 0; row < rows; row++)
            {
                var line = new char[columns];
                for (int column = 0; column < columns; column++)
                {
                    int code = 0x20 + (((row * columns) + column) % 95);
                    Draw(picture, mode, row, column, code, fg, bg, cursor: null);
                    line[column] = (char)code;
                }

                expected[row] = new string(line);
            }

            Assert.Equal(expected, ScreenText.Read(picture, mode, cursor: null, Os));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(6)]
    public void TheCursorCellReadsTheGlyphShownOrNot(int mode)
    {
        // The cursor replaces line 7 of its cell (OS $D6DE). Shown in the text colour or a third
        // colour, or not shown, every glyph reads as itself, except that a space and an underscore
        // read as a space, and under a cursor that is shown the other three pairs that differ only
        // in line 7 cannot be read.
        var pairs = ScreenText.SameAboveLine7(Os).ToArray();
        foreach (uint? cursor in new uint?[] { null, White, Red })
        {
            for (int code = 0x20; code <= 0x7E; code++)
            {
                uint[] picture = Blank();
                Draw(picture, mode, 2, 3, code, White, Black, cursor);
                char read = ScreenText.ReadCell(picture, Os, mode, 2, 3, hasCursor: true);

                char c = (char)code;
                char expected = c is ' ' or '_' ? ' '
                    : cursor is not null && pairs.Any(p => p.Item1 == c || p.Item2 == c) ? ScreenText.Unknown
                    : c;
                Assert.True(expected == read, $"mode {mode}, character {code:X2}, cursor {cursor:X8}: read '{read}'");
            }
        }
    }

    [Fact]
    public void AStrayPixelOrAThirdColourReadsAsUnknown()
    {
        uint[] picture = Blank();
        Draw(picture, 4, 0, 0, 'A', White, Black, cursor: null);
        Draw(picture, 4, 0, 1, 'B', White, Black, cursor: null);
        Draw(picture, 4, 0, 2, 'C', White, Black, cursor: null);
        picture[(3 * 640) + 16] ^= 0x00FFFFFF;      // half a font bit of B, at (16, 3)
        picture[(5 * 640) + 32] = Red;              // C: a font bit's patch in a third colour
        picture[(5 * 640) + 33] = Red;
        string row = ScreenText.Read(picture, 4, cursor: null, Os)[0];
        Assert.Equal("A" + ScreenText.Unknown + ScreenText.Unknown, row[..3]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void ACharacterPutInScreenMemoryIsDrawnAndReadBack(int mode)
    {
        // An 'E' in row 1, column 2, put in RAM as the OS would put it (s5b): each font byte
        // becomes the mode's bytes for its eight pixels, in the text colour where a bit is set and
        // colour 0 where it is not. The text colour is the OS's default: logical 8 in two colours,
        // pixel value 3 (logical 10) in four, and 7 in sixteen; the palette is the OS's (s5c).
        int start = UlaDisplay.ScreenStart(mode);
        int bytesPerLine = UlaDisplay.BytesPerLine(mode);
        (int columns, int _) = ScreenText.Grid(Os, mode);
        int bytesPerChar = bytesPerLine / columns;
        int valuesPerByte = UlaDisplay.PixelsPerByte(mode);
        int text = valuesPerByte switch { 8 => 1, 4 => 3, _ => 7 };

        var ram = new byte[0x8000];
        ReadOnlySpan<byte> glyph = ScreenText.Glyph(Os, 'E');
        int cell = start + (1 * bytesPerLine * 8) + (2 * bytesPerChar * 8);
        for (int line = 0; line < 8; line++)
        {
            for (int j = 0; j < bytesPerChar; j++)
            {
                var values = new int[valuesPerByte];
                for (int k = 0; k < valuesPerByte; k++)
                {
                    int bit = 7 - ((j * valuesPerByte) + k);
                    values[k] = ((glyph[line] >> bit) & 1) != 0 ? text : 0;
                }

                ram[cell + (j * 8) + line] = Encode(values);
            }
        }

        var display = new UlaDisplay(ram);
        display.Write(7, (byte)(mode << 3), 0);
        display.Write(3, (byte)(start >> 9), 0);
        byte[] palette = mode switch
        {
            1 or 5 => [0x73, 0x31],
            2 => [0xF5, 0x5F, 0x05, 0x5F, 0x05, 0x50, 0xF5, 0x50],
            _ => [0x11, 0x11],
        };
        for (int i = 0; i < palette.Length; i++)
        {
            display.Write(8 + i, palette[i], 0);
        }

        display.CatchUp((255 * 128) + 1);
        string[] rows = ScreenText.Read(display.Screen.Pixels, mode, cursor: null, Os);
        Assert.Equal("  E", rows[1].TrimEnd());
        Assert.All(rows.Where((_, i) => i != 1), r => Assert.Equal("", r.TrimEnd()));
    }

    /// <summary>
    /// A byte from its pixels' values, left first, by s5b: one bit a pixel from bit 7; two bits,
    /// pixel k's high bit at 7-k and low at 3-k; four bits, pixel k's at 7-k, 5-k, 3-k, 1-k.
    /// </summary>
    private static byte Encode(int[] values)
    {
        int b = 0;
        for (int k = 0; k < values.Length; k++)
        {
            int v = values[k];
            switch (values.Length)
            {
                case 8:
                    b |= (v & 1) << (7 - k);
                    break;
                case 4:
                    b |= ((v >> 1) & 1) << (7 - k);
                    b |= (v & 1) << (3 - k);
                    break;
                default:
                    b |= ((v >> 3) & 1) << (7 - k);
                    b |= ((v >> 2) & 1) << (5 - k);
                    b |= ((v >> 1) & 1) << (3 - k);
                    b |= (v & 1) << (1 - k);
                    break;
            }
        }

        return (byte)b;
    }

    private static uint[] Blank()
    {
        var picture = new uint[640 * 256];
        Array.Fill(picture, Black);
        return picture;
    }

    /// <summary>
    /// Draws one character straight from the font into a picture, as the display would show it:
    /// each font bit 640 / columns / 8 pixels wide, a line a row, the blank lines of modes 3 and 6
    /// black, and line 7 in <paramref name="cursor"/>'s colour where a cursor is shown.
    /// </summary>
    private static void Draw(uint[] picture, int mode, int row, int column, int code, uint fg, uint bg, uint? cursor)
    {
        (int columns, _) = ScreenText.Grid(Os, mode);
        int lines = ScreenText.LinesPerRow(mode);
        int cellWidth = 640 / columns;
        int bitWidth = cellWidth / 8;
        ReadOnlySpan<byte> glyph = ScreenText.Glyph(Os, code);
        for (int line = 0; line < lines; line++)
        {
            int y = (row * lines) + line;
            for (int x = 0; x < cellWidth; x++)
            {
                uint colour = line >= 8 ? Black
                    : line == 7 && cursor is { } c ? c
                    : ((glyph[line] >> (7 - (x / bitWidth))) & 1) != 0 ? fg : bg;
                picture[(y * 640) + (column * cellWidth) + x] = colour;
            }
        }
    }
}
