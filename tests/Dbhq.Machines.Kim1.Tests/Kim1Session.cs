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

    /// <summary>The 6530-002's 1 KB, from roms/ in this repository, hash-checked.</summary>
    public static byte[] Rom002 => RepoPaths.ReadChecked(Pins.Kim1Rom002Path, Pins.Kim1Rom002Sha256);

    /// <summary>The 6530-003's 1 KB, likewise.</summary>
    public static byte[] Rom003 => RepoPaths.ReadChecked(Pins.Kim1Rom003Path, Pins.Kim1Rom003Sha256);

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
        if (Machine.Mode == Kim1Mode.Teletype)
        {
            RunTeletype();
        }

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

    /// <summary>The teletype's paper: everything printed since the session began.</summary>
    public TeletypePaper Paper { get; } = new();

    /// <summary>Everything printed since the session began, as it came off the line.</summary>
    public string Printed { get; private set; } = "";

    /// <summary>Closes the TTY/KB switch, presses RS and types RUB OUT, as the User Manual's section 2.6 says to.</summary>
    public Kim1Session LogOn()
    {
        Machine.Mode = Kim1Mode.Teletype;
        Keys("[RS]");
        return Type("[RUBOUT]");
    }

    /// <summary>
    /// Types at the teletype, written as the User Manual writes it: [SPACE],
    /// [CR], [LF] and [RUBOUT] in brackets, other characters as themselves.
    /// Runs until the keys have all gone and the monitor has finished
    /// answering, then puts what was printed on the paper.
    /// </summary>
    public Kim1Session Type(string keys)
    {
        Machine.Teletype.Send(Kim1Teletype.Parse(keys));
        return RunTeletype();
    }

    /// <summary>Runs until the teletype is idle, or for at most <paramref name="limit"/> cycles, and prints what came.</summary>
    public Kim1Session RunTeletype(long limit = 120_000_000)
    {
        long end = Machine.Cycles + limit;
        do
        {
            Machine.Run(10_000);
        }
        while (!Machine.Teletype.Idle(Machine.Cycles) && Machine.Cycles < end);

        Print();
        return this;
    }

    /// <summary>Moves what the teletype has printed onto the paper.</summary>
    public void Print()
    {
        string text = Machine.ReadTeletype();
        Printed += text;
        Paper.Print(text);
    }

    /// <summary>Holds ST for 40 ms and lets go, as a finger does.</summary>
    public void Stop() => Keys("[ST]");

    /// <summary>
    /// True when <paramref name="shown"/> matches <paramref name="expected"/>.
    /// An x in the expectation is the manual's "don't care": any lit hex
    /// digit, but not a dark one.
    /// </summary>
    /// <summary>Whether each of <paramref name="shown"/> lines matches its line of <paramref name="expected"/>, as <see cref="Matches(string, string)"/> matches.</summary>
    public static bool Matches(IReadOnlyList<string> expected, IReadOnlyList<string> shown) =>
        expected.Count == shown.Count && expected.Zip(shown).All(p => Matches(p.First, p.Second));

    public static bool Matches(string expected, string shown) =>
        expected.Length == shown.Length && expected.Zip(shown).All(p => p.First == 'x' ? Uri.IsHexDigit(p.Second) : p.First == p.Second);
}
