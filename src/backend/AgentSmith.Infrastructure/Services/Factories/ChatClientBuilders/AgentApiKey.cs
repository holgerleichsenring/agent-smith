using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// 2026-11-18-9ae8: an agent's provider key — its own api_key_secret first, then the provider's variables in order,
/// each resolved by <see cref="ICredentialResolver.Named"/>: the environment variable when it holds a value, else the
/// catalog secret of that name, whose value may be the one stored in the admin area.
/// </summary>
public sealed class AgentApiKey(ICredentialResolver credentials)
{
    public string? Resolve(AgentConfig agent, params string[] fallbacks) =>
        Own(agent) ?? fallbacks.Select(credentials.Named).FirstOrDefault(value => value is not null);

    /// <summary>The value of the agent's own api_key_secret; null when it names none or that has no value.</summary>
    public string? Own(AgentConfig agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return string.IsNullOrEmpty(agent.ApiKeySecret) ? null : credentials.Named(agent.ApiKeySecret);
    }
}
