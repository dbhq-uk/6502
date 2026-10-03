namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The Texas Instruments SN76489AN, IC18 on the Model B: three square-wave tone channels and a
/// noise channel, each with a 2 dB-step attenuator, mixed into one output.
/// </summary>
/// <remarks>
/// <para>
/// Built from the fact sheet <c>docs/bbc-micro/facts/via.md</c> section 4. Channels here are TI's,
/// counted from 0: tone 1, 2 and 3 are channels 0, 1 and 2 and the noise is channel 3. The OS's
/// SOUND channels run the other way: SOUND 1, 2 and 3 are chip channels 2, 1 and 0, and SOUND 0 is
/// the noise (s4.7).
/// </para>
/// <para>
/// <b>Writes (s4.2).</b> A byte with bit 7 set latches a register, <c>1 cc t dddd</c>, and puts its
/// low four bits in the low four bits of that register; a byte with bit 7 clear puts its low six
/// bits in the high six bits of a latched tone period, or its low bits in a latched attenuation or
/// noise control, and the latch stays. A write to the noise control, either kind, resets the shift
/// register to <see cref="ShiftRegisterSeed"/>. Only a write changes a register.
/// </para>
/// <para>
/// <b>Time (s4.3).</b> The chip runs on the video ULA's 4 MHz, divided by 16: one chip clock, at
/// 250 kHz, every eight 2 MHz CPU cycles. Each clock each tone's counter counts down; at zero it
/// reloads with the period and toggles the channel's flip-flop, so a period of n is a square wave
/// of 125000 / n Hz. A period of 0 counts as 1024, which the sheet recommends and no source
/// establishes (s4.3; <c>docs/known-differences.md</c>). The noise has its own counter, reloaded
/// with 16, 32 or 64, or with tone 3's period for rate 3, and its own flip-flop; each time that
/// flip-flop rises the 15-bit shift register shifts right, bit 14 taking bit 0 XOR bit 1 for white
/// noise or bit 0 alone for periodic, and bit 0 is the noise channel's output (s4.4). A counter is
/// never reset by a write: a new period takes effect at the next reload.
/// </para>
/// <para>
/// <b>Output (s4.5, s4.6).</b> Each channel swings between 0 and its amplitude, 10^(-2n/20) for an
/// attenuation n of 0 to 14 and nothing for 15, not between minus and plus. The mix is the sum of
/// the four, divided by four, so it runs from 0 to 1. The levels are kept as whole numbers out of
/// 32767, the sheet's column, so a sample's sum is exact.
/// </para>
/// <para>
/// <b>Samples.</b> Sample j of a <see cref="SoundBuffer"/> at S samples a second is the mean of the
/// mix after each chip clock from floor(j 250000 / S) + 1 to floor((j + 1) 250000 / S): a box
/// average of whole clocks, five or six of them at 48 kHz, with no clock lost or counted twice.
/// It is a rational resample, chosen over an integer ratio, which would have forced a sample rate
/// no browser's audio runs at (250 kHz over 5 is 50 kHz), and over sampling the mix at one point a
/// sample, which turns a period-1 tone's 125 kHz into noise rather than the steady half level a
/// real BBC's filters make of it (s4.3).
/// </para>
/// <para>
/// <b>Lazily.</b> In a machine the chip is never ticked. It keeps the clock it has reached and
/// works out the clocks it owes when it is written to, looked at, or its buffer is read, and each
/// channel is worked out over a span at once: a tone's high clocks and its counter come from the
/// span's length in a few divisions, and the shift register is stepped once per shift, of which
/// there are at most 125,000 a second. While all four channels are off the samples are silence,
/// written in one go; a span that would make more samples than the buffer holds makes only the
/// newest buffer's worth, as the rest would be dropped. A write lands after the chip clock of its
/// CPU cycle, so the output changes from that clock, in the middle of a sample if it falls there.
/// Nothing the chip does is visible to the CPU (its READY line is not wired, s4.1), so it names no
/// event to the bus.
/// </para>
/// <para>
/// <b>Power on.</b> What a real chip holds at power on is not known (s4, "Could NOT establish");
/// the model starts silent, with every register 0, the tone counters at 1024, the noise counter at
/// 16, every flip-flop low and the shift register at its seed. The chip has no reset pin, so BREAK
/// leaves it; the OS silences it at every reset.
/// </para>
/// </remarks>
public sealed class Sn76489
{
    /// <summary>Chip clocks a second: 4 MHz divided by 16 (s4.3).</summary>
    public const int ClockRate = 250_000;

    /// <summary>2 MHz CPU cycles to a chip clock.</summary>
    public const int CpuCyclesPerClock = 8;

    /// <summary>The noise shift register after a write to the noise control: the top of 15 bits (s4.4).</summary>
    public const int ShiftRegisterSeed = 0x4000;

    // The amplitude of each attenuation, out of 32767 (via.md s4.5), and the sum of four at full.
    private static readonly int[] Levels = MakeLevels();
    private const int FullMix = 4 * 32767;

    private readonly BbcClock? _clock;
    private readonly SoundBuffer? _buffer;

    // The registers: three periods, four attenuations, the noise control, the latched address.
    private readonly int[] _period = new int[3];
    private readonly int[] _attenuation = new int[4];
    private int _noise;
    private int _latched;

    // The counters (clocks until the next toggle) and flip-flops (0 or 1), the noise's last.
    private readonly int[] _counter = new int[4];
    private readonly int[] _high = new int[4];
    private int _shift;

    // Chip clocks done, and the sample being made: its index, its first and last clocks (it holds
    // the clocks after _sampleStart up to _sampleEnd), and the sum of its clocks' mixes so far.
    private long _clocks;
    private long _sample;
    private long _sampleStart;
    private long _sampleEnd;
    private long _sum;

    /// <summary>
    /// A chip on its own, whose time is the calls to <see cref="Tick"/> and <see cref="Run"/>,
    /// filling <paramref name="buffer"/> if one is given.
    /// </summary>
    public Sn76489(SoundBuffer? buffer = null)
    {
        _buffer = buffer;
        PowerOnState();
        _sampleEnd = buffer is null ? long.MaxValue : SampleEndOf(0);
    }

    /// <summary>The chip in a machine: its time is the machine's clock, and it fills <paramref name="buffer"/>.</summary>
    internal Sn76489(BbcClock clock, SoundBuffer buffer)
        : this(buffer)
    {
        _clock = clock;
        buffer.Filling = Sync;
    }

    /// <summary>The buffer this chip fills, or null.</summary>
    public SoundBuffer? Buffer => _buffer;

    /// <summary>Chip clocks done since power on.</summary>
    public long Clocks
    {
        get
        {
            Sync();
            return _clocks;
        }
    }

    /// <summary>The register the next data byte goes to, 0 to 7: <c>cc t</c> of the last latch byte.</summary>
    public int LatchedRegister => _latched;

    /// <summary>The noise control, 0 to 7: bit 2 set for white noise, bits 1 and 0 the shift rate.</summary>
    public int NoiseControl => _noise;

    /// <summary>The 15-bit noise shift register; bit 0 is the noise channel's output.</summary>
    public int ShiftRegister
    {
        get
        {
            Sync();
            return _shift;
        }
    }

    /// <summary>The mixed output now, 0 to 1: the four channels' levels summed and divided by four.</summary>
    public float Output
    {
        get
        {
            Sync();
            int mix = 0;
            for (int channel = 0; channel < 4; channel++)
            {
                mix += HighNow(channel) ? Levels[_attenuation[channel]] : 0;
            }
            return mix / (float)FullMix;
        }
    }

    /// <summary>The amplitude of attenuation <paramref name="attenuation"/>: 10^(-2n/20), and 0 for 15 (s4.5).</summary>
    public static float Volume(int attenuation)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attenuation, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(attenuation, 15);
        return attenuation == 15 ? 0f : (float)Math.Pow(10, -2.0 * attenuation / 20);
    }

    /// <summary>The amplitude of attenuation <paramref name="attenuation"/> out of 32767, as the mix adds it.</summary>
    public static int Level(int attenuation)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attenuation, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(attenuation, 15);
        return Levels[attenuation];
    }

    /// <summary>Tone channel <paramref name="channel"/>'s 10-bit period, 0 to 2.</summary>
    public int TonePeriod(int channel) => _period[channel];

    /// <summary>Channel <paramref name="channel"/>'s attenuation, 0 (full) to 15 (off); channel 3 is the noise.</summary>
    public int Attenuation(int channel) => _attenuation[channel];

    /// <summary>Whether channel <paramref name="channel"/>'s output is high now: a tone's flip-flop, or the noise's bit 0.</summary>
    public bool ChannelHigh(int channel)
    {
        Sync();
        return HighNow(channel);
    }

    /// <summary>Channel <paramref name="channel"/>'s output now: 0, or its amplitude while high.</summary>
    public float ChannelOutput(int channel) => ChannelHigh(channel) ? Volume(_attenuation[channel]) : 0f;

    /// <summary>The chip's data byte, taken after the clocks owed up to now (s4.2).</summary>
    public void Write(byte value)
    {
        Sync();
        if ((value & 0x80) != 0)
        {
            _latched = (value >> 4) & 7;
            Store(value & 0x0F, highBits: false);
        }
        else
        {
            Store(value & 0x3F, highBits: true);
        }
    }

    /// <summary>One chip clock, for a chip on its own.</summary>
    public void Tick() => Run(1);

    /// <summary>
    /// <paramref name="clocks"/> chip clocks at once, for a chip on its own: what the machine's
    /// chip does when it catches up.
    /// </summary>
    public void Run(long clocks)
    {
        if (_clock is not null)
        {
            throw new InvalidOperationException("A sound chip in a machine runs on the machine's clock.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(clocks);
        Advance(clocks);
    }

    /// <summary>Switching on: the clocks owed are done with the state before, then the power-on state.</summary>
    internal void PowerOn()
    {
        Sync();
        PowerOnState();
    }

    /// <summary>In a machine, does the chip clocks up to the clock's CPU cycle; on its own, nothing.</summary>
    internal void Sync()
    {
        if (_clock is not null)
        {
            Advance((_clock.Cycles / CpuCyclesPerClock) - _clocks);
        }
    }

    private void PowerOnState()
    {
        Array.Clear(_period);
        Array.Fill(_attenuation, 15);
        _noise = 0;
        _latched = 0;
        _counter[0] = _counter[1] = _counter[2] = 1024;
        _counter[3] = 16;
        Array.Clear(_high);
        _shift = ShiftRegisterSeed;
    }

    private void Store(int bits, bool highBits)
    {
        int register = _latched;
        if ((register & 1) == 0 && register < 6)
        {
            int channel = register >> 1;
            _period[channel] = highBits ? (_period[channel] & 0x00F) | (bits << 4) : (_period[channel] & 0x3F0) | bits;
        }
        else if (register == 6)
        {
            _noise = bits & 7;
            _shift = ShiftRegisterSeed;
        }
        else
        {
            _attenuation[register >> 1] = bits & 0x0F;
        }
    }

    private bool HighNow(int channel) => channel == 3 ? (_shift & 1) != 0 : _high[channel] != 0;

    private bool Silent => (_attenuation[0] & _attenuation[1] & _attenuation[2] & _attenuation[3]) == 15;

    private long SampleEndOf(long sample) => (sample + 1) * ClockRate / _buffer!.SampleRate;

    /// <summary>
    /// <paramref name="clocks"/> chip clocks, making every sample that ends in them. A sample is
    /// added to the buffer when its last clock is done.
    /// </summary>
    private void Advance(long clocks)
    {
        if (clocks <= 0)
        {
            return;
        }

        SoundBuffer? buffer = _buffer;
        if (buffer is null)
        {
            RunChannels(clocks, accumulate: false);
            _clocks += clocks;
            return;
        }

        while (true)
        {
            long toEnd = _sampleEnd - _clocks;
            if (clocks < toEnd)
            {
                _sum += RunChannels(clocks, accumulate: !Silent);
                _clocks += clocks;
                return;
            }

            // The sample in hand ends in the span.
            _sum += RunChannels(toEnd, accumulate: !Silent);
            _clocks = _sampleEnd;
            clocks -= toEnd;
            buffer.Add((float)(_sum / ((double)(_sampleEnd - _sampleStart) * FullMix)));
            NextSample(1);
            if (clocks == 0)
            {
                return;
            }

            // Whole samples after it, at once, if they are silent or too many for the buffer.
            bool silent = Silent;
            if (!silent && clocks <= ClockRate)
            {
                continue;
            }

            long whole = ((((_clocks + clocks + 1) * buffer.SampleRate) - 1) / ClockRate) - _sample;
            long skip = silent ? whole : whole - buffer.Capacity;
            if (skip <= 0)
            {
                continue;
            }

            long end = SampleEndOf(_sample + skip - 1);
            RunChannels(end - _clocks, accumulate: false);
            clocks -= end - _clocks;
            _clocks = end;
            NextSample(skip);
            if (silent)
            {
                buffer.AddSilence(skip);
            }
            else
            {
                buffer.Drop(skip);
            }
        }
    }

    private void NextSample(long samples)
    {
        _sample += samples;
        _sampleStart = _clocks;
        _sampleEnd = SampleEndOf(_sample);
        _sum = 0;
    }

    /// <summary>
    /// <paramref name="n"/> clocks of every channel, worked out at once, and, if
    /// <paramref name="accumulate"/>, the sum of the mix over them in levels out of 32767.
    /// </summary>
    private long RunChannels(long n, bool accumulate)
    {
        long sum = 0;
        for (int channel = 0; channel < 3; channel++)
        {
            int level = accumulate ? Levels[_attenuation[channel]] : 0;
            long c = _counter[channel];
            if (n < c)
            {
                _counter[channel] = (int)(c - n);
                sum += _high[channel] * n * level;
                continue;
            }

            long period = PeriodOf(_period[channel]);
            if (level != 0)
            {
                sum += HighClocks(n, c, period, _high[channel]) * level;
            }

            long after = n - c;
            _high[channel] ^= (int)((1 + (after / period)) & 1);
            _counter[channel] = (int)(period - (after % period));
        }

        int noiseLevel = accumulate ? Levels[_attenuation[3]] : 0;
        long count = _counter[3];
        if (n < count)
        {
            _counter[3] = (int)(count - n);
            sum += (_shift & 1) * n * noiseLevel;
            return sum;
        }

        long reload = NoiseReload();
        long rest = n - count;
        long toggles = 1 + (rest / reload);
        int flipFlop = _high[3];

        // The flip-flop toggles at clocks count, count + reload, ...; it rises at every other one,
        // the first if it is low now, and each rise is a shift.
        long shifts = (toggles - flipFlop + 1) / 2;
        if (noiseLevel != 0)
        {
            // The output changes only at a shift, so it is summed a stretch between shifts at a time.
            long high = 0, done = 0;
            long at = count + (flipFlop * reload);
            for (long s = 0; s < shifts; s++, at += 2 * reload)
            {
                high += (_shift & 1) * (at - 1 - done);
                done = at - 1;
                Shift();
            }
            high += (_shift & 1) * (n - done);
            sum += high * noiseLevel;
        }
        else
        {
            // The register's own period: 32767 shifts for white noise, a maximal 15-bit LFSR, and
            // 15 for periodic, a rotation. A write always reseeds it, so it never holds 0.
            for (long s = shifts % ((_noise & 4) != 0 ? 32767 : 15); s > 0; s--)
            {
                Shift();
            }
        }

        _high[3] = flipFlop ^ (int)(toggles & 1);
        _counter[3] = (int)(reload - (rest % reload));
        return sum;
    }

    /// <summary>
    /// The clocks of <paramref name="n"/> on which a flip-flop at <paramref name="high"/> now, which
    /// toggles at clock <paramref name="first"/> (no later than <paramref name="n"/>) and every
    /// <paramref name="period"/> after, is high: <paramref name="high"/> up to the clock before the
    /// first toggle, then stretches of a period alternating, the first of them the other way.
    /// </summary>
    private static long HighClocks(long n, long first, long period, int high)
    {
        long sum = high != 0 ? first - 1 : 0;
        long rest = n - first + 1;
        long whole = rest / period;
        sum += (high == 0 ? (whole + 1) / 2 : whole / 2) * period;
        if (((whole & 1) == 0) == (high == 0))
        {
            sum += rest - (whole * period);
        }
        return sum;
    }

    private static long PeriodOf(int register) => register == 0 ? 1024 : register;

    private long NoiseReload() => (_noise & 3) switch
    {
        0 => 16,
        1 => 32,
        2 => 64,
        _ => PeriodOf(_period[2]),
    };

    private void Shift()
    {
        int feedback = (_noise & 4) != 0 ? (_shift ^ (_shift >> 1)) & 1 : _shift & 1;
        _shift = (_shift >> 1) | (feedback << 14);
    }

    private static int[] MakeLevels()
    {
        var levels = new int[16];
        for (int n = 0; n < 15; n++)
        {
            levels[n] = (int)Math.Round(32767 * Math.Pow(10, -2.0 * n / 20));
        }
        return levels;
    }
}
