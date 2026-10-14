using Xunit;
using static Dbhq.Machines.Atari2600.Tests.TiaScene;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// Priority and score mode, from <c>docs/atari-2600/facts/tia.md</c> section 13: which colour a
/// pixel takes where objects overlap, with CTRLPF D2 clear and set, and the playfield's colours in
/// score mode. Drawn by a TIA alone with the colours of <see cref="TiaScene"/>; the playfield
/// stripe used is PF1 = <c>$F0</c>, pixels 16 to 31 and 96 to 111.
/// </summary>
public class TiaPriorityTests
{
    // Single-width players and width-8 missiles and ball, each first lit at pixel 20, over the
    // stripe at 16 to 31 (and 96 to 111): a reset landing at clock 83 (player) or 84 (the others).
    private static TiaScene Scene(byte ctrlpf, bool p0 = false, bool p1 = false, bool m0 = false, bool m1 = false, bool ball = false, bool pf = false)
    {
        var scene = new TiaScene()
            .Write(Reg.Ctrlpf, (byte)(ctrlpf | 0x30)).Write(Reg.Nusiz0, 0x30).Write(Reg.Nusiz1, 0x30)
            .Write(Reg.Grp0, p0 ? (byte)0xFF : (byte)0).Write(Reg.Grp1, p1 ? (byte)0xFF : (byte)0)
            .Write(Reg.Enam0, m0 ? (byte)2 : (byte)0).Write(Reg.Enam1, m1 ? (byte)2 : (byte)0)
            .Write(Reg.Enabl, ball ? (byte)2 : (byte)0).Write(Reg.Pf1, pf ? (byte)0xF0 : (byte)0);
        return scene.At(0, 83, Reg.Resp0, 0).At(0, 83, Reg.Resp1, 0).At(0, 84, Reg.Resm0, 0).At(0, 84, Reg.Resm1, 0).At(0, 84, Reg.Resbl, 0);
    }

    private static byte At20(TiaScene scene) => scene.Rows(3)[2][20];

    public static TheoryData<byte, string, byte> Overlaps() => new()
    {
        // CTRLPF D2 clear: P0 and M0 over P1 and M1 over the playfield and ball over the background.
        { 0x00, "p0 pf", P0 },
        { 0x00, "p1 pf", P1 },
        { 0x00, "p0 p1", P0 },
        { 0x00, "m0 p1", P0 },
        { 0x00, "m1 pf", P1 },
        { 0x00, "ball p1", P1 },
        { 0x00, "ball m0", P0 },
        { 0x00, "ball", Pf },
        { 0x00, "pf", Pf },
        { 0x00, "m0 m1 p0 p1 ball pf", P0 },

        // CTRLPF D2 set: the playfield and ball in front of everything.
        { 0x04, "p0 pf", Pf },
        { 0x04, "ball p0", Pf },
        { 0x04, "ball m1", Pf },
        { 0x04, "p0 p1", P0 },
        { 0x04, "m1 p0", P0 },
        { 0x04, "m1", P1 },
    };

    [Theory]
    [MemberData(nameof(Overlaps))]
    public void Section_13_the_front_object_takes_the_pixel(byte ctrlpf, string objects, byte colour)
    {
        string[] on = objects.Split(' ');
        TiaScene scene = Scene(ctrlpf, on.Contains("p0"), on.Contains("p1"), on.Contains("m0"), on.Contains("m1"), on.Contains("ball"), on.Contains("pf"));
        Assert.Equal(colour, At20(scene));
    }

    [Fact]
    public void Worked_example_score_mode_takes_the_players_colours_by_half()
    {
        // Section 13: CTRLPF = $02 draws the playfield in COLUP0 on the left and COLUP1 on the
        // right, and COLUPF nowhere.
        byte[] row = new TiaScene().Write(Reg.Ctrlpf, 0x02).Write(Reg.Pf1, 0xF0).Rows(3)[2];
        Assert.Equal(Span(16, 31), Lit(row, P0));
        Assert.Equal(Span(96, 111), Lit(row, P1));
        Assert.Empty(Lit(row, Pf));
    }

    [Fact]
    public void Section_13_in_score_mode_the_playfield_takes_its_players_priority()
    {
        // 2k6specs: the left half's playfield is at player 0's level, so in front of player 1
        // [guessing - verify].
        TiaScene scene = Scene(0x02, p1: true, pf: true);
        Assert.Equal(P0, At20(scene));
    }

    [Fact]
    public void Section_13_in_score_mode_the_ball_keeps_the_playfields_colour()
    {
        // 2k6specs [guessing - verify].
        TiaScene scene = Scene(0x02, ball: true);
        Assert.Equal(Pf, At20(scene));
    }

    [Fact]
    public void Section_13_score_mode_is_ignored_when_the_playfield_is_in_front()
    {
        // 2k6specs: the score bit counts only with D2 clear [guessing - verify].
        byte[] row = new TiaScene().Write(Reg.Ctrlpf, 0x06).Write(Reg.Pf1, 0xF0).Rows(3)[2];
        Assert.Equal(Spans((16, 31), (96, 111)), Lit(row, Pf));
    }

    [Fact]
    public void Section_13_score_modes_left_half_ends_with_the_last_left_cell()
    {
        // PF2 D7 is the left half's last cell, 76 to 79, and its right-half copy 156 to 159.
        byte[] row = new TiaScene().Write(Reg.Ctrlpf, 0x02).Write(Reg.Pf2, 0x80).Rows(3)[2];
        Assert.Equal(Span(76, 79), Lit(row, P0));
        Assert.Equal(Span(156, 159), Lit(row, P1));
    }

    [Fact]
    public void Section_13_score_modes_halves_follow_the_playfields_own_halves()
    {
        // The right half is the playfield's, from the CNT decode at pixel 80, so a cell right of
        // pixel 80 is in COLUP1: PF0 = $10 repeated draws 0 to 3 and 80 to 83.
        byte[] row = new TiaScene().Write(Reg.Ctrlpf, 0x02).Write(Reg.Pf0, 0x10).Rows(3)[2];
        Assert.Equal(Span(0, 3), Lit(row, P0));
        Assert.Equal(Span(80, 83), Lit(row, P1));
    }
}
