using Xunit;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Electron.Tests;

public class RomTests
{
    [Fact]
    public void TheOperatingSystemIsSixteenKilobytesAndMatchesItsPin() =>
        Assert.Equal(16384, RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256).Length);

    [Fact]
    public void TheOperatingSystemIsOs100AndItsVectorsAreTheSheets()
    {
        byte[] os = RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256);

        // ula.md s1a: vectors at file offset $3FFA: NMI $0D00, RESET $D8D2, IRQ $DAE7
        Assert.Equal(new byte[] { 0x00, 0x0D, 0xD2, 0xD8, 0xE7, 0xDA }, os[0x3FFA..0x4000]);

        // ula.md s10a: "OS 1.00" after BRK and error number $F7 at $E5F6 (offset $25F6)
        Assert.Contains("OS 1.00", System.Text.Encoding.ASCII.GetString(os));
    }

    [Fact]
    public void BasicIsTheBbcsFileSoItIsKeptOnce() =>
        Assert.Equal("45bd55dc0f6f0f8f1fe9e2481de7def206565eec8f600ba3068b849ca4132079", Pins.BbcBasicSha256);

    [Fact]
    public void TheRomsCheckedTogetherBuildAnElectronRoms()
    {
        var roms = new ElectronRoms(
            RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256),
            RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256));
        Assert.Equal(ElectronRoms.RomSize, roms.Os.Length);
        Assert.Equal(ElectronRoms.RomSize, roms.Basic.Length);
        Assert.Throws<ArgumentException>(() => new ElectronRoms(new byte[10], roms.Basic));
    }
}
