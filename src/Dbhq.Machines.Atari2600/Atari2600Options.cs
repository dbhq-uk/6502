namespace Dbhq.Machines.Atari2600;

/// <summary>What a machine is built with, beyond its cartridge and region.</summary>
public sealed class Atari2600Options
{
    /// <summary>The host's sample rate, in samples a second. The sound arrives in task 10; until then it is kept and not used.</summary>
    public int SampleRate { get; init; } = 48_000;

    /// <summary>
    /// The chips advanced inside every call, a colour clock and a RIOT cycle at a time: the
    /// reference a lazy build is checked against (AGENTS.md rule 1; spec, "Speed"). In task 4 every
    /// build is that reference, because nothing is lazy yet, so the option changes nothing; the
    /// speed pass (issue #130) keeps this path when it makes the default lazy.
    /// </summary>
    public bool PerColourClockReference { get; init; }
}
