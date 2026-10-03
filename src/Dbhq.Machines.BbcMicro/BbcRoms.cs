namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The three ROM images a Model B runs: the operating system (MOS 1.20, at
/// $C000-$FFFF), BBC BASIC 2 (paged ROM slot 15) and the Disc Filing System
/// (paged ROM slot 14). Each is exactly 16384 bytes.
/// </summary>
public sealed record BbcRoms
{
    /// <summary>The size of each image in bytes.</summary>
    public const int RomSize = 16384;

    public BbcRoms(byte[] Os, byte[] Basic, byte[] Dfs)
    {
        this.Os = Check(Os, nameof(Os));
        this.Basic = Check(Basic, nameof(Basic));
        this.Dfs = Check(Dfs, nameof(Dfs));
    }

    public byte[] Os { get; }

    public byte[] Basic { get; }

    public byte[] Dfs { get; }

    private static byte[] Check(byte[] rom, string name)
    {
        ArgumentNullException.ThrowIfNull(rom, name);
        if (rom.Length != RomSize)
        {
            throw new ArgumentException($"The {name} ROM must be {RomSize} bytes, not {rom.Length}.", name);
        }

        return rom;
    }
}
