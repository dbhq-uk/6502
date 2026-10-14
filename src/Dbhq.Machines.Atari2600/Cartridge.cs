using System.Globalization;
using Dbhq.Machines.Atari2600.Boards;

namespace Dbhq.Machines.Atari2600;

/// <summary>
/// A cartridge file, read: its image and the board chosen for it. A 2600 image has no header, so
/// the board is chosen by the file's size, and an F8, F6 or F4 image gets the Superchip when the
/// first 256 bytes of every 4K bank hold one repeated byte.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/atari-2600/facts/cartridge.md</c> section 9. Among the boards modelled each
/// size names one board (9.1). The Superchip's RAM hides the first 256 bytes of each bank, so a
/// Superchip image usually has filler there, and a plain image that happens to have filler there
/// loses nothing by getting the RAM (9.2). The rule misses a Superchip image whose hidden bytes are
/// not filler (Korth names Dig Dug), which then runs without its RAM; detecting the board from the
/// image is issue #126.
/// </para>
/// <para>
/// <see cref="Load"/> is given what a visitor chose to open, so it trusts nothing: the length is
/// checked before anything is copied, and a file of any other size is refused with a
/// <see cref="CartridgeException"/> whose message is one plain sentence. A file of a modelled size
/// made for another board (an 8K E0 game, say) runs as the board of its size, and fails.
/// </para>
/// </remarks>
public sealed class Cartridge
{
    /// <summary>The largest file <see cref="Load"/> reads, in bytes: the largest board modelled, F4. The page reads it.</summary>
    public const int MaxFileSize = 32768;

    private const int BankSize = 4096;
    private const int FillerSize = 256;

    /// <summary>
    /// The sizes modelled, each with the board it names, kept in this one place for
    /// <see cref="Load"/> and its message (cartridge.md sections 2 and 9.1).
    /// </summary>
    private static readonly (int Size, string Name, Family Family)[] Boards =
    [
        (2048, "2K", Family.Plain),
        (4096, "4K", Family.Plain),
        (8192, "F8", Family.F8),
        (12288, "FA", Family.Fa),
        (16384, "F6", Family.F6),
        (32768, "F4", Family.F4),
    ];

    private readonly byte[] _image;
    private readonly Family _family;
    private readonly bool _superchip;

    private Cartridge(byte[] image, string name, Family family, bool superchip)
    {
        _image = image;
        _family = family;
        _superchip = superchip;
        BoardName = superchip ? name + " with the Superchip" : name;
    }

    private enum Family
    {
        Plain,
        F8,
        F6,
        F4,
        Fa,
    }

    /// <summary>
    /// The board chosen, for the page to show: "2K", "4K", "F8", "F6", "F4", "FA", or one of the
    /// first three "with the Superchip".
    /// </summary>
    public string BoardName { get; }

    /// <summary>
    /// Reads <paramref name="file"/>: chooses the board by its size and the Superchip by its filler,
    /// and keeps a copy of the image. Throws <see cref="CartridgeException"/>, with a sentence for
    /// the page, for a size no modelled board has; nothing is copied before the size is checked.
    /// </summary>
    public static Cartridge Load(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);

        int index = Array.FindIndex(Boards, board => board.Size == file.Length);
        if (index < 0)
        {
            throw new CartridgeException(Refusal(file.Length));
        }

        (_, string name, Family family) = Boards[index];
        bool superchip = (family is Family.F8 or Family.F6 or Family.F4) && HasFiller(file);
        return new Cartridge((byte[])file.Clone(), name, family, superchip);
    }

    /// <summary>A fresh board over the image, at its power-on bank with its RAM clear.</summary>
    public IBoard CreateBoard()
    {
        if (_family is Family.Plain)
        {
            return new Plain(_image);
        }

        if (_family is Family.Fa)
        {
            return new Fa(_image);
        }

        BankedBoard board = _family switch
        {
            Family.F8 => new F8(_image),
            Family.F6 => new F6(_image),
            _ => new F4(_image),
        };
        return _superchip ? new Superchip(board) : board;
    }

    /// <summary>True when the first 256 bytes of every 4K bank are one repeated byte, each bank its own (cartridge.md 9.2).</summary>
    private static bool HasFiller(byte[] file)
    {
        for (int bank = 0; bank < file.Length; bank += BankSize)
        {
            byte filler = file[bank];
            for (int i = 1; i < FillerSize; i++)
            {
                if (file[bank + i] != filler)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string Refusal(int length)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        var sizes = new System.Text.StringBuilder();
        for (int i = 0; i < Boards.Length; i++)
        {
            string separator = i == 0 ? string.Empty : i == Boards.Length - 1 ? " or " : ", ";
            sizes.Append(separator).Append(Boards[i].Size.ToString("N0", invariant)).Append(" (").Append(Boards[i].Name).Append(')');
        }

        string unit = length == 1 ? "byte" : "bytes";
        return $"This file is {length.ToString("N0", invariant)} {unit}, and this machine runs cartridges of {sizes} bytes only.";
    }
}
