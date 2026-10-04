using System.Text;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// An Acorn DFS catalogue read from sectors 0 and 1 of a side, written from the published layout
/// (fact sheet <c>disc.md</c> s5a, from BeebWiki's "Acorn DFS disc format"), not from what the
/// machine wrote. <see cref="DfsCatalogueTests"/> checks it against the sheet's two worked
/// examples before any disc test relies on it.
/// </summary>
/// <param name="Title">The twelve title bytes as stored: sector 0 bytes 0-7, then sector 1 bytes 0-3, padding and all.</param>
/// <param name="Cycle">The cycle number, sector 1 byte 4, stored in BCD and given here as its decimal value.</param>
/// <param name="BootOption">Sector 1 byte 6, bits 5 and 4.</param>
/// <param name="Sectors">The disc's sector count: byte 6 bits 1 and 0 above byte 7.</param>
/// <param name="Files">The entries in catalogue order, which is descending start sector.</param>
public sealed record DfsCatalogue(string Title, int Cycle, int BootOption, int Sectors, IReadOnlyList<DfsFile> Files)
{
    /// <summary>The title with its NUL and space padding taken off the end.</summary>
    public string TitleText => Title.TrimEnd('\0', ' ');

    public static DfsCatalogue Read(DiscImage disc, int side = 0) =>
        Read(disc.Sector(side, 0, 0), disc.Sector(side, 0, 1));

    public static DfsCatalogue Read(ReadOnlySpan<byte> sector0, ReadOnlySpan<byte> sector1)
    {
        string title = Encoding.Latin1.GetString(sector0[..8]) + Encoding.Latin1.GetString(sector1[..4]);
        int cycle = (10 * (sector1[4] >> 4)) + (sector1[4] & 0x0F);
        int count = sector1[5] / 8;
        int boot = (sector1[6] >> 4) & 3;
        int sectors = ((sector1[6] & 3) << 8) | sector1[7];

        var files = new List<DfsFile>();
        for (int n = 0; n < count; n++)
        {
            ReadOnlySpan<byte> name = sector0.Slice(8 + (8 * n), 8);
            ReadOnlySpan<byte> info = sector1.Slice(8 + (8 * n), 8);
            int high = info[6];
            files.Add(new DfsFile(
                Directory: (char)(name[7] & 0x7F),
                Name: Encoding.Latin1.GetString(name[..7]).TrimEnd(' '),
                Locked: (name[7] & 0x80) != 0,
                Load: info[0] | (info[1] << 8) | (((high >> 2) & 3) << 16),
                Exec: info[2] | (info[3] << 8) | (((high >> 6) & 3) << 16),
                Length: info[4] | (info[5] << 8) | (((high >> 4) & 3) << 16),
                Start: info[7] | ((high & 3) << 8)));
        }

        return new DfsCatalogue(title, cycle, boot, sectors, files);
    }
}

/// <summary>One file in a DFS catalogue: addresses and length are 18 bits, the start sector 10.</summary>
public sealed record DfsFile(char Directory, string Name, bool Locked, int Load, int Exec, int Length, int Start)
{
    /// <summary>
    /// The line <c>*INFO</c> prints for the file, by the sheet's rule (s6c): directory, a full stop,
    /// the name padded to seven, two spaces, <c>L</c> or a space, two spaces, then load, exec and
    /// length in six hex digits and the start sector in three. An address whose bits 17 and 16 are
    /// both set belongs to the I/O processor and shows as <c>FF</c> and its low sixteen bits (s5a).
    /// </summary>
    public string InfoLine =>
        $"{Directory}.{Name,-7}  {(Locked ? 'L' : ' ')}  {Address(Load)} {Address(Exec)} {Length:X6} {Start:X3}";

    private static string Address(int address) =>
        (address >> 16) == 3 ? $"FF{address & 0xFFFF:X4}" : $"{address:X6}";
}
