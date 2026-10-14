using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// CNROM, mapper 3, from <c>docs/nes/facts/mappers.md</c> section 5. Each 8 KB CHR bank holds its
/// own number in every byte, and so does each 16 KB PRG bank, so a read says which bank it saw.
/// </summary>
public class CnromTests
{
    private static IMapper Board(int prgBanks = 2, int chrBanks = 4, int submapper = 0, bool verticalMirroring = false)
    {
        return Cartridge.Load(TestCartridge.Banked(3, prgBanks, 16384, chrBanks, 8192, submapper, verticalMirroring: verticalMirroring)).CreateMapper();
    }

    private static byte Cpu(IMapper mapper, int address) => mapper.CpuRead((ushort)address, 0xEE);

    [Fact]
    public void AtPowerOnChrBankZeroIsAtPpu0()
    {
        IMapper mapper = Board();

        Assert.Equal(0, mapper.PpuRead(0x0000));
        Assert.Equal(0, mapper.PpuRead(0x1FFF));
    }

    [Theory]
    [InlineData(0xC000, 1)]
    [InlineData(0x8000, 0)]
    public void AWriteSelectsTheEightKilobyteChrBank(int address, int romByte)
    {
        // A submapper 1 board has no conflicts, so the value is taken whole at any address.
        IMapper mapper = Board(submapper: 1);
        Assert.Equal(romByte, Cpu(mapper, address));

        mapper.CpuWrite((ushort)address, 2);

        Assert.Equal(2, mapper.PpuRead(0x0000));
        Assert.Equal(2, mapper.PpuRead(0x0FFF));
        Assert.Equal(2, mapper.PpuRead(0x1000));
        Assert.Equal(2, mapper.PpuRead(0x1FFF));
    }

    [Fact]
    public void PrgIsNotBankedAndASixteenKilobytePrgIsRepeated()
    {
        IMapper thirtyTwo = Board(prgBanks: 2);
        IMapper sixteen = Board(prgBanks: 1);

        thirtyTwo.CpuWrite(0xC000, 3);

        Assert.Equal(0, Cpu(thirtyTwo, 0x8000));
        Assert.Equal(1, Cpu(thirtyTwo, 0xC000));
        Assert.Equal(0, Cpu(sixteen, 0x8000));
        Assert.Equal(0, Cpu(sixteen, 0xC000));
        Assert.Equal(0, Cpu(sixteen, 0xFFFF));
    }

    [Fact]
    public void ByDefaultTheWriteIsANDedWithTheRomByteAtThatAddress()
    {
        IMapper mapper = Board();

        // $8000 is in PRG bank 0, whose bytes are 0: 3 AND 0 = 0.
        mapper.CpuWrite(0x8000, 3);
        Assert.Equal(0, mapper.PpuRead(0x0000));

        // $C000 is in PRG bank 1, whose bytes are 1: 3 AND 1 = 1.
        mapper.CpuWrite(0xC000, 3);
        Assert.Equal(1, mapper.PpuRead(0x0000));
    }

    [Fact]
    public void Submapper2ANDsAsWellAndSubmapper1DoesNot()
    {
        IMapper conflicts = Board(submapper: 2);
        IMapper none = Board(submapper: 1);

        conflicts.CpuWrite(0x8000, 3);
        none.CpuWrite(0x8000, 3);

        Assert.Equal(0, conflicts.PpuRead(0x0000));
        Assert.Equal(3, none.PpuRead(0x0000));
    }

    [Fact]
    public void ABankNumberPastTheEndWrapsAndNeverReadsOutOfRange()
    {
        IMapper mapper = Board(chrBanks: 4, submapper: 1);

        mapper.CpuWrite(0x8000, 0xFF);
        Assert.Equal(3, mapper.PpuRead(0x1FFF));

        IMapper two = Board(chrBanks: 2, submapper: 1);
        two.CpuWrite(0x8000, 3);
        Assert.Equal(1, two.PpuRead(0x1FFF));

        IMapper one = Board(chrBanks: 1, submapper: 1);
        for (int value = 0; value < 256; value++)
        {
            one.CpuWrite(0x8000, (byte)value);
            Assert.Equal(0, one.PpuRead(0x1FFF));
        }
    }

    [Fact]
    public void ACartridgeWithNoChrRomHasRamThatReadsBack()
    {
        IMapper mapper = Board(chrBanks: 0, submapper: 1);

        mapper.CpuWrite(0x8000, 3);
        mapper.PpuWrite(0x0123, 0xA5);

        Assert.Equal(0xA5, mapper.PpuRead(0x0123));
    }

    [Fact]
    public void ChrRomIgnoresWrites()
    {
        IMapper mapper = Board();

        mapper.PpuWrite(0x0000, 0x77);

        Assert.Equal(0, mapper.PpuRead(0x0000));
    }

    [Fact]
    public void TheMirroringIsTheHeaders()
    {
        Assert.Equal(Mirroring.Vertical, Board(verticalMirroring: true).Mirroring);
        Assert.Equal(Mirroring.Horizontal, Board(verticalMirroring: false).Mirroring);
    }

    [Fact]
    public void ThePowerOnResetsTheBankAndTheResetButtonDoesNot()
    {
        IMapper mapper = Board(submapper: 1);
        mapper.CpuWrite(0x8000, 2);

        mapper.Reset(false);
        Assert.Equal(2, mapper.PpuRead(0x0000));

        mapper.Reset(true);
        Assert.Equal(0, mapper.PpuRead(0x0000));
    }

    [Fact]
    public void PrgRamFollowsTheHeaderAndAPowerCycleClearsIt()
    {
        IMapper mapper = Cartridge.Load(TestCartridge.Banked(3, 2, 16384, 4, 8192, prgRamUnits: 1)).CreateMapper();
        mapper.CpuWrite(0x6000, 0x12);
        Assert.Equal(0x12, Cpu(mapper, 0x6000));

        mapper.Reset(false);
        Assert.Equal(0x12, Cpu(mapper, 0x6000));

        mapper.ClearPrgRam();
        Assert.Equal(0, Cpu(mapper, 0x6000));
    }
}
