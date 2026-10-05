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
    /// <summary>
    /// The time at which an access that starts at <paramref name="start"/> completes. A ROM access
    /// takes one cycle. A RAM or I/O access completes at the first 1 MHz boundary at least two
    /// cycles on, never the next one: 2 cycles from a boundary, 3 from one cycle off. Which parity
    /// is a boundary is not known (ula.md s4f, s12 item 1) and no total depends on it, so a
    /// boundary is an even time, the sheet's own convention.
    /// </summary>
    /// <remarks>
    /// Contention is added in task 6; until then <paramref name="mode"/> is ignored. A RAM access
    /// in modes 0 to 3 will wait here for the end of the display window of a contended line.
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

        return boundary;
    }
}
