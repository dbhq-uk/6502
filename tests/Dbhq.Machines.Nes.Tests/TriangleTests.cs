using Xunit;
using static Dbhq.Machines.Nes.Tests.ApuTesting;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The triangle, <c>docs/nes/facts/apu.md</c> section 6: the 32-step sequence on a timer that
/// counts CPU cycles, the linear counter and its control flag, the two counters that must both be
/// non-zero, and the ultrasonic periods 0 and 1. Both regions, because the frame counter clocks it.
/// </summary>
public class TriangleTests
{
    // apu.md 6: 15 14 ... 1 0 0 1 ... 14 15.
    private static readonly int[] Sequence =
    [
        15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0,
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    ];

    // Enabled, the control flag set (so the linear counter holds at R and the length is halted),
    // R = 127, timer t, length entry 1, then one quarter frame to load the linear counter.
    private static void Play(Apu apu, int t)
    {
        apu.Write(0x15, 0x04);
        apu.Write(0x08, 0xFF);
        apu.Write(0x0A, (byte)t);
        apu.Write(0x0B, (byte)(0x08 | (t >> 8)));
        ClockQuarterAndHalf(apu);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheSequenceIsTheSheetsThirtyTwoStepsOneStepEveryTPlusOneCpuCycles(string region)
    {
        Apu apu = Make(region);
        const int t = 40;
        Play(apu, t);

        int start = apu.Triangle.SequenceStep;
        TickUntil(apu, () => apu.Triangle.SequenceStep != start, t + 1);

        for (int n = 0; n < 64; n++)
        {
            int step = (start + 1 + n) % 32;
            for (int c = 0; c < t + 1; c++)
            {
                Assert.Equal(step, apu.Triangle.SequenceStep);
                Assert.Equal(Sequence[step], apu.Triangle.Output);
                apu.Tick();
            }
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void AtPowerOnTheSequenceIsAtItsFirstStep(string region)
    {
        // apu.md 13 gives step 0 after a reset and an unknown phase at power; the model takes step
        // 0 for both.
        Apu apu = Make(region);
        Assert.Equal(0, apu.Triangle.SequenceStep);
        Assert.Equal(15, apu.Triangle.Output);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheLinearCounterReloadsThenCountsDownWhenTheControlFlagIsClear(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x04);
        apu.Write(0x08, 0x05);   // control clear, R = 5
        apu.Write(0x0B, 0x08);   // sets the reload flag

        var counts = new List<int>();
        for (int quarter = 0; quarter < 8; quarter++)
        {
            ClockQuarterAndHalf(apu);
            counts.Add(apu.Triangle.LinearCounter);
        }

        Assert.Equal(new[] { 5, 4, 3, 2, 1, 0, 0, 0 }, counts);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void WithTheControlFlagSetTheLinearCounterReloadsEveryQuarterFrame(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x04);
        apu.Write(0x08, 0x85);   // control set, R = 5
        apu.Write(0x0B, 0x08);
        for (int quarter = 0; quarter < 6; quarter++)
        {
            ClockQuarterAndHalf(apu);
            Assert.Equal(5, apu.Triangle.LinearCounter);
        }

        // Clearing the control flag lets the reload flag clear on the next quarter frame, which
        // still reloads; then it counts down.
        apu.Write(0x08, 0x05);
        ClockQuarterAndHalf(apu);
        Assert.Equal(5, apu.Triangle.LinearCounter);
        ClockQuarterAndHalf(apu);
        Assert.Equal(4, apu.Triangle.LinearCounter);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheControlFlagAlsoHaltsTheLengthCounter(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x04);
        apu.Write(0x08, 0x85);
        apu.Write(0x0B, 0x18);   // entry 3: 2
        for (int half = 0; half < 4; half++)
        {
            ClockQuarterAndHalf(apu);
        }

        Assert.Equal(2, apu.Triangle.LengthCounter);

        apu.Write(0x08, 0x05);
        ClockQuarterAndHalf(apu);
        Assert.Equal(1, apu.Triangle.LengthCounter);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void ALinearCounterOfZeroStopsTheSequencerAndTheOutputHoldsItsValue(string region)
    {
        Apu apu = Make(region);
        Play(apu, 10);
        Tick(apu, 55);
        apu.Write(0x08, 0x80);   // R = 0, control set: the next quarter frame loads 0
        ClockQuarterAndHalf(apu);
        Assert.Equal(0, apu.Triangle.LinearCounter);

        int step = apu.Triangle.SequenceStep;
        int output = apu.Triangle.Output;
        for (int c = 0; c < 500; c++)
        {
            apu.Tick();
            Assert.Equal(step, apu.Triangle.SequenceStep);
            Assert.Equal(output, apu.Triangle.Output);
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void ALengthCounterOfZeroStopsTheSequencerAndTheOutputHoldsItsValue(string region)
    {
        Apu apu = Make(region);
        Play(apu, 10);
        Tick(apu, 55);
        apu.Write(0x15, 0x00);
        Assert.Equal(0, apu.Triangle.LengthCounter);
        Assert.NotEqual(0, apu.Triangle.LinearCounter);

        int step = apu.Triangle.SequenceStep;
        int output = apu.Triangle.Output;
        for (int c = 0; c < 500; c++)
        {
            apu.Tick();
            Assert.Equal(step, apu.Triangle.SequenceStep);
            Assert.Equal(output, apu.Triangle.Output);
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void PeriodsZeroAndOneRunUltrasonicallyAsTheSheetSays(string region)
    {
        // apu.md 6: periods 0 and 1 give an ultrasonic wave, which some emulators halt; the model
        // keeps the real behaviour. Period 0 steps every CPU cycle and period 1 every second.
        foreach (int t in new[] { 0, 1 })
        {
            Apu apu = Make(region);
            Play(apu, t);
            int step = apu.Triangle.SequenceStep;
            for (int c = 0; c < 64; c++)
            {
                apu.Tick();
                if (t == 1)
                {
                    apu.Tick();
                }

                step = (step + 1) % 32;
                Assert.Equal(step, apu.Triangle.SequenceStep);
                Assert.Equal(Sequence[step], apu.Triangle.Output);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheTimerWriteDoesNotRestartTheSequence(string region)
    {
        Apu apu = Make(region);
        Play(apu, 10);
        Tick(apu, 100);
        int step = apu.Triangle.SequenceStep;
        apu.Write(0x0B, 0x08);
        Assert.Equal(step, apu.Triangle.SequenceStep);
    }
}
