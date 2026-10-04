using System.Reflection;
using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests.Oracle;

/// <summary>
/// The lazy video ULA's record of which framebuffer rows are all black, checked directly. The ULA
/// keeps a flag a row (<c>_rowBlack</c>) and a count of rows not known to be black
/// (<c>_rowsNotBlack</c>) so a black row costs nothing to clear; a flag left set on a row with
/// something drawn in it would leave the picture wrong in a way the frame comparison sees only if
/// it happens to look then. So every few hundred cycles, and at each frame end, every flagged row
/// must be all black and the count must match the flags, while programs of every mode with few
/// displayed rows, lines blanked by RA3, no display width, interlace and skews and short frames
/// switch rows between black and drawn. The frames are compared with the per-character oracle
/// too.
/// </summary>
/// <remarks>
/// Written by task 8's reviewer as a scratch check and kept (task 15) with two of its eight seeds
/// at two thirds of its length, so it runs in seconds. It reads two private fields by reflection, so
/// renaming either fails it at once rather than letting it check nothing.
/// </remarks>
public class BlackRowStressTests
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
    public void RowsFlaggedBlackAreBlackWhileRowsChangeBetweenBlackAndDrawn(int seed)
    {
        const long cycles = 2_000_000;
        var bus = new BbcBus(BbcSession.Roms);
        var oracle = new ReferenceBbcBus(BbcSession.Roms);
        bus.PowerOnReset();
        oracle.PowerOnReset();

        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        FieldInfo rowBlackField = typeof(VideoUla).GetField("_rowBlack", Private)
            ?? throw new InvalidOperationException("VideoUla has no _rowBlack: this test checks nothing until it is told the new name");
        FieldInfo notBlackField = typeof(VideoUla).GetField("_rowsNotBlack", Private)
            ?? throw new InvalidOperationException("VideoUla has no _rowsNotBlack: this test checks nothing until it is told the new name");

        var random = new Random(seed * 7919);
        long compares = 0, invariants = 0, lastCompare = 0, oracleFrames = 0, flaggedSeen = 0, unflaggedSeen = 0;

        void Write(ushort address, byte value)
        {
            bus.Write(address, value);
            oracle.Write(address, value);
        }

        void Crtc(int register, byte value)
        {
            Write((ushort)(0xFE00 + (2 * random.Next(4))), (byte)register);
            Write(0xFE01, value);
        }

        void Invariant()
        {
            _ = bus.Screen.Frames;
            bool[] flags = (bool[])rowBlackField.GetValue(bus.VideoUla)!;
            int notBlack = (int)notBlackField.GetValue(bus.VideoUla)!;
            ReadOnlySpan<uint> pixels = bus.Screen.Pixels;
            int count = 0;
            for (int r = 0; r < 512; r++)
            {
                if (!flags[r])
                {
                    count++;
                    unflaggedSeen++;
                    continue;
                }

                flaggedSeen++;
                for (int x = 0; x < 640; x++)
                {
                    if (pixels[(r * 640) + x] != 0xFF000000)
                    {
                        Assert.Fail($"seed {seed} cycle {bus.Cycles}: row {r} flagged black but pixel {x} is {pixels[(r * 640) + x]:X8}");
                    }
                }
            }

            Assert.Equal(count, notBlack);
            invariants++;
        }

        void Poke(int count)
        {
            _ = bus.Screen.Frames;
            for (int i = 0; i < count; i++)
            {
                ushort address = (ushort)random.Next(0x3000, 0x8000);
                byte value = (byte)random.Next(256);
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
                Assert.Fail($"seed {seed} at cycle {bus.Cycles}, pixel ({i % 640}, {i / 640}): expected {expected[i]:X8}, was {actual[i]:X8}");
            }
            Assert.Equal(oracle.VideoUla.Frames, bus.Screen.Frames);
            compares++;
            lastCompare = bus.Cycles;
        }

        Write(0xFE42, 0x0F);
        for (int a = 0; a < 0x8000; a++)
        {
            // Sparse content: many bytes zero so many lines are black-on-black or partly drawn.
            byte value = random.Next(3) == 0 ? (byte)random.Next(256) : (byte)0;
            bus.PokeRam((ushort)a, value);
            oracle.PokeRam((ushort)a, value);
        }

        while (bus.Cycles < cycles)
        {
            Program(random, Crtc, Write);
            Poke(random.Next(200));

            int span = random.Next(4) == 0 ? random.Next(80_000) : random.Next(1, 15_000);
            for (int i = 0; i < span; i++)
            {
                Assert.Equal(oracle.Read(0x0000), bus.Read(0x0000));
                if (oracle.VideoUla.Frames != oracleFrames)
                {
                    oracleFrames = oracle.VideoUla.Frames;
                    if (bus.Cycles - lastCompare >= 1_500)
                    {
                        Compare();
                        Invariant();
                    }
                }

                int kind = random.Next(400);
                if (kind < 12)
                {
                    // Split the line being drawn into pieces.
                    _ = bus.Screen.Frames;
                }
                else if (kind < 14)
                {
                    Invariant();
                }
                else if (kind < 17)
                {
                    Write(0xFE21, (byte)random.Next(256));
                    Poke(random.Next(3) * random.Next(40));
                }
                else if (kind < 19)
                {
                    // The teletext select on and off, and other control bytes.
                    byte control = random.Next(2) == 0
                        ? (byte)(Controls[random.Next(8)] ^ (random.Next(2) == 0 ? 0x02 : 0))
                        : (byte)random.Next(256);
                    Write(0xFE20, control);
                    Poke(random.Next(3) * random.Next(40));
                }
                else if (kind < 24)
                {
                    int register = new[] { 1, 4, 5, 6, 7, 8, 9, 12, 13, 0, 10, 14, 15 }[random.Next(13)];
                    byte value = register switch
                    {
                        1 => (byte)(random.Next(3) == 0 ? 0 : random.Next(0x52)),
                        4 => (byte)random.Next(random.Next(2) == 0 ? 6 : 0x28),
                        5 => (byte)random.Next(32),
                        6 => (byte)random.Next(random.Next(2) == 0 ? 4 : 0x28),
                        7 => (byte)random.Next(0x28),
                        8 => (byte)(new byte[] { 0x00, 0x01, 0x03, 0x02, 0x10, 0x11, 0x21, 0x93, 0xC1, 0x31 }[random.Next(10)]),
                        9 => (byte)random.Next(16),
                        12 => (byte)random.Next(64),
                        13 => (byte)random.Next(256),
                        0 => (byte)(0x3F + random.Next(-3, 0x41)),
                        10 => (byte)random.Next(128),
                        14 => (byte)random.Next(64),
                        _ => (byte)random.Next(256),
                    };
                    Crtc(register, value);
                    Poke(random.Next(3) * random.Next(40));
                }
                else if (kind < 26)
                {
                    Write(0xFE40, (byte)((random.Next(2) << 3) | (4 + random.Next(2))));
                    Poke(random.Next(3) * random.Next(40));
                }
                else if (kind == 26 && random.Next(60) == 0)
                {
                    bus.BreakReset();
                    oracle.BreakReset();
                }
            }
        }

        Compare();
        Invariant();
        Assert.True(compares > 20, $"only {compares} frames compared");
        Assert.True(invariants > 5_000, $"the flags were checked only {invariants} times");
        Assert.True(flaggedSeen > 0 && unflaggedSeen > 0, $"rows flagged black {flaggedSeen}, not flagged {unflaggedSeen}: one kind never came up");
    }

    private static void Program(Random random, Action<int, byte> crtc, Action<ushort, byte> write)
    {
        int mode = random.Next(8);
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

        // Black-and-content shapes: few displayed rows, RA3-blanked lines, empty width, interlace
        // on and off, skews, short frames.
        if (random.Next(2) == 0) { crtc(6, (byte)random.Next(Math.Max(1, (int)set[6]))); }
        if (random.Next(2) == 0) { crtc(9, (byte)random.Next(16)); }
        if (random.Next(4) == 0) { crtc(1, (byte)random.Next(4)); }
        if (random.Next(2) == 0) { crtc(8, (byte)(new byte[] { 0x00, 0x01, 0x03, 0x10, 0x20, 0x11, 0x21, 0x93 }[random.Next(8)])); }
        if (random.Next(3) == 0) { crtc(4, (byte)random.Next(Math.Max(1, (int)set[4]))); }
        if (random.Next(3) == 0) { crtc(5, (byte)random.Next(32)); }
        crtc(14, (byte)(set[12] + random.Next(0, 9)));
        crtc(15, (byte)random.Next(256));
        if (random.Next(2) == 0)
        {
            crtc(10, (byte)random.Next(8));
            crtc(11, (byte)random.Next(8, 12));
        }
        else
        {
            crtc(10, 0x20);
        }

        byte[] palette = [0x80, 0x90, 0xA0, 0xB0, 0xC0, 0xD0, 0xE0, 0xF0, 0x07, 0x17, 0x27, 0x37, 0x47, 0x57, 0x67, 0x77];
        foreach (byte value in palette)
        {
            write(0xFE21, random.Next(8) == 0 ? (byte)random.Next(256) : value);
        }
    }
}
