using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The sample buffer: the sound as samples at the host's rate, alone and through the machine. Its
/// count against machine time (the plan's "Fixed before running", item 11: one second gives the
/// sample rate's count, plus or minus 1, in both regions), its order, its bound with no reader (the
/// plan's Review Focus 5), the region switch mid-stream, and that the samples do not depend on how
/// a host divides the run.
/// </summary>
public class SampleBufferTests
{
    // LDA #4, STA AUDC0, LDA #15, STA AUDV0, then JMP to itself: a 15.7 kHz tone at full volume.
    private static readonly byte[] Tone = [0xA9, 0x04, 0x85, 0x15, 0xA9, 0x0F, 0x85, 0x19, 0x4C, 0x08, 0xF0];

    // LDA #8, STA AUDC0, LDA #9, STA AUDF0, LDA #12, STA AUDV0, LDA #5, STA AUDV1, then a loop that
    // writes AUDV1 from X as it counts: noise, and the volume written as a sample is played.
    private static readonly byte[] NoiseAndVolume =
    [
        0xA9, 0x08, 0x85, 0x15, 0xA9, 0x09, 0x85, 0x17, 0xA9, 0x0C, 0x85, 0x19, 0xA9, 0x05, 0x85, 0x1A,
        0xE8, 0x86, 0x1A, 0x4C, 0x10, 0xF0,
    ];

    // JMP to itself: silence.
    private static readonly byte[] Silence = [0x4C, 0x00, 0xF0];

    public static TheoryData<string, int, string> RegionsRatesAndPrograms()
    {
        var rows = new TheoryData<string, int, string>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (int rate in new[] { 48_000, 44_100 })
            {
                rows.Add(region, rate, "tone");
                rows.Add(region, rate, "silence");
            }

            rows.Add(region, 48_000, "noise and volume");
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsRatesAndPrograms))]
    public void Item_11_one_second_of_machine_time_gives_the_sample_rates_count_plus_or_minus_one(string regionName, int rate, string program)
    {
        Region region = TestPrograms.RegionNamed(regionName);
        Atari2600 machine = Machine(region, rate, Program(program));

        machine.Run((long)Math.Round(region.CpuHz));

        long made = machine.Sound.Available + machine.Sound.Dropped;
        Assert.InRange(made, rate - 1, rate + 1);
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Item_11_switching_the_region_mid_stream_keeps_the_rate(string first)
    {
        Region from = TestPrograms.RegionNamed(first);
        Region to = from == Region.Ntsc ? Region.Pal : Region.Ntsc;
        Atari2600 machine = Machine(from, 48_000, Tone);

        machine.Run((long)Math.Round(from.CpuHz / 2));
        machine.Bus.Region = to;
        Assert.Equal(to.CpuHz, machine.Sound.CpuHz);
        machine.Run((long)Math.Round(to.CpuHz / 2));

        long made = machine.Sound.Available + machine.Sound.Dropped;
        Assert.InRange(made, 48_000 - 1, 48_000 + 1);
    }

    [Fact]
    public void Item_11_alone_one_second_of_colour_clocks_gives_the_rate_and_a_change_of_clock_keeps_it()
    {
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 100_000);
        long clocks = (long)Math.Round(Region.Ntsc.ColourClockHz);
        AddInPieces(buffer, clocks, 0.25);
        Assert.InRange(buffer.Available, 48_000 - 1, 48_000 + 1);

        var switched = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 100_000);
        AddInPieces(switched, (long)Math.Round(Region.Ntsc.ColourClockHz / 2), 0.25);
        switched.CpuHz = Region.Pal.CpuHz;
        AddInPieces(switched, (long)Math.Round(Region.Pal.ColourClockHz / 2), 0.75);
        Assert.InRange(switched.Available, 48_000 - 1, 48_000 + 1);
    }

    [Fact]
    public void Read_gives_the_samples_in_order_however_it_is_called()
    {
        // Two buffers fed the same levels; one is read seven at a time, round its ring's end many
        // times, the other at once. They give the same samples.
        var chunked = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 50);
        var whole = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 10_000);
        var fromChunks = new List<float>();
        float[] seven = new float[7];
        for (int i = 0; i < 2000; i++)
        {
            int clocks = 37 + (i * 13 % 101);
            double level = i * 7 % 31 / 30.0;
            chunked.Add(clocks, level);
            whole.Add(clocks, level);
            int n;
            while ((n = chunked.Read(seven)) > 0)
            {
                fromChunks.AddRange(seven[..n]);
            }
        }

        Assert.Equal(0, chunked.Dropped);
        float[] all = new float[10_000];
        int count = whole.Read(all);
        Assert.True(count > 1000);
        Assert.Equal(all[..count], fromChunks.ToArray());
        Assert.Equal(0, whole.Available);
    }

    [Fact]
    public void Review_Focus_5_with_no_reader_it_keeps_the_newest_capacity_and_counts_the_rest_as_dropped()
    {
        const int capacity = 1000;
        var bounded = new SampleBuffer(48_000, Region.Pal.CpuHz, capacity);
        var reference = new SampleBuffer(48_000, Region.Pal.CpuHz, 20 * capacity);
        long clocksPerSample = (long)Math.Ceiling(Region.Pal.ColourClockHz / 48_000);
        for (int i = 0; i < 10 * capacity; i++)
        {
            double level = (i % 17) / 16.0;
            bounded.Add((int)clocksPerSample, level);
            reference.Add((int)clocksPerSample, level);
        }

        long made = reference.Available;
        Assert.True(made >= 10 * capacity);
        Assert.Equal(capacity, bounded.Capacity);
        Assert.Equal(capacity, bounded.Available);
        Assert.Equal(made - capacity, bounded.Dropped);

        float[] all = new float[made];
        reference.Read(all);
        float[] kept = new float[capacity];
        Assert.Equal(capacity, bounded.Read(kept));
        Assert.Equal(all[^capacity..], kept);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void A_steady_level_comes_out_as_that_level(double level)
    {
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 10_000);
        AddInPieces(buffer, (long)(Region.Ntsc.ColourClockHz / 10), level);
        float[] samples = new float[10_000];
        int count = buffer.Read(samples);
        foreach (float sample in samples[(count - 100)..count])
        {
            Assert.Equal(level, sample, 6);
        }
    }

    [Fact]
    public void A_step_comes_out_where_it_happened_late_by_the_latency()
    {
        // A step from 0 to 1 at a colour clock that falls 0.4 of the way between two sample
        // instants: the output passes one half between the instants either side of it, the
        // latency later.
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 10_000);
        double clocksPerSample = Region.Ntsc.ColourClockHz / 48_000;
        int at = (int)Math.Round(1000.4 * clocksPerSample);
        double position = at / clocksPerSample;
        buffer.Add(at, 1.0);
        buffer.Add((int)(200 * clocksPerSample), 1.0);
        float[] samples = new float[2000];
        int count = buffer.Read(samples);
        int firstOverHalf = Array.FindIndex(samples, 0, count, s => s >= 0.5f);

        // Sample index i (0-based) is instant i + 1.
        Assert.Equal((int)Math.Floor(position) + buffer.LatencySamples + 1, firstOverHalf + 1, 1.0);
    }

    [Fact]
    public void The_samples_do_not_depend_on_how_the_run_is_divided()
    {
        // The samples are made at the channels' steps and caught up at the end of each Run, so a
        // host that runs a frame at a time and one that runs the second at once read the same
        // samples, to the bit.
        Region region = Region.Ntsc;
        long second = (long)Math.Round(region.CpuHz);

        Atari2600 once = Machine(region, 48_000, NoiseAndVolume);
        once.Run(second);

        Atari2600 pieces = Machine(region, 48_000, NoiseAndVolume);
        while (pieces.Bus.Cycles < once.Bus.Cycles)
        {
            pieces.Run(Math.Min(997, once.Bus.Cycles - pieces.Bus.Cycles));
        }

        Assert.Equal(once.Bus.Cycles, pieces.Bus.Cycles);
        float[] a = new float[once.Sound.Capacity];
        float[] b = new float[pieces.Sound.Capacity];
        int na = once.Sound.Read(a);
        int nb = pieces.Sound.Read(b);
        Assert.Equal(na, nb);
        Assert.Equal(a[..na], b[..nb]);
        Assert.Equal(once.Sound.Dropped, pieces.Sound.Dropped);
    }

    [Fact]
    public void The_machine_holds_a_quarter_of_a_second_and_power_on_empties_it()
    {
        Atari2600 machine = Machine(Region.Pal, 44_100, Tone);
        Assert.Same(machine.Bus.Sound, machine.Sound);
        Assert.Equal(44_100 / 4, machine.Sound.Capacity);
        Assert.Equal(44_100, machine.Sound.SampleRate);

        machine.Run(100_000);
        Assert.True(machine.Sound.Available > 0);
        machine.PowerOn();
        Assert.Equal(0, machine.Sound.Available);
        Assert.Equal(0, machine.Sound.Dropped);
    }

    [Fact]
    public void Clear_starts_afresh_so_silence_after_it_is_exactly_zero()
    {
        // A level held and a step just made, whose smoothed edge is still to come; then Clear, as
        // power on does, and silence: every sample is 0, with nothing of the sound before it.
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 10_000);
        buffer.Add(0, 0.7);
        AddInPieces(buffer, 20_000, 0.7);
        buffer.Add(0, 0.2);
        buffer.Clear();
        Assert.Equal(0, buffer.Available);
        Assert.Equal(0, buffer.Dropped);

        AddInPieces(buffer, 20_000, 0.0);
        float[] samples = new float[10_000];
        int count = buffer.Read(samples);
        Assert.True(count > 200);
        Assert.All(samples[..count], s => Assert.Equal(0f, s));
    }

    [Fact]
    public void Adding_allocates_nothing()
    {
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 1000);
        buffer.Add(100, 0.5);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1_000_000; i++)
        {
            buffer.Add(112 + (i & 1) * 4, (i & 3) / 3.0);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void It_refuses_a_rate_a_clock_or_a_capacity_that_is_not_above_zero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(0, Region.Ntsc.CpuHz, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(48_000, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(48_000, Region.Ntsc.CpuHz, 0));
        var buffer = new SampleBuffer(48_000, Region.Ntsc.CpuHz, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.CpuHz = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Atari2600(Cartridge.Load(TestPrograms.Image4K(Silence)), Region.Ntsc, new Atari2600Options { SampleRate = 0 }));
    }

    private static Atari2600 Machine(Region region, int rate, byte[] code)
    {
        var machine = new Atari2600(Cartridge.Load(TestPrograms.Image4K(code)), region, new Atari2600Options { SampleRate = rate });
        machine.PowerOn();
        return machine;
    }

    private static byte[] Program(string name) => name switch
    {
        "tone" => Tone,
        "silence" => Silence,
        _ => NoiseAndVolume,
    };

    // Adds clocks at level in pieces the size of the TIA's audio steps.
    private static void AddInPieces(SampleBuffer buffer, long clocks, double level)
    {
        while (clocks > 0)
        {
            int piece = (int)Math.Min(114, clocks);
            buffer.Add(piece, level);
            clocks -= piece;
        }
    }
}
