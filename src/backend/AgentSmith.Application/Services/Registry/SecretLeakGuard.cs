using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Defence against the model echoing a leaked secret: the LLM is only ever told
/// the placeholder convention, never a token, so a real
/// <see cref="RegistryConfig.Token"/> appearing verbatim in its output means the
/// model read one out of the repo. Such output is rejected — never written.
/// Runs on the RAW LLM output, before host-side substitution legitimately
/// injects the real token.
/// </summary>
public sealed class SecretLeakGuard
{
    public bool IsClean(string output, IReadOnlyList<RegistryConfig> registries) =>
        LeakedHosts(output, registries).Count == 0;

    /// <summary>
    /// The hosts whose configured token value appears verbatim in the output
    /// (empty when clean). Hosts, not tokens — so a rejection reason can name
    /// the registry without echoing the secret into logs.
    /// </summary>
    public IReadOnlyList<string> LeakedHosts(string output, IReadOnlyList<RegistryConfig> registries) =>
        registries
            .Where(r => !string.IsNullOrEmpty(r.Token)
                && output.Contains(r.Token, StringComparison.Ordinal))
            .Select(r => r.Host)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
