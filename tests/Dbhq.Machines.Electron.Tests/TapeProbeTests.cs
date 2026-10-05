using Dbhq.Machines.Electron.Tape;
using Xunit;
using Xunit.Abstractions;
using Piece = Dbhq.Machines.Electron.Tests.TapeProbe.Piece;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The seam the probe sits on, and the OS's <c>SAVE</c> of a one-block program run against
/// <see cref="TapeProbe"/> (task 10). What the SAVE tests assert was found by running the OS and
/// is written into <c>tape.md</c> s7 as <c>[from run]</c>; the numbers they hold it to come from
/// the ROM and the fact sheets, each cited.
/// </summary>
public class TapeProbeTests(ITestOutputHelper output)
{
    /// <summary>One SAVE of <c>10 PRINT "HELLO"</c> as <c>TEST</c>, shared by the tests that read it.</summary>
    internal static readonly Lazy<TapeRuns.SaveRun> Test = new(() => TapeRuns.Save("10 PRINT \"HELLO\"\r", "SAVE \"TEST\"\r"));

    [Fact]
    public void WithNoTapTheCassetteAnswersFe04()
    {
        // Until task 12 nothing was behind $FE04 with no tap, and it read as $FE, the high byte of
        // the address. Now the cassette is: with no tap set, a read gives its receive register.
        var m = new ElectronMachine(ElectronSession.Roms);
        m.InsertTape([new Carrier(20), new TapeByte(0x2A)]);
        m.Bus.Write(0xFE07, 0x40); // input mode, motor on
        long ready = m.Bus.Cycles + (20 * TapeTiming.CarrierCycleCpuCycles) + TapeProbe.ReadyCycles;
        while (m.Bus.Cycles < ready)
        {
            m.Bus.Read(0xC000);
        }

        Assert.Equal(0x2A, m.Bus.Read(0xFE04));
    }

    [Fact]
    public void WithATapSetTheCassetteSeesNothing()
    {
        // The probe stays an independent check on the cassette (task 12): while it is on the seam
        // it takes the cassette's registers, and the cassette neither moves nor records.
        var m = new ElectronMachine(ElectronSession.Roms);
        m.StartRecording();
        var probe = new TapeProbe(m.Bus.Ula);
        m.Bus.Ula.Tap = probe;
        m.Bus.Write(0xFE07, 0x44); // output mode, motor on
        m.Bus.Write(0xFE04, 0x2A);

        Assert.True(probe.MotorOn);
        Assert.False(m.MotorOn);
        Assert.Empty(m.EjectTape());
    }

    [Fact]
    public void TheTapSeesEveryCassetteRegisterAndAnswersFe04()
    {
        var bus = new ElectronBus(ElectronSession.Roms);
        var probe = new TapeProbe(bus.Ula);
        bus.Ula.Tap = probe;

        bus.Write(0xFE04, 0x2A);
        bus.Write(0xFE15, 0x40); // $FE05 in the next 16-byte block: the registers are mirrored (ula.md s1c)
        bus.Write(0xFE06, 0x00);
        bus.Write(0xFE07, 0x44);
        bus.Write(0xFE00, 0x0C); // not a cassette register: not logged
        long before = bus.Cycles;
        bus.Read(0xFE04);

        Assert.Equal([4, 5, 6, 7, 4], probe.Log.Select(a => a.Register));
        Assert.Equal([true, true, true, true, false], probe.Log.Select(a => a.IsWrite));
        Assert.Equal([0x2A, 0x40, 0x00, 0x44], probe.Log.Take(4).Select(a => a.Value));
        Assert.True(probe.Log[^1].Cycle > before);
        Assert.Equal(bus.Cycles, probe.Log[^1].Cycle);
    }

    [Fact]
    public void SaveWritesOneBlockInTheSheetsLayoutAndItsCrcsAreTheSheets()
    {
        TapeRuns.SaveRun run = Test.Value;
        TapeRuns.Block b = Assert.Single(TapeRuns.Blocks(run.Probe.Recorded));

        Assert.Equal("TEST", b.Name);
        Assert.Equal(0, b.Number);
        Assert.Equal(0x80, b.Flag); // the last block, with data, not locked (tape.md s2a)
        Assert.Equal(0u, b.Next);
        Assert.Equal(b.Length, b.Data.Length);
        Assert.Equal(run.Memory(b.Load, b.Length), b.Data);

        // tape.md s3: the header CRC is over the name to the next-file address, the data CRC over
        // the data, both high byte first. This is the proof on a real file that s3 asked for.
        Assert.Equal(TapeCrc.Of(b.HeaderSpan), b.HeaderCrc);
        Assert.Equal(TapeCrc.Of(b.Data), b.DataCrc);
    }

    [Fact]
    public void TheOsMakesCarrierByLeavingTheLineIdleAndSendsTheBlockBackToBack()
    {
        TapeRuns.SaveRun run = Test.Value;
        List<Piece> tape = run.Probe.Recorded;
        List<TapeProbe.Sent> sent = run.Probe.Written;

        // Carrier, the block's bytes, carrier, and nothing else: no $FF leader bytes and no dummy byte.
        Assert.False(tape[0].IsByte);
        Assert.False(tape[^1].IsByte);
        Assert.All(tape.Skip(1).SkipLast(1), p => Assert.True(p.IsByte));
        Assert.Equal(0x2A, sent[0].Value);

        // Every write to $FE04 was made while recording: none was made outside output mode or with the motor off.
        Assert.Equal(sent.Count, run.Probe.Log.Count(a => a.IsWrite && a.Register == 4));

        // Each later byte is written after transmit-empty and inside the stop bit of the byte
        // before (tape.md s4), so it starts on the line the moment that byte ends.
        TapeRuns.AssertLatencies(run, output);
    }

    [Fact]
    public void TheLeaderIs255FieldsAfterReturnAndTheTailIs265()
    {
        // ROM $F7F3: five waits for $0240 to count down from 50 past zero, 51 each, and the
        // display-end handler decrements $0240 once a field ($DBC0). After a file's last block,
        // $F7BF waits 5 x 2 fields and $F7CE five more waits of 51 before the motor goes off.
        TapeRuns.SaveRun run = Test.Value;
        long leader = run.Probe.Written[0].Start - run.ReturnDown;
        long tail = run.MotorOff - (run.Probe.Written[^1].Start + TapeProbe.ByteCycles);

        output.WriteLine($"leader {leader} cycles from RETURN down to the sync byte; tail {tail} cycles from the last byte's end to the motor off; " +
            $"SAVE {run.MotorOff - run.ReturnDown} cycles from RETURN down to the motor off; {run.Probe.Written.Count} bytes");
        Assert.InRange(leader, (255 * TapeRuns.Field) - TapeRuns.Slack, (255 * TapeRuns.Field) + TapeRuns.Slack);
        Assert.InRange(tail, (265 * TapeRuns.Field) - TapeRuns.Slack, (265 * TapeRuns.Field) + TapeRuns.Slack);
    }
}

/// <summary>A <c>*SAVE</c> of 513 bytes under a ten-character name: two full blocks and one of a single byte (task 10).</summary>
public class TapeBlockTests(ITestOutputHelper output)
{
    private const ushort Start = 0x3000;
    private const int Length = 0x201;

    internal static readonly Lazy<TapeRuns.SaveRun> Big = new(() => TapeRuns.Save(
        "",
        "*SAVE \"ABCDEFGHIJ\" 3000 3201\r",
        s =>
        {
            for (int i = 0; i < Length; i++)
            {
                s.Machine.Bus.PokeRam((ushort)(Start + i), (byte)(i * 7));
            }
        }));

    [Fact]
    public void BlocksHold256BytesAndOnlyTheLastIsFlagged()
    {
        TapeRuns.SaveRun run = Big.Value;
        List<TapeRuns.Block> blocks = TapeRuns.Blocks(run.Probe.Recorded);

        Assert.Equal([0, 1, 2], blocks.Select(b => b.Number));
        Assert.Equal([256, 256, 1], blocks.Select(b => b.Length)); // 513 = 2 x 256 + 1; at 255 a block it would be 255, 255, 3
        Assert.Equal([0x00, 0x00, 0x80], blocks.Select(b => (int)b.Flag));
        Assert.All(blocks, b =>
        {
            Assert.Equal("ABCDEFGHIJ", b.Name);
            Assert.Equal(Start, (ushort)b.Load);
            Assert.Equal(0u, b.Next);
            Assert.Equal(TapeCrc.Of(b.HeaderSpan), b.HeaderCrc);
            Assert.Equal(TapeCrc.Of(b.Data), b.DataCrc);
        });
        Assert.Equal(run.Memory(Start, Length), blocks.SelectMany(b => b.Data).ToArray());
    }

    [Fact]
    public void BlocksAreSeparatedBy45FieldsOfIdleLine()
    {
        // ROM $F7BF: 5 x 2 fields after each block; $F764 then $F7F7: 5 x ($C7 + 1) before the
        // next, with $C7 = 6 ($FA88) when no gap has been set: 10 + 35 = 45 fields.
        (List<long> carriers, _) = TapeRuns.Split(Big.Value.Probe.Recorded);
        output.WriteLine("carriers " + string.Join(", ", carriers));
        Assert.Equal(4, carriers.Count);
        Assert.All(carriers.Skip(1).SkipLast(1), gap => Assert.InRange(gap, (45 * TapeRuns.Field) - TapeRuns.Slack, (45 * TapeRuns.Field) + TapeRuns.Slack));
    }

    [Fact]
    public void EveryBlockIsSentBackToBack() => TapeRuns.AssertLatencies(Big.Value, output);

    [Fact]
    public void ANameOfElevenCharactersIsRefusedBeforeTheMotorStarts()
    {
        var s = new ElectronSession().Boot();
        TapeProbe probe = TapeProbe.Attach(s);
        s.Type("SAVE \"ABCDEFGHIJK\"\r").RunUntilPrompt();

        Assert.Contains("Bad string", s.ScreenText().Select(r => r.TrimEnd())); // OS $E870
        Assert.Empty(probe.Written);
        Assert.DoesNotContain(probe.Log, a => a.Register == 7 && (a.Value & 0x40) != 0);
    }
}

/// <summary>
/// <c>LOAD</c> from the tape the OS wrote, whole and broken on purpose (task 10, <c>tape.md</c> s7
/// item 7). The messages are the OS's own strings, at the ROM addresses cited.
/// </summary>
public class TapeLoadTests(ITestOutputHelper output)
{
    /// <summary>
    /// The broken tapes keep the recorded blocks and gaps behind a leader of one second: the OS
    /// needs high tone before the sync byte, not five seconds of it, and the run is shorter.
    /// </summary>
    private const long ShortLeader = 2_000_000;

    [Fact]
    public void TheTapeTheOsWroteLoadsBackAndLists()
    {
        List<Piece> tape = TapeProbeTests.Test.Value.Probe.Recorded;
        var s = new ElectronSession().Boot();
        TapeProbe probe = TapeProbe.Attach(s);
        probe.Insert(tape);

        s.Type("LOAD \"TEST\"");
        long returnDown = s.Machine.Cycles;
        s.Type("\r").RunUntilPrompt(tape.Sum(p => p.Length) + 4_000_000);
        string[] rows = Trimmed(s);
        Assert.Contains("Searching", rows); // OS $F59E
        Assert.Contains("Loading", rows);   // OS $F92A

        // The motor goes off as the block's last byte is read: the tape moved its leader and its
        // bytes, give or take one byte time, and not its trailing carrier.
        Assert.False(probe.MotorOn);
        int lastOn = probe.Log.FindLastIndex(a => a.Register == 7 && (a.Value & 0x40) != 0);
        long motorOff = probe.Log.Skip(lastOn + 1).First(a => a.Register == 7).Cycle;
        output.WriteLine($"LOAD {motorOff - returnDown} cycles from RETURN down to the motor off; tape moved {probe.TapeTime}; " +
            "carriers " + string.Join(", ", TapeRuns.Split(tape).Carriers) + $"; {tape.Count(p => p.IsByte)} bytes");
        long toLastByte = tape.SkipLast(1).Sum(p => p.Length);
        Assert.InRange(probe.TapeTime, toLastByte - TapeProbe.ByteCycles, toLastByte + TapeProbe.ByteCycles);

        s.Type("LIST\r").RunUntilPrompt();
        Assert.Contains("   10 PRINT \"HELLO\"", Trimmed(s));
    }

    /// <summary>
    /// The catalogue line the OS prints for a block (ROM $F82E-$F86D): the name padded to eleven
    /// columns, the block number in hex, and on the last block a space and the file's length in hex.
    /// </summary>
    private static string Catalogue(string name, int block, int? fileLength = null) =>
        name.PadRight(11) + block.ToString("X2") + (fileLength is { } n ? " " + n.ToString("X4") : "");

    [Fact]
    public void ABadHeaderCrcSaysDataAndSearchesAgain() =>
        AssertRewind(
            Broken(TapeProbeTests.Test.Value, b => b.HeaderCrcAt),
            "LOAD \"TEST\"\r",
            ["Searching", "Loading", Catalogue("TEST", 0, 0x10), "Data?", "Rewind tape", "Searching"]);

    [Fact]
    public void ABadDataCrcSaysDataAndSearchesAgain() =>
        AssertRewind(
            Broken(TapeProbeTests.Test.Value, b => b.DataAt),
            "LOAD \"TEST\"\r",
            ["Searching", "Loading", Catalogue("TEST", 0, 0x10), "Data?", "Rewind tape", "Searching"]);

    [Fact]
    public void ABlockOutOfOrderSaysBlockAndSearchesAgain()
    {
        (List<long> carriers, List<byte[]> blocks) = TapeRuns.Split(TapeBlockTests.Big.Value.Probe.Recorded);
        carriers[0] = ShortLeader;
        AssertRewind(
            TapeRuns.Join(carriers, [blocks[0], blocks[2], blocks[1]]),
            "*LOAD \"ABCDEFGHIJ\"\r",
            ["Searching", "Loading", Catalogue("ABCDEFGHIJ", 0), Catalogue("ABCDEFGHIJ", 2, 0x201), "Block?", "Rewind tape", "Searching"]);
    }

    [Fact]
    public void ALockedFileSaysLockedAndLoadsNothing()
    {
        // Flag bit 0 set, and the header CRC made good again with the sheet's CRC (tape.md s3).
        List<Piece> tape = Broken(TapeProbeTests.Test.Value, b => b.FlagAt, fixHeaderCrc: true);
        var s = new ElectronSession().Boot();
        TapeProbe.Attach(s).Insert(tape);

        s.Type("LOAD \"TEST\"\r").RunUntilPrompt(tape.Sum(p => p.Length));
        Assert.Contains("Locked", Trimmed(s)); // OS $F131, error $D5 from $F12D
        s.Type("LIST\r").RunUntilPrompt();
        Assert.DoesNotContain(Trimmed(s), r => r.Contains("PRINT", StringComparison.Ordinal));
    }

    [Fact]
    public void AMissingFileShowsWhatIsThereSearchesOnAndEscapeStopsIt()
    {
        List<Piece> tape = Broken(TapeProbeTests.Test.Value, flip: null);
        var s = new ElectronSession().Boot();
        TapeProbe probe = TapeProbe.Attach(s);
        probe.Insert(tape);

        s.Type("LOAD \"OTHER\"\r");
        TapeRuns.RunUntil(s, rows => rows.Any(r => r.StartsWith("TEST ", StringComparison.Ordinal)), tape.Sum(p => p.Length));
        TapeRuns.RunUntil(s, _ => probe.AtEnd, tape.Sum(p => p.Length));
        s.RunFor(2_000_000);
        string[] rows = Trimmed(s);
        Assert.Equal("Searching", rows[^2]); // no message, no prompt: the file's catalogue line, and the search goes on
        Assert.Equal(Catalogue("TEST", 0, 0x10), rows[^1]);
        Assert.True(probe.MotorOn);

        s.Machine.Keyboard.Down(ElectronKey.Escape);
        s.RunFor(ElectronSession.HoldCycles);
        s.Machine.Keyboard.Up(ElectronKey.Escape);
        s.RunUntilPrompt();
        Assert.Contains("Escape", Trimmed(s)); // OS $F91D
        Assert.False(probe.MotorOn);
    }

    /// <summary>The tape <paramref name="run"/> recorded, behind a short leader, with bit 0 of one byte of its first block flipped if <paramref name="flip"/> picks one.</summary>
    private static List<Piece> Broken(TapeRuns.SaveRun run, Func<TapeRuns.Block, int>? flip, bool fixHeaderCrc = false)
    {
        (List<long> carriers, List<byte[]> blocks) = TapeRuns.Split(run.Probe.Recorded);
        carriers[0] = ShortLeader;
        byte[] first = blocks[0];
        TapeRuns.Block b = TapeRuns.Parse(first);
        if (flip is not null)
        {
            first[flip(b)] ^= 0x01;
        }

        if (fixHeaderCrc)
        {
            ushort crc = TapeCrc.Of(first[1..b.HeaderCrcAt]); // the flipped header no longer parses: it is the span that is wanted
            first[b.HeaderCrcAt] = (byte)(crc >> 8);
            first[b.HeaderCrcAt + 1] = (byte)crc;
        }

        return TapeRuns.Join(carriers, blocks);
    }

    /// <summary>
    /// LOADs the tape until the OS searches a second time, and checks every row from the first
    /// <c>Searching</c> on: the messages are OS $F59E Searching, $F92A Loading, $FA00 Data?,
    /// $FA16 Block? and $FA35 Rewind tape.
    /// </summary>
    private static void AssertRewind(List<Piece> tape, string command, string[] expected)
    {
        var s = new ElectronSession().Boot();
        TapeProbe probe = TapeProbe.Attach(s);
        probe.Insert(tape);

        s.Type(command);
        TapeRuns.RunUntil(s, rows => rows.Count(r => r.TrimEnd() == "Searching") == 2, tape.Sum(p => p.Length));
        string[] rows = Trimmed(s);
        Assert.Equal(expected, rows[Array.IndexOf(rows, "Searching")..]);
        Assert.True(probe.MotorOn);
    }

    private static string[] Trimmed(ElectronSession s) =>
        s.ScreenText().Select(r => r.TrimEnd()).Where(r => r.Length > 0).ToArray();
}

/// <summary>What the probe tests share: a SAVE run, the block layout of <c>tape.md</c> s2a and the CRC of s3.</summary>
internal static class TapeRuns
{
    /// <summary>A field, on average: 39,936 or 40,064 cycles (<c>ula.md</c> s5d), one display-end interrupt each.</summary>
    public const long Field = 40_000;

    /// <summary>
    /// Two fields either way: each of the OS's waits starts partway through a field, and RETURN is
    /// seen at the OS's next keyboard scan, so a count of whole fields is not exact.
    /// </summary>
    public const long Slack = 2 * 40_064;

    /// <summary>One SAVE: the probe, the cycle RETURN went down at the <c>RECORD then RETURN</c> prompt, and the cycle the motor went off.</summary>
    internal sealed record SaveRun(TapeProbe Probe, long ReturnDown, long MotorOff, ElectronBus Bus)
    {
        public byte[] Memory(uint address, int length) =>
            Enumerable.Range(0, length).Select(i => Bus.Peek((ushort)(address + i))).ToArray();
    }

    /// <summary>One block, parsed by the layout of <c>tape.md</c> s2a, with where its fields are in its bytes.</summary>
    internal sealed record Block(
        string Name, uint Load, uint Exec, int Number, int Length, byte Flag, uint Next,
        byte[] HeaderSpan, ushort HeaderCrc, byte[] Data, ushort DataCrc,
        int FlagAt, int HeaderCrcAt, int DataAt);

    /// <summary>Boots, types <paramref name="program"/> and <paramref name="command"/>, presses RETURN at the OS's prompt and runs until BASIC's prompt.</summary>
    public static SaveRun Save(string program, string command, Action<ElectronSession>? setUp = null)
    {
        var s = new ElectronSession().Boot();
        TapeProbe probe = TapeProbe.Attach(s);
        setUp?.Invoke(s);
        if (program.Length > 0)
        {
            s.Type(program).RunUntilPrompt();
        }

        s.Type(command);
        RunUntil(s, rows => rows.Any(r => r.TrimEnd() == "RECORD then RETURN"), 4_000_000); // OS $F8A9
        long returnDown = s.Machine.Cycles;
        s.Type("\r").RunUntilPrompt(60_000_000);

        long lastByte = probe.Written[^1].Cycle;
        long motorOff = probe.Log.First(a => a.Cycle > lastByte && a.Register == 7 && (a.Value & 0x40) == 0).Cycle;
        return new SaveRun(probe, returnDown, motorOff, s.Machine.Bus);
    }

    /// <summary>
    /// The OS's latency after transmit-empty: for each byte after the first in a block, the cycles
    /// from the byte before raising transmit-empty to the OS's write of this one. Each must be
    /// below one bit time, so the write lands in the stop bit and the byte starts as that one ends
    /// (tape.md s5). Prints the least and the most over every block of the run.
    /// </summary>
    public static void AssertLatencies(SaveRun run, ITestOutputHelper output)
    {
        List<TapeProbe.Sent> sent = run.Probe.Written;
        var latencies = new List<long>();
        int first = 0;
        foreach (Block block in Blocks(run.Probe.Recorded))
        {
            int count = BlockBytes(block);
            for (int i = first + 1; i < first + count; i++)
            {
                latencies.Add(sent[i].Cycle - (sent[i - 1].Start + TapeProbe.ReadyCycles));
                Assert.Equal(sent[i - 1].Start + TapeProbe.ByteCycles, sent[i].Start);
            }

            first += count;
        }

        Assert.Equal(sent.Count, first);
        output.WriteLine($"latency after transmit-empty: least {latencies.Min()}, most {latencies.Max()} cycles, over {latencies.Count} writes");
        Assert.All(latencies, l => Assert.InRange(l, 0, TapeProbe.BitCycles - 1));

        static int BlockBytes(Block b) => b.DataAt + b.Length + (b.Length > 0 ? 2 : 0);
    }

    /// <summary>Runs a field at a time until <paramref name="done"/> holds of the screen; fails with the screen if <paramref name="maxCycles"/> pass first.</summary>
    public static void RunUntil(ElectronSession s, Func<string[], bool> done, long maxCycles)
    {
        long end = s.Machine.Cycles + maxCycles;
        while (!done(s.ScreenText()))
        {
            if (s.Machine.Cycles >= end)
            {
                throw new Xunit.Sdk.XunitException("Not within " + maxCycles + " cycles. The screen:\n" + string.Join("\n", s.ScreenText().Select(r => "|" + r.TrimEnd())));
            }

            s.RunFor(Field);
        }
    }

    /// <summary>The carriers and the runs of bytes between them; a run of bytes is one block.</summary>
    public static (List<long> Carriers, List<byte[]> Blocks) Split(IReadOnlyList<Piece> tape)
    {
        var carriers = new List<long>();
        var blocks = new List<byte[]>();
        var bytes = new List<byte>();
        foreach (Piece p in tape)
        {
            if (p.IsByte)
            {
                bytes.Add(p.Value);
                continue;
            }

            if (bytes.Count > 0)
            {
                blocks.Add([.. bytes]);
                bytes.Clear();
            }

            carriers.Add(p.CarrierCycles);
        }

        if (bytes.Count > 0)
        {
            blocks.Add([.. bytes]);
        }

        return (carriers, blocks);
    }

    /// <summary>Carrier, block, carrier, block, ..., carrier: <paramref name="carriers"/> has one more than <paramref name="blocks"/>.</summary>
    public static List<Piece> Join(IReadOnlyList<long> carriers, IReadOnlyList<byte[]> blocks)
    {
        var tape = new List<Piece>();
        for (int i = 0; i < blocks.Count; i++)
        {
            tape.Add(Piece.Carrier(carriers[i]));
            tape.AddRange(blocks[i].Select(Piece.Byte));
        }

        tape.Add(Piece.Carrier(carriers[blocks.Count]));
        return tape;
    }

    public static List<Block> Blocks(IReadOnlyList<Piece> tape) => [.. Split(tape).Blocks.Select(Parse)];

    /// <summary>
    /// A block by <c>tape.md</c> s2a, read by the production <see cref="TapeBlocks.Parse"/> (which
    /// also checks both CRCs and that the bytes are exactly one block), with the places its fields
    /// sit in <paramref name="b"/> and the CRCs as stored, which the tests that break a tape need.
    /// </summary>
    public static Block Parse(byte[] b)
    {
        TapeBlock block = Assert.Single(TapeBlocks.Parse(b));
        int f = 1 + block.Name.Length + 1; // the first byte of the load address: sync, name, zero
        int headerCrcAt = f + 17;
        int dataAt = headerCrcAt + 2;
        int length = block.Data.Length;
        return new Block(
            block.Name,
            block.Load,
            block.Exec,
            block.Number,
            length,
            block.Flag,
            block.Next,
            b[1..headerCrcAt],
            (ushort)((b[headerCrcAt] << 8) | b[headerCrcAt + 1]),
            block.Data,
            length > 0 ? (ushort)((b[dataAt + length] << 8) | b[dataAt + length + 1]) : (ushort)0,
            f + 12,
            headerCrcAt,
            dataAt);
    }
}
