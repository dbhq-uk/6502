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

    /// <summary>The picture the video ULA has drawn, brought up to now when it is read.</summary>
    public Framebuffer Screen => Bus.Screen;

    /// <summary>
    /// The sound, as samples from 0 to 1 at <see cref="BbcOptions.SampleRate"/>: one sample for
    /// each 250,000 / rate chip clocks, a chip clock being eight CPU cycles, made up to now when it
    /// is read (<see cref="Sn76489"/>).
    /// </summary>
    public SoundBuffer Sound => Bus.Sound;

    /// <summary>The 8271 floppy disc controller and its two drives.</summary>
    public Fdc8271 Fdc => Bus.Fdc;

    /// <summary>
    /// Puts a disc in drive 0 or 1, or empties it with null. DFS keeps the catalogue it last read
    /// and reads it again only after its ready test fails, which happens once the head has unloaded,
    /// about 2.4 seconds after the last disc command (disc.md s2b); so a disc changed sooner shows
    /// the old catalogue, as on a real BBC.
    /// </summary>
    public void Insert(int drive, DiscImage? image) => Bus.Fdc.Insert(drive, image);

    /// <summary>Takes the disc out of drive 0 or 1 and returns it, or null if there was none.</summary>
    public DiscImage? Eject(int drive) => Bus.Fdc.Eject(drive);

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
