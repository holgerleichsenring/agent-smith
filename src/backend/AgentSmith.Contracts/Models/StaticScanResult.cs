namespace AgentSmith.Contracts.Models;

/// <summary>
/// Aggregated result from static pattern scanning.
/// </summary>
public sealed record StaticScanResult(
    IReadOnlyList<PatternFinding> Findings,
    int FilesScanned,
    int PatternsApplied,
    int DurationMilliseconds);
