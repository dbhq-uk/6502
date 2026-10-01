namespace Dbhq.Machines.Kim1;

/// <summary>
/// The MOS 6530 RRIOT: 1 KB of mask ROM, 64 bytes of RAM, two 8-bit I/O ports
/// with data direction registers, and an interval timer, in one chip.
/// </summary>
/// <remarks>
/// Written from the MCS6530 data sheet (MOS Technology, March 1976) and
/// Appendix H of the KIM-1 User Manual (August 1976). The chip has no clock of
/// its own here: the machine calls <see cref="Tick"/> once at the start of
/// every bus cycle, before the access itself is served.
/// </remarks>
public sealed class Rriot6530
{
    public const int RomSize = 1024;

    public const int RamSize = 64;

    private readonly byte[] _rom;
    private readonly byte[] _ram = new byte[RamSize];

    private byte _counter;
    private int _prescale = 1;
    private int _untilDecrement = 1;
    private bool _passedZero;
    private bool _flag;
    private bool _flagSetThisCycle;

    public Rriot6530(ReadOnlySpan<byte> rom)
    {
        if (rom.Length != RomSize)
        {
            throw new ArgumentException($"A 6530 holds {RomSize} bytes of ROM, not {rom.Length}", nameof(rom));
        }

        _rom = rom.ToArray();
    }

    public byte PortAData { get; private set; }

    public byte PortADirection { get; private set; }

    public byte PortBData { get; private set; }

    public byte PortBDirection { get; private set; }

    /// <summary>
    /// What the outside world drives onto port A's pins. A pin nothing pulls
    /// low reads high: the data sheet gives every input a pull-up.
    /// </summary>
    public byte PortAInput { get; set; } = 0xFF;

    /// <inheritdoc cref="PortAInput"/>
    public byte PortBInput { get; set; } = 0xFF;

    /// <summary>True while the timer may pull PB7 low as an interrupt. Set by A3 on every timer access.</summary>
    public bool InterruptEnabled { get; private set; }

    /// <summary>True once the timer has counted past zero, until the timer is next read or written.</summary>
    public bool TimerFlag => _flag;

    /// <summary>The timer's count, read without the side effects a read by the CPU has.</summary>
    public byte TimerCount => _counter;

    /// <summary>PB7 is low as an interrupt: the timer flag is set and A3 enabled it.</summary>
    public bool IrqActive => InterruptEnabled && _flag;

    /// <summary>
    /// The level on each port A pin. "During a read operation the
    /// microprocessor is not reading the I/O Registers but in fact is reading
    /// the peripheral data pins": an output pin shows its data bit, an input
    /// pin shows what drives it.
    /// </summary>
    public byte PortAPins => (byte)((PortAData & PortADirection) | (PortAInput & ~PortADirection));

    /// <inheritdoc cref="PortAPins"/>
    /// <remarks>PB7 doubles as the timer's interrupt output, which pulls the pin low.</remarks>
    public byte PortBPins
    {
        get
        {
            byte pins = (byte)((PortBData & PortBDirection) | (PortBInput & ~PortBDirection));
            return IrqActive && (PortBDirection & 0x80) == 0 ? (byte)(pins & 0x7F) : pins;
        }
    }

    public byte ReadRom(int offset) => _rom[offset & (RomSize - 1)];

    public byte ReadRam(int offset) => _ram[offset & (RamSize - 1)];

    public void WriteRam(int offset, byte value) => _ram[offset & (RamSize - 1)] = value;

    /// <summary>
    /// One clock of the timer. The count drops on the first clock after a
    /// write, then once every prescale period; when it passes zero to $FF the
    /// flag is set and the count falls at one per clock from then on.
    /// </summary>
    /// <remarks>
    /// The data sheet's worked example fixes this: 52 written with divide by
    /// 8 reads 25 after 213 clocks and 0 after 415, and the flag is set at
    /// (52 x 8) + 1 = 417.
    /// </remarks>
    public void Tick()
    {
        _flagSetThisCycle = false;
        if (--_untilDecrement > 0)
        {
            return;
        }

        _counter--;
        if (_counter == 0xFF)
        {
            _flag = true;
            _flagSetThisCycle = true;
            _passedZero = true;
        }

        _untilDecrement = _passedZero ? 1 : _prescale;
    }

    /// <summary>
    /// A read of the I/O and timer area. <paramref name="register"/> is
    /// address lines A0 to A3: A2 low is the ports (A1 and A0 pick the
    /// register), A2 high is the timer (A0 high reads the flag, low reads the
    /// count, and A3 sets the interrupt enable).
    /// </summary>
    public byte ReadRegister(int register)
    {
        if ((register & 0x04) == 0)
        {
            return (register & 0x03) switch
            {
                0 => PortAPins,
                1 => PortADirection,
                2 => PortBPins,
                _ => PortBDirection,
            };
        }

        if ((register & 0x01) != 0)
        {
            // "When the interrupt flag is read on DB7 all other outputs go to 0."
            return _flag ? (byte)0x80 : (byte)0x00;
        }

        InterruptEnabled = (register & 0x08) != 0;

        // "The reading of the timer at the same time the interrupt occurs
        // will not reset the interrupt flag."
        if (!_flagSetThisCycle)
        {
            _flag = false;
            if (_passedZero)
            {
                // KIM-1 User Manual, H-6: a read after zero "will restore the
                // divide ratio to its previously programmed value".
                _passedZero = false;
                _untilDecrement = _prescale;
            }
        }

        return _counter;
    }

    /// <summary>
    /// A write to the I/O and timer area. With A2 high it starts the timer:
    /// A1 and A0 choose divide by 1, 8, 64 or 1024 and A3 the interrupt enable.
    /// </summary>
    public void WriteRegister(int register, byte value)
    {
        if ((register & 0x04) == 0)
        {
            switch (register & 0x03)
            {
                case 0:
                    PortAData = value;
                    break;
                case 1:
                    PortADirection = value;
                    break;
                case 2:
                    PortBData = value;
                    break;
                default:
                    PortBDirection = value;
                    break;
            }

            return;
        }

        _prescale = (register & 0x03) switch
        {
            0 => 1,
            1 => 8,
            2 => 64,
            _ => 1024,
        };
        InterruptEnabled = (register & 0x08) != 0;
        _counter = value;
        _untilDecrement = 1;
        _passedZero = false;
        _flag = false;
    }

    /// <summary>
    /// The RES line: "a zeroing of all four I/O registers", so every pin
    /// becomes an input, and the interrupt is disabled. RAM and the timer's
    /// count are not mentioned, and are left as they are.
    /// </summary>
    public void Reset()
    {
        PortAData = 0;
        PortADirection = 0;
        PortBData = 0;
        PortBDirection = 0;
        InterruptEnabled = false;
    }
}
