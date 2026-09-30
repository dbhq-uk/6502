using Xunit;

namespace Dbhq.Cpu6502.Tests.Harte;

/// <summary>
/// Which opcodes run against Harte's data. Every opcode of every variant,
/// except WAI and STP on WDC, whose files are empty; WaitAndStopTests covers them.
/// </summary>
public static class Coverage
{
    public static IEnumerable<byte> Opcodes(CpuVariant variant) => Enumerable.Range(0, 256)
        .Select(o => (byte)o)
        .Where(o => !(variant == CpuVariant.Wdc65C02 && o is 0xCB or 0xDB));

    public static TheoryData<byte> TheoryData(CpuVariant variant) => new(Opcodes(variant));
}
