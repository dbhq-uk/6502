namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// Keys going down and coming up, as a web page reports them, played into a
/// <see cref="BbcMachine"/>'s keyboard in the order they came and at a pace the OS can read:
/// a key that went down is held for at least <see cref="HoldCycles"/> before it comes up, and a
/// key that came up stays up for at least <see cref="RestCycles"/> before the same key goes down
/// again. Both are machine cycles, not wall-clock time.
/// </summary>
/// <remarks>
/// <para>
/// The OS reads the keyboard on its 100 Hz tick (<c>via.md</c> section 3(a)), so a key that goes
/// down and up between two ticks is never seen. A browser can report both in one frame, before
/// the machine has run a single cycle in between (a test driving the page does, and so can a
/// quick typist on a slow frame), so without this the key would be lost. The times are those
/// <c>BbcSession.Type</c> in the tests uses, 40 ms each: four ticks, well short of the
/// auto-repeat delay.
/// </para>
/// <para>
/// <b>Order is kept.</b> An event waits for every event before it, so SHIFT that went down
/// before a key and up after it is held across the whole of that key's hold, and the machine
/// sees the keys in the order the person pressed them. A different key may go down the moment
/// another comes up, as on a real keyboard when typing quickly; only the same key waits for its
/// rest, so the OS sees it up between two presses.
/// </para>
/// <para>
/// A key held down for longer than the hold is simply held: it comes up when the page says it
/// came up, and the OS's own auto-repeat works as it does on the machine.
/// </para>
/// <para>
/// <b>SHIFT and BREAK.</b> <see cref="ShiftBreak"/> queues what a person does to start a disc:
/// SHIFT down, BREAK pressed and let go, and SHIFT held for <see cref="ShiftBreakHoldCycles"/>
/// more while the machine restarts, so the DFS sees it and runs the disc's <c>!BOOT</c>. The
/// BREAK is an event in the queue like the keys, so it comes after everything queued before it
/// and SHIFT is already in the matrix when the 6502 starts again.
/// </para>
/// </remarks>
public sealed class BbcKeyPresses
{
    /// <summary>The least a key is held down: 40 ms at 2 MHz, as <c>BbcSession.HoldCycles</c>.</summary>
    public const long HoldCycles = 80_000;

    /// <summary>The least a key stays up before it goes down again: 40 ms at 2 MHz, as <c>BbcSession.RestCycles</c>.</summary>
    public const long RestCycles = 80_000;

    /// <summary>
    /// How long <see cref="ShiftBreak"/> holds SHIFT after the BREAK: half a second at 2 MHz. The
    /// OS reads SHIFT once, as it starts again after the reset, well inside this; and it is short
    /// enough that a program the disc starts does not see SHIFT held for long. The preset discs
    /// test (<c>DiscLibraryTests</c>) starts every bundled disc with it.
    /// </summary>
    public const long ShiftBreakHoldCycles = 1_000_000;

    private readonly BbcMachine _machine;
    private readonly Queue<Event> _pending = new();

    // The cycle each key last went down and last came up, by its internal number (0 to $79), and
    // the least time its present press is held.
    private readonly long[] _downAt = new long[0x80];
    private readonly long[] _upAt = new long[0x80];
    private readonly long[] _holdFor = new long[0x80];

    private enum Kind
    {
        Down,
        Up,
        Break,
    }

    private readonly record struct Event(BbcKey Key, Kind Kind, long Hold);

    public BbcKeyPresses(BbcMachine machine)
    {
        _machine = machine;
        Array.Fill(_upAt, long.MinValue / 2);
        Array.Fill(_downAt, long.MinValue / 2);
    }

    /// <summary>Events not yet played into the keyboard.</summary>
    public int Pending => _pending.Count;

    /// <summary>Queues <paramref name="key"/> going down.</summary>
    public void Down(BbcKey key) => Enqueue(key, Kind.Down, HoldCycles);

    /// <summary>Queues <paramref name="key"/> coming up.</summary>
    public void Up(BbcKey key) => Enqueue(key, Kind.Up, 0);

    /// <summary>
    /// Queues SHIFT and BREAK, the way a disc is started: SHIFT goes down, the machine is reset as
    /// by its BREAK key (<see cref="BbcMachine.PressBreak"/>) on the same cycle, and SHIFT comes up
    /// <see cref="ShiftBreakHoldCycles"/> later. With a disc in drive 0 whose boot option is set,
    /// the DFS then runs its <c>!BOOT</c>.
    /// </summary>
    public void ShiftBreak()
    {
        Enqueue(BbcKey.Shift, Kind.Down, ShiftBreakHoldCycles);
        Enqueue(BbcKey.Shift, Kind.Break, 0);
        Enqueue(BbcKey.Shift, Kind.Up, 0);
    }

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

    private void Enqueue(BbcKey key, Kind kind, long hold)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key in the matrix.");
        }

        _pending.Enqueue(new Event(key, kind, hold));
    }

    // The cycle an event may be played at: an up once its key has been held, a down once its key
    // has rested, and a BREAK as soon as everything before it has been played.
    private long Due(Event e) => e.Kind switch
    {
        Kind.Down => _upAt[(int)e.Key] + RestCycles,
        Kind.Up => _downAt[(int)e.Key] + _holdFor[(int)e.Key],
        _ => long.MinValue,
    };

    private void Play()
    {
        long now = _machine.Cycles;
        while (_pending.Count > 0 && Due(_pending.Peek()) <= now)
        {
            Event e = _pending.Dequeue();
            switch (e.Kind)
            {
                case Kind.Down:
                    _machine.Keyboard.Press(e.Key);
                    _downAt[(int)e.Key] = now;
                    _holdFor[(int)e.Key] = e.Hold;
                    break;
                case Kind.Up:
                    _machine.Keyboard.Release(e.Key);
                    _upAt[(int)e.Key] = now;
                    break;
                default:
                    _machine.PressBreak();
                    break;
            }
        }
    }
}
