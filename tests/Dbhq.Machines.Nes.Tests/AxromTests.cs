using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// AxROM, mapper 7, from <c>docs/nes/facts/mappers.md</c> section 5. Each 32 KB PRG bank holds its
/// own number in every byte, so a read says which bank it saw.
/// </summary>
public class AxromTests
{
    private static IMapper Board(int prgBanks = 4, int submapper = 0)
    {
        return Cartridge.Load(TestCartridge.Banked(7, prgBanks, 32768, submapper: submapper)).CreateMapper();
    }

    private static byte Cpu(IMapper mapper, int address) => mapper.CpuRead((ushort)address, 0xEE);

    [Fact]
    public void AtPowerOnBankZeroFillsTheCpuSpaceAndTheNametablePageIsTheLower()
    {
        IMapper mapper = Board();

        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(0, Cpu(mapper, 0xFFFF));
        Assert.Equal(Mirroring.SingleScreenLow, mapper.Mirroring);
    }

    [Fact]
    public void AWriteSelectsThe32KilobyteBankForTheWholeOf8000ToFFFF()
    {
        IMapper mapper = Board();

        mapper.CpuWrite(0x8000, 2);

        foreach (int address in new[] { 0x8000, 0x9FFF, 0xA000, 0xBFFF, 0xC000, 0xDFFF, 0xE000, 0xFFFF })
        {
            Assert.Equal(2, Cpu(mapper, address));
        }
    }

    [Fact]
    public void Bit4ChoosesTheNametablePageAndBit3IsIgnored()
    {
        IMapper mapper = Board();

        mapper.CpuWrite(0x8000, 0x13);

        Assert.Equal(3, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.SingleScreenHigh, mapper.Mirroring);

        mapper.CpuWrite(0xFFFF, 0x0A);

        Assert.Equal(2, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.SingleScreenLow, mapper.Mirroring);
    }

    [Fact]
    public void OnlyThreeBitsChooseTheBankSoABoardWithFewerBanksWraps()
    {
        IMapper mapper = Board(prgBanks: 2);

        mapper.CpuWrite(0x8000, 5);

        Assert.Equal(1, Cpu(mapper, 0x8000));
        Assert.Equal(1, Cpu(mapper, 0xFFFF));
    }

    [Fact]
    public void ASixteenKilobytePrgIsHalfABankAndIsRepeatedNeverReadOutOfRange()
    {
        // One 16 KB bank: the 32 KB window shows it twice, whatever bank is written.
        IMapper mapper = Cartridge.Load(TestCartridge.Banked(7, 1)).CreateMapper();

        for (int value = 0; value < 256; value++)
        {
            mapper.CpuWrite(0x8000, (byte)value);
            Assert.Equal(0, Cpu(mapper, 0x8000));
            Assert.Equal(0, Cpu(mapper, 0xC000));
            Assert.Equal(0, Cpu(mapper, 0xFFFF));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WithoutASubmapperForConflictsTheWriteIsTakenWhole(int submapper)
    {
        IMapper mapper = Board(submapper: submapper);

        // The ROM at $8000 is bank 0, whose bytes are 0: a conflict would make this 0.
        mapper.CpuWrite(0x8000, 0x12);

        Assert.Equal(2, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.SingleScreenHigh, mapper.Mirroring);
    }

    [Fact]
    public void Submapper2ANDsTheWriteWithTheRomByteAtThatAddress()
    {
        // Bank 0's bytes are 0 in a banked file, so a conflict leaves it stuck at 0.
        IMapper stuck = Board(submapper: 2);
        stuck.CpuWrite(0x8000, 0x12);
        Assert.Equal(0, Cpu(stuck, 0x8000));
        Assert.Equal(Mirroring.SingleScreenLow, stuck.Mirroring);

        // With bank 0's bytes set to $13, 0x1B AND 0x13 = 0x13: bank 3, upper page; bit 3 is lost
        // to the AND as well as to the register.
        byte[] file = TestCartridge.Banked(7, 4, 32768, submapper: 2);
        Array.Fill(file, (byte)0x13, 16, 32768);
        IMapper mapper = Cartridge.Load(file).CreateMapper();

        mapper.CpuWrite(0x8000, 0x1B);

        Assert.Equal(3, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.SingleScreenHigh, mapper.Mirroring);

        // Now the ROM byte is bank 3's, 3: 0x1B AND 3 = 3, and the page bit is lost.
        mapper.CpuWrite(0x8000, 0x1B);

        Assert.Equal(3, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.SingleScreenLow, mapper.Mirroring);
    }

    [Fact]
    public void ChrIsEightKilobytesOfRamThatReadsBack()
    {
        IMapper mapper = Board();

        mapper.PpuWrite(0x0000, 0xA5);
        mapper.PpuWrite(0x1FFF, 0x5A);

        Assert.Equal(0xA5, mapper.PpuRead(0x0000));
        Assert.Equal(0x5A, mapper.PpuRead(0x1FFF));
    }

    [Fact]
    public void ThePowerOnResetsTheBankThePageAndTheChrRamAndTheResetButtonDoesNot()
    {
        IMapper mapper = Board();
        mapper.CpuWrite(0x8000, 0x12);
        mapper.PpuWrite(0x0010, 0x77);

        mapper.Reset(false);

        Assert.Equal(2, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.SingleScreenHigh, mapper.Mirroring);
        Assert.Equal(0x77, mapper.PpuRead(0x0010));

        mapper.Reset(true);

        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.SingleScreenLow, mapper.Mirroring);
        Assert.Equal(0, mapper.PpuRead(0x0010));
    }

    [Fact]
    public void PrgRamFollowsTheHeaderAndAPowerCycleClearsIt()
    {
        IMapper mapper = Cartridge.Load(TestCartridge.Banked(7, 4, 32768, prgRamUnits: 1)).CreateMapper();
        mapper.CpuWrite(0x6000, 0x12);
        Assert.Equal(0x12, Cpu(mapper, 0x6000));

        mapper.Reset(false);
        Assert.Equal(0x12, Cpu(mapper, 0x6000));

        mapper.ClearPrgRam();
        Assert.Equal(0, Cpu(mapper, 0x6000));
    }
}
