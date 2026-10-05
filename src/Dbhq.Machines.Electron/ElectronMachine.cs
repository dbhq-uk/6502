using Dbhq.Cpu6502;

namespace Dbhq.Machines.Electron;

/// <summary>
/// Acorn's Electron: an NMOS 6502 at 2 MHz behind the ULA, 32 KB of RAM, OS 1.00 and BBC BASIC
/// in paged ROM slots 10 and 11 (fact sheet <c>ula.md</c> sections 1, 2 and 10).
/// </summary>
/// <remarks>
/// The bus owns the ULA and the keyboard and counts the cycles; the machine joins it to the CPU,
/// carries the ULA's IRQ line to the CPU after each instruction, and gives the two kinds of
/// reset. The ULA has no NMI source of its own (s6a), so the NMI line stays inactive. The machine
/// starts with the power off: call <see cref="PowerOn"/> once, before the first
/// <see cref="Step"/>. RAM starts as zeros.
/// </remarks>
public sealed class ElectronMachine
{
    public ElectronMachine(ElectronRoms roms, ElectronOptions? options = null)
    {
        options ??= new ElectronOptions();
        Bus = new ElectronBus(roms, options.SampleRate, options.BasicSlots);
        Cpu = new Cpu(Bus, CpuVariant.Nmos6502);
    }

    public ElectronBus Bus { get; }

    public Cpu Cpu { get; }

    /// <summary>The keyboard matrix in slots 8 and 9.</summary>
    public ElectronKeyboard Keyboard => Bus.Keyboard;

    /// <summary>2 MHz cycles since power on, wait cycles included.</summary>
    public long Cycles => Bus.Cycles;

    /// <summary>
    /// Switches on: the ULA raises its power-on flag, which the OS reads to tell a power on from a
    /// BREAK (s6a, s10a), then the 6502 runs its reset sequence and starts at the vector in the OS
    /// ROM, <c>$D8D2</c> (s1a). Only the flag is raised: RAM, the ULA's registers and the ROM latch
    /// are not cleared. They are in their power-on state from construction, so a machine is
    /// switched on once, and a second call is not a cold start.
    /// </summary>
    public void PowerOn()
    {
        Bus.Ula.PowerOn();
        Cpu.Reset();
        Cpu.Irq = Bus.Irq;
    }

    /// <summary>
    /// The BREAK key, which makes the ULA assert RST (s7c). Only the CPU is reset: RAM is kept, and
    /// the ULA keeps every register, because which of them BREAK clears is not known (s12 item 5)
    /// and the OS rewrites what it needs. The power-on flag is not raised, which is how the OS
    /// knows this was a BREAK.
    /// </summary>
    public void PressBreak()
    {
        Cpu.Reset();
        Cpu.Irq = Bus.Irq;
    }

    /// <summary>
    /// Runs one instruction, or one interrupt sequence, and returns the CPU cycles it took. Then
    /// the ULA's IRQ line, caught up to the cycle the instruction ended on, goes to the CPU.
    /// </summary>
    public int Step()
    {
        long start = Bus.Cycles;
        Cpu.Step();
        Cpu.Irq = Bus.Irq;
        return (int)(Bus.Cycles - start);
    }

    /// <summary>Runs whole instructions until at least <paramref name="cycles"/> more 2 MHz cycles have passed.</summary>
    public void Run(long cycles)
    {
        long end = Bus.Cycles + cycles;
        while (Bus.Cycles < end)
        {
            Step();
        }
    }
}
