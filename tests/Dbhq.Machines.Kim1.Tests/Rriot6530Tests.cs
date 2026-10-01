using Xunit;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// The 6530 on its own, clocked by hand. Register numbers are address lines
/// A0 to A3, so 0x06 is the KIM-1's $1706 and 0x0E is $170E.
/// </summary>
public sealed class Rriot6530Tests
{
    private static Rriot6530 Chip() => new(new byte[Rriot6530.RomSize]);

    public static TheoryData<int, int> Prescales => new() { { 0, 1 }, { 1, 8 }, { 2, 64 }, { 3, 1024 } };

    /// <summary>
    /// Written N at divide by k, the timer reads N - 1 on the next clock, drops
    /// once every k clocks, and passes zero, setting the flag, on clock
    /// N x k + 1. After that it falls by one a clock.
    /// </summary>
    [Theory]
    [MemberData(nameof(Prescales))]
    public void TheTimerCountsDownAndSetsItsFlagOnTheRightClockAtEachPrescale(int select, int divide)
    {
        const int start = 5;
        var chip = Chip();
        chip.WriteRegister(0x04 | select, start);
        int expiry = start * divide + 1;
        for (int t = 1; t <= expiry + 300; t++)
        {
            chip.Tick();
            int expected = t < expiry ? start - 1 - (t - 1) / divide : 0xFF - (t - expiry);
            Assert.True((expected & 0xFF) == Peek(chip), $"divide by {divide}, clock {t}: expected ${expected & 0xFF:X2}, read ${Peek(chip):X2}");
            Assert.True(chip.TimerFlag == t >= expiry, $"divide by {divide}, clock {t}: flag {chip.TimerFlag}");
            Assert.Equal(t >= expiry ? 0x80 : 0x00, chip.ReadRegister(0x07));
        }
    }

    /// <summary>The data sheet's worked example: 52 at divide by 8.</summary>
    [Fact]
    public void TheDataSheetsWorkedExampleHolds()
    {
        var chip = Chip();
        chip.WriteRegister(0x05, 52);
        var reads = new Dictionary<int, byte>();
        bool flagBefore417 = false;
        for (int t = 1; t <= 500; t++)
        {
            chip.Tick();
            reads[t] = Peek(chip);
            flagBefore417 |= t < 417 && chip.TimerFlag;
        }

        Assert.Equal(25, reads[213]);
        Assert.Equal(0, reads[415]);
        Assert.Equal(0xFF, reads[417]);
        Assert.False(flagBefore417);

        // "If after interrupt the timer is read and a value of 11100100 is
        // read, the time since interrupt is 28T": 416T + 28T = 444T.
        Assert.Equal(0b1110_0100, reads[444]);

        // "10101100 ... two's complement is 84, 84 + (52 x 8) = 500."
        Assert.Equal(0b1010_1100, reads[500]);
    }

    [Fact]
    public void ReadingTheTimerClearsTheFlagButNotOnTheClockItIsSet()
    {
        var chip = Chip();
        chip.WriteRegister(0x04, 2);
        chip.Tick();
        chip.Tick();
        chip.Tick();
        Assert.True(chip.TimerFlag);
        Assert.Equal(0xFF, chip.ReadRegister(0x06));
        Assert.True(chip.TimerFlag);

        chip.Tick();
        chip.ReadRegister(0x06);
        Assert.False(chip.TimerFlag);
    }

    [Fact]
    public void ReadingTheFlagDoesNotClearIt()
    {
        var chip = Chip();
        chip.WriteRegister(0x04, 0);
        chip.Tick();
        chip.Tick();
        Assert.Equal(0x80, chip.ReadRegister(0x07));
        Assert.Equal(0x80, chip.ReadRegister(0x07));
        Assert.True(chip.TimerFlag);
    }

    [Fact]
    public void WritingTheTimerClearsTheFlag()
    {
        var chip = Chip();
        chip.WriteRegister(0x04, 0);
        chip.Tick();
        Assert.True(chip.TimerFlag);
        chip.WriteRegister(0x04, 9);
        Assert.False(chip.TimerFlag);
    }

    /// <summary>KIM-1 User Manual, H-6: a read after zero "will restore the divide ratio to its previously programmed value".</summary>
    [Fact]
    public void ReadingTheTimerAfterItPassedZeroRestoresItsPrescale()
    {
        var chip = Chip();
        chip.WriteRegister(0x05, 1);
        for (int t = 0; t < 9; t++)
        {
            chip.Tick();
        }

        Assert.True(chip.TimerFlag);
        chip.Tick();
        byte count = chip.ReadRegister(0x06);

        // Back at divide by 8: seven more clocks leave the count alone, the eighth drops it.
        for (int t = 0; t < 7; t++)
        {
            chip.Tick();
            Assert.Equal(count, Peek(chip));
        }

        chip.Tick();
        Assert.Equal((byte)(count - 1), Peek(chip));
    }

    [Fact]
    public void AddressLineA3EnablesTheInterruptOnPB7()
    {
        var chip = Chip();
        chip.WriteRegister(0x0C, 0);
        chip.Tick();
        Assert.True(chip.IrqActive);
        Assert.Equal(0, chip.PortBPins & 0x80);

        chip.WriteRegister(0x04, 0);
        chip.Tick();
        Assert.True(chip.TimerFlag);
        Assert.False(chip.IrqActive);
        Assert.Equal(0x80, chip.PortBPins & 0x80);

        // Reading the flag leaves the enable alone; reading the count with A3 high sets it.
        chip.ReadRegister(0x0F);
        Assert.False(chip.IrqActive);
        chip.ReadRegister(0x0E);
        Assert.True(chip.InterruptEnabled);
    }

    [Fact]
    public void ThePortsReadOutputBitsFromTheRegisterAndInputBitsFromThePins()
    {
        var chip = Chip();
        chip.WriteRegister(0x01, 0x0F);
        chip.WriteRegister(0x00, 0xA5);
        chip.PortAInput = 0x3C;
        Assert.Equal(0x35, chip.ReadRegister(0x00));
        Assert.Equal(0x0F, chip.ReadRegister(0x01));

        chip.WriteRegister(0x03, 0xF0);
        chip.WriteRegister(0x02, 0x5A);
        chip.PortBInput = 0x0C;
        Assert.Equal(0x5C, chip.ReadRegister(0x02));
        Assert.Equal(0xF0, chip.ReadRegister(0x03));

        // An input pin nothing drives low reads high: the data register does not show through.
        chip.PortAInput = 0xFF;
        chip.WriteRegister(0x01, 0x00);
        Assert.Equal(0xFF, chip.ReadRegister(0x00));
        Assert.Equal(0xA5, chip.PortAData);
    }

    [Fact]
    public void ResetZeroesTheFourIoRegistersAndLeavesRamAlone()
    {
        var chip = Chip();
        chip.WriteRegister(0x00, 0x12);
        chip.WriteRegister(0x01, 0x34);
        chip.WriteRegister(0x02, 0x56);
        chip.WriteRegister(0x03, 0x78);
        chip.WriteRegister(0x0C, 0x10);
        chip.WriteRam(5, 0x99);

        chip.Reset();

        Assert.Equal(0, chip.PortAData);
        Assert.Equal(0, chip.PortADirection);
        Assert.Equal(0, chip.PortBData);
        Assert.Equal(0, chip.PortBDirection);
        Assert.False(chip.InterruptEnabled);
        Assert.Equal(0x99, chip.ReadRam(5));
    }

    private static byte Peek(Rriot6530 chip) => chip.TimerCount;
}
