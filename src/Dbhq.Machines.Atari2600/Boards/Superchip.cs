namespace Dbhq.Machines.Atari2600.Boards;

/// <summary>
/// The Superchip: 128 bytes of RAM on an F8, F6 or F4 board (F8SC, F6SC, F4SC), written at
/// <c>$1000-$107F</c> and read at <c>$1080-$10FF</c> in every bank. The board it carries answers
/// everything above, and switches as it does alone.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/cartridge.md</c> section 6.
/// </para>
/// <list type="bullet">
/// <item><description>The port is chosen by the address alone, because the slot has no read/write line. So a read of the write port, real or dummy, writes the byte left on the data bus into the cell, and the CPU reads that same byte back, since nothing drives the bus. The suite's <c>ram-superchip</c> asserts this and says it is untested on hardware.</description></item>
/// <item><description>A write to the read port stores nothing [inferring]; the bus is a clash of the CPU and the RAM [guessing - verify], and the model keeps the CPU's byte.</description></item>
/// <item><description>The RAM is not banked: a switch neither moves nor clears it. The ROM under <c>$1000-$10FF</c> is never seen.</description></item>
/// <item><description>The RAM is clear at power on: a choice, since no source states it.</description></item>
/// </list>
/// <para>
/// An access to the RAM ports does not reach the board underneath: its hotspots are at the top of
/// the window, so it would do nothing with it.
/// </para>
/// </remarks>
public sealed class Superchip : IBoard
{
    /// <summary>The RAM's size in bytes.</summary>
    public const int RamSize = 128;

    private const int WritePort = 0x000;
    private const int ReadPort = 0x080;
    private const int PortsEnd = 0x100;

    private readonly BankedBoard _board;
    private readonly byte[] _ram = new byte[RamSize];

    /// <summary>The Superchip on <paramref name="board"/>, which it owns from now on.</summary>
    internal Superchip(BankedBoard board)
    {
        _board = board;
    }

    /// <inheritdoc />
    public int Bank => _board.Bank;

    /// <inheritdoc />
    public byte Access(ushort address, bool write, byte dataBus)
    {
        int offset = address & 0xFFF;
        if (offset >= PortsEnd)
        {
            return _board.Access(address, write, dataBus);
        }

        if (offset < ReadPort)
        {
            _ram[offset - WritePort] = dataBus;
            return dataBus;
        }

        return write ? dataBus : _ram[offset - ReadPort];
    }

    /// <inheritdoc />
    public byte Peek(ushort address)
    {
        int offset = address & 0xFFF;
        if (offset >= PortsEnd)
        {
            return _board.Peek(address);
        }

        return _ram[offset < ReadPort ? offset - WritePort : offset - ReadPort];
    }

    /// <inheritdoc />
    public void PowerOn()
    {
        _board.PowerOn();
        Array.Clear(_ram);
    }
}
