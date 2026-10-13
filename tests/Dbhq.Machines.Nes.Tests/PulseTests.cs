using Xunit;
using static Dbhq.Machines.Nes.Tests.ApuTesting;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The two pulse channels, <c>docs/nes/facts/apu.md</c> sections 2 to 5: the duty sequences, the
/// timer in APU cycles, the sweep and its two negates, the envelope and the length counter. Every
/// test runs on both regions, because the channel is clocked by the region's frame counter.
/// </summary>
public class PulseTests
{
    // apu.md 2, the duty table "as the output steps after a restart", one row a duty.
    private static readonly int[][] DutySteps =
    [
        [0, 1, 0, 0, 0, 0, 0, 0],
        [0, 1, 1, 0, 0, 0, 0, 0],
        [0, 1, 1, 1, 1, 0, 0, 0],
        [1, 0, 0, 1, 1, 1, 1, 1],
    ];

    // apu.md 5, the length table, entry L for bits 7 to 3 of the fourth register.
    private static readonly int[] LengthTable =
    [
        10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
        12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30,
    ];

    public static TheoryData<string, int> RegionsAndDuties()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            for (int duty = 0; duty < 4; duty++)
            {
                rows.Add(region, duty);
            }
        }

        return rows;
    }

    public static TheoryData<string, int> RegionsAndChannels()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            rows.Add(region, 1);
            rows.Add(region, 2);
        }

        return rows;
    }

    private static PulseChannel Channel(Apu apu, int channel) => channel == 1 ? apu.Pulse1 : apu.Pulse2;

    // A pulse playing: enabled, the duty given, the length halted, constant volume 15, the sweep
    // off, the timer t, and the length loaded from entry 1 (254).
    private static void Play(Apu apu, int channel, int duty, int t)
    {
        int b = channel == 1 ? 0x00 : 0x04;
        apu.Write(0x15, 0x03);
        apu.Write(b, (byte)((duty << 6) | 0x3F));
        apu.Write(b + 1, 0x00);
        apu.Write(b + 2, (byte)t);
        apu.Write(b + 3, (byte)(0x08 | (t >> 8)));
    }

    [Theory]
    [MemberData(nameof(RegionsAndDuties))]
    public void EachDutyStepsThroughItsEightOutputsOneStepEveryTimerPeriod(string region, int duty)
    {
        // apu.md worked example 0: after a $4003 write the sequencer is at its first step, and each
        // step lasts t + 1 APU cycles, 2(t + 1) CPU cycles, except the first, because the write does
        // not reset the timer's divider.
        Apu apu = Make(region);
        const int t = 100;
        Play(apu, 1, duty, t);

        Assert.Equal(0, apu.Pulse1.SequenceStep);
        Assert.Equal(15 * DutySteps[duty][0], apu.Pulse1.Output);

        // Run to the first step's end, then read sixteen whole steps.
        TickUntil(apu, () => apu.Pulse1.SequenceStep != 0, 2 * (t + 1));

        for (int n = 1; n <= 16; n++)
        {
            int step = n % 8;
            Assert.Equal(step, apu.Pulse1.SequenceStep);
            for (int c = 0; c < 2 * (t + 1); c++)
            {
                Assert.Equal(step, apu.Pulse1.SequenceStep);
                Assert.Equal(15 * DutySteps[duty][step], apu.Pulse1.Output);
                apu.Tick();
            }
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheTwelveAndAHalfPercentDutyGivesTheSheetsWorkedExample(string region)
    {
        // apu.md worked example 0: duty 0, constant volume 15, t = 100: 0 15 0 0 0 0 0 0.
        Apu apu = Make(region);
        Play(apu, 1, 0, 100);
        var outputs = new List<int> { apu.Pulse1.Output };
        int last = apu.Pulse1.SequenceStep;
        while (outputs.Count < 8)
        {
            TickUntil(apu, () => apu.Pulse1.SequenceStep != last, 202);
            last = apu.Pulse1.SequenceStep;
            outputs.Add(apu.Pulse1.Output);
        }

        Assert.Equal(new[] { 0, 15, 0, 0, 0, 0, 0, 0 }, outputs);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void ADutyWriteChangesTheWaveButNotTheSequencersPlace(string region)
    {
        Apu apu = Make(region);
        Play(apu, 1, 0, 50);
        TickUntil(apu, () => apu.Pulse1.SequenceStep == 3, 8 * 102);

        apu.Write(0x00, 0xBF);   // duty 2

        Assert.Equal(3, apu.Pulse1.SequenceStep);
        Assert.Equal(15, apu.Pulse1.Output);
    }

    [Theory]
    [MemberData(nameof(RegionsAndChannels))]
    public void APeriodUnderEightSilencesTheChannel(string region, int channel)
    {
        Apu apu = Make(region);
        Play(apu, channel, 2, 7);
        for (int c = 0; c < 400; c++)
        {
            Assert.Equal(0, Channel(apu, channel).Output);
            apu.Tick();
        }

        Play(apu, channel, 2, 8);
        int loudest = 0;
        for (int c = 0; c < 400; c++)
        {
            loudest = Math.Max(loudest, Channel(apu, channel).Output);
            apu.Tick();
        }

        Assert.Equal(15, loudest);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheNegatesDifferPulseOneTakesOneMore(string region)
    {
        // apu.md worked example 1: t = 20, shift 0, negate: pulse 1's target 20 - 21 = -1, clamped
        // to 0; pulse 2's 20 - 20 = 0. And with shift 1: 20 - 10 - 1 = 9 against 20 - 10 = 10.
        Apu apu = Make(region);
        Play(apu, 1, 2, 20);
        Play(apu, 2, 2, 20);

        apu.Write(0x01, 0x08);
        apu.Write(0x05, 0x08);
        Assert.Equal(0, apu.Pulse1.SweepTarget);
        Assert.Equal(0, apu.Pulse2.SweepTarget);

        apu.Write(0x01, 0x09);
        apu.Write(0x05, 0x09);
        Assert.Equal(9, apu.Pulse1.SweepTarget);
        Assert.Equal(10, apu.Pulse2.SweepTarget);
    }

    [Theory]
    [MemberData(nameof(RegionsAndChannels))]
    public void ATargetOverSevenFfMutesTheChannelEvenWithTheSweepOff(string region, int channel)
    {
        // apu.md worked example 1: negate off, shift 0, t = $400 gives $800, over $7FF.
        Apu apu = Make(region);
        Play(apu, channel, 3, 0x400);
        Assert.Equal(0x800, Channel(apu, channel).SweepTarget);
        Assert.Equal(0, Channel(apu, channel).Output);

        // One less is $7FE, in range: duty 3's first step is 1, so it sounds.
        Play(apu, channel, 3, 0x3FF);
        Assert.Equal(0x7FE, Channel(apu, channel).SweepTarget);
        Assert.Equal(15, Channel(apu, channel).Output);
    }

    [Theory]
    [MemberData(nameof(RegionsAndChannels))]
    public void TheSweepMovesThePeriodToTheTargetOnHalfFramesEveryPPlusOne(string region, int channel)
    {
        // apu.md 3: enabled, P = 2, shift 3, up: t goes to t + (t >> 3) on the half frames where
        // the divider is 0.
        Apu apu = Make(region);
        Play(apu, channel, 2, 0x100);
        int b = channel == 1 ? 0x01 : 0x05;
        apu.Write(b, 0xA3);

        var periods = new List<int>();
        for (int half = 0; half < 7; half++)
        {
            ClockQuarterAndHalf(apu);
            periods.Add(Channel(apu, channel).Period);
        }

        // The divider is 0 at power on, so the first half frame moves t and reloads the divider
        // with 2; then 1, 0, and the next moves t again: every P + 1 = 3 half frames.
        Assert.Equal(new[] { 0x120, 0x120, 0x120, 0x144, 0x144, 0x144, 0x16C }, periods);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheSweepLeavesThePeriodWhenTheShiftIsZero(string region)
    {
        Apu apu = Make(region);
        Play(apu, 1, 2, 0x100);
        apu.Write(0x01, 0x80);   // enabled, P = 0, shift 0
        for (int half = 0; half < 4; half++)
        {
            ClockQuarterAndHalf(apu);
        }

        Assert.Equal(0x100, apu.Pulse1.Period);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheEnvelopeDecaysFromFifteenAndStopsAtZero(string region)
    {
        // apu.md 4: V = 0 is a divider of period 1, so the decay falls one each quarter frame.
        Apu apu = Make(region);
        apu.Write(0x15, 0x01);
        apu.Write(0x00, 0x80);   // duty 2, loop clear, envelope, V = 0
        apu.Write(0x03, 0x08);

        var volumes = new List<int>();
        for (int quarter = 0; quarter < 18; quarter++)
        {
            ClockQuarterAndHalf(apu);
            volumes.Add(apu.Pulse1.Volume);
        }

        Assert.Equal(new[] { 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 0, 0 }, volumes);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheEnvelopeLoopsBackToFifteenWhenAsked(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x01);
        apu.Write(0x00, 0xA0);   // loop (and length halt), envelope, V = 0
        apu.Write(0x03, 0x08);

        var volumes = new List<int>();
        for (int quarter = 0; quarter < 18; quarter++)
        {
            ClockQuarterAndHalf(apu);
            volumes.Add(apu.Pulse1.Volume);
        }

        Assert.Equal(new[] { 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 15, 14 }, volumes);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheEnvelopesDividerTakesVPlusOneQuarterFramesAStep(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x01);
        apu.Write(0x00, 0x82);   // V = 2: three quarter frames a step
        apu.Write(0x03, 0x08);

        var volumes = new List<int>();
        for (int quarter = 0; quarter < 8; quarter++)
        {
            ClockQuarterAndHalf(apu);
            volumes.Add(apu.Pulse1.Volume);
        }

        Assert.Equal(new[] { 15, 15, 15, 14, 14, 14, 13, 13 }, volumes);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void ConstantVolumeHoldsVWhileTheDecayRunsUnderneath(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x01);
        apu.Write(0x00, 0x97);   // constant volume 7, V is also the divider: 8 quarters a step
        apu.Write(0x03, 0x08);
        for (int quarter = 0; quarter < 20; quarter++)
        {
            ClockQuarterAndHalf(apu);
            Assert.Equal(7, apu.Pulse1.Volume);
        }

        // The decay kept running: 1 (start) + 8 + 8 + 3 quarter frames left it at 13.
        apu.Write(0x00, 0x87);
        Assert.Equal(13, apu.Pulse1.Volume);
    }

    [Theory]
    [MemberData(nameof(RegionsAndChannels))]
    public void EveryLengthTableEntryLoads(string region, int channel)
    {
        Apu apu = Make(region);
        int b = channel == 1 ? 0x00 : 0x04;
        apu.Write(0x15, 0x03);
        for (int l = 0; l < 32; l++)
        {
            apu.Write(b + 3, (byte)(l << 3));
            Assert.Equal(LengthTable[l], Channel(apu, channel).LengthCounter);
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheLengthCounterCountsDownOnHalfFramesAndSilencesAtZero(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x01);
        apu.Write(0x00, 0xDF);   // duty 3, halt clear, constant 15
        apu.Write(0x02, 0x40);
        apu.Write(0x03, 0x18);   // entry 3: 2

        Assert.Equal(2, apu.Pulse1.LengthCounter);
        Assert.Equal(15, apu.Pulse1.Output);
        Assert.Equal(0x01, apu.PeekStatus() & 0x01);

        ClockQuarterAndHalf(apu);
        Assert.Equal(1, apu.Pulse1.LengthCounter);
        ClockQuarterAndHalf(apu);
        Assert.Equal(0, apu.Pulse1.LengthCounter);
        Assert.Equal(0, apu.Pulse1.Output);
        Assert.Equal(0, apu.PeekStatus() & 0x01);
        ClockQuarterAndHalf(apu);
        Assert.Equal(0, apu.Pulse1.LengthCounter);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheHaltBitStopsTheLengthCounter(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x01);
        apu.Write(0x00, 0xFF);   // halt
        apu.Write(0x03, 0x18);
        for (int half = 0; half < 5; half++)
        {
            ClockQuarterAndHalf(apu);
        }

        Assert.Equal(2, apu.Pulse1.LengthCounter);

        apu.Write(0x00, 0xDF);   // halt clear
        ClockQuarterAndHalf(apu);
        Assert.Equal(1, apu.Pulse1.LengthCounter);
    }

    [Theory]
    [MemberData(nameof(RegionsAndChannels))]
    public void ClearingTheEnableBitZeroesTheLengthAndAWriteThenLoadsNothing(string region, int channel)
    {
        Apu apu = Make(region);
        int b = channel == 1 ? 0x00 : 0x04;
        apu.Write(0x15, 0x03);
        apu.Write(b + 3, 0x08);
        Assert.Equal(254, Channel(apu, channel).LengthCounter);

        apu.Write(0x15, 0x00);
        Assert.Equal(0, Channel(apu, channel).LengthCounter);
        apu.Write(b + 3, 0x08);
        Assert.Equal(0, Channel(apu, channel).LengthCounter);
        Assert.Equal(0, apu.PeekStatus() & 0x03);
    }
}
