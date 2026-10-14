namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The machine's sound, as samples at the host's rate: the TIA tells it each time its level may
/// change, with the colour clocks since it last did (<see cref="Add"/>); it resamples that to the
/// sample rate with band-limited steps and keeps the samples in a ring for the page to read
/// (<see cref="Read"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Told at the channels' steps, not every cycle.</b> The TIA's level changes only when the
/// channels step, twice a line, or when a volume is written (tia.md sections 18.2 and 18.7). So the
/// TIA calls <see cref="Add"/> at those points, inside the cycle in which they happen, and nothing
/// runs on the colour clocks between: two or three calls a line, against 228 colour clocks.
/// </para>
/// <para>
/// <b>Time is counted in whole colour clocks.</b> Sample instant n falls at (n - start) x the
/// colour clocks a sample period holds, counted from the last change of the clock, so which samples
/// exist depends only on how many colour clocks have passed, not on how the calls divided them. A
/// run made in many pieces gives the same samples, to the bit, as one made at once.
/// </para>
/// <para>
/// <b>The resampler is a band-limited step</b>, chosen by the plan's "Fixed before running", item
/// 12, with the rule fixed before measuring and the figures in the task 10 journal entry
/// (<c>docs/journal/2026-10-14-the-atari-2600-sound.md</c>): the box filter missed the line, so the
/// band-limited step is kept. Each change of the level is added to the output as a step whose edge
/// is smoothed by a windowed sinc, the NES's kernel (<c>src/Dbhq.Machines.Nes/SampleBuffer.cs</c>):
/// a Kaiser window (beta 9) over 16 samples each side, cut off at 0.42 of the sample rate, the
/// edge placed to 1/4096 of a sample. A change costs 33 multiply-adds into a small ring of future
/// samples; a sample costs an add. The output runs <see cref="LatencySamples"/> behind the level.
/// No filter of the console's is modelled: the fact sheet gives none.
/// </para>
/// <para>
/// <b>The ring never grows</b> (the plan's Review Focus 5). When it is full the oldest sample is
/// dropped for each new one and <see cref="Dropped"/> counts them, so a hidden tab that stops
/// reading loses old sound and no memory. Nothing allocates after construction.
/// </para>
/// </remarks>
public sealed class SampleBuffer : IReportsState
{
    // The kernel: half-width in samples, the cut-off as a fraction of the sample rate, the Kaiser
    // window's beta, and the phases an edge is placed to within a sample (the NES's, item 12's rule).
    private const int HalfWidth = 16;
    private const double Cutoff = 0.42;
    private const double Beta = 9;
    private const int Phases = 4096;
    private const int Taps = (2 * HalfWidth) + 1;

    // The future samples a step adds into: a power of two over the taps.
    private const int PendingLength = 64;
    private const int PendingMask = PendingLength - 1;

    private static readonly float[] Kernel = BuildKernel();

    private readonly float[] _ring;
    private readonly double[] _pending = new double[PendingLength];

    private double _cpuHz;
    private double _clocksPerSample;

    // Colour clocks since the clock last changed; the instant last emitted, counted from power on;
    // the instant the clock last changed at; and how far past that instant, in samples, it changed.
    private long _clock;
    private long _instant;
    private long _baseInstant;
    private double _baseFraction;

    // The level now, and the output's running sum of steps.
    private double _level;
    private double _output;

    private int _read;
    private int _count;
    private long _dropped;

    /// <summary>
    /// A buffer making <paramref name="sampleRate"/> samples a second from a TIA clocked three times
    /// <paramref name="cpuHz"/>, holding up to <paramref name="capacity"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A rate, the clock or the capacity is not above 0.</exception>
    public SampleBuffer(int sampleRate, double cpuHz, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        SampleRate = sampleRate;
        Capacity = capacity;
        _ring = new float[capacity];
        CpuHz = cpuHz;
    }

    /// <summary>Samples a second.</summary>
    public int SampleRate { get; }

    /// <summary>The most samples it holds; it never holds more.</summary>
    public int Capacity { get; }

    /// <summary>Samples waiting to be read.</summary>
    public int Available => _count;

    /// <summary>Samples dropped, oldest first, because the reader fell behind.</summary>
    public long Dropped => _dropped;

    /// <summary>How many samples the output runs behind the level: the step's half-width.</summary>
    public int LatencySamples => HalfWidth;

    /// <summary>
    /// The CPU clock, a third of the colour clock, in hertz. The region switch changes it at once:
    /// the colour clocks added before it are timed by the old clock and those after by the new, and
    /// the sample instants run on without a gap or a repeat.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not above 0.</exception>
    public double CpuHz
    {
        get => _cpuHz;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            if (_clocksPerSample > 0)
            {
                // Re-base the count: the clock changes here, at the place reached so far.
                _baseFraction = Position() - (_instant - _baseInstant);
                _baseInstant = _instant;
                _clock = 0;
            }

            _cpuHz = value;
            _clocksPerSample = 3 * value / SampleRate;
        }
    }

    /// <summary>
    /// <paramref name="colourClocks"/> have passed at the level last given; from now the level is
    /// <paramref name="level"/>, the mixed output from 0 to 1. Every sample instant the clocks pass
    /// is finished and stored, then a change of level is placed where it happens.
    /// </summary>
    public void Add(int colourClocks, double level)
    {
        _clock += colourClocks;
        while (InstantTime(_instant + 1) <= _clock)
        {
            Emit();
        }

        if (level != _level)
        {
            Step(level - _level);
            _level = level;
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

    /// <summary>Everything back as it was made: no samples, nothing dropped, the level 0 and no step under way, as at power on.</summary>
    public void Clear()
    {
        Array.Clear(_pending);
        _clock = 0;
        _instant = 0;
        _baseInstant = 0;
        _baseFraction = 0;
        _level = 0;
        _output = 0;
        _read = 0;
        _count = 0;
        _dropped = 0;
    }

    // Where the count has reached, in samples past the instant the clock last changed at.
    private double Position() => (_clock / _clocksPerSample) + _baseFraction;

    // The colour clock, counted from the last change of the clock, at which instant n falls.
    private double InstantTime(long n) => (n - _baseInstant - _baseFraction) * _clocksPerSample;

    // Adds a step of delta here, between the instant last emitted and the next. With the output
    // HalfWidth samples behind, the step's smoothed edge touches the next Taps instants, none of
    // them stored yet.
    private void Step(double delta)
    {
        double fraction = Position() - (_instant - _baseInstant);
        int phase = Math.Clamp((int)(fraction * Phases), 0, Phases - 1);
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

        int write = _read + _count;
        if (write >= Capacity)
        {
            write -= Capacity;
        }

        _ring[write] = (float)_output;
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

    /// <summary>
    /// Every field, as <see cref="IReportsState"/> asks: the clock and the count of instants, the
    /// level and the steps under way, the ring's place and count, and the samples waiting in it,
    /// oldest first.
    /// </summary>
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_pending), _pending);
        sink.Add(nameof(_cpuHz), _cpuHz);
        sink.Add(nameof(_clocksPerSample), _clocksPerSample);
        sink.Add(nameof(_clock), _clock);
        sink.Add(nameof(_instant), _instant);
        sink.Add(nameof(_baseInstant), _baseInstant);
        sink.Add(nameof(_baseFraction), _baseFraction);
        sink.Add(nameof(_level), _level);
        sink.Add(nameof(_output), _output);
        sink.Add(nameof(_read), _read);
        sink.Add(nameof(_count), _count);
        sink.Add(nameof(_dropped), _dropped);

        // The waiting samples in the order Read gives them; the rest of the ring is old.
        float[] waiting = new float[_count];
        int first = Math.Min(_count, Capacity - _read);
        _ring.AsSpan(_read, first).CopyTo(waiting);
        _ring.AsSpan(0, _count - first).CopyTo(waiting.AsSpan(first));
        sink.Add(nameof(_ring), waiting);
        sink.Skip(nameof(SampleRate), StateReport.Fixed);
        sink.Skip(nameof(Capacity), StateReport.Fixed);
    }

    // For each phase f (the edge f of the way from one instant to the next), the step's share of
    // each of the next Taps instants: the integral of the windowed sinc over that instant's
    // period, shifted HalfWidth later. Each phase's shares add to 1, so a step settles exactly.
    // The NES's construction (src/Dbhq.Machines.Nes/SampleBuffer.cs), with the same parameters.
    private static float[] BuildKernel()
    {
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
