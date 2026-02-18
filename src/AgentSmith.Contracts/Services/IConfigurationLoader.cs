using AgentSmith.Contracts.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Loads and deserializes the agent-smith configuration file.
/// </summary>
public interface IConfigurationLoader
{
    AgentSmithConfig LoadConfig(string configPath);
}
