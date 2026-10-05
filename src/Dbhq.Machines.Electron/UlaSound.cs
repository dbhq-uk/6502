namespace Dbhq.Machines.Electron;

/// <summary>
/// The ULA's sound: one channel and one bit (fact sheet <c>ula.md</c> s8). It is selected by
/// $FE07 bits 2 and 1 = <c>01</c>, the counter at $FE06 is S, and the output toggles every
/// 32 x (S + 1) cycles of 2 MHz, a frequency of 1 MHz / (32 x (S + 1)). S of 0 and 1 give a
/// constant level.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is lazy, and it has no event the bus must watch.</b> Between two writes the output is a
/// square wave whose every edge is a known sum, <c>start + k x 32 x (S + 1)</c>, so
/// <see cref="CatchUp"/> walks the toggles that have come due and cuts the time between them
/// into samples, without the bus asking it for anything on the cycle. It is brought up to date
/// at a write to $FE06 or $FE07, and when the page counts or reads <see cref="Buffer"/> (or asks
/// for <see cref="Toggles"/> or <see cref="Level"/>). The ULA's own <c>NextEvent</c> does not
/// include it: a toggle every 96 cycles at the fastest audible pitch would put an event in the
/// bus's hot comparison for no gain. A buffer nobody reads costs nothing, and the ring keeps
/// the newest second whatever the gap.
/// </para>
/// <para>
/// <b>A sample is the mean of the 1-bit level over its interval</b> (s8: "better average the
/// level over the interval, because the high frequencies are above audio"), taken in exact
/// integers: the interval boundaries are <c>k x 2,000,000 / sampleRate</c> cycles and the
/// position is kept in cycles x sampleRate, so the count of samples never drifts.
/// </para>
/// <para>
/// <b>Silence is 0.</b> The mean is 0 to 1, and a held level, high or low, is not a sound. The
/// amplifier is modelled as coupled and not direct: each sample goes through a one-pole high-pass
/// at 10 Hz (<c>y = x - xPrev + r x yPrev</c>), so a square wave comes out as a swing of about
/// plus and minus a half about 0, a held level decays to exactly 0 and stays there, and entering
/// or leaving sound mode with the level high does not click at the speaker. The first sample
/// from silence has nothing before it to remove, so it is its mean exactly.
/// </para>
/// <para>
/// <b>Two things the sheet leaves open (s12 item 9), both chosen and recorded in
/// <c>known-differences.md</c>.</b> A write to $FE06 restarts the divider, so the next toggle is
/// 32 x (S + 1) cycles after the write; and sound mode starts the divider when it is entered and
/// keeps the level the output last held. A write to $FE07 that stays in sound mode (the OS writes
/// it for the screen mode and the LED as well, s10c) does not touch the divider.
/// </para>
/// </remarks>
public sealed class UlaSound
{
    // The 2 MHz clock, per second: the length of a sample in units of cycles x sampleRate.
    private const long SampleUnits = 2_000_000;
    private const int CyclesPerCounterStep = 32;
    private const long Never = long.MaxValue;
    private const double CouplingHz = 10;
    private const double Settled = 1e-6;
    private const int CounterRegister = 6;
    private const int ControlRegister = 7;

    private readonly Func<long> _clock;
    private readonly int _sampleRate;
    private readonly double _retain;

    private bool _enabled = true;
    private int _counter;
    private bool _level;
    private long _toggles;
    private long _next = Never;

    // Where the samples have got to: _pos cycles, and the same time as _units (cycles x rate).
    private long _pos;
    private long _units;
    private long _sampleEnd = SampleUnits;
    private long _highUnits;
    private double _xPrev;
    private double _yPrev;

    /// <param name="sampleRate">Samples a second, 1 to <see cref="SoundBuffer.MaxSampleRate"/>.</param>
    /// <param name="clock">The machine's cycle count: asked when the page reads the buffer.</param>
    public UlaSound(int sampleRate, Func<long> clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        Buffer = new SoundBuffer(sampleRate);
        _sampleRate = sampleRate;
        _clock = clock;
        _retain = Math.Exp(-2 * Math.PI * CouplingHz / sampleRate);
        Buffer.Filling = () => CatchUp(_clock());
    }

    /// <summary>The samples made so far, for the page to read; reading it catches the sound up.</summary>
    public SoundBuffer Buffer { get; }

    /// <summary>Whether $FE07 bits 2 and 1 say sound (s8). The ULA is in sound mode at power on, which is S10's reset state and is silent until a counter of 2 or more is written (s12 item 4).</summary>
    public bool Enabled => _enabled;

    /// <summary>S, the last value written to $FE06 (s8); 0 at power on.</summary>
    public int Counter => _counter;

    /// <summary>The output bit, brought up to now; it is low at power on and holds its last value outside sound mode.</summary>
    public bool Level
    {
        get
        {
            CatchUp(_clock());
            return _level;
        }
    }

    /// <summary>How many times the output has changed, brought up to now: only in sound mode, and never at S of 0 or 1.</summary>
    public long Toggles
    {
        get
        {
            CatchUp(_clock());
            return _toggles;
        }
    }

    /// <summary>
    /// Brings the sound up to <paramref name="cycle"/>: every toggle that has come due, each at
    /// its own time, and the samples between them. A cycle at or before where it has got to does
    /// nothing.
    /// </summary>
    internal void CatchUp(long cycle)
    {
        if (cycle <= _pos)
        {
            return;
        }

        while (_next <= cycle)
        {
            Advance(_next);
            _level = !_level;
            _toggles++;
            _next += HalfPeriod(_counter);
        }

        Advance(cycle);
    }

    /// <summary>A write to register 6 (the counter) or 7 (the control register) at <paramref name="cycle"/>; the bus has already mirrored the address.</summary>
    internal void Write(int register, byte value, long cycle)
    {
        if (register is not (CounterRegister or ControlRegister))
        {
            throw new ArgumentOutOfRangeException(nameof(register), register, "Only $FE06 and $FE07 reach the sound.");
        }

        CatchUp(cycle);
        if (register == CounterRegister)
        {
            _counter = value;
            Restart();
            return;
        }

        bool sound = ((value >> 1) & 3) == 1;
        bool entering = sound && !_enabled;
        _enabled = sound;
        if (entering)
        {
            Restart();
        }
        else if (!sound)
        {
            _next = Never;
        }
    }

    /// <summary>Cycles from one toggle to the next at counter <paramref name="s"/>: 32 x (S + 1) (s8).</summary>
    private static long HalfPeriod(int s) => (long)CyclesPerCounterStep * (s + 1);

    /// <summary>Starts the divider now: the next toggle is a whole period away, or never in tape mode or at S of 0 or 1.</summary>
    private void Restart() => _next = _enabled && _counter >= 2 ? _pos + HalfPeriod(_counter) : Never;

    /// <summary>Makes the samples up to <paramref name="cycle"/> at the level it holds now.</summary>
    private void Advance(long cycle)
    {
        long to = cycle * _sampleRate;
        while (_units < to)
        {
            if (_units == _sampleEnd - SampleUnits)
            {
                // At the start of a sample: every whole sample before `to` is one level throughout.
                long whole = (to - _units) / SampleUnits;
                if (whole > 0)
                {
                    EmitRun(_level ? 1.0 : 0.0, whole);
                    _units += whole * SampleUnits;
                    _sampleEnd += whole * SampleUnits;
                    continue;
                }
            }

            long stop = Math.Min(to, _sampleEnd);
            if (_level)
            {
                _highUnits += stop - _units;
            }

            _units = stop;
            if (_units == _sampleEnd)
            {
                EmitSample(_highUnits / (double)SampleUnits);
                _highUnits = 0;
                _sampleEnd += SampleUnits;
            }
        }

        _pos = cycle;
    }

    private void EmitSample(double mean)
    {
        double y = mean - _xPrev + _retain * _yPrev;
        _xPrev = mean;
        _yPrev = y;
        Buffer.Add((float)y);
    }

    /// <summary>
    /// <paramref name="count"/> samples of one level. The coupling takes a while to let a step go,
    /// so they are made one at a time until it has, and the rest of a long run is silence in one go.
    /// </summary>
    private void EmitRun(double level, long count)
    {
        for (long left = count; left > 0; left--)
        {
            if (level == _xPrev && Math.Abs(_yPrev) < Settled)
            {
                _yPrev = 0;
                Buffer.AddSilence(left);
                return;
            }

            EmitSample(level);
        }
    }
}
