namespace Dbhq.Cpu6502;

/// <summary>The member of the 6502 family to behave as.</summary>
public enum CpuVariant
{
    /// <summary>The original NMOS 6502 and its cut-down versions, undocumented opcodes included.</summary>
    Nmos6502,

    /// <summary>Ricoh's 2A03 and 2A07 in the NES: the NMOS 6502 with decimal mode removed.</summary>
    Ricoh2A03,

    /// <summary>The Synertek 65C02, also called the 65SC02.</summary>
    Synertek65C02,

    /// <summary>The Rockwell 65C02: the Synertek set plus BBR, BBS, RMB and SMB.</summary>
    Rockwell65C02,

    /// <summary>The WDC 65C02: the Rockwell set plus WAI and STP.</summary>
    Wdc65C02,
}
