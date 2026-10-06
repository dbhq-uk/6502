using Xunit;
using Xunit.Abstractions;

namespace Dbhq.Cpu6502.Tests.Harte;

/// <summary>
/// The opcodes the Harte runs leave out, said out loud: each variant's count and every exclusion
/// with its reason are printed, and checked, so a change to the list shows in the test output.
/// </summary>
public sealed class CoverageTests(ITestOutputHelper output)
{
    [Fact]
    public void EachVariantRunsEveryOpcodeButTheExclusionsAndEachHasItsReason()
    {
        foreach (CpuVariant variant in Enum.GetValues<CpuVariant>())
        {
            List<Coverage.Exclusion> left = Coverage.Excluded.Where(e => e.Variant == variant).ToList();
            output.WriteLine($"{variant} ({HarteSets.Folder(variant)}): {Coverage.Opcodes(variant).Count()} opcodes run, {left.Count} excluded");
            foreach (Coverage.Exclusion e in left)
            {
                output.WriteLine($"  ${e.Opcode:X2} excluded: {e.Reason}");
            }

            Assert.Equal(256 - left.Count, Coverage.Opcodes(variant).Count());
            Assert.All(left, e => Assert.False(string.IsNullOrWhiteSpace(e.Reason)));
        }

        // WAI and STP on WDC, and LXA on the Ricoh 2A03 variant. Nothing else.
        Assert.Equal(
            [(CpuVariant.Ricoh2A03, (byte)0xAB), (CpuVariant.Wdc65C02, (byte)0xCB), (CpuVariant.Wdc65C02, (byte)0xDB)],
            Coverage.Excluded.Select(e => (e.Variant, e.Opcode)).OrderBy(p => p.Variant).ThenBy(p => p.Opcode));

        // The NMOS 6502 set keeps LXA, at $EE.
        Assert.Contains((byte)0xAB, Coverage.Opcodes(CpuVariant.Nmos6502));
    }

    [Fact]
    public void TheNes6502LxaFilePassesOnTheNmosVariantSoOnlyTheConstantIsLeftOut()
    {
        // The Ricoh 2A03 variant differs from the NMOS 6502 in decimal mode, which LXA does not
        // use, and in LXA's constant. Run on the NMOS variant, whose constant is $EE as the file
        // expects, every case of the nes6502 $AB file passes: its cycles, bus accesses, flags and
        // memory are checked, and only the constant is not.
        IReadOnlyList<HarteCase> cases = HarteFile.Load(HarteSets.FileFor(CpuVariant.Ricoh2A03, 0xAB));
        Assert.NotEmpty(cases);

        List<string> failures = HarteRunner.Run(CpuVariant.Nmos6502, 0xAB, cases);
        Assert.True(failures.Count == 0, string.Join("\n", failures));

        // And on the Ricoh 2A03 variant it fails, so the exclusion is still needed.
        Assert.NotEmpty(HarteRunner.Run(CpuVariant.Ricoh2A03, 0xAB, cases));
        output.WriteLine($"nes6502 $AB: {cases.Count} cases pass on Nmos6502 ($EE) and fail on Ricoh2A03 ($FF)");
    }
}
