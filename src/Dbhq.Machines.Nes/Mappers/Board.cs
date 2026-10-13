using System.Diagnostics.CodeAnalysis;

namespace Dbhq.Machines.Nes.Mappers;

/// <summary>
/// What the boards share: PRG ROM read through four 8 KB windows, CHR read through eight 1 KB
/// windows, PRG RAM at <c>$6000</c>, and CHR RAM that a power cycle clears. A board sets the
/// window bases when its registers change, so a read is one array access and one add.
/// </summary>
/// <remarks>
/// <para>
/// A visitor's file may have fewer banks than a board's registers can name, so every memory is
/// first made a whole number of banks (<see cref="Fit"/>) and every bank number is taken modulo the
/// count. Then no address a board computes can fall outside its array.
/// </para>
/// <para>
/// Built from <c>docs/nes/facts/mappers.md</c> sections 3 to 6. Reads are pure (they change
/// nothing in the board), as <see cref="IMapper.CpuRead"/> asks.
/// </para>
/// </remarks>
public abstract class Board : IMapper, IReportsState
{
    private const int PrgWindow = 0x2000;
    private const int ChrWindow = 0x400;
    private const int ChrBank = 0x2000;

    private readonly bool _chrIsRam;

    // The PRG RAM's length less one when it is a power of two, as every NES 2.0 size is, so an
    // access masks instead of dividing; -1 for any other length, which takes the remainder.
    private readonly int _prgRamMask;

    /// <summary>
    /// Builds a board's memory from a parsed cartridge, with PRG in whole banks of
    /// <paramref name="prgBankSize"/> and CHR in whole 8 KB banks, at least one.
    /// </summary>
    private protected Board(Cartridge cartridge, int prgBankSize)
    {
        Prg = Fit(cartridge.Prg, prgBankSize);
        _chrIsRam = cartridge.ChrIsRam;
        Chr = _chrIsRam
            ? new byte[Math.Max(ChrBank, RoundUp(cartridge.ChrRamSize, ChrBank))]
            : Fit(cartridge.Chr, ChrBank);
        PrgRam = new byte[cartridge.PrgRamSize];
        _prgRamMask = System.Numerics.BitOperations.IsPow2(PrgRam.Length) ? PrgRam.Length - 1 : -1;
        Mirroring = cartridge.Mirroring;

        // Until a board says otherwise: the whole PRG from its start, the first CHR 8 KB.
        for (int window = 0; window < PrgBase.Length; window++)
        {
            PrgBase[window] = window * PrgWindow % Prg.Length;
        }

        for (int window = 0; window < ChrBase.Length; window++)
        {
            ChrBase[window] = window * ChrWindow;
        }
    }

    /// <summary>The PRG ROM, a whole number of banks long, so a window base plus an offset never leaves it.</summary>
    private protected byte[] Prg { get; }

    /// <summary>The CHR, ROM or RAM, a whole number of banks long, and never empty.</summary>
    private protected byte[] Chr { get; }

    /// <summary>Where in <see cref="Prg"/> each 8 KB window of <c>$8000</c> to <c>$FFFF</c> starts.</summary>
    private protected int[] PrgBase { get; } = new int[4];

    /// <summary>Where in <see cref="Chr"/> each 1 KB window of the pattern tables starts.</summary>
    private protected int[] ChrBase { get; } = new int[8];

    /// <summary>False while the board has switched its PRG RAM off: reads are open bus and writes go nowhere.</summary>
    private protected bool PrgRamEnabled { get; set; } = true;

    /// <summary>False while the board refuses writes to its PRG RAM and still lets it be read (MMC3's write protect).</summary>
    private protected bool PrgRamWritable { get; set; } = true;

    private Mirroring _mirroring;

    /// <inheritdoc />
    public Mirroring Mirroring
    {
        get => _mirroring;
        private protected set
        {
            _mirroring = value;
            NametablePages.Fill(NametablePageTable, value);
        }
    }

    /// <inheritdoc />
    public int[] NametablePageTable { get; } = new int[4];

    /// <inheritdoc />
    public virtual bool Irq => false;

    /// <inheritdoc />
    public byte[] PrgRam { get; }

    /// <inheritdoc />
    public byte CpuRead(ushort address, byte openBus)
    {
        if (address >= 0x8000)
        {
            return RomByte(address);
        }

        if (address >= 0x6000 && PrgRamEnabled && PrgRam.Length > 0)
        {
            return PrgRam[PrgRamIndex(address)];
        }

        return openBus;
    }

    /// <inheritdoc />
    public void CpuWrite(ushort address, byte value)
    {
        if (address >= 0x8000)
        {
            WriteRegister(address, value);
        }
        else if (address >= 0x6000 && PrgRamEnabled && PrgRamWritable && PrgRam.Length > 0)
        {
            PrgRam[PrgRamIndex(address)] = value;
        }
    }

    /// <inheritdoc />
    /// <remarks>Always true: <see cref="Chr"/> and <see cref="ChrBase"/> are the board's for good, and its banks move only the offsets.</remarks>
    public bool TryGetPatternWindows([NotNullWhen(true)] out byte[]? chr, [NotNullWhen(true)] out int[]? windows)
    {
        chr = Chr;
        windows = ChrBase;
        return true;
    }

    /// <inheritdoc />
    /// <remarks>Always true: <see cref="Prg"/> and <see cref="PrgBase"/> are the board's for good, and its banks move only the offsets.</remarks>
    public bool TryGetPrgWindows([NotNullWhen(true)] out byte[]? prg, [NotNullWhen(true)] out int[]? windows)
    {
        prg = Prg;
        windows = PrgBase;
        return true;
    }

    /// <inheritdoc />
    public byte PpuRead(ushort address)
    {
        return Chr[ChrBase[(address >> 10) & 7] + (address & (ChrWindow - 1))];
    }

    /// <inheritdoc />
    public void PpuWrite(ushort address, byte value)
    {
        if (_chrIsRam)
        {
            Chr[ChrBase[(address >> 10) & 7] + (address & (ChrWindow - 1))] = value;
        }
    }

    /// <inheritdoc />
    public virtual void PpuAddressChanged(ushort address, long cpuCycle)
    {
    }

    /// <inheritdoc />
    public virtual void CpuCycle()
    {
    }

    /// <inheritdoc />
    /// <remarks>False here: a board that watches overrides <see cref="PpuAddressChanged"/> and this.</remarks>
    public virtual bool WatchesPpuAddresses => false;

    /// <inheritdoc />
    /// <remarks>False here: a board that counts overrides <see cref="CpuCycle"/> and this.</remarks>
    public virtual bool CountsCpuCycles => false;

    /// <inheritdoc />
    /// <remarks>False here: a board with an IRQ overrides <see cref="Irq"/> and this.</remarks>
    public virtual bool CanInterrupt => false;

    /// <inheritdoc />
    public virtual void Reset(bool power)
    {
        if (power && _chrIsRam)
        {
            Array.Clear(Chr);
        }
    }

    /// <inheritdoc />
    public void ClearPrgRam()
    {
        Array.Clear(PrgRam);
    }

    /// <summary>A CPU write at <c>$8000</c> to <c>$FFFF</c>: the board's register.</summary>
    private protected abstract void WriteRegister(ushort address, byte value);

    /// <summary>The byte the PRG ROM holds at a CPU address of <c>$8000</c> or more: what a bus conflict ANDs with.</summary>
    private protected byte RomByte(ushort address)
    {
        return Prg[PrgBase[(address >> 13) & 3] + (address & (PrgWindow - 1))];
    }

    /// <summary>
    /// Points a 16 KB half of the CPU space (0 for <c>$8000</c>, 1 for <c>$C000</c>) at a bank of
    /// <see cref="Prg"/>, taken modulo the banks there are.
    /// </summary>
    private protected void SetPrg16(int half, int bank)
    {
        SetPrg(half * 2, Wrap(bank, Prg.Length / 0x4000) * 0x4000, 2);
    }

    /// <summary>Points the whole CPU space at a bank of 32 KB, taken modulo the banks there are.</summary>
    private protected void SetPrg32(int bank)
    {
        SetPrg(0, Wrap(bank, Prg.Length / 0x8000) * 0x8000, 4);
    }

    /// <summary>
    /// Points an 8 KB window of the CPU space (0 for <c>$8000</c> to 3 for <c>$E000</c>) at an 8 KB
    /// bank of <see cref="Prg"/>, taken modulo the banks there are; a negative bank counts back
    /// from the end, so -1 is the last.
    /// </summary>
    private protected void SetPrg8(int window, int bank)
    {
        PrgBase[window] = Wrap(bank, Prg8Count) * PrgWindow;
    }

    /// <summary>Points the pattern tables at an 8 KB bank of <see cref="Chr"/>, taken modulo the banks there are.</summary>
    private protected void SetChr8(int bank)
    {
        SetChr(0, Wrap(bank, Chr.Length / ChrBank) * ChrBank, 8);
    }

    /// <summary>Points a 4 KB half of the pattern tables (0 for <c>$0000</c>, 1 for <c>$1000</c>) at a 4 KB bank of <see cref="Chr"/>, taken modulo the banks there are.</summary>
    private protected void SetChr4(int half, int bank)
    {
        SetChr(half * 4, Wrap(bank, Chr.Length / 0x1000) * 0x1000, 4);
    }

    /// <summary>Points a 1 KB window of the pattern tables (0 for <c>$0000</c> to 7 for <c>$1C00</c>) at a 1 KB bank of <see cref="Chr"/>, taken modulo the banks there are.</summary>
    private protected void SetChr1(int window, int bank)
    {
        ChrBase[window] = Wrap(bank, Chr.Length / ChrWindow) * ChrWindow;
    }

    /// <summary>The number of 16 KB banks in the PRG.</summary>
    private protected int Prg16Count => Prg.Length / 0x4000;

    /// <summary>The number of 8 KB banks in the PRG.</summary>
    private protected int Prg8Count => Prg.Length / PrgWindow;

    /// <summary>
    /// <paramref name="data"/> as a whole number of <paramref name="unit"/> byte banks: the same
    /// array when it already is one, otherwise a copy padded by repeating the data (the way an
    /// address wraps on a smaller chip), and one zeroed bank when there is no data.
    /// </summary>
    private protected static byte[] Fit(byte[] data, int unit)
    {
        if (data.Length == 0)
        {
            return new byte[unit];
        }

        if (data.Length % unit == 0)
        {
            return data;
        }

        byte[] fitted = new byte[RoundUp(data.Length, unit)];
        for (int i = 0; i < fitted.Length; i++)
        {
            fitted[i] = data[i % data.Length];
        }

        return fitted;
    }

    private static int RoundUp(int size, int unit) => (size + unit - 1) / unit * unit;

    // The bank modulo the count, never negative.
    private static int Wrap(int bank, int count) => ((bank % count) + count) % count;

    private void SetPrg(int firstWindow, int offset, int windows)
    {
        for (int i = 0; i < windows; i++)
        {
            PrgBase[firstWindow + i] = offset + (i * PrgWindow);
        }
    }

    private void SetChr(int firstWindow, int offset, int windows)
    {
        for (int i = 0; i < windows; i++)
        {
            ChrBase[firstWindow + i] = offset + (i * ChrWindow);
        }
    }

    /// <inheritdoc />
    void IReportsState.ReportState(IStateSink sink)
    {
        ReportState(sink);
    }

    /// <summary>
    /// The board's state for <see cref="IReportsState"/>: what every board shares, then, in a
    /// board that overrides this, its own registers. The PRG ROM never changes, and neither does
    /// CHR ROM; CHR RAM is reported.
    /// </summary>
    private protected virtual void ReportState(IStateSink sink)
    {
        sink.Skip(nameof(_prgRamMask), StateReport.Fixed);
        sink.Skip(nameof(Prg), "ROM");
        if (_chrIsRam)
        {
            sink.Add(nameof(Chr), Chr);
        }
        else
        {
            sink.Skip(nameof(Chr), "ROM");
        }

        sink.Skip(nameof(_chrIsRam), StateReport.Fixed);
        sink.Add(nameof(PrgBase), PrgBase);
        sink.Add(nameof(ChrBase), ChrBase);
        sink.Add(nameof(PrgRamEnabled), PrgRamEnabled);
        sink.Add(nameof(PrgRamWritable), PrgRamWritable);
        sink.Add(nameof(_mirroring), (int)_mirroring);
        sink.Add(nameof(NametablePageTable), NametablePageTable);
        sink.Add(nameof(PrgRam), PrgRam);
    }

    // Where in PrgRam an address from $6000 falls; RAM smaller than the 8 KB window repeats in it.
    private int PrgRamIndex(ushort address)
    {
        int offset = address - 0x6000;
        return _prgRamMask >= 0 ? offset & _prgRamMask : offset % PrgRam.Length;
    }
}
