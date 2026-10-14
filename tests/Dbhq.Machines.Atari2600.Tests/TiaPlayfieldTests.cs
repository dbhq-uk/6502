using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The playfield, from <c>docs/atari-2600/facts/tia.md</c> section 8: twenty bits a half line, four
/// pixels a bit, in the bit order the sheet's table gives; the right half repeats them or, with
/// CTRLPF D0, reflects them; and a mid-line write shows only in the cells whose bit is read after
/// it lands. Drawn by a TIA alone, with the playfield in <c>$46</c> over a background of 0.
/// </summary>
public class TiaPlayfieldTests
{
    private const int Colupf = 0x08;
    private const int Ctrlpf = 0x0A;
    private const int Pf0 = 0x0D;
    private const int Pf1 = 0x0E;
    private const int Pf2 = 0x0F;
    private const byte Lit = 0x46;

    /// <summary>
    /// tia.md section 8's table, as cells: the register and bit of each cell 0 to 19, left to
    /// right. Cell c is pixels 4c to 4c + 3, and again from pixel 80.
    /// </summary>
    private static readonly (int Register, int Bit)[] Cells =
    [
        (Pf0, 4), (Pf0, 5), (Pf0, 6), (Pf0, 7),
        (Pf1, 7), (Pf1, 6), (Pf1, 5), (Pf1, 4), (Pf1, 3), (Pf1, 2), (Pf1, 1), (Pf1, 0),
        (Pf2, 0), (Pf2, 1), (Pf2, 2), (Pf2, 3), (Pf2, 4), (Pf2, 5), (Pf2, 6), (Pf2, 7),
    ];

    public static TheoryData<int, bool> EveryCell()
    {
        var data = new TheoryData<int, bool>();
        for (int cell = 0; cell < 20; cell++)
        {
            data.Add(cell, false);
            data.Add(cell, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryCell))]
    public void Section_8_each_bit_draws_its_four_pixels_in_each_half(int cell, bool reflect)
    {
        (int register, int bit) = Cells[cell];
        byte[] row = Line(tia =>
        {
            tia.Write(Ctrlpf, reflect ? (byte)0x01 : (byte)0x00);
            tia.Write(register, (byte)(1 << bit));
        });

        // The right half repeats the cell at 80 + 4c, or reflects it to 156 - 4c.
        int left = 4 * cell;
        int right = reflect ? 156 - (4 * cell) : 80 + (4 * cell);
        for (int x = 0; x < 160; x++)
        {
            bool lit = (x >= left && x < left + 4) || (x >= right && x < right + 4);
            Assert.True(lit == (row[x] == Lit), $"pixel {x}: {row[x]:X2}");
        }
    }

    [Theory]
    [InlineData(Pf0, 0x0F)]
    [InlineData(Pf1, 0x00)]
    [InlineData(Pf2, 0x00)]
    public void Section_8_pf0s_low_nibble_draws_nothing(int register, byte value)
    {
        byte[] row = Line(tia => tia.Write(register, value));
        Assert.All(row, p => Assert.Equal(0x00, p));
    }

    [Fact]
    public void Worked_example_pf0_50_pf1_c3_pf2_a5()
    {
        byte[] row = Line(tia =>
        {
            tia.Write(Pf0, 0x50);
            tia.Write(Pf1, 0xC3);
            tia.Write(Pf2, 0xA5);
        });

        int[][] spans = [[0, 3], [8, 11], [16, 23], [40, 47], [48, 51], [56, 59], [68, 71], [76, 79]];
        for (int x = 0; x < 160; x++)
        {
            int half = x % 80;
            bool lit = spans.Any(s => half >= s[0] && half <= s[1]);
            Assert.True(lit == (row[x] == Lit), $"pixel {x}");
        }
    }

    [Theory]
    [InlineData(false, 80)]
    [InlineData(true, 144)]
    public void Worked_example_pf0_f0_repeats_or_reflects(bool reflect, int rightStart)
    {
        byte[] row = Line(tia =>
        {
            tia.Write(Ctrlpf, reflect ? (byte)0x01 : (byte)0x00);
            tia.Write(Pf0, 0xF0);
        });
        for (int x = 0; x < 160; x++)
        {
            bool lit = x < 16 || (x >= rightStart && x < rightStart + 16);
            Assert.True(lit == (row[x] == Lit), $"pixel {x}");
        }
    }

    [Fact]
    public void Section_8_ctrlpfs_other_bits_do_not_reflect()
    {
        byte[] row = Line(tia =>
        {
            // Every bit but D0, and but D1 and D2, which task 6 gives score mode and priority.
            tia.Write(Ctrlpf, 0xF8);
            tia.Write(Pf0, 0xF0);
        });
        Assert.Equal(Lit, row[80]);
        Assert.Equal(0x00, row[144]);
    }

    [Fact]
    public void Worked_example_a_pf1_write_on_cycle_29_changes_the_cells_not_yet_read()
    {
        // PF1 from $00 to $FF landing at W 87: the cells at pixels 16 to 23 (first pixel at clock
        // 84 and 88) keep $00; 24 to 47 (from clock 92) and the right half's copy are new.
        byte[] row = Line(tia => { }, (87, Pf1, 0xFF));
        for (int x = 0; x < 160; x++)
        {
            bool lit = (x >= 24 && x < 48) || (x >= 96 && x < 128);
            Assert.True(lit == (row[x] == Lit), $"pixel {x}");
        }
    }

    [Fact]
    public void Worked_example_pf2_ff_on_cycle_45_and_00_on_cycle_48_lights_72_to_79()
    {
        byte[] row = Line(tia => { }, (135, Pf2, 0xFF), (144, Pf2, 0x00));
        for (int x = 0; x < 160; x++)
        {
            Assert.True((x >= 72 && x < 80) == (row[x] == Lit), $"pixel {x}");
        }
    }

    [Theory]
    [InlineData(-3, true)]
    [InlineData(-2, true)]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    public void Section_8_a_cell_takes_its_bit_two_clocks_before_its_first_pixel(int offset, bool shows)
    {
        // tia.md section 8: a write at W <= P - 3 shows in the cell starting at P, and at W >= P - 1
        // it does not. W = P - 2 is the sheet's open case [guessing - verify]; the model reads the
        // bit as it stands on clock P - 2, so it shows.
        foreach (int cell in new[] { 0, 7, 19 })
        {
            foreach (int half in new[] { 0, 80 })
            {
                (int register, int bit) = Cells[cell];
                int p = 68 + half + (4 * cell);
                byte[] row = Line(tia => { }, (p + offset, register, (byte)(1 << bit)));
                int x = half + (4 * cell);
                Assert.True(shows == (row[x] == Lit), $"cell {cell} at {x}, W = P {offset}");
                Assert.True(shows == (row[x + 3] == Lit), $"cell {cell} at {x + 3}, W = P {offset}");
            }
        }
    }

    [Fact]
    public void Section_8_a_reflect_write_mid_line_reflects_the_cells_read_after_it()
    {
        // The sheet does not say when the reflect bit is read; the model reads it with each cell's
        // bit [guessing - verify]. Turned on at W 160, it is first read for the right half's
        // fifth cell (fetched at clock 162), so PF0 = $10 (cell 0) is drawn at 80 to 83 unreflected
        // and again at 156 to 159, reflected; the cells between show nothing.
        byte[] row = Line(tia => tia.Write(Pf0, 0x10), (160, Ctrlpf, 0x01));
        for (int x = 0; x < 160; x++)
        {
            bool lit = x < 4 || (x >= 80 && x < 84) || x >= 156;
            Assert.True(lit == (row[x] == Lit), $"pixel {x}");
        }
    }

    [Fact]
    public void Section_8_the_playfield_colour_and_the_background_take_their_own_registers()
    {
        byte[] row = Line(tia =>
        {
            tia.Write(0x09, 0x82);
            tia.Write(Pf1, 0xFF);
        });
        Assert.All(row[..16], p => Assert.Equal(0x82, p));
        Assert.All(row[16..48], p => Assert.Equal(Lit, p));
        Assert.All(row[48..96], p => Assert.Equal(0x82, p));
    }

    // One line from power on, with COLUPF at Lit: set up in the blank by setUp, then each write
    // made with the counter at its clock, then the line's row.
    private static byte[] Line(Action<Tia> setUp, params (int Clock, int Register, byte Value)[] writes)
    {
        var tia = new Tia();
        tia.Write(Colupf, Lit);
        setUp(tia);
        foreach ((int clock, int register, byte value) in writes)
        {
            Clock.RunTo(tia, clock);
            tia.Write(register, value);
        }

        return Clock.FinishLine(tia);
    }
}
