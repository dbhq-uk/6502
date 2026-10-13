using System.Diagnostics.CodeAnalysis;

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
public sealed class Nrom : IMapper, IReportsState
{
    private readonly byte[] _prg;
    private readonly byte[] _chr;
    private readonly bool _chrIsRam;

    // Each memory's length less one when it is a power of two, as every real NROM's is, so an
    // access masks instead of dividing; -1 for any other length, which takes the remainder.
    private readonly int _prgMask;
    private readonly int _chrMask;
    private readonly int _prgRamMask;

    /// <summary>A board from a parsed cartridge. A CHR RAM is the board's own, so each board starts with it clear.</summary>
    internal Nrom(Cartridge cartridge)
    {
        _prg = cartridge.Prg;
        _chrIsRam = cartridge.ChrIsRam;
        _chr = _chrIsRam ? new byte[cartridge.ChrRamSize] : cartridge.Chr;
        PrgRam = new byte[cartridge.PrgRamSize];
        Mirroring = cartridge.Mirroring;
        NametablePages.Fill(NametablePageTable, Mirroring);
        _prgMask = MaskFor(_prg.Length);
        _chrMask = MaskFor(_chr.Length);
        _prgRamMask = MaskFor(PrgRam.Length);
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
            return _prg[Wrap(address - 0x8000, _prgMask, _prg.Length)];
        }

        if (address >= 0x6000 && PrgRam.Length > 0)
        {
            return PrgRam[Wrap(address - 0x6000, _prgRamMask, PrgRam.Length)];
        }

        return openBus;
    }

    /// <inheritdoc />
    public void CpuWrite(ushort address, byte value)
    {
        if (address is >= 0x6000 and < 0x8000 && PrgRam.Length > 0)
        {
            PrgRam[Wrap(address - 0x6000, _prgRamMask, PrgRam.Length)] = value;
        }
    }

    /// <inheritdoc />
    public byte PpuRead(ushort address)
    {
        return _chr.Length == 0 ? (byte)0 : _chr[Wrap(address & 0x1FFF, _chrMask, _chr.Length)];
    }

    /// <inheritdoc />
    public void PpuWrite(ushort address, byte value)
    {
        if (_chrIsRam && _chr.Length > 0)
        {
            _chr[Wrap(address & 0x1FFF, _chrMask, _chr.Length)] = value;
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
    public bool WatchesPpuAddresses => false;

    /// <inheritdoc />
    public bool CountsCpuCycles => false;

    /// <inheritdoc />
    public bool CanInterrupt => false;

    /// <inheritdoc />
    public int[] NametablePageTable { get; } = new int[4];

    /// <inheritdoc />
    /// <remarks>
    /// True when the PRG is a whole number of 8 KB, as every real NROM's 16 or 32 KB is: then
    /// <see cref="CpuRead"/>'s remainder lands each 8 KB window on a whole bank, 16 KB repeating
    /// at <c>$C000</c>. Any other length is false, and the bus calls.
    /// </remarks>
    public bool TryGetPrgWindows([NotNullWhen(true)] out byte[]? prg, [NotNullWhen(true)] out int[]? windows)
    {
        if (_prg.Length == 0 || _prg.Length % 0x2000 != 0)
        {
            prg = null;
            windows = null;
            return false;
        }

        prg = _prg;
        windows = new int[4];
        for (int window = 0; window < windows.Length; window++)
        {
            windows[window] = window * 0x2000 % _prg.Length;
        }

        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// True when the CHR is at least 8 KB, as every real NROM's is: then <see cref="PpuRead"/>'s
    /// remainder never wraps and the windows are the first 8 KB in order. A smaller CHR wraps
    /// inside 8 KB, which the windows cannot say, so it is false and the PPU calls.
    /// </remarks>
    public bool TryGetPatternWindows([NotNullWhen(true)] out byte[]? chr, [NotNullWhen(true)] out int[]? windows)
    {
        if (_chr.Length < 0x2000)
        {
            chr = null;
            windows = null;
            return false;
        }

        chr = _chr;
        windows = [0x0000, 0x0400, 0x0800, 0x0C00, 0x1000, 0x1400, 0x1800, 0x1C00];
        return true;
    }

    /// <inheritdoc />
    public void Reset(bool power)
    {
        if (power && _chrIsRam)
        {
            Array.Clear(_chr);
        }
    }

    /// <inheritdoc />
    public void ClearPrgRam()
    {
        Array.Clear(PrgRam);
    }

    /// <inheritdoc />
    void IReportsState.ReportState(IStateSink sink)
    {
        sink.Skip(nameof(_prg), "ROM");
        if (_chrIsRam)
        {
            sink.Add(nameof(_chr), _chr);
        }
        else
        {
            sink.Skip(nameof(_chr), "ROM");
        }

        sink.Skip(nameof(_chrIsRam), StateReport.Fixed);
        sink.Skip(nameof(_prgMask), StateReport.Fixed);
        sink.Skip(nameof(_chrMask), StateReport.Fixed);
        sink.Skip(nameof(_prgRamMask), StateReport.Fixed);
        sink.Skip(nameof(Mirroring), StateReport.Fixed);
        sink.Skip(nameof(NametablePageTable), StateReport.Fixed);
        sink.Add(nameof(PrgRam), PrgRam);
    }

    // The length less one for a power of two, else -1 (and -1 for an empty memory, never read).
    private static int MaskFor(int length) => length > 0 && System.Numerics.BitOperations.IsPow2(length) ? length - 1 : -1;

    // An offset into a memory of the given length, repeating it: the same as the remainder.
    private static int Wrap(int offset, int mask, int length) => mask >= 0 ? offset & mask : offset % length;
}
