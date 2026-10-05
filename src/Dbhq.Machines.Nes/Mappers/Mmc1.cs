namespace Dbhq.Machines.Nes.Mappers;

/// <summary>
/// MMC1, mapper 1: a 5-bit serial port, a control register, two CHR bank registers and a PRG bank
/// register, with 8 KB of PRG RAM at <c>$6000</c> that the PRG register can switch off.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/nes/facts/mappers.md</c> section 3. Every write at <c>$8000</c> to
/// <c>$FFFF</c> shifts bit 0 in, low bit first; the fifth goes to the register that address bits
/// 14 and 13 name. A write with bit 7 set clears the shift register and ORs <c>$0C</c> into the
/// control register, and is never ignored. A write on the cycle straight after a write is ignored:
/// <see cref="Board.CpuCycle"/> counts the cycles since the last write, and a read-modify-write's
/// second write falls on the cycle after its first. A write that is ignored still counts as a
/// write, so a run of them takes only the first.
/// </para>
/// <para>
/// The chip has no reset line, so the console's reset button changes nothing in it, and only
/// <see cref="Reset"/> with power true puts it back: control <c>$0C</c> (PRG mode 3), the other
/// registers 0 and the shift register empty. The sheet says only the power-on state; that the reset
/// button leaves the chip alone is from the nesdev wiki's silence on any reset and the chip's
/// pinout, and is not on the sheet [guessing - verify].
/// </para>
/// <para>
/// Bank numbers wrap modulo the banks there are, so a 16 KB PRG is bank 0 in every mode. The plan's
/// plain SxROM case only: the larger SOROM, SUROM, SXROM and SZROM boards, which bank PRG RAM or
/// more PRG through the CHR registers, are not modelled (the sheet's open item 3).
/// </para>
/// </remarks>
public sealed class Mmc1 : Board
{
    private int _shift;
    private int _count;
    private int _control;
    private int _chr0;
    private int _chr1;
    private int _prg;
    private int _sinceWrite;

    /// <summary>A board from a parsed cartridge.</summary>
    internal Mmc1(Cartridge cartridge)
        : base(cartridge, 0x4000)
    {
        PowerOn();
    }

    /// <inheritdoc />
    public override bool CountsCpuCycles => true;

    /// <inheritdoc />
    public override void CpuCycle()
    {
        if (_sinceWrite < 2)
        {
            _sinceWrite++;
        }
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
        // Cycles since the last write: 1 means this write is on the cycle straight after one.
        bool consecutive = _sinceWrite == 1;
        _sinceWrite = 0;

        if ((value & 0x80) != 0)
        {
            _shift = 0;
            _count = 0;
            _control |= 0x0C;
            ApplyRegisters();
            return;
        }

        if (consecutive)
        {
            return;
        }

        _shift |= (value & 1) << _count;
        if (++_count < 5)
        {
            return;
        }

        int loaded = _shift;
        _shift = 0;
        _count = 0;
        switch ((address >> 13) & 3)
        {
            case 0:
                _control = loaded;
                break;
            case 1:
                _chr0 = loaded;
                break;
            case 2:
                _chr1 = loaded;
                break;
            default:
                _prg = loaded;
                break;
        }

        ApplyRegisters();
    }

    private void PowerOn()
    {
        _shift = 0;
        _count = 0;
        _control = 0x0C;
        _chr0 = 0;
        _chr1 = 0;
        _prg = 0;
        _sinceWrite = 2;
        ApplyRegisters();
    }

    // Turns the registers into the window bases the reads use, so a read does no work of its own.
    private void ApplyRegisters()
    {
        Mirroring = (_control & 3) switch
        {
            0 => Mirroring.SingleScreenLow,
            1 => Mirroring.SingleScreenHigh,
            2 => Mirroring.Vertical,
            _ => Mirroring.Horizontal,
        };

        int bank = _prg & 0x0F;
        switch ((_control >> 2) & 3)
        {
            case 0:
            case 1:
                // 32 KB: the low bit is ignored, and both halves wrap on their own.
                SetPrg16(0, bank & 0x0E);
                SetPrg16(1, (bank & 0x0E) + 1);
                break;
            case 2:
                SetPrg16(0, 0);
                SetPrg16(1, bank);
                break;
            default:
                SetPrg16(0, bank);
                SetPrg16(1, Prg16Count - 1);
                break;
        }

        PrgRamEnabled = (_prg & 0x10) == 0;

        if ((_control & 0x10) == 0)
        {
            // 8 KB: one bank number, its low bit ignored, the second register unused.
            SetChr4(0, _chr0 & 0x1E);
            SetChr4(1, (_chr0 & 0x1E) + 1);
        }
        else
        {
            SetChr4(0, _chr0);
            SetChr4(1, _chr1);
        }
    }
}
