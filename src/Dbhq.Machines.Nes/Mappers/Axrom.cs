namespace Dbhq.Machines.Nes.Mappers;

/// <summary>
/// AxROM, mapper 7: a switchable 32 KB bank across <c>$8000</c> to <c>$FFFF</c>, 8 KB of CHR RAM,
/// and one nametable page for all four tables, chosen by the same write.
/// </summary>
/// <remarks>
/// Built from <c>docs/nes/facts/mappers.md</c> section 5. A write is <c>xxxM xPPP</c>: bits 2 to 0
/// the PRG bank (wrapped to the banks there are, so a 16 KB file is half a bank shown twice), bit 4
/// the page. The sheet says to model no bus conflicts. A NES 2.0 file whose submapper is 2 says
/// the board has them (the nesdev AxROM page gives 2 as "AND-type bus conflicts"), and then the
/// value is ANDed with the ROM byte at the address written, which also loses bit 4 where the ROM
/// byte lacks it.
/// </remarks>
public sealed class Axrom : Board
{
    private readonly bool _busConflicts;

    /// <summary>A board from a parsed cartridge.</summary>
    internal Axrom(Cartridge cartridge)
        : base(cartridge, 0x8000)
    {
        _busConflicts = cartridge.Submapper == 2;
        Select(0);
    }

    /// <inheritdoc />
    public override void Reset(bool power)
    {
        base.Reset(power);
        if (power)
        {
            Select(0);
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
        Select(_busConflicts ? value & RomByte(address) : value);
    }

    private void Select(int value)
    {
        SetPrg32(value & 7);
        Mirroring = (value & 0x10) != 0 ? Mirroring.SingleScreenHigh : Mirroring.SingleScreenLow;
    }
}
