using Xunit;

namespace Dbhq.Cpu6502.Tests.Harte;

/// <summary>
/// One test per opcode per variant: every one of Harte's cases for that
/// opcode, checked cycle by cycle. One class per variant, so xUnit runs the
/// five sets in parallel.
/// </summary>
public abstract class HarteTests(CpuVariant variant)
{
    protected void Run(byte opcode)
    {
        IReadOnlyList<HarteCase> cases = HarteFile.Load(HarteSets.FileFor(variant, opcode));
        Assert.NotEmpty(cases);
        List<string> failures = HarteRunner.Run(variant, opcode, cases);
        Assert.True(failures.Count == 0, $"{variant} ${opcode:X2} fails {failures.Count} or more cases:\n{string.Join("\n", failures)}");
    }
}

public sealed class Nmos6502Harte() : HarteTests(CpuVariant.Nmos6502)
{
    public static TheoryData<byte> Opcodes => Coverage.TheoryData(CpuVariant.Nmos6502);

    [Theory]
    [MemberData(nameof(Opcodes))]
    public void Opcode(byte opcode) => Run(opcode);
}

public sealed class Ricoh2A03Harte() : HarteTests(CpuVariant.Ricoh2A03)
{
    public static TheoryData<byte> Opcodes => Coverage.TheoryData(CpuVariant.Ricoh2A03);

    [Theory]
    [MemberData(nameof(Opcodes))]
    public void Opcode(byte opcode) => Run(opcode);
}

public sealed class Synertek65C02Harte() : HarteTests(CpuVariant.Synertek65C02)
{
    public static TheoryData<byte> Opcodes => Coverage.TheoryData(CpuVariant.Synertek65C02);

    [Theory]
    [MemberData(nameof(Opcodes))]
    public void Opcode(byte opcode) => Run(opcode);
}

public sealed class Rockwell65C02Harte() : HarteTests(CpuVariant.Rockwell65C02)
{
    public static TheoryData<byte> Opcodes => Coverage.TheoryData(CpuVariant.Rockwell65C02);

    [Theory]
    [MemberData(nameof(Opcodes))]
    public void Opcode(byte opcode) => Run(opcode);
}

public sealed class Wdc65C02Harte() : HarteTests(CpuVariant.Wdc65C02)
{
    public static TheoryData<byte> Opcodes => Coverage.TheoryData(CpuVariant.Wdc65C02);

    [Theory]
    [MemberData(nameof(Opcodes))]
    public void Opcode(byte opcode) => Run(opcode);
}
