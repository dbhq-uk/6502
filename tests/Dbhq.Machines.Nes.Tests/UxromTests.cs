using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// UxROM, mapper 2, from <c>docs/nes/facts/mappers.md</c> section 4. Each PRG bank holds its own
/// number in every byte (<see cref="TestCartridge.Banked"/>), so a read says which bank it saw.
/// </summary>
public class UxromTests
{
    private static IMapper Board(int prgBanks = 8, int submapper = 0, bool verticalMirroring = false)
    {
        return Cartridge.Load(TestCartridge.Banked(2, prgBanks, submapper: submapper, verticalMirroring: verticalMirroring)).CreateMapper();
    }

    private static byte Cpu(IMapper mapper, int address) => mapper.CpuRead((ushort)address, 0xEE);

    [Fact]
    public void AtPowerOnTheFirstBankIsAt8000AndTheLastAtC000()
    {
        IMapper mapper = Board();

        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(0, Cpu(mapper, 0xBFFF));
        Assert.Equal(7, Cpu(mapper, 0xC000));
        Assert.Equal(7, Cpu(mapper, 0xFFFF));
    }

    [Theory]
    [InlineData(0x8000)]
    [InlineData(0xBFFF)]
    [InlineData(0xC000)]
    [InlineData(0xFFFF)]
    public void AWriteAnywhereIn8000ToFFFFSelectsTheBankAt8000AndTheLastStaysAtC000(int address)
    {
        IMapper mapper = Board();

        mapper.CpuWrite((ushort)address, 3);

        Assert.Equal(3, Cpu(mapper, 0x8000));
        Assert.Equal(3, Cpu(mapper, 0xBFFF));
        Assert.Equal(7, Cpu(mapper, 0xC000));
        Assert.Equal(7, Cpu(mapper, 0xFFFF));
    }

    [Fact]
    public void TheRegisterIsAFullEightBitsWithNoBusConflicts()
    {
        // 256 banks cannot be built in a test file, so a 16-bank board shows the low four bits
        // arriving whole: bank 12 from a write of 12, written where the ROM byte is 0.
        IMapper mapper = Board(prgBanks: 16);

        mapper.CpuWrite(0x8000, 12);

        Assert.Equal(12, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void ABankNumberPastTheEndWrapsAndNeverReadsOutOfRange()
    {
        IMapper mapper = Board(prgBanks: 8);

        mapper.CpuWrite(0x8000, 0xFB);

        Assert.Equal(3, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void ACartridgeWithOneBankReadsItEverywhere()
    {
        IMapper mapper = Board(prgBanks: 1);

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

        // $8000 holds bank 0, whose bytes are 0: a bus conflict would make this a write of 0.
        mapper.CpuWrite(0x8000, 3);

        Assert.Equal(3, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void Submapper2ANDsTheWriteWithTheRomByteAtThatAddress()
    {
        IMapper mapper = Board(submapper: 2);

        // $8000 is in bank 0, so the ROM byte is 0: 3 AND 0 = bank 0.
        mapper.CpuWrite(0x8000, 3);
        Assert.Equal(0, Cpu(mapper, 0x8000));

        // $C000 is in bank 7, so the ROM byte is 7: 3 AND 7 = 3, and 0x0B AND 7 = 3.
        mapper.CpuWrite(0xC000, 3);
        Assert.Equal(3, Cpu(mapper, 0x8000));
        mapper.CpuWrite(0xC000, 0x0B);
        Assert.Equal(3, Cpu(mapper, 0x8000));

        // Now bank 3 is at $8000, so a write there ANDs with 3: 6 AND 3 = 2.
        mapper.CpuWrite(0x8000, 6);
        Assert.Equal(2, Cpu(mapper, 0x8000));
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
    public void TheMirroringIsTheHeaders()
    {
        Assert.Equal(Mirroring.Vertical, Board(verticalMirroring: true).Mirroring);
        Assert.Equal(Mirroring.Horizontal, Board(verticalMirroring: false).Mirroring);
    }

    [Fact]
    public void ThePowerOnResetsTheBankAndTheChrRamAndTheResetButtonDoesNot()
    {
        IMapper mapper = Board();
        mapper.CpuWrite(0x8000, 5);
        mapper.PpuWrite(0x0010, 0x77);

        mapper.Reset(false);

        Assert.Equal(5, Cpu(mapper, 0x8000));
        Assert.Equal(0x77, mapper.PpuRead(0x0010));

        mapper.Reset(true);

        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(0, mapper.PpuRead(0x0010));
    }

    [Fact]
    public void PrgRamFollowsTheHeaderAndAPowerCycleClearsIt()
    {
        IMapper mapper = Cartridge.Load(TestCartridge.Banked(2, 8, prgRamUnits: 1)).CreateMapper();
        mapper.CpuWrite(0x6000, 0x12);

        Assert.Equal(0x12, Cpu(mapper, 0x6000));

        mapper.Reset(false);
        Assert.Equal(0x12, Cpu(mapper, 0x6000));

        mapper.ClearPrgRam();
        Assert.Equal(0, Cpu(mapper, 0x6000));
    }
}
