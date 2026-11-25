using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>2026-11-25-d0e2: the process's <see cref="IMachineTokens"/> — GitHub App tokens from GitHub, service principal
/// tokens from Microsoft Entra.</summary>
public sealed class MachineTokens(GitHubAppTokens github, EntraPrincipalTokens entra) : IMachineTokens
{
    public string InstallationToken(MachineTarget target, MachineIdentity identity, string privateKeyPem) =>
        github.InstallationToken(target, identity, privateKeyPem);

    public string AppLogin(MachineTarget target, MachineIdentity identity, string privateKeyPem) =>
        github.AppLogin(target, identity, privateKeyPem);

    public string PrincipalToken(MachineTarget target, MachineIdentity identity, string clientSecret) =>
        entra.Token(target, identity, clientSecret);
}
