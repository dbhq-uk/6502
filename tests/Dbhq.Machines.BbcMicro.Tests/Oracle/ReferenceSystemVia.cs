// The test oracle: the per-cycle implementation as it stood at commit 2876852, before task 6b
// made the machine's chips run lazily. Kept unchanged apart from its names, and never used by
// the machine; the equivalence tests run it side by side with the real one and compare them.
namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The system VIA, IC3 at <c>$FE40</c>, as the Model B wires it: the keyboard and the sound
/// chip on port A, the addressable latch IC32 on port B, vsync on CA1 and the keyboard
/// interrupt on CA2 (fact sheet <c>via.md</c> sections 2 and 3).
/// </summary>
/// <remarks>
/// <para>
/// <b>Port B and the latch.</b> PB0 to PB2 address one bit of the 74LS259 latch and PB3 is the
/// value it takes (section 2.2). Bit 0 is the sound chip's write enable, bit 3 the keyboard
/// enable, bits 4 and 5 the screen start adder, bits 6 and 7 the lock LEDs. The real board
/// strobes the latch on every write to the VIA; here it is strobed on writes to ORB and DDRB,
/// which the sheet shows is the same while PB0 to PB3 are outputs, as the OS always has them
/// [inferring, section 2.2]. The latch's /CLR is tied high, so no reset clears it; what it holds
/// at power on is not known and is taken as 0. PB4 and PB5 are the joystick fire buttons and
/// read 1, not pressed; PB7 reads 1, which tells the OS no speech chip is fitted; PB6 has no OS
/// dependency and reads 1 [guessing - verify] (sections 1.3, 2.1).
/// </para>
/// <para>
/// <b>The keyboard (section 3(a)).</b> A 74LS163 counter drives the column. With latch bit 3
/// low the keyboard is enabled: on each 1 MHz clock the counter loads PA0 to PA3, and PA7
/// reads 1 when the key at that column and the row on PA4 to PA6 is down. With bit 3 high the
/// counter free-runs over 0 to 15 at 1 MHz and the 74LS251 that drives PA7 is off; nothing
/// else drives PA7, so it reads 0 [guessing - verify: the sheet's recommendation]. Either way
/// CA2 is high while a key in rows 1 to 7 of the counter's column is down, so autoscan gives
/// one rising edge per 16 microsecond sweep while a key is held. The counter and PA7 change in
/// <see cref="Tick"/>, at the start of a cycle, so an access in the same cycle sees them.
/// </para>
/// <para>
/// <b>Control lines.</b> CA1 is the 6845's vsync, idle low, so the OS's PCR of <c>$04</c> sets
/// IFR1 when the pulse ends. CA2 is the keyboard, idle low with no key down. CB1 (the ADC's end
/// of conversion) and CB2 (the light pen) have nothing fitted and stay at the chip's starting
/// level, so they never make an edge.
/// </para>
/// <para>
/// <b>Reset.</b> Only power on resets this chip; BREAK does not (section 1.2, <c>bus.md</c>
/// section 5). <see cref="BbcBus"/> does the choosing.
/// </para>
/// </remarks>
public sealed class ReferenceSystemVia : ReferenceVia6522
{
    private readonly BbcKeyboard _keyboard;
    private int _column;

    public ReferenceSystemVia(BbcKeyboard keyboard)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        _keyboard = keyboard;

        // PB0-PB3 inputs pulled up until DDRB makes them outputs; PB4-PB7 as above.
        PortBInput = 0xFF;
        UpdatePortAInput();

        // Idle levels, set before the OS enables an interrupt so the first change is a real edge.
        SetCa1(false);
        SetCa2(false);

        PortBWritten += StrobeLatch;
    }

    /// <summary>IC32, the eight-bit addressable latch (section 2.2).</summary>
    public byte Latch { get; private set; }

    /// <summary>
    /// The screen start adder's two latch bits: bit 0 is C0 (latch bit 4) and bit 1 is C1
    /// (latch bit 5). The OS writes C1 C0 = 10 for modes 0 to 2, 00 for mode 3, 11 for modes 4
    /// and 5 and 01 for mode 6 (section 2.2, which trusts the ROM over the AUG's table).
    /// </summary>
    public int ScreenStartLatch => (Latch >> 4) & 3;

    /// <summary>
    /// The sound chip's write strobe: latch bit 0 falling, with the byte then on port A. The OS
    /// puts the byte on PA, writes ORB = <c>$00</c>, waits, and writes ORB = <c>$08</c>.
    /// </summary>
    public event Action<byte>? SoundWrite;

    /// <summary>The 6845's VSYNC output, which drives CA1.</summary>
    public bool VsyncInput
    {
        set => SetCa1(value);
    }

    /// <summary>One 1 MHz cycle: the chip, then the keyboard's counter, PA7 and CA2.</summary>
    public override void Tick()
    {
        base.Tick();

        if (KeyboardEnabled)
        {
            _column = PortAPins & 0x0F;
        }
        else
        {
            _column = (_column + 1) & 0x0F;
        }

        UpdatePortAInput();
        SetCa2(_keyboard.AnyKeyDown(_column));
    }

    /// <summary>Latch bit 3 low: the OS's "stop auto scanning", PA selects a key.</summary>
    private bool KeyboardEnabled => (Latch & 0x08) == 0;

    /// <summary>
    /// What the outside drives on port A. PA0 to PA6 are outputs whenever the OS reads the
    /// keyboard; as inputs nothing drives them and they read 1. PA7 is the key.
    /// </summary>
    private void UpdatePortAInput()
    {
        int row = (PortAPins >> 4) & 7;
        bool key = KeyboardEnabled && _keyboard.Read(_column, row);
        PortAInput = (byte)(0x7F | (key ? 0x80 : 0));
    }

    private void StrobeLatch()
    {
        int pins = PortBPins;
        int bit = pins & 7;
        bool high = (pins & 0x08) != 0;
        bool soundWasHigh = (Latch & 0x01) != 0;

        Latch = high ? (byte)(Latch | (1 << bit)) : (byte)(Latch & ~(1 << bit));

        if (bit == 0 && soundWasHigh && !high)
        {
            SoundWrite?.Invoke(PortAPins);
        }
    }
}
