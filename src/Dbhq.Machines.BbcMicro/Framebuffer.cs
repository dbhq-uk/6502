namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The picture the video ULA draws: 640 by 512 pixels, RGBA, one <see cref="uint"/> each as
/// <c>0xAABBGGRR</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Across.</b> One pixel is a 16 MHz pixel clock, 62.5 ns, the finest the ULA makes, so 640
/// is the 40 microseconds a mode 0 to 6 line displays and every mode fills the width: a mode 0
/// pixel is one wide, a mode 1 or 4 pixel two, a mode 2 or 5 pixel four. Pixel 0 is the start of
/// the CRTC's character 0 (C0 = 0) of the line, not a place measured from HSYNC, so the picture
/// does not move when R2 moves the sync, as it would on a television. A character drawn from
/// cycle <c>t</c> of a line that started at cycle <c>s</c> is at <c>8 (t - s)</c>. Whatever a
/// line does not reach is black.
/// </para>
/// <para>
/// <b>Down.</b> Row <c>2y</c> or <c>2y + 1</c> holds line <c>y</c> of the CRTC's frame, counted
/// from the frame start (C4 and C9 to 0), for lines 0 to 255; later lines are not kept. With
/// interlace on (R8 bit 0, which the OS sets in every mode), each frame is one field and the two
/// fields are woven: an even field's lines go to the even rows and an odd field's to the odd
/// rows, because the field after an even field's half-line VSYNC is drawn half a line lower on
/// the screen. With interlace off, both rows get the line. So with the OS's registers a field's
/// 256 lines are rows 0 to 511, and a still picture is the same in both fields. The rows of
/// lines a field did not reach are made black when it ends.
/// </para>
/// <para>
/// <b>When.</b> <see cref="Frames"/> counts the CRTC's frame starts, one a field, fifty a second
/// with the OS's registers: when it moves on, a field is complete. The buffer is drawn a line at a
/// time as the machine runs, so it always holds the latest field woven with the one before, and a
/// palette change in the middle of a field shows in the lines drawn after it.
/// </para>
/// </remarks>
public sealed class Framebuffer
{
    /// <summary>The width in pixels.</summary>
    internal const int PixelsAcross = 640;

    /// <summary>The height in rows: 256 lines of a field, twice.</summary>
    internal const int Rows = 512;

    /// <summary>Opaque black.</summary>
    internal const uint Black = 0xFF000000;

    private readonly uint[] _pixels = new uint[PixelsAcross * Rows];
    private long _frames;
    private long _written;

    internal Framebuffer()
    {
        Array.Fill(_pixels, Black);
    }

    /// <summary>640.</summary>
    public int Width => PixelsAcross;

    /// <summary>512.</summary>
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

    /// <summary>The CRTC's frames completed: it moves on once a field, when a field's lines are all drawn.</summary>
    public long Frames
    {
        get
        {
            BeforeRead?.Invoke();
            return _frames;
        }
    }

    /// <summary>
    /// How many pixels the video ULA has written since the machine was made, black ones included:
    /// a measure of the drawing's work, for tests and the speed bench. A black write over a row
    /// already black is skipped and not counted.
    /// </summary>
    public long PixelsWritten
    {
        get
        {
            BeforeRead?.Invoke();
            return _written;
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

    /// <summary>The pixels, for the ULA to draw into, without bringing anything up to date.</summary>
    internal uint[] Buffer => _pixels;

    /// <summary>Counts pixels written.</summary>
    internal void Wrote(int pixels) => _written += pixels;

    /// <summary>Counts a completed frame.</summary>
    internal void FrameDone() => _frames++;
}
