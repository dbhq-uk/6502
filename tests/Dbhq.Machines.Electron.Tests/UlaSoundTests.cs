using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The ULA's one-bit sound (ula.md s8). Times are 2 MHz cycles from power on, and the tests drive
/// the sound directly with a clock they set, so every expected number is arithmetic on the sheet's
/// formula: the output toggles every 32 x (S + 1) cycles, which is a frequency of
/// 1 MHz / (32 x (S + 1)) (s8, from S1 14.1 fig 14.5b).
/// </summary>
public class UlaSoundTests
{
    private const int CounterRegister = 6;
    private const int ControlRegister = 7;

    // $FE07 bits 2 and 1 pick what the one pin does (s8): 00 cassette in, 01 sound, 10 cassette out.
    private const byte SoundMode = 0b0000_0010;
    private const byte CassetteIn = 0b0000_0000;
    private const byte CassetteOut = 0b0000_0100;
    private const byte Unused = 0b0000_0110;

    private const long CyclesPerSecond = 2_000_000;

    private sealed class Rig
    {
        public long Now;

        public Rig(int sampleRate = 44_100)
        {
            Sound = new UlaSound(sampleRate, () => Now);
        }

        public UlaSound Sound { get; }

        /// <summary>Writes the register at the current time.</summary>
        public void Write(int register, byte value) => Sound.Write(register, value, Now);

        /// <summary>Sound mode with counter <paramref name="s"/>, both written at the current time.</summary>
        public void Start(int s)
        {
            Write(ControlRegister, SoundMode);
            Write(CounterRegister, (byte)s);
        }

        public void RunTo(long cycle)
        {
            Now = cycle;
            Sound.CatchUp(cycle);
        }

        public float[] Drain()
        {
            var all = new float[Sound.Buffer.Count];
            Sound.Buffer.Read(all);
            return all;
        }
    }

    [Fact]
    public void WithSOf15TheOutputTogglesEvery512CyclesSoItsPitchIs1953Hz()
    {
        // s8: S = 15 gives 32 x 16 = 512 cycles between toggles, a wave of 1,024 cycles, which is
        // 2,000,000 / 1,024 = 1,953.125 Hz (1 MHz / (32 x 16) = 1,953.125).
        // 100,000 cycles hold 100,000 / 512 = 195.3 toggles, so 195 whole ones (196 if the first
        // fell a hair earlier, so either is a pass).
        var rig = new Rig();
        rig.Start(15);
        rig.RunTo(100_000);
        Assert.InRange(rig.Sound.Toggles, 195, 196);
    }

    [Fact]
    public void WithSOf255TheOutputIs122Hz()
    {
        // s8: S = 255 gives 32 x 256 = 8,192 cycles between toggles, a wave of 16,384 cycles, so
        // 2,000,000 / 16,384 = 122.07 Hz (1 MHz / 8,192 = 122.07). A second of machine time,
        // 2,000,000 cycles, holds 2,000,000 / 8,192 = 244.14 toggles: 244 whole ones, which is
        // 122 whole waves.
        var rig = new Rig();
        rig.Start(255);
        rig.RunTo(CyclesPerSecond);
        Assert.Equal(244, rig.Sound.Toggles);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WithSOf0Or1TheOutputIsAConstantLevel(int s)
    {
        // s8: the two lowest values are inaudible, a constant level, usable as an on-off speaker bit.
        var rig = new Rig();
        rig.Start(s);
        rig.RunTo(CyclesPerSecond);
        Assert.Equal(0, rig.Sound.Toggles);
        Assert.False(rig.Sound.Level);
    }

    [Theory]
    [InlineData(CassetteIn)]
    [InlineData(CassetteOut)]
    [InlineData(Unused)]
    public void OutsideSoundModeTheOutputNeverToggles(byte mode)
    {
        // s8: sound is 01 in $FE07 bits 2 and 1; any other value is not sound, however long it runs.
        var rig = new Rig();
        rig.Write(ControlRegister, mode);
        rig.Write(CounterRegister, 15);
        rig.RunTo(5 * CyclesPerSecond);
        Assert.Equal(0, rig.Sound.Toggles);
    }

    [Fact]
    public void TheOtherBitsOfTheControlRegisterDoNotMatter()
    {
        // The mode, the motor and the caps lock LED share $FE07 (s1c); only bits 2 and 1 pick sound.
        // $B2 is mode 6 (bits 5 to 3 = 110), caps LED on (bit 7), and 01 in bits 2 and 1.
        var rig = new Rig();
        rig.Write(ControlRegister, 0xB2);
        rig.Write(CounterRegister, 15);
        rig.RunTo(100_000);
        Assert.InRange(rig.Sound.Toggles, 195, 196);
    }

    [Fact]
    public void AWriteToTheCounterRestartsTheDivider()
    {
        // The choice recorded in known-differences (s12 item 9). Written at cycle 0 with S = 15
        // the first toggle is at 0 + 512. A second write at 400 restarts the count, so the first
        // toggle is at 400 + 512 = 912, not 512.
        var rig = new Rig();
        rig.Start(15);
        rig.RunTo(400);
        rig.Write(CounterRegister, 15);
        rig.RunTo(911);
        Assert.Equal(0, rig.Sound.Toggles);
        rig.RunTo(912);
        Assert.Equal(1, rig.Sound.Toggles);
        rig.RunTo(912 + 511);
        Assert.Equal(1, rig.Sound.Toggles);
        rig.RunTo(912 + 512);
        Assert.Equal(2, rig.Sound.Toggles);
    }

    [Fact]
    public void ANewCounterSetsThePeriodFromTheWrite()
    {
        // S = 15 then, at cycle 100, S = 31: 32 x 32 = 1,024 cycles to the next toggle, so the
        // first one is at 100 + 1,024 = 1,124.
        var rig = new Rig();
        rig.Start(15);
        rig.RunTo(100);
        rig.Write(CounterRegister, 31);
        rig.RunTo(1_123);
        Assert.Equal(0, rig.Sound.Toggles);
        rig.RunTo(1_124);
        Assert.Equal(1, rig.Sound.Toggles);
    }

    [Fact]
    public void AControlWriteThatStaysInSoundModeLeavesTheDividerAlone()
    {
        // The OS writes $FE07 for the screen mode and the LED as well as for sound (s8, s10c), so
        // a write that keeps bits 2 and 1 at 01 must not restart the count. S = 15 from cycle 0
        // toggles at 512, 1,024, ...; a $FE07 write at 300 changes only the display mode bits.
        var rig = new Rig();
        rig.Start(15);
        rig.RunTo(300);
        rig.Write(ControlRegister, 0x30 | SoundMode);
        rig.RunTo(512);
        Assert.Equal(1, rig.Sound.Toggles);
    }

    [Fact]
    public void LeavingSoundModeStopsTheToggleAndHoldsTheLevel()
    {
        // s8: outside sound mode the output holds its last level. S = 15: toggles at 512 (high)
        // and 1,024 (low), 1,536 (high). Leave at 1,600 with the level high.
        var rig = new Rig();
        rig.Start(15);
        rig.RunTo(1_600);
        Assert.Equal(3, rig.Sound.Toggles);
        Assert.True(rig.Sound.Level);
        rig.Write(ControlRegister, CassetteIn);
        rig.RunTo(1_600 + 10 * CyclesPerSecond);
        Assert.Equal(3, rig.Sound.Toggles);
        Assert.True(rig.Sound.Level);
    }

    [Fact]
    public void EnteringSoundModeHoldsTheLevelAndStartsTheDivider()
    {
        // The choice recorded in known-differences (s12 item 9): the level on entry is the level
        // the output held, and the divider starts at the write. High, then out of sound mode, then
        // back in at cycle 5,000 with S = 15: the level is still high, nothing toggles at the
        // write, and the first toggle is at 5,000 + 512.
        var rig = new Rig();
        rig.Start(15);
        rig.RunTo(1_600);
        rig.Write(ControlRegister, CassetteIn);
        rig.RunTo(5_000);
        rig.Write(ControlRegister, SoundMode);
        Assert.True(rig.Sound.Level);
        Assert.Equal(3, rig.Sound.Toggles);
        rig.RunTo(5_000 + 511);
        Assert.Equal(3, rig.Sound.Toggles);
        rig.RunTo(5_000 + 512);
        Assert.Equal(4, rig.Sound.Toggles);
        Assert.False(rig.Sound.Level);
    }

    [Fact]
    public void ACounterWrittenOutsideSoundModeIsKeptForWhenSoundStarts()
    {
        // Tape mode uses $FE06 for the baud setting (s9); a program may set S first and the mode
        // after. Nothing toggles meanwhile, and S is what was written.
        var rig = new Rig();
        rig.Write(ControlRegister, CassetteOut);
        rig.Write(CounterRegister, 7);
        rig.RunTo(10_000);
        Assert.Equal(0, rig.Sound.Toggles);
        Assert.Equal(7, rig.Sound.Counter);
        rig.RunTo(20_000);
        rig.Write(ControlRegister, SoundMode);
        rig.RunTo(20_000 + 32 * 8 - 1);
        Assert.Equal(0, rig.Sound.Toggles);
        rig.RunTo(20_000 + 32 * 8);
        Assert.Equal(1, rig.Sound.Toggles);
    }

    [Fact]
    public void ASecondOfS15AtTheSampleRateIsThatManySamplesOfASquareWave()
    {
        // 2,000,000 cycles is one second, so at 44,100 samples a second the buffer holds exactly
        // 44,100. S = 15 is 1,953.125 Hz, which crosses zero twice a wave: 3,906.25 times a
        // second. The signal is a square wave about silence (the amplifier's coupling takes out
        // the level it sits at), so its mean is near 0 and it swings both ways.
        var rig = new Rig(44_100);
        rig.Start(15);
        rig.RunTo(CyclesPerSecond);
        Assert.Equal(44_100, rig.Sound.Buffer.Count);

        float[] samples = rig.Drain();
        int crossings = 0;
        for (int i = 1; i < samples.Length; i++)
        {
            if ((samples[i - 1] < 0) != (samples[i] < 0))
            {
                crossings++;
            }
        }

        Assert.InRange(crossings, (int)(3_906.25 * 0.98), (int)(3_906.25 * 1.02));
        Assert.InRange(samples.Average(), -0.02, 0.02);
        Assert.True(samples.Max() > 0.3f);
        Assert.True(samples.Min() < -0.3f);
        Assert.All(samples, s => Assert.InRange(s, -1f, 1f));
    }

    [Fact]
    public void ASampleIsTheMeanOfTheLevelOverItsInterval()
    {
        // At 500 samples a second a sample is 2,000,000 / 500 = 4,000 cycles. S = 15 written at 0:
        // low to 512, high 512 to 1,024, low to 1,536, high 1,536 to 2,048, low to 2,560, high
        // 2,560 to 3,072, low to 3,584, high from 3,584. Up to 4,000 the level was high for
        // 512 + 512 + 512 + (4,000 - 3,584) = 1,952 cycles: a mean of 1,952 / 4,000 = 0.488. The
        // first sample has nothing before it for the coupling to remove, so it is that mean.
        var rig = new Rig(500);
        rig.Start(15);
        rig.RunTo(4_000);
        Assert.Equal(1, rig.Sound.Buffer.Count);
        float[] first = rig.Drain();
        Assert.Equal(0.488f, first[0], 1e-6f);
    }

    [Fact]
    public void SilenceIsZeroWhetherNothingWasEverPlayedOrALevelIsHeld()
    {
        var rig = new Rig(44_100);
        rig.RunTo(CyclesPerSecond);
        Assert.Equal(0, rig.Sound.Toggles);
        Assert.All(rig.Drain(), s => Assert.Equal(0f, s));

        // Held high, outside sound mode: a level held for ever is not a sound, so the samples
        // settle back to exactly 0 and stay there (the amplifier is coupled, not direct).
        rig.Start(15);
        rig.RunTo(CyclesPerSecond + 1_600);
        Assert.True(rig.Sound.Level);
        rig.Write(ControlRegister, CassetteIn);
        rig.RunTo(4 * CyclesPerSecond);
        float[] tail = rig.Drain();
        Assert.Equal(0f, tail[^1]);
        Assert.Equal(0f, tail[^44_100]);
    }

    [Fact]
    public void AConstantLevelFromSOf1IsSilentToo()
    {
        // S = 1 is a constant level (s8): the output does not change, so there is nothing to play.
        var rig = new Rig(44_100);
        rig.Start(1);
        rig.RunTo(CyclesPerSecond);
        Assert.All(rig.Drain(), s => Assert.Equal(0f, s));
    }

    [Fact]
    public void ABufferNobodyReadsKeepsTheNewestSecondAndCountsWhatItDropped()
    {
        // At 1,000 samples a second the buffer holds 1,000. Two seconds make 2,000 samples, so
        // 1,000 are dropped; nine more seconds of silence drop 9,000 more.
        var rig = new Rig(1_000);
        rig.Start(15);
        rig.RunTo(2 * CyclesPerSecond);
        Assert.Equal(1_000, rig.Sound.Buffer.Capacity);
        Assert.Equal(1_000, rig.Sound.Buffer.Count);
        Assert.Equal(1_000, rig.Sound.Buffer.Overruns);

        rig.Write(ControlRegister, CassetteIn);
        rig.RunTo(11 * CyclesPerSecond);
        Assert.Equal(1_000, rig.Sound.Buffer.Count);
        Assert.Equal(10_000, rig.Sound.Buffer.Overruns);
    }

    [Fact]
    public void AnHourOfSilenceIsCountedAndDoesNotFillTheBufferOneSampleAtATime()
    {
        const long Hour = 3_600 * CyclesPerSecond;
        var rig = new Rig(44_100);
        rig.RunTo(Hour);
        Assert.Equal(44_100, rig.Sound.Buffer.Count);
        Assert.Equal(3_600L * 44_100 - 44_100, rig.Sound.Buffer.Overruns);
    }

    [Fact]
    public void ReadingTheBufferGivesTheOldestFirstAndEmptiesWhatItTook()
    {
        var rig = new Rig(44_100);
        rig.Start(15);
        rig.RunTo(CyclesPerSecond / 10);
        int made = rig.Sound.Buffer.Count;
        Assert.Equal(4_410, made);

        var everything = new float[made];
        // A copy of the buffer's contents in two reads must equal one read of the same samples.
        var twin = new Rig(44_100);
        twin.Start(15);
        twin.RunTo(CyclesPerSecond / 10);
        float[] expected = twin.Drain();

        var firstPart = new float[1_000];
        Assert.Equal(1_000, rig.Sound.Buffer.Read(firstPart));
        Assert.Equal(made - 1_000, rig.Sound.Buffer.Count);
        var rest = new float[made];
        Assert.Equal(made - 1_000, rig.Sound.Buffer.Read(rest));
        firstPart.CopyTo(everything, 0);
        rest.AsSpan(0, made - 1_000).CopyTo(everything.AsSpan(1_000));
        Assert.Equal(expected, everything);
        Assert.Equal(0, rig.Sound.Buffer.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(384_001)]
    public void ASampleRateThatCannotBeUsedIsRefused(int rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UlaSound(rate, () => 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SoundBuffer(rate));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8_000)]
    [InlineData(384_000)]
    public void TheEdgesOfTheSampleRateRangeWork(int rate)
    {
        var rig = new Rig(rate);
        rig.Start(15);
        rig.RunTo(CyclesPerSecond);
        Assert.Equal(rate, rig.Sound.Buffer.Count + rig.Sound.Buffer.Overruns);
    }

    [Fact]
    public void ARegisterOtherThanTheCounterAndTheControlIsRefused()
    {
        var rig = new Rig();
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Write(5, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rig.Write(16, 0));
    }
}
