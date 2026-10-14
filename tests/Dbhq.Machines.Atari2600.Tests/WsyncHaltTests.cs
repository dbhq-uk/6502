using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The WSYNC halt, from <c>docs/atari-2600/facts/bus.md</c> section 5 and <c>tia.md</c> section 5:
/// a write to WSYNC holds RDY low until colour clock 0 of the next line; the 6507 stops only on a
/// read, so the next read is repeated, a whole cycle each time, until then. Cycle k of a line runs
/// colour clocks 3k - 3 to 3k - 1 (tia.md section 4, the split task 5 settles), so from power on,
/// with the TIA at colour clock 0, the bus's n-th call is cycle n of the first line. The held reads
/// are seen through <see cref="RecordingBoard"/>, which stands in for the observer here: the
/// observer is told of TIA and RIOT accesses and bank switches, not of plain cartridge reads.
/// </summary>
public class WsyncHaltTests
{
    private static (Atari2600Bus Bus, RecordingBoard Board) PoweredBus(Region region)
    {
        var board = new RecordingBoard(Cartridge.Load(TestPrograms.Image4K()).CreateBoard());
        var bus = new Atari2600Bus(board, region);
        bus.PowerOn();
        return (bus, board);
    }

    public static TheoryData<string, int> EveryWriteCycle()
    {
        var data = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            for (int k = 1; k <= 76; k++)
            {
                data.Add(region, k);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryWriteCycle))]
    public void Worked_examples_a_wsync_written_on_cycle_k_halts_76_less_k_read_cycles(string regionName, int k)
    {
        (Atari2600Bus bus, RecordingBoard board) = PoweredBus(TestPrograms.RegionNamed(regionName));
        for (int i = 1; i < k; i++)
        {
            bus.Read(0x1000);
        }

        bus.Write(0x0002, 0x00);
        Assert.Equal(k, bus.Cycles);
        board.Accesses.Clear();

        bus.Read(0x1234);

        // tia.md section 5's table: cycle 20 halts 56, 73 halts 3, 75 halts 1, 76 none. Then the
        // read itself, on cycle 1 of the next line, colour clocks 0 to 2.
        int halted = 76 - k;
        Assert.Equal(k + halted + 1, bus.Cycles);
        Assert.Equal(3, bus.Tia.HorizontalCounter);
        Assert.False(bus.Tia.RdyLow);

        // Every halted cycle is a read of the held address, one access each, as the slot sees it.
        Assert.Equal(halted + 1, board.Accesses.Count);
        Assert.All(board.Accesses, access => Assert.Equal(((ushort)0x1234, false), access));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void A_wsync_written_on_cycle_1_of_a_line_waits_for_the_line_after(string regionName)
    {
        // tia.md section 5: a write one cycle later than 76 is cycle 1 of the next line, and halts
        // cycles 2 to 76 of it.
        (Atari2600Bus bus, _) = PoweredBus(TestPrograms.RegionNamed(regionName));
        for (int i = 0; i < 76; i++)
        {
            bus.Read(0x1000);
        }

        bus.Write(0x0002, 0x00);
        bus.Read(0x1000);
        Assert.Equal(76 + 76 + 1, bus.Cycles);
        Assert.Equal(3, bus.Tia.HorizontalCounter);
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void A_write_straight_after_the_wsync_write_goes_ahead(string regionName)
    {
        (Atari2600Bus bus, _) = PoweredBus(TestPrograms.RegionNamed(regionName));
        bus.Write(0x0002, 0x00);
        bus.Write(0x0080, 0x77);
        Assert.Equal(2, bus.Cycles);
        Assert.Equal(0x77, bus.Riot.ReadRam(0));
        Assert.True(bus.Tia.RdyLow);

        bus.Read(0x1000);
        Assert.Equal(77, bus.Cycles);
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void The_riot_counts_through_the_halt(string regionName)
    {
        // bus.md section 5: the RIOT has no RDY pin, so each halted cycle is one of its cycles.
        (Atari2600Bus bus, _) = PoweredBus(TestPrograms.RegionNamed(regionName));
        bus.Write(0x0294, 200);
        bus.Write(0x0002, 0x00);
        bus.Read(0x1000);
        // The timer write is cycle 1; cycle 2 writes WSYNC, cycles 3 to 76 are halted, and the read
        // is the next line's cycle 1: 76 cycles after the write, every one counted.
        Assert.Equal(77, bus.Cycles);
        Assert.Equal(200 - 76, bus.Riot.PeekRegister(0x0284));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void Section_5_dec_wsync_writes_twice_then_halts_and_costs_one_line(string regionName)
    {
        // STA WSYNC; DEC WSYNC; NOP; JMP to the NOP.
        Atari2600 machine = TestPrograms.Machine(TestPrograms.RegionNamed(regionName), 0x85, 0x02, 0xC6, 0x02, 0xEA, 0x4C, 0x04, 0xF0);
        var observer = new ObserverTests.Recorder();
        machine.Bus.Observer = observer;

        machine.Step();
        Assert.Equal(["write $0002"], observer.Accesses);

        // DEC WSYNC waits for the line, then runs from cycle 1: fetch, operand, the read of $02
        // on cycle 3, the old byte written back on 4 and the new one on 5.
        machine.Step();
        Assert.Equal(["write $0002", "read $0002", "write $0002", "write $0002"], observer.Accesses);
        Assert.Equal(15, machine.Bus.Tia.HorizontalCounter);

        // The NOP's fetch waits from cycle 6 to the end of the line: 71 halted cycles, then the
        // fetch on cycle 1 and the NOP's second cycle.
        Assert.Equal(71 + 2, machine.Step());
        Assert.Equal(6, machine.Bus.Tia.HorizontalCounter);
        Assert.Equal(2, observer.Halts.Count);
    }
}
