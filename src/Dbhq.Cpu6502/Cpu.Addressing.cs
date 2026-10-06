using System.Runtime.CompilerServices;

namespace Dbhq.Cpu6502;

public partial class Cpu<TBus>
{
    /// <summary>
    /// What an indexed instruction does with its address, which decides
    /// whether it makes a throwaway read before the real access.
    /// </summary>
    private enum Access
    {
        /// <summary>A read: the throwaway read happens only on a page cross.</summary>
        Read,

        /// <summary>A write: always a throwaway read first.</summary>
        Write,

        /// <summary>A read-modify-write: always on NMOS, only on a page cross on the 65C02.</summary>
        Modify,

        /// <summary>INC and DEC on the 65C02, which always take the extra cycle.</summary>
        ModifyAlways,
    }

    private ushort StackAddress => (ushort)(0x0100 | S);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort FetchWord()
    {
        byte lo = Read(PC++);
        byte hi = Read(PC++);
        return (ushort)(lo | hi << 8);
    }

    private ushort ReadVector(ushort address)
    {
        byte lo = Read(address);
        byte hi = Read((ushort)(address + 1));
        return (ushort)(lo | hi << 8);
    }

    /// <summary>Reads a pointer from zero page. The high byte wraps within zero page.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort PointerAt(byte zeroPage)
    {
        byte lo = Read(zeroPage);
        byte hi = Read((byte)(zeroPage + 1));
        return (ushort)(lo | hi << 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort Zp() => Read(PC++);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort ZpX()
    {
        byte zeroPage = Read(PC++);
        Read(zeroPage);
        return (byte)(zeroPage + X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort ZpY()
    {
        byte zeroPage = Read(PC++);
        Read(zeroPage);
        return (byte)(zeroPage + Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort Abs() => FetchWord();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AbsX(Access access) => Indexed(FetchWord(), X, access);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort AbsY(Access access) => Indexed(FetchWord(), Y, access);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort IzX()
    {
        byte zeroPage = Read(PC++);
        Read(zeroPage);
        return PointerAt((byte)(zeroPage + X));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort IzY(Access access) => Indexed(PointerAt(Read(PC++)), Y, access);

    /// <summary>The 65C02's (zp) mode.</summary>
    private ushort Izp() => PointerAt(Read(PC++));

    /// <summary>
    /// Adds an index to a base address and makes the throwaway read the chip
    /// makes while it fixes the high byte.
    /// </summary>
    /// <remarks>
    /// The NMOS chip reads the half-computed address: the right low byte with
    /// the base's high byte. The 65C02 re-reads the last byte of the
    /// instruction instead, which is the byte before PC.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort Indexed(ushort baseAddress, byte index, Access access)
    {
        ushort address = (ushort)(baseAddress + index);
        bool crossed = ((baseAddress ^ address) & 0xFF00) != 0;
        bool extraCycle = access switch
        {
            Access.Read => crossed,
            Access.Modify => crossed || !_cmos,
            _ => true,
        };
        if (extraCycle)
        {
            Read(_cmos ? (ushort)(PC - 1) : (ushort)((baseAddress & 0xFF00) | (address & 0x00FF)));
        }

        return address;
    }

    /// <summary>
    /// The first two cycles of a read-modify-write. The NMOS chip writes the
    /// old value back before the new one; the 65C02 reads it a second time.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadForModify(ushort address)
    {
        byte value = Read(address);
        if (_cmos)
        {
            Read(address);
        }
        else
        {
            Write(address, value);
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Push(byte value)
    {
        Write(StackAddress, value);
        S--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Pull()
    {
        S++;
        return Read(StackAddress);
    }
}
