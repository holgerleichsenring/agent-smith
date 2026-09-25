using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Config-driven model registry that maps task types to model assignments.
/// Falls back to Primary when Reasoning or ContextGeneration is not configured, and to Scout
/// when CodeMapGeneration is not — the sweep's measured binding (p0374).
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
            TaskType.ContextGeneration => config.ContextGeneration ?? config.Primary,
            // 2026-09-25-2fa7: SCOUT, not Primary. p0374 measured the repository sweep onto the
            // scout model after 450k+ tokens a run went through the flagship one; wiring this role
            // must not quietly undo that, so an operator who sets nothing keeps exactly that.
            TaskType.CodeMapGeneration => config.CodeMapGeneration ?? config.Scout,
            _ => config.Primary
        };

        logger.LogDebug(
            "Model registry: {TaskType} → {Model} (max {MaxTokens} tokens)",
            taskType, assignment.Model, assignment.MaxTokens);

        return assignment;
    }
}
