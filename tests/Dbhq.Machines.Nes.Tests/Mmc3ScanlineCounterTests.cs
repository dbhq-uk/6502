using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// MMC3's scanline counter driven by the real PPU, alone and on the real bus, in both regions:
/// that the CPU cycle the board is told with each PPU address is the cycle of that access, that the
/// counter clocks once a line on the lines the PPU fetches sprites, at the dot the sprite pattern
/// address goes out, and that its IRQ reaches the CPU by the bus's start-of-cycle rule.
/// <c>docs/nes/facts/mappers.md</c> section 6 and <c>ppu.md</c> sections 6 and 7.
/// </summary>
public class Mmc3ScanlineCounterTests
{
    // The dot on which the PPU puts the first sprite pattern address on its bus (FetchSprite, slot
    // 0, step 4: dot 257 + 4). The sheet says "PPU cycle 260" from the wiki and marks the dot a
    // guess (open item 1); mmc3_test_2's 4-scanline_timing passes with the rise here (the journal,
    // task 11).
    private const int ClockDot = 261;

    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    private static Region RegionNamed(string name) => name == "PAL" ? Region.Pal : Region.Ntsc;

    // An MMC3 cartridge: 32 KB of PRG in 8 KB banks and 8 KB of CHR in 1 KB banks, which holds the
    // 6502 program the bus tests need at $E000 (the fixed last bank) when given one.
    private static Cartridge Mmc3Cartridge(byte[]? lastBank = null)
    {
        byte[] file = TestCartridge.Banked(4, 4, 8192, 8, 1024);
        if (lastBank is not null)
        {
            lastBank.CopyTo(file, 16 + (3 * 8192));
        }

        return Cartridge.Load(file);
    }

    /// <summary>
    /// A board between the PPU and an MMC3 that tells it the CPU cycle the test keeps rather than
    /// the PPU's, for a PPU run alone a dot at a time, which has no bus to set its cycle.
    /// </summary>
    private sealed class Clocked(IMapper inner) : IMapper
    {
        public long Cycle { get; set; }

        public Mirroring Mirroring => inner.Mirroring;

        public bool Irq => inner.Irq;

        public byte[] PrgRam => inner.PrgRam;

        public byte CpuRead(ushort address, byte openBus) => inner.CpuRead(address, openBus);

        public void CpuWrite(ushort address, byte value) => inner.CpuWrite(address, value);

        public byte PpuRead(ushort address) => inner.PpuRead(address);

        public void PpuWrite(ushort address, byte value) => inner.PpuWrite(address, value);

        public void PpuAddressChanged(ushort address, long cpuCycle) => inner.PpuAddressChanged(address, Cycle);

        public void CpuCycle() => inner.CpuCycle();

        public void Reset(bool power) => inner.Reset(power);

        public void ClearPrgRam() => inner.ClearPrgRam();
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ARealPpuClocksTheCounterOnceALineAtTheFirstSpritePatternFetch(string name)
    {
        // Background at $0000, 8x8 sprites at $1000, no sprites on the screen (OAM is zero, so
        // sprite 0 at Y 0 is in range on lines 0 to 7, and its fetches are at $1000 too). Latch 0
        // raises the IRQ on every clock (the Sharp behaviour), so each clock is seen as it happens.
        Region region = RegionNamed(name);
        List<(int Line, int Dot)> clocks = ClocksInAFrame(region, ctrl: 0x08);

        int[] expectedLines = [.. Enumerable.Range(0, 240), region.PreRenderLine];
        Assert.Equal(expectedLines, clocks.Select(c => c.Line));
        Assert.All(clocks, c => Assert.Equal(ClockDot, c.Dot));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithTheBackgroundAt1000TheCounterClocksOnceALineAtTheNextLinesFirstPatternFetch(string name)
    {
        // Background at $1000, sprites at $0000: A12 is low through the sprite fetches and rises
        // at the next line's first background pattern fetch, dot 325 (the sheet's "dot 324 of the
        // line before", one dot apart as at 260 and 261). Once a line: the background's nametable
        // fetches are not told to the board (ppu.md 6), so the low from 337 to dot 4 is not seen.
        Region region = RegionNamed(name);
        List<(int Line, int Dot)> clocks = ClocksInAFrame(region, ctrl: 0x10);

        int[] expectedLines = [.. Enumerable.Range(0, 240), region.PreRenderLine];
        Assert.Equal(expectedLines, clocks.Select(c => c.Line));
        Assert.All(clocks, c => Assert.Equal(325, c.Dot));
    }

    // A $2006 pair written during rendering, its second write landing on one dot of every visible
    // line: PPUCTRL, the high byte (the low is $00), the dot, and the dot the counter should clock
    // on, once a rendering line, or -1 for never. During rendering the bus carries the fetches,
    // not v (ppu.md 6), so the write must change nothing.
    public static TheoryData<string, byte, byte, int, int> Writes2006DuringRendering()
    {
        var data = new TheoryData<string, byte, byte, int, int>();
        foreach (string region in new[] { "NTSC", "PAL" })
        {
            // Sprites at $1000, $2000 on dot 262. A told v would be a false low, but the slot's own
            // high-plane fetch on dot 263 ends it after a dot, so this case alone could not fail.
            data.Add(region, 0x08, 0x20, 262, ClockDot);

            // Both tables at $1000: no told low lasts long enough, so the counter never clocks. A
            // told $2000 on dot 337 would be a false low 9 dots before the next line's first
            // pattern fetch on dot 5, and a clock.
            data.Add(region, 0x18, 0x20, 337, -1);

            // Background at $1000, sprites at $0000: A12 low since dot 257. A told $1000 on dot 300
            // would be a false rise after a long low, a second clock besides dot 325's.
            data.Add(region, 0x10, 0x10, 300, 325);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Writes2006DuringRendering))]
    public void A2006WriteDuringRenderingIsNotOnTheBusAndGivesNoExtraClock(string name, byte ctrl, byte high, int writeDot, int clockDot)
    {
        Region region = RegionNamed(name);
        List<(int Line, int Dot)> clocks = ClocksInAFrame(region, ctrl, ppu =>
        {
            if (ppu.Line < 240 && ppu.Dot == writeDot)
            {
                ppu.WriteRegister(6, high);
                ppu.WriteRegister(6, 0x00);
            }
        });

        int[] expectedLines = clockDot < 0 ? [] : [.. Enumerable.Range(0, 240), region.PreRenderLine];
        Assert.Equal(expectedLines, clocks.Select(c => c.Line));
        Assert.All(clocks, c => Assert.Equal(clockDot, c.Dot));
    }

    // The (line, dot) of each clock in the second frame of a PPU run alone, latch 0, rendering on
    // from power on: the first frame settles, and every clock is acknowledged as it comes.
    // <paramref name="beforeDot"/>, when given, runs before every dot of both frames.
    private static List<(int Line, int Dot)> ClocksInAFrame(Region region, byte ctrl, Action<Ppu>? beforeDot = null)
    {
        var board = new Clocked(Mmc3Cartridge().CreateMapper());
        var ppu = new Ppu(region, board);
        ppu.PowerOn();
        board.CpuWrite(0xC000, 0);
        board.CpuWrite(0xC001, 0);
        board.CpuWrite(0xE001, 0);
        ppu.WriteRegister(0, ctrl);
        ppu.WriteRegister(1, 0x18);

        long dots = 0;
        var clocks = new List<(int Line, int Dot)>();
        while (ppu.Frame < 2)
        {
            beforeDot?.Invoke(ppu);
            (long frame, int line, int dot) = (ppu.Frame, ppu.Line, ppu.Dot);
            ppu.Tick();
            dots++;
            board.Cycle = dots * region.DotsDenominator / region.DotsNumerator;
            if (board.Irq)
            {
                if (frame == 1)
                {
                    clocks.Add((line, dot));
                }

                board.CpuWrite(0xE000, 0);
                board.CpuWrite(0xE001, 0);
            }
        }

        return clocks;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheBoardIsToldTheBusCycleOfEachPpuAccess(string name)
    {
        // A12 is set low by two $2006 writes and high by two more. The high comes in the cycle of
        // the second write, so with no cycle between the pairs it is 2 cycles after the low (two
        // M2 falls: filtered out), and with one read between, 3 (a clock). Had the board been told
        // any other cycle, the two would not differ.
        foreach ((int gap, bool clocks) in new[] { (0, false), (1, true), (2, true) })
        {
            var nes = new Nes(Mmc3Cartridge(), RegionNamed(name));
            nes.PowerOn();
            NesBus bus = nes.Bus;

            // Latch 0, reload, IRQs on; then A12 high and acknowledged, so the test starts from high.
            bus.Write(0xC000, 0);
            bus.Write(0xC001, 0);
            bus.Write(0xE001, 0);
            bus.Write(0x2006, 0x10);
            bus.Write(0x2006, 0x00);
            bus.Write(0xE000, 0);
            bus.Write(0xE001, 0);

            bus.Write(0x2006, 0x00);
            bus.Write(0x2006, 0x00);
            for (int i = 0; i < gap; i++)
            {
                bus.Read(0x0000);
            }

            bus.Write(0x2006, 0x10);
            bus.Write(0x2006, 0x00);

            // The line is the board's as the next cycle begins: not seen in the write's own cycle.
            Assert.False(nes.Cpu.Irq);
            bus.Read(0x0000);
            Assert.Equal(clocks, nes.Cpu.Irq);
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void OnTheBusTheIrqReachesTheCpuOnceALineAndTwoHundredAndFortyOneTimesAFrame(string name)
    {
        Region region = RegionNamed(name);
        var nes = new Nes(Mmc3Cartridge(), region);
        nes.PowerOn();
        NesBus bus = nes.Bus;
        bus.Write(0x4017, 0x40);
        bus.Write(0xC000, 0);
        bus.Write(0xC001, 0);
        bus.Write(0xE001, 0);
        bus.Write(0x2000, 0x08);
        bus.Write(0x2001, 0x18);
        while (bus.Ppu.Frame < 1)
        {
            bus.Read(0x0000);
        }

        // The settling frame's last IRQ is still held: acknowledge it.
        bus.Write(0xE000, 0);
        bus.Write(0xE001, 0);

        // The CPU sees the line a cycle after the board raised it, so the clock was in the cycle
        // before the one after which Cpu.Irq is first true: the dots between the two positions
        // noted before that cycle and after it.
        var lines = new List<int>();
        (int Line, int Dot) beforeLast = (bus.Ppu.Line, bus.Ppu.Dot);
        while (bus.Ppu.Frame < 3)
        {
            (int Line, int Dot) before = (bus.Ppu.Line, bus.Ppu.Dot);
            bus.Read(0x0000);
            if (nes.Cpu.Irq)
            {
                Assert.Equal(before.Line, beforeLast.Line);
                Assert.InRange(ClockDot, beforeLast.Dot, before.Dot - 1);
                lines.Add(before.Line);
                bus.Write(0xE000, 0);
                bus.Write(0xE001, 0);
                Assert.False(nes.Cpu.Irq);
            }

            beforeLast = before;
        }

        int[] frame = [.. Enumerable.Range(0, 240), region.PreRenderLine];
        Assert.Equal([.. frame, .. frame], lines);
    }
}
