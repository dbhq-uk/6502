using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The glyph table against the datasheet: every grid typed in <see cref="Figure11"/> from the
/// SAA5050 datasheet's Figure 11 must be the production table's rows exactly. This is the check
/// that the table taken from Bedstead is the SAA5050's English set and was not damaged on the way.
/// </summary>
public class TeletextGlyphsTests
{
    public static TheoryData<char> Glyphs()
    {
        var data = new TheoryData<char>();
        foreach (char glyph in Figure11.Grids.Keys)
        {
            data.Add(glyph);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Glyphs))]
    public void TheTableHoldsTheDatasheetsGrid(char glyph)
    {
        string[] grid = Figure11.Grids[glyph];
        ReadOnlySpan<byte> rows = TeletextGlyphs.Rows(Figure11.Code(glyph));

        Assert.Equal(9, rows.Length);
        for (int r = 0; r < 9; r++)
        {
            Assert.True(Figure11.Bits(grid[r]) == rows[r], $"'{glyph}' row {r}: the datasheet has {grid[r]}, the table {Convert.ToString(rows[r], 2).PadLeft(5, '0')}");
        }
    }

    [Fact]
    public void TheGridsCoverTheBriefsMinimum()
    {
        foreach (char glyph in "BC32KASDFORI> ")
        {
            Assert.True(Figure11.Grids.ContainsKey(glyph), $"no grid for '{glyph}'");
        }
    }

    [Fact]
    public void BitSevenIsIgnoredAndControlCodesAreSpaces()
    {
        // video.md s4.1: the input is 7 bits. Table 1: control codes are displayed as spaces.
        Assert.True(TeletextGlyphs.Rows(0xC1).SequenceEqual(TeletextGlyphs.Rows(0x41)));
        Assert.True(TeletextGlyphs.Rows(0x81).SequenceEqual(TeletextGlyphs.Rows(0x20)));
        Assert.True(TeletextGlyphs.Rows(0x1F).SequenceEqual(TeletextGlyphs.Rows(0x20)));
    }

    [Fact]
    public void TheEnglishSetDiffersFromUsAsciiWhereTheSheetSays()
    {
        // video.md s4.4: $23 is the pound sign and $5F the hash, so they must differ; and a sample
        // of codes outside the list is plain ASCII, here checked against the typed grids for letters.
        Assert.False(TeletextGlyphs.Rows(0x23).SequenceEqual(TeletextGlyphs.Rows(0x5F)));
        Assert.Equal(Figure11.Bits("#####"), TeletextGlyphs.Rows(0x60)[3]);
        Assert.Equal(0, TeletextGlyphs.Rows(0x60)[0]);
    }
}
