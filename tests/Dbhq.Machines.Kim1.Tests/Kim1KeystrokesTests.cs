using Xunit;

namespace Dbhq.Machines.Kim1.Tests;

/// <summary>
/// The keystroke player the browser page and the tests share: keys are named
/// as the manual names them, and each tap is held and then rested for a fixed
/// number of machine cycles, however fast the taps arrive.
/// </summary>
public sealed class Kim1KeystrokesTests
{
    private const long Hold = Kim1Keystrokes.HoldCycles;

    [Fact]
    public void KeysAreReadAsTheManualWritesThem()
    {
        Assert.Equal(["AD", "0", "0", "0", "2", "DA", "1", "8"], Kim1Keystrokes.Parse("[AD] 0002 [DA] 18"));
        Assert.Equal(["+", "GO", "PC", "ST", "RS"], Kim1Keystrokes.Parse("[+] [GO] [PC] [ST] [RS]"));
        Assert.Equal(["F", "A"], Kim1Keystrokes.Parse("fa"));

        // AD in brackets is the key; AD without them is two hex keys, the byte $AD.
        Assert.Equal(["A", "D"], Kim1Keystrokes.Parse("AD"));
        Assert.Throws<FormatException>(() => Kim1Keystrokes.Parse("[XY]"));
        Assert.Throws<FormatException>(() => Kim1Keystrokes.Parse("G"));
        Assert.Throws<ArgumentException>(() => new Kim1Keystrokes(new Kim1Session().Machine).Tap("SST"));
    }

    [Fact]
    public void EachTapIsHeldThenRestedBeforeTheNextKeyGoesDown()
    {
        var machine = new Kim1Session().Machine;
        var keys = new Kim1Keystrokes(machine);

        // Two taps at once, as a fast double click would queue them.
        keys.Tap("5");
        keys.Tap("5");
        Assert.Equal(2, keys.Pending);

        long start = machine.Cycles;
        keys.Run(1);
        Assert.True(machine.Keypad.IsPressed(Kim1Key.Key5));

        keys.Run(Hold - 100);
        Assert.True(machine.Keypad.IsPressed(Kim1Key.Key5), "released before its hold was up");

        keys.Run(200);
        Assert.False(machine.Keypad.IsPressed(Kim1Key.Key5), "still held after its hold");
        Assert.Equal(1, keys.Pending);

        // The second tap waits out the rest, so the monitor sees the key come up between the two.
        keys.Run(Hold - 400);
        Assert.False(machine.Keypad.IsPressed(Kim1Key.Key5), "the second tap went down before the rest was over");
        keys.Run(400);
        Assert.True(machine.Keypad.IsPressed(Kim1Key.Key5));

        keys.RunUntilIdle();
        Assert.False(machine.Keypad.IsPressed(Kim1Key.Key5));
        Assert.True(keys.Idle);
        Assert.InRange(machine.Cycles - start, 4 * Hold, 4 * Hold + 10);
    }

    [Fact]
    public void StHoldsTheStopLineForItsHoldAndRsResetsOnTheCycleItGoesDown()
    {
        var machine = new Kim1Session().Machine;
        var keys = new Kim1Keystrokes(machine);

        keys.Tap("ST");
        keys.Run(1);
        Assert.True(machine.StopKey);
        keys.Run(Hold);
        Assert.False(machine.StopKey);

        // Never reset, the board shows nothing; RS starts the monitor, which scans the digits.
        keys.RunUntilIdle();
        Assert.Equal("      ", machine.ReadDisplay());
        keys.Tap("RS");
        keys.RunUntilIdle();
        Assert.Equal("000000", machine.ReadDisplay());
    }

    /// <summary>
    /// The page can press a key on the keypad and type at the teletype in one
    /// go. The keypad goes first, and the teletype's key is held until the last
    /// tap has been up for its rest.
    /// </summary>
    [Fact]
    public void TeletypeKeysWaitForTheKeypadTapsQueuedBeforeThem()
    {
        var machine = new Kim1Session().Machine;
        machine.Mode = Kim1Mode.Teletype;
        var keys = new Kim1Keystrokes(machine);

        keys.Tap("RS");
        keys.TypeAtTeletype("\u007f");
        Assert.Equal(1, keys.HeldForTeletype);
        Assert.False(keys.Idle, "idle with a key still held for the teletype");

        keys.Run(2 * Hold - 100);
        Assert.Equal(1, keys.HeldForTeletype);
        Assert.Equal(0, machine.Teletype.Queued);
        Assert.False(machine.Teletype.Sending);

        keys.Run(200);
        Assert.Equal(0, keys.HeldForTeletype);
        Assert.True(machine.Teletype.Queued == 1 || machine.Teletype.Sending, "the key was not handed to the teletype");

        // And the monitor, which was reset by RS and had time to start, answers it.
        keys.RunUntilIdle();
        do
        {
            machine.Run(10_000);
        }
        while (!machine.Teletype.Idle(machine.Cycles));
        Assert.Contains("KIM", machine.ReadTeletype());
    }

    [Fact]
    public void TeletypeKeysWithNoTapsBeforeThemGoOnTheNextRun()
    {
        var machine = new Kim1Session().Machine;
        var keys = new Kim1Keystrokes(machine);

        keys.TypeAtTeletype("A");
        keys.Run(1);
        Assert.Equal(0, keys.HeldForTeletype);
        Assert.True(machine.Teletype.Queued == 1 || machine.Teletype.Sending);

        // A held key can be dropped, as a terminal swapped for another drops its own.
        keys.Tap("RS");
        keys.TypeAtTeletype("B");
        keys.DropHeldForTeletype();
        Assert.Equal(0, keys.HeldForTeletype);
    }

    /// <summary>
    /// Stopping a running program with ST and typing at the teletype in one go:
    /// the monitor takes a moment to start printing KIM, and a character sent
    /// into that moment is garbled, which the teletype's quiet-line rule cannot
    /// prevent because the answer has not begun. Held until ST is done, the
    /// characters arrive after the answer.
    /// </summary>
    [Fact]
    public void AStopKeyAndTypingQueuedTogetherAreNotGarbled()
    {
        var kim = new Kim1Session();
        kim.Machine.Mode = Kim1Mode.Teletype;
        kim.Keys("[RS]");
        kim.Type("[RUBOUT]");
        kim.Type("17FA[SPACE]00.1C.");
        kim.Type("0200[SPACE]AD.10.02.18.6D.11.02.8D.12.02.4C.0A.02.");
        kim.Type("0210[SPACE]27.15.");
        kim.Type("0200[SPACE]G");

        kim.Keystrokes.Tap("ST");
        kim.Keystrokes.TypeAtTeletype(Kim1Teletype.Parse("0212[SPACE]"));
        do
        {
            kim.Keystrokes.Run(10_000);
        }
        while (!kim.Keystrokes.Idle || !kim.Machine.Teletype.Idle(kim.Machine.Cycles));
        kim.Print();

        Assert.Equal(["KIM", "020A 4C 0212", "0212 3C"], kim.Paper.Last(3));
        Assert.Equal(0, kim.Machine.Teletype.FramingErrors);
    }
}
