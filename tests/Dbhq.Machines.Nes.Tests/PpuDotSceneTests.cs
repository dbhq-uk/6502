using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The PPU's registers accessed on every dot of a line, the lazy PPU against the per-dot
/// reference (<see cref="DotScenePair"/>): a visible line with sprite 0 hit and the overflow flag
/// on it, the VBlank lines, and the pre-render line on odd and even frames with rendering on and
/// off, in both regions. A program timed from the frame cannot do this on PAL, whose 3.2 dots a
/// cycle reach only 10 of every 16 dots of a line, so the pre-render line's dots 1, 257, 337 and
/// 340 are out of its reach (the lazy chips journal, "After the review: synthetic cartridges");
/// a scene puts the PPU where it starts (<see cref="Ppu.MoveTo"/>). Each scene starts a line
/// before its line, so the catch-up to the access covers a whole line and the dots of the line
/// before the access, and ends a line after, which a fast path for whole lines would take. The
/// tests are split by place so they run side by side.
/// </summary>
public class PpuDotSceneTests
{
    // The scenes' line on the picture: sprite 0 and more than eight sprites are on it.
    internal const int VisibleLine = 100;

    // The PPU's control register in the scenes: NMI on, sprites from $1000.
    internal const byte SceneCtrl = 0x88;

    /// <summary>
    /// The accesses, each a function of the dot offset of its target, the scene's PPUCTRL and
    /// PPUMASK: the access on the target dot, after any it needs at the scene's start (a scroll
    /// write after a <c>$2002</c> read, so <c>w</c> is known; a second write after its first).
    /// </summary>
    internal static readonly Dictionary<string, Func<int, byte, byte, DotAccess[]>> Kinds = new(StringComparer.Ordinal)
    {
        ["$2000 write"] = (t, ctrl, _) => [DotAccess.Write(t, 0, (byte)(ctrl ^ 0xB3))],
        ["$2001 write, rendering switched"] = (t, _, mask) => [DotAccess.Write(t, 1, (byte)(mask ^ 0x1E))],
        ["$2001 write, clips, greyscale and emphasis"] = (t, _, mask) => [DotAccess.Write(t, 1, (byte)(mask ^ 0xE7))],
        ["$2002 read"] = (t, _, _) => [DotAccess.Read(t, 2)],
        ["$2003 write"] = (t, _, _) => [DotAccess.Write(t, 3, 0x41)],
        ["$2004 write"] = (t, _, _) => [DotAccess.Write(t, 4, 0x5A)],
        ["$2004 read"] = (t, _, _) => [DotAccess.Read(t, 4)],
        ["$2005 first write"] = (t, _, _) => [DotAccess.Read(0, 2), DotAccess.Write(t, 5, 0x5D)],
        ["$2005 second write"] = (t, _, _) => [DotAccess.Read(0, 2), DotAccess.Write(0, 5, 0x21), DotAccess.Write(t, 5, 0x9E)],
        ["$2006 first write"] = (t, _, _) => [DotAccess.Read(0, 2), DotAccess.Write(t, 6, 0x2B)],
        ["$2006 second write"] = (t, _, _) => [DotAccess.Read(0, 2), DotAccess.Write(0, 6, 0x24), DotAccess.Write(t, 6, 0x6C)],
        ["$2007 read"] = (t, _, _) => [DotAccess.Read(t, 7)],
        ["$2007 write"] = (t, _, _) => [DotAccess.Write(t, 7, 0x77)],
        ["the picture read, a catch-up with no access"] = (t, _, _) => [DotAccess.Picture(t)],
    };

    public static TheoryData<string, string> KindsInBothRegions()
    {
        var data = new TheoryData<string, string>();
        foreach (string kind in Kinds.Keys)
        {
            data.Add(kind, "NTSC");
            data.Add(kind, "PAL");
        }

        return data;
    }

    /// <summary>
    /// A PPU with something on every line: tiles from a pattern whose bytes all differ nearby, a
    /// palette, both nametables full, sprite 0 on <see cref="VisibleLine"/> and ten more on the
    /// line after (<see cref="OamByte"/>), the rest spread down the picture with every attribute,
    /// and the scroll set.
    /// </summary>
    internal static Ppu Busy(Region region, byte ctrl, byte mask)
    {
        var scene = new PpuScene(region, Mirroring.Vertical);
        TestCartridge.Pattern(0x2000).CopyTo(scene.Mapper.Chr, 0);
        for (int i = 0; i < 32; i++)
        {
            scene.Poke((ushort)(0x3F00 + i), (byte)((i * 5) + 1));
        }

        for (int i = 0; i < 0x800; i++)
        {
            scene.Poke((ushort)(0x2000 + i), (byte)((i * 7) + 3));
        }

        for (int i = 0; i < 256; i++)
        {
            scene.Ppu.Oam[i] = OamByte(i);
        }

        scene.Scroll(13, 7, 0, ctrl);
        scene.Ppu.WriteRegister(1, mask);
        return scene.Ppu;
    }

    /// <summary>
    /// Sprite <c>i / 4</c>'s byte <c>i % 4</c>: sprite 0 from <see cref="VisibleLine"/> down, in
    /// front; sprites 1 to 10 from the line after, so the evaluation on the visible line finds
    /// eleven for the next and sets the overflow flag; the others spread.
    /// </summary>
    internal static byte OamByte(int i)
    {
        int sprite = i >> 2;
        return (i & 3) switch
        {
            0 => sprite == 0 ? (byte)(VisibleLine - 1) : sprite <= 10 ? (byte)VisibleLine : (byte)(sprite * 37 % 240),
            1 => (byte)((sprite * 11) + 1),
            2 => sprite == 0 ? (byte)0 : (byte)((sprite * 0x41) & 0xE3),
            _ => sprite == 0 ? (byte)120 : (byte)(sprite * 4),
        };
    }

    // The scenes for one place and one access: the target on each dot 0 to 340 of `line`, each
    // scene from dot 0 of the line before to dot 0 of the line after the next, so the catch-up to
    // the access covers a whole line and the dots of the line before the target, and the one
    // after it the rest of the line and a whole line more. The scenes run one after another on
    // one pair, so each starts by writing its PPUCTRL and PPUMASK: an access that changed them in
    // the scene before (rendering switched off, say) would otherwise carry over, and the access
    // would change nothing.
    internal static IEnumerable<DotScene> AtEveryDot(string kind, int line, bool odd, byte status, byte ctrl, byte mask)
    {
        for (int dot = 0; dot < Region.DotsPerLine; dot++)
        {
            int target = Region.DotsPerLine + dot;
            DotAccess[] accesses = [DotAccess.Write(0, 0, ctrl), DotAccess.Write(0, 1, mask), .. Kinds[kind](target, ctrl, mask)];
            yield return new DotScene($"{kind} at line {line} dot {dot} of an {(odd ? "odd" : "even")} frame, PPUMASK ${mask:X2}", line - 1, 0, odd, status, accesses, 3 * Region.DotsPerLine);
        }
    }

    internal static int RunAll(Region region, byte mask, IEnumerable<DotScene> scenes)
    {
        var pair = new DotScenePair(() => Busy(region, SceneCtrl, mask));
        foreach (DotScene scene in scenes)
        {
            pair.Run(scene);
        }

        return pair.Checks;
    }

    [Theory]
    [MemberData(nameof(KindsInBothRegions))]
    public void EveryDotOfAVisibleLine(string kind, string region)
    {
        int checks = RunAll(PpuScene.RegionNamed(region), 0x1E, AtEveryDot(kind, VisibleLine, odd: false, status: 0x00, SceneCtrl, 0x1E));
        Assert.True(checks > Region.DotsPerLine);
    }

    // The scenes' setup really puts sprite 0 hit and the overflow flag on the visible line, at
    // dots inside it, so the $2002 reads across the line see each flag change.
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void TheVisibleLineHasSprite0HitAndOverflowInsideIt(string region)
    {
        Ppu ppu = Busy(PpuScene.RegionNamed(region), SceneCtrl, 0x1E);
        ppu.MoveTo(VisibleLine - 1, 0, oddFrame: false, status: 0x00);
        while (ppu.Line != VisibleLine)
        {
            ppu.Tick();
        }

        Assert.Equal(0, ppu.PeekRegister(2) & 0x60);
        int hit = -1;
        int overflow = -1;
        for (int dot = 0; dot < Region.DotsPerLine; dot++)
        {
            int flags = ppu.PeekRegister(2);
            hit = hit < 0 && (flags & 0x40) != 0 ? dot : hit;
            overflow = overflow < 0 && (flags & 0x20) != 0 ? dot : overflow;
            ppu.Tick();
        }

        Assert.InRange(hit, 3, 257);
        Assert.InRange(overflow, 66, 257);
    }
}

/// <summary>The pre-render line's every dot (<see cref="PpuDotSceneTests"/>).</summary>
public class PpuDotScenePreRenderTests
{
    public static TheoryData<string, string, bool> Rows()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (string kind in PpuDotSceneTests.Kinds.Keys)
        {
            foreach (string region in new[] { "NTSC", "PAL" })
            {
                data.Add(kind, region, true);
                data.Add(kind, region, false);
            }
        }

        return data;
    }

    // The pre-render line's every dot, on an odd and an even frame: from the last VBlank line,
    // with the flags set, through the frame's end (on NTSC with rendering on, the odd frame's
    // dropped dot) into the next frame.
    [Theory]
    [MemberData(nameof(Rows))]
    public void EveryDotOfThePreRenderLine(string kind, string region, bool rendering)
    {
        Region r = PpuScene.RegionNamed(region);
        byte mask = rendering ? (byte)0x1E : (byte)0x00;
        var scenes = new[] { false, true }.SelectMany(odd => PpuDotSceneTests.AtEveryDot(kind, r.PreRenderLine, odd, status: 0xE0, PpuDotSceneTests.SceneCtrl, mask));
        int checks = PpuDotSceneTests.RunAll(r, mask, scenes);
        Assert.True(checks > 2 * Region.DotsPerLine);
    }
}

/// <summary>The VBlank lines' every dot (<see cref="PpuDotSceneTests"/>).</summary>
public class PpuDotSceneVblankTests
{
    public static TheoryData<string, string, int> Rows()
    {
        var data = new TheoryData<string, string, int>();
        foreach (string kind in PpuDotSceneTests.Kinds.Keys)
        {
            foreach (string region in new[] { "NTSC", "PAL" })
            {
                // The line after the picture, the line whose dot 1 sets the VBlank flag, and the
                // last line before the pre-render line.
                foreach (int line in new[] { 240, 241, PpuScene.RegionNamed(region).PreRenderLine - 1 })
                {
                    data.Add(kind, region, line);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void EveryDotOfAVblankLine(string kind, string region, int line)
    {
        Region r = PpuScene.RegionNamed(region);
        byte status = line > 241 ? (byte)0x80 : (byte)0x00;
        int checks = PpuDotSceneTests.RunAll(r, 0x1E, PpuDotSceneTests.AtEveryDot(kind, line, odd: false, status, PpuDotSceneTests.SceneCtrl, 0x1E));
        Assert.True(checks > Region.DotsPerLine);
    }
}

/// <summary>The scene tests' hook, <see cref="Ppu.MoveTo"/>: where it puts the PPU, and what it leaves alone.</summary>
public class PpuMoveToTests
{
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void ItPutsThePpuThereWithTheFlagsGivenAndLeavesTheRestAlone(string region)
    {
        Region r = PpuScene.RegionNamed(region);
        Ppu ppu = PpuDotSceneTests.Busy(r, PpuDotSceneTests.SceneCtrl, 0x1E);
        ppu.Deliver(12345);
        ppu.CatchUp();
        uint[] picture = [.. ppu.PixelsAsRun];
        ushort v = ppu.V;
        long frame = ppu.Frame;

        ppu.MoveTo(241, 0, oddFrame: true, status: 0x40);

        Assert.Equal((241, 0, true, frame), (ppu.Line, ppu.Dot, ppu.OddFrame, ppu.Frame));
        Assert.Equal(0x40, ppu.PeekRegister(2) & 0xE0);
        Assert.Equal(v, ppu.V);
        Assert.Equal(picture, ppu.PixelsAsRun);

        // NMI is on and the flag clear, so the next event is the dot that sets it, two dots on.
        Assert.Equal(ppu.LogicalDots + 2, ppu.NextEventDot);
        ppu.Tick();
        ppu.Tick();
        Assert.True(ppu.Nmi);
    }

    [Fact]
    public void OnAnOddNtscFramePastDot338TheFrameDropsItsLastDot()
    {
        Ppu ppu = PpuDotSceneTests.Busy(Region.Ntsc, PpuDotSceneTests.SceneCtrl, 0x1E);
        ppu.MoveTo(Region.Ntsc.PreRenderLine, 339, oddFrame: true, status: 0x00);
        long frame = ppu.Frame;

        ppu.Tick();

        Assert.Equal((0, 0, frame + 1), (ppu.Line, ppu.Dot, ppu.Frame));
    }

    [Fact]
    public void APositionOutsideTheFrameIsRefused()
    {
        Ppu ppu = PpuDotSceneTests.Busy(Region.Ntsc, PpuDotSceneTests.SceneCtrl, 0x1E);
        Assert.Throws<ArgumentOutOfRangeException>(() => ppu.MoveTo(Region.Ntsc.Lines, 0, false, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ppu.MoveTo(0, Region.DotsPerLine, false, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ppu.MoveTo(-1, 0, false, 0));
    }
}
