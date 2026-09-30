using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Cpu6502.Tests.Harte;

/// <summary>Runs Harte cases one instruction at a time and says exactly where each one differs.</summary>
public static class HarteRunner
{
    /// <summary>Runs every case and returns up to maxFailures descriptions of the ones that fail.</summary>
    public static List<string> Run(CpuVariant variant, byte opcode, IReadOnlyList<HarteCase> cases, int maxFailures = 5)
    {
        var bus = new FlatBus();
        var failures = new List<string>();
        foreach (HarteCase c in cases)
        {
            string? failure = RunCase(bus, variant, opcode, c);
            if (failure is not null)
            {
                failures.Add($"{c.Name}: {failure}");
                if (failures.Count == maxFailures)
                {
                    break;
                }
            }
        }

        return failures;
    }

    /// <summary>Runs one case on a bus shared between cases, and leaves the bus clean for the next.</summary>
    public static string? RunCase(FlatBus bus, CpuVariant variant, byte opcode, HarteCase c)
    {
        foreach (var (address, value) in c.Initial.Ram)
        {
            bus.Memory[address] = value;
        }

        bus.Log.Clear();
        var cpu = new Cpu(bus, variant)
        {
            PC = c.Initial.PC,
            S = c.Initial.S,
            A = c.Initial.A,
            X = c.Initial.X,
            Y = c.Initial.Y,
            P = c.Initial.P,
        };

        string? failure;
        try
        {
            cpu.Step();
            failure = Compare(variant, opcode, c, cpu, bus);
        }
        catch (NotImplementedException e)
        {
            failure = e.Message;
        }

        foreach (var (address, _) in c.Initial.Ram)
        {
            bus.Memory[address] = 0;
        }

        foreach (BusAccess access in bus.Log)
        {
            bus.Memory[access.Address] = 0;
        }

        return failure;
    }

    /// <summary>
    /// The one documented exception to comparing every cycle: on the 65C02,
    /// the extra decimal-mode cycle of ADC #imm and SBC #imm. Harte's data
    /// has it read a fixed address that differs by variant, which looks like
    /// an artefact of how the data was made. The cycle must exist and be a
    /// read; its address and value are not compared. See
    /// docs/known-differences.md.
    /// </summary>
    public static bool IsDecimalImmediateExtraCycle(CpuVariant variant, byte opcode, HarteCase c, int cycle) =>
        variant is CpuVariant.Synertek65C02 or CpuVariant.Rockwell65C02 or CpuVariant.Wdc65C02
        && (opcode is 0x69 or 0xE9)
        && (c.Initial.P & (byte)StatusFlags.Decimal) != 0
        && cycle == 2;

    private static string? Compare(CpuVariant variant, byte opcode, HarteCase c, Cpu cpu, FlatBus bus)
    {
        if (bus.Log.Count != c.Cycles.Length)
        {
            return $"{bus.Log.Count} cycles, expected {c.Cycles.Length}. Ours: {string.Join(", ", bus.Log)}. Expected: {string.Join(", ", c.Cycles)}";
        }

        for (int i = 0; i < c.Cycles.Length; i++)
        {
            if (IsDecimalImmediateExtraCycle(variant, opcode, c, i))
            {
                if (bus.Log[i].IsWrite)
                {
                    return $"cycle {i} is a write, expected a read";
                }

                continue;
            }

            if (bus.Log[i] != c.Cycles[i])
            {
                return $"cycle {i} is a {bus.Log[i]}, expected a {c.Cycles[i]}";
            }
        }

        HarteState f = c.Final;
        (string Name, int Actual, int Expected)[] registers =
        [
            ("PC", cpu.PC, f.PC), ("S", cpu.S, f.S), ("A", cpu.A, f.A), ("X", cpu.X, f.X), ("Y", cpu.Y, f.Y), ("P", cpu.P, f.P),
        ];
        foreach (var (name, actual, expected) in registers)
        {
            if (actual != expected)
            {
                return $"{name} is ${actual:X2}, expected ${expected:X2}";
            }
        }

        foreach (var (address, value) in f.Ram)
        {
            if (bus.Memory[address] != value)
            {
                return $"memory ${address:X4} is ${bus.Memory[address]:X2}, expected ${value:X2}";
            }
        }

        return null;
    }
}
