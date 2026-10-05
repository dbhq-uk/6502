using Dbhq.Machines.Electron.Tape;
using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The block parser and encoder (<c>tape.md</c> s2). The expected bytes of the worked block are
/// from the sheet's section 2b and its CRC rule, not from the encoder under test.
/// </summary>
public class TapeBlocksTests
{
    // tape.md s2b, all 30 bytes.
    private const string WorkedHex = "2a5445535400000900000009000000000300800309000009d84142433994";

    private static readonly TapeBlock Worked = new("TEST", 0x900, 0x900, 0, 0x80, 0x903, "ABC"u8.ToArray());

    private static byte[] WorkedBytes() => Convert.FromHexString(WorkedHex);

    [Fact]
    public void EncodeGivesTheSheetsThirtyBytesExactly()
    {
        byte[] bytes = TapeBlocks.Encode(Worked);
        Assert.Equal(30, bytes.Length);
        Assert.Equal(WorkedHex, Convert.ToHexString(bytes).ToLowerInvariant());
    }

    [Fact]
    public void ParseGivesTheBlockBack()
    {
        TapeBlock block = Assert.Single(TapeBlocks.Parse(WorkedBytes()));
        Assert.Equal("TEST", block.Name);
        Assert.Equal(0x900u, block.Load);
        Assert.Equal(0x900u, block.Exec);
        Assert.Equal(0, block.Number);
        Assert.Equal(0x80, block.Flag);
        Assert.Equal(0x903u, block.Next);
        Assert.Equal("ABC"u8.ToArray(), block.Data);
        Assert.Equal(Worked, block); // a block is equal by its fields, its data included
    }

    [Fact]
    public void ParseAcceptsAnyEnumerableOfBytes() =>
        Assert.Equal(Worked, Assert.Single(TapeBlocks.Parse(WorkedBytes().Select(b => b))));

    [Fact]
    public void NoBytesAreNoBlocks() => Assert.Empty(TapeBlocks.Parse([]));

    [Fact]
    public void BlocksBackToBackAreParsedInOrder()
    {
        var first = new TapeBlock("PROG", 0x1900, 0x8023, 0, 0x00, 0, [.. Enumerable.Range(0, 256).Select(i => (byte)i)]);
        var second = new TapeBlock("PROG", 0x1900, 0x8023, 1, 0x80, 0, [0x42]);
        byte[] stream = [.. TapeBlocks.Encode(first), .. TapeBlocks.Encode(second)];

        IReadOnlyList<TapeBlock> blocks = TapeBlocks.Parse(stream);

        Assert.Equal([first, second], blocks);
    }

    [Fact]
    public void AFlippedDataByteIsADataCrcErrorNamingTheBlock()
    {
        byte[] bytes = WorkedBytes();
        bytes[26] ^= 0x01; // the B of ABC

        TapeFormatException e = Assert.Throws<TapeFormatException>(() => TapeBlocks.Parse(bytes));

        Assert.Contains("data CRC", e.Message);
        Assert.Contains("TEST", e.Message);
        Assert.Contains("offset 0", e.Message);
    }

    [Fact]
    public void AFlippedDataCrcByteIsAlsoADataCrcError()
    {
        byte[] bytes = WorkedBytes();
        bytes[29] ^= 0x80;
        Assert.Contains("data CRC", Assert.Throws<TapeFormatException>(() => TapeBlocks.Parse(bytes)).Message);
    }

    [Theory]
    [InlineData(1)]  // the first byte of the name
    [InlineData(6)]  // the load address
    [InlineData(18)] // the flag
    [InlineData(23)] // the header CRC itself
    public void AFlippedHeaderByteIsAHeaderCrcErrorNamingTheBlock(int at)
    {
        byte[] bytes = WorkedBytes();
        bytes[at] ^= 0x01;

        TapeFormatException e = Assert.Throws<TapeFormatException>(() => TapeBlocks.Parse(bytes));

        Assert.Contains("header CRC", e.Message);
        Assert.Contains("offset 0", e.Message);
    }

    [Fact]
    public void AStreamNotStartingWithTheSyncByteIsRefusedAtItsOffset()
    {
        byte[] bytes = [.. WorkedBytes(), 0x55];

        TapeFormatException e = Assert.Throws<TapeFormatException>(() => TapeBlocks.Parse(bytes));

        Assert.Contains("sync", e.Message);
        Assert.Contains("offset 30", e.Message);
    }

    [Theory]
    [InlineData(3, "name")]    // in the name
    [InlineData(12, "header")] // in the addresses
    [InlineData(24, "header")] // between the two bytes of the header CRC
    public void AStreamThatEndsInTheHeaderIsReportedWithTheOffsetItStopsAt(int length, string where)
    {
        byte[] bytes = WorkedBytes()[..length];

        TapeFormatException e = Assert.Throws<TapeFormatException>(() => TapeBlocks.Parse(bytes));

        Assert.Contains(where, e.Message);
        Assert.Contains($"offset {length}", e.Message);
    }

    [Theory]
    [InlineData(25)] // before the data
    [InlineData(27)] // in the data
    [InlineData(29)] // between the two bytes of the data CRC
    public void AStreamThatEndsInTheDataIsReportedWithTheOffsetItStopsAt(int length)
    {
        byte[] bytes = WorkedBytes()[..length];

        TapeFormatException e = Assert.Throws<TapeFormatException>(() => TapeBlocks.Parse(bytes));

        Assert.Contains("data", e.Message);
        Assert.Contains($"offset {length}", e.Message);
    }

    [Fact]
    public void ANameWithNoTerminatorInElevenBytesIsRefused()
    {
        // Ten characters and then the terminator is the most the OS writes (tape.md s7 item 6).
        byte[] bytes = [0x2A, .. "ABCDEFGHIJK"u8, .. new byte[30]];
        Assert.Contains("name", Assert.Throws<TapeFormatException>(() => TapeBlocks.Parse(bytes)).Message);
    }

    [Fact]
    public void ATenCharacterNameRoundTrips()
    {
        var block = Worked with { Name = "ABCDEFGHIJ" };
        Assert.Equal(block, Assert.Single(TapeBlocks.Parse(TapeBlocks.Encode(block))));
    }

    [Fact]
    public void ABlockWithFlagBit6HasNoDataCrc()
    {
        // tape.md s2a: bit 6 says the block has no data, and then there is no data CRC.
        var empty = new TapeBlock("TITLE", 0, 0, 0, 0xC0, 0, []);

        byte[] bytes = TapeBlocks.Encode(empty);

        Assert.Equal(1 + 6 + 17 + 2, bytes.Length); // sync, name and zero, the fixed fields, the header CRC: nothing after
        Assert.Equal(empty, Assert.Single(TapeBlocks.Parse(bytes)));
    }

    [Fact]
    public void ABlockOfLengthZeroHasNoDataCrcEitherWayOfFlag()
    {
        var empty = new TapeBlock("TITLE", 0, 0, 0, 0x80, 0, []);
        Assert.Equal(1 + 6 + 17 + 2, TapeBlocks.Encode(empty).Length);
        Assert.Equal(empty, Assert.Single(TapeBlocks.Parse(TapeBlocks.Encode(empty))));
    }

    [Fact]
    public void WithFlagBit6SetNoDataCrcIsReadOrChecked()
    {
        // The data's own CRC is left out, so a stream with a bad one in that place is not an error:
        // the bytes after the data are the next block's.
        var flagged = new TapeBlock("X", 0, 0, 0, 0x40, 0, [1, 2, 3]);
        byte[] bytes = [.. TapeBlocks.Encode(flagged), .. TapeBlocks.Encode(Worked)];

        Assert.Equal([flagged, Worked], TapeBlocks.Parse(bytes));
    }

    [Fact]
    public void TheNextFileAddressIsKeptAndNotChecked()
    {
        // The OS writes 0; a reader must not depend on it (tape.md s2a).
        var odd = Worked with { Next = 0xDEADBEEF };
        Assert.Equal(odd, Assert.Single(TapeBlocks.Parse(TapeBlocks.Encode(odd))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("A\0B")]
    [InlineData("A€B")]
    public void EncodeRefusesANameThatCannotBeWritten(string name) =>
        Assert.Throws<ArgumentException>(() => TapeBlocks.Encode(Worked with { Name = name }));

    [Fact]
    public void EncodeRefusesMoreDataThanTheLengthFieldHolds() =>
        Assert.Throws<ArgumentException>(() => TapeBlocks.Encode(Worked with { Data = new byte[65_536] }));
}
