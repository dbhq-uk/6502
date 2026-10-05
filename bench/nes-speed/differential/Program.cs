using System.Collections.Concurrent;
using Dbhq.Cpu6502.TestSupport;
using Dbhq.Machines.Nes;

// The NES differential check, for work that must not change behaviour, such as making the machine
// faster: every pinned NES test ROM is run in both regions, and for each the program writes one
// line of hashes of what the machine did. Two builds that behave the same write the same file.
//
//   dotnet run -c Release --project bench/nes-speed/differential -- <output file> [frames]
//
// Each run powers on, runs the frames given (150 by default; six times that for SNOW and the MMC3
// ROMs, whose work is in the picture and the scanline counter), and presses reset once half way.
// Hashed: after every instruction, the CPU's registers, the cycle count and the PPU's line and
// dot (so every interrupt and every dot is in place); at the end of every frame, all the pixels,
// v, t, fine X and the dot count, and the sound samples made so far; at the end, RAM, what the
// PPU's registers would read, and VRAM. The ROMs come from the pinned fork, checked against their
// hashes (NesTestRoms.Read), and are never committed.
string outFile = args.Length > 0 ? args[0] : throw new ArgumentException("usage: <output file> [frames]");
int frames = args.Length > 1 ? int.Parse(args[1]) : 150;

// Read first, one at a time, so the downloads of a first run do not race.
var roms = Pins.NesTestRomHashes.Keys.Order(StringComparer.Ordinal).Select(name => (Name: name, Bytes: NesTestRoms.Read(name))).ToList();
var results = new ConcurrentDictionary<string, string>();
var jobs = roms.SelectMany(rom => new[] { (Rom: rom, Region: Region.Ntsc), (Rom: rom, Region: Region.Pal) });
Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = 4 }, job => results[$"{job.Rom.Name} {job.Region.Name}"] = Run(job.Rom.Name, job.Rom.Bytes, job.Region, frames));
File.WriteAllLines(outFile, results.OrderBy(result => result.Key, StringComparer.Ordinal).Select(result => $"{result.Key}: {result.Value}"));
Console.WriteLine($"{results.Count} runs written to {outFile}");
return;

static string Run(string name, byte[] bytes, Region region, int frames)
{
    Nes nes;
    try
    {
        nes = new Nes(Cartridge.Load(bytes), region);
    }
    catch (NesFormatException e)
    {
        return "not loaded: " + e.Message;
    }

    int want = name.Contains("snow", StringComparison.Ordinal) || name.Contains("mmc3", StringComparison.Ordinal) ? frames * 6 : frames;
    var trace = new Fnv();
    var sound = new Fnv();
    var samples = new float[4096];
    var ppu = nes.Bus.Ppu;
    var cpu = nes.Cpu;
    long lastFrame = 0;
    long steps = 0;
    nes.PowerOn();
    while (ppu.Frame < want)
    {
        nes.Step();
        steps++;
        trace.Add((ulong)cpu.PC | ((ulong)cpu.A << 16) | ((ulong)cpu.X << 24) | ((ulong)cpu.Y << 32) | ((ulong)cpu.P << 40) | ((ulong)cpu.S << 48));
        trace.Add((ulong)nes.Bus.Cycles ^ ((ulong)ppu.Line << 40) ^ ((ulong)ppu.Dot << 52));
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
        int read;
        while ((read = nes.Sound.Read(samples)) > 0)
        {
            for (int i = 0; i < read; i++)
            {
                sound.Add(BitConverter.SingleToUInt32Bits(samples[i]));
            }
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

    return $"steps={steps} cycles={nes.Bus.Cycles} trace={trace.Value:X16} sound={sound.Value:X16} dropped={nes.Sound.Dropped} memory={memory.Value:X16}";
}

// FNV-1a over 64-bit values.
internal sealed class Fnv
{
    public ulong Value { get; private set; } = 14695981039346656037UL;

    public void Add(ulong value)
    {
        Value = (Value ^ value) * 1099511628211UL;
    }
}
