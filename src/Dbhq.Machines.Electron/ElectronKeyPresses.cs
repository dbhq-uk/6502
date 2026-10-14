namespace Dbhq.Machines.Electron;

/// <summary>
/// Keys going down and coming up, as a web page reports them, played into an
/// <see cref="ElectronMachine"/>'s keyboard in the order they came and at a pace the OS can read:
/// a key that went down is held for at least <see cref="HoldCycles"/> before it comes up, and a
/// key that came up stays up for at least <see cref="RestCycles"/> before the same key goes down
/// again. Both are machine cycles, not wall-clock time.
/// </summary>
/// <remarks>
/// <para>
/// <b>A copy of the BBC Micro's <c>BbcKeyPresses</c>, adapted, on purpose.</b> The two are not
/// made one class yet: two machines show what they share, and a third will show where the line
/// between the shared part and each machine's own falls. What changed is the key type, the
/// Electron's <see cref="ElectronKey"/>, and that there is no SHIFT and BREAK: the BBC's starts a
/// disc, and the Electron has none.
/// </para>
/// <para>
/// The OS reads the keyboard on its interrupts, 100 times a second (fact sheet <c>ula.md</c> s7a,
/// s6c), so a key that goes down and up between two reads is never seen. A browser can report both
/// in one frame, before the machine has run a single cycle in between (a test driving the page
/// does, and so can a quick typist on a slow frame), so without this the key would be lost. The
/// times are those the tests' <c>ElectronSession.Type</c> uses, 40 ms each: a frame of 80,000
/// cycles (s5d), four of the OS's reads.
/// </para>
/// <para>
/// <b>Order is kept.</b> An event waits for every event before it, so Shift that went down before
/// a key and up after it is held across the whole of that key's hold, and the machine sees the
/// keys in the order the person pressed them. A different key may go down the moment another
/// comes up, as on a real keyboard when typing quickly; only the same key waits for its rest, so
/// the OS sees it up between two presses.
/// </para>
/// <para>
/// A key held down for longer than the hold is simply held: it comes up when the page says it
/// came up, and the OS's own auto-repeat works as it does on the machine.
/// </para>
/// </remarks>
public sealed class ElectronKeyPresses
{
    /// <summary>The least a key is held down: 40 ms at 2 MHz, as <c>ElectronSession.HoldCycles</c>.</summary>
    public const long HoldCycles = 80_000;

    /// <summary>The least a key stays up before it goes down again: 40 ms at 2 MHz, as <c>ElectronSession.RestCycles</c>.</summary>
    public const long RestCycles = 80_000;

    private readonly ElectronMachine _machine;
    private readonly Queue<Event> _pending = new();

    // The cycle each key last went down and last came up, by its value (column plus sixteen times
    // its bit, $00 to $3D).
    private readonly long[] _downAt = new long[0x40];
    private readonly long[] _upAt = new long[0x40];

    private readonly record struct Event(ElectronKey Key, bool Down);

    public ElectronKeyPresses(ElectronMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        _machine = machine;
        Array.Fill(_upAt, long.MinValue / 2);
        Array.Fill(_downAt, long.MinValue / 2);
    }

    /// <summary>Events not yet played into the keyboard.</summary>
    public int Pending => _pending.Count;

    /// <summary>Queues <paramref name="key"/> going down.</summary>
    public void Down(ElectronKey key) => Enqueue(key, down: true);

    /// <summary>Queues <paramref name="key"/> coming up.</summary>
    public void Up(ElectronKey key) => Enqueue(key, down: false);

    /// <summary>
    /// Runs the machine for at least <paramref name="cycles"/> more CPU cycles, playing each
    /// queued event on the first instruction boundary at or after the cycle it falls due.
    /// </summary>
    public void Run(long cycles)
    {
        long end = _machine.Cycles + cycles;
        while (true)
        {
            Play();
            long now = _machine.Cycles;
            if (now >= end)
            {
                return;
            }

            long next = _pending.Count > 0 ? Math.Min(end, Due(_pending.Peek())) : end;
            _machine.Run(Math.Max(1, next - now));
        }
    }

    private void Enqueue(ElectronKey key, bool down)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key in the matrix.");
        }

        _pending.Enqueue(new Event(key, down));
    }

    // The cycle an event may be played at: a down once its key has rested, an up once it has been held.
    private long Due(Event e) => e.Down ? _upAt[(int)e.Key] + RestCycles : _downAt[(int)e.Key] + HoldCycles;

    private void Play()
    {
        long now = _machine.Cycles;
        while (_pending.Count > 0 && Due(_pending.Peek()) <= now)
        {
            Event e = _pending.Dequeue();
            if (e.Down)
            {
                _machine.Keyboard.Down(e.Key);
                _downAt[(int)e.Key] = now;
            }
            else
            {
                _machine.Keyboard.Up(e.Key);
                _upAt[(int)e.Key] = now;
            }
        }
    }
}
