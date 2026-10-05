namespace Dbhq.Machines.Nes;

/// <summary>What a caller may choose when it builds a machine.</summary>
public sealed class NesOptions
{
    /// <summary>The sound's sample rate in hertz, for the sample buffer the sound unit fills (task 9).</summary>
    public int SampleRate { get; init; } = 48_000;
}
