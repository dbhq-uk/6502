using Dbhq.Cpu6502.TestSupport;
using Xunit;

namespace Dbhq.Cpu6502.Tests.ChipTrace;

/// <summary>
/// The site's chip page replays a trace of the core. The trace is committed so
/// the site can build without .NET, and this test is what keeps it honest: if
/// the core, the program or the tool changes, the committed file no longer
/// matches and this fails until the tool is run again.
/// </summary>
public sealed class ChipTraceTests
{
    [Fact]
    public void TheCommittedTraceIsWhatTheToolWritesToday()
    {
        string path = Path.Combine(RepoPaths.Root, Dbhq.Cpu6502.ChipTrace.ChipTrace.RelativePath);
        string committed = File.ReadAllText(path).ReplaceLineEndings("\n");
        string fresh = Dbhq.Cpu6502.ChipTrace.ChipTrace.Build();
        Assert.True(committed == fresh, "site/src/data/chip-trace.json is out of date: run `dotnet run --project tools/Dbhq.Cpu6502.ChipTrace` from the repository root");
    }
}
