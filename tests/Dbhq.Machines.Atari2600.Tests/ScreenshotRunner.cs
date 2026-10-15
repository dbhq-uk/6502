using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>What one screenshot test gave: the rows compared and the mismatched pixels among them.</summary>
internal readonly record struct ScreenshotOutcome(string Test, string Build, int Rows, int Mismatches, int Offset)
{
    public override string ToString() =>
        $"{Test} ({Build}): {Mismatches} mismatched pixels in {Rows} rows of {FrameBuffer.Width}, reference row r against our row r + {Offset}";
}

/// <summary>
/// Runs one of the suite's screenshot tests headless and compares its frame with the test's
/// reference picture by the partition rule (<see cref="Partition"/>). The <c>_ntsc</c> build runs
/// on NTSC against <c>_ntsc.png</c>, and the <c>_pal</c> build on PAL against <c>_pal.png</c>; the
/// real-console captures are not compared in the first version (issue #162).
/// </summary>
/// <remarks>
/// Fixed before running (the plan's items 3 and 4): the frame is the eighth to end after power on,
/// a frame ending at the end of VSYNC or at 400 lines without one; and our frame's row 0 is the
/// first line after VSYNC ends. The rows are aligned once, on <c>harness/calibration</c> in both
/// regions, and <see cref="RowOffset"/> is that alignment, used for every test and never chosen
/// per test. The comparison covers the rows both pictures have, 160 pixels wide.
/// </remarks>
internal static class ScreenshotRunner
{
    /// <summary>The frame compared, the plan's item 3.</summary>
    public const int Frame = 8;

    /// <summary>
    /// Reference row r is compared with our row r + <see cref="RowOffset"/>: the alignment
    /// <c>harness/calibration</c> matched best in both regions on 14 October 2026
    /// (<c>ScreenshotTests.Calibration_matches_best_at_the_recorded_offset</c>).
    /// </summary>
    public const int RowOffset = 0;

    /// <summary>The offsets the calibration tries, either side of none.</summary>
    public const int OffsetsTried = 8;

    /// <summary>The calibration test, a picture with nothing in doubt (suite.md section 5.3).</summary>
    public const string Calibration = "harness/calibration";

    /// <summary>Every screenshot test in the suite: each source with a <c>_ntsc.png</c> beside it (suite.md section 1.2).</summary>
    public static IReadOnlyList<string> Tests =>
        [.. Atari2600Suite.Names.Where(name => name.EndsWith("_ntsc.png", StringComparison.Ordinal)).Select(name => name[..^"_ntsc.png".Length])];

    /// <summary>The test's reference picture for <paramref name="region"/>, from the pinned fork, hash-checked.</summary>
    public static Picture Reference(string test, Region region) =>
        PngReader.Read(Atari2600Suite.Read($"{test}_{SuiteRunner.Build(region)}.png"));

    /// <summary>The colour numbers of the test's eighth frame in <paramref name="region"/>, a row each line.</summary>
    /// <exception cref="InvalidOperationException">The frame has no lines.</exception>
    public static byte[][] Run(string test, Region region)
    {
        FrameBuffer frame = RunMachine(test, region).Bus.Tia.Frame;
        if (frame.Lines == 0)
        {
            throw new InvalidOperationException($"{test} ({SuiteRunner.Build(region)}): frame {Frame} has no lines.");
        }

        var rows = new byte[frame.Lines][];
        for (int row = 0; row < rows.Length; row++)
        {
            rows[row] = frame.ColourNumbers.Slice(row * FrameBuffer.Width, FrameBuffer.Width).ToArray();
        }

        return rows;
    }

    /// <summary>The machine with the test in <paramref name="region"/>, powered on and run to the end of frame <see cref="Frame"/>.</summary>
    public static Atari2600 RunMachine(string test, Region region)
    {
        var machine = new Atari2600(Cartridge.Load(SuiteRunner.Rom(test, region)), region);
        machine.PowerOn();
        machine.RunFrames(Frame);
        return machine;
    }

    /// <summary>
    /// Compares <paramref name="ours"/> with <paramref name="reference"/>, reference row r with our
    /// row r + <paramref name="offset"/>, over the rows both have.
    /// </summary>
    /// <exception cref="InvalidDataException">The reference is not 160 pixels wide, or no row is shared.</exception>
    public static (int Rows, int Mismatches) Compare(Picture reference, byte[][] ours, int offset)
    {
        if (reference.Width != FrameBuffer.Width)
        {
            throw new InvalidDataException($"The reference is {reference.Width} pixels wide, not {FrameBuffer.Width}.");
        }

        int first = Math.Max(0, -offset);
        int last = Math.Min(reference.Height, ours.Length - offset);
        if (last <= first)
        {
            throw new InvalidDataException($"No row is shared at offset {offset}: the reference has {reference.Height} rows and our frame {ours.Length}.");
        }

        int rows = last - first;
        var referencePixels = new uint[rows * FrameBuffer.Width];
        var ourPixels = new byte[rows * FrameBuffer.Width];
        for (int r = 0; r < rows; r++)
        {
            reference.Pixels.AsSpan((first + r) * FrameBuffer.Width, FrameBuffer.Width).CopyTo(referencePixels.AsSpan(r * FrameBuffer.Width));
            ours[first + r + offset].CopyTo(ourPixels, r * FrameBuffer.Width);
        }

        return (rows, Partition.Mismatches(referencePixels, ourPixels));
    }

    /// <summary>Runs the test in <paramref name="region"/> and compares its frame at <see cref="RowOffset"/>.</summary>
    public static ScreenshotOutcome Outcome(string test, Region region, out Picture reference, out byte[][] ours)
    {
        reference = Reference(test, region);
        ours = Run(test, region);
        (int rows, int mismatches) = Compare(reference, ours, RowOffset);
        return new ScreenshotOutcome(test, SuiteRunner.Build(region), rows, mismatches, RowOffset);
    }

    /// <summary>
    /// Our frame and the reference side by side, ours on the left in <paramref name="region"/>'s
    /// palette, for a person to look at; rows the other lacks are black.
    /// </summary>
    public static Picture SideBySide(Picture reference, byte[][] ours, Region region, int offset)
    {
        const int Gap = 8;
        int height = Math.Max(reference.Height, ours.Length - offset);
        int width = (2 * FrameBuffer.Width) + Gap;
        var pixels = new uint[width * height];
        Array.Fill(pixels, 0xFF000000);
        for (int r = 0; r < height; r++)
        {
            int ourRow = r + offset;
            for (int x = 0; x < FrameBuffer.Width; x++)
            {
                if (ourRow >= 0 && ourRow < ours.Length)
                {
                    pixels[(r * width) + x] = region.Palette.Rgba(ours[ourRow][x]);
                }

                if (r < reference.Height)
                {
                    pixels[(r * width) + FrameBuffer.Width + Gap + x] = reference.Pixels[(r * FrameBuffer.Width) + x];
                }
            }
        }

        return new Picture(width, height, pixels);
    }
}
