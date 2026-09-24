using System.Text.RegularExpressions;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Constants;

/// <summary>
/// 2026-09-23-4722b: the environment-variable names a spawned sandbox must receive — the canonical
/// <see cref="AgentEnvKeys"/> plus every <c>api_key_secret</c> a configured agent actually names.
///
/// An agent may name ANY variable and every chat-client builder honours it, but the spawners
/// forwarded a hardcoded list: an agent naming its own key worked in-process and silently failed to
/// authenticate inside a sandbox. A literal list also drifts — the Docker one had already lost
/// AZURE_OPENAI_API_KEY and GROQ_API_KEY. Deriving the set from the configuration keeps it in step
/// with the place the operator declares the name.
/// </summary>
public static partial class AgentSecretNames
{
    /// <summary>
    /// A Kubernetes secret key may hold only these characters, so a variable whose derived key
    /// would be illegal is refused by name instead of producing an invalid pod spec at spawn time.
    /// </summary>
    [GeneratedRegex("^[-._a-zA-Z0-9]+$")]
    private static partial Regex SecretKeyCharset { get; }

    /// <summary>
    /// Every canonical name, plus each distinct <c>api_key_secret</c> the agents name, in a stable
    /// order: the canonical set first, then the configured additions as they were declared.
    /// </summary>
    public static IReadOnlyList<string> For(AgentSmithConfig? config)
    {
        var names = new List<string>(AgentSecretBinding.All.Select(b => b.EnvVar));
        var seen = names.ToHashSet(StringComparer.Ordinal);

        foreach (var agent in config?.Agents.Values ?? Enumerable.Empty<AgentConfig>())
        {
            var named = agent.ApiKeySecret;
            if (string.IsNullOrWhiteSpace(named) || !seen.Add(named)) continue;
            if (!SecretKeyCharset.IsMatch(K8sSecretKeyFor(named)))
                throw new InvalidOperationException(
                    $"api_key_secret '{named}' cannot become a Kubernetes secret key: a name may hold "
                    + "only letters, digits, '-', '_' and '.'.");
            names.Add(named);
        }

        return names;
    }

    /// <summary>
    /// The key an environment variable takes inside the operator-managed secret. The eleven
    /// explicit pairs in <see cref="AgentSecretBinding.All"/> all agree with this derivation, and
    /// they keep their literal spelling regardless — they are the documented contract of a secret
    /// that is already deployed, and re-deriving them would rename live keys.
    /// </summary>
    public static string K8sSecretKeyFor(string envVar) =>
        AgentSecretBinding.All.FirstOrDefault(b =>
            string.Equals(b.EnvVar, envVar, StringComparison.Ordinal))?.K8sSecretKey
        ?? envVar.ToLowerInvariant().Replace('_', '-');
}
