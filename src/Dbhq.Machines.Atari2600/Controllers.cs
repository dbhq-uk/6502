namespace Dbhq.Machines.Atari2600;

/// <summary>
/// One joystick as held: any combination of the four directions and the fire button, including
/// directions a real stick cannot press together (the plan's Review Focus 4).
/// </summary>
[Flags]
public enum Joystick
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8,
    Fire = 16,
}

/// <summary>
/// The console switches as set: Reset and Select are pressed while their flag is set and spring
/// back when it is cleared; black and white and the two difficulties at A stay as they are set.
/// No flag is the console at rest: colour, both difficulties at B, nothing pressed.
/// </summary>
[Flags]
public enum ConsoleSwitches
{
    None = 0,
    Reset = 1,
    Select = 2,
    BlackAndWhite = 4,
    LeftDifficultyA = 8,
    RightDifficultyA = 16,
}

/// <summary>
/// The two joysticks and the console switches, wired as the console wires them: the directions on
/// the RIOT's port A, the fire buttons on the TIA's INPT4 and INPT5, and the switches on port B,
/// every one active low (<c>docs/atari-2600/facts/riot.md</c> section 10, <c>tia.md</c> section 20).
/// </summary>
/// <remarks>
/// <para>
/// <b>Port A</b>: the left stick (0) on D7 to D4, right, left, down and up; the right stick (1) on D3
/// to D0 in the same order. A held direction grounds its pin. <b>Port B</b>: D7 the right
/// difficulty (1 at A), D6 the left (1 at A), D3 colour (0 at black and white), D1 Select and D0
/// Reset (0 pressed); D2, D4 and D5 are not connected and float high through the chip's pull-ups
/// (riot.md section 12 item 6) [guessing - verify].
/// </para>
/// <para>
/// What the pins do when the program makes them outputs is the chips' business: the RIOT reads a
/// port A output at 1 as 0 while its switch grounds it, and a port B output as its register (riot.md
/// section 5); the TIA latches the fire buttons when VBLANK D6 asks (tia.md section 20.2).
/// </para>
/// <para>
/// The state is what was last written, kept as written and reported as written. It is the outside
/// world, so the machine's power on does not change it. A change reaches the chips at once, between
/// two of the CPU's bus accesses, which is the only place the host can make it.
/// </para>
/// </remarks>
internal sealed class Controllers
{
    private const Joystick EveryStickInput = Joystick.Up | Joystick.Down | Joystick.Left | Joystick.Right | Joystick.Fire;

    private const ConsoleSwitches EverySwitch = ConsoleSwitches.Reset | ConsoleSwitches.Select | ConsoleSwitches.BlackAndWhite
        | ConsoleSwitches.LeftDifficultyA | ConsoleSwitches.RightDifficultyA;

    // Port B's pins that no switch reaches: PB2, PB4 and PB5 (riot.md section 2).
    private const byte UnconnectedPortB = 0x34;

    private readonly Riot6532 _riot;
    private readonly Tia _tia;
    private readonly Joystick[] _sticks = new Joystick[2];
    private ConsoleSwitches _switches;

    internal Controllers(Riot6532 riot, Tia tia)
    {
        _riot = riot;
        _tia = tia;
        Apply();
    }

    /// <summary>The console switches as last set.</summary>
    internal ConsoleSwitches Switches => _switches;

    /// <summary>The joystick in port <paramref name="stick"/> as last set.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stick"/> is not 0 or 1.</exception>
    internal Joystick Stick(int stick)
    {
        CheckStick(stick);
        return _sticks[stick];
    }

    /// <summary>Sets the joystick in port <paramref name="stick"/>, 0 the left and 1 the right.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="stick"/> is not 0 or 1, or <paramref name="state"/> has a bit that names no input.
    /// </exception>
    internal void SetStick(int stick, Joystick state)
    {
        CheckStick(stick);
        if ((state & ~EveryStickInput) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "A joystick has four directions and a fire button, and nothing else.");
        }

        _sticks[stick] = state;
        Apply();
    }

    /// <summary>Sets the console switches.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> has a bit that names no switch.</exception>
    internal void SetSwitches(ConsoleSwitches state)
    {
        if ((state & ~EverySwitch) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "The console has Reset, Select, the colour switch and two difficulty switches, and nothing else.");
        }

        _switches = state;
        Apply();
    }

    /// <summary>Port A's pins with the two sticks held as given, before any output the program drives.</summary>
    internal static byte PortA(Joystick left, Joystick right) =>
        (byte)~((Directions(left) << 4) | Directions(right));

    /// <summary>Port B's pins with the switches set as given, before any output the program drives.</summary>
    internal static byte PortB(ConsoleSwitches switches)
    {
        int pins = UnconnectedPortB;
        pins |= (switches & ConsoleSwitches.Reset) == 0 ? 0x01 : 0;
        pins |= (switches & ConsoleSwitches.Select) == 0 ? 0x02 : 0;
        pins |= (switches & ConsoleSwitches.BlackAndWhite) == 0 ? 0x08 : 0;
        pins |= (switches & ConsoleSwitches.LeftDifficultyA) != 0 ? 0x40 : 0;
        pins |= (switches & ConsoleSwitches.RightDifficultyA) != 0 ? 0x80 : 0;
        return (byte)pins;
    }

    /// <summary>The fire buttons' pins for the TIA: bit 0 INPT4's (the left stick), bit 1 INPT5's; 1 is up.</summary>
    internal static byte FirePins(Joystick left, Joystick right) =>
        (byte)(((left & Joystick.Fire) == 0 ? 0b01 : 0) | ((right & Joystick.Fire) == 0 ? 0b10 : 0));

    // A stick's four directions as a nibble, up D0, down D1, left D2, right D3: the order the port
    // has them in (riot.md section 10), and the enum's.
    private static int Directions(Joystick stick) => (int)stick & 0x0F;

    private static void CheckStick(int stick)
    {
        if (stick is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(stick), stick, "The console has two joystick ports, 0 the left and 1 the right.");
        }
    }

    private void Apply()
    {
        _riot.PortAInput = PortA(_sticks[0], _sticks[1]);
        _riot.PortBInput = PortB(_switches);
        _tia.FirePins = FirePins(_sticks[0], _sticks[1]);
    }
}
