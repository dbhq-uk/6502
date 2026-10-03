namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The user VIA, IC69 at <c>$FE60</c>: the printer port on port A and the user port on port
/// B (fact sheet <c>via.md</c> section 2.4). Neither is modelled, so it is the chip with nothing
/// plugged in.
/// </summary>
/// <remarks>
/// A real Model B with nothing attached reads <c>?&amp;FE60</c> = 255, so the port inputs are
/// all 1 (section 1.2). CA1, the printer's ACK, is pulled up to +5 V through 4k7, so it idles
/// high and the OS's PCR of <c>$0E</c> (a negative edge) never sees an edge. CB1 and CB2 are on
/// the user port and taken to idle high like the port's other inputs [inferring]. CA2 is the
/// printer strobe, an output the OS drives through the PCR. BREAK resets this chip (section
/// 1.2); <see cref="BbcBus"/> does the choosing.
/// </remarks>
public sealed class UserVia : Via6522
{
    /// <summary>A user VIA on its own, whose time is the calls to <see cref="Via6522.Tick"/>.</summary>
    public UserVia()
        : this(null)
    {
    }

    internal UserVia(BbcClock? clock)
        : base(clock)
    {
        PortAInput = 0xFF;
        PortBInput = 0xFF;
        SetCa1(true);
        SetCb1(true);
        SetCb2(true);
    }
}
