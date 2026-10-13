using Xunit;
using static Dbhq.Machines.Nes.Tests.ApuTesting;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The frame counter, <c>docs/nes/facts/apu.md</c> section 10, on both regions. Every step cycle
/// comes from <see cref="Region"/>: its six entries are the rows of the sheet's table (ruling I).
/// In 4-step mode: [0] a quarter, [1] a quarter and a half, [2] a quarter, [3] the IRQ flag, [4] a
/// quarter, a half and the flag, [5] the flag and the wrap. In 5-step mode: [0] to [2] the same,
/// [3] nothing, [4] a quarter and a half, [5] the wrap. A quarter frame is seen as the triangle's
/// linear counter falling one, and a half frame as pulse 1's length counter falling one.
/// </summary>
public class FrameCounterTests
{
    public static TheoryData<string, int> RegionsAndParities()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            rows.Add(region, 0);
            rows.Add(region, 1);
        }

        return rows;
    }

    // The cycles after the write at which the sequence resets: 3 if the write is on an odd cycle
    // (a put, in the model), 4 if on an even one (a get), so the reset always lands on a get and the
    // steps on the puts the sheet's table names.
    private static int Delay(long writeCycle) => writeCycle % 2 == 1 ? 3 : 4;

    // A unit whose quarter and half clocks show: pulse 1 with the length halt clear and 254 loaded,
    // the triangle with the control flag clear and its linear counter at 127.
    private static Apu Watched(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x0F);
        apu.Write(0x00, 0x00);
        apu.Write(0x03, 0x08);
        apu.Write(0x08, 0x7F);
        apu.Write(0x0B, 0x08);
        ClockQuarterAndHalf(apu);
        Assert.Equal(127, apu.Triangle.LinearCounter);
        Assert.Equal(253, apu.Pulse1.LengthCounter);
        return apu;
    }

    private sealed record Seen(List<long> Quarters, List<long> Halves, List<long> IrqSets);

    // Runs the cycles and notes, as cycles after the reset, each quarter clock, each half clock and
    // each cycle in which the IRQ flag was set again after a read cleared it.
    private static Seen Watch(Apu apu, long reset, int cycles)
    {
        var seen = new Seen([], [], []);
        int linear = apu.Triangle.LinearCounter;
        int length = apu.Pulse1.LengthCounter;
        for (int c = 0; c < cycles; c++)
        {
            apu.Tick();
            long at = apu.Cycles - reset;
            if (apu.Triangle.LinearCounter != linear)
            {
                seen.Quarters.Add(at);
                linear = apu.Triangle.LinearCounter;
            }

            if (apu.Pulse1.LengthCounter != length)
            {
                seen.Halves.Add(at);
                length = apu.Pulse1.LengthCounter;
            }

            if ((apu.PeekStatus() & 0x40) != 0)
            {
                seen.IrqSets.Add(at);
                apu.ReadStatus();
            }
        }

        return seen;
    }

    [Theory]
    [MemberData(nameof(RegionsAndParities))]
    public void FourStepModeClocksOnTheRegionsStepsAndSetsTheFlagThreeCyclesInARow(string region, int parity)
    {
        IReadOnlyList<int> s = RegionNamed(region).FrameCounterFourStep;
        Apu apu = Watched(region);
        long write = WriteOnParity(apu, 0x17, 0x00, parity);
        long reset = write + Delay(write);

        Seen seen = Watch(apu, reset, (int)(reset - write) + (2 * s[5]) + 10);

        long p = s[5];
        Assert.Equal(new long[] { s[0], s[1], s[2], s[4], p + s[0], p + s[1], p + s[2], p + s[4] }, seen.Quarters);
        Assert.Equal(new long[] { s[1], s[4], p + s[1], p + s[4] }, seen.Halves);
        Assert.Equal(new long[] { s[3], s[4], s[5], p + s[3], p + s[4], p + s[5] }, seen.IrqSets);
    }

    [Theory]
    [MemberData(nameof(RegionsAndParities))]
    public void FiveStepModeClocksAtOnceThenOnTheRegionsStepsAndNeverSetsTheFlag(string region, int parity)
    {
        IReadOnlyList<int> s = RegionNamed(region).FrameCounterFiveStep;
        Apu apu = Watched(region);
        long write = WriteOnParity(apu, 0x17, 0x80, parity);
        long reset = write + Delay(write);

        Seen seen = Watch(apu, reset, (int)(reset - write) + (2 * s[5]) + 10);

        long p = s[5];
        Assert.Equal(new long[] { 0, s[0], s[1], s[2], s[4], p + s[0], p + s[1], p + s[2], p + s[4] }, seen.Quarters);
        Assert.Equal(new long[] { 0, s[1], s[4], p + s[1], p + s[4] }, seen.Halves);
        Assert.Empty(seen.IrqSets);
    }

    [Theory]
    [MemberData(nameof(RegionsAndParities))]
    public void AWriteResetsTheSequenceThreeCyclesLaterFromAnOddCycleAndFourFromAnEvenOne(string region, int parity)
    {
        // The 5-step write's immediate clocks show exactly when the reset lands.
        Apu apu = Watched(region);
        long write = WriteOnParity(apu, 0x17, 0x80, parity);
        int linear = apu.Triangle.LinearCounter;

        int expected = parity == 1 ? 3 : 4;
        for (int c = 1; c < expected; c++)
        {
            apu.Tick();
            Assert.Equal(linear, apu.Triangle.LinearCounter);
        }

        apu.Tick();
        Assert.Equal(write + expected, apu.Cycles);
        Assert.Equal(linear - 1, apu.Triangle.LinearCounter);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheOldSequenceRunsOnUntilTheResetLands(string region)
    {
        // A write three cycles before the 4-step IRQ step does not stop that step.
        IReadOnlyList<int> s = RegionNamed(region).FrameCounterFourStep;
        Apu apu = Make(region);
        long write = WriteOnParity(apu, 0x17, 0x00, 1);
        long reset = write + 3;
        Tick(apu, (int)(reset + s[3] - 2 - apu.Cycles));
        apu.Write(0x17, 0x00);
        Assert.Equal(0, apu.PeekStatus() & 0x40);
        Tick(apu, 2);
        Assert.Equal(0x40, apu.PeekStatus() & 0x40);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void SettingTheInhibitBitClearsTheFlagAndKeepsItClear(string region)
    {
        IReadOnlyList<int> s = RegionNamed(region).FrameCounterFourStep;
        Apu apu = Make(region);
        Tick(apu, s[5] + 10);
        Assert.True(apu.Irq);

        apu.Write(0x17, 0x40);
        Assert.False(apu.Irq);
        Assert.Equal(0, apu.PeekStatus() & 0x40);

        Tick(apu, 3 * s[5]);
        Assert.False(apu.Irq);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void WritingZeroOrEightyLeavesTheFlagAlone(string region)
    {
        // apu_test 3-irq_flag, item 6: writing $00 or $80 to $4017 does not touch the flag.
        IReadOnlyList<int> s = RegionNamed(region).FrameCounterFourStep;
        Apu apu = Make(region);
        Tick(apu, s[5] + 10);
        apu.Write(0x17, 0x00);
        Assert.True(apu.Irq);
        apu.Write(0x17, 0x80);
        Assert.True(apu.Irq);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void ReadingTheStatusClearsTheFlagAndPeekingDoesNot(string region)
    {
        IReadOnlyList<int> s = RegionNamed(region).FrameCounterFourStep;
        Apu apu = Make(region);
        Tick(apu, s[5] + 10);

        Assert.Equal(0x40, apu.PeekStatus() & 0x40);
        Assert.Equal(0x40, apu.PeekStatus() & 0x40);
        Assert.True(apu.Irq);

        Assert.Equal(0x40, apu.ReadStatus() & 0x40);
        Assert.False(apu.Irq);
        Assert.Equal(0, apu.ReadStatus() & 0x40);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void AtPowerOnTheUnitActsAsIfZeroWasWrittenTenCyclesBeforeTheFirstInstruction(string region)
    {
        // apu.md 10: the first instruction starts after the seven reset cycles, in cycle 8, so the
        // write is in cycle -2, an even one, and the reset lands four cycles later, in cycle 2.
        IReadOnlyList<int> s = RegionNamed(region).FrameCounterFourStep;
        Apu apu = Make(region);
        Assert.False(apu.Irq);
        Tick(apu, 2 + s[3] - 1);
        Assert.False(apu.Irq);
        apu.Tick();
        Assert.True(apu.Irq);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void ResetClearsTheFlagSilencesTheChannelsAndKeepsTheMode(string region)
    {
        // The fork's apu_reset/readme.txt: at reset the flag is clear, $4015 is cleared, and the
        // last value written to $4017 is written again.
        IReadOnlyList<int> five = RegionNamed(region).FrameCounterFiveStep;
        Apu apu = Make(region);
        Tick(apu, RegionNamed(region).FrameCounterFourStep[5] + 10);
        apu.Write(0x15, 0x0F);
        apu.Write(0x03, 0x08);
        apu.Write(0x0B, 0x08);
        Assert.True(apu.Irq);

        apu.Reset();
        Assert.False(apu.Irq);
        Assert.Equal(0, apu.PeekStatus());
        Assert.Equal(0, apu.Pulse1.LengthCounter);
        Assert.Equal(0, apu.Triangle.SequenceStep);

        apu.Write(0x17, 0x80);
        apu.Reset();
        Tick(apu, 3 * five[5]);
        Assert.False(apu.Irq);
    }

    // A $C0 write on an odd cycle: the reset, with its half frame, lands 3 cycles later. Ticks
    // to the cycle given (1 or 2 after the write) and returns, so the caller writes in that cycle.
    private static long ToCycleAfterAHalfFrameWrite(Apu apu, int cycleAfterWrite)
    {
        long write = WriteOnParity(apu, 0x17, 0xC0, 1);
        Tick(apu, cycleAfterWrite);
        return write;
    }

    public static TheoryData<string, int, bool, int> HaltWrites()
    {
        // The cycle of the halt write (1 or 2 after the $4017 write; the clock is in 3), whether
        // it sets or clears the halt, and the length after the clock (from 254).
        var rows = new TheoryData<string, int, bool, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            rows.Add(region, 1, true, 254);
            rows.Add(region, 2, true, 253);
            rows.Add(region, 1, false, 253);
            rows.Add(region, 2, false, 254);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(HaltWrites))]
    public void AHaltWrittenInTheCycleBeforeAHalfFrameTakesEffectAfterIt(string region, int writeCycle, bool halt, int expected)
    {
        // The fork's pal_apu_tests/readme.txt, test 10: "Changes to length counter halt occur
        // after clocking length, not before." The unit clocks in the tick that begins a cycle, so
        // the write that coincides with the clock is the one in the cycle before.
        Apu apu = Make(region);
        apu.Write(0x15, 0x01);
        apu.Write(0x00, (byte)(halt ? 0x00 : 0x20));
        apu.Write(0x03, 0x08);
        Tick(apu, 2);

        ToCycleAfterAHalfFrameWrite(apu, writeCycle);
        apu.Write(0x00, (byte)(halt ? 0x20 : 0x00));
        Tick(apu, 3 - writeCycle);

        Assert.Equal(expected, apu.Pulse1.LengthCounter);
    }

    public static TheoryData<string, int, bool, int> ReloadWrites()
    {
        // The cycle of the reload (to 2), whether the counter held 254 or 0 before it, and the
        // length after the clock.
        var rows = new TheoryData<string, int, bool, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            rows.Add(region, 1, true, 1);
            rows.Add(region, 2, true, 253);
            rows.Add(region, 1, false, 1);
            rows.Add(region, 2, false, 2);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(ReloadWrites))]
    public void AReloadInTheCycleBeforeAHalfFrameIsDroppedUnlessTheCounterWasZero(string region, int writeCycle, bool loaded, int expected)
    {
        // The fork's pal_apu_tests/readme.txt, test 11: "Write to length counter reload should be
        // ignored when made during length counter clocking and the length counter is not zero."
        Apu apu = Make(region);
        apu.Write(0x15, 0x01);
        apu.Write(0x00, 0x00);
        if (loaded)
        {
            apu.Write(0x03, 0x08);
        }

        Tick(apu, 2);

        ToCycleAfterAHalfFrameWrite(apu, writeCycle);
        apu.Write(0x03, 0x18);
        Tick(apu, 3 - writeCycle);

        Assert.Equal(expected, apu.Pulse1.LengthCounter);
    }
}
