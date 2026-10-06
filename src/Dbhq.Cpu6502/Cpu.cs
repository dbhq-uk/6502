using System.Runtime.CompilerServices;

namespace Dbhq.Cpu6502;

/// <summary>
/// A 6502-family CPU in which every bus access is one cycle, on any <see cref="IBus"/>.
/// </summary>
/// <remarks>
/// The same CPU as <see cref="Cpu{TBus}"/>, cycle for cycle, with the bus reached
/// through the interface. Tests and tools use it; a machine that wants speed
/// builds <see cref="Cpu{TBus}"/> on a struct of its own.
/// </remarks>
public sealed class Cpu : Cpu<AnyBus>
{
    public Cpu(IBus bus, CpuVariant variant)
        : base(new AnyBus(bus), variant)
    {
    }
}

/// <summary>
/// Any <see cref="IBus"/>, as a struct, so that <see cref="Cpu"/> can be a
/// <see cref="Cpu{TBus}"/>.
/// </summary>
public readonly struct AnyBus(IBus bus) : IBus
{
    public byte Read(ushort address) => bus.Read(address);

    public void Write(ushort address, byte value) => bus.Write(address, value);
}

/// <summary>
/// A 6502-family CPU in which every bus access is one cycle.
/// </summary>
/// <remarks>
/// The CPU has no memory of its own. Every byte goes through <see cref="IBus"/>,
/// and each read or write is one cycle, so an instruction's cycles can be
/// counted by counting its bus accesses. The code for each instruction does its
/// throwaway reads and writes explicitly, because on real hardware they reach
/// the bus and a hardware register can react to them.
/// <para>
/// The bus is a type argument, a struct, so that the compiler makes a copy of
/// this code for each machine's bus and can call, or inline, the bus's own
/// methods directly (task 17). <see cref="Cpu"/> is this class on any
/// <see cref="IBus"/>.
/// </para>
/// </remarks>
/// <typeparam name="TBus">The machine's bus: a struct, usually holding a reference to the bus object.</typeparam>
public partial class Cpu<TBus> : CpuBase
    where TBus : struct, IBus
{
    // Not readonly: a call on a readonly field of an unknown struct type would copy it first.
    private TBus _bus;
    private readonly bool _cmos;
    private readonly bool _decimal;
    private readonly bool _bitInstructions;
    private readonly bool _waitAndStop;

    public Cpu(TBus bus, CpuVariant variant)
        : base(variant)
    {
        _bus = bus;
        _cmos = variant is CpuVariant.Synertek65C02 or CpuVariant.Rockwell65C02 or CpuVariant.Wdc65C02;
        _decimal = variant != CpuVariant.Ricoh2A03;
        _bitInstructions = variant is CpuVariant.Rockwell65C02 or CpuVariant.Wdc65C02;
        _waitAndStop = variant == CpuVariant.Wdc65C02;
        P = I | U;
    }

    /// <summary>
    /// Runs one instruction, or one interrupt sequence, and returns the number
    /// of cycles it took. Returns 0 while the CPU is waiting or stopped and
    /// makes no bus access; the machine then advances its own clock.
    /// </summary>
    public int Step()
    {
        long start = Cycles;
        if (IsStopped)
        {
            return 0;
        }

        if (IsJammed)
        {
            // A jammed NMOS chip keeps reading $FFFF. That is a real bus
            // access, so the machine keeps its clock.
            Read(0xFFFF);
            return 1;
        }

        if (IsWaiting && !WakeFromWait())
        {
            return 0;
        }

        if (_interruptPending)
        {
            InterruptSequence();
        }
        else
        {
            Execute(Read(PC++));
        }

        Poll();
        return (int)(Cycles - start);
    }

    /// <summary>
    /// The reset sequence: seven cycles, three of them stack reads that the
    /// chip makes in place of pushes, then the vector at $FFFC.
    /// </summary>
    public void Reset()
    {
        IsJammed = false;
        IsWaiting = false;
        IsStopped = false;
        _needNmi = false;
        _pollIrq = false;
        _pollNmi = false;
        _pollFrozen = false;
        _pollSuppressed = false;
        _interruptPending = false;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte Read(ushort address)
    {
        byte value = _bus.Read(address);
        EndCycle();
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Write(ushort address, byte value)
    {
        _bus.Write(address, value);
        EndCycle();
    }

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
