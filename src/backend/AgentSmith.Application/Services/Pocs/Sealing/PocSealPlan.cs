using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Sealing;

/// <summary>2026-11-23-933dg: what one PoC's seal is made of — the provider, the namespace, its secret variables and, with
/// pull_mode pull_secret, the dockerconfigjson of the pull secret and the registry inputs of the fingerprint.</summary>
public sealed record PocSealPlan(
    IPocSecretProvider Provider, string Namespace, IReadOnlyDictionary<string, SecretInput> Secrets, string PullMode,
    SecretInput? PullSecret = null, string? LoginServer = null, string? PullUser = null, SecretInput? PullToken = null)
{
    public bool HasAnything => Secrets.Count > 0 || PullSecret is not null;

    public PocSealInputs Inputs(string certificateFingerprint) =>
        new(Provider.Name, certificateFingerprint, Namespace, Secrets, PullMode, LoginServer, PullUser, PullToken);
}
