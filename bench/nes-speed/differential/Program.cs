using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Dbhq.Cpu6502.TestSupport;
using Dbhq.Machines.Nes;
using Dbhq.Machines.Nes.Tests;

// The NES differential check, for work that must not change behaviour, such as making the machine
// faster or its chips lazy: every pinned NES test ROM, the bundled homebrew with a fixed round of
// button presses, and the synthetic cartridges assembled here (Synthetic.cs), which keep rendering
// on and touch the chips at dots that move frame by frame, are run in both regions, and for each
// the program writes one line of hashes of what the machine did. Two builds that behave the same
// write the same file.
//
//   dotnet run -c Release --project bench/nes-speed/differential -- <output file> [frames] [options]
//   dotnet run -c Release --project bench/nes-speed/differential -- --check <baseline file> [frames] [options] [--out <file>]
//       options: --oracle, --only <text>, --threads <n> (4 by default), --coverage
//
// The first writes the file. The second runs, compares with a baseline file written by the first
// (bench/nes-speed/differential/baseline/), prints the first run that differs and which of its
// hashes do, and exits 1 on any difference, 0 when every line is the same. --oracle builds the
// machine with NesOptions.PerDotReference, the per-dot reference of the lazy chips; --only runs
// the ROMs whose name contains the text, and --check then compares those lines alone; --threads
// runs that many at once, which changes no hash; --coverage prints, for each synthetic run, the
// dots its PPU register writes landed on with rendering on, which changes no hash either.
//
// Each run powers on, runs the frames given (150 by default; six times that for SNOW and the MMC3
// ROMs, whose work is in the picture and the scanline counter; four times for the homebrew and the
// synthetic cartridges), and presses reset once half way. The file's first line names the format and the frames; then one
// line a run, "<rom> <region>: " and these, in this order:
//
//   steps, cycles  instructions run and CPU cycles at the end
//   trace          after every instruction, the CPU's registers, the cycle count and the PPU's line
//                  and dot (the logical position, which a lazy PPU reports without catching up);
//                  at the end of every frame, all the pixels, v, t, fine X and the dot count, OAM
//                  and the interrupt lines
//   sound          the samples made, read after every frame
//   dropped        samples the buffer dropped
//   memory         at the end: RAM, what the PPU's registers would read, VRAM, OAM and the lines
//
// Those six are hashed as the first version of this tool hashed them (FNV-1a over 64-bit words),
// so its files and these agree on them. The rest are new in format 2, hashed with each word mixed
// first so that two differences cannot cancel:
//
//   cpu      the per-instruction words of trace again, in the stronger hash
//   points   how many observation points there were (below)
//   ppu      at each point where the PPU can be seen, the point, the logical position and the
//            PPU's whole state as it reports it (Ppu.ReportState): each CPU access to $2000-$3FFF,
//            OAM DMA's writes among them, and each write to the board's registers ($8000-$FFFF),
//            with the picture left out; each frame end, from inside the dot that ends it, with the
//            picture; and power on and the reset button
//   apu      the same for the sound unit and the sample buffer: each CPU access to $4000-$401F,
//            each DMC fetch, after the samples are read at each frame's end, power on and reset
//   board    at each of those points but the frame ends, the bus's own state, the controllers and
//            the board's (RAM, the open bus, the DMA and the pads, the board's registers, PRG RAM
//            and CHR RAM)
//   lines    the NMI and IRQ lines the CPU is given, every cycle
//
// At a point where one chip can be seen the other is not hashed, because a lazy build need not
// have caught it up; at a frame end only the PPU and the cycle count are, because the frame can end
// before or after the cycle's access. What each report holds, and why each field it leaves out is
// left out, is in the chips' ReportState methods and in docs/journal/2026-10-06-the-nes-lazy-chips.md.
// Before anything runs, the program checks by reflection that every field of every chip is
// reported or skipped (StateCompleteness, shared with StateReportTests), and stops if one is not.
// The ROMs come from the pinned fork, checked against their hashes (NesTestRoms.Read), and are
// never committed; the homebrew is the committed one, checked against its hash; the synthetic
// cartridges are made from bytes each run, the same each time.
const string Format = "# nes-differential format 2";

string? outFile = null;
string? baseline = null;
string? only = null;
int frames = 150;
bool oracle = false;
bool coverage = false;
int threads = 4;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--check":
            baseline = args[++i];
            break;
        case "--out":
            outFile = args[++i];
            break;
        case "--only":
            only = args[++i];
            break;
        case "--oracle":
            oracle = true;
            break;
        case "--coverage":
            coverage = true;
            break;
        case "--threads":
            threads = int.Parse(args[++i]);
            break;
        default:
            if (int.TryParse(args[i], out int n))
            {
                frames = n;
            }
            else if (outFile is null && baseline is null)
            {
                outFile = args[i];
            }
            else
            {
                throw new ArgumentException($"what is {args[i]}? usage: <output file> [frames] [--oracle] [--only <text>], or --check <baseline file> [frames] [--oracle] [--only <text>] [--out <file>]");
            }

            break;
    }
}

if (outFile is null && baseline is null)
{
    throw new ArgumentException("usage: <output file> [frames] [--oracle] [--only <text>], or --check <baseline file> [frames] [--oracle] [--only <text>] [--out <file>]");
}

if (!NesBus.Observable)
{
    Console.Error.WriteLine("the bus's observer is off: the project file's RuntimeHostConfigurationOption Dbhq.Machines.Nes.Observable is missing");
    return 2;
}

// Read first, one at a time, so the downloads of a first run do not race.
var jobs = Pins.NesTestRomHashes.Keys.Order(StringComparer.Ordinal)
    .Select(name => new Job(name, NesTestRoms.Read(name), name.Contains("snow", StringComparison.Ordinal) || name.Contains("mmc3", StringComparison.Ordinal) ? 6 : 1, Scripted: false))
    .Append(new Job("homebrew/" + Path.GetFileName(Pins.NesHomebrewPath), RepoPaths.ReadChecked(Pins.NesHomebrewPath, Pins.NesHomebrewSha256), 4, Scripted: true))
    .Concat(Synthetic.Jobs().Select(job => new Job(job.Name, job.Bytes, Synthetic.Times, Scripted: false)))
    .Where(job => only is null || job.Name.Contains(only, StringComparison.Ordinal))
    .ToList();

// Every field reported: each board the ROMs use, and the rest of the machine with the first.
var problems = new SortedSet<string>(StringComparer.Ordinal);
var boards = new HashSet<Type>();
foreach (Job job in jobs)
{
    try
    {
        var nes = new Nes(Cartridge.Load(job.Bytes), Region.Ntsc);
        if (boards.Add(BoardOf(nes)))
        {
            problems.UnionWith(StateCompleteness.Check(nes.Bus).Problems);
        }
    }
    catch (NesFormatException)
    {
    }
}

if (problems.Count > 0)
{
    Console.Error.WriteLine("the state reports leave fields out, so the hashes would not cover them:");
    foreach (string problem in problems)
    {
        Console.Error.WriteLine("  " + problem);
    }

    return 2;
}

var results = new ConcurrentDictionary<string, string>();
var runs = jobs.SelectMany(job => new[] { (Job: job, Region: Region.Ntsc), (Job: job, Region: Region.Pal) });
var dots = new ConcurrentDictionary<string, string>();
Parallel.ForEach(runs, new ParallelOptions { MaxDegreeOfParallelism = threads }, run =>
{
    string key = $"{run.Job.Name} {run.Region.Name}";
    try
    {
        results[key] = Run(run.Job, run.Region, frames, oracle, coverage && run.Job.Name.StartsWith("synthetic/", StringComparison.Ordinal) ? report => dots[key] = report : null);
    }
    catch (Exception e) when (e is not OutOfMemoryException)
    {
        // A build that throws on one run still writes the others, and the line says what was
        // thrown and where, so --check reports it as that run's difference.
        var where = new System.Diagnostics.StackTrace(e).GetFrames().Select(f => f.GetMethod()).FirstOrDefault(m => m?.DeclaringType?.Namespace?.StartsWith("Dbhq", StringComparison.Ordinal) == true);
        results[key] = $"crashed: {e.GetType().Name}: {e.Message} in {where?.DeclaringType?.Name}.{where?.Name}";
    }
});
if (coverage)
{
    foreach (var (key, report) in dots.OrderBy(d => d.Key, StringComparer.Ordinal))
    {
        Console.WriteLine($"{key}: {report}");
    }
}

var lines = new List<string> { $"{Format} frames={frames}" };
lines.AddRange(results.OrderBy(result => result.Key, StringComparer.Ordinal).Select(result => $"{result.Key}: {result.Value}"));
if (outFile is not null)
{
    File.WriteAllLines(outFile, lines);
    Console.WriteLine($"{results.Count} runs written to {outFile}{(oracle ? " (per-dot reference)" : "")}");
}

return baseline is null ? 0 : Compare(baseline, lines, only, oracle);

static int Compare(string baseline, List<string> lines, string? only, bool oracle)
{
    string[] expected = File.ReadAllLines(baseline);
    if (expected.Length == 0 || expected[0] != lines[0])
    {
        Console.WriteLine($"DIFFERENT: the baseline's first line is \"{(expected.Length > 0 ? expected[0] : "")}\" and this run's \"{lines[0]}\"; the format or the frames differ");
        return 1;
    }

    var want = expected.Skip(1).Select(Split).Where(line => only is null || line.Key.Contains(only, StringComparison.Ordinal)).ToDictionary(line => line.Key, line => line.Value);
    var got = lines.Skip(1).Select(Split).ToDictionary(line => line.Key, line => line.Value);
    var keys = want.Keys.Union(got.Keys).Order(StringComparer.Ordinal).ToList();
    int differ = 0;
    foreach (string key in keys)
    {
        want.TryGetValue(key, out string? a);
        got.TryGetValue(key, out string? b);
        if (a == b)
        {
            continue;
        }

        if (differ++ == 0)
        {
            Console.WriteLine($"DIFFERENT: the first run that differs is {key}");
            if (a is null || b is null)
            {
                Console.WriteLine(a is null ? "  it is not in the baseline" : "  it was not run");
            }
            else
            {
                var fa = Fields(a);
                var fb = Fields(b);
                foreach (string field in fa.Keys.Union(fb.Keys))
                {
                    fa.TryGetValue(field, out string? va);
                    fb.TryGetValue(field, out string? vb);
                    if (va != vb)
                    {
                        Console.WriteLine($"  {field}: {va} in the baseline, {vb} now");
                    }
                }
            }
        }
    }

    if (differ > 0)
    {
        Console.WriteLine($"DIFFERENT: {differ} of {keys.Count} runs differ from {baseline}{(oracle ? " (per-dot reference)" : "")}");
        return 1;
    }

    Console.WriteLine($"IDENTICAL: all {keys.Count} runs match {baseline}{(oracle ? " (per-dot reference)" : "")}");
    return 0;
}

static KeyValuePair<string, string> Split(string line)
{
    int colon = line.IndexOf(": ", StringComparison.Ordinal);
    if (colon < 0)
    {
        throw new FormatException($"not a line this tool writes, \"<rom> <region>: <hashes>\": \"{line}\"");
    }

    return new(line[..colon], line[(colon + 2)..]);
}

// The name=value pairs of a line, in order; a line that is not pairs is one field.
static Dictionary<string, string> Fields(string value)
{
    var fields = new Dictionary<string, string>(StringComparer.Ordinal);
    if (!value.StartsWith("steps=", StringComparison.Ordinal))
    {
        fields["result"] = value;
        return fields;
    }

    foreach (string pair in value.Split(' '))
    {
        int equals = pair.IndexOf('=', StringComparison.Ordinal);
        fields[pair[..equals]] = pair[(equals + 1)..];
    }

    return fields;
}

static Type BoardOf(Nes nes)
{
    var finder = new BoardFinder();
    ((IReportsState)nes.Bus).ReportState(finder);
    return finder.Board!.GetType();
}

static string Run(Job job, Region region, int frames, bool oracle, Action<string>? coverage)
{
    Nes nes;
    try
    {
        nes = new Nes(Cartridge.Load(job.Bytes), region, new NesOptions { PerDotReference = oracle });
    }
    catch (NesFormatException e)
    {
        return "not loaded: " + e.Message;
    }

    int want = frames * job.Times;
    var trace = new Fnv();
    var sound = new Fnv();
    var watch = new Watch(nes) { Coverage = coverage is null ? null : new DotCoverage() };
    var samples = new float[4096];
    var ppu = nes.Bus.Ppu;
    var cpu = nes.Cpu;
    nes.Bus.Observer = watch;
    long lastFrame = 0;
    long steps = 0;
    nes.PowerOn();
    while (ppu.Frame < want)
    {
        nes.Step();
        steps++;
        ulong registers = (ulong)cpu.PC | ((ulong)cpu.A << 16) | ((ulong)cpu.X << 24) | ((ulong)cpu.Y << 32) | ((ulong)cpu.P << 40) | ((ulong)cpu.S << 48);
        ulong position = (ulong)nes.Bus.Cycles ^ ((ulong)ppu.Line << 40) ^ ((ulong)ppu.Dot << 52);
        trace.Add(registers);
        trace.Add(position);
        watch.Cpu.Add(registers);
        watch.Cpu.Add(position);
        if (ppu.Frame == lastFrame)
        {
            continue;
        }

        lastFrame = ppu.Frame;
        foreach (uint pixel in ppu.Screen.Pixels)
        {
            trace.Add(pixel);
        }

        trace.Add((ulong)ppu.V | ((ulong)ppu.T << 16) | ((ulong)ppu.FineX << 32) | ((ulong)nes.Bus.PpuDots << 36));
        AddOam(trace, ppu);
        trace.Add(Lines(nes));
        int read;
        while ((read = nes.Sound.Read(samples)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                sound.Add(BitConverter.SingleToUInt32Bits(samples[i]));
            }
        }

        // The samples have been read, which is where the page reads them.
        watch.FrameRead();
        if (job.Scripted)
        {
            nes.SetButtons(0, Script(lastFrame, want));
        }

        if (lastFrame == want / 2)
        {
            nes.Reset();
        }
    }

    var memory = new Fnv();
    for (ushort address = 0; address < 0x800; address++)
    {
        memory.Add(nes.Bus.Peek(address));
    }

    for (int register = 0; register < 8; register++)
    {
        memory.Add(ppu.PeekRegister(register));
    }

    for (ushort address = 0x2000; address < 0x3F20; address++)
    {
        memory.Add(ppu.PeekVram(address));
    }

    AddOam(memory, ppu);
    memory.Add(Lines(nes));
    coverage?.Invoke(watch.Coverage!.Report());

    return $"steps={steps} cycles={nes.Bus.Cycles} trace={trace.Value:X16} sound={sound.Value:X16} dropped={nes.Sound.Dropped} memory={memory.Value:X16}"
        + $" cpu={watch.Cpu.Value:X16} points={watch.Points} ppu={watch.Ppu.Value:X16} apu={watch.Apu.Value:X16} board={watch.Board.Value:X16} lines={watch.Lines.Value:X16}";
}

// The homebrew's buttons for the frame after `frame`: the title for a second and a half, Start to
// begin the first level, then a fixed round of moves and turns, one button held 3 frames in 6;
// after the reset half way, the same again from the title. Start is not in the round, since in a
// level it pauses.
static byte Script(long frame, int want)
{
    const byte a = 0x01, b = 0x02, select = 0x04, start = 0x08, up = 0x10, down = 0x20, left = 0x40, right = 0x80;
    long f = frame % (want / 2);
    if (f is >= 90 and < 96)
    {
        return start;
    }

    if (f < 150 || f % 6 >= 3)
    {
        return 0;
    }

    ReadOnlySpan<byte> round = [right, a, down, b, left, select, up, a, right, right, down, b];
    return round[(int)(f / 6 % round.Length)];
}

static void AddOam(Fnv hash, Ppu ppu)
{
    foreach (byte value in ppu.Oam)
    {
        hash.Add(value);
    }
}

// The interrupt lines: the CPU's NMI and IRQ inputs, the PPU's NMI output and the sound unit's IRQ.
static ulong Lines(Nes nes) =>
    (nes.Cpu.Nmi ? 1UL : 0) | (nes.Cpu.Irq ? 2UL : 0) | (nes.Bus.Ppu.Nmi ? 4UL : 0) | (nes.Bus.Apu.Irq ? 8UL : 0);

// A ROM to run: its name, its bytes, how many times the frames given it runs for, and whether it
// gets the homebrew's button presses.
internal sealed record Job(string Name, byte[] Bytes, int Times, bool Scripted);

// FNV-1a over 64-bit words, as the first version of the tool hashed.
internal sealed class Fnv
{
    public ulong Value { get; private set; } = 14695981039346656037UL;

    public void Add(ulong value)
    {
        Value = (Value ^ value) * 1099511628211UL;
    }
}

// FNV-1a over 64-bit words, each mixed first with splitmix64's finaliser, a bijection that spreads
// every bit of the word over all 64, so a difference in a word's high bits does not stay in the
// hash's high bits, where a second one could cancel it.
internal sealed class Mixed
{
    public ulong Value { get; private set; } = 14695981039346656037UL;

    public void Add(ulong value)
    {
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        value ^= value >> 31;
        Value = (Value ^ value) * 1099511628211UL;
    }

    // The bytes as little-endian 64-bit words, the last padded with zeros, after their count. A
    // memory is most of what a point hashes, so its words go through four lanes at once, each a
    // multiply, a rotate and an add of the next word, which are bijections, so a change in any one
    // word changes its lane; the lanes then go through Add, mixed.
    public void Add(ReadOnlySpan<byte> bytes)
    {
        Add((ulong)bytes.Length);
        ReadOnlySpan<ulong> words = MemoryMarshal.Cast<byte, ulong>(bytes);
        ulong a = 0x243F6A8885A308D3UL, b = 0x13198A2E03707344UL, c = 0xA4093822299F31D0UL, d = 0x082EFA98EC4E6C89UL;
        int i = 0;
        for (; i + 4 <= words.Length; i += 4)
        {
            a = (System.Numerics.BitOperations.RotateLeft(a * 0x9E3779B97F4A7C15UL, 31)) + words[i];
            b = (System.Numerics.BitOperations.RotateLeft(b * 0x9E3779B97F4A7C15UL, 31)) + words[i + 1];
            c = (System.Numerics.BitOperations.RotateLeft(c * 0x9E3779B97F4A7C15UL, 31)) + words[i + 2];
            d = (System.Numerics.BitOperations.RotateLeft(d * 0x9E3779B97F4A7C15UL, 31)) + words[i + 3];
        }

        for (; i < words.Length; i++)
        {
            a = (System.Numerics.BitOperations.RotateLeft(a * 0x9E3779B97F4A7C15UL, 31)) + words[i];
        }

        ulong tail = 0;
        for (int j = words.Length * 8; j < bytes.Length; j++)
        {
            tail |= (ulong)bytes[j] << ((j & 7) * 8);
        }

        Add(a);
        Add(b);
        Add(c);
        Add(d);
        Add(tail);
    }
}

// Hashes a state report into one hash, going into the parts it is let into: the PPU, the sound
// unit and the sample buffer, and the picture, each only when asked.
internal sealed class HashSink(Mixed hash) : IStateSink
{
    public bool Ppu { get; set; }

    public bool Apu { get; set; }

    public bool Picture { get; set; }

    public void Add(string name, long value) => hash.Add((ulong)value);

    public void Add(string name, ulong value) => hash.Add(value);

    public void Add(string name, bool value) => hash.Add(value ? 1UL : 0);

    public void Add(string name, double value) => hash.Add(BitConverter.DoubleToUInt64Bits(value));

    public void Add(string name, ReadOnlySpan<byte> values) => hash.Add(values);

    public void Add(string name, ReadOnlySpan<int> values) => hash.Add(MemoryMarshal.AsBytes(values));

    public void Add(string name, ReadOnlySpan<uint> values) => hash.Add(MemoryMarshal.AsBytes(values));

    public void Add(string name, ReadOnlySpan<long> values) => hash.Add(MemoryMarshal.AsBytes(values));

    public void Add(string name, ReadOnlySpan<float> values) => hash.Add(MemoryMarshal.AsBytes(values));

    public void Add(string name, ReadOnlySpan<double> values) => hash.Add(MemoryMarshal.AsBytes(values));

    public void Add(string name, IReportsState part)
    {
        bool enter = part switch
        {
            Dbhq.Machines.Nes.Ppu => Ppu,
            Dbhq.Machines.Nes.Apu or SampleBuffer => Apu,
            FrameBuffer => Picture,
            _ => true,
        };
        if (enter)
        {
            part.ReportState(this);
        }
    }

    public void Skip(string name, string why)
    {
    }
}

// Finds the board in the bus's report.
internal sealed class BoardFinder : IStateSink
{
    public IReportsState? Board { get; private set; }

    public void Add(string name, long value)
    {
    }

    public void Add(string name, ulong value)
    {
    }

    public void Add(string name, bool value)
    {
    }

    public void Add(string name, double value)
    {
    }

    public void Add(string name, ReadOnlySpan<byte> values)
    {
    }

    public void Add(string name, ReadOnlySpan<int> values)
    {
    }

    public void Add(string name, ReadOnlySpan<uint> values)
    {
    }

    public void Add(string name, ReadOnlySpan<long> values)
    {
    }

    public void Add(string name, ReadOnlySpan<float> values)
    {
    }

    public void Add(string name, ReadOnlySpan<double> values)
    {
    }

    public void Add(string name, IReportsState part)
    {
        if (name == "_mapper")
        {
            Board = part;
        }
    }

    public void Skip(string name, string why)
    {
    }
}

// The observer: at each point the bus says a chip can be seen, the point and the logical position,
// then the state of what can be seen there (see the top of the file).
internal sealed class Watch : INesObserver
{
    // What kind of point, the first word hashed at it.
    private const ulong PpuAccess = 1;
    private const ulong ApuAccess = 2;
    private const ulong BoardWrite = 3;
    private const ulong DmcFetch = 4;
    private const ulong FrameEnd = 5;
    private const ulong FrameReadPoint = 6;
    private const ulong PowerOnPoint = 7;
    private const ulong ResetPoint = 8;

    private readonly NesBus _bus;
    private readonly IReportsState _busReport;
    private readonly IReportsState _ppuReport;
    private readonly IReportsState _apuReport;
    private readonly IReportsState _soundReport;
    private readonly HashSink _ppuSink;
    private readonly HashSink _apuSink;
    private readonly HashSink _boardSink;

    public Watch(Nes nes)
    {
        _bus = nes.Bus;
        _busReport = nes.Bus;
        _ppuReport = nes.Bus.Ppu;
        _apuReport = nes.Bus.Apu;
        _soundReport = nes.Bus.Sound;
        _ppuSink = new HashSink(Ppu) { Ppu = true };
        _apuSink = new HashSink(Apu) { Apu = true };
        _boardSink = new HashSink(Board);
    }

    public Mixed Cpu { get; } = new();

    public Mixed Ppu { get; } = new();

    public Mixed Apu { get; } = new();

    public Mixed Board { get; } = new();

    public Mixed Lines { get; } = new();

    public long Points { get; private set; }

    // Where the PPU register writes landed, for --coverage; null otherwise.
    public DotCoverage? Coverage { get; init; }

    public void Accessed(ushort address, bool write, byte value)
    {
        if (Coverage is not null && address < 0x4000)
        {
            Coverage.Note(write, address, _bus.Ppu);
        }

        ulong access = ((ulong)address << 8) | (write ? 1UL << 24 : 0) | ((ulong)value << 32);
        ulong point;
        if (address < 0x4000)
        {
            point = PpuAccess | access;
            PpuPoint(point, picture: false);
        }
        else if (address < 0x4020)
        {
            point = ApuAccess | access;
            ApuPoint(point);
        }
        else
        {
            point = BoardWrite | access;
            PpuPoint(point, picture: false);
        }

        BoardPoint(point);
    }

    public void DmcFetched()
    {
        ApuPoint(DmcFetch);
        BoardPoint(DmcFetch);
    }

    public void FrameEnded()
    {
        PpuPoint(FrameEnd, picture: true);
    }

    public void CycleEnded(bool nmi, bool irq)
    {
        Lines.Add((nmi ? 1UL : 0) | (irq ? 2UL : 0));
    }

    public void ChipsReset(bool power)
    {
        ulong kind = power ? PowerOnPoint : ResetPoint;
        PpuPoint(kind, picture: true);
        ApuPoint(kind);
        BoardPoint(kind);
    }

    // After the samples are read at a frame's end: the sound unit, the buffer and the bus.
    public void FrameRead()
    {
        ApuPoint(FrameReadPoint);
        BoardPoint(FrameReadPoint);
    }

    // The PPU's whole state, with the logical position: the bus's counts and the PPU's line and
    // dot as it reports them, which a lazy PPU gives without catching up. At a frame end the line
    // and dot are not hashed: the dot that ends the frame may be run before the bus is done with
    // its cycle, so the logical position can be a dot or two on; the state has the PPU's own.
    private void PpuPoint(ulong point, bool picture)
    {
        Points++;
        Ppu.Add(point);
        Ppu.Add((ulong)_bus.Cycles);
        Ppu.Add((ulong)_bus.PpuDots);
        if ((point & 0xFF) != FrameEnd)
        {
            Ppu.Add((uint)_bus.Ppu.Line | ((ulong)(uint)_bus.Ppu.Dot << 16));
        }

        _ppuSink.Picture = picture;
        _ppuReport.ReportState(_ppuSink);
    }

    private void ApuPoint(ulong point)
    {
        Points++;
        Apu.Add(point);
        Apu.Add((ulong)_bus.Cycles);
        Apu.Add((ulong)_bus.PpuDots);
        _apuReport.ReportState(_apuSink);
        _soundReport.ReportState(_apuSink);
    }

    // The bus's own state, the pads' and the board's; never the PPU's or the sound unit's.
    private void BoardPoint(ulong point)
    {
        Board.Add(point);
        Board.Add((ulong)_bus.Cycles);
        _busReport.ReportState(_boardSink);
    }
}

// For --coverage: the dots at which a write to a PPU register landed with rendering on, on the
// visible lines and on the pre-render line, and the dots of line 241 at which a $2000 write or a
// $2002 read landed, so the synthetic jobs' sweeps can be seen to reach every dot.
internal sealed class DotCoverage
{
    private static readonly int[] Key = [0, 1, 2, 255, 256, 257, 258, 320, 337, 338, 339, 340];
    private readonly bool[] _visible = new bool[341];
    private readonly bool[] _preRender = new bool[341];
    private readonly bool[] _vblank = new bool[341];

    public void Note(bool write, ushort address, Ppu ppu)
    {
        int line = ppu.Line;
        int dot = ppu.Dot;
        if (line == 241 && (write ? (address & 7) == 0 : (address & 7) == 2))
        {
            _vblank[dot] = true;
        }

        if (!write || !ppu.RenderingEnabled)
        {
            return;
        }

        if (line < 240)
        {
            _visible[dot] = true;
        }
        else if (line == ppu.Region.PreRenderLine)
        {
            _preRender[dot] = true;
        }
    }

    public string Report()
    {
        string Of(bool[] hit) => $"{hit.Count(h => h)}/341 (key dots missing: {string.Join(",", Key.Where(d => !hit[d]))})";
        return $"visible {Of(_visible)}, pre-render {Of(_preRender)}; line 241, $2000 writes and $2002 reads, dots 0 to 3: {string.Join(",", Enumerable.Range(0, 4).Where(d => _vblank[d]))}";
    }
}
