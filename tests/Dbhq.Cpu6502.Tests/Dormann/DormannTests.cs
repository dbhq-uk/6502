using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Cpu6502.Tests.Dormann;

/// <summary>
/// Klaus Dormann's whole-program tests: bugs that only show when
/// instructions run in sequence. Each one loops on an address when it ends;
/// the test passes if that address is the one its listing says "test passed".
/// </summary>
public sealed class DormannTests
{
    public static TheoryData<CpuVariant> AllVariants => new(Enum.GetValues<CpuVariant>());

    public static TheoryData<CpuVariant> DecimalVariants => new(Enum.GetValues<CpuVariant>().Where(v => v != CpuVariant.Ricoh2A03));

    public static TheoryData<CpuVariant> CmosVariants => new(CpuVariant.Synertek65C02, CpuVariant.Rockwell65C02, CpuVariant.Wdc65C02);

    [LinuxOnlyTheory]
    [MemberData(nameof(AllVariants))]
    public void FunctionalTestPasses(CpuVariant variant) => AssertPasses(TestSupport.Dormann.Functional(variant), variant);

    [LinuxOnlyTheory]
    [MemberData(nameof(CmosVariants))]
    public void ExtendedOpcodesTestPasses(CpuVariant variant) => AssertPasses(TestSupport.Dormann.Extended(variant), variant);

    [LinuxOnlyTheory]
    [MemberData(nameof(DecimalVariants))]
    public void DecimalTestPasses(CpuVariant variant)
    {
        TestSupport.Dormann.Program program = TestSupport.Dormann.Decimal(variant);
        var (bus, trap) = Run(program, variant);

        // The decimal test reports through its ERROR byte at $0B: 0 when every case passed.
        Assert.True(bus.Memory[0x0B] == 0, $"{program.Name} stopped at ${trap:X4} with ERROR = {bus.Memory[0x0B]}");
    }

    private static void AssertPasses(TestSupport.Dormann.Program program, CpuVariant variant)
    {
        Assert.NotNull(program.Success);
        var (_, trap) = Run(program, variant);
        Assert.True(trap == program.Success, $"{program.Name} stopped at ${trap:X4}; success is ${program.Success:X4}. Look that address up in .testdata/dormann/build/{program.Name}/program.lst");
    }

    private static (FlatBus Bus, ushort Trap) Run(TestSupport.Dormann.Program program, CpuVariant variant)
    {
        var bus = new FlatBus { Recording = false };
        program.Memory.CopyTo(bus.Memory, 0);
        var cpu = new Cpu(bus, variant) { PC = program.Start, S = 0xFF, P = 0x24 };
        return (bus, TestSupport.Dormann.RunToTrap(cpu));
    }
}
