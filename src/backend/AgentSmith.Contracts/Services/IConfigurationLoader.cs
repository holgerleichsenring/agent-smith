using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Loads and deserializes the agent-smith configuration file.
/// </summary>
public interface IConfigurationLoader
{
    AgentSmithConfig LoadConfig(string configPath);

    /// <summary>
    /// p0345c: the last successful read this process performed — path + wall-clock
    /// time. Server-side fact for the dashboard's drift story ("file changed after
    /// last read"); null until the first successful load.
    /// </summary>
    ConfigFileReadFact? LastRead { get; }
}

/// <summary>When and from where the running process last loaded its configuration.</summary>
public sealed record ConfigFileReadFact(string Path, DateTimeOffset ReadAt);
