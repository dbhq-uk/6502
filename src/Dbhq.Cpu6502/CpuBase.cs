namespace Dbhq.Cpu6502;

/// <summary>
/// What every CPU of the family shows the rest of a machine: its registers, its
/// interrupt lines, and the calls that run it. A bus holds one of these to set the
/// lines; a test or a tool reads the registers through it.
/// </summary>
/// <remarks>
/// The CPU itself is <see cref="Cpu{TBus}"/>, made for one type of bus, or
/// <see cref="Cpu"/>, which takes any <see cref="IBus"/>. They do the same thing
/// cycle for cycle; the first is faster, because the compiler sees the bus's own
/// code (task 17).
/// </remarks>
public abstract class CpuBase
{
    private protected CpuBase(CpuVariant variant)
    {
        Variant = variant;
    }

    public CpuVariant Variant { get; }

    public byte A { get; set; }

    public byte X { get; set; }

    public byte Y { get; set; }

    public byte S { get; set; }

    /// <summary>
    /// The status register. Bits 4 and 5 hold whatever was last loaded into
    /// them; PLP and RTI clear bit 4 and set bit 5, as the chip does.
    /// </summary>
    public byte P { get; set; }

    public ushort PC { get; set; }

    /// <summary>Bus accesses made so far. One per cycle.</summary>
    public long Cycles { get; set; }

    /// <summary>True while the IRQ line is held active.</summary>
    public bool Irq { get; set; }

    /// <summary>True while the NMI line is held active. An NMI fires on the change to active.</summary>
    public bool Nmi { get; set; }

    /// <summary>An NMOS JAM opcode has locked the CPU until a reset.</summary>
    public bool IsJammed { get; private protected set; }

    /// <summary>A WDC WAI is waiting for an interrupt.</summary>
    public bool IsWaiting { get; private protected set; }

    /// <summary>A WDC STP has stopped the clock until a reset.</summary>
    public bool IsStopped { get; private protected set; }
}
