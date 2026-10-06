namespace Dbhq.Machines.Nes;

/// <summary>What a caller may choose when it builds a machine.</summary>
public sealed class NesOptions
{
    /// <summary>
    /// The sound's sample rate in hertz: <see cref="Nes.Sound"/> makes samples at this rate. It must
    /// be above 0 and no more than an eighth of the region's CPU clock (about 200 kHz); the
    /// machine's constructor throws <see cref="ArgumentOutOfRangeException"/> otherwise.
    /// </summary>
    public int SampleRate { get; init; } = 48_000;
}
