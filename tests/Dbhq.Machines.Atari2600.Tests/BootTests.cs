using System.Text.Json;
using Dbhq.Cpu6502.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The boot check of the bundled game, 2048 2600, in both regions (<see cref="BootCheck"/> holds
/// the checks, which <see cref="Atari2600AcceptanceTests"/> calls too); the recorder that writes
/// the hashes the check compares with; and the checks that the game's folder holds what
/// <c>AGENTS.md</c> rule 4 asks of a bundled cartridge.
/// </summary>
public sealed class BootTests(ITestOutputHelper output)
{
    private static string Folder => Path.Combine(RepoPaths.Root, "machines", "atari-2600", "cartridges", "2048");

    [Fact]
    public void The_game_is_the_pinned_file_and_a_plain_2K_cartridge()
    {
        Cartridge cartridge = Cartridge.Load(BootCheck.Rom());

        Assert.Equal("2K", cartridge.BoardName);
        Assert.Equal("machines/atari-2600/cartridges/2048/2048.bin", Pins.Atari2600HomebrewPath);
    }

    [Fact]
    public void The_folder_holds_the_game_its_licence_its_readme_and_its_manifest()
    {
        foreach (string file in new[] { "2048.bin", "LICENSE", "README.md", "manifest.json" })
        {
            Assert.True(File.Exists(Path.Combine(Folder, file)), $"{file} is missing from {Folder}");
        }

        string licence = File.ReadAllText(Path.Combine(Folder, "LICENSE"));
        Assert.StartsWith("The MIT License (MIT)", licence, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) 2014 Carlos Duarte do Nascimento (Chester)", licence, StringComparison.Ordinal);

        string readme = File.ReadAllText(Path.Combine(Folder, "README.md"));
        Assert.Contains(Pins.Atari2600HomebrewSha256, readme, StringComparison.Ordinal);
        Assert.Contains(Commit, readme, StringComparison.Ordinal);
        Assert.Contains("the cartridge art is not in the ROM", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_folders_manifest_and_the_list_name_the_licence_and_the_hash()
    {
        byte[] rom = BootCheck.Rom();
        using JsonDocument own = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, "manifest.json")));
        JsonElement game = own.RootElement;
        Assert.Equal("2048", game.GetProperty("slug").GetString());
        Assert.Equal("2048.bin", game.GetProperty("file").GetString());
        Assert.Equal("MIT", game.GetProperty("licence").GetString());
        Assert.Equal(Pins.Atari2600HomebrewSha256, game.GetProperty("sha256").GetString());
        Assert.Equal(rom.Length, game.GetProperty("bytes").GetInt32());
        Assert.Equal(Commit, game.GetProperty("commit").GetString());
        Assert.Equal(Cartridge.Load(rom).BoardName, game.GetProperty("board").GetString());

        using JsonDocument list = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, "..", "manifest.json")));
        JsonElement listed = Assert.Single(list.RootElement.GetProperty("cartridges").EnumerateArray());
        Assert.Equal("2048", listed.GetProperty("slug").GetString());
        Assert.Equal("MIT", listed.GetProperty("licence").GetString());
        Assert.Equal(Pins.Atari2600HomebrewSha256, listed.GetProperty("sha256").GetString());
        Assert.Equal(game.GetProperty("title").GetString(), listed.GetProperty("title").GetString());
        Assert.Equal(game.GetProperty("author").GetString(), listed.GetProperty("author").GetString());
    }

    [Fact]
    public void The_notice_carries_both_copyright_lines_and_roms_readme_points_to_the_folder()
    {
        string notice = File.ReadAllText(Path.Combine(RepoPaths.Root, "NOTICE.md"));
        Assert.Contains("Copyright (c) 2014 Carlos Duarte do Nascimento (Chester)", notice, StringComparison.Ordinal);
        Assert.Contains("Original 2048 game Copyright (c) 2014 Gabriele Cirulli", notice, StringComparison.Ordinal);
        Assert.Contains(Commit, notice, StringComparison.Ordinal);

        string roms = File.ReadAllText(Path.Combine(RepoPaths.Root, "roms", "README.md"));
        Assert.Contains("machines/atari-2600/cartridges/2048/", roms, StringComparison.Ordinal);
    }

    [Fact]
    public void The_pages_try_it_names_the_same_game()
    {
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "machines", "atari-2600", "try-it.json")));

        Assert.Equal(Pins.Atari2600HomebrewPath, file.RootElement.GetProperty("rom").GetString());
        Assert.Equal("MIT", file.RootElement.GetProperty("licence").GetString());
        Assert.NotEmpty(file.RootElement.GetProperty("steps").EnumerateArray());
        Assert.NotEmpty(file.RootElement.GetProperty("controls").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void The_frame_is_the_one_on_file(string region)
    {
        BootCheck.AssertFrameIsTheRecordedOne(BootCheck.RegionNamed(region));
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void The_picture_is_not_blank(string region)
    {
        BootRun run = BootCheck.Run(BootCheck.RegionNamed(region));
        output.WriteLine($"{region}: {run.Lines} lines, {run.Colours.Distinct().Count()} colour numbers");
        BootCheck.AssertPictureIsNotBlank(BootCheck.RegionNamed(region));
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void The_sound_keeps_up_with_the_frames(string region)
    {
        BootRun run = BootCheck.Run(BootCheck.RegionNamed(region));
        output.WriteLine($"{region}: {run.Samples} samples, {run.ExpectedSamples:F1} from the run's time, {run.SamplesPerFrame:F1} a frame, {run.Dropped} dropped");
        BootCheck.AssertSoundKeptUp(BootCheck.RegionNamed(region));
    }

    /// <summary>
    /// The recorder. With <c>ATARI_RECORD=1</c> it writes <c>machines/atari-2600/expected-frames.json</c>
    /// from the machine's own frames, and a PNG of each region's window to
    /// <c>TestResults/atari-2600/</c> for a person to look at before the file is committed: if the
    /// picture is wrong, the machine is wrong. Run: <c>ATARI_RECORD=1 dotnet test
    /// tests/Dbhq.Machines.Atari2600.Tests --filter Record</c>.
    /// </summary>
    /// <remarks>
    /// Without the variable it checks that the committed file is, byte for byte, what it would
    /// write, so the file was made by the recorder and not edited by hand (<c>AGENTS.md</c> rule 5).
    /// It is not skipped instead: the site's results refuse any skipped test.
    /// </remarks>
    [Fact]
    public void Record_the_boot_frames()
    {
        string text = BootCheck.Text();
        if (!SuiteOutcomes.Recording)
        {
            Assert.True(File.Exists(BootCheck.ExpectedPath), $"{BootCheck.ExpectedPath} does not exist: record it with {SuiteOutcomes.RecordCommand}");
            Assert.Equal(text, File.ReadAllText(BootCheck.ExpectedPath));
            return;
        }

        string pictures = Path.Combine(RepoPaths.Root, "TestResults", "atari-2600");
        Directory.CreateDirectory(pictures);
        foreach (Region region in new[] { Region.Ntsc, Region.Pal })
        {
            BootRun run = BootCheck.Run(region);
            string png = Path.Combine(pictures, $"boot-{region.Name}-frame-{BootCheck.Frames(region)}.png");
            PngReader.Write(png, new Picture(FrameBuffer.Width, region.ShownLines, run.Window));
            string large = Path.ChangeExtension(png, null) + "-x4.png";
            PngReader.Write(large, Enlarged(new Picture(FrameBuffer.Width, region.ShownLines, run.Window), 4));
            output.WriteLine($"{region.Name}: {run.Lines} lines, colours {run.ColoursSha256}, rgba {run.RgbaSha256}, the window in {png} and four times the size in {large}");
        }

        File.WriteAllText(BootCheck.ExpectedPath, text);
        output.WriteLine($"wrote {BootCheck.ExpectedPath}");
    }

    // Each pixel drawn as a square of scale by scale, for a person to look at.
    private static Picture Enlarged(Picture picture, int scale)
    {
        int width = picture.Width * scale;
        var pixels = new uint[width * picture.Height * scale];
        for (int y = 0; y < picture.Height * scale; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = picture.Pixels[((y / scale) * picture.Width) + (x / scale)];
            }
        }

        return new Picture(width, picture.Height * scale, pixels);
    }

    private const string Commit ="35de3c1b3b7bc4c3aadc8cf7fe630050a6984761";
}
