using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// MMC1, from <c>docs/nes/facts/mappers.md</c> section 3. Every cartridge is built so a bank holds
/// its own number in every byte (<see cref="TestCartridge.Banked"/>): a PRG bank is 16 KB and a CHR
/// bank is 4 KB here, so a read says which bank the board put at that address.
/// </summary>
public class Mmc1Tests
{
    private const ushort ControlRegister = 0x8000;
    private const ushort ChrRegister0 = 0xA000;
    private const ushort ChrRegister1 = 0xC000;
    private const ushort PrgRegister = 0xE000;

    // Eight 16 KB PRG banks and sixteen 4 KB CHR banks (eight 8 KB units in the file). The CHR
    // count is even, because the file counts CHR in 8 KB.
    private static IMapper Board(
        int prgBanks = 8, int chrBanks4K = 16, bool battery = false, int prgRamUnits = 1)
    {
        byte[] file = TestCartridge.Banked(
            1, prgBanks, 16384, chrBanks4K, 4096, battery: battery, prgRamUnits: prgRamUnits);
        return Cartridge.Load(file).CreateMapper();
    }

    // Two idle cycles, so the next write is not on the cycle after the last.
    private static void Idle(IMapper mapper)
    {
        mapper.CpuCycle();
        mapper.CpuCycle();
    }

    // One write, on a cycle of its own.
    private static void Write(IMapper mapper, ushort address, byte value)
    {
        Idle(mapper);
        mapper.CpuWrite(address, value);
    }

    // A register loaded the way a program does: five writes, low bit first, none consecutive.
    private static void Load(IMapper mapper, ushort register, int value)
    {
        for (int bit = 0; bit < 5; bit++)
        {
            Write(mapper, register, (byte)((value >> bit) & 1));
        }
    }

    private static byte Cpu(IMapper mapper, int address) => mapper.CpuRead((ushort)address, 0xEE);

    private static byte Ppu(IMapper mapper, int address) => mapper.PpuRead((ushort)address);

    [Fact]
    public void AtPowerOnThePrgIsInMode3AndTheMirroringIsOneScreenLower()
    {
        IMapper mapper = Board();

        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(0, Cpu(mapper, 0xBFFF));
        Assert.Equal(7, Cpu(mapper, 0xC000));
        Assert.Equal(7, Cpu(mapper, 0xFFFF));
        Assert.Equal(Mirroring.SingleScreenLow, mapper.Mirroring);
        Assert.False(mapper.Irq);
    }

    [Fact]
    public void TheFifthWriteLoadsTheRegisterAndTheFourBeforeItDoNot()
    {
        IMapper mapper = Board();

        // Bank 5 is %00101, low bit first: 1, 0, 1, 0, 0 (the sheet's worked example 2).
        Write(mapper, 0x8000, 0x80);
        Write(mapper, PrgRegister, 1);
        Write(mapper, PrgRegister, 0);
        Write(mapper, PrgRegister, 1);
        Write(mapper, PrgRegister, 0);

        Assert.Equal(0, Cpu(mapper, 0x8000));

        Write(mapper, PrgRegister, 0);

        Assert.Equal(5, Cpu(mapper, 0x8000));
        Assert.Equal(5, Cpu(mapper, 0xBFFF));
        Assert.Equal(7, Cpu(mapper, 0xC000));
    }

    [Fact]
    public void OnlyTheFifthWritesAddressChoosesTheRegister()
    {
        IMapper mapper = Board();

        // Four writes to the control register's range and the fifth to the PRG register's.
        Write(mapper, 0x8000, 1);
        Write(mapper, 0x9FFF, 0);
        Write(mapper, 0xA000, 1);
        Write(mapper, 0xC123, 0);
        Write(mapper, 0xFFFF, 0);

        Assert.Equal(5, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.SingleScreenLow, mapper.Mirroring);
    }

    [Fact]
    public void AfterTheFifthWriteTheShiftRegisterStartsAgain()
    {
        IMapper mapper = Board();
        Load(mapper, PrgRegister, 3);

        Load(mapper, PrgRegister, 6);

        Assert.Equal(6, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void AWriteWithBit7SetClearsTheShiftRegisterAndSetsPrgMode3()
    {
        IMapper mapper = Board();
        Load(mapper, ControlRegister, 0b10010);
        Load(mapper, PrgRegister, 4);

        // Mode 0 (32 KB): bank 4 at $8000, bank 5 at $C000.
        Assert.Equal(5, Cpu(mapper, 0xC000));

        // Three bits go in, then the reset throws them away: the next five load a clean value.
        Write(mapper, PrgRegister, 1);
        Write(mapper, PrgRegister, 1);
        Write(mapper, PrgRegister, 1);
        Write(mapper, PrgRegister, 0x80);

        Assert.Equal(7, Cpu(mapper, 0xC000));
        Load(mapper, PrgRegister, 2);
        Assert.Equal(2, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void TheResetWriteKeepsTheOtherControlBits()
    {
        IMapper mapper = Board();

        // Vertical (2), PRG mode 0, 4 KB CHR (bit 4).
        Load(mapper, ControlRegister, 0b10010);
        Load(mapper, ChrRegister0, 3);
        Load(mapper, ChrRegister1, 6);
        Write(mapper, ControlRegister, 0xFF);

        Assert.Equal(Mirroring.Vertical, mapper.Mirroring);
        Assert.Equal(3, Ppu(mapper, 0x0000));
        Assert.Equal(6, Ppu(mapper, 0x1000));
        Assert.Equal(7, Cpu(mapper, 0xC000));
    }

    public static TheoryData<int, int, int> PrgModes() => new()
    {
        // control PRG mode, PRG register, then the banks at $8000 and $C000.
        { 0, 5, 4 },
        { 0, 4, 4 },
        { 1, 5, 4 },
        { 1, 7, 6 },
        { 2, 5, 0 },
        { 3, 5, 5 },
    };

    [Theory]
    [MemberData(nameof(PrgModes))]
    public void EachPrgModeMapsItsBanks(int mode, int register, int lowBank)
    {
        IMapper mapper = Board();
        Load(mapper, ControlRegister, mode << 2);
        Load(mapper, PrgRegister, register);

        int highBank = mode switch
        {
            0 or 1 => lowBank + 1,
            2 => register,
            _ => 7,
        };
        Assert.Equal(lowBank, Cpu(mapper, 0x8000));
        Assert.Equal(lowBank, Cpu(mapper, 0xBFFF));
        Assert.Equal(highBank, Cpu(mapper, 0xC000));
        Assert.Equal(highBank, Cpu(mapper, 0xFFFF));
    }

    [Fact]
    public void In8KbChrModeTheLowBitOfTheBankIsIgnoredAndTheSecondRegisterToo()
    {
        IMapper mapper = Board();
        Load(mapper, ControlRegister, 0b01100);
        Load(mapper, ChrRegister0, 5);
        Load(mapper, ChrRegister1, 9);

        Assert.Equal(4, Ppu(mapper, 0x0000));
        Assert.Equal(4, Ppu(mapper, 0x0FFF));
        Assert.Equal(5, Ppu(mapper, 0x1000));
        Assert.Equal(5, Ppu(mapper, 0x1FFF));
    }

    [Fact]
    public void In4KbChrModeEachRegisterChoosesItsHalf()
    {
        IMapper mapper = Board();
        Load(mapper, ControlRegister, 0b11100);
        Load(mapper, ChrRegister0, 3);
        Load(mapper, ChrRegister1, 12);

        Assert.Equal(3, Ppu(mapper, 0x0000));
        Assert.Equal(3, Ppu(mapper, 0x0FFF));
        Assert.Equal(12, Ppu(mapper, 0x1000));
        Assert.Equal(12, Ppu(mapper, 0x1FFF));
    }

    [Theory]
    [InlineData(0, Mirroring.SingleScreenLow)]
    [InlineData(1, Mirroring.SingleScreenHigh)]
    [InlineData(2, Mirroring.Vertical)]
    [InlineData(3, Mirroring.Horizontal)]
    public void TheControlRegistersLowBitsChooseTheMirroring(int bits, Mirroring expected)
    {
        IMapper mapper = Board();

        Load(mapper, ControlRegister, 0b01100 | bits);

        Assert.Equal(expected, mapper.Mirroring);
    }

    [Fact]
    public void TheHeadersMirroringDoesNotMatter()
    {
        byte[] file = TestCartridge.Banked(1, 8, 16384, 2, 4096, verticalMirroring: true);
        IMapper mapper = Cartridge.Load(file).CreateMapper();

        Load(mapper, ControlRegister, 0b01111);

        Assert.Equal(Mirroring.Horizontal, mapper.Mirroring);
    }

    [Fact]
    public void AWriteOnTheCycleAfterAWriteIsIgnored()
    {
        IMapper mapper = Board();

        // Bank 5 is 1, 0, 1, 0, 0. The third write and the one straight after it are a
        // read-modify-write's pair: the second is on the next cycle, so it is ignored, and the
        // load is a write short.
        Write(mapper, PrgRegister, 1);
        Write(mapper, PrgRegister, 0);
        Write(mapper, PrgRegister, 1);
        mapper.CpuCycle();
        mapper.CpuWrite(PrgRegister, 1);
        Write(mapper, PrgRegister, 0);

        Assert.Equal(0, Cpu(mapper, 0x8000));

        // The first of the pair was the one taken: the next write completes bank %00101.
        Write(mapper, PrgRegister, 0);

        Assert.Equal(5, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void ARunOfConsecutiveWritesTakesOnlyTheFirst()
    {
        IMapper mapper = Board();
        Load(mapper, ControlRegister, 0b01111);

        // Three writes on three cycles in a row: the shift register gets one bit.
        Idle(mapper);
        mapper.CpuWrite(PrgRegister, 1);
        mapper.CpuCycle();
        mapper.CpuWrite(PrgRegister, 1);
        mapper.CpuCycle();
        mapper.CpuWrite(PrgRegister, 1);
        for (int i = 0; i < 4; i++)
        {
            Write(mapper, PrgRegister, 0);
        }

        Assert.Equal(1, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void AWriteOneIdleCycleAfterAWriteIsTaken()
    {
        IMapper mapper = Board();

        mapper.CpuCycle();
        mapper.CpuWrite(PrgRegister, 1);
        mapper.CpuCycle();
        mapper.CpuCycle();
        mapper.CpuWrite(PrgRegister, 0);
        Write(mapper, PrgRegister, 1);
        Write(mapper, PrgRegister, 0);
        Write(mapper, PrgRegister, 0);

        Assert.Equal(5, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void TheBit7ResetIsNeverIgnored()
    {
        IMapper mapper = Board();
        Load(mapper, ControlRegister, 0);
        Assert.Equal(1, Cpu(mapper, 0xC000));

        // A write, then a reset on the very next cycle: the reset still happens.
        Idle(mapper);
        mapper.CpuWrite(PrgRegister, 1);
        mapper.CpuCycle();
        mapper.CpuWrite(PrgRegister, 0x80);

        Assert.Equal(7, Cpu(mapper, 0xC000));
    }

    [Fact]
    public void ThroughTheBusAReadModifyWritesSecondWriteIsIgnoredAndWritesAReadApartAreNot()
    {
        var bus = new NesBus(Cartridge.Load(TestCartridge.Banked(1, 8, 16384, 2, 4096)), Region.Ntsc);

        // Bank 5 is 1, 0, 1, 0, 0. A read between two writes puts a cycle between them.
        foreach (byte bit in new byte[] { 1, 0, 1 })
        {
            bus.Write(PrgRegister, bit);
            bus.Read(0x8000);
        }

        // An INC's two writes in a row: the second is ignored, so one bit goes in, not two.
        bus.Write(PrgRegister, 0);
        bus.Write(PrgRegister, 1);

        // Had the second counted, that would have been the fifth bit and the bank would be 5 by now.
        Assert.Equal(0, bus.Peek(0x8000));

        bus.Read(0x8000);
        bus.Write(PrgRegister, 0);

        Assert.Equal(5, bus.Peek(0x8000));
    }

    // A real INC $E000 on the CPU, through the bus: it reads $E000 (7, bank 7's fill), writes the 7
    // back and then 8 on the next cycle. Only the first write reaches the shift register, so
    // with four STA $E000 of 1 after it the register is 1,1,1,1,1 and bank 15 (7 of the file's 8)
    // is at $8000 (read past the program, which bank 7 also holds). Had the second write counted,
    // the bits would be 1,0,1,1,1 and the bank 13 (5): with the rule switched off once, it was.
    [Fact]
    public void ARealIncOnTheCpuLoadsOneBitNotTwo()
    {
        byte[] file = TestCartridge.Banked(1, 8, 16384, 2, 4096);
        byte[] program =
        [
            0xEE, 0x00, 0xE0,
            0xA9, 0x01,
            0x8D, 0x00, 0xE0,
            0x8D, 0x00, 0xE0,
            0x8D, 0x00, 0xE0,
            0x8D, 0x00, 0xE0,
            0x4C, 0x11, 0xC0,
        ];

        // Bank 7 is fixed at $C000 at power on (control $0C); the program starts it, and the reset
        // vector points there.
        int bank7 = 16 + (7 * 16384);
        program.CopyTo(file, bank7);
        file[bank7 + 0x3FFC] = 0x00;
        file[bank7 + 0x3FFD] = 0xC0;
        var nes = new Nes(Cartridge.Load(file), Region.Ntsc);
        nes.PowerOn();

        while (nes.Cpu.PC != 0xC011)
        {
            nes.Step();
        }

        Assert.Equal(7, nes.Bus.Peek(0x8100));
    }

    [Fact]
    public void ThePrgRamIsAtSixThousandAndTheBankRegisterCanDisableIt()
    {
        IMapper mapper = Board(prgRamUnits: 1);
        mapper.CpuWrite(0x6000, 0x12);
        mapper.CpuWrite(0x7FFF, 0x34);

        Assert.Equal(0x12, Cpu(mapper, 0x6000));
        Assert.Equal(0x34, Cpu(mapper, 0x7FFF));

        // Bit 4 of the PRG register disables it: reads give the open bus and writes do nothing.
        Load(mapper, PrgRegister, 0b10000);
        mapper.CpuWrite(0x6000, 0x99);

        Assert.Equal(0xEE, Cpu(mapper, 0x6000));
        Assert.Equal(0xEE, Cpu(mapper, 0x7FFF));

        Load(mapper, PrgRegister, 0);

        Assert.Equal(0x12, Cpu(mapper, 0x6000));
    }

    [Fact]
    public void ABoardWithNoPrgRamGivesTheOpenBus()
    {
        // NES 2.0 with the PRG RAM sizes in byte 10 cleared.
        byte[] file = TestCartridge.Banked(1, 8, 16384, 2, 4096, submapper: 1);
        file[10] = 0;
        IMapper mapper = Cartridge.Load(file).CreateMapper();

        Assert.Empty(mapper.PrgRam);
        mapper.CpuWrite(0x6000, 1);
        Assert.Equal(0xEE, Cpu(mapper, 0x6000));
    }

    [Fact]
    public void TheResetButtonKeepsTheBatteryRamAndTheRegistersAndAPowerCycleClearsThem()
    {
        IMapper mapper = Board(battery: true);
        mapper.CpuWrite(0x6000, 0x12);
        Load(mapper, ControlRegister, 0b11111);
        Load(mapper, PrgRegister, 3);

        // The reset button: the chip has no reset line, so nothing in it changes.
        mapper.Reset(false);

        Assert.Equal(0x12, Cpu(mapper, 0x6000));
        Assert.Equal(3, Cpu(mapper, 0x8000));
        Assert.Equal(Mirroring.Horizontal, mapper.Mirroring);

        // Power: the registers go back; the RAM goes when the bus clears it.
        mapper.Reset(true);

        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(7, Cpu(mapper, 0xC000));
        Assert.Equal(Mirroring.SingleScreenLow, mapper.Mirroring);
        Assert.Equal(0x12, Cpu(mapper, 0x6000));

        mapper.ClearPrgRam();

        Assert.Equal(0, Cpu(mapper, 0x6000));
    }

    [Fact]
    public void APowerOnPutsTheShiftRegisterBackToo()
    {
        IMapper mapper = Board();
        Write(mapper, PrgRegister, 1);
        Write(mapper, PrgRegister, 1);

        mapper.Reset(true);
        Load(mapper, PrgRegister, 2);

        Assert.Equal(2, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void ThroughTheMachineAPowerOnResetsTheBoardAndTheResetButtonDoesNot()
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Banked(1, 8, 16384, 2, 4096, battery: true)), Region.Ntsc);
        nes.PowerOn();
        nes.Bus.Write(0x6000, 0x42);

        // Bank 4 is 0, 0, 1, 0, 0, with a read between the writes.
        foreach (byte bit in new byte[] { 0, 0, 1, 0, 0 })
        {
            nes.Bus.Write(PrgRegister, bit);
            nes.Bus.Read(0x8000);
        }

        Assert.Equal(4, nes.Bus.Peek(0x8000));

        nes.Reset();

        Assert.Equal(4, nes.Bus.Peek(0x8000));
        Assert.Equal(0x42, nes.Bus.Peek(0x6000));

        nes.PowerOn();

        Assert.Equal(0, nes.Bus.Peek(0x8000));
        Assert.Equal(0, nes.Bus.Peek(0x6000));
    }

    [Fact]
    public void ChrRamIsBankedAndAPowerOnClearsItButTheResetButtonDoesNot()
    {
        IMapper mapper = Cartridge.Load(TestCartridge.Banked(1, 8)).CreateMapper();
        Load(mapper, ControlRegister, 0b11100);
        Load(mapper, ChrRegister0, 1);
        Load(mapper, ChrRegister1, 0);

        mapper.PpuWrite(0x0000, 0xA5);
        mapper.PpuWrite(0x1FFF, 0x5A);

        // 4 KB bank 1 is the upper half of the 8 KB RAM, and bank 0 the lower.
        Assert.Equal(0xA5, Ppu(mapper, 0x0000));
        Assert.Equal(0x5A, Ppu(mapper, 0x1FFF));
        Load(mapper, ChrRegister0, 0);
        Load(mapper, ChrRegister1, 1);
        Assert.Equal(0xA5, Ppu(mapper, 0x1000));
        Assert.Equal(0x5A, Ppu(mapper, 0x0FFF));

        mapper.Reset(false);
        Assert.Equal(0xA5, Ppu(mapper, 0x1000));

        mapper.Reset(true);
        Assert.Equal(0, Ppu(mapper, 0x1000));
        Assert.Equal(0, Ppu(mapper, 0x0FFF));
    }

    [Fact]
    public void ChrRomIgnoresWrites()
    {
        IMapper mapper = Board();

        mapper.PpuWrite(0x0000, 0x77);

        Assert.Equal(0, Ppu(mapper, 0x0000));
    }

    // Bad ROM safety (the plan's Review Focus 1): a board with fewer banks than its registers
    // can name wraps the bank number and never reads outside the file.
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(1, 0)]
    public void BankNumbersWrapForACartridgeSmallerThanTheRegistersCanName(int prgBanks, int chrBanks4K)
    {
        // No CHR banks is CHR RAM.
        IMapper mapper = Cartridge.Load(TestCartridge.Banked(1, prgBanks, 16384, chrBanks4K, 4096)).CreateMapper();

        for (int control = 0; control < 32; control++)
        {
            Load(mapper, ControlRegister, control);
            for (int bank = 0; bank < 32; bank++)
            {
                Load(mapper, PrgRegister, bank);
                Load(mapper, ChrRegister0, bank);
                Load(mapper, ChrRegister1, bank);
                foreach (int address in new[] { 0x8000, 0xBFFF, 0xC000, 0xFFFF })
                {
                    Assert.True(Cpu(mapper, address) < prgBanks, $"control {control}, bank {bank}, ${address:X4}");
                }

                foreach (int address in new[] { 0x0000, 0x0FFF, 0x1000, 0x1FFF })
                {
                    Assert.True(chrBanks4K == 0 || Ppu(mapper, address) < chrBanks4K, $"control {control}, bank {bank}, ${address:X4}");
                }
            }
        }
    }

    [Fact]
    public void ASixteenKilobytePrgInAThirtyTwoKilobyteModeShowsTheBankTwice()
    {
        IMapper mapper = Cartridge.Load(TestCartridge.Banked(1, 1, 16384, 2, 4096)).CreateMapper();

        Load(mapper, ControlRegister, 0b00000);
        Load(mapper, PrgRegister, 6);

        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(0, Cpu(mapper, 0xFFFF));
    }

    [Fact]
    public void APrgThatIsNotWholeBanksIsPaddedByWrappingAndNeverReadsOutOfRange()
    {
        // NES 2.0 exponent form: 2^13 * 3 = 24,576 bytes, a bank and a half.
        byte[] header = TestCartridge.Header();
        header[4] = (13 << 2) | 1;
        header[6] = 0x10;
        header[7] = 0x08;
        header[9] = 0x0F;
        header[11] = 7;
        byte[] prg = TestCartridge.Pattern(24576);
        IMapper mapper = Cartridge.Load(TestCartridge.Join(header, prg)).CreateMapper();

        for (int bank = 0; bank < 16; bank++)
        {
            Load(mapper, PrgRegister, bank);
            foreach (int address in new[] { 0x8000, 0xBFFF, 0xC000, 0xFFFF })
            {
                Cpu(mapper, address);
            }
        }

        Load(mapper, PrgRegister, 0);
        Assert.Equal(prg[0], Cpu(mapper, 0x8000));
    }

    [Fact]
    public void ACartridgeWithNoCharacterMemoryAtAllReadsZero()
    {
        // NES 2.0, CHR size 0 and CHR RAM size 0.
        byte[] header = TestCartridge.Header();
        header[4] = 2;
        header[6] = 0x10;
        header[7] = 0x08;
        IMapper mapper = Cartridge.Load(TestCartridge.Join(header, new byte[32768])).CreateMapper();

        Load(mapper, ChrRegister0, 5);
        mapper.PpuWrite(0x0000, 1);

        Assert.Equal(0, Ppu(mapper, 0x0000));
        Assert.Equal(0, Ppu(mapper, 0x1FFF));
    }
}
