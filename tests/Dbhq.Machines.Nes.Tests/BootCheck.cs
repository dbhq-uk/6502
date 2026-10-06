using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>What the bundled homebrew left after <see cref="BootCheck.Frames"/> frames from power on.</summary>
/// <param name="Pixels">The frame buffer, as the machine holds it.</param>
/// <param name="FrameSha256">The SHA-256 of <see cref="BootCheck.FrameBytes"/> of the pixels.</param>
/// <param name="Samples">The samples the sound made in that time, all read out as the machine ran.</param>
/// <param name="LoudestLateSample">The largest magnitude among the samples of the second half of the run.</param>
/// <param name="Dropped">Samples the sound's ring dropped because they were not read in time.</param>
/// <param name="Backdrop">The colour of palette entry 0, the backdrop, as the frame buffer holds colours.</param>
public sealed record BootRun(uint[] Pixels, string FrameSha256, long Samples, float LoudestLateSample, long Dropped, uint Backdrop);

/// <summary>
/// The boot check of the NES's bundled homebrew (the spec's "Proof", layer 4): the title runs a
/// set number of frames from power on in each region, and its picture is compared with the one
/// recorded in <c>machines/nes/expected-frames.json</c>, which the recorder in
/// <see cref="BootTests"/> writes and nobody edits. <see cref="BootTests"/> and
/// <see cref="NesAcceptanceTests"/> both call the checks here, so the numbers are in one place.
/// </summary>
/// <remarks>
/// The title is Lan Master, by Shiru (<c>roms/README.md</c>). From power on it shows black, fades
/// its title screen in, whole by frame 22 on NTSC and frame 19 on PAL, then holds it, with two
/// small things that move: the computer beside the name and the cursor by START (seen frame by
/// frame on 6 October 2026; the journal). Frame 120, two seconds on NTSC and 2.4 on PAL, is well
/// past the fade, with the cursor shown in both regions.
/// </remarks>
public static class BootCheck
{
    /// <summary>Frames from power on to the picture that is compared.</summary>
    public const int Frames = 120;

    /// <summary>
    /// The loudest a late sample of a silent machine may be. The console's filters never quite
    /// reach 0 after the step at power on: measured on 6 October 2026, with no sound played
    /// (nestest) the loudest sample in the second half of 120 frames was 1.7e-30, and in Lan
    /// Master's title music 0.05 (the journal). This is between the two, 34 dB under the music.
    /// <see cref="BootTests"/> checks the silent side and prints both.
    /// </summary>
    public const float Silence = 0.001f;

    /// <summary>The share of the picture, at least, that is not the backdrop: one pixel in a hundred.</summary>
    public const double LeastDrawn = 0.01;

    private static readonly ConcurrentDictionary<string, Lazy<BootRun>> Runs = new();

    /// <summary>The recorded frame hashes and the frame count they are for.</summary>
    public static string ExpectedPath => Path.Combine(RepoPaths.Root, "machines", "nes", "expected-frames.json");

    /// <summary>The bundled homebrew, checked against its pin as it is read.</summary>
    public static byte[] Rom() => RepoPaths.ReadChecked(Pins.NesHomebrewPath, Pins.NesHomebrewSha256);

    /// <summary>The two regions the check runs in, by name.</summary>
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    /// <summary>The region called <paramref name="name"/>.</summary>
    public static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    /// <summary>
    /// Runs the title from power on to the end of frame <see cref="Frames"/>, reading the sound
    /// as it goes. A run is made once for each region and kept: the machine is deterministic.
    /// </summary>
    public static BootRun Run(Region region) => Runs.GetOrAdd(region.Name, _ => new Lazy<BootRun>(() => Measure(Rom(), region))).Value;

    /// <summary>
    /// The frame's bytes as the page's canvas takes them: each pixel's red, green, blue and alpha,
    /// row by row from the top left. The frame buffer's <c>0xAABBGGRR</c> written out low byte first.
    /// </summary>
    public static byte[] FrameBytes(uint[] pixels)
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

    /// <summary>The frame after <see cref="Frames"/> frames hashes to the recorded hash for its region.</summary>
    public static void AssertFrameIsTheRecordedOne(Region region)
    {
        Assert.Equal(ExpectedHash(region), Run(region).FrameSha256);
    }

    /// <summary>
    /// The recorded hash of the frame after <see cref="Frames"/> frames on <paramref name="region"/>,
    /// read from <see cref="ExpectedPath"/> once it is checked to be for this ROM and this count.
    /// </summary>
    public static string ExpectedHash(Region region)
    {
        Assert.True(File.Exists(ExpectedPath), $"{ExpectedPath} does not exist. Record it with NES_RECORD=1 dotnet test --filter Record, look at the pictures it writes, and commit it.");
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(ExpectedPath));
        JsonElement root = file.RootElement;
        Assert.Equal(Pins.NesHomebrewPath, root.GetProperty("rom").GetString());
        Assert.True(root.GetProperty("frames").GetInt32() == Frames, $"the recording is of frame {root.GetProperty("frames").GetInt32()} and the check runs {Frames}: record it again");
        return root.GetProperty("hashes").GetProperty(region.Name).GetString()!;
    }

    /// <summary>The picture has more than one colour, and more than a sliver of it is not the backdrop.</summary>
    public static void AssertPictureIsNotBlank(Region region)
    {
        BootRun run = Run(region);
        int colours = run.Pixels.Distinct().Count();
        Assert.True(colours > 1, $"{region.Name}: the picture is one colour");

        int drawn = run.Pixels.Count(p => p != run.Backdrop);
        double share = (double)drawn / run.Pixels.Length;
        Assert.True(share >= LeastDrawn, $"{region.Name}: only {drawn} of {run.Pixels.Length} pixels are not the backdrop ({run.Backdrop:X8})");
    }

    /// <summary>
    /// The sound ran for the whole run, at its rate, with nothing dropped, and the title played
    /// something: a late sample louder than <see cref="Silence"/>.
    /// </summary>
    public static void AssertSoundPlays(Region region)
    {
        BootRun run = Run(region);

        // The run is the time from power on to the end of the frame, which is the frames at the
        // region's rate. The tolerance is one frame's samples: power on starts a few dots into
        // the first frame, and the resampler holds back its latency. This count is the check that
        // each region ran at its own frame rate (about 95.8 thousand samples on NTSC against 115.2
        // thousand on PAL for the same 120 frames), which the recorded picture cannot be: the two
        // regions' frames at N are the same picture, so their hashes are equal.
        double perFrame = new NesOptions().SampleRate / region.FramesPerSecond;
        long expected = (long)Math.Round(Frames * perFrame);
        long tolerance = (long)Math.Ceiling(perFrame);
        Assert.Equal(0, run.Dropped);
        Assert.InRange(run.Samples, expected - tolerance, expected + tolerance);
        Assert.True(run.LoudestLateSample > Silence, $"{region.Name}: the loudest sample of the second half was {run.LoudestLateSample}, which is silence");
    }

    /// <summary>
    /// Runs <paramref name="rom"/> from power on to the end of frame <see cref="Frames"/> on
    /// <paramref name="region"/>, reading the sound as it goes, and returns what it left.
    /// </summary>
    public static BootRun Measure(byte[] rom, Region region)
    {
        var nes = new Nes(Cartridge.Load(rom), region);
        nes.PowerOn();
        FrameBuffer screen = nes.Bus.Ppu.Screen;
        float[] buffer = new float[nes.Sound.Capacity];
        long samples = 0;
        float loudest = 0;

        void Drain(bool late)
        {
            int read = nes.Sound.Read(buffer);
            samples += read;
            if (late)
            {
                for (int i = 0; i < read; i++)
                {
                    loudest = Math.Max(loudest, Math.Abs(buffer[i]));
                }
            }
        }

        while (screen.Frame < Frames)
        {
            nes.Step();
            if (nes.Sound.Available >= buffer.Length / 2)
            {
                Drain(screen.Frame >= Frames / 2);
            }
        }

        Drain(true);
        uint[] pixels = (uint[])screen.Pixels.Clone();
        uint backdrop = PpuPalette.Colour(nes.Bus.Ppu.PeekVram(0x3F00) & 0x3F, 0, region.EmphasisSwapsRedAndGreen, false);
        string hash = Convert.ToHexStringLower(SHA256.HashData(FrameBytes(pixels)));
        return new BootRun(pixels, hash, samples, loudest, nes.Sound.Dropped, backdrop);
    }
}
