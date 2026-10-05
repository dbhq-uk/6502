namespace Dbhq.Machines.Electron;

/// <summary>What the person at the keyboard has set before switching on.</summary>
public sealed class ElectronOptions
{
    /// <summary>
    /// Samples a second in the machine's sound output. The default is 44,100.
    /// </summary>
    public int SampleRate { get; init; } = 44_100;

    /// <summary>
    /// The paged ROM slots BASIC appears in: 10 and 11 on a stock machine, one chip in both
    /// (<c>ula.md</c> s2b). For tests only: the boot tests take BASIC out, or leave it in one slot,
    /// to show that the OS's ROM scan is what starts it (s2c).
    /// </summary>
    internal IReadOnlyCollection<int> BasicSlots { get; init; } = [10, 11];
}
