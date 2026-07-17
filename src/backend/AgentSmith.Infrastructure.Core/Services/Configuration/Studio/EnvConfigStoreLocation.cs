using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// Default file-store location: the same CONFIG_PATH the server resolves at
/// startup, falling back to the in-container default.
/// </summary>
public sealed class EnvConfigStoreLocation : IConfigStoreLocation
{
    public string ConfigPath { get; } =
        Environment.GetEnvironmentVariable("CONFIG_PATH") ?? "/app/config/agentsmith.yml";
}
