using System.Runtime.CompilerServices;

namespace Dbhq.Machines.Nes;

/// <summary>
/// The sound unit of the 2A03 and 2A07: two pulses, the triangle, the noise, the DMC and the frame
/// counter, one <see cref="Tick"/> a CPU cycle, and the mixer. Built from
/// <c>docs/nes/facts/apu.md</c>. The DMC's sample fetches are DMA, which the bus runs
/// (<see cref="NesBus"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The clocks.</b> The triangle's timer counts CPU cycles; the pulses', the noise's and the
/// DMC's count APU cycles, one in two CPU cycles (apu.md 1). The model clocks them on the odd
/// cycles, which with the bus's count from power on are the puts, the second half of an APU cycle;
/// which half is which is a choice, the same one the bus makes for its DMA (even cycles are
/// gets).
/// </para>
/// <para>
/// <b>The frame counter</b> (apu.md 10) counts CPU cycles from its last reset and acts at the
/// region's six steps (<see cref="Region.FrameCounterFourStep"/> and
/// <see cref="Region.FrameCounterFiveStep"/>). In 4-step mode: a quarter frame, a quarter and a
/// half, a quarter, the IRQ flag, a quarter and a half with the flag, the flag and the wrap. In
/// 5-step mode: the same three, nothing, a quarter and a half, the wrap. The wrap's cycle is the
/// next sequence's cycle 0. A <c>$4017</c> write resets the sequence 3 cycles later from an odd
/// (put) cycle and 4 from an even (get) one, so a reset always lands on a get and the steps fall
/// on the puts the sheet's table names; with bit 7 set the reset brings a quarter and a half frame
/// at once. The IRQ inhibit bit acts at the write.
/// </para>
/// <para>
/// <b>Inside a cycle</b> the bus ticks the unit before the CPU's access, so a flag or a clock the
/// unit makes in a cycle is seen by a read in that cycle, and a write in a cycle comes after its
/// tick. The fork's <c>apu_test</c> and <c>pal_apu_tests</c> pass with this order.
/// </para>
/// <para>
/// <b>Speed.</b> The tick does a compare for the frame counter, a count for the triangle and, on
/// odd cycles, a count for each pulse, the noise and the DMC; nothing else happens until a timer or
/// a step comes round, and nothing allocates. <see cref="Output"/> is a field, worked out again
/// through the mixer's tables (<see cref="ApuMixer"/>) only when something feeding it moved.
/// </para>
/// </remarks>
public sealed class Apu : IReportsState
{
    // Which actions each of the six steps takes, by mode (apu.md 10, ruling I).
    private const int Quarter = 1;
    private const int Half = 2;
    private const int SetIrq = 4;
    private const int Wrap = 8;

    private static readonly int[] FourStepActions =
        [Quarter, Quarter | Half, Quarter, SetIrq, Quarter | Half | SetIrq, SetIrq | Wrap];

    private static readonly int[] FiveStepActions =
        [Quarter, Quarter | Half, Quarter, 0, Quarter | Half, Wrap];

    private readonly int[] _fourStep;
    private readonly int[] _fiveStep;

    private long _cycles;

    // The mixed level as last worked out, and whether something may have changed it since.
    private double _output;
    private double _pulseMix;
    private double _tndMix;
    private bool _mixStale = true;

    // The frame counter.
    private bool _fiveStepMode;
    private bool _irqInhibit;
    private bool _frameIrq;
    private int[] _steps;
    private int[] _actions;
    private int _stepIndex;
    private int _frameCycle;

    // _steps[_stepIndex], the cycle of the next step, kept so the tick reads no array.
    private int _nextStep;
    private int _resetIn;

    // The mode last written to $4017, which takes effect at the reset it schedules.
    private bool _pendingFiveStepMode;

    // A length register or halt bit was written in this cycle: the next tick's clock sees it.
    private bool _lengthWritten;

    /// <summary>A sound unit for <paramref name="region"/>, powered on.</summary>
    public Apu(Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        Region = region;
        _fourStep = [.. region.FrameCounterFourStep];
        _fiveStep = [.. region.FrameCounterFiveStep];
        _steps = _fourStep;
        _actions = FourStepActions;
        Pulse1 = new PulseChannel(onesComplement: true);
        Pulse2 = new PulseChannel(onesComplement: false);
        Triangle = new TriangleChannel();
        Noise = new NoiseChannel(region);
        Dmc = new DmcChannel(region);
        PowerOn();
    }

    /// <summary>The region whose frame counter steps and noise periods this unit uses.</summary>
    public Region Region { get; }

    /// <summary>Pulse 1, at <c>$4000</c> to <c>$4003</c>; its sweep negates in ones' complement.</summary>
    public PulseChannel Pulse1 { get; }

    /// <summary>Pulse 2, at <c>$4004</c> to <c>$4007</c>; its sweep negates in two's complement.</summary>
    public PulseChannel Pulse2 { get; }

    /// <summary>The triangle, at <c>$4008</c> to <c>$400B</c>.</summary>
    public TriangleChannel Triangle { get; }

    /// <summary>The noise, at <c>$400C</c> to <c>$400F</c>.</summary>
    public NoiseChannel Noise { get; }

    /// <summary>The DMC, at <c>$4010</c> to <c>$4013</c>.</summary>
    public DmcChannel Dmc { get; }

    /// <summary>CPU cycles ticked since power on; its parity tells a get (even) from a put (odd).</summary>
    public long Cycles => _cycles;

    /// <summary>The IRQ line the unit holds: the frame counter's flag or the DMC's.</summary>
    public bool Irq => _frameIrq || Dmc.IrqFlag;

    /// <summary>
    /// The mixed level, 0 to 1, from the sheet's non-linear formulas (apu.md 11) through the
    /// mixer's tables, as it stands after the last tick or write. The bus reads it once a cycle for
    /// the sample buffer.
    /// </summary>
    /// <remarks>
    /// It is worked out only when something that feeds it may have changed: a timer that moved a
    /// channel's sequence (and then only the group it is in), a frame counter step, a register
    /// write. Otherwise it is the value of the cycle before, so reading it costs nothing.
    /// </remarks>
    public double Output
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_mixStale)
            {
                Remix();
            }

            return _output;
        }
    }

    /// <summary>
    /// Power on (apu.md 13): every register 0, the channels disabled, the noise register 1, the
    /// frame IRQ flag clear, and the frame counter as if <c>$00</c> had been written to
    /// <c>$4017</c> ten cycles before the first instruction.
    /// </summary>
    public void PowerOn()
    {
        _cycles = 0;
        _mixStale = true;
        Pulse1.PowerOn();
        Pulse2.PowerOn();
        Triangle.PowerOn();
        Noise.PowerOn();
        Dmc.PowerOn();
        _lengthWritten = false;
        _fiveStepMode = false;
        _irqInhibit = false;
        _frameIrq = false;
        _steps = _fourStep;
        _actions = FourStepActions;
        _stepIndex = 0;
        _frameCycle = 0;
        _nextStep = _steps[0];
        ScheduleResetAsIfWrittenBeforeTheFirstInstruction(fiveStep: false);
    }

    /// <summary>
    /// The reset button (apu.md 13 and the fork's <c>apu_reset/readme.txt</c>): the channels are
    /// disabled, the triangle goes back to step 0, the DMC's level keeps bit 0, the frame IRQ flag
    /// clears, and the last <c>$4017</c> mode is written again ten cycles before the first
    /// instruction. The other registers keep their values.
    /// </summary>
    public void Reset()
    {
        _mixStale = true;
        WriteStatus(0);
        Triangle.Reset();
        Dmc.Reset();
        _frameIrq = false;
        ScheduleResetAsIfWrittenBeforeTheFirstInstruction(_pendingFiveStepMode);
    }

    /// <summary>One CPU cycle.</summary>
    public void Tick()
    {
        _cycles++;

        if (_resetIn > 0 && --_resetIn == 0)
        {
            ResetSequence();
        }
        else if (++_frameCycle == _nextStep)
        {
            Step();
        }

        bool tnd = Triangle.ClockTimer();
        bool pulses = false;
        if ((_cycles & 1) != 0)
        {
            pulses = Pulse1.ClockTimer() | Pulse2.ClockTimer();
            tnd |= Noise.ClockTimer() | Dmc.ClockTimer(_cycles);
        }

        if (_mixStale)
        {
            Remix();
        }
        else
        {
            if (pulses)
            {
                _pulseMix = ApuMixer.Pulse(Pulse1.Output, Pulse2.Output);
            }

            if (tnd)
            {
                _tndMix = ApuMixer.Tnd(Triangle.Output, Noise.Output, Dmc.Level);
            }

            if (pulses | tnd)
            {
                _output = _pulseMix + _tndMix;
            }
        }

        if (_lengthWritten)
        {
            _lengthWritten = false;
            Pulse1.Length.EndCycle();
            Pulse2.Length.EndCycle();
            Triangle.Length.EndCycle();
            Noise.Length.EndCycle();
        }
    }

    /// <summary>
    /// A read of <c>$4015</c> (apu.md 9): bits 0 to 3 are the length counters over 0, bit 4 the
    /// DMC's bytes remaining over 0, bit 6 the frame IRQ flag, which the read clears, and bit 7 the
    /// DMC's IRQ flag, which it does not. Bit 5 is the bus's.
    /// </summary>
    public byte ReadStatus()
    {
        byte status = PeekStatus();
        _frameIrq = false;
        return status;
    }

    /// <summary>What a read of <c>$4015</c> would give, with no side effect.</summary>
    public byte PeekStatus()
    {
        int status = (Pulse1.LengthCounter > 0 ? 0x01 : 0)
            | (Pulse2.LengthCounter > 0 ? 0x02 : 0)
            | (Triangle.LengthCounter > 0 ? 0x04 : 0)
            | (Noise.LengthCounter > 0 ? 0x08 : 0)
            | (Dmc.BytesRemaining > 0 ? 0x10 : 0)
            | (_frameIrq ? 0x40 : 0)
            | (Dmc.IrqFlag ? 0x80 : 0);
        return (byte)status;
    }

    /// <summary>
    /// A write to <c>$4000</c> to <c>$4017</c>, as register <c>0</c> to <c>0x17</c>. <c>$4014</c>
    /// and <c>$4016</c> are not the unit's and are ignored.
    /// </summary>
    public void Write(int register, byte value)
    {
        _mixStale = true;
        switch (register)
        {
            case < 0x04:
                Pulse1.Write(register, value);
                _lengthWritten |= register is 0 or 3;
                break;
            case < 0x08:
                Pulse2.Write(register - 0x04, value);
                _lengthWritten |= register is 0x04 or 0x07;
                break;
            case < 0x0C:
                Triangle.Write(register - 0x08, value);
                _lengthWritten |= register is 0x08 or 0x0B;
                break;
            case < 0x10:
                Noise.Write(register - 0x0C, value);
                _lengthWritten |= register is 0x0C or 0x0F;
                break;
            case < 0x14:
                Dmc.Write(register - 0x10, value);
                break;
            case 0x15:
                WriteStatus(value);
                break;
            case 0x17:
                WriteFrameCounter(value, _cycles);
                break;
        }
    }

    private void WriteStatus(byte value)
    {
        Pulse1.Length.SetEnabled((value & 0x01) != 0);
        Pulse2.Length.SetEnabled((value & 0x02) != 0);
        Triangle.Length.SetEnabled((value & 0x04) != 0);
        Noise.Length.SetEnabled((value & 0x08) != 0);
        Dmc.WriteEnable((value & 0x10) != 0, _cycles);
    }

    // A $4017 write made in cycle writeCycle: the inhibit acts now, the mode at the reset, 3 cycles
    // after an odd (put) cycle and 4 after an even (get) one.
    private void WriteFrameCounter(byte value, long writeCycle)
    {
        _irqInhibit = (value & 0x40) != 0;
        if (_irqInhibit)
        {
            _frameIrq = false;
        }

        _pendingFiveStepMode = (value & 0x80) != 0;
        long resetAt = writeCycle + ((writeCycle & 1) != 0 ? 3 : 4);
        _resetIn = (int)(resetAt - _cycles);
    }

    // apu.md 10: power and reset act as if $4017 was written 10 cycles before the first
    // instruction, which follows the CPU's 7 reset cycles: the write is 2 cycles before now.
    private void ScheduleResetAsIfWrittenBeforeTheFirstInstruction(bool fiveStep)
    {
        WriteFrameCounter((byte)((fiveStep ? 0x80 : 0) | (_irqInhibit ? 0x40 : 0)), _cycles - 2);
    }

    private void ResetSequence()
    {
        _mixStale = true;
        _fiveStepMode = _pendingFiveStepMode;
        _steps = _fiveStepMode ? _fiveStep : _fourStep;
        _actions = _fiveStepMode ? FiveStepActions : FourStepActions;
        _stepIndex = 0;
        _frameCycle = 0;
        _nextStep = _steps[0];
        if (_fiveStepMode)
        {
            ClockQuarterFrame();
            ClockHalfFrame();
        }
    }

    private void Step()
    {
        _mixStale = true;
        int actions = _actions[_stepIndex];
        if ((actions & Quarter) != 0)
        {
            ClockQuarterFrame();
        }

        if ((actions & Half) != 0)
        {
            ClockHalfFrame();
        }

        if ((actions & SetIrq) != 0 && !_irqInhibit)
        {
            _frameIrq = true;
        }

        if ((actions & Wrap) != 0)
        {
            _stepIndex = 0;
            _frameCycle = 0;
        }
        else
        {
            _stepIndex++;
        }

        _nextStep = _steps[_stepIndex];
    }

    private void Remix()
    {
        _mixStale = false;
        _pulseMix = ApuMixer.Pulse(Pulse1.Output, Pulse2.Output);
        _tndMix = ApuMixer.Tnd(Triangle.Output, Noise.Output, Dmc.Level);
        _output = _pulseMix + _tndMix;
    }

    private void ClockQuarterFrame()
    {
        Pulse1.ClockQuarter();
        Pulse2.ClockQuarter();
        Triangle.ClockQuarter();
        Noise.ClockQuarter();
    }

    private void ClockHalfFrame()
    {
        Pulse1.ClockHalf();
        Pulse2.ClockHalf();
        Triangle.ClockHalf();
        Noise.ClockHalf();
    }

    /// <summary>
    /// Every field of the unit, as <see cref="IReportsState"/> asks: the cycle count, the frame
    /// counter, the length write flag, the channels, and the mixer. The mixer's three values are
    /// reported as <see cref="Output"/> would make them, worked out here when they are stale and
    /// nothing stored, so a report changes nothing; whether they were stale is a cache's own
    /// bookkeeping, which a batch may leave either way with the same values.
    /// </summary>
    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Skip(nameof(_fourStep), StateReport.Fixed);
        sink.Skip(nameof(_fiveStep), StateReport.Fixed);
        sink.Skip(nameof(Region), StateReport.Fixed);
        sink.Add(nameof(_cycles), _cycles);

        double pulseMix = _mixStale ? ApuMixer.Pulse(Pulse1.Output, Pulse2.Output) : _pulseMix;
        double tndMix = _mixStale ? ApuMixer.Tnd(Triangle.Output, Noise.Output, Dmc.Level) : _tndMix;
        sink.Add(nameof(_output), _mixStale ? pulseMix + tndMix : _output);
        sink.Add(nameof(_pulseMix), pulseMix);
        sink.Add(nameof(_tndMix), tndMix);
        sink.Skip(nameof(_mixStale), "whether the three values above were stale: they are reported as they would be worked out");

        sink.Add(nameof(_fiveStepMode), _fiveStepMode);
        sink.Add(nameof(_irqInhibit), _irqInhibit);
        sink.Add(nameof(_frameIrq), _frameIrq);
        sink.Add(nameof(_steps), _steps == _fiveStep ? 5L : 4L);
        sink.Add(nameof(_actions), _actions == FiveStepActions ? 5L : 4L);
        sink.Add(nameof(_stepIndex), _stepIndex);
        sink.Add(nameof(_frameCycle), _frameCycle);
        sink.Add(nameof(_nextStep), _nextStep);
        sink.Add(nameof(_resetIn), _resetIn);
        sink.Add(nameof(_pendingFiveStepMode), _pendingFiveStepMode);
        sink.Add(nameof(_lengthWritten), _lengthWritten);
        sink.Add(nameof(Pulse1), Pulse1);
        sink.Add(nameof(Pulse2), Pulse2);
        sink.Add(nameof(Triangle), Triangle);
        sink.Add(nameof(Noise), Noise);
        sink.Add(nameof(Dmc), Dmc);
    }
}
