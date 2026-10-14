using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Dbhq.Machines.Electron;
using Dbhq.Machines.Electron.Tape;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page fetches the ROMs and calls ElectronHost.Load.
Console.WriteLine("Dbhq.Machines.Electron.Wasm ready");

/// <summary>
/// The Acorn Electron, as the page's JavaScript sees it: one machine, a call that runs it, its
/// keys, BREAK, its picture, its sound and its cassette. Everything else, including when to run it
/// and for how long, is the page's (on the shared host site/public/machine-host.js). The speed
/// check in bench/electron-speed/ uses the same class, and the calls it alone uses are kept for it:
/// <see cref="Peek"/>, <see cref="Mode"/>, <see cref="SetMode"/> and <see cref="ScreenRow"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The picture and the sound are handed over, not copied out</b>, as the BBC Micro's host does
/// and for its reason. A frame is 640 by 256 RGBA pixels, 640 KB; returning it as an array would
/// copy it twice (into a new .NET array, then into a new JavaScript one) and leave the garbage
/// collector to clear both. Instead <see cref="Picture"/> calls the page's <c>picture</c> function,
/// imported from the JavaScript module <c>electron</c>, with a view of the framebuffer's own
/// memory, and the page copies it once, straight into its canvas's pixels; the view is good only
/// during that call. The pixels are <c>0xAABBGGRR</c>, so in memory they are R, G, B, A, the order
/// a canvas's ImageData keeps. The sound goes the same way through <c>sound</c>. A view can be
/// bytes, ints or doubles, not floats, so the samples go as their bytes and the page reads them as
/// a Float32Array. The page registers both functions before it calls <see cref="Load"/>.
/// </para>
/// <para>
/// <b>A key is its place in the matrix.</b> <see cref="PressKey"/> and <see cref="ReleaseKey"/>
/// take the value of <see cref="ElectronKey"/>: the column, 0 to 13, plus sixteen times the bit,
/// 0 to 3 (fact sheet <c>ula.md</c> s7b). That number is fixed by the hardware, so it does not move
/// if the enum is reordered, and the page maps a browser's <c>event.code</c> to it. A number that
/// is not a key is refused.
/// </para>
/// <para>
/// <b>Nothing from a file throws.</b> <see cref="InsertTape"/> reads a UEF the visitor chose, so a
/// file that is not one this machine can play is the visitor's mistake, not the page's: it returns
/// the reader's reason as text, and an empty string when the tape went in.
/// </para>
/// </remarks>
public static partial class ElectronHost
{
    // Mode 6 text: 25 rows of 40 cells at $6000, each cell 8 bytes (ula.md s10b).
    private const int ScreenStart = 0x6000;
    private const int Columns = 40;
    private const int Rows = 25;

    /// <summary>The origin text written into a tape the page saves.</summary>
    private const string Origin = "dbhq-uk/6502, the Acorn Electron at 6502.dbhq.uk";

    private static ElectronMachine? _machine;
    private static ElectronKeyPresses? _keys;
    private static byte[] _os = [];
    private static float[] _samples = [];

    private static ElectronMachine Machine => _machine ?? throw new InvalidOperationException("Load the ROMs first");

    private static ElectronKeyPresses Keys => _keys ?? throw new InvalidOperationException("Load the ROMs first");

    /// <summary>
    /// Builds an Electron from the 16 KB OS and BASIC images, its sound made at
    /// <paramref name="sampleRate"/> samples a second, and switches it on.
    /// </summary>
    [JSExport]
    public static void Load(byte[] os, byte[] basic, int sampleRate)
    {
        _os = os;
        _machine = new ElectronMachine(new ElectronRoms(os, basic), new ElectronOptions { SampleRate = sampleRate });
        _keys = new ElectronKeyPresses(_machine);
        _samples = new float[_machine.Bus.Sound.Buffer.Capacity];
        _machine.PowerOn();
    }

    /// <summary>
    /// Runs the machine for at least <paramref name="cycles"/> 2 MHz cycles, playing the keys the
    /// page has reported on the cycles they fall due, and returns the cycles since power on.
    /// </summary>
    [JSExport]
    public static double Run(int cycles)
    {
        Keys.Run(cycles);
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

    /// <summary>
    /// A key going down, by its place in the matrix (the value of <see cref="ElectronKey"/>). It is
    /// played into the matrix through <see cref="ElectronKeyPresses"/>, so a key that comes up at
    /// once is still held long enough for the OS to see it.
    /// </summary>
    [JSExport]
    public static void PressKey(int key) => Keys.Down((ElectronKey)key);

    /// <summary>A key coming up, by its place in the matrix.</summary>
    [JSExport]
    public static void ReleaseKey(int key) => Keys.Up((ElectronKey)key);

    /// <summary>The BREAK key: the CPU is reset and RAM and the ULA are kept, as the machine's own does (<see cref="ElectronMachine.PressBreak"/>).</summary>
    [JSExport]
    public static void Break() => Machine.PressBreak();

    /// <summary>Frames the ULA has completed since power on: when it moves on, there is a new picture.</summary>
    [JSExport]
    public static double Frames() => Machine.Bus.Screen.Frames;

    /// <summary>
    /// Hands the page the picture as it stands: calls its <c>picture</c> function with a view of
    /// the framebuffer's bytes, 640 by 256 pixels of four bytes, R, G, B and A for each. Returns the
    /// frames completed, as <see cref="Frames"/>.
    /// </summary>
    [JSExport]
    public static double Picture()
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(Machine.Bus.Screen.Pixels);
        ShowPicture(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(bytes), bytes.Length));
        return Machine.Bus.Screen.Frames;
    }

    /// <summary>
    /// Takes the sound made since the last call and, if there is any, hands it to the page's
    /// <c>sound</c> function as the bytes of that many 32-bit float samples: silence is 0 and a
    /// tone swings about a half either side of it (<see cref="UlaSound"/>). Returns how many samples it handed over.
    /// </summary>
    [JSExport]
    public static int SoundSamples()
    {
        int read = Machine.Bus.Sound.Buffer.Read(_samples);
        if (read > 0)
        {
            PlaySound(MemoryMarshal.AsBytes(_samples.AsSpan(0, read)));
        }

        return read;
    }

    /// <summary>Throws away the sound made so far, so it starts afresh when the page turns it on. Returns how many samples went.</summary>
    [JSExport]
    public static int DropSound() => Machine.Bus.Sound.Buffer.Read(_samples);

    /// <summary>
    /// Puts a tape in, rewound, from the bytes of a UEF file, gzipped or not. Returns an empty
    /// string when it went in, or the reader's reason when the file is not a tape this machine can
    /// play; then the tape that was in stays in. It plays when the OS turns the motor on.
    /// </summary>
    [JSExport]
    public static string InsertTape(byte[] uefFile)
    {
        IReadOnlyList<TapeEvent> tape;
        try
        {
            tape = UefReader.Read(uefFile);
        }
        catch (UefException e)
        {
            return e.Message;
        }

        Machine.InsertTape(tape);
        return "";
    }

    /// <summary>
    /// Takes the tape out and returns it as a UEF file's bytes, gzipped: the tape that was put in,
    /// or what was recorded since <see cref="StartRecording"/>. Returns null when there was no tape,
    /// or a recording with nothing on it.
    /// </summary>
    [JSExport]
    public static byte[]? EjectTape()
    {
        IReadOnlyList<TapeEvent> tape = Machine.EjectTape();
        return tape.Count == 0 ? null : UefWriter.Write(tape, Origin);
    }

    /// <summary>Winds the tape back to its start. A recording stops, and is then the tape that plays.</summary>
    [JSExport]
    public static void Rewind() => Machine.Rewind();

    /// <summary>Puts in a blank tape and presses record: what the OS saves from now is recorded.</summary>
    [JSExport]
    public static void StartRecording() => Machine.StartRecording();

    /// <summary>Whether the OS has the cassette motor on, as the Electron's own motor light would show.</summary>
    [JSExport]
    public static bool MotorOn() => Machine.MotorOn;

    /// <summary>Seconds of tape played since it went in or was rewound, or recorded since recording started.</summary>
    [JSExport]
    public static double TapeSeconds() => Machine.TapePosition / (double)TapeTiming.CpuHz;

    /// <summary>Bytes of this tape load that the OS did not read in time: none, on a good load.</summary>
    [JSExport]
    public static int LostBytes() => Machine.LostBytes;

    [JSImport("picture", "electron")]
    private static partial void ShowPicture([JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    [JSImport("sound", "electron")]
    private static partial void PlaySound([JSMarshalAs<JSType.MemoryView>] Span<byte> samples);

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
