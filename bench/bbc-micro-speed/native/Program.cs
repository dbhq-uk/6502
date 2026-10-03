using System.Diagnostics;
using System.Text;
using Dbhq.Cpu6502.TestSupport;
using Dbhq.Machines.BbcMicro;

// The native half of the BBC Micro speed check: the browser page's workload, run as an
// ordinary .NET program, and a fingerprint of a scripted run, so a change to the bus or its
// chips can be shown to change nothing the machine does.
//
//   dotnet run -c Release --project bench/bbc-micro-speed/native -- [timed runs] [cycles a run] [screen mode] [screen]
//       screen: boot (default, the boot screen as the OS leaves it), dense (mode 7 screen memory
//       filled with random bytes, every control code included) or text (random printable
//       characters), filled after the prompt check, with a fixed seed so every build gets the same page
//   dotnet run -c Release --project bench/bbc-micro-speed/native -- --fingerprint
//   dotnet run -c Release --project bench/bbc-micro-speed/native -- --profile [rounds]
var roms = new BbcRoms(
    RepoPaths.ReadChecked(Pins.BbcOsPath, Pins.BbcOsSha256),
    RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256),
    RepoPaths.ReadChecked(Pins.BbcDfsPath, Pins.BbcDfsSha256));

if (args.Length == 1 && args[0] == "--fingerprint")
{
    Fingerprint.Run(roms);
    return;
}

if (args.Length >= 1 && args[0] == "--profile")
{
    Profile.Run(roms, args.Length > 1 ? int.Parse(args[1]) : 7);
    return;
}

int runs = args.Length > 0 ? int.Parse(args[0]) : 5;
int cycles = args.Length > 1 ? int.Parse(args[1]) : 2_000_000;
int mode = args.Length > 2 ? int.Parse(args[2]) : 7;
string screen = args.Length > 3 ? args[3] : "boot";
Speed(roms, runs, cycles, mode, screen);
return;

// The page's workload: boot for three seconds of machine time, check the prompt, then time
// the runs. Prints the lines the page prints. Outside mode 7 the screen is pixels, so the
// prompt check is the OS's own record, as on the page: the mode at &0355 and the text cursor
// at &0318 and &0319, one column right of the > on row 5.
static void Speed(BbcRoms roms, int runs, int cycles, int mode, string screen)
{
    var machine = new BbcMachine(roms, new BbcOptions { StartupMode = mode });
    var clock = Stopwatch.StartNew();
    machine.PowerOn();
    machine.Run(6_000_000);
    Report("boot", machine.Cycles, clock.Elapsed.TotalMilliseconds);

    bool prompt = mode == 7
        ? Row(machine, 1).StartsWith("BBC Computer 32K", StringComparison.Ordinal)
            && Row(machine, 3).StartsWith("BASIC", StringComparison.Ordinal)
            && Row(machine, 5).StartsWith('>')
        : machine.Bus.Peek(0x0355) == mode && machine.Bus.Peek(0x0318) == 1 && machine.Bus.Peek(0x0319) == 5;
    Console.WriteLine("prompt " + (prompt ? "yes" : "no"));
    if (screen != "boot")
    {
        DensePage.Fill(machine.Bus, screen);
        Console.WriteLine("screen " + screen);
    }

    for (int i = 1; i <= runs; i++)
    {
        long start = machine.Cycles;
        clock.Restart();
        machine.Run(cycles);
        Report("timed " + i, machine.Cycles - start, clock.Elapsed.TotalMilliseconds);
    }
}

static void Report(string label, long cycles, double ms)
{
    double perSecond = cycles / (ms / 1000.0);
    Console.WriteLine($"{label} cycles={cycles} ms={ms:F3} cycles_per_second={perSecond:F0} mhz={perSecond / 1e6:F3}");
}

static string Row(BbcMachine machine, int row)
{
    var text = new StringBuilder(40);
    for (int i = 0; i < 40; i++)
    {
        byte code = machine.Bus.Peek((ushort)(0x7C00 + (row * 40) + i));
        text.Append(code is >= 0x20 and < 0x7F ? (char)code : ' ');
    }

    return text.ToString();
}

/// <summary>
/// A scripted run whose every instruction and every chip's state is folded into hashes. Two
/// builds of the machine that print the same lines did the same thing on every instruction
/// boundary: the cycle count, the registers and the IRQ line, and at each checkpoint all of RAM
/// and every VIA register, port pin and control line.
/// </summary>
internal static class Fingerprint
{
    private const ulong Prime = 0x100000001B3;

    // Exercises the timers, the IFR, the shift register's mode, the sound chip's latch and the
    // keyboard: a BASIC program typed in, run, then a BREAK and another run.
    private static readonly string[] Lines =
    [
        "10 FOR I=1 TO 60",
        "20 ?&FE6B=&08+(I AND 3)*&40+(I AND 4)*8:?&FE62=255:?&FE60=I*4",
        "25 ?&FE6C=(I AND 7)*34:?&FE61=I:?&FE68=I:?&FE69=0:?&FE64=I:?&FE65=0",
        "30 X%=?&FE6D:Y%=?&FE68:Z%=?&FE6A:W%=?&FE44:V%=?&FE60",
        "40 SOUND 1,-15,I,1",
        "50 PRINT I;\" \";X%;\" \";Y%;\" \";Z%;\" \";W%;\" \";V%",
        "60 NEXT",
        "RUN",
    ];

    public static void Run(BbcRoms roms)
    {
        var machine = new BbcMachine(roms);
        var state = new State(machine);
        machine.PowerOn();

        state.RunFor(6_000_000);
        state.Print("boot");

        foreach (string line in Lines)
        {
            foreach (char c in line)
            {
                state.Type(c);
            }
            state.Type('\r');
        }
        state.RunFor(40_000_000);
        state.Print("program");
        PrintScreen(machine);

        machine.PressBreak();
        state.RunFor(6_000_000);
        state.Print("break");

        foreach (char c in "RUN")
        {
            state.Type(c);
        }
        state.Type('\r');
        state.RunFor(20_000_000);
        state.Print("rerun");

        PrintScreen(machine);
    }

    private static void PrintScreen(BbcMachine machine)
    {
        for (int row = 0; row < 25; row++)
        {
            Console.WriteLine("screen " + Row(machine, row).TrimEnd());
        }
    }

    private static string Row(BbcMachine machine, int row)
    {
        var text = new StringBuilder(40);
        for (int i = 0; i < 40; i++)
        {
            byte code = machine.Bus.Peek((ushort)(0x7C00 + (row * 40) + i));
            text.Append(code is >= 0x20 and < 0x7F ? (char)code : '.');
        }

        return text.ToString();
    }

    private sealed class State(BbcMachine machine)
    {
        private ulong _steps = 0xCBF29CE484222325;
        private ulong _checkpoints = 0xCBF29CE484222325;
        private long _instructions;
        private long _irqCycles;
        private long _nextCheckpoint = 100_000;

        public void RunFor(long cycles)
        {
            long end = machine.Cycles + cycles;
            while (machine.Cycles < end)
            {
                machine.Step();
                var cpu = machine.Cpu;
                _instructions++;
                if (cpu.Irq)
                {
                    _irqCycles++;
                }

                Mix(ref _steps, (ulong)machine.Cycles);
                Mix(ref _steps, cpu.PC | ((ulong)cpu.A << 16) | ((ulong)cpu.X << 24) | ((ulong)cpu.Y << 32)
                    | ((ulong)cpu.S << 40) | ((ulong)cpu.P << 48) | (cpu.Irq ? 1UL << 56 : 0));

                if (machine.Cycles >= _nextCheckpoint)
                {
                    _nextCheckpoint += 100_000;
                    Checkpoint();
                }
            }
        }

        public void Type(char c)
        {
            (BbcKey key, bool shift) = KeyFor(c);
            var keyboard = machine.Keyboard;
            if (shift)
            {
                keyboard.Press(BbcKey.Shift);
            }
            keyboard.Press(key);
            RunFor(80_000);
            keyboard.Release(key);
            if (shift)
            {
                keyboard.Release(BbcKey.Shift);
            }
            RunFor(80_000);
        }

        public void Print(string label) =>
            Console.WriteLine($"{label} cycles={machine.Cycles} instructions={_instructions} irq_steps={_irqCycles} steps={_steps:X16} checkpoints={_checkpoints:X16}");

        private void Checkpoint()
        {
            var bus = machine.Bus;
            Mix(ref _checkpoints, (ulong)machine.Cycles);
            for (int a = 0; a < 0x8000; a += 8)
            {
                ulong word = 0;
                for (int b = 0; b < 8; b++)
                {
                    word |= (ulong)bus.Peek((ushort)(a + b)) << (8 * b);
                }
                Mix(ref _checkpoints, word);
            }

            foreach (Via6522 via in new Via6522[] { bus.SystemVia, bus.UserVia })
            {
                for (int r = 0; r < 16; r++)
                {
                    Mix(ref _checkpoints, via.Peek(r));
                }
                Mix(ref _checkpoints, via.PortAPins | ((ulong)via.PortBPins << 8) | (via.Irq ? 1UL << 16 : 0)
                    | (via.Ca2Out ? 1UL << 17 : 0) | (via.Cb2Out ? 1UL << 18 : 0));
            }

            Mix(ref _checkpoints, bus.SystemVia.Latch | ((ulong)bus.RomSlot << 8) | (bus.Irq ? 1UL << 16 : 0));
        }

        private static void Mix(ref ulong hash, ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                hash ^= (byte)(value >> (8 * i));
                hash *= Prime;
            }
        }

        private static (BbcKey Key, bool Shift) KeyFor(char c) => c switch
        {
            >= 'A' and <= 'Z' => (Enum.Parse<BbcKey>(c.ToString()), false),
            >= '0' and <= '9' => (Enum.Parse<BbcKey>("D" + c), false),
            ' ' => (BbcKey.Space, false),
            '\r' => (BbcKey.Return, false),
            '=' => (BbcKey.Minus, true),
            '?' => (BbcKey.Slash, true),
            '&' => (BbcKey.D6, true),
            '%' => (BbcKey.D5, true),
            '"' => (BbcKey.D2, true),
            ';' => (BbcKey.Semicolon, false),
            ':' => (BbcKey.Colon, false),
            ',' => (BbcKey.Comma, false),
            '-' => (BbcKey.Minus, false),
            '(' => (BbcKey.D8, true),
            ')' => (BbcKey.D9, true),
            '+' => (BbcKey.Semicolon, true),
            '*' => (BbcKey.Colon, true),
            _ => throw new ArgumentOutOfRangeException(nameof(c), c, "No key for this character."),
        };
    }
}

/// <summary>
/// Where a cycle's time goes: the booted machine against a bare CPU running the same code on a
/// flat 64 KB copy of its memory, with no bus logic and no chips. The difference is what the
/// bus and its chips cost. The same program at commit 54a6630, before task 6b, also ran the
/// machine with its chips' per-cycle ticks removed and with one VIA at a time; those variants
/// overrode the bus's per-cycle tick, which task 6b took away, and their figures are in the
/// journal for the second of October's speed entry.
/// </summary>
internal static class Profile
{
    private const long Cycles = 20_000_000;

    private enum Variant { Full, FlatBus }

    public static void Run(BbcRoms roms, int rounds)
    {
        var variants = Enum.GetValues<Variant>();
        var results = variants.ToDictionary(v => v, _ => new List<double>());

        // One round first, not counted, so every variant is measured on fully compiled code.
        foreach (Variant variant in variants)
        {
            NanosecondsPerCycle(roms, variant);
        }

        for (int round = 0; round < rounds; round++)
        {
            // Alternate the order each round so drift in the machine's load falls on all of them.
            IEnumerable<Variant> order = round % 2 == 0 ? variants : variants.Reverse();
            foreach (Variant variant in order)
            {
                results[variant].Add(NanosecondsPerCycle(roms, variant));
            }
        }

        foreach (Variant variant in variants)
        {
            var sorted = results[variant].Order().ToList();
            Console.WriteLine($"{variant,-14} ns_per_cycle median={sorted[sorted.Count / 2]:F2} min={sorted[0]:F2} max={sorted[^1]:F2} rounds={sorted.Count}");
        }
    }

    private static double NanosecondsPerCycle(BbcRoms roms, Variant variant)
    {
        var machine = new BbcMachine(roms);
        machine.PowerOn();
        machine.Run(6_000_000);
        var cpu = machine.Cpu;

        if (variant == Variant.FlatBus)
        {
            var memory = new byte[0x10000];
            for (int a = 0; a < 0x10000; a++)
            {
                memory[a] = machine.Bus.Peek((ushort)a);
            }
            var bare = new Dbhq.Cpu6502.Cpu(new FlatBus(memory), Dbhq.Cpu6502.CpuVariant.Nmos6502)
            {
                PC = cpu.PC, A = cpu.A, X = cpu.X, Y = cpu.Y, S = cpu.S, P = cpu.P,
            };
            var clock = Stopwatch.StartNew();
            long done = 0;
            while (done < Cycles)
            {
                done += bare.Step();
            }
            return clock.Elapsed.TotalNanoseconds / done;
        }

        long start = machine.Cycles;
        var timer = Stopwatch.StartNew();
        machine.Run(Cycles);
        return timer.Elapsed.TotalNanoseconds / (machine.Cycles - start);
    }

    /// <summary>64 KB and nothing else: every access is one array read or write.</summary>
    private sealed class FlatBus(byte[] memory) : Dbhq.Cpu6502.IBus
    {
        public byte Read(ushort address) => memory[address];

        public void Write(ushort address, byte value) => memory[address] = value;
    }
}

/// <summary>
/// A mode 7 page that makes the teletext chip draw every cell: the 1,000 bytes of screen memory
/// at &amp;7C00 filled with random bytes (<c>dense</c>, so every control code, double height,
/// hold and flash come up) or random printable characters (<c>text</c>), from seed 1234. The same
/// code is in the browser host (<c>BbcHost.FillScreen</c>), so the two runs time the same page.
/// </summary>
internal static class DensePage
{
    public static void Fill(BbcBus bus, string kind)
    {
        var random = new Random(1234);
        for (int i = 0; i < 1000; i++)
        {
            int value = kind switch
            {
                "dense" => random.Next(256),
                "text" => random.Next(0x20, 0x7F),
                _ => throw new ArgumentException($"no screen called {kind}: boot, dense or text"),
            };
            bus.PokeRam((ushort)(0x7C00 + i), (byte)value);
        }
    }
}
