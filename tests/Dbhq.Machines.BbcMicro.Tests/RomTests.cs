using Xunit;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.BbcMicro.Tests;

public class RomTests
{
    [Theory]
    [InlineData("os")]
    [InlineData("basic")]
    [InlineData("dfs")]
    public void EachRomIsSixteenKilobytesAndMatchesItsPin(string which)
    {
        (string path, string sha) = which switch
        {
            "os" => (Pins.BbcOsPath, Pins.BbcOsSha256),
            "basic" => (Pins.BbcBasicPath, Pins.BbcBasicSha256),
            _ => (Pins.BbcDfsPath, Pins.BbcDfsSha256),
        };
        Assert.Equal(16384, RepoPaths.ReadChecked(path, sha).Length);
    }

    [Fact]
    public void TheOperatingSystemIsMos120()
    {
        byte[] os = RepoPaths.ReadChecked(Pins.BbcOsPath, Pins.BbcOsSha256);

        // "OS 1.20" at CPU $E825 (file offset $2825), after BRK and error number $F7 (bus.md s4a).
        Assert.Equal("OS 1.20", System.Text.Encoding.ASCII.GetString(os, 0x2825, 7));
        Assert.Equal(0xCD, os[0x3FFC]);
        Assert.Equal(0xD9, os[0x3FFD]); // reset vector $D9CD
    }

    [Fact]
    public void BbcRomsAcceptsThePinnedRomsAndRefusesAnyOtherSize()
    {
        byte[] os = RepoPaths.ReadChecked(Pins.BbcOsPath, Pins.BbcOsSha256);
        byte[] basic = RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256);
        byte[] dfs = RepoPaths.ReadChecked(Pins.BbcDfsPath, Pins.BbcDfsSha256);

        var roms = new BbcRoms(os, basic, dfs);
        Assert.Same(os, roms.Os);
        Assert.Equal(7, new BbcOptions().StartupMode);

        Assert.Throws<ArgumentException>(() => new BbcRoms(os[..16383], basic, dfs));
        Assert.Throws<ArgumentException>(() => new BbcRoms(os, new byte[16385], dfs));
        Assert.Throws<ArgumentNullException>(() => new BbcRoms(os, basic, null!));
    }
}
