using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Cpu6502.Tests.Interrupts;

/// <summary>
/// What the 65C02 does differently when it takes an interrupt. There is no
/// public transistor-level model of the 65C02, so these follow WDC's
/// datasheet and are not checked against the chip. See docs/known-differences.md.
/// </summary>
public sealed class CmosInterruptTests
{
    [Theory]
    [InlineData(CpuVariant.Nmos6502, true)]
    [InlineData(CpuVariant.Synertek65C02, false)]
    [InlineData(CpuVariant.Rockwell65C02, false)]
    [InlineData(CpuVariant.Wdc65C02, false)]
    public void AnIrqClearsDecimalModeOnlyOnThe65C02(CpuVariant variant, bool keepsDecimal)
    {
        var bus = Program(0xF8, 0x58, 0xEA, 0xEA);
        var cpu = new Cpu(bus, variant) { PC = 0x0400, S = 0xFF, P = 0x24, Irq = true };

        while (cpu.PC != 0x0500)
        {
            cpu.Step();
        }

        Assert.Equal(keepsDecimal, (cpu.P & (byte)StatusFlags.Decimal) != 0);
        Assert.True((bus.Memory[0x01FD] & (byte)StatusFlags.Decimal) != 0, "the pushed P keeps decimal mode, so RTI restores it");
    }

    internal static FlatBus Program(params byte[] code)
    {
        var bus = new FlatBus();
        Array.Fill(bus.Memory, (byte)0xEA);
        code.CopyTo(bus.Memory, 0x0400);
        bus.Memory[0xFFFA] = 0x00;
        bus.Memory[0xFFFB] = 0x06;
        bus.Memory[0xFFFE] = 0x00;
        bus.Memory[0xFFFF] = 0x05;
        return bus;
    }
}
