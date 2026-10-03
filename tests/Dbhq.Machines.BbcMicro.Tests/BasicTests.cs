using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// BBC BASIC typed in on the keyboard matrix and read back off the picture: the whole machine at
/// work, the keyboard and its interrupt, the OS, BASIC, the VDU drivers and the video chips.
/// </summary>
/// <remarks>
/// <para>
/// Each test boots its own machine, because each types into it. After the boot the prompt is on
/// row 5 in every mode (<see cref="BootScreen"/>), so what is typed is echoed there and the
/// answer starts on row 6. BASIC prints a number right-aligned in a field ten wide by default, so
/// the tests compare a number's row with its spaces trimmed. Booting takes three seconds of
/// machine time and typing 160,000 cycles a key (<see cref="BbcSession.HoldCycles"/>,
/// <see cref="BbcSession.RestCycles"/>), which natively is a few tenths of a second a test.
/// </para>
/// <para>
/// Where the expected text comes from: the commands and their answers are what is typed and what
/// BASIC must answer, worked out by hand (6 times 7, the loop's three values), never what the
/// machine printed; the prompt is the ROM's (<see cref="BootScreen.Prompt"/>).
/// </para>
/// </remarks>
public class BasicTests
{
    /// <summary>Time for a command to run and for the picture to show it in both fields: five fields.</summary>
    private const long Settle = 200_000;

    private const uint Black = 0xFF000000, White = 0xFFFFFFFF, Blue = 0xFFFF0000, Red = 0xFF0000FF, Cyan = 0xFFFFFF00;

    [Fact]
    public void TheTypingTableIsTheKeyboardsLegends()
    {
        // BbcSession makes its table from the OS's key table and SHIFT rule; it must come out as
        // the keys are marked (via.md s3(b)), which is not a PC's layout: " is SHIFT and 2, * is
        // SHIFT and colon, + SHIFT and semicolon, = SHIFT and minus.
        Assert.Equal((BbcKey.D2, true), BbcSession.Keys['"']);
        Assert.Equal((BbcKey.Colon, true), BbcSession.Keys['*']);
        Assert.Equal((BbcKey.Semicolon, true), BbcSession.Keys['+']);
        Assert.Equal((BbcKey.Minus, true), BbcSession.Keys['=']);
        Assert.Equal((BbcKey.Slash, true), BbcSession.Keys['?']);
        Assert.Equal((BbcKey.D6, true), BbcSession.Keys['&']);
        Assert.Equal((BbcKey.FullStop, false), BbcSession.Keys['.']);
        Assert.Equal((BbcKey.At, false), BbcSession.Keys['@']);
        Assert.Equal((BbcKey.P, false), BbcSession.Keys['P']);
        Assert.Equal((BbcKey.Space, false), BbcSession.Keys[' ']);
        Assert.Equal((BbcKey.Return, false), BbcSession.Keys['\r']);
        Assert.False(BbcSession.Keys.ContainsKey('p'));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void PrintSixTimesSevenShowsFortyTwoOnTheNextRow(int mode)
    {
        string[] screen = Booted(mode).Type("PRINT 6*7\r").RunFor(Settle).ScreenText();

        Assert.Equal(">PRINT 6*7", screen[5].TrimEnd());
        Assert.Equal("42", screen[6].Trim());
        Assert.Equal(">", screen[7].TrimEnd());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(1)]
    public void AForLoopPrintsOneTwoThree(int mode)
    {
        string[] screen = Booted(mode).Type("FOR I=1 TO 3:PRINT I:NEXT\r").RunFor(Settle).ScreenText();

        Assert.Equal(">FOR I=1 TO 3:PRINT I:NEXT", screen[5].TrimEnd());
        Assert.Equal("1", screen[6].Trim());
        Assert.Equal("2", screen[7].Trim());
        Assert.Equal("3", screen[8].Trim());
        Assert.Equal(">", screen[9].TrimEnd());
    }

    [Theory]
    [InlineData(7)]
    [InlineData(2)]
    public void AProgramLineIsStoredAndRuns(int mode)
    {
        string[] screen = Booted(mode).Type("10 PRINT \"HELLO\"\r").Type("RUN\r").RunFor(Settle).ScreenText();

        Assert.Equal(">10 PRINT \"HELLO\"", screen[5].TrimEnd());
        Assert.Equal(">RUN", screen[6].TrimEnd());
        Assert.Equal("HELLO", screen[7].TrimEnd());
        Assert.Equal(">", screen[8].TrimEnd());
    }

    [Fact]
    public void Mode4ClearsTheScreenAndThePromptIsInFortyColumns()
    {
        // MODE clears the screen and homes the cursor, so the prompt is alone on row 0. The decode
        // takes the mode from the OS and cuts the picture into forty columns of sixteen pixels and
        // thirty-two rows; it reads the prompt only if the chips drew mode 4.
        BbcSession s = Booted(7).Type("MODE 4\r").RunFor(Settle);
        string[] screen = s.ScreenText();

        Assert.Equal(4, s.Machine.Bus.Peek(0x0355));
        Assert.Equal(40, s.Machine.Bus.VideoUla.CharactersPerLine);
        Assert.Equal(32, screen.Length);
        Assert.All(screen, row => Assert.Equal(40, row.Length));
        Assert.Equal(">", screen[0].TrimEnd());
        Assert.All(screen.Skip(1), row => Assert.Equal("", row.TrimEnd()));
    }

    [Fact]
    public void PDotIsPrint()
    {
        // BASIC's abbreviations: P. is PRINT. The line is echoed as typed.
        string[] screen = Booted(7).Type("P.6*7\r").RunFor(Settle).ScreenText();

        Assert.Equal(">P.6*7", screen[5].TrimEnd());
        Assert.Equal("42", screen[6].Trim());
        Assert.Equal(">", screen[7].TrimEnd());
    }

    [Fact]
    public void ReadingTheSystemViaFromBasicGivesAByteAndThePromptComesBack()
    {
        // ?&FE40 reads the system VIA's port B through the bus, a slow 1 MHz access. Its value
        // depends on the latch and the inputs, so the test asks only for a byte, and that BASIC
        // carries on.
        string[] screen = Booted(7).Type("PRINT ?&FE40\r").RunFor(Settle).ScreenText();

        Assert.Equal(">PRINT ?&FE40", screen[5].TrimEnd());
        Assert.True(int.TryParse(screen[6].Trim(), out int value) && value is >= 0 and <= 255, $"row 6 reads \"{screen[6].Trim()}\"");
        Assert.Equal(">", screen[7].TrimEnd());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EveryCharacterTheOsDrawsReadsBack(int mode)
    {
        // VDU 32 to 126, the whole of the ROM's font but the block at 127, which VDU 127 never
        // draws. The output starts on the row after the command, wrapping at the mode's width.
        // BASIC takes most of a second over the loop in mode 2, so this waits two seconds.
        const string command = "FOR I=32 TO 126:VDU I:NEXT";
        string[] screen = Booted(mode).Type(command + "\r").RunFor(4_000_000).ScreenText();
        int columns = ScreenText.Grids[mode].Columns;
        int first = 5 + (((1 + command.Length) + columns - 1) / columns);

        string all = string.Concat(Enumerable.Range(32, 95).Select(c => (char)c));
        for (int i = 0; i < all.Length; i += columns)
        {
            string expected = all.Substring(i, Math.Min(columns, all.Length - i));
            Assert.Equal(expected, screen[first + (i / columns)][..expected.Length]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void PrintingPastTheBottomScrollsAndTheWrapBringsTheTopBack(int mode)
    {
        // Forty numbers are more rows than any mode has, so the OS scrolls the screen by moving
        // the CRTC's start address (video.md s2.5), and from then on the picture runs past the
        // end of memory and the hardware wraps it back to the mode's screen base. With the prompt
        // on the last row, the numbers above it count down from 40. The OS takes more than half a
        // second over it in the twenty kilobyte modes, as a real machine would, so this waits two.
        string[] screen = Booted(mode).Type("FOR I=1 TO 40:PRINT I:NEXT\r").RunFor(4_000_000).ScreenText();
        int last = screen.Length - 1;

        Assert.Equal(">", screen[last].TrimEnd());
        for (int n = 40; n > 40 - 15; n--)
        {
            Assert.Equal(n.ToString(System.Globalization.CultureInfo.InvariantCulture), screen[last - 41 + n].Trim());
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(1)]
    public void Vdu19MakesTheBackgroundBlue(int mode)
    {
        // VDU 19,0,4,0,0,0 sets logical colour 0, the background, to physical colour 4, blue: the
        // OS writes the palette (eight writes in mode 4, four in mode 1, video.md s2.3), and every
        // pixel of the background turns blue. The text stays white, so the decode still reads it.
        BbcSession s = Booted(mode);
        Assert.All(Row(s, 20), pixel => Assert.Equal(Black, pixel));

        string[] screen = s.Type("VDU 19,0,4,0,0,0\r").RunFor(Settle).ScreenText();

        Assert.Equal(">VDU 19,0,4,0,0,0", screen[5].TrimEnd());
        Assert.Equal(">", screen[6].TrimEnd());
        Assert.Equal(BootScreen.Banner, screen[1].TrimEnd());
        Assert.All(Row(s, 20), pixel => Assert.Equal(Blue, pixel));
        var colours = new HashSet<uint>();
        foreach (uint pixel in Row(s, 1))
        {
            colours.Add(pixel);
        }
        Assert.Equal([Blue, White], colours.Order());
    }

    [Fact]
    public void AFlashingColourAlternatesFieldByField()
    {
        // VDU 19,1,9,0,0,0 makes logical colour 1, the text, physical colour 9, flashing red and
        // cyan (video.md s2.3); the OS toggles the ULA's flash select from its vsync interrupt, 25
        // fields of each by default (s2.2), in the vsync interrupt, below the picture. Sampled once
        // a field for a hundred fields, the banner's row shows red in some and cyan in others, and
        // nothing else lit.
        BbcSession s = Booted(4).Type("VDU 19,1,9,0,0,0\r").RunFor(Settle);
        Framebuffer screen = s.Machine.Screen;
        int red = 0, cyan = 0;
        for (int field = 0; field < 100; field++)
        {
            long frames = screen.Frames;
            while (screen.Frames == frames)
            {
                s.RunFor(1_000);
            }

            // The even framebuffer rows only: one field draws them all, so they show one moment
            // of the flash. The odd rows hold the field before.
            var lit = new HashSet<uint>();
            uint[] pixels = Row(s, 1);
            for (int y = 0; y < 16; y += 2)
            {
                foreach (uint pixel in pixels.AsSpan(y * 640, 640))
                {
                    if (pixel != Black)
                    {
                        lit.Add(pixel);
                    }
                }
            }
            Assert.Single(lit);
            red += lit.Contains(Red) ? 1 : 0;
            cyan += lit.Contains(Cyan) ? 1 : 0;
        }

        Assert.Equal(100, red + cyan);
        Assert.True(red >= 30 && cyan >= 30, $"red in {red} fields, cyan in {cyan}");
    }

    private static BbcSession Booted(int mode) => new BbcSession(mode).Boot();

    /// <summary>The pixels of a text row in a mode of eight-line rows: sixteen framebuffer rows.</summary>
    private static uint[] Row(BbcSession s, int row)
    {
        ReadOnlySpan<uint> pixels = s.Machine.Screen.Pixels;
        return pixels.Slice(16 * row * 640, 16 * 640).ToArray();
    }
}
