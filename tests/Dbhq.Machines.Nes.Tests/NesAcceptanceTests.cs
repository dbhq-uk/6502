using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The test that makes the NES count as running: one test for each thing the spec's "Proof"
/// says counts, each calling the check the other test files already make, so nothing here
/// restates a number. <c>nestest</c> through the real bus in both regions
/// (<see cref="NestestOnTheBusTests"/>); every pinned test ROM that <see cref="TestRomTable"/>
/// says passes, and every known failure failing as it is written down
/// (<see cref="BlarggTests"/>); the boot check of the bundled homebrew in both regions
/// (<see cref="BootCheck"/>); and the PRG RAM rule (<see cref="PrgRamRule"/>).
/// </summary>
/// <remarks>
/// <para>
/// The registry's <c>acceptance</c> field names this class. Rename it there too.
/// </para>
/// <para>
/// The test ROMs are the same runs <see cref="BlarggTests"/> makes: <see cref="TestRomTable"/>
/// keeps each result the first time it is asked for, so in a whole run of the test project each
/// ROM runs once, whichever class asks first. Run alone, this class takes about as long as
/// <see cref="BlarggTests"/> does.
/// </para>
/// </remarks>
public sealed class NesAcceptanceTests(ITestOutputHelper output)
{
    public static TheoryData<string> Regions() => BootCheck.Regions();

    public static TheoryData<string> PassingTestRoms()
    {
        var names = new TheoryData<string>();
        foreach (string name in TestRomTable.Passing.Keys.Order(StringComparer.Ordinal))
        {
            names.Add(name);
        }

        return names;
    }

    public static TheoryData<string> KnownFailures()
    {
        var names = new TheoryData<string>();
        foreach (string name in TestRomTable.KnownFailures.Keys.Order(StringComparer.Ordinal))
        {
            names.Add(name);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void NestestRunsThroughTheRealBus(string region)
    {
        NestestOnTheBusTests.AssertEveryLineMatches(BootCheck.RegionNamed(region));
    }

    [Theory]
    [MemberData(nameof(PassingTestRoms))]
    public void EveryPinnedTestRomThatPassesPasses(string rom)
    {
        TestRomTable.Passing[rom](output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(KnownFailures))]
    public void EveryKnownFailureFailsAsWrittenDown(string rom)
    {
        TestRomTable.KnownFailures[rom](output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheHomebrewBootsToItsRecordedPictureWithSound(string region)
    {
        Region chosen = BootCheck.RegionNamed(region);
        BootCheck.AssertFrameIsTheRecordedOne(chosen);
        BootCheck.AssertPictureIsNotBlank(chosen);
        BootCheck.AssertSoundPlays(chosen);
    }

    [Theory]
    [MemberData(nameof(PrgRamRule.Mappers), MemberType = typeof(PrgRamRule))]
    public void BatteryBackedPrgRamSurvivesResetAndNotAPowerCycle(int mapper)
    {
        PrgRamRule.AssertResetKeepsItAndAPowerCycleClearsIt(mapper);
    }
}
