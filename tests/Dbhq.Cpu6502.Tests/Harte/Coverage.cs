using Xunit;

namespace Dbhq.Cpu6502.Tests.Harte;

/// <summary>
/// Which opcodes run against Harte's data: every opcode of every variant except the ones in
/// <see cref="Excluded"/>, each with its reason. <see cref="CoverageTests"/> prints the list and
/// how many each variant leaves out, so an exclusion is never a silent skip.
/// </summary>
public static class Coverage
{
    /// <summary>An opcode of one variant that does not run against Harte's file, and why.</summary>
    public sealed record Exclusion(CpuVariant Variant, byte Opcode, string Reason);

    /// <summary>Every opcode left out of a Harte run.</summary>
    public static IReadOnlyList<Exclusion> Excluded { get; } =
    [
        new(CpuVariant.Wdc65C02, 0xCB, "WAI: Harte's file is empty; WaitAndStopTests covers it"),
        new(CpuVariant.Wdc65C02, 0xDB, "STP: Harte's file is empty; WaitAndStopTests covers it"),
        new(
            CpuVariant.Ricoh2A03,
            0xAB,
            "LXA: the nes6502 file expects the constant $EE, and the Ricoh 2A03 variant uses $FF, the "
            + "value Blargg's instr_test-v5 passes with (decided 6 October 2026, docs/known-differences.md); "
            + "CoverageTests runs the file on the NMOS variant, so everything but the constant is still checked"),
    ];

    public static IEnumerable<byte> Opcodes(CpuVariant variant) => Enumerable.Range(0, 256)
        .Select(o => (byte)o)
        .Where(o => !Excluded.Any(e => e.Variant == variant && e.Opcode == o));

    public static TheoryData<byte> TheoryData(CpuVariant variant) => new(Opcodes(variant));
}
