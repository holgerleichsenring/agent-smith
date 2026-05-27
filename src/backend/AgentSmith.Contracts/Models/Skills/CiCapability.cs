namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Project CI/CD setup as projected onto the triage input.
/// Derived from ProjectMap.Ci by ProjectMapExcerptBuilder.
/// </summary>
public sealed record CiCapability(
    bool HasPipeline,
    string? DeploymentTarget);
