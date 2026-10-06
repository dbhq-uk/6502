namespace Dbhq.Machines.Nes;

/// <summary>
/// A standard NES controller (fact sheet <c>bus.md</c> section 7): eight buttons behind a shift
/// register that the console's strobe loads and its reads clock.
/// </summary>
/// <remarks>
/// <para>
/// While the strobe is high the register reloads from the buttons all the time, so a read gives
/// button A and does not advance. When the strobe falls the buttons are latched, and each read then
/// gives the next one: A, B, Select, Start, Up, Down, Left, Right. After eight reads an official
/// pad gives 1.
/// </para>
/// <para>
/// <b>The pad drives bit 0 only.</b> <see cref="Read"/> returns 0 or 1. The bus adds the open-bus
/// bits 7 to 5 and leaves 4 to 1 at 0, as the NES-001 does when nothing drives them.
/// </para>
/// <para>
/// <b>Any 8-bit mask is reported as written</b>, Left with Right and Up with Down included. A real
/// pad's wiring has eight independent switches and no interlock, and some games read the
/// impossible mask on purpose, so nothing here filters it.
/// </para>
/// </remarks>
public sealed class Controller
{
    private bool _strobe;
    private byte _latched;
    private int _reads;

    /// <summary>The buttons held: bit 0 A, 1 B, 2 Select, 3 Start, 4 Up, 5 Down, 6 Left, 7 Right.</summary>
    public byte Buttons { get; set; }

    /// <summary>
    /// The strobe line, from bit 0 of a write to <c>$4016</c>. Raising it, or leaving it high, loads
    /// the register. Lowering it latches the buttons as they are then. Writing 0 while it is
    /// already low changes nothing: the pad is not reloaded.
    /// </summary>
    public void Strobe(bool high)
    {
        if (high || _strobe)
        {
            _latched = Buttons;
            _reads = 0;
        }

        _strobe = high;
    }

    /// <summary>
    /// One read of the port: bit 0 is the next button, and the register moves on. With the strobe
    /// high it is button A as it is now, and the register does not move. After eight reads it is 1.
    /// Bits 1 to 7 are 0 here; the bus puts the open bus in 7 to 5.
    /// </summary>
    public byte Read()
    {
        if (_strobe)
        {
            return (byte)(Buttons & 1);
        }

        if (_reads >= 8)
        {
            return 1;
        }

        return (byte)((_latched >> _reads++) & 1);
    }

    /// <summary>What <see cref="Read"/> would give, with no side effect.</summary>
    public byte Peek()
    {
        if (_strobe)
        {
            return (byte)(Buttons & 1);
        }

        return _reads >= 8 ? (byte)1 : (byte)((_latched >> _reads) & 1);
    }

    /// <summary>Switches the pad off and on: the strobe low and the register empty. The buttons held are the player's, and stay.</summary>
    internal void PowerOn()
    {
        _strobe = false;
        _latched = 0;
        _reads = 0;
    }
}
