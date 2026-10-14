using Xunit;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The ULA's interrupt registers and the frame that times them (ula.md s1c, s5d, s6). Times are
/// 2 MHz cycles from power on; the tests drive the ULA's own catch-up directly, with no CPU.
/// </summary>
public class UlaInterruptTests
{
    // Status bits (s6a).
    private const int MasterIrq = 0x01;
    private const int PowerOnFlag = 0x02;
    private const int DisplayEnd = 0x04;
    private const int RealTimeClock = 0x08;
    private const int ReceiveFull = 0x10;
    private const int TransmitEmpty = 0x20;
    private const int HighTone = 0x40;

    // The clear register's bits (s6b).
    private const int ClearDisplayEnd = 0x10;
    private const int ClearRealTimeClock = 0x20;
    private const int ClearHighTone = 0x40;
    private const int ClearNmi = 0x80;

    private const int EnableRegister = 0;
    private const int ClearRegister = 5;
    private const int ControlRegister = 7;

    private static byte ModeBits(int mode) => (byte)(mode << 3);

    /// <summary>The first cycle in (<paramref name="from"/>, <paramref name="to"/>] at which the bit is set; it then clears the source.</summary>
    private static long FirstSet(Ula ula, int statusBit, int clearMask, long from, long to)
    {
        for (long c = from + 1; c <= to; c++)
        {
            ula.CatchUp(c);
            if ((ula.Status & statusBit) != 0)
            {
                ula.Write(ClearRegister, (byte)clearMask);
                return c;
            }
        }

        return -1;
    }

    [Fact]
    public void TheFirstReadOfTheStatusHasThePowerOnFlagAndTheSecondDoesNot()
    {
        // s6a: bit 7 is always 1; bit 1 is set at power on and cleared by the first read of $FE00.
        var ula = new Ula();
        byte first = ula.Read(0);
        Assert.NotEqual(0, first & 0x80);
        Assert.NotEqual(0, first & PowerOnFlag);
        byte second = ula.Read(0);
        Assert.NotEqual(0, second & 0x80);
        Assert.Equal(0, second & PowerOnFlag);
    }

    [Fact]
    public void ReadingTheStatusProperty_DoesNotClearThePowerOnFlag()
    {
        var ula = new Ula();
        Assert.NotEqual(0, ula.Status & PowerOnFlag);
        Assert.NotEqual(0, ula.Status & PowerOnFlag);
        ula.Read(0);
        Assert.Equal(0, ula.Status & PowerOnFlag);
    }

    [Fact]
    public void PowerOnSetsTheFlagAgain_AndBreakHasNoSuchCall()
    {
        // s6a: BREAK does not set the flag, which is how the OS tells them apart. s12 item 5: the
        // ULA has no reset of its own on BREAK, so only power on does.
        var ula = new Ula();
        ula.Read(0);
        Assert.Equal(0, ula.Status & PowerOnFlag);
        ula.PowerOn();
        Assert.NotEqual(0, ula.Status & PowerOnFlag);
    }

    [Fact]
    public void TransmitEmptyIsSetAtPowerOnAndNothingElseIs()
    {
        // s6a: transmit empty is "normally set"; its power-on value is open (s12 item 4) and the
        // model chooses set. Bit 7 is always 1, bit 1 is the power-on flag.
        var ula = new Ula();
        Assert.Equal(0x80 | PowerOnFlag | TransmitEmpty, ula.Status);
    }

    [Fact]
    public void TheRealTimeClockIsSetWhetherOrNotItIsEnabled_AndTheEnableOnlyGatesTheIrq()
    {
        // s6a: the status bits are set whether or not the interrupt is enabled.
        var ula = new Ula();
        ula.CatchUp(12_669);
        Assert.Equal(0, ula.Status & RealTimeClock);

        ula.CatchUp(12_670);
        Assert.NotEqual(0, ula.Status & RealTimeClock);
        Assert.Equal(0, ula.Status & MasterIrq);
        Assert.False(ula.Irq);

        ula.Write(EnableRegister, 0x08);
        Assert.NotEqual(0, ula.Status & MasterIrq);
        Assert.True(ula.Irq);

        // s6b: a write of 1 to bit 5 of $FE05 clears it.
        ula.Write(ClearRegister, ClearRealTimeClock);
        Assert.Equal(0, ula.Status & RealTimeClock);
        Assert.Equal(0, ula.Status & MasterIrq);
        Assert.False(ula.Irq);
    }

    [Fact]
    public void TheRealTimeClockFiresAt12670And52670OfEachFrame()
    {
        // s5d: RTC odd 12,670, RTC even 52,670 (exactly 40,000 on), and again 80,000 later.
        var ula = new Ula();
        Assert.Equal(12_670, FirstSet(ula, RealTimeClock, ClearRealTimeClock, 0, 80_000));
        Assert.Equal(52_670, FirstSet(ula, RealTimeClock, ClearRealTimeClock, 12_670, 80_000));
        Assert.Equal(92_670, FirstSet(ula, RealTimeClock, ClearRealTimeClock, 52_670, 160_000));
        Assert.Equal(132_670, FirstSet(ula, RealTimeClock, ClearRealTimeClock, 92_670, 160_000));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)] // acts as mode 4 (s5a)
    public void DisplayEndIsAt32736And72672InTheModesWithTwoHundredAndFiftySixLines(int mode)
    {
        var ula = new Ula();
        ula.Write(ControlRegister, ModeBits(mode));
        Assert.Equal(32_736, FirstSet(ula, DisplayEnd, ClearDisplayEnd, 0, 40_000));
        Assert.Equal(72_672, FirstSet(ula, DisplayEnd, ClearDisplayEnd, 32_736, 80_000));
        Assert.Equal(112_736, FirstSet(ula, DisplayEnd, ClearDisplayEnd, 72_672, 120_000));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void DisplayEndIsAt31968And71904InTheTwoHundredAndFiftyLineModes(int mode)
    {
        // s5d: 768 cycles earlier than in the other modes, because these show 250 lines, not 256.
        var ula = new Ula();
        ula.Write(ControlRegister, ModeBits(mode));
        Assert.Equal(31_968, FirstSet(ula, DisplayEnd, ClearDisplayEnd, 0, 40_000));
        Assert.Equal(71_904, FirstSet(ula, DisplayEnd, ClearDisplayEnd, 31_968, 80_000));
        Assert.Equal(111_968, FirstSet(ula, DisplayEnd, ClearDisplayEnd, 71_904, 120_000));
    }

    [Fact]
    public void TheTwoDisplayEndsAreThirtyNineThousandNineHundredAndThirtySixApart_NotForty()
    {
        // s5d: the fields are 39,936 and 40,064 cycles, so display end is 40,000 plus or minus 64
        // apart, and the clock is exactly 40,000 apart.
        var ula = new Ula();
        long first = FirstSet(ula, DisplayEnd, ClearDisplayEnd, 0, 40_000);
        long second = FirstSet(ula, DisplayEnd, ClearDisplayEnd, first, 80_000);
        long third = FirstSet(ula, DisplayEnd, ClearDisplayEnd, second, 120_000);
        Assert.Equal(39_936, second - first);
        Assert.Equal(40_064, third - second);
        Assert.Equal(80_000, third - first);
    }

    [Fact]
    public void ChangingTheModeBetweenTwoFramesChangesOnlyTheLaterFiring()
    {
        // The mode in force at the moment decides (s5d).
        var ula = new Ula();
        ula.Write(ControlRegister, ModeBits(4));
        Assert.Equal(32_736, FirstSet(ula, DisplayEnd, ClearDisplayEnd, 0, 40_000));
        ula.Write(ControlRegister, ModeBits(6));
        Assert.Equal(71_904, FirstSet(ula, DisplayEnd, ClearDisplayEnd, 32_736, 80_000));

        var other = new Ula();
        other.Write(ControlRegister, ModeBits(6));
        Assert.Equal(31_968, FirstSet(other, DisplayEnd, ClearDisplayEnd, 0, 40_000));
        other.Write(ControlRegister, ModeBits(4));
        Assert.Equal(72_672, FirstSet(other, DisplayEnd, ClearDisplayEnd, 31_968, 80_000));
    }

    [Fact]
    public void AModeOf7ActsAsMode4_AndTheModeComesFromBits5To3()
    {
        var ula = new Ula();
        for (int mode = 0; mode < 7; mode++)
        {
            ula.Write(ControlRegister, ModeBits(mode));
            Assert.Equal(mode, ula.Mode);
        }

        ula.Write(ControlRegister, ModeBits(7));
        Assert.Equal(4, ula.Mode);

        // The other bits of $FE07 (comms mode, motor, caps lock LED) are not the mode.
        ula.Write(ControlRegister, 0xC7);
        Assert.Equal(0, ula.Mode);
        ula.Write(ControlRegister, (byte)(0xC7 | ModeBits(5)));
        Assert.Equal(5, ula.Mode);
    }

    [Fact]
    public void EachClearBitClearsOnlyItsOwnSource()
    {
        // s6b: bit 7 NMI (nothing in the ULA), bit 6 high tone, bit 5 RTC, bit 4 display end.
        Ula Raised()
        {
            var ula = new Ula();
            ula.CatchUp(32_736); // RTC at 12,670 and display end both set
            ula.SetStatus(6);
            Assert.NotEqual(0, ula.Status & (RealTimeClock | DisplayEnd | HighTone));
            return ula;
        }

        Ula u = Raised();
        u.Write(ClearRegister, ClearDisplayEnd);
        Assert.Equal(0, u.Status & DisplayEnd);
        Assert.NotEqual(0, u.Status & RealTimeClock);
        Assert.NotEqual(0, u.Status & HighTone);

        u = Raised();
        u.Write(ClearRegister, ClearHighTone);
        Assert.Equal(0, u.Status & HighTone);
        Assert.NotEqual(0, u.Status & RealTimeClock);
        Assert.NotEqual(0, u.Status & DisplayEnd);

        u = Raised();
        u.Write(ClearRegister, ClearRealTimeClock);
        Assert.Equal(0, u.Status & RealTimeClock);
        Assert.NotEqual(0, u.Status & DisplayEnd);
        Assert.NotEqual(0, u.Status & HighTone);

        u = Raised();
        byte before = u.Status;
        u.Write(ClearRegister, ClearNmi);
        Assert.Equal(before, u.Status);

        // The ROM select bits of the same write are not the ULA's: bits 3 to 0 clear nothing.
        u = Raised();
        before = u.Status;
        u.Write(ClearRegister, 0x0F);
        Assert.Equal(before, u.Status);
    }

    [Fact]
    public void OnlyBits6To2OfTheEnableAreKept()
    {
        // s1c: bits 1, 0 and 7 of a write to $FE00 have no effect.
        var ula = new Ula();
        ula.CatchUp(12_670);
        Assert.NotEqual(0, ula.Status & RealTimeClock);

        ula.Write(EnableRegister, 0x83);
        Assert.False(ula.Irq);
        Assert.Equal(0, ula.Status & MasterIrq);
        Assert.NotEqual(0, ula.Status & PowerOnFlag); // not changed by the enable write

        ula.Write(EnableRegister, 0x08);
        Assert.True(ula.Irq);

        // A write replaces the whole enable: it does not add to it.
        ula.Write(EnableRegister, 0x04);
        Assert.False(ula.Irq);
        ula.Write(EnableRegister, 0xFF);
        Assert.True(ula.Irq);
        ula.Write(EnableRegister, 0x83);
        Assert.False(ula.Irq);
    }

    [Fact]
    public void ADisabledSourceDoesNotRaiseIrq_AndAnEnabledOneDoes()
    {
        var ula = new Ula();
        ula.Write(EnableRegister, 0x04); // display end only
        ula.CatchUp(12_670);
        Assert.False(ula.Irq); // RTC is set but not enabled
        ula.CatchUp(32_736);
        Assert.True(ula.Irq);
        ula.Write(ClearRegister, ClearDisplayEnd);
        Assert.False(ula.Irq);
    }

    [Fact]
    public void TheTapeStatusBitsAreSetAndClearedByTheTapeTasksThroughTheUla()
    {
        // Bits 4, 5 and 6 (receive full, transmit empty, high tone) belong to the tape (s6a).
        var ula = new Ula();
        ula.Write(EnableRegister, 0x10);
        Assert.False(ula.Irq);
        ula.SetStatus(4);
        Assert.NotEqual(0, ula.Status & ReceiveFull);
        Assert.True(ula.Irq);
        ula.ClearStatus(4);
        Assert.Equal(0, ula.Status & ReceiveFull);
        Assert.False(ula.Irq);

        ula.ClearStatus(5);
        Assert.Equal(0, ula.Status & TransmitEmpty);
        ula.SetStatus(5);
        Assert.NotEqual(0, ula.Status & TransmitEmpty);

        Assert.Throws<ArgumentOutOfRangeException>(() => ula.SetStatus(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ula.ClearStatus(2));
    }

    [Fact]
    public void NextEventIsTheNextCycleAtWhichSomethingHappens()
    {
        var ula = new Ula();
        Assert.Equal(12_670, ula.NextEvent);
        ula.CatchUp(12_669);
        Assert.Equal(12_670, ula.NextEvent);
        ula.CatchUp(12_670);
        Assert.Equal(32_736, ula.NextEvent); // power-on mode is 0: 256 lines
        ula.Write(ControlRegister, ModeBits(6));
        Assert.Equal(31_968, ula.NextEvent);
        ula.CatchUp(31_968);
        Assert.Equal(52_670, ula.NextEvent);
        ula.CatchUp(52_670);
        Assert.Equal(71_904, ula.NextEvent);
        ula.CatchUp(71_904);
        Assert.Equal(92_670, ula.NextEvent);
    }

    [Fact]
    public void ACatchUpThatSpansSeveralEventsRaisesEachOfThem()
    {
        // The bus catches up when it next looks, which can be after several events.
        var ula = new Ula();
        ula.CatchUp(100_000);
        Assert.NotEqual(0, ula.Status & (RealTimeClock | DisplayEnd));
        Assert.Equal(112_736, ula.NextEvent); // the next frame's odd display end; 92,670 has fired
    }

    // --- on the bus ---

    private static readonly byte[] Os = RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256);
    private static readonly byte[] Basic = RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256);

    private static ElectronBus NewBus() => new(new ElectronRoms(Os, Basic));

    private static void RunTo(ElectronBus bus, long cycle)
    {
        while (bus.Cycles < cycle)
        {
            bus.Read(0xC000); // an OS ROM read: one cycle
        }
    }

    [Fact]
    public void TheStatusRegisterAppearsInEverySixteenByteBlockAndAReadClearsThePowerOnFlag()
    {
        // s1c: the top nibble of the low byte is ignored.
        ElectronBus bus = NewBus();
        Assert.NotEqual(0, bus.Peek(0xFEA0) & PowerOnFlag);
        Assert.NotEqual(0, bus.Peek(0xFEA0) & PowerOnFlag); // a Peek is not a read
        Assert.NotEqual(0, bus.Read(0xFE50) & PowerOnFlag);
        Assert.Equal(0, bus.Read(0xFE00) & PowerOnFlag);
        Assert.Equal(0, bus.Peek(0xFEF0) & PowerOnFlag);
    }

    [Theory]
    [InlineData(0xFE07, 4)]
    [InlineData(0xFEA7, 5)]
    [InlineData(0xFEF7, 6)]
    public void AWriteToAMirrorOfTheControlRegisterReachesTheMode(int address, int mode)
    {
        // s1c: the registers repeat in every 16-byte block, so $FEA7 and $FEF7 are $FE07. The
        // mode is bits 5 to 3 of the byte written.
        ElectronBus bus = NewBus();
        Assert.Equal(0, bus.Ula.Mode);
        bus.Write((ushort)address, ModeBits(mode));
        Assert.Equal(mode, bus.Ula.Mode);
    }

    [Theory]
    [InlineData(0xFE02)]
    [InlineData(0xFEA2)]
    [InlineData(0xFE0F)]
    public void AReadOfAnUnreadableRegisterIsTheHighByteOfTheAddress(int address)
    {
        // s1b, s12 item 3: the model's rule for a read the ULA does not answer.
        ElectronBus bus = NewBus();
        Assert.Equal((byte)(address >> 8), bus.Read((ushort)address));
        Assert.Equal((byte)(address >> 8), bus.Peek((ushort)address));
    }

    [Fact]
    public void TheEnableIsWrittenInAnyBlock_AndTheInterruptReachesTheBusAtTheRightCycle()
    {
        ElectronBus bus = NewBus();
        bus.Write(0xFEF0, 0x08); // enable RTC through a mirror
        RunTo(bus, 12_669);
        Assert.False(bus.Irq);
        Assert.Equal(0, bus.Peek(0xFE00) & RealTimeClock);

        // The next access carries the clock past 12,670 and the ULA catches up before it is used.
        bus.Read(0xC000);
        Assert.True(bus.Cycles >= 12_670);
        Assert.True(bus.Irq);
        Assert.NotEqual(0, bus.Peek(0xFE00) & (RealTimeClock | MasterIrq));

        // Clearing it through $FE05 also clears the line, and does not move the paged ROM from 0
        // to anywhere it would not go (the ROM select reads the same write, s2a).
        bus.Write(0xFE05, ClearRealTimeClock);
        Assert.False(bus.Irq);
        Assert.Equal(0, bus.Peek(0xFE00) & RealTimeClock);
    }

    [Fact]
    public void ThePagedRomIsNotDroppedByAnInterruptClear_WhenBasicIsPagedIn()
    {
        // One write to $FE05 does both jobs: the select's acceptance rule stays in the bus (s2a)
        // and the same write clears the interrupt (s6b).
        ElectronBus bus = NewBus();
        bus.Write(0xFE05, 0x0C);
        bus.Write(0xFE05, 0x0A);
        RunTo(bus, 13_000);
        Assert.NotEqual(0, bus.Peek(0xFE00) & RealTimeClock);
        bus.Write(0xFE05, ClearRealTimeClock);
        Assert.Equal(10, bus.RomSlot);
        Assert.Equal(0, bus.Peek(0xFE00) & RealTimeClock);
    }

    [Fact]
    public void TheModeWrittenThroughTheBusReachesTheUla()
    {
        ElectronBus bus = NewBus();
        bus.Write(0xFE07, ModeBits(5));
        Assert.Equal(5, bus.Ula.Mode);
    }
}
