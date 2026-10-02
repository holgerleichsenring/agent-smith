namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0345c: the discovery cache served to the config studio's repo picker
/// (<c>GET /api/config/connections/{id}/repos</c>). A connection that was never
/// discovered serves <see cref="DiscoveredAt"/> null + an empty list — the UI
/// says "not discovered yet" instead of guessing.
/// 2026-10-02-5f89c: beside the last success, the last attempt and its error, the repo count of
/// the last success, and whether a first discovery is still running.
/// </summary>
public sealed record ConnectionReposView(
    DateTimeOffset? DiscoveredAt, IReadOnlyList<ConnectionRepoView> Repos,
    DateTimeOffset? LastAttemptAt = null, string? LastError = null, int? RepoCount = null,
    bool Discovering = false);
