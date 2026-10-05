using System.Runtime.InteropServices.JavaScript;
using Dbhq.Machines.Nes;

// JSExport exists only in the browser; declaring that keeps the platform analyser (an error here) quiet.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]

// Nothing runs at startup. The page fetches the cartridge and calls NesHost.Load.
Console.WriteLine("Dbhq.Machines.Nes.Wasm ready");

/// <summary>
/// The NES, as the page's JavaScript sees it: one machine and a call that runs it. This is the
/// first host, enough for the speed check in bench/nes-speed/; the picture, the sound and the
/// controllers join it with the tasks that build them.
/// </summary>
public static partial class NesHost
{
    private static Nes? _nes;

    private static Nes Machine => _nes ?? throw new InvalidOperationException("Load a cartridge first");

    /// <summary>
    /// Builds a console with the iNES file <paramref name="rom"/> in it, in region
    /// <paramref name="region"/> (0 NTSC, 1 PAL) and its sound made at
    /// <paramref name="sampleRate"/> samples a second, and switches it on.
    /// </summary>
    [JSExport]
    public static void Load(byte[] rom, int region, int sampleRate)
    {
        var chosen = region switch
        {
            0 => Region.Ntsc,
            1 => Region.Pal,
            _ => throw new ArgumentOutOfRangeException(nameof(region), region, "0 is NTSC and 1 is PAL"),
        };
        _nes = new Nes(Cartridge.Load(rom), chosen, new NesOptions { SampleRate = sampleRate });
        _nes.PowerOn();
    }

    /// <summary>
    /// Runs whole instructions until at least <paramref name="cycles"/> more CPU cycles have
    /// passed, and returns the cycles run since power on.
    /// </summary>
    [JSExport]
    public static double Run(int cycles)
    {
        Machine.Run(cycles);
        return Machine.Bus.Cycles;
    }

    /// <summary>The region's CPU clock in hertz, <see cref="Region.CpuHz"/>, so a page never types a rate.</summary>
    [JSExport]
    public static double CpuHz() => Machine.Region.CpuHz;

    /// <summary>CPU cycles run since power on.</summary>
    [JSExport]
    public static double Cycles() => Machine.Bus.Cycles;

    /// <summary>Frames the PPU has completed since power on.</summary>
    [JSExport]
    public static double Frames() => Machine.Bus.Ppu.Frame;
}
