using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The computed colours, against what <c>docs/nes/facts/ppu.md</c> sections 10 and 11 and the
/// wiki's NTSC video page say of the signal in prose: the greys, the blacks, white, hue 8 on the
/// colour burst, 30 degrees a hue, one luma a row, how greyscale and emphasis act, and the
/// 2C07's swapped emphasis bits. No colour is compared with a copied table.
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

    // The angle of a colour's chroma in the U, V plane, in degrees: atan2(V, U).
    private static double ChromaAngle(int colour)
    {
        (_, double u, double v) = PpuPalette.Yuv(colour, 0);
        return Math.Atan2(v, u) * 180 / Math.PI;
    }

    private static double AngleBetween(double a, double b) => Math.Abs(((a - b + 540) % 360) - 180);

    [Theory]
    [InlineData(0x08)]
    [InlineData(0x18)]
    [InlineData(0x28)]
    [InlineData(0x38)]
    public void Hue8DecodesAsPureMinusUTheColourBurstsPhase(int colour)
    {
        // NTSC video: "NTSC colorburst (pure shade -U) is the same phase as phase 8".
        (_, double u, double v) = PpuPalette.Yuv(colour, 0);
        Assert.True(u < 0, $"${colour:X2}: U is {u}, not negative");
        Assert.True(AngleBetween(ChromaAngle(colour), 180) < 0.5, $"${colour:X2} is at {ChromaAngle(colour):F2} degrees, not 180");
    }

    public static TheoryData<int> HueSteps()
    {
        var rows = new TheoryData<int>();
        for (int row = 0; row < 4; row++)
        {
            for (int hue = 1; hue < 0xC; hue++)
            {
                rows.Add((row << 4) | hue);
            }
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(HueSteps))]
    public void EachHueStepTurnsTheChromaByAPhaseOf30Degrees(int colour)
    {
        // NTSC video: 12 colour square waves at regular phases, hue H using wave H. One hue on
        // is one phase of twelve on, 30 degrees, the same way round every time.
        double turn = ((ChromaAngle(colour + 1) - ChromaAngle(colour)) + 360) % 360;
        Assert.True(Math.Abs(turn - 30) < 0.5, $"${colour:X2} to ${colour + 1:X2} turns {turn:F2} degrees");
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x10)]
    [InlineData(0x20)]
    [InlineData(0x30)]
    public void TheHuesOfARowShareOneLumaAndOneSaturation(int row)
    {
        // NTSC video: the colours of a row have "exactly the same luminosity; only the chroma
        // phase differs".
        (double y1, double u1, double v1) = PpuPalette.Yuv(row | 1, 0);
        for (int hue = 2; hue <= 0xC; hue++)
        {
            (double y, double u, double v) = PpuPalette.Yuv(row | hue, 0);
            Assert.Equal(y1, y, 9);
            Assert.Equal(Math.Sqrt((u1 * u1) + (v1 * v1)), Math.Sqrt((u * u) + (v * v)), 9);
        }
    }

    [Fact]
    public void EachRowsLumaIsAboveTheRowBelowIt()
    {
        for (int row = 0; row < 3; row++)
        {
            Assert.True(PpuPalette.Yuv((row << 4) | 1, 0).Y < PpuPalette.Yuv(((row + 1) << 4) | 1, 0).Y, $"row {row + 1} is not brighter than row {row}");
        }
    }

    [Fact]
    public void GreysHaveNoChroma()
    {
        foreach (int colour in new[] { 0x00, 0x10, 0x20, 0x30, 0x0D, 0x1D, 0x2D, 0x3D, 0x0F })
        {
            (_, double u, double v) = PpuPalette.Yuv(colour, 0);
            Assert.True(Math.Abs(u) < 1e-9 && Math.Abs(v) < 1e-9, $"${colour:X2} has chroma {u}, {v}");
        }
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
