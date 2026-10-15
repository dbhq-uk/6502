namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// Cartridge images built by the tests, from the board descriptions in
/// <c>docs/atari-2600/facts/cartridge.md</c>. None is a game.
/// </summary>
internal static class TestImages
{
    /// <summary>The size of one bank, and of the cartridge window (cartridge.md section 2).</summary>
    public const int BankSize = 4096;

    /// <summary>
    /// An image of <paramref name="size"/> bytes in which every byte of 4K bank n holds n, so a
    /// read shows which bank answered. With <paramref name="filler"/> the first 256 bytes of every
    /// bank are one repeated byte, so the filler rule gives an 8K, 16K or 32K image the Superchip
    /// (cartridge.md section 9.2); without it, byte <c>$FF</c> of each bank holds <c>$FF</c> less
    /// the bank number instead, which no test of a banked board reads, so the rule does not.
    /// </summary>
    public static byte[] BankNumbers(int size, bool filler)
    {
        byte[] image = new byte[size];
        for (int i = 0; i < size; i++)
        {
            image[i] = (byte)(i / BankSize);
        }

        if (!filler)
        {
            for (int bank = 0; bank * BankSize < size; bank++)
            {
                image[(bank * BankSize) + 0xFF] = (byte)(0xFF - bank);
            }
        }

        return image;
    }

    /// <summary>
    /// An image of <paramref name="size"/> bytes whose bytes change with the offset, so a read
    /// shows which offset answered. No 256 bytes in a row repeat one value.
    /// </summary>
    public static byte[] Pattern(int size)
    {
        byte[] image = new byte[size];
        for (int i = 0; i < size; i++)
        {
            image[i] = (byte)((i * 31) + (i >> 8));
        }

        return image;
    }

    /// <summary>The image's offset for a 13-bit cartridge address in <paramref name="bank"/>.</summary>
    public static int Offset(int bank, int address) => (bank * BankSize) + (address & 0x0FFF);

    /// <summary>
    /// Puts <paramref name="bytes"/> into <paramref name="image"/> at the 13-bit cartridge address
    /// <paramref name="address"/> in <paramref name="bank"/>, for a test that needs code or a
    /// particular byte there.
    /// </summary>
    public static byte[] Put(this byte[] image, int bank, int address, params byte[] bytes)
    {
        bytes.CopyTo(image, Offset(bank, address));
        return image;
    }
}
