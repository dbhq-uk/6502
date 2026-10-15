using Dbhq.Machines.Atari2600.Boards;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// F8, F6 and F4 (<c>docs/atari-2600/facts/cartridge.md</c> sections 2 to 5): each hotspot, read
/// or written, selects its bank; a hotspot read returns its byte in the bank it selects (section 4
/// statement 2, which corrected the plan's first draft); every later access sees the new bank; and
/// no other address switches. The images hold their bank number in every byte, so a read shows
/// which bank answered. The last tests take their accesses from the core (section 5).
/// </summary>
public class BankedBoardTests
{
    public static TheoryData<string> Boards() => new() { "F8", "F6", "F4" };

    private static (int Size, int FirstHotspot, int Banks) Shape(string name) => name switch
    {
        "F8" => (8192, 0x1FF8, 2),
        "F6" => (16384, 0x1FF6, 4),
        "F4" => (32768, 0x1FF4, 8),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static IBoard Make(string name, byte[] image) => name switch
    {
        "F8" => new F8(image),
        "F6" => new F6(image),
        "F4" => new F4(image),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static IBoard Make(string name) => Make(name, TestImages.BankNumbers(Shape(name).Size, filler: false));

    [Theory]
    [MemberData(nameof(Boards))]
    public void Each_hotspot_read_selects_its_bank_and_returns_its_byte_there(string name)
    {
        (_, int first, int banks) = Shape(name);
        for (int from = 0; from < banks; from++)
        {
            for (int to = 0; to < banks; to++)
            {
                IBoard board = Make(name);
                board.Access((ushort)(first + from), write: false, 0x00);
                Assert.Equal(from, board.Bank);

                byte read = board.Access((ushort)(first + to), write: false, 0x00);

                Assert.Equal(to, board.Bank);
                Assert.Equal(to, read);
                Assert.Equal(to, board.Access(0x1234, write: false, 0x00));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void Each_hotspot_written_with_any_value_selects_its_bank(string name)
    {
        (_, int first, int banks) = Shape(name);
        foreach (byte value in new byte[] { 0x00, 0x01, 0xFE, 0xFF })
        {
            for (int to = 0; to < banks; to++)
            {
                IBoard board = Make(name);
                board.Access((ushort)(first + ((to + 1) % banks)), write: false, 0x00);

                board.Access((ushort)(first + to), write: true, value);

                Assert.Equal(to, board.Bank);
                Assert.Equal(to, board.Access(0x1000, write: false, 0x00));
                Assert.Equal(to, board.Access(0x1FFC, write: false, 0x00));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void No_other_address_switches_by_read_or_write(string name)
    {
        (_, int first, int banks) = Shape(name);
        for (int bank = 0; bank < banks; bank++)
        {
            IBoard board = Make(name);
            board.Access((ushort)(first + bank), write: false, 0x00);
            for (int address = 0x1000; address < 0x2000; address++)
            {
                if (address >= first && address < first + banks)
                {
                    continue;
                }

                board.Access((ushort)address, write: false, 0xFF);
                board.Access((ushort)address, write: true, 0xFF);
                Assert.Equal(bank, board.Bank);
            }
        }
    }

    [Fact]
    public void Switching_f6_and_f4_as_the_sheets_worked_example_does()
    {
        // cartridge.md worked example 2.
        IBoard f6 = Make("F6");
        f6.Access(0x1FF6, write: false, 0x00);
        Assert.Equal(0, f6.Bank);
        f6.Access(0x1FF8, write: true, 0x00);
        Assert.Equal(2, f6.Bank);
        f6.Access(0x1FF7, write: false, 0x00);
        Assert.Equal(1, f6.Bank);
        f6.Access(0x1FFA, write: false, 0x00);
        Assert.Equal(1, f6.Bank);
        f6.Access(0x1FF5, write: false, 0x00);
        Assert.Equal(1, f6.Bank);

        IBoard f4 = Make("F4");
        f4.Access(0x1FFA, write: false, 0x00);
        Assert.Equal(6, f4.Bank);
        f4.Access(0x1FF5, write: false, 0x00);
        Assert.Equal(1, f4.Bank);

        IBoard f8 = Make("F8");
        f8.Access(0x1FF5, write: false, 0x00);
        Assert.Equal(1, f8.Bank);
        f8.Access(0x1FFA, write: false, 0x00);
        Assert.Equal(1, f8.Bank);
    }

    [Fact]
    public void The_hotspot_window_as_the_suites_test_has_it()
    {
        // cartridge.md worked example 3, with the bytes of the suite's bank-hotspot-window.
        byte[] image = TestImages.BankNumbers(8192, filler: false)
            .Put(0, 0x1FF8, 0x58, 0x59)
            .Put(1, 0x1FF8, 0x78, 0x79)
            .Put(0, 0x1FF0, 0x50);
        var board = new F8(image);

        Assert.Equal(1, board.Bank);
        Assert.Equal(0x58, board.Access(0x1FF8, write: false, 0x00));
        Assert.Equal(0, board.Bank);

        Assert.Equal(0x79, board.Access(0x1FF9, write: false, 0x00));
        Assert.Equal(1, board.Bank);

        board.Access(0x1FF8, write: false, 0x00);
        Assert.Equal(0x50, board.Access(0x1FF0, write: false, 0x00));
        Assert.Equal(0, board.Bank);

        board.Access(0x1FF9, write: true, 0x00);
        Assert.Equal(1, board.Bank);
    }

    [Fact]
    public void An_inc_of_a_hotspot_is_three_accesses_and_ends_in_its_bank()
    {
        // Review Focus 1, cartridge.md worked example 4: INC $1FF8 (EE F8 1F) reads it, writes the
        // old value and writes the new, and each is an access the board acts on.
        var bus = new BoardBus(Make("F8"));
        bus.Load(0x0200, 0xEE, 0xF8, 0x1F);
        Assert.Equal(1, bus.Board.Bank);

        bus.Cpu.Step();

        Assert.Equal(BoardBus.Accesses((0x1FF8, false, 0), (0x1FF8, true, 0), (0x1FF8, true, 1)), bus.BoardAccesses);
        Assert.Equal(0, bus.Board.Bank);
    }

    [Fact]
    public void A_dummy_read_of_a_hotspot_switches_as_a_real_read_does()
    {
        // cartridge.md worked example 4: LDA $1FF9,X with X=$FF makes a dummy read of $1FF8, then
        // reads $20F8, which on 13 lines is the RIOT's RAM at $00F8 and not the cartridge.
        var bus = new BoardBus(Make("F8"));
        bus.Load(0x0200, 0xA2, 0xFF, 0xBD, 0xF9, 0x1F);
        Assert.Equal(1, bus.Board.Bank);

        bus.Cpu.Step();
        bus.Cpu.Step();

        Assert.Equal(BoardBus.Accesses((0x1FF8, false, 0)), bus.BoardAccesses);
        Assert.Equal(0, bus.Board.Bank);
    }

    [Fact]
    public void An_indexed_store_to_a_hotspot_reads_it_first()
    {
        // cartridge.md worked example 4 marks this [guessing - verify]; the core, checked against
        // Harte's tests, settles it: STA $1FF0,X with X=8 reads $1FF8, then writes it.
        var bus = new BoardBus(Make("F8"));
        bus.Load(0x0200, 0xA2, 0x08, 0xA9, 0x33, 0x9D, 0xF0, 0x1F);

        bus.Cpu.Step();
        bus.Cpu.Step();
        bus.Cpu.Step();

        Assert.Equal(BoardBus.Accesses((0x1FF8, false, 0), (0x1FF8, true, 0x33)), bus.BoardAccesses);
        Assert.Equal(0, bus.Board.Bank);
    }

    [Fact]
    public void The_opcode_after_a_switching_instruction_comes_from_the_new_bank()
    {
        // cartridge.md worked example 3, opcode level: BIT $1FF9 at $1800 in bank 0; the next
        // fetch, at $1803, is from bank 1. Bank 0 has LDX #$00 there and bank 1 LDA #$11.
        byte[] image = TestImages.BankNumbers(8192, filler: false)
            .Put(0, 0x1800, 0x2C, 0xF9, 0x1F, 0xA2, 0x00)
            .Put(1, 0x1803, 0xA9, 0x11);
        var board = new F8(image);
        board.Access(0x1FF8, write: false, 0x00);
        var bus = new BoardBus(board);
        bus.Cpu.PC = 0x1800;
        bus.Cpu.A = 0x00;
        bus.Cpu.X = 0x55;

        bus.Cpu.Step();
        Assert.Equal(1, board.Bank);
        bus.Cpu.Step();

        Assert.Equal(0x11, bus.Cpu.A);
        Assert.Equal(0x55, bus.Cpu.X);
    }
}
