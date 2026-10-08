using System.Reflection;
using Dbhq.Machines.Nes.Mappers;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The state reports and the observer that the differential (<c>bench/nes-speed/differential</c>)
/// rests on, which is the gate for the lazy chips (<c>docs/superpowers/plans/2026-10-06-nes-lazy-chips.md</c>):
/// every field of every chip is reported or skipped with its reason, and the bus tells the
/// observer of each point where a chip can be seen.
/// </summary>
public class StateReportTests
{
    [Theory]
    [MemberData(nameof(Boards))]
    public void EveryFieldOfEveryPartIsReportedOrSkipped(int mapper, bool chrRam)
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Banked(mapper, prgBanks: 2, chrBanks: chrRam ? 0 : 2)));
        nes.PowerOn();

        var (problems, checkedTypes) = StateCompleteness.Check(nes.Bus);

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        foreach (Type part in new[] { typeof(NesBus), typeof(Ppu), typeof(FrameBuffer), typeof(Apu), typeof(PulseChannel), typeof(TriangleChannel), typeof(NoiseChannel), typeof(DmcChannel), typeof(LengthCounter), typeof(Envelope), typeof(SampleBuffer), typeof(Controller) })
        {
            Assert.Contains(part, checkedTypes);
        }

        Assert.Contains(BoardType(mapper), checkedTypes);
    }

    public static TheoryData<int, bool> Boards()
    {
        var data = new TheoryData<int, bool>();
        foreach (int mapper in Cartridge.SupportedMappers)
        {
            data.Add(mapper, false);
            data.Add(mapper, true);
        }

        return data;
    }

    [Fact]
    public void ObservationIsSwitchedOnForTheTests()
    {
        // The project file sets the runtime option; without it the observer is never called.
        Assert.True(NesBus.Observable);
    }

    [Fact]
    public void TheObserverIsToldOfRegisterAccessesBoardWritesFrameEndsAndEveryCycle()
    {
        // LDA $2002, STA $4000, STA $8000, BIT $4016, then JMP to itself.
        byte[] program = [0xAD, 0x02, 0x20, 0x8D, 0x00, 0x40, 0x8D, 0x00, 0x80, 0x2C, 0x16, 0x40, 0x4C, 0x0C, 0x80];
        var nes = Machine(program);
        var observer = new Recorder();
        nes.Bus.Observer = observer;
        nes.PowerOn();
        Assert.Equal([true], observer.Resets);
        Assert.Equal(nes.Bus.Cycles, observer.Cycles);
        for (int i = 0; i < 4; i++)
        {
            nes.Step();
        }

        Assert.Equal(nes.Bus.Cycles, observer.Cycles);
        Assert.Equal(new[] { "read $2002", "write $4000", "write $8000", "read $4016" }, observer.Accesses);
        Assert.Equal(0, observer.Frames);
        nes.RunFrames(2);
        Assert.Equal(2, observer.Frames);
        Assert.Equal(nes.Bus.Cycles, observer.Cycles);
        nes.Reset();
        Assert.Equal([true, false], observer.Resets);
    }

    [Fact]
    public void TheObserverIsToldOfEachDmcFetch()
    {
        // A one-byte sample from $C000: $4012 = 0, $4013 = 0, then $4015 = $10 starts it.
        byte[] program = [0xA9, 0x00, 0x8D, 0x12, 0x40, 0x8D, 0x13, 0x40, 0xA9, 0x10, 0x8D, 0x15, 0x40, 0x4C, 0x0D, 0x80];
        var nes = Machine(program);
        var observer = new Recorder();
        nes.Bus.Observer = observer;
        nes.PowerOn();
        nes.Run(200);

        Assert.Equal(1, observer.DmcFetches);
    }

    [Fact]
    public void TheObserverIsNoneUntilOneIsSetAndThePpuHasTheBussOwn()
    {
        var nes = Machine([0x4C, 0x00, 0x80]);
        Assert.Null(nes.Bus.Observer);
        nes.PowerOn();
        nes.RunFrames(1);
        Assert.Null(nes.Bus.Ppu.Observer);
        var observer = new Recorder();
        nes.Bus.Observer = observer;
        Assert.Same(observer, nes.Bus.Ppu.Observer);
    }

    [Fact]
    public void AnOamDmaIsToldAsItsWritesTo2004()
    {
        // LDA #$02, STA $4014, then JMP to itself.
        var nes = Machine([0xA9, 0x02, 0x8D, 0x14, 0x40, 0x4C, 0x05, 0x80]);
        var observer = new Recorder();
        nes.Bus.Observer = observer;
        nes.PowerOn();
        nes.Run(600);

        Assert.Equal("write $4014", observer.Accesses[0]);
        Assert.Equal(256, observer.Accesses.Count(access => access == "write $2004"));
        Assert.Equal(257, observer.Accesses.Count);
    }

    [Fact]
    public void ReadsOfTheBoardAndPrgRamAccessesAreNotTold()
    {
        // LDA $8000, STA $6000, LDA $6000, STA $7FFF, then JMP to itself: every fetch is a read
        // of $8000 and up too.
        var nes = Machine([0xAD, 0x00, 0x80, 0x8D, 0x00, 0x60, 0xAD, 0x00, 0x60, 0x8D, 0xFF, 0x7F, 0x4C, 0x0C, 0x80]);
        var observer = new Recorder();
        nes.Bus.Observer = observer;
        nes.PowerOn();
        nes.Run(200);

        Assert.Empty(observer.Accesses);
    }

    // The reports are complete by value, not only by name: each reported field, changed alone,
    // changes the report, and put back, puts it back.
    [Theory]
    [MemberData(nameof(Boards))]
    public void ChangingAnyReportedFieldChangesTheReport(int mapper, bool chrRam)
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Banked(mapper, prgBanks: 2, chrBanks: chrRam ? 0 : 2)));
        nes.PowerOn();
        nes.RunFrames(3);
        IReportsState root = nes.Bus;
        List<ulong> before = Values(root);
        var problems = new List<string>();
        int changed = 0;
        foreach (IReportsState part in Parts(root))
        {
            var names = new Reasons();
            part.ReportState(names);
            foreach (string name in names.Reported.Distinct())
            {
                FieldInfo field = FieldOf(part.GetType(), name);
                object? value = field.GetValue(part);

                // An array is changed at its first element and, apart, at its last, so a report
                // that leaves out the end of an array fails as well as one that leaves out its start.
                int[] elements = value is Array { Length: > 1 } array && !IsMapped(part, field) ? [0, array.Length - 1] : [0];
                foreach (int element in elements)
                {
                    string what = elements.Length > 1 ? $"{name}[{element}]" : name;
                    if (!Perturb(part, field, value, element))
                    {
                        problems.Add($"{part.GetType().Name}.{what}: no way to change it ({field.FieldType.Name})");
                        continue;
                    }

                    if (Values(root).SequenceEqual(before))
                    {
                        problems.Add($"{part.GetType().Name}.{what}: changed, and the report did not change");
                    }

                    Restore(part, field, value, element);
                    changed++;
                    if (!Values(root).SequenceEqual(before))
                    {
                        problems.Add($"{part.GetType().Name}.{what}: put back, and the report did not come back");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.True(changed > 150, $"only {changed} fields were changed");
    }

    // Every field skipped as fixed is one the class cannot change after it is made.
    [Theory]
    [MemberData(nameof(Boards))]
    public void EveryFieldSkippedAsFixedIsReadOnly(int mapper, bool chrRam)
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Banked(mapper, prgBanks: 2, chrBanks: chrRam ? 0 : 2)));
        var problems = new List<string>();
        foreach (IReportsState part in Parts(nes.Bus))
        {
            var names = new Reasons();
            part.ReportState(names);
            foreach (string name in names.Fixed)
            {
                if (!FieldOf(part.GetType(), name).IsInitOnly)
                {
                    problems.Add($"{part.GetType().Name}.{name} is skipped as fixed and is not read-only");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    // The fields skipped as another part's own are that part's: the PPU's and the bus's copies of
    // the board's memory, windows and layout, and the PPU's picture.
    [Theory]
    [MemberData(nameof(Boards))]
    public void EveryFieldSkippedAsAnothersIsTheSameObject(int mapper, bool chrRam)
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Banked(mapper, prgBanks: 2, chrBanks: chrRam ? 0 : 2)));
        NesBus bus = nes.Bus;
        Ppu ppu = bus.Ppu;
        object board = Get(bus, "_mapper")!;
        Assert.Same(board, Get(ppu, "_mapper"));
        Assert.Same(ppu.Screen.Pixels, Get(ppu, "_pixels"));
        Assert.Same(((IMapper)board).NametablePageTable, Get(ppu, "_nametablePages"));
        if (board is Board)
        {
            Assert.Same(Get(board, "Chr"), Get(ppu, "_chr"));
            Assert.Same(Get(board, "ChrBase"), Get(ppu, "_chrWindows"));
            Assert.Same(Get(board, "Prg"), Get(bus, "_prg"));
            Assert.Same(Get(board, "PrgBase"), Get(bus, "_prgWindows"));
        }
        else
        {
            // NROM keeps no windows: the PPU's and the bus's are made once, in order, and never written.
            Assert.IsType<Nrom>(board);
            Assert.Same(Get(board, "_chr"), Get(ppu, "_chr"));
            Assert.Same(Get(board, "_prg"), Get(bus, "_prg"));
            Assert.Equal(new[] { 0x0000, 0x0400, 0x0800, 0x0C00, 0x1000, 0x1400, 0x1800, 0x1C00 }, (int[])Get(ppu, "_chrWindows")!);
            Assert.Equal(new[] { 0x0000, 0x2000, 0x4000 % ((byte[])Get(board, "_prg")!).Length, 0x6000 % ((byte[])Get(board, "_prg")!).Length }, (int[])Get(bus, "_prgWindows")!);
        }
    }

    // Every part under the root, each once.
    private static List<IReportsState> Parts(IReportsState root)
    {
        var parts = new List<IReportsState>();
        var queue = new Queue<IReportsState>([root]);
        while (queue.Count > 0)
        {
            IReportsState part = queue.Dequeue();
            if (parts.Any(p => ReferenceEquals(p, part)))
            {
                continue;
            }

            parts.Add(part);
            var names = new Reasons();
            part.ReportState(names);
            foreach (IReportsState child in names.Parts)
            {
                queue.Enqueue(child);
            }
        }

        return parts;
    }

    // Every value the root's report gives, the parts' in place.
    private static List<ulong> Values(IReportsState root)
    {
        var sink = new ValueSink();
        root.ReportState(sink);
        return sink.Values;
    }

    private static FieldInfo FieldOf(Type type, string name)
    {
        int end = name.IndexOfAny([' ', '[']);
        name = end < 0 ? name : name[..end];
        for (Type? t = type; t is not null; t = t.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo? field = t.GetField(name, flags) ?? t.GetField($"<{name}>k__BackingField", flags);
            if (field is not null)
            {
                return field;
            }
        }

        throw new InvalidOperationException($"{type.Name} has no field {name}");
    }

    private static object? Get(object target, string name)
    {
        FieldInfo? field = null;
        for (Type? t = target.GetType(); t is not null && field is null; t = t.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            field = t.GetField(name, flags) ?? t.GetField($"<{name}>k__BackingField", flags);
        }

        return field is not null ? field.GetValue(target) : target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(target);
    }

    // The two fields reported in a mapped form, which Perturb changes in their own way.
    private static bool IsMapped(object part, FieldInfo field) =>
        (part is Apu && field.Name is "_steps" or "_actions") || (part is SampleBuffer && field.Name == "_ring");

    // Changes the field's value, or the given element of an array, in place; false if it cannot.
    private static bool Perturb(object part, FieldInfo field, object? value, int element)
    {
        // The two fields reported in a mapped form: which frame counter table is in use (the
        // other table), and the sample ring, of which the waiting samples are reported (the first).
        if (part is Apu && field.Name is "_steps" or "_actions")
        {
            string other = field.Name == "_steps"
                ? (ReferenceEquals(value, Get(part, "_fiveStep")) ? "_fourStep" : "_fiveStep")
                : (ReferenceEquals(value, Get(part, "FiveStepActions")) ? "FourStepActions" : "FiveStepActions");
            field.SetValue(part, Get(part, other));
            return true;
        }

        if (part is SampleBuffer && field.Name == "_ring")
        {
            var ring = (float[])value!;
            int read = (int)Get(part, "_read")!;
            if ((int)Get(part, "_count")! == 0)
            {
                return false;
            }

            ring[read] = (float)Changed(ring[read]);
            return true;
        }

        switch (value)
        {
            case Array array when array.Length > element:
                array.SetValue(Changed(array.GetValue(element)!), element);
                return true;
            case Array:
                return false;
            case null:
                return false;
            default:
                field.SetValue(part, Changed(value));
                return true;
        }
    }

    private static void Restore(object part, FieldInfo field, object? value, int element)
    {
        if (part is SampleBuffer && field.Name == "_ring")
        {
            var ring = (float[])value!;
            int read = (int)Get(part, "_read")!;
            ring[read] = (float)Changed(ring[read]);
            return;
        }

        if (value is Array array && !(part is Apu && field.Name is "_steps" or "_actions"))
        {
            array.SetValue(Changed(array.GetValue(element)!), element);
            return;
        }

        field.SetValue(part, value);
    }

    // A different value of the same type: the low bit flipped (a float's or a double's lowest bit
    // of its fraction), so that changing it again gives it back; or for an enum, the next value.
    private static object Changed(object value) => value switch
    {
        bool b => !b,
        byte b => (byte)(b ^ 1),
        ushort u => (ushort)(u ^ 1),
        int i => i ^ 1,
        uint u => u ^ 1,
        long l => l ^ 1,
        ulong u => u ^ 1,
        float f => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(f) ^ 1),
        double d => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(d) ^ 1),
        Mirroring m => (Mirroring)(((int)m + 1) % Enum.GetValues<Mirroring>().Length),
        _ => throw new InvalidOperationException($"no way to change a {value.GetType().Name}"),
    };

    // NROM with the program at $8000 and the reset vector pointing at it.
    private static Nes Machine(byte[] program)
    {
        byte[] file = TestCartridge.Ines1(prgBanks: 2, chrBanks: 1);
        program.CopyTo(file, 16);
        file[16 + 0x7FFC] = 0x00;
        file[16 + 0x7FFD] = 0x80;
        return new Nes(Cartridge.Load(file));
    }

    private static Type BoardType(int mapper) => mapper switch
    {
        0 => typeof(Nrom),
        1 => typeof(Mmc1),
        2 => typeof(Uxrom),
        3 => typeof(Cnrom),
        4 => typeof(Mmc3),
        _ => typeof(Axrom),
    };

    // The names a report gives: those with values (not the parts), the parts, and those skipped as fixed.
    private sealed class Reasons : IStateSink
    {
        public List<string> Reported { get; } = [];

        public List<string> Fixed { get; } = [];

        public List<IReportsState> Parts { get; } = [];

        public void Add(string name, long value) => Reported.Add(name);

        public void Add(string name, ulong value) => Reported.Add(name);

        public void Add(string name, bool value) => Reported.Add(name);

        public void Add(string name, double value) => Reported.Add(name);

        public void Add(string name, ReadOnlySpan<byte> values) => Reported.Add(name);

        public void Add(string name, ReadOnlySpan<int> values) => Reported.Add(name);

        public void Add(string name, ReadOnlySpan<uint> values) => Reported.Add(name);

        public void Add(string name, ReadOnlySpan<long> values) => Reported.Add(name);

        public void Add(string name, ReadOnlySpan<float> values) => Reported.Add(name);

        public void Add(string name, ReadOnlySpan<double> values) => Reported.Add(name);

        public void Add(string name, IReportsState part) => Parts.Add(part);

        public void Skip(string name, string why)
        {
            if (why == StateReport.Fixed)
            {
                Fixed.Add(name);
            }
        }
    }

    // Every value of a report and of its parts, in order, each word as it is.
    private sealed class ValueSink : IStateSink
    {
        public List<ulong> Values { get; } = [];

        public void Add(string name, long value) => Values.Add((ulong)value);

        public void Add(string name, ulong value) => Values.Add(value);

        public void Add(string name, bool value) => Values.Add(value ? 1UL : 0);

        public void Add(string name, double value) => Values.Add(BitConverter.DoubleToUInt64Bits(value));

        public void Add(string name, ReadOnlySpan<byte> values) => AddAll(values.ToArray().Select(v => (ulong)v));

        public void Add(string name, ReadOnlySpan<int> values) => AddAll(values.ToArray().Select(v => (ulong)(uint)v));

        public void Add(string name, ReadOnlySpan<uint> values) => AddAll(values.ToArray().Select(v => (ulong)v));

        public void Add(string name, ReadOnlySpan<long> values) => AddAll(values.ToArray().Select(v => (ulong)v));

        public void Add(string name, ReadOnlySpan<float> values) => AddAll(values.ToArray().Select(v => (ulong)BitConverter.SingleToUInt32Bits(v)));

        public void Add(string name, ReadOnlySpan<double> values) => AddAll(values.ToArray().Select(BitConverter.DoubleToUInt64Bits));

        public void Add(string name, IReportsState part) => part.ReportState(this);

        public void Skip(string name, string why)
        {
        }

        private void AddAll(IEnumerable<ulong> values)
        {
            List<ulong> list = [.. values];
            Values.Add((ulong)list.Count);
            Values.AddRange(list);
        }
    }

    private sealed class Recorder : INesObserver
    {
        public List<string> Accesses { get; } = [];

        public long Cycles { get; private set; }

        public int Frames { get; private set; }

        public int DmcFetches { get; private set; }

        public void Accessed(ushort address, bool write, byte value)
        {
            if (Frames == 0)
            {
                Accesses.Add($"{(write ? "write" : "read")} ${address:X4}");
            }
        }

        public void DmcFetched() => DmcFetches++;

        public void FrameEnded() => Frames++;

        public void CycleEnded(bool nmi, bool irq) => Cycles++;

        public List<bool> Resets { get; } = [];

        public void ChipsReset(bool power) => Resets.Add(power);
    }
}
