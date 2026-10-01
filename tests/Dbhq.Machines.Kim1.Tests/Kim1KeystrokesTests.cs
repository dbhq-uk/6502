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
}
