using System.Security.Cryptography;
using System.Text;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0505: content hash of one fixture shape's SOURCE tree. Byte-identical rule to
/// tools/measure_data_toolchain_emit.py — skip the generated names, sort relative
/// paths ordinally, hash "&lt;relpath&gt;\n&lt;sha256-of-bytes&gt;\n" per file, sha256 the
/// concatenation. A fixture edited without a re-measurement moves this hash and
/// the table's recorded one stops matching.
/// </summary>
public sealed class DataToolchainFixtureHash
{
    private static readonly string[] SkipDirectories = ["target", "dbt_packages", "logs", ".git"];
    private static readonly string[] SkipFiles = [".user.yml", "package-lock.yml"];

    public string Compute(string shapeDirectory)
    {
        var buffer = new StringBuilder();
        foreach (var (relative, digest) in Entries(shapeDirectory).OrderBy(e => e.Relative, StringComparer.Ordinal))
            buffer.Append(relative).Append('\n').Append(digest).Append('\n');
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(buffer.ToString())));
    }

    private static IEnumerable<(string Relative, string Digest)> Entries(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (IsGenerated(relative)) continue;
            yield return (relative, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))));
        }
    }

    private static bool IsGenerated(string relative)
    {
        var segments = relative.Split('/');
        return segments[..^1].Any(SkipDirectories.Contains) || SkipFiles.Contains(segments[^1]);
    }
}
