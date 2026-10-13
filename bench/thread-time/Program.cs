using System.Runtime.InteropServices;
using Dbhq.Bench;
using Dbhq.Cpu6502;
using Dbhq.Cpu6502.TestSupport;
using Dbhq.Machines.Nes;
using Dbhq.Machines.BbcMicro;
using Dbhq.Machines.Kim1;

// Thread CPU time a cycle: how many nanoseconds of this thread's own CPU time one emulated cycle
// costs, for the core alone and for each machine. On a shared machine the wall clock counts the
// time other work held the processor; the thread's CPU time does not (task 17, the core's speed).
//
//   dotnet run -c Release --project bench/thread-time -- <workload> [runs]
//       workload: core (Dormann's functional test on a flat bus, NMOS, run to its trap each run),
//       bbc (the BBC Micro at the prompt after 6 million cycles, 4 million a run), kim (the KIM-1
//       monitor after RS and 2 million cycles, 10 million a run), ntsc or pal (the NES running
//       SNOW after 5 million cycles, 1.79 million a run), ntsc-oracle or pal-oracle (the same with
//       NesOptions.PerDotReference, the PPU caught up every cycle as the per-dot build ran it),
//       lan-ntsc or lan-pal (the NES running the bundled homebrew, Lan Master, at its title,
//       after 5 million cycles, 1.79 million a run), play-ntsc or play-pal (the same game played by
//       bench/nes-speed/lan-master-play.json: 6 million cycles to get into the first level, then 14
//       million cycles a run, in whole frames, the pad set before each; it prints the PPU frame it
//       ended on beside the script's last frame of play for the region, which it must not pass)
//
// One untimed run first, then the runs (5 by default). Prints one line: the best, the median and
// every run, in nanoseconds a cycle. Linux only: it reads CLOCK_THREAD_CPUTIME_ID. The README
// says how to compare two builds.
string mode = args[0];
int runs = args.Length > 1 ? int.Parse(args[1]) : 5;
Action<long> run;
PlayScript playScript = null!;
Dbhq.Machines.Nes.Ppu playPpu = null!;
Func<long> cycles;
long chunk;
switch (mode)
{
    case "ntsc":
    case "pal":
    case "ntsc-oracle":
    case "pal-oracle":
    case "lan-ntsc":
    case "lan-pal":
    {
        byte[] rom = mode.StartsWith("lan", StringComparison.Ordinal)
            ? RepoPaths.ReadChecked(Pins.NesHomebrewPath, Pins.NesHomebrewSha256)
            : NesTestRoms.Read("other/snow.nes");
        Region region = mode.Contains("ntsc", StringComparison.Ordinal) ? Region.Ntsc : Region.Pal;
        var nes = new Nes(Cartridge.Load(rom), region, NesOptionsFor(perDot: mode.EndsWith("-oracle", StringComparison.Ordinal)));
        nes.PowerOn();
        nes.Run(5_000_000);
        run = c => nes.Run((int)c);
        cycles = () => nes.Bus.Cycles;
        chunk = 1_790_000;
        break;
    }
    case "play-ntsc":
    case "play-pal":
    {
        byte[] rom = RepoPaths.ReadChecked(Pins.NesHomebrewPath, Pins.NesHomebrewSha256);
        Region region = mode == "play-ntsc" ? Region.Ntsc : Region.Pal;
        playScript = PlayScript.Load(Path.Combine(RepoPaths.Root, "bench", "nes-speed", "lan-master-play.json"), region.Name);
        var nes = new Nes(Cartridge.Load(rom), region, NesOptionsFor(perDot: false));
        nes.PowerOn();
        playPpu = nes.Bus.Ppu;
        // Whole frames, the pad set before each, until at least this many more cycles have run.
        void Play(long c)
        {
            long end = nes.Bus.Cycles + c;
            while (nes.Bus.Cycles < end)
            {
                nes.SetButtons(0, playScript.MaskAt(playPpu.Frame));
                nes.RunFrames(1);
            }
        }

        Play(6_000_000);
        run = Play;
        cycles = () => nes.Bus.Cycles;
        chunk = 14_000_000;
        break;
    }
    case "bbc":
    {
        var roms = new BbcRoms(
            RepoPaths.ReadChecked(Pins.BbcOsPath, Pins.BbcOsSha256),
            RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256),
            RepoPaths.ReadChecked(Pins.BbcDfsPath, Pins.BbcDfsSha256));
        var m = new BbcMachine(roms, new BbcOptions { StartupMode = 7 });
        m.PowerOn();
        m.Run(6_000_000);
        run = c => m.Run(c);
        cycles = () => m.Cycles;
        chunk = 4_000_000;
        break;
    }
    case "kim":
    {
        var m = new Kim1Machine(RepoPaths.ReadChecked(Pins.Kim1Rom002Path, Pins.Kim1Rom002Sha256), RepoPaths.ReadChecked(Pins.Kim1Rom003Path, Pins.Kim1Rom003Sha256));
        m.PressReset();
        m.Run(2_000_000);
        run = c => m.Run(c);
        cycles = () => m.Cycles;
        chunk = 10_000_000;
        break;
    }
    case "core":
    {
        Dormann.Program program = Dormann.Functional(CpuVariant.Nmos6502);
        var bus = new FlatBus { Recording = false };
        Cpu? cpu = null;
        void Fresh() { program.Memory.CopyTo(bus.Memory, 0); cpu = new Cpu(bus, CpuVariant.Nmos6502) { PC = program.Start, S = 0xFF, P = 0x24 }; }
        Fresh();
        long total = 0;
        run = c => { Fresh(); Dormann.RunToTrap(cpu!); total += cpu!.Cycles; };
        cycles = () => total;
        chunk = 0;
        break;
    }
    default: throw new ArgumentException(mode);
}

run(chunk); // warm
var ns = new List<double>();
for (int i = 0; i < runs; i++)
{
    long c0 = cycles();
    long t0 = ThreadNs();
    run(chunk);
    long t1 = ThreadNs();
    ns.Add((double)(t1 - t0) / (cycles() - c0));
}
ns.Sort();
string played = playScript is null ? "" : $" frame={playPpu.Frame} limit={playScript.Limit}{(playPpu.Frame > playScript.Limit ? " PAST THE SCRIPT" : "")}";
Console.WriteLine($"{mode} ns/cycle best={ns[0]:F2} median={ns[ns.Count / 2]:F2} all={string.Join(",", ns.Select(x => x.ToString("F2")))}{played}");

// The options, with NesOptions.PerDotReference set for the per-dot reference. Set by reflection
// so this folder still builds when it is copied into an export of a commit from before the option
// (5e48505, the lazy chips' baseline), as the README's comparison does; there the -oracle
// workloads stop with a message and the rest run.
static NesOptions NesOptionsFor(bool perDot)
{
    var options = new NesOptions();
    if (perDot)
    {
        System.Reflection.PropertyInfo option = typeof(NesOptions).GetProperty("PerDotReference")
            ?? throw new InvalidOperationException("this build has no NesOptions.PerDotReference, so no per-dot reference to run");
        option.SetValue(options, true);
    }

    return options;
}

// This thread's CPU time, from clock_gettime(CLOCK_THREAD_CPUTIME_ID), which is clock 3 on Linux.
static long ThreadNs()
{
    Timespec ts;
    clock_gettime(3, out ts);
    return ts.Sec * 1_000_000_000L + ts.Nsec;
}

[DllImport("libc")] static extern int clock_gettime(int clk, out Timespec ts);
struct Timespec { public long Sec; public long Nsec; }
