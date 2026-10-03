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
/// <para>
/// <b>The CRTC</b> (task 7) is the first chip to join that way. Its character clock is every CPU
/// cycle or every even one, as the video ULA's control bit 4 says, and it names as its events
/// each VSYNC edge and each frame start. VSYNC is the system VIA's CA1: the CRTC hands each edge
/// to the VIA with the cycle it happened in, and the VIA brings the CRTC up to date before it
/// catches up itself, so it never passes a cycle with an edge still to come.
/// </para>
/// <para>
/// <b>The video ULA</b> (task 8) reads the CRTC's outputs rather than driving its inputs, so it
/// joins the other way round: it draws a line at a time, at each line's last character, and that
/// cycle is its event. Line ends come every 64 microseconds and can never move the IRQ line, so
/// they are kept apart from the chips' events (<see cref="BbcClock.ChipEvent"/>): at a line end
/// the bus has the ULA draw and does nothing else. Before any write that changes what the ULA
/// draws the ULA draws up to the write's cycle first: its own registers do it themselves (and the
/// control register sets the CRTC's clock), the system VIA reports a change to the latch's
/// screen start bits before making it, and the CRTC brings the ULA up to any cycle before
/// moving there itself, so a direct call to any of the chips keeps them agreeing.
/// </para>
/// <para>
/// <b>The sound chip</b> (task 11) joins in none of the three places. Its READY line is not
/// wired (via.md s4.1), so nothing it does can reach the CPU and it names no event; it is not
/// ticked either. The system VIA hands it each byte when latch bit 0 falls, and it catches up to
/// that cycle before taking it; its buffer catches it up to now whenever the buffer is read.
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
    private readonly BbcClock _clock = new();
    private Cpu? _cpu;

    // The video ULA's next line end, as it was when the bus last looked: a write that moves it (a
    // CRTC register, the ULA's clock bit) is a chip access, so the bus looks again at its end.
    private long _lineEnd;

    public BbcBus(BbcRoms roms, BbcOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(roms);
        _os = roms.Os;
        _basic = roms.Basic;
        _dfs = roms.Dfs;
        options ??= new BbcOptions();
        Keyboard = new BbcKeyboard(options.StartupMode);
        SystemVia = new SystemVia(Keyboard, _clock);
        UserVia = new UserVia(_clock);

        // The CRTC's VSYNC is the system VIA's CA1. The VIA brings the CRTC up to date before
        // itself, and the CRTC hands it each edge with the cycle it happened in.
        Crtc = new Crtc6845(_clock);
        Crtc.VsyncDriven += SystemVia.SetVsyncAt;
        SystemVia.VsyncSource = Crtc;

        // The video ULA reads the CRTC's outputs, screen memory and the system VIA's two screen
        // start latch bits, and draws a line at a time.
        VideoUla = new VideoUla(_clock, Crtc, _ram, SystemVia);

        // The sound chip takes the byte on port A when latch bit 0 falls. It runs lazily and is
        // never ticked: nothing it does can reach the CPU, so it has no place in Service().
        Sound = new SoundBuffer(options.SampleRate);
        SoundChip = new Sn76489(_clock, Sound);
        SystemVia.SoundWrite += SoundChip.Write;
    }

    /// <summary>The keyboard, with its start-up links set from the options.</summary>
    public BbcKeyboard Keyboard { get; }

    /// <summary>The system VIA at $FE40-$FE5F.</summary>
    public SystemVia SystemVia { get; }

    /// <summary>The user VIA at $FE60-$FE7F.</summary>
    public UserVia UserVia { get; }

    /// <summary>
    /// The 6845 CRTC at $FE00-$FE07. Its character clock is 2 MHz or 1 MHz as the video ULA's
    /// control bit 4 says, and its VSYNC drives the system VIA's CA1.
    /// </summary>
    public Crtc6845 Crtc { get; }

    /// <summary>The video ULA at $FE20-$FE2F, which draws the picture.</summary>
    public VideoUla VideoUla { get; }

    /// <summary>The picture, drawn up to now when it is read.</summary>
    public Framebuffer Screen => VideoUla.Screen;

    /// <summary>The SN76489, written through the system VIA (via.md s4.1).</summary>
    public Sn76489 SoundChip { get; }

    /// <summary>The sound chip's samples, made up to now when they are read.</summary>
    public SoundBuffer Sound { get; }

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
    /// The power-on reset: both VIAs and the CRTC, the video ULA's registers to zero and the sound
    /// chip to its power-on state. The latch IC32 and the ROM latch are not reset (via.md section
    /// 3(a), bus.md section 6 item 4).
    /// </summary>
    /// <remarks>
    /// The CRTC's /RES is taken to be on RST, the reset that power on and BREAK both make and
    /// that the hardware guide says goes to all the circuitry but the system VIA (S9 s3.14);
    /// no schematic was read for IC2's pin. A reset keeps its registers and drops VSYNC. The video
    /// ULA has no reset pin (video.md s2.1), and what its registers hold at power on is not known:
    /// the model takes zero, which also puts the CRTC on the 1 MHz clock.
    /// </remarks>
    public void PowerOnReset()
    {
        SystemVia.Reset();
        UserVia.Reset();
        Crtc.Reset();
        VideoUla.PowerOn();
        SoundChip.PowerOn();
        ChipAccessed();
        Service();
    }

    /// <summary>
    /// What BREAK resets here: the user VIA and the CRTC, and not the system VIA, which only the
    /// power-on circuit resets, so the OS can tell the two apart from its IER (bus.md section 5).
    /// Nor the video ULA, which has no reset pin; the OS writes both its registers again in the mode
    /// change every BREAK makes. Nor the sound chip, which has none either; the OS silences it at
    /// every reset (via.md s4.7).
    /// </summary>
    public void BreakReset()
    {
        UserVia.Reset();
        Crtc.Reset();
        ChipAccessed();
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
    /// <remarks>
    /// The order matters. The CRTC goes first, because its VSYNC is the system VIA's CA1: an edge
    /// that is due must reach the VIA, in its own cycle, before the VIA catches up past it and is
    /// asked for its IRQ line and its next event. (The VIA would ask the CRTC itself, through its
    /// <c>SyncInputs</c>, but the bus does not rely on that here.)
    /// </remarks>
    private void Service()
    {
        BbcClock clock = _clock;
        long now = clock.Cycles;
        if (now >= clock.ChipEvent)
        {
            // The ULA's lines first: Crtc.SyncIfDue may take the CRTC to now, and the ULA must
            // have asked about every line end before then. A write may have moved the line end.
            _lineEnd = VideoUla.RenderLinesTo(now);
            Crtc.SyncIfDue();
            clock.ChipEvent = Math.Min(Math.Min(SystemVia.NextEventCycle, UserVia.NextEventCycle), Crtc.NextEventCycle);
            if (_cpu is not null)
            {
                _cpu.Irq = SystemVia.Irq || UserVia.Irq;
            }
        }
        else if (now >= _lineEnd)
        {
            _lineEnd = VideoUla.RenderLinesTo(now);
        }

        clock.NextEvent = Math.Min(clock.ChipEvent, _lineEnd);
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
        <= 0x07 => ReadCrtc(offset),

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
                // Even addresses are the address register and odd ones the data register (video.md s1.2).
                if ((offset & 1) == 0)
                {
                    Crtc.WriteAddress(value);
                }
                else
                {
                    Crtc.WriteData(value);
                    ChipAccessed();
                }
                break;
            case >= 0x20 and <= 0x2F:
                // A0 chooses the control register or the palette (video.md s2.1). The ULA draws up
                // to this cycle with what it had before taking the write.
                if ((offset & 1) == 0)
                {
                    // Control bit 4 is the CRTC's clock (video.md s2.2), which the ULA sets.
                    VideoUla.WriteControl(value);
                    ChipAccessed();
                }
                else
                {
                    VideoUla.WritePalette(value);
                }
                break;
            case >= 0x30 and <= 0x3F:
                // Write only, and the whole block of sixteen is the one latch (S1 s.17, s.21).
                RomSlot = value & 0x0F;
                _paged = PagedRom(RomSlot);
                break;
            case >= 0x40 and <= 0x5F:
                // ORB and DDRB strobe the latch; if its screen start bits change, the system VIA
                // has the ULA draw up to this cycle with the bits it had first.
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
        <= 0x07 => ReadCrtc(offset),
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
    /// A CRTC read: the data register at odd addresses, which has no side effect, and the
    /// write-only address register at even ones, which reads $00 (video.md s1.2, s1.3).
    /// </summary>
    private byte ReadCrtc(int offset) => (offset & 1) == 0 ? (byte)0x00 : Crtc.ReadData();
}
