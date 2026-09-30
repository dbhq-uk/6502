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
