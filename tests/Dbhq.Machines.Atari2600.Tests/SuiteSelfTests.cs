using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The suite's trusted self-tests, through the real bus, each in its own region (the plan's "The
/// suite's results, and what each one gates"; <c>docs/atari-2600/facts/suite.md</c>). A row is
/// added by the task that builds the last part its test needs, as <c>suite.md</c> section 5 places
/// it. Tasks 4 to 10 kept a "not yet" list here of trusted tests whose parts were not built, each
/// failing exactly as written; task 11 emptied it, and removed it: every trusted test passes but
/// four, which moved to the recorded tests with Dan's agreement on 15 October 2026
/// (<see cref="SuiteOutcomes.MovedFromTrusted"/>). <see cref="NotRun"/> lists the tests of
/// boards not modelled, with each board's issue, and
/// <see cref="The_trust_split_places_every_test_in_the_suite_exactly_once"/> checks that nothing
/// is left out.
/// </summary>
public class SuiteSelfTests(ITestOutputHelper output)
{
    /// <summary>The trusted self-tests that pass from tasks 4, 6 and 7 (suite.md sections 5.1 to 5.7, and section 9's tasks).</summary>
    public static readonly string[] Trusted =
    [
        // cpu/, task 4 (suite.md 5.1).
        "cpu/floating-bus",
        "cpu/open-bus",
        "cpu/rmw-wsync",
        "cpu/stack-aliases-ram",

        // riot/, task 4 (5.2).
        "riot/io-mirrors",
        "riot/io-output",
        "riot/io-ports",
        "riot/pa7-edge",
        "riot/pa7-polarity",
        "riot/ram-mirrors",
        "riot/ram",
        "riot/timer-divisors",
        "riot/timer-underflow-read",
        "riot/timer-vblank-spin",
        "riot/timer",

        // harness/, task 4 (5.3).
        "harness/sanity",

        // tia-timing/, the two that need only the halt and the timer (5.4, and section 10).
        "tia-timing/wsync",
        "tia-timing/wsync-line-end",

        // cartridge/, the four trusted tests on the boards of task 2 (5.7).
        "cartridge/mirror-2k",
        "cartridge/bank-f8",
        "cartridge/bank-f6",
        "cartridge/bank-f4",

        // Task 6, the objects, priority and the collisions: the rows suite.md section 9 gives task 6.
        "cpu/rmw-strobe",
        "cpu/stack-aliases-tia",
        "tia-timing/reset-same-line",
        "tia-timing/resp-restrobe",
        "tia-timing/rsync",
        "tia-render/missiles-ball",
        "tia-render/nusiz",
        "tia-render/player-reflect",
        "tia-render/players",
        "tia-render/playfield-reflect",
        "tia-render/playfield",
        "tia-render/positioning",
        "tia-render/vertical-delay",
        "collision/all-pairs",
        "collision/latches",
        "collision/onset-sweep",
        "collision/per-pixel",
        "collision/reset-phase",

        // Task 9, the inputs: cpu/partial-drive, the one row suite.md gives task 9. It passed from
        // task 6, because it reads only the inputs at rest (a dumped INPT0 reads 0, an open fire
        // button 1), which task 5 had fixed; with the joysticks, the fire latch and the paddle ports
        // built from tia.md section 20 it still passes.
        "cpu/partial-drive",

        // Task 7, HMOVE and the motion clocks: the rows suite.md section 9 gives task 7, but the
        // four collision/merge-delivery tests, recorded since 15 October 2026 with Dan's
        // agreement (SuiteOutcomes.MovedFromTrusted). collision/blank-gating passed from task 6
        // on a visible overlap; with HMOVE its group 2 missiles meet inside the blank, and a fault
        // that stops the latches in the blank now fails it (the task 7 journal entry).
        "tia-timing/hmove-apply",
        "tia-timing/hmove-line-aligned",
        "tia-timing/hmove-reset-merge",
        "tia-timing/hmove-walk",
        "tia-render/missile-copies",
        "tia-render/missile-reset-lock",
        "collision/blank-gating",
        "collision/blanked-reset",
        "collision/copy-adjacency",
        "collision/hmove-edge",
        "collision/latency-swallow",
        "collision/resbl-kill",
        "collision/stuck-drift",
    ];

    /// <summary>
    /// The suite's tests for boards the machine does not model, with each board's issue
    /// (<c>suite.md</c> section 5.8): not run, in any region, until the board is built. Every
    /// SECAM build is not run either, because SECAM is not modelled (#106).
    /// </summary>
    public static readonly (string Test, int Issue)[] NotRun =
    [
        ("cartridge/ar-config", 154),
        ("cartridge/ar-hazards", 154),
        ("cartridge/ar-write", 154),
        ("cartridge/bank-03e0", 152),
        ("cartridge/bank-0840", 145),
        ("cartridge/bank-0fa0", 153),
        ("cartridge/bank-3e-wide", 136),
        ("cartridge/bank-3e", 136),
        ("cartridge/bank-3ep-wide", 137),
        ("cartridge/bank-3ep", 137),
        ("cartridge/bank-3f", 132),
        ("cartridge/bank-bf", 141),
        ("cartridge/bank-df", 142),
        ("cartridge/bank-e0", 131),
        ("cartridge/bank-e7", 133),
        ("cartridge/bank-ef", 140),
        ("cartridge/bank-f0", 138),
        ("cartridge/bank-fc", 150),
        ("cartridge/bank-fe", 135),
        ("cartridge/bank-jane", 151),
        ("cartridge/bank-mdm", 147),
        ("cartridge/bank-sb", 143),
        ("cartridge/bank-ua", 139),
        ("cartridge/bank-wd", 148),
        ("cartridge/bank-wf8", 149),
        ("cartridge/bank-x07", 146),
        ("cartridge/dpc-bank", 134),
        ("cartridge/dpc-drawline", 134),
        ("cartridge/dpc-fetch", 134),
        ("cartridge/dpc-flag-edges", 134),
        ("cartridge/dpc-flag", 134),
        ("cartridge/dpc-music", 134),
        ("cartridge/dpc-probes", 134),
        ("cartridge/dpc-rng", 134),
        ("cartridge/dpc-shift", 134),
        ("cartridge/dpc-swizzle", 134),
        ("cartridge/dpc-window-reads", 134),
        ("cartridge/ram-cv", 144),
    ];

    /// <summary>The folders whose self-tests are trusted (the plan's trust table), with <c>harness/sanity</c>.</summary>
    private static readonly string[] TrustedFolders = ["cpu/", "riot/", "collision/", "tia-timing/", "tia-render/"];

    /// <summary>The trusted cartridge tests, on the boards of task 2 (the plan's trust table).</summary>
    private static readonly string[] TrustedCartridge = ["cartridge/mirror-2k", "cartridge/bank-f8", "cartridge/bank-f6", "cartridge/bank-f4"];

    /// <summary>
    /// The recorded cartridge tests, whose headers the plan's trust table does not vouch for. Typed
    /// here, apart from <see cref="SuiteOutcomes.SelfTests"/>, so a test moved into that list from
    /// <see cref="NotRun"/> fails the split check (task 11's review).
    /// </summary>
    private static readonly string[] RecordedCartridge = ["cartridge/bank-fa", "cartridge/bank-hotspot-window", "cartridge/power-on-bank", "cartridge/ram-superchip"];

    public static TheoryData<string, string> TrustedRows() => Rows(Trusted);



    [Theory]
    [MemberData(nameof(TrustedRows))]
    public void Trusted_self_test_passes(string test, string region)
    {
        SuiteOutcome outcome = SuiteRunner.Run(test, TestPrograms.RegionNamed(region));
        output.WriteLine(outcome.ToString());
        Assert.True(outcome.Passed, outcome.ToString());
    }

    [Fact]
    public void No_test_is_in_two_lists()
    {
        string[] all = [.. Trusted, .. SuiteOutcomes.SelfTests, .. ScreenshotRunner.Tests, .. NotRun.Select(row => row.Test)];
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        foreach (string test in all)
        {
            Assert.Contains($"{test}_ntsc.a26", Dbhq.Cpu6502.TestSupport.Atari2600Suite.Names);
            Assert.Contains($"{test}_pal.a26", Dbhq.Cpu6502.TestSupport.Atari2600Suite.Names);
        }
    }

    /// <summary>
    /// The trust split, applied to every source in the pinned suite (the plan's trust table;
    /// <c>suite.md</c> section 1.3): a screenshot test is recorded; a self-test in a trusted folder,
    /// <c>harness/sanity</c> and the four trusted cartridge tests are trusted, and so in
    /// <see cref="Trusted"/>, unless Dan agreed to move it to recorded
    /// (<see cref="SuiteOutcomes.MovedFromTrusted"/>); the recorded cartridge tests are recorded; and
    /// every other cartridge test is in <see cref="NotRun"/>. So no test is skipped silently.
    /// </summary>
    [Fact]
    public void The_trust_split_places_every_test_in_the_suite_exactly_once()
    {
        string[] sources = [.. Dbhq.Cpu6502.TestSupport.Atari2600Suite.Names.Where(name => name.EndsWith(".asm", StringComparison.Ordinal)).Select(name => name[..^".asm".Length])];
        Assert.NotEmpty(sources);
        HashSet<string> trusted = [.. Trusted];
        HashSet<string> screenshots = [.. ScreenshotRunner.Tests];
        HashSet<string> notRun = [.. NotRun.Select(row => row.Test)];
        foreach (string test in sources)
        {
            bool screenshot = screenshots.Contains(test);
            bool moved = SuiteOutcomes.MovedFromTrusted.Contains(test);
            bool trustedBySplit = !screenshot && (TrustedFolders.Any(folder => test.StartsWith(folder, StringComparison.Ordinal)) || test == "harness/sanity" || TrustedCartridge.Contains(test));
            bool recordedBySplit = screenshot || RecordedCartridge.Contains(test) || moved;
            Assert.True((RecordedCartridge.Contains(test) || moved) == SuiteOutcomes.SelfTests.Contains(test), $"{test}: a recorded cartridge test or moved from trusted is {RecordedCartridge.Contains(test) || moved}, in SuiteOutcomes.SelfTests is {SuiteOutcomes.SelfTests.Contains(test)}");
            Assert.True(!moved || trustedBySplit, $"{test}: moved from trusted, but the split does not trust it");
            Assert.True((trustedBySplit && !moved) == trusted.Contains(test), $"{test}: trusted by the split and not moved is {trustedBySplit && !moved}, in Trusted is {trusted.Contains(test)}");
            Assert.True(!moved || recordedBySplit, $"{test}: moved from trusted, so it must be a recorded self-test");
            Assert.True((!trustedBySplit && !recordedBySplit) == notRun.Contains(test), $"{test}: neither trusted nor recorded by the split, so it must be in NotRun, and only then");
        }

        Assert.Equal(sources.Length, trusted.Count + screenshots.Count + SuiteOutcomes.SelfTests.Length + notRun.Count);
    }

    /// <summary>
    /// Each test moved from trusted to recorded has its <c>docs/known-differences.md</c> entry, as the
    /// plan's trust table asks ("moves to recorded with a known-differences entry giving its cause and
    /// Dan's agreement"): its name appears there (task 11's review).
    /// </summary>
    [Fact]
    public void Each_test_moved_from_trusted_is_named_in_the_known_differences()
    {
        string known = File.ReadAllText(Path.Combine(Dbhq.Cpu6502.TestSupport.RepoPaths.Root, "docs", "known-differences.md"));
        foreach (string test in SuiteOutcomes.MovedFromTrusted)
        {
            Assert.True(known.Contains($"`{test}`", StringComparison.Ordinal), $"{test} moved from trusted, but docs/known-differences.md does not name it");
        }
    }

    private static TheoryData<string, string> Rows(IEnumerable<string> tests)
    {
        var data = new TheoryData<string, string>();
        foreach (string test in tests)
        {
            data.Add(test, "NTSC");
            data.Add(test, "PAL");
        }

        return data;
    }
}
