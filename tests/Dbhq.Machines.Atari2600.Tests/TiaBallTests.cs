using Xunit;
using static Dbhq.Machines.Atari2600.Tests.TiaScene;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The ball, from <c>docs/atari-2600/facts/tia.md</c> section 12: RESBL draws on its own line and
/// can be strobed many times a line, a reset in the blank against one on the drawn line, its four
/// widths from CTRLPF, and vertical delay. Drawn by a TIA alone, in <see cref="TiaScene.Pf"/>.
/// </summary>
public class TiaBallTests
{
    [Fact]
    public void Worked_example_resbl_on_cycle_55_draws_on_its_own_line()
    {
        // Section 12: CTRLPF = $20, ENABL = 2, RESBL on cycle 55 (W 165): pixels 101 to 104 on
        // that line and every line after.
        byte[][] rows = new TiaScene().Write(Reg.Ctrlpf, 0x20).Write(Reg.Enabl, 0x02).At(1, 165, Reg.Resbl, 0).Rows(3);
        Assert.Equal(Span(101, 104), Lit(rows[1], Pf).Where(x => x > 60));
        Assert.Equal(Span(101, 104), Lit(rows[2], Pf));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(30, 2)]
    [InlineData(66, 2)]
    [InlineData(69, 5)]
    [InlineData(72, 8)]
    [InlineData(102, 38)]
    public void Section_9_a_reset_in_the_blank_lands_two_clocks_after_it_and_one_on_the_line_four_after_the_write(int w, int pixel)
    {
        byte[][] rows = new TiaScene().Write(Reg.Enabl, 0x02).At(1, w, Reg.Resbl, 0).Rows(3);
        Assert.Equal([pixel], Lit(rows[1], Pf).Where(x => x >= 2));
        Assert.Equal([pixel], Lit(rows[2], Pf));
    }

    [Theory]
    [InlineData(0x00, 1)]
    [InlineData(0x10, 2)]
    [InlineData(0x20, 4)]
    [InlineData(0x30, 8)]
    public void Section_12_ctrlpf_d5_and_d4_give_widths_1_2_4_and_8(byte ctrlpf, int width)
    {
        byte[][] rows = new TiaScene().Write(Reg.Ctrlpf, ctrlpf).Write(Reg.Enabl, 0x02).At(0, 81, Reg.Resbl, 0).Rows(3);
        Assert.Equal(Span(17, 16 + width), Lit(rows[2], Pf));
    }

    [Fact]
    public void Section_12_the_ball_can_be_strobed_many_times_a_line()
    {
        // Towers: each RESBL starts the ball at once, so two strobes on one line draw it twice; the
        // lines after draw it where the last left it.
        byte[][] rows = new TiaScene().Write(Reg.Enabl, 0x02).At(1, 90, Reg.Resbl, 0).At(1, 120, Reg.Resbl, 0).Rows(3);
        Assert.Equal([26, 56], Lit(rows[1], Pf).Where(x => x >= 20));
        Assert.Equal([56], Lit(rows[2], Pf));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Section_12_vertical_delay_draws_the_old_enable_bit(byte vdelbl, bool drawn)
    {
        // ENABL on, GRP1 written (the old bit becomes on), ENABL off: VDELBL draws the old bit.
        byte[][] rows = new TiaScene()
            .Write(Reg.Vdelbl, vdelbl).Write(Reg.Enabl, 0x02).Write(Reg.Grp1, 0x00).Write(Reg.Enabl, 0x00)
            .At(0, 81, Reg.Resbl, 0).Rows(3);
        Assert.Equal(drawn ? [17] : [], Lit(rows[2], Pf));
    }

    [Fact]
    public void Section_12_a_grp0_write_does_not_copy_the_enable_bit()
    {
        byte[][] rows = new TiaScene()
            .Write(Reg.Vdelbl, 1).Write(Reg.Enabl, 0x02).Write(Reg.Grp0, 0x00)
            .At(0, 81, Reg.Resbl, 0).Rows(3);
        Assert.Empty(Lit(rows[2], Pf));
    }

    [Fact]
    public void Section_12_the_ball_takes_the_playfields_colour()
    {
        byte[][] rows = new TiaScene().Write(Reg.Colupf, 0x34).Write(Reg.Enabl, 0x02).At(0, 81, Reg.Resbl, 0).Rows(3);
        Assert.Equal([17], Lit(rows[2], 0x34));
    }
}
