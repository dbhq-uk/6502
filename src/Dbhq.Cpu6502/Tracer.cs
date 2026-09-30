namespace Dbhq.Cpu6502;

/// <summary>One line of a trace: the state before an instruction runs.</summary>
public sealed record TraceLine(ushort PC, byte[] Bytes, OpcodeInfo Opcode, string Text, byte A, byte X, byte Y, byte P, byte S, long Cycles)
{
    /// <summary>The line in the format of the Nintendulator log that comes with nestest, without its PPU column.</summary>
    public override string ToString() =>
        $"{PC:X4}  {string.Join(' ', Bytes.Select(b => b.ToString("X2"))),-8} {(Opcode.Undocumented ? '*' : ' ')}{Text,-32}A:{A:X2} X:{X:X2} Y:{Y:X2} P:{P:X2} SP:{S:X2} CYC:{Cycles}";
}

public static class Tracer
{
    /// <summary>Captures the CPU's state and the instruction about to run.</summary>
    public static TraceLine Capture(Cpu cpu, Func<ushort, byte> peek)
    {
        OpcodeInfo info = OpcodeTable.Get(cpu.Variant, peek(cpu.PC));
        byte[] bytes = new byte[info.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = peek((ushort)(cpu.PC + i));
        }

        string text = Disassembler.Disassemble(cpu.Variant, peek, cpu.PC);
        return new TraceLine(cpu.PC, bytes, info, text, cpu.A, cpu.X, cpu.Y, cpu.P, cpu.S, cpu.Cycles);
    }
}
