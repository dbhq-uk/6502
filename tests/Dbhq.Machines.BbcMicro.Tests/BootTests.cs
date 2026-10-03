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
/// <c>Acorn DFS</c> is not on the screen yet, and that is the ROM's doing. Before it serves any
/// call the DFS reads the 8271's status at <c>$FE80</c> and does nothing if either of its two
/// low bits is set (DFS <c>$B495-$B49A</c>). No 8271 is fitted, so <c>$FE80</c> reads as an
/// absent fast device, <c>$FE</c>, and the DFS stays silent, as a real Model B with the ROM and
/// no controller would. The row comes back when the 8271 does.
/// </para>
/// </remarks>
public class BootTests(BootedModes booted) : IClassFixture<BootedModes>
{
    [Fact]
    public void ColdBootPrintsTheBannerAndTheBasicPromptInMode7()
    {
        var s = new BbcSession(mode: 7).Boot();
        Assert.Equal("", s.ScreenRowAsMemory(0).TrimEnd());
        Assert.Equal("BBC Computer 32K", s.ScreenRowAsMemory(1).TrimEnd());
        Assert.Equal("", s.ScreenRowAsMemory(2).TrimEnd());
        Assert.Equal("BASIC", s.ScreenRowAsMemory(3).TrimEnd());
        Assert.Equal("", s.ScreenRowAsMemory(4).TrimEnd());
        Assert.Equal(">", s.ScreenRowAsMemory(5).TrimEnd()); // plus the cursor
        Assert.Equal("", s.ScreenRowAsMemory(6).TrimEnd());
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
        Assert.Equal("BBC Computer", s.ScreenRowAsMemory(1).TrimEnd());
        Assert.Equal("BASIC", s.ScreenRowAsMemory(3).TrimEnd());
        Assert.Equal(">", s.ScreenRowAsMemory(5).TrimEnd());
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
        Assert.Equal(BootScreen.Rows.Count - 1, bus.Peek(0x0319));
    }

    [Fact]
    public void TheBootScreenIsTheRomsText()
    {
        // The expected rows are read out of the ROMs, as bus.md s4b sets them out, not copied from
        // what the machine printed: the banner from OS $C304 (to its zero) and $C317 (to its BEL),
        // BASIC's title at $8009 (to its zero) and the prompt from BASIC's LDA #$3E at $8B06.
        Assert.Equal("BBC Computer 32K", BootScreen.Banner);
        Assert.Equal("BASIC", BootScreen.Language);
        Assert.Equal(">", BootScreen.Prompt);
        Assert.Equal(["", "BBC Computer 32K", "", "BASIC", "", ">"], BootScreen.Rows);
    }
}

/// <summary>The boot screen, rows from 0, with each string taken from the ROMs.</summary>
public static class BootScreen
{
    public static string Banner => Text(BbcSession.Roms.Os, 0x0304, 0x00) + Text(BbcSession.Roms.Os, 0x0317, 0x07);

    public static string Language => Text(BbcSession.Roms.Basic, 0x0009, 0x00);

    public static string Prompt => ((char)Opcode(BbcSession.Roms.Basic, 0x0B06, 0xA9)).ToString();

    /// <summary>
    /// Without the 8271 the DFS prints nothing at boot (bus.md s4b, task 5). When task 12 fits the
    /// 8271, <c>Acorn DFS</c> and a blank row come after row 2: add <c>"Acorn DFS", "",</c> after
    /// the second blank below, the one-line change that moves <c>BASIC</c> to row 5 and the prompt
    /// to row 7 in every mode.
    /// </summary>
    public static IReadOnlyList<string> Rows => ["", Banner, "", Language, "", Prompt];

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

    private readonly Lazy<string[]>[] _screens;

    public BootedModes()
    {
        _screens = Enumerable.Range(0, 8).Select(mode => new Lazy<string[]>(() => Session(mode).ScreenText())).ToArray();
    }

    public BbcSession Session(int mode) => _sessions[mode].Value;

    public string[] Screen(int mode) => _screens[mode].Value;
}
