namespace AgentSmith.Contracts.Models;

/// <summary>
/// Aggregated result from git history secret scanning.
/// </summary>
public sealed record GitHistoryScanResult(
    IReadOnlyList<HistoryFinding> Findings,
    int CommitsScanned,
    int DurationMilliseconds);
