namespace Dbhq.Cpu6502;

/// <summary>An opcode's mnemonic and addressing mode, for disassembly.</summary>
/// <param name="Undocumented">
/// True for an opcode the maker never documented. Traces mark it with a
/// star, as the Nintendulator log that comes with nestest does.
/// </param>
public readonly record struct OpcodeInfo(string Mnemonic, AddressingMode Mode, bool Undocumented)
{
    public int Length => Mode switch
    {
        AddressingMode.Implied or AddressingMode.Accumulator => 1,
        AddressingMode.Absolute or AddressingMode.AbsoluteX or AddressingMode.AbsoluteY
            or AddressingMode.Indirect or AddressingMode.AbsoluteIndexedIndirect
            or AddressingMode.ZeroPageRelative => 3,
        _ => 2,
    };
}

/// <summary>
/// What every opcode is on every variant. Used for disassembly only: the
/// CPU does not consult it to execute.
/// </summary>
public static class OpcodeTable
{
    // One row per high nibble. Undocumented names follow Nintendulator's
    // log (ISB, not ISC), so a trace can be compared with nestest.log.
    private static readonly string[] NmosRows =
    [
        "BRK imp,ORA izx,*JAM imp,*SLO izx,*NOP zp,ORA zp,ASL zp,*SLO zp,PHP imp,ORA imm,ASL acc,*ANC imm,*NOP abs,ORA abs,ASL abs,*SLO abs",
        "BPL rel,ORA izy,*JAM imp,*SLO izy,*NOP zpx,ORA zpx,ASL zpx,*SLO zpx,CLC imp,ORA aby,*NOP imp,*SLO aby,*NOP abx,ORA abx,ASL abx,*SLO abx",
        "JSR abs,AND izx,*JAM imp,*RLA izx,BIT zp,AND zp,ROL zp,*RLA zp,PLP imp,AND imm,ROL acc,*ANC imm,BIT abs,AND abs,ROL abs,*RLA abs",
        "BMI rel,AND izy,*JAM imp,*RLA izy,*NOP zpx,AND zpx,ROL zpx,*RLA zpx,SEC imp,AND aby,*NOP imp,*RLA aby,*NOP abx,AND abx,ROL abx,*RLA abx",
        "RTI imp,EOR izx,*JAM imp,*SRE izx,*NOP zp,EOR zp,LSR zp,*SRE zp,PHA imp,EOR imm,LSR acc,*ALR imm,JMP abs,EOR abs,LSR abs,*SRE abs",
        "BVC rel,EOR izy,*JAM imp,*SRE izy,*NOP zpx,EOR zpx,LSR zpx,*SRE zpx,CLI imp,EOR aby,*NOP imp,*SRE aby,*NOP abx,EOR abx,LSR abx,*SRE abx",
        "RTS imp,ADC izx,*JAM imp,*RRA izx,*NOP zp,ADC zp,ROR zp,*RRA zp,PLA imp,ADC imm,ROR acc,*ARR imm,JMP ind,ADC abs,ROR abs,*RRA abs",
        "BVS rel,ADC izy,*JAM imp,*RRA izy,*NOP zpx,ADC zpx,ROR zpx,*RRA zpx,SEI imp,ADC aby,*NOP imp,*RRA aby,*NOP abx,ADC abx,ROR abx,*RRA abx",
        "*NOP imm,STA izx,*NOP imm,*SAX izx,STY zp,STA zp,STX zp,*SAX zp,DEY imp,*NOP imm,TXA imp,*ANE imm,STY abs,STA abs,STX abs,*SAX abs",
        "BCC rel,STA izy,*JAM imp,*SHA izy,STY zpx,STA zpx,STX zpy,*SAX zpy,TYA imp,STA aby,TXS imp,*TAS aby,*SHY abx,STA abx,*SHX aby,*SHA aby",
        "LDY imm,LDA izx,LDX imm,*LAX izx,LDY zp,LDA zp,LDX zp,*LAX zp,TAY imp,LDA imm,TAX imp,*LXA imm,LDY abs,LDA abs,LDX abs,*LAX abs",
        "BCS rel,LDA izy,*JAM imp,*LAX izy,LDY zpx,LDA zpx,LDX zpy,*LAX zpy,CLV imp,LDA aby,TSX imp,*LAS aby,LDY abx,LDA abx,LDX aby,*LAX aby",
        "CPY imm,CMP izx,*NOP imm,*DCP izx,CPY zp,CMP zp,DEC zp,*DCP zp,INY imp,CMP imm,DEX imp,*SBX imm,CPY abs,CMP abs,DEC abs,*DCP abs",
        "BNE rel,CMP izy,*JAM imp,*DCP izy,*NOP zpx,CMP zpx,DEC zpx,*DCP zpx,CLD imp,CMP aby,*NOP imp,*DCP aby,*NOP abx,CMP abx,DEC abx,*DCP abx",
        "CPX imm,SBC izx,*NOP imm,*ISB izx,CPX zp,SBC zp,INC zp,*ISB zp,INX imp,SBC imm,NOP imp,*SBC imm,CPX abs,SBC abs,INC abs,*ISB abs",
        "BEQ rel,SBC izy,*JAM imp,*ISB izy,*NOP zpx,SBC zpx,INC zpx,*ISB zpx,SED imp,SBC aby,*NOP imp,*ISB aby,*NOP abx,SBC abx,INC abx,*ISB abx",
    ];

    // Every opcode the NMOS chip left undocumented, as the 65C02 defines it.
    // Opcodes in columns 7 and F, and $CB and $DB, depend on the variant.
    private static readonly string[] CmosChanges =
    [
        "02 *NOP imm", "22 *NOP imm", "42 *NOP imm", "62 *NOP imm", "82 *NOP imm", "C2 *NOP imm", "E2 *NOP imm",
        "12 ORA izp", "32 AND izp", "52 EOR izp", "72 ADC izp", "92 STA izp", "B2 LDA izp", "D2 CMP izp", "F2 SBC izp",
        "04 TSB zp", "0C TSB abs", "14 TRB zp", "1C TRB abs", "1A INC acc", "3A DEC acc",
        "34 BIT zpx", "3C BIT abx", "89 BIT imm", "5A PHY imp", "7A PLY imp", "DA PHX imp", "FA PLX imp",
        "64 STZ zp", "74 STZ zpx", "9C STZ abs", "9E STZ abx", "7C JMP iax", "80 BRA rel",
        "44 *NOP zp", "54 *NOP zpx", "D4 *NOP zpx", "F4 *NOP zpx", "5C *NOP abs", "DC *NOP abs", "FC *NOP abs",
    ];

    private static readonly Dictionary<CpuVariant, OpcodeInfo[]> Tables = new()
    {
        [CpuVariant.Nmos6502] = Nmos(),
        [CpuVariant.Ricoh2A03] = Nmos(),
        [CpuVariant.Synertek65C02] = Cmos(CpuVariant.Synertek65C02),
        [CpuVariant.Rockwell65C02] = Cmos(CpuVariant.Rockwell65C02),
        [CpuVariant.Wdc65C02] = Cmos(CpuVariant.Wdc65C02),
    };

    public static OpcodeInfo Get(CpuVariant variant, byte opcode) => Tables[variant][opcode];

    private static OpcodeInfo[] Nmos()
    {
        var table = new OpcodeInfo[256];
        for (int row = 0; row < 16; row++)
        {
            string[] entries = NmosRows[row].Split(',');
            for (int column = 0; column < 16; column++)
            {
                table[row * 16 + column] = Parse(entries[column]);
            }
        }

        return table;
    }

    private static OpcodeInfo[] Cmos(CpuVariant variant)
    {
        OpcodeInfo[] table = Nmos();
        for (int opcode = 0; opcode < 256; opcode++)
        {
            if (table[opcode].Undocumented)
            {
                // Every x3 and xB not listed below is a one-byte no-op.
                table[opcode] = new OpcodeInfo("NOP", AddressingMode.Implied, true);
            }
        }

        foreach (string change in CmosChanges)
        {
            table[Convert.ToByte(change[..2], 16)] = Parse(change[3..]);
        }

        bool bitInstructions = variant != CpuVariant.Synertek65C02;
        for (int bit = 0; bit < 8; bit++)
        {
            int row = bit << 4;
            table[row | 0x07] = bitInstructions ? new($"RMB{bit}", AddressingMode.ZeroPage, false) : new("NOP", bit % 2 == 0 ? AddressingMode.ZeroPage : AddressingMode.ZeroPageX, true);
            table[0x80 | row | 0x07] = bitInstructions ? new($"SMB{bit}", AddressingMode.ZeroPage, false) : new("NOP", bit % 2 == 0 ? AddressingMode.ZeroPage : AddressingMode.ZeroPageX, true);
            table[row | 0x0F] = bitInstructions ? new($"BBR{bit}", AddressingMode.ZeroPageRelative, false) : new("NOP", AddressingMode.Absolute, true);
            table[0x80 | row | 0x0F] = bitInstructions ? new($"BBS{bit}", AddressingMode.ZeroPageRelative, false) : new("NOP", AddressingMode.Absolute, true);
        }

        if (variant == CpuVariant.Wdc65C02)
        {
            table[0xCB] = new OpcodeInfo("WAI", AddressingMode.Implied, false);
            table[0xDB] = new OpcodeInfo("STP", AddressingMode.Implied, false);
        }
        else
        {
            table[0xDB] = new OpcodeInfo("NOP", AddressingMode.ZeroPageX, true);
        }

        return table;
    }

    private static OpcodeInfo Parse(string entry)
    {
        string[] parts = entry.Split(' ');
        bool undocumented = parts[0].StartsWith('*');
        AddressingMode mode = parts[1] switch
        {
            "imp" => AddressingMode.Implied,
            "acc" => AddressingMode.Accumulator,
            "imm" => AddressingMode.Immediate,
            "zp" => AddressingMode.ZeroPage,
            "zpx" => AddressingMode.ZeroPageX,
            "zpy" => AddressingMode.ZeroPageY,
            "abs" => AddressingMode.Absolute,
            "abx" => AddressingMode.AbsoluteX,
            "aby" => AddressingMode.AbsoluteY,
            "izx" => AddressingMode.IndirectX,
            "izy" => AddressingMode.IndirectY,
            "ind" => AddressingMode.Indirect,
            "rel" => AddressingMode.Relative,
            "izp" => AddressingMode.ZeroPageIndirect,
            "iax" => AddressingMode.AbsoluteIndexedIndirect,
            _ => throw new FormatException($"Unknown addressing mode in '{entry}'"),
        };
        return new OpcodeInfo(parts[0].TrimStart('*'), mode, undocumented);
    }
}
