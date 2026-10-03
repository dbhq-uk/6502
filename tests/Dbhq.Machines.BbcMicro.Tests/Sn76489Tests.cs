using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The SN76489 on its own, from the fact sheet <c>via.md</c> section 4: the worked examples of
/// s4.8, the noise test vectors of s4.4 and the volume table of s4.5. Chip channels are TI's
/// numbering from 0: tone 1, 2 and 3 are channels 0, 1 and 2, and the noise is channel 3.
/// </summary>
public class Sn76489Tests
{
    // via.md s4.5: A(n) to six places, the same times 32767, and a channel's share of a 1.0 mix.
    public static TheoryData<int, double, int, double> VolumeTable => new()
    {
        { 0, 1.000000, 32767, 0.2500 },
        { 1, 0.794328, 26028, 0.1986 },
        { 2, 0.630957, 20675, 0.1577 },
        { 3, 0.501187, 16422, 0.1253 },
        { 4, 0.398107, 13045, 0.0995 },
        { 5, 0.316228, 10362, 0.0791 },
        { 6, 0.251189, 8231, 0.0628 },
        { 7, 0.199526, 6538, 0.0499 },
        { 8, 0.158489, 5193, 0.0396 },
        { 9, 0.125893, 4125, 0.0315 },
        { 10, 0.100000, 3277, 0.0250 },
        { 11, 0.079433, 2603, 0.0199 },
        { 12, 0.063096, 2067, 0.0158 },
        { 13, 0.050119, 1642, 0.0125 },
        { 14, 0.039811, 1304, 0.0100 },
        { 15, 0.0, 0, 0.0 },
    };

    [Theory]
    [MemberData(nameof(VolumeTable))]
    public void TheVolumeTableIsTwoDecibelsAStepAndFifteenIsOff(int attenuation, double amplitude, int level, double share)
    {
        Assert.Equal(amplitude, Sn76489.Volume(attenuation), 6);
        Assert.Equal(level, Sn76489.Level(attenuation));

        // One channel at that attenuation, its flip-flop high and the others silent: the mix is a
        // quarter of the amplitude, so four channels at full volume make 1.0 (s4.5, s4.6).
        var chip = new Sn76489();
        chip.Write(0x81); // tone 1 period 1: the flip-flop toggles every clock
        chip.Write(0x00);
        chip.Write((byte)(0x90 | attenuation));
        while (!chip.ChannelHigh(0))
        {
            chip.Tick();
            Assert.True(chip.Clocks < 2_000, "the channel never went high");
        }

        Assert.Equal(amplitude, chip.ChannelOutput(0), 5);

        // The mix adds the levels out of 32767, so it is the sheet's share to its four places, give
        // or take the half a level the whole numbers round away.
        Assert.Equal(level / (4.0 * 32767), chip.Output, 6);
        Assert.InRange(chip.Output, share - 0.00006, share + 0.00006);
    }

    [Fact]
    public void PowerOnIsSilentWithTheShiftRegisterAtItsSeed()
    {
        // The real chip's power-on state is not known (s4 "Could NOT establish"); the model starts
        // silent, every register 0 and the shift register at &4000.
        var chip = new Sn76489();
        for (int channel = 0; channel < 4; channel++)
        {
            Assert.Equal(15, chip.Attenuation(channel));
        }
        for (int channel = 0; channel < 3; channel++)
        {
            Assert.Equal(0, chip.TonePeriod(channel));
        }
        Assert.Equal(0, chip.NoiseControl);
        Assert.Equal(Sn76489.ShiftRegisterSeed, chip.ShiftRegister);
        Assert.Equal(0x4000, Sn76489.ShiftRegisterSeed);
        Assert.Equal(0f, chip.Output);
    }

    // via.md s4.8, the tone formula table: n, the frequency, and chip tone 1's latch and data bytes.
    [Theory]
    [InlineData(1, 125000.000, 0x81, 0x00)]
    [InlineData(2, 62500.000, 0x82, 0x00)]
    [InlineData(100, 1250.000, 0x84, 0x06)]
    [InlineData(284, 440.141, 0x8C, 0x11)]
    [InlineData(478, 261.506, 0x8E, 0x1D)]
    [InlineData(1023, 122.190, 0x8F, 0x3F)]
    public void ATonePeriodTogglesEveryNClocksAtTheSheetsFrequency(int n, double hertz, int latch, int data)
    {
        // The rule beside the table: latch = &80 | (cc << 5) | (n & 15), data = n >> 4.
        Assert.Equal(latch, 0x80 | (n & 15));
        Assert.Equal(data, n >> 4);

        // Tone 1, 2 and 3: the base &80, &A0, &C0.
        for (int channel = 0; channel < 3; channel++)
        {
            var chip = new Sn76489();
            chip.Write((byte)(latch + (channel * 0x20)));
            chip.Write((byte)data);
            Assert.Equal(n, chip.TonePeriod(channel));

            List<long> toggles = Toggles(chip, channel, count: 4);

            // The first toggle comes when the count from before runs out; after it, every n clocks.
            Assert.Equal(n, toggles[2] - toggles[1]);
            Assert.Equal(n, toggles[3] - toggles[2]);

            // Two toggles a cycle of the square wave, at 250 kHz (4 MHz / 16, s4.3).
            double measured = Sn76489.ClockRate / (double)(toggles[3] - toggles[1]);
            Assert.Equal(hertz, measured, 3);
        }
    }

    [Fact]
    public void TheChipClockIsTheCpuClockDividedByEight()
    {
        // 4 MHz from the video ULA, divided by 16, is 250 kHz; the CPU runs at 2 MHz (s4.3).
        Assert.Equal(250_000, Sn76489.ClockRate);
        Assert.Equal(8, Sn76489.CpuCyclesPerClock);
    }

    // via.md s4.8, the register byte tests.
    [Fact]
    public void ALatchAndADataByteMakeATenBitPeriod()
    {
        var chip = new Sn76489();
        chip.Write(0x8C);
        chip.Write(0x11);
        Assert.Equal(0x11C, chip.TonePeriod(0));
        Assert.Equal(284, chip.TonePeriod(0));

        chip.Write(0x8F);
        chip.Write(0x3F);
        Assert.Equal(1023, chip.TonePeriod(0));
    }

    [Fact]
    public void ALatchByteAloneChangesOnlyTheLowNibble()
    {
        var chip = new Sn76489();
        chip.Write(0x80);
        chip.Write(0x2A); // high six bits &2A
        chip.Write(0x8C);
        Assert.Equal((0x2A << 4) | 0xC, chip.TonePeriod(0));
        chip.Write(0x8F);
        Assert.Equal((0x2A << 4) | 0xF, chip.TonePeriod(0));
    }

    [Fact]
    public void AttenuationLatchBytes()
    {
        var chip = new Sn76489();
        chip.Write(0x90);
        Assert.Equal(0, chip.Attenuation(0));
        chip.Write(0x9F);
        Assert.Equal(15, chip.Attenuation(0));
    }

    [Fact]
    public void TheOsResetSequenceTurnsEveryChannelOff()
    {
        var chip = new Sn76489();
        foreach (byte value in new byte[] { 0x90, 0xB0, 0xD0, 0xF0 })
        {
            chip.Write(value);
        }
        foreach (byte value in new byte[] { 0x9F, 0xBF, 0xDF, 0xFF })
        {
            chip.Write(value);
        }
        for (int channel = 0; channel < 4; channel++)
        {
            Assert.Equal(15, chip.Attenuation(channel));
        }
    }

    [Fact]
    public void ADataByteAfterAnAttenuationLatchIsNotIgnored()
    {
        var chip = new Sn76489();
        chip.Write(0xDF);
        Assert.Equal(15, chip.Attenuation(2));
        chip.Write(0x00);
        Assert.Equal(0, chip.Attenuation(2));
        Assert.Equal(0, chip.TonePeriod(2)); // the latch was the attenuation, not the period
    }

    [Theory]
    [InlineData(0xE5, 0x5)] // white, NF = 01, N/1024
    [InlineData(0xE4, 0x4)] // white, NF = 00, N/512
    [InlineData(0xE3, 0x3)] // periodic, NF = 11, tone 3
    public void ANoiseWriteSetsTheControlAndResetsTheShiftRegister(int value, int control)
    {
        var chip = new Sn76489();
        chip.Write(0xE4);
        chip.Write(0xF0);
        chip.Run(10_000);
        Assert.NotEqual(Sn76489.ShiftRegisterSeed, chip.ShiftRegister);

        chip.Write((byte)value);
        Assert.Equal(control, chip.NoiseControl);
        Assert.Equal(Sn76489.ShiftRegisterSeed, chip.ShiftRegister);
    }

    [Fact]
    public void ADataByteAfterANoiseLatchAlsoResetsTheShiftRegister()
    {
        var chip = new Sn76489();
        chip.Write(0xE4);
        chip.Run(10_000);
        Assert.NotEqual(Sn76489.ShiftRegisterSeed, chip.ShiftRegister);

        chip.Write(0x05);
        Assert.Equal(5, chip.NoiseControl);
        Assert.Equal(Sn76489.ShiftRegisterSeed, chip.ShiftRegister);
    }

    // via.md s4.4: the output bit (bit 0) before each shift, from the seed &4000.
    [Fact]
    public void WhiteNoiseFromTheSeedMatchesTheSheetsVector()
    {
        Assert.Equal("0000000000000010000000000000110000000000", NoiseBits(0xE4, 40));
    }

    [Fact]
    public void PeriodicNoiseFromTheSeedMatchesTheSheetsVector()
    {
        Assert.Equal("00000000000000100000000000000100", NoiseBits(0xE0, 32));
    }

    [Fact]
    public void WhiteNoiseRepeatsAfter32767ShiftsAndPeriodicAfter15()
    {
        // A maximal 15-bit LFSR (s4.4); periodic is one 1 in every 15 shifts.
        Assert.Equal(32767, ShiftPeriod(0xE4));
        Assert.Equal(15, ShiftPeriod(0xE0));
    }

    // via.md s4.8, the noise rate tests: NF 00, 01 and 10 shift at 7812.5, 3906.25 and 1953.125 Hz.
    [Theory]
    [InlineData(0xE4, 7812.5, 520.83)]
    [InlineData(0xE5, 3906.25, 260.42)]
    [InlineData(0xE6, 1953.125, 130.21)]
    public void NoiseShiftsAtTheSheetsRates(int control, double white, double periodicNote)
    {
        var chip = new Sn76489();
        chip.Write((byte)control);
        List<long> shifts = Shifts(chip, 4);
        long interval = shifts[3] - shifts[2];
        Assert.Equal(interval, shifts[2] - shifts[1]);
        Assert.Equal(white, (double)Sn76489.ClockRate / interval, 3);
        Assert.Equal(periodicNote, (double)Sn76489.ClockRate / interval / 15, 2);
    }

    [Fact]
    public void NoiseRateThreeFollowsToneThree()
    {
        // 125000 / N3 Hz (s4.2): a shift every 2 N3 clocks.
        var chip = new Sn76489();
        chip.Write(0xC4);
        chip.Write(0x06); // tone 3 period 100
        chip.Write(0xE7);
        List<long> shifts = Shifts(chip, 4);
        Assert.Equal(200, shifts[3] - shifts[2]);
        Assert.Equal(200, shifts[2] - shifts[1]);
    }

    [Fact]
    public void APeriodOfZeroIsTakenAs1024()
    {
        // Not established for a real chip (s4.3): this model counts a 10-bit 0 as 1024, the sheet's
        // recommendation, and docs/known-differences.md says so.
        var chip = new Sn76489();
        chip.Write(0x80);
        chip.Write(0x00);
        List<long> toggles = Toggles(chip, 0, count: 3);
        Assert.Equal(1024, toggles[2] - toggles[1]);
    }

    [Fact]
    public void AChannelSwingsBetweenZeroAndItsAmplitude()
    {
        // Unipolar, 0 to A(n), not -A to +A (s4.6).
        var chip = new Sn76489();
        chip.Write(0x84);
        chip.Write(0x06);
        chip.Write(0x92);
        var seen = new HashSet<float>();
        for (int i = 0; i < 3000; i++)
        {
            chip.Tick();
            seen.Add(chip.ChannelOutput(0));
        }

        Assert.Equal(new HashSet<float> { 0f, Sn76489.Volume(2) }, seen);
    }

    [Fact]
    public void TheBufferGetsOneSampleForEachSliceOfClocksAtItsRate()
    {
        // 250 kHz into 48 kHz: sample j is the mean of clocks floor(j * 250000 / 48000) + 1 to
        // floor((j + 1) * 250000 / 48000), so every sample is five or six clocks and none is lost.
        var buffer = new SoundBuffer(48_000);
        var chip = new Sn76489(buffer);
        chip.Run(250_000);
        Assert.Equal(48_000, buffer.Count);

        // The 48,001st sample is clocks 250,001 to floor(48001 * 250000 / 48000) = 250,005.
        chip.Run(4);
        Assert.Equal(48_000, buffer.Count);
        Assert.Equal(0, buffer.Overruns);
        chip.Run(1);
        Assert.Equal(48_000, buffer.Count); // full: the oldest went
        Assert.Equal(1, buffer.Overruns);
    }

    [Fact]
    public void ASampleIsTheMeanOfItsClocks()
    {
        // Period 1 toggles every clock, so a sample of eight clocks holds four high ones: half the
        // channel's amplitude, which is the n = 1 constant level sampled sound relies on (s4.3).
        var buffer = new SoundBuffer(Sn76489.ClockRate / 8);
        var chip = new Sn76489(buffer);
        chip.Write(0x81);
        chip.Write(0x00);
        chip.Write(0x90);
        chip.Run(6_000);

        var samples = new float[buffer.Count];
        buffer.Read(samples);
        Assert.Equal(0.125f, samples[^1]); // a half of 0.25, one channel's share at full volume
    }

    [Fact]
    public void TheBufferKeepsTheNewestSecondAndCountsWhatItDrops()
    {
        var buffer = new SoundBuffer(100);
        Assert.Equal(100, buffer.SampleRate);
        Assert.Equal(100, buffer.Capacity);

        for (int i = 0; i < 150; i++)
        {
            buffer.Add(i);
        }

        Assert.Equal(100, buffer.Count);
        Assert.Equal(50, buffer.Overruns);

        var first = new float[30];
        Assert.Equal(30, buffer.Read(first));
        Assert.Equal(50f, first[0]);
        Assert.Equal(79f, first[29]);

        var rest = new float[200];
        Assert.Equal(70, buffer.Read(rest));
        Assert.Equal(80f, rest[0]);
        Assert.Equal(149f, rest[69]);
        Assert.Equal(0, buffer.Read(rest));
    }

    [Fact]
    public void AChipInAMachineRefusesATick()
    {
        var machine = new BbcMachine(BbcSession.Roms);
        Assert.Throws<InvalidOperationException>(() => machine.Bus.SoundChip.Tick());
        Assert.Throws<InvalidOperationException>(() => machine.Bus.SoundChip.Run(1));
    }

    private static List<long> Toggles(Sn76489 chip, int channel, int count)
    {
        var toggles = new List<long>();
        bool level = chip.ChannelHigh(channel);
        while (toggles.Count < count)
        {
            chip.Tick();
            if (chip.ChannelHigh(channel) != level)
            {
                level = !level;
                toggles.Add(chip.Clocks);
            }
            Assert.True(chip.Clocks < 10_000, "the channel stopped toggling");
        }
        return toggles;
    }

    private static List<long> Shifts(Sn76489 chip, int count)
    {
        var shifts = new List<long>();
        int register = chip.ShiftRegister;
        while (shifts.Count < count)
        {
            chip.Tick();
            if (chip.ShiftRegister != register)
            {
                register = chip.ShiftRegister;
                shifts.Add(chip.Clocks);
            }
            Assert.True(chip.Clocks < 10_000, "the shift register stopped");
        }
        return shifts;
    }

    /// <summary>The output bit before each of the first <paramref name="count"/> shifts after a write of <paramref name="control"/>.</summary>
    private static string NoiseBits(int control, int count)
    {
        var chip = new Sn76489();
        chip.Write((byte)control);
        var bits = new System.Text.StringBuilder();
        int register = chip.ShiftRegister;
        bits.Append(register & 1);
        while (bits.Length < count)
        {
            chip.Tick();
            if (chip.ShiftRegister != register)
            {
                register = chip.ShiftRegister;
                bits.Append(register & 1);
            }
            Assert.True(chip.Clocks < 100_000, "the shift register stopped");
        }
        return bits.ToString();
    }

    private static int ShiftPeriod(int control)
    {
        var chip = new Sn76489();
        chip.Write((byte)control);
        int seed = chip.ShiftRegister;
        int register = seed;
        int shifts = 0;
        while (true)
        {
            chip.Tick();
            Assert.True(chip.Clocks < 2_000_000, "the shift register did not come back to its seed");
            if (chip.ShiftRegister == register)
            {
                continue;
            }
            register = chip.ShiftRegister;
            shifts++;
            if (register == seed)
            {
                return shifts;
            }
        }
    }
}
