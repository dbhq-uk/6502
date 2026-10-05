namespace Dbhq.Machines.Electron;

/// <summary>
/// The Electron's ULA, so far its interrupt registers and the frame that times them (fact sheet
/// <c>ula.md</c> s1c, s5a, s5d and s6). It is lazy, as the BBC's chips are: it does nothing on
/// the cycle, and <see cref="NextEvent"/> says when it next has something to do. The bus compares
/// that one number against its clock on every access and calls <see cref="CatchUp"/> only when
/// the clock has reached it.
/// </summary>
/// <remarks>
/// <para>
/// A frame is 80,000 cycles from power on, two fields of unequal length (s5d). Four events fall
/// in it, always in this order: the odd field's clock interrupt, the odd field's display end, the
/// even field's clock interrupt and the even field's display end. The display end is six lines
/// earlier in modes 3 and 6, which show 250 lines and not 256, so its time depends on the
/// mode in force when it comes due. Writing the mode moves the pending event.
/// </para>
/// <para>
/// The status bits are set whether or not the interrupt is enabled; the enable gates only bit 0
/// and <see cref="Irq"/> (s6a). The ULA has no NMI source of its own.
/// </para>
/// </remarks>
public sealed class Ula
{
    // The frame, in 2 MHz cycles from power on (s5d).
    private const long FrameCycles = 80_000;
    private const long OddRtc = 12_670;
    private const long EvenRtc = 52_670;
    private const long OddDisplayEnd = 32_736;
    private const long EvenDisplayEnd = 72_672;

    // Modes 3 and 6 display 250 lines, not 256: six lines of 128 cycles earlier, 768 (s5d: 31,968 and 71,904).
    private const long ShortModeAdvance = 768;

    private const int EventsPerFrame = 4;
    private const int OddRtcEvent = 0;
    private const int OddDisplayEndEvent = 1;
    private const int EvenRtcEvent = 2;

    // Status bits (s6a). Bit 7 is always 1.
    private const int MasterIrqBit = 0x01;
    private const int PowerOnBit = 0x02;
    private const int DisplayEndBit = 0x04;
    private const int RealTimeClockBit = 0x08;
    private const int TransmitEmptyBit = 0x20;
    private const int SourceMask = 0x7C;

    // Interrupt clear bits of $FE05 (s6b). Bit 7 clears the NMI, which the ULA does not hold.
    private const int ClearDisplayEnd = 0x10;
    private const int ClearRealTimeClock = 0x20;
    private const int ClearHighTone = 0x40;
    private const int HighToneBit = 0x40;

    private int _enable;
    private int _sources;
    private bool _powerOn;
    private long _frameStart;
    private int _event;

    /// <summary>
    /// A ULA at power on. Its power-on state, where the sheet does not give it (s12 item 4): no
    /// interrupt enabled, transmit empty set (it is "normally set"), display mode 0.
    /// </summary>
    public Ula()
    {
        PowerOn();
        _enable = 0;
        _sources = TransmitEmptyBit;
        NextEvent = EventTime(_event);
    }

    /// <summary>
    /// The display mode, 0 to 6: bits 5 to 3 of $FE07, with a value of 7 acting as mode 4 (s5a).
    /// </summary>
    public int Mode { get; private set; }

    /// <summary>
    /// What a read of $FE00 gives (s6a): bit 7 always 1, bit 0 the master IRQ, bit 1 the power-on
    /// flag, bits 6 to 2 the sources. Reading this property changes nothing; a read of the
    /// register through <see cref="Read"/> also clears the power-on flag.
    /// </summary>
    public byte Status =>
        (byte)(0x80 | (_sources & SourceMask) | (_powerOn ? PowerOnBit : 0) | (Irq ? MasterIrqBit : 0));

    /// <summary>The CPU's IRQ line: an enabled source is set (s6a). Current as of the last catch-up.</summary>
    public bool Irq => (_sources & _enable) != 0;

    /// <summary>
    /// The next cycle at which something happens, which the bus compares against its clock on
    /// every access. It moves on at each catch-up and when the mode is written.
    /// </summary>
    internal long NextEvent { get; private set; }

    /// <summary>
    /// Sets the power-on flag, which the first read of $FE00 clears (s6a). BREAK does not call it:
    /// that is how the OS tells a BREAK from a power on, and the ULA has no reset of its own on
    /// BREAK (s12 item 5).
    /// </summary>
    public void PowerOn() => _powerOn = true;

    /// <summary>
    /// Raises the clock and display-end interrupts that have come due by <paramref name="cycle"/>,
    /// each at its own time and in order, with the mode in force now.
    /// </summary>
    internal void CatchUp(long cycle)
    {
        while (NextEvent <= cycle)
        {
            switch (_event)
            {
                case OddRtcEvent or EvenRtcEvent:
                    _sources |= RealTimeClockBit;
                    break;
                default:
                    _sources |= DisplayEndBit;
                    break;
            }

            if (++_event == EventsPerFrame)
            {
                _event = 0;
                _frameStart += FrameCycles;
            }

            NextEvent = EventTime(_event);
        }
    }

    /// <summary>A write to register 0 to 15; the bus has already mirrored the address.</summary>
    internal void Write(int register, byte value)
    {
        CheckRegister(register);
        switch (register)
        {
            case 0:
                // Bits 1, 0 and 7 have no effect (s1c).
                _enable = value & SourceMask;
                break;
            case 5:
                // Bits 7 to 4 clear interrupts; bits 3 to 0 are the ROM select, which the bus
                // handles from the same write (s2a, s6b). Bit 7 clears the NMI: nothing here.
                if ((value & ClearRealTimeClock) != 0)
                {
                    _sources &= ~RealTimeClockBit;
                }

                if ((value & ClearDisplayEnd) != 0)
                {
                    _sources &= ~DisplayEndBit;
                }

                if ((value & ClearHighTone) != 0)
                {
                    _sources &= ~HighToneBit;
                }

                break;
            case 7:
                int mode = (value >> 3) & 7;
                Mode = mode == 7 ? 4 : mode;
                NextEvent = EventTime(_event);
                break;
        }
    }

    /// <summary>
    /// A read of register 0 to 15. Only the status is readable so far; the bus answers a read of
    /// any other register itself (ula.md s1b), so asking for one here is a bug in the caller.
    /// </summary>
    internal byte Read(int register)
    {
        CheckRegister(register);
        if (register != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(register), register, "Only the status register is readable here.");
        }

        byte status = Status;
        _powerOn = false;
        return status;
    }

    /// <summary>Raises status bit 4, 5 or 6 (receive full, transmit empty, high tone): for the tape (s6a).</summary>
    internal void SetStatus(int bit) => _sources |= TapeBit(bit);

    /// <summary>Clears status bit 4, 5 or 6: for the tape (s6a).</summary>
    internal void ClearStatus(int bit) => _sources &= ~TapeBit(bit);

    private static int TapeBit(int bit)
    {
        if (bit is < 4 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(bit), bit, "The tape owns status bits 4, 5 and 6.");
        }

        return 1 << bit;
    }

    private static void CheckRegister(int register)
    {
        if ((uint)register > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(register), register, "The registers are 0 to 15.");
        }
    }

    /// <summary>The cycle at which event <paramref name="e"/> of the current frame falls, in the mode now in force.</summary>
    private long EventTime(int e)
    {
        long shortMode = Mode is 3 or 6 ? ShortModeAdvance : 0;
        return _frameStart + e switch
        {
            OddRtcEvent => OddRtc,
            OddDisplayEndEvent => OddDisplayEnd - shortMode,
            EvenRtcEvent => EvenRtc,
            _ => EvenDisplayEnd - shortMode,
        };
    }
}
