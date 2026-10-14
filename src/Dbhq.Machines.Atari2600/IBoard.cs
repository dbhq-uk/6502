namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The cartridge's board: what answers the CPU at <c>$1000</c> to <c>$1FFF</c>, how it switches
/// banks, and the RAM some boards carry. One class a board, in <c>Boards/</c>, built from
/// <c>docs/atari-2600/facts/cartridge.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// The slot has no read/write line, so a board sees an address and the data bus and nothing else
/// (cartridge.md section 3). It acts on every access, a read as much as a write and a dummy read as
/// much as a real one, which is why every bus access must be the CPU's own (AGENTS.md rule 1).
/// </para>
/// <para>
/// The 6507's thirteen lines are the bus's business, not the board's: the bus keeps A0 to A12 and
/// gives the board only accesses with A12 high. A board decodes A0 to A11.
/// </para>
/// </remarks>
public interface IBoard
{
    /// <summary>
    /// One access the CPU makes to the cartridge, read or write alike. <paramref name="address"/>
    /// is the 13-bit address, A12 set; <paramref name="dataBus"/> is the byte on the bus this cycle:
    /// the value written, or for a read the byte left on the bus from the last cycle. Returns the
    /// byte the board drives on a read; where it drives nothing (a write, or a read of a RAM write
    /// port) it returns <paramref name="dataBus"/>.
    /// </summary>
    byte Access(ushort address, bool write, byte dataBus);

    /// <summary>
    /// What a read of <paramref name="address"/> would return now, with no side effect: no bank
    /// switches and no RAM cell is written. A hotspot shows the byte its read would return. A RAM
    /// write port drives nothing on a read, so there is no byte to show; it shows the cell, which
    /// is what a debugger wants to see. For the tests and for the bus's <c>Peek</c>.
    /// </summary>
    byte Peek(ushort address);

    /// <summary>The bank showing at <c>$1000</c>: 0 for a plain board.</summary>
    int Bank { get; }

    /// <summary>
    /// Puts the board as it is at power on: the last bank (cartridge.md section 8; a choice, since
    /// the hardware's is undefined) and the RAM cleared to zero (section 6; a choice, since no
    /// source states it). Both are in <c>docs/known-differences.md</c>.
    /// </summary>
    void PowerOn();
}
