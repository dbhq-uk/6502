using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// The standard controllers of <c>docs/nes/facts/bus.md</c> section 7, on the bus, in both regions:
/// the strobe, the order of the buttons, the ones after the eighth read, the open-bus bits, and any
/// 8-bit mask reported as written (the spec's Review Focus 4).
/// </summary>
public class ControllerTests
{
    private const byte A = 0x01, B = 0x02, Select = 0x04, Start = 0x08, Up = 0x10, Down = 0x20, Left = 0x40, Right = 0x80;

    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    private static Nes Build(string region)
    {
        var nes = new Nes(Cartridge.Load(TestCartridge.Ines1(1, 1)), region == "PAL" ? Region.Pal : Region.Ntsc);
        nes.PowerOn();
        return nes;
    }

    // Puts a value on the CPU's data bus: the next read of a pad leaves bits 7 to 5 as it was.
    private static void SeedOpenBus(Nes nes, byte value) => nes.Bus.Write(0x0000, value);

    private static void Latch(Nes nes)
    {
        nes.Bus.Write(0x4016, 1);
        nes.Bus.Write(0x4016, 0);
    }

    private static int[] ReadEight(Nes nes, ushort port)
    {
        var bits = new int[8];
        for (int i = 0; i < 8; i++)
        {
            SeedOpenBus(nes, 0x00);
            bits[i] = nes.Bus.Read(port) & 1;
        }

        return bits;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void WithTheStrobeHighEveryReadIsTheAButtonAndNothingAdvances(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, A | Right);
        nes.Bus.Write(0x4016, 1);

        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(1, nes.Bus.Read(0x4016) & 1);
        }

        // Released: the next read follows the buttons, and the strobe is still high.
        nes.SetButtons(0, Right);
        Assert.Equal(0, nes.Bus.Read(0x4016) & 1);

        // The strobe falls: the eight bits are those held now, from the first.
        nes.SetButtons(0, A | Right);
        nes.Bus.Write(0x4016, 0);
        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 0, 1 }, ReadEight(nes, 0x4016));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AStrobeHighThenLowLatchesAndTheNextEightReadsAreInOrder(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, A | Select | Up | Left);

        Latch(nes);

        // A, B, Select, Start, Up, Down, Left, Right.
        Assert.Equal(new[] { 1, 0, 1, 0, 1, 0, 1, 0 }, ReadEight(nes, 0x4016));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EachButtonHasItsOwnPositionInTheReport(string region)
    {
        Nes nes = Build(region);
        byte[] buttons = [A, B, Select, Start, Up, Down, Left, Right];

        for (int position = 0; position < 8; position++)
        {
            nes.SetButtons(0, buttons[position]);
            Latch(nes);
            int[] bits = ReadEight(nes, 0x4016);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(i == position ? 1 : 0, bits[i]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheNinthReadAndAfterAreOne(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, 0x00);
        Latch(nes);
        ReadEight(nes, 0x4016);

        for (int i = 0; i < 10; i++)
        {
            SeedOpenBus(nes, 0x00);
            Assert.Equal(1, nes.Bus.Read(0x4016) & 1);
        }
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheButtonsAreLatchedWhenTheStrobeFallsNotWhenTheyAreRead(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, A);
        Latch(nes);

        // A change after the latch is not seen until the next latch.
        nes.SetButtons(0, B);
        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 0, 0 }, ReadEight(nes, 0x4016));

        Latch(nes);
        Assert.Equal(new[] { 0, 1, 0, 0, 0, 0, 0, 0 }, ReadEight(nes, 0x4016));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AWriteOfZeroToTheStrobeWhileItIsAlreadyLowDoesNotReload(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, A | B);
        Latch(nes);
        SeedOpenBus(nes, 0);
        nes.Bus.Read(0x4016);

        nes.Bus.Write(0x4016, 0);

        // The second read is B, not A again.
        SeedOpenBus(nes, 0);
        Assert.Equal(1, nes.Bus.Read(0x4016) & 1);
        SeedOpenBus(nes, 0);
        Assert.Equal(0, nes.Bus.Read(0x4016) & 1);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void OnlyBitZeroOfTheWriteIsTheStrobe(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, B);

        // $FE has bit 0 low and every other bit high: no strobe.
        nes.Bus.Write(0x4016, 0x01);
        nes.Bus.Write(0x4016, 0xFE);
        Assert.Equal(new[] { 0, 1, 0, 0, 0, 0, 0, 0 }, ReadEight(nes, 0x4016));

        // $03 has bit 0 high: strobe on.
        nes.Bus.Write(0x4016, 0x03);
        SeedOpenBus(nes, 0);
        Assert.Equal(0, nes.Bus.Read(0x4016) & 1);
        nes.SetButtons(0, A);
        SeedOpenBus(nes, 0);
        Assert.Equal(1, nes.Bus.Read(0x4016) & 1);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheStrobeWriteReachesBothPadsAndEachPortReadsItsOwn(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, A);
        nes.SetButtons(1, B | Right);

        Latch(nes);

        Assert.Equal(new[] { 1, 0, 0, 0, 0, 0, 0, 0 }, ReadEight(nes, 0x4016));
        Assert.Equal(new[] { 0, 1, 0, 0, 0, 0, 0, 1 }, ReadEight(nes, 0x4017));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AWriteTo4017DoesNotStrobeThePads(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, B);
        Latch(nes);
        SeedOpenBus(nes, 0);
        nes.Bus.Read(0x4016);

        // Ruling B: $4017 is the frame counter's. Bit 0 is not a strobe, so the pad is not reloaded.
        nes.Bus.Write(0x4017, 0x01);

        SeedOpenBus(nes, 0);
        Assert.Equal(1, nes.Bus.Read(0x4016) & 1);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void TheUpperBitsOfAReadAreTheOpenBusAndBitsFourToOneAreZero(string region)
    {
        Nes nes = Build(region);

        // The bus holds $FF: nothing pressed gives $E0, not $FF. The pad drives bits 4 to 0 only,
        // and D4 to D1 read 0 when nothing drives them.
        nes.SetButtons(0, 0x00);
        nes.SetButtons(1, 0x00);
        Latch(nes);
        SeedOpenBus(nes, 0xFF);
        Assert.Equal(0xE0, nes.Bus.Read(0x4016));
        SeedOpenBus(nes, 0xFF);
        Assert.Equal(0xE0, nes.Bus.Read(0x4017));

        // With A held, bit 0 joins the open-bus bits.
        nes.SetButtons(0, A);
        nes.SetButtons(1, A);
        Latch(nes);
        SeedOpenBus(nes, 0xFF);
        Assert.Equal(0xE1, nes.Bus.Read(0x4016));
        SeedOpenBus(nes, 0xFF);
        Assert.Equal(0xE1, nes.Bus.Read(0x4017));

        // And a bus of zero gives zero in them.
        Latch(nes);
        SeedOpenBus(nes, 0x00);
        Assert.Equal(0x01, nes.Bus.Read(0x4016));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ABusOf40GivesThe40And41OfTheSheetsWorkedExample(string region)
    {
        Nes nes = Build(region);

        // bus.md worked example 5: A and Right held, eight reads with the bus at $40.
        nes.SetButtons(0, A | Right);
        Latch(nes);
        var reads = new List<byte>();
        for (int i = 0; i < 9; i++)
        {
            SeedOpenBus(nes, 0x40);
            reads.Add(nes.Bus.Read(0x4016));
        }

        Assert.Equal(new byte[] { 0x41, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x41, 0x41 }, reads);
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void ALoadOfA4016WithTheHighByteOn4016ReadsTheSheetsExample(string region)
    {
        // The open bus a real LDA $4016 sees is the operand's high byte, $40.
        var nes = new Nes(Cartridge.Load(TestCartridge.Join(
            TestCartridge.Ines1(1, 1)[..16],
            Program(0xAD, 0x16, 0x40),
            new byte[8192])), region == "PAL" ? Region.Pal : Region.Ntsc);
        nes.PowerOn();
        nes.SetButtons(0, A);
        Latch(nes);

        nes.Step();

        Assert.Equal(0x41, nes.Cpu.A);
    }

    // A 16 KB PRG with the code at $C000 and the reset vector pointing to it.
    private static byte[] Program(params byte[] code)
    {
        byte[] prg = new byte[16384];
        code.CopyTo(prg, 0);
        prg[0x3FFC] = 0x00;
        prg[0x3FFD] = 0xC0;
        return prg;
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void AnyMaskIsReportedAsWrittenIncludingLeftWithRightAndUpWithDown(string region)
    {
        Nes nes = Build(region);

        // Review Focus 4: nothing filters the impossible masks.
        nes.SetButtons(0, 0xC0);
        Latch(nes);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 1, 1 }, ReadEight(nes, 0x4016));

        nes.SetButtons(0, 0x30);
        Latch(nes);
        Assert.Equal(new[] { 0, 0, 0, 0, 1, 1, 0, 0 }, ReadEight(nes, 0x4016));

        nes.SetButtons(1, 0xF0);
        Latch(nes);
        Assert.Equal(new[] { 0, 0, 0, 0, 1, 1, 1, 1 }, ReadEight(nes, 0x4017));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void EveryEightBitMaskComesBackBitForBit(string region)
    {
        Nes nes = Build(region);

        for (int mask = 0; mask < 256; mask++)
        {
            nes.SetButtons(1, (byte)mask);
            Latch(nes);
            int[] bits = ReadEight(nes, 0x4017);
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal((mask >> i) & 1, bits[i]);
            }
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void SetButtonsRefusesAPadThatIsNotZeroOrOne(int pad)
    {
        Nes nes = Build("NTSC");

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => nes.SetButtons(pad, 0xFF));
        Assert.Equal("pad", error.ParamName);
    }

    [Fact]
    public void ThePadIsAClassOfItsOwnWithButtonsAStrobeAndARead()
    {
        var pad = new Controller { Buttons = Start | Down };

        pad.Strobe(true);
        Assert.Equal(0, pad.Read());
        pad.Strobe(false);

        Assert.Equal(new byte[] { 0, 0, 0, 1, 0, 1, 0, 0, 1, 1 }, Enumerable.Range(0, 10).Select(_ => pad.Read()).ToArray());
    }

    [Fact]
    public void PowerOnStartsTheReportAgainAndKeepsTheButtonsHeld()
    {
        Nes nes = Build("NTSC");
        nes.SetButtons(0, A | B);
        Latch(nes);
        SeedOpenBus(nes, 0);
        nes.Bus.Read(0x4016);

        nes.PowerOn();

        // Held buttons are the player's and stay; the position in the report starts again.
        Assert.Equal(A | B, nes.Bus.GetController(0).Buttons);
        Latch(nes);
        Assert.Equal(new[] { 1, 1, 0, 0, 0, 0, 0, 0 }, ReadEight(nes, 0x4016));
    }

    [Theory]
    [MemberData(nameof(Regions))]
    public void PeekOfAPortShowsTheNextBitWithoutAdvancing(string region)
    {
        Nes nes = Build(region);
        nes.SetButtons(0, B);
        Latch(nes);

        Assert.Equal(0, nes.Bus.Peek(0x4016) & 1);
        Assert.Equal(0, nes.Bus.Peek(0x4016) & 1);
        SeedOpenBus(nes, 0);
        Assert.Equal(0, nes.Bus.Read(0x4016) & 1);
        Assert.Equal(1, nes.Bus.Peek(0x4016) & 1);
    }
}
