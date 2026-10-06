namespace Dbhq.Cpu6502;

/// <summary>
/// An <see cref="IBus"/> as an abstract class. A machine's bus that derives from it is called by
/// the CPU through a virtual call, which WebAssembly's ahead-of-time build makes cheaper than a
/// call through the interface (task 17). Any other <see cref="IBus"/> works as before.
/// </summary>
public abstract class Bus : IBus
{
    public abstract byte Read(ushort address);

    public abstract void Write(ushort address, byte value);
}
