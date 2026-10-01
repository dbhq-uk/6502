using Dbhq.Cpu6502;

namespace Dbhq.Machines.Kim1;

/// <summary>
/// MOS Technology's KIM-1 (1976): an NMOS 6502 at 1 MHz, two 6530s, 1 KB of
/// RAM, a 23-key keypad and six LED digits.
/// </summary>
/// <remarks>
/// The teletype interface, the cassette interface and the expansion bus are
/// not modelled. The machine starts powered on and idle; press RS, as the
/// User Manual says to, to start the monitor. RAM starts as zeros.
/// </remarks>
public sealed class Kim1Machine
{
    /// <param name="rom002">The 1 KB ROM of the 6530-002, at $1C00-$1FFF.</param>
    /// <param name="rom003">The 1 KB ROM of the 6530-003, at $1800-$1BFF.</param>
    public Kim1Machine(ReadOnlySpan<byte> rom002, ReadOnlySpan<byte> rom003)
    {
        Bus = new Kim1Bus(rom002, rom003);
        Cpu = new Cpu(Bus, CpuVariant.Nmos6502);
        Bus.Cpu = Cpu;
    }

    public Kim1Bus Bus { get; }

    public Cpu Cpu { get; }

    public Kim1Keypad Keypad => Bus.Keypad;

    /// <summary>Bus cycles since power on: microseconds, at the KIM-1's 1 MHz.</summary>
    public long Cycles => Bus.Cycles;

    /// <summary>The SST slide switch: on stops after every instruction outside the monitor ROM.</summary>
    public bool SingleStep
    {
        get => Bus.SingleStep;
        set => Bus.SingleStep = value;
    }

    /// <summary>The ST key, held while true. It drives the CPU's NMI line.</summary>
    public bool StopKey
    {
        get => Bus.StopHeld;
        set => Bus.StopHeld = value;
    }

    /// <summary>The RS key: resets the 6530s and the CPU, which then starts at the vector in ROM.</summary>
    public void PressReset()
    {
        Bus.ResetChips();
        Cpu.Reset();
    }

    /// <summary>Runs one instruction, or one interrupt sequence, and returns its cycles.</summary>
    public int Step()
    {
        Bus.BeginInstruction();
        return Cpu.Step();
    }

    /// <summary>Runs whole instructions until at least <paramref name="cycles"/> more bus cycles have passed.</summary>
    public void Run(long cycles)
    {
        long end = Bus.Cycles + cycles;
        while (Bus.Cycles < end)
        {
            Step();
        }
    }

    /// <summary>The six digits as they look now. See <see cref="Kim1Display.Read"/>.</summary>
    public string ReadDisplay() => Bus.Display.Read(Bus.Cycles);
}
