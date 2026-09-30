using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Dbhq.Cpu6502;

namespace Dbhq.Cpu6502.TestSupport;

/// <summary>
/// Klaus Dormann's test programs, assembled at test time with his own
/// assembler, as65, from a pinned commit. They are GPL, so they are never
/// committed here.
/// </summary>
public static partial class Dormann
{
    /// <summary>One assembled build of one program.</summary>
    /// <param name="Success">The address of the "test passed" trap, from the listing. Null for the decimal test, which reports through its ERROR byte.</param>
    public sealed record Program(string Name, byte[] Memory, ushort Start, ushort? Success);

    private const string Base = "https://raw.githubusercontent.com/Klaus2m5/6502_65C02_functional_tests/" + Pins.DormannCommit + "/";

    /// <summary>The functional test: every documented instruction and mode. The 2A03 build leaves decimal mode out.</summary>
    public static Program Functional(CpuVariant variant) => Assemble(
        $"functional-{variant}",
        "6502_functional_test.a65",
        Pins.DormannFunctionalSha256,
        variant == CpuVariant.Ricoh2A03 ? new() { ["disable_decimal"] = "1" } : [],
        cmos: false,
        stopToJump: false,
        start: 0x0400);

    /// <summary>
    /// Bruce Clark's exhaustive decimal-mode test, with every flag checked.
    /// It ends on a 65C02 STP, which the NMOS chip does not have, so its end
    /// becomes jmp *. Only the 65C02 builds get as65's -x switch, which also
    /// turns jmp * into BRA, a two-byte no-op on the NMOS chip.
    /// </summary>
    public static Program Decimal(CpuVariant variant)
    {
        bool cmos = variant is CpuVariant.Synertek65C02 or CpuVariant.Rockwell65C02 or CpuVariant.Wdc65C02;
        return Assemble(
            $"decimal-{variant}",
            "6502_decimal_test.a65",
            Pins.DormannDecimalSha256,
            new() { ["cputype"] = cmos ? "1" : "0", ["chk_n"] = "1", ["chk_v"] = "1", ["chk_z"] = "1", ["chk_c"] = "1" },
            cmos,
            stopToJump: true,
            start: 0x0200);
    }

    /// <summary>
    /// The 65C02's added instructions. WAI and STP are left out on every
    /// variant, because they cannot be tested this way. On Synertek the
    /// bit-instruction opcodes are left out too: Dormann's test expects them
    /// to be one-byte no-ops, and Harte's Synertek data says otherwise. See
    /// docs/known-differences.md.
    /// </summary>
    public static Program Extended(CpuVariant variant) => Assemble(
        $"extended-{variant}",
        "65C02_extended_opcodes_test.a65c",
        Pins.DormannExtendedSha256,
        new() { ["wdc_op"] = "1", ["rkwl_wdc_op"] = variant == CpuVariant.Synertek65C02 ? "2" : "1" },
        cmos: true,
        stopToJump: false,
        start: 0x0400);

    /// <summary>
    /// Runs until the program loops on one address, which is how every
    /// Dormann test stops, whether it passed or failed.
    /// </summary>
    public static ushort RunToTrap(Cpu cpu, long maxCycles = 400_000_000)
    {
        while (cpu.Cycles < maxCycles)
        {
            ushort before = cpu.PC;
            cpu.Step();
            if (cpu.PC == before)
            {
                return before;
            }
        }

        throw new TimeoutException($"No trap within {maxCycles:N0} cycles; PC is ${cpu.PC:X4}");
    }

    private static Program Assemble(string name, string file, string sha256, Dictionary<string, string> settings, bool cmos, bool stopToJump, ushort start)
    {
        string assembler = Assembler();
        string source = File.ReadAllText(PinnedFiles.Fetch(Base + file, Path.Combine("dormann", file), PinnedFiles.Sha256(sha256)));
        foreach (var (setting, value) in settings)
        {
            var pattern = new Regex($@"(?m)^{setting}\s*=\s*\S+");
            if (pattern.Matches(source).Count != 1)
            {
                throw new InvalidDataException($"{file} does not set {setting} exactly once");
            }

            source = pattern.Replace(source, $"{setting} = {value}");
        }

        if (stopToJump)
        {
            if (EndOfTest().Matches(source).Count != 1)
            {
                throw new InvalidDataException($"{file} has no end_of_test macro ending in STP");
            }

            source = EndOfTest().Replace(source, "$1                jmp *");
        }

        string directory = Path.Combine(RepoPaths.TestData, "dormann", "build", name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, file), source);
        string[] arguments = ["-lprogram.lst", "-m", "-s2", "-w", "-h0", .. cmos ? new[] { "-x" } : [], "-oprogram.hex", file];
        var info = new ProcessStartInfo(assembler, arguments)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(info)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            string hint = output.Contains("error while loading shared libraries", StringComparison.Ordinal)
                ? " as65 is a 32-bit Linux program: sudo apt-get install libc6-i386 lib32stdc++6"
                : "";
            throw new InvalidOperationException($"as65 failed on {file} with exit code {process.ExitCode}.{hint}\n{output}");
        }

        byte[] memory = new byte[0x10000];
        IntelHex.Load(Path.Combine(directory, "program.hex"), memory);
        ushort? success = null;
        foreach (string line in File.ReadLines(Path.Combine(directory, "program.lst")))
        {
            var match = PassedTrap().Match(line);
            if (match.Success)
            {
                success = Convert.ToUInt16(match.Groups[1].Value, 16);
            }
        }

        return new Program(name, memory, start, success);
    }

    private static string Assembler()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("as65 is a 32-bit Linux program; Dormann's tests run on Linux only");
        }

        string zip = PinnedFiles.Fetch(Base + "as65_142.zip", Path.Combine("dormann", "as65_142.zip"), PinnedFiles.Sha256(Pins.DormannAssemblerSha256));
        string path = Path.Combine(RepoPaths.TestData, "dormann", "as65");
        lock (typeof(Dormann))
        {
            if (!File.Exists(path))
            {
                using var archive = ZipFile.OpenRead(zip);
                archive.GetEntry("as65")!.ExtractToFile(path);
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        return path;
    }

    [GeneratedRegex(@"(end_of_test macro[^\n]*\n)[^\n]*db\s+\$db[^\n]*")]
    private static partial Regex EndOfTest();

    [GeneratedRegex(@"^([0-9a-fA-F]{4}) :.*test passed")]
    private static partial Regex PassedTrap();
}
