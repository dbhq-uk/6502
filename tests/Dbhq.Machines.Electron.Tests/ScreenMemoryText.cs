namespace Dbhq.Machines.Electron.Tests;

/// <summary>
/// Reads mode 6 text out of screen memory, before the display exists. In mode 6 the OS writes
/// each character as an 8-byte cell at <c>$6000 + row x 320 + column x 8</c>, one byte for each
/// of its eight lines, and the bytes are the character's glyph in the OS ROM's font:
/// <c>$C000 + (ch - $20) x 8</c> for <c>$20</c> to <c>$7F</c> (<c>ula.md</c> s10b). So a cell
/// is read back by matching its 8 bytes against the font. The glyphs come from the ROM and the
/// text from the ROM running on the CPU, so the check is not circular.
/// </summary>
public static class ScreenMemoryText
{
    /// <summary>Mode 6 screen memory starts here (<c>ula.md</c> s10b).</summary>
    public const int ScreenStart = 0x6000;

    public const int Rows = 25;

    public const int Columns = 40;

    /// <summary>Bytes in one character row: 40 cells of 8 bytes.</summary>
    public const int BytesPerRow = Columns * 8;

    /// <summary>
    /// What a cell that matches no glyph reads as: the bell glyph and the cursor, for instance. It
    /// is not printable ASCII, so it cannot be mistaken for a character the OS printed.
    /// </summary>
    public const char Unknown = '\uFFFD';

    /// <summary>The address of the first byte of the cell at <paramref name="row"/>, <paramref name="column"/>.</summary>
    public static ushort CellAddress(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Columns);
        return (ushort)(ScreenStart + (row * BytesPerRow) + (column * 8));
    }

    /// <summary>The 8 bytes of one cell, read without a bus cycle.</summary>
    public static byte[] Cell(ElectronBus bus, int row, int column)
    {
        ushort address = CellAddress(row, column);
        var bytes = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            bytes[i] = bus.Peek((ushort)(address + i));
        }

        return bytes;
    }

    /// <summary>The 25 rows of 40 characters, each cell matched against the font in <paramref name="os"/>.</summary>
    public static string[] Read(ElectronBus bus, byte[] os)
    {
        ArgumentNullException.ThrowIfNull(bus);
        Dictionary<ulong, char> font = Font(os);
        var rows = new string[Rows];
        for (int row = 0; row < Rows; row++)
        {
            var text = new char[Columns];
            for (int column = 0; column < Columns; column++)
            {
                text[column] = font.TryGetValue(Key(Cell(bus, row, column)), out char c) ? c : Unknown;
            }

            rows[row] = new string(text);
        }

        return rows;
    }

    /// <summary>The font at OS <c>$C000</c>, file offset 0: characters <c>$20</c> to <c>$7F</c>, 8 bytes each.</summary>
    private static Dictionary<ulong, char> Font(byte[] os)
    {
        ArgumentNullException.ThrowIfNull(os);
        var font = new Dictionary<ulong, char>();
        for (int ch = 0x20; ch <= 0x7F; ch++)
        {
            // TryAdd: should two glyphs be the same, the lower code is the one reported.
            font.TryAdd(Key(os.AsSpan((ch - 0x20) * 8, 8)), (char)ch);
        }

        return font;
    }

    private static ulong Key(ReadOnlySpan<byte> cell) => BitConverter.ToUInt64(cell);
}
