namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The Model B's keyboard: a matrix of 10 columns by 8 rows holding the 72 keys and the
/// eight start-up links (fact sheet <c>via.md</c> section 3).
/// </summary>
/// <remarks>
/// <para>
/// Columns and rows are named as the OS names them: the column is PA0 to PA3 of the system
/// VIA, 0 to 9, and the row is PA4 to PA6, 0 to 7. Column numbers 10 to 15 select no column.
/// </para>
/// <para>
/// The links are a switch between row 0 and each of columns 2 to 9. Bit n of the start-up
/// byte is column 9 - n, and a made link reads as a pressed key and is stored as 0 once the
/// OS inverts the byte (section 3(c)). The start-up mode is bits 0 to 2. The other five links
/// are left open, so the byte is <c>$F8</c> plus the mode: SHIFT-BREAK boots the disc, the
/// slowest disc timings, and DFS rather than NFS, the sheet's default for every bit but the
/// mode.
/// </para>
/// </remarks>
public sealed class BbcKeyboard
{
    private const int Columns = 16, Rows = 8;

    // Cell column + 16 * row, true when the key is down or the link is made.
    private readonly bool[] _cells = new bool[Columns * Rows];
    private readonly bool[] _links = new bool[Columns * Rows];

    /// <param name="startupMode">The screen mode the links select, 0 to 7.</param>
    public BbcKeyboard(int startupMode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(startupMode, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startupMode, 7);

        int startUpByte = 0xF8 | startupMode;
        for (int bit = 0; bit < 8; bit++)
        {
            _links[9 - bit] = (startUpByte & (1 << bit)) == 0;
        }
    }

    public void Press(BbcKey key) => _cells[Cell(key)] = true;

    public void Release(BbcKey key) => _cells[Cell(key)] = false;

    public bool IsDown(BbcKey key) => _cells[Cell(key)];

    /// <summary>
    /// The matrix cell the system VIA samples on PA7 when the keyboard is enabled: true when a
    /// key is down or a link is made at that column and row.
    /// </summary>
    public bool Read(int column, int row)
    {
        int cell = (column & 0xF) + 16 * (row & 7);
        return _cells[cell] || _links[cell];
    }

    /// <summary>
    /// The line that drives CA2: true when a key in rows 1 to 7 of the column is down. Row 0,
    /// with SHIFT, CTRL and the links, is not wired to the 74LS30 that makes it (section 3(a)).
    /// </summary>
    public bool AnyKeyDown(int column)
    {
        column &= 0xF;
        if (column > 9)
        {
            return false;
        }

        for (int row = 1; row < Rows; row++)
        {
            if (_cells[column + 16 * row])
            {
                return true;
            }
        }
        return false;
    }

    private static int Cell(BbcKey key)
    {
        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key in the matrix.");
        }
        return (int)key;
    }
}
