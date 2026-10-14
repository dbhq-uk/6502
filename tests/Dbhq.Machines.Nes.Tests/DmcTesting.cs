namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// What the DMC's tests share: a machine whose 32 KB of program ROM holds sample bytes the test
/// chooses, and ways to run its bus one cycle at a time. The CPU's program is never run; the
/// tests make the bus's reads and writes themselves, so the DMC's DMA happens on the real path,
/// inside a <see cref="NesBus.Read"/>.
/// </summary>
public static class DmcTesting
{
    /// <summary>
    /// A machine, powered on, whose program ROM at <c>$8000</c> to <c>$FFFF</c> is
    /// <paramref name="prg"/>'s bytes, 32 KB of them.
    /// </summary>
    public static Nes Machine(string region, byte[] prg)
    {
        if (prg.Length != 0x8000)
        {
            throw new ArgumentException("the program ROM is 32 KB", nameof(prg));
        }

        byte[] file = TestCartridge.Join(TestCartridge.Ines1(2, 1)[..16], prg, new byte[8192]);
        var nes = new Nes(Cartridge.Load(file), ApuTesting.RegionNamed(region));
        nes.PowerOn();
        return nes;
    }

    /// <summary>A machine whose program ROM is every byte <paramref name="fill"/>.</summary>
    public static Nes Machine(string region, byte fill) => Machine(region, TestCartridge.Filled(0x8000, fill));

    /// <summary>
    /// One idle read of RAM, and the cycles it took: 1, or more when a DMA halted the CPU on it.
    /// </summary>
    public static int IdleRead(Nes nes)
    {
        long before = nes.Bus.Cycles;
        nes.Bus.Read(0x0000);
        return (int)(nes.Bus.Cycles - before);
    }

    /// <summary>Idle reads until at least <paramref name="cycles"/> more cycles have passed.</summary>
    public static void Run(Nes nes, long cycles)
    {
        long end = nes.Bus.Cycles + cycles;
        while (nes.Bus.Cycles < end)
        {
            nes.Bus.Read(0x0000);
        }
    }

    /// <summary>Idle reads until the next cycle is <paramref name="cycle"/>.</summary>
    public static void RunUntilNextCycleIs(Nes nes, long cycle)
    {
        if (nes.Bus.Cycles >= cycle)
        {
            throw new InvalidOperationException($"cycle {cycle} has passed: the bus is at {nes.Bus.Cycles}");
        }

        while (nes.Bus.Cycles + 1 < cycle)
        {
            nes.Bus.Read(0x0000);
        }
    }

    /// <summary>Idle reads until the next cycle has the parity asked for (0 even, 1 odd).</summary>
    public static void RunUntilNextParityIs(Nes nes, int parity)
    {
        while ((nes.Bus.Cycles + 1) % 2 != parity)
        {
            nes.Bus.Read(0x0000);
        }
    }

    /// <summary>
    /// Idle reads until one of them is halted by a DMA, failing after <paramref name="limit"/>
    /// cycles. Returns the cycle the halted read began in and the cycles stolen from it.
    /// </summary>
    public static (long HaltCycle, int Stolen) RunUntilAStall(Nes nes, int limit)
    {
        long end = nes.Bus.Cycles + limit;
        while (nes.Bus.Cycles < end)
        {
            long halt = nes.Bus.Cycles + 1;
            int took = IdleRead(nes);
            if (took > 1)
            {
                return (halt, took - 1);
            }
        }

        throw new Xunit.Sdk.XunitException($"no DMA halted the CPU within {limit} cycles");
    }

    /// <summary>
    /// Starts a sample: rate index <paramref name="rate"/>, the address and length registers as
    /// given, then <c>$10</c> to <c>$4015</c> in a cycle of the parity asked for. Returns the
    /// write's cycle.
    /// </summary>
    public static long Start(Nes nes, int rate, byte address, byte length, int parity, bool loop = false, bool irq = false)
    {
        nes.Bus.Write(0x4010, (byte)((irq ? 0x80 : 0) | (loop ? 0x40 : 0) | rate));
        nes.Bus.Write(0x4012, address);
        nes.Bus.Write(0x4013, length);
        RunUntilNextParityIs(nes, parity);
        nes.Bus.Write(0x4015, 0x10);
        return nes.Bus.Cycles;
    }
}
