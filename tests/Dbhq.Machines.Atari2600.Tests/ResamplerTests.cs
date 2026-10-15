using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The resampler, chosen by the plan's "Fixed before running", item 12, with what it left open
/// fixed in the task 10 journal entry before anything was measured
/// (<c>docs/journal/2026-10-14-the-atari-2600-sound.md</c>, "The resampler's rule"). A TIA alone
/// plays <c>AUDC</c> 4 at volume 15 and <c>AUDF</c> 0, 15 and 31; every change of its level goes,
/// at its colour clock, to the box filter and to the sample buffer's band-limited steps; each
/// output's spectrum is searched for the strongest component that is not a harmonic of the tone.
/// The figures are written to the test output, and the journal records them with the command.
/// </summary>
public class ResamplerTests(ITestOutputHelper output)
{
    // Item 12's line: the strongest component that is not a harmonic at least 40 dB under the tone.
    private const double Line = -40;

    private const int SampleRate = 48_000;
    private const int Skipped = 2048;
    private const int Measured = 1 << 16;

    private const int Audc0 = 0x15;
    private const int Audf0 = 0x17;
    private const int Audv0 = 0x19;

    public static TheoryData<string, int> Cases()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (int audf in new[] { 0, 15, 31 })
            {
                rows.Add(region, audf);
            }
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Item_12_the_sample_buffer_meets_the_line(string regionName, int audf)
    {
        (double steps, double box, double fundamental) = Measure(TestPrograms.RegionNamed(regionName), audf);
        output.WriteLine($"{regionName} AUDC 4 AUDF {audf} ({fundamental:F1} Hz) at {SampleRate} Hz: strongest component that is not a harmonic, band-limited steps {steps:F1} dB, box filter {box:F1} dB");
        Assert.True(steps <= Line, $"the sample buffer's strongest component that is not a harmonic is {steps:F1} dB");
    }

    [Fact]
    public void Item_12_the_box_filter_misses_the_line_so_the_rule_keeps_the_band_limited_step()
    {
        var misses = new List<string>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (int audf in new[] { 0, 15, 31 })
            {
                double box = Measure(TestPrograms.RegionNamed(region), audf).Box;
                if (box > Line)
                {
                    misses.Add($"{region} AUDF {audf}: {box:F1} dB");
                }
            }
        }

        output.WriteLine($"the box filter misses the line in: {string.Join("; ", misses)}");
        Assert.True(misses.Count > 0, "the box filter meets the line in all six cases: item 12 keeps it, not the band-limited step");
    }

    // The two figures of one case, in dB under the fundamental, and the fundamental in hertz.
    private static (double Steps, double Box, double Fundamental) Measure(Region region, int audf)
    {
        var tia = new Tia();
        tia.Write(Audc0, 0x04);
        tia.Write(Audf0, (byte)audf);
        tia.Write(Audv0, 15);

        var steps = new SampleBuffer(SampleRate, region.CpuHz, Skipped + Measured + 1000);
        var box = new BoxResampler(SampleRate, region.ColourClockHz);
        double level = tia.SoundLevel;
        steps.Add(0, level);
        box.Add(0, level);

        // Twice the colour clocks the samples need, at most, so a broken resampler fails and does not hang.
        long limit = (long)(2 * (Skipped + Measured + 100) * region.ColourClockHz / SampleRate);
        int clocks = 0;
        for (long tick = 0; tick < limit && (steps.Available < Skipped + Measured + 100 || box.Samples.Count < Skipped + Measured + 100); tick++)
        {
            tia.Tick();
            clocks++;

            // A held level is passed on every 1,024 colour clocks as well, so the resamplers make
            // samples through a silence; it adds no step.
            if (tia.SoundLevel != level || clocks == 1024)
            {
                level = tia.SoundLevel;
                steps.Add(clocks, level);
                box.Add(clocks, level);
                clocks = 0;
            }
        }

        float[] all = new float[steps.Available];
        steps.Read(all);
        float[] stepped = all[Skipped..(Skipped + Measured)];
        float[] boxed = [.. box.Samples.Skip(Skipped).Take(Measured)];

        // tia.md sections 18.2 and 18.3: two audio steps a line, AUDC 4 toggling on every divided
        // pulse, one every AUDF + 1 steps, so a period of AUDF + 1 lines.
        double fundamental = region.ColourClockHz / (228.0 * (audf + 1));
        double nyquist = SampleRate / 2.0;
        double s = AudioMeasure.WorstAlias(AudioMeasure.Spectrum(stepped), SampleRate, fundamental, nyquist).Decibels;
        double b = AudioMeasure.WorstAlias(AudioMeasure.Spectrum(boxed), SampleRate, fundamental, nyquist).Decibels;
        return (s, b, fundamental);
    }
}

/// <summary>
/// The box filter, the resampler item 12 measures the band-limited step against: each sample is
/// the mean of the level over its own sample period, worked out exactly from the colour clocks at
/// which the level changes. Its response is a sinc whose first null is at the sample rate, so what
/// lies between the Nyquist and the sample rate folds back only partly reduced. Kept here, not in
/// the machine, so the comparison can be run again.
/// </summary>
public sealed class BoxResampler(int sampleRate, double colourClockHz)
{
    private readonly double _clocksPerSample = colourClockHz / sampleRate;
    private readonly List<float> _samples = [];
    private double _sum;
    private double _filled;
    private double _level;

    public IReadOnlyList<float> Samples => _samples;

    /// <summary><paramref name="colourClocks"/> at the level last given, then <paramref name="level"/> from now on.</summary>
    public void Add(int colourClocks, double level)
    {
        double left = colourClocks;
        while (_filled + left >= _clocksPerSample)
        {
            double take = _clocksPerSample - _filled;
            _sum += _level * take;
            _samples.Add((float)(_sum / _clocksPerSample));
            _sum = 0;
            _filled = 0;
            left -= take;
        }

        _sum += _level * left;
        _filled += left;
        _level = level;
    }
}
