namespace Dbhq.Machines.Electron;

/// <summary>
/// The display side of the Electron's ULA, modes 0 to 6: the start address, the mode, the palette
/// and the address generator, drawing into a <see cref="Framebuffer"/> (fact sheet <c>ula.md</c>
/// section 5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Registers.</b> <c>$FE02</c> and <c>$FE03</c> hold the start address shifted right by one,
/// in 64-byte steps: <c>$FE03</c> bits 5 to 0 are A14 to A9 and <c>$FE02</c> bits 7 to 5 are A8
/// to A6 (s1c, s5a). Bits 5 to 3 of <c>$FE07</c> are the mode, a 7 acting as mode 4 (s5a); the
/// rest of that register is not the display's. <c>$FE08</c> to <c>$FE0F</c> are the palette, in
/// negative logic, a 1 turning a colour off (s5c).
/// </para>
/// <para>
/// <b>Pixels.</b> A pixel value becomes a logical colour: one bit a pixel gives 0 and 8, two give
/// 0, 2, 8 and 10, four give the value itself (s5c); bit 7 is the leftmost pixel, and a two- or
/// four-bit pixel takes its bits from every other or every second bit of the byte (s5b). The
/// palette gives each logical colour red, green and blue on or off.
/// </para>
/// <para>
/// <b>Addresses (s5e).</b> The start address is read once, at the start of each field; a start
/// below <c>$0800</c> is replaced by the mode's start. A line fetches 80 bytes (modes 0 to 3) or 40
/// (4 to 6) at <c>rowbase + 8k + lineInRow</c>, an address past <c>$7FFF</c> continuing at the
/// mode's start plus the overflow. After the last line of a character row, line 7, or line 9 in
/// modes 3 and 6, the row base moves on by 8 times the bytes a line. In modes 3 and 6 lines 8 and
/// 9 of each row are black and fetch nothing (s5a), and only 250 lines are shown; the rest of the
/// 256 is black.
/// </para>
/// <para>
/// <b>A line at a time, lazily.</b> The display does not run on the cycle. Each line is drawn
/// whole, from the registers and RAM as they are at the cycle the line starts (s5d: line
/// <c>L</c> of a field starts <c>128 L</c> cycles into it, the odd field at
/// <c>t mod 80,000 == 0</c> and the even at 39,936). It is drawn on demand: before a register
/// write (<see cref="Write"/>), before a store to RAM that a line still to be drawn could show (the
/// bus asks with <see cref="NextLineStart"/> and <see cref="WatchFrom"/>, one comparison each),
/// when the ULA's interrupts are caught up, and when the picture is read. A write at cycle
/// <c>t</c> is seen by every line that starts at <c>t</c> or later. That makes a mode write take
/// effect at the end of the line it lands in, as s5e says, and a palette write from the next
/// line. Both fields draw the same 256 lines into the same rows.
/// </para>
/// <para>
/// <b>A byte at a time.</b> A line is drawn as one copy a byte, from a table of each byte value's
/// ready-made framebuffer pixels in the current mode and palette, made again only when either
/// changes. Writing each pixel on its own cost the browser most of its speed in mode 0 (task 13).
/// </para>
/// <para>
/// <b>What is not exact</b>, in <c>docs/known-differences.md</c>: a line's bytes are read at its
/// start and not one by one through it; the offset from a fetch to its pixel is not modelled; a
/// palette write is seen from the next line, not at once (s12 item 6); what the counters do when
/// the mode moves between 80 and 40 bytes a line in the middle of a row is not established (s12
/// item 7), and the model keeps the row base and the line in the row as they are; the address
/// counter advances once a row through the blank lines of modes 3 and 6 (s12 item 8).
/// </para>
/// <para>
/// <b>Power on.</b> What the registers hold is not known (s12 item 4). The model takes zero, which
/// is mode 0 from <c>$0000</c>, so from the mode's start, <c>$3000</c>, and a palette of zeros,
/// every colour white, until the OS writes them.
/// </para>
/// </remarks>
public sealed class UlaDisplay
{
    private const long FrameCycles = 80_000;
    private const long EvenFieldStart = 39_936;
    private const int LineCycles = 128;
    private const int ActiveLines = Framebuffer.Rows;
    private const int Width = Framebuffer.PixelsAcross;
    private const int LowestModeStart = 0x3000;

    // The palette's bits, as the table in s5c gives them, bit 7 first; "x" is unused. A 1 turns
    // that colour's component off.
    private static readonly string[] PaletteTable =
    [
        "B10 B8 B2 B0 G10 G8 x x",   // $FE08
        "x x G2 G0 R10 R8 R2 R0",    // $FE09
        "B14 B12 B6 B4 G14 G12 x x", // $FE0A
        "x x G6 G4 R14 R12 R6 R4",   // $FE0B
        "B15 B13 B7 B5 G15 G13 x x", // $FE0C
        "x x G7 G5 R15 R13 R7 R5",   // $FE0D
        "B11 B9 B3 B1 G11 G9 x x",   // $FE0E
        "x x G3 G1 R11 R9 R3 R1",    // $FE0F
    ];

    // For each logical colour and each of red, green and blue: the palette register (0 to 7) and
    // the bit's mask.
    private static readonly (int Register, int Mask)[,] PaletteBits = MakePaletteBits();

    // For each format, one, two or four bits a pixel: each byte's pixels as logical colours, left first.
    private static readonly byte[] OneBit = MakeLogical(8);
    private static readonly byte[] TwoBits = MakeLogical(4);
    private static readonly byte[] FourBits = MakeLogical(2);

    private readonly byte[] _ram;
    private readonly byte[] _palette = new byte[8];
    private readonly uint[] _colours = new uint[16];

    // Each byte value's framebuffer pixels in the mode and palette the table was made for, 8 a
    // byte in modes 0 to 3 and 16 in modes 4 to 6, ready to copy: a line is 80 or 40 copies, not
    // a write per pixel. Made again only when the mode or the palette has changed since.
    private readonly uint[] _pixels = new uint[256 * (Width / 40)];
    private int _pixelsMode = -1;
    private byte _startLow;
    private byte _startHigh;

    // Where the drawing is: the frame and field of the next line to draw, and that line.
    private long _frameStart;
    private bool _oddField = true;
    private int _line;

    // The address generator of the field being drawn (s5e).
    private int _fieldStart;
    private int _rowBase;
    private int _lineInRow;

    /// <param name="ram">The 32 KB the display fetches from, shared with the bus.</param>
    /// <param name="now">
    /// The machine's clock, so that reading the picture first draws up to now; null on its own,
    /// where the caller brings it up to date with <see cref="CatchUp"/>.
    /// </param>
    internal UlaDisplay(byte[] ram, Func<long>? now = null)
    {
        ArgumentNullException.ThrowIfNull(ram);
        _ram = ram;
        if (now is not null)
        {
            Screen.BeforeRead = () => CatchUp(now());
        }

        UpdateColours();
        WatchFrom = Math.Min(LowestModeStart, StartFor(StartAddress, Mode));
    }

    /// <summary>The picture.</summary>
    public Framebuffer Screen { get; } = new();

    /// <summary>The display's mode, 0 to 6, from the last write to $FE07.</summary>
    public int Mode { get; private set; }

    /// <summary>The start address as $FE02 and $FE03 hold it now: the next field's, before the rule for a start below $0800.</summary>
    public int StartAddress => ((_startHigh & 0x3F) << 9) | ((_startLow & 0xE0) << 1);

    /// <summary>The cycle at which the next line still to be drawn starts.</summary>
    internal long NextLineStart { get; private set; }

    /// <summary>
    /// The lowest RAM address a line still to be drawn could show: the field's start, the next
    /// field's, or the lowest start a mode wraps to, whichever is lowest. A store below it changes
    /// nothing on the picture, so the bus does not bring the picture up to date for it.
    /// </summary>
    internal int WatchFrom { get; private set; }

    /// <summary>Bytes a line fetches: 80 in modes 0 to 3, 40 in 4 to 6 (s5a).</summary>
    public static int BytesPerLine(int mode) => mode <= 3 ? 80 : 40;

    /// <summary>Lines to a character row: 10 in modes 3 and 6, 8 of data and 2 blank, and 8 in the rest (s5a).</summary>
    public static int LinesPerRow(int mode) => mode is 3 or 6 ? 10 : 8;

    /// <summary>Lines of a field the mode shows: 250 in modes 3 and 6, 256 in the rest (s5a).</summary>
    public static int DisplayedLines(int mode) => mode is 3 or 6 ? 250 : 256;

    /// <summary>The mode's screen start, which is also where an address past $7FFF wraps to (s5a, s5e).</summary>
    public static int ScreenStart(int mode) => mode switch
    {
        <= 2 => 0x3000,
        3 => 0x4000,
        4 or 5 => 0x5800,
        _ => 0x6000,
    };

    /// <summary>Pixels in a byte: 8 at one bit a pixel (modes 0, 3, 4, 6), 4 at two (1, 5), 2 at four (2) (s5a).</summary>
    public static int PixelsPerByte(int mode) => mode switch
    {
        1 or 5 => 4,
        2 => 2,
        _ => 8,
    };

    /// <summary>The logical colours of a byte's pixels in <paramref name="mode"/>, left first (s5b, s5c).</summary>
    public static void LogicalColours(int mode, byte value, Span<byte> pixels)
    {
        int n = PixelsPerByte(mode);
        Table(n).AsSpan(value * n, n).CopyTo(pixels);
    }

    /// <summary>The colour the palette gives logical colour <paramref name="logical"/> (0 to 15) now, as <c>0xAABBGGRR</c>.</summary>
    public uint Colour(int logical) => _colours[logical];

    /// <summary>
    /// A write to register <paramref name="register"/> (0 to 15) at <paramref name="cycle"/>: the
    /// lines that started before it are drawn first with what was there before. Only 2, 3, 7 and
    /// 8 to 15 are the display's; a write to any other is ignored here.
    /// </summary>
    internal void Write(int register, byte value, long cycle)
    {
        if (register is not (2 or 3 or 7 or >= 8 and <= 15))
        {
            return;
        }

        CatchUp(cycle);
        switch (register)
        {
            case 2:
                _startLow = value;
                break;
            case 3:
                _startHigh = value;
                break;
            case 7:
                Mode = Ula.ModeOf(value);
                break;
            default:
                _palette[register - 8] = value;
                UpdateColours();
                break;
        }

        UpdateWatch();
    }

    /// <summary>Draws every line that starts before <paramref name="cycle"/>.</summary>
    internal void CatchUp(long cycle)
    {
        while (NextLineStart < cycle)
        {
            if (_line == 0)
            {
                if (!_oddField)
                {
                    // The even field's first line starts where the odd field ends (s5d).
                    Screen.FrameDone();
                }

                // s5e: the start address, once a field.
                _fieldStart = StartFor(StartAddress, Mode);
                _rowBase = _fieldStart;
                _lineInRow = 0;
                UpdateWatch();
            }

            DrawLine();
            NextLine();
        }
    }

    /// <summary>A start below $0800 is the mode's start (s5e).</summary>
    private static int StartFor(int start, int mode) => start < 0x0800 ? ScreenStart(mode) : start;

    private static byte[] Table(int pixelsPerByte) => pixelsPerByte switch
    {
        8 => OneBit,
        4 => TwoBits,
        _ => FourBits,
    };

    private void UpdateWatch() =>
        WatchFrom = Math.Min(LowestModeStart, Math.Min(_fieldStart == 0 ? LowestModeStart : _fieldStart, StartFor(StartAddress, Mode)));

    /// <summary>Draws line <see cref="_line"/> of the field, then moves the address generator on.</summary>
    private void DrawLine()
    {
        int mode = Mode;
        Span<uint> row = Screen.Buffer.AsSpan(_line * Width, Width);
        bool blank = _line >= DisplayedLines(mode) || _lineInRow >= 8;
        if (blank)
        {
            row.Fill(Framebuffer.Black);
        }
        else
        {
            if (_pixelsMode != mode)
            {
                MakePixels(mode);
            }

            int bytes = BytesPerLine(mode);
            int across = Width / bytes;
            int wrapTo = ScreenStart(mode);
            for (int k = 0; k < bytes; k++)
            {
                int address = _rowBase + (8 * k) + _lineInRow;
                if (address >= 0x8000)
                {
                    address = address - 0x8000 + wrapTo;
                }

                _pixels.AsSpan(_ram[address] * across, across).CopyTo(row.Slice(k * across, across));
            }
        }

        // s5e: after the last line of a character row the row base moves on by 8N. A line past
        // the row's last (after a change from a 10-line mode) also ends the row.
        if (++_lineInRow >= LinesPerRow(mode))
        {
            _lineInRow = 0;
            _rowBase += 8 * BytesPerLine(mode);
            if (_rowBase >= 0x8000)
            {
                _rowBase = _rowBase - 0x8000 + ScreenStart(mode);
            }
        }
    }

    /// <summary>Moves to the next line the picture shows: line 255 is followed by the next field's line 0.</summary>
    private void NextLine()
    {
        if (++_line == ActiveLines)
        {
            _line = 0;
            if (_oddField)
            {
                _oddField = false;
            }
            else
            {
                _oddField = true;
                _frameStart += FrameCycles;
            }
        }

        NextLineStart = _frameStart + (_oddField ? 0 : EvenFieldStart) + ((long)_line * LineCycles);
    }

    private void UpdateColours()
    {
        for (int c = 0; c < 16; c++)
        {
            uint colour = Framebuffer.Black;
            for (int component = 0; component < 3; component++)
            {
                (int register, int mask) = PaletteBits[c, component];
                if ((_palette[register] & mask) == 0)
                {
                    // Red, green and blue are bytes 0, 1 and 2 of 0xAABBGGRR.
                    colour |= 0xFFu << (8 * component);
                }
            }

            _colours[c] = colour;
        }

        _pixelsMode = -1;
    }

    /// <summary>
    /// Makes the pixel table for <paramref name="mode"/> and the palette as it is: for each byte
    /// value, its pixels' logical colours (s5b, s5c) through the palette, each pixel as wide as the
    /// mode's pixels are on the framebuffer.
    /// </summary>
    private void MakePixels(int mode)
    {
        int perByte = PixelsPerByte(mode);
        int across = Width / BytesPerLine(mode);
        int width = across / perByte;
        byte[] logical = Table(perByte);
        for (int value = 0; value < 256; value++)
        {
            Span<uint> pixels = _pixels.AsSpan(value * across, across);
            for (int p = 0; p < perByte; p++)
            {
                pixels.Slice(p * width, width).Fill(_colours[logical[(value * perByte) + p]]);
            }
        }

        _pixelsMode = mode;
    }

    private static (int, int)[,] MakePaletteBits()
    {
        var bits = new (int, int)[16, 3];
        var found = new bool[16, 3];
        for (int register = 0; register < PaletteTable.Length; register++)
        {
            string[] names = PaletteTable[register].Split(' ');
            for (int i = 0; i < 8; i++)
            {
                if (names[i] == "x")
                {
                    continue;
                }

                int component = "RGB".IndexOf(names[i][0], StringComparison.Ordinal);
                int colour = int.Parse(names[i][1..], System.Globalization.CultureInfo.InvariantCulture);
                bits[colour, component] = (register, 0x80 >> i);
                found[colour, component] = true;
            }
        }

        foreach (bool f in found)
        {
            if (!f)
            {
                throw new InvalidOperationException("The palette table leaves a colour's component out.");
            }
        }

        return bits;
    }

    /// <summary>For each byte, its pixels' logical colours at 8, 4 or 2 pixels a byte (s5b, s5c).</summary>
    private static byte[] MakeLogical(int pixelsPerByte)
    {
        var table = new byte[256 * pixelsPerByte];
        for (int value = 0; value < 256; value++)
        {
            for (int k = 0; k < pixelsPerByte; k++)
            {
                int Bit(int n) => (value >> n) & 1;
                table[(value * pixelsPerByte) + k] = pixelsPerByte switch
                {
                    // 0 is logical colour 0 and 1 is 8.
                    8 => (byte)(Bit(7 - k) << 3),
                    // High bit 7-k and low bit 3-k: values 0, 1, 2, 3 are logical 0, 2, 8, 10.
                    4 => (byte)((Bit(7 - k) << 3) | (Bit(3 - k) << 1)),
                    // Bits 7-k, 5-k, 3-k, 1-k, high to low: the value is the logical colour.
                    _ => (byte)((Bit(7 - k) << 3) | (Bit(5 - k) << 2) | (Bit(3 - k) << 1) | Bit(1 - k)),
                };
            }
        }

        return table;
    }
}
