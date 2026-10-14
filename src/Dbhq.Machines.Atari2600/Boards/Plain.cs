namespace Dbhq.Machines.Atari2600.Boards;

/// <summary>
/// A plain 2K or 4K cartridge: the image fills the window, and a 2K image appears twice because
/// the board does not wire A11. Nothing switches and a write changes nothing.
/// </summary>
/// <remarks>Built from <c>docs/atari-2600/facts/cartridge.md</c> section 1.</remarks>
public sealed class Plain : IBoard
{
    private readonly byte[] _image;
    private readonly int _mask;

    /// <summary>A board over <paramref name="image"/>, 2048 or 4096 bytes, which it keeps and never changes.</summary>
    internal Plain(byte[] image)
    {
        _image = image;
        _mask = image.Length - 1;
    }

    /// <inheritdoc />
    public int Bank => 0;

    /// <inheritdoc />
    public byte Access(ushort address, bool write, byte dataBus) => write ? dataBus : _image[address & _mask];

    /// <inheritdoc />
    public byte Peek(ushort address) => _image[address & _mask];

    /// <inheritdoc />
    public void PowerOn()
    {
    }
}
