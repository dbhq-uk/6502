namespace Dbhq.Machines.Electron;

/// <summary>
/// The two ROM images an Electron runs: the operating system (OS 1.00, at
/// $C000-$FFFF, with $FC00-$FEFF hidden by I/O) and BBC BASIC 2 (paged ROM slots
/// 10 and 11, one chip in both). Each is exactly 16384 bytes.
/// </summary>
public sealed record ElectronRoms
{
    /// <summary>The size of each image in bytes.</summary>
    public const int RomSize = 16384;

    public ElectronRoms(byte[] Os, byte[] Basic)
    {
        this.Os = Check(Os, nameof(Os));
        this.Basic = Check(Basic, nameof(Basic));
    }

    public byte[] Os { get; }

    public byte[] Basic { get; }

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
