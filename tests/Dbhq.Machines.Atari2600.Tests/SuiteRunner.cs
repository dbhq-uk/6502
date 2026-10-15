using System.Globalization;
using Dbhq.Cpu6502.TestSupport;

namespace Dbhq.Machines.Atari2600.Tests;

/// <summary>
/// What a self-test of the suite left in RAM (<c>docs/atari-2600/facts/suite.md</c> section 2):
/// RESULT at <c>$80</c> (<c>$A5</c> pass, <c>$5A</c> fail, anything else no verdict yet), and
/// CODE, OBSERVED and EXPECTED at <c>$81</c> to <c>$83</c>, read as hexadecimal.
/// </summary>
internal readonly record struct SuiteOutcome(string Test, string Build, byte Result, byte Code, byte Observed, byte Expected, long Instructions)
{
    public const byte PassMagic = 0xA5;
    public const byte FailMagic = 0x5A;

    public bool Passed => Result == PassMagic;

    public bool Failed => Result == FailMagic;

    public override string ToString()
    {
        if (Passed)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Test} ({Build}): pass, OBSERVED ${Observed:X2}, after {Instructions} instructions");
        }

        return Failed
            ? string.Create(CultureInfo.InvariantCulture, $"{Test} ({Build}): fail, CODE ${Code:X2}, OBSERVED ${Observed:X2}, EXPECTED ${Expected:X2}, after {Instructions} instructions")
            : string.Create(CultureInfo.InvariantCulture, $"{Test} ({Build}): no verdict after {Instructions} instructions, $80 holds ${Result:X2}");
    }
}

/// <summary>
/// Runs one of the suite's self-tests headless on the machine, through the real bus, until the
/// byte at <c>$80</c> is a verdict or the limit fixed before running is reached (the plan's
/// "Fixed before running", item 1: 5,000,000 instructions from power on, counted as calls to
/// <c>Cpu.Step</c>, the suite harness's own limit). The <c>_ntsc</c> build runs on NTSC and the
/// <c>_pal</c> build on PAL; SECAM is not run.
/// </summary>
internal static class SuiteRunner
{
    /// <summary>The plan's item 1.</summary>
    public const long InstructionLimit = 5_000_000;

    private const int Result = 0x00;
    private const int Code = 0x01;
    private const int Observed = 0x02;
    private const int Expected = 0x03;

    /// <summary>The build for <paramref name="region"/>: "ntsc" or "pal".</summary>
    public static string Build(Region region) => region == Region.Pal ? "pal" : "ntsc";

    /// <summary>The test's binary for <paramref name="region"/>, from the pinned fork, hash-checked.</summary>
    public static byte[] Rom(string test, Region region) => Atari2600Suite.Read($"{test}_{Build(region)}.a26");

    /// <summary>Powers on with the test's binary for <paramref name="region"/> and runs it to a verdict or the limit.</summary>
    public static SuiteOutcome Run(string test, Region region)
    {
        var machine = new Atari2600(Cartridge.Load(Rom(test, region)), region);
        machine.PowerOn();
        Riot6532 riot = machine.Bus.Riot;
        long instructions = 0;
        while (instructions < InstructionLimit)
        {
            machine.Step();
            instructions++;
            byte result = riot.ReadRam(Result);
            if (result is SuiteOutcome.PassMagic or SuiteOutcome.FailMagic)
            {
                break;
            }
        }

        return new SuiteOutcome(test, Build(region), riot.ReadRam(Result), riot.ReadRam(Code), riot.ReadRam(Observed), riot.ReadRam(Expected), instructions);
    }
}
