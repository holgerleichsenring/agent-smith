using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Pocs.Sealing;

/// <summary>
/// 2026-11-23-933dg: gathers what a PoC's seal is made of, refusing before anything is sealed — an unknown provider,
/// pull_mode pull_secret without a master key, a secret variable whose stored value does not open (by name: sealing
/// without it would deploy the PoC missing it), and a pull token that is not there or does not open (naming
/// pull_auth, in the registry factory's words).
/// </summary>
public sealed class PocSealPlanner(
    IEnumerable<IPocSecretProvider> providers, IPocVariableValues values, ICredentialResolver credentials, IPocSealFingerprint fingerprint)
{
    public async Task<(PocSealPlan? Plan, string? Refusal)> PlanAsync(PocVariableSubject poc, PocTargetConfig target, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(target);
        if (providers.FirstOrDefault(p => p.Name == target.Secrets.Provider) is not { } provider)
            return (null, $"{PocSecretsConfig.Key}.provider of target '{target.Name}' is '{target.Secrets.Provider}'; it must be "
                + $"'{PocSecretsConfig.SealedSecrets}', the one secret provider agent smith writes so far.");
        if (target.Registry is not { } registry) return (null, $"{PocRegistryConfig.Key} of target '{target.Name}' is missing.");
        var pull = registry.PullMode == PocRegistryConfig.PullSecretMode;
        if (pull && !fingerprint.IsAvailable) return (null, PocSealKey.NeedsMasterKey(target.Name) + ".");
        var stored = await values.ReadAsync(poc.PocId, ct);
        if (stored.Unreadable.OrderBy(u => u.Key, StringComparer.Ordinal).FirstOrDefault() is { Key: { } name } unreadable)
            return (null, $"PoC variable {name} cannot be read: {unreadable.Value}; set it again.");
        var plan = new PocSealPlan(provider, PocNamespace.For(poc.Name), stored.Values, registry.PullMode);
        return pull ? WithPull(plan, registry) : (plan, null);
    }

    private (PocSealPlan?, string?) WithPull(PocSealPlan plan, PocRegistryConfig registry)
    {
        var lookup = credentials.Lookup(registry.PullAuth);
        if (lookup.State == CredentialLookupState.Unreadable)
            return (null, $"{PocRegistryConfig.Key}.pull_auth of registry {registry.LoginServer}: {lookup}");
        if (lookup.State != CredentialLookupState.Value)
            return (null, $"{PocRegistryConfig.Key}.pull_auth names '{registry.PullAuth}', which holds no value; set the "
                + $"pull-only token of registry {registry.LoginServer} there");
        var token = new Contracts.Models.SecretInput(lookup.Value!);
        return (plan with
        {
            PullSecret = DockerConfigJson.For(registry.LoginServer, registry.PullUser, token),
            LoginServer = registry.LoginServer, PullUser = registry.PullUser, PullToken = token,
        }, null);
    }
}
