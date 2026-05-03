namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Orchestration metadata declared in a skill's agentsmith.md.
/// Defines role, execution order, output type, and parallelism.
/// </summary>
public sealed record SkillOrchestration(
    OrchestrationRole Role,
    SkillOutputType Output,
    IReadOnlyList<string> RunsAfter,
    IReadOnlyList<string> RunsBefore,
    IReadOnlyList<string> ParallelWith,
    IReadOnlyList<string> InputCategories)
{
    /// <summary>Default orchestration for skills without an explicit orchestration block.</summary>
    public static SkillOrchestration DefaultContributor => new(
        OrchestrationRole.Contributor,
        SkillOutputType.Artifact,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());
}
