namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0281a: a git-host connection that holds the shared host/org/auth ONCE so projects can
/// reference repos under it by glob/list instead of enumerating each repo. Repos are
/// DISCOVERED from the provider API (not declared); a project selects them with
/// <c>connection/pattern</c>. Reuses <see cref="RepoType"/> for the host kind (github |
/// gitlab | azure_devops); Local is not a connection type.
/// </summary>
public sealed record ResolvedConnection
{
    public string Name { get; init; } = string.Empty;
    public RepoType Type { get; init; } = RepoType.GitHub;

    /// <summary>Azure DevOps organisation (e.g. the org segment of dev.azure.com/{org}).</summary>
    public string? Organization { get; init; }

    /// <summary>Azure DevOps project (the team project that scopes the repos).</summary>
    public string? Project { get; init; }

    /// <summary>GitHub owner / org whose repos are listed.</summary>
    public string? Owner { get; init; }

    /// <summary>GitLab group whose projects are listed.</summary>
    public string? Group { get; init; }

    /// <summary>Optional host override for self-hosted GitLab/GitHub Enterprise (default cloud).</summary>
    public string? Host { get; init; }

    public string Auth { get; init; } = string.Empty;

    /// <summary>
    /// Connection-level default branch fallback used only until discovery reads each repo's
    /// real default branch from the provider. A repo's discovered default branch wins.
    /// </summary>
    public string? DefaultBranch { get; init; }
}
