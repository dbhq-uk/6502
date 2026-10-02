namespace Dbhq.Cpu6502.TestSupport;

public static class RepoPaths
{
    /// <summary>The repository root: the nearest directory above the running binaries that holds 6502.slnx.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>
    /// Reads a file committed to the repository and checks its SHA-256, so a ROM
    /// that has been altered fails loudly instead of running.
    /// </summary>
    public static byte[] ReadChecked(string relativePath, string sha256Hex)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(Root, relativePath));
        string actual = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        if (actual != sha256Hex)
        {
            throw new InvalidDataException($"{relativePath} does not match its pinned hash: {actual}");
        }

        return bytes;
    }

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
