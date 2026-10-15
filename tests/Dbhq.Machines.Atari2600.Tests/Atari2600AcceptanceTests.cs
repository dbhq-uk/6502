using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The test that makes the Atari 2600 count as running: one test for each thing the spec's "What
/// is being built" says counts, each calling the check the other test files already make, so
/// nothing here restates a number or a list. Every trusted self-test of the pinned suite passes in
/// both regions (<see cref="SuiteSelfTests"/>), and the trust split leaves no test of the suite
/// out; every recorded test, self-test and screenshot alike, gives the outcome recorded in
/// <c>machines/atari-2600/suite-outcomes.json</c> in both regions (<see cref="SuiteRecordedTests"/>);
/// and the bundled game boots to its recorded frame in both regions, with a picture and with the
/// sound keeping up (<see cref="BootCheck"/>).
/// </summary>
/// <remarks>
/// <para>
/// The registry's <c>acceptance</c> field is to name this class (task 14 of the plan). Rename it
/// there too.
/// </para>
/// <para>
/// The runs are the ones the other test files make: <see cref="SuiteSelfTests"/>,
/// <see cref="SuiteOutcomes"/> and <see cref="BootCheck"/> keep each result the first time it is
/// asked for, so in a whole run of the test project each program runs once, whichever class asks
/// first.
/// </para>
/// </remarks>
public sealed class Atari2600AcceptanceTests(ITestOutputHelper output)
{
    public static TheoryData<string, string> TrustedRows() => SuiteSelfTests.TrustedRows();

    public static TheoryData<string, string> RecordedSelfTestRows() => SuiteRecordedTests.SelfTestRows();

    public static TheoryData<string, string> ScreenshotRows() => SuiteRecordedTests.ScreenshotRows();

    public static TheoryData<string> Regions() => BootCheck.Regions();

    [Theory]
    [MemberData(nameof(TrustedRows))]
    public void Every_trusted_self_test_passes(string test, string region)
    {
        SuiteSelfTests.AssertTrustedPasses(test, region, output.WriteLine);
    }

    [Fact]
    public void The_trust_split_leaves_no_test_of_the_suite_out()
    {
        SuiteSelfTests.AssertTheTrustSplitPlacesEveryTestOnce();
    }

    [Theory]
    [MemberData(nameof(RecordedSelfTestRows))]
    public void Every_recorded_self_test_gives_its_recorded_outcome(string test, string region)
    {
        SuiteRecordedTests.AssertSelfTestGivesItsRecordedOutcome(test, region, output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(ScreenshotRows))]
    public void Every_screenshot_test_gives_its_recorded_mismatch_count(string test, string region)
    {
        SuiteRecordedTests.AssertScreenshotGivesItsRecordedCount(test, region, output.WriteLine);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void The_bundled_game_boots_to_its_recorded_frame(string region)
    {
        Region chosen = BootCheck.RegionNamed(region);
        BootCheck.AssertFrameIsTheRecordedOne(chosen);
        BootCheck.AssertPictureIsNotBlank(chosen);
        BootCheck.AssertSoundKeptUp(chosen);
    }
}
