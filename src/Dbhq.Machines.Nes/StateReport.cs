namespace Dbhq.Machines.Nes;

/// <summary>
/// Where a chip puts its whole state when it is asked (<see cref="IReportsState.ReportState"/>):
/// each field by its name, in a fixed order. The differential in
/// <c>bench/nes-speed/differential</c> hashes what it is given, and a test compares the names with
/// the fields the class declares, so a field that is neither reported nor skipped is found.
/// </summary>
/// <remarks>
/// Nothing here changes a chip. A report reads the fields as they are, so in a build whose chips
/// are caught up on demand it says what the chip's state has been computed to, which at an
/// observation point (<see cref="INesObserver"/>) is the logical position.
/// </remarks>
internal interface IStateSink
{
    void Add(string name, long value);

    void Add(string name, ulong value);

    void Add(string name, bool value);

    void Add(string name, double value);

    void Add(string name, ReadOnlySpan<byte> values);

    void Add(string name, ReadOnlySpan<int> values);

    void Add(string name, ReadOnlySpan<uint> values);

    void Add(string name, ReadOnlySpan<long> values);

    void Add(string name, ReadOnlySpan<float> values);

    void Add(string name, ReadOnlySpan<double> values);

    /// <summary>A field that holds another part with state of its own, which reports itself.</summary>
    void Add(string name, IReportsState part);

    /// <summary>A field that is not state, with the reason: fixed when the object is made, or kept elsewhere.</summary>
    void Skip(string name, string why);
}

/// <summary>A part of the machine that can report its whole state to an <see cref="IStateSink"/>.</summary>
internal interface IReportsState
{
    /// <summary>Every field, by name: its value, or <see cref="IStateSink.Skip"/> with why it is not state. Reads only.</summary>
    void ReportState(IStateSink sink);
}

/// <summary>
/// Told by the bus of each point where a chip can be seen from outside it, which is each point
/// where a build that advances the chips lazily must have caught them up (the lazy chips design,
/// <c>docs/superpowers/specs/2026-10-06-nes-lazy-chips-design.md</c>). For the differential and
/// the tests; see <see cref="NesBus.Observable"/>.
/// </summary>
internal interface INesObserver
{
    /// <summary>
    /// A CPU access in the cycle now running, made: any access to <c>$2000</c> to <c>$401F</c>
    /// (the PPU's registers, the sound unit's, OAM DMA's and the pads'), OAM DMA's writes to
    /// <c>$2004</c> among them, and any write to the board's registers at <c>$8000</c> to
    /// <c>$FFFF</c>, which can move its pattern banks or its nametable layout under the PPU. Called
    /// after the access (a read's value is <paramref name="value"/>) and before the cycle's dots that
    /// follow it.
    /// </summary>
    void Accessed(ushort address, bool write, byte value);

    /// <summary>The DMC's DMA has read its sample byte and handed it to the channel.</summary>
    void DmcFetched();

    /// <summary>
    /// The PPU has finished a frame: called from inside the dot that ends it, with the PPU at line 0
    /// dot 0, in the cycle that delivers that dot. The PPU runs its dots when it is caught up, so
    /// when the frame's last dot is one of the two before a cycle's access and the access is not
    /// one that catches the PPU up (not a PPU register, not a write to the cartridge), the frame
    /// ends at the end of the cycle, after that cycle's <see cref="Accessed"/> call; the per-dot
    /// reference calls it before. Either way the PPU's state and the bus's counts it sees are the
    /// same.
    /// </summary>
    void FrameEnded();

    /// <summary>A cycle is over and the CPU has been given these lines for the next one.</summary>
    void CycleEnded(bool nmi, bool irq);

    /// <summary>
    /// Power on (<paramref name="power"/> true) or the reset button has put the chips where it
    /// leaves them, before the CPU's reset sequence runs its cycles.
    /// </summary>
    void ChipsReset(bool power);
}

/// <summary>The reasons a state report gives most often.</summary>
internal static class StateReport
{
    /// <summary>The field is set when the object is made and never changes.</summary>
    internal const string Fixed = "fixed when the object is made";
}
