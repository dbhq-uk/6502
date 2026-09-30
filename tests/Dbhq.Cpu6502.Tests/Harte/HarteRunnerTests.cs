using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Cpu6502.Tests.Harte;

/// <summary>A harness that cannot fail proves nothing, so these make it fail on purpose.</summary>
public sealed class HarteRunnerTests
{
    private static HarteCase Nop(BusAccess secondCycle) => new(
        "ea synthetic",
        new HarteState(0x0200, 0xFD, 0, 0, 0, 0x24, [(0x0200, 0xEA), (0x0201, 0x42)]),
        new HarteState(0x0201, 0xFD, 0, 0, 0, 0x24, [(0x0200, 0xEA), (0x0201, 0x42)]),
        [new BusAccess(0x0200, 0xEA, false), secondCycle]);

    [Fact]
    public void ACorrectCasePasses()
    {
        string? failure = HarteRunner.RunCase(new FlatBus(), CpuVariant.Nmos6502, 0xEA, Nop(new BusAccess(0x0201, 0x42, false)));

        Assert.Null(failure);
    }

    [Fact]
    public void AWrongCycleIsNamed()
    {
        string? failure = HarteRunner.RunCase(new FlatBus(), CpuVariant.Nmos6502, 0xEA, Nop(new BusAccess(0x0202, 0x42, false)));

        Assert.NotNull(failure);
        Assert.Contains("cycle 1", failure);
    }

    [Fact]
    public void AWrongRegisterIsNamed()
    {
        var wrongA = Nop(new BusAccess(0x0201, 0x42, false)) with
        {
            Final = new HarteState(0x0201, 0xFD, 0x01, 0, 0, 0x24, [(0x0200, 0xEA), (0x0201, 0x42)]),
        };

        string? failure = HarteRunner.RunCase(new FlatBus(), CpuVariant.Nmos6502, 0xEA, wrongA);

        Assert.NotNull(failure);
        Assert.Contains("A is $00, expected $01", failure);
    }

    [Fact]
    public void AWrongMemoryValueIsNamed()
    {
        var wrongRam = Nop(new BusAccess(0x0201, 0x42, false)) with
        {
            Final = new HarteState(0x0201, 0xFD, 0, 0, 0, 0x24, [(0x0200, 0xEA), (0x0201, 0x99)]),
        };

        string? failure = HarteRunner.RunCase(new FlatBus(), CpuVariant.Nmos6502, 0xEA, wrongRam);

        Assert.NotNull(failure);
        Assert.Contains("memory $0201 is $42, expected $99", failure);
    }

    [Fact]
    public void AWrongCycleCountIsNamed()
    {
        var extraCycle = Nop(new BusAccess(0x0201, 0x42, false)) with
        {
            Cycles = [new BusAccess(0x0200, 0xEA, false), new BusAccess(0x0201, 0x42, false), new BusAccess(0x0202, 0xEA, false)],
        };

        string? failure = HarteRunner.RunCase(new FlatBus(), CpuVariant.Nmos6502, 0xEA, extraCycle);

        Assert.NotNull(failure);
        Assert.Contains("2 cycles, expected 3", failure);
    }

    [Fact]
    public void TheDecimalExceptionIsOneCycleWide()
    {
        var decimalCase = Nop(new BusAccess(0x0201, 0x42, false)) with
        {
            Initial = new HarteState(0x0200, 0xFD, 0, 0, 0, 0x2C, []),
        };

        Assert.True(HarteRunner.IsDecimalImmediateExtraCycle(CpuVariant.Wdc65C02, 0x69, decimalCase, 2));
        Assert.False(HarteRunner.IsDecimalImmediateExtraCycle(CpuVariant.Wdc65C02, 0x69, decimalCase, 1));
        Assert.False(HarteRunner.IsDecimalImmediateExtraCycle(CpuVariant.Nmos6502, 0x69, decimalCase, 2));
        Assert.False(HarteRunner.IsDecimalImmediateExtraCycle(CpuVariant.Wdc65C02, 0x65, decimalCase, 2));
    }
}
