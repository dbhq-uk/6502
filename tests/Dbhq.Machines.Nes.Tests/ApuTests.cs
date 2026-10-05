using Xunit;
using static Dbhq.Machines.Nes.Tests.ApuTesting;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The sound unit as a whole: the status register (<c>docs/nes/facts/apu.md</c> section 9) and
/// the mixed output (section 11, worked example 4).
/// </summary>
public class ApuTests
{
    // apu.md 11, written out from the sheet for the test, with the DMC at 0.
    private static double Mix(int pulse1, int pulse2, int triangle, int noise)
    {
        double pulse = pulse1 + pulse2 == 0 ? 0 : 95.88 / ((8128.0 / (pulse1 + pulse2)) + 100);
        double tnd = triangle + noise == 0 ? 0 : 159.79 / ((1 / ((triangle / 8227.0) + (noise / 12241.0))) + 100);
        return pulse + tnd;
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void AtPowerOnTheOutputIsTheTriangleHoldingFifteen(string region)
    {
        // apu.md worked example 4: triangle 15 alone gives 0.2464. The triangle is at step 0,
        // output 15, and holds it while silent (apu.md 6).
        Apu apu = Make(region);
        Assert.Equal(0.2464, apu.Output, 4);
        Assert.Equal(Mix(0, 0, 15, 0), apu.Output, 12);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void BothPulsesAtFifteenGiveTheSheetsFigure(string region)
    {
        // apu.md worked example 4: both pulses at 15 give 0.2585.
        Apu apu = Make(region);
        apu.Write(0x15, 0x03);
        foreach (int b in new[] { 0x00, 0x04 })
        {
            apu.Write(b, 0xFF);       // duty 3, whose first step is 1; constant 15
            apu.Write(b + 2, 0x40);
            apu.Write(b + 3, 0x08);
        }

        Assert.Equal(15, apu.Pulse1.Output);
        Assert.Equal(15, apu.Pulse2.Output);
        Assert.Equal(Mix(15, 15, 15, 0), apu.Output, 12);
        Assert.Equal(0.2585, Mix(15, 15, 0, 0), 4);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheOutputStaysWithinZeroAndOneWithEveryChannelLoud(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x0F);
        apu.Write(0x00, 0xBF);
        apu.Write(0x02, 0x20);
        apu.Write(0x03, 0x08);
        apu.Write(0x04, 0x7F);
        apu.Write(0x06, 0x30);
        apu.Write(0x07, 0x08);
        apu.Write(0x08, 0xFF);
        apu.Write(0x0A, 0x10);
        apu.Write(0x0B, 0x08);
        apu.Write(0x0C, 0x3F);
        apu.Write(0x0F, 0x08);
        ClockQuarterAndHalf(apu);
        for (int c = 0; c < 20_000; c++)
        {
            apu.Tick();
            Assert.InRange(apu.Output, 0.0, 1.0);
            Assert.Equal(Mix(apu.Pulse1.Output, apu.Pulse2.Output, apu.Triangle.Output, apu.Noise.Output), apu.Output, 12);
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheStatusShowsEachLengthCounterOverZero(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x0F);
        Assert.Equal(0, apu.PeekStatus());

        apu.Write(0x03, 0x08);
        Assert.Equal(0x01, apu.PeekStatus());
        apu.Write(0x07, 0x08);
        Assert.Equal(0x03, apu.PeekStatus());
        apu.Write(0x0B, 0x08);
        Assert.Equal(0x07, apu.PeekStatus());
        apu.Write(0x0F, 0x08);
        Assert.Equal(0x0F, apu.PeekStatus());

        apu.Write(0x15, 0x05);
        Assert.Equal(0x05, apu.PeekStatus());
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheDmcRegistersAreAcceptedAndChangeNothingYet(string region)
    {
        // The DMC is task 9's. Until then its four registers and its $4015 bit are taken and ignored.
        Apu apu = Make(region);
        double before = apu.Output;
        for (int r = 0x10; r <= 0x13; r++)
        {
            apu.Write(r, 0xFF);
        }

        apu.Write(0x15, 0x10);
        Assert.Equal(0, apu.PeekStatus());
        Assert.Equal(before, apu.Output);
    }
}
