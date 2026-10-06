using Dbhq.Cpu6502;

// The interrupt differential, for work on the core that must not change behaviour: random memory
// run as code on every CPU variant, with the IRQ and NMI lines toggled at random inside bus
// accesses (so on every cycle of an instruction, its last and second to last included), the lines
// also changed and the CPU reset now and then between steps. It hashes every bus access and the
// CPU's state after every step. Two builds that behave the same print the same lines.
//
//   dotnet run -c Release --project bench/interrupt-differential -- <steps>
//
// Each of the 60 configurations (5 variants, 4 rates of line changes, 3 seeds) runs <steps> steps
// and prints a line; the last line is a hash of them all. Written by the review of task 17 (the
// core's speed), because the poll there relies on a rule no test names for a future instruction:
// one that changes the I flag after its last bus access must call FreezePoll first. The README
// says how to run it against an older commit.
//
// The bus implements IBus, not Bus, so that the same file builds on commits from before Bus
// existed; the CPU calls it through its wrapper, which does what a Bus does.
long steps = long.Parse(args[0]);
ulong total = 0xCBF29CE484222325;
foreach (CpuVariant variant in Enum.GetValues<CpuVariant>())
foreach (int profile in new[] { 2, 8, 64, 1000 })
foreach (int seed in new[] { 1, 2, 3 })
{
    var rng = new Random(seed * 7919 + profile * 31 + (int)variant);
    var bus = new RandBus(rng, profile);
    var cpu = new Cpu(bus, variant);
    bus.Cpu = cpu;
    cpu.Reset();
    ulong h = 0xCBF29CE484222325;
    int interrupts = 0, jams = 0, waits = 0, stops = 0, zero = 0;
    for (long i = 0; i < steps; i++)
    {
        if (rng.Next(4000) == 0) { cpu.Irq = rng.Next(2) == 0; }
        if (rng.Next(4000) == 0) { cpu.Nmi = rng.Next(2) == 0; }
        if (rng.Next(200_000) == 0 || ((cpu.IsJammed || cpu.IsStopped) && rng.Next(20) == 0))
        {
            if (cpu.IsJammed) jams++;
            if (cpu.IsStopped) stops++;
            cpu.Reset();
        }
        if (cpu.IsWaiting) waits++;
        long pcBefore = cpu.PC;
        int n = cpu.Step();
        if (n == 0) { zero++; bus.Tick(); }
        if (n == 7 && cpu.PC != pcBefore) interrupts++;
        h = Add(h, (ulong)(uint)n | ((ulong)cpu.PC << 8) | ((ulong)cpu.A << 24) | ((ulong)cpu.X << 32) | ((ulong)cpu.Y << 40) | ((ulong)cpu.S << 48) | ((ulong)cpu.P << 56));
        h = Add(h, (ulong)cpu.Cycles ^ ((cpu.IsJammed ? 1UL : 0) << 61) ^ ((cpu.IsWaiting ? 1UL : 0) << 62) ^ ((cpu.IsStopped ? 1UL : 0) << 63));
    }
    h = Add(h, bus.Hash);
    total = Add(total, h);
    Console.WriteLine($"{variant} p={profile} seed={seed} cycles={cpu.Cycles} hash={h:X16} sevens={interrupts} jams={jams} stops={stops} waitsteps={waits} zero={zero}");
}
Console.WriteLine($"total {total:X16}");

static ulong Add(ulong hash, ulong value) => (hash ^ value) * 0x100000001B3;

sealed class RandBus : IBus
{
    private readonly byte[] _m = new byte[0x10000];
    private readonly Random _rng;
    private readonly int _p;
    public Cpu? Cpu;
    public ulong Hash = 0xCBF29CE484222325;
    public RandBus(Random rng, int p) { _rng = rng; _p = p; rng.NextBytes(_m); }
    public void Tick()
    {
        if (_rng.Next(_p) == 0) Cpu!.Irq = !Cpu.Irq;
        if (_rng.Next(_p * 2) == 0) Cpu!.Nmi = !Cpu.Nmi;
    }
    public byte Read(ushort a)
    {
        byte v = _m[a];
        Hash = (Hash ^ (a | (ulong)v << 16)) * 0x100000001B3;
        Tick();
        // Sometimes rewrite memory under the CPU so it does not settle into a short loop.
        if (_rng.Next(64) == 0) _m[_rng.Next(0x10000)] = (byte)_rng.Next(256);
        return v;
    }
    public void Write(ushort a, byte v)
    {
        _m[a] = v;
        Hash = (Hash ^ (a | (ulong)v << 16 | 1UL << 24)) * 0x100000001B3;
        Tick();
    }
}
