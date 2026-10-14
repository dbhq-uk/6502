namespace Dbhq.Machines.Atari2600;

/// <summary>
/// Where a part puts its whole state when it is asked (<see cref="IReportsState.Report"/>): each
/// field by its name, in a fixed order. Built in task 4 for the speed pass (issue #130), whose
/// differential will hash it at each point where a chip can be seen (<see cref="IAtari2600Observer"/>),
/// as the NES's lazy chips needed and had to add afterwards (spec, "Decisions made while writing
/// this"). A test compares the names with the fields each class declares, so a field that is
/// neither reported nor skipped is found.
/// </summary>
/// <remarks>Nothing here changes a part. A report reads the fields as they are.</remarks>
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
    void Report(IStateSink sink);
}

/// <summary>
/// Told by the bus of each point where a chip can be seen from outside it, which is each point
/// where a build that advances the chips lazily must have caught them up (spec, "Speed"): the
/// TIA is seen at the CPU's accesses to it, at the end of a WSYNC halt and at a frame's end; the
/// RIOT's timer and ports at the CPU's accesses to its I/O half; and a board at a bank switch. For
/// the speed pass's differential and the tests; see <see cref="Atari2600Bus.Observable"/>.
/// </summary>
internal interface IAtari2600Observer
{
    /// <summary>
    /// A CPU access, made: each access to the TIA or to the RIOT's I/O and timer half, and each
    /// access to the cartridge that switched its bank. <paramref name="address"/> is the 13-bit
    /// address the bus keeps; a read's <paramref name="value"/> is the byte the CPU got. Called after
    /// the access, inside its cycle.
    /// </summary>
    void Accessed(ushort address, bool write, byte value);

    /// <summary>
    /// A WSYNC halt is over: the TIA has reached the line's start, and the CPU's held read runs as
    /// the next cycle. <paramref name="cycle"/> is the cycle count at that point, so a halt after a
    /// write on a line's last cycle, which waits no cycle at all, is told too.
    /// </summary>
    void HaltReleased(long cycle);

    /// <summary>
    /// A frame has ended, at the end of VSYNC or at the line limit; <paramref name="cycle"/> is the
    /// cycle in which it ended, counted.
    /// </summary>
    void FrameEnded(long cycle);
}

/// <summary>The reasons a state report gives most often.</summary>
internal static class StateReport
{
    /// <summary>The field is set when the object is made and never changes.</summary>
    internal const string Fixed = "fixed when the object is made";

    /// <summary>The cartridge's ROM, which never changes.</summary>
    internal const string Image = "the cartridge's image, which never changes";
}
