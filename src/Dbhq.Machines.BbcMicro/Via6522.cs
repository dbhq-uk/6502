namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The 6522 Versatile Interface Adapter, one 1 MHz cycle at a time. The BBC Micro has two:
/// the system VIA and the user VIA, which wrap this class.
/// </summary>
/// <remarks>
/// <para>
/// Built from the fact sheet <c>docs/bbc-micro/facts/via.md</c> section 1 and nothing
/// else. The sheet's notation is used here: W is the cycle in which a write happens, and
/// W+k the k-th cycle after it.
/// </para>
/// <para>
/// <b>How to drive it.</b> Call <see cref="Tick"/> at the start of every 1 MHz cycle, then
/// make at most one <see cref="Read"/> or <see cref="Write"/> in that cycle. A read sees
/// everything that <see cref="Tick"/> changed, which stands for the chip sampling a read at
/// the end of the cycle (section 1.0).
/// </para>
/// <para>
/// <b>The visibility rule (section 1.9).</b> A timer or the shift register sets its flag
/// in the middle of cycle E, so a read in E sees it set and a read in E-1 does not. Here
/// the flag is set in the <see cref="Tick"/> that starts E, and <see cref="Irq"/> asserts
/// from E too. A clearing access in that same cycle E does not clear the flag, because on
/// the real chip the set is held for the whole cycle; instead <see cref="Irq"/> asserts
/// from the start of E+1. This is the coincident acknowledge that stardot measured on real
/// Model Bs and Masters with Rockwell parts (section 1.9). The sheet gives it for timer 1;
/// this class applies it to every flag set in <see cref="Tick"/> (both timers and the shift
/// register), and not to the control lines, whose edges arrive between ticks.
/// </para>
/// <para>
/// <b>Timers (sections 1.4, 1.5).</b> A T1C-H or T2C-H write in W loads the counter with N,
/// which it holds through W+1. It then counts down one a cycle, reaches 0 in W+N+1 and
/// &amp;FFFF in W+N+2, which is when the flag rises: N+1.5 cycles after the end of the write.
/// Timer 1 reloads from the latch in the next cycle, so in free-run it flags every N+2
/// cycles. Timer 2 never reloads.
/// </para>
/// </remarks>
public class Via6522
{
    /// <summary>
    /// Cycles from the SR read (or write) that starts a shift in mode 010 to the cycle in which
    /// IFR2 rises: a read that many cycles after the starting one sees the flag.
    /// </summary>
    /// <remarks>
    /// <c>via.md</c> section 1.8 measured "about 19" off the WDC datasheet's Figure 2-7 with a
    /// ruler, and marks it [guessing - verify]: it is a drawing of the CMOS part, not a
    /// measurement of the NMOS one. Its only user on the BBC is DFS 1.20, which polls IFR2,
    /// so all it needs is for the flag to appear; the number decides how long it waits.
    /// </remarks>
    public const int ShiftMode2Cycles = 19;

    private const byte FlagCa2 = 0x01, FlagCa1 = 0x02, FlagSr = 0x04, FlagCb2 = 0x08;
    private const byte FlagCb1 = 0x10, FlagT2 = 0x20, FlagT1 = 0x40;

    private byte _ora, _orb, _ddra, _ddrb, _acr, _pcr, _ifr, _ier, _sr;
    private byte _latchedA, _latchedB;

    // Timer 1.
    private ushort _t1Counter, _t1Latch;
    private bool _t1Hold;          // the cycle after a T1C-H write, when the counter holds N
    private bool _t1Reload;        // the cycle after &FFFF, when the latch is copied in
    private bool _t1Armed;         // one-shot: a T1C-H write arms one flag
    private bool _pb7 = true;      // the PB7 flip-flop; its state before any T1C-H write is not known

    // Timer 2.
    private ushort _t2Counter;
    private byte _t2LatchLow;
    private bool _t2Hold;
    private bool _t2Armed;
    private bool _pb6WasHigh = true;

    // The shift register: cycles left until IFR2 rises, 0 when not shifting.
    private int _srCyclesLeft;

    // Flags set by the Tick that started this cycle, and those of them a clearing access hit.
    private byte _justSet, _collided;

    // The control lines as last seen, and what CA2 and CB2 drive in handshake and pulse modes.
    private bool _ca1, _ca2, _cb1, _cb2;
    private bool _ca2Handshake = true, _cb2Handshake = true;
    private int _ca2Pulse, _cb2Pulse;

    public Via6522()
    {
        Reset();
    }

    /// <summary>Raised after a write to ORA (register 1 or F) or DDRA.</summary>
    public event Action? PortAWritten;

    /// <summary>Raised after a write to ORB or DDRB.</summary>
    public event Action? PortBWritten;

    /// <summary>
    /// What the outside drives on PA7..0. Inputs with nothing attached read 1 on a real
    /// Model B (section 1.2), so it starts at &amp;FF.
    /// </summary>
    public byte PortAInput { get; set; } = 0xFF;

    /// <summary>What the outside drives on PB7..0; starts at &amp;FF like port A.</summary>
    public byte PortBInput { get; set; } = 0xFF;

    /// <summary>The level on each port A pin: ORA where DDRA is 1, the outside where it is 0.</summary>
    public byte PortAPins => (byte)((_ora & _ddra) | (PortAInput & ~_ddra));

    /// <summary>
    /// The level on each port B pin, as port A; with ACR7 set PB7 is the timer 1 output,
    /// whatever DDRB7 says (section 1.3).
    /// </summary>
    public byte PortBPins
    {
        get
        {
            byte pins = (byte)((_orb & _ddrb) | (PortBInput & ~_ddrb));
            return TimerDrivesPb7 ? (byte)((pins & 0x7F) | (_pb7 ? 0x80 : 0)) : pins;
        }
    }

    /// <summary>
    /// The IRQ_N line, true when asserted: some flag in IFR bits 0 to 6 is set and enabled in
    /// IER, except a flag a clearing access hit in the cycle it rose, which waits a cycle.
    /// </summary>
    public bool Irq => (_ifr & ~_collided & _ier & 0x7F) != 0;

    /// <summary>The level CA2 drives when the PCR makes it an output; true when it is an input.</summary>
    public bool Ca2Out => ControlOut((_pcr >> 1) & 7, _ca2Handshake);

    /// <summary>The level CB2 drives when the PCR makes it an output; true when it is an input.</summary>
    public bool Cb2Out => ControlOut((_pcr >> 5) & 7, _cb2Handshake);

    private bool TimerDrivesPb7 => (_acr & 0x80) != 0;

    private bool Timer1FreeRuns => (_acr & 0x40) != 0;

    private bool Timer2CountsPulses => (_acr & 0x20) != 0;

    private int ShiftMode => (_acr >> 2) & 7;

    private int Ca2Mode => (_pcr >> 1) & 7;

    private int Cb2Mode => (_pcr >> 5) & 7;

    /// <summary>
    /// What /RES does (section 1.2): ORA, ORB, DDRA, DDRB, ACR, PCR, IFR and IER become 0. The
    /// timers' counters and latches and the shift register keep their values. "T1, T2, SR and
    /// interrupt logic disabled" is taken to mean that no one-shot is left armed and no shift is
    /// running [inferring from the same row].
    /// </summary>
    public void Reset()
    {
        _ora = _orb = _ddra = _ddrb = _acr = _pcr = _ifr = _ier = 0;
        _justSet = _collided = 0;
        _t1Armed = _t2Armed = false;
        _srCyclesLeft = 0;
        _ca2Handshake = _cb2Handshake = true;
        _ca2Pulse = _cb2Pulse = 0;
    }

    /// <summary>
    /// One 1 MHz cycle. Call it at the start of the cycle, before that cycle's access. A
    /// subclass that wires the chip into a machine adds what the outside does in the cycle, after
    /// calling this.
    /// </summary>
    public virtual void Tick()
    {
        _justSet = 0;
        _collided = 0;

        TickTimer1();
        TickTimer2();
        TickShiftRegister();
        _ca2Handshake = TickPulse(ref _ca2Pulse, _ca2Handshake);
        _cb2Handshake = TickPulse(ref _cb2Pulse, _cb2Handshake);
    }

    /// <summary>Reads register 0 to 15 in the current cycle, with its side effects.</summary>
    public byte Read(int register)
    {
        byte value = Peek(register);
        switch (register & 0xF)
        {
            case 0x0:
                ClearFlags((byte)(FlagCb1 | (Cb2Independent ? 0 : FlagCb2)));
                break;
            case 0x1:
                ClearFlags((byte)(FlagCa1 | (Ca2Independent ? 0 : FlagCa2)));
                StartCa2Handshake();
                break;
            case 0x4:
                ClearFlags(FlagT1);
                break;
            case 0x8:
                ClearFlags(FlagT2);
                break;
            case 0xA:
                ClearFlags(FlagSr);
                StartShift();
                break;
        }
        return value;
    }

    /// <summary>
    /// What a read of register 0 to 15 would return, with none of its side effects: for tests
    /// and debuggers, never for the CPU.
    /// </summary>
    public byte Peek(int register) => (register & 0xF) switch
    {
        0x0 => ReadPortB(),
        0x2 => _ddrb,
        0x3 => _ddra,
        0x4 => (byte)_t1Counter,
        0x5 => (byte)(_t1Counter >> 8),
        0x6 => (byte)_t1Latch,
        0x7 => (byte)(_t1Latch >> 8),
        0x8 => (byte)_t2Counter,
        0x9 => (byte)(_t2Counter >> 8),
        0xA => _sr,
        0xB => _acr,
        0xC => _pcr,
        0xD => (byte)(_ifr | ((_ifr & _ier & 0x7F) != 0 ? 0x80 : 0)),
        0xE => (byte)(_ier | 0x80),
        _ => ReadPortA(), // registers 1 and F
    };

    /// <summary>Writes register 0 to 15 in the current cycle, with its side effects.</summary>
    public void Write(int register, byte value)
    {
        switch (register & 0xF)
        {
            case 0x0:
                _orb = value;
                ClearFlags((byte)(FlagCb1 | (Cb2Independent ? 0 : FlagCb2)));
                StartCb2Handshake();
                PortBWritten?.Invoke();
                break;
            case 0x1:
                _ora = value;
                ClearFlags((byte)(FlagCa1 | (Ca2Independent ? 0 : FlagCa2)));
                StartCa2Handshake();
                PortAWritten?.Invoke();
                break;
            case 0x2:
                _ddrb = value;
                PortBWritten?.Invoke();
                break;
            case 0x3:
                _ddra = value;
                PortAWritten?.Invoke();
                break;
            case 0x4:
            case 0x6:
                _t1Latch = (ushort)((_t1Latch & 0xFF00) | value);
                break;
            case 0x5:
                _t1Latch = (ushort)((_t1Latch & 0x00FF) | (value << 8));
                _t1Counter = _t1Latch;
                _t1Hold = true;
                _t1Reload = false;
                _t1Armed = true;
                _pb7 = false;
                ClearFlags(FlagT1);
                break;
            case 0x7:
                // Clears IFR6 even in one-shot: MOS, WDC and a real Model B, against the
                // Rockwell sheet and the AUG (section 1.1 and the disagreement table).
                _t1Latch = (ushort)((_t1Latch & 0x00FF) | (value << 8));
                ClearFlags(FlagT1);
                break;
            case 0x8:
                _t2LatchLow = value;
                break;
            case 0x9:
                _t2Counter = (ushort)((value << 8) | _t2LatchLow);
                _t2Hold = true;
                _t2Armed = true;
                ClearFlags(FlagT2);
                break;
            case 0xA:
                _sr = value;
                ClearFlags(FlagSr);
                StartShift();
                break;
            case 0xB:
                _acr = value;
                if (ShiftMode != 2)
                {
                    _srCyclesLeft = 0;
                }
                break;
            case 0xC:
                _pcr = value;
                break;
            case 0xD:
                ClearFlags((byte)(value & 0x7F));
                break;
            case 0xE:
                if ((value & 0x80) != 0)
                {
                    _ier |= (byte)(value & 0x7F);
                }
                else
                {
                    _ier &= (byte)~value;
                }
                break;
            default:
                // Register F: as ORA, without the handshake or the flag clearing (section 1.1).
                _ora = value;
                PortAWritten?.Invoke();
                break;
        }
    }

    /// <summary>The level on CA1. Sets IFR1 on the edge PCR bit 0 picks: 0 negative, 1 positive.</summary>
    public void SetCa1(bool level)
    {
        if (level == _ca1)
        {
            return;
        }
        _ca1 = level;
        if (level != ((_pcr & 0x01) != 0))
        {
            return;
        }

        SetFlag(FlagCa1);
        if ((_acr & 0x01) != 0)
        {
            _latchedA = PortAPins;
        }
        if (Ca2Mode == 4)
        {
            _ca2Handshake = true; // handshake: CA2 back high on the CA1 active edge
        }
    }

    /// <summary>The level on CA2. In the input modes sets IFR0 on the edge PCR bits 3 to 1 pick.</summary>
    public void SetCa2(bool level)
    {
        if (level == _ca2)
        {
            return;
        }
        _ca2 = level;
        if (InputEdgeIsActive(Ca2Mode, level))
        {
            SetFlag(FlagCa2);
        }
    }

    /// <summary>The level on CB1. Sets IFR4 on the edge PCR bit 4 picks: 0 negative, 1 positive.</summary>
    public void SetCb1(bool level)
    {
        if (level == _cb1)
        {
            return;
        }
        _cb1 = level;
        if (level != ((_pcr & 0x10) != 0))
        {
            return;
        }

        SetFlag(FlagCb1);
        if ((_acr & 0x02) != 0)
        {
            _latchedB = PortBPins;
        }
        if (Cb2Mode == 4)
        {
            _cb2Handshake = true;
        }
    }

    /// <summary>The level on CB2. In the input modes sets IFR3 on the edge PCR bits 7 to 5 pick.</summary>
    public void SetCb2(bool level)
    {
        if (level == _cb2)
        {
            return;
        }
        _cb2 = level;
        if (InputEdgeIsActive(Cb2Mode, level))
        {
            SetFlag(FlagCb2);
        }
    }

    private bool Ca2Independent => Ca2Mode is 1 or 3;

    private bool Cb2Independent => Cb2Mode is 1 or 3;

    /// <summary>
    /// CA2 and CB2 input modes (section 1.7): 000 negative edge, 001 independent negative,
    /// 010 positive edge, 011 independent positive. Modes 100 and up are outputs.
    /// </summary>
    private static bool InputEdgeIsActive(int mode, bool level) =>
        mode < 4 && level == ((mode & 2) != 0);

    /// <summary>Modes 100 and 101 drive the handshake level, 110 holds low, 111 holds high.</summary>
    private static bool ControlOut(int mode, bool handshake) => mode switch
    {
        4 or 5 => handshake,
        6 => false,
        _ => true,
    };

    /// <summary>
    /// IRA reads the pins (section 1.3), or what CA1's active edge latched when ACR0 is set.
    /// </summary>
    private byte ReadPortA() => (_acr & 0x01) != 0 ? _latchedA : PortAPins;

    /// <summary>
    /// IRB reads ORB for output bits and the pins for input bits (section 1.3), the pins as CB1
    /// latched them when ACR1 is set; with ACR7 set PB7 is the timer output either way.
    /// </summary>
    private byte ReadPortB()
    {
        byte inputs = (_acr & 0x02) != 0 ? _latchedB : PortBPins;
        byte value = (byte)((_orb & _ddrb) | (inputs & ~_ddrb));
        return TimerDrivesPb7 ? (byte)((value & 0x7F) | (_pb7 ? 0x80 : 0)) : value;
    }

    /// <summary>
    /// Handshake (100): CA2 goes low on an ORA read or write and back high on CA1's active
    /// edge. Pulse (101): CA2 goes low for one cycle after the access. The sheet gives these in
    /// words only (section 1.7); here the line goes low at the access and, in pulse mode, comes
    /// back at the start of the second cycle after it [inferring]. Nothing on the BBC uses
    /// either: the OS drives the printer strobe through the PCR by hand.
    /// </summary>
    private void StartCa2Handshake()
    {
        if (Ca2Mode is 4 or 5)
        {
            _ca2Handshake = false;
            _ca2Pulse = Ca2Mode == 5 ? 2 : 0;
        }
    }

    /// <summary>As <see cref="StartCa2Handshake"/>, for CB2, which fires on an ORB write only.</summary>
    private void StartCb2Handshake()
    {
        if (Cb2Mode is 4 or 5)
        {
            _cb2Handshake = false;
            _cb2Pulse = Cb2Mode == 5 ? 2 : 0;
        }
    }

    private static bool TickPulse(ref int pulse, bool level)
    {
        if (pulse == 0)
        {
            return level;
        }
        pulse--;
        return pulse == 0 || level;
    }

    private void TickTimer1()
    {
        if (_t1Hold)
        {
            _t1Hold = false;
            return;
        }
        if (_t1Reload)
        {
            // One cycle of &FFFF, then the latch (section 1.4). The sheet leaves open whether a
            // one-shot reloads too; this follows the datasheet's figure and the real-machine
            // report and reloads in both modes (docs/known-differences.md).
            _t1Reload = false;
            _t1Counter = _t1Latch;
            return;
        }
        if (_t1Counter != 0)
        {
            _t1Counter--;
            return;
        }

        _t1Counter = 0xFFFF;
        _t1Reload = true;
        if (Timer1FreeRuns)
        {
            _pb7 = !_pb7;
            SetFlagInTick(FlagT1);
        }
        else if (_t1Armed)
        {
            _pb7 = true;
            SetFlagInTick(FlagT1);
        }
        _t1Armed = false;
    }

    private void TickTimer2()
    {
        if (Timer2CountsPulses)
        {
            // Pulse counting (section 1.5, WDC 2.10): one count for each PB6 pulse that is low
            // at the start of a cycle, and IFR5 when the count reaches zero. Seeing a pulse as a
            // high-to-low change between two ticks is [inferring]; nothing on the BBC pulses PB6.
            bool high = (PortBPins & 0x40) != 0;
            if (!high && _pb6WasHigh)
            {
                _t2Counter--;
                if (_t2Counter == 0 && _t2Armed)
                {
                    _t2Armed = false;
                    SetFlagInTick(FlagT2);
                }
            }
            _pb6WasHigh = high;
            _t2Hold = false;
            return;
        }

        _pb6WasHigh = (PortBPins & 0x40) != 0;
        if (_t2Hold)
        {
            _t2Hold = false;
            return;
        }
        if (_t2Counter != 0)
        {
            _t2Counter--;
            return;
        }

        // Timer 2 does not reload: &FFFF, &FFFE and on (section 1.5).
        _t2Counter = 0xFFFF;
        if (_t2Armed)
        {
            _t2Armed = false;
            SetFlagInTick(FlagT2);
        }
    }

    /// <summary>
    /// Only mode 010, shift in under the system clock, is modelled, because it is the only one the
    /// BBC's ROMs use (section 1.8). Its eight bits come from CB2 and are all taken when the
    /// flag rises, not one per shift pulse. The other modes shift nothing and set no flag
    /// (docs/known-differences.md).
    /// </summary>
    private void TickShiftRegister()
    {
        if (_srCyclesLeft == 0 || --_srCyclesLeft != 0)
        {
            return;
        }

        for (int bit = 0; bit < 8; bit++)
        {
            _sr = (byte)((_sr << 1) | (_cb2 ? 1 : 0)); // bit 0 first, towards bit 7 (WDC 2.12.3)
        }
        SetFlagInTick(FlagSr);
    }

    /// <summary>A read or a write of SR starts a shift in mode 010 (MOS datasheet, mode 010).</summary>
    private void StartShift()
    {
        if (ShiftMode == 2)
        {
            _srCyclesLeft = ShiftMode2Cycles;
        }
    }

    private void SetFlag(byte flag) => _ifr |= flag;

    private void SetFlagInTick(byte flag)
    {
        _ifr |= flag;
        _justSet |= flag;
    }

    /// <summary>
    /// Clears flags, except one that rose in this very cycle: that one stays set and its IRQ
    /// waits until the next cycle (section 1.9, coincident acknowledge).
    /// </summary>
    private void ClearFlags(byte flags)
    {
        _collided |= (byte)(flags & _justSet);
        _ifr &= (byte)~(flags & ~_justSet);
    }
}
