using Xunit;
using Dbhq.Cpu6502;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The ULA holding the CPU off RAM while it fetches the display (fact sheet <c>ula.md</c> s4).
/// Every expected number here is from the sheet, s4 and s11, or arithmetic on its rule that
/// anyone can redo; none comes from running the model. The sheet's convention for the phase,
/// which moves exact instants by a cycle and no total (s4f), is the one used: a line starts at
/// <c>T = 0</c>, 1 MHz boundaries are at even <c>T</c>, and the window blocks the boundaries at
/// positions 2 to 80 of a contended line.
/// </summary>
public class ContentionTests
{
    private const long Frame = 80_000;   // s4e, s5d
    private const long Line = 128;       // s4d
    private const long EvenField = 39_936; // s5d: the odd field is 312 lines

    private static readonly byte[] Os = RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256);
    private static readonly byte[] Basic = RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256);

    // Mode, n, and the time the nth access of a pure RAM stream that starts at T = 0 completes
    // (ula.md s11c). Hand check of the first entry: line 0 is contended, so its first access waits
    // for the window and completes at 82, and the 23 that follow take 2 each, so line 0 completes
    // 24 accesses by its end and access 24 completes at 128. Access 6,144 is the 24th of line 255,
    // at 256 x 128 = 32,768; the 56 blank lines left in the odd field add 64 accesses each, so
    // access 9,728 ends with the odd field at 39,936, and 19,520 with the frame (s11c).
    [Theory]
    [InlineData(0, 24, 128)] [InlineData(0, 48, 256)] [InlineData(0, 6144, 32768)] [InlineData(0, 9728, 39936)] [InlineData(0, 9792, 40304)] [InlineData(0, 19520, 80000)]
    [InlineData(1, 24, 128)] [InlineData(1, 48, 256)] [InlineData(1, 6144, 32768)] [InlineData(1, 9728, 39936)] [InlineData(1, 9792, 40304)] [InlineData(1, 19520, 80000)]
    [InlineData(2, 24, 128)] [InlineData(2, 48, 256)] [InlineData(2, 6144, 32768)] [InlineData(2, 9728, 39936)] [InlineData(2, 9792, 40304)] [InlineData(2, 19520, 80000)]
    [InlineData(3, 24, 128)] [InlineData(3, 48, 256)] [InlineData(3, 6144, 24688)] [InlineData(3, 9728, 35456)] [InlineData(3, 9792, 35584)] [InlineData(3, 19520, 70400)]
    [InlineData(4, 24, 48)]  [InlineData(4, 48, 96)]  [InlineData(4, 6144, 12288)] [InlineData(4, 9728, 19456)] [InlineData(4, 9792, 19584)] [InlineData(4, 19520, 39040)]
    [InlineData(5, 24, 48)]  [InlineData(5, 48, 96)]  [InlineData(5, 6144, 12288)] [InlineData(5, 9728, 19456)] [InlineData(5, 9792, 19584)] [InlineData(5, 19520, 39040)]
    [InlineData(6, 24, 48)]  [InlineData(6, 48, 96)]  [InlineData(6, 6144, 12288)] [InlineData(6, 9728, 19456)] [InlineData(6, 9792, 19584)] [InlineData(6, 19520, 39040)]
    public void TheNthAccessCompletesWhenTheSheetSays(int mode, int n, long expected)
    {
        long t = 0;
        for (int i = 0; i < n; i++)
        {
            t = ElectronTiming.Complete(t, AccessKind.Ram, mode);
        }

        // Plus or minus 2: a different phase convention moves each entry by one (s11c).
        Assert.InRange(t, expected - 2, expected + 2);
    }

    // s4e, s11b: a pure RAM stream completes 19,520 accesses a frame in modes 0 to 2 (512
    // contended lines of 24 and 113 free lines of 64), 24,000 in mode 3 (400 contended lines,
    // 225 free) and 40,000, one every 2 cycles, in modes 4 to 6. Plus or minus 1, where the frame
    // boundary cuts an access.
    [Theory]
    [InlineData(0, 19_520)]
    [InlineData(1, 19_520)]
    [InlineData(2, 19_520)]
    [InlineData(3, 24_000)]
    [InlineData(4, 40_000)]
    [InlineData(5, 40_000)]
    [InlineData(6, 40_000)]
    public void APureRamStreamCompletesTheSheetsCountInAFrame(int mode, int expected)
    {
        Assert.InRange(Stream(mode).Count, expected - 1, expected + 1);
    }

    // s4e, s11b: in modes 0 to 2 every contended line has one access of 82 cycles (the one that
    // meets the window) and 23 of 2, so a frame has 512 of 82 and every other access is 2. Mode 3
    // has 400 contended lines, so 400 of 82. Modes 4 to 6 have none longer than 2.
    [Theory]
    [InlineData(0, 512)]
    [InlineData(1, 512)]
    [InlineData(2, 512)]
    [InlineData(3, 400)]
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 0)]
    public void TheHistogramOfAccessLengthsIsTheSheets(int mode, int stalls)
    {
        List<long> lengths = Stream(mode);
        Assert.Equal(stalls, lengths.Count(l => l == 82));
        Assert.All(lengths, l => Assert.True(l is 2 or 82, $"An access of {l} cycles."));
    }

    // s4a: the longest the real clock is held is 41.25 us, a 41.5 us cycle, which is 83 cycles of
    // 2 MHz. A RAM access that starts on a boundary and meets a window takes 82, one that starts a
    // cycle off a boundary 83 (s4e). So the pure RAM stream's longest is 82, and no access takes
    // longer than 83 in a stream where ROM accesses leave RAM accesses starting off a boundary.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheLongestStallIs82Or83Cycles(int mode)
    {
        Assert.Equal(82, Stream(mode).Max());

        long t = 0;
        long longest = 0;
        for (int i = 0; t < Frame; i++)
        {
            // RAM, ROM, RAM, RAM, ROM, ...: the ROM cycles move the next RAM start off a boundary.
            AccessKind kind = i % 3 == 1 ? AccessKind.Rom : AccessKind.Ram;
            long end = ElectronTiming.Complete(t, kind, mode);
            longest = Math.Max(longest, end - t);
            t = end;
        }

        Assert.InRange(longest, 2, 83);

        // The 83: from the last cycle of line 0, position 127, the first boundary at least two
        // cycles on is position 2 of line 1, inside its window, so the access completes at line
        // 1's position 82, 83 cycles after it started.
        Assert.Equal(Line + 82, ElectronTiming.Complete(Line - 1, AccessKind.Ram, mode));
    }

    // s4b: the wait is decided at the boundary. From position 0 of a contended line the first
    // boundary is at position 2, inside the window, so the access waits for position 82; from
    // position 50 it is inside the window already, and it waits for the same boundary.
    [Theory]
    [InlineData(0, 0, 82)]
    [InlineData(0, 50, 82)]
    [InlineData(0, 79, 82)]  // 80 is the last blocked boundary
    [InlineData(0, 80, 82)]  // the next free one is 82
    [InlineData(0, 81, 84)]  // off a boundary: 3 cycles, the window is behind it
    [InlineData(0, 126, 128)] // the last free boundary of the line is 126; 128 is position 0
    [InlineData(3, 0, 82)]
    [InlineData(3, 50, 82)]
    public void AnAccessThatMeetsTheWindowWaitsForItsEnd(int mode, long position, long completes)
    {
        // Line 1 of the odd field and of the even field: both contended in every mode 0 to 3.
        foreach (long lineStart in new[] { Line, EvenField + Line })
        {
            Assert.Equal(lineStart + completes, ElectronTiming.Complete(lineStart + position, AccessKind.Ram, mode));
        }
    }

    // s4b: writes follow the same rule as reads, and the rule is the address kind's alone.
    [Fact]
    public void ARamWriteWaitsLikeARead()
    {
        ElectronBus bus = BusInMode(0);
        RunTo(bus, Line);
        bus.Write(0x1234, 0x56);
        Assert.Equal(Line + 82, bus.Cycles);
    }

    // s4c: mode 3's two blank lines under each 10-line row fetch nothing, so they are not
    // contended. Line 8 and line 9 of each row are free; line 10, the next row's first, is not.
    [Theory]
    [InlineData(7, 82)]
    [InlineData(8, 2)]
    [InlineData(9, 2)]
    [InlineData(10, 82)]
    [InlineData(248, 2)]  // row 24's blank lines
    [InlineData(249, 2)]
    [InlineData(250, 2)]  // below the 250 lines of mode 3
    public void Mode3LeavesTheTwoBlankLinesOfEachRowFree(int line, long cost)
    {
        long start = line * Line;
        Assert.Equal(start + cost, ElectronTiming.Complete(start, AccessKind.Ram, 3));
    }

    // s4c: modes 0 to 2 contend all 256 active lines, and nothing below them.
    [Theory]
    [InlineData(255, 82)]
    [InlineData(256, 2)]
    [InlineData(311, 2)]
    public void Modes0To2ContendTheirTwoHundredAndFiftySixLines(int line, long cost)
    {
        long start = line * Line;
        Assert.Equal(start + cost, ElectronTiming.Complete(start, AccessKind.Ram, 0));
    }

    // s3a, s12 item 2: I/O ($FC00-$FEFF and the keyboard) is 1 MHz and never held up, so a read
    // from position 10 of a contended line completes at position 12.
    [Fact]
    public void IoIsNotBlockedByTheWindow()
    {
        Assert.Equal(Line + 12, ElectronTiming.Complete(Line + 10, AccessKind.Io, 0));
        Assert.Equal(Line + 11, ElectronTiming.Complete(Line + 10, AccessKind.Rom, 0));

        // The same through the bus: the keyboard in slot 8, read in mode 0 at position 10.
        ElectronBus bus = BusInMode(0);
        bus.Write(0xFE05, 0x08);
        RunTo(bus, Line + 10);
        bus.Read(0x8000);
        Assert.Equal(Line + 12, bus.Cycles);
    }

    // A frame boundary does not break the rule (the review's input class: an access that starts
    // in one frame and completes in the next). From 79,999 in mode 0 the first boundary at least
    // two cycles on is 80,002, which is position 2 of line 0 of the next frame's odd field, inside
    // that line's window. So the access waits for position 82 and completes at 80,000 + 82, not
    // at 80,002.
    [Fact]
    public void AnAccessAcrossTheFrameBoundaryMeetsTheNextFramesWindow()
    {
        Assert.Equal(Frame + 82, ElectronTiming.Complete(Frame - 1, AccessKind.Ram, 0));
        // And across the odd field's end into the even field's line 0.
        Assert.Equal(EvenField + 82, ElectronTiming.Complete(EvenField - 1, AccessKind.Ram, 0));
    }

    // s4b, s5d: the ULA's frame. A field starts at T mod 80,000 = 0 (odd) and 39,936 (even); the
    // line is the cycles since the field start over 128, the position the remainder.
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(127, 0, 127)]
    [InlineData(128, 1, 0)]
    [InlineData(39_935, 311, 127)]   // the odd field's last line, 312 lines
    [InlineData(39_936, 0, 0)]       // the even field
    [InlineData(79_999, 312, 127)]   // the even field's last line, 313 lines
    [InlineData(80_000, 0, 0)]
    [InlineData(80_000 + 39_936 + 130, 1, 2)]
    public void FieldAndLineFollowTheSheet(long t, int line, int position)
    {
        Ula.FieldAndLine(t, out int l, out int p);
        Assert.Equal((line, position), (l, p));
    }

    // The ULA's mode when the access starts decides it (s4b): a RAM access after a write of mode
    // 6 is not held, one after a write of mode 0 is.
    [Fact]
    public void TheModeInForceDecidesTheWait()
    {
        ElectronBus bus = BusInMode(6);
        RunTo(bus, Line);
        bus.Read(0x1000);
        Assert.Equal(Line + 2, bus.Cycles);

        bus.Write(0xFE07, 0x00); // mode 0, from position 2: I/O, 2 cycles
        bus.Read(0x1000);        // from position 4: waits for 82
        Assert.Equal(Line + 82, bus.Cycles);
    }

    // s11b, through the real core and bus: SEI, ten NOPs and JMP $2001 in RAM. Each NOP is two RAM
    // accesses (the opcode, then the dummy read of the next byte) and JMP three, 23 a loop. In
    // mode 4 every one takes 2 cycles, so 46 cycles a loop, every loop.
    [Fact]
    public void ThePureRamLoopRuns46CyclesALoopInMode4()
    {
        ElectronBus bus = BusInMode(4);
        bus.PokeRam(0x2000, 0x78); // SEI
        for (int i = 0; i < 10; i++)
        {
            bus.PokeRam((ushort)(0x2001 + i), 0xEA); // NOP
        }

        bus.PokeRam(0x200B, 0x4C); // JMP $2001
        bus.PokeRam(0x200C, 0x01);
        bus.PokeRam(0x200D, 0x20);

        var cpu = new Cpu(bus, CpuVariant.Nmos6502) { PC = 0x2000 };
        cpu.Step(); // SEI
        for (int loop = 0; loop < 2000; loop++)
        {
            long before = bus.Cycles;
            for (int i = 0; i < 11; i++)
            {
                cpu.Step();
            }

            Assert.Equal(0x2001, cpu.PC);
            Assert.Equal(46, bus.Cycles - before);
        }
    }

    /// <summary>Wraps the bus and writes down every access the core makes, with the cycles it took.</summary>
    private sealed class RecordingBus(ElectronBus inner) : IBus
    {
        public List<(ushort Address, bool IsWrite, long Cost)> Accesses { get; } = [];

        public byte Read(ushort address)
        {
            long before = inner.Cycles;
            byte value = inner.Read(address);
            Accesses.Add((address, false, inner.Cycles - before));
            return value;
        }

        public void Write(ushort address, byte value)
        {
            long before = inner.Cycles;
            inner.Write(address, value);
            Accesses.Add((address, true, inner.Cycles - before));
        }
    }

    // A read-modify-write's writes reach the bus and are judged like any other access (s3a, s4b).
    // NMOS INC abs makes six accesses (64doc, M. Makela and J. West, "absolute addressing,
    // read-modify-write instructions"): 1 the opcode, 2 the address low, 3 the address high, 4 the
    // read of the target, 5 the write back of the old value, 6 the write of the new.
    //
    // In mode 0 the instruction starts at position 118 of contended line 1. The first five
    // accesses are RAM from even times, 2 cycles each, and end at 256, position 0 of line 2. The
    // sixth, the final write, starts there; its first boundary is position 2, inside the window, so
    // it waits for position 82: 82 cycles, and 92 for the instruction.
    [Fact]
    public void AReadModifyWritesWritesAreHeldLikeAnyOtherRamAccess()
    {
        ElectronBus bus = BusInMode(0);
        bus.PokeRam(0x2000, 0xEE); // INC $1234
        bus.PokeRam(0x2001, 0x34);
        bus.PokeRam(0x2002, 0x12);
        bus.PokeRam(0x1234, 0x41);
        RunTo(bus, Line + 118);

        var recorder = new RecordingBus(bus);
        var cpu = new Cpu(recorder, CpuVariant.Nmos6502) { PC = 0x2000 };
        cpu.Step();

        Assert.Equal(
            new (ushort, bool, long)[]
            {
                (0x2000, false, 2), (0x2001, false, 2), (0x2002, false, 2),
                (0x1234, false, 2), (0x1234, true, 2), (0x1234, true, 82),
            },
            recorder.Accesses.ToArray());
        Assert.Equal(0x42, bus.Peek(0x1234));
        Assert.Equal((2 * Line) + 82, bus.Cycles);
    }

    // The same instruction with its target in the OS ROM, in mode 6: the opcode and the address
    // are RAM, 2 cycles each from a boundary, and the read and both writes are ROM, 1 cycle each,
    // because each access is judged by its own address (s3a). The write to ROM goes nowhere.
    [Fact]
    public void EachAccessOfAReadModifyWriteIsJudgedByItsOwnAddress()
    {
        ElectronBus bus = BusInMode(6);
        bus.PokeRam(0x2000, 0xEE); // INC $C000
        bus.PokeRam(0x2001, 0x00);
        bus.PokeRam(0x2002, 0xC0);

        var recorder = new RecordingBus(bus);
        var cpu = new Cpu(recorder, CpuVariant.Nmos6502) { PC = 0x2000 };
        cpu.Step();

        Assert.Equal(
            new (ushort, bool, long)[]
            {
                (0x2000, false, 2), (0x2001, false, 2), (0x2002, false, 2),
                (0xC000, false, 1), (0xC000, true, 1), (0xC000, true, 1),
            },
            recorder.Accesses.ToArray());
        Assert.Equal(Os[0], bus.Peek(0xC000));
    }

    /// <summary>
    /// The lengths of the accesses of a pure RAM stream that starts at <c>T = 0</c>, for each access
    /// that completes within the first frame.
    /// </summary>
    private static List<long> Stream(int mode)
    {
        var lengths = new List<long>();
        long t = 0;
        while (true)
        {
            long end = ElectronTiming.Complete(t, AccessKind.Ram, mode);
            if (end > Frame)
            {
                return lengths;
            }

            lengths.Add(end - t);
            t = end;
        }
    }

    /// <summary>A bus with the ULA set to <paramref name="mode"/> by a write to $FE07 (s5a), which takes cycles 0 to 2.</summary>
    private static ElectronBus BusInMode(int mode)
    {
        var bus = new ElectronBus(new ElectronRoms(Os, Basic));
        bus.Write(0xFE07, (byte)(mode << 3));
        Assert.Equal(mode, bus.Ula.Mode);
        return bus;
    }

    /// <summary>Moves the clock to <paramref name="t"/> exactly by reading the OS ROM, one cycle a read (s3a).</summary>
    private static void RunTo(ElectronBus bus, long t)
    {
        Assert.True(bus.Cycles <= t);
        while (bus.Cycles < t)
        {
            bus.Read(0xC000);
        }
    }
}
