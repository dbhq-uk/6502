using Xunit;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>The display on its own, fed the decoder output and segment lines a scan produces.</summary>
public sealed class Kim1DisplayTests
{
    private static readonly byte[] Shapes = [0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F, 0x77, 0x7C, 0x39, 0x5E, 0x79, 0x71];

    /// <summary>
    /// Scans the six digits once, the way the monitor's CONVD does it: blank
    /// the segments, select the digit, drive the segments, hold for 500
    /// cycles. Returns the cycle it finished on.
    /// </summary>
    private static long Scan(Kim1Display display, long cycle, string digits)
    {
        for (int i = 0; i < 6; i++)
        {
            display.Observe(cycle, i + 3, 0x00);
            display.Observe(cycle + 4, i + 4, 0x00);
            display.Observe(cycle + 8, i + 4, Shapes[Convert.ToInt32(digits[i].ToString(), 16)]);
            cycle += 508;
        }

        display.Observe(cycle, 10, 0x00);
        return cycle;
    }

    [Fact]
    public void ADigitShowsTheSegmentsHeldWhileItWasSelected()
    {
        var display = new Kim1Display();
        long end = Scan(display, 100, "1C4F02");
        Assert.Equal("1C4F02", display.Read(end));

        end = Scan(display, end, "ABCDEF");
        Assert.Equal("ABCDEF", display.Read(end));
    }

    [Fact]
    public void EverySegmentPatternOfAHexDigitDecodesToThatDigit()
    {
        for (int i = 0; i < 16; i++)
        {
            Assert.Equal("0123456789ABCDEF"[i], Kim1Display.Decode(Shapes[i]));
        }

        Assert.Equal('?', Kim1Display.Decode(0x01));
        Assert.Equal(' ', Kim1Display.Decode(0x00));
    }

    [Fact]
    public void ADigitNotScannedForTwentyMillisecondsIsDark()
    {
        var display = new Kim1Display();
        long end = Scan(display, 0, "123456");
        Assert.Equal("123456", display.Read(end + Kim1Display.Persistence - 3100));
        Assert.Equal("      ", display.Read(end + Kim1Display.Persistence + 1));
    }

    [Fact]
    public void ASegmentLitForLessThanHalfTheSelectionDoesNotShow()
    {
        var display = new Kim1Display();
        display.Observe(0, 4, 0x7F);
        display.Observe(100, 4, 0x06);
        display.Observe(1000, 10, 0);
        Assert.Equal('1', display.Read(1000)[0]);
    }

    [Fact]
    public void DecoderOutputsOtherThanFourToNineLightNothing()
    {
        var display = new Kim1Display();
        foreach (int output in new[] { 0, 1, 2, 3, 10, 15 })
        {
            display.Observe(output * 1000, output, 0x7F);
        }

        display.Observe(20_000, 10, 0);
        Assert.Equal("      ", display.Read(20_000));
    }
}
