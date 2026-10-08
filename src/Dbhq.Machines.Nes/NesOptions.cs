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

    /// <summary>
    /// True to advance the PPU and the sound unit inside every bus call, dot by dot and cycle by
    /// cycle, even in a build that otherwise brings them up to date only when something can see
    /// them: the per-dot reference ("oracle") that the lazy build is checked against, kept for the
    /// tests and the benches, and slower. False by default. In this build it is the PPU that is
    /// lazy (caught up where it can be seen); the sound unit is still ticked every cycle.
    /// </summary>
    public bool PerDotReference { get; init; }
}
