// A small 6502 assembler for the differential's synthetic cartridges (Synthetic.cs): each
// instruction is a method named for its mnemonic and addressing mode, labels are resolved when the
// code is assembled, and a branch out of range is an error.
internal sealed class Asm(int origin)
{
    private readonly List<byte> _code = [];
    private readonly Dictionary<string, int> _labels = new(StringComparer.Ordinal);
    private readonly List<(int At, string Label, char Kind)> _fixups = [];

    public int Here => origin + _code.Count;

    public void Label(string name) => _labels.Add(name, Here);

    public void Data(params byte[] bytes) => _code.AddRange(bytes);

    public void Data(IEnumerable<byte> bytes) => _code.AddRange(bytes);

    public byte[] Assemble()
    {
        byte[] code = [.. _code];
        foreach (var (at, label, kind) in _fixups)
        {
            int target = _labels.TryGetValue(label, out int t) ? t : throw new InvalidOperationException($"no label {label}");
            switch (kind)
            {
                case 'a':
                    code[at] = (byte)target;
                    code[at + 1] = (byte)(target >> 8);
                    break;
                case '<':
                    code[at] = (byte)target;
                    break;
                case '>':
                    code[at] = (byte)(target >> 8);
                    break;
                default:
                    int offset = target - (origin + at + 1);
                    code[at] = offset is >= -128 and <= 127 ? (byte)(sbyte)offset : throw new InvalidOperationException($"branch to {label} out of range");
                    break;
            }
        }

        return code;
    }

    public int Address(string label) => _labels[label];

    // Addressing modes.
    private void Imp(byte op) => _code.Add(op);

    private void Imm(byte op, int value)
    {
        _code.Add(op);
        _code.Add((byte)value);
    }

    private void Abs(byte op, int address)
    {
        _code.Add(op);
        _code.Add((byte)address);
        _code.Add((byte)(address >> 8));
    }

    private void Abs(byte op, string label)
    {
        _code.Add(op);
        _fixups.Add((_code.Count, label, 'a'));
        _code.Add(0);
        _code.Add(0);
    }

    private void Branch(byte op, string label)
    {
        _code.Add(op);
        _fixups.Add((_code.Count, label, 'r'));
        _code.Add(0);
    }

    // The instructions the programs use. A number below $100 is zero page where the 6502 has that
    // mode; "I" is immediate, "X" and "Y" indexed absolute, "IndY" (zp),Y.
    public void LdaI(int v) => Imm(0xA9, v);

    public void LdaLo(string label)
    {
        _code.Add(0xA9);
        _fixups.Add((_code.Count, label, '<'));
        _code.Add(0);
    }

    public void LdaHi(string label)
    {
        _code.Add(0xA9);
        _fixups.Add((_code.Count, label, '>'));
        _code.Add(0);
    }

    public void Lda(int a) => Mem(0xA5, 0xAD, a);

    public void LdaX(string label) => Abs(0xBD, label);

    public void LdaX(int a) => Abs(0xBD, a);

    public void LdaIndY(int zp) => Imm(0xB1, zp);

    public void LdxI(int v) => Imm(0xA2, v);

    public void Ldx(int a) => Mem(0xA6, 0xAE, a);

    public void LdyI(int v) => Imm(0xA0, v);

    public void Sta(int a) => Mem(0x85, 0x8D, a);

    public void StaX(int a) => Abs(0x9D, a);

    public void StaX(string label) => Abs(0x9D, label);

    public void StaIndY(int zp) => Imm(0x91, zp);

    public void Inc(int a) => Mem(0xE6, 0xEE, a);

    public void Inc(string label) => Abs(0xEE, label);

    public void Lsr(int zp) => Imm(0x46, zp);

    public void Ror(int zp) => Imm(0x66, zp);

    public void Bit(int a) => Mem(0x24, 0x2C, a);

    public void AndI(int v) => Imm(0x29, v);

    public void OraI(int v) => Imm(0x09, v);

    public void Ora(int zp) => Imm(0x05, zp);

    public void OraX(string label) => Abs(0x1D, label);

    public void EorI(int v) => Imm(0x49, v);

    public void AdcI(int v) => Imm(0x69, v);

    public void Adc(int zp) => Imm(0x65, zp);

    public void AdcX(string label) => Abs(0x7D, label);

    public void AdcX(int a) => Abs(0x7D, a);

    public void SbcI(int v) => Imm(0xE9, v);

    public void SbcZ(int zp) => Imm(0xE5, zp);

    public void CmpI(int v) => Imm(0xC9, v);

    public void CpxI(int v) => Imm(0xE0, v);

    public void AslA() => Imp(0x0A);

    public void LsrA() => Imp(0x4A);

    public void RolA() => Imp(0x2A);

    public void Inx() => Imp(0xE8);

    public void Iny() => Imp(0xC8);

    public void Dex() => Imp(0xCA);

    public void Dey() => Imp(0x88);

    public void Tax() => Imp(0xAA);

    public void Txa() => Imp(0x8A);

    public void Tay() => Imp(0xA8);

    public void Tya() => Imp(0x98);

    public void Txs() => Imp(0x9A);

    public void Pha() => Imp(0x48);

    public void Php() => Imp(0x08);

    public void Plp() => Imp(0x28);

    public void Pla() => Imp(0x68);

    public void Clc() => Imp(0x18);

    public void Sec() => Imp(0x38);

    public void Sei() => Imp(0x78);

    public void Cli() => Imp(0x58);

    public void Cld() => Imp(0xD8);

    public void Nop() => Imp(0xEA);

    public void Rts() => Imp(0x60);

    public void Rti() => Imp(0x40);

    public void Jmp(string label) => Abs(0x4C, label);

    public void JmpInd(int zp) => Abs(0x6C, zp);

    public void Jsr(string label) => Abs(0x20, label);

    public void Bpl(string label) => Branch(0x10, label);

    public void Bmi(string label) => Branch(0x30, label);

    public void Bvc(string label) => Branch(0x50, label);

    public void Bvs(string label) => Branch(0x70, label);

    public void Bcc(string label) => Branch(0x90, label);

    public void Bcs(string label) => Branch(0xB0, label);

    public void Bne(string label) => Branch(0xD0, label);

    public void Beq(string label) => Branch(0xF0, label);

    private void Mem(byte zp, byte abs, int a)
    {
        if (a < 0x100)
        {
            Imm(zp, a);
        }
        else
        {
            Abs(abs, a);
        }
    }
}
