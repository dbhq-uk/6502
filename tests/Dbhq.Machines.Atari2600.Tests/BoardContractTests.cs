using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// What every board promises the bus (<see cref="IBoard"/>), checked over each board the
/// cartridge can make: <see cref="IBoard.Peek"/> has no side effect and shows what a read would
/// return; <see cref="IBoard.PowerOn"/> gives the bank <c>docs/atari-2600/facts/cartridge.md</c>
/// section 8 chose, the last, and clears the RAM; and no address in the window makes a board throw.
/// </summary>
public class BoardContractTests
{
    public static TheoryData<string, int, bool, int> Boards() => new()
    {
        { "2K", 2048, false, 0 },
        { "4K", 4096, false, 0 },
        { "F8", 8192, false, 1 },
        { "FA", 12288, false, 2 },
        { "F6", 16384, false, 3 },
        { "F4", 32768, false, 7 },
        { "F8 with the Superchip", 8192, true, 1 },
        { "F6 with the Superchip", 16384, true, 3 },
        { "F4 with the Superchip", 32768, true, 7 },
    };

    private static Cartridge Load(string name, int size, bool filler)
    {
        Cartridge cartridge = Cartridge.Load(TestImages.BankNumbers(size, filler));
        Assert.Equal(name, cartridge.BoardName);
        return cartridge;
    }

    /// <summary>The write ports, where a read drives nothing (cartridge.md sections 6 and 7.1).</summary>
    private static bool IsWritePort(string name, int address) =>
        (name.EndsWith("Superchip", StringComparison.Ordinal) && address < 0x1080)
        || (name == "FA" && address < 0x1100);

    /// <summary>
    /// Every address read with a byte on the bus that changes, every address written, then every
    /// address read again; the bytes returned and the bank after each access.
    /// </summary>
    private static List<int> Script(IBoard board)
    {
        var seen = new List<int>();
        for (int address = 0x1000; address < 0x2000; address++)
        {
            seen.Add(board.Access((ushort)address, write: false, (byte)address));
            seen.Add(board.Bank);
        }

        for (int address = 0x1000; address < 0x2000; address++)
        {
            board.Access((ushort)address, write: true, (byte)((address * 3) | 1));
            seen.Add(board.Bank);
        }

        for (int address = 0x1000; address < 0x2000; address++)
        {
            seen.Add(board.Access((ushort)address, write: false, 0x00));
            seen.Add(board.Bank);
        }

        return seen;
    }

    [Fact]
    public void Every_board_in_the_library_is_one_the_cartridge_makes_here()
    {
        var boards = typeof(IBoard).Assembly.GetTypes()
            .Where(t => typeof(IBoard).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .Select(t => t.Name)
            .ToHashSet();
        var made = new HashSet<string>();
        foreach (object[] row in Boards())
        {
            made.Add(Load((string)row[0]!, (int)row[1]!, (bool)row[2]!).CreateBoard().GetType().Name);
        }

        Assert.Equal(boards.Order(), made.Order());
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void Peeking_every_address_changes_nothing(string name, int size, bool filler, int powerOnBank)
    {
        Cartridge cartridge = Load(name, size, filler);
        IBoard peeked = cartridge.CreateBoard();
        IBoard alone = cartridge.CreateBoard();

        for (int address = 0x1000; address < 0x2000; address++)
        {
            peeked.Peek((ushort)address);
            Assert.Equal(powerOnBank, peeked.Bank);
        }

        Assert.Equal(Script(alone), Script(peeked));
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void Peek_shows_what_a_read_would_return(string name, int size, bool filler, int powerOnBank)
    {
        Cartridge cartridge = Load(name, size, filler);
        IBoard peeked = cartridge.CreateBoard();
        for (int address = 0x1000; address < 0x2000; address++)
        {
            if (IsWritePort(name, address))
            {
                continue;
            }

            IBoard read = cartridge.CreateBoard();
            Assert.Equal(read.Access((ushort)address, write: false, 0x5C), peeked.Peek((ushort)address));
        }

        Assert.Equal(powerOnBank, peeked.Bank);
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void Peek_of_a_write_port_shows_the_cell_and_writes_nothing(string name, int size, bool filler, int powerOnBank)
    {
        // A write port drives nothing on a read, so there is no byte to show; Peek shows the cell,
        // which is what the read port at the same cell gives (IBoard.Peek).
        _ = powerOnBank;
        IBoard board = Load(name, size, filler).CreateBoard();
        int readPort = name == "FA" ? 0x100 : 0x80;
        for (int address = 0x1000; IsWritePort(name, address); address++)
        {
            board.Access((ushort)address, write: true, (byte)(address ^ 0x3C));
            Assert.Equal((byte)(address ^ 0x3C), board.Peek((ushort)address));
            Assert.Equal((byte)(address ^ 0x3C), board.Access((ushort)(address + readPort), write: false, 0x00));
        }
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void A_new_board_is_at_the_last_bank_and_power_on_puts_it_back(string name, int size, bool filler, int powerOnBank)
    {
        Cartridge cartridge = Load(name, size, filler);
        IBoard board = cartridge.CreateBoard();
        Assert.Equal(powerOnBank, board.Bank);
        Assert.Equal(Math.Max(0, (size / TestImages.BankSize) - 1), powerOnBank);

        // Leave it in another bank with its RAM written, then power it on.
        Script(board);
        board.Access(0x1FF8, write: true, 0x01);
        for (int address = 0x1000; IsWritePort(name, address); address++)
        {
            board.Access((ushort)address, write: true, 0xA5);
        }

        board.PowerOn();

        // The RAM is read through its read port first: the script writes every cell before it
        // reads one, so it alone would not see RAM that power on left as it was.
        Assert.Equal(powerOnBank, board.Bank);
        int readPort = name == "FA" ? 0x100 : 0x80;
        for (int address = 0x1000; IsWritePort(name, address); address++)
        {
            Assert.Equal(0x00, board.Access((ushort)(address + readPort), write: false, 0xFF));
        }

        Assert.Equal(Script(cartridge.CreateBoard()), Script(board));
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void The_ram_is_clear_at_power_on(string name, int size, bool filler, int powerOnBank)
    {
        // cartridge.md section 6: no source states the power-on contents; zero is the model's choice.
        _ = powerOnBank;
        IBoard board = Load(name, size, filler).CreateBoard();
        int readPort = name == "FA" ? 0x100 : 0x80;
        for (int address = 0x1000; IsWritePort(name, address); address++)
        {
            Assert.Equal(0x00, board.Access((ushort)(address + readPort), write: false, 0xFF));
        }
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void No_address_in_the_window_makes_a_board_throw(string name, int size, bool filler, int powerOnBank)
    {
        _ = powerOnBank;
        IBoard board = Load(name, size, filler).CreateBoard();
        for (int address = 0x1000; address < 0x2000; address++)
        {
            foreach (byte value in new byte[] { 0x00, 0xFF })
            {
                board.Peek((ushort)address);
                board.Access((ushort)address, write: false, value);
                board.Access((ushort)address, write: true, value);
            }
        }
    }
}
