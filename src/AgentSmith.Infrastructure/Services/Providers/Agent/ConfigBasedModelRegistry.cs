using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Config-driven model registry that maps task types to model assignments.
/// Falls back to Primary model when Reasoning is not configured.
/// </summary>
public sealed class ConfigBasedModelRegistry(
    ModelRegistryConfig config,
    ILogger logger) : IModelRegistry
{
    public ModelAssignment GetModel(TaskType taskType)
    {
        var assignment = taskType switch
        {
            TaskType.Scout => config.Scout,
            TaskType.Primary => config.Primary,
            TaskType.Planning => config.Planning,
            TaskType.Reasoning => config.Reasoning ?? config.Primary,
            TaskType.Summarization => config.Summarization,
            _ => config.Primary
        };

        logger.LogDebug(
            "Model registry: {TaskType} → {Model} (max {MaxTokens} tokens)",
            taskType, assignment.Model, assignment.MaxTokens);

        return assignment;
    }
}
