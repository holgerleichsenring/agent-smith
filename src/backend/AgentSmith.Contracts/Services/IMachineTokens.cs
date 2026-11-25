using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-25-9992h: what a GitHub App machine identity needs from GitHub — an installation token for the target's host,
/// and the App's own login (<c>{slug}[bot]</c>), which an installation token cannot read from <c>GET /user</c>. Synchronous,
/// because <see cref="ICredentialResolver"/> is; a failure throws <see cref="Exceptions.MachineCredentialException"/>.
/// <para>2026-11-25-d0e2: and a Microsoft Entra service principal's access token for Azure DevOps. The target is the entity
/// that acts and the url its host is read from, so a repository and a tracker mint alike.</para>
/// </summary>
public interface IMachineTokens
{
    string InstallationToken(MachineTarget target, MachineIdentity identity, string privateKeyPem);

    string AppLogin(MachineTarget target, MachineIdentity identity, string privateKeyPem);

    string PrincipalToken(MachineTarget target, MachineIdentity identity, string clientSecret);
}

/// <summary>2026-11-25-d0e2: who mints (<see cref="Entity"/>, as a refusal names it) and the url whose host answers.</summary>
public sealed record MachineTarget(string Entity, string? Url);
