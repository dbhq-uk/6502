using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// A KIM-1 with the real monitor ROM, driven the way a person drives one:
/// a key is held long enough for the monitor to see it, then let go.
/// </summary>
public sealed class Kim1Session
{
    /// <summary>How long a key is held, and then left up: 40 ms each at 1 MHz.</summary>
    public const long HoldCycles = 40_000;

    public Kim1Session()
    {
        Machine = new Kim1Machine(Rom002, Rom003);
    }

    /// <summary>The 6530-002's 1 KB, pinned and hash-checked, never committed.</summary>
    public static byte[] Rom002 => File.ReadAllBytes(PinnedFiles.Fetch(Pins.Kim1Rom002Url, Path.Combine("kim-1", "6530-002.bin"), PinnedFiles.Sha256(Pins.Kim1Rom002Sha256)));

    /// <summary>The 6530-003's 1 KB, likewise.</summary>
    public static byte[] Rom003 => File.ReadAllBytes(PinnedFiles.Fetch(Pins.Kim1Rom003Url, Path.Combine("kim-1", "6530-003.bin"), PinnedFiles.Sha256(Pins.Kim1Rom003Sha256)));

    public Kim1Machine Machine { get; }

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
    /// [DA], [+], [GO] and [PC] in brackets, and a run of hex digits one key
    /// per character. "[AD] 0002 [DA] 18" is eight presses. The brackets are
    /// needed because AD and DA are also bytes: $AD is LDA absolute.
    /// </summary>
    public Kim1Session Keys(string keys)
    {
        foreach (string word in keys.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (word)
            {
                case "[AD]":
                    Press(Kim1Key.Address);
                    break;
                case "[DA]":
                    Press(Kim1Key.Data);
                    break;
                case "[+]":
                    Press(Kim1Key.Plus);
                    break;
                case "[GO]":
                    Press(Kim1Key.Go);
                    break;
                case "[PC]":
                    Press(Kim1Key.ProgramCounter);
                    break;
                default:
                    foreach (char c in word)
                    {
                        Press((Kim1Key)Convert.ToInt32(c.ToString(), 16));
                    }

                    break;
            }
        }

        return this;
    }

    public void Press(Kim1Key key)
    {
        Machine.Keypad.Press(key);
        Machine.Run(HoldCycles);
        Machine.Keypad.Release(key);
        Machine.Run(HoldCycles);
    }

    /// <summary>Holds ST for 40 ms and lets go, as a finger does.</summary>
    public void Stop()
    {
        Machine.StopKey = true;
        Machine.Run(HoldCycles);
        Machine.StopKey = false;
        Machine.Run(HoldCycles);
    }

    /// <summary>
    /// True when <paramref name="shown"/> matches <paramref name="expected"/>.
    /// An x in the expectation is the manual's "don't care": any lit hex
    /// digit, but not a dark one.
    /// </summary>
    public static bool Matches(string expected, string shown) =>
        expected.Length == shown.Length && expected.Zip(shown).All(p => p.First == 'x' ? Uri.IsHexDigit(p.Second) : p.First == p.Second);
}
