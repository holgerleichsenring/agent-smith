namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-25-0099b: one attempt of a ticket as the delivery overview reads it — the run's light columns and the
/// channel of the act that started it, null when the run never fetched its ticket.
/// </summary>
public sealed record DeliveryAttemptRow(
    string RunId,
    string Project,
    string TicketId,
    string? TicketTitle,
    string Status,
    string? EndKind,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    double? DurationSeconds,
    decimal CostUsd,
    string? CauseChannel);
