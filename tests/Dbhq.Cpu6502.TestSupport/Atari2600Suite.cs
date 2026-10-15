using System.Security.Cryptography;

namespace Dbhq.Cpu6502.TestSupport;

/// <summary>
/// The Atari 2600's test suite, missingno-vcs-tests, which is not ours to commit. Its sources and
/// reference pictures come from the fork dbhq-uk/missingno-vcs-tests at Pins.Atari2600SuiteCommit,
/// and its built binaries from the fork dbhq-uk/missingno at Pins.Atari2600BinariesCommit. Every
/// file is checked against its git blob hash in the committed manifest, and the manifest against
/// Pins.Atari2600SuiteManifestSha256, before anything is used (AGENTS.md rule 3).
/// </summary>
public static class Atari2600Suite
{
    /// <summary>The manifest, repository-relative. tools/atari-2600-suite-manifest.sh writes it.</summary>
    public const string ManifestPath = "tests/Dbhq.Cpu6502.TestSupport/atari-2600-suite.manifest";

    private const string SuiteFork = "missingno-vcs-tests";
    private const string SuitePrefix = "roms/";
    private const string BinariesFork = "missingno";
    private const string BinariesPrefix = "crates/systems/vcs/tests/accuracy/roms/";

    /// <summary>One file: its name in the suite ("harness/sanity_ntsc.a26"), its fork, its path there and its git blob hash.</summary>
    public sealed record Entry(string Name, string Fork, string Path, string BlobSha);

    private static readonly Lazy<IReadOnlyDictionary<string, Entry>> Manifest = new(Load);

    /// <summary>Every file in the manifest, sorted by name.</summary>
    public static IReadOnlyList<Entry> Entries => [.. Manifest.Value.Values.OrderBy(e => e.Name, StringComparer.Ordinal)];

    /// <summary>Every name in the manifest, sorted.</summary>
    public static IReadOnlyList<string> Names => [.. Manifest.Value.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// Downloads (once) the file called name from its fork at its pinned commit, checks its git
    /// blob hash, and returns its bytes. name is a binary such as "harness/sanity_ntsc.a26", or a
    /// source or reference picture such as "tia-timing/hmove-comb_pal.png". Throws for a name the
    /// manifest does not hold, before anything is fetched.
    /// </summary>
    public static byte[] Read(string name)
    {
        if (!Manifest.Value.TryGetValue(name, out Entry? entry))
        {
            throw new ArgumentException($"{name} is not in {ManifestPath}", nameof(name));
        }

        string commit = entry.Fork == BinariesFork ? Pins.Atari2600BinariesCommit : Pins.Atari2600SuiteCommit;
        string url = $"https://raw.githubusercontent.com/dbhq-uk/{entry.Fork}/{commit}/{entry.Path}";
        string local = System.IO.Path.Combine("atari-2600", entry.Fork, commit, System.IO.Path.Combine(entry.Path.Split('/')));
        return File.ReadAllBytes(PinnedFiles.Fetch(url, local, PinnedFiles.GitBlob(entry.BlobSha)));
    }

    private static IReadOnlyDictionary<string, Entry> Load()
    {
        byte[] bytes = File.ReadAllBytes(System.IO.Path.Combine(RepoPaths.Root, ManifestPath));
        string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (actual != Pins.Atari2600SuiteManifestSha256)
        {
            throw new InvalidDataException($"{ManifestPath} does not match Pins.Atari2600SuiteManifestSha256: {actual}");
        }

        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (string line in System.Text.Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split(' ');
            if (parts.Length != 3)
            {
                throw new InvalidDataException($"{ManifestPath}: not \"<blob sha> <fork> <path>\": {line}");
            }

            string prefix = parts[1] switch
            {
                SuiteFork => SuitePrefix,
                BinariesFork => BinariesPrefix,
                _ => throw new InvalidDataException($"{ManifestPath}: {parts[1]} is not one of the two pinned forks"),
            };
            if (!parts[2].StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{ManifestPath}: {parts[2]} is not under {prefix} in {parts[1]}");
            }

            var entry = new Entry(parts[2][prefix.Length..], parts[1], parts[2], parts[0]);
            if (!entries.TryAdd(entry.Name, entry))
            {
                throw new InvalidDataException($"{ManifestPath}: {entry.Name} is listed twice");
            }
        }

        return entries;
    }
}
