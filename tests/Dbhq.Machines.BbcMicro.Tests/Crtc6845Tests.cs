using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The 6845 CRTC with the registers the OS writes, against the numbers in <c>video.md</c>
/// section 5, and the two quirks section 1.6 asks for. A chip on its own counts one
/// <see cref="Crtc6845.Tick"/> a character; at 2 MHz that is one CPU cycle, at 1 MHz two.
/// </summary>
public class Crtc6845Tests
{
    /// <summary>
    /// R0 to R11 as MOS 1.20 stores them (video.md s3.1, from the ROM at $C46E), for modes 0 to 2,
    /// 3, 4 and 5, 6, and 7, with R12 and R13 the screen start the OS writes (s3.1).
    /// </summary>
    private static readonly byte[][] Tables =
    [
        [0x7F, 0x50, 0x62, 0x28, 0x26, 0x00, 0x20, 0x22, 0x01, 0x07, 0x67, 0x08, 0x06, 0x00],
        [0x7F, 0x50, 0x62, 0x28, 0x1E, 0x02, 0x19, 0x1B, 0x01, 0x09, 0x67, 0x09, 0x08, 0x00],
        [0x3F, 0x28, 0x31, 0x24, 0x26, 0x00, 0x20, 0x22, 0x01, 0x07, 0x67, 0x08, 0x0B, 0x00],
        [0x3F, 0x28, 0x31, 0x24, 0x1E, 0x02, 0x19, 0x1B, 0x01, 0x09, 0x67, 0x09, 0x0C, 0x00],
        [0x3F, 0x28, 0x33, 0x24, 0x1E, 0x02, 0x19, 0x1B, 0x93, 0x12, 0x72, 0x13, 0x28, 0x00],
    ];

    /// <summary>The OS's table set for each mode (video.md s3.1, the group index at $C440).</summary>
    private static int SetOf(int mode) => new[] { 0, 0, 0, 1, 2, 2, 3, 4 }[mode];

    /// <summary>CPU cycles per character: modes 0 to 3 run the CRTC at 2 MHz, 4 to 7 at 1 MHz (s2.2).</summary>
    private static int CyclesPerCharacter(int mode) => mode < 4 ? 1 : 2;

    /// <summary>
    /// Writes R11 down to R0 as the OS does on a mode change, then R12 and R13 (video.md s3.3):
    /// R7 is the table value plus one, and R8, when its bit 7 is clear, is the table value EOR 1
    /// after <c>*TV x,1</c>. Then a reset, so every test starts at the first field.
    /// </summary>
    private static Crtc6845 Programmed(int mode, bool interlaceOff = false)
    {
        var crtc = new Crtc6845();
        byte[] table = Tables[SetOf(mode)];
        for (int r = 11; r >= 0; r--)
        {
            byte value = table[r];
            if (r == 7)
            {
                value++;
            }
            else if (r == 8 && (value & 0x80) == 0 && interlaceOff)
            {
                value ^= 1;
            }
            Write(crtc, r, value);
        }

        Write(crtc, 12, table[12]);
        Write(crtc, 13, table[13]);
        crtc.Reset();
        return crtc;
    }

    private static void Write(Crtc6845 crtc, int register, byte value)
    {
        crtc.WriteAddress((byte)register);
        crtc.WriteData(value);
    }

    /// <summary>The character numbers at which VSYNC fell, over <paramref name="characters"/> characters.</summary>
    private static List<long> VsyncFalls(Crtc6845 crtc, long characters)
    {
        var falls = new List<long>();
        bool before = crtc.VSync;
        for (long t = 1; t <= characters; t++)
        {
            crtc.Tick();
            bool now = crtc.VSync;
            if (before && !now)
            {
                falls.Add(t);
            }
            before = now;
        }
        return falls;
    }

    private static void Run(Crtc6845 crtc, long characters)
    {
        for (long t = 0; t < characters; t++)
        {
            crtc.Tick();
        }
    }

    /// <summary>Ticks until a frame starts; the chip then stands at C0 = 0 of line 0.</summary>
    private static void RunToFrameStart(Crtc6845 crtc)
    {
        bool started = false;
        void OnFrame() => started = true;
        crtc.FrameStarted += OnFrame;
        for (int t = 0; t < 400_000 && !started; t++)
        {
            crtc.Tick();
            _ = crtc.VSync; // look, so the chip catches up in this character
        }
        crtc.FrameStarted -= OnFrame;
        Assert.True(started, "no frame started");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    public void WithTheOsDefaultsVsyncFallsEvery40000CpuCycles(int mode)
    {
        // video.md s5: interlace sync is on by default, 625 lines a frame at 128 CPU cycles a line,
        // and the even field's VSYNC half a line late, so 312.5 lines from fall to fall.
        Crtc6845 crtc = Programmed(mode);
        int perCharacter = CyclesPerCharacter(mode);
        List<long> falls = VsyncFalls(crtc, 13 * 40_000 / perCharacter);

        Assert.True(falls.Count >= 12, $"only {falls.Count} falls");
        for (int i = 2; i < 12; i++)
        {
            Assert.Equal(40_000, (falls[i] - falls[i - 1]) * perCharacter);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void WithInterlaceOffVsyncFallsEvery39936CpuCycles(int mode)
    {
        // *TV 0,1 makes R8 = 0: 312 lines a field, 50.08 Hz (video.md s5).
        Crtc6845 crtc = Programmed(mode, interlaceOff: true);
        int perCharacter = CyclesPerCharacter(mode);
        List<long> falls = VsyncFalls(crtc, 13 * 40_000 / perCharacter);

        for (int i = 2; i < 12; i++)
        {
            Assert.Equal(39_936, (falls[i] - falls[i - 1]) * perCharacter);
        }
    }

    [Fact]
    public void AnInterlacedFrameIs625LinesWithVsyncStartingAtLine280()
    {
        // Mode 7, R8 = &93, R9 = 18 in interlace sync and video: 10 lines a row in each field,
        // 31 rows and 2 adjust lines, one field a line longer, VSYNC at row 28 (video.md s5).
        Crtc6845 crtc = Programmed(7);
        RunToFrameStart(crtc);
        RunToFrameStart(crtc);

        var linesBeforeVsync = new List<int>();
        var linesPerField = new List<int>();
        int lines = 0;
        bool hsync = crtc.HSync, vsync = crtc.VSync;
        crtc.FrameStarted += () =>
        {
            linesPerField.Add(lines);
            lines = 0;
        };

        for (int t = 0; t < 4 * 313 * 64; t++)
        {
            crtc.Tick();
            if (crtc.HSync && !hsync)
            {
                lines++;
            }
            if (crtc.VSync && !vsync)
            {
                linesBeforeVsync.Add(lines);
            }
            hsync = crtc.HSync;
            vsync = crtc.VSync;
        }

        Assert.True(linesPerField.Count >= 4, $"only {linesPerField.Count} fields");
        Assert.Equal(625, linesPerField[0] + linesPerField[1]);
        Assert.Equal(625, linesPerField[2] + linesPerField[3]);
        Assert.Equal(new[] { 312, 313 }, linesPerField.Take(2).Order());
        Assert.All(linesBeforeVsync, n => Assert.Equal(280, n));
    }

    [Fact]
    public void InMode7EachFieldRowHasTenLinesOfAlternateRasters()
    {
        // RA steps by two: 0, 2 ... 18 in one field and 1, 3 ... 19 in the other (video.md s1.4).
        Crtc6845 crtc = Programmed(7);
        RunToFrameStart(crtc);

        var fields = new List<List<int>>();
        for (int field = 0; field < 2; field++)
        {
            var rasters = new List<int>();
            for (int line = 0; line < 10; line++)
            {
                rasters.Add(crtc.RasterAddress);
                Run(crtc, 64);
            }
            Assert.Equal(0, crtc.RasterAddress & ~1); // the next row starts again at 0 or 1
            fields.Add(rasters);
            RunToFrameStart(crtc);
        }

        int[] even = [0, 2, 4, 6, 8, 10, 12, 14, 16, 18];
        int[] odd = [1, 3, 5, 7, 9, 11, 13, 15, 17, 19];
        Assert.Contains(fields, f => f.SequenceEqual(even));
        Assert.Contains(fields, f => f.SequenceEqual(odd));
    }

    [Fact]
    public void Mode4LinesAre64CharactersWith40DisplayedAndHsyncAt49ForFour()
    {
        Crtc6845 crtc = Programmed(4);
        RunToFrameStart(crtc);

        // Line 0 of the second field is displayed; the chip stands at its C0 = 0. Record a line
        // and the first character of the next.
        var display = new List<bool>();
        var hsync = new List<bool>();
        for (int c = 0; c <= 64; c++)
        {
            display.Add(crtc.DisplayEnable);
            hsync.Add(crtc.HSync);
            crtc.Tick();
        }

        Assert.Equal(Enumerable.Range(0, 40), display.Select((on, c) => (on, c)).Where(x => x.on).Select(x => x.c).Where(c => c < 64));
        Assert.Equal(Enumerable.Range(49, 4), hsync.Select((on, c) => (on, c)).Where(x => x.on).Select(x => x.c));
        Assert.True(display[64], "the next line starts 64 characters (128 CPU cycles) later");
    }

    [Theory]
    [InlineData(0, 80, 8, 256)]
    [InlineData(3, 80, 10, 250)]
    [InlineData(4, 40, 8, 256)]
    [InlineData(6, 40, 10, 250)]
    [InlineData(7, 40, 10, 250)]
    public void MaAdvancesByR1EachRowAndTheDisplayIsOnForR1CharactersOnR6Rows(int mode, int r1, int linesPerRow, int displayedLines)
    {
        // video.md s5: 256 displayed lines in modes 0 to 2, 4 and 5 and 250 in 3, 6 and 7 (the
        // ULA blanks two of each ten in modes 3 and 6, task 8). MA restarts each line at its
        // row's start, which moves on by R1 a row; RA counts 0 to R9 (or the field's half of it).
        Crtc6845 crtc = Programmed(mode);
        RunToFrameStart(crtc);
        int start = mode == 7 ? 0x2800 : Tables[SetOf(mode)][12] << 8;
        int lineLength = mode < 4 ? 128 : 64;

        int lit = 0;
        for (int line = 0; line < 312; line++)
        {
            int rowStart = crtc.MemoryAddress;
            int raster = crtc.RasterAddress;
            int on = 0;
            for (int c = 0; c < lineLength; c++)
            {
                on += crtc.DisplayEnable ? 1 : 0;
                crtc.Tick();
            }

            if (line < displayedLines)
            {
                int row = line / linesPerRow;
                Assert.Equal((start + (row * r1)) & 0x3FFF, rowStart);
                int expected = mode == 7 ? (2 * (line % 10)) | (raster & 1) : line % linesPerRow;
                Assert.Equal(expected, raster);
                Assert.Equal(r1, on);
                lit++;
            }
            else
            {
                Assert.Equal(0, on);
            }
        }

        Assert.Equal(displayedLines, lit);
    }

    [Theory]
    [InlineData(0, 8, 256)]
    [InlineData(7, 10, 250)]
    public void TheLineStartAndTheVerticalDisplayAreWhatAScanlineNeeds(int mode, int linesPerRow, int displayedLines)
    {
        // What a consumer drawing a line at a time reads at the line's start: MA there, which
        // holds for the whole line while MemoryAddress counts on, and whether the line is inside
        // rows 0 to R6 - 1 (video.md s5: 256 lines in mode 0, 250 in mode 7).
        Crtc6845 crtc = Programmed(mode);
        RunToFrameStart(crtc);
        int start = mode == 7 ? 0x2800 : 0x0600;
        int r1 = mode == 7 ? 40 : 80;
        int lineLength = mode == 7 ? 64 : 128;

        int inside = 0;
        for (int line = 0; line < 312; line++)
        {
            int lineStart = crtc.LineStartAddress;
            bool vertical = crtc.VerticalDisplay;
            Assert.Equal(lineStart, crtc.MemoryAddress);
            if (line < displayedLines)
            {
                Assert.Equal((start + (line / linesPerRow * r1)) & 0x3FFF, lineStart);
                Assert.True(vertical, $"line {line} is displayed");
                inside++;
            }
            else
            {
                Assert.False(vertical, $"line {line} is not displayed");
            }

            Run(crtc, lineLength / 2);
            Assert.Equal(lineStart, crtc.LineStartAddress);
            Assert.Equal((lineStart + (lineLength / 2)) & 0x3FFF, crtc.MemoryAddress);
            Run(crtc, lineLength / 2);
        }

        Assert.Equal(displayedLines, inside);
    }

    [Fact]
    public void TheVerticalDisplayIsOffInTheFirstFieldAfterReset()
    {
        Crtc6845 crtc = Programmed(0);
        Assert.False(crtc.VerticalDisplay);
        Run(crtc, 100 * 128);
        Assert.False(crtc.VerticalDisplay);
        RunToFrameStart(crtc);
        Assert.True(crtc.VerticalDisplay);
    }

    [Fact]
    public void TheCursorIsOnLinesR10ToR11OfItsRowAtItsAddress()
    {
        // Mode 0 with a steady cursor (R10 bits 6 and 5 = 00) at the fourth character of the
        // third row: lines 7 to 8 of the row, but R9 = 7, so line 7 only.
        Crtc6845 crtc = Programmed(0);
        Write(crtc, 10, 0x07);
        int address = 0x0600 + (2 * 80) + 3;
        Write(crtc, 14, (byte)(address >> 8));
        Write(crtc, 15, (byte)address);
        RunToFrameStart(crtc);

        var seen = new List<(int Line, int Ma)>();
        for (int line = 0; line < 312; line++)
        {
            for (int c = 0; c < 128; c++)
            {
                if (crtc.Cursor)
                {
                    seen.Add((line, crtc.MemoryAddress));
                }
                crtc.Tick();
            }
        }

        Assert.Equal([(23, address)], seen);
    }

    [Fact]
    public void TheOsCursorBlinksSixteenFieldsOnAndSixteenOff()
    {
        // R10 = &67: start line 7, blink with a 32-field period (video.md s1.3).
        Crtc6845 crtc = Programmed(0);
        Write(crtc, 14, 0x06);
        Write(crtc, 15, 0x00);
        RunToFrameStart(crtc);

        var shown = new List<bool>();
        for (int field = 0; field < 64; field++)
        {
            bool any = false;
            void OnFrame() => shown.Add(any);
            crtc.FrameStarted += OnFrame;
            int before = shown.Count;
            for (int t = 0; t < 50_000 && shown.Count == before; t++)
            {
                any |= crtc.Cursor;
                crtc.Tick();
            }
            Assert.True(shown.Count > before, "no frame started");
            crtc.FrameStarted -= OnFrame;
        }

        Assert.Equal(32, shown.Count(s => s));
        int[] changes = Enumerable.Range(1, shown.Count - 1).Where(i => shown[i] != shown[i - 1]).ToArray();
        Assert.True(changes.Length >= 3, $"only {changes.Length} changes");
        Assert.All(changes.Zip(changes.Skip(1)), p => Assert.Equal(16, p.Second - p.First));
    }

    [Fact]
    public void Mode7SkewsTheDisplayOneCharacterAndTheCursorTwo()
    {
        // R8 = &93: DISPTMG skew 1, CUDISP skew 2 (video.md s1.3, s4.1).
        Crtc6845 crtc = Programmed(7);
        Write(crtc, 10, 0x12); // steady, line 18
        Write(crtc, 14, 0x28);
        Write(crtc, 15, 0x05);
        RunToFrameStart(crtc);
        RunToFrameStart(crtc);

        var displayAt = new List<int>();
        var cursorAt = new List<int>();
        for (int t = 0; t < 312 * 64; t++)
        {
            int ma = crtc.MemoryAddress;
            if (crtc.DisplayEnable && displayAt.Count == 0)
            {
                displayAt.Add(ma);
            }
            if (crtc.Cursor)
            {
                cursorAt.Add(ma);
            }
            crtc.Tick();
        }

        Assert.Equal([0x2801], displayAt);       // on one character after MA = the screen start
        Assert.NotEmpty(cursorAt);
        Assert.All(cursorAt, ma => Assert.Equal(0x2807, ma)); // two characters after MA = R14/R15
    }

    [Fact]
    public void OnlyR12ToR17ReadBack()
    {
        // The HD6845S datasheet: R12 and R13 can be read on the S (not the R), with R12's top two
        // bits 0; R14 and R15 read; R16 and R17 are the light pen, never strobed here. R0 to R11
        // are write only and read 0, which nobody measured (video.md s6, item 8).
        var crtc = new Crtc6845();
        for (int r = 0; r < 16; r++)
        {
            Write(crtc, r, 0xFF);
        }

        var read = new byte[20];
        for (int r = 0; r < 20; r++)
        {
            crtc.WriteAddress((byte)r);
            read[r] = crtc.ReadData();
        }

        Assert.Equal(new byte[12], read[..12]);
        Assert.Equal(new byte[] { 0x3F, 0xFF, 0x3F, 0xFF, 0x00, 0x00, 0x00, 0x00 }, read[12..]);
    }

    [Fact]
    public void TheFirstFieldAfterResetShowsNothingAndStartsMaAtZero()
    {
        // S1, "display sequence after /RES": DISPTMG and CUDISP stay low, R12 and R13 are not used.
        Crtc6845 crtc = Programmed(0);
        Write(crtc, 10, 0x00);
        Write(crtc, 14, 0x00);
        Write(crtc, 15, 0x00);
        Assert.Equal(0, crtc.MemoryAddress);

        bool started = false;
        crtc.FrameStarted += () => started = true;
        for (int t = 0; t < 50_000; t++)
        {
            crtc.Tick();
            _ = crtc.VSync;
            if (started)
            {
                break;
            }
            Assert.False(crtc.DisplayEnable);
            Assert.False(crtc.Cursor);
        }

        Assert.True(started, "no frame started");
        Assert.Equal(0x0600, crtc.MemoryAddress);
    }

    [Theory]
    [InlineData(0, false)] // written while C0 = 0: the chip judges the last line again at C0 = 1
    [InlineData(2, true)]  // written at C0 = 2: the decision is taken, the frame ends anyway
    public void TheEndOfTheFrameIsDecidedAtTheStartOfTheLastLine(int c0, bool frameEnds)
    {
        // video.md s1.6, first quirk (ACCC 10.3.1.2). Interlace off, so every field is 312 lines.
        Crtc6845 crtc = Programmed(0, interlaceOff: true);
        RunToFrameStart(crtc);
        Run(crtc, (311 * 128) + c0); // to C0 = c0 of the last line, row 38's line 7
        Write(crtc, 4, 50);

        bool started = false;
        crtc.FrameStarted += () => started = true;
        Run(crtc, 128 - c0);
        _ = crtc.VSync;
        Assert.Equal(frameEnds, started);
    }

    [Fact]
    public void WithR0OfOneLinesAreTwoCharactersWithNoHsyncAndEachFrameGetsAnAdjustLine()
    {
        // video.md s1.6, second quirk (ACCC 13.2.5): C0 never reaches 2, so the adjust armed at
        // C0 = 0 is never disarmed. With R4 = R9 = R5 = 0 each line of two characters is a frame,
        // followed by one adjust line, so a frame starts every four characters, not every two.
        var crtc = new Crtc6845();
        Write(crtc, 0, 1);
        Write(crtc, 2, 95);
        Write(crtc, 3, 0x28);
        Write(crtc, 4, 0);
        Write(crtc, 9, 0);
        Write(crtc, 5, 0);
        Write(crtc, 6, 1);
        crtc.Reset();

        var starts = new List<int>();
        int t = 0;
        crtc.FrameStarted += () => starts.Add(t);
        for (t = 1; t <= 64; t++)
        {
            crtc.Tick();
            Assert.False(crtc.HSync);
        }

        Assert.True(starts.Count > 10);
        Assert.All(starts.Zip(starts.Skip(1)), p => Assert.Equal(4, p.Second - p.First));
    }

    [Fact]
    public void ACrtcInAMachineRefusesTick()
    {
        var bus = new BbcBus(BbcSession.Roms);
        Assert.Throws<InvalidOperationException>(bus.Crtc.Tick);
    }

    [Theory]
    [InlineData(0, 0x9C, false, 40_000)]
    [InlineData(4, 0x88, false, 40_000)]
    [InlineData(7, 0x4B, false, 40_000)]
    [InlineData(0, 0x9C, true, 39_936)]
    [InlineData(4, 0x88, true, 39_936)]
    public void InTheMachineEachVsyncFallSetsIfr1InItsOwnCycle(int mode, byte ulaControl, bool interlaceOff, int gap)
    {
        // The video ULA's control byte for the mode (video.md s2.2) picks the CRTC's clock; the
        // OS's PCR of $04 makes CA1 take VSYNC's fall (via.md s1.7). The flag is looked for after
        // every one-cycle RAM read, so the cycle it is first seen in is the cycle of the edge.
        var bus = new BbcBus(BbcSession.Roms);
        bus.PowerOnReset();
        bus.Write(0xFE20, ulaControl);
        byte[] table = Tables[SetOf(mode)];
        for (int r = 11; r >= 0; r--)
        {
            byte value = r == 7 ? (byte)(table[r] + 1) : r == 8 && interlaceOff && (table[r] & 0x80) == 0 ? (byte)(table[r] ^ 1) : table[r];
            bus.Write(0xFE00, (byte)r);
            bus.Write(0xFE01, value);
        }
        bus.Write(0xFE4C, 0x04);
        bus.Write(0xFE4D, 0x7F);

        // And the flag comes with VSYNC's fall, not its rise: high before the read that first sees
        // the flag, low after it. The gaps alone would not show an edge taken the wrong way up.
        var falls = new List<long>();
        for (int n = 0; n < 13 * 40_000 && falls.Count < 12; n++)
        {
            bool before = bus.Crtc.VSync;
            bus.Read(0x0000);
            if ((bus.SystemVia.Peek(0xD) & 0x02) != 0)
            {
                Assert.True(before && !bus.Crtc.VSync, $"IFR1 rose at cycle {bus.Cycles} with VSYNC {before} before and {bus.Crtc.VSync} after");
                falls.Add(bus.Cycles);
                bus.Write(0xFE4D, 0x02);
            }
        }

        Assert.Equal(12, falls.Count);
        for (int i = 2; i < falls.Count; i++)
        {
            Assert.Equal(gap, falls[i] - falls[i - 1]);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BreakAndPowerOnResetTheCrtcsCountersButNotItsRegisters(bool powerOn)
    {
        // The CRTC's /RES is taken to be on RST, which power on and BREAK both make (S9 s3.14):
        // the counters go to 0 and the registers stay (S1), so MA and RA restart from 0.
        var s = new BbcSession(mode: 7).Boot();
        BbcBus bus = s.Machine.Bus;
        Assert.NotEqual(0, bus.Crtc.MemoryAddress & 0x03FF);

        if (powerOn)
        {
            bus.PowerOnReset();
        }
        else
        {
            bus.BreakReset();
        }

        Assert.Equal(0, bus.Crtc.MemoryAddress);
        Assert.Equal(0, bus.Crtc.RasterAddress);
        Assert.False(bus.Crtc.VSync);
        bus.Write(0xFE00, 12);
        Assert.Equal(0x28, bus.Peek(0xFE01));
    }
}
