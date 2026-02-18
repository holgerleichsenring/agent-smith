using AgentSmith.Contracts.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Creates the appropriate IAgentProvider based on configuration.
/// </summary>
public interface IAgentProviderFactory
{
    IAgentProvider Create(AgentConfig config);
}
