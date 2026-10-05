using Xunit;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The sound through the bus and on the real OS (ula.md s8): the registers reach it, the page's
/// read catches it up, and the start-up beep makes the output toggle at a pitch a person can hear.
/// </summary>
public class SoundTests
{
    private const ushort Counter = 0xFE06;
    private const ushort Control = 0xFE07;
    private const long CyclesPerSecond = 2_000_000;

    private static ElectronBus NewBus(int sampleRate = 44_100) => new(ElectronSession.Roms, sampleRate);

    /// <summary>Runs the bus with no CPU: a ROM read is one cycle (s3a), so this is exact.</summary>
    private static void Idle(ElectronBus bus, long cycles)
    {
        long end = bus.Cycles + cycles;
        while (bus.Cycles < end)
        {
            bus.Read(0xC000);
        }
    }

    [Fact]
    public void WritesToTheCounterAndTheControlRegisterReachTheSoundInEveryMirror()
    {
        // The ULA's registers repeat through $FE00-$FEFF with the top nibble ignored (s1c), so
        // $FEF6 is the counter and $FEE7 is the control register.
        ElectronBus bus = NewBus();
        bus.Write(0xFEE7, 0x02);
        bus.Write(0xFEF6, 15);
        Assert.Equal(15, bus.Sound.Counter);
        Assert.True(bus.Sound.Enabled);
        long start = bus.Cycles;
        Idle(bus, 100_000);

        // S = 15: a toggle every 512 cycles from the counter write (s8), and the clock moved on
        // (100,000 + the write's own cycles) / 512 = 195.3 toggles.
        Assert.InRange(bus.Sound.Toggles, 195, 196);
        Assert.True(bus.Cycles - start >= 100_000);
    }

    [Fact]
    public void ReadingTheBufferBringsItUpToTheBusClock()
    {
        // The sound does nothing on the cycle: the page's read does the work. After the idle run
        // the buffer holds one sample for each whole 2,000,000 / 44,100 cycles that have passed.
        ElectronBus bus = NewBus(44_100);
        bus.Write(Control, 0x02);
        bus.Write(Counter, 15);
        Idle(bus, CyclesPerSecond);
        long expected = bus.Cycles * 44_100 / CyclesPerSecond;
        Assert.Equal(expected, bus.Sound.Buffer.Count);

        var samples = new float[bus.Sound.Buffer.Count];
        Assert.Equal(samples.Length, bus.Sound.Buffer.Read(samples));
        Assert.Contains(samples, s => s > 0.3f);
        Assert.Contains(samples, s => s < -0.3f);
    }

    [Fact]
    public void ASampleRateOfZeroOrLessIsRefusedByTheBusAndTheMachine()
    {
        foreach (int rate in new[] { 0, -1, 384_001 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ElectronBus(ElectronSession.Roms, rate));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ElectronMachine(ElectronSession.Roms, new ElectronOptions { SampleRate = rate }));
        }
    }

    [Fact]
    public void TheMachineBuildsItsBufferAtTheRateInTheOptions()
    {
        var machine = new ElectronMachine(ElectronSession.Roms, new ElectronOptions { SampleRate = 22_050 });
        Assert.Equal(22_050, machine.Bus.Sound.Buffer.SampleRate);
        Assert.Equal(22_050, machine.Bus.Sound.Buffer.Capacity);
    }

    [Fact]
    public void TheIdlePromptIsSilent()
    {
        // Control: a booted machine at its prompt makes no sound. The boot uses $FE06 for the tape
        // (s10c), and a toggle counts only in sound mode, so this holds the count at zero.
        ElectronSession session = new ElectronSession().Boot().RunUntilPrompt();
        ElectronBus bus = session.Machine.Bus;
        long before = bus.Sound.Toggles;
        session.RunFor(CyclesPerSecond);
        Assert.Equal(before, bus.Sound.Toggles);
    }

    [Fact]
    public void TheStartUpBeepTogglesTheOutputAtAnAudiblePitch()
    {
        // The beep is OSWRCH 7 (s8, from ROM $DA3F). A property of the ROM and not a number from
        // the model: typing VDU 7 makes the output toggle, and the counter the OS gave it, before and
        // after any step in which it toggled, is a pitch within what a person hears, 20 Hz to 20 kHz, by the sheet's
        // formula 1 MHz / (32 x (S + 1)).
        ElectronSession session = new ElectronSession().Boot().RunUntilPrompt();
        session.Type("VDU 7");
        ElectronMachine machine = session.Machine;
        ElectronBus bus = machine.Bus;
        long toggles = bus.Sound.Toggles;
        var counters = new SortedSet<int>();

        // Return down for one frame, then up, then run on with the beep sounding, stepping an
        // instruction at a time so the counter can be seen while the output toggles.
        machine.Keyboard.Down(ElectronKey.Return);
        Observe(machine, ElectronSession.HoldCycles, counters, ref toggles);
        machine.Keyboard.Up(ElectronKey.Return);
        Observe(machine, 2 * CyclesPerSecond, counters, ref toggles);

        Assert.True(bus.Sound.Toggles > 0, "VDU 7 did not make the output toggle.");
        Assert.NotEmpty(counters);
        foreach (int s in counters)
        {
            double hertz = 1_000_000.0 / (32 * (s + 1));
            Assert.InRange(hertz, 20.0, 20_000.0);
        }
    }

    /// <summary>Steps for <paramref name="cycles"/>, noting the counter before and after any step that made the output toggle (a step can write it).</summary>
    private static void Observe(ElectronMachine machine, long cycles, SortedSet<int> counters, ref long toggles)
    {
        ElectronBus bus = machine.Bus;
        long end = machine.Cycles + cycles;
        while (machine.Cycles < end)
        {
            int counter = bus.Sound.Counter;
            bool sounding = bus.Sound.Enabled;
            machine.Step();
            long now = bus.Sound.Toggles;
            if (now > toggles && sounding)
            {
                counters.Add(counter);
                counters.Add(bus.Sound.Counter);
            }

            toggles = now;
        }
    }
}
