namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// Reads the text on the screen back from the picture: the framebuffer the ULA drew, cut into the
/// mode's character cells, each matched against the OS ROM's font. Never the machine's screen
/// memory, which is what makes a check with it more than the OS reading back its own writes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The grid.</b> Columns and rows are the OS's own, from the tables the mode change reads with
/// <c>LDY $C3B4,X</c> (columns - 1) and <c>LDY $C3AD,X</c> (rows - 1) at <c>$CA0F</c> and
/// <c>$CA18</c>, X being the mode. A row is 8 lines of the picture, or 10 in modes 3 and 6, whose
/// last two are the blank lines the ULA draws black (<c>ula.md</c> s5a); the picture has one row of
/// pixels a line. A cell is 640 / columns pixels across.
/// </para>
/// <para>
/// <b>The font.</b> The OS draws a character's eight by eight bits from its font at
/// <c>$C000 + (code - $20) x 8</c> (<c>ula.md</c> s10b), a byte a row with bit 7 on the left, each
/// bit as one pixel of the mode in the text colour where it is set and the background where it is
/// not. In modes 1, 2 and 5 it expands each bit into the two or four bits a pixel of that colour
/// takes, so in every mode a font bit is one screen pixel, 640 / columns / 8 framebuffer pixels
/// wide, and a cell holds at most two colours. A cell is read by first taking the colour of each
/// font bit's patch, which must be one colour, then finding the glyph whose set bits are exactly
/// the patches of one colour. A cell of one colour is a space. A cell that matches no glyph, or
/// two, reads as <see cref="Unknown"/>, which is not printable ASCII, so it cannot be mistaken for
/// a character the OS printed: the bell glyph after the banner reads as it. The candidates are
/// <c>$20</c> to <c>$7E</c>: <c>$7F</c> is a solid block, and VDU 127 is delete.
/// </para>
/// <para>
/// <b>The cursor.</b> The Electron has no hardware cursor. The OS's routine at <c>$D6DE</c> swaps
/// line 7 of each byte of the cursor's cell with bytes it keeps from <c>$080C</c>
/// (<c>LDY #$07</c>, <c>LDA ($D6),Y</c>, <c>LDA $080C,X</c>, <c>STA ($D6),Y</c>), and flashes the
/// cursor by swapping back. So in the cursor's cell line 7 may be the cursor and not the glyph,
/// and the decode is told where the cursor is: the OS's text cursor, which the caller reads from
/// the OS's variables. In that cell a plain reading is tried, and if it fails, a reading of lines 0
/// to 6 alone, with line 7 one colour. Four pairs of glyphs differ only in line 7 (space and
/// underscore, comma and full stop, colon and semicolon, g and q), so under a cursor that is shown
/// the second reading cannot tell them apart and gives <see cref="Unknown"/>; a space and an
/// underscore, the cursor shown on one and not the other, draw the same, so in the cursor's cell
/// either reads as a space. A test holds this list against the font.
/// </para>
/// </remarks>
public static class ScreenText
{
    /// <summary>What a cell that matches no glyph, or several, reads as.</summary>
    public const char Unknown = '\uFFFD';

    private const int Width = 640;
    private const int CursorLine = 7;

    /// <summary>The OS ROM offsets of the text grid tables (CPU address - $C000).</summary>
    private const int ColumnsTable = 0x03B4, RowsTable = 0x03AD;

    /// <summary>The mode's text grid, from the OS's own tables.</summary>
    public static (int Columns, int Rows) Grid(byte[] os, int mode)
    {
        ArgumentNullException.ThrowIfNull(os);
        ArgumentOutOfRangeException.ThrowIfNegative(mode);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mode, 6);
        return (os[ColumnsTable + mode] + 1, os[RowsTable + mode] + 1);
    }

    /// <summary>Lines of the picture to a text row: 10 in modes 3 and 6, 8 in the rest (<c>ula.md</c> s5a).</summary>
    public static int LinesPerRow(int mode) => mode is 3 or 6 ? 10 : 8;

    /// <summary>The eight font bytes of a character, from <c>os.rom</c> at <c>$C000 + (code - $20) x 8</c>.</summary>
    public static ReadOnlySpan<byte> Glyph(byte[] os, int code) => os.AsSpan((code - 0x20) * 8, 8);

    /// <summary>The rows of text on the screen, top first, each as many characters as the mode has columns.</summary>
    /// <param name="pixels">The framebuffer's 640 by 256 pixels.</param>
    /// <param name="mode">The screen mode the OS set, 0 to 6.</param>
    /// <param name="cursor">The OS's text cursor, column and row, or null for none.</param>
    /// <param name="os">The OS ROM, for its font and its grid tables.</param>
    public static string[] Read(ReadOnlySpan<uint> pixels, int mode, (int Column, int Row)? cursor, byte[] os)
    {
        (int columns, int rows) = Grid(os, mode);
        uint[] picture = pixels.ToArray();
        var text = new string[rows];
        var line = new char[columns];
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                bool hasCursor = cursor is { } at && at.Column == column && at.Row == row;
                line[column] = ReadCell(picture, os, mode, row, column, hasCursor);
            }

            text[row] = new string(line);
        }

        return text;
    }

    /// <summary>The character at one cell; <paramref name="hasCursor"/> if it is the cursor's.</summary>
    public static char ReadCell(uint[] picture, byte[] os, int mode, int row, int column, bool hasCursor)
    {
        ArgumentNullException.ThrowIfNull(picture);
        (int columns, _) = Grid(os, mode);
        int lines = LinesPerRow(mode);
        int cellWidth = Width / columns;
        int bitWidth = cellWidth / 8;
        int left = cellWidth * column;
        int top = lines * row;

        var colours = new uint[lines, 8];
        for (int line = 0; line < lines; line++)
        {
            int y = top + line;
            for (int bit = 0; bit < 8; bit++)
            {
                int x0 = left + (bit * bitWidth);
                uint colour = picture[(y * Width) + x0];
                for (int x = 1; x < bitWidth; x++)
                {
                    if (picture[(y * Width) + x0 + x] != colour)
                    {
                        return Unknown;
                    }
                }

                colours[line, bit] = colour;
            }
        }

        // The blank lines under a row in modes 3 and 6 are black (s5a), cursor or not: the OS's
        // cursor touches line 7 only.
        for (int line = 8; line < lines; line++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                if (colours[line, bit] != 0xFF000000)
                {
                    return Unknown;
                }
            }
        }

        char? plain = Match(os, colours, 8);
        if (!hasCursor)
        {
            return plain ?? Unknown;
        }

        // The cursor's cell: line 7 may be the cursor's bytes. A space and an underscore read as
        // a space either way; otherwise the plain reading, or else lines 0 to 6 with line 7 one colour.
        char? upper = LineOneColour(colours, CursorLine) ? Match(os, colours, CursorLine) : null;
        if (plain is ' ' or '_')
        {
            return ' ';
        }

        return plain ?? upper ?? Unknown;
    }

    /// <summary>
    /// The pairs of glyphs, codes <c>$20</c> to <c>$7E</c>, that are the same in lines 0 to 6: what
    /// a cursor shown over line 7 cannot tell apart.
    /// </summary>
    public static IEnumerable<(char, char)> SameAboveLine7(byte[] os)
    {
        var seen = new Dictionary<ulong, char>();
        for (int code = 0x20; code <= 0x7E; code++)
        {
            ulong key = Bits(Glyph(os, code), CursorLine);
            if (seen.TryGetValue(key, out char other))
            {
                yield return (other, (char)code);
            }
            else
            {
                seen.Add(key, (char)code);
            }
        }
    }

    private static bool LineOneColour(uint[,] colours, int line)
    {
        for (int bit = 1; bit < 8; bit++)
        {
            if (colours[line, bit] != colours[line, 0])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The one character that lines 0 to <paramref name="lines"/> - 1 of the cell show, in two
    /// colours or one; null if none or several.
    /// </summary>
    private static char? Match(byte[] os, uint[,] colours, int lines)
    {
        var distinct = new List<uint>(2);
        for (int line = 0; line < lines; line++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                if (!distinct.Contains(colours[line, bit]))
                {
                    if (distinct.Count == 2)
                    {
                        return null;
                    }

                    distinct.Add(colours[line, bit]);
                }
            }
        }

        Dictionary<ulong, char?> font = Font(os, lines);
        if (distinct.Count == 1)
        {
            // One colour: a space, or nothing if the lines read also fit another glyph.
            return font.TryGetValue(0, out char? blank) ? blank : null;
        }

        char? found = null;
        foreach (uint foreground in distinct)
        {
            ulong bits = 0;
            for (int line = 0; line < lines; line++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    bits = (bits << 1) | (colours[line, bit] == foreground ? 1UL : 0);
                }
            }

            if (font.TryGetValue(bits, out char? glyph))
            {
                if (found is not null || glyph is null)
                {
                    return null;
                }

                found = glyph;
            }
        }

        return found;
    }

    private static readonly Dictionary<(byte[], int), Dictionary<ulong, char?>> Fonts = new(new FontKeyComparer());

    /// <summary>
    /// The bits of lines 0 to <paramref name="lines"/> - 1 of each candidate glyph, to its
    /// character, or to null where two glyphs share them.
    /// </summary>
    private static Dictionary<ulong, char?> Font(byte[] os, int lines)
    {
        lock (Fonts)
        {
            if (!Fonts.TryGetValue((os, lines), out Dictionary<ulong, char?>? font))
            {
                font = [];
                for (int code = 0x20; code <= 0x7E; code++)
                {
                    ulong bits = Bits(Glyph(os, code), lines);
                    font[bits] = font.ContainsKey(bits) ? null : (char)code;
                }

                Fonts[(os, lines)] = font;
            }

            return font;
        }
    }

    private static ulong Bits(ReadOnlySpan<byte> glyph, int lines)
    {
        ulong bits = 0;
        for (int line = 0; line < lines; line++)
        {
            bits = (bits << 8) | glyph[line];
        }

        return bits;
    }

    private sealed class FontKeyComparer : IEqualityComparer<(byte[], int)>
    {
        public bool Equals((byte[], int) a, (byte[], int) b) => ReferenceEquals(a.Item1, b.Item1) && a.Item2 == b.Item2;

        public int GetHashCode((byte[], int) key) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.Item1), key.Item2);
    }
}
