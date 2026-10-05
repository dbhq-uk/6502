using System.Collections.ObjectModel;

namespace Dbhq.Machines.Nes;

/// <summary>
/// Every number that differs between the NTSC and the PAL console, so that no chip tests for a
/// region by name: the bus, the PPU and the sound unit are given a <see cref="Region"/> and read
/// what they need from it.
/// </summary>
/// <remarks>
/// <para>
/// Built from <c>docs/nes/facts/timing.md</c> sections 1 to 3 and <c>apu.md</c> sections 7, 8, 10
/// and 12. Dendy, which runs the PAL frame at three dots a cycle, is not a region here: it is
/// refused where a file names it (<see cref="Cartridge"/>), and the sheet puts it out of scope.
/// </para>
/// <para>
/// The frame rate is derived, never typed (AGENTS.md rule 5): the CPU clock times the dot ratio,
/// over the dots in a frame.
/// </para>
/// </remarks>
public sealed class Region
{
    /// <summary>Dots in one line, the same on both (timing.md 2).</summary>
    public const int DotsPerLine = 341;

    private Region(
        string name,
        int lines,
        bool oddFrameSkipsADot,
        int dotsNumerator,
        int dotsDenominator,
        double cpuHz,
        int[] noisePeriods,
        int[] dmcRates,
        int[] frameCounterFourStep,
        int[] frameCounterFiveStep,
        bool emphasisSwapsRedAndGreen)
    {
        Name = name;
        Lines = lines;
        PreRenderLine = lines - 1;
        OddFrameSkipsADot = oddFrameSkipsADot;
        DotsNumerator = dotsNumerator;
        DotsDenominator = dotsDenominator;
        CpuHz = cpuHz;
        NoisePeriods = Array.AsReadOnly(noisePeriods);
        DmcRates = Array.AsReadOnly(dmcRates);
        FrameCounterFourStep = Array.AsReadOnly(frameCounterFourStep);
        FrameCounterFiveStep = Array.AsReadOnly(frameCounterFiveStep);
        EmphasisSwapsRedAndGreen = emphasisSwapsRedAndGreen;
    }

    /// <summary>
    /// The NTSC console (2A03 and 2C02): master clock 236.25 MHz / 11, the CPU at master / 12, the
    /// PPU at master / 4, so exactly three dots a CPU cycle (timing.md 1).
    /// </summary>
    public static Region Ntsc { get; } = new(
        name: "NTSC",
        lines: 262,
        oddFrameSkipsADot: true,
        dotsNumerator: 3,
        dotsDenominator: 1,
        cpuHz: 236_250_000.0 / 11 / 12,
        noisePeriods: [4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068],
        dmcRates: [428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54],
        frameCounterFourStep: [7457, 14913, 22371, 29828, 29829, 29830],
        frameCounterFiveStep: [7457, 14913, 22371, 29829, 37281, 37282],
        emphasisSwapsRedAndGreen: false);

    /// <summary>
    /// The PAL console (2A07 and 2C07): master clock 26.6017125 MHz, the CPU at master / 16, the PPU
    /// at master / 5, so 3.2 dots a CPU cycle, which is 16 dots in every 5 cycles (timing.md 1 and 3).
    /// </summary>
    public static Region Pal { get; } = new(
        name: "PAL",
        lines: 312,
        oddFrameSkipsADot: false,
        dotsNumerator: 16,
        dotsDenominator: 5,
        cpuHz: 26_601_712.5 / 16,
        noisePeriods: [4, 8, 14, 30, 60, 88, 118, 148, 188, 236, 354, 472, 708, 944, 1890, 3778],
        dmcRates: [398, 354, 316, 298, 276, 236, 210, 198, 176, 148, 132, 118, 98, 78, 66, 50],
        frameCounterFourStep: [8313, 16627, 24939, 33252, 33253, 33254],
        frameCounterFiveStep: [8313, 16627, 24939, 33253, 41565, 41566],
        emphasisSwapsRedAndGreen: true);

    /// <summary>"NTSC" or "PAL", for the page to show.</summary>
    public string Name { get; }

    /// <summary>Lines in a frame: 262 on NTSC, 312 on PAL (timing.md 2).</summary>
    public int Lines { get; }

    /// <summary>The last line of the frame, where the flags clear and the next frame's fetches start: 261 or 311.</summary>
    public int PreRenderLine { get; }

    /// <summary>
    /// True on NTSC: with rendering on, an odd frame drops one dot from the pre-render line. PAL
    /// frames are always whole (timing.md 2).
    /// </summary>
    public bool OddFrameSkipsADot { get; }

    /// <summary>
    /// The numerator of the PPU dots a CPU cycle: 3 on NTSC, 16 on PAL. In <c>n</c> cycles the PPU
    /// runs <c>n x DotsNumerator / DotsDenominator</c> dots, which the bus keeps whole with an
    /// accumulator (timing.md 3).
    /// </summary>
    public int DotsNumerator { get; }

    /// <summary>The denominator of the PPU dots a CPU cycle: 1 on NTSC, 5 on PAL.</summary>
    public int DotsDenominator { get; }

    /// <summary>The CPU clock in hertz, from the master clock and the CPU's divider (timing.md 1).</summary>
    public double CpuHz { get; }

    /// <summary>
    /// Frames a second, derived: the CPU clock times the dots a cycle, over the dots in a frame
    /// (341 times the lines, less half a dot where an odd frame drops one, which is half the frames).
    /// </summary>
    public double FramesPerSecond =>
        CpuHz * DotsNumerator / DotsDenominator / (DotsPerLine * Lines - (OddFrameSkipsADot ? 0.5 : 0.0));

    /// <summary>The noise channel's timer periods, in CPU cycles, by the index in <c>$400E</c> (apu.md 7).</summary>
    public IReadOnlyList<int> NoisePeriods { get; }

    /// <summary>The DMC's output rates, in CPU cycles, by the index in <c>$4010</c> (apu.md 8).</summary>
    public IReadOnlyList<int> DmcRates { get; }

    /// <summary>
    /// The frame counter's 4-step sequence in CPU cycles after the sequencer resets, one entry for
    /// each row of the table in apu.md 10: a quarter frame, a quarter and a half, a quarter, the IRQ
    /// flag set; a quarter and a half with the flag set; the flag set once more as the sequence
    /// wraps. The last entry is the period, 29830 or 33254 cycles.
    /// </summary>
    public IReadOnlyList<int> FrameCounterFourStep { get; }

    /// <summary>
    /// The 5-step sequence, one entry for each row of the table in apu.md 10: a quarter frame, a
    /// quarter and a half, a quarter, nothing, a quarter and a half; the last entry is where the
    /// sequence wraps. The frame IRQ flag is never set in this mode.
    /// </summary>
    public IReadOnlyList<int> FrameCounterFiveStep { get; }

    /// <summary>
    /// True on PAL: the 2C07 swaps the red and green emphasis bits of <c>$2001</c> (ppu.md 10), so
    /// bit 5 emphasises green and bit 6 red.
    /// </summary>
    public bool EmphasisSwapsRedAndGreen { get; }
}
