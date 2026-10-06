using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The frame buffer the PPU draws into, in both regions: its size, its frame count, and a frame
/// with rendering off.
/// </summary>
public class FrameBufferTests
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    private static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    private static Ppu Build(string region)
    {
        var ppu = new Ppu(RegionNamed(region), new TestMapper());
        ppu.PowerOn();
        return ppu;
    }

    private static void RunFrame(Ppu ppu)
    {
        long frame = ppu.Frame;
        while (ppu.Frame == frame)
        {
            ppu.Tick();
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AfterOneFrameTheBufferIs256By240AndItsFrameHasGoneUpByOne(string region)
    {
        Ppu ppu = Build(region);
        Assert.Equal(0, ppu.Screen.Frame);

        RunFrame(ppu);

        Assert.Equal(256, FrameBuffer.Width);
        Assert.Equal(240, FrameBuffer.Height);
        Assert.Equal(256 * 240, ppu.Screen.Pixels.Length);
        Assert.Equal(1, ppu.Screen.Frame);
        Assert.Equal(ppu.Frame, ppu.Screen.Frame);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AFrameWithRenderingOffIsEntirelyTheBackdropColour(string region)
    {
        Ppu ppu = Build(region);

        // Backdrop $3F00 = $16; v left pointing at a nametable, not the palette.
        ppu.WriteRegister(6, 0x3F);
        ppu.WriteRegister(6, 0x00);
        ppu.WriteRegister(7, 0x16);
        ppu.WriteRegister(6, 0x20);
        ppu.WriteRegister(6, 0x00);

        RunFrame(ppu);

        uint backdrop = PpuPalette.Colour(0x16, 0, RegionNamed(region).EmphasisSwapsRedAndGreen, false);
        Assert.All(ppu.Screen.Pixels, pixel => Assert.Equal(backdrop, pixel));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void APowerOnClearsTheBufferAndItsCount(string region)
    {
        Ppu ppu = Build(region);
        ppu.WriteRegister(6, 0x3F);
        ppu.WriteRegister(6, 0x00);
        ppu.WriteRegister(7, 0x30);
        RunFrame(ppu);

        ppu.PowerOn();

        Assert.Equal(0, ppu.Screen.Frame);
        Assert.All(ppu.Screen.Pixels, pixel => Assert.Equal(0xFF000000u, pixel));
    }
}
