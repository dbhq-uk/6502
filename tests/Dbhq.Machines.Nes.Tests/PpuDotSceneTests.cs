using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The PPU's registers accessed on every dot of a line, the lazy PPU against the per-dot
/// reference (<see cref="DotScenePair"/>): visible lines at the top, the middle and the bottom of
/// the picture and one on which coarse Y wraps, each with sprite 0 hit and the overflow flag on
/// it; sprite 0 at each x of the sprite0 workload with the left clips off and on; two accesses on
/// one line; the VBlank lines; and the pre-render line on odd and even frames with rendering on
/// and off; in both regions. A program cannot reach every dot of PAL's pre-render line: on one of
/// PAL's dot phases its accesses land on only 10 of each 16 dots of a line, and only the reset
/// button moves it to another phase. A scene puts the PPU where it starts instead
/// (<see cref="Ppu.MoveTo"/>). Each scene starts a line before its line, so the catch-up to the
/// access covers a whole line and the dots of the line before the access, and ends a line after,
/// which a fast path for whole lines would take. The tests are split by family so they run side
/// by side.
/// </summary>
public class PpuDotSceneTests
{
    // The scenes' visible lines: the top, the middle, lower down, the last. Sprite 0 and ten more
    // sprites are put on the one a table runs on (OamByte).
    internal const int VisibleLine = 100;

    // The line on which coarse Y wraps from 29 to 0 at dot 256 (WrapPreamble).
    internal const int WrapLine = 150;

    // The PPU's control register in the scenes: NMI on, sprites from $1000.
    internal const byte SceneCtrl = 0x88;

    internal static readonly int[] VisibleLines = [1, VisibleLine, 200, 239];

    // The sprite0 workload's x's, and PPUMASK with the left clips off, both on, and each alone.
    internal static readonly int[] Sprite0Xs = [0, 1, 7, 8, 128, 248, 254, 255];
    internal static readonly byte[] ClipMasks = [0x1E, 0x18, 0x1A, 0x1C];

    /// <summary>
    /// The accesses, each a function of the dot offset of its target, the scene's PPUCTRL and
    /// PPUMASK: the access on the target dot, last, after any it needs at the scene's start (a
    /// scroll write after a <c>$2002</c> read, so <c>w</c> is known; a second write after its first).
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

    // The kinds whose access changes neither the picture nor the flags (OAMADDR does not steer
    // this model's evaluation), so in every scene of theirs sprite 0 hit and the overflow flag
    // must be set inside the line.
    private static readonly HashSet<string> Watching = new(StringComparer.Ordinal) { "$2002 read", "$2003 write", "$2004 read", "the picture read, a catch-up with no access" };

    public static TheoryData<string, string, int> VisibleRows()
    {
        var data = new TheoryData<string, string, int>();
        foreach (string kind in Kinds.Keys)
        {
            foreach (string region in new[] { "NTSC", "PAL" })
            {
                foreach (int line in VisibleLines.Append(WrapLine))
                {
                    data.Add(kind, region, line);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// A PPU with something on every line: tiles from a pattern whose bytes all differ nearby, a
    /// palette, both nametables full, sprite 0 on <paramref name="line"/> at
    /// <paramref name="sprite0X"/> and ten more on the line after (<see cref="OamByte"/>), the
    /// rest spread down the picture with every attribute, and the scroll set.
    /// </summary>
    internal static Ppu Busy(Region region, byte ctrl, byte mask, int line = VisibleLine, int sprite0X = 120)
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
            scene.Ppu.Oam[i] = OamByte(i, line, sprite0X);
        }

        scene.Scroll(ScrollX, ScrollY, 0, ctrl);
        scene.Ppu.WriteRegister(1, mask);
        return scene.Ppu;
    }

    /// <summary>
    /// Sprite <c>i / 4</c>'s byte <c>i % 4</c>: sprite 0 from <paramref name="line"/> down, in
    /// front, at <paramref name="sprite0X"/>; sprites 1 to 10 from the line after, so the
    /// evaluation on the line finds eleven for the next and sets the overflow flag; the others
    /// spread.
    /// </summary>
    internal static byte OamByte(int i, int line = VisibleLine, int sprite0X = 120)
    {
        int sprite = i >> 2;
        return (i & 3) switch
        {
            0 => sprite == 0 ? (byte)(line - 1) : sprite <= 10 ? (byte)line : (byte)(sprite * 37 % 240),
            1 => (byte)((sprite * 11) + 1),
            2 => sprite == 0 ? (byte)0 : (byte)((sprite * 0x41) & 0xE3),
            _ => sprite == 0 ? (byte)sprite0X : (byte)(sprite * 4),
        };
    }

    // What every scene writes first, at its start: its PPUCTRL and PPUMASK, so an access in the
    // scene before (rendering switched off, say) does not carry over and leave the access nothing
    // to change. On a visible line, then v, by a mid-frame scroll's writes ($2002 read, $2006,
    // $2005, $2005, $2006), so every scene draws the same lines whatever the one before left: the
    // v the frame would have on the line before (ScrollV), or on the wrap line coarse Y 29 and
    // fine Y 6, so the line before's Y increment makes fine Y 7 and the line's own, at dot 256,
    // wraps coarse Y to 0 and turns the vertical nametable.
    internal static DotAccess[] Preamble(byte ctrl, byte mask, int v = -1)
    {
        DotAccess[] registers = [DotAccess.Write(0, 0, ctrl), DotAccess.Write(0, 1, mask)];
        if (v < 0)
        {
            return registers;
        }

        int coarseX = v & 31;
        int coarseY = (v >> 5) & 31;
        int fineY = v >> 12;
        return
        [
            .. registers, DotAccess.Read(0, 2), DotAccess.Write(0, 6, (byte)((v >> 8) & 0x3F)),
            DotAccess.Write(0, 5, (byte)((coarseY << 3) | fineY)), DotAccess.Write(0, 5, (byte)((coarseX << 3) | (ScrollX % 8))), DotAccess.Write(0, 6, (byte)v),
        ];
    }

    // The scroll Busy sets, 13 across and 7 down.
    private const int ScrollX = 13;
    private const int ScrollY = 7;

    // v as the frame has it on line `line` (the background's Y is the scroll's plus the line, the
    // vertical nametable turned past 240), with coarse X the scroll's; on the wrap line's line
    // before, coarse Y 29 and fine Y 6.
    internal static int ScrollV(int line, bool wrap)
    {
        if (wrap)
        {
            return (6 << 12) | (29 << 5) | (ScrollX / 8);
        }

        int y = ScrollY + line;
        int nametable = 0;
        if (y >= 240)
        {
            y -= 240;
            nametable = 2;
        }

        return ((y & 7) << 12) | (nametable << 10) | ((y >> 3) << 5) | (ScrollX / 8);
    }

    // Where dot `dot` of `line` is from the scene's start at dot 0 of the line before: the line
    // and the dot, but on the pre-render line of an odd frame that drops its last dot, where dot
    // 340 is never run and the offset lands on line 0 dot 0 of the next frame.
    private static (int Line, int Dot) Position(Region region, int line, int dot, bool odd, byte mask) =>
        dot == 340 && line == region.PreRenderLine && odd && region.OddFrameSkipsADot && (mask & 0x18) != 0 ? (0, 0) : (line, dot);

    // The accesses of one scene: the preamble, then the kind's, each with the position it must be
    // made at.
    private static DotAccess[] Accesses(string kind, Region region, int line, int dot, bool odd, byte ctrl, byte mask, bool wrap)
    {
        DotAccess[] own = Kinds[kind](Region.DotsPerLine + dot, ctrl, mask);
        (int l, int d) = Position(region, line, dot, odd, mask);
        int v = line <= FrameBuffer.Height ? ScrollV(line - 1, wrap) : -1;
        return [.. Preamble(ctrl, mask, v).Select(a => a.At(line - 1, 0)), .. own[..^1].Select(a => a.At(line - 1, 0)), own[^1].At(l, d)];
    }

    // The scenes for one place and one access: the target on each dot 0 to 340 of `line`, each
    // scene from dot 0 of the line before to dot 0 of the line after the next, so the catch-up to
    // the access covers a whole line and the dots of the line before the target, and the one
    // after it the rest of the line and a whole line more. Each is named by where its access
    // really lands.
    internal static IEnumerable<DotScene> AtEveryDot(string kind, Region region, int line, bool odd, byte status, byte ctrl, byte mask, bool wrap = false)
    {
        for (int dot = 0; dot < Region.DotsPerLine; dot++)
        {
            (int l, int d) = Position(region, line, dot, odd, mask);
            string where = (l, d) == (line, dot) ? $"line {line} dot {dot}" : $"line {l} dot {d} of the next frame (the offset of line {line} dot {dot}, which this frame drops)";
            yield return new DotScene($"{kind} at {where} of an {(odd ? "odd" : "even")} frame, {region.Name}, PPUMASK ${mask:X2}", line - 1, 0, odd, status, Accesses(kind, region, line, dot, odd, ctrl, mask, wrap), 3 * Region.DotsPerLine);
        }
    }

    // The same, with the kind's access made twice on the line: on dot d and again on dot d + k,
    // so the catch-up between the two starts and ends inside the line.
    internal static IEnumerable<DotScene> TwiceOnTheLine(string kind, Region region, int line, int k, byte ctrl, byte mask)
    {
        for (int dot = 0; dot + k < Region.DotsPerLine; dot++)
        {
            DotAccess[] once = Accesses(kind, region, line, dot, false, ctrl, mask, wrap: false);
            DotAccess again = once[^1] with { Offset = once[^1].Offset + k, Dot = dot + k };
            yield return new DotScene($"{kind} at line {line} dots {dot} and {dot + k}, {region.Name}", line - 1, 0, false, 0x00, [.. once, again], 3 * Region.DotsPerLine);
        }
    }

    // Runs the scenes on a new pair built by `build`, and, with `line` given, checks in every
    // scene that sprite 0 hit and the overflow flag are clear as the line begins and, with
    // `inside`, that both are set by its end.
    internal static int RunAll(Func<Ppu> build, IEnumerable<DotScene> scenes, int line = -1, bool inside = false)
    {
        var pair = new DotScenePair(build);
        int start = -1;
        int end = -1;
        if (line >= 0)
        {
            pair.EachDot = ppu =>
            {
                if (ppu.Dot == 0 && ppu.Line == line)
                {
                    start = ppu.PeekRegister(2) & 0x60;
                }
                else if (ppu.Dot == 0 && ppu.Line == line + 1)
                {
                    end = ppu.PeekRegister(2) & 0x60;
                }
            };
        }

        foreach (DotScene scene in scenes)
        {
            start = end = -1;
            pair.Run(scene);
            if (line < 0)
            {
                continue;
            }

            Assert.True(start == 0, $"{scene.Name}: sprite 0 hit or overflow already set as line {line} begins (flags ${start:X2})");
            Assert.True(!inside || end == 0x60, $"{scene.Name}: sprite 0 hit and overflow not both set inside line {line} (flags ${end:X2} at its end)");
        }

        return pair.Checks;
    }

    [Theory]
    [MemberData(nameof(VisibleRows))]
    public void EveryDotOfAVisibleLine(string kind, string region, int line)
    {
        Region r = PpuScene.RegionNamed(region);
        bool wrap = line == WrapLine;

        // Odd frames as well on NTSC, the region whose odd frames differ.
        var parities = r.OddFrameSkipsADot ? new[] { false, true } : new[] { false };
        var scenes = parities.SelectMany(odd => AtEveryDot(kind, r, line, odd, status: 0x00, SceneCtrl, 0x1E, wrap));
        int checks = RunAll(() => Busy(r, SceneCtrl, 0x1E, line), scenes, line, inside: Watching.Contains(kind));
        Assert.True(checks > Region.DotsPerLine);
    }

    // On the wrap line the scenes' v really is at coarse Y 29 with fine Y 7 as dot 256 runs, and
    // coarse Y is 0 after it, with the vertical nametable turned.
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void OnTheWrapLineCoarseYWrapsFrom29InsideTheLine(string region)
    {
        Region r = PpuScene.RegionNamed(region);
        var seen = new List<(int Before, int After)>();
        int before = -1;
        var pair = new DotScenePair(() => Busy(r, SceneCtrl, 0x1E, WrapLine));
        pair.EachDot = ppu =>
        {
            if (ppu.Line == WrapLine && ppu.Dot == 256)
            {
                before = ppu.V;
            }
            else if (ppu.Line == WrapLine && ppu.Dot == 257)
            {
                seen.Add((before, ppu.V));
            }
        };
        foreach (DotScene scene in AtEveryDot("the picture read, a catch-up with no access", r, WrapLine, false, 0x00, SceneCtrl, 0x1E, wrap: true))
        {
            pair.Run(scene);
        }

        Assert.Equal(Region.DotsPerLine, seen.Count);
        foreach ((int v0, int v1) in seen)
        {
            Assert.Equal((29, 7), ((v0 >> 5) & 31, v0 >> 12));
            Assert.Equal(0, (v1 >> 5) & 31);
            Assert.NotEqual(v0 & 0x0800, v1 & 0x0800);
        }
    }
}

/// <summary>The <c>$2002</c> read on every dot of line 200, with sprite 0 at each of the sprite0 workload's x's and the left clips off and on (<see cref="PpuDotSceneTests"/>).</summary>
public class PpuDotSceneSprite0Tests
{
    private const int Line = 200;

    public static TheoryData<int, byte, string> Rows()
    {
        var data = new TheoryData<int, byte, string>();
        foreach (int x in PpuDotSceneTests.Sprite0Xs)
        {
            foreach (byte mask in PpuDotSceneTests.ClipMasks)
            {
                data.Add(x, mask, "NTSC");
                data.Add(x, mask, "PAL");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void A2002ReadOnEveryDotWithSprite0There(int x, byte mask, string region)
    {
        Region r = PpuScene.RegionNamed(region);
        int checks = PpuDotSceneTests.RunAll(
            () => PpuDotSceneTests.Busy(r, PpuDotSceneTests.SceneCtrl, mask, Line, x),
            PpuDotSceneTests.AtEveryDot("$2002 read", r, Line, false, 0x00, PpuDotSceneTests.SceneCtrl, mask),
            Line);
        Assert.True(checks > Region.DotsPerLine);
    }
}

/// <summary>Each access made twice on one line, on dot d and on d + k (<see cref="PpuDotSceneTests"/>).</summary>
public class PpuDotSceneTwiceTests
{
    public static TheoryData<string, int, string> Rows()
    {
        var data = new TheoryData<string, int, string>();
        foreach (string kind in PpuDotSceneTests.Kinds.Keys)
        {
            foreach (int k in new[] { 1, 3, 8, 64 })
            {
                data.Add(kind, k, "NTSC");
                data.Add(kind, k, "PAL");
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void TwoAccessesOnOneLine(string kind, int k, string region)
    {
        Region r = PpuScene.RegionNamed(region);
        int checks = PpuDotSceneTests.RunAll(
            () => PpuDotSceneTests.Busy(r, PpuDotSceneTests.SceneCtrl, 0x1E),
            PpuDotSceneTests.TwiceOnTheLine(kind, r, PpuDotSceneTests.VisibleLine, k, PpuDotSceneTests.SceneCtrl, 0x1E),
            PpuDotSceneTests.VisibleLine);
        Assert.True(checks > Region.DotsPerLine);
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
        var scenes = new[] { false, true }.SelectMany(odd => PpuDotSceneTests.AtEveryDot(kind, r, r.PreRenderLine, odd, status: 0xE0, PpuDotSceneTests.SceneCtrl, mask));
        int checks = PpuDotSceneTests.RunAll(() => PpuDotSceneTests.Busy(r, PpuDotSceneTests.SceneCtrl, mask), scenes);
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
        int checks = PpuDotSceneTests.RunAll(() => PpuDotSceneTests.Busy(r, PpuDotSceneTests.SceneCtrl, 0x1E), PpuDotSceneTests.AtEveryDot(kind, r, line, odd: false, status, PpuDotSceneTests.SceneCtrl, 0x1E));
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

    // The sprites fetched for the line the PPU was on are not drawn on the line it is put on: put
    // at the visible line with sprite 0 fetched for it, no hit comes on that line.
    [Fact]
    public void ItLeavesNoSpriteFetchedForTheLineItPutsThePpuOn()
    {
        Ppu ppu = PpuDotSceneTests.Busy(Region.Ntsc, PpuDotSceneTests.SceneCtrl, 0x1E);
        ppu.MoveTo(PpuDotSceneTests.VisibleLine - 1, 0, oddFrame: false, status: 0x00);
        while (ppu.Line != PpuDotSceneTests.VisibleLine)
        {
            ppu.Tick();
        }

        ppu.MoveTo(PpuDotSceneTests.VisibleLine, 0, oddFrame: false, status: 0x00);
        for (int dot = 0; dot < Region.DotsPerLine; dot++)
        {
            ppu.Tick();
        }

        Assert.Equal(0, ppu.PeekRegister(2) & 0x40);
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
