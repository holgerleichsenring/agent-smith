using AgentSmith.Application.Services.Archives;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>
/// 2026-11-23-933db: the Dockerfile at the archive's root (after the strip), inflated held to
/// pocs.archive.max_dockerfile_kb and its CRC-32 — the one entry the upload inflates — and checked by
/// <see cref="DockerfileRules"/>.
/// </summary>
public sealed class PocRootDockerfile(BoundedZipInflater inflater, DockerfileRules rules)
{
    internal PocDockerfileCheck Check(IReadOnlyList<PocArchiveItem> items, PocArchiveLimits limits)
    {
        if (items.FirstOrDefault(IsRootDockerfile) is not { } item) return new PocDockerfileCheck(Missing, null);
        var bound = DockerfileRules.MaxBytes(limits);
        if (item.Size > bound) return new PocDockerfileCheck(DockerfileRules.TooLarge(item.Size, limits), null);
        var inflated = inflater.Inflate(item.Entry, bound);
        return inflated.Fault switch
        {
            ZipInflationFault.OverBound => new PocDockerfileCheck(DockerfileRules.TooLarge(bound + 1, limits), null),
            ZipInflationFault.ChecksumMismatch => new PocDockerfileCheck(new PocRuleRefusal(PocArchiveChecks.Dockerfile,
                "the Dockerfile does not match its own checksum; the archive is damaged or misstates its size"), null),
            _ => rules.Check(inflated.Bytes, limits),
        };
    }

    /// <summary>The intake's check of the Dockerfile it already inflated.</summary>
    internal PocDockerfileCheck Check(IReadOnlyList<PocInflatedFile> files, PocArchiveLimits limits) =>
        files.FirstOrDefault(f => f.Path == DockerfileRules.FileName) is { } file
            ? rules.Check(file.Bytes, limits)
            : new PocDockerfileCheck(Missing, null);

    private static bool IsRootDockerfile(PocArchiveItem item) => !item.IsDirectory && item.Path == DockerfileRules.FileName;

    private static readonly PocRuleRefusal Missing = new(PocArchiveChecks.Dockerfile,
        "the archive holds no Dockerfile at its root (after its one top folder); a PoC is built from it");
}
