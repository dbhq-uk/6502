using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The bus, from <c>docs/atari-2600/facts/bus.md</c>: the 13-bit decode and every mirror
/// (sections 1 and 2), one cycle a call (AGENTS.md rule 1), the floating bus (section 4), and no
/// interrupt line (section 1). The WSYNC halt is in <c>WsyncHaltTests</c>.
/// </summary>
public class Atari2600BusTests
{
    private static Atari2600Bus PoweredBus(Region region, byte[]? image = null)
    {
        var bus = new Atari2600Bus(Cartridge.Load(image ?? TestImages.Pattern(4096)), region);
        bus.PowerOn();
        return bus;
    }

    // The part bus.md section 2 names for an address the core puts out, from its bits alone:
    // A13 to A15 dropped; A12 the cartridge; else A7 low the TIA; else A9 the RIOT's RAM or I/O.
    private static Atari2600Bus.Chip PartFromTheBits(int address)
    {
        bool a12 = (address & (1 << 12)) != 0;
        bool a9 = (address & (1 << 9)) != 0;
        bool a7 = (address & (1 << 7)) != 0;
        if (a12)
        {
            return Atari2600Bus.Chip.Cartridge;
        }

        if (!a7)
        {
            return Atari2600Bus.Chip.Tia;
        }

        return a9 ? Atari2600Bus.Chip.RiotIo : Atari2600Bus.Chip.RiotRam;
    }

    [Fact]
    public void Section_2_every_address_reaches_the_part_its_bits_name()
    {
        for (int address = 0; address <= 0xFFFF; address++)
        {
            Assert.Equal(PartFromTheBits(address), Atari2600Bus.Decode((ushort)address));
        }
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Section_2_peek_gives_each_parts_byte_at_every_address(string regionName)
    {
        byte[] image = TestImages.Pattern(4096);
        Atari2600Bus bus = PoweredBus(TestPrograms.RegionNamed(regionName), image);

        // Odd bytes in RAM, so no cell reads as a TIA register does with the bus at $00.
        for (int i = 0; i < Riot6532.RamSize; i++)
        {
            bus.PokeRam(i, (byte)((i * 2) + 1));
        }

        for (int address = 0; address <= 0xFFFF; address++)
        {
            int line = address & 0x1FFF;
            byte expected = PartFromTheBits(address) switch
            {
                Atari2600Bus.Chip.Cartridge => image[line & 0xFFF],
                Atari2600Bus.Chip.RiotRam => (byte)(((line & 0x7F) * 2) + 1),
                Atari2600Bus.Chip.RiotIo => bus.Riot.PeekRegister((ushort)line),

                // bus.md section 4 with the bus at $00 from power on, no latch set and both fire
                // buttons up: only INPT4 and INPT5 drive a 1, on D7.
                _ => (line & 0x0F) is 0x0C or 0x0D ? (byte)0x80 : (byte)0x00,
            };
            Assert.Equal(expected, bus.Peek((ushort)address));
        }
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Worked_example_2_the_vectors_and_the_cartridge_mirrors(string regionName)
    {
        Atari2600Bus bus = PoweredBus(TestPrograms.RegionNamed(regionName));
        Assert.Equal(bus.Peek(0x1FFC), bus.Peek(0xFFFC));
        Assert.Equal(bus.Peek(0x1FFD), bus.Peek(0xFFFD));
        Assert.Equal(bus.Peek(0x1000), bus.Peek(0xF000));
        Assert.Equal(bus.Peek(0x1000), bus.Peek(0x3000));
        Assert.Equal(bus.Read(0x1FFC), bus.Read(0xFFFC));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Worked_example_2_one_ram_cell_at_every_mirror_the_stack_included(string regionName)
    {
        Atari2600Bus bus = PoweredBus(TestPrograms.RegionNamed(regionName));
        foreach (ushort mirror in new ushort[] { 0x0080, 0x0180, 0x0480, 0x0580, 0x0880, 0x0980, 0x0C80, 0x0D80, 0x2080, 0xE180 })
        {
            byte value = (byte)(mirror >> 4);
            bus.Write(mirror, value);
            Assert.Equal(value, bus.Read(0x0080));
            Assert.Equal(value, bus.Riot.ReadRam(0));
        }

        // The stack top, $01FF, is RAM byte 127, the cell the program reads at $00FF.
        bus.Write(0x01FF, 0x5C);
        Assert.Equal(0x5C, bus.Read(0x00FF));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Worked_example_2_the_tia_at_0100_to_017F_takes_writes_through_a6(string regionName)
    {
        Atari2600Bus bus = PoweredBus(TestPrograms.RegionNamed(regionName));

        bus.Write(0x011D, 0x02);
        Assert.Equal(0x02, bus.Tia.Written(0x1D));

        // A6 is not decoded on a write: $0143 is RSYNC ($03), as the stack wipe strobes it.
        bus.Write(0x0143, 0x5A);
        Assert.Equal(0x5A, bus.Tia.Written(0x03));

        // $0102 is WSYNC: the halt waits for the next read.
        Assert.False(bus.Tia.RdyLow);
        bus.Write(0x0102, 0x00);
        Assert.True(bus.Tia.RdyLow);
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Section_1_each_call_is_one_cycle_and_three_colour_clocks(string regionName)
    {
        Atari2600Bus bus = PoweredBus(TestPrograms.RegionNamed(regionName));
        ushort[] addresses = [0x1000, 0x0080, 0x0280, 0x0030, 0x0009, 0x0294, 0x1FFF, 0x0181];
        for (int n = 1; n <= 200; n++)
        {
            ushort address = addresses[n % addresses.Length];
            if (n % 3 == 0)
            {
                bus.Write(address, (byte)n);
            }
            else
            {
                bus.Read(address);
            }

            Assert.Equal(n, bus.Cycles);
            Assert.Equal(3L * n, bus.ColourClocks);
        }
    }

    [Fact]
    public void Section_1_the_tia_moves_three_colour_clocks_and_the_riot_one_cycle_a_call()
    {
        Atari2600Bus bus = PoweredBus(Region.Ntsc);
        bus.Write(0x0294, 10);
        for (int n = 1; n <= 10; n++)
        {
            bus.Read(0x1000);
            Assert.Equal((3 * (n + 1)) % 228, bus.Tia.HorizontalCounter);
            Assert.Equal(10 - n, bus.Riot.PeekRegister(0x0284));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Section_4_a_collision_register_drives_d7_and_d6_over_the_residue(bool d7, bool d6)
    {
        Atari2600Bus bus = PoweredBus(Region.Ntsc);

        // CXM0P ($00): D7 is the M0-P1 latch, bit 1 of the latches; D6 the M0-P0 latch, bit 0.
        bus.Tia.CollisionLatches = (ushort)((d7 ? 2 : 0) | (d6 ? 1 : 0));
        byte expected = (byte)((d7 ? 0x80 : 0) | (d6 ? 0x40 : 0) | 0x3F);
        foreach (ushort mirror in new ushort[] { 0x0000, 0x0010, 0x0030, 0x0070, 0x0100, 0x0D40 })
        {
            bus.Write(0x1000, 0x3F);
            Assert.Equal(expected, bus.Read(mirror));
            Assert.Equal(expected, bus.DataBus);
        }
    }

    [Fact]
    public void Section_4_an_input_port_drives_d7_only_and_e_and_f_drive_nothing()
    {
        Atari2600Bus bus = PoweredBus(Region.Ntsc);

        // INPT4, fire button up: D7 is 1, the low seven from the residue.
        bus.Write(0x1000, 0x3F);
        Assert.Equal(0xBF, bus.Read(0x000C));
        bus.Write(0x1000, 0x7F);
        Assert.Equal(0xFF, bus.Read(0x003D));

        // INPT0, with no paddle connected: D7 is 0, the low seven from the residue.
        bus.Write(0x1000, 0xFF);
        Assert.Equal(0x7F, bus.Read(0x0008));

        // $2E and $xF drive nothing: the residue whole.
        bus.Write(0x1000, 0x3F);
        Assert.Equal(0x3F, bus.Read(0x002E));
        bus.Write(0x1000, 0xC5);
        Assert.Equal(0xC5, bus.Read(0x007F));
    }

    [Fact]
    public void Section_4_cxblpf_drives_d7_only_its_d6_taken_to_float()
    {
        // tia.md section 2.2 [guessing - verify]: CXBLPF's D6 is unused and floats.
        Atari2600Bus bus = PoweredBus(Region.Ntsc);
        bus.Tia.CollisionLatches = 0xFFFF;
        bus.Write(0x1000, 0x3F);
        Assert.Equal(0xBF, bus.Read(0x0006));
        bus.Write(0x1000, 0x7F);
        Assert.Equal(0xFF, bus.Read(0x0006));
        bus.Write(0x1000, 0x00);
        Assert.Equal(0xC0, bus.Read(0x0007));
    }

    [Fact]
    public void Section_2_a_write_decodes_a0_to_a5_and_cxclr_clears_the_latches()
    {
        Atari2600Bus bus = PoweredBus(Region.Ntsc);

        // HMBL ($24) through A8 and A6: $0164.
        bus.Write(0x0164, 0x70);
        Assert.Equal(0x70, bus.Tia.Written(0x24));
        Assert.Equal(0x00, bus.Tia.Written(0x04));

        // $2D to $3F select no register: nothing below changes.
        bus.Write(0x003F, 0x99);
        Assert.Equal(0x00, bus.Tia.Written(0x1F));

        bus.Tia.CollisionLatches = 0xFFFF;
        bus.Write(0x006C, 0x00);
        Assert.Equal(0, bus.Tia.CollisionLatches);
        bus.Write(0x1000, 0x00);
        Assert.Equal(0x00, bus.Read(0x0007));
    }

    [Fact]
    public void Peek_changes_nothing_anywhere()
    {
        // TIM1T with the interrupt enabled, an edge armed, a WSYNC, then a loop that reads nothing
        // of the I/O half: LDA #$05; STA $029C; STA $0285; STA WSYNC; JMP to itself.
        byte[] code = [0xA9, 0x05, 0x8D, 0x9C, 0x02, 0x8D, 0x85, 0x02, 0x85, 0x02, 0x4C, 0x0A, 0xF0];
        var machine = new Atari2600(Cartridge.Load(TestPrograms.ImageF8(code)), Region.Ntsc);
        machine.PowerOn();
        machine.Run(40);
        Assert.True(machine.Bus.Riot.TimerFlag);
        string before = StateReportTests.Hash(machine.Bus);
        for (int address = 0; address <= 0xFFFF; address++)
        {
            machine.Bus.Peek((ushort)address);
        }

        // Last of all bank 0's hotspot, so a Peek that switched could not have switched back.
        Assert.Equal(1, machine.Bus.Board.Bank);
        machine.Bus.Peek(0x1FF8);
        Assert.Equal(before, StateReportTests.Hash(machine.Bus));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Power_on_again_puts_every_part_back(string regionName)
    {
        // VSYNC on; TIM1T with its interrupt on; a RAM write; the F8 hotspot of bank 0; WSYNC; loop.
        Region region = TestPrograms.RegionNamed(regionName);
        byte[] code = [0xA9, 0x02, 0x85, 0x00, 0x8D, 0x9C, 0x02, 0x85, 0x80, 0xAD, 0xF8, 0x1F, 0x85, 0x02, 0x4C, 0x0C, 0xF0];
        var fresh = new Atari2600(Cartridge.Load(TestPrograms.ImageF8(code)), region);
        fresh.PowerOn();
        var used = new Atari2600(Cartridge.Load(TestPrograms.ImageF8(code)), region);
        used.PowerOn();
        used.Run(100_000);
        used.Bus.Tia.CollisionLatches = 0x0F0F;
        Assert.NotEqual(StateReportTests.Hash(fresh.Bus), StateReportTests.Hash(used.Bus));

        used.PowerOn();
        Assert.Equal(StateReportTests.Hash(fresh.Bus), StateReportTests.Hash(used.Bus));
        Assert.Equal(fresh.Cpu.PC, used.Cpu.PC);
    }

    [Fact]
    public void Section_4_the_residue_is_the_last_byte_read_or_written()
    {
        Atari2600Bus bus = PoweredBus(Region.Ntsc);
        Assert.Equal(0x00, bus.DataBus);
        bus.Write(0x0080, 0x12);
        Assert.Equal(0x12, bus.DataBus);
        bus.PokeRam(1, 0x34);
        Assert.Equal(0x34, bus.Read(0x0081));
        Assert.Equal(0x34, bus.DataBus);
        Assert.Equal(0x34, bus.Peek(0x002F));
        Assert.Equal(0x34, bus.DataBus);

        // Power on puts it back to $00 (a choice, bus.md section 8 item 3). The CPU's reset then
        // overwrites it, so only the bus alone shows it.
        bus.PowerOn();
        Assert.Equal(0x00, bus.DataBus);
        Assert.Equal(0x00, bus.Peek(0x002F));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Worked_example_4_the_floating_bus_through_the_cpu(string regionName)
    {
        // LDA $2E; STA $90; LDA $013E; STA $91; LDA $70; STA $92; LDA $0970; STA $93;
        // LDA $3C; STA $94; LDA $18; STA $95; then JMP to itself.
        Atari2600 machine = TestPrograms.Machine(
            TestPrograms.RegionNamed(regionName),
            0xA5, 0x2E, 0x85, 0x90,
            0xAD, 0x3E, 0x01, 0x85, 0x91,
            0xA5, 0x70, 0x85, 0x92,
            0xAD, 0x70, 0x09, 0x85, 0x93,
            0xA5, 0x3C, 0x85, 0x94,
            0xA5, 0x18, 0x85, 0x95,
            0x4C, 0x1A, 0xF0);
        for (int i = 0; i < 13; i++)
        {
            machine.Step();
        }

        Assert.Equal(0x2E, machine.Bus.Riot.ReadRam(0x10));
        Assert.Equal(0x01, machine.Bus.Riot.ReadRam(0x11));
        Assert.Equal(0x30, machine.Bus.Riot.ReadRam(0x12));
        Assert.Equal(0x09, machine.Bus.Riot.ReadRam(0x13));
        Assert.Equal(0xBC, machine.Bus.Riot.ReadRam(0x14));
        Assert.Equal(0x18, machine.Bus.Riot.ReadRam(0x15));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Section_1_no_interrupt_line_is_raised_when_the_riot_asks_for_one(string regionName)
    {
        // LDA #$02; STA $029C (TIM1T with its interrupt enabled); CLI; then JMP to itself.
        Atari2600 machine = TestPrograms.Machine(TestPrograms.RegionNamed(regionName), 0xA9, 0x02, 0x8D, 0x9C, 0x02, 0x58, 0x4C, 0x06, 0xF0);
        for (int i = 0; i < 50; i++)
        {
            machine.Step();
            Assert.False(machine.Cpu.Irq);
            Assert.False(machine.Cpu.Nmi);
        }

        Assert.True(machine.Bus.Riot.Irq);
        Assert.Equal(0xF006, machine.Cpu.PC);
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Power_on_resets_the_chips_and_runs_the_cpus_reset_from_the_vector(string regionName)
    {
        Atari2600 machine = TestPrograms.Machine(TestPrograms.RegionNamed(regionName), 0xEA);
        Assert.Equal(TestPrograms.Start, machine.Cpu.PC);
        Assert.Equal(0xFD, machine.Cpu.S);
        Assert.Equal(7, machine.Bus.Cycles);
        Assert.Equal(21, machine.Bus.Tia.HorizontalCounter);
        Assert.Equal(2, machine.Step());
        Assert.Equal(9, machine.Bus.Cycles);
    }

    [Fact]
    public void Run_and_RunFrames_stop_at_whole_instructions()
    {
        // VSYNC on (LDA #2; STA VSYNC), three WSYNCs, VSYNC off (LDA #0; STA VSYNC), then two
        // hundred WSYNCs by a loop (LDX #200; STA WSYNC; DEX; BNE), and round again.
        Atari2600 machine = TestPrograms.Machine(
            Region.Ntsc,
            0xA9, 0x02, 0x85, 0x00, 0x85, 0x02, 0x85, 0x02, 0x85, 0x02,
            0xA9, 0x00, 0x85, 0x00,
            0xA2, 0xC8, 0x85, 0x02, 0xCA, 0xD0, 0xFB,
            0x4C, 0x00, 0xF0);
        machine.Run(1000);
        // An instruction after a WSYNC write may wait up to 75 halted cycles.
        Assert.InRange(machine.Bus.Cycles, 1007, 1007 + 80);
        long frames = machine.Bus.Tia.Frame.Frames;
        machine.RunFrames(3);
        Assert.Equal(frames + 3, machine.Bus.Tia.Frame.Frames);
        Assert.Equal(203, machine.Bus.Tia.Frame.Lines);
    }

    [Fact]
    public void A_program_that_never_ends_vsync_still_ends_a_frame_every_400_lines()
    {
        // STA WSYNC; JMP back: one line a loop, and no VSYNC.
        Atari2600 machine = TestPrograms.Machine(Region.Ntsc, 0x85, 0x02, 0x4C, 0x00, 0xF0);
        machine.RunFrames(2);
        Assert.Equal(2, machine.Bus.Tia.Frame.Frames);
        Assert.Equal(Tia.MaxLines, machine.Bus.Tia.Frame.Lines);
        Assert.InRange(machine.Bus.ColourClocks, 2L * Tia.MaxLines * 228, (2L * Tia.MaxLines * 228) + 228);
    }

    [Fact]
    public void The_region_switch_takes_effect_at_once_and_changes_nothing_else()
    {
        Atari2600 machine = TestPrograms.Machine(Region.Ntsc, 0x85, 0x02, 0x4C, 0x00, 0xF0);
        machine.Run(500);
        long cycles = machine.Bus.Cycles;
        int counter = machine.Bus.Tia.HorizontalCounter;
        machine.Bus.Region = Region.Pal;
        Assert.Same(Region.Pal, machine.Bus.Region);
        Assert.Equal(cycles, machine.Bus.Cycles);
        Assert.Equal(counter, machine.Bus.Tia.HorizontalCounter);
        Assert.Throws<ArgumentNullException>(() => machine.Bus.Region = null!);
    }
}
