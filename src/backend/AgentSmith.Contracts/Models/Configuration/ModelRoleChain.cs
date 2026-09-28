using AgentSmith.Contracts.Providers;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Which model answers a task for one agent — the fallback chain, stated once so the
/// runtime, the run gate and the startup report cannot disagree about it. A role with no
/// model named is unset: primary inherits the agent's own <c>model</c>/<c>deployment</c>,
/// every other role inherits primary, and code-map generation follows scout before primary.
/// An inheriting role takes the assignment it inherits whole, max_tokens included.
/// </summary>
public sealed class ModelRoleChain(AgentConfig agent)
{
    /// <summary>The roles that carried a built-in Claude model before the chain existed. A
    /// models block that leaves one of them unset now inherits where it used to get Claude.</summary>
    private static readonly (string Role, Func<ModelRegistryConfig, ModelAssignment?> Read)[] FormerlyDefaulted =
    [
        ("primary", m => m.Primary),
        ("scout", m => m.Scout),
        ("planning", m => m.Planning),
        ("summarization", m => m.Summarization),
    ];

    public ModelAssignment For(TaskType task)
    {
        var models = agent.Models;
        return task switch
        {
            TaskType.Primary => Primary(),
            TaskType.Scout => Named(models?.Scout) ?? Primary(),
            TaskType.Planning => Named(models?.Planning) ?? Primary(),
            TaskType.Reasoning => Named(models?.Reasoning) ?? Primary(),
            TaskType.Summarization => Named(models?.Summarization) ?? Primary(),
            TaskType.ContextGeneration => Named(models?.ContextGeneration) ?? Primary(),
            TaskType.CodeMapGeneration => Named(models?.CodeMapGeneration) ?? Named(models?.Scout) ?? Primary(),
            _ => Primary(),
        };
    }

    /// <summary>The roles a declared <c>models:</c> block leaves to inherit that once carried a
    /// built-in Claude model — empty when there is no block, which always inherited everything.</summary>
    public IReadOnlyList<string> InheritingFormerDefaults() =>
        agent.Models is not { } models
            ? []
            : [.. FormerlyDefaulted.Where(role => Named(role.Read(models)) is null).Select(role => role.Role)];

    private ModelAssignment Primary() =>
        Named(agent.Models?.Primary)
        ?? new ModelAssignment
        {
            Model = agent.Model,
            Deployment = agent.Models?.Primary?.Deployment ?? agent.Deployment,
            MaxTokens = agent.Models?.Primary?.MaxTokens ?? new ModelAssignment().MaxTokens,
            ContextWindowTokens = agent.Models?.Primary?.ContextWindowTokens,
        };

    /// <summary>A role counts as set only when it names a model.</summary>
    private static ModelAssignment? Named(ModelAssignment? assignment) =>
        string.IsNullOrWhiteSpace(assignment?.Model) ? null : assignment;
}
