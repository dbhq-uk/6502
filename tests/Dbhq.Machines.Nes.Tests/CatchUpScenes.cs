using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>What an access in a scene does: a CPU write or read, or the reset button.</summary>
internal enum ActKind
{
    Write,
    Read,
    Reset,
}

/// <summary>
/// One access of a scene: made in the first cycle whose access sees the PPU at or after
/// <paramref name="Line"/>, <paramref name="Dot"/> of the scene's frame (an access sees the PPU
/// after two of its cycle's dots), or at once if that is past.
/// </summary>
internal readonly record struct Act(int Line, int Dot, ActKind Kind, ushort Address = 0, byte Value = 0)
{
    public static Act Write(int line, int dot, ushort address, byte value) => new(line, dot, ActKind.Write, address, value);

    public static Act Read(int line, int dot, ushort address) => new(line, dot, ActKind.Read, address);
}

/// <summary>
/// A scene for the bus: a cartridge, the accesses that set it up in frame 0, and those of each
/// frame from 1 to <paramref name="Frames"/>, worked out from the frame and the region.
/// <paramref name="Watches"/> says the board watches the PPU's address bus, so even the lazy build
/// catches the PPU up every cycle.
/// </summary>
internal sealed record CatchUpScene(Func<byte[]> Cartridge, byte Ctrl, byte Mask, Func<int, Region, Act[]> Frame, int Frames, bool Watches = false, Act[]? Extra = null);

/// <summary>
/// The scenes the lazy PPU is checked on against the per-dot reference, and how they are run and
/// compared. Never a game: the cartridges are bytes, and the accesses are the scenes'.
/// </summary>
internal static class CatchUpScenes
{
    // The dots of a line that the review focus names, and their neighbours.
    private static readonly int[] KeyDots = [0, 1, 2, 3, 64, 65, 128, 255, 256, 257, 258, 320, 321, 336, 337, 338, 339, 340];

    public static readonly Dictionary<string, CatchUpScene> Table = new(StringComparer.Ordinal)
    {
        // Review Focus 5: frames with no register access at all; the PPU is caught up only at its
        // events and at the frame ends.
        ["no access, NMI on"] = new(Nrom, 0x80, 0x1E, (_, _) => [], 4),
        ["no access, NMI off"] = new(Nrom, 0x00, 0x1E, (_, _) => [], 4),
        ["no access, rendering off"] = new(Nrom, 0x80, 0x00, (_, _) => [], 3),

        // Review Focus 1: scroll writes on the line's key dots, a different line for each.
        ["$2005 and $2006 writes on the key dots"] = new(Nrom, 0x80, 0x1E, (f, _) => KeyDots.SelectMany((d, k) => new[]
        {
            Act.Write(20 + (8 * k), d, 0x2005, (byte)((f * 13) + k)),
            Act.Write(20 + (8 * k), d, 0x2005, (byte)((f * 7) + k)),
            Act.Write(24 + (8 * k), d, 0x2006, (byte)(0x20 + (k & 7))),
            Act.Write(24 + (8 * k), d, 0x2006, (byte)(f + (k * 9))),
        }).ToArray(), 4),
        ["$2000 nametable writes mid-line"] = new(Nrom, 0x80, 0x1E, (f, _) => KeyDots.Select((d, k) => Act.Write(30 + (10 * k), d, 0x2000, (byte)(0x80 | ((f + k) & 3)))).ToArray(), 4),

        // Review Focus 2: rendering switched at the line's edges, and on the pre-render line's
        // dots around 338, where the odd frame's dropped dot is decided.
        ["$2001 at the line edges and the dropped dot"] = new(Nrom, 0x80, 0x1E, (f, r) =>
        [
            Act.Write(0, 5, 0x2001, 0x1E),
            Act.Write(50, 338, 0x2001, 0x00),
            Act.Write(51, 1, 0x2001, 0x1E),
            Act.Write(90, 255, 0x2001, 0x08),
            Act.Write(90, 258, 0x2001, 0x1E),
            Act.Write(130, 339, 0x2001, 0x16),
            Act.Write(131, 0, 0x2001, 0x1E),
            Act.Write(170, 100, 0x2001, 0xFF),
            Act.Write(170, 180, 0x2001, 0x1E),
            Act.Write(r.PreRenderLine, 333 + (f % 8), 0x2001, 0x00),
        ], 9),

        // Review Focus 4: $2002 read on and around the VBlank dot, with NMI on.
        ["$2002 read at line 240 dot 340"] = new(Nrom, 0x80, 0x1E, (_, _) => [Act.Read(240, 340, 0x2002)], 6),
        ["$2002 read at line 241 dot 0"] = new(Nrom, 0x80, 0x1E, (_, _) => [Act.Read(241, 0, 0x2002)], 6),
        ["$2002 read at line 241 dot 1"] = new(Nrom, 0x80, 0x1E, (_, _) => [Act.Read(241, 1, 0x2002)], 6),
        ["$2002 read at line 241 dot 2"] = new(Nrom, 0x80, 0x1E, (_, _) => [Act.Read(241, 2, 0x2002)], 6),

        // Review Focus 4: $2000 bit 7 on and off through VBlank, and a $2002 read between.
        ["$2000 bit 7 toggled in VBlank"] = new(Nrom, 0x00, 0x1E, (f, r) =>
        [
            Act.Write(241, f % 4, 0x2000, 0x80),
            Act.Write(243, 10, 0x2000, 0x00),
            Act.Write(246, 100, 0x2000, 0x80),
            Act.Read(250, 5, 0x2002),
            Act.Write(252, 0, 0x2000, 0x00),
            Act.Write(255, 0, 0x2000, 0x80),
            Act.Write(r.PreRenderLine, f % 3, 0x2000, 0x00),
            Act.Write(r.PreRenderLine, 5, 0x2000, 0x80),
        ], 8),
        ["$2000 bit 7 set in VBlank, rendering off"] = new(Nrom, 0x00, 0x00, (f, _) => [Act.Write(241, 3 * f, 0x2000, 0x80), Act.Write(248, 0, 0x2000, 0x00)], 6),

        // OAM: DMA, OAMADDR and OAMDATA while the PPU renders, and in VBlank.
        ["OAM DMA and $2003 and $2004 while rendering"] = new(Nrom, 0x80, 0x1E, (f, r) =>
        [
            Act.Write(60, 100, 0x4014, 0x02),
            Act.Write(80, 10, 0x2003, (byte)(5 + f)),
            Act.Write(80, 20, 0x2004, 0x33),
            Act.Read(90, 300, 0x2004),
            Act.Read(100, 64, 0x2004),
            Act.Read(100, 200, 0x2004),
            Act.Write(250, 0, 0x2003, 0x00),
            Act.Write(250, 10, 0x4014, 0x02),
            Act.Write(r.PreRenderLine, 300, 0x4014, 0x02),
        ], 4),

        ["$2007 while rendering"] = new(Nrom, 0x80, 0x1E, (f, r) =>
        [
            Act.Read(70, 50, 0x2007),
            Act.Write(70, 200, 0x2007, (byte)(0x44 + f)),
            Act.Read(110, 255, 0x2007),
            Act.Read(r.PreRenderLine, 320, 0x2007),
        ], 4),

        // Sprite 0 hit and overflow polled, a read a cycle, across the lines sprite 0 is on.
        ["$2002 polled through sprite 0"] = new(Nrom, 0x80, 0x1E, (f, _) => Enumerable.Range(0, 400).Select(i => Act.Read(28 + (f % 3), 200, 0x2002)).ToArray(), 4),
        ["$2002 polled through 8 by 16 sprites"] = new(Nrom, 0xA0, 0x1E, (f, _) => Enumerable.Range(0, 400).Select(i => Act.Read(27 + (f % 4), 100, 0x2002)).ToArray(), 4),

        // Ruling T: the board's writes change the pattern banks under the PPU.
        ["CNROM bank writes mid-line"] = new(Cnrom, 0x80, 0x1E, (f, r) =>
        [
            Act.Write(100, 130, 0x8000, (byte)(f % 4)),
            Act.Write(150, 257, 0x8000, (byte)((f + 1) % 4)),
            Act.Write(200, 3 * f, 0x8000, (byte)((f + 2) % 4)),
            Act.Write(r.PreRenderLine, 330, 0x8000, 0x00),
        ], 4),
        ["PRG RAM writes mid-line"] = new(Cnrom, 0x80, 0x1E, (f, _) => [Act.Write(100, 130, 0x6000, (byte)f), Act.Write(101, 0, 0x6001, (byte)f)], 3),

        // A board that watches the PPU's address bus: MMC3's scanline IRQ, caught up every cycle.
        ["MMC3 scanline IRQ"] = new(Mmc3, 0x08, 0x1E, (f, r) =>
        [
            Act.Write(60, 0, 0xE000, 0),
            Act.Write(60, 3, 0xE001, 0),
            Act.Write(60, 6, 0xC000, (byte)(((f * 3) % 20) + 5)),
            Act.Write(120, 100, 0xE000, 0),
            Act.Write(120, 103, 0xE001, 0),
            Act.Write(140, 200, 0x8000, 0x02),
            Act.Write(140, 203, 0x8001, (byte)(f & 7)),
            Act.Write(r.PreRenderLine, 0, 0xC001, 0),
        ], 4, Watches: true, Extra: [Act.Write(0, 0, 0xC000, 10), Act.Write(0, 0, 0xC001, 0), Act.Write(0, 0, 0xE001, 0)]),

        // The reset button mid-frame, then rendering and NMI back on where evaluation is not running.
        ["reset mid-frame"] = new(Nrom, 0x80, 0x1E, (f, r) => f == 2
            ? [Act.Write(0, 0, 0x0000, 0), new Act(100, 100, ActKind.Reset), Act.Write(r.PreRenderLine, 100, 0x2001, 0x1E), Act.Write(r.PreRenderLine, 110, 0x2000, 0x80)]
            : [], 4),
    };

    /// <summary>Sprite <c>i / 4</c>'s byte <c>i % 4</c>: sprite 0 at y 30, the others spread down the picture, several to a line.</summary>
    public static byte OamByte(int i)
    {
        int sprite = i >> 2;
        return (i & 3) switch
        {
            0 => sprite == 0 ? (byte)30 : (byte)(20 + (sprite * 3 % 200)),
            1 => (byte)((sprite * 11) + 1),
            2 => (byte)((sprite * 0x41) & 0xE3),
            _ => sprite == 0 ? (byte)40 : (byte)(sprite * 4),
        };
    }

    /// <summary>NROM, 32 KB of PRG, CHR ROM whose bytes are all different nearby, vertical mirroring.</summary>
    public static byte[] Nrom()
    {
        byte[] header = TestCartridge.Header();
        header[4] = 2;
        header[5] = 1;
        header[6] = 1;
        return TestCartridge.Join(header, new byte[0x8000], TestCartridge.Pattern(0x2000));
    }

    /// <summary>CNROM with four CHR banks, each byte its bank's number, and no bus conflicts (submapper 1).</summary>
    public static byte[] Cnrom() => TestCartridge.Banked(3, prgBanks: 2, chrBanks: 4, submapper: 1);

    /// <summary>MMC3 with 32 KB of PRG and 8 KB of CHR in 1 KB banks, each byte its bank's number.</summary>
    public static byte[] Mmc3() => TestCartridge.Banked(4, prgBanks: 4, prgBankSize: 0x2000, chrBanks: 8, chrBankSize: 0x400);

    /// <summary>
    /// Runs a scene on a new machine: the per-dot reference when <paramref name="oracle"/>, else the
    /// lazy build. Power on, the setup in frame 0, the acts of each frame, then on to frame end.
    /// </summary>
    public static (CatchUpRecorder Recorder, NesBus Bus) Run(CatchUpScene scene, Region region, bool oracle)
    {
        var bus = new NesBus(Cartridge.Load(scene.Cartridge()), region, new NesOptions { PerDotReference = oracle });
        var recorder = new CatchUpRecorder(bus);
        bus.Observer = recorder;
        bus.PowerOn();
        for (int i = 0; i < 256; i++)
        {
            bus.PokeRam((ushort)(0x200 + i), OamByte(i));
        }

        foreach (Act act in Setup(scene))
        {
            Do(bus, act);
        }

        for (int frame = 1; frame <= scene.Frames; frame++)
        {
            foreach (Act act in scene.Frame(frame, region))
            {
                IdleTo(bus, frame, act.Line, act.Dot);
                Do(bus, act);
            }
        }

        IdleTo(bus, scene.Frames + 1, 0, 30);
        return (recorder, bus);
    }

    // Rendering off: the palette, both nametables, OAM by DMA from page 2, the scroll, then the
    // scene's own board writes, PPUCTRL and PPUMASK.
    private static IEnumerable<Act> Setup(CatchUpScene scene)
    {
        yield return Act.Read(0, 0, 0x2002);
        yield return Act.Write(0, 0, 0x2000, 0x00);
        yield return Act.Write(0, 0, 0x2001, 0x00);
        yield return Act.Write(0, 0, 0x2006, 0x3F);
        yield return Act.Write(0, 0, 0x2006, 0x00);
        for (int i = 0; i < 32; i++)
        {
            yield return Act.Write(0, 0, 0x2007, (byte)((i * 5) + 1));
        }

        yield return Act.Write(0, 0, 0x2006, 0x20);
        yield return Act.Write(0, 0, 0x2006, 0x00);
        for (int i = 0; i < 0x800; i++)
        {
            yield return Act.Write(0, 0, 0x2007, (byte)((i * 7) + 3));
        }

        yield return Act.Write(0, 0, 0x2003, 0x00);
        yield return Act.Write(0, 0, 0x4014, 0x02);
        yield return Act.Read(0, 0, 0x0000);
        yield return Act.Write(0, 0, 0x2005, 13);
        yield return Act.Write(0, 0, 0x2005, 7);
        foreach (Act act in scene.Extra ?? [])
        {
            yield return act;
        }

        yield return Act.Write(0, 0, 0x2000, scene.Ctrl);
        yield return Act.Write(0, 0, 0x2001, scene.Mask);
    }

    // Idle cycles, reads of RAM, until the next access would see the PPU at or after the dot, in
    // the frame given. Line, Dot and Frame are the logical position: reading them catches nothing up.
    private static void IdleTo(NesBus bus, long frame, int line, int dot)
    {
        int target = (line * Region.DotsPerLine) + dot;
        while (bus.Ppu.Frame < frame || (bus.Ppu.Frame == frame && (bus.Ppu.Line * Region.DotsPerLine) + bus.Ppu.Dot + 2 < target))
        {
            bus.Read(0x0000);
        }
    }

    private static void Do(NesBus bus, Act act)
    {
        switch (act.Kind)
        {
            case ActKind.Write:
                bus.Write(act.Address, act.Value);
                break;
            case ActKind.Read:
                bus.Read(act.Address);
                break;
            default:
                bus.Reset();
                break;
        }
    }

    /// <summary>The two records the same, point for point and cycle for cycle; the first difference named.</summary>
    public static void AssertSame(CatchUpRecorder reference, CatchUpRecorder lazy)
    {
        for (int i = 0; i < Math.Min(reference.Points.Count, lazy.Points.Count); i++)
        {
            if (reference.Points[i] != lazy.Points[i])
            {
                Assert.Fail($"point {i} differs:\n  per-dot: {reference.Points[i]}\n  lazy:    {lazy.Points[i]}\n  after:   {(i > 0 ? reference.Points[i - 1] : "nothing")}");
            }
        }

        Assert.Equal(reference.Points.Count, lazy.Points.Count);
        for (int i = 0; i < Math.Min(reference.Lines.Count, lazy.Lines.Count); i++)
        {
            if (reference.Lines[i] != lazy.Lines[i])
            {
                Assert.Fail($"the lines differ at cycle {i + 1}: per-dot NMI {(reference.Lines[i] & 1) != 0} IRQ {(reference.Lines[i] & 2) != 0}, lazy NMI {(lazy.Lines[i] & 1) != 0} IRQ {(lazy.Lines[i] & 2) != 0}");
            }
        }

        Assert.Equal(reference.Lines.Count, lazy.Lines.Count);
    }

    /// <summary>
    /// A hash of a part's whole state as it reports it, which reads only and catches nothing up:
    /// a PPU's with its picture or without, a bus's without the PPU and the sound unit.
    /// </summary>
    public static ulong Hash(IReportsState part, bool picture)
    {
        var sink = new HashSink(picture);
        part.ReportState(sink);
        return sink.Value;
    }

    private sealed class HashSink(bool picture) : IStateSink
    {
        public ulong Value { get; private set; } = 14695981039346656037UL;

        public void Add(string name, long value) => Mix((ulong)value);

        public void Add(string name, ulong value) => Mix(value);

        public void Add(string name, bool value) => Mix(value ? 1UL : 0);

        public void Add(string name, double value) => Mix(BitConverter.DoubleToUInt64Bits(value));

        public void Add(string name, ReadOnlySpan<byte> values)
        {
            Mix((ulong)values.Length);
            foreach (byte value in values)
            {
                Mix(value);
            }
        }

        public void Add(string name, ReadOnlySpan<int> values)
        {
            Mix((ulong)values.Length);
            foreach (int value in values)
            {
                Mix((uint)value);
            }
        }

        public void Add(string name, ReadOnlySpan<uint> values)
        {
            Mix((ulong)values.Length);
            foreach (uint value in values)
            {
                Mix(value);
            }
        }

        public void Add(string name, ReadOnlySpan<long> values)
        {
            Mix((ulong)values.Length);
            foreach (long value in values)
            {
                Mix((ulong)value);
            }
        }

        public void Add(string name, ReadOnlySpan<float> values)
        {
            Mix((ulong)values.Length);
            foreach (float value in values)
            {
                Mix(BitConverter.SingleToUInt32Bits(value));
            }
        }

        public void Add(string name, ReadOnlySpan<double> values)
        {
            Mix((ulong)values.Length);
            foreach (double value in values)
            {
                Mix(BitConverter.DoubleToUInt64Bits(value));
            }
        }

        public void Add(string name, IReportsState part)
        {
            bool enter = part switch
            {
                Ppu or Apu or SampleBuffer => false,
                FrameBuffer => picture,
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

        private void Mix(ulong value)
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            Value = (Value ^ value) * 1099511628211UL;
        }
    }
}

/// <summary>
/// Records, for a scene, every point where the PPU can be seen (each access to its registers or a
/// write to the cartridge, with the logical position and the PPU's and the bus's state; each frame
/// end with the picture; the reset button), the bus's counts at each, the interrupt lines every
/// cycle, and the most dots the PPU was owed at a cycle's end. It reads only, so it catches nothing up.
/// </summary>
internal sealed class CatchUpRecorder(NesBus bus) : INesObserver
{
    public List<string> Points { get; } = [];

    public List<byte> Lines { get; } = [];

    public long MostOwed { get; private set; }

    public void Accessed(ushort address, bool write, byte value)
    {
        string what = $"{(write ? "write" : "read")} ${address:X4} {value:X2} cycle {bus.Cycles} dots {bus.PpuDots} bus {CatchUpScenes.Hash(bus, picture: false):X16}";
        Points.Add(address is < 0x4000 or >= 0x4020
            ? $"{what} at line {bus.Ppu.Line} dot {bus.Ppu.Dot} ppu {CatchUpScenes.Hash(bus.Ppu, picture: false):X16}"
            : what);
    }

    public void FrameEnded() => Points.Add($"frame end cycle {bus.Cycles} dots {bus.PpuDots} ppu {CatchUpScenes.Hash(bus.Ppu, picture: true):X16}");

    public void CycleEnded(bool nmi, bool irq)
    {
        Lines.Add((byte)((nmi ? 1 : 0) | (irq ? 2 : 0)));
        MostOwed = Math.Max(MostOwed, bus.Ppu.LogicalDots - bus.Ppu.CaughtUpDots);
    }

    public void ChipsReset(bool power) => Points.Add($"{(power ? "power on" : "reset")} cycle {bus.Cycles} ppu {CatchUpScenes.Hash(bus.Ppu, picture: true):X16}");

    public void DmcFetched()
    {
    }
}
