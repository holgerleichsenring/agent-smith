using AgentSmith.Contracts.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Resolves model assignments based on task type.
/// Enables cost-efficient model routing (e.g. Haiku for discovery, Sonnet for coding).
/// </summary>
public interface IModelRegistry
{
    ModelAssignment GetModel(TaskType taskType);
}
