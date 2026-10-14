using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The palette, computed from each region's colour signal as <c>docs/atari-2600/facts/tia.md</c>
/// sections 19.4 and 19.5 give it, never copied from a table. The worked examples are the sheet's
/// section 19.6, which <c>tools/probes/atari-2600/tia_palette.py</c> prints from the same formulas,
/// so they catch a slip in coding the formula, not an error in the formula itself; the properties
/// are the ones the sheet says tests can take from it. Colours are
/// <c>0xAABBGGRR</c>.
/// </summary>
public class TiaPaletteTests
{
    public static TheoryData<string> Regions => TestPrograms.Regions;

    // #RRGGBB as the palette gives it.
    private static uint Rgba(int rgb) => 0xFF000000u | (uint)((rgb & 0xFF) << 16) | (uint)(rgb & 0xFF00) | (uint)((rgb >> 16) & 0xFF);

    private static (int R, int G, int B) Parts(uint rgba) => ((int)(rgba & 0xFF), (int)((rgba >> 8) & 0xFF), (int)((rgba >> 16) & 0xFF));

    [Theory]
    [InlineData("NTSC", 0x00, 0x000000)]
    [InlineData("NTSC", 0x0E, 0xFFFFFF)]
    [InlineData("NTSC", 0x1E, 0xFFFF4D)]
    [InlineData("NTSC", 0x44, 0xAA1F21)]
    [InlineData("NTSC", 0xC8, 0x30CB6A)]
    [InlineData("NTSC", 0xF8, 0x92B400)]
    [InlineData("PAL", 0x1E, 0xFFFFFF)]
    [InlineData("PAL", 0x2E, 0xFFFF53)]
    [InlineData("PAL", 0x58, 0x4BCE14)]
    [InlineData("PAL", 0x64, 0xA9211B)]
    [InlineData("PAL", 0x7E, 0x9FFFD1)]
    [InlineData("PAL", 0xC8, 0xAC63FF)]
    [InlineData("PAL", 0xE8, 0x929292)]
    public void Section_19_6_worked_examples(string region, int number, int rgb)
    {
        TiaPalette palette = TestPrograms.RegionNamed(region).Palette;
        Assert.Equal(Rgba(rgb), palette.Rgba((byte)number));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Hue_0_is_grey_at_every_luminance(string region)
    {
        TiaPalette palette = TestPrograms.RegionNamed(region).Palette;
        for (int lum = 0; lum < 8; lum++)
        {
            (int r, int g, int b) = Parts(palette.Rgba((byte)(lum << 1)));
            Assert.Equal(r, g);
            Assert.Equal(g, b);
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Luminance_rises_with_the_number_within_every_hue(string region)
    {
        // No component falls, and their sum rises (tia.md section 19.6).
        TiaPalette palette = TestPrograms.RegionNamed(region).Palette;
        for (int hue = 0; hue < 16; hue++)
        {
            for (int lum = 1; lum < 8; lum++)
            {
                (int r0, int g0, int b0) = Parts(palette.Rgba((byte)((hue << 4) | ((lum - 1) << 1))));
                (int r1, int g1, int b1) = Parts(palette.Rgba((byte)((hue << 4) | (lum << 1))));
                Assert.True(r1 >= r0 && g1 >= g0 && b1 >= b0, $"hue {hue}, luminance {lum}");
                Assert.True(r1 + g1 + b1 > r0 + g0 + b0, $"hue {hue}, luminance {lum}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Bit_0_makes_no_difference_and_every_colour_is_opaque(string region)
    {
        TiaPalette palette = TestPrograms.RegionNamed(region).Palette;
        for (int n = 0; n < 256; n += 2)
        {
            Assert.Equal(palette.Rgba((byte)n), palette.Rgba((byte)(n + 1)));
            Assert.Equal(0xFF000000u, palette.Rgba((byte)n) & 0xFF000000u);
        }
    }

    [Fact]
    public void Section_19_5_pal_shows_hues_0_1_14_and_15_as_grey()
    {
        TiaPalette palette = Region.Pal.Palette;
        foreach (int hue in new[] { 0, 1, 14, 15 })
        {
            for (int lum = 0; lum < 8; lum++)
            {
                (int r, int g, int b) = Parts(palette.Rgba((byte)((hue << 4) | (lum << 1))));
                Assert.True(r == g && g == b, $"hue {hue}, luminance {lum}");
            }
        }

        // And each of hues 2 to 13 is a colour at a middle luminance.
        for (int hue = 2; hue <= 13; hue++)
        {
            (int r, int g, int b) = Parts(palette.Rgba((byte)((hue << 4) | 0x08)));
            Assert.False(r == g && g == b, $"hue {hue}");
        }
    }

    [Fact]
    public void Section_19_3_ntsc_hue_15_lands_on_hue_1_and_every_other_hue_is_a_colour()
    {
        // The model's step is 360/14 degrees (tia.md section 19.3, a choice).
        TiaPalette palette = Region.Ntsc.Palette;
        for (int lum = 0; lum < 8; lum++)
        {
            Assert.Equal(palette.Rgba((byte)(0x10 | (lum << 1))), palette.Rgba((byte)(0xF0 | (lum << 1))));
        }

        for (int hue = 1; hue < 16; hue++)
        {
            (int r, int g, int b) = Parts(palette.Rgba((byte)((hue << 4) | 0x08)));
            Assert.False(r == g && g == b, $"hue {hue}");
        }
    }

    [Theory]
    [InlineData("NTSC", 120)]
    [InlineData("PAL", 104)]
    public void Section_19_6_the_model_gives_the_sheets_count_of_colours(string region, int count)
    {
        TiaPalette palette = TestPrograms.RegionNamed(region).Palette;
        var colours = new HashSet<uint>();
        for (int n = 0; n < 256; n += 2)
        {
            colours.Add(palette.Rgba((byte)n));
        }

        Assert.Equal(count, colours.Count);
    }

    [Fact]
    public void Ntsc_and_pal_differ()
    {
        Assert.NotSame(Region.Ntsc.Palette, Region.Pal.Palette);
        int differ = 0;
        for (int n = 0; n < 256; n += 2)
        {
            if (Region.Ntsc.Palette.Rgba((byte)n) != Region.Pal.Palette.Rgba((byte)n))
            {
                differ++;
            }
        }

        // Hue 0 is the same grey in both; most of the rest differ.
        Assert.True(differ > 64, $"{differ} colours differ");
        Assert.Equal(Region.Ntsc.Palette.Rgba(0x0E), Region.Pal.Palette.Rgba(0x0E));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Black_and_white_are_the_ends_of_hue_0(string region)
    {
        // tia.md section 19.4: luminance 0 is black, and the contrast scale makes 7 white.
        TiaPalette palette = TestPrograms.RegionNamed(region).Palette;
        Assert.Equal(0xFF000000u, palette.Rgba(0x00));
        Assert.Equal(0xFFFFFFFFu, palette.Rgba(0x0E));
    }
}
