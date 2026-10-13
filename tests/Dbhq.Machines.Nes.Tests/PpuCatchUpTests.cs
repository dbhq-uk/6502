using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The PPU caught up on demand (the lazy chips plan, task 2): the bus delivers dots to the PPU's
/// logical position, and the PPU runs them, dot by dot as before, only when something can see it.
/// The PPU alone first: what <see cref="Ppu.Deliver"/>, <see cref="Ppu.CatchUp"/> and
/// <see cref="Ppu.NextEventDot"/> do, which reads catch it up and which do not. Then the bus: a
/// table of scenes run on the per-dot reference (<see cref="NesOptions.PerDotReference"/>, which
/// catches the PPU up every cycle) and on the lazy build, which must give the same state at every
/// point where the PPU can be seen and the same interrupt lines on every cycle.
/// </summary>
public class PpuCatchUpTests
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    private static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    // A PPU with something on the screen: tiles, a palette, sprites in range of many lines, and
    // rendering on with the left columns shown, so the dots do real work.
    private static Ppu Busy(Region region, byte ctrl = 0)
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
            scene.Ppu.Oam[i] = CatchUpScenes.OamByte(i);
        }

        scene.Scroll(13, 7, 0, ctrl);
        scene.Ppu.WriteRegister(1, 0x1E);
        return scene.Ppu;
    }

    private static long FrameDots(Region region) => (long)Region.DotsPerLine * region.Lines;

    [Theory]
    [MemberData(nameof(Regions))]
    public void DeliverMovesTheLogicalPositionAndNothingElse(string region)
    {
        Ppu ppu = Busy(RegionNamed(region));
        long start = ppu.CaughtUpDots;
        Assert.Equal(start, ppu.LogicalDots);
        int line = ppu.Line;
        int dot = ppu.Dot;
        ulong before = CatchUpScenes.Hash(ppu, picture: true);

        ppu.Deliver(1000);

        Assert.Equal(start + 1000, ppu.LogicalDots);
        Assert.Equal(start, ppu.CaughtUpDots);
        long index = (line * Region.DotsPerLine) + dot + 1000;
        Assert.Equal(((int)(index / Region.DotsPerLine), (int)(index % Region.DotsPerLine)), (ppu.Line, ppu.Dot));
        Assert.Equal(start, ppu.CaughtUpDots);
        Assert.Equal(before, CatchUpScenes.Hash(ppu, picture: true));

        ppu.CatchUp();

        Assert.Equal(ppu.LogicalDots, ppu.CaughtUpDots);
        Assert.Equal(((int)(index / Region.DotsPerLine), (int)(index % Region.DotsPerLine)), (ppu.Line, ppu.Dot));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ACatchUpIsTheSameDotsAsTicks(string region)
    {
        Ppu ticked = Busy(RegionNamed(region));
        Ppu lazy = Busy(RegionNamed(region));
        var frames = new List<ulong>();
        var lazyFrames = new List<ulong>();
        ticked.Observer = new FrameHashes(ticked, frames);
        lazy.Observer = new FrameHashes(lazy, lazyFrames);

        // Chunks of every size from 1 up, across frame ends and, on NTSC with rendering on, the
        // odd frames' dropped dot; the position read between them is the logical one.
        int size = 1;
        long total = 0;
        while (total < 3 * FrameDots(RegionNamed(region)))
        {
            for (int i = 0; i < size; i++)
            {
                ticked.Tick();
            }

            long caught = lazy.CaughtUpDots;
            lazy.Deliver(size);
            Assert.Equal((ticked.Line, ticked.Dot), (lazy.Line, lazy.Dot));
            Assert.Equal(caught, lazy.CaughtUpDots);
            total += size;
            size = (size * 7 % 4093) + 1;
            if (size % 5 == 0)
            {
                lazy.CatchUp();
                Assert.Equal(CatchUpScenes.Hash(ticked, picture: true), CatchUpScenes.Hash(lazy, picture: true));
            }
        }

        lazy.CatchUp();
        Assert.Equal(CatchUpScenes.Hash(ticked, picture: true), CatchUpScenes.Hash(lazy, picture: true));
        Assert.Equal(frames, lazyFrames);
        Assert.True(frames.Count >= 3);
    }

    // Review Focus 5: a catch-up over several frames runs every frame end, each in turn, and each
    // frame's picture is the one the dots drew.
    [Theory]
    [MemberData(nameof(Regions))]
    public void ACatchUpOverSeveralFramesEndsEachFrame(string region)
    {
        Ppu ticked = Busy(RegionNamed(region), ctrl: 0x80);
        Ppu lazy = Busy(RegionNamed(region), ctrl: 0x80);
        var frames = new List<ulong>();
        var lazyFrames = new List<ulong>();
        ticked.Observer = new FrameHashes(ticked, frames);
        lazy.Observer = new FrameHashes(lazy, lazyFrames);
        long dots = (4 * FrameDots(RegionNamed(region))) + 1234;
        long frame = lazy.Frame;

        // Delivered in pieces with nothing caught up, the position read between them is the
        // ticked PPU's: across several frame ends, with rendering on, so on NTSC the odd frames
        // drop their last dot (the multi-frame walk of the logical position and its parity).
        long caught = lazy.CaughtUpDots;
        for (long done = 0; done < dots;)
        {
            int piece = (int)Math.Min(30_011, dots - done);
            for (int i = 0; i < piece; i++)
            {
                ticked.Tick();
            }

            lazy.Deliver(piece);
            done += piece;
            Assert.Equal((ticked.Line, ticked.Dot), (lazy.Line, lazy.Dot));
        }

        Assert.Equal(caught, lazy.CaughtUpDots);
        Assert.Empty(lazyFrames);
        lazy.CatchUp();

        Assert.Equal(4, lazyFrames.Count);
        Assert.Equal(frames, lazyFrames);
        Assert.Equal(frame + 4, lazy.Frame);
        Assert.Equal(CatchUpScenes.Hash(ticked, picture: true), CatchUpScenes.Hash(lazy, picture: true));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void NextEventDotIsTheVblankDotWhileNmiIsOnAndNoneWhileItIsOff(string region)
    {
        Region r = RegionNamed(region);
        var ppu = new Ppu(r, new TestMapper());
        ppu.PowerOn();
        Assert.Equal(long.MaxValue, ppu.NextEventDot);

        // At line 0 dot 0 the dot that sets the flag is 241 lines and 2 dots of running away.
        ppu.WriteRegister(0, 0x80);
        long vblank = ppu.LogicalDots + (241 * Region.DotsPerLine) + 2;
        Assert.Equal(vblank, ppu.NextEventDot);

        ppu.Deliver((int)(vblank - ppu.LogicalDots - 1));
        ppu.CatchUp();
        Assert.False(ppu.Nmi);
        Assert.Equal(vblank, ppu.NextEventDot);
        ppu.Deliver(1);
        ppu.CatchUp();
        Assert.True(ppu.Nmi);

        // With the flag set, the next change is the pre-render line's dot 1 clearing it.
        long clear = ppu.LogicalDots + ((r.PreRenderLine * Region.DotsPerLine) + 1) - ((241 * Region.DotsPerLine) + 2) + 1;
        Assert.Equal(clear, ppu.NextEventDot);

        // $2000 bit 7 off: the line cannot change until it is written again.
        ppu.WriteRegister(0, 0x00);
        Assert.Equal(long.MaxValue, ppu.NextEventDot);
        ppu.WriteRegister(0, 0x80);
        Assert.Equal(clear, ppu.NextEventDot);

        // A $2002 read clears the flag: the next change is the next frame's VBlank dot.
        ppu.ReadRegister(2);
        Assert.Equal(ppu.LogicalDots + FrameDots(r), ppu.NextEventDot);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AWriteTo2000CatchesUpFirstSoNextEventDotIsFromTheLogicalDot(string region)
    {
        var ppu = new Ppu(RegionNamed(region), new TestMapper());
        ppu.PowerOn();
        ppu.Deliver(5000);
        Assert.Equal(long.MaxValue, ppu.NextEventDot);

        ppu.WriteRegister(0, 0x80);

        Assert.Equal(ppu.LogicalDots, ppu.CaughtUpDots);
        Assert.Equal(ppu.LogicalDots + (241 * Region.DotsPerLine) + 1 - 5000 + 1, ppu.NextEventDot);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheReadsThatSeeTheStateCatchUpAndThePositionDoesNot(string region)
    {
        Region r = RegionNamed(region);
        var reads = new (string Name, Action<Ppu> Read)[]
        {
            ("V", p => _ = p.V),
            ("T", p => _ = p.T),
            ("FineX", p => _ = p.FineX),
            ("WriteToggle", p => _ = p.WriteToggle),
            ("Oam", p => _ = p.Oam),
            ("Screen", p => _ = p.Screen),
            ("Nmi", p => _ = p.Nmi),
            ("PeekRegister", p => _ = p.PeekRegister(2)),
            ("PeekVram", p => _ = p.PeekVram(0x2000)),
            ("ReadRegister", p => _ = p.ReadRegister(2)),
            ("WriteRegister", p => p.WriteRegister(5, 1)),
        };
        foreach ((string name, Action<Ppu> read) in reads)
        {
            Ppu ppu = Busy(r);
            ppu.Deliver(777);
            read(ppu);
            Assert.True(ppu.LogicalDots == ppu.CaughtUpDots, $"{name} did not catch the PPU up");
        }

        var logical = new (string Name, Action<Ppu> Read)[]
        {
            ("Line", p => _ = p.Line),
            ("Dot", p => _ = p.Dot),
            ("LogicalDots", p => _ = p.LogicalDots),
            ("NextEventDot", p => _ = p.NextEventDot),
            ("RenderingEnabled", p => _ = p.RenderingEnabled),
            ("Frame", p => _ = p.Frame),
            ("OddFrame", p => _ = p.OddFrame),
        };
        foreach ((string name, Action<Ppu> read) in logical)
        {
            Ppu ppu = Busy(r);
            long caught = ppu.CaughtUpDots;
            ppu.Deliver(777);
            read(ppu);
            Assert.True(ppu.CaughtUpDots == caught, $"{name} caught the PPU up");
        }

        // The frame count and the odd frame change only at a frame end, so they catch up when one
        // is owed, and give the count the dots make.
        foreach (Func<Ppu, object> read in new Func<Ppu, object>[] { p => p.Frame, p => p.OddFrame })
        {
            Ppu ticked = Busy(r);
            Ppu ppu = Busy(r);
            for (int i = 0; i < FrameDots(r); i++)
            {
                ticked.Tick();
            }

            ppu.Deliver((int)FrameDots(r));
            Assert.Equal(read(ticked), read(ppu));
            Assert.Equal(ppu.LogicalDots, ppu.CaughtUpDots);
        }
    }

    // An access on the pre-render line's last dots works the frame's end out again: before dot 338
    // has run, from the rendering switch; after, from the decision dot 338 made. On an odd NTSC
    // frame with rendering on the frame drops its last dot, so the logical position and the frame
    // count after the access must follow the ticked PPU's through the frame's end. Through the bus
    // this cannot be seen (the access sees dot 338 after its cycle's first two dots, and the true
    // and a wrong end then fall in the same next cycle), so it is pinned here.
    [Theory]
    [MemberData(nameof(Regions))]
    public void AnAccessOnThePreRenderLinesLastDotsKnowsWhetherTheFrameDropsItsLastDot(string region)
    {
        Region r = RegionNamed(region);
        foreach (bool odd in new[] { false, true })
        {
            for (int dot = 335; dot <= 340; dot++)
            {
                if (odd && dot == 340 && r.OddFrameSkipsADot)
                {
                    // The odd frame's dropped dot: the PPU is never there.
                    continue;
                }

                Ppu ticked = Busy(r);
                Ppu lazy = Busy(r);
                foreach (Ppu ppu in new[] { ticked, lazy })
                {
                    while (ppu.OddFrame != odd || ppu.Line != r.PreRenderLine || ppu.Dot != dot)
                    {
                        ppu.Tick();
                    }

                    ppu.WriteRegister(0, 0x80);
                }

                for (int i = 0; i < 4; i++)
                {
                    ticked.Tick();
                    lazy.Deliver(1);
                    Assert.Equal((ticked.Line, ticked.Dot), (lazy.Line, lazy.Dot));
                    Assert.Equal(ticked.Frame, lazy.Frame);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TickIsOneDotDeliveredAndCaughtUp(string region)
    {
        Ppu ppu = Busy(RegionNamed(region));
        long start = ppu.LogicalDots;

        ppu.Tick();

        Assert.Equal(start + 1, ppu.LogicalDots);
        Assert.Equal(start + 1, ppu.CaughtUpDots);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ResetAndPowerOnCatchUpFirst(string region)
    {
        foreach (bool power in new[] { false, true })
        {
            Ppu ticked = Busy(RegionNamed(region));
            Ppu lazy = Busy(RegionNamed(region));
            for (int i = 0; i < 54321; i++)
            {
                ticked.Tick();
            }

            lazy.Deliver(54321);
            if (power)
            {
                ticked.PowerOn();
                lazy.PowerOn();
            }
            else
            {
                ticked.Reset();
                lazy.Reset();
            }

            Assert.Equal(lazy.LogicalDots, lazy.CaughtUpDots);
            Assert.Equal(CatchUpScenes.Hash(ticked, picture: true), CatchUpScenes.Hash(lazy, picture: true));
        }
    }

    // The scene table: the per-dot reference and the lazy build, the same accesses at the same
    // cycles, the same state wherever the PPU can be seen and the same lines every cycle.
    public static TheoryData<string, string> Scenes()
    {
        var data = new TheoryData<string, string>();
        foreach (string name in CatchUpScenes.Table.Keys)
        {
            data.Add(name, "NTSC");
            data.Add(name, "PAL");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scenes))]
    public void TheLazyBuildSeesWhatThePerDotReferenceSees(string scene, string region)
    {
        CatchUpScene s = CatchUpScenes.Table[scene];
        (CatchUpRecorder reference, NesBus referenceBus) = CatchUpScenes.Run(s, RegionNamed(region), oracle: true);
        (CatchUpRecorder lazy, NesBus lazyBus) = CatchUpScenes.Run(s, RegionNamed(region), oracle: false);

        CatchUpScenes.AssertSame(reference, lazy);
        Assert.Equal(0, reference.MostOwed);
        if (s.Watches)
        {
            Assert.Equal(0, lazy.MostOwed);
        }
        else
        {
            Assert.True(lazy.MostOwed > 0, "the lazy build never left a dot owed at a cycle's end");
        }

        Assert.Equal(referenceBus.Cycles, lazyBus.Cycles);
        lazyBus.Ppu.CatchUp();
        Assert.Equal(CatchUpScenes.Hash(referenceBus.Ppu, picture: true), CatchUpScenes.Hash(lazyBus.Ppu, picture: true));
        Assert.Equal(referenceBus.Ppu.V, lazyBus.Ppu.V);
    }

    private sealed class FrameHashes(Ppu ppu, List<ulong> hashes) : INesObserver
    {
        public void Accessed(ushort address, bool write, byte value)
        {
        }

        public void ChipsReset(bool power)
        {
        }

        public void CycleEnded(bool nmi, bool irq)
        {
        }

        public void DmcFetched()
        {
        }

        public void FrameEnded() => hashes.Add(CatchUpScenes.Hash(ppu, picture: true));
    }
}
