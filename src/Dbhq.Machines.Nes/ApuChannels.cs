namespace Dbhq.Machines.Nes;

/// <summary>
/// A length counter, <c>docs/nes/facts/apu.md</c> section 5: loaded from a table by the
/// channel's fourth register while the channel is enabled, counted down on half frames unless
/// halted, and silencing the channel at 0.
/// </summary>
/// <remarks>
/// A write that lands in the cycle before a half-frame clock acts with it, as the fork's
/// <c>pal_apu_tests</c> 10 and 11 require: the clock uses the halt flag as it was before the write,
/// and a reload is dropped if the counter was not 0 (apu.md section 5, from the fork's readme). The
/// unit clocks in the tick that begins a cycle, so a write in the cycle before is the one the readme
/// calls "during" the clock. The two flags that hold this last one cycle.
/// </remarks>
internal sealed class LengthCounter
{
    // apu.md 5.
    private static readonly byte[] Table =
    [
        10, 254, 20, 2, 40, 4, 80, 6, 160, 8, 60, 10, 14, 12, 26, 14,
        12, 16, 24, 18, 48, 20, 96, 22, 192, 24, 72, 26, 16, 28, 32, 30,
    ];

    private bool _haltWritten;
    private bool _haltBefore;
    private bool _loaded;
    private int _valueBefore;

    public int Value { get; private set; }

    public bool Halt { get; private set; }

    public bool Enabled { get; private set; }

    public void PowerOn()
    {
        Value = 0;
        Halt = false;
        Enabled = false;
        EndCycle();
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (!enabled)
        {
            Value = 0;
        }
    }

    public void SetHalt(bool halt)
    {
        if (!_haltWritten)
        {
            _haltBefore = Halt;
            _haltWritten = true;
        }

        Halt = halt;
    }

    public void Load(int index)
    {
        if (!Enabled)
        {
            return;
        }

        if (!_loaded)
        {
            _valueBefore = Value;
            _loaded = true;
        }

        Value = Table[index];
    }

    public void Clock()
    {
        bool halt = _haltWritten ? _haltBefore : Halt;
        if (_loaded)
        {
            if (_valueBefore == 0)
            {
                // The reload stands, and a counter at 0 is not clocked.
                return;
            }

            Value = _valueBefore;
        }

        if (!halt && Value > 0)
        {
            Value--;
        }
    }

    // The cycle after a write is over: the write no longer coincides with a clock.
    public void EndCycle()
    {
        _haltWritten = false;
        _loaded = false;
    }
}

/// <summary>
/// The envelope of a pulse or the noise, <c>docs/nes/facts/apu.md</c> section 4.
/// </summary>
internal sealed class Envelope
{
    private bool _start;
    private int _divider;
    private int _decay;

    public bool Loop { get; set; }

    public bool Constant { get; set; }

    public int V { get; set; }

    public int Volume => Constant ? V : _decay;

    public void PowerOn()
    {
        _start = false;
        _divider = 0;
        _decay = 0;
        Loop = false;
        Constant = false;
        V = 0;
    }

    public void Restart()
    {
        _start = true;
    }

    public void ClockQuarter()
    {
        if (_start)
        {
            _start = false;
            _decay = 15;
            _divider = V;
            return;
        }

        if (_divider > 0)
        {
            _divider--;
            return;
        }

        _divider = V;
        if (_decay > 0)
        {
            _decay--;
        }
        else if (Loop)
        {
            _decay = 15;
        }
    }
}

/// <summary>
/// A pulse channel, <c>docs/nes/facts/apu.md</c> sections 2 to 5: an 11-bit timer in APU cycles,
/// an 8-step duty sequencer, the sweep, the envelope and the length counter. The two channels
/// differ only in the sweep's negate.
/// </summary>
public sealed class PulseChannel
{
    // apu.md 2, the duty table as the output steps after a restart: bit s is step s.
    private static readonly byte[] Duties = [0x02, 0x06, 0x1E, 0xF9];

    private readonly bool _onesComplement;
    private readonly Envelope _envelope = new();
    private readonly LengthCounter _length = new();

    private int _timer;
    private int _period;
    private int _step;
    private int _duty;
    private bool _muted;

    private bool _sweepEnabled;
    private int _sweepPeriod;
    private bool _sweepNegate;
    private int _sweepShift;
    private int _sweepDivider;
    private bool _sweepReload;

    internal PulseChannel(bool onesComplement)
    {
        _onesComplement = onesComplement;
        PowerOn();
    }

    /// <summary>The level to the mixer, 0 to 15.</summary>
    public int Output =>
        _length.Value == 0 || _muted || ((Duties[_duty] >> _step) & 1) == 0 ? 0 : _envelope.Volume;

    /// <summary>The sequencer's step, 0 to 7, counted from the last restart (apu.md 2).</summary>
    public int SequenceStep => _step;

    /// <summary>The timer period <c>t</c>, 0 to <c>$7FF</c>.</summary>
    public int Period => _period;

    /// <summary>
    /// The sweep's target period, computed all the time (apu.md 3): <c>t</c> plus or minus
    /// <c>t &gt;&gt; S</c>, pulse 1 taking one more when negated, a negative sum clamped to 0.
    /// </summary>
    public int SweepTarget
    {
        get
        {
            int change = _period >> _sweepShift;
            if (!_sweepNegate)
            {
                return _period + change;
            }

            int target = _period - change - (_onesComplement ? 1 : 0);
            return target < 0 ? 0 : target;
        }
    }

    /// <summary>The envelope's output, 0 to 15.</summary>
    public int Volume => _envelope.Volume;

    /// <summary>The length counter.</summary>
    public int LengthCounter => _length.Value;

    internal LengthCounter Length => _length;

    internal void PowerOn()
    {
        _envelope.PowerOn();
        _length.PowerOn();
        _timer = 0;
        _period = 0;
        _step = 0;
        _duty = 0;
        _sweepEnabled = false;
        _sweepPeriod = 0;
        _sweepNegate = false;
        _sweepShift = 0;
        _sweepDivider = 0;
        _sweepReload = false;
        UpdateMute();
    }

    internal void Write(int register, byte value)
    {
        switch (register)
        {
            case 0:
                _duty = value >> 6;
                _envelope.Loop = (value & 0x20) != 0;
                _length.SetHalt((value & 0x20) != 0);
                _envelope.Constant = (value & 0x10) != 0;
                _envelope.V = value & 0x0F;
                break;
            case 1:
                _sweepEnabled = (value & 0x80) != 0;
                _sweepPeriod = (value >> 4) & 7;
                _sweepNegate = (value & 0x08) != 0;
                _sweepShift = value & 7;
                _sweepReload = true;
                break;
            case 2:
                _period = (_period & 0x700) | value;
                break;
            default:
                _period = (_period & 0xFF) | ((value & 7) << 8);
                _length.Load(value >> 3);
                _step = 0;
                _envelope.Restart();
                break;
        }

        UpdateMute();
    }

    // One APU cycle: the timer counts down, and on passing 0 reloads and moves the sequencer.
    internal void ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _period;
            _step = (_step + 1) & 7;
        }
        else
        {
            _timer--;
        }
    }

    internal void ClockQuarter() => _envelope.ClockQuarter();

    internal void ClockHalf()
    {
        _length.Clock();
        if (_sweepDivider == 0 && _sweepEnabled && _sweepShift != 0 && !_muted)
        {
            _period = SweepTarget;
            UpdateMute();
        }

        if (_sweepDivider == 0 || _sweepReload)
        {
            _sweepDivider = _sweepPeriod;
            _sweepReload = false;
        }
        else
        {
            _sweepDivider--;
        }
    }

    // apu.md 3: muted while t is under 8 or the target is over $7FF, the sweep enabled or not.
    private void UpdateMute()
    {
        _muted = _period < 8 || SweepTarget > 0x7FF;
    }
}

/// <summary>
/// The triangle, <c>docs/nes/facts/apu.md</c> section 6: a timer in CPU cycles, a 32-step
/// sequencer that runs while the linear counter and the length counter are both above 0, and holds
/// its value when they are not.
/// </summary>
/// <remarks>
/// Periods 0 and 1 are not halted. The sheet says they give an ultrasonic wave, which some
/// emulators halt instead, and that the model keeps the real behaviour unless the resampler needs
/// otherwise (task 9 settles that with the resampler).
/// </remarks>
public sealed class TriangleChannel
{
    private readonly LengthCounter _length = new();

    private int _timer;
    private int _period;
    private int _step;
    private int _linear;
    private int _linearReloadValue;
    private bool _linearReload;
    private bool _control;

    internal TriangleChannel()
    {
        PowerOn();
    }

    /// <summary>The level to the mixer, 0 to 15: the sequence's value, held when it stops.</summary>
    public int Output => _step < 16 ? 15 - _step : _step - 16;

    /// <summary>The sequencer's step, 0 to 31.</summary>
    public int SequenceStep => _step;

    /// <summary>The timer period <c>t</c>, 0 to <c>$7FF</c>.</summary>
    public int Period => _period;

    /// <summary>The linear counter, 0 to 127.</summary>
    public int LinearCounter => _linear;

    /// <summary>The length counter.</summary>
    public int LengthCounter => _length.Value;

    internal LengthCounter Length => _length;

    internal void PowerOn()
    {
        _length.PowerOn();
        _timer = 0;
        _period = 0;
        _step = 0;
        _linear = 0;
        _linearReloadValue = 0;
        _linearReload = false;
        _control = false;
    }

    // apu.md 13: after a reset the sequence is at step 0.
    internal void Reset()
    {
        _step = 0;
    }

    internal void Write(int register, byte value)
    {
        switch (register)
        {
            case 0:
                _control = (value & 0x80) != 0;
                _length.SetHalt(_control);
                _linearReloadValue = value & 0x7F;
                break;
            case 1:
                break;
            case 2:
                _period = (_period & 0x700) | value;
                break;
            default:
                _period = (_period & 0xFF) | ((value & 7) << 8);
                _length.Load(value >> 3);
                _linearReload = true;
                break;
        }
    }

    // One CPU cycle.
    internal void ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _period;
            if (_linear != 0 && _length.Value != 0)
            {
                _step = (_step + 1) & 31;
            }
        }
        else
        {
            _timer--;
        }
    }

    internal void ClockQuarter()
    {
        if (_linearReload)
        {
            _linear = _linearReloadValue;
        }
        else if (_linear > 0)
        {
            _linear--;
        }

        if (!_control)
        {
            _linearReload = false;
        }
    }

    internal void ClockHalf() => _length.Clock();
}

/// <summary>
/// The noise channel, <c>docs/nes/facts/apu.md</c> section 7: a 15-bit shift register clocked by a
/// timer whose period is the region's, the envelope and the length counter.
/// </summary>
public sealed class NoiseChannel
{
    private readonly int[] _periods;
    private readonly Envelope _envelope = new();
    private readonly LengthCounter _length = new();

    private int _timer;
    private int _reload;
    private int _periodIndex;
    private int _shiftRegister;
    private int _feedbackBit;

    internal NoiseChannel(Region region)
    {
        _periods = [.. region.NoisePeriods];
        PowerOn();
    }

    /// <summary>The level to the mixer, 0 to 15: silent while bit 0 is set.</summary>
    public int Output => _length.Value == 0 || (_shiftRegister & 1) != 0 ? 0 : _envelope.Volume;

    /// <summary>The 15-bit shift register.</summary>
    public int ShiftRegister => _shiftRegister;

    /// <summary>True in mode 1, where the feedback takes bit 6.</summary>
    public bool ShortMode => _feedbackBit == 6;

    /// <summary>The timer period in CPU cycles: the region's entry for the index in <c>$400E</c>.</summary>
    public int Period => _periods[_periodIndex];

    /// <summary>The envelope's output, 0 to 15.</summary>
    public int Volume => _envelope.Volume;

    /// <summary>The length counter.</summary>
    public int LengthCounter => _length.Value;

    internal LengthCounter Length => _length;

    internal void PowerOn()
    {
        _envelope.PowerOn();
        _length.PowerOn();
        _timer = 0;
        _periodIndex = 0;
        _reload = ReloadFor(0);
        _feedbackBit = 1;

        // apu.md 7: the register is 1 at power up.
        _shiftRegister = 1;
    }

    internal void Write(int register, byte value)
    {
        switch (register)
        {
            case 0:
                _envelope.Loop = (value & 0x20) != 0;
                _length.SetHalt((value & 0x20) != 0);
                _envelope.Constant = (value & 0x10) != 0;
                _envelope.V = value & 0x0F;
                break;
            case 1:
                break;
            case 2:
                _feedbackBit = (value & 0x80) != 0 ? 6 : 1;
                _periodIndex = value & 0x0F;
                _reload = ReloadFor(_periodIndex);
                break;
            default:
                _length.Load(value >> 3);
                _envelope.Restart();
                break;
        }
    }

    // One APU cycle. The table is in CPU cycles, all even, so the timer, a divider of period
    // reload + 1 APU cycles, reloads with half the entry less one.
    internal void ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _reload;
            int feedback = (_shiftRegister ^ (_shiftRegister >> _feedbackBit)) & 1;
            _shiftRegister = (_shiftRegister >> 1) | (feedback << 14);
        }
        else
        {
            _timer--;
        }
    }

    internal void ClockQuarter() => _envelope.ClockQuarter();

    internal void ClockHalf() => _length.Clock();

    private int ReloadFor(int index) => (_periods[index] / 2) - 1;
}
