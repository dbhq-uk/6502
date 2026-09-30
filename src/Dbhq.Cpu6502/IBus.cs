namespace Dbhq.Cpu6502;

/// <summary>
/// Everything the CPU can reach: memory and every other chip in the machine.
/// </summary>
/// <remarks>
/// Each call is one CPU cycle. A machine implements this by advancing its
/// other chips by one cycle inside each call, so the CPU never needs a clock
/// of its own. A slow device or a DMA stall is the machine taking more than
/// one cycle inside a call before it returns.
/// </remarks>
public interface IBus
{
    byte Read(ushort address);

    void Write(ushort address, byte value);
}
