using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The suite's recorded outcomes (the plan's "The suite's results, and what each one gates"):
/// what each recorded test gives on the machine, and the file that keeps them,
/// <c>machines/atari-2600/suite-outcomes.json</c>, which only the recorder in
/// <see cref="SuiteRecordedTests"/> writes (<c>AGENTS.md</c> rule 5).
/// </summary>
/// <remarks>
/// <para>
/// <b>What is recorded.</b> The four cartridge self-tests whose boards' behaviour the suite's
/// headers call untested on hardware or a diagnostic (<see cref="SelfTests"/>), and every screenshot
/// test (<see cref="ScreenshotRunner.Tests"/>), each in both regions (<c>suite.md</c> section 1.3).
/// </para>
/// <para>
/// <b>A self-test's outcome</b> is <c>"pass"</c>; or, for a test whose header reports a measurement
/// in OBSERVED on a pass (<see cref="ObservedOnPass"/>, <c>suite.md</c> section 2),
/// <c>{ "outcome": "pass", "observed": "$B1" }</c>; or <c>{ "outcome": "fail", "code", "observed",
/// "expected" }</c>, the three bytes in hexadecimal as the suite reads them; or
/// <c>{ "outcome": "no verdict", "result" }</c> with the byte at <c>$80</c> after the plan's limit
/// (item 1). <b>A screenshot test's outcome</b> is its count of mismatched pixels by the partition
/// rule (item 5), on the eighth frame (item 3) at the row offset fixed on calibration (item 4).
/// </para>
/// <para>
/// The file names the two pinned commits and the manifest's hash, so a new pin cannot be compared
/// with outcomes recorded under the old one.
/// </para>
/// </remarks>
internal static class SuiteOutcomes
{
    /// <summary>The environment variable that makes the recorder write the file.</summary>
    public const string RecordVariable = "ATARI_RECORD";

    /// <summary>The command that records the file, for messages.</summary>
    public const string RecordCommand = "ATARI_RECORD=1 dotnet test tests/Dbhq.Machines.Atari2600.Tests --filter Record";

    /// <summary>The recorded cartridge self-tests (the plan's trust table; <c>suite.md</c> section 5.7).</summary>
    public static readonly string[] SelfTests =
    [
        "cartridge/bank-fa",
        "cartridge/bank-hotspot-window",
        "cartridge/power-on-bank",
        "cartridge/ram-superchip",
    ];

    /// <summary>
    /// The recorded self-tests whose header reports a measurement in OBSERVED on a pass, so a pass
    /// is recorded with it (<c>suite.md</c> section 2: of the recorded tests, only
    /// <c>power-on-bank</c>, which reports the bank the board woke in).
    /// </summary>
    public static readonly string[] ObservedOnPass = ["cartridge/power-on-bank"];

    /// <summary>The two regions, by the names the file uses.</summary>
    public static readonly string[] Regions = ["NTSC", "PAL"];

    private static readonly ConcurrentDictionary<string, Lazy<SuiteOutcome>> SelfTestRuns = new();
    private static readonly ConcurrentDictionary<string, Lazy<int>> ScreenshotRuns = new();

    /// <summary>The file of recorded outcomes.</summary>
    public static string FilePath => Path.Combine(RepoPaths.Root, "machines", "atari-2600", "suite-outcomes.json");

    /// <summary>True when the recorder is to write the file rather than check it.</summary>
    public static bool Recording => Environment.GetEnvironmentVariable(RecordVariable) == "1";

    /// <summary>The outcome of a recorded self-test in <paramref name="region"/>, as the file holds it. Run once and kept.</summary>
    public static JsonNode SelfTestOutcome(string test, string region)
    {
        SuiteOutcome run = SelfTestRuns.GetOrAdd($"{test} {region}", _ => new Lazy<SuiteOutcome>(() => SuiteRunner.Run(test, TestPrograms.RegionNamed(region)))).Value;
        return Format(run);
    }

    /// <summary>A self-test's run as the file holds it (see the remarks).</summary>
    public static JsonNode Format(SuiteOutcome run)
    {
        string test = run.Test;
        if (run.Passed)
        {
            return ObservedOnPass.Contains(test)
                ? new JsonObject { ["outcome"] = "pass", ["observed"] = Hex(run.Observed) }
                : JsonValue.Create("pass");
        }

        return run.Failed
            ? new JsonObject { ["outcome"] = "fail", ["code"] = Hex(run.Code), ["observed"] = Hex(run.Observed), ["expected"] = Hex(run.Expected) }
            : new JsonObject { ["outcome"] = "no verdict", ["result"] = Hex(run.Result) };
    }

    /// <summary>The mismatched pixels of a screenshot test in <paramref name="region"/>. Run once and kept.</summary>
    public static int ScreenshotOutcome(string test, string region) =>
        ScreenshotRuns.GetOrAdd($"{test} {region}", key => new Lazy<int>(() => ScreenshotRunner.Outcome(test, TestPrograms.RegionNamed(region), out _, out _).Mismatches)).Value;

    /// <summary>The whole file as the recorder writes it: every recorded test, both regions, from the machine.</summary>
    public static string Text()
    {
        var selfTests = new JsonObject();
        foreach (string test in SelfTests.Order(StringComparer.Ordinal))
        {
            var regions = new JsonObject();
            foreach (string region in Regions)
            {
                regions[region] = SelfTestOutcome(test, region);
            }

            selfTests[test] = regions;
        }

        var screenshots = new JsonObject();
        foreach (string test in ScreenshotRunner.Tests.Order(StringComparer.Ordinal))
        {
            var regions = new JsonObject();
            foreach (string region in Regions)
            {
                regions[region] = ScreenshotOutcome(test, region);
            }

            screenshots[test] = regions;
        }

        var file = new JsonObject
        {
            ["about"] = $"Written by the recorder ({RecordCommand}), never by hand. Each recorded test of the suite, in each region: a self-test's verdict, or a screenshot test's count of mismatched pixels by the partition rule. A change fails CI; it goes in the journal before the file is recorded again.",
            ["suiteCommit"] = Pins.Atari2600SuiteCommit,
            ["binariesCommit"] = Pins.Atari2600BinariesCommit,
            ["manifestSha256"] = Pins.Atari2600SuiteManifestSha256,
            ["selfTests"] = selfTests,
            ["screenshots"] = screenshots,
        };
        return file.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    /// <summary>
    /// The recorded outcome of <paramref name="test"/> in <paramref name="region"/> under
    /// <paramref name="kind"/> (<c>selfTests</c> or <c>screenshots</c>), or null if the file does
    /// not hold it.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file has not been recorded.</exception>
    public static JsonNode? Recorded(string kind, string test, string region)
    {
        if (!File.Exists(FilePath))
        {
            throw new FileNotFoundException($"{FilePath} does not exist: record it with {RecordCommand}.", FilePath);
        }

        JsonNode? file = JsonNode.Parse(File.ReadAllText(FilePath));
        return file?[kind]?[test]?[region]?.DeepClone();
    }

    private static string Hex(byte value) => string.Create(CultureInfo.InvariantCulture, $"${value:X2}");
}
