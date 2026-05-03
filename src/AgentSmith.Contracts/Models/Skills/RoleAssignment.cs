namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Per-role activation criteria. A skill may declare different positive/negative
/// keys for different roles — e.g. architect is lead when pattern-decision is primary,
/// reviewer when layer-touch is present.
/// </summary>
public sealed record RoleAssignment(SkillRole Role, ActivationCriteria Criteria);
