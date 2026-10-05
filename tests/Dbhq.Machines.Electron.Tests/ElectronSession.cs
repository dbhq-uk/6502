using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>An Electron for a test: the pinned ROMs, and ways to drive it and look at it.</summary>
public sealed class ElectronSession
{
    /// <summary>
    /// How long <see cref="Type"/> holds each key down, in CPU cycles: 40 ms, one frame of 80,000
    /// cycles (<c>ula.md</c> s5d), which is four of the OS's 100 Hz ticks (s6c: one at each clock
    /// and display-end interrupt). The OS scans the keyboard on those interrupts (s7a), so a key
    /// held this long is seen however the press falls against them.
    /// </summary>
    public const long HoldCycles = 80_000;

    /// <summary>
    /// How long <see cref="Type"/> waits with every key up before the next, in CPU cycles: one frame
    /// again, so the OS's scan sees the key go up before another comes down.
    /// </summary>
    public const long RestCycles = 80_000;

    private static readonly Lazy<ElectronRoms> LazyRoms = new(() => new ElectronRoms(
        RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256),
        RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256)));

    /// <param name="options">The machine's options; the default is a stock Electron.</param>
    public ElectronSession(ElectronOptions? options = null)
    {
        Machine = new ElectronMachine(Roms, options);
    }

    /// <summary>The OS ROM and BASIC, each checked against its pinned SHA-256.</summary>
    public static ElectronRoms Roms => LazyRoms.Value;

    /// <summary>
    /// For each character <see cref="Type"/> can type, the key and whether Shift goes with it. The
    /// keys are the matrix positions of <c>ula.md</c> s7b, and the shifted characters are the
    /// Electron's own key legends: Shift and a digit gives <c>!"#$%&amp;'()</c> on 1 to 9 and
    /// <c>@</c> on 0, and Shift gives <c>=</c> on <c>-</c>, <c>*</c> on <c>:</c>, <c>+</c> on
    /// <c>;</c>, <c>?</c> on <c>/</c>, <c>&gt;</c> on <c>.</c> and <c>&lt;</c> on <c>,</c>. Letters
    /// are typed unshifted: the OS starts with Caps Lock on (s7c), so they come out as capitals.
    /// A test holds this table against the OS ROM's own key and Shift tables.
    /// </summary>
    public static IReadOnlyDictionary<char, (ElectronKey Key, bool Shift)> Keys { get; } = MakeKeys();

    public ElectronMachine Machine { get; }

    /// <summary>
    /// Called after every instruction while set, for a test-only device that acts at its own
    /// times (<see cref="TapeProbe"/>). Null, the default, runs the machine in whole runs.
    /// </summary>
    internal Action? AfterEachStep { get; set; }

    /// <summary>Switches on and runs; the default is one second of machine time (<c>ula.md</c> s10a, s10b).</summary>
    public ElectronSession Boot(long cycles = 2_000_000)
    {
        Machine.PowerOn();
        Run(cycles);
        return this;
    }

    /// <summary>Runs for at least <paramref name="cycles"/> more CPU cycles.</summary>
    public ElectronSession RunFor(long cycles)
    {
        Run(cycles);
        return this;
    }

    /// <summary>
    /// Types <paramref name="text"/> on the keyboard matrix, a key at a time, running the machine
    /// throughout: Shift down with the key where the character needs it, the key held for
    /// <see cref="HoldCycles"/>, then every key up for <see cref="RestCycles"/>. A carriage return
    /// or a newline is Return.
    /// </summary>
    public ElectronSession Type(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ElectronKeyboard keyboard = Machine.Keyboard;
        foreach (char c in text)
        {
            if (!Keys.TryGetValue(c == '\n' ? '\r' : c, out (ElectronKey Key, bool Shift) press))
            {
                throw new ArgumentOutOfRangeException(nameof(text), c, "No key types this character.");
            }

            if (press.Shift)
            {
                keyboard.Down(ElectronKey.Shift);
            }

            keyboard.Down(press.Key);
            Run(HoldCycles);
            keyboard.Up(press.Key);
            if (press.Shift)
            {
                keyboard.Up(ElectronKey.Shift);
            }

            Run(RestCycles);
        }

        return this;
    }

    /// <summary>Mode 6 only: the 25 rows of 40 characters in screen memory at <c>$6000</c> (<see cref="ScreenMemoryText"/>).</summary>
    public string[] ScreenMemoryRows() => ScreenMemoryText.Read(Machine.Bus, Roms.Os);

    /// <summary>
    /// The OS's screen mode, from its variable at <c>$0355</c>: the mode change reads its grid
    /// tables with it (<c>LDX $0355 : LDY $C3B4,X</c> at <c>$CA0C</c>).
    /// </summary>
    public int OsMode => Machine.Bus.Peek(0x0355);

    /// <summary>
    /// The OS's text cursor, column at <c>$0318</c> and row at <c>$0319</c>: the mode change and
    /// CLS zero both (<c>STA $0318 : STA $0319</c> at <c>$CBB0</c>).
    /// </summary>
    public (int Column, int Row) OsCursor => (Machine.Bus.Peek(0x0318), Machine.Bus.Peek(0x0319));

    /// <summary>
    /// The text on the screen, a string a row, each as wide as the mode, read off the picture by
    /// <see cref="global::Dbhq.Machines.Electron.Tests.ScreenText"/>. Two things come from the
    /// OS's variables, neither of them screen memory: the mode (<see cref="OsMode"/>), which says
    /// how to cut the picture into cells, and the cursor (<see cref="OsCursor"/>), which says
    /// which cell's line 7 may be the cursor's.
    /// </summary>
    public string[] ScreenText() =>
        global::Dbhq.Machines.Electron.Tests.ScreenText.Read(Machine.Bus.Screen.Pixels, OsMode, OsCursor, Roms.Os);

    /// <summary>
    /// Runs until BASIC's prompt is on the picture and has stayed for a field: the last row that
    /// is not blank is <c>&gt;</c> and nothing else, with the OS's cursor beside it, in column 1 of
    /// that row, and the whole screen read the same a field later. Fails with the screen as read
    /// if it has not happened within <paramref name="maxCycles"/>.
    /// </summary>
    public ElectronSession RunUntilPrompt(long maxCycles = 4_000_000)
    {
        // A field is 39,936 or 40,064 cycles (ula.md s5d); 40,000 is their mean, and two reads
        // that far apart are a field apart give or take a line.
        const long Field = 40_000;
        long end = Machine.Cycles + maxCycles;
        string[]? previous = null;
        string[] rows = [];
        while (Machine.Cycles < end)
        {
            Run(Field);
            rows = ScreenText();
            bool atPrompt = AtPrompt(rows, OsCursor);
            if (atPrompt && previous is not null && rows.SequenceEqual(previous))
            {
                return this;
            }

            previous = atPrompt ? rows : null;
        }

        throw new Xunit.Sdk.XunitException(
            $"No prompt within {maxCycles:N0} cycles. Mode {OsMode}, cursor {OsCursor}, the screen:\n" +
            string.Join("\n", rows.Select(r => "|" + r.TrimEnd())));
    }

    /// <summary>Whether the last row that is not blank is the prompt alone, with the cursor beside it.</summary>
    private static bool AtPrompt(string[] rows, (int Column, int Row) cursor)
    {
        int last = Array.FindLastIndex(rows, r => r.TrimEnd().Length > 0);
        return last >= 0 && rows[last].TrimEnd() == ">" && cursor == (1, last);
    }

    /// <summary>Runs whole instructions for at least <paramref name="cycles"/> cycles, calling <see cref="AfterEachStep"/> after each if it is set.</summary>
    private void Run(long cycles)
    {
        if (AfterEachStep is not { } after)
        {
            Machine.Run(cycles);
            return;
        }

        long end = Machine.Cycles + cycles;
        while (Machine.Cycles < end)
        {
            Machine.Step();
            after();
        }
    }

    private static Dictionary<char, (ElectronKey, bool)> MakeKeys()
    {
        var keys = new Dictionary<char, (ElectronKey, bool)>
        {
            [' '] = (ElectronKey.Space, false),
            ['\r'] = (ElectronKey.Return, false),
        };

        for (char c = 'A'; c <= 'Z'; c++)
        {
            keys.Add(c, (Enum.Parse<ElectronKey>(c.ToString()), false));
        }

        // The digits, and what Shift gives on each: index 0 is the 0 key.
        const string shiftedDigits = "@!\"#$%&'()";
        for (int d = 0; d <= 9; d++)
        {
            ElectronKey key = Enum.Parse<ElectronKey>("D" + d);
            keys.Add((char)('0' + d), (key, false));
            keys.Add(shiftedDigits[d], (key, true));
        }

        foreach ((char plain, char shifted, ElectronKey key) in new[]
        {
            ('-', '=', ElectronKey.Minus),
            (':', '*', ElectronKey.Colon),
            (';', '+', ElectronKey.Semicolon),
            ('/', '?', ElectronKey.Slash),
            ('.', '>', ElectronKey.FullStop),
            (',', '<', ElectronKey.Comma),
        })
        {
            keys.Add(plain, (key, false));
            keys.Add(shifted, (key, true));
        }

        return keys;
    }
}
