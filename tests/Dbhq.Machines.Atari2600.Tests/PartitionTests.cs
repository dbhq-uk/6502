using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The partition rule (the plan's "Fixed before running", item 5), on pictures the test builds.
/// </summary>
public class PartitionTests
{
    private const uint Red = 0xFF0000FF;
    private const uint Green = 0xFF00FF00;
    private const uint Blue = 0xFFFF0000;

    [Fact]
    public void Identical_pictures_give_zero()
    {
        uint[] reference = [Red, Red, Green, Blue, Green, Red];
        byte[] ours = [0x44, 0x44, 0xC8, 0x86, 0xC8, 0x44];
        Assert.Equal(0, Partition.Mismatches(reference, ours));
    }

    [Fact]
    public void Colours_renamed_one_for_one_give_zero()
    {
        // The reference's palette is not ours: only which pixels share a colour counts.
        uint[] reference = [Red, Green, Green, Blue, Blue, Blue];
        byte[] ours = [0x0E, 0x00, 0x00, 0x2A, 0x2A, 0x2A];
        Assert.Equal(0, Partition.Mismatches(reference, ours));
    }

    [Fact]
    public void One_wrong_pixel_gives_one()
    {
        uint[] reference = [Red, Red, Red, Green, Green, Green];
        byte[] ours = [0x44, 0x44, 0xC8, 0xC8, 0xC8, 0xC8];
        Assert.Equal(1, Partition.Mismatches(reference, ours));
    }

    [Fact]
    public void Two_reference_colours_drawn_as_one_number_count_the_less_frequent_ones_pixels()
    {
        // Red has four pixels and green two, all drawn as $44: red keeps the match, and green's two
        // pixels are mismatched.
        uint[] reference = [Red, Red, Red, Red, Green, Green];
        byte[] ours = [0x44, 0x44, 0x44, 0x44, 0x44, 0x44];
        Assert.Equal(2, Partition.Mismatches(reference, ours));
    }

    [Fact]
    public void A_tie_under_a_colour_goes_to_the_lower_number()
    {
        // Under red, $44 and $22 are found twice each, so red's match is $22, the lower; green's is
        // $22 too, and red, with more pixels, keeps it. So red's two $44s and green's three $22s
        // count: 5. Were the tie to go to $44, only red's two $22s would.
        uint[] reference = [Red, Red, Red, Red, Green, Green, Green];
        byte[] ours = [0x44, 0x22, 0x44, 0x22, 0x22, 0x22, 0x22];
        Assert.Equal(5, Partition.Mismatches(reference, ours));
    }

    [Fact]
    public void Pictures_of_different_sizes_are_refused()
    {
        Assert.Throws<ArgumentException>(() => Partition.Mismatches(new uint[4], new byte[5]));
    }
}
