namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    /// <summary>The 65C02's additions and defined no-ops. Task 7 fills this in.</summary>
    private void ExecuteCmos(byte opcode) =>
        throw new NotImplementedException($"{Variant} opcode ${opcode:X2} is not implemented");
}
