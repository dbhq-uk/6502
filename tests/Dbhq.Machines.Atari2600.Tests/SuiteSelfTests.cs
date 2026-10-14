using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The suite's trusted self-tests, through the real bus, each in its own region (the plan's "The
/// suite's results, and what each one gates"; <c>docs/atari-2600/facts/suite.md</c>). A row is
/// added by the task that builds the last part its test needs, as <c>suite.md</c> section 5 places
/// it; a trusted test in a folder already run whose parts are not built yet is in
/// <see cref="NotYet"/> with that task's number, and must not pass until it moves. Task 11 ends
/// with <see cref="NotYet"/> empty.
/// </summary>
public class SuiteSelfTests(ITestOutputHelper output)
{
    /// <summary>The trusted self-tests that pass from task 4 and task 6 (suite.md sections 5.1 to 5.7, and section 9's tasks).</summary>
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

        // Task 6, the objects, priority and the collisions (suite.md section 9: two in cpu/, three
        // in tia-timing/, eight in tia-render/ and five in collision/).
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

        // Two that pass from task 6, before the task suite.md gives them, and are not claimed as
        // task 6's: their task must keep them passing. cpu/partial-drive (task 9) reads only the
        // inputs at rest, which task 5 fixed (a dumped INPT0 reads 0, an open fire button 1), and
        // its collision checks are task 6's. collision/blank-gating (task 7) needs objects whose
        // outputs fire inside the horizontal blank; in this model a start held as its counter waits
        // through the blank does that without HMOVE, which task 7 builds and the test was written
        // for, so its pass here is not yet the mechanism the header describes.
        "cpu/partial-drive",
        "collision/blank-gating",
    ];

    /// <summary>
    /// Trusted self-tests in the folders run here whose parts come in a later task, with the next
    /// task that must run them again (suite.md section 5.1). Each must fail, in both regions, with
    /// the CODE, OBSERVED and EXPECTED written beside it, the first check that needs a part not built
    /// yet: so a hang, a pass or a different failure is seen.
    /// </summary>
    public static readonly (string Test, int Task, byte Code, byte Observed, byte Expected)[] NotYet =
    [
        // The trusted self-tests in tia-timing/, tia-render/ and collision/ that use HMOVE, which
        // task 7 builds (suite.md sections 5.4 to 5.6 and 10). Their first failures were read on
        // 14 October 2026, after task 6.
        ("tia-timing/hmove-apply", 7, 0x01, 0xFF, 0x01),
        ("tia-timing/hmove-line-aligned", 7, 0x02, 0x40, 0x00),
        ("tia-timing/hmove-reset-merge", 7, 0x01, 0xFF, 0x0F),
        ("tia-timing/hmove-walk", 7, 0x01, 0x00, 0xC0),
        ("tia-render/missile-copies", 7, 0x16, 0x00, 0x40),
        ("tia-render/missile-reset-lock", 7, 0x01, 0x00, 0x01),
        ("collision/blanked-reset", 7, 0x02, 0x00, 0x20),
        ("collision/copy-adjacency", 7, 0x01, 0xFF, 0xE0),
        ("collision/hmove-edge", 7, 0x01, 0x00, 0xF8),
        ("collision/latency-swallow", 7, 0x12, 0x00, 0x80),
        ("collision/merge-delivery-replant", 7, 0x01, 0x00, 0x4D),
        ("collision/merge-delivery-stretch", 7, 0x02, 0x00, 0x3E),
        ("collision/merge-delivery-train", 7, 0x02, 0x01, 0x7F),
        ("collision/merge-delivery", 7, 0x01, 0x44, 0x7B),
        ("collision/resbl-kill", 7, 0x03, 0x00, 0x20),
        ("collision/stuck-drift", 7, 0x02, 0x00, 0x80),
    ];

    /// <summary>
    /// The recorded cartridge tests (the plan's trust table): run and shown, never gating here, so
    /// their test asserts nothing about the outcome.
    /// Task 11 records their outcomes in <c>machines/atari-2600/suite-outcomes.json</c>.
    /// </summary>
    public static readonly string[] Recorded =
    [
        "cartridge/bank-fa",
        "cartridge/bank-hotspot-window",
        "cartridge/power-on-bank",
        "cartridge/ram-superchip",
    ];

    public static TheoryData<string, string> TrustedRows() => Rows(Trusted);

    public static TheoryData<string, string> NotYetRows() => Rows(NotYet.Select(row => row.Test));

    public static TheoryData<string, string> RecordedRows() => Rows(Recorded);

    [Theory]
    [MemberData(nameof(TrustedRows))]
    public void Trusted_self_test_passes(string test, string region)
    {
        SuiteOutcome outcome = SuiteRunner.Run(test, TestPrograms.RegionNamed(region));
        output.WriteLine(outcome.ToString());
        Assert.True(outcome.Passed, outcome.ToString());
    }

    [Theory]
    [MemberData(nameof(NotYetRows))]
    public void Trusted_self_test_waiting_for_a_later_task_fails_where_expected(string test, string region)
    {
        var row = NotYet.Single(row => row.Test == test);
        SuiteOutcome outcome = SuiteRunner.Run(test, TestPrograms.RegionNamed(region));
        output.WriteLine($"{outcome} (task {row.Task} runs it next)");
        Assert.False(outcome.Passed, $"{outcome}: it passes now, so move it from NotYet to Trusted");
        Assert.True(outcome.Failed, $"{outcome}: no verdict, where a fail at CODE ${row.Code:X2} was expected");
        Assert.Equal((row.Code, row.Observed, row.Expected), (outcome.Code, outcome.Observed, outcome.Expected));
    }

    [Theory]
    [MemberData(nameof(RecordedRows))]
    public void Recorded_cartridge_test_is_run_and_shown(string test, string region)
    {
        SuiteOutcome outcome = SuiteRunner.Run(test, TestPrograms.RegionNamed(region));
        output.WriteLine(outcome.ToString());
    }

    [Fact]
    public void No_test_is_in_two_lists()
    {
        string[] all = [.. Trusted, .. NotYet.Select(row => row.Test), .. Recorded];
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        foreach (string test in all)
        {
            Assert.Contains($"{test}_ntsc.a26", Dbhq.Cpu6502.TestSupport.Atari2600Suite.Names);
            Assert.Contains($"{test}_pal.a26", Dbhq.Cpu6502.TestSupport.Atari2600Suite.Names);
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
