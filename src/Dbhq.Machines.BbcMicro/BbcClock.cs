namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The Model B's clock as its chips see it: the 2 MHz CPU cycles the bus has run, and the
/// earliest cycle at which some chip next needs the bus to look at it.
/// </summary>
/// <remarks>
/// <para>
/// The bus is the only thing that moves <see cref="Cycles"/>, one for each cycle of each access.
/// A chip that runs lazily reads the time here and catches up to it whenever it is looked at, so
/// the chip's state is never stale to anyone who asks.
/// </para>
/// <para>
/// <see cref="NextEvent"/> is the event horizon. Between events nothing a chip does can be seen
/// from outside it, so the bus does no work for the chips at all; at the end of the access whose
/// cycles reach <see cref="NextEvent"/> it brings every chip up to date, sets the CPU's IRQ line
/// from them and asks each for its next event. A chip changed from outside (a key, a line, a
/// reset) calls <see cref="WakeAt"/> so the bus looks again at the end of its next cycle.
/// </para>
/// </remarks>
internal sealed class BbcClock
{
    /// <summary>2 MHz CPU cycles since power on, stretch cycles included.</summary>
    public long Cycles;

    /// <summary>The cycle at whose end the bus next looks: the earlier of <see cref="ChipEvent"/> and the video ULA's next line end.</summary>
    public long NextEvent;

    /// <summary>
    /// The cycle at whose end the bus next brings the chips that can move the IRQ line up to date
    /// and asks them for their next event. The video ULA's line ends, which come every 64
    /// microseconds and cannot move the IRQ line, are kept apart from it, so a line end costs the
    /// bus the ULA's drawing and nothing else.
    /// </summary>
    public long ChipEvent;

    /// <summary>Brings <see cref="ChipEvent"/> and <see cref="NextEvent"/> forward to <paramref name="cycle"/> if they are later.</summary>
    public void WakeAt(long cycle)
    {
        if (cycle < ChipEvent)
        {
            ChipEvent = cycle;
        }

        if (cycle < NextEvent)
        {
            NextEvent = cycle;
        }
    }
}
