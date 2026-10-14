using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>NROM, from <c>docs/nes/facts/mappers.md</c> section 2.</summary>
public class NromTests
{
    private static IMapper Board(byte[] file) => Cartridge.Load(file).CreateMapper();

    [Fact]
    public void ASixteenKilobytePrgIsMirroredIntoBothHalvesOfTheCpuSpace()
    {
        byte[] prg = TestCartridge.Pattern(16384);
        IMapper mapper = Board(TestCartridge.Join(
            TestCartridge.Ines1(0, 0)[..4], [1, 1], new byte[10], prg, new byte[8192]));

        Assert.Equal(prg[0], mapper.CpuRead(0x8000, 0xFF));
        Assert.Equal(prg[0], mapper.CpuRead(0xC000, 0xFF));
        Assert.Equal(prg[0x1234], mapper.CpuRead(0x9234, 0xFF));
        Assert.Equal(prg[0x1234], mapper.CpuRead(0xD234, 0xFF));
        Assert.Equal(prg[0x3FFF], mapper.CpuRead(0xFFFF, 0xFF));
        Assert.Equal(prg[0x3FFF], mapper.CpuRead(0xBFFF, 0xFF));
    }

    [Fact]
    public void AThirtyTwoKilobytePrgIsNotMirrored()
    {
        byte[] prg = TestCartridge.Pattern(32768);
        byte[] header = TestCartridge.Header();
        header[4] = 2;
        header[5] = 1;
        IMapper mapper = Board(TestCartridge.Join(header, prg, new byte[8192]));

        Assert.Equal(prg[0], mapper.CpuRead(0x8000, 0xFF));
        Assert.Equal(prg[0x4000], mapper.CpuRead(0xC000, 0xFF));
        Assert.Equal(prg[0x7FFF], mapper.CpuRead(0xFFFF, 0xFF));
        Assert.NotEqual(mapper.CpuRead(0x8001, 0xFF), mapper.CpuRead(0xC001, 0xFF));
    }

    [Fact]
    public void WritesToPrgAreIgnored()
    {
        IMapper mapper = Board(TestCartridge.Ines1(1, 1, prgFill: 0x42));

        mapper.CpuWrite(0x8000, 0x99);
        mapper.CpuWrite(0xFFFF, 0x99);

        Assert.Equal(0x42, mapper.CpuRead(0x8000, 0x00));
        Assert.Equal(0x42, mapper.CpuRead(0xFFFF, 0x00));
    }

    [Fact]
    public void ChrRomReadsTheFileAndIgnoresWrites()
    {
        byte[] chr = TestCartridge.Pattern(8192, offset: 9);
        IMapper mapper = Board(TestCartridge.Join(
            [0x4E, 0x45, 0x53, 0x1A, 1, 1], new byte[10], TestCartridge.Filled(16384, 0), chr));

        Assert.Equal(chr[0], mapper.PpuRead(0x0000));
        Assert.Equal(chr[0x1FFF], mapper.PpuRead(0x1FFF));

        mapper.PpuWrite(0x0010, (byte)(chr[0x10] ^ 0xFF));

        Assert.Equal(chr[0x10], mapper.PpuRead(0x0010));
    }

    [Fact]
    public void ChrRamWritesReadBack()
    {
        IMapper mapper = Board(TestCartridge.Ines1(1, 0));

        Assert.Equal(0, mapper.PpuRead(0x0123));
        mapper.PpuWrite(0x0123, 0xA5);
        mapper.PpuWrite(0x1FFF, 0x5A);

        Assert.Equal(0xA5, mapper.PpuRead(0x0123));
        Assert.Equal(0x5A, mapper.PpuRead(0x1FFF));
    }

    [Fact]
    public void PrgRamIsAtSixThousandToSevenFFFFWhenTheHeaderSaysSo()
    {
        // iNES 1 byte 8 = 1: 8 KB.
        IMapper mapper = Board(TestCartridge.Ines1(1, 1, prgRamUnits: 1));

        Assert.Equal(8192, mapper.PrgRam.Length);
        mapper.CpuWrite(0x6000, 0x12);
        mapper.CpuWrite(0x7FFF, 0x34);

        Assert.Equal(0x12, mapper.CpuRead(0x6000, 0xFF));
        Assert.Equal(0x34, mapper.CpuRead(0x7FFF, 0xFF));
        Assert.Equal(0x12, mapper.PrgRam[0]);
        Assert.Equal(0x34, mapper.PrgRam[0x1FFF]);
    }

    [Fact]
    public void WithoutPrgRamTheReadReturnsTheOpenBusItWasGivenAndTheWriteIsIgnored()
    {
        // NES 2.0 with PRG RAM sizes of 0: none.
        IMapper mapper = Board(TestCartridge.Nes2(1, 1));

        Assert.Empty(mapper.PrgRam);
        mapper.CpuWrite(0x6000, 0x12);

        Assert.Equal(0xC3, mapper.CpuRead(0x6000, 0xC3));
        Assert.Equal(0x3C, mapper.CpuRead(0x7FFF, 0x3C));
    }

    [Fact]
    public void AnAddressBelowPrgRamIsOpenBus()
    {
        IMapper mapper = Board(TestCartridge.Ines1(1, 1, prgRamUnits: 1));

        Assert.Equal(0x81, mapper.CpuRead(0x4020, 0x81));
        Assert.Equal(0x81, mapper.CpuRead(0x5FFF, 0x81));
    }

    [Fact]
    public void ASmallPrgRamIsMirroredThroughTheWindow()
    {
        // Family BASIC style: 2 KB (64 << 5) at $6000, repeated through $7FFF.
        IMapper mapper = Board(TestCartridge.Nes2(1, 1, prgRamShift: 5));

        Assert.Equal(2048, mapper.PrgRam.Length);
        mapper.CpuWrite(0x6001, 0x77);

        Assert.Equal(0x77, mapper.CpuRead(0x6801, 0x00));
        Assert.Equal(0x77, mapper.CpuRead(0x7801, 0x00));
    }

    [Fact]
    public void ClearPrgRamIsAPowerCycle()
    {
        IMapper mapper = Board(TestCartridge.Ines1(1, 1, prgRamUnits: 1, battery: true));
        mapper.CpuWrite(0x6000, 0x12);

        mapper.ClearPrgRam();

        Assert.Equal(0, mapper.CpuRead(0x6000, 0xFF));
    }

    [Fact]
    public void TheBoardTakesTheHeadersMirroringAndHasNoIrq()
    {
        Assert.Equal(Mirroring.Vertical, Board(TestCartridge.Ines1(1, 1, verticalMirroring: true)).Mirroring);
        Assert.Equal(Mirroring.Horizontal, Board(TestCartridge.Ines1(1, 1, verticalMirroring: false)).Mirroring);

        IMapper mapper = Board(TestCartridge.Ines1(1, 1));
        mapper.PpuAddressChanged(0x1000, 5);
        mapper.CpuCycle();

        Assert.False(mapper.Irq);
    }

    [Fact]
    public void ANes20FileWithNoChrReadsZeroRatherThanFailing()
    {
        IMapper mapper = Board(TestCartridge.Nes2(1, 0));

        Assert.Equal(0, mapper.PpuRead(0x0000));
        mapper.PpuWrite(0x0000, 1);
        Assert.Equal(0, mapper.PpuRead(0x1FFF));
    }
}
