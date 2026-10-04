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
/// the middle of a line's scan is seen by the whole of the stretch drawn after it. At 1 MHz a
/// register write in a character's second cycle is applied from the next character, half a
/// character late, because the pipeline is not known well enough to place it inside one.
/// Logging every store to RAM to do better would cost every write the CPU makes.
/// The ULA's pipeline, from a byte's fetch to its pixels, is not documented (s6 item 3); the
/// model has none (<see cref="PipelineDelayCharacters"/>). Both are in
/// <c>docs/known-differences.md</c>.
/// </para>
/// <para>
/// <b>Reset.</b> The ULA has no reset pin (s2.1, the pin list in the sheet's S8), so BREAK leaves
/// both registers; the OS writes them again in the mode change every BREAK makes. At power on what
/// they hold is not known, and the model takes zero.
/// </para>
/// <para>
/// <b>Mode 7 (s4).</b> With the teletext select on, the picture is the SAA5050's
/// (<see cref="TeletextChip"/>), passed through without the palette. The chip latches each byte
/// the CRTC fetches, with LOSE, which is DISPTMG after the R8 skew, one character later: so the
/// byte fetched in character <c>c</c> is shown if DISPTMG is high in character <c>c + 1</c>, which
/// is what the one-character display skew of mode 7's R8 (&amp;93) lines up. The chip's output
/// then leaves <see cref="TeletextDelayCharacters"/> characters after the fetch, which is what the
/// two-character cursor skew and the ULA's cursor segment 1 line up, so the cursor is drawn over
/// the cell at its address. The framebuffer puts each cell where its byte was fetched, so mode 7
/// fills the same 640 pixels as the other modes: a cell is drawn as its character's output
/// arrives, three characters back. The three cells in the chip when the select changes in the
/// middle of a line are black, and the RA3 gate is off. The chip's own line, row, double height
/// and flash state moves on at every line end in every mode: DEW is VSYNC's rise as seen at a
/// line's end, and a line counts if DISPTMG after the skew was high in any of its characters.
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

    /// <summary>
    /// The characters from the CRTC's fetch of a mode 7 byte to the SAA5050's picture of it
    /// leaving the chip: three. The datasheet gives about 2.6 us for graphics and 2.77 us for
    /// alphanumerics from the chip's input, a character being 1 us (<c>video.md</c> s4.1); the R8
    /// skews the OS writes for mode 7 (CUDISP two characters, then the ULA's cursor segment 1, one
    /// more) line the cursor up three characters after the fetch; and mode 7 is said to sit one
    /// character right of mode 6 with its HSYNC two characters later, which is three again. No
    /// source gives the figure itself (s6 item 5).
    /// </summary>
    public const int TeletextDelayCharacters = 3;

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

    // Rows known to be wholly black, and how many are not. A flag is cleared before anything but
    // black is written to its row, so a set flag always means the row is black, and every black
    // write over a flagged row is skipped: a line that draws nothing paints nothing, and a field
    // that ends early clears only the rows that need it. Before the OS has programmed the CRTC its
    // registers are zero and a frame is a line long, so this is what keeps power on cheap.
    private readonly bool[] _rowBlack = MakeAllTrue(Framebuffer.Rows);
    private int _rowsNotBlack;

    // Whether the line being drawn has written nothing but black, so its rows are black when it
    // ends, however many pieces it was drawn in.
    private bool _lineBlack;

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

    // For each 12-bit half-dot mask from the teletext chip, which of a cell's 16 pixels (1 MHz) or
    // 8 (2 MHz) are foreground: a pixel shows the half-dot under its centre.
    private static readonly ushort[] HalfDotsTo16 = MakeHalfDotPixels(16);
    private static readonly ushort[] HalfDotsTo8 = MakeHalfDotPixels(8);

    // For writing teletext cells four pixels at a time: for each pair of physical colours
    // (foreground * 8 + background) and each pattern of four pixels (bit 0 the left one), the four
    // pixels as two pairs, 32 ulongs a colour pair. The cursor's inversion maps a physical colour
    // to another (c XOR 7), so the 64 pairs are every cell there can be, and the table is made
    // once rather than again at each change of colour.
    private static readonly ulong[] QuadsByColours = MakeQuads();

    // The teletext chip's pipeline: the bytes of the line's last four characters, the newest in
    // bits 0 to 7, and for the three before the newest whether LOSE came with it (DISPTMG after the
    // skew in the character after), the newest in bit 0; the first character of the line whose
    // byte the chip took while the teletext select was on, in an unbroken stretch, or -1; whether
    // the last cell drawn had LOSE; and whether nothing more on this line can be shown.
    private uint _ttxByteRing;
    private int _ttxLoseRing;
    private int _ttxFirst = -1;
    private bool _ttxShowing;
    private bool _ttxDark;


    // For the chip's own count: DISPTMG after the skew was high in a character of this line; VSYNC
    // at the end of the last stretch drawn, and at the end of the line before.
    private bool _loseSeen;
    private bool _vsyncNow, _vsyncAtLastLineEnd;

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
        crtc.SetCharacterClock(TwoMhz);
        systemVia.ScreenLatchChanging += SyncToNow;
        Screen.BeforeRead = SyncToNow;
    }

    /// <summary>The picture.</summary>
    public Framebuffer Screen { get; } = new();

    /// <summary>The SAA5050 teletext chip, whose picture the ULA shows with the teletext select on.</summary>
    public Teletext TeletextChip { get; } = new();

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
            return TeletextAddress(memoryAddress);
        }

        int address = ((memoryAddress & 0x0FFF) << 3) | (rasterAddress & 7);
        if ((memoryAddress & 0x1000) != 0)
        {
            address = (address + WrapAdd[screenStartLatch & 3]) & 0x7FFF;
        }

        return address;
    }

    /// <summary>
    /// The TTX VDU path of <see cref="ScreenAddress"/>, for an MA with MA13 high: RA is ignored,
    /// DA14 is MA11, DA13 to DA10 are high and DA9 to DA0 are MA9 to MA0, so the byte is in
    /// &amp;3C00 to &amp;3FFF or &amp;7C00 to &amp;7FFF (<c>video.md</c> s2.5). The one place the
    /// formula is written; the mode 7 drawing calls it for every character.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int TeletextAddress(int memoryAddress) =>
        ((memoryAddress & 0x0800) << 3) | 0x3C00 | (memoryAddress & 0x03FF);

    /// <summary>
    /// Writes the control register, after drawing up to now with the old value. In a machine bit 4
    /// is the CRTC's clock, and the ULA sets it here, so a write by any path keeps the two agreeing.
    /// </summary>
    public void WriteControl(byte value)
    {
        SyncToNow();
        Control = value;
        _crtc?.SetCharacterClock((value & 0x10) != 0);
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
    /// on, the picture is the teletext chip's, and a teletext cell depends on the codes before it
    /// on its row, so one byte on its own draws black here; the machine draws mode 7 through
    /// <see cref="TeletextChip"/>.
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

    /// <summary>Power on: both registers to zero, drawn up to now first, and the teletext chip's counts to zero.</summary>
    internal void PowerOn()
    {
        SyncToNow();
        Array.Clear(_palette);
        WriteControl(0);
        TeletextChip.PowerOn();
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
        _vsyncNow = state.VSync;
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
                _vsyncNow = state.VSync;
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
        _lineBlack = true;

        // The teletext chip's line: CRS is RA0 inverted (s1.5), and its pipeline starts empty.
        TeletextChip.BeginLine((state.RasterAddress & 1) == 0);
        _ttxFirst = -1;
        _ttxShowing = false;
        _ttxDark = false;
        _ttxByteRing = 0;
        _ttxLoseRing = 0;
        _loseSeen = false;
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

    /// <summary>
    /// The line the drawing stands in has ended: the teletext chip counts it, and the rest of the
    /// line past its last character is black.
    /// </summary>
    private void FinishLine()
    {
        // DEW is VSYNC; the model sees it at line ends, which every VSYNC pulse, a line or more
        // long, spans. A field start takes the place of the line count.
        bool fieldStart = _vsyncNow && !_vsyncAtLastLineEnd;
        _vsyncAtLastLineEnd = _vsyncNow;
        if (fieldStart)
        {
            TeletextChip.FieldStart();
        }
        else
        {
            TeletextChip.LineEnd(_loseSeen);
        }

        if (_rowA < 0)
        {
            return;
        }

        if (_x < Width)
        {
            if (!_rowBlack[_rowA / Width])
            {
                FillBlack(_rowA + _x, Width - _x);
            }

            if (_rowB >= 0 && !_rowBlack[_rowB / Width])
            {
                FillBlack(_rowB + _x, Width - _x);
            }
            _x = Width;
        }

        if (_lineBlack)
        {
            KnownBlack(_rowA);
            if (_rowB >= 0)
            {
                KnownBlack(_rowB);
            }
        }
    }

    /// <summary>A field ends: the rows of the lines it did not reach are black, and a completed one is counted.</summary>
    private void EndField(bool completed)
    {
        for (int line = _fieldLines; line < 256 && _rowsNotBlack > 0; line++)
        {
            if (_fieldInterlace)
            {
                PaintBlack(((2 * line) + (_fieldOdd ? 1 : 0)) * Width);
            }
            else
            {
                PaintBlack(2 * line * Width);
                PaintBlack(((2 * line) + 1) * Width);
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
        if (Teletext)
        {
            DrawTeletextRun(in state, c, count, firstCycle);
            return;
        }

        // The ULA's own picture: the cells in the teletext chip's pipeline are not shown, and the
        // chip's line count still needs to know whether LOSE came on this line.
        _ttxFirst = -1;
        _ttxShowing = false;
        _loseSeen = _loseSeen || AnyDisplayIn(c, count, state.DisplaySkew, _displayHistory, c == 0 || _hd, state.VerticalDisplay, state.HorizontalDisplayed);

        uint[] pixels = Screen.Buffer;
        int rowA = _rowA;
        int rowB = _rowB;
        int width = state.CyclesPerCharacter * 8;
        int x = (int)(firstCycle - _lineStart) * 8;
        int copyFrom = Math.Min(Math.Min(_x, x), Width);
        if (rowA >= 0 && x > _x && _x < Width && !_rowBlack[rowA / Width])
        {
            FillBlack(rowA + _x, Math.Min(x, Width) - _x);
        }

        int r1 = state.HorizontalDisplayed;
        bool vertical = state.VerticalDisplay;
        bool cursorLine = state.CursorOnLine;
        int cursorAt = state.CursorAddress;
        int lineAddress = state.LineStartAddress;
        int ra = state.RasterAddress;
        int displaySkew = state.DisplaySkew;
        int cursorSkew = state.CursorSkew;
        bool blanked = (ra & 8) != 0;
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
            // black. The lines RA3 blanks, and the lines outside the vertical display. A whole
            // line over a row already black paints nothing.
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
                else if (!_rowBlack[rowA / Width])
                {
                    FillBlack(rowA + x, Math.Min(stop, Width) - x);
                }
            }
            x = stop;
            c = end;
        }

        Span<uint> row = rowA >= 0 ? pixels.AsSpan(rowA, Width) : default;
        if (rowA >= 0 && c < end && x < Width)
        {
            Drawn(rowA);
            if (rowB >= 0)
            {
                Drawn(rowB);
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
                Screen.Wrote(straight * width);

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
                    FillBlack(rowA + x, Math.Min(stop, Width) - x);
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
            Screen.Wrote(fits ? width : Width - x);
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
        if (rowA >= 0 && rowB >= 0 && !(_rowBlack[rowA / Width] && _rowBlack[rowB / Width]))
        {
            // Both rows black already: nothing to copy.
            int copyTo = Math.Min(x, Width);
            if (copyTo > copyFrom)
            {
                pixels.AsSpan(rowA + copyFrom, copyTo - copyFrom).CopyTo(pixels.AsSpan(rowB + copyFrom));
                Screen.Wrote(copyTo - copyFrom);
            }
        }
        _x = x;
    }

    /// <summary>
    /// Mode 7: <paramref name="count"/> characters of the current line from C0 = <paramref name="c"/>,
    /// as the teletext chip sees them, and the cells whose output leaves the chip in them.
    /// </summary>
    /// <remarks>
    /// Each character the chip takes its byte, and the byte before it takes LOSE, DISPTMG after the
    /// skew in this character. The cell fetched <see cref="TeletextDelayCharacters"/> characters
    /// ago leaves the chip now: the chip gives its colours if it had LOSE (the row's defaults come
    /// back when LOSE rises), black if not, the ULA's cursor inverts it as it does in every mode,
    /// and it is drawn where it was fetched. DISPTMG, CUDISP and the cursor's segments are worked
    /// out each character the way <see cref="DrawRun"/> does. Once neither the display nor the
    /// cursor can come on again in the line and no cell with LOSE is left in the chip, every cell
    /// left in the line is black: from then on a run paints the black its cells cover in one go
    /// (<see cref="PaintDarkCells"/>) instead of working each character out.
    /// </remarks>
    private void DrawTeletextRun(in CrtcState state, int c, int count, long firstCycle)
    {
        int width = state.CyclesPerCharacter * 8;
        int xc = (int)(firstCycle - _lineStart) * 8;
        if (_ttxDark)
        {
            PaintDarkCells(c, c + count, xc, width);
            return;
        }

        uint[] pixels = Screen.Buffer;
        int rowA = _rowA;
        int rowB = _rowB;
        int r1 = state.HorizontalDisplayed;
        bool vertical = state.VerticalDisplay;
        bool cursorLine = state.CursorOnLine;
        int cursorAt = state.CursorAddress;
        int lineAddress = state.LineStartAddress;
        int ra = state.RasterAddress;
        int displaySkew = state.DisplaySkew;
        int cursorSkew = state.CursorSkew;
        int latch = _systemVia!.ScreenStartLatch;
        byte[] ram = _ram;
        int control = Control;
        Teletext chip = TeletextChip;

        if (c == 0 && vertical && displaySkew == 1 && r1 >= 1 && r1 + TeletextDelayCharacters < count
            && _cursorHistory == 0 && _cursorAge >= CursorIdle
            && (cursorSkew == 3 || !cursorLine || ((cursorAt - lineAddress) & 0x3FFF) >= r1))
        {
            // The straight path, which is every line of the OS's mode 7 but the cursor's: the run
            // starts the line, DISPTMG is skewed one character as mode 7's R8 has it, no cursor is
            // under way or can start, and the run reaches past the display. Then what the steps
            // below work out is fixed: the byte of character f has LOSE exactly when f is below R1,
            // so cells 0 to R1 - 1 are shown, from the row's defaults, each where it was fetched;
            // LOSE came in the line; and the line goes dark at character R1 + 3.
            _ttxFirst = 0;
            chip.StartDisplay();
            int shown = Math.Min(r1, rowA < 0 ? 0 : (Width - xc + width - 1) / width);
            bool rowKnownBlack = rowA >= 0 && _rowBlack[rowA / Width];
            for (int f = 0; f < r1; f++)
            {
                int ma = (lineAddress + f) & 0x3FFF;
                int address = (ma & 0x2000) != 0 ? TeletextAddress(ma) : ScreenAddress(ma, ra, latch);
                int packed = chip.Cell(ram[address]);
                if (f >= shown)
                {
                    continue;
                }

                // A cell that is all black, black on black or a blank on black, over a row known
                // to be black paints nothing; any other cell is PutTeletextCell's.
                bool blackCell = (packed & 0x70000) == 0 && ((packed & 0xFFF) == 0 || (packed & 0x7000) == 0);
                if (!(blackCell && rowKnownBlack))
                {
                    int x = xc + (f * width);
                    PutTeletextCell(pixels, rowA, rowB, x, Math.Min(width, Width - x), width, packed & 0xFFF, (packed >> 12) & 7, (packed >> 16) & 7);
                    rowKnownBlack = _rowBlack[rowA / Width];
                }
            }

            int drawnTo = Math.Min(xc + (shown * width), Width);
            if (rowA >= 0 && rowB >= 0 && drawnTo > xc && !(_rowBlack[rowA / Width] && _rowBlack[rowB / Width]))
            {
                pixels.AsSpan(rowA + xc, drawnTo - xc).CopyTo(pixels.AsSpan(rowB + xc));
                Screen.Wrote(drawnTo - xc);
            }
            _x = Math.Max(_x, drawnTo);
            _loseSeen = true;
            _ttxShowing = false;
            _ttxDark = true;
            int darkFrom = r1 + TeletextDelayCharacters;
            PaintDarkCells(darkFrom, count, xc + (darkFrom * width), width);
            return;
        }

        bool hd = c == 0 || _hd;
        int dh = _displayHistory, ch = _cursorHistory;
        int age = _cursorAge;
        bool loseSeen = _loseSeen;
        bool showing = _ttxShowing;
        uint bytes = _ttxByteRing;
        int lose = _ttxLoseRing;
        if (_ttxFirst < 0)
        {
            _ttxFirst = c;
        }
        int first = _ttxFirst;
        int lo = Width, hi = 0;
        int end = c + count;
        for (; c < end; c++, xc += width)
        {
            if ((!vertical || !hd || c == r1) && (dh | ch | (lose & 3)) == 0 && age >= CursorIdle)
            {
                // Nothing on the rest of this line can have LOSE or a cursor, and the chip holds no
                // cell with LOSE: every cell from here to the line's end is black.
                _ttxDark = true;
                showing = false;
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

            // This character's byte goes into the chip, and the byte before takes its LOSE.
            bool display = displaySkew != 3 && ((dh >> displaySkew) & 1) != 0;
            loseSeen |= display;
            int ma = (lineAddress + c) & 0x3FFF;
            int address = (ma & 0x2000) != 0 ? TeletextAddress(ma) : ScreenAddress(ma, ra, latch);
            bytes = (bytes << 8) | ram[address];
            lose = ((lose << 1) | (display ? 1 : 0)) & 7;

            // The cell fetched three characters ago leaves the chip: its byte is bits 24 to 31 of
            // the ring and its LOSE bit 2.
            if (c - TeletextDelayCharacters < first)
            {
                continue;
            }

            int foreground = 0, background = 0;
            int mask = 0;
            if ((lose & 4) != 0)
            {
                if (!showing)
                {
                    chip.StartDisplay();
                    showing = true;
                }

                int packed = chip.Cell((int)(bytes >> 24));
                mask = packed & 0xFFF;
                foreground = (packed >> 12) & 7;
                background = (packed >> 16) & 7;
            }
            else
            {
                showing = false;
            }

            if (age < CursorIdle && (control & (age == 0 ? 0x80 : age == 1 ? 0x40 : 0x20)) != 0)
            {
                // The cursor inverts the picture: physical colour c becomes c XOR 7.
                foreground ^= 7;
                background ^= 7;
            }

            int x = xc - (TeletextDelayCharacters * width);
            if (rowA < 0 || x < 0 || x >= Width)
            {
                continue;
            }

            if (x > _x)
            {
                if (!_rowBlack[rowA / Width])
                {
                    FillBlack(rowA + _x, x - _x);
                }
                lo = Math.Min(lo, _x);
            }

            int n = Math.Min(width, Width - x);
            PutTeletextCell(pixels, rowA, rowB, x, n, width, mask, foreground, background);
            lo = Math.Min(lo, x);
            hi = Math.Max(hi, x + n);
            _x = Math.Max(_x, x + n);
        }

        _cursorAge = age;
        _loseSeen = loseSeen;
        _ttxShowing = showing;
        _ttxByteRing = bytes;
        _ttxLoseRing = lose;
        if (rowA >= 0 && rowB >= 0 && hi > lo && !(_rowBlack[rowA / Width] && _rowBlack[rowB / Width]))
        {
            pixels.AsSpan(rowA + lo, hi - lo).CopyTo(pixels.AsSpan(rowB + lo));
            Screen.Wrote(hi - lo);
        }

        if (c < end)
        {
            PaintDarkCells(c, end, xc, width);
        }
    }

    /// <summary>
    /// One teletext cell's <paramref name="n"/> pixels (all <paramref name="width"/> but at the
    /// picture's right edge) at <paramref name="x"/> on row A, in physical colours 0 to 7: the
    /// background, and the foreground where the half-dot under a pixel is set. A black cell over a
    /// row known to be black paints nothing; anything else marks the line's rows as drawn first,
    /// once a line, since nothing marks them black again before the line ends. A whole cell is
    /// written four pixels at a time from <see cref="QuadsByColours"/>.
    /// </summary>
    private void PutTeletextCell(uint[] pixels, int rowA, int rowB, int x, int n, int width, int mask, int foreground, int background)
    {
        if (background == 0 && (mask == 0 || foreground == 0))
        {
            if (!_rowBlack[rowA / Width])
            {
                pixels.AsSpan(rowA + x, n).Fill(Black);
                Screen.Wrote(n);
            }
            return;
        }

        if (_lineBlack)
        {
            Drawn(rowA);
            if (rowB >= 0)
            {
                Drawn(rowB);
            }
        }

        int bits = (width == 16 ? HalfDotsTo16 : HalfDotsTo8)[mask];
        Screen.Wrote(n);
        if (n != width)
        {
            Span<uint> part = pixels.AsSpan(rowA + x, n);
            for (int p = 0; p < part.Length; p++)
            {
                part[p] = Physical[((bits >> p) & 1) != 0 ? foreground : background];
            }
            return;
        }

        ReadOnlySpan<ulong> quads = QuadsByColours.AsSpan(((foreground * 8) + background) * 32, 32);
        Span<ulong> pairs = MemoryMarshal.Cast<uint, ulong>(pixels.AsSpan(rowA + x, width));
        if (width == 16)
        {
            pairs = pairs[..8];
            int q0 = (bits & 15) * 2, q1 = ((bits >> 4) & 15) * 2, q2 = ((bits >> 8) & 15) * 2, q3 = ((bits >> 12) & 15) * 2;
            pairs[0] = quads[q0];
            pairs[1] = quads[q0 + 1];
            pairs[2] = quads[q1];
            pairs[3] = quads[q1 + 1];
            pairs[4] = quads[q2];
            pairs[5] = quads[q2 + 1];
            pairs[6] = quads[q3];
            pairs[7] = quads[q3 + 1];
        }
        else
        {
            pairs = pairs[..4];
            int q0 = (bits & 15) * 2, q1 = ((bits >> 4) & 15) * 2;
            pairs[0] = quads[q0];
            pairs[1] = quads[q0 + 1];
            pairs[2] = quads[q1];
            pairs[3] = quads[q1 + 1];
        }
    }

    private static ulong[] MakeQuads()
    {
        var table = new ulong[64 * 32];
        for (int pair = 0; pair < 64; pair++)
        {
            uint foreground = Physical[pair >> 3], background = Physical[pair & 7];
            for (int q = 0; q < 16; q++)
            {
                table[(pair * 32) + (2 * q)] = ((q & 1) != 0 ? foreground : background) | ((ulong)((q & 2) != 0 ? foreground : background) << 32);
                table[(pair * 32) + (2 * q) + 1] = ((q & 4) != 0 ? foreground : background) | ((ulong)((q & 8) != 0 ? foreground : background) << 32);
            }
        }
        return table;
    }

    /// <summary>
    /// After the line has gone dark: what drawing characters <paramref name="from"/> to
    /// <paramref name="to"/> (the first at <paramref name="xc"/>) one at a time would paint, every
    /// cell black. A cell is drawn <see cref="TeletextDelayCharacters"/> characters back from its
    /// character, so at a slower clock than the cells before it it can land on them, which is why
    /// the rest of the line cannot simply be left to the line's end. Cells fetched before the
    /// chip's stretch began, or off either side of the picture, are not drawn, and a gap before the
    /// first is painted black, as in <see cref="DrawTeletextRun"/>.
    /// </summary>
    private void PaintDarkCells(int from, int to, int xc, int width)
    {
        int rowA = _rowA;
        int delay = TeletextDelayCharacters * width;
        int start = Math.Max(from, _ttxFirst + TeletextDelayCharacters);
        int x = xc + ((start - from) * width) - delay;
        if (x < 0)
        {
            int skip = (-x + width - 1) / width;
            start += skip;
            x += skip * width;
        }

        if (rowA < 0 || start >= to || x >= Width)
        {
            return;
        }

        int endX = Math.Min(xc + ((to - from) * width) - delay, Width);
        int paintFrom = Math.Min(_x, x);
        if (!_rowBlack[rowA / Width])
        {
            FillBlack(rowA + paintFrom, endX - paintFrom);
        }

        int rowB = _rowB;
        if (rowB >= 0 && !_rowBlack[rowB / Width])
        {
            FillBlack(rowB + paintFrom, endX - paintFrom);
        }
        _x = Math.Max(_x, endX);
    }

    /// <summary>
    /// Whether DISPTMG after the skew is high in any of <paramref name="count"/> characters from
    /// C0 = <paramref name="c"/>, worked out at once from the rules <see cref="DrawRun"/> steps:
    /// character <c>t</c> shows DISPTMG before the skew of character <c>t - skew</c>, which is in
    /// <paramref name="history"/> (bit 0 the character before <paramref name="c"/>) when it comes
    /// before the run, and otherwise is on from <paramref name="c"/> while the vertical and the
    /// horizontal display are, the horizontal going off at R1.
    /// </summary>
    private static bool AnyDisplayIn(int c, int count, int skew, int history, bool horizontal, bool vertical, int r1)
    {
        if (skew == 3)
        {
            return false;
        }

        for (int back = 1; back <= skew; back++)
        {
            if (skew - back < count && ((history >> (back - 1)) & 1) != 0)
            {
                return true;
            }
        }

        return count > skew && vertical && horizontal && r1 != c;
    }

    private static ushort[] MakeHalfDotPixels(int pixels)
    {
        var table = new ushort[1 << BbcMicro.Teletext.CellWidth];
        for (int mask = 0; mask < table.Length; mask++)
        {
            int bits = 0;
            for (int p = 0; p < pixels; p++)
            {
                int halfDot = ((2 * p) + 1) * BbcMicro.Teletext.CellWidth / (2 * pixels);
                if (((mask >> halfDot) & 1) != 0)
                {
                    bits |= 1 << p;
                }
            }
            table[mask] = (ushort)bits;
        }
        return table;
    }

    /// <summary>Makes the row starting at pixel <paramref name="row"/> black, unless it is known to be.</summary>
    private void PaintBlack(int row)
    {
        if (!_rowBlack[row / Width])
        {
            FillBlack(row, Width);
            KnownBlack(row);
        }
    }

    /// <summary>The row starting at pixel <paramref name="row"/> is all black now.</summary>
    private void KnownBlack(int row)
    {
        int index = row / Width;
        if (!_rowBlack[index])
        {
            _rowBlack[index] = true;
            _rowsNotBlack--;
        }
    }

    /// <summary>The row starting at pixel <paramref name="row"/> is about to get more than black.</summary>
    private void Drawn(int row)
    {
        _lineBlack = false;
        int index = row / Width;
        if (_rowBlack[index])
        {
            _rowBlack[index] = false;
            _rowsNotBlack++;
        }
    }

    private void FillBlack(int from, int length)
    {
        Screen.Buffer.AsSpan(from, length).Fill(Black);
        Screen.Wrote(length);
    }
}
