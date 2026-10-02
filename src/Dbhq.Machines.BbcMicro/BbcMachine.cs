using Dbhq.Cpu6502;

namespace Dbhq.Machines.BbcMicro;

/// <summary>
/// Acorn's BBC Micro Model B: an NMOS 6502 at 2 MHz, 32 KB of RAM, MOS 1.20, BBC BASIC in
/// paged ROM slot 15 and the DFS in slot 14.
/// </summary>
/// <remarks>
/// The bus owns every chip and counts the cycles; the machine joins it to the CPU and gives
/// the two kinds of reset. The machine starts with the power off: call <see cref="PowerOn"/>
/// before the first <see cref="Step"/>. RAM starts as zeros.
/// </remarks>
public sealed class BbcMachine
{
    public BbcMachine(BbcRoms roms, BbcOptions? options = null)
    {
        Bus = new BbcBus(roms, options);
        Cpu = new Cpu(Bus, CpuVariant.Nmos6502);
        Bus.Cpu = Cpu;
    }

    public BbcBus Bus { get; }

    public Cpu Cpu { get; }

    /// <summary>The keyboard, with its start-up links set from the options.</summary>
    public BbcKeyboard Keyboard => Bus.Keyboard;

    /// <summary>2 MHz CPU cycles since power on, stretch cycles included.</summary>
    public long Cycles => Bus.Cycles;

    /// <summary>
    /// Switches on: the power-on reset clears both VIAs, then the 6502 runs its reset sequence
    /// and starts at the vector in the OS ROM, <c>$D9CD</c> (fact sheet <c>bus.md</c> section 5).
    /// </summary>
    public void PowerOn()
    {
        Bus.PowerOnReset();
        Cpu.Reset();
    }

    /// <summary>
    /// The BREAK key: resets everything except the system VIA, which only the power-on circuit
    /// resets, so the OS reads its IER and knows this was not a power on (<c>bus.md</c> section 5).
    /// </summary>
    public void PressBreak()
    {
        Bus.BreakReset();
        Cpu.Reset();
    }

    /// <summary>Runs one instruction, or one interrupt sequence, and returns the CPU cycles it took.</summary>
    public int Step()
    {
        long start = Bus.Cycles;
        Cpu.Step();
        return (int)(Bus.Cycles - start);
    }

    /// <summary>Runs whole instructions until at least <paramref name="cpuCycles"/> more 2 MHz cycles have passed.</summary>
    public void Run(long cpuCycles)
    {
        long end = Bus.Cycles + cpuCycles;
        while (Bus.Cycles < end)
        {
            Step();
        }
    }
}
