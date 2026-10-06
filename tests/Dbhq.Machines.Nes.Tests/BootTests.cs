using System.Text.Json;
using System.Text.Json.Nodes;
using Dbhq.Cpu6502.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The boot check of the bundled homebrew, Lan Master, in both regions (<see cref="BootCheck"/>
/// holds the checks, which <see cref="NesAcceptanceTests"/> calls too), and the recorder that
/// writes the hashes the check compares with.
/// </summary>
public sealed class BootTests(ITestOutputHelper output)
{
    [Fact]
    public void TheHomebrewIsTheOnePinnedAndIsACartridgeThisMachineRuns()
    {
        Cartridge cartridge = Cartridge.Load(BootCheck.Rom());

        Assert.Equal(0, cartridge.Mapper);
        _ = new Nes(cartridge);
    }

    [Fact]
    public void ThePagesTryItNamesTheSameRom()
    {
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "machines", "nes", "try-it.json")));

        Assert.Equal(Pins.NesHomebrewPath, file.RootElement.GetProperty("rom").GetString());
        Assert.NotEmpty(file.RootElement.GetProperty("steps").EnumerateArray());
        Assert.NotEmpty(file.RootElement.GetProperty("controls").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void TheFrameIsTheOneOnFile(string region)
    {
        BootCheck.AssertFrameIsTheRecordedOne(BootCheck.RegionNamed(region));
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void ThePictureIsNotBlank(string region)
    {
        BootRun run = BootCheck.Run(BootCheck.RegionNamed(region));
        output.WriteLine($"{region}: {run.Pixels.Distinct().Count()} colours, {run.Pixels.Count(p => p != run.Backdrop)} of {run.Pixels.Length} pixels not the backdrop ({run.Backdrop:X8})");
        BootCheck.AssertPictureIsNotBlank(BootCheck.RegionNamed(region));
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void TheTitleMakesSoundAtTheSampleRate(string region)
    {
        BootRun run = BootCheck.Run(BootCheck.RegionNamed(region));
        output.WriteLine($"{region}: {run.Samples} samples, the loudest late one {run.LoudestLateSample}, {run.Dropped} dropped");
        BootCheck.AssertSoundPlays(BootCheck.RegionNamed(region));
    }

    [Fact]
    public void ASilentRomStaysUnderTheSilenceLine()
    {
        // nestest from its reset vector draws its menu and waits for a button, playing nothing.
        // Its loudest late sample is the filters' tail of the step at power on, which the line
        // must be above, as the title's music must be above the line.
        BootRun silent = BootCheck.Measure(NesTestRoms.Read("other/nestest.nes"), Region.Ntsc);
        BootRun title = BootCheck.Run(Region.Ntsc);
        output.WriteLine($"the loudest late sample: nestest {silent.LoudestLateSample}, Lan Master {title.LoudestLateSample}, the line {BootCheck.Silence}");

        Assert.True(silent.LoudestLateSample < BootCheck.Silence, $"nestest's loudest late sample is {silent.LoudestLateSample}");
    }

    /// <summary>
    /// The recorder. With <c>NES_RECORD=1</c> it writes <c>machines/nes/expected-frames.json</c>
    /// from the machine's own frames, and a PNG of each region's frame to <c>TestResults/nes/</c>
    /// for a person to look at before the file is committed: if the picture is wrong, the machine
    /// is wrong. Without it, it does nothing. Run: <c>NES_RECORD=1 dotnet test --filter Record</c>.
    /// </summary>
    [Fact]
    public void RecordTheBootFrames()
    {
        if (Environment.GetEnvironmentVariable("NES_RECORD") != "1")
        {
            output.WriteLine("NES_RECORD is not 1, so nothing was recorded.");
            return;
        }

        string pictures = Path.Combine(RepoPaths.Root, "TestResults", "nes");
        Directory.CreateDirectory(pictures);
        var hashes = new JsonObject();
        foreach (Region region in new[] { Region.Ntsc, Region.Pal })
        {
            BootRun run = BootCheck.Run(region);
            hashes[region.Name] = run.FrameSha256;
            string png = Path.Combine(pictures, $"boot-{region.Name}-frame-{BootCheck.Frames}.png");
            Png.Write(png, run.Pixels, FrameBuffer.Width, FrameBuffer.Height);
            output.WriteLine($"{region.Name}: {run.FrameSha256}, the picture in {png}");
        }

        var file = new JsonObject
        {
            ["rom"] = Pins.NesHomebrewPath,
            ["frames"] = BootCheck.Frames,
            ["hashes"] = hashes,
        };
        File.WriteAllText(BootCheck.ExpectedPath, file.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        output.WriteLine($"wrote {BootCheck.ExpectedPath}");
    }
}
