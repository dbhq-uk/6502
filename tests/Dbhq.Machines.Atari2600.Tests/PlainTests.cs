using Dbhq.Machines.Atari2600.Boards;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The plain 2K and 4K boards (<c>docs/atari-2600/facts/cartridge.md</c> section 1): the image
/// fills the window, a 2K image twice, and a write changes nothing.
/// </summary>
public class PlainTests
{
    [Fact]
    public void A_2k_image_appears_at_1000_and_again_at_1800()
    {
        byte[] image = TestImages.Pattern(2048);
        var board = new Plain(image);

        for (int offset = 0; offset < 2048; offset++)
        {
            Assert.Equal(image[offset], board.Access((ushort)(0x1000 + offset), write: false, 0x00));
            Assert.Equal(image[offset], board.Access((ushort)(0x1800 + offset), write: false, 0x00));
            Assert.Equal(image[offset], board.Peek((ushort)(0x1800 + offset)));
        }
    }

    [Fact]
    public void A_2k_images_reset_vector_is_read_through_the_mirror()
    {
        // cartridge.md worked example 1: $1FFC and $1FFD are offsets $7FC and $7FD.
        byte[] image = TestImages.Pattern(2048);
        image[0x7FC] = 0x00;
        image[0x7FD] = 0xF8;
        var board = new Plain(image);

        Assert.Equal(0x00, board.Access(0x1FFC, write: false, 0x00));
        Assert.Equal(0xF8, board.Access(0x1FFD, write: false, 0x00));
    }

    [Fact]
    public void A_4k_image_fills_the_window_once()
    {
        byte[] image = TestImages.Pattern(4096);
        var board = new Plain(image);

        for (int offset = 0; offset < 4096; offset++)
        {
            Assert.Equal(image[offset], board.Access((ushort)(0x1000 + offset), write: false, 0x00));
        }
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(4096)]
    public void A_write_changes_nothing(int size)
    {
        byte[] image = TestImages.Pattern(size);
        byte[] copy = (byte[])image.Clone();
        var board = new Plain(image);

        for (int address = 0x1000; address < 0x2000; address++)
        {
            board.Access((ushort)address, write: true, (byte)~image[address & (size - 1)]);
        }

        Assert.Equal(copy, image);
        for (int address = 0x1000; address < 0x2000; address++)
        {
            Assert.Equal(image[address & (size - 1)], board.Access((ushort)address, write: false, 0x00));
        }

        Assert.Equal(0, board.Bank);
    }
}
