namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    private const byte C = 0x01;
    private const byte Z = 0x02;
    private const byte I = 0x04;
    private const byte D = 0x08;
    private const byte B = 0x10;
    private const byte U = 0x20;
    private const byte V = 0x40;
    private const byte N = 0x80;

    private bool Flag(byte flag) => (P & flag) != 0;

    private void SetFlag(byte flag, bool on) => P = on ? (byte)(P | flag) : (byte)(P & ~flag);

    /// <summary>Sets N and Z from a result and returns it.</summary>
    private byte NZ(byte value)
    {
        SetFlag(Z, value == 0);
        SetFlag(N, (value & 0x80) != 0);
        return value;
    }
}
