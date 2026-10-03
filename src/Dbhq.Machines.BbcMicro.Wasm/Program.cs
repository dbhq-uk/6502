using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Dbhq.Machines.BbcMicro;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page fetches the ROMs and calls BbcHost.Load.
Console.WriteLine("Dbhq.Machines.BbcMicro.Wasm ready");

/// <summary>
/// The BBC Micro Model B, as the page's JavaScript sees it: one machine, a call that
/// runs it, and the mode 7 text screen. Everything else, including when to run it and
/// for how long, is the page's.
/// </summary>
public static partial class BbcHost
{
    private static BbcMachine? _machine;

    private static BbcMachine Machine => _machine ?? throw new InvalidOperationException("Load the ROMs first");

    /// <summary>Builds a Model B from the three 16 KB ROM images and switches it on.</summary>
    [JSExport]
    public static void Load(byte[] os, byte[] basic, byte[] dfs)
    {
        _machine = new BbcMachine(new BbcRoms(os, basic, dfs));
        _machine.PowerOn();
    }

    /// <summary>
    /// As <see cref="Load"/>, with the start-up links set for screen mode <paramref name="mode"/>
    /// (0 to 7), so the speed check can be run in a mode the video ULA draws pixels in.
    /// </summary>
    [JSExport]
    public static void LoadInMode(byte[] os, byte[] basic, byte[] dfs, int mode)
    {
        _machine = new BbcMachine(new BbcRoms(os, basic, dfs), new BbcOptions { StartupMode = mode });
        _machine.PowerOn();
    }

    /// <summary>
    /// For the speed check's worst case: fills the 1,000 bytes of mode 7 screen memory at &amp;7C00
    /// with random bytes (<paramref name="kind"/> "dense", every control code included) or random
    /// printable characters ("text"), from seed 1234, as the native bench's <c>DensePage</c> does,
    /// so the teletext chip draws every cell.
    /// </summary>
    [JSExport]
    public static void FillScreen(string kind)
    {
        var random = new Random(1234);
        for (int i = 0; i < 1000; i++)
        {
            int value = kind switch
            {
                "dense" => random.Next(256),
                "text" => random.Next(0x20, 0x7F),
                _ => throw new ArgumentException($"no screen called {kind}: dense or text"),
            };
            Machine.Bus.PokeRam((ushort)(0x7C00 + i), (byte)value);
        }
    }

    private static readonly float[] Samples = new float[48_000];

    /// <summary>
    /// For the speed check's sound load: <paramref name="kind"/> "tone" sets all four channels
    /// sounding, three tones and white noise, written straight to the chip; "silent" leaves the
    /// chip as the OS left it. The native bench's <c>SoundLoad</c> does the same.
    /// </summary>
    [JSExport]
    public static void SoundOn(string kind)
    {
        byte[] bytes = kind switch
        {
            "silent" => [],
            "tone" => [0x8D, 0x0E, 0x90, 0xA5, 0x13, 0xB2, 0xC3, 0x1A, 0xD4, 0xE4, 0xF6],
            _ => throw new ArgumentException($"no sound called {kind}: silent or tone"),
        };
        foreach (byte value in bytes)
        {
            Machine.Bus.SoundChip.Write(value);
        }
    }

    /// <summary>
    /// As <see cref="Run"/>, reading the sound buffer every 40,000 cycles, a field, as a page reads
    /// it every frame, and returns the cycles since power on.
    /// </summary>
    [JSExport]
    public static double RunWithSound(int cycles)
    {
        long end = Machine.Cycles + cycles;
        while (Machine.Cycles < end)
        {
            Machine.Run(Math.Min(40_000, end - Machine.Cycles));
            Machine.Sound.Read(Samples);
        }
        return Machine.Cycles;
    }

    /// <summary>A byte of memory, read without a bus cycle.</summary>
    [JSExport]
    public static int Peek(int address) => Machine.Bus.Peek((ushort)address);

    /// <summary>2 MHz cycles since power on, stretch cycles included.</summary>
    [JSExport]
    public static double Cycles() => Machine.Cycles;

    /// <summary>Runs the machine for at least <paramref name="cycles"/> 2 MHz cycles and returns the cycles since power on.</summary>
    [JSExport]
    public static double Run(int cycles)
    {
        Machine.Run(cycles);
        return Machine.Cycles;
    }

    /// <summary>
    /// Mode 7 only: the 40 characters of text row <paramref name="row"/> (0 to 24), read from screen
    /// memory at <c>$7C00 + row * 40</c>. A code outside the printable ASCII range shows as a space.
    /// </summary>
    [JSExport]
    public static string ScreenRow(int row)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(row, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(row, 24);

        var text = new StringBuilder(40);
        for (int i = 0; i < 40; i++)
        {
            byte code = Machine.Bus.Peek((ushort)(0x7C00 + (row * 40) + i));
            text.Append(code is >= 0x20 and < 0x7F ? (char)code : ' ');
        }

        return text.ToString();
    }
}
