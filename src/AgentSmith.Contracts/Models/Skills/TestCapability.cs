namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Project test setup as projected onto the triage input.
/// Derived from ProjectMap.TestProjects + Ci by ProjectMapExcerptBuilder.
/// </summary>
public sealed record TestCapability(
    bool HasTestSetup,
    string? TestCommand,
    bool RunnableInPipeline);
