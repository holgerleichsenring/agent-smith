using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Migration.Services;

/// <summary>2026-11-22-747da: the configuration `database migrate` was pointed at with --config.</summary>
public sealed class FixedConfigStoreLocation(string configPath) : IConfigStoreLocation
{
    public string ConfigPath { get; } = configPath;
}
