using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Dbhq.Cpu6502.TestSupport;

/// <summary>Downloads a third-party file once, checks it, and keeps it under .testdata.</summary>
public static class PinnedFiles
{
    private static readonly HttpClient Http = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        Timeout = TimeSpan.FromMinutes(5),
    };

    private static readonly ConcurrentDictionary<string, Lazy<string>> Fetched = new();

    /// <summary>
    /// Returns the local path of the file at url, stored at relativePath under
    /// .testdata. A cached copy is checked again before it is used, and a copy
    /// that fails the check is downloaded again.
    /// </summary>
    public static string Fetch(string url, string relativePath, Func<byte[], bool> verify)
    {
        string path = Path.Combine(RepoPaths.TestData, relativePath);
        return Fetched.GetOrAdd(path, _ => new Lazy<string>(() => Download(url, path, verify))).Value;
    }

    public static Func<byte[], bool> Sha256(string hex) =>
        bytes => Convert.ToHexStringLower(SHA256.HashData(bytes)) == hex;

    /// <summary>A file's git blob hash, which GitHub's tree API gives for every file at a commit.</summary>
    public static Func<byte[], bool> GitBlob(string hex) => bytes =>
    {
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        sha1.AppendData(Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"));
        sha1.AppendData(bytes);
        return Convert.ToHexStringLower(sha1.GetHashAndReset()) == hex;
    };

    private static string Download(string url, string path, Func<byte[], bool> verify)
    {
        if (File.Exists(path) && verify(File.ReadAllBytes(path)))
        {
            return path;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] bytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        if (!verify(bytes))
        {
            throw new InvalidDataException($"{url} does not match its pinned hash");
        }

        string partial = path + ".partial";
        File.WriteAllBytes(partial, bytes);
        File.Move(partial, path, overwrite: true);
        return path;
    }
}
