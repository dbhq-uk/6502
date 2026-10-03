using System.Runtime.InteropServices;

namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The video ULA (Ferranti 5C094, VIDPROC), IC6 on the Model B: it turns the bytes the CRTC
/// addresses into pixels through its palette, and draws them into a <see cref="Framebuffer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Built from the fact sheet <c>docs/bbc-micro/facts/video.md</c> sections 2 and 3. Two
/// write-only registers, A0 choosing: the control register at <c>&amp;FE20</c> and the palette at
/// <c>&amp;FE21</c> (s2.1).
/// </para>
/// <para>
/// <b>Control (s2.2).</b> Bits 7, 6 and 5 enable cursor segments 0, 1 and 2; bit 4 is the CRTC's
/// clock, 2 MHz or 1 MHz (the bus passes it on); bits 3 and 2 the pixel rate, 2, 4, 8 or 16 MHz,
/// which is 10, 20, 40 or 80 characters a line; bit 1 selects the teletext chip's picture; bit 0
/// is the flash select. The byte does not say which mode it is: modes 0 and 3 write the same
/// byte, and so do 4 and 6; they differ in the CRTC and the latch.
/// </para>
/// <para>
/// <b>Palette (s2.3).</b> Sixteen entries of four bits; a write stores bits 3 to 0 at the entry
/// bits 7 to 4 name. An entry is a flash bit and the colour's blue, green and red inverted, so it
/// shows physical colour <c>stored XOR 7</c>, or the stored bits themselves while the entry's
/// flash bit and the flash select are both set.
/// </para>
/// <para>
/// <b>Pixels (s2.4).</b> Each CRTC character the ULA takes the byte at the CRTC's address into
/// its shift register. Each pixel clock the palette is looked up with bits 7, 5, 3 and 1 of the
/// register, which then shifts left, filling with 1s. A character lasts pixel rate / CRTC rate
/// clocks, so past eight the index is 15 (80 columns on the 1 MHz clock), and with one clock only
/// the odd bits show (10 columns on 2 MHz). Then DISEN, which is DISPTMG and not RA3, blanks to
/// black, and CURSOR inverts what is left (s1.5, s2.4). The RA3 gate is taken to be off while the
/// teletext select is on: mode 7 runs RA 0 to 19 and is not blanked, but how is not known (s6
/// item 4).
/// </para>
/// <para>
/// <b>Cursor (s2.2).</b> When CUDISP comes, the ULA draws segment 0 on that character, segment 1
/// on the next and segment 2 on the two after, each only if its control bit is set: one, two or
/// four characters, which is one, two or four bytes in every mode.
/// </para>
/// <para>
/// <b>Address (s2.5).</b> The byte for MA and RA is <c>(MA &lt;&lt; 3) | RA2..0</c> on MA0 to
/// MA11; when MA12 is high the address has passed <c>&amp;7FFF</c> and the hardware adds a fixed
/// amount chosen by the system VIA's latch bits 4 and 5 (C0 and C1): &amp;4000, &amp;6000,
/// &amp;3000 or &amp;5800 for C1 C0 = 00, 01, 10, 11, which wraps the address back to the mode's
/// screen base. MA13 high is the teletext path, where RA is ignored. <see cref="ScreenAddress"/>.
/// </para>
/// <para>
/// <b>A line at a time, and when.</b> The ULA does not run in every cycle. It draws whole lines,
/// at an event the bus looks at, the cycle of each line's last character
/// (<see cref="NextEventCycle"/>): it asks the CRTC for its state at that cycle
/// (<see cref="Crtc6845.StateAt"/>), which holds the line's start address, RA, the vertical
/// display, R1, the skews and the cursor, and from it works out each character's DISPTMG, CUDISP
/// and address the way the CRTC does, reads the bytes from RAM and draws. Registers change only at
/// writes, and before any write that changes what it draws (its own registers, a CRTC register,
/// the latch bits) the ULA first draws up to that write's cycle with what it had, so a write in
/// the middle of a line changes the line from the next character: what a raster bar needs. The
/// CRTC also brings the ULA up to any cycle before it moves there itself, for any reason.
/// </para>
/// <para>
/// <b>What is not exact.</b> A line's bytes are read from RAM when the line is drawn, at its end
/// or at the last write before it, not in each byte's own cycle, so a store to screen memory in
/// the middle of a line's scan is seen by the whole of that line, or none of it, depending on
/// where it fell. Logging every store to RAM to do better would cost every write the CPU makes.
/// The ULA's pipeline, from a byte's fetch to its pixels, is not documented (s6 item 3); the
/// model has none (<see cref="PipelineDelayCharacters"/>). Both are in
/// <c>docs/known-differences.md</c>.
/// </para>
/// <para>
/// <b>Reset.</b> The ULA has no reset pin (s2.1, the pin list in the sheet's S8), so BREAK leaves
/// both registers; the OS writes them again in the mode change every BREAK makes. At power on what
/// they hold is not known, and the model takes zero.
/// </para>
/// </remarks>
public sealed class VideoUla : ICrtcConsumer
{
    /// <summary>
    /// The characters between the ULA taking a byte and its pixels leaving: none, in this model.
    /// The real figure is not documented (<c>video.md</c> s6 item 3). With none, a character's
    /// pixels start in the cycle of its own character clock, and a register write in cycle
    /// <c>t</c> is seen from the first character clocked after <c>t</c>. The drawing is written
    /// for this value; a different one would need the pixels of the characters in the pipe to be
    /// held back past a write.
    /// </summary>
    public const int PipelineDelayCharacters = 0;

    private const uint Black = Framebuffer.Black;
    private const uint Invert = 0x00FFFFFF;
    private const int Width = Framebuffer.PixelsAcross;
    private const int CursorIdle = 4;

    // Physical colours 0 to 7 as 0xAABBGGRR: bit 0 red, bit 1 green, bit 2 blue (s2.3).
    private static readonly uint[] Physical = MakePhysical();

    // For each byte, the palette index for each of sixteen pixel clocks, four bits each, the first
    // in bits 3 to 0: bits 7, 5, 3 and 1 of the shift register after that many shifts filling
    // with 1s, so from the ninth clock on it is 15.
    private static readonly ulong[] IndexesByByte = MakeIndexes();

    // What C1 C0 adds to an address past $7FFF (s2.5, the ROM's pairs).
    private static readonly int[] WrapAdd = [0x4000, 0x6000, 0x3000, 0x5800];

    private readonly byte[] _palette = new byte[16];
    private readonly uint[] _colours = new uint[16];
    private readonly uint[] _scratch = new uint[16];

    // For the pixel rate and clock in force: for each byte, the palette index of each of the
    // character's 16 MHz pixels, sixteen to a byte. Made again only when bits 4 to 2 of the
    // control register change, never for the flash select or the palette.
    private readonly byte[] _pixelIndexes = new byte[256 * 16];
    private int _shape = -1;

    // Each byte's finished pixels for the registers in force, made when the byte is next drawn
    // after a register write: a write only moves _stamp on, so a palette change in the middle of
    // a line costs nothing until the bytes it affects are drawn. Kept two pixels to a ulong, the
    // first pixel in the low half, so a character is copied in four or eight stores; the machine's
    // memory is little-endian on every platform .NET runs it on, which the constructor checks.
    private readonly ulong[] _patterns = new ulong[256 * 8];
    private readonly int[] _patternStamps = new int[256];
    private int _stamp = 1;

    // Rows known to be wholly black, so a line that draws nothing does not paint black over black.
    private readonly bool[] _rowBlack = MakeAllTrue(Framebuffer.Rows);

    // In a machine: where the ULA reads from, and how far it has drawn. _done is the cycle its
    // drawing stands at; _c0 is the CRTC's C0 then; _hd and the histories are the CRTC's
    // horizontal display and DISPTMG and CUDISP before the skew, after that character.
    private readonly BbcClock? _clock;
    private readonly Crtc6845? _crtc;
    private readonly byte[] _ram = [];
    private readonly SystemVia? _systemVia;
    private long _done;
    private int _c0;
    private bool _hd;
    private int _displayHistory, _cursorHistory;
    private int _cursorAge = CursorIdle;
    private bool _drawing;

    // The line being drawn: the cycle of its C0 = 0 character, the next pixel not yet written, and
    // where it goes in the framebuffer (a row's first pixel; -1 for a line not kept).
    private long _lineStart;
    private int _x;
    private int _rowA = -1, _rowB = -1;

    // The field being drawn: how many of its lines have started (up to 256), and how it is woven.
    private int _fieldLines;
    private bool _fieldOdd, _fieldInterlace;

    /// <summary>A ULA on its own: registers and <see cref="Draw"/>, and a picture that is never drawn.</summary>
    public VideoUla()
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("The pixel patterns assume a little-endian machine.");
        }

        WriteControl(0);
    }

    /// <summary>A ULA in a machine, drawing from the CRTC's outputs, RAM and the system VIA's latch.</summary>
    internal VideoUla(BbcClock clock, Crtc6845 crtc, byte[] ram, SystemVia systemVia)
        : this()
    {
        _clock = clock;
        _crtc = crtc;
        _ram = ram;
        _systemVia = systemVia;
        _done = clock.Cycles;
        StartFromCrtc(crtc.StateAt(clock.Cycles));
        crtc.Consumer = this;
        Screen.BeforeRead = SyncToNow;
    }

    /// <summary>The picture.</summary>
    public Framebuffer Screen { get; } = new();

    /// <summary>The control register, as last written.</summary>
    public byte Control { get; private set; }

    /// <summary>Bits 3 and 2 of the control register: 10, 20, 40 or 80 characters a line.</summary>
    public int CharactersPerLine => 10 << ((Control >> 2) & 3);

    /// <summary>Bit 4: the CRTC's clock is 2 MHz (modes 0 to 3) rather than 1 MHz.</summary>
    public bool TwoMhz => (Control & 0x10) != 0;

    /// <summary>Bit 1: the picture is the teletext chip's (mode 7).</summary>
    public bool Teletext => (Control & 0x02) != 0;

    /// <summary>Bit 0: entries with the flash bit show their other colour.</summary>
    public bool FlashSelect => (Control & 0x01) != 0;

    /// <summary>
    /// The cycle of the current line's last character, when the bus next has the ULA draw: from the
    /// CRTC's character at the cycle the drawing stands at, R0 and the clock.
    /// </summary>
    internal long NextEventCycle
    {
        get
        {
            Crtc6845 crtc = _crtc!;
            int r0 = crtc.HorizontalTotal;
            int c0 = _c0;
            long characters = c0 == r0 || c0 == 255 ? r0 + 1 : (c0 < r0 ? r0 : 255) - c0;
            return crtc.TimeAfterCharacters(_done, characters);
        }
    }

    /// <summary>
    /// The RAM address the ULA fetches for the CRTC's <paramref name="memoryAddress"/> (MA) and
    /// <paramref name="rasterAddress"/> (RA), with the system VIA's screen start latch bits
    /// <paramref name="screenStartLatch"/> (bit 0 C0, bit 1 C1), <c>video.md</c> s2.5.
    /// </summary>
    public static int ScreenAddress(int memoryAddress, int rasterAddress, int screenStartLatch)
    {
        if ((memoryAddress & 0x2000) != 0)
        {
            return ((memoryAddress & 0x0800) << 3) | 0x3C00 | (memoryAddress & 0x03FF);
        }

        int address = ((memoryAddress & 0x0FFF) << 3) | (rasterAddress & 7);
        if ((memoryAddress & 0x1000) != 0)
        {
            address = (address + WrapAdd[screenStartLatch & 3]) & 0x7FFF;
        }

        return address;
    }

    /// <summary>Writes the control register, after drawing up to now with the old value.</summary>
    public void WriteControl(byte value)
    {
        SyncToNow();
        Control = value;
        int shape = (value >> 2) & 7;
        if (shape != _shape)
        {
            _shape = shape;
            MakePixelIndexes();
        }
        UpdateColours();
        _stamp++;
    }

    /// <summary>Writes the palette: bits 3 to 0 into the entry bits 7 to 4 name, after drawing up to now.</summary>
    public void WritePalette(byte value)
    {
        SyncToNow();
        int index = value >> 4;
        _palette[index] = (byte)(value & 0x0F);
        _colours[index] = Physical[PhysicalColour(index)];
        _stamp++;
    }

    /// <summary>The physical colour, 0 to 7 (black, red, green, yellow, blue, magenta, cyan, white), palette entry <paramref name="index"/> shows now.</summary>
    public int PhysicalColour(int index)
    {
        int entry = _palette[index & 0x0F];
        return (entry & 8) != 0 && FlashSelect ? entry & 7 : (entry & 7) ^ 7;
    }

    /// <summary>
    /// The pixels one character makes from <paramref name="screenByte"/> with the registers as they
    /// are now, displayed and with no cursor, into <paramref name="pixels"/> in the framebuffer's
    /// 16 MHz pixels: 8 on the 2 MHz clock, 16 on 1 MHz. Returns how many. With the teletext select
    /// on, the picture is the teletext chip's, which is not built yet (task 9): black.
    /// </summary>
    public int Draw(byte screenByte, Span<uint> pixels)
    {
        int width = TwoMhz ? 8 : 16;
        Span<uint> into = pixels[..width];
        if (Teletext)
        {
            into.Fill(Black);
        }
        else
        {
            DrawByte(screenByte, into);
        }

        return width;
    }

    /// <summary>The bus's event: draws every line that has ended by <paramref name="now"/>, and returns the next line end.</summary>
    internal long RenderLinesTo(long now)
    {
        long end;
        while ((end = NextEventCycle) <= now)
        {
            DrawTo(end);
        }

        return end;
    }

    /// <summary>Draws up to now: before a write that changes what the ULA draws, and before the picture is read.</summary>
    internal void SyncToNow()
    {
        if (_clock is not null)
        {
            DrawTo(_clock.Cycles);
        }
    }

    /// <summary>Power on: both registers to zero, drawn up to now first.</summary>
    internal void PowerOn()
    {
        SyncToNow();
        Array.Clear(_palette);
        WriteControl(0);
    }

    void ICrtcConsumer.CatchUpTo(long cycle) => DrawTo(cycle);

    void ICrtcConsumer.CrtcReset()
    {
        // The CRTC's counters are back to 0 in the cycle it stands at: the line and the field
        // being drawn end there, and a new line 0 starts.
        FinishLine();
        EndField(completed: false);
        Crtc6845 crtc = _crtc!;
        _done = crtc.StateCycle;
        StartFromCrtc(crtc.StateForConsumer(_done));
    }

    private static uint[] MakePhysical()
    {
        var colours = new uint[8];
        for (int c = 0; c < 8; c++)
        {
            colours[c] = Black | ((c & 1) != 0 ? 0x000000FFu : 0) | ((c & 2) != 0 ? 0x0000FF00u : 0) | ((c & 4) != 0 ? 0x00FF0000u : 0);
        }
        return colours;
    }

    private static ulong[] MakeIndexes()
    {
        var table = new ulong[256];
        for (int b = 0; b < 256; b++)
        {
            ulong indexes = 0;
            int sr = b;
            for (int clock = 0; clock < 16; clock++)
            {
                int index = (((sr >> 7) & 1) << 3) | (((sr >> 5) & 1) << 2) | (((sr >> 3) & 1) << 1) | ((sr >> 1) & 1);
                indexes |= (ulong)index << (4 * clock);
                sr = ((sr << 1) | 1) & 0xFF;
            }
            table[b] = indexes;
        }
        return table;
    }

    private void UpdateColours()
    {
        for (int i = 0; i < 16; i++)
        {
            _colours[i] = Physical[PhysicalColour(i)];
        }
    }

    /// <summary>
    /// The table <see cref="DrawByte"/> reads: a character lasts pixel rate / CRTC rate pixel
    /// clocks, each 16 / pixel rate of the framebuffer's pixels wide, and clock <c>t</c> looks the
    /// palette up with the index the shift register gives after <c>t</c> shifts.
    /// </summary>
    private void MakePixelIndexes()
    {
        int pixelRate = 2 << ((Control >> 2) & 3);
        int width = 16 / pixelRate;
        int pixels = TwoMhz ? 8 : 16;
        for (int b = 0; b < 256; b++)
        {
            ulong indexes = IndexesByByte[b];
            for (int p = 0; p < pixels; p++)
            {
                _pixelIndexes[(b * 16) + p] = (byte)((indexes >> (4 * (p / width))) & 15);
            }
        }
    }

    /// <summary>One character's pixels from a byte, through the palette as it is now.</summary>
    private void DrawByte(byte screenByte, Span<uint> into)
    {
        if (_patternStamps[screenByte] != _stamp)
        {
            MakePattern(screenByte);
        }

        MemoryMarshal.Cast<ulong, uint>(_patterns.AsSpan(screenByte << 3, 8))[..into.Length].CopyTo(into);
    }

    /// <summary>The sixteen pixels <paramref name="screenByte"/> makes with the registers in force.</summary>
    private void MakePattern(int screenByte)
    {
        _patternStamps[screenByte] = _stamp;
        uint[] colours = _colours;
        int from = screenByte << 4;
        int to = screenByte << 3;
        for (int p = 0; p < 8; p++)
        {
            uint first = colours[_pixelIndexes[from + (2 * p)] & 15];
            uint second = colours[_pixelIndexes[from + (2 * p) + 1] & 15];
            _patterns[to + p] = first | ((ulong)second << 32);
        }
    }

    private static bool[] MakeAllTrue(int length)
    {
        var flags = new bool[length];
        Array.Fill(flags, true);
        return flags;
    }

    /// <summary>
    /// Takes the CRTC's state as the point the drawing stands at, with a new line 0 starting there:
    /// at power on and after a reset of the CRTC, whose C0 = 0 is then never clocked.
    /// </summary>
    private void StartFromCrtc(in CrtcState state)
    {
        _c0 = state.Character;
        _hd = state.HorizontalDisplay;
        _displayHistory = state.DisplayHistory;
        _cursorHistory = state.CursorHistory;
        StartLine(in state, state.Cycle);
    }

    /// <summary>Draws every character clocked up to and including <paramref name="target"/>.</summary>
    private void DrawTo(long target)
    {
        if (_drawing || target <= _done)
        {
            return;
        }

        _drawing = true;
        try
        {
            Crtc6845 crtc = _crtc!;
            while (_done < target)
            {
                // Up to the end of the line, or the target if that comes first. Registers do not
                // change in between (a write draws up to its cycle first), so the line's end is
                // where R0 and the clock now say.
                int r0 = crtc.HorizontalTotal;
                bool ended = _c0 == r0 || _c0 == 255;
                long characters = ended ? r0 + 1 : (_c0 < r0 ? r0 : 255) - _c0;
                long to = Math.Min(crtc.TimeAfterCharacters(_done, characters), target);
                long clocked = crtc.CharactersBetween(_done, to);
                CrtcState state = crtc.StateForConsumer(to);
                if (clocked > 0)
                {
                    long first = crtc.TimeAfterCharacters(_done, 1);
                    if (ended)
                    {
                        FinishLine();
                        if (state.LineInFrame == 0)
                        {
                            EndField(completed: true);
                        }
                        StartLine(in state, first);
                    }

                    int c = ended ? 0 : _c0 + 1;
                    if (c + clocked - 1 != state.Character)
                    {
                        throw new InvalidOperationException(
                            $"The ULA expected character {c + clocked - 1} at cycle {to} and the CRTC is at {state.Character}.");
                    }

                    DrawRun(in state, c, (int)clocked, first);
                }

                _done = to;
                _c0 = state.Character;
                _hd = state.HorizontalDisplay;
                _displayHistory = state.DisplayHistory;
                _cursorHistory = state.CursorHistory;
            }
        }
        finally
        {
            _drawing = false;
        }
    }

    /// <summary>A line starts at <paramref name="lineStart"/>, the cycle of its C0 = 0: where it goes in the framebuffer.</summary>
    private void StartLine(in CrtcState state, long lineStart)
    {
        int line = state.LineInFrame;
        _lineStart = lineStart;
        _x = 0;
        _fieldOdd = state.OddField;
        _fieldInterlace = state.Interlace;
        if (line >= 256)
        {
            _rowA = _rowB = -1;
            return;
        }

        _fieldLines = line + 1;
        if (state.Interlace)
        {
            _rowA = ((2 * line) + (state.OddField ? 1 : 0)) * Width;
            _rowB = -1;
        }
        else
        {
            _rowA = 2 * line * Width;
            _rowB = _rowA + Width;
        }
    }

    /// <summary>The rest of the line the drawing stands in, past its last character, is black.</summary>
    private void FinishLine()
    {
        if (_rowA < 0 || _x >= Width)
        {
            return;
        }

        uint[] pixels = Screen.Buffer;
        pixels.AsSpan(_rowA + _x, Width - _x).Fill(Black);
        if (_rowB >= 0)
        {
            pixels.AsSpan(_rowB + _x, Width - _x).Fill(Black);
        }
        _x = Width;
    }

    /// <summary>A field ends: the rows of the lines it did not reach are black, and a completed one is counted.</summary>
    private void EndField(bool completed)
    {
        uint[] pixels = Screen.Buffer;
        for (int line = _fieldLines; line < 256; line++)
        {
            if (_fieldInterlace)
            {
                int row = (2 * line) + (_fieldOdd ? 1 : 0);
                pixels.AsSpan(row * Width, Width).Fill(Black);
                _rowBlack[row] = true;
            }
            else
            {
                pixels.AsSpan(2 * line * Width, 2 * Width).Fill(Black);
                _rowBlack[2 * line] = _rowBlack[(2 * line) + 1] = true;
            }
        }

        _fieldLines = 0;
        if (completed)
        {
            Screen.FrameDone();
        }
    }

    /// <summary>
    /// Draws <paramref name="count"/> characters of the current line from C0 = <paramref name="c"/>,
    /// the first clocked in <paramref name="firstCycle"/>, with the CRTC's state at the last.
    /// </summary>
    /// <remarks>
    /// Each character's DISPTMG and CUDISP before the skew are the CRTC's rules (its
    /// <c>Step</c>): the horizontal display goes on at C0 = 0 and off at C0 = R1, the display is
    /// that and the vertical display, and the cursor is on where MA is R14 and R15 on a cursor
    /// line. The skews delay both through the same three-character histories the CRTC keeps.
    /// </remarks>
    private void DrawRun(in CrtcState state, int c, int count, long firstCycle)
    {
        uint[] pixels = Screen.Buffer;
        int rowA = _rowA;
        int rowB = _rowB;
        int width = state.CyclesPerCharacter * 8;
        int x = (int)(firstCycle - _lineStart) * 8;
        int copyFrom = Math.Min(Math.Min(_x, x), Width);
        if (rowA >= 0 && x > _x && _x < Width)
        {
            pixels.AsSpan(rowA + _x, Math.Min(x, Width) - _x).Fill(Black);
        }

        int r1 = state.HorizontalDisplayed;
        bool vertical = state.VerticalDisplay;
        bool cursorLine = state.CursorOnLine;
        int cursorAt = state.CursorAddress;
        int lineAddress = state.LineStartAddress;
        int ra = state.RasterAddress;
        int displaySkew = state.DisplaySkew;
        int cursorSkew = state.CursorSkew;
        bool blanked = Teletext || (ra & 8) != 0;
        int latch = _systemVia!.ScreenStartLatch;
        byte[] ram = _ram;
        int control = Control;

        bool hd = c == 0 || _hd;
        int dh = _displayHistory, ch = _cursorHistory;
        int age = _cursorAge;
        int end = c + count;
        if ((blanked || (!vertical && dh == 0)) && !cursorLine && ch == 0 && age >= CursorIdle)
        {
            // Nothing in the run can show, and no cursor can start or be under way: it is all
            // black. Mode 7 until the teletext chip exists, the lines RA3 blanks, and the lines
            // outside the vertical display. A whole line over a row already black paints nothing.
            int stop = x + (count * width);
            if (rowA >= 0 && x < Width)
            {
                if (x == 0 && stop >= Width)
                {
                    PaintBlack(rowA);
                    if (rowB >= 0)
                    {
                        PaintBlack(rowB);
                    }
                    copyFrom = Width;
                }
                else
                {
                    pixels.AsSpan(rowA + x, Math.Min(stop, Width) - x).Fill(Black);
                }
            }
            x = stop;
            c = end;
        }

        Span<uint> row = rowA >= 0 ? pixels.AsSpan(rowA, Width) : default;
        if (rowA >= 0 && c < end && x < Width)
        {
            _rowBlack[rowA / Width] = false;
            if (rowB >= 0)
            {
                _rowBlack[rowB / Width] = false;
            }
        }

        if (displaySkew == 0 && cursorSkew == 0 && !blanked && vertical && hd && age >= CursorIdle && rowA >= 0)
        {
            // The straight path, which is every displayed character of modes 0 to 6 but the
            // cursor's: no skew, nothing blanked, the display on and no cursor started or under
            // way. Each character up to R1, the cursor's character or the right edge is its byte
            // through the palette, and nothing else.
            int last = end;
            if (r1 >= c && r1 < last)
            {
                last = r1;
            }

            if (cursorLine)
            {
                int cursorCharacter = c + ((cursorAt - lineAddress - c) & 0x3FFF);
                last = Math.Min(last, cursorCharacter);
            }

            last = Math.Min(last, c + (x >= Width ? 0 : (Width - x) / width));
            int straight = last - c;
            if (straight > 0)
            {
                Span<ulong> pairs = MemoryMarshal.Cast<uint, ulong>(row);
                ulong[] patterns = _patterns;
                int[] stamps = _patternStamps;
                int stamp = _stamp;
                int halves = width >> 1;
                int pair = x >> 1;
                int low = ra & 7;
                for (; c < last; c++, pair += halves)
                {
                    // MA12 and MA13 clear, which is all of a screen until it wraps: no adder.
                    int ma = (lineAddress + c) & 0x3FFF;
                    int b = ram[(ma & 0x3000) == 0 ? (ma << 3) | low : ScreenAddress(ma, ra, latch)];
                    if (stamps[b] != stamp)
                    {
                        MakePattern(b);
                    }

                    // Four pairs a character at 2 MHz, eight at 1 MHz, through slices of a fixed
                    // length so the copies need no bounds check each.
                    ReadOnlySpan<ulong> from = patterns.AsSpan(b << 3, 8);
                    Span<ulong> into = pairs.Slice(pair, halves);
                    if (halves == 4)
                    {
                        into = into[..4];
                        into[0] = from[0];
                        into[1] = from[1];
                        into[2] = from[2];
                        into[3] = from[3];
                    }
                    else
                    {
                        into = into[..8];
                        into[0] = from[0];
                        into[1] = from[1];
                        into[2] = from[2];
                        into[3] = from[3];
                        into[4] = from[4];
                        into[5] = from[5];
                        into[6] = from[6];
                        into[7] = from[7];
                    }
                }
                x += straight * width;

                // As the CRTC's histories would stand: the display on, no cursor.
                dh = straight >= 3 ? 7 : ((dh << straight) | ((1 << straight) - 1)) & 7;
                ch = straight >= 3 ? 0 : (ch << straight) & 7;
            }
        }

        for (; c < end; c++, x += width)
        {
            if ((!vertical || !hd) && (dh | ch) == 0 && age >= CursorIdle)
            {
                // Nothing more can show on this run: every character left is black.
                int stop = x + ((end - c) * width);
                if (rowA >= 0 && x < Width)
                {
                    pixels.AsSpan(rowA + x, Math.Min(stop, Width) - x).Fill(Black);
                }
                x = stop;
                break;
            }

            if (c == r1)
            {
                hd = false;
            }

            bool on = vertical && hd;
            bool cursor = on && cursorLine && ((lineAddress + c) & 0x3FFF) == cursorAt;
            dh = ((dh << 1) | (on ? 1 : 0)) & 7;
            ch = ((ch << 1) | (cursor ? 1 : 0)) & 7;
            if (cursorSkew != 3 && ((ch >> cursorSkew) & 1) != 0)
            {
                age = 0;
            }
            else if (age < CursorIdle)
            {
                age++;
            }

            if (rowA < 0 || x >= Width)
            {
                continue;
            }

            bool fits = x + width <= Width;
            Span<uint> into = fits ? row.Slice(x, width) : _scratch.AsSpan(0, width);
            if (!blanked && displaySkew != 3 && ((dh >> displaySkew) & 1) != 0)
            {
                DrawByte(ram[ScreenAddress(lineAddress + c, ra, latch)], into);
            }
            else
            {
                into.Fill(Black);
            }

            if (age < CursorIdle && (control & (age == 0 ? 0x80 : age == 1 ? 0x40 : 0x20)) != 0)
            {
                for (int i = 0; i < into.Length; i++)
                {
                    into[i] ^= Invert;
                }
            }

            if (!fits)
            {
                _scratch.AsSpan(0, Width - x).CopyTo(row[x..]);
            }
        }

        _cursorAge = age;
        if (rowA >= 0 && rowB >= 0)
        {
            int copyTo = Math.Min(x, Width);
            if (copyTo > copyFrom)
            {
                pixels.AsSpan(rowA + copyFrom, copyTo - copyFrom).CopyTo(pixels.AsSpan(rowB + copyFrom));
            }
        }
        _x = x;
    }

    /// <summary>Makes the row starting at pixel <paramref name="row"/> black, unless it is known to be.</summary>
    private void PaintBlack(int row)
    {
        int index = row / Width;
        if (!_rowBlack[index])
        {
            Screen.Buffer.AsSpan(row, Width).Fill(Black);
            _rowBlack[index] = true;
        }
    }
}
