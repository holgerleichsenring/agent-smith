using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-25-9992h: what a GitHub App machine identity needs from GitHub — an installation token for the repository's host,
/// and the App's own login (<c>{slug}[bot]</c>), which an installation token cannot read from <c>GET /user</c>. Synchronous,
/// because <see cref="ICredentialResolver"/> is; a failure throws <see cref="Exceptions.MachineCredentialException"/>.
/// </summary>
public interface IMachineTokens
{
    string InstallationToken(RepoConnection repo, MachineIdentity identity, string privateKeyPem);

    string AppLogin(RepoConnection repo, MachineIdentity identity, string privateKeyPem);
}
