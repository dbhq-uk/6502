using Xunit;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// The TIA's line, chip alone, from <c>docs/atari-2600/facts/tia.md</c> sections 3 (the horizontal
/// sync counter), 5 (WSYNC), 6 (RSYNC) and 7 (VSYNC and VBLANK), and section 4's delays. A write
/// "lands at colour clock W" when it is made with <see cref="Tia.HorizontalCounter"/> at W: the
/// clock the next <see cref="Tia.Tick"/> runs, so the write is seen from that clock on.
/// </summary>
public class TiaLineTests
{
    private const int Vsync = 0x00;
    private const int Vblank = 0x01;
    private const int Wsync = 0x02;
    private const int Rsync = 0x03;
    private const int Colubk = 0x09;
    private const int Pf0 = 0x0D;

    [Fact]
    public void Section_3_a_line_is_228_colour_clocks_and_the_counter_wraps()
    {
        var tia = new Tia();
        Assert.Equal(0, tia.HorizontalCounter);
        for (int n = 1; n <= 3 * Tia.ColourClocksPerLine; n++)
        {
            tia.Tick();
            Assert.Equal(n % 228, tia.HorizontalCounter);
        }
    }

    [Fact]
    public void Section_3_the_counter_is_towers_polynomial_counter_stepped_every_four_clocks()
    {
        // tia.md section 3's table: the state at each decode, entered at count x 4 colour clocks.
        (int Count, int State)[] decodes =
        [
            (0, 0b000000),
            (1, 0b100000),
            (4, 0b111100),  // SHS
            (8, 0b110111),  // RHS
            (12, 0b001111), // RCB
            (16, 0b011100), // RHB
            (18, 0b010111), // LRHB
            (36, 0b101100), // CNT
            (56, 0b010100), // SHB
        ];
        var tia = new Tia();
        foreach ((int count, int state) in decodes)
        {
            Clock.RunTo(tia, count * 4);
            Assert.Equal(state, tia.PolynomialCounter);
            for (int phase = 1; phase < 4; phase++)
            {
                tia.Tick();
                Assert.Equal(state, tia.PolynomialCounter);
            }
        }

        // SHB's reset: four clocks after count 56 the counter is 000000 again, not its next state.
        Clock.RunTo(tia, 0);
        Assert.Equal(0, tia.PolynomialCounter);
    }

    [Fact]
    public void Section_3_the_illegal_state_resets_and_the_rule_is_xnor_of_the_two_rightmost_bits()
    {
        Assert.Equal(0, Tia.NextState(0b111111));
        Assert.Equal(0b100000, Tia.NextState(0b000000));
        Assert.Equal(0b011111, Tia.NextState(0b111110));

        // From 000000 the rule visits 63 states and comes back (tia.md section 3).
        int state = 0;
        var seen = new HashSet<int>();
        do
        {
            Assert.True(seen.Add(state));
            state = Tia.NextState(state);
        }
        while (state != 0);
        Assert.Equal(63, seen.Count);
    }

    [Fact]
    public void Section_3_the_blank_is_the_first_68_clocks_and_pixel_x_is_clock_68_plus_x()
    {
        var tia = new Tia();
        for (int clock = 0; clock < 228; clock++)
        {
            Assert.Equal(clock < 68, tia.HorizontalBlank);
            tia.Tick();
        }

        // A background write landing at clock 68 + x colours pixel x and every pixel after it.
        foreach (int x in new[] { 0, 1, 91, 159 })
        {
            tia = new Tia();
            Clock.RunTo(tia, 68 + x);
            tia.Write(Colubk, 0x0E);
            byte[] row = Clock.FinishLine(tia);
            for (int p = 0; p < 160; p++)
            {
                Assert.Equal(p >= x ? 0x0E : 0x00, row[p]);
            }
        }
    }

    [Fact]
    public void Section_3_the_counter_keeps_its_line_whatever_the_registers_hold()
    {
        var tia = new Tia();
        tia.Write(Vblank, 0x02);
        tia.Write(Colubk, 0x44);
        tia.Write(Pf0, 0xF0);
        Clock.Run(tia, 1000);
        Assert.Equal(1000 % 228, tia.HorizontalCounter);
    }

    [Fact]
    public void Section_5_halt_release_is_due_from_colour_clock_0_of_the_next_line()
    {
        // tia.md section 5: the latch is reset by the next line's blank, at colour clock 0.
        foreach (int w in new[] { 3, 60, 159, 225 })
        {
            var tia = new Tia();
            Clock.RunTo(tia, w);
            Assert.True(tia.HaltReleaseDue);
            tia.Write(Wsync, 0);
            Assert.False(tia.HaltReleaseDue);
            Assert.True(tia.RdyLow);
            while (tia.HorizontalCounter != 0)
            {
                tia.Tick();
                Assert.False(tia.HaltReleaseDue);
            }

            // The tick of colour clock 0 releases RDY.
            tia.Tick();
            Assert.True(tia.HaltReleaseDue);
            Assert.False(tia.RdyLow);
        }
    }

    [Fact]
    public void Section_5_a_wsync_landing_at_the_line_end_is_released_by_the_next_clock()
    {
        // A write on cycle 76 lands at colour clock 228, which is the next line's 0; the latch is
        // reset on that clock, so it costs nothing ("absorbed at the wrap").
        var tia = new Tia();
        Clock.Run(tia, 228);
        Assert.Equal(0, tia.HorizontalCounter);
        tia.Write(Wsync, 0);
        Assert.False(tia.HaltReleaseDue);
        tia.Tick();
        Assert.True(tia.HaltReleaseDue);

        // A write one clock later waits a whole line.
        tia.Write(Wsync, 0);
        Clock.Run(tia, 226);
        Assert.False(tia.HaltReleaseDue);
        tia.Tick();
        Assert.Equal(0, tia.HorizontalCounter);
        Assert.False(tia.HaltReleaseDue);
        tia.Tick();
        Assert.True(tia.HaltReleaseDue);
    }

    [Fact]
    public void Section_6_the_model_restarts_the_line_three_clocks_after_rsync_a_choice()
    {
        // tia.md section 6's worked example: RSYNC written on cycle 36, W 108. The restart is a
        // choice [guessing - verify], three clocks after W (the sheet's "about 4", and see
        // CycleOrderTests for why three).
        var tia = new Tia();
        tia.Write(Colubk, 0x0E);
        Clock.RunTo(tia, 108);
        tia.Write(Rsync, 0);
        tia.Tick();
        tia.Tick();
        Assert.False(tia.HorizontalBlank);
        tia.Tick();

        // The line starts again at 000000 with the blank on, and the frame has a new row.
        Assert.Equal(0, tia.HorizontalCounter);
        Assert.Equal(0, tia.PolynomialCounter);
        Assert.True(tia.HorizontalBlank);
        Clock.Run(tia, 228);
        Assert.Equal(0, tia.HorizontalCounter);

        Clock.EndFrame(tia);
        Assert.Equal(2, tia.Frame.Lines);
        byte[] cut = Clock.Row(tia, 0);
        byte[] full = Clock.Row(tia, 1);

        // The cut line drew pixels 0 to 42 (clocks 68 to 110) and leaves the rest black.
        Assert.All(cut[..43], p => Assert.Equal(0x0E, p));
        Assert.All(cut[43..], p => Assert.Equal(0x00, p));
        Assert.All(full, p => Assert.Equal(0x0E, p));
    }

    [Fact]
    public void Section_6_a_line_rsync_cuts_short_shows_nothing_of_an_older_frame()
    {
        // Two white frames of two lines fill both of the frame's buffers; then, after a black line,
        // a line in $44 is cut at W 108, and the rest of its row must be black, not the white two
        // frames old. (Row 0 is the carried line, so the cut line is row 1.)
        var tia = new Tia();
        tia.Write(Colubk, 0x0E);
        for (int frame = 0; frame < 2; frame++)
        {
            Clock.Run(tia, 2 * 228);
            Clock.EndFrame(tia);
        }

        tia.Write(Colubk, 0x00);
        Clock.Run(tia, 228);
        tia.Write(Colubk, 0x44);
        Clock.RunTo(tia, 108);
        tia.Write(Rsync, 0);
        Clock.Run(tia, 3);
        tia.Write(Colubk, 0x00);
        Clock.Run(tia, 228);
        Clock.EndFrame(tia);

        Assert.Equal(3, tia.Frame.Lines);
        byte[] cut = Clock.Row(tia, 1);
        Assert.All(cut[..43], p => Assert.Equal(0x44, p));
        Assert.All(cut[43..], p => Assert.Equal(0x00, p));
    }

    [Fact]
    public void Section_6_rsync_redraws_the_playfield_from_the_new_lines_pixel_0()
    {
        // The playfield is decoded from the counter RSYNC resets (tia.md section 6).
        var tia = new Tia();
        tia.Write(0x08, 0x0E);
        tia.Write(Pf0, 0xF0);
        Clock.RunTo(tia, 108);
        tia.Write(Rsync, 0);
        Clock.Run(tia, 3 + 228);
        Clock.EndFrame(tia);
        byte[] row = Clock.Row(tia, 1);
        Assert.All(row[..16], p => Assert.Equal(0x0E, p));
        Assert.All(row[16..80], p => Assert.Equal(0x00, p));
    }

    [Fact]
    public void Section_6_the_line_rsync_starts_releases_a_pending_wsync_the_models_choice()
    {
        // tia.md open item 1 asks whether it does; the model releases RDY at every line start,
        // RSYNC's included [guessing - verify].
        var tia = new Tia();
        Clock.RunTo(tia, 30);
        tia.Write(Wsync, 0);
        Clock.RunTo(tia, 108);
        tia.Write(Rsync, 0);
        Clock.Run(tia, 3);
        Assert.False(tia.HaltReleaseDue);
        tia.Tick();
        Assert.True(tia.HaltReleaseDue);
    }

    [Fact]
    public void Section_7_vsync_on_then_off_ends_a_frame_and_the_plans_item_4_makes_that_line_row_0()
    {
        var tia = new Tia();

        // Line 0: VSYNC on at clock 10, off at clock 20: the frame ends, and this line, the first
        // after the VSYNC lines, is the next frame's row 0. It turns white from pixel 30.
        Clock.RunTo(tia, 10);
        tia.Write(Vsync, 0x02);
        Assert.Equal(0, tia.Frame.Frames);
        Clock.RunTo(tia, 20);
        tia.Write(Vsync, 0x00);
        Assert.Equal(1, tia.Frame.Frames);
        Clock.RunTo(tia, 98);
        tia.Write(Colubk, 0x0E);

        // Line 1 black, line 2 white; VSYNC ends on line 3.
        Clock.RunTo(tia, 0);
        tia.Write(Colubk, 0x00);
        Clock.Run(tia, 228);
        tia.Write(Colubk, 0x0E);
        Clock.Run(tia, 228 + 5);
        tia.Write(Vsync, 0x02);
        tia.Write(Vsync, 0x00);

        Assert.Equal(2, tia.Frame.Frames);
        Assert.Equal(3, tia.Frame.Lines);
        byte[] first = Clock.Row(tia, 0);
        Assert.All(first[..30], p => Assert.Equal(0x00, p));
        Assert.All(first[30..], p => Assert.Equal(0x0E, p));
        Assert.All(Clock.Row(tia, 1), p => Assert.Equal(0x00, p));
        Assert.All(Clock.Row(tia, 2), p => Assert.Equal(0x0E, p));
    }

    [Fact]
    public void Section_7_vsync_held_or_started_ends_no_frame()
    {
        var tia = new Tia();
        tia.Write(Vsync, 0x02);
        tia.Write(Vsync, 0x02);
        tia.Write(Vsync, 0xFF);
        Assert.Equal(0, tia.Frame.Frames);
        tia.Write(Vsync, 0x00);
        Assert.Equal(1, tia.Frame.Frames);
        tia.Write(Vsync, 0x00);
        Assert.Equal(1, tia.Frame.Frames);
    }

    [Fact]
    public void Section_7_vblank_draws_black_and_keeps_counting()
    {
        var tia = new Tia();
        tia.Write(Colubk, 0x0E);
        tia.Write(0x08, 0x46);
        tia.Write(Pf0, 0xF0);
        tia.Write(Vblank, 0x02);
        Clock.Run(tia, 3 * 228);
        Assert.Equal(0, tia.HorizontalCounter);
        tia.Write(Vblank, 0x00);
        Clock.Run(tia, 228);
        Clock.EndFrame(tia);

        Assert.Equal(4, tia.Frame.Lines);
        for (int line = 0; line < 3; line++)
        {
            Assert.All(Clock.Row(tia, line), p => Assert.Equal(0x00, p));
        }

        byte[] shown = Clock.Row(tia, 3);
        Assert.All(shown[..16], p => Assert.Equal(0x46, p));
        Assert.All(shown[16..80], p => Assert.Equal(0x0E, p));
    }

    [Fact]
    public void Section_4_vblank_shows_one_clock_after_the_write()
    {
        // tia.md section 7's worked example: set on cycle 45 (W 135), cleared on cycle 48 (W 144):
        // pixel 67 still shows, pixels 68 to 76 are black, 77 shows again.
        var tia = new Tia();
        tia.Write(Colubk, 0x0E);
        Clock.RunTo(tia, 135);
        tia.Write(Vblank, 0x02);
        Clock.RunTo(tia, 144);
        tia.Write(Vblank, 0x00);
        byte[] row = Clock.FinishLine(tia);
        for (int x = 0; x < 160; x++)
        {
            Assert.Equal(x is >= 68 and <= 76 ? 0x00 : 0x0E, row[x]);
        }
    }

    [Fact]
    public void Section_4_a_colour_register_shows_on_the_clock_the_write_lands()
    {
        // tia.md section 4's worked examples: COLUBK on cycle 53 (W 159) from pixel 91, and back on
        // cycle 56 (W 168) from pixel 100.
        var tia = new Tia();
        Clock.RunTo(tia, 159);
        tia.Write(Colubk, 0x44);
        Clock.RunTo(tia, 168);
        tia.Write(Colubk, 0x00);
        byte[] row = Clock.FinishLine(tia);
        for (int x = 0; x < 160; x++)
        {
            Assert.Equal(x is >= 91 and <= 99 ? 0x44 : 0x00, row[x]);
        }
    }

    [Fact]
    public void Section_19_1_bit_0_of_a_colour_is_not_kept_in_the_frame()
    {
        var tia = new Tia();
        tia.Write(Colubk, 0x45);
        byte[] row = Clock.FinishLine(tia);
        Assert.All(row, p => Assert.Equal(0x44, p));
    }

    [Fact]
    public void Review_focus_3_a_tia_that_never_sees_vsync_ends_a_frame_every_400_lines()
    {
        var tia = new Tia();
        Clock.Run(tia, (Tia.MaxLines * 228) - 1);
        Assert.Equal(0, tia.Frame.Frames);
        tia.Tick();
        Assert.Equal(1, tia.Frame.Frames);
        Assert.Equal(Tia.MaxLines, tia.Frame.Lines);
        Clock.Run(tia, Tia.MaxLines * 228);
        Assert.Equal(2, tia.Frame.Frames);
        Assert.Equal(Tia.MaxLines, tia.Frame.Lines);
        Assert.Equal(FrameBuffer.MaxLines, Tia.MaxLines);
    }

    [Fact]
    public void Review_focus_3_vsync_strobed_every_few_lines_makes_short_frames()
    {
        var tia = new Tia();
        for (int frame = 1; frame <= 50; frame++)
        {
            Clock.Run(tia, 3 * 228);
            tia.Write(Vsync, 0x02);
            tia.Write(Vsync, 0x00);
            Assert.Equal(frame, tia.Frame.Frames);
            Assert.Equal(3, tia.Frame.Lines);
            Assert.Equal(3 * FrameBuffer.Width, tia.Frame.ColourNumbers.Length);
        }

        // VSYNC ended twice on one line: a frame of no lines, and nothing grows.
        tia.Write(Vsync, 0x02);
        tia.Write(Vsync, 0x00);
        Assert.Equal(0, tia.Frame.Lines);
        Assert.Equal(0, tia.Frame.ColourNumbers.Length);
    }

    [Fact]
    public void Power_on_puts_the_counter_the_frame_and_the_registers_back()
    {
        var tia = new Tia();
        tia.Write(Colubk, 0x0E);
        tia.Write(Vblank, 0x02);
        tia.Write(Pf0, 0xF0);
        Clock.Run(tia, 1000);
        tia.Write(Rsync, 0);
        tia.Write(Wsync, 0);
        tia.Write(Vsync, 0x02);
        tia.Write(Vsync, 0x00);
        tia.Tick();

        tia.PowerOn();
        Assert.Equal(0, tia.HorizontalCounter);
        Assert.Equal(0, tia.PolynomialCounter);
        Assert.True(tia.HorizontalBlank);
        Assert.True(tia.HaltReleaseDue);
        Assert.Equal(0, tia.Frame.Frames);
        Assert.Equal(0, tia.Frame.Lines);

        // Nothing of before shows: the line is all background 0, and a full line is 228 clocks.
        byte[] row = Clock.FinishLine(tia);
        Assert.All(row, p => Assert.Equal(0x00, p));
    }
}

/// <summary>Drives a TIA alone, a colour clock at a time, and reads the rows of its frame.</summary>
internal static class Clock
{
    public static void Run(Tia tia, int clocks)
    {
        for (int i = 0; i < clocks; i++)
        {
            tia.Tick();
        }
    }

    /// <summary>Ticks until the next clock is <paramref name="clock"/>: not at all if it is, and at most a line and a bit.</summary>
    public static void RunTo(Tia tia, int clock)
    {
        if (tia.HorizontalCounter == clock)
        {
            return;
        }

        for (int i = 0; i < 300; i++)
        {
            tia.Tick();
            if (tia.HorizontalCounter == clock)
            {
                return;
            }
        }

        throw new InvalidOperationException($"the counter never reached {clock}");
    }

    /// <summary>Ends the frame with VSYNC on and off, so the rows drawn so far can be read.</summary>
    public static void EndFrame(Tia tia)
    {
        tia.Write(0x00, 0x02);
        tia.Write(0x00, 0x00);
    }

    /// <summary>Runs to the end of the line from power on, ends the frame there and returns its one row.</summary>
    public static byte[] FinishLine(Tia tia)
    {
        do
        {
            tia.Tick();
        }
        while (tia.HorizontalCounter != 0);

        EndFrame(tia);
        Assert.Equal(1, tia.Frame.Lines);
        return Row(tia, 0);
    }

    public static byte[] Row(Tia tia, int row) =>
        tia.Frame.ColourNumbers.Slice(row * FrameBuffer.Width, FrameBuffer.Width).ToArray();
}
