using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The lazy SN76489, which works out a span of chip clocks at once and skips what cannot be heard,
/// against <see cref="ReferenceSn76489"/>, which does one clock at a time: random writes at random
/// clocks, random spans before each look, and the samples compared one by one, exactly, as the
/// buffer gives them back. Then the same in the machine, driven by the real OS and BASIC.
/// </summary>
public class SoundEquivalenceTests
{
    [Theory]
    [InlineData(1, 48_000)]
    [InlineData(2, 44_100)]
    [InlineData(3, 250_000)]
    [InlineData(4, 31_250)]
    [InlineData(5, 1_000)]
    [InlineData(6, 7)]
    [InlineData(7, 48_000)]
    [InlineData(8, 44_100)]
    public void TheLazyChipMatchesTheClockByClockOneSampleBySample(int seed, int rate)
    {
        var random = new Random(seed);
        var buffer = new SoundBuffer(rate);
        var chip = new Sn76489(buffer);
        var reference = new ReferenceSn76489(rate);
        var ring = new RingModel(rate);
        int compared = 0, heard = 0;

        for (int step = 0; step < 4_000; step++)
        {
            int choice = random.Next(100);
            if (choice < 45)
            {
                byte value = RandomByte(random);
                chip.Write(value);
                reference.Write(value);
            }
            else if (choice < 50)
            {
                chip.Tick();
                reference.Tick();
            }
            else
            {
                // Mostly short, sometimes longer than the buffer's second (250,000 clocks), which
                // makes only the newest second and drops the rest.
                long span = random.Next(40) switch
                {
                    < 20 => random.Next(0, 12),
                    < 32 => random.Next(0, 400),
                    < 36 => random.Next(0, 5_000),
                    < 39 => random.Next(0, 200_000),
                    _ => random.Next(250_000, 700_000),
                };
                chip.Run(span);
                reference.Run(span);
            }

            ring.Take(reference.Samples);
            Assert.Equal(reference.Clocks, chip.Clocks);
            Assert.Equal(reference.ShiftRegister, chip.ShiftRegister);
            for (int channel = 0; channel < 4; channel++)
            {
                Assert.Equal(reference.ChannelHigh(channel), chip.ChannelHigh(channel));
                Assert.Equal(reference.Attenuation(channel), chip.Attenuation(channel));
            }

            if (random.Next(8) == 0)
            {
                var drained = new float[random.Next(1, Math.Min(rate + 10, 60_000))];
                int count = buffer.Read(drained);
                float[] expected = ring.Read(drained.Length);
                Assert.Equal(expected.Length, count);
                for (int i = 0; i < count; i++)
                {
                    Assert.True(expected[i] == drained[i], $"seed {seed}, step {step}: sample {i} of the read is {drained[i]}, the oracle {expected[i]}");
                    heard += drained[i] != 0 ? 1 : 0;
                }
                compared += count;
                Assert.Equal(ring.Overruns, buffer.Overruns);
            }
        }

        // Every path was taken: sounded samples, silent ones, and spans that overflowed the buffer.
        Assert.True(compared > 10_000 || rate < 1_000, $"only {compared} samples compared");
        Assert.True(heard > compared / 10, $"only {heard} of {compared} samples were sounded");
        Assert.True(compared - heard > compared / 100, $"only {compared - heard} of {compared} samples were silent");
        Assert.True(buffer.Overruns > 0, "no span overflowed the buffer");
    }

    [Fact]
    public void TheMachineMatchesTheOracleThroughTheOsSoundSystem()
    {
        // BASIC drives every channel through the OS: tones, noise, envelopes that move pitch and
        // amplitude every centisecond, and the queue. Every write the system VIA strobes into the
        // chip goes to the oracle at the chip clock of its CPU cycle, and the machine's samples,
        // read at random moments, must equal the oracle's one by one.
        var session = new BbcSession();
        BbcMachine machine = session.Machine;
        SoundBuffer sound = machine.Sound;
        var reference = new ReferenceSn76489(sound.SampleRate);
        var random = new Random(9);
        int writes = 0, compared = 0, heard = 0;
        machine.Bus.SystemVia.SoundWrite += value =>
        {
            reference.Run((machine.Cycles / Sn76489.CpuCyclesPerClock) - reference.Clocks);
            reference.Write(value);
            writes++;
        };

        void Compare()
        {
            var drained = new float[random.Next(1, sound.Capacity)];
            int count = sound.Read(drained);
            reference.Run((machine.Cycles / Sn76489.CpuCyclesPerClock) - reference.Clocks);
            Assert.True(reference.Samples.Count >= count, $"the machine has {count} samples, the oracle {reference.Samples.Count}");
            for (int i = 0; i < count; i++)
            {
                Assert.True(reference.Samples[i] == drained[i], $"after {compared} samples: sample {i} is {drained[i]}, the oracle {reference.Samples[i]}");
                heard += drained[i] != 0 ? 1 : 0;
            }
            reference.Samples.RemoveRange(0, count);
            compared += count;
        }

        // The boot, with the start-up beep, then two lines typed a key at a time, looking between keys.
        machine.PowerOn();
        for (int i = 0; i < 12; i++)
        {
            session.RunFor(500_000);
            Compare();
        }

        foreach (string line in new[]
        {
            "ENVELOPE 1,1,4,-4,2,5,5,5,126,-2,-2,-2,126,90\r",
            "FOR I=0 TO 7:SOUND 0,-12,I,6:SOUND 1,1,I*20,8:SOUND 2,-9,100+I,5:SOUND 3,-15,200-I,4:NEXT\r",
        })
        {
            foreach (char c in line)
            {
                session.Type(c.ToString());
                Compare();
            }
        }

        for (int look = 0; look < 400; look++)
        {
            session.RunFor(random.Next(1, 40_000));
            Compare();
        }

        Assert.Equal(0, sound.Overruns);
        Assert.True(writes > 200, $"only {writes} writes reached the chip");
        Assert.True(heard > 50_000, $"only {heard} of {compared} samples were sounded");
    }

    /// <summary>A byte the OS might write, and many it would not: every register, short periods, every noise mode.</summary>
    private static byte RandomByte(Random random) => random.Next(10) switch
    {
        // An attenuation, mostly audible.
        < 3 => (byte)(0x90 | (random.Next(4) << 5) | (random.Next(3) == 0 ? 15 : random.Next(15))),

        // A period's low nibble, or its high six bits, kept short a third of the time.
        < 5 => (byte)(0x80 | (random.Next(3) << 5) | random.Next(16)),
        < 7 => (byte)(random.Next(3) == 0 ? random.Next(2) : random.Next(64)),

        // The noise control, any mode.
        < 8 => (byte)(0xE0 | random.Next(8)),

        _ => (byte)random.Next(256),
    };

    /// <summary>The buffer's rule, kept beside the oracle: the newest second, the oldest dropped and counted.</summary>
    private sealed class RingModel(int capacity)
    {
        private readonly Queue<float> _samples = new();

        public long Overruns { get; private set; }

        public void Take(List<float> samples)
        {
            foreach (float sample in samples)
            {
                _samples.Enqueue(sample);
                if (_samples.Count > capacity)
                {
                    _samples.Dequeue();
                    Overruns++;
                }
            }
            samples.Clear();
        }

        public float[] Read(int count)
        {
            var read = new float[Math.Min(count, _samples.Count)];
            for (int i = 0; i < read.Length; i++)
            {
                read[i] = _samples.Dequeue();
            }
            return read;
        }
    }
}
