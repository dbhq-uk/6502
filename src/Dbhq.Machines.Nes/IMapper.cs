using System.Diagnostics.CodeAnalysis;

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
    /// Told of the addresses the PPU puts on its bus (<c>$0000</c> to <c>$3FFF</c>), with the CPU
    /// cycle it did so in, which is all MMC3's scanline counter needs to watch address line A12:
    /// every pattern fetch and each sprite slot's first nametable fetch while rendering, and
    /// <c>v</c> and the <c>$2007</c> accesses outside rendering, whatever the address. It runs on the
    /// PPU's busiest path, so it must be trivial and allocate nothing.
    /// </summary>
    void PpuAddressChanged(ushort address, long cpuCycle);

    /// <summary>
    /// Called once a CPU cycle, before that cycle's access, for a board that must tell one cycle from
    /// the next (MMC1 ignores a write the cycle after a write). It runs for every cycle, so it must
    /// be trivial and allocate nothing. The PPU's dots of the cycle, the two before the access too,
    /// run when the PPU is caught up, so for a board that does not watch the PPU's address bus they
    /// may run after this call and after the access. So a board whose call here changes anything
    /// the PPU reads (its pattern banks, its nametable layout) must say
    /// <see cref="WatchesPpuAddresses"/>, which keeps the PPU's dots in their place, or the bus must
    /// catch the PPU up before the call.
    /// </summary>
    void CpuCycle();

    /// <summary>
    /// True when the board needs <see cref="PpuAddressChanged"/>: false lets the PPU skip the call,
    /// about 90 a line while it renders. It must not change after the board is made. A board that
    /// implements this interface itself and does not say is told, so for it leaving this out is
    /// never wrong, only slower. <see cref="Mappers.Board"/> says false for all three of these
    /// flags, so a board built on it that overrides <see cref="PpuAddressChanged"/>,
    /// <see cref="CpuCycle"/> or <see cref="Irq"/> must override the matching flag too, or the
    /// call is skipped; a test checks every board for it. It also keeps the PPU exact for the
    /// board: the bus catches the PPU up every cycle for a board that watches, so each address
    /// reaches it in the cycle it is put out, where for the others the PPU runs its dots only when
    /// something can see it. So a board whose PPU-side calls change anything the CPU can see, an
    /// IRQ counted from the PPU's fetches for one, must say true.
    /// </summary>
    bool WatchesPpuAddresses => true;

    /// <summary>
    /// True when the board needs <see cref="CpuCycle"/>: false lets the bus skip the call each
    /// cycle. It must not change after the board is made. A board that implements this interface
    /// itself and does not say is called; a <see cref="Mappers.Board"/> must say (see
    /// <see cref="WatchesPpuAddresses"/>).
    /// </summary>
    bool CountsCpuCycles => true;

    /// <summary>
    /// True when the board can ever hold the IRQ line: false lets the bus skip reading
    /// <see cref="Irq"/> each cycle. It must not change after the board is made. A board that
    /// implements this interface itself and does not say is read; a <see cref="Mappers.Board"/>
    /// must say (see <see cref="WatchesPpuAddresses"/>).
    /// </summary>
    bool CanInterrupt => true;

    /// <summary>
    /// The board's pattern tables as memory the PPU may read without a call, when the board keeps
    /// them so: eight 1 KB windows into <paramref name="chr"/>, starting at the offsets in
    /// <paramref name="windows"/>, so that a read at <c>a</c> is
    /// <c>chr[windows[(a &gt;&gt; 10) &amp; 7] + (a &amp; 0x3FF)]</c>, exactly what
    /// <see cref="PpuRead"/> returns. The board must keep both arrays for good and change only the
    /// window offsets, in place, when it switches banks. A board that does not keep its pattern
    /// tables that way returns false, and the PPU calls <see cref="PpuRead"/>.
    /// </summary>
    bool TryGetPatternWindows([NotNullWhen(true)] out byte[]? chr, [NotNullWhen(true)] out int[]? windows)
    {
        chr = null;
        windows = null;
        return false;
    }

    /// <summary>
    /// The board's PRG ROM as memory the bus may read without a call, when the board keeps it so:
    /// four 8 KB windows into <paramref name="prg"/>, for <c>$8000</c>, <c>$A000</c>, <c>$C000</c>
    /// and <c>$E000</c>, starting at the offsets in <paramref name="windows"/>, so that a CPU read
    /// at <c>a</c> from <c>$8000</c> up is <c>prg[windows[(a &gt;&gt; 13) &amp; 3] + (a &amp; 0x1FFF)]</c>,
    /// exactly what <see cref="CpuRead"/> returns there. The board must keep both arrays for good
    /// and change only the offsets, in place, when it switches banks. A board that does not keep
    /// its PRG that way returns false, and the bus calls <see cref="CpuRead"/>.
    /// </summary>
    bool TryGetPrgWindows([NotNullWhen(true)] out byte[]? prg, [NotNullWhen(true)] out int[]? windows)
    {
        prg = null;
        windows = null;
        return false;
    }

    /// <summary>
    /// Four entries, the page of nametable RAM each nametable uses under <see cref="Mirroring"/>
    /// now (<see cref="NametablePages"/>), which the board keeps for good and fills again in place
    /// whenever its mirroring changes, so the PPU reads it without a call; or null, and the PPU
    /// asks <see cref="Mirroring"/> on each nametable fetch.
    /// </summary>
    int[]? NametablePageTable => null;

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
