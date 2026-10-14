namespace Dbhq.Machines.Electron.Tape;

/// <summary>The CRC of an Acorn tape block (<c>tape.md</c> s3).</summary>
public static class TapeCrc
{
    /// <summary>
    /// CRC-16 CCITT as the sheet writes it: polynomial $1021, initial value 0, most significant
    /// bit first, no final XOR (the algorithm usually called XMODEM). On tape it is stored high
    /// byte first.
    /// </summary>
    public static ushort Of(ReadOnlySpan<byte> data)
    {
        int crc = 0;
        foreach (byte b in data)
        {
            crc ^= b << 8;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) & 0xFFFF : (crc << 1) & 0xFFFF;
            }
        }

        return (ushort)crc;
    }
}
