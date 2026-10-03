using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The machine's VIAs against the per-cycle oracle they replaced, through long runs of random
/// register accesses, input changes and ticks, with the same seed every time.
/// </summary>
/// <remarks>
/// <para>
/// Each run drives three chips with the same inputs: the oracle, which does all of its work in
/// every tick, and two copies of the real chip. <c>Watched</c> is compared with the oracle in every
/// cycle: every register as a read would see it, the IRQ line, both ports' pins and inputs and the
/// CA2 and CB2 outputs. Looking at a chip makes it catch up, so the watched copy never skips a
/// cycle. <c>Unwatched</c> is compared only when it is accessed and at random moments, so between
/// those it catches up over spans of up to tens of thousands of cycles at once, which is the path
/// the machine takes.
/// </para>
/// <para>
/// The runs alternate busy stretches, in which an access or an input change comes every few
/// cycles, with quiet ones in which nothing touches the chip and the timers run out, wrap and
/// reload by themselves. Timer values are mostly small, so the timers expire, collide with
/// accesses and flag often.
/// </para>
/// </remarks>
public class ViaEquivalenceTests
{
    private const int Seed = 6522;

    [Fact]
    public void TheVia6522MatchesThePerCycleOracleCycleForCycle()
    {
        var oracle = new ReferenceVia6522();
        var watched = new Via6522();
        var unwatched = new Via6522();
        var counts = new int[6];
        oracle.PortAWritten += () => counts[0]++;
        oracle.PortBWritten += () => counts[1]++;
        watched.PortAWritten += () => counts[2]++;
        watched.PortBWritten += () => counts[3]++;
        unwatched.PortAWritten += () => counts[4]++;
        unwatched.PortBWritten += () => counts[5]++;

        var random = new Random(Seed);
        var run = new Run(
            random,
            tick: () =>
            {
                oracle.Tick();
                watched.Tick();
                unwatched.Tick();
            },
            access: () => RandomAccess(random, oracle, watched, unwatched),
            compareWatched: () => Assert.Equal(State.Of(oracle), State.Of(watched)),
            compareUnwatched: () => Assert.Equal(State.Of(oracle), State.Of(unwatched)));

        long accesses = run.Go(busyCycles: 800_000);

        Assert.True(accesses > 100_000, $"only {accesses} accesses");
        Assert.Equal(counts[0], counts[2]);
        Assert.Equal(counts[1], counts[3]);
        Assert.Equal(counts[0], counts[4]);
        Assert.Equal(counts[1], counts[5]);
    }

    [Fact]
    public void TheSystemViaAndItsKeyboardMatchThePerCycleOracleCycleForCycle()
    {
        var oracleKeys = new BbcKeyboard(7);
        var watchedKeys = new BbcKeyboard(7);
        var unwatchedKeys = new BbcKeyboard(7);
        var oracle = new ReferenceSystemVia(oracleKeys);
        var watched = new SystemVia(watchedKeys);
        var unwatched = new SystemVia(unwatchedKeys);
        var sounds = new List<byte>[] { [], [], [] };
        oracle.SoundWrite += sounds[0].Add;
        watched.SoundWrite += sounds[1].Add;
        unwatched.SoundWrite += sounds[2].Add;

        BbcKey[] keys = Enum.GetValues<BbcKey>();
        var random = new Random(Seed + 1);
        var run = new Run(
            random,
            tick: () =>
            {
                oracle.Tick();
                watched.Tick();
                unwatched.Tick();
            },
            access: () =>
            {
                int kind = random.Next(10);
                if (kind < 2)
                {
                    // A key goes down or comes up. Most are in rows 1 to 7, which drive CA2.
                    BbcKey key = keys[random.Next(keys.Length)];
                    bool down = random.Next(2) == 0;
                    foreach (BbcKeyboard keyboard in new[] { oracleKeys, watchedKeys, unwatchedKeys })
                    {
                        if (down)
                        {
                            keyboard.Press(key);
                        }
                        else
                        {
                            keyboard.Release(key);
                        }
                    }
                }
                else if (kind < 3)
                {
                    bool vsync = random.Next(2) == 0;
                    oracle.VsyncInput = vsync;
                    watched.VsyncInput = vsync;
                    unwatched.VsyncInput = vsync;
                }
                else if (kind < 6)
                {
                    // The latch: write ORB as the OS does, a bit address and a value, so the
                    // keyboard swings between autoscan and enabled and the sound strobe fires.
                    byte value = (byte)random.Next(16);
                    oracle.Write(0x0, value);
                    watched.Write(0x0, value);
                    unwatched.Write(0x0, value);
                }
                else
                {
                    RandomAccess(random, oracle, watched, unwatched);
                }

                Assert.Equal(oracle.Latch, watched.Latch);
                Assert.Equal(oracle.Latch, unwatched.Latch);
            },
            compareWatched: () => Assert.Equal(State.Of(oracle), State.Of(watched)),
            compareUnwatched: () => Assert.Equal(State.Of(oracle), State.Of(unwatched)));

        long accesses = run.Go(busyCycles: 800_000);

        Assert.True(accesses > 100_000, $"only {accesses} accesses");
        Assert.Equal(sounds[0], sounds[1]);
        Assert.Equal(sounds[0], sounds[2]);
        Assert.NotEmpty(sounds[0]);
    }

    /// <summary>One random read, write, peek, input change or reset, on all three chips at once.</summary>
    private static void RandomAccess(Random random, ReferenceVia6522 oracle, Via6522 watched, Via6522 unwatched)
    {
        int kind = random.Next(100);
        if (kind < 40)
        {
            int register = RandomRegister(random);
            byte expected = oracle.Read(register);
            Assert.Equal(expected, watched.Read(register));
            Assert.Equal(expected, unwatched.Read(register));
        }
        else if (kind < 85)
        {
            int register = RandomRegister(random);
            byte value = RandomValue(random, register);
            oracle.Write(register, value);
            watched.Write(register, value);
            unwatched.Write(register, value);
        }
        else if (kind < 99)
        {
            bool level = random.Next(2) == 0;
            byte pins = (byte)random.Next(256);
            switch (random.Next(6))
            {
                case 0:
                    oracle.SetCa1(level);
                    watched.SetCa1(level);
                    unwatched.SetCa1(level);
                    break;
                case 1:
                    oracle.SetCa2(level);
                    watched.SetCa2(level);
                    unwatched.SetCa2(level);
                    break;
                case 2:
                    oracle.SetCb1(level);
                    watched.SetCb1(level);
                    unwatched.SetCb1(level);
                    break;
                case 3:
                    oracle.SetCb2(level);
                    watched.SetCb2(level);
                    unwatched.SetCb2(level);
                    break;
                case 4:
                    oracle.PortAInput = pins;
                    watched.PortAInput = pins;
                    unwatched.PortAInput = pins;
                    break;
                default:
                    // PB6 changing is what timer 2 counts in pulse mode.
                    oracle.PortBInput = pins;
                    watched.PortBInput = pins;
                    unwatched.PortBInput = pins;
                    break;
            }
        }
        else if (random.Next(20) == 0)
        {
            oracle.Reset();
            watched.Reset();
            unwatched.Reset();
        }
    }

    private static int RandomRegister(Random random)
    {
        // The timers, the flags and the shift register twice as often as the ports.
        int[] weighted = [0, 1, 2, 3, 4, 4, 5, 5, 6, 7, 8, 8, 9, 9, 0xA, 0xA, 0xB, 0xB, 0xC, 0xD, 0xD, 0xE, 0xE, 0xF];
        return weighted[random.Next(weighted.Length)];
    }

    private static byte RandomValue(Random random, int register) => register switch
    {
        // High bytes of the timers mostly 0, so a timer runs out within a few dozen cycles.
        5 or 7 or 9 => random.Next(4) == 0 ? (byte)random.Next(256) : (byte)0,
        4 or 6 or 8 => random.Next(4) == 0 ? (byte)random.Next(256) : (byte)random.Next(40),

        // Shift mode 010 half the time, so the shift register's count is exercised.
        0xB => random.Next(2) == 0 ? (byte)((random.Next(256) & ~0x1C) | 0x08) : (byte)random.Next(256),
        _ => (byte)random.Next(256),
    };

    /// <summary>Everything a chip shows the outside, read without side effects.</summary>
    private readonly record struct State(
        ulong RegistersLow, ulong RegistersHigh, byte PinsA, byte PinsB, byte InputA, byte InputB, bool Irq, bool Ca2Out, bool Cb2Out)
    {
        public static State Of(Via6522 via) => new(
            Pack(via.Peek, 0), Pack(via.Peek, 8), via.PortAPins, via.PortBPins, via.PortAInput, via.PortBInput, via.Irq, via.Ca2Out, via.Cb2Out);

        public static State Of(ReferenceVia6522 via) => new(
            Pack(via.Peek, 0), Pack(via.Peek, 8), via.PortAPins, via.PortBPins, via.PortAInput, via.PortBInput, via.Irq, via.Ca2Out, via.Cb2Out);

        private static ulong Pack(Func<int, byte> peek, int first)
        {
            ulong packed = 0;
            for (int i = 0; i < 8; i++)
            {
                packed |= (ulong)peek(first + i) << (8 * i);
            }
            return packed;
        }
    }

    /// <summary>The schedule: busy stretches and quiet ones, and when each copy is compared.</summary>
    private sealed class Run(Random random, Action tick, Action access, Action compareWatched, Action compareUnwatched)
    {
        /// <summary>Runs until the busy stretches add up to <paramref name="busyCycles"/>, and returns the accesses made.</summary>
        public long Go(long busyCycles)
        {
            long accesses = 0;
            long busyDone = 0;
            while (busyDone < busyCycles)
            {
                bool busy = random.Next(3) != 0;
                int length = busy ? random.Next(50, 3000) : random.Next(1, 40_000);
                for (int i = 0; i < length; i++)
                {
                    tick();
                    if (busy)
                    {
                        busyDone++;
                        if (random.Next(4) == 0)
                        {
                            access();
                            accesses++;
                            compareUnwatched();
                        }
                    }

                    compareWatched();
                    if (random.Next(5000) == 0)
                    {
                        compareUnwatched();
                    }
                }

                compareUnwatched();
            }

            return accesses;
        }
    }
}
