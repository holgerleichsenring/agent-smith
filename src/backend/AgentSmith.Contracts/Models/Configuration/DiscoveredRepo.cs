namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0281a: one repository discovered under a <see cref="ResolvedConnection"/> via the
/// provider's list-repos API. The clone URL and the repo's real default branch come from
/// the provider, so neither has to be configured.
/// </summary>
public sealed record DiscoveredRepo
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string? DefaultBranch { get; init; }
}
