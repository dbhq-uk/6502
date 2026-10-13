using Dbhq.Cpu6502.TestSupport;
using Dbhq.Machines.Kim1;

// The KIM-1 differential check, for work that must not change behaviour, such as making the core
// faster (task 17): a scripted session on the machine, with a line of hashes for each part. Two
// builds that behave the same print the same lines.
//
//   dotnet run -c Release --project bench/kim-1-differential
//
// The session: RS, the monitor idling, the "try it" program of machines/kim-1/try-it.json typed in
// and run, ST to stop it, the SST switch stepping it one instruction at a time (an NMI on every
// opcode fetch outside the monitor), and the ST key held down. Hashed after every instruction: the
// CPU's registers, its cycle count, the bus's cycle count and the NMI line. At the end of each
// part: the display, RAM and both 6530s' RAM. The keys are pressed one instruction at a time, so
// every key lands on the same instruction in both builds. The ROMs are the committed ones,
// checked against their hashes.
var machine = new Kim1Machine(
    RepoPaths.ReadChecked(Pins.Kim1Rom002Path, Pins.Kim1Rom002Sha256),
    RepoPaths.ReadChecked(Pins.Kim1Rom003Path, Pins.Kim1Rom003Sha256));
var session = new Session(machine);

session.Keys("[RS]");
session.Run(500_000);
session.Print("reset");

// The "try it" program: the NMI vector at the monitor, LDA $0210, CLC, ADC $0211, STA $0212,
// JMP $020A, the two numbers, and GO.
session.Keys("[AD] 17FA [DA] 00 [+] 1C");
session.Keys("[AD] 0200 [DA] AD [+] 10 [+] 02 [+] 18 [+] 6D [+] 11 [+] 02 [+] 8D [+] 12 [+] 02 [+] 4C [+] 0A [+] 02");
session.Keys("[AD] 0210 [DA] 27 [+] 15");
session.Print("typed");
session.Keys("[AD] 0200 [GO]");
session.Run(2_000_000);
session.Print("running");
session.Keys("[ST]");
session.Keys("[AD] 0212");
session.Run(200_000);
session.Print("stopped");

// SST on: GO runs the program one instruction at a time, each step an NMI back into the monitor.
machine.SingleStep = true;
session.Keys("[AD] 0200 [GO] [GO] [GO] [GO] [GO] [GO] [GO] [GO]");
session.Run(300_000);
session.Print("single steps");
machine.SingleStep = false;

// ST held, with the program running: the NMI line stays active, so its edge comes once.
session.Keys("[GO]");
session.Run(100_000);
machine.StopKey = true;
session.Run(100_000);
machine.StopKey = false;
session.Run(200_000);
session.Print("stop held");

// One instruction at a time, with the keys pressed and released on the instructions they fall due.
internal sealed class Session(Kim1Machine machine)
{
    private readonly Kim1Keystrokes _keys = new(machine);
    private ulong _trace = 0xCBF29CE484222325;
    private long _instructions;

    public void Keys(string keys)
    {
        _keys.Type(keys);
        while (!_keys.Idle)
        {
            Step();
        }
    }

    public void Run(long cycles)
    {
        long end = machine.Cycles + cycles;
        while (machine.Cycles < end)
        {
            Step();
        }
    }

    public void Print(string part)
    {
        ulong memory = 0xCBF29CE484222325;
        for (int address = 0; address < 0x2000; address++)
        {
            memory = Add(memory, machine.Bus.Peek((ushort)address));
        }

        Console.WriteLine($"{part} instructions={_instructions} cycles={machine.Cycles} cpu_cycles={machine.Cpu.Cycles} trace={_trace:X16} memory={memory:X16} display=\"{machine.ReadDisplay()}\"");
    }

    private void Step()
    {
        // Kim1Keystrokes.Run(1) runs one instruction (at least one cycle), then presses or
        // releases any key that has fallen due.
        _keys.Run(1);
        _instructions++;
        var cpu = machine.Cpu;
        _trace = Add(_trace, (ulong)cpu.PC | ((ulong)cpu.A << 16) | ((ulong)cpu.X << 24) | ((ulong)cpu.Y << 32) | ((ulong)cpu.P << 40) | ((ulong)cpu.S << 48) | (cpu.Nmi ? 1UL << 56 : 0));
        _trace = Add(_trace, (ulong)cpu.Cycles ^ ((ulong)machine.Cycles << 32));
    }

    private static ulong Add(ulong hash, ulong value) => (hash ^ value) * 0x100000001B3;
}
