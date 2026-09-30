namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    /// <summary>The opcodes the NMOS chip was never documented to have. Task 6 fills this in.</summary>
    private void ExecuteNmos(byte opcode) =>
        throw new NotImplementedException($"{Variant} opcode ${opcode:X2} is not implemented");
}
