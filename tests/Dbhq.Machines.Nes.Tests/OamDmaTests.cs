using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// OAM DMA, <c>docs/nes/facts/bus.md</c> section 5, in both regions. A write of a page to
/// <c>$4014</c> copies it to OAM through <c>$2004</c> and halts the CPU on its next read: one
/// halt cycle, one alignment cycle if the next is not a get, then 256 get and put pairs. That is 513
/// or 514 stolen cycles, not counting the write; the CPU then makes its read again, which is the
/// cycle after them. The model makes the even cycles gets, so a write that lands on an even cycle
/// costs 513 and one on an odd cycle 514 (bus.md 5, worked example 3 and open item 1).
/// </summary>
public class OamDmaTests
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

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

    private static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    // A machine whose program is never run: the tests drive the bus's cycles themselves.
    private static Nes IdleMachine(string region)
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1)), RegionNamed(region));
        nes.PowerOn();
        return nes;
    }

    // A machine that runs code at $C000, with the reset vector pointing to it.
    private static Nes ProgramMachine(string region, params byte[] code)
    {
        byte[] prg = new byte[16384];
        code.CopyTo(prg, 0);
        prg[0x3FFC] = 0x00;
        prg[0x3FFD] = 0xC0;
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(TestCartridge.Ines1(1, 1)[..16], prg, new byte[8192])), RegionNamed(region));
        nes.PowerOn();
        return nes;
    }

    // RAM page $02 holds 255 - i, so a copy that is off by one place or by a page shows.
    private static void FillPage(Nes nes, int page, Func<int, byte> value)
    {
        for (int i = 0; i < 256; i++)
        {
            nes.Bus.PokeRam((ushort)((page << 8) + i), value(i));
        }
    }

    // What OAM holds after a copy: bits 4 to 2 of an attribute byte do not exist (ppu.md 4).
    private static byte AsStored(int oamIndex, byte value) => (oamIndex & 3) == 2 ? (byte)(value & 0xE3) : value;

    // Writes $4014 in a cycle of the given parity: the cycle's number, which the bus's count is
    // after it, is even for 0 and odd for 1.
    private static void WriteDmaOnParity(Nes nes, int parity, byte page)
    {
        while ((nes.Bus.Cycles + 1) % 2 != parity)
        {
            nes.Bus.Read(0x0000);
        }

        nes.Bus.Write(0x4014, page);
        Assert.Equal(parity, (int)(nes.Bus.Cycles % 2));
    }

    [Theory]
    [MemberData(nameof(RegionsAndParities))]
    public void ACopyStealsFiveHundredAndThirteenCyclesFromAnEvenStartAndFiveHundredAndFourteenFromAnOddOne(string region, int parity)
    {
        Nes nes = IdleMachine(region);
        FillPage(nes, 2, i => (byte)(255 - i));
        nes.Bus.Write(0x2003, 0x00);
        WriteDmaOnParity(nes, parity, 0x02);

        // The write itself is one cycle and starts nothing: the CPU's next read is where it halts.
        long start = nes.Bus.Cycles;
        nes.Bus.Read(0x0000);

        // The stolen cycles, then the CPU's own read once the copy is done.
        long stolen = parity == 0 ? 513 : 514;
        Assert.Equal(stolen + 1, nes.Bus.Cycles - start);

        for (int i = 0; i < 256; i++)
        {
            Assert.Equal(AsStored(i, (byte)(255 - i)), nes.Bus.Ppu.Oam[i]);
        }
    }

    [Theory]
    [MemberData(nameof(RegionsAndParities))]
    public void ThePpuRunsThroughTheStallAtTheRegionsRatio(string region, int parity)
    {
        Region r = RegionNamed(region);
        Nes nes = IdleMachine(region);
        Ppu ppu = nes.Bus.Ppu;
        nes.Bus.Write(0x2003, 0x00);
        WriteDmaOnParity(nes, parity, 0x02);

        long cycles = nes.Bus.Cycles;
        long dots = nes.Bus.PpuDots;
        long position = Position(ppu, r);
        nes.Bus.Read(0x0000);

        long n = nes.Bus.Cycles - cycles;
        Assert.Equal(parity == 0 ? 514 : 515, n);

        // n cycles owe n * 3 dots (NTSC) or n * 16 / 5 (PAL), to within the one the accumulator holds.
        long owed = n * r.DotsNumerator / r.DotsDenominator;
        long ran = nes.Bus.PpuDots - dots;
        Assert.InRange(ran, owed, owed + 1);
        if (region == "NTSC")
        {
            Assert.Equal(3 * n, ran);
        }

        // And the PPU really moved by them: its own position, not only the bus's count.
        Assert.Equal(ran, Position(ppu, r) - position);
    }

    private static long Position(Ppu ppu, Region r) => (ppu.Frame * r.Lines * Region.DotsPerLine) + (ppu.Line * Region.DotsPerLine) + ppu.Dot;

    [Theory]
    [MemberData(nameof(RegionsAndParities))]
    public void TheCpuMakesNoInstructionFetchInTheStallAndTheNextInstructionRunsOnce(string region, int parity)
    {
        // The prefix is 2 cycles (NOP) or 3 (LDA $00), so the STA lands on either parity.
        byte[] prefix = parity == 0 ? [0xEA] : [0xA5, 0x00];
        byte[] code = [.. prefix, 0xA9, 0x02, 0x8D, 0x14, 0x40, 0xEE, 0x00, 0x03, 0xEA];
        Nes nes = ProgramMachine(region, code);
        FillPage(nes, 2, i => (byte)i);

        // Run the prefix, the LDA and the STA, which is 4 cycles and leaves the DMA waiting.
        nes.Step();
        nes.Step();
        nes.Step();
        ushort pc = nes.Cpu.PC;
        Assert.Equal(0xC000 + code.Length - 4, pc);
        long start = nes.Bus.Cycles;
        long stolen = nes.Bus.Cycles % 2 == 0 ? 513 : 514;

        // INC $0300 is 6 cycles. Its first read, the opcode fetch, is the one that is halted, and the
        // stall comes before it: nothing else is fetched, and the INC runs once.
        int cycles = nes.Step();

        Assert.Equal(stolen + 6, cycles);
        Assert.Equal(start + stolen + 6, nes.Bus.Cycles);
        Assert.Equal(pc + 3, nes.Cpu.PC);
        Assert.Equal(1, nes.Bus.Peek(0x0300));
        Assert.Equal(0, nes.Bus.Peek(0x0301));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheCopyStartsAtTheOamAddressAndWrapsIntoOamWhenItIsFour(string region)
    {
        Nes nes = IdleMachine(region);
        FillPage(nes, 2, i => (byte)(i + 1));
        nes.Bus.Write(0x2003, 0x04);

        nes.Bus.Write(0x4014, 0x02);
        nes.Bus.Read(0x0000);

        // Source byte i lands at OAM address (4 + i) mod 256: the first four sources wrap to 252 to 255
        // after the run reaches the end, and the page's last four fill OAM 0 to 3.
        for (int i = 0; i < 256; i++)
        {
            int target = (4 + i) & 0xFF;
            Assert.Equal(AsStored(target, (byte)(i + 1)), nes.Bus.Ppu.Oam[target]);
        }

        Assert.Equal(AsStored(0, 253), nes.Bus.Ppu.Oam[0]);

        // 256 writes take OAMADDR round to where it was.
        Assert.Equal(nes.Bus.Ppu.Oam[4], nes.Bus.Peek(0x2004));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ACopyDoesNotResetTheOamAddressToZeroFirst(string region)
    {
        Nes nes = IdleMachine(region);
        FillPage(nes, 2, i => (byte)(i | 0x80));
        nes.Bus.Write(0x2003, 0xFC);

        nes.Bus.Write(0x4014, 0x02);
        nes.Bus.Read(0x0000);

        // From $FC: the first four go to 252 to 255, and the rest to 0 upward.
        Assert.Equal(0x80, nes.Bus.Ppu.Oam[0xFC]);
        Assert.Equal(0x84, nes.Bus.Ppu.Oam[0]);
        Assert.Equal(0xFF, nes.Bus.Ppu.Oam[0xFB]);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ASecondWriteBeforeTheCopyRunsReplacesThePageAsAReadModifyWriteMakes(string region)
    {
        Nes nes = IdleMachine(region);
        FillPage(nes, 2, i => (byte)(0x20 + (i & 0x0F)));
        FillPage(nes, 3, i => (byte)(0x30 + (i & 0x0F)));
        nes.Bus.Write(0x2003, 0x00);

        // INC $4014 writes the old value and then the new one; the copy is of the second page.
        nes.Bus.Write(0x4014, 0x02);
        nes.Bus.Write(0x4014, 0x03);
        long start = nes.Bus.Cycles;
        nes.Bus.Read(0x0000);

        Assert.Equal(0x30, nes.Bus.Ppu.Oam[0]);
        Assert.Equal(0x3F, nes.Bus.Ppu.Oam[0x0F]);
        Assert.True(nes.Bus.Cycles - start is 514 or 515);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ACopyRunsOnTheNextReadAndOnlyOnce(string region)
    {
        Nes nes = IdleMachine(region);
        nes.Bus.Write(0x4014, 0x02);

        long start = nes.Bus.Cycles;
        nes.Bus.Read(0x0000);
        Assert.True(nes.Bus.Cycles - start >= 514);

        // The page was used up: the next read is one cycle.
        start = nes.Bus.Cycles;
        nes.Bus.Read(0x0000);
        Assert.Equal(1, nes.Bus.Cycles - start);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AWriteIsNeverHaltedSoAnotherWriteRunsBeforeTheCopy(string region)
    {
        Nes nes = IdleMachine(region);
        nes.Bus.Write(0x4014, 0x02);

        long start = nes.Bus.Cycles;
        nes.Bus.Write(0x0000, 0x11);
        nes.Bus.Write(0x0001, 0x22);

        // The CPU is halted only on a read (bus.md 5): both writes took one cycle each.
        Assert.Equal(2, nes.Bus.Cycles - start);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheHaltedReadIsRepeatedOnTheStolenCyclesAndMadeAgainAfterThem(string region)
    {
        // $2002 is a read that clears the VBlank flag. The halted read repeats, so the flag the
        // CPU's own read finds is already cleared by the stolen reads: the CPU reads it clear.
        Nes nes = IdleMachine(region);
        nes.Bus.Write(0x2000, 0x00);
        while (!(nes.Bus.Ppu.Line == 245))
        {
            nes.Bus.Read(0x0000);
        }

        Assert.Equal(0x80, nes.Bus.Peek(0x2002) & 0x80);
        nes.Bus.Write(0x4014, 0x02);

        byte status = nes.Bus.Read(0x2002);

        Assert.Equal(0x00, status & 0x80);
    }

    [Theory]
    [MemberData(nameof(RegionsAndParities))]
    public void AHaltOnAPadReadClocksThePadOnceForTheStolenReadsAndOnceMoreForTheCpusOwn(string region, int parity)
    {
        // bus.md 6 and 7: "Controllers see one clock for each run of consecutive reads". The halt
        // and the alignment cycle repeat the read in consecutive cycles, which is one clock, and the
        // CPU's own read after the copy is the second. Held: B only. A read with no DMA is A (0),
        // then B (1). With the DMA in front the repeated reads take A and the CPU reads B.
        Nes nes = IdleMachine(region);
        nes.SetButtons(0, 0x02);
        nes.Bus.Write(0x4016, 1);
        nes.Bus.Write(0x4016, 0);
        nes.Bus.Write(0x0000, 0x00);
        WriteDmaOnParity(nes, parity, 0x02);

        byte read = nes.Bus.Read(0x4016);

        Assert.Equal(1, read & 1);
        Assert.Equal(0, nes.Bus.Peek(0x4016) & 1);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithRenderingOnTheCopyDoesNotWriteOam(string region)
    {
        // bus.md 5 and ppu.md 4: a $2004 write during rendering does not reach OAM, and that
        // includes OAM DMA. The address moves on in steps of four.
        Nes nes = IdleMachine(region);
        FillPage(nes, 2, _ => 0x55);
        nes.Bus.Write(0x2001, 0x1E);
        while (nes.Bus.Ppu.Line != 20)
        {
            nes.Bus.Read(0x0000);
        }

        nes.Bus.Write(0x4014, 0x02);
        nes.Bus.Read(0x0000);

        Assert.True(nes.Bus.Ppu.Line < 240, "the copy was meant to run inside the picture");
        Assert.All(nes.Bus.Ppu.Oam, b => Assert.Equal(0, b));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheResetButtonDropsACopyThatWasWaiting(string region)
    {
        Nes nes = IdleMachine(region);
        nes.Bus.Write(0x4014, 0x02);
        long before = nes.Bus.Cycles;

        nes.Reset();

        // The reset sequence is seven cycles and no copy ran in it.
        Assert.Equal(before + 7, nes.Bus.Cycles);
        long start = nes.Bus.Cycles;
        nes.Bus.Read(0x0000);
        Assert.Equal(1, nes.Bus.Cycles - start);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void PowerOnDropsACopyThatWasWaiting(string region)
    {
        Nes nes = IdleMachine(region);
        nes.Bus.Write(0x4014, 0x02);

        nes.PowerOn();

        Assert.Equal(7, nes.Bus.Cycles);
        nes.Bus.Read(0x0000);
        Assert.Equal(8, nes.Bus.Cycles);
    }
}
