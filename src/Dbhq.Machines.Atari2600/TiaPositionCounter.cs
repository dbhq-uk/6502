namespace Dbhq.Machines.Atari2600;

/// <summary>
/// One movable object's position counter, as the TIA has five of them: a counter of 40 counts, each
/// four colour clocks long, stepped by its own two-phase clock, which the motion clock (MOTCK)
/// drives while the horizontal blank is off; the decodes that start each copy; the start signal
/// they make; and the reset strobe that restarts it. <see cref="TiaPlayer"/>, <see cref="TiaMissile"/>
/// and <see cref="TiaBall"/> each own one and draw from its start signal.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/tia.md</c> sections 9, 11 and 12, and Towers's notes on the
/// position counters. <b>The count</b> is the counter's place in its sequence, 0 to 39: the chip's
/// counter is a six-bit polynomial one, like the horizontal sync counter, but only where it is in
/// its sequence is ever decoded, so the place is kept and not the bits. <b>The phase</b> is the
/// two-phase clock's quarter, 0 to 3; the count steps as the phase wraps, every four MOTCK clocks.
/// A count lasts four clocks, and those four clocks are where a start signal is active.
/// </para>
/// <para>
/// <b>The start decodes</b> (section 9): count 39 starts the main copy, as the counter wraps to 0;
/// counts 3, 7 and 15 start the close, medium and far copies, when NUSIZ asks for them (section
/// 10). A decode does not act at once: it is held until the two-phase clock next completes a
/// cycle, the boundary into the next count, and only there becomes the start signal, active for
/// that count's four clocks (Towers: the decodes "pass through a block of logic that requires a
/// full cycle of the two-phase clock"). NUSIZ is read at the boundary, as the decode is "ANDed with
/// flags from the NUSIZ register before being latched" (Towers).
/// </para>
/// <para>
/// <b>The reset</b> (section 9). A RESPx, RESMx or RESBL write holds the counter for four colour
/// clocks and then starts it at count 0, phase 0, which is itself a boundary of the two-phase
/// clock. The first two of the four are colour clocks, which run in the blank too; the last two
/// are MOTCK clocks, which wait for the blank to end. That split is the one that gives both arms
/// of section 9's landing rule from one mechanism: a missile reset written at W on the drawn line
/// starts its count at W + 4, and one written in the blank at colour clock 70, two clocks after
/// the blank ends; with the main copy's start one count before the wrap, the object's first pixel
/// is where the count restarts, on the next line [inferring from section 9's table, which it fits
/// at every row but one; guessing - verify]. The one row it does not fit is a player reset landing
/// at colour clock 69, one clock after the blank ends, which the suite measured at pixel 7 where
/// this gives 6 (tia.md open item 7); task 7's <c>merge-delivery-replant</c> checks it.
/// </para>
/// <para>
/// <b>A decode held through a reset</b> is delivered at the reset's end, so a copy whose decode
/// was made before the strobe still draws, moved to start where the reset ends: the "ride-through"
/// of the suite's <c>resp-restrobe</c>, and Towers's start "lost or delayed up to 3 CLK". A reset
/// that lands before the decode is made stops the counter short of it, and the copy is lost.
/// </para>
/// </remarks>
internal sealed class TiaPositionCounter : IReportsState
{
    /// <summary>Counts in the counter's sequence (tia.md section 9).</summary>
    internal const int Counts = 40;

    // What a held decode will start (tia.md sections 9 and 10).
    private const int None = 0;
    private const int Main = 1;
    private const int Close = 2;
    private const int Medium = 3;
    private const int Far = 4;

    // The clocks a reset holds the counter: the first two colour clocks, the last two MOTCK clocks.
    private const int ResetClocks = 4;
    private const int ResetMotionClocks = 2;

    // The counter's place in its sequence, 0 to 39, and the two-phase clock's quarter, 0 to 3.
    private int _count;
    private int _phase;

    // The clocks of a reset still to run, 4 to 0.
    private int _reset;

    // A decode held for the next boundary of the two-phase clock: None, Main, Close, Medium or Far.
    private int _held;

    // The start signal, active for the four clocks of the count after a boundary that delivered a
    // decode, and the same signal a count later (the ball's and missile's width 8, section 12).
    private bool _start;
    private bool _startBefore;

    // The copy the last start began was the main one (for RESMP, section 11).
    private bool _startMain;

    // A start the player's graphics have not taken yet (section 10's extra clock).
    private bool _startUntaken;

    internal TiaPositionCounter()
    {
    }

    /// <summary>The two-phase clock's quarter, 0 to 3: 0 is the clock after a boundary.</summary>
    internal int Phase => _phase;

    /// <summary>The counter's place in its sequence, 0 to 39.</summary>
    internal int Count => _count;

    /// <summary>True while a reset holds the counter.</summary>
    internal bool Resetting => _reset > 0;

    /// <summary>The start signal: active for the four clocks of the count it began.</summary>
    internal bool Start => _start;

    /// <summary>The start signal of the count before, which a width of eight adds (tia.md section 12).</summary>
    internal bool StartBefore => _startBefore;

    /// <summary>The last start began the main copy, not a close, medium or far one.</summary>
    internal bool StartMain => _startMain;

    /// <summary>
    /// The copies NUSIZ D2 to D0 ask for, besides the main one, as a mask: bit 0 close, bit 1
    /// medium, bit 2 far (tia.md section 10's table). The stretched settings, <c>%101</c> and
    /// <c>%111</c>, ask for none.
    /// </summary>
    internal static int Copies(byte nusiz) => (nusiz & 0x07) switch
    {
        0b001 => 0b001,
        0b010 => 0b010,
        0b011 => 0b011,
        0b100 => 0b100,
        0b110 => 0b110,
        _ => 0,
    };

    /// <summary>
    /// A reset strobe has landed: the counter is held for four clocks and then starts at count 0.
    /// The ball's reset also sends a start (<paramref name="sendsStart"/>, tia.md section 12), so
    /// the ball draws where its count restarts, on the same line. A start being drawn, or one a
    /// player's scan counter has not taken yet, is cut off [guessing - verify: the suite's
    /// <c>collision/resbl-kill</c>, task 7's, says a ball pixel not yet shown is erased; the model
    /// does the same for every object]; a decode already held rides through.
    /// </summary>
    internal void Reset(bool sendsStart)
    {
        _reset = ResetClocks;
        _start = false;
        _startBefore = false;
        _startUntaken = false;
        if (sendsStart)
        {
            _held = Main;
        }
    }

    /// <summary>
    /// Sets the counter to <paramref name="count"/> and <paramref name="phase"/> at once, ending any
    /// reset, with the decode that count makes held: RESMP's reset of a missile (tia.md section 11).
    /// </summary>
    internal void Plant(int count, int phase)
    {
        _reset = 0;
        _count = count;
        _phase = phase;
        _held = Decode(count);
    }

    /// <summary>
    /// The player's graphics take the start: true once for each start, on the first clock their
    /// own clock lets them (tia.md section 10).
    /// </summary>
    internal bool TakeStart()
    {
        bool untaken = _startUntaken;
        _startUntaken = false;
        return untaken;
    }

    /// <summary>
    /// The end of a colour clock: <paramref name="motionClock"/> says MOTCK ran in it (the blank is
    /// off); <paramref name="copies"/> is the copy mask (<see cref="Copies"/>) as NUSIZ stands now.
    /// </summary>
    internal void Clock(bool motionClock, int copies)
    {
        if (_reset > 0)
        {
            if (_reset > ResetMotionClocks || motionClock)
            {
                if (--_reset == 0)
                {
                    _count = 0;
                    _phase = 0;
                    Boundary(copies);
                }
            }

            return;
        }

        if (!motionClock)
        {
            return;
        }

        if (++_phase == 4)
        {
            _phase = 0;
            _count = _count == Counts - 1 ? 0 : _count + 1;
            Boundary(copies);
        }
    }

    internal void PowerOn()
    {
        _count = 0;
        _phase = 0;
        _reset = 0;
        _held = None;
        _start = false;
        _startBefore = false;
        _startMain = false;
        _startUntaken = false;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_count), _count);
        sink.Add(nameof(_phase), _phase);
        sink.Add(nameof(_reset), _reset);
        sink.Add(nameof(_held), _held);
        sink.Add(nameof(_start), _start);
        sink.Add(nameof(_startBefore), _startBefore);
        sink.Add(nameof(_startMain), _startMain);
        sink.Add(nameof(_startUntaken), _startUntaken);
    }

    // The decode the counter makes on entering a count (tia.md section 9).
    private static int Decode(int count) => count switch
    {
        Counts - 1 => Main,
        3 => Close,
        7 => Medium,
        15 => Far,
        _ => None,
    };

    // A boundary of the two-phase clock, into the count now held: the decode held from the count
    // before becomes the start signal if NUSIZ allows its copy, and the new count's decode is held.
    private void Boundary(int copies)
    {
        _startBefore = _start;
        int held = _held;
        _start = held == Main || (held != None && (copies & (1 << (held - Close))) != 0);
        if (_start)
        {
            _startMain = held == Main;
            _startUntaken = true;
        }

        _held = Decode(_count);
    }
}
