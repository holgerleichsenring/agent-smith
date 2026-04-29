namespace AgentSmith.Contracts.Services;

/// <summary>
/// Loads a YAML baseline (e.g. api-headers) from the current content root,
/// with AGENTSMITH_CONFIG_DIR override on top. Returns null when the file
/// is absent — callers degrade gracefully.
/// </summary>
public interface IBaselineLoader
{
    string? Load(string baselineName);
}
