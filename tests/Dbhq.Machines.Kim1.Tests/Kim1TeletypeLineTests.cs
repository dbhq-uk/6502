using Xunit;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// The teletype on its own, with no KIM-1: the bits its keyboard puts on the
/// line, and the characters its printer makes of a line driven by hand. Every
/// expected edge is worked out here from the baud rate, not taken from the
/// class.
/// </summary>
public sealed class Kim1TeletypeLineTests
{
    private const double Bit110 = 1_000_000.0 / 110;

    /// <summary>Runs the teletype one cycle at a time, as the bus does, from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static void Tick(Kim1Teletype tty, long from, long to)
    {
        for (long c = from; c <= to; c++)
        {
            tty.Advance(c);
        }
    }

    [Fact]
    public void AKeySendsAStartBitSevenBitsOfAsciiAMarkAndTwoStopBitsAt110Baud()
    {
        var tty = new Kim1Teletype();
        var edges = new List<(long Cycle, bool Mark)>();
        tty.OutputChanged += (c, m) => edges.Add((c, m));

        // PB0 high and PB5 low: the printer loop follows the keyboard, so the
        // keyboard's own bits can be watched on it.
        tty.Drive(1, pb0: true, pb5: false);
        tty.Send('A');
        Tick(tty, 1, 200_000);

        // 'A' is $41: on the line, least significant bit first, with the
        // eighth bit a mark: 1000 0011, after a start bit (0) and before two
        // stop bits (1 1). The levels are 0, 1,0,0,0,0,0,1,1, 1,1.
        bool[] levels = [false, true, false, false, false, false, false, true, true, true, true];
        long start = edges[0].Cycle;
        var expected = new List<(long, bool)>();
        bool level = true;
        for (int bit = 0; bit < levels.Length; bit++)
        {
            if (levels[bit] != level)
            {
                expected.Add((start + (long)Math.Floor(bit * Bit110), levels[bit]));
                level = levels[bit];
            }
        }

        Assert.Equal(expected, edges);

        // The bit times are 9,090 or 9,091 cycles, never more or less.
        Assert.Equal(9090, edges[1].Cycle - edges[0].Cycle);
        Assert.Equal(5 * Bit110, edges[3].Cycle - edges[2].Cycle, 1.0);

        // And the line is a mark again from the end of the second stop bit on.
        Assert.True(tty.KeyboardMark);
        Assert.False(tty.Sending);
    }

    [Fact]
    public void ThePrinterSamplesAtTheMiddleOfEachBitAndDecodesTheCharacter()
    {
        var tty = new Kim1Teletype();
        DriveCharacter(tty, 1_000, 'K', Bit110, stopMark: true);
        Assert.Equal("K", tty.Drain());
        Assert.Equal(0, tty.FramingErrors);
    }

    [Fact]
    public void ACharacterSentAFewPerCentSlowStillDecodes()
    {
        // The monitor's own bits are a few cycles short of 1,000,000 / 110,
        // because its delay loop counts in 14-cycle steps. Sampling at the
        // middle of each bit tolerates far more than that.
        var tty = new Kim1Teletype();
        DriveCharacter(tty, 1_000, 'M', Bit110 * 1.03, stopMark: true);
        DriveCharacter(tty, 200_000, 'I', Bit110 * 0.97, stopMark: true);
        Assert.Equal("MI", tty.Drain());
    }

    [Fact]
    public void ACharacterWhoseStopBitIsASpaceIsThrownAwayAndTheNextStillDecodes()
    {
        var tty = new Kim1Teletype();
        DriveCharacter(tty, 1_000, 'X', Bit110, stopMark: false);
        Assert.Equal("", tty.Drain());
        Assert.Equal(1, tty.FramingErrors);

        DriveCharacter(tty, 200_000, 'Y', Bit110, stopMark: true);
        Assert.Equal("Y", tty.Drain());
        Assert.Equal(1, tty.FramingErrors);
    }

    [Fact]
    public void ASpaceShorterThanHalfABitIsNotAStartBit()
    {
        var tty = new Kim1Teletype();
        tty.Drive(100, pb0: false, pb5: true);
        tty.Drive(106, pb0: true, pb5: true);
        Tick(tty, 106, 50_000);
        Assert.Equal("", tty.Drain());
        Assert.Equal(0, tty.FramingErrors);
        Assert.False(tty.Receiving);
    }

    [Fact]
    public void PB5HighHoldsPA7AtAMarkAndKeepsTheKeyboardOffThePrinter()
    {
        var tty = new Kim1Teletype();
        var edges = new List<(long, bool)>();
        tty.OutputChanged += (c, m) => edges.Add((c, m));
        tty.Drive(1, pb0: true, pb5: true);
        tty.Send('@');
        var pa7 = new List<bool>();
        var keyboard = new List<bool>();
        for (long c = 1; c <= 120_000; c++)
        {
            tty.Advance(c);
            pa7.Add(tty.Pa7);
            keyboard.Add(tty.KeyboardMark);
        }

        Assert.Contains(false, keyboard);
        Assert.DoesNotContain(false, pa7);
        Assert.Empty(edges);
        Assert.Equal("", tty.Drain());
    }

    [Fact]
    public void WithPB5LowTheKeyboardIsEchoedToThePrinterByTheWiring()
    {
        var tty = new Kim1Teletype();
        tty.Drive(1, pb0: true, pb5: false);
        tty.Send("HI");
        Tick(tty, 1, 400_000);
        Assert.Equal("HI", tty.Drain());
    }

    [Fact]
    public void PB0LowHoldsThePrinterAtASpaceWhateverTheKeyboardDoes()
    {
        var tty = new Kim1Teletype();
        tty.Drive(1, pb0: false, pb5: false);
        tty.Send('U');
        var marks = new List<bool>();
        for (long c = 1; c <= 120_000; c++)
        {
            tty.Advance(c);
            marks.Add(tty.OutputMark);
        }

        Assert.DoesNotContain(true, marks);
    }

    [Fact]
    public void QueuedKeysWaitForBothLinesToBeQuiet()
    {
        var tty = new Kim1Teletype();
        var starts = new List<long>();
        tty.Drive(1, pb0: true, pb5: false);
        tty.OutputChanged += (c, m) =>
        {
            if (!m && !tty.Receiving)
            {
                starts.Add(c);
            }
        };
        tty.Send("AB");
        Tick(tty, 1, 400_000);

        // The second key starts once the first has finished, its two stop
        // bits included, and the line has then been quiet for four bits.
        long first = 1;
        long expected = first + (11 * 1_000_000 / 110) + (4 * 1_000_000 / 110);
        Assert.Equal("AB", tty.Drain());
        Assert.Equal(2, starts.Count);
        Assert.Equal(first, starts[0]);
        Assert.InRange(starts[1], expected, expected + 1);
    }

    /// <summary>
    /// The bus only calls the teletype on cycles where something is due, so a
    /// key queued after a long quiet spell must not start in the past: it goes
    /// on the first cycle the teletype is advanced to after it was queued.
    /// </summary>
    [Fact]
    public void AKeyQueuedAfterALongQuietSpellStartsOnTheNextCycleNotInThePast()
    {
        var tty = new Kim1Teletype();
        var edges = new List<long>();
        tty.OutputChanged += (c, _) => edges.Add(c);
        tty.Drive(1, pb0: true, pb5: false);
        Assert.Equal(long.MaxValue, tty.Due);

        tty.Send('A');
        tty.Advance(500_000);
        Assert.Equal([500_000L], edges);
        Tick(tty, 500_001, 700_000);
        Assert.Equal("A", tty.Drain());
    }

    [Theory]
    [InlineData("0200[SPACE]G", "0200 G")]
    [InlineData("[RUBOUT]", "\u007F")]
    [InlineData("18.[CR][LF]", "18.\r\n")]
    public void KeysAreWrittenAsTheManualWritesThem(string keys, string sent) =>
        Assert.Equal(sent, Kim1Teletype.Parse(keys));

    [Theory]
    [InlineData("0200 G")]
    [InlineData("[RUB OUT]")]
    [InlineData("[TAB]")]
    [InlineData("[SPACE")]
    public void AKeyThatCannotBeSeenOnThePageIsRefused(string keys) =>
        Assert.Throws<FormatException>(() => Kim1Teletype.Parse(keys));

    /// <summary>Drives the printer loop through one character, as the KIM-1's PB0 would, with bits <paramref name="bit"/> cycles long.</summary>
    private static void DriveCharacter(Kim1Teletype tty, long start, char c, double bit, bool stopMark)
    {
        bool[] levels = new bool[10];
        levels[0] = false;
        for (int i = 0; i < 8; i++)
        {
            levels[i + 1] = ((c >> i) & 1) != 0;
        }

        levels[9] = stopMark;
        long cycle = start;
        for (int i = 0; i < levels.Length; i++)
        {
            long edge = start + (long)Math.Round(i * bit);
            Tick(tty, cycle, edge);
            tty.Drive(edge, pb0: levels[i], pb5: true);
            cycle = edge;
        }

        long end = start + (long)Math.Round(11 * bit);
        Tick(tty, cycle, end);
        tty.Drive(end, pb0: true, pb5: true);
        Tick(tty, end, end + (long)(2 * bit));
    }

    /// <summary>
    /// Reading what the printer has decoded between cycles brings it up to
    /// the cycle the machine has run, and no further: a key queued since then
    /// has not had a cycle to start on, so it does not start on or before
    /// that cycle.
    /// </summary>
    [Fact]
    public void CatchingUpDoesNotStartAKeyQueuedSinceTheLastCycle()
    {
        var tty = new Kim1Teletype();
        tty.Drive(1, pb0: true, pb5: false);
        tty.Advance(100);

        tty.Send('A');
        tty.CatchUp(100);
        Assert.True(tty.KeyboardMark, "the key started on a cycle that has already run");
        Assert.False(tty.Sending);
        Assert.Equal(1, tty.Queued);
        Assert.True(tty.Due <= 101, "the terminal would not be called on the next cycle");

        tty.Advance(101);
        Assert.False(tty.KeyboardMark);
        Assert.True(tty.Sending);
    }

    /// <summary>
    /// The quiet time runs from the end of what the printer last heard: for a
    /// good character, the middle of its stop bit, where the receiver reads it.
    /// </summary>
    [Fact]
    public void TheKeyboardWaitsFourBitsFromTheMiddleOfTheMonitorsLastStopBit()
    {
        var tty = new Kim1Teletype();
        DriveCharacter(tty, 1_000, '\0', Bit110, stopMark: true);
        Assert.Equal("\0", tty.Drain());

        tty.Send('A');
        long started = FirstCycleSending(tty, 119_182, 300_000);
        long expected = 1_000 + (long)(9.5 * Bit110) + (long)(4 * Bit110);
        Assert.InRange(started, expected - 1, expected + 1);
    }

    /// <summary>
    /// After a framing error the printer gives up at the middle of the bad
    /// stop bit, but the line is not quiet until it has gone back to a mark.
    /// </summary>
    [Fact]
    public void TheKeyboardWaitsFourBitsFromTheLineReturningToAMarkAfterAFramingError()
    {
        var tty = new Kim1Teletype();
        DriveCharacter(tty, 1_000, 'X', Bit110, stopMark: false);
        Assert.Equal(1, tty.FramingErrors);

        tty.Send('A');
        long started = FirstCycleSending(tty, 119_182, 300_000);
        long expected = 1_000 + (long)Math.Round(11 * Bit110) + (long)(4 * Bit110);
        Assert.InRange(started, expected - 1, expected + 1);
    }

    /// <summary>Runs the teletype a cycle at a time and returns the first cycle on which its keyboard has a character on the line.</summary>
    private static long FirstCycleSending(Kim1Teletype tty, long from, long to)
    {
        for (long c = from; c <= to; c++)
        {
            tty.Advance(c);
            if (tty.Sending)
            {
                return c;
            }
        }

        return -1;
    }
}
