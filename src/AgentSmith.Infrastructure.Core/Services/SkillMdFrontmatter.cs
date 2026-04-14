namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Frontmatter model for SKILL.md files using hyphenated naming convention.
/// </summary>
internal sealed class SkillMdFrontmatter
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Emoji { get; set; }
    public string? Description { get; set; }
    public List<string>? Triggers { get; set; }
    public string? Version { get; set; }
    public string? AllowedTools { get; set; }
}
