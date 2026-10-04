using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Dbhq.Machines.BbcMicro;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page fetches the ROMs and calls BbcHost.Load.
Console.WriteLine("Dbhq.Machines.BbcMicro.Wasm ready");

/// <summary>
/// The BBC Micro Model B, as the page's JavaScript sees it: one machine, a call that runs it,
/// its keys, BREAK, its picture, its sound and its disc drives. Everything else, including when
/// to run it and for how long, is the page's (site/public/bbc-micro.js, on the shared host
/// site/public/machine-host.js). The speed check in bench/bbc-micro-speed/ uses the same class.
/// </summary>
/// <remarks>
/// <para>
/// <b>The picture and the sound are handed over, not copied out.</b> A field is 640 by 512 RGBA
/// pixels, 1.3 MB, fifty times a second. Returning it as an array would copy it twice (into a new
/// .NET array, then into a new JavaScript one) and leave the garbage collector 65 MB a second to
/// clear. Instead <see cref="Picture"/> calls the page's <c>picture</c> function, imported from
/// the JavaScript module <c>bbc-micro</c>, with a view of the framebuffer's own memory, and the
/// page copies it once, straight into its canvas's pixels; the view is good only during that call.
/// The pixels are <c>0xAABBGGRR</c>, so in memory they are R, G, B, A, the order a canvas's
/// ImageData keeps. The sound goes the same way through <c>sound</c>. A view can be bytes, ints or
/// doubles, not floats, so the samples go as their bytes and the page reads them as a
/// Float32Array. The page registers both functions before it calls <see cref="Load"/>.
/// </para>
/// </remarks>
public static partial class BbcHost
{
    private static BbcMachine? _machine;
    private static BbcKeyPresses? _keys;
    private static float[] _samples = [];

    private static BbcMachine Machine => _machine ?? throw new InvalidOperationException("Load the ROMs first");

    private static BbcKeyPresses Keys => _keys ?? throw new InvalidOperationException("Load the ROMs first");

    /// <summary>
    /// Builds a Model B from the three 16 KB ROM images, with the start-up links set for screen
    /// mode <paramref name="mode"/> (0 to 7; the machine's own default is 7) and its sound made at
    /// <paramref name="sampleRate"/> samples a second, and switches it on.
    /// </summary>
    [JSExport]
    public static void Load(byte[] os, byte[] basic, byte[] dfs, int mode, int sampleRate)
    {
        _machine = new BbcMachine(new BbcRoms(os, basic, dfs), new BbcOptions { StartupMode = mode, SampleRate = sampleRate });
        _keys = new BbcKeyPresses(_machine);
        _samples = new float[_machine.Sound.Capacity];
        _machine.PowerOn();
    }

    /// <summary>
    /// For the speed check: <see cref="Load"/> in screen mode <paramref name="mode"/>, at the
    /// default sample rate. Kept so the check can time builds from before <see cref="Load"/> took a
    /// mode.
    /// </summary>
    [JSExport]
    public static void LoadInMode(byte[] os, byte[] basic, byte[] dfs, int mode) =>
        Load(os, basic, dfs, mode, new BbcOptions().SampleRate);

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

    private static readonly float[] BenchSamples = new float[48_000];
    private static bool _tone;

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
        _tone = kind == "tone";
    }

    /// <summary>
    /// As <see cref="Run"/>, reading the sound buffer every 40,000 cycles, a field, as a page reads
    /// it every frame, and returns the cycles since power on. After <see cref="SoundOn"/> with
    /// "tone" it throws if a field's samples were all silent.
    /// </summary>
    [JSExport]
    public static double RunWithSound(int cycles)
    {
        long end = Machine.Cycles + cycles;
        while (Machine.Cycles < end)
        {
            Machine.Run(Math.Min(40_000, end - Machine.Cycles));
            int read = Machine.Sound.Read(BenchSamples);

            // The OS can write the chip at any time; the load's figure means nothing if it fell silent.
            // A last short run can end before the chip's next sample, and no samples is not silence.
            if (_tone && read > 0 && !BenchSamples.AsSpan(0, read).ContainsAnyExcept(0f))
            {
                throw new InvalidOperationException("the tone load went silent");
            }
        }
        return Machine.Cycles;
    }

    /// <summary>A byte of memory, read without a bus cycle.</summary>
    [JSExport]
    public static int Peek(int address) => Machine.Bus.Peek((ushort)address);

    /// <summary>2 MHz cycles since power on, stretch cycles included.</summary>
    [JSExport]
    public static double Cycles() => Machine.Cycles;

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

    /// <summary>
    /// A key going down, by its internal number (the value of <see cref="BbcKey"/>, column plus
    /// sixteen times row). It is played into the matrix through <see cref="BbcKeyPresses"/>, so a key
    /// that comes up at once is still held long enough for the OS to see it.
    /// </summary>
    [JSExport]
    public static void KeyDown(int key) => Keys.Down((BbcKey)key);

    /// <summary>A key coming up, by its internal number.</summary>
    [JSExport]
    public static void KeyUp(int key) => Keys.Up((BbcKey)key);

    /// <summary>The BREAK key: resets everything but the system VIA, as the machine's own does.</summary>
    [JSExport]
    public static void Break() => Machine.PressBreak();

    /// <summary>The framebuffer's width in pixels.</summary>
    [JSExport]
    public static int ScreenWidth() => Machine.Screen.Width;

    /// <summary>The framebuffer's height in rows.</summary>
    [JSExport]
    public static int ScreenHeight() => Machine.Screen.Height;

    /// <summary>Fields the CRTC has completed since power on: when it moves on, there is a new picture.</summary>
    [JSExport]
    public static double Frames() => Machine.Screen.Frames;

    /// <summary>
    /// Hands the page the picture as it stands: calls its <c>picture</c> function with a view of
    /// the framebuffer's bytes, width times height times four, R, G, B and A for each pixel. Returns
    /// the fields completed, as <see cref="Frames"/>.
    /// </summary>
    [JSExport]
    public static double Picture()
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(Machine.Screen.Pixels);
        ShowPicture(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(bytes), bytes.Length));
        return Machine.Screen.Frames;
    }

    /// <summary>
    /// Takes the sound made since the last call and, if there is any, hands it to the page's
    /// <c>sound</c> function as the bytes of that many 32-bit float samples, from 0 to 1. Returns
    /// how many samples it handed over.
    /// </summary>
    [JSExport]
    public static int Sound()
    {
        int read = Machine.Sound.Read(_samples);
        if (read > 0)
        {
            PlaySound(MemoryMarshal.AsBytes(_samples.AsSpan(0, read)));
        }

        return read;
    }

    /// <summary>Throws away the sound made so far, so it starts afresh when the page turns it on. Returns how many samples went.</summary>
    [JSExport]
    public static int DropSound() => Machine.Sound.Read(_samples);

    /// <summary>
    /// Puts a disc image in drive <paramref name="drive"/>: <paramref name="doubleSided"/> for a
    /// <c>.dsd</c> file, false for a <c>.ssd</c>, and the write-protect tab set if
    /// <paramref name="readOnly"/>. Returns its tracks a side, 40 or 80. Throws when the file is
    /// larger than a disc.
    /// </summary>
    [JSExport]
    public static int InsertDisc(int drive, byte[] data, bool doubleSided, bool readOnly)
    {
        DiscImage image = DiscImage.FromBytes(data, doubleSided);
        image.ReadOnly = readOnly;
        Machine.Insert(drive, image);
        return image.Tracks;
    }

    /// <summary>Puts a blank disc, with an empty DFS catalogue, in drive <paramref name="drive"/>.</summary>
    [JSExport]
    public static void BlankDisc(int drive, int tracks, bool doubleSided) =>
        Machine.Insert(drive, DiscImage.Blank(tracks, doubleSided));

    /// <summary>The disc in drive <paramref name="drive"/> as a file's bytes, written to as it stands, or null when the drive is empty.</summary>
    [JSExport]
    public static byte[]? SaveDisc(int drive) => Machine.Fdc.Disc(drive)?.ToBytes();

    /// <summary>Takes the disc out of drive <paramref name="drive"/>.</summary>
    [JSExport]
    public static void EjectDisc(int drive) => Machine.Eject(drive);

    /// <summary>Sets or clears the write-protect tab of the disc in drive <paramref name="drive"/>, if there is one.</summary>
    [JSExport]
    public static void SetDiscReadOnly(int drive, bool readOnly)
    {
        if (Machine.Fdc.Disc(drive) is { } disc)
        {
            disc.ReadOnly = readOnly;
        }
    }

    [JSImport("picture", "bbc-micro")]
    private static partial void ShowPicture([JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    [JSImport("sound", "bbc-micro")]
    private static partial void PlaySound([JSMarshalAs<JSType.MemoryView>] Span<byte> samples);
}
