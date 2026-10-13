using Dbhq.Cpu6502;
using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// What the machine tells the page for its models (models plan, task 6): the three LEDs, and how
/// many times the CPU has reached each chip. Counting changes nothing else, which
/// <see cref="BusTraceTests"/> and the native fingerprint show.
/// </summary>
public class MachineStateTests
{
    /// <summary>
    /// The chip each SHEILA address selects, from the Service Manual's strobe list, not from the
    /// bus's own decode [D1, PDF page 58: <c>&amp;FE00</c> IC2, <c>&amp;FE08</c> IC4, <c>&amp;FE10</c>
    /// IC7, <c>&amp;FE20</c> IC6, <c>&amp;FE30</c> IC76, <c>&amp;FE40</c> IC3, <c>&amp;FE60</c> IC69,
    /// <c>&amp;FE80</c> IC78, <c>&amp;FEC0</c> IC73], with each block's size from <c>bus.md</c> s1c.
    /// The station ID, the ADLC and the Tube are none of the counted chips; neither is anything
    /// outside SHEILA.
    /// </summary>
    private static BbcChip? ChipAt(ushort address) => address switch
    {
        >= 0xFE00 and <= 0xFE07 => BbcChip.Crtc,
        >= 0xFE08 and <= 0xFE0F => BbcChip.Acia,
        >= 0xFE10 and <= 0xFE17 => BbcChip.SerialUla,
        >= 0xFE20 and <= 0xFE2F => BbcChip.VideoUla,
        >= 0xFE30 and <= 0xFE3F => BbcChip.RomLatch,
        >= 0xFE40 and <= 0xFE5F => BbcChip.SystemVia,
        >= 0xFE60 and <= 0xFE7F => BbcChip.UserVia,
        >= 0xFE80 and <= 0xFE9F => BbcChip.Fdc8271,
        >= 0xFEC0 and <= 0xFEDF => BbcChip.Adc,
        _ => null,
    };

    private static BbcBus NewBus() => new(BbcSession.Roms);

    private static uint[] Counts(BbcBus bus) => Enum.GetValues<BbcChip>().Select(bus.Accesses).ToArray();

    private static uint[] Only(BbcChip? chip, uint count)
    {
        var counts = new uint[Enum.GetValues<BbcChip>().Length];
        if (chip is { } c)
        {
            counts[(int)c] = count;
        }
        return counts;
    }

    [Fact]
    public void TheChipsAreTheTenTheModelsPlanNamed()
    {
        Assert.Equal(
            ["Crtc", "Acia", "SerialUla", "VideoUla", "RomLatch", "SystemVia", "UserVia", "Fdc8271", "Adc", "Sound"],
            Enum.GetNames<BbcChip>());
    }

    [Fact]
    public void EachCpuReadOrWriteAddsOneToTheChipItsAddressSelectsAndNothingToTheOthers()
    {
        var addresses = Enumerable.Range(0xFE00, 0x100).Select(a => (ushort)a)
            .Concat(new ushort[] { 0x0000, 0x7FFF, 0x8000, 0xBFFF, 0xC000, 0xFBFF, 0xFC00, 0xFCFF, 0xFD00, 0xFDFF, 0xFF00, 0xFFFF });
        foreach (ushort address in addresses)
        {
            foreach (bool write in new[] { false, true })
            {
                var bus = NewBus();
                if (write)
                {
                    bus.Write(address, 0x00);
                }
                else
                {
                    bus.Read(address);
                }

                Assert.True(Only(ChipAt(address), 1).SequenceEqual(Counts(bus)), $"{(write ? "write" : "read")} ${address:X4}: [{string.Join(", ", Counts(bus))}]");
            }
        }
    }

    [Fact]
    public void APeekCountsNothing()
    {
        var bus = NewBus();
        for (int address = 0; address <= 0xFFFF; address++)
        {
            bus.Peek((ushort)address);
        }

        Assert.Equal(Only(null, 0), Counts(bus));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AStretchedAccessCountsOnceWhateverItsWait(int parity)
    {
        var bus = NewBus();
        while ((bus.Cycles & 1) != parity)
        {
            bus.Read(0x0000);
        }

        long before = bus.Cycles;
        bus.Read(0xFE40);

        Assert.Equal(2 + parity, bus.Cycles - before);
        Assert.Equal(Only(BbcChip.SystemVia, 1), Counts(bus));
    }

    [Theory]
    [InlineData(0xFE08, BbcChip.Acia)]
    [InlineData(0xFE62, BbcChip.UserVia)]
    public void AReadModifyWriteCountsItsReadAndBothItsWrites(int address, BbcChip chip)
    {
        // INC abs on the NMOS core: the opcode and operand fetches from RAM, then a read, the old
        // value written back and the new one written.
        var bus = NewBus();
        var cpu = new Cpu(bus, CpuVariant.Nmos6502);
        bus.Cpu = cpu;
        bus.PokeRam(0x0200, 0xEE);
        bus.PokeRam(0x0201, (byte)address);
        bus.PokeRam(0x0202, (byte)(address >> 8));
        cpu.PC = 0x0200;

        cpu.Step();

        Assert.Equal(Only(chip, 3), Counts(bus));
    }

    [Fact]
    public void ASoundWriteThroughTheSystemViaAddsOneToTheSoundChip()
    {
        // The OS's way (via.md s4.1): the byte on port A, then latch bit 0 taken high and low.
        var bus = NewBus();
        bus.Write(0xFE42, 0xFF);    // DDRB: PB0 to PB7 out
        bus.Write(0xFE43, 0xFF);    // DDRA: PA0 to PA7 out
        bus.Write(0xFE4F, 0x9F);    // ORA, no handshake: channel 0 silent
        bus.Write(0xFE40, 0x08);    // latch bit 0 high
        bus.Write(0xFE40, 0x00);    // and low: the strobe

        Assert.Equal(1u, bus.Accesses(BbcChip.Sound));
        Assert.Equal(5u, bus.Accesses(BbcChip.SystemVia));

        bus.Write(0xFE40, 0x08);    // rising is no strobe
        Assert.Equal(1u, bus.Accesses(BbcChip.Sound));
    }

    [Fact]
    public void AfterBootTheOsHasReachedTheVideoChipsTheViasAndTheRomLatchAndStarCatReachesThe8271()
    {
        var s = new BbcSession(disc: DiscImage.Blank(80, false)).Boot();
        BbcBus bus = s.Machine.Bus;
        foreach (BbcChip chip in new[] { BbcChip.Crtc, BbcChip.VideoUla, BbcChip.RomLatch, BbcChip.SystemVia, BbcChip.UserVia, BbcChip.Sound })
        {
            Assert.True(bus.Accesses(chip) > 0, $"{chip} was not reached at boot");
        }

        uint before = bus.Accesses(BbcChip.Fdc8271);
        s.Type("*CAT\r").RunFor(4_000_000);

        Assert.True(bus.Accesses(BbcChip.Fdc8271) > before, "*CAT did not reach the 8271");
        Assert.Contains(Enumerable.Range(0, 25).Select(s.ScreenRowAsMemory), row => row.StartsWith(" (00)", StringComparison.Ordinal));
    }

    [Fact]
    public void OverAWholeSessionEachCounterIsTheAccessesTheCpuMadeToItsChip()
    {
        // Counted again outside the machine, by address, from every access the CPU made; the sound
        // chip's by the system VIA's own strobe event.
        var machine = new TracedMachine(DiscImage.Blank(80, false));
        var expected = new uint[Enum.GetValues<BbcChip>().Length];
        machine.Accessed = (address, write) =>
        {
            if (ChipAt(address) is { } chip)
            {
                expected[(int)chip]++;
            }
        };
        machine.Machine.SystemVia.SoundWrite += _ => expected[(int)BbcChip.Sound]++;

        machine.RunSession();

        Assert.Equal(expected, Counts(machine.Machine));
        Assert.All(expected.Where((_, i) => (BbcChip)i is not (BbcChip.Acia or BbcChip.Adc or BbcChip.SerialUla)), n => Assert.True(n > 0));
    }

    [Fact]
    public void TheCountsAreAlsoGivenAsOneArrayInChipOrderAndStartAgainAtPowerOn()
    {
        var s = new BbcSession().Boot();
        BbcBus bus = s.Machine.Bus;

        Assert.Equal(Counts(bus).Select(c => unchecked((int)c)), bus.AccessCounts());

        s.Machine.PowerOn();
        Assert.Equal(Only(null, 0), Counts(bus));
    }

    [Fact]
    public void TheSerialUlaKeepsTheLastByteWrittenAnywhereInItsBlockAndAReadChangesNothing()
    {
        var bus = NewBus();
        Assert.Equal(0x00, bus.SerialUlaControl);
        bus.Write(0xFE10, 0x85);
        Assert.Equal(0x85, bus.SerialUlaControl);
        bus.Write(0xFE17, 0x64);
        Assert.Equal(0x64, bus.SerialUlaControl);
        bus.Read(0xFE10);
        bus.Write(0xFE18, 0xFF);    // the station ID, beyond the serial ULA's block
        Assert.Equal(0x64, bus.SerialUlaControl);
    }

    [Fact]
    public void AfterBootCapsLockIsOnItsLightIsLitAndALetterTypesACapital()
    {
        var s = new BbcSession().Boot();

        Assert.Equal(BbcLeds.CapsLock, s.Machine.Bus.Leds);
        // The OS's LED routine at $EEEB writes latch bit 6 from keyboard status bit 4, which is 0
        // while CAPS LOCK is engaged (AUG, OSBYTE &CA): the light is lit by a low latch bit.
        Assert.Equal(0, s.Machine.Bus.SystemVia.Latch & 0x40);

        Tap(s, BbcKey.A);
        Assert.Equal(">A", s.ScreenRowAsMemory(7)[..2]);
    }

    [Fact]
    public void CapsLockPressedPutsItsLightOutAndALetterTypesSmall()
    {
        var s = new BbcSession().Boot();
        Tap(s, BbcKey.CapsLock);

        Assert.Equal(BbcLeds.None, s.Machine.Bus.Leds);
        Assert.Equal(0x40, s.Machine.Bus.SystemVia.Latch & 0x40);

        Tap(s, BbcKey.A);
        Assert.Equal(">a", s.ScreenRowAsMemory(7)[..2]);
    }

    [Fact]
    public void ShiftLockLightsItsOwnLightPutsCapsLockOutAndShiftsEveryKey()
    {
        // KEYV (MOS 1.20 reassembly, S4): SHIFT LOCK disengages CAPS LOCK, and while it is engaged
        // every key is shifted, so 1 types !.
        var s = new BbcSession().Boot();
        Tap(s, BbcKey.ShiftLock);

        Assert.Equal(BbcLeds.ShiftLock, s.Machine.Bus.Leds);

        Tap(s, BbcKey.D1);
        Assert.Equal(">!", s.ScreenRowAsMemory(7)[..2]);
    }

    [Fact]
    public void StarMotorOneLightsTheCassetteMotorLightAndStarMotorZeroPutsItOut()
    {
        // Serial ULA control bit 7 (AUG s.20.9.4; Service Manual s.3.5).
        var s = new BbcSession().Boot();
        s.Type("*MOTOR 1\r").RunFor(200_000);

        Assert.Equal(BbcLeds.CassetteMotor | BbcLeds.CapsLock, s.Machine.Bus.Leds);
        Assert.Equal(0x80, s.Machine.Bus.SerialUlaControl & 0x80);

        s.Type("*MOTOR 0\r").RunFor(200_000);
        Assert.Equal(BbcLeds.CapsLock, s.Machine.Bus.Leds);
    }

    private static void Tap(BbcSession s, BbcKey key)
    {
        s.Machine.Keyboard.Press(key);
        s.RunFor(BbcSession.HoldCycles);
        s.Machine.Keyboard.Release(key);
        s.RunFor(BbcSession.RestCycles);
    }
}
