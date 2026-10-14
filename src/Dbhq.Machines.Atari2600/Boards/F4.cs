namespace Dbhq.Machines.Atari2600.Boards;

/// <summary>
/// F4, Atari's 32K board: eight banks, <c>$1FF4</c> to <c>$1FFB</c> selecting banks 0 to 7
/// (<c>docs/atari-2600/facts/cartridge.md</c> section 2). The switching is <see cref="BankedBoard"/>'s.
/// </summary>
public sealed class F4 : BankedBoard
{
    /// <summary>A board over <paramref name="image"/>, 32768 bytes, which it keeps and never changes.</summary>
    internal F4(byte[] image)
        : base(image, banks: 8, firstHotspot: 0xFF4)
    {
    }
}
