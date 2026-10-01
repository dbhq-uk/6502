namespace Dbhq.Machines.Kim1;

/// <summary>
/// Key taps, queued and played into a <see cref="Kim1Machine"/> at a person's
/// pace: each key is held for <see cref="HoldCycles"/> and then left up for as
/// long again before the next one goes down. The browser page and the tests
/// both press keys through this, so the page is driven the way the acceptance
/// test drives the machine.
/// </summary>
/// <remarks>
/// <para>
/// The monitor reads a key only once it has seen it down across a whole scan,
/// and waits for it to come up before it takes another. A click on a web page
/// can be over in a few milliseconds, which the monitor would miss, and a
/// visitor can click faster than the monitor reads. Playing taps at a fixed
/// pace, timed in machine cycles rather than in wall-clock time, makes every
/// click count once, however fast it was or however slowly the browser runs.
/// </para>
/// <para>
/// Keys are named as the User Manual names them: <c>0</c> to <c>F</c>,
/// <c>AD</c>, <c>DA</c>, <c>+</c>, <c>GO</c> and <c>PC</c> in the key matrix,
/// and <c>ST</c> and <c>RS</c>, which are wired to the CPU instead.
/// </para>
/// </remarks>
public sealed class Kim1Keystrokes
{
    /// <summary>How long a key is held, and then left up: 40 ms each at 1 MHz.</summary>
    public const long HoldCycles = 40_000;

    private readonly Kim1Machine _machine;
    private readonly Queue<string> _pending = new();
    private string? _down;
    private long _until;

    public Kim1Keystrokes(Kim1Machine machine)
    {
        _machine = machine;
    }

    /// <summary>Every key name <see cref="Tap"/> accepts.</summary>
    public static IReadOnlyList<string> Names { get; } =
        ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "A", "B", "C", "D", "E", "F", "AD", "DA", "+", "GO", "PC", "ST", "RS"];

    /// <summary>Taps waiting to be played, and the one being played, if any.</summary>
    public int Pending => _pending.Count + (_down is null ? 0 : 1);

    /// <summary>True when nothing is queued and the last key has been up for its full rest.</summary>
    public bool Idle => _pending.Count == 0 && _down is null && _machine.Cycles >= _until;

    /// <summary>Queues one tap of the key called <paramref name="name"/>.</summary>
    public void Tap(string name)
    {
        if (!Names.Contains(name))
        {
            throw new ArgumentException($"\"{name}\" is not a KIM-1 key", nameof(name));
        }

        _pending.Enqueue(name);
    }

    /// <summary>
    /// Splits keys written as the User Manual writes them into key names:
    /// <c>[AD]</c>, <c>[DA]</c>, <c>[+]</c>, <c>[GO]</c>, <c>[PC]</c>,
    /// <c>[ST]</c> and <c>[RS]</c> in brackets, and a run of hex digits as one
    /// key per character. "[AD] 0002 [DA] 18" is eight keys. The brackets are
    /// needed because AD and DA are also bytes: $AD is LDA absolute.
    /// </summary>
    public static IReadOnlyList<string> Parse(string keys)
    {
        var names = new List<string>();
        foreach (string word in keys.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.StartsWith('[') && word.EndsWith(']'))
            {
                string name = word[1..^1];
                if (!Names.Contains(name))
                {
                    throw new FormatException($"\"{word}\" is not a bracketed KIM-1 key");
                }

                names.Add(name);
                continue;
            }

            foreach (char c in word)
            {
                if (!Uri.IsHexDigit(c))
                {
                    throw new FormatException($"'{c}' in \"{word}\" is not a hex key; write the other keys in brackets");
                }

                names.Add(char.ToUpperInvariant(c).ToString());
            }
        }

        return names;
    }

    /// <summary>Queues every key in <paramref name="keys"/>, written as <see cref="Parse"/> reads them.</summary>
    public void Type(string keys)
    {
        foreach (string name in Parse(keys))
        {
            Tap(name);
        }
    }

    /// <summary>
    /// Runs the machine for at least <paramref name="cycles"/> more bus cycles,
    /// pressing and releasing queued keys on the cycles they fall due.
    /// </summary>
    public void Run(long cycles)
    {
        long end = _machine.Cycles + cycles;
        while (_machine.Cycles < end)
        {
            Advance();
            long next = _down is not null || _machine.Cycles < _until ? Math.Min(end, _until) : end;
            _machine.Run(Math.Max(1, next - _machine.Cycles));
        }

        Advance();
    }

    /// <summary>Runs until every queued key has been pressed, released and rested.</summary>
    public void RunUntilIdle()
    {
        while (!Idle)
        {
            Run(Math.Max(1, _until - _machine.Cycles));
        }
    }

    private void Advance()
    {
        long now = _machine.Cycles;
        if (_down is not null && now >= _until)
        {
            Release(_down);
            _down = null;
            _until = now + HoldCycles;
        }

        if (_down is null && now >= _until && _pending.Count > 0)
        {
            _down = _pending.Dequeue();
            Press(_down);
            _until = now + HoldCycles;
        }
    }

    private void Press(string name)
    {
        switch (name)
        {
            case "ST":
                _machine.StopKey = true;
                break;
            case "RS":
                _machine.PressReset();
                break;
            default:
                _machine.Keypad.Press(Key(name));
                break;
        }
    }

    private void Release(string name)
    {
        switch (name)
        {
            case "ST":
                _machine.StopKey = false;
                break;
            case "RS":
                break;
            default:
                _machine.Keypad.Release(Key(name));
                break;
        }
    }

    private static Kim1Key Key(string name) => name switch
    {
        "AD" => Kim1Key.Address,
        "DA" => Kim1Key.Data,
        "+" => Kim1Key.Plus,
        "GO" => Kim1Key.Go,
        "PC" => Kim1Key.ProgramCounter,
        _ => (Kim1Key)Convert.ToInt32(name, 16),
    };
}
