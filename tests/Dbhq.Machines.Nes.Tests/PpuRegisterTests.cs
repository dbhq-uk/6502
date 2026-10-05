using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The PPU's registers and memory, each from <c>docs/nes/facts/ppu.md</c> sections 1 to 4 and its
/// worked examples, and the nametable wiring from <c>mappers.md</c> section 1. The PPU runs alone
/// here, on a test board, with rendering off unless a test turns it on.
/// </summary>
public class PpuRegisterTests
{
    private static (Ppu Ppu, TestMapper Mapper) Build(Mirroring mirroring = Mirroring.Horizontal)
    {
        var mapper = new TestMapper { Mirroring = mirroring };
        var ppu = new Ppu(Region.Ntsc, mapper);
        ppu.PowerOn();
        return (ppu, mapper);
    }

    private static void SetAddress(Ppu ppu, ushort address)
    {
        ppu.WriteRegister(6, (byte)(address >> 8));
        ppu.WriteRegister(6, (byte)address);
    }

    private static void Poke(Ppu ppu, ushort address, byte value)
    {
        SetAddress(ppu, address);
        ppu.WriteRegister(7, value);
    }

    [Fact]
    public void WorkedExample1_TheRegisterWritesOnPpuScrolling()
    {
        (Ppu ppu, _) = Build();

        ppu.WriteRegister(0, 0x00);
        Assert.Equal((0x0000, 0x0000, 0, false), (ppu.T, ppu.V, ppu.FineX, ppu.WriteToggle));

        ppu.ReadRegister(2);
        Assert.Equal((0x0000, 0x0000, 0, false), (ppu.T, ppu.V, ppu.FineX, ppu.WriteToggle));

        ppu.WriteRegister(5, 0x7D);
        Assert.Equal((0x000F, 0x0000, 5, true), (ppu.T, ppu.V, ppu.FineX, ppu.WriteToggle));

        ppu.WriteRegister(5, 0x5E);
        Assert.Equal((0x616F, 0x0000, 5, false), (ppu.T, ppu.V, ppu.FineX, ppu.WriteToggle));

        ppu.WriteRegister(6, 0x3D);
        Assert.Equal((0x3D6F, 0x0000, 5, true), (ppu.T, ppu.V, ppu.FineX, ppu.WriteToggle));

        ppu.WriteRegister(6, 0xF0);
        Assert.Equal((0x3DF0, 0x3DF0, 5, false), (ppu.T, ppu.V, ppu.FineX, ppu.WriteToggle));
    }

    [Fact]
    public void ThePpuctrlWriteSetsTheNametableBitsOfT()
    {
        (Ppu ppu, _) = Build();

        ppu.WriteRegister(0, 0x03);

        Assert.Equal(0x0C00, ppu.T);
    }

    [Fact]
    public void TheWriteToggleIsSharedBy2005And2006AndResetByAStatusRead()
    {
        (Ppu ppu, _) = Build();

        // One $2005 write, then a status read: the next $2005 write is a first write again.
        ppu.WriteRegister(5, 0x00);
        Assert.True(ppu.WriteToggle);
        ppu.ReadRegister(2);
        Assert.False(ppu.WriteToggle);
        ppu.WriteRegister(5, 0x07);
        Assert.Equal(7, ppu.FineX);
        Assert.True(ppu.WriteToggle);

        // The toggle is one for both registers: this $2006 write is the second of the pair.
        ppu.WriteRegister(6, 0x45);
        Assert.False(ppu.WriteToggle);
        Assert.Equal(0x0045, ppu.V & 0x00FF);
    }

    [Fact]
    public void TwoWritesTo2006SetVAndAThirdStartsANewPair()
    {
        (Ppu ppu, _) = Build();

        ppu.WriteRegister(6, 0x21);
        Assert.Equal(0x0000, ppu.V);
        ppu.WriteRegister(6, 0x08);
        Assert.Equal(0x2108, ppu.V);

        ppu.WriteRegister(6, 0x3F);
        Assert.Equal(0x2108, ppu.V);
        Assert.True(ppu.WriteToggle);
        ppu.WriteRegister(6, 0x00);
        Assert.Equal(0x3F00, ppu.V);
    }

    [Fact]
    public void TheFirst2006WriteClearsBit14OfT()
    {
        (Ppu ppu, _) = Build();

        // $2005's second write puts fine Y 7 in t bits 14 to 12; $2006 keeps only six bits of its data.
        ppu.WriteRegister(5, 0x00);
        ppu.WriteRegister(5, 0x07);
        Assert.Equal(0x7000, ppu.T);

        ppu.WriteRegister(6, 0xFF);

        Assert.Equal(0x3F00, ppu.T);
    }

    [Fact]
    public void WorkedExample3_TheReadBufferDelaysANonPaletteReadByOneAndAPaletteReadIsImmediate()
    {
        (Ppu ppu, _) = Build();
        Poke(ppu, 0x2000, 0x11);
        Poke(ppu, 0x2001, 0x22);
        Poke(ppu, 0x3F00, 0x0F);
        Poke(ppu, 0x2F00, 0xAB);

        SetAddress(ppu, 0x2000);
        Assert.Equal(0x00, ppu.ReadRegister(7));
        Assert.Equal(0x11, ppu.ReadRegister(7));
        Assert.Equal(0x22, ppu.ReadRegister(7));
        Assert.Equal(0x2003, ppu.V);

        // The latch holds $00, the last value written; the palette read is immediate.
        SetAddress(ppu, 0x3F00);
        Assert.Equal(0x0F, ppu.ReadRegister(7));

        // And the buffer took the nametable byte underneath, $2F00, which the next read shows.
        SetAddress(ppu, 0x2000);
        Assert.Equal(0xAB, ppu.ReadRegister(7));
    }

    [Fact]
    public void APaletteReadTakesBits7And6FromTheLatchAndShowsGreyscale()
    {
        (Ppu ppu, _) = Build();
        Poke(ppu, 0x3F01, 0x2C);
        SetAddress(ppu, 0x3F01);

        // A write to OAMADDR fills the latch with $C0.
        ppu.WriteRegister(3, 0xC0);
        Assert.Equal(0xEC, ppu.ReadRegister(7));

        ppu.WriteRegister(1, 0x01);
        SetAddress(ppu, 0x3F01);
        ppu.WriteRegister(3, 0x00);
        Assert.Equal(0x20, ppu.ReadRegister(7));
    }

    [Fact]
    public void ThePpuctrlIncrementIsOneOrThirtyTwo()
    {
        (Ppu ppu, _) = Build();

        SetAddress(ppu, 0x2000);
        ppu.WriteRegister(7, 0x01);
        Assert.Equal(0x2001, ppu.V);
        ppu.ReadRegister(7);
        Assert.Equal(0x2002, ppu.V);

        ppu.WriteRegister(0, 0x04);
        ppu.WriteRegister(7, 0x01);
        Assert.Equal(0x2022, ppu.V);
        ppu.ReadRegister(7);
        Assert.Equal(0x2042, ppu.V);
    }

    [Fact]
    public void WorkedExample2_A2007AccessDuringRenderingIncrementsCoarseXAndY()
    {
        (Ppu ppu, _) = Build();

        // Line 0 with the background on is rendering. v = $0000 takes a coarse X increment and a
        // Y increment (the first row of the worked example), whatever PPUCTRL bit 2 says.
        ppu.WriteRegister(0, 0x04);
        ppu.WriteRegister(1, 0x08);
        ppu.ReadRegister(7);

        Assert.Equal(0x1001, ppu.V);
    }

    [Fact]
    public void ThePaletteEntryZeroOfEachSpritePaletteIsTheBackgroundsAndTheRangeRepeats()
    {
        (Ppu ppu, _) = Build();

        foreach ((ushort sprite, ushort background) in new (ushort, ushort)[] { (0x3F10, 0x3F00), (0x3F14, 0x3F04), (0x3F18, 0x3F08), (0x3F1C, 0x3F0C) })
        {
            Poke(ppu, sprite, 0x21);
            Assert.Equal(0x21, ppu.PeekVram(background));
            Poke(ppu, background, 0x0F);
            Assert.Equal(0x0F, ppu.PeekVram(sprite));
        }

        // The other sprite entries are their own.
        Poke(ppu, 0x3F01, 0x01);
        Poke(ppu, 0x3F11, 0x11);
        Assert.Equal(0x01, ppu.PeekVram(0x3F01));
        Assert.Equal(0x11, ppu.PeekVram(0x3F11));

        // $3F20 to $3FFF repeat $3F00 to $3F1F, and an entry keeps six bits.
        Assert.Equal(0x0F, ppu.PeekVram(0x3F20));
        Assert.Equal(0x11, ppu.PeekVram(0x3FF1));
        Poke(ppu, 0x3FFF, 0xFF);
        Assert.Equal(0x3F, ppu.PeekVram(0x3F1F));
    }

    public static TheoryData<Mirroring, ushort[]> Arrangements() => new()
    {
        // The addresses that show the same byte as $2C05, from mappers.md section 1's table.
        { Mirroring.Vertical, [0x2405] },
        { Mirroring.Horizontal, [0x2805] },
        { Mirroring.SingleScreenLow, [0x2005, 0x2405, 0x2805] },
        { Mirroring.SingleScreenHigh, [0x2005, 0x2405, 0x2805] },
        { Mirroring.FourScreen, [] },
    };

    [Theory]
    [MemberData(nameof(Arrangements))]
    public void NametablesFollowTheBoardsWiring(Mirroring mirroring, ushort[] sameAs2C05)
    {
        (Ppu ppu, _) = Build(mirroring);

        Poke(ppu, 0x2C05, 0x5A);

        foreach (ushort address in new ushort[] { 0x2005, 0x2405, 0x2805 })
        {
            Assert.Equal(sameAs2C05.Contains(address) ? 0x5A : 0x00, ppu.PeekVram(address));
        }

        // $3000 to $3EFF repeats $2000 to $2EFF.
        Assert.Equal(0x5A, ppu.PeekVram(0x3C05));
    }

    [Fact]
    public void TheTwoSingleScreensAreTheTwoKilobytes()
    {
        var mapper = new TestMapper { Mirroring = Mirroring.SingleScreenLow };
        var ppu = new Ppu(Region.Ntsc, mapper);
        ppu.PowerOn();
        Poke(ppu, 0x2005, 0x11);

        // mappers.md worked example 1: lower is CIRAM $0005, upper is $0405, the vertical's B.
        mapper.Mirroring = Mirroring.SingleScreenHigh;
        Assert.Equal(0x00, ppu.PeekVram(0x2005));
        Poke(ppu, 0x2005, 0x22);
        mapper.Mirroring = Mirroring.Vertical;
        Assert.Equal(0x11, ppu.PeekVram(0x2005));
        Assert.Equal(0x22, ppu.PeekVram(0x2405));
    }

    [Fact]
    public void PatternTableAccessesGoToTheBoard()
    {
        (Ppu ppu, TestMapper mapper) = Build();
        mapper.Chr[0x1234] = 0x77;

        Poke(ppu, 0x0010, 0x99);
        SetAddress(ppu, 0x1234);
        ppu.ReadRegister(7);

        Assert.Equal(0x99, mapper.Chr[0x0010]);
        Assert.Equal(0x77, ppu.ReadRegister(7));
    }

    [Fact]
    public void EveryPatternTableAddressOnThePpusBusIsReportedToTheBoard()
    {
        (Ppu ppu, TestMapper mapper) = Build();

        SetAddress(ppu, 0x1FFF);
        ppu.WriteRegister(7, 0x00);
        SetAddress(ppu, 0x2000);
        ppu.ReadRegister(7);

        // $2006 put $1FFF on the bus, the $2007 write accessed it, and the increment moved to
        // $2000, which is a nametable, so it is not a pattern-table address and not reported.
        Assert.Equal(new ushort[] { 0x1FFF, 0x1FFF }, mapper.Reported.Select(r => r.Address));
    }

    [Fact]
    public void Oam2003And2004ReadAndWriteAndTheAttributeByteKeepsFiveBits()
    {
        (Ppu ppu, _) = Build();

        ppu.WriteRegister(3, 0x10);
        ppu.WriteRegister(4, 0xAA);
        ppu.WriteRegister(4, 0xBB);
        ppu.WriteRegister(4, 0xFF);

        Assert.Equal(0xAA, ppu.Oam[0x10]);
        Assert.Equal(0xBB, ppu.Oam[0x11]);
        Assert.Equal(0xE3, ppu.Oam[0x12]);

        // A read does not move OAMADDR.
        ppu.WriteRegister(3, 0x11);
        Assert.Equal(0xBB, ppu.ReadRegister(4));
        Assert.Equal(0xBB, ppu.ReadRegister(4));
    }

    [Fact]
    public void AnOamWriteDuringRenderingBumpsTheHighSixBitsOfOamaddrAndWritesNothing()
    {
        (Ppu ppu, _) = Build();
        ppu.Oam[0x14] = 0x77;
        ppu.WriteRegister(3, 0x10);

        // Line 0, background on: rendering.
        ppu.WriteRegister(1, 0x08);
        ppu.WriteRegister(4, 0x99);

        // Read back with rendering off: during rendering a $2004 read shows what sprite
        // evaluation is reading, not OAM at OAMADDR (ppu.md 1; PpuSpriteTests).
        ppu.WriteRegister(1, 0x00);
        Assert.Equal(0x00, ppu.Oam[0x10]);
        Assert.Equal(0x77, ppu.ReadRegister(4));
    }

    [Fact]
    public void AWriteTo2002OnlySetsTheLatch()
    {
        (Ppu ppu, _) = Build();
        ppu.WriteRegister(5, 0x00);
        while (!(ppu.Line == 241 && ppu.Dot == 2))
        {
            ppu.Tick();
        }

        byte before = ppu.PeekRegister(2);
        ppu.WriteRegister(2, 0x5A);

        // The VBlank flag and the write toggle are untouched; the latch holds $5A, which a read
        // of a write-only register returns and the status read shows in bits 4 to 0.
        Assert.Equal(0x80, before & 0x80);
        Assert.Equal(0x80 | 0x1A, ppu.PeekRegister(2));
        Assert.True(ppu.WriteToggle);
        Assert.Equal(0x5A, ppu.ReadRegister(0));
    }

    [Fact]
    public void AStatusReadLoadsBits7To5OntoTheLatchAndKeepsBits4To0()
    {
        (Ppu ppu, _) = Build();
        while (!(ppu.Line == 241 && ppu.Dot == 2))
        {
            ppu.Tick();
        }

        ppu.WriteRegister(3, 0x7F);

        Assert.Equal(0x9F, ppu.ReadRegister(2));
        Assert.Equal(0x9F, ppu.ReadRegister(5));
        Assert.Equal(0x1F, ppu.ReadRegister(2));
    }

    [Fact]
    public void TheLatchDoesNotDecay_ItsDecayIsNotModelled()
    {
        (Ppu ppu, _) = Build();
        ppu.WriteRegister(3, 0xA5);

        // Two frames, about 33 ms: a real latch would have lost its bits (3 to 30 ms, ppu.md 1).
        for (int i = 0; i < 2 * 262 * 341; i++)
        {
            ppu.Tick();
        }

        Assert.Equal(0xA5, ppu.ReadRegister(5));
    }

    [Fact]
    public void PeekVramAndPeekRegisterHaveNoSideEffect()
    {
        (Ppu ppu, TestMapper mapper) = Build();
        Poke(ppu, 0x2100, 0x31);
        Poke(ppu, 0x2101, 0x32);
        SetAddress(ppu, 0x2100);
        ppu.ReadRegister(7);
        while (!(ppu.Line == 241 && ppu.Dot == 2))
        {
            ppu.Tick();
        }

        int reported = mapper.Reported.Count;
        Assert.Equal(0x31, ppu.PeekVram(0x2100));
        Assert.Equal(0x31, ppu.PeekRegister(7));
        Assert.Equal(0x31, ppu.PeekRegister(7));
        Assert.Equal(0x80, ppu.PeekRegister(2) & 0x80);
        Assert.Equal(0x80, ppu.PeekRegister(2) & 0x80);
        Assert.Equal(0x2101, ppu.V);
        Assert.Equal(reported, mapper.Reported.Count);

        // The real reads then see what the peeks saw, and do their side effects.
        Assert.Equal(0x31, ppu.ReadRegister(7));
        Assert.Equal(0x80, ppu.ReadRegister(2) & 0x80);
        Assert.Equal(0x00, ppu.PeekRegister(2) & 0x80);
    }

    [Fact]
    public void ResetClearsTheControlRegistersAndKeepsVramOamAndV()
    {
        (Ppu ppu, _) = Build();
        Poke(ppu, 0x2000, 0x44);
        ppu.Oam[3] = 0x55;
        ppu.WriteRegister(0, 0x80);
        ppu.WriteRegister(1, 0x1E);
        ppu.WriteRegister(5, 0x00);
        for (int i = 0; i < 1000; i++)
        {
            ppu.Tick();
        }

        // Rendering has moved v on from $2001; the reset leaves it where it is.
        ushort v = ppu.V;
        ppu.Reset();

        Assert.False(ppu.RenderingEnabled);
        Assert.False(ppu.WriteToggle);
        Assert.Equal(0, ppu.T);
        Assert.Equal(v, ppu.V);
        Assert.Equal((0, 0), (ppu.Line, ppu.Dot));
        Assert.Equal(0x44, ppu.PeekVram(0x2000));
        Assert.Equal(0x55, ppu.Oam[3]);
    }
}
