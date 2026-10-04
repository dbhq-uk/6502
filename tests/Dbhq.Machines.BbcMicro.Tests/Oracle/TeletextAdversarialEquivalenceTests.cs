using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// <see cref="VideoUlaEquivalenceTests"/> leaning on mode 7: the lazy video ULA and its teletext
/// chip against the per-character oracle, with programs that are mostly mode 7, screen memory
/// poked mostly at <c>$7C00</c> and <c>$3C00</c> with control codes (<c>$80</c> to <c>$9F</c>),
/// and control register writes that turn the teletext select and the 2 MHz clock on and off
/// seven times as often. The whole framebuffer must match at the end of each frame.
/// </summary>
/// <remarks>
/// Written by task 9's reviewer as a scratch check and kept (task 15) with four of its twelve
/// seeds, so it runs in seconds. As in <see cref="VideoUlaEquivalenceTests"/>, screen memory is changed only
/// after the lazy ULA has been brought up to the cycle (reading the picture does that), because
/// the lazy ULA reads a line's bytes when it draws the line, not in each byte's own cycle
/// (<c>docs/known-differences.md</c>), and a change made at any other moment would show that.
/// It pins the model, including the choices both share, not the hardware.
/// </remarks>
public class TeletextAdversarialEquivalenceTests
{
    private static readonly byte[][] CrtcSets =
    [
        [0x7F, 0x50, 0x62, 0x28, 0x26, 0x00, 0x20, 0x23, 0x01, 0x07, 0x67, 0x08, 0x06, 0x00],
        [0x7F, 0x50, 0x62, 0x28, 0x1E, 0x02, 0x19, 0x1C, 0x01, 0x09, 0x67, 0x09, 0x08, 0x00],
        [0x3F, 0x28, 0x31, 0x24, 0x26, 0x00, 0x20, 0x23, 0x01, 0x07, 0x67, 0x08, 0x0B, 0x00],
        [0x3F, 0x28, 0x31, 0x24, 0x1E, 0x02, 0x19, 0x1C, 0x01, 0x09, 0x67, 0x09, 0x0C, 0x00],
        [0x3F, 0x28, 0x33, 0x24, 0x1E, 0x02, 0x19, 0x1C, 0x93, 0x12, 0x72, 0x13, 0x28, 0x00],
    ];

    private static readonly byte[] Controls = [0x9C, 0xD8, 0xF4, 0x9C, 0x88, 0xC4, 0x88, 0x4B];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void TheLazyUlaMatchesTheOracleThroughMode7HeavyPrograms(int seed)
    {
        var bus = new BbcBus(BbcSession.Roms);
        var oracle = new ReferenceBbcBus(BbcSession.Roms);
        bus.PowerOnReset();
        oracle.PowerOnReset();

        var random = new Random(seed * 7919 + 11);
        long compares = 0, teletextCompares = 0, writes = 0, lastCompare = 0, oracleFrames = 0;

        void Write(ushort address, byte value)
        {
            bus.Write(address, value);
            oracle.Write(address, value);
            Assert.Equal(oracle.Cycles, bus.Cycles);
            writes++;
        }

        void Crtc(int register, byte value)
        {
            Write((ushort)(0xFE00 + (2 * random.Next(4))), (byte)register);
            Write(0xFE01, value);
        }

        void Poke(int count)
        {
            // Reading the picture brings the lazy ULA up to now, so both have read screen memory
            // as it stood before the poke.
            _ = bus.Screen.Frames;
            for (int i = 0; i < count; i++)
            {
                ushort address = random.Next(2) == 0
                    ? (ushort)((random.Next(2) == 0 ? 0x7C00 : 0x3C00) + random.Next(0x400))
                    : (ushort)random.Next(0x3000, 0x8000);
                byte value = random.Next(2) == 0 ? (byte)(0x80 + random.Next(32)) : (byte)random.Next(256);
                bus.PokeRam(address, value);
                oracle.PokeRam(address, value);
            }
        }

        void Compare()
        {
            ReadOnlySpan<uint> actual = bus.Screen.Pixels;
            uint[] expected = oracle.VideoUla.Pixels;
            if (!actual.SequenceEqual(expected))
            {
                int i = 0;
                while (actual[i] == expected[i])
                {
                    i++;
                }
                Assert.Fail($"at cycle {bus.Cycles} after {writes} writes, pixel ({i % 640}, {i / 640}): expected {expected[i]:X8}, was {actual[i]:X8}");
            }
            Assert.Equal(oracle.VideoUla.Frames, bus.Screen.Frames);
            compares++;
            teletextCompares += bus.VideoUla.Teletext ? 1 : 0;
            lastCompare = bus.Cycles;
        }

        Write(0xFE42, 0x0F);
        for (int a = 0; a < 0x8000; a++)
        {
            byte value = (byte)random.Next(256);
            bus.PokeRam((ushort)a, value);
            oracle.PokeRam((ushort)a, value);
        }

        while (bus.Cycles < 5_000_000)
        {
            Program(random, Crtc, Write);
            Poke(random.Next(200));

            int span = random.Next(4) == 0 ? random.Next(150_000) : random.Next(1, 20_000);
            for (int i = 0; i < span; i++)
            {
                Assert.Equal(oracle.Read(0x0000), bus.Read(0x0000));
                if (oracle.VideoUla.Frames != oracleFrames)
                {
                    oracleFrames = oracle.VideoUla.Frames;
                    if (bus.Cycles - lastCompare >= 4_000)
                    {
                        Compare();
                    }
                }

                int kind = random.Next(600);
                if (kind < 4)
                {
                    Write(0xFE21, (byte)random.Next(256));
                    Poke(random.Next(3) * random.Next(40));
                }
                else if (kind < 12)
                {
                    // Mode 7's control byte with the teletext select, the clock and the shape flipped at random.
                    byte control = random.Next(3) == 0
                        ? (byte)random.Next(256)
                        : (byte)(0x4B ^ (random.Next(2) << 1) ^ (random.Next(2) << 4) ^ (random.Next(8) << 5));
                    Write(0xFE20, control);
                    Poke(random.Next(3) * random.Next(40));
                }
                else if (kind < 14)
                {
                    int register = new[] { 1, 8, 10, 11, 12, 13, 14, 15, 0, 9, 2, 3 }[random.Next(random.Next(4) == 0 ? 12 : 8)];
                    byte value = register switch
                    {
                        // The cursor kept on the screen and steady most of the time, so it is
                        // there when frames are compared.
                        10 => (byte)(random.Next(3) == 0 ? random.Next(128) : random.Next(8)),
                        14 => (byte)(random.Next(4) == 0 ? random.Next(64) : 0x06 + random.Next(8)),
                        8 or 15 or 13 => (byte)random.Next(256),
                        _ => (byte)random.Next(0x50),
                    };
                    Crtc(register, value);
                    Poke(random.Next(3) * random.Next(40));
                }
                else if (kind < 15)
                {
                    // Latch bit 4 or 5, the screen start adder.
                    Write(0xFE40, (byte)((random.Next(2) << 3) | (4 + random.Next(2))));
                    Poke(random.Next(3) * random.Next(40));
                }
                else if (kind == 15 && random.Next(40) == 0)
                {
                    bus.BreakReset();
                    oracle.BreakReset();
                }
            }
        }

        Compare();
        Assert.True(compares > 100, $"only {compares} frames compared");
        Assert.True(teletextCompares > 60, $"only {teletextCompares} frames compared in teletext");
        Assert.True(writes > 20_000, $"only {writes} writes");
    }

    private static void Program(Random random, Action<int, byte> crtc, Action<ushort, byte> write)
    {
        if (random.Next(4) != 0)
        {
            // An OS mode, changed a little.
            int mode = random.Next(3) == 0 ? random.Next(8) : 7;
            byte[] set = CrtcSets[mode switch { 0 or 1 or 2 => 0, 3 => 1, 4 or 5 => 2, 6 => 3, _ => 4 }];
            write(0xFE20, Controls[mode]);
            int[] latch = mode switch { 0 or 1 or 2 => [0, 1], 3 => [0, 0], 4 or 5 => [1, 1], _ => [1, 0] };
            write(0xFE40, (byte)((latch[0] << 3) | 4));
            write(0xFE40, (byte)((latch[1] << 3) | 5));
            for (int r = 11; r >= 0; r--)
            {
                crtc(r, set[r]);
            }
            crtc(12, set[12]);
            crtc(13, set[13]);
            for (int n = random.Next(4); n > 0; n--)
            {
                int r = new[] { 1, 8, 12, 13, 9, 4, 0, 10, 11 }[random.Next(9)];
                byte value = r switch
                {
                    8 => (byte)random.Next(256),
                    12 => (byte)(set[12] + random.Next(-3, 40)),
                    13 => (byte)random.Next(256),
                    10 => (byte)random.Next(128),
                    _ => (byte)(set[r] + random.Next(-3, 4)),
                };
                crtc(r, value);
            }
            crtc(14, (byte)(set[12] + random.Next(0, 9)));
            crtc(15, (byte)random.Next(256));
            if (random.Next(2) == 0)
            {
                // A steady cursor, often a block, and often at the end of a row, where a long
                // cursor or a cursor skew runs on into the border.
                crtc(10, (byte)random.Next(8));
                crtc(11, (byte)random.Next(8, 12));
                if (random.Next(2) == 0)
                {
                    int column = set[1] - 1 - random.Next(3);
                    int address = (set[12] << 8) + (set[1] * random.Next(20)) + column;
                    crtc(14, (byte)((address >> 8) & 0x3F));
                    crtc(15, (byte)address);
                }
            }

            byte[] palette = mode switch
            {
                1 or 5 => [0xA0, 0xB0, 0xE0, 0xF0, 0x84, 0x94, 0xC4, 0xD4, 0x26, 0x36, 0x66, 0x76, 0x07, 0x17, 0x47, 0x57],
                2 => [0xF8, 0xE9, 0xDA, 0xCB, 0xBC, 0xAD, 0x9E, 0x8F, 0x70, 0x61, 0x52, 0x43, 0x34, 0x25, 0x16, 0x07],
                _ => [0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0, 0x07, 0x17, 0x27, 0x37, 0x47, 0x57, 0x67, 0x77],
            };
            foreach (byte value in palette)
            {
                write(0xFE21, random.Next(8) == 0 ? (byte)random.Next(256) : value);
            }
        }
        else
        {
            // A small random program: frames of a few lines, lines of a few characters.
            write(0xFE20, (byte)random.Next(256));
            crtc(0, (byte)random.Next(random.Next(4) == 0 ? 4 : 90));
            crtc(1, (byte)random.Next(60));
            crtc(2, (byte)random.Next(60));
            crtc(3, (byte)random.Next(256));
            int rows = random.Next(random.Next(3) == 0 ? 40 : 5);
            crtc(4, (byte)rows);
            crtc(5, (byte)random.Next(4));
            crtc(6, (byte)random.Next(rows + 2));
            crtc(7, (byte)random.Next(rows + 2));
            crtc(8, (byte)random.Next(256));
            crtc(9, (byte)random.Next(random.Next(5) == 0 ? 32 : 10));
            crtc(10, (byte)random.Next(128));
            crtc(11, (byte)random.Next(12));
            crtc(12, (byte)random.Next(64));
            crtc(13, (byte)random.Next(256));
            crtc(14, (byte)random.Next(64));
            crtc(15, (byte)random.Next(256));
        }
    }
}
