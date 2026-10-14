using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The resampler, chosen by measurement (task 9 of the NES plan). A pulse at a known period goes
/// through the box filter and through the sample buffer's band-limited steps, and each output's
/// spectrum is searched for anything that is not a harmonic: below the Nyquist that can only be
/// an alias. The figures are written to the test output; the journal records them with the
/// command. The second half is the triangle's ultrasonic periods 0 and 1 (apu.md 6), which must
/// come out as near silence, not as a tone folded down into the audible band.
/// </summary>
public class ResamplerTests(ITestOutputHelper output)
{
    // A component 40 dB under the note is 1 % of its amplitude: the line set before measuring for
    // "no strong alias below the Nyquist".
    private const double GoodEnough = -40;

    // What the sample buffer measured as built, -72.4 dB at worst (the NTSC triangle at period
    // 0; the pulses -89.6 dB at worst), with a margin: a change to the resampler that loses much
    // of that fails here.
    private const double AsMeasured = -65;

    private const int Length = 1 << 16;

    public static TheoryData<string, int, int> Cases()
    {
        // Timer periods t: 16 (t + 1) CPU cycles a period, about 6.6, 2.7 and 1.1 kHz on NTSC.
        var rows = new TheoryData<string, int, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (int t in new[] { 16, 40, 100 })
            {
                rows.Add(region, t, 48_000);
            }
        }

        rows.Add("NTSC", 40, 44_100);
        return rows;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ThePulseThroughTheSampleBufferHasNoStrongAliasAndTheBoxFilterDoes(string region, int t, int sampleRate)
    {
        double cpuHz = ApuTesting.RegionNamed(region).CpuHz;
        double fundamental = cpuHz / (16 * (t + 1));

        var buffer = new SampleBuffer(sampleRate, cpuHz, Length + 1000, consoleFilters: false);
        var box = new BoxResampler(sampleRate, cpuHz);

        // A 50 % pulse at volume 15, from the mixer: half its period high, half low.
        double high = ApuMixer.Pulse(15, 0);
        long cycles = (long)Math.Ceiling((Length + 200) * cpuHz / sampleRate);
        int half = 8 * (t + 1);
        for (long c = 0; c < cycles; c++)
        {
            double level = (c / half) % 2 == 0 ? high : 0;
            buffer.Add(level);
            box.Add(level);
        }

        float[] blep = new float[Length + 1000];
        int made = buffer.Read(blep);
        Assert.True(made >= Length + 100);

        var blepAlias = AudioMeasure.WorstAlias(AudioMeasure.Spectrum(blep[100..(Length + 100)]), sampleRate, fundamental, sampleRate / 2.0);
        var boxAlias = AudioMeasure.WorstAlias(AudioMeasure.Spectrum([.. box.Samples.Skip(100).Take(Length)]), sampleRate, fundamental, sampleRate / 2.0);
        var blepAudible = AudioMeasure.WorstAlias(AudioMeasure.Spectrum(blep[100..(Length + 100)]), sampleRate, fundamental, 16_000);
        var boxAudible = AudioMeasure.WorstAlias(AudioMeasure.Spectrum([.. box.Samples.Skip(100).Take(Length)]), sampleRate, fundamental, 16_000);

        output.WriteLine($"{region} t={t} ({fundamental:F1} Hz) at {sampleRate} Hz: worst alias to the Nyquist, band-limited steps {blepAlias.Decibels:F1} dB at {blepAlias.Hertz:F0} Hz, box {boxAlias.Decibels:F1} dB at {boxAlias.Hertz:F0} Hz; to 16 kHz, steps {blepAudible.Decibels:F1} dB, box {boxAudible.Decibels:F1} dB");

        Assert.True(blepAlias.Decibels < AsMeasured, $"the sample buffer's worst alias is {blepAlias.Decibels:F1} dB at {blepAlias.Hertz:F0} Hz");
        Assert.True(boxAlias.Decibels > GoodEnough, $"the box filter now meets the line, {boxAlias.Decibels:F1} dB: the simpler one would do");
        Assert.True(boxAlias.Decibels > blepAlias.Decibels + 20, $"the box filter's worst alias, {boxAlias.Decibels:F1} dB, is not clearly worse than the steps' {blepAlias.Decibels:F1} dB");
    }

    [Theory]
    [InlineData("NTSC", 0)]
    [InlineData("NTSC", 1)]
    [InlineData("PAL", 0)]
    [InlineData("PAL", 1)]
    public void AnUltrasonicTriangleComesOutAsNearSilence(string region, int period)
    {
        // apu.md 6: periods 0 and 1 step every CPU cycle or every second one, 56 or 28 kHz on
        // NTSC. Through the real sound unit and the sample buffer, its output's swing must be small
        // against the swing of an audible triangle at the same volume.
        double cpuHz = ApuTesting.RegionNamed(region).CpuHz;
        double fullSwing = ApuMixer.Tnd(15, 0, 0);

        float[] Through(int timer, bool steps)
        {
            Apu apu = ApuTesting.Make(region);
            apu.Write(0x15, 0x04);
            apu.Write(0x08, 0xFF);
            apu.Write(0x0A, (byte)timer);
            apu.Write(0x0B, (byte)(0x08 | (timer >> 8)));
            ApuTesting.ClockQuarterAndHalf(apu);
            var buffer = new SampleBuffer(48_000, cpuHz, 20_000, consoleFilters: false);
            var box = new BoxResampler(48_000, cpuHz);
            for (int c = 0; c < (int)(cpuHz / 4); c++)
            {
                apu.Tick();
                buffer.Add(apu.Output);
                box.Add(apu.Output);
            }

            if (!steps)
            {
                return [.. box.Samples.Skip(1000)];
            }

            float[] samples = new float[20_000];
            int count = buffer.Read(samples);
            return samples[1000..count];
        }

        double Swing(float[] samples) => samples.Max() - samples.Min();

        float[] audible = Through(100, steps: true);
        Assert.True(Swing(audible) > 0.9 * fullSwing);

        double steps = 20 * Math.Log10(Swing(Through(period, steps: true)) / fullSwing);
        double boxed = 20 * Math.Log10(Swing(Through(period, steps: false)) / fullSwing);
        output.WriteLine($"{region} triangle period {period}: peak-to-peak against a full triangle, band-limited steps {steps:F1} dB, box {boxed:F1} dB");

        Assert.True(steps < AsMeasured, $"the ultrasonic triangle swings {steps:F1} dB against a full one");
        Assert.True(boxed > GoodEnough, $"through the box filter the ultrasonic triangle is {boxed:F1} dB: the simpler one would do");
    }
}
