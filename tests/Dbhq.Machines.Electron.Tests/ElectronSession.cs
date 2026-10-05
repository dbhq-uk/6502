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

    /// <summary>Switches on and runs; the default is one second of machine time (<c>ula.md</c> s10a, s10b).</summary>
    public ElectronSession Boot(long cycles = 2_000_000)
    {
        Machine.PowerOn();
        Machine.Run(cycles);
        return this;
    }

    /// <summary>Runs for at least <paramref name="cycles"/> more CPU cycles.</summary>
    public ElectronSession RunFor(long cycles)
    {
        Machine.Run(cycles);
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
            Machine.Run(HoldCycles);
            keyboard.Up(press.Key);
            if (press.Shift)
            {
                keyboard.Up(ElectronKey.Shift);
            }

            Machine.Run(RestCycles);
        }

        return this;
    }

    /// <summary>Mode 6 only: the 25 rows of 40 characters in screen memory at <c>$6000</c> (<see cref="ScreenMemoryText"/>).</summary>
    public string[] ScreenMemoryRows() => ScreenMemoryText.Read(Machine.Bus, Roms.Os);

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
