using Xunit;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Electron.Tests;

public class ElectronBusTests
{
    private static readonly byte[] Os = RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256);
    private static readonly byte[] Basic = RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256);

    private static ElectronBus NewBus() => new(new ElectronRoms(Os, Basic));

    /// <summary>A bus that counts the calls to <see cref="Tick"/>.</summary>
    private sealed class CountingBus(ElectronRoms roms) : ElectronBus(roms)
    {
        public long Ticks { get; private set; }

        protected internal override void Tick() => Ticks++;
    }

    [Fact]
    public void RamReadsBackAtBothEndsAndDoesNotAlias()
    {
        // ula.md s1a: $0000-$7FFF is 32 KB with no mirroring.
        ElectronBus bus = NewBus();
        bus.Write(0x0000, 0x11);
        bus.Write(0x7FFF, 0x22);
        Assert.Equal(0x11, bus.Read(0x0000));
        Assert.Equal(0x22, bus.Read(0x7FFF));
        Assert.Equal(0x00, bus.Read(0x4000));
        bus.Write(0x4000, 0x33);
        Assert.Equal(0x11, bus.Read(0x0000));
        Assert.Equal(0x33, bus.Read(0x4000));
    }

    [Fact]
    public void TheOperatingSystemIsReadOnlyAndHoldsTheVectors()
    {
        ElectronBus bus = NewBus();
        byte before = bus.Peek(0xC000);
        bus.Write(0xC000, (byte)(before ^ 0xFF));
        Assert.Equal(before, bus.Peek(0xC000));
        // ula.md s1a: RESET vector $D8D2.
        Assert.Equal(0xD2, bus.Peek(0xFFFC));
        Assert.Equal(0xD8, bus.Peek(0xFFFD));
        Assert.Equal(Os[0x3F00], bus.Peek(0xFF00));
    }

    [Fact]
    public void TheCreditsTextHiddenByIoIsNotReadable()
    {
        // ula.md s1a: image offsets $3C00-$3EFF are hidden by I/O. They hold the credits text.
        ElectronBus bus = NewBus();
        Assert.Equal((byte)'(', Os[0x3C00]);
        Assert.NotEqual(Os[0x3C00], bus.Peek(0xFC00));
        Assert.Equal(Os[0x3BFF], bus.Peek(0xFBFF));
    }

    [Fact]
    public void BasicIsPagedInBySlotTenAndEleven()
    {
        // ula.md s2a: the OS writes $0C and then the slot number. s2b: slots 10 and 11 are BASIC.
        ElectronBus bus = NewBus();
        bus.Write(0xFE05, 0x0C);
        bus.Write(0xFE05, 0x0A);
        Assert.Equal(10, bus.RomSlot);
        // The title "BASIC" starts at offset 9 of basic.rom; compared with the file, not typed.
        Assert.Equal(Basic[9], bus.Peek(0x8009));
        Assert.Equal((byte)'B', bus.Peek(0x8009));
        Assert.Equal(Basic[0x3FFF], bus.Peek(0xBFFF));

        bus.Write(0xFE05, 0x0B);
        Assert.Equal(11, bus.RomSlot);
        Assert.Equal(Basic[9], bus.Peek(0x8009));
    }

    [Fact]
    public void WritesToThePagedWindowAreIgnored()
    {
        ElectronBus bus = NewBus();
        bus.Write(0xFE05, 0x0C);
        bus.Write(0xFE05, 0x0A);
        byte before = bus.Peek(0x8009);
        bus.Write(0x8009, (byte)(before ^ 0xFF));
        Assert.Equal(before, bus.Peek(0x8009));
    }

    [Fact]
    public void TheSelectRegisterFollowsTheSheetsAcceptanceRule()
    {
        // ula.md s2a: with slot 8 to 11 paged in, a write is honoured only if its bit 3 is set.
        ElectronBus bus = NewBus();
        bus.Write(0xFE05, 0x0C);
        bus.Write(0xFE05, 0x0A);
        Assert.Equal(10, bus.RomSlot);

        bus.Write(0xFE05, 0x00); // the shape of an interrupt clear: bit 3 clear
        Assert.Equal(10, bus.RomSlot);

        // $0C selects slot 12 and so opens the register to a second write that can select any slot.
        bus.Write(0xFE05, 0x0C);
        Assert.Equal(12, bus.RomSlot);
        bus.Write(0xFE05, 0x03);
        Assert.Equal(3, bus.RomSlot);

        // From slots 0 to 7 or 12 to 15 the same clear write does change the slot to 0.
        bus.Write(0xFE05, 0x10);
        Assert.Equal(0, bus.RomSlot);
    }

    [Fact]
    public void ThePowerOnSlotIsZero() => Assert.Equal(0, NewBus().RomSlot);

    [Theory]
    [InlineData(0xFE15)]
    [InlineData(0xFEA5)]
    [InlineData(0xFEF5)]
    public void TheSelectRegisterAppearsInEverySixteenByteBlock(int address)
    {
        // ula.md s1a: SHEILA is only the ULA, repeated every 16 bytes.
        ElectronBus bus = NewBus();
        bus.Write((ushort)address, 0x0C);
        bus.Write((ushort)address, 0x0A);
        Assert.Equal(10, bus.RomSlot);
    }

    [Fact]
    public void AnEmptySlotReadsTheHighByteOfTheAddress()
    {
        // ula.md s12 item 3: not measured; the sheet recommends the high byte.
        ElectronBus bus = NewBus();
        bus.Write(0xFE05, 0x05);
        Assert.Equal(5, bus.RomSlot);
        Assert.Equal(0x81, bus.Peek(0x8123));
        Assert.Equal(0xB7, bus.Peek(0xB700));
    }

    [Fact]
    public void TheKeyboardSlotsReadZeroUntilTheKeyboardIsBuilt()
    {
        // Placeholder owned by task 4, which decodes the keyboard matrix here.
        ElectronBus bus = NewBus();
        bus.Write(0xFE05, 0x08);
        Assert.Equal(8, bus.RomSlot);
        Assert.Equal(0x00, bus.Peek(0xA000));
        bus.Write(0xFE05, 0x09);
        Assert.Equal(0x00, bus.Peek(0x9FFF));
    }

    [Fact]
    public void FredAndJimAndAnUnreadableUlaRegisterReadTheHighByteOfTheAddress()
    {
        // FRED and JIM have nothing fitted, and the ULA answers only the status ($FE00): the
        // model's rule is the high byte of the address (ula.md s1b, s12 item 3).
        ElectronBus bus = NewBus();
        Assert.Equal(0xFE, bus.Peek(0xFE01));
        Assert.Equal(0xFC, bus.Peek(0xFC70));
        Assert.Equal(0xFD, bus.Peek(0xFD10));
    }

    [Fact]
    public void ThePeekAndPokeTakeNoCycle()
    {
        ElectronBus bus = NewBus();
        bus.PokeRam(0x1234, 0x56);
        Assert.Equal(0x56, bus.Peek(0x1234));
        Assert.Equal(0, bus.Cycles);
        Assert.Throws<ArgumentOutOfRangeException>(() => bus.PokeRam(0x8000, 0));
    }

    [Fact]
    public void EachAccessCostsWhatItsOwnAddressSays()
    {
        // ula.md s4b. Every start here is on a 1 MHz boundary (even), so RAM and I/O cost 2 and
        // ROM 1; after the ROM read the count is odd, so the next RAM access costs 3.
        ElectronBus bus = NewBus();
        long Cost(Action access)
        {
            long before = bus.Cycles;
            access();
            return bus.Cycles - before;
        }

        Assert.Equal(2, Cost(() => bus.Read(0x0000)));      // RAM
        Assert.Equal(2, Cost(() => bus.Write(0x0000, 1)));  // RAM write
        Assert.Equal(2, Cost(() => bus.Read(0xFE00)));      // I/O
        Assert.Equal(1, Cost(() => bus.Read(0xC000)));      // OS ROM
        Assert.Equal(3, Cost(() => bus.Read(0x0000)));      // RAM from an odd count
        Assert.Equal(1, Cost(() => bus.Read(0xFBFF)));      // the last OS ROM byte before I/O
        Assert.Equal(3, Cost(() => bus.Read(0xFC00)));      // I/O from an odd count
        Assert.Equal(1, Cost(() => bus.Read(0xFF00)));      // OS ROM, the top page
    }

    [Fact]
    public void ThePagedWindowCostsByTheSlotThatIsPagedIn()
    {
        ElectronBus bus = NewBus();
        bus.Write(0xFE05, 0x0C);
        bus.Write(0xFE05, 0x0A); // BASIC: a ROM, 1 cycle
        Assert.Equal(0, bus.Cycles % 2);
        long before = bus.Cycles;
        bus.Read(0x8000);
        Assert.Equal(1, bus.Cycles - before);

        bus.Write(0xFE05, 0x0C); // an empty slot is a ROM position too
        bus.Write(0xFE05, 0x05);
        before = bus.Cycles;
        bus.Read(0x8000);
        Assert.Equal(1, bus.Cycles - before);

        bus.Write(0xFE05, 0x0C);
        bus.Write(0xFE05, 0x09); // the keyboard: a 1 MHz access (ula.md s3a)
        if (bus.Cycles % 2 != 0)
        {
            bus.Read(0xC000);
        }

        before = bus.Cycles;
        bus.Read(0x8000);
        Assert.Equal(2, bus.Cycles - before);
    }

    [Fact]
    public void TickRunsOncePerElapsedCycle()
    {
        var bus = new CountingBus(new ElectronRoms(Os, Basic));
        bus.Read(0x0000);   // 2
        bus.Read(0xC000);   // 1
        bus.Read(0x0000);   // 3
        bus.Write(0xFE05, 0x0C); // I/O from an even count: 2
        Assert.Equal(8, bus.Cycles);
        Assert.Equal(bus.Cycles, bus.Ticks);
    }

    [Fact]
    public void BasicCanBeLeftInOneSlotForTheBootTests()
    {
        // The test seam of task 5: BASIC in slot 10 only leaves slot 11 empty, which reads the
        // high byte of the address (s12 item 3), and the stock bus has it in both (s2b).
        var bus = new ElectronBus(new ElectronRoms(Os, Basic), 44_100, [10]);
        Select(bus, 10);
        Assert.Equal(Basic[0x0009], bus.Read(0x8009));
        Select(bus, 11);
        Assert.Equal(0x80, bus.Read(0x8009));

        ElectronBus stock = NewBus();
        Select(stock, 11);
        Assert.Equal(Basic[0x0009], stock.Read(0x8009));

        // The OS's sequence (s2a): $0C first, so the register accepts any slot, then the slot.
        static void Select(ElectronBus b, int slot)
        {
            b.Write(0xFE05, 0x0C);
            b.Write(0xFE05, (byte)slot);
            Assert.Equal(slot, b.RomSlot);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(16)]
    public void TheSeamRefusesASlotBasicCannotBeIn(int slot)
    {
        // s2b: there are 16 slots, and 8 and 9 are the keyboard, not a ROM image.
        Assert.Throws<ArgumentOutOfRangeException>(() => new ElectronBus(new ElectronRoms(Os, Basic), 44_100, [slot]));
    }
}
