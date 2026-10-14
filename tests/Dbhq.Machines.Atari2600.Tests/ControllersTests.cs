using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The two joysticks and the console switches, through the machine: each input reaches its bit of
/// the RIOT's port A or port B (<c>docs/atari-2600/facts/riot.md</c> section 10) or the TIA's INPT4
/// and INPT5 (<c>tia.md</c> section 20), active low, and any combination is taken and reported as
/// written (the plan's Review Focus 4).
/// </summary>
public class ControllersTests
{
    private const ushort Swcha = 0x0280;
    private const ushort Swacnt = 0x0281;
    private const ushort Swchb = 0x0282;
    private const ushort Swbcnt = 0x0283;
    private const ushort Timint = 0x0285;
    private const ushort Pa7Negative = 0x0284;
    private const ushort Vblank = 0x0001;
    private const ushort Inpt4 = 0x000C;
    private const ushort Inpt5 = 0x000D;

    private const Joystick AllOfIt = Joystick.Up | Joystick.Down | Joystick.Left | Joystick.Right | Joystick.Fire;

    private const ConsoleSwitches EverySwitch = ConsoleSwitches.Reset | ConsoleSwitches.Select | ConsoleSwitches.BlackAndWhite
        | ConsoleSwitches.LeftDifficultyA | ConsoleSwitches.RightDifficultyA;

    private static Atari2600 Machine() => TestPrograms.Machine(Region.Ntsc);

    private static bool Fire(Atari2600 machine, ushort port) => (machine.Bus.Peek(port) & 0x80) != 0;

    public static TheoryData<int, Joystick, byte> Directions => new()
    {
        // riot.md section 10's table: P0 on D7 to D4 (right, left, down, up), P1 on D3 to D0.
        { 0, Joystick.Right, 0x7F },
        { 0, Joystick.Left, 0xBF },
        { 0, Joystick.Down, 0xDF },
        { 0, Joystick.Up, 0xEF },
        { 1, Joystick.Right, 0xF7 },
        { 1, Joystick.Left, 0xFB },
        { 1, Joystick.Down, 0xFD },
        { 1, Joystick.Up, 0xFE },
    };

    [Theory]
    [MemberData(nameof(Directions))]
    public void Section_10_each_direction_reaches_its_bit_of_port_a_active_low(int stick, Joystick direction, byte swcha)
    {
        Atari2600 machine = Machine();
        Assert.Equal(0xFF, machine.Bus.Peek(Swcha));

        machine.SetJoystick(stick, direction);
        Assert.Equal(swcha, machine.Bus.Peek(Swcha));
        Assert.True(Fire(machine, Inpt4));
        Assert.True(Fire(machine, Inpt5));

        machine.SetJoystick(stick, Joystick.None);
        Assert.Equal(0xFF, machine.Bus.Peek(Swcha));
    }

    [Theory]
    [InlineData(0, Inpt4, Inpt5)]
    [InlineData(1, Inpt5, Inpt4)]
    public void Section_20_1_fire_reaches_its_inpt_d7_active_low_and_not_port_a(int stick, ushort port, ushort other)
    {
        Atari2600 machine = Machine();
        machine.SetJoystick(stick, Joystick.Fire);
        Assert.False(Fire(machine, port));
        Assert.True(Fire(machine, other));
        Assert.Equal(0xFF, machine.Bus.Peek(Swcha));

        machine.SetJoystick(stick, Joystick.None);
        Assert.True(Fire(machine, port));
    }

    [Fact]
    public void Section_20_2_the_worked_example_through_a_program()
    {
        // LDA $0C; STA $80; LDA $0D; STA $81; JMP $F008. The low seven bits of each read are the
        // operand left on the bus, so INPT4 reads $8C up and $0C pressed (tia.md section 20.2).
        byte[] code = [0xA5, 0x0C, 0x85, 0x80, 0xA5, 0x0D, 0x85, 0x81, 0x4C, 0x08, 0xF0];
        Atari2600 machine = TestPrograms.Machine(Region.Ntsc, code);
        machine.SetJoystick(1, Joystick.Fire);
        for (int i = 0; i < 4; i++)
        {
            machine.Step();
        }

        Assert.Equal(0x8C, machine.Bus.Peek(0x0080));
        Assert.Equal(0x0D, machine.Bus.Peek(0x0081));
    }

    [Fact]
    public void Section_20_2_the_latch_holds_a_press_through_the_bus()
    {
        Atari2600 machine = Machine();
        machine.Bus.Write(Vblank, 0x40);
        machine.SetJoystick(0, Joystick.Fire);
        machine.SetJoystick(0, Joystick.None);
        Assert.False(Fire(machine, Inpt4));
        Assert.True(Fire(machine, Inpt5));

        machine.Bus.Write(Vblank, 0x00);
        Assert.True(Fire(machine, Inpt4));
    }

    public static TheoryData<ConsoleSwitches, byte> Switches => new()
    {
        // riot.md section 10: Reset D0 and Select D1 read 0 pressed; colour D3 reads 0 at black and
        // white; the difficulties D6 (left) and D7 (right) read 1 at A; D2, D4 and D5 read 1.
        { ConsoleSwitches.None, 0x3F },
        { ConsoleSwitches.Reset, 0x3E },
        { ConsoleSwitches.Select, 0x3D },
        { ConsoleSwitches.BlackAndWhite, 0x37 },
        { ConsoleSwitches.LeftDifficultyA, 0x7F },
        { ConsoleSwitches.RightDifficultyA, 0xBF },
    };

    [Theory]
    [MemberData(nameof(Switches))]
    public void Section_10_each_switch_reaches_its_bit_of_port_b(ConsoleSwitches switches, byte swchb)
    {
        Atari2600 machine = Machine();
        Assert.Equal(0x3F, machine.Bus.Peek(Swchb));

        machine.SetSwitches(switches);
        Assert.Equal(swchb, machine.Bus.Peek(Swchb));

        machine.SetSwitches(ConsoleSwitches.None);
        Assert.Equal(0x3F, machine.Bus.Peek(Swchb));
    }

    [Theory]
    [InlineData(Joystick.None, Joystick.None, ConsoleSwitches.None, 0xFF, 0x3F)]
    [InlineData(Joystick.Up | Joystick.Right, Joystick.None, ConsoleSwitches.None, 0x6F, 0x3F)]
    [InlineData(Joystick.None, Joystick.Down | Joystick.Left, ConsoleSwitches.None, 0xF9, 0x3F)]
    [InlineData(Joystick.None, Joystick.None, ConsoleSwitches.Reset | ConsoleSwitches.LeftDifficultyA, 0xFF, 0x7E)]
    [InlineData(Joystick.None, Joystick.None, ConsoleSwitches.Select | ConsoleSwitches.BlackAndWhite | ConsoleSwitches.RightDifficultyA, 0xFF, 0xB5)]
    public void Worked_example_10_inputs(Joystick stick0, Joystick stick1, ConsoleSwitches switches, byte swcha, byte swchb)
    {
        Atari2600 machine = Machine();
        machine.SetJoystick(0, stick0);
        machine.SetJoystick(1, stick1);
        machine.SetSwitches(switches);
        Assert.Equal(swcha, machine.Bus.Peek(Swcha));
        Assert.Equal(swchb, machine.Bus.Peek(Swchb));
    }

    [Fact]
    public void Review_focus_4_up_and_down_together_are_taken_as_written()
    {
        Atari2600 machine = Machine();
        machine.SetJoystick(0, Joystick.Up | Joystick.Down);
        machine.SetJoystick(1, Joystick.Left | Joystick.Right);
        Assert.Equal(Joystick.Up | Joystick.Down, machine.JoystickState(0));
        Assert.Equal(Joystick.Left | Joystick.Right, machine.JoystickState(1));
        Assert.Equal(0xC3, machine.Bus.Peek(Swcha));
    }

    [Fact]
    public void Review_focus_4_every_input_at_once_is_taken_as_written()
    {
        Atari2600 machine = Machine();
        machine.SetJoystick(0, AllOfIt);
        machine.SetJoystick(1, AllOfIt);
        machine.SetSwitches(EverySwitch);

        Assert.Equal(AllOfIt, machine.JoystickState(0));
        Assert.Equal(AllOfIt, machine.JoystickState(1));
        Assert.Equal(EverySwitch, machine.Switches);
        Assert.Equal(0x00, machine.Bus.Peek(Swcha));
        Assert.Equal(0xF4, machine.Bus.Peek(Swchb));
        Assert.False(Fire(machine, Inpt4));
        Assert.False(Fire(machine, Inpt5));
    }

    [Fact]
    public void Review_focus_4_inputs_are_held_through_a_power_cycle()
    {
        // The console's Reset is a switch the program reads, not a reset line (riot.md section 2), and
        // a held stick or switch is the outside world: power on leaves them as they are.
        Atari2600 machine = Machine();
        machine.SetSwitches(ConsoleSwitches.Reset | ConsoleSwitches.BlackAndWhite);
        machine.SetJoystick(0, Joystick.Left | Joystick.Fire);
        machine.PowerOn();

        Assert.Equal(ConsoleSwitches.Reset | ConsoleSwitches.BlackAndWhite, machine.Switches);
        Assert.Equal(Joystick.Left | Joystick.Fire, machine.JoystickState(0));
        Assert.Equal(0x36, machine.Bus.Peek(Swchb));
        Assert.Equal(0xBF, machine.Bus.Peek(Swcha));
        Assert.False(Fire(machine, Inpt4));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void A_stick_that_is_not_0_or_1_is_refused(int stick)
    {
        Atari2600 machine = Machine();
        Assert.Throws<ArgumentOutOfRangeException>(() => machine.SetJoystick(stick, Joystick.Up));
        Assert.Throws<ArgumentOutOfRangeException>(() => machine.JoystickState(stick));
        Assert.Equal(0xFF, machine.Bus.Peek(Swcha));
    }

    [Fact]
    public void A_bit_that_names_no_input_is_refused_and_changes_nothing()
    {
        Atari2600 machine = Machine();
        machine.SetJoystick(0, Joystick.Up);
        machine.SetSwitches(ConsoleSwitches.Select);

        Assert.Throws<ArgumentOutOfRangeException>(() => machine.SetJoystick(0, (Joystick)32));
        Assert.Throws<ArgumentOutOfRangeException>(() => machine.SetSwitches((ConsoleSwitches)32));
        Assert.Equal(Joystick.Up, machine.JoystickState(0));
        Assert.Equal(ConsoleSwitches.Select, machine.Switches);
        Assert.Equal(0xEF, machine.Bus.Peek(Swcha));
        Assert.Equal(0x3D, machine.Bus.Peek(Swchb));
    }

    [Fact]
    public void Section_5_a_port_a_output_at_1_reads_0_while_its_stick_holds_it_low()
    {
        // Task 9's decision on riot.md section 5's inference: the joystick's switch grounds the pin
        // and port A's output cannot hold it high, so an output at 1 reads 0 while pressed; an output
        // at 0 reads 0 whatever the stick.
        Atari2600 machine = Machine();
        machine.Bus.Write(Swacnt, 0xFF);
        machine.Bus.Write(Swcha, 0xFF);
        machine.SetJoystick(0, Joystick.Up);
        Assert.Equal(0xEF, machine.Bus.Peek(Swcha));

        machine.SetJoystick(0, Joystick.None);
        machine.Bus.Write(Swcha, 0x00);
        Assert.Equal(0x00, machine.Bus.Peek(Swcha));
    }

    [Fact]
    public void Section_5_a_port_b_output_reads_its_register_whatever_the_switch()
    {
        // Port B's outputs are push-pull and read the register (riot.md section 5): worked example 3,
        // with Reset held on an output bit.
        Atari2600 machine = Machine();
        machine.Bus.Write(Swbcnt, 0x35);
        machine.Bus.Write(Swchb, 0x01);
        machine.SetSwitches(ConsoleSwitches.Reset);
        Assert.Equal(0x0B, machine.Bus.Peek(Swchb));
    }

    [Fact]
    public void Section_8_pushing_the_left_stick_right_is_a_pa7_edge()
    {
        // P0 right is PA7 (section 10), and the edge detect watches the pin (section 8).
        Atari2600 machine = Machine();
        machine.Bus.Write(Pa7Negative, 0x00);
        Assert.Equal(0x00, machine.Bus.Peek(Timint) & 0x40);

        machine.SetJoystick(0, Joystick.Right);
        Assert.Equal(0x40, machine.Bus.Peek(Timint) & 0x40);
    }
}
