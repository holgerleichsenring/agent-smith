namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Plan-phase output that travels from the Plan-phase Lead into Review-phase prompts as {{plan}}.
/// Reviewers without an assigned lead in the same run see a null PlanArtifact and run as generic reviewers.
/// </summary>
public sealed record PlanArtifact(
    string? LeadSkill,
    IReadOnlyList<SkillObservation> Observations,
    DateTimeOffset CreatedAt);
