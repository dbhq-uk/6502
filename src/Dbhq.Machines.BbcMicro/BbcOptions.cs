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

    /// <summary>
    /// Samples a second in <see cref="BbcMachine.Sound"/>, 1 to 250,000. The default is 48,000; a
    /// page sets its audio context's rate, so the browser does not resample a second time.
    /// </summary>
    public int SampleRate { get; init; } = 48_000;
}
