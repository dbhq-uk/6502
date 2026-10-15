using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using Dbhq.Machines.Atari2600;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page fetches the cartridge and calls Atari2600Host.Load.
Console.WriteLine("Dbhq.Machines.Atari2600.Wasm ready");

/// <summary>
/// The Atari 2600, as the page's JavaScript sees it: one machine, a call that runs it, its two
/// joysticks and its console switches, the region switch, the power switch, its picture and its
/// sound. Everything else, including when to run it and for how long, is the page's
/// (site/public/atari-2600.js, on the shared host site/public/machine-host.js). The speed check in
/// bench/atari-2600-speed/ uses the same class.
/// </summary>
/// <remarks>
/// <para>
/// <b>A visitor's file is checked by the library.</b> <see cref="Load"/> reads it with
/// <see cref="Cartridge.Load"/>, which <c>CartridgeTests</c> tests: a file no modelled board takes
/// throws the cartridge's own sentence, which reaches the page as the error's message, before the
/// machine running is touched, so after a bad file it carries on as it was.
/// </para>
/// <para>
/// <b>The picture and the sound are handed over, not copied out</b>, as NesHost's are.
/// <see cref="Picture"/> fills the region's window of the last frame through its palette
/// (<see cref="FrameBuffer.ToRgba"/>), 160 pixels by the window's lines, into a buffer made once,
/// and calls the page's <c>picture</c> function, imported from the JavaScript module
/// <c>atari2600</c>, with a view of it; the page copies it once, straight into its canvas's
/// pixels, and the view is good only during that call. The pixels are <c>0xAABBGGRR</c>, so in
/// memory they are R, G, B, A, the order a canvas's ImageData keeps. The sound goes the same way
/// through <c>sound</c>, as the bytes of 32-bit float samples. The page registers both functions
/// before it calls <see cref="Load"/>.
/// </para>
/// <para>
/// <b>The region switches at once</b> (<see cref="SetRegion"/>): the palette and the clock change
/// and the machine runs on, with no restart, because region.md section 5 found nothing else a
/// program can see differ.
/// </para>
/// <para>
/// <b>The cycle count never goes back.</b> The shared host takes each frame's cycles as the
/// difference between two readings of <see cref="Cycles"/>, so the count here is the cycles run
/// since the first <see cref="Load"/>, carried across power cycles and new cartridges, and not the
/// bus's own count, which a power on sets to 0.
/// </para>
/// </remarks>
public static partial class Atari2600Host
{
    private static Atari2600? _machine;
    private static float[] _samples = [];

    // The window of the larger region, made once: Picture fills the current region's part of it.
    private static readonly uint[] Window = new uint[FrameBuffer.Width * Math.Max(Dbhq.Machines.Atari2600.Region.Ntsc.ShownLines, Dbhq.Machines.Atari2600.Region.Pal.ShownLines)];

    // Cycles run by machines since replaced, and before power cycles, so Cycles never goes back.
    private static long _cyclesBefore;

    private static Atari2600 Machine => _machine ?? throw new InvalidOperationException("Load a cartridge first");

    /// <summary>
    /// Builds a console with the cartridge file <paramref name="rom"/> in it, in
    /// <paramref name="region"/> ("NTSC" or "PAL"), its sound made at <paramref name="sampleRate"/>
    /// samples a second, switches it on, and returns one sentence naming the board it chose. A file
    /// no modelled board takes throws the cartridge's own sentence, and the machine running, if
    /// any, is kept.
    /// </summary>
    [JSExport]
    public static string Load(byte[] rom, string region, int sampleRate)
    {
        Region chosen = RegionNamed(region);
        Cartridge cartridge = Cartridge.Load(rom);
        var machine = new Atari2600(cartridge, chosen, new Atari2600Options { SampleRate = sampleRate });
        machine.PowerOn();
        if (_machine is not null)
        {
            _cyclesBefore += _machine.Bus.Cycles;
        }

        _machine = machine;
        _samples = new float[machine.Sound.Capacity];
        return $"The board is {cartridge.BoardName}, chosen from the file.";
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
    /// Runs whole instructions until <paramref name="frames"/> more frames have ended, and returns
    /// <see cref="Frames"/>. After <see cref="PowerCycle"/>, <c>RunFrames(n)</c> leaves the picture
    /// of frame n, the one <c>machines/atari-2600/expected-frames.json</c> records.
    /// </summary>
    [JSExport]
    public static double RunFrames(int frames)
    {
        Machine.RunFrames(frames);
        return Frames();
    }

    /// <summary>CPU cycles run since the first <see cref="Load"/>, WSYNC halt cycles included.</summary>
    [JSExport]
    public static double Cycles() => _cyclesBefore + Machine.Bus.Cycles;

    /// <summary>Frames the TIA has completed since this machine's power on: when it moves on, there is a new picture.</summary>
    [JSExport]
    public static double Frames() => Machine.Bus.Tia.Frame.Frames;

    /// <summary>The lines the last frame had (<see cref="FrameBuffer.Lines"/>), for the page's line count and its PAL suggestion.</summary>
    [JSExport]
    public static int Lines() => Machine.Bus.Tia.Frame.Lines;

    /// <summary>The region the machine runs in, "NTSC" or "PAL", which the page reads back rather than keeping its own.</summary>
    [JSExport]
    public static string Region() => Machine.Bus.Region.Name;

    /// <summary>
    /// Switches the region to <paramref name="region"/> ("NTSC" or "PAL") at once: the palette and
    /// the clock change and the machine runs on with no restart (region.md section 5).
    /// </summary>
    [JSExport]
    public static void SetRegion(string region) => Machine.Bus.Region = RegionNamed(region);

    /// <summary>The region's CPU clock in hertz, <see cref="Region.CpuHz"/>, so a page never types a rate.</summary>
    [JSExport]
    public static double CpuHz() => Machine.Bus.Region.CpuHz;

    /// <summary>The lines a frame of the region's games has, <see cref="Region.NominalLines"/>.</summary>
    [JSExport]
    public static int NominalLines() => Machine.Bus.Region.NominalLines;

    /// <summary>
    /// Holds the joystick in port <paramref name="stick"/>, 0 the left and 1 the right, as
    /// <paramref name="state"/>, the bits of <see cref="Joystick"/>: 1 up, 2 down, 4 left, 8 right,
    /// 16 fire. Any combination is taken as given, opposite directions together included.
    /// </summary>
    [JSExport]
    public static void SetJoystick(int stick, int state) => Machine.SetJoystick(stick, (Joystick)state);

    /// <summary>
    /// Sets the console switches as <paramref name="state"/>, the bits of
    /// <see cref="ConsoleSwitches"/>: 1 Reset and 2 Select, pressed while set; 4 black and white,
    /// 8 the left difficulty at A and 16 the right, which stay as set.
    /// </summary>
    [JSExport]
    public static void SetSwitches(int state) => Machine.SetSwitches((ConsoleSwitches)state);

    /// <summary>
    /// The power switch, off and on: the chips go to their power-on state and the game starts from
    /// the beginning, in the same region, with the joysticks and the switches as they were.
    /// </summary>
    [JSExport]
    public static void PowerCycle()
    {
        _cyclesBefore += Machine.Bus.Cycles;
        Machine.PowerOn();
    }

    /// <summary>
    /// Hands the page the picture as it stands: the region's window of the last frame, 160 times
    /// the window's lines times four bytes, R, G, B and A for each pixel, through its
    /// <c>picture</c> function. Returns the frames completed, as <see cref="Frames"/>.
    /// </summary>
    [JSExport]
    public static double Picture()
    {
        Region region = Machine.Bus.Region;
        Span<uint> window = Window.AsSpan(0, FrameBuffer.Width * region.ShownLines);
        Machine.Bus.Tia.Frame.ToRgba(region, window);
        ShowPicture(MemoryMarshal.AsBytes(window));
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

    private static Region RegionNamed(string region) => region switch
    {
        "NTSC" => Dbhq.Machines.Atari2600.Region.Ntsc,
        "PAL" => Dbhq.Machines.Atari2600.Region.Pal,
        _ => throw new ArgumentException($"The region is \"NTSC\" or \"PAL\", not \"{region}\".", nameof(region)),
    };

    [JSImport("picture", "atari2600")]
    private static partial void ShowPicture([JSMarshalAs<JSType.MemoryView>] Span<byte> pixels);

    [JSImport("sound", "atari2600")]
    private static partial void PlaySound([JSMarshalAs<JSType.MemoryView>] Span<byte> samples);
}
