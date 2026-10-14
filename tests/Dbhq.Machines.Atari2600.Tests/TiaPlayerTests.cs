using Xunit;
using static Dbhq.Machines.Atari2600.Tests.TiaScene;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The players, from <c>docs/atari-2600/facts/tia.md</c> sections 9 and 10: where a RESP0 written
/// at a colour clock puts the player, the eight NUSIZ settings with their copies and widths,
/// reflection, the graphics registers and vertical delay, the copies a reset leaves in flight, the
/// scan counter running on past the line's end, and RESMP's lock. Drawn by a TIA alone; player 0
/// in <see cref="TiaScene.P0"/> over a background of 0. A write "at W" lands before colour clock W
/// is run, as a store on cycle k lands at W = 3k (section 1).
/// </summary>
public class TiaPlayerTests
{
    /// <summary>
    /// tia.md section 9's table, for a single-width player with GRP0 = <c>$80</c>: the colour clock
    /// the write lands at, and the first pixel of the main copy from the line after on. The rows at
    /// 3 and 30 are resets in the blank; 225 runs past the line's end and wraps to pixel 2. The
    /// row at 69 is in its own test.
    /// </summary>
    public static TheoryData<int, int> Landings() => new()
    {
        { 3, 3 },
        { 30, 3 },
        { 66, 3 },
        { 72, 9 },
        { 102, 39 },
        { 165, 102 },
        { 207, 144 },
        { 225, 2 },
    };

    [Theory]
    [MemberData(nameof(Landings))]
    public void Section_9_a_reset_lands_the_player_where_the_table_says(int w, int pixel)
    {
        byte[][] rows = new TiaScene().Write(Reg.Grp0, 0x80).At(0, w, Reg.Resp0, 0).Rows(4);
        Assert.Equal([pixel], Lit(rows[3], P0));
        Assert.Equal([pixel], Lit(rows[2], P0));
    }

    [Fact]
    public void Section_9_a_reset_at_colour_clock_69_lands_at_6_where_the_suite_measured_7()
    {
        // tia.md section 9 and open item 7: the suite measured a player reset on cycle 23 (W 69,
        // one clock after the blank ends) at pixel 7, where both arms of the landing rule give 6.
        // The model gives 6. This pins it, so a change is seen; task 7's merge-delivery-replant,
        // which measured it, decides.
        byte[][] rows = new TiaScene().Write(Reg.Grp0, 0x80).At(0, 69, Reg.Resp0, 0).Rows(3);
        Assert.Equal([6], Lit(rows[2], P0));
    }

    [Fact]
    public void Section_9_a_reset_does_not_start_the_main_copy_on_its_own_line()
    {
        byte[][] rows = new TiaScene().Write(Reg.Grp0, 0xFF).At(1, 102, Reg.Resp0, 0).Rows(3);
        Assert.DoesNotContain(Lit(rows[1], P0), x => x >= 30);
        Assert.Equal(Span(39, 46), Lit(rows[2], P0));
    }

    public static TheoryData<byte, int[]> SizesAndCopies() => new()
    {
        { 0b000, Span(3, 10) },
        { 0b001, Spans((3, 10), (19, 26)) },
        { 0b010, Spans((3, 10), (35, 42)) },
        { 0b011, Spans((3, 10), (19, 26), (35, 42)) },
        { 0b100, Spans((3, 10), (67, 74)) },
        { 0b101, Span(4, 19) },
        { 0b110, Spans((3, 10), (35, 42), (67, 74)) },
        { 0b111, Span(4, 35) },
    };

    [Theory]
    [MemberData(nameof(SizesAndCopies))]
    public void Section_10_each_nusiz_setting_draws_its_copies_and_width(byte nusiz, int[] lit)
    {
        // The section's table: GRP0 = $FF, the main copy reset in the blank (x = 3); the double and
        // quad copies start one clock later.
        byte[][] rows = new TiaScene().Write(Reg.Nusiz0, nusiz).Write(Reg.Grp0, 0xFF).At(0, 30, Reg.Resp0, 0).Rows(3);
        Assert.Equal(lit, Lit(rows[2], P0));
    }

    [Fact]
    public void Section_10_player_1_is_drawn_by_its_own_registers()
    {
        byte[][] rows = new TiaScene().Write(Reg.Nusiz1, 0b001).Write(Reg.Grp1, 0xFF).At(0, 102, Reg.Resp1, 0).Rows(3);
        Assert.Equal(Spans((39, 46), (55, 62)), Lit(rows[2], P1));
        Assert.DoesNotContain(rows[2], p => p == P0);
    }

    [Fact]
    public void Worked_example_resp0_on_cycle_55_with_a_close_copy()
    {
        // Section 10: on the strobe line only the close copy shows, at 118; from the next line 102
        // and 118 (Towers's table, row 55).
        byte[][] rows = new TiaScene().Write(Reg.Nusiz0, 0b001).Write(Reg.Grp0, 0x80).At(1, 165, Reg.Resp0, 0).Rows(3);
        Assert.Equal([118], Lit(rows[1], P0).Where(x => x > 80));
        Assert.Equal([102, 118], Lit(rows[2], P0));
    }

    [Theory]
    [InlineData(false, new[] { 37, 38, 39, 40, 45, 46, 47, 48, 49, 50, 51, 52, 57, 58, 59, 60 })]
    [InlineData(true, new[] { 45, 46, 47, 48, 53, 54, 55, 56, 57, 58, 59, 60, 65, 66, 67, 68 })]
    public void Worked_example_b4_at_quad_width_plain_and_reflected(bool reflect, int[] lit)
    {
        // A quad player from pixel 37: a single-width one from the same reset would start at 36.
        byte[][] rows = new TiaScene()
            .Write(Reg.Nusiz0, 0b111).Write(Reg.Grp0, 0xB4).Write(Reg.Refp0, reflect ? (byte)0x08 : (byte)0xF7)
            .At(0, 99, Reg.Resp0, 0).Rows(3);
        Assert.Equal(lit, Lit(rows[2], P0));
    }

    [Theory]
    [InlineData(0x00, 0x01, 10)]
    [InlineData(0x08, 0x01, 3)]
    [InlineData(0x00, 0x80, 3)]
    [InlineData(0x08, 0x80, 10)]
    public void Section_10_d7_is_drawn_first_and_reflect_draws_d0_first(byte refp0, byte grp0, int pixel)
    {
        byte[][] rows = new TiaScene().Write(Reg.Refp0, refp0).Write(Reg.Grp0, grp0).At(0, 30, Reg.Resp0, 0).Rows(3);
        Assert.Equal([pixel], Lit(rows[2], P0));
    }

    [Fact]
    public void Section_10_the_reflect_bit_is_read_at_every_pixel()
    {
        // Towers: REFP is read as each pixel is made, so a write mid-player reverses the bits not
        // yet drawn. GRP0 = $F0 from pixel 50; REFP0 set landing at clock 120 (pixel 52): pixels 50
        // and 51 are bits 7 and 6, then bits 5 down to 0 are read reversed, as bits 2 to 7.
        var scene = new TiaScene().Write(Reg.Grp0, 0xF0).At(0, 113, Reg.Resp0, 0);
        byte[][] rows = scene.At(1, 120, Reg.Refp0, 0x08).Rows(2);
        Assert.Equal([50, 51, 54, 55, 56, 57], Lit(rows[1], P0));
    }

    [Theory]
    [InlineData(1, new[] { 3, 4, 5, 6 })]
    [InlineData(0, new[] { 7, 8, 9, 10 })]
    public void Worked_example_vertical_delay(byte vdelp0, int[] lit)
    {
        // VDELP0, GRP0 = $F0, GRP1 = anything (old GRP0 becomes $F0), GRP0 = $0F (new GRP0).
        byte[][] rows = new TiaScene()
            .Write(Reg.Vdelp0, vdelp0).Write(Reg.Grp0, 0xF0).Write(Reg.Grp1, 0x00).Write(Reg.Grp0, 0x0F)
            .At(0, 30, Reg.Resp0, 0).Rows(3);
        Assert.Equal(lit, Lit(rows[2], P0));
    }

    [Theory]
    [InlineData(1, new[] { 3, 4, 5, 6 })]
    [InlineData(0, new[] { 7, 8, 9, 10 })]
    public void Section_10_vertical_delay_for_player_1_is_copied_by_grp0(byte vdelp1, int[] lit)
    {
        byte[][] rows = new TiaScene()
            .Write(Reg.Vdelp1, vdelp1).Write(Reg.Grp1, 0xF0).Write(Reg.Grp0, 0x00).Write(Reg.Grp1, 0x0F)
            .At(0, 30, Reg.Resp1, 0).Rows(3);
        Assert.Equal(lit, Lit(rows[2], P1));
    }

    [Fact]
    public void Section_10_a_grp1_write_does_not_copy_into_player_1s_own_old_register()
    {
        // GRP1 copies new GRP0 to old GRP0, never its own new to its own old.
        byte[][] rows = new TiaScene().Write(Reg.Vdelp1, 1).Write(Reg.Grp1, 0xFF).At(0, 30, Reg.Resp1, 0).Rows(3);
        Assert.DoesNotContain(rows[2], p => p == P1);
    }

    [Fact]
    public void Section_4_a_grp0_write_mid_player_shows_a_clock_after_it_lands()
    {
        // Player 0 at 50 (clock 118), single width, all bits lit; GRP0 = 0 landing at clock 121
        // (pixel 53): pixel 53 still shows the old bits, 54 on the new (section 4's W + 1).
        var scene = new TiaScene().Write(Reg.Grp0, 0xFF).At(0, 113, Reg.Resp0, 0);
        byte[][] rows = scene.At(1, 121, Reg.Grp0, 0x00).Rows(2);
        Assert.Equal(Span(50, 53), Lit(rows[1], P0));
    }

    [Fact]
    public void Section_10_a_player_past_the_right_edge_finishes_on_the_next_line()
    {
        // Single width from pixel 156: four pixels on the line, the other four from the next
        // line's pixel 0 (Towers: the scan counter is never reset).
        byte[][] rows = new TiaScene().Write(Reg.Grp0, 0xFF).At(0, 219, Reg.Resp0, 0).Rows(4);
        Assert.Equal(Spans((0, 3), (156, 159)), Lit(rows[3], P0));
    }

    [Fact]
    public void Section_9_a_copy_decoded_before_a_reset_rides_through_it()
    {
        // NUSIZ0 close copies, GRP0 = $F0. A reset at 93 decodes its close copy at clock 109, due
        // at pixel 46; a second reset at 111 holds it and it starts 5 clocks after the second
        // reset, at 48 (tia.md section 9, Towers and the suite's resp-restrobe). The second reset's
        // own close copy is at 64.
        byte[][] rows = new TiaScene().Write(Reg.Nusiz0, 0b001).Write(Reg.Grp0, 0xF0)
            .At(1, 93, Reg.Resp0, 0).At(1, 111, Reg.Resp0, 0).Rows(2);
        Assert.Equal(Spans((48, 51), (64, 67)), Lit(rows[1], P0).Where(x => x >= 40 && x < 80));
    }

    [Fact]
    public void Section_9_a_copy_lands_naturally_with_no_second_reset()
    {
        byte[][] rows = new TiaScene().Write(Reg.Nusiz0, 0b001).Write(Reg.Grp0, 0xF0).At(1, 93, Reg.Resp0, 0).Rows(2);
        Assert.Equal(Span(46, 49), Lit(rows[1], P0).Where(x => x >= 40 && x < 80));
    }

    [Theory]
    [InlineData(0b001, true)]
    [InlineData(0b000, false)]
    public void Section_9_nusiz_is_read_as_the_decode_becomes_the_start(byte late, bool copy)
    {
        // Towers: a decode is "ANDed with flags from the NUSIZ register before being latched". A
        // reset at 100 decodes count 3 at clock 116 and delivers it at 120; NUSIZ0 written at 118,
        // between the two, decides whether the close copy at pixel 53 is drawn [guessing - verify].
        var scene = new TiaScene().Write(Reg.Nusiz0, (byte)(late ^ 0b001)).Write(Reg.Grp0, 0x80).At(1, 100, Reg.Resp0, 0);
        byte[][] rows = scene.At(1, 118, Reg.Nusiz0, late).Rows(2);
        Assert.Equal(copy, Lit(rows[1], P0).Contains(53));
    }

    [Fact]
    public void Section_10_a_start_while_the_player_is_drawing_restarts_its_scan()
    {
        // A quad player from pixel 4 (scan count 0 from clock 72); NUSIZ0 = %001 written at clock
        // 84, as its scan reaches 3, makes it single width with a close copy, whose decode is
        // delivered at 86 while the scan is at 5. The model restarts the scan, so the eight bits
        // run again from clock 87, and the player is lit from 4 to 26 [guessing - verify: Towers
        // says only that the scan counter is never reset otherwise].
        var scene = new TiaScene().Write(Reg.Nusiz0, 0b111).Write(Reg.Grp0, 0xFF).At(0, 30, Reg.Resp0, 0);
        byte[][] rows = scene.At(2, 84, Reg.Nusiz0, 0b001).Rows(3);
        Assert.Equal(Span(4, 26), Lit(rows[2], P0).Where(x => x < 40));
    }

    [Fact]
    public void Section_9_a_reset_cuts_off_a_start_the_scan_has_not_taken()
    {
        // A player at 145 with a close copy: the copy's start comes as each line ends and waits
        // through the next line's blank for the scan counter's first clock, to draw at pixels 1 to
        // 8 (line 1 shows the one the strobe line's decode made). A RESP0 in
        // that blank cuts it off, as a reset cuts off a start being drawn (tia.md section 9.1)
        // [guessing - verify: the suite's resbl-kill says so for the ball only].
        var scene = new TiaScene().Write(Reg.Nusiz0, 0b001).Write(Reg.Grp0, 0xFF).At(0, 208, Reg.Resp0, 0);
        byte[][] rows = scene.At(2, 30, Reg.Resp0, 0).Rows(4);
        Assert.Equal(Spans((1, 8), (145, 152)), Lit(rows[1], P0));
        // Line 2 shows only the new reset's close copy, decoded before its counter wraps.
        Assert.Equal(Span(19, 26), Lit(rows[2], P0));
        Assert.Equal(Spans((3, 10), (19, 26)), Lit(rows[3], P0));
    }

    [Fact]
    public void Section_9_a_reset_before_the_decode_loses_the_copy()
    {
        // A second reset landing at 108, before the first reset's close decode at 109: the counter
        // never reaches it, so only the second reset's close copy shows, at 61 (Towers: the start
        // "lost or delayed").
        byte[][] rows = new TiaScene().Write(Reg.Nusiz0, 0b001).Write(Reg.Grp0, 0xF0)
            .At(1, 93, Reg.Resp0, 0).At(1, 108, Reg.Resp0, 0).Rows(2);
        Assert.Equal(Span(61, 64), Lit(rows[1], P0).Where(x => x >= 40 && x < 80));
    }

    [Fact]
    public void Section_9_resets_every_line_hide_the_main_copy()
    {
        // Towers: a player reset twice on every line, less than 160 clocks apart, never shows its
        // main copy; with no copies in NUSIZ it shows nothing.
        var scene = new TiaScene().Write(Reg.Grp0, 0xFF);
        for (int line = 0; line < 4; line++)
        {
            scene.At(line, 90, Reg.Resp0, 0).At(line, 200, Reg.Resp0, 0);
        }

        byte[][] rows = scene.Rows(4);
        Assert.All(rows, row => Assert.DoesNotContain(row, p => p == P0));
    }

    [Theory]
    [InlineData(0b000, 54)]
    [InlineData(0b101, 57)]
    [InlineData(0b111, 61)]
    public void Section_11_resmp_hides_the_missile_and_releases_it_by_the_players_width(byte nusiz, int pixel)
    {
        // Player 0 reset to single-width pixel 50 (a stretched one starts at 51), GRP0 = 0 so only
        // the missile shows; RESMP0 locks missile 0 on lines 1 and 2, and is cleared in line 3's
        // blank: the missile lands 4, 6 or 10 pixels right of the player's first pixel (the suite's
        // missile-reset-lock, a trusted test of task 7).
        var scene = new TiaScene().Write(Reg.Nusiz0, nusiz).Write(Reg.Enam0, 0x02).Write(Reg.Resm0, 0)
            .At(0, 113, Reg.Resp0, 0).At(0, 200, Reg.Resmp0, 0x02);
        byte[][] rows = scene.At(3, 10, Reg.Resmp0, 0x00).Rows(5);
        Assert.DoesNotContain(rows[1], p => p == P0);
        Assert.DoesNotContain(rows[2], p => p == P0);
        Assert.Equal([pixel], Lit(rows[4], P0));
    }

    [Fact]
    public void Section_11_resmp_follows_the_main_copy_only()
    {
        // With a close copy, the missile still locks to the main copy at 50.
        var scene = new TiaScene().Write(Reg.Nusiz0, 0b001).Write(Reg.Enam0, 0x02)
            .At(0, 113, Reg.Resp0, 0).At(0, 200, Reg.Resmp0, 0x02);
        byte[][] rows = scene.At(3, 10, Reg.Resmp0, 0x00).Rows(5);
        Assert.Equal([54, 70], Lit(rows[4], P0));
    }
}
