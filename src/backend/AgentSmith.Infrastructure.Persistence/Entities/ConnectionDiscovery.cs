namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-10-02-5ab2a: one connection's repo discovery — the last success (its repos and when)
/// and the last attempt with its error. Two disjoint halves: a success owns ReposJson and
/// DiscoveredAt, a failure owns LastError, and both stamp LastAttemptAt, so neither erases the
/// other's half whichever replica writes last. A row, not a Redis hash, so a flush loses
/// neither the repos nor the reason the last attempt failed.
/// </summary>
public sealed class ConnectionDiscovery : EntityBase
{
    /// <summary>The connection's name, lower-cased — the key.</summary>
    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>The repos of the last success, as JSON; null until one succeeded.</summary>
    public string? ReposJson { get; set; }

    public DateTimeOffset? DiscoveredAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>Why the last attempt failed; null when it succeeded.</summary>
    public string? LastError { get; set; }
}
