using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>What the bundled game left after its boot check's frames from power on, in one region.</summary>
/// <param name="Lines">The lines the last frame had.</param>
/// <param name="Colours">The last frame's TIA colour numbers, <see cref="FrameBuffer.Width"/> a row, <paramref name="Lines"/> rows.</param>
/// <param name="Window">The region's window of that frame as the page's canvas shows it, <c>0xAABBGGRR</c> a pixel.</param>
/// <param name="ColoursSha256">The SHA-256 of <paramref name="Colours"/>.</param>
/// <param name="RgbaSha256">The SHA-256 of <paramref name="Window"/> as the canvas's bytes (<see cref="BootCheck.RgbaBytes"/>).</param>
/// <param name="Samples">The samples the sound made in that time, all read out as the machine ran.</param>
/// <param name="ExpectedSamples">The samples the run's time gives at the sample rate: its CPU cycles over the region's CPU clock.</param>
/// <param name="SamplesPerFrame">One frame's samples: <paramref name="ExpectedSamples"/> over the frames run.</param>
/// <param name="Dropped">Samples the sound's ring dropped because they were not read in time.</param>
public sealed record BootRun(int Lines, byte[] Colours, uint[] Window, string ColoursSha256, string RgbaSha256, long Samples, double ExpectedSamples, double SamplesPerFrame, long Dropped);

/// <summary>
/// The boot check of the Atari 2600's bundled game (the spec's "Proof", layer 4): 2048 2600 runs a
/// set number of frames from power on in each region with set switches, and its picture is
/// compared with the one recorded in <c>machines/atari-2600/expected-frames.json</c>, which the
/// recorder in <see cref="BootTests"/> writes and nobody edits. <see cref="BootTests"/> and
/// <see cref="Atari2600AcceptanceTests"/> both call the checks here, so the numbers are in one place.
/// </summary>
/// <remarks>
/// <para>
/// Fixed before running (the plan's "Fixed before running", item 7): NTSC with the colour switch at
/// colour and PAL with it at black and white, because the game makes PAL frames only then (its
/// README); 120 frames on NTSC and 100 on PAL, a frame ending at the end of VSYNC or at 400 lines
/// without one (item 2). If the picture at that frame were blank, that would be a finding: the
/// counts are not moved to find a picture.
/// </para>
/// <para>
/// Two hashes are kept for each region. <c>colours</c> hashes the whole last frame as TIA colour
/// numbers, so it does not depend on the palette. <c>rgba</c> hashes the region's window of it
/// (<see cref="FrameBuffer.ToRgba"/>), <see cref="FrameBuffer.Width"/> by
/// <see cref="Region.ShownLines"/>, as the page's canvas holds it: each pixel's red, green, blue
/// and alpha bytes, row by row from the top left. The browser check compares the canvas with it
/// (the plan's item 14).
/// </para>
/// </remarks>
public static class BootCheck
{
    /// <summary>The two regions the check runs in, by name.</summary>
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    /// <summary>The region called <paramref name="name"/>.</summary>
    public static Region RegionNamed(string name) => TestPrograms.RegionNamed(name);

    /// <summary>The frames from power on to the picture compared on <paramref name="region"/>: the plan's item 7.</summary>
    public static int Frames(Region region) => region == Region.Pal ? 100 : 120;

    /// <summary>The console switches the check holds from power on in <paramref name="region"/>: the plan's item 7.</summary>
    public static ConsoleSwitches Switches(Region region) => region == Region.Pal ? ConsoleSwitches.BlackAndWhite : ConsoleSwitches.None;

    /// <summary>The colour switch's position for <paramref name="region"/>, as the file names it.</summary>
    public static string SwitchName(Region region) => (Switches(region) & ConsoleSwitches.BlackAndWhite) != 0 ? "black and white" : "colour";

    private static readonly ConcurrentDictionary<string, Lazy<BootRun>> Runs = new();

    // The recorded file as it stood when the class was first used, as SuiteOutcomes keeps its own:
    // the recorder reads Runs in Text before it writes, so a check run beside it compares with the
    // committed hashes and never with a file half written.
    private static readonly string? Committed = File.Exists(ExpectedPath) ? File.ReadAllText(ExpectedPath) : null;

    /// <summary>The recorded frame hashes, the frame counts and the switches they are for.</summary>
    public static string ExpectedPath => Path.Combine(RepoPaths.Root, "machines", "atari-2600", "expected-frames.json");

    /// <summary>The bundled game, checked against its pin as it is read.</summary>
    public static byte[] Rom() => RepoPaths.ReadChecked(Pins.Atari2600HomebrewPath, Pins.Atari2600HomebrewSha256);

    /// <summary>
    /// Runs the game from power on to the end of frame <see cref="Frames"/> on <paramref name="region"/>,
    /// reading the sound as it goes. A run is made once for each region and kept: the machine is
    /// deterministic.
    /// </summary>
    public static BootRun Run(Region region) => Runs.GetOrAdd(region.Name, _ => new Lazy<BootRun>(() => Measure(Rom(), region))).Value;

    /// <summary>The window's pixels as the canvas's bytes: <c>0xAABBGGRR</c> written out low byte first.</summary>
    public static byte[] RgbaBytes(uint[] pixels)
    {
        byte[] bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            uint p = pixels[i];
            bytes[(i * 4) + 0] = (byte)p;
            bytes[(i * 4) + 1] = (byte)(p >> 8);
            bytes[(i * 4) + 2] = (byte)(p >> 16);
            bytes[(i * 4) + 3] = (byte)(p >> 24);
        }

        return bytes;
    }

    /// <summary>
    /// Both hashes of the frame on <paramref name="region"/> are the recorded ones, read from
    /// <see cref="ExpectedPath"/> once it is checked to be for this ROM, this frame count and
    /// these switches.
    /// </summary>
    public static void AssertFrameIsTheRecordedOne(Region region)
    {
        Assert.True(Committed is not null, $"{ExpectedPath} does not exist. Record it with {SuiteOutcomes.RecordCommand}, look at the pictures it writes, and commit it.");
        using JsonDocument file = JsonDocument.Parse(Committed);
        JsonElement root = file.RootElement;
        Assert.Equal(Pins.Atari2600HomebrewPath, root.GetProperty("rom").GetString());
        int frames = root.GetProperty("frames").GetProperty(region.Name).GetInt32();
        Assert.True(frames == Frames(region), $"{region.Name}: the recording is of frame {frames} and the check runs {Frames(region)}: record it again");
        Assert.Equal(SwitchName(region), root.GetProperty("switches").GetProperty(region.Name).GetString());

        BootRun run = Run(region);
        Assert.Equal(root.GetProperty("colours").GetProperty(region.Name).GetString(), run.ColoursSha256);
        Assert.Equal(root.GetProperty("rgba").GetProperty(region.Name).GetString(), run.RgbaSha256);
    }

    /// <summary>The frame is not blank: it holds more than one colour number (the plan's task 12, step 4).</summary>
    public static void AssertPictureIsNotBlank(Region region)
    {
        BootRun run = Run(region);
        Assert.True(run.Colours.Distinct().Count() > 1, $"{region.Name}: the frame of {run.Lines} lines is one colour number");
    }

    /// <summary>
    /// The sound kept up with the picture: nothing was dropped, and the samples read are at least
    /// what the run's time gives at the sample rate, less one frame's worth (the plan's task 12,
    /// step 4). The run's time is its CPU cycles over the region's CPU clock, so this also checks
    /// that each region ran at its own clock.
    /// </summary>
    public static void AssertSoundKeptUp(Region region)
    {
        BootRun run = Run(region);
        Assert.Equal(0, run.Dropped);
        Assert.True(run.Samples >= run.ExpectedSamples - run.SamplesPerFrame, $"{region.Name}: {run.Samples} samples where the run's time gives {run.ExpectedSamples:F1}, and one frame is {run.SamplesPerFrame:F1}");
    }

    /// <summary>The whole of <see cref="ExpectedPath"/> as the recorder writes it, from the machine's own frames.</summary>
    public static string Text()
    {
        var frames = new JsonObject();
        var switches = new JsonObject();
        var colours = new JsonObject();
        var rgba = new JsonObject();
        foreach (Region region in new[] { Region.Ntsc, Region.Pal })
        {
            BootRun run = Run(region);
            frames[region.Name] = Frames(region);
            switches[region.Name] = SwitchName(region);
            colours[region.Name] = run.ColoursSha256;
            rgba[region.Name] = run.RgbaSha256;
        }

        var file = new JsonObject
        {
            ["about"] = $"Written by the recorder ({SuiteOutcomes.RecordCommand}), never by hand. The picture of the bundled game after the given frames from power on with the colour switch as given, in each region: colours hashes the whole frame as TIA colour numbers, and rgba the window of the region as the canvas of the page holds it, which the browser check compares.",
            ["rom"] = Pins.Atari2600HomebrewPath,
            ["frames"] = frames,
            ["switches"] = switches,
            ["colours"] = colours,
            ["rgba"] = rgba,
        };
        return file.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    /// <summary>
    /// Runs <paramref name="rom"/> from power on to the end of frame <see cref="Frames"/> on
    /// <paramref name="region"/> with <see cref="Switches"/> held, reading the sound after each
    /// frame, and returns what it left.
    /// </summary>
    public static BootRun Measure(byte[] rom, Region region)
    {
        var machine = new Atari2600(Cartridge.Load(rom), region);
        machine.SetSwitches(Switches(region));
        machine.PowerOn();
        float[] buffer = new float[machine.Sound.Capacity];
        long samples = 0;
        int count = Frames(region);
        for (int frame = 0; frame < count; frame++)
        {
            machine.RunFrames(1);
            samples += machine.Sound.Read(buffer);
        }

        FrameBuffer screen = machine.Bus.Tia.Frame;
        byte[] colours = screen.ColourNumbers.ToArray();
        uint[] window = new uint[FrameBuffer.Width * region.ShownLines];
        screen.ToRgba(region, window);
        double expected = machine.Bus.Cycles / region.CpuHz * machine.Sound.SampleRate;
        return new BootRun(
            screen.Lines,
            colours,
            window,
            Convert.ToHexStringLower(SHA256.HashData(colours)),
            Convert.ToHexStringLower(SHA256.HashData(RgbaBytes(window))),
            samples,
            expected,
            expected / count,
            machine.Sound.Dropped);
    }
}
