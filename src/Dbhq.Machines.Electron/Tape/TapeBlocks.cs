using System.Text;

namespace Dbhq.Machines.Electron.Tape;

/// <summary>A tape stream that is not a run of well-formed blocks. The message names the field and the offset.</summary>
public sealed class TapeFormatException(string message) : Exception(message);

/// <summary>
/// One block of an Acorn cassette file (<c>tape.md</c> s2a). The two CRCs are not kept: they are
/// computed on the way out and checked on the way in. Two blocks are equal when their fields and
/// their data are.
/// </summary>
public sealed record TapeBlock(string Name, uint Load, uint Exec, ushort Number, byte Flag, uint Next, byte[] Data)
{
    public bool Equals(TapeBlock? other) =>
        other is not null && Name == other.Name && Load == other.Load && Exec == other.Exec
        && Number == other.Number && Flag == other.Flag && Next == other.Next
        && Data.AsSpan().SequenceEqual(other.Data);

    public override int GetHashCode() => HashCode.Combine(Name, Load, Exec, Number, Flag, Next, Data.Length);
}

/// <summary>Reads and writes blocks by the layout of <c>tape.md</c> s2a.</summary>
public static class TapeBlocks
{
    private const byte Sync = 0x2A;
    private const byte NoDataFlag = 0x40; // bit 6: the block has no data, so no data CRC
    private const int MaxName = 10; // the OS refuses an eleventh character (tape.md s7 item 6)
    private const int FixedFields = 17; // load 4, exec 4, number 2, length 2, flag 1, next 4

    /// <summary>
    /// Blocks run together in a stream of bytes, one after the other, as the OS sends them (the
    /// carrier between blocks is not in a byte stream). Both CRCs are checked. There is a data CRC
    /// when the block has data and bit 6 of its flag is clear, and none otherwise.
    /// </summary>
    /// <exception cref="TapeFormatException">The stream is not a run of whole, good blocks: the message names the field and the offset.</exception>
    public static IReadOnlyList<TapeBlock> Parse(IEnumerable<byte> bytes)
    {
        byte[] b = bytes as byte[] ?? [.. bytes];
        var blocks = new List<TapeBlock>();
        int at = 0;
        while (at < b.Length)
        {
            blocks.Add(ParseOne(b, ref at));
        }

        return blocks;
    }

    /// <summary>The block as it goes onto tape: the sync byte, the header, its CRC, the data and its CRC, both high byte first.</summary>
    /// <exception cref="ArgumentException">The name is not 1 to 10 characters of 1 to 255, or there is more data than the length field holds.</exception>
    public static byte[] Encode(TapeBlock block)
    {
        string name = block.Name;
        if (name.Length is < 1 or > MaxName || name.Any(c => c is < '\u0001' or > 'ÿ'))
        {
            throw new ArgumentException($"A block name is 1 to {MaxName} characters, none of them zero or above 255: \"{name}\".", nameof(block));
        }

        if (block.Data.Length > ushort.MaxValue)
        {
            throw new ArgumentException($"A block holds at most 65,535 bytes of data, this one has {block.Data.Length}.", nameof(block));
        }

        var header = new List<byte>(name.Length + 1 + FixedFields);
        header.AddRange(Encoding.Latin1.GetBytes(name));
        header.Add(0);
        AddLe(header, block.Load, 4);
        AddLe(header, block.Exec, 4);
        AddLe(header, block.Number, 2);
        AddLe(header, (uint)block.Data.Length, 2);
        header.Add(block.Flag);
        AddLe(header, block.Next, 4);

        var output = new List<byte>(header.Count + block.Data.Length + 5) { Sync };
        output.AddRange(header);
        AddCrc(output, TapeCrc.Of([.. header]));
        output.AddRange(block.Data);
        if (HasDataCrc(block.Flag, block.Data.Length))
        {
            AddCrc(output, TapeCrc.Of(block.Data));
        }

        return [.. output];
    }

    private static bool HasDataCrc(byte flag, int length) => length > 0 && (flag & NoDataFlag) == 0;

    private static void AddLe(List<byte> to, uint value, int bytes)
    {
        for (int i = 0; i < bytes; i++)
        {
            to.Add((byte)(value >> (8 * i)));
        }
    }

    private static void AddCrc(List<byte> to, ushort crc)
    {
        to.Add((byte)(crc >> 8));
        to.Add((byte)crc);
    }

    private static uint Le(byte[] b, int at, int bytes)
    {
        uint value = 0;
        for (int i = 0; i < bytes; i++)
        {
            value |= (uint)b[at + i] << (8 * i);
        }

        return value;
    }

    private static TapeBlock ParseOne(byte[] b, ref int at)
    {
        int start = at;
        if (b[start] != Sync)
        {
            throw new TapeFormatException($"Expected the sync byte $2A at offset {start}, found ${b[start]:X2}.");
        }

        int nameStart = start + 1;
        int window = Math.Min(MaxName + 1, b.Length - nameStart);
        int nameLength = window > 0 ? Array.IndexOf(b, (byte)0, nameStart, window) - nameStart : -1;
        if (nameLength < 0 && window < MaxName + 1)
        {
            throw new TapeFormatException($"The tape ends in the middle of the name of the block at offset {start}: the stream stops at offset {b.Length}.");
        }

        if (nameLength < 0)
        {
            throw new TapeFormatException($"The name of the block at offset {start} has no terminator within {MaxName} characters.");
        }

        if (nameLength == 0)
        {
            throw new TapeFormatException($"The name of the block at offset {start} is empty: a name is 1 to {MaxName} characters.");
        }

        string name = Encoding.Latin1.GetString(b, nameStart, nameLength);
        int fields = nameStart + nameLength + 1;
        int headerCrcAt = fields + FixedFields;
        int dataAt = headerCrcAt + 2;
        if (b.Length < dataAt)
        {
            throw new TapeFormatException($"The tape ends in the middle of the header of block \"{name}\" at offset {start}: the stream stops at offset {b.Length}, the header runs to offset {dataAt}.");
        }

        ushort number = (ushort)Le(b, fields + 8, 2);
        int length = (int)Le(b, fields + 10, 2);
        byte flag = b[fields + 12];
        string label = $"Block \"{name}\" (number {number}) at offset {start}";

        ushort headerCrc = TapeCrc.Of(b.AsSpan(nameStart, headerCrcAt - nameStart));
        ushort storedHeaderCrc = (ushort)((b[headerCrcAt] << 8) | b[headerCrcAt + 1]);
        if (headerCrc != storedHeaderCrc)
        {
            throw new TapeFormatException($"{label}: header CRC error, the tape has ${storedHeaderCrc:X4} and the header gives ${headerCrc:X4}.");
        }

        bool hasCrc = HasDataCrc(flag, length);
        int dataEnd = dataAt + length;
        if (b.Length < dataEnd)
        {
            throw new TapeFormatException($"{label}: the tape ends in the middle of the data, the stream stops at offset {b.Length} and the data runs to offset {dataEnd}.");
        }

        byte[] data = b[dataAt..dataEnd];
        if (hasCrc)
        {
            if (b.Length < dataEnd + 2)
            {
                throw new TapeFormatException($"{label}: the tape ends in the middle of the data CRC, the stream stops at offset {b.Length}.");
            }

            ushort stored = (ushort)((b[dataEnd] << 8) | b[dataEnd + 1]);
            ushort computed = TapeCrc.Of(data);
            if (stored != computed)
            {
                throw new TapeFormatException($"{label}: data CRC error, the tape has ${stored:X4} and the data gives ${computed:X4}.");
            }

            dataEnd += 2;
        }

        at = dataEnd;
        return new TapeBlock(name, Le(b, fields, 4), Le(b, fields + 4, 4), number, flag, Le(b, fields + 13, 4), data);
    }
}
