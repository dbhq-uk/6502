using Xunit;
using static Dbhq.Machines.Atari2600.Tests.TiaScene;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The missiles, from <c>docs/atari-2600/facts/tia.md</c> sections 9 and 11: where a RESM0 puts
/// the missile, its four widths, the copies NUSIZ gives it and the stretched settings that give
/// none, ENAM, and a reset strobed twice. RESMP is in <see cref="TiaPlayerTests"/>, with the player
/// it locks to. Drawn by a TIA alone, missile 0 in <see cref="TiaScene.P0"/>.
/// </summary>
public class TiaMissileTests
{
    /// <summary>tia.md section 9's table, for a missile: the colour clock a write lands at, and the first pixel.</summary>
    public static TheoryData<int, int> Landings() => new()
    {
        { 3, 2 },
        { 30, 2 },
        { 66, 2 },
        { 69, 5 },
        { 72, 8 },
        { 102, 38 },
        { 165, 101 },
        { 207, 143 },
        { 225, 1 },
    };

    [Theory]
    [MemberData(nameof(Landings))]
    public void Section_9_a_reset_lands_the_missile_where_the_table_says(int w, int pixel)
    {
        byte[][] rows = new TiaScene().Write(Reg.Enam0, 0x02).At(0, w, Reg.Resm0, 0).Rows(4);
        Assert.Equal([pixel], Lit(rows[3], P0));
        Assert.Equal([pixel], Lit(rows[2], P0));
    }

    [Fact]
    public void Worked_example_resm0_on_cycle_55_draws_nothing_on_its_line()
    {
        // Section 12's example: a RESM0 on cycle 55 draws nothing on that line and pixel 101 from
        // the next.
        byte[][] rows = new TiaScene().Write(Reg.Enam0, 0x02).At(1, 165, Reg.Resm0, 0).Rows(3);
        Assert.DoesNotContain(Lit(rows[1], P0), x => x > 60);
        Assert.Equal([101], Lit(rows[2], P0));
    }

    [Theory]
    [InlineData(0x00, 17, 17)]
    [InlineData(0x10, 17, 18)]
    [InlineData(0x20, 17, 20)]
    [InlineData(0x30, 17, 24)]
    public void Section_11_nusiz_d5_and_d4_give_widths_1_2_4_and_8(byte nusiz, int first, int last)
    {
        byte[][] rows = new TiaScene().Write(Reg.Nusiz0, nusiz).Write(Reg.Enam0, 0x02).At(0, 81, Reg.Resm0, 0).Rows(3);
        Assert.Equal(Span(first, last), Lit(rows[2], P0));
    }

    [Theory]
    [InlineData(0x30, new[] { 17 })]
    [InlineData(0x33, new[] { 17, 33, 49 })]
    [InlineData(0x35, new[] { 17 })]
    [InlineData(0x37, new[] { 17 })]
    [InlineData(0x34, new[] { 17, 81 })]
    [InlineData(0x36, new[] { 17, 49, 81 })]
    public void Worked_example_missile_copies_and_the_stretched_settings(byte nusiz, int[] starts)
    {
        // Section 11: NUSIZ0 = $33 gives 17 to 24, 33 to 40 and 49 to 56; $35 and $37 give 17 to 24
        // only, one copy and not stretched; the far copy is 64 after the main one.
        byte[][] rows = new TiaScene().Write(Reg.Nusiz0, nusiz).Write(Reg.Enam0, 0x02).At(0, 81, Reg.Resm0, 0).Rows(3);
        Assert.Equal(Spans([.. starts.Select(s => (s, s + 7))]), Lit(rows[2], P0));
    }

    [Fact]
    public void Section_11_enam_d1_turns_the_missile_on()
    {
        var scene = new TiaScene().Write(Reg.Enam0, 0xFD).At(0, 81, Reg.Resm0, 0);
        byte[][] rows = scene.At(2, 0, Reg.Enam0, 0x02).Rows(3);
        Assert.Empty(Lit(rows[1], P0));
        Assert.Equal([17], Lit(rows[2], P0));
    }

    [Fact]
    public void Section_11_missile_1_is_drawn_by_its_own_registers()
    {
        byte[][] rows = new TiaScene().Write(Reg.Nusiz1, 0x10).Write(Reg.Enam1, 0x02).At(0, 102, Reg.Resm1, 0).Rows(3);
        Assert.Equal([38, 39], Lit(rows[2], P1));
        Assert.Empty(Lit(rows[2], P0));
    }

    [Fact]
    public void Section_9_a_reset_strobed_twice_lands_where_the_second_puts_it()
    {
        // A read-modify-write of RESM0 strobes it twice, a cycle apart, and the second write
        // decides (the suite's cpu/rmw-strobe).
        byte[][] rows = new TiaScene().Write(Reg.Enam0, 0x02).At(0, 102, Reg.Resm0, 0).At(0, 105, Reg.Resm0, 0).Rows(3);
        Assert.Equal([41], Lit(rows[2], P0));
    }

    [Fact]
    public void Section_9_the_counter_keeps_running_under_vblank()
    {
        // tia.md section 7 [inferring]: VBLANK hides the missile and does not stop its counter.
        var scene = new TiaScene().Write(Reg.Enam0, 0x02).At(0, 102, Reg.Resm0, 0).At(1, 0, Reg.Vblank, 0x02);
        byte[][] rows = scene.At(3, 0, Reg.Vblank, 0x00).Rows(4);
        Assert.Empty(Lit(rows[2], P0));
        Assert.Equal([38], Lit(rows[3], P0));
    }
}
