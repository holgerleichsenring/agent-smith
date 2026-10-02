namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-10-02-5f89c: what is known about one connection's repo discovery — the last success
/// (its repos and when) and the last attempt with its error, if that attempt failed. A failure
/// never erases the last success: a connection whose token was revoked still shows yesterday's
/// repos, with the reason beside them.
/// </summary>
public sealed record ConnectionDiscoveryStatus(
    DateTimeOffset? DiscoveredAt,
    IReadOnlyList<DiscoveredRepo> Repos,
    DateTimeOffset? LastAttemptAt,
    string? LastError)
{
    /// <summary>The number of repos of the last success, or null when none ever succeeded.</summary>
    public int? RepoCount => DiscoveredAt is null ? null : Repos.Count;
}
