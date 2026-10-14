using Dbhq.Machines.Nes.Mappers;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// What the bus and the PPU rely on when they skip a call to the board or read its memory without
/// one (task 6b): every board says which per-cycle calls it needs, and every memory view a board
/// gives reads what its CpuRead, PpuRead and Mirroring give.
/// </summary>
public class BoardContractTests
{
    [Fact]
    public void EveryBoardSaysWhichPerCycleCallsItNeeds()
    {
        var boards = typeof(Board).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(Board)) && !t.IsAbstract).ToHashSet();
        var checkedBoards = new HashSet<Type>();
        foreach (int number in Cartridge.SupportedMappers)
        {
            IMapper mapper = Cartridge.Load(File(number, prgSize: 0x8000, chrSize: 0x2000)).CreateMapper();
            if (mapper is not Board)
            {
                continue;
            }

            Type type = mapper.GetType();
            checkedBoards.Add(type);

            // Board says false for all three, so a board that overrides a call must say it needs it.
            Assert.True(Overrides(type, nameof(IMapper.PpuAddressChanged)) == mapper.WatchesPpuAddresses, $"{type.Name}: PpuAddressChanged and WatchesPpuAddresses disagree");
            Assert.True(Overrides(type, nameof(IMapper.CpuCycle)) == mapper.CountsCpuCycles, $"{type.Name}: CpuCycle and CountsCpuCycles disagree");
            if (type.GetProperty(nameof(IMapper.Irq))!.GetGetMethod()!.DeclaringType != typeof(Board))
            {
                Assert.True(mapper.CanInterrupt, $"{type.Name} overrides Irq but says it cannot interrupt");
            }
        }

        // Every board in the assembly was reached through a mapper number, so none was missed.
        Assert.Equal(boards.OrderBy(t => t.Name), checkedBoards.OrderBy(t => t.Name));
    }

    public static TheoryData<int, int, int> Sizes()
    {
        var data = new TheoryData<int, int, int>();
        foreach (int number in Cartridge.SupportedMappers)
        {
            foreach (int prg in new[] { 0x2000, 0x4000, 0x6000, 0x8000 })
            {
                foreach (int chr in new[] { 0, 0x1000, 0x2000, 0x4000 })
                {
                    data.Add(number, prg, chr);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public void TheMemoryViewsReadWhatTheBoardReads(int number, int prgSize, int chrSize)
    {
        IMapper mapper = Cartridge.Load(File(number, prgSize, chrSize)).CreateMapper();
        if (mapper is Board)
        {
            Assert.True(mapper.TryGetPrgWindows(out _, out _));
            Assert.True(mapper.TryGetPatternWindows(out _, out _));
            Assert.NotNull(mapper.NametablePageTable);
        }

        // CHR RAM starts at zero: fill it, so a misplaced window shows.
        for (int address = 0; address < 0x2000; address++)
        {
            mapper.PpuWrite((ushort)address, (byte)((address * 7) + (address >> 8)));
        }

        // Bank writes through the board's own registers: random addresses from $8000 and values,
        // with the cycles between them a board that counts cycles needs (MMC1 ignores a write the
        // cycle after one).
        var random = new Random(number * 1000 + prgSize + chrSize);
        for (int write = 0; write < 400; write++)
        {
            mapper.CpuCycle();
            mapper.CpuCycle();
            mapper.CpuWrite((ushort)random.Next(0x8000, 0x10000), (byte)random.Next(256));
            if (write % 40 == 39)
            {
                CheckViews(mapper);
            }
        }
    }

    private static void CheckViews(IMapper mapper)
    {
        if (mapper.TryGetPrgWindows(out byte[]? prg, out int[]? prgWindows))
        {
            byte[] expected = new byte[0x8000];
            byte[] actual = new byte[0x8000];
            for (int address = 0x8000; address < 0x10000; address++)
            {
                expected[address - 0x8000] = mapper.CpuRead((ushort)address, 0x55);
                actual[address - 0x8000] = prg[prgWindows[(address >> 13) & 3] + (address & 0x1FFF)];
            }

            Assert.Equal(expected, actual);
        }

        if (mapper.TryGetPatternWindows(out byte[]? chr, out int[]? chrWindows))
        {
            byte[] expected = new byte[0x2000];
            byte[] actual = new byte[0x2000];
            for (int address = 0; address < 0x2000; address++)
            {
                expected[address] = mapper.PpuRead((ushort)address);
                actual[address] = chr[chrWindows[(address >> 10) & 7] + (address & 0x3FF)];
            }

            Assert.Equal(expected, actual);
        }

        if (mapper.NametablePageTable is { } pages)
        {
            Assert.Equal(Enumerable.Range(0, 4).Select(table => Page(mapper.Mirroring, table)), pages);
        }
    }

    // The page each nametable uses, from mappers.md 1: vertical 0 1 0 1, horizontal 0 0 1 1, single
    // screen all 0 or all 1, four screen 0 1 2 3.
    private static int Page(Mirroring mirroring, int table) => mirroring switch
    {
        Mirroring.Vertical => table & 1,
        Mirroring.Horizontal => table >> 1,
        Mirroring.SingleScreenLow => 0,
        Mirroring.SingleScreenHigh => 1,
        _ => table,
    };

    private static bool Overrides(Type type, string method) => type.GetMethod(method)!.DeclaringType != typeof(Board);

    // A NES 2.0 file with the sizes in the exponent form, so 8 KB, 24 KB and 4 KB can be said; no CHR
    // means 8 KB of CHR RAM. Every byte is a pattern that differs from its neighbours and its bank.
    private static byte[] File(int number, int prgSize, int chrSize)
    {
        byte[] header = TestCartridge.Header();
        header[4] = SizeByte(prgSize);
        header[5] = chrSize == 0 ? (byte)0 : SizeByte(chrSize);
        header[6] = (byte)((number & 0x0F) << 4);
        header[7] = (byte)((number & 0xF0) | 0x08);
        header[9] = (byte)(0x0F | (chrSize == 0 ? 0 : 0xF0));
        header[10] = 0x07;
        header[11] = (byte)(chrSize == 0 ? 0x07 : 0);
        return TestCartridge.Join(header, TestCartridge.Pattern(prgSize), TestCartridge.Pattern(chrSize, 17));
    }

    // 2 to the power E times (2M + 1), as the byte (E << 2) | M.
    private static byte SizeByte(int size)
    {
        int multiplier = size % 3 == 0 ? 3 : 1;
        int exponent = System.Numerics.BitOperations.Log2((uint)(size / multiplier));
        return (byte)((exponent << 2) | (multiplier == 3 ? 1 : 0));
    }
}
