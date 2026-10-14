namespace Dbhq.Machines.Atari2600.Boards;

/// <summary>
/// FA, CBS RAM Plus: 12K in three 4K banks selected at <c>$1FF8</c> to <c>$1FFA</c> only when data
/// bit D0 is 1, and 256 bytes of RAM written at <c>$1000-$10FF</c> and read at <c>$1100-$11FF</c>
/// in every bank.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/cartridge.md</c> section 7.
/// </para>
/// <list type="bullet">
/// <item><description><b>The D0 gate</b> follows the sheet's choice (7.2): CBS's patent, US 4,485,457, switches only with D0 high, and the suite's <c>bank-fa</c> asserts it; Horton and Korth say any access switches. D1 to D7 are ignored. A write gates on the byte written. A read gates on the hotspot's byte in the bank showing before the access, and returns that byte [guessing - verify]: the gate decides late in the cycle, so section 4's rule for F8, F6 and F4 is not extended to FA. The choice is in <c>docs/known-differences.md</c>.</description></item>
/// <item><description><b>The RAM</b> ports are chosen by the address alone, as the Superchip's are (7.1), so a read of the write port writes the byte left on the data bus into the cell, and a write to the read port stores nothing. The RAM is not banked, and it is clear at power on (a choice).</description></item>
/// <item><description>The board powers on in its last bank, bank 2, a choice (section 8).</description></item>
/// </list>
/// </remarks>
public sealed class Fa : IBoard, IReportsState
{
    /// <summary>The RAM's size in bytes.</summary>
    public const int RamSize = 256;

    private const int BankSize = 0x1000;
    private const int Banks = 3;
    private const int FirstHotspot = 0xFF8;
    private const int WritePort = 0x000;
    private const int ReadPort = 0x100;
    private const int PortsEnd = 0x200;

    private readonly byte[] _image;
    private readonly byte[] _ram = new byte[RamSize];
    private int _base;

    /// <summary>A board over <paramref name="image"/>, 12288 bytes, which it keeps and never changes.</summary>
    internal Fa(byte[] image)
    {
        _image = image;
        PowerOn();
    }

    /// <inheritdoc />
    public int Bank { get; private set; }

    /// <inheritdoc />
    public byte Access(ushort address, bool write, byte dataBus)
    {
        int offset = address & (BankSize - 1);
        if (offset < PortsEnd)
        {
            if (offset < ReadPort)
            {
                _ram[offset - WritePort] = dataBus;
                return dataBus;
            }

            return write ? dataBus : _ram[offset - ReadPort];
        }

        byte data = write ? dataBus : _image[_base + offset];
        int hotspot = offset - FirstHotspot;
        if ((uint)hotspot < Banks && (data & 0x01) != 0)
        {
            Select(hotspot);
        }

        return data;
    }

    /// <inheritdoc />
    public byte Peek(ushort address)
    {
        int offset = address & (BankSize - 1);
        if (offset < PortsEnd)
        {
            return _ram[offset < ReadPort ? offset - WritePort : offset - ReadPort];
        }

        return _image[_base + offset];
    }

    /// <inheritdoc />
    public void PowerOn()
    {
        Select(Banks - 1);
        Array.Clear(_ram);
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Skip(nameof(_image), StateReport.Image);
        sink.Add(nameof(_ram), _ram);
        sink.Add(nameof(_base), _base);
        sink.Add(nameof(Bank), Bank);
    }

    private void Select(int bank)
    {
        Bank = bank;
        _base = bank * BankSize;
    }
}
