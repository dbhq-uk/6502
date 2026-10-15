namespace Dbhq.Machines.Atari2600.Boards;

/// <summary>
/// F8, Atari's 8K board: two banks, <c>$1FF8</c> selecting bank 0 and <c>$1FF9</c> bank 1
/// (<c>docs/atari-2600/facts/cartridge.md</c> section 2). The switching is <see cref="BankedBoard"/>'s.
/// </summary>
public sealed class F8 : BankedBoard
{
    /// <summary>A board over <paramref name="image"/>, 8192 bytes, which it keeps and never changes.</summary>
    internal F8(byte[] image)
        : base(image, banks: 2, firstHotspot: 0xFF8)
    {
    }
}
