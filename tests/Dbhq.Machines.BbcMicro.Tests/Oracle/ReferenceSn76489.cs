namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The SN76489 the plain way, a chip clock at a time, as the oracle for the machine's lazy chip,
/// which works out spans of clocks at once. Written from <c>via.md</c> section 4 and the model the
/// machine's chip states, not from its code: three tone counters and a noise counter that count
/// down and toggle a flip-flop at zero, a 15-bit shift register that shifts when the noise
/// flip-flop rises, and the samples, each the mean of its clocks' mixed level.
/// </summary>
/// <remarks>
/// The model, as both chips keep it. A tone's counter reloads with its period at zero, a period of
/// 0 counting as 1024. The noise counter reloads with 16, 32 or 64, or tone 3's period for rate 3.
/// After each clock the mix is the sum, over the four channels whose flip-flop (for the noise, the
/// shift register's bit 0) is high, of the attenuation's level out of 32767. Sample j is the clocks
/// after floor(j R / S) up to floor((j + 1) R / S), R being 250,000 and S the sample rate, and its
/// value is the sum of their mixes over (clocks times 4 times 32767). Power on: every attenuation
/// 15, every period and the noise control 0, the counters at 1024 and 16, the flip-flops low.
/// </remarks>
internal sealed class ReferenceSn76489
{
    // via.md s4.5, the x 32767 column.
    private static readonly int[] Levels =
        [32767, 26028, 20675, 16422, 13045, 10362, 8231, 6538, 5193, 4125, 3277, 2603, 2067, 1642, 1304, 0];

    private readonly int[] _period = new int[3];
    private readonly int[] _attenuation = [15, 15, 15, 15];
    private readonly int[] _counter = [1024, 1024, 1024, 16];
    private readonly bool[] _high = new bool[4];
    private int _noise;
    private int _latched;
    private int _shift = 0x4000;

    private readonly int _rate;
    private long _sample;
    private long _start;
    private long _end;
    private long _sum;

    public ReferenceSn76489(int sampleRate)
    {
        _rate = sampleRate;
        _end = Sn76489.ClockRate / _rate;
    }

    public long Clocks { get; private set; }

    public List<float> Samples { get; } = [];

    public int ShiftRegister => _shift;

    /// <summary>Shifts of the noise register since power on.</summary>
    public long Shifts { get; private set; }

    public int TonePeriod(int channel) => _period[channel];

    public int Attenuation(int channel) => _attenuation[channel];

    public bool ChannelHigh(int channel) => channel == 3 ? (_shift & 1) != 0 : _high[channel];

    public void Write(byte value)
    {
        if ((value & 0x80) != 0)
        {
            _latched = (value >> 4) & 7;
            switch (_latched)
            {
                case 0 or 2 or 4:
                    _period[_latched / 2] = (_period[_latched / 2] & 0x3F0) | (value & 0x0F);
                    break;
                case 6:
                    _noise = value & 7;
                    _shift = 0x4000;
                    break;
                default:
                    _attenuation[_latched / 2] = value & 0x0F;
                    break;
            }
            return;
        }

        switch (_latched)
        {
            case 0 or 2 or 4:
                _period[_latched / 2] = (_period[_latched / 2] & 0x00F) | ((value & 0x3F) << 4);
                break;
            case 6:
                _noise = value & 7;
                _shift = 0x4000;
                break;
            default:
                _attenuation[_latched / 2] = value & 0x0F;
                break;
        }
    }

    public void Run(long clocks)
    {
        for (long i = 0; i < clocks; i++)
        {
            Tick();
        }
    }

    public void Tick()
    {
        for (int channel = 0; channel < 3; channel++)
        {
            if (--_counter[channel] == 0)
            {
                _counter[channel] = _period[channel] == 0 ? 1024 : _period[channel];
                _high[channel] = !_high[channel];
            }
        }

        if (--_counter[3] == 0)
        {
            _counter[3] = (_noise & 3) switch
            {
                0 => 16,
                1 => 32,
                2 => 64,
                _ => _period[2] == 0 ? 1024 : _period[2],
            };
            _high[3] = !_high[3];
            if (_high[3])
            {
                int feedback = (_noise & 4) != 0 ? (_shift ^ (_shift >> 1)) & 1 : _shift & 1;
                _shift = (_shift >> 1) | (feedback << 14);
                Shifts++;
            }
        }

        Clocks++;
        for (int channel = 0; channel < 4; channel++)
        {
            if (ChannelHigh(channel))
            {
                _sum += Levels[_attenuation[channel]];
            }
        }

        if (Clocks == _end)
        {
            Samples.Add((float)(_sum / ((double)(_end - _start) * (4 * 32767))));
            _sum = 0;
            _sample++;
            _start = _end;
            _end = (_sample + 1) * Sn76489.ClockRate / _rate;
        }
    }
}
