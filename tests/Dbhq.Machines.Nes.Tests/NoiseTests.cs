using Xunit;
using static Dbhq.Machines.Nes.Tests.ApuTesting;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The noise channel, <c>docs/nes/facts/apu.md</c> section 7: the 15-bit shift register in its
/// two modes, the region's period table, and the output.
/// </summary>
public class NoiseTests
{
    public static TheoryData<string, int> RegionsAndIndexes()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            for (int index = 0; index < 16; index++)
            {
                rows.Add(region, index);
            }
        }

        return rows;
    }

    // apu.md worked example 2: the register after each of its first 16 clocks from 1.
    private static readonly int[] ModeZeroRegister =
    [
        0x4000, 0x2000, 0x1000, 0x0800, 0x0400, 0x0200, 0x0100, 0x0080,
        0x0040, 0x0020, 0x0010, 0x0008, 0x0004, 0x0002, 0x4001, 0x6000,
    ];

    private static readonly int[] ModeOneRegister =
    [
        0x4000, 0x2000, 0x1000, 0x0800, 0x0400, 0x0200, 0x0100, 0x0080,
        0x0040, 0x4020, 0x2010, 0x1008, 0x0804, 0x0402, 0x0201, 0x4100,
    ];

    // Each value the register takes, in order, as the timer clocks it, from power on.
    private static List<int> Registers(Apu apu, int count)
    {
        var seen = new List<int>();
        int last = apu.Noise.ShiftRegister;
        for (int n = 0; n < count; n++)
        {
            TickUntil(apu, () => apu.Noise.ShiftRegister != last, 5000);
            last = apu.Noise.ShiftRegister;
            seen.Add(last);
        }

        return seen;
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void AtPowerOnTheRegisterIsOne(string region)
    {
        Assert.Equal(1, Make(region).Noise.ShiftRegister);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void ModeZeroFromOneGivesTheSheetsSequence(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x0E, 0x00);

        List<int> registers = Registers(apu, 16);

        Assert.Equal(ModeZeroRegister, registers);

        // The output bit is bit 0: the first ten are 0, and the first 1 is at the fifteenth.
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 }, registers.Select(r => r & 1));
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void ModeOneFromOneGivesTheSheetsSequence(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x0E, 0x80);

        List<int> registers = Registers(apu, 16);

        Assert.Equal(ModeOneRegister, registers);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 }, registers.Select(r => r & 1));
    }

    [Theory]
    [MemberData(nameof(RegionsAndIndexes))]
    public void ThePeriodForEachIndexIsTheRegionsEntry(string region, int index)
    {
        Apu apu = Make(region);
        apu.Write(0x0E, (byte)index);
        Assert.Equal(RegionNamed(region).NoisePeriods[index], apu.Noise.Period);

        // The write does not restart the timer, so skip to a clock, then time the next three.
        int last = apu.Noise.ShiftRegister;
        TickUntil(apu, () => apu.Noise.ShiftRegister != last, 5000);

        for (int n = 0; n < 3; n++)
        {
            last = apu.Noise.ShiftRegister;
            int cycles = TickUntil(apu, () => apu.Noise.ShiftRegister != last, 5000);
            Assert.Equal(RegionNamed(region).NoisePeriods[index], cycles);
        }
    }

    [Fact]
    public void TheFirstMiddleAndLastPeriodsAreTheSheetsNumbers()
    {
        // apu.md 7, typed from the sheet so a wrong table in Region fails here.
        Assert.Equal(4, Region.Ntsc.NoisePeriods[0]);
        Assert.Equal(160, Region.Ntsc.NoisePeriods[7]);
        Assert.Equal(4068, Region.Ntsc.NoisePeriods[15]);
        Assert.Equal(4, Region.Pal.NoisePeriods[0]);
        Assert.Equal(148, Region.Pal.NoisePeriods[7]);
        Assert.Equal(3778, Region.Pal.NoisePeriods[15]);
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheOutputIsTheVolumeWhenBitZeroIsClearAndTheLengthIsLoaded(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x08);
        apu.Write(0x0C, 0x3A);   // halt, constant volume 10
        apu.Write(0x0E, 0x00);
        apu.Write(0x0F, 0x08);

        Assert.Equal(10, apu.Noise.Volume);
        for (int c = 0; c < 200; c++)
        {
            int expected = (apu.Noise.ShiftRegister & 1) == 0 ? 10 : 0;
            Assert.Equal(expected, apu.Noise.Output);
            apu.Tick();
        }

        apu.Write(0x15, 0x00);
        Assert.Equal(0, apu.Noise.LengthCounter);
        for (int c = 0; c < 200; c++)
        {
            Assert.Equal(0, apu.Noise.Output);
            apu.Tick();
        }
    }

    [Theory]
    [MemberData(nameof(Regions), MemberType = typeof(ApuTesting))]
    public void TheEnvelopeRestartsOnTheLengthWrite(string region)
    {
        Apu apu = Make(region);
        apu.Write(0x15, 0x08);
        apu.Write(0x0C, 0x20);   // loop and halt, envelope, V = 0
        apu.Write(0x0F, 0x08);
        for (int quarter = 0; quarter < 4; quarter++)
        {
            ClockQuarterAndHalf(apu);
        }

        Assert.Equal(12, apu.Noise.Volume);
        apu.Write(0x0F, 0x08);
        ClockQuarterAndHalf(apu);
        Assert.Equal(15, apu.Noise.Volume);
    }
}
