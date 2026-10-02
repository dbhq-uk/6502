using Xunit;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.BbcMicro.Tests;

public class BbcBusTests
{
    private static readonly byte[] Os = RepoPaths.ReadChecked(Pins.BbcOsPath, Pins.BbcOsSha256);
    private static readonly byte[] Basic = RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256);
    private static readonly byte[] Dfs = RepoPaths.ReadChecked(Pins.BbcDfsPath, Pins.BbcDfsSha256);

    /// <summary>A bus that counts the cycles it gives to the other chips.</summary>
    private sealed class CountingBus() : BbcBus(new BbcRoms(Os, Basic, Dfs))
    {
        public int Ticks { get; private set; }

        protected override void Tick()
        {
            Ticks++;
            base.Tick();
        }
    }

    private static CountingBus NewBus() => new();

    /// <summary>Runs one bus cycle and says how many CPU cycles it took, stretch included.</summary>
    private static long Cost(CountingBus bus, Action cycle)
    {
        long before = bus.Cycles;
        int ticksBefore = bus.Ticks;
        cycle();
        long cost = bus.Cycles - before;
        Assert.Equal(cost, bus.Ticks - ticksBefore); // every cycle ticks the other chips exactly once
        return cost;
    }

    /// <summary>Moves Cycles to the wanted parity with RAM reads, which cost one cycle each.</summary>
    private static void AlignTo(CountingBus bus, int parity)
    {
        while ((bus.Cycles & 1) != parity)
        {
            bus.Read(0x0000);
        }
    }

    [Fact]
    public void RamReadsBackAndTheTopHalfOfTheRamDoesNotAliasTheBottom()
    {
        var bus = NewBus();
        bus.Write(0x0000, 0x11);
        bus.Write(0x4000, 0x22);
        bus.Write(0x7FFF, 0x33);

        Assert.Equal(0x11, bus.Read(0x0000));
        Assert.Equal(0x22, bus.Read(0x4000));
        Assert.Equal(0x33, bus.Read(0x7FFF));
        Assert.Equal(0x11, bus.Peek(0x0000));
    }

    [Fact]
    public void PokeRamLoadsRamWithoutACycle()
    {
        var bus = NewBus();
        bus.PokeRam(0x1234, 0x5A);

        Assert.Equal(0, bus.Cycles);
        Assert.Equal(0x5A, bus.Peek(0x1234));
        Assert.Equal(0, bus.Cycles);
    }

    [Fact]
    public void AWriteToTheOperatingSystemRomChangesNothing()
    {
        var bus = NewBus();
        byte before = bus.Peek(0xC000);
        bus.Write(0xC000, (byte)(before ^ 0xFF));
        bus.Write(0xFF00, 0x00);

        Assert.Equal(Os[0], before);
        Assert.Equal(before, bus.Peek(0xC000));
        Assert.Equal(Os[0x3F00], bus.Peek(0xFF00));
    }

    [Fact]
    public void TheOperatingSystemShowsAtC000AndFF00AndTheVectorsAreWhereTheFactSheetSays()
    {
        var bus = NewBus();

        Assert.Equal(0xCD, bus.Peek(0xFFFC));
        Assert.Equal(0xD9, bus.Peek(0xFFFD));
        Assert.Equal(Os[0], bus.Peek(0xC000));
        Assert.Equal(Os[0x3BFF], bus.Peek(0xFBFF)); // the last byte before the I/O hole
        Assert.Equal(Os[0x3F00], bus.Peek(0xFF00)); // the first byte after it
        Assert.Equal(Os[0x3FFC], bus.Read(0xFFFC));
    }

    [Fact]
    public void TheRomLatchPagesBasicDfsAndAnEmptySlot()
    {
        var bus = NewBus();

        bus.Write(0xFE30, 0x0F);
        Assert.Equal(15, bus.RomSlot);
        Assert.Equal((byte)'B', bus.Peek(0x8009)); // the title "BASIC" starts at $8009
        Assert.Equal(Basic[9], bus.Read(0x8009));

        bus.Write(0xFE3F, 0x0E);
        Assert.Equal(14, bus.RomSlot);
        Assert.Equal(Dfs[9], bus.Peek(0x8009));
        Assert.Equal(Dfs[0x3FFF], bus.Peek(0xBFFF));

        bus.Write(0xFE30, 0x03);
        Assert.Equal(3, bus.RomSlot);
        Assert.Equal(0x80, bus.Peek(0x8009)); // an empty slot reads the high byte of the address
        Assert.Equal(0xBF, bus.Peek(0xBFFF));
    }

    [Fact]
    public void TheRomLatchTakesTheLowFourBitsAndEveryAddressInItsBlock()
    {
        var bus = NewBus();

        bus.Write(0xFE38, 0xFF);
        Assert.Equal(15, bus.RomSlot);
        bus.Write(0xFE3B, 0xA2);
        Assert.Equal(2, bus.RomSlot);
    }

    [Fact]
    public void AWriteToPagedRomChangesNothing()
    {
        var bus = NewBus();
        bus.Write(0xFE30, 0x0F);
        bus.Write(0x8009, 0x00);
        bus.Write(0xBFFF, 0x00);

        Assert.Equal(Basic[9], bus.Peek(0x8009));
        Assert.Equal(Basic[0x3FFF], bus.Peek(0xBFFF));
    }

    [Fact]
    public void AbsentDevicesReadWhatTheOperatingSystemDependsOn()
    {
        var bus = NewBus();

        Assert.Equal(0xFF, bus.Read(0xFC00)); // FRED
        Assert.Equal(0xFF, bus.Read(0xFD00)); // JIM
        Assert.Equal(0xFE, bus.Read(0xFEA0)); // a fast device: the high byte of the address
        Assert.Equal(0xFE, bus.Read(0xFE20));
        Assert.Equal(0x00, bus.Read(0xFEC0)); // a slow device
        Assert.Equal(0, bus.Read(0xFEE0) & 1); // the Tube probe: bit 0 clear means no Tube
        Assert.Equal(0, bus.Read(0xFEC0) & 0x40); // the ADC busy flag, which via.md s2.5 needs clear

        // No 8271 yet: its status reads $FE, whose low two bits are set, so the DFS takes the
        // controller as missing and serves no call (DFS $B495-$B49A), and prints no banner.
        Assert.Equal(0xFE, bus.Read(0xFE80));
    }

    [Fact]
    public void TheCrtcHoldsWhatIsWrittenAndReadsBackOnlyItsReadableRegisters()
    {
        // video.md s1.2 and s1.3: even address = the address register, odd = the data register,
        // mirrored through $FE00-$FE07. R12 to R15 read back on the HD6845S, with the top two
        // bits of the high byte reading 0; R16 and R17 have no light pen strobe, so read 0.
        // Everything else is write only and reads as an absent slow device, $00 (bus.md s1d).
        var bus = NewBus();

        bus.Write(0xFE00, 12);
        bus.Write(0xFE01, 0xE8);
        bus.Write(0xFE06, 13); // a mirror of the address register
        bus.Write(0xFE07, 0x34);
        bus.Write(0xFE00, 14);
        bus.Write(0xFE01, 0x7F);
        bus.Write(0xFE00, 15);
        bus.Write(0xFE01, 0x56);
        bus.Write(0xFE00, 1);
        bus.Write(0xFE01, 80);

        bus.Write(0xFE00, 12);
        Assert.Equal(0x28, bus.Read(0xFE01));
        Assert.Equal(0x28, bus.Read(0xFE03)); // a mirror of the data register
        bus.Write(0xFE00, 13);
        Assert.Equal(0x34, bus.Read(0xFE01));
        bus.Write(0xFE00, 14);
        Assert.Equal(0x3F, bus.Read(0xFE01));
        bus.Write(0xFE00, 15);
        Assert.Equal(0x56, bus.Read(0xFE01));
        bus.Write(0xFE00, 16);
        Assert.Equal(0x00, bus.Read(0xFE01));
        bus.Write(0xFE00, 1);
        Assert.Equal(0x00, bus.Read(0xFE01)); // R1 is write only
        Assert.Equal(0x00, bus.Read(0xFE00)); // so is the address register
    }

    [Fact]
    public void TheVideoUlaTakesWritesAndReadsAsAnAbsentFastDevice()
    {
        // video.md s2.1: two write-only registers. A read of $FE20 is Econet's INTON, which is
        // not fitted, so it reads as an absent fast device does, $FE (bus.md s1d, measured).
        var bus = NewBus();
        bus.Write(0xFE20, 0x4B);
        bus.Write(0xFE21, 0x07);

        Assert.Equal(0xFE, bus.Read(0xFE20));
        Assert.Equal(0xFE, bus.Read(0xFE21));
        Assert.Equal(0xFE, bus.Read(0xFE2F));
    }

    [Fact]
    public void PeekHasNoCycleAndNoSideEffect()
    {
        var bus = NewBus();
        bus.Peek(0xFE40);
        bus.Peek(0xFC00);
        bus.Peek(0x8000);

        Assert.Equal(0, bus.Cycles);
        Assert.Equal(0, bus.Ticks);
        Assert.Equal(0xFF, bus.Peek(0xFD00));
    }

    [Fact]
    public void ASlowReadCostsTwoCyclesFromAnEvenCountAndThreeFromAnOddOne()
    {
        var bus = NewBus();
        AlignTo(bus, 0);
        Assert.Equal(2, Cost(bus, () => bus.Read(0xFE40)));

        AlignTo(bus, 1);
        Assert.Equal(3, Cost(bus, () => bus.Read(0xFE40)));
    }

    [Fact]
    public void BackToBackSlowAccessesCostTwoEachFromAnEvenCount()
    {
        var bus = NewBus();
        AlignTo(bus, 0);

        Assert.Equal(2, Cost(bus, () => bus.Read(0xFE40)));
        Assert.Equal(2, Cost(bus, () => bus.Read(0xFE41)));
        Assert.Equal(0, bus.Cycles & 1);
    }

    [Fact]
    public void AnOddStartCostsThreeAndTheNextSlowAccessCostsTwo()
    {
        var bus = NewBus();
        AlignTo(bus, 1);

        Assert.Equal(3, Cost(bus, () => bus.Read(0xFE60)));
        Assert.Equal(2, Cost(bus, () => bus.Read(0xFE60)));
    }

    [Fact]
    public void FastAccessesCostOneCycle()
    {
        var bus = NewBus();
        foreach (int parity in new[] { 0, 1 })
        {
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Read(0x1000)));        // RAM
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Read(0xC000)));        // OS ROM
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Read(0x8000)));        // paged ROM
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Write(0xFE20, 0x00))); // video ULA
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Write(0xFE30, 0x0F))); // ROM latch
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Read(0xFE80)));        // 8271
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Read(0xFEA0)));        // ADLC
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Read(0xFEE0)));        // Tube
            AlignTo(bus, parity);
            Assert.Equal(1, Cost(bus, () => bus.Read(0xFF00)));        // OS ROM above the I/O
        }
    }

    [Theory]
    [InlineData(0xFC00)] // FRED
    [InlineData(0xFD00)] // JIM
    [InlineData(0xFE00)] // CRTC
    [InlineData(0xFE08)] // ACIA
    [InlineData(0xFE10)] // serial ULA
    [InlineData(0xFE18)] // station ID: not established, taken as slow
    [InlineData(0xFE40)] // system VIA
    [InlineData(0xFE5F)] // system VIA mirror
    [InlineData(0xFE60)] // user VIA
    [InlineData(0xFE7F)] // user VIA mirror
    [InlineData(0xFEC0)] // ADC
    [InlineData(0xFEDF)] // ADC mirror
    public void EverySlowRangeStretchesAndEveryFastRangeDoesNot(int address)
    {
        var bus = NewBus();
        AlignTo(bus, 0);
        Assert.Equal(2, Cost(bus, () => bus.Read((ushort)address)));
    }

    [Theory]
    [InlineData(0xFE1F, 2)] // last slow address before the video ULA
    [InlineData(0xFE2F, 1)] // video ULA
    [InlineData(0xFE3F, 1)] // ROM latch
    [InlineData(0xFE9F, 1)] // 8271
    [InlineData(0xFEBF, 1)] // ADLC
    [InlineData(0xFEFF, 1)] // Tube
    [InlineData(0xFBFF, 1)] // OS ROM just below the I/O
    [InlineData(0xFF00, 1)] // OS ROM just above it
    public void TheEdgesOfTheSlowRangesAreWhereTheFactSheetPutsThem(int address, int expected)
    {
        var bus = NewBus();
        AlignTo(bus, 0);
        Assert.Equal(expected, Cost(bus, () => bus.Read((ushort)address)));
    }

    [Fact]
    public void AWriteToASlowAddressCostsTheSameAsARead()
    {
        var bus = NewBus();
        AlignTo(bus, 0);
        Assert.Equal(2, Cost(bus, () => bus.Write(0xFE40, 0x00)));

        AlignTo(bus, 1);
        Assert.Equal(3, Cost(bus, () => bus.Write(0xFE40, 0x00)));
    }

    [Fact]
    public void ADummyReadAndTheWriteAfterItAreEachJudgedByTheirOwnAddress()
    {
        // STA $FBF0,X with X=$50: the dummy read is at the unfixed $FB40 (OS ROM, fast),
        // the write is at $FC40 (FRED, slow). Even start: 1, which leaves the count odd, then 3.
        var bus = NewBus();
        AlignTo(bus, 0);
        Assert.Equal(1, Cost(bus, () => bus.Read(0xFB40)));
        Assert.Equal(3, Cost(bus, () => bus.Write(0xFC40, 0x00))); // odd after one cycle: 3

        // STA $FDF0,X with X=$40: the dummy read is at the unfixed $FD30 (JIM, slow),
        // the write is at $FE30 (the ROM latch, fast). Even start: 2 then 1.
        AlignTo(bus, 0);
        Assert.Equal(2, Cost(bus, () => bus.Read(0xFD30)));
        Assert.Equal(1, Cost(bus, () => bus.Write(0xFE30, 0x0F)));
        Assert.Equal(15, bus.RomSlot);

        // STA $FE40,X with X=$00: both cycles are slow, so 2 then 2 from an even count.
        AlignTo(bus, 0);
        Assert.Equal(2, Cost(bus, () => bus.Read(0xFE40)));
        Assert.Equal(2, Cost(bus, () => bus.Write(0xFE40, 0x00)));
    }

    [Fact]
    public void ReadModifyWriteOnASlowAddressStretchesEachOfItsThreeAccesses()
    {
        // ROL $FE48 from an even count: read 2, write the old value 2, write the new value 2.
        var bus = NewBus();
        AlignTo(bus, 0);
        long start = bus.Cycles;
        bus.Read(0xFE48);
        bus.Write(0xFE48, 0x00);
        bus.Write(0xFE48, 0x00);

        Assert.Equal(6, bus.Cycles - start);
    }

    [Fact]
    public void ReadModifyWriteOnASlowAddressFromAnOddCountCostsSeven()
    {
        // ROL $FE48 from an odd count: the read waits two and costs 3, which leaves the count
        // even, so each write then costs 2 (bus.md section 2b).
        var bus = NewBus();
        AlignTo(bus, 1);

        Assert.Equal(3, Cost(bus, () => bus.Read(0xFE48)));
        Assert.Equal(2, Cost(bus, () => bus.Write(0xFE48, 0x00)));
        Assert.Equal(2, Cost(bus, () => bus.Write(0xFE48, 0x00)));
    }

    /// <summary>A bus that records, at the start of every cycle, what the chips can see.</summary>
    private sealed class WatchingBus() : BbcBus(new BbcRoms(Os, Basic, Dfs))
    {
        public List<int> SlotsSeen { get; } = [];

        public List<byte> SystemIerSeen { get; } = [];

        protected override void Tick()
        {
            SlotsSeen.Add(RomSlot);
            SystemIerSeen.Add(SystemVia.Peek(0xE));
            base.Tick();
        }
    }

    [Fact]
    public void TheChipsTickBeforeTheAccessOfTheirCycleSoTheySeeTheOldValue()
    {
        var bus = new WatchingBus();

        bus.Write(0xFE30, 0x0F);
        Assert.Equal([0], bus.SlotsSeen);
        Assert.Equal(15, bus.RomSlot);

        bus.SlotsSeen.Clear();
        bus.Write(0xFE30, 0x0E);
        Assert.Equal([15], bus.SlotsSeen);

        while ((bus.Cycles & 1) != 1)
        {
            bus.Read(0x0000);
        }
        bus.SystemIerSeen.Clear();
        bus.Write(0xFE4E, 0xF2); // three cycles from an odd count, all of them before the write
        Assert.Equal([0x80, 0x80, 0x80], bus.SystemIerSeen);
        Assert.Equal(0xF2, bus.SystemVia.Peek(0xE));

        bus.SystemIerSeen.Clear();
        bus.Read(0x0000);
        Assert.Equal([0xF2], bus.SystemIerSeen);
    }
}
