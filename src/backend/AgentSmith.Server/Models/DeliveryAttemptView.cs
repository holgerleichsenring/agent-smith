namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-25-0099b: one attempt on the delivery timeline.
/// </summary>
/// <param name="Outcome">running, queued, waiting, delivered, shortfall, cancelled or failed.</param>
/// <param name="Cause">Why it ended: the run's EndKind, else its status word; null while it has not ended.</param>
/// <param name="Rework">A person asked for the change: a ticket comment, a pull-request review or a status move back.</param>
public sealed record DeliveryAttemptView(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    double Seconds,
    string Outcome,
    string? Cause,
    bool Rework,
    decimal CostUsd,
    int SpecsDone,
    int SpecsTotal);
