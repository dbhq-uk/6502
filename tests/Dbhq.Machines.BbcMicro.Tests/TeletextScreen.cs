namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// Reads mode 7 text back from the framebuffer, by matching each cell against the glyph grids
/// typed from the datasheet's Figure 11 (<see cref="Figure11"/>), never the production table.
/// </summary>
/// <remarks>
/// <para>
/// Where a cell is: with the OS's mode 7 registers a text row is 20 framebuffer rows (10 lines a
/// field, the fields woven) and a cell 16 pixels across, one microsecond of the 1 MHz character
/// clock (<see cref="Framebuffer"/>'s remarks). The chip's 12 half-dots each last a twelfth of
/// that, so a pixel shows the half-dot under its centre: pixel p is half-dot (2p + 1) * 12 / 32,
/// rounded down. Glyph row r is the cell's line r + 1, frame rows 2(r + 1) and 2(r + 1) + 1 of the
/// text row (even field, then odd), and its dot d is half-dots 2 + 2d and 3 + 2d (s4.1).
/// </para>
/// <para>
/// A dot counts as on when every pixel of both its half-dots is lit, not black, on both frame
/// rows. Character rounding only ever adds a half-dot beside a dot that is off, and on one of the
/// two fields, so a dot it touches is never read as on unless both of its rows have a diagonal on
/// both sides of it, which none of the glyphs read here has. The cell's top line and left column
/// must be black. A cell that matches no grid, or more than one, reads as <c>?</c>.
/// </para>
/// </remarks>
public static class TeletextScreen
{
    private const uint OpaqueBlack = 0xFF000000;

    public static string ReadRow(Framebuffer screen, int row, int cells = 40)
    {
        var text = new char[cells];
        for (int column = 0; column < cells; column++)
        {
            text[column] = ReadCell(screen, row, column);
        }
        return new string(text);
    }

    public static char ReadCell(Framebuffer screen, int row, int column)
    {
        int left = 16 * column, top = 20 * row;

        // The blank top line, both fields, and the blank left column on every line.
        for (int x = 0; x < 16; x++)
        {
            if (Lit(screen, left + x, top) || Lit(screen, left + x, top + 1))
            {
                return '?';
            }
        }
        for (int y = 0; y < 20; y++)
        {
            foreach (int x in PixelsOf(0).Concat(PixelsOf(1)))
            {
                if (Lit(screen, left + x, top + y))
                {
                    return '?';
                }
            }
        }

        var dots = new int[9];
        for (int r = 0; r < 9; r++)
        {
            for (int d = 0; d < 5; d++)
            {
                bool on = true;
                foreach (int x in PixelsOf(2 + (2 * d)).Concat(PixelsOf(3 + (2 * d))))
                {
                    on &= Lit(screen, left + x, top + (2 * (r + 1))) && Lit(screen, left + x, top + (2 * (r + 1)) + 1);
                }
                dots[r] = (dots[r] << 1) | (on ? 1 : 0);
            }
        }

        char found = '?';
        foreach ((char glyph, string[] grid) in Figure11.Grids)
        {
            bool match = true;
            for (int r = 0; r < 9 && match; r++)
            {
                match = Figure11.Bits(grid[r]) == dots[r];
            }
            if (match)
            {
                if (found != '?')
                {
                    return '?';
                }
                found = glyph;
            }
        }
        return found;
    }

    /// <summary>The pixels, 0 to 15 across a cell, that show half-dot <paramref name="halfDot"/>.</summary>
    private static IEnumerable<int> PixelsOf(int halfDot)
    {
        for (int p = 0; p < 16; p++)
        {
            if ((2 * p + 1) * 12 / 32 == halfDot)
            {
                yield return p;
            }
        }
    }

    private static bool Lit(Framebuffer screen, int x, int y) => screen.Pixel(x, y) != OpaqueBlack;
}
