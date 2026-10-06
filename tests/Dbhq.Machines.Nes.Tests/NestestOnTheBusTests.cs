using System.Text.RegularExpressions;
using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Machines.Nes.Tests;

/// <summary>
/// nestest in its automated mode, run through the whole machine: the real bus, the cartridge, the
/// RAM mirrors and the dot counter. The log's PPU column is the check that the counter and the
/// reset alignment are right (<c>docs/nes/facts/timing.md</c> section 4).
/// </summary>
public sealed partial class NestestOnTheBusTests
{
    private const string LogBase = "https://raw.githubusercontent.com/dbhq-uk/nes-test-roms/" + Pins.NestestCommit + "/other/";

    public static TheoryData<string> Regions() => new() { "NTSC", "PAL" };

    [Theory]
    [MemberData(nameof(Regions))]
    public void EveryLineOfTheLogMatchesThroughTheRealBus(string regionName)
    {
        AssertEveryLineMatches(regionName == "PAL" ? Region.Pal : Region.Ntsc);
    }

    /// <summary>
    /// Runs nestest from <c>$C000</c> on <paramref name="region"/> and checks every line of the
    /// log against the CPU, the bus's cycle count and the dot counter, then nestest's own error
    /// codes. <see cref="NesAcceptanceTests"/> calls this too.
    /// </summary>
    internal static void AssertEveryLineMatches(Region region)
    {
        bool pal = region == Region.Pal;
        string[] log = File.ReadAllLines(PinnedFiles.Fetch(
            LogBase + "nestest.log",
            Path.Combine("nestest", "nestest.log"),
            PinnedFiles.Sha256(Pins.NestestLogSha256)));
        var nes = new Nes(Cartridge.Load(NesTestRoms.Read("other/nestest.nes")), region);
        nes.PowerOn();

        // The ROM's automation entry, as the CPU-only test does. Reset leaves S and P as the log has them.
        Assert.Equal(0xFD, nes.Cpu.S);
        Assert.Equal(0x24, nes.Cpu.P);
        nes.Cpu.PC = 0xC000;

        for (int i = 0; i < log.Length; i++)
        {
            Match expected = LogLine().Match(log[i]);
            Assert.True(expected.Success, $"line {i + 1} of nestest.log does not parse: {log[i]}");

            Dbhq.Cpu6502.TraceLine actual = Dbhq.Cpu6502.Tracer.Capture(nes.Cpu, nes.Bus.Peek);
            string ours = $"{actual.PC:X4} {string.Join(' ', actual.Bytes.Select(b => b.ToString("X2")))} {(actual.Opcode.Undocumented ? "*" : "")}{actual.Opcode.Mnemonic} A:{actual.A:X2} X:{actual.X:X2} Y:{actual.Y:X2} P:{actual.P:X2} SP:{actual.S:X2} CYC:{actual.Cycles}";
            string theirs = $"{expected.Groups["pc"].Value} {expected.Groups["bytes"].Value.Trim()} {expected.Groups["mark"].Value.Trim()}{expected.Groups["mnemonic"].Value} A:{expected.Groups["a"].Value} X:{expected.Groups["x"].Value} Y:{expected.Groups["y"].Value} P:{expected.Groups["p"].Value} SP:{expected.Groups["s"].Value} CYC:{expected.Groups["cycles"].Value}";
            Assert.True(ours == theirs, $"line {i + 1}:\n  log:  {theirs}\n  ours: {ours}");

            // The bus keeps its own count of the cycles, and it must agree with the CPU's.
            Assert.True(actual.Cycles == nes.Bus.Cycles, $"line {i + 1}: the CPU counts {actual.Cycles} cycles and the bus {nes.Bus.Cycles}");

            long dots = nes.Bus.PpuDots;
            if (pal)
            {
                long expectedDots = actual.Cycles * 16 / 5;
                Assert.True(dots == expectedDots, $"line {i + 1}: {dots} dots after {actual.Cycles} cycles, expected {expectedDots}");
            }
            else
            {
                // Valid until the first frame ends; the log is shorter than a frame.
                long line = dots / 341 % 262;
                long dot = dots % 341;
                string ppu = $"{line},{dot}";
                string logPpu = $"{int.Parse(expected.Groups["line"].Value)},{int.Parse(expected.Groups["dot"].Value)}";
                Assert.True(ppu == logPpu, $"line {i + 1}: PPU {ppu}, the log says {logPpu}");
            }

            if (i < log.Length - 1)
            {
                nes.Step();
            }
        }

        // nestest leaves its error codes at $02 and $03: zero means every check passed.
        Assert.Equal(0, nes.Bus.Peek(0x02));
        Assert.Equal(0, nes.Bus.Peek(0x03));
    }

    [GeneratedRegex(@"^(?<pc>[0-9A-F]{4})  (?<bytes>(?:[0-9A-F]{2} ){1,3}) *(?<mark>[ *])(?<mnemonic>[A-Z]{3}) .*A:(?<a>[0-9A-F]{2}) X:(?<x>[0-9A-F]{2}) Y:(?<y>[0-9A-F]{2}) P:(?<p>[0-9A-F]{2}) SP:(?<s>[0-9A-F]{2}) PPU: *(?<line>\d+), *(?<dot>\d+) CYC:(?<cycles>\d+)$")]
    private static partial Regex LogLine();
}
