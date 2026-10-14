using System.Security.Cryptography;
using Xunit;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The pins of the missingno-vcs-tests suite (plan task 1). The suite's sources and reference
/// pictures come from the fork dbhq-uk/missingno-vcs-tests and its built binaries from the fork
/// dbhq-uk/missingno, each at the commit in Pins.cs and each checked against the git blob hash in
/// the committed manifest, whose own SHA-256 is in Pins.cs (AGENTS.md rule 3). Only the last test
/// uses the network.
/// </summary>
public class SuiteManifestTests
{
    private const string Regions = "_ntsc.a26|_pal.a26|_secam.a26";

    private static string TestOf(string name, string suffix) => name[..^suffix.Length];

    [Fact]
    public void The_manifest_is_the_one_pinned()
    {
        byte[] manifest = File.ReadAllBytes(Path.Combine(RepoPaths.Root, Atari2600Suite.ManifestPath));
        Assert.Equal(Pins.Atari2600SuiteManifestSha256, Convert.ToHexStringLower(SHA256.HashData(manifest)));
    }

    [Fact]
    public void Every_line_names_a_fork_pinned_here_and_a_full_blob_hash()
    {
        foreach (Atari2600Suite.Entry entry in Atari2600Suite.Entries)
        {
            Assert.Matches("^[0-9a-f]{40}$", entry.BlobSha);
            string expectedFork = entry.Name.EndsWith(".a26", StringComparison.Ordinal) ? "missingno" : "missingno-vcs-tests";
            Assert.Equal(expectedFork, entry.Fork);
        }

        Assert.NotEmpty(Atari2600Suite.Entries);
    }

    [Fact]
    public void Every_source_has_its_three_binaries_and_every_binary_its_source()
    {
        IReadOnlyList<string> names = Atari2600Suite.Names;
        var sources = names.Where(n => n.EndsWith(".asm", StringComparison.Ordinal)).Select(n => TestOf(n, ".asm")).ToHashSet();
        Assert.NotEmpty(sources);

        foreach (string test in sources)
        {
            foreach (string suffix in Regions.Split('|'))
            {
                Assert.Contains(test + suffix, names);
            }
        }

        foreach (string binary in names.Where(n => n.EndsWith(".a26", StringComparison.Ordinal)))
        {
            string suffix = Regions.Split('|').Single(s => binary.EndsWith(s, StringComparison.Ordinal));
            Assert.Contains(TestOf(binary, suffix), sources);
        }
    }

    [Fact]
    public void Every_reference_picture_has_its_pair_and_its_source()
    {
        IReadOnlyList<string> names = Atari2600Suite.Names;
        var ntsc = names.Where(n => n.EndsWith("_ntsc.png", StringComparison.Ordinal)).Select(n => TestOf(n, "_ntsc.png")).ToHashSet();
        var pal = names.Where(n => n.EndsWith("_pal.png", StringComparison.Ordinal)).Select(n => TestOf(n, "_pal.png")).ToHashSet();
        Assert.NotEmpty(ntsc);
        Assert.Equal(ntsc.Order(StringComparer.Ordinal), pal.Order(StringComparer.Ordinal));
        foreach (string test in ntsc)
        {
            Assert.Contains(test + ".asm", names);
        }
    }

    [Fact]
    public void The_names_are_sorted_and_each_is_held_once()
    {
        IReadOnlyList<string> names = Atari2600Suite.Names;
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_name_the_manifest_does_not_hold_is_refused_without_a_fetch()
    {
        Assert.Throws<ArgumentException>(() => Atari2600Suite.Read("harness/no-such-test_ntsc.a26"));
    }

    [Theory]
    [InlineData("harness/sanity_ntsc.a26")]
    [InlineData("harness/sanity_pal.a26")]
    public void The_sanity_binaries_download_and_reset_into_the_cartridge(string name)
    {
        byte[] rom = Atari2600Suite.Read(name);

        Assert.True(rom.Length is 2048 or 4096, $"{name} is {rom.Length} bytes");
        // The reset vector is the image's last four bytes' first two (at $1FFC in the 6507's
        // 13-bit space). With its top three bits dropped, as the 6507 drops them (bus.md), it
        // must land in the cartridge window $1000-$1FFF.
        int vector = rom[^4] | (rom[^3] << 8);
        int thirteenBits = vector & 0x1FFF;
        Assert.InRange(thirteenBits, 0x1000, 0x1FFF);
    }
}
