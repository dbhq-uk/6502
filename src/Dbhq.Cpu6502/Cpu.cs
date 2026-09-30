namespace Dbhq.Cpu6502;

/// <summary>
/// A 6502-family CPU in which every bus access is one cycle.
/// </summary>
/// <remarks>
/// The CPU has no memory of its own. Every byte goes through <see cref="IBus"/>,
/// and each read or write is one cycle, so an instruction's cycles can be
/// counted by counting its bus accesses. The code for each instruction does its
/// throwaway reads and writes explicitly, because on real hardware they reach
/// the bus and a hardware register can react to them.
/// </remarks>
public sealed partial class Cpu
{
    private readonly IBus _bus;
    private readonly bool _cmos;

    public Cpu(IBus bus, CpuVariant variant)
    {
        _bus = bus;
        Variant = variant;
        _cmos = variant is CpuVariant.Synertek65C02 or CpuVariant.Rockwell65C02 or CpuVariant.Wdc65C02;
        P = I | U;
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

    /// <summary>An NMOS JAM opcode has locked the CPU until <see cref="Reset"/>.</summary>
    public bool IsJammed { get; private set; }

    /// <summary>A WDC WAI is waiting for an interrupt.</summary>
    public bool IsWaiting { get; private set; }

    /// <summary>A WDC STP has stopped the clock until <see cref="Reset"/>.</summary>
    public bool IsStopped { get; private set; }

    /// <summary>
    /// Runs one instruction and returns the number of cycles it took.
    /// </summary>
    public int Step()
    {
        long start = Cycles;
        Execute(Read(PC++));
        return (int)(Cycles - start);
    }

    /// <summary>
    /// The reset sequence: seven cycles, three of them stack reads that the
    /// chip makes in place of pushes, then the vector at $FFFC.
    /// </summary>
    public void Reset()
    {
        Read(PC);
        Read(PC);
        Read(StackAddress);
        S--;
        Read(StackAddress);
        S--;
        Read(StackAddress);
        S--;
        SetFlag(I, true);
        if (_cmos)
        {
            SetFlag(D, false);
        }

        PC = ReadVector(0xFFFC);
    }

    private byte Read(ushort address)
    {
        byte value = _bus.Read(address);
        EndCycle();
        return value;
    }

    private void Write(ushort address, byte value)
    {
        _bus.Write(address, value);
        EndCycle();
    }

    /// <summary>The end of every cycle. Task 8 adds the interrupt lines here.</summary>
    private void EndCycle() => Cycles++;

    private void Execute(byte opcode)
    {
        if (!ExecuteOfficial(opcode))
        {
            if (_cmos)
            {
                ExecuteCmos(opcode);
            }
            else
            {
                ExecuteNmos(opcode);
            }
        }
    }
}
