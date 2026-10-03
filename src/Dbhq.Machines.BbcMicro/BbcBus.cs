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
/// The chips arrive task by task and each is reached from the decode in
/// <see cref="ReadSheila"/> and <see cref="WriteSheila"/>. The two VIAs run on the 1 MHz
/// clock, so they tick on even values of <see cref="Cycles"/>; a stretched access always ends
/// on one, so every VIA access has a tick before it in the same 1 MHz cycle. Their IRQ outputs
/// share the 6502's IRQ line (via.md section 1.11), and the line is set from them at the end of
/// a bus cycle, after its access, never between a tick and the access that follows it.
/// </para>
/// <para>
/// <b>The chips run lazily, and the bus does no work for them on most cycles</b> (task 6b).
/// Each chip reads the time from the machine's clock and does the cycles it owes when anything
/// looks at it, so a VIA access sees exactly the ticks before it, as if every cycle had ticked
/// it. The CPU's IRQ line can only change when a chip is accessed, when it is changed from
/// outside, or on a cycle the chip itself names in advance (a timer running out, a flag, a key):
/// that is the event horizon, <see cref="BbcClock.NextEvent"/>. After each access the bus
/// compares the cycle count with it, and only when it is reached, or a chip was accessed, does
/// it bring the chips up to date, set the IRQ line and ask each chip for its next event
/// (<see cref="Service"/>). A chip added later joins in the same three places: the decode, the
/// minimum in <see cref="Service"/>, and the resets.
/// </para>
/// </remarks>
public sealed class BbcBus : IBus
{
    private readonly byte[] _ram = new byte[0x8000];
    private readonly byte[] _os;
    private readonly byte[] _basic;
    private readonly byte[] _dfs;

    // What an empty paged ROM slot reads: the high byte of each address, so a read of the slot is
    // the same array lookup as a fitted ROM. See ReadPagedRom for why.
    private static readonly byte[] EmptySlot = MakeEmptySlot();

    // The paged ROM the latch chose: BASIC, the DFS or EmptySlot. Set only when the latch is.
    private byte[] _paged = EmptySlot;
    private readonly Crtc6845Stub _crtc = new();
    private readonly VideoUlaStub _videoUla = new();
    private readonly BbcClock _clock = new();
    private Cpu? _cpu;

    public BbcBus(BbcRoms roms, BbcOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(roms);
        _os = roms.Os;
        _basic = roms.Basic;
        _dfs = roms.Dfs;
        Keyboard = new BbcKeyboard((options ?? new BbcOptions()).StartupMode);
        SystemVia = new SystemVia(Keyboard, _clock);
        UserVia = new UserVia(_clock);
    }

    /// <summary>The keyboard, with its start-up links set from the options.</summary>
    public BbcKeyboard Keyboard { get; }

    /// <summary>The system VIA at $FE40-$FE5F.</summary>
    public SystemVia SystemVia { get; }

    /// <summary>The user VIA at $FE60-$FE7F.</summary>
    public UserVia UserVia { get; }

    /// <summary>The CPU whose IRQ line the VIAs drive. The line is set at the end of the next access.</summary>
    public Cpu? Cpu
    {
        get => _cpu;
        set
        {
            _cpu = value;
            _clock.WakeAt(_clock.Cycles + 1);
        }
    }

    /// <summary>The 6502's IRQ line as the chips drive it: the OR of both VIAs. The ACIA is absent.</summary>
    public bool Irq => SystemVia.Irq || UserVia.Irq;

    /// <summary>2 MHz CPU cycles since power on, stretch cycles included.</summary>
    public long Cycles => _clock.Cycles;

    /// <summary>The paged ROM latch, 0 to 15. Slot 15 is BASIC and slot 14 the DFS.</summary>
    public int RomSlot { get; private set; }

    public byte Read(ushort address)
    {
        // Memory first, which is nearly every access: one cycle, never stretched, and no chip
        // sees it, so the only other work is the look at the event horizon.
        BbcClock clock = _clock;
        byte value;
        if (address < 0x8000)
        {
            value = _ram[address];
        }
        else if (address < 0xC000)
        {
            value = _paged[address - 0x8000];
        }
        else if (address < 0xFC00 || address >= 0xFF00)
        {
            value = _os[address - 0xC000];
        }
        else
        {
            Stretch(address);
            clock.Cycles++;
            value = address < 0xFE00 ? (byte)0xFF : ReadSheila(address & 0xFF);
            if (clock.Cycles >= clock.NextEvent)
            {
                Service();
            }
            return value;
        }

        if (++clock.Cycles >= clock.NextEvent)
        {
            Service();
        }
        return value;
    }

    public void Write(ushort address, byte value)
    {
        BbcClock clock = _clock;
        if (address < 0x8000)
        {
            _ram[address] = value;
        }
        else if (address >= 0xFC00 && address < 0xFF00)
        {
            Stretch(address);
            if (address >= 0xFE00)
            {
                clock.Cycles++;
                WriteSheila(address & 0xFF, value);
                if (clock.Cycles >= clock.NextEvent)
                {
                    Service();
                }
                return;
            }
        }

        // $8000-$FBFF and $FF00-$FFFF are ROM, and FRED and JIM have nothing fitted:
        // a write goes nowhere.
        if (++clock.Cycles >= clock.NextEvent)
        {
            Service();
        }
    }

    /// <summary>
    /// The power-on reset: both VIAs. The latch IC32 and the ROM latch are not reset
    /// (via.md section 3(a), bus.md section 6 item 4).
    /// </summary>
    public void PowerOnReset()
    {
        SystemVia.Reset();
        UserVia.Reset();
        Service();
    }

    /// <summary>
    /// What BREAK resets here: the user VIA, and not the system VIA, which only the power-on
    /// circuit resets, so the OS can tell the two apart from its IER (bus.md section 5).
    /// </summary>
    public void BreakReset()
    {
        UserVia.Reset();
        Service();
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

    /// <summary>
    /// The event horizon reached, or a chip accessed: every chip catches up to now, the CPU's IRQ
    /// line is set from them, and the next look is put at the earliest cycle any chip names.
    /// </summary>
    private void Service()
    {
        _clock.NextEvent = Math.Min(SystemVia.NextEventCycle, UserVia.NextEventCycle);
        if (_cpu is not null)
        {
            _cpu.Irq = SystemVia.Irq || UserVia.Irq;
        }
    }

    /// <summary>A chip was accessed in this cycle, so the bus looks at the chips at its end.</summary>
    private void ChipAccessed() => _clock.WakeAt(_clock.Cycles);

    private void Stretch(ushort address)
    {
        if (!IsSlow(address))
        {
            return;
        }

        // The chips' ticks in these cycles are done when the chips are next looked at.
        _clock.Cycles += 1 + (_clock.Cycles & 1);
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

    private byte ReadPagedRom(ushort address) => _paged[address - 0x8000];

    /// <summary>The ROM in the slot the latch chose.</summary>
    private byte[] PagedRom(int slot) => slot switch
    {
        15 => _basic,
        14 => _dfs,

        // An empty slot: the last value on the bus, which is the high byte of the
        // address just fetched. One poster on one machine measured the same for an
        // absent fast device; for an empty ROM slot it is an assumption (bus.md s6, item 3).
        _ => EmptySlot,
    };

    private static byte[] MakeEmptySlot()
    {
        var slot = new byte[BbcRoms.RomSize];
        for (int i = 0; i < slot.Length; i++)
        {
            slot[i] = (byte)((0x8000 + i) >> 8);
        }
        return slot;
    }

    /// <summary>
    /// A read from SHEILA, $FE00-$FEFF, by offset. Each chip adds a case. A device
    /// that is not fitted answers as the bus does: the high byte of the address for a
    /// fast device ($FE) and $00 for a slow one, whose weak pull-downs have time
    /// to discharge (bus.md section 1d).
    /// </summary>
    private byte ReadSheila(int offset) => offset switch
    {
        <= 0x07 => _crtc.Read(offset & 1),

        // A4 is not decoded, so each VIA's sixteen registers repeat in the upper half of its
        // block (bus.md section 1c).
        >= 0x40 and <= 0x5F => ReadVia(SystemVia, offset),
        >= 0x60 and <= 0x7F => ReadVia(UserVia, offset),
        _ => AbsentSheila(offset),
    };

    private byte ReadVia(Via6522 via, int offset)
    {
        byte value = via.Read(offset & 0x0F);
        ChipAccessed();
        return value;
    }

    /// <summary>A write to SHEILA by offset. Each chip adds a case; the ROM latch is the first.</summary>
    private void WriteSheila(int offset, byte value)
    {
        switch (offset)
        {
            case <= 0x07:
                _crtc.Write(offset & 1, value);
                break;
            case >= 0x20 and <= 0x2F:
                _videoUla.Write(offset & 1, value);
                break;
            case >= 0x30 and <= 0x3F:
                // Write only, and the whole block of sixteen is the one latch (S1 s.17, s.21).
                RomSlot = value & 0x0F;
                _paged = PagedRom(RomSlot);
                break;
            case >= 0x40 and <= 0x5F:
                SystemVia.Write(offset & 0x0F, value);
                ChipAccessed();
                break;
            case >= 0x60 and <= 0x7F:
                UserVia.Write(offset & 0x0F, value);
                ChipAccessed();
                break;
        }
    }

    /// <summary>A read with no side effect. Reading SHEILA changes some chips, so each chip says what its peek is.</summary>
    private byte PeekSheila(int offset) => offset switch
    {
        <= 0x07 => _crtc.Read(offset & 1),
        >= 0x40 and <= 0x5F => SystemVia.Peek(offset & 0x0F),
        >= 0x60 and <= 0x7F => UserVia.Peek(offset & 0x0F),
        _ => AbsentSheila(offset),
    };

    private static byte AbsentSheila(int offset)
    {
        ushort address = (ushort)(0xFE00 | offset);
        return IsSlow(address) ? (byte)0x00 : (byte)0xFE;
    }

    /// <summary>
    /// The 6845 CRTC's registers and nothing else: no counters, no sync, no vsync on CA1. It
    /// holds what the OS writes so the boot can run before the video exists. Even addresses in
    /// $FE00-$FE07 are the address register and odd ones the data register (video.md s1.2).
    /// </summary>
    /// <remarks>
    /// Only R12 to R17 read back on the HD6845S (video.md s1.3); R12 and R14 are six bits, as
    /// the 14-bit addresses they hold need. R16 and R17, the light pen, have no strobe and read
    /// 0. Every other read is a write-only register, which reads as an absent slow device does,
    /// $00 (bus.md s1d and s6 item 3; the OS never reads one).
    /// </remarks>
    private sealed class Crtc6845Stub
    {
        private readonly byte[] _registers = new byte[18];
        private int _address;

        public void Write(int rs, byte value)
        {
            if (rs == 0)
            {
                _address = value & 0x1F;
            }
            else if (_address < 16)
            {
                // R16 and R17 are read only, and 18 to 31 are not registers.
                _registers[_address] = value;
            }
        }

        public byte Read(int rs) => rs == 1 && _address is >= 12 and <= 17
            ? (byte)(_registers[_address] & ((_address & 1) == 0 ? 0x3F : 0xFF))
            : (byte)0x00;
    }

    /// <summary>
    /// The video ULA's two write-only registers, control at $FE20 and palette at $FE21,
    /// mirrored through $FE2F with A0 choosing (video.md s2.1 to s2.3). It holds what the OS
    /// writes and draws nothing. A read is Econet's INTON, which is not fitted, so it reads as
    /// an absent fast device does (bus.md s1c and s1d), and the bus answers it.
    /// </summary>
    private sealed class VideoUlaStub
    {
        private readonly byte[] _palette = new byte[16];

        public byte Control { get; private set; }

        public void Write(int a0, byte value)
        {
            if (a0 == 0)
            {
                Control = value;
            }
            else
            {
                _palette[value >> 4] = (byte)(value & 0x0F);
            }
        }
    }
}
