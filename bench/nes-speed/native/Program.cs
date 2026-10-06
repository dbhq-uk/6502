using System.Diagnostics;
using Dbhq.Cpu6502.TestSupport;
using Dbhq.Machines.Nes;

// The native half of the NES speed check: the browser page's workload, run as an ordinary .NET
// program. The ROM is SNOW, from the pinned fork (NesTestRoms.Read checks its hash), never committed.
//
//   dotnet run -c Release --project bench/nes-speed/native -- [timed runs] [cycles a run] [region] [boot cycles]
//       region: ntsc (default) or pal
//
// The machine is powered on, run for the boot cycles (five million by default, about three
// seconds of machine time), then timed over the runs. Each timed run is the cycles given (1.79
// million by default, about one second of machine time on NTSC). Every line has the form the
// page prints, and ends with the multiple of real time: the cycles a second divided by the
// region's own CPU clock (Region.CpuHz), which is never typed here.
const string RomName = "other/snow.nes";

int runs = args.Length > 0 ? int.Parse(args[0]) : 5;
int cycles = args.Length > 1 ? int.Parse(args[1]) : 1_790_000;
string regionName = args.Length > 2 ? args[2] : "ntsc";
int bootCycles = args.Length > 3 ? int.Parse(args[3]) : 5_000_000;
Region region = regionName switch
{
    "ntsc" => Region.Ntsc,
    "pal" => Region.Pal,
    _ => throw new ArgumentException($"region is ntsc or pal, not {regionName}"),
};

var nes = new Nes(Cartridge.Load(NesTestRoms.Read(RomName)), region);
Console.WriteLine($"region {region.Name} cpu_hz={region.CpuHz:F0}");
var clock = Stopwatch.StartNew();
nes.PowerOn();
nes.Run(bootCycles);
Report("boot", nes.Bus.Cycles, nes.Bus.Ppu.Frame, clock.Elapsed.TotalMilliseconds);

// The check that the timed runs are a ROM that draws: rendering is on, and the picture changes
// between frames. Without it a ROM that had stopped drawing would time the PPU's idle path.
var ppu = nes.Bus.Ppu;
ulong before = Hash(ppu.Screen.Pixels);
nes.Run(Math.Max(1, (int)(region.CpuHz / region.FramesPerSecond) + 1));
ulong after = Hash(ppu.Screen.Pixels);
Console.WriteLine($"rendering {(ppu.RenderingEnabled ? "yes" : "NO")}");
Console.WriteLine($"picture changes {(before != after ? "yes" : "NO")}");

for (int i = 1; i <= runs; i++)
{
    long start = nes.Bus.Cycles;
    long startFrames = ppu.Frame;
    clock.Restart();
    nes.Run(cycles);
    double ms = clock.Elapsed.TotalMilliseconds;
    Report($"timed {i}", nes.Bus.Cycles - start, ppu.Frame - startFrames, ms, region);
}

return;

static void Report(string label, long ran, long frames, double ms, Region? region = null)
{
    double perSecond = ran / (ms / 1000);
    string times = region is null ? "" : $" times_real={perSecond / region.CpuHz:F2}";
    Console.WriteLine($"{label} cycles={ran} frames={frames} ms={ms:F3} cycles_per_second={perSecond:F0} mhz={perSecond / 1e6:F3}{times}");
}

static ulong Hash(uint[] pixels)
{
    ulong hash = 14695981039346656037UL;
    foreach (uint pixel in pixels)
    {
        hash = (hash ^ pixel) * 1099511628211UL;
    }

    return hash;
}
