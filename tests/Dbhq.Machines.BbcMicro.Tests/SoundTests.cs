using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The SN76489 in the machine: written through the system VIA's port A and latch bit 0
/// (<c>via.md</c> s4.1, s2.2), and driven by the real MOS 1.20 and BASIC.
/// </summary>
public class SoundTests
{
    [Fact]
    public void OnlyLatchBitZeroFallingWritesThePortAByteToTheChip()
    {
        var bus = new BbcBus(BbcSession.Roms);
        bus.PowerOnReset();
        var strobes = new List<byte>();
        bus.SystemVia.SoundWrite += strobes.Add;
        Sn76489 chip = bus.SoundChip;

        bus.Write(0xFE42, 0x0F); // DDRB: PB0 to PB3 drive the latch
        bus.Write(0xFE43, 0xFF); // DDRA: port A is all output, as the OS sets it (ROM EB25)
        bus.Write(0xFE40, 0x08); // latch bit 0 high: write enable off
        bus.Write(0xFE4F, 0x90); // the byte on PA: tone 1 at full volume
        Assert.Equal(15, chip.Attenuation(0));

        bus.Write(0xFE40, 0x00); // latch bit 0 low: the strobe (ROM EB2C)
        Assert.Equal(0, chip.Attenuation(0));
        Assert.Equal(new byte[] { 0x90 }, strobes);

        // Bit 0 already low, rising, or another latch bit: none is a write.
        bus.Write(0xFE4F, 0x9F);
        bus.Write(0xFE40, 0x00);
        bus.Write(0xFE40, 0x08);
        bus.Write(0xFE40, 0x03); // the keyboard's enable, latch bit 3 (ROM F02A)
        bus.Write(0xFE40, 0x0B);
        bus.Write(0xFE41, 0x9F); // ORA with handshake: a port write, not a strobe
        Assert.Equal(0, chip.Attenuation(0));
        Assert.Single(strobes);

        bus.Write(0xFE40, 0x00);
        Assert.Equal(15, chip.Attenuation(0));
        Assert.Equal(new byte[] { 0x90, 0x9F }, strobes);
    }

    [Fact]
    public void AWriteLandsAfterTheChipClockOfItsCpuCycle()
    {
        // 31,250 samples a second is eight chip clocks, 64 CPU cycles, a sample. Tone 1 at period 1
        // toggles every clock, so a sample wholly after the write is half of a channel's share.
        var machine = new BbcMachine(BbcSession.Roms, new BbcOptions { SampleRate = 31_250 });
        BbcBus bus = machine.Bus;
        bus.PowerOnReset();
        bus.Write(0xFE42, 0x0F);
        bus.Write(0xFE43, 0xFF);
        bus.Write(0xFE40, 0x08);
        foreach (byte value in new byte[] { 0x81, 0x00 })
        {
            bus.Write(0xFE4F, value);
            bus.Write(0xFE40, 0x00);
            bus.Write(0xFE40, 0x08);
        }

        // Past the count from power on (up to 1024 clocks), so the tone is toggling every clock.
        bus.Write(0xFE4F, 0x90);
        for (int i = 0; i < 10_000; i++)
        {
            bus.Read(0x0000);
        }

        bus.Write(0xFE40, 0x00); // the strobe: tone 1 at full volume from this cycle's clock on
        long cycle = bus.Cycles;
        Assert.Equal(cycle / 8, bus.SoundChip.Clocks);
        for (int i = 0; i < 1000; i++)
        {
            bus.Read(0x0000);
        }

        var samples = new float[machine.Sound.Count];
        machine.Sound.Read(samples);
        // Sample j is clocks 8j + 1 to 8j + 8: the one holding the write's clock is partly sounded,
        // the one before is silent and the one after is wholly sounded.
        int sample = (int)(cycle / 8 / 8);
        Assert.Equal(0f, samples[sample - 1]);
        Assert.InRange(samples[sample], 0f, 0.125f);
        Assert.Equal(0.125f, samples[sample + 1]);
        Assert.All(samples.Take(sample), s => Assert.Equal(0f, s));
    }

    [Fact]
    public void TheOsSilencesEveryChannelAtResetAndSetsPitchZero()
    {
        // ROM DAAA calls EC60, which for X = 7 down to 4 calls ECA2: the attenuation 15 byte through
        // EB03, then pitch 0 through ED06, which is period 1008 plus the channel's detune, 2, 1 or 0,
        // as a latch byte and a data byte, or for the noise (X = 4) the control byte &E0 (via.md s4.7).
        var session = new BbcSession();
        var strobes = new List<byte>();
        session.Machine.Bus.SystemVia.SoundWrite += strobes.Add;
        session.Machine.PowerOn();
        session.RunFor(2_000_000);

        Assert.Equal(new byte[] { 0x9F, 0x82, 0x3F, 0xBF, 0xA1, 0x3F, 0xDF, 0xC0, 0x3F, 0xFF, 0xE0 }, strobes.Take(11));

        var chip = new Sn76489();
        foreach (byte value in strobes.Take(11))
        {
            chip.Write(value);
        }
        Assert.Equal([1010, 1009, 1008], new[] { chip.TonePeriod(0), chip.TonePeriod(1), chip.TonePeriod(2) });
        Assert.Equal([15, 15, 15, 15], Enumerable.Range(0, 4).Select(chip.Attenuation));
        Assert.Equal(0, chip.NoiseControl);
    }

    [Fact]
    public void SwitchingOffAndOnSilencesTheChipButBreakDoesNot()
    {
        // The chip has no reset pin: BREAK leaves it, and only power on puts it back to the model's
        // power-on state (silent, every register 0), before the OS writes anything.
        var session = new BbcSession().Boot();
        Sn76489 chip = session.Machine.Bus.SoundChip;
        session.Type("SOUND 1,-15,100,100\r");
        Assert.Equal(0, chip.Attenuation(2));

        session.Machine.Bus.BreakReset();
        Assert.Equal(0, chip.Attenuation(2));
        Assert.Equal(237, chip.TonePeriod(2));

        session.Machine.PowerOn();
        Assert.Equal(15, chip.Attenuation(2));
        Assert.Equal(0, chip.TonePeriod(2));
        Assert.Equal(Sn76489.ShiftRegisterSeed, chip.ShiftRegister);
    }

    [Fact]
    public void TheStartUpBeepSoundsInTheFirstSecond()
    {
        // The banner carries a BEL (bus.md s4b), which the OS plays through the sound chip.
        var session = new BbcSession();
        session.Machine.PowerOn();
        session.RunFor(2_000_000);

        SoundBuffer sound = session.Machine.Sound;
        Assert.Equal(48_000, sound.SampleRate);
        var samples = new float[sound.Capacity];
        int count = sound.Read(samples);
        Assert.True(count >= 47_999, $"only {count} samples in the first second");
        Assert.Equal(0f, samples[0]);
        Assert.Contains(samples.Take(count), s => s > 0);
        Assert.All(samples.Take(count), s => Assert.InRange(s, 0f, 1f));
    }

    [Fact]
    public void ASoundCommandOnChannelOnePlaysToneThreeAtThePitchesPeriod()
    {
        // SOUND 1 is chip tone 3 (via.md s4.7); pitch 100 is period 237, 527.43 Hz (s4.8), and
        // amplitude -15 is attenuation 0.
        var session = new BbcSession().Boot();
        Sn76489 chip = session.Machine.Bus.SoundChip;
        session.Type("SOUND 1,-15,100,20\r");

        Assert.Equal(237, chip.TonePeriod(2));
        Assert.Equal(0, chip.Attenuation(2));
        Assert.Equal(15, chip.Attenuation(0));
        Assert.Equal(15, chip.Attenuation(1));
        Assert.Equal(15, chip.Attenuation(3));

        // Channel 1 on its own, a tick at a time of the machine: it swings between 0 and full.
        var levels = new HashSet<float>();
        for (int i = 0; i < 100; i++)
        {
            session.RunFor(100);
            levels.Add(chip.ChannelOutput(2));
        }
        Assert.Equal(new HashSet<float> { 0f, 1f }, levels);

        // And in the samples: a square wave at the pitch's frequency, read off its rising edges.
        SoundBuffer sound = session.Machine.Sound;
        sound.Read(new float[sound.Capacity]);
        session.RunFor(1_000_000);
        var samples = new float[sound.Capacity];
        int count = sound.Read(samples);
        Assert.InRange(count, 23_999, 24_001);
        Assert.All(samples.Take(count), s => Assert.InRange(s, 0f, 0.25f));
        int rises = 0;
        for (int i = 1; i < count; i++)
        {
            if (samples[i - 1] < 0.125f && samples[i] >= 0.125f)
            {
                rises++;
            }
        }
        double hertz = rises / (count / 48_000.0);
        Assert.InRange(hertz, 527.43 - 3, 527.43 + 3);
    }

    // via.md s4.8, the OS pitch tests: the SOUND pitch and the period the ROM's tables give chip
    // tone 3, then channels 2 and 3 at pitch 53, which add 1 and 2 (ROM C441-C444).
    [Fact]
    public void TheOsTurnsPitchesIntoTheSheetsPeriods()
    {
        var session = new BbcSession().Boot();
        Sn76489 chip = session.Machine.Bus.SoundChip;

        // Pitch 0 is already there: the reset wrote it (ROM ECBA to ED06). The OS writes a pitch only
        // when it differs from the channel's last (ROM ED01-ED04), so a SOUND at pitch 0 writes none.
        Assert.Equal(1008, chip.TonePeriod(2));

        var decoder = new Sn76489();
        for (int channel = 0; channel < 3; channel++)
        {
            decoder.Write((byte)(0x80 | (channel << 5) | (chip.TonePeriod(channel) & 0x0F)));
            decoder.Write((byte)(chip.TonePeriod(channel) >> 4));
        }
        var periods = new List<int>[] { [], [], [] };
        session.Machine.Bus.SystemVia.SoundWrite += value =>
        {
            decoder.Write(value);
            if ((value & 0x80) == 0 && decoder.LatchedRegister is 0 or 2 or 4)
            {
                periods[decoder.LatchedRegister / 2].Add(decoder.TonePeriod(decoder.LatchedRegister / 2));
            }
        };

        session.Type("10 FOR I=1 TO 8:READ P:SOUND 1,-15,P,1:NEXT:SOUND 2,-15,53,1:SOUND 3,-15,53,1\r");
        session.Type("20 DATA 0,1,4,48,52,53,100,255\r");
        session.Type("RUN\r");
        session.RunFor(4_000_000);

        // SOUND 1 is chip tone 3, SOUND 2 tone 2 and SOUND 3 tone 1, each queue playing at once.
        Assert.Equal([994, 951, 504, 475, 469, 237, 25], periods[2]);
        Assert.Equal([470], periods[1]);
        Assert.Equal([471], periods[0]);
    }
}
