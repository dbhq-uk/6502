using Xunit;
using static Dbhq.Machines.Nes.Tests.DmcTesting;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// DMC DMA, <c>docs/nes/facts/bus.md</c> section 6: a fetch halts the CPU on a read, spends a
/// dummy cycle and an alignment cycle if the next is not a get, then reads on a get. The first
/// fetch after a <c>$4015</c> write (a load) tries to halt on the get of the second APU cycle after
/// the write and takes 3; later ones (reloads) try to halt on a put and take 4; a halt that meets a
/// write waits; inside OAM DMA a fetch costs 2, and at its very end 1 or 3. The model makes the
/// even cycles gets, as it does for OAM DMA. Every cost is the change in <c>Bus.Cycles</c>.
/// </summary>
public class DmcDmaTests
{
    public static TheoryData<string> Regions() => ApuTesting.Regions();

    public static TheoryData<string, int> RegionsAndParities()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            rows.Add(region, 0);
            rows.Add(region, 1);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsAndParities))]
    public void ALoadHaltsOnTheGetOfTheSecondApuCycleAfterTheWriteAndStealsThree(string region, int parity)
    {
        Nes nes = Machine(region, 0x00);
        long write = Start(nes, 15, 0x00, 0x10, parity);

        // A write on a get (even) leaves its APU cycle at the next put; the second APU cycle after
        // begins 4 cycles after the write. From a put (odd) it is 3 (bus.md 6).
        (long halt, int stolen) = RunUntilAStall(nes, 10);
        Assert.Equal(write + (parity == 0 ? 4 : 3), halt);
        Assert.Equal(0, halt % 2);
        Assert.Equal(3, stolen);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AReloadHaltsOnAPutAndStealsFour(string region)
    {
        Nes nes = Machine(region, 0x00);
        Start(nes, 15, 0x00, 0x10, parity: 0);
        RunUntilAStall(nes, 10);

        for (int fetch = 0; fetch < 5; fetch++)
        {
            (long halt, int stolen) = RunUntilAStall(nes, 20 * 54);
            Assert.Equal(1, halt % 2);
            Assert.Equal(4, stolen);
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AReloadHaltsOnTheNextPutAfterTheOutputClockThatEmptiedTheBuffer(string region)
    {
        // apu.md 8 and bus.md 6: a reload "may halt on the next put" after the output unit empties
        // the buffer. The unit clocks on the puts (odd cycles), so the halt comes 2 cycles after
        // the clock that ended a byte. When in that APU cycle the hardware schedules it is not on
        // the sheet and the pinned ROMs pass with a later put too (known-differences.md), so this
        // pins the model's choice. Bytes of $AA from a level of 64 move the level on every clock,
        // which shows when each clock came; the first byte plays silent, so its reload is skipped.
        Nes nes = Machine(region, 0xAA);
        nes.Bus.Write(0x4011, 64);
        Start(nes, 15, 0x00, 0x10, parity: 0, loop: true);
        RunUntilAStall(nes, 10);
        RunUntilAStall(nes, 20 * 54);

        for (int fetch = 0; fetch < 5; fetch++)
        {
            long lastClock = -1;
            long halt = -1;
            int level = nes.Bus.Apu.Dmc.Level;
            long end = nes.Bus.Cycles + (20 * 54);
            while (halt < 0 && nes.Bus.Cycles < end)
            {
                long cycle = nes.Bus.Cycles + 1;
                if (IdleRead(nes) > 1)
                {
                    halt = cycle;
                }
                else if (nes.Bus.Apu.Dmc.Level != level)
                {
                    lastClock = cycle;
                    level = nes.Bus.Apu.Dmc.Level;
                }
            }

            Assert.Equal(1, lastClock % 2);
            Assert.Equal(lastClock + 2, halt);
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ReloadsComeOncePerByteAtTheRate(string region)
    {
        // A byte lasts 8 output clocks: at rate $F that is 8 x 54 cycles on NTSC, 8 x 50 on PAL.
        Region r = ApuTesting.RegionNamed(region);
        Nes nes = Machine(region, 0x00);
        Start(nes, 15, 0x00, 0x10, parity: 0);
        RunUntilAStall(nes, 10);

        long previous = RunUntilAStall(nes, 20 * 54).HaltCycle;
        for (int fetch = 0; fetch < 5; fetch++)
        {
            long halt = RunUntilAStall(nes, 20 * 54).HaltCycle;
            Assert.Equal(8 * r.DmcRates[15], halt - previous);
            previous = halt;
        }
    }

    [Theory]
    [InlineData("NTSC", 0, 1, 4)]
    [InlineData("NTSC", 0, 2, 3)]
    [InlineData("NTSC", 1, 1, 4)]
    [InlineData("NTSC", 1, 2, 3)]
    [InlineData("PAL", 0, 1, 4)]
    [InlineData("PAL", 0, 2, 3)]
    [InlineData("PAL", 1, 1, 4)]
    [InlineData("PAL", 1, 2, 3)]
    public void ALoadWhoseHaltMeetsWritesWaitsForTheNextRead(string region, int parity, int writes, int stolen)
    {
        // bus.md 6: a halt that meets a write waits and tries again. Delayed by one it lands on a
        // put and needs the alignment cycle: 4. Delayed by two it is back on a get: 3.
        Nes nes = Machine(region, 0x00);
        long write = Start(nes, 15, 0x00, 0x10, parity);
        long attempt = write + (parity == 0 ? 4 : 3);
        RunUntilNextCycleIs(nes, attempt);
        for (int i = 0; i < writes; i++)
        {
            nes.Bus.Write(0x0000, 0x00);
        }

        Assert.Equal(attempt + writes - 1, nes.Bus.Cycles);
        Assert.Equal(1 + stolen, IdleRead(nes));
    }

    [Theory]
    [InlineData("NTSC", 1, 3)]
    [InlineData("NTSC", 2, 4)]
    [InlineData("NTSC", 3, 3)]
    [InlineData("PAL", 1, 3)]
    [InlineData("PAL", 2, 4)]
    [InlineData("PAL", 3, 3)]
    public void AReloadWhoseHaltMeetsWritesWaitsForTheNextRead(string region, int writes, int stolen)
    {
        // The DMA page's examples: a reload delayed by one or three cycles halts on a get and
        // takes 3; by two, on a put, and takes 4.
        long reload = FirstReloadHalt(region);

        Nes nes = ReloadMachine(region);
        RunUntilNextCycleIs(nes, reload);
        for (int i = 0; i < writes; i++)
        {
            nes.Bus.Write(0x0000, 0x00);
        }

        Assert.Equal(1 + stolen, IdleRead(nes));
    }

    [Theory]
    [InlineData("NTSC", 201, 2)]
    [InlineData("NTSC", 511, 1)]
    [InlineData("NTSC", 513, 3)]
    [InlineData("PAL", 201, 2)]
    [InlineData("PAL", 511, 1)]
    [InlineData("PAL", 513, 3)]
    public void AFetchInsideOamDmaCostsTwoAndAtItsEndOneOrThree(string region, int before, int extra)
    {
        // A $4014 write in an even cycle W halts the CPU at W + 1 (a put): the copy's gets are
        // W + 2 to W + 512, its puts W + 3 to W + 513. A reload halting on a put in the middle
        // costs 2; on the second-to-last put, W + 511, it costs 1; on the last, W + 513, 3 (the
        // DMA page's examples).
        long reload = FirstReloadHalt(region);
        Assert.Equal(1, reload % 2);

        Nes nes = ReloadMachine(region);
        RunUntilNextCycleIs(nes, reload - before);
        nes.Bus.Write(0x4014, 0x02);
        Assert.Equal(0, nes.Bus.Cycles % 2);

        long start = nes.Bus.Cycles;
        nes.Bus.Read(0x0000);
        Assert.Equal(513 + extra + 1, nes.Bus.Cycles - start);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheCopyStillReachesOamWhenAFetchInterruptsIt(string region)
    {
        long reload = FirstReloadHalt(region);
        Nes nes = ReloadMachine(region);
        for (int i = 0; i < 256; i++)
        {
            nes.Bus.PokeRam((ushort)(0x0200 + i), (byte)(255 - i));
        }

        nes.Bus.Write(0x2003, 0x00);
        RunUntilNextCycleIs(nes, reload - 201);
        nes.Bus.Write(0x4014, 0x02);
        nes.Bus.Read(0x0000);

        for (int i = 0; i < 256; i++)
        {
            byte expected = (byte)(255 - i);
            Assert.Equal((i & 3) == 2 ? (byte)(expected & 0xE3) : expected, nes.Bus.Ppu.Oam[i]);
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheCpusOwnAccessesAreUnchangedInCountAndOnlyTheBusCountGrows(string region)
    {
        // A program that plays a looped sample at the fastest rate and counts down 2000 times. The
        // CPU's own cycle count is the same as with the DMC off; the bus's grows by the fetches.
        byte[] Program(byte enable) =>
        [
            0xA9, 0x4F, 0x8D, 0x10, 0x40, // LDA #$4F; STA $4010: loop, rate $F
            0xA9, 0xFF, 0x8D, 0x13, 0x40, // LDA #$FF; STA $4013
            0xA9, enable, 0x8D, 0x15, 0x40, // LDA #enable; STA $4015
            0xA2, 0x00, 0xA0, 0x08,       // LDX #0; LDY #8
            0xCA, 0xD0, 0xFD,             // loop: DEX; BNE loop
            0x88, 0xD0, 0xFA,             // DEY; BNE loop
            0x4C, 0x19, 0xC0,             // JMP * (at $C019)
        ];

        (long cpu, long bus) Run(byte enable)
        {
            byte[] prg = new byte[0x8000];
            byte[] code = Program(enable);
            code.CopyTo(prg, 0x4000);
            prg[0x7FFC] = 0x00;
            prg[0x7FFD] = 0xC0;
            Nes nes = Machine(region, prg);
            long cpuStart = nes.Cpu.Cycles;
            long busStart = nes.Bus.Cycles;
            while (nes.Cpu.PC != 0xC019)
            {
                nes.Step();
            }

            return (nes.Cpu.Cycles - cpuStart, nes.Bus.Cycles - busStart);
        }

        (long cpuOff, long busOff) = Run(0x00);
        (long cpuOn, long busOn) = Run(0x10);
        Assert.Equal(cpuOff, busOff);
        Assert.Equal(cpuOff, cpuOn);

        // About 10,000 cycles at 8 x 54 (or 8 x 50) a byte: a load of 3, then 4 for each reload.
        long stolen = busOn - cpuOn;
        Assert.True(stolen > 0);
        Assert.InRange(stolen, 3 + (4 * 15), 3 + (4 * 30));
        Assert.Equal(0, (stolen - 3) % 4);
    }

    [Theory]
    [InlineData("NTSC", 1)]
    [InlineData("PAL", 0)]
    public void AHaltOnAPadReadClocksThePadOnceMoreOnNtscOnly(string region, int lost)
    {
        // bus.md 6: on the 2A03 the halted cycles repeat the CPU's read of $4016, the pad sees one
        // clock for that run and another for the CPU's own read, so a bit is lost. The 2A07 does
        // not repeat the read.
        long reload = FirstReloadHalt(region);
        Nes nes = ReloadMachine(region);
        nes.SetButtons(0, 0b0000_0101);
        nes.Bus.Write(0x4016, 1);
        nes.Bus.Write(0x4016, 0);

        RunUntilNextCycleIs(nes, reload);
        var bits = new List<int> { nes.Bus.Read(0x4016) & 1 };
        Assert.True(nes.Bus.Cycles - reload >= 4);
        for (int i = 0; i < 8; i++)
        {
            nes.Bus.Read(0x0000);
            bits.Add(nes.Bus.Read(0x4016) & 1);
        }

        // A, B, Select, Start, ... with A and Select held, then the 1s after the eighth.
        int[] pad = [1, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1];
        Assert.Equal(pad.Skip(lost).Take(9), bits);
    }

    [Theory]
    [InlineData("NTSC", 3)]
    [InlineData("PAL", 0)]
    public void AHaltOnAPpudataReadReadsItAgainOnNtscOnly(string region, int extra)
    {
        // bus.md 6 and the DMA page's example: a reload halted on a $2007 read repeats it on the
        // halt, dummy and alignment cycles, three extra reads, each moving the PPU address on.
        long reload = FirstReloadHalt(region);
        Nes nes = ReloadMachine(region);
        nes.Bus.Write(0x2006, 0x20);
        nes.Bus.Write(0x2006, 0x00);

        RunUntilNextCycleIs(nes, reload);
        nes.Bus.Read(0x2007);
        Assert.True(nes.Bus.Cycles - reload >= 4);
        Assert.Equal(0x2001 + extra, nes.Bus.Ppu.V);
    }

    // A byte at the slowest rate, with room to spare: 8 x 428 cycles on NTSC, 8 x 398 on PAL.
    private const int SlowByte = 8 * 428 + 100;

    // The machine the reload tests share: a looped sample at rate 0, started on an even cycle,
    // run to just after its first reload. Built the same way each time, so its next reload halts
    // on the same cycle, which is a whole byte, over 3000 cycles, away.
    private static Nes ReloadMachine(string region)
    {
        Nes nes = Machine(region, 0x00);
        Start(nes, 0, 0x00, 0x10, parity: 0, loop: true);
        RunUntilAStall(nes, 10);
        RunUntilAStall(nes, SlowByte);
        return nes;
    }

    private static long FirstReloadHalt(string region) => RunUntilAStall(ReloadMachine(region), SlowByte).HaltCycle;
}
