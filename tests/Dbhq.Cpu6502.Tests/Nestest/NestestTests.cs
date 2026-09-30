using System.Text.RegularExpressions;
using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Cpu6502.Tests.Nestest;

/// <summary>
/// nestest in its automated mode needs no picture chip: the program at
/// $C000 runs on plain memory. Its log, from the Nintendulator emulator,
/// gives the state before every instruction, 8,991 of them.
/// </summary>
public sealed partial class NestestTests
{
    private const string Base = "https://raw.githubusercontent.com/christopherpow/nes-test-roms/" + Pins.NestestCommit + "/other/";

    [Fact]
    public void EveryLineOfTheLogMatches()
    {
        byte[] rom = File.ReadAllBytes(PinnedFiles.Fetch(Base + "nestest.nes", Path.Combine("nestest", "nestest.nes"), PinnedFiles.Sha256(Pins.NestestRomSha256)));
        string[] log = File.ReadAllLines(PinnedFiles.Fetch(Base + "nestest.log", Path.Combine("nestest", "nestest.log"), PinnedFiles.Sha256(Pins.NestestLogSha256)));
        var bus = new FlatBus { Recording = false };
        Array.Copy(rom, 16, bus.Memory, 0x8000, 0x4000);
        Array.Copy(rom, 16, bus.Memory, 0xC000, 0x4000);
        var cpu = new Cpu(bus, CpuVariant.Ricoh2A03) { PC = 0xC000, S = 0xFD, P = 0x24, Cycles = 7 };

        for (int i = 0; i < log.Length; i++)
        {
            Match expected = LogLine().Match(log[i]);
            Assert.True(expected.Success, $"line {i + 1} of nestest.log does not parse: {log[i]}");
            TraceLine actual = Tracer.Capture(cpu, address => bus.Memory[address]);
            string ours = $"{actual.PC:X4} {string.Join(' ', actual.Bytes.Select(b => b.ToString("X2")))} {(actual.Opcode.Undocumented ? "*" : "")}{actual.Opcode.Mnemonic} A:{actual.A:X2} X:{actual.X:X2} Y:{actual.Y:X2} P:{actual.P:X2} SP:{actual.S:X2} CYC:{actual.Cycles}";
            string theirs = $"{expected.Groups["pc"].Value} {expected.Groups["bytes"].Value.Trim()} {expected.Groups["mark"].Value.Trim()}{expected.Groups["mnemonic"].Value} A:{expected.Groups["a"].Value} X:{expected.Groups["x"].Value} Y:{expected.Groups["y"].Value} P:{expected.Groups["p"].Value} SP:{expected.Groups["s"].Value} CYC:{expected.Groups["cycles"].Value}";
            Assert.True(ours == theirs, $"line {i + 1}:\n  log:  {theirs}\n  ours: {ours}");
            if (i < log.Length - 1)
            {
                cpu.Step();
            }
        }

        // nestest leaves its error codes at $02 and $03: zero means every check passed.
        Assert.Equal(0, bus.Memory[0x02]);
        Assert.Equal(0, bus.Memory[0x03]);
    }

    [Fact]
    public void TheTraceLineIsNintendulatorsFormat()
    {
        var bus = new FlatBus();
        bus.Memory[0xC000] = 0x4C;
        bus.Memory[0xC001] = 0xF5;
        bus.Memory[0xC002] = 0xC5;
        var cpu = new Cpu(bus, CpuVariant.Ricoh2A03) { PC = 0xC000, S = 0xFD, P = 0x24, Cycles = 7 };

        string line = Tracer.Capture(cpu, address => bus.Memory[address]).ToString();

        Assert.Equal("C000  4C F5 C5  JMP $C5F5                       A:00 X:00 Y:00 P:24 SP:FD CYC:7", line);
    }

    [GeneratedRegex(@"^(?<pc>[0-9A-F]{4})  (?<bytes>(?:[0-9A-F]{2} ){1,3}) *(?<mark>[ *])(?<mnemonic>[A-Z]{3}) .*A:(?<a>[0-9A-F]{2}) X:(?<x>[0-9A-F]{2}) Y:(?<y>[0-9A-F]{2}) P:(?<p>[0-9A-F]{2}) SP:(?<s>[0-9A-F]{2}) .*CYC:(?<cycles>\d+)$")]
    private static partial Regex LogLine();
}
