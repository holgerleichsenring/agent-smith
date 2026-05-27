namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// p0131a: maps the SKILL.md frontmatter <c>role</c> string (producer /
/// investigator / judge / filter) to the typed pipeline-side
/// <see cref="SkillRole"/> enum (Lead / Analyst / Reviewer / Filter).
/// </summary>
public static class SkillRoleMapping
{
    public static SkillRole ToSkillRole(string role) => role switch
    {
        "producer" => SkillRole.Lead,
        "investigator" => SkillRole.Analyst,
        "judge" => SkillRole.Reviewer,
        "filter" => SkillRole.Filter,
        _ => SkillRole.Analyst,
    };
}
