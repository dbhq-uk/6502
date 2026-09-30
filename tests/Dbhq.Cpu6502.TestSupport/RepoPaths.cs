namespace Dbhq.Cpu6502.TestSupport;

public static class RepoPaths
{
    /// <summary>The repository root: the nearest directory above the running binaries that holds 6502.slnx.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Downloaded and generated test data. Git ignores it.</summary>
    public static string TestData => Path.Combine(Root, ".testdata");

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "6502.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No 6502.slnx above {AppContext.BaseDirectory}");
    }
}
