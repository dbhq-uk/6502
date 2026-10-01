using System.Runtime.InteropServices.JavaScript;
using Dbhq.Machines.Kim1;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page fetches the ROM and calls Kim1Host.Load.
Console.WriteLine("Dbhq.Machines.Kim1.Wasm ready");

/// <summary>
/// The KIM-1, as the page's JavaScript sees it: one machine, its keys, its
/// SST switch, a call that runs it, and the six digits. Everything else,
/// including when to run it and for how long, is the page's.
/// </summary>
public static partial class Kim1Host
{
    private static Kim1Machine? _machine;
    private static Kim1Keystrokes? _keys;

    private static Kim1Machine Machine => _machine ?? throw new InvalidOperationException("Load the ROM first");

    private static Kim1Keystrokes Keys => _keys ?? throw new InvalidOperationException("Load the ROM first");

    /// <summary>Powers on a KIM-1 with the two 1 KB monitor ROMs: the 6530-002's and the 6530-003's.</summary>
    [JSExport]
    public static void Load(byte[] rom002, byte[] rom003)
    {
        _machine = new Kim1Machine(rom002, rom003);
        _keys = new Kim1Keystrokes(_machine);
    }

    /// <summary>Queues one tap of a key, named as the manual names it: 0 to F, AD, DA, +, GO, PC, ST or RS.</summary>
    [JSExport]
    public static void Tap(string key) => Keys.Tap(key);

    /// <summary>Taps waiting to be played, and the one being played.</summary>
    [JSExport]
    public static int Pending() => Keys.Pending;

    /// <summary>The SST slide switch.</summary>
    [JSExport]
    public static void SetSingleStep(bool on) => Machine.SingleStep = on;

    /// <summary>Bus cycles since power on: microseconds of the KIM-1's time.</summary>
    [JSExport]
    public static double Cycles() => Machine.Cycles;

    /// <summary>Runs the machine for at least <paramref name="cycles"/> cycles, playing queued keys, and returns the cycles since power on.</summary>
    [JSExport]
    public static double Run(int cycles)
    {
        Keys.Run(cycles);
        return Machine.Cycles;
    }

    /// <summary>The segments digit <paramref name="digit"/> (0 to 5, left to right) shows now: bit 0 is segment a, bit 6 segment g, and 0 is dark.</summary>
    [JSExport]
    public static int Segments(int digit) => Machine.Bus.Display.Segments(digit, Machine.Cycles);

    /// <summary>The six digits as text, as the tests read them: a hex digit, a space where dark, '?' for any other shape.</summary>
    [JSExport]
    public static string Display() => Machine.ReadDisplay();
}
