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
    /// <remarks>
    /// A read must be pure: it changes nothing in the board. <c>NesBus.Peek</c> calls it to show
    /// memory with no cycle and no side effect, and a read that moved a register would change the
    /// machine each time a debugger looked. No board this machine models needs one that does; a
    /// board that does would need a separate peek method.
    /// </remarks>
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

    /// <summary>
    /// Called once a CPU cycle, before that cycle's access, for a board that must tell one cycle from
    /// the next (MMC1 ignores a write the cycle after a write). It runs for every cycle, so it must
    /// be trivial and allocate nothing.
    /// </summary>
    void CpuCycle();

    /// <summary>
    /// Puts the board's registers where a switch-on or the reset button leaves them. With
    /// <paramref name="power"/> true (the console is switched on) every register goes to its
    /// power-on value, and so does CHR RAM, which a real board's RAM loses with the power. With
    /// <paramref name="power"/> false (the reset button) the board does what the real chips do, which
    /// for the boards here is nothing: their registers have no reset line. Neither clears PRG RAM;
    /// <see cref="ClearPrgRam"/> does, and only a power cycle calls it.
    /// </summary>
    void Reset(bool power);

    /// <summary>The board's PRG RAM, battery backed or not, or an empty array when it has none.</summary>
    byte[] PrgRam { get; }

    /// <summary>Clears the PRG RAM, as a power cycle does. A reset leaves it alone.</summary>
    void ClearPrgRam();
}
