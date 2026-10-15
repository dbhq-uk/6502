using Xunit;
using static Dbhq.Machines.Atari2600.Tests.TiaScene;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// HMOVE and the motion clocks, from <c>docs/atari-2600/facts/tia.md</c> sections 15 to 17: each
/// motion value, the comb, late HMOVE, the pulses on the drawn line, a reset that meets a pulse, and
/// a motion register rewritten while the pulses run. Drawn by a TIA alone (<see cref="TiaScene"/>):
/// a write "on cycle k" is made at colour clock W = 3k of its line, as a store after a WSYNC lands
/// (section 1).
/// </summary>
public class TiaMotionTests
{
    private const byte Background = 0x9A;

    /// <summary>tia.md section 15's table, HMOVE on cycle 3 with missile 0 from pixel 17: each value and the pixel after.</summary>
    public static TheoryData<byte, int> Values() => new()
    {
        { 0x00, 17 }, { 0x10, 16 }, { 0x20, 15 }, { 0x30, 14 }, { 0x40, 13 }, { 0x50, 12 }, { 0x60, 11 }, { 0x70, 10 },
        { 0x80, 25 }, { 0x90, 24 }, { 0xA0, 23 }, { 0xB0, 22 }, { 0xC0, 21 }, { 0xD0, 20 }, { 0xE0, 19 }, { 0xF0, 18 },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void Section_15_each_motion_value_moves_the_missile_by_its_signed_value(byte value, int pixel)
    {
        byte[][] rows = Missile(17).At(1, 0, Reg.Hmm0, value).At(2, 9, Reg.Hmove, 0).Rows(4);
        Assert.Equal([17], Lit(rows[1], P0));
        Assert.Equal([pixel], Lit(rows[2], P0));
        Assert.Equal([pixel], Lit(rows[3], P0));
    }

    [Fact]
    public void Section_15_the_low_nibble_is_ignored()
    {
        byte[][] rows = Missile(17).At(1, 0, Reg.Hmm0, 0x7F).At(2, 9, Reg.Hmove, 0).Rows(4);
        Assert.Equal([10], Lit(rows[3], P0));
    }

    [Fact]
    public void Section_15_each_hmove_applies_the_value_again()
    {
        // HMM0 = $80 and one HMOVE a line walks the missile 8 pixels right each time (hmove-walk).
        var scene = Missile(14).At(1, 0, Reg.Hmm0, 0x80);
        for (int line = 2; line < 9; line++)
        {
            scene.At(line, 9, Reg.Hmove, 0);
        }

        byte[][] rows = scene.Rows(10);
        for (int line = 2; line < 9; line++)
        {
            Assert.Equal([14 + (8 * (line - 1))], Lit(rows[line], P0));
        }

        Assert.Equal([70], Lit(rows[9], P0));
    }

    [Fact]
    public void Section_15_each_object_moves_by_its_own_register()
    {
        // Player 0 from 40, player 1 from 60, missile 1 from 80 and the ball from 100, each one pixel
        // wide but the players, which are $80: one pixel each.
        var scene = new TiaScene()
            .Write(Reg.Grp0, 0x80).Write(Reg.Grp1, 0x80).Write(Reg.Enam1, 0x02).Write(Reg.Enabl, 0x02)
            .At(0, 103, Reg.Resp0, 0).At(0, 123, Reg.Resp1, 0).At(0, 144, Reg.Resm1, 0).At(0, 164, Reg.Resbl, 0)
            .At(1, 0, Reg.Hmp0, 0x70).Write(Reg.Hmp1, 0xF0).Write(Reg.Hmm1, 0x10).Write(Reg.Hmbl, 0x90);
        byte[][] rows = scene.At(2, 9, Reg.Hmove, 0).Rows(4);
        Assert.Equal([40, 60], Lit(rows[1], P0).Concat(Lit(rows[1], P1)).Where(x => x < 70).Order());
        Assert.Equal([33], Lit(rows[3], P0));
        Assert.Equal([61, 79], Lit(rows[3], P1));
        Assert.Equal([107], Lit(rows[3], Pf));
    }

    [Fact]
    public void Section_15_hmclr_sets_every_motion_register_to_zero()
    {
        var scene = Missile(17).At(1, 0, Reg.Hmm0, 0x70).Write(Reg.Hmp0, 0x70).Write(Reg.Hmp1, 0x70).Write(Reg.Hmm1, 0x70).Write(Reg.Hmbl, 0x70);
        byte[][] rows = scene.At(1, 100, Reg.Hmclr, 0).At(2, 9, Reg.Hmove, 0).Rows(4);
        Assert.Equal([17], Lit(rows[3], P0));
        for (int register = Reg.Hmp0; register <= Reg.Hmbl; register++)
        {
            Assert.Equal(0, scene.Tia.Written(register));
        }
    }

    [Fact]
    public void Worked_example_the_comb_blanks_the_first_eight_pixels_of_the_hmove_line()
    {
        // Section 15: HMOVE on cycle 3 with a stripe at pixels 0 to 3: that line is black from
        // pixel 0 to 7, whatever the playfield or the background; the stripe at 16 to 19 stays.
        var scene = new TiaScene().Write(Reg.Colubk, Background).Write(Reg.Pf0, 0x10).Write(Reg.Pf1, 0x80);
        byte[][] rows = scene.At(2, 9, Reg.Hmove, 0).Rows(4);
        Assert.Equal(Spans((0, 3), (16, 19), (80, 83), (96, 99)), Lit(rows[1], Pf));
        Assert.Equal(Spans((16, 19), (80, 83), (96, 99)), Lit(rows[2], Pf));
        Assert.Equal(Span(0, 7), Lit(rows[2], 0));
        Assert.Equal(Background, rows[2][8]);
        Assert.Equal(Spans((0, 3), (16, 19), (80, 83), (96, 99)), Lit(rows[3], Pf));
    }

    /// <summary>
    /// tia.md section 16: where HMOVE is written decides on which line the comb falls. The line and
    /// the colour clock of the write, and the line the comb is on (-1 for none).
    /// </summary>
    public static TheoryData<int, int, int> Combs() => new()
    {
        { 2, 3, 2 },     // cycle 1
        { 2, 57, 2 },    // cycle 19
        { 2, 60, 2 },    // cycle 20, the last that combs its own line
        { 2, 63, -1 },   // cycle 21
        { 2, 165, -1 },  // cycle 55
        { 2, 222, -1 },  // cycle 74: pulses, no comb
        { 2, 225, 3 },   // cycle 75: the comb on the next line
        { 3, 0, 3 },     // cycle 76, colour clock 228: the next line's start
    };

    [Theory]
    [MemberData(nameof(Combs))]
    public void Section_16_hmove_combs_its_own_line_up_to_cycle_20_and_the_next_from_cycle_75(int line, int w, int combed)
    {
        byte[][] rows = new TiaScene().Write(Reg.Colubk, Background).At(line, w, Reg.Hmove, 0).Rows(5);
        for (int row = 1; row < 5; row++)
        {
            Assert.Equal(row == combed ? Span(0, 7) : [], Lit(rows[row], 0));
        }
    }

    /// <summary>
    /// tia.md section 16's movement table: the cycle HMOVE is written on, a motion value, and the
    /// net move, negative to the left. The cells the sheet marks as the model's own are left out.
    /// </summary>
    public static TheoryData<int, byte, int> Movements()
    {
        var data = new TheoryData<int, byte, int>();
        (int K, int D00, int D70, int D80, int Df0)[] rows =
        [
            (3, 0, -7, 8, 1), (4, 0, -6, 8, 1), (5, 0, -5, 8, 1), (10, 0, -2, 8, 1), (12, 0, 0, 8, 1),
            (14, 1, 1, 8, 1), (16, 3, 3, 8, 3), (18, 4, 4, 8, 4), (20, 6, 6, 8, 6),
            (21, 0, 0, 0, 0), (40, 0, 0, 0, 0), (54, 0, 0, 0, 0),
            (55, 0, -1, 0, 0), (60, 0, -5, 0, 0), (64, -1, -8, 0, 0), (65, -2, -9, 0, -1),
            (70, -5, -12, 0, -4), (73, -8, -15, 0, -7), (74, -8, -15, 0, -7),
            (75, 0, -7, 8, 1), (76, 0, -7, 8, 1), (80, 0, -6, 8, 1),
        ];
        foreach (var row in rows)
        {
            data.Add(row.K, 0x00, row.D00);
            data.Add(row.K, 0x70, row.D70);
            data.Add(row.K, 0x80, row.D80);
            data.Add(row.K, 0xF0, row.Df0);
        }

        data.Add(8, 0x70, -3);
        return data;
    }

    [Theory]
    [MemberData(nameof(Movements))]
    public void Section_16_late_and_early_hmove_move_by_the_pulses_that_fall_in_a_blank(int k, byte value, int move)
    {
        int w = 3 * k;
        byte[][] rows = Missile(80).At(1, 0, Reg.Hmm0, value).At(2 + (w / 228), w % 228, Reg.Hmove, 0).Rows(7);
        Assert.Equal([80], Lit(rows[1], P0));
        Assert.Equal([80 + move], Lit(rows[6], P0));
    }

    /// <summary>
    /// tia.md section 16: a 1-pixel missile under the pulses of an HMOVE on cycle 33 with HMM0 =
    /// $70, which fall on pixels 42 to 98 four apart: the dot's pixel, and what that line draws.
    /// </summary>
    public static TheoryData<int, int[]> Residues() => new()
    {
        { 96, [96] },        // x mod 4 = 0: normal
        { 97, [97] },        // 1: normal
        { 98, [] },          // 2: swallowed
        { 99, [98, 99] },    // 3: widened to x - 1 and x
        { 102, [102] },      // past the last pulse, untouched
        { 38, [38] },        // before the first pulse
    };

    [Theory]
    [MemberData(nameof(Residues))]
    public void Section_16_pulses_on_the_drawn_line_swallow_or_widen_a_dot_by_its_residue(int x, int[] drawn)
    {
        byte[][] rows = Missile(x).At(1, 0, Reg.Hmm0, 0x70).At(2, 99, Reg.Hmove, 0).Rows(4);
        Assert.Equal(drawn, Lit(rows[2], P0));
        Assert.Equal([x], Lit(rows[3], P0));
    }

    [Theory]
    [InlineData(97, true)]
    [InlineData(98, false)]
    [InlineData(99, true)]
    public void Section_14_a_swallowed_dot_collides_with_nothing_and_a_widened_one_does(int x, bool collides)
    {
        // The collision follows the serial output (the suite's stuck-drift): the playfield is lit
        // at pixels 96 to 99, under the dot.
        var scene = Missile(x).Write(Reg.Pf1, 0x80).At(1, 0, Reg.Hmm0, 0x70).At(2, 0, Reg.Cxclr, 0).At(2, 99, Reg.Hmove, 0);
        Assert.Equal(collides ? 0x80 : 0x00, scene.ReadAt(2, 220, Reg.Cxm0fb) & 0x80);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(63)]
    [InlineData(66)]
    [InlineData(69)]
    public void Section_17_a_rewrite_before_the_last_comparison_stops_the_pulses_a_step_early(int w)
    {
        // HMOVE on cycle 3, HMM0 $70, the missile at 101: $60 written on cycles 20 to 23 gives 14
        // pulses, so the missile ends at 95.
        byte[][] rows = Missile(101).At(1, 0, Reg.Hmm0, 0x70).At(2, 9, Reg.Hmove, 0).At(2, w, Reg.Hmm0, 0x60).Rows(5);
        Assert.Equal([95], Lit(rows[2], P0));
        Assert.Equal([95], Lit(rows[4], P0));
    }

    [Theory]
    [InlineData(75)]
    [InlineData(90)]
    [InlineData(105)]
    public void Section_17_a_rewrite_after_the_last_comparison_has_no_effect(int w)
    {
        byte[][] rows = Missile(101).At(1, 0, Reg.Hmm0, 0x70).At(2, 9, Reg.Hmove, 0).At(2, w, Reg.Hmm0, 0x60).Rows(5);
        Assert.Equal([94], Lit(rows[3], P0));
        Assert.Equal([94], Lit(rows[4], P0));
    }

    [Fact]
    public void Section_17_a_new_value_whose_stop_is_still_ahead_works_normally()
    {
        // $30 written on cycle 8 replaces $70 before its stop step comes: a move of 3 left.
        byte[][] rows = Missile(101).At(1, 0, Reg.Hmm0, 0x70).At(2, 9, Reg.Hmove, 0).At(2, 24, Reg.Hmm0, 0x30).Rows(4);
        Assert.Equal([98], Lit(rows[3], P0));
    }

    /// <summary>
    /// tia.md section 17: once jammed, the latch sends a pulse every 4 colour clocks, 17 a line in
    /// the blank, so the missile moves 17 left a line, and its dot cycles through the residues of
    /// section 16 on the drawn line: from 101, 94 swallowed, 77, 60, 43 widened, 26 swallowed, 9.
    /// </summary>
    private static readonly int[][] Drift = [[], [77], [60], [42, 43], [], [9]];

    [Fact]
    public void Section_17_a_rewrite_on_cycle_24_jams_the_latch_and_the_missile_drifts_17_a_line()
    {
        // The Cosmic Ark starfield: $60 written on cycle 24 misses the step for counter 1 and fails
        // the final comparison.
        byte[][] rows = Missile(101).At(1, 0, Reg.Hmm0, 0x70).At(2, 9, Reg.Hmove, 0).At(2, 72, Reg.Hmm0, 0x60).Rows(8);
        for (int line = 0; line < Drift.Length; line++)
        {
            Assert.Equal(Drift[line], Lit(rows[2 + line], P0));
        }
    }

    [Fact]
    public void Section_17_zero_written_on_cycle_19_jams_and_only_80_frees_it()
    {
        // $00 on cycle 19 comes after its stop step (counter 7, the pulse at 49). $70 on a later
        // line leaves it jammed; $80 frees it, and the missile stays where it is.
        var scene = Missile(101).At(1, 0, Reg.Hmm0, 0x70).At(2, 9, Reg.Hmove, 0).At(2, 57, Reg.Hmm0, 0x00);
        byte[][] rows = scene.At(3, 100, Reg.Hmm0, 0x70).At(7, 100, Reg.Hmm0, 0x80).Rows(10);
        for (int line = 0; line < Drift.Length; line++)
        {
            Assert.Equal(Drift[line], Lit(rows[2 + line], P0));
        }

        Assert.Equal([9], Lit(rows[8], P0));
        Assert.Equal([9], Lit(rows[9], P0));
    }

    [Fact]
    public void Section_17_hmclr_on_cycle_19_jams_every_object_at_70()
    {
        // [inferring from Towers]: HMCLR 20 cycles in has the effect of $00 on cycle 19, for all five.
        var scene = Missile(101).Write(Reg.Enabl, 0x02).At(0, 184, Reg.Resbl, 0);
        scene.At(1, 0, Reg.Hmm0, 0x70).Write(Reg.Hmbl, 0x70).At(2, 9, Reg.Hmove, 0).At(2, 57, Reg.Hmclr, 0);
        byte[][] rows = scene.Rows(5);
        Assert.Equal([77], Lit(rows[3], P0));
        Assert.Equal([60], Lit(rows[4], P0));
        Assert.Equal([120 - 7 - 17], Lit(rows[3], Pf));
    }

    [Fact]
    public void Worked_example_a_reset_and_a_pulse_on_one_clock_count_once()
    {
        // Section 15: sta WSYNC, sta HMOVE (cycle 3), sta RESBL (cycle 6), every HMxx 0: the ball
        // lands at pixel 3 (the suite's hmove-reset-merge).
        var scene = new TiaScene().Write(Reg.Enabl, 0x02).At(2, 9, Reg.Hmove, 0).At(2, 18, Reg.Resbl, 0);
        byte[][] rows = scene.Rows(4);
        Assert.Equal([3], Lit(rows[3], Pf));
    }

    /// <summary>Section 15: from the ball at pixel 3, one more HMOVE on cycle 3 with each HMBL value.</summary>
    public static TheoryData<byte, int> Walks() => new()
    {
        { 0x00, 3 }, { 0x10, 2 }, { 0x20, 1 }, { 0x30, 0 }, { 0x40, 159 }, { 0x50, 158 }, { 0x60, 157 }, { 0x70, 156 },
        { 0x80, 11 }, { 0x90, 10 }, { 0xA0, 9 }, { 0xB0, 8 }, { 0xC0, 7 }, { 0xD0, 6 }, { 0xE0, 5 }, { 0xF0, 4 },
    };

    [Theory]
    [MemberData(nameof(Walks))]
    public void Worked_example_the_ball_walked_from_pixel_3_by_each_value(byte value, int pixel)
    {
        var scene = new TiaScene().Write(Reg.Enabl, 0x02).At(2, 9, Reg.Hmove, 0).At(2, 18, Reg.Resbl, 0);
        byte[][] rows = scene.At(3, 100, Reg.Hmbl, value).At(4, 9, Reg.Hmove, 0).Rows(6);
        Assert.Equal([pixel], Lit(rows[5], Pf));
    }

    /// <summary>
    /// tia.md section 9: on a line with an early HMOVE the blank ends at colour clock 76, so a reset
    /// lands at the later of its strobe arm and its blanking arm, 79 for a player and 78 for a
    /// missile or the ball. The colour clock of the write, and the first pixel of a missile, the
    /// ball and a player (the suite's blanked-reset and copy-adjacency).
    /// </summary>
    public static TheoryData<int, int, int> BlankedResets() => new()
    {
        { 72, 10, 11 },
        { 75, 11, 12 },
        { 78, 14, 15 },
    };

    [Theory]
    [MemberData(nameof(BlankedResets))]
    public void Section_9_a_reset_under_the_comb_lands_at_the_later_arm(int w, int missile, int player)
    {
        var scene = new TiaScene().Write(Reg.Enam0, 0x02).Write(Reg.Enabl, 0x02).Write(Reg.Grp1, 0x80).At(2, 9, Reg.Hmove, 0);
        byte[][] rows = scene.At(2, w, Reg.Resm0, 0).Write(Reg.Resbl, 0).Write(Reg.Resp1, 0).Rows(4);
        Assert.Equal([missile], Lit(rows[3], P0));

        // The ball draws on its strobe line too (section 12); on the next it is behind the missile.
        Assert.Equal([missile], Lit(rows[2], Pf));
        Assert.Equal([player], Lit(rows[3], P1));
    }

    [Fact]
    public void A_second_hmove_while_the_pulses_run_does_not_restart_the_count_jigo2600_a_choice()
    {
        // [inferring from jigo2600, Visual Objects, paraphrased: SEC repeated while the counter runs
        // leaves the counter alone; the latches are set again; guessing - verify.] With HMM0 $00, a second HMOVE on cycle 12 of the same line still stops the
        // pulses at the first count's eighth, so the missile does not move; a count started again
        // would give fourteen pulses in the blank and move it 6 left.
        byte[][] rows = Missile(80).At(2, 9, Reg.Hmove, 0).At(2, 36, Reg.Hmove, 0).Rows(4);
        Assert.Equal([80], Lit(rows[3], P0));
    }

    /// <summary>A missile 0, one pixel wide, from pixel <paramref name="x"/> (4 to 159) from line 1.</summary>
    private static TiaScene Missile(int x) => new TiaScene().Write(Reg.Enam0, 0x02).At(0, x + 64, Reg.Resm0, 0);
}
