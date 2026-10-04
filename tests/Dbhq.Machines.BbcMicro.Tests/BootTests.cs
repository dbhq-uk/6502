using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The real MOS 1.20 booting with BASIC and the DFS, checked before any video exists. In mode 7
/// the OS writes the banner into screen memory as character codes at <c>$7C00</c>, so the text
/// can be read from RAM; it comes from the ROMs running on the CPU, so the check is not circular.
/// </summary>
/// <remarks>
/// <para>
/// Every expected string is in the ROMs (bus.md s4b): <c>BBC Computer </c> and <c>32K</c> at OS
/// <c>$C304</c>, the language title <c>BASIC</c> at BASIC <c>$8009</c>, printed by the OS at
/// <c>$DBF2</c>, and the prompt <c>&gt;</c> from BASIC <c>$8B06</c>.
/// </para>
/// <para>
/// <c>Acorn DFS</c> is on the screen because the 8271 is fitted (task 12). While bit 7 of the
/// start-up options at <c>$028F</c> is set, as these links leave it (DFS <c>$80F7-$80FD</c>),
/// before it serves any call the DFS reads the 8271's status at <c>$FE80</c> and does nothing if either of its two
/// low bits is set (DFS <c>$B495-$B49A</c>). Until task 12 no 8271 was fitted, <c>$FE80</c> read
/// as an absent fast device, <c>$FE</c>, and the DFS stayed silent, as a real Model B with the
/// ROM and no controller would; an idle 8271 reads <c>$00</c> there. The drive is empty in these
/// tests, and the DFS boots without touching it.
/// </para>
/// </remarks>
public class BootTests(BootedModes booted) : IClassFixture<BootedModes>
{
    private const uint Black = 0xFF000000, White = 0xFFFFFFFF;

    [Fact]
    public void ColdBootPrintsTheBannerAndTheBasicPromptInMode7()
    {
        // The rows of BootScreen, the DFS's line among them, then a blank row below the prompt
        // (and its cursor).
        var s = new BbcSession(mode: 7).Boot();
        for (int row = 0; row < BootScreen.Rows.Count; row++)
        {
            Assert.Equal(BootScreen.Rows[row], s.ScreenRowAsMemory(row).TrimEnd());
        }
        Assert.Equal("", s.ScreenRowAsMemory(BootScreen.Rows.Count).TrimEnd());
    }

    [Fact]
    public void BreakPrintsTheBannerWithoutTheMemorySize()
    {
        // A soft BREAK leaves the system VIA alone, so its IER tells the OS this is not a power
        // on; $028D is then 0 and the OS skips the "32K" (OS $DB71-$DB74), then re-enters the
        // language it was running, printing its title (OS $DBBE-$DBF2).
        var s = new BbcSession(mode: 7).Boot();
        s.Machine.PressBreak();
        s.Machine.Run(6_000_000);

        Assert.Equal(0, s.Machine.Bus.Peek(0x028D)); // the last reset was a soft BREAK
        Assert.Equal(BootScreen.BannerAfterBreak, s.ScreenRowAsMemory(1).TrimEnd());
        Assert.Equal(BootScreen.Language, s.ScreenRowAsMemory(BootScreen.LanguageRow).TrimEnd());
        Assert.Equal(BootScreen.Prompt, s.ScreenRowAsMemory(BootScreen.PromptRow).TrimEnd());
    }

    [Fact]
    public void ColdBootLeavesTheOsRecordsOfAWorkingMachine()
    {
        // What task 5's scratch tracer saw, pinned. $028D, the last reset type, is 1, a power on:
        // the system VIA's IER read $80 at OS $D9D7. $028E, the top of RAM, is $80, so the banner
        // says 32K. $0277 keeps its default of $FF: the user VIA passed the OS's PCR check at
        // $DA94-$DA9F, which would have incremented it. Bit 7 of the start-up options at $028F is
        // set (the links inverted, via.md "Startup options links"), which is what sends every
        // service call to the DFS half of DFS,NET first (DFS $80F7-$80FD).
        var s = new BbcSession(mode: 7).Boot();
        BbcBus bus = s.Machine.Bus;

        Assert.Equal(1, bus.Peek(0x028D));
        Assert.Equal(0x80, bus.Peek(0x028E));
        Assert.Equal(0xFF, bus.Peek(0x0277));
        Assert.Equal(0x80, bus.Peek(0x028F) & 0x80);
    }

    [Fact]
    public void TheOsClockCountsAHundredTicksASecond()
    {
        // Timer 1 of the system VIA interrupts every 10 ms, and each tick adds one to the OS's
        // clock (OS $DDD1-$DDEA): two five-byte copies at $0292 and $0297, most significant byte
        // first, the current one chosen by $0283 (5 or 10), so it ends at $0291 + $0283. A second
        // of machine time is a hundred ticks.
        var s = new BbcSession(mode: 7).Boot();
        long before = Clock(s.Machine.Bus);
        s.Machine.Run(2_000_000);
        long after = Clock(s.Machine.Bus);

        Assert.Equal(100, after - before);

        static long Clock(BbcBus bus)
        {
            int last = 0x0291 + bus.Peek(0x0283);
            long time = 0;
            for (int address = last - 4; address <= last; address++)
            {
                time = (time << 8) | bus.Peek((ushort)address);
            }
            return time;
        }
    }

    [Fact]
    public void TheOsCountsFiftyVsyncsASecond()
    {
        // The vsync interrupt (CA1, IFR bit 1) decrements the OS's counter at $0240 (OSBYTE 176),
        // once a field, and a field is 40,000 cycles in mode 7 (video.md s5), so a second of
        // machine time is fifty.
        var s = new BbcSession(mode: 7).Boot();
        byte before = s.Machine.Bus.Peek(0x0240);
        s.Machine.Run(2_000_000);
        byte after = s.Machine.Bus.Peek(0x0240);

        Assert.Equal(50, (before - after) & 0xFF);
    }

    [Theory]
    [InlineData(0, 0x3000, 0x06)]
    [InlineData(1, 0x3000, 0x06)]
    [InlineData(2, 0x3000, 0x06)]
    [InlineData(3, 0x4000, 0x08)]
    [InlineData(4, 0x5800, 0x0B)]
    [InlineData(5, 0x5800, 0x0B)]
    [InlineData(6, 0x6000, 0x0C)]
    [InlineData(7, 0x7C00, 0x28)]
    public void EachStartUpModeBootsIntoThatMode(int mode, int screenBase, int r12)
    {
        var s = new BbcSession(mode).Boot();
        BbcBus bus = s.Machine.Bus;

        // The OS keeps the current mode at $0355: it is stored by STX $0355 at $CB3D, after
        // AND #$07 on the requested mode.
        Assert.Equal(mode, bus.Peek(0x0355));

        // The banner's pixels (or, in mode 7, its characters) are in that mode's screen memory
        // (bus.md s3c); the RAM clear at power on left it all zero.
        int nonZero = 0;
        for (int a = screenBase; a < 0x8000; a++)
        {
            nonZero += bus.Peek((ushort)a) != 0 ? 1 : 0;
        }
        Assert.True(nonZero > 0, $"Nothing was drawn in screen memory from ${screenBase:X4}.");

        // The CRTC holds the mode's screen start, start address / 8, or ($7C00 hi - $74) EOR $20
        // in mode 7 (bus.md s3b).
        bus.Write(0xFE00, 12);
        Assert.Equal(r12, bus.Peek(0xFE01));
        bus.Write(0xFE00, 13);
        Assert.Equal(0x00, bus.Peek(0xFE01));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void EachModeBootsToThePromptReadOffThePicture(int mode)
    {
        // The same rows in every mode: the banner is sixteen characters, so even a twenty-column
        // mode does not wrap it. Rows below the prompt are blank.
        string[] screen = booted.Screen(mode);
        Assert.Equal(ScreenText.Grids[mode].Rows, screen.Length);
        for (int row = 0; row < screen.Length; row++)
        {
            Assert.Equal(ScreenText.Grids[mode].Columns, screen[row].Length);
            string expected = row < BootScreen.Rows.Count ? BootScreen.Rows[row] : "";
            Assert.True(expected == screen[row].TrimEnd(), $"mode {mode}, row {row}: \"{screen[row].TrimEnd()}\"");
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void EachModeBootsInWhiteOnBlack(int mode)
    {
        // The shapes alone do not say the colours are right: the decode takes either colour of a
        // cell as the text, so a palette fault that drew mode 2 as cyan on red still read (task 10's
        // review). The OS's default is white text on black in every mode: in its power-up palettes,
        // which video.md s2.3 decodes, logical 0 is black and the text colour the OS picks is
        // white (logical 1 of 2, 3 of 4, 7 of 16), and mode 7's rows start white on black (s4.2,
        // the start-of-row defaults). The cursor only inverts
        // black and white into each other. So the whole picture holds black and white and nothing
        // else, and in every cell the decode reads, the text is white and the background black.
        var colours = new HashSet<uint>();
        foreach (uint pixel in booted.Session(mode).Machine.Screen.Pixels)
        {
            colours.Add(pixel);
        }
        Assert.Equal([Black, White], colours.Order());

        ScreenText.Cell[][] cells = booted.Cells(mode);
        int text = 0;
        for (int row = 0; row < cells.Length; row++)
        {
            for (int column = 0; column < cells[row].Length; column++)
            {
                ScreenText.Cell cell = cells[row][column];
                Assert.True(cell.Background == Black, $"mode {mode}, cell ({column}, {row}): background {cell.Background:X8}");
                Assert.True(cell.Text == ' ' ? cell.Foreground is null : cell.Foreground == White,
                    $"mode {mode}, cell ({column}, {row}) '{cell.Text}': text {cell.Foreground:X8}");
                text += cell.Text == ' ' ? 0 : 1;
            }
        }
        Assert.Equal(string.Concat(BootScreen.Rows).Count(c => c != ' '), text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void TheOsTextWindowIsTheModesGridAndTheCursorFollowsThePrompt(int mode)
    {
        // The decode cuts the picture by the grid of video.md s3.1; the OS's own text window says
        // the same: left $0308, bottom $0309, right $030A, top $030B, set from the ROM's tables at
        // $C9C7-$C9D6. And the text cursor the decode is told about, $0318 and $0319, stands just
        // after the prompt.
        BbcBus bus = booted.Session(mode).Machine.Bus;
        (int columns, int rows) = ScreenText.Grids[mode];
        Assert.Equal(0, bus.Peek(0x0308));
        Assert.Equal(rows - 1, bus.Peek(0x0309));
        Assert.Equal(columns - 1, bus.Peek(0x030A));
        Assert.Equal(0, bus.Peek(0x030B));
        Assert.Equal(1, bus.Peek(0x0318));
        Assert.Equal(BootScreen.PromptRow, bus.Peek(0x0319));
    }

    [Fact]
    public void TheBootScreenIsTheRomsText()
    {
        // The expected rows are read out of the ROMs, as bus.md s4b sets them out, not copied from
        // what the machine printed: the banner from OS $C304 (to its zero) and $C317 (to its BEL),
        // BASIC's title at $8009 (to its zero) and the prompt from BASIC's LDA #$3E at $8B06. The
        // DFS's line is at DFS $B3B4 (to its carriage return).
        Assert.Equal("BBC Computer 32K", BootScreen.Banner);
        Assert.Equal("BBC Computer", BootScreen.BannerAfterBreak);
        Assert.Equal("BASIC", BootScreen.Language);
        Assert.Equal(">", BootScreen.Prompt);
        Assert.Equal("Acorn DFS", BootScreen.Dfs);

        // The layout of bus.md s4b: a blank row, the banner, a blank, the DFS's line and a blank,
        // then the language, a blank and the prompt.
        Assert.Equal(["", BootScreen.Banner, "", BootScreen.Dfs, "", BootScreen.Language, "", BootScreen.Prompt], BootScreen.Rows);
        Assert.Equal(5, BootScreen.LanguageRow);
        Assert.Equal(7, BootScreen.PromptRow);
    }
}

/// <summary>The boot screen, rows from 0, with each string taken from the ROMs.</summary>
public static class BootScreen
{
    /// <summary>The banner after a power on: OS <c>$C304</c> and the memory size at <c>$C317</c>.</summary>
    public static string Banner => Text(BbcSession.Roms.Os, 0x0304, 0x00) + Text(BbcSession.Roms.Os, 0x0317, 0x07);

    /// <summary>The banner after a soft BREAK, which skips the memory size: OS <c>$C304</c>, trailing space dropped.</summary>
    public static string BannerAfterBreak => Text(BbcSession.Roms.Os, 0x0304, 0x00).TrimEnd();

    public static string Language => Text(BbcSession.Roms.Basic, 0x0009, 0x00);

    public static string Prompt => ((char)Opcode(BbcSession.Roms.Basic, 0x0B06, 0xA9)).ToString();

    /// <summary>The line the DFS prints at boot once an 8271 is fitted: DFS ROM <c>$B3B4</c>, file offset <c>$33B4</c>, to its carriage return.</summary>
    public static string Dfs => Text(BbcSession.Roms.Dfs, 0x33B4, 0x0D);

    /// <summary>
    /// The rows from 0. With the 8271 fitted the DFS prints <see cref="Dfs"/> and a blank row after
    /// row 2 (bus.md s4b; task 5 found it silent without the 8271, task 12 fitted it). Every boot
    /// test, memory and picture, and every BASIC test takes its rows from this list
    /// (<see cref="LanguageRow"/>, <see cref="PromptRow"/>), so <c>BASIC</c> is on row 5 and the
    /// prompt on row 7 in all of them. <c>Acorn DFS</c> is nine characters, so it does not wrap in
    /// the twenty-column modes.
    /// </summary>
    public static IReadOnlyList<string> Rows => ["", Banner, "", Dfs, "", Language, "", Prompt];

    /// <summary>The language title's row.</summary>
    public static int LanguageRow => Rows.Count - 3;

    /// <summary>The prompt's row, where the cursor waits after the boot.</summary>
    public static int PromptRow => Rows.Count - 1;

    private static string Text(byte[] rom, int offset, byte end)
    {
        int stop = Array.IndexOf(rom, end, offset);
        return System.Text.Encoding.ASCII.GetString(rom, offset, stop - offset);
    }

    // The operand of an immediate instruction, checking the opcode is the one expected.
    private static byte Opcode(byte[] rom, int offset, byte opcode)
    {
        Assert.Equal(opcode, rom[offset]);
        return rom[offset + 1];
    }
}

/// <summary>
/// One booted machine for each start-up mode, made the first time a test asks for it and shared by
/// the read-only boot tests, so a mode is booted once rather than once a test. Nothing that types
/// or changes the machine uses these.
/// </summary>
public sealed class BootedModes
{
    private readonly Lazy<BbcSession>[] _sessions =
        Enumerable.Range(0, 8).Select(mode => new Lazy<BbcSession>(() => new BbcSession(mode).Boot())).ToArray();

    private readonly Lazy<ScreenText.Cell[][]>[] _cells;

    public BootedModes()
    {
        _cells = Enumerable.Range(0, 8).Select(mode => new Lazy<ScreenText.Cell[][]>(() => Session(mode).ScreenCells())).ToArray();
    }

    public BbcSession Session(int mode) => _sessions[mode].Value;

    public ScreenText.Cell[][] Cells(int mode) => _cells[mode].Value;

    public string[] Screen(int mode) => Cells(mode).Select(row => new string(row.Select(cell => cell.Text).ToArray())).ToArray();
}
