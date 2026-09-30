using System.Diagnostics;
using System.Globalization;

namespace Dbhq.Cpu6502.Bench;

/// <summary>
/// The fixed 6502 program the speed check runs, and the loop that runs it.
/// Shared, as one source file, by the browser app and the native console app,
/// so both measure exactly the same bytes on exactly the same core.
/// </summary>
/// <remarks>
/// This is a synthetic program of our own, written for this measurement. It is
/// not a real machine's software and it is not a third-party test program.
/// </remarks>
public static class Workload
{
    /// <summary>Where the program starts and where its loop begins.</summary>
    private const ushort Start = 0x1FE0;
    private const ushort Loop = 0x1FE2;

    /// <summary>
    /// The program, hand-assembled and placed at $1FE0. It straddles the page
    /// boundary at $2000 on purpose, so the branch that closes the loop crosses
    /// a page. Zero page holds four counters, at $10 to $13.
    /// </summary>
    private static readonly byte[] Program =
    [
        // 1FE0  LDX #$00      X is the index for the indexed accesses; it wraps every 256 laps.
        0xA2, 0x00,
        // loop:
        // 1FE2  LDA $10       a zero-page load...
        0xA5, 0x10,
        // 1FE4  CLC
        0x18,
        // 1FE5  ADC #$03      ...binary arithmetic on it...
        0x69, 0x03,
        // 1FE7  STA $10       ...and a zero-page store.
        0x85, 0x10,
        // 1FE9  STA $0400     an absolute store...
        0x8D, 0x00, 0x04,
        // 1FEC  LDA $0400     ...and an absolute load of the same byte.
        0xAD, 0x00, 0x04,
        // 1FEF  EOR $11       logic on a second counter.
        0x45, 0x11,
        // 1FF1  STA $11
        0x85, 0x11,
        // 1FF3  LDA $03F0,X   an indexed load. From $03F0 it crosses into page $04 once X is $10
        //                     or more: 5 cycles on those laps (15 in 16), 4 on the rest.
        0xBD, 0xF0, 0x03,
        // 1FF6  STA $0500,X   an indexed store, which always takes its extra cycle.
        0x9D, 0x00, 0x05,
        // 1FF9  JSR $2015     a subroutine call...
        0x20, 0x15, 0x20,
        // 1FFC  SED           decimal mode on...
        0xF8,
        // 1FFD  CLC
        0x18,
        // 1FFE  LDA $12
        0xA5, 0x12,
        // 2000  ADC #$27      ...a decimal-mode add, the slowest arithmetic path in the core...
        0x69, 0x27,
        // 2002  STA $12
        0x85, 0x12,
        // 2004  CLD           ...and decimal mode off again.
        0xD8,
        // 2005  LDY #$00      sets Z, so the next branch is not taken and the one after is.
        0xA0, 0x00,
        // 2007  BNE $2012     a branch that is never taken (2 cycles).
        0xD0, 0x09,
        // 2009  BEQ $200C     a taken branch that stays on its page (3 cycles).
        0xF0, 0x01,
        // 200B  NOP           skipped by the branch above.
        0xEA,
        // 200C  INX
        0xE8,
        // 200D  BNE $1FE2     the loop-closing branch: taken 255 laps in 256, backwards across
        //                     a page boundary (4 cycles); not taken when X wraps to 0.
        0xD0, 0xD3,
        // 200F  JMP $1FE2     reached only when X wraps: the other way round the loop.
        0x4C, 0xE2, 0x1F,
        // 2012  JMP $1FE2     the target of the never-taken branch at $2007.
        0x4C, 0xE2, 0x1F,
        // 2015  INC $13       the subroutine: a read-modify-write on zero page...
        0xE6, 0x13,
        // 2017  LDA $13       ...a load...
        0xA5, 0x13,
        // 2019  RTS           ...and the return.
        0x60,
    ];

    /// <summary>The bytes of the program, for the check that they read back as intended.</summary>
    public static ReadOnlySpan<byte> Bytes => Program;

    public static ushort ProgramStart => Start;

    /// <summary>A plain 64 KB of RAM. No logging, no other devices.</summary>
    private sealed class Ram : IBus
    {
        public readonly byte[] Memory = new byte[0x10000];

        public byte Read(ushort address) => Memory[address];

        public void Write(ushort address, byte value) => Memory[address] = value;
    }

    /// <summary>What one run reports.</summary>
    public readonly record struct Result(long Cycles, double Milliseconds)
    {
        public double CyclesPerSecond => Cycles / (Milliseconds / 1000.0);

        /// <summary>
        /// The one line every build prints, in the invariant culture so that
        /// the figures read the same wherever the browser thinks it is.
        /// </summary>
        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture,
                $"cycles={Cycles} ms={Milliseconds:F3} cycles_per_second={CyclesPerSecond:F0} mhz={CyclesPerSecond / 1e6:F3}");
    }

    /// <summary>
    /// Runs the program on a fresh NMOS 6502 for at least this many cycles,
    /// one instruction at a time, and times it. It can overshoot by the length
    /// of one instruction, so the cycles actually run are reported.
    /// </summary>
    public static Result Run(long cycles)
    {
        var bus = new Ram();
        Program.CopyTo(bus.Memory, Start);
        var cpu = new Cpu(bus, CpuVariant.Nmos6502) { PC = Start, S = 0xFF, P = 0x24 };

        long begin = Stopwatch.GetTimestamp();
        while (cpu.Cycles < cycles)
        {
            cpu.Step();
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(begin);
        return new Result(cpu.Cycles, elapsed.TotalMilliseconds);
    }
}
