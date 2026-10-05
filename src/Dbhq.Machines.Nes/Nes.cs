using Dbhq.Cpu6502;

namespace Dbhq.Machines.Nes;

/// <summary>
/// The NES: a Ricoh 2A03 or 2A07 (the NMOS 6502 without decimal mode) on a bus that owns the
/// chips and counts the cycles, in either region.
/// </summary>
/// <remarks>
/// The machine starts with the power off: call <see cref="PowerOn"/> before the first
/// <see cref="Step"/>. Both <see cref="Cartridge.Load"/> and the board's creation, which the
/// constructor does, can throw <see cref="NesFormatException"/>, and a caller that shows a visitor's
/// file must catch it from both.
/// </remarks>
public sealed class Nes
{
    /// <summary>
    /// A machine with <paramref name="cartridge"/> in it. The region is the one given, or else the
    /// header's, or else NTSC.
    /// </summary>
    /// <exception cref="NesFormatException">The cartridge needs a mapper this machine does not model.</exception>
    public Nes(Cartridge cartridge, Region? region = null, NesOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(cartridge);
        Region = region ?? cartridge.HeaderRegion ?? Region.Ntsc;
        Bus = new NesBus(cartridge, Region, options);
        Cpu = new Cpu(Bus, CpuVariant.Ricoh2A03);
        Bus.Cpu = Cpu;
    }

    public NesBus Bus { get; }

    public Cpu Cpu { get; }

    public Region Region { get; }

    /// <summary>
    /// Switches on: the chips and RAM are cleared, the CPU's registers start at zero, and its reset
    /// sequence runs, seven cycles, ending at the reset vector with S at $FD and P at $24. The bus
    /// has run those cycles, so the dot counter reads 21 on NTSC when it is done.
    /// </summary>
    public void PowerOn()
    {
        Bus.PowerOn();
        Cpu.A = 0;
        Cpu.X = 0;
        Cpu.Y = 0;
        Cpu.S = 0;
        Cpu.P = 0x24;
        Cpu.PC = 0;
        Cpu.Cycles = 0;
        Cpu.Reset();
    }

    /// <summary>The reset button: the CPU resets, and RAM and PRG RAM are kept.</summary>
    public void Reset()
    {
        Bus.Reset();
        Cpu.Reset();
    }

    /// <summary>Runs one instruction, or one interrupt sequence, and returns the CPU cycles it took.</summary>
    public int Step()
    {
        long start = Bus.Cycles;
        Cpu.Step();
        return (int)(Bus.Cycles - start);
    }

    /// <summary>Runs whole instructions until at least <paramref name="cpuCycles"/> more cycles have passed.</summary>
    public void Run(long cpuCycles)
    {
        long end = Bus.Cycles + cpuCycles;
        while (Bus.Cycles < end)
        {
            Step();
        }
    }
}
