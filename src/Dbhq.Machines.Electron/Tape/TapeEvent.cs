namespace Dbhq.Machines.Electron.Tape;

/// <summary>
/// What a tape is made of, as the UEF reader and writer and the cassette see it: carrier, bytes
/// and gaps, never a waveform (<c>tape.md</c> s5, the "ULA bit" boundary).
/// </summary>
public abstract record TapeEvent;

/// <summary>
/// An idle line, the high tone the OS leaves the line at for its leader and between blocks
/// (<c>tape.md</c> s7 items 1 and 2). <paramref name="Cycles"/> counts cycles of 2400 Hz, as the UEF
/// chunk <c>&amp;0110</c> does, each <see cref="TapeTiming.CarrierCycleCpuCycles"/> (832) CPU cycles.
/// </summary>
public sealed record Carrier(int Cycles) : TapeEvent;

/// <summary>One byte at 1200 baud: ten bit times, <see cref="TapeTiming.ByteCpuCycles"/> (16,640) CPU cycles.</summary>
public sealed record TapeByte(byte Value) : TapeEvent;

/// <summary>A gap of silence, <paramref name="Cycles"/> in 2 MHz CPU cycles (not cycles of the tone).</summary>
public sealed record Silence(int Cycles) : TapeEvent;

/// <summary>The tape's timing against the 2 MHz CPU clock, in one place (<c>tape.md</c> s4 and s5 "Carrier units").</summary>
public static class TapeTiming
{
    /// <summary>The CPU clock the tape's cycle counts are measured against.</summary>
    public const int CpuHz = 2_000_000;

    /// <summary>The base frequency of an Acorn tape, in hertz, and the only one this reader loads (<c>tape.md</c> s6).</summary>
    public const int BaseFrequency = 1200;

    /// <summary>CPU cycles in one cycle of the 2400 Hz tone: half a bit time. The unit of <see cref="Carrier"/>.</summary>
    public const int CarrierCycleCpuCycles = 832;

    /// <summary>CPU cycles in one bit time at 1200 baud: 832 microseconds.</summary>
    public const int BitCpuCycles = 1_664;

    /// <summary>CPU cycles in one byte of ten bits: a start bit, eight data bits and a stop bit.</summary>
    public const int ByteCpuCycles = 10 * BitCpuCycles;
}
