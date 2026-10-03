namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// The 72 keys in the Model B's keyboard matrix. BREAK is not one of them: it is a switch on
/// the reset line, not a cell in the matrix (fact sheet <c>via.md</c> section 3(d)).
/// </summary>
/// <remarks>
/// Each value is the OS's internal key number, <c>column + 16 * row</c>, where the column is
/// PA0 to PA3 (0 to 9) and the row PA4 to PA6 (0 to 7), as the OS and the Advanced User Guide
/// name them; the Service Manual swaps the two names (section 3(a)). The positions are the
/// sheet's table in section 3(b). The two SHIFT keys share one cell, so there is one
/// <see cref="Shift"/>. The eight start-up links are row 0, columns 2 to 9, and are not keys:
/// <see cref="BbcKeyboard"/> sets them from the start-up mode.
/// </remarks>
public enum BbcKey
{
    Shift = 0x00, Ctrl = 0x01,
    Q = 0x10, D3 = 0x11, D4 = 0x12, D5 = 0x13, F4 = 0x14, D8 = 0x15, F7 = 0x16, Minus = 0x17, Caret = 0x18, Left = 0x19,
    F0 = 0x20, W = 0x21, E = 0x22, T = 0x23, D7 = 0x24, I = 0x25, D9 = 0x26, D0 = 0x27, Underscore = 0x28, Down = 0x29,
    D1 = 0x30, D2 = 0x31, D = 0x32, R = 0x33, D6 = 0x34, U = 0x35, O = 0x36, P = 0x37, LeftBracket = 0x38, Up = 0x39,
    CapsLock = 0x40, A = 0x41, X = 0x42, F = 0x43, Y = 0x44, J = 0x45, K = 0x46, At = 0x47, Colon = 0x48, Return = 0x49,
    ShiftLock = 0x50, S = 0x51, C = 0x52, G = 0x53, H = 0x54, N = 0x55, L = 0x56, Semicolon = 0x57, RightBracket = 0x58, Delete = 0x59,
    Tab = 0x60, Z = 0x61, Space = 0x62, V = 0x63, B = 0x64, M = 0x65, Comma = 0x66, FullStop = 0x67, Slash = 0x68, Copy = 0x69,
    Escape = 0x70, F1 = 0x71, F2 = 0x72, F3 = 0x73, F5 = 0x74, F6 = 0x75, F8 = 0x76, F9 = 0x77, Backslash = 0x78, Right = 0x79,
}
