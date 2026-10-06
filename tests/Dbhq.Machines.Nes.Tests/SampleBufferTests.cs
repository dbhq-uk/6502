using System.Runtime.CompilerServices;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The sample buffer: one <see cref="SampleBuffer.Add"/> a CPU cycle, samples out at the sample
/// rate, and a ring that drops its oldest samples when the reader falls behind and never grows
/// (Review Focus 3). Every rate comes from <see cref="Region.CpuHz"/>, so the tests run on both.
/// </summary>
public class SampleBufferTests
{
    public static TheoryData<string, int> RegionsAndRates()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            rows.Add(region, 48_000);
            rows.Add(region, 44_100);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsAndRates))]
    public void OneSecondOfCyclesGivesOneSecondOfSamples(string region, int sampleRate)
    {
        double cpuHz = ApuTesting.RegionNamed(region).CpuHz;
        var buffer = new SampleBuffer(sampleRate, cpuHz, sampleRate * 2);

        long cycles = (long)Math.Round(cpuHz);
        for (long c = 0; c < cycles; c++)
        {
            buffer.Add(0.25);
        }

        Assert.InRange(buffer.Available, sampleRate - 1, sampleRate + 1);
        Assert.Equal(0, buffer.Dropped);
    }

    [Theory]
    [MemberData(nameof(RegionsAndRates))]
    public void ReadGivesTheSamplesInTheOrderTheyWereMade(string region, int sampleRate)
    {
        // Ten steady levels, each held for 200 samples' worth of cycles. With the console's filters
        // left out, each block's middle sample is its level, and the blocks come out in order,
        // whether read at once or in pieces.
        double cpuHz = ApuTesting.RegionNamed(region).CpuHz;
        var whole = new SampleBuffer(sampleRate, cpuHz, 4000, consoleFilters: false);
        var pieces = new SampleBuffer(sampleRate, cpuHz, 4000, consoleFilters: false);
        double cyclesPerSample = cpuHz / sampleRate;
        for (int block = 0; block < 10; block++)
        {
            long end = (long)Math.Round((block + 1) * 200 * cyclesPerSample);
            long start = (long)Math.Round(block * 200 * cyclesPerSample);
            for (long c = start; c < end; c++)
            {
                whole.Add(block / 10.0);
                pieces.Add(block / 10.0);
            }
        }

        float[] all = new float[4000];
        int count = whole.Read(all);
        Assert.InRange(count, 1999, 2001);
        Assert.Equal(0, whole.Available);

        float[] inPieces = new float[count];
        int read = 0;
        while (read < count)
        {
            read += pieces.Read(inPieces.AsSpan(read, Math.Min(37, count - read)));
        }

        Assert.Equal(all[..count], inPieces);

        int latency = whole.LatencySamples;
        for (int block = 0; block < 9; block++)
        {
            int middle = (block * 200) + 100 + latency;
            Assert.Equal(block / 10.0, all[middle], 6);
        }
    }

    [Theory]
    [MemberData(nameof(RegionsAndRates))]
    public void WithNoReaderTenTimesTheCapacityKeepsTheNewestAndCountsTheRestDropped(string region, int sampleRate)
    {
        // Review Focus 3: the tab is hidden and nothing drains the buffer.
        double cpuHz = ApuTesting.RegionNamed(region).CpuHz;
        const int capacity = 1000;
        var buffer = new SampleBuffer(sampleRate, cpuHz, capacity, consoleFilters: false);
        double cyclesPerSample = cpuHz / sampleRate;

        // The level is the block number, each block 100 samples long; ten times the capacity.
        long cycles = (long)Math.Round(10 * capacity * cyclesPerSample);
        for (long c = 0; c < cycles; c++)
        {
            buffer.Add(Math.Floor(c / cyclesPerSample / 100) / 1000.0);
        }

        long made = buffer.Available + buffer.Dropped;
        Assert.InRange(made, (10 * capacity) - 1, (10 * capacity) + 1);
        Assert.Equal(capacity, buffer.Capacity);
        Assert.Equal(capacity, buffer.Available);
        Assert.Equal(made - capacity, buffer.Dropped);

        // What is left is the newest tenth: blocks 89 or 90 to 99, nothing older, and the last
        // block's level is there.
        float[] kept = new float[capacity];
        Assert.Equal(capacity, buffer.Read(kept));
        Assert.True(kept.Min() >= 0.0885f, $"the oldest kept sample is {kept.Min()}, from before the last tenth");
        Assert.Equal(0.099, kept[^1], 6);
        Assert.Equal(0, buffer.Available);
    }

    [Fact]
    public void TheBufferNeverGrowsPastItsCapacity()
    {
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 256);
        for (int c = 0; c < 2_000_000; c++)
        {
            buffer.Add((c >> 6) & 1);
            Assert.True(buffer.Available <= 256);
        }

        Assert.Equal(256, buffer.Available);
    }

    [Fact]
    public void AReadOfAnEmptyBufferReturnsZero()
    {
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 100);
        float[] destination = new float[10];
        Assert.Equal(0, buffer.Read(destination));
        Assert.Equal(0, buffer.Read(Span<float>.Empty));
        Assert.Equal(0, buffer.Available);
        Assert.Equal(0, buffer.Dropped);
    }

    [Fact]
    public void AReadIntoASmallerSpanLeavesTheRest()
    {
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 100, consoleFilters: false);
        for (int c = 0; c < 40 * 38; c++)
        {
            buffer.Add(0.5);
        }

        int available = buffer.Available;
        Assert.Equal(10, buffer.Read(new float[10]));
        Assert.Equal(available - 10, buffer.Available);
    }

    [Fact]
    public void ClearEmptiesItAndForgetsTheDrops()
    {
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 10);
        for (int c = 0; c < 10_000; c++)
        {
            buffer.Add(0.5);
        }

        Assert.True(buffer.Dropped > 0);
        buffer.Clear();
        Assert.Equal(0, buffer.Available);
        Assert.Equal(0, buffer.Dropped);
    }

    [Theory]
    [MemberData(nameof(ApuTesting.Regions), MemberType = typeof(ApuTesting))]
    public void TheConsolesFiltersTakeTheSteadyLevelAway(string region)
    {
        // apu.md 11: the NES has high-pass filters at 90 Hz and 440 Hz after its DACs, so a level
        // held for a second ends near 0, where without them it stays.
        double cpuHz = ApuTesting.RegionNamed(region).CpuHz;
        var filtered = new SampleBuffer(48_000, cpuHz, 48_000);
        var plain = new SampleBuffer(48_000, cpuHz, 48_000, consoleFilters: false);
        for (long c = 0; c < (long)cpuHz; c++)
        {
            filtered.Add(0.5);
            plain.Add(0.5);
        }

        float[] samples = new float[48_000];
        int count = filtered.Read(samples);
        Assert.InRange(Math.Abs(samples[count - 1]), 0.0, 1e-4);
        count = plain.Read(samples);
        Assert.Equal(0.5, samples[count - 1], 6);
    }

    [Fact]
    public void AddingAllocatesNothing()
    {
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 1000);
        float[] destination = new float[64];

        // The measured pass is a method of its own, compiled optimised from its first call, and it
        // runs three times before it is measured, so Add and Read have been called millions of
        // times first. A loop measured in the test method itself, after 10,000 Adds and no Read,
        // was once counted with 3,896 bytes in a full run of the project (6 October 2026). Add
        // and Read allocate nothing, so that was the runtime's own work on code still being
        // compiled; this shape passed every one of 10 full runs and 25 runs of the class since.
        for (int pass = 0; pass < 3; pass++)
        {
            AddAndRead(buffer, destination);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        AddAndRead(buffer, destination);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    // A million cycles of sound in, read out as a page would, a block at a time.
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void AddAndRead(SampleBuffer buffer, float[] destination)
    {
        for (int c = 0; c < 1_000_000; c++)
        {
            buffer.Add(((c >> 5) & 1) * 0.3);
            if ((c & 1023) == 0)
            {
                buffer.Read(destination);
            }
        }
    }

    [Fact]
    public void ANonsenseConstructionIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(0, Region.Ntsc.CpuHz, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(48_000, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(48_000, Region.Ntsc.CpuHz, 0));
        // A sample rate above the CPU's clock would need more than one sample from a cycle.
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(2_000_000, Region.Ntsc.CpuHz, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-48_000)]
    [InlineData(1_000_000)]
    public void AMachineWithANonsenseSampleRateIsRefused(int sampleRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1)), Region.Ntsc, new NesOptions { SampleRate = sampleRate }));
    }

    [Theory]
    [MemberData(nameof(ApuTesting.Regions), MemberType = typeof(ApuTesting))]
    public void TheMachineFillsItsBufferAtTheOptionsRate(string region)
    {
        Region r = ApuTesting.RegionNamed(region);
        var nes = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1, prgFill: 0xEA)), r, new NesOptions { SampleRate = 32_000 });
        nes.PowerOn();
        nes.Sound.Clear();
        long start = nes.Bus.Cycles;
        nes.Run((long)(r.CpuHz / 10));
        long ran = nes.Bus.Cycles - start;
        double expected = ran * 32_000 / r.CpuHz;
        Assert.InRange(nes.Sound.Available, expected - 2, expected + 2);
        Assert.Same(nes.Bus.Sound, nes.Sound);
    }
}
