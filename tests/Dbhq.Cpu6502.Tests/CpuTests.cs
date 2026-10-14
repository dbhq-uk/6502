using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Cpu6502.Tests;

public sealed class CpuTests
{
    [Fact]
    public void ResetTakesSevenCyclesAndReadsTheVector()
    {
        var bus = new FlatBus();
        bus.Memory[0xFFFC] = 0x34;
        bus.Memory[0xFFFD] = 0x12;
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);

        cpu.Reset();

        Assert.Equal(7, cpu.Cycles);
        Assert.Equal(0x1234, cpu.PC);
        Assert.Equal(0xFD, cpu.S);
        Assert.True((cpu.P & (byte)StatusFlags.InterruptDisable) != 0);
        Assert.Equal(new BusAccess(0xFFFC, 0x34, false), bus.Log[5]);
        Assert.Equal(new BusAccess(0xFFFD, 0x12, false), bus.Log[6]);
        Assert.All(bus.Log, access => Assert.False(access.IsWrite));
    }

    [Theory]
    [InlineData(CpuVariant.Nmos6502, true)]
    [InlineData(CpuVariant.Ricoh2A03, true)]
    [InlineData(CpuVariant.Synertek65C02, false)]
    [InlineData(CpuVariant.Rockwell65C02, false)]
    [InlineData(CpuVariant.Wdc65C02, false)]
    public void ResetClearsDecimalModeOnlyOnThe65C02(CpuVariant variant, bool keepsDecimal)
    {
        var cpu = new Cpu(new FlatBus(), variant) { P = (byte)StatusFlags.Decimal };

        cpu.Reset();

        Assert.Equal(keepsDecimal, (cpu.P & (byte)StatusFlags.Decimal) != 0);
    }

    [Fact]
    public void NopIsTwoCyclesAndReadsTheByteAfterIt()
    {
        var bus = new FlatBus();
        bus.Memory[0x0200] = 0xEA;
        bus.Memory[0x0201] = 0x42;
        var cpu = new Cpu(bus, CpuVariant.Nmos6502) { PC = 0x0200 };

        int cycles = cpu.Step();

        Assert.Equal(2, cycles);
        Assert.Equal(0x0201, cpu.PC);
        Assert.Equal([new BusAccess(0x0200, 0xEA, false), new BusAccess(0x0201, 0x42, false)], bus.Log);
    }

    public static TheoryData<CpuVariant, byte, byte, byte> LxaCases()
    {
        // LXA (opcode $AB) puts (A OR a constant) AND the operand in A and X. The constant is
        // $FF on the Ricoh 2A03 variant (the console-calibrated value instr_test-v5 passes with,
        // decided 6 October 2026) and $EE on the NMOS 6502, as Harte's 6502 data has it.
        var rows = new TheoryData<CpuVariant, byte, byte, byte>();
        foreach ((byte a, byte operand) in new (byte, byte)[] { (0x00, 0xFF), (0x00, 0x5A), (0x11, 0xFF), (0x80, 0x37), (0xFF, 0xC3) })
        {
            rows.Add(CpuVariant.Ricoh2A03, a, operand, (byte)((a | 0xFF) & operand));
            rows.Add(CpuVariant.Nmos6502, a, operand, (byte)((a | 0xEE) & operand));
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(LxaCases))]
    public void LxaOrsAWithTheVariantsConstantAndAndsTheOperand(CpuVariant variant, byte a, byte operand, byte expected)
    {
        var bus = new FlatBus();
        bus.Memory[0x0200] = 0xAB;
        bus.Memory[0x0201] = operand;
        var cpu = new Cpu(bus, variant) { PC = 0x0200, A = a, X = 0x99 };

        int cycles = cpu.Step();

        Assert.Equal(2, cycles);
        Assert.Equal(expected, cpu.A);
        Assert.Equal(expected, cpu.X);
        Assert.Equal(expected == 0, (cpu.P & (byte)StatusFlags.Zero) != 0);
        Assert.Equal((expected & 0x80) != 0, (cpu.P & (byte)StatusFlags.Negative) != 0);
    }
}
