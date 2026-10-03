namespace Dbhq.Machines.BbcMicro;

/// <summary>What the person at the keyboard has set before switching on.</summary>
public sealed class BbcOptions
{
    /// <summary>
    /// The screen mode the machine starts in, 0 to 7. The Model B reads it from
    /// the start-up links beside the keyboard; the default is mode 7, the
    /// teletext screen.
    /// </summary>
    public int StartupMode { get; init; } = 7;
}
