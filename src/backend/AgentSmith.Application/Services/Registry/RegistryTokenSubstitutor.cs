using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Replaces every <c>__AS_TOKEN_&lt;host&gt;__</c> placeholder in a templated
/// auth-config file with the real <see cref="RegistryConfig.Token"/> matched by
/// host, host-side, just before the file is written. A placeholder whose host
/// has no configured registry fails the whole file rather than writing an empty
/// or half-substituted auth config that silently breaks restore.
/// </summary>
public sealed class RegistryTokenSubstitutor
{
    public TokenSubstitutionResult Substitute(string content, IReadOnlyList<RegistryConfig> registries)
    {
        var unmatched = RegistryTokenPlaceholder.HostsIn(content)
            .Where(host => FindToken(host, registries) is null)
            .ToList();
        if (unmatched.Count > 0)
            return TokenSubstitutionResult.Fail(
                $"placeholder host(s) with no configured registry: [{string.Join(", ", unmatched)}]");

        var substituted = RegistryTokenPlaceholder.Replace(content, host => FindToken(host, registries));
        return TokenSubstitutionResult.Ok(substituted);
    }

    private static string? FindToken(string host, IReadOnlyList<RegistryConfig> registries) =>
        registries.FirstOrDefault(r =>
            string.Equals(r.Host, host, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(r.Token))?.Token;
}
