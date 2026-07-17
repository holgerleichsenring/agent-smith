namespace AgentSmith.Contracts.Services;

/// <summary>
/// Where the file-backed config store reads and writes agentsmith.yml. Kept as a
/// port so the server can bind it to its resolved CONFIG_PATH and tests can point
/// at a temp file without touching the environment.
/// </summary>
public interface IConfigStoreLocation
{
    string ConfigPath { get; }
}
