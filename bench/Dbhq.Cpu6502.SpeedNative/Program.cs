using Dbhq.Cpu6502;
using Dbhq.Cpu6502.Bench;

// The native half of the browser speed check: the same workload as the browser
// app, on the same core, run as an ordinary .NET program. Prints the same two
// lines the page prints.
//
//   dotnet run -c Release --project bench/Dbhq.Cpu6502.SpeedNative -- [measured cycles] [warm-up cycles]
//   dotnet run -c Release --project bench/Dbhq.Cpu6502.SpeedNative -- --listing
if (args.Length == 1 && args[0] == "--listing")
{
    Listing();
    return;
}

long cycles = args.Length > 0 ? long.Parse(args[0]) : 100_000_000;
long warmup = args.Length > 1 ? long.Parse(args[1]) : 5_000_000;

Console.WriteLine("warmup " + Workload.Run(warmup));
Console.WriteLine("measured " + Workload.Run(cycles));
return;

// Checks that the bytes read back as the program the comments describe: the
// disassembly, then how long the branches and the two indexed accesses took
// over 512 laps of the loop (two full rounds of X).
static void Listing()
{
    var memory = new byte[0x10000];
    Workload.Bytes.CopyTo(memory.AsSpan(Workload.ProgramStart));
    var bus = new ArrayBus(memory);

    ushort address = Workload.ProgramStart;
    ushort end = (ushort)(Workload.ProgramStart + Workload.Bytes.Length);
    while (address < end)
    {
        string text = Disassembler.Disassemble(CpuVariant.Nmos6502, a => memory[a], address);
        Console.WriteLine($"{address:X4}  {text}");
        address += (ushort)OpcodeTable.Get(CpuVariant.Nmos6502, memory[address]).Length;
    }

    var cpu = new Cpu(bus, CpuVariant.Nmos6502) { PC = Workload.ProgramStart, S = 0xFF, P = 0x24 };
    ushort[] watched = [0x1FF3, 0x1FF6, 0x2007, 0x2009, 0x200D];
    var tally = new SortedDictionary<(ushort Address, int Cycles), int>();
    int laps = 0;
    while (laps < 512)
    {
        ushort pc = cpu.PC;
        int cycles = cpu.Step();
        if (Array.IndexOf(watched, pc) >= 0)
        {
            tally[(pc, cycles)] = tally.GetValueOrDefault((pc, cycles)) + 1;
        }

        if (pc == 0x200D || pc == 0x2012)
        {
            laps++;
        }
    }

    Console.WriteLine("laps=512 (instruction address, cycles taken, times seen)");
    foreach (var entry in tally)
    {
        Console.WriteLine($"{entry.Key.Address:X4} cycles={entry.Key.Cycles} count={entry.Value}");
    }
}

file sealed class ArrayBus(byte[] memory) : Bus
{
    public override byte Read(ushort address) => memory[address];

    public override void Write(ushort address, byte value) => memory[address] = value;
}
