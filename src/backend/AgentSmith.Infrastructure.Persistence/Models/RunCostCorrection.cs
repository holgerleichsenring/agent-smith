namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-08-25-61f1: one run whose stored total was a sum of copies, and what it became
/// once the copies were gone. Reported by name and by both numbers so an operator can
/// reconcile a cost rollup that was published before the repair ran.
/// </summary>
public sealed record RunCostCorrection(string RunId, decimal Before, decimal After);
