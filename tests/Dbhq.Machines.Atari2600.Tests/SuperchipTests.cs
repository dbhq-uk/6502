using Dbhq.Machines.Atari2600.Boards;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The Superchip on F8, F6 and F4 (<c>docs/atari-2600/facts/cartridge.md</c> section 6): 128
/// bytes of RAM written at <c>$1000-$107F</c> and read at <c>$1080-$10FF</c>, one RAM for every
/// bank, and a read of the write port, real or dummy, writes the byte left on the data bus into the
/// cell (plan, Review Focus 1). The board under it still switches as it does alone.
/// </summary>
public class SuperchipTests
{
    public static TheoryData<string> Boards() => new() { "F8", "F6", "F4" };

    private static (int Size, int FirstHotspot, int Banks) Shape(string name) => name switch
    {
        "F8" => (8192, 0x1FF8, 2),
        "F6" => (16384, 0x1FF6, 4),
        "F4" => (32768, 0x1FF4, 8),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static Superchip Make(string name)
    {
        byte[] image = TestImages.BankNumbers(Shape(name).Size, filler: true);
        BankedBoard board = name switch
        {
            "F8" => new F8(image),
            "F6" => new F6(image),
            "F4" => new F4(image),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        return new Superchip(board);
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void The_ram_is_written_at_1000_and_read_at_1080_in_every_bank(string name)
    {
        (_, int first, int banks) = Shape(name);
        for (int bank = 0; bank < banks; bank++)
        {
            Superchip board = Make(name);
            board.Access((ushort)(first + bank), write: false, 0x00);
            for (int cell = 0; cell < 128; cell++)
            {
                board.Access((ushort)(0x1000 + cell), write: true, (byte)(cell + bank));
            }

            for (int cell = 0; cell < 128; cell++)
            {
                Assert.Equal((byte)(cell + bank), board.Access((ushort)(0x1080 + cell), write: false, 0xEE));
                Assert.Equal((byte)(cell + bank), board.Peek((ushort)(0x1080 + cell)));
            }

            Assert.Equal(bank, board.Bank);
        }
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void The_ram_is_kept_across_a_bank_switch(string name)
    {
        (_, int first, int banks) = Shape(name);
        Superchip board = Make(name);
        board.Access(0x1000, write: true, 0xA5);
        board.Access(0x107F, write: true, 0x5A);

        for (int bank = 0; bank < banks; bank++)
        {
            board.Access((ushort)(first + bank), write: true, 0x00);
            Assert.Equal(bank, board.Bank);
            Assert.Equal(0xA5, board.Access(0x1080, write: false, 0x00));
            Assert.Equal(0x5A, board.Access(0x10FF, write: false, 0x00));
        }
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void A_read_of_the_write_port_writes_the_bus_into_the_cell(string name)
    {
        Superchip board = Make(name);
        board.Access(0x1010, write: true, 0x44);

        byte read = board.Access(0x1010, write: false, 0x99);

        Assert.Equal(0x99, read);
        Assert.Equal(0x99, board.Access(0x1090, write: false, 0x00));
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void A_read_of_the_read_port_changes_nothing(string name)
    {
        Superchip board = Make(name);
        board.Access(0x1020, write: true, 0x3C);

        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0x3C, board.Access(0x10A0, write: false, (byte)(0x10 * i)));
        }

        for (int cell = 0; cell < 128; cell++)
        {
            Assert.Equal(cell == 0x20 ? 0x3C : 0x00, board.Access((ushort)(0x1080 + cell), write: false, 0xFF));
        }
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void A_write_to_the_read_port_stores_nothing(string name)
    {
        // cartridge.md worked example 5, last row: STA $1080 with A = $33 leaves cell 0 alone.
        Superchip board = Make(name);
        board.Access(0x1000, write: true, 0xA5);
        board.Access(0x1080, write: true, 0x33);
        Assert.Equal(0xA5, board.Access(0x1080, write: false, 0x00));
    }

    [Theory]
    [MemberData(nameof(Boards))]
    public void The_rom_above_the_ram_and_the_hotspots_are_the_boards(string name)
    {
        (_, int first, int banks) = Shape(name);
        Superchip board = Make(name);
        for (int bank = 0; bank < banks; bank++)
        {
            Assert.Equal(bank, board.Access((ushort)(first + bank), write: false, 0x00));
            Assert.Equal(bank, board.Bank);
            Assert.Equal(bank, board.Access(0x1100, write: false, 0x00));
            Assert.Equal(bank, board.Access(0x1FFC, write: false, 0x00));
        }
    }

    [Fact]
    public void The_sheets_worked_example_five()
    {
        // cartridge.md worked example 5, through the core.
        Superchip board = Make("F8");
        board.Access(0x1FF8, write: false, 0x00);
        var bus = new BoardBus(board);
        bus.Load(
            0x0200,
            0xA9, 0xA5,             // LDA #$A5
            0x8D, 0x00, 0x10,       // STA $1000
            0xAD, 0x80, 0x10,       // LDA $1080
            0x8D, 0xF9, 0x1F,       // STA $1FF9
            0xAD, 0x80, 0x10);      // LDA $1080
        bus.Cpu.Step();
        bus.Cpu.Step();
        bus.Cpu.Step();
        Assert.Equal(0xA5, bus.Cpu.A);
        bus.Cpu.Step();
        bus.Cpu.Step();
        Assert.Equal(1, board.Bank);
        Assert.Equal(0xA5, bus.Cpu.A);

        // LDA $1000 (AD 00 10): a read of the write port with $10, the operand's high byte, on the bus.
        bus.Load(0x0300, 0xAD, 0x00, 0x10);
        bus.Cpu.Step();
        Assert.Equal(0x10, bus.Cpu.A);
        Assert.Equal(0x10, board.Peek(0x1080));
    }

    [Fact]
    public void The_suites_page_crossing_read_stores_the_bus_residue()
    {
        // cartridge.md worked example 5, from the suite's ram-superchip: LDY #$80, LDA $F080,Y
        // makes a dummy read of $F000 (on 13 lines $1000) with $F0 on the bus, then reads $F100.
        Superchip board = Make("F8");
        board.Access(0x1000, write: true, 0x77);
        var bus = new BoardBus(board);
        bus.Load(0x0200, 0xA0, 0x80, 0xB9, 0x80, 0xF0);

        bus.Cpu.Step();
        bus.Cpu.Step();

        Assert.Equal(BoardBus.Accesses((0x1000, false, 0xF0), (0x1100, false, 1)), bus.BoardAccesses);
        Assert.Equal(0xF0, board.Peek(0x1080));
        Assert.Equal(1, bus.Cpu.A);
    }

    [Fact]
    public void A_page_crossing_indexed_load_writes_the_cell_its_dummy_read_touches()
    {
        // Review Focus 1, cartridge.md worked example 4: LDA $10F0,X with X=$20 makes a dummy read
        // of $1010, cell $10 of the write port, with $10 on the bus; then reads the ROM at $1110.
        Superchip board = Make("F8");
        var bus = new BoardBus(board);
        bus.Load(0x0200, 0xA2, 0x20, 0xBD, 0xF0, 0x10);

        bus.Cpu.Step();
        bus.Cpu.Step();

        Assert.Equal(0x10, board.Peek(0x1090));
        Assert.Equal(1, bus.Cpu.A);
    }
}
