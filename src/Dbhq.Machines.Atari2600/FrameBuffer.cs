namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The picture as the TIA draws it: one TIA colour number a pixel, 160 a line, a row for each line
/// from the line VSYNC ends on to the next end of VSYNC, or to <see cref="MaxLines"/> lines without
/// one; and the region's window of it as RGBA through the region's palette.
/// </summary>
/// <remarks>
/// <para>
/// The 2600 has no frame buffer: the program draws each line as the beam crosses it (spec, "What is
/// being built"). This is the model's record of what the beam drew, kept as colour numbers so the
/// suite's pictures can be compared without a palette (spec, "Decisions made while writing this").
/// Bit 0 of a colour is not wired to the colour decode (<c>tia.md</c> section 19.1), so a colour
/// number here always has it clear.
/// </para>
/// <para>
/// <b>Where a frame starts and ends</b> (the plan's "Fixed before running", items 2 and 4). A frame
/// ends when VSYNC's D1 goes from 1 to 0, and the line it ends on, the first after the VSYNC lines,
/// is the next frame's row 0, a choice over the line after it [guessing - verify]; so a program that
/// makes 262 lines makes frames of 262 rows. A frame with no end of VSYNC ends as its 400th line is
/// done.
/// </para>
/// <para>
/// Two buffers: the TIA draws into one while <see cref="ColourNumbers"/> shows the other, the last
/// frame finished, which stays as it was until the next frame ends. Nothing is allocated after the
/// buffer is made.
/// </para>
/// </remarks>
public sealed class FrameBuffer : IReportsState
{
    /// <summary>Pixels in a line: the 160 colour clocks after the horizontal blank (tia.md section 1).</summary>
    public const int Width = 160;

    /// <summary>The lines a frame may have, the plan's "Fixed before running" item 2.</summary>
    public const int MaxLines = 400;

    private const uint Black = 0xFF000000;

    // The frame being drawn, and the last one finished.
    private byte[] _drawing = new byte[Width * MaxLines];
    private byte[] _shown = new byte[Width * MaxLines];

    // The row being drawn; the rows the last frame had; the frames finished since power on.
    private int _row;
    private int _lines;
    private long _frames;

    internal FrameBuffer()
    {
    }

    /// <summary>The lines the last frame had.</summary>
    public int Lines => _lines;

    /// <summary>Frames finished since power on.</summary>
    public long Frames => _frames;

    /// <summary>The last frame: <see cref="Width"/> colour numbers a row, <see cref="Lines"/> rows, the line VSYNC ended on first.</summary>
    public ReadOnlySpan<byte> ColourNumbers => _shown.AsSpan(0, _lines * Width);

    /// <summary>
    /// The region's window of the last frame (region.md section 3), <see cref="Width"/> pixels by
    /// <see cref="Region.ShownLines"/> rows from <see cref="Region.FirstShownLine"/>, each as
    /// <c>0xAABBGGRR</c> through the region's palette. A row the frame does not reach is black.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="into"/> is shorter than the window.</exception>
    public void ToRgba(Region region, Span<uint> into)
    {
        ArgumentNullException.ThrowIfNull(region);
        int shown = region.ShownLines;
        if (into.Length < Width * shown)
        {
            throw new ArgumentException($"The window needs {Width * shown} pixels; the span has {into.Length}.", nameof(into));
        }

        TiaPalette palette = region.Palette;
        for (int row = 0; row < shown; row++)
        {
            Span<uint> line = into.Slice(row * Width, Width);
            int source = region.FirstShownLine + row;
            if (source >= _lines)
            {
                line.Fill(Black);
                continue;
            }

            ReadOnlySpan<byte> colours = _shown.AsSpan(source * Width, Width);
            for (int x = 0; x < Width; x++)
            {
                line[x] = palette.Rgba(colours[x]);
            }
        }
    }

    /// <summary>Puts pixel <paramref name="x"/> of the row being drawn: a colour number, bit 0 clear.</summary>
    internal void Put(int x, byte colour) => _drawing[(_row * Width) + x] = colour;

    /// <summary>
    /// A line starts: the next row, cleared to black, so a line RSYNC cuts short leaves nothing of
    /// an older frame. The 400th line done ends the frame.
    /// </summary>
    internal void NextLine()
    {
        if (++_row == MaxLines)
        {
            Finish(carry: false);
            return;
        }

        _drawing.AsSpan(_row * Width, Width).Clear();
    }

    /// <summary>VSYNC has ended: the frame ends, and the line being drawn, so far, is the next frame's row 0.</summary>
    internal void EndOfVsync() => Finish(carry: true);

    internal void PowerOn()
    {
        Array.Clear(_drawing);
        Array.Clear(_shown);
        _row = 0;
        _lines = 0;
        _frames = 0;
    }

    /// <inheritdoc />
    void IReportsState.Report(IStateSink sink)
    {
        sink.Add(nameof(_drawing), _drawing);
        sink.Add(nameof(_shown), _shown);
        sink.Add(nameof(_row), _row);
        sink.Add(nameof(_lines), _lines);
        sink.Add(nameof(_frames), _frames);
    }

    private void Finish(bool carry)
    {
        _lines = _row;
        _frames++;
        (_shown, _drawing) = (_drawing, _shown);
        Span<byte> first = _drawing.AsSpan(0, Width);
        if (carry && _row < MaxLines)
        {
            _shown.AsSpan(_row * Width, Width).CopyTo(first);
        }
        else
        {
            first.Clear();
        }

        _row = 0;
    }
}
