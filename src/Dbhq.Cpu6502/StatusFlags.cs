namespace Dbhq.Cpu6502;

/// <summary>The bits of the status register, P.</summary>
[Flags]
public enum StatusFlags : byte
{
    Carry = 0x01,
    Zero = 0x02,
    InterruptDisable = 0x04,
    Decimal = 0x08,

    /// <summary>Not a flag the chip stores: set in the copy of P that BRK and PHP push.</summary>
    Break = 0x10,

    /// <summary>Not a flag the chip stores: always set in the copy of P that is pushed.</summary>
    Unused = 0x20,
    Overflow = 0x40,
    Negative = 0x80,
}
