using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The catalogue helper against the fact sheet's two worked examples (<c>disc.md</c> s5c and
/// s5d), before any disc test trusts it. The bytes are copied from the sheet.
/// </summary>
public class DfsCatalogueTests
{
    [Fact]
    public void WorkedExampleOneOneFileOnATitledDisc()
    {
        // s5c: *SAVE TEST 2000 2100 2000 2000 on the blank 40-track disc titled MYTITLE.
        byte[] sector0 = new byte[256], sector1 = new byte[256];
        byte[] title = [0x4D, 0x59, 0x54, 0x49, 0x54, 0x4C, 0x45, 0x00];
        byte[] name = [0x54, 0x45, 0x53, 0x54, 0x20, 0x20, 0x20, 0x24];
        byte[] header = [0x00, 0x00, 0x00, 0x00, 0x01, 0x08, 0x01, 0x90];
        byte[] info = [0x00, 0x20, 0x00, 0x20, 0x00, 0x01, 0x00, 0x02];
        title.CopyTo(sector0, 0);
        name.CopyTo(sector0, 8);
        header.CopyTo(sector1, 0);
        info.CopyTo(sector1, 8);

        DfsCatalogue catalogue = DfsCatalogue.Read(sector0, sector1);

        Assert.Equal("MYTITLE", catalogue.TitleText);
        Assert.Equal(1, catalogue.Cycle);
        Assert.Equal(0, catalogue.BootOption);
        Assert.Equal(400, catalogue.Sectors);
        DfsFile file = Assert.Single(catalogue.Files);
        Assert.Equal(new DfsFile('$', "TEST", false, 0x2000, 0x2000, 0x0100, 2), file);
        Assert.Equal("$.TEST        002000 002000 000100 002", file.InfoLine);
    }

    [Fact]
    public void WorkedExampleTwoHighBitsAndAStartSectorAboveTwoFiftyFive()
    {
        // s5d: FILL at sectors 2-338 (length $15100), then HELLO saved with load $FFFF1900, exec
        // $FFFF8023, length $0123, placed at sector $153 and listed first. *INFO * printed
        // "$.HELLO       FF1900 FF8023 000123 153".
        byte[] sector0 = new byte[256], sector1 = new byte[256];
        byte[] hello = [0x48, 0x45, 0x4C, 0x4C, 0x4F, 0x20, 0x20, 0x24];
        byte[] fill = [0x46, 0x49, 0x4C, 0x4C, 0x20, 0x20, 0x20, 0x24];
        byte[] helloInfo = [0x00, 0x19, 0x23, 0x80, 0x23, 0x01, 0xCD, 0x53];
        byte[] fillInfo = [0x00, 0x19, 0x00, 0x19, 0x00, 0x51, 0x10, 0x02];
        hello.CopyTo(sector0, 8);
        fill.CopyTo(sector0, 16);
        helloInfo.CopyTo(sector1, 8);
        fillInfo.CopyTo(sector1, 16);
        sector1[5] = 0x10;
        sector1[6] = 0x01;
        sector1[7] = 0x90;

        DfsCatalogue catalogue = DfsCatalogue.Read(sector0, sector1);

        Assert.Equal(2, catalogue.Files.Count);
        Assert.Equal(new DfsFile('$', "HELLO", false, 0x31900, 0x38023, 0x123, 0x153), catalogue.Files[0]);
        Assert.Equal(new DfsFile('$', "FILL", false, 0x1900, 0x1900, 0x15100, 2), catalogue.Files[1]);
        Assert.Equal("$.HELLO       FF1900 FF8023 000123 153", catalogue.Files[0].InfoLine);

        // A locked file sets bit 7 of the directory byte: A.HELLO locked is $C1 there (s5d).
        sector0[15] = 0xC1;
        DfsFile locked = DfsCatalogue.Read(sector0, sector1).Files[0];
        Assert.True(locked.Locked);
        Assert.Equal('A', locked.Directory);
        Assert.StartsWith("A.HELLO    L  ", locked.InfoLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ABlankDiscHasAnEmptyCatalogue()
    {
        DfsCatalogue catalogue = DfsCatalogue.Read(DiscImage.Blank(80, doubleSided: true), side: 1);
        Assert.Equal(new string('\0', 12), catalogue.Title);
        Assert.Equal(0, catalogue.Cycle);
        Assert.Equal(800, catalogue.Sectors);
        Assert.Empty(catalogue.Files);
    }
}
