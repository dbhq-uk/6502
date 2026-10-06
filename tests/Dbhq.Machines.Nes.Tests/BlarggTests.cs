using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// Blargg's test ROMs, and the others from the fork at the pinned commit, group by group. The
/// table of which ROMs run, on which region, and what each must report is
/// <see cref="TestRomTable"/>, with the reasons; each test here runs one group's rows through
/// that table's check, which <see cref="NesAcceptanceTests"/> runs too.
/// </summary>
public class BlarggTests(ITestOutputHelper output)
{
    [Theory]
    [MemberData(nameof(TestRomTable.SpriteHitTests), MemberType = typeof(TestRomTable))]
    public void EachSpriteHitTestPasses(string rom)
    {
        TestRomTable.AssertScreenReportingRomPasses($"sprite_hit_tests_2005.10.05/{rom}.nes", Region.Ntsc);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.SpriteOverflowTests), MemberType = typeof(TestRomTable))]
    public void EachSpriteOverflowTestPasses(string rom)
    {
        TestRomTable.AssertScreenReportingRomPasses($"sprite_overflow_tests/{rom}.nes", Region.Ntsc);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.ApuTestSingles), MemberType = typeof(TestRomTable))]
    public void EachApuTestSinglePasses(string single)
    {
        TestRomTable.AssertApuTestSinglePasses(single);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.PalApuTests), MemberType = typeof(TestRomTable))]
    public void EachPalApuTestPasses(string rom)
    {
        TestRomTable.AssertScreenReportingRomPasses($"pal_apu_tests/{rom}.nes", Region.Pal);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.DmcDmaDuringRead4), MemberType = typeof(TestRomTable))]
    public void EachDmcDmaDuringRead4RomPrintsAnOutputItsSourceAccepts(string rom, string[] accepted)
    {
        TestRomTable.AssertDmcDmaDuringRead4RomPrintsAnOutputItsSourceAccepts(rom, accepted);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.DmcDmaKnownFailures), MemberType = typeof(TestRomTable))]
    public void EachKnownFailureStillFailsAsWrittenDown(string rom, string printsNow, string[] accepted, string cause)
    {
        TestRomTable.AssertDmcDmaKnownFailureStillFailsAsWrittenDown(rom, printsNow, accepted, cause, output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.SprdmaAndDmcDma), MemberType = typeof(TestRomTable))]
    public void EachSprdmaAndDmcDmaRomPasses(string rom)
    {
        TestRomTable.AssertSprdmaAndDmcDmaRomPasses(rom);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.ApuMixerRoms), MemberType = typeof(TestRomTable))]
    public void EachApuMixerRomCancelsItsToneToNearSilence(string rom)
    {
        TestRomTable.AssertApuMixerRomCancelsItsToneToNearSilence(rom, output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.CombinedMmc1Roms), MemberType = typeof(TestRomTable))]
    public void EachCombinedMmc1RomPasses(string pinnedName, string expected, Region region, int millions)
    {
        TestRomTable.AssertCombinedMmc1RomPasses(pinnedName, expected, region, millions, output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.RamReportingRoms), MemberType = typeof(TestRomTable))]
    public void EachRamReportingRomPasses(string pinnedName, Region region, int millions)
    {
        TestRomTable.AssertRamReportingRomPasses(pinnedName, region, millions, output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.RamReportingKnownFailures), MemberType = typeof(TestRomTable))]
    public void EachRamReportingKnownFailureStillFailsAsWrittenDown(string pinnedName, Region region, int statusNow, string printsNow, int millions, string cause)
    {
        TestRomTable.AssertRamReportingKnownFailureStillFailsAsWrittenDown(pinnedName, region, statusNow, printsNow, millions, cause, output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.BranchTimingTests), MemberType = typeof(TestRomTable))]
    public void EachBranchTimingTestPasses(string rom)
    {
        TestRomTable.AssertScreenReportingRomPasses($"branch_timing_tests/{rom}.nes", Region.Ntsc);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.BlarggApu2005), MemberType = typeof(TestRomTable))]
    public void EachBlarggApu2005RomPrintsResultCode1(string rom)
    {
        TestRomTable.AssertBlarggApu2005RomPrintsResultCode1(rom);
    }

    [Fact]
    public void CpuDummyReadsPasses()
    {
        TestRomTable.AssertCpuDummyReadsPasses();
    }

    [Theory]
    [MemberData(nameof(TestRomTable.Mmc3Test2Singles), MemberType = typeof(TestRomTable))]
    public void EachMmc3Test2SinglePasses(string single)
    {
        TestRomTable.AssertMmc3Test2SinglePasses(single);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.Mmc3IrqTests), MemberType = typeof(TestRomTable))]
    public void EachMmc3IrqTestPasses(string rom)
    {
        TestRomTable.AssertScreenReportingRomPasses($"mmc3_irq_tests/{rom}.nes", Region.Ntsc);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.Mmc3OtherRevision), MemberType = typeof(TestRomTable))]
    public void EachMmc3RomOfTheOtherRevisionStillFailsAsWrittenDown(string rom, bool screen, int statusNow, string printsNow, string cause)
    {
        TestRomTable.AssertMmc3RomOfTheOtherRevisionStillFailsAsWrittenDown(rom, screen, statusNow, printsNow, cause, output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(TestRomTable.PpuVblNmiSingles), MemberType = typeof(TestRomTable))]
    public void EachPpuVblNmiSinglePasses(string single)
    {
        TestRomTable.AssertPpuVblNmiSinglePasses(single);
    }
}
