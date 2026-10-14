using System.Text.Json;
using Dbhq.Cpu6502.TestSupport;
using Dbhq.Machines.Electron.Tape;
using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The test that makes the Electron count as running: the machine boots OS 1.00 and BASIC to the
/// prompt, the program its page shows is typed in on the keyboard matrix and run, and what it
/// prints is read back off the picture the ULA drew. A second pass types the same program through
/// <see cref="ElectronKeyPresses"/>, the queue the page's keys go through, with every key reported
/// down and up at once as a browser can, so the page's way in is the one tested too. A third pass
/// saves the program to tape with the machine's own <c>SAVE</c>, writes the tape as a UEF, reads it
/// back and loads it on a fresh machine, and runs it there.
/// </summary>
/// <remarks>
/// <para>
/// The program is <c>machines/electron/try-it.json</c>, the file the page renders, so the page and
/// this test cannot disagree. What the screen must show is in the same file, worked out from
/// BASIC's own rules (a number after a semicolon is printed with no padding), never copied from
/// what the machine printed.
/// </para>
/// <para>
/// The third pass splits the lines by BASIC's rule: a line that starts with a line number goes into
/// the program, and any other line is a command run at once. The program lines are typed and saved;
/// the commands are typed on the machine that loaded them, after <c>CLS</c>, so the screen starts
/// from the top row.
/// </para>
/// <para>
/// The registry's <c>acceptance</c> field names this class. Rename it there too.
/// </para>
/// </remarks>
public sealed class ElectronAcceptanceTests
{
    /// <summary>The boot's prompt is on row 5 (<c>ula.md</c> s10b).</summary>
    private const int Prompt = 5;

    private static (string[] Lines, string[] Shows) TryIt()
    {
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "machines", "electron", "try-it.json")));
        string[] Strings(string name) => file.RootElement.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToArray();
        return (Strings("lines"), Strings("shows"));
    }

    [Fact]
    public void TheProgramOnThePageRunsAndPrintsWhatThePageSays()
    {
        var (lines, shows) = TryIt();
        var s = new ElectronSession().Boot().RunUntilPrompt();
        foreach (string line in lines)
        {
            s.Type(line + "\r");
        }

        AssertScreen(s.RunUntilPrompt().ScreenText(), Prompt, lines, shows);
    }

    [Fact]
    public void TheSameProgramTypedThroughThePagesKeyQueueRunsTheSame()
    {
        var (lines, shows) = TryIt();
        var s = new ElectronSession().Boot().RunUntilPrompt();
        var keys = new ElectronKeyPresses(s.Machine);
        foreach (string line in lines)
        {
            QueueAtOnce(keys, line + "\r");
        }

        RunUntilPlayed(keys);
        AssertScreen(s.RunUntilPrompt().ScreenText(), Prompt, lines, shows);
    }

    [Fact]
    public void TheProgramSavedToTapeLoadsOnAFreshMachineAndRunsTheSame()
    {
        var (lines, shows) = TryIt();
        string[] program = [.. lines.Where(IsProgramLine)];
        string[] commands = [.. lines.Where(l => !IsProgramLine(l))];
        Assert.NotEmpty(program);
        Assert.NotEmpty(commands);

        // Typed and saved with the OS's own SAVE, onto a blank tape (tape.md s7: RECORD then RETURN).
        var saver = new ElectronSession().Boot().RunUntilPrompt();
        foreach (string line in program)
        {
            saver.Type(line + "\r");
        }

        saver.RunUntilPrompt();
        saver.Machine.StartRecording();
        saver.Type("SAVE \"HELLO\"\r");
        TapeRuns.RunUntil(saver, rows => rows.Any(r => r.TrimEnd() == "RECORD then RETURN"), 4_000_000);
        saver.Type("\r");
        TapeRoundTrip.RunUntilMotor(saver, on: false, 60_000_000);
        saver.RunUntilPrompt();
        byte[] uef = UefWriter.Write(saver.Machine.EjectTape(), "dbhq-uk/6502 Electron acceptance test");

        // A fresh machine: nothing carried over but the file.
        var loader = new ElectronSession().Boot().RunUntilPrompt();
        loader.Machine.InsertTape(UefReader.Read(uef));
        loader.Type("LOAD \"HELLO\"\r");
        TapeRoundTrip.RunUntilMotor(loader, on: true, 4_000_000);
        TapeRoundTrip.RunUntilMotor(loader, on: false, 20_000_000);
        loader.RunUntilPrompt();
        Assert.Equal(0, loader.Machine.LostBytes);

        loader.Type("CLS\r").RunUntilPrompt();
        foreach (string command in commands)
        {
            loader.Type(command + "\r");
        }

        AssertScreen(loader.RunUntilPrompt().ScreenText(), 0, commands, shows);
    }

    /// <summary>What the site's browser check types through the page: <c>PRINT 6*7</c>, then RETURN.</summary>
    [Fact]
    public void PrintSixTimesSevenThroughThePagesKeyQueueShowsFortyTwo()
    {
        var s = new ElectronSession().Boot().RunUntilPrompt();
        var keys = new ElectronKeyPresses(s.Machine);
        QueueAtOnce(keys, "PRINT 6*7\r");
        RunUntilPlayed(keys);
        string[] screen = s.RunUntilPrompt().ScreenText();

        Assert.Equal(">PRINT 6*7", screen[Prompt].TrimEnd());
        Assert.Equal("42", screen[Prompt + 1].Trim());
        Assert.Equal(">", screen[Prompt + 2].TrimEnd());
    }

    /// <summary>BASIC's rule: a line that starts with a line number is stored, not run.</summary>
    private static bool IsProgramLine(string line) => line.Length > 0 && char.IsAsciiDigit(line[0]);

    /// <summary>
    /// From row <paramref name="from"/>, each line echoed after the prompt on its own row, then the
    /// lines the page says the program prints, then the prompt again.
    /// </summary>
    private static void AssertScreen(string[] screen, int from, string[] lines, string[] shows)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            Assert.Equal(">" + lines[i], screen[from + i].TrimEnd());
        }

        int first = from + lines.Length;
        for (int i = 0; i < shows.Length; i++)
        {
            Assert.Equal(shows[i], screen[first + i].TrimEnd());
        }

        Assert.Equal(">", screen[first + shows.Length].TrimEnd());
    }

    /// <summary>
    /// Every key of <paramref name="text"/> reported down and then up with no time between, Shift
    /// around the keys that need it, as a browser reports a key a test presses.
    /// </summary>
    private static void QueueAtOnce(ElectronKeyPresses keys, string text)
    {
        foreach (char c in text)
        {
            (ElectronKey key, bool shift) = ElectronSession.Keys[c];
            if (shift)
            {
                keys.Down(ElectronKey.Shift);
            }

            keys.Down(key);
            keys.Up(key);
            if (shift)
            {
                keys.Up(ElectronKey.Shift);
            }
        }
    }

    private static void RunUntilPlayed(ElectronKeyPresses keys)
    {
        while (keys.Pending > 0)
        {
            keys.Run(10_000);
        }

        // The last key's hold, and a rest after it.
        keys.Run(ElectronKeyPresses.HoldCycles + ElectronKeyPresses.RestCycles);
    }
}
