namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    /// <summary>
    /// The 151 opcodes every member of the family shares. Where the NMOS chip
    /// and the 65C02 differ in how one of these uses the bus, the difference
    /// lives in the addressing helpers, not here.
    /// </summary>
    /// <returns>False when the opcode is not one of the shared set.</returns>
    private bool ExecuteOfficial(byte opcode)
    {
        switch (opcode)
        {
            // Loads
            case 0xA9: A = NZ(Read(PC++)); break;
            case 0xA5: A = NZ(Read(Zp())); break;
            case 0xB5: A = NZ(Read(ZpX())); break;
            case 0xAD: A = NZ(Read(Abs())); break;
            case 0xBD: A = NZ(Read(AbsX(Access.Read))); break;
            case 0xB9: A = NZ(Read(AbsY(Access.Read))); break;
            case 0xA1: A = NZ(Read(IzX())); break;
            case 0xB1: A = NZ(Read(IzY(Access.Read))); break;
            case 0xA2: X = NZ(Read(PC++)); break;
            case 0xA6: X = NZ(Read(Zp())); break;
            case 0xB6: X = NZ(Read(ZpY())); break;
            case 0xAE: X = NZ(Read(Abs())); break;
            case 0xBE: X = NZ(Read(AbsY(Access.Read))); break;
            case 0xA0: Y = NZ(Read(PC++)); break;
            case 0xA4: Y = NZ(Read(Zp())); break;
            case 0xB4: Y = NZ(Read(ZpX())); break;
            case 0xAC: Y = NZ(Read(Abs())); break;
            case 0xBC: Y = NZ(Read(AbsX(Access.Read))); break;

            // Stores
            case 0x85: Write(Zp(), A); break;
            case 0x95: Write(ZpX(), A); break;
            case 0x8D: Write(Abs(), A); break;
            case 0x9D: Write(AbsX(Access.Write), A); break;
            case 0x99: Write(AbsY(Access.Write), A); break;
            case 0x81: Write(IzX(), A); break;
            case 0x91: Write(IzY(Access.Write), A); break;
            case 0x86: Write(Zp(), X); break;
            case 0x96: Write(ZpY(), X); break;
            case 0x8E: Write(Abs(), X); break;
            case 0x84: Write(Zp(), Y); break;
            case 0x94: Write(ZpX(), Y); break;
            case 0x8C: Write(Abs(), Y); break;

            // Transfers. A one-byte instruction reads the byte after it and discards it.
            case 0xAA: Read(PC); X = NZ(A); break;
            case 0xA8: Read(PC); Y = NZ(A); break;
            case 0x8A: Read(PC); A = NZ(X); break;
            case 0x98: Read(PC); A = NZ(Y); break;
            case 0xBA: Read(PC); X = NZ(S); break;
            case 0x9A: Read(PC); S = X; break;

            // Flags
            case 0x18: Read(PC); SetFlag(C, false); break;
            case 0x38: Read(PC); SetFlag(C, true); break;
            case 0x58: Read(PC); SetFlag(I, false); break;
            case 0x78: Read(PC); SetFlag(I, true); break;
            case 0xB8: Read(PC); SetFlag(V, false); break;
            case 0xD8: Read(PC); SetFlag(D, false); break;
            case 0xF8: Read(PC); SetFlag(D, true); break;

            // Increments and decrements
            case 0xE6: IncAt(Zp()); break;
            case 0xF6: IncAt(ZpX()); break;
            case 0xEE: IncAt(Abs()); break;
            case 0xFE: IncAt(AbsX(Access.ModifyAlways)); break;
            case 0xC6: DecAt(Zp()); break;
            case 0xD6: DecAt(ZpX()); break;
            case 0xCE: DecAt(Abs()); break;
            case 0xDE: DecAt(AbsX(Access.ModifyAlways)); break;
            case 0xE8: Read(PC); X = NZ((byte)(X + 1)); break;
            case 0xC8: Read(PC); Y = NZ((byte)(Y + 1)); break;
            case 0xCA: Read(PC); X = NZ((byte)(X - 1)); break;
            case 0x88: Read(PC); Y = NZ((byte)(Y - 1)); break;

            // Compares
            case 0xC9: Compare(A, Read(PC++)); break;
            case 0xC5: Compare(A, Read(Zp())); break;
            case 0xD5: Compare(A, Read(ZpX())); break;
            case 0xCD: Compare(A, Read(Abs())); break;
            case 0xDD: Compare(A, Read(AbsX(Access.Read))); break;
            case 0xD9: Compare(A, Read(AbsY(Access.Read))); break;
            case 0xC1: Compare(A, Read(IzX())); break;
            case 0xD1: Compare(A, Read(IzY(Access.Read))); break;
            case 0xE0: Compare(X, Read(PC++)); break;
            case 0xE4: Compare(X, Read(Zp())); break;
            case 0xEC: Compare(X, Read(Abs())); break;
            case 0xC0: Compare(Y, Read(PC++)); break;
            case 0xC4: Compare(Y, Read(Zp())); break;
            case 0xCC: Compare(Y, Read(Abs())); break;

            // Logic
            case 0x29: And(Read(PC++)); break;
            case 0x25: And(Read(Zp())); break;
            case 0x35: And(Read(ZpX())); break;
            case 0x2D: And(Read(Abs())); break;
            case 0x3D: And(Read(AbsX(Access.Read))); break;
            case 0x39: And(Read(AbsY(Access.Read))); break;
            case 0x21: And(Read(IzX())); break;
            case 0x31: And(Read(IzY(Access.Read))); break;
            case 0x09: Ora(Read(PC++)); break;
            case 0x05: Ora(Read(Zp())); break;
            case 0x15: Ora(Read(ZpX())); break;
            case 0x0D: Ora(Read(Abs())); break;
            case 0x1D: Ora(Read(AbsX(Access.Read))); break;
            case 0x19: Ora(Read(AbsY(Access.Read))); break;
            case 0x01: Ora(Read(IzX())); break;
            case 0x11: Ora(Read(IzY(Access.Read))); break;
            case 0x49: Eor(Read(PC++)); break;
            case 0x45: Eor(Read(Zp())); break;
            case 0x55: Eor(Read(ZpX())); break;
            case 0x4D: Eor(Read(Abs())); break;
            case 0x5D: Eor(Read(AbsX(Access.Read))); break;
            case 0x59: Eor(Read(AbsY(Access.Read))); break;
            case 0x41: Eor(Read(IzX())); break;
            case 0x51: Eor(Read(IzY(Access.Read))); break;
            case 0x24: Bit(Read(Zp())); break;
            case 0x2C: Bit(Read(Abs())); break;

            // Shifts and rotates
            case 0x0A: Read(PC); A = Asl(A); break;
            case 0x06: AslAt(Zp()); break;
            case 0x16: AslAt(ZpX()); break;
            case 0x0E: AslAt(Abs()); break;
            case 0x1E: AslAt(AbsX(Access.Modify)); break;
            case 0x4A: Read(PC); A = Lsr(A); break;
            case 0x46: LsrAt(Zp()); break;
            case 0x56: LsrAt(ZpX()); break;
            case 0x4E: LsrAt(Abs()); break;
            case 0x5E: LsrAt(AbsX(Access.Modify)); break;
            case 0x2A: Read(PC); A = Rol(A); break;
            case 0x26: RolAt(Zp()); break;
            case 0x36: RolAt(ZpX()); break;
            case 0x2E: RolAt(Abs()); break;
            case 0x3E: RolAt(AbsX(Access.Modify)); break;
            case 0x6A: Read(PC); A = Ror(A); break;
            case 0x66: RorAt(Zp()); break;
            case 0x76: RorAt(ZpX()); break;
            case 0x6E: RorAt(Abs()); break;
            case 0x7E: RorAt(AbsX(Access.Modify)); break;

            case 0xEA: Read(PC); break;

            // Arithmetic
            case 0x69: AdcAt(PC++); break;
            case 0x65: AdcAt(Zp()); break;
            case 0x75: AdcAt(ZpX()); break;
            case 0x6D: AdcAt(Abs()); break;
            case 0x7D: AdcAt(AbsX(Access.Read)); break;
            case 0x79: AdcAt(AbsY(Access.Read)); break;
            case 0x61: AdcAt(IzX()); break;
            case 0x71: AdcAt(IzY(Access.Read)); break;
            case 0xE9: SbcAt(PC++); break;
            case 0xE5: SbcAt(Zp()); break;
            case 0xF5: SbcAt(ZpX()); break;
            case 0xED: SbcAt(Abs()); break;
            case 0xFD: SbcAt(AbsX(Access.Read)); break;
            case 0xF9: SbcAt(AbsY(Access.Read)); break;
            case 0xE1: SbcAt(IzX()); break;
            case 0xF1: SbcAt(IzY(Access.Read)); break;

            // Control flow
            case 0x10: Branch(!Flag(N)); break;
            case 0x30: Branch(Flag(N)); break;
            case 0x50: Branch(!Flag(V)); break;
            case 0x70: Branch(Flag(V)); break;
            case 0x90: Branch(!Flag(C)); break;
            case 0xB0: Branch(Flag(C)); break;
            case 0xD0: Branch(!Flag(Z)); break;
            case 0xF0: Branch(Flag(Z)); break;
            case 0x4C: PC = Abs(); break;
            case 0x6C: JmpIndirect(); break;
            case 0x20: Jsr(); break;
            case 0x60: Rts(); break;
            case 0x40: Rti(); break;
            case 0x00: Brk(); break;

            // Stack
            case 0x48: Read(PC); Push(A); break;
            case 0x08: Read(PC); Push((byte)(P | B | U)); break;
            case 0x68: Read(PC); Read(StackAddress); A = NZ(Pull()); break;
            case 0x28: Read(PC); Read(StackAddress); P = (byte)((Pull() & ~B) | U); break;

            default: return false;
        }

        return true;
    }
}
