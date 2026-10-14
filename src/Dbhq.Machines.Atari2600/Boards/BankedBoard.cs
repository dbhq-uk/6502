namespace Dbhq.Machines.Atari2600.Boards;

/// <summary>
/// Atari's bank switching, which F8, F6 and F4 share: 4K banks, one showing at a time, chosen by an
/// access to a run of hotspots near the top of the window, one hotspot a bank in order.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/cartridge.md</c> sections 2 to 5 and 8.
/// </para>
/// <list type="bullet">
/// <item><description>Any access to a hotspot switches, read or write, whatever the data (section 3).</description></item>
/// <item><description>A hotspot read returns its byte in the bank it selects: the latch flips while the address is on the bus (section 4, statement 2, which the sheet follows and which corrected the plan's first draft).</description></item>
/// <item><description>The latch holds until another hotspot is accessed (section 3).</description></item>
/// <item><description>The board powers on in its last bank, a choice (section 8).</description></item>
/// </list>
/// <para>
/// The Superchip (<see cref="Superchip"/>) is carried on top of one of these, so the three are
/// written once.
/// </para>
/// </remarks>
public abstract class BankedBoard : IBoard
{
    private const int BankSize = 0x1000;

    private readonly byte[] _image;
    private readonly int _firstHotspot;
    private readonly int _banks;
    private int _base;

    /// <summary>
    /// A board over <paramref name="image"/>, <paramref name="banks"/> banks of 4K, whose hotspots
    /// start at the 12-bit offset <paramref name="firstHotspot"/>.
    /// </summary>
    private protected BankedBoard(byte[] image, int banks, int firstHotspot)
    {
        _image = image;
        _banks = banks;
        _firstHotspot = firstHotspot;
        PowerOn();
    }

    /// <inheritdoc />
    public int Bank { get; private set; }

    /// <inheritdoc />
    public byte Access(ushort address, bool write, byte dataBus)
    {
        int offset = address & (BankSize - 1);
        int hotspot = offset - _firstHotspot;
        if ((uint)hotspot < (uint)_banks)
        {
            Select(hotspot);
        }

        return write ? dataBus : _image[_base + offset];
    }

    /// <inheritdoc />
    public byte Peek(ushort address)
    {
        int offset = address & (BankSize - 1);
        int hotspot = offset - _firstHotspot;
        int bank = (uint)hotspot < (uint)_banks ? hotspot : Bank;
        return _image[(bank * BankSize) + offset];
    }

    /// <inheritdoc />
    public void PowerOn() => Select(_banks - 1);

    private void Select(int bank)
    {
        Bank = bank;
        _base = bank * BankSize;
    }
}
