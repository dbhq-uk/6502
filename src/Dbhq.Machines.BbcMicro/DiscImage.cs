namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// A floppy disc as a file of sectors: the <c>.ssd</c> and <c>.dsd</c> images Acorn DFS discs are
/// kept in, ten 256-byte sectors a track, 40 or 80 tracks, one side or two.
/// </summary>
/// <remarks>
/// <para>
/// Built from the fact sheet <c>docs/bbc-micro/facts/disc.md</c> section 4. An image has no header,
/// no sector IDs and no gaps: it is the sectors' bytes in order. A <c>.ssd</c> holds one side,
/// track after track, so sector s of track t is at <c>(t x 10 + s) x 256</c>. A <c>.dsd</c> holds
/// two, and the sources disagree on the order (s4): this class takes the form the sheet calls the
/// most common and recommends, <b>track interleaved</b>, track 0 of side 0, then track 0 of side 1,
/// then track 1 of side 0 and so on, so the offset is <c>((t x 2 + side) x 10 + s) x 256</c>. The
/// other form, all of side 0 then all of side 1, is not read.
/// </para>
/// <para>
/// <b>40 or 80 tracks</b> is decided by the length, as s4 says, with the caller saying whether the
/// image is double sided (the file's extension): up to 40 tracks' worth of bytes is a 40-track
/// image, up to 80 tracks' worth an 80-track one. An image short enough to be 40 tracks whose
/// catalogue says 800 sectors (s4's second test) is an 80-track disc trimmed to the part in use,
/// as archives often keep them, and is taken as 80 tracks. <b>A short image is extended with zeros</b> to
/// the whole disc, which is the sheet's recommendation and has never been tried against a real
/// drive, because no real disc is short (<c>docs/known-differences.md</c>).
/// </para>
/// </remarks>
public sealed class DiscImage
{
    /// <summary>Bytes in a sector.</summary>
    public const int SectorSize = 256;

    /// <summary>Sectors in a track, numbered 0 to 9 (s4: the skew on a real disc is not stored).</summary>
    public const int SectorsPerTrack = 10;

    /// <summary>Bytes in one side of one track.</summary>
    public const int TrackSize = SectorSize * SectorsPerTrack;

    private DiscImage(byte[] data, int tracks, bool doubleSided)
    {
        Data = data;
        Tracks = tracks;
        DoubleSided = doubleSided;
    }

    /// <summary>Tracks on each side.</summary>
    public int Tracks { get; }

    /// <summary>True for a two-sided (<c>.dsd</c>) image.</summary>
    public bool DoubleSided { get; }

    /// <summary>The write-protect tab: the controller refuses every write, and DFS says <c>Disk read only</c>.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>The image's bytes, which the controller reads and writes in place.</summary>
    internal byte[] Data { get; }

    /// <summary>
    /// An image from a file's bytes: <paramref name="doubleSided"/> for a <c>.dsd</c>, false for a
    /// <c>.ssd</c>. Up to 40 tracks' worth of bytes makes a 40-track disc and up to 80 an 80-track
    /// one, unless the first side's catalogue says 800 sectors, which makes it 80 tracks; anything
    /// shorter than the whole disc is extended with zeros. The array is copied.
    /// </summary>
    public static DiscImage FromBytes(byte[] data, bool doubleSided)
    {
        ArgumentNullException.ThrowIfNull(data);
        int sides = doubleSided ? 2 : 1;
        int tracks = data.Length <= 40 * TrackSize * sides ? 40
            : data.Length <= 80 * TrackSize * sides ? 80
            : throw new ArgumentException($"{data.Length} bytes is more than an 80-track {(doubleSided ? "double" : "single")}-sided disc holds.", nameof(data));
        if (tracks == 40 && data.Length >= SectorSize + 8 && (((data[SectorSize + 6] & 3) << 8) | data[SectorSize + 7]) == 800)
        {
            tracks = 80;
        }

        var bytes = new byte[tracks * TrackSize * sides];
        data.CopyTo(bytes, 0);
        return new DiscImage(bytes, tracks, doubleSided);
    }

    /// <summary>
    /// A blank disc with an empty DFS catalogue on each side (s5b, s7): every byte zero except the
    /// disc's sector count, bits 9 and 8 in byte 6 of sector 1 and bits 7 to 0 in byte 7. A 40-track
    /// side has 400 sectors (<c>$01 $90</c>) and an 80-track one 800 (<c>$03 $20</c>). The title is
    /// empty, the cycle number 0 and the boot option 0.
    /// </summary>
    public static DiscImage Blank(int tracks, bool doubleSided)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tracks, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tracks, 80);

        int sides = doubleSided ? 2 : 1;
        var image = new DiscImage(new byte[tracks * TrackSize * sides], tracks, doubleSided);
        int sectors = tracks * SectorsPerTrack;
        for (int side = 0; side < sides; side++)
        {
            Span<byte> sector1 = image.Sector(side, 0, 1);
            sector1[6] = (byte)(sectors >> 8);
            sector1[7] = (byte)sectors;
        }
        return image;
    }

    /// <summary>The image as a file's bytes, in the layout it was read in: a copy.</summary>
    public byte[] ToBytes() => (byte[])Data.Clone();

    /// <summary>
    /// The 256 bytes of sector <paramref name="sector"/> of track <paramref name="track"/> on side
    /// <paramref name="side"/>, in place, so a write to the span changes the disc.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The sector is not on this disc.</exception>
    public Span<byte> Sector(int side, int track, int sector)
    {
        if (!Contains(side, track, sector))
        {
            throw new ArgumentOutOfRangeException(nameof(sector), $"Side {side}, track {track}, sector {sector} is not on this {Tracks}-track {(DoubleSided ? "double" : "single")}-sided disc.");
        }

        return Data.AsSpan(Offset(side, track, sector), SectorSize);
    }

    /// <summary>Whether the disc has sector <paramref name="sector"/> of track <paramref name="track"/> on side <paramref name="side"/>.</summary>
    public bool Contains(int side, int track, int sector) =>
        side >= 0 && side < (DoubleSided ? 2 : 1)
        && track >= 0 && track < Tracks
        && sector >= 0 && sector < SectorsPerTrack;

    /// <summary>Where a sector starts in the image: track interleaved for two sides (s4).</summary>
    internal int Offset(int side, int track, int sector) =>
        ((((track * (DoubleSided ? 2 : 1)) + side) * SectorsPerTrack) + sector) * SectorSize;
}
