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
        Assert.Equal(Mix(0, 0, 15, 0), apu.Output, 6);
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
        Assert.Equal(Mix(15, 15, 15, 0), apu.Output, 6);
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
            Assert.Equal(Mix(apu.Pulse1.Output, apu.Pulse2.Output, apu.Triangle.Output, apu.Noise.Output), apu.Output, 6);
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheMixWorkedOutOnlyOnChangesIsTheChannelsMixOnEveryCycle(string region)
    {
        // The unit works the mix out again only when a timer, a frame step or a write may have
        // changed it. Through a machine, with every channel and the DMC playing, envelopes
        // decaying, sweeps, length counters running out and random writes to every register at
        // random times (a fixed seed), the cached level must be the channels' mix on every cycle.
        var random = new Random(9);
        byte[] prg = new byte[0x8000];
        random.NextBytes(prg);
        Nes nes = DmcTesting.Machine(region, prg);
        Apu apu = nes.Bus.Apu;
        byte[] registers = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x0A, 0x0B, 0x0C, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x15, 0x17];
        nes.Bus.Write(0x4015, 0x1F);
        nes.Bus.Write(0x4010, 0x4F);
        for (int c = 0; c < 200_000; c++)
        {
            if (random.Next(300) == 0)
            {
                nes.Bus.Write((ushort)(0x4000 + registers[random.Next(registers.Length)]), (byte)random.Next(256));
            }
            else
            {
                nes.Bus.Read(0x0000);
            }

            Assert.Equal(ApuMixer.Mix(apu.Pulse1.Output, apu.Pulse2.Output, apu.Triangle.Output, apu.Noise.Output, apu.Dmc.Level), apu.Output, 12);
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
    public void TheDmcsRegistersReachItAndItsBitsShowInTheStatus(string region)
    {
        // Task 9 replaced task 8's "accepted and ignored": $4011 sets the level the mixer hears,
        // $4013 the length, and enabling it shows bytes remaining in bit 4 (apu.md 8 and 9).
        Apu apu = Make(region);
        apu.Write(0x11, 0x40);
        Assert.Equal(0x40, apu.Dmc.Level);
        Assert.Equal(ApuMixer.Mix(0, 0, 15, 0, 0x40), apu.Output, 6);

        apu.Write(0x13, 0x01);
        apu.Write(0x15, 0x10);
        Assert.Equal(17, apu.Dmc.BytesRemaining);
        Assert.Equal(0x10, apu.PeekStatus());

        apu.Write(0x15, 0x00);
        Assert.Equal(0, apu.PeekStatus());
    }
}
