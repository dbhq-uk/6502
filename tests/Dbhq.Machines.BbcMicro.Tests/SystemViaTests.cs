using Xunit;
using Dbhq.Cpu6502;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The BBC's wiring of its two VIAs, driven through the bus as the OS drives it. Expected
/// values are from <c>docs/bbc-micro/facts/via.md</c> sections 1.2, 1.3, 2 and 3, and
/// <c>bus.md</c> sections 1c and 5.
/// </summary>
public class SystemViaTests
{
    private static readonly BbcRoms Roms = new(
        RepoPaths.ReadChecked(Pins.BbcOsPath, Pins.BbcOsSha256),
        RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256),
        RepoPaths.ReadChecked(Pins.BbcDfsPath, Pins.BbcDfsSha256));

    private const ushort Orb = 0xFE40, Ddrb = 0xFE42, Ddra = 0xFE43, T1CL = 0xFE44, T1CH = 0xFE45;
    private const ushort Pcr = 0xFE4C, Ifr = 0xFE4D, Ier = 0xFE4E, OraNoHandshake = 0xFE4F;

    private static BbcBus NewBus(int mode = 7) => new(Roms, new BbcOptions { StartupMode = mode });

    /// <summary>Spends CPU cycles on RAM reads, which are fast and touch no chip.</summary>
    private static void Wait(BbcBus bus, int cycles)
    {
        for (int i = 0; i < cycles; i++)
        {
            bus.Read(0x0000);
        }
    }

    /// <summary>The OS's reset set-up of port B (via.md s2.3): DDRB = $0F, then latch bits 6 to 0 set.</summary>
    private static void SetUpPortBAsTheOsDoes(BbcBus bus)
    {
        bus.Write(Ddrb, 0x0F);
        for (int value = 0x0E; value >= 0x08; value--)
        {
            bus.Write(Orb, (byte)value);
        }
    }

    /// <summary>The OS's key test at <c>$F02A</c>: ORB = 3, DDRA = $7F, write X to $FE4F, read it back.</summary>
    private static byte KeyTest(BbcBus bus, int x)
    {
        bus.Write(Orb, 0x03);
        bus.Write(Ddra, 0x7F);
        bus.Write(OraNoHandshake, (byte)x);
        return bus.Read(OraNoHandshake);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void EachLatchBitIsAddressedByPb0ToPb2AndTakesPb3(int bit)
    {
        var bus = NewBus();
        bus.Write(Ddrb, 0x0F);

        bus.Write(Orb, (byte)bit);          // PB3 = 0: the bit goes low
        Assert.Equal(0, bus.SystemVia.Latch & (1 << bit));
        byte others = (byte)(bus.SystemVia.Latch & ~(1 << bit));

        bus.Write(Orb, (byte)(0x08 | bit)); // PB3 = 1: the bit goes high
        Assert.Equal(1 << bit, bus.SystemVia.Latch & (1 << bit));
        Assert.Equal(others, (byte)(bus.SystemVia.Latch & ~(1 << bit))); // only the addressed bit moved

        bus.Write(Orb, (byte)bit);
        Assert.Equal(others, bus.SystemVia.Latch);
    }

    [Fact]
    public void TheOsResetSequenceSetsLatchBitsSixToZero()
    {
        var bus = NewBus();
        SetUpPortBAsTheOsDoes(bus);

        Assert.Equal(0x7F, bus.SystemVia.Latch & 0x7F);
    }

    [Theory]
    [InlineData(0, 0, 0)] // mode 3: C1 = 0, C0 = 0
    [InlineData(1, 0, 1)] // C0 is latch bit 4
    [InlineData(0, 1, 2)] // C1 is latch bit 5
    [InlineData(1, 1, 3)]
    public void TheScreenStartLatchIsBitsFourAndFive(int c0, int c1, int expected)
    {
        var bus = NewBus();
        bus.Write(Ddrb, 0x0F);
        bus.Write(Orb, (byte)(4 | (c0 << 3)));
        bus.Write(Orb, (byte)(5 | (c1 << 3)));

        Assert.Equal(expected, bus.SystemVia.ScreenStartLatch);
    }

    [Fact]
    public void ASoundWriteIsTheFallOfLatchBitZeroWithTheByteOnPortA()
    {
        // The OS's sound write at $EB21: DDRA = $FF, ORA (no handshake) = the byte,
        // ORB = $00 (WE low), then ORB = $08 (WE high) (via.md s2.2, s4 section 1).
        var bus = NewBus();
        SetUpPortBAsTheOsDoes(bus);
        var written = new List<byte>();
        bus.SystemVia.SoundWrite += written.Add;

        bus.Write(Ddra, 0xFF);
        bus.Write(OraNoHandshake, 0x9F);
        bus.Write(Orb, 0x00);
        bus.Write(Orb, 0x08);
        bus.Write(Orb, 0x00);    // a second fall is a second write
        bus.Write(Orb, 0x00);    // held low is not another fall

        Assert.Equal([0x9F, 0x9F], written);
    }

    [Theory]
    [MemberData(nameof(BbcKeyboardTests.Keys), MemberType = typeof(BbcKeyboardTests))]
    public void TheKeyTestReadsPa7HighExactlyWhenTheSelectedKeyIsDown(BbcKey key, int column, int row)
    {
        var bus = NewBus(7);
        SetUpPortBAsTheOsDoes(bus);
        int x = column + row * 16;

        Assert.Equal(x, KeyTest(bus, x));          // up: bit 7 clear, the low bits echo X

        bus.Keyboard.Press(key);
        Assert.Equal(0x80 | x, KeyTest(bus, x));   // down: bit 7 set

        int neighbour = ((column + 1) % 10) + row * 16;
        Assert.Equal(0, KeyTest(bus, neighbour) & 0x80);
        Assert.Equal(0, KeyTest(bus, column + ((row + 1) % 8) * 16) & 0x80);
    }

    [Fact]
    public void WithTheKeyboardDisabledPa7ReadsZero()
    {
        // Latch bit 3 high: the 74LS251 is off and nothing drives PA7. via.md s3, "Could NOT
        // establish", recommends 0 with no speech chip.
        var bus = NewBus(0);
        SetUpPortBAsTheOsDoes(bus);
        bus.Keyboard.Press(BbcKey.Space);

        bus.Write(Orb, 0x0B);
        bus.Write(Ddra, 0x7F);
        bus.Write(OraNoHandshake, 0x62);
        Assert.Equal(0x62, bus.Read(OraNoHandshake));
        bus.Write(OraNoHandshake, 0x09); // a made link
        Assert.Equal(0x09, bus.Read(OraNoHandshake));
    }

    [Theory]
    [InlineData(7, 0xFF)]
    [InlineData(0, 0xF8)]
    public void TheOsLinkReadThroughTheSystemViaGivesTheStartUpByte(int mode, int expected)
    {
        var bus = NewBus(mode);
        SetUpPortBAsTheOsDoes(bus);

        // $DA10-$DA3D: X = 9 down to 1, JSR $F02A, CPX #$80, ROR $FC; then ROL $FC and EOR #$FF.
        int fc = 0;
        bool carry = false;
        for (int x = 9; x >= 1; x--)
        {
            bool pressed = KeyTest(bus, x) >= 0x80;
            bool outBit = (fc & 1) != 0;
            fc = (fc >> 1) | (pressed ? 0x80 : 0);
            carry = outBit;
        }
        fc = ((fc << 1) & 0xFF) | (carry ? 1 : 0);

        Assert.Equal(expected, fc ^ 0xFF);
    }

    [Fact]
    public void CtrlReadsAsColumnOneRowZero()
    {
        var bus = NewBus();
        SetUpPortBAsTheOsDoes(bus);
        bus.Keyboard.Press(BbcKey.Ctrl);

        Assert.Equal(0x81, KeyTest(bus, 1));
        Assert.Equal(0x00, KeyTest(bus, 0)); // SHIFT is column 0
    }

    /// <summary>
    /// Steps 3 to 8 of the OS's full scan at <c>$F0E6</c>: DDRA = $7F, latch bit 3 low, column
    /// 15, clear IFR0, select column X, test IFR0 (via.md s3(e)).
    /// </summary>
    private static bool ColumnHasAKey(BbcBus bus, int column)
    {
        bus.Write(Ddra, 0x7F);
        bus.Write(Orb, 0x03);
        bus.Write(OraNoHandshake, 0x0F);
        bus.Write(Ifr, 0x01);
        bus.Write(OraNoHandshake, (byte)column);
        return (bus.Read(Ifr) & 0x01) != 0;
    }

    [Theory]
    [MemberData(nameof(BbcKeyboardTests.Keys), MemberType = typeof(BbcKeyboardTests))]
    public void TheOsColumnTestSetsIfr0ForAKeyInRowsOneToSevenOfThatColumn(BbcKey key, int column, int row)
    {
        var bus = NewBus(7);
        SetUpPortBAsTheOsDoes(bus);
        bus.Write(Pcr, 0x04); // the OS's PCR: CA2 a positive-edge input
        bus.Keyboard.Press(key);

        for (int c = 0; c <= 9; c++)
        {
            Assert.Equal(c == column && row != 0, ColumnHasAKey(bus, c));
        }
    }

    [Fact]
    public void InAutoscanAKeyRaisesTheCa2InterruptOnceTheOsEnablesIt()
    {
        var bus = NewBus();
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);
        bus.Cpu = cpu;
        SetUpPortBAsTheOsDoes(bus);       // latch bit 3 is now high: autoscan
        bus.Write(Ier, 0x7F);
        bus.Write(Ifr, 0x7F);
        bus.Write(Pcr, 0x04);
        bus.Write(Ier, 0x81);             // tidyUpAfterKeyboardProcessing at $EEE2

        Wait(bus, 64);
        Assert.Equal(0, bus.Read(Ifr) & 0x01);
        Assert.False(cpu.Irq);

        bus.Keyboard.Press(BbcKey.G);     // column 3, row 5
        Wait(bus, 40);                    // the counter sweeps sixteen columns in 16 us, 32 CPU cycles
        Assert.Equal(0x81, bus.Read(Ifr) & 0x81);
        Assert.True(cpu.Irq);

        bus.Write(Ier, 0x01);             // KEYV disables it ($EF02)
        Assert.False(cpu.Irq);
        bus.Write(Ifr, 0x01);
        Wait(bus, 40);
        Assert.Equal(0x01, bus.Read(Ifr) & 0x01); // the held key keeps raising the edge
        Assert.False(cpu.Irq);

        bus.Keyboard.Release(BbcKey.G);
        bus.Write(Ifr, 0x01);
        Wait(bus, 40);
        Assert.Equal(0, bus.Read(Ifr) & 0x01);
    }

    [Fact]
    public void ARowZeroKeyNeverRaisesCa2()
    {
        var bus = NewBus(0);              // made links are in row 0 too
        SetUpPortBAsTheOsDoes(bus);
        bus.Write(Pcr, 0x04);
        bus.Write(Ifr, 0x7F);
        bus.Keyboard.Press(BbcKey.Shift);
        bus.Keyboard.Press(BbcKey.Ctrl);

        Wait(bus, 64);
        Assert.Equal(0, bus.Read(Ifr) & 0x01);
    }

    [Fact]
    public void VsyncDrivesCa1AndItsFallSetsIfr1()
    {
        var bus = NewBus();
        bus.Write(Pcr, 0x04);
        bus.Write(Ifr, 0x7F);

        bus.SystemVia.VsyncInput = true;  // the pulse starts: not the active edge
        Assert.Equal(0, bus.Read(Ifr) & 0x02);
        bus.SystemVia.VsyncInput = false; // the pulse ends
        Assert.Equal(0x02, bus.Read(Ifr) & 0x02);
    }

    [Fact]
    public void SystemIerReadsEightyAfterPowerOnAndKeepsItsValueAcrossBreak()
    {
        var bus = NewBus();
        Assert.Equal(0x80, bus.Read(Ier));

        bus.Write(Ier, 0xF2);
        bus.Write(0xFE6E, 0x82);          // the user VIA's printer ACK
        bus.BreakReset();
        Assert.Equal(0xF2, bus.Read(Ier));
        Assert.Equal(0x80, bus.Read(0xFE6E)); // BREAK resets the user VIA

        bus.PowerOnReset();
        Assert.Equal(0x80, bus.Read(Ier));
    }

    [Fact]
    public void BreakLeavesTheLatchAndPowerOnDoesNotClearIt()
    {
        // IC32's /CLR is tied high (via.md s3(a)).
        var bus = NewBus();
        SetUpPortBAsTheOsDoes(bus);
        byte latch = bus.SystemVia.Latch;

        bus.BreakReset();
        Assert.Equal(latch, bus.SystemVia.Latch);
        bus.PowerOnReset();
        Assert.Equal(latch, bus.SystemVia.Latch);
    }

    [Fact]
    public void PortBReadsOneOnPb7AndTheFireButtons()
    {
        // PB7 = 1 means no speech chip ($DB11); PB4 and PB5 = 1 are the fire buttons not
        // pressed (via.md s1.3, s2.1).
        var bus = NewBus();
        Assert.Equal(0xB0, bus.Read(Orb) & 0xB0);

        SetUpPortBAsTheOsDoes(bus);
        bus.Write(Orb, 0x00);
        Assert.Equal(0xF0, bus.Read(Orb));    // PB0-3 from ORB, PB4-7 from the pins
    }

    [Fact]
    public void TheUserViaPcrReadsBackWhatWasWritten()
    {
        // The OS's check at $DA8F: write $0E to $FE6C and compare (bus.md s1d).
        var bus = NewBus();
        Assert.Equal(0x00, bus.Read(0xFE6C));
        bus.Write(0xFE6C, 0x0E);
        Assert.Equal(0x0E, bus.Read(0xFE6C));
    }

    [Fact]
    public void TheUserViaReadsAsAnIdlePort()
    {
        // A real Model B with nothing attached: ?&FE60 = 255, and with DDRA = &FF ?&FE61 is
        // ORA (via.md s1.2, s2.4).
        var bus = NewBus();
        Assert.Equal(0xFF, bus.Read(0xFE60));
        bus.Write(0xFE63, 0xFF);
        Assert.Equal(0x00, bus.Read(0xFE61));
        bus.Write(0xFE61, 0x41);
        Assert.Equal(0x41, bus.Read(0xFE61));
    }

    [Fact]
    public void TheUserViaPrinterAckLineIdlesHighSoTheOsSetUpRaisesNoFlag()
    {
        var bus = NewBus();
        bus.Write(0xFE6C, 0x0E);
        bus.Write(0xFE6E, 0x82);
        Wait(bus, 32);
        Assert.Equal(0x00, bus.Read(0xFE6D));

        bus.UserVia.SetCa1(false);    // a printer's ACK pulling the line low is an edge
        Assert.Equal(0x82, bus.Read(0xFE6D));
    }

    [Theory]
    [InlineData(0xFE40, 0xFE50)]
    [InlineData(0xFE4C, 0xFE5C)]
    [InlineData(0xFE60, 0xFE70)]
    [InlineData(0xFE6C, 0xFE7C)]
    public void EachViaIsMirroredInTheUpperHalfOfItsBlock(int register, int mirror)
    {
        var bus = NewBus();
        // Two registers on: DDRB for the ORB rows, so ORB reads back as written; IER for the PCR
        // rows, which the PCR does not depend on.
        bus.Write((ushort)(register + 2), 0xFF);
        bus.Write((ushort)mirror, 0x5A);

        Assert.Equal(0x5A, bus.Read((ushort)register));
        Assert.Equal(0x5A, bus.Peek((ushort)mirror));
    }

    [Fact]
    public void PeekShowsAViaRegisterWithoutClearingAFlag()
    {
        var bus = NewBus();
        bus.Write(Ier, 0xC0);
        bus.Write(0xFE4B, 0x00);
        bus.Write(T1CL, 0x02);
        bus.Write(T1CH, 0x00);
        Wait(bus, 16);

        Assert.Equal(0xC0, bus.Peek(Ifr) & 0xC0);
        bus.Peek(T1CL);                            // a read of T1C-L would clear IFR6
        Assert.Equal(0xC0, bus.Peek(Ifr) & 0xC0);
        bus.Read(T1CL);
        Assert.Equal(0x00, bus.Peek(Ifr) & 0x40);
    }

    [Fact]
    public void ASystemViaTimerFlagDrivesTheCpuIrqThroughTheBus()
    {
        var bus = NewBus();
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);
        bus.Cpu = cpu;

        bus.Write(0xFE4B, 0x00);   // one-shot
        bus.Write(T1CL, 0x04);
        bus.Write(T1CH, 0x00);
        Wait(bus, 16);
        Assert.Equal(0x40, bus.Read(Ifr) & 0x40);
        Assert.False(cpu.Irq);     // not enabled

        bus.Write(Ier, 0xC0);
        Assert.True(cpu.Irq);
        Assert.True(bus.Irq);

        bus.Write(Ifr, 0x40);      // the OS's acknowledge
        Assert.False(cpu.Irq);
    }

    [Fact]
    public void TheUserViaSharesTheLine()
    {
        var bus = NewBus();
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);
        bus.Cpu = cpu;

        bus.Write(0xFE6B, 0x00);
        bus.Write(0xFE64, 0x04);
        bus.Write(0xFE65, 0x00);
        bus.Write(0xFE6E, 0xC0);
        Wait(bus, 16);
        Assert.True(cpu.Irq);

        bus.Write(0xFE6D, 0x40);
        Assert.False(cpu.Irq);
    }

    [Fact]
    public void TheViasTickOnceForEveryTwoCpuCycles()
    {
        // The OS's 100 Hz tick: T1 free-run with latch $270E flags every 10000 VIA cycles
        // (via.md s1.4), which is 20000 CPU cycles.
        var bus = NewBus();
        bus.Write(0xFE4B, 0x40);
        bus.Write(0xFE46, 0x0E);
        bus.Write(0xFE47, 0x27);
        bus.Write(T1CH, 0x27);
        bus.Write(Ifr, 0x7F);
        Wait(bus, 1000);
        while ((bus.Peek(Ifr) & 0x40) == 0)
        {
            bus.Read(0x0000);
        }
        long first = bus.Cycles;
        bus.Write(Ifr, 0x40);
        while ((bus.Peek(Ifr) & 0x40) == 0)
        {
            bus.Read(0x0000);
        }

        Assert.Equal(20000, bus.Cycles - first);
    }
}
