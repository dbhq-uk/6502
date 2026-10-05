namespace Dbhq.Machines.Electron;

/// <summary>What the person at the keyboard has set before switching on.</summary>
public sealed class ElectronOptions
{
    /// <summary>
    /// Samples a second in the machine's sound output. The default is 44,100.
    /// </summary>
    public int SampleRate { get; init; } = 44_100;
}
