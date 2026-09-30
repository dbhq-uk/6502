using Xunit;

namespace Dbhq.Cpu6502.Tests.Interrupts;

/// <summary>
/// NMOS interrupt timing, checked cycle by cycle against runs of the
/// Visual6502 transistor-level model. The expected data is the chip's own
/// circuit, not a reading of a document.
/// </summary>
public sealed class TransistorModelTests
{
    public static TheoryData<string> Runs => new(TransistorModelRun.Load().Select(r => r.Name));

    [Theory]
    [MemberData(nameof(Runs))]
    public void MatchesTheTransistorModel(string name)
    {
        TransistorModelRun run = TransistorModelRun.Load().Single(r => r.Name == name);
        var bus = new ScheduledBus(run);
        Array.Fill(bus.Memory, (byte)0xEA);
        run.Code.CopyTo(bus.Memory, 0x0400);
        bus.Memory[0x0500] = 0x40;
        bus.Memory[0x0600] = 0x40;
        bus.Memory[0xFFFA] = 0x00;
        bus.Memory[0xFFFB] = 0x06;
        bus.Memory[0xFFFC] = 0x00;
        bus.Memory[0xFFFD] = 0x04;
        bus.Memory[0xFFFE] = 0x00;
        bus.Memory[0xFFFF] = 0x05;
        var cpu = new Cpu(bus, CpuVariant.Nmos6502)
        {
            A = run.A,
            X = run.X,
            Y = run.Y,
            S = run.S,
            P = (byte)((run.P & ~(byte)StatusFlags.Break) | (byte)StatusFlags.Unused),
            PC = 0x0400,
        };
        bus.Cpu = cpu;

        while (bus.Log.Count < run.Cycles.Length)
        {
            cpu.Step();
        }

        for (int i = 0; i < run.Cycles.Length; i++)
        {
            Assert.True(run.Cycles[i] == bus.Log[i], $"{name}, cycle {i}: ours is a {bus.Log[i]}, the chip's is a {run.Cycles[i]}");
        }
    }
}
