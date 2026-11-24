using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>2026-11-23-933dc: what the dependency scan of a PoC found — its vulnerabilities, its own notes (unpinned
/// lines, dependencies not checked), or the reason the scan could not run, which fails the run.</summary>
public sealed record PocDependencyResult(
    IReadOnlyList<PocVulnerability> Vulnerabilities, IReadOnlyList<PocScanFinding> Notes, string? Failure = null)
{
    public static PocDependencyResult Failed(string reason) => new([], [], reason);
}
