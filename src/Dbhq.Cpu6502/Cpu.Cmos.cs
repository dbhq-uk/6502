namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    /// <summary>The 65C02's additions, and its defined no-ops, which differ by variant.</summary>
    private void ExecuteCmos(byte opcode)
    {
        if ((opcode & 0x0F) == 0x07)
        {
            // RMB and SMB on Rockwell and WDC. On Synertek a two-byte no-op
            // that reads like LDA zp in even rows and LDA zp,X in odd ones.
            if (_bitInstructions)
            {
                ResetOrSetBit(opcode);
            }
            else if ((opcode & 0x10) == 0)
            {
                Read(Zp());
            }
            else
            {
                byte b = Read(PC++);
                Read(b);
                Read((byte)(b + X));
            }

            return;
        }

        if ((opcode & 0x0F) == 0x0F)
        {
            // BBR and BBS on Rockwell and WDC. On Synertek a three-byte no-op
            // of three cycles in even rows and four in odd ones.
            if (_bitInstructions)
            {
                BranchOnBit(opcode);
            }
            else
            {
                FetchWord();
                if ((opcode & 0x10) != 0)
                {
                    Read((ushort)(PC - 1));
                }
            }

            return;
        }

        switch (opcode)
        {
            // The (zp) addressing mode
            case 0x12: Ora(Read(Izp())); break;
            case 0x32: And(Read(Izp())); break;
            case 0x52: Eor(Read(Izp())); break;
            case 0x72: AdcAt(Izp()); break;
            case 0x92: Write(Izp(), A); break;
            case 0xB2: A = NZ(Read(Izp())); break;
            case 0xD2: Compare(A, Read(Izp())); break;
            case 0xF2: SbcAt(Izp()); break;

            // New instructions
            case 0x04: Tsb(Zp()); break;
            case 0x0C: Tsb(Abs()); break;
            case 0x14: Trb(Zp()); break;
            case 0x1C: Trb(Abs()); break;
            case 0x1A: Read(PC); A = NZ((byte)(A + 1)); break;
            case 0x3A: Read(PC); A = NZ((byte)(A - 1)); break;
            case 0x34: Bit(Read(ZpX())); break;
            case 0x3C: Bit(Read(AbsX(Access.Read))); break;
            case 0x89: SetFlag(Z, (A & Read(PC++)) == 0); break;
            case 0x5A: Read(PC); Push(Y); break;
            case 0xDA: Read(PC); Push(X); break;
            case 0x7A: Read(PC); Read(StackAddress); Y = NZ(Pull()); break;
            case 0xFA: Read(PC); Read(StackAddress); X = NZ(Pull()); break;
            case 0x64: Write(Zp(), 0); break;
            case 0x74: Write(ZpX(), 0); break;
            case 0x9C: Write(Abs(), 0); break;
            case 0x9E: Write(AbsX(Access.Write), 0); break;
            case 0x7C: JmpIndexedIndirect(); break;
            case 0x80: Branch(true); break;

            // WDC only. The cycle counts are from WDC's datasheet: Harte has no data for them.
            case 0xCB when _waitAndStop: Read(PC); Read(PC); IsWaiting = true; break;
            case 0xDB when _waitAndStop: Read(PC); Read(PC); IsStopped = true; break;

            // Defined no-ops. Lengths and timings are Harte's; see the 30 September journal entry.
            case 0xCB: Read(PC); break;
            case 0xDB: case 0x54: case 0xD4: case 0xF4: { byte b = Read(PC++); Read(b); Read((byte)(b + X)); break; }
            case 0x02: case 0x22: case 0x42: case 0x62: case 0x82: case 0xC2: case 0xE2: Read(PC++); break;
            case 0x44: Read(Zp()); break;
            case 0x5C: case 0xDC: case 0xFC: FetchWord(); Read((ushort)(PC - 1)); break;

            default:
                if ((opcode & 0x03) == 0x03)
                {
                    // Every other x3 and xB: one byte, one cycle, nothing after the fetch.
                    break;
                }

                throw new NotImplementedException($"{Variant} opcode ${opcode:X2} is not implemented");
        }
    }

    private void Tsb(ushort address)
    {
        byte value = ReadForModify(address);
        SetFlag(Z, (A & value) == 0);
        Write(address, (byte)(value | A));
    }

    private void Trb(ushort address)
    {
        byte value = ReadForModify(address);
        SetFlag(Z, (A & value) == 0);
        Write(address, (byte)(value & ~A));
    }

    private void JmpIndexedIndirect()
    {
        ushort baseAddress = FetchWord();
        Read((ushort)(PC - 2));
        ushort pointer = (ushort)(baseAddress + X);
        byte lo = Read(pointer);
        byte hi = Read((ushort)(pointer + 1));
        PC = (ushort)(lo | hi << 8);
    }

    private void ResetOrSetBit(byte opcode)
    {
        int bit = (opcode >> 4) & 7;
        ushort address = Zp();
        byte value = ReadForModify(address);
        Write(address, opcode < 0x80 ? (byte)(value & ~(1 << bit)) : (byte)(value | (1 << bit)));
    }

    private void BranchOnBit(byte opcode)
    {
        int bit = (opcode >> 4) & 7;
        bool branchIfSet = opcode >= 0x80;
        byte zeroPage = Read(PC++);
        byte value = Read(zeroPage);
        Read(zeroPage);
        sbyte offset = (sbyte)Read(PC++);
        if ((((value >> bit) & 1) != 0) != branchIfSet)
        {
            return;
        }

        Read(PC);
        ushort target = (ushort)(PC + offset);
        if ((target & 0xFF00) != (PC & 0xFF00))
        {
            Read(PC);
        }

        PC = target;
    }
}
