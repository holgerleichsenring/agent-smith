namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Compact projection of a skill's frontmatter, written into
/// <c>_index/&lt;category&gt;.yaml</c> and consumed by the triage step as input.
/// p0131a: shape simplified to the new SKILL.md fields — <c>Role</c>
/// (producer/investigator/judge/filter, single role per skill),
/// <c>OutputSchema</c> (observation/plan/diff/bootstrap), and the optional
/// <c>ActivatesWhen</c> boolean expression. Legacy fields (RolesSupported,
/// ActivationCriteria-bag, RoleAssignments, OutputType) retired together
/// with the multi-role skill format in p0127c.
/// </summary>
public sealed record SkillIndexEntry(
    string Name,
    string Description,
    string Role,
    string? OutputSchema,
    string? ActivatesWhen);
