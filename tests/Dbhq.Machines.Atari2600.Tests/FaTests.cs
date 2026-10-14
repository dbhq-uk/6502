using Dbhq.Machines.Atari2600.Boards;
using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// FA, CBS RAM Plus (<c>docs/atari-2600/facts/cartridge.md</c> section 7): three banks selected at
/// <c>$1FF8</c> to <c>$1FFA</c> only when data bit D0 is 1, and 256 bytes of RAM written at
/// <c>$1000-$10FF</c> and read at <c>$1100-$11FF</c>.
/// </summary>
/// <remarks>
/// The D0 gate follows the sheet's choice in section 7.2: CBS's patent (US 4,485,457) and the
/// suite's <c>bank-fa</c> have it; Horton and Korth say any access switches. The sheet follows the
/// patent, so these tests do, and the disagreement is in <c>docs/known-differences.md</c>. On a
/// read the gate sees the hotspot's byte in the bank showing before the access, and the read
/// returns that byte (section 7.2, marked [guessing - verify] there).
/// </remarks>
public class FaTests
{
    private const int Size = 12288;

    /// <summary>
    /// The bytes of cartridge.md worked example 6, from the suite's <c>bank-fa</c>: every bank holds
    /// <c>$01</c> at its hotspots, except <c>$00</c> at <c>$1FFA</c> in bank 0 and at <c>$1FF9</c> in
    /// bank 1. The rest of each bank holds its bank number.
    /// </summary>
    private static byte[] Example6()
    {
        byte[] image = TestImages.BankNumbers(Size, filler: false);
        for (int bank = 0; bank < 3; bank++)
        {
            image.Put(bank, 0x1FF8, 0x01, 0x01, 0x01);
        }

        return image.Put(0, 0x1FFA, 0x00).Put(1, 0x1FF9, 0x00);
    }

    private static Fa InBank(int bank)
    {
        var board = new Fa(Example6());
        board.Access((ushort)(0x1FF8 + bank), write: true, 0x01);
        Assert.Equal(bank, board.Bank);
        return board;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Each_hotspot_written_with_d0_set_selects_its_bank(int to)
    {
        foreach (byte value in new byte[] { 0x01, 0x03, 0xFF })
        {
            Fa board = InBank((to + 1) % 3);
            board.Access((ushort)(0x1FF8 + to), write: true, value);
            Assert.Equal(to, board.Bank);
            Assert.Equal(to, board.Access(0x1234, write: false, 0x00));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_hotspot_written_with_d0_clear_changes_nothing(int to)
    {
        // D1 to D7 are ignored: $FE has every bit but D0.
        foreach (byte value in new byte[] { 0x00, 0x02, 0xFE })
        {
            int from = (to + 1) % 3;
            Fa board = InBank(from);
            board.Access((ushort)(0x1FF8 + to), write: true, value);
            Assert.Equal(from, board.Bank);
        }
    }

    [Fact]
    public void The_sheets_worked_example_six()
    {
        // Row 1 and 2: STA $1FF9 with D0 set and clear.
        Fa board = InBank(0);
        board.Access(0x1FF9, write: true, 0x01);
        Assert.Equal(1, board.Bank);
        board = InBank(0);
        board.Access(0x1FF9, write: true, 0x00);
        Assert.Equal(0, board.Bank);

        // Row 3: BIT $1FF9 reads bank 0's $01 there, so it switches; bank 1's own $00 plays no part.
        board = InBank(0);
        Assert.Equal(0x01, board.Access(0x1FF9, write: false, 0x00));
        Assert.Equal(1, board.Bank);

        // Row 4: BIT $1FFA reads bank 0's $00 there, so it does not.
        board = InBank(0);
        Assert.Equal(0x00, board.Access(0x1FFA, write: false, 0xFF));
        Assert.Equal(0, board.Bank);

        // Row 5: from bank 2, STA $1FF8 with $03.
        board = InBank(2);
        board.Access(0x1FF8, write: true, 0x03);
        Assert.Equal(0, board.Bank);

        // Row 6: STA $1040, LDA $1140.
        board.Access(0x1040, write: true, 0x6D);
        Assert.Equal(0x6D, board.Access(0x1140, write: false, 0x00));
    }

    [Fact]
    public void A_hotspot_read_returns_the_byte_of_the_bank_showing_before_it()
    {
        // Bank 2 holds $01 at $1FF9 and bank 1 holds $00: the read switches, and returns bank 2's.
        Fa board = InBank(2);
        Assert.Equal(0x01, board.Access(0x1FF9, write: false, 0x00));
        Assert.Equal(1, board.Bank);
    }

    [Fact]
    public void A_read_gates_on_the_rom_byte_and_not_on_the_bus()
    {
        // Bank 0's $1FFA is $00: a read with $FF left on the bus still does not switch.
        Fa board = InBank(0);
        board.Access(0x1FFA, write: false, 0xFF);
        Assert.Equal(0, board.Bank);

        // Bank 0's $1FF9 is $01: a read with $00 left on the bus still switches.
        board.Access(0x1FF9, write: false, 0x00);
        Assert.Equal(1, board.Bank);
    }

    [Fact]
    public void No_other_address_switches()
    {
        for (int bank = 0; bank < 3; bank++)
        {
            Fa board = InBank(bank);
            for (int address = 0x1000; address < 0x2000; address++)
            {
                if (address is >= 0x1FF8 and <= 0x1FFA)
                {
                    continue;
                }

                board.Access((ushort)address, write: true, 0xFF);
                board.Access((ushort)address, write: false, 0xFF);
                Assert.Equal(bank, board.Bank);
            }
        }
    }

    [Fact]
    public void The_ram_is_written_at_1000_and_read_at_1100_in_every_bank()
    {
        for (int bank = 0; bank < 3; bank++)
        {
            Fa board = InBank(bank);
            for (int cell = 0; cell < 256; cell++)
            {
                board.Access((ushort)(0x1000 + cell), write: true, (byte)(cell ^ 0xA5));
            }

            for (int cell = 0; cell < 256; cell++)
            {
                Assert.Equal((byte)(cell ^ 0xA5), board.Access((ushort)(0x1100 + cell), write: false, 0x00));
                Assert.Equal((byte)(cell ^ 0xA5), board.Peek((ushort)(0x1100 + cell)));
            }
        }
    }

    [Fact]
    public void The_ram_is_one_for_every_bank_and_kept_across_a_switch()
    {
        Fa board = InBank(0);
        board.Access(0x1000, write: true, 0x11);
        board.Access(0x10FF, write: true, 0x22);

        for (int bank = 0; bank < 3; bank++)
        {
            board.Access((ushort)(0x1FF8 + bank), write: true, 0x01);
            Assert.Equal(0x11, board.Access(0x1100, write: false, 0x00));
            Assert.Equal(0x22, board.Access(0x11FF, write: false, 0x00));
        }
    }

    [Fact]
    public void A_read_of_the_write_port_writes_the_bus_into_the_cell()
    {
        // cartridge.md section 7.1 [inferring]: the port is chosen by address alone.
        Fa board = InBank(0);
        board.Access(0x1033, write: true, 0x44);

        Assert.Equal(0x99, board.Access(0x1033, write: false, 0x99));
        Assert.Equal(0x99, board.Access(0x1133, write: false, 0x00));
    }

    [Fact]
    public void A_write_to_the_read_port_stores_nothing()
    {
        Fa board = InBank(0);
        board.Access(0x1033, write: true, 0x44);
        board.Access(0x1133, write: true, 0x99);
        Assert.Equal(0x44, board.Access(0x1133, write: false, 0x00));
    }

    [Fact]
    public void A_read_of_the_read_port_changes_nothing()
    {
        Fa board = InBank(1);
        board.Access(0x1077, write: true, 0x5A);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(0x5A, board.Access(0x1177, write: false, (byte)i));
        }

        Assert.Equal(1, board.Bank);
    }

    [Fact]
    public void The_rom_above_the_ram_answers_from_the_bank()
    {
        for (int bank = 0; bank < 3; bank++)
        {
            Fa board = InBank(bank);
            Assert.Equal(bank, board.Access(0x1200, write: false, 0x00));
            Assert.Equal(bank, board.Access(0x1FF0, write: false, 0x00));
        }
    }

    [Fact]
    public void A_page_crossing_load_reads_back_what_its_dummy_read_wrote()
    {
        // cartridge.md worked example 4, FA row: LDA $10F0,X with X=$20 makes a dummy read of
        // $1010 with $10 on the bus, the write port of cell $10, then reads $1110, its read port.
        var bus = new BoardBus(InBank(0));
        bus.Load(0x0200, 0xA2, 0x20, 0xBD, 0xF0, 0x10);

        bus.Cpu.Step();
        bus.Cpu.Step();

        Assert.Equal(BoardBus.Accesses((0x1010, false, 0x10), (0x1110, false, 0x10)), bus.BoardAccesses);
        Assert.Equal(0x10, bus.Cpu.A);
    }
}
