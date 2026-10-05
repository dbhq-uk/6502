using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The bus: the memory map and mirrors of <c>docs/nes/facts/bus.md</c> sections 1 and 3, and the
/// dot ratio of <c>timing.md</c> section 3, in both regions.
/// </summary>
public class NesBusTests
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    private static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    // A 32 KB PRG whose bytes are known, with a recognisable last four bytes.
    private static (NesBus Bus, byte[] Prg) Build(string region)
    {
        byte[] prg = TestCartridge.Pattern(32768);
        byte[] header = TestCartridge.Header();
        header[4] = 2;
        header[5] = 1;
        Cartridge cartridge = Cartridge.Load(TestCartridge.Join(header, prg, new byte[8192]));
        return (new NesBus(cartridge, RegionNamed(region)), prg);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void RamReadsBackAndIsMirroredFourTimes(string region)
    {
        (NesBus bus, _) = Build(region);

        bus.Write(0x0000, 0x11);
        bus.Write(0x07FF, 0x22);

        Assert.Equal(0x11, bus.Read(0x0000));
        Assert.Equal(0x22, bus.Read(0x07FF));
        foreach (ushort mirror in new ushort[] { 0x0800, 0x1000, 0x1800 })
        {
            Assert.Equal(0x11, bus.Read(mirror));
            Assert.Equal(0x22, bus.Read((ushort)(mirror + 0x7FF)));
        }

        bus.Write(0x1973, 0x5A);
        Assert.Equal(0x5A, bus.Read(0x0173));
        Assert.Equal(0x5A, bus.Read(0x0973));
        Assert.Equal(0x5A, bus.Read(0x1173));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AWriteToProgramRomOnNromIsIgnored(string region)
    {
        (NesBus bus, byte[] prg) = Build(region);

        bus.Write(0x8000, (byte)(prg[0] ^ 0xFF));

        Assert.Equal(prg[0], bus.Read(0x8000));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void PeekReadsTheVectorsFromTheCartridgeWithoutACycle(string region)
    {
        (NesBus bus, byte[] prg) = Build(region);

        Assert.Equal(prg[^4], bus.Peek(0xFFFC));
        Assert.Equal(prg[^3], bus.Peek(0xFFFD));
        Assert.Equal(prg[^2], bus.Peek(0xFFFE));
        Assert.Equal(prg[^1], bus.Peek(0xFFFF));
        Assert.Equal(0, bus.Cycles);
        Assert.Equal(0, bus.PpuDots);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void PokeRamWritesTheMirroredRamWithoutACycle(string region)
    {
        (NesBus bus, _) = Build(region);

        bus.PokeRam(0x1802, 0x77);

        Assert.Equal(0x77, bus.Peek(0x0002));
        Assert.Equal(0, bus.Cycles);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AnUnmappedReadReturnsTheLastValueOnTheBus(string region)
    {
        (NesBus bus, byte[] prg) = Build(region);

        // The read of $5000 returns what the read before it left, which is the byte at $8003.
        byte before = bus.Read(0x8003);
        Assert.Equal(prg[3], before);
        Assert.Equal(prg[3], bus.Read(0x5000));

        // A write puts its value on the bus as well.
        bus.Write(0x5000, 0xAB);
        Assert.Equal(0xAB, bus.Read(0x5001));

        // And a read of RAM does, so the unmapped read after it gives that byte back.
        bus.PokeRam(0x0010, 0x3C);
        bus.Read(0x0010);
        Assert.Equal(0x3C, bus.Read(0x4018));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheControllerPortsDriveOnlyTheLowFiveBitsAndTheRestIsOpenBus(string region)
    {
        (NesBus bus, _) = Build(region);

        bus.Write(0x0000, 0x40);
        bus.Read(0x0000);

        Assert.Equal(0x40, bus.Read(0x4016));
        Assert.Equal(0x40, bus.Read(0x4017));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ThePpuRegisterWindowIsMirroredEveryEightBytes(string region)
    {
        (NesBus bus, _) = Build(region);

        // The stub keeps writes and reads the open bus; the decode is what is checked: a write
        // anywhere in the window must not touch RAM or the cartridge.
        bus.Write(0x3456, 0x99);
        bus.Write(0x2000, 0x98);

        Assert.Equal(0, bus.Peek(0x0000));
        Assert.Equal(0, bus.Peek(0x0456));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EachReadOrWriteIsOneCycleAndTheDotsFollowTheRegionsRatio(string region)
    {
        (NesBus bus, _) = Build(region);
        bool pal = region == "PAL";

        for (int n = 1; n <= 1000; n++)
        {
            if (n % 2 == 0)
            {
                bus.Write(0x0000, 0);
            }
            else
            {
                bus.Read(0x8000);
            }

            Assert.Equal(n, bus.Cycles);
            Assert.Equal(pal ? n * 16 / 5 : 3 * n, bus.PpuDots);
        }

        Assert.Equal(pal ? 3200 : 3000, bus.PpuDots);
    }

    [Fact]
    public void ThePalDotsRunThreeThreeThreeThreeFour()
    {
        (NesBus bus, _) = Build("PAL");
        var pattern = new List<long>();

        for (int i = 0; i < 10; i++)
        {
            long before = bus.PpuDots;
            bus.Read(0x8000);
            pattern.Add(bus.PpuDots - before);
        }

        Assert.Equal(new long[] { 3, 3, 3, 3, 4, 3, 3, 3, 3, 4 }, pattern);
    }

    [Fact]
    public void TheBusCarriesTheRegionItWasGiven()
    {
        Assert.Same(Region.Pal, Build("PAL").Bus.Region);
        Assert.Same(Region.Ntsc, Build("NTSC").Bus.Region);
    }

    [Fact]
    public void TheMachineTakesTheHeadersRegionUnlessTheCallerNamesOne()
    {
        var pal = new Nes(Cartridge.Load(TestCartridge.Nes2(1, 0, timing: 1)));
        var either = new Nes(Cartridge.Load(TestCartridge.Nes2(1, 0, timing: 2)));
        var forced = new Nes(Cartridge.Load(TestCartridge.Nes2(1, 0, timing: 1)), Region.Ntsc);
        var ines = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 0)));

        Assert.Same(Region.Pal, pal.Region);
        Assert.Same(Region.Pal, pal.Bus.Region);
        Assert.Same(Region.Ntsc, either.Region);
        Assert.Same(Region.Ntsc, forced.Region);
        Assert.Same(Region.Ntsc, ines.Region);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void PowerOnClearsRamAndTheCountersAndTheResetTakesSevenCycles(string region)
    {
        byte[] prg = TestCartridge.Filled(16384, 0xEA);
        prg[0x3FFC] = 0x34;
        prg[0x3FFD] = 0xC2;
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(TestCartridge.Ines1(1, 1)[..16], prg, new byte[8192])), RegionNamed(region));
        nes.Bus.PokeRam(0x0100, 0xFF);

        nes.PowerOn();

        Assert.Equal(0, nes.Bus.Peek(0x0100));
        Assert.Equal(7, nes.Bus.Cycles);
        Assert.Equal(7, nes.Cpu.Cycles);
        Assert.Equal(region == "PAL" ? 22 : 21, nes.Bus.PpuDots);
        Assert.Equal(0xC234, nes.Cpu.PC);
        Assert.Equal(0xFD, nes.Cpu.S);
        Assert.Equal(0x24, nes.Cpu.P);

        // A second power on starts the counters again.
        nes.PowerOn();
        Assert.Equal(7, nes.Bus.Cycles);
        Assert.Equal(7, nes.Cpu.Cycles);
    }

    [Fact]
    public void ResetKeepsRamAndPrgRamAndTheCpuResetsAgain()
    {
        byte[] prg = TestCartridge.Filled(16384, 0xEA);
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(TestCartridge.Ines1(1, 1)[..16], prg, new byte[8192])));
        nes.PowerOn();
        nes.Bus.Write(0x6000, 0x5A);
        nes.Bus.PokeRam(0x0200, 0x66);
        long cycles = nes.Bus.Cycles;

        nes.Reset();

        Assert.Equal(0x5A, nes.Bus.Peek(0x6000));
        Assert.Equal(0x66, nes.Bus.Peek(0x0200));
        Assert.Equal(cycles + 7, nes.Bus.Cycles);

        nes.PowerOn();
        Assert.Equal(0, nes.Bus.Peek(0x6000));
    }

    [Fact]
    public void StepReturnsTheCyclesTheInstructionTookAndRunPassesTheCount()
    {
        byte[] prg = TestCartridge.Filled(16384, 0xEA);
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(TestCartridge.Ines1(1, 1)[..16], prg, new byte[8192])));
        nes.PowerOn();
        long start = nes.Bus.Cycles;

        Assert.Equal(2, nes.Step());

        nes.Run(100);
        Assert.True(nes.Bus.Cycles - start >= 102);
        Assert.True(nes.Bus.Cycles - start < 104);
    }

    [Fact]
    public void TheBusDrivesTheCpuInterruptLinesFromTheChips()
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1)));
        nes.PowerOn();
        nes.Cpu.Nmi = true;
        nes.Cpu.Irq = true;

        nes.Bus.Read(0x8000);

        // The stubs hold neither line, so the bus has taken them low.
        Assert.False(nes.Cpu.Nmi);
        Assert.False(nes.Cpu.Irq);
    }
}
