namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// Builds cartridge files from byte arrays, from the format's description in
/// <c>docs/nes/facts/cartridge.md</c>. Tests use these and never a game.
/// </summary>
public static class TestCartridge
{
    /// <summary>An iNES 1 file: the 16-byte header, an optional trainer, PRG, then CHR.</summary>
    public static byte[] Ines1(
        int prgBanks,
        int chrBanks,
        int mapper = 0,
        bool verticalMirroring = false,
        bool fourScreen = false,
        bool battery = false,
        bool trainer = false,
        int prgRamUnits = 0,
        byte prgFill = 0,
        byte chrFill = 0)
    {
        byte[] header = Header();
        header[4] = (byte)prgBanks;
        header[5] = (byte)chrBanks;
        header[6] = (byte)(((mapper & 0x0F) << 4)
            | (verticalMirroring ? 1 : 0)
            | (battery ? 2 : 0)
            | (trainer ? 4 : 0)
            | (fourScreen ? 8 : 0));
        header[7] = (byte)(mapper & 0xF0);
        header[8] = (byte)prgRamUnits;
        return Join(header, trainer ? Filled(512, 0xEE) : [], Filled(prgBanks * 16384, prgFill), Filled(chrBanks * 8192, chrFill));
    }

    /// <summary>
    /// A NES 2.0 file with the sizes written in the simple form (a 12-bit count in 16 KB and 8 KB
    /// units). <paramref name="prgRamShift"/> and the others are the nibbles of bytes 10 and 11.
    /// </summary>
    public static byte[] Nes2(
        int prgBanks,
        int chrBanks,
        int mapper = 0,
        int submapper = 0,
        int timing = 0,
        int prgRamShift = 0,
        int prgNvRamShift = 0,
        int chrRamShift = 0,
        bool verticalMirroring = false,
        byte prgFill = 0,
        byte chrFill = 0)
    {
        byte[] header = Header();
        header[4] = (byte)prgBanks;
        header[5] = (byte)chrBanks;
        header[6] = (byte)(((mapper & 0x0F) << 4) | (verticalMirroring ? 1 : 0));
        header[7] = (byte)((mapper & 0xF0) | 0x08);
        header[8] = (byte)(((mapper >> 8) & 0x0F) | (submapper << 4));
        header[9] = (byte)(((prgBanks >> 8) & 0x0F) | (((chrBanks >> 8) & 0x0F) << 4));
        header[10] = (byte)(prgRamShift | (prgNvRamShift << 4));
        header[11] = (byte)chrRamShift;
        header[12] = (byte)timing;
        return Join(header, Filled(prgBanks * 16384, prgFill), Filled(chrBanks * 8192, chrFill));
    }

    /// <summary>A header with the magic and everything else zero, for a test to set.</summary>
    public static byte[] Header()
    {
        byte[] header = new byte[16];
        header[0] = 0x4E;
        header[1] = 0x45;
        header[2] = 0x53;
        header[3] = 0x1A;
        return header;
    }

    /// <summary>The files laid end to end.</summary>
    public static byte[] Join(params byte[][] parts)
    {
        return parts.SelectMany(p => p).ToArray();
    }

    /// <summary>A block of <paramref name="length"/> bytes, each <paramref name="value"/>.</summary>
    public static byte[] Filled(int length, byte value)
    {
        byte[] block = new byte[length];
        Array.Fill(block, value);
        return block;
    }

    /// <summary>A block whose byte at <c>i</c> is <c>i % 251</c>, so a misplaced read shows.</summary>
    public static byte[] Pattern(int length, int offset = 0)
    {
        byte[] block = new byte[length];
        for (int i = 0; i < length; i++)
        {
            block[i] = (byte)((i + offset) % 251);
        }

        return block;
    }
}
