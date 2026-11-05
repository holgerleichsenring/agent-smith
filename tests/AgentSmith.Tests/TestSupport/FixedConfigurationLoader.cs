using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.TestSupport;

/// <summary>A configuration store that always reads back the one configuration it was given.</summary>
internal sealed class FixedConfigurationLoader(AgentSmithConfig config) : IConfigurationLoader
{
    public AgentSmithConfig LoadConfig(string configPath) => config;

    public ConfigFileReadFact? LastRead => null;
}
