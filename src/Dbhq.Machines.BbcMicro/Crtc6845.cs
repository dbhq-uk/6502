namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The Hitachi HD6845S CRT controller, IC2 on the Model B: the counters that make the picture's
/// timing, the memory and raster addresses, and the sync, display and cursor outputs.
/// </summary>
/// <remarks>
/// <para>
/// Built from the fact sheet <c>docs/bbc-micro/facts/video.md</c> section 1, which takes the
/// HD6845S's counter model from the Amstrad CPC CRTC Compendium (ACCC, "CRTC 0", source S2 of the
/// sheet) and the Hitachi datasheet (S1). Everything below is that model; where it is a choice,
/// the choice is named, and the parts left out are in <c>docs/known-differences.md</c>.
/// </para>
/// <para>
/// <b>Registers.</b> <see cref="WriteAddress"/> picks one of R0 to R17 (five bits; 18 to 31 are
/// nothing) and <see cref="WriteData"/> writes it. R0 to R15 are writable, R16 and R17 are not.
/// R12 to R17 read back, as the HD6845S datasheet says ("Start Address Register: possible to
/// read" in its comparison with the HD6845R), with R12 and R14 six bits wide; R16 and R17, the
/// light pen, are never strobed and read 0. R0 to R11 are write only and read <c>$00</c>, which
/// nobody has measured (<c>video.md</c> section 6, item 8).
/// </para>
/// <para>
/// <b>Horizontal.</b> C0 counts character clocks 0 to R0 and wraps. The display is on from C0 =
/// 0 until C0 = R1. HSYNC is high from the character at which C0 = R2 for R3 bits 3 to 0
/// characters (0 makes no HSYNC). MA, the memory address, is the line's start plus C0, 14 bits.
/// </para>
/// <para>
/// <b>Vertical (ACCC 10.3.1, 11.2.2, 13.2).</b> C9 counts lines in a character row and C4 rows.
/// At each line end (C0 = R0): if C9 = R9 then C4 goes up one and C9 to 0, else C9 goes up one,
/// so a C9 past R9 runs on to 31 and wraps. The end of the frame is decided early: while C0 is 0
/// or 1 the chip sets its "last line" state from C4 = R4 and C9 = R9, and never looks again on
/// that line, so changing R4 or R9 later in the last line does not stop the frame ending (quirk
/// 1 of section 1.6). A last line arms the vertical adjust, and at C0 = 2 the chip disarms it if
/// there are no adjust lines to add; with R0 = 0 or 1, C0 never reaches 2, so a one-line adjust
/// happens even with R5 = 0 and every line of two characters has no HSYNC (quirk 2, ACCC
/// 13.2.5). Adjust lines number R5, plus one in interlace on an even field; C4 goes up once, to
/// R4 + 1, and C9 counts them. Then C4 and C9 go to 0, MA reloads from R12 and R13, and a frame
/// starts. The row start MA advances by taking MA at C0 = R1 on the row's last line.
/// </para>
/// <para>
/// <b>Vertical sync.</b> VSYNC starts at the start of the first line of row R7, if the line before
/// reached C0 = 2 (ACCC 13.2.2), and lasts R3 bits 7 to 4 lines (0 = 16), counted at line
/// starts. In either interlace mode, on an even field, the whole pulse is half a line late:
/// VSYNC rises and falls at C0 = (R0 + 1) / 2 instead of at C0 = 0. That makes the field
/// 312.5 lines from vsync to vsync, the 40,000 CPU cycles a real Model B measures (section 5,
/// from S14, where a scope showed the delay as "exactly half a line"); the ACCC gives the delay
/// as C0 = R0/2, a character less, and section 1.4 records the difference.
/// </para>
/// <para>
/// <b>Interlace and parity (ACCC 19.5.2, 19.6.1, 19.7.2).</b> R8 bit 0 turns interlace on. The
/// field's parity is copied at each frame start from a second state that flips when C4 reaches
/// R6 (so a frame in which C4 never reaches R6 does not flip it). An even field gets the
/// half-line VSYNC and the extra adjust line. In interlace sync and video (R8 bits 1 and 0 both
/// set) C9 still counts one a line, a row ends when C9 = R9 / 2, and RA is C9 shifted left with
/// the field's parity in bit 0, so R9 = 18 gives ten lines a field, RA 0, 2 ... 18 on an even
/// field and 1, 3 ... 19 on an odd one.
/// </para>
/// <para>
/// <b>Display and cursor.</b> DISPTMG is the horizontal and vertical display together, delayed
/// by R8 bits 5 and 4 characters (3 = never on). The vertical display goes on at a frame start
/// and off at the start of row R6. CUDISP is high in the character where MA = R14 and R15 on
/// lines R10 bits 4 to 0 to R11 of the row, while the undelayed display is on and the blink
/// allows (R10 bits 6 and 5: 00 steady, 01 none, 10 eight fields on and eight off, 11 sixteen
/// and sixteen, counted from reset), delayed by R8 bits 7 and 6 characters. In the first field
/// after a reset the display and cursor stay off and MA and RA start from 0 (S1, "display
/// sequence after /RES").
/// </para>
/// <para>
/// <b>Lazily, and exactly.</b> In a machine the chip reads the time from the machine's clock and
/// does nothing when a cycle happens. Its character clock is every CPU cycle at 2 MHz and every
/// even one at 1 MHz (the video ULA's control bit 4 chooses, <see cref="SetCharacterClock"/>).
/// When anything looks at it, it works out the cycles owed: the character that ends a line, and
/// the middle of a line during a half-line VSYNC, are stepped one at a time (they are the only
/// characters that can make an event), and every run of characters between them is worked out
/// at once, so a line costs one or two steps, not a step a character. It also works out ahead when its next event is, a VSYNC edge
/// or a frame start, and keeps the state it will have then, so the bus can look at it only at
/// those cycles (<see cref="NextEventCycle"/>) and a look then costs nothing more. The test
/// project holds a per-character model of the same rules and compares the two.
/// </para>
/// <para>
/// <b>Its consumer.</b> The video ULA reads this chip's state a line at a time with
/// <see cref="StateAt"/>, so it must never find the chip past a cycle it still has to ask about.
/// So before the chip moves on to any cycle, for any reason (a register write, the bus's look at
/// an event, the system VIA's catching up, a getter), it first has its <see cref="Consumer"/>
/// draw up to that cycle, and it tells the consumer of a reset. Once the ULA is attached it asks
/// about every line end, so the chip steps a line at a time, one or two steps a line, as well as
/// working its next event out ahead for the bus.
/// </para>
/// </remarks>
public sealed class Crtc6845
{
    private const int EventVsync = 1;
    private const int EventFrame = 2;

    // How far ahead a prediction looks, in steps (a step is a line's last character, or the
    // middle of a line in a half-line VSYNC, so about a line). Just after a register write the chip
    // may be written again soon, so it looks only a few lines ahead; once things are quiet, 1,024
    // lines, about three and a third fields.
    private const int QuickSteps = 4;
    private const int DeepSteps = 1024;

    private readonly byte[] _r = new byte[18];
    private int _address;
    private State _s;

    // The time. On its own the chip counts its Tick calls, one per character. In a machine it
    // reads the CPU cycle count, and _doneTime is the cycle its state stands at: the characters
    // of every cycle up to and including it are done.
    private readonly BbcClock? _clock;
    private long _ownTicks;
    private long _doneTime;
    private bool _fast;
    private bool _catchingUp;

    // The next event worked out ahead: when (a time as above), what it is, and the state just
    // after it. A write to a register that changes the timing makes the time stale; any other
    // write makes only the state stale, and the chip steps to the event instead of jumping.
    private bool _eventTimeValid;
    private bool _snapshotValid;
    private bool _quick = true;
    private long _predictedTime;
    private int _predictedEvent;
    private State _predicted;

    // The chip that reads this one's outputs a line at a time (the video ULA), and whether it is
    // catching up now, so the chip does not call it again from inside its own catch-up.
    private bool _consumerRunning;

    /// <summary>A chip on its own, whose time is the calls to <see cref="Tick"/>.</summary>
    public Crtc6845()
    {
        Reset();
    }

    /// <summary>A chip in a machine, whose time is the machine's clock. It starts on the 1 MHz clock.</summary>
    internal Crtc6845(BbcClock clock)
    {
        _clock = clock;
        _doneTime = clock.Cycles;
        Reset();
    }

    /// <summary>Raised when VSYNC falls, the edge the system VIA's CA1 takes with the OS's PCR of <c>$04</c>.</summary>
    /// <remarks>
    /// In a machine the chip raises its events while it catches up, so a handler runs at some
    /// cycle after the edge, while the chip's outputs still stand at the edge. A handler must not
    /// touch the system VIA or write to this chip.
    /// </remarks>
    public event Action? VSyncFell;

    /// <summary>Raised when a frame starts: C4 and C9 go to 0 and MA reloads from R12 and R13.</summary>
    public event Action? FrameStarted;

    /// <summary>The bus's wire to CA1: each VSYNC edge, with the CPU cycle it happened in and the new level.</summary>
    internal event Action<long, bool>? VsyncDriven;

    /// <summary>
    /// The chip that draws from this one's outputs, the video ULA. Before this chip moves on to any
    /// cycle, for whatever reason, the consumer is brought up to that cycle first, so it never finds
    /// the chip past a cycle it still has to ask about (<see cref="StateAt"/>). A reset is passed
    /// on to it once done.
    /// </summary>
    internal ICrtcConsumer? Consumer { get; set; }

    /// <summary>R0, the horizontal total: the last C0 of a line, unless C0 has run past it.</summary>
    internal int HorizontalTotal => _r[0];

    /// <summary>MA, the 14-bit memory address of the character being output.</summary>
    public int MemoryAddress
    {
        get
        {
            Sync();
            return (_s.LineStart + _s.C0) & 0x3FFF;
        }
    }

    /// <summary>RA, the raster address: C9, or in interlace sync and video C9 shifted left with the field's parity.</summary>
    public int RasterAddress
    {
        get
        {
            Sync();
            return Raster(in _s);
        }
    }

    /// <summary>DISPTMG, with the R8 skew applied.</summary>
    public bool DisplayEnable
    {
        get
        {
            Sync();
            int skew = (_r[8] >> 4) & 3;
            return skew != 3 && ((_s.DispHistory >> skew) & 1) != 0;
        }
    }

    /// <summary>
    /// MA at the start of the current line (C0 = 0): the address of the line's first character,
    /// the same on every line of a character row. <see cref="MemoryAddress"/> is this plus C0.
    /// </summary>
    public int LineStartAddress
    {
        get
        {
            Sync();
            return _s.LineStart;
        }
    }

    /// <summary>
    /// Whether the current line is inside the vertical display window: from a frame start to the
    /// start of row R6, and never in the first field after a reset. DISPTMG is this and the
    /// horizontal display (C0 below R1), delayed by the R8 skew.
    /// </summary>
    public bool VerticalDisplay
    {
        get
        {
            Sync();
            return _s.VDisp && !_s.FirstField;
        }
    }

    /// <summary>
    /// The time the chip's state stands at: a CPU cycle in a machine, a count of
    /// <see cref="Tick"/> calls on its own. <see cref="StateAt"/> answers for any time from here
    /// to now. Every getter, a register write and the bus's looks move it on to now.
    /// </summary>
    public long StateCycle => _doneTime;

    /// <summary>
    /// The chip's state at <paramref name="cycle"/>, a time from <see cref="StateCycle"/> to now:
    /// what a consumer drawing a line at a time reads. The chip is brought up to that cycle and no
    /// further (its events up to it are raised on the way), so the answer reflects the registers in
    /// force then, and a later call can ask about any later cycle. Returns a value; nothing is
    /// allocated.
    /// </summary>
    /// <remarks>
    /// The contract: a CRTC register write brings the chip up to that write's cycle first, after
    /// which no earlier cycle can be asked about; so does a write to the ULA's registers in a
    /// machine, because the ULA draws up to the write's cycle with this method before taking it.
    /// A consumer must take every state it needs, up to a write's cycle, before the write, and it
    /// must take its events before anything else brings the chip up to now. The chip makes sure
    /// of the second for the <see cref="Consumer"/> it has: before moving on for any reason, it
    /// has the consumer draw up to the cycle first. Asking for a cycle the chip has already passed
    /// throws, rather than answering with a later state. Inside one of the chip's own events, only
    /// the event's cycle can be asked about.
    /// </remarks>
    public CrtcState StateAt(long cycle)
    {
        if (cycle > Now)
        {
            throw new ArgumentOutOfRangeException(nameof(cycle), cycle, "That cycle has not happened yet.");
        }

        SyncTo(cycle);
        if (cycle != _doneTime)
        {
            throw new InvalidOperationException(
                $"The CRTC's state stands at {_doneTime}, so it cannot say what it was at {cycle}.");
        }

        int displaySkew = (_r[8] >> 4) & 3;
        int cursorSkew = (_r[8] >> 6) & 3;
        bool vertical = _s.VDisp && !_s.FirstField;
        return new CrtcState(
            cycle,
            _s.C0,
            (_s.LineStart + _s.C0) & 0x3FFF,
            _s.LineStart,
            Raster(in _s),
            vertical,
            displaySkew != 3 && ((_s.DispHistory >> displaySkew) & 1) != 0,
            cursorSkew != 3 && ((_s.CursorHistory >> cursorSkew) & 1) != 0,
            _s.HsActive,
            _s.VsOut,
            _r[1],
            displaySkew,
            cursorSkew,
            CursorAddress,
            vertical && CursorShows(in _s),
            _clock is not null && !_fast ? 2 : 1,
            _s.Line,
            _s.ParityOdd,
            Interlace,
            _s.HDisp,
            _s.DispHistory,
            _s.CursorHistory);
    }

    /// <summary>
    /// <see cref="StateAt"/> for the consumer itself, which is already drawing up to
    /// <paramref name="cycle"/>, so the chip does not call it back first.
    /// </summary>
    internal CrtcState StateForConsumer(long cycle)
    {
        bool running = _consumerRunning;
        _consumerRunning = true;
        try
        {
            return StateAt(cycle);
        }
        finally
        {
            _consumerRunning = running;
        }
    }

    /// <summary>HSYNC.</summary>
    public bool HSync
    {
        get
        {
            Sync();
            return _s.HsActive;
        }
    }

    /// <summary>VSYNC, which drives the system VIA's CA1.</summary>
    public bool VSync
    {
        get
        {
            Sync();
            return _s.VsOut;
        }
    }

    /// <summary>CUDISP, with the R8 skew applied.</summary>
    public bool Cursor
    {
        get
        {
            Sync();
            int skew = (_r[8] >> 6) & 3;
            return skew != 3 && ((_s.CursorHistory >> skew) & 1) != 0;
        }
    }

    /// <summary>
    /// The machine's CPU cycle of this chip's next event (a VSYNC edge or a frame start), or a
    /// later cycle at which to look again if none comes within 1,024 lines, about three and a third
    /// fields. The bus need not look at the chip before then.
    /// </summary>
    internal long NextEventCycle
    {
        get
        {
            if (!_eventTimeValid)
            {
                Sync();
                Predict();
            }
            return _predictedTime;
        }
    }

    private long Now => _clock is null ? _ownTicks : _clock.Cycles;

    private int Half => (_r[0] + 1) >> 1;

    private bool Interlace => (_r[8] & 0x01) != 0;

    private bool SyncAndVideo => (_r[8] & 0x03) == 0x03;

    private int CursorAddress => ((_r[14] << 8) | _r[15]) & 0x3FFF;

    /// <summary>Writes the address register: the number of the register the next data access reaches.</summary>
    public void WriteAddress(byte value) => _address = value & 0x1F;

    /// <summary>Writes the register the address register names. R16, R17 and 18 to 31 take nothing.</summary>
    public void WriteData(byte value)
    {
        if (_address >= 16)
        {
            return;
        }

        if (_catchingUp)
        {
            throw new InvalidOperationException("A handler of this chip's event wrote to it.");
        }

        Sync();
        _r[_address] = (byte)(value & WriteMask(_address));
        if (_address is 1 or 2 or >= 10)
        {
            // The cursor, the display width, HSYNC and the start address change what the chip's
            // state will be, but not when its next event comes.
            _snapshotValid = false;
        }
        else
        {
            Invalidate();
        }
    }

    /// <summary>Reads the register the address register names: R12 to R15 read back, and the rest read 0, the light pen registers R16 and R17 included because the BBC Micro leaves LPSTB unused.</summary>
    public byte ReadData() => _address switch
    {
        >= 12 and <= 15 => _r[_address],
        _ => 0x00,
    };

    /// <summary>
    /// One character clock. Only for a chip on its own: one in a machine takes its time from the
    /// machine's clock.
    /// </summary>
    public void Tick()
    {
        if (_clock is not null)
        {
            throw new InvalidOperationException("This chip's time is the machine's clock.");
        }

        _ownTicks++;
    }

    /// <summary>
    /// The /RES pin (S1): every counter to 0, every output low, the registers kept. The first
    /// field after it shows nothing and ignores R12 and R13. A VSYNC that was high falls, which is
    /// an edge on CA1.
    /// </summary>
    public void Reset()
    {
        Sync();
        bool vsyncWasHigh = _s.VsOut;
        _s = new State { FirstField = true };
        Invalidate();
        if (vsyncWasHigh)
        {
            Raise(EventVsync);
        }

        Consumer?.CrtcReset();
    }

    /// <summary>
    /// The video ULA's control bit 4: true for the 2 MHz character clock (modes 0 to 3), false
    /// for 1 MHz (modes 4 to 7). The cycles before the change are done at the old rate.
    /// </summary>
    internal void SetCharacterClock(bool twoMhz)
    {
        if (twoMhz == _fast)
        {
            return;
        }

        Sync();
        _fast = twoMhz;
        Invalidate();
    }

    /// <summary>
    /// Catches up only if an event may be due by now: what the system VIA calls before it catches
    /// up itself, so it has seen every VSYNC edge before the cycle it moves to.
    /// </summary>
    internal void SyncIfDue()
    {
        if (!_eventTimeValid || _predictedTime <= Now)
        {
            Sync();
        }
    }

    /// <summary>Does the characters owed up to now, raising each event at its own cycle.</summary>
    internal void Sync() => SyncTo(Now);

    /// <summary>
    /// Does the characters owed up to <paramref name="time"/>, which is not after now. A time the
    /// chip has already passed does nothing: it cannot go back. Inside one of its own events the
    /// chip stands still, so a handler sees the state at the event.
    /// </summary>
    internal void SyncTo(long time)
    {
        if (_catchingUp || time <= _doneTime)
        {
            return;
        }

        if (Consumer is not null && !_consumerRunning)
        {
            // The consumer draws up to the cycle first, asking this chip about each line on the way.
            _consumerRunning = true;
            try
            {
                Consumer.CatchUpTo(time);
            }
            finally
            {
                _consumerRunning = false;
            }

            if (time <= _doneTime)
            {
                return;
            }
        }

        _catchingUp = true;
        try
        {
            while (_doneTime < time)
            {
                if (_eventTimeValid && _predictedTime <= _doneTime)
                {
                    _eventTimeValid = _snapshotValid = false;
                }

                if (_eventTimeValid && _snapshotValid && _predictedTime <= time)
                {
                    _s = _predicted;
                    _doneTime = _predictedTime;
                    _eventTimeValid = _snapshotValid = false;
                    Raise(_predictedEvent);
                    continue;
                }

                long ticks = TicksBetween(_doneTime, time);
                if (ticks == 0)
                {
                    _doneTime = time;
                    break;
                }

                long ran = Run(ref _s, ticks, int.MaxValue, out int events);
                if (events == 0)
                {
                    _doneTime = time;
                    break;
                }

                _doneTime = TimeAfter(_doneTime, ran);
                Raise(events);
            }

            if (_eventTimeValid && _predictedTime <= _doneTime)
            {
                _eventTimeValid = _snapshotValid = false;
            }
        }
        finally
        {
            _catchingUp = false;
        }
    }

    private static byte WriteMask(int register) => register switch
    {
        4 or 6 or 7 or 10 => 0x7F,
        5 or 9 or 11 => 0x1F,
        8 => 0xF3,
        12 or 14 => 0x3F,
        _ => 0xFF,
    };

    private void Invalidate()
    {
        _eventTimeValid = _snapshotValid = false;
        _quick = true;
    }

    /// <summary>The time of the <paramref name="characters"/>-th character clock after <paramref name="from"/>.</summary>
    internal long TimeAfterCharacters(long from, long characters) => TimeAfter(from, characters);

    /// <summary>The character clocks after <paramref name="from"/> up to and including <paramref name="to"/>.</summary>
    internal long CharactersBetween(long from, long to) => TicksBetween(from, to);

    /// <summary>Character clocks between two times: every cycle at 2 MHz (or on its own), every even cycle at 1 MHz.</summary>
    private long TicksBetween(long from, long to) =>
        _clock is not null && !_fast ? (to >> 1) - (from >> 1) : to - from;

    /// <summary>The time of the <paramref name="ticks"/>-th character clock after <paramref name="from"/>.</summary>
    private long TimeAfter(long from, long ticks) =>
        _clock is not null && !_fast ? (from & ~1L) + (2 * ticks) : from + ticks;

    private void Raise(int events)
    {
        if ((events & EventVsync) != 0)
        {
            bool level = _s.VsOut;
            VsyncDriven?.Invoke(_doneTime, level);
            if (!level)
            {
                VSyncFell?.Invoke();
            }
        }

        if ((events & EventFrame) != 0)
        {
            FrameStarted?.Invoke();
        }
    }

    /// <summary>Works out the next event from now, and the state just after it, on a copy of the state.</summary>
    private void Predict()
    {
        State s = _s;
        long ran = Run(ref s, long.MaxValue / 4, _quick ? QuickSteps : DeepSteps, out int events);
        _predicted = s;
        _predictedTime = TimeAfter(_doneTime, ran);
        _predictedEvent = events;
        _eventTimeValid = _snapshotValid = true;
        _quick = false;
    }

    /// <summary>
    /// Runs up to <paramref name="ticks"/> character clocks, stopping after the first that has an
    /// event, or after <paramref name="maxSteps"/> steps. Returns the clocks run.
    /// </summary>
    /// <remarks>
    /// Only two characters of a line can make an event: the one that ends the line, and, during a
    /// half-line VSYNC, the one at the middle. Those go through <see cref="Step"/>; every run of
    /// characters between them is done at once by <see cref="Advance"/>, so a line costs one or two
    /// steps however long it is.
    /// </remarks>
    private long Run(ref State s, long ticks, int maxSteps, out int events)
    {
        long done = 0;
        int steps = 0;
        events = 0;
        while (done < ticks)
        {
            int c = s.C0;
            int stop = c <= _r[0] ? _r[0] : 255;
            if (s.VsMid)
            {
                int half = Half;
                if (half > c && half <= stop)
                {
                    stop = half - 1;
                }
            }

            long run = Math.Min(stop - c, ticks - done);
            if (run > 0)
            {
                Advance(ref s, c, c + (int)run);
                done += run;
                if (done == ticks)
                {
                    break;
                }
            }

            events = Step(ref s);
            done++;
            if (events != 0 || ++steps >= maxSteps)
            {
                break;
            }
        }

        return done;
    }

    /// <summary>
    /// The characters at which C0 is <paramref name="from"/> + 1 to <paramref name="to"/>, in one
    /// go: exactly what <see cref="Step"/> would do for each, given that none of them ends the line
    /// or is the middle of a half-line VSYNC, so none moves the vertical counters or VSYNC. The rules
    /// at C0 = 1, 2 and R1 happen once each if their character is in the run, HSYNC is counted in
    /// at most a few spans, and the display and cursor histories are worked out for the last three
    /// characters.
    /// </summary>
    private void Advance(ref State s, int from, int to)
    {
        if (from < 1)
        {
            s.LastLine = s.C4 == _r[4] && RowEnds(in s);
            s.AdjustArmed = s.LastLine;
        }

        if (from < 2 && to >= 2)
        {
            if (s.LastLine && AdjustLines(in s) == 0)
            {
                s.AdjustArmed = false;
            }
            s.VsyncAllowed = true;
        }

        int r1 = _r[1];
        bool hDisplay = s.HDisp;
        bool endsDisplay = r1 > from && r1 <= to;
        if (endsDisplay)
        {
            s.HDisp = false;
            if (RowEnds(in s))
            {
                s.RowStart = (s.LineStart + r1) & 0x3FFF;
            }
        }

        AdvanceHsync(ref s, from, to);

        // The histories: one bit a character, the newest in bit 0, three kept.
        bool vertical = s.VDisp && !s.FirstField;
        bool cursorLine = vertical && CursorShows(in s);
        int cursorAt = (CursorAddress - s.LineStart) & 0x3FFF;
        int first = Math.Max(from + 1, to - 2);
        int display = s.DispHistory, cursor = s.CursorHistory;
        for (int c = first; c <= to; c++)
        {
            bool on = vertical && hDisplay && !(endsDisplay && c >= r1);
            display = ((display << 1) | (on ? 1 : 0)) & 7;
            cursor = ((cursor << 1) | (on && cursorLine && c == cursorAt ? 1 : 0)) & 7;
        }

        s.DispHistory = display;
        s.CursorHistory = cursor;
        s.C0 = to;
    }

    /// <summary>
    /// HSYNC over the characters at which C0 is <paramref name="from"/> + 1 to <paramref name="to"/>:
    /// each character counts a running pulse and ends it at R3's width, then starts one if C0 = R2.
    /// </summary>
    private void AdvanceHsync(ref State s, int from, int to)
    {
        int width = _r[3] & 0x0F;
        int r2 = _r[2];
        int at = from;
        while (at < to)
        {
            if (s.HsActive)
            {
                int left = (width - s.HsCount) & 0x0F;
                if (left == 0)
                {
                    left = 16;
                }

                if (at + left > to)
                {
                    s.HsCount = (s.HsCount + (to - at)) & 0x0F;
                    return;
                }

                at += left;
                s.HsCount = width;
                s.HsActive = false;
                if (at == r2 && width != 0)
                {
                    s.HsActive = true;
                    s.HsCount = 0;
                }
            }
            else if (width != 0 && r2 > at && r2 <= to)
            {
                at = r2;
                s.HsActive = true;
                s.HsCount = 0;
            }
            else
            {
                return;
            }
        }
    }

    /// <summary>One character clock, the rules in the order the class's remarks give them. Returns its events.</summary>
    private int Step(ref State s)
    {
        bool vsyncBefore = s.VsOut;
        int events = 0;
        if (s.C0 == _r[0] || s.C0 == 255)
        {
            s.C0 = 0;
            events |= NewLine(ref s);
        }
        else
        {
            s.C0++;
        }

        int c0 = s.C0;
        if (c0 < 2)
        {
            // The last line is judged while C0 is 0 or 1, and arms the adjust by default.
            s.LastLine = s.C4 == _r[4] && RowEnds(in s);
            s.AdjustArmed = s.LastLine;
        }
        else if (c0 == 2)
        {
            if (s.LastLine && AdjustLines(in s) == 0)
            {
                s.AdjustArmed = false;
            }
            s.VsyncAllowed = true;
        }

        if (c0 == _r[1])
        {
            s.HDisp = false;
            if (RowEnds(in s))
            {
                s.RowStart = (s.LineStart + c0) & 0x3FFF;
            }
        }

        if (s.HsActive)
        {
            s.HsCount = (s.HsCount + 1) & 0x0F;
            if (s.HsCount == (_r[3] & 0x0F))
            {
                s.HsActive = false;
            }
        }

        if (!s.HsActive && c0 == _r[2] && (_r[3] & 0x0F) != 0)
        {
            s.HsActive = true;
            s.HsCount = 0;
        }

        if (s.VsMid && c0 == Half)
        {
            s.VsOut = s.VsLine;
            if (!s.VsLine)
            {
                s.VsMid = false;
            }
        }

        bool display = s.HDisp && s.VDisp && !s.FirstField;
        bool cursor = display && CursorShows(in s) && ((s.LineStart + c0) & 0x3FFF) == CursorAddress;
        s.DispHistory = ((s.DispHistory << 1) | (display ? 1 : 0)) & 7;
        s.CursorHistory = ((s.CursorHistory << 1) | (cursor ? 1 : 0)) & 7;

        if (s.VsOut != vsyncBefore)
        {
            events |= EventVsync;
        }
        return events;
    }

    /// <summary>C0 has just wrapped: the vertical counters move on, and the line's start is set up.</summary>
    private int NewLine(ref State s)
    {
        int events = 0;
        bool rowStarted = false;
        s.Line++;
        if (s.InAdjust)
        {
            int total = AdjustLines(in s);
            int next = (s.C9 + 1) & 0x1F;

            // C9 counts the adjust lines and is compared with their number for the next line;
            // with no lines to add (only reachable when C0 never got to 2) the one forced line
            // ends it, because the chip compares C9 itself then (ACCC 13.2.5).
            if (next == total || s.C9 == total)
            {
                NewFrame(ref s);
                events |= EventFrame;
                rowStarted = true;
            }
            else
            {
                s.C9 = next;
            }
        }
        else if (s.LastLine)
        {
            if (s.AdjustArmed)
            {
                s.InAdjust = true;
                s.C4 = (s.C4 + 1) & 0x7F;
                s.C9 = 0;
            }
            else
            {
                NewFrame(ref s);
                events |= EventFrame;
            }
            rowStarted = true;
        }
        else if (RowEnds(in s))
        {
            s.C4 = (s.C4 + 1) & 0x7F;
            s.C9 = 0;
            rowStarted = true;
        }
        else
        {
            s.C9 = (s.C9 + 1) & 0x1F;
        }

        s.LineStart = s.RowStart;
        s.HDisp = true;

        if (s.VsLine)
        {
            s.VsCount = (s.VsCount + 1) & 0x0F;
            if (s.VsCount == ((_r[3] >> 4) & 0x0F))
            {
                s.VsLine = false;
            }
        }

        if (rowStarted)
        {
            if (s.C4 == _r[6])
            {
                s.VDisp = false;
                s.ParityR6 = !s.ParityOdd;
            }

            if (s.C4 == _r[7] && s.VsyncAllowed && !s.VsLine)
            {
                s.VsLine = true;
                s.VsCount = 0;
                s.VsMid = Interlace && !s.ParityOdd;
            }
        }

        s.VsyncAllowed = false;
        if (!s.VsMid)
        {
            s.VsOut = s.VsLine;
        }
        return events;
    }

    private void NewFrame(ref State s)
    {
        s.Line = 0;
        s.C4 = 0;
        s.C9 = 0;
        s.InAdjust = false;
        s.RowStart = ((_r[12] << 8) | _r[13]) & 0x3FFF;
        s.FirstField = false;
        s.ParityOdd = s.ParityR6;
        s.VDisp = true;
        s.Fields = (s.Fields + 1) & 0x1F;
    }

    /// <summary>The number of adjust lines: R5, and one more in interlace when the field is even (ACCC 19.6.1).</summary>
    private int AdjustLines(in State s) => (_r[5] & 0x1F) + (Interlace && s.ParityR6 ? 1 : 0);

    /// <summary>Whether this line is the last of its character row: C9 = R9, or C9 = R9 / 2 in interlace sync and video.</summary>
    private bool RowEnds(in State s) => SyncAndVideo ? s.C9 == (_r[9] >> 1) : s.C9 == _r[9];

    private int Raster(in State s) => SyncAndVideo ? ((s.C9 << 1) | (s.ParityOdd ? 1 : 0)) & 0x1F : s.C9;

    private bool CursorShows(in State s)
    {
        int ra = Raster(in s);
        if (ra < (_r[10] & 0x1F) || ra > _r[11])
        {
            return false;
        }

        return ((_r[10] >> 5) & 3) switch
        {
            0 => true,
            1 => false,
            2 => (s.Fields & 0x08) == 0,
            _ => (s.Fields & 0x10) == 0,
        };
    }

    /// <summary>Everything the chip counts, as one value so a prediction can run on a copy.</summary>
    private struct State
    {
        public int C0, C9, C4;
        public int LineStart, RowStart;
        public bool HDisp, VDisp, FirstField;
        public bool LastLine, AdjustArmed, InAdjust, VsyncAllowed;
        public bool HsActive;
        public int HsCount;
        public bool VsLine, VsMid, VsOut;
        public int VsCount;
        public bool ParityOdd, ParityR6;
        public int Fields;
        public int DispHistory, CursorHistory;

        // Lines since the frame started: 0 on its first line.
        public int Line;
    }
}

/// <summary>
/// What <see cref="Crtc6845.StateAt"/> reports: the CRTC's state at one cycle, for a consumer that
/// draws a line at a time.
/// </summary>
/// <param name="Cycle">The cycle asked about.</param>
/// <param name="Character">C0, the character of the line being output.</param>
/// <param name="MemoryAddress">MA, 14 bits: <paramref name="LineStartAddress"/> plus C0.</param>
/// <param name="LineStartAddress">MA at C0 = 0 of this line, the same on every line of a row.</param>
/// <param name="RasterAddress">RA.</param>
/// <param name="VerticalDisplay">The line is inside rows 0 to R6 - 1, and not in the first field after a reset.</param>
/// <param name="DisplayEnable">DISPTMG for this character, with the skew.</param>
/// <param name="Cursor">CUDISP for this character, with the skew.</param>
/// <param name="HSync">HSYNC.</param>
/// <param name="VSync">VSYNC.</param>
/// <param name="HorizontalDisplayed">R1 in force: on a displayed line, characters 0 to R1 - 1 are displayed before the skew.</param>
/// <param name="DisplaySkew">R8 bits 5 and 4: DISPTMG is late by this many characters; 3 means it never comes.</param>
/// <param name="CursorSkew">R8 bits 7 and 6: CUDISP is late by this many characters; 3 means it never comes.</param>
/// <param name="CursorAddress">R14 and R15, 14 bits.</param>
/// <param name="CursorOnLine">The cursor shows on this line: inside the vertical display, RA from R10 to R11, and the blink on. It is then at the character where MA equals <paramref name="CursorAddress"/>, if that is displayed.</param>
/// <param name="CyclesPerCharacter">CPU cycles a character: 1 at 2 MHz (and on its own), 2 at 1 MHz.</param>
/// <param name="LineInFrame">Lines since the frame started (C4 and C9 last went to 0): 0 on its first line, and 0 after a reset.</param>
/// <param name="OddField">The field's parity, copied at each frame start (ACCC 19.6.1): an even field is the one with the half-line VSYNC and the extra line.</param>
/// <param name="Interlace">R8 bit 0: interlace sync, or sync and video.</param>
/// <param name="HorizontalDisplay">The horizontal display after this character, before the skew: on from C0 = 0 until C0 = R1.</param>
/// <param name="DisplayHistory">DISPTMG before the skew for this character (bit 0) and the two before it (bits 1 and 2).</param>
/// <param name="CursorHistory">CUDISP before the skew, the same way.</param>
public readonly record struct CrtcState(
    long Cycle,
    int Character,
    int MemoryAddress,
    int LineStartAddress,
    int RasterAddress,
    bool VerticalDisplay,
    bool DisplayEnable,
    bool Cursor,
    bool HSync,
    bool VSync,
    int HorizontalDisplayed,
    int DisplaySkew,
    int CursorSkew,
    int CursorAddress,
    bool CursorOnLine,
    int CyclesPerCharacter,
    int LineInFrame,
    bool OddField,
    bool Interlace,
    bool HorizontalDisplay,
    int DisplayHistory,
    int CursorHistory);

/// <summary>
/// A chip that reads the CRTC's outputs a line at a time, the video ULA: the CRTC brings it up to a
/// cycle before moving on to that cycle, and tells it of a reset.
/// </summary>
internal interface ICrtcConsumer
{
    /// <summary>Draws everything up to <paramref name="cycle"/>, asking the CRTC with <see cref="Crtc6845.StateAt"/>.</summary>
    void CatchUpTo(long cycle);

    /// <summary>The CRTC's counters have just been reset, at the cycle it stands at.</summary>
    void CrtcReset();
}
