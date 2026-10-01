using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// A KIM-1 with the real monitor ROM, driven the way a person drives one:
/// a key is held long enough for the monitor to see it, then let go.
/// </summary>
public sealed class Kim1Session
{
    /// <summary>How long a key is held, and then left up: 40 ms each at 1 MHz.</summary>
    public const long HoldCycles = Kim1Keystrokes.HoldCycles;

    public Kim1Session()
    {
        Machine = new Kim1Machine(Rom002, Rom003);
        Keystrokes = new Kim1Keystrokes(Machine);
    }

    /// <summary>The 6530-002's 1 KB, pinned and hash-checked, never committed.</summary>
    public static byte[] Rom002 => File.ReadAllBytes(PinnedFiles.Fetch(Pins.Kim1Rom002Url, Path.Combine("kim-1", "6530-002.bin"), PinnedFiles.Sha256(Pins.Kim1Rom002Sha256)));

    /// <summary>The 6530-003's 1 KB, likewise.</summary>
    public static byte[] Rom003 => File.ReadAllBytes(PinnedFiles.Fetch(Pins.Kim1Rom003Url, Path.Combine("kim-1", "6530-003.bin"), PinnedFiles.Sha256(Pins.Kim1Rom003Sha256)));

    public Kim1Machine Machine { get; }

    public Kim1Keystrokes Keystrokes { get; }

    /// <summary>The display as the User Manual prints it: four address digits, a space, two data digits.</summary>
    public string Display
    {
        get
        {
            string digits = Machine.ReadDisplay();
            return $"{digits[..4]} {digits[4..]}";
        }
    }

    /// <summary>Presses RS and lets the monitor run for 100 ms.</summary>
    public Kim1Session Boot()
    {
        Machine.PressReset();
        Machine.Run(100_000);
        return this;
    }

    /// <summary>
    /// Presses keys in turn, written as the User Manual writes them: [AD],
    /// [DA], [+], [GO], [PC], [ST] and [RS] in brackets, and a run of hex
    /// digits one key per character. "[AD] 0002 [DA] 18" is eight presses.
    /// They go through <see cref="Kim1Keystrokes"/>, the same player the
    /// browser page uses: each key held 40 ms, then left up 40 ms.
    /// </summary>
    public Kim1Session Keys(string keys)
    {
        Keystrokes.Type(keys);
        Keystrokes.RunUntilIdle();
        return this;
    }

    public void Press(Kim1Key key) => Keys(key switch
    {
        Kim1Key.Address => "[AD]",
        Kim1Key.Data => "[DA]",
        Kim1Key.Plus => "[+]",
        Kim1Key.Go => "[GO]",
        Kim1Key.ProgramCounter => "[PC]",
        _ => ((int)key).ToString("X"),
    });

    /// <summary>Holds ST for 40 ms and lets go, as a finger does.</summary>
    public void Stop() => Keys("[ST]");

    /// <summary>
    /// True when <paramref name="shown"/> matches <paramref name="expected"/>.
    /// An x in the expectation is the manual's "don't care": any lit hex
    /// digit, but not a dark one.
    /// </summary>
    public static bool Matches(string expected, string shown) =>
        expected.Length == shown.Length && expected.Zip(shown).All(p => p.First == 'x' ? Uri.IsHexDigit(p.Second) : p.First == p.Second);
}
