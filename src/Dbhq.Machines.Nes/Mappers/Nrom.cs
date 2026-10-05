namespace Dbhq.Machines.Nes.Mappers;

/// <summary>
/// NROM, mapper 0: the board with no registers. 16 or 32 KB of PRG ROM at <c>$8000</c>, with the
/// 16 KB form repeated at <c>$C000</c>, 8 KB of CHR (ROM, or RAM where the file has none), and the
/// nametable arrangement fixed by the header.
/// </summary>
/// <remarks>
/// Built from <c>docs/nes/facts/mappers.md</c> section 2. PRG RAM is whatever the header's size
/// says, at <c>$6000</c> to <c>$7FFF</c>, repeated through that window when it is smaller than 8
/// KB. The sheet says only Family BASIC has any on a real NROM board, and that most emulators give
/// 8 KB; the header decides here, so the test ROMs, which report through <c>$6000</c>, work.
/// </remarks>
public sealed class Nrom : IMapper
{
    private readonly byte[] _prg;
    private readonly byte[] _chr;
    private readonly bool _chrIsRam;

    /// <summary>A board from a parsed cartridge. A CHR RAM is the board's own, so each board starts with it clear.</summary>
    internal Nrom(Cartridge cartridge)
    {
        _prg = cartridge.Prg;
        _chrIsRam = cartridge.ChrIsRam;
        _chr = _chrIsRam ? new byte[cartridge.ChrRamSize] : cartridge.Chr;
        PrgRam = new byte[cartridge.PrgRamSize];
        Mirroring = cartridge.Mirroring;
    }

    /// <inheritdoc />
    public Mirroring Mirroring { get; }

    /// <inheritdoc />
    public bool Irq => false;

    /// <inheritdoc />
    public byte[] PrgRam { get; }

    /// <inheritdoc />
    public byte CpuRead(ushort address, byte openBus)
    {
        if (address >= 0x8000)
        {
            return _prg[(address - 0x8000) % _prg.Length];
        }

        if (address >= 0x6000 && PrgRam.Length > 0)
        {
            return PrgRam[(address - 0x6000) % PrgRam.Length];
        }

        return openBus;
    }

    /// <inheritdoc />
    public void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and < 0x8000 && PrgRam.Length > 0)
        {
            PrgRam[(address - 0x6000) % PrgRam.Length] = value;
        }
    }

    /// <inheritdoc />
    public byte PpuRead(ushort address)
    {
        return _chr.Length == 0 ? (byte)0 : _chr[(address & 0x1FFF) % _chr.Length];
    }

    /// <inheritdoc />
    public void PpuWrite(ushort address, byte value)
    {
        if (_chrIsRam && _chr.Length > 0)
        {
            _chr[(address & 0x1FFF) % _chr.Length] = value;
        }
    }

    /// <inheritdoc />
    public void PpuAddressChanged(ushort address, long cpuCycle)
    {
    }

    /// <inheritdoc />
    public void CpuCycle()
    {
    }

    /// <inheritdoc />
    public void ClearPrgRam()
    {
        Array.Clear(PrgRam);
    }
}
