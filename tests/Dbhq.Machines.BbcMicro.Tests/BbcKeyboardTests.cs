using Xunit;

namespace Dbhq.Machines.BbcMicro.Tests;

/// <summary>
/// The keyboard matrix on its own. Every position is typed from the fact sheet
/// <c>docs/bbc-micro/facts/via.md</c> section 3(b), "Full list, sorted by internal number",
/// and the start-up links from section 3(c). Columns are PA0 to PA3 and rows PA4 to PA6, as
/// the OS and the Advanced User Guide name them (the Service Manual swaps the names).
/// </summary>
public class BbcKeyboardTests
{
    /// <summary>
    /// The 72 keys, typed from the sheet's list in its order: the key, its column, its row.
    /// The expected cell comes from these two numbers, never from the key's own value.
    /// </summary>
    public static readonly (BbcKey Key, int Column, int Row)[] SheetTable =
    [
        (BbcKey.Shift, 0, 0), (BbcKey.Ctrl, 1, 0),
        (BbcKey.Q, 0, 1), (BbcKey.D3, 1, 1), (BbcKey.D4, 2, 1), (BbcKey.D5, 3, 1), (BbcKey.F4, 4, 1),
        (BbcKey.D8, 5, 1), (BbcKey.F7, 6, 1), (BbcKey.Minus, 7, 1), (BbcKey.Caret, 8, 1), (BbcKey.Left, 9, 1),
        (BbcKey.F0, 0, 2), (BbcKey.W, 1, 2), (BbcKey.E, 2, 2), (BbcKey.T, 3, 2), (BbcKey.D7, 4, 2),
        (BbcKey.I, 5, 2), (BbcKey.D9, 6, 2), (BbcKey.D0, 7, 2), (BbcKey.Underscore, 8, 2), (BbcKey.Down, 9, 2),
        (BbcKey.D1, 0, 3), (BbcKey.D2, 1, 3), (BbcKey.D, 2, 3), (BbcKey.R, 3, 3), (BbcKey.D6, 4, 3),
        (BbcKey.U, 5, 3), (BbcKey.O, 6, 3), (BbcKey.P, 7, 3), (BbcKey.LeftBracket, 8, 3), (BbcKey.Up, 9, 3),
        (BbcKey.CapsLock, 0, 4), (BbcKey.A, 1, 4), (BbcKey.X, 2, 4), (BbcKey.F, 3, 4), (BbcKey.Y, 4, 4),
        (BbcKey.J, 5, 4), (BbcKey.K, 6, 4), (BbcKey.At, 7, 4), (BbcKey.Colon, 8, 4), (BbcKey.Return, 9, 4),
        (BbcKey.ShiftLock, 0, 5), (BbcKey.S, 1, 5), (BbcKey.C, 2, 5), (BbcKey.G, 3, 5), (BbcKey.H, 4, 5),
        (BbcKey.N, 5, 5), (BbcKey.L, 6, 5), (BbcKey.Semicolon, 7, 5), (BbcKey.RightBracket, 8, 5), (BbcKey.Delete, 9, 5),
        (BbcKey.Tab, 0, 6), (BbcKey.Z, 1, 6), (BbcKey.Space, 2, 6), (BbcKey.V, 3, 6), (BbcKey.B, 4, 6),
        (BbcKey.M, 5, 6), (BbcKey.Comma, 6, 6), (BbcKey.FullStop, 7, 6), (BbcKey.Slash, 8, 6), (BbcKey.Copy, 9, 6),
        (BbcKey.Escape, 0, 7), (BbcKey.F1, 1, 7), (BbcKey.F2, 2, 7), (BbcKey.F3, 3, 7), (BbcKey.F5, 4, 7),
        (BbcKey.F6, 5, 7), (BbcKey.F8, 6, 7), (BbcKey.F9, 7, 7), (BbcKey.Backslash, 8, 7), (BbcKey.Right, 9, 7),
    ];

    public static TheoryData<BbcKey, int, int> Keys()
    {
        var data = new TheoryData<BbcKey, int, int>();
        foreach (var (key, column, row) in SheetTable)
        {
            data.Add(key, column, row);
        }
        return data;
    }

    [Fact]
    public void TheSheetListsSeventyTwoKeysInSeventyTwoCellsAndTheEnumHasEachOnce()
    {
        Assert.Equal(72, SheetTable.Length);
        Assert.Equal(72, SheetTable.Select(k => k.Column + k.Row * 16).Distinct().Count());
        Assert.Equal(
            Enum.GetValues<BbcKey>().OrderBy(k => k.ToString()),
            SheetTable.Select(k => k.Key).OrderBy(k => k.ToString()));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void PressingAKeyClosesItsCellAndNoOther(BbcKey key, int column, int row)
    {
        var keyboard = new BbcKeyboard(7); // every link open, so row 0 holds only what is pressed
        keyboard.Press(key);

        Assert.True(keyboard.IsDown(key));
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                Assert.Equal(c == column && r == row, keyboard.Read(c, r));
            }
        }

        keyboard.Release(key);
        Assert.False(keyboard.IsDown(key));
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                Assert.False(keyboard.Read(c, r));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void OnlyAKeyInRowsOneToSevenRaisesItsColumnsInterruptLine(BbcKey key, int column, int row)
    {
        // The 74LS30 that drives CA2 sees rows 1 to 7; row 0 (SHIFT, CTRL, the links) is not
        // connected to it (via.md section 3(a)).
        var keyboard = new BbcKeyboard(0); // links made in row 0 must not raise it either
        keyboard.Press(key);

        for (int c = 0; c < 16; c++)
        {
            Assert.Equal(c == column && row != 0, keyboard.AnyKeyDown(c));
        }
    }

    /// <summary>
    /// What the OS does at <c>$DA11</c>: for X = 9 down to 1, test key X (column X, row 0)
    /// and <c>ROR $FC</c> with the carry set when it is down; then <c>ROL $FC</c>, which
    /// leaves CTRL in the carry, and <c>$FC EOR $FF</c> is the start-up byte (via.md s3(c),
    /// and the ROM at <c>$DA10-$DA3D</c>).
    /// </summary>
    private static (byte StartUp, bool Ctrl) ReadLinksAsTheOsDoes(Func<int, bool> keyDown)
    {
        int fc = 0x00;
        bool carry = false;
        for (int x = 9; x >= 1; x--)
        {
            bool pressed = keyDown(x);
            bool outBit = (fc & 1) != 0;
            fc = (fc >> 1) | (pressed ? 0x80 : 0);
            carry = outBit;
        }

        bool ctrl = (fc & 0x80) != 0;
        fc = ((fc << 1) & 0xFF) | (carry ? 1 : 0);
        return ((byte)(fc ^ 0xFF), ctrl);
    }

    [Theory]
    [InlineData(7, 0xFF)] // every link open: mode 7, the sheet's default byte
    [InlineData(0, 0xF8)] // links made on columns 9, 8 and 7: mode 0
    [InlineData(3, 0xFB)]
    [InlineData(6, 0xFE)]
    public void TheLinksGiveTheStartUpByteTheOsReads(int mode, int expected)
    {
        var keyboard = new BbcKeyboard(mode);

        var (startUp, ctrl) = ReadLinksAsTheOsDoes(column => keyboard.Read(column, 0));

        Assert.Equal(expected, startUp);
        Assert.Equal(mode, startUp & 7);
        Assert.False(ctrl);
    }

    [Fact]
    public void CtrlIsColumnOneOnTheLinkScanAndEndsInTheCarry()
    {
        var keyboard = new BbcKeyboard(7);
        keyboard.Press(BbcKey.Ctrl);

        var (startUp, ctrl) = ReadLinksAsTheOsDoes(column => keyboard.Read(column, 0));

        Assert.True(ctrl);
        Assert.Equal(0xFF, startUp); // CTRL does not leak into the start-up byte
    }

    [Fact]
    public void ALinkIsInRowZeroColumnNineLessItsBit()
    {
        // Bit n of the start-up byte is column 9 - n (via.md section 3(c)); a made link reads
        // as a pressed key and stores as 0.
        var keyboard = new BbcKeyboard(5); // 101: bits 0 and 2 set, bit 1 clear
        Assert.False(keyboard.Read(9, 0));
        Assert.True(keyboard.Read(8, 0));
        Assert.False(keyboard.Read(7, 0));
        for (int column = 2; column <= 6; column++)
        {
            Assert.False(keyboard.Read(column, 0));
        }
    }

    [Fact]
    public void ColumnsTenToFifteenSelectNothing()
    {
        var keyboard = new BbcKeyboard(0);
        foreach (var (key, _, _) in SheetTable)
        {
            keyboard.Press(key);
        }

        for (int column = 10; column < 16; column++)
        {
            Assert.False(keyboard.AnyKeyDown(column));
            for (int row = 0; row < 8; row++)
            {
                Assert.False(keyboard.Read(column, row));
            }
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void AStartUpModeOutsideZeroToSevenIsRefused(int mode)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BbcKeyboard(mode));
    }
}
