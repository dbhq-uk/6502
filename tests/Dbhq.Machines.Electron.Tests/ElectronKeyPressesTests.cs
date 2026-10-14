using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The page's key queue on its own, the BBC Micro's tests of its own queue with the Electron's
/// keys: a key that a browser reports down and up in the same frame is still held long enough for
/// the OS's scan to see it, keys are played in the order they came, and only the same key waits to
/// be pressed again. The times are the queue's own constants, 40 ms each; the margins below allow
/// for an instruction boundary, never for a different rule.
/// </summary>
public class ElectronKeyPressesTests
{
    // The machine is at the prompt in mode 6, where nothing waits for the display (ula.md s4), so
    // an access is at most three cycles (s3) and an instruction or an interrupt's entry at most
    // seven accesses: well under this.
    private const long Slack = 32;

    private static (ElectronMachine Machine, ElectronKeyPresses Keys) Booted()
    {
        ElectronMachine machine = new ElectronSession().Boot().Machine;
        return (machine, new ElectronKeyPresses(machine));
    }

    [Fact]
    public void AKeyReportedDownAndUpAtOnceIsHeldForTheHoldThenReleased()
    {
        var (machine, keys) = Booted();
        keys.Down(ElectronKey.A);
        keys.Up(ElectronKey.A);
        long start = machine.Cycles;

        keys.Run(1);
        Assert.True(machine.Keyboard.IsDown(ElectronKey.A), "the key did not go down at once");

        keys.Run(ElectronKeyPresses.HoldCycles - (machine.Cycles - start) - Slack);
        Assert.True(machine.Keyboard.IsDown(ElectronKey.A), "the key came up before its hold was over");

        keys.Run(2 * Slack);
        Assert.False(machine.Keyboard.IsDown(ElectronKey.A), "the key was still down after its hold");
        Assert.Equal(0, keys.Pending);
    }

    [Fact]
    public void TheSameKeyPressedTwiceRestsUpBetweenThePresses()
    {
        var (machine, keys) = Booted();
        keys.Down(ElectronKey.L);
        keys.Up(ElectronKey.L);
        keys.Down(ElectronKey.L);
        keys.Up(ElectronKey.L);
        long start = machine.Cycles;

        keys.Run(ElectronKeyPresses.HoldCycles + Slack);
        Assert.False(machine.Keyboard.IsDown(ElectronKey.L), "the first press was not released after its hold");

        keys.Run(ElectronKeyPresses.HoldCycles + ElectronKeyPresses.RestCycles - (machine.Cycles - start) - Slack);
        Assert.False(machine.Keyboard.IsDown(ElectronKey.L), "the second press came before the rest was over");

        keys.Run(2 * Slack);
        Assert.True(machine.Keyboard.IsDown(ElectronKey.L), "the second press did not come after the rest");
    }

    [Fact]
    public void ADifferentKeyGoesDownAsSoonAsTheOneBeforeComesUp()
    {
        var (machine, keys) = Booted();
        keys.Down(ElectronKey.P);
        keys.Up(ElectronKey.P);
        keys.Down(ElectronKey.R);
        keys.Up(ElectronKey.R);

        keys.Run(ElectronKeyPresses.HoldCycles - Slack);
        Assert.True(machine.Keyboard.IsDown(ElectronKey.P));
        Assert.False(machine.Keyboard.IsDown(ElectronKey.R), "R went down before P came up: the order was not kept");

        keys.Run(2 * Slack);
        Assert.False(machine.Keyboard.IsDown(ElectronKey.P));
        Assert.True(machine.Keyboard.IsDown(ElectronKey.R), "R waited for a rest that only the same key needs");
    }

    [Fact]
    public void ShiftHeldAroundAKeyIsDownForTheWholeOfThatKeysHold()
    {
        var (machine, keys) = Booted();
        keys.Down(ElectronKey.Shift);
        keys.Down(ElectronKey.Colon);
        keys.Up(ElectronKey.Colon);
        keys.Up(ElectronKey.Shift);

        // Sampled across the colon's hold: Shift is down every time the colon is, and the colon
        // must have been seen down, or the loop would prove nothing.
        long start = machine.Cycles;
        int seen = 0;
        while (machine.Cycles - start < ElectronKeyPresses.HoldCycles + (4 * Slack))
        {
            keys.Run(1_000);
            if (machine.Keyboard.IsDown(ElectronKey.Colon))
            {
                seen++;
                Assert.True(machine.Keyboard.IsDown(ElectronKey.Shift), "Shift came up while the colon was still down");
            }
        }

        Assert.True(seen > 0, "the colon was never seen down");
        Assert.False(machine.Keyboard.IsDown(ElectronKey.Colon));
        Assert.False(machine.Keyboard.IsDown(ElectronKey.Shift));
    }

    [Fact]
    public void AKeyHeldLongerThanTheHoldStaysDownUntilItComesUp()
    {
        var (machine, keys) = Booted();
        keys.Down(ElectronKey.Space);
        keys.Run(10 * ElectronKeyPresses.HoldCycles);
        Assert.True(machine.Keyboard.IsDown(ElectronKey.Space));

        keys.Up(ElectronKey.Space);
        keys.Run(1);
        Assert.False(machine.Keyboard.IsDown(ElectronKey.Space), "a key held past its hold did not come up when asked");
    }

    [Fact]
    public void AValueThatIsNotAKeyIsRefused()
    {
        // ula.md s7b: column 0 bit 2 and column 2 bit 3 are not connected, and 14 columns of 4 end at $3D.
        var (_, keys) = Booted();
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.Down((ElectronKey)0x20));
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.Up((ElectronKey)0x32));
        Assert.Throws<ArgumentOutOfRangeException>(() => keys.Down((ElectronKey)0x40));
        Assert.Equal(0, keys.Pending);
    }
}
