using System.Runtime.InteropServices;
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
//       SNOW after 5 million cycles, 1.79 million a run)
//
// One untimed run first, then the runs (5 by default). Prints one line: the best, the median and
// every run, in nanoseconds a cycle. Linux only: it reads CLOCK_THREAD_CPUTIME_ID. The README
// says how to compare two builds.
string mode = args[0];
int runs = args.Length > 1 ? int.Parse(args[1]) : 5;
Action<long> run;
Func<long> cycles;
long chunk;
switch (mode)
{
    case "ntsc":
    case "pal":
    {
        var nes = new Nes(Cartridge.Load(NesTestRoms.Read("other/snow.nes")), mode == "ntsc" ? Region.Ntsc : Region.Pal);
        nes.PowerOn();
        nes.Run(5_000_000);
        run = c => nes.Run((int)c);
        cycles = () => nes.Bus.Cycles;
        chunk = 1_790_000;
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
Console.WriteLine($"{mode} ns/cycle best={ns[0]:F2} median={ns[ns.Count / 2]:F2} all={string.Join(",", ns.Select(x => x.ToString("F2")))}");

// This thread's CPU time, from clock_gettime(CLOCK_THREAD_CPUTIME_ID), which is clock 3 on Linux.
static long ThreadNs()
{
    Timespec ts;
    clock_gettime(3, out ts);
    return ts.Sec * 1_000_000_000L + ts.Nsec;
}

[DllImport("libc")] static extern int clock_gettime(int clk, out Timespec ts);
struct Timespec { public long Sec; public long Nsec; }
