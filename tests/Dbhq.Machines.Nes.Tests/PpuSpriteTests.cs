using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The sprites, from <c>docs/nes/facts/ppu.md</c> sections 4 and 7 to 9: the line's delay, the
/// flips, priority, 8 by 16, eight a line and the overflow flag with its bug (worked example 6),
/// sprite 0 hit (worked example 5), the left-column clip, and what evaluation does to
/// <c>$2004</c> and OAMADDR. In both regions.
/// </summary>
public class PpuSpriteTests
{
    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    private const byte Backdrop = 0x0F;

    // Tile 2 of table 0 is solid value 1, tile 3 solid value 2, tile 4 a single pixel at the top
    // left, value 3. Sprite palette 0 is $16, $2A, $12. Every sprite is hidden (Y = $FF) to begin.
    private static PpuScene Scene(string region)
    {
        var scene = new PpuScene(PpuScene.RegionNamed(region));
        scene.SolidTile(0, 2, 1);
        scene.SolidTile(0, 3, 2);
        scene.Tile(0, 4, [0x80, 0, 0, 0, 0, 0, 0, 0], [0x80, 0, 0, 0, 0, 0, 0, 0]);
        scene.Poke(0x3F00, Backdrop);
        scene.Poke(0x3F11, 0x16);
        scene.Poke(0x3F12, 0x2A);
        scene.Poke(0x3F13, 0x12);
        Array.Fill(scene.Ppu.Oam, (byte)0xFF);
        scene.Scroll(0, 0);
        return scene;
    }

    private static void Sprite(PpuScene scene, int index, int y, int tile, int attributes, int x)
    {
        scene.Ppu.Oam[index * 4] = (byte)y;
        scene.Ppu.Oam[(index * 4) + 1] = (byte)tile;
        scene.Ppu.Oam[(index * 4) + 2] = (byte)(attributes & 0xE3);
        scene.Ppu.Oam[(index * 4) + 3] = (byte)x;
    }

    private static uint SpriteColour(PpuScene scene, int value) => value switch
    {
        1 => scene.Colour(0x16),
        2 => scene.Colour(0x2A),
        3 => scene.Colour(0x12),
        _ => scene.Colour(Backdrop),
    };

    private static bool Sprite0Hit(PpuScene scene) => (scene.Ppu.PeekRegister(2) & 0x40) != 0;

    private static bool Overflow(PpuScene scene) => (scene.Ppu.PeekRegister(2) & 0x20) != 0;

    // The box (left, top, 8 wide, height high) is filled with value, and everything else is the backdrop.
    private static void AssertBox(PpuScene scene, int left, int top, int height, Func<int, int, int> value)
    {
        for (int y = 0; y < FrameBuffer.Height; y++)
        {
            for (int x = 0; x < FrameBuffer.Width; x++)
            {
                bool inside = x >= left && x < left + 8 && y >= top && y < top + height;
                uint expected = SpriteColour(scene, inside ? value(x - left, y - top) : 0);
                if (scene.Pixel(x, y) != expected)
                {
                    Assert.Fail($"pixel ({x}, {y}) is {scene.Pixel(x, y):X8}, expected {expected:X8}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ASpriteAtYAndXIsDrawnFromTheLineAfterY(string region)
    {
        PpuScene scene = Scene(region);
        Sprite(scene, 0, 50, 2, 0, 100);

        scene.Show(0x14);

        AssertBox(scene, 100, 51, 8, (_, _) => 1);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ASpriteUsesThePaletteItsAttributesChoose(string region)
    {
        PpuScene scene = Scene(region);
        scene.Poke(0x3F1D, 0x2A);
        scene.Scroll(0, 0);
        Sprite(scene, 0, 50, 2, 3, 100);

        scene.Show(0x14);

        Assert.Equal(scene.Colour(0x2A), scene.Pixel(100, 51));
    }

    [Theory]
    [InlineData("NTSC", 0x00, 0, 0)]
    [InlineData("NTSC", 0x40, 7, 0)]
    [InlineData("NTSC", 0x80, 0, 7)]
    [InlineData("NTSC", 0xC0, 7, 7)]
    [InlineData("PAL", 0x00, 0, 0)]
    [InlineData("PAL", 0x40, 7, 0)]
    [InlineData("PAL", 0x80, 0, 7)]
    [InlineData("PAL", 0xC0, 7, 7)]
    public void FlipsMoveTheTopLeftPixel(string region, int attributes, int column, int row)
    {
        PpuScene scene = Scene(region);
        Sprite(scene, 0, 50, 4, attributes, 100);

        scene.Show(0x14);

        AssertBox(scene, 100, 51, 8, (x, y) => x == column && y == row ? 3 : 0);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ASpriteBehindTheBackgroundShowsOnlyWhereTheBackgroundIsTransparent(string region)
    {
        // Background tile 5: its left four pixels value 1, its right four transparent, palette 0
        // entry 1 = $21. A behind sprite over it shows only in the right half; a front one in all.
        PpuScene scene = Scene(region);
        scene.Tile(0, 5, Enumerable.Repeat((byte)0xF0, 8).ToArray(), new byte[8]);
        scene.Poke(0x3F01, 0x21);
        scene.PlaceTile(0, 12, 6, 5);
        Sprite(scene, 0, 47, 2, 0x20, 96);
        Sprite(scene, 1, 63, 2, 0x00, 96);
        scene.PlaceTile(0, 12, 8, 5);
        scene.Scroll(0, 0);

        scene.Show(0x1E);

        for (int x = 96; x < 104; x++)
        {
            uint behind = x < 100 ? scene.Colour(0x21) : scene.Colour(0x16);
            Assert.Equal(behind, scene.Pixel(x, 48));
            Assert.Equal(scene.Colour(0x16), scene.Pixel(x, 64));
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ABehindSpriteAtALowerIndexHidesAFrontSpriteUnderIt(string region)
    {
        // ppu.md 7: the lowest-numbered opaque sprite wins among sprites, then its own priority
        // decides against the background.
        PpuScene scene = Scene(region);
        scene.SolidTile(0, 5, 1);
        scene.Poke(0x3F01, 0x21);
        scene.PlaceTile(0, 12, 6, 5);
        Sprite(scene, 0, 47, 3, 0x20, 96);
        Sprite(scene, 1, 47, 2, 0x00, 96);
        scene.Scroll(0, 0);

        scene.Show(0x1E);

        Assert.Equal(scene.Colour(0x21), scene.Pixel(100, 48));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheLowerNumberedSpriteIsInFront(string region)
    {
        PpuScene scene = Scene(region);
        Sprite(scene, 3, 50, 3, 0, 100);
        Sprite(scene, 7, 50, 2, 0, 104);

        scene.Show(0x14);

        Assert.Equal(SpriteColour(scene, 2), scene.Pixel(104, 51));
        Assert.Equal(SpriteColour(scene, 1), scene.Pixel(108, 51));
    }

    [Theory]
    [InlineData("NTSC", 0x00)]
    [InlineData("NTSC", 0x80)]
    [InlineData("PAL", 0x00)]
    [InlineData("PAL", 0x80)]
    public void In8By16ModeBit0OfTheTileChoosesTheTableAndAVerticalFlipSwapsTheHalves(string region, int attributes)
    {
        // Tile $05: table $1000, top tile 4, bottom tile 5. Table 0's tiles 4 and 5 are different,
        // so the wrong table shows.
        PpuScene scene = Scene(region);
        scene.SolidTile(1, 4, 1);
        scene.SolidTile(1, 5, 2);
        scene.SolidTile(0, 5, 3);
        Sprite(scene, 0, 50, 0x05, attributes, 100);
        scene.Scroll(0, 0, ctrl: 0x20);

        scene.Show(0x14);

        bool flipped = attributes == 0x80;
        AssertBox(scene, 100, 51, 16, (_, y) => (y < 8) != flipped ? 1 : 2);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void In8By8ModePpuctrlBit3ChoosesTheSpriteTable(string region)
    {
        PpuScene scene = Scene(region);
        scene.SolidTile(1, 2, 2);
        Sprite(scene, 0, 50, 2, 0, 100);
        scene.Scroll(0, 0, ctrl: 0x08);

        scene.Show(0x14);

        AssertBox(scene, 100, 51, 8, (_, _) => 2);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ANinthSpriteOnALineIsDroppedAndSetsOverflow(string region)
    {
        PpuScene scene = Scene(region);
        for (int i = 0; i < 9; i++)
        {
            Sprite(scene, i, 50, 2, 0, i * 16);
        }

        scene.Ppu.WriteRegister(1, 0x14);
        scene.RunFrames(1);

        // Line 50 evaluates line 51's sprites, and finds the ninth.
        scene.TickTo(50, 0);
        Assert.False(Overflow(scene));
        scene.TickTo(51, 0);
        Assert.True(Overflow(scene));

        scene.RunFrames(1);
        for (int i = 0; i < 9; i++)
        {
            Assert.Equal(i < 8 ? SpriteColour(scene, 1) : SpriteColour(scene, 0), scene.Pixel(i * 16, 51));
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EightSpritesOnALineDoNotSetOverflow(string region)
    {
        PpuScene scene = Scene(region);
        for (int i = 0; i < 8; i++)
        {
            Sprite(scene, i, 50, 2, 0, i * 16);
        }

        scene.Ppu.WriteRegister(1, 0x14);
        scene.RunFrames(1);
        scene.TickTo(240, 0);

        Assert.False(Overflow(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WorkedExample6_TheOverflowBugsFalsePositive(string region)
    {
        // Sprites 0 to 7 at Y = 10; sprite 8 at $F0, out of range, so the check moves to sprite 9
        // and its byte 1, the tile $0A, which is read as a Y in range for line 10.
        PpuScene scene = Scene(region);
        for (int i = 0; i < 8; i++)
        {
            Sprite(scene, i, 10, 2, 0, i * 16);
        }

        Sprite(scene, 8, 0xF0, 2, 0, 200);
        Sprite(scene, 9, 0xF0, 0x0A, 0, 200);

        scene.Ppu.WriteRegister(1, 0x14);
        scene.RunFrames(1);
        scene.TickTo(10, 0);
        Assert.False(Overflow(scene));
        scene.TickTo(11, 0);

        Assert.True(Overflow(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WorkedExample6_TheOverflowBugsFalseNegative(string region)
    {
        // Sprite 9 now really is on line 10 (Y = 10), a ninth sprite, but the check reads its tile
        // $80 as its Y, then the diagonal bytes of the sprites after it, all $FF: no overflow.
        PpuScene scene = Scene(region);
        for (int i = 0; i < 8; i++)
        {
            Sprite(scene, i, 10, 2, 0, i * 16);
        }

        Sprite(scene, 8, 0xF0, 2, 0, 200);
        Sprite(scene, 9, 10, 0x80, 0, 200);

        scene.Ppu.WriteRegister(1, 0x14);
        scene.RunFrames(1);
        scene.TickTo(240, 0);

        Assert.False(Overflow(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void OverflowIsClearedAtDot1OfThePreRenderLine(string region)
    {
        PpuScene scene = Scene(region);
        for (int i = 0; i < 9; i++)
        {
            Sprite(scene, i, 50, 2, 0, i * 16);
        }

        scene.Ppu.WriteRegister(1, 0x14);
        scene.RunFrames(1);
        scene.TickTo(scene.Region.PreRenderLine, 1);
        Assert.True(Overflow(scene));
        scene.Ppu.Tick();
        Assert.False(Overflow(scene));
    }

    // Worked example 5: the background solid everywhere, sprite 0 solid at Y = 30.
    private static PpuScene HitScene(string region, int x)
    {
        PpuScene scene = Scene(region);
        scene.SolidTile(0, 1, 1);
        for (int i = 0; i < 960; i++)
        {
            scene.Poke((ushort)(0x2000 + i), 1);
        }

        scene.Scroll(0, 0);
        Sprite(scene, 0, 30, 2, 0, x);
        return scene;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WorkedExample5_Sprite0HitSetsAtTheFirstOpaqueOverlap(string region)
    {
        // Line 31, column 40: decided on dot 42 (column X on dot X + 2, ppu.md 6).
        PpuScene scene = HitScene(region, 40);
        scene.Ppu.WriteRegister(1, 0x1E);
        scene.RunFrames(1);

        scene.TickTo(31, 42);
        Assert.False(Sprite0Hit(scene));
        scene.Ppu.Tick();
        Assert.True(Sprite0Hit(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WorkedExample5_NoHitAtColumn255(string region)
    {
        PpuScene scene = HitScene(region, 255);
        scene.Ppu.WriteRegister(1, 0x1E);
        scene.RunFrames(1);

        scene.TickTo(240, 0);
        Assert.False(Sprite0Hit(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WorkedExample5_TheLeftClipMovesTheHitToColumn8(string region)
    {
        PpuScene scene = HitScene(region, 4);
        scene.Ppu.WriteRegister(1, 0x18);
        scene.RunFrames(1);

        scene.TickTo(31, 10);
        Assert.False(Sprite0Hit(scene));
        scene.Ppu.Tick();
        Assert.True(Sprite0Hit(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WorkedExample5_NoHitWhenTheLeftClipHidesTheWholeSprite(string region)
    {
        PpuScene scene = HitScene(region, 0);
        scene.Ppu.WriteRegister(1, 0x18);
        scene.RunFrames(1);

        scene.TickTo(240, 0);
        Assert.False(Sprite0Hit(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void NoHitWithEitherLayerOff(string region)
    {
        foreach (byte mask in new byte[] { 0x16, 0x0A })
        {
            PpuScene scene = HitScene(region, 40);
            scene.Ppu.WriteRegister(1, mask);
            scene.RunFrames(1);
            scene.TickTo(240, 0);
            Assert.False(Sprite0Hit(scene));
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AnotherSpriteNeverHits(string region)
    {
        PpuScene scene = HitScene(region, 40);
        Sprite(scene, 0, 0xFF, 2, 0, 40);
        Sprite(scene, 1, 30, 2, 0, 40);
        scene.Ppu.WriteRegister(1, 0x1E);
        scene.RunFrames(1);

        scene.TickTo(240, 0);
        Assert.False(Sprite0Hit(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void Sprite0HitIsClearedAtDot1OfThePreRenderLine(string region)
    {
        PpuScene scene = HitScene(region, 40);
        scene.Ppu.WriteRegister(1, 0x1E);
        scene.RunFrames(1);

        scene.TickTo(scene.Region.PreRenderLine, 1);
        Assert.True(Sprite0Hit(scene));
        scene.Ppu.Tick();
        Assert.False(Sprite0Hit(scene));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithPpumaskBit2ClearSpritesAreHiddenInTheLeftmostEightPixels(string region)
    {
        PpuScene scene = Scene(region);
        Sprite(scene, 0, 50, 2, 0, 4);

        scene.Show(0x10);

        AssertBox(scene, 4, 51, 8, (x, _) => x + 4 >= 8 ? 1 : 0);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void OnLine0NoSpriteIsDrawnBecauseThePreRenderLineEvaluatesNone(string region)
    {
        // A sprite at Y = 0 starts on line 1. Line 0's sprites would come from the pre-render
        // line, which never evaluates (ppu.md 7).
        PpuScene scene = Scene(region);
        Sprite(scene, 0, 0, 2, 0, 100);

        scene.Show(0x14);

        AssertBox(scene, 100, 1, 8, (_, _) => 1);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void DuringRenderingOamaddrIsClearedOnDots257To320AndA2004ReadOnDots1To64IsFF(string region)
    {
        PpuScene scene = Scene(region);
        scene.Ppu.Oam[0] = 0x12;
        scene.Ppu.Oam[0x40] = 0x34;
        scene.Ppu.WriteRegister(1, 0x18);
        scene.TickTo(20, 30);
        Assert.Equal(0xFF, scene.Ppu.ReadRegister(4));

        scene.TickTo(20, 300);
        scene.Ppu.WriteRegister(3, 0x40);
        scene.Ppu.Tick();
        scene.Ppu.WriteRegister(1, 0x00);
        Assert.Equal(scene.Ppu.Oam[0], scene.Ppu.ReadRegister(4));
    }
}
