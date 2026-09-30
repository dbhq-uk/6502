using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Cpu6502.Tests;

/// <summary>The core is not done while any opcode of any variant is missing.</summary>
public sealed class CompletenessTests
{
    public static TheoryData<CpuVariant> Variants => new(Enum.GetValues<CpuVariant>());

    [Theory]
    [MemberData(nameof(Variants))]
    public void EveryOpcodeIsImplemented(CpuVariant variant)
    {
        var missing = new List<string>();
        for (int opcode = 0; opcode < 256; opcode++)
        {
            var bus = new FlatBus { Recording = false };
            bus.Memory[0x0200] = (byte)opcode;
            var cpu = new Cpu(bus, variant) { PC = 0x0200 };
            try
            {
                cpu.Step();
            }
            catch (NotImplementedException)
            {
                missing.Add($"${opcode:X2}");
            }
        }

        Assert.True(missing.Count == 0, $"{variant} is missing {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void EveryOpcodeHasADisassembly(CpuVariant variant)
    {
        for (int opcode = 0; opcode < 256; opcode++)
        {
            Assert.False(string.IsNullOrEmpty(OpcodeTable.Get(variant, (byte)opcode).Mnemonic), $"{variant} ${opcode:X2}");
        }
    }
}
