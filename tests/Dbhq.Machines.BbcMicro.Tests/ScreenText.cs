namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// Reads the text on the screen back from the picture: the framebuffer the video chips drew, cut
/// into the mode's character cells, each matched against a font. Never the machine's screen
/// memory, which is what makes a check with it more than the OS reading back its own writes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Modes 0 to 6.</b> The grid is the mode's text grid (<see cref="Grids"/>, <c>video.md</c>
/// s3.1). With the OS's registers a text row is R9 + 1 lines of a field (8, or 10 in modes 3 and
/// 6), each line two framebuffer rows, the fields woven, and a cell is 640 / columns pixels across
/// (<see cref="Framebuffer"/>'s remarks). The OS draws a character's eight by eight bits from its
/// font, at <c>$C000 + (code - $20) * 8</c> in <c>os.rom</c>, a byte a row with bit 7 on the left,
/// each bit as one pixel of the mode in the text foreground colour where it is set and the text
/// background where it is not: one bit a pixel in modes 0, 3, 4 and 6, and in modes 1, 2 and 5 the
/// OS expands each bit into the two or four bits a pixel of that colour takes. So in every mode a
/// font bit is one screen pixel, 640 / columns / 8 framebuffer pixels wide, and a cell holds at most
/// two colours. Lines 8 and 9 of a row in modes 3 and 6 are the gap the hardware blanks: black.
/// </para>
/// <para>
/// A cell is read by first taking the colour of each font bit's patch, which must be one colour on
/// each of its two framebuffer rows, and the same on both, then finding the glyph whose set bits
/// are exactly the patches of one colour, the other colour being the background. A cell of one colour is a space. A cell that
/// matches no glyph, or two, reads as <c>?</c>. The candidates are codes <c>$20</c> to <c>$7E</c>:
/// <c>$7F</c> is a solid block in the font, which would match any cell of one colour, and the OS
/// never draws it, because VDU 127 is delete. Characters a program redefines with VDU 23 are not
/// read: the font is the ROM's.
/// </para>
/// <para>
/// <b>Mode 7.</b> Each cell is matched against the datasheet's glyph grids with
/// <see cref="TeletextScreen"/>, as task 9's tests do.
/// </para>
/// <para>
/// <b>The cursor.</b> The cursor inverts some lines of one cell, and it blinks. In a mode whose two
/// colours are each other's inverse, such as white on black, a space under the cursor and an
/// underscore with the cursor off draw the same pixels, so one picture cannot tell them apart. The
/// decode is therefore told where the cursor is: the OS's text cursor, which the caller reads from
/// the OS's variables, and which lines it covers, R10 and R11 from the ROM's register table for the
/// mode (<c>video.md</c> s3.1). In that one cell, a reading with those lines inverted is also
/// allowed, and where both readings fit, which only a space and an underscore can, the cell reads as
/// a space. So in white on black, in a mode with eight-line rows, an underscore in the cursor's
/// cell reads as a space whether the cursor is shown or not: a limit of reading one picture. In
/// modes 3 and 6 the cursor also lights the blank lines 8 and 9, and the two never draw alike.
/// The cursor blinks field by field, and the framebuffer weaves the fields, so just after a blink
/// one field of the cell has the cursor and the other does not: in the cursor's cell each field is
/// read on its own, and the two readings must agree.
/// </para>
/// </remarks>
public static class ScreenText
{
    /// <summary>The cursor's inversion: physical colour c becomes c XOR 7.</summary>
    private const uint Invert = 0x00FFFFFF;

    private const uint OpaqueBlack = 0xFF000000;

    private const int Width = 640;

    /// <summary>
    /// Each mode's text grid, columns by rows, as <c>video.md</c> s3.1 gives it. A test holds it to
    /// the ROM's tables at <c>$C3EF</c> and <c>$C3E7</c> and to the text window the OS sets.
    /// </summary>
    public static readonly IReadOnlyList<(int Columns, int Rows)> Grids =
    [
        (80, 32), (40, 32), (20, 32), (80, 25), (40, 32), (20, 32), (40, 25), (40, 25),
    ];

    /// <summary>
    /// The rows of text on the screen, top first, each as many characters as the mode has columns.
    /// </summary>
    /// <param name="pixels">The framebuffer's 640 by 512 pixels.</param>
    /// <param name="mode">The screen mode the OS set, 0 to 7.</param>
    /// <param name="cursor">The OS's text cursor, column and row, or null for none.</param>
    /// <param name="os">The OS ROM, for its font and its CRTC register table.</param>
    public static string[] Read(ReadOnlySpan<uint> pixels, int mode, (int Column, int Row)? cursor, byte[] os)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mode, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mode, 7);
        uint[] picture = pixels.ToArray();
        (int columns, int rows) = Grids[mode];
        (int cursorFirst, int cursorLast) = CursorLines(os, mode);

        var text = new string[rows];
        for (int row = 0; row < rows; row++)
        {
            var line = new char[columns];
            for (int column = 0; column < columns; column++)
            {
                bool hasCursor = cursor is { } at && at.Column == column && at.Row == row;
                line[column] = mode == 7
                    ? ReadTeletextCell(picture, row, column, hasCursor, cursorFirst, cursorLast)
                    : ReadCell(picture, os, mode, row, column, hasCursor, cursorFirst, cursorLast);
            }
            text[row] = new string(line);
        }
        return text;
    }

    /// <summary>
    /// One cell of <see cref="Read"/>: the character at <paramref name="column"/> and
    /// <paramref name="row"/>, the cursor's cell if <paramref name="hasCursor"/>.
    /// </summary>
    public static char ReadCell(uint[] picture, int mode, int row, int column, bool hasCursor, byte[] os)
    {
        (int first, int last) = CursorLines(os, mode);
        return mode == 7
            ? ReadTeletextCell(picture, row, column, hasCursor, first, last)
            : ReadCell(picture, os, mode, row, column, hasCursor, first, last);
    }

    /// <summary>The lines of a cell the cursor covers in a mode: R10's start line and R11, from the ROM's table.</summary>
    public static (int First, int Last) CursorLines(byte[] os, int mode)
    {
        byte[] registers = CrtcTable(os, mode);
        return (registers[10] & 0x1F, registers[11] & 0x1F);
    }

    /// <summary>
    /// R0 to R11 as the OS stores them for a mode: the mode's group from <c>$C440</c>, then twelve
    /// bytes a group from <c>$C46E</c> (<c>video.md</c> s3.1).
    /// </summary>
    public static byte[] CrtcTable(byte[] os, int mode)
    {
        int group = os[0x0440 + mode];
        return os.AsSpan(0x046E + (12 * group), 12).ToArray();
    }

    /// <summary>The eight font bytes of a character, from <c>os.rom</c> at <c>$C000 + (code - $20) * 8</c>.</summary>
    public static ReadOnlySpan<byte> Glyph(byte[] os, int code) => os.AsSpan((code - 0x20) * 8, 8);

    private static char ReadCell(uint[] picture, byte[] os, int mode, int row, int column, bool hasCursor, int cursorFirst, int cursorLast)
    {
        (int columns, _) = Grids[mode];
        int lines = (CrtcTable(os, mode)[9] & 0x1F) + 1;
        int cellWidth = Width / columns;
        int bitWidth = cellWidth / 8;
        int left = cellWidth * column;
        int top = 2 * lines * row;

        // Each font bit's patch must be one colour along its framebuffer row. The even rows are
        // one field and the odd rows the other (Framebuffer's remarks), taken apart here because
        // the cursor blinks field by field: at a blink, one field has it and the other does not.
        var fields = new uint[2][,];
        for (int field = 0; field < 2; field++)
        {
            fields[field] = new uint[lines, 8];
            for (int line = 0; line < lines; line++)
            {
                int y = top + (2 * line) + field;
                for (int bit = 0; bit < 8; bit++)
                {
                    int x0 = left + (bit * bitWidth);
                    uint colour = picture[(y * Width) + x0];
                    for (int x = 1; x < bitWidth; x++)
                    {
                        if (picture[(y * Width) + x0 + x] != colour)
                        {
                            return '?';
                        }
                    }
                    fields[field][line, bit] = colour;
                }
            }
        }

        if (!hasCursor)
        {
            // Away from the cursor both fields show the same.
            for (int line = 0; line < lines; line++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    if (fields[0][line, bit] != fields[1][line, bit])
                    {
                        return '?';
                    }
                }
            }
            return Match(os, fields[0], lines, invertFrom: lines, invertTo: -1) ?? '?';
        }

        // The cursor's cell: each field read on its own, the cursor shown or not, and the two
        // fields must agree.
        char even = Choose(
            Match(os, fields[0], lines, invertFrom: lines, invertTo: -1),
            Match(os, fields[0], lines, cursorFirst, Math.Min(cursorLast, lines - 1)));
        char odd = Choose(
            Match(os, fields[1], lines, invertFrom: lines, invertTo: -1),
            Match(os, fields[1], lines, cursorFirst, Math.Min(cursorLast, lines - 1)));
        return even == odd ? even : '?';
    }

    private static char ReadTeletextCell(uint[] picture, int row, int column, bool hasCursor, int cursorFirst, int cursorLast)
    {
        // A mode 7 cell is 20 framebuffer rows, the even field's lines and the odd field's woven,
        // so raster address RA is the cell's frame row RA (TeletextScreen's remarks), and the
        // cursor's lines are frame rows R10 to R11 of the cell, even ones from the even field.
        int left = 16 * column, top = 20 * row;
        char plain = TeletextScreen.ReadCell((x, y) => picture[(y * Width) + x], row, column);
        if (!hasCursor)
        {
            return plain;
        }

        // The cursor shown on neither field, either one, or both: the readings that fit must agree.
        char found = '?';
        for (int shownOn = 0; shownOn < 4; shownOn++)
        {
            char reading = TeletextScreen.ReadCell(
                (x, y) =>
                {
                    uint pixel = picture[(y * Width) + x];
                    bool covered = x >= left && x < left + 16 && y - top >= cursorFirst && y - top <= cursorLast
                        && ((shownOn >> (y & 1)) & 1) != 0;
                    return covered ? pixel ^ Invert : pixel;
                },
                row,
                column);
            if (reading != '?')
            {
                if (found != '?' && found != reading)
                {
                    return '?';
                }
                found = reading;
            }
        }
        return found;
    }

    /// <summary>
    /// The cursor's cell: the reading that fits, and where both fit and differ, a space, because a
    /// space under the cursor and an underscore draw the same pixels (the remarks).
    /// </summary>
    private static char Choose(char? plain, char? underCursor)
    {
        if (plain is null)
        {
            return underCursor ?? '?';
        }
        if (underCursor is null || underCursor == plain)
        {
            return plain.Value;
        }
        return plain == ' ' || underCursor == ' ' ? ' ' : '?';
    }

    /// <summary>
    /// The one character the cell's colours show, with lines <paramref name="invertFrom"/> to
    /// <paramref name="invertTo"/> taken as inverted by the cursor; null if none or several.
    /// </summary>
    private static char? Match(byte[] os, uint[,] colours, int lines, int invertFrom, int invertTo)
    {
        uint Colour(int line, int bit)
        {
            uint colour = colours[line, bit];
            return line >= invertFrom && line <= invertTo ? colour ^ Invert : colour;
        }

        // The blanked lines below the font, in modes 3 and 6, are black.
        for (int line = 8; line < lines; line++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                if (Colour(line, bit) != OpaqueBlack)
                {
                    return null;
                }
            }
        }

        var distinct = new List<uint>(2);
        for (int line = 0; line < 8; line++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                uint colour = Colour(line, bit);
                if (!distinct.Contains(colour))
                {
                    if (distinct.Count == 2)
                    {
                        return null;
                    }
                    distinct.Add(colour);
                }
            }
        }

        if (distinct.Count == 1)
        {
            return ' ';
        }

        char? found = null;
        foreach (uint foreground in distinct)
        {
            ulong bits = 0;
            for (int line = 0; line < 8; line++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    bits = (bits << 1) | (Colour(line, bit) == foreground ? 1UL : 0);
                }
            }

            if (Font(os).TryGetValue(bits, out char glyph))
            {
                if (found is not null && found != glyph)
                {
                    return null;
                }
                found = glyph;
            }
        }
        return found;
    }

    private static readonly Dictionary<byte[], Dictionary<ulong, char>> Fonts = new(ReferenceEqualityComparer.Instance);

    /// <summary>The 64 bits of each candidate glyph, row 0 first and bit 7 of each row first, to its character.</summary>
    private static Dictionary<ulong, char> Font(byte[] os)
    {
        lock (Fonts)
        {
            if (!Fonts.TryGetValue(os, out Dictionary<ulong, char>? font))
            {
                font = [];
                for (int code = 0x21; code <= 0x7E; code++)
                {
                    ulong bits = 0;
                    foreach (byte b in Glyph(os, code))
                    {
                        bits = (bits << 8) | b;
                    }

                    // Two glyphs with the same bits could not be told apart; the font has none,
                    // and a test says so.
                    if (!font.TryAdd(bits, (char)code))
                    {
                        throw new InvalidOperationException($"Characters {(int)font[bits]:X2} and {code:X2} share a glyph.");
                    }
                }
                Fonts[os] = font;
            }
            return font;
        }
    }
}
