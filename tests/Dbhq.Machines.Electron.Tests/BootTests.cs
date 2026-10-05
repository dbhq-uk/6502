using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The real OS 1.00 booting with BASIC, checked before the display exists. In mode 6 the OS
/// writes the banner into screen memory at <c>$6000</c> as 8-byte cells of its own font, so the
/// text can be read from RAM (<see cref="ScreenMemoryText"/>); it comes from the ROMs running on
/// the CPU, so the check is not circular.
/// </summary>
/// <remarks>
/// Every expected row is <c>ula.md</c> s10b: <c>Acorn Electron </c> at OS <c>$C303</c> with the
/// bell glyph after it on a power on, the language title <c>BASIC</c> at BASIC <c>$8009</c>
/// printed by the OS, and BASIC's prompt <c>&gt;</c>.
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
        string[] rows = s.ScreenMemoryRows();

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
        string[] rows = s.ScreenMemoryRows();

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
        string[] rows = s.ScreenMemoryRows();

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
        s.Type("PRINT 6*7\r").RunFor(200_000);
        string[] rows = s.ScreenMemoryRows();

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
