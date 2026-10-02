using Dbhq.Cpu6502;

namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The Model B's address decoding, and its clock: each read or write is one 2 MHz
/// CPU cycle, and a slow device makes it longer.
/// </summary>
/// <remarks>
/// <para>
/// The memory map (fact sheet <c>bus.md</c> section 1):
/// </para>
/// <list type="table">
/// <item><term>$0000-$7FFF</term><description>32 KB of RAM, with no aliasing: the OS tells a Model A by aliasing at $4000</description></item>
/// <item><term>$8000-$BFFF</term><description>the paged ROM that the latch at $FE30-$FE3F chose; writes are ignored</description></item>
/// <item><term>$C000-$FBFF</term><description>the operating system ROM</description></item>
/// <item><term>$FC00-$FCFF</term><description>FRED, the 1 MHz bus</description></item>
/// <item><term>$FD00-$FDFF</term><description>JIM, the 1 MHz bus</description></item>
/// <item><term>$FE00-$FEFF</term><description>SHEILA, the on-board chips</description></item>
/// <item><term>$FF00-$FFFF</term><description>the operating system ROM again, with the vectors</description></item>
/// </list>
/// <para>
/// The 1 MHz stretch (section 2b). The VIAs, CRTC, ACIA, serial ULA and ADC, and
/// the 1 MHz bus, run on a 1 MHz clock. A CPU cycle that addresses one is held until
/// that clock's edge: one extra cycle if the cycle starts on an even count of CPU
/// cycles, two if it starts on an odd one, so <c>wait = 1 + (T and 1)</c>. The
/// stretch is judged for each bus cycle by that cycle's own address, so a dummy
/// read or the first write of a read-modify-write is stretched or not on its own
/// merits. The extra cycles come first and the access happens at the end of the
/// last one. Which of the two phases the machine starts in is not known
/// (section 6), so the count begins at zero, which is even.
/// </para>
/// <para>
/// Nothing yet ticks: the chips arrive task by task and each is called from
/// <see cref="Tick"/> and from the decode in <see cref="ReadSheila"/> and
/// <see cref="WriteSheila"/>.
/// </para>
/// </remarks>
public class BbcBus : IBus
{
    private readonly byte[] _ram = new byte[0x8000];
    private readonly byte[] _os;
    private readonly byte[] _basic;
    private readonly byte[] _dfs;

    public BbcBus(BbcRoms roms)
    {
        ArgumentNullException.ThrowIfNull(roms);
        _os = roms.Os;
        _basic = roms.Basic;
        _dfs = roms.Dfs;
    }

    /// <summary>2 MHz CPU cycles since power on, stretch cycles included.</summary>
    public long Cycles { get; private set; }

    /// <summary>The paged ROM latch, 0 to 15. Slot 15 is BASIC and slot 14 the DFS.</summary>
    public int RomSlot { get; private set; }

    public byte Read(ushort address)
    {
        Stretch(address);
        Cycle();
        return Decode(address, sideEffects: true);
    }

    public void Write(ushort address, byte value)
    {
        Stretch(address);
        Cycle();
        switch (address >> 12)
        {
            case < 8:
                _ram[address] = value;
                break;
            case 0xF when address >= 0xFE00 && address < 0xFF00:
                WriteSheila(address & 0xFF, value);
                break;
        }

        // $8000-$FBFF and $FF00-$FFFF are ROM, and FRED and JIM have nothing fitted:
        // a write goes nowhere.
    }

    /// <summary>Reads memory without a bus cycle: for tests and debuggers, never for the CPU.</summary>
    public byte Peek(ushort address) => Decode(address, sideEffects: false);

    /// <summary>Loads RAM without a bus cycle: for tests.</summary>
    public void PokeRam(ushort address, byte value)
    {
        if (address >= _ram.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(address), address, "RAM ends at $7FFF.");
        }

        _ram[address] = value;
    }

    /// <summary>One CPU cycle for every chip but the CPU. Empty until the chips are added.</summary>
    protected internal virtual void Tick()
    {
    }

    /// <summary>
    /// The one place a cycle is counted and the other chips are clocked, so no
    /// cycle exists without an access and no access without a cycle.
    /// </summary>
    private void Cycle()
    {
        Cycles++;
        Tick();
    }

    private void Stretch(ushort address)
    {
        if (!IsSlow(address))
        {
            return;
        }

        int wait = 1 + (int)(Cycles & 1);
        for (int i = 0; i < wait; i++)
        {
            Cycle();
        }
    }

    /// <summary>
    /// True for an address on the 1 MHz clock: FRED and JIM, and every SHEILA device
    /// except the video ULA ($FE20-$FE2F), the ROM latch ($FE30-$FE3F), the 8271
    /// ($FE80-$FE9F), the ADLC ($FEA0-$FEBF) and the Tube ($FEE0-$FEFF). The station ID
    /// at $FE18-$FE1F is in no source's list; it is taken as slow (bus.md section 6, item 2).
    /// </summary>
    private static bool IsSlow(ushort address)
    {
        if (address < 0xFC00 || address > 0xFEFF)
        {
            return false;
        }

        if (address < 0xFE00)
        {
            return true;
        }

        int offset = address & 0xFF;
        return offset < 0x20 || (offset >= 0x40 && offset < 0x80) || (offset >= 0xC0 && offset < 0xE0);
    }

    private byte Decode(ushort address, bool sideEffects)
    {
        if (address < 0x8000)
        {
            return _ram[address];
        }

        if (address < 0xC000)
        {
            return ReadPagedRom(address);
        }

        if (address >= 0xFC00 && address < 0xFF00)
        {
            return address < 0xFE00 ? (byte)0xFF : sideEffects ? ReadSheila(address & 0xFF) : PeekSheila(address & 0xFF);
        }

        return _os[address - 0xC000];
    }

    private byte ReadPagedRom(ushort address) => RomSlot switch
    {
        15 => _basic[address - 0x8000],
        14 => _dfs[address - 0x8000],

        // An empty slot: the last value on the bus, which is the high byte of the
        // address just fetched. One poster on one machine measured the same for an
        // absent fast device; for an empty ROM slot it is an assumption (bus.md s6, item 3).
        _ => (byte)(address >> 8),
    };

    /// <summary>
    /// A read from SHEILA, $FE00-$FEFF, by offset. Each chip adds a case. A device
    /// that is not fitted answers as the bus does: the high byte of the address for a
    /// fast device ($FE) and $00 for a slow one, whose weak pull-downs have time
    /// to discharge (bus.md section 1d).
    /// </summary>
    private byte ReadSheila(int offset) => offset switch
    {
        _ => AbsentSheila(offset),
    };

    /// <summary>A write to SHEILA by offset. Each chip adds a case; the ROM latch is the first.</summary>
    private void WriteSheila(int offset, byte value)
    {
        switch (offset)
        {
            case >= 0x30 and <= 0x3F:
                // Write only, and the whole block of sixteen is the one latch (S1 s.17, s.21).
                RomSlot = value & 0x0F;
                break;
        }
    }

    /// <summary>A read with no side effect. Reading SHEILA changes some chips, so each chip says what its peek is.</summary>
    private byte PeekSheila(int offset) => offset switch
    {
        _ => AbsentSheila(offset),
    };

    private static byte AbsentSheila(int offset)
    {
        ushort address = (ushort)(0xFE00 | offset);
        return IsSlow(address) ? (byte)0x00 : (byte)0xFE;
    }
}
