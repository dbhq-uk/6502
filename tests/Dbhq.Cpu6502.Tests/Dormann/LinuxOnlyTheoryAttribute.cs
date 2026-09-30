using Xunit;

namespace Dbhq.Cpu6502.Tests.Dormann;

/// <summary>
/// Dormann's assembler is a 32-bit Linux program. These tests run on Linux,
/// locally and in CI, and are skipped elsewhere with the reason. On Linux a
/// missing library fails the test with the command that installs it.
/// </summary>
public sealed class LinuxOnlyTheoryAttribute : TheoryAttribute
{
    public LinuxOnlyTheoryAttribute()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip = "Dormann's assembler, as65, runs on Linux only; CI runs these";
        }
    }
}
