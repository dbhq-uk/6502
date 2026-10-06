using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Dbhq.Machines.Nes;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page fetches the cartridge and calls NesHost.Load.
Console.WriteLine("Dbhq.Machines.Nes.Wasm ready");

/// <summary>
/// The NES, as the page's JavaScript sees it: one machine, a call that runs it, its two
/// controllers, Reset and the power switch, its picture and its sound. Everything else, including
/// when to run it and for how long, is the page's (site/public/nes.js, on the shared host
/// site/public/machine-host.js). The speed check in bench/nes-speed/ uses the same class.
/// </summary>
/// <remarks>
/// <para>
/// <b>A visitor's file is checked by the library.</b> <see cref="RegionOf"/> and
/// <see cref="Load"/> are <see cref="NesLoader"/>'s, which <c>NesLoaderTests</c> tests: a bad
/// file throws the cartridge's own sentence, which reaches the page as the error's message, and
/// the machine already running is replaced only once the new one is built, so after a bad file
/// it carries on as it was.
/// </para>
/// <para>
/// <b>The picture and the sound are handed over, not copied out</b>, as BbcHost's are. A frame
/// is 256 by 240 RGBA pixels, 240 KB, sixty times a second. <see cref="Picture"/> calls the
/// page's <c>picture</c> function, imported from the JavaScript module <c>nes</c>, with a view of
/// the frame buffer's own memory, and the page copies it once, straight into its canvas's
/// pixels; the view is good only during that call. The pixels are <c>0xAABBGGRR</c>, so in
/// memory they are R, G, B, A, the order a canvas's ImageData keeps. The sound goes the same way
/// through <c>sound</c>, as the bytes of 32-bit float samples (a view can be bytes, ints or
/// doubles, not floats). The page registers both functions before it calls <see cref="Load"/>.
/// </para>
/// <para>
/// <b>The cycle count never goes back.</b> The shared host takes each frame's cycles as the
/// difference between two readings of <see cref="Cycles"/>, so the count here is the cycles run
/// since the first <see cref="Load"/>, carried across power cycles and new cartridges, and
/// not the bus's own count, which a power cycle sets to 0.
/// </para>
/// </remarks>
public static partial class NesHost
{
    private static Nes? _nes;
    private static float[] _samples = [];

    // Cycles run by machines since replaced, and before power cycles, so Cycles never goes back.
    private static long _cyclesBefore;

    private static Nes Machine => _nes ?? throw new InvalidOperationException("Load a cartridge first");

    /// <summary>
    /// The region the file's header names, "NTSC" or "PAL", or "" when it does not say. Throws the
    /// cartridge's plain sentence when the file is not one this machine can run.
    /// </summary>
    [JSExport]
    public static string RegionOf(byte[] rom) => NesLoader.RegionOf(rom);

    /// <summary>
    /// Builds a console with the cartridge file <paramref name="rom"/> in it, in
    /// <paramref name="region"/> ("NTSC", "PAL", or "" for the header's, else NTSC), its sound made
    /// at <paramref name="sampleRate"/> samples a second, switches it on, and returns one sentence
    /// saying which region it chose and why. On a bad file it throws the cartridge's sentence and
    /// the machine running, if any, is kept.
    /// </summary>
    [JSExport]
    public static string Load(byte[] rom, string region, int sampleRate)
    {
        Nes nes = NesLoader.Load(rom, region, new NesOptions { SampleRate = sampleRate }, out string why);
        if (_nes is not null)
        {
            _cyclesBefore += _nes.Bus.Cycles;
        }

        _nes = nes;
        _samples = new float[nes.Sound.Capacity];
        return why;
    }

    /// <summary>The largest cartridge file <see cref="Load"/> takes, in bytes, so a page never types the limit.</summary>
    [JSExport]
    public static int MaxRomBytes() => Cartridge.MaxFileSize;

    /// <summary>
    /// Runs whole instructions until at least <paramref name="cycles"/> more CPU cycles have
    /// passed, and returns <see cref="Cycles"/>.
    /// </summary>
    [JSExport]
    public static double Run(int cycles)
    {
        Machine.Run(cycles);
        return Cycles();
    }

    /// <summary>
    /// Runs whole instructions until <paramref name="frames"/> more frames have completed
    /// (<see cref="Nes.RunFrames"/>), and returns the frames since power on. After
    /// <see cref="PowerCycle"/>, <c>RunFrames(n)</c> leaves the picture of frame n, the one
    /// <c>machines/nes/expected-frames.json</c> records.
    /// </summary>
    [JSExport]
    public static double RunFrames(int frames)
    {
        Machine.RunFrames(frames);
        return Frames();
    }

    /// <summary>The region's CPU clock in hertz, <see cref="Region.CpuHz"/>, so a page never types a rate.</summary>
    [JSExport]
    public static double CpuHz() => Machine.Region.CpuHz;

    /// <summary>
    /// CPU cycles run since the first <see cref="Load"/>: this machine's since power on, plus those
    /// of the machines before it and of the runs before each power cycle.
    /// </summary>
    [JSExport]
    public static double Cycles() => _cyclesBefore + Machine.Bus.Cycles;

    /// <summary>Frames the PPU has completed since power on: when it moves on, there is a new picture.</summary>
    [JSExport]
    public static double Frames() => Machine.Bus.Ppu.Frame;

    /// <summary>
    /// The buttons held on controller <paramref name="pad"/> (0 for port 1, 1 for port 2), as the
    /// mask <see cref="Nes.SetButtons"/> takes: bit 0 A, 1 B, 2 Select, 3 Start, 4 Up, 5 Down,
    /// 6 Left, 7 Right. Any mask is taken as given, opposite directions together included.
    /// </summary>
    [JSExport]
    public static void SetButtons(int pad, int mask)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mask, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mask, 0xFF);
        Machine.SetButtons(pad, (byte)mask);
    }

    /// <summary>The console's reset button: RAM and the cartridge's PRG RAM are kept.</summary>
    [JSExport]
    public static void Reset() => Machine.Reset();

    /// <summary>The power switch, off and on: RAM and the cartridge's PRG RAM are cleared, battery RAM included.</summary>
    [JSExport]
    public static void PowerCycle()
    {
        _cyclesBefore += Machine.Bus.Cycles;
        Machine.PowerOn();
    }

    /// <summary>
    /// Hands the page the picture as it stands: calls its <c>picture</c> function with a view of
    /// the frame buffer's bytes, 256 times 240 times four, R, G, B and A for each pixel. Returns
    /// the frames completed, as <see cref="Frames"/>.
    /// </summary>
    [JSExport]
    public static double Picture()
    {
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(Machine.Bus.Ppu.Screen.Pixels.AsSpan());
        ShowPicture(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(bytes), bytes.Length));
        return Frames();
    }

    /// <summary>
    /// Takes the sound made since the last call and, if there is any, hands it to the page's
    /// <c>sound</c> function as the bytes of that many 32-bit float samples. Returns how many
    /// samples it handed over.
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

    [JSImport("picture", "nes")]
    private static partial void ShowPicture([JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    [JSImport("sound", "nes")]
    private static partial void PlaySound([JSMarshalAs<JSType.MemoryView>] Span<byte> samples);
}
