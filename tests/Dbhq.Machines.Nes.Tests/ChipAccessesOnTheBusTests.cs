using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The bus's <see cref="NesBus.Accesses"/>: what the CPU's reads and writes add to each
/// <see cref="NesChip"/>'s counter, through the bus's own I/O decode (<c>docs/nes/facts/models.md</c>,
/// "What the counters count"). A peek counts nothing, and the counters start again at power on.
/// </summary>
public class ChipAccessesOnTheBusTests
{
    // A machine whose program is never run: the tests drive the bus's cycles themselves.
    private static Nes IdleMachine()
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1)));
        nes.PowerOn();
        return nes;
    }

    // A machine that runs code at $C000, with the reset vector pointing to it.
    private static Nes ProgramMachine(params byte[] code)
    {
        byte[] prg = new byte[16384];
        code.CopyTo(prg, 0);
        prg[0x3FFC] = 0x00;
        prg[0x3FFD] = 0xC0;
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(TestCartridge.Ines1(1, 1)[..16], prg, new byte[8192])));
        nes.PowerOn();
        return nes;
    }

    // What each chip's counter gained while `act` ran, in NesChip order.
    private static int[] Added(Nes nes, Action act)
    {
        int[] before = nes.Bus.Accesses.Snapshot();
        act();
        int[] after = nes.Bus.Accesses.Snapshot();
        return [.. after.Select((count, i) => unchecked(count - before[i]))];
    }

    [Fact]
    public void Power_on_leaves_every_counter_at_zero()
    {
        Assert.Equal([0, 0, 0, 0], IdleMachine().Bus.Accesses.Snapshot());
    }

    [Fact]
    public void One_read_of_2002_adds_one_to_the_ppu_and_nothing_else()
    {
        Nes nes = IdleMachine();
        Assert.Equal([1, 0, 0, 0], Added(nes, () => nes.Bus.Read(0x2002)));
    }

    [Fact]
    public void A_peek_adds_nothing()
    {
        Nes nes = IdleMachine();
        Assert.Equal([0, 0, 0, 0], Added(nes, () =>
        {
            foreach (ushort address in new ushort[] { 0x2002, 0x2007, 0x4015, 0x4016, 0x4017 })
            {
                nes.Bus.Peek(address);
            }
        }));
    }

    [Fact]
    public void Ram_and_the_cartridge_add_nothing()
    {
        Nes nes = IdleMachine();
        Assert.Equal([0, 0, 0, 0], Added(nes, () =>
        {
            nes.Bus.Read(0x0000);
            nes.Bus.Write(0x0000, 1);
            nes.Bus.Read(0x8000);
            nes.Bus.Read(0x4018);
            nes.Bus.Read(0x4020);
        }));
    }

    [Fact]
    public void Oam_dma_from_page_2_adds_256_writes_to_the_ppu_and_one_to_the_apu()
    {
        Nes nes = IdleMachine();
        long cycles = nes.Bus.Cycles;

        // The write to $4014, then a read of RAM, on which the copy halts the CPU: the copy's reads
        // of page $02 and its halted reads of $0000 are RAM; its 256 writes to $2004 are the PPU's.
        int[] added = Added(nes, () =>
        {
            nes.Bus.Write(0x4014, 0x02);
            nes.Bus.Read(0x0000);
        });

        Assert.Equal([256, 1, 0, 0], added);
        Assert.InRange(nes.Bus.Cycles - cycles, 2 + 513, 2 + 514);
    }

    [Fact]
    public void Inc_4016_reads_pad_1_once_and_writes_the_strobe_twice()
    {
        // INC $4016: the NMOS core reads, writes the old value, then the new (bus.md 7).
        Nes nes = ProgramMachine(0xEE, 0x16, 0x40);
        Assert.Equal([0, 2, 1, 0], Added(nes, () => nes.Step()));
    }

    [Fact]
    public void Reads_of_4017_are_pad_2_and_writes_are_the_apu()
    {
        Nes nes = IdleMachine();
        Assert.Equal([0, 1, 0, 2], Added(nes, () =>
        {
            nes.Bus.Read(0x4017);
            nes.Bus.Read(0x4017);
            nes.Bus.Write(0x4017, 0x40);
        }));
    }

    [Fact]
    public void Reset_keeps_the_counters_and_power_on_starts_them_again()
    {
        Nes nes = IdleMachine();
        nes.Bus.Read(0x2002);
        nes.Reset();
        Assert.Equal(1u, nes.Bus.Accesses[NesChip.Ppu]);
        nes.PowerOn();
        Assert.Equal([0, 0, 0, 0], nes.Bus.Accesses.Snapshot());
    }

    [Fact]
    public void Counting_adds_no_cycle()
    {
        Nes nes = IdleMachine();
        long cycles = nes.Bus.Cycles;
        nes.Bus.Read(0x2002);
        nes.Bus.Write(0x4016, 1);
        nes.Bus.Read(0x4016);
        Assert.Equal(cycles + 3, nes.Bus.Cycles);
    }

    [Theory]
    [MemberData(nameof(BootCheck.Regions), MemberType = typeof(BootCheck))]
    public void A_frame_of_the_bundled_homebrew_talks_to_the_ppu(string region)
    {
        var nes = new Nes(Cartridge.Load(BootCheck.Rom()), BootCheck.RegionNamed(region));
        nes.PowerOn();
        nes.RunFrames(2);
        Assert.True(nes.Bus.Accesses[NesChip.Ppu] > 0, $"{region}: no access to the PPU in two frames");
    }
}
