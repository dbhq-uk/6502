using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The <c>.ssd</c> and <c>.dsd</c> images, from the fact sheet <c>disc.md</c> sections 4 and 5:
/// sizes, the empty catalogue, where each sector sits, and short images.
/// </summary>
public class DiscImageTests
{
    [Fact]
    public void ABlankFortyTrackDiscIsTheSheetsEmptyCatalogue()
    {
        // disc.md s5b and s7: 102,400 bytes; sector 0 all zero; sector 1 zero but for the sector
        // count, 400 = $190, as $01 in byte 6 and $90 in byte 7.
        DiscImage disc = DiscImage.Blank(40, doubleSided: false);
        byte[] bytes = disc.ToBytes();

        Assert.Equal(102_400, bytes.Length);
        Assert.Equal(40, disc.Tracks);
        Assert.False(disc.DoubleSided);
        Assert.False(disc.ReadOnly);
        Assert.All(bytes.AsSpan(0, 256).ToArray(), b => Assert.Equal(0, b));
        byte[] sector1 = new byte[256];
        sector1[6] = 0x01;
        sector1[7] = 0x90;
        Assert.Equal(sector1, bytes.AsSpan(256, 256).ToArray());
        Assert.All(bytes.AsSpan(512).ToArray(), b => Assert.Equal(0, b));
    }

    [Fact]
    public void ABlankEightyTrackDiscCountsEightHundredSectors()
    {
        // 204,800 bytes, and 800 = $320: $03 and $20 (disc.md s5b).
        byte[] bytes = DiscImage.Blank(80, doubleSided: false).ToBytes();

        Assert.Equal(204_800, bytes.Length);
        Assert.Equal(0x03, bytes[0x106]);
        Assert.Equal(0x20, bytes[0x107]);
        Assert.Equal(2, bytes.Count(b => b != 0));
    }

    [Fact]
    public void ABlankDoubleSidedDiscHasACatalogueOnEachSide()
    {
        // Side 1's track 0 follows side 0's in an interleaved .dsd, so its catalogue is at 2,560
        // (disc.md s5b, "for a .dsd, build the same pair for each side at offset 0 and offset 2 560").
        byte[] bytes = DiscImage.Blank(40, doubleSided: true).ToBytes();

        Assert.Equal(204_800, bytes.Length);
        Assert.Equal([0x01, 0x90], bytes.AsSpan(0x106, 2).ToArray());
        Assert.Equal([0x01, 0x90], bytes.AsSpan(2560 + 0x106, 2).ToArray());
        Assert.Equal(4, bytes.Count(b => b != 0));
    }

    [Theory]
    [InlineData(false, 0, 0, 0, 0)]
    [InlineData(false, 0, 0, 9, 9 * 256)]
    [InlineData(false, 0, 1, 0, 2560)]
    [InlineData(false, 0, 39, 9, 102_400 - 256)]
    [InlineData(true, 0, 0, 0, 0)]
    [InlineData(true, 1, 0, 0, 2560)]
    [InlineData(true, 0, 1, 0, 5120)]
    [InlineData(true, 1, 1, 3, 7680 + (3 * 256))]
    [InlineData(true, 1, 39, 9, 204_800 - 256)]
    public void EachSectorSitsWhereTheSheetsLayoutPutsIt(bool doubleSided, int side, int track, int sector, int offset)
    {
        // .ssd: (track x 10 + sector) x 256. .dsd, track interleaved: ((track x 2 + side) x 10 +
        // sector) x 256 (disc.md s4). A sector written through the span is found at that offset.
        DiscImage disc = DiscImage.Blank(40, doubleSided);
        Span<byte> span = disc.Sector(side, track, sector);
        Assert.Equal(256, span.Length);
        span.Fill(0xA5);

        byte[] bytes = disc.ToBytes();
        Assert.All(bytes.AsSpan(offset, 256).ToArray(), b => Assert.Equal(0xA5, b));
        Assert.Equal(256, bytes.Count(b => b == 0xA5));
    }

    [Fact]
    public void ASectorOffTheDiscThrows()
    {
        DiscImage single = DiscImage.Blank(40, doubleSided: false);
        Assert.Throws<ArgumentOutOfRangeException>(() => single.Sector(1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => single.Sector(0, 40, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => single.Sector(0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => single.Sector(0, -1, 0));
        Assert.False(single.Contains(1, 0, 0));
        Assert.True(single.Contains(0, 39, 9));

        DiscImage dsd = DiscImage.Blank(80, doubleSided: true);
        Assert.True(dsd.Contains(1, 79, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => dsd.Sector(2, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => dsd.Sector(0, 80, 0));
    }

    [Theory]
    [InlineData(102_400, false, 40)]
    [InlineData(204_800, false, 80)]
    [InlineData(204_800, true, 40)]
    [InlineData(409_600, true, 80)]
    public void TheLengthAndTheSidesSayFortyOrEightyTracks(int length, bool doubleSided, int tracks)
    {
        // disc.md s4: 102,400 is a 40-track .ssd; 204,800 an 80-track .ssd or a 40-track .dsd, as
        // the extension says; 409,600 an 80-track .dsd.
        var data = new byte[length];
        new Random(length).NextBytes(data);
        DiscImage disc = DiscImage.FromBytes(data, doubleSided);

        Assert.Equal(tracks, disc.Tracks);
        Assert.Equal(doubleSided, disc.DoubleSided);
        Assert.Equal(data, disc.ToBytes());
    }

    [Theory]
    [InlineData(0, false, 40)]
    [InlineData(3 * 2560, false, 40)]
    [InlineData(102_401, false, 80)]
    [InlineData(150_000, true, 40)]
    [InlineData(300_000, true, 80)]
    public void AShortImageIsExtendedWithZeros(int length, bool doubleSided, int tracks)
    {
        // The sheet's recommendation for an image shorter than its disc, never tried on a real
        // drive, because no real disc is short (disc.md s4; docs/known-differences.md).
        var data = new byte[length];
        new Random(length + 1).NextBytes(data);
        byte[] bytes = DiscImage.FromBytes(data, doubleSided).ToBytes();

        Assert.Equal(tracks * 2560 * (doubleSided ? 2 : 1), bytes.Length);
        Assert.Equal(data, bytes.AsSpan(0, length).ToArray());
        Assert.All(bytes.AsSpan(length).ToArray(), b => Assert.Equal(0, b));
    }

    [Fact]
    public void AnImageLongerThanEightyTracksIsRefused()
    {
        Assert.Throws<ArgumentException>(() => DiscImage.FromBytes(new byte[204_801], doubleSided: false));
        Assert.Throws<ArgumentException>(() => DiscImage.FromBytes(new byte[409_601], doubleSided: true));
    }

    [Fact]
    public void TheImageIsCopiedInAndOut()
    {
        var data = new byte[102_400];
        DiscImage disc = DiscImage.FromBytes(data, doubleSided: false);
        data[0] = 1;
        byte[] first = disc.ToBytes();
        first[1] = 1;

        Assert.Equal(0, disc.Sector(0, 0, 0)[0]);
        Assert.Equal(0, disc.Sector(0, 0, 0)[1]);
    }
}
