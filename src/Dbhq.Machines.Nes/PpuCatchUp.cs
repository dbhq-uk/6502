using System.Runtime.CompilerServices;

namespace Dbhq.Machines.Nes;

/// <summary>
/// The PPU caught up on demand (the lazy chips design, <c>docs/superpowers/specs/2026-10-06-nes-lazy-chips-design.md</c>;
/// task 2 of its plan). The rest of the class is in <c>Ppu.cs</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two positions.</b> The bus delivers each cycle's dots to the <b>logical</b> position
/// (<see cref="Deliver"/>, an add), and the PPU's state is computed up to the <b>caught-up</b>
/// one. <see cref="CatchUp"/> runs the owed dots, one at a time, with the same per-dot code
/// <see cref="Tick"/> always ran, so the state it leaves is the per-dot build's, dot for dot. Only
/// the moment the dots run moves.
/// </para>
/// <para>
/// <b>When the bus catches it up</b> (<see cref="NesBus"/>): before a CPU access to
/// <c>$2000</c> to <c>$3FFF</c>, OAM DMA's writes to <c>$2004</c> among them, and before a CPU
/// write to the cartridge at <c>$4020</c> to <c>$FFFF</c>, which can switch the pattern banks or
/// the nametable layout under it, at the dot the access sees (the cycle's first two dots are
/// delivered first, as the per-dot build runs them first); at the end of a cycle whose dots reach
/// <see cref="NextEventDot"/> or the frame's end; before power on and the reset button; and every
/// cycle when the board watches the PPU's address bus or the machine is the per-dot reference.
/// </para>
/// <para>
/// <b>What can be seen without an access</b> is the NMI output, which the bus reads at the start
/// of every cycle, and the frame end. The NMI output is the VBlank flag and PPUCTRL bit 7; between
/// register accesses only two dots change it: line 241 dot 1, which sets the flag, and dot 1 of
/// the pre-render line, which clears it. <see cref="NextEventDot"/> is the one of them that would
/// change the output next, and the bus catches up in the cycle that reaches it, so the output is
/// what the per-dot build has at every cycle's end. A register access moves it: a <c>$2000</c>
/// write turns bit 7 on or off, a <c>$2002</c> read clears the flag or stops it being set, and a
/// <c>$2001</c> write decides whether the odd frame drops its last dot, which moves the frame's
/// end; each is made after a catch-up, so the next event is worked out again from the state the
/// access left.
/// </para>
/// <para>
/// <b>What reads catch up.</b> <see cref="Line"/>, <see cref="Dot"/>, <see cref="LogicalDots"/>
/// and <see cref="RenderingEnabled"/> give the logical position and catch nothing up: the
/// position is worked out from the caught-up one and the dots owed, with the odd frames' dropped
/// dot. <see cref="Frame"/> and <see cref="OddFrame"/> change only at a frame end and catch up
/// only when one is owed. Every other read of the state (<see cref="V"/>, <see cref="T"/>,
/// <see cref="FineX"/>, <see cref="WriteToggle"/>, <see cref="Oam"/>, <see cref="Screen"/>,
/// <see cref="Nmi"/>, the peeks) and every register access catches up first, so it gives what the
/// per-dot build would.
/// </para>
/// </remarks>
public sealed partial class Ppu
{
    // Dots delivered by the bus, and dots run, since the PPU was made. Neither is ever reset, so
    // the events below are counted on them too.
    private long _logicalDots;
    private long _caughtUpDots;

    // The logical dot at which the NMI output next changes, or long.MaxValue; the one at which the
    // frame ends; and the earlier of the two, which the bus compares with each cycle. Worked out
    // again (ScheduleEvents) when the dots pass one of them and after a register access or a reset
    // that can move them.
    private long _nextEventDot = long.MaxValue;
    private long _frameEndDot;
    private long _catchUpAt;

    /// <summary>Dots the bus has delivered since the PPU was made: the logical position.</summary>
    public long LogicalDots => _logicalDots;

    /// <summary>Dots the PPU's state has been run to since it was made; at most <see cref="LogicalDots"/>.</summary>
    public long CaughtUpDots => _caughtUpDots;

    /// <summary>
    /// The value of <see cref="LogicalDots"/> once the dot that next changes the NMI output has
    /// been delivered: the dot that sets the VBlank flag while PPUCTRL bit 7 is set and the flag is
    /// clear, the pre-render line's dot 1, which clears it, while both are set, or
    /// <see cref="long.MaxValue"/> while bit 7 is clear, when no dot can change it. Reading it
    /// catches nothing up.
    /// </summary>
    public long NextEventDot => _nextEventDot;

    /// <summary>Whether the dots delivered have reached the next event or the frame's end, so the bus must catch the PPU up before the cycle ends.</summary>
    internal bool EventDue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _logicalDots >= _catchUpAt;
    }

    /// <summary>
    /// The NMI output as the state stands, without a catch-up: the bus reads it at the start of
    /// each cycle, when the state has been run through every dot that changes it (see the remarks).
    /// </summary>
    internal bool NmiOutput => (_status & StatusVblank) != 0 && (_ctrl & 0x80) != 0;

    /// <summary>Adds <paramref name="dots"/> to the logical position and runs none of them.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dots"/> is negative.</exception>
    public void Deliver(int dots)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dots);
        _logicalDots += dots;
    }

    /// <summary>The bus's <see cref="Deliver"/>: its counts are never negative.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Owe(int dots)
    {
        _logicalDots += dots;
    }

    /// <summary>
    /// Runs the dots owed, one at a time with the per-dot code, until <see cref="CaughtUpDots"/>
    /// is <see cref="LogicalDots"/>. Each frame end met on the way is run in turn, as the per-dot
    /// build runs it.
    /// </summary>
    public void CatchUp()
    {
        // The count goes up before the dot runs, so a frame end inside it, which tells the
        // observer, sees the position and the count agree.
        while (_caughtUpDots < _logicalDots)
        {
            _caughtUpDots++;
            RunDot();
        }

        if (_caughtUpDots >= _catchUpAt)
        {
            ScheduleEvents();
        }
    }

    // The logical position: the caught-up one moved on by the dots owed. Up to the frame's end it
    // is a sum; past it each frame is a whole number of lines, less the odd frame's dropped dot
    // when the region drops it and rendering is on, which no dot can change (only a $2001 write,
    // which catches up first).
    private void LogicalPosition(out int line, out int dot)
    {
        long owed = _logicalDots - _caughtUpDots;
        long toEnd = _frameEndDot - _caughtUpDots;
        long index;
        if (owed < toEnd)
        {
            index = ((long)_line * Region.DotsPerLine) + _dot + owed;
        }
        else
        {
            index = owed - toEnd;
            bool odd = !_oddFrame;
            long frame = (long)_lines * Region.DotsPerLine;
            while (true)
            {
                long length = odd && _oddFrameSkipsADot && RenderingEnabled ? frame - 1 : frame;
                if (index < length)
                {
                    break;
                }

                index -= length;
                odd = !odd;
            }
        }

        line = (int)(index / Region.DotsPerLine);
        dot = (int)(index % Region.DotsPerLine);
    }

    // Works out the frame's end and the NMI output's next change from the caught-up state, in
    // dots from the caught-up position, and the earlier of the two for the bus.
    private void ScheduleEvents()
    {
        int index = (_line * Region.DotsPerLine) + _dot;

        // The frame ends with the dot at pre-render dot 340, or at 339 when the odd frame drops
        // its last dot, which dot 338 decides from the rendering switch (Tick).
        bool drops = _line == _preRenderLine && _dot > DropDecidedDot
            ? _dropDot
            : _oddFrame && _oddFrameSkipsADot && RenderingEnabled;
        long toEnd = ((long)_lines * Region.DotsPerLine) - index - (drops ? 1 : 0);
        _frameEndDot = _caughtUpDots + toEnd;

        long next = long.MaxValue;
        if ((_ctrl & 0x80) != 0)
        {
            // Set: the pre-render line's dot 1 clears it. Clear: line 241 dot 1 sets it, unless a
            // $2002 read stopped it, which catching up there finds out at no cost but a catch-up.
            int target = ((_status & StatusVblank) != 0 ? _preRenderLine : VblankLine) * Region.DotsPerLine + 1;
            next = _caughtUpDots + (target >= index ? target - index + 1 : toEnd + target + 1);
        }

        _nextEventDot = next;
        _catchUpAt = Math.Min(next, _frameEndDot);
    }
}
