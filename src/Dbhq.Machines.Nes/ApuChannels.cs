using System.Runtime.CompilerServices;

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
internal sealed class LengthCounter : IReportsState
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

    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Add(nameof(_haltWritten), _haltWritten);
        sink.Add(nameof(_haltBefore), _haltBefore);
        sink.Add(nameof(_loaded), _loaded);
        sink.Add(nameof(_valueBefore), _valueBefore);
        sink.Add(nameof(Value), Value);
        sink.Add(nameof(Halt), Halt);
        sink.Add(nameof(Enabled), Enabled);
    }
}

/// <summary>
/// The envelope of a pulse or the noise, <c>docs/nes/facts/apu.md</c> section 4.
/// </summary>
internal sealed class Envelope : IReportsState
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

    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Add(nameof(_start), _start);
        sink.Add(nameof(_divider), _divider);
        sink.Add(nameof(_decay), _decay);
        sink.Add(nameof(Loop), Loop);
        sink.Add(nameof(Constant), Constant);
        sink.Add(nameof(V), V);
    }
}

/// <summary>
/// A pulse channel, <c>docs/nes/facts/apu.md</c> sections 2 to 5: an 11-bit timer in APU cycles,
/// an 8-step duty sequencer, the sweep, the envelope and the length counter. The two channels
/// differ only in the sweep's negate.
/// </summary>
public sealed class PulseChannel : IReportsState
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
    // Returns true when the sequencer moved, which may change the output.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _period;
            _step = (_step + 1) & 7;
            return true;
        }

        _timer--;
        return false;
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

    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Skip(nameof(_onesComplement), StateReport.Fixed);
        sink.Add(nameof(_envelope), _envelope);
        sink.Add(nameof(_length), _length);
        sink.Add(nameof(_timer), _timer);
        sink.Add(nameof(_period), _period);
        sink.Add(nameof(_step), _step);
        sink.Add(nameof(_duty), _duty);
        sink.Add(nameof(_muted), _muted);
        sink.Add(nameof(_sweepEnabled), _sweepEnabled);
        sink.Add(nameof(_sweepPeriod), _sweepPeriod);
        sink.Add(nameof(_sweepNegate), _sweepNegate);
        sink.Add(nameof(_sweepShift), _sweepShift);
        sink.Add(nameof(_sweepDivider), _sweepDivider);
        sink.Add(nameof(_sweepReload), _sweepReload);
    }
}

/// <summary>
/// The triangle, <c>docs/nes/facts/apu.md</c> section 6: a timer in CPU cycles, a 32-step
/// sequencer that runs while the linear counter and the length counter are both above 0, and holds
/// its value when they are not.
/// </summary>
/// <remarks>
/// Periods 0 and 1 are not halted. The sheet says they give an ultrasonic wave, which some
/// emulators halt instead. The sample buffer's resampler does not need them halted: an ultrasonic
/// triangle comes out of it more than 65 dB under a full one, the line <c>ResamplerTests</c> holds
/// (the run of 6 October 2026 printed -72.4 dB).
/// </remarks>
public sealed class TriangleChannel : IReportsState
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

    /// <summary>True while the sequencer runs: the linear and length counters are both over 0.</summary>
    public bool Running => _linear != 0 && _length.Value != 0;

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

    // One CPU cycle. Returns true when the sequencer moved.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _period;
            if (_linear != 0 && _length.Value != 0)
            {
                _step = (_step + 1) & 31;
                return true;
            }

            return false;
        }

        _timer--;
        return false;
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

    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Add(nameof(_length), _length);
        sink.Add(nameof(_timer), _timer);
        sink.Add(nameof(_period), _period);
        sink.Add(nameof(_step), _step);
        sink.Add(nameof(_linear), _linear);
        sink.Add(nameof(_linearReloadValue), _linearReloadValue);
        sink.Add(nameof(_linearReload), _linearReload);
        sink.Add(nameof(_control), _control);
    }
}

/// <summary>
/// The noise channel, <c>docs/nes/facts/apu.md</c> section 7: a 15-bit shift register clocked by a
/// timer whose period is the region's, the envelope and the length counter.
/// </summary>
public sealed class NoiseChannel : IReportsState
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
    // Returns true when the register shifted.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ClockTimer()
    {
        if (_timer == 0)
        {
            _timer = _reload;
            int feedback = (_shiftRegister ^ (_shiftRegister >> _feedbackBit)) & 1;
            _shiftRegister = (_shiftRegister >> 1) | (feedback << 14);
            return true;
        }

        _timer--;
        return false;
    }

    internal void ClockQuarter() => _envelope.ClockQuarter();

    internal void ClockHalf() => _length.Clock();

    private int ReloadFor(int index) => (_periods[index] / 2) - 1;

    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Skip(nameof(_periods), StateReport.Fixed);
        sink.Add(nameof(_envelope), _envelope);
        sink.Add(nameof(_length), _length);
        sink.Add(nameof(_timer), _timer);
        sink.Add(nameof(_reload), _reload);
        sink.Add(nameof(_periodIndex), _periodIndex);
        sink.Add(nameof(_shiftRegister), _shiftRegister);
        sink.Add(nameof(_feedbackBit), _feedbackBit);
    }
}

/// <summary>
/// The DMC, <c>docs/nes/facts/apu.md</c> section 8: a timer at the region's rate, an output unit
/// that moves a 7-bit level by 2 for each bit of a sample byte, and a reader that asks the bus for
/// the next byte by DMA (<c>bus.md</c> section 6) whenever the one-byte buffer is empty and bytes
/// remain.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fetch is the bus's.</b> The channel only says when it wants one: the cycle from which the
/// bus may halt the CPU, and the address. The bus halts on its next read at or after that cycle,
/// runs the stolen cycles and hands the byte back (<see cref="CompleteFetch"/>). A load, the first
/// fetch after <c>$4015</c> starts a sample with the buffer empty, may halt on the get of the second
/// APU cycle after the write: 3 cycles after a write on a put, 4 after one on a get. A reload, after
/// the output unit empties the buffer, may halt on the next put. The bus's even cycles are gets, as
/// for OAM DMA, and the unit's APU-cycle clocks fall on the odd ones, the puts, so a reload made in
/// a put's tick may halt two cycles later. When in that APU cycle the hardware schedules it is not
/// on the sheet; the fork's DMA ROMs synchronise themselves to the DMC, so they test the cost and the
/// parity, which follow from this, more than the delay.
/// </para>
/// <para>
/// <b>Not modelled</b> (the DMA page's "Bugs"): a sample stopped in the APU cycle before a reload
/// would be scheduled does not start an aborted one-cycle DMA, and the late 2A03G and 2A03H's
/// extra fetch does not happen.
/// </para>
/// </remarks>
public sealed class DmcChannel : IReportsState
{
    private readonly int[] _rates;

    private int _rateIndex;
    private int _timer;
    private int _level;
    private bool _irqEnabled;
    private bool _loop;
    private bool _irqFlag;
    private int _sampleAddress;
    private int _sampleLength;

    // The reader.
    private int _currentAddress;
    private int _bytesRemaining;
    private int _buffer;
    private bool _bufferFull;
    private long _fetchFrom;

    // The output unit.
    private int _shift;
    private int _bitsRemaining;
    private bool _silent;

    internal DmcChannel(Region region)
    {
        _rates = [.. region.DmcRates];
        PowerOn();
    }

    /// <summary>The output level, 0 to 127, which always goes to the mixer.</summary>
    public int Level => _level;

    /// <summary>The time between output changes in CPU cycles: the region's rate for the index in <c>$4010</c>.</summary>
    public int Period => _rates[_rateIndex];

    /// <summary>The sample's start, <c>$C000 + A x 64</c> for the <c>A</c> written to <c>$4012</c>.</summary>
    public int SampleAddress => _sampleAddress;

    /// <summary>The sample's length in bytes, <c>L x 16 + 1</c> for the <c>L</c> written to <c>$4013</c>.</summary>
    public int SampleLength => _sampleLength;

    /// <summary>The address of the next byte the reader will fetch.</summary>
    public int CurrentAddress => _currentAddress;

    /// <summary>The bytes of the sample still to fetch; over 0 is what <c>$4015</c> bit 4 shows.</summary>
    public int BytesRemaining => _bytesRemaining;

    /// <summary>The DMC's IRQ flag: set when a sample ends with the IRQ enabled and no loop; it holds the IRQ line.</summary>
    public bool IrqFlag => _irqFlag;

    /// <summary>True while the reader wants a byte: the buffer is empty and bytes remain.</summary>
    internal bool FetchWanted => !_bufferFull && _bytesRemaining > 0;

    /// <summary>True when the reader wants a byte and the bus may halt the CPU for it in <paramref name="cycle"/>.</summary>
    internal bool WantsHalt(long cycle) => !_bufferFull && _bytesRemaining > 0 && cycle >= _fetchFrom;

    /// <summary>The address the bus's DMA reads.</summary>
    internal ushort FetchAddress => (ushort)_currentAddress;

    internal void PowerOn()
    {
        _rateIndex = 0;
        _timer = Reload(0);
        _level = 0;
        _irqEnabled = false;
        _loop = false;
        _irqFlag = false;
        _sampleAddress = 0xC000;
        _sampleLength = 1;
        _currentAddress = 0xC000;
        _bytesRemaining = 0;
        _buffer = 0;
        _bufferFull = false;
        _fetchFrom = 0;
        _shift = 0;
        _bitsRemaining = 8;
        _silent = true;
    }

    // apu.md 13: after a reset the level keeps bit 0; $4015 is written 0 by the unit.
    internal void Reset()
    {
        _level &= 1;
    }

    internal void Write(int register, byte value)
    {
        switch (register)
        {
            case 0:
                _irqEnabled = (value & 0x80) != 0;
                _loop = (value & 0x40) != 0;
                _rateIndex = value & 0x0F;
                if (!_irqEnabled)
                {
                    _irqFlag = false;
                }

                break;
            case 1:
                _level = value & 0x7F;
                break;
            case 2:
                _sampleAddress = 0xC000 + (value * 64);
                break;
            default:
                _sampleLength = (value * 16) + 1;
                break;
        }
    }

    // A $4015 write in cycle writeCycle (apu.md 9): D = 0 stops the sample, D = 1 starts it only
    // if no bytes remain, and either way the IRQ flag clears. A start with the buffer empty is a
    // load: its fetch may halt on the get of the second APU cycle after the write.
    internal void WriteEnable(bool enabled, long writeCycle)
    {
        _irqFlag = false;
        if (!enabled)
        {
            _bytesRemaining = 0;
            return;
        }

        if (_bytesRemaining == 0)
        {
            Restart();
            if (!_bufferFull)
            {
                _fetchFrom = writeCycle + ((writeCycle & 1) != 0 ? 3 : 4);
            }
        }
    }

    /// <summary>The byte the bus's DMA read: into the buffer, and the reader moves on.</summary>
    internal void CompleteFetch(byte value)
    {
        _buffer = value;
        _bufferFull = true;
        _currentAddress = _currentAddress == 0xFFFF ? 0x8000 : _currentAddress + 1;
        if (--_bytesRemaining == 0)
        {
            if (_loop)
            {
                Restart();
            }
            else if (_irqEnabled)
            {
                _irqFlag = true;
            }
        }
    }

    // One APU cycle, in the CPU cycle numbered cycle. The rates are in CPU cycles, all even, so the
    // timer, a divider of period reload + 1 APU cycles, reloads with half the rate less one.
    // Returns true when the output unit was clocked, which may change the level.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ClockTimer(long cycle)
    {
        if (_timer > 0)
        {
            _timer--;
            return false;
        }

        return ClockOutput(cycle);
    }

    // The timer passed 0: it reloads and the output unit is clocked. Returns true.
    private bool ClockOutput(long cycle)
    {
        _timer = Reload(_rateIndex);
        if (!_silent)
        {
            if ((_shift & 1) != 0)
            {
                if (_level <= 125)
                {
                    _level += 2;
                }
            }
            else if (_level >= 2)
            {
                _level -= 2;
            }
        }

        _shift >>= 1;
        if (--_bitsRemaining > 0)
        {
            return true;
        }

        // The output cycle ends: the next byte, if there is one, moves in, and the reader is free
        // to fetch again, from the next put.
        _bitsRemaining = 8;
        if (!_bufferFull)
        {
            _silent = true;
            return true;
        }

        _silent = false;
        _shift = _buffer;
        _bufferFull = false;
        _fetchFrom = cycle + 2;
        return true;
    }

    private void Restart()
    {
        _currentAddress = _sampleAddress;
        _bytesRemaining = _sampleLength;
    }

    private int Reload(int index) => (_rates[index] / 2) - 1;

    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Skip(nameof(_rates), StateReport.Fixed);
        sink.Add(nameof(_rateIndex), _rateIndex);
        sink.Add(nameof(_timer), _timer);
        sink.Add(nameof(_level), _level);
        sink.Add(nameof(_irqEnabled), _irqEnabled);
        sink.Add(nameof(_loop), _loop);
        sink.Add(nameof(_irqFlag), _irqFlag);
        sink.Add(nameof(_sampleAddress), _sampleAddress);
        sink.Add(nameof(_sampleLength), _sampleLength);
        sink.Add(nameof(_currentAddress), _currentAddress);
        sink.Add(nameof(_bytesRemaining), _bytesRemaining);
        sink.Add(nameof(_buffer), _buffer);
        sink.Add(nameof(_bufferFull), _bufferFull);
        sink.Add(nameof(_fetchFrom), _fetchFrom);
        sink.Add(nameof(_shift), _shift);
        sink.Add(nameof(_bitsRemaining), _bitsRemaining);
        sink.Add(nameof(_silent), _silent);
    }
}
