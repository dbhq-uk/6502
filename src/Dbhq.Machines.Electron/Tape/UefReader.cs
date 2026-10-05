using System.Globalization;
using System.IO.Compression;

namespace Dbhq.Machines.Electron.Tape;

/// <summary>A file that is not a UEF this machine can load: the message names the problem, or the chunk.</summary>
public sealed class UefException(string message) : Exception(message);

/// <summary>
/// Reads a UEF tape image (<c>tape.md</c> s1) into <see cref="TapeEvent"/>s. It does what the
/// specification says a simple reader needs: chunks <c>&amp;0100</c>, <c>&amp;0110</c> and
/// <c>&amp;0111</c>, and 1200 baud assumed ("Simplified Usage", s1b). It reads the two gap chunks
/// as silence and refuses, naming the chunk, the tapes it cannot load (s6). Everything else it
/// skips by its length.
/// </summary>
public static class UefReader
{
    /// <summary>
    /// The most a gzip stream may inflate to, 16 MiB. A tape of half an hour a side is a few hundred
    /// kilobytes, so this is generous, and it bounds what a gzip stream of a few kilobytes can ask
    /// for. A stream that inflates further is refused.
    /// </summary>
    public const int MaxInflatedBytes = 16 * 1024 * 1024;

    private const int HeaderLength = 12;
    private static readonly byte[] Magic = [.. "UEF File!\0"u8];

    // One immutable event per byte value, shared, so a long data chunk costs a reference a byte.
    private static readonly TapeByte[] ByteEvents = [.. Enumerable.Range(0, 256).Select(i => new TapeByte((byte)i))];

    /// <summary>The tape's events, in order. A file that is gzip is inflated first; one that is not is read as it is.</summary>
    /// <exception cref="UefException">The file is damaged, or is a tape this reader does not load.</exception>
    public static IReadOnlyList<TapeEvent> Read(ReadOnlySpan<byte> file)
    {
        if (file.Length == 0)
        {
            throw new UefException("The file is empty.");
        }

        byte[] inflated = [];
        ReadOnlySpan<byte> bytes = file;
        if (file.Length >= 2 && file[0] == 0x1F && file[1] == 0x8B)
        {
            inflated = Inflate(file);
            bytes = inflated;
            if (bytes.Length == 0)
            {
                throw new UefException("The gzip stream inflates to nothing: it is empty.");
            }
        }

        CheckHeader(bytes);
        var events = new List<TapeEvent>();
        int at = HeaderLength;
        if (at == bytes.Length)
        {
            throw new UefException("The file has a UEF header and no chunks, so there is no tape in it.");
        }

        while (at < bytes.Length)
        {
            if (bytes.Length - at < 6)
            {
                throw new UefException($"The chunk header at offset {at} is cut short: the file has {bytes.Length - at} of its 6 bytes.");
            }

            ushort id = (ushort)(bytes[at] | (bytes[at + 1] << 8));
            uint length = (uint)(bytes[at + 2] | (bytes[at + 3] << 8) | (bytes[at + 4] << 16) | (bytes[at + 5] << 24));
            int dataAt = at + 6;
            if (length > (uint)(bytes.Length - dataAt))
            {
                throw new UefException($"Chunk &{id:X4} at offset {at} says it is {length} bytes long, which runs past the end of the file ({bytes.Length - dataAt} bytes are left).");
            }

            Chunk(id, bytes.Slice(dataAt, (int)length), at, events);
            at = dataAt + (int)length;
        }

        return events;
    }

    private static byte[] Inflate(ReadOnlySpan<byte> file)
    {
        try
        {
            using var gzip = new GZipStream(new MemoryStream(file.ToArray()), CompressionMode.Decompress);
            byte[] buffer = new byte[(int)Math.Clamp((long)file.Length * 4, 4096L, MaxInflatedBytes + 1L)];
            int used = 0;
            while (true)
            {
                if (used == buffer.Length)
                {
                    // Full: grow, but never past one byte over the limit, which is what proves it was passed.
                    Array.Resize(ref buffer, (int)Math.Min((long)buffer.Length * 2, MaxInflatedBytes + 1L));
                }

                int read = gzip.Read(buffer, used, buffer.Length - used);
                if (read == 0)
                {
                    CheckTrailer(file, buffer.AsSpan(0, used));
                    return buffer[..used];
                }

                used += read;
                if (used > MaxInflatedBytes)
                {
                    throw new UefException($"The gzip stream inflates to more than {MaxInflatedBytes / (1024 * 1024)} MB, which is more than a tape needs: refused.");
                }
            }
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or IOException)
        {
            throw new UefException($"The tape is cut short or damaged: its gzip stream does not inflate ({e.Message}).");
        }
    }

    // GZipStream does not say when a stream is cut short or damaged: it hands back whatever it
    // inflated and reports the end. (Cut at every point of a 50,000 byte stream, down to the 12th
    // byte, it threw nothing.) So the trailer is checked here: a gzip member ends with the CRC-32
    // and the length, mod 2^32, of what it inflates to, each little endian. A file of one member is
    // what a gzip writer makes of one file, and what a UEF is.
    private static void CheckTrailer(ReadOnlySpan<byte> file, ReadOnlySpan<byte> inflated)
    {
        const int Overhead = 10 + 8; // the shortest header and the trailer
        if (file.Length < Overhead
            || BitConverter.ToUInt32(file[^8..^4]) != Crc32(inflated)
            || BitConverter.ToUInt32(file[^4..]) != (uint)inflated.Length)
        {
            throw new UefException("The tape is cut short or damaged: its gzip stream does not end with the CRC and length of what it inflates to.");
        }
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return ~crc;
    }

    private static void CheckHeader(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderLength)
        {
            throw new UefException($"The file is too short for a UEF header: it has {bytes.Length} bytes and the header is {HeaderLength}.");
        }

        if (!bytes[..Magic.Length].SequenceEqual(Magic))
        {
            throw new UefException("This is not a UEF: the file does not start with \"UEF File!\" and a zero byte.");
        }

        byte major = bytes[11];
        if (major != 0)
        {
            throw new UefException($"The UEF has major version {major}: this reader loads version 0, whose chunks mean what the specification says (the minor version is {bytes[10]}).");
        }
    }

    private static void Chunk(ushort id, ReadOnlySpan<byte> data, int at, List<TapeEvent> events)
    {
        switch (id)
        {
            case 0x0100:
                foreach (byte b in data)
                {
                    events.Add(ByteEvents[b]);
                }

                break;
            case 0x0102 when data.Length > 0:
                throw Refuse(id, at, "carries explicit tape data (raw bits), which this reader does not load; it loads Acorn program tapes, whose data is in chunk &0100");
            case 0x0104 when data.Length > 0:
                throw Refuse(id, at, "defines a tape format other than the implied 8 bits with a start and a stop bit, which this reader does not load");
            case 0x0110:
                Expect(id, at, data, 2);
                AddCarrier(events, data[0] | (data[1] << 8));
                break;
            case 0x0111:
                // Carrier, then the ten framed bits 0 0 1 0 1 0 1 0 1 1, then carrier. The first
                // bit is the start bit and the last the stop bit; the eight between go down the
                // wire least significant bit first as 0 1 0 1 0 1 0 1, so the byte is 0b10101010,
                // $AA. The specification says "always $AA"; the sheet's "$55-pattern" was this
                // wire order read as if it were the value.
                Expect(id, at, data, 4);
                AddCarrier(events, data[0] | (data[1] << 8));
                events.Add(ByteEvents[0xAA]);
                AddCarrier(events, data[2] | (data[3] << 8));
                break;
            case 0x0112:
                // The sheet and the specification both give the length as 1 / (2n x base
                // frequency) seconds, which shrinks as n grows and so cannot be a length. The
                // specification calls n "a rest length counted relative to the base frequency":
                // this reader takes n half cycles of the base frequency, n / (2 x 1200) seconds, so
                // 2,400 is one second. At 2,000,000 CPU cycles a second that is n x 2,000,000 /
                // 2,400, rounded to the nearest cycle.
                Expect(id, at, data, 2);
                events.Add(new Silence((int)Math.Round((data[0] | (data[1] << 8)) * (double)TapeTiming.CpuHz / (2 * TapeTiming.BaseFrequency), MidpointRounding.AwayFromZero)));
                break;
            case 0x0113:
                Expect(id, at, data, 4);
                float frequency = BitConverter.ToSingle(data);
                if (frequency != TapeTiming.BaseFrequency)
                {
                    throw Refuse(id, at, $"changes the base frequency to {frequency.ToString(CultureInfo.InvariantCulture)} Hz, and only 1200 Hz tapes are loaded");
                }

                break;
            case 0x0116:
                Expect(id, at, data, 4);
                float seconds = BitConverter.ToSingle(data);
                double cycles = Math.Round(seconds * (double)TapeTiming.CpuHz);
                if (!(cycles >= 0 && cycles <= int.MaxValue))
                {
                    throw Refuse(id, at, $"gives a gap of {seconds.ToString(CultureInfo.InvariantCulture)} seconds, which is not a length of time between 0 and about 17 minutes");
                }

                events.Add(new Silence((int)cycles));
                break;
            case 0x0117:
                Expect(id, at, data, 2);
                int baud = data[0] | (data[1] << 8);
                if (baud != 1200)
                {
                    throw Refuse(id, at, $"sets the data encoding to {baud}, and only 1200 baud is loaded (the Electron's ULA is a fixed 1200 baud receiver)");
                }

                break;
            default:
                // The origin, target machine and position markers, security cycles, phase changes, the
                // multiplexed copies, inlay scans, disc, ROM and snapshot chunks, and any id this
                // reader has not heard of: not tape data, skipped by their length.
                break;
        }
    }

    private static void Expect(ushort id, int at, ReadOnlySpan<byte> data, int length)
    {
        if (data.Length != length)
        {
            throw new UefException($"Chunk &{id:X4} at offset {at} is {data.Length} bytes long and should be {length}.");
        }
    }

    private static UefException Refuse(ushort id, int at, string why) =>
        new($"Chunk &{id:X4} at offset {at} {why}.");

    // Carrier chunks that follow each other are one carrier, so a long one the writer split in two
    // reads back as it was written.
    private static void AddCarrier(List<TapeEvent> events, int cycles)
    {
        if (events.Count > 0 && events[^1] is Carrier last && (long)last.Cycles + cycles <= int.MaxValue)
        {
            events[^1] = new Carrier(last.Cycles + cycles);
        }
        else
        {
            events.Add(new Carrier(cycles));
        }
    }
}
