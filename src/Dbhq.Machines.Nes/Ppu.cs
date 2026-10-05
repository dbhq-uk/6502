namespace Dbhq.Machines.Nes;

/// <summary>
/// The picture processing unit, the 2C02 on NTSC and the 2C07 on PAL: its eight registers, its
/// memory, and its clock of one dot a <see cref="Tick"/>.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/nes/facts/ppu.md</c> sections 1 to 5 and 12, and <c>timing.md</c> section 2.
/// This is the PPU of task 4 of the NES plan: the registers, VRAM, OAM, the scroll registers and
/// the frame's timing. It draws nothing yet; the picture, sprite 0 and overflow are task 5's.
/// </para>
/// <para>
/// <b>The position.</b> <see cref="Line"/> and <see cref="Dot"/> name the dot the PPU runs next.
/// <see cref="Tick"/> runs that dot's events and moves on, so a register access between two ticks
/// happens before the dot the position names. Power on puts the PPU at line 0 dot 0 (timing.md 4),
/// so after 21 ticks it is at line 0 dot 21, as <c>nestest.log</c>'s first line has it.
/// </para>
/// <para>
/// <b>The VBlank flag</b> is set by the dot at line 241 dot 1, and cleared, with sprite 0 hit and
/// overflow, by dot 1 of the pre-render line (ppu.md 5). A read of <c>$2002</c> while the PPU is
/// at line 241 dot 1, which is one dot before the flag is set, reads it clear and stops it being
/// set that frame, so no NMI comes (bus.md 2). A read one dot later reads it set and clears it.
/// Whether that read also stops the NMI is the CPU's affair: the NMI output is the flag and
/// PPUCTRL bit 7, and a flag set and cleared inside one CPU cycle is never seen by the CPU.
/// </para>
/// <para>
/// <b>The odd frame.</b> The PPU keeps an even and odd flag, toggled every frame. Where the region
/// drops a dot (NTSC) and rendering is on, an odd frame goes from dot 339 of the pre-render line
/// straight to line 0 dot 0 (timing.md 2). Rendering is sampled when dot 338 runs, so a
/// <c>$2001</c> write made while the PPU is at dot 339 is too late for this frame: measured
/// against <c>ppu_vbl_nmi</c> test 10, which fails with the sample at 337 or 339.
/// </para>
/// <para>
/// <b>The I/O latch</b> is the PPU's own, not the CPU's open bus (ppu.md 1). Every register write
/// fills it, reads of <c>$2004</c> and <c>$2007</c> fill it, a read of <c>$2002</c> fills bits 7 to
/// 5, and a read of a write-only register returns it. The decay of its bits over 3 to 30 ms is not
/// modelled: it holds its value until the next access.
/// </para>
/// <para>
/// <b>The PPU's address bus.</b> Every pattern-table address the PPU puts on its bus goes to
/// <see cref="IMapper.PpuAddressChanged"/>, which is what MMC3 watches. With no rendering yet the
/// only addresses are <c>v</c> and the <c>$2007</c> accesses (ppu.md 6: in VBlank or with
/// rendering off the bus carries <c>v</c>).
/// </para>
/// </remarks>
public sealed class Ppu
{
    // The line at whose dot 1 the VBlank flag is set, on both regions (timing.md 2).
    private const int VblankLine = 241;

    // The pre-render dot whose run samples rendering for the odd frame's dropped dot, measured
    // against ppu_vbl_nmi test 10: dot 337 drops it too soon, dot 339 too late (timing.md 2).
    private const int DropDecidedDot = 338;

    private const int StatusVblank = 0x80;
    private const int StatusSprite0 = 0x40;
    private const int StatusOverflow = 0x20;

    private readonly Region _region;
    private readonly IMapper _mapper;
    private readonly int _preRenderLine;
    private readonly int _lines;
    private readonly bool _oddFrameSkipsADot;

    // The console's 2 KB of nametable RAM, then the 2 KB a four-screen board adds, used only while
    // the board says FourScreen (mappers.md 1).
    private readonly byte[] _nametables = new byte[0x1000];
    private readonly byte[] _palette = new byte[32];

    private byte _ctrl;
    private byte _mask;
    private byte _status;
    private byte _oamAddress;
    private byte _latch;
    private byte _readBuffer;

    private ushort _v;
    private ushort _t;
    private byte _x;
    private bool _w;

    private int _line;
    private int _dot;
    private bool _oddFrame;
    private long _frame;

    // Set by a $2002 read one dot before the VBlank flag would be set: the flag is not set that frame.
    private bool _suppressVblank;

    // Decided when dot 338 of the pre-render line runs: this odd frame drops its last dot.
    private bool _dropDot;

    /// <summary>A PPU for <paramref name="region"/>, whose pattern tables and nametable wiring are <paramref name="mapper"/>'s.</summary>
    public Ppu(Region region, IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(mapper);
        _region = region;
        _mapper = mapper;
        _preRenderLine = region.PreRenderLine;
        _lines = region.Lines;
        _oddFrameSkipsADot = region.OddFrameSkipsADot;
    }

    /// <summary>The region this PPU is: the 2C02 for NTSC, the 2C07 for PAL.</summary>
    public Region Region => _region;

    /// <summary>
    /// The NMI output: true while the VBlank flag and PPUCTRL bit 7 are both set (ppu.md 5). The
    /// CPU takes an NMI on its change to true.
    /// </summary>
    public bool Nmi => (_status & StatusVblank) != 0 && (_ctrl & 0x80) != 0;

    /// <summary>The line of the dot the PPU runs next, 0 to <see cref="Region.PreRenderLine"/>.</summary>
    public int Line => _line;

    /// <summary>The dot the PPU runs next, 0 to 340.</summary>
    public int Dot => _dot;

    /// <summary>Frames completed since power on.</summary>
    public long Frame => _frame;

    /// <summary>True while the frame the PPU is in is an odd one, the frames that can drop a dot.</summary>
    public bool OddFrame => _oddFrame;

    /// <summary>The 256 bytes of sprite memory: 64 sprites of Y, tile, attributes and X (ppu.md 4).</summary>
    public byte[] Oam { get; } = new byte[256];

    /// <summary>The current VRAM address <c>v</c>, 15 bits (ppu.md 2). For tests and a debugger.</summary>
    public ushort V => _v;

    /// <summary>The waiting address <c>t</c>, 15 bits (ppu.md 2). For tests and a debugger.</summary>
    public ushort T => _t;

    /// <summary>The fine X scroll <c>x</c>, 3 bits (ppu.md 2). For tests and a debugger.</summary>
    public byte FineX => _x;

    /// <summary>The write toggle <c>w</c>: true after the first write of a <c>$2005</c> or <c>$2006</c> pair (ppu.md 2).</summary>
    public bool WriteToggle => _w;

    /// <summary>True while either layer is switched on in PPUMASK (bits 3 and 4).</summary>
    public bool RenderingEnabled => (_mask & 0x18) != 0;

    /// <summary>
    /// The CPU cycle count, for <see cref="IMapper.PpuAddressChanged"/>. The bus sets it; alone the
    /// PPU reports cycle 0.
    /// </summary>
    internal Func<long> CpuCycles { get; set; } = static () => 0;

    // Rendering is on and the PPU is on a line that renders: the visible lines and the pre-render line.
    private bool Rendering => RenderingEnabled && (_line < 240 || _line == _preRenderLine);

    /// <summary>
    /// Power on (ppu.md 12): the registers, the scroll registers and the read buffer at zero, the
    /// position at line 0 dot 0 of an even frame, and the memories cleared. A real console leaves
    /// OAM, the palette and the nametables unspecified and VBlank often set; zeros and a clear flag
    /// are the choice, so a run is repeatable.
    /// </summary>
    public void PowerOn()
    {
        _status = 0;
        _oamAddress = 0;
        _latch = 0;
        _v = 0;
        _frame = 0;
        Array.Clear(Oam);
        Array.Clear(_palette);
        Array.Clear(_nametables);
        Reset();
    }

    /// <summary>
    /// The reset button (ppu.md 12; timing.md 4: the NES-001 resets the PPU with the CPU).
    /// PPUCTRL, PPUMASK, <c>w</c>, <c>t</c>, <c>x</c> and the read buffer go to zero, the frame is
    /// even again and the PPU starts at the top of the picture. The VBlank flag, OAMADDR,
    /// <c>v</c> and the memories are kept.
    /// </summary>
    public void Reset()
    {
        _ctrl = 0;
        _mask = 0;
        _w = false;
        _t = 0;
        _x = 0;
        _readBuffer = 0;
        _oddFrame = false;
        _line = 0;
        _dot = 0;
        _suppressVblank = false;
        _dropDot = false;
    }

    /// <summary>Runs one dot: the dot <see cref="Line"/> and <see cref="Dot"/> name, then moves on.</summary>
    public void Tick()
    {
        if (_dot == 1)
        {
            if (_line == VblankLine)
            {
                if (!_suppressVblank)
                {
                    _status |= StatusVblank;
                }

                _suppressVblank = false;
            }
            else if (_line == _preRenderLine)
            {
                _status &= unchecked((byte)~(StatusVblank | StatusSprite0 | StatusOverflow));
            }
        }

        if (_line == _preRenderLine && _dot >= DropDecidedDot)
        {
            if (_dot == DropDecidedDot)
            {
                _dropDot = _oddFrame && _oddFrameSkipsADot && RenderingEnabled;
            }
            else if (_dot == 339 && _dropDot)
            {
                // The odd frame's dropped dot: dot 340 of the pre-render line never runs.
                _dropDot = false;
                _dot = 0;
                _line = 0;
                EndFrame();
                return;
            }
        }

        if (++_dot == Region.DotsPerLine)
        {
            _dot = 0;
            if (++_line == _lines)
            {
                _line = 0;
                EndFrame();
            }
        }
    }

    /// <summary>A CPU read of register <paramref name="register"/> (0 to 7) at the PPU's present dot, with its side effects.</summary>
    public byte ReadRegister(int register)
    {
        switch (register & 7)
        {
            case 2:
            {
                byte value = (byte)((_status & 0xE0) | (_latch & 0x1F));
                _latch = value;
                _status &= unchecked((byte)~StatusVblank);
                _w = false;
                if (_line == VblankLine && _dot == 1)
                {
                    // One dot before the flag is set: read clear, and never set this frame.
                    _suppressVblank = true;
                }

                return value;
            }

            case 4:
                _latch = Oam[_oamAddress];
                return _latch;

            case 7:
            {
                ushort address = (ushort)(_v & 0x3FFF);
                byte value;
                if (address >= 0x3F00)
                {
                    // A palette read is immediate, with the latch in bits 7 and 6; the buffer takes
                    // the nametable byte underneath (ppu.md 3).
                    value = (byte)(ReadPalette(address) | (_latch & 0xC0));
                    _readBuffer = ReadNametable((ushort)(address - 0x1000));
                }
                else
                {
                    value = _readBuffer;
                    _readBuffer = ReadVram(address);
                }

                Report(address);
                _latch = value;
                IncrementAfterAccess();
                return value;
            }

            default:
                return _latch;
        }
    }

    /// <summary>A CPU write of <paramref name="value"/> to register <paramref name="register"/> (0 to 7), with its side effects.</summary>
    public void WriteRegister(int register, byte value)
    {
        _latch = value;
        switch (register & 7)
        {
            case 0:
                _ctrl = value;
                _t = (ushort)((_t & ~0x0C00) | ((value & 0x03) << 10));
                break;

            case 1:
                _mask = value;
                break;

            case 2:
                // PPUSTATUS is read-only: a write fills the latch and nothing else.
                break;

            case 3:
                _oamAddress = value;
                break;

            case 4:
                if (Rendering)
                {
                    // During rendering the write does not reach OAM and bumps only the high six bits.
                    _oamAddress = (byte)(_oamAddress + 4);
                }
                else
                {
                    // Bits 4 to 2 of a sprite's attribute byte do not exist (ppu.md 4).
                    Oam[_oamAddress] = (_oamAddress & 3) == 2 ? (byte)(value & 0xE3) : value;
                    _oamAddress++;
                }

                break;

            case 5:
                if (!_w)
                {
                    _t = (ushort)((_t & ~0x001F) | (value >> 3));
                    _x = (byte)(value & 0x07);
                }
                else
                {
                    _t = (ushort)((_t & ~0x73E0) | ((value & 0x07) << 12) | ((value & 0xF8) << 2));
                }

                _w = !_w;
                break;

            case 6:
                if (!_w)
                {
                    // Bits 13 to 8 from the data, and bit 14 cleared.
                    _t = (ushort)((_t & 0x00FF) | ((value & 0x3F) << 8));
                }
                else
                {
                    _t = (ushort)((_t & 0x7F00) | value);
                    _v = _t;
                    Report(_v);
                }

                _w = !_w;
                break;

            default:
            {
                ushort address = (ushort)(_v & 0x3FFF);
                WriteVram(address, value);
                Report(address);
                IncrementAfterAccess();
                break;
            }
        }
    }

    /// <summary>
    /// What a read of register <paramref name="register"/> would return now, with no side effect:
    /// no flag is cleared, the read buffer and <c>v</c> do not move, and the latch is not changed.
    /// </summary>
    public byte PeekRegister(int register)
    {
        switch (register & 7)
        {
            case 2:
                return (byte)((_status & 0xE0) | (_latch & 0x1F));
            case 4:
                return Oam[_oamAddress];
            case 7:
            {
                ushort address = (ushort)(_v & 0x3FFF);
                return address >= 0x3F00 ? (byte)(ReadPalette(address) | (_latch & 0xC0)) : _readBuffer;
            }

            default:
                return _latch;
        }
    }

    /// <summary>The byte at PPU address <paramref name="address"/>, with no side effect, for tests.</summary>
    public byte PeekVram(ushort address)
    {
        address &= 0x3FFF;
        return address >= 0x3F00 ? _palette[PaletteIndex(address)] : ReadVram(address);
    }

    private static int PaletteIndex(ushort address)
    {
        int index = address & 0x1F;

        // Entry 0 of each sprite palette is entry 0 of the background palette (ppu.md 3).
        return (index & 0x13) == 0x10 ? index & 0x0F : index;
    }

    private void EndFrame()
    {
        _frame++;
        _oddFrame = !_oddFrame;
    }

    private byte ReadPalette(ushort address)
    {
        byte value = _palette[PaletteIndex(address)];

        // Greyscale shows in palette reads too (ppu.md 10).
        return (_mask & 0x01) != 0 ? (byte)(value & 0x30) : value;
    }

    private byte ReadVram(ushort address)
    {
        return address < 0x2000 ? _mapper.PpuRead(address) : ReadNametable(address);
    }

    private void WriteVram(ushort address, byte value)
    {
        if (address < 0x2000)
        {
            _mapper.PpuWrite(address, value);
        }
        else if (address < 0x3F00)
        {
            _nametables[NametableIndex(address)] = value;
        }
        else
        {
            _palette[PaletteIndex(address)] = (byte)(value & 0x3F);
        }
    }

    private byte ReadNametable(ushort address)
    {
        return _nametables[NametableIndex(address)];
    }

    // Where in the nametable RAM a PPU address in $2000 to $3EFF lands, by the board's wiring
    // (mappers.md 1). $3000 to $3EFF repeats $2000 to $2EFF.
    private int NametableIndex(ushort address)
    {
        int table = (address >> 10) & 3;
        int offset = address & 0x3FF;
        int page = _mapper.Mirroring switch
        {
            Mirroring.Vertical => table & 1,
            Mirroring.Horizontal => table >> 1,
            Mirroring.SingleScreenLow => 0,
            Mirroring.SingleScreenHigh => 1,
            _ => table,
        };
        return (page << 10) | offset;
    }

    // After a $2007 access: up by 1 or 32 outside rendering; during rendering a coarse X and a Y
    // increment together, whatever PPUCTRL says (ppu.md 2).
    private void IncrementAfterAccess()
    {
        if (Rendering)
        {
            _v = IncrementY(IncrementCoarseX(_v));
        }
        else
        {
            _v = (ushort)((_v + ((_ctrl & 0x04) != 0 ? 32 : 1)) & 0x7FFF);
            Report(_v);
        }
    }

    private static ushort IncrementCoarseX(ushort v)
    {
        return (v & 0x001F) == 31 ? (ushort)((v & ~0x001F) ^ 0x0400) : (ushort)(v + 1);
    }

    private static ushort IncrementY(ushort v)
    {
        if ((v & 0x7000) != 0x7000)
        {
            return (ushort)(v + 0x1000);
        }

        v = (ushort)(v & ~0x7000);
        int coarseY = (v & 0x03E0) >> 5;
        if (coarseY == 29)
        {
            coarseY = 0;
            v ^= 0x0800;
        }
        else if (coarseY == 31)
        {
            coarseY = 0;
        }
        else
        {
            coarseY++;
        }

        return (ushort)((v & ~0x03E0) | (coarseY << 5));
    }

    // Tells the board of a pattern-table address on the PPU's bus.
    private void Report(ushort address)
    {
        address &= 0x3FFF;
        if (address < 0x2000)
        {
            _mapper.PpuAddressChanged(address, CpuCycles());
        }
    }
}
