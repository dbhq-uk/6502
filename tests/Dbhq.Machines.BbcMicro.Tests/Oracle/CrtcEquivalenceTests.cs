using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The lazy CRTC against the per-character oracle, through random register programs written at
/// random characters, with the same seed every time.
/// </summary>
/// <remarks>
/// <para>
/// Three chips get the same writes and resets: the oracle, which does every rule in every tick,
/// and two copies of <see cref="Crtc6845"/>. <c>Watched</c> is looked at after every character,
/// so it catches up one character at a time. <c>Unwatched</c> is looked at only at random
/// moments, from a few characters to several frames apart, so it catches up over long spans,
/// jumps to the state it worked out ahead, and passes many VSYNC edges and frame starts in one
/// catch-up, which is the path the machine takes. Every output and the number of each event so
/// far must match.
/// </para>
/// <para>
/// The programs are a mix: the OS's own register sets with a few values changed, and small
/// random ones (R0 from 0 up, a row or two a frame, short VSYNCs) that make frames, edges and
/// the quirks (R0 below 2, a frame end decided early, adjust lines, both interlace modes, a C9
/// or C4 that runs past its register and wraps) come round every few hundred characters.
/// </para>
/// </remarks>
public class CrtcEquivalenceTests
{
    private static readonly byte[][] OsSets =
    [
        [0x7F, 0x50, 0x62, 0x28, 0x26, 0x00, 0x20, 0x23, 0x01, 0x07, 0x67, 0x08, 0x06, 0x00],
        [0x7F, 0x50, 0x62, 0x28, 0x1E, 0x02, 0x19, 0x1C, 0x01, 0x09, 0x67, 0x09, 0x08, 0x00],
        [0x3F, 0x28, 0x31, 0x24, 0x26, 0x00, 0x20, 0x23, 0x01, 0x07, 0x67, 0x08, 0x0B, 0x00],
        [0x3F, 0x28, 0x33, 0x24, 0x1E, 0x02, 0x19, 0x1C, 0x93, 0x12, 0x72, 0x13, 0x28, 0x00],
    ];

    [Fact]
    public void TheLazyCrtcMatchesThePerCharacterOracle()
    {
        var oracle = new ReferenceCrtc6845();
        var watched = new Crtc6845();
        var unwatched = new Crtc6845();
        var counts = new int[6];
        oracle.VSyncFell += () => counts[0]++;
        oracle.FrameStarted += () => counts[1]++;
        watched.VSyncFell += () => counts[2]++;
        watched.FrameStarted += () => counts[3]++;
        unwatched.VSyncFell += () => counts[4]++;
        unwatched.FrameStarted += () => counts[5]++;

        var random = new Random(6845);
        long ticks = 0, writes = 0, looks = 0;
        long nextLook = 1;

        void Write(int register, byte value)
        {
            oracle.WriteAddress((byte)register);
            watched.WriteAddress((byte)register);
            unwatched.WriteAddress((byte)register);
            oracle.WriteData(value);
            watched.WriteData(value);
            unwatched.WriteData(value);
            writes++;
        }

        // The chip's outputs are read first: looking is what makes it catch up and raise its events.
        void Compare(Crtc6845 chip, int countsAt, string which)
        {
            var expected = (oracle.MemoryAddress, oracle.RasterAddress, oracle.DisplayEnable, oracle.HSync, oracle.VSync, oracle.Cursor, oracle.LineStartAddress, oracle.VerticalDisplay, counts[0], counts[1]);
            var outputs = (chip.MemoryAddress, chip.RasterAddress, chip.DisplayEnable, chip.HSync, chip.VSync, chip.Cursor, chip.LineStartAddress, chip.VerticalDisplay);
            var actual = (outputs.Item1, outputs.Item2, outputs.Item3, outputs.Item4, outputs.Item5, outputs.Item6, outputs.Item7, outputs.Item8, counts[countsAt], counts[countsAt + 1]);
            if (expected != actual)
            {
                Assert.Fail($"{which} at character {ticks} after {writes} writes: expected {expected}, was {actual}");
            }
        }

        while (ticks < 10_000_000)
        {
            RandomProgram(random, Write);
            Write(14, (byte)random.Next(64));
            Write(15, (byte)random.Next(256));
            if (random.Next(4) == 0)
            {
                oracle.Reset();
                watched.Reset();
                unwatched.Reset();
            }

            // Run the program for a while: quiet, or with writes at random characters.
            long span = random.Next(4) == 0 ? random.Next(200_000) : random.Next(5_000);
            bool busy = random.Next(2) == 0;
            for (long i = 0; i < span; i++)
            {
                oracle.Tick();
                watched.Tick();
                unwatched.Tick();
                ticks++;
                Compare(watched, 2, "watched");
                if (ticks >= nextLook)
                {
                    Compare(unwatched, 4, "unwatched");
                    looks++;
                    nextLook = ticks + (random.Next(100) == 0 ? random.Next(1, 300_000) : random.Next(1, 500));
                }

                if (busy && random.Next(40) == 0)
                {
                    int r = random.Next(4) == 0 ? random.Next(16) : new[] { 0, 4, 5, 7, 8, 9, 12, 13, 14, 15 }[random.Next(10)];
                    Write(r, (byte)random.Next(r is 0 or 2 or 13 or 15 ? 40 : 12));
                }
            }
        }

        Assert.True(counts[0] > 5_000, $"only {counts[0]} VSYNC falls");
        Assert.True(counts[1] > 5_000, $"only {counts[1]} frames");
        Assert.True(looks > 2_000, $"only {looks} looks at the unwatched chip");
    }

    /// <summary>
    /// Asks the lazy chip for its state at random cycles from where it stands to now, in order, and
    /// compares each with what the plain model was at that cycle. The plain model's states are
    /// recorded every character, so any past cycle can be checked; a register write is made only
    /// after the consumer has looked up to its cycle, as the contract in <see cref="Crtc6845.StateAt"/>
    /// asks, and the record starts again after it.
    /// </summary>
    [Fact]
    public void StateAtMatchesThePerCharacterOracleAtPastCycles()
    {
        var oracle = new ReferenceCrtc6845();
        var chip = new Crtc6845();
        var random = new Random(6845 + 1);
        var record = new List<CrtcState>();
        long now = 0, checks = 0, writes = 0;

        void Look()
        {
            if (record.Count == 0)
            {
                return;
            }

            long from = Math.Max(chip.StateCycle, record.Count == 0 ? now : record[0].Cycle);
            for (long t = from; t <= now; t += 1 + random.Next(random.Next(3) == 0 ? 3_000 : 70))
            {
                CrtcState expected = record[(int)(t - record[0].Cycle)];
                CrtcState actual = chip.StateAt(t);
                if (expected != actual)
                {
                    Assert.Fail($"at character {t} after {writes} writes: expected {expected}, was {actual}");
                }
                checks++;
            }
        }

        void Write(int register, byte value)
        {
            Look();
            oracle.WriteAddress((byte)register);
            chip.WriteAddress((byte)register);
            oracle.WriteData(value);
            chip.WriteData(value);
            record.Clear();
            writes++;
        }

        while (now < 4_000_000)
        {
            RandomProgram(random, Write);
            Write(14, (byte)random.Next(64));
            Write(15, (byte)random.Next(256));
            if (random.Next(4) == 0)
            {
                Look();
                oracle.Reset();
                chip.Reset();
                record.Clear();
            }

            long span = random.Next(4) == 0 ? random.Next(100_000) : random.Next(5_000);
            for (long i = 0; i < span; i++)
            {
                oracle.Tick();
                chip.Tick();
                now++;
                record.Add(oracle.State(now, 1));
                if (random.Next(300) == 0)
                {
                    Look();
                }
                if (random.Next(3_000) == 0)
                {
                    Write(random.Next(16), (byte)random.Next(40));
                }
            }
        }

        // A cycle the chip has passed, or one still to come, is refused.
        Look();
        Assert.Throws<InvalidOperationException>(() => chip.StateAt(chip.StateCycle - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => chip.StateAt(now + 1));
        Assert.True(checks > 50_000, $"only {checks} checks");
    }

    private static void RandomProgram(Random random, Action<int, byte> write)
    {
        // A program: the OS's, changed a little, or a small random one.
        if (random.Next(3) == 0)
        {
            byte[] set = OsSets[random.Next(OsSets.Length)];
            for (int r = 11; r >= 0; r--)
            {
                write(r, set[r]);
            }
            write(12, set[12]);
            write(13, set[13]);
            for (int n = random.Next(3); n > 0; n--)
            {
                int r = random.Next(16);
                write(r, (byte)(set[Math.Min(r, 13)] + random.Next(-2, 3)));
            }
        }
        else
        {
            write(0, (byte)random.Next(random.Next(4) == 0 ? 4 : 40));
            write(1, (byte)random.Next(24));
            write(2, (byte)random.Next(24));
            write(3, (byte)random.Next(256));
            int rows = random.Next(5);
            write(4, (byte)rows);
            write(5, (byte)random.Next(4));
            write(6, (byte)random.Next(rows + 2));
            write(7, (byte)random.Next(rows + 2));
            write(8, (byte)random.Next(256));
            write(9, (byte)random.Next(random.Next(5) == 0 ? 32 : 6));
            write(10, (byte)random.Next(128));
            write(11, (byte)random.Next(8));
        }
    }
}
