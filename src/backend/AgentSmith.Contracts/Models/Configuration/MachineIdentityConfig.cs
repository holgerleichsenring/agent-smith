namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-25-9992h: the project's <c>machine:</c> block — the identity agent-smith acts under on each hoster. <see cref="Name"/>
/// is the actor its own records book acts under and, with <see cref="Email"/>, the author of every commit it makes; each hoster
/// entry names the secret its acts authenticate with. Absent: the project acts with each repo's own auth secret, and its setup
/// says so (<c>ProjectMachineFindings</c>).
/// </summary>
public sealed class MachineIdentityConfig
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public MachineHosterConfig? Github { get; set; }

    public MachineHosterConfig? Gitlab { get; set; }

    public MachineHosterConfig? AzureDevops { get; set; }

    /// <summary>The entry for a hoster; null for one the block does not name and for a local repository.</summary>
    public MachineHosterConfig? For(RepoType hoster) => hoster switch
    {
        RepoType.GitHub => Github,
        RepoType.GitLab => Gitlab,
        RepoType.AzureDevOps => AzureDevops,
        _ => null,
    };

    /// <summary>The identity a repository of <paramref name="hoster"/> acts under; null when the block names no entry for it.</summary>
    public MachineIdentity? IdentityFor(RepoType hoster) => For(hoster) is { } entry
        ? new MachineIdentity(Name.Trim(), string.IsNullOrWhiteSpace(entry.Email) ? Email.Trim() : entry.Email.Trim(),
            entry.Auth.Trim(), Blank(entry.AppId), Blank(entry.InstallationId), Blank(entry.TenantId), Blank(entry.ClientId))
        : null;

    /// <summary>2026-11-25-0873: the identity a tracker's writes run under — its hoster's entry; null for Jira, which has none.</summary>
    public MachineIdentity? IdentityFor(TrackerType tracker) => HosterOf(tracker) is { } hoster ? IdentityFor(hoster) : null;

    /// <summary>2026-11-25-0873: the hoster whose machine entry a tracker type acts under; null for Jira.</summary>
    public static RepoType? HosterOf(TrackerType tracker) => tracker switch
    {
        TrackerType.GitHub => RepoType.GitHub,
        TrackerType.GitLab => RepoType.GitLab,
        TrackerType.AzureDevOps => RepoType.AzureDevOps,
        _ => null,
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// 2026-11-25-9992h: one hoster's machine identity. <see cref="Auth"/> names the secret: a bot user's token (GitHub machine user,
/// GitLab project or group access token, Azure DevOps bot user PAT) — or, with <see cref="AppId"/> and
/// <see cref="InstallationId"/>, a GitHub App's private key (PEM), from which installation tokens are minted.
/// <see cref="Email"/> overrides the block's commit email on this hoster (a GitHub App's bot address).
/// <para>2026-11-25-d0e2: on Azure DevOps, <see cref="TenantId"/> and <see cref="ClientId"/> make the entry a service principal
/// and <see cref="Auth"/> its client secret, traded for Entra access tokens.</para>
/// </summary>
public sealed class MachineHosterConfig
{
    public string Auth { get; set; } = string.Empty;

    public string? AppId { get; set; }

    public string? InstallationId { get; set; }

    public string? Email { get; set; }

    public string? TenantId { get; set; }

    public string? ClientId { get; set; }
}
