namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-13-9a59n: a person's Retry of a run that ended on infrastructure. A retry leaves no trace on the
/// tracker for the run to read, so it is a row of the run it retries — one per run (RunId unique), replaced by
/// a later request, and deleted with that run. It stays a pending act of the ticket until an attempt starts
/// after it.
/// </summary>
public sealed class RunRetryRequest : EntityBase
{
    public long Id { get; set; }

    /// <summary>The retried run.</summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>The principal who asked.</summary>
    public string RequestedBy { get; set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; set; }
}
