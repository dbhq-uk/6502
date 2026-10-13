using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The PPU's frame, alone and dot by dot, in both regions: the VBlank flag, the status read's race
/// with it, the NMI output, the frame's length and the odd frame's dropped dot
/// (<c>docs/nes/facts/ppu.md</c> section 5, <c>timing.md</c> section 2, <c>bus.md</c> section 2).
/// </summary>
/// <remarks>
/// <see cref="Ppu.Line"/> and <see cref="Ppu.Dot"/> name the dot the PPU runs next, so a register
/// access made while the PPU is "at line 241 dot 1" comes before that dot runs.
/// </remarks>
public class PpuTimingTests
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    private static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    private static Ppu Build(string region)
    {
        var ppu = new Ppu(RegionNamed(region), new TestMapper());
        ppu.PowerOn();
        return ppu;
    }

    private static void TickTo(Ppu ppu, int line, int dot)
    {
        for (int i = 0; i < 2 * 312 * 341; i++)
        {
            if (ppu.Line == line && ppu.Dot == dot)
            {
                return;
            }

            ppu.Tick();
        }

        Assert.Fail($"the PPU never reached line {line} dot {dot}");
    }

    private static bool VblankFlag(Ppu ppu) => (ppu.PeekRegister(2) & 0x80) != 0;

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheVblankFlagIsSetAtLine241Dot1AndClearedAtDot1OfThePreRenderLine(string region)
    {
        Ppu ppu = Build(region);
        int preRender = RegionNamed(region).PreRenderLine;

        TickTo(ppu, 240, 340);
        Assert.False(VblankFlag(ppu));
        TickTo(ppu, 241, 1);
        Assert.False(VblankFlag(ppu));

        ppu.Tick();
        Assert.True(VblankFlag(ppu));

        TickTo(ppu, preRender, 1);
        Assert.True(VblankFlag(ppu));
        ppu.Tick();
        Assert.False(VblankFlag(ppu));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AStatusReadOneDotBeforeTheFlagIsSetReadsItClearAndSuppressesItForTheFrame(string region)
    {
        Ppu ppu = Build(region);
        ppu.WriteRegister(0, 0x80);
        TickTo(ppu, 241, 1);

        Assert.Equal(0, ppu.ReadRegister(2) & 0x80);

        // Nothing is set, and no NMI comes, for the rest of the frame.
        while (ppu.Line != RegionNamed(region).PreRenderLine)
        {
            ppu.Tick();
            Assert.False(VblankFlag(ppu));
            Assert.False(ppu.Nmi);
        }

        // The next frame sets it as usual.
        TickTo(ppu, 241, 2);
        Assert.True(VblankFlag(ppu));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AStatusReadOnTheDotAfterTheFlagIsSetReadsItSetAndClearsIt(string region)
    {
        Ppu ppu = Build(region);
        TickTo(ppu, 241, 2);

        Assert.Equal(0x80, ppu.ReadRegister(2) & 0x80);
        Assert.False(VblankFlag(ppu));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheNmiOutputFollowsTheFlagWhilePpuctrlBit7IsSet(string region)
    {
        Ppu ppu = Build(region);
        int preRender = RegionNamed(region).PreRenderLine;
        ppu.WriteRegister(0, 0x80);

        TickTo(ppu, 241, 1);
        Assert.False(ppu.Nmi);
        ppu.Tick();
        Assert.True(ppu.Nmi);
        TickTo(ppu, preRender, 1);
        Assert.True(ppu.Nmi);
        ppu.Tick();
        Assert.False(ppu.Nmi);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void SettingPpuctrlBit7DuringVblankRaisesNmiAtOnceAndAgainAfterClearingIt(string region)
    {
        Ppu ppu = Build(region);
        TickTo(ppu, 250, 0);
        Assert.False(ppu.Nmi);

        ppu.WriteRegister(0, 0x80);
        Assert.True(ppu.Nmi);

        // Toggling bit 7 without reading $2002 gives another rising edge (ppu.md 5).
        ppu.WriteRegister(0, 0x00);
        Assert.False(ppu.Nmi);
        ppu.WriteRegister(0, 0x80);
        Assert.True(ppu.Nmi);

        // A status read clears the flag, and the output with it.
        ppu.ReadRegister(2);
        Assert.False(ppu.Nmi);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AFrameIsLinesTimes341DotsLessOneOnAnOddNtscFrameWithRenderingOn(string region)
    {
        Region r = RegionNamed(region);
        Ppu ppu = Build(region);
        ppu.WriteRegister(1, 0x08);

        long even = DotsToNextFrame(ppu);
        long odd = DotsToNextFrame(ppu);

        Assert.Equal(r.Lines * 341, even);
        Assert.Equal(region == "NTSC" ? r.Lines * 341 - 1 : r.Lines * 341, odd);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithRenderingOffEveryFrameIsWhole(string region)
    {
        Region r = RegionNamed(region);
        Ppu ppu = Build(region);

        Assert.Equal(r.Lines * 341, DotsToNextFrame(ppu));
        Assert.Equal(r.Lines * 341, DotsToNextFrame(ppu));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheDroppedDotIsDecidedWhenDot338OfThePreRenderLineRuns(string region)
    {
        Region r = RegionNamed(region);

        // Rendering switched on with the PPU at dot 338 of the odd frame's pre-render line counts:
        // on NTSC dots 338 and 339 run and 340 is dropped. PAL never drops it.
        Ppu early = Build(region);
        DotsToNextFrame(early);
        TickTo(early, r.PreRenderLine, 338);
        early.WriteRegister(1, 0x08);
        Assert.Equal(region == "NTSC" ? 2 : 3, DotsToNextFrame(early));

        // At dot 339 it is too late for this frame: dots 339 and 340 both run.
        Ppu late = Build(region);
        DotsToNextFrame(late);
        TickTo(late, r.PreRenderLine, 339);
        late.WriteRegister(1, 0x08);
        Assert.Equal(2, DotsToNextFrame(late));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void FrameCountsOncePerFrameAndTheLinesComeFromTheRegion(string region)
    {
        Region r = RegionNamed(region);
        Ppu ppu = Build(region);
        int maxLine = 0;
        long previous = ppu.Frame;

        for (int i = 0; i < 3 * r.Lines * 341; i++)
        {
            ppu.Tick();
            maxLine = Math.Max(maxLine, ppu.Line);
            Assert.InRange(ppu.Frame - previous, 0, 1);
            if (ppu.Frame != previous)
            {
                Assert.Equal((0, 0), (ppu.Line, ppu.Dot));
                previous = ppu.Frame;
            }
        }

        Assert.Equal(3, ppu.Frame);
        Assert.Equal(r.Lines - 1, maxLine);
    }

    private static long DotsToNextFrame(Ppu ppu)
    {
        long frame = ppu.Frame;
        long dots = 0;
        while (ppu.Frame == frame)
        {
            ppu.Tick();
            dots++;
        }

        return dots;
    }
}
