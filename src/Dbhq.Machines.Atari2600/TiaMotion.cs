namespace Dbhq.Machines.Atari2600;

/// <summary>
/// HMOVE and the motion clocks: the ripple counter, the five more-motion latches with their
/// comparators, and the HMOVE latch that stretches the blank (<c>docs/atari-2600/facts/tia.md</c>
/// sections 15 to 17). It says, each colour clock, which objects get an extra clock; the objects
/// take it on their own position counters (<see cref="TiaPositionCounter"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The mechanism</b>, as Towers and the suite give it, and as jigo2600's reading of the TIA's
/// schematic names its parts (SEC, SECL, the extra clocks ECx and their enables). There is no table
/// of special cases: every late-HMOVE effect comes from these few pieces and when they act.
/// </para>
/// <list type="bullet">
/// <item><b>The start, SEC.</b> A write to HMOVE is taken on the first colour clock of the line's
/// grid (the horizontal counter's two-phase clock, quarter 2) at least four clocks after the write
/// lands, so the first extra clock falls on the first slot at or after W + 7 (section 15). SEC
/// loads the ripple counter with 15 if it is at rest, and sets every more-motion latch and the
/// HMOVE latch. A count already running is not started again [inferring from jigo2600, Visual
/// Objects: "triggering SEC multiple times while the counter is running has no effect";
/// guessing - verify].</item>
/// <item><b>The comparisons.</b> On each grid clock, an object whose latch is set gets an extra
/// clock; then the ripple counter steps down one, and each latch is cleared where the counter
/// equals its register's nibble with the low three bits inverted (w = D7 to D4 XOR <c>%0111</c>).
/// The register is read as it stands on that clock, so a rewrite while the count runs acts from the
/// next grid clock (section 17). After 0 the counter comes to rest at 15 and stops; a latch whose
/// stop step was missed stays set, and the object gets an extra clock on every grid clock, on every
/// line, until a write makes w equal 15 (<c>$80</c> to <c>$8F</c>). HMCLR changes the registers, not
/// the latches.</item>
/// <item><b>The comb.</b> The HMOVE latch (SECL) is cleared as each line starts. Set when the
/// horizontal counter passes count 16, the blank ends at count 18 (LRHB, colour clock 76) instead
/// (section 15), so the line's first eight pixels are black and the objects miss eight motion
/// clocks.</item>
/// </list>
/// <para>
/// <b>When an extra clock acts.</b> The grid clocks are colour clocks 2, 6, ..., 226 of a line, one
/// after the slots 1, 5, ..., 225 the suite counts, because an extra clock rises with the colour
/// clock and so steps the object before its pixel is latched, half a clock ahead of a motion clock
/// (jigo2600, Visual Objects: the extra clock's edge is "in sync with CLK, not CLKP", and "the levels
/// are updated before" the pixel latch). In the blank it moves the object one pixel left. On the
/// drawn line it merges with that clock's motion clock into one early step, so the object does not
/// move, but its pixel for that clock is skipped and the next one shown twice: a one-pixel dot two
/// clocks after a slot is swallowed and one three after is widened (section 16) [inferring: the
/// placement fits the suite's residues, and makes a reset and an extra clock fall on one clock in
/// <c>hmove-reset-merge</c>, tia.md open item 4].
/// </para>
/// </remarks>
public sealed class TiaMotion : IReportsState
{
    /// <summary>The five objects' bits in the mask <see cref="Tick"/> returns, as the TIA's outputs are numbered.</summary>
    internal const int P0 = 1, P1 = 2, M0 = 4, M1 = 8, Bl = 16;

    /// <summary>The motion registers HMP0 to HMBL, <c>$20</c> to <c>$24</c> (tia.md section 2.1).</summary>
    internal const int Hmp0 = 0x20;

    private const int Objects = 5;
    private const int AllObjects = (1 << Objects) - 1;
    private const int Rest = 15;

    /// <summary>The colour clocks after an HMOVE write lands before it can be taken (section 15).</summary>
    private const int StartDelay = 4;

    // The ripple counter, 15 down to 0, and whether it is counting or at rest.
    private int _ripple;
    private bool _counting;

    // The more-motion latches, one bit an object.
    private int _latches;

    // The HMOVE latch (SECL), which ends the blank at LRHB.
    private bool _extended;

    // An HMOVE written and not yet taken, and the clocks still to wait before it can be.
    private bool _pending;
    private int _wait;

    internal TiaMotion() => PowerOn();

    /// <summary>The HMOVE latch: the blank ends at LRHB on this line (section 15).</summary>
    internal bool Extended => _extended;

    /// <summary>The more-motion latches, for the tests.</summary>
    internal int Latches => _latches;

    /// <summary>A write to HMOVE has landed.</summary>
    internal void Strobe()
    {
        _pending = true;
        _wait = StartDelay;
    }

    /// <summary>A line starts: the HMOVE latch is cleared (section 16; Towers: "when the HSync Counter wraps around").</summary>
    internal void LineStart() => _extended = false;

    /// <summary>
    /// One colour clock, at <paramref name="quarter"/> of the horizontal counter's two-phase clock,
    /// with the write registers as they stand. Returns the objects that get an extra clock now.
    /// </summary>
    internal int Tick(int quarter, byte[] registers)
    {
        bool grid = quarter == 2;
        bool start = grid && _pending && _wait == 0;
        if (_wait > 0)
        {
            _wait--;
        }

        if (!grid)
        {
            return 0;
        }

        int pulses = _latches;
        if (_counting && --_ripple < 0)
        {
            _ripple = Rest;
            _counting = false;
        }

        if (start)
        {
            // SEC: the count starts if it is at rest, and every latch is set.
            _pending = false;
            if (!_counting)
            {
                _ripple = Rest;
                _counting = true;
            }

            _latches = AllObjects;
            _extended = true;
        }

        if (_latches != 0)
        {
            for (int i = 0; i < Objects; i++)
            {
                if (((registers[Hmp0 + i] >> 4) ^ 0x07) == _ripple)
                {
                    _latches &= ~(1 << i);
                }
            }
        }

        return pulses;
    }

    internal void PowerOn()
    {
        _ripple = Rest;
        _counting = false;
        _latches = 0;
        _extended = false;
        _pending = false;
        _wait = 0;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_ripple), _ripple);
        sink.Add(nameof(_counting), _counting);
        sink.Add(nameof(_latches), _latches);
        sink.Add(nameof(_extended), _extended);
        sink.Add(nameof(_pending), _pending);
        sink.Add(nameof(_wait), _wait);
    }
}
