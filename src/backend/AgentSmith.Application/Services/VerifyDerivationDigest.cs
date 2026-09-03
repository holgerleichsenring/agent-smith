using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-01-e14d: hashes the files a repository's verify block was derived from.
/// <para>
/// One class for both sides on purpose. The write path stamps the digest into the
/// declaration and a later run recomputes it; two implementations of "the same bytes"
/// would drift into a permanent false alarm, and a report that cries wolf every run is
/// the one outcome that teaches an operator to ignore it.
/// </para>
/// <para>
/// The digest covers the declared PATHS as well as their content, so a renamed pipeline
/// file is a move the report sees. A declared path that is not in the tree hashes as
/// absent, which makes both its deletion and its later reappearance visible.
/// </para>
/// </summary>
public sealed class VerifyDerivationDigest(ISandboxFileReaderFactory readerFactory)
{
    /// <summary>Stands for a declared path that is not in the tree — distinct from empty.</summary>
    private const string Absent = "(absent)";

    /// <summary>
    /// The digest of <paramref name="files"/>, each resolved from the repository root
    /// (2026-09-03-7bac: where the declaration that names them is written), in
    /// declaration order.
    /// </summary>
    public async Task<string> ComputeAsync(
        ISandbox sandbox, IReadOnlyList<string> files, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(files);
        var reader = readerFactory.Create(sandbox);
        var builder = new StringBuilder();
        foreach (var file in files)
        {
            var content = await reader.TryReadAsync(FromRoot(file), ct);
            builder.Append(file).Append('\n').Append(Normalised(content)).Append('\n');
        }
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    // Line endings are normalised because a checkout that converts them is not a change
    // to the pipeline. The digest would otherwise differ between a clone made with one
    // eol setting and a clone made with another, and report every run of one of them.
    private static string Normalised(string? content) =>
        content is null ? Absent : content.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string FromRoot(string relative)
    {
        var trimmed = relative.Trim().Replace('\\', '/').TrimStart('/');
        return $"{Repository.SandboxWorkPath.TrimEnd('/')}/{trimmed}";
    }
}
