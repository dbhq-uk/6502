using System.Globalization;
using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// Why two PAL screenshot tests mismatch by the partition rule (task 11's review): the suite's PAL
/// references draw some colour numbers in one colour, so the numbers the TIA keeps apart share one
/// reference colour, and the rule counts every pixel of the less frequent ones as wrong. These tests
/// pin which numbers share a colour, so a mismatch with any other cause changes the grouping and
/// fails here, as well as changing the recorded count in <c>suite-outcomes.json</c>.
/// </summary>
/// <remarks>
/// The task 11 journal entry has the triage. In <c>tia-render/colors</c> the reference draws hues 0,
/// 1, 14 and 15 of each luminance in one grey, and at luminance 0 it draws <c>$10</c>, <c>$E0</c> and
/// <c>$F0</c> in one dark grey apart from <c>$00</c>'s black. In <c>harness/calibration</c> it draws
/// <c>$E8</c> and <c>$08</c> in one grey. Every group but the last is a set of numbers the model's own
/// PAL palette also draws in one colour (<c>tia.md</c> section 19.5); at luminance 0 the model draws
/// all four black, a palette difference in <c>docs/known-differences.md</c>.
/// </remarks>
public class PalReferenceGroupingTests(ITestOutputHelper output)
{
    /// <summary>The hues the PAL references draw as one grey at each luminance.</summary>
    private static readonly int[] GreyHues = [0x0, 0x1, 0xE, 0xF];

    [Fact]
    public void Colors_on_PAL_mismatches_only_where_the_reference_draws_hues_0_1_14_and_15_in_one_grey()
    {
        Grouping grouping = Group("tia-render/colors", output);

        // At luminance 1 to 7, the four grey hues share one reference colour; at luminance 0, all but $00.
        var expected = new List<SortedSet<byte>>();
        for (int luminance = 0; luminance < 8; luminance++)
        {
            expected.Add([.. GreyHues.Select(hue => (byte)((hue << 4) | (luminance << 1))).Where(n => n != 0x00)]);
        }

        Assert.Equal(Describe(expected), Describe(grouping.Shared));
        Assert.Equal(grouping.LostPixels, grouping.Mismatches);

        // Each number the rule loses is drawn on whole lines of the picture.
        Assert.All(grouping.Lost, pair => Assert.Equal(0, pair.Value % FrameBuffer.Width));
        Assert.Equal(grouping.Lost.Count * FrameBuffer.Width, grouping.Mismatches);

        // The model's PAL palette draws each shared group in one colour too.
        Assert.All(grouping.Shared, group => Assert.Single(group.Select(n => Region.Pal.Palette.Rgba(n)).Distinct()));
    }

    [Fact]
    public void Calibration_on_PAL_mismatches_only_where_the_reference_draws_E8_and_08_in_one_grey()
    {
        Grouping grouping = Group(ScreenshotRunner.Calibration, output);

        Assert.Equal(Describe([new SortedSet<byte> { 0x08, 0xE8 }]), Describe(grouping.Shared));
        Assert.Equal([(byte)0xE8], grouping.Lost.Keys);
        Assert.Equal(grouping.LostPixels, grouping.Mismatches);
        Assert.Equal(Region.Pal.Palette.Rgba(0x08), Region.Pal.Palette.Rgba(0xE8));
    }

    /// <summary>
    /// What the partition rule saw in the test's PAL frame: each reference colour with more than one
    /// colour number under it, the numbers that lost to their group's match with their pixels, and
    /// the outcome's mismatches.
    /// </summary>
    private sealed record Grouping(List<SortedSet<byte>> Shared, SortedDictionary<byte, int> Lost, int Mismatches)
    {
        public int LostPixels => Lost.Values.Sum();
    }

    private static Grouping Group(string test, ITestOutputHelper output)
    {
        ScreenshotOutcome outcome = ScreenshotRunner.Outcome(test, Region.Pal, out Picture reference, out byte[][] ours);
        int offset = ScreenshotRunner.RowOffset;
        int first = Math.Max(0, -offset);
        int last = Math.Min(reference.Height, ours.Length - offset);
        var under = new Dictionary<uint, SortedDictionary<byte, int>>();
        for (int row = first; row < last; row++)
        {
            for (int x = 0; x < FrameBuffer.Width; x++)
            {
                uint colour = reference.Pixels[(row * FrameBuffer.Width) + x];
                byte number = ours[row + offset][x];
                if (!under.TryGetValue(colour, out SortedDictionary<byte, int>? counts))
                {
                    under[colour] = counts = [];
                }

                counts[number] = counts.GetValueOrDefault(number) + 1;
            }
        }

        var shared = new List<SortedSet<byte>>();
        var lost = new SortedDictionary<byte, int>();
        foreach ((uint colour, SortedDictionary<byte, int> counts) in under.OrderBy(pair => pair.Key))
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{colour:X8}: {string.Join(", ", counts.Select(pair => $"${pair.Key:X2} x {pair.Value}"))}"));
            if (counts.Count < 2)
            {
                continue;
            }

            shared.Add([.. counts.Keys]);

            // The match is the number found most often, a tie to the lower (Partition); the rest lose.
            byte match = counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).First().Key;
            foreach ((byte number, int pixels) in counts.Where(pair => pair.Key != match))
            {
                lost[number] = pixels;
            }
        }

        output.WriteLine(outcome.ToString());
        return new Grouping(shared, lost, outcome.Mismatches);
    }

    private static string Describe(IEnumerable<SortedSet<byte>> groups) =>
        string.Join("; ", groups.Select(group => string.Join(" ", group.Select(n => n.ToString("X2", CultureInfo.InvariantCulture)))).Order(StringComparer.Ordinal));
}
