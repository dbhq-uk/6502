namespace Dbhq.Machines.Nes;

/// <summary>
/// The machine's sound, as samples at a chosen rate: the bus gives it the mixed level once a CPU
/// cycle (<see cref="Add"/>), it resamples that to the sample rate, passes it through the
/// console's own filters, and keeps the samples in a ring for the page to read
/// (<see cref="Read"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The resampler is a band-limited step, on blocks of 8 cycles.</b> The level is first
/// averaged over blocks of 8 CPU cycles. Each change in a block's mean is then added to the output
/// as a step whose edge is smoothed by a windowed sinc: a Kaiser window (beta 9) over 16 samples
/// each side, cut off at 0.42 of the sample rate. Its response, worked out from the kernel, is
/// flat to 0.35 of the rate (16.8 kHz at 48 kHz), half at 0.42, 60 dB down at the Nyquist and 94
/// dB or more from 0.55 of the rate up. The edge is placed to 1/4096 of a sample. A cycle costs
/// an add; a block whose mean has not changed costs a compare; a change costs 33 multiply-adds
/// into a small ring of future samples. The output runs <see cref="LatencySamples"/> behind the
/// input, so every sample a step touches is still to come when the step arrives.
/// </para>
/// <para>
/// <b>Why the blocks.</b> Without them a triangle at period 0 or 1, which steps every CPU cycle or
/// every second one, made a step in nearly every cycle, about 120 to 200 ns more a cycle. Every
/// pulse period (16 (t + 1) cycles) and triangle period (32 (t + 1)) is a whole number of blocks,
/// so a note's block means repeat exactly with the note and anything the blocks fold can only land
/// on the note's own harmonics; the 8-cycle mean is down by under 0.25 dB at 20 kHz. Steps come at
/// most once a block, an eighth of the cycles.
/// </para>
/// <para>
/// <b>Chosen by measurement</b>, in task 9 of the NES plan, over a box filter (the mean of each
/// sample period), which is simpler: on pulses the box filter's worst alias was 20 to 32 dB under
/// the note against the line of 40, and an ultrasonic triangle came through it at 6 to 23 dB under
/// a full one. The figures and the command are in the journal and in <c>ResamplerTests</c>.
/// </para>
/// <para>
/// <b>The console's filters</b> follow, at the sample rate (apu.md 11, from the APU Mixer page):
/// a high-pass at 90 Hz, another at 440 Hz and a low-pass at 14 kHz, each first order. They can be
/// left out, for the tests that need the resampler alone.
/// </para>
/// <para>
/// <b>The ring never grows</b> (the plan's Review Focus 3). When it is full the oldest sample is
/// dropped for each new one and <see cref="Dropped"/> counts them, so a hidden tab that stops
/// reading loses old sound and no memory. Nothing allocates after construction.
/// </para>
/// </remarks>
public sealed class SampleBuffer
{
    // The level is averaged over blocks of this many cycles before it is resampled.
    private const int BlockCycles = 8;

    // A block whose mean moved by less than this is taken as unchanged: the mean of eight equal
    // levels can differ from the level in its last bit.
    private const double Unchanged = 1e-12;

    // The kernel: half-width in samples, the cut-off as a fraction of the sample rate, the Kaiser
    // window's beta, and the phases an edge is placed to within a sample. With 512 phases an
    // ultrasonic triangle's eight-level block steps were placed coarsely enough to leave it at
    // -55 dB; with 4096 it is -72 dB or under (the journal, task 9). The table is 4096 x 33
    // floats, 540 KB, of which a step reads one row.
    private const int HalfWidth = 16;
    private const double Cutoff = 0.42;
    private const double Beta = 9;
    private const int Phases = 4096;
    private const int Taps = (2 * HalfWidth) + 1;

    // The future samples a step adds into: a power of two over the taps.
    private const int PendingLength = 64;
    private const int PendingMask = PendingLength - 1;

    private static readonly float[] Kernel = BuildKernel();

    private readonly double _blockIncrement;
    private readonly float[] _ring;
    private readonly double[] _pending = new double[PendingLength];
    private readonly bool _consoleFilters;

    // The console's filters: two high-passes and a low-pass, first order.
    private readonly double _highPass90;
    private readonly double _highPass440;
    private readonly double _lowPass14k;
    private double _hp90Out;
    private double _hp90In;
    private double _hp440Out;
    private double _hp440In;
    private double _lpOut;

    // The block being summed and the cycles left in it; the level now, the output's running sum of
    // steps, the sample instant whose period the current block is in, and how far through that
    // period the block begins.
    private double _blockSum;
    private int _blockLeft = BlockCycles;
    private double _level;
    private double _output;
    private long _instant;
    private double _fraction;

    private int _read;
    private int _count;
    private long _dropped;

    /// <summary>
    /// A buffer making <paramref name="sampleRate"/> samples a second from one level a cycle at
    /// <paramref name="cpuHz"/>, holding up to <paramref name="capacity"/>, with the console's
    /// filters.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A rate or the capacity is not above 0, or the sample rate is above the CPU clock.</exception>
    public SampleBuffer(int sampleRate, double cpuHz, int capacity)
        : this(sampleRate, cpuHz, capacity, consoleFilters: true)
    {
    }

    /// <summary>A buffer as above, with the console's filters or without them.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A rate or the capacity is not above 0, or the sample rate is above the CPU clock.</exception>
    public SampleBuffer(int sampleRate, double cpuHz, int capacity, bool consoleFilters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cpuHz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (sampleRate * (double)BlockCycles > cpuHz)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), sampleRate, $"the sample rate cannot be above the CPU's clock over {BlockCycles}");
        }

        SampleRate = sampleRate;
        Capacity = capacity;
        _blockIncrement = BlockCycles * sampleRate / cpuHz;
        _ring = new float[capacity];
        _consoleFilters = consoleFilters;

        double dt = 1.0 / sampleRate;
        _highPass90 = HighPass(90, dt);
        _highPass440 = HighPass(440, dt);
        double rc = 1 / (2 * Math.PI * 14_000);
        _lowPass14k = dt / (rc + dt);
    }

    /// <summary>Samples a second.</summary>
    public int SampleRate { get; }

    /// <summary>The most samples it holds; it never holds more.</summary>
    public int Capacity { get; }

    /// <summary>Samples waiting to be read.</summary>
    public int Available => _count;

    /// <summary>Samples dropped, oldest first, because the reader fell behind.</summary>
    public long Dropped => _dropped;

    /// <summary>How many samples the output runs behind the levels added: the step's half-width.</summary>
    public int LatencySamples => HalfWidth;

    /// <summary>
    /// One CPU cycle at <paramref name="level"/>, the mixed output from 0 to 1. Every eighth cycle
    /// ends a block: a change in the block's mean is placed where the block began, and each sample
    /// instant the block passes is finished and stored.
    /// </summary>
    public void Add(double level)
    {
        _blockSum += level;
        if (--_blockLeft != 0)
        {
            return;
        }

        double mean = _blockSum / BlockCycles;
        _blockSum = 0;
        _blockLeft = BlockCycles;
        if (Math.Abs(mean - _level) > Unchanged)
        {
            Step(mean - _level);
            _level = mean;
        }

        _fraction += _blockIncrement;
        while (_fraction >= 1)
        {
            _fraction -= 1;
            Emit();
        }
    }

    /// <summary>
    /// Moves the oldest waiting samples into <paramref name="destination"/>, as many as fit, in
    /// order, and returns how many.
    /// </summary>
    public int Read(Span<float> destination)
    {
        int n = Math.Min(destination.Length, _count);
        int first = Math.Min(n, Capacity - _read);
        _ring.AsSpan(_read, first).CopyTo(destination);
        _ring.AsSpan(0, n - first).CopyTo(destination[first..]);
        _read = (_read + n) % Capacity;
        _count -= n;
        return n;
    }

    /// <summary>Empties it and sets <see cref="Dropped"/> back to 0, as at power on. The level being resampled is kept.</summary>
    public void Clear()
    {
        _read = 0;
        _count = 0;
        _dropped = 0;
    }

    // Adds a step of delta at the start of this block. With the output HalfWidth samples behind,
    // the step's smoothed edge touches the next Taps instants, none of them stored yet.
    private void Step(double delta)
    {
        int phase = (int)(_fraction * Phases);
        int offset = phase * Taps;
        long first = _instant + 1;
        for (int i = 0; i < Taps; i++)
        {
            _pending[(first + i) & PendingMask] += delta * Kernel[offset + i];
        }
    }

    private void Emit()
    {
        _instant++;
        int slot = (int)(_instant & PendingMask);
        _output += _pending[slot];
        _pending[slot] = 0;

        double sample = _output;
        if (_consoleFilters)
        {
            _hp90Out = _highPass90 * (_hp90Out + sample - _hp90In);
            _hp90In = sample;
            _hp440Out = _highPass440 * (_hp440Out + _hp90Out - _hp440In);
            _hp440In = _hp90Out;
            _lpOut += _lowPass14k * (_hp440Out - _lpOut);
            sample = _lpOut;
        }

        int write = _read + _count;
        if (write >= Capacity)
        {
            write -= Capacity;
        }

        _ring[write] = (float)sample;
        if (_count == Capacity)
        {
            _read = _read + 1 == Capacity ? 0 : _read + 1;
            _dropped++;
        }
        else
        {
            _count++;
        }
    }

    private static double HighPass(double hertz, double dt)
    {
        double rc = 1 / (2 * Math.PI * hertz);
        return rc / (rc + dt);
    }

    // For each phase f (the edge f of the way from one instant to the next), the step's share of
    // each of the next Taps instants: the integral of the windowed sinc over that instant's
    // period, shifted HalfWidth later. Each phase's shares add to 1, so a step settles exactly.
    private static float[] BuildKernel()
    {
        // The integral of the impulse, finely sampled from -HalfWidth to HalfWidth.
        const int resolution = 4096;
        int points = (2 * HalfWidth * resolution) + 1;
        double[] integral = new double[points];
        double i0Beta = BesselI0(Beta);
        double previous = Impulse(-HalfWidth, i0Beta);
        for (int p = 1; p < points; p++)
        {
            double x = -HalfWidth + ((double)p / resolution);
            double value = Impulse(x, i0Beta);
            integral[p] = integral[p - 1] + ((previous + value) / 2 / resolution);
            previous = value;
        }

        double total = integral[^1];
        double StepAt(double x)
        {
            if (x <= -HalfWidth)
            {
                return 0;
            }

            if (x >= HalfWidth)
            {
                return 1;
            }

            double position = (x + HalfWidth) * resolution;
            int below = (int)position;
            double between = position - below;
            double value = integral[below] + ((below + 1 < points ? integral[below + 1] - integral[below] : 0) * between);
            return value / total;
        }

        float[] kernel = new float[Phases * Taps];
        for (int phase = 0; phase < Phases; phase++)
        {
            double f = (phase + 0.5) / Phases;
            double sum = 0;
            for (int i = 0; i < Taps; i++)
            {
                double share = StepAt(1 + i - f - HalfWidth) - StepAt(i - f - HalfWidth);
                kernel[(phase * Taps) + i] = (float)share;
                sum += (float)share;
            }

            // Single precision rounding: the last share takes up what is left, so the sum is 1.
            kernel[(phase * Taps) + Taps - 1] += (float)(1 - sum);
        }

        return kernel;
    }

    // The windowed sinc at x samples from its centre.
    private static double Impulse(double x, double i0Beta)
    {
        double sinc = x == 0 ? 2 * Cutoff : Math.Sin(2 * Math.PI * Cutoff * x) / (Math.PI * x);
        double r = x / HalfWidth;
        double window = r * r >= 1 ? 0 : BesselI0(Beta * Math.Sqrt(1 - (r * r))) / i0Beta;
        return sinc * window;
    }

    // The modified Bessel function of the first kind, order 0, by its series.
    private static double BesselI0(double x)
    {
        double sum = 1;
        double term = 1;
        for (int k = 1; k < 50; k++)
        {
            term *= x / (2 * k);
            sum += term * term;
        }

        return sum;
    }
}
