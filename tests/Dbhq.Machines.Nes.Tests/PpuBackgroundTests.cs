using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The background, drawn dot by dot from <c>docs/nes/facts/ppu.md</c> sections 2, 6 and 10: the
/// tiles, the attributes, fine and coarse scroll, the nametable select, the copies of <c>t</c>
/// into <c>v</c> at the dots the sheet gives, the left-column clip, and the picture with
/// rendering off. Each test builds a few tiles through the PPU's registers and the board's
/// pattern RAM, in both regions.
/// </summary>
public class PpuBackgroundTests
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    // Rows of a tile whose pixel values, left to right, are 3 3 1 1 2 2 0 0: low plane $F0, high
    // plane $CC. Pixel value 0 is transparent and shows the backdrop.
    private static readonly int[] PatternValues = [3, 3, 1, 1, 2, 2, 0, 0];

    private const byte Backdrop = 0x21;

    // A scene with that tile as tile 1 of table 0, the backdrop $21, and background palette 2 set
    // to $16, $2A, $12, chosen by the attribute for the tile at column 5, row 3 of nametable 0.
    private static PpuScene Scene(string region)
    {
        var scene = new PpuScene(PpuScene.RegionNamed(region));
        scene.Tile(0, 1, Enumerable.Repeat((byte)0xF0, 8).ToArray(), Enumerable.Repeat((byte)0xCC, 8).ToArray());
        scene.Poke(0x3F00, Backdrop);
        scene.Poke(0x3F09, 0x16);
        scene.Poke(0x3F0A, 0x2A);
        scene.Poke(0x3F0B, 0x12);
        return scene;
    }

    private static uint TileColour(PpuScene scene, int value)
    {
        return value switch
        {
            1 => scene.Colour(0x16),
            2 => scene.Colour(0x2A),
            3 => scene.Colour(0x12),
            _ => scene.Colour(Backdrop),
        };
    }

    // Column 5, row 3 is in attribute byte $23C1 (columns 4 to 7, rows 0 to 3), in its bottom left
    // quadrant, bits 5 and 4 (ppu.md 6).
    private static void PlaceTheTile(PpuScene scene, int nametable = 0, int column = 5, int row = 3)
    {
        scene.PlaceTile(nametable, column, row, 1);
        int attribute = 0x23C0 + (nametable * 0x400) + ((row / 4) * 8) + (column / 4);
        int shift = (((row % 4) / 2) * 4) + (((column % 4) / 2) * 2);
        scene.Poke((ushort)attribute, (byte)(2 << shift));
    }

    // Every pixel of the picture is the tile's where it covers (left, top), and the backdrop elsewhere.
    private static void AssertTheTileIsAt(PpuScene scene, int left, int top, int fromLine = 0, int toLine = 239)
    {
        for (int y = fromLine; y <= toLine; y++)
        {
            for (int x = 0; x < FrameBuffer.Width; x++)
            {
                bool inside = x >= left && x < left + 8 && y >= top && y < top + 8;
                uint expected = TileColour(scene, inside ? PatternValues[x - left] : 0);
                if (scene.Pixel(x, y) != expected)
                {
                    Assert.Fail($"pixel ({x}, {y}) is {scene.Pixel(x, y):X8}, expected {expected:X8}; the tile was expected at ({left}, {top})");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AOneTilePatternAtNametable0DrawsAtItsEightByEightPixelsInThePaletteTheAttributeChose(string region)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene);
        scene.Scroll(0, 0);

        scene.Show(0x0A);

        AssertTheTileIsAt(scene, 40, 24);
    }

    public static TheoryData<string, int> RegionsAndFineX()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            for (int fineX = 0; fineX < 8; fineX++)
            {
                rows.Add(region, fineX);
            }
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsAndFineX))]
    public void FineXScrollShiftsThePictureByThatManyPixels(string region, int fineX)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene);
        scene.Scroll(fineX, 0);

        scene.Show(0x0A);

        AssertTheTileIsAt(scene, 40 - fineX, 24);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void FineYScrollMovesThePictureUp(string region)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene);
        scene.Scroll(0, 5);

        scene.Show(0x0A);

        AssertTheTileIsAt(scene, 40, 19);
    }

    public static TheoryData<string, int> RegionsAndNametables()
    {
        var rows = new TheoryData<string, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            for (int nametable = 0; nametable < 4; nametable++)
            {
                rows.Add(region, nametable);
            }
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsAndNametables))]
    public void PpuctrlBits0And1SelectTheNametable(string region, int nametable)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene, nametable);
        scene.Scroll(0, 0, nametable);

        scene.Show(0x0A);

        AssertTheTileIsAt(scene, 40, 24);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void CoarseXScrollMovesByWholeTilesAndRunsOnIntoTheNextNametable(string region)
    {
        // The tile at column 2 of nametable 1 is 256 + 16 across the two tables; scrolled by 200 it
        // is at 72, which needs the coarse X increment to flip into nametable 1 at column 31.
        PpuScene scene = Scene(region);
        PlaceTheTile(scene, nametable: 1, column: 2, row: 3);
        scene.Scroll(200, 0);

        scene.Show(0x0A);

        AssertTheTileIsAt(scene, 72, 24);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void CoarseYScrollRunsOnIntoTheNametableBelowAfterRow29(string region)
    {
        // The tile at row 2 of nametable 2 is 240 + 16 down the two tables; scrolled by 200 it is
        // at 56, which needs the Y increment to go from coarse Y 29 to 0 and flip into nametable 2.
        PpuScene scene = Scene(region);
        PlaceTheTile(scene, nametable: 2, column: 5, row: 2);
        scene.Scroll(0, 200);

        scene.Show(0x0A);

        AssertTheTileIsAt(scene, 40, 56);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheBackgroundPatternTableIsChosenByPpuctrlBit4(string region)
    {
        PpuScene scene = Scene(region);
        scene.Tile(1, 1, Enumerable.Repeat((byte)0xF0, 8).ToArray(), Enumerable.Repeat((byte)0xCC, 8).ToArray());
        scene.Tile(0, 1, new byte[8], new byte[8]);
        PlaceTheTile(scene);
        scene.Scroll(0, 0, ctrl: 0x10);

        scene.Show(0x0A);

        AssertTheTileIsAt(scene, 40, 24);
    }

    public static TheoryData<string, int, int> RegionsAndWriteDots()
    {
        var rows = new TheoryData<string, int, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            // A write made before dot 257 runs is copied into v on line 100's dot 257, and shows
            // from line 101; a write made after it waits for line 101's dot 257.
            rows.Add(region, 200, 101);
            rows.Add(region, 257, 101);
            rows.Add(region, 258, 102);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsAndWriteDots))]
    public void AMidFrameScrollWriteMovesTheLinesAfterTheNextDot257AndNotTheVerticalScroll(string region, int dot, int firstMovedLine)
    {
        // The tile at row 12 covers lines 96 to 103; it moves 16 to the left from the first line
        // whose fetches use the new horizontal bits. The vertical half of the write waits for the
        // next pre-render line, so nothing moves up.
        PpuScene scene = Scene(region);
        PlaceTheTile(scene, column: 5, row: 12);
        scene.Scroll(0, 0);
        scene.Ppu.WriteRegister(1, 0x0A);
        scene.RunFrames(1);

        scene.TickTo(100, dot);
        scene.Ppu.WriteRegister(5, 16);
        scene.Ppu.WriteRegister(5, 50);
        scene.RunFrames(1);

        AssertTheTileIsAt(scene, 40, 96, 0, firstMovedLine - 1);
        AssertTheTileIsAt(scene, 24, 96, firstMovedLine, 239);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AMidFrameAddressWriteCopiesTIntoVAtOnce(string region)
    {
        // After line 100's dot 257, v = $0280 (coarse Y 20, fine Y 0): the next line's tiles are
        // fetched from row 20, so the tile at row 20 shows from line 101 and not from line 160.
        PpuScene scene = Scene(region);
        PlaceTheTile(scene, column: 5, row: 20);
        scene.Scroll(0, 0);
        scene.Ppu.WriteRegister(1, 0x0A);
        scene.RunFrames(1);

        scene.TickTo(100, 260);
        scene.Ppu.WriteRegister(6, 0x02);
        scene.Ppu.WriteRegister(6, 0x80);
        Assert.Equal(0x0280, scene.Ppu.V);
        scene.RunFrames(1);

        AssertTheTileIsAt(scene, 40, 101);
    }

    public static TheoryData<string, int, int> RegionsAndPreRenderDots()
    {
        var rows = new TheoryData<string, int, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            // Dots 280 to 304 of the pre-render line copy t's vertical bits into v; a write made
            // before dot 304 runs is in time for this frame, one made after it is not.
            rows.Add(region, 279, 8);
            rows.Add(region, 304, 8);
            rows.Add(region, 305, 0);
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(RegionsAndPreRenderDots))]
    public void TheVerticalScrollIsCopiedOnDots280To304OfThePreRenderLine(string region, int dot, int scrolledBy)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene);
        scene.Scroll(0, 0);
        scene.Ppu.WriteRegister(1, 0x0A);
        scene.RunFrames(1);

        scene.TickTo(scene.Region.PreRenderLine, dot);
        scene.Ppu.WriteRegister(5, 0);
        scene.Ppu.WriteRegister(5, 8);

        // The rest of this frame, then the next, whose picture the copy decides.
        scene.RunFrames(2);

        AssertTheTileIsAt(scene, 40, 24 - scrolledBy);
    }

    [Theory]
    [InlineData("NTSC", 0x73A0, 0xEF, 0x0800)]
    [InlineData("NTSC", 0x73E0, 0xFF, 0x0000)]
    [InlineData("PAL", 0x73A0, 0xEF, 0x0800)]
    [InlineData("PAL", 0x73E0, 0xFF, 0x0000)]
    public void WorkedExample2_TheYIncrementAtCoarseY29And31(string region, int t, int scrollY, int after)
    {
        // Rows 2 and 3 of ppu.md worked example 2, reached through rendering: the pre-render line
        // copies t into v, and line 0's dot 256 does the Y increment. Dot 257 then copies t's
        // horizontal bits, which are 0, so v is the increment's result.
        PpuScene scene = new(PpuScene.RegionNamed(region));
        scene.Scroll(0, scrollY);
        Assert.Equal(t, scene.Ppu.T);
        scene.Ppu.WriteRegister(1, 0x08);
        scene.RunFrames(1);

        // Dots 328 and 336 of the pre-render line have moved coarse X on by two.
        Assert.Equal(t | 2, scene.Ppu.V);

        scene.TickTo(0, 258);

        Assert.Equal(after, scene.Ppu.V);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WorkedExample4_TheFirstFetchesOfALine(string region)
    {
        foreach ((byte scrollY, ushort low) in new[] { ((byte)0, (ushort)0x0240), ((byte)2, (ushort)0x0242) })
        {
            PpuScene scene = new(PpuScene.RegionNamed(region));
            scene.Poke(0x2000, 0x24);
            scene.Scroll(0, scrollY);
            scene.Ppu.WriteRegister(1, 0x08);

            // Each pattern fetch puts its address out on its first dot: 325 and 327.
            scene.TickTo(scene.Region.PreRenderLine, 321);
            scene.Mapper.Reported.Clear();
            scene.TickTo(scene.Region.PreRenderLine, 325);
            Assert.Empty(scene.Mapper.Reported);
            scene.Ppu.Tick();
            Assert.Equal([low], scene.Mapper.Reported.Select(r => r.Address));
            scene.TickTo(scene.Region.PreRenderLine, 327);
            scene.Ppu.Tick();
            Assert.Equal([low, (ushort)(low + 8)], scene.Mapper.Reported.Select(r => r.Address));

            // Dot 328 increments coarse X.
            scene.TickTo(scene.Region.PreRenderLine, 329);
            Assert.Equal(scrollY == 0 ? 0x0001 : 0x2001, scene.Ppu.V);
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithPpumaskBit1ClearTheLeftmostEightPixelsShowTheBackdrop(string region)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene, column: 0, row: 3);
        PlaceTheTile(scene, column: 1, row: 3);
        scene.Scroll(0, 0);

        scene.Show(0x08);

        for (int x = 0; x < 16; x++)
        {
            uint expected = x < 8 ? TileColour(scene, 0) : TileColour(scene, PatternValues[x - 8]);
            Assert.Equal(expected, scene.Pixel(x, 24));
        }

        scene.Show(0x0A);
        Assert.Equal(TileColour(scene, 3), scene.Pixel(0, 24));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithTheBackgroundOffAndSpritesOnTheTilesAreNotDrawn(string region)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene);
        scene.Scroll(0, 0);

        scene.Show(0x16);

        Assert.All(scene.Ppu.Screen.Pixels, pixel => Assert.Equal(scene.Colour(Backdrop), pixel));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithRenderingOffThePictureIsTheBackdropOrThePaletteEntryVPointsAt(string region)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene);

        scene.Ppu.ReadRegister(2);
        scene.Ppu.WriteRegister(6, 0x20);
        scene.Ppu.WriteRegister(6, 0x00);
        scene.RunFrames(1);
        Assert.All(scene.Ppu.Screen.Pixels, pixel => Assert.Equal(scene.Colour(Backdrop), pixel));

        // v at $3F0A: the picture is palette entry $0A, $2A.
        scene.Ppu.WriteRegister(6, 0x3F);
        scene.Ppu.WriteRegister(6, 0x0A);
        scene.RunFrames(1);
        Assert.All(scene.Ppu.Screen.Pixels, pixel => Assert.Equal(scene.Colour(0x2A), pixel));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void GreyscaleAndEmphasisApplyToEveryPixel(string region)
    {
        PpuScene scene = Scene(region);
        PlaceTheTile(scene);
        scene.Scroll(0, 0);

        // Greyscale and blue emphasis (PPUMASK bit 7) on top of the background.
        scene.Show(0x8B);

        uint Expected(int value) => PpuPalette.Colour(value, 4, scene.Region.EmphasisSwapsRedAndGreen, true);
        Assert.Equal(Expected(0x12), scene.Pixel(40, 24));
        Assert.Equal(Expected(0x16), scene.Pixel(42, 24));
        Assert.Equal(Expected(Backdrop), scene.Pixel(0, 0));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheVerticalCopyStartsOnDot280(string region)
    {
        // Before dot 280 runs, v's vertical bits are what dot 256's Y increment left; dot 280
        // copies t's into it (ppu.md 2).
        PpuScene scene = Scene(region);
        scene.Scroll(0, 0x50);
        scene.Ppu.WriteRegister(1, 0x0A);
        scene.RunFrames(1);

        int pre = scene.Region.PreRenderLine;
        scene.TickTo(pre, 257);
        int afterIncrement = scene.Ppu.V & 0x7BE0;
        Assert.NotEqual(scene.Ppu.T & 0x7BE0, afterIncrement);

        scene.TickTo(pre, 280);
        Assert.Equal(afterIncrement, scene.Ppu.V & 0x7BE0);
        scene.Ppu.Tick();
        Assert.Equal(scene.Ppu.T & 0x7BE0, scene.Ppu.V & 0x7BE0);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ThePalPpuEmphasisesWithItsRedAndGreenBitsSwapped(string region)
    {
        // PPUMASK bit 5. PpuPalette numbers emphasis in the 2C02's order, bit 0 red and bit 1
        // green, so on the 2C02 bit 5 is emphasis 1, and on the 2C07, where bit 5 is green
        // (ppu.md 10), it is emphasis 2 of the same table.
        PpuScene scene = Scene(region);
        PlaceTheTile(scene);
        scene.Scroll(0, 0);

        scene.Show(0x2A);

        int emphasis = region == "PAL" ? 2 : 1;
        Assert.Equal(PpuPalette.Colour(0x12, emphasis, false, false), scene.Pixel(40, 24));
        Assert.Equal(PpuPalette.Colour(0x16, emphasis, false, false), scene.Pixel(42, 24));
        Assert.Equal(PpuPalette.Colour(Backdrop, emphasis, false, false), scene.Pixel(0, 0));
        Assert.NotEqual(PpuPalette.Colour(0x12, 1, false, false), PpuPalette.Colour(0x12, 2, false, false));
    }
}
