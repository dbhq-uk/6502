using System.Runtime.InteropServices;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// A hash of the picture as far as the PPU has drawn it in the frame, for a point where the PPU
/// can be seen: the rows it has finished in the frame, and the row it is on, whole (the pixels it
/// has drawn on it and those the frame before left). Each finished row is hashed once, at the first
/// point after it is finished, and folded into a running hash, so a frame costs its rows once and a
/// point one row, not the whole picture. The pixels and the position are read as the PPU's state
/// stands (<see cref="Ppu.PixelsAsRun"/>, <see cref="Ppu.CaughtUpLine"/>), with no catch-up, so
/// the hash never changes when the dots run; at a point where the PPU can be seen it has been
/// caught up. Shared by the scene tests' recorder and the differential
/// (<c>bench/nes-speed/differential</c>), which links this file.
/// </summary>
/// <remarks>
/// A picture made in a different order, a line's pixels written all at once at its end, say, or
/// a pixel put in the wrong column and put right a dot later, can leave the frame's picture the
/// same, so a hash at the frame's end alone does not see it. This sees it at any point inside the
/// window: a point on the row is a hash of the row as it stands. The rows below the PPU's line
/// are not hashed until it reaches them, when they are the row it is on. The running hash starts
/// again when <see cref="Ppu.FrameStartTime"/> changes, at each frame end and the reset button.
/// </remarks>
internal sealed class DrawnRows
{
    private const ulong Start = 14695981039346656037UL;

    private ulong _finished = Start;
    private int _rows;
    private long _frameStart = -1;

    /// <summary>
    /// Power on: the next point starts the running hash again, even if the frame's start time is
    /// the one it had (power on sets it to 0, as it was at the first).
    /// </summary>
    public void Restart()
    {
        _frameStart = -1;
    }

    /// <summary>The hash at a point: the rows finished in the frame, their count, and the row the PPU is on.</summary>
    public ulong Hash(Ppu ppu)
    {
        ArgumentNullException.ThrowIfNull(ppu);
        if (ppu.FrameStartTime != _frameStart)
        {
            _frameStart = ppu.FrameStartTime;
            _finished = Start;
            _rows = 0;
        }

        uint[] pixels = ppu.PixelsAsRun;
        int line = ppu.CaughtUpLine;
        int finished = Math.Min(line, FrameBuffer.Height);
        for (; _rows < finished; _rows++)
        {
            _finished = Mix(_finished, Row(pixels, _rows));
        }

        ulong hash = Mix(_finished, (ulong)_rows);
        return line < FrameBuffer.Height ? Mix(hash, Row(pixels, line)) : hash;
    }

    // One row's 256 pixels as 128 words, through four lanes of a multiply, a rotate and an add,
    // each a bijection, so a change in any word changes its lane; then the lanes, mixed.
    private static ulong Row(uint[] pixels, int row)
    {
        ReadOnlySpan<ulong> words = MemoryMarshal.Cast<uint, ulong>(pixels.AsSpan(row * FrameBuffer.Width, FrameBuffer.Width));
        ulong a = 0x243F6A8885A308D3UL, b = 0x13198A2E03707344UL, c = 0xA4093822299F31D0UL, d = 0x082EFA98EC4E6C89UL;
        for (int i = 0; i < words.Length; i += 4)
        {
            a = System.Numerics.BitOperations.RotateLeft(a * 0x9E3779B97F4A7C15UL, 31) + words[i];
            b = System.Numerics.BitOperations.RotateLeft(b * 0x9E3779B97F4A7C15UL, 31) + words[i + 1];
            c = System.Numerics.BitOperations.RotateLeft(c * 0x9E3779B97F4A7C15UL, 31) + words[i + 2];
            d = System.Numerics.BitOperations.RotateLeft(d * 0x9E3779B97F4A7C15UL, 31) + words[i + 3];
        }

        return Mix(Mix(Mix(Mix(Mix(Start, (ulong)row), a), b), c), d);
    }

    // FNV-1a over the word after splitmix64's finaliser, as the differential's Mixed hash.
    private static ulong Mix(ulong hash, ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return (hash ^ value) * 1099511628211UL;
    }
}
