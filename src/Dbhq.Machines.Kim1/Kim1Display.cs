namespace Dbhq.Machines.Kim1;

/// <summary>
/// The six seven-segment digits. Only one is lit at a time: the monitor
/// selects a digit through outputs 4 to 9 of the 74145 decoder and drives
/// its segments on PA0 to PA6, then moves on. The eye sees each digit as
/// what it showed for most of the time it was selected.
/// </summary>
/// <remarks>
/// This is a model of persistence of vision, not of the LEDs. During one
/// selection, each segment is counted lit when it was on for more than half
/// of the cycles the digit was selected; a digit not selected for
/// <see cref="Persistence"/> cycles is dark. The monitor's own scan blanks the
/// segments for a few cycles at each change of digit, which this rule
/// ignores, as the eye does.
/// </remarks>
public sealed class Kim1Display
{
    /// <summary>How long a digit stays lit after it was last scanned: 20 ms at 1 MHz.</summary>
    public const long Persistence = 20_000;

    public const int Digits = 6;

    private readonly long[,] _on = new long[Digits, 7];
    private readonly long[] _selected = new long[Digits];
    private readonly byte[] _shown = new byte[Digits];
    private readonly long[] _lastLit = [long.MinValue, long.MinValue, long.MinValue, long.MinValue, long.MinValue, long.MinValue];
    private int _digit = -1;
    private byte _segments;
    private long _since;

    /// <summary>
    /// Records the lines at <paramref name="cycle"/>: the decoder output
    /// selected and the levels on PA0 to PA6. Called whenever either changes.
    /// </summary>
    public void Observe(long cycle, int decoderOutput, byte segments)
    {
        Accumulate(cycle);
        int digit = decoderOutput is >= 4 and <= 9 ? decoderOutput - 4 : -1;
        if (digit != _digit && _digit >= 0)
        {
            Finish(_digit, cycle);
        }

        _digit = digit;
        _segments = (byte)(segments & 0x7F);
        _since = cycle;
    }

    /// <summary>The segments digit <paramref name="digit"/> shows at <paramref name="now"/>, bit 0 for a to bit 6 for g. Zero when dark.</summary>
    public byte Segments(int digit, long now) =>
        now - _lastLit[digit] <= Persistence ? _shown[digit] : (byte)0;

    /// <summary>
    /// The six digits as text: a hexadecimal digit where the segments make
    /// one, a space where the digit is dark, and '?' for any other pattern.
    /// </summary>
    public string Read(long now)
    {
        var text = new char[Digits];
        for (int i = 0; i < Digits; i++)
        {
            text[i] = Decode(Segments(i, now));
        }

        return new string(text);
    }

    /// <summary>The usual seven-segment shapes, segment a in bit 0 to segment g in bit 6.</summary>
    public static char Decode(byte segments) => segments switch
    {
        0x00 => ' ',
        0x3F => '0',
        0x06 => '1',
        0x5B => '2',
        0x4F => '3',
        0x66 => '4',
        0x6D => '5',
        0x7D => '6',
        0x07 => '7',
        0x7F => '8',
        0x6F => '9',
        0x77 => 'A',
        0x7C => 'B',
        0x39 => 'C',
        0x5E => 'D',
        0x79 => 'E',
        0x71 => 'F',
        _ => '?',
    };

    private void Accumulate(long cycle)
    {
        if (_digit < 0)
        {
            return;
        }

        long span = cycle - _since;
        _selected[_digit] += span;
        for (int segment = 0; segment < 7; segment++)
        {
            if ((_segments & (1 << segment)) != 0)
            {
                _on[_digit, segment] += span;
            }
        }
    }

    private void Finish(int digit, long cycle)
    {
        byte shown = 0;
        for (int segment = 0; segment < 7; segment++)
        {
            if (_on[digit, segment] * 2 > _selected[digit])
            {
                shown |= (byte)(1 << segment);
            }

            _on[digit, segment] = 0;
        }

        if (_selected[digit] > 0)
        {
            _shown[digit] = shown;
            _lastLit[digit] = cycle;
        }

        _selected[digit] = 0;
    }
}
