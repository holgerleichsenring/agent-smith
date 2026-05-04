namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Skills assigned to a single pipeline phase by triage.
/// At most one Lead and one Filter per phase; Analysts and Reviewers are unordered lists.
/// All members are skill names (not roles) and must reference loaded skills whose
/// roles_supported contains the corresponding SkillRole.
/// </summary>
public sealed record PhaseAssignment(
    string? Lead,
    IReadOnlyList<string> Analysts,
    IReadOnlyList<string> Reviewers,
    string? Filter);
