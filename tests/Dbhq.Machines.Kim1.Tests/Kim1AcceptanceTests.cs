using Xunit;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// The test that makes the KIM-1 count as implemented: the original monitor
/// ROM boots, and a program is entered on the keypad, run, stopped and read
/// back off the six digits. Every key goes through the key matrix and the
/// monitor's own keypad scan, and every digit is read from the segments the
/// monitor drives, so a fault in the 6530s, the decoding, the keypad or the
/// display shows up here.
/// </summary>
/// <remarks>
/// The registry's <c>acceptance</c> field names this class. Rename it there too.
/// </remarks>
public sealed class Kim1AcceptanceTests
{
    [Fact]
    public void BeforeResetTheDigitsAreDarkAndAfterItTheMonitorShowsAnAddressAndItsByte()
    {
        var kim = new Kim1Session();
        kim.Machine.Run(100_000);
        Assert.Equal("       ", kim.Display);

        kim.Boot();

        // RAM starts as zeros, so the address the monitor shows, POINTH and
        // POINTL at $FB and $FA, is 0000, and the byte there is 00.
        Assert.Equal("0000 00", kim.Display);
        Assert.InRange(kim.Machine.Cpu.PC, 0x1C00, 0x1FFF);

        // RST leaves the monitor in address mode: a digit goes into the address.
        kim.Keys("1");
        Assert.Equal("0001 00", kim.Display);
    }

    /// <summary>
    /// KIM-1 User Manual, section 2.4, "Lets try a simple program": add two
    /// numbers. Each step's keys and the display the manual says to expect.
    /// </summary>
    [Fact]
    public void TheUserManualsFirstProgramRunsStepByStepAsTheManualSays()
    {
        var kim = new Kim1Session().Boot();
        (string Keys, string Display)[] steps =
        [
            ("[AD]", "xxxx xx"),
            ("0002", "0002 xx"),
            ("[DA]", "0002 xx"),
            ("18", "0002 18"),
            ("[+] A5", "0003 A5"),
            ("[+] 00", "0004 00"),
            ("[+] 65", "0005 65"),
            ("[+] 01", "0006 01"),
            ("[+] 85", "0007 85"),
            ("[+] FA", "0008 FA"),
            ("[+] A9", "0009 A9"),
            ("[+] 00", "000A 00"),
            ("[+] 85", "000B 85"),
            ("[+] FB", "000C FB"),
            ("[+] 4C", "000D 4C"),
            ("[+] 4F", "000E 4F"),
            ("[+] 1C", "000F 1C"),
            ("[AD]", "000F 1C"),
            ("00F1", "00F1 xx"),
            ("[DA] 00", "00F1 00"),
            ("[AD]", "00F1 00"),
            ("0000", "0000 xx"),

            // Step 21 is DA, 0, 2 in the scanned manual. A text transcription
            // of it drops the 0 and the 2.
            ("[DA] 02", "0000 02"),
            ("[+] 03", "0001 03"),
            ("[+]", "0002 18"),
        ];

        foreach (var (keys, display) in steps)
        {
            kim.Keys(keys);
            Assert.True(Kim1Session.Matches(display, kim.Display), $"after {keys}: the manual shows {display}, the display shows {kim.Display}");
        }

        // Step 23: GO. "The result, 05, appears in the right two digits of
        // the address display." The program jumps back into the monitor with
        // the sum in POINTL, so the address shown is 0005, and the byte at
        // 0005 is the program's own 65 (ADC).
        kim.Keys("[GO]");
        Assert.Equal("0005 65", kim.Display);
    }

    /// <summary>
    /// A program that stores its result and loops, entered on the keypad, run
    /// with GO, stopped with ST (after setting the NMI vector the way the
    /// manual's section 4.6 says to), and its result read back.
    /// </summary>
    [Fact]
    public void AProgramIsDepositedRunStoppedAndItsResultReadBackOffTheDigits()
    {
        var kim = new Kim1Session().Boot();

        // ST works only once the NMI vector at $17FA holds $1C00.
        kim.Keys("[AD] 17FA [DA] 00 [+] 1C");
        Assert.Equal("17FB 1C", kim.Display);

        // $0200: LDA $0210; CLC; ADC $0211; STA $0212; JMP $020A.
        kim.Keys("[AD] 0200 [DA] AD [+] 10 [+] 02 [+] 18 [+] 6D [+] 11 [+] 02 [+] 8D [+] 12 [+] 02 [+] 4C [+] 0A [+] 02");
        Assert.Equal("020C 02", kim.Display);
        kim.Keys("[AD] 0210 [DA] 27 [+] 15");
        Assert.Equal("0211 15", kim.Display);

        kim.Keys("[AD] 0200 [GO]");

        // Running, the program never scans the display, so it goes dark.
        Assert.Equal("       ", kim.Display);
        Assert.Equal(0x3C, kim.Machine.Bus.Peek(0x0212));

        // ST: the monitor saves the registers and shows where it stopped,
        // which is the loop's JMP.
        kim.Stop();
        Assert.Equal("020A 4C", kim.Display);

        // The result, read off the digits the way a person would.
        kim.Keys("[AD] 0212");
        Assert.Equal("0212 3C", kim.Display);

        // The accumulator the monitor saved at $00F3 holds the sum too.
        kim.Keys("[AD] 00F3");
        Assert.Equal("00F3 3C", kim.Display);

        // PC recalls where the program stopped.
        kim.Keys("[PC]");
        Assert.Equal("020A 4C", kim.Display);
    }

    /// <summary>
    /// The SST switch: "The display will show the address and data for the
    /// next instruction to be executed" after each GO (manual, section 4.7).
    /// </summary>
    [Fact]
    public void SingleStepStopsAfterEveryInstructionAndShowsTheNextOne()
    {
        var kim = new Kim1Session().Boot();
        kim.Keys("[AD] 17FA [DA] 00 [+] 1C");
        kim.Keys("[AD] 0200 [DA] AD [+] 10 [+] 02 [+] 18 [+] 6D [+] 11 [+] 02 [+] 8D [+] 12 [+] 02 [+] 4C [+] 0A [+] 02");
        kim.Keys("[AD] 0210 [DA] 27 [+] 15 [AD] 0200");

        kim.Machine.SingleStep = true;
        string[] expected = ["0203 18", "0204 6D", "0207 8D", "020A 4C", "020A 4C"];
        foreach (string display in expected)
        {
            kim.Keys("[GO]");
            Assert.Equal(display, kim.Display);
        }

        // Stepping one instruction at a time reached the store.
        kim.Keys("[AD] 0212");
        Assert.Equal("0212 3C", kim.Display);
    }

    [Fact]
    public void TheDigitsShowWhatTheMonitorScansAndGoDarkWhenNothingScansThem()
    {
        var kim = new Kim1Session().Boot();
        kim.Keys("[AD] 0300 [DA] 4C [+] 00 [+] 03 [AD] 0300");
        Assert.Equal("0300 4C", kim.Display);

        // JMP $0300 runs forever and never scans. The address the monitor
        // showed is still in RAM at POINTH and POINTL, but nothing lights a
        // segment unless the scan drives it, so the digits go dark.
        kim.Keys("[GO]");
        Assert.Equal("       ", kim.Display);
        Assert.Equal(0x03, kim.Machine.Bus.Peek(0x00FB));
        Assert.Equal(0x00, kim.Machine.Bus.Peek(0x00FA));
    }
}
