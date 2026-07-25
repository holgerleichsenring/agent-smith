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
    public bool IsClean(string llmOutput, IReadOnlyList<RegistryConfig> registries) =>
        !LeakedTokens(llmOutput, registries).Any();

    /// <summary>The configured token values that appear verbatim in the output (empty when clean).</summary>
    public IReadOnlyList<string> LeakedTokens(string llmOutput, IReadOnlyList<RegistryConfig> registries) =>
        registries
            .Select(r => r.Token)
            .Where(token => !string.IsNullOrEmpty(token)
                && llmOutput.Contains(token, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
