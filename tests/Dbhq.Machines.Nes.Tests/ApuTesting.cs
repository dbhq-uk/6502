using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// What the sound unit's tests share: a unit for a region, and ways to clock its frame counter
/// and to write on a chosen cycle parity. The tests drive <see cref="Apu"/> directly, one
/// <see cref="Apu.Tick"/> a CPU cycle, as the bus does.
/// </summary>
public static class ApuTesting
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    public static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    /// <summary>A sound unit just powered on.</summary>
    public static Apu Make(string region) => new(RegionNamed(region));

    /// <summary>Runs <paramref name="cycles"/> CPU cycles.</summary>
    public static void Tick(Apu apu, int cycles)
    {
        for (int i = 0; i < cycles; i++)
        {
            apu.Tick();
        }
    }

    /// <summary>
    /// Ticks until <paramref name="done"/> holds, failing the test if it has not after
    /// <paramref name="limit"/> cycles, so a broken unit fails instead of hanging. Returns the
    /// cycles run.
    /// </summary>
    public static int TickUntil(Apu apu, Func<bool> done, int limit)
    {
        int cycles = 0;
        while (!done())
        {
            Assert.True(++cycles <= limit, $"the condition did not hold within {limit} cycles");
            apu.Tick();
        }

        return cycles;
    }

    /// <summary>
    /// One quarter-frame and one half-frame clock, and nothing else: a write of <c>$C0</c> to
    /// <c>$4017</c> (5-step, IRQ inhibited) resets the sequence 3 or 4 cycles later with both clocks
    /// at once (apu.md 10), and the sequence's first step is thousands of cycles away.
    /// </summary>
    public static void ClockQuarterAndHalf(Apu apu)
    {
        apu.Write(0x17, 0xC0);
        Tick(apu, 4);
    }

    /// <summary>
    /// Ticks until the next cycle has the parity asked for (0 even, 1 odd), then makes the write in
    /// it, as the bus does: the cycle's tick, then the access. Returns the write's cycle.
    /// </summary>
    public static long WriteOnParity(Apu apu, int register, byte value, int parity)
    {
        while ((apu.Cycles + 1) % 2 != parity)
        {
            apu.Tick();
        }

        apu.Tick();
        apu.Write(register, value);
        return apu.Cycles;
    }
}
