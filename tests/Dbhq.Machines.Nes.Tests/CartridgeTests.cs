using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The header parser, from <c>docs/nes/facts/cartridge.md</c>. Every file is built here from the
/// format's description. The bad-file cases are the plan's Review Focus item 1: each gives one
/// plain sentence and none crashes, hangs or allocates what the header asks for.
/// </summary>
public class CartridgeTests
{
    [Fact]
    public void AnInes1FileParsesToItsHeader()
    {
        byte[] file = TestCartridge.Ines1(prgBanks: 1, chrBanks: 1, mapper: 0, verticalMirroring: true, prgFill: 0xAA, chrFill: 0x55);

        Cartridge cartridge = Cartridge.Load(file);

        Assert.Equal(0, cartridge.Mapper);
        Assert.Equal(0, cartridge.Submapper);
        Assert.Equal(16384, cartridge.Prg.Length);
        Assert.Equal(8192, cartridge.Chr.Length);
        Assert.All(cartridge.Prg, b => Assert.Equal(0xAA, b));
        Assert.All(cartridge.Chr, b => Assert.Equal(0x55, b));
        Assert.False(cartridge.ChrIsRam);
        Assert.Equal(Mirroring.Vertical, cartridge.Mirroring);
        Assert.False(cartridge.Battery);
    }

    [Fact]
    public void HorizontalMirroringIsFlag6Bit0Clear()
    {
        Cartridge cartridge = Cartridge.Load(TestCartridge.Ines1(1, 1, verticalMirroring: false));
        Assert.Equal(Mirroring.Horizontal, cartridge.Mirroring);
    }

    [Fact]
    public void FourScreenIsFlag6Bit3AndBeatsBit0()
    {
        Cartridge cartridge = Cartridge.Load(TestCartridge.Ines1(1, 1, verticalMirroring: true, fourScreen: true));
        Assert.Equal(Mirroring.FourScreen, cartridge.Mirroring);
    }

    [Fact]
    public void ThePrgAndChrAreWhatTheFileHoldsInOrder()
    {
        byte[] prg = TestCartridge.Pattern(32768);
        byte[] chr = TestCartridge.Pattern(8192, offset: 7);
        byte[] file = TestCartridge.Join(TestCartridge.Header().Select((b, i) => i == 4 ? (byte)2 : i == 5 ? (byte)1 : b).ToArray(), prg, chr);

        Cartridge cartridge = Cartridge.Load(file);

        Assert.Equal(prg, cartridge.Prg);
        Assert.Equal(chr, cartridge.Chr);
    }

    [Fact]
    public void ATrainerIsSkippedSoPrgStarts512BytesLater()
    {
        byte[] prg = TestCartridge.Pattern(16384);
        byte[] header = TestCartridge.Header();
        header[4] = 1;
        header[5] = 0;
        header[6] = 0x04;
        byte[] file = TestCartridge.Join(header, TestCartridge.Filled(512, 0xEE), prg);

        Cartridge cartridge = Cartridge.Load(file);

        Assert.Equal(prg, cartridge.Prg);
        Assert.NotEqual(0xEE, cartridge.Prg[0]);
    }

    [Fact]
    public void AChrSizeOfZeroGivesEightKilobytesOfChrRam()
    {
        Cartridge cartridge = Cartridge.Load(TestCartridge.Ines1(prgBanks: 2, chrBanks: 0));

        Assert.True(cartridge.ChrIsRam);
        Assert.Equal(8192, cartridge.Chr.Length);
        Assert.All(cartridge.Chr, b => Assert.Equal(0, b));
    }

    [Theory]
    [InlineData(0, 8192)]
    [InlineData(1, 8192)]
    [InlineData(2, 16384)]
    public void InesPrgRamIsInUnitsOfEightKilobytesAndZeroMeansEight(int unitsInHeader, int bytes)
    {
        // cartridge.md 1, byte 8. The Blargg ROMs write their result to $6000 with byte 8 = 0.
        Cartridge cartridge = Cartridge.Load(TestCartridge.Ines1(1, 1, prgRamUnits: unitsInHeader));
        Assert.Equal(bytes, cartridge.PrgRamSize);
    }

    [Fact]
    public void TheBatteryIsFlag6Bit1()
    {
        Assert.True(Cartridge.Load(TestCartridge.Ines1(1, 1, battery: true)).Battery);
        Assert.False(Cartridge.Load(TestCartridge.Ines1(1, 1, battery: false)).Battery);
    }

    [Fact]
    public void TheMapperIsBothNibblesOfAnInesHeader()
    {
        // Mapper 0x42 is not supported, but it is read.
        Cartridge cartridge = Cartridge.Load(TestCartridge.Ines1(1, 1, mapper: 0x42));
        Assert.Equal(0x42, cartridge.Mapper);
    }

    [Fact]
    public void JunkInBytes12To15KeepsOnlyTheLowMapperNibble()
    {
        // "DiskDude!" style junk adds 64 to the mapper (cartridge.md 1): the high nibble is ignored.
        byte[] file = TestCartridge.Ines1(1, 1, mapper: 0x41);
        file[12] = (byte)'d';
        file[13] = (byte)'e';

        Assert.Equal(1, Cartridge.Load(file).Mapper);
    }

    [Fact]
    public void ANes20FileReadsTheTwelveBitMapperAndTheSubmapper()
    {
        byte[] file = TestCartridge.Nes2(prgBanks: 1, chrBanks: 1, mapper: 0x5A3, submapper: 9);

        Cartridge cartridge = Cartridge.Load(file);

        Assert.Equal(0x5A3, cartridge.Mapper);
        Assert.Equal(9, cartridge.Submapper);
    }

    [Fact]
    public void ANes20FileReadsTheHighNibbleOfTheChrSize()
    {
        // 256 banks of 8 KB CHR (2 MB) needs the high nibble in byte 9. The same nibble for PRG
        // starts at 256 banks of 16 KB, which is 4 MB and over the limit, so it cannot be loaded.
        byte[] file = TestCartridge.Nes2(prgBanks: 1, chrBanks: 256);

        Cartridge cartridge = Cartridge.Load(file);

        Assert.Equal(16384, cartridge.Prg.Length);
        Assert.Equal(256 * 8192, cartridge.Chr.Length);
        Assert.False(cartridge.ChrIsRam);
    }

    [Fact]
    public void ANes20FileReadsTheExponentMultiplierSizes()
    {
        // Byte 9 high nibble $F: byte 5 is EEEEEEMM, size 2^E x (MM x 2 + 1). E = 13, MM = 1: 8192 x 3.
        // The same form for PRG: byte 4 = E = 14, MM = 0: 16384 x 1.
        byte[] header = TestCartridge.Header();
        header[4] = (byte)((14 << 2) | 0);
        header[5] = (byte)((13 << 2) | 1);
        header[7] = 0x08;
        header[9] = 0xFF;
        byte[] prg = TestCartridge.Pattern(16384);
        byte[] chr = TestCartridge.Pattern(8192 * 3, offset: 3);

        Cartridge cartridge = Cartridge.Load(TestCartridge.Join(header, prg, chr));

        Assert.Equal(prg, cartridge.Prg);
        Assert.Equal(chr, cartridge.Chr);
        Assert.False(cartridge.ChrIsRam);
    }

    [Fact]
    public void ANes20ExponentSizeTooBigForTheFileIsRefusedWithoutAllocatingIt()
    {
        // E = 63, MM = 3 asks for about 6.4 x 10^19 bytes.
        byte[] header = TestCartridge.Header();
        header[4] = 0xFF;
        header[5] = 0;
        header[7] = 0x08;
        header[9] = 0x0F;

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(header.Concat(new byte[100]).ToArray()));

        AssertPlainSentence(error.Message);
    }

    [Fact]
    public void ANes20FileWithNoChrRomAndAChrRamNibbleHasThatMuchChrRam()
    {
        // 64 << 7 = 8192.
        Cartridge cartridge = Cartridge.Load(TestCartridge.Nes2(prgBanks: 1, chrBanks: 0, chrRamShift: 7));

        Assert.True(cartridge.ChrIsRam);
        Assert.Equal(8192, cartridge.Chr.Length);
    }

    [Fact]
    public void ANes20FileWithNoChrAtAllHasNone()
    {
        // cartridge.md 2: with a NES 2.0 header, no CHR ROM does not imply 8 KB of CHR RAM.
        Cartridge cartridge = Cartridge.Load(TestCartridge.Nes2(prgBanks: 1, chrBanks: 0));

        Assert.False(cartridge.ChrIsRam);
        Assert.Empty(cartridge.Chr);
    }

    [Fact]
    public void ANes20FileReadsThePrgRamAndBatterySizes()
    {
        // Volatile 64 << 7 = 8192; non-volatile 64 << 5 = 2048, which sets the battery.
        Cartridge cartridge = Cartridge.Load(TestCartridge.Nes2(1, 1, prgRamShift: 7, prgNvRamShift: 5));

        Assert.Equal(8192 + 2048, cartridge.PrgRamSize);
        Assert.True(cartridge.Battery);
    }

    [Fact]
    public void ANes20FileWithNoPrgRamHasNone()
    {
        Cartridge cartridge = Cartridge.Load(TestCartridge.Nes2(1, 1));

        Assert.Equal(0, cartridge.PrgRamSize);
        Assert.False(cartridge.Battery);
    }

    [Theory]
    [InlineData(0, "NTSC")]
    [InlineData(1, "PAL")]
    [InlineData(2, null)]
    public void ANes20FileNamesItsRegionInByte12(int timing, string? expected)
    {
        Region? region = Cartridge.Load(TestCartridge.Nes2(1, 1, timing: timing)).HeaderRegion;

        Assert.Equal(expected, region?.Name);
        if (expected == "NTSC")
        {
            Assert.Same(Region.Ntsc, region);
        }

        if (expected == "PAL")
        {
            Assert.Same(Region.Pal, region);
        }
    }

    [Fact]
    public void ANes20FileForTheDendyIsRefusedByName()
    {
        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(TestCartridge.Nes2(1, 1, timing: 3)));

        Assert.Contains("Dendy", error.Message);
        AssertPlainSentence(error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AnInesFileNamesNoRegionWhateverItsTvSystemBitsSay(int tvSystemBit)
    {
        // cartridge.md 3: byte 9 bit 0 is not trusted. Byte 10 is set too, as the unofficial form.
        byte[] file = TestCartridge.Ines1(1, 1);
        file[9] = (byte)tvSystemBit;
        file[10] = (byte)(tvSystemBit == 1 ? 1 : 0);

        Assert.Null(Cartridge.Load(file).HeaderRegion);
    }

    [Fact]
    public void AnEmptyFileIsRefused()
    {
        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load([]));

        AssertPlainSentence(error.Message);
        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFileOf15BytesIsRefusedAsShorterThanAHeader()
    {
        byte[] file = TestCartridge.Header()[..15];

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(file));

        AssertPlainSentence(error.Message);
        Assert.Contains("16", error.Message);
    }

    [Fact]
    public void AFileWithTheWrongMagicIsRefused()
    {
        byte[] file = TestCartridge.Ines1(1, 1);
        file[3] = 0x1B;

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(file));

        AssertPlainSentence(error.Message);
        Assert.Contains("iNES", error.Message);
    }

    [Fact]
    public void APrgSizeThatRunsPastTheEndOfTheFileIsRefused()
    {
        // The header says 2 banks (32 KB) and the file holds one.
        byte[] file = TestCartridge.Ines1(1, 0);
        file[4] = 2;

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(file));

        AssertPlainSentence(error.Message);
        Assert.Contains("shorter", error.Message);
    }

    [Fact]
    public void AChrSizeThatRunsPastTheEndOfTheFileIsRefused()
    {
        byte[] file = TestCartridge.Ines1(1, 1);
        file[5] = 2;

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(file));

        AssertPlainSentence(error.Message);
    }

    [Fact]
    public void ATrainerThatRunsPastTheEndOfTheFileIsRefused()
    {
        // The trainer bit is set and the file has the header and PRG but no room for 512 more bytes.
        byte[] file = TestCartridge.Ines1(1, 0);
        file[6] |= 0x04;

        Assert.Throws<NesFormatException>(() => Cartridge.Load(file));
    }

    [Fact]
    public void APrgSizeOfZeroIsRefused()
    {
        byte[] file = TestCartridge.Header();

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(file.Concat(new byte[8192]).ToArray()));

        AssertPlainSentence(error.Message);
        Assert.Contains("program", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMapperThatIsNotSupportedIsRefusedWhenItsBoardIsAskedForAndTheMessageNamesIt()
    {
        // Mapper 5 is the MMC5, which is not modelled. Load reads the header; the board is refused.
        Cartridge cartridge = Cartridge.Load(TestCartridge.Ines1(1, 1, mapper: 5));

        NesFormatException error = Assert.Throws<NesFormatException>(() => cartridge.CreateMapper());

        AssertPlainSentence(error.Message);
        Assert.Contains("mapper 5", error.Message);
        Assert.Contains("mappers: 0", error.Message);
    }

    [Fact]
    public void ATwelveBitMapperIsNamedInFullInTheMessage()
    {
        Cartridge cartridge = Cartridge.Load(TestCartridge.Nes2(1, 1, mapper: 0x123));

        NesFormatException error = Assert.Throws<NesFormatException>(() => cartridge.CreateMapper());

        Assert.Contains("mapper 291", error.Message);
    }

    [Fact]
    public void AFileOverTheSizeLimitIsRefusedBeforeItsHeaderIsRead()
    {
        // One byte over the limit, and not even an iNES file: the size is the first thing checked.
        byte[] file = new byte[Cartridge.MaxFileSize + 1];

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(file));

        AssertPlainSentence(error.Message);
        Assert.Contains("4 MB", error.Message);
    }

    [Fact]
    public void AFileJustUnderTheSizeLimitIsNotRefusedForItsSize()
    {
        // 255 banks of 16 KB PRG and a header is 4,177,936 bytes, the largest whole-bank file the
        // limit lets in.
        byte[] file = TestCartridge.Nes2(prgBanks: 255, chrBanks: 0);

        Cartridge cartridge = Cartridge.Load(file);

        Assert.True(file.Length <= Cartridge.MaxFileSize);
        Assert.Equal(255 * 16384, cartridge.Prg.Length);
    }

    [Fact]
    public void AHostileHeaderThatAsksForHugeSizesInAShortFileFailsWithoutAllocating()
    {
        // NES 2.0, PRG and CHR both in the exponent form with E = 63, in a file of 16 bytes.
        byte[] header = TestCartridge.Header();
        header[4] = 0xFC;
        header[5] = 0xFC;
        header[7] = 0x08;
        header[9] = 0xFF;

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(header));

        AssertPlainSentence(error.Message);
    }

    [Fact]
    public void AHostileHeaderThatAsksForTwelveBitBankCountsInAShortFileFailsWithoutAllocating()
    {
        // NES 2.0, PRG 2047 banks (32 MB) and CHR 2047 banks (16 MB), in a file of 16 bytes.
        byte[] header = TestCartridge.Header();
        header[4] = 0xFF;
        header[5] = 0xFF;
        header[7] = 0x08;
        header[9] = 0x77;

        NesFormatException error = Assert.Throws<NesFormatException>(() => Cartridge.Load(header));

        AssertPlainSentence(error.Message);
    }

    [Fact]
    public void AMapperCanBeCreatedMoreThanOnceAndEachOwnsItsChrRam()
    {
        Cartridge cartridge = Cartridge.Load(TestCartridge.Ines1(1, 0));
        IMapper first = cartridge.CreateMapper();
        IMapper second = cartridge.CreateMapper();

        first.PpuWrite(0x0000, 0x77);

        Assert.Equal(0x77, first.PpuRead(0x0000));
        Assert.Equal(0x00, second.PpuRead(0x0000));
    }

    /// <summary>One sentence for a page: not empty, one line, ends with a full stop, no dashes.</summary>
    private static void AssertPlainSentence(string message)
    {
        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.DoesNotContain('\n', message);
        Assert.EndsWith(".", message);
        Assert.Equal(1, message.Count(c => c == '.'));
        Assert.DoesNotContain('\u2013', message);
        Assert.DoesNotContain('\u2014', message);
    }
}
