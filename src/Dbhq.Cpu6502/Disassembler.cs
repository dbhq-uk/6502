namespace Dbhq.Cpu6502;

public static class Disassembler
{
    /// <summary>
    /// The instruction at an address as text, such as "LDA $1234,X". Reads
    /// memory through peek, which must not have side effects: disassembly is
    /// never a bus access.
    /// </summary>
    public static string Disassemble(CpuVariant variant, Func<ushort, byte> peek, ushort address)
    {
        OpcodeInfo info = OpcodeTable.Get(variant, peek(address));
        byte b1 = peek((ushort)(address + 1));
        byte b2 = peek((ushort)(address + 2));
        ushort word = (ushort)(b1 | b2 << 8);
        string operand = info.Mode switch
        {
            AddressingMode.Implied => "",
            AddressingMode.Accumulator => "A",
            AddressingMode.Immediate => $"#${b1:X2}",
            AddressingMode.ZeroPage => $"${b1:X2}",
            AddressingMode.ZeroPageX => $"${b1:X2},X",
            AddressingMode.ZeroPageY => $"${b1:X2},Y",
            AddressingMode.Absolute => $"${word:X4}",
            AddressingMode.AbsoluteX => $"${word:X4},X",
            AddressingMode.AbsoluteY => $"${word:X4},Y",
            AddressingMode.IndirectX => $"(${b1:X2},X)",
            AddressingMode.IndirectY => $"(${b1:X2}),Y",
            AddressingMode.Indirect => $"(${word:X4})",
            AddressingMode.Relative => $"${(ushort)(address + 2 + (sbyte)b1):X4}",
            AddressingMode.ZeroPageIndirect => $"(${b1:X2})",
            AddressingMode.AbsoluteIndexedIndirect => $"(${word:X4},X)",
            AddressingMode.ZeroPageRelative => $"${b1:X2},${(ushort)(address + 3 + (sbyte)b2):X4}",
            _ => throw new InvalidOperationException($"Unknown addressing mode {info.Mode}"),
        };
        return operand.Length == 0 ? info.Mnemonic : $"{info.Mnemonic} {operand}";
    }
}
