namespace Dbhq.Machines.Kim1;

/// <summary>
/// The 21 keys in the KIM-1's key matrix. Each value is the code the
/// monitor's GETKEY routine returns for that key. RS, ST and the SST switch
/// are not in the matrix: they are wired to the CPU, and live on
/// <see cref="Kim1Machine"/>.
/// </summary>
public enum Kim1Key
{
    Key0 = 0x00,
    Key1 = 0x01,
    Key2 = 0x02,
    Key3 = 0x03,
    Key4 = 0x04,
    Key5 = 0x05,
    Key6 = 0x06,
    Key7 = 0x07,
    Key8 = 0x08,
    Key9 = 0x09,
    KeyA = 0x0A,
    KeyB = 0x0B,
    KeyC = 0x0C,
    KeyD = 0x0D,
    KeyE = 0x0E,
    KeyF = 0x0F,

    /// <summary>AD: address entry mode.</summary>
    Address = 0x10,

    /// <summary>DA: data entry mode.</summary>
    Data = 0x11,

    /// <summary>+: next address.</summary>
    Plus = 0x12,

    /// <summary>GO: run from the address shown.</summary>
    Go = 0x13,

    /// <summary>PC: recall the program counter saved at the last stop.</summary>
    ProgramCounter = 0x14,
}

/// <summary>
/// The key matrix: three rows, each selected by one output of the 74145
/// decoder, and seven columns read on PA0 to PA6 of the 6530-002. A pressed
/// key pulls its column low while its row is selected.
/// </summary>
/// <remarks>
/// Row r holds keys 7r to 7r + 6, the first on PA6 and the last on PA0.
/// That is the wiring the monitor's GETKEY routine decodes, and the one MAME's
/// KIM-1 driver also uses.
/// </remarks>
public sealed class Kim1Keypad
{
    private readonly bool[] _down = new bool[21];

    public void Press(Kim1Key key) => _down[Index(key)] = true;

    public void Release(Kim1Key key) => _down[Index(key)] = false;

    public void ReleaseAll() => Array.Clear(_down);

    public bool IsPressed(Kim1Key key) => _down[Index(key)];

    /// <summary>
    /// What the keys put on PA0 to PA7 while the decoder selects
    /// <paramref name="row"/>. PA7 is the teletype input, not a column, and
    /// stays high.
    /// </summary>
    public byte Columns(int row)
    {
        byte columns = 0xFF;
        if (row is < 0 or > 2)
        {
            return columns;
        }

        for (int i = 0; i < 7; i++)
        {
            if (_down[row * 7 + i])
            {
                columns &= (byte)~(1 << (6 - i));
            }
        }

        return columns;
    }

    private static int Index(Kim1Key key) =>
        (int)key is >= 0 and <= 0x14 ? (int)key : throw new ArgumentOutOfRangeException(nameof(key), key, "not a key in the matrix");
}
