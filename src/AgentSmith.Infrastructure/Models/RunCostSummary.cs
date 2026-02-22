namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// Cost breakdown for a single pipeline run, in USD.
/// </summary>
public sealed record RunCostSummary(
    IReadOnlyDictionary<string, PhaseCost> Phases,
    decimal TotalCost);

/// <summary>
/// Cost for a single execution phase.
/// </summary>
public sealed record PhaseCost(
    string Model,
    int InputTokens,
    int OutputTokens,
    int CacheReadTokens,
    int Iterations,
    decimal Cost);
