namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The MOS 6532 RIOT: 128 bytes of RAM, ports A and B with their data direction registers, an
/// interval timer and the PA7 edge detect. On the 2600 port A reads the joysticks and port B the
/// console switches, and the chip's IRQ pin is not connected.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/riot.md</c>, which rests on the 6532 data sheets of 1977
/// and 1981 and the suite's <c>riot/</c> test headers. The KIM-1's <c>Rriot6530</c> was read as a
/// guide to the shape; nothing is shared with it, by the spec's decision.
/// </para>
/// <para>
/// The chip has no clock of its own here. The bus calls <see cref="Tick"/> once at the start of
/// every CPU cycle, halted or not, and then makes that cycle's access, if it reaches the chip. So
/// the cycle of a timer write is the sheet's cycle 0, and the count first drops on cycle 1.
/// </para>
/// <para>
/// The bus decodes the chip select (A12 low, A7 high) and RS (A9). The register methods decode
/// A0 to A4 only, so any address in the I/O half may be passed whole, mirrors included.
/// </para>
/// </remarks>
public sealed class Riot6532 : IReportsState
{
    /// <summary>The RAM's size in bytes.</summary>
    public const int RamSize = 128;

    private readonly byte[] _ram = new byte[RamSize];

    private byte _ora;
    private byte _ddra;
    private byte _orb;
    private byte _ddrb;
    private byte _portAInput = 0xFF;

    private byte _count;
    private int _divider;
    private int _untilDecrement;
    private bool _freeRunning;
    private bool _timerFlag;
    private bool _timerFlagSetThisCycle;
    private bool _timerInterruptEnabled;

    private bool _edgeFlag;
    private bool _edgeInterruptEnabled;
    private bool _edgePositive;
    private bool _pa7;

    /// <summary>A chip at its power-on state (<see cref="PowerOn"/>).</summary>
    public Riot6532() => PowerOn();

    /// <summary>
    /// What the outside world puts on port A's pins: on the 2600, the joysticks, active low. A bit
    /// nothing pulls low is 1, through the chip's pull-ups (riot.md section 5). A change of PA7 here
    /// is an edge for the detector, as a change made by the program is.
    /// </summary>
    public byte PortAInput
    {
        get => _portAInput;
        set
        {
            _portAInput = value;
            WatchPa7();
        }
    }

    /// <summary>
    /// What the outside world puts on port B's pins: on the 2600, the console switches, active low.
    /// PB2, PB4 and PB5 are unconnected and read 1 here, through the chip's pull-ups (riot.md section
    /// 10); no source states the value read on a 2600 [guessing - verify] (section 12 item 6).
    /// </summary>
    public byte PortBInput { get; set; } = 0xFF;

    /// <summary>
    /// The level on each port A pin. An input pin is what drives it. An output pin is its ORA bit,
    /// pulled to 0 by a closed switch, because port A's output sources too little current to hold
    /// a grounded pin high (riot.md section 5, an inference the sheet marks [guessing - verify];
    /// task 9 kept it for the joysticks, riot.md section 12 item 4).
    /// </summary>
    public byte PortAPins => (byte)(_portAInput & (_ora | ~_ddra));

    /// <summary>
    /// The level on each port B pin. Port B's outputs are push-pull, so an output pin is its ORB
    /// bit, and the chip reads ORB for it; an input pin is what drives it (riot.md section 5).
    /// </summary>
    public byte PortBPins => (byte)((_orb & _ddrb) | (PortBInput & ~_ddrb));

    /// <summary>
    /// True from the cycle the timer passes zero until a read of the count (other than one on that
    /// very cycle) or a timer write (riot.md section 6). Bit 7 of the flag register.
    /// </summary>
    public bool TimerFlag => _timerFlag;

    /// <summary>
    /// True from an edge of PA7 of the armed polarity until the flag register is read (riot.md
    /// section 8). Bit 6 of the flag register.
    /// </summary>
    public bool EdgeFlag => _edgeFlag;

    /// <summary>
    /// The chip's IRQ pin, true when pulled low: a flag set whose interrupt is enabled. The enables
    /// gate the pin and never the flags (riot.md section 7). Not connected on the 2600.
    /// </summary>
    public bool Irq => (_timerFlag && _timerInterruptEnabled) || (_edgeFlag && _edgeInterruptEnabled);

    /// <summary>The byte at <paramref name="offset"/>; A0 to A6 pick it, so any address may be passed.</summary>
    public byte ReadRam(int offset) => _ram[offset & (RamSize - 1)];

    /// <summary>Stores <paramref name="value"/> at <paramref name="offset"/>; A0 to A6 pick the byte.</summary>
    public void WriteRam(int offset, byte value) => _ram[offset & (RamSize - 1)] = value;

    /// <summary>
    /// One CPU cycle: the timer's clock. The count drops on the first cycle after a write, then
    /// once every divider's worth of cycles; on cycle D x N + 1 it passes from 0 to <c>$FF</c>, the
    /// timer flag is set, and from then on it falls once a cycle (riot.md section 6, following the
    /// 1977 sheet's figure and the suite's <c>timer-vblank-spin</c> over the 1981 figure).
    /// </summary>
    public void Tick()
    {
        _timerFlagSetThisCycle = false;
        if (--_untilDecrement > 0)
        {
            return;
        }

        _count--;
        _untilDecrement = 1;
        if (_freeRunning)
        {
            // In the free run the count wraps past $00 again with the flag still set. That is not
            // a new interrupt, so a read on that cycle clears the flag as any other does
            // [guessing - verify]: no source or suite test reaches it.
            return;
        }

        if (_count == 0xFF)
        {
            _freeRunning = true;
            _timerFlag = true;
            _timerFlagSetThisCycle = true;
            return;
        }

        _untilDecrement = _divider;
    }

    /// <summary>
    /// A read of the I/O and timer half by the CPU, with its side effects. A2 low reads a port
    /// register (A1 and A0 pick it). A2 high and A0 low reads the count, A3 setting or clearing the
    /// timer's interrupt enable; A2 high and A0 high reads the flag register (riot.md section 3).
    /// </summary>
    /// <remarks>
    /// Reading the count clears the timer flag, except on the cycle the flag is set, and ends the
    /// free run: the count then falls from the value read once every divider's worth of cycles,
    /// the first fall a whole divider after the read [guessing - verify] (riot.md section 12 item
    /// 1). A read on the cycle the flag is set leaves the free run going as it leaves the flag
    /// [guessing - verify] (section 12 item 2). Reading the flag register clears the edge flag
    /// only.
    /// </remarks>
    public byte ReadRegister(ushort address)
    {
        if ((address & 0x04) == 0)
        {
            return ReadPort(address);
        }

        if ((address & 0x01) != 0)
        {
            byte flags = Flags;
            _edgeFlag = false;
            return flags;
        }

        _timerInterruptEnabled = (address & 0x08) != 0;

        // "The reading of the timer at the same time the interrupt occurs will not reset the
        // interrupt flag" (6532, riot.md section 6; the suite's timer-underflow-read).
        if (!_timerFlagSetThisCycle)
        {
            _timerFlag = false;
            if (_freeRunning)
            {
                _freeRunning = false;
                _untilDecrement = _divider;
            }
        }

        return _count;
    }

    /// <summary>
    /// What <see cref="ReadRegister"/> would return, with no side effect: no flag cleared, no
    /// enable changed and the free run left going. For the bus's <c>Peek</c> and the tests.
    /// </summary>
    public byte PeekRegister(ushort address)
    {
        if ((address & 0x04) == 0)
        {
            return ReadPort(address);
        }

        return (address & 0x01) != 0 ? Flags : _count;
    }

    /// <summary>
    /// A write to the I/O and timer half (riot.md section 3). A2 low writes a port register, all
    /// eight bits whatever the directions. A2 high and A4 low is the PA7 edge control: A0 arms the
    /// positive (1) or negative (0) edge, A1 enables its interrupt, and the data is ignored. A2
    /// high and A4 high starts the timer: the data is the count, A1 and A0 choose divide by 1, 8,
    /// 64 or 1024, A3 the interrupt enable, and the timer flag is cleared.
    /// </summary>
    public void WriteRegister(ushort address, byte value)
    {
        if ((address & 0x04) == 0)
        {
            switch (address & 0x03)
            {
                case 0:
                    _ora = value;
                    WatchPa7();
                    break;
                case 1:
                    _ddra = value;
                    WatchPa7();
                    break;
                case 2:
                    _orb = value;
                    break;
                default:
                    _ddrb = value;
                    break;
            }

            return;
        }

        if ((address & 0x10) == 0)
        {
            // Arming sets nothing, whatever the level and the old polarity: the suite's
            // pa7-polarity measured it, against the data sheet's "may" (riot.md section 8).
            _edgePositive = (address & 0x01) != 0;
            _edgeInterruptEnabled = (address & 0x02) != 0;
            return;
        }

        _divider = (address & 0x03) switch
        {
            0 => 1,
            1 => 8,
            2 => 64,
            _ => 1024,
        };
        _timerInterruptEnabled = (address & 0x08) != 0;
        _count = value;
        _untilDecrement = 1;
        _freeRunning = false;
        _timerFlag = false;
    }

    /// <summary>
    /// The power-on state. RES zeroes ORA, DDRA, ORB and DDRB, so every pin is an input, turns both
    /// interrupts off and arms the negative edge (riot.md section 9). The rest is not stated by any
    /// source, so the model picks fixed values that make runs repeat, recorded in
    /// <c>docs/known-differences.md</c>: the RAM clear; both flags clear; the count at <c>$00</c> at
    /// divide by 1, so the first cycle passes zero, sets the timer flag and starts the free run. The
    /// real timer is taken to have been running since the RIOT's own reset ended, before the CPU's,
    /// which rests on the two reset circuits' RC values [guessing - verify] (riot.md section 9).
    /// </summary>
    public void PowerOn()
    {
        Array.Clear(_ram);
        _ora = 0;
        _ddra = 0;
        _orb = 0;
        _ddrb = 0;
        _timerInterruptEnabled = false;
        _edgeInterruptEnabled = false;
        _edgePositive = false;

        _count = 0;
        _divider = 1;
        _untilDecrement = 1;
        _freeRunning = false;
        _timerFlag = false;
        _timerFlagSetThisCycle = false;
        _edgeFlag = false;
        _pa7 = (PortAPins & 0x80) != 0;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_ram), _ram);
        sink.Add(nameof(_ora), _ora);
        sink.Add(nameof(_ddra), _ddra);
        sink.Add(nameof(_orb), _orb);
        sink.Add(nameof(_ddrb), _ddrb);
        sink.Add(nameof(_portAInput), _portAInput);
        sink.Add(nameof(PortBInput), PortBInput);
        sink.Add(nameof(_count), _count);
        sink.Add(nameof(_divider), _divider);
        sink.Add(nameof(_untilDecrement), _untilDecrement);
        sink.Add(nameof(_freeRunning), _freeRunning);
        sink.Add(nameof(_timerFlag), _timerFlag);
        sink.Add(nameof(_timerFlagSetThisCycle), _timerFlagSetThisCycle);
        sink.Add(nameof(_timerInterruptEnabled), _timerInterruptEnabled);
        sink.Add(nameof(_edgeFlag), _edgeFlag);
        sink.Add(nameof(_edgeInterruptEnabled), _edgeInterruptEnabled);
        sink.Add(nameof(_edgePositive), _edgePositive);
        sink.Add(nameof(_pa7), _pa7);
    }

    /// <summary>The flag register: bit 7 the timer flag, bit 6 the edge flag, bits 5 to 0 zero.</summary>
    private byte Flags => (byte)((_timerFlag ? 0x80 : 0) | (_edgeFlag ? 0x40 : 0));

    private byte ReadPort(ushort address) => (address & 0x03) switch
    {
        0 => PortAPins,
        1 => _ddra,
        2 => PortBPins,
        _ => _ddrb,
    };

    /// <summary>
    /// Sets the edge flag when the PA7 pin moves in the armed direction. The detector watches the
    /// pin, input or output, so ORA, DDRA and the joystick all reach it (riot.md section 8). It runs
    /// when the pin can change, so the flag is there for a read on any later cycle; when the real
    /// flag becomes visible is not stated [guessing - verify] (section 12 item 5).
    /// </summary>
    private void WatchPa7()
    {
        bool level = (PortAPins & 0x80) != 0;
        if (level == _pa7)
        {
            return;
        }

        _pa7 = level;
        if (level == _edgePositive)
        {
            _edgeFlag = true;
        }
    }
}
