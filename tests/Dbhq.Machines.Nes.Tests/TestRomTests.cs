using System.Security.Cryptography;
using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The test ROMs come from the fork dbhq-uk/nes-test-roms at a pinned commit,
/// each checked against its hash in Pins.cs (AGENTS.md rule 3). Each later task
/// adds the ROMs it uses to Pins.NesTestRomHashes, and this test fetches them all.
/// </summary>
public class TestRomTests
{
    public static TheoryData<string> PinnedNames()
    {
        var names = new TheoryData<string>();
        foreach (string name in Pins.NesTestRomHashes.Keys.Order(StringComparer.Ordinal))
        {
            names.Add(name);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(PinnedNames))]
    public void EachPinnedRomMatchesItsHashAndIsAnInesFile(string pinnedName)
    {
        byte[] rom = NesTestRoms.Read(pinnedName);

        Assert.Equal(Pins.NesTestRomHashes[pinnedName], Convert.ToHexStringLower(SHA256.HashData(rom)));
        Assert.Equal(new byte[] { 0x4E, 0x45, 0x53, 0x1A }, rom[..4]);
    }

    [Fact]
    public void NestestAndTheCombinedPpuVblNmiRomArePinned()
    {
        Assert.Contains("other/nestest.nes", Pins.NesTestRomHashes.Keys);
        Assert.Contains("ppu_vbl_nmi/ppu_vbl_nmi.nes", Pins.NesTestRomHashes.Keys);
    }

    [Fact]
    public void NestestHasOneHashNotTwo()
    {
        // The core's own nestest test pins the same file; the two must never drift apart.
        Assert.Equal(Pins.NestestRomSha256, Pins.NesTestRomHashes["other/nestest.nes"]);
        Assert.Equal(Pins.NestestCommit, Pins.NesTestRomsCommit);
    }

    [Fact]
    public void ANameThatIsNotPinnedIsRefused()
    {
        var error = Assert.Throws<ArgumentException>(() => NesTestRoms.Read("other/not-pinned.nes"));
        Assert.Contains("other/not-pinned.nes", error.Message);
    }
}
