using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The suite's recorded tests (the plan's trust table, task 11): the four cartridge self-tests the
/// headers do not vouch for, and every screenshot test, each in both regions, must give the outcome
/// recorded in <c>machines/atari-2600/suite-outcomes.json</c>. A changed outcome fails here; it goes
/// in the journal before the file is recorded again. <see cref="SuiteOutcomes"/> says what an outcome is.
/// </summary>
public class SuiteRecordedTests(ITestOutputHelper output)
{
    public static TheoryData<string, string> SelfTestRows() => Rows(SuiteOutcomes.SelfTests);

    public static TheoryData<string, string> ScreenshotRows() => Rows(ScreenshotRunner.Tests);

    [Theory]
    [MemberData(nameof(SelfTestRows))]
    public void Recorded_self_test_gives_its_recorded_outcome(string test, string region)
    {
        JsonNode outcome = SuiteOutcomes.SelfTestOutcome(test, region);
        output.WriteLine($"{test} ({region}): {outcome.ToJsonString()}");
        AssertRecorded("selfTests", test, region, outcome);
    }

    [Theory]
    [MemberData(nameof(ScreenshotRows))]
    public void Screenshot_test_gives_its_recorded_mismatch_count(string test, string region)
    {
        JsonNode outcome = JsonValue.Create(SuiteOutcomes.ScreenshotOutcome(test, region));
        output.WriteLine($"{test} ({region}): {outcome.ToJsonString()} mismatched pixels");
        AssertRecorded("screenshots", test, region, outcome);
    }

    [Fact]
    public void Each_kind_of_self_test_outcome_is_recorded_in_its_own_shape()
    {
        // Every shape SuiteOutcomes describes, from made-up runs, because the suite's recorded
        // tests do not give them all (none gives no verdict). Added after task 11's fault pass,
        // where a no verdict recorded as a pass survived.
        Assert.Equal("\"pass\"", SuiteOutcomes.Format(new SuiteOutcome("cartridge/bank-fa", "ntsc", 0xA5, 0, 0xB1, 0, 1)).ToJsonString());
        Assert.Equal("{\"outcome\":\"pass\",\"observed\":\"$A0\"}", SuiteOutcomes.Format(new SuiteOutcome("cartridge/power-on-bank", "ntsc", 0xA5, 0, 0xA0, 0, 1)).ToJsonString());
        Assert.Equal("{\"outcome\":\"fail\",\"code\":\"$0C\",\"observed\":\"$01\",\"expected\":\"$02\"}", SuiteOutcomes.Format(new SuiteOutcome("cartridge/bank-fa", "pal", 0x5A, 0x0C, 0x01, 0x02, 1)).ToJsonString());
        Assert.Equal("{\"outcome\":\"no verdict\",\"result\":\"$00\"}", SuiteOutcomes.Format(new SuiteOutcome("cartridge/bank-fa", "pal", 0x00, 0, 0, 0, SuiteRunner.InstructionLimit)).ToJsonString());
    }

    /// <summary>
    /// The recorder. With <c>ATARI_RECORD=1</c> it writes <c>machines/atari-2600/suite-outcomes.json</c>
    /// from the machine's own outcomes. Run: <c>ATARI_RECORD=1 dotnet test
    /// tests/Dbhq.Machines.Atari2600.Tests --filter Record</c>.
    /// </summary>
    /// <remarks>
    /// Without the variable it checks that the committed file is, byte for byte, what it would
    /// write, so the file was made by the recorder and not edited by hand, and holds no test that
    /// is not recorded (<c>AGENTS.md</c> rule 5). It is not skipped instead: the site's results
    /// refuse any skipped test.
    /// </remarks>
    [Fact]
    public void Record_the_suite_outcomes()
    {
        string text = SuiteOutcomes.Text();
        if (!SuiteOutcomes.Recording)
        {
            Assert.True(File.Exists(SuiteOutcomes.FilePath), $"{SuiteOutcomes.FilePath} does not exist: record it with {SuiteOutcomes.RecordCommand}");
            Assert.Equal(text, File.ReadAllText(SuiteOutcomes.FilePath));
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(SuiteOutcomes.FilePath)!);
        File.WriteAllText(SuiteOutcomes.FilePath, text);
        output.WriteLine($"wrote {SuiteOutcomes.FilePath}");
    }

    // Every row compares, the recorder's run included: it compares with the file as it stood when
    // the run began (SuiteOutcomes.Recorded), so the recorder writing the file in the same run
    // cannot race it, and a run with ATARI_RECORD=1 cannot pass a changed outcome vacuously (task
    // 11's review). A changed outcome then fails while it is recorded, which is the point.
    private static void AssertRecorded(string kind, string test, string region, JsonNode outcome)
    {
        JsonNode? recorded = SuiteOutcomes.Recorded(kind, test, region);
        Assert.True(recorded is not null, $"{test} ({region}) is not in {SuiteOutcomes.FilePath}: record it with {SuiteOutcomes.RecordCommand}");
        Assert.True(JsonNode.DeepEquals(recorded, outcome), $"{test} ({region}) gave {outcome.ToJsonString()} where {recorded!.ToJsonString()} is recorded. Say why in the journal, then record it again with {SuiteOutcomes.RecordCommand}.");
    }

    private static TheoryData<string, string> Rows(IEnumerable<string> tests)
    {
        var data = new TheoryData<string, string>();
        foreach (string test in tests)
        {
            foreach (string region in SuiteOutcomes.Regions)
            {
                data.Add(test, region);
            }
        }

        return data;
    }
}
