namespace AgentSmith.Contracts.Models;

/// <summary>
/// Trend analysis comparing the current security scan with the previous one.
/// </summary>
public sealed record SecurityTrend(
    int NewFindings,
    int ResolvedFindings,
    int CriticalDelta,
    int HighDelta,
    int TotalScans,
    decimal AverageCost,
    SecurityRunSnapshot? Previous,
    SecurityRunSnapshot Current);
