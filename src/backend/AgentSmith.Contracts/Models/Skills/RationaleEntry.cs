namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Parsed token from a TriageOutput rationale string.
/// Token grammar: <role>=<skill>:<key>; (positive) and -<skill>:<key>; (negative, role-less).
/// Role is null on negative entries; consumers infer role from context if needed.
/// </summary>
public sealed record RationaleEntry(
    SkillRole? Role,
    string Skill,
    string Key,
    bool Negative);
