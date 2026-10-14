namespace Dbhq.Machines.Nes.Mappers;

/// <summary>
/// CNROM, mapper 3: 16 or 32 KB of PRG ROM that is not banked (16 KB repeats at <c>$C000</c>), a
/// switchable 8 KB bank of CHR, and the nametable arrangement from the header.
/// </summary>
/// <remarks>
/// Built from <c>docs/nes/facts/mappers.md</c> section 5. The write at <c>$8000</c> to
/// <c>$FFFF</c> picks the CHR bank; a number past the last bank wraps, which for the usual four
/// banks is the sheet's bits 1 to 0. The original board has AND-type bus conflicts, so the value is
/// ANDed with the ROM byte at the address written unless the NES 2.0 submapper is 1 (none). The
/// sheet marks this [guessing - verify]: no test ROM in the plan writes a value that differs from
/// the ROM byte.
/// </remarks>
public sealed class Cnrom : Board
{
    private readonly bool _busConflicts;

    /// <summary>A board from a parsed cartridge.</summary>
    internal Cnrom(Cartridge cartridge)
        : base(cartridge, 0x4000)
    {
        _busConflicts = cartridge.Submapper != 1;
        SetChr8(0);
    }

    /// <inheritdoc />
    public override void Reset(bool power)
    {
        base.Reset(power);
        if (power)
        {
            SetChr8(0);
        }
    }

    /// <inheritdoc />
    private protected override void ReportState(IStateSink sink)
    {
        base.ReportState(sink);
        sink.Skip(nameof(_busConflicts), StateReport.Fixed);
    }

    private protected override void WriteRegister(ushort address, byte value)
    {
        SetChr8(_busConflicts ? value & RomByte(address) : value);
    }
}
