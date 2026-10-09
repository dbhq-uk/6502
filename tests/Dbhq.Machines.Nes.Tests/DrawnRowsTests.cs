using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The hash of the picture as far as it is drawn (<see cref="DrawnRows"/>), which the differential
/// and the scene recorder take at each point where the PPU can be seen: what it covers, what it
/// leaves for later, and that it reads without a catch-up.
/// </summary>
public class DrawnRowsTests
{
    // A PPU at `line`, `dot`, rendering on, with a picture drawn above it.
    private static Ppu At(int line, int dot)
    {
        Ppu ppu = PpuDotSceneTests.Busy(Region.Ntsc, PpuDotSceneTests.SceneCtrl, 0x1E);
        while (ppu.Line != line || ppu.Dot != dot)
        {
            ppu.Tick();
        }

        return ppu;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    [InlineData(128)]
    [InlineData(255)]
    public void APixelOfTheRowThePpuIsOnChangesTheHashDrawnOrNot(int column)
    {
        Ppu ppu = At(100, 130);
        ulong before = new DrawnRows().Hash(ppu);
        ppu.PixelsAsRun[(100 * FrameBuffer.Width) + column] ^= 1;

        Assert.NotEqual(before, new DrawnRows().Hash(ppu));
    }

    [Fact]
    public void ARowFinishedSinceTheLastPointChangesTheHashAndARowBelowDoesNot()
    {
        Ppu ppu = At(100, 0);
        var rows = new DrawnRows();
        var same = new DrawnRows();
        _ = rows.Hash(ppu);
        _ = same.Hash(ppu);
        while (ppu.Line != 120)
        {
            ppu.Tick();
        }

        ulong unchanged = same.Hash(ppu);
        ppu.PixelsAsRun[(110 * FrameBuffer.Width) + 7] ^= 1;
        ppu.PixelsAsRun[(130 * FrameBuffer.Width) + 7] ^= 1;
        ulong changed = rows.Hash(ppu);

        Assert.NotEqual(unchanged, changed);
        ppu.PixelsAsRun[(110 * FrameBuffer.Width) + 7] ^= 1;
        Assert.Equal(unchanged, new DrawnRows().Hash(ppu));
    }

    // A finished row is hashed once, at the first point after it is finished; the frame's end
    // hashes the whole picture, and the next frame starts again.
    [Fact]
    public void AFinishedRowIsHashedOnceAndTheNextFrameStartsAgain()
    {
        Ppu ppu = At(100, 0);
        var rows = new DrawnRows();
        ulong first = rows.Hash(ppu);
        ppu.PixelsAsRun[(50 * FrameBuffer.Width) + 7] ^= 1;
        Assert.Equal(first, rows.Hash(ppu));
        Assert.NotEqual(first, new DrawnRows().Hash(ppu));

        long frame = ppu.Frame;
        while (ppu.Frame == frame || ppu.Line != 100)
        {
            ppu.Tick();
        }

        Assert.Equal(new DrawnRows().Hash(ppu), rows.Hash(ppu));
    }

    [Fact]
    public void ItReadsWithoutACatchUp()
    {
        Ppu ppu = At(100, 0);
        long caught = ppu.CaughtUpDots;
        ulong before = new DrawnRows().Hash(ppu);
        ppu.Deliver(5000);

        Assert.Equal(before, new DrawnRows().Hash(ppu));
        Assert.Equal(caught, ppu.CaughtUpDots);
    }

    [Fact]
    public void InVblankEveryRowIsFinished()
    {
        Ppu ppu = At(250, 3);
        ulong before = new DrawnRows().Hash(ppu);
        ppu.PixelsAsRun[(239 * FrameBuffer.Width) + 255] ^= 1;

        Assert.NotEqual(before, new DrawnRows().Hash(ppu));
    }
}
