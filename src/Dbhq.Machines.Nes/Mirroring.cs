namespace Dbhq.Machines.Nes;

/// <summary>
/// How the cartridge connects the console's 2 KB of nametable RAM to the four nametable addresses
/// <c>$2000</c> to <c>$2FFF</c> (<c>docs/nes/facts/mappers.md</c> section 1).
/// </summary>
public enum Mirroring
{
    /// <summary>Horizontal mirroring: <c>$2000</c> and <c>$2400</c> are one table, <c>$2800</c> and <c>$2C00</c> the other (CIRAM A10 = PPU A11).</summary>
    Horizontal,

    /// <summary>Vertical mirroring: <c>$2000</c> and <c>$2800</c> are one table, <c>$2400</c> and <c>$2C00</c> the other (CIRAM A10 = PPU A10).</summary>
    Vertical,

    /// <summary>All four addresses show the first kilobyte.</summary>
    SingleScreenLow,

    /// <summary>All four addresses show the second kilobyte.</summary>
    SingleScreenHigh,

    /// <summary>Four separate tables, the extra two in RAM on the cartridge.</summary>
    FourScreen,
}
