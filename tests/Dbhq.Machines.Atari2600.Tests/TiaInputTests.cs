using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The TIA's six input ports, chip alone, from <c>docs/atari-2600/facts/tia.md</c> section 20: the
/// fire buttons on INPT4 and INPT5 with the latch that VBLANK D6 turns on, and INPT0 to INPT3 with
/// no paddle connected. A read gives the bits the port drives, D7, and 0 elsewhere; the bus puts the
/// residue on the rest (<see cref="Atari2600BusTests"/>).
/// </summary>
public class TiaInputTests
{
    private const int Vblank = 0x01;
    private const int Inpt4 = 0x0C;
    private const int Inpt5 = 0x0D;

    // TiaFirePins: bit 0 is INPT4's pin, bit 1 INPT5's; 1 is high, the button up.
    private const byte BothUp = 0b11;
    private const byte LeftDown = 0b10;
    private const byte RightDown = 0b01;

    [Fact]
    public void Section_20_2_the_worked_example_step_by_step()
    {
        // tia.md section 20.2's table, left joystick, INPT4's D7 at each step.
        var tia = new Tia();
        Assert.Equal(0x80, tia.Read(Inpt4));        // 1: latch off, released

        tia.FirePins = LeftDown;
        Assert.Equal(0x00, tia.Read(Inpt4));        // 2: latch off, held
        tia.FirePins = BothUp;
        Assert.Equal(0x80, tia.Read(Inpt4));        // 3: latch off, released

        tia.Write(Vblank, 0x40);
        Assert.Equal(0x80, tia.Read(Inpt4));        // 4: latch on, released
        tia.FirePins = LeftDown;
        Assert.Equal(0x00, tia.Read(Inpt4));        // 5: latch on, held
        tia.FirePins = BothUp;
        Assert.Equal(0x00, tia.Read(Inpt4));        // 6: released, and latched

        tia.Write(Vblank, 0x00);
        Assert.Equal(0x80, tia.Read(Inpt4));        // 7: disabling resets the latch to 1
        tia.FirePins = LeftDown;
        Assert.Equal(0x00, tia.Read(Inpt4));        // 8: latch off, held

        tia.Write(Vblank, 0x40);
        Assert.Equal(0x00, tia.Read(Inpt4));        // 9: enabled with the button held: cleared at once
    }

    [Fact]
    public void Section_20_2_the_right_button_is_inpt5_and_the_left_does_not_reach_it()
    {
        var tia = new Tia();
        tia.FirePins = RightDown;
        Assert.Equal(0x00, tia.Read(Inpt5));
        Assert.Equal(0x80, tia.Read(Inpt4));

        tia.FirePins = LeftDown;
        Assert.Equal(0x80, tia.Read(Inpt5));
        Assert.Equal(0x00, tia.Read(Inpt4));
    }

    [Fact]
    public void Section_20_2_both_latches_are_enabled_and_disabled_together()
    {
        var tia = new Tia();
        tia.Write(Vblank, 0x40);
        tia.FirePins = 0b00;
        tia.FirePins = BothUp;
        Assert.Equal(0x00, tia.Read(Inpt4));
        Assert.Equal(0x00, tia.Read(Inpt5));

        tia.Write(Vblank, 0x00);
        Assert.Equal(0x80, tia.Read(Inpt4));
        Assert.Equal(0x80, tia.Read(Inpt5));
    }

    [Fact]
    public void Section_20_2_disabling_resets_the_latch_so_the_next_enable_starts_at_1()
    {
        // While D6 is clear the port reads the pin, so the reset shows only when the latch comes back
        // in: a press latched, the latch disabled and enabled again with the button up reads 1
        // ("Disable latches (D6 = 0) also resets latches to logic true", the SPG's VBLANK).
        var tia = new Tia();
        tia.Write(Vblank, 0x40);
        tia.FirePins = 0b00;
        tia.FirePins = BothUp;
        tia.Write(Vblank, 0x00);
        tia.Write(Vblank, 0x40);
        Assert.Equal(0x80, tia.Read(Inpt4));
        Assert.Equal(0x80, tia.Read(Inpt5));
    }

    [Fact]
    public void Section_20_2_each_latch_holds_its_own_press()
    {
        var tia = new Tia();
        tia.Write(Vblank, 0x40);
        tia.FirePins = RightDown;
        tia.FirePins = BothUp;
        Assert.Equal(0x80, tia.Read(Inpt4));
        Assert.Equal(0x00, tia.Read(Inpt5));
    }

    [Fact]
    public void Section_20_1_one_vblank_write_sets_the_latch_bit_with_the_others()
    {
        // A program that writes $42 starts the blank and enables the latches; one that then writes
        // $02 to keep the blank on disables them, and the latch reads 1 again (section 20.1).
        var tia = new Tia();
        tia.Write(Vblank, 0x42);
        tia.FirePins = LeftDown;
        tia.FirePins = BothUp;
        Assert.Equal(0x00, tia.Read(Inpt4));

        tia.Write(Vblank, 0x02);
        Assert.Equal(0x80, tia.Read(Inpt4));
    }

    [Fact]
    public void Section_20_2_rewriting_vblank_with_d6_set_keeps_a_latched_press()
    {
        // The latch is reset only by disabling it; writing D6 = 1 again leaves it (an SR latch whose
        // set input is the inverted enable, jigo2600 Ports).
        var tia = new Tia();
        tia.Write(Vblank, 0x40);
        tia.FirePins = LeftDown;
        tia.FirePins = BothUp;
        tia.Write(Vblank, 0xC0);
        Assert.Equal(0x00, tia.Read(Inpt4));
    }

    public static TheoryData<int, byte, byte> PaddlePorts()
    {
        var data = new TheoryData<int, byte, byte>();
        foreach (int port in new[] { 0x08, 0x09, 0x0A, 0x0B })
        {
            foreach (byte vblank in new byte[] { 0x00, 0x80, 0xC0, 0x40 })
            {
                foreach (byte pins in new byte[] { BothUp, 0b00 })
                {
                    data.Add(port, vblank, pins);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PaddlePorts))]
    public void Section_20_3_inpt0_to_inpt3_read_0_with_no_paddle_connected(int port, byte vblank, byte firePins)
    {
        // Nothing in the console pulls these pins up, so with joysticks in both ports a port reads 0,
        // the dump on or off, and the fire buttons do not reach it (tia.md section 20.3).
        var tia = new Tia();
        tia.Write(Vblank, 0x80);
        tia.Write(Vblank, vblank);
        tia.FirePins = firePins;
        Assert.Equal(0x00, tia.Read(port));
    }

    [Fact]
    public void Section_20_2_power_on_disables_the_latch_and_keeps_the_pins_a_choice()
    {
        // The latch's power-on state is a choice (tia.md open item 13): disabled, so the port reads
        // the pin. A button held through a power cycle is still held: the pins are the outside world.
        var tia = new Tia();
        tia.Write(Vblank, 0x40);
        tia.FirePins = LeftDown;
        tia.FirePins = BothUp;
        tia.FirePins = RightDown;
        tia.PowerOn();

        Assert.Equal(RightDown, tia.FirePins);
        Assert.Equal(0x80, tia.Read(Inpt4));
        Assert.Equal(0x00, tia.Read(Inpt5));
    }

    [Fact]
    public void An_input_read_drives_d7_only()
    {
        var tia = new Tia();
        tia.FirePins = BothUp;
        for (int register = 0x08; register <= 0x0D; register++)
        {
            Assert.Equal(0, tia.Read(register) & 0x7F);
            Assert.Equal(0x80, Tia.DrivenLines(register));
        }
    }
}
