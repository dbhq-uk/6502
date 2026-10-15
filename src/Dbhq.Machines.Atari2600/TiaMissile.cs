namespace Dbhq.Machines.Atari2600;

/// <summary>
/// A missile: its position counter, with the player's copy decodes and without the player's extra
/// clock, and its width (<c>docs/atari-2600/facts/tia.md</c> section 11).
/// </summary>
/// <remarks>
/// <para>
/// <b>Copies.</b> NUSIZx D2 to D0 copy the missile as they copy its player, and the double and
/// quad settings give one copy, not stretched (section 11; Towers: Missile Position Counters).
/// </para>
/// <para>
/// <b>Width</b>, NUSIZx D5 and D4, is the ball's circuit (section 12; Towers: the width
/// "enclockifier"): the start signal is active for the four clocks of a count; width 1 lets through
/// its first quarter of the two-phase clock, width 2 its first two, width 4 all of it, and width 8
/// adds the start signal of the count before, so the four clocks after as well.
/// </para>
/// <para>
/// <b>RESMP.</b> While RESMPx D1 is set the missile puts out nothing, so it is neither drawn nor
/// collides, and its counter is reset where its player's main copy says (<see cref="TiaPlayer"/>).
/// Once D1 is clear it draws from where that reset left it.
/// </para>
/// </remarks>
public sealed class TiaMissile : IReportsState
{
    private readonly TiaPositionCounter _counter = new();

    internal TiaMissile()
    {
    }

    /// <summary>The position counter, for the tests and task 7's motion.</summary>
    internal TiaPositionCounter Counter => _counter;

    /// <summary>A RESMx strobe has landed (tia.md section 9): no start of its own.</summary>
    internal void Reset() => _counter.Reset(sendsStart: false);

    /// <summary>
    /// RESMP's reset, at the player's lock point (<see cref="TiaPlayer.Clock"/>): the counter is put
    /// two clocks before the wrap of count 39 into count 0, a quarter earlier than the clock it is
    /// about to run, so the missile's main copy starts two clocks after the lock point.
    /// </summary>
    internal void LockToPlayer() => _counter.Plant(TiaPositionCounter.Counts - 1, 1);

    /// <summary>
    /// The missile's serial output for this colour clock, by ENAMx D1, RESMPx D1 and NUSIZx D5 and
    /// D4 (sections 11 and 12).
    /// </summary>
    internal bool Output(byte enam, byte resmp, byte nusiz)
    {
        if ((enam & 0x02) == 0 || (resmp & 0x02) != 0)
        {
            return false;
        }

        return Width(_counter, nusiz >> 4);
    }

    /// <summary>The end of a colour clock: the counter is clocked, with the copies NUSIZx asks for.</summary>
    internal void Clock(bool motionClock, byte nusiz) => _counter.Clock(motionClock, TiaPositionCounter.Copies(nusiz));

    /// <summary>
    /// The width circuit the missiles and the ball share (tia.md section 12): from the start signal
    /// and the two-phase clock's quarter, by the two size bits (0 to 3, widths 1, 2, 4 and 8).
    /// </summary>
    internal static bool Width(TiaPositionCounter counter, int size) => (size & 0x03) switch
    {
        0 => counter.Start && counter.Phase == 0,
        1 => counter.Start && counter.Phase < 2,
        2 => counter.Start,
        _ => counter.Start || counter.StartBefore,
    };

    internal void PowerOn() => _counter.PowerOn();

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink) => sink.Add(nameof(_counter), _counter);
}
