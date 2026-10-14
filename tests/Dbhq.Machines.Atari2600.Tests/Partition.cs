namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The comparison of a frame with a reference picture as a partition (the plan's "Fixed before
/// running", item 5), so that it does not depend on the palette the reference was drawn with.
/// </summary>
/// <remarks>
/// For each colour in the reference, the TIA colour number found most often under it in our frame
/// is its match; a tie goes to the lower number. If two or more reference colours match one number,
/// the one with the most pixels in the reference keeps it and the others keep no match (a tie of
/// pixels goes to the lower RGBA value, a choice the plan leaves open). A pixel is mismatched when
/// our colour number is not its reference colour's match, or its colour has none. The outcome is
/// the count of mismatched pixels; zero is a pass.
/// </remarks>
internal static class Partition
{
    /// <summary>
    /// The mismatched pixels between <paramref name="reference"/>, RGBA values, and
    /// <paramref name="ours"/>, TIA colour numbers, the same pixels in the same order.
    /// </summary>
    /// <exception cref="ArgumentException">The two are not the same length.</exception>
    public static int Mismatches(ReadOnlySpan<uint> reference, ReadOnlySpan<byte> ours)
    {
        if (reference.Length != ours.Length)
        {
            throw new ArgumentException($"The reference has {reference.Length} pixels and our frame {ours.Length}.", nameof(ours));
        }

        // How often each colour number is found under each reference colour, and each colour's pixels.
        var under = new Dictionary<uint, int[]>();
        for (int i = 0; i < reference.Length; i++)
        {
            if (!under.TryGetValue(reference[i], out int[]? counts))
            {
                counts = new int[256];
                under.Add(reference[i], counts);
            }

            counts[ours[i]]++;
        }

        // Each reference colour's match: the number found most often under it, the lower on a tie.
        var match = new Dictionary<uint, int>();
        foreach ((uint colour, int[] counts) in under)
        {
            int best = 0;
            for (int number = 1; number < counts.Length; number++)
            {
                if (counts[number] > counts[best])
                {
                    best = number;
                }
            }

            match.Add(colour, best);
        }

        // Where reference colours share a match, only the most frequent keeps it.
        foreach (IGrouping<int, uint> shared in match.Keys.ToArray().GroupBy(colour => match[colour]))
        {
            uint keeper = shared.OrderByDescending(colour => under[colour].Sum()).ThenBy(colour => colour).First();
            foreach (uint colour in shared.Where(colour => colour != keeper))
            {
                match[colour] = -1;
            }
        }

        int mismatches = 0;
        for (int i = 0; i < reference.Length; i++)
        {
            if (ours[i] != match[reference[i]])
            {
                mismatches++;
            }
        }

        return mismatches;
    }
}
