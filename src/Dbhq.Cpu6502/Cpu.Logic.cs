using System.Runtime.CompilerServices;

namespace Dbhq.Cpu6502;

public sealed partial class Cpu
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Asl(byte value)
    {
        SetFlag(C, (value & 0x80) != 0);
        return NZ((byte)(value << 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Lsr(byte value)
    {
        SetFlag(C, (value & 0x01) != 0);
        return NZ((byte)(value >> 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Rol(byte value)
    {
        int carryIn = P & C;
        SetFlag(C, (value & 0x80) != 0);
        return NZ((byte)((value << 1) | carryIn));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Ror(byte value)
    {
        int carryIn = P & C;
        SetFlag(C, (value & 0x01) != 0);
        return NZ((byte)((value >> 1) | (carryIn << 7)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void And(byte value) => A = NZ((byte)(A & value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Ora(byte value) => A = NZ((byte)(A | value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Eor(byte value) => A = NZ((byte)(A ^ value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Compare(byte register, byte value)
    {
        SetFlag(C, register >= value);
        NZ((byte)(register - value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Bit(byte value)
    {
        SetFlag(Z, (A & value) == 0);
        SetFlag(N, (value & 0x80) != 0);
        SetFlag(V, (value & 0x40) != 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AslAt(ushort address) => Write(address, Asl(ReadForModify(address)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LsrAt(ushort address) => Write(address, Lsr(ReadForModify(address)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RolAt(ushort address) => Write(address, Rol(ReadForModify(address)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RorAt(ushort address) => Write(address, Ror(ReadForModify(address)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncAt(ushort address) => Write(address, NZ((byte)(ReadForModify(address) + 1)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecAt(ushort address) => Write(address, NZ((byte)(ReadForModify(address) - 1)));
}
