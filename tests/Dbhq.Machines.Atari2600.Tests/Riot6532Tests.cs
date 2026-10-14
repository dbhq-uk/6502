using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The 6532 RIOT, chip alone, from the worked examples in <c>docs/atari-2600/facts/riot.md</c>.
/// Each test names the section or worked example it is taken from.
/// </summary>
/// <remarks>
/// The chip is driven as the bus will drive it (task 4): at the start of every CPU cycle
/// <see cref="Riot6532.Tick"/>, then that cycle's access, if it reaches the chip. Cycle numbers are
/// the sheet's: cycle 0 is the cycle of the timer write.
/// </remarks>
public class Riot6532Tests
{
    private const ushort Swcha = 0x0280;
    private const ushort Swacnt = 0x0281;
    private const ushort Swchb = 0x0282;
    private const ushort Swbcnt = 0x0283;
    private const ushort Intim = 0x0284;
    private const ushort Timint = 0x0285;
    private const ushort Pa7Negative = 0x0284;
    private const ushort Pa7Positive = 0x0285;
    private const ushort Tim1t = 0x0294;
    private const ushort Tim8t = 0x0295;
    private const ushort Tim64t = 0x0296;
    private const ushort T1024t = 0x0297;

    /// <summary>A RIOT and the cycle it has reached: each <see cref="To"/> ticks once per cycle.</summary>
    private sealed class Clock
    {
        private long _cycle = -1;

        public Clock(Riot6532? riot = null) => Riot = riot ?? new Riot6532();

        public Riot6532 Riot { get; }

        public void To(long cycle)
        {
            Assert.True(cycle >= _cycle, $"cycle {cycle} is before cycle {_cycle}");
            while (_cycle < cycle)
            {
                Riot.Tick();
                _cycle++;
            }
        }

        public void Write(long cycle, ushort address, byte value)
        {
            To(cycle);
            Riot.WriteRegister(address, value);
        }

        public byte Read(long cycle, ushort address)
        {
            To(cycle);
            return Riot.ReadRegister(address);
        }
    }

    // Section 4: RAM.

    [Fact]
    public void Ram_holds_all_128_bytes_and_no_two_addresses_share_a_cell()
    {
        var riot = new Riot6532();
        Assert.Equal(128, Riot6532.RamSize);
        for (int i = 0; i < Riot6532.RamSize; i++)
        {
            riot.WriteRam(i, (byte)(i ^ 0xA5));
        }

        for (int i = 0; i < Riot6532.RamSize; i++)
        {
            Assert.Equal((byte)(i ^ 0xA5), riot.ReadRam(i));
        }

        // The address-identity march of the suite's riot/ram: a write to one cell changes no other.
        for (int i = 0; i < Riot6532.RamSize; i++)
        {
            riot.WriteRam(i, 0xFF);
            for (int j = 0; j < Riot6532.RamSize; j++)
            {
                Assert.Equal(j <= i ? (byte)0xFF : (byte)(j ^ 0xA5), riot.ReadRam(j));
            }
        }
    }

    [Fact]
    public void Ram_offsets_above_127_reach_the_cell_a0_to_a6_pick()
    {
        // The bus may pass a mirror's address whole: $0180 + i is cell i (riot.md section 3, mirrors).
        var riot = new Riot6532();
        for (int i = 0; i < Riot6532.RamSize; i++)
        {
            riot.WriteRam(0x180 + i, (byte)(i ^ 0x3C));
        }

        for (int i = 0; i < Riot6532.RamSize; i++)
        {
            Assert.Equal((byte)(i ^ 0x3C), riot.ReadRam(i));
            Assert.Equal((byte)(i ^ 0x3C), riot.ReadRam(0x0D80 + i));
        }
    }

    // Section 3, worked example 1: the decode inside the I/O half.

    [Fact]
    public void Worked_example_1_every_read_mirror_of_intim_and_timint_answers()
    {
        foreach (ushort address in new ushort[] { 0x0284, 0x0286, 0x028C, 0x028E, 0x0294, 0x0296, 0x029C, 0x029E })
        {
            var clock = new Clock();
            clock.Write(0, Tim8t, 0x40);
            Assert.Equal(0x3F, clock.Read(1, address));
        }

        foreach (ushort address in new ushort[] { 0x0285, 0x0287, 0x028D, 0x028F, 0x0295, 0x0297, 0x029D, 0x029F })
        {
            var clock = new Clock();
            clock.Write(0, Tim1t, 0x00);
            Assert.Equal(0x80, clock.Read(1, address));
        }
    }

    [Fact]
    public void Worked_example_1_the_port_registers_repeat_every_eight_and_ignore_a5_and_a6()
    {
        var riot = new Riot6532();
        riot.WriteRegister(Swacnt, 0x5A);
        riot.WriteRegister(Swbcnt, 0x3C);
        foreach (int offset in new[] { 0x00, 0x08, 0x10, 0x18, 0x20, 0x40, 0x60 })
        {
            Assert.Equal(0x5A, riot.ReadRegister((ushort)(Swacnt + offset)));
            Assert.Equal(0x3C, riot.ReadRegister((ushort)(Swbcnt + offset)));
        }

        // Write $02A1: SWACNT, A5 free.
        riot.WriteRegister(0x02A1, 0x81);
        Assert.Equal(0x81, riot.ReadRegister(Swacnt));
    }

    [Fact]
    public void Worked_example_1_a_write_with_a4_clear_is_edge_control_and_not_a_timer_write()
    {
        var clock = new Clock();
        clock.Write(0, Tim8t, 0x10);
        clock.Write(5, Pa7Negative, 0x77);
        clock.Write(6, 0x028D, 0x77);
        Assert.Equal(0x0E, clock.Read(9, Intim));
    }

    [Fact]
    public void Worked_example_1_write_029e_is_tim64t_with_the_interrupt_enabled()
    {
        var clock = new Clock();
        clock.Write(0, 0x029E, 0x01);
        clock.To(64);
        Assert.Equal(0x00, clock.Riot.PeekRegister(Intim));
        Assert.False(clock.Riot.Irq);
        clock.To(65);
        Assert.Equal(0xFF, clock.Riot.PeekRegister(Intim));
        Assert.True(clock.Riot.TimerFlag);
        Assert.True(clock.Riot.Irq);
    }

    [Fact]
    public void Worked_example_1_read_028c_is_intim_and_sets_the_timer_interrupt_enable()
    {
        var clock = new Clock();
        clock.Write(0, Tim1t, 0x05);
        Assert.Equal(0x04, clock.Read(1, 0x028C));
        clock.To(6);
        Assert.True(clock.Riot.TimerFlag);
        Assert.True(clock.Riot.Irq);
    }

    // Section 5: ports A and B.

    [Fact]
    public void Worked_example_2_port_a_output_register_keeps_all_eight_bits()
    {
        var riot = new Riot6532();
        Assert.Equal(0xFF, riot.PortAInput);

        riot.WriteRegister(Swacnt, 0x00);
        riot.WriteRegister(Swcha, 0x55);
        Assert.Equal(0x00, riot.ReadRegister(Swacnt));
        Assert.Equal(0xFF, riot.ReadRegister(Swcha));

        riot.WriteRegister(Swacnt, 0x0F);
        Assert.Equal(0xF5, riot.ReadRegister(Swcha));

        riot.WriteRegister(Swcha, 0xAA);
        Assert.Equal(0xFA, riot.ReadRegister(Swcha));

        riot.WriteRegister(Swacnt, 0xFF);
        Assert.Equal(0xAA, riot.ReadRegister(Swcha));

        riot.WriteRegister(Swacnt, 0x00);
        Assert.Equal(0xFF, riot.ReadRegister(Swcha));

        riot.WriteRegister(Swacnt, 0xFF);
        Assert.Equal(0xAA, riot.ReadRegister(Swcha));
        Assert.Equal(0xAA, riot.PortAPins);

        // The suite's riot/io-ports: with every bit an output, writes read back unchanged.
        foreach (byte value in new byte[] { 0x55, 0xAA, 0xFF, 0x00 })
        {
            riot.WriteRegister(Swcha, value);
            Assert.Equal(value, riot.ReadRegister(Swcha));
        }
    }

    [Fact]
    public void Section_5_an_input_pin_reads_what_drives_it()
    {
        var riot = new Riot6532 { PortAInput = 0x6F, PortBInput = 0x3F };
        Assert.Equal(0x6F, riot.ReadRegister(Swcha));
        Assert.Equal(0x6F, riot.PortAPins);
        Assert.Equal(0x3F, riot.ReadRegister(Swchb));
        Assert.Equal(0x3F, riot.PortBPins);
    }

    [Fact]
    public void Worked_example_3_port_b_reads_its_output_register_for_output_bits()
    {
        var riot = new Riot6532 { PortBInput = 0x3F };
        riot.WriteRegister(Swbcnt, 0x34);
        riot.WriteRegister(Swchb, 0x00);
        Assert.Equal(0x34, riot.ReadRegister(Swbcnt));
        Assert.Equal(0x0B, riot.ReadRegister(Swchb));
        Assert.Equal(0x0B, riot.PortBPins);

        // Push-pull: an output bit reads ORB whatever drives the pin from outside.
        riot.PortBInput = 0x00;
        riot.WriteRegister(Swbcnt, 0xFF);
        riot.WriteRegister(Swchb, 0xFF);
        Assert.Equal(0xFF, riot.ReadRegister(Swchb));
    }

    [Fact]
    public void Section_5_a_port_a_output_reads_low_while_its_switch_pulls_it_low()
    {
        // riot.md section 5, an inference the sheet marks [guessing - verify]; task 9 decides.
        var riot = new Riot6532 { PortAInput = 0x7F };
        riot.WriteRegister(Swacnt, 0xFF);
        riot.WriteRegister(Swcha, 0xFF);
        Assert.Equal(0x7F, riot.ReadRegister(Swcha));

        // An output at 0 reads 0 whatever the switch.
        riot.PortAInput = 0xFF;
        riot.WriteRegister(Swcha, 0x00);
        Assert.Equal(0x00, riot.ReadRegister(Swcha));
    }

    // Section 6: the timer.

    public static TheoryData<ushort, int> Dividers => new()
    {
        { Tim1t, 1 },
        { Tim8t, 8 },
        { Tim64t, 64 },
        { T1024t, 1024 },
    };

    /// <summary>What a read on <paramref name="cycle"/> sees after a write of 3 on cycle 0.</summary>
    private static (byte Count, bool Flag) ReadAfterWritingThree(ushort timer, long cycle)
    {
        var count = new Clock();
        count.Write(0, timer, 3);
        byte value = count.Read(cycle, Intim);

        var flag = new Clock();
        flag.Write(0, timer, 3);
        bool set = (flag.Read(cycle, Timint) & 0x80) != 0;
        return (value, set);
    }

    [Theory]
    [MemberData(nameof(Dividers))]
    public void Worked_example_4_write_three_at_each_divider(ushort timer, int divider)
    {
        int d = divider;
        (long Cycle, byte Count, bool Flag)[] expected =
        [
            (1, 0x02, false),
            (d, 0x02, false),
            (d + 1, 0x01, false),
            (2 * d, 0x01, false),
            (2 * d + 1, 0x00, false),
            (3 * d, 0x00, false),
            (3 * d + 1, 0xFF, true),
            (3 * d + 2, 0xFE, true),
            (3 * d + 3, 0xFD, true),
        ];
        foreach (var (cycle, count, flag) in expected)
        {
            var seen = ReadAfterWritingThree(timer, cycle);
            Assert.True(seen == (count, flag), $"cycle {cycle}: expected ({count:X2}, {flag}), read ({seen.Count:X2}, {seen.Flag})");
        }
    }

    [Theory]
    [MemberData(nameof(Dividers))]
    public void Section_6_the_count_on_every_cycle_by_the_rule(ushort timer, int divider)
    {
        // A read on cycle n, 1 <= n <= D x N, returns N - 1 - floor((n - 1) / D); then $FF, once a
        // cycle down. Reads before zero change nothing but the enable, so one chip is read each cycle.
        const int n = 5;
        var clock = new Clock();
        clock.Write(0, timer, n);
        int end = divider * n + 300;
        for (int cycle = 1; cycle <= end; cycle++)
        {
            int expected = cycle <= divider * n
                ? n - 1 - ((cycle - 1) / divider)
                : 0xFF - (cycle - (divider * n) - 1);
            clock.To(cycle);
            Assert.Equal((byte)expected, clock.Riot.PeekRegister(Intim));
            if (cycle <= divider * n)
            {
                Assert.Equal((byte)expected, clock.Read(cycle, Intim));
            }
        }
    }

    [Fact]
    public void Section_6_the_data_sheets_own_case_52_at_divide_by_8()
    {
        Assert.Equal(51, ReadAfter(1));
        Assert.Equal(50, ReadAfter(9));
        Assert.Equal(49, ReadAfter(17));
        Assert.Equal(25, ReadAfter(213));
        Assert.Equal(0, ReadAfter(415));
        Assert.Equal(0, ReadAfter(416));
        Assert.Equal(0xFF, ReadAfter(417));
        Assert.Equal(0xAC, ReadAfter(500));

        static byte ReadAfter(long cycle)
        {
            var clock = new Clock();
            clock.Write(0, Tim8t, 52);
            return clock.Read(cycle, Intim);
        }
    }

    [Fact]
    public void Section_6_a_write_of_zero_underflows_at_once()
    {
        var clock = new Clock();
        clock.Write(0, Tim64t, 0);
        Assert.False(clock.Riot.TimerFlag);
        clock.To(1);
        Assert.True(clock.Riot.TimerFlag);
        Assert.Equal(0xFF, clock.Riot.PeekRegister(Intim));
        clock.To(2);
        Assert.Equal(0xFE, clock.Riot.PeekRegister(Intim));
    }

    [Fact]
    public void Section_6_a_timer_write_clears_the_flag_and_ends_the_free_run()
    {
        var clock = new Clock();
        clock.Write(0, Tim1t, 1);
        clock.To(10);
        Assert.True(clock.Riot.TimerFlag);
        clock.Write(11, Tim8t, 2);
        Assert.False(clock.Riot.TimerFlag);
        Assert.Equal(0x01, clock.Read(12, Intim));
        Assert.Equal(0x01, clock.Read(19, Intim));
        Assert.Equal(0x00, clock.Read(20, Intim));
    }

    [Fact]
    public void Worked_example_5_the_flags_clearing_rules()
    {
        var clock = new Clock();
        clock.Write(0, Tim64t, 3);
        Assert.False(clock.Riot.TimerFlag);
        Assert.Equal(0x00, clock.Read(150, Timint) & 0x80);
        Assert.False(clock.Riot.TimerFlag);
        Assert.Equal(0x80, clock.Read(200, Timint) & 0x80);
        Assert.True(clock.Riot.TimerFlag);
        Assert.Equal(0xF4, clock.Read(204, Intim));
        Assert.False(clock.Riot.TimerFlag);
        Assert.Equal(0xF4, clock.Read(210, Intim));
        Assert.False(clock.Riot.TimerFlag);

        // The divider is 64 again from the read on 204 [guessing - verify], riot.md section 12 item 1.
        Assert.Equal(0xF4, clock.Riot.PeekRegister(Intim));
        clock.To(267);
        Assert.Equal(0xF4, clock.Riot.PeekRegister(Intim));
        clock.To(268);
        Assert.Equal(0xF3, clock.Riot.PeekRegister(Intim));

        clock.Write(270, Tim8t, 5);
        Assert.False(clock.Riot.TimerFlag);
    }

    [Theory]
    [InlineData(16, 0x00, true)]
    [InlineData(15, 0xFF, true)]
    [InlineData(14, 0xFE, false)]
    public void Worked_example_6_the_read_on_the_cycle_it_passes_zero(int v, byte intim, bool flagOnCycle23)
    {
        // riot/timer-underflow-read: STX TIM1T on cycle 0, LDA INTIM reads on 16, TIMINT read on 23.
        var clock = new Clock();
        clock.Write(0, Tim1t, (byte)v);
        Assert.Equal(intim, clock.Read(16, Intim));
        Assert.Equal(flagOnCycle23, (clock.Read(23, Timint) & 0x80) != 0);
    }

    [Fact]
    public void Worked_example_7_the_timers_edge_at_one_cycle_resolution()
    {
        // riot/timer-vblank-spin: write 8 to TIM64T, then INX; LDA INTIM; BNE until INTIM reads 0.
        int[] expected = [46, 47, 47, 47, 47, 47, 48, 48];
        for (int idx = 0; idx < 8; idx++)
        {
            var clock = new Clock();
            clock.Write(0, Tim64t, 8);
            int passes = 0;
            for (int j = 1; ; j++)
            {
                long cycle = 29 + (2 * (8 - idx)) + (9 * (j - 1));
                if (clock.Read(cycle, Intim) == 0)
                {
                    passes = j;
                    break;
                }
            }

            Assert.True(expected[idx] == passes, $"IDX {idx}: expected {expected[idx]} passes, counted {passes}");
        }
    }

    [Fact]
    public void Section_6_reading_timint_does_not_clear_the_timer_flag()
    {
        var clock = new Clock();
        clock.Write(0, Tim1t, 0);
        for (int cycle = 2; cycle < 10; cycle++)
        {
            Assert.Equal(0x80, clock.Read(cycle, Timint) & 0x80);
        }

        Assert.True(clock.Riot.TimerFlag);
    }

    [Fact]
    public void Section_12_item_2_an_intim_read_on_the_flags_cycle_does_not_end_the_free_run()
    {
        // [guessing - verify]: the read that leaves the flag set leaves the free run too.
        var clock = new Clock();
        clock.Write(0, Tim8t, 3);
        Assert.Equal(0xFF, clock.Read(25, Intim));
        Assert.True(clock.Riot.TimerFlag);
        Assert.Equal(0xFE, clock.Read(26, Intim));
        Assert.False(clock.Riot.TimerFlag);
    }

    [Fact]
    public void Section_6_the_free_run_wraps_and_only_the_first_pass_of_zero_protects_a_read()
    {
        // Past $00 again the count wraps to $FF with the flag still set; that is not a new
        // interrupt, so a read there clears the flag [guessing - verify].
        var clock = new Clock();
        clock.Write(0, Tim1t, 0);
        clock.To(256);
        Assert.Equal(0x00, clock.Riot.PeekRegister(Intim));
        Assert.True(clock.Riot.TimerFlag);
        Assert.Equal(0xFF, clock.Read(257, Intim));
        Assert.False(clock.Riot.TimerFlag);
    }

    // Section 7: the flag register and the IRQ pin.

    [Fact]
    public void Section_7_timint_drives_bit_7_the_timer_bit_6_the_edge_and_zero_below()
    {
        var clock = new Clock();
        clock.Riot.WriteRegister(Swacnt, 0x80);
        clock.Riot.WriteRegister(Swcha, 0x00);
        clock.Write(0, Tim1t, 0);
        Assert.Equal(0xC0, clock.Read(2, Timint));
        Assert.Equal(0x80, clock.Read(3, Timint));
    }

    [Fact]
    public void Worked_example_8_the_timer_enable_follows_a3_on_every_timer_read_and_write()
    {
        var enabled = new Clock();
        enabled.Write(0, 0x029C, 2);
        enabled.To(3);
        Assert.True(enabled.Riot.TimerFlag);
        Assert.True(enabled.Riot.Irq);

        // A TIMINT read does not touch the enable, whatever its A3: at $0285 A3 is 0, which on an
        // INTIM read would turn the enable off.
        enabled.Read(5, 0x0285);
        Assert.True(enabled.Riot.Irq);
        enabled.Read(6, 0x028D);
        Assert.True(enabled.Riot.Irq);

        // Nor does an edge control write, which has A3 0 at $0286 (A1 turns the edge's own enable on).
        enabled.Write(7, 0x0286, 0);
        Assert.True(enabled.Riot.Irq);

        Assert.Equal(0xF8, enabled.Read(10, Intim));
        Assert.False(enabled.Riot.TimerFlag);
        Assert.False(enabled.Riot.Irq);

        var disabled = new Clock();
        disabled.Write(0, Tim1t, 2);
        disabled.To(3);
        Assert.True(disabled.Riot.TimerFlag);
        Assert.False(disabled.Riot.Irq);
    }

    // Section 8: the PA7 edge detect.

    [Fact]
    public void Worked_example_9_pa7_edges()
    {
        var clock = new Clock();
        long c = 0;
        clock.Write(c++, Swacnt, 0x80);
        clock.Write(c++, Swcha, 0x80);
        clock.Write(c++, Pa7Negative, 0x00);

        // The suite reads twice: the first read clears what setting up may have left.
        clock.Read(c++, Timint);
        Assert.Equal(0x00, clock.Read(c++, Timint) & 0x40);

        clock.Write(c++, Swcha, 0x00);
        Assert.Equal(0x40, clock.Read(c++, Timint) & 0x40);
        Assert.Equal(0x00, clock.Read(c++, Timint) & 0x40);

        clock.Write(c++, Swcha, 0x80);
        Assert.Equal(0x00, clock.Read(c++, Timint) & 0x40);

        clock.Write(c++, Pa7Positive, 0x00);
        Assert.Equal(0x00, clock.Read(c++, Timint) & 0x40);
        clock.Write(c++, Swcha, 0x00);
        Assert.Equal(0x00, clock.Read(c++, Timint) & 0x40);

        clock.Write(c++, Swcha, 0x80);
        Assert.Equal(0x40, clock.Read(c++, Timint) & 0x40);

        clock.Write(c++, Tim1t, 2);
        c += 10;
        clock.Write(c++, Swcha, 0x00);
        clock.Write(c++, Swcha, 0x80);
        Assert.Equal(0xC0, clock.Read(c++, Timint) & 0xC0);
        Assert.Equal(0x80, clock.Read(c++, Timint) & 0xC0);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0x80)]
    public void Section_8_arming_sets_nothing_whatever_the_level_and_the_polarity(byte level)
    {
        // riot/pa7-polarity measured it on a console; the data sheet's "may" is not followed.
        var riot = new Riot6532();
        riot.WriteRegister(Swacnt, 0x80);
        riot.WriteRegister(Swcha, level);
        riot.ReadRegister(Timint);
        foreach (ushort arm in new ushort[] { Pa7Positive, Pa7Negative, Pa7Negative, Pa7Positive, Pa7Positive, 0x0287, 0x0286 })
        {
            riot.WriteRegister(arm, 0xFF);
            Assert.False(riot.EdgeFlag);
        }
    }

    [Fact]
    public void Section_8_the_edge_control_data_is_ignored()
    {
        foreach (byte data in new byte[] { 0x00, 0x01, 0xFE, 0xFF })
        {
            var positive = new Riot6532();
            positive.WriteRegister(Swacnt, 0x80);
            positive.WriteRegister(Pa7Positive, data);
            positive.ReadRegister(Timint);
            positive.WriteRegister(Swcha, 0x80);
            Assert.True(positive.EdgeFlag);

            var negative = new Riot6532();
            negative.WriteRegister(Swacnt, 0x80);
            negative.WriteRegister(Pa7Negative, data);
            negative.ReadRegister(Timint);
            negative.WriteRegister(Swcha, 0x80);
            Assert.False(negative.EdgeFlag);
        }
    }

    [Fact]
    public void Section_8_an_edge_from_the_joystick_or_a_direction_change_is_an_edge()
    {
        // The detector watches the pin, as an input or an output.
        var riot = new Riot6532();
        riot.PortAInput = 0x7F;
        Assert.True(riot.EdgeFlag);
        riot.ReadRegister(Timint);
        riot.PortAInput = 0xFF;
        Assert.False(riot.EdgeFlag);

        // ORA bit 7 is 0 after power on; making PA7 an output pulls the pin low.
        riot.WriteRegister(Swacnt, 0x80);
        Assert.True(riot.EdgeFlag);
    }

    [Fact]
    public void Section_8_only_a_timint_read_clears_the_edge_flag()
    {
        var clock = new Clock();
        clock.Write(0, Swacnt, 0x80);
        clock.Write(1, Swcha, 0x00);
        Assert.True(clock.Riot.EdgeFlag);
        clock.Read(2, Intim);
        clock.Write(3, Tim8t, 9);
        clock.Read(4, 0x028C);
        clock.Write(5, Pa7Positive, 0);
        clock.Write(6, Pa7Negative, 0);
        clock.Read(7, Swcha);
        clock.Read(8, Swchb);
        clock.Read(9, Swacnt);
        clock.Read(10, Swbcnt);
        Assert.True(clock.Riot.EdgeFlag);
        Assert.Equal(0x40, clock.Read(11, Timint) & 0x40);
        Assert.False(clock.Riot.EdgeFlag);
    }

    [Fact]
    public void Section_7_the_edge_enable_gates_the_irq_pin_and_never_the_flag()
    {
        var riot = new Riot6532();
        riot.WriteRegister(Swacnt, 0x80);
        riot.WriteRegister(Swcha, 0x80);

        riot.WriteRegister(Pa7Negative, 0);
        riot.WriteRegister(Swcha, 0x00);
        Assert.True(riot.EdgeFlag);
        Assert.False(riot.Irq);

        // $0286: A1 = 1, the interrupt on, still negative.
        riot.WriteRegister(0x0286, 0);
        Assert.True(riot.Irq);
        riot.ReadRegister(Timint);
        Assert.False(riot.Irq);

        // $0287: positive, with the interrupt.
        riot.WriteRegister(0x0287, 0);
        riot.WriteRegister(Swcha, 0x80);
        Assert.True(riot.EdgeFlag);
        Assert.True(riot.Irq);
    }

    // Section 9: power on.

    [Fact]
    public void Section_9_power_on()
    {
        var clock = new Clock();
        Riot6532 riot = clock.Riot;
        for (int i = 0; i < Riot6532.RamSize; i++)
        {
            riot.WriteRam(i, 0x5A);
        }

        clock.Write(0, Swacnt, 0xFF);
        clock.Write(1, Swcha, 0x00);
        clock.Write(2, Swbcnt, 0xFF);
        clock.Write(3, Swchb, 0x00);
        clock.Write(4, 0x0287, 0);
        clock.Write(5, 0x029F, 0x01);
        clock.To(2000);
        Assert.True(riot.TimerFlag);
        Assert.True(riot.EdgeFlag);
        Assert.True(riot.Irq);

        riot.PowerOn();
        Assert.False(riot.TimerFlag);
        Assert.False(riot.EdgeFlag);
        Assert.False(riot.Irq);

        // RES: the four I/O registers zeroed, so every pin an input; both interrupts off.
        Assert.Equal(0x00, riot.PeekRegister(Swacnt));
        Assert.Equal(0x00, riot.PeekRegister(Swbcnt));
        Assert.Equal(0xFF, riot.PeekRegister(Swcha));
        Assert.Equal(0xFF, riot.PeekRegister(Swchb));
        Assert.Equal(0x00, riot.PeekRegister(Timint));

        riot.WriteRegister(Swacnt, 0xFF);
        riot.WriteRegister(Swbcnt, 0xFF);
        Assert.Equal(0x00, riot.PeekRegister(Swcha));
        Assert.Equal(0x00, riot.PeekRegister(Swchb));
        Assert.True(riot.EdgeFlag);
        Assert.False(riot.Irq);

        // The model's choices (riot.md section 9, docs/known-differences.md): RAM clear; the count at
        // $00 at divide by 1, so the first cycle passes zero and the timer free-runs, as the real one
        // does by the time the program starts.
        for (int i = 0; i < Riot6532.RamSize; i++)
        {
            Assert.Equal(0x00, riot.ReadRam(i));
        }

        Assert.Equal(0x00, riot.PeekRegister(Intim));
        riot.Tick();
        Assert.Equal(0xFF, riot.PeekRegister(Intim));
        Assert.True(riot.TimerFlag);
        Assert.False(riot.Irq);
        riot.Tick();
        Assert.Equal(0xFE, riot.PeekRegister(Intim));
    }

    [Fact]
    public void Section_9_power_on_arms_the_negative_edge()
    {
        var riot = new Riot6532();
        riot.WriteRegister(Pa7Positive, 0);
        riot.PowerOn();
        riot.WriteRegister(Swcha, 0x80);
        riot.WriteRegister(Swacnt, 0x80);
        Assert.False(riot.EdgeFlag);
        riot.WriteRegister(Swcha, 0x00);
        Assert.True(riot.EdgeFlag);
    }

    [Fact]
    public void Section_9_a_new_chip_is_powered_on()
    {
        var fresh = new Riot6532();
        var powered = new Riot6532();
        powered.PowerOn();
        for (ushort address = 0x0280; address < 0x02A0; address++)
        {
            Assert.Equal(powered.PeekRegister(address), fresh.PeekRegister(address));
        }

        Assert.Equal(powered.TimerFlag, fresh.TimerFlag);
        Assert.Equal(powered.EdgeFlag, fresh.EdgeFlag);

        // The same from then on: the timer's next cycles, and an edge on PA7 from the joystick.
        for (int cycle = 0; cycle < 3; cycle++)
        {
            fresh.Tick();
            powered.Tick();
            Assert.Equal(powered.PeekRegister(Intim), fresh.PeekRegister(Intim));
            Assert.Equal(powered.TimerFlag, fresh.TimerFlag);
        }

        fresh.PortAInput = 0x7F;
        powered.PortAInput = 0x7F;
        Assert.True(powered.EdgeFlag);
        Assert.Equal(powered.EdgeFlag, fresh.EdgeFlag);
    }

    // PeekRegister: the bus's Peek, with no side effect.

    [Fact]
    public void Peek_has_no_side_effect()
    {
        // Two chips run one script; one is also peeked at every register on every cycle. Every
        // read, and the flags, must agree: through an edge and its clearing, the cycle the timer
        // passes zero, a read on that cycle, and a read that ends the free run.
        (long Cycle, bool Write, ushort Address, byte Value)[] script =
        [
            (0, true, Swacnt, 0x80),
            (1, true, Swcha, 0x00),
            (2, true, 0x029C, 3),
            (4, false, Swcha, 0),
            (5, false, 0x028C, 0),
            (6, false, 0x028C, 0),
            (7, false, Timint, 0),
            (9, false, Timint, 0),
            (12, false, Intim, 0),
            (20, true, Swcha, 0x80),
            (21, true, Swcha, 0x00),
            (30, false, Timint, 0),
            (40, false, 0x028C, 0),
            (60, false, Timint, 0),
        ];
        var plain = new Clock();
        var peeked = new Clock();
        int next = 0;
        for (long cycle = 0; cycle < 80; cycle++)
        {
            plain.To(cycle);
            peeked.To(cycle);
            var peeks = new byte[0x20];
            for (int a = 0; a < 0x20; a++)
            {
                peeks[a] = peeked.Riot.PeekRegister((ushort)(0x0280 + a));
            }

            if (next < script.Length && script[next].Cycle == cycle)
            {
                var (_, write, address, value) = script[next++];
                if (write)
                {
                    plain.Write(cycle, address, value);
                    peeked.Write(cycle, address, value);
                }
                else
                {
                    byte read = plain.Read(cycle, address);
                    Assert.Equal(read, peeked.Read(cycle, address));
                    Assert.Equal(read, peeks[address - 0x0280]);
                }
            }

            Assert.Equal(plain.Riot.TimerFlag, peeked.Riot.TimerFlag);
            Assert.Equal(plain.Riot.EdgeFlag, peeked.Riot.EdgeFlag);
            Assert.Equal(plain.Riot.Irq, peeked.Riot.Irq);
            Assert.Equal(plain.Riot.PeekRegister(Intim), peeked.Riot.PeekRegister(Intim));
        }

        Assert.Equal(script.Length, next);
    }

    [Fact]
    public void Peek_shows_the_flag_register_without_clearing_the_edge()
    {
        var riot = new Riot6532();
        riot.WriteRegister(Swacnt, 0x80);
        riot.WriteRegister(Swcha, 0x00);
        Assert.Equal(0x40, riot.PeekRegister(Timint) & 0x40);
        Assert.True(riot.EdgeFlag);
        Assert.Equal(0x40, riot.ReadRegister(Timint) & 0x40);
        Assert.False(riot.EdgeFlag);
    }
}
