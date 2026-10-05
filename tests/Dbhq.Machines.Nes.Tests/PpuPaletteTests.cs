using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The computed colours, against what <c>docs/nes/facts/ppu.md</c> sections 10 and 11 say is
/// known of them: the greys, the blacks, white, the hue of each colour of the second row, how
/// greyscale and emphasis act, and the 2C07's swapped emphasis bits.
/// </summary>
public class PpuPaletteTests
{
    private static (int R, int G, int B, int A) Channels(uint colour) =>
        ((int)(colour & 0xFF), (int)((colour >> 8) & 0xFF), (int)((colour >> 16) & 0xFF), (int)(colour >> 24));

    private static uint Plain(int index) => PpuPalette.Colour(index, 0, false, false);

    [Fact]
    public void Entry0FIsBlack()
    {
        Assert.Equal((0, 0, 0, 0xFF), Channels(Plain(0x0F)));
    }

    [Fact]
    public void Entry30IsWhiteAndSoIs20()
    {
        Assert.Equal((0xFF, 0xFF, 0xFF, 0xFF), Channels(Plain(0x30)));
        Assert.Equal((0xFF, 0xFF, 0xFF, 0xFF), Channels(Plain(0x20)));
    }

    [Fact]
    public void HuesEAndFAreBlackOnEveryRow()
    {
        for (int row = 0; row < 4; row++)
        {
            Assert.Equal((0, 0, 0, 0xFF), Channels(Plain((row << 4) | 0x0E)));
            Assert.Equal((0, 0, 0, 0xFF), Channels(Plain((row << 4) | 0x0F)));
        }
    }

    [Fact]
    public void Hues0AndDAreGreysThatGetBrighterRowByRow()
    {
        // ppu.md 10: hue 0 is grey and $D dark grey; $0D is blacker than black, so it clips to 0.
        int[] order = [0x0D, 0x1D, 0x2D, 0x00, 0x10, 0x3D, 0x20];
        int last = -1;
        foreach (int index in order)
        {
            (int r, int g, int b, _) = Channels(Plain(index));
            Assert.True(r == g && g == b, $"${index:X2} is not grey: {r} {g} {b}");
            Assert.True(r >= last, $"${index:X2} ({r}) is darker than the grey before it ({last})");
            last = r;
        }

        Assert.Equal(0, Channels(Plain(0x0D)).R);
    }

    // The hue, in degrees, of the 2C02G table's row $1x in ppu.md section 11 (the wiki's
    // 2C02G_U_wiki), the sheet's known entries, against which the computed colours are checked.
    public static TheoryData<int, string> SecondRow() => new()
    {
        { 0x11, "0041D9" }, { 0x12, "2F1EFF" }, { 0x13, "6704F2" }, { 0x14, "9400B4" },
        { 0x15, "AA0057" }, { 0x16, "A31800" }, { 0x17, "803900" }, { 0x18, "4B5B00" },
        { 0x19, "137600" }, { 0x1A, "008100" }, { 0x1B, "007923" }, { 0x1C, "006288" },
    };

    [Theory]
    [MemberData(nameof(SecondRow))]
    public void EachColourOfTheSecondRowHasTheHueOfTheSheetsTable(int index, string sheet)
    {
        (int r, int g, int b, _) = Channels(Plain(index));
        int sr = Convert.ToInt32(sheet[..2], 16);
        int sg = Convert.ToInt32(sheet[2..4], 16);
        int sb = Convert.ToInt32(sheet[4..], 16);

        // The sheet's table is Pally's, which models the 2C02G's phase distortion and a 7.5 IRE
        // setup; the formula here does neither, so the hues agree to within 20 degrees, not exactly.
        double difference = Math.Abs(((Hue(r, g, b) - Hue(sr, sg, sb) + 540) % 360) - 180);
        Assert.True(difference <= 20, $"${index:X2} computed {r:X2}{g:X2}{b:X2}, sheet {sheet}: {difference:F1} degrees apart");
    }

    private static double Hue(int r, int g, int b)
    {
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        double c = max - min;
        double h = max == r ? (g - b) / c : max == g ? 2 + (b - r) / c : 4 + (r - g) / c;
        return ((h * 60) + 360) % 360;
    }

    [Fact]
    public void GreyscaleAndsTheIndexWith30()
    {
        for (int index = 0; index < 64; index++)
        {
            Assert.Equal(Plain(index & 0x30), PpuPalette.Colour(index, 0, false, true));
        }
    }

    [Theory]
    [InlineData(0x30)]
    [InlineData(0x20)]
    [InlineData(0x10)]
    [InlineData(0x00)]
    public void EachEmphasisBitDarkensTheOtherTwoChannels(int index)
    {
        (int r, int g, int b, _) = Channels(Plain(index));

        // Bit 0 (PPUMASK bit 5) red, bit 1 green, bit 2 blue, on the 2C02 (ppu.md 10).
        (int er, int eg, int eb, _) = Channels(PpuPalette.Colour(index, 1, false, false));
        Assert.True(eg < g && eb < b && er >= eg && er >= eb, $"red emphasis of ${index:X2}: {er} {eg} {eb} from {r} {g} {b}");

        (er, eg, eb, _) = Channels(PpuPalette.Colour(index, 2, false, false));
        Assert.True(er < r && eb < b && eg >= er && eg >= eb, $"green emphasis of ${index:X2}: {er} {eg} {eb} from {r} {g} {b}");

        (er, eg, eb, _) = Channels(PpuPalette.Colour(index, 4, false, false));
        Assert.True(er < r && eg < g && eb >= er && eb >= eg, $"blue emphasis of ${index:X2}: {er} {eg} {eb} from {r} {g} {b}");

        // All three dim every channel.
        (er, eg, eb, _) = Channels(PpuPalette.Colour(index, 7, false, false));
        Assert.True(er < r && eg < g && eb < b, $"all emphasis of ${index:X2}: {er} {eg} {eb} from {r} {g} {b}");
    }

    [Fact]
    public void EmphasisLeavesTheBlacksOfHuesEAndFAlone()
    {
        for (int emphasis = 0; emphasis < 8; emphasis++)
        {
            Assert.Equal(Plain(0x0F), PpuPalette.Colour(0x0F, emphasis, false, false));
            Assert.Equal(Plain(0x2E), PpuPalette.Colour(0x2E, emphasis, false, false));
        }
    }

    [Fact]
    public void OnPalTheRedAndGreenEmphasisBitsAreSwapped()
    {
        for (int index = 0; index < 64; index++)
        {
            Assert.Equal(PpuPalette.Colour(index, 2, false, false), PpuPalette.Colour(index, 1, true, false));
            Assert.Equal(PpuPalette.Colour(index, 1, false, false), PpuPalette.Colour(index, 2, true, false));
            Assert.Equal(PpuPalette.Colour(index, 4, false, false), PpuPalette.Colour(index, 4, true, false));
            Assert.Equal(PpuPalette.Colour(index, 0, false, false), PpuPalette.Colour(index, 0, true, false));
            Assert.Equal(PpuPalette.Colour(index, 5, false, false), PpuPalette.Colour(index, 6, true, false));
        }
    }

    [Fact]
    public void EveryColoursAlphaIsFF()
    {
        foreach (bool swap in new[] { false, true })
        {
            for (int emphasis = 0; emphasis < 8; emphasis++)
            {
                for (int index = 0; index < 64; index++)
                {
                    Assert.Equal(0xFFu, PpuPalette.Colour(index, emphasis, swap, false) >> 24);
                    Assert.Equal(0xFFu, PpuPalette.Colour(index, emphasis, swap, true) >> 24);
                }
            }
        }
    }
}
