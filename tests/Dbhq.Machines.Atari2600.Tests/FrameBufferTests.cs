using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The frame: colour numbers, a row a line, from the line VSYNC ends on to the next end of VSYNC or
/// the line limit (the plan's "Fixed before running" items 2 and 4), and its window for a region
/// through that region's palette (<c>docs/atari-2600/facts/region.md</c> section 3).
/// </summary>
public class FrameBufferTests
{
    private const uint Black = 0xFF000000;

    [Fact]
    public void The_frame_is_160_wide_and_at_most_400_lines()
    {
        Assert.Equal(160, FrameBuffer.Width);
        Assert.Equal(400, FrameBuffer.MaxLines);
    }

    [Theory]
    [InlineData("NTSC", 200)]
    [InlineData("PAL", 200)]
    [InlineData("NTSC", 100)]
    [InlineData("PAL", 37)]
    public void After_a_frame_frames_rises_by_one_and_lines_is_the_programs_line_count(string regionName, int lines)
    {
        // LDA #$0E; STA COLUBK; then a frame: LDA #2; STA VSYNC; STA WSYNC; LDA #0; STA VSYNC (the
        // line this ends on is row 0); LDX #lines-1; STA WSYNC; DEX; BNE; JMP to the frame.
        Atari2600 machine = TestPrograms.Machine(
            TestPrograms.RegionNamed(regionName),
            0xA9, 0x0E, 0x85, 0x09,
            0xA9, 0x02, 0x85, 0x00, 0x85, 0x02, 0xA9, 0x00, 0x85, 0x00,
            0xA2, (byte)(lines - 1), 0x85, 0x02, 0xCA, 0xD0, 0xFB,
            0x4C, 0x04, 0xF0);

        // The first frame ends at the first end of VSYNC, a short one from power on.
        machine.RunFrames(1);
        Assert.Equal(1, machine.Bus.Tia.Frame.Frames);
        for (int frame = 2; frame <= 4; frame++)
        {
            machine.RunFrames(1);
            Assert.Equal(frame, machine.Bus.Tia.Frame.Frames);
            Assert.Equal(lines, machine.Bus.Tia.Frame.Lines);
            Assert.Equal(lines * FrameBuffer.Width, machine.Bus.Tia.Frame.ColourNumbers.Length);
        }

        // Every pixel of every row is the background: the program changes nothing else.
        Assert.All(machine.Bus.Tia.Frame.ColourNumbers.ToArray(), p => Assert.Equal(0x0E, p));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void A_frame_of_the_background_at_0E_is_that_colour_across_the_window(string regionName)
    {
        Region region = TestPrograms.RegionNamed(regionName);
        Tia tia = Frame(region.NominalLines, _ => 0x0E);
        uint[] window = new uint[FrameBuffer.Width * region.ShownLines];
        tia.Frame.ToRgba(region, window);
        uint white = region.Palette.Rgba(0x0E);
        Assert.Equal(0xFFFFFFFFu, white);
        Assert.All(window, p => Assert.Equal(white, p));
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void To_rgba_takes_the_regions_window_through_its_palette(string regionName)
    {
        // Each line its own colour number, so a row out of place shows.
        Region region = TestPrograms.RegionNamed(regionName);
        Tia tia = Frame(region.NominalLines, line => (byte)(line * 2));
        uint[] window = new uint[FrameBuffer.Width * region.ShownLines];
        Array.Fill(window, 0x12345678u);
        tia.Frame.ToRgba(region, window);
        for (int row = 0; row < region.ShownLines; row++)
        {
            uint expected = region.Palette.Rgba((byte)((region.FirstShownLine + row) * 2));
            for (int x = 0; x < FrameBuffer.Width; x++)
            {
                Assert.Equal(expected, window[(row * FrameBuffer.Width) + x]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TestPrograms.Regions), MemberType = typeof(TestPrograms))]
    public void To_rgba_is_black_below_a_short_frame(string regionName)
    {
        // A long frame first, then two short ones, so the buffer shown last still holds the long
        // frame's rows below the short one's: those must not show.
        Region region = TestPrograms.RegionNamed(regionName);
        const int Lines = 100;
        var tia = new Tia();
        Frames(tia, 300, _ => 0x0E);
        Frames(tia, Lines, _ => 0x0E);
        Frames(tia, Lines, _ => 0x0E);
        uint[] window = new uint[FrameBuffer.Width * region.ShownLines];
        tia.Frame.ToRgba(region, window);
        for (int row = 0; row < region.ShownLines; row++)
        {
            uint expected = region.FirstShownLine + row < Lines ? 0xFFFFFFFFu : Black;
            Assert.Equal(expected, window[row * FrameBuffer.Width]);
            Assert.Equal(expected, window[(row * FrameBuffer.Width) + FrameBuffer.Width - 1]);
        }
    }

    [Fact]
    public void To_rgba_before_any_frame_is_black_and_a_short_span_is_refused()
    {
        var tia = new Tia();
        uint[] window = new uint[FrameBuffer.Width * Region.Pal.ShownLines];
        tia.Frame.ToRgba(Region.Pal, window);
        Assert.All(window, p => Assert.Equal(Black, p));

        uint[] small = new uint[(FrameBuffer.Width * Region.Ntsc.ShownLines) - 1];
        Assert.Throws<ArgumentException>(() => tia.Frame.ToRgba(Region.Ntsc, small));
        Assert.Throws<ArgumentNullException>(() => tia.Frame.ToRgba(null!, window));
    }

    [Fact]
    public void The_window_follows_the_region_given_not_the_one_the_frame_was_made_in()
    {
        // The region switch changes the palette at once (region.md section 5).
        Tia tia = Frame(Region.Pal.NominalLines, _ => 0x58);
        uint[] ntsc = new uint[FrameBuffer.Width * Region.Ntsc.ShownLines];
        uint[] pal = new uint[FrameBuffer.Width * Region.Pal.ShownLines];
        tia.Frame.ToRgba(Region.Ntsc, ntsc);
        tia.Frame.ToRgba(Region.Pal, pal);
        Assert.All(ntsc, p => Assert.Equal(Region.Ntsc.Palette.Rgba(0x58), p));
        Assert.All(pal, p => Assert.Equal(Region.Pal.Palette.Rgba(0x58), p));
        Assert.NotEqual(ntsc[0], pal[0]);
    }

    [Fact]
    public void The_last_frame_stays_as_it_was_while_the_next_is_drawn()
    {
        Tia tia = Frame(10, _ => 0x0E);
        tia.Write(0x09, 0x44);
        Clock.Run(tia, 5 * 228);
        Assert.Equal(10, tia.Frame.Lines);
        Assert.All(tia.Frame.ColourNumbers.ToArray(), p => Assert.Equal(0x0E, p));
    }

    // A TIA alone: one frame of the given lines, each line's background chosen by its number from
    // power on, ended by VSYNC as the next line starts.
    private static Tia Frame(int lines, Func<int, byte> background)
    {
        var tia = new Tia();
        Frames(tia, lines, background);
        return tia;
    }

    // One more frame on the TIA, ended by VSYNC as its last line ends.
    private static void Frames(Tia tia, int lines, Func<int, byte> background)
    {
        for (int line = 0; line < lines; line++)
        {
            tia.Write(0x09, background(line));
            Clock.Run(tia, 228);
        }

        Clock.EndFrame(tia);
        Assert.Equal(lines, tia.Frame.Lines);
    }
}
