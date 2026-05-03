namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Compact projection of a skill's frontmatter, written into _index/&lt;category&gt;.yaml
/// and consumed by the triage step as input. Body content is excluded — triage decides
/// role + phase from frontmatter alone.
/// </summary>
public sealed record SkillIndexEntry(
    string Name,
    string Description,
    IReadOnlyList<SkillRole> RolesSupported,
    ActivationCriteria Activation,
    IReadOnlyList<RoleAssignment> RoleAssignments,
    IReadOnlyDictionary<SkillRole, OutputForm> OutputType);
