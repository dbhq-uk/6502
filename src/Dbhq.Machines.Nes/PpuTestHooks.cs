namespace Dbhq.Machines.Nes;

/// <summary>
/// Internal members for the tests and the differential (<c>bench/nes-speed/differential</c>): reads
/// of the PPU that do not catch it up, for an observer that must not change when the dots run,
/// and a way to put the PPU where a scene starts. Nothing in the machine calls them. The rest of
/// the class is in <c>Ppu.cs</c>.
/// </summary>
public sealed partial class Ppu
{
    /// <summary>
    /// The picture as the dots run so far have drawn it: <see cref="Screen"/>'s pixels without the
    /// catch-up. At a point where the PPU can be seen it has been caught up, so this is the
    /// picture there.
    /// </summary>
    internal uint[] PixelsAsRun => _pixels;

    /// <summary>The line of the caught-up position: the line of the dot the PPU's state runs next.</summary>
    internal int CaughtUpLine => _line;

    /// <summary>The dot of the caught-up position, 0 to 340.</summary>
    internal int CaughtUpDot => _dot;

    /// <summary>
    /// The dots run before the present frame's line 0 dot 0 and since power on: it changes at each
    /// frame end and at the reset button, which puts the PPU back at the top, and at nothing else.
    /// An observer that keeps something for the frame so far starts again when it changes.
    /// </summary>
    internal long FrameStartTime => _timeBase;

    /// <summary>OAM without the catch-up (<see cref="Oam"/> catches up).</summary>
    internal byte[] OamAsRun => _oam;

    /// <summary>PPUMASK as last written. Only a register write changes it, so it needs no catch-up.</summary>
    internal byte Mask => _mask;

    /// <summary>
    /// A test hook: runs the dots owed, then puts the PPU at <paramref name="line"/>,
    /// <paramref name="dot"/> of an odd or an even frame, with the VBlank, sprite 0 and overflow
    /// flags of <paramref name="status"/> (its bits 7 to 5), so a scene can start at any dot
    /// without running the frame up to it. Everything else is left as it is: the scroll
    /// registers, the shifters, sprite evaluation, the picture and the memories, and the frame
    /// count. Whether the odd frame drops its last dot is worked out as dot 338 would have, when
    /// the position is past it. No program can do this; the scene tests use it to place an
    /// access on every dot of a line, in both regions, which a program timed from the frame
    /// cannot (PAL's 3.2 dots a cycle reach only 10 of every 16 dots of a line).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The line or the dot is outside the region's frame.</exception>
    internal void MoveTo(int line, int dot, bool oddFrame, byte status)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(line);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(line, _lines);
        ArgumentOutOfRangeException.ThrowIfNegative(dot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(dot, Region.DotsPerLine);
        CatchUp();
        _line = line;
        _dot = dot;
        _oddFrame = oddFrame;
        _status = (byte)((_status & 0x1F) | (status & 0xE0));
        _suppressVblank = false;
        _dropDot = line == _preRenderLine && dot > DropDecidedDot && oddFrame && _oddFrameSkipsADot && RenderingEnabled;
        ScheduleEvents();
    }
}
