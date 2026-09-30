using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Cpu6502.Tests.Harte;

/// <summary>Where each variant's Harte files live, downloaded on first use and checked against the manifest.</summary>
public static class HarteSets
{
    private static readonly Dictionary<string, string> Manifest = File
        .ReadLines(Path.Combine(AppContext.BaseDirectory, "Harte", "harte.manifest"))
        .Select(line => line.Split(' '))
        .ToDictionary(parts => parts[1], parts => parts[0]);

    public static string Folder(CpuVariant variant) => variant switch
    {
        CpuVariant.Nmos6502 => "6502",
        CpuVariant.Ricoh2A03 => "nes6502",
        CpuVariant.Synertek65C02 => "synertek65c02",
        CpuVariant.Rockwell65C02 => "rockwell65c02",
        CpuVariant.Wdc65C02 => "wdc65c02",
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    public static string FileFor(CpuVariant variant, byte opcode)
    {
        string relative = $"{Folder(variant)}/v1/{opcode:x2}.json";
        return PinnedFiles.Fetch(
            $"https://raw.githubusercontent.com/SingleStepTests/65x02/{Pins.HarteCommit}/{relative}",
            Path.Combine("harte", relative),
            PinnedFiles.GitBlob(Manifest[relative]));
    }
}
