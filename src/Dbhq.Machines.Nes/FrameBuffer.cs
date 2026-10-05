namespace Dbhq.Machines.Nes;

/// <summary>
/// The picture the PPU draws: 256 by 240 pixels, one <see cref="uint"/> each as
/// <c>0xAABBGGRR</c>, so in memory each pixel is R, G, B, A, as a canvas wants (as the BBC
/// Micro's frame buffer is).
/// </summary>
/// <remarks>
/// <para>
/// The PPU writes one pixel a dot, on dots 2 to 257 of lines 0 to 239: column <c>X</c> on dot
/// <c>X + 2</c> (<c>docs/nes/facts/ppu.md</c> section 6), whether rendering is on or off. So the
/// buffer always holds the latest picture, and a change made in the middle of a frame shows in
/// the pixels drawn after it.
/// </para>
/// <para>
/// <see cref="Frame"/> counts the frames the PPU has completed, the same count as
/// <see cref="Ppu.Frame"/>: it moves on as the pre-render line ends, so the picture of lines 0 to
/// 239 is whole when it does.
/// </para>
/// </remarks>
public sealed class FrameBuffer
{
    /// <summary>Pixels across.</summary>
    public const int Width = 256;

    /// <summary>Pixels down: the 240 visible lines.</summary>
    public const int Height = 240;

    /// <summary>Opaque black, the colour of every pixel before the PPU draws.</summary>
    internal const uint Black = 0xFF000000;

    internal FrameBuffer()
    {
        Array.Fill(Pixels, Black);
    }

    /// <summary>Every pixel, row by row from the top left, as <c>0xAABBGGRR</c>.</summary>
    public uint[] Pixels { get; } = new uint[Width * Height];

    /// <summary>Frames completed since power on; the page draws when this changes.</summary>
    public long Frame { get; private set; }

    internal void EndFrame()
    {
        Frame++;
    }

    internal void PowerOn()
    {
        Frame = 0;
        Array.Fill(Pixels, Black);
    }
}
