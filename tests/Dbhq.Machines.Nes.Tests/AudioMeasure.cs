namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// What the resampler's measurements need: a spectrum, and the box filter the sample buffer was
/// compared with (task 9 of the NES plan wrote both and kept one by measurement; this is the one
/// not kept, here so the comparison can be run again).
/// </summary>
public static class AudioMeasure
{
    /// <summary>
    /// The magnitude spectrum of <paramref name="samples"/> (a power of two long) through a
    /// four-term Blackman-Harris window, whose side lobes are 92 dB down, so a component 80 dB under
    /// another still shows. Bin <c>k</c> is <c>k x rate / length</c> hertz; there are length / 2.
    /// </summary>
    public static double[] Spectrum(float[] samples)
    {
        int n = samples.Length;
        if (n == 0 || (n & (n - 1)) != 0)
        {
            throw new ArgumentException("the length must be a power of two", nameof(samples));
        }

        double[] re = new double[n];
        double[] im = new double[n];
        double mean = samples.Average(s => (double)s);
        for (int i = 0; i < n; i++)
        {
            double x = 2 * Math.PI * i / n;
            double window = 0.35875 - (0.48829 * Math.Cos(x)) + (0.14128 * Math.Cos(2 * x)) - (0.01168 * Math.Cos(3 * x));
            re[i] = (samples[i] - mean) * window;
        }

        Fft(re, im);
        double[] magnitude = new double[n / 2];
        for (int k = 0; k < n / 2; k++)
        {
            magnitude[k] = Math.Sqrt((re[k] * re[k]) + (im[k] * im[k]));
        }

        return magnitude;
    }

    /// <summary>
    /// The strongest part of <paramref name="spectrum"/> that is not a harmonic of
    /// <paramref name="fundamental"/>, in decibels under the fundamental, at or below
    /// <paramref name="upTo"/> hertz. A pulse sampled cleanly has only its harmonics under the
    /// Nyquist; anything else there is an alias of a harmonic above it. Bins within
    /// <paramref name="guard"/> of a harmonic, or of 0 Hz, are the window's main lobe and are left
    /// out. Returns the figure and the frequency it is at.
    /// </summary>
    public static (double Decibels, double Hertz) WorstAlias(double[] spectrum, int sampleRate, double fundamental, double upTo, int guard = 8)
    {
        int n = spectrum.Length * 2;
        double binHz = (double)sampleRate / n;
        int fundamentalBin = (int)Math.Round(fundamental / binHz);
        double peak = 0;
        for (int k = fundamentalBin - guard; k <= fundamentalBin + guard; k++)
        {
            peak = Math.Max(peak, spectrum[k]);
        }

        double worst = 0;
        int worstBin = 0;
        int last = Math.Min(spectrum.Length - 1, (int)(upTo / binHz));
        for (int k = guard + 1; k <= last; k++)
        {
            double hz = k * binHz;
            double harmonic = Math.Round(hz / fundamental) * fundamental;
            if (Math.Abs(hz - harmonic) <= guard * binHz)
            {
                continue;
            }

            if (spectrum[k] > worst)
            {
                worst = spectrum[k];
                worstBin = k;
            }
        }

        return (20 * Math.Log10(worst / peak), worstBin * binHz);
    }

    // An in-place radix-2 FFT.
    private static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (int length = 2; length <= n; length <<= 1)
        {
            double angle = -2 * Math.PI / length;
            double wRe = Math.Cos(angle);
            double wIm = Math.Sin(angle);
            for (int i = 0; i < n; i += length)
            {
                double cRe = 1;
                double cIm = 0;
                for (int k = 0; k < length / 2; k++)
                {
                    int a = i + k;
                    int b = a + (length / 2);
                    double tRe = (re[b] * cRe) - (im[b] * cIm);
                    double tIm = (re[b] * cIm) + (im[b] * cRe);
                    re[b] = re[a] - tRe;
                    im[b] = im[a] - tIm;
                    re[a] += tRe;
                    im[a] += tIm;
                    double next = (cRe * wRe) - (cIm * wIm);
                    cIm = (cRe * wIm) + (cIm * wRe);
                    cRe = next;
                }
            }
        }
    }
}

/// <summary>
/// The box filter: each output sample is the mean of the level over its own sample period, the
/// cycle that straddles a boundary split between the two. One add a cycle and nothing more, which
/// is why it was tried first. Its response is a sinc whose first null is at the sample rate, so
/// what lies between the Nyquist and the sample rate folds back only partly reduced.
/// </summary>
public sealed class BoxResampler
{
    private readonly double _cyclesPerSample;
    private readonly List<float> _samples = [];
    private double _sum;
    private double _remaining;

    public BoxResampler(int sampleRate, double cpuHz)
    {
        _cyclesPerSample = cpuHz / sampleRate;
        _remaining = _cyclesPerSample;
    }

    public IReadOnlyList<float> Samples => _samples;

    public void Add(double level)
    {
        if (_remaining > 1)
        {
            _sum += level;
            _remaining -= 1;
            return;
        }

        _sum += level * _remaining;
        _samples.Add((float)(_sum / _cyclesPerSample));
        double over = 1 - _remaining;
        _sum = level * over;
        _remaining = _cyclesPerSample - over;
    }
}
