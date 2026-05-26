using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Reads the active provider from <see cref="AgentSmithConfig.PrimaryProvider"/>.
/// Returns an empty string when unset, which signals "no provider overrides" to
/// <see cref="IProviderOverrideResolver"/>.
/// </summary>
public sealed class ActiveProviderResolver(AgentSmithConfig config) : IActiveProviderResolver
{
    public string GetActiveProvider() => config.PrimaryProvider ?? string.Empty;
}
