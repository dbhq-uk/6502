using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The order inside a CPU cycle, through the real bus and CPU: which colour clock a store's write
/// lands on. The plan's "Fixed before running" item 8 keeps the split of the cycle's three colour
/// clocks around the access by the suite's <c>tia-timing/</c> self-tests and its tie rule; task 5
/// kept all three before the access (<c>docs/atari-2600/facts/tia.md</c> section 4 records the
/// table). With that split a store whose write is cycle k of a line, counted from the first cycle
/// after a WSYNC halt, lands at colour clock 3k (tia.md section 1).
/// </summary>
public class CycleOrderTests
{
    /// <summary>The split task 5 kept: all three colour clocks of a cycle run before its access.</summary>
    [Fact]
    public void The_bus_runs_all_three_colour_clocks_before_the_access()
    {
        Assert.Equal(3, Atari2600Bus.ClocksBeforeAccess);
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Worked_example_colubk_written_on_cycle_53_changes_pixel_91(string regionName)
    {
        // tia.md section 4: STA COLUBK writing on cycle 53 (W 159) from pixel 91, and STY COLUBK on
        // cycle 56 (W 168) back from pixel 100. From power on, with the TIA's power-on phase.
        AssertPulse(TestPrograms.RegionNamed(regionName), cleanStart: false);
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Worked_example_holds_after_cleanstarts_two_rsync_strobes(string regionName)
    {
        // dasm's CLEAN_START, which every suite program but nine runs first, strobes RSYNC twice
        // as its stack wipe passes $0143 and $0103 (suite.md section 8). The tests' statements of
        // where a write lands (tia.md section 1: W = 3k) are made after it, so the line RSYNC
        // starts must keep a write on cycle k at clock 3k: the model restarts the counter three
        // clocks after RSYNC's write, the multiple of three nearest Towers's "a full H@1-H@2
        // cycle" (tia.md section 6) [guessing - verify; task 6's rsync test bears on it].
        AssertPulse(TestPrograms.RegionNamed(regionName), cleanStart: true);
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void The_midline_color_programs_pulse_lands_where_its_header_says(string regionName)
    {
        // The suite's tia-timing/midline-color, a screenshot test (recorded, compared from task 7)
        // whose header states the picture: after CLEAN_START, each line's 9-pixel pulse of
        // COL_FIELD starts at x 91 on the first swept line and 6 pixels further left each line, to
        // x 25 on the twelfth, then wraps. This reads those columns from the frame; it is not the
        // screenshot comparison, which task 7 builds.
        Region region = TestPrograms.RegionNamed(regionName);
        byte field = region == Region.Pal ? (byte)0x58 : (byte)0xC8;
        var machine = new Atari2600(Cartridge.Load(Atari2600Suite.Read($"tia-timing/midline-color_{SuiteRunner.Build(region)}.a26")), region);
        machine.PowerOn();
        machine.RunFrames(4);

        FrameBuffer frame = machine.Bus.Tia.Frame;
        var starts = new List<int>();
        for (int row = 0; row < frame.Lines; row++)
        {
            ReadOnlySpan<byte> line = frame.ColourNumbers.Slice(row * FrameBuffer.Width, FrameBuffer.Width);
            int first = line.IndexOf(field);
            if (first < 0)
            {
                continue;
            }

            int last = line.LastIndexOf(field);
            Assert.True(last - first == 8, $"row {row}: the pulse runs from {first} to {last}");
            Assert.True(line[first..(last + 1)].IndexOfAnyExcept(field) < 0, $"row {row}: the pulse has a gap");
            starts.Add(first);
        }

        // A sweep of twelve lines a turn, down the visible lines.
        Assert.True(starts.Count > 120, $"{starts.Count} rows hold the pulse");
        Assert.Equal(91, starts[0]);
        for (int i = 1; i < starts.Count; i++)
        {
            int expected = starts[i - 1] == 25 ? 91 : starts[i - 1] - 6;
            Assert.Equal(expected, starts[i]);
        }
    }

    // Runs the pulse program for three frames and checks the pulse line: row 1 of the last frame.
    private static void AssertPulse(Region region, bool cleanStart)
    {
        Atari2600 machine = TestPrograms.Machine(region, PulseProgram(cleanStart));
        machine.RunFrames(3);
        FrameBuffer frame = machine.Bus.Tia.Frame;
        Assert.Equal(2, frame.Lines);
        ReadOnlySpan<byte> row = frame.ColourNumbers.Slice(FrameBuffer.Width, FrameBuffer.Width);
        for (int x = 0; x < FrameBuffer.Width; x++)
        {
            Assert.True((x is >= 91 and <= 99 ? 0x44 : 0x00) == row[x], $"pixel {x} is {row[x]:X2}");
        }
    }

    /// <summary>
    /// A frame of two lines, again and again: VSYNC on and off (that line is row 0), then a WSYNC,
    /// 25 NOPs (cycles 1 to 50), STA COLUBK (writing on cycle 53) and STY COLUBK (on 56), then a
    /// WSYNC. With <paramref name="cleanStart"/>, dasm's CLEAN_START comes first.
    /// </summary>
    private static byte[] PulseProgram(bool cleanStart)
    {
        var code = new List<byte>();
        if (cleanStart)
        {
            // SEI; CLD; LDX #0; TXA; TAY; then DEX; TXS; PHA; BNE back to the DEX.
            code.AddRange([0x78, 0xD8, 0xA2, 0x00, 0x8A, 0xA8, 0xCA, 0x9A, 0x48, 0xD0, 0xFB]);
        }

        ushort frame = (ushort)(TestPrograms.Start + code.Count);
        code.AddRange([0xA9, 0x02, 0x85, 0x00, 0xA9, 0x00, 0x85, 0x00]);
        code.AddRange([0xA9, 0x44, 0xA0, 0x00, 0x85, 0x02]);
        code.AddRange(Enumerable.Repeat((byte)0xEA, 25));
        code.AddRange([0x85, 0x09, 0x84, 0x09, 0x85, 0x02]);
        code.AddRange([0x4C, (byte)(frame & 0xFF), (byte)(frame >> 8)]);
        return [.. code];
    }
}
