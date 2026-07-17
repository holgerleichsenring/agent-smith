namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0345c: the discovery cache served to the config studio's repo picker
/// (<c>GET /api/config/connections/{id}/repos</c>). A connection that was never
/// discovered serves <see cref="DiscoveredAt"/> null + an empty list — the UI
/// says "not discovered yet" instead of guessing.
/// </summary>
public sealed record ConnectionReposView(
    DateTimeOffset? DiscoveredAt, IReadOnlyList<ConnectionRepoView> Repos);

/// <summary>One discovered repo: name + the provider-reported default branch.</summary>
public sealed record ConnectionRepoView(string Name, string? DefaultBranch);
