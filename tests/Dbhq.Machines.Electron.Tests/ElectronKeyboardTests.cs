using Xunit;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// The keyboard matrix (ula.md s7). The table in s7b is copied below as data, and every test's
/// expected value comes from it or from arithmetic on an address, never from the model.
/// </summary>
public class ElectronKeyboardTests
{
    /// <summary>
    /// ula.md s7b, column by column: the key at bit 0, 1, 2 and 3. An empty name is a position
    /// that is not connected. The two shifts are one position and Caps Lock and Func are one key.
    /// </summary>
    private static readonly string[][] Matrix =
    [
        ["Right", "Copy", "", "Space"],         // 0, $BFFE
        ["Left", "Down", "Return", "Delete"],   // 1, $BFFD
        ["Minus", "Up", "Colon", ""],           // 2, $BFFB
        ["D0", "P", "Semicolon", "Slash"],      // 3, $BFF7
        ["D9", "O", "L", "FullStop"],           // 4, $BFEF
        ["D8", "I", "K", "Comma"],              // 5, $BFDF
        ["D7", "U", "J", "M"],                  // 6, $BFBF
        ["D6", "Y", "H", "N"],                  // 7, $BF7F
        ["D5", "T", "G", "B"],                  // 8, $BEFF
        ["D4", "R", "F", "V"],                  // 9, $BDFF
        ["D3", "E", "D", "C"],                  // 10, $BBFF
        ["D2", "W", "S", "X"],                  // 11, $B7FF
        ["D1", "Q", "A", "Z"],                  // 12, $AFFF
        ["Escape", "CapsLock", "Ctrl", "Shift"], // 13, $9FFF
    ];

    private const int Columns = 14;
    private const int NotConnected = 2;

    private static ElectronKey Key(string name) => Enum.Parse<ElectronKey>(name);

    /// <summary>The address with only this column's line low, in the paged ROM window (s7b).</summary>
    private static ushort ColumnAddress(int column) => (ushort)(0x8000 | (0x3FFF & ~(1 << column)));

    /// <summary>Every connected position in the table: column, bit and key.</summary>
    public static IEnumerable<object[]> Positions()
    {
        for (int column = 0; column < Columns; column++)
        {
            for (int bit = 0; bit < 4; bit++)
            {
                if (Matrix[column][bit].Length > 0)
                {
                    yield return [column, bit, Matrix[column][bit]];
                }
            }
        }
    }

    [Fact]
    public void TheTableAddressesAreTheOnesTheSheetGives()
    {
        // The test's own address rule against s7b's Address column, so a wrong rule cannot hide.
        ushort[] sheet =
        [
            0xBFFE, 0xBFFD, 0xBFFB, 0xBFF7, 0xBFEF, 0xBFDF, 0xBFBF, 0xBF7F,
            0xBEFF, 0xBDFF, 0xBBFF, 0xB7FF, 0xAFFF, 0x9FFF,
        ];
        for (int column = 0; column < Columns; column++)
        {
            Assert.Equal(sheet[column], ColumnAddress(column));
        }
    }

    [Fact]
    public void ThereIsOneKeyForEveryConnectedPositionInTheTable()
    {
        // 14 columns of 4 bits, less the two positions s7b marks "not connected".
        int expected = Columns * 4 - NotConnected;
        string[] names = Matrix.SelectMany(c => c).Where(n => n.Length > 0).ToArray();
        Assert.Equal(expected, names.Length);
        Assert.Equal(expected, names.Distinct().Count());
        Assert.Equal(NotConnected, Matrix.SelectMany(c => c).Count(n => n.Length == 0));

        // The enum is exactly those keys: no more, no fewer, no key the table does not have.
        Assert.Equal(expected, Enum.GetValues<ElectronKey>().Length);
        Assert.Equal(names.Order(StringComparer.Ordinal), Enum.GetNames<ElectronKey>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BothShiftsAreOneKeyAndCapsLockAndFuncAreOne()
    {
        string[] names = Enum.GetNames<ElectronKey>();
        Assert.Single(names, "Shift");
        Assert.Single(names, "CapsLock");
        Assert.DoesNotContain(names, n => n.Contains("Func", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Left", StringComparison.Ordinal) && n.Contains("Shift", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("Right", StringComparison.Ordinal) && n.Contains("Shift", StringComparison.Ordinal));
        Assert.Equal(1, Matrix.SelectMany(c => c).Count(n => n == "Shift"));
        Assert.Equal(1, Matrix.SelectMany(c => c).Count(n => n == "CapsLock"));
    }

    [Fact]
    public void BreakIsNotAKey()
    {
        // s7c: BREAK is a switch on the reset line, not a matrix position.
        Assert.DoesNotContain(Enum.GetNames<ElectronKey>(), n => n.Contains("Break", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NothingPressedReadsZeroEverywhere()
    {
        var keyboard = new ElectronKeyboard();
        for (int address = 0x8000; address < 0xC000; address++)
        {
            Assert.Equal(0, keyboard.Read((ushort)address));
        }
    }

    [Theory]
    [MemberData(nameof(Positions))]
    public void AKeyAloneReadsExactlyItsBitAtItsOwnColumn(int column, int bit, string name)
    {
        var keyboard = new ElectronKeyboard();
        keyboard.Down(Key(name));
        Assert.Equal(1 << bit, keyboard.Read(ColumnAddress(column)));

        // Every other column's own address reads 0: the key is in one column only.
        for (int other = 0; other < Columns; other++)
        {
            if (other != column)
            {
                Assert.Equal(0, keyboard.Read(ColumnAddress(other)));
            }
        }

        // With every column line high no column is selected.
        Assert.Equal(0, keyboard.Read(0xBFFF));
    }

    [Fact]
    public void EveryKeyDownReadsZeroWhenNoColumnLineIsLow()
    {
        var keyboard = new ElectronKeyboard();
        foreach (ElectronKey key in Enum.GetValues<ElectronKey>())
        {
            keyboard.Down(key);
        }

        Assert.Equal(0, keyboard.Read(0xBFFF));
    }

    [Fact]
    public void EveryKeyDownReadsAllFourBitsAndNoMoreAtAnyAddressWithALineLow()
    {
        // Bits 7 to 4 read 0 (s7a), and column 0 and 2 each have a position that is not
        // connected, but with every other column down the four bits are all set at $A000.
        var keyboard = new ElectronKeyboard();
        foreach (ElectronKey key in Enum.GetValues<ElectronKey>())
        {
            keyboard.Down(key);
        }

        Assert.Equal(0x0F, keyboard.Read(0xA000));
        Assert.Equal(0x0F, keyboard.Read(0x8000));
    }

    [Fact]
    public void NotConnectedPositionsNeverReadAsSet()
    {
        // Column 0 bit 2 and column 2 bit 3 (s7b): with every key down they still read 0.
        var keyboard = new ElectronKeyboard();
        foreach (ElectronKey key in Enum.GetValues<ElectronKey>())
        {
            keyboard.Down(key);
        }

        Assert.Equal(0b1011, keyboard.Read(ColumnAddress(0)));
        Assert.Equal(0b0111, keyboard.Read(ColumnAddress(2)));
    }

    [Fact]
    public void TwoKeysInOneColumnReadAsTheirBitsTogether()
    {
        // Q is column 12 bit 1 and A is column 12 bit 2.
        var keyboard = new ElectronKeyboard();
        keyboard.Down(ElectronKey.Q);
        keyboard.Down(ElectronKey.A);
        Assert.Equal(0b0110, keyboard.Read(0xAFFF));
    }

    [Fact]
    public void TwoColumnsSelectedAtOnceReadAsTheOrOfBoth()
    {
        // $A7FF has A12 and A11 low: column 12 and column 11. Q is column 12 bit 1, W is
        // column 11 bit 1 and S is column 11 bit 2.
        var keyboard = new ElectronKeyboard();
        keyboard.Down(ElectronKey.Q);
        Assert.Equal(0b0010, keyboard.Read(0xA7FF));
        keyboard.Down(ElectronKey.S);
        Assert.Equal(0b0110, keyboard.Read(0xA7FF));
        keyboard.Down(ElectronKey.Z);
        Assert.Equal(0b1110, keyboard.Read(0xA7FF));

        // Each column on its own still reads only its own keys.
        Assert.Equal(0b1010, keyboard.Read(0xAFFF)); // Q and Z
        Assert.Equal(0b0100, keyboard.Read(0xB7FF)); // S
    }

    [Fact]
    public void SeveralKeysInDifferentColumnsAreAllSeenAtOnceAndKeepTheirOwnColumns()
    {
        var keyboard = new ElectronKeyboard();
        keyboard.Down(ElectronKey.Shift);   // column 13 bit 3
        keyboard.Down(ElectronKey.A);       // column 12 bit 2
        keyboard.Down(ElectronKey.Return);  // column 1 bit 2
        keyboard.Down(ElectronKey.Space);   // column 0 bit 3
        Assert.Equal(0b1000, keyboard.Read(0x9FFF));
        Assert.Equal(0b0100, keyboard.Read(0xAFFF));
        Assert.Equal(0b0100, keyboard.Read(0xBFFD));
        Assert.Equal(0b1000, keyboard.Read(0xBFFE));
        Assert.Equal(0b1100, keyboard.Read(0xA000)); // columns 0 to 12: A, Return and Space
    }

    [Fact]
    public void ShiftCtrlAndEscapeShareColumnThirteenAndAllShowAtOnce()
    {
        var keyboard = new ElectronKeyboard();
        keyboard.Down(ElectronKey.Shift);
        keyboard.Down(ElectronKey.Ctrl);
        Assert.Equal(0b1100, keyboard.Read(0x9FFF));
        keyboard.Down(ElectronKey.Escape);
        Assert.Equal(0b1101, keyboard.Read(0x9FFF));
        keyboard.Down(ElectronKey.CapsLock);
        Assert.Equal(0b1111, keyboard.Read(0x9FFF));
    }

    [Fact]
    public void CapsLockAndFuncPressedWithAnotherKeyAreTwoDistinctPositions()
    {
        // s7b: Caps Lock / Func is one key, pressed with another to make a function key
        // (FUNC plus K is CHAIN). The matrix just sees the two positions.
        var keyboard = new ElectronKeyboard();
        keyboard.Down(ElectronKey.CapsLock);
        keyboard.Down(ElectronKey.K);
        Assert.Equal(0b0010, keyboard.Read(0x9FFF));
        Assert.Equal(0b0100, keyboard.Read(ColumnAddress(5))); // K, bit 2
    }

    [Fact]
    public void TheOsProbeAtA000SeesColumnsZeroToTwelveButNotColumnThirteen()
    {
        // s7c: the OS uses $A000 (A13 high, A0 to A12 low) for "any key in columns 0 to 12" and
        // $9FFF for column 13 alone.
        foreach (ElectronKey key in new[] { ElectronKey.Escape, ElectronKey.CapsLock, ElectronKey.Ctrl, ElectronKey.Shift })
        {
            var keyboard = new ElectronKeyboard();
            keyboard.Down(key);
            Assert.Equal(0, keyboard.Read(0xA000));
            Assert.NotEqual(0, keyboard.Read(0x9FFF));
        }

        for (int column = 0; column < Columns - 1; column++)
        {
            for (int bit = 0; bit < 4; bit++)
            {
                if (Matrix[column][bit].Length == 0)
                {
                    continue;
                }

                var keyboard = new ElectronKeyboard();
                keyboard.Down(Key(Matrix[column][bit]));
                Assert.NotEqual(0, keyboard.Read(0xA000));
                Assert.Equal(0, keyboard.Read(0x9FFF));
            }
        }
    }

    [Fact]
    public void UpReleasesAKey()
    {
        var keyboard = new ElectronKeyboard();
        keyboard.Down(ElectronKey.Q);
        Assert.True(keyboard.IsDown(ElectronKey.Q));
        keyboard.Up(ElectronKey.Q);
        Assert.False(keyboard.IsDown(ElectronKey.Q));
        Assert.Equal(0, keyboard.Read(0xAFFF));
    }

    [Fact]
    public void AKeyIsAStateNotACount()
    {
        var keyboard = new ElectronKeyboard();
        keyboard.Down(ElectronKey.Q);
        keyboard.Down(ElectronKey.Q);
        keyboard.Up(ElectronKey.Q);
        Assert.False(keyboard.IsDown(ElectronKey.Q));
        Assert.Equal(0, keyboard.Read(0xAFFF));

        // Releasing a key that is not down changes nothing, here or for its neighbour.
        keyboard.Down(ElectronKey.A);
        keyboard.Up(ElectronKey.Q);
        Assert.True(keyboard.IsDown(ElectronKey.A));
        Assert.Equal(0b0100, keyboard.Read(0xAFFF));
    }

    [Fact]
    public void IsDownSaysOnlyAboutItsOwnKey()
    {
        var keyboard = new ElectronKeyboard();
        keyboard.Down(ElectronKey.Q);
        Assert.False(keyboard.IsDown(ElectronKey.A));
        Assert.False(keyboard.IsDown(ElectronKey.W));
    }

    [Theory]
    [InlineData(0x20)] // column 0 bit 2: not connected
    [InlineData(0x32)] // column 2 bit 3: not connected
    [InlineData(0x0E)] // there is no column 14
    [InlineData(0x40)] // there is no bit 4
    public void AValueThatIsNotAKeyIsRefused(int value)
    {
        var keyboard = new ElectronKeyboard();
        Assert.Throws<ArgumentOutOfRangeException>(() => keyboard.Down((ElectronKey)value));
        Assert.Throws<ArgumentOutOfRangeException>(() => keyboard.Up((ElectronKey)value));
        Assert.Throws<ArgumentOutOfRangeException>(() => keyboard.IsDown((ElectronKey)value));
    }

    // --- on the bus ---

    private static readonly byte[] Os = RepoPaths.ReadChecked(Pins.ElectronOsPath, Pins.ElectronOsSha256);
    private static readonly byte[] Basic = RepoPaths.ReadChecked(Pins.BbcBasicPath, Pins.BbcBasicSha256);

    private static ElectronBus NewBus() => new(new ElectronRoms(Os, Basic));

    private static void Select(ElectronBus bus, int slot)
    {
        // The OS's own sequence (s2a): $0C first, so the register accepts any slot, then the slot.
        bus.Write(0xFE05, 0x0C);
        bus.Write(0xFE05, (byte)slot);
        Assert.Equal(slot, bus.RomSlot);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    public void SlotsEightAndNineBothReadTheKeyboard(int slot)
    {
        ElectronBus bus = NewBus();
        Select(bus, slot);
        Assert.Equal(0, bus.Peek(0x9FFF));
        bus.Keyboard.Down(ElectronKey.Escape);
        Assert.Equal(0b0001, bus.Peek(0x9FFF));
        Assert.Equal(0b0001, bus.Read(0x9FFF));
        bus.Keyboard.Down(ElectronKey.Shift);
        Assert.Equal(0b1001, bus.Read(0x9FFF));
        Assert.Equal(0, bus.Read(0xBFFF));
    }

    [Fact]
    public void TheKeyboardIsTheSameDeviceInSlotsEightAndNine()
    {
        ElectronBus bus = NewBus();
        bus.Keyboard.Down(ElectronKey.Q);
        bus.Keyboard.Down(ElectronKey.A);
        Select(bus, 8);
        byte inEight = bus.Read(0xAFFF);
        Select(bus, 9);
        Assert.Equal(inEight, bus.Read(0xAFFF));
        Assert.Equal(0b0110, inEight);
    }

    [Fact]
    public void SlotTenReadsBasicNotTheKeyboard()
    {
        ElectronBus bus = NewBus();
        bus.Keyboard.Down(ElectronKey.Escape);
        Select(bus, 10);
        Assert.Equal(Basic[0x1FFF], bus.Read(0x9FFF));
        Assert.Equal(Basic[0x0000], bus.Read(0x8000));
    }

    [Fact]
    public void ABasicSlotOrAnEmptyOneNeverShowsAKeyAtTheKeyboardAddresses()
    {
        ElectronBus bus = NewBus();
        bus.Keyboard.Down(ElectronKey.Escape);
        Select(bus, 12);
        Assert.Equal(0x9F, bus.Read(0x9FFF)); // empty: the high byte of the address, task 2's rule
    }

    [Fact]
    public void AReadInTheKeyboardSlotIsAOneMegahertzAccess()
    {
        // The cost rule is task 2's and does not change: slots 8 and 9 are I/O, so a read is two
        // cycles from an even count.
        ElectronBus bus = NewBus();
        Select(bus, 8);
        long before = bus.Cycles;
        if ((before & 1) != 0)
        {
            bus.Read(0xC000); // one ROM cycle to land on an even count
            before = bus.Cycles;
        }

        bus.Read(0x9FFF);
        Assert.Equal(before + 2, bus.Cycles);
    }
}
