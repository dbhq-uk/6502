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
    public void AReadOf4015LeavesTheOpenBusAsItWas(string region)
    {
        // bus.md 3: every read and write updates the bus's value except a read of $4015, which is
        // internal to the CPU; bit 5 of that read is the bus's. Nothing in the sound unit is set,
        // so the status bits are 0: a bus of $25 reads $20, and the bus still holds $25 after it.
        (NesBus bus, _) = Build(region);
        bus.Write(0x0000, 0x25);

        Assert.Equal(0x20, bus.Read(0x4015));
        Assert.Equal(0x25, bus.Read(0x4018));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheControllerPortsDriveOnlyBitZeroAndTheTopThreeBitsAreOpenBus(string region)
    {
        (NesBus bus, _) = Build(region);

        // The bus holds $FF. Nothing is pressed, so the pad gives bit 0 clear and bits 4 to 1 clear
        // too (nothing drives them): the result is $E0, the three open-bus bits, and not $FF.
        bus.Write(0x0000, 0xFF);
        Assert.Equal(0xE0, bus.Read(0x4016));
        bus.Write(0x0000, 0xFF);
        Assert.Equal(0xE0, bus.Read(0x4017));

        // And a bus of $40, the usual high byte, gives $40.
        bus.Write(0x0000, 0x40);
        Assert.Equal(0x40, bus.Read(0x4016));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ThePpuRegisterWindowIsMirroredEveryEightBytes(string region)
    {
        (NesBus bus, _) = Build(region);

        // OAMADDR through one mirror and OAMDATA through two others: each lands in OAM, so each is
        // the register $2003 or $2004 and not RAM, and the address moves on between them.
        bus.Write(0x200B, 0x10);
        bus.Write(0x3FFC, 0x55);
        bus.Write(0x2014, 0x66);
        bus.Write(0x2FFC, 0x77);

        Assert.Equal(0x55, bus.Ppu.Oam[0x10]);
        Assert.Equal(0x66, bus.Ppu.Oam[0x11]);
        Assert.Equal(0x63, bus.Ppu.Oam[0x12]); // $77 with the attribute byte's unused bits cleared

        // And none of them touched RAM, whose mirrors the same low bits would reach.
        Assert.Equal(0, bus.Peek(0x000B));
        Assert.Equal(0, bus.Peek(0x03FC));
        Assert.Equal(0, bus.Peek(0x0014));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AWriteTo3456ReachesPpuaddr(string region)
    {
        (NesBus bus, _) = Build(region);

        // bus.md worked example 1: $3456 is $2006.
        bus.Write(0x3456, 0x21);
        bus.Write(0x3456, 0x08);
        Assert.Equal(0x2108, bus.Ppu.V);

        bus.Write(0x2007, 0x6B);
        Assert.Equal(0x6B, bus.Ppu.PeekVram(0x2108));

        // And $200A and $3FFA are $2002: a read of either resets the write toggle.
        bus.Write(0x2005, 0x00);
        Assert.True(bus.Ppu.WriteToggle);
        bus.Read(0x3FFA);
        Assert.False(bus.Ppu.WriteToggle);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ThePpuHasItsOwnLatchApartFromTheCpusOpenBus(string region)
    {
        (NesBus bus, _) = Build(region);

        // The CPU's bus holds $AB; the PPU's latch still holds 0, and a write-only register gives it.
        bus.Write(0x5000, 0xAB);
        Assert.Equal(0x00, bus.Read(0x2000));

        // A PPU write fills the PPU's latch, and what the PPU drives is then on the CPU's bus too.
        bus.Write(0x2003, 0x5C);
        bus.Write(0x0000, 0x11);
        Assert.Equal(0x5C, bus.Read(0x2005));
        Assert.Equal(0x5C, bus.Read(0x5000));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void PeekOfStatusAndDataHasNoSideEffect(string region)
    {
        (NesBus bus, _) = Build(region);
        bus.Write(0x2006, 0x21);
        bus.Write(0x2006, 0x00);
        bus.Write(0x2007, 0x42);
        bus.Write(0x2006, 0x21);
        bus.Write(0x2006, 0x00);
        bus.Read(0x2007);
        while (!(bus.Ppu.Line == 241 && bus.Ppu.Dot > 1))
        {
            bus.Read(0x0000);
        }

        // Peeks: the flag stays set, the buffer and v do not move, and the cycle count holds.
        long cycles = bus.Cycles;
        Assert.Equal(0x80, bus.Peek(0x2002) & 0x80);
        Assert.Equal(0x80, bus.Peek(0x200A) & 0x80);
        Assert.Equal(0x42, bus.Peek(0x2007));
        Assert.Equal(0x42, bus.Peek(0x2007));
        Assert.Equal(0x2101, bus.Ppu.V);
        Assert.Equal(cycles, bus.Cycles);

        // The reads then do what the peeks did not.
        Assert.Equal(0x80, bus.Read(0x2002) & 0x80);
        Assert.Equal(0x00, bus.Peek(0x2002) & 0x80);
        Assert.Equal(0x42, bus.Read(0x2007));
        Assert.Equal(0x2102, bus.Ppu.V);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void PeekLeavesTheOpenBusLatchAlone(string region)
    {
        (NesBus bus, _) = Build(region);
        bus.Write(0x2003, 0x77);
        bus.Write(0x5000, 0xAB);

        foreach (ushort address in new ushort[] { 0x0000, 0x2000, 0x2002, 0x2004, 0x2007, 0x4015, 0x4016, 0x5000, 0x8000, 0xFFFF })
        {
            bus.Peek(address);
        }

        // The CPU's latch still holds the $AB written, and the PPU's still holds $77.
        Assert.Equal(0xAB, bus.Read(0x5000));
        Assert.Equal(0x77, bus.Read(0x2000));
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
    public void PowerOnPutsThePalFourthDotInEveryFifthCycleCountedFromPowerOn()
    {
        // timing.md 3, the model's choice: the accumulator is zero at power on, so the fourth dot
        // falls in cycles 5, 10, 15 ... from power on. The reset's 7 cycles have run, so the
        // next ones are cycles 8 to 17. A bus that was never powered on starts at zero too, which
        // is all ThePalDotsRunThreeThreeThreeThreeFour sees.
        var nes = IdleMachine(Region.Pal);
        nes.Bus.Read(0x0000);
        nes.PowerOn();
        Assert.Equal(7, nes.Bus.Cycles);

        var pattern = new List<long>();

        for (int i = 0; i < 10; i++)
        {
            long before = nes.Bus.PpuDots;
            nes.Bus.Read(0x0000);
            pattern.Add(nes.Bus.PpuDots - before);
        }

        Assert.Equal(new long[] { 3, 3, 4, 3, 3, 3, 3, 4, 3, 3 }, pattern);
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

    [Theory]
    [MemberData(nameof(PrgRamRule.Mappers), MemberType = typeof(PrgRamRule))]
    public void ABatteryBackedCartridgeKeepsItsPrgRamThroughTheResetButtonAndLosesItAtPowerOn(int mapper)
    {
        PrgRamRule.AssertResetKeepsItAndAPowerCycleClearsIt(mapper);
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

        // Neither the PPU (no VBlank, NMI off) nor the sound unit (its frame IRQ flag is clear at
        // power on) holds a line, so the bus has taken them low.
        Assert.False(nes.Cpu.Nmi);
        Assert.False(nes.Cpu.Irq);
    }

    [Fact]
    public void ResetResetsThePpu()
    {
        byte[] prg = TestCartridge.Filled(16384, 0xEA);
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(TestCartridge.Ines1(1, 1)[..16], prg, new byte[8192])));
        nes.PowerOn();
        nes.Bus.Write(0x2000, 0x80);
        nes.Bus.Write(0x2001, 0x1E);
        nes.Bus.Write(0x2005, 0x00);
        nes.Run(30_000);
        Assert.True(nes.Bus.Ppu.Frame >= 1);

        nes.Reset();

        // PPUCTRL, PPUMASK and the toggle are cleared, and the PPU starts again from the top of
        // the picture: the seven reset cycles leave it at line 0 dot 21, as at power on.
        Assert.False(nes.Bus.Ppu.RenderingEnabled);
        Assert.False(nes.Bus.Ppu.WriteToggle);
        Assert.False(nes.Bus.Ppu.Nmi);
        Assert.Equal((0, 21), (nes.Bus.Ppu.Line, nes.Bus.Ppu.Dot));
    }

    // A 16 KB program: at $C000 a number of NOPs, then enable NMI and count in an 11-cycle loop;
    // at $C100 an NMI handler that increments $0300 and returns. The vectors point at them. Each
    // NOP moves the loop two cycles against the PPU, and 2 has no factor in common with 11, so
    // eleven different counts put the NMI on every cycle of the loop once.
    private static Nes NmiMachine(Region region, int nops = 0)
    {
        byte[] prg = new byte[16384];
        int loop = 0xC000 + nops + 5;
        byte[] main =
        [
            .. Enumerable.Repeat((byte)0xEA, nops),
            0xA9, 0x80,                                 // LDA #$80
            0x8D, 0x00, 0x20,                           // STA $2000
            0xE8,                                       // loop: INX       2 cycles
            0xEA,                                       // NOP             2
            0xAD, 0x00, 0x02,                           // LDA $0200       4
            0x4C, (byte)loop, (byte)(loop >> 8),        // JMP loop        3
        ];
        byte[] handler =
        [
            0xEE, 0x00, 0x03,       // C100 INC $0300
            0x40,                   // C103 RTI
        ];
        main.CopyTo(prg, 0x0000);
        handler.CopyTo(prg, 0x0100);
        prg[0x3FFA] = 0x00;
        prg[0x3FFB] = 0xC1;
        prg[0x3FFC] = 0x00;
        prg[0x3FFD] = 0xC0;
        prg[0x3FFE] = 0x00;
        prg[0x3FFF] = 0xC1;
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(TestCartridge.Ines1(1, 1)[..16], prg, new byte[8192])), region);
        nes.PowerOn();
        return nes;
    }

    public static TheoryData<string, int> RegionsAndShifts()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            for (int nops = 0; nops < 11; nops++)
            {
                rows.Add(region, nops);
            }
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsAndShifts))]
    public void ThePpusNmiReachesTheCpuAtTheInstructionBoundaryAfterTheCycleThatRaisedIt(string region, int nops)
    {
        Region r = RegionNamed(region);
        var nes = NmiMachine(r, nops);

        // The cycle N in which the PPU runs line 241 dot 1, the dot that sets the flag. Dots
        // 0 to n-1 run in cycles 1 to c when c * numerator / denominator >= n.
        long setDot = 241L * 341 + 1;
        long n = 1;
        while (n * r.DotsNumerator / r.DotsDenominator <= setDot)
        {
            n++;
        }

        // The CPU sees a line changed during cycle N from cycle N + 1 on (the line as the cycle
        // began), so the instruction holding cycle N + 1 is the last before the NMI.
        long seen = n + 1;
        ushort returnAddress = 0;
        while (true)
        {
            long start = nes.Bus.Cycles;
            Assert.True(nes.Cpu.PC < 0xC100, $"the NMI was taken early, before cycle {seen}");
            nes.Step();
            Assert.Equal(nes.Bus.Cycles >= n, nes.Bus.Ppu.Nmi);
            if (start < seen && seen <= nes.Bus.Cycles)
            {
                returnAddress = nes.Cpu.PC;
                break;
            }
        }

        // The next step is the NMI: seven cycles, to the handler, with the return address pushed.
        Assert.Equal(7, nes.Step());
        Assert.Equal(0xC100, nes.Cpu.PC);
        ushort pushed = (ushort)(nes.Bus.Peek((ushort)(0x0100 + nes.Cpu.S + 2)) | nes.Bus.Peek((ushort)(0x0100 + nes.Cpu.S + 3)) << 8);
        Assert.Equal(returnAddress, pushed);

        // The handler runs and writes its byte, and RTI goes back.
        Assert.Equal(0, nes.Bus.Peek(0x0300));
        nes.Step();
        nes.Step();
        Assert.Equal(1, nes.Bus.Peek(0x0300));
        Assert.Equal(returnAddress, nes.Cpu.PC);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EnablingNmiDuringVblankIsTakenAfterTheNextInstruction(string region)
    {
        var nes = NmiMachine(RegionNamed(region));

        // Run the loop at $C005 with NMI still off, until the PPU is well into VBlank.
        nes.Cpu.PC = 0xC005;
        while (nes.Bus.Ppu.Line != 245)
        {
            nes.Step();
        }

        Assert.False(nes.Bus.Ppu.Nmi);

        // LDA #$80, STA $2000: the write in STA's last cycle raises NMI, which is taken after the
        // next instruction, INX, and not straight after the STA (ppu_vbl_nmi test 4, item 11).
        nes.Cpu.PC = 0xC000;
        nes.Step();
        nes.Step();
        Assert.True(nes.Bus.Ppu.Nmi);
        Assert.Equal(0xC005, nes.Cpu.PC);
        nes.Step();
        Assert.Equal(0xC006, nes.Cpu.PC);
        Assert.Equal(7, nes.Step());
        Assert.Equal(0xC100, nes.Cpu.PC);
    }

    // A machine whose program is never run: the tests drive the bus's cycles themselves.
    private static Nes IdleMachine(Region region)
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1)), region);
        nes.PowerOn();
        return nes;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithRenderingOffThePpusFrameLineAndDotAreTheBussDotCount(string region)
    {
        Region r = RegionNamed(region);
        var nes = IdleMachine(r);
        Ppu ppu = nes.Bus.Ppu;

        // Three and a bit frames, checked after every cycle: no frame is a dot short.
        long cycles = (long)(3.2 * r.Lines * Region.DotsPerLine * r.DotsDenominator / r.DotsNumerator);
        for (long i = 0; i < cycles; i++)
        {
            nes.Bus.Read(0x0000);
            long position = (ppu.Frame * r.Lines * Region.DotsPerLine) + (ppu.Line * Region.DotsPerLine) + ppu.Dot;
            Assert.Equal(nes.Bus.PpuDots, position);
        }

        Assert.Equal(3, ppu.Frame);
    }

    [Theory]
    [InlineData("NTSC", 27395)]
    [InlineData("PAL", 25683)]
    public void TheFirstVblankFlagIsSetInTheCycleThatRunsLine241Dot1(string region, long cycle)
    {
        // timing.md 2 and 4: power on is line 0 dot 0 with nothing ticked, and the flag is set as
        // the dot at line 241 dot 1 runs, the dot numbered 241 x 341 + 1 = 82182 from 0. On NTSC
        // cycle n runs dots 3(n - 1) to 3n - 1, so dot 82182 is the first of cycle 27395; the
        // wiki's PPU power up state says "around 27384" (known-differences.md). On PAL, after n
        // cycles 16n / 5 dots have run, rounded down, so dot 82182 runs in the first cycle after
        // which more than 82182 have run, cycle 25683.
        Region r = RegionNamed(region);
        int dot = (241 * Region.DotsPerLine) + 1;
        Assert.Equal(cycle, ((((long)dot + 1) * r.DotsDenominator) + r.DotsNumerator - 1) / r.DotsNumerator);

        var nes = IdleMachine(r);
        while ((nes.Bus.Peek(0x2002) & 0x80) == 0 && nes.Bus.Cycles < 2 * cycle)
        {
            nes.Bus.Read(0x0000);
        }

        Assert.Equal(cycle, nes.Bus.Cycles);
    }

    public static TheoryData<string, int, bool> RegionsAndSuppressionDots()
    {
        var rows = new TheoryData<string, int, bool>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            rows.Add(region, 2, false);
            rows.Add(region, 3, false);
            rows.Add(region, 4, true);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsAndSuppressionDots))]
    public void AStatusReadOnTheDotTheFlagIsSetOrTheNextReadsItAndStopsTheNmi(string region, int dot, bool nmi)
    {
        // bus.md 2: a $2002 read on the dot the flag is set, or one dot after, reads it set and the
        // NMI does not happen; two dots after, the NMI does. In the model the flag is set as dot
        // (241, 1) runs, so a read whose access lands with the PPU at (241, 2) is on that dot,
        // (241, 3) one after, and (241, 4) two after.
        Region r = RegionNamed(region);
        var nes = IdleMachine(r);
        Ppu ppu = nes.Bus.Ppu;
        nes.Bus.Write(0x2000, 0x80);

        // Two of a cycle's dots run before its access, so the cycle must start at (241, dot - 2).
        // On NTSC the cycles' starts fall on other dots in each of three frames. A PAL frame is
        // 33247.5 cycles, so its starts repeat every two frames; there the reset button, which
        // restarts the PPU at line 0 dot 0 while the bus's dot accumulator runs on, moves them.
        bool found = false;
        for (int attempt = 0; attempt < 10 && !found; attempt++)
        {
            long limit = nes.Bus.Cycles + (3L * r.Lines * Region.DotsPerLine);
            while (nes.Bus.Cycles < limit && !(found = ppu.Line == 241 && ppu.Dot == dot - 2))
            {
                nes.Bus.Read(0x0000);
            }

            if (!found)
            {
                // A different count of cycles each time, so the accumulator differs at the reset.
                for (int i = 0; i <= attempt; i++)
                {
                    nes.Bus.Read(0x0000);
                }

                nes.Reset();
                nes.Bus.Write(0x2000, 0x80);
            }
        }

        Assert.True(found, $"no cycle started at line 241 dot {dot - 2}");

        byte status = nes.Bus.Read(0x2002);
        Assert.True((status & 0x80) != 0, $"a read landing at (241, {dot}) read the flag clear");

        // The line the CPU saw in the read's cycle, and in the one after.
        bool seen = nes.Cpu.Nmi;
        nes.Bus.Read(0x0000);
        seen |= nes.Cpu.Nmi;
        Assert.Equal(nmi, seen);
    }

    // A machine running a program at $C000, with the reset vector at $C000 and the IRQ vector at
    // $C010.
    private static Nes IrqMachine(string region, byte[] main, byte[] handler)
    {
        byte[] prg = new byte[16384];
        main.CopyTo(prg, 0);
        handler.CopyTo(prg, 0x10);
        prg[0x3FFC] = 0x00;
        prg[0x3FFD] = 0xC0;
        prg[0x3FFE] = 0x10;
        prg[0x3FFF] = 0xC0;
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(TestCartridge.Ines1(1, 1)[..16], prg, new byte[8192])), RegionNamed(region));
        nes.PowerOn();
        return nes;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void PeekOf4015ShowsTheFrameIrqFlagAndLeavesItAReadClearsIt(string region)
    {
        (NesBus bus, _) = Build(region);
        int period = RegionNamed(region).FrameCounterFourStep[5];
        for (int i = 0; i < period + 10; i++)
        {
            bus.Read(0x0000);
        }

        Assert.Equal(0x40, bus.Peek(0x4015) & 0x40);
        Assert.Equal(0x40, bus.Peek(0x4015) & 0x40);
        Assert.Equal(0x40, bus.Read(0x4015) & 0x40);
        Assert.Equal(0, bus.Peek(0x4015) & 0x40);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheFrameIrqReachesTheCpuOncePerFrameWhenTheHandlerReads4015(string region)
    {
        // CLI, then JMP to itself; the handler counts in $00 and reads $4015 to clear the flag.
        Nes nes = IrqMachine(region, [0x58, 0x4C, 0x01, 0xC0], [0xE6, 0x00, 0xAD, 0x15, 0x40, 0x40]);
        int period = RegionNamed(region).FrameCounterFourStep[5];

        nes.Run((2 * period) + 100);

        Assert.Equal(2, nes.Bus.Peek(0x0000));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AWriteTo4017ReachesTheFrameCounter(string region)
    {
        // LDA #$40, STA $4017 (IRQ inhibit), CLI, then JMP to itself: no IRQ comes.
        Nes nes = IrqMachine(region, [0xA9, 0x40, 0x8D, 0x17, 0x40, 0x58, 0x4C, 0x06, 0xC0], [0xE6, 0x00, 0xAD, 0x15, 0x40, 0x40]);
        int period = RegionNamed(region).FrameCounterFourStep[5];

        nes.Run(3 * period);

        Assert.Equal(0, nes.Bus.Peek(0x0000));
        Assert.False(nes.Bus.Apu.Irq);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheFrameIrqReachesTheCpuInTheCycleAfterTheOneThatSetIt(string region)
    {
        // The NMI's rule (timing.md 3) applied to the IRQ: the CPU sees the line as the cycle
        // began. pal_apu_tests 08.irq_timing fails "too soon" with the line taken at the end of
        // the cycle and "too late" with it a cycle later (task 8, the journal).
        var nes = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1)), RegionNamed(region));
        nes.PowerOn();
        while (!nes.Bus.Apu.Irq)
        {
            Assert.True(nes.Bus.Cycles < 100_000, "the frame IRQ flag was never set");
            nes.Bus.Read(0x0000);
        }

        Assert.False(nes.Cpu.Irq);
        nes.Bus.Read(0x0000);
        Assert.True(nes.Cpu.Irq);
    }

    // The lazy PPU (the lazy chips plan, task 2). A board that watches the PPU's address bus is
    // told each address in the cycle it is put out, so the PPU is caught up every cycle for it,
    // and the addresses reach it with the same cycles as on the per-dot reference.
    [Theory]
    [MemberData(nameof(Regions))]
    public void ABoardThatWatchesThePpusAddressesHasThePpuCaughtUpEveryCycle(string region)
    {
        foreach (bool watches in new[] { true, false })
        {
            var lazyBoard = new StubBoard(watches);
            var referenceBoard = new StubBoard(watches);
            var lazy = new NesBus(lazyBoard, RegionNamed(region));
            var reference = new NesBus(referenceBoard, RegionNamed(region), new NesOptions { PerDotReference = true });
            var owed = new OwedDots(lazy);
            lazy.Observer = owed;
            foreach (NesBus bus in new[] { lazy, reference })
            {
                bus.PowerOn();
                bus.Write(0x2001, 0x1E);
                for (int i = 0; i < 70_000; i++)
                {
                    bus.Read(0x0000);
                }

                bus.Write(0x2000, 0x18);
                for (int i = 0; i < 40_000; i++)
                {
                    bus.Read(0x0000);
                }
            }

            if (watches)
            {
                Assert.Equal(0, owed.Most);
                Assert.True(referenceBoard.Reported.Count > 1000);
            }
            else
            {
                Assert.True(owed.Most > 0, "a board that does not watch had the PPU caught up every cycle");
                Assert.Empty(referenceBoard.Reported);
            }

            Assert.Equal(referenceBoard.Reported, lazyBoard.Reported);
            lazy.Ppu.CatchUp();
            Assert.Equal(CatchUpScenes.Hash(reference.Ppu, picture: true), CatchUpScenes.Hash(lazy.Ppu, picture: true));
        }
    }

    // Ruling T: a write to the cartridge can switch the pattern banks under the PPU, so the PPU is
    // caught up before it. A CNROM bank switch in the middle of a line is seen by the fetches of the
    // dots after the write and not by those before, in the lazy build as in the per-dot one.
    [Theory]
    [MemberData(nameof(Regions))]
    public void ACartridgeWriteMidLineIsSeenOnlyByTheDotsAfterIt(string region)
    {
        uint[]? referencePicture = null;
        foreach (bool oracle in new[] { true, false })
        {
            var bus = new NesBus(Cartridge.Load(CatchUpScenes.Cnrom()), RegionNamed(region), new NesOptions { PerDotReference = oracle });
            bus.PowerOn();

            // Every pattern byte of bank b is b: bank 0 draws the backdrop, and bank 3, $03 in both
            // planes, colour 3 in the last two columns of each tile and the backdrop in the rest.
            bus.Write(0x2006, 0x3F);
            bus.Write(0x2006, 0x00);
            foreach (byte colour in new byte[] { 0x0F, 0x16, 0x2A, 0x12 })
            {
                bus.Write(0x2007, colour);
            }

            bus.Write(0x2001, 0x0A);
            while (bus.Ppu.Frame < 1)
            {
                bus.Read(0x0000);
            }

            // No PPU access and no NMI in frame 1 before the write, so the lazy PPU owes it every
            // dot of the frame so far.
            while (bus.Ppu.Line < 100 || bus.Ppu.Dot < 128)
            {
                bus.Read(0x0000);
            }

            if (!oracle)
            {
                Assert.True(bus.Ppu.LogicalDots - bus.Ppu.CaughtUpDots > 100 * Region.DotsPerLine);
            }

            bus.Write(0x8000, 3);
            while (bus.Ppu.Frame < 2)
            {
                bus.Read(0x0000);
            }

            uint[] pixels = bus.Ppu.Screen.Pixels;
            uint backdrop = PpuPalette.Colour(0x0F, 0, RegionNamed(region).EmphasisSwapsRedAndGreen, false);
            uint three = PpuPalette.Colour(0x12, 0, RegionNamed(region).EmphasisSwapsRedAndGreen, false);
            Assert.All(Enumerable.Range(99 * 256, 256), i => Assert.Equal(backdrop, pixels[i]));
            Assert.All(Enumerable.Range(100 * 256, 100), i => Assert.Equal(backdrop, pixels[i]));
            Assert.All(Enumerable.Range((100 * 256) + 160, 96), i => Assert.Equal((i & 7) >= 6 ? three : backdrop, pixels[i]));
            Assert.All(Enumerable.Range(101 * 256, 139 * 256), i => Assert.Equal((i & 7) >= 6 ? three : backdrop, pixels[i]));
            if (referencePicture is null)
            {
                referencePicture = (uint[])pixels.Clone();
            }
            else
            {
                Assert.Equal(referencePicture, pixels);
            }
        }
    }

    // The bus catches the PPU up in the cycle whose dots reach NextEventDot, so the NMI line is
    // raised at that cycle's end, as the per-dot build raises it, and not before.
    [Theory]
    [MemberData(nameof(Regions))]
    public void TheBusCatchesThePpuUpInTheCycleThatReachesNextEventDot(string region)
    {
        var bus = new NesBus(Cartridge.Load(CatchUpScenes.Nrom()), RegionNamed(region));
        bus.PowerOn();
        Assert.Equal(long.MaxValue, bus.Ppu.NextEventDot);
        bus.Write(0x2000, 0x80);
        long vblank = bus.Ppu.NextEventDot;
        Assert.Equal(bus.Ppu.LogicalDots + (241 * Region.DotsPerLine) + 1 - ((bus.Ppu.Line * Region.DotsPerLine) + bus.Ppu.Dot) + 1, vblank);

        while (bus.Ppu.LogicalDots < vblank)
        {
            bus.Read(0x0000);
            if (bus.Ppu.LogicalDots < vblank)
            {
                Assert.True(bus.Ppu.CaughtUpDots < bus.Ppu.LogicalDots, "the PPU was caught up before its event");
            }
        }

        Assert.Equal(bus.Ppu.LogicalDots, bus.Ppu.CaughtUpDots);
        Assert.True(bus.Ppu.Nmi);
        Assert.NotEqual(vblank, bus.Ppu.NextEventDot);

        // Writing $2000 moves it: off, none; on again, the flag's clearing.
        bus.Write(0x2000, 0x00);
        Assert.Equal(long.MaxValue, bus.Ppu.NextEventDot);
        bus.Write(0x2000, 0x80);
        Assert.Equal(bus.Ppu.LogicalDots + (RegionNamed(region).PreRenderLine * Region.DotsPerLine) + 1 - ((bus.Ppu.Line * Region.DotsPerLine) + bus.Ppu.Dot) + 1, bus.Ppu.NextEventDot);
    }

    // Power on clears a board's CHR RAM, so the PPU runs the dots it is owed before the board is
    // reset, with the pattern bytes it would have fetched; the reset button too, before the chips.
    [Theory]
    [MemberData(nameof(Regions))]
    public void PowerOnAndResetCatchThePpuUpBeforeTheBoardIsReset(string region)
    {
        string? reference = null;
        foreach (bool oracle in new[] { true, false })
        {
            var bus = new NesBus(Cartridge.Load(TestCartridge.Banked(2, prgBanks: 2)), RegionNamed(region), new NesOptions { PerDotReference = oracle });
            var recorder = new CatchUpRecorder(bus);
            bus.Observer = recorder;
            bus.PowerOn();
            bus.Write(0x2006, 0x00);
            bus.Write(0x2006, 0x00);
            for (int i = 0; i < 0x2000; i++)
            {
                bus.Write(0x2007, (byte)((i * 13) + 5));
            }

            bus.Write(0x2001, 0x1E);
            while (bus.Ppu.Frame < 1 || bus.Ppu.Line < 100)
            {
                bus.Read(0x0000);
            }

            bus.Reset();
            while (bus.Ppu.Line < RegionNamed(region).PreRenderLine)
            {
                bus.Read(0x0000);
            }

            bus.Write(0x2001, 0x1E);
            while (bus.Ppu.Frame < 2 || bus.Ppu.Line < 120 || bus.Ppu.Dot < 100)
            {
                bus.Read(0x0000);
            }

            if (!oracle)
            {
                Assert.True(bus.Ppu.LogicalDots - bus.Ppu.CaughtUpDots > 1000);
            }

            bus.PowerOn();
            string record = string.Join("\n", recorder.Points);
            if (reference is null)
            {
                reference = record;
            }
            else
            {
                Assert.Equal(reference, record);
            }
        }
    }

    // A board for the bus alone: no PRG, 8 KB of pattern RAM, and a record of the addresses the PPU
    // tells it, if it says it watches.
    private sealed class StubBoard(bool watches) : IMapper
    {
        private readonly byte[] _chr = new byte[0x2000];

        public List<(ushort Address, long CpuCycle)> Reported { get; } = [];

        public Mirroring Mirroring => Mirroring.Vertical;

        public bool Irq => false;

        public bool WatchesPpuAddresses => watches;

        public bool CountsCpuCycles => false;

        public bool CanInterrupt => false;

        public byte[] PrgRam { get; } = [];

        public byte CpuRead(ushort address, byte openBus) => openBus;

        public void CpuWrite(ushort address, byte value)
        {
        }

        public byte PpuRead(ushort address) => (byte)(address * 7);

        public void PpuWrite(ushort address, byte value) => _chr[address & 0x1FFF] = value;

        public void PpuAddressChanged(ushort address, long cpuCycle) => Reported.Add((address, cpuCycle));

        public void CpuCycle()
        {
        }

        public void Reset(bool power)
        {
        }

        public void ClearPrgRam()
        {
        }
    }

    // The most dots the PPU was owed at a cycle's end, and at an access that sees it.
    private sealed class OwedDots(NesBus bus) : INesObserver
    {
        public long Most { get; private set; }

        public void Accessed(ushort address, bool write, byte value)
        {
            if (address < 0x4000)
            {
                Assert.Equal(bus.Ppu.LogicalDots, bus.Ppu.CaughtUpDots);
            }
        }

        public void CycleEnded(bool nmi, bool irq) => Most = Math.Max(Most, bus.Ppu.LogicalDots - bus.Ppu.CaughtUpDots);

        public void ChipsReset(bool power)
        {
        }

        public void DmcFetched()
        {
        }

        public void FrameEnded()
        {
        }
    }
}
