namespace Dbhq.Machines.Electron;

/// <summary>
/// The Electron's keyboard: 14 columns of 4 keys, read as the paged "ROM" in slots 8 and 9 (fact
/// sheet <c>ula.md</c> section 7).
/// </summary>
/// <remarks>
/// <para>
/// Address lines A0 to A13 are the columns, and a column is selected when its line is low. Data
/// lines D0 to D3 are the four rows, and a bit is 1 when a key in a selected column is down.
/// Several low lines at once return the OR of those columns. Bits 7 to 4 read 0, and so does
/// everything when no key is down or no line is low (s7a).
/// </para>
/// <para>
/// The keyboard is a device, not a ROM image: slots 8 and 9 are the same device, so the bus
/// calls <see cref="Read"/> for either. Only the low 14 address bits matter: the bus has already
/// decided that the address is in the paged window.
/// </para>
/// </remarks>
public sealed class ElectronKeyboard
{
    private const int Columns = 14;
    private const int ColumnMask = (1 << Columns) - 1;

    // The values that are keys: every other value, such as a position that is not connected, is refused.
    private static readonly bool[] Connected = BuildConnected();

    // Bit n of element c is set while the key at column c, row n is down.
    private readonly byte[] _columns = new byte[Columns];

    /// <summary>Presses a key. A key is a state: pressing it again changes nothing.</summary>
    public void Down(ElectronKey key) => _columns[Column(key)] |= Row(key);

    /// <summary>Releases a key. A key that is not down stays up.</summary>
    public void Up(ElectronKey key) => _columns[Column(key)] &= (byte)~Row(key);

    public bool IsDown(ElectronKey key) => (_columns[Column(key)] & Row(key)) != 0;

    /// <summary>
    /// The value read at an address in $8000-$BFFF with slot 8 or 9 paged in (s7a): the OR of the
    /// four-bit state of every column whose address line is low.
    /// </summary>
    public byte Read(ushort address)
    {
        int selected = ~address & ColumnMask;
        int value = 0;
        for (int column = 0; selected != 0; column++, selected >>= 1)
        {
            if ((selected & 1) != 0)
            {
                value |= _columns[column];
            }
        }

        return (byte)value;
    }

    private static int Column(ElectronKey key)
    {
        Validate(key);
        return (int)key & 0x0F;
    }

    private static byte Row(ElectronKey key) => (byte)(1 << ((int)key >> 4));

    private static void Validate(ElectronKey key)
    {
        int value = (int)key;
        if (value < 0 || value >= Connected.Length || !Connected[value])
        {
            throw new ArgumentOutOfRangeException(nameof(key), key, "Not a position in the keyboard matrix.");
        }
    }

    private static bool[] BuildConnected()
    {
        var connected = new bool[0x40];
        foreach (ElectronKey key in Enum.GetValues<ElectronKey>())
        {
            connected[(int)key] = true;
        }

        return connected;
    }
}
