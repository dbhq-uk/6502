namespace Dbhq.Machines.Nes;

/// <summary>
/// The chips on the NES's board that the CPU's accesses can be told apart by address, for the
/// inside model's marks (<c>docs/nes/facts/models.md</c>, "What the counters count").
/// </summary>
public enum NesChip
{
    /// <summary>The PPU's registers, <c>$2000-$3FFF</c>; OAM DMA's writes to <c>$2004</c> included.</summary>
    Ppu,

    /// <summary>The sound and I/O registers on the CPU's own die: <c>$4000-$4015</c>, and the writes to <c>$4016</c> and <c>$4017</c>.</summary>
    Apu,

    /// <summary>Reads of <c>$4016</c>, through the 74HC368 printed "40H368(CI)".</summary>
    Pad1,

    /// <summary>Reads of <c>$4017</c>, through the 74HC368 printed "40H368(CII)".</summary>
    Pad2,
}

/// <summary>
/// Counts the CPU's reads and writes of each <see cref="NesChip"/>, so the page can show which
/// chips the processor talks to. The bus calls <see cref="Note"/> on every access it decodes to
/// <c>$2000-$401F</c> and on no other, and never for a peek. Nothing here changes what any chip
/// does or adds a cycle: it only counts.
/// </summary>
/// <remarks>
/// The counters wrap, so a reader takes the difference between two snapshots, which stays right
/// across a wrap as long as fewer than 2^32 accesses fall between them.
/// </remarks>
public sealed class ChipAccesses
{
    private readonly uint[] _counts = new uint[Enum.GetValues<NesChip>().Length];

    /// <summary>The chip an access to <paramref name="address"/> reaches, or null for none of them.</summary>
    public static NesChip? ChipAt(ushort address, bool write) => address switch
    {
        >= 0x2000 and <= 0x3FFF => NesChip.Ppu,
        >= 0x4000 and <= 0x4015 => NesChip.Apu,
        0x4016 => write ? NesChip.Apu : NesChip.Pad1,
        0x4017 => write ? NesChip.Apu : NesChip.Pad2,
        _ => null,
    };

    /// <summary>Counts one access by the CPU, if it reaches a counted chip.</summary>
    public void Note(ushort address, bool write)
    {
        if (ChipAt(address, write) is { } chip)
        {
            unchecked { _counts[(int)chip]++; }
        }
    }

    /// <summary>The accesses to <paramref name="chip"/> since power on, wrapping.</summary>
    public uint this[NesChip chip] => _counts[(int)chip];

    /// <summary>Every counter, in <see cref="NesChip"/> order, as the host hands them to the page.</summary>
    public int[] Snapshot() => Array.ConvertAll(_counts, c => unchecked((int)c));
}
