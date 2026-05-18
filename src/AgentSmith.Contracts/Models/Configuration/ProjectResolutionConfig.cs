namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Declares how a webhook handler resolves an incoming ticket to this project.
/// Required on every project trigger block (github/gitlab/azuredevops/jira). Without it
/// the handler has no way to route, since a single tracker may host work for many
/// agent-smith projects.
/// </summary>
public sealed record ProjectResolutionConfig
{
    public ResolutionStrategy Strategy { get; init; } = ResolutionStrategy.Tag;

    /// <summary>
    /// Strategy-specific match value. For tag/to_address: exact case-insensitive match.
    /// For area_path: hierarchical prefix match against the normalised backslash form.
    /// For repo: exact case-insensitive URL match against the project's single repo.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}
