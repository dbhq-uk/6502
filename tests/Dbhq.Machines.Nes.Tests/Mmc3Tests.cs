using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// MMC3, from <c>docs/nes/facts/mappers.md</c> section 6. Every cartridge is built so a bank holds
/// its own number in every byte (<see cref="TestCartridge.Banked"/>): a PRG bank is 8 KB and a CHR
/// bank is 1 KB here, the MMC3's own units, so a read says which bank the board put at that
/// address. The scanline counter is driven by telling the board of PPU addresses with the CPU
/// cycle of each, as the PPU does; the tests that drive it from a real PPU and a real bus are in
/// <see cref="Mmc3ScanlineCounterTests"/>.
/// </summary>
public class Mmc3Tests
{
    private const ushort BankSelect = 0x8000;
    private const ushort BankData = 0x8001;
    private const ushort MirroringRegister = 0xA000;
    private const ushort PrgRamProtect = 0xA001;
    private const ushort IrqLatch = 0xC000;
    private const ushort IrqReload = 0xC001;
    private const ushort IrqDisable = 0xE000;
    private const ushort IrqEnable = 0xE001;

    // The CPU cycle the next made-up A12 change is told with: each test moves it on.
    private long _cycle = 1000;

    // Sixteen 8 KB PRG banks (128 KB) and sixty-four 1 KB CHR banks (64 KB) unless a test asks.
    private static IMapper Board(int prgBanks8K = 16, int chrBanks1K = 64, bool verticalMirroring = false)
    {
        return Cartridge.Load(TestCartridge.Banked(4, prgBanks8K, 8192, chrBanks1K, 1024, verticalMirroring: verticalMirroring)).CreateMapper();
    }

    private static byte Cpu(IMapper mapper, int address) => mapper.CpuRead((ushort)address, 0xEE);

    private static byte Ppu(IMapper mapper, int address) => mapper.PpuRead((ushort)address);

    // A bank register written as a program writes it: the register's number to $8000, then the bank to $8001.
    private static void SetBank(IMapper mapper, int register, int bank, int mode = 0)
    {
        mapper.CpuWrite(BankSelect, (byte)(mode | register));
        mapper.CpuWrite(BankData, (byte)bank);
    }

    // One clock of the counter: A12 low, then high again a filter's length and more later.
    private void Clock(IMapper mapper)
    {
        mapper.PpuAddressChanged(0x0000, _cycle);
        _cycle += 10;
        mapper.PpuAddressChanged(0x1000, _cycle);
        _cycle += 10;
    }

    // A12 is low from power on, so a test of single edges first puts it high, a clock that counts
    // nothing (the counter is 0 and IRQs are off).
    private static void HighFirst(IMapper mapper)
    {
        mapper.PpuAddressChanged(0x1000, 10);
    }

    // The counter's setup as a program does it: the latch, a reload, IRQs on.
    private static void StartCounter(IMapper mapper, int latch)
    {
        mapper.CpuWrite(IrqLatch, (byte)latch);
        mapper.CpuWrite(IrqReload, 0);
        mapper.CpuWrite(IrqEnable, 0);
    }

    // How many clocks until the IRQ line goes low (Irq true), up to a limit.
    private int ClocksUntilIrq(IMapper mapper, int limit = 600)
    {
        for (int clocks = 1; clocks <= limit; clocks++)
        {
            Clock(mapper);
            if (mapper.Irq)
            {
                return clocks;
            }
        }

        return -1;
    }

    [Fact]
    public void AtPowerOnTheLastTwoBanksAreFixedAndTheRestReadInOrder()
    {
        IMapper mapper = Board();

        // R6 and R7 are unspecified at power on (the sheet); the model starts them at 0 and 1, so
        // a 32 KB program that never writes them reads straight through, as the test ROMs need.
        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(1, Cpu(mapper, 0xA000));
        Assert.Equal(14, Cpu(mapper, 0xC000));
        Assert.Equal(15, Cpu(mapper, 0xE000));
        Assert.Equal(15, Cpu(mapper, 0xFFFF));
        for (int window = 0; window < 8; window++)
        {
            Assert.Equal(window, Ppu(mapper, window * 0x400));
            Assert.Equal(window, Ppu(mapper, (window * 0x400) + 0x3FF));
        }

        Assert.False(mapper.Irq);
    }

    // The eight bank registers in CHR inversion 0 and PRG mode 0: where each lands and what it reads.
    public static TheoryData<int, int, int[], int[]> BankRegisters() => new()
    {
        // R0 and R1 are 2 KB: the low bit of the bank is ignored, and the two 1 KB halves follow.
        { 0, 0x25, [0x0000, 0x0400], [0x24, 0x25] },
        { 1, 0x0B, [0x0800, 0x0C00], [0x0A, 0x0B] },
        { 2, 0x31, [0x1000], [0x31] },
        { 3, 0x32, [0x1400], [0x32] },
        { 4, 0x33, [0x1800], [0x33] },
        { 5, 0x3F, [0x1C00], [0x3F] },
    };

    [Theory]
    [MemberData(nameof(BankRegisters))]
    public void EachChrBankRegisterPutsItsBankWhereTheSheetSays(int register, int bank, int[] addresses, int[] expected)
    {
        IMapper mapper = Board();

        SetBank(mapper, register, bank);

        for (int i = 0; i < addresses.Length; i++)
        {
            Assert.Equal(expected[i], Ppu(mapper, addresses[i]));
            Assert.Equal(expected[i], Ppu(mapper, addresses[i] + 0x3FF));
        }
    }

    [Fact]
    public void R6AndR7SwitchTheFirstTwoPrgWindowsAndIgnoreBits7And6()
    {
        IMapper mapper = Board();

        // $C5 and $C9 with bits 7 and 6 cleared are 5 and 9.
        SetBank(mapper, 6, 0xC5);
        SetBank(mapper, 7, 0xC9);

        Assert.Equal(5, Cpu(mapper, 0x8000));
        Assert.Equal(5, Cpu(mapper, 0x9FFF));
        Assert.Equal(9, Cpu(mapper, 0xA000));
        Assert.Equal(9, Cpu(mapper, 0xBFFF));
        Assert.Equal(14, Cpu(mapper, 0xC000));
        Assert.Equal(15, Cpu(mapper, 0xE000));
    }

    [Fact]
    public void PrgMode1SwapsR6WithTheSecondLastBank()
    {
        IMapper mapper = Board();
        SetBank(mapper, 6, 5);
        SetBank(mapper, 7, 9);

        // Bit 6 of the bank select: R6 moves to $C000 and the second-last bank to $8000.
        mapper.CpuWrite(BankSelect, 0x40);

        Assert.Equal(14, Cpu(mapper, 0x8000));
        Assert.Equal(9, Cpu(mapper, 0xA000));
        Assert.Equal(5, Cpu(mapper, 0xC000));
        Assert.Equal(5, Cpu(mapper, 0xDFFF));
        Assert.Equal(15, Cpu(mapper, 0xE000));

        // And back.
        mapper.CpuWrite(BankSelect, 0x00);
        Assert.Equal(5, Cpu(mapper, 0x8000));
        Assert.Equal(14, Cpu(mapper, 0xC000));
    }

    [Fact]
    public void ChrInversionSwapsTheTwoKilobyteBanksWithTheOneKilobyteBanks()
    {
        IMapper mapper = Board();
        SetBank(mapper, 0, 0x10);
        SetBank(mapper, 1, 0x12);
        SetBank(mapper, 2, 0x20);
        SetBank(mapper, 3, 0x21);
        SetBank(mapper, 4, 0x22);
        SetBank(mapper, 5, 0x23);

        // Bit 7 of the bank select: R0 and R1 at $1000 and $1800, R2 to R5 at $0000 to $0C00.
        mapper.CpuWrite(BankSelect, 0x80);

        int[] inverted = [0x20, 0x21, 0x22, 0x23, 0x10, 0x11, 0x12, 0x13];
        int[] plain = [0x10, 0x11, 0x12, 0x13, 0x20, 0x21, 0x22, 0x23];
        for (int window = 0; window < 8; window++)
        {
            Assert.Equal(inverted[window], Ppu(mapper, window * 0x400));
        }

        mapper.CpuWrite(BankSelect, 0x00);
        for (int window = 0; window < 8; window++)
        {
            Assert.Equal(plain[window], Ppu(mapper, window * 0x400));
        }
    }

    [Fact]
    public void BothModesTogetherAndARegisterWrittenWhileInverted()
    {
        IMapper mapper = Board();

        // Select R2 with both mode bits set, and write it: it lands at $0000 while inverted.
        SetBank(mapper, 2, 0x2A, mode: 0xC0);
        SetBank(mapper, 6, 3, mode: 0xC0);

        Assert.Equal(0x2A, Ppu(mapper, 0x0000));
        Assert.Equal(3, Cpu(mapper, 0xC000));
        Assert.Equal(14, Cpu(mapper, 0x8000));
    }

    [Fact]
    public void TheRegistersAreDecodedByEvenAndOddAddressThroughEachEightKilobytes()
    {
        IMapper mapper = Board();

        // $9FFE is an even address in $8000 to $9FFF (bank select), $9FFF an odd one (bank data).
        mapper.CpuWrite(0x9FFE, 6);
        mapper.CpuWrite(0x9FFF, 7);
        Assert.Equal(7, Cpu(mapper, 0x8000));

        mapper.CpuWrite(0xBFFE, 1);
        Assert.Equal(Mirroring.Horizontal, mapper.Mirroring);
    }

    [Fact]
    public void TheMirroringRegisterChoosesVerticalOrHorizontal()
    {
        IMapper mapper = Board(verticalMirroring: true);
        Assert.Equal(Mirroring.Vertical, mapper.Mirroring);

        mapper.CpuWrite(MirroringRegister, 1);
        Assert.Equal(Mirroring.Horizontal, mapper.Mirroring);

        // Only bit 0 counts.
        mapper.CpuWrite(MirroringRegister, 0xFE);
        Assert.Equal(Mirroring.Vertical, mapper.Mirroring);
    }

    [Fact]
    public void TheMirroringRegisterHasNoEffectWithFourScreenVram()
    {
        byte[] file = TestCartridge.Banked(4, 16, 8192, 64, 1024);
        file[6] |= 0x08;
        IMapper mapper = Cartridge.Load(file).CreateMapper();

        mapper.CpuWrite(MirroringRegister, 0);
        Assert.Equal(Mirroring.FourScreen, mapper.Mirroring);
        mapper.CpuWrite(MirroringRegister, 1);
        Assert.Equal(Mirroring.FourScreen, mapper.Mirroring);
    }

    [Fact]
    public void PrgRamIsOnAndWritableAtPowerOn()
    {
        IMapper mapper = Board();

        mapper.CpuWrite(0x6000, 0x42);
        mapper.CpuWrite(0x7FFF, 0x43);

        Assert.Equal(0x42, Cpu(mapper, 0x6000));
        Assert.Equal(0x43, Cpu(mapper, 0x7FFF));
    }

    [Fact]
    public void ThePrgRamProtectRegisterEnablesAndWriteProtects()
    {
        IMapper mapper = Board();
        mapper.CpuWrite(0x6000, 0x11);

        // Bit 7 set, bit 6 set: readable, and a write is refused.
        mapper.CpuWrite(PrgRamProtect, 0xC0);
        mapper.CpuWrite(0x6000, 0x22);
        Assert.Equal(0x11, Cpu(mapper, 0x6000));

        // Bit 7 clear: the chip is off, a read is open bus and a write goes nowhere.
        mapper.CpuWrite(PrgRamProtect, 0x00);
        Assert.Equal(0xEE, Cpu(mapper, 0x6000));
        mapper.CpuWrite(0x6000, 0x33);

        // Bit 7 set, bit 6 clear: readable and writable, and the refused writes changed nothing.
        mapper.CpuWrite(PrgRamProtect, 0x80);
        Assert.Equal(0x11, Cpu(mapper, 0x6000));
        mapper.CpuWrite(0x6000, 0x44);
        Assert.Equal(0x44, Cpu(mapper, 0x6000));
    }

    [Fact]
    public void WithTheLatchAtNTheIrqComesOnTheNPlusFirstClock()
    {
        foreach (int latch in new[] { 1, 2, 5, 31, 255 })
        {
            IMapper mapper = Board();
            StartCounter(mapper, latch);

            // The first clock after the reload loads the latch; then N clocks count it to 0.
            Assert.Equal(latch + 1, ClocksUntilIrq(mapper));
        }
    }

    [Fact]
    public void WorkedExample3_TheIrqAfterAReloadOfThirtyOneAndEveryThirtyTwoClocksAfter()
    {
        IMapper mapper = Board();
        StartCounter(mapper, 31);

        Assert.Equal(32, ClocksUntilIrq(mapper));

        // $E000 then $E001 acknowledges and keeps IRQs on, as a handler does; the latch stays 31.
        mapper.CpuWrite(IrqDisable, 0);
        mapper.CpuWrite(IrqEnable, 0);
        Assert.Equal(32, ClocksUntilIrq(mapper));
    }

    [Fact]
    public void TheIrqHoldsTheLineUntilE000IsWritten()
    {
        IMapper mapper = Board();
        StartCounter(mapper, 1);
        Assert.Equal(2, ClocksUntilIrq(mapper));

        // More clocks, a reload, a new latch and an enable leave it held.
        Clock(mapper);
        mapper.CpuWrite(IrqLatch, 50);
        mapper.CpuWrite(IrqReload, 0);
        mapper.CpuWrite(IrqEnable, 0);
        Clock(mapper);
        Assert.True(mapper.Irq);

        mapper.CpuWrite(IrqDisable, 0);
        Assert.False(mapper.Irq);
    }

    [Fact]
    public void AWriteToC001ReloadsOnTheNextClockAndNotAtOnce()
    {
        IMapper mapper = Board();
        StartCounter(mapper, 4);
        Clock(mapper);
        Clock(mapper);
        Clock(mapper);

        // The counter is 2. A reload asked for now waits for the next clock, which loads 4, so the
        // IRQ is 5 clocks away, not 2. Writing $C001 raises nothing itself.
        mapper.CpuWrite(IrqReload, 0);
        Assert.False(mapper.Irq);
        Assert.Equal(5, ClocksUntilIrq(mapper));
    }

    [Fact]
    public void AWriteToC000AloneDoesNotReload()
    {
        IMapper mapper = Board();
        StartCounter(mapper, 2);
        Clock(mapper);

        // Counter 2: a new latch of 100 waits until the counter reaches 0 and reloads.
        mapper.CpuWrite(IrqLatch, 100);
        Assert.Equal(2, ClocksUntilIrq(mapper));
        mapper.CpuWrite(IrqDisable, 0);
        mapper.CpuWrite(IrqEnable, 0);
        Assert.Equal(101, ClocksUntilIrq(mapper));
    }

    [Fact]
    public void DisablingAcknowledgesAndTheCounterRunsOnWhileDisabled()
    {
        IMapper mapper = Board();
        StartCounter(mapper, 2);
        mapper.CpuWrite(IrqDisable, 0);

        // Clocks 1 to 3 load 2 and count to 0, with IRQs off: nothing. Clocks 4 and 5 reload 2 and
        // count to 1. Enabled again, the sixth reaches 0 and raises it.
        for (int i = 0; i < 5; i++)
        {
            Clock(mapper);
            Assert.False(mapper.Irq);
        }

        mapper.CpuWrite(IrqEnable, 0);
        Assert.False(mapper.Irq);
        Assert.Equal(1, ClocksUntilIrq(mapper));
    }

    [Fact]
    public void LatchZeroRaisesTheIrqOnEveryClockTheSharpBehaviour()
    {
        IMapper mapper = Board();
        StartCounter(mapper, 0);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(1, ClocksUntilIrq(mapper));
            mapper.CpuWrite(IrqDisable, 0);
            mapper.CpuWrite(IrqEnable, 0);
        }
    }

    [Fact]
    public void LatchZeroAfterTheCounterReachedZeroStillRaisesTheIrqUnlikeTheOldBehaviour()
    {
        // mmc3_test_2's 6-MMC3_alt, test 2, in the behaviour the model does not take: with the
        // counter run down to 0 and the latch then set to 0, the NEC ("old") chip raises nothing
        // on the clocks that follow. The Sharp ("new") chip, the model's, raises it on each.
        IMapper mapper = Board();
        StartCounter(mapper, 2);
        Assert.Equal(3, ClocksUntilIrq(mapper));
        mapper.CpuWrite(IrqDisable, 0);
        mapper.CpuWrite(IrqEnable, 0);

        mapper.CpuWrite(IrqLatch, 0);
        Clock(mapper);

        Assert.True(mapper.Irq);
    }

    [Fact]
    public void AReloadOfANonZeroLatchRaisesNothing()
    {
        IMapper mapper = Board();
        StartCounter(mapper, 1);
        Assert.Equal(2, ClocksUntilIrq(mapper));
        mapper.CpuWrite(IrqDisable, 0);
        mapper.CpuWrite(IrqEnable, 0);

        // At 0 the next clock reloads 1: not zero, so no IRQ.
        Clock(mapper);
        Assert.False(mapper.Irq);
    }

    [Fact]
    public void TheFilterCountsARiseOnlyAfterA12HasBeenLowForThreeCycles()
    {
        IMapper mapper = Board();
        HighFirst(mapper);
        StartCounter(mapper, 0);

        // Low in cycle 100, high in 102: two M2 falls between, filtered out.
        mapper.PpuAddressChanged(0x0FFF, 100);
        mapper.PpuAddressChanged(0x1000, 102);
        Assert.False(mapper.Irq);

        // Low in 200, high in 203: three falls, a clock.
        mapper.PpuAddressChanged(0x0000, 200);
        mapper.PpuAddressChanged(0x1FFF, 203);
        Assert.True(mapper.Irq);
    }

    [Fact]
    public void TwoRisesCloserThanTheFilterCountOnce()
    {
        IMapper mapper = Board();
        StartCounter(mapper, 1);

        // The first rise loads 1. The second comes a cycle after A12 fell again: not counted, so the
        // counter is still 1 and no IRQ. A third, after a long low, counts it to 0.
        mapper.PpuAddressChanged(0x0000, 500);
        mapper.PpuAddressChanged(0x1000, 510);
        mapper.PpuAddressChanged(0x0000, 511);
        mapper.PpuAddressChanged(0x1000, 512);
        Assert.False(mapper.Irq);

        mapper.PpuAddressChanged(0x0000, 520);
        mapper.PpuAddressChanged(0x1000, 530);
        Assert.True(mapper.Irq);
    }

    [Fact]
    public void A12StayingHighOrStayingLowIsNotAClock()
    {
        IMapper mapper = Board();
        HighFirst(mapper);
        StartCounter(mapper, 0);

        // High after high, however far apart, is no rise; low after low is no rise; nor are the
        // other address lines changing.
        mapper.PpuAddressChanged(0x1000, 100);
        mapper.PpuAddressChanged(0x1FF0, 200);
        mapper.PpuAddressChanged(0x3F00, 300);
        Assert.False(mapper.Irq);

        mapper.PpuAddressChanged(0x0000, 400);
        mapper.PpuAddressChanged(0x2FFF, 500);
        mapper.PpuAddressChanged(0x0FFF, 600);
        Assert.False(mapper.Irq);
    }

    [Fact]
    public void AnyAddressWithBit12SetIsA12HighPaletteAndNametableMirrorsIncluded()
    {
        IMapper mapper = Board();
        HighFirst(mapper);
        StartCounter(mapper, 0);

        // $2FFF has A12 low; $3F00 (a palette address in v) has it high.
        mapper.PpuAddressChanged(0x2FFF, 100);
        mapper.PpuAddressChanged(0x3F00, 110);

        Assert.True(mapper.Irq);
    }

    [Fact]
    public void TheResetButtonLeavesTheBoardAndAPowerOnRestoresIt()
    {
        IMapper mapper = Board();
        SetBank(mapper, 6, 7, mode: 0x40);
        SetBank(mapper, 2, 0x30, mode: 0xC0);
        mapper.CpuWrite(MirroringRegister, 1);
        StartCounter(mapper, 0);
        Clock(mapper);
        Assert.True(mapper.Irq);

        mapper.Reset(false);
        Assert.Equal(7, Cpu(mapper, 0xC000));
        Assert.Equal(0x30, Ppu(mapper, 0x0000));
        Assert.Equal(Mirroring.Horizontal, mapper.Mirroring);
        Assert.True(mapper.Irq);

        mapper.Reset(true);
        Assert.Equal(0, Cpu(mapper, 0x8000));
        Assert.Equal(14, Cpu(mapper, 0xC000));
        Assert.Equal(0, Ppu(mapper, 0x0000));
        Assert.Equal(Mirroring.Horizontal, mapper.Mirroring);
        Assert.False(mapper.Irq);
        Clock(mapper);
        Assert.False(mapper.Irq);
    }

    [Fact]
    public void APowerOnPutsTheHeadersMirroringBack()
    {
        IMapper mapper = Board(verticalMirroring: true);
        mapper.CpuWrite(MirroringRegister, 1);

        mapper.Reset(true);

        Assert.Equal(Mirroring.Vertical, mapper.Mirroring);
    }

    // Files smaller than the registers can name (Review Focus 1): every value of every register,
    // in both modes, reads a bank that exists, the one the value names modulo the count.
    public static TheoryData<int, int> SmallFiles() => new()
    {
        { 2, 8 },
        { 4, 8 },
        { 6, 16 },
        { 16, 64 },
    };

    [Theory]
    [MemberData(nameof(SmallFiles))]
    public void EveryBankNumberWrapsModuloTheBanksInTheFile(int prgBanks8K, int chrBanks1K)
    {
        IMapper mapper = Board(prgBanks8K, chrBanks1K);
        for (int mode = 0; mode < 0x100; mode += 0x40)
        {
            for (int value = 0; value < 256; value++)
            {
                for (int register = 0; register < 8; register++)
                {
                    SetBank(mapper, register, value, mode);
                }

                bool prgMode1 = (mode & 0x40) != 0;
                int prg = (value & 0x3F) % prgBanks8K;
                int last = prgBanks8K - 1;
                Assert.Equal(prgMode1 ? last - 1 : prg, Cpu(mapper, 0x8000));
                Assert.Equal(prg, Cpu(mapper, 0xA000));
                Assert.Equal(prgMode1 ? prg : last - 1, Cpu(mapper, 0xDFFF));
                Assert.Equal(last, Cpu(mapper, 0xFFFF));

                int twoK = (value & 0xFE) % chrBanks1K;
                int twoKHigh = (value | 1) % chrBanks1K;
                int oneK = value % chrBanks1K;
                int twoKBase = (mode & 0x80) != 0 ? 0x1000 : 0x0000;
                int oneKBase = twoKBase ^ 0x1000;
                Assert.Equal(twoK, Ppu(mapper, twoKBase));
                Assert.Equal(twoKHigh, Ppu(mapper, twoKBase + 0x7FF));
                Assert.Equal(oneK, Ppu(mapper, oneKBase + 0xFFF));
            }
        }
    }

    [Fact]
    public void AnEightKilobyteProgramIsEveryWindowAndTheSecondLastBankWraps()
    {
        // A NES 2.0 file whose PRG is 8 KB, in the exponent form: 2^13 x 1. One bank, so the
        // "second-last" bank, -1, must wrap to it too.
        byte[] header = TestCartridge.Header();
        header[4] = 13 << 2;
        header[5] = 1;
        header[6] = 0x40;
        header[7] = 0x08;
        header[9] = 0x0F;
        byte[] prg = TestCartridge.Filled(8192, 0x5A);
        IMapper mapper = Cartridge.Load(TestCartridge.Join(header, prg, new byte[8192])).CreateMapper();

        mapper.CpuWrite(BankSelect, 0x40);
        foreach (int address in new[] { 0x8000, 0xA000, 0xC000, 0xE000 })
        {
            Assert.Equal(0x5A, Cpu(mapper, address));
        }
    }

    [Fact]
    public void ChrRamIsWrittenThroughTheBanks()
    {
        // No CHR in the file: 8 KB of CHR RAM, banked like ROM. R2 = 3 puts its fourth KB at $1000.
        IMapper mapper = Cartridge.Load(TestCartridge.Banked(4, 16, 8192)).CreateMapper();
        SetBank(mapper, 2, 3);

        mapper.PpuWrite(0x1000, 0x77);

        Assert.Equal(0x77, Ppu(mapper, 0x0C00));
        Assert.Equal(0x77, Ppu(mapper, 0x1000));
    }
}
