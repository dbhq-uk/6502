using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The page's key queue on its own: a key that a browser reports down and up in the same frame
/// is still held long enough for the OS's 100 Hz scan to see it, keys are played in the order
/// they came, and only the same key waits to be pressed again. The times are the queue's own
/// constants, 40 ms each; the margins below allow for an instruction boundary, at most a few
/// cycles, never for a different rule.
/// </summary>
public class BbcKeyPressesTests
{
    // An instruction is at most seven cycles, plus a slow device's stretch: well under this.
    private const long Slack = 32;

    private static (BbcMachine Machine, BbcKeyPresses Keys) Booted()
    {
        BbcMachine machine = new BbcSession().Boot().Machine;
        return (machine, new BbcKeyPresses(machine));
    }

    [Fact]
    public void AKeyReportedDownAndUpAtOnceIsHeldForTheHoldThenReleased()
    {
        var (machine, keys) = Booted();
        keys.Down(BbcKey.A);
        keys.Up(BbcKey.A);
        long start = machine.Cycles;

        keys.Run(1);
        Assert.True(machine.Keyboard.IsDown(BbcKey.A), "the key did not go down at once");

        keys.Run(BbcKeyPresses.HoldCycles - (machine.Cycles - start) - Slack);
        Assert.True(machine.Keyboard.IsDown(BbcKey.A), "the key came up before its hold was over");

        keys.Run(2 * Slack);
        Assert.False(machine.Keyboard.IsDown(BbcKey.A), "the key was still down after its hold");
        Assert.Equal(0, keys.Pending);
    }

    [Fact]
    public void TheSameKeyPressedTwiceRestsUpBetweenThePresses()
    {
        var (machine, keys) = Booted();
        keys.Down(BbcKey.L);
        keys.Up(BbcKey.L);
        keys.Down(BbcKey.L);
        keys.Up(BbcKey.L);
        long start = machine.Cycles;

        keys.Run(BbcKeyPresses.HoldCycles + Slack);
        Assert.False(machine.Keyboard.IsDown(BbcKey.L), "the first press was not released after its hold");

        keys.Run(BbcKeyPresses.HoldCycles + BbcKeyPresses.RestCycles - (machine.Cycles - start) - Slack);
        Assert.False(machine.Keyboard.IsDown(BbcKey.L), "the second press came before the rest was over");

        keys.Run(2 * Slack);
        Assert.True(machine.Keyboard.IsDown(BbcKey.L), "the second press did not come after the rest");
    }

    [Fact]
    public void ADifferentKeyGoesDownAsSoonAsTheOneBeforeComesUp()
    {
        var (machine, keys) = Booted();
        keys.Down(BbcKey.P);
        keys.Up(BbcKey.P);
        keys.Down(BbcKey.R);
        keys.Up(BbcKey.R);

        keys.Run(BbcKeyPresses.HoldCycles - Slack);
        Assert.True(machine.Keyboard.IsDown(BbcKey.P));
        Assert.False(machine.Keyboard.IsDown(BbcKey.R), "R went down before P came up: the order was not kept");

        keys.Run(2 * Slack);
        Assert.False(machine.Keyboard.IsDown(BbcKey.P));
        Assert.True(machine.Keyboard.IsDown(BbcKey.R), "R waited for a rest that only the same key needs");
    }

    [Fact]
    public void ShiftHeldAroundAKeyIsDownForTheWholeOfThatKeysHold()
    {
        var (machine, keys) = Booted();
        keys.Down(BbcKey.Shift);
        keys.Down(BbcKey.Colon);
        keys.Up(BbcKey.Colon);
        keys.Up(BbcKey.Shift);

        // Sampled across the colon's hold: SHIFT is down every time the colon is.
        long start = machine.Cycles;
        while (machine.Cycles - start < BbcKeyPresses.HoldCycles + (4 * Slack))
        {
            keys.Run(1_000);
            if (machine.Keyboard.IsDown(BbcKey.Colon))
            {
                Assert.True(machine.Keyboard.IsDown(BbcKey.Shift), "SHIFT came up while the colon was still down");
            }
        }

        Assert.False(machine.Keyboard.IsDown(BbcKey.Colon));
        Assert.False(machine.Keyboard.IsDown(BbcKey.Shift));
    }

    [Fact]
    public void AKeyHeldLongerThanTheHoldStaysDownUntilItComesUp()
    {
        var (machine, keys) = Booted();
        keys.Down(BbcKey.Space);
        keys.Run(10 * BbcKeyPresses.HoldCycles);
        Assert.True(machine.Keyboard.IsDown(BbcKey.Space));

        keys.Up(BbcKey.Space);
        keys.Run(1);
        Assert.False(machine.Keyboard.IsDown(BbcKey.Space), "a key held past its hold did not come up when asked");
    }

    [Fact]
    public void AValueThatIsNotAKeyIsRefused()
    {
        var (_, keys) = Booted();
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.Down((BbcKey)0x0A));
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.Up((BbcKey)0x7A));
    }
}
