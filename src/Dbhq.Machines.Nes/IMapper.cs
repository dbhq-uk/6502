namespace Dbhq.Machines.Nes;

/// <summary>
/// The cartridge's board: what answers the CPU at <c>$4020</c> to <c>$FFFF</c> and the PPU at
/// <c>$0000</c> to <c>$1FFF</c>, how it connects the nametables, and whether it holds the IRQ line.
/// One class a board, in <c>Mappers/</c>, built from <c>docs/nes/facts/mappers.md</c>.
/// </summary>
public interface IMapper
{
    /// <summary>
    /// What the cartridge puts on the CPU's data bus for a read at <paramref name="address"/>
    /// (<c>$4020</c> to <c>$FFFF</c>). Where the board drives nothing it returns
    /// <paramref name="openBus"/>, the value the bus last held, so a read of nothing keeps it.
    /// </summary>
    byte CpuRead(ushort address, byte openBus);

    /// <summary>A CPU write at <paramref name="address"/>: a register, PRG RAM, or nothing.</summary>
    void CpuWrite(ushort address, byte value);

    /// <summary>A PPU read of a pattern table (<c>$0000</c> to <c>$1FFF</c>).</summary>
    byte PpuRead(ushort address);

    /// <summary>A PPU write to a pattern table, which only a board with CHR RAM takes.</summary>
    void PpuWrite(ushort address, byte value);

    /// <summary>How the board connects the nametables now. A board that does not choose gives the header's.</summary>
    Mirroring Mirroring { get; }

    /// <summary>True while the board holds the IRQ line low.</summary>
    bool Irq { get; }

    /// <summary>
    /// Told of every pattern-table address the PPU puts on its bus, with the CPU cycle it did so
    /// in, which is all MMC3's scanline counter needs to watch address line A12.
    /// </summary>
    void PpuAddressChanged(ushort address, long cpuCycle);

    /// <summary>Called once a CPU cycle, for a board that must tell one cycle from the next (MMC1 ignores a write the cycle after a write).</summary>
    void CpuCycle();

    /// <summary>The board's PRG RAM, battery backed or not, or an empty array when it has none.</summary>
    byte[] PrgRam { get; }

    /// <summary>Clears the PRG RAM, as a power cycle does. A reset leaves it alone.</summary>
    void ClearPrgRam();
}
