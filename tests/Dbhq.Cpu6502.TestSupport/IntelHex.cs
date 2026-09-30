using System.Globalization;

namespace Dbhq.Cpu6502.TestSupport;

public static class IntelHex
{
    /// <summary>Loads the data records of an Intel hex file into 64 KB of memory.</summary>
    public static void Load(string path, byte[] memory)
    {
        foreach (string line in File.ReadLines(path))
        {
            if (line.Length < 11 || line[0] != ':')
            {
                continue;
            }

            int count = int.Parse(line.AsSpan(1, 2), NumberStyles.HexNumber);
            int address = int.Parse(line.AsSpan(3, 4), NumberStyles.HexNumber);
            int type = int.Parse(line.AsSpan(7, 2), NumberStyles.HexNumber);
            if (type != 0)
            {
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                memory[(address + i) & 0xFFFF] = byte.Parse(line.AsSpan(9 + i * 2, 2), NumberStyles.HexNumber);
            }
        }
    }
}
