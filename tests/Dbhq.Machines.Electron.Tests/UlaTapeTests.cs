using Dbhq.Machines.Electron.Tape;
using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The cassette at the ULA's serial register, alone: a machine that is never switched on, its bus
/// driven by hand, no OS. The times are from <c>tape.md</c> s4 and s5 and <c>ula.md</c> s6a and s9:
/// a bit is 1,664 cycles, a byte ten bits (16,640), receive-full and transmit-empty nine bits into
/// a byte (14,976), high-tone-detect after ten bits of carrier (16,640), a carrier cycle 832. These
/// are the properties the test-only <see cref="TapeProbe"/> showed the OS loads and saves with
/// (task 10), asserted here of the production <see cref="UlaTape"/>.
/// </summary>
public class UlaTapeTests
{
    // Status bits (ula.md s6a).
    private const int ReceiveFull = 0x10;
    private const int TransmitEmpty = 0x20;
    private const int HighTone = 0x40;

    // $FE07 (ula.md s9a): bit 6 the motor, bits 2 and 1 the comms mode: 00 input, 10 output, 01 sound.
    private const byte InputMotorOn = 0x40;
    private const byte OutputMotorOn = 0x44;
    private const byte SoundMotorOn = 0x42;
    private const byte InputMotorOff = 0x00;
    private const byte OutputMotorOff = 0x04;

    private const long Bit = 1_664;
    private const long Byte = 10 * Bit;   // 16,640
    private const long Ready = 9 * Bit;   // 14,976
    private const long CarrierCycle = 832;

    private static ElectronMachine NewMachine() => new(ElectronSession.Roms);

    /// <summary>Moves the bus clock to <paramref name="cycle"/> with reads of the OS ROM, one cycle each (ula.md s3a).</summary>
    private static void RunTo(ElectronBus bus, long cycle)
    {
        Assert.True(bus.Cycles <= cycle, $"already at {bus.Cycles}, past {cycle}");
        while (bus.Cycles < cycle)
        {
            bus.Read(0xC000);
        }
    }

    /// <summary>Writes, and returns the cycle the write took effect at.</summary>
    private static long Write(ElectronBus bus, ushort address, byte value)
    {
        bus.Write(address, value);
        return bus.Cycles;
    }

    private static int Status(ElectronMachine m, int bit) => m.Bus.Ula.Status & bit;

    [Fact]
    public void HighToneIsSetAfterTenBitTimesOfCarrierAndAWriteOf40ClearsIt()
    {
        var m = NewMachine();
        m.InsertTape([new Carrier(100), new TapeByte(0x2A)]);
        long on = Write(m.Bus, 0xFE07, InputMotorOn);

        RunTo(m.Bus, on + Byte - 1);
        Assert.Equal(0, Status(m, HighTone));
        RunTo(m.Bus, on + Byte);
        Assert.NotEqual(0, Status(m, HighTone));
        Assert.Equal(Byte, m.TapePosition);

        m.Bus.Write(0xFE05, 0x40); // ula.md s6b: bit 6 clears high tone
        Assert.Equal(0, Status(m, HighTone));
    }

    [Fact]
    public void HighToneComesAgainEveryTenBitTimesWhileTheCarrierLasts()
    {
        // tape.md s7 item 11 leaves open whether the ULA raises it again once cleared; the probe
        // did, every ten bit times, and the OS loaded every tape so. The cassette does the same.
        var m = NewMachine();
        m.InsertTape([new Carrier(100)]);
        long on = Write(m.Bus, 0xFE07, InputMotorOn);
        RunTo(m.Bus, on + Byte);
        m.Bus.Write(0xFE05, 0x40);

        RunTo(m.Bus, on + (2 * Byte) - 1);
        Assert.Equal(0, Status(m, HighTone));
        RunTo(m.Bus, on + (2 * Byte));
        Assert.NotEqual(0, Status(m, HighTone));
    }

    [Fact]
    public void ConsecutiveCarriersAreOneToneAndSilenceBreaksIt()
    {
        // Ten cycles of 2400 Hz are 8,320 CPU cycles, half the ten bit times high tone needs.
        var joined = NewMachine();
        joined.InsertTape([new Carrier(10), new Carrier(10)]);
        long on = Write(joined.Bus, 0xFE07, InputMotorOn);
        RunTo(joined.Bus, on + Byte);
        Assert.NotEqual(0, Status(joined, HighTone));

        var broken = NewMachine();
        broken.InsertTape([new Carrier(10), new Silence(1_000), new Carrier(10)]);
        on = Write(broken.Bus, 0xFE07, InputMotorOn);
        RunTo(broken.Bus, on + Byte + 1_000 + 100);
        Assert.Equal(0, Status(broken, HighTone));
    }

    [Fact]
    public void ReceiveFullIsSetNineBitTimesIntoAByteAndReadingFe04ClearsIt()
    {
        var m = NewMachine();
        m.InsertTape([new Carrier(100), new TapeByte(0x2A)]);
        long on = Write(m.Bus, 0xFE07, InputMotorOn);
        long byteStart = on + (100 * CarrierCycle); // 83,200 cycles of carrier

        RunTo(m.Bus, byteStart + Ready - 1);
        Assert.Equal(0, Status(m, ReceiveFull));
        RunTo(m.Bus, byteStart + Ready);
        Assert.NotEqual(0, Status(m, ReceiveFull));

        Assert.Equal(0x2A, m.Bus.Read(0xFE04));
        Assert.Equal(0, Status(m, ReceiveFull));
        Assert.Equal(0, m.LostBytes);
    }

    [Fact]
    public void AByteNotReadIn4000CyclesIsLostCountedAndReplacedByTheNext()
    {
        // ula.md s9: the byte must be read within about 2 ms, 4,000 cycles.
        var m = NewMachine();
        m.InsertTape([new Carrier(20), new TapeByte(0x01), new TapeByte(0x02)]);
        long on = Write(m.Bus, 0xFE07, InputMotorOn);
        long ready1 = on + (20 * CarrierCycle) + Ready;

        RunTo(m.Bus, ready1 + 4_000 - 1);
        Assert.NotEqual(0, Status(m, ReceiveFull));
        Assert.Equal(0, m.LostBytes);
        RunTo(m.Bus, ready1 + 4_000);
        Assert.Equal(0, Status(m, ReceiveFull));
        Assert.Equal(1, m.LostBytes);

        RunTo(m.Bus, ready1 + Byte);
        Assert.NotEqual(0, Status(m, ReceiveFull));
        Assert.Equal(0x02, m.Bus.Read(0xFE04));
        RunTo(m.Bus, ready1 + Byte + 10_000);
        Assert.Equal(1, m.LostBytes);
    }

    [Fact]
    public void TheTapeStopsAtItsEndAndDeliversNothingMore()
    {
        var m = NewMachine();
        m.InsertTape([new Carrier(20)]);
        long on = Write(m.Bus, 0xFE07, InputMotorOn);
        RunTo(m.Bus, on + Byte);
        Assert.NotEqual(0, Status(m, HighTone)); // a carrier of exactly ten bit times is heard
        m.Bus.Write(0xFE05, 0x40);

        RunTo(m.Bus, on + 200_000);
        Assert.Equal(0, Status(m, HighTone));
        Assert.Equal(0, Status(m, ReceiveFull));
        Assert.Equal(20 * CarrierCycle, m.TapePosition);
    }

    [Fact]
    public void WithTheMotorOffOrInNeitherModeTheTapeDoesNotMove()
    {
        foreach (byte control in new[] { InputMotorOff, SoundMotorOn })
        {
            var m = NewMachine();
            m.InsertTape([new Carrier(100), new TapeByte(0x2A)]);
            long at = Write(m.Bus, 0xFE07, control);
            RunTo(m.Bus, at + 200_000);
            Assert.Equal(0, Status(m, HighTone | ReceiveFull));
            Assert.Equal(0, m.TapePosition);
        }

        var r = NewMachine();
        r.StartRecording();
        long off = Write(r.Bus, 0xFE07, OutputMotorOff);
        r.Bus.Write(0xFE04, 0x2A);
        RunTo(r.Bus, off + 100_000);
        Assert.False(r.MotorOn);
        Assert.Equal(0, r.TapePosition);
        Assert.Empty(r.EjectTape());
    }

    [Fact]
    public void TransmitEmptyIsSetNineBitTimesAfterTheByteStartsAndTheNextWaitsForTheLine()
    {
        var m = NewMachine();
        m.StartRecording();
        long on = Write(m.Bus, 0xFE07, OutputMotorOn);

        // A byte written to an idle line starts at once (tape.md s5).
        long w1 = Write(m.Bus, 0xFE04, 0x2A);
        Assert.Equal(0, Status(m, TransmitEmpty));
        RunTo(m.Bus, w1 + Ready - 1);
        Assert.Equal(0, Status(m, TransmitEmpty));
        RunTo(m.Bus, w1 + Ready);
        Assert.NotEqual(0, Status(m, TransmitEmpty));

        // Written just after transmit-empty, inside the first byte's stop bit, as the OS does
        // (tape.md s7 item 2): it starts when the first byte ends, ten bit times after it started.
        RunTo(m.Bus, w1 + Ready + 160);
        m.Bus.Write(0xFE04, 0x55);
        Assert.Equal(0, Status(m, TransmitEmpty));
        long start2 = w1 + Byte;
        RunTo(m.Bus, start2 + Ready - 1);
        Assert.Equal(0, Status(m, TransmitEmpty));
        RunTo(m.Bus, start2 + Ready);
        Assert.NotEqual(0, Status(m, TransmitEmpty));

        // 100,000 cycles of idle line after the second byte ends: 120.19 cycles of 2400 Hz,
        // recorded as 120 (tape.md s5, carrier units: to the nearest). The idle stretches before
        // the first byte and after the last are a few cycles, under a bit time: not carrier.
        long end2 = start2 + Byte;
        RunTo(m.Bus, end2 + 100_000 - 3);
        long w3 = Write(m.Bus, 0xFE04, 0x0D);
        Assert.InRange(w3 - end2, 100_000 - 3, 100_000 + 3);
        RunTo(m.Bus, w3 + Byte);
        long off = Write(m.Bus, 0xFE07, OutputMotorOff);
        Assert.False(m.MotorOn);

        Assert.Equal(off - on, m.TapePosition); // the tape moved from the motor going on to its going off
        Assert.Equal(
            new TapeEvent[] { new TapeByte(0x2A), new TapeByte(0x55), new Carrier(120), new TapeByte(0x0D) },
            m.EjectTape());
    }

    [Fact]
    public void IdleLineLongerThanABitTimeIsCarrierAndShorterIsNot()
    {
        // tape.md s7 item 2: within a block the OS writes inside the stop bit, so a gap under one
        // bit time is never one it made; the recorder drops it rather than write a carrier that
        // cannot be heard (high tone needs ten bit times). 2,000 cycles is 2.4 carrier cycles: 2.
        foreach ((long gap, TapeEvent[] expected) in new[]
        {
            (2_000L, new TapeEvent[] { new TapeByte(1), new Carrier(2), new TapeByte(2) }),
            (1_600L, new TapeEvent[] { new TapeByte(1), new TapeByte(2) }),
        })
        {
            var m = NewMachine();
            m.StartRecording();
            Write(m.Bus, 0xFE07, OutputMotorOn);
            long w1 = Write(m.Bus, 0xFE04, 1);
            RunTo(m.Bus, w1 + Byte + gap - 3);
            long w2 = Write(m.Bus, 0xFE04, 2);
            Assert.InRange(w2 - (w1 + Byte), gap - 3, gap);
            RunTo(m.Bus, w2 + Byte);
            Write(m.Bus, 0xFE07, OutputMotorOff);
            Assert.Equal(expected, m.EjectTape());
        }
    }

    [Fact]
    public void RewindAfterRecordingPlaysTheRecordingBack()
    {
        var m = NewMachine();
        m.StartRecording();
        long on = Write(m.Bus, 0xFE07, OutputMotorOn);
        RunTo(m.Bus, on + (40 * CarrierCycle));
        long w = Write(m.Bus, 0xFE04, 0x2A);
        RunTo(m.Bus, w + Byte);
        Write(m.Bus, 0xFE07, OutputMotorOff);

        m.Rewind();
        Assert.Equal(0, m.TapePosition);
        long play = Write(m.Bus, 0xFE07, InputMotorOn);
        RunTo(m.Bus, play + (40 * CarrierCycle) + Ready + 3);
        Assert.NotEqual(0, Status(m, ReceiveFull));
        Assert.Equal(0x2A, m.Bus.Read(0xFE04));
        Assert.Equal(new TapeEvent[] { new Carrier(40), new TapeByte(0x2A) }, m.EjectTape());
    }

    [Fact]
    public void TheTapeIsTheSameHoweverOftenTheHostLooks()
    {
        // The cassette is lazy: one machine catches the ULA up at every cycle, the other jumps.
        TapeEvent[] tape = [new Carrier(30), new TapeByte(1), new TapeByte(2), new TapeByte(3)];
        var close = NewMachine();
        var far = NewMachine();
        close.InsertTape(tape);
        far.InsertTape(tape);
        long on1 = Write(close.Bus, 0xFE07, InputMotorOn);
        long on2 = Write(far.Bus, 0xFE07, InputMotorOn);
        Assert.Equal(on1, on2);

        long end = on1 + (30 * CarrierCycle) + (3 * Byte) + 5_000;
        RunTo(close.Bus, end);
        far.Bus.Ula.CatchUp(end);
        Assert.Equal(3, close.LostBytes);
        Assert.Equal(close.LostBytes, far.LostBytes);
        Assert.Equal(close.Bus.Ula.Status, far.Bus.Ula.Status);
    }

    [Fact]
    public void WithNoTapeAndTheMotorOnNothingHappens()
    {
        var m = NewMachine();
        long on = Write(m.Bus, 0xFE07, InputMotorOn);
        RunTo(m.Bus, on + 100_000);
        Assert.Equal(0, Status(m, HighTone | ReceiveFull));
        Assert.True(m.MotorOn);
        Assert.Equal(0, m.TapePosition);
        Assert.Empty(m.EjectTape());
    }
}
