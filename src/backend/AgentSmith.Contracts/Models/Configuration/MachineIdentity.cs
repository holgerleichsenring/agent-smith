namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-25-9992h: the machine identity one repository acts under, resolved from its project's <c>machine:</c> block —
/// the commit author (<see cref="Name"/>, <see cref="Email"/>) and the secret behind its token (<see cref="Auth"/>). With
/// <see cref="AppId"/> it is a GitHub App, and <see cref="Auth"/> holds the App's private key.
/// <para>2026-11-25-d0e2: with <see cref="TenantId"/> and <see cref="ClientId"/> it is a Microsoft Entra service principal, and
/// <see cref="Auth"/> holds its client secret.</para>
/// </summary>
public sealed record MachineIdentity(string Name, string Email, string Auth, string? AppId = null, string? InstallationId = null,
    string? TenantId = null, string? ClientId = null)
{
    public bool IsGitHubApp => AppId is not null;

    public bool IsAzurePrincipal => TenantId is not null && ClientId is not null;
}
