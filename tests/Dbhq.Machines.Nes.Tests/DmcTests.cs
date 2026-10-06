using Xunit;
using static Dbhq.Machines.Nes.Tests.DmcTesting;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The DMC, <c>docs/nes/facts/apu.md</c> section 8: its registers, its rates by region, the reader
/// that fetches sample bytes by DMA, the output unit that moves the level by 2 a bit, and the loop
/// and the IRQ. Each test runs on both regions; the bus carries the fetches.
/// </summary>
public class DmcTests
{
    public static TheoryData<string> Regions() => ApuTesting.Regions();

    public static TheoryData<string, int> RegionsAndRates()
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

    [Theory]
    [MemberData(nameof(Regions))]
    public void FourZeroOneOneLoadsTheLevelAtOnceAndBitSevenIsNotPartOfIt(string region)
    {
        Nes nes = Machine(region, 0x00);
        Assert.Equal(0, nes.Bus.Apu.Dmc.Level);

        nes.Bus.Write(0x4011, 0x45);
        Assert.Equal(0x45, nes.Bus.Apu.Dmc.Level);

        nes.Bus.Write(0x4011, 0xC5);
        Assert.Equal(0x45, nes.Bus.Apu.Dmc.Level);
    }

    [Theory]
    [MemberData(nameof(RegionsAndRates))]
    public void EachRateIndexGivesTheRegionsPeriodBetweenOutputChanges(string region, int index)
    {
        Region r = ApuTesting.RegionNamed(region);

        // Every bit of $55 is the opposite of the one before, so the level moves on every clock:
        // up 2, down 2, around 64.
        Nes nes = Machine(region, 0x55);
        nes.Bus.Write(0x4011, 64);
        Start(nes, index, 0x00, 0xFF, parity: 0, loop: true);
        Assert.Equal(r.DmcRates[index], nes.Bus.Apu.Dmc.Period);

        // Let the first byte reach the output unit, then time ten changes.
        int level = nes.Bus.Apu.Dmc.Level;
        var changes = new List<long>();
        long limit = nes.Bus.Cycles + (30L * r.DmcRates[index]);
        while (changes.Count < 12 && nes.Bus.Cycles < limit)
        {
            IdleRead(nes);
            if (nes.Bus.Apu.Dmc.Level != level)
            {
                level = nes.Bus.Apu.Dmc.Level;
                changes.Add(nes.Bus.Cycles);
            }
        }

        Assert.Equal(12, changes.Count);
        for (int i = 2; i < changes.Count; i++)
        {
            Assert.Equal(r.DmcRates[index], changes[i] - changes[i - 1]);
        }
    }

    [Theory]
    [InlineData("NTSC", 0, 428)]
    [InlineData("NTSC", 7, 214)]
    [InlineData("NTSC", 15, 54)]
    [InlineData("PAL", 0, 398)]
    [InlineData("PAL", 7, 198)]
    [InlineData("PAL", 15, 50)]
    public void ThreeRatesAreTheNumbersTheSheetGives(string region, int index, int period)
    {
        // Typed from apu.md 8, so a wrong table in Region fails here as well as above.
        Nes nes = Machine(region, 0x00);
        nes.Bus.Write(0x4010, (byte)index);
        Assert.Equal(period, nes.Bus.Apu.Dmc.Period);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WorkedExampleThreeAByteOfFifteenFromSixtyFour(string region)
    {
        // apu.md worked example 3: level 64 and the byte $0F give 66, 68, 70, 72, 70, 68, 66, 64.
        byte[] prg = new byte[0x8000];
        prg[0x4000] = 0x0F;
        Nes nes = Machine(region, prg);
        nes.Bus.Write(0x4011, 64);
        Start(nes, 15, 0x00, 0x00, parity: 0);

        Assert.Equal([66, 68, 70, 72, 70, 68, 66, 64], Levels(nes, 8, 64 * 54));
    }

    [Theory]
    [InlineData("NTSC", 120, 0xFF, new[] { 122, 124, 126 }, 126)]
    [InlineData("NTSC", 121, 0xFF, new[] { 123, 125, 127 }, 127)]
    [InlineData("NTSC", 5, 0x00, new[] { 3, 1 }, 1)]
    [InlineData("NTSC", 6, 0x00, new[] { 4, 2, 0 }, 0)]
    [InlineData("PAL", 120, 0xFF, new[] { 122, 124, 126 }, 126)]
    [InlineData("PAL", 121, 0xFF, new[] { 123, 125, 127 }, 127)]
    [InlineData("PAL", 5, 0x00, new[] { 3, 1 }, 1)]
    [InlineData("PAL", 6, 0x00, new[] { 4, 2, 0 }, 0)]
    public void TheLevelMovesByTwoOnEachBitAndStopsShortOfTheEnds(string region, int start, byte fill, int[] expected, int final)
    {
        // apu.md 8: a 1 adds 2 if the level is 125 or less, a 0 takes 2 if it is 2 or more.
        Nes nes = Machine(region, fill);
        nes.Bus.Write(0x4011, (byte)start);
        Start(nes, 15, 0x00, 0x01, parity: 0);

        Assert.Equal(expected, Levels(nes, expected.Length, 40 * 54));

        // Twelve more bits and it has not moved.
        Run(nes, 12 * 54);
        Assert.Equal(final, nes.Bus.Apu.Dmc.Level);
    }

    [Theory]
    [InlineData("NTSC", 0x00, 0xC000, 0x00, 1)]
    [InlineData("NTSC", 0x01, 0xC040, 0x01, 17)]
    [InlineData("NTSC", 0x80, 0xE000, 0x80, 2049)]
    [InlineData("NTSC", 0xFF, 0xFFC0, 0xFF, 4081)]
    [InlineData("PAL", 0x00, 0xC000, 0x00, 1)]
    [InlineData("PAL", 0x01, 0xC040, 0x01, 17)]
    [InlineData("PAL", 0x80, 0xE000, 0x80, 2049)]
    [InlineData("PAL", 0xFF, 0xFFC0, 0xFF, 4081)]
    public void TheAddressIsC000PlusSixtyFourTimesAAndTheLengthSixteenTimesLPlusOne(string region, byte a, int address, byte l, int length)
    {
        Nes nes = Machine(region, 0x00);
        nes.Bus.Write(0x4012, a);
        nes.Bus.Write(0x4013, l);
        Assert.Equal(address, nes.Bus.Apu.Dmc.SampleAddress);
        Assert.Equal(length, nes.Bus.Apu.Dmc.SampleLength);

        // Enabling starts the sample there; the first fetch has not happened yet.
        nes.Bus.Write(0x4015, 0x10);
        Assert.Equal(address, nes.Bus.Apu.Dmc.CurrentAddress);
        Assert.Equal(length, nes.Bus.Apu.Dmc.BytesRemaining);
        Assert.Equal(0x10, nes.Bus.Peek(0x4015) & 0x10);

        // After it, the address has moved on one and one byte fewer remains.
        RunUntilAStall(nes, 10);
        Assert.Equal((address + 1) & 0xFFFF, nes.Bus.Apu.Dmc.CurrentAddress);
        Assert.Equal(length - 1, nes.Bus.Apu.Dmc.BytesRemaining);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AFetchWrapsFromFfffToEightThousand(string region)
    {
        // $4012 = $FF puts the sample at $FFC0; 65 bytes run past $FFFF. The bytes up to $FFFF are
        // 0 and the one at $8000 is $FF, so the level rises only if the 65th came from $8000.
        byte[] prg = new byte[0x8000];
        prg[0x0000] = 0xFF;
        Nes nes = Machine(region, prg);
        Start(nes, 15, 0xFF, 0x04, parity: 0);

        for (int fetch = 1; fetch <= 64; fetch++)
        {
            RunUntilAStall(nes, 20 * 54);
        }

        Assert.Equal(0x8000, nes.Bus.Apu.Dmc.CurrentAddress);
        Assert.Equal(1, nes.Bus.Apu.Dmc.BytesRemaining);
        Assert.Equal(0, nes.Bus.Apu.Dmc.Level);

        RunUntilAStall(nes, 20 * 54);
        Assert.Equal(0x8001, nes.Bus.Apu.Dmc.CurrentAddress);
        Assert.Equal(0, nes.Bus.Apu.Dmc.BytesRemaining);
        Run(nes, 20 * 54);
        Assert.Equal(16, nes.Bus.Apu.Dmc.Level);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ASampleThatEndsWithIrqEnabledSetsTheFlagAndHoldsTheLine(string region)
    {
        Nes nes = Machine(region, 0x00);
        Start(nes, 15, 0x00, 0x00, parity: 0, irq: true);
        Assert.False(nes.Bus.Apu.Dmc.IrqFlag);

        // A one-byte sample: its only fetch ends it.
        RunUntilAStall(nes, 10);
        Assert.Equal(0, nes.Bus.Apu.Dmc.BytesRemaining);
        Assert.True(nes.Bus.Apu.Dmc.IrqFlag);
        Assert.True(nes.Bus.Apu.Irq);
        Assert.Equal(0x80, nes.Bus.Peek(0x4015) & 0x90);

        // A read of $4015 does not clear it (apu.md 9).
        nes.Bus.Read(0x4015);
        Assert.True(nes.Bus.Apu.Dmc.IrqFlag);

        // A write to $4015 does.
        nes.Bus.Write(0x4015, 0x00);
        Assert.False(nes.Bus.Apu.Dmc.IrqFlag);
        Assert.False(nes.Bus.Apu.Irq);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ClearingTheIrqEnableClearsTheFlag(string region)
    {
        Nes nes = Machine(region, 0x00);
        Start(nes, 15, 0x00, 0x00, parity: 0, irq: true);
        RunUntilAStall(nes, 10);
        Assert.True(nes.Bus.Apu.Dmc.IrqFlag);

        // Setting it again does not bring the flag back.
        nes.Bus.Write(0x4010, 0x8F);
        Assert.True(nes.Bus.Apu.Dmc.IrqFlag);
        nes.Bus.Write(0x4010, 0x0F);
        Assert.False(nes.Bus.Apu.Dmc.IrqFlag);
        nes.Bus.Write(0x4010, 0x8F);
        Assert.False(nes.Bus.Apu.Dmc.IrqFlag);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ASampleThatEndsWithIrqDisabledSetsNoFlag(string region)
    {
        Nes nes = Machine(region, 0x00);
        Start(nes, 15, 0x00, 0x00, parity: 0, irq: false);
        RunUntilAStall(nes, 10);
        Assert.Equal(0, nes.Bus.Apu.Dmc.BytesRemaining);
        Assert.False(nes.Bus.Apu.Dmc.IrqFlag);
        Assert.False(nes.Bus.Apu.Irq);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ALoopedSampleStartsAgainAndNeverSetsTheFlag(string region)
    {
        Nes nes = Machine(region, 0x00);
        Start(nes, 15, 0x02, 0x01, parity: 0, loop: true, irq: true);

        // 17 bytes, three times over: after the 17th fetch the reader is back at the start.
        var addresses = new List<int>();
        for (int fetch = 1; fetch <= 51; fetch++)
        {
            RunUntilAStall(nes, 20 * 54);
            addresses.Add(nes.Bus.Apu.Dmc.CurrentAddress);
            Assert.False(nes.Bus.Apu.Dmc.IrqFlag);
            Assert.True(nes.Bus.Apu.Dmc.BytesRemaining > 0);
        }

        Assert.Equal(0xC080, addresses[16]);
        Assert.Equal(17, nes.Bus.Apu.Dmc.BytesRemaining);
        Assert.Equal(0xC081, addresses[17]);
        Assert.Equal(0xC080, addresses[33]);
        Assert.Equal(0xC080, addresses[50]);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WritingZeroToFourZeroOneFiveStopsTheSample(string region)
    {
        Nes nes = Machine(region, 0x00);
        Start(nes, 15, 0x00, 0x10, parity: 0);
        RunUntilAStall(nes, 10);
        Assert.True(nes.Bus.Apu.Dmc.BytesRemaining > 0);

        nes.Bus.Write(0x4015, 0x00);
        Assert.Equal(0, nes.Bus.Apu.Dmc.BytesRemaining);
        Assert.Equal(0, nes.Bus.Peek(0x4015) & 0x10);

        // No more fetches come.
        long end = nes.Bus.Cycles + (40 * 54);
        while (nes.Bus.Cycles < end)
        {
            Assert.Equal(1, IdleRead(nes));
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EnablingAgainWhileBytesRemainChangesNothing(string region)
    {
        Nes nes = Machine(region, 0x00);
        Start(nes, 15, 0x00, 0x01, parity: 0);
        RunUntilAStall(nes, 10);
        int remaining = nes.Bus.Apu.Dmc.BytesRemaining;
        int address = nes.Bus.Apu.Dmc.CurrentAddress;

        nes.Bus.Write(0x4013, 0x05);
        nes.Bus.Write(0x4015, 0x10);
        Assert.Equal(remaining, nes.Bus.Apu.Dmc.BytesRemaining);
        Assert.Equal(address, nes.Bus.Apu.Dmc.CurrentAddress);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheResetButtonDisablesTheDmcAndKeepsBitZeroOfTheLevel(string region)
    {
        // apu.md 13: $4015 is 0 after a reset and the level keeps bit 0 only.
        Nes nes = Machine(region, 0x00);
        nes.Bus.Write(0x4011, 0x45);
        Start(nes, 15, 0x00, 0x10, parity: 0);
        RunUntilAStall(nes, 10);

        nes.Reset();
        Assert.Equal(0x01, nes.Bus.Apu.Dmc.Level);
        Assert.Equal(0, nes.Bus.Apu.Dmc.BytesRemaining);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheLevelReachesTheMixerWhetherOrNotTheDmcIsEnabled(string region)
    {
        // apu.md 8: the level always goes to the mixer.
        Nes nes = Machine(region, 0x00);
        Apu apu = nes.Bus.Apu;
        nes.Bus.Write(0x4011, 127);
        Assert.Equal(ApuMixer.Mix(apu.Pulse1.Output, apu.Pulse2.Output, apu.Triangle.Output, apu.Noise.Output, 127), apu.Output, 12);
        Assert.True(apu.Output > ApuMixer.Mix(0, 0, apu.Triangle.Output, 0, 0));
    }

    // The next count levels the output unit gives, each one a change, within the cycle limit.
    private static int[] Levels(Nes nes, int count, int limit)
    {
        var levels = new List<int>();
        int level = nes.Bus.Apu.Dmc.Level;
        long end = nes.Bus.Cycles + limit;
        while (levels.Count < count && nes.Bus.Cycles < end)
        {
            nes.Bus.Read(0x0000);
            if (nes.Bus.Apu.Dmc.Level != level)
            {
                level = nes.Bus.Apu.Dmc.Level;
                levels.Add(level);
            }
        }

        return [.. levels];
    }
}
