using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The region's numbers, from <c>docs/atari-2600/facts/region.md</c>, each test naming its
/// section. A figure the sheet derives is derived here again from the sheet's sources, so the test
/// does not copy a typed result.
/// </summary>
public class RegionTests
{
    /// <summary>The frame's line limit, the plan's "Fixed before running" item 2.</summary>
    private const int LineLimit = 400;

    /// <summary>The lines of VSYNC a frame starts with on both regions (region.md section 2).</summary>
    private const int VsyncLines = 3;

    public static TheoryData<string> Names() => new() { "NTSC", "PAL" };

    private static Region Named(string name) => name == "NTSC" ? Region.Ntsc : Region.Pal;

    [Fact]
    public void The_two_regions_are_named()
    {
        Assert.Equal("NTSC", Region.Ntsc.Name);
        Assert.Equal("PAL", Region.Pal.Name);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_cpu_clock_is_the_colour_clock_divided_by_three(string name)
    {
        // region.md section 1: the TIA divides its oscillator by three for the 6507's phase zero.
        Region region = Named(name);
        Assert.Equal(region.ColourClockHz, region.CpuHz * 3, 1e-6);
    }

    [Fact]
    public void The_ntsc_colour_clock_is_the_colour_subcarrier()
    {
        // region.md section 1: exactly 315/88 MHz, which Korth rounds to 3.579545 MHz.
        Assert.Equal(315_000_000.0 / 88, Region.Ntsc.ColourClockHz);
        Assert.Equal(3_579_545, Region.Ntsc.ColourClockHz, 1.0);
        Assert.Equal(1_193_182, Region.Ntsc.CpuHz, 1.0);
    }

    [Fact]
    public void The_pal_colour_clock_is_four_fifths_of_the_pal_subcarrier()
    {
        // region.md section 1 and open item 1: 4.43361875 MHz x 4 / 5, which is 3.546895 MHz;
        // Korth gives 3.546894 MHz, a part in three and a half million away.
        Assert.Equal(4_433_618.75 * 4 / 5, Region.Pal.ColourClockHz);
        Assert.Equal(3_546_894, Region.Pal.ColourClockHz, 2.0);
        Assert.Equal(1_182_298, Region.Pal.CpuHz, 1.0);
    }

    [Fact]
    public void The_regions_differ_in_their_clock()
    {
        Assert.NotEqual(Region.Ntsc.ColourClockHz, Region.Pal.ColourClockHz);
        Assert.NotEqual(Region.Ntsc.CpuHz, Region.Pal.CpuHz);
    }

    [Fact]
    public void A_frame_has_the_lines_atari_recommended()
    {
        // region.md section 2: 3 + 37 + 192 + 30 on NTSC, 3 + 45 + 228 + 36 on PAL.
        Assert.Equal(3 + 37 + 192 + 30, Region.Ntsc.NominalLines);
        Assert.Equal(3 + 45 + 228 + 36, Region.Pal.NominalLines);
    }

    [Fact]
    public void The_picture_window_is_the_sheets_choice()
    {
        // region.md section 3: the picture lines with 18 lines above and 18 below.
        Assert.Equal(37 - 18, Region.Ntsc.FirstShownLine);
        Assert.Equal(192 + 18 + 18, Region.Ntsc.ShownLines);
        Assert.Equal(45 - 18, Region.Pal.FirstShownLine);
        Assert.Equal(228 + 18 + 18, Region.Pal.ShownLines);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_window_fits_inside_the_line_limit_and_a_nominal_frame(string name)
    {
        // region.md section 3: inside the limit of 400, and inside a nominal frame less its VSYNC.
        Region region = Named(name);
        Assert.InRange(region.FirstShownLine, 0, LineLimit);
        Assert.True(region.FirstShownLine + region.ShownLines <= LineLimit);
        Assert.True(region.FirstShownLine + region.ShownLines <= region.NominalLines - VsyncLines);
        Assert.True(region.ShownLines > 0);
    }

    [Fact]
    public void The_ntsc_pixel_is_twelve_by_seven()
    {
        // region.md section 4: (135/11 MHz) / (315/88 MHz) / 2, the square-pixel rate over the
        // colour clock, over the two interlaced lines one 2600 line covers, is 12/7 exactly.
        Assert.Equal(12 * Region.Ntsc.PixelHeight, 7 * Region.Ntsc.PixelWidth);
        double expected = 135_000_000.0 / 11 / Region.Ntsc.ColourClockHz / 2;
        Assert.Equal(expected, (double)Region.Ntsc.PixelWidth / Region.Ntsc.PixelHeight, 1e-9);
    }

    [Fact]
    public void The_pal_pixel_is_within_a_part_in_a_thousand_of_the_sheets_shape()
    {
        // region.md section 4: 14.75 MHz over the PAL colour clock, over 2, held as two integers
        // within 1 part in 1000.
        double expected = 14_750_000.0 / Region.Pal.ColourClockHz / 2;
        double held = (double)Region.Pal.PixelWidth / Region.Pal.PixelHeight;
        Assert.True(Math.Abs(held - expected) / expected < 0.001, $"{held} is not within a part in a thousand of {expected}");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void The_pixel_shape_is_a_ratio_of_two_positive_integers(string name)
    {
        Region region = Named(name);
        Assert.True(region.PixelWidth > 0);
        Assert.True(region.PixelHeight > 0);
    }
}
