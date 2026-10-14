namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The ball: a position counter with only the main copy's decode, its width, and its "new" and
/// "old" enable bits (<c>docs/atari-2600/facts/tia.md</c> section 12).
/// </summary>
/// <remarks>
/// <para>
/// <b>RESBL sends a start</b>, unlike the other resets, so the ball draws at its new place on the
/// line it is strobed, four clocks after the write, and can be strobed many times a line (Towers:
/// Ball Horizontal Position Counter).
/// </para>
/// <para>
/// <b>Width</b> is CTRLPF D5 and D4, by the circuit the missiles share
/// (<see cref="TiaMissile.Width"/>). <b>Vertical delay:</b> ENABL writes the new enable bit, a GRP1
/// write copies it to the old one, and VDELBL D0 draws from the old one.
/// </para>
/// </remarks>
public sealed class TiaBall : IReportsState
{
    private readonly TiaPositionCounter _counter = new();

    // ENABL D1 as written, and as the last GRP1 write copied it.
    private bool _new;
    private bool _old;

    internal TiaBall()
    {
    }

    /// <summary>The position counter, for the tests and task 7's motion.</summary>
    internal TiaPositionCounter Counter => _counter;

    /// <summary>A RESBL strobe has landed: the counter restarts and a start is sent (section 12).</summary>
    internal void Reset() => _counter.Reset(sendsStart: true);

    /// <summary>A write to ENABL: the new enable bit, D1.</summary>
    internal void WriteEnable(byte value) => _new = (value & 0x02) != 0;

    /// <summary>A GRP1 write: the new enable bit is copied to the old one.</summary>
    internal void CopyNewToOld() => _old = _new;

    /// <summary>The ball's serial output for this colour clock, by VDELBL D0 and CTRLPF D5 and D4.</summary>
    internal bool Output(byte vdelbl, byte ctrlpf)
    {
        bool enabled = (vdelbl & 0x01) != 0 ? _old : _new;
        return enabled && TiaMissile.Width(_counter, ctrlpf >> 4);
    }

    /// <summary>The end of a colour clock: the counter is clocked; the ball has no copies.</summary>
    internal void Clock(bool motionClock) => _counter.Clock(motionClock, copies: 0);

    internal void PowerOn()
    {
        _counter.PowerOn();
        _new = false;
        _old = false;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_counter), _counter);
        sink.Add(nameof(_new), _new);
        sink.Add(nameof(_old), _old);
    }
}
