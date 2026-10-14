using System.Runtime.InteropServices.JavaScript;
using Dbhq.Machines.Atari2600;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The caller fetches the cartridge and calls Atari2600Host.Load.
Console.WriteLine("Dbhq.Machines.Atari2600.Wasm ready");

/// <summary>
/// The Atari 2600, as JavaScript sees it: one machine and the calls that run it. This is the first
/// host, task 8's, for the browser speed check in bench/atari-2600-speed/; task 13 adds what the
/// page needs (the picture, the sound, the joysticks, the switches and the region switch).
/// </summary>
/// <remarks>
/// The cycle count never goes back: <see cref="Cycles"/> is the cycles run since the first
/// <see cref="Load"/>, carried across new cartridges, as <c>NesHost</c>'s is, so a caller can take
/// the difference of two readings.
/// </remarks>
public static partial class Atari2600Host
{
    private static Atari2600? _machine;

    // Cycles run by machines since replaced, so Cycles never goes back.
    private static long _cyclesBefore;

    private static Atari2600 Machine => _machine ?? throw new InvalidOperationException("Load a cartridge first");

    /// <summary>
    /// Builds a console with the cartridge file <paramref name="rom"/> in it, in
    /// <paramref name="region"/> ("NTSC" or "PAL"), with <paramref name="sampleRate"/> for the
    /// sound to come (task 10), switches it on, and returns one sentence naming the board it chose.
    /// A file no modelled board takes throws the cartridge's own sentence, and the machine running,
    /// if any, is kept.
    /// </summary>
    [JSExport]
    public static string Load(byte[] rom, string region, int sampleRate)
    {
        Region chosen = region switch
        {
            "NTSC" => Region.Ntsc,
            "PAL" => Region.Pal,
            _ => throw new ArgumentException($"The region is \"NTSC\" or \"PAL\", not \"{region}\".", nameof(region)),
        };
        Cartridge cartridge = Cartridge.Load(rom);
        var machine = new Atari2600(cartridge, chosen, new Atari2600Options { SampleRate = sampleRate });
        machine.PowerOn();
        if (_machine is not null)
        {
            _cyclesBefore += _machine.Bus.Cycles;
        }

        _machine = machine;
        return $"The cartridge runs as {cartridge.BoardName}, in {chosen.Name}.";
    }

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

    /// <summary>Runs whole instructions until <paramref name="frames"/> more frames have ended, and returns <see cref="Frames"/>.</summary>
    [JSExport]
    public static double RunFrames(int frames)
    {
        Machine.RunFrames(frames);
        return Frames();
    }

    /// <summary>CPU cycles run since the first <see cref="Load"/>, WSYNC halt cycles included.</summary>
    [JSExport]
    public static double Cycles() => _cyclesBefore + Machine.Bus.Cycles;

    /// <summary>Frames the TIA has completed since this machine's power on.</summary>
    [JSExport]
    public static double Frames() => Machine.Bus.Tia.Frame.Frames;

    /// <summary>The lines the last frame had (<see cref="FrameBuffer.Lines"/>).</summary>
    [JSExport]
    public static int Lines() => Machine.Bus.Tia.Frame.Lines;

    /// <summary>The region's CPU clock in hertz, <see cref="Region.CpuHz"/>, so a caller never types a rate.</summary>
    [JSExport]
    public static double CpuHz() => Machine.Bus.Region.CpuHz;

    /// <summary>The lines a frame of the region's games has, <see cref="Region.NominalLines"/>.</summary>
    [JSExport]
    public static int NominalLines() => Machine.Bus.Region.NominalLines;
}
