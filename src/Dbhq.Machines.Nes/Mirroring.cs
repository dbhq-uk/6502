namespace Dbhq.Machines.Nes;

/// <summary>
/// How the cartridge connects the console's 2 KB of nametable RAM to the four nametable addresses
/// <c>$2000</c> to <c>$2FFF</c> (<c>docs/nes/facts/mappers.md</c> section 1).
/// </summary>
public enum Mirroring
{
    /// <summary>Horizontal mirroring: <c>$2000</c> and <c>$2400</c> are one table, <c>$2800</c> and <c>$2C00</c> the other (CIRAM A10 = PPU A11).</summary>
    Horizontal,

    /// <summary>Vertical mirroring: <c>$2000</c> and <c>$2800</c> are one table, <c>$2400</c> and <c>$2C00</c> the other (CIRAM A10 = PPU A10).</summary>
    Vertical,

    /// <summary>All four addresses show the first kilobyte.</summary>
    SingleScreenLow,

    /// <summary>All four addresses show the second kilobyte.</summary>
    SingleScreenHigh,

    /// <summary>Four separate tables, the extra two in RAM on the cartridge.</summary>
    FourScreen,
}

/// <summary>Which 1 KB page of nametable RAM each of the four nametables uses, for each <see cref="Mirroring"/> (mappers.md 1).</summary>
internal static class NametablePages
{
    /// <summary>
    /// Fills <paramref name="pages"/>, four entries, with the page each nametable uses under
    /// <paramref name="mirroring"/>: vertical 0 1 0 1, horizontal 0 0 1 1, single screen all 0
    /// or all 1, four screen 0 1 2 3, the last two being the board's own 2 KB.
    /// </summary>
    public static void Fill(int[] pages, Mirroring mirroring)
    {
        for (int table = 0; table < 4; table++)
        {
            pages[table] = Of(mirroring, table);
        }
    }

    /// <summary>The page nametable <paramref name="table"/> (0 to 3) uses under <paramref name="mirroring"/>.</summary>
    public static int Of(Mirroring mirroring, int table) => mirroring switch
    {
        Mirroring.Vertical => table & 1,
        Mirroring.Horizontal => table >> 1,
        Mirroring.SingleScreenLow => 0,
        Mirroring.SingleScreenHigh => 1,
        _ => table,
    };
}
