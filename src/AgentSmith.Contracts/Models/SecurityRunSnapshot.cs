namespace AgentSmith.Contracts.Models;

/// <summary>
/// Snapshot of security scan results, committed to .agentsmith/security/ on the default branch.
/// Used for git-based trend analysis without needing old runs/ directories.
/// </summary>
public sealed record SecurityRunSnapshot(
    DateTimeOffset Date,
    string Branch,
    int FindingsCritical,
    int FindingsHigh,
    int FindingsMedium,
    int FindingsRetained,
    int FindingsAutoFixed,
    IReadOnlyList<string> ScanTypes,
    int NewSinceLast,
    int ResolvedSinceLast,
    IReadOnlyList<string> TopCategories,
    decimal CostUsd);
