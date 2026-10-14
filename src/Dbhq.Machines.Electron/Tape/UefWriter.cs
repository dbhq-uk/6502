using System.IO.Compression;
using System.Text;

namespace Dbhq.Machines.Electron.Tape;

/// <summary>
/// Writes <see cref="TapeEvent"/>s as a UEF 0.10 file, gzip compressed (<c>tape.md</c> s1). The
/// chunks are an origin (<c>&amp;0000</c>), the target machine (<c>&amp;0005</c>, an Electron), and
/// then data as <c>&amp;0100</c> and carrier as <c>&amp;0110</c>, which is all the OS's own tape
/// needs (s1a, s7): carrier is an idle line, and there is no dummy byte, so <c>&amp;0111</c> is
/// read and never written. A gap is a floating point gap, <c>&amp;0116</c>.
/// </summary>
public static class UefWriter
{
    private const int MaxCarrierChunk = ushort.MaxValue;

    /// <summary>The file for <paramref name="events"/>, with <paramref name="origin"/> as its origin text.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A carrier or a silence is negative.</exception>
    public static byte[] Write(IEnumerable<TapeEvent> events, string origin)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(origin);

        var raw = new MemoryStream();
        raw.Write("UEF File!\0"u8);
        raw.WriteByte(10); // the minor version, then the major: 0.10 (tape.md s1)
        raw.WriteByte(0);
        WriteChunk(raw, 0x0000, Encoding.UTF8.GetBytes(origin));
        WriteChunk(raw, 0x0005, [0x10]); // an Electron, no keyboard preference

        var run = new List<byte>();
        foreach (TapeEvent e in events)
        {
            switch (e)
            {
                case TapeByte b:
                    run.Add(b.Value);
                    continue;
                case Carrier c:
                    ArgumentOutOfRangeException.ThrowIfNegative(c.Cycles);
                    Flush(raw, run);
                    int left = c.Cycles;
                    do
                    {
                        int take = Math.Min(left, MaxCarrierChunk);
                        WriteChunk(raw, 0x0110, [(byte)take, (byte)(take >> 8)]);
                        left -= take;
                    }
                    while (left > 0);
                    break;
                case Silence s:
                    ArgumentOutOfRangeException.ThrowIfNegative(s.Cycles);
                    Flush(raw, run);
                    WriteChunk(raw, 0x0116, BitConverter.GetBytes((float)(s.Cycles / (double)TapeTiming.CpuHz)));
                    break;
                default:
                    throw new ArgumentException($"Not a tape event: {e}.", nameof(events));
            }
        }

        Flush(raw, run);

        var file = new MemoryStream();
        using (var gzip = new GZipStream(file, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(raw.GetBuffer(), 0, (int)raw.Length);
        }

        return file.ToArray();
    }

    private static void Flush(MemoryStream to, List<byte> run)
    {
        if (run.Count > 0)
        {
            WriteChunk(to, 0x0100, [.. run]);
            run.Clear();
        }
    }

    private static void WriteChunk(MemoryStream to, ushort id, ReadOnlySpan<byte> data)
    {
        to.WriteByte((byte)id);
        to.WriteByte((byte)(id >> 8));
        to.Write(BitConverter.GetBytes((uint)data.Length));
        to.Write(data);
    }
}
