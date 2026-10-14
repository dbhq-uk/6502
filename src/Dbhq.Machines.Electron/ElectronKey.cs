namespace Dbhq.Machines.Electron;

/// <summary>
/// The keys in the Electron's keyboard matrix: 14 columns of 4 bits is 56 positions, less the two
/// the schematic leaves not connected (column 0 bit 2 and column 2 bit 3), which is 54 keys (fact
/// sheet <c>ula.md</c> section 7b). BREAK is not one of them: it is a switch on the reset line,
/// not a position in the matrix (s7c).
/// </summary>
/// <remarks>
/// Each value is <c>column | (bit &lt;&lt; 4)</c>: the column is the address line that selects it,
/// A0 to A13 as 0 to 13, and the bit is the data line it pulls, D0 to D3 as 0 to 3. The two Shift
/// keys are one position, so there is one <see cref="Shift"/>. Caps Lock and Func are one key,
/// <see cref="CapsLock"/>: pressed alone it toggles the lock and pressed with another key it
/// makes a function key (s7b).
/// </remarks>
public enum ElectronKey
{
    // Column 0
    Right = 0x00, Copy = 0x10, Space = 0x30,

    // Column 1
    Left = 0x01, Down = 0x11, Return = 0x21, Delete = 0x31,

    // Column 2
    Minus = 0x02, Up = 0x12, Colon = 0x22,

    // Columns 3 to 12
    D0 = 0x03, P = 0x13, Semicolon = 0x23, Slash = 0x33,
    D9 = 0x04, O = 0x14, L = 0x24, FullStop = 0x34,
    D8 = 0x05, I = 0x15, K = 0x25, Comma = 0x35,
    D7 = 0x06, U = 0x16, J = 0x26, M = 0x36,
    D6 = 0x07, Y = 0x17, H = 0x27, N = 0x37,
    D5 = 0x08, T = 0x18, G = 0x28, B = 0x38,
    D4 = 0x09, R = 0x19, F = 0x29, V = 0x39,
    D3 = 0x0A, E = 0x1A, D = 0x2A, C = 0x3A,
    D2 = 0x0B, W = 0x1B, S = 0x2B, X = 0x3B,
    D1 = 0x0C, Q = 0x1C, A = 0x2C, Z = 0x3C,

    // Column 13, the one the OS reads alone at $9FFF
    Escape = 0x0D, CapsLock = 0x1D, Ctrl = 0x2D, Shift = 0x3D,
}
