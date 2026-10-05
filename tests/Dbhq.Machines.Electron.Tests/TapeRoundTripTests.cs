using Dbhq.Machines.Electron.Tape;
using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The round trip the design asks for (<c>tape.md</c> s5): the machine's own <c>SAVE</c>, recorded
/// by the cassette and written as a UEF by <see cref="UefWriter"/>, read back by
/// <see cref="UefReader"/> and loaded by a fresh machine. Nothing in the loop is a recording made
/// elsewhere.
/// </summary>
public class TapeRoundTripTests(ITestOutputHelper output)
{
    [Fact]
    public void ASavedProgramLoadsBackAfterAReset()
    {
        TapeRoundTrip.Saved saved = TapeRoundTrip.Hello.Value;

        // The tape image was written by the machine and is read by the repo's own reader, not recorded elsewhere.
        IReadOnlyList<TapeEvent> read = UefReader.Read(saved.Uef);
        Assert.Equal(saved.Events, read);
        IReadOnlyList<TapeBlock> blocks = TapeBlocks.Parse(read.OfType<TapeByte>().Select(b => b.Value));
        Assert.Equal("TEST", blocks[0].Name);                  // tape.md s2a: the OS wrote a valid block
        Assert.All(blocks, b => Assert.NotNull(b.Data));       // Parse has checked both CRCs (s3)
        Assert.NotEqual(0, blocks[^1].Flag & 0x80);            // bit 7: the last block (s2a)

        var t = new ElectronSession().Boot();                  // a fresh machine: nothing carried over
        t.Machine.InsertTape(read);
        t.Type("LOAD \"TEST\"");
        long returnDown = t.Machine.Cycles;
        t.Type("\r");
        TapeRoundTrip.RunUntilMotor(t, on: true, 4_000_000);
        long motorOff = TapeRoundTrip.RunUntilMotor(t, on: false, 20_000_000);
        t.RunUntilPrompt();
        Assert.Equal(0, t.Machine.LostBytes);

        // The motor goes off as the block's last byte is read (tape.md s7 item 9): the tape moved
        // its leader and its bytes, give or take a byte time, and not its trailing carrier.
        long toLastByte = TapeRoundTrip.Length(read.SkipLast(1));
        Assert.InRange(t.Machine.TapePosition, toLastByte - TapeTiming.ByteCpuCycles, toLastByte + TapeTiming.ByteCpuCycles);

        t.Type("LIST\r").RunUntilPrompt();
        Assert.Contains(t.ScreenText(), r => r.Contains("10 PRINT \"HELLO\"", StringComparison.Ordinal));
        t.Type("RUN\r").RunUntilPrompt();
        Assert.Contains(t.ScreenText(), r => r.TrimEnd() == "HELLO");

        long load = motorOff - returnDown;
        output.WriteLine(
            $"SAVE {saved.SaveCycles} cycles, {TapeRoundTrip.Seconds(saved.SaveCycles)} s, from RETURN at RECORD then RETURN to the motor off; " +
            $"LOAD {load} cycles, {TapeRoundTrip.Seconds(load)} s, from RETURN to the motor off; tape moved {t.Machine.TapePosition} cycles; " +
            $"UEF {saved.Uef.Length} bytes; {read.Count(e => e is TapeByte)} bytes on tape; carriers " +
            string.Join(", ", read.OfType<Carrier>().Select(c => c.Cycles)));
    }

    [Fact]
    public void TheCassetteRecordsWhatTheProbeRecorded()
    {
        // The test-only probe of task 10 is kept as an independent check: the same SAVE on the
        // production cassette records the same bytes, and the probe's idle stretches, in CPU
        // cycles, turned into carrier by the rule of tape.md s5 (over a bit time, to the nearest
        // cycle of 832).
        List<TapeEvent> fromProbe = [.. TapeProbeTests.Test.Value.Probe.Recorded
            .Where(p => p.IsByte || p.CarrierCycles > TapeTiming.BitCpuCycles)
            .Select(p => p.IsByte
                ? (TapeEvent)new TapeByte(p.Value)
                : new Carrier((int)Math.Round(p.CarrierCycles / (double)TapeTiming.CarrierCycleCpuCycles, MidpointRounding.AwayFromZero)))];

        Assert.Equal(fromProbe, TapeRoundTrip.Hello.Value.Events);
    }
}

/// <summary>A program of 600 bytes: three blocks, saved, read back from the UEF and loaded by a fresh machine.</summary>
public class TapeLongRoundTripTests
{
    [Fact]
    public void ALongerProgramGoesInThreeBlocksInOrderAndLoadsBack()
    {
        TapeRoundTrip.Saved saved = TapeRoundTrip.Long.Value;
        IReadOnlyList<TapeEvent> read = UefReader.Read(saved.Uef);
        IReadOnlyList<TapeBlock> blocks = TapeBlocks.Parse(read.OfType<TapeByte>().Select(b => b.Value));

        // tape.md s2a and s7 item 3: 256 bytes a block, numbered from 0, bit 7 of the flag on the last only.
        Assert.Equal([0, 1, 2], blocks.Select(b => (int)b.Number));
        Assert.Equal([256, 256, 600 - 512], blocks.Select(b => b.Data.Length));
        Assert.Equal([0x00, 0x00, 0x80], blocks.Select(b => (int)b.Flag));
        Assert.All(blocks, b => Assert.Equal("LONG", b.Name));
        byte[] program = TapeRoundTrip.Memory(saved.Bus, TapeRoundTrip.Page, 600);
        Assert.Equal(program, blocks.SelectMany(b => b.Data).ToArray());

        // Loaded behind a leader of one second rather than five, which the OS does not need.
        List<TapeEvent> tape = [.. read];
        tape[0] = new Carrier(TapeRoundTrip.ShortLeader);
        var t = new ElectronSession().Boot();
        t.Machine.InsertTape(tape);
        t.Type("LOAD \"LONG\"\r").RunUntilPrompt(TapeRoundTrip.Length(tape));
        Assert.Equal(0, t.Machine.LostBytes);
        Assert.Equal(program, TapeRoundTrip.Memory(t.Machine.Bus, TapeRoundTrip.Page, 600));
    }
}

/// <summary>
/// What goes wrong on a tape, and that the machine never hangs (review focus 3). Each run is
/// bounded in cycles, so a hang fails the test. The broken tapes play behind a leader of one second,
/// as the probe's do (task 10), to keep the runs short; the OS needs ten bit times of high tone
/// before a sync byte, not five seconds of it.
/// </summary>
public class TapeFaultTests
{
    /// <summary>Two seconds of machine time: long enough to see a screen that is not changing.</summary>
    private const long Watch = 4_000_000;

    [Fact]
    public void ABadDataCrcSaysDataAndCorruptsNothingBeyondTheBlock()
    {
        // One bit of the block's first data byte flipped, in the events, then through the UEF
        // writer and reader as a user's damaged tape would come.
        TapeRoundTrip.Saved saved = TapeRoundTrip.Hello.Value;
        List<TapeEvent> events = [.. saved.Events];
        events[0] = new Carrier(TapeRoundTrip.ShortLeader);
        byte[] block = [.. events.OfType<TapeByte>().Select(b => b.Value)];
        int dataAt = events.FindIndex(e => e is TapeByte) + TapeRuns.Parse(block).DataAt;
        events[dataAt] = new TapeByte((byte)(((TapeByte)events[dataAt]).Value ^ 0x01));
        IReadOnlyList<TapeEvent> tape = UefReader.Read(UefWriter.Write(events, "a damaged tape"));

        var t = new ElectronSession().Boot();
        int blockLength = TapeRuns.Parse(block).Length;
        int beyond = TapeRoundTrip.Page + blockLength;
        byte[] before = TapeRoundTrip.Memory(t.Machine.Bus, beyond, TapeRoundTrip.ScreenStart - beyond);
        t.Machine.InsertTape(tape);
        t.Type("LOAD \"TEST\"\r");
        TapeRuns.RunUntil(t, rows => rows.Count(r => r.TrimEnd() == "Searching") == 2, TapeRoundTrip.Length(tape));

        // tape.md s7 item 7: the OS's own messages, $F59E Searching, $F92A Loading, $FA00 Data?
        // and $FA35 Rewind tape, and the catalogue line of the block ($F82E-$F86D).
        string[] rows = Trimmed(t);
        Assert.Equal(
            ["Searching", "Loading", "TEST       00 0010", "Data?", "Rewind tape", "Searching"],
            rows[Array.IndexOf(rows, "Searching")..]);
        Assert.True(t.Machine.MotorOn);
        Assert.Equal(before, TapeRoundTrip.Memory(t.Machine.Bus, beyond, TapeRoundTrip.ScreenStart - beyond));
    }

    [Fact]
    public void BreakWhileLoadingStopsTheTapeAndGivesThePrompt()
    {
        var t = StartLoading();
        t.Machine.PressBreak();
        long pressed = t.Machine.Cycles;

        // The ULA keeps its registers on BREAK (ula.md s12 item 5, task 5): what stops the motor is
        // the OS's reset code writing $B4, motor bit clear, to $FE07 at $D96B-$D970 (s10a). A soft
        // BREAK skips the RAM clear, so that write comes within the first field.
        TapeRoundTrip.RunUntilMotor(t, on: false, 40_000);
        Assert.InRange(t.Machine.Cycles - pressed, 1, 40_000);
        t.RunUntilPrompt();
        Assert.False(t.Machine.MotorOn);
    }

    [Fact]
    public void EjectingDuringALoadLeavesTheOsWaitingAndEscapeGivesThePrompt()
    {
        // The OS has no timeout of its own here (tape.md s7 item 12): with the tape gone it waits
        // with the motor on, the screen unchanged, until Escape.
        var t = StartLoading();
        t.Machine.EjectTape();
        AssertWaiting(t);
        Escape(t);
    }

    [Fact]
    public void ASilentTapeLeavesLoadSearchingAndTheScreenUnchanged()
    {
        var t = new ElectronSession().Boot();
        t.Machine.InsertTape([new Silence(20_000_000)]);
        t.Type("LOAD \"TEST\"\r");
        TapeRuns.RunUntil(t, rows => rows.Any(r => r.TrimEnd() == "Searching"), Watch);
        AssertWaiting(t);
        Assert.Equal("Searching", Trimmed(t)[^1]);
        Escape(t);
    }

    [Fact]
    public void TapeAndCatOnAnEmptyTapeDoNotHang()
    {
        var t = new ElectronSession().Boot();
        t.Machine.InsertTape([]);
        t.Type("*TAPE\r").RunUntilPrompt();
        t.Type("*CAT\r");
        AssertWaiting(t);
        Assert.Equal(">*CAT", Trimmed(t)[^1]);
        Escape(t);
    }

    /// <summary>A fresh machine loading the round trip's tape, behind a short leader, as far as <c>Loading</c>.</summary>
    private static ElectronSession StartLoading()
    {
        List<TapeEvent> tape = [.. TapeRoundTrip.Hello.Value.Events];
        tape[0] = new Carrier(TapeRoundTrip.ShortLeader);
        var t = new ElectronSession().Boot();
        t.Machine.InsertTape(tape);
        t.Type("LOAD \"TEST\"\r");
        TapeRuns.RunUntil(t, rows => rows.Any(r => r.TrimEnd() == "Loading"), TapeRoundTrip.Length(tape));
        Assert.True(t.Machine.MotorOn);
        return t;
    }

    /// <summary>For <see cref="Watch"/> cycles the screen does not change and the motor stays on.</summary>
    private static void AssertWaiting(ElectronSession t)
    {
        t.RunFor(ElectronSession.HoldCycles);
        string[] before = t.ScreenText();
        t.RunFor(Watch);
        Assert.Equal(before, t.ScreenText());
        Assert.True(t.Machine.MotorOn);
    }

    /// <summary>Escape gives <c>Escape</c> (OS $F91D) and the prompt within the default bound, with the motor off.</summary>
    private static void Escape(ElectronSession t)
    {
        t.Machine.Keyboard.Down(ElectronKey.Escape);
        t.RunFor(ElectronSession.HoldCycles);
        t.Machine.Keyboard.Up(ElectronKey.Escape);
        t.RunUntilPrompt();
        Assert.Contains("Escape", Trimmed(t));
        Assert.False(t.Machine.MotorOn);
    }

    private static string[] Trimmed(ElectronSession s) =>
        s.ScreenText().Select(r => r.TrimEnd()).Where(r => r.Length > 0).ToArray();
}

/// <summary>What the round-trip tests share: one SAVE of each program, run once.</summary>
internal static class TapeRoundTrip
{
    /// <summary>PAGE on the Electron, where BASIC keeps the program: BASIC's <c>$18</c> holds its high byte.</summary>
    public const ushort Page = 0x0E00;

    /// <summary>Mode 6's screen starts here (ula.md s5a), BASIC's start-up mode; everything below it is the program's and the OS's.</summary>
    public const ushort ScreenStart = 0x6000;

    /// <summary>A leader of one second, in cycles of 2400 Hz: 2,000,000 / 832, rounded down.</summary>
    public const int ShortLeader = 2_000_000 / TapeTiming.CarrierCycleCpuCycles;

    /// <summary>One SAVE: the tape it recorded, that tape as a UEF, the cycles from RETURN to the motor off, and the machine's bus.</summary>
    internal sealed record Saved(IReadOnlyList<TapeEvent> Events, byte[] Uef, long SaveCycles, ElectronBus Bus);

    /// <summary><c>10 PRINT "HELLO"</c>, saved as <c>TEST</c>.</summary>
    public static readonly Lazy<Saved> Hello = new(() => Save("SAVE \"TEST\"\r", s => s.Type("10 PRINT \"HELLO\"\r").RunUntilPrompt()));

    /// <summary>A program of exactly 600 bytes, saved as <c>LONG</c>.</summary>
    public static readonly Lazy<Saved> Long = new(() => Save("SAVE \"LONG\"\r", PutLongProgram));

    public static long Length(IEnumerable<TapeEvent> events) => events.Sum(e => e switch
    {
        Carrier c => (long)c.Cycles * TapeTiming.CarrierCycleCpuCycles,
        TapeByte => TapeTiming.ByteCpuCycles,
        Silence s => s.Cycles,
        _ => throw new ArgumentException(e.ToString()),
    });

    public static string Seconds(long cycles) => (cycles / (double)TapeTiming.CpuHz).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    public static byte[] Memory(ElectronBus bus, int address, int length) =>
        Enumerable.Range(address, length).Select(a => bus.Peek((ushort)a)).ToArray();

    /// <summary>Runs an instruction at a time until the motor is <paramref name="on"/>, and returns the cycle; fails if <paramref name="maxCycles"/> pass first.</summary>
    public static long RunUntilMotor(ElectronSession s, bool on, long maxCycles)
    {
        long end = s.Machine.Cycles + maxCycles;
        while (s.Machine.MotorOn != on)
        {
            if (s.Machine.Cycles >= end)
            {
                throw new Xunit.Sdk.XunitException($"The motor was not {(on ? "on" : "off")} within {maxCycles} cycles.");
            }

            s.Machine.Step();
        }

        return s.Machine.Cycles;
    }

    /// <summary>Boots, sets up the program, records, types <paramref name="command"/> and RETURN at <c>RECORD then RETURN</c> (OS $F8A9), and takes the tape out when the prompt is back.</summary>
    private static Saved Save(string command, Action<ElectronSession> setUp)
    {
        var s = new ElectronSession().Boot();
        setUp(s);
        s.Machine.StartRecording();
        s.Type(command);
        TapeRuns.RunUntil(s, rows => rows.Any(r => r.TrimEnd() == "RECORD then RETURN"), 4_000_000);
        long returnDown = s.Machine.Cycles;
        s.Type("\r");
        long motorOff = RunUntilMotor(s, on: false, 60_000_000);
        s.RunUntilPrompt();
        IReadOnlyList<TapeEvent> events = s.Machine.EjectTape();
        return new Saved(events, UefWriter.Write(events, "dbhq-uk/6502 round trip test"), motorOff - returnDown, s.Machine.Bus);
    }

    /// <summary>
    /// Puts a 600-byte program at PAGE and has BASIC take it with <c>OLD</c>, rather than typing 600
    /// characters. BASIC's layout: each line is <c>$0D</c>, the line number high byte first, a length
    /// that counts from the <c>$0D</c> to the end of the line, and the text; <c>$F4</c> is REM; the
    /// program ends <c>$0D $FF</c>. Three lines of 6 bytes and 194, 194 and 192 letters, and the end:
    /// 18 + 580 + 2 = 600.
    /// </summary>
    private static void PutLongProgram(ElectronSession s)
    {
        var program = new List<byte>();
        foreach ((int number, int letters) in new[] { (10, 194), (20, 194), (30, 192) })
        {
            program.AddRange([0x0D, (byte)(number >> 8), (byte)number, (byte)(6 + letters), 0x20, 0xF4]);
            program.AddRange(Enumerable.Range(0, letters).Select(i => (byte)('A' + (i % 26))));
        }

        program.AddRange([0x0D, 0xFF]);
        Assert.Equal(600, program.Count);
        Assert.Equal(Page >> 8, s.Machine.Bus.Peek(0x18));
        for (int i = 0; i < program.Count; i++)
        {
            s.Machine.Bus.PokeRam((ushort)(Page + i), program[i]);
        }

        s.Type("OLD\r").RunUntilPrompt();
        int top = s.Machine.Bus.Peek(0x12) | (s.Machine.Bus.Peek(0x13) << 8); // BASIC's TOP
        Assert.Equal(Page + 600, top);
    }
}
