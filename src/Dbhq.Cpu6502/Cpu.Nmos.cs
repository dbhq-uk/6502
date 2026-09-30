namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    /// <summary>The 105 opcodes the NMOS chip was never documented to have.</summary>
    private void ExecuteNmos(byte opcode)
    {
        switch (opcode)
        {
            // No-ops of every length, each making the reads its addressing mode makes.
            case 0x1A: case 0x3A: case 0x5A: case 0x7A: case 0xDA: case 0xFA: Read(PC); break;
            case 0x80: case 0x82: case 0x89: case 0xC2: case 0xE2: Read(PC++); break;
            case 0x04: case 0x44: case 0x64: Read(Zp()); break;
            case 0x14: case 0x34: case 0x54: case 0x74: case 0xD4: case 0xF4: Read(ZpX()); break;
            case 0x0C: Read(Abs()); break;
            case 0x1C: case 0x3C: case 0x5C: case 0x7C: case 0xDC: case 0xFC: Read(AbsX(Access.Read)); break;

            case 0x02: case 0x12: case 0x22: case 0x32: case 0x42: case 0x52:
            case 0x62: case 0x72: case 0x92: case 0xB2: case 0xD2: case 0xF2:
                Jam();
                break;

            // A shift or increment followed by an ALU operation on the result.
            case 0x07: Slo(Zp()); break;
            case 0x17: Slo(ZpX()); break;
            case 0x0F: Slo(Abs()); break;
            case 0x1F: Slo(AbsX(Access.Modify)); break;
            case 0x1B: Slo(AbsY(Access.Modify)); break;
            case 0x03: Slo(IzX()); break;
            case 0x13: Slo(IzY(Access.Modify)); break;
            case 0x27: Rla(Zp()); break;
            case 0x37: Rla(ZpX()); break;
            case 0x2F: Rla(Abs()); break;
            case 0x3F: Rla(AbsX(Access.Modify)); break;
            case 0x3B: Rla(AbsY(Access.Modify)); break;
            case 0x23: Rla(IzX()); break;
            case 0x33: Rla(IzY(Access.Modify)); break;
            case 0x47: Sre(Zp()); break;
            case 0x57: Sre(ZpX()); break;
            case 0x4F: Sre(Abs()); break;
            case 0x5F: Sre(AbsX(Access.Modify)); break;
            case 0x5B: Sre(AbsY(Access.Modify)); break;
            case 0x43: Sre(IzX()); break;
            case 0x53: Sre(IzY(Access.Modify)); break;
            case 0x67: Rra(Zp()); break;
            case 0x77: Rra(ZpX()); break;
            case 0x6F: Rra(Abs()); break;
            case 0x7F: Rra(AbsX(Access.Modify)); break;
            case 0x7B: Rra(AbsY(Access.Modify)); break;
            case 0x63: Rra(IzX()); break;
            case 0x73: Rra(IzY(Access.Modify)); break;
            case 0xC7: Dcp(Zp()); break;
            case 0xD7: Dcp(ZpX()); break;
            case 0xCF: Dcp(Abs()); break;
            case 0xDF: Dcp(AbsX(Access.Modify)); break;
            case 0xDB: Dcp(AbsY(Access.Modify)); break;
            case 0xC3: Dcp(IzX()); break;
            case 0xD3: Dcp(IzY(Access.Modify)); break;
            case 0xE7: Isc(Zp()); break;
            case 0xF7: Isc(ZpX()); break;
            case 0xEF: Isc(Abs()); break;
            case 0xFF: Isc(AbsX(Access.Modify)); break;
            case 0xFB: Isc(AbsY(Access.Modify)); break;
            case 0xE3: Isc(IzX()); break;
            case 0xF3: Isc(IzY(Access.Modify)); break;

            // A and X together
            case 0x87: Write(Zp(), (byte)(A & X)); break;
            case 0x97: Write(ZpY(), (byte)(A & X)); break;
            case 0x8F: Write(Abs(), (byte)(A & X)); break;
            case 0x83: Write(IzX(), (byte)(A & X)); break;
            case 0xA7: A = X = NZ(Read(Zp())); break;
            case 0xB7: A = X = NZ(Read(ZpY())); break;
            case 0xAF: A = X = NZ(Read(Abs())); break;
            case 0xBF: A = X = NZ(Read(AbsY(Access.Read))); break;
            case 0xA3: A = X = NZ(Read(IzX())); break;
            case 0xB3: A = X = NZ(Read(IzY(Access.Read))); break;

            // Immediate oddities. ANE and LXA use the constant $EE, as Harte's
            // data does; real chips vary. See docs/known-differences.md.
            case 0x0B: case 0x2B: Anc(Read(PC++)); break;
            case 0x4B: Alr(Read(PC++)); break;
            case 0x6B: Arr(Read(PC++)); break;
            case 0x8B: A = NZ((byte)((A | 0xEE) & X & Read(PC++))); break;
            case 0xAB: A = X = NZ((byte)((A | 0xEE) & Read(PC++))); break;
            case 0xCB: Sbx(Read(PC++)); break;
            case 0xEB: SbcAt(PC++); break;

            // Stores that AND the value with one more than the high byte of the base.
            case 0x9C: { ushort b = FetchWord(); ReadHalfComputed(b, X); StoreHighAnd(b, X, Y); break; }
            case 0x9E: { ushort b = FetchWord(); ReadHalfComputed(b, Y); StoreHighAnd(b, Y, X); break; }
            case 0x9F: { ushort b = FetchWord(); ReadHalfComputed(b, Y); StoreHighAnd(b, Y, (byte)(A & X)); break; }
            case 0x93: { ushort b = PointerAt(Read(PC++)); ReadHalfComputed(b, Y); StoreHighAnd(b, Y, (byte)(A & X)); break; }
            case 0x9B: { ushort b = FetchWord(); ReadHalfComputed(b, Y); S = (byte)(A & X); StoreHighAnd(b, Y, S); break; }
            case 0xBB: A = X = S = NZ((byte)(Read(AbsY(Access.Read)) & S)); break;

            default:
                throw new NotImplementedException($"{Variant} opcode ${opcode:X2} is not implemented");
        }
    }

    /// <summary>
    /// A JAM opcode locks the chip. Harte's data records one step of it as
    /// the opcode fetch and these ten reads; after that the chip keeps reading $FFFF.
    /// </summary>
    private void Jam()
    {
        Read(PC);
        Read(0xFFFF);
        Read(0xFFFE);
        Read(0xFFFE);
        for (int i = 0; i < 6; i++)
        {
            Read(0xFFFF);
        }

        IsJammed = true;
    }

    private void Slo(ushort address)
    {
        byte value = Asl(ReadForModify(address));
        Write(address, value);
        Ora(value);
    }

    private void Rla(ushort address)
    {
        byte value = Rol(ReadForModify(address));
        Write(address, value);
        And(value);
    }

    private void Sre(ushort address)
    {
        byte value = Lsr(ReadForModify(address));
        Write(address, value);
        Eor(value);
    }

    private void Rra(ushort address)
    {
        byte value = Ror(ReadForModify(address));
        Write(address, value);
        Adc(value);
    }

    private void Dcp(ushort address)
    {
        byte value = (byte)(ReadForModify(address) - 1);
        Write(address, value);
        Compare(A, value);
    }

    private void Isc(ushort address)
    {
        byte value = (byte)(ReadForModify(address) + 1);
        Write(address, value);
        Sbc(value);
    }

    private void Anc(byte value)
    {
        And(value);
        SetFlag(C, Flag(N));
    }

    private void Alr(byte value)
    {
        byte masked = (byte)(A & value);
        SetFlag(C, (masked & 0x01) != 0);
        A = NZ((byte)(masked >> 1));
    }

    /// <summary>
    /// AND then ROR, with its own flag rules, and with its own decimal-mode
    /// adjustment on a chip that has decimal mode.
    /// </summary>
    private void Arr(byte value)
    {
        int carryIn = P & C;
        byte masked = (byte)(A & value);
        byte result = (byte)((carryIn << 7) | (masked >> 1));
        if (_decimal && Flag(D))
        {
            SetFlag(N, carryIn != 0);
            SetFlag(Z, result == 0);
            SetFlag(V, ((masked ^ result) & 0x40) != 0);
            int hi = masked >> 4;
            int lo = masked & 0x0F;
            if (lo + (lo & 1) > 5)
            {
                result = (byte)((result & 0xF0) | ((result + 6) & 0x0F));
            }

            bool carry = hi + (hi & 1) > 5;
            SetFlag(C, carry);
            if (carry)
            {
                result = (byte)(result + 0x60);
            }
        }
        else
        {
            NZ(result);
            SetFlag(C, (result & 0x40) != 0);
            SetFlag(V, (((result >> 6) ^ (result >> 5)) & 1) != 0);
        }

        A = result;
    }

    private void Sbx(byte value)
    {
        int difference = (A & X) - value;
        SetFlag(C, difference >= 0);
        X = NZ((byte)difference);
    }

    /// <summary>The NMOS throwaway read of the right low byte with the base's high byte.</summary>
    private void ReadHalfComputed(ushort baseAddress, byte index) =>
        Read((ushort)((baseAddress & 0xFF00) | ((baseAddress + index) & 0x00FF)));

    /// <summary>
    /// SHA, SHX, SHY and TAS store the value ANDed with one more than the
    /// base's high byte; on a page cross that also becomes the high byte of
    /// the address written.
    /// </summary>
    private void StoreHighAnd(ushort baseAddress, byte index, byte value)
    {
        ushort address = (ushort)(baseAddress + index);
        byte result = (byte)(value & ((baseAddress >> 8) + 1));
        if (((baseAddress ^ address) & 0xFF00) != 0)
        {
            address = (ushort)((result << 8) | (address & 0x00FF));
        }

        Write(address, result);
    }
}
