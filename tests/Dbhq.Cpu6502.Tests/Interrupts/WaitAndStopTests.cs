using Xunit;

namespace Dbhq.Cpu6502.Tests.Interrupts;

/// <summary>
/// WDC's WAI and STP, which Harte's data leaves out. Cycle counts are from
/// WDC's datasheet; how long WAI takes to wake is not asserted, because no
/// reference we trust gives it.
/// </summary>
public sealed class WaitAndStopTests
{
    [Fact]
    public void WaiTakesThreeCyclesThenMakesNoBusAccess()
    {
        var bus = CmosInterruptTests.Program(0xCB);
        var cpu = new Cpu(bus, CpuVariant.Wdc65C02) { PC = 0x0400, P = 0x24 };

        Assert.Equal(3, cpu.Step());
        Assert.True(cpu.IsWaiting);
        int accesses = bus.Log.Count;
        Assert.Equal(0, cpu.Step());
        Assert.Equal(accesses, bus.Log.Count);
    }

    [Fact]
    public void AnIrqWithInterruptsDisabledWakesWaiWithoutBeingTaken()
    {
        var bus = CmosInterruptTests.Program(0xCB, 0xA9, 0x42);
        var cpu = new Cpu(bus, CpuVariant.Wdc65C02) { PC = 0x0400, S = 0xFF, P = 0x24 };
        cpu.Step();

        cpu.Irq = true;
        cpu.Step();

        Assert.False(cpu.IsWaiting);
        Assert.Equal(0x42, cpu.A);
        Assert.Equal(0x0403, cpu.PC);
        Assert.Equal(0xFF, cpu.S);
    }

    [Fact]
    public void AnIrqWithInterruptsEnabledWakesWaiAndIsTaken()
    {
        var bus = CmosInterruptTests.Program(0x58, 0xCB);
        var cpu = new Cpu(bus, CpuVariant.Wdc65C02) { PC = 0x0400, S = 0xFF, P = 0x24 };
        cpu.Step();
        cpu.Step();

        cpu.Irq = true;
        cpu.Step();

        Assert.Equal(0x0500, cpu.PC);
        Assert.Equal(0x04, bus.Memory[0x01FF]);
        Assert.Equal(0x02, bus.Memory[0x01FE]);
    }

    [Fact]
    public void AnNmiWakesWaiAndIsTaken()
    {
        var bus = CmosInterruptTests.Program(0xCB);
        var cpu = new Cpu(bus, CpuVariant.Wdc65C02) { PC = 0x0400, S = 0xFF, P = 0x24 };
        cpu.Step();

        cpu.Nmi = true;
        cpu.Step();

        Assert.Equal(0x0600, cpu.PC);
    }

    [Fact]
    public void StpStopsEverythingUntilReset()
    {
        var bus = CmosInterruptTests.Program(0xDB);
        bus.Memory[0xFFFC] = 0x00;
        bus.Memory[0xFFFD] = 0x07;
        var cpu = new Cpu(bus, CpuVariant.Wdc65C02) { PC = 0x0400, P = 0x24 };

        Assert.Equal(3, cpu.Step());
        cpu.Irq = true;
        cpu.Nmi = true;
        Assert.Equal(0, cpu.Step());
        Assert.True(cpu.IsStopped);

        cpu.Reset();

        Assert.False(cpu.IsStopped);
        Assert.Equal(0x0700, cpu.PC);
    }

    [Theory]
    [InlineData(CpuVariant.Rockwell65C02, 0xCB, 2)]
    [InlineData(CpuVariant.Synertek65C02, 0xCB, 2)]
    [InlineData(CpuVariant.Rockwell65C02, 0xDB, 4)]
    [InlineData(CpuVariant.Synertek65C02, 0xDB, 4)]
    public void OnlyWdcWaitsOrStops(CpuVariant variant, byte opcode, int cycles)
    {
        var cpu = new Cpu(CmosInterruptTests.Program(opcode), variant) { PC = 0x0400, P = 0x24 };

        Assert.Equal(cycles, cpu.Step());
        Assert.False(cpu.IsWaiting);
        Assert.False(cpu.IsStopped);
    }
}
