using Dbhq.Cpu6502.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The suite's screenshot tests (the plan's task 7): first the alignment, fixed once on
/// <c>harness/calibration</c> in both regions (item 4), then every screenshot test in both regions,
/// frame eight (item 3), giving its count of mismatched pixels by the partition rule (item 5). No
/// count is asserted here: <see cref="SuiteRecordedTests"/> compares each with the count recorded
/// in <c>machines/atari-2600/suite-outcomes.json</c>. A failure of the runner itself (no frame, a
/// reference of the wrong width, an unreadable PNG) fails the test. For a count that is not zero,
/// our frame and the reference are written side by side to <c>TestResults/atari-2600/</c>, to be
/// looked at.
/// </summary>
public class ScreenshotTests(ITestOutputHelper output)
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    public static TheoryData<string, string> Rows()
    {
        var data = new TheoryData<string, string>();
        foreach (string test in ScreenshotRunner.Tests)
        {
            data.Add(test, "NTSC");
            data.Add(test, "PAL");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Calibration_matches_best_at_the_recorded_offset(string region)
    {
        Region r = TestPrograms.RegionNamed(region);
        Picture reference = ScreenshotRunner.Reference(ScreenshotRunner.Calibration, r);
        byte[][] ours = ScreenshotRunner.Run(ScreenshotRunner.Calibration, r);
        int best = int.MinValue, fewest = int.MaxValue;
        for (int offset = -ScreenshotRunner.OffsetsTried; offset <= ScreenshotRunner.OffsetsTried; offset++)
        {
            (int rows, int mismatches) = ScreenshotRunner.Compare(reference, ours, offset);
            output.WriteLine($"offset {offset,3}: {mismatches} mismatched pixels in {rows} rows");
            if (mismatches < fewest)
            {
                (best, fewest) = (offset, mismatches);
            }
        }

        output.WriteLine($"best: offset {best}, {fewest} mismatched pixels; our frame has {ours.Length} rows, the reference {reference.Height}");
        Assert.Equal(ScreenshotRunner.RowOffset, best);
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void Screenshot_test_runs_and_reports_its_mismatch_count(string test, string region)
    {
        Region r = TestPrograms.RegionNamed(region);
        ScreenshotOutcome outcome = ScreenshotRunner.Outcome(test, r, out Picture reference, out byte[][] ours);
        output.WriteLine(outcome.ToString());
        if (outcome.Mismatches != 0)
        {
            string folder = Path.Combine(RepoPaths.Root, "TestResults", "atari-2600");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"{test.Replace('/', '-')}_{outcome.Build}.png");
            PngReader.Write(path, ScreenshotRunner.SideBySide(reference, ours, r, ScreenshotRunner.RowOffset));
            output.WriteLine($"ours (left) and the reference (right): {path}");
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void The_runner_compares_the_eighth_frame_after_power_on(string region)
    {
        // The plan's item 3, fixed before running: the harness runs eight frames and keeps the last.
        Atari2600 machine = ScreenshotRunner.RunMachine(ScreenshotRunner.Calibration, TestPrograms.RegionNamed(region));
        Assert.Equal(8, machine.Bus.Tia.Frame.Frames);
    }

    [Fact]
    public void Every_screenshot_test_has_a_reference_in_each_region_compared()
    {
        // suite.md section 9: every source with a picture; the manifest must give each its two.
        Assert.NotEmpty(ScreenshotRunner.Tests);
        Assert.Contains(ScreenshotRunner.Calibration, ScreenshotRunner.Tests);
        foreach (string test in ScreenshotRunner.Tests)
        {
            Assert.Contains($"{test}_pal.png", Atari2600Suite.Names);
            Assert.Contains($"{test}_ntsc.a26", Atari2600Suite.Names);
            Assert.Contains($"{test}_pal.a26", Atari2600Suite.Names);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void The_png_reader_reads_back_what_it_writes_with_each_filter(byte filter)
    {
        var picture = new Picture(3, 3, [
            0xFF0000FF, 0xFF00FF00, 0xFFFF0000,
            0x80FFFFFF, 0xFF000000, 0xFF123456,
            0xFF8090A0, 0xFFF0E0D0, 0x01020304]);
        string path = Path.Combine(Path.GetTempPath(), $"atari-2600-png-{Guid.NewGuid():N}.png");
        try
        {
            PngReader.Write(path, picture, filter);
            Picture read = PngReader.Read(File.ReadAllBytes(path));
            Assert.Equal((3, 3), (read.Width, read.Height));
            Assert.Equal(picture.Pixels, read.Pixels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_png_reader_refuses_what_it_cannot_read_with_a_plain_message()
    {
        Assert.Contains("signature", Assert.Throws<InvalidDataException>(() => PngReader.Read([1, 2, 3])).Message);
        byte[] png = Atari2600Suite.Read($"{ScreenshotRunner.Calibration}_ntsc.png");
        png[png.Length / 2] ^= 0xFF;
        Assert.Throws<InvalidDataException>(() => PngReader.Read(png));
    }

    /// <summary>
    /// Each filter undone on one row of single bytes, worked by hand from the PNG specification,
    /// section 9.2: the filtered bytes, the row above, and the row they give.
    /// </summary>
    public static TheoryData<byte, byte[], byte[], byte[]> Filters() => new()
    {
        // None: as it is.
        { 0, [10, 20, 30], [1, 2, 3], [10, 20, 30] },
        // Sub: each byte plus the one to its left.
        { 1, [10, 20, 30], [1, 2, 3], [10, 30, 60] },
        // Up: each byte plus the one above.
        { 2, [10, 20, 30], [1, 2, 3], [11, 22, 33] },
        // Average: plus the floor of (left + up) / 2: 10 + 0 = 10, 20 + (10 + 2) / 2 = 26,
        // 30 + (26 + 3) / 2 = 44, rounding down.
        { 3, [10, 20, 30], [1, 2, 3], [10, 26, 44] },
        // Paeth: the first byte has a = c = 0, b = 100, so p = 100 and b is nearest: 5 + 100 = 105.
        // The second: a = 105, b = 50, c = 100, p = 55, distances 50, 5, 45: b, 5 + 50 = 55. The
        // third: a = 55, b = 60, c = 50, p = 65, distances 10, 5, 15: b, 5 + 60 = 65.
        { 4, [5, 5, 5], [100, 50, 60], [105, 55, 65] },
    };

    [Theory]
    [MemberData(nameof(Filters))]
    public void The_png_reader_undoes_each_filter_as_the_specification_works_it(byte filter, byte[] filtered, byte[] above, byte[] row)
    {
        byte[] bytes = [.. filtered];
        PngReader.Unfilter(filter, bytes, above, bpp: 1);
        Assert.Equal(row, bytes);
    }

    [Fact]
    public void The_png_reader_refuses_a_chunk_whose_crc_is_wrong()
    {
        // The IHDR chunk's CRC is bytes 29 to 32 of the file: a wrong CRC alone is refused.
        byte[] png = Atari2600Suite.Read($"{ScreenshotRunner.Calibration}_ntsc.png");
        png[29] ^= 0x01;
        Assert.Contains("CRC", Assert.Throws<InvalidDataException>(() => PngReader.Read(png)).Message);
    }

    [Fact]
    public void A_reference_of_the_wrong_width_fails_the_runner()
    {
        var narrow = new Picture(159, 1, new uint[159]);
        Assert.Throws<InvalidDataException>(() => ScreenshotRunner.Compare(narrow, [new byte[FrameBuffer.Width]], 0));
    }

    [Fact]
    public void A_reference_reads_as_the_suite_measured_it()
    {
        // suite.md section 4: 160 by 259 on NTSC and 160 by 309 on PAL, alpha 255 everywhere.
        Picture ntsc = ScreenshotRunner.Reference(ScreenshotRunner.Calibration, Region.Ntsc);
        Picture pal = ScreenshotRunner.Reference(ScreenshotRunner.Calibration, Region.Pal);
        Assert.Equal((160, 259), (ntsc.Width, ntsc.Height));
        Assert.Equal((160, 309), (pal.Width, pal.Height));
        Assert.All(ntsc.Pixels, p => Assert.Equal(0xFFu, p >> 24));
    }
}
