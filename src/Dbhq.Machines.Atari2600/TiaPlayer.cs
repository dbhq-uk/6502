namespace Dbhq.Machines.Atari2600;

/// <summary>
/// A player: its position counter, its graphics scan counter, and its "new" and "old" graphics
/// registers (<c>docs/atari-2600/facts/tia.md</c> sections 9 and 10).
/// </summary>
/// <remarks>
/// <para>
/// <b>The scan counter</b> is a three-bit counter that picks which of the eight bits is drawn. A
/// start from the position counter sets it to 0, and it then counts to 7 and stops; it is never
/// reset otherwise, so a player that runs off the right edge finishes after the next line's blank
/// (Towers: Player Graphics Scan Counters). It is clocked by MOTCK through a gate NUSIZ sets: every
/// clock at single width; at double width the clocks of the two-phase clock's quarters 1 and 3; at
/// quad width its quarter 1 only. The start is taken on the first clock the gate lets through, so a
/// single-width player's first pixel is one clock after the start signal and a stretched one's two,
/// which is section 9's "one clock later still" for double and quad width; a NUSIZ write takes
/// effect on the next clock, mid-player too (section 10). A start that arrives while the scan is
/// running restarts it at 0; no source says [guessing - verify]. The quarters chosen for the gates are
/// the ones that give those first pixels and widths [inferring from Towers, who names the phases of
/// the two-phase clock without their order; guessing - verify].
/// </para>
/// <para>
/// <b>The bits.</b> D7 is drawn first, D0 first with REFPx D3 set; the reflect bit and VDELPx are
/// read at every pixel. A write to GRPx puts the new register; GRP0's write copies new GRP1 to old
/// GRP1, and GRP1's copies new GRP0 to old GRP0. The registers are drawn as they stood a colour
/// clock earlier, the one clock section 4 reads from the suite's <c>midline-grp</c> picture, which
/// is recorded and not trusted [guessing - verify].
/// </para>
/// <para>
/// <b>RESMP</b> (section 11) is driven from here: while RESMPx D1 is set, the missile's counter is
/// reset as this player's main copy is drawn. Towers says it happens as the scan counter reaches
/// <c>%100</c>, and the SPG calls the place the player's centre; the suite measures the missile at
/// 4, 6 and 10 pixels right of a single, double and quad player's first pixel, and 2k6specs gives
/// 6 and 10 too. A reset at <c>%100</c> with any one delay gives 4, 8 and 16. The one scan count
/// whose time, with one delay, gives all three is <c>%010</c>: the missile lands two clocks after
/// the player's third bit starts (2, 4 or 8 pixels in, by width). So the model resets it
/// there [inferring from the suite's three measurements; it disagrees with Towers's bit pattern;
/// guessing - verify]. Task 7's <c>missile-reset-lock</c> checks it.
/// </para>
/// </remarks>
public sealed class TiaPlayer : IReportsState
{
    private readonly TiaPositionCounter _counter = new();

    // The graphics registers as written, and as the scan sees them, a colour clock later.
    private byte _new;
    private byte _old;
    private byte _seenNew;
    private byte _seenOld;

    // The scan counter: drawing, and the bit it is at, 0 to 7; and whether the copy is the main one.
    private bool _scanning;
    private int _scan;
    private bool _scanMain;

    internal TiaPlayer()
    {
    }

    /// <summary>The position counter, for the tests and task 7's motion.</summary>
    internal TiaPositionCounter Counter => _counter;

    /// <summary>A RESPx strobe has landed (tia.md section 9): no start of its own.</summary>
    internal void Reset() => _counter.Reset(sendsStart: false);

    /// <summary>A write to this player's GRPx.</summary>
    internal void WriteGraphics(byte value) => _new = value;

    /// <summary>The other player's GRP write: the new register is copied to the old one.</summary>
    internal void CopyNewToOld() => _old = _new;

    /// <summary>
    /// The player's serial output for this colour clock: the bit the scan counter is at, of the
    /// register VDELPx picks, in the order REFPx picks.
    /// </summary>
    internal bool Output(byte refp, byte vdelp)
    {
        if (!_scanning)
        {
            return false;
        }

        byte graphics = (vdelp & 0x01) != 0 ? _seenOld : _seenNew;
        int bit = (refp & 0x08) != 0 ? _scan : 7 - _scan;
        return ((graphics >> bit) & 1) != 0;
    }

    /// <summary>
    /// A clock of the player: the scan counter moves if its gate lets it through, and the position
    /// counter is clocked. <paramref name="motionClock"/> is MOTCK, or an extra clock from HMOVE
    /// (<see cref="TiaMotion"/>), which the TIA takes before the pixel and the motion clock at the
    /// end of a colour clock. Returns true when
    /// the scan counter has just reached <c>%010</c> in the main copy, where RESMP resets the
    /// missile (see the remarks).
    /// </summary>
    internal bool Clock(bool motionClock, byte nusiz)
    {
        bool lockPoint = false;
        if (motionClock && Gate(nusiz & 0x07, _counter.Phase))
        {
            if (_counter.TakeStart())
            {
                // A new start restarts a running scan [guessing - verify].
                _scanning = true;
                _scan = 0;
                _scanMain = _counter.StartMain;
            }
            else if (_scanning)
            {
                if (++_scan == 8)
                {
                    _scanning = false;
                }
                else if (_scan == 2 && _scanMain)
                {
                    lockPoint = true;
                }
            }
        }

        _counter.Clock(motionClock, TiaPositionCounter.Copies(nusiz));
        return lockPoint;
    }

    /// <summary>The end of a colour clock: the graphics registers are seen as they now stand.</summary>
    internal void SeeRegisters()
    {
        _seenNew = _new;
        _seenOld = _old;
    }

    internal void PowerOn()
    {
        _counter.PowerOn();
        _new = 0;
        _old = 0;
        _seenNew = 0;
        _seenOld = 0;
        _scanning = false;
        _scan = 0;
        _scanMain = false;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_counter), _counter);
        sink.Add(nameof(_new), _new);
        sink.Add(nameof(_old), _old);
        sink.Add(nameof(_seenNew), _seenNew);
        sink.Add(nameof(_seenOld), _seenOld);
        sink.Add(nameof(_scanning), _scanning);
        sink.Add(nameof(_scan), _scan);
        sink.Add(nameof(_scanMain), _scanMain);
    }

    // The scan counter's clock gate by NUSIZ D2 to D0: double width (%101) on quarters 1 and 3 of
    // the two-phase clock, quad width (%111) on quarter 1, every clock otherwise.
    private static bool Gate(int size, int phase) => size switch
    {
        0b101 => (phase & 1) != 0,
        0b111 => phase == 1,
        _ => true,
    };
}
