namespace Dbhq.Cpu6502;

/// <summary>How an instruction finds its operand, which sets its length.</summary>
public enum AddressingMode
{
    Implied,
    Accumulator,
    Immediate,
    ZeroPage,
    ZeroPageX,
    ZeroPageY,
    Absolute,
    AbsoluteX,
    AbsoluteY,
    IndirectX,
    IndirectY,
    Indirect,
    Relative,

    /// <summary>The 65C02's (zp).</summary>
    ZeroPageIndirect,

    /// <summary>The 65C02's JMP (abs,X).</summary>
    AbsoluteIndexedIndirect,

    /// <summary>BBR and BBS: a zero-page address and a branch offset.</summary>
    ZeroPageRelative,
}
