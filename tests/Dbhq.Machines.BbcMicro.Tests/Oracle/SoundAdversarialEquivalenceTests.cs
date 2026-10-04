using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// <see cref="SoundEquivalenceTests"/> pushed at its edges: the lazy chip against the
/// clock-by-clock oracle at sample rates from 1 a second to one above half the chip's 250 kHz,
/// with runs that stop on a sample's boundary or one clock either side of it, writes of every
/// kind (noise control, a period's low and high halves, attenuation, bytes with no meaning), runs
/// of up to 2.2 million clocks with white noise playing, and the buffer read in random amounts so
/// it overruns. After every step the clock count, the shift register and each channel's
/// flip-flop must match, and every sample read must be the oracle's, bit for bit.
/// </summary>
/// <remarks>
/// Written by task 11's reviewer as a scratch check and kept (task 15) with four of its twelve
/// rates, so it runs in seconds. It pins the model, including its choices, not the hardware.
/// </remarks>
public class SoundAdversarialEquivalenceTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(101, 3)]
    [InlineData(110, 48_000)]
    [InlineData(108, 125_001)]
    public void TheLazyChipMatchesTheOracleAtTheEdges(int seed, int rate)
    {
        var random = new Random(seed);
        var buffer = new SoundBuffer(rate);
        var chip = new Sn76489(buffer);
        var reference = new ReferenceSn76489(rate);
        var queue = new Queue<float>();
        long overruns = 0;
        long compared = 0, heard = 0, longNoiseSpans = 0;

        void Take()
        {
            foreach (float s in reference.Samples)
            {
                queue.Enqueue(s);
                if (queue.Count > rate)
                {
                    queue.Dequeue();
                    overruns++;
                }
            }
            reference.Samples.Clear();
        }

        long NextBoundary(long clocks)
        {
            // The first clock count at which a sample ends after this one: the smallest
            // floor((j + 1) * clock rate / rate) above it.
            long j = (clocks * rate) / Sn76489.ClockRate;
            while (true)
            {
                long end = (j + 1) * Sn76489.ClockRate / rate;
                if (end > clocks)
                {
                    return end;
                }
                j++;
            }
        }

        for (int step = 0; step < 2_500; step++)
        {
            int choice = random.Next(100);
            if (choice < 40)
            {
                byte value = random.Next(12) switch
                {
                    0 => (byte)(0xE0 | random.Next(8)),
                    1 => 0xE4,
                    2 => (byte)(0xC0 | random.Next(2)),
                    3 => 0x00,
                    4 => 0xFF,
                    5 => (byte)(0x9F | (random.Next(4) << 5)),
                    6 => (byte)(0x90 | (random.Next(4) << 5) | random.Next(16)),
                    7 => (byte)(0x80 | (random.Next(3) << 5) | random.Next(16)),
                    8 => (byte)random.Next(64),
                    9 => 0xE7,
                    _ => (byte)random.Next(256),
                };
                chip.Write(value);
                reference.Write(value);
            }
            else if (choice < 70)
            {
                // To a sample boundary, one before, or one after.
                long boundary = NextBoundary(chip.Clocks) + random.Next(-1, 2);
                long span = Math.Max(0, boundary - chip.Clocks);
                chip.Run(span);
                reference.Run(span);
            }
            else
            {
                long span = random.Next(20) switch
                {
                    < 10 => random.Next(0, 50),
                    < 16 => random.Next(0, 20_000),
                    < 18 => random.Next(0, 300_000),
                    _ => random.Next(1_000_000, 2_200_000),
                };
                if (span > 1_000_000 && (chip.NoiseControl & 4) != 0)
                {
                    longNoiseSpans++;
                }
                chip.Run(span);
                reference.Run(span);
            }

            Take();
            Assert.Equal(reference.Clocks, chip.Clocks);
            Assert.Equal(reference.ShiftRegister, chip.ShiftRegister);
            for (int channel = 0; channel < 4; channel++)
            {
                Assert.Equal(reference.ChannelHigh(channel), chip.ChannelHigh(channel));
            }

            if (random.Next(5) == 0)
            {
                var drained = new float[random.Next(1, rate + 5)];
                int count = buffer.Read(drained);
                int expectedCount = Math.Min(drained.Length, queue.Count);
                Assert.Equal(expectedCount, count);
                for (int i = 0; i < count; i++)
                {
                    float e = queue.Dequeue();
                    Assert.True(e == drained[i], $"seed {seed} rate {rate} step {step}: sample {i} {drained[i]} vs {e}");
                    heard += drained[i] != 0 ? 1 : 0;
                }
                compared += count;
                Assert.Equal(overruns, buffer.Overruns);
            }
        }

        Assert.True(compared > 0, "no sample was compared");
        Assert.True(heard > 0, "every sample compared was silence");
        Assert.True(longNoiseSpans > 0, "no long run with white noise playing");
    }
}
