using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The fifteen collision latches, from <c>docs/atari-2600/facts/tia.md</c> sections 2.2 and 14:
/// each set by its own pair and no other, on the read register and data line the table gives; set
/// from the objects' own outputs, in the blank as well as on the drawn line; never while VBLANK D1
/// is on; untouched by priority; and cleared by CXCLR. Drawn by a TIA alone.
/// </summary>
public class TiaCollisionTests
{
    private const int P0 = 0;
    private const int P1 = 1;
    private const int M0 = 2;
    private const int M1 = 3;
    private const int Bl = 4;
    private const int Pf = 5;

    private static readonly string[] Names = ["P0", "P1", "M0", "M1", "BL", "PF"];

    /// <summary>tia.md section 2.2's table: each pair, its read register and its line (D7 or D6).</summary>
    private static readonly (int A, int B, int Register, byte Line)[] Pairs =
    [
        (M0, P1, Reg.Cxm0p, 0x80), (M0, P0, Reg.Cxm0p, 0x40),
        (M1, P0, Reg.Cxm1p, 0x80), (M1, P1, Reg.Cxm1p, 0x40),
        (P0, Pf, Reg.Cxp0fb, 0x80), (P0, Bl, Reg.Cxp0fb, 0x40),
        (P1, Pf, Reg.Cxp1fb, 0x80), (P1, Bl, Reg.Cxp1fb, 0x40),
        (M0, Pf, Reg.Cxm0fb, 0x80), (M0, Bl, Reg.Cxm0fb, 0x40),
        (M1, Pf, Reg.Cxm1fb, 0x80), (M1, Bl, Reg.Cxm1fb, 0x40),
        (Bl, Pf, Reg.Cxblpf, 0x80),
        (P0, P1, Reg.Cxppmm, 0x80), (M0, M1, Reg.Cxppmm, 0x40),
    ];

    public static TheoryData<int> EveryPair()
    {
        var data = new TheoryData<int>();
        for (int i = 0; i < Pairs.Length; i++)
        {
            data.Add(i);
        }

        return data;
    }

    /// <summary>
    /// A TIA with the objects in <paramref name="on"/> each drawn over pixels 40 to 47 from line 1:
    /// single-width players with GRP = <c>$FF</c> reset at clock 103, width-8 missiles and ball
    /// reset at 104, and PF1 = <c>$03</c> (pixels 40 to 47 and 120 to 127). The latches are cleared
    /// in line 2's blank, so line 2 is the one measured.
    /// </summary>
    private static TiaScene Scene(params int[] on)
    {
        var scene = new TiaScene()
            .Write(Reg.Nusiz0, 0x30).Write(Reg.Nusiz1, 0x30).Write(Reg.Ctrlpf, 0x30)
            .Write(Reg.Grp0, on.Contains(P0) ? (byte)0xFF : (byte)0).Write(Reg.Grp1, on.Contains(P1) ? (byte)0xFF : (byte)0)
            .Write(Reg.Enam0, on.Contains(M0) ? (byte)2 : (byte)0).Write(Reg.Enam1, on.Contains(M1) ? (byte)2 : (byte)0)
            .Write(Reg.Enabl, on.Contains(Bl) ? (byte)2 : (byte)0).Write(Reg.Pf1, on.Contains(Pf) ? (byte)0x03 : (byte)0);
        scene.At(0, 103, Reg.Resp0, 0).At(0, 103, Reg.Resp1, 0).At(0, 104, Reg.Resm0, 0).At(0, 104, Reg.Resm1, 0).At(0, 104, Reg.Resbl, 0);
        return scene.At(2, 10, Reg.Cxclr, 0);
    }

    // The eight collision registers' D7 and D6, read at a line and clock.
    private static byte[] Latches(TiaScene scene, int line, int clock)
    {
        scene.RunTo(line, clock);
        return [.. Enumerable.Range(0, 8).Select(r => scene.Tia.Read(r))];
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void Section_14_each_latch_is_set_by_its_own_pair_and_no_other(int pair)
    {
        (int a, int b, int register, byte line) = Pairs[pair];
        byte[] latches = Latches(Scene(a, b), 3, 10);
        for (int r = 0; r < 8; r++)
        {
            Assert.True((r == register ? line : 0) == latches[r], $"{Names[a]}-{Names[b]}: register {r} reads {latches[r]:X2}");
        }
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public void Section_14_no_latch_is_set_by_one_object_alone(int pair)
    {
        (int a, int b, _, _) = Pairs[pair];
        Assert.All(Latches(Scene(a), 3, 10), l => Assert.Equal(0, l));
        Assert.All(Latches(Scene(b), 3, 10), l => Assert.Equal(0, l));
    }

    [Fact]
    public void Worked_example_all_six_set_all_fifteen()
    {
        byte[] latches = Latches(Scene(P0, P1, M0, M1, Bl, Pf), 3, 10);
        Assert.Equal(new byte[] { 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0x80, 0xC0 }, latches);
    }

    [Fact]
    public void Worked_example_all_pairs_with_the_objects_in_two_groups()
    {
        // Section 14: no playfield, P0, M0 and BL together on the left and P1 and M1 on the right:
        // CXM0P D6 (M0-P0), CXM1P D6 (M1-P1), CXP0FB D6 (P0-BL), CXM0FB D6 (M0-BL), and nothing else.
        var scene = new TiaScene()
            .Write(Reg.Nusiz0, 0x30).Write(Reg.Nusiz1, 0x30).Write(Reg.Ctrlpf, 0x30)
            .Write(Reg.Grp0, 0xFF).Write(Reg.Grp1, 0xFF).Write(Reg.Enam0, 2).Write(Reg.Enam1, 2).Write(Reg.Enabl, 2);
        scene.At(0, 103, Reg.Resp0, 0).At(0, 104, Reg.Resm0, 0).At(0, 104, Reg.Resbl, 0);
        scene.At(0, 163, Reg.Resp1, 0).At(0, 164, Reg.Resm1, 0);
        scene.At(2, 10, Reg.Cxclr, 0);
        Assert.Equal(new byte[] { 0x40, 0x40, 0x40, 0x00, 0x40, 0x00, 0x00, 0x00 }, Latches(scene, 3, 10));
    }

    [Theory]
    [InlineData(0xAA, 0x55, 0x00)]
    [InlineData(0xF0, 0x0F, 0x00)]
    [InlineData(0xAA, 0xAA, 0x80)]
    public void Worked_example_latches_follow_lit_pixels_not_spans(byte grp0, byte grp1, byte cxppmm)
    {
        // Section 14 (the suite's per-pixel): two quad players at one place.
        var scene = new TiaScene().Write(Reg.Nusiz0, 0x07).Write(Reg.Nusiz1, 0x07).Write(Reg.Grp0, grp0).Write(Reg.Grp1, grp1);
        scene.At(0, 103, Reg.Resp0, 0).At(0, 103, Reg.Resp1, 0).At(2, 10, Reg.Cxclr, 0);
        Assert.Equal(cxppmm, Latches(scene, 3, 10)[Reg.Cxppmm]);
    }

    [Fact]
    public void Section_14_the_latches_set_in_the_blank()
    {
        // Two single-width players from pixel 156 with only GRP bit 3 lit, which the scan counter
        // holds through the next line's blank (tia.md section 10) and shows at that line's pixel 0.
        // Cleared at clock 5 of the blank and read at clock 60, before any pixel: the latch is set.
        var scene = new TiaScene().Write(Reg.Grp0, 0x08).Write(Reg.Grp1, 0x08);
        scene.At(0, 219, Reg.Resp0, 0).At(0, 219, Reg.Resp1, 0);
        scene.At(3, 5, Reg.Cxclr, 0);
        Assert.Equal(0x80, Latches(scene, 3, 60)[Reg.Cxppmm]);
    }

    [Fact]
    public void Section_14_vblank_alone_stops_the_latches()
    {
        // VBLANK D1 on from line 1: the objects overlap and no latch sets; off from line 4's start,
        // and it sets on that line.
        TiaScene scene = Scene(P0, P1, M0, M1, Bl, Pf);
        scene.At(2, 20, Reg.Vblank, 0x02);
        scene.At(2, 30, Reg.Cxclr, 0);
        Assert.All(Latches(scene, 4, 0), l => Assert.Equal(0, l));
        scene.Write(Reg.Vblank, 0x00);
        Assert.Equal(new byte[] { 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0xC0, 0x80, 0xC0 }, Latches(scene, 5, 0));
    }

    [Fact]
    public void Section_14_priority_does_not_affect_the_latches()
    {
        TiaScene scene = Scene(P0, Pf);
        scene.Write(Reg.Ctrlpf, 0x34);
        Assert.Equal(0x80, Latches(scene, 3, 10)[Reg.Cxp0fb]);
    }

    [Fact]
    public void Section_14_cxclr_clears_all_fifteen_and_they_hold_until_then()
    {
        TiaScene scene = Scene(P0, P1, M0, M1, Bl, Pf);
        scene.At(3, 10, Reg.Enam0, 0);
        scene.Write(Reg.Enam1, 0).Write(Reg.Enabl, 0).Write(Reg.Grp0, 0).Write(Reg.Grp1, 0).Write(Reg.Pf1, 0);
        Assert.All(Latches(scene, 5, 0), l => Assert.NotEqual(0, l));
        scene.Write(Reg.Cxclr, 0);
        Assert.All(Latches(scene, 6, 0), l => Assert.Equal(0, l));
    }

    [Fact]
    public void Section_14_a_missile_hidden_by_resmp_collides_with_nothing()
    {
        // Locked from line 2's blank, the missile is planted where player 0 is, still over player 1
        // and the playfield, and hidden; the latches are cleared in line 3's blank.
        TiaScene scene = Scene(M0, P1, Pf);
        scene.Write(Reg.Resmp0, 0x02);
        scene.At(3, 10, Reg.Cxclr, 0);
        byte[] latches = Latches(scene, 5, 10);
        Assert.Equal(0x80, latches[Reg.Cxp1fb]);
        Assert.Equal(0, latches[Reg.Cxm0p]);
        Assert.Equal(0, latches[Reg.Cxm0fb]);
    }
}
