using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The real-hardware anchor (fact sheet <c>ula.md</c> s11d, s4g): BASIC's empty FOR loop timed in
/// each mode on two real Electrons. This measures the whole chain against hardware: the core's bus
/// cycles, the OS and BASIC ROMs, the interrupts and the contention rule. The expected figures are
/// the real machines' measurements copied from the sheet, not the model's output.
/// </summary>
public class ForLoopAnchorTests(ITestOutputHelper output)
{
    /// <summary>s11d: plus or minus 0.05 s. TIME counts centiseconds and the two machines differ by up to 0.02 s.</summary>
    private const double Tolerance = 0.05;

    /// <summary>Where BASIC keeps <c>A%</c>: the resident integers start at <c>$0400</c> with <c>@%</c>, four bytes each (s11d).</summary>
    private const ushort APercent = 0x0404;

    /// <summary>How often the run is checked for <c>A%</c> being set: 0.1 s of machine time.</summary>
    private const long Chunk = 200_000;

    /// <summary>
    /// The longest a run may take before it counts as a hang: 20 s of machine time, against 14.9 s
    /// for the slowest mode's loop plus the mode change before it (s11d).
    /// </summary>
    private const long Cap = 40_000_000;

    /// <summary>s11d, seconds per mode 0 to 6: the Issue 2 Electron (hoglet).</summary>
    private static readonly double[] Issue2 = [14.71, 14.74, 14.89, 11.92, 7.07, 7.08, 7.07];

    /// <summary>s11d, seconds per mode 0 to 6: the Issue 4 Electron with no Plus 1 (davidb).</summary>
    private static readonly double[] Issue4 = [14.71, 14.74, 14.90, 11.94, 7.08, 7.08, 7.08];

    /// <summary>s4c: what mode 3 gives if its two blank lines under each row are contended too.</summary>
    private const double Mode3WithBlankLinesContended = 14.34;

    /// <summary>One run per mode, shared by the tests in this class.</summary>
    private static readonly Lazy<double>[] Measured =
        Enumerable.Range(0, 7).Select(mode => new Lazy<double>(() => Measure(mode))).ToArray();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void TheForLoopTakesWhatTheRealMachinesTook(int mode)
    {
        double seconds = Measured[mode].Value;
        output.WriteLine($"Mode {mode}: {seconds:F2} s; Issue 2 {Issue2[mode]:F2} s, Issue 4 {Issue4[mode]:F2} s");
        Assert.InRange(seconds, Issue2[mode] - Tolerance, Issue2[mode] + Tolerance);
        Assert.InRange(seconds, Issue4[mode] - Tolerance, Issue4[mode] + Tolerance);
    }

    [Fact]
    public void TheModesStandInTheRealMachinesProportions()
    {
        // s11d: modes 0 to 2 against 4 to 6 are about 2.08 times apart, and mode 3 is between them
        // at about 1.68 times mode 4. The bounds are the real figures' own ratios, widened by the
        // tolerance on each figure, so they are arithmetic on the sheet's table.
        double[] lowest = Issue2.Zip(Issue4, Math.Min).Select(s => s - Tolerance).ToArray();
        double[] highest = Issue2.Zip(Issue4, Math.Max).Select(s => s + Tolerance).ToArray();

        for (int fast = 4; fast <= 6; fast++)
        {
            for (int slow = 0; slow <= 2; slow++)
            {
                Assert.InRange(
                    Measured[slow].Value / Measured[fast].Value,
                    lowest[slow] / highest[fast],
                    highest[slow] / lowest[fast]);
            }
        }

        Assert.InRange(Measured[3].Value / Measured[4].Value, lowest[3] / highest[4], highest[3] / lowest[4]);
        Assert.True(Measured[3].Value > Measured[4].Value && Measured[3].Value < Measured[0].Value);

        // s4c: contending mode 3's blank lines gives 14.34 s. The model must be nearer the real 11.92.
        Assert.True(
            Math.Abs(Measured[3].Value - Issue2[3]) < Math.Abs(Measured[3].Value - Mode3WithBlankLinesContended),
            $"Mode 3 took {Measured[3].Value} s, nearer {Mode3WithBlankLinesContended} than {Issue2[3]}.");
    }

    /// <summary>
    /// Boots, types the sheet's program for <paramref name="mode"/>, runs it, and returns <c>A%</c>
    /// in seconds. The run is checked every <see cref="Chunk"/> cycles and stops once <c>A%</c> is
    /// set, or fails at <see cref="Cap"/>.
    /// </summary>
    private static double Measure(int mode)
    {
        var s = new ElectronSession().Boot();
        ElectronBus bus = s.Machine.Bus;
        Assert.Equal(0, ReadAPercent(bus));

        s.Type($"10 MODE {mode}\r20 TIME=0\r30 FOR A=0 TO 10000:NEXT\r40 A%=TIME\rRUN\r");

        for (long run = 0; ReadAPercent(bus) == 0; run += Chunk)
        {
            Assert.True(run < Cap, $"Mode {mode}: A% was not set within {Cap} cycles.");
            s.RunFor(Chunk);
        }

        // BASIC stores the four bytes of A% over several instructions: let it finish the line.
        s.RunFor(Chunk);
        return ReadAPercent(bus) / 100.0;
    }

    private static int ReadAPercent(ElectronBus bus) =>
        bus.Peek(APercent) | (bus.Peek(APercent + 1) << 8) | (bus.Peek(APercent + 2) << 16) | (bus.Peek(APercent + 3) << 24);
}
