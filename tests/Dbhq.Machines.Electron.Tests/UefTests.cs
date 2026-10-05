using System.Diagnostics;
using System.IO.Compression;
using Dbhq.Machines.Electron.Tape;
using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The UEF reader and writer (<c>tape.md</c> s1, s6). Files are built by hand from the sheet's
/// layout, so no test reads what the writer wrote with the writer's own idea of the format, except
/// the round trip, which says so.
/// </summary>
public class UefTests(ITestOutputHelper output)
{
    // "UEF File!" and its zero, then the minor and the major version byte (tape.md s1).
    private static byte[] Header(byte minor = 10, byte major = 0) => [.. "UEF File!\0"u8, minor, major];

    private static byte[] Chunk(ushort id, params byte[] data) =>
        [(byte)id, (byte)(id >> 8), .. BitConverter.GetBytes((uint)data.Length), .. data];

    private static byte[] Uef(params byte[][] chunks) => [.. Header(), .. chunks.SelectMany(c => c)];

    private static byte[] Gzip(byte[] bytes)
    {
        var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gz.Write(bytes);
        }

        return output.ToArray();
    }

    private static byte[] Inflate(byte[] file)
    {
        using var gz = new GZipStream(new MemoryStream(file), CompressionMode.Decompress);
        var output = new MemoryStream();
        gz.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Float(float value) => BitConverter.GetBytes(value);

    private static IEnumerable<TapeEvent> Bytes(params byte[] values) => values.Select(v => (TapeEvent)new TapeByte(v));

    private static UefException Refused(byte[] file) => Assert.Throws<UefException>(() => UefReader.Read(file));

    // ---- Writing

    [Fact]
    public void ARoundTripGivesTheEventsBack()
    {
        // The one test that reads the writer's output with the reader: the writer and reader agree.
        TapeEvent[] events = [new Carrier(1000), new TapeByte(0x2A), new TapeByte(0x54), new Silence(1_000_000), new Carrier(200)];

        byte[] file = UefWriter.Write(events, "round trip test");

        Assert.Equal(events, UefReader.Read(file));
    }

    [Fact]
    public void TheFileIsGzipAndInflatesToTheHeaderWithVersionZeroPointTen()
    {
        byte[] file = UefWriter.Write([new Carrier(1)], "test");

        Assert.Equal([0x1F, 0x8B], file[..2]);
        byte[] inflated = Inflate(file);
        // "UEF File!" and its zero, as the sheet writes them: 55 45 46 20 46 69 6C 65 21 00.
        Assert.Equal(Convert.FromHexString("5545462046696C652100"), inflated[..10]);
        Assert.Equal(0x0A, inflated[10]); // minor 10
        Assert.Equal(0x00, inflated[11]); // major 0
    }

    [Fact]
    public void TheWritersChunksAreOriginThenTargetMachineThenDataAndCarrierAsTheSheetLaysThemOut()
    {
        byte[] inflated = Inflate(UefWriter.Write([new Carrier(0x0102), new TapeByte(0x2A), new TapeByte(0x54), new TapeByte(0x45)], "orig"));

        List<(ushort Id, byte[] Data)> chunks = Chunks(inflated);

        Assert.Equal([(ushort)0x0000, (ushort)0x0005, (ushort)0x0110, (ushort)0x0100], chunks.Select(c => c.Id));
        Assert.Equal("orig"u8.ToArray(), chunks[0].Data);
        Assert.Equal([0x10], chunks[1].Data); // an Electron, no keyboard preference
        Assert.Equal([0x02, 0x01], chunks[2].Data); // 2 bytes, little endian
        Assert.Equal([0x2A, 0x54, 0x45], chunks[3].Data);
        // The length field of the data chunk is the byte count, four bytes, little endian.
        int at = inflated.Length - 3 - 4;
        Assert.Equal([3, 0, 0, 0], inflated[at..(at + 4)]);
    }

    [Fact]
    public void ConsecutiveBytesShareOneDataChunkAndNeverTheOtherKindsOfEvent()
    {
        byte[] inflated = Inflate(UefWriter.Write(
            [.. Bytes(1, 2), new Carrier(5), .. Bytes(3), new Silence(2_000_000), .. Bytes(4, 5)], "x"));

        List<(ushort Id, byte[] Data)> chunks = Chunks(inflated);

        Assert.Equal([(ushort)0, 5, 0x0100, 0x0110, 0x0100, 0x0116, 0x0100], chunks.Select(c => c.Id));
        Assert.Equal([4, 5], chunks[^1].Data);
        Assert.Equal(Float(1.0f), chunks[5].Data); // two million cycles of the 2 MHz clock: one second
    }

    [Fact]
    public void ACarrierLongerThanTheChunkHoldsIsSplitAndReadsBackAsOne()
    {
        byte[] file = UefWriter.Write([new Carrier(70_000)], "x");

        Assert.Equal(2, Chunks(Inflate(file)).Count(c => c.Id == 0x0110));
        Assert.Equal([new Carrier(70_000)], UefReader.Read(file));
    }

    [Fact]
    public void TheWriterNeverWritesTheDummyByteChunk()
    {
        byte[] inflated = Inflate(UefWriter.Write([new Carrier(10), new TapeByte(0xAA), new Carrier(10)], "x"));
        Assert.DoesNotContain(Chunks(inflated), c => c.Id == 0x0111);
    }

    [Fact]
    public void AnEmptyTapeWritesAndReadsBackAsEmpty() =>
        Assert.Empty(UefReader.Read(UefWriter.Write([], "nothing on it")));

    [Theory]
    [InlineData(-1)]
    public void AnEventThatCannotBeWrittenIsRefused(int cycles)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UefWriter.Write([new Carrier(cycles)], "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => UefWriter.Write([new Silence(cycles)], "x"));
    }

    private static List<(ushort Id, byte[] Data)> Chunks(byte[] inflated)
    {
        var chunks = new List<(ushort, byte[])>();
        int at = 12;
        while (at < inflated.Length)
        {
            ushort id = (ushort)(inflated[at] | (inflated[at + 1] << 8));
            int length = BitConverter.ToInt32(inflated, at + 2);
            chunks.Add((id, inflated[(at + 6)..(at + 6 + length)]));
            at += 6 + length;
        }

        return chunks;
    }

    // ---- Reading what it uses

    [Fact]
    public void DataChunksBecomeABytePerByteAndCarrierBecomesCarrier()
    {
        byte[] file = Uef(Chunk(0x0110, 0xE8, 0x03), Chunk(0x0100, 0x2A, 0x54), Chunk(0x0100, 0x45));

        Assert.Equal<TapeEvent>([new Carrier(1000), new TapeByte(0x2A), new TapeByte(0x54), new TapeByte(0x45)], UefReader.Read(file));
    }

    [Fact]
    public void APlainUncompressedUefReadsTheSameAsTheCompressedOne()
    {
        byte[] plain = Uef(Chunk(0x0110, 0x0A, 0x00), Chunk(0x0100, 1, 2, 3));

        Assert.Equal(UefReader.Read(plain), UefReader.Read(Gzip(plain)));
        Assert.Equal<TapeEvent>([new Carrier(10), new TapeByte(1), new TapeByte(2), new TapeByte(3)], UefReader.Read(plain));
    }

    [Fact]
    public void WhatItDoesNotUseIsSkippedByItsLength()
    {
        byte[] bytes = Uef(
            Chunk(0x0000, "origin text"u8.ToArray()),
            Chunk(0x0005, 0x10),
            Chunk(0x0100, 1, 2),
            Chunk(0x0003, "an inlay scan"u8.ToArray()),
            Chunk(0x0120, "position marker"u8.ToArray()),
            Chunk(0x7777, 0x00, 0x01), // an id nobody has heard of
            Chunk(0x0101, 9, 9, 9), // multiplexed copy of a data chunk: not read
            Chunk(0x0114, 1, 2, 3, 4, 5), // security cycles
            Chunk(0x0115, 0x5A, 0x00), // phase change
            Chunk(0x0100, 3));

        Assert.Equal<TapeEvent>([new TapeByte(1), new TapeByte(2), new TapeByte(3)], UefReader.Read(bytes));
    }

    [Fact]
    public void AnIntegerGapIsSilenceInCpuCycles()
    {
        // The sheet gives the gap as 1 / (2 x n x base frequency), which falls as n grows and so
        // cannot be a length. S2 itself is dated draft 28 and has the same words. This reader takes
        // n half cycles of the base frequency, n / (2 x 1200) seconds, as the text "a rest length
        // counted relative to the base frequency" says: 2,400 of them are one second. At the 2 MHz
        // clock, one second is 2,000,000 CPU cycles.
        byte[] file = Uef(Chunk(0x0112, 0x60, 0x09)); // 2400

        Assert.Equal<TapeEvent>([new Silence(2_000_000)], UefReader.Read(file));
    }

    [Fact]
    public void AFloatingPointGapIsSilenceInCpuCycles()
    {
        Assert.Equal<TapeEvent>([new Silence(1_000_000)], UefReader.Read(Uef(Chunk(0x0116, Float(0.5f)))));
        Assert.Equal<TapeEvent>([new Silence(0)], UefReader.Read(Uef(Chunk(0x0116, Float(0f)))));
    }

    [Fact]
    public void ACarrierWithADummyByteIsCarrierTheDummyByteAndCarrier()
    {
        // The ten framed bits are 0 0 1 0 1 0 1 0 1 1 (tape.md s1a). The first is the start bit,
        // the last the stop bit, and the eight between are the data, least significant bit first:
        // 0 1 0 1 0 1 0 1, which is bit 1, 3, 5 and 7 set, 0b10101010 = $AA. (S2 says "always
        // $AA". The sheet's "$55-pattern" was the bit pattern read in the order it goes down the
        // wire as if it were the value: $55 would go out 1 0 1 0 1 0 1 0.)
        byte[] file = Uef(Chunk(0x0111, 0x64, 0x00, 0xC8, 0x00)); // 100 before, 200 after

        Assert.Equal<TapeEvent>([new Carrier(100), new TapeByte(0xAA), new Carrier(200)], UefReader.Read(file));
    }

    [Fact]
    public void TheDummyBytesBitsAreTheSheetsTenBitsAndNotTheirMirrorImage()
    {
        bool[] wire = [false, .. Enumerable.Range(0, 8).Select(i => (0xAA >> i & 1) != 0), true];
        Assert.Equal([0, 0, 1, 0, 1, 0, 1, 0, 1, 1], wire.Select(b => b ? 1 : 0));
    }

    [Fact]
    public void CarrierChunksRunTogetherAreOneCarrier() =>
        Assert.Equal<TapeEvent>([new Carrier(300)], UefReader.Read(Uef(Chunk(0x0110, 100, 0), Chunk(0x0110, 200, 0))));

    [Fact]
    public void OneChunkOfAMillionBytesIsFastAndReadsEveryByte()
    {
        byte[] data = new byte[1_000_000];
        new Random(1).NextBytes(data);
        byte[] file = Gzip(Uef(Chunk(0x0100, data)));

        // Only the read is timed, on this thread. The limit is wide so that a loaded machine does
        // not fail it: a reader that is quadratic in the chunk's length would take hours, not seconds.
        var clock = Stopwatch.StartNew();
        IReadOnlyList<TapeEvent> events = UefReader.Read(file);
        clock.Stop();
        output.WriteLine($"a data chunk of a million bytes read in {clock.ElapsedMilliseconds} ms");
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"a data chunk of a million bytes took {clock.Elapsed}");

        Assert.Equal(1_000_000, events.Count);
        Assert.Equal(data, events.Select(e => ((TapeByte)e).Value).ToArray());
    }

    // ---- Disc, ROM and snapshot chunks are not tape

    [Fact]
    public void DiscAndRomChunksAreSkippedAndAFileOfOnlyThoseIsAnEmptyTape()
    {
        byte[] file = Uef(Chunk(0x0200, 1, 2, 3), Chunk(0x0300, 4, 5), Chunk(0x0201, 6));

        Assert.Empty(UefReader.Read(file));
    }

    // ---- What it will not load: each names the chunk (tape.md s6)

    [Fact]
    public void Baud300IsRefusedNamingTheChunkAndTheValue()
    {
        UefException e = Refused(Uef(Chunk(0x0117, 0x2C, 0x01))); // 300
        Assert.Contains("0117", e.Message);
        Assert.Contains("300", e.Message);
    }

    [Fact]
    public void Baud1200IsCarriedOn() =>
        Assert.Equal<TapeEvent>([new TapeByte(7)], UefReader.Read(Uef(Chunk(0x0117, 0xB0, 0x04), Chunk(0x0100, 7)))); // 1200

    [Fact]
    public void AnythingButThe300Or1200DataEncodingIsRefusedToo()
    {
        UefException e = Refused(Uef(Chunk(0x0117, 0x01, 0x00)));
        Assert.Contains("0117", e.Message);
        Assert.Contains("1", e.Message);
    }

    [Fact]
    public void ADefinedTapeFormatWithDataIsRefusedNamingTheChunk()
    {
        UefException e = Refused(Uef(Chunk(0x0104, 8, (byte)'N', 1, 0x41, 0x42)));
        Assert.Contains("0104", e.Message);
    }

    [Fact]
    public void ExplicitTapeDataIsRefusedNamingTheChunk()
    {
        UefException e = Refused(Uef(Chunk(0x0102, 0, 0xFF, 0x00, 0xFF)));
        Assert.Contains("0102", e.Message);
    }

    [Fact]
    public void AChunkOfZeroLengthForAFormatItDoesNotLoadIsNothingToRefuse()
    {
        Assert.Empty(UefReader.Read(Uef(Chunk(0x0102), Chunk(0x0104))));
    }

    [Fact]
    public void ABaseFrequencyOtherThan1200IsRefusedNamingTheChunk()
    {
        Assert.Equal([0x00, 0x00, 0x96, 0x44], Float(1200f)); // 1200.0 as an IEEE single (tape.md s1a)

        UefException e = Refused(Uef(Chunk(0x0113, Float(1500f))));

        Assert.Contains("0113", e.Message);
        Assert.Contains("1500", e.Message);
    }

    [Fact]
    public void ABaseFrequencyOf1200IsCarriedOn() =>
        Assert.Equal<TapeEvent>([new TapeByte(9)], UefReader.Read(Uef(Chunk(0x0113, 0x00, 0x00, 0x96, 0x44), Chunk(0x0100, 9))));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(255)]
    public void AMajorVersionAboveZeroIsRefused(byte major)
    {
        byte[] file = [.. Header(10, major), .. Chunk(0x0100, 1)];

        UefException e = Refused(file);

        Assert.Contains("major version", e.Message);
        Assert.Contains(major.ToString(), e.Message);
    }

    [Fact]
    public void AnyMinorVersionIsRead() =>
        Assert.Equal<TapeEvent>([new TapeByte(1)], UefReader.Read([.. Header(5, 0), .. Chunk(0x0100, 1)]));

    // ---- Damaged input: each throws UefException naming the problem, and nothing else

    [Fact]
    public void AZeroLengthFileIsRefused() => Assert.Contains("empty", Refused([]).Message);

    [Fact]
    public void AFileTooShortForTheHeaderIsRefused()
    {
        Assert.Contains("header", Refused("UEF"u8.ToArray()).Message);
        Assert.Contains("header", Refused([.. "UEF File!\0"u8]).Message); // no version bytes
        Assert.Contains("header", Refused([.. "UEF File!\0"u8, 10]).Message); // no major version
    }

    [Fact]
    public void TheMagicWithoutItsZeroIsRefused()
    {
        byte[] file = [.. "UEF File!"u8, 10, 0, .. Chunk(0x0100, 1)];

        Assert.Contains("UEF File!", Refused(file).Message);
    }

    [Fact]
    public void NotAUefIsRefusedSayingSo() =>
        Assert.Contains("not a UEF", Refused("This is a text file, not a tape image."u8.ToArray()).Message);

    [Fact]
    public void AHeaderAndNoChunksIsRefusedAsHavingNothingOnIt() =>
        Assert.Contains("no chunks", Refused(Header()).Message);

    [Fact]
    public void AGzipStreamCutShortIsRefusedSayingTheTapeIsCutShort()
    {
        byte[] data = new byte[50_000];
        new Random(2).NextBytes(data);
        byte[] file = Gzip(Uef(Chunk(0x0100, data)));

        UefException e = Refused(file[..(file.Length / 2)]);

        Assert.Contains("cut short", e.Message);
    }

    [Theory]
    [InlineData(1)]  // the last byte of the trailer
    [InlineData(8)]  // the whole trailer
    [InlineData(9)]  // the trailer and a byte
    [InlineData(100)]
    public void AGzipStreamCutAnywhereIsRefused(int cutOff)
    {
        // The BCL's GZipStream says nothing when a stream ends early, so the reader checks the trailer.
        byte[] data = new byte[20_000];
        new Random(4).NextBytes(data);
        byte[] file = Gzip(Uef(Chunk(0x0100, data)));

        Assert.Contains("cut short", Refused(file[..^cutOff]).Message);
    }

    [Fact]
    public void AGzipStreamWithABitFlippedInItsMiddleIsRefused()
    {
        byte[] data = new byte[20_000];
        new Random(5).NextBytes(data);
        byte[] file = Gzip(Uef(Chunk(0x0100, data)));
        file[file.Length / 2] ^= 0x10;

        UefException e = Refused(file);

        Assert.Contains("damaged", e.Message);
    }

    [Fact]
    public void AGzipStreamThatIsNotGzipInsideIsRefused()
    {
        byte[] file = [0x1F, 0x8B, 0x08, 0x00, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

        Assert.Contains("gzip", Refused(file).Message);
    }

    [Fact]
    public void AGzipStreamThatInflatesWithoutAUefInsideIsRefused() =>
        Assert.Contains("UEF", Refused(Gzip("hello, this is not a tape"u8.ToArray())).Message);

    [Fact]
    public void AChunkWhoseLengthRunsPastTheEndOfTheFileIsRefused()
    {
        byte[] file = [.. Header(), 0x00, 0x01, 100, 0, 0, 0, 1, 2, 3];

        UefException e = Refused(file);

        Assert.Contains("0100", e.Message);
        Assert.Contains("100", e.Message);
        Assert.Contains("past the end", e.Message);
    }

    [Fact]
    public void AChunkOfLengthFfffffffIsRefusedWithoutTryingToAllocateIt()
    {
        byte[] file = [.. Header(), 0x00, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 1, 2, 3];

        long before = GC.GetAllocatedBytesForCurrentThread();
        UefException e = Refused(file);
        long used = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Contains("4294967295", e.Message);
        Assert.True(used < 100_000, $"{used} bytes allocated");
    }

    [Fact]
    public void AChunkHeaderCutShortIsRefused()
    {
        byte[] file = [.. Header(), .. Chunk(0x0100, 1), 0x00, 0x01, 0x05];

        Assert.Contains("chunk header", Refused(file).Message);
    }

    [Fact]
    public void ACarrierChunkOfTheWrongLengthIsRefusedNamingTheChunk()
    {
        Assert.Contains("0110", Refused(Uef(Chunk(0x0110, 1, 2, 3))).Message);
        Assert.Contains("0111", Refused(Uef(Chunk(0x0111, 1, 2))).Message);
        Assert.Contains("0112", Refused(Uef(Chunk(0x0112, 1))).Message);
        Assert.Contains("0113", Refused(Uef(Chunk(0x0113, 1, 2))).Message);
        Assert.Contains("0116", Refused(Uef(Chunk(0x0116, 1, 2))).Message);
        Assert.Contains("0117", Refused(Uef(Chunk(0x0117, 1))).Message);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(-1f)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(1e9f)]
    public void AFloatingPointGapThatIsNotALengthOfTimeIsRefused(float seconds) =>
        Assert.Contains("0116", Refused(Uef(Chunk(0x0116, Float(seconds)))).Message);

    [Fact]
    public void ABaseFrequencyThatIsNotANumberIsRefused() =>
        Assert.Contains("0113", Refused(Uef(Chunk(0x0113, Float(float.NaN)))).Message);

    [Fact]
    public void AGzipBombIsRefusedBeyondTheLimitRatherThanInflated()
    {
        // 40 MB of zeros is about 40 KB of gzip. The limit is 4 MiB (UefReader.MaxInflatedBytes).
        byte[] bomb = Gzip(new byte[40_000_000]);
        Assert.True(bomb.Length < 100_000);

        long before = GC.GetAllocatedBytesForCurrentThread();
        UefException e = Refused(bomb);
        long used = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Contains("inflates to more than 4 MiB", e.Message);
        Assert.True(used < 4 * UefReader.MaxInflatedBytes, $"{used} bytes allocated");
    }

    [Fact]
    public void TheLimitIsFourMiBWhichIsManyTimesATapeSide()
    {
        // Half an hour of tape at 1200 baud, ten bits a byte, is 1,800 x 120 = 216,000 bytes
        // (tape.md s4). Four MiB is about nineteen of those, and a byte read costs a reference in
        // the list of events, so the most a file can ask of the browser's memory stays small.
        Assert.Equal(4 * 1024 * 1024, UefReader.MaxInflatedBytes);
        Assert.True(UefReader.MaxInflatedBytes > 19 * 216_000);
    }

    [Fact]
    public void AStreamThatInflatesJustPastTheLimitIsRefusedAndOneJustUnderIsRead()
    {
        int under = UefReader.MaxInflatedBytes - 18;
        Assert.Equal(under, UefReader.Read(Gzip(ZerosUef(under))).Count);
        Assert.Contains("inflates to more than 4 MiB", Refused(Gzip(ZerosUef(under + 1))).Message);
    }

    [Fact]
    public void APlainFileLargerThanTheLimitIsRefusedToo()
    {
        // Not gzip, so nothing is inflated, but it would still be a list of millions of events.
        int under = UefReader.MaxInflatedBytes - 18;
        Assert.Equal(under, UefReader.Read(ZerosUef(under)).Count);
        Assert.Contains("more than 4 MiB", Refused(ZerosUef(under + 1)).Message);
    }

    /// <summary>
    /// A UEF of one data chunk of <paramref name="length"/> zero bytes, built in place: the header's
    /// 12 bytes and the chunk's 6 make a file of <paramref name="length"/> + 18.
    /// </summary>
    private static byte[] ZerosUef(int length)
    {
        byte[] file = new byte[18 + length];
        Header().CopyTo(file, 0);
        Chunk(0x0100).CopyTo(file, 12);
        BitConverter.GetBytes((uint)length).CopyTo(file, 14);
        return file;
    }

    [Fact]
    public void NoDamageToAValidFileThrowsAnythingButUefException()
    {
        // Flip and cut a real file at random, 3,000 times, fixed seed. Each result is a tape or a
        // UefException; none hangs and none is any other exception.
        byte[] good = [.. Header(), .. Chunk(0x0000, "o"u8.ToArray()), .. Chunk(0x0110, 10, 0), .. Chunk(0x0100, 1, 2, 3, 4),
                       .. Chunk(0x0111, 1, 0, 2, 0), .. Chunk(0x0112, 5, 0), .. Chunk(0x0113, 0, 0, 0x96, 0x44),
                       .. Chunk(0x0116, Float(0.1f)), .. Chunk(0x0117, 0xB0, 0x04)];
        var random = new Random(3);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 3000; i++)
        {
            byte[] bad = (byte[])good.Clone();
            for (int k = random.Next(1, 4); k > 0; k--)
            {
                bad[random.Next(bad.Length)] = (byte)random.Next(256);
            }

            if (i % 3 == 0)
            {
                bad = bad[..random.Next(bad.Length + 1)];
            }

            try
            {
                UefReader.Read(i % 2 == 0 ? bad : Gzip(bad));
            }
            catch (UefException)
            {
            }
        }

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20));
    }
}
