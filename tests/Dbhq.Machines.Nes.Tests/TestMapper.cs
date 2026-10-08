using System.Diagnostics.CodeAnalysis;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// A board for testing the PPU alone: 8 KB of pattern-table RAM, a mirroring the test sets, and a
/// record of every address the PPU reports on its bus.
/// </summary>
/// <remarks>
/// By default it is a board that implements <see cref="IMapper"/> and says nothing more, so the
/// PPU tells it every address and reads its pattern tables through <see cref="PpuRead"/>. With
/// <see cref="Quiet"/> and <see cref="Windowed"/> it is a board as the real ones here are: it does
/// not watch the PPU's address bus, and it gives its pattern RAM as windows and its nametable
/// layout as a page table, so the PPU reads both without a call. Then a catch-up may render whole
/// lines at once (the fast scanline renderer, <c>PpuScanline.cs</c>), which it never does for a
/// board that watches, and on a line that fetches tiles only from a board with windows.
/// </remarks>
public sealed class TestMapper : IMapper
{
    private static readonly int[] FlatWindows = [0x0000, 0x0400, 0x0800, 0x0C00, 0x1000, 0x1400, 0x1800, 0x1C00];
    private readonly int[] _pages = new int[4];
    private Mirroring _mirroring = Mirroring.Horizontal;

    public TestMapper()
    {
        NametablePages.Fill(_pages, _mirroring);
    }

    public byte[] Chr { get; } = new byte[0x2000];

    public List<(ushort Address, long CpuCycle)> Reported { get; } = [];

    /// <summary>A board that does not watch the PPU's address bus (see the remarks).</summary>
    public bool Quiet { get; init; }

    /// <summary>A board that gives its pattern RAM as windows and its nametable layout as a page table (see the remarks).</summary>
    public bool Windowed { get; init; }

    public Mirroring Mirroring
    {
        get => _mirroring;
        set
        {
            _mirroring = value;
            NametablePages.Fill(_pages, value);
        }
    }

    public bool Irq => false;

    public byte[] PrgRam { get; } = [];

    bool IMapper.WatchesPpuAddresses => !Quiet;

    int[]? IMapper.NametablePageTable => Windowed ? _pages : null;

    bool IMapper.TryGetPatternWindows([NotNullWhen(true)] out byte[]? chr, [NotNullWhen(true)] out int[]? windows)
    {
        chr = Windowed ? Chr : null;
        windows = Windowed ? FlatWindows : null;
        return Windowed;
    }

    public byte CpuRead(ushort address, byte openBus) => openBus;

    public void CpuWrite(ushort address, byte value)
    {
    }

    public byte PpuRead(ushort address) => Chr[address & 0x1FFF];

    public void PpuWrite(ushort address, byte value) => Chr[address & 0x1FFF] = value;

    public void PpuAddressChanged(ushort address, long cpuCycle) => Reported.Add((address, cpuCycle));

    public void CpuCycle()
    {
    }

    public void Reset(bool power)
    {
    }

    public void ClearPrgRam()
    {
    }
}
