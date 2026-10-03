using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The video ULA and the framebuffer, modes 0 to 6, against <c>video.md</c> sections 2 and 3.
/// Every expected pixel is worked by hand from the sheet and written here, never taken from what
/// the code printed.
/// </summary>
/// <remarks>
/// Pixels are written as one letter per 16 MHz pixel (the framebuffer's unit): K black, R red,
/// G green, Y yellow, B blue, M magenta, C cyan, W white, the physical colours 0 to 7 of
/// <c>video.md</c> s2.3. The register values for each mode are the sheet's, read from the ROM
/// (s2.2, s2.3, s2.5 and s3.1), with R7 written as the table's value plus one, as the OS does (s3.3).
/// </remarks>
public class VideoUlaTests
{
    // video.md s2.3: the palette writes the OS makes on a mode change, by group.
    private static readonly byte[] PaletteTwoColour = [0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0, 0x07, 0x17, 0x27, 0x37, 0x47, 0x57, 0x67, 0x77];
    private static readonly byte[] PaletteFourColour = [0xA0, 0xB0, 0xE0, 0xF0, 0x84, 0x94, 0xC4, 0xD4, 0x26, 0x36, 0x66, 0x76, 0x07, 0x17, 0x47, 0x57];
    private static readonly byte[] PaletteSixteenColour = [0xF8, 0xE9, 0xDA, 0xCB, 0xBC, 0xAD, 0x9E, 0x8F, 0x70, 0x61, 0x52, 0x43, 0x34, 0x25, 0x16, 0x07];

    // video.md s2.2: the control byte for each mode, from the ROM at $C3F7.
    private static readonly byte[] ControlByMode = [0x9C, 0xD8, 0xF4, 0x9C, 0x88, 0xC4, 0x88, 0x4B];

    private const uint K = 0xFF000000, R = 0xFF0000FF, G = 0xFF00FF00, Y = 0xFF00FFFF;
    private const uint B = 0xFFFF0000, M = 0xFFFF00FF, C = 0xFFFFFF00, W = 0xFFFFFFFF;

    [Theory]
    [InlineData(0, 80, true, false, false)]
    [InlineData(1, 40, true, false, false)]
    [InlineData(2, 20, true, false, false)]
    [InlineData(3, 80, true, false, false)]
    [InlineData(4, 40, false, false, false)]
    [InlineData(5, 20, false, false, false)]
    [InlineData(6, 40, false, false, false)]
    [InlineData(7, 40, false, true, true)]
    public void TheControlByteOfEachModeDecodes(int mode, int characters, bool twoMhz, bool teletext, bool flash)
    {
        // video.md s2.2: bits 3 and 2 are the characters per line (10, 20, 40, 80), bit 4 the
        // CRTC's clock, bit 1 teletext, bit 0 the flash select. Mode 7's byte, $4B, has bit 0 set.
        var ula = new VideoUla();
        ula.WriteControl(ControlByMode[mode]);

        Assert.Equal(ControlByMode[mode], ula.Control);
        Assert.Equal(characters, ula.CharactersPerLine);
        Assert.Equal(twoMhz, ula.TwoMhz);
        Assert.Equal(teletext, ula.Teletext);
        Assert.Equal(flash, ula.FlashSelect);
    }

    [Theory]
    [InlineData(0x07, 0, 0)] // logical 0, value 7: physical 7 XOR 7 = black
    [InlineData(0x16, 1, 1)] // value 6: red
    [InlineData(0x25, 2, 2)] // green
    [InlineData(0x34, 3, 3)] // yellow
    [InlineData(0x43, 4, 4)] // blue
    [InlineData(0x52, 5, 5)] // magenta
    [InlineData(0x61, 6, 6)] // cyan
    [InlineData(0x70, 7, 7)] // white
    [InlineData(0xF8, 15, 7)] // flash bit, value 0: white while the flash select is off
    public void APaletteWriteSetsTheEntryToThePhysicalColourXor7(byte write, int index, int physical)
    {
        // video.md s2.3: $xy stores y at entry x; bits 2 to 0 are the colour inverted.
        var ula = new VideoUla();
        ula.WritePalette(write);
        Assert.Equal(physical, ula.PhysicalColour(index));
    }

    [Theory]
    [InlineData(0x8F, 0, 7)] // 8: black-white
    [InlineData(0x9E, 1, 6)] // 9: red-cyan
    [InlineData(0xAD, 2, 5)] // 10: green-magenta
    [InlineData(0xBC, 3, 4)] // 11: yellow-blue
    [InlineData(0xCB, 4, 3)] // 12: blue-yellow
    [InlineData(0xDA, 5, 2)] // 13: magenta-green
    [InlineData(0xE9, 6, 1)] // 14: cyan-red
    [InlineData(0xF8, 7, 0)] // 15: white-black
    public void AFlashingEntryShowsItsComplementWhileTheFlashSelectIsOn(byte write, int steady, int flashed)
    {
        // video.md s2.3: with the entry's flash bit and control bit 0 both set, the stored
        // (inverted) colour goes out, so a flashing colour c alternates with c XOR 7. The OS does
        // the counting (s5); only the select's effect is asserted here.
        var ula = new VideoUla();
        ula.WritePalette(write);
        int index = write >> 4;

        ula.WriteControl(0xF4);
        Assert.Equal(steady, ula.PhysicalColour(index));
        ula.WriteControl(0xF5);
        Assert.Equal(flashed, ula.PhysicalColour(index));
    }

    [Fact]
    public void TheFlashSelectLeavesAnEntryWithoutTheFlashBitAlone()
    {
        var ula = new VideoUla();
        ula.WritePalette(0x16);
        ula.WriteControl(0xF5);
        Assert.Equal(1, ula.PhysicalColour(1));
    }

    [Theory]
    // Mode 0 (1 bit a pixel): $80 lights the leftmost pixel, $01 the rightmost (s2.4, mask table $C40D).
    [InlineData(0, 0x80, "WKKKKKKK")]
    [InlineData(0, 0x01, "KKKKKKKW")]
    [InlineData(3, 0x81, "WKKKKKKW")]
    // Mode 1 (2 bits): pixel p is b(7-p) high and b(3-p) low; logical 1 red, 2 yellow, 3 white.
    [InlineData(1, 0x08, "RRKKKKKK")]
    [InlineData(1, 0xF0, "YYYYYYYY")]
    [InlineData(1, 0xA5, "YYRRYYRR")]
    // Mode 2 (4 bits): pixel 0 is b7 b5 b3 b1, pixel 1 b6 b4 b2 b0; logical n is physical n.
    [InlineData(2, 0x14, "KKKKCCCC")]
    [InlineData(2, 0x12, "RRRRBBBB")]
    // Modes 4 and 6 at 1 MHz: eight 8 MHz pixels, two of the framebuffer's each.
    [InlineData(4, 0x80, "WWKKKKKKKKKKKKKK")]
    [InlineData(6, 0x41, "KKWWKKKKKKKKKKWW")]
    // Mode 5 at 1 MHz: four 4 MHz pixels, four each.
    [InlineData(5, 0x08, "RRRRKKKKKKKKKKKK")]
    [InlineData(5, 0xCA, "WWWWYYYYRRRRKKKK")]
    public void EachModeTurnsAByteIntoThePixelsTheSheetGives(int mode, byte value, string expected)
    {
        var ula = new VideoUla();
        ula.WriteControl(ControlByMode[mode]);
        foreach (byte write in PaletteFor(mode))
        {
            ula.WritePalette(write);
        }

        Assert.Equal(expected, Letters(ula, value));
    }

    [Fact]
    public void EightyColumnsOnTheOneMegahertzClockFillEveryOtherHalfWithLogicalColourFifteen()
    {
        // video.md s2.2: with 80 columns and the 1 MHz CRTC clock the shift register runs out
        // and the second half of each character is logical colour 15. Entry 15 is made green
        // so it shows.
        var ula = new VideoUla();
        ula.WriteControl(0x8C);
        foreach (byte write in PaletteTwoColour)
        {
            ula.WritePalette(write);
        }
        ula.WritePalette(0xF5);

        Assert.Equal("WKKKKKKKGGGGGGGG", Letters(ula, 0x80));
    }

    [Fact]
    public void TenColumnsOnTheTwoMegahertzClockShowOnlyTheOddBits()
    {
        // video.md s2.2: one 2 MHz pixel a character, from bits 7, 5, 3 and 1.
        var ula = new VideoUla();
        ula.WriteControl(0x90);
        foreach (byte write in PaletteSixteenColour)
        {
            ula.WritePalette(write);
        }

        Assert.Equal("RRRRRRRR", Letters(ula, 0x12)); // b7 b5 b3 b1 = 0001: red
        Assert.Equal("RRRRRRRR", Letters(ula, 0x47)); // 0100 0111: the same odd bits, other even ones
    }

    [Fact]
    public void Mode7DrawsNothingUntilTheTeletextChipExists()
    {
        // Task 9 adds the SAA5050. Until then the teletext input is black.
        var ula = new VideoUla();
        ula.WriteControl(ControlByMode[7]);
        Assert.Equal(new string('K', 16), Letters(ula, 0xFF));
    }

    [Theory]
    [InlineData(0x83, 0x80)] // logical 1 in mode 0 is the 8 entries with bit 3 set: blue
    [InlineData(0x83, 0xFF)]
    public void InATwoColourModeALogicalColourIsEightEntries(byte first, byte value)
    {
        // video.md s2.3: 8 writes in modes 0, 3, 4, 6, value bit 7 the logical colour. Logical 1
        // made blue with the eight writes $83, $93 ... $F3, the rest left as the OS sets them.
        var ula = new VideoUla();
        ula.WriteControl(ControlByMode[0]);
        foreach (byte write in PaletteTwoColour)
        {
            ula.WritePalette(write);
        }
        for (int i = 0; i < 8; i++)
        {
            ula.WritePalette((byte)(first + (i << 4)));
        }

        string pixels = Letters(ula, value);
        Assert.Equal('B', pixels[0]);
        Assert.DoesNotContain('W', pixels);
    }

    [Theory]
    [InlineData(0x80)] // pixel 0 logical 2, the rest 0
    [InlineData(0xF0)] // every pixel logical 2
    [InlineData(0xF7)] // pixel 0 logical 2 with every other pixel's bits set around it
    public void InAFourColourModeALogicalColourIsFourEntries(byte value)
    {
        // video.md s2.3: 4 writes in modes 1 and 5, value bits 7 and 5 the logical colour. Logical
        // 2 (bit 7 set, bit 5 clear) made blue at entries 8, 9, 12 and 13, whatever the other
        // pixels' bits in bits 6 and 4 of the index.
        var ula = new VideoUla();
        ula.WriteControl(ControlByMode[1]);
        foreach (byte write in PaletteFourColour)
        {
            ula.WritePalette(write);
        }
        foreach (byte write in new byte[] { 0x83, 0x93, 0xC3, 0xD3 })
        {
            ula.WritePalette(write);
        }

        Assert.Equal("BB", Letters(ula, value)[..2]);
    }

    [Fact]
    public void InTheSixteenColourModeALogicalColourIsOneEntry()
    {
        var ula = new VideoUla();
        ula.WriteControl(ControlByMode[2]);
        foreach (byte write in PaletteSixteenColour)
        {
            ula.WritePalette(write);
        }
        ula.WritePalette(0x13); // logical 1 made blue

        Assert.Equal("BBBBBBBB", Letters(ula, 0x03)); // the OS's fill byte for colour 1 (s2.4, ROM $C42A)
        Assert.Equal("KKKKGGGG", Letters(ula, 0x04)); // logical 2 is still green
    }

    [Theory]
    // Mode 0's registers (s3.1: R12/R13 = $0600): the first, second and last character cells.
    [InlineData(0x0600, 0, 2, 0x3000)]
    [InlineData(0x0601, 0, 2, 0x3008)]
    [InlineData(0x0600, 7, 2, 0x3007)]
    [InlineData(0x0FFF, 7, 2, 0x7FFF)]
    // Past $7FFF (MA12 high) the adder wraps by the screen's size: 20 KB in modes 0 to 2 ...
    [InlineData(0x1000, 0, 2, 0x3000)]
    [InlineData(0x104F, 7, 2, 0x327F)]
    // ... 16 KB in mode 3, 10 KB in modes 4 and 5, 8 KB in mode 6 (s2.5), each to its base.
    [InlineData(0x0800, 0, 0, 0x4000)]
    [InlineData(0x1000, 0, 0, 0x4000)]
    [InlineData(0x0B00, 0, 3, 0x5800)]
    [InlineData(0x1000, 0, 3, 0x5800)]
    [InlineData(0x0C00, 0, 1, 0x6000)]
    [InlineData(0x1000, 0, 1, 0x6000)]
    [InlineData(0x1001, 3, 1, 0x600B)]
    // MA13 high is the teletext path: RA ignored, $3C00 or $7C00 by MA11 (s2.5).
    [InlineData(0x2800, 5, 2, 0x7C00)]
    [InlineData(0x2BE7, 0, 0, 0x7FE7)]
    [InlineData(0x2000, 0, 2, 0x3C00)]
    public void TheScreenAddressComesFromMaAndRaWithTheHardwareWrap(int ma, int ra, int latch, int address)
    {
        Assert.Equal(address, VideoUla.ScreenAddress(ma, ra, latch));
    }

    [Theory]
    [InlineData(0, 2, 0x3000)]
    [InlineData(1, 2, 0x3000)]
    [InlineData(2, 2, 0x3000)]
    [InlineData(3, 0, 0x4000)]
    [InlineData(4, 3, 0x5800)]
    [InlineData(5, 3, 0x5800)]
    [InlineData(6, 1, 0x6000)]
    public void TheOsSetsTheRomsLatchBitsSoTheWrapLandsOnTheModesBase(int mode, int latch, int screenBase)
    {
        // The latch bits are the ROM's (via.md s2.2, video.md s2.5: C1 C0 = 10, 00, 11, 01), which
        // swap the Advanced User Guide's pairs for modes 0 to 2 and 4 to 5. With them, the address
        // just past $7FFF is the mode's own screen base.
        var s = new BbcSession(mode).Boot(3_000_000);
        Assert.Equal(latch, s.Machine.Bus.SystemVia.ScreenStartLatch);
        Assert.Equal(screenBase, VideoUla.ScreenAddress(0x1000, 0, s.Machine.Bus.SystemVia.ScreenStartLatch));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    public void InModes3And6Lines8And9OfEachRowAreBlack(int mode)
    {
        // video.md s1.5: DISEN = DISPTMG and not RA3, so the ULA blanks lines 8 and 9 of each
        // 10-line row although the CRTC displays them. Every byte $FF: logical 1, white.
        var rig = new Rig(mode);
        rig.Fill(0xFF);
        rig.RunFrames(3);

        for (int row = 0; row < 25; row++)
        {
            for (int ra = 0; ra < 10; ra++)
            {
                int line = (row * 10) + ra;
                uint expected = ra < 8 ? W : K;
                foreach (int y in new[] { 2 * line, (2 * line) + 1 })
                {
                    foreach (int x in new[] { 0, 333, 639 })
                    {
                        Assert.True(expected == rig.At(x, y), $"mode {mode} row {row} line {ra} at ({x}, {y}): {rig.At(x, y):X8}");
                    }
                }
            }
        }

        // Below the 25 rows the CRTC displays nothing.
        Assert.Equal(K, rig.At(320, 2 * 252));
    }

    [Fact]
    public void InModes0To2AndOtherModesNoLineIsBlanked()
    {
        var rig = new Rig(4);
        rig.Fill(0xFF);
        rig.RunFrames(3);
        for (int y = 0; y < 512; y++)
        {
            Assert.Equal(W, rig.At(100, y));
        }
    }

    [Fact]
    public void ANewStartAddressScrollsThePictureAndTheWrapBringsTheTopBackAtTheBottom()
    {
        // Mode 0. One white character at $3000, the first cell. Moving R12/R13 on by one
        // character puts MA $0600 at the last cell, where MA reaches $1000 and the adder wraps
        // it back to $3000 (s2.5).
        var rig = new Rig(0);
        rig.Bus.PokeRam(0x3000, 0xFF);
        rig.RunFrames(3);
        Assert.Equal(W, rig.At(0, 0));
        Assert.Equal(K, rig.At(632, 2 * 248));

        rig.Crtc(13, 0x01);
        rig.RunFrames(3);
        Assert.Equal(K, rig.At(0, 0));
        Assert.Equal(W, rig.At(632, 2 * 248));
        Assert.Equal(W, rig.At(639, (2 * 248) + 1));
        Assert.Equal(K, rig.At(631, 2 * 248));
    }

    [Theory]
    [InlineData(0, 8)] // segment 0 only: one character, one byte
    [InlineData(1, 16)] // segments 0 and 1: two characters
    [InlineData(2, 32)] // all three: four characters
    [InlineData(4, 16)] // 1 MHz, segment 0: one character of 16 pixels
    [InlineData(5, 32)] // 1 MHz, segments 0 and 1
    public void TheCursorIsTheSegmentsTheControlByteEnables(int mode, int width)
    {
        // video.md s2.2: segments 0 and 1 one CRTC character each, segment 2 two, drawn in turn
        // from where CUDISP starts; the cursor inverts the output (s2.4), so black turns white.
        var rig = new Rig(mode);
        int start = mode < 4 ? 0x0600 : 0x0B00;
        rig.Crtc(10, 0x00); // steady, from line 0
        rig.Crtc(11, 0x07);
        rig.Crtc(14, (byte)((start + 5) >> 8));
        rig.Crtc(15, (byte)((start + 5) & 0xFF));
        rig.RunFrames(3);

        int left = 5 * (mode < 4 ? 8 : 16);
        for (int y = 0; y < 16; y++)
        {
            Assert.Equal(K, rig.At(left - 1, y));
            Assert.Equal(W, rig.At(left, y));
            Assert.Equal(W, rig.At(left + width - 1, y));
            Assert.Equal(K, rig.At(left + width, y));
        }
        Assert.Equal(K, rig.At(left, 16));
    }

    [Fact]
    public void TheCursorRunsOnIntoTheBorderWhenItsSkewIsLongerThanTheDisplays()
    {
        // BeebWiki (video.md S4): "In various MODEs the cursor can be made to extend into the
        // right border". Mode 2's four-character cursor at the last displayed character, with
        // R1 cut to 70 so the border is in the picture and R8 = $81: interlace, the display not
        // delayed and CUDISP delayed two characters (s1.3). CUDISP comes at character 71, so
        // characters 71 to 74 are inverted, all four in the border, after the display has ended.
        var rig = new Rig(2);
        rig.Crtc(1, 70);
        rig.Crtc(8, 0x81);
        rig.Crtc(10, 0x00);
        rig.Crtc(11, 0x07);
        rig.Crtc(14, 0x06);
        rig.Crtc(15, 69);
        rig.RunFrames(3);

        for (int y = 0; y < 16; y++)
        {
            Assert.Equal(K, rig.At((71 * 8) - 1, y));
            Assert.Equal(W, rig.At(71 * 8, y));
            Assert.Equal(W, rig.At((75 * 8) - 1, y));
            Assert.Equal(K, rig.At(75 * 8, y));
        }
    }

    [Fact]
    public void TheFlashSelectChangesTheColoursOnTheScreen()
    {
        // Mode 2, every byte $C0: both pixels logical 8, flashing black-white (s2.3).
        var rig = new Rig(2);
        rig.Fill(0xC0);
        rig.RunFrames(3);
        Assert.Equal(K, rig.At(200, 200));

        rig.Bus.Write(0xFE20, 0xF5);
        rig.RunFrames(3);
        Assert.Equal(W, rig.At(200, 200));
    }

    [Theory]
    [InlineData(0, -1)] // 2 MHz: a character a cycle
    [InlineData(4, 1)] // 1 MHz, the write in the even cycle that clocks its character
    [InlineData(4, 0)] // 1 MHz, the write in the odd cycle, the second half of its character
    public void APaletteChangeInTheMiddleOfALineChangesTheColourFromTheNextCharacter(int mode, int phase)
    {
        // Every byte $FF, so every pixel is palette entry 15 (bits 7, 5, 3, 1 all set): white.
        // Entry 15 becomes cyan ($F1) in the middle of line 100. The ULA draws every character
        // clocked up to the write's cycle with the old palette, so the character of the write's
        // cycle is white and the next is cyan. That a write reaches the pixels with no delay of
        // its own is an assumption: the ULA's pipeline is not documented (video.md s6 item 3), and
        // it is VideoUla.PipelineDelayCharacters, in docs/known-differences.md.
        Assert.Equal(0, VideoUla.PipelineDelayCharacters);
        var rig = new Rig(mode);
        rig.Fill(0xFF);
        rig.RunFrames(2);

        CrtcState now = rig.Bus.Crtc.StateAt(rig.Bus.Cycles);
        while (now.LineInFrame != 100 || now.Character != 30 || (phase >= 0 && (rig.Bus.Cycles & 1) != phase))
        {
            rig.Bus.Read(0x0000);
            now = rig.Bus.Crtc.StateAt(rig.Bus.Cycles);
        }

        rig.Bus.Write(0xFE21, 0xF1);
        CrtcState write = rig.Bus.Crtc.StateAt(rig.Bus.Cycles);
        int width = mode < 4 ? 8 : 16;
        int boundary = (write.Character + 1) * width;
        int row = 200 + (write.OddField ? 1 : 0);
        rig.Run(400);

        for (int x = 0; x < 640; x++)
        {
            Assert.True((x < boundary ? W : C) == rig.At(x, row), $"line 100 at x = {x}, boundary {boundary}: {rig.At(x, row):X8}");
            Assert.Equal(W, rig.At(x, row - 2));
            Assert.Equal(C, rig.At(x, row + 2));
        }
    }

    [Fact]
    public void Mode1BootsAndTheBannerIsOnTheScreen()
    {
        // The OS in mode 1 prints its banner in white (logical 3) on black. After two more frames
        // the framebuffer holds more than the background.
        var s = new BbcSession(mode: 1).Boot(3_000_000);
        Framebuffer screen = s.Machine.Screen;
        long frames = screen.Frames;
        for (int n = 0; n < 200 && screen.Frames < frames + 2; n++)
        {
            s.Machine.Run(1_000);
        }

        Assert.True(screen.Frames >= frames + 2, "two frames did not complete");
        var colours = new HashSet<uint>();
        foreach (uint pixel in screen.Pixels)
        {
            colours.Add(pixel);
        }
        Assert.Contains(K, colours);
        Assert.Contains(W, colours);

        // The banner is on text row 1, pixel rows 16 to 31.
        int lit = 0;
        for (int y = 16; y < 32; y++)
        {
            for (int x = 0; x < 640; x++)
            {
                lit += screen.Pixel(x, y) == W ? 1 : 0;
            }
        }
        Assert.True(lit > 100, $"only {lit} white pixels on the banner's row");
    }

    [Fact]
    public void BeforeTheOsProgramsTheCrtcNothingIsPaintedOverTheBlackScreen()
    {
        // At power on every CRTC register is 0, so a frame is a line of one character and a
        // field ends every few cycles with almost every row unreached. Those rows are black
        // already, and painting them black again in every such frame made the first moments of
        // a boot run slower than a real BBC Micro (task 8's review). Counted, not timed: in a
        // quarter of a million cycles of that, not one pixel may be written.
        var bus = new BbcBus(BbcSession.Roms);
        bus.PowerOnReset();
        long frames = bus.Screen.Frames;
        long written = bus.Screen.PixelsWritten;
        for (int i = 0; i < 250_000; i++)
        {
            bus.Read(0x0000);
        }

        Assert.True(bus.Screen.Frames - frames > 10_000, $"only {bus.Screen.Frames - frames} frames");
        Assert.Equal(written, bus.Screen.PixelsWritten);
    }

    [Fact]
    public void AFieldWritesEachRowOfItsLinesOnceInAModeThatDrawsThem()
    {
        // Mode 1 interlaced, every byte $FF: a field of 256 displayed lines writes 256 rows of 640
        // pixels and nothing more, so the drawing's work is the picture's size.
        var rig = new Rig(1);
        rig.Fill(0xFF);
        rig.RunFrames(2);

        // From one frame start to the next, a cycle at a time.
        void ToFrameStart()
        {
            long frames = rig.Bus.Screen.Frames;
            while (rig.Bus.Screen.Frames == frames)
            {
                rig.Bus.Read(0x0000);
            }
        }

        ToFrameStart();
        long written = rig.Bus.Screen.PixelsWritten;
        ToFrameStart();
        Assert.Equal(256 * 640, rig.Bus.Screen.PixelsWritten - written);
    }

    [Fact]
    public void AFieldThatEndsEarlyBlanksTheRowsItDidNotReach()
    {
        // Mode 4, one white character on line 255, the last kept (row 31, line 7: $5800 + 31 *
        // 320 + 7). Then frames of two rows that display nothing (R4 = 1, R6 = 0; C4 still reaches
        // R6, so the parity still alternates): line 255 is never reached again, and each field
        // must clear the rows it did not reach, down to the last one, which is the white one.
        var rig = new Rig(4);
        rig.Bus.PokeRam(0x5800 + (31 * 320) + 7, 0xFF);
        rig.RunFrames(3);
        Assert.Equal(W, rig.At(0, 510));
        Assert.Equal(W, rig.At(0, 511));

        rig.Crtc(4, 1);
        rig.Crtc(6, 0);
        rig.RunFrames(4);
        Assert.Equal(K, rig.At(0, 510));
        Assert.Equal(K, rig.At(0, 511));
    }

    [Fact]
    public void BreakKeepsTheUlasRegistersAndPowerOnClearsThem()
    {
        // The ULA has no reset pin (video.md s2.1; the pin list in S8), so BREAK leaves its
        // registers; the OS writes both again in the mode change every BREAK makes. What the
        // registers hold at power on is not known, and the model takes zero.
        var s = new BbcSession(mode: 1).Boot(3_000_000);
        VideoUla ula = s.Machine.Bus.VideoUla;
        byte control = ula.Control;
        Assert.Equal(0xD8, control & 0xFE);

        s.Machine.PressBreak();
        Assert.Equal(control, ula.Control);

        s.Machine.PowerOn();
        Assert.Equal(0, ula.Control);
        Assert.Equal(7, ula.PhysicalColour(0)); // entry 0 holds 0: white, the stored bits inverted
    }

    private static byte[] PaletteFor(int mode) => mode switch
    {
        1 or 5 => PaletteFourColour,
        2 => PaletteSixteenColour,
        _ => PaletteTwoColour,
    };

    private static string Letters(VideoUla ula, byte value)
    {
        Span<uint> pixels = stackalloc uint[16];
        int count = ula.Draw(value, pixels);
        var text = new char[count];
        for (int i = 0; i < count; i++)
        {
            text[i] = pixels[i] switch
            {
                K => 'K',
                R => 'R',
                G => 'G',
                Y => 'Y',
                B => 'B',
                M => 'M',
                C => 'C',
                W => 'W',
                _ => '?',
            };
        }
        return new string(text);
    }

    /// <summary>
    /// A bus with no CPU running, set to one of the OS's modes by writing the registers the OS
    /// writes (video.md s3.1, s3.3), with screen memory clear. Time passes as reads of RAM.
    /// </summary>
    private sealed class Rig
    {
        // R0 to R11 as the ROM stores them (s3.1), R7 then written plus one (s3.3).
        private static readonly byte[] CrtcModes0To2 = [0x7F, 0x50, 0x62, 0x28, 0x26, 0x00, 0x20, 0x22, 0x01, 0x07, 0x67, 0x08];
        private static readonly byte[] CrtcMode3 = [0x7F, 0x50, 0x62, 0x28, 0x1E, 0x02, 0x19, 0x1B, 0x01, 0x09, 0x67, 0x09];
        private static readonly byte[] CrtcModes4To5 = [0x3F, 0x28, 0x31, 0x24, 0x26, 0x00, 0x20, 0x22, 0x01, 0x07, 0x67, 0x08];
        private static readonly byte[] CrtcMode6 = [0x3F, 0x28, 0x31, 0x24, 0x1E, 0x02, 0x19, 0x1B, 0x01, 0x09, 0x67, 0x09];

        public Rig(int mode)
        {
            Bus = new BbcBus(BbcSession.Roms);
            Bus.PowerOnReset();
            Bus.Write(0xFE42, 0x0F); // DDRB: PB0 to PB3 drive the latch

            // The latch bits, C0 (bit 4) then C1 (bit 5), as the ROM's pairs (s2.5).
            (int c0, int c1) = mode switch
            {
                0 or 1 or 2 => (0, 1),
                3 => (0, 0),
                4 or 5 => (1, 1),
                _ => (1, 0),
            };
            Bus.Write(0xFE40, (byte)((c0 << 3) | 4));
            Bus.Write(0xFE40, (byte)((c1 << 3) | 5));

            Bus.Write(0xFE20, ControlByMode[mode]);
            byte[] crtc = mode switch
            {
                0 or 1 or 2 => CrtcModes0To2,
                3 => CrtcMode3,
                4 or 5 => CrtcModes4To5,
                _ => CrtcMode6,
            };
            for (int r = 11; r >= 0; r--)
            {
                Crtc(r, r == 7 ? (byte)(crtc[r] + 1) : crtc[r]);
            }
            Crtc(12, mode switch { 0 or 1 or 2 => 0x06, 3 => 0x08, 4 or 5 => 0x0B, _ => 0x0C });
            Crtc(13, 0x00);

            foreach (byte write in PaletteFor(mode))
            {
                Bus.Write(0xFE21, write);
            }

            BaseAddress = mode switch { 0 or 1 or 2 => 0x3000, 3 => 0x4000, 4 or 5 => 0x5800, _ => 0x6000 };
        }

        public BbcBus Bus { get; }

        public int BaseAddress { get; }

        public void Crtc(int register, byte value)
        {
            Bus.Write(0xFE00, (byte)register);
            Bus.Write(0xFE01, value);
        }

        public void Fill(byte value)
        {
            for (int a = BaseAddress; a < 0x8000; a++)
            {
                Bus.PokeRam((ushort)a, value);
            }
        }

        public void Run(int cycles)
        {
            for (int i = 0; i < cycles; i++)
            {
                Bus.Read(0x0000);
            }
        }

        public void RunFrames(int frames)
        {
            long target = Bus.Screen.Frames + frames;
            for (int n = 0; n < 1_000 && Bus.Screen.Frames < target; n++)
            {
                Run(1_000);
            }
            Assert.True(Bus.Screen.Frames >= target, $"{frames} frames did not complete");
        }

        public uint At(int x, int y) => Bus.Screen.Pixel(x, y);
    }
}
