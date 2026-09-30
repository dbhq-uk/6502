using Xunit;

namespace Dbhq.Cpu6502.Tests.Harte;

/// <summary>
/// Which opcodes run against Harte's data so far. Each task adds the opcodes
/// it implements, so CI stays green while the core is built. Task 11
/// replaces this with every opcode.
/// </summary>
public static class Coverage
{
    // Task 2: the first two instructions, enough to prove the harness.
    private static readonly byte[] FirstInstructions =
    [
        0xA9, 0xEA,
    ];

    private static readonly byte[] Official = [..FirstInstructions];

    private static readonly byte[] NmosOnly = [];

    private static readonly byte[] CmosOnly = [];

    public static IEnumerable<byte> Opcodes(CpuVariant variant)
    {
        bool cmos = variant is CpuVariant.Synertek65C02 or CpuVariant.Rockwell65C02 or CpuVariant.Wdc65C02;
        return Official.Concat(cmos ? CmosOnly : NmosOnly)

            // WAI and STP have no Harte data; WaitAndStopTests covers them.
            .Where(o => !(variant == CpuVariant.Wdc65C02 && o is 0xCB or 0xDB))
            .Distinct()
            .Order();
    }

    public static TheoryData<byte> TheoryData(CpuVariant variant) => new(Opcodes(variant));

    private static byte[] OtherThan(byte[] opcodes) => Enumerable.Range(0, 256).Select(o => (byte)o).Except(opcodes).ToArray();
}
