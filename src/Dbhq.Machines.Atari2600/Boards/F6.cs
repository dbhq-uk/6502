namespace Dbhq.Machines.Atari2600.Boards;

/// <summary>
/// F6, Atari's 16K board: four banks, <c>$1FF6</c> to <c>$1FF9</c> selecting banks 0 to 3
/// (<c>docs/atari-2600/facts/cartridge.md</c> section 2). The switching is <see cref="BankedBoard"/>'s.
/// </summary>
public sealed class F6 : BankedBoard
{
    /// <summary>A board over <paramref name="image"/>, 16384 bytes, which it keeps and never changes.</summary>
    internal F6(byte[] image)
        : base(image, banks: 4, firstHotspot: 0xFF6)
    {
    }
}
