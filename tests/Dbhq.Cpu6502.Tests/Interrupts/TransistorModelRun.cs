using System.Globalization;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Cpu6502.Tests.Interrupts;

/// <summary>
/// One run of the transistor-level model, as tools/perfect6502/generate.sh
/// writes it: the program, when the interrupt lines change, the registers at
/// the first opcode fetch, and every bus cycle. An off cycle of -1 holds the
/// line to the end; a run may pulse NMI a second time (Nmi2On to Nmi2Off).
/// </summary>
public sealed record TransistorModelRun(string Name, byte[] Code, int IrqOn, int IrqOff, int NmiOn, int NmiOff, int Nmi2On, int Nmi2Off, byte A, byte X, byte Y, byte S, byte P, BusAccess[] Cycles)
{
    public override string ToString() => Name;

    public static IReadOnlyList<TransistorModelRun> Load()
    {
        var runs = new List<TransistorModelRun>();
        string[] lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Interrupts", "visual6502.txt"))
            .Where(line => !line.StartsWith('#') || line.StartsWith("## ", StringComparison.Ordinal))
            .ToArray();
        for (int i = 0; i < lines.Length;)
        {
            string name = lines[i++][3..];
            byte[] code = lines[i++].Split(' ')[1..].Select(Hex).ToArray();
            int[] irq = lines[i++].Split(' ')[1..].Select(int.Parse).ToArray();
            int[] nmi = lines[i++].Split(' ')[1..].Select(int.Parse).ToArray();
            int[] nmi2 = lines[i].StartsWith("nmi2 ", StringComparison.Ordinal)
                ? lines[i++].Split(' ')[1..].Select(int.Parse).ToArray()
                : [-1, -1];
            byte[] state = lines[i++].Split(' ')[1..].Select(pair => Hex(pair[2..])).ToArray();
            var cycles = new List<BusAccess>();
            while (i < lines.Length && !lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                string[] parts = lines[i++].Split(' ');
                cycles.Add(new BusAccess(ushort.Parse(parts[1], NumberStyles.HexNumber), Hex(parts[2]), parts[3] == "W"));
            }

            runs.Add(new TransistorModelRun(name, code, irq[0], irq[1], nmi[0], nmi[1], nmi2[0], nmi2[1], state[0], state[1], state[2], state[3], state[4], cycles.ToArray()));
        }

        return runs;
    }

    private static byte Hex(string text) => byte.Parse(text, NumberStyles.HexNumber);
}

/// <summary>
/// A bus that raises and lowers the interrupt lines at the cycles a run
/// names. A change scheduled for cycle k happens before access k, as the
/// harness changes the chip's pins before cycle k's two half-steps.
/// </summary>
public sealed class ScheduledBus(TransistorModelRun run) : IBus
{
    public byte[] Memory { get; } = new byte[0x10000];

    public List<BusAccess> Log { get; } = [];

    public Cpu? Cpu { get; set; }

    public byte Read(ushort address)
    {
        ChangeLines();
        byte value = Memory[address];
        Log.Add(new BusAccess(address, value, false));
        return value;
    }

    public void Write(ushort address, byte value)
    {
        ChangeLines();
        Memory[address] = value;
        Log.Add(new BusAccess(address, value, true));
    }

    private void ChangeLines()
    {
        int cycle = Log.Count;
        if (cycle == run.IrqOn)
        {
            Cpu!.Irq = true;
        }

        if (cycle == run.IrqOff)
        {
            Cpu!.Irq = false;
        }

        if (cycle == run.NmiOn)
        {
            Cpu!.Nmi = true;
        }

        if (cycle == run.NmiOff)
        {
            Cpu!.Nmi = false;
        }

        if (cycle == run.Nmi2On)
        {
            Cpu!.Nmi = true;
        }

        if (cycle == run.Nmi2Off)
        {
            Cpu!.Nmi = false;
        }
    }
}
