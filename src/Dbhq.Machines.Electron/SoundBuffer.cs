namespace Dbhq.Machines.Electron;

/// <summary>
/// The sound the machine has made, as float samples at a fixed rate, waiting for the page to
/// play them: a ring that holds the newest second.
/// </summary>
/// <remarks>
/// <para>
/// The samples are made lazily, as the BBC's are: <see cref="UlaSound"/> works out the samples it
/// owes when it is written to and when this buffer is read or counted, so a page that reads it
/// every frame gets every sample up to the cycle the machine has reached, and a buffer nobody
/// reads costs nothing between writes.
/// </para>
/// <para>
/// <b>When it is full</b> the oldest sample goes to make room and <see cref="Overruns"/> counts
/// it. A page that drains every frame never fills it; a page with its sound off can leave it to
/// fill, and then reads the last second. Running short is the page's to notice.
/// </para>
/// </remarks>
public sealed class SoundBuffer
{
    /// <summary>The most samples a second a buffer is made for: 384 kHz is well above any page's audio.</summary>
    public const int MaxSampleRate = 384_000;

    private readonly float[] _ring;
    private int _head;
    private int _count;
    private long _overruns;

    /// <param name="sampleRate">Samples a second, 1 to <see cref="MaxSampleRate"/>.</param>
    public SoundBuffer(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, MaxSampleRate);
        SampleRate = sampleRate;
        _ring = new float[sampleRate];
    }

    /// <summary>Samples a second.</summary>
    public int SampleRate { get; }

    /// <summary>How many samples the buffer holds before it drops the oldest: one second's.</summary>
    public int Capacity => _ring.Length;

    /// <summary>The samples waiting, brought up to now first.</summary>
    public int Count
    {
        get
        {
            Filling?.Invoke();
            return _count;
        }
    }

    /// <summary>Samples dropped, oldest first, because the buffer was full, brought up to now first.</summary>
    public long Overruns
    {
        get
        {
            Filling?.Invoke();
            return _overruns;
        }
    }

    /// <summary>The sound that fills this buffer, asked to catch up before the buffer is read; null for a buffer on its own.</summary>
    internal Action? Filling { get; set; }

    /// <summary>Adds one sample, dropping the oldest if the buffer is full.</summary>
    public void Add(float sample)
    {
        if (_count == _ring.Length)
        {
            _head = _head + 1 == _ring.Length ? 0 : _head + 1;
            _count--;
            _overruns++;
        }

        int tail = _head + _count;
        _ring[tail >= _ring.Length ? tail - _ring.Length : tail] = sample;
        _count++;
    }

    /// <summary>
    /// Copies the oldest samples into <paramref name="destination"/>, as many as fit, after the
    /// sound has made every sample up to now, and returns how many it copied.
    /// </summary>
    public int Read(Span<float> destination)
    {
        Filling?.Invoke();
        int n = Math.Min(destination.Length, _count);
        int first = Math.Min(n, _ring.Length - _head);
        _ring.AsSpan(_head, first).CopyTo(destination);
        _ring.AsSpan(0, n - first).CopyTo(destination[first..]);
        _head = (_head + n) % _ring.Length;
        _count -= n;
        return n;
    }

    /// <summary>Adds <paramref name="samples"/> silent samples at once, with the same rule when full.</summary>
    internal void AddSilence(long samples)
    {
        if (samples >= _ring.Length)
        {
            _overruns += _count + samples - _ring.Length;
            Array.Clear(_ring);
            _head = 0;
            _count = _ring.Length;
            return;
        }

        int n = (int)samples;
        int drop = Math.Max(0, _count + n - _ring.Length);
        if (drop > 0)
        {
            _head = (_head + drop) % _ring.Length;
            _count -= drop;
            _overruns += drop;
        }

        int tail = (_head + _count) % _ring.Length;
        int first = Math.Min(n, _ring.Length - tail);
        _ring.AsSpan(tail, first).Clear();
        _ring.AsSpan(0, n - first).Clear();
        _count += n;
    }
}
