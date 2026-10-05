namespace Dbhq.Machines.Nes;

/// <summary>
/// The picture processing unit, the 2C02 on NTSC and the 2C07 on PAL: its eight registers, its
/// memory, and its clock of one dot a <see cref="Tick"/>, on which it draws the picture.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/nes/facts/ppu.md</c> and <c>timing.md</c> section 2. Task 4 of the NES plan
/// built the registers, VRAM, OAM, the scroll registers and the frame's timing; task 5 the drawing:
/// the background pipeline, the sprite unit, sprite 0 hit, the overflow flag and the colours.
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
/// <b>The background</b> (ppu.md 6), on the visible lines and the pre-render line while either
/// layer is on. Dots 1 to 256 and 321 to 336 fetch a tile every 8 dots: the nametable byte on the
/// second dot, the attribute byte on the fourth, the low and high pattern bytes on the sixth and
/// eighth, each pattern address put out on the dot before its read; the eighth dot moves coarse X
/// on. The fetched tile goes into the low halves of two 16-bit pattern shifters, and its two
/// attribute bits, spread to 8, into two attribute shifters, on dots 9, 17 ... 257, 329 and 337.
/// The shifters shift left on dots 2 to 257 and 322 to 337, and fine X picks the pixel from their
/// top 8 bits. Dot 256 does the Y increment, dot 257 copies <c>t</c>'s horizontal bits into
/// <c>v</c>, and dots 280 to 304 of the pre-render line copy its vertical bits.
/// </para>
/// <para>
/// <b>The sprites</b> (ppu.md 7 to 9). On each visible line, dots 1 to 64 fill secondary OAM with
/// <c>$FF</c>, and dots 65 to 256 evaluate, reading OAM on odd dots and acting on even ones: the
/// first 8 sprites in range for the next line are copied, and then the search for a ninth reads the
/// diagonal bytes the hardware does, so the overflow flag has its bug. The pre-render line does
/// not evaluate, so line 0 has no sprites. Dots 257 to 320 fetch the 8 slots' pattern bytes, an
/// empty slot fetching tile <c>$FF</c>, and clear OAMADDR. A sprite is drawn on the line after
/// the one that found it.
/// </para>
/// <para>
/// <b>The pixel.</b> Column <c>X</c> of a visible line is decided on dot <c>X + 2</c>, from the
/// shifters before that dot shifts them: the wiki has sprite 0 hit act "as if the image starts at
/// cycle 2" (ppu.md 6). It is decided by the multiplexer of ppu.md 7, and written to <see cref="Screen"/> in the colour the palette
/// entry, greyscale and emphasis give (<see cref="PpuPalette"/>). With rendering off the pixel is
/// the backdrop, or the palette entry <c>v</c> points at when it points into the palette. Sprite
/// 0 hit is set on the dot an opaque pixel of sprite 0 meets an opaque background pixel, except
/// at column 255 and where the left-column clip hides either.
/// </para>
/// <para>
/// <b>The I/O latch</b> is the PPU's own, not the CPU's open bus (ppu.md 1). Every register write
/// fills it, reads of <c>$2004</c> and <c>$2007</c> fill it, a read of <c>$2002</c> fills bits 7 to
/// 5, and a read of a write-only register returns it. Each bit decays to 0 once it has gone
/// <see cref="LatchDecaySeconds"/> without being driven: a write drives all eight, a <c>$2002</c>
/// read bits 7 to 5, a palette read bits 5 to 0, and any other read of <c>$2004</c> or
/// <c>$2007</c> all eight. The time is worked out only when the latch is used, so the dots pay
/// nothing for it.
/// </para>
/// <para>
/// <b>The PPU's address bus.</b> The addresses the PPU puts on its bus go to
/// <see cref="IMapper.PpuAddressChanged"/>, which is what MMC3 watches (its A12): during rendering
/// the background's and the sprites' pattern fetches, and each sprite slot's first garbage
/// nametable fetch; outside rendering (in VBlank or with rendering off) <c>v</c> whenever it
/// changes and the <c>$2007</c> accesses, whatever the address (ppu.md 6). A <c>$2006</c> or
/// <c>$2007</c> access during rendering is not told: the bus is carrying the fetches. The background's nametable and
/// attribute fetches are left out, though their A12 is always 0: between two pattern fetches they
/// are a 4-dot low, which MMC3's filter ignores anyway, and from dot 337 to the next line's dot 4
/// a 9-dot low, exactly 3 CPU cycles on NTSC, which told would clock the counter twice a line with
/// the background at <c>$1000</c>, where the sheet says once (mappers.md 6, ppu.md 6). Leaving
/// them out also saves a call every 4 dots. The CPU cycle given with each is
/// <see cref="CpuCycle"/>, which the bus sets once a cycle.
/// </para>
/// <para>
/// Nothing on the per-dot path allocates: every buffer is made with the PPU.
/// </para>
/// </remarks>
public sealed partial class Ppu
{
    // The line at whose dot 1 the VBlank flag is set, on both regions (timing.md 2).
    private const int VblankLine = 241;

    // The pre-render dot whose run samples rendering for the odd frame's dropped dot, measured
    // against ppu_vbl_nmi test 10: dot 337 drops it too soon, dot 339 too late (timing.md 2).
    private const int DropDecidedDot = 338;

    private const int StatusVblank = 0x80;
    private const int StatusSprite0 = 0x40;
    private const int StatusOverflow = 0x20;

    // Each byte with its bits in the other order, for a sprite flipped horizontally.
    private static readonly byte[] Reversed = BuildReversed();

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

    // When each bit of the latch was last driven, in dots of _time, and how many dots it lasts.
    private readonly long[] _latchDriven = new long[8];
    private readonly long _latchDecayDots;

    // Dots run before the present frame's line 0 dot 0 and since the last reset; with the
    // frame's lines and dots it makes a time that keeps counting across the reset button.
    private long _timeBase;

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

    // The colours of this region's PPU, (emphasis << 6) | colour, and PPUMASK's part in the index.
    private readonly uint[] _colours;
    private int _emphasis;
    private int _greyscaleMask = 0x3F;

    // The background: the bytes the 8-dot fetch has read, the pattern address it put out, and the
    // four shifters (ppu.md 6).
    private byte _nametableByte;
    private int _attributeBits;
    private byte _patternLowByte;
    private byte _patternHighByte;
    private ushort _patternAddress;
    private ushort _patternLow;
    private ushort _patternHigh;
    private ushort _attributeLow;
    private ushort _attributeHigh;

    // Sprite evaluation for the next line (ppu.md 7 and 9): secondary OAM, the sprite n and byte
    // m being read, the byte the last odd dot read, where in secondary OAM the next byte goes, how
    // many sprites are copied, and whether 8 are found, the search is over, sprite 0 is among them.
    private readonly byte[] _secondaryOam = new byte[32];
    private int _evaluationN;
    private int _evaluationM;
    private byte _oamLatch;
    private int _secondaryIndex;
    private int _found;
    private bool _secondaryFull;
    private bool _evaluationDone;
    private bool _sprite0Found;

    // The sprites of the next line, laid out as the fetches on dots 257 to 320 read them: for each
    // column the winning sprite pixel, 0 where none is opaque, else its value (bits 1 and 0), its
    // palette (3 and 2), its priority (bit 5) and whether it is sprite 0 (bit 6). The slots are
    // laid in order and a column already taken is kept, so the lowest-numbered sprite wins.
    private readonly byte[] _spriteLine = new byte[256];
    private const int SpriteBehind = 0x20;
    private const int SpriteIsSprite0 = 0x40;

    // How many slots of the line being fetched hold a sprite, and whether slot 0 is sprite 0.
    private int _spriteCount;
    private bool _sprite0OnLine;

    // The slot being fetched on dots 257 to 320.
    private byte _fetchY;
    private byte _fetchTile;
    private byte _fetchAttributes;
    private byte _fetchX;
    private byte _fetchLow;
    private ushort _spriteAddress;

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
        _colours = PpuPalette.Table(region.EmphasisSwapsRedAndGreen);
        _latchDecayDots = (long)(LatchDecaySeconds * region.CpuHz * region.DotsNumerator / region.DotsDenominator);
    }

    /// <summary>
    /// How long a bit of the I/O latch keeps its value when nothing drives it: about 600 ms, the
    /// fork's <c>ppu_open_bus/readme.txt</c> measured on a console (ppu.md 1). The wiki says at
    /// least one bit goes after 3 to 30 ms; the model gives every bit the readme's time.
    /// </summary>
    public const double LatchDecaySeconds = 0.6;

    /// <summary>The picture, written a pixel a dot.</summary>
    public FrameBuffer Screen { get; } = new();

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
    /// The CPU cycle the PPU's dots are running in, given with each address to
    /// <see cref="IMapper.PpuAddressChanged"/>. The bus sets it once a cycle, before the cycle's
    /// dots; alone the PPU reports cycle 0.
    /// </summary>
    internal long CpuCycle { get; set; }

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
        _timeBase = 0;
        Array.Clear(_latchDriven);
        _v = 0;
        _frame = 0;
        Array.Clear(Oam);
        Array.Clear(_palette);
        Array.Clear(_nametables);
        Screen.PowerOn();
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
        // The position goes back to the top; the time the latch's bits are measured in does not.
        _timeBase += (_line * Region.DotsPerLine) + _dot;
        _ctrl = 0;
        SetMask(0);
        _w = false;
        _t = 0;
        _x = 0;
        _readBuffer = 0;
        _oddFrame = false;
        _line = 0;
        _dot = 0;
        _suppressVblank = false;
        _dropDot = false;
        _patternLow = 0;
        _patternHigh = 0;
        _attributeLow = 0;
        _attributeHigh = 0;
        _spriteCount = 0;
        _sprite0OnLine = false;
        _found = 0;
        _sprite0Found = false;
        Array.Clear(_spriteLine);
    }

    /// <summary>Runs one dot: the dot <see cref="Line"/> and <see cref="Dot"/> name, then moves on.</summary>
    public void Tick()
    {
        if (_line < 240)
        {
            if ((_mask & 0x18) != 0)
            {
                RenderDot(true);
            }
            else if (_dot >= 2 && _dot <= 257)
            {
                DrawRenderingOff();
                if (_dot == 257)
                {
                    // No sprite was fetched for the next line.
                    _spriteCount = 0;
                    Array.Clear(_spriteLine);
                }
            }
        }
        else if (_line == _preRenderLine)
        {
            if ((_mask & 0x18) != 0)
            {
                RenderDot(false);
            }
            else if (_dot == 257)
            {
                // No sprite was fetched for line 0.
                _spriteCount = 0;
                Array.Clear(_spriteLine);
            }
        }

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
                EndFrame((_lines * Region.DotsPerLine) - 1);
                return;
            }
        }

        if (++_dot == Region.DotsPerLine)
        {
            _dot = 0;
            if (++_line == _lines)
            {
                _line = 0;
                EndFrame(_lines * Region.DotsPerLine);
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
                byte value = (byte)((_status & 0xE0) | (Latch() & 0x1F));
                Drive(value, 0xE0);
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
            {
                byte value = OamData();
                Drive(value, 0xFF);
                return value;
            }

            case 7:
            {
                ushort address = (ushort)(_v & 0x3FFF);
                byte value;
                if (address >= 0x3F00)
                {
                    // A palette read is immediate, with the latch in bits 7 and 6; the buffer takes
                    // the nametable byte underneath (ppu.md 3).
                    value = (byte)(ReadPalette(address) | (Latch() & 0xC0));
                    _readBuffer = ReadNametable((ushort)(address - 0x1000));
                    Drive(value, 0x3F);
                }
                else
                {
                    value = _readBuffer;
                    _readBuffer = ReadVram(address);
                    Drive(value, 0xFF);
                }

                Report(address);
                IncrementAfterAccess();
                return value;
            }

            default:
                return Latch();
        }
    }

    /// <summary>A CPU write of <paramref name="value"/> to register <paramref name="register"/> (0 to 7), with its side effects.</summary>
    public void WriteRegister(int register, byte value)
    {
        Drive(value, 0xFF);
        switch (register & 7)
        {
            case 0:
                _ctrl = value;
                _t = (ushort)((_t & ~0x0C00) | ((value & 0x03) << 10));
                break;

            case 1:
                SetMask(value);
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
                return (byte)((_status & 0xE0) | (Latch() & 0x1F));
            case 4:
                return OamData();
            case 7:
            {
                ushort address = (ushort)(_v & 0x3FFF);
                return address >= 0x3F00 ? (byte)(ReadPalette(address) | (Latch() & 0xC0)) : _readBuffer;
            }

            default:
                return Latch();
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

    // The dots run since power on.
    private long Time => _timeBase + (_line * Region.DotsPerLine) + _dot;

    // The latch as it reads now: each bit not driven for the decay time is 0. Clearing a bit for
    // good is the same, since only a drive sets it again.
    private byte Latch()
    {
        if (_latch != 0)
        {
            long now = Time;
            for (int bit = 0; bit < 8; bit++)
            {
                if ((_latch & (1 << bit)) != 0 && now - _latchDriven[bit] > _latchDecayDots)
                {
                    _latch &= (byte)~(1 << bit);
                }
            }
        }

        return _latch;
    }

    // Puts the bits of value under mask on the latch, as an access that drives those bits does.
    private void Drive(byte value, int mask)
    {
        long now = Time;
        _latch = (byte)((Latch() & ~mask) | (value & mask));
        for (int bit = 0; bit < 8; bit++)
        {
            if ((mask & (1 << bit)) != 0)
            {
                _latchDriven[bit] = now;
            }
        }
    }

    private void EndFrame(int dots)
    {
        _timeBase += dots;
        _frame++;
        _oddFrame = !_oddFrame;
        Screen.EndFrame();
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

    // Tells the board of v, or a $2007 access, on the PPU's bus. Only outside rendering: while it
    // renders the bus carries the fetches, not v (ppu.md 6). Every address goes, nametable and
    // palette ones too: A12 is bit 12 of any of them, so $3F00 is A12 high (ppu.md 6, task 11).
    private void Report(ushort address)
    {
        if (Rendering)
        {
            return;
        }

        _mapper.PpuAddressChanged((ushort)(address & 0x3FFF), CpuCycle);
    }

    private void SetMask(byte value)
    {
        _mask = value;
        _emphasis = (value >> 5) << 6;
        _greyscaleMask = (value & 0x01) != 0 ? 0x30 : 0x3F;
    }

    // What a $2004 read sees: during rendering on a visible line, what sprite evaluation and the
    // sprite fetches are reading ($FF while secondary OAM is cleared, ppu.md 1); otherwise OAM.
    private byte OamData()
    {
        return _line < 240 && RenderingEnabled ? _oamLatch : Oam[_oamAddress];
    }

    // One dot of a line that renders, with rendering on (ppu.md 6 and 7).
    private void RenderDot(bool visible)
    {
        int dot = _dot;
        if (dot == 0)
        {
            return;
        }

        if (dot <= 256)
        {
            if (dot >= 2)
            {
                // Column dot - 2 is decided from the shifters as they stand, then they shift.
                if (visible)
                {
                    DrawPixel(dot - 2);
                }

                Shift();
            }

            if ((dot & 7) == 1 && dot >= 9)
            {
                Reload();
            }

            if (visible)
            {
                Evaluate(dot);
            }

            FetchBackground(dot);
            if (dot == 256)
            {
                _v = IncrementY(_v);
            }

            return;
        }

        if (dot <= 320)
        {
            if (dot == 257)
            {
                if (visible)
                {
                    DrawPixel(255);
                }

                Shift();
                Reload();

                // t's horizontal bits: coarse X and the horizontal nametable bit.
                _v = (ushort)((_v & ~0x041F) | (_t & 0x041F));

                // The next line's sprites are the ones this line found; the pre-render line finds none.
                _spriteCount = visible ? _found : 0;
                _sprite0OnLine = visible && _sprite0Found;
                Array.Clear(_spriteLine);
            }
            else if (!visible && dot >= 280 && dot <= 304)
            {
                // t's vertical bits: fine Y, the vertical nametable bit and coarse Y.
                _v = (ushort)((_v & ~0x7BE0) | (_t & 0x7BE0));
            }

            _oamAddress = 0;
            FetchSprite(dot - 257);
            return;
        }

        if (dot <= 336)
        {
            if (dot >= 322)
            {
                Shift();
            }

            if (dot == 329)
            {
                Reload();
            }

            FetchBackground(dot);
        }
        else if (dot == 337)
        {
            Shift();
            Reload();
        }
    }

    // Column x of the line being drawn: the background, the sprites, the multiplexer (ppu.md 7),
    // sprite 0 hit (ppu.md 8), and the colour.
    private void DrawPixel(int x)
    {
        int pixel = 0;
        if ((_mask & 0x08) != 0 && (x >= 8 || (_mask & 0x02) != 0))
        {
            int bit = 15 - _x;
            pixel = ((_patternLow >> bit) & 1) | (((_patternHigh >> bit) & 1) << 1);
            if (pixel != 0)
            {
                pixel |= (((_attributeLow >> bit) & 1) << 2) | (((_attributeHigh >> bit) & 1) << 3);
            }
        }

        int sprite = _spriteLine[x];
        if (sprite != 0 && (_mask & 0x10) != 0 && (x >= 8 || (_mask & 0x04) != 0))
        {
            if ((sprite & SpriteIsSprite0) != 0 && pixel != 0 && x != 255)
            {
                _status |= StatusSprite0;
            }

            // The winning sprite pixel goes in front unless it is behind and the background is opaque.
            if (pixel == 0 || (sprite & SpriteBehind) == 0)
            {
                pixel = 0x10 | (sprite & 0x0F);
            }
        }

        // A pixel value of 0 is the backdrop, $3F00, so index 0 needs no mirror.
        Screen.Pixels[(_line << 8) | x] = _colours[_emphasis | (_palette[pixel] & _greyscaleMask)];
    }

    // With rendering off the picture is the backdrop, or the entry v points at in the palette (ppu.md 10).
    private void DrawRenderingOff()
    {
        int entry = (_v & 0x3F00) == 0x3F00 ? PaletteIndex(_v) : 0;
        Screen.Pixels[(_line << 8) | (_dot - 2)] = _colours[_emphasis | (_palette[entry] & _greyscaleMask)];
    }
}
