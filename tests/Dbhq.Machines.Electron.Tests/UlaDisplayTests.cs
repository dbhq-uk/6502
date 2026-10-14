using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The ULA's display on its own, with RAM and no CPU, against <c>ula.md</c> section 5. Times are
/// 2 MHz cycles from power on, and the tests drive the display's catch-up directly: line
/// <c>L</c> of the odd field starts at <c>128 L</c> and line <c>L</c> of the even field at
/// <c>39,936 + 128 L</c> (s5d). Every expected pixel is worked by hand from the sheet.
/// </summary>
/// <remarks>
/// A write at cycle <c>t</c> is in time for a line that starts at <c>t</c> or later; a line that
/// started before <c>t</c> has been drawn from what was there before. So a register written at
/// cycle 0 is in force for the first line.
/// </remarks>
public class UlaDisplayTests
{
    private const uint Black = 0xFF000000, White = 0xFFFFFFFF, Red = 0xFF0000FF, Green = 0xFF00FF00;
    private const uint Yellow = 0xFF00FFFF, Blue = 0xFFFF0000, Magenta = 0xFFFF00FF, Cyan = 0xFFFFFF00;

    private const int Line = 128;
    private const int EvenField = 39_936;
    private const int Frame = 80_000;

    // ula.md s5a: the start address registers and the mode bits for each mode.
    private static readonly byte[] Fe03ByMode = [0x18, 0x18, 0x18, 0x20, 0x2C, 0x2C, 0x30];

    private static byte ModeBits(int mode) => (byte)(mode << 3);

    /// <summary>A display on its own RAM, in <paramref name="mode"/>, at the OS's start for it and with the OS's palette for it (s5a, s5c).</summary>
    private static UlaDisplay Display(int mode, byte[] ram)
    {
        var display = new UlaDisplay(ram);
        display.Write(7, ModeBits(mode), 0);
        display.Write(2, 0x00, 0);
        display.Write(3, Fe03ByMode[mode], 0);
        byte[] palette = mode switch
        {
            1 or 5 => [0x73, 0x31],
            2 => [0xF5, 0x5F, 0x05, 0x5F, 0x05, 0x50, 0xF5, 0x50],
            _ => [0x11, 0x11],
        };
        for (int i = 0; i < palette.Length; i++)
        {
            display.Write(8 + i, palette[i], 0);
        }

        return display;
    }

    /// <summary>Draws every line of the odd field (s5d: line 255 starts at 32,640).</summary>
    private static void DrawOddField(UlaDisplay display) => display.CatchUp((255 * Line) + 1);

    [Fact]
    public void TheModeTableIsTheSheets()
    {
        // ula.md s5a: bytes per line, display lines, screen start and $FE03 for modes 0 to 6;
        // $FE02 is $00 in every mode. Lines per character row: 10 in modes 3 and 6, 8 in the rest.
        int[] bytes = [80, 80, 80, 80, 40, 40, 40];
        int[] lines = [256, 256, 256, 250, 256, 256, 250];
        int[] starts = [0x3000, 0x3000, 0x3000, 0x4000, 0x5800, 0x5800, 0x6000];
        int[] rowLines = [8, 8, 8, 10, 8, 8, 10];
        for (int mode = 0; mode < 7; mode++)
        {
            Assert.Equal(bytes[mode], UlaDisplay.BytesPerLine(mode));
            Assert.Equal(lines[mode], UlaDisplay.DisplayedLines(mode));
            Assert.Equal(starts[mode], UlaDisplay.ScreenStart(mode));
            Assert.Equal(rowLines[mode], UlaDisplay.LinesPerRow(mode));

            // $FE03 = start >> 9 and $FE02 = (start >> 1) AND $E0 (s5a), and back again.
            Assert.Equal(Fe03ByMode[mode], starts[mode] >> 9);
            Assert.Equal(0x00, (starts[mode] >> 1) & 0xE0);
            var display = new UlaDisplay(new byte[0x8000]);
            display.Write(2, 0x00, 0);
            display.Write(3, Fe03ByMode[mode], 0);
            Assert.Equal(starts[mode], display.StartAddress);
        }
    }

    [Fact]
    public void TheStartAddressHas64ByteSteps()
    {
        // s5a: $FE02 bits 7 to 5 are A8 to A6 and $FE03 bits 5 to 0 are A14 to A9. $FE02 = $E0 and
        // $FE03 = $3F give $7E00 + $1C0 = $7FC0. Bits 4 to 0 of $FE02 and 7 and 6 of $FE03 are not stored.
        var display = new UlaDisplay(new byte[0x8000]);
        display.Write(2, 0xFF, 0);
        display.Write(3, 0xFF, 0);
        Assert.Equal(0x7FC0, display.StartAddress);
        display.Write(2, 0x20, 0);
        display.Write(3, 0x2C, 0);
        Assert.Equal(0x5840, display.StartAddress);
    }

    [Fact]
    public void A1BitByteIsEightPixelsBit7OnTheLeft()
    {
        // s5b, s5c: $A5 = 1010 0101; one bit a pixel, bit 7 first, 0 is logical colour 0 and 1 is 8.
        Assert.Equal(new byte[] { 8, 0, 8, 0, 0, 8, 0, 8 }, Logical(0, 0xA5));
        Assert.Equal(Logical(0, 0xA5), Logical(3, 0xA5));
        Assert.Equal(Logical(0, 0xA5), Logical(4, 0xA5));
        Assert.Equal(Logical(0, 0xA5), Logical(6, 0xA5));
    }

    [Fact]
    public void A2BitByteIsFourPixelsFromBits7MinusKAnd3MinusK()
    {
        // s5b: pixel k has bit 7-k high and bit 3-k low; s5c: values 0, 1, 2, 3 are logical 0, 2, 8, 10.
        // $A5 = 1010 0101: k=0 bits 7,3 = 1,0 -> 2 -> 8; k=1 bits 6,2 = 0,1 -> 1 -> 2;
        // k=2 bits 5,1 = 1,0 -> 8; k=3 bits 4,0 = 0,1 -> 2.
        Assert.Equal(new byte[] { 8, 2, 8, 2 }, Logical(1, 0xA5));
        Assert.Equal(Logical(1, 0xA5), Logical(5, 0xA5));
        // $F0 = 1111 0000: every pixel's high bit, value 2 -> 8; $0F: every low bit, 1 -> 2; $FF: 3 -> 10.
        Assert.Equal(new byte[] { 8, 8, 8, 8 }, Logical(1, 0xF0));
        Assert.Equal(new byte[] { 2, 2, 2, 2 }, Logical(1, 0x0F));
        Assert.Equal(new byte[] { 10, 10, 10, 10 }, Logical(1, 0xFF));
    }

    [Fact]
    public void A4BitByteIsTwoPixelsFromBits7531And6420()
    {
        // s5b: pixel k is bits 7-k, 5-k, 3-k, 1-k, high to low, and the value is the logical colour.
        // $A5 = 1010 0101: k=0 bits 7,5,3,1 = 1,1,0,0 = 12; k=1 bits 6,4,2,0 = 0,0,1,1 = 3.
        Assert.Equal(new byte[] { 12, 3 }, Logical(2, 0xA5));
        // $AA = 1010 1010: k=0 bits 7,5,3,1 all 1 = 15; k=1 bits 6,4,2,0 all 0 = 0.
        Assert.Equal(new byte[] { 15, 0 }, Logical(2, 0xAA));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 4)]
    [InlineData(6, 2)]
    public void TheByteA5IsDrawnAcrossTheFullWidthInEachMode(int mode, int width)
    {
        // Every mode fills 640 pixels (s3b: 40 microseconds of a line): 80 bytes of 8 pixels one
        // wide in modes 0 and 3, of 4 two wide in mode 1 and of 2 four wide in mode 2; 40 bytes of
        // 8 pixels two wide in modes 4 and 6 and of 4 four wide in mode 5. Each pixel is the
        // palette's colour for its logical colour.
        var ram = new byte[0x8000];
        Array.Fill(ram, (byte)0xA5, 0x3000, 0x5000);
        UlaDisplay display = Display(mode, ram);
        DrawOddField(display);

        byte[] logical = Logical(mode, 0xA5);
        Assert.Equal(640, UlaDisplay.BytesPerLine(mode) * logical.Length * width);
        for (int x = 0; x < 640; x++)
        {
            uint expected = display.Colour(logical[(x / width) % logical.Length]);
            Assert.Equal(expected, display.Screen.Pixel(x, 0));
            Assert.Equal(expected, display.Screen.Pixel(x, 7));
        }
    }

    [Fact]
    public void TheOsPaletteForMode0IsBlackAndWhiteAndRedNeedsBothRegisters()
    {
        // s5c: $FE08 = $11 and $FE09 = $11 (0001 0001). Colour 0: B0 ($FE08 bit 4), G0 ($FE09 bit 4)
        // and R0 ($FE09 bit 0) are all 1, off: black. Colour 8: B8 ($FE08 bit 6), G8 ($FE08 bit 2)
        // and R8 ($FE09 bit 2) are all 0, on: white.
        var display = Display(0, new byte[0x8000]);
        Assert.Equal(Black, display.Colour(0));
        Assert.Equal(White, display.Colour(8));

        // $FE09 = $FB, bit 2 alone clear: R8 on and G2, G0, R10, R2, R0 off. Colour 8's green and
        // blue are in $FE08, still $11, so colour 8 is still white: the brief's "red on, green
        // and blue off" needs $FE08 bits 6 and 2 set as well, which $FF does.
        display.Write(9, 0xFB, 0);
        Assert.Equal(White, display.Colour(8));
        display.Write(8, 0xFF, 0);
        Assert.Equal(Red, display.Colour(8));
        Assert.Equal(Black, display.Colour(0));
    }

    [Fact]
    public void TheOsPalettesForModes1And2DecodeToTheirColours()
    {
        // s5c: modes 1 and 5, $73 $31: $FE08 = 0111 0011, $FE09 = 0011 0001.
        // Colour 0: B0 (08 b4) 1, G0 (09 b4) 1, R0 (09 b0) 1: black.
        // Colour 2: B2 (08 b5) 1, G2 (09 b5) 1, R2 (09 b1) 0: red.
        // Colour 8: B8 (08 b6) 1, G8 (08 b2) 0, R8 (09 b2) 0: yellow.
        // Colour 10: B10 (08 b7) 0, G10 (08 b3) 0, R10 (09 b3) 0: white.
        var four = Display(1, new byte[0x8000]);
        Assert.Equal(new[] { Black, Red, Yellow, White }, new[] { four.Colour(0), four.Colour(2), four.Colour(8), four.Colour(10) });

        // Mode 2, $F5 $5F $05 $5F $05 $50 $F5 $50 in $FE08 to $FE0F: colours 0 to 7 are black, red,
        // green, yellow, blue, magenta, cyan, white, and 8 to 15 the same eight. One worked here,
        // colour 5: B5 is $FE0C bit 4 (the third pair's register, $05 = 0000 0101) = 0, on; G5 is
        // $FE0D bit 4 ($50 = 0101 0000) = 1, off; R5 is $FE0D bit 0 = 0, on: magenta.
        var sixteen = Display(2, new byte[0x8000]);
        uint[] eight = [Black, Red, Green, Yellow, Blue, Magenta, Cyan, White];
        for (int c = 0; c < 16; c++)
        {
            Assert.Equal(eight[c & 7], sixteen.Colour(c));
        }
    }

    [Fact]
    public void APaletteWriteTakesEffectFromTheNextLine()
    {
        // Each line is drawn from the registers as they are at its start: a palette write in the
        // middle of line 50 leaves line 50 as it was and changes line 51.
        var ram = new byte[0x8000];
        Array.Fill(ram, (byte)0xFF, 0x3000, 0x5000);
        UlaDisplay display = Display(0, ram);
        display.Write(8, 0xFF, (50 * Line) + 10);
        display.Write(9, 0xFB, (50 * Line) + 10);
        DrawOddField(display);

        Assert.Equal(White, display.Screen.Pixel(0, 50));
        Assert.Equal(Red, display.Screen.Pixel(0, 51));
    }

    [Fact]
    public void TheStartAddressIsReadOnceAtTheStartOfEachField()
    {
        // s5e: a write to $FE02 or $FE03 during a field takes effect at the next one. Mode 4 at
        // $5800; line 104 is row 13, line 0: its first byte is at start + 13 x 320 (s5b).
        var ram = new byte[0x8000];
        ram[0x5800 + (13 * 320)] = 0xFF; // line 104 from $5800
        ram[0x6000] = 0xFF;              // line 0 from $6000
        UlaDisplay display = Display(4, ram);

        display.Write(3, 0x30, (100 * Line) + 50); // $6000 from the next field
        Assert.Equal(0x6000, display.StartAddress);
        DrawOddField(display);
        Assert.Equal(Black, display.Screen.Pixel(0, 0));    // $5800 is 0
        Assert.Equal(White, display.Screen.Pixel(0, 104));  // still from $5800

        display.CatchUp(EvenField + (255 * Line) + 1);
        Assert.Equal(White, display.Screen.Pixel(0, 0));    // $6000
        Assert.Equal(Black, display.Screen.Pixel(0, 104));  // $6000 + 13 x 320 is 0
    }

    [Fact]
    public void PastThe7FFFTheAddressWrapsToTheModesStart()
    {
        // s5e: a start of $7F00 in mode 4 ($FE03 = $7F00 >> 9 = $3F, $FE02 = ($7F00 >> 1) AND $E0
        // = $80). Line 0, byte k is at $7F00 + 8k: k = 32 is $8000, so $5800 + 0, drawn at x =
        // 32 x 16 = 512. Row 1's base is $7F00 + 320 = $8040, so $5840, at line 8, x = 0.
        var ram = new byte[0x8000];
        ram[0x5800] = 0xFF;
        ram[0x5840] = 0xFF;
        ram[0x7F00 + (8 * 31)] = 0x01; // byte 31 of line 0: its last pixel, x = 31 x 16 + 14
        var display = new UlaDisplay(ram);
        display.Write(7, ModeBits(4), 0);
        display.Write(2, 0x80, 0);
        display.Write(3, 0x3F, 0);
        display.Write(8, 0x11, 0);
        display.Write(9, 0x11, 0);
        Assert.Equal(0x7F00, display.StartAddress);
        DrawOddField(display);

        Assert.Equal(White, display.Screen.Pixel((31 * 16) + 14, 0));
        Assert.Equal(Black, display.Screen.Pixel((31 * 16) + 13, 0));
        Assert.Equal(White, display.Screen.Pixel(512, 0));  // byte 32, $5800 = $FF: x = 512 to 527
        Assert.Equal(White, display.Screen.Pixel(527, 0));
        Assert.Equal(Black, display.Screen.Pixel(528, 0));  // byte 33, $5808 = 0
        Assert.Equal(White, display.Screen.Pixel(0, 8));
        Assert.Equal(Black, display.Screen.Pixel(0, 7));
    }

    [Fact]
    public void AStartBelow0800IsTheModesStart()
    {
        // s5e: loading an address below $0800 gives the mode's start instead; $0800 itself is
        // honoured. $FE03 = 2 is $0400; in mode 4 that is $5800. $FE03 = 4 is $0800.
        var ram = new byte[0x8000];
        ram[0x5800] = 0xFF;
        ram[0x0800 + 8] = 0xFF;
        var display = new UlaDisplay(ram);
        display.Write(7, ModeBits(4), 0);
        display.Write(3, 0x02, 0);
        display.Write(8, 0x11, 0);
        display.Write(9, 0x11, 0);
        display.Write(3, 0x04, (10 * Line) + 3); // $0800, from the even field
        DrawOddField(display);
        Assert.Equal(White, display.Screen.Pixel(0, 0));
        Assert.Equal(Black, display.Screen.Pixel(16, 0));

        display.CatchUp(EvenField + (255 * Line) + 1);
        Assert.Equal(Black, display.Screen.Pixel(0, 0));
        Assert.Equal(White, display.Screen.Pixel(16, 0));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void Modes3And6HaveTwoBlackLinesUnderEachRow(int mode)
    {
        // s5a, s5e: 10 lines a row, 8 of data and 2 blank, and the next row's base is rowbase + 8N
        // (N = 80 or 40): row 1 starts at start + 640 in mode 3 and + 320 in mode 6, at line 10.
        // 25 rows are 250 lines; lines 250 to 255 are black (s5d).
        int start = UlaDisplay.ScreenStart(mode);
        int n = UlaDisplay.BytesPerLine(mode);
        var ram = new byte[0x8000];
        Array.Fill(ram, (byte)0xFF, start, 0x8000 - start);
        ram[start + (8 * n)] = 0x00; // row 1, line 0, byte 0
        UlaDisplay display = Display(mode, ram);
        DrawOddField(display);

        for (int line = 0; line < 8; line++)
        {
            Assert.Equal(White, display.Screen.Pixel(0, line));
        }

        Assert.Equal(Black, display.Screen.Pixel(0, 8));
        Assert.Equal(Black, display.Screen.Pixel(639, 9));
        Assert.Equal(Black, display.Screen.Pixel(0, 10));  // row 1's first byte, $00
        Assert.Equal(White, display.Screen.Pixel(0, 11)); // row 1, line 1: start + 8N + 1
        Assert.Equal(White, display.Screen.Pixel(0, 247)); // row 24, line 7
        Assert.Equal(Black, display.Screen.Pixel(0, 248)); // row 24, lines 8 and 9
        Assert.Equal(Black, display.Screen.Pixel(0, 249));
        Assert.Equal(Black, display.Screen.Pixel(0, 250));
        Assert.Equal(Black, display.Screen.Pixel(0, 255));
    }

    [Fact]
    public void Row24OfMode6EndsAt7F3F()
    {
        // s5a: 25 rows of 320 bytes from $6000 end at $7F3F. Row 24, line 7 is line 247; its last
        // byte is $6000 + 24 x 320 + 39 x 8 + 7 = $7F3F, drawn at x = 39 x 16 to 639.
        var ram = new byte[0x8000];
        ram[0x7F3F] = 0x01;
        UlaDisplay display = Display(6, ram);
        DrawOddField(display);
        Assert.Equal(White, display.Screen.Pixel(638, 247));
        Assert.Equal(Black, display.Screen.Pixel(637, 247));
        Assert.Equal(Black, display.Screen.Pixel(639, 248));
    }

    [Fact]
    public void AModeWriteTakesEffectAtTheEndOfTheScanline()
    {
        // s5e: a mode change takes effect at the end of the current scanline. Every screen byte
        // is $80, one pixel lit at its left: in mode 4 a byte is 16 pixels, so x = 0 and 1 are lit
        // and 8 is not; in mode 0 a byte is 8 pixels, so x = 0 and 8 are lit and 1 is not.
        var ram = new byte[0x8000];
        Array.Fill(ram, (byte)0x80, 0x3000, 0x5000);
        UlaDisplay display = Display(4, ram);
        display.Write(7, ModeBits(0), (100 * Line) + 50);
        display.Write(7, ModeBits(4), 150 * Line); // exactly at line 150's start: in time for it
        DrawOddField(display);

        Assert.Equal(White, display.Screen.Pixel(1, 100));
        Assert.Equal(Black, display.Screen.Pixel(8, 100));
        Assert.Equal(Black, display.Screen.Pixel(1, 101));
        Assert.Equal(White, display.Screen.Pixel(8, 101));
        Assert.Equal(White, display.Screen.Pixel(8, 149));
        Assert.Equal(White, display.Screen.Pixel(1, 150));
        Assert.Equal(Black, display.Screen.Pixel(8, 150));
    }

    [Fact]
    public void AFrameIsCountedAtTheEndOfTheOddField()
    {
        // s5d: the odd field is cycles 0 to 39,935 of a frame of 80,000.
        var display = new UlaDisplay(new byte[0x8000]);
        Assert.Equal(0, display.Screen.Frames);
        display.CatchUp(EvenField);
        Assert.Equal(0, display.Screen.Frames);
        display.CatchUp(EvenField + 1);
        Assert.Equal(1, display.Screen.Frames);
        display.CatchUp(Frame + EvenField);
        Assert.Equal(1, display.Screen.Frames);
        display.CatchUp(Frame + EvenField + 1);
        Assert.Equal(2, display.Screen.Frames);
        display.CatchUp((10 * Frame) + 1);
        Assert.Equal(10, display.Screen.Frames);
    }

    [Fact]
    public void ThePictureIs640By256AndStartsBlack()
    {
        // One field's lines (s5d: both fields show the same lines), opaque black until drawn.
        var display = new UlaDisplay(new byte[0x8000]);
        Assert.Equal(640, display.Screen.Width);
        Assert.Equal(256, display.Screen.Height);
        Assert.Equal(640 * 256, display.Screen.Pixels.Length);
        Assert.All(display.Screen.Pixels.ToArray(), p => Assert.Equal(Black, p));
    }

    // --- on the bus ---

    [Fact]
    public void AStoreToScreenMemoryDuringALineShowsFromTheNextField()
    {
        // The bus brings the picture up to date before a store to RAM a line still to be drawn
        // could show. Mode 4 at $5800, black and white, set during the odd field, so in force from
        // the even field (s5e: the start is read once a field). Line 40 is row 5, line 0: its first
        // byte is $5800 + 5 x 320 = $5E40. A store there after line 40 has started is not in this
        // field's line 40, which was drawn from RAM as it was at its start; the next field's is.
        var bus = new ElectronBus(ElectronSession.Roms);
        bus.Write(0xFE07, ModeBits(4));
        bus.Write(0xFE02, 0x00);
        bus.Write(0xFE03, 0x2C);
        bus.Write(0xFE08, 0x11);
        bus.Write(0xFE09, 0x11);
        RunTo(bus, EvenField + (40 * Line) + 20);
        bus.Write(0x5E40, 0xFF);
        bus.Write(0x5E41, 0xFF); // line 41's first byte, stored before line 41 starts
        Assert.True(bus.Cycles < EvenField + (41 * Line));

        // Without the bus's check, line 40 would be drawn later from RAM with the store in it.
        RunTo(bus, EvenField + (42 * Line) + 1);
        Assert.Equal(Black, bus.Screen.Pixel(0, 40));
        Assert.Equal(White, bus.Screen.Pixel(0, 41));
        RunTo(bus, Frame + (40 * Line) + 1);
        Assert.Equal(White, bus.Screen.Pixel(0, 40));
    }

    [Fact]
    public void ThePaletteAndModeWritesOnTheBusArePlacedByTheirCycle()
    {
        // The same rule through the bus: the line in which a register write lands keeps what it
        // had, and the next takes the new value. RAM is zero, so every pixel is colour 0.
        var bus = new ElectronBus(ElectronSession.Roms);
        bus.Write(0xFE07, ModeBits(4));
        bus.Write(0xFE08, 0x11);
        bus.Write(0xFE09, 0x11);
        RunTo(bus, (60 * Line) + 30);
        bus.Write(0xFE09, 0x10); // R0 on: colour 0 red
        RunTo(bus, 62 * Line);

        Assert.Equal(Black, bus.Screen.Pixel(0, 60));
        Assert.Equal(Red, bus.Screen.Pixel(0, 61));
    }

    /// <summary>The bus's clock moved on by reads of the OS ROM, one cycle each, to <paramref name="cycle"/>.</summary>
    private static void RunTo(ElectronBus bus, long cycle)
    {
        while (bus.Cycles < cycle)
        {
            bus.Read(0xC000);
        }
    }

    private static byte[] Logical(int mode, byte value)
    {
        var pixels = new byte[UlaDisplay.PixelsPerByte(mode)];
        UlaDisplay.LogicalColours(mode, value, pixels);
        return pixels;
    }
}
