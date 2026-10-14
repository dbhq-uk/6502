namespace Dbhq.Cpu6502.TestSupport;

/// <summary>
/// The NES test ROMs: Blargg's and others', which are not ours to commit. They
/// come from the fork dbhq-uk/nes-test-roms at Pins.NesTestRomsCommit, and each
/// is checked against its hash in Pins.NesTestRomHashes (AGENTS.md rule 3).
/// </summary>
public static class NesTestRoms
{
    private const string Base = "https://raw.githubusercontent.com/dbhq-uk/nes-test-roms/" + Pins.NesTestRomsCommit + "/";

    /// <summary>
    /// Downloads (once) the file at pinnedName, a path in the fork such as
    /// "ppu_vbl_nmi/ppu_vbl_nmi.nes", checks it against its pin, and returns its bytes.
    /// </summary>
    public static byte[] Read(string pinnedName)
    {
        if (!Pins.NesTestRomHashes.TryGetValue(pinnedName, out string? sha256))
        {
            throw new ArgumentException($"{pinnedName} is not a key in Pins.NesTestRomHashes; pin it there first", nameof(pinnedName));
        }

        string local = Path.Combine("nes-test-roms", Pins.NesTestRomsCommit, Path.Combine(pinnedName.Split('/')));
        return File.ReadAllBytes(PinnedFiles.Fetch(Base + pinnedName, local, PinnedFiles.Sha256(sha256)));
    }
}
