using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Config-driven model registry that maps task types to one agent's model assignments,
/// through <see cref="ModelRoleChain"/>: an unset role inherits, it never falls back to a
/// model id of some other provider.
/// </summary>
public sealed class ConfigBasedModelRegistry(
    AgentConfig agent,
    ILogger logger) : IModelRegistry
{
    private readonly ModelRoleChain _chain = new(agent);

    public ModelAssignment GetModel(TaskType taskType)
    {
        var assignment = _chain.For(taskType);
        ModelApi.RefuseOutsideOpenAi(assignment.ProviderType ?? agent.Type, assignment); // 2026-11-25-9992k

        logger.LogDebug(
            "Model registry: {TaskType} → {Model} (max {MaxTokens} tokens)",
            taskType, assignment.Model, assignment.MaxTokens);

        return assignment;
    }
}
