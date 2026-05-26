namespace AgentSmith.Contracts.Models;

/// <summary>
/// Aggregated result from dependency vulnerability auditing.
/// </summary>
public sealed record DependencyAuditResult(
    IReadOnlyList<DependencyFinding> Findings,
    string Ecosystem,
    int DurationMilliseconds);
