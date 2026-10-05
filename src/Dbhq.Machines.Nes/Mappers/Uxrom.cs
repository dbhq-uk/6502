namespace Dbhq.Machines.Nes.Mappers;

/// <summary>
/// UxROM, mapper 2: a switchable 16 KB bank at <c>$8000</c>, the last bank fixed at
/// <c>$C000</c>, 8 KB of CHR (RAM, normally), and the nametable arrangement from the header.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/nes/facts/mappers.md</c> section 4. The bank register is a whole 8 bits at
/// any address in <c>$8000</c> to <c>$FFFF</c>, as emulators treat it, and a number past the last
/// bank wraps.
/// </para>
/// <para>
/// Bus conflicts: the sheet says to model none. A NES 2.0 file whose submapper is 2 says the board
/// has AND-type conflicts (the sheet says the submappers tell, and the nesdev UxROM page gives 2 as
/// "AND-type bus conflicts"), and then the value written is ANDed with the ROM byte at the address
/// written. Submapper 0 (unknown, and every iNES file) and 1 have none.
/// </para>
/// </remarks>
public sealed class Uxrom : Board
{
    private readonly bool _busConflicts;

    /// <summary>A board from a parsed cartridge.</summary>
    internal Uxrom(Cartridge cartridge)
        : base(cartridge, 0x4000)
    {
        _busConflicts = cartridge.Submapper == 2;
        PowerOn();
    }

    /// <inheritdoc />
    public override void Reset(bool power)
    {
        base.Reset(power);
        if (power)
        {
            PowerOn();
        }
    }

    private protected override void WriteRegister(ushort address, byte value)
    {
        int bank = _busConflicts ? value & RomByte(address) : value;
        SetPrg16(0, bank);
    }

    private void PowerOn()
    {
        SetPrg16(0, 0);
        SetPrg16(1, Prg16Count - 1);
    }
}
