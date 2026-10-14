using System.Text;
using System.Text.Json;
using Dbhq.Cpu6502.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// The KIM-1 with a teletype on its 20 mA loops, the original monitor ROM
/// driving it. Every character typed goes in as bits on PA7, timed in cycles,
/// and every character checked is decoded from the edges the monitor puts on
/// PB0: nothing here reads the monitor's RAM to learn what it printed.
/// </summary>
/// <remarks>
/// Some tests write what they measured to the test log, for the journal:
/// <c>dotnet test tests/Dbhq.Machines.Kim1.Tests -c Release --filter Kim1TeletypeTests --logger "console;verbosity=detailed"</c>.
/// </remarks>
public sealed class Kim1TeletypeTests(ITestOutputHelper output)
{
    private const string Pad = "\0\0\0\0\0\0";

    /// <summary>
    /// The User Manual, section 2.6, step 5: "Press the [RS] key on the KIM-1
    /// module then press the [RUB OUT] key on the TTY ... you should
    /// immediately observe a message being typed as follows: KIM".
    /// </summary>
    [Fact]
    public void WithTheSwitchAtTtyResetThenRubOutPrintsKimAndTheAddress()
    {
        var kim = new Kim1Session().LogOn();

        // The RUB OUT comes back first, echoed by the wiring. Then the
        // monitor's CR LF and six NULs, KIM, another CR LF and six NULs, and
        // the address in its pointer with the byte there, each followed by a
        // space. RAM starts as zeros, so that is 0000 and 00.
        Assert.Equal($"\u007F\r\n{Pad}KIM\r\n{Pad}0000 00 ", kim.Printed);
        Assert.Equal(["", "KIM", "0000 00"], kim.Paper.Lines);
        Assert.Equal(0, kim.Machine.Teletype.FramingErrors);

        // In teletype mode the monitor never scans the digits.
        Assert.Equal("       ", kim.Display);
    }

    /// <summary>
    /// RS turns every 6530 port pin into an input, and an input pin is pulled
    /// up, so PB0 holds the printer loop at a mark until the monitor sets the
    /// port up again: a reset prints nothing, and the line is never left at a
    /// space for the printer to take as a long start bit.
    /// </summary>
    [Fact]
    public void AfterRsThePullUpOnPB0HoldsThePrinterLoopAtAMark()
    {
        var kim = new Kim1Session();
        kim.Machine.Mode = Kim1Mode.Teletype;
        kim.Machine.PressReset();
        Assert.Equal(0, kim.Machine.Bus.U2.PortBDirection);
        Assert.True(kim.Machine.Teletype.OutputMark);
        kim.Machine.Run(100_000);
        Assert.Equal("", kim.Machine.ReadTeletype());
        Assert.Equal(0, kim.Machine.Teletype.FramingErrors);
    }

    /// <summary>
    /// The User Manual's section 2.6, step 6: the same cells as the keypad's
    /// first program, through the teletype, with what the manual says is
    /// printed after each line.
    /// </summary>
    [Fact]
    public void TheUserManualsTeletypeExampleRunsAsTheManualSays()
    {
        var kim = new Kim1Session().LogOn();
        (string Keys, string[] Prints)[] steps =
        [
            ("0002[SPACE]", ["0000 00 0002", "0002 00"]),
            ("18.", ["0002 00 18.", "0003 00"]),
            ("A5.", ["0003 00 A5.", "0004 00"]),

            // LF steps back a cell: "0003 A5". The LF's own echo feeds the
            // paper a line before the monitor's CR LF, so a blank line comes first.
            ("[LF]", ["0004 00", "", "0003 A5"]),

            // RUB OUT starts again: "KIM", then the cell.
            ("[RUBOUT]", ["0003 A5", "KIM", "0003 A5"]),
        ];

        foreach (var (keys, prints) in steps)
        {
            kim.Type(keys);
            Assert.Equal(prints, kim.Paper.Last(prints.Length));
        }

        Assert.Equal(0, kim.Machine.Teletype.FramingErrors);
    }

    /// <summary>
    /// The session the KIM-1's web page asks a visitor to type at its
    /// terminal, read from the same file the page is built from: the program
    /// the keypad's try-it enters, deposited through the teletype, run with G,
    /// stopped with ST and its result read back off the paper.
    /// </summary>
    [Fact]
    public void TheSessionOnTheMachinesWebPageRunsAsThePageSays()
    {
        using JsonDocument file = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "machines", "kim-1", "teletype.json")));
        JsonElement[] steps = [.. file.RootElement.GetProperty("steps").EnumerateArray()];
        Assert.NotEmpty(steps);

        var kim = new Kim1Session();
        kim.Machine.Mode = Kim1Mode.Teletype;
        string lastLine = "";
        foreach (JsonElement step in steps)
        {
            long before = kim.Machine.Cycles;
            string? keypad = step.TryGetProperty("keypad", out JsonElement k) ? k.GetString() : null;
            string? type = step.TryGetProperty("type", out JsonElement t) ? t.GetString() : null;
            string[] prints = [.. step.GetProperty("prints").EnumerateArray().Select(p => p.GetString()!)];
            if (keypad is not null)
            {
                kim.Keys(keypad);
            }

            if (type is not null)
            {
                kim.Type(type);
            }

            IReadOnlyList<string> shown = kim.Paper.Last(prints.Length);
            lastLine = prints[^1];
            output.WriteLine($"{keypad} {type}: {kim.Machine.Cycles - before:N0} cycles");
            Assert.True(Kim1Session.Matches(prints, shown), $"after {keypad} {type}: the page promises \"{string.Join(" / ", prints)}\", the paper shows \"{string.Join(" / ", shown)}\"");
        }

        output.WriteLine($"the whole session: {kim.Machine.Cycles:N0} cycles");
        Assert.Equal(0, kim.Machine.Teletype.FramingErrors);

        // The program really ran: the sum is in RAM where it stored it.
        Assert.Equal(0x3C, kim.Machine.Bus.Peek(0x0212));

        // Open the switch and the monitor hands back to the keypad, whose digits
        // show the cell the teletype last opened: the last line the page promises.
        // The site's browser check expects the same digits, worked out from the same file.
        kim.Machine.Mode = Kim1Mode.Keypad;
        kim.Machine.Run(100_000);
        Assert.True(Kim1Session.Matches(lastLine, kim.Display), $"with the switch open the digits show \"{kim.Display}\", not \"{lastLine}\"");
    }

    /// <summary>
    /// The monitor sends at the speed it measured from RUB OUT. Every bit it
    /// sends, read off the edges on PB0 while it prints KIM and the address,
    /// is within half a per cent of a 110 baud bit, 1,000,000 / 110 cycles.
    /// </summary>
    [Fact]
    public void TheMonitorSendsAt110BaudAfterTimingRubOutAt110Baud()
    {
        var kim = new Kim1Session();
        var line = new List<(long Cycle, bool Mark)>();
        var printed = new List<long>();
        kim.Machine.Teletype.OutputChanged += (cycle, mark) => line.Add((cycle, mark));
        kim.Machine.Teletype.Printed += (cycle, _) => printed.Add(cycle);
        kim.LogOn();

        // The first character printed is the echo of RUB OUT; the monitor's
        // own start after it. In each of its characters the time from the
        // start edge to the rising edge into the stop bit is nine bits,
        // because the eighth bit the monitor sends is always a space.
        double bit = 1_000_000.0 / 110;
        var widths = new List<double>();
        long? start = null;
        foreach (var (cycle, mark) in line.Where(e => e.Cycle > printed[0]))
        {
            if (!mark && start is null)
            {
                start = cycle;
            }
            else if (mark && start is not null && cycle - start.Value > 8.5 * bit)
            {
                widths.Add((cycle - start.Value) / 9.0);
                start = null;
            }
        }

        output.WriteLine($"{widths.Count} characters; a bit is {widths.Min():F1} to {widths.Max():F1} cycles, {widths.Average():F1} on average, against {bit:F1}");
        Assert.Equal(printed.Count - 1, widths.Count);
        foreach (double width in widths)
        {
            Assert.InRange(width, bit * 0.995, bit * 1.005);
        }
    }

    /// <summary>
    /// The monitor measures the speed, so any terminal speed works: it logs
    /// on, prints and reads at each, and the printer decodes every character.
    /// </summary>
    [Theory]
    [InlineData(110)]
    [InlineData(300)]
    [InlineData(1200)]
    [InlineData(2400)]
    public void TheMonitorAdjustsToTheSpeedOfTheTerminal(int baud)
    {
        var kim = new Kim1Session();
        kim.Machine.Teletype = new Kim1Teletype(baud);
        kim.LogOn();
        output.WriteLine($"{baud} baud: logged on after {kim.Machine.Cycles:N0} cycles");
        kim.Type("0002[SPACE]18.[LF]");
        Assert.Equal(["0002 00 18.", "0003 00", "", "0002 18"], kim.Paper.Last(4));
        Assert.Equal(0, kim.Machine.Teletype.FramingErrors);
    }

    /// <summary>
    /// A whole command queued at once, while the monitor is still printing
    /// its answer to the first key: the teletype holds each key until both
    /// lines have been quiet, so none is lost. Sent back to back instead, as a
    /// tape reader would, the keys after the first space go while the monitor
    /// is printing and is not listening, and are lost.
    /// </summary>
    [Fact]
    public void KeysQueuedWhileTheMonitorIsPrintingAreNotLost()
    {
        var paced = new Kim1Session().LogOn();
        paced.Machine.Teletype.Send(Kim1Teletype.Parse("0002[SPACE]18.A5.0002[SPACE]"));
        paced.RunTeletype();
        Assert.Equal(["0000 00 0002", "0002 00 18.", "0003 00 A5.", "0004 00 0002", "0002 18"], paced.Paper.Last(5));

        var unpaced = new Kim1Session();
        unpaced.Machine.Teletype = new Kim1Teletype(quietBits: 0);
        unpaced.LogOn();
        unpaced.Machine.Teletype.Send(Kim1Teletype.Parse("0002[SPACE]18.A5.0002[SPACE]"));
        unpaced.RunTeletype();
        Assert.Equal(0x00, unpaced.Machine.Bus.Peek(0x0002));
    }

    /// <summary>
    /// Q punches the cells from the open address to the end address at
    /// $17F7 and $17F8 as paper tape records, in the format of the User
    /// Manual's Appendix F. The record is worked out here from the bytes, and
    /// the punched text is then read back into a second KIM-1 with L.
    /// </summary>
    [Fact]
    public void QPunchesAPaperTapeAndLReadsItIntoAnotherKim1()
    {
        var kim = new Kim1Session().LogOn();
        kim.Type("0200[SPACE]A9.05.85.40.00.");
        kim.Type("17F7[SPACE]01.02.");
        kim.Type("0200[SPACE]");
        int before = kim.Printed.Length;
        kim.Type("Q");
        string punched = kim.Printed[before..];

        // One record of 24 bytes from $0200 (the dump always writes whole
        // records), its check-sum the sum of every byte in it after the ";",
        // then the last record: no bytes, the number of records, its sum.
        byte[] data = [0xA9, 0x05, 0x85, 0x40, 0x00, .. new byte[19]];
        int sum = 0x18 + 0x02 + 0x00 + data.Sum(b => b);
        var record = new StringBuilder(";180200");
        foreach (byte b in data)
        {
            record.Append(b.ToString("X2"));
        }

        record.Append(sum.ToString("X4"));
        Assert.Equal($"Q\r\n{Pad}{record}\r\n{Pad};0000010001", punched);

        var other = new Kim1Session().LogOn();
        other.Type("L");
        other.Machine.Teletype.Send(punched[1..]);
        other.RunTeletype();

        // A good load ends with XOFF, which stops the reader, and KIM.
        Assert.Contains($"\u0013 KIM\r\n{Pad}\r\n{Pad}KIM", other.Printed);
        Assert.DoesNotContain("ERR", other.Printed);
        other.Type("0200[SPACE][CR][CR][CR][CR]");
        Assert.Equal(["0200 A9", "0201 05", "0202 85", "0203 40", "0204 00"], other.Paper.Last(5).Select(l => l[..7]));
    }

    /// <summary>
    /// A record of one byte, $EA at $0300, whose check-sum should be $01 +
    /// $03 + $00 + $EA = $00EE. With $00EE the byte loads; with $00EF the
    /// monitor prints ERR, and goes back to KIM.
    /// </summary>
    [Fact]
    public void LPrintsErrForARecordWhoseCheckSumIsWrong()
    {
        var good = new Kim1Session().LogOn();
        good.Type("L");
        good.Machine.Teletype.Send(";010300EA00EE\r\n;0000010001");
        good.RunTeletype();
        Assert.DoesNotContain("ERR", good.Printed);
        good.Type("0300[SPACE]");
        Assert.Equal("0300 EA", good.Paper.Lines[^1]);

        var bad = new Kim1Session().LogOn();
        bad.Type("L");
        bad.Machine.Teletype.Send(";010300EA00EF");
        bad.RunTeletype();
        Assert.Contains("\u0013 ERR\u0013 KIM", bad.Printed);
    }

    /// <summary>
    /// With the switch at KB, which is where it starts, the monitor runs the
    /// keypad and ignores the teletype. The wiring still echoes what is typed,
    /// as it would on a board with a teletype left plugged in.
    /// </summary>
    [Fact]
    public void WithTheSwitchAtKbTheMonitorIgnoresTheTeletype()
    {
        var kim = new Kim1Session().Boot();
        Assert.Equal(Kim1Mode.Keypad, kim.Machine.Mode);
        kim.Type("[RUBOUT]0200[SPACE]");
        Assert.Equal("\u007F0200 ", kim.Printed);
        Assert.Equal("0000 00", kim.Display);

        // And the keypad works as it always has.
        kim.Keys("[AD] 0200");
        Assert.Equal("0200 00", kim.Display);
    }

    /// <summary>
    /// The User Manual, section 3: the teletype loop is left "in response to
    /// ... a change in the mode from TTY to Keyboard/Display". Opening the
    /// switch while the monitor waits for a key brings back the digits.
    /// </summary>
    [Fact]
    public void OpeningTheSwitchHandsTheMonitorBackToTheKeypad()
    {
        var kim = new Kim1Session().LogOn();
        kim.Type("0200[SPACE]");
        Assert.Equal("       ", kim.Display);
        kim.Machine.Mode = Kim1Mode.Keypad;
        kim.Machine.Run(100_000);
        Assert.Equal("0200 00", kim.Display);
    }

    /// <summary>
    /// Swapping the terminal for one at another speed does not lose what the
    /// old one had printed and the page had not yet read.
    /// </summary>
    [Fact]
    public void ReplacingTheTerminalKeepsWhatItHadPrinted()
    {
        var kim = new Kim1Session();
        kim.Machine.Mode = Kim1Mode.Teletype;
        kim.Keys("[RS]");
        kim.Machine.Teletype.Send(Kim1Teletype.Parse("[RUBOUT]"));
        kim.Machine.Run(5_000_000);

        // Nothing has read the paper yet. A terminal at another speed goes on.
        kim.Machine.Teletype = new Kim1Teletype(300);
        Assert.Equal(300, kim.Machine.Teletype.Baud);
        Assert.Contains("KIM", kim.Machine.ReadTeletype());
        Assert.Equal("", kim.Machine.ReadTeletype());
    }

    [Theory]
    [InlineData(Kim1Teletype.MinBaud - 1)]
    [InlineData(Kim1Teletype.MaxBaud + 1)]
    public void ASpeedOutsideTheRangeIsRefusedWhereverItIsChecked(int baud)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Kim1Teletype(baud));
        Assert.Throws<ArgumentOutOfRangeException>(() => Kim1Teletype.CheckBaud(baud));
        Kim1Teletype.CheckBaud(Kim1Teletype.MinBaud);
        Kim1Teletype.CheckBaud(Kim1Teletype.MaxBaud);
    }

    /// <summary>
    /// The page reads the paper every frame, between cycles. Doing so must not
    /// start a key typed since the last cycle on that cycle, which has run.
    /// </summary>
    [Fact]
    public void ReadingThePaperDoesNotStartAKeyOnACycleThatHasAlreadyRun()
    {
        var kim = new Kim1Session().LogOn();
        kim.Machine.Run(1_000_000);

        kim.Machine.Teletype.Send('A');
        kim.Machine.ReadTeletype();
        Assert.False(kim.Machine.Teletype.Sending, "reading the paper started the key");
        Assert.True(kim.Machine.Teletype.KeyboardMark);

        kim.Machine.Run(1);
        Assert.True(kim.Machine.Teletype.Sending);
        Assert.False(kim.Machine.Teletype.KeyboardMark, "the key did not start on the cycle after it was typed");
    }
}
