using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Dbhq.Machines.Electron;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page fetches the ROMs and calls ElectronHost.Load.
Console.WriteLine("Dbhq.Machines.Electron.Wasm ready");

/// <summary>
/// The Acorn Electron, as the page's JavaScript sees it: one machine and a call that runs it. When
/// to run it and for how long is the page's. So far only the speed check in bench/electron-speed/
/// uses it, and it has no display, sound or keys yet.
/// </summary>
public static partial class ElectronHost
{
    // Mode 6 text: 25 rows of 40 cells at $6000, each cell 8 bytes (ula.md s10b).
    private const int ScreenStart = 0x6000;
    private const int Columns = 40;
    private const int Rows = 25;

    private static ElectronMachine? _machine;
    private static byte[] _os = [];

    private static ElectronMachine Machine => _machine ?? throw new InvalidOperationException("Load the ROMs first");

    /// <summary>
    /// Builds an Electron from the 16 KB OS and BASIC images, its sound made at
    /// <paramref name="sampleRate"/> samples a second, and switches it on.
    /// </summary>
    [JSExport]
    public static void Load(byte[] os, byte[] basic, int sampleRate)
    {
        _os = os;
        _machine = new ElectronMachine(new ElectronRoms(os, basic), new ElectronOptions { SampleRate = sampleRate });
        _machine.PowerOn();
    }

    /// <summary>
    /// Runs the machine for at least <paramref name="cycles"/> 2 MHz cycles and returns the cycles
    /// since power on.
    /// </summary>
    [JSExport]
    public static double Run(int cycles)
    {
        Machine.Run(cycles);
        return Machine.Cycles;
    }

    /// <summary>2 MHz cycles since power on, wait cycles included.</summary>
    [JSExport]
    public static double Cycles() => Machine.Cycles;

    /// <summary>A byte of memory, read without a bus cycle.</summary>
    [JSExport]
    public static int Peek(int address) => Machine.Bus.Peek((ushort)address);

    /// <summary>The display mode the ULA is in, 0 to 6 (it reports mode 7 as 4).</summary>
    [JSExport]
    public static int Mode() => Machine.Bus.Ula.Mode;

    /// <summary>
    /// For the speed check: puts the ULA in display mode <paramref name="mode"/> (0 to 6) by
    /// writing its control register, $FE07, as a program would (caps lock on, sound and cassette
    /// modes off, motor off). It is a bus write, so one cycle or three pass, between instructions.
    /// </summary>
    [JSExport]
    public static void SetMode(int mode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mode, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mode, 6);
        Machine.Bus.Write(0xFE07, (byte)(0x80 | (mode << 3)));
    }

    /// <summary>
    /// Mode 6 only: the 40 characters of text row <paramref name="row"/> (0 to 24), each cell of
    /// screen memory matched against the glyphs of the OS ROM's font ($C000 + (code - $20) x 8).
    /// A cell that matches no glyph (the bell, the cursor) shows as U+FFFD.
    /// </summary>
    [JSExport]
    public static string ScreenRow(int row)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(row, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);

        var text = new StringBuilder(Columns);
        for (int column = 0; column < Columns; column++)
        {
            int cell = ScreenStart + (row * Columns * 8) + (column * 8);
            text.Append(Glyph(cell));
        }

        return text.ToString();
    }

    private static char Glyph(int cell)
    {
        for (int code = 0x20; code <= 0x7F; code++)
        {
            int font = (code - 0x20) * 8;
            int i = 0;
            while (i < 8 && Machine.Bus.Peek((ushort)(cell + i)) == _os[font + i])
            {
                i++;
            }

            if (i == 8)
            {
                return (char)code;
            }
        }

        return '\uFFFD';
    }
}
