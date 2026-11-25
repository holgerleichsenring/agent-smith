namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-25-9992h: the machine identity one repository acts under, resolved from its project's <c>machine:</c> block —
/// the commit author (<see cref="Name"/>, <see cref="Email"/>) and the secret behind its token (<see cref="Auth"/>). With
/// <see cref="AppId"/> it is a GitHub App, and <see cref="Auth"/> holds the App's private key.
/// </summary>
public sealed record MachineIdentity(string Name, string Email, string Auth, string? AppId = null, string? InstallationId = null)
{
    public bool IsGitHubApp => AppId is not null;
}
