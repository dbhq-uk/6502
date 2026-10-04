// The test oracle: the per-cycle implementation as it stood at commit 2876852, before task 6b
// made the machine's chips run lazily. Kept unchanged apart from its names, and never used by
// the machine; the equivalence tests run it side by side with the real one and compare them.
// Task 7 added the one change: the per-character ReferenceCrtc6845 in place of the CRTC stub,
// ticked in every cycle its clock is due, with its VSYNC on the system VIA's CA1. Task 8 added
// the per-character ReferenceVideoUla in place of the ULA stub, fed in every character clock
// with the byte the CRTC's address fetches from RAM in that cycle. Task 12 added the per-cycle
// ReferenceFdc8271 at $FE80-$FE9F, ticked in every cycle, with its INT on the CPU's NMI line.
using Dbhq.Cpu6502;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

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
/// The chips arrive task by task and each is called from <see cref="Tick"/> and from
/// the decode in <see cref="ReadSheila"/> and <see cref="WriteSheila"/>. The two VIAs
/// run on the 1 MHz clock, so they tick on even values of <see cref="Cycles"/>; a
/// stretched access always ends on one, so every VIA access has a tick before it in
/// the same 1 MHz cycle. Their IRQ outputs share the 6502's IRQ line (via.md section
/// 1.11), and the line is set from them at the end of each bus cycle, after its
/// access, never between a tick and the access that follows it.
/// </para>
/// </remarks>
public sealed class ReferenceBbcBus : IBus
{
    private readonly byte[] _ram = new byte[0x8000];
    private readonly byte[] _os;
    private readonly byte[] _basic;
    private readonly byte[] _dfs;

    public ReferenceBbcBus(BbcRoms roms, BbcOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(roms);
        _os = roms.Os;
        _basic = roms.Basic;
        _dfs = roms.Dfs;
        Keyboard = new BbcKeyboard((options ?? new BbcOptions()).StartupMode);
        SystemVia = new ReferenceSystemVia(Keyboard);
        UserVia = new ReferenceUserVia();
    }

    /// <summary>The keyboard, with its start-up links set from the options.</summary>
    public BbcKeyboard Keyboard { get; }

    /// <summary>The system VIA at $FE40-$FE5F.</summary>
    public ReferenceSystemVia SystemVia { get; }

    /// <summary>The user VIA at $FE60-$FE7F.</summary>
    public ReferenceUserVia UserVia { get; }

    /// <summary>The CRTC at $FE00-$FE07, one character at a time.</summary>
    public ReferenceCrtc6845 Crtc { get; } = new();

    /// <summary>The video ULA at $FE20-$FE2F, one character at a time.</summary>
    public ReferenceVideoUla VideoUla { get; } = new();

    /// <summary>The 8271 at $FE80-$FE9F, one cycle at a time.</summary>
    public ReferenceFdc8271 Fdc { get; } = new();

    /// <summary>The CPU whose IRQ line the VIAs drive, and whose NMI line the 8271 drives.</summary>
    public Cpu? Cpu { get; set; }

    /// <summary>The 6502's IRQ line as the chips drive it: the OR of both VIAs. The ACIA is absent.</summary>
    public bool Irq => SystemVia.Irq || UserVia.Irq;

    /// <summary>2 MHz CPU cycles since power on, stretch cycles included.</summary>
    public long Cycles { get; private set; }

    /// <summary>The paged ROM latch, 0 to 15. Slot 15 is BASIC and slot 14 the DFS.</summary>
    public int RomSlot { get; private set; }

    public byte Read(ushort address)
    {
        Stretch(address);
        Cycle();
        byte value = Decode(address, sideEffects: true);
        DriveIrq();
        return value;
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
        DriveIrq();
    }

    /// <summary>
    /// The power-on reset: both VIAs. The latch IC32 and the ROM latch are not reset
    /// (via.md section 3(a), bus.md section 6 item 4).
    /// </summary>
    public void PowerOnReset()
    {
        SystemVia.Reset();
        UserVia.Reset();
        ResetCrtc();
        VideoUla.PowerOn();
        Fdc.PowerOn();
        DriveIrq();
    }

    /// <summary>
    /// What BREAK resets here: the user VIA, and not the system VIA, which only the power-on
    /// circuit resets, so the OS can tell the two apart from its IER (bus.md section 5).
    /// </summary>
    public void BreakReset()
    {
        UserVia.Reset();
        ResetCrtc();
        DriveIrq();
    }

    private void ResetCrtc()
    {
        bool before = Crtc.VSync;
        Crtc.Reset();
        if (Crtc.VSync != before)
        {
            SystemVia.VsyncInput = Crtc.VSync;
        }

        VideoUla.CrtcReset(Cycles, Crtc.OddField, Crtc.InterlaceOn, Crtc.RasterAddress, Crtc.VSync);
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

    /// <summary>One CPU cycle for every chip but the CPU.</summary>
    private void Tick()
    {
        Fdc.Tick();
        if ((Cycles & 1) == 0)
        {
            SystemVia.Tick();
            UserVia.Tick();
        }

        // The CRTC's character clock: every cycle at 2 MHz, every even one at 1 MHz, as the video
        // ULA's control bit 4 says. A VSYNC edge reaches CA1 in the same cycle, after the VIA's tick.
        if ((VideoUla.Control & 0x10) != 0 || (Cycles & 1) == 0)
        {
            bool before = Crtc.VSync;
            Crtc.Tick();
            if (Crtc.VSync != before)
            {
                SystemVia.VsyncInput = Crtc.VSync;
            }

            int ra = Crtc.RasterAddress;
            byte fetched = _ram[ReferenceVideoUla.ScreenAddress(Crtc.MemoryAddress, ra, SystemVia.ScreenStartLatch)];
            VideoUla.Clock(Cycles, Crtc.LineStarted, Crtc.Line, Crtc.OddField, Crtc.InterlaceOn, fetched, Crtc.DisplayEnable, Crtc.Cursor, ra, Crtc.VSync);
        }
    }

    /// <summary>
    /// Sets the CPU's IRQ line from the chips. Called once a cycle's access is over, because a
    /// VIA's IRQ can rise in the tick that starts a cycle and fall at an acknowledge in it.
    /// </summary>
    private void DriveIrq()
    {
        if (Cpu is not null)
        {
            Cpu.Irq = Irq;
            Cpu.Nmi = Fdc.Interrupt;
        }
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
        <= 0x07 => (offset & 1) == 0 ? (byte)0 : Crtc.ReadData(),

        // A4 is not decoded, so each VIA's sixteen registers repeat in the upper half of its
        // block (bus.md section 1c).
        >= 0x40 and <= 0x5F => SystemVia.Read(offset & 0x0F),
        >= 0x60 and <= 0x7F => UserVia.Read(offset & 0x0F),
        >= 0x80 and <= 0x9F => (offset & 7) switch
        {
            0 => Fdc.ReadStatus(),
            1 => Fdc.ReadResult(),
            2 or 3 => AbsentSheila(offset),
            _ => Fdc.ReadData(),
        },
        _ => AbsentSheila(offset),
    };

    /// <summary>A write to SHEILA by offset. Each chip adds a case; the ROM latch is the first.</summary>
    private void WriteSheila(int offset, byte value)
    {
        switch (offset)
        {
            case <= 0x07 when (offset & 1) == 0:
                Crtc.WriteAddress(value);
                break;
            case <= 0x07:
                Crtc.WriteData(value);
                break;
            case >= 0x20 and <= 0x2F when (offset & 1) == 0:
                VideoUla.WriteControl(value);
                break;
            case >= 0x20 and <= 0x2F:
                VideoUla.WritePalette(value);
                break;
            case >= 0x30 and <= 0x3F:
                // Write only, and the whole block of sixteen is the one latch (S1 s.17, s.21).
                RomSlot = value & 0x0F;
                break;
            case >= 0x40 and <= 0x5F:
                SystemVia.Write(offset & 0x0F, value);
                break;
            case >= 0x60 and <= 0x7F:
                UserVia.Write(offset & 0x0F, value);
                break;
            case >= 0x80 and <= 0x9F when (offset & 7) == 0:
                Fdc.WriteCommand(value);
                break;
            case >= 0x80 and <= 0x9F when (offset & 7) == 1:
                Fdc.WriteParameter(value);
                break;
            case >= 0x80 and <= 0x9F when (offset & 7) == 2:
                Fdc.WriteReset(value);
                break;
            case >= 0x80 and <= 0x9F when (offset & 7) >= 4:
                Fdc.WriteData(value);
                break;
        }
    }

    /// <summary>A read with no side effect. Reading SHEILA changes some chips, so each chip says what its peek is.</summary>
    private byte PeekSheila(int offset) => offset switch
    {
        <= 0x07 => (offset & 1) == 0 ? (byte)0 : Crtc.ReadData(),
        >= 0x40 and <= 0x5F => SystemVia.Peek(offset & 0x0F),
        >= 0x60 and <= 0x7F => UserVia.Peek(offset & 0x0F),
        >= 0x80 and <= 0x9F => (offset & 7) switch
        {
            0 or 1 => Fdc.Peek(offset & 7),
            2 or 3 => AbsentSheila(offset),
            _ => Fdc.Peek(4),
        },
        _ => AbsentSheila(offset),
    };

    private static byte AbsentSheila(int offset)
    {
        ushort address = (ushort)(0xFE00 | offset);
        return IsSlow(address) ? (byte)0x00 : (byte)0xFE;
    }
}
