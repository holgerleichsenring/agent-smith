using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the code scan of one revision — its checked files, the in-process pattern scan, the sandboxed
/// dependency scan, and the verdict of the block rules. A scan that could not run is a failure, never a clean verdict.
/// </summary>
public sealed class PocCodeScanner(
    PocRevisionFiles revisionFiles, IPocSecretScan secrets, PocDependencyScan dependencies, PocBlockRules rules)
{
    public async Task<(PocScanVerdict? Verdict, string? Failure)> ScanAsync(
        ResolvedProject project, PocScanSubject subject, PocScanConfig config, CancellationToken ct)
    {
        var (files, unreadable) = await revisionFiles.ReadAsync(subject, ct);
        if (unreadable is not null) return (null, unreadable);
        var patterns = secrets.Scan(files);
        if (patterns.Failure is not null) return (null, patterns.Failure);
        var packages = await dependencies.ScanAsync(project, files, config, ct);
        if (packages.Failure is not null) return (null, packages.Failure);
        return (rules.Decide(patterns.Hits, packages.Vulnerabilities, [.. patterns.Notes, .. packages.Notes], config), null);
    }
}
