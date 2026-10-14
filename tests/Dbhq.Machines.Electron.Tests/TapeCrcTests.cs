using Dbhq.Machines.Electron.Tape;
using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The tape CRC (<c>tape.md</c> s3), checked three ways, none of them by running the model: the
/// published check value, the Advanced User Guide's own example, and the sheet's block vector.
/// </summary>
public class TapeCrcTests
{
    [Fact]
    public void TheStandardCheckValue() => Assert.Equal(0x31C3, TapeCrc.Of("123456789"u8));

    [Fact]
    public void TheAugsExampleBlockDataCrc() =>
        // S1 prints &655D for its EQUW. EQUW lays the low byte down first, so that is the bytes
        // 5D 65 in memory, which is $5D65 read high byte first (tape.md s3).
        Assert.Equal(0x5D65, TapeCrc.Of("REM This is a very short text file.\r"u8));

    [Fact]
    public void TheWorkedBlockCrcs()
    {
        // tape.md s2b: the name to the next-file address, 22 bytes, then the data.
        byte[] header = Convert.FromHexString("54455354000009000000090000000003008003090000");
        Assert.Equal(22, header.Length);
        Assert.Equal(0x09D8, TapeCrc.Of(header));
        Assert.Equal(0x3994, TapeCrc.Of("ABC"u8));
    }

    [Fact]
    public void NothingHasTheInitialValue() => Assert.Equal(0, TapeCrc.Of([]));

    [Fact]
    public void DataFollowedByItsOwnCrcHighByteFirstGivesZero()
    {
        // The property of an XMODEM CRC that makes a receiver's check one comparison, and a
        // check on the byte order: with the low byte first this would not hold.
        byte[] data = "REM This is a very short text file.\r"u8.ToArray();
        ushort crc = TapeCrc.Of(data);
        Assert.Equal(0, TapeCrc.Of([.. data, (byte)(crc >> 8), (byte)crc]));
    }
}
