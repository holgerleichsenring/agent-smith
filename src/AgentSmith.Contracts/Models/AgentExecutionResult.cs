using AgentSmith.Domain.Entities;

namespace AgentSmith.Contracts.Models;

/// <summary>
/// Result of an agentic plan execution, carrying code changes, cost breakdown, and duration.
/// </summary>
public sealed record AgentExecutionResult(
    IReadOnlyList<CodeChange> Changes,
    RunCostSummary? CostSummary,
    int? DurationSeconds);
