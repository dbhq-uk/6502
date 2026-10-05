using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The real OS 1.00 booting with BASIC. The text is read off the picture the ULA drew
/// (<see cref="ElectronSession.ScreenText"/>), and in one test also out of screen memory at
/// <c>$6000</c> (<see cref="ScreenMemoryText"/>), so that a fault in the display cannot hide a
/// fault in the boot, or the other way round. Both come from the ROMs running on the CPU, so the
/// check is not circular.
/// </summary>
/// <remarks>
/// Every expected row is <c>ula.md</c> s10b: <c>Acorn Electron </c> at OS <c>$C303</c> with the
/// bell glyph after it on a power on, the language title <c>BASIC</c> at BASIC <c>$8009</c>
/// printed by the OS, and BASIC's prompt <c>&gt;</c>. The bell glyph is no character of the font,
/// so its cell reads as the decoders' unknown mark.
/// </remarks>
public class BootTests
{
    private const string Banner = "Acorn Electron";

    /// <summary>Row 1, column 15: the cell after <c>Acorn Electron </c> (s10b).</summary>
    private const int BellRow = 1;
    private const int BellColumn = 15;

    /// <summary>The bell glyph, OS <c>$C42B</c>, as the sheet gives it (s10b).</summary>
    private static readonly byte[] BellGlyph = [0x18, 0x3C, 0x3C, 0x7E, 0x7E, 0x00, 0x7E, 0x3C];

    [Fact]
    public void ColdBootPrintsTheBannerAndTheBasicPromptInMode6()
    {
        var s = new ElectronSession().Boot();
        string[] rows = s.ScreenText();

        Assert.Equal(6, s.OsMode);
        Assert.Equal(6, s.Machine.Bus.Display.Mode);
        Assert.Equal(25, rows.Length);
        Assert.All(rows, r => Assert.Equal(40, r.Length));
        Assert.Equal("", rows[0].TrimEnd());
        Assert.Equal(Banner + " " + ScreenText.Unknown, rows[BellRow].TrimEnd());
        Assert.Equal("", rows[2].TrimEnd());
        Assert.Equal("BASIC", rows[3].TrimEnd());
        Assert.Equal("", rows[4].TrimEnd());
        AssertPrompt(rows[5]);
        for (int row = 6; row < 25; row++)
        {
            Assert.Equal("", rows[row].TrimEnd());
        }
    }

    [Fact]
    public void ColdBootPutsTheSameTextInScreenMemory()
    {
        // The same screen read out of RAM, with no display involved: the boot is checked even if
        // the picture is wrong.
        var s = new ElectronSession().Boot();
        string[] rows = s.ScreenMemoryRows();

        Assert.Equal("", rows[0].TrimEnd());
        Assert.Equal(Banner + " " + ScreenMemoryText.Unknown, rows[BellRow].TrimEnd());
        Assert.Equal("", rows[2].TrimEnd());
        Assert.Equal("BASIC", rows[3].TrimEnd());
        Assert.Equal("", rows[4].TrimEnd());
        AssertPrompt(rows[5]);
        for (int row = 6; row < ScreenMemoryText.Rows; row++)
        {
            Assert.Equal("", rows[row].TrimEnd());
        }
    }

    [Fact]
    public void ThePowerOnBannerEndsWithTheBellGlyph()
    {
        // s10b: the glyph is copied from OS $C42B into the cursor cell, $6000 + 1 x 320 + 15 x 8.
        var s = new ElectronSession().Boot();
        Assert.Equal(0x6000 + (1 * 320) + (15 * 8), ScreenMemoryText.CellAddress(BellRow, BellColumn));
        Assert.Equal(BellGlyph, ElectronSession.Roms.Os[0x042B..0x0433]);
        Assert.Equal(BellGlyph, ScreenMemoryText.Cell(s.Machine.Bus, BellRow, BellColumn));

        // And on the picture: in mode 6 a cell is 16 pixels across and 10 lines down, a font bit
        // two pixels wide (s5a), white where the glyph's bit is set and black where not (s5c, the
        // OS's mode 6 palette). Row 1, column 15 is x = 240 to 255, lines 10 to 19.
        Framebuffer screen = s.Machine.Bus.Screen;
        for (int line = 0; line < 10; line++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool lit = line < 8 && ((BellGlyph[line] >> (7 - (x / 2))) & 1) != 0;
                Assert.Equal(lit ? 0xFFFFFFFF : 0xFF000000, screen.Pixel((BellColumn * 16) + x, (BellRow * 10) + line));
            }
        }
    }

    [Fact]
    public void BreakKeepsRamAndPrintsTheBannerWithoutTheBell()
    {
        // s10b: $028D is 0 on a soft BREAK, so the OS prints the text and the newlines and no bell
        // glyph. s1a, s10a: RAM is not cleared on BREAK unless the break action asks for it.
        var s = new ElectronSession().Boot();
        s.Machine.Bus.PokeRam(0x1234, 0xA5);

        s.Machine.PressBreak();
        s.Machine.Run(2_000_000);
        string[] rows = s.ScreenText();

        Assert.Equal(0, s.Machine.Bus.Peek(0x028D));
        Assert.Equal(Banner, rows[BellRow].TrimEnd());
        Assert.Equal(new byte[8], ScreenMemoryText.Cell(s.Machine.Bus, BellRow, BellColumn));
        Assert.Equal("BASIC", rows[3].TrimEnd());
        AssertPrompt(rows[5]);
        Assert.Equal(0xA5, s.Machine.Bus.Peek(0x1234));
    }

    [Fact]
    public void WithNoBasicTheOsAsksForALanguage()
    {
        // s2c: with no language ROM in any slot, row 3 prints "Language?" (OS $DAA6) and the OS
        // stops there, the cursor after it, with no prompt. So it is the paging and the OS's ROM
        // scan that start BASIC.
        var s = new ElectronSession(new ElectronOptions { BasicSlots = [] }).Boot();
        string[] rows = s.ScreenText();

        Assert.StartsWith(Banner, rows[BellRow]);
        AssertTextThenCursor("Language?", rows[3]);
        Assert.Equal("", rows[5].TrimEnd());
    }

    [Theory]
    [InlineData(new[] { 10, 11 }, 11)]
    [InlineData(new[] { 10 }, 10)]
    [InlineData(new[] { 11 }, 11)]
    public void BasicInEitherSlotBootsToThePrompt(int[] slots, int language)
    {
        // s2c: the OS scans slots 0 to 15 and records each language ROM at $024B; the last one
        // recorded, the highest slot, is the language it enters. BASIC in 10 and 11 is one chip in
        // both, and slot 11 is chosen.
        var s = new ElectronSession(new ElectronOptions { BasicSlots = slots }).Boot();
        string[] rows = s.ScreenText();

        Assert.Equal("BASIC", rows[3].TrimEnd());
        AssertPrompt(rows[5]);
        Assert.Equal(language, s.Machine.Bus.Peek(0x024B));
    }

    [Fact]
    public void ALineTypedOnTheMatrixRunsInBasic()
    {
        // The keyboard through the OS's interrupt-driven scan (s7a), Shift included: * is Shift
        // and colon on the Electron's legends. BASIC echoes the line on the prompt's row, prints
        // the answer on the next and a new prompt below it.
        var s = new ElectronSession().Boot();
        s.Type("PRINT 6*7\r").RunUntilPrompt();
        string[] rows = s.ScreenText();

        Assert.Equal(">PRINT 6*7", rows[5].TrimEnd());
        Assert.Equal("42", rows[6].Trim());
        AssertPrompt(rows[7]);
    }

    [Fact]
    public void TheOsReadsThePowerOnFlagOnceAndNotAfterABreak()
    {
        // s6a, s10a: the OS reads $FE00 at $D8E8 (LDA $FE00) and tests bit 1. At power on the bit
        // is 1, and that read clears it. BREAK does not set it, so after a BREAK the same read
        // gives 0, which is how the OS tells the two apart.
        const ushort AfterTheRead = 0xD8EB;
        var s = new ElectronSession();
        ElectronMachine m = s.Machine;

        m.PowerOn();
        Assert.Equal(0x02, m.Bus.Ula.Status & 0x02);
        StepTo(m, AfterTheRead);
        Assert.Equal(0x02, m.Cpu.A & 0x02);
        Assert.Equal(0x00, m.Bus.Ula.Status & 0x02);

        m.Run(2_000_000);
        Assert.Equal(1, m.Bus.Peek(0x028D)); // s10b: 1 on a power on

        m.PressBreak();
        Assert.Equal(0x00, m.Bus.Ula.Status & 0x02);
        StepTo(m, AfterTheRead);
        Assert.Equal(0x00, m.Cpu.A & 0x02);

        m.Run(2_000_000);
        Assert.Equal(0, m.Bus.Peek(0x028D)); // s10b: 0 on a soft BREAK
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EachModeTypedInBasicShowsThePromptAtTheTopLeft(int mode)
    {
        // Boot in mode 6, then MODE 0, MODE 1, ... in turn up to this mode, each typed in BASIC,
        // which clears the screen and prints the prompt at the top left. The geometry is the OS's
        // own: the screen start from its tables at $C3FB (the mode's map) and $C40B (the start's
        // high byte for each map), and the text grid from $C3B4 and $C3AD (ula.md s5a). Typing in
        // modes 0 to 3 is slower, because the display holds the CPU off RAM (s4), hence the cap.
        byte[] os = ElectronSession.Roms.Os;
        var s = new ElectronSession().Boot();
        for (int n = 0; n <= mode; n++)
        {
            s.Type($"MODE {n}\r").RunUntilPrompt(maxCycles: 8_000_000);
        }

        int start = os[0x040B + os[0x03FB + mode]] << 8;
        (int columns, int rows) = ScreenText.Grid(os, mode);
        Assert.Equal(mode, s.OsMode);
        Assert.Equal(mode, s.Machine.Bus.Display.Mode);
        Assert.Equal(start, s.Machine.Bus.Display.StartAddress);
        Assert.Equal(UlaDisplay.ScreenStart(mode), start);
        Assert.Equal((1, 0), s.OsCursor);

        string[] text = s.ScreenText();
        Assert.Equal(rows, text.Length);
        Assert.All(text, r => Assert.Equal(columns, r.Length));
        AssertPrompt(text[0]);
        Assert.All(text.Skip(1), r => Assert.Equal("", r.TrimEnd()));

        // The '>' itself, pixel by pixel in the top left cell: 640 / columns pixels across, a font
        // bit 640 / columns / 8 of them, one line a row of the picture, lit where its bit is set.
        // Lit is the text colour and unlit colour 0, which the OS's palettes make white and black
        // in every mode (s5c).
        int bitWidth = 640 / columns / 8;
        ReadOnlySpan<byte> glyph = ScreenText.Glyph(os, '>');
        Framebuffer screen = s.Machine.Bus.Screen;
        for (int line = 0; line < 8; line++)
        {
            for (int x = 0; x < 8 * bitWidth; x++)
            {
                bool lit = ((glyph[line] >> (7 - (x / bitWidth))) & 1) != 0;
                Assert.True((lit ? 0xFFFFFFFF : 0xFF000000) == screen.Pixel(x, line), $"mode {mode}, line {line}, x {x}");
            }
        }
    }

    /// <summary>Row 5: BASIC's prompt in column 0 and the cursor beside it, then nothing (s10b).</summary>
    private static void AssertPrompt(string row) => AssertTextThenCursor(">", row);

    /// <summary>
    /// <paramref name="text"/> at the start of the row, the cursor in the next cell, then nothing.
    /// The cursor is drawn in software: the OS's routine at <c>$D6DE</c> swaps line 7 of the cursor
    /// cell (<c>LDY #$07</c> at <c>$D6F6</c>) with a byte it keeps at <c>$080C</c>, and flashes it
    /// by swapping it back. On a blank cell in mode 6 that line is <c>$FF</c>, which is the font's
    /// <c>_</c>, so the cell reads as <c>_</c> or as a space depending on when it is looked at.
    /// </summary>
    private static void AssertTextThenCursor(string text, string row)
    {
        Assert.Equal(text, row[..text.Length]);
        Assert.Contains(row[text.Length], new[] { ' ', '_' });
        Assert.Equal("", row[(text.Length + 1)..].TrimEnd());
    }

    /// <summary>Steps whole instructions until the CPU's PC is <paramref name="pc"/>, within the reset code.</summary>
    private static void StepTo(ElectronMachine machine, ushort pc)
    {
        for (int i = 0; i < 100 && machine.Cpu.PC != pc; i++)
        {
            machine.Step();
        }

        Assert.Equal(pc, machine.Cpu.PC);
    }
}
