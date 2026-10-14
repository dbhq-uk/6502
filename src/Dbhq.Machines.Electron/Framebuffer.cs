namespace Dbhq.Machines.Electron;

/// <summary>
/// The picture the ULA draws: 640 by 256 pixels, RGBA, one <see cref="uint"/> each as
/// <c>0xAABBGGRR</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Across.</b> One pixel is a 16 MHz pixel clock, the finest the ULA makes, so 640 is the 40
/// microseconds a line displays and every mode fills the width: a mode 0 or 3 pixel is one wide, a
/// mode 1, 4 or 6 pixel two, a mode 2 or 5 pixel four (<c>ula.md</c> s3b).
/// </para>
/// <para>
/// <b>Down.</b> Row <c>y</c> is display line <c>y</c> of a field, 0 to 255. The Electron's two
/// fields show the same lines (s4c, s5d), so the picture is one field's lines, not the BBC
/// Micro's two fields woven: the page stretches it to 4:3, and there is no flicker to weave. Rows
/// 250 to 255 in modes 3 and 6, the blank lines under each text row in those modes, and anything
/// the display has not drawn are opaque black.
/// </para>
/// <para>
/// <b>When.</b> The display draws a line at a time as the machine runs, and before anything here
/// is read it is brought up to now. <see cref="Frames"/> moves on once a frame, at the end of the
/// odd field, which is 25 times a second of machine time (s5d: a frame is 80,000 cycles of 2 MHz).
/// </para>
/// </remarks>
public sealed class Framebuffer
{
    /// <summary>The width in pixels.</summary>
    internal const int PixelsAcross = 640;

    /// <summary>The height in rows: the 256 lines of a field.</summary>
    internal const int Rows = 256;

    /// <summary>Opaque black.</summary>
    internal const uint Black = 0xFF000000;

    private readonly uint[] _pixels = new uint[PixelsAcross * Rows];
    private long _frames;

    internal Framebuffer()
    {
        Array.Fill(_pixels, Black);
    }

    /// <summary>640.</summary>
    public int Width => PixelsAcross;

    /// <summary>256.</summary>
    public int Height => Rows;

    /// <summary>Every pixel, row by row from the top left, as <c>0xAABBGGRR</c>.</summary>
    public ReadOnlySpan<uint> Pixels
    {
        get
        {
            BeforeRead?.Invoke();
            return _pixels;
        }
    }

    /// <summary>Frames completed: it moves on at the end of each odd field.</summary>
    public long Frames
    {
        get
        {
            BeforeRead?.Invoke();
            return _frames;
        }
    }

    /// <summary>What a reader would see at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public uint Pixel(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, PixelsAcross);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Rows);
        return Pixels[(y * PixelsAcross) + x];
    }

    /// <summary>Brings the drawing up to now before anything is read, so a reader never sees a stale picture.</summary>
    internal Action? BeforeRead { get; set; }

    /// <summary>The pixels, for the display to draw into, without bringing anything up to date.</summary>
    internal uint[] Buffer => _pixels;

    /// <summary>Counts a completed frame.</summary>
    internal void FrameDone() => _frames++;
}
