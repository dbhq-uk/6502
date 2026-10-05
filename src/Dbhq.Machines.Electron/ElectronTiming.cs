namespace Dbhq.Machines.Electron;

/// <summary>What a bus cycle is for, which is all its cost depends on (ula.md s3a, s4b).</summary>
public enum AccessKind
{
    /// <summary>The OS ROM and a paged ROM: 2 MHz, one cycle.</summary>
    Rom,

    /// <summary>RAM, A15 clear: 1 MHz in every mode, and in modes 0 to 3 held up by the display.</summary>
    Ram,

    /// <summary>$FC00-$FEFF and the keyboard slots: 1 MHz, never held up by the display.</summary>
    Io,
}

/// <summary>
/// How long a bus cycle takes (ula.md s4b). Time counts 2 MHz cycles since power on.
/// </summary>
public static class ElectronTiming
{
    // The display window of a contended line blocks the 1 MHz boundaries at these positions
    // within the line: 40 boundaries, 80 cycles (s4b, s4d). Where the window sits against the line
    // start is a convention, the sheet's own (s4f): no total depends on it.
    private const int WindowFirst = 2;
    private const int WindowLast = 80;

    /// <summary>
    /// The time at which an access that starts at <paramref name="start"/> completes, with the
    /// ULA in <paramref name="mode"/> when it starts. A ROM access takes one cycle. A RAM or I/O
    /// access completes at the first 1 MHz boundary at least two cycles on, never the next one: 2
    /// cycles from a boundary, 3 from one cycle off. Which parity is a boundary is not known (ula.md
    /// s4f, s12 item 1) and no total depends on it, so a boundary is an even time, the sheet's own
    /// convention. In modes 0 to 3 a RAM access whose boundary falls in the display window of a
    /// contended line waits for the window's end (s4b, s4c); I/O never waits (s3a, s12 item 2).
    /// </summary>
    /// <remarks>
    /// The wait is decided at the boundary, so an access that starts before a window and still
    /// needs a boundary inside it is stopped, and one that starts inside waits for its end (s4b).
    /// This is the sheet's loop as it is written; a boundary is checked once per step of two.
    /// </remarks>
    public static long Complete(long start, AccessKind kind, int mode)
    {
        if (kind == AccessKind.Rom)
        {
            return start + 1;
        }

        long boundary = start + 2;
        if ((boundary & 1) != 0)
        {
            boundary++;
        }

        if (kind == AccessKind.Ram && mode <= 3)
        {
            while (Blocked(boundary, mode))
            {
                boundary += 2;
            }
        }

        return boundary;
    }

    /// <summary>Whether the display holds the RAM at boundary <paramref name="t"/> (s4b).</summary>
    private static bool Blocked(long t, int mode)
    {
        Ula.FieldAndLine(t, out int line, out int position);
        return position is >= WindowFirst and <= WindowLast && IsContendedLine(line, mode);
    }

    /// <summary>
    /// s4c: in modes 0 to 2 the 256 active lines of a field; in mode 3 the 8 data lines of each of
    /// its 25 rows of 10, and not the 2 blank lines under each row, which fetch nothing; in modes 4
    /// to 6 none.
    /// </summary>
    private static bool IsContendedLine(int line, int mode) => mode switch
    {
        <= 2 => line < 256,
        3 => line < 250 && line % 10 < 8,
        _ => false,
    };
}
