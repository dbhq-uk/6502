using Dbhq.Cpu6502;

namespace Dbhq.Machines.Atari2600;

/// <summary>
/// The Atari 2600: a 6507, run as the core's NMOS 6502 unchanged, on a bus that owns the TIA, the
/// RIOT and the cartridge's board and counts the cycles, in either region.
/// </summary>
/// <remarks>
/// The machine starts with the power off: call <see cref="PowerOn"/> before the first
/// <see cref="Step"/>. The console has no reset line to the CPU other than power on; its Reset
/// switch is read by the program through the RIOT (<see cref="SetSwitches"/>).
/// </remarks>
public sealed class Atari2600
{
    /// <summary>A machine with <paramref name="cartridge"/> in it, in <paramref name="region"/> or else NTSC.</summary>
    public Atari2600(Cartridge cartridge, Region? region = null, Atari2600Options? options = null)
    {
        ArgumentNullException.ThrowIfNull(cartridge);
        Bus = new Atari2600Bus(cartridge, region ?? Region.Ntsc, options);
        Cpu = new Cpu(Bus, CpuVariant.Nmos6502);
        _controllers = new Controllers(Bus.Riot, Bus.Tia);
    }

    private readonly Controllers _controllers;

    public Atari2600Bus Bus { get; }

    public Cpu Cpu { get; }

    /// <summary>
    /// Switches on: the chips go to their power-on state, the CPU's registers start at zero, and its
    /// reset sequence runs, seven cycles through the bus, ending at the reset vector at
    /// <c>$1FFC</c> with S at <c>$FD</c>.
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

    /// <summary>The console switches as last set (<see cref="SetSwitches"/>).</summary>
    public ConsoleSwitches Switches => _controllers.Switches;

    /// <summary>
    /// Holds the joystick in port <paramref name="stick"/>, 0 the left and 1 the right, as
    /// <paramref name="state"/> says until the next call: its directions on the RIOT's port A and
    /// its button on the TIA's INPT4 or INPT5, active low. Any combination is taken as written,
    /// opposite directions together included. It holds through <see cref="PowerOn"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="stick"/> is not 0 or 1, or <paramref name="state"/> has a bit that names no input.
    /// </exception>
    public void SetJoystick(int stick, Joystick state) => _controllers.SetStick(stick, state);

    /// <summary>The joystick in port <paramref name="stick"/> as last set (<see cref="SetJoystick"/>).</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stick"/> is not 0 or 1.</exception>
    public Joystick JoystickState(int stick) => _controllers.Stick(stick);

    /// <summary>
    /// Sets the console switches on the RIOT's port B until the next call: Reset and Select are
    /// pressed while their flag is set, and the colour and difficulty switches stay where this puts
    /// them. They hold through <see cref="PowerOn"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> has a bit that names no switch.</exception>
    public void SetSwitches(ConsoleSwitches state) => _controllers.SetSwitches(state);

    /// <summary>Runs one instruction and returns the CPU cycles it took, a WSYNC halt before it included.</summary>
    public int Step()
    {
        long start = Bus.Cycles;
        Cpu.Step();
        return (int)(Bus.Cycles - start);
    }

    /// <summary>Runs whole instructions until at least <paramref name="cycles"/> more cycles have passed.</summary>
    public void Run(long cycles)
    {
        long end = Bus.Cycles + cycles;
        while (Bus.Cycles < end)
        {
            Step();
        }
    }

    /// <summary>
    /// Runs whole instructions until <paramref name="frames"/> more frames have ended, and stops at
    /// the instruction in which the last ends.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="frames"/> is negative.</exception>
    public void RunFrames(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        long end = Bus.Tia.Frame.Frames + frames;
        while (Bus.Tia.Frame.Frames < end)
        {
            Step();
        }
    }
}
