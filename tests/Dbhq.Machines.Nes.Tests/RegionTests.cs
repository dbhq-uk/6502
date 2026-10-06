using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The region's numbers, from <c>docs/nes/facts/timing.md</c> and <c>apu.md</c>. Every figure that
/// is derived is recomputed here from the master clock and the frame's shape, so the test does not
/// copy a typed rate.
/// </summary>
public class RegionTests
{
    public static TheoryData<string> Names() => new() { "NTSC", "PAL" };

    private static Region Named(string name) => name == "NTSC" ? Region.Ntsc : Region.Pal;

    [Fact]
    public void TheTwoRegionsAreNamed()
    {
        Assert.Equal("NTSC", Region.Ntsc.Name);
        Assert.Equal("PAL", Region.Pal.Name);
    }

    [Fact]
    public void TheFrameHasTheLinesTheSheetGives()
    {
        Assert.Equal(262, Region.Ntsc.Lines);
        Assert.Equal(312, Region.Pal.Lines);
        Assert.Equal(261, Region.Ntsc.PreRenderLine);
        Assert.Equal(311, Region.Pal.PreRenderLine);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ThePreRenderLineIsTheLastLine(string name)
    {
        Region region = Named(name);
        Assert.Equal(region.Lines - 1, region.PreRenderLine);
    }

    [Fact]
    public void OnlyNtscDropsADotOnAnOddFrame()
    {
        Assert.True(Region.Ntsc.OddFrameSkipsADot);
        Assert.False(Region.Pal.OddFrameSkipsADot);
    }

    [Fact]
    public void OnlyThePalPictureChipSwapsRedAndGreenInEmphasis()
    {
        Assert.False(Region.Ntsc.EmphasisSwapsRedAndGreen);
        Assert.True(Region.Pal.EmphasisSwapsRedAndGreen);
    }

    [Fact]
    public void TheDotRatioIsThreeToOneOnNtscAndSixteenToFiveOnPal()
    {
        Assert.Equal(3, Region.Ntsc.DotsNumerator);
        Assert.Equal(1, Region.Ntsc.DotsDenominator);
        Assert.Equal(16, Region.Pal.DotsNumerator);
        Assert.Equal(5, Region.Pal.DotsDenominator);
    }

    [Theory]
    [InlineData("NTSC", 15)]
    [InlineData("PAL", 16)]
    public void FiveCpuCyclesRunThisManyDots(string name, int dots)
    {
        Region region = Named(name);
        Assert.Equal(dots, region.DotsNumerator * 5 / region.DotsDenominator);
    }

    [Fact]
    public void TheCpuClockIsTheMasterClockOverItsDivider()
    {
        // timing.md 1: NTSC 236.25 MHz / 11 / 12; PAL 26.6017125 MHz / 16.
        Assert.Equal(236_250_000.0 / 11 / 12, Region.Ntsc.CpuHz, 6);
        Assert.Equal(26_601_712.5 / 16, Region.Pal.CpuHz, 6);
    }

    [Fact]
    public void TheFrameRateFollowsFromTheClockTheDotsAndTheLines()
    {
        // NTSC: CPU clock x 3 dots / (341 x 262 dots, less the half dot an odd frame drops on
        // average). PAL: CPU clock x 3.2 dots / (341 x 312 dots).
        double ntscMaster = 236_250_000.0 / 11;
        double ntsc = ntscMaster / 12 * 3 / (341 * 262 - 0.5);
        double palMaster = 26_601_712.5;
        double pal = palMaster / 16 * (16.0 / 5) / (341 * 312);

        Assert.Equal(ntsc, Region.Ntsc.FramesPerSecond, 3);
        Assert.Equal(pal, Region.Pal.FramesPerSecond, 3);
        // And they are near the figures timing.md 1 gives, which is a check on the formula.
        Assert.InRange(Region.Ntsc.FramesPerSecond, 60.098, 60.099);
        Assert.InRange(Region.Pal.FramesPerSecond, 50.006, 50.008);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheNoiseAndDmcTablesHaveSixteenEntries(string name)
    {
        Region region = Named(name);
        Assert.Equal(16, region.NoisePeriods.Count);
        Assert.Equal(16, region.DmcRates.Count);
    }

    [Fact]
    public void TheNoisePeriodsAreApuSection7()
    {
        Assert.Equal(
            new[] { 4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068 },
            Region.Ntsc.NoisePeriods);
        Assert.Equal(
            new[] { 4, 8, 14, 30, 60, 88, 118, 148, 188, 236, 354, 472, 708, 944, 1890, 3778 },
            Region.Pal.NoisePeriods);
    }

    [Fact]
    public void TheDmcRatesAreApuSection8()
    {
        Assert.Equal(
            new[] { 428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54 },
            Region.Ntsc.DmcRates);
        Assert.Equal(
            new[] { 398, 354, 316, 298, 276, 236, 210, 198, 176, 148, 132, 118, 98, 78, 66, 50 },
            Region.Pal.DmcRates);
    }

    [Fact]
    public void TheFrameCounterListsAreTheRowsOfApuSection10()
    {
        // The CPU cycles of the sheet's table, row by row, the last being where the sequence wraps.
        Assert.Equal(new[] { 7457, 14913, 22371, 29828, 29829, 29830 }, Region.Ntsc.FrameCounterFourStep);
        Assert.Equal(new[] { 7457, 14913, 22371, 29829, 37281, 37282 }, Region.Ntsc.FrameCounterFiveStep);
        Assert.Equal(new[] { 8313, 16627, 24939, 33252, 33253, 33254 }, Region.Pal.FrameCounterFourStep);
        Assert.Equal(new[] { 8313, 16627, 24939, 33253, 41565, 41566 }, Region.Pal.FrameCounterFiveStep);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheFrameCounterListsRiseAndTheFourStepRepeatsEveryFrameIrqPeriod(string name)
    {
        Region region = Named(name);
        foreach (IReadOnlyList<int> steps in new[] { region.FrameCounterFourStep, region.FrameCounterFiveStep })
        {
            for (int i = 1; i < steps.Count; i++)
            {
                Assert.True(steps[i] > steps[i - 1]);
            }
        }

        // apu.md 10: the frame IRQ is set every 29830 cycles on NTSC and 33254 on PAL.
        int period = name == "NTSC" ? 29830 : 33254;
        Assert.Equal(period, region.FrameCounterFourStep[^1]);
    }

    [Fact]
    public void TheTablesCannotBeChangedThroughTheInterface()
    {
        Assert.IsNotType<int[]>(Region.Ntsc.NoisePeriods);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)Region.Ntsc.NoisePeriods)[0] = 1);
    }
}
