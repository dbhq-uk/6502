using Xunit;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>The memory map, read and written through the bus as the CPU would.</summary>
public sealed class Kim1MemoryMapTests
{
    private static Kim1Bus Bus() => new Kim1Session().Machine.Bus;

    [Fact]
    public void TheMonitorRomIsAt1800To1FFFAndCannotBeWritten()
    {
        var bus = Bus();
        byte[] rom003 = Kim1Session.Rom003;
        byte[] rom002 = Kim1Session.Rom002;
        for (int i = 0; i < 0x400; i++)
        {
            Assert.Equal(rom003[i], bus.Read((ushort)(0x1800 + i)));
            Assert.Equal(rom002[i], bus.Read((ushort)(0x1C00 + i)));
        }

        for (int i = 0; i < 0x800; i++)
        {
            ushort address = (ushort)(0x1800 + i);
            byte before = bus.Read(address);
            bus.Write(address, (byte)~before);
            Assert.Equal(before, bus.Read(address));
        }
    }

    /// <summary>A13 to A15 are not decoded, so the 8 KB repeats and the CPU finds its vectors in the ROM.</summary>
    [Fact]
    public void TheEightKilobytesRepeatThroughMemorySoTheVectorsComeFromTheRom()
    {
        var bus = Bus();
        for (int block = 1; block < 8; block++)
        {
            for (int i = 0x1800; i < 0x2000; i += 0x37)
            {
                Assert.Equal(bus.Read((ushort)i), bus.Read((ushort)(block * 0x2000 + i)));
            }
        }

        bus.Write(0x0123, 0x5A);
        Assert.Equal(0x5A, bus.Read(0xE123));
        bus.Write(0x6123, 0xA5);
        Assert.Equal(0xA5, bus.Read(0x0123));

        // The vectors at $FFFA-$FFFF are $1FFA-$1FFF: the last six bytes of the 6530-002.
        byte[] rom002 = Kim1Session.Rom002;
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(rom002[0x3FA + i], bus.Read((ushort)(0xFFFA + i)));
        }

        var kim = new Kim1Session().Machine;
        kim.PressReset();
        Assert.Equal(rom002[0x3FC] | rom002[0x3FD] << 8, kim.Cpu.PC);
    }

    [Fact]
    public void RamIsAt0000To03FFAnd1780To17FFAndNowhereElse()
    {
        var bus = Bus();
        for (int i = 0; i < 0x400; i++)
        {
            bus.Write((ushort)i, (byte)(i * 7));
        }

        for (int i = 0; i < 0x80; i++)
        {
            bus.Write((ushort)(0x1780 + i), (byte)(i ^ 0xA5));
        }

        for (int i = 0; i < 0x400; i++)
        {
            Assert.Equal((byte)(i * 7), bus.Read((ushort)i));
        }

        for (int i = 0; i < 0x80; i++)
        {
            Assert.Equal((byte)(i ^ 0xA5), bus.Read((ushort)(0x1780 + i)));
        }

        // The two 6530s' RAM: the 6530-003 at $1780, the 6530-002 at $17C0.
        Assert.Equal(0xA5, bus.U3.ReadRam(0));
        Assert.Equal(0x40 ^ 0xA5, bus.U2.ReadRam(0));
    }

    /// <summary>
    /// K1 to K4 and $1400-$16FF hold nothing. A read there sees whatever was
    /// last on the data bus, not what was written.
    /// </summary>
    [Fact]
    public void TheUndecodedBlocksHoldNothing()
    {
        var bus = Bus();
        bus.Write(0x0000, 0x77);
        foreach (int address in new[] { 0x0400, 0x0800, 0x0C00, 0x1000, 0x13FF, 0x1400, 0x1500, 0x16FF })
        {
            bus.Write((ushort)address, 0x55);
            Assert.Equal(0x77, bus.Read(0x0000));
            Assert.Equal(0x77, bus.Read((ushort)address));
        }
    }

    /// <summary>A4 and A5 are not decoded in the I/O and timer area, so each chip's 16 registers appear four times.</summary>
    [Fact]
    public void EachChipsRegistersRepeatFourTimesIn64Bytes()
    {
        var bus = Bus();
        bus.Write(0x1701, 0xFF);
        bus.Write(0x1700, 0x3C);
        foreach (int mirror in new[] { 0x1700, 0x1710, 0x1720, 0x1730 })
        {
            Assert.Equal(0x3C, bus.Read((ushort)mirror));
            Assert.Equal(0xFF, bus.Read((ushort)(mirror + 1)));
        }

        bus.Write(0x1733, 0x0F);
        Assert.Equal(0x0F, bus.U3.PortBDirection);
        Assert.Equal(0, bus.U2.PortBDirection);

        bus.Write(0x1771, 0x81);
        Assert.Equal(0x81, bus.U2.PortADirection);
    }

    [Fact]
    public void TheApplicationPortsRespectTheirDataDirectionRegisters()
    {
        var bus = Bus();
        bus.U3.PortAInput = 0b1100_0011;
        bus.Write(0x1701, 0b0000_1111);
        bus.Write(0x1700, 0b1010_1010);
        Assert.Equal(0b1100_1010, bus.Read(0x1700));
        Assert.Equal(0b1100_1010, bus.U3.PortAPins);

        bus.U3.PortBInput = 0b0101_0101;
        bus.Write(0x1703, 0b1111_0000);
        bus.Write(0x1702, 0b0011_1100);
        Assert.Equal(0b0011_0101, bus.Read(0x1702));
    }

    public static TheoryData<int, int> Prescales => new() { { 0, 1 }, { 1, 8 }, { 2, 64 }, { 3, 1024 } };

    /// <summary>
    /// Every access is one clock for the timers, read or write, whatever it
    /// addresses: the 6530-003's flag at $1707 rises on the access N x k + 1
    /// after the write that started it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Prescales))]
    public void TheApplicationTimerFlagRisesOnTheRightBusCycleAtEachPrescale(int select, int divide)
    {
        const int start = 3;
        var bus = Bus();
        bus.Write((ushort)(0x1704 | select), start);
        int accesses = 0;
        while (true)
        {
            accesses++;
            bool flag;
            if (accesses % 2 == 0)
            {
                bus.Write(0x0200, 0);
                flag = bus.U3.TimerFlag;
            }
            else
            {
                flag = bus.Read(0x1707) == 0x80;
            }

            if (flag)
            {
                break;
            }

            Assert.True(accesses < 10_000, "the flag never rose");
        }

        Assert.Equal(start * divide + 1, accesses);
    }

    /// <summary>
    /// RS drives the 6530s' RES line as well as the CPU's, so the user's port
    /// goes back to all inputs. The monitor sets up only its own chip's ports,
    /// so what the keypad shows at $1701 afterwards is what RES left there.
    /// </summary>
    [Fact]
    public void TheRsKeyTurnsTheApplicationPortBackIntoInputs()
    {
        var kim = new Kim1Session().Boot();
        kim.Keys("[AD] 1701 [DA] FF");
        Assert.Equal("1701 FF", kim.Display);
        Assert.Equal(0xFF, kim.Machine.Bus.U3.PortADirection);

        kim.Boot();
        kim.Keys("[AD] 1701");
        Assert.Equal("1701 00", kim.Display);
    }

    [Fact]
    public void EveryBusAccessTheCpuMakesIsOneMachineCycle()
    {
        var kim = new Kim1Session().Boot();
        kim.Keys("AD 0200");
        Assert.Equal(kim.Machine.Cpu.Cycles, kim.Machine.Cycles);
    }
}
