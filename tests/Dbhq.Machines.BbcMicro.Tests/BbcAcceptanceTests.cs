using System.Text.Json;
using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The test that makes the BBC Micro count as running: the machine boots MOS 1.20, BASIC and
/// DFS to the prompt, the program its page shows is typed in on the keyboard matrix and run, and
/// what it prints is read back off the picture the video chips drew. A second pass types the
/// same program through <see cref="BbcKeyPresses"/>, the queue the page's keys go through, with
/// every key reported down and up at once as a browser can, so the page's way in is the one
/// tested too.
/// </summary>
/// <remarks>
/// <para>
/// The program is <c>machines/bbc-micro/try-it.json</c>, the file the page renders, so the page
/// and this test cannot disagree. What the screen must show is in the same file, worked out from
/// BASIC's own rules (a number after a semicolon is printed with no padding), never copied from
/// what the machine printed.
/// </para>
/// <para>
/// The registry's <c>acceptance</c> field names this class. Rename it there too.
/// </para>
/// </remarks>
public sealed class BbcAcceptanceTests
{
    /// <summary>Time for the program to run and for both fields of the picture to show it: ten fields.</summary>
    private const long Settle = 400_000;

    private static readonly int Prompt = BootScreen.Rows.Count - 1;

    private static (string[] Lines, string[] Shows) TryIt()
    {
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "machines", "bbc-micro", "try-it.json")));
        string[] Strings(string name) => file.RootElement.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToArray();
        return (Strings("lines"), Strings("shows"));
    }

    [Fact]
    public void TheProgramOnThePageRunsAndPrintsWhatThePageSays()
    {
        var (lines, shows) = TryIt();
        var bbc = new BbcSession().Boot();
        foreach (string line in lines)
        {
            bbc.Type(line + "\r");
        }

        AssertScreen(bbc.RunFor(Settle).ScreenText(), lines, shows);
    }

    [Fact]
    public void TheSameProgramTypedThroughThePagesKeyQueueRunsTheSame()
    {
        var (lines, shows) = TryIt();
        var bbc = new BbcSession().Boot();
        var keys = new BbcKeyPresses(bbc.Machine);
        foreach (string line in lines)
        {
            QueueAtOnce(keys, line + "\r");
        }

        RunUntilPlayed(keys);
        AssertScreen(bbc.RunFor(Settle).ScreenText(), lines, shows);
    }

    /// <summary>What the site's browser check types through the page: <c>PRINT 6*7</c>, then RETURN.</summary>
    [Fact]
    public void PrintSixTimesSevenThroughThePagesKeyQueueShowsFortyTwo()
    {
        var bbc = new BbcSession().Boot();
        var keys = new BbcKeyPresses(bbc.Machine);
        QueueAtOnce(keys, "PRINT 6*7\r");
        RunUntilPlayed(keys);
        string[] screen = bbc.RunFor(Settle).ScreenText();

        Assert.Equal(">PRINT 6*7", screen[Prompt].TrimEnd());
        Assert.Equal("42", screen[Prompt + 1].Trim());
        Assert.Equal(BootScreen.Prompt, screen[Prompt + 2].TrimEnd());
    }

    /// <summary>
    /// Each line echoed after the prompt on its own row, from the boot's prompt row on, then the
    /// lines the page says the program prints, then the prompt again.
    /// </summary>
    private static void AssertScreen(string[] screen, string[] lines, string[] shows)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            Assert.Equal(BootScreen.Prompt + lines[i], screen[Prompt + i].TrimEnd());
        }

        int first = Prompt + lines.Length;
        for (int i = 0; i < shows.Length; i++)
        {
            Assert.Equal(shows[i], screen[first + i].TrimEnd());
        }

        Assert.Equal(BootScreen.Prompt, screen[first + shows.Length].TrimEnd());
    }

    /// <summary>
    /// Every key of <paramref name="text"/> reported down and then up with no time between, SHIFT
    /// around the keys that need it, as a browser reports a key a test presses.
    /// </summary>
    private static void QueueAtOnce(BbcKeyPresses keys, string text)
    {
        foreach (char c in text)
        {
            (BbcKey key, bool shift) = BbcSession.Keys[c];
            if (shift)
            {
                keys.Down(BbcKey.Shift);
            }

            keys.Down(key);
            keys.Up(key);
            if (shift)
            {
                keys.Up(BbcKey.Shift);
            }
        }
    }

    private static void RunUntilPlayed(BbcKeyPresses keys)
    {
        while (keys.Pending > 0)
        {
            keys.Run(10_000);
        }

        // The last key's hold, and a rest after it.
        keys.Run(BbcKeyPresses.HoldCycles + BbcKeyPresses.RestCycles);
    }
}
