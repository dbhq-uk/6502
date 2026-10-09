using System.Runtime.CompilerServices;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The fast scanline renderer (<c>PpuScanline.cs</c>, task 3 of the lazy chips plan): a catch-up
/// that owes every dot of a line from its dot 0 may run the line at once, when the board does not
/// watch the PPU's address bus and the line is a VBlank or post-render line, a visible line with
/// rendering off, or a visible line with the background on and the sprites off. Each family of
/// scenes runs on the per-dot reference and on the lazy PPU (<see cref="DotScenePair"/>), which
/// must agree on the whole state and the picture after every access and at every line's end; and
/// each scene says how many of its lines the fast path must take, so a fast path that declined
/// everything would fail, and so would one that took a line it must not: the pre-render line, a
/// line with the sprites on, a line a register access or a board write falls inside, any line of
/// a board that watches, and a line of tiles from a board without pattern windows.
/// </summary>
public class PpuScanlineTests
{
    // NMI on, sprites from $1000, the background from $0000.
    internal const byte Ctrl = 0x88;

    // The background on and the sprites off: the left clip on and off, the sprite clip bit (which
    // changes nothing with the sprites off), greyscale, and each emphasis bit and all three.
    internal static readonly byte[] BackgroundMasks = [0x08, 0x0A, 0x0E, 0x09, 0x0B, 0x2A, 0x4A, 0x8A, 0xEB];

    // Rendering off: greyscale, the clip bits, and each emphasis bit.
    internal static readonly byte[] OffMasks = [0x00, 0x01, 0x06, 0x20, 0x40, 0x80, 0xE1];

    // The sprites on, with the background or without: lines the fast path must refuse.
    internal static readonly byte[] SpriteMasks = [0x10, 0x14, 0x18, 0x1A, 0x1C, 0x1E, 0xFF];

    internal static readonly int[] Lines = [1, 100, 200, 239];

    /// <summary>
    /// Whether the fast path may take a whole line of a quiet board (one that does not watch and
    /// has pattern windows) with PPUMASK <paramref name="mask"/>: any line but the pre-render line
    /// that is in VBlank or after the picture, and a visible line with the sprites off.
    /// </summary>
    internal static bool Fast(Region region, int line, byte mask) =>
        line != region.PreRenderLine && (line >= FrameBuffer.Height || (mask & 0x10) == 0);

    /// <summary>
    /// The writes that set <c>v</c> and <c>t</c> both to <paramref name="v"/> and fine X to
    /// <paramref name="fineX"/>, as a mid-frame scroll does: a <c>$2002</c> read, then <c>$2006</c>,
    /// <c>$2005</c>, <c>$2005</c>, <c>$2006</c>, all at <paramref name="offset"/>.
    /// </summary>
    internal static DotAccess[] SetScroll(int offset, int v, int fineX) =>
    [
        DotAccess.Read(offset, 2),
        DotAccess.Write(offset, 6, (byte)((v >> 8) & 0x3F)),
        DotAccess.Write(offset, 5, (byte)((((v >> 5) & 31) << 3) | (v >> 12))),
        DotAccess.Write(offset, 5, (byte)(((v & 31) << 3) | fineX)),
        DotAccess.Write(offset, 6, (byte)v),
    ];

    /// <summary>The writes that set <c>v</c> to <paramref name="v"/> by <c>$2006</c> alone, after a <c>$2002</c> read.</summary>
    internal static DotAccess[] SetV(int offset, int v) =>
    [
        DotAccess.Read(offset, 2),
        DotAccess.Write(offset, 6, (byte)(v >> 8)),
        DotAccess.Write(offset, 6, (byte)v),
    ];

    // The dots of a line: 341, but the pre-render line of an odd frame with rendering on drops one
    // where the region drops it.
    internal static int LineLength(Region region, int line, bool odd, byte mask) =>
        line == region.PreRenderLine && odd && region.OddFrameSkipsADot && (mask & 0x18) != 0 ? Region.DotsPerLine - 1 : Region.DotsPerLine;

    /// <summary>
    /// A scene of <paramref name="count"/> whole lines from dot 0 of <paramref name="first"/>:
    /// <paramref name="setup"/> before its first dot, then the picture read at each line's end, so
    /// each catch-up is one whole line, and after it the accesses <paramref name="between"/> gives
    /// for that line end (its number from 1), made at the same dot. <paramref name="mask"/> is the
    /// PPUMASK in force on the pre-render line, which decides an odd frame's dropped dot.
    /// </summary>
    internal static DotScene WholeLines(string name, Region region, int first, bool odd, byte status, byte mask, IEnumerable<DotAccess> setup, int count, Func<int, int, IEnumerable<DotAccess>>? between = null)
    {
        var accesses = new List<DotAccess>(setup.Select(a => a.At(first, 0)));
        int offset = 0;
        int line = first;
        bool frameOdd = odd;
        for (int i = 1; i <= count; i++)
        {
            offset += LineLength(region, line, frameOdd, mask);
            if (++line == region.Lines)
            {
                line = 0;
                frameOdd = !frameOdd;
            }

            accesses.Add(DotAccess.Picture(offset).At(line, 0));
            if (between is not null)
            {
                int at = line;
                accesses.AddRange(between(i, offset).Select(a => a.At(at, 0)));
            }
        }

        return new DotScene($"{name}, {region.Name}", first, 0, odd, status, accesses, offset);
    }

    /// <summary>
    /// Runs <paramref name="scene"/> on <paramref name="pair"/> and checks that the fast path took
    /// <paramref name="fast"/> lines of it on the lazy PPU, and none ever on the reference, whose
    /// catch-ups are one dot.
    /// </summary>
    internal static void Run(DotScenePair pair, DotScene scene, int fast)
    {
        long before = pair.Lazy.FastLines;
        pair.Run(scene);
        Assert.True(pair.Reference.FastLines == 0, $"{scene.Name}: the reference took the fast path");
        long taken = pair.Lazy.FastLines - before;
        if (taken != fast)
        {
            Assert.Fail($"{scene.Name}: the fast path took {taken} lines, where {fast} may be taken");
        }
    }

    /// <summary>
    /// A PPU on <paramref name="board"/>, set up as <see cref="PpuDotSceneTests.Busy"/> sets one:
    /// a palette, both nametables full (the board's mirroring must be vertical), sprite 0 on
    /// <paramref name="line"/> and ten more on the line after, the scroll, and the registers.
    /// </summary>
    internal static Ppu Build(Region region, IMapper board, byte ctrl, byte mask, int line = PpuDotSceneTests.VisibleLine)
    {
        var ppu = new Ppu(region, board);
        ppu.PowerOn();
        for (int i = 0; i < 32; i++)
        {
            Poke(ppu, (ushort)(0x3F00 + i), (byte)((i * 5) + 1));
        }

        for (int i = 0; i < 0x800; i++)
        {
            Poke(ppu, (ushort)(0x2000 + i), (byte)((i * 7) + 3));
        }

        for (int i = 0; i < 256; i++)
        {
            ppu.Oam[i] = PpuDotSceneTests.OamByte(i, line);
        }

        ppu.ReadRegister(2);
        ppu.WriteRegister(0, ctrl);
        ppu.WriteRegister(5, 13);
        ppu.WriteRegister(5, 7);
        ppu.WriteRegister(1, mask);
        return ppu;
    }

    private static void Poke(Ppu ppu, ushort address, byte value)
    {
        ppu.ReadRegister(2);
        ppu.WriteRegister(6, (byte)(address >> 8));
        ppu.WriteRegister(6, (byte)address);
        ppu.WriteRegister(7, value);
    }

    // The scroll table: each fine X; coarse X at both ends and across an attribute quadrant's
    // edge; coarse Y at the top, across the quadrants, before and at the wrap from 29, and in the
    // attribute rows 30 and 31, which wrap to 0 without turning the nametable, each with fine Y 0
    // and 7 (7 makes the line's Y increment carry into coarse Y); and each nametable.
    internal static IEnumerable<(string Name, int V, int FineX)> Scrolls()
    {
        const int Base = (3 << 12) | (5 << 5) | 13;
        for (int fineX = 0; fineX < 8; fineX++)
        {
            yield return ($"fine X {fineX}", Base, fineX);
        }

        foreach (int coarseX in new[] { 0, 1, 2, 3, 29, 30, 31 })
        {
            yield return ($"coarse X {coarseX}", (Base & ~31) | coarseX, 3);
        }

        foreach (int coarseY in new[] { 0, 1, 2, 3, 27, 28, 29, 30, 31 })
        {
            foreach (int fineY in new[] { 0, 7 })
            {
                yield return ($"coarse Y {coarseY} fine Y {fineY}", (fineY << 12) | (coarseY << 5) | 6, 5);
            }
        }

        for (int nametable = 0; nametable < 4; nametable++)
        {
            yield return ($"nametable {nametable}", Base | (nametable << 10), 6);
        }
    }

    // The PPUCTRL the scroll table's rows rotate through: NMI on or off, the background's pattern
    // table at $0000 or $1000, 8 by 16 sprites (whose evaluation still runs with the sprites off).
    private static readonly byte[] Ctrls = [0x88, 0x98, 0xA8, 0x00, 0x10];

    public static TheoryData<string, byte, int> BackgroundRows()
    {
        var data = new TheoryData<string, byte, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (byte mask in BackgroundMasks)
            {
                foreach (int line in Lines)
                {
                    data.Add(region, mask, line);
                }
            }
        }

        return data;
    }

    // The background alone across the scroll table: three whole lines from the line before, each
    // taken by the fast path, with the state and the picture the per-dot path's at each line's end.
    [Theory]
    [MemberData(nameof(BackgroundRows))]
    public void BackgroundLinesAcrossTheScroll(string region, byte mask, int line)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, mask, line, quiet: true));
        int i = 0;
        foreach ((string name, int v, int fineX) in Scrolls())
        {
            byte ctrl = Ctrls[i++ % Ctrls.Length];
            DotAccess[] setup = [DotAccess.Write(0, 0, ctrl), DotAccess.Write(0, 1, mask), .. SetScroll(0, v, fineX)];
            Run(pair, WholeLines($"{name}, PPUCTRL ${ctrl:X2}, PPUMASK ${mask:X2}, from line {line - 1}", r, line - 1, false, 0x00, mask, setup, 3), 3);
        }
    }

    public static TheoryData<string, Mirroring> MirroringRows()
    {
        var data = new TheoryData<string, Mirroring>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (Mirroring mirroring in new[] { Mirroring.Horizontal, Mirroring.SingleScreenLow, Mirroring.SingleScreenHigh, Mirroring.FourScreen, Mirroring.Vertical })
            {
                data.Add(region, mirroring);
            }
        }

        return data;
    }

    // The scroll table under each nametable layout, every page of it filled with its own bytes,
    // so a nametable fetch that picks the wrong page (one bit of the nametable select dropped,
    // say, which vertical mirroring alone would hide) draws other tiles and attributes: each
    // nametable select, and the coarse Y wrap from 29, which turns the vertical nametable.
    [Theory]
    [MemberData(nameof(MirroringRows))]
    public void BackgroundLinesUnderEachMirroring(string region, Mirroring mirroring)
    {
        Region r = PpuScene.RegionNamed(region);
        foreach (byte mask in new byte[] { 0x0A, 0x08 })
        {
            var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, mask, PpuDotSceneTests.VisibleLine, quiet: true, mirroring: mirroring));
            foreach (int line in new[] { 1, PpuDotSceneTests.VisibleLine, 239 })
            {
                int i = 0;
                foreach ((string name, int v, int fineX) in Scrolls())
                {
                    byte ctrl = Ctrls[i++ % Ctrls.Length];
                    DotAccess[] setup = [DotAccess.Write(0, 0, ctrl), DotAccess.Write(0, 1, mask), .. SetScroll(0, v, fineX)];
                    Run(pair, WholeLines($"{mirroring}, {name}, PPUCTRL ${ctrl:X2}, PPUMASK ${mask:X2}, from line {line - 1}", r, line - 1, false, 0x00, mask, setup, 3), 3);
                }
            }
        }
    }

    // The same on a real NROM board, its CHR ROM read through its windows.
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void BackgroundLinesOnNrom(string region)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => Build(r, Cartridge.Load(CatchUpScenes.Nrom()).CreateMapper(), Ctrl, 0x0A));
        foreach (int line in Lines)
        {
            foreach ((string name, int v, int fineX) in Scrolls())
            {
                DotAccess[] setup = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, 0x0A), .. SetScroll(0, v, fineX)];
                Run(pair, WholeLines($"NROM, {name}, from line {line - 1}", r, line - 1, false, 0x00, 0x0A, setup, 3), 3);
            }
        }
    }

    public static TheoryData<string, byte, bool> PreRenderRows()
    {
        var data = new TheoryData<string, byte, bool>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (byte mask in BackgroundMasks.Concat(OffMasks).Concat(SpriteMasks))
            {
                data.Add(region, mask, false);
                data.Add(region, mask, true);
            }
        }

        return data;
    }

    // Review Focus 2's dropped dot: the pre-render line is left to the per-dot path, and the lines
    // after it, from line 0 dot 0 of the next frame (one dot sooner on an odd NTSC frame with
    // rendering on), are taken.
    [Theory]
    [MemberData(nameof(PreRenderRows))]
    public void TheLinesAfterThePreRenderLine(string region, byte mask, bool odd)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, mask, 1, quiet: true));
        foreach ((string name, int v, int fineX) in Scrolls().Take(12))
        {
            DotAccess[] setup = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, mask), .. SetScroll(0, v, fineX)];
            int fast = (Fast(r, 0, mask) ? 1 : 0) + (Fast(r, 1, mask) ? 1 : 0);
            Run(pair, WholeLines($"{name}, PPUMASK ${mask:X2}, the pre-render line of an {(odd ? "odd" : "even")} frame", r, r.PreRenderLine, odd, 0xE0, mask, setup, 3), fast);
        }
    }

    public static TheoryData<string, byte, int> OffRows()
    {
        var data = new TheoryData<string, byte, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (byte mask in OffMasks)
            {
                foreach (int line in Lines)
                {
                    data.Add(region, mask, line);
                }
            }
        }

        return data;
    }

    // Visible lines with rendering off: the backdrop, or the palette entry v points at (each of
    // the palette's mirrors, and v with bit 14 set), and the backdrop's colour changed at a line
    // boundary by a $2007 write, which also moves v on (by 1 or by 32) to the next entry.
    [Theory]
    [MemberData(nameof(OffRows))]
    public void RenderingOffLines(string region, byte mask, int line)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, mask, line, quiet: true));
        foreach (int v in new[] { 0x2000, 0x23C5, 0x2F05, 0x3EFF, 0x3F00, 0x3F05, 0x3F10, 0x3F13, 0x3F14, 0x3F1F, 0x3FE7, 0x7F0A, 0x1F05 })
        {
            DotAccess[] setup = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, mask), .. SetV(0, v)];
            Run(pair, WholeLines($"v ${v:X4}, PPUMASK ${mask:X2}, from line {line - 1}", r, line - 1, false, 0x00, mask, setup, 3), 3);
        }

        foreach (byte ctrl in new byte[] { 0x88, 0x8C })
        {
            DotAccess[] setup = [DotAccess.Write(0, 0, ctrl), DotAccess.Write(0, 1, mask), .. SetV(0, 0x3F00)];
            IEnumerable<DotAccess> Between(int end, int offset) => end switch
            {
                1 => [DotAccess.Write(offset, 7, 0x21)],
                2 => SetV(offset, 0x2000),
                _ => [],
            };
            Run(pair, WholeLines($"the backdrop written at line {line} dot 0, PPUCTRL ${ctrl:X2}, PPUMASK ${mask:X2}", r, line - 1, false, 0x00, mask, setup, 3, Between), 3);
        }
    }

    public static TheoryData<string, byte, byte> IdleRows()
    {
        var data = new TheoryData<string, byte, byte>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (byte mask in new byte[] { 0x00, 0x0A, 0x1E })
            {
                foreach (byte ctrl in new byte[] { 0x88, 0x08 })
                {
                    data.Add(region, mask, ctrl);
                }
            }
        }

        return data;
    }

    // The lines after the picture and in VBlank, each taken whole, with the VBlank flag set by
    // line 241's dot 1 inside the fast line: from line 238 through the pre-render line into the
    // next frame, with the picture read at each line's end, and again with $2002 read there, which
    // clears the flag and the write toggle.
    [Theory]
    [MemberData(nameof(IdleRows))]
    public void IdleLines(string region, byte mask, byte ctrl)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, ctrl, mask, 200, quiet: true));
        int count = r.PreRenderLine - 238 + 2;
        int fast = Enumerable.Range(238, count).Count(l => Fast(r, l % r.Lines, mask));
        DotAccess[] setup = [DotAccess.Write(0, 0, ctrl), DotAccess.Write(0, 1, mask)];
        foreach (bool odd in new[] { false, true })
        {
            Run(pair, WholeLines($"PPUCTRL ${ctrl:X2}, PPUMASK ${mask:X2}, {(odd ? "odd" : "even")}", r, 238, odd, 0x00, mask, setup, count), fast);
            Run(pair, WholeLines($"PPUCTRL ${ctrl:X2}, PPUMASK ${mask:X2}, {(odd ? "odd" : "even")}, $2002 read at each line's end", r, 238, odd, 0x00, mask, setup, count, (_, offset) => [DotAccess.Read(offset, 2)]), fast);
        }
    }

    // A $2002 read on the dots around the one that sets the VBlank flag: on line 241 dot 1, one
    // dot before, it stops the flag, and line 241 is not whole in either catch-up, so the per-dot
    // path runs it; on dot 0 line 241 is whole after the read.
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void A2002ReadAroundTheVblankDot(string region)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, 0x0A, 200, quiet: true));
        foreach (int dot in new[] { 0, 1, 2, 3, 340 })
        {
            int offset = dot == 340 ? 340 : Region.DotsPerLine + dot;
            DotAccess[] accesses = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, 0x0A), DotAccess.Read(offset, 2)];
            var scene = new DotScene($"$2002 read at {(dot == 340 ? "line 240 dot 340" : $"line 241 dot {dot}")}, {r.Name}", 240, 0, false, 0x00, accesses, 3 * Region.DotsPerLine);

            // Lines 240, 241 and 242: 240 is whole unless the read is on it, 241 only when the
            // read is before it or on its dot 0, 242 always.
            int fast = (dot == 340 ? 0 : 1) + (dot == 0 || dot == 340 ? 1 : 0) + 1;
            Run(pair, scene, fast);
        }
    }

    public static TheoryData<string, byte, int> SpriteRows()
    {
        var data = new TheoryData<string, byte, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (byte mask in SpriteMasks)
            {
                foreach (int line in Lines)
                {
                    data.Add(region, mask, line);
                }
            }
        }

        return data;
    }

    // With the sprites on, the visible lines are refused (task 4's), and the per-dot path's
    // state is reached all the same.
    [Theory]
    [MemberData(nameof(SpriteRows))]
    public void SpriteLinesAreRefused(string region, byte mask, int line)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, mask, line, quiet: true));
        foreach ((string name, int v, int fineX) in Scrolls().Take(8))
        {
            DotAccess[] setup = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, mask), .. SetScroll(0, v, fineX)];
            Run(pair, WholeLines($"{name}, PPUMASK ${mask:X2}, from line {line - 1}", r, line - 1, false, 0x00, mask, setup, 2), 0);
        }
    }

    public static TheoryData<string, string, int> AccessRows()
    {
        var data = new TheoryData<string, string, int>();
        foreach (string kind in PpuDotSceneTests.Kinds.Keys)
        {
            foreach (string region in new[] { "NTSC", "PAL" })
            {
                foreach (int line in new[] { 1, PpuDotSceneTests.VisibleLine, 239 })
                {
                    data.Add(kind, region, line);
                }
            }
        }

        return data;
    }

    // Review Focus 1: each kind of access on every dot of a visible line with the background
    // alone, from dot 0 of the line before to dot 0 of the line after the next. The line before
    // is whole in the catch-up to the access and the line after in the one after it, so both are
    // taken (with the PPUMASK the access leaves, for the line after); the access's own line is
    // taken only when the access is on its dot 0, and otherwise the per-dot path runs it in two
    // parts.
    [Theory]
    [MemberData(nameof(AccessRows))]
    public void AnAccessOnAnyDotOfTheLine(string kind, string region, int line)
    {
        Region r = PpuScene.RegionNamed(region);
        const byte Mask = 0x0A;
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, Mask, line, quiet: true));
        int dot = 0;
        foreach (DotScene scene in PpuDotSceneTests.AtEveryDot(kind, r, line, false, 0x00, Ctrl, Mask))
        {
            DotAccess last = scene.Accesses[^1];
            byte after = last.Kind == DotAccessKind.Write && last.Register == 1 ? last.Value : Mask;
            int fast = (Fast(r, line - 1, Mask) ? 1 : 0) + (dot == 0 && Fast(r, line, after) ? 1 : 0) + (Fast(r, line + 1, after) ? 1 : 0);
            Run(pair, scene, fast);
            dot++;
        }

        Assert.Equal(Region.DotsPerLine, dot);
    }

    // The masks rendering is switched between at a line's edge: off, the background with the
    // left clip and without, everything, and the sprites alone.
    private static readonly byte[] EdgeMasks = [0x00, 0x08, 0x0A, 0x1E, 0x10];

    public static TheoryData<string, byte, byte> EdgeRows()
    {
        var data = new TheoryData<string, byte, byte>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (byte from in EdgeMasks)
            {
                foreach (byte to in EdgeMasks.Where(m => m != from))
                {
                    data.Add(region, from, to);
                }
            }
        }

        return data;
    }

    // Review Focus 2: rendering switched on or off by a $2001 write on the last two dots of the
    // line before, or on dot 0 or 1 of the line: the lines a catch-up owes whole are taken with
    // the PPUMASK in force on them. On the pre-render line, the write on dots 336 to 340 of odd
    // and even frames, around dot 338, which decides the odd frame's dropped dot; lines 0 and 1
    // after it are whole either way.
    [Theory]
    [MemberData(nameof(EdgeRows))]
    public void RenderingSwitchedAtALineEdge(string region, byte from, byte to)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, from, PpuDotSceneTests.VisibleLine, quiet: true));
        foreach (int line in new[] { 1, PpuDotSceneTests.VisibleLine, 239 })
        {
            foreach (int target in new[] { 339, 340, 341, 342 })
            {
                DotAccess[] accesses =
                [
                    DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, from), .. SetScroll(0, PpuDotSceneTests.ScrollV(line - 1, false), 5),
                    DotAccess.Write(target, 1, to),
                ];
                var scene = new DotScene($"$2001 ${from:X2} to ${to:X2} at {(target < 341 ? $"line {line - 1} dot {target}" : $"line {line} dot {target - 341}")}, {r.Name}", line - 1, 0, false, 0x00, accesses, 3 * Region.DotsPerLine);
                int fast = (target >= 341 && Fast(r, line - 1, from) ? 1 : 0) + (target <= 341 && Fast(r, line, to) ? 1 : 0) + (Fast(r, line + 1, to) ? 1 : 0);
                Run(pair, scene, fast);
            }
        }

        foreach (bool odd in new[] { false, true })
        {
            for (int target = 336; target <= 340; target++)
            {
                DotAccess[] accesses = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, from), DotAccess.Write(target, 1, to)];
                var scene = new DotScene($"$2001 ${from:X2} to ${to:X2} at the pre-render line's dot {target} of an {(odd ? "odd" : "even")} frame, {r.Name}", r.PreRenderLine, 0, odd, 0xE0, accesses, 3 * Region.DotsPerLine);
                Run(pair, scene, (Fast(r, 0, to) ? 1 : 0) + (Fast(r, 1, to) ? 1 : 0));
            }
        }
    }

    // CNROM: four banks of CHR ROM whose bytes all differ, switched by a write to the board at a
    // line's dot 0, so the line after is fetched whole from the new bank, and again inside the
    // next line, which the per-dot path then runs. The board's windows are what the fast path reads.
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void ChrBanksSwitchedAtALineBoundary(string region)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => Build(r, Cartridge.Load(Cnrom()).CreateMapper(), Ctrl, 0x0A));
        var rows = new Dictionary<int, uint[]>();
        foreach (int line in new[] { 1, 50, PpuDotSceneTests.VisibleLine, 200, 239 })
        {
            for (int bank = 0; bank < 4; bank++)
            {
                DotAccess[] accesses =
                [
                    DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, 0x0A), .. SetScroll(0, PpuDotSceneTests.ScrollV(line - 1, false), 5),
                    DotAccess.Cartridge(0, 0x8000, 0),
                    DotAccess.Cartridge(Region.DotsPerLine, 0x8000, (byte)bank).At(line, 0),
                    DotAccess.Cartridge((2 * Region.DotsPerLine) + 100, 0x8000, (byte)((bank + 1) & 3)).At(line + 1, 100),
                ];
                var scene = new DotScene($"CNROM bank {bank} from line {line} dot 0, {r.Name}", line - 1, 0, false, 0x00, accesses, 4 * Region.DotsPerLine);
                Run(pair, scene, 3);
                if (line == PpuDotSceneTests.VisibleLine)
                {
                    rows[bank] = pair.Lazy.PixelsAsRun.AsSpan(line * FrameBuffer.Width, FrameBuffer.Width).ToArray();
                }
            }
        }

        // The banks really differ on the line: each draws it differently.
        Assert.Equal(4, rows.Values.Select(row => string.Join(",", row)).Distinct().Count());
    }

    /// <summary>CNROM with four 8 KB banks of CHR ROM whose bytes all differ, and its PRG all $FF, so its bus conflicts change no value written; vertical mirroring.</summary>
    internal static byte[] Cnrom()
    {
        byte[] file = TestCartridge.Ines1(2, 4, mapper: 3, verticalMirroring: true, prgFill: 0xFF);
        TestCartridge.Pattern(4 * 0x2000, 17).CopyTo(file, 16 + (2 * 0x4000));
        return file;
    }

    public static TheoryData<string, byte> FrameRows()
    {
        var data = new TheoryData<string, byte>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            foreach (byte mask in new byte[] { 0x00, 0x08, 0x0A, 0x1E })
            {
                data.Add(region, mask);
            }
        }

        return data;
    }

    // Review Focus 5: one catch-up over three frames and a part, with no access in it: every
    // frame end is run and checked on the way, and every line the fast path may take is taken.
    [Theory]
    [MemberData(nameof(FrameRows))]
    public void ManyFramesInOneCatchUp(string region, byte mask)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, mask, 200, quiet: true));
        int length = (3 * r.Lines * Region.DotsPerLine) + 1000;
        foreach (bool odd in new[] { false, true })
        {
            // The lines whole inside the scene, from line 0 dot 0.
            int fast = 0;
            int left = length;
            int line = 0;
            bool frameOdd = odd;
            while (true)
            {
                int dots = LineLength(r, line, frameOdd, mask);
                if (left < dots)
                {
                    break;
                }

                left -= dots;
                fast += Fast(r, line, mask) ? 1 : 0;
                if (++line == r.Lines)
                {
                    line = 0;
                    frameOdd = !frameOdd;
                }
            }

            var scene = new DotScene($"three frames in one catch-up, PPUMASK ${mask:X2}, from an {(odd ? "odd" : "even")} frame, {r.Name}", 0, 0, odd, 0x00, [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, mask)], length);
            Run(pair, scene, fast);
        }
    }

    // A catch-up one dot short of a line runs it on the per-dot path; the dot after it is a
    // catch-up of its own; the next line, owed whole, is taken.
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void ALineNotOwedWholeIsNotTaken(string region)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, 0x0A, quiet: true));
        DotAccess[] accesses = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, 0x0A), DotAccess.Picture(340), DotAccess.Picture(341), DotAccess.Picture(682)];
        Run(pair, new DotScene($"a line in two catch-ups, {r.Name}", 50, 0, false, 0x00, accesses, 682), 1);
        Assert.True(pair.Lazy.ExactLines > 0);
    }

    // The switch: with the fast path off the lazy PPU runs every line on the per-dot path, and
    // the scenes agree all the same.
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void TheFastPathCanBeSwitchedOff(string region)
    {
        Region r = PpuScene.RegionNamed(region);
        var pair = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, 0x0A, quiet: true));
        pair.Lazy.WholeLines = false;
        DotAccess[] setup = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, 0x0A)];
        Run(pair, WholeLines("the fast path off", r, 230, false, 0x00, 0x0A, setup, 40), 0);
        pair.Lazy.WholeLines = true;
        Run(pair, WholeLines("the fast path on again", r, 230, false, 0x00, 0x0A, setup, 40), Enumerable.Range(230, 40).Count(l => Fast(r, l % r.Lines, 0x0A)));
    }

    // A board that watches the PPU's address bus is never taken, whatever the line; a board that
    // does not watch but has no pattern windows has its idle and rendering-off lines taken and
    // its background lines refused, whose fetches would be calls to it.
    [Theory]
    [InlineData("NTSC")]
    [InlineData("PAL")]
    public void TheBoardDecides(string region)
    {
        Region r = PpuScene.RegionNamed(region);
        foreach (byte mask in new byte[] { 0x00, 0x0A })
        {
            DotAccess[] setup = [DotAccess.Write(0, 0, Ctrl), DotAccess.Write(0, 1, mask)];
            var watching = new DotScenePair(() => PpuDotSceneTests.Busy(r, Ctrl, mask));
            Run(watching, WholeLines($"a board that watches, PPUMASK ${mask:X2}", r, 230, false, 0x00, mask, setup, 40), 0);

            var windowless = new DotScenePair(() =>
            {
                var board = new TestMapper { Mirroring = Mirroring.Vertical, Quiet = true };
                TestCartridge.Pattern(0x2000).CopyTo(board.Chr, 0);
                return Build(r, board, Ctrl, mask);
            });
            int fast = Enumerable.Range(230, 40).Count(l => l % r.Lines != r.PreRenderLine && (l % r.Lines >= FrameBuffer.Height || mask == 0x00));
            Run(windowless, WholeLines($"a quiet board without windows, PPUMASK ${mask:X2}", r, 230, false, 0x00, mask, setup, 40), fast);
        }
    }

    // A frame of whole lines allocates nothing: the measured pass is a method of its own,
    // compiled optimised from its first call and run three times first (as SampleBufferTests
    // does, whose note says why), with the background on, then off.
    [Theory]
    [InlineData("NTSC", 0x0A)]
    [InlineData("PAL", 0x00)]
    public void WholeLinesAllocateNothing(string region, byte mask)
    {
        Ppu ppu = PpuDotSceneTests.Busy(PpuScene.RegionNamed(region), Ctrl, mask, quiet: true);
        for (int pass = 0; pass < 3; pass++)
        {
            Frames(ppu);
        }

        long fast = ppu.FastLines;
        long before = GC.GetAllocatedBytesForCurrentThread();
        Frames(ppu);

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
        Assert.True(ppu.FastLines - fast > 4 * 200);
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static void Frames(Ppu ppu)
    {
        ppu.Deliver(4 * 312 * Region.DotsPerLine);
        ppu.CatchUp();
    }

    // The bus: a lazy machine on NROM renders most of its lines at once, where the per-dot
    // reference never does; on MMC3, which watches, neither does.
    [Theory]
    [InlineData("no access, the background alone", "NTSC")]
    [InlineData("no access, the background alone", "PAL")]
    [InlineData("no access, rendering off", "NTSC")]
    [InlineData("no access, rendering off", "PAL")]
    [InlineData("no access, NMI on", "NTSC")]
    [InlineData("MMC3 scanline IRQ", "NTSC")]
    public void TheBusTakesWholeLines(string name, string region)
    {
        Region r = PpuScene.RegionNamed(region);
        CatchUpScene s = CatchUpScenes.Table[name];
        (_, NesBus reference) = CatchUpScenes.Run(s, r, oracle: true);
        (_, NesBus lazy) = CatchUpScenes.Run(s, r, oracle: false);
        Assert.Equal(0, reference.Ppu.FastLines);
        if (s.Watches)
        {
            Assert.Equal(0, lazy.Ppu.FastLines);
            return;
        }

        // Every line but the pre-render line is in reach with the sprites off; with them on, the
        // lines after the picture and in VBlank.
        long frames = s.Frames + 1;
        long reach = (s.Mask & 0x10) == 0 ? r.Lines - 1 : r.Lines - FrameBuffer.Height - 1;
        Assert.True(lazy.Ppu.FastLines > frames * reach * 3 / 4, $"{name}: the fast path took {lazy.Ppu.FastLines} lines of {frames} frames, each with {reach} in reach");
    }
}
