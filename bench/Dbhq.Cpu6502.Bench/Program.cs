using System.Diagnostics;
using Dbhq.Cpu6502;
using Dbhq.Cpu6502.TestSupport;

// How fast the core runs on its own, measured on Dormann's functional test.
// A local benchmark, not a CI check: CI runners vary too much to fail a build
// on. .NET compiles hot code in stages while it runs, so the first run is
// slow; the best of five is reported.
const double BbcMicroHz = 2_000_000;
const double TargetMultiple = 25;

Dormann.Program program = Dormann.Functional(CpuVariant.Nmos6502);
double best = 0;
for (int run = 1; run <= 5; run++)
{
    var bus = new FlatBus { Recording = false };
    program.Memory.CopyTo(bus.Memory, 0);
    var cpu = new Cpu(bus, CpuVariant.Nmos6502) { PC = program.Start, S = 0xFF, P = 0x24 };
    var clock = Stopwatch.StartNew();
    Dormann.RunToTrap(cpu);
    clock.Stop();
    double hz = cpu.Cycles / clock.Elapsed.TotalSeconds;
    best = Math.Max(best, hz);
    Console.WriteLine($"run {run}: {cpu.Cycles:N0} cycles in {clock.Elapsed.TotalSeconds:F2} s, {hz / 1e6:F1} MHz");
}

double multiple = best / BbcMicroHz;
Console.WriteLine($"best {best / 1e6:F1} MHz: {multiple:F0} times a 2 MHz BBC Micro, against a target of {TargetMultiple:F0}");
return multiple >= TargetMultiple ? 0 : 1;
