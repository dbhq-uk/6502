using System.Globalization;
using Dbhq.Cpu6502.TestSupport;
using Dbhq.Machines.Atari2600.Boards;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// Choosing the board from a file: by its size, and the Superchip by its filler
/// (<c>docs/atari-2600/facts/cartridge.md</c> section 9), and the plain refusal of every other size.
/// The last test reads the suite's own cartridge files through <see cref="Atari2600Suite"/>.
/// </summary>
public class CartridgeTests
{
    /// <summary>The modelled sizes, cartridge.md section 9.1.</summary>
    private static readonly int[] ModelledSizes = [2048, 4096, 8192, 12288, 16384, 32768];

    public static TheoryData<int, string, Type> BySize() => new()
    {
        { 2048, "2K", typeof(Plain) },
        { 4096, "4K", typeof(Plain) },
        { 8192, "F8", typeof(F8) },
        { 12288, "FA", typeof(Fa) },
        { 16384, "F6", typeof(F6) },
        { 32768, "F4", typeof(F4) },
    };

    [Theory]
    [MemberData(nameof(BySize))]
    public void Each_modelled_size_names_one_board(int size, string name, Type board)
    {
        Cartridge cartridge = Cartridge.Load(TestImages.BankNumbers(size, filler: false));
        Assert.Equal(name, cartridge.BoardName);
        Assert.IsType(board, cartridge.CreateBoard());
    }

    public static TheoryData<int, string> SuperchipSizes() => new()
    {
        { 8192, "F8 with the Superchip" },
        { 16384, "F6 with the Superchip" },
        { 32768, "F4 with the Superchip" },
    };

    [Theory]
    [MemberData(nameof(SuperchipSizes))]
    public void Filler_in_the_first_256_bytes_of_every_bank_gives_the_superchip(int size, string name)
    {
        // Each bank's filler is its own bank number, so the banks need not share one value.
        Cartridge cartridge = Cartridge.Load(TestImages.BankNumbers(size, filler: true));
        Assert.Equal(name, cartridge.BoardName);
        Assert.IsType<Superchip>(cartridge.CreateBoard());
    }

    public static TheoryData<int, int, int> OneByteChanged()
    {
        var data = new TheoryData<int, int, int>();
        foreach (int size in new[] { 8192, 16384, 32768 })
        {
            for (int bank = 0; bank < size / TestImages.BankSize; bank++)
            {
                data.Add(size, bank, 0x00);
                data.Add(size, bank, 0xFF);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(OneByteChanged))]
    public void One_byte_changed_in_one_banks_filler_takes_the_superchip_away(int size, int bank, int offset)
    {
        byte[] image = TestImages.BankNumbers(size, filler: true);
        image[(bank * TestImages.BankSize) + offset] ^= 0x40;

        Cartridge cartridge = Cartridge.Load(image);
        Assert.DoesNotContain("Superchip", cartridge.BoardName, StringComparison.Ordinal);
        Assert.IsNotType<Superchip>(cartridge.CreateBoard());
    }

    [Fact]
    public void A_byte_just_past_the_first_256_does_not_count()
    {
        byte[] image = TestImages.BankNumbers(8192, filler: true);
        image[0x100] ^= 0x40;
        image[0x1100] ^= 0x40;
        Assert.Equal("F8 with the Superchip", Cartridge.Load(image).BoardName);
    }

    [Theory]
    [InlineData(2048, "2K")]
    [InlineData(4096, "4K")]
    [InlineData(12288, "FA")]
    public void Only_f8_f6_and_f4_carry_the_superchip(int size, string name)
    {
        // A 2K, 4K or FA image full of filler is still that board (cartridge.md section 2).
        Assert.Equal(name, Cartridge.Load(TestImages.BankNumbers(size, filler: true)).BoardName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3072)]
    [InlineData(8448)]
    [InlineData(10496)]
    [InlineData(4095)]
    [InlineData(32769)]
    [InlineData(65536)]
    [InlineData(10 * 1024 * 1024)]
    public void Every_other_size_is_refused_with_a_sentence_naming_it_and_the_modelled_sizes(int size)
    {
        // 8448 is a Supercharger image and 10496 a DPC one, from the suite (suite.md section 6).
        var refusal = Assert.Throws<CartridgeException>(() => Cartridge.Load(new byte[size]));

        string message = refusal.Message;
        Assert.Contains(size.ToString("N0", CultureInfo.InvariantCulture) + (size == 1 ? " byte," : " bytes,"), message, StringComparison.Ordinal);
        foreach (int modelled in ModelledSizes)
        {
            Assert.Contains(modelled.ToString("N0", CultureInfo.InvariantCulture), message, StringComparison.Ordinal);
        }

        Assert.EndsWith(".", message, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refusal_allocates_nothing_the_size_of_the_file()
    {
        // The check is on the array's length before anything is copied (plan, Review Focus 2).
        byte[] file = new byte[10 * 1024 * 1024];
        Assert.Throws<CartridgeException>(() => Cartridge.Load(file));

        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<CartridgeException>(() => Cartridge.Load(file));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 64 * 1024, $"refusing a 10 MB file allocated {allocated} bytes");
    }

    [Fact]
    public void The_largest_file_read_is_the_largest_modelled_board()
    {
        Assert.Equal(Cartridge.MaxFileSize, ModelledSizes.Max());
        Cartridge.Load(new byte[Cartridge.MaxFileSize]);
        Assert.Throws<CartridgeException>(() => Cartridge.Load(new byte[Cartridge.MaxFileSize + 1]));
    }

    [Fact]
    public void No_file_is_refused_plainly()
    {
        Assert.Throws<ArgumentNullException>(() => Cartridge.Load(null!));
    }

    [Fact]
    public void The_cartridge_keeps_its_own_copy_of_the_file()
    {
        byte[] file = TestImages.Pattern(4096);
        byte expected = file[0x123];
        Cartridge cartridge = Cartridge.Load(file);
        file[0x123] ^= 0xFF;

        Assert.Equal(expected, cartridge.CreateBoard().Peek(0x1123));
    }

    [Fact]
    public void Each_board_made_is_a_fresh_one()
    {
        Cartridge cartridge = Cartridge.Load(TestImages.BankNumbers(8192, filler: false));
        IBoard first = cartridge.CreateBoard();
        first.Access(0x1FF8, write: false, 0x00);

        IBoard second = cartridge.CreateBoard();
        Assert.NotSame(first, second);
        Assert.Equal(0, first.Bank);
        Assert.Equal(1, second.Bank);
    }

    public static TheoryData<string, string> SuiteFiles()
    {
        // The plan's "Fixed before running" item 6, with mirror-2k and bank-fa beside it, for the
        // _ntsc and _pal builds (suite.md section 6 measured both).
        var data = new TheoryData<string, string>();
        (string Test, string Board)[] expected =
        [
            ("mirror-2k", "2K"),
            ("bank-f8", "F8"),
            ("bank-f6", "F6"),
            ("bank-f4", "F4"),
            ("bank-fa", "FA"),
            ("bank-hotspot-window", "F8"),
            ("power-on-bank", "F8"),
            ("ram-superchip", "F8 with the Superchip"),
        ];
        foreach ((string test, string board) in expected)
        {
            data.Add($"cartridge/{test}_ntsc.a26", board);
            data.Add($"cartridge/{test}_pal.a26", board);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(SuiteFiles))]
    public void The_suites_own_cartridge_files_get_the_boards_fixed_before_running(string name, string board)
    {
        Assert.Equal(board, Cartridge.Load(Atari2600Suite.Read(name)).BoardName);
    }
}
